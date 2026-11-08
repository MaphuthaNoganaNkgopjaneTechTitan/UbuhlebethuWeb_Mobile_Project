using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.Core.Repositories;
using UbuhlebethuConnectPro.WebApp.Data;

namespace UbuhlebethuConnectPro.Infrastructure.Repositories
{
    public class SystemSettingsRepository : ISystemSettingsRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public SystemSettingsRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<SystemSettings> GetSettingsAsync(CancellationToken ct = default)
        {
            var settings = await _dbContext.Set<SystemSettings>().FirstOrDefaultAsync(ct);
            if (settings == null)
            {
                settings = new SystemSettings();
                _dbContext.Set<SystemSettings>().Add(settings);
                await _dbContext.SaveChangesAsync(ct);
            }
            return settings;
        }

        public async Task UpdateSettingsAsync(SystemSettings settings, CancellationToken ct = default)
        {
            var existing = await GetSettingsAsync(ct);
            existing.CompanyName = settings.CompanyName;
            existing.VatNumber = settings.VatNumber;
            existing.Address = settings.Address;
            existing.LogoPath = settings.LogoPath;
            existing.QuotationExpiryDays = settings.QuotationExpiryDays;
            existing.DefaultPaymentTerms = settings.DefaultPaymentTerms;
            existing.AIConfidenceThreshold = settings.AIConfidenceThreshold;
            existing.AIFeatureEnabled = settings.AIFeatureEnabled;
            existing.QuoteApprovedTemplate = settings.QuoteApprovedTemplate;
            existing.PaymentReceivedTemplate = settings.PaymentReceivedTemplate;
            existing.UpdatedAt = System.DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct);
        }
    }
}
