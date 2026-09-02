using System.Collections.Generic;
using System.Threading.Tasks;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Application.Repositories
{
    public interface IPartnerLedgerRepository : IBaseRepository<PartnerLedger>
    {
        Task<IEnumerable<PartnerLedger>> GetLedgerWithPartnerAsync();
        Task<PartnerLedger> GetByIdAsync(int id);
        void Update(PartnerLedger ledger);
        void Delete(PartnerLedger ledger);
    }
}
