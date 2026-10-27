using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VinayagaPlates.Application;
using VinayagaPlates.Application.Repositories;
using VinayagaPlates.Application.Services;
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
            return await Db.AccountTransactions
                .Include(t => t.Account)
                .AsNoTracking()
                .OrderByDescending(t => t.CreatedAt)
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
                ?? all.FirstOrDefault(a => !string.IsNullOrEmpty(a.AccountType) && (a.AccountType.ToLower().Contains(trimmed) || trimmed.Contains(a.AccountType.ToLower())))
                ?? (trimmed == "cash" || trimmed == "cash in hand" || trimmed == "default" ? all.FirstOrDefault() : null);
        }

        public async Task AddAuditLogAsync(AuditLog log) =>
            await Db.AuditLogs.AddAsync(log);

        public async Task<IEnumerable<AuditLog>> GetAuditLogsAsync()
        {
            return await Db.AuditLogs.ToListAsync();
        }
    }
}
