using System;
using System.Text;

namespace UbuhlebethuConnectPro.Core.Services.AI
{
    public class CostEstimateResult
    {
        public string BuildingType { get; set; } = "Residential";
        public decimal AreaM2 { get; set; }
        public string Location { get; set; } = "Gauteng";
        public string FinishGrade { get; set; } = "Standard";
        public decimal BaseRatePerM2 { get; set; }
        public decimal AdjustedRatePerM2 { get; set; }
        public decimal SubtotalExVat { get; set; }
        public decimal VatAmount { get; set; }
        public decimal TotalIncVat { get; set; }
        public int EstimatedWeeks { get; set; }

        public decimal SubstructureAmount { get; set; }
        public decimal SuperstructureAmount { get; set; }
        public decimal RoofingAmount { get; set; }
        public decimal ServicesAmount { get; set; }
        public decimal FinishesAmount { get; set; }
        public decimal PrelimsAndContingencyAmount { get; set; }

        public decimal Milestone1Deposit { get; set; }
        public decimal Milestone2RoofHeight { get; set; }
        public decimal Milestone3PracticalCompletion { get; set; }
        public decimal Milestone4Retention { get; set; }

        public string FormattedReport { get; set; } = string.Empty;
    }

    /// <summary>
    /// Professional construction cost estimator based on standard South African Quantity Surveying rates (ZAR).
    /// </summary>
    public static class ConstructionCostEstimator
    {
        public static CostEstimateResult GenerateEstimate(string buildingType, decimal areaM2, string? location = null, string? quality = null)
        {
            var bType = string.IsNullOrWhiteSpace(buildingType) ? "Residential" : buildingType.Trim();
            var loc = string.IsNullOrWhiteSpace(location) ? "Gauteng" : location.Trim();
            var grade = string.IsNullOrWhiteSpace(quality) ? "Standard" : quality.Trim();
            var area = Math.Max(10m, areaM2);

            // Base rate per m² (ZAR) according to standard SA commercial & residential building indices
            decimal baseRate = bType.ToLowerInvariant() switch
            {
                var t when t.Contains("rdp") || t.Contains("social") => 6500m,
                var t when t.Contains("clinic") || t.Contains("health") => 13500m,
                var t when t.Contains("school") || t.Contains("education") => 11800m,
                var t when t.Contains("industrial") || t.Contains("warehouse") => 9200m,
                var t when t.Contains("civil") || t.Contains("paving") || t.Contains("road") => 4200m,
                var t when t.Contains("luxury") || t.Contains("high-end") => 18500m,
                var t when t.Contains("commercial") || t.Contains("office") => 15000m,
                _ => 11500m // Standard suburban residential home
            };

            // Location adjustment factor (logistics & regional supply chain)
            decimal locFactor = loc.ToLowerInvariant() switch
            {
                var l when l.Contains("cape") || l.Contains("coastal") => 1.08m,
                var l when l.Contains("durban") || l.Contains("kzn") => 1.04m,
                var l when l.Contains("rural") || l.Contains("remote") => 1.12m,
                _ => 1.0m // Gauteng / inland baseline
            };

            // Finish grade factor
            decimal gradeFactor = grade.ToLowerInvariant() switch
            {
                "premium" or "luxury" => 1.30m,
                "medium" or "upmarket" => 1.15m,
                _ => 1.0m
            };

            decimal adjustedRate = Math.Round(baseRate * locFactor * gradeFactor, 2);
            decimal subtotal = Math.Round(adjustedRate * area, 2);
            decimal vat = Math.Round(subtotal * 0.15m, 2);
            decimal total = subtotal + vat;

            // Trade breakdown percentages based on ASAQS (Association of South African Quantity Surveyors)
            decimal substructure = Math.Round(subtotal * 0.18m, 2);
            decimal superstructure = Math.Round(subtotal * 0.25m, 2);
            decimal roofing = Math.Round(subtotal * 0.15m, 2);
            decimal services = Math.Round(subtotal * 0.17m, 2);
            decimal finishes = Math.Round(subtotal * 0.17m, 2);
            decimal prelimsContingency = Math.Round(subtotal * 0.08m, 2);

            // 4-Stage Milestone Schedule
            decimal m1 = Math.Round(subtotal * 0.30m, 2); // 30% Deposit & Site Establishment
            decimal m2 = Math.Round(subtotal * 0.40m, 2); // 40% Roof Height / Plate Level
            decimal m3 = Math.Round(subtotal * 0.25m, 2); // 25% Practical Completion
            decimal m4 = Math.Round(subtotal * 0.05m, 2); // 5% Final Retention

            // Estimated timeline in weeks
            int weeks = Math.Max(4, (int)Math.Ceiling((double)area / 18.0) + 4);

            var sb = new StringBuilder();
            sb.AppendLine($"### 🏗️ UbuhleBethu Indicative Cost Estimate");
            sb.AppendLine($"**Project Scope**: {area:N0} m² {grade} {bType} in {loc}");
            sb.AppendLine($"**Estimated Construction Duration**: ~{weeks} weeks ({Math.Round(weeks / 4.3, 1)} months)");
            sb.AppendLine();
            sb.AppendLine($"| Cost Element | Rate / Percentage | Amount (ZAR) |");
            sb.AppendLine($"| :--- | :--- | :--- |");
            sb.AppendLine($"| **Base Rate / m²** | Adjusted for {loc} ({grade}) | R{adjustedRate:N2} / m² |");
            sb.AppendLine($"| **1. Substructure & Foundations** | 18% of scope | R{substructure:N2} |");
            sb.AppendLine($"| **2. Superstructure & Brickwork** | 25% of scope | R{superstructure:N2} |");
            sb.AppendLine($"| **3. Roof Trusses, Tiles & Waterproofing** | 15% of scope | R{roofing:N2} |");
            sb.AppendLine($"| **4. Plumbing, Electrical & HVAC** | 17% of scope | R{services:N2} |");
            sb.AppendLine($"| **5. Plaster, Paint, Tiles & Joinery** | 17% of scope | R{finishes:N2} |");
            sb.AppendLine($"| **6. Prelims, NHBRC Levy & Contingency** | 8% allowance | R{prelimsContingency:N2} |");
            sb.AppendLine($"| **Subtotal (Excl. VAT)** | **100% Total Cost** | **R{subtotal:N2}** |");
            sb.AppendLine($"| **VAT (15%)** | South African Statutory VAT | R{vat:N2} |");
            sb.AppendLine($"| **Total Estimated Investment (Incl. VAT)** | **Grand Total** | **R{total:N2}** |");
            sb.AppendLine();
            sb.AppendLine($"#### 💳 Standard 4-Stage Milestone Billing Schedule");
            sb.AppendLine($"- **Milestone 1: Deposit & Site Establishment (30%)**: R{m1:N2} (payable before mobilization)");
            sb.AppendLine($"- **Milestone 2: Wall Plate & Roof Trusses (40%)**: R{m2:N2} (unlocked at 40% site progress)");
            sb.AppendLine($"- **Milestone 3: Practical Completion & Handover (25%)**: R{m3:N2} (unlocked at 90% progress)");
            sb.AppendLine($"- **Milestone 4: Retention (5%)**: R{m4:N2} (released after 90-day defect liability period)");
            sb.AppendLine();
            sb.AppendLine($"> [!NOTE]");
            sb.AppendLine($"> *This estimate is generated in accordance with UbuhleBethu ConnectPro QS standards. Final tender pricing is subject to structural engineer soil tests and site inspection.*");

            return new CostEstimateResult
            {
                BuildingType = bType,
                AreaM2 = area,
                Location = loc,
                FinishGrade = grade,
                BaseRatePerM2 = baseRate,
                AdjustedRatePerM2 = adjustedRate,
                SubtotalExVat = subtotal,
                VatAmount = vat,
                TotalIncVat = total,
                EstimatedWeeks = weeks,
                SubstructureAmount = substructure,
                SuperstructureAmount = superstructure,
                RoofingAmount = roofing,
                ServicesAmount = services,
                FinishesAmount = finishes,
                PrelimsAndContingencyAmount = prelimsContingency,
                Milestone1Deposit = m1,
                Milestone2RoofHeight = m2,
                Milestone3PracticalCompletion = m3,
                Milestone4Retention = m4,
                FormattedReport = sb.ToString()
            };
        }
    }
}
