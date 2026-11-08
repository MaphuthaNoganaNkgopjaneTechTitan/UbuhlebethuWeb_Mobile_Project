using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace UbuhlebethuConnectPro.WebApp.Models.DTOs
{
    public class LoginViewModel
    {
        [Required]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }

    public class InvoiceViewModel : UbuhlebethuConnectPro.Core.Models.DTOs.InvoiceViewModel {}

    public class ManagerDashboardViewModel : UbuhlebethuConnectPro.Core.Models.DTOs.ManagerDashboardViewModel {}

    public class ChatRequest
    {
        public string Question { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public List<UbuhlebethuConnectPro.WebApp.ApiClient.ConversationMessageDto>? History { get; set; }
        public Guid? ProjectId { get; set; }
    }

    public class SiteReportForm
    {
        [Required] public Guid ProjectId { get; set; }
        [Required, StringLength(2000)] public string Description { get; set; } = string.Empty;
        [Range(0, 100)] public int ProgressPercent { get; set; }
        [Range(-90, 90)] public double? Latitude { get; set; }
        [Range(-180, 180)] public double? Longitude { get; set; }
        [StringLength(50)] public string? Stage { get; set; }
        public IFormFile? Photo { get; set; }
    }
}
