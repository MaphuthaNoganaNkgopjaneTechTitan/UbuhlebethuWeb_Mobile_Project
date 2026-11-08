using Microsoft.AspNetCore.Identity;

namespace UbuhlebethuConnectPro.Core.Models
{
    public class ApplicationUser : IdentityUser
    {
        public bool IsDeleted { get; set; } = false;
        public decimal? CreditLimit { get; set; }
        public string? SkillSet { get; set; }
        public bool CanApproveBudget { get; set; }
        public bool IsSuperAdmin { get; set; }
        public string? AssignedRegion { get; set; }
        public bool CanApproveQuotes { get; set; }
    }
}
