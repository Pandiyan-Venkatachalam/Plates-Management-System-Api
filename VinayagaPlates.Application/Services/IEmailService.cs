using System.Threading.Tasks;

namespace VinayagaPlates.Application.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string senderEmail, string senderPassword, string to, string subject, string htmlBody);
    }
}
