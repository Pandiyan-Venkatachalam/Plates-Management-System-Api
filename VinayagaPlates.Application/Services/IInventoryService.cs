using System.Collections.Generic;
using System.Threading.Tasks;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Application.Services
{
    public interface IInventoryService
    {
        Task<int> GetPhysicalStockAsync(int productId, int locationId);
        Task<int> GetReservedStockAsync(int productId, int locationId);
        Task<int> GetAvailableStockAsync(int productId, int locationId, int? excludeOrderId = null);
        
        Task<List<InventoryAllocation>> AllocateStockFIFOAsync(
            int productId, 
            int locationId, 
            int quantity, 
            int saleDetailId, 
            int? orderId, 
            string username);

        Task TransferStockAsync(
            int productId, 
            int sourceLocationId, 
            int destLocationId, 
            int quantity, 
            string referenceType, 
            string referenceId, 
            string username);

        Task AdjustStockAsync(
            int productId, 
            int locationId, 
            int quantity, 
            string direction, 
            decimal unitCost, 
            string reason, 
            string username);

        Task<bool> ReconcileStockAsync(int productId, int locationId);
    }
}
