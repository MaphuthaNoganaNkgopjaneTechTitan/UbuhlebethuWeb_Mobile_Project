using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace UbuhlebethuConnectPro.Core.Services
{
    public class EmailNotificationService : IEmailNotificationService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailNotificationService> _logger;

        public EmailNotificationService(IConfiguration config, ILogger<EmailNotificationService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                bool isEnabled = _config.GetValue<bool>("Email:Enabled");
                string smtpHost = _config["Email:SmtpHost"] ?? "smtp-relay.brevo.com";
                int port = _config.GetValue<int>("Email:Port", 587);
                string user = _config["Email:User"] ?? string.Empty;
                string password = _config["Email:Password"] ?? string.Empty;
                string fromEmail = _config["Email:From"] ?? "no-reply@ubuhlebethu.co.za";

                // If disabled or missing credentials, log and exit gracefully without throwing
                if (!isEnabled || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
                {
                    _logger.LogInformation("[Email Mock/Disabled] To: {To} | Subject: {Subject}", toEmail, subject);
                    return;
                }

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail, "Ubuhlebethu ConnectPro"),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };
                mailMessage.To.Add(toEmail);

                using var smtpClient = new SmtpClient(smtpHost, port)
                {
                    Credentials = new NetworkCredential(user, password),
                    EnableSsl = true
                };

                await smtpClient.SendMailAsync(mailMessage);
                _logger.LogInformation("[Email Sent] Notification delivered to {To}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email notification to {Email}", toEmail);
            }
        }
    }
}