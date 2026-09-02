using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Application.Services
{
    public class InventoryService : IInventoryService
    {
        private readonly ApplicationDbContext _db;

        public InventoryService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<int> GetPhysicalStockAsync(int productId, int locationId)
        {
            // Physical stock derived from IN/OUT movements
            var inQty = await _db.InventoryMovements
                .Where(m => m.ProductId == productId && m.LocationId == locationId && m.Direction == "IN")
                .SumAsync(m => m.Quantity);

            var outQty = await _db.InventoryMovements
                .Where(m => m.ProductId == productId && m.LocationId == locationId && m.Direction == "OUT")
                .SumAsync(m => m.Quantity);

            return inQty - outQty;
        }

        public async Task<int> GetReservedStockAsync(int productId, int locationId)
        {
            return await _db.StockReservations
                .Where(sr => sr.ProductId == productId && sr.LocationId == locationId && sr.Status == "ACTIVE")
                .SumAsync(sr => sr.Quantity);
        }

        public async Task<int> GetAvailableStockAsync(int productId, int locationId, int? excludeOrderId = null)
        {
            var physical = await GetPhysicalStockAsync(productId, locationId);
            
            // Get reservations, excluding the current order reservation if passed
            var query = _db.StockReservations
                .Where(sr => sr.ProductId == productId && sr.LocationId == locationId && sr.Status == "ACTIVE");

            if (excludeOrderId.HasValue)
            {
                query = query.Where(sr => sr.OrderDetail.OrderId != excludeOrderId.Value);
            }

            var reserved = await query.SumAsync(sr => sr.Quantity);

            return Math.Max(0, physical - reserved);
        }

        public async Task<List<InventoryAllocation>> AllocateStockFIFOAsync(
            int productId, 
            int locationId, 
            int quantity, 
            int saleDetailId, 
            int? orderId, 
            string username)
        {
            if (quantity <= 0)
                throw new ArgumentException("Quantity to allocate must be positive.");

            // Concurrency: Select batches with raw row-level update locking
            var isSqlite = _db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
            var query = isSqlite 
                ? @"SELECT * FROM ""InventoryBatches"" WHERE ""ProductId"" = {0} AND ""LocationId"" = {1} AND ""CurrentQuantity"" > 0"
                : @"SELECT * FROM ""InventoryBatches"" WHERE ""ProductId"" = {0} AND ""LocationId"" = {1} AND ""CurrentQuantity"" > 0 FOR UPDATE";

            var batches = await _db.InventoryBatches
                .FromSqlRaw(query, productId, locationId)
                .OrderBy(b => b.ReceivedDate)
                .ThenBy(b => b.BatchId)
                .ToListAsync();

            // Validate available stock including reservation logic
            var available = await GetAvailableStockAsync(productId, locationId, orderId);
            if (quantity > available)
                throw new InvalidOperationException($"Insufficient stock available. Required: {quantity}, Available: {available}.");

            var allocations = new List<InventoryAllocation>();
            int remaining = quantity;

            foreach (var b in batches)
            {
                if (remaining <= 0) break;

                int allocated = Math.Min(b.CurrentQuantity, remaining);
                b.CurrentQuantity -= allocated;
                remaining -= allocated;

                var allocation = new InventoryAllocation
                {
                    SaleDetailId = saleDetailId,
                    BatchId = b.BatchId,
                    Quantity = allocated,
                    UnitCost = b.LandedUnitCost > 0 ? b.LandedUnitCost : b.UnitCost,
                    TotalCost = allocated * (b.LandedUnitCost > 0 ? b.LandedUnitCost : b.UnitCost),
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = username
                };

                allocations.Add(allocation);
                await _db.InventoryAllocations.AddAsync(allocation);

                var movement = new InventoryMovement
                {
                    ProductId = productId,
                    BatchId = b.BatchId,
                    LocationId = locationId,
                    MovementType = "SALE_OUT",
                    Direction = "OUT",
                    Quantity = allocated,
                    UnitCost = allocation.UnitCost,
                    TotalCost = allocation.TotalCost,
                    ReferenceType = "SALE_DETAIL",
                    ReferenceId = saleDetailId.ToString(),
                    Description = $"FIFO Allocation for Sale Detail {saleDetailId}",
                    CreatedBy = username,
                    CreatedAt = DateTime.UtcNow
                };

                await _db.InventoryMovements.AddAsync(movement);
            }

            if (remaining > 0)
                throw new InvalidOperationException("Batches did not contain enough physical quantity to satisfy the request.");

            await _db.SaveChangesAsync();
            return allocations;
        }

        public async Task TransferStockAsync(
            int productId, 
            int sourceLocationId, 
            int destLocationId, 
            int quantity, 
            string referenceType, 
            string referenceId, 
            string username)
        {
            if (quantity <= 0)
                throw new ArgumentException("Quantity to transfer must be positive.");

            // Concurrency: SELECT FOR UPDATE on source batches
            var isSqlite = _db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
            var query = isSqlite 
                ? @"SELECT * FROM ""InventoryBatches"" WHERE ""ProductId"" = {0} AND ""LocationId"" = {1} AND ""CurrentQuantity"" > 0"
                : @"SELECT * FROM ""InventoryBatches"" WHERE ""ProductId"" = {0} AND ""LocationId"" = {1} AND ""CurrentQuantity"" > 0 FOR UPDATE";

            var sourceBatches = await _db.InventoryBatches
                .FromSqlRaw(query, productId, sourceLocationId)
                .OrderBy(b => b.ReceivedDate)
                .ThenBy(b => b.BatchId)
                .ToListAsync();

            var available = sourceBatches.Sum(b => b.CurrentQuantity);
            if (quantity > available)
                throw new InvalidOperationException($"Insufficient source quantity for transfer. Required: {quantity}, Available: {available}.");

            int remaining = quantity;

            foreach (var sb in sourceBatches)
            {
                if (remaining <= 0) break;

                int allocated = Math.Min(sb.CurrentQuantity, remaining);
                sb.CurrentQuantity -= allocated;
                remaining -= allocated;

                // Log TRANSFER_OUT movement
                var outMovement = new InventoryMovement
                {
                    ProductId = productId,
                    BatchId = sb.BatchId,
                    LocationId = sourceLocationId,
                    MovementType = "TRANSFER_OUT",
                    Direction = "OUT",
                    Quantity = allocated,
                    UnitCost = sb.UnitCost,
                    TotalCost = allocated * sb.UnitCost,
                    ReferenceType = referenceType,
                    ReferenceId = referenceId,
                    Description = $"Transfer to Location {destLocationId}",
                    CreatedBy = username,
                    CreatedAt = DateTime.UtcNow
                };
                await _db.InventoryMovements.AddAsync(outMovement);

                // Create matching batch at destination preserving cost-layer info
                var destBatch = new InventoryBatch
                {
                    BatchNumber = sb.BatchNumber + "-TRF",
                    ProductId = productId,
                    InitialQuantity = allocated,
                    CurrentQuantity = allocated,
                    UnitCost = sb.UnitCost,
                    LandedUnitCost = sb.LandedUnitCost,
                    TotalLandedCost = allocated * sb.LandedUnitCost,
                    LocationId = destLocationId,
                    ReceivedDate = DateTime.UtcNow,
                    Status = sb.Status,
                    CreatedBy = username,
                    CreatedAt = DateTime.UtcNow
                };
                await _db.InventoryBatches.AddAsync(destBatch);
                await _db.SaveChangesAsync(); // save to generate batch ID

                // Log TRANSFER_IN movement
                var inMovement = new InventoryMovement
                {
                    ProductId = productId,
                    BatchId = destBatch.BatchId,
                    LocationId = destLocationId,
                    MovementType = "TRANSFER_IN",
                    Direction = "IN",
                    Quantity = allocated,
                    UnitCost = sb.UnitCost,
                    TotalCost = allocated * sb.UnitCost,
                    ReferenceType = referenceType,
                    ReferenceId = referenceId,
                    Description = $"Transfer from Location {sourceLocationId} Batch {sb.BatchId}",
                    CreatedBy = username,
                    CreatedAt = DateTime.UtcNow
                };
                await _db.InventoryMovements.AddAsync(inMovement);
            }

            await _db.SaveChangesAsync();
        }

        public async Task AdjustStockAsync(
            int productId, 
            int locationId, 
            int quantity, 
            string direction, 
            decimal unitCost, 
            string reason, 
            string username)
        {
            if (quantity <= 0)
                throw new ArgumentException("Quantity must be positive.");

            if (direction != "IN" && direction != "OUT")
                throw new ArgumentException("Direction must be 'IN' or 'OUT'.");

            if (direction == "IN")
            {
                // Create new batch for inbound adjustment
                var batch = new InventoryBatch
                {
                    BatchNumber = "ADJ-" + DateTime.UtcNow.Ticks.ToString().Substring(10),
                    ProductId = productId,
                    InitialQuantity = quantity,
                    CurrentQuantity = quantity,
                    UnitCost = unitCost,
                    LandedUnitCost = unitCost,
                    TotalLandedCost = quantity * unitCost,
                    LocationId = locationId,
                    ReceivedDate = DateTime.UtcNow,
                    Status = "FINALIZED",
                    CreatedBy = username,
                    CreatedAt = DateTime.UtcNow
                };

                await _db.InventoryBatches.AddAsync(batch);
                await _db.SaveChangesAsync();

                var movement = new InventoryMovement
                {
                    ProductId = productId,
                    BatchId = batch.BatchId,
                    LocationId = locationId,
                    MovementType = "ADJUSTMENT_IN",
                    Direction = "IN",
                    Quantity = quantity,
                    UnitCost = unitCost,
                    TotalCost = quantity * unitCost,
                    ReferenceType = "ADJUSTMENT",
                    ReferenceId = batch.BatchId.ToString(),
                    Description = reason,
                    CreatedBy = username,
                    CreatedAt = DateTime.UtcNow
                };
                await _db.InventoryMovements.AddAsync(movement);
            }
            else
            {
                // Outbound adjustment: consume existing batches (FIFO)
                var isSqlite = _db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
                var query = isSqlite 
                    ? @"SELECT * FROM ""InventoryBatches"" WHERE ""ProductId"" = {0} AND ""LocationId"" = {1} AND ""CurrentQuantity"" > 0"
                    : @"SELECT * FROM ""InventoryBatches"" WHERE ""ProductId"" = {0} AND ""LocationId"" = {1} AND ""CurrentQuantity"" > 0 FOR UPDATE";

                var sourceBatches = await _db.InventoryBatches
                    .FromSqlRaw(query, productId, locationId)
                    .OrderBy(b => b.ReceivedDate)
                    .ThenBy(b => b.BatchId)
                    .ToListAsync();

                var available = sourceBatches.Sum(b => b.CurrentQuantity);
                if (quantity > available)
                    throw new InvalidOperationException($"Insufficient quantity for adjustment. Required: {quantity}, Available: {available}.");

                int remaining = quantity;

                foreach (var b in sourceBatches)
                {
                    if (remaining <= 0) break;

                    int allocated = Math.Min(b.CurrentQuantity, remaining);
                    b.CurrentQuantity -= allocated;
                    remaining -= allocated;

                    var movement = new InventoryMovement
                    {
                        ProductId = productId,
                        BatchId = b.BatchId,
                        LocationId = locationId,
                        MovementType = "ADJUSTMENT_OUT",
                        Direction = "OUT",
                        Quantity = allocated,
                        UnitCost = b.UnitCost,
                        TotalCost = allocated * b.UnitCost,
                        ReferenceType = "ADJUSTMENT",
                        ReferenceId = b.BatchId.ToString(),
                        Description = reason,
                        CreatedBy = username,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _db.InventoryMovements.AddAsync(movement);
                }
            }

            await _db.SaveChangesAsync();
        }

        public async Task<bool> ReconcileStockAsync(int productId, int locationId)
        {
            var movementStock = await GetPhysicalStockAsync(productId, locationId);
            
            var batchStock = await _db.InventoryBatches
                .Where(b => b.ProductId == productId && b.LocationId == locationId)
                .SumAsync(b => b.CurrentQuantity);

            return movementStock == batchStock;
        }
    }
}
