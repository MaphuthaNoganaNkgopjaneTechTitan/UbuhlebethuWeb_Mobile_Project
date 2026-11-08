using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UbuhlebethuConnectPro.WebApp.ApiClient;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace UbuhlebethuConnectPro.WebApp.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly IConnectProApiClient _apiClient;

        public NotificationsController(IConnectProApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        // Returns a partial listing of recent notifications for the current user
        public async Task<IActionResult> List()
        {
            var username = User.Identity?.Name ?? string.Empty;
            var notifs = await _apiClient.GetUnreadNotificationsAsync(username);
            return PartialView("_NotificationsList", notifs.Take(10).ToList());
        }

        // Full notifications center
        public async Task<IActionResult> Index()
        {
            var username = User.Identity?.Name ?? string.Empty;
            var notifs = await _apiClient.GetUnreadNotificationsAsync(username);
            return View(notifs);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(Guid id)
        {
            await _apiClient.MarkNotificationReadAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}