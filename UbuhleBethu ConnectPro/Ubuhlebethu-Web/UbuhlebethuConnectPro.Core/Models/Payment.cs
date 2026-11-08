using System;

namespace UbuhlebethuConnectPro.Core.Models
{
    public class Payment
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public bool IsDeleted { get; set; } = false;
        public Guid InvoiceId { get; set; }
        public decimal Amount { get; set; }
        public DateTime PaidAt { get; set; } = DateTime.UtcNow;
        public string? PaymentReference { get; set; }

        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.EFT;
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
        public string? ProofPath { get; set; }
        public string? RecordedBy { get; set; }
        public string? VerifiedBy { get; set; }
        public DateTime? VerifiedAt { get; set; }
    }
}
