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

            // 3. Sync missing active sales & purchases to AccountTransactions if needed
            if (defaultAcc != null)
            {
                var currentSaleRefs = await Db.AccountTransactions
                    .Where(t => t.ReferenceType == "SALE")
                    .Select(t => t.ReferenceId)
                    .ToListAsync();

                var salesToAdd = validSales
                    .Where(s => s.PaidAmount > 0 && !currentSaleRefs.Contains(s.SaleId.ToString()) && !currentSaleRefs.Contains(s.SaleNumber))
                    .ToList();

                if (salesToAdd.Any())
                {
                    foreach (var s in salesToAdd)
                    {
                        Db.AccountTransactions.Add(new AccountTransaction
                        {
                            AccountId = defaultAcc.AccountId,
                            TransactionType = "CREDIT",
                            Amount = s.PaidAmount,
                            ReferenceType = "SALE",
                            ReferenceId = s.SaleId.ToString(),
                            Description = $"Collected payment for Sale {s.SaleNumber}",
                            CreatedBy = s.CreatedBy ?? "SYSTEM",
                            CreatedAt = s.SaleDate != default ? s.SaleDate : s.CreatedAt
                        });
                    }
                    await Db.SaveChangesAsync();
                }

            }

            return await Db.AccountTransactions
                .Include(t => t.Account)
                .ToListAsync();
        }

        public async Task AddTransactionAsync(AccountTransaction tx) =>
            await Db.AccountTransactions.AddAsync(tx);

        public async Task<BusinessAccount> GetByNameAsync(string name)
        {
            return await Db.BusinessAccounts
                .FirstOrDefaultAsync(a => a.AccountName.ToLower() == name.ToLower());
        }

        public async Task AddAuditLogAsync(AuditLog log) =>
            await Db.AuditLogs.AddAsync(log);

        public async Task<IEnumerable<AuditLog>> GetAuditLogsAsync()
        {
            return await Db.AuditLogs.ToListAsync();
        }
    }
}
