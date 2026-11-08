using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.Core.Services.AI;

namespace UbuhlebethuConnectPro.Api.Controllers.Api
{
    // ─── Request / Response DTOs ──────────────────────────────────────────────

    /// <summary>Legacy single-turn request (backward compatible with mobile).</summary>
    public class ChatAskRequest
    {
        public string Question { get; set; } = string.Empty;
        public string? Provider { get; set; }
    }

    /// <summary>Legacy single-turn response.</summary>
    public class ChatAskResponse
    {
        public string? Answer { get; set; }
        public string? Suggestion { get; set; }
        public bool Fallback { get; set; }
    }

    public class MaterialEstimateRequest
    {
        public string Materials { get; set; } = string.Empty;
    }

    public class QuoteSuggestionRequest
    {
        public string Context { get; set; } = string.Empty;
    }

    public class AiResponse
    {
        public string Suggestion { get; set; } = string.Empty;
    }

    // ─── Multi-turn conversation DTOs ─────────────────────────────────────────

    /// <summary>
    /// Rich multi-turn conversation request.  The client sends the FULL message history
    /// on every request; the server has no session state.
    /// </summary>
    public class ConversationRequest
    {
        /// <summary>Full message history — [system?, user, assistant, user, …].</summary>
        public List<ConversationMessage> Messages { get; set; } = new();

        /// <summary>Optional project context (fills tool definitions automatically).</summary>
        public Guid? ProjectId { get; set; }

        /// <summary>Preferred provider: "gemini" | "groq" | "nvidia" | null (auto).</summary>
        public string? Provider { get; set; }
    }

    public class ConversationMessage
    {
        public string Role { get; set; } = "user";    // "user" | "assistant" | "system"
        public string Content { get; set; } = string.Empty;
    }

    public class ConversationResponse
    {
        public string Answer { get; set; } = string.Empty;
        public bool UsedFallback { get; set; }
        public string Source { get; set; } = string.Empty;
        public List<string> ToolsUsed { get; set; } = new();
    }

    // ─── Controller ───────────────────────────────────────────────────────────


    [ApiController]
    [IgnoreAntiforgeryToken]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [EnableRateLimiting("ai")]
    public class AiApiController : ControllerBase
    {
        private static readonly string[] RolePriority =
            { "Admin", "SystemAdmin", "Manager", "Management", "Executive", "Office", "SiteManager", "FieldWorker", "Client" };

        private readonly IAIService _aiService;
        private readonly AIToolExecutor _toolExecutor;
        private readonly IUserAIMemoryService _memory;

        public AiApiController(IAIService aiService, AIToolExecutor toolExecutor, IUserAIMemoryService memory)
        {
            _aiService = aiService;
            _toolExecutor = toolExecutor;
            _memory = memory;
        }

        /// <summary>The caller's role as proven by the JWT - never taken from the request body.</summary>
        private string ResolveRole() =>
            User.Identity?.IsAuthenticated == true
                ? RolePriority.FirstOrDefault(User.IsInRole) ?? "User"
                : "Anonymous";

        /// <summary>
        /// Shared pipeline for every chat endpoint: verified identity -> tools bound to that identity -> provider
        /// cascade with memory -> response. The model can only reach data through <see cref="AIToolExecutor"/>.
        /// </summary>
        private async Task<AIChatResponse> RunChatAsync(IEnumerable<AIChatMessage> messages, Guid? projectId, string? provider, CancellationToken ct)
        {
            var authenticated = User.Identity?.IsAuthenticated == true;
            var role = ResolveRole();
            var userName = authenticated ? User.Identity?.Name : null;

            var aiRequest = new AIChatRequest
            {
                Messages = messages.ToList(),
                ProjectId = projectId,
                UserRole = role,
                UserName = userName,
                Provider = provider,
                IsAuthenticated = authenticated
            };

            _toolExecutor.SetUserContext(userName, role, authenticated);
            return await _aiService.AskWithHistoryAsync(
                aiRequest,
                AIToolExecutor.GetAvailableTools(),
                (toolCall, token) => _toolExecutor.ExecuteAsync(toolCall, token),
                ct);
        }

        // ─── Multi-turn conversation with tool calling ────────────────────────

        /// <summary>
        /// POST api/v1/chat/conversation
        /// Accepts the full message history; answers like an LLM with live, role-checked data and per-user memory.
        /// </summary>
        [HttpPost("api/v1/chat/conversation")]
        [AllowAnonymous]
        public async Task<IActionResult> Conversation([FromBody] ConversationRequest request, CancellationToken ct)
        {
            if (request?.Messages == null || !request.Messages.Any(m => m.Role == "user" && !string.IsNullOrWhiteSpace(m.Content)))
                return BadRequest(new { error = "At least one user message is required." });

            var result = await RunChatAsync(
                request.Messages.Select(m => new AIChatMessage { Role = m.Role, Content = m.Content }),
                request.ProjectId, request.Provider, ct);

            return Ok(new ConversationResponse
            {
                Answer = result.Text,
                UsedFallback = result.UsedFallback,
                Source = result.Source,
                ToolsUsed = result.ToolsUsed
            });
        }

        // ─── Legacy single-turn endpoint (kept for older clients) ─────────────

        /// <summary>POST api/v1/chat/ask - single question, forwarded to the same pipeline.</summary>
        [HttpPost("api/v1/chat/ask")]
        [AllowAnonymous]
        public async Task<IActionResult> AskChatbot([FromBody] ChatAskRequest request, CancellationToken ct)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Question))
                return BadRequest(new ChatAskResponse { Answer = "Question is required." });

            var result = await RunChatAsync(
                new[] { new AIChatMessage { Role = "user", Content = request.Question } },
                null, request.Provider, ct);

            return Ok(new ChatAskResponse { Answer = result.Text, Suggestion = result.Text, Fallback = result.UsedFallback });
        }

        // ─── Privacy controls for the assistant's memory ─────────────────────

        /// <summary>GET api/v1/chat/memory - shows the caller exactly what the assistant remembers about them.</summary>
        [HttpGet("api/v1/chat/memory")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetMemory()
        {
            var formatted = await _memory.FormatMemoryForPromptAsync(User.Identity?.Name);
            return Ok(new { memory = formatted });
        }

        /// <summary>DELETE api/v1/chat/memory - erases everything stored about the caller.</summary>
        [HttpDelete("api/v1/chat/memory")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> ClearMemory()
        {
            await _memory.ClearAsync(User.Identity?.Name);
            return NoContent();
        }

        // ─── Estimate & Quote endpoints ───────────────────────────────────────

        [HttpPost("api/v1/ai/estimate-materials")]
        public async Task<IActionResult> EstimateMaterials([FromBody] MaterialEstimateRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Materials))
                return Ok(new AiResponse { Suggestion = "Estimated Cost: R 0.00" });

            var quoteReq = new QuoteRequest
            {
                ProjectDescription = request.Materials,
                ProjectType = "Material Estimation"
            };

            var result = await _aiService.EstimateAsync(quoteReq);
            var text = $"Estimated Cost: R {result.Amount:N2}. Source: {result.Source}";
            return Ok(new AiResponse { Suggestion = text });
        }

        [HttpPost("api/v1/ai/suggest-quote")]
        public async Task<IActionResult> SuggestQuote([FromBody] QuoteSuggestionRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Context))
                return Ok(new AiResponse { Suggestion = "Standard quote estimate applies." });

            var quoteReq = new QuoteRequest
            {
                ProjectDescription = request.Context,
                ProjectType = "General Construction"
            };

            var result = await _aiService.EstimateAsync(quoteReq);
            var text = $"Suggested Quote: R {result.Amount:N2} based on scope: {request.Context} ({result.Source})";
            return Ok(new AiResponse { Suggestion = text });
        }
    }
}
