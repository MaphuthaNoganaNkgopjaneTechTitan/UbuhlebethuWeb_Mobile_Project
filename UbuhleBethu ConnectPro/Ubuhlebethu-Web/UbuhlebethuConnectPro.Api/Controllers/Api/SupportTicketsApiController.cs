using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.Core.Services;
using UbuhlebethuConnectPro.WebApp.Data;

namespace UbuhlebethuConnectPro.Api.Controllers.Api
{
    public class TicketRequest
    {
        public string Subject { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class TicketStatusRequest
    {
        public string Status { get; set; } = string.Empty;
        public string? Response { get; set; }
    }

    public class RespondTicketRequest
    {
        public string Response { get; set; } = string.Empty;
    }

    [ApiController]
    [IgnoreAntiforgeryToken]
    [Route("api/v1/support/tickets")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class SupportTicketsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IEmailNotificationService _emailService;

        public SupportTicketsApiController(ApplicationDbContext db, IEmailNotificationService emailService)
        {
            _db = db;
            _emailService = emailService;
        }

        // GET: api/v1/support/tickets
        [HttpGet]
        public async Task<IActionResult> List()
        {
            var isStaff = User.IsInRole("Admin") ||
                          User.IsInRole("SystemAdmin") ||
                          User.IsInRole("Manager") ||
                          User.IsInRole("Management") ||
                          User.IsInRole("Executive") ||
                          User.IsInRole("Office") ||
                          User.IsInRole("OfficeStaff");

            var query = _db.SupportTickets.AsNoTracking();

            if (!isStaff)
            {
                var currentUserName = User.Identity?.Name ?? string.Empty;
                query = query.Where(t => t.CreatedBy == currentUserName);
            }

            var tickets = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();
            return Ok(tickets);
        }

        // POST: api/v1/support/tickets
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] TicketRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Description))
                return BadRequest(new { error = "Subject and description are required." });

            if (request.Subject.Length > 160 || request.Description.Length > 4000)
                return BadRequest(new { error = "Subject or description is too long." });

            var ticket = new SupportTicket
            {
                CreatedBy = User.Identity?.Name ?? string.Empty,
                Subject = request.Subject.Trim(),
                Description = request.Description.Trim(),
                Status = "Open",
                CreatedAt = DateTime.UtcNow
            };

            _db.SupportTickets.Add(ticket);
            await _db.SaveChangesAsync();

            return Ok(ticket);
        }

        // POST: api/v1/support/tickets/{id}/respond
        [HttpPost("{id:guid}/respond")]
        [Authorize(Roles = "Admin,SystemAdmin,Manager,Management,Executive,Office,OfficeStaff")]
        public async Task<IActionResult> Respond(Guid id, [FromBody] RespondTicketRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Response))
                return BadRequest(new { error = "Response cannot be blank." });

            var ticket = await _db.SupportTickets.FindAsync(id);
            if (ticket == null)
                return NotFound(new { error = "Ticket not found." });

            // 1. Identify assigned role tag from subject
            string assignedRole = "Project Manager";
            string cleanSubject = ticket.Subject ?? "Support Request";
            if (!string.IsNullOrEmpty(ticket.Subject) && ticket.Subject.StartsWith("[") && ticket.Subject.Contains("]"))
            {
                int endIdx = ticket.Subject.IndexOf(']');
                assignedRole = ticket.Subject.Substring(1, endIdx - 1);
                cleanSubject = ticket.Subject.Substring(endIdx + 1).Trim();
            }

            bool isManager = User.IsInRole("Manager") || User.IsInRole("Management") || User.IsInRole("Executive");
            bool isOffice = User.IsInRole("Office") || User.IsInRole("OfficeStaff");
            bool isAdmin = User.IsInRole("Admin") || User.IsInRole("SystemAdmin");

            bool authorizedToReply = false;
            if (assignedRole.Contains("Manager") && isManager) authorizedToReply = true;
            else if ((assignedRole.Contains("Office") || assignedRole.Contains("QS")) && isOffice) authorizedToReply = true;
            else if (assignedRole.Contains("Admin") && isAdmin) authorizedToReply = true;
            else if (isAdmin) authorizedToReply = true;

            if (!authorizedToReply)
            {
                return Forbid();
            }

            var responderName = User.Identity?.Name ?? assignedRole;

            // 2. Update ticket status and reply
            ticket.Response = request.Response.Trim();
            ticket.Status = "Resolved";
            ticket.UpdatedAt = DateTime.UtcNow;

            // 3. Create the in-app notification for the ticket creator
            var notificationText = $"Response received from {responderName} on ticket '{cleanSubject}': {request.Response.Trim()}";

            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = ticket.CreatedBy, // Targets shudu@gmail.com
                Message = notificationText,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };
            _db.Notifications.Add(notification);

            await _db.SaveChangesAsync();

            // 4. Dispatch Email Notification to Client if valid email
            if (!string.IsNullOrEmpty(ticket.CreatedBy) && ticket.CreatedBy.Contains("@"))
            {
                string emailSubject = $"Update on Ticket: {cleanSubject} [Resolved]";
                string emailHtml = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden;'>
                        <div style='background-color: #9b1c1c; padding: 20px; color: white;'>
                            <h2 style='margin: 0; font-size: 20px;'>Ubuhlebethu ConnectPro</h2>
                            <p style='margin: 4px 0 0 0; font-size: 13px; opacity: 0.9;'>Support Ticket Update</p>
                        </div>
                        <div style='padding: 24px; color: #334155; line-height: 1.6;'>
                            <p>Hello,</p>
                            <p>Your support ticket regarding <strong>{cleanSubject}</strong> has been reviewed and responded to by <strong>{responderName}</strong>.</p>
                            
                            <div style='background: #f8fafc; border-left: 4px solid #16a34a; padding: 14px; margin: 20px 0; border-radius: 4px;'>
                                <p style='margin: 0 0 6px 0; font-size: 12px; color: #64748b; font-weight: bold;'>STAKEHOLDER RESPONSE:</p>
                                <p style='margin: 0; font-size: 14px;'>{request.Response.Trim()}</p>
                            </div>

                            <p style='margin-top: 24px;'>You can log in to your portal anytime to view this ticket and track ongoing site progress.</p>
                            <a href='http://localhost:7139/Support' style='display: inline-block; background-color: #9b1c1c; color: white; padding: 10px 18px; text-decoration: none; border-radius: 6px; font-weight: bold; font-size: 14px;'>View Support Ticket</a>
                        </div>
                        <div style='background: #f1f5f9; padding: 12px 24px; font-size: 11px; color: #64748b; text-align: center;'>
                            &copy; {DateTime.UtcNow.Year} Ubuhlebethu Business Enterprise CC. All rights reserved.
                        </div>
                    </div>";

                // Fire and forget email task so response completes without latency
                _ = _emailService.SendEmailAsync(ticket.CreatedBy, emailSubject, emailHtml);
            }

            return Ok(new { message = "Response submitted and notification generated successfully.", ticket });
        }

        // PUT: api/v1/support/tickets/{id}/status
        [HttpPut("{id:guid}/status")]
        [Authorize(Roles = "Admin,SystemAdmin,Manager,Management,Office,OfficeStaff,Executive")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] TicketStatusRequest request)
        {
            if (request == null || !new[] { "Open", "In Progress", "Resolved", "Closed" }.Contains(request.Status))
                return BadRequest(new { error = "Invalid ticket status." });

            var ticket = await _db.SupportTickets.FindAsync(id);
            if (ticket == null)
                return NotFound();

            ticket.Status = request.Status;
            ticket.Response = request.Response?.Trim();
            ticket.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Ok(ticket);
        }
    }
}