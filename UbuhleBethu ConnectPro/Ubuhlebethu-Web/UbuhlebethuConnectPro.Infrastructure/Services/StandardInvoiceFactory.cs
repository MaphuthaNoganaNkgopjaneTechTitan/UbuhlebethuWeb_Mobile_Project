using System;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.Core.Services;

namespace UbuhlebethuConnectPro.Infrastructure.Services
{
    public class StandardInvoiceFactory : IInvoiceFactory
    {
        public Invoice CreateFromQuote(QuoteRequest quote)
        {
            return new Invoice
            {
                Id = Guid.NewGuid(),
                QuoteRequestId = quote.Id,
                Amount = quote.EstimatedBudget,
                CreatedAt = DateTime.UtcNow,
                IsPaid = false,
                DocumentUrl = "/invoices/" + Guid.NewGuid() + ".pdf"
            };
        }
    }
}
