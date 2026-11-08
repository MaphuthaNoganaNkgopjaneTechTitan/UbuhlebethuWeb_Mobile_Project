using System;
using System.Threading.Tasks;
using UbuhlebethuConnectPro.Core.Models;

namespace UbuhlebethuConnectPro.Core.Services
{
    public interface IQuotationService
    {
        Task<QuoteRequest> CreateQuoteAsync(QuoteRequest request, string username);
        Task<Invoice> MarkAsPaidAsync(Guid quoteId, string username, bool isSystemAdmin);
        Task ApproveQuoteAsync(Guid quoteId, string approverUsername);
        Task<Invoice> ApproveQuoteAtomicAsync(Guid quoteId, string approverUsername);
        void AttachObserver(INotificationObserver observer);

        Task SendToManagerForReviewAsync(Guid quoteId, decimal? updatedEstimate, string senderUsername);
        Task ManagerApproveQuoteAsync(Guid quoteId, string managerUsername);
        Task ManagerRejectQuoteAsync(Guid quoteId, string rejectionReason, string managerUsername);
        Task<Invoice> QsFinalizeAndSendFeedbackAsync(Guid quoteId, string feedbackText, string qsUsername);
    }
}
