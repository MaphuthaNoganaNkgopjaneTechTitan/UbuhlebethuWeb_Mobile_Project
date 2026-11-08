using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.WebApp.Data;

namespace UbuhlebethuConnectPro.Api.Controllers.Api
{
    [ApiController]
    [IgnoreAntiforgeryToken]
    [Route("api/v1/audit")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    public class AuditApiController : ControllerBase
    {
        private readonly UbuhlebethuConnectPro.WebApp.Data.ApplicationDbContext _dbContext;

        public AuditApiController(UbuhlebethuConnectPro.WebApp.Data.ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public class AuditLogDto
        {
            public Guid Id { get; set; }
            public string ActionType { get; set; } = string.Empty;
            public string PerformedBy { get; set; } = string.Empty;
            public string TargetUserId { get; set; } = string.Empty;
            public string? Reason { get; set; }
            public string? Details { get; set; }
            public DateTime Timestamp { get; set; }
        }

        public class AuditLogsListDto
        {
            public IList<AuditLogDto> Items { get; set; } = new List<AuditLogDto>();
            public int Page { get; set; }
            public int PageSize { get; set; }
            public int TotalPages { get; set; }
            public int TotalCount { get; set; }
            public string? Query { get; set; }
        }

        // GET: api/audit
        [HttpGet("")]
        public async Task<ActionResult<AuditLogsListDto>> GetAuditLogs([FromQuery] string? q = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            const int defaultPageSize = 20;
            if (pageSize <= 0) pageSize = defaultPageSize;
            
            var query = _dbContext.AuditLogs.AsQueryable();
            
            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                query = query.Where(a => 
                    a.TargetUserId.Contains(q) || 
                    a.ActionType.Contains(q) || 
                    a.PerformedBy.Contains(q) || 
                    (a.Reason != null && a.Reason.Contains(q)) || 
                    (a.Details != null && a.Details.Contains(q)));
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            
            var items = await query
                .OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AuditLogDto
                {
                    Id = a.Id,
                    ActionType = a.ActionType,
                    PerformedBy = a.PerformedBy,
                    TargetUserId = a.TargetUserId,
                    Reason = a.Reason,
                    Details = a.Details,
                    Timestamp = a.Timestamp
                })
                .ToListAsync();

            return Ok(new AuditLogsListDto
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages,
                TotalCount = totalCount,
                Query = q
            });
        }

        // GET: api/audit/export
        [HttpGet("export")]
        public async Task<IActionResult> ExportAuditLogs([FromQuery] string? q = null)
        {
            var query = _dbContext.AuditLogs.AsQueryable();
            
            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                query = query.Where(a => 
                    a.TargetUserId.Contains(q) || 
                    a.ActionType.Contains(q) || 
                    a.PerformedBy.Contains(q) || 
                    (a.Reason != null && a.Reason.Contains(q)) || 
                    (a.Details != null && a.Details.Contains(q)));
            }

            var logs = await query.OrderByDescending(a => a.Timestamp).ToListAsync();
            
            var sb = new StringBuilder();
            sb.AppendLine("Timestamp,ActionType,PerformedBy,TargetUserId,Reason,Details");
            foreach (var log in logs)
            {
                var ts = log.Timestamp.ToString("o"); // ISO 8601 format
                sb.AppendLine($"\"{ts}\",\"{log.ActionType}\",\"{log.PerformedBy}\",\"{log.TargetUserId}\",\"{(log.Reason ?? string.Empty)}\",\"{(log.Details ?? string.Empty)}\"");
            }
            
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = string.IsNullOrWhiteSpace(q) ? "auditlogs.csv" : $"auditlogs_filtered_{System.Net.WebUtility.UrlEncode(q)}.csv";
            return File(bytes, "text/csv", fileName);
        }
        // GET: api/audit/user/{userId}
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<AuditLogsListDto>> GetAuditLogsByUserId(string userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            const int defaultPageSize = 20;
            if (pageSize <= 0) pageSize = defaultPageSize;
            
            var query = _dbContext.AuditLogs.Where(a => a.TargetUserId == userId || a.PerformedBy == userId);
            
            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            
            var items = await query
                .OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AuditLogDto
                {
                    Id = a.Id,
                    ActionType = a.ActionType,
                    PerformedBy = a.PerformedBy,
                    TargetUserId = a.TargetUserId,
                    Reason = a.Reason,
                    Details = a.Details,
                    Timestamp = a.Timestamp
                })
                .ToListAsync();

            return Ok(new AuditLogsListDto
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages,
                TotalCount = totalCount,
                Query = null
            });
        }
    }
}
