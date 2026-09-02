using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VinayagaPlates.Application;
using VinayagaPlates.Application.Repositories;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Infrastructure.Repositories
{
    public class PartnerRepository : BaseRepository<Partner>, IPartnerRepository
    {
        public PartnerRepository(ApplicationDbContext db) : base(db)
        {
        }

        public async Task<IEnumerable<PartnerLedger>> GetLedgersAsync()
        {
            return await Db.PartnerLedgers
                .Include(l => l.Partner)
                .ToListAsync();
        }

        public async Task AddLedgerEntryAsync(PartnerLedger ledger) =>
            await Db.PartnerLedgers.AddAsync(ledger);

        public async Task CreatePartnerAsync(Partner partner, string createdBy)
        {
            partner.CreatedBy = createdBy ?? "SYSTEM";
            await Db.Partners.AddAsync(partner);
            await Db.SaveChangesAsync();
        }
    }
}
