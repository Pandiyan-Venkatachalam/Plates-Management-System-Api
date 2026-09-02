using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VinayagaPlates.Application;
using VinayagaPlates.Application.Repositories;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Infrastructure.Repositories
{
    public class PartnerLedgerRepository : BaseRepository<PartnerLedger>, IPartnerLedgerRepository
    {
        public PartnerLedgerRepository(ApplicationDbContext db) : base(db)
        {
        }

        public async Task<IEnumerable<PartnerLedger>> GetLedgerWithPartnerAsync()
        {
            return await Db.PartnerLedgers
                .Include(l => l.Partner)
                .ToListAsync();
        }
    }
}
