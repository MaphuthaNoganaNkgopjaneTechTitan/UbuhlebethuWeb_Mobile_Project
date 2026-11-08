using System;
using System.Collections.Generic;

namespace UbuhlebethuConnectPro.WebApp.Models.DTOs
{
    public class SystemHealthViewModel
    {
        public string Status { get; set; } = "Healthy";
        public string TotalDuration { get; set; } = "0.0ms";
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
        public SystemInfoViewModel System { get; set; } = new();
        public Dictionary<string, HealthEntryViewModel> Entries { get; set; } = new();
        public bool IsDegradedOrUnhealthy => !string.Equals(Status, "Healthy", StringComparison.OrdinalIgnoreCase);
    }

    public class SystemInfoViewModel
    {
        public string Server { get; set; } = "UbuhleBethu-Api";
        public string Environment { get; set; } = "Production";
        public string Framework { get; set; } = ".NET 10.0";
        public string Os { get; set; } = "Unknown";
        public string Uptime { get; set; } = "0d 0h 0m";
    }

    public class HealthEntryViewModel
    {
        public string Status { get; set; } = "Healthy";
        public string Description { get; set; } = string.Empty;
        public string Duration { get; set; } = "0.0ms";
        public Dictionary<string, object>? Data { get; set; }
    }
}
