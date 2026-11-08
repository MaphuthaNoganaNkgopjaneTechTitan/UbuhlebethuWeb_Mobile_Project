using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.Core.Options;

namespace UbuhlebethuConnectPro.Core.Services.AI
{
    /// <summary>Groq (OpenAI-compatible) provider. Fast open-weight models with native tool calling.</summary>
    public class GroqAIService : IAIService
    {
        private const string Url = "https://api.groq.com/openai/v1/chat/completions";
        private readonly HttpClient _http;
        private readonly GroqOptions _opts;
        private readonly AIAssistantOptions _assistant;
        private readonly AIRequestFactory _factory = new();
        private readonly ILogger<GroqAIService> _log;

        public GroqAIService(HttpClient http, IOptions<GroqOptions> opts, ILogger<GroqAIService> log,
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
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(3));
                var text = await OpenAiCompatibleChat.SimpleAsync(_http, Url, _opts.ApiKey!, _opts.Model, "Groq",
                    req.Context + " Please return a JSON object with 'estimate' (number) and 'confidence' (0.0 to 1.0). Do not include markdown blocks.",
                    req.Prompt, json: true, temperature: 0.2, _log, cts.Token);
                if (string.IsNullOrWhiteSpace(text)) return fallback;

                using var inner = JsonDocument.Parse(text);
                var amount = inner.RootElement.GetProperty("estimate").GetDecimal();
                var confidence = inner.RootElement.TryGetProperty("confidence", out var c) ? c.GetDouble() : 0.5;
                if (amount <= 0 || confidence < 0.5) return fallback;
                return new AIEstimateResult(decimal.Round(amount, 2), false, "Groq");
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                _log.LogWarning(ex, "Groq AI estimate failed.");
                return fallback;
            }
        }

        public async Task<AIChatResult> AskAsync(string question, string context, string fallbackAnswer, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_opts.ApiKey)) return new AIChatResult(fallbackAnswer, true);
            try
            {
                var req = _factory.Create(AIRequestType.ChatbotQuery, (question, context));
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(3, _opts.ChatTimeoutSeconds)));
                var text = await OpenAiCompatibleChat.SimpleAsync(_http, Url, _opts.ApiKey!, _opts.Model, "Groq",
                    req.Context, req.Prompt, json: false, temperature: 0.3, _log, cts.Token);
                return string.IsNullOrWhiteSpace(text) ? new AIChatResult(fallbackAnswer, true) : new AIChatResult(text, false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                _log.LogWarning(ex, "Groq AI chat failed.");
                return new AIChatResult(fallbackAnswer, true);
            }
        }

        public async Task<AIChatResponse> AskWithHistoryAsync(
            AIChatRequest request, List<AITool>? tools = null,
            Func<AIToolCall, CancellationToken, Task<AIToolResult>>? toolExecutor = null,
            CancellationToken ct = default)
        {
            var fallback = new AIChatResponse { Text = "Groq unavailable.", UsedFallback = true, Source = "Groq (unavailable)" };
            if (string.IsNullOrWhiteSpace(_opts.ApiKey)) return fallback;

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, _opts.ChatTimeoutSeconds)));
                var result = await OpenAiCompatibleChat.RunAsync(_http, Url, _opts.ApiKey!, _opts.Model, "Groq",
                    request, AIContextBuilder.Build(request), tools, toolExecutor, _assistant, _log, cts.Token);
                return result ?? fallback;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                _log.LogWarning(ex, "Groq AskWithHistoryAsync failed.");
                return fallback;
            }
        }
    }
}
