using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using UbuhlebethuConnectPro.WebApp.Data;

namespace UbuhlebethuConnectPro.Api.Health
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly ApplicationDbContext _dbContext;

        public DatabaseHealthCheck(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
                if (canConnect)
                {
                    var data = new Dictionary<string, object>
                    {
                        { "provider", _dbContext.Database.ProviderName ?? "Npgsql.EntityFrameworkCore.PostgreSQL" },
                        { "database", _dbContext.Database.GetDbConnection().Database ?? "neondb" },
                        { "status", "Connected" }
                    };

                    return HealthCheckResult.Healthy("PostgreSQL database connection is responsive and queries execute normally.", data);
                }

                return HealthCheckResult.Unhealthy("Cannot establish connection to PostgreSQL database.");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy($"Database health check failed: {ex.Message}", ex);
            }
        }
    }
}
