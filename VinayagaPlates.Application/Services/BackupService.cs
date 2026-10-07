using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace VinayagaPlates.Application.Services
{
    public class BackupService : IBackupService
    {
        private readonly ApplicationDbContext _context;

        public BackupService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<byte[]> GenerateJsonBackupAsync()
        {
            var backupData = new
            {
                ExportDate = DateTime.UtcNow,
                Data = new
                {
                    Users = await _context.Users.AsNoTracking().ToListAsync(),
                    Roles = await _context.Roles.AsNoTracking().ToListAsync(),
                    Partner = await _context.Partner.AsNoTracking().ToListAsync(),
                    BusinessAccounts = await _context.BusinessAccounts.AsNoTracking().ToListAsync(),
                    AccountTransactions = await _context.AccountTransactions.AsNoTracking().ToListAsync(),
                    PartnerLedgers = await _context.PartnerLedgers.AsNoTracking().ToListAsync(),
                    Products = await _context.Products.AsNoTracking().ToListAsync(),
                    ProductCategories = await _context.ProductCategories.AsNoTracking().ToListAsync(),
                    InventoryBatches = await _context.InventoryBatches.AsNoTracking().ToListAsync(),
                    InventoryMovements = await _context.InventoryMovements.AsNoTracking().ToListAsync(),
                    Purchases = await _context.Purchases.AsNoTracking().ToListAsync(),
                    PurchaseDetails = await _context.PurchaseDetails.AsNoTracking().ToListAsync(),
                    Sales = await _context.Sales.AsNoTracking().ToListAsync(),
                    SaleDetails = await _context.SaleDetails.AsNoTracking().ToListAsync(),
                    Suppliers = await _context.Suppliers.AsNoTracking().ToListAsync(),
                    Customers = await _context.Customers.AsNoTracking().ToListAsync(),
                    CustomerPricings = await _context.CustomerPricings.AsNoTracking().ToListAsync(),
                    AuditLogs = await _context.AuditLogs.AsNoTracking().ToListAsync()
                }
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
            };

            var jsonString = JsonSerializer.Serialize(backupData, options);
            return Encoding.UTF8.GetBytes(jsonString);
        }
    }
}
