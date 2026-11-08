using System.Threading.Tasks;

namespace UbuhlebethuConnectPro.Core.Services
{
    public interface IEmailNotificationService
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlBody);
    }
}