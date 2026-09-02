using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VinayagaPlates.Application;
using VinayagaPlates.Application.Repositories;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Infrastructure.Repositories
{
    public class PurchaseRepository : BaseRepository<Purchase>, IPurchaseRepository
    {
        public PurchaseRepository(ApplicationDbContext db) : base(db)
        {
        }

        public async Task<IEnumerable<Purchase>> GetPurchasesWithDetailsAsync()
        {
            return await Db.Purchases
                .Include(p => p.Supplier)
                .Include(p => p.Details)
                    .ThenInclude(d => d.Product)
                        .ThenInclude(p => p.Category)
                .Include(p => p.Details)
                    .ThenInclude(d => d.Product)
                        .ThenInclude(p => p.Variant)
                .Include(p => p.Details)
                    .ThenInclude(d => d.Product)
                        .ThenInclude(p => p.Unit)
                .ToListAsync();
        }

        public async Task<Purchase> GetPurchaseWithDetailsByIdAsync(int id)
        {
            return await Db.Purchases
                .Include(p => p.Supplier)
                .Include(p => p.Details)
                    .ThenInclude(d => d.Product)
                        .ThenInclude(p => p.Category)
                .Include(p => p.Details)
                    .ThenInclude(d => d.Product)
                        .ThenInclude(p => p.Variant)
                .Include(p => p.Details)
                    .ThenInclude(d => d.Product)
                        .ThenInclude(p => p.Unit)
                .FirstOrDefaultAsync(p => p.PurchaseId == id);
        }
    }
}
