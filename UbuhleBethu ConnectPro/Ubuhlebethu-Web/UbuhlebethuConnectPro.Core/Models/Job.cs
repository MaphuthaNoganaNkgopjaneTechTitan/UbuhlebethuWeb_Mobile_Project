namespace UbuhlebethuConnectPro.Core.Models
{
    public class Job
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public bool IsDeleted { get; set; } = false;
        public string JobNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string BuildingType { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string ClientUserName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public int Progress { get; set; }
        public int Variance { get; set; }
        public decimal EstimatedCost { get; set; }
        public decimal InvoicedAmount { get; set; }
        public string Status { get; set; } = "In Progress";
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public string SiteManager { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double GeofenceRadiusMeters { get; set; } = 300.0;
        public bool NhbrcCompliant { get; set; } = true;
        public string Notes { get; set; } = string.Empty;
    }
}
