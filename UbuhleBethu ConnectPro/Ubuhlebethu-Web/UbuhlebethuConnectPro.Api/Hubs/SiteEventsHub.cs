using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace UbuhlebethuConnectPro.Api.Hubs
{
    /// <summary>
    /// Lightweight SignalR hub for real-time site events (site reports, photos, progress updates, attendance, materials).
    /// Pushes instant updates to connected web dashboards (Manager Details, Client Portal, Site Reports grid).
    /// </summary>
    public class SiteEventsHub : Hub
    {
        public async Task JoinProject(string projectId)
        {
            if (!string.IsNullOrWhiteSpace(projectId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"project_{projectId.Trim().ToLowerInvariant()}");
            }
        }

        public async Task LeaveProject(string projectId)
        {
            if (!string.IsNullOrWhiteSpace(projectId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"project_{projectId.Trim().ToLowerInvariant()}");
            }
        }
    }
}
