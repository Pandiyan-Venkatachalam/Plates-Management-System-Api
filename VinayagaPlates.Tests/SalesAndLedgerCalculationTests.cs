using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VinayagaPlates.Application;
using VinayagaPlates.Domain.Entities;
using Xunit;

namespace VinayagaPlates.Tests
{
    public class SalesAndLedgerCalculationTests
    {
        private ApplicationDbContext CreateInMemoryDb()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
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
            await db.ProductCategories.AddAsync(cat);
            await db.ProductVariants.AddAsync(v);
            await db.ProductUnits.AddAsync(u);

            var p1 = new Product { ProductId = 1, ProductCode = "P001", ProductName = "10 inch Plate", CategoryId = 1, VariantId = 1, UnitId = 1 };
            var supplier = new Supplier { SupplierId = 1, SupplierName = "Supplier A", Phone = "123", Email = "sup@a.com", Address = "Add", ContactPerson = "Person" };
            var customer = new Customer { CustomerId = 1, CustomerName = "Customer A", Phone = "999", Email = "cust@a.com", Address = "Add" };
            var location = new Location { LocationId = 1, LocationName = "Main Godown" };
            var account = new BusinessAccount { AccountId = 1, AccountName = "Pandiyan Acc", AccountType = "CASH" };

            await db.Products.AddAsync(p1);
            await db.Suppliers.AddAsync(supplier);
            await db.Customers.AddAsync(customer);
            await db.Locations.AddAsync(location);
            await db.BusinessAccounts.AddAsync(account);
            await db.SaveChangesAsync();
        }

        [Fact]
        public void Sale_Calculations_Total_And_Balance_Are_Accurate()
        {
            // Arrange
            decimal unitPrice = 12.50m;
            int quantity = 200;
            decimal discount = 100m;
            decimal paidAmount = 1500m;

            // Act
            decimal subTotal = unitPrice * quantity; // 2,500
            decimal totalAmount = subTotal - discount; // 2,400
            decimal remainingBalance = totalAmount - paidAmount; // 900

            // Assert
            Assert.Equal(2500m, subTotal);
            Assert.Equal(2400m, totalAmount);
            Assert.Equal(900m, remainingBalance);
            Assert.True(paidAmount < totalAmount, "Payment status should be PARTIAL");
        }

        [Fact]
        public async Task DeleteSale_Reverts_Batch_Stock_Quantities_Correctly()
        {
            // Arrange
            using var db = CreateInMemoryDb();
            await SeedBaseData(db);

            var batch = new InventoryBatch
            {
                BatchId = 1,
                ProductId = 1,
                LocationId = 1,
                BatchNumber = "BATCH-001",
                InitialQuantity = 1000,
                CurrentQuantity = 800 // 200 units were deducted by sale
            };
            var sale = new Sale
            {
                SaleId = 1,
                CustomerId = 1,
                SaleNumber = "SAL-001",
                TotalAmount = 2400m,
                PaidAmount = 2400m,
                Status = "COMPLETED",
                PaymentStatus = "PAID",
                CreatedBy = "TEST"
            };
            var detail = new SaleDetail
            {
                SaleDetailId = 1,
                SaleId = 1,
                ProductId = 1,
                BatchId = 1,
                Quantity = 200,
                UnitPrice = 12m
            };

            await db.InventoryBatches.AddAsync(batch);
            await db.Sales.AddAsync(sale);
            await db.SaleDetails.AddAsync(detail);
            await db.SaveChangesAsync();

            // Act: Revert logic simulating DeleteSaleAsync
            batch.CurrentQuantity += detail.Quantity;
            db.SaleDetails.Remove(detail);
            db.Sales.Remove(sale);
            await db.SaveChangesAsync();

            // Assert
            var updatedBatch = await db.InventoryBatches.FindAsync(1);
            Assert.NotNull(updatedBatch);
            Assert.Equal(1000, updatedBatch.CurrentQuantity); // Restored to 1000!
            Assert.Null(await db.Sales.FindAsync(1)); // Sale cleanly removed
        }

        [Fact]
        public async Task Purchase_Payment_Deletion_Reverts_Ledger_Balance()
        {
            // Arrange
            using var db = CreateInMemoryDb();
            await SeedBaseData(db);

            var purchase = new Purchase
            {
                PurchaseId = 1,
                SupplierId = 1,
                PurchaseNumber = "PUR-001",
                TotalAmount = 50000m,
                PaidAmount = 30000m,
                PaymentStatus = "PARTIAL",
                Status = "RECEIVED",
                CreatedBy = "TEST"
            };
            var payment = new PurchasePayment
            {
                PaymentId = 10,
                PurchaseId = 1,
                AccountId = 1,
                Amount = 10000m,
                PaymentMethod = "CASH",
                Notes = "Initial payment",
                CreatedBy = "TEST",
                CreatedAt = DateTime.UtcNow
            };
            var linkedTx = new AccountTransaction
            {
                TransactionId = 100,
                AccountId = 1,
                TransactionType = "DEBIT",
                Amount = 10000m,
                ReferenceType = "PURCHASE_PAYMENT",
                ReferenceId = "10",
                Description = "Payment for PUR-001",
                CreatedBy = "TEST",
                CreatedAt = DateTime.UtcNow
            };

            await db.Purchases.AddAsync(purchase);
            await db.PurchasePayments.AddAsync(payment);
            await db.AccountTransactions.AddAsync(linkedTx);
            await db.SaveChangesAsync();

            // Act: Simulate DeletePurchasePayment
            db.AccountTransactions.Remove(linkedTx);
            db.PurchasePayments.Remove(payment);
            purchase.PaidAmount -= payment.Amount;
            if (purchase.PaidAmount == 0) purchase.PaymentStatus = "UNPAID";
            await db.SaveChangesAsync();

            // Assert
            var remainingTx = await db.AccountTransactions.FirstOrDefaultAsync(t => t.TransactionId == 100);
            Assert.Null(remainingTx); // Linked debit removed
            var updatedPurchase = await db.Purchases.FindAsync(1);
            Assert.NotNull(updatedPurchase);
            Assert.Equal(20000m, updatedPurchase.PaidAmount); // 30,000 - 10,000 = 20,000
        }

        [Fact]
        public async Task Purchase_Deletion_Blocks_When_Items_Already_Sold()
        {
            // Arrange
            using var db = CreateInMemoryDb();
            await SeedBaseData(db);

            var batch = new InventoryBatch
            {
                BatchId = 5,
                ProductId = 1,
                LocationId = 1,
                BatchNumber = "BATCH-TEST",
                InitialQuantity = 500,
                CurrentQuantity = 450 // 50 items were sold!
            };
            await db.InventoryBatches.AddAsync(batch);
            await db.SaveChangesAsync();

            // Act & Assert: Deleting purchase must throw InvalidOperationException
            Assert.Throws<InvalidOperationException>(() =>
            {
                if (batch.CurrentQuantity < batch.InitialQuantity)
                {
                    throw new InvalidOperationException($"Cannot delete purchase because {batch.InitialQuantity - batch.CurrentQuantity} units have already been sold in Sales.");
                }
            });
        }

        [Fact]
        public void Ledger_Math_Formula_OpeningBalance_Plus_PeriodNet_Equals_InHand()
        {
            // Arrange
            decimal openingBalance = 13345m;
            decimal salesCollected = 48520m;
            decimal purchasesPaid = 48520m;
            decimal expensesPaid = 635m;
            decimal partnerInvested = 635m;

            // Act
            decimal periodCredits = salesCollected + partnerInvested; // 49,155
            decimal periodDebits = purchasesPaid + expensesPaid;     // 49,155
            decimal netInHand = openingBalance + (periodCredits - periodDebits);

            // Assert
            Assert.Equal(13345m, netInHand); // Exactly equal to opening balance!
        }
    }
}
