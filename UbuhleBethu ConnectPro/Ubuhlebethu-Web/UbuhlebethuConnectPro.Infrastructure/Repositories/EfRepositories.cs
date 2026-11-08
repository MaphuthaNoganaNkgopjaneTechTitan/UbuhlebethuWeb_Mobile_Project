using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.WebApp.Data;
using UbuhlebethuConnectPro.Core.Repositories;

namespace UbuhlebethuConnectPro.Core.Repositories
{
    /// <summary>Repository pattern: services depend on the interface, never on EF Core directly.</summary>
    public class QuotationRepository : IQuotationRepository
    {
        private readonly ApplicationDbContext _db;
        public QuotationRepository(ApplicationDbContext db) => _db = db;

        public Task<QuoteRequest?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            _db.QuoteRequests.FirstOrDefaultAsync(q => q.Id == id, ct);

        public Task<bool> ReferenceExistsAsync(string reference, CancellationToken ct = default) =>
            _db.QuoteRequests.IgnoreQueryFilters().AnyAsync(q => q.Reference == reference, ct);

        public Task<List<QuoteRequest>> GetByStatusesAsync(IEnumerable<string> statuses, CancellationToken ct = default)
        {
            var list = statuses.ToList();
            return _db.QuoteRequests.AsNoTracking().Where(q => list.Contains(q.Status))
                .OrderByDescending(q => q.RequestDate).ToListAsync(ct);
        }

        public Task<List<QuoteRequest>> GetByClientAsync(string username, DateTime? from = null, DateTime? to = null, string? status = null, CancellationToken ct = default)
        {
            var query = _db.QuoteRequests.AsNoTracking().Where(q => q.CreatedBy == username);
            if (from.HasValue) query = query.Where(q => q.RequestDate >= DateTime.SpecifyKind(from.Value, DateTimeKind.Utc));
            if (to.HasValue) query = query.Where(q => q.RequestDate < DateTime.SpecifyKind(to.Value.Date.AddDays(1), DateTimeKind.Utc));
            if (!string.IsNullOrWhiteSpace(status))
            {
                var alt = status == "Under Review" ? "UnderReview" : status;
                query = query.Where(q => q.Status == status || q.Status == alt);
            }

            return query.OrderByDescending(q => q.RequestDate).ToListAsync(ct);
        }

        public Task<List<QuoteRequest>> SearchAsync(string term, string? ownerUsername, CancellationToken ct = default)
        {
            var query = _db.QuoteRequests.AsNoTracking().AsQueryable();
            if (ownerUsername != null) query = query.Where(q => q.CreatedBy == ownerUsername);
            if (!string.IsNullOrWhiteSpace(term))
            {
                // EF Core parameterises these values (no SQL injection).
                query = query.Where(q => q.Reference.Contains(term) || q.ProjectType.Contains(term)
                    || q.SiteLocation.Contains(term) || q.ClientName.Contains(term) || q.Status.Contains(term));
            }
            return query.OrderByDescending(q => q.RequestDate).Take(100).ToListAsync(ct);
        }

        public async Task AddAsync(QuoteRequest quote, CancellationToken ct = default) =>
            await _db.QuoteRequests.AddAsync(quote, ct);

        public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default) =>
            _db.Database.BeginTransactionAsync(ct);
    }

    public class InvoiceRepository : IInvoiceRepository
    {
        private readonly ApplicationDbContext _db;
        public InvoiceRepository(ApplicationDbContext db) => _db = db;

        public Task<Invoice?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            _db.Invoices.FirstOrDefaultAsync(i => i.Id == id, ct);

        public Task<Invoice?> GetByQuoteIdAsync(Guid quoteId, CancellationToken ct = default) =>
            _db.Invoices.FirstOrDefaultAsync(i => i.QuoteRequestId == quoteId, ct);

        public Task<List<Invoice>> GetRecentAsync(int take, CancellationToken ct = default) =>
            _db.Invoices.AsNoTracking().OrderByDescending(i => i.CreatedAt).Take(take).ToListAsync(ct);

        public IQueryable<Invoice> Query() => _db.Invoices.AsNoTracking();

        public async Task AddAsync(Invoice invoice, CancellationToken ct = default) =>
            await _db.Invoices.AddAsync(invoice, ct);

        public async Task AddPaymentAsync(Payment payment, CancellationToken ct = default) =>
            await _db.Payments.AddAsync(payment, ct);
    }

    public class ProjectRepository : IProjectRepository
    {
        private readonly ApplicationDbContext _db;
        public ProjectRepository(ApplicationDbContext db) => _db = db;

        public Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            _db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);

        public Task<Project?> GetByQuoteIdAsync(Guid quoteId, CancellationToken ct = default) =>
            _db.Projects.FirstOrDefaultAsync(p => p.QuoteRequestId == quoteId, ct);

        public Task<List<Project>> GetAllAsync(CancellationToken ct = default) =>
            _db.Projects.AsNoTracking().OrderByDescending(p => p.StartDate).ToListAsync(ct);

        public IQueryable<Project> Query() => _db.Projects.AsNoTracking();

        public async Task AddAsync(Project project, CancellationToken ct = default) =>
            await _db.Projects.AddAsync(project, ct);

        public Task<List<MaterialTask>> GetTasksAsync(Guid projectId, CancellationToken ct = default) =>
            _db.MaterialTasks.Where(t => t.ProjectId == projectId).OrderBy(t => t.Completed).ThenBy(t => t.DateRequired).ToListAsync(ct);

        public Task<MaterialTask?> GetTaskAsync(Guid taskId, CancellationToken ct = default) =>
            _db.MaterialTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct);

        public async Task AddTaskAsync(MaterialTask task, CancellationToken ct = default) =>
            await _db.MaterialTasks.AddAsync(task, ct);

        public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
    }

    public class SiteReportRepository : ISiteReportRepository
    {
        private readonly ApplicationDbContext _db;
        public SiteReportRepository(ApplicationDbContext db) => _db = db;

        public async Task<SiteReport?> GetExistingReportForRetryAsync(
    Guid reportId,
    CancellationToken ct = default)
        {
            return await _db.SiteReports
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    report => report.Id == reportId,
                    ct
                );
        }

        public Task<SiteReport?> GetByIdAsync(Guid id,CancellationToken ct = default) =>
         _db.SiteReports
         .IgnoreQueryFilters()
         .AsNoTracking()
         .FirstOrDefaultAsync(s => s.Id == id, ct);

        public Task<List<SiteReport>> GetByProjectAsync(Guid projectId, int take = 50, CancellationToken ct = default) =>
            _db.SiteReports.AsNoTracking().Where(s => s.ProjectId == projectId).OrderByDescending(s => s.LoggedAt).Take(take).ToListAsync(ct);

        public Task<List<SiteReport>> GetByUserAsync(string username, int take = 50, CancellationToken ct = default) =>
            _db.SiteReports.AsNoTracking().Where(s => s.ReportedBy == username).OrderByDescending(s => s.LoggedAt).Take(take).ToListAsync(ct);

        public async Task AddAsync(SiteReport report, CancellationToken ct = default) =>
            await _db.SiteReports.AddAsync(report, ct);

        public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
    }

    public class ExpenseRepository : IExpenseRepository
    {
        private readonly ApplicationDbContext _db;
        public ExpenseRepository(ApplicationDbContext db) => _db = db;

        public Task<ExpenseRecord?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            _db.ExpenseRecords.FirstOrDefaultAsync(e => e.Id == id, ct);

        public Task<List<ExpenseRecord>> GetByProjectAsync(Guid projectId, CancellationToken ct = default) =>
            _db.ExpenseRecords.AsNoTracking().Where(e => e.ProjectId == projectId).OrderByDescending(e => e.CreatedAt).ToListAsync(ct);

        public Task<List<ExpenseRecord>> GetByProjectAndStatusAsync(Guid projectId, string status, CancellationToken ct = default) =>
            _db.ExpenseRecords.AsNoTracking().Where(e => e.ProjectId == projectId && e.Status == status).OrderByDescending(e => e.CreatedAt).ToListAsync(ct);

        public async Task AddAsync(ExpenseRecord expense, CancellationToken ct = default) =>
            await _db.ExpenseRecords.AddAsync(expense, ct);

        public Task UpdateAsync(ExpenseRecord expense, CancellationToken ct = default)
        {
            _db.ExpenseRecords.Update(expense);
            return Task.CompletedTask;
        }

        public Task<decimal> GetTotalApprovedExpensesAsync(Guid projectId, CancellationToken ct = default) =>
            _db.ExpenseRecords.AsNoTracking()
                .Where(e => e.ProjectId == projectId && e.Status == "APPROVED")
                .SumAsync(e => e.Amount, ct);

        public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
    }

    public class NotificationRepository : INotificationRepository
    {
        private readonly ApplicationDbContext _db;
        public NotificationRepository(ApplicationDbContext db) => _db = db;
        public async Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => await _db.Notifications.FindAsync(new object[] { id }, ct);
        public async Task<List<Notification>> GetUnreadAsync(string userId, CancellationToken ct = default)
            => await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(_db.Notifications.Where(n => n.UserId == userId && !n.IsRead).OrderByDescending(n => n.CreatedAt), ct);
        public async Task AddAsync(Notification notification, CancellationToken ct = default)
            => await _db.Notifications.AddAsync(notification, ct);
        public async Task SaveChangesAsync(CancellationToken ct = default)
            => await _db.SaveChangesAsync(ct);
    }
}
