using VinayagaPlates.Application;
using VinayagaPlates.Application.Repositories;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Infrastructure.Repositories
{
    public class VariantRepository : BaseRepository<ProductVariant>, IVariantRepository
    {
        public VariantRepository(ApplicationDbContext db) : base(db)
        {
        }
    }
}
