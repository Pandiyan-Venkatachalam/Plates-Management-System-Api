using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VinayagaPlates.Application;
using VinayagaPlates.Application.Repositories;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Infrastructure.Repositories
{
    public class AccountRepository : BaseRepository<BusinessAccount>, IAccountRepository
    {
        public AccountRepository(ApplicationDbContext db) : base(db)
        {
        }

        public async Task<IEnumerable<AccountTransaction>> GetTransactionsAsync()
        {
            var defaultAcc = await Db.BusinessAccounts.FirstOrDefaultAsync();

            // 1. Fetch valid active entities
            var validSales = await Db.Sales.ToListAsync();
            var validSaleIds = validSales.Select(s => s.SaleId.ToString()).ToHashSet();
            var validSaleNumbers = validSales.Where(s => !string.IsNullOrEmpty(s.SaleNumber)).Select(s => s.SaleNumber).ToHashSet();

            var validPurchases = await Db.Purchases.ToListAsync();
            var validPurchaseIds = validPurchases.Select(p => p.PurchaseId.ToString()).ToHashSet();
            var validPurchaseNumbers = validPurchases.Where(p => !string.IsNullOrEmpty(p.PurchaseNumber)).Select(p => p.PurchaseNumber).ToHashSet();

            var validLedgerIds = (await Db.PartnerLedgers.Select(l => l.LedgerId).ToListAsync())
                .Select(id => $"LEDGER-{id}").ToHashSet();

            // 2. Find and delete orphaned AccountTransactions
            var allTx = await Db.AccountTransactions.ToListAsync();
            var orphanedTx = new List<AccountTransaction>();

            foreach (var t in allTx)
            {
                if (t.ReferenceType == "SALE")
                {
                    if (!validSaleIds.Contains(t.ReferenceId) && !validSaleNumbers.Contains(t.ReferenceId))
                    {
                        orphanedTx.Add(t);
                    }
                }
                else if (t.ReferenceType == "PURCHASE")
                {
                    if (!validPurchaseIds.Contains(t.ReferenceId) && !validPurchaseNumbers.Contains(t.ReferenceId))
                    {
                        orphanedTx.Add(t);
                    }
                }
                else if (t.ReferenceType == "PARTNER_TRANSACTION")
                {
                    if (t.ReferenceId != null && t.ReferenceId.StartsWith("LEDGER-") && !validLedgerIds.Contains(t.ReferenceId))
                    {
                        orphanedTx.Add(t);
                    }
                }
            }

            if (orphanedTx.Any())
            {
                Db.AccountTransactions.RemoveRange(orphanedTx);
                await Db.SaveChangesAsync();
            }

            return await Db.AccountTransactions
                .Include(t => t.Account)
                .ToListAsync();
        }

        public async Task AddTransactionAsync(AccountTransaction tx) =>
            await Db.AccountTransactions.AddAsync(tx);

        public async Task<BusinessAccount> GetByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var trimmed = name.Trim().ToLower();
            var all = await Db.BusinessAccounts.ToListAsync();
            return all.FirstOrDefault(a => a.AccountName.Trim().ToLower() == trimmed)
                ?? all.FirstOrDefault(a => a.AccountName.ToLower().Contains(trimmed) || trimmed.Contains(a.AccountName.ToLower()))
                ?? all.FirstOrDefault(a => !string.IsNullOrEmpty(a.AccountType) && a.AccountType.ToLower() == trimmed)
                ?? all.FirstOrDefault(a => a.AccountId.ToString() == trimmed);
        }

        public async Task AddAuditLogAsync(AuditLog log) =>
            await Db.AuditLogs.AddAsync(log);

        public async Task<IEnumerable<AuditLog>> GetAuditLogsAsync()
        {
            return await Db.AuditLogs.ToListAsync();
        }
    }
}
