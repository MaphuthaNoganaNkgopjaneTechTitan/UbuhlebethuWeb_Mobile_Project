using System;

namespace UbuhlebethuConnectPro.Core.Models
{
    public class SystemSettings
    {
        public int Id { get; set; }
        
        // Company Profile
        public string CompanyName { get; set; } = "Ubuhlebethu ConnectPro";
        public string VatNumber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string LogoPath { get; set; } = string.Empty;
        
        // Settings
        public int QuotationExpiryDays { get; set; } = 30;
        public string DefaultPaymentTerms { get; set; } = "Net 30";
        
        // AI
        public double AIConfidenceThreshold { get; set; } = 0.75;
        public bool AIFeatureEnabled { get; set; } = true;
        
        // Notification Templates
        public string QuoteApprovedTemplate { get; set; } = "Your quotation {QuoteId} has been approved.";
        public string PaymentReceivedTemplate { get; set; } = "We have received your payment of {Amount} for Invoice {InvoiceId}.";
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
