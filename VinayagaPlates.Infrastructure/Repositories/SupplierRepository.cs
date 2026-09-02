using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VinayagaPlates.Application;
using VinayagaPlates.Application.Repositories;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Infrastructure.Repositories
{
    public class SupplierRepository : BaseRepository<Supplier>, ISupplierRepository
    {
        public SupplierRepository(ApplicationDbContext db) : base(db)
        {
        }

        public async Task<IEnumerable<Supplier>> GetSuppliersAsync() =>
            await Db.Suppliers.ToListAsync();

        public async Task AddSupplierAsync(Supplier supplier) =>
            await Db.Suppliers.AddAsync(supplier);
    }
}
