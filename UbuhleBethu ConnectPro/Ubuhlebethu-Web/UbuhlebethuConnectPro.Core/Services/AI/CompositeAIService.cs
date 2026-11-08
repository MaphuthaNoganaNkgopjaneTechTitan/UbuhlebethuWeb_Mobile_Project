using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.Core.Options;

namespace UbuhlebethuConnectPro.Core.Services.AI
{
    /// <summary>
    /// Orchestrates the assistant: sanitises history, injects the user's persistent memory, tries the LLM providers
    /// in priority order (preferred provider first) inside one overall time budget, then - only if every provider is
    /// down - answers from the local domain engine (knowledge base + live-data tools) so the user is never stranded.
    /// </summary>
    public class CompositeAIService : IAIService
    {
        private static readonly string[] DefaultOrder = { "gemini", "groq", "nvidia" };

        private readonly GeminiAIService _gemini;
        private readonly GroqAIService _groq;
        private readonly NvidiaAIService _nvidia;
        private readonly IUserAIMemoryService _memory;
        private readonly AIAssistantOptions _opts;
        private readonly ILogger<CompositeAIService> _log;

        public CompositeAIService(
            GeminiAIService gemini,
            GroqAIService groq,
            NvidiaAIService nvidia,
            IUserAIMemoryService memory,
            ILogger<CompositeAIService> log,
            IOptions<AIAssistantOptions>? opts = null)
        {
            _gemini = gemini;
            _groq = groq;
            _nvidia = nvidia;
            _memory = memory;
            _log = log;
            _opts = opts?.Value ?? new AIAssistantOptions();
        }

        public async Task<AIEstimateResult> EstimateAsync(QuoteRequest quote, CancellationToken ct = default)
        {
            var res = await _gemini.EstimateAsync(quote, ct);
            if (!res.UsedFallback) return res;

            _log.LogInformation("Gemini estimate fell back. Trying Groq...");
            var groqRes = await _groq.EstimateAsync(quote, ct);
            if (!groqRes.UsedFallback) return groqRes;

            _log.LogInformation("Groq estimate fell back. Trying Nvidia...");
            var nvidiaRes = await _nvidia.EstimateAsync(quote, ct);
            if (!nvidiaRes.UsedFallback) return nvidiaRes;

            // Deterministic QS engine
            var sqmM2 = quote.EstimatedBudget > 0 ? quote.EstimatedBudget / 11500m : 120m;
            var estimate = ConstructionCostEstimator.GenerateEstimate(quote.ProjectType ?? "Residential", sqmM2);
            return new AIEstimateResult(estimate.SubtotalExVat, false, "UbuhleBethu QS Engine");
        }

        public async Task<AIChatResult> AskAsync(string question, string context, string fallbackAnswer, CancellationToken ct = default)
        {
            var res = await _gemini.AskAsync(question, context, fallbackAnswer, ct);
            if (!res.UsedFallback) return res;

            var groqRes = await _groq.AskAsync(question, context, fallbackAnswer, ct);
            if (!groqRes.UsedFallback) return groqRes;

            var nvidiaRes = await _nvidia.AskAsync(question, context, fallbackAnswer, ct);
            if (!nvidiaRes.UsedFallback) return nvidiaRes;

            return res;
        }

        public async Task<AIChatResponse> AskWithHistoryAsync(
            AIChatRequest request,
            List<AITool>? tools = null,
            Func<AIToolCall, CancellationToken, Task<AIToolResult>>? toolExecutor = null,
            CancellationToken ct = default)
        {
            // 1. Hygiene: bounded, role-filtered history (clients can never inject system prompts)
            request.Messages = AIChatTransport.Sanitize(request.Messages, _opts);
            if (request.Messages.Count == 0)
                return new AIChatResponse { Text = "Hi! Ask me anything about quotes, projects, site work, invoices or how to do something in ConnectPro.", Source = "ConnectPro Assistant" };

            var lastQuestion = request.Messages.Last(m => m.Role == "user").Content;
            var userKey = request.IsAuthenticated ? request.UserName : null;

            // 2. Learn from the message and load this user's memory (authenticated users only - never a shared bucket)
            if (userKey != null)
            {
                await _memory.AutoLearnFromMessageAsync(userKey, lastQuestion);
                request.MemoryContext = await _memory.FormatMemoryForPromptAsync(userKey);
            }

            // 3. Remote LLMs, preferred provider first, one overall time budget
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
            budget.CancelAfter(TimeSpan.FromSeconds(Math.Max(10, _opts.TotalBudgetSeconds)));

            foreach (var provider in ProviderOrder(request.Provider))
            {
                if (budget.IsCancellationRequested) break;
                var res = await GetService(provider).AskWithHistoryAsync(request, tools, toolExecutor, budget.Token);
                if (!res.UsedFallback && !string.IsNullOrWhiteSpace(res.Text)) return res;
                _log.LogWarning("AI provider {Provider} unavailable for this turn; trying next.", provider);
            }

            // 4. Local domain engine (knowledge base + live-data tools)
            _log.LogWarning("All remote AI providers failed; answering from the local domain engine.");
            var text = await GenerateLocalResponseAsync(request, toolExecutor, ct);
            return new AIChatResponse
            {
                Text = text,
                UsedFallback = true,
                Source = "ConnectPro built-in knowledge (AI providers unreachable)",
                ToolsUsed = new List<string> { "SystemKnowledgeBase" }
            };
        }

        private static IEnumerable<string> ProviderOrder(string? preferred)
        {
            var p = preferred?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(p) || p == "auto" || !DefaultOrder.Contains(p)) return DefaultOrder;
            return new[] { p }.Concat(DefaultOrder.Where(x => x != p));
        }

        private IAIService GetService(string provider) => provider switch
        {
            "groq" => _groq,
            "nvidia" => _nvidia,
            _ => _gemini
        };

        // ─────────────────────────── Local domain engine ───────────────────────────

        private static readonly Regex AreaRx = new(@"(\d[\d\s,.]*)\s*(?:m2|m²|sqm|sq\.?\s*m|square\s+met(?:er|re)s?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex GuidRx = new(@"[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static bool Has(string text, params string[] words) => words.Any(w => text.Contains(w, StringComparison.Ordinal));

        private async Task<string> GenerateLocalResponseAsync(
            AIChatRequest request, Func<AIToolCall, CancellationToken, Task<AIToolResult>>? exec, CancellationToken ct)
        {
            var q = request.Messages.Last(m => m.Role == "user").Content.Trim();
            var lower = q.ToLowerInvariant();
            var role = SystemKnowledgeBase.CanonicalRole(request.UserRole);
            var name = request.IsAuthenticated ? request.UserName : null;
            var hello = name == null ? "Hi" : $"Hi {name}";

            // Resolve short follow-ups ("and how do I do that?") against the previous user turn
            var previousUser = request.Messages.Where(m => m.Role == "user").Reverse().Skip(1).FirstOrDefault()?.Content ?? string.Empty;
            var contextual = lower.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 4 && previousUser.Length > 0
                ? $"{previousUser.ToLowerInvariant()} {lower}" : lower;

            async Task<string?> Tool(string toolName, Dictionary<string, string>? args = null)
            {
                if (exec == null) return null;
                var r = await exec(new AIToolCall { Name = toolName, Arguments = args ?? new() }, ct);
                return r.Result;
            }

            // Greetings / thanks / identity
            if (Regex.IsMatch(lower, @"^(hi|hello|hey|howzit|good (morning|afternoon|evening)|sawubona|yo)\b[\s!.,]*$") ||
                Has(lower, "who are you", "what can you do", "what do you do", "help me"))
            {
                return $"{hello}! I'm the ConnectPro Assistant. Right now my cloud AI is busy, so I'm answering from the system's built-in knowledge - I can still help.\n\n" +
                       $"As **{role}** I can walk you through tasks step by step, tell you whether your role is allowed to do something and who is if not, give indicative cost estimates, and look up your live projects, quotes and invoices.\n\n" +
                       "Try: *\"How do I request a quote?\"*, *\"Estimate a 150 m² house in Durban\"*, *\"What can I do with my role?\"* or *\"What's the status of my projects?\"*";
            }
            if (Regex.IsMatch(lower, @"^(thanks|thank you|thx|ta|cheers)\b")) return "You're welcome! Ask anytime if you need anything else in the system.";

            // Memory
            if (Has(lower, "what do you remember", "what did i tell you", "my memory", "what do you know about me"))
            {
                var mem = await Tool("get_my_memory") ?? "I don't have memory available right now.";
                return $"{mem}\n\nSay *\"Remember that ...\"* to teach me something or *\"Forget ...\"* to remove it.";
            }

            // Capabilities / who can do what
            if (Has(contextual, "what can i do", "what am i allowed", "my permissions", "my role can", "what can my role") ||
                Regex.IsMatch(contextual, @"who (can|is able|has)\b"))
            {
                var guide = Regex.IsMatch(contextual, @"who (can|is able|has)\b") ? SystemKnowledgeBase.FindBestGuide(contextual) : null;
                if (guide != null)
                    return $"### {guide.Title}\n**Who can do this:** {string.Join(", ", guide.AllowedRoles)}\n**Where:** `{guide.NavigationPath}`\n\n" +
                           $"{(SystemKnowledgeBase.CanPerform(request.UserRole, guide) ? "✅ Your role **can** do this." : "🔒 Your role cannot do this - ask one of the roles above.")} Ask *\"how do I ...\"* for the exact steps.";
                return SystemKnowledgeBase.BuildCapabilitySummary(request.UserRole);
            }

            // Cost / quote estimates
            var areaMatch = AreaRx.Match(contextual);
            if (Has(contextual, "estimate", "cost", "how much", "price", "quote me", "budget for") &&
                (areaMatch.Success || Has(contextual, "house", "home", "clinic", "school", "warehouse", "paving", "road", "building", "flat", "office block")) &&
                !Has(lower, "how do i", "where do i"))
            {
                var area = 120m;
                if (areaMatch.Success && decimal.TryParse(areaMatch.Groups[1].Value.Replace(" ", "").Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out var a) && a > 0) area = a;

                var bType = Has(contextual, "clinic", "health") ? "Clinic"
                    : Has(contextual, "school", "classroom") ? "School"
                    : Has(contextual, "industrial", "warehouse", "factory") ? "Industrial Warehouse"
                    : Has(contextual, "civil", "paving", "road") ? "Civil Paving"
                    : Has(contextual, "office", "commercial", "shop", "retail") ? "Commercial"
                    : Has(contextual, "luxury", "high-end") ? "Luxury Residential"
                    : Has(contextual, "rdp", "social housing") ? "RDP/Social housing"
                    : "Residential";
                var location = Has(contextual, "durban", "kzn", "kwazulu") ? "Durban, KZN"
                    : Has(contextual, "cape town", "western cape") ? "Western Cape"
                    : Has(contextual, "rural") ? "Rural"
                    : "Gauteng";
                var grade = Has(contextual, "premium", "luxury", "high-end") ? "Premium" : Has(contextual, "medium") ? "Medium" : "Standard";

                string? body = await Tool("estimate_construction_cost", new()
                {
                    ["building_type"] = bType, ["area_square_meters"] = area.ToString(CultureInfo.InvariantCulture),
                    ["location"] = location, ["quality_tier"] = grade
                });
                body ??= ConstructionCostEstimator.GenerateEstimate(bType, area, location, grade).FormattedReport;

                var next = role == "Client" || role == "Anonymous"
                    ? "**Next step:** submit a quote request (Client Portal > Quotations > Request a Quote) so our QS can prepare a binding quote."
                    : "**Next step:** use Pending Quotes / AI Draft Estimate to turn this into a formal proposal, apply markup and send it for manager approval.";
                return $"{hello}, here's an indicative estimate (assumed: {bType}, {area:N0} m², {location}, {grade} finish):\n\n{body}\n\n{next}";
            }

            // Live data
            var guidMatch = GuidRx.Match(q);
            var projectId = guidMatch.Success ? guidMatch.Value : request.ProjectId?.ToString()
                ?? (name != null ? (await _memory.GetMemoryAsync(name)).PinnedProjectId?.ToString() : null);

            if (exec != null && request.IsAuthenticated)
            {
                if (Has(contextual, "my quotes", "my quotations", "quote status", "status of my quote", "pending quote"))
                    return $"### Your quotations\n\n{await Tool("get_my_quotes")}";
                if (Has(contextual, "my invoices", "invoice status", "outstanding invoice", "overdue"))
                    return $"### Invoices\n\n{await Tool("get_my_invoices")}";
                if (Has(contextual, "list projects", "my projects", "all projects", "active projects", "which projects"))
                    return $"### Projects\n\n{await Tool("list_projects")}";

                if (projectId != null && Has(contextual, "budget", "spent", "remaining", "expenses", "site report", "progress", "status", "project"))
                {
                    var args = new Dictionary<string, string> { ["project_id"] = projectId };
                    if (Has(contextual, "budget", "spent", "remaining")) return $"### Project budget\n\n{await Tool("get_project_budget", args)}";
                    if (Has(contextual, "expense")) return $"### Project expenses\n\n{await Tool("get_expenses", args)}";
                    if (Has(contextual, "site report", "photo")) return $"### Recent site reports\n\n{await Tool("get_site_reports", args)}";
                    return $"### Project overview\n\n{await Tool("get_project_status", args)}";
                }
                if (projectId == null && Has(contextual, "budget", "expenses", "site report", "project status", "progress of"))
                    return $"Which project do you mean? Here are the ones you can access:\n\n{await Tool("list_projects")}\n\nTell me the project name or paste its ID.";
            }
            else if (Has(contextual, "my quotes", "my invoices", "my projects", "budget", "project status"))
            {
                return "I can look that up once you're signed in. Please log in and ask again.";
            }

            // Step-by-step guides
            var assessment = SystemKnowledgeBase.AssessCapability(request.UserRole, contextual);
            if (assessment.ActionTitle == "General System Operation")
            {
                var topics = string.Join("\n", SystemKnowledgeBase.AllGuides
                    .Where(g => SystemKnowledgeBase.CanPerform(request.UserRole, g)).Take(10).Select(g => $"- {g.Title}"));
                return $"{hello}, I'm running on built-in knowledge right now and I'm not sure which task you mean. Could you rephrase or pick one of these?\n\n{topics}\n\n" +
                       "You can also ask for a cost estimate (e.g. *\"estimate a 200 m² house in Cape Town\"*) or *\"what can I do?\"*";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"### {assessment.ActionTitle}");
            sb.AppendLine(assessment.PermissionExplanation);
            sb.AppendLine();
            sb.AppendLine($"**Where:** `{assessment.NavigationPath}`");
            sb.AppendLine($"**Who can do this:** {string.Join(", ", assessment.AllowedRoles)}");
            sb.AppendLine();
            sb.AppendLine("**Steps**");
            foreach (var step in assessment.ExactSteps) sb.AppendLine(step);
            if (!string.IsNullOrWhiteSpace(assessment.ContactRecommendation))
            {
                sb.AppendLine();
                sb.AppendLine($"**What to do instead:** {assessment.ContactRecommendation}");
            }
            if (assessment.ProTips.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("**Tips**");
                foreach (var tip in assessment.ProTips) sb.AppendLine($"- {tip}");
            }
            return sb.ToString();
        }
    }
}
