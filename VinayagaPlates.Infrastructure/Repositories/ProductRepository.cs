using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VinayagaPlates.Application;
using VinayagaPlates.Application.Repositories;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Infrastructure.Repositories
{
    public class ProductRepository : BaseRepository<Product>, IProductRepository
    {
        public ProductRepository(ApplicationDbContext db) : base(db)
        {
        }



        public async Task<IEnumerable<Product>> GetProductsWithDetailsAsync()
        {
            return await Db.Products
                .Include(p => p.Category)
                .Include(p => p.Variant)
                .Include(p => p.Unit)
                .Include(p => p.InventoryBatches)
                .ToListAsync();
        }
    }
}
