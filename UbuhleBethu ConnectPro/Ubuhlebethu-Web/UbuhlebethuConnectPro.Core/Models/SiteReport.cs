using System;

namespace UbuhlebethuConnectPro.Core.Models
{
    public class SiteReport
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ProjectId { get; set; }
        public string ReportedBy { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int ProgressPercent { get; set; }
        public string ConstructionStage { get; set; } = string.Empty;
        public string? PhotoPath { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public DateTime LoggedAt { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Submitted";
        public bool IsOffSite { get; set; }
        public double? DistanceFromSiteMeters { get; set; }
        public bool IsDeleted { get; set; }
    }
}
