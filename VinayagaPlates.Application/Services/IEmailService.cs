using System.Threading.Tasks;

namespace VinayagaPlates.Application.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string htmlBody);
    }
}
