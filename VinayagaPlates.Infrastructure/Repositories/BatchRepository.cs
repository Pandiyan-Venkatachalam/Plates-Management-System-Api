using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VinayagaPlates.Application;
using VinayagaPlates.Application.Repositories;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Infrastructure.Repositories
{
    public class BatchRepository : BaseRepository<InventoryBatch>, IBatchRepository
    {
        public BatchRepository(ApplicationDbContext db) : base(db)
        {
        }

        public async Task<IEnumerable<InventoryBatch>> GetBatchesAsync()
        {
            return await Db.InventoryBatches
                .Include(b => b.Product)
                .ToListAsync();
        }

        public async Task<InventoryBatch> GetBatchByIdAsync(int batchId) =>
            await Db.InventoryBatches.FindAsync(batchId);

        public async Task AddBatchAsync(InventoryBatch batch) =>
            await Db.InventoryBatches.AddAsync(batch);

        public void UpdateBatch(InventoryBatch batch) =>
            Db.InventoryBatches.Update(batch);

        public async Task AddMovementAsync(InventoryMovement movement) =>
            await Db.InventoryMovements.AddAsync(movement);

        public async Task<IEnumerable<InventoryBatch>> GetAvailableBatchesForProductAsync(int productId)
        {
            return await Db.InventoryBatches
                .Where(b => b.ProductId == productId && b.CurrentQuantity > 0)
                .OrderBy(b => b.ReceivedDate)
                .ToListAsync();
        }
    }
}
