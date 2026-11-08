using System;
using System.Collections.Generic;

namespace UbuhlebethuConnectPro.Core.Models.DTOs
{
    public class InvoiceViewModel
    {
        public Guid Id { get; set; }
        public decimal Amount { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid QuoteRequestId { get; set; }
        public bool IsPaid { get; set; }
        
        // Quote Info
        public string QuoteProjectDescription { get; set; } = string.Empty;
        public string QuoteClientName { get; set; } = string.Empty;
        public string QuoteReference { get; set; } = string.Empty;
        public string QuoteSiteLocation { get; set; } = string.Empty;
        public string QuoteProjectType { get; set; } = string.Empty;
    }

    public class ManagerDashboardViewModel
    {
        public int TotalProjects { get; set; }
        public decimal TotalValue { get; set; }
        public decimal TotalInvoiced { get; set; }
        public decimal TotalPaid { get; set; }
        public List<InvoiceViewModel> RecentInvoices { get; set; } = new();
    }
}
