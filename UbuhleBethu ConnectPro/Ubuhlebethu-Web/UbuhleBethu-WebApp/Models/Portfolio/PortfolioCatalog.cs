namespace UbuhlebethuConnectPro.WebApp.Models.Portfolio
{
    /// <summary>One completed building / project shown on the public website.</summary>
    public sealed record PortfolioProject(
        string Slug,
        string Name,
        string Category,        // matches the sections in the company profile
        string ProjectType,     // value stored on the quotation (drives the estimator)
        string? Institution,
        string? ContractSum,    // as printed in the 2020 company profile
        string? Period,
        string? Scope,
        bool HasContractDetails)
    {
        public string ImageUrl => $"/images/portfolio/{Slug}.jpg";

        /// <summary>All photos of this project, shown in the "View photos" viewer.</summary>
        public IReadOnlyList<string> Gallery => PortfolioCatalog.GalleryFor(Slug, ImageUrl);
    }

    /// <summary>
    /// Source: "Ubuhlebethu Business Enterprise cc – Profile (2020)". The first 14 entries come from the
    /// "Projects" table (status: Completed). The remaining 10 are showcase projects named in the profile's
    /// photo captions (no contract data was published for them).
    /// Contract sums are reproduced exactly as printed; a few are malformed in the source PDF
    /// (e.g. the stormwater one) – please confirm with the company before go-live.
    /// </summary>
    public static class PortfolioCatalog
    {
        public const string Building = "Building";
        public const string Civil = "Civil & Structural";
        public const string Housing = "Housing";
        public const string Property = "Property Development";
        public const string Demolition = "Demolition";

        public static readonly IReadOnlyList<string> Categories =
            new[] { Building, Civil, Housing, Property, Demolition };

        private const string TBuilding = "Building (Schools / Clinics / Commercial)";
        private const string TCivil = "Civil & Structural";
        private const string THousing = "Housing (RDP / Social)";
        private const string TProperty = "Property Development";
        private const string TDemolition = "Demolition";

        public static string ProjectTypeFor(string category) => category switch
        {
            Building => TBuilding,
            Civil => TCivil,
            Housing => THousing,
            Property => TProperty,
            Demolition => TDemolition,
            _ => TBuilding
        };

        public static readonly IReadOnlyList<string> ProjectTypes =
            new[] { TBuilding, TCivil, THousing, TProperty, TDemolition };

        public static readonly IReadOnlyList<PortfolioProject> All = new List<PortfolioProject>
        {
            new("hoek-street-conversion", "Conversion & Refurbishment of 27 Hoek Street, Johannesburg", Building, TBuilding,
                "Joshco", "R77 623 454", "18 months", "Residential Building", true),
            new("greenspark-clinic", "Greenspark Clinic – Construction and Design", Building, TBuilding,
                "Department of Infrastructure Development", "R70 102 055.42", "18 months", "Building, Civil and Electrical", true),
            new("tweefontein-g-health-centre", "New Tweefontein G Community Health Centre, Thembisile Municipality", Building, TBuilding,
                "IDT", "R21 719 891.27", "18 months", "Building Work, Civil, Electrical & Mechanical", true),
            new("shosholoza-meyl-refurb", "Building Refurbishment for Shosholoza Meyl Junction", Building, TBuilding,
                "Prasa", "R17 643 909.96", "12 months", "Building Work, Mechanical, Electrical & Demolition", true),
            new("mareleng-primary", "Outstanding Works at Mareleng Primary School, Siyabuswa", Building, TBuilding,
                "Dept. of Public Works, Roads and Transport", "R3 617 017.70", "3 months", "Building Work", true),
            new("molapalama-combined", "Outstanding Works at Molapalama Combined School, Senotlelo", Building, TBuilding,
                "Dept. of Public Works, Roads and Transport", "R3 420 455.88", "3 months", "Building Work", true),
            new("alexandra-football-for-hope", "Alexandra Football for Hope Center", Building, TBuilding,
                "Architecture for Humanity", "R2 290 052.26", "5 months", "Building Work", true),
            new("traverners-pavilion", "Upgrade of the Traverners Pavilion at Bidvest Wanderers", Building, TBuilding,
                "Gauteng Cricket Board", "R760 612.56", "6 weeks", null, true),

            new("soshanguve-200-houses", "Construction of 200 Houses in Soshanguve Block TT", Housing, THousing,
                "Department of Human Settlement", "R20 590 710.00", "12 months", "RDP Houses", true),

            new("maluti-a-phofung-roads", "Internal Roads at Maluti-A-Phofung", Civil, TCivil,
                "Babusisekile Bus Enterprise", "R22 832 751.02", "6 months", "Road Works, Sewer, Storm Water and Water Reticulation", true),
            new("mamello-100-stands", "Construction of 100 Stands at Mamello Ext 2", Civil, TCivil,
                "Dept. of Human Settlement", "R12.5m", "12 months", "Sewer, Water Reticulation & Roads", true),
            new("bulk-services-stormwater", "Bulk Services Development – Supply and Laying of Stormwater Pipes", Civil, TCivil,
                "Edwin Construction", "R561 9232.28", "6 weeks", "Storm Water Line", true),

            new("hoek-street-demolition", "Internal Demolition at 27 Hoek Street, Johannesburg", Demolition, TDemolition,
                "Joshco", "R5 178 877.62", "3 months", "Demolition", true),
            new("frederick-street-demolition", "Frederick Street Student Accommodation Demolition", Demolition, TDemolition,
                "Adowa Student Accommodation Fund", "R329 990.17", "2 months", "Demolition", true),

            // Showcase projects named in the profile (no contract data published)
            new("kwaggastroom-station", "Kwaggastroom Railway Station", Building, TBuilding, null, null, null, "Railway station refurbishment", false),
            new("tut-student-accommodation", "Tshwane University of Technology (TUT)", Building, TBuilding, null, null, null, "Residential student accommodation", false),
            new("wilberforce-college", "Wilberforce Community College", Building, TBuilding, null, null, null, "Community college buildings", false),
            new("umtholo-primary", "Umtholo Primary School", Building, TBuilding, null, null, null, "School construction", false),
            new("tshwane-mail-centre", "Tshwane Mail Centre", Building, TBuilding, null, null, null, "Tenant installations", false),
            new("duhva-eskom-stores", "Eskom New Stores at Duhva Power Station", Building, TBuilding, null, null, null, "Stores building and equipment", false),
            new("tshiame-idz", "Tshiame Industrial Development Zone", Civil, TCivil, null, null, null, "Roads and services", false),
            new("kwa-thema-rdp", "Kwa-Thema RDP Housing", Housing, THousing, null, null, null, "RDP housing", false),
            new("blue-hills-estate", "Blue Hills Estate", Property, TProperty, null, null, null, "Residential design", false),
            new("sethokga-hostel-demolition", "Sethokga Hostel Demolition", Demolition, TDemolition, null, null, null, "Building demolition", false),
        };

        // Photos from the 2020 company profile, grouped per project.
        private static readonly Dictionary<string, string[]> Galleries = new(StringComparer.OrdinalIgnoreCase)
        {
            ["hoek-street-conversion"] = new[] { "hoek_1", "hoek_2", "hoek_3" },
            ["blue-hills-estate"] = new[] { "bluehills_1", "bluehills_2", "bluehills_3", "bluehills_4" },
            ["kwaggastroom-station"] = new[] { "kwaggas_b_3", "kwaggas_b_2", "kwaggas_b_1", "kwaggas_3", "kwaggas_2" },
            ["tut-student-accommodation"] = new[] { "tut_3", "tut_2", "tut_1" },
            ["wilberforce-college"] = new[] { "wilberforce_3", "wilberforce_2", "wilberforce_1" },
            ["umtholo-primary"] = new[] { "umtholo_3", "umtholo_2", "umtholo_1" },
            ["tshwane-mail-centre"] = new[] { "tshwane_1", "tshwane_2", "tshwane_3" },
            ["kwa-thema-rdp"] = new[] { "rdp_3", "rdp_2", "rdp_1", "rdp_handover", "rdp_handover_b" },
            ["tshiame-idz"] = new[] { "tshiame_1", "tshiame_2", "tshiame_3" },
            ["sethokga-hostel-demolition"] = new[] { "sethokga_1", "sethokga_2", "sethokga_3" },
        };

        public static IReadOnlyList<string> GalleryFor(string slug, string cover) =>
            Galleries.TryGetValue(slug, out var files)
                ? files.Select(f => $"/images/gallery/{f}.jpg").ToArray()
                : new[] { cover };

        public static PortfolioProject? Find(string? slug) =>
            string.IsNullOrWhiteSpace(slug) ? null
            : All.FirstOrDefault(p => string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase));
    }
}
