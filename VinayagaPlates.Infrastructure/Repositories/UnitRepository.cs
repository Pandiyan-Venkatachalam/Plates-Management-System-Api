using VinayagaPlates.Application;
using VinayagaPlates.Application.Repositories;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Infrastructure.Repositories
{
    public class UnitRepository : BaseRepository<ProductUnit>, IUnitRepository
    {
        public UnitRepository(ApplicationDbContext db) : base(db)
        {
        }
    }
}
