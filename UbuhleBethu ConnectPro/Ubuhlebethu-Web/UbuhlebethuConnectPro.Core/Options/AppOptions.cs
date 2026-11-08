namespace UbuhlebethuConnectPro.Core.Options
{
    public class GeminiOptions
    {
        public const string Section = "Gemini";
        public string? ApiKey { get; set; }
        public string Model { get; set; } = "gemini-3.8-flash";
        /// <summary>NFR-06: fall back within 3 seconds if the AI service is slow or unavailable.</summary>
        public int TimeoutSeconds { get; set; } = 3;
        /// <summary>Conversational chat needs far longer than the 3s estimate budget (tool calls = several round trips).</summary>
        public int ChatTimeoutSeconds { get; set; } = 25;
        /// <summary>Tried in order when the primary model is overloaded (503) or unavailable.</summary>
        public string[] FallbackModels { get; set; } = new[] { "gemini-2.5-flash", "gemini-flash-latest" };
    }

    public class JwtOptions
    {
        public const string Section = "Jwt";
        public string Key { get; set; } = string.Empty;
        public string Issuer { get; set; } = "UbuhlebethuConnectPro";
        public string Audience { get; set; } = "UbuhlebethuConnectPro.Clients";
        public int ExpiryMinutes { get; set; } = 60;
    }

    public class StorageOptions
    {
        public const string Section = "Storage";
        public string Provider { get; set; } = "local"; // "local" or "s3"
        public string ServiceUrl { get; set; } = string.Empty;
        public string Bucket { get; set; } = string.Empty;
        public string AccessKeyId { get; set; } = string.Empty;
        public string SecretAccessKey { get; set; } = string.Empty;
        public string UploadPath { get; set; } = "wwwroot/uploads";
        public long MaxPhotoBytes { get; set; } = 5 * 1024 * 1024;
    }
}


