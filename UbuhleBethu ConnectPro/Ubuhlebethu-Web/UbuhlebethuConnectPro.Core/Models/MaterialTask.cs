using System;

namespace UbuhlebethuConnectPro.Core.Models
{
    public class MaterialTask
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public bool IsDeleted { get; set; } = false;
        public Guid ProjectId { get; set; }
        public string TaskName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public bool Completed { get; set; }
        public DateTime DateRequired { get; set; }
        public string? AssignedTo { get; set; }
    }
}
