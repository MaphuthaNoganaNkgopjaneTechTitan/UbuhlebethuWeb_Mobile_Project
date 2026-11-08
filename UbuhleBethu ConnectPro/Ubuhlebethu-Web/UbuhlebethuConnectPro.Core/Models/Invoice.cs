using System;

namespace UbuhlebethuConnectPro.Core.Models
{
    public class Invoice
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public bool IsDeleted { get; set; } = false;
        public Guid QuoteRequestId { get; set; }
        public decimal Amount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsPaid { get; set; } = false;
        public string? DocumentUrl { get; set; }

        public DateTime IssueDate { get; set; } = DateTime.UtcNow;
        public DateTime DueDate { get; set; } = DateTime.UtcNow.AddDays(30);
        public InvoiceStatus Status { get; set; } = InvoiceStatus.Unpaid;
        public decimal PaidAmount { get; set; }

        public void UpdateStatus()
        {
            if (PaidAmount >= Amount && Amount > 0)
            {
                Status = InvoiceStatus.Paid;
                IsPaid = true;
            }
            else if (PaidAmount > 0)
            {
                Status = InvoiceStatus.PartiallyPaid;
                IsPaid = false;
            }
            else if (DateTime.UtcNow > DueDate && Status != InvoiceStatus.Paid)
            {
                Status = InvoiceStatus.Overdue;
                IsPaid = false;
            }
            else
            {
                Status = InvoiceStatus.Unpaid;
                IsPaid = false;
            }
        }
    }
}
