using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using UbuhlebethuConnectPro.Core.Repositories;
using UbuhlebethuConnectPro.Core.Services; 
using UbuhlebethuConnectPro.Infrastructure.Services;
using UbuhlebethuConnectPro.Core.Models;

namespace UbuhlebethuConnectPro.Api.Controllers.Api
{
    public class SiteReportForm
    {
        // Optional ID supplied by Android for offline synchronization.
        public Guid? ClientReportId { get; set; }
        [Required] public Guid ProjectId { get; set; }
        [Required, StringLength(2000)] public string Description { get; set; } = string.Empty;
        [Range(0, 100)] public int ProgressPercent { get; set; }
        [Range(-90, 90)] public double? Latitude { get; set; }
        [Range(-180, 180)] public double? Longitude { get; set; }
        [StringLength(50)] public string? Stage { get; set; }
        public IFormFile? Photo { get; set; }
    }

    /// <summary>REST API consumed by mobile workers, managers, and office staff.</summary>
    [ApiController]
    [IgnoreAntiforgeryToken]
    [Route("api/v1")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Client,FieldWorker,MobileWorker,Manager,SiteManager,Admin,SystemAdmin,Management,Office,OfficeStaff,QS,Executive")]
    public class SiteReportsApiController : ControllerBase
    {
        private readonly ISiteReportService _service;
        private readonly ISiteReportRepository _reports;
        private readonly IProjectRepository _projects;
        private readonly UbuhlebethuConnectPro.Core.Services.IStorageService _storageService;
        private readonly Microsoft.AspNetCore.SignalR.IHubContext<UbuhlebethuConnectPro.Api.Hubs.SiteEventsHub> _hubContext;

        private static readonly byte[] PlaceholderSvgBytes = System.Text.Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"600\" height=\"400\" viewBox=\"0 0 600 400\">" +
            "<rect fill=\"#1e293b\" width=\"600\" height=\"400\"/>" +
            "<circle cx=\"300\" cy=\"170\" r=\"48\" fill=\"#334155\"/>" +
            "<text fill=\"#94a3b8\" font-family=\"system-ui,-apple-system,sans-serif\" font-size=\"36\" x=\"300\" y=\"182\" text-anchor=\"middle\">📷</text>" +
            "<text fill=\"#f8fafc\" font-family=\"system-ui,-apple-system,sans-serif\" font-size=\"20\" font-weight=\"600\" x=\"300\" y=\"250\" text-anchor=\"middle\">Photo Unavailable or Syncing</text>" +
            "<text fill=\"#94a3b8\" font-family=\"system-ui,-apple-system,sans-serif\" font-size=\"14\" x=\"300\" y=\"280\" text-anchor=\"middle\">The photo has not yet synced from the mobile worker&apos;s device.</text>" +
            "</svg>");

        public SiteReportsApiController(
            ISiteReportService service,
            ISiteReportRepository reports,
            IProjectRepository projects,
            UbuhlebethuConnectPro.Core.Services.IStorageService storageService,
            Microsoft.AspNetCore.SignalR.IHubContext<UbuhlebethuConnectPro.Api.Hubs.SiteEventsHub> hubContext)
        {
            _service = service;
            _reports = reports;
            _projects = projects;
            _storageService = storageService;
            _hubContext = hubContext;
        }

        [HttpGet("sitereports/project/{projectId:guid}")]
        public async Task<IActionResult> ProjectReports(Guid projectId, CancellationToken ct)
        {
            var project = await _projects.GetByIdAsync(projectId, ct);
            if (project == null) return Ok(Array.Empty<object>());
            if (User.IsInRole("Client") && !string.Equals(project.ClientUserName, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
                return Forbid();

            var reports = await _reports.GetByProjectAsync(projectId, 50, ct);
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            return Ok(reports.Where(r => !r.IsDeleted).Select(r =>
            {
                string? resolvedPhoto = null;
                if (!string.IsNullOrWhiteSpace(r.PhotoPath))
                {
                    if (r.PhotoPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        r.PhotoPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        resolvedPhoto = r.PhotoPath;
                    }
                    else
                    {
                        resolvedPhoto = $"{baseUrl}/api/v1/sitereports/{r.Id}/photo";
                    }
                }

                return new
                {
                    r.Id,
                    r.ProjectId,
                    r.ReportedBy,
                    r.Description,
                    r.ProgressPercent,
                    constructionStage = r.ConstructionStage,
                    photoPath = resolvedPhoto,
                    r.Latitude,
                    r.Longitude,
                    r.LoggedAt,
                    r.Status,
                    r.IsOffSite,
                    r.DistanceFromSiteMeters
                };
            }));
        }

        [HttpGet("projects")]
        public async Task<IActionResult> Projects()
        {
            var all = await _projects.GetAllAsync();
            return Ok(all.Where(p => !p.IsDeleted && string.Equals(p.Status, "Active", StringComparison.OrdinalIgnoreCase))
                .Select(p => new { p.Id, p.Title, p.Location, p.ProgressPercent, p.Status, p.Latitude, p.Longitude, p.GeofenceRadiusMeters }));
        }

        [HttpGet("sitereports/{id:guid}/photo")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPhoto(Guid id, CancellationToken ct)
        {
            var report = await _reports.GetByIdAsync(id, ct);
            if (report == null || string.IsNullOrWhiteSpace(report.PhotoPath))
            {
                return File(PlaceholderSvgBytes, "image/svg+xml");
            }
            var stream = await _storageService.GetAsync(report.PhotoPath, ct);
            if (stream == null)
            {
                if (report.PhotoPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    report.PhotoPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    return Redirect(report.PhotoPath);
                }
                return File(PlaceholderSvgBytes, "image/svg+xml");
            }

            var ext = Path.GetExtension(report.PhotoPath).ToLowerInvariant();
            var contentType = ext switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };

            return File(stream, contentType);
        }

        [HttpGet("sitereports/mine")]
        public async Task<IActionResult> Mine()
        {
            var list = await _reports.GetByUserAsync(User.Identity?.Name ?? string.Empty);
            return Ok(list.Select(r => new { r.Id, r.ProjectId, r.Description, r.ProgressPercent, r.ConstructionStage, r.Status, r.LoggedAt, hasPhoto = r.PhotoPath != null }));
        }


        [HttpPost("sitereports")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> Create(
            [FromForm] SiteReportForm form,
            CancellationToken ct)
        {
            try
            {
                UploadedPhoto? photo = null;

                if (form.Photo != null)
                {
                    await using var ms = new MemoryStream();
                    await form.Photo.CopyToAsync(ms, ct);
                    ms.Position = 0;

                    photo = new UploadedPhoto(
                        ms,
                        form.Photo.FileName,
                        form.Photo.Length
                    );

                    var reportWithPhoto = await _service.CreateAsync(
                        new SiteReportSubmission(
                            form.ProjectId,
                            form.Description,
                            form.ProgressPercent,
                            form.Latitude,
                            form.Longitude,
                            form.Stage
                        ),
                        User.Identity?.Name ?? string.Empty,
                        photo,
                        ct,
                        form.ClientReportId
                    );

                    await BroadcastSiteReportAsync(reportWithPhoto, ct);

                    return CreatedAtAction(
                        nameof(Mine),
                        new { id = reportWithPhoto.Id },
                        new
                        {
                            reportWithPhoto.Id,
                            reportWithPhoto.ConstructionStage,
                            reportWithPhoto.Status
                        }
                    );
                }

                var report = await _service.CreateAsync(
                    new SiteReportSubmission(
                        form.ProjectId,
                        form.Description,
                        form.ProgressPercent,
                        form.Latitude,
                        form.Longitude,
                        form.Stage
                    ),
                    User.Identity?.Name ?? string.Empty,
                    null,
                    ct,
                    form.ClientReportId
                );

                await BroadcastSiteReportAsync(report, ct);

                return CreatedAtAction(
                    nameof(Mine),
                    new { id = report.Id },
                    new
                    {
                        report.Id,
                        report.ConstructionStage,
                        report.Status
                    }
                );
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { error = "Project not found." });
            }
            catch (ValidationFailedException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        private async Task BroadcastSiteReportAsync(SiteReport report, CancellationToken ct)
        {
            try
            {
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                string? photoUrl = null;
                if (!string.IsNullOrWhiteSpace(report.PhotoPath))
                {
                    if (report.PhotoPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        report.PhotoPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        photoUrl = report.PhotoPath;
                    }
                    else
                    {
                        photoUrl = $"{baseUrl}/api/v1/sitereports/{report.Id}/photo";
                    }
                }

                var payload = new
                {
                    reportId = report.Id,
                    projectId = report.ProjectId,
                    reportedBy = report.ReportedBy,
                    description = report.Description,
                    progressPercent = report.ProgressPercent,
                    constructionStage = report.ConstructionStage,
                    photoUrl = photoUrl,
                    loggedAt = report.LoggedAt.ToString("o"),
                    isOffSite = report.IsOffSite,
                    distanceFromSiteMeters = report.DistanceFromSiteMeters,
                    status = report.Status
                };

                var projectGroup = $"project_{report.ProjectId.ToString().ToLowerInvariant()}";
                await _hubContext.Clients.Group(projectGroup).SendAsync("SiteReportUploaded", payload, ct);
                await _hubContext.Clients.All.SendAsync("SiteReportUploaded", payload, ct);
                await _hubContext.Clients.All.SendAsync("ProjectProgressUpdated", new { projectId = report.ProjectId, progressPercent = report.ProgressPercent }, ct);
            }
            catch
            {
                // Non-fatal if broadcast encounters client network interruption
            }
        }
    }
}
