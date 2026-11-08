using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.Core.Models.DTOs;
using UbuhlebethuConnectPro.WebApp.Data;
using UbuhlebethuConnectPro.Core.Repositories;

namespace UbuhlebethuConnectPro.Api.Controllers.Api
{
    [ApiController]
    [IgnoreAntiforgeryToken]
    [Route("api/v1/dashboard")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    public class DashboardApiController : ControllerBase
    {
        private readonly UbuhlebethuConnectPro.WebApp.Data.ApplicationDbContext _dbContext;

        public DashboardApiController(UbuhlebethuConnectPro.WebApp.Data.ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // GET: api/dashboard/stats
        [HttpGet("stats")]
        [HttpGet("dashboard")]
        [HttpGet("/api/manager/dashboard")]
        public async Task<ActionResult<ManagerDashboardViewModel>> GetDashboardStats()
        {
            var totalProjects = await _dbContext.Projects.CountAsync();
            var totalValue = await _dbContext.Projects.SumAsync(p => p.EstimatedCost);
            var totalInvoiced = await _dbContext.Invoices.SumAsync(i => i.Amount);
            var totalPaid = await _dbContext.Invoices.Where(i => i.IsPaid).SumAsync(i => i.Amount);
            
            var recentInvoices = await _dbContext.Invoices
                .OrderByDescending(i => i.CreatedAt)
                .Take(10)
                .Select(i => new InvoiceViewModel
                {
                    Id = i.Id,
                    Amount = i.Amount,
                    IsPaid = i.IsPaid,
                    QuoteRequestId = i.QuoteRequestId,
                    CreatedAt = i.CreatedAt
                })
                .ToListAsync();

            return Ok(new ManagerDashboardViewModel
            {
                TotalProjects = totalProjects,
                TotalValue = totalValue,
                TotalInvoiced = totalInvoiced,
                TotalPaid = totalPaid,
                RecentInvoices = recentInvoices
            });
        }

        // GET: api/dashboard/jobs
        [HttpGet("jobs")]
        public async Task<ActionResult<List<Job>>> GetJobs([FromServices] UbuhlebethuConnectPro.Core.Repositories.IProjectRepository projectRepo)
        {
            var jobs = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                projectRepo.Query().Select(p => new Job
                {
                    Id = p.Id,
                    JobNumber = p.QuoteReference,
                    Description = p.Title,
                    BuildingType = p.ProjectType,
                    ClientName = p.ClientName,
                    ClientUserName = p.ClientUserName,
                    Location = p.Location,
                    Progress = p.ProgressPercent,
                    Variance = p.VariancePercent,
                    EstimatedCost = p.EstimatedCost,
                    InvoicedAmount = p.InvoicedToDate,
                    SiteManager = p.SiteManagerName,
                    NhbrcCompliant = p.NhbrcCompliant,
                    Status = p.Status,
                    Notes = p.Notes
                })
            );
            return Ok(jobs);
        }

        [HttpPost("jobs/{id}")]
        [Authorize(Roles = "Admin,SystemAdmin,Office,Manager,Management,Executive,SiteManager")]
        public async Task<IActionResult> UpdateJob(string id, [FromBody] Job updatedJob)
        {
            if (!Guid.TryParse(id, out var projectGuid))
            {
                return BadRequest(new { error = "Invalid job/project ID format." });
            }

            var project = await _dbContext.Projects.FindAsync(projectGuid);
            if (project == null)
            {
                return NotFound();
            }

            if (updatedJob.Notes != null) project.Notes = updatedJob.Notes.Length > 4000 ? updatedJob.Notes[..4000] : updatedJob.Notes;
            if (!string.IsNullOrWhiteSpace(updatedJob.BuildingType)) project.ProjectType = updatedJob.BuildingType;
            if (!string.IsNullOrEmpty(updatedJob.Description)) project.Title = updatedJob.Description;
            if (!string.IsNullOrEmpty(updatedJob.Location)) project.Location = updatedJob.Location;
            if (!string.IsNullOrEmpty(updatedJob.ClientName)) project.ClientName = updatedJob.ClientName;
            if (!string.IsNullOrEmpty(updatedJob.Status)) project.Status = updatedJob.Status;
            if (!string.IsNullOrEmpty(updatedJob.SiteManager)) project.SiteManagerName = updatedJob.SiteManager;
            if (updatedJob.Progress >= 0) project.ProgressPercent = updatedJob.Progress;
            project.VariancePercent = updatedJob.Variance;
            if (updatedJob.EstimatedCost > 0) project.EstimatedCost = updatedJob.EstimatedCost;
            if (updatedJob.InvoicedAmount >= 0) project.InvoicedToDate = updatedJob.InvoicedAmount;
            project.NhbrcCompliant = updatedJob.NhbrcCompliant;

            await _dbContext.SaveChangesAsync();

            var job = new Job
            {
                Id = project.Id,
                JobNumber = project.QuoteReference,
                Description = project.Title,
                BuildingType = project.ProjectType,
                ClientName = project.ClientName,
                Location = project.Location,
                Progress = project.ProgressPercent,
                Variance = project.VariancePercent,
                EstimatedCost = project.EstimatedCost,
                InvoicedAmount = project.InvoicedToDate,
                SiteManager = project.SiteManagerName,
                NhbrcCompliant = project.NhbrcCompliant,
                Status = project.Status,
                Notes = project.Notes
            };

            return Ok(job);
        }

        public class MilestonePaymentRequest
        {
            public int? Milestone { get; set; }
        }

        [HttpPost("jobs/{id}/milestone-payment")]
        [Authorize(Roles = "Client,Admin,SystemAdmin,Office,Manager,Management,Executive")]
        public async Task<IActionResult> PayJobMilestone(string id, [FromBody] MilestonePaymentRequest? req)
        {
            if (!Guid.TryParse(id, out var projectGuid))
            {
                return BadRequest(new { error = "Invalid job ID." });
            }

            var project = await _dbContext.Projects.FindAsync(projectGuid);
            if (project == null) return NotFound(new { error = "Project not found." });

            var currentUserName = User.Identity?.Name ?? string.Empty;
            bool isStaff = User.IsInRole("Admin") || User.IsInRole("SystemAdmin") || User.IsInRole("Office") || User.IsInRole("Manager") || User.IsInRole("Management") || User.IsInRole("Executive");
            bool isOwner = (!string.IsNullOrEmpty(project.ClientUserName) && string.Equals(project.ClientUserName, currentUserName, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrEmpty(project.ClientName) && string.Equals(project.ClientName, currentUserName, StringComparison.OrdinalIgnoreCase));

            if (!isStaff && !isOwner)
            {
                return Forbid();
            }

            decimal totalBudget = project.EstimatedCost;
            if (totalBudget <= 0)
            {
                return BadRequest(new { error = "Project has invalid budget." });
            }

            decimal m1Amount = Math.Round(totalBudget * 0.25m, 2);
            decimal m2Amount = Math.Round(totalBudget * 0.50m, 2);
            decimal m3Amount = Math.Round(totalBudget - m1Amount - m2Amount, 2);

            int requestedMilestone = req?.Milestone ?? 0;
            if (requestedMilestone <= 0)
            {
                if (project.InvoicedToDate < m1Amount)
                {
                    requestedMilestone = 1;
                }
                else if (project.InvoicedToDate < m1Amount + m2Amount)
                {
                    if (project.ProgressPercent < 40)
                    {
                        return BadRequest(new { error = $"Milestone 2 requires site progress of at least 40%. Current progress is {project.ProgressPercent}%." });
                    }
                    requestedMilestone = 2;
                }
                else if (project.InvoicedToDate < totalBudget)
                {
                    if (project.ProgressPercent < 90)
                    {
                        return BadRequest(new { error = $"Milestone 3 requires site progress of at least 90%. Current progress is {project.ProgressPercent}%." });
                    }
                    requestedMilestone = 3;
                }
                else
                {
                    return BadRequest(new { error = "All milestone payments for this project have already been settled." });
                }
            }

            decimal payAmount = 0m;
            string milestoneName = string.Empty;

            switch (requestedMilestone)
            {
                case 1:
                    milestoneName = "Milestone 1: Deposit (25%)";
                    if (project.InvoicedToDate >= m1Amount)
                    {
                        return BadRequest(new { error = "Milestone 1 (Deposit 25%) has already been invoiced and paid." });
                    }
                    payAmount = m1Amount - project.InvoicedToDate;
                    project.InvoicedToDate = m1Amount;
                    if (project.Status == "Planning" || project.Status == "Draft" || string.IsNullOrEmpty(project.Status))
                    {
                        project.Status = "In Progress";
                    }
                    break;

                case 2:
                    milestoneName = "Milestone 2: Progress (50%)";
                    if (project.InvoicedToDate < m1Amount)
                    {
                        return BadRequest(new { error = "Milestone 1 (Deposit 25%) must be paid before Milestone 2 can be billed." });
                    }
                    if (project.ProgressPercent < 40)
                    {
                        return BadRequest(new { error = $"Milestone 2 requires site progress of at least 40%. Current progress is {project.ProgressPercent}%." });
                    }
                    if (project.InvoicedToDate >= m1Amount + m2Amount)
                    {
                        return BadRequest(new { error = "Milestone 2 (Progress 50%) has already been invoiced and paid." });
                    }
                    payAmount = (m1Amount + m2Amount) - project.InvoicedToDate;
                    project.InvoicedToDate = m1Amount + m2Amount;
                    break;

                case 3:
                    milestoneName = "Milestone 3: Practical Completion / Handover (25%)";
                    if (project.InvoicedToDate < m1Amount + m2Amount)
                    {
                        return BadRequest(new { error = "Milestone 2 (Progress 50%) must be paid before Milestone 3 can be billed." });
                    }
                    if (project.ProgressPercent < 90)
                    {
                        return BadRequest(new { error = $"Milestone 3 requires site progress of at least 90%. Current progress is {project.ProgressPercent}%." });
                    }
                    if (project.InvoicedToDate >= totalBudget)
                    {
                        return BadRequest(new { error = "Milestone 3 (Practical Completion 25%) has already been invoiced and paid." });
                    }
                    payAmount = totalBudget - project.InvoicedToDate;
                    project.InvoicedToDate = totalBudget;
                    if (project.ProgressPercent >= 100)
                    {
                        project.Status = "Completed";
                    }
                    break;

                default:
                    return BadRequest(new { error = "Invalid milestone number. Must be 1, 2, or 3." });
            }

            try
            {
                await UbuhlebethuConnectPro.Api.Data.ProjectActivityStore.AddAsync(
                    _dbContext,
                    project.Id,
                    "Milestone",
                    $"{milestoneName} Settled",
                    $"Payment simulation of ZAR {payAmount:N2} processed for {milestoneName}. Total invoiced to date: ZAR {project.InvoicedToDate:N2} of ZAR {project.EstimatedCost:N2}.",
                    payAmount.ToString("F2"),
                    DateTime.UtcNow.ToString("yyyy-MM-dd"),
                    "Complete",
                    null, null, null,
                    currentUserName
                );
            }
            catch { /* Ignore activity logging if table unavailable */ }

            if (project.QuoteRequestId.HasValue)
            {
                var quote = await _dbContext.QuoteRequests.FindAsync(project.QuoteRequestId.Value);
                if (quote != null)
                {
                    if (project.InvoicedToDate >= totalBudget)
                    {
                        quote.IsPaid = true;
                        quote.Status = "Paid";
                    }
                    else
                    {
                        quote.Status = "Invoiced";
                    }

                    if (quote.InvoiceId.HasValue)
                    {
                        var inv = await _dbContext.Invoices.FindAsync(quote.InvoiceId.Value);
                        if (inv != null)
                        {
                            inv.PaidAmount = project.InvoicedToDate;
                            if (project.InvoicedToDate >= totalBudget)
                            {
                                inv.IsPaid = true;
                                inv.Status = InvoiceStatus.Paid;
                            }
                            else
                            {
                                inv.Status = InvoiceStatus.PartiallyPaid;
                            }
                        }
                    }
                }
            }

            _dbContext.AuditLogs.Add(new AuditLog
            {
                ActionType = "MilestonePaymentSimulation",
                PerformedBy = currentUserName,
                TargetUserId = project.ClientUserName,
                Reason = $"{milestoneName} payment simulation",
                Details = $"Simulated payment of ZAR {payAmount:N2} for Project {project.QuoteReference} ({project.Title}). Invoiced to date: ZAR {project.InvoicedToDate:N2}.",
                Timestamp = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync();

            var updatedJob = new Job
            {
                Id = project.Id,
                JobNumber = project.QuoteReference,
                Description = project.Title,
                BuildingType = project.ProjectType,
                ClientName = project.ClientName,
                ClientUserName = project.ClientUserName,
                Location = project.Location,
                Progress = project.ProgressPercent,
                Variance = project.VariancePercent,
                EstimatedCost = project.EstimatedCost,
                InvoicedAmount = project.InvoicedToDate,
                SiteManager = project.SiteManagerName,
                NhbrcCompliant = project.NhbrcCompliant,
                Status = project.Status,
                Notes = project.Notes
            };

            return Ok(new
            {
                ok = true,
                message = $"{milestoneName} payment of ZAR {payAmount:N2} simulated successfully.",
                milestone = requestedMilestone,
                amountPaid = payAmount,
                totalInvoiced = project.InvoicedToDate,
                job = updatedJob
            });
        }

        public class DeleteProjectsRequest
        {
            public List<Guid> ProjectIds { get; set; } = new List<Guid>();
            public string? Reason { get; set; }
        }

        [HttpPost("jobs/delete")]
        [Authorize(Roles = "Manager,Management")]
        public async Task<IActionResult> DeleteProjects([FromBody] DeleteProjectsRequest request)
        {
            if (request?.ProjectIds == null || !request.ProjectIds.Any())
            {
                return BadRequest(new { error = "No project IDs provided." });
            }

            var projects = await _dbContext.Projects
                .Where(p => request.ProjectIds.Contains(p.Id))
                .ToListAsync();

            if (!projects.Any())
            {
                return NotFound(new { error = "No matching projects found." });
            }

            var currentManager = User.Identity?.Name ?? "System";

            foreach (var p in projects)
            {
                p.IsDeleted = true;

                var tasks = await _dbContext.MaterialTasks.Where(t => t.ProjectId == p.Id).ToListAsync();
                foreach (var t in tasks) t.IsDeleted = true;

                var reports = await _dbContext.SiteReports.Where(r => r.ProjectId == p.Id).ToListAsync();
                foreach (var r in reports) r.IsDeleted = true;

                try
                {
                    var pid = p.Id.ToString();
                    await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM \"ProjectActivities\" WHERE \"ProjectId\" = {0}", pid);
                }
                catch { /* Ignore if table doesn't exist yet */ }
            }

            _dbContext.AuditLogs.Add(new AuditLog
            {
                ActionType = "DeleteProjects",
                PerformedBy = currentManager,
                TargetUserId = string.Empty,
                Reason = string.IsNullOrWhiteSpace(request?.Reason) ? "Manager project deletion" : request.Reason,
                Details = $"Deleted {projects.Count} project(s): {string.Join(", ", projects.Select(p => p.Title + " [" + p.QuoteReference + "]"))}.",
                Timestamp = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync();

            return Ok(new { message = $"Successfully deleted {projects.Count} project(s)." });
        }

        [HttpDelete("jobs/{id}")]
        [Authorize(Roles = "Manager,Management")]
        public async Task<IActionResult> DeleteSingleJob(string id)
        {
            if (!Guid.TryParse(id, out var projectGuid))
            {
                return BadRequest(new { error = "Invalid project ID format." });
            }

            return await DeleteProjects(new DeleteProjectsRequest
            {
                ProjectIds = new List<Guid> { projectGuid },
                Reason = "Single project deletion by manager"
            });
        }

        [HttpPost("jobs/{id}/delete")]
        [Authorize(Roles = "Manager,Management")]
        public async Task<IActionResult> PostDeleteSingleJob(string id)
        {
            return await DeleteSingleJob(id);
        }

        // ---- project activities (daily logs, milestones, purchase orders, documents, client contact) ----

        public class ActivityRequest
        {
            public string Kind { get; set; } = "";
            public string Title { get; set; } = "";
            public string? Body { get; set; }
            public string? Amount { get; set; }
            public string? DueDate { get; set; }
            public string? Status { get; set; }
            public string? FileName { get; set; }
            public string? ContentType { get; set; }
            public string? ContentBase64 { get; set; }
        }

        [HttpGet("jobs/{id}/activities")]
        public async Task<IActionResult> GetActivities(string id, CancellationToken ct)
        {
            if (!Guid.TryParse(id, out var pid)) return BadRequest(new { error = "Invalid job ID." });
            return Ok(await UbuhlebethuConnectPro.Api.Data.ProjectActivityStore.ListAsync(_dbContext, pid, ct));
        }

        [HttpPost("jobs/{id}/activities")]
        [Authorize(Roles = "Admin,SystemAdmin,Office,Manager,Management,Executive,SiteManager")]
        public async Task<IActionResult> AddActivity(string id, [FromBody] ActivityRequest req)
        {
            if (!Guid.TryParse(id, out var pid)) return BadRequest(new { error = "Invalid job ID." });
            if (req == null || !UbuhlebethuConnectPro.Api.Data.ProjectActivityStore.Kinds.Contains(req.Kind))
                return BadRequest(new { error = "Unknown activity type." });
            var title = (req.Title ?? "").Trim();
            if (title.Length == 0) return BadRequest(new { error = "A title is required." });
            if (title.Length > 200) return BadRequest(new { error = "Title must be 200 characters or fewer." });
            if ((req.Body?.Length ?? 0) > 4000) return BadRequest(new { error = "Text must be 4000 characters or fewer." });
            if ((req.ContentBase64?.Length ?? 0) > UbuhlebethuConnectPro.Api.Data.ProjectActivityStore.MaxFileChars)
                return BadRequest(new { error = "File is too large (limit 2 MB)." });
            if (req.Amount != null && !decimal.TryParse(req.Amount, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var amt))
                return BadRequest(new { error = "Amount must be a number." });
            if (await _dbContext.Projects.FindAsync(pid) == null) return NotFound();

            var row = await UbuhlebethuConnectPro.Api.Data.ProjectActivityStore.AddAsync(_dbContext, pid, req.Kind, title, req.Body?.Trim(),
                req.Amount, req.DueDate, req.Status, req.FileName, req.ContentType, req.ContentBase64, User.Identity?.Name);
            return Ok(row);
        }

        public class StatusRequest { public string Status { get; set; } = ""; }

        [HttpPost("jobs/{id}/activities/{activityId}/status")]
        [Authorize(Roles = "Admin,SystemAdmin,Office,Manager,Management,Executive,SiteManager")]
        public async Task<IActionResult> SetActivityStatus(string id, string activityId, [FromBody] StatusRequest req)
        {
            if (!Guid.TryParse(id, out var pid)) return BadRequest(new { error = "Invalid job ID." });
            var rows = await UbuhlebethuConnectPro.Api.Data.ProjectActivityStore.ListAsync(_dbContext, pid);
            var row = rows.FirstOrDefault(r => r.Id == activityId);
            if (row == null) return NotFound();
            var allowed = row.Kind switch
            {
                "Milestone" => new[] { "Scheduled", "In Progress", "Complete" },
                "PurchaseOrder" => new[] { "Pending Sign-off", "Approved", "Delivered" },
                _ => Array.Empty<string>()
            };
            if (!allowed.Contains(req?.Status)) return BadRequest(new { error = "That status is not allowed for this item." });
            await UbuhlebethuConnectPro.Api.Data.ProjectActivityStore.UpdateStatusAsync(_dbContext, pid, activityId, row.Kind, req!.Status);
            return Ok(new { ok = true });
        }

        [HttpGet("activities/{activityId}/file")]
        public async Task<IActionResult> GetActivityFile(string activityId, CancellationToken ct)
        {
            var (row, content) = await UbuhlebethuConnectPro.Api.Data.ProjectActivityStore.GetFileAsync(_dbContext, activityId, ct);
            if (row == null || string.IsNullOrEmpty(content)) return NotFound();
            return Ok(new { projectId = row.ProjectId, fileName = row.FileName ?? row.Title, contentType = row.ContentType ?? "application/octet-stream", contentBase64 = content });
        }
    }
}
