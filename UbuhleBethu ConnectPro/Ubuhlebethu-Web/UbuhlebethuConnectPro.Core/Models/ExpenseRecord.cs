using System;

namespace UbuhlebethuConnectPro.Core.Models
{
    public class ExpenseRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ManagerName { get; set; } = string.Empty;
        public string ItemDescription { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = "APPROVED";
        public Guid ProjectId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
