using System.Linq;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.WebApp.Models;

namespace UbuhlebethuConnectPro.WebApp.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public DbSet<ExpenseRecord> ExpenseRecords { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // Application entities
        public DbSet<QuoteRequest> QuoteRequests { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<MaterialTask> MaterialTasks { get; set; }
        public DbSet<SiteReport> SiteReports { get; set; }
        public DbSet<AttendanceRecord> AttendanceRecords { get; set; }
        public DbSet<SupportTicket> SupportTickets { get; set; }
        public DbSet<Material> Materials { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            foreach (var property in builder.Model.GetEntityTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                property.SetColumnType("decimal(18,2)");
            }

            // Configure STPH and Query Filters
            builder.Entity<ApplicationUser>(b => {
                b.HasQueryFilter(e => !e.IsDeleted);
                b.HasIndex(e => e.IsDeleted);
            });

            builder.Entity<SupportTicket>(b => {
                b.HasIndex(e => e.CreatedBy);
                b.HasIndex(e => e.Status);
            });

            builder.Entity<QuoteRequest>(b => {
                b.HasQueryFilter(e => !e.IsDeleted);
                b.HasIndex(e => e.IsDeleted);
                b.HasIndex(e => e.Reference).IsUnique();
                b.HasIndex(e => e.CreatedBy);
                b.HasOne<Invoice>()
                 .WithOne()
                 .HasForeignKey<QuoteRequest>(e => e.InvoiceId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<Invoice>(b => {
                b.HasQueryFilter(e => !e.IsDeleted);
                b.HasIndex(e => e.IsDeleted);
                b.HasIndex(e => e.QuoteRequestId).IsUnique();
                b.Property(e => e.Status).HasConversion<string>();
            });

            builder.Entity<Project>(b => {
                b.HasKey(e => e.Id);
                b.HasQueryFilter(e => !e.IsDeleted);
                b.HasIndex(e => e.IsDeleted);
            });

            builder.Entity<ExpenseRecord>(b => {
                b.HasKey(e => e.Id);
                b.Property(e => e.ProjectId).HasColumnType("uuid");
            });

            builder.Entity<SiteReport>(b => {
                b.HasQueryFilter(e => !e.IsDeleted);
                b.HasIndex(e => e.IsDeleted);
            });

            builder.Entity<MaterialTask>(b => {
                b.HasQueryFilter(e => !e.IsDeleted);
                b.HasIndex(e => e.IsDeleted);
            });

            builder.Entity<Notification>(b => {
                b.HasQueryFilter(e => !e.IsDeleted);
                b.HasIndex(e => e.IsDeleted);
            });

            builder.Entity<Payment>(b => {
                b.HasQueryFilter(e => !e.IsDeleted);
                b.HasIndex(e => e.IsDeleted);
            });

            builder.Entity<AuditLog>(b => {
                b.HasIndex(e => e.Timestamp);
                b.HasIndex(e => e.TargetUserId);
            });

            builder.Entity<Material>(b => {
                b.HasQueryFilter(e => !e.IsDeleted);
                b.HasIndex(e => e.Barcode);
            });
        }
    }
}
