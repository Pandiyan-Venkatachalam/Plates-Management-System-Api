using System.Collections.Generic;
using System.Threading.Tasks;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Application.Repositories
{
    public interface IPartnerRepository : IBaseRepository<Partner>
    {
        Task<IEnumerable<PartnerLedger>> GetLedgersAsync();
        Task AddLedgerEntryAsync(PartnerLedger ledger);
        Task CreatePartnerAsync(Partner partner, string createdBy);
        Task<Partner> GetByIdAsync(int id);
        void Update(Partner partner);
        void Delete(Partner partner);
    }
}
