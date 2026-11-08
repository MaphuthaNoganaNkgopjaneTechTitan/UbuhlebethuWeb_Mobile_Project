using System;
using System.Threading.Tasks;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.Core.Repositories;
using UbuhlebethuConnectPro.Core.Services;

namespace UbuhlebethuConnectPro.Infrastructure.Services
{
    public class DbNotificationObserver : INotificationObserver
    {
        private readonly INotificationRepository _notificationRepo;

        public DbNotificationObserver(INotificationRepository notificationRepo)
        {
            _notificationRepo = notificationRepo;
        }

        public async Task NotifyAsync(string userId, string message, string referenceId)
        {
            var notif = new Notification
            {
                UserId = userId,
                Message = message,
                CreatedAt = DateTime.UtcNow,
                IsRead = false,
                ReferenceId = referenceId
            };
            await _notificationRepo.AddAsync(notif);
            await _notificationRepo.SaveChangesAsync();
        }
    }
}
