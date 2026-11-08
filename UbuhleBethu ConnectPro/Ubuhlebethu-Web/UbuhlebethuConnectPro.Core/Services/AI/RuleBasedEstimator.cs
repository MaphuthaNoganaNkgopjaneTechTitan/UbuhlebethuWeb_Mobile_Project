using UbuhlebethuConnectPro.Core.Models;

namespace UbuhlebethuConnectPro.Core.Services.AI
{
    /// <summary>
    /// Deterministic fallback used when the AI service is slow, unavailable or low-confidence (NFR-06).
    /// Rates are indicative only and must be validated by a QS before quoting.
    /// </summary>
    public static class RuleBasedEstimator
    {
        // Indicative ZAR per m2 by project category.
        public static decimal RatePerSquareMetre(string projectType)
        {
            var t = projectType ?? string.Empty;
            if (t.Contains("RDP", StringComparison.OrdinalIgnoreCase) || t.Contains("Social", StringComparison.OrdinalIgnoreCase)) return 6500m;
            if (t.Contains("Civil", StringComparison.OrdinalIgnoreCase)) return 4800m;
            if (t.Contains("Clinic", StringComparison.OrdinalIgnoreCase) || t.Contains("School", StringComparison.OrdinalIgnoreCase)) return 12500m;
            if (t.Contains("Industrial", StringComparison.OrdinalIgnoreCase)) return 9000m;
            return 14500m; // commercial / default
        }

        public static decimal EstimateFromArea(string projectType, decimal areaM2, string quality = "Standard")
        {
            var grade = quality switch { "Premium" => 1.25m, "Medium" => 1.1m, _ => 1.0m };
            return decimal.Round(RatePerSquareMetre(projectType) * Math.Max(0, areaM2) * grade, 2);
        }

        /// <summary>Estimate for a request that has no floor area: anchor on the client's stated budget.</summary>
        public static decimal Estimate(QuoteRequest q)
        {
            var factor = q.ProjectType switch
            {
                var s when s.Contains("RDP", StringComparison.OrdinalIgnoreCase) => 0.95m,
                var s when s.Contains("Clinic", StringComparison.OrdinalIgnoreCase) => 1.05m,
                var s when s.Contains("School", StringComparison.OrdinalIgnoreCase) => 1.0m,
                var s when s.Contains("Industrial", StringComparison.OrdinalIgnoreCase) => 1.08m,
                _ => 1.0m
            };
            var estimate = q.EstimatedBudget > 0 ? q.EstimatedBudget * factor : 250_000m * factor;
            return decimal.Round(Math.Max(20_000m, estimate), 2);
        }
    }
}
