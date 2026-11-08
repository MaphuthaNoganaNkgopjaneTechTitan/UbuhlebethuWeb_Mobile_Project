using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.Core.Repositories;
using UbuhlebethuConnectPro.Core.Services;

namespace UbuhlebethuConnectPro.Infrastructure.Services
{
    public class QuotationService : IQuotationService
    {
        private readonly IQuotationRepository _quoteRepo;
        private readonly IInvoiceRepository _invoiceRepo;
        private readonly IInvoiceFactory _invoiceFactory;
        private readonly IProjectRepository _projectRepo;
        private readonly List<INotificationObserver> _observers = new List<INotificationObserver>();
        private readonly QuotationStateMachine _stateMachine;

        public QuotationService(IQuotationRepository quoteRepo, IInvoiceRepository invoiceRepo, IInvoiceFactory invoiceFactory, IProjectRepository projectRepo)
        {
            _quoteRepo = quoteRepo;
            _invoiceRepo = invoiceRepo;
            _invoiceFactory = invoiceFactory;
            _projectRepo = projectRepo;
            _stateMachine = new QuotationStateMachine();
        }

        public void AttachObserver(INotificationObserver observer)
        {
            _observers.Add(observer);
        }

        private async Task NotifyAllAsync(string userId, string message, string reference)
        {
            foreach (var obs in _observers)
            {
                await obs.NotifyAsync(userId, message, reference);
            }
        }

        public async Task<QuoteRequest> CreateQuoteAsync(QuoteRequest request, string username)
        {
            request.Id = Guid.NewGuid();
            request.RequestDate = DateTime.UtcNow;
            request.Reference = "UBU-" + Random.Shared.Next(1000, 9999);
            request.Status = "Submitted"; // Immediately visible to Office
            request.CreatedBy = username;

            await _quoteRepo.AddAsync(request);
            await _quoteRepo.SaveChangesAsync();

            await NotifyAllAsync("office", $"New quotation submitted: {request.Reference} by {request.ClientName}", request.Reference);

            return request;
        }

        // 1. Office sends to Manager for sign-off
        public async Task SendToManagerForReviewAsync(Guid quoteId, decimal? updatedEstimate, string senderUsername)
        {
            var quote = await _quoteRepo.GetByIdAsync(quoteId);
            if (quote == null) throw new KeyNotFoundException("Quote not found");

            if (updatedEstimate.HasValue && updatedEstimate.Value > 0)
            {
                quote.EstimatedBudget = updatedEstimate.Value;
            }

            quote.Status = "Under Review";
            await _quoteRepo.SaveChangesAsync();

            await NotifyAllAsync(quote.CreatedBy ?? "", $"Your quotation {quote.Reference} is now Under Review with Management.", quote.Reference ?? "");
            await NotifyAllAsync("manager", $"Quotation {quote.Reference} has been sent by {senderUsername} for your review.", quote.Reference ?? "");
        }

        // 2. Manager accepts quotation (signs off)
        public async Task ManagerApproveQuoteAsync(Guid quoteId, string managerUsername)
        {
            var quote = await _quoteRepo.GetByIdAsync(quoteId);
            if (quote == null) throw new KeyNotFoundException("Quote not found");

            quote.Status = "ManagerApproved";

            // Prepare or update invoice so invoice amount is the same as the approved quotation
            if (quote.InvoiceId.HasValue)
            {
                var existingInvoice = await _invoiceRepo.GetByIdAsync(quote.InvoiceId.Value);
                if (existingInvoice != null)
                {
                    existingInvoice.Amount = quote.EstimatedBudget;
                }
            }
            else
            {
                var invoice = _invoiceFactory.CreateFromQuote(quote);
                invoice.Amount = quote.EstimatedBudget;
                await _invoiceRepo.AddAsync(invoice);
                quote.InvoiceId = invoice.Id;
            }

            await _quoteRepo.SaveChangesAsync();

            await NotifyAllAsync("office", $"Quotation {quote.Reference} was accepted by {managerUsername}. Ready for final feedback and invoice.", quote.Reference ?? "");
        }

        // 3. Manager rejects quotation with reason
        public async Task ManagerRejectQuoteAsync(Guid quoteId, string rejectionReason, string managerUsername)
        {
            var quote = await _quoteRepo.GetByIdAsync(quoteId);
            if (quote == null) throw new KeyNotFoundException("Quote not found");

            if (string.IsNullOrWhiteSpace(rejectionReason))
                throw new ArgumentException("A reason must be provided for rejecting the quote.");

            quote.Status = "Rejected";
            quote.ProjectDescription = $"{quote.ProjectDescription}\n[Manager Rejection Reason by {managerUsername}]: {rejectionReason}";
            await _quoteRepo.SaveChangesAsync();

            await NotifyAllAsync(quote.CreatedBy ?? "", $"Quotation {quote.Reference} was rejected. Reason: {rejectionReason}", quote.Reference ?? "");
            await NotifyAllAsync("office", $"Quotation {quote.Reference} was rejected by {managerUsername}. Reason: {rejectionReason}", quote.Reference ?? "");
        }

        // 4. QS sends feedback, changes status to Approved, generates invoice and project
        public async Task<Invoice> QsFinalizeAndSendFeedbackAsync(Guid quoteId, string feedbackText, string qsUsername)
        {
            using var tx = await _quoteRepo.BeginTransactionAsync();
            try
            {
                var quote = await _quoteRepo.GetByIdAsync(quoteId);
                if (quote == null) throw new KeyNotFoundException("Quote not found");

                quote.Status = "Invoiced";

                Invoice invoice;
                if (quote.InvoiceId.HasValue)
                {
                    var existingInvoice = await _invoiceRepo.GetByIdAsync(quote.InvoiceId.Value);
                    if (existingInvoice != null)
                    {
                        existingInvoice.Amount = quote.EstimatedBudget;
                        invoice = existingInvoice;
                    }
                    else
                    {
                        invoice = _invoiceFactory.CreateFromQuote(quote);
                        invoice.Amount = quote.EstimatedBudget;
                        await _invoiceRepo.AddAsync(invoice);
                        quote.InvoiceId = invoice.Id;
                    }
                }
                else
                {
                    invoice = _invoiceFactory.CreateFromQuote(quote);
                    invoice.Amount = quote.EstimatedBudget;
                    await _invoiceRepo.AddAsync(invoice);
                    quote.InvoiceId = invoice.Id;
                }

                var project = new Project
                {
                    Id = Guid.NewGuid(),
                    QuoteRequestId = quote.Id,
                    Title = quote.ProjectDescription ?? "New Project",
                    StartDate = DateTime.UtcNow,
                    Status = "PendingPayment",
                    SiteManagerName = qsUsername,
                    ClientName = quote.ClientName ?? "Unknown",
                    ClientUserName = quote.CreatedBy ?? "Unknown",
                    QuoteReference = quote.Reference ?? "",
                    EstimatedCost = quote.EstimatedBudget,
                    InvoicedToDate = quote.EstimatedBudget,
                    VariancePercent = 0,
                    Location = quote.SiteLocation ?? "Gauteng",
                    ProjectType = quote.ProjectType ?? "General Construction"
                };

                await _projectRepo.AddAsync(project);
                await _quoteRepo.SaveChangesAsync();

                string clientMsg = string.IsNullOrWhiteSpace(feedbackText)
                    ? $"Your quotation {quote.Reference} has been finalized. Invoice #{invoice.Id} generated."
                    : $"Quotation {quote.Reference} finalized! QS Feedback: {feedbackText}";

                await NotifyAllAsync(quote.CreatedBy ?? "", clientMsg, quote.Reference ?? "");

                await tx.CommitAsync();
                return invoice;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task<Invoice> MarkAsPaidAsync(Guid quoteId, string username, bool isSystemAdmin)
        {
            using var tx = await _quoteRepo.BeginTransactionAsync();
            try
            {
                var quote = await _quoteRepo.GetByIdAsync(quoteId);
                if (quote == null) throw new KeyNotFoundException("Quote not found");

                if (quote.Status != "Invoiced" && quote.Status != "Approved" && quote.Status != "ManagerApproved")
                {
                    throw new InvalidOperationException("Quote must be invoiced or approved before payment.");
                }

                if (!isSystemAdmin && quote.CreatedBy != username)
                {
                    throw new UnauthorizedAccessException("Cannot mark another user's quote as paid.");
                }

                if (quote.IsPaid)
                {
                    throw new InvalidOperationException("Already marked as paid.");
                }

                Invoice invoice;
                if (quote.InvoiceId.HasValue)
                {
                    var existingInvoice = await _invoiceRepo.GetByIdAsync(quote.InvoiceId.Value);
                    if (existingInvoice != null)
                    {
                        invoice = existingInvoice;
                    }
                    else
                    {
                        invoice = _invoiceFactory.CreateFromQuote(quote);
                        await _invoiceRepo.AddAsync(invoice);
                        quote.InvoiceId = invoice.Id;
                    }
                }
                else
                {
                    invoice = _invoiceFactory.CreateFromQuote(quote);
                    await _invoiceRepo.AddAsync(invoice);
                    quote.InvoiceId = invoice.Id;
                }

                invoice.Amount = quote.EstimatedBudget;
                invoice.PaidAmount = invoice.Amount;
                invoice.IsPaid = true;
                invoice.Status = InvoiceStatus.Paid;

                quote.IsPaid = true;
                quote.Status = "Paid";

                // Activate the project now that payment has been confirmed by the client
                var project = await _projectRepo.GetByQuoteIdAsync(quote.Id);
                if (project != null)
                {
                    project.Status = "Active";
                    project.StartDate = DateTime.UtcNow;
                    await _projectRepo.SaveChangesAsync();
                }
                else
                {
                    project = new Project
                    {
                        Id = Guid.NewGuid(),
                        QuoteRequestId = quote.Id,
                        Title = quote.ProjectDescription ?? "New Project",
                        StartDate = DateTime.UtcNow,
                        Status = "Active",
                        SiteManagerName = "Site Manager",
                        ClientName = quote.ClientName ?? "Unknown",
                        ClientUserName = quote.CreatedBy ?? "Unknown",
                        QuoteReference = quote.Reference ?? "",
                        EstimatedCost = quote.EstimatedBudget,
                        InvoicedToDate = quote.EstimatedBudget,
                        VariancePercent = 0,
                        Location = quote.SiteLocation ?? "Gauteng",
                        ProjectType = quote.ProjectType ?? "General Construction"
                    };
                    await _projectRepo.AddAsync(project);
                }

                await _quoteRepo.SaveChangesAsync();
                await NotifyAllAsync(quote.CreatedBy ?? "", $"Payment received for {quote.Reference}. Project is now Active!", quote.Reference ?? "");

                await tx.CommitAsync();
                return invoice;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task ApproveQuoteAsync(Guid quoteId, string approverUsername)
        {
            await QsFinalizeAndSendFeedbackAsync(quoteId, "Approved by QS", approverUsername);
        }

        public async Task<Invoice> ApproveQuoteAtomicAsync(Guid quoteId, string approverUsername)
        {
            return await QsFinalizeAndSendFeedbackAsync(quoteId, "Approved atomically", approverUsername);
        }
    }
}