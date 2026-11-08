namespace UbuhlebethuConnectPro.Core.Options
{
    public class GroqOptions
    {
        public const string Section = "Groq";
        public string? ApiKey { get; set; }
        public string Model { get; set; } = "llama-3.3-70b-versatile";
        public int ChatTimeoutSeconds { get; set; } = 25;
    }
}
