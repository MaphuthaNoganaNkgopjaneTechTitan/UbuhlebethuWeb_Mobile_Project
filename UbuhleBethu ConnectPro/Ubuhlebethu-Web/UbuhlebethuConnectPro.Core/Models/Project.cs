using System;

namespace UbuhlebethuConnectPro.Core.Models
{
    public class Project
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public bool IsDeleted { get; set; } = false;
        public string Title { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string QuoteReference { get; set; } = string.Empty;
        public Guid? QuoteRequestId { get; set; }
        public string ClientUserName { get; set; } = string.Empty;
        public string ProjectType { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string SiteManagerName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Active";
        public decimal EstimatedCost { get; set; }
        public decimal InvoicedToDate { get; set; }
        public int ProgressPercent { get; set; }
        public int VariancePercent { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double GeofenceRadiusMeters { get; set; } = 300.0;
        public bool NhbrcCompliant { get; set; } = true;
        public string Notes { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
