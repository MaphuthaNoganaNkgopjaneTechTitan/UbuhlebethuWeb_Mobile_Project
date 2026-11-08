namespace UbuhlebethuConnectPro.Core.Options
{
    /// <summary>Tunables for the conversational assistant. Bind from the "AIAssistant" config section.</summary>
    public class AIAssistantOptions
    {
        public const string Section = "AIAssistant";
        /// <summary>Hard ceiling for one chat turn across every provider before the local engine answers.</summary>
        public int TotalBudgetSeconds { get; set; } = 50;
        /// <summary>Maximum model-tool-model round trips per turn.</summary>
        public int MaxToolRounds { get; set; } = 4;
        /// <summary>Only the newest N messages are sent to the model (cost + prompt-injection surface control).</summary>
        public int MaxHistoryMessages { get; set; } = 16;
        public int MaxMessageChars { get; set; } = 3000;
    }
}
