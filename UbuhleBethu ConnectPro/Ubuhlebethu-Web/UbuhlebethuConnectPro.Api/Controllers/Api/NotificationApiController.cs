using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.WebApp.Data;
using UbuhlebethuConnectPro.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace UbuhlebethuConnectPro.Api.Controllers.Api
{
    [ApiController]
    [IgnoreAntiforgeryToken]
    [Route("api/v1/notifications")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    public class NotificationApiController : ControllerBase
    {
        private readonly UbuhlebethuConnectPro.WebApp.Data.ApplicationDbContext _dbContext;
        private readonly UbuhlebethuConnectPro.Core.Repositories.INotificationRepository _notifRepo;

        public NotificationApiController(UbuhlebethuConnectPro.WebApp.Data.ApplicationDbContext dbContext, UbuhlebethuConnectPro.Core.Repositories.INotificationRepository notifRepo)
        {
            _dbContext = dbContext;
            _notifRepo = notifRepo;
        }

        public class NotificationDto
        {
            public Guid Id { get; set; }
            public string UserId { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public bool IsRead { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        // GET: api/notifications/unread
        [HttpGet("unread")]
        public async Task<ActionResult<IEnumerable<NotificationDto>>> GetUnreadNotifications()
        {
            var username = User.Identity?.Name ?? string.Empty;
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            var notifications = _dbContext.Notifications
                .Where(n => n.UserId == username && !n.IsRead)
                .OrderByDescending(n => n.CreatedAt)
                .Take(10)
                .Select(n => new NotificationDto
                {
                    Id = n.Id,
                    UserId = n.UserId,
                    Message = n.Message,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt
                });
            var notificationList = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(notifications);

            return Ok(notificationList);
        }

        // POST: api/notifications/{id}/markread
        [HttpPost("{id}/markread")]
        public async Task<IActionResult> MarkAsRead(Guid id)
        {
            var notification = await _dbContext.Notifications.FindAsync(id);
            if (notification == null) return NotFound();

            var username = User.Identity?.Name ?? string.Empty;
            if (notification.UserId != username) return Forbid();

            notification.IsRead = true;
            await _dbContext.SaveChangesAsync();
            
            return Ok();
        }

        // GET: api/notifications/unread/{username}
        [HttpGet("unread/{username}")]
        public async Task<ActionResult<IEnumerable<NotificationDto>>> GetUnreadNotificationsByUsername(string username)
        {
            var notifications = await _notifRepo.GetUnreadAsync(username);
            var dtos = notifications.Select(n => new NotificationDto
            {
                Id = n.Id,
                UserId = n.UserId,
                Message = n.Message,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            }).ToList();
            
            return Ok(dtos);
        }

        // POST: api/notifications/{id}/read
        [HttpPost("{id}/read")]
        public async Task<IActionResult> ReadNotification(Guid id)
        {
            var notification = await _notifRepo.GetByIdAsync(id);
            if (notification == null) return NotFound();

            var username = User.Identity?.Name;
            if (!string.IsNullOrEmpty(username) && notification.UserId != username) 
                return Forbid();

            notification.IsRead = true;
            await _notifRepo.SaveChangesAsync();
            
            return Ok();
        }
    }
}
