using System.Threading.Tasks;

namespace UbuhlebethuConnectPro.Core.Services
{
    public interface INotificationObserver
    {
        Task NotifyAsync(string userId, string message, string referenceId);
    }
}
