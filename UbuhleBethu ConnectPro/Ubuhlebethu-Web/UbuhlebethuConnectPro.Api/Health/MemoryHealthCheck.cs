using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace UbuhlebethuConnectPro.Api.Health
{
    public class MemoryHealthCheck : IHealthCheck
    {
        // 1 GB threshold for degraded warning, 1.5 GB for critical unhealthy status
        private const long DegradedThresholdBytes = 1024L * 1024L * 1024L;
        private const long UnhealthyThresholdBytes = 1536L * 1024L * 1024L;

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var allocatedBytes = GC.GetTotalMemory(forceFullCollection: false);
            long workingSetBytes;
            try
            {
                using var currentProc = Process.GetCurrentProcess();
                workingSetBytes = currentProc.WorkingSet64;
            }
            catch
            {
                workingSetBytes = allocatedBytes;
            }

            var allocatedMb = Math.Round((double)allocatedBytes / (1024 * 1024), 2);
            var workingSetMb = Math.Round((double)workingSetBytes / (1024 * 1024), 2);

            var data = new Dictionary<string, object>
            {
                { "allocatedMemoryMb", allocatedMb },
                { "workingSetMb", workingSetMb },
                { "gen0Collections", GC.CollectionCount(0) },
                { "gen1Collections", GC.CollectionCount(1) },
                { "gen2Collections", GC.CollectionCount(2) }
            };

            if (workingSetBytes > UnhealthyThresholdBytes)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"Memory usage critically high: {workingSetMb} MB exceeds safety threshold of 1536 MB.",
                    null,
                    data));
            }

            if (workingSetBytes > DegradedThresholdBytes)
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"Memory usage elevated: {workingSetMb} MB exceeds warning threshold of 1024 MB.",
                    null,
                    data));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                $"Process memory within optimal parameters: Working Set {workingSetMb} MB, GC Allocated {allocatedMb} MB.",
                data));
        }
    }
}
