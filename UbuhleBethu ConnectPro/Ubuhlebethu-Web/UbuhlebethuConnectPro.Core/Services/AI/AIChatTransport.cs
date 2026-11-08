using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UbuhlebethuConnectPro.Core.Options;

namespace UbuhlebethuConnectPro.Core.Services.AI
{
    /// <summary>Shared plumbing for every LLM provider: retries, history hygiene and output clean-up.</summary>
    public static class AIChatTransport
    {
        private static readonly Regex ThinkBlock = new(@"<think>.*?</think>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Removes chain-of-thought blocks some reasoning models (Qwen, DeepSeek) leak into content.</summary>
        public static string CleanModelText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var cleaned = ThinkBlock.Replace(text, string.Empty);
            // An unterminated <think> means the answer was cut off mid-reasoning: drop the dangling part.
            var open = cleaned.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
            if (open >= 0) cleaned = cleaned[..open];
            return cleaned.Trim();
        }

        /// <summary>
        /// Keeps the newest N messages, caps each message, drops empty/system roles (a client must never be able to
        /// inject a system prompt) and guarantees the conversation starts with a user turn.
        /// </summary>
        public static List<AIChatMessage> Sanitize(IEnumerable<AIChatMessage> messages, AIAssistantOptions opts)
        {
            var list = messages
                .Where(m => m != null && !string.IsNullOrWhiteSpace(m.Content))
                .Where(m => m.Role is "user" or "assistant")
                .Select(m => new AIChatMessage
                {
                    Role = m.Role,
                    Content = m.Content.Length > opts.MaxMessageChars ? m.Content[..opts.MaxMessageChars] : m.Content,
                    Timestamp = m.Timestamp
                })
                .ToList();

            if (list.Count > opts.MaxHistoryMessages)
                list = list.Skip(list.Count - opts.MaxHistoryMessages).ToList();

            while (list.Count > 0 && list[0].Role != "user") list.RemoveAt(0);
            return list;
        }

        public static bool IsTransient(HttpStatusCode code) =>
            code == HttpStatusCode.TooManyRequests || (int)code >= 500;

        /// <summary>
        /// POSTs JSON with one automatic retry on 429/5xx (providers routinely answer "high demand" for a second or two).
        /// Returns null (never throws) if the call ultimately fails, logging the provider's error body for diagnosis.
        /// </summary>
        public static async Task<JsonDocument?> PostJsonAsync(
            HttpClient http, string url, object body, Action<HttpRequestHeaders> addAuth,
            ILogger log, string provider, CancellationToken ct, int attempts = 2)
        {
            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                try
                {
                    using var msg = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
                    addAuth(msg.Headers);
                    using var resp = await http.SendAsync(msg, ct);
                    if (resp.IsSuccessStatusCode)
                    {
                        var stream = await resp.Content.ReadAsStreamAsync(ct);
                        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                    }

                    var err = await resp.Content.ReadAsStringAsync(ct);
                    log.LogWarning("{Provider} returned {Status} (attempt {Attempt}/{Max}): {Body}",
                        provider, (int)resp.StatusCode, attempt, attempts, err.Length > 400 ? err[..400] : err);

                    if (!IsTransient(resp.StatusCode) || attempt == attempts) return null;
                    await Task.Delay(TimeSpan.FromMilliseconds(700 * attempt), ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    log.LogWarning("{Provider} call timed out or was cancelled.", provider);
                    return null;
                }
                catch (HttpRequestException ex)
                {
                    log.LogWarning(ex, "{Provider} network failure (attempt {Attempt}).", provider, attempt);
                    if (attempt == attempts) return null;
                    await Task.Delay(TimeSpan.FromMilliseconds(700 * attempt), ct);
                }
            }
            return null;
        }

        /// <summary>Flattens a JSON object of tool arguments into the string dictionary tools consume.</summary>
        public static Dictionary<string, string> ToArgs(JsonElement obj)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (obj.ValueKind != JsonValueKind.Object) return dict;
            foreach (var p in obj.EnumerateObject())
                dict[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() ?? "" : p.Value.ToString();
            return dict;
        }

        /// <summary>Tool output is untrusted data: cap it so one huge ledger can't blow the context window.</summary>
        public static string CapToolResult(string? result, int max = 6000) =>
            string.IsNullOrEmpty(result) ? "(no data)" : result.Length <= max ? result : result[..max] + "\n…(truncated)";
    }

    /// <summary>
    /// Complete OpenAI-compatible chat + tool-calling loop (Groq and NVIDIA NIM both speak this protocol).
    /// Implements the protocol correctly: the assistant's tool_calls message is echoed back before the tool results,
    /// and the loop may chain several tool rounds before the model writes its final answer.
    /// </summary>
    public static class OpenAiCompatibleChat
    {
        public static async Task<AIChatResponse?> RunAsync(
            HttpClient http, string url, string apiKey, string model, string provider,
            AIChatRequest request, string systemPrompt, List<AITool>? tools,
            Func<AIToolCall, CancellationToken, Task<AIToolResult>>? toolExecutor,
            AIAssistantOptions assistantOpts, ILogger log, CancellationToken ct)
        {
            var messages = new List<object> { new { role = "system", content = systemPrompt } };
            foreach (var m in request.Messages.Where(m => m.Role is "user" or "assistant"))
                messages.Add(new { role = m.Role, content = m.Content });

            var toolDefs = tools?.Select(t => new
            {
                type = "function",
                function = new { name = t.Name, description = t.Description, parameters = t.Parameters }
            }).ToArray();

            var toolsUsed = new List<string>();

            for (var round = 0; round <= assistantOpts.MaxToolRounds; round++)
            {
                var allowTools = toolDefs is { Length: > 0 } && toolExecutor != null && round < assistantOpts.MaxToolRounds;
                object body = allowTools
                    ? new { model, messages, tools = toolDefs, tool_choice = "auto", temperature = 0.3, max_tokens = 1800 }
                    : new { model, messages, temperature = 0.3, max_tokens = 1800 };

                using var doc = await AIChatTransport.PostJsonAsync(
                    http, url, body, h => h.Authorization = new AuthenticationHeaderValue("Bearer", apiKey),
                    log, provider, ct);
                if (doc == null) return null;

                if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0) return null;
                var message = choices[0].GetProperty("message");

                if (allowTools && message.TryGetProperty("tool_calls", out var calls) &&
                    calls.ValueKind == JsonValueKind.Array && calls.GetArrayLength() > 0)
                {
                    // 1) echo the assistant turn that requested the tools (required by the protocol)
                    messages.Add(new
                    {
                        role = "assistant",
                        content = message.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null,
                        tool_calls = calls.Clone()
                    });

                    // 2) run every requested tool and append one tool message per call
                    foreach (var tc in calls.EnumerateArray())
                    {
                        var fn = tc.GetProperty("function");
                        var name = fn.GetProperty("name").GetString() ?? string.Empty;
                        var id = tc.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? name : name;
                        var args = new Dictionary<string, string>();
                        try
                        {
                            var raw = fn.TryGetProperty("arguments", out var a) ? a.GetString() : "{}";
                            using var argDoc = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
                            args = AIChatTransport.ToArgs(argDoc.RootElement);
                        }
                        catch (JsonException) { /* malformed args -> tool reports what is missing */ }

                        toolsUsed.Add(name);
                        var result = await toolExecutor!(new AIToolCall { Name = name, Arguments = args }, ct);
                        messages.Add(new { role = "tool", tool_call_id = id, content = AIChatTransport.CapToolResult(result.Result) });
                    }
                    continue;
                }

                var text = AIChatTransport.CleanModelText(
                    message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String
                        ? content.GetString() : null);
                if (string.IsNullOrWhiteSpace(text)) return null;

                return new AIChatResponse { Text = text, UsedFallback = false, Source = provider, ToolsUsed = toolsUsed.Distinct().ToList() };
            }
            return null;
        }

        /// <summary>Single-shot completion used by the estimate/legacy-ask paths.</summary>
        public static async Task<string?> SimpleAsync(
            HttpClient http, string url, string apiKey, string model, string provider,
            string system, string user, bool json, double temperature, ILogger log, CancellationToken ct)
        {
            object body = json
                ? new { model, messages = new object[] { new { role = "system", content = system }, new { role = "user", content = user } }, response_format = new { type = "json_object" }, temperature }
                : new { model, messages = new object[] { new { role = "system", content = system }, new { role = "user", content = user } }, temperature };

            using var doc = await AIChatTransport.PostJsonAsync(
                http, url, body, h => h.Authorization = new AuthenticationHeaderValue("Bearer", apiKey), log, provider, ct, attempts: 1);
            if (doc == null) return null;
            if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0) return null;
            var msg = choices[0].GetProperty("message");
            return msg.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String
                ? AIChatTransport.CleanModelText(c.GetString()) : null;
        }
    }
}
