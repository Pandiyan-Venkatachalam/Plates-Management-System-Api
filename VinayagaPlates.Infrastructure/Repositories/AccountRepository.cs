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
                allTx = await Db.AccountTransactions.ToListAsync();
            }

            // 3. Backfill any unlinked PartnerLedger entries
            var existingPartnerTxs = allTx.Where(t => t.ReferenceType == "PARTNER_TRANSACTION").ToList();
            var allLedgers = await Db.PartnerLedgers.Include(l => l.Partner).ToListAsync();
            var businessAccounts = await Db.BusinessAccounts.ToListAsync();
            var defaultAccount = businessAccounts.FirstOrDefault();

            var newPartnerTxs = new List<AccountTransaction>();
            foreach (var pl in allLedgers)
            {
                var refId = $"LEDGER-{pl.LedgerId}";
                var hasTx = existingPartnerTxs.Any(t => t.ReferenceId == refId || (t.Amount == pl.Amount && t.Description != null && !string.IsNullOrEmpty(pl.Description) && t.Description.Contains(pl.Description)));
                if (!hasTx)
                {
                    var pName = pl.Partner?.PartnerName?.Trim().ToLower() ?? "";
                    var matchedAcc = businessAccounts.FirstOrDefault(a => 
                        (!string.IsNullOrEmpty(pName) && (a.AccountName.ToLower().Contains(pName) || pName.Contains(a.AccountName.ToLower()))) ||
                        (!string.IsNullOrEmpty(a.AccountType) && (a.AccountType.ToLower().Contains(pName) || pName.Contains(a.AccountType.ToLower())))
                    ) ?? defaultAccount;

                    if (matchedAcc != null)
                    {
                        var newTx = new AccountTransaction
                        {
                            AccountId = matchedAcc.AccountId,
                            TransactionType = pl.TransactionType == "INVESTMENT" ? "CREDIT" : "DEBIT",
                            Amount = pl.Amount,
                            ReferenceType = "PARTNER_TRANSACTION",
                            ReferenceId = refId,
                            Description = $"{pl.TransactionType} by Partner. Details: {pl.Description}",
                            CreatedBy = pl.CreatedBy ?? "SYSTEM",
                            CreatedAt = pl.CreatedAt
                        };
                        newPartnerTxs.Add(newTx);
                    }
                }
            }

            if (newPartnerTxs.Any())
            {
                await Db.AccountTransactions.AddRangeAsync(newPartnerTxs);
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
            var cleanId = trimmed.Replace("acct-", "").Replace("acct", "").Trim();
            var all = await Db.BusinessAccounts.ToListAsync();

            return all.FirstOrDefault(a => a.AccountId.ToString() == cleanId)
                ?? all.FirstOrDefault(a => a.AccountName.Trim().ToLower() == trimmed)
                ?? all.FirstOrDefault(a => !string.IsNullOrEmpty(a.AccountType) && a.AccountType.Trim().ToLower() == trimmed)
                ?? all.FirstOrDefault(a => a.AccountName.ToLower().Contains(trimmed) || trimmed.Contains(a.AccountName.ToLower()))
                ?? all.FirstOrDefault(a => !string.IsNullOrEmpty(a.AccountType) && (a.AccountType.ToLower().Contains(trimmed) || trimmed.Contains(a.AccountType.ToLower())));
        }

        public async Task AddAuditLogAsync(AuditLog log) =>
            await Db.AuditLogs.AddAsync(log);

        public async Task<IEnumerable<AuditLog>> GetAuditLogsAsync()
        {
            return await Db.AuditLogs.ToListAsync();
        }
    }
}
