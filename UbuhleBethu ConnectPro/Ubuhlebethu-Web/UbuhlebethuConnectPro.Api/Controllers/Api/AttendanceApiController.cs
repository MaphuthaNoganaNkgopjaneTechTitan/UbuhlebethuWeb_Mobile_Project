using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.WebApp.Data;
using UbuhlebethuConnectPro.Infrastructure.Services;

namespace UbuhlebethuConnectPro.Api.Controllers.Api
{
    public class CheckInRequestDto
    {
        public Guid ProjectId { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }

    public class CheckOutRequestDto
    {
        public int AttendanceId { get; set; }
    }

    public class ApprovalRequestDto
    {
        public bool Approved { get; set; }
    }

    public class AttendanceRecordDto
    {
        public int Id { get; set; }
        public string WorkerName { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public bool IsPresent { get; set; }
        public double DailyRate { get; set; }
        public string CheckInTime { get; set; } = "Not Started";
        public string CheckOutTime { get; set; } = "Not Finished";
        public bool IsOffSite { get; set; }
        public double? DistanceFromSiteMeters { get; set; }
    }

    [ApiController]
    [IgnoreAntiforgeryToken]
    [Route("api/v1/attendance")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class AttendanceApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly Microsoft.AspNetCore.SignalR.IHubContext<UbuhlebethuConnectPro.Api.Hubs.SiteEventsHub> _hubContext;

        public AttendanceApiController(
            ApplicationDbContext db,
            Microsoft.AspNetCore.SignalR.IHubContext<UbuhlebethuConnectPro.Api.Hubs.SiteEventsHub> hubContext)
        {
            _db = db;
            _hubContext = hubContext;
        }

        [HttpPost("check-in")]
        public async Task<IActionResult> CheckIn([FromBody] CheckInRequestDto req)
        {
            var currentUserName = User.Identity?.Name;
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserName == currentUserName || u.Id == currentUserName);
            if (user == null)
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                               ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
                if (!string.IsNullOrEmpty(userIdClaim))
                {
                    user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userIdClaim || u.UserName == userIdClaim);
                }
            }
            if (user == null) return Unauthorized();

            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == req.ProjectId);
            double? distanceFromSite = null;
            bool isOffSite = false;

            if (project != null && project.Latitude.HasValue && project.Longitude.HasValue &&
                req.Latitude.HasValue && req.Longitude.HasValue)
            {
                var dist = SiteReportService.CalculateDistanceMeters(
                    req.Latitude.Value,
                    req.Longitude.Value,
                    project.Latitude.Value,
                    project.Longitude.Value
                );
                distanceFromSite = Math.Round(dist, 1);
                var radius = project.GeofenceRadiusMeters > 0 ? project.GeofenceRadiusMeters : 300.0;
                isOffSite = dist > radius;
            }

            var att = new AttendanceRecord
            {
                UserId = user.Id,
                ProjectId = req.ProjectId,
                CheckInTime = DateTime.UtcNow,
                CheckInLatitude = req.Latitude,
                CheckInLongitude = req.Longitude,
                DistanceFromSiteMeters = distanceFromSite,
                IsOffSite = isOffSite,
                IsApproved = false
            };

            _db.AttendanceRecords.Add(att);
            await _db.SaveChangesAsync();

            try
            {
                await _hubContext.Clients.All.SendAsync("AttendanceRecorded", new
                {
                    projectId = att.ProjectId,
                    workerName = user.UserName ?? "Worker",
                    type = "CheckIn",
                    isOffSite = att.IsOffSite,
                    checkInTime = att.CheckInTime.ToString("o")
                });
            }
            catch { }

            return Ok(MapToDto(att, user));
        }

        [HttpPost("check-out")]
        public async Task<IActionResult> CheckOut([FromBody] CheckOutRequestDto req)
        {
            var att = await _db.AttendanceRecords.Include(a => a.User).FirstOrDefaultAsync(a => a.Id == req.AttendanceId);
            if (att == null) return NotFound();

            var currentUserName = User.Identity?.Name;
            var currentUser = await _db.Users.FirstOrDefaultAsync(u => u.UserName == currentUserName || u.Id == currentUserName);
            if (currentUser == null)
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                               ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
                if (!string.IsNullOrEmpty(userIdClaim))
                {
                    currentUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == userIdClaim || u.UserName == userIdClaim);
                }
            }
            if (currentUser == null) return Unauthorized();

            bool isManagerOrAdmin = User.IsInRole("SiteManager") || User.IsInRole("Manager") || User.IsInRole("Admin") || User.IsInRole("SystemAdmin") || User.IsInRole("Management");
            if (att.UserId != currentUser.Id && !isManagerOrAdmin)
            {
                return Forbid();
            }

            att.CheckOutTime = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            try
            {
                await _hubContext.Clients.All.SendAsync("AttendanceRecorded", new
                {
                    projectId = att.ProjectId,
                    workerName = att.User?.UserName ?? currentUser.UserName ?? "Worker",
                    type = "CheckOut",
                    checkOutTime = att.CheckOutTime.Value.ToString("o")
                });
            }
            catch { }

            return Ok(MapToDto(att, att.User ?? currentUser));
        }

        [HttpGet("")]
        public async Task<IActionResult> ListForProject([FromQuery] string projectId, [FromQuery] string? date)
        {
            if (!Guid.TryParse(projectId, out var projectGuid))
                return BadRequest(new { error = "Invalid projectId format." });

            var query = _db.AttendanceRecords
                .Include(a => a.User)
                .Where(a => a.ProjectId == projectGuid);

            if (!string.IsNullOrWhiteSpace(date) && DateTime.TryParse(date, out var d))
            {
                var utcDate = DateTime.SpecifyKind(d.Date, DateTimeKind.Utc);
                var start = utcDate.AddHours(-14);
                var end = utcDate.AddDays(1).AddHours(14);
                query = query.Where(a => a.CheckInTime >= start && a.CheckInTime <= end);
            }

            var records = await query
                .OrderByDescending(a => a.CheckInTime)
                .ToListAsync();

            return Ok(records.Select(r => MapToDto(r, r.User)));
        }

        [HttpPut("{id}/approval")]
        [Authorize(Roles = "SiteManager,Manager,Admin,SystemAdmin,Management")]
        public async Task<IActionResult> Approve(int id, [FromBody] ApprovalRequestDto req)
        {
            if (!User.IsInRole("SiteManager") && !User.IsInRole("Manager") && !User.IsInRole("Admin") && !User.IsInRole("SystemAdmin") && !User.IsInRole("Management"))
            {
                return Forbid();
            }

            var att = await _db.AttendanceRecords.FindAsync(id);
            if (att == null) return NotFound();

            att.IsApproved = req.Approved;
            await _db.SaveChangesAsync();
            return Ok();
        }

        private static AttendanceRecordDto MapToDto(AttendanceRecord att, ApplicationUser? user)
        {
            return new AttendanceRecordDto
            {
                Id = att.Id,
                WorkerName = user?.UserName ?? (!string.IsNullOrEmpty(att.UserId) ? att.UserId : "Worker"),
                Date = att.CheckInTime.ToLocalTime().ToString("yyyy-MM-dd"),
                IsPresent = true,
                DailyRate = 500.00,
                CheckInTime = att.CheckInTime.ToLocalTime().ToString("HH:mm"),
                CheckOutTime = att.CheckOutTime.HasValue ? att.CheckOutTime.Value.ToLocalTime().ToString("HH:mm") : "In Progress (On Shift)",
                IsOffSite = att.IsOffSite,
                DistanceFromSiteMeters = att.DistanceFromSiteMeters
            };
        }
    }
}
