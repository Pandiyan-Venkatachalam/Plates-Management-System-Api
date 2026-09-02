using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VinayagaPlates.Application;
using VinayagaPlates.Application.Repositories;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Infrastructure.Repositories
{
    public class SalesRepository : BaseRepository<Sale>, ISalesRepository
    {
        public SalesRepository(ApplicationDbContext db) : base(db)
        {
        }

        public async Task<IEnumerable<Sale>> GetSalesWithDetailsAsync()
        {
            return await Db.Sales
                .Include(s => s.Customer)
                .Include(s => s.Details)
                .ToListAsync();
        }
    }
}
