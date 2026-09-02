using System.Collections.Generic;
using System.Threading.Tasks;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Application.Repositories
{
    public interface IBatchRepository : IBaseRepository<InventoryBatch>
    {
        Task<IEnumerable<InventoryBatch>> GetBatchesAsync();
        Task<InventoryBatch> GetBatchByIdAsync(int batchId);
        Task AddBatchAsync(InventoryBatch batch);
        void UpdateBatch(InventoryBatch batch);
        Task AddMovementAsync(InventoryMovement movement);
        Task<IEnumerable<InventoryBatch>> GetAvailableBatchesForProductAsync(int productId);
    }
}
