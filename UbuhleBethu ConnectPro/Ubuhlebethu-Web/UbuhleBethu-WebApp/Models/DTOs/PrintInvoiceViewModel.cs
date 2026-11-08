using System;
using System.Collections.Generic;

namespace UbuhlebethuConnectPro.WebApp.Models.DTOs
{
    public class MilestoneItemViewModel
    {
        public int StageNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Percentage { get; set; }
        public decimal Amount { get; set; }
        public int RequiredProgress { get; set; }
        public bool IsPaid { get; set; }
        public bool IsEligible { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusClass { get; set; } = "badge-neutral";
    }

    public class PrintInvoiceViewModel
    {
        public Guid? JobId { get; set; }
        public Guid? QuoteId { get; set; }
        public Guid? InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public DateTime IssueDate { get; set; } = DateTime.UtcNow;
        public DateTime DueDate { get; set; } = DateTime.UtcNow.AddDays(30);

        // Client info
        public string ClientName { get; set; } = string.Empty;
        public string ClientUserName { get; set; } = string.Empty;
        public string ClientEmail { get; set; } = string.Empty;
        public string ClientPhone { get; set; } = string.Empty;
        public string SiteLocation { get; set; } = string.Empty;

        // Project info
        public string ProjectType { get; set; } = string.Empty;
        public string ProjectDescription { get; set; } = string.Empty;
        public string SiteManager { get; set; } = string.Empty;
        public int Progress { get; set; }
        public int Variance { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool NhbrcCompliant { get; set; } = true;

        // Financials
        public decimal TotalAmount { get; set; }
        public decimal InvoicedAmount { get; set; }
        public decimal RemainingAmount => Math.Max(0, TotalAmount - InvoicedAmount);
        public decimal VatRate { get; set; } = 0.15m;
        public decimal Subtotal => Math.Round(TotalAmount / (1m + VatRate), 2);
        public decimal VatAmount => TotalAmount - Subtotal;
        public bool IsFullyPaid => InvoicedAmount >= TotalAmount && TotalAmount > 0;

        // Milestones
        public List<MilestoneItemViewModel> Milestones { get; set; } = new();

        // Banking details for payment simulation
        public string BankName { get; set; } = "First National Bank (FNB)";
        public string AccountName { get; set; } = "Ubuhlebethu Business Enterprise cc";
        public string AccountNumber { get; set; } = "62000000000";
        public string BranchCode { get; set; } = "250655";
        public string AccountType { get; set; } = "Commercial Business Cheque Account";
        public string PaymentTerms { get; set; } = "Progressive Milestone Invoicing - Net 30 Days";
    }
}
