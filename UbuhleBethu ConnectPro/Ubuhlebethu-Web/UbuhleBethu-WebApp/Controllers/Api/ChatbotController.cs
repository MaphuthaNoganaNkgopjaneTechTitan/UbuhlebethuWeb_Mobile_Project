using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UbuhlebethuConnectPro.Core.Services.AI;
using UbuhlebethuConnectPro.WebApp.ApiClient;
using UbuhlebethuConnectPro.WebApp.Models.DTOs;

namespace UbuhlebethuConnectPro.WebApp.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatbotController : ControllerBase
    {
        private readonly IConnectProApiClient _apiClient;
        private readonly IAIService _fallbackAiService;

        public ChatbotController(IConnectProApiClient apiClient, IAIService fallbackAiService)
        {
            _apiClient = apiClient;
            _fallbackAiService = fallbackAiService;
        }

        [HttpPost("ask")]
        [AllowAnonymous]
        public async Task<IActionResult> Ask([FromBody] ChatRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request?.Question))
                return BadRequest(new { Answer = "Question is required." });

            var isAuthenticated = User.Identity?.IsAuthenticated == true;
            var userRole = User.FindFirstValue(ClaimTypes.Role) ?? (isAuthenticated ? "User" : "Anonymous");
            var userName = isAuthenticated ? User.Identity?.Name : null;

            // Assemble full conversation history
            var messages = new List<ConversationMessageDto>();
            if (request.History != null && request.History.Count > 0)
            {
                foreach (var m in request.History.Where(h => !string.IsNullOrWhiteSpace(h?.Content)))
                {
                    messages.Add(new ConversationMessageDto { Role = m.Role, Content = m.Content });
                }
            }
            messages.Add(new ConversationMessageDto { Role = "user", Content = request.Question.Trim() });

            // 1. Try routing through the backend REST API (which has live DB repos, JWT context & memory)
            try
            {
                var apiReq = new ConversationRequestDto
                {
                    Messages = messages,
                    ProjectId = request.ProjectId,
                    Provider = request.Provider
                };

                var apiResult = await _apiClient.ConversationAsync(apiReq);
                if (apiResult != null && !string.IsNullOrWhiteSpace(apiResult.Answer))
                {
                    return Ok(new
                    {
                        Answer = apiResult.Answer,
                        Fallback = apiResult.UsedFallback,
                        Source = apiResult.Source,
                        ToolsUsed = apiResult.ToolsUsed
                    });
                }
            }
            catch
            {
                // Non-fatal, cascade to local in-process AI pipeline
            }

            // 2. Cascade to local AI pipeline (Groq -> Gemini -> Nvidia -> Domain Intelligence Engine)
            var aiRequest = new AIChatRequest
            {
                Messages = messages.Select(m => new AIChatMessage
                {
                    Role = m.Role,
                    Content = m.Content
                }).ToList(),
                ProjectId = request.ProjectId,
                UserRole = userRole,
                UserName = userName,
                Provider = request.Provider,
                IsAuthenticated = isAuthenticated
            };

            var localResult = await _fallbackAiService.AskWithHistoryAsync(aiRequest, ct: ct);
            return Ok(new
            {
                Answer = localResult.Text,
                Fallback = localResult.UsedFallback,
                Source = localResult.Source,
                ToolsUsed = localResult.ToolsUsed
            });
        }
    }
}
