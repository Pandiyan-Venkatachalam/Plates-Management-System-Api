using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VinayagaPlates.Application;
using VinayagaPlates.Application.Services;
using VinayagaPlates.Domain.Entities;
using Xunit;

namespace VinayagaPlates.Tests
{
    public class InventoryServiceTests
    {
        private ApplicationDbContext CreateInMemoryDatabaseContext()
        {
            var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
            connection.Open();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

            var db = new ApplicationDbContext(options);
            db.Database.EnsureCreated();
            return db;
        }

        private async Task SeedBasicData(ApplicationDbContext db)
        {
            var cat = new ProductCategory { CategoryId = 1, CategoryName = "Plates" };
            var v = new ProductVariant { VariantId = 1, VariantName = "Round" };
            var u = new ProductUnit { UnitId = 1, UnitName = "Pieces" };
            await db.ProductCategories.AddAsync(cat);
            await db.ProductVariants.AddAsync(v);
            await db.ProductUnits.AddAsync(u);
            await db.SaveChangesAsync();

            var product = new Product 
            { 
                ProductId = 1, 
                ProductName = "12 inch Areca Plate", 
                ProductCode = "PRD001",
                CategoryId = 1,
                VariantId = 1,
                UnitId = 1
            };
            var location = new Location { LocationId = 1, LocationName = "Main Godown" };

            await db.Products.AddAsync(product);
            await db.Locations.AddAsync(location);
            await db.SaveChangesAsync();
        }

        [Fact]
        public async Task Test1_FIFO_Allocation_Success()
        {
            // Arrange
            using (var db = CreateInMemoryDatabaseContext())
            {
                await SeedBasicData(db);

                var batchA = new InventoryBatch
                {
                    BatchId = 1,
                    BatchNumber = "BAT-A",
                    ProductId = 1,
                    LocationId = 1,
                    InitialQuantity = 1000,
                    CurrentQuantity = 1000,
                    UnitCost = 5.00m,
                    ReceivedDate = DateTime.UtcNow.AddDays(-2),
                    Status = "FINALIZED"
                };

                var batchB = new InventoryBatch
                {
                    BatchId = 2,
                    BatchNumber = "BAT-B",
                    ProductId = 1,
                    LocationId = 1,
                    InitialQuantity = 2000,
                    CurrentQuantity = 2000,
                    UnitCost = 5.50m,
                    ReceivedDate = DateTime.UtcNow.AddDays(-1),
                    Status = "FINALIZED"
                };

                var customer = new Customer { CustomerId = 1, CustomerName = "Test", Phone = "123", Email = "a@a.com", Address = "Add" };
                var sale = new Sale { SaleId = 1, CustomerId = 1, SaleNumber = "S001", SaleDate = DateTime.UtcNow, PaymentStatus = "PAID", Status = "COMPLETED" };
                var saleDetail = new SaleDetail { SaleDetailId = 101, SaleId = 1, ProductId = 1, Quantity = 1500, UnitPrice = 10.00m, BatchId = 1 };

                await db.Customers.AddAsync(customer);
                await db.Sales.AddAsync(sale);
                await db.SaleDetails.AddAsync(saleDetail);
                await db.InventoryBatches.AddRangeAsync(batchA, batchB);

                // Add corresponding IN movements for authoritative physical calculation
                await db.InventoryMovements.AddRangeAsync(
                    new InventoryMovement { ProductId = 1, LocationId = 1, BatchId = 1, Direction = "IN", Quantity = 1000, MovementType = "PURCHASE_IN", CreatedBy = "SYSTEM", Description = "Inbound", ReferenceType = "SEED", ReferenceId = "1" },
                    new InventoryMovement { ProductId = 1, LocationId = 1, BatchId = 2, Direction = "IN", Quantity = 2000, MovementType = "PURCHASE_IN", CreatedBy = "SYSTEM", Description = "Inbound", ReferenceType = "SEED", ReferenceId = "2" }
                );
                await db.SaveChangesAsync();

                var service = new InventoryService(db);

                // Act - Allocate 1,500
                var allocations = await service.AllocateStockFIFOAsync(1, 1, 1500, 101, null, "TEST_USER");

                // Assert
                Assert.Equal(2, allocations.Count);
                
                var allocA = allocations.First(a => a.BatchId == 1);
                var allocB = allocations.First(a => a.BatchId == 2);

                Assert.Equal(1000, allocA.Quantity);
                Assert.Equal(5.00m, allocA.UnitCost);

                Assert.Equal(500, allocB.Quantity);
                Assert.Equal(5.50m, allocB.UnitCost);

                // Validate total Cost
                var Cost = allocations.Sum(a => a.TotalCost);
                Assert.Equal(7750m, Cost);

                // Verify projections updated
                Assert.Equal(0, batchA.CurrentQuantity);
                Assert.Equal(1500, batchB.CurrentQuantity);
            }
        }

        [Fact]
        public async Task Test2_Insufficient_Stock_Rolls_Back()
        {
            // Arrange
            using (var db = CreateInMemoryDatabaseContext())
            {
                await SeedBasicData(db);

                var batchA = new InventoryBatch
                {
                    BatchId = 1,
                    BatchNumber = "BAT-A",
                    ProductId = 1,
                    LocationId = 1,
                    InitialQuantity = 1000,
                    CurrentQuantity = 1000,
                    UnitCost = 5.00m,
                    ReceivedDate = DateTime.UtcNow,
                    Status = "FINALIZED"
                };
                await db.InventoryBatches.AddAsync(batchA);

                // Movement IN = 1000 (Physical = 1000)
                await db.InventoryMovements.AddAsync(
                    new InventoryMovement { ProductId = 1, LocationId = 1, BatchId = 1, Direction = "IN", Quantity = 1000, MovementType = "PURCHASE_IN", CreatedBy = "SYSTEM", Description = "Inbound", ReferenceType = "SEED", ReferenceId = "1" }
                );

                // Confirmed Order reservation = 300 (Available = 700)
                var customer = new Customer { CustomerId = 1, CustomerName = "Test Customer", Phone = "123456", Email = "a@a.com", Address = "Add" };
                var order = new Order { OrderId = 1, CustomerId = 1, OrderNo = "ORD001" };
                var orderDetail = new OrderDetail { OrderDetailId = 1, OrderId = 1, ProductId = 1, OrderedQuantity = 300 };
                var reservation = new StockReservation
                {
                    ReservationId = 1,
                    OrderDetailId = 1,
                    ProductId = 1,
                    LocationId = 1,
                    Quantity = 300,
                    Status = "ACTIVE"
                };
                
                await db.Customers.AddAsync(customer);
                await db.Orders.AddAsync(order);
                await db.OrderDetails.AddAsync(orderDetail);
                await db.StockReservations.AddAsync(reservation);
                await db.SaveChangesAsync();

                var service = new InventoryService(db);

                // Act & Assert - Request 800 (exceeds available 700)
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    service.AllocateStockFIFOAsync(1, 1, 800, 102, null, "TEST_USER")
                );

                // Verify nothing was recorded/changed
                Assert.Equal(1000, batchA.CurrentQuantity);
                Assert.Empty(db.InventoryAllocations.ToList());
                
                // Only 1 original IN movement should exist
                Assert.Single(db.InventoryMovements.ToList());
            }
        }

        [Fact]
        public async Task Test4_Rollback_Restores_Stock_Projections()
        {
            // Arrange
            using (var db = CreateInMemoryDatabaseContext())
            {
                await SeedBasicData(db);

                var batchA = new InventoryBatch
                {
                    BatchId = 1,
                    BatchNumber = "BAT-A",
                    ProductId = 1,
                    LocationId = 1,
                    InitialQuantity = 1000,
                    CurrentQuantity = 1000,
                    UnitCost = 5.00m,
                    ReceivedDate = DateTime.UtcNow,
                    Status = "FINALIZED"
                };
                var customer = new Customer { CustomerId = 1, CustomerName = "Test", Phone = "123", Email = "a@a.com", Address = "Add" };
                var sale = new Sale { SaleId = 1, CustomerId = 1, SaleNumber = "S001", SaleDate = DateTime.UtcNow, PaymentStatus = "PAID", Status = "COMPLETED" };
                var saleDetail = new SaleDetail { SaleDetailId = 103, SaleId = 1, ProductId = 1, Quantity = 400, UnitPrice = 10.00m, BatchId = 1 };

                await db.Customers.AddAsync(customer);
                await db.Sales.AddAsync(sale);
                await db.SaleDetails.AddAsync(saleDetail);
                await db.InventoryBatches.AddAsync(batchA);
                await db.InventoryMovements.AddAsync(
                    new InventoryMovement { ProductId = 1, LocationId = 1, BatchId = 1, Direction = "IN", Quantity = 1000, MovementType = "PURCHASE_IN", CreatedBy = "SYSTEM", Description = "Inbound", ReferenceType = "SEED", ReferenceId = "1" }
                );
                await db.SaveChangesAsync();

                var service = new InventoryService(db);

                // Force transaction simulation
                using (var transaction = await db.Database.BeginTransactionAsync())
                {
                    try
                    {
                        // 1. FIFO Allocate 400 (leaves 600)
                        await service.AllocateStockFIFOAsync(1, 1, 400, 103, null, "TEST_USER");
                        Assert.Equal(600, batchA.CurrentQuantity);

                        // 2. Force throw error before committing transaction
                        throw new Exception("Simulated mid-operation transaction failure");
                    }
                    catch (Exception)
                    {
                        // Rollback explicitly
                        await transaction.RollbackAsync();
                    }
                }

                // Assert batch cache was restored back to 1000 after transaction rollback
                var updatedBatch = await db.InventoryBatches.AsNoTracking().FirstOrDefaultAsync(b => b.BatchId == 1);
                Assert.Equal(1000, updatedBatch.CurrentQuantity);
                Assert.Empty(db.InventoryAllocations.ToList());
            }
        }

        [Fact]
        public async Task Test5_Transfer_Preserves_Cost_Layers()
        {
            // Arrange
            using (var db = CreateInMemoryDatabaseContext())
            {
                await SeedBasicData(db);
                var location2 = new Location { LocationId = 2, LocationName = "Secondary Godown" };
                await db.Locations.AddAsync(location2);

                var batchA = new InventoryBatch
                {
                    BatchId = 1,
                    BatchNumber = "BAT-A",
                    ProductId = 1,
                    LocationId = 1,
                    InitialQuantity = 500,
                    CurrentQuantity = 500,
                    UnitCost = 5.00m,
                    LandedUnitCost = 5.00m,
                    ReceivedDate = DateTime.UtcNow.AddDays(-2),
                    Status = "FINALIZED"
                };

                var batchB = new InventoryBatch
                {
                    BatchId = 2,
                    BatchNumber = "BAT-B",
                    ProductId = 1,
                    LocationId = 1,
                    InitialQuantity = 700,
                    CurrentQuantity = 700,
                    UnitCost = 5.50m,
                    LandedUnitCost = 5.50m,
                    ReceivedDate = DateTime.UtcNow.AddDays(-1),
                    Status = "FINALIZED"
                };

                await db.InventoryBatches.AddRangeAsync(batchA, batchB);
                await db.InventoryMovements.AddRangeAsync(
                    new InventoryMovement { ProductId = 1, LocationId = 1, BatchId = 1, Direction = "IN", Quantity = 500, MovementType = "PURCHASE_IN", CreatedBy = "SYSTEM", Description = "Inbound", ReferenceType = "SEED", ReferenceId = "1" },
                    new InventoryMovement { ProductId = 1, LocationId = 1, BatchId = 2, Direction = "IN", Quantity = 700, MovementType = "PURCHASE_IN", CreatedBy = "SYSTEM", Description = "Inbound", ReferenceType = "SEED", ReferenceId = "2" }
                );
                await db.SaveChangesAsync();

                var service = new InventoryService(db);

                // Act - Transfer 600 from Location 1 to Location 2
                await service.TransferStockAsync(1, 1, 2, 600, "STOCK_TRANSFER", "TRF001", "TEST_USER");

                // Assert Location 1 projection is updated
                Assert.Equal(0, batchA.CurrentQuantity);
                Assert.Equal(600, batchB.CurrentQuantity); // 700 - 100

                // Destination Location 2 cost layers verified
                var destinationBatches = db.InventoryBatches
                    .Where(b => b.LocationId == 2)
                    .ToList()
                    .OrderBy(b => b.UnitCost)
                    .ToList();

                Assert.Equal(2, destinationBatches.Count);
                
                var destA = destinationBatches.First(b => b.UnitCost == 5.00m);
                var destB = destinationBatches.First(b => b.UnitCost == 5.50m);

                Assert.Equal(500, destA.CurrentQuantity);
                Assert.Equal(100, destB.CurrentQuantity);
            }
        }
    }
}
