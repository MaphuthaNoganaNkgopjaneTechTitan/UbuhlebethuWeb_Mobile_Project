using System;
using System.Collections.Generic;

namespace UbuhlebethuConnectPro.Core.Services.AI
{
    // ─── Conversation message types ───────────────────────────────────────────

    /// <summary>A single message in a multi-turn conversation.</summary>
    public class AIChatMessage
    {
        public string Role { get; set; } = "user";      // "user" | "assistant" | "system"
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Rich request carrying full conversation history + optional live-data context.</summary>
    public class AIChatRequest
    {
        /// <summary>Full conversation history (newest last). Minimum: one user message.</summary>
        public List<AIChatMessage> Messages { get; set; } = new();

        /// <summary>If set, the AI will load live project data to answer questions about this project.</summary>
        public Guid? ProjectId { get; set; }

        /// <summary>The calling user's role (Admin, Manager, SiteManager, FieldWorker, …).</summary>
        public string? UserRole { get; set; }

        /// <summary>The caller's username — used for personalised context.</summary>
        public string? UserName { get; set; }

        /// <summary>Preferred AI provider override: "gemini" | "groq" | "nvidia" | null (auto).</summary>
        public string? Provider { get; set; }

        /// <summary>Server-populated: formatted per-user memory injected into the system prompt. Never trusted from clients.</summary>
        public string? MemoryContext { get; set; }

        /// <summary>Server-populated: true only when the caller presented a valid identity.</summary>
        public bool IsAuthenticated { get; set; }
    }

    /// <summary>Response from the conversational AI pipeline.</summary>
    public class AIChatResponse
    {
        public string Text { get; set; } = string.Empty;
        public bool UsedFallback { get; set; }
        public string Source { get; set; } = string.Empty;

        /// <summary>Optional: list of tools the AI invoked to answer the question.</summary>
        public List<string> ToolsUsed { get; set; } = new();
    }

    // ─── Tool / function calling types ────────────────────────────────────────

    /// <summary>Represents a single tool definition passed to the LLM.</summary>
    public class AITool
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    /// <summary>A tool call returned by the LLM that the runtime must execute.</summary>
    public class AIToolCall
    {
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, string> Arguments { get; set; } = new();
    }

    /// <summary>Result of executing a tool call.</summary>
    public class AIToolResult
    {
        public string ToolName { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
        public bool Success { get; set; } = true;
    }
}
