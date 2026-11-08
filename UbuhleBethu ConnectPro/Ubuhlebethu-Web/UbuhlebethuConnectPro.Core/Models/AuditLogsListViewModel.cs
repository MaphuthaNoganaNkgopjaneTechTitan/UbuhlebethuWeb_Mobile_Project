using System.Collections.Generic;

namespace UbuhlebethuConnectPro.Core.Models
{
    public class AuditLogsListViewModel
    {
        public List<AuditLog> Items { get; set; } = new List<AuditLog>();
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalPages { get; set; }
        public string? Query { get; set; }
    }
}
