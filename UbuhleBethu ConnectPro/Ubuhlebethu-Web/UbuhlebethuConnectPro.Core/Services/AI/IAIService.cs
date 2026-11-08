using Microsoft.Extensions.Logging;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.Core.Options;

namespace UbuhlebethuConnectPro.Core.Services.AI
{
    public record AIEstimateResult(decimal Amount, bool UsedFallback, string Source);
    public record AIChatResult(string Text, bool UsedFallback);

    public interface IAIService
    {
        Task<AIEstimateResult> EstimateAsync(QuoteRequest quote, CancellationToken ct = default);

        /// <summary>Simple single-turn chat (backward-compatible).</summary>
        Task<AIChatResult> AskAsync(string question, string context, string fallbackAnswer, CancellationToken ct = default);

        /// <summary>
        /// Multi-turn conversational chat with full history and optional tool calling.
        /// Implementations should forward the entire message history to the LLM.
        /// </summary>
        Task<AIChatResponse> AskWithHistoryAsync(
            AIChatRequest request,
            List<AITool>? tools = null,
            Func<AIToolCall, CancellationToken, Task<AIToolResult>>? toolExecutor = null,
            CancellationToken ct = default);
    }

    /// <summary>
    /// Google Gemini integration. The API key lives only on the server. Any timeout, error or
    /// low-confidence answer returns a fallback so core business flows never depend on the AI (NFR-06).
    /// </summary>
    public class GeminiAIService : IAIService
    {
        private readonly HttpClient _http;
        private readonly GeminiOptions _opts;
        private readonly AIAssistantOptions _assistant;
        private readonly AIRequestFactory _factory = new();
        private readonly ILogger<GeminiAIService> _log;

        public GeminiAIService(HttpClient http, IOptions<GeminiOptions> opts, ILogger<GeminiAIService> log,
            IOptions<AIAssistantOptions>? assistant = null)
        {
            _http = http; _opts = opts.Value; _log = log; _assistant = assistant?.Value ?? new AIAssistantOptions();
        }

        public async Task<AIEstimateResult> EstimateAsync(QuoteRequest quote, CancellationToken ct = default)
        {
            var fallback = new AIEstimateResult(RuleBasedEstimator.Estimate(quote), true, "Rule-based fallback");
            if (string.IsNullOrWhiteSpace(_opts.ApiKey)) return fallback;

            try
            {
                var req = _factory.Create(AIRequestType.QuotationEstimate, quote);
                var text = await CallGeminiAsync(req, ct);
                if (text == null) return fallback;

                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                var amount = root.GetProperty("estimate").GetDecimal();
                var confidence = root.TryGetProperty("confidence", out var c) ? c.GetDouble() : 0.5;
                if (amount <= 0 || confidence < 0.5) return fallback; // low-confidence -> fallback
                return new AIEstimateResult(decimal.Round(amount, 2), false, "Gemini");
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                _log.LogWarning(ex, "AI estimate failed; using rule-based fallback.");
                return fallback;
            }
        }

        public async Task<AIChatResult> AskAsync(string question, string context, string fallbackAnswer, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_opts.ApiKey)) return new AIChatResult(fallbackAnswer, true);
            try
            {
                var req = _factory.Create(AIRequestType.ChatbotQuery, (question, context));
                var text = await CallGeminiAsync(req, ct, expectJson: false);
                return string.IsNullOrWhiteSpace(text) ? new AIChatResult(fallbackAnswer, true) : new AIChatResult(text.Trim(), false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                _log.LogWarning(ex, "AI chat failed; using fallback answer.");
                return new AIChatResult(fallbackAnswer, true);
            }
        }

        /// <summary>
        /// Agentic chat turn: model -> (tool calls -> model)* -> answer. If the primary model is overloaded the
        /// configured fallback models are tried before the composite service moves on to another provider.
        /// </summary>
        public async Task<AIChatResponse> AskWithHistoryAsync(
            AIChatRequest request,
            List<AITool>? tools = null,
            Func<AIToolCall, CancellationToken, Task<AIToolResult>>? toolExecutor = null,
            CancellationToken ct = default)
        {
            var fallback = new AIChatResponse { Text = "Gemini unavailable.", UsedFallback = true, Source = "Gemini (unavailable)" };
            if (string.IsNullOrWhiteSpace(_opts.ApiKey)) return fallback;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, _opts.ChatTimeoutSeconds)));

            var models = new[] { _opts.Model }
                .Concat(_opts.FallbackModels ?? Array.Empty<string>())
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var model in models)
            {
                try
                {
                    var result = await RunAgentAsync(model, request, tools, toolExecutor, cts.Token);
                    if (result != null) return result;
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                {
                    _log.LogWarning(ex, "Gemini model {Model} failed.", model);
                }
                if (cts.IsCancellationRequested) break;
            }
            return fallback;
        }

        private async Task<AIChatResponse?> RunAgentAsync(
            string model, AIChatRequest request, List<AITool>? tools,
            Func<AIToolCall, CancellationToken, Task<AIToolResult>>? toolExecutor, CancellationToken ct)
        {
            var systemInstruction = new { parts = new[] { new { text = AIContextBuilder.Build(request) } } };
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";

            var contents = new List<object>();
            foreach (var m in request.Messages.Where(m => m.Role is "user" or "assistant"))
                contents.Add(new { role = m.Role == "assistant" ? "model" : "user", parts = new[] { new { text = m.Content } } });

            var declarations = tools?.Select(t => new { name = t.Name, description = t.Description, parameters = t.Parameters }).ToArray();
            // Gemini 3 models are tuned for their default temperature; lowering it can cause looping.
            object? genConfig = model.StartsWith("gemini-3", StringComparison.OrdinalIgnoreCase)
                ? new { maxOutputTokens = 2048 }
                : new { temperature = 0.3, maxOutputTokens = 2048 };

            var toolsUsed = new List<string>();

            for (var round = 0; round <= _assistant.MaxToolRounds; round++)
            {
                var allowTools = declarations is { Length: > 0 } && toolExecutor != null && round < _assistant.MaxToolRounds;
                object body = allowTools
                    ? new { systemInstruction, contents, tools = new[] { new { functionDeclarations = declarations } }, generationConfig = genConfig }
                    : new { systemInstruction, contents, generationConfig = genConfig };

                using var doc = await AIChatTransport.PostJsonAsync(
                    _http, url, body, h => h.Add("x-goog-api-key", _opts.ApiKey!), _log, $"Gemini/{model}", ct);
                if (doc == null) return null;

                if (!doc.RootElement.TryGetProperty("candidates", out var cands) || cands.GetArrayLength() == 0) return null;
                if (!cands[0].TryGetProperty("content", out var content) ||
                    !content.TryGetProperty("parts", out var parts) || parts.GetArrayLength() == 0) return null;

                var calls = parts.EnumerateArray().Where(p => p.TryGetProperty("functionCall", out _)).ToList();
                if (allowTools && calls.Count > 0)
                {
                    // Echo the model turn verbatim (keeps thoughtSignature etc. that Gemini 3 requires on the next call).
                    contents.Add(JsonSerializer.Deserialize<JsonElement>(content.GetRawText()));

                    var responses = new List<object>();
                    foreach (var part in calls)
                    {
                        var fc = part.GetProperty("functionCall");
                        var name = fc.GetProperty("name").GetString() ?? string.Empty;
                        var args = fc.TryGetProperty("args", out var a) ? AIChatTransport.ToArgs(a) : new Dictionary<string, string>();
                        toolsUsed.Add(name);
                        var result = await toolExecutor!(new AIToolCall { Name = name, Arguments = args }, ct);
                        responses.Add(new { functionResponse = new { name, response = new { result = AIChatTransport.CapToolResult(result.Result) } } });
                    }
                    contents.Add(new { role = "user", parts = responses.ToArray() });
                    continue;
                }

                var text = AIChatTransport.CleanModelText(string.Concat(parts.EnumerateArray()
                    .Where(p => !(p.TryGetProperty("thought", out var th) && th.ValueKind == JsonValueKind.True))
                    .Select(p => p.TryGetProperty("text", out var t) ? t.GetString() : null)));
                if (string.IsNullOrWhiteSpace(text)) return null;

                return new AIChatResponse { Text = text, UsedFallback = false, Source = $"Gemini ({model})", ToolsUsed = toolsUsed.Distinct().ToList() };
            }
            return null;
        }

        private async Task<string?> CallGeminiAsync(AIRequest req, CancellationToken ct, bool expectJson = true)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, expectJson ? _opts.TimeoutSeconds : _opts.ChatTimeoutSeconds)));

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_opts.Model}:generateContent";
            var body = new
            {
                systemInstruction = new { parts = new[] { new { text = req.Context } } },
                contents = new[] { new { role = "user", parts = new[] { new { text = req.Prompt } } } },
                generationConfig = expectJson
                    ? (object)new { responseMimeType = "application/json", temperature = 0.2 }
                    : new { temperature = 0.3 }
            };

            using var doc = await AIChatTransport.PostJsonAsync(
                _http, url, body, h => h.Add("x-goog-api-key", _opts.ApiKey!), _log, "Gemini", cts.Token, attempts: 1); // key in header, never in URL/logs
            if (doc == null) return null;

            if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                return null;

            if (!candidates[0].TryGetProperty("content", out var content) ||
                !content.TryGetProperty("parts", out var parts) ||
                parts.GetArrayLength() == 0 ||
                !parts[0].TryGetProperty("text", out var text))
            {
                return null;
            }

            return text.GetString();
        }
    }
}
