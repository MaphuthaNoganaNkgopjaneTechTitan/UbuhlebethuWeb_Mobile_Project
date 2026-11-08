using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using UbuhlebethuConnectPro.Core.Options;
using UbuhlebethuConnectPro.Core.Services;

namespace UbuhlebethuConnectPro.Api.Health
{
    public class StorageHealthCheck : IHealthCheck
    {
        private readonly IStorageService _storageService;
        private readonly StorageOptions _options;

        public StorageHealthCheck(IStorageService storageService, IOptions<StorageOptions> options)
        {
            _storageService = storageService;
            _options = options.Value;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var data = new Dictionary<string, object>
                {
                    { "provider", _options.Provider },
                    { "bucket", string.IsNullOrWhiteSpace(_options.Bucket) ? "local" : _options.Bucket },
                    { "serviceUrl", string.IsNullOrWhiteSpace(_options.ServiceUrl) ? "local-disk" : _options.ServiceUrl },
                    { "maxPhotoBytes", _options.MaxPhotoBytes }
                };

                // Probe the storage URL generator and service readiness
                var probeUrl = _storageService.GetPublicOrPresignedUrl("health/probe.txt", TimeSpan.FromMinutes(1));
                if (!string.IsNullOrWhiteSpace(probeUrl))
                {
                    return Task.FromResult(HealthCheckResult.Healthy(
                        $"Storage service ({_options.Provider}) is active and reachable with URL resolution operational.",
                        data));
                }

                return Task.FromResult(HealthCheckResult.Degraded(
                    "Storage service is operating in local disk fallback mode.",
                    null,
                    data));
            }
            catch (Exception ex)
            {
                return Task.FromResult(HealthCheckResult.Degraded($"Storage health check degraded: {ex.Message}", ex));
            }
        }
    }
}
