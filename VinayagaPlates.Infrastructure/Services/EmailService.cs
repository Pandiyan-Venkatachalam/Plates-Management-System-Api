using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using VinayagaPlates.Application.Services;
using Microsoft.Extensions.Logging;

namespace VinayagaPlates.Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendEmailAsync(string senderEmail, string senderPassword, string to, string subject, string htmlBody)
        {
            try
            {
                if (string.IsNullOrEmpty(senderEmail) || string.IsNullOrEmpty(senderPassword))
                {
                    _logger.LogWarning("Email credentials are not provided.");
                    return;
                }

                using var client = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential(senderEmail, senderPassword),
                    EnableSsl = true,
                };

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail, "VPMS System"),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true,
                };

                // Add multiple recipients if provided as comma-separated string
                var recipients = to.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var recipient in recipients)
                {
                    mailMessage.To.Add(recipient.Trim());
                }

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation($"Email sent successfully to {to}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to send email: {ex.Message}");
            }
        }
    }
}
