using System;

namespace UbuhlebethuConnectPro.Core.Models
{
    public class Material
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Barcode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal UnitCost { get; set; }
        public string Unit { get; set; } = "Units";
        public Guid? ProjectId { get; set; }
        public string? RegisteredBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; } = false;
    }
}
