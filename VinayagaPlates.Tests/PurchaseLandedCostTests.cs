using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VinayagaPlates.Application;
using VinayagaPlates.Application.Services;
using VinayagaPlates.Contracts.DTOs;
using VinayagaPlates.Domain.Entities;
using Xunit;

namespace VinayagaPlates.Tests
{
    public class PurchaseLandedCostTests
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

        private async Task SeedBaseData(ApplicationDbContext db)
        {
            var cat = new ProductCategory { CategoryId = 1, CategoryName = "Plates" };
            var v = new ProductVariant { VariantId = 1, VariantName = "Round" };
            var u = new ProductUnit { UnitId = 1, UnitName = "Pieces" };
            await db.ProductCategories.AddRangeAsync(cat);
            await db.ProductVariants.AddRangeAsync(v);
            await db.ProductUnits.AddRangeAsync(u);

            var p1 = new Product { ProductId = 1, ProductCode = "P001", ProductName = "10 inch Plate", CategoryId = 1, VariantId = 1, UnitId = 1 };
            var p2 = new Product { ProductId = 2, ProductCode = "P002", ProductName = "12 inch Plate", CategoryId = 1, VariantId = 1, UnitId = 1 };
            var supplier = new Supplier { SupplierId = 1, SupplierName = "Supplier A", Phone = "123", Email = "sup@a.com", Address = "Add", ContactPerson = "Person" };
            var location = new Location { LocationId = 1, LocationName = "Main Godown" };
            var account = new BusinessAccount { AccountId = 1, AccountName = "Cash Account", AccountType = "CASH" };

            await db.Products.AddRangeAsync(p1, p2);
            await db.Suppliers.AddAsync(supplier);
            await db.Locations.AddAsync(location);
            await db.BusinessAccounts.AddAsync(account);
            await db.SaveChangesAsync();
        }

        private class MockPurchaseRepository : VinayagaPlates.Application.Repositories.IPurchaseRepository
        {
            private readonly ApplicationDbContext _db;
            public MockPurchaseRepository(ApplicationDbContext db) => _db = db;
            public async Task AddAsync(Purchase entity) => await _db.Purchases.AddAsync(entity);
            public async Task AddBatchAsync(InventoryBatch batch) => await _db.InventoryBatches.AddAsync(batch);
            public async Task AddMovementAsync(InventoryMovement movement) => await _db.InventoryMovements.AddAsync(movement);
            public async Task SaveChangesAsync() => await _db.SaveChangesAsync();
            public void Update(Purchase entity) { }
            public void Delete(Purchase entity) { }
            public Task<IEnumerable<Purchase>> GetAllAsync() => Task.FromResult(Enumerable.Empty<Purchase>());
            public Task<Purchase> GetByIdAsync(int id) => Task.FromResult<Purchase>(null);
            public Task<IEnumerable<Supplier>> GetSuppliersAsync() => Task.FromResult(Enumerable.Empty<Supplier>());
            public Task AddSupplierAsync(Supplier supplier) => Task.CompletedTask;
            public Task<IEnumerable<Purchase>> GetPurchasesWithDetailsAsync() => Task.FromResult(Enumerable.Empty<Purchase>());
            public Task<IEnumerable<InventoryBatch>> GetBatchesAsync() => Task.FromResult(Enumerable.Empty<InventoryBatch>());
            public Task<InventoryBatch> GetBatchByIdAsync(int id) => Task.FromResult<InventoryBatch>(null);
            public void UpdateBatch(InventoryBatch batch) { }
        }

        private class MockAccountRepository : VinayagaPlates.Application.Repositories.IAccountRepository
        {
            private readonly ApplicationDbContext _db;
            public MockAccountRepository(ApplicationDbContext db) => _db = db;
            public async Task AddTransactionAsync(AccountTransaction tx) => await _db.AccountTransactions.AddAsync(tx);
            public async Task<BusinessAccount> GetByNameAsync(string name) => await _db.BusinessAccounts.FirstOrDefaultAsync(a => a.AccountName == name);
            public Task AddAsync(BusinessAccount entity) => Task.CompletedTask;
            public void Update(BusinessAccount entity) { }
            public void Delete(BusinessAccount entity) { }
            public Task<IEnumerable<BusinessAccount>> GetAllAsync() => Task.FromResult(Enumerable.Empty<BusinessAccount>());
            public Task<BusinessAccount> GetByIdAsync(int id) => Task.FromResult<BusinessAccount>(null);
            public Task SaveChangesAsync() => _db.SaveChangesAsync();
            public Task<IEnumerable<AccountTransaction>> GetTransactionsAsync() => Task.FromResult(Enumerable.Empty<AccountTransaction>());
            public Task AddAuditLogAsync(AuditLog log) => Task.CompletedTask;
            public Task<IEnumerable<AuditLog>> GetAuditLogsAsync() => Task.FromResult(Enumerable.Empty<AuditLog>());
        }

        [Fact]
        public async Task Test_Quantity_Based_Landed_Cost()
        {
            using (var db = CreateInMemoryDatabaseContext())
            {
                await SeedBaseData(db);

                // Setup repositories and service
                var purchaseRepo = new MockPurchaseRepository(db);
                var accountRepo = new MockAccountRepository(db);
                var service = new VpmsService(
                    null, null, purchaseRepo, null, null, accountRepo, null, db);

                // Purchase 100 of P1 @ ₹10.00 and 300 of P2 @ ₹20.00 (Total Qty = 400)
                // Expense = ₹400.00 (Quantity Based) -> Expect ₹1.00 allocated per unit
                var req = new PurchaseCreateRequest(
                    SupplierId: 1,
                    PurchaseDate: DateTime.UtcNow,
                    Details: new List<PurchaseDetailRequest>
                    {
                        new PurchaseDetailRequest(1, 100, 10.00m),
                        new PurchaseDetailRequest(2, 300, 20.00m)
                    },
                    Expenses: new List<PurchaseExpenseRequest>
                    {
                        new PurchaseExpenseRequest(ExpenseTypeId: 1, Amount: 400.00m, AllocationMethod: "QUANTITY_BASED", Description: "Transport")
                    },
                    PaidAmount: 7400.00m,
                    PaymentMethodAccountName: "Cash Account"
                );

                // Act
                var purchase = await service.CreatePurchaseAsync(req, "TEST_USER");

                // Assert Total Purchase includes expenses
                // 100 * 10 + 300 * 20 = 7000 + 400 = 7400
                Assert.Equal(7400.00m, purchase.TotalAmount);

                // Verify batches landed costs
                var batch1 = db.InventoryBatches.First(b => b.ProductId == 1);
                var batch2 = db.InventoryBatches.First(b => b.ProductId == 2);

                // Landed cost details: P1 should have 10 + 1 = 11
                Assert.Equal(11.00m, batch1.LandedUnitCost);
                // P2 should have 20 + 1 = 21
                Assert.Equal(21.00m, batch2.LandedUnitCost);

                // Verify authoritative movement records
                var mov1 = db.InventoryMovements.First(m => m.BatchId == batch1.BatchId);
                Assert.Equal(11.00m, mov1.UnitCost);
                Assert.Equal(1100.00m, mov1.TotalCost);
            }
        }

        [Fact]
        public async Task Test_Value_Based_Landed_Cost()
        {
            using (var db = CreateInMemoryDatabaseContext())
            {
                await SeedBaseData(db);

                var purchaseRepo = new MockPurchaseRepository(db);
                var accountRepo = new MockAccountRepository(db);
                var service = new VpmsService(
                    null, null, purchaseRepo, null, null, accountRepo, null, db);

                // P1 = 100 units @ ₹10 = ₹1,000
                // P2 = 100 units @ ₹30 = ₹3,000
                // Total Value = ₹4,000
                // Expense = ₹800.00 (Value Based) -> P1 gets 25% (₹200 -> ₹2.00/unit), P2 gets 75% (₹600 -> ₹6.00/unit)
                var req = new PurchaseCreateRequest(
                    SupplierId: 1,
                    PurchaseDate: DateTime.UtcNow,
                    Details: new List<PurchaseDetailRequest>
                    {
                        new PurchaseDetailRequest(1, 100, 10.00m),
                        new PurchaseDetailRequest(2, 100, 30.00m)
                    },
                    Expenses: new List<PurchaseExpenseRequest>
                    {
                        new PurchaseExpenseRequest(ExpenseTypeId: 1, Amount: 800.00m, AllocationMethod: "VALUE_BASED", Description: "Handling")
                    },
                    PaidAmount: 4800.00m,
                    PaymentMethodAccountName: "Cash Account"
                );

                // Act
                var purchase = await service.CreatePurchaseAsync(req, "TEST_USER");

                // Assert
                Assert.Equal(4800.00m, purchase.TotalAmount);

                var batch1 = db.InventoryBatches.First(b => b.ProductId == 1);
                var batch2 = db.InventoryBatches.First(b => b.ProductId == 2);

                Assert.Equal(12.00m, batch1.LandedUnitCost); // 10 + 2
                Assert.Equal(36.00m, batch2.LandedUnitCost); // 30 + 6
            }
        }
    }
}
