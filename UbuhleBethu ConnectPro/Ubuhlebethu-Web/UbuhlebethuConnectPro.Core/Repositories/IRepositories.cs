using Microsoft.EntityFrameworkCore.Storage;
using UbuhlebethuConnectPro.Core.Models;

namespace UbuhlebethuConnectPro.Core.Repositories
{
    public interface IQuotationRepository
    {
        Task<QuoteRequest?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<bool> ReferenceExistsAsync(string reference, CancellationToken ct = default);
        Task<List<QuoteRequest>> GetByStatusesAsync(IEnumerable<string> statuses, CancellationToken ct = default);
        Task<List<QuoteRequest>> GetByClientAsync(string username, DateTime? from = null, DateTime? to = null, string? status = null, CancellationToken ct = default);
        Task<List<QuoteRequest>> SearchAsync(string term, string? ownerUsername, CancellationToken ct = default);
        Task AddAsync(QuoteRequest quote, CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);
        Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);
    }

    public interface IInvoiceRepository
    {
        Task<Invoice?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<Invoice?> GetByQuoteIdAsync(Guid quoteId, CancellationToken ct = default);
        Task<List<Invoice>> GetRecentAsync(int take, CancellationToken ct = default);
        IQueryable<Invoice> Query();
        Task AddAsync(Invoice invoice, CancellationToken ct = default);
        Task AddPaymentAsync(Payment payment, CancellationToken ct = default);
    }

    public interface IProjectRepository
    {
        Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<Project?> GetByQuoteIdAsync(Guid quoteId, CancellationToken ct = default);
        Task<List<Project>> GetAllAsync(CancellationToken ct = default);
        IQueryable<Project> Query();
        Task AddAsync(Project project, CancellationToken ct = default);
        Task<List<MaterialTask>> GetTasksAsync(Guid projectId, CancellationToken ct = default);
        Task<MaterialTask?> GetTaskAsync(Guid taskId, CancellationToken ct = default);
        Task AddTaskAsync(MaterialTask task, CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);
    }

    public interface ISiteReportRepository
    {
        Task<SiteReport?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<SiteReport?> GetExistingReportForRetryAsync(Guid reportId, CancellationToken ct = default);
        Task<List<SiteReport>> GetByProjectAsync(Guid projectId, int take = 50, CancellationToken ct = default);
        Task<List<SiteReport>> GetByUserAsync(string username, int take = 50, CancellationToken ct = default);
        Task AddAsync(SiteReport report, CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);

    }

    public interface IExpenseRepository
    {
        Task<ExpenseRecord?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<List<ExpenseRecord>> GetByProjectAsync(Guid projectId, CancellationToken ct = default);
        Task<List<ExpenseRecord>> GetByProjectAndStatusAsync(Guid projectId, string status, CancellationToken ct = default);
        Task AddAsync(ExpenseRecord expense, CancellationToken ct = default);
        Task UpdateAsync(ExpenseRecord expense, CancellationToken ct = default);
        Task<decimal> GetTotalApprovedExpensesAsync(Guid projectId, CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);
    }

    public interface INotificationRepository
    {
        Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<List<Notification>> GetUnreadAsync(string userId, CancellationToken ct = default);
        Task AddAsync(Notification notification, CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
