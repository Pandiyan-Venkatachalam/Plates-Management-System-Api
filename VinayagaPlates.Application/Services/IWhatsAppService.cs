using System.Collections.Generic;
using System.Threading.Tasks;
using VinayagaPlates.Contracts.DTOs;

namespace VinayagaPlates.Application.Services
{
    public interface IWhatsAppService
    {
        Task<List<PartnerWhatsAppDto>> GetPartnerWhatsAppRecipientsAsync();
        Task<bool> SendWhatsAppMessageAsync(string phoneNumber, string message);
        Task<int> BroadcastToPartnersAsync(string message, string eventType);
    }
}
