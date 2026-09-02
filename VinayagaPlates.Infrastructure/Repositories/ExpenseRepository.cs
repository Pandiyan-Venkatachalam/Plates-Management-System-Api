using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VinayagaPlates.Application;
using VinayagaPlates.Application.Repositories;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Infrastructure.Repositories
{
    public class ExpenseRepository : BaseRepository<AccountTransaction>, IExpenseRepository
    {
        public ExpenseRepository(ApplicationDbContext db) : base(db)
        {
        }

        public async Task<IEnumerable<AccountTransaction>> GetExpensesOnlyAsync()
        {
            return await Db.AccountTransactions
                .Where(t => t.ReferenceType == "EXPENSE")
                .Include(t => t.Account)
                .ToListAsync();
        }

        public async Task<AccountTransaction> GetExpenseByIdAsync(int id)
        {
            return await Db.AccountTransactions
                .Include(t => t.Account)
                .FirstOrDefaultAsync(t => t.TransactionId == id && t.ReferenceType == "EXPENSE");
        }

        public async Task<List<AccountTransaction>> CreateExpenseAsync(string description, decimal amount, int accountId, string createdBy, List<VinayagaPlates.Contracts.DTOs.ExpenseContributionRequest>? contributions = null)
        {
            var resultList = new List<AccountTransaction>();
            var sharedRefId = "EXP-" + DateTime.UtcNow.Ticks;

            if (contributions != null && contributions.Any(c => c.Amount > 0))
            {
                var totalContrib = contributions.Where(c => c.Amount > 0).Sum(c => c.Amount);
                if (Math.Abs(totalContrib - amount) > 0.01m)
                {
                    throw new ArgumentException($"The sum of expense contributions (₹{totalContrib:N2}) must equal the expense amount (₹{amount:N2}).");
                }

                foreach (var c in contributions.Where(c => c.Amount > 0))
                {
                    var acc = await Db.BusinessAccounts.FindAsync(c.AccountId);
                    if (acc == null)
                        throw new ArgumentException($"Business account with ID {c.AccountId} not found.");

                    // In-Hand Balance Check
                    var txs = await Db.AccountTransactions.Where(t => t.AccountId == acc.AccountId).ToListAsync();
                    var credits = txs.Where(t => t.TransactionType == "CREDIT").Sum(t => t.Amount);
                    var debits = txs.Where(t => t.TransactionType == "DEBIT").Sum(t => t.Amount);
                    var available = credits - debits;

                    if (c.Amount > available)
                    {
                        throw new ArgumentException($"Insufficient funds in {acc.AccountName}. Available Cash In-Hand is ₹{available:N2}, but attempted to spend ₹{c.Amount:N2}. Please record a partner investment first.");
                    }

                    var expenseTx = new AccountTransaction
                    {
                        AccountId = acc.AccountId,
                        TransactionType = "DEBIT",
                        Amount = c.Amount,
                        ReferenceType = "EXPENSE",
                        ReferenceId = sharedRefId,
                        Description = description,
                        CreatedBy = createdBy,
                        CreatedAt = DateTime.UtcNow
                    };

                    await Db.AccountTransactions.AddAsync(expenseTx);
                    resultList.Add(expenseTx);
                }
            }
            else
            {
                var acc = await Db.BusinessAccounts.FindAsync(accountId);
                if (acc == null)
                    throw new ArgumentException("Business account not found.");

                // In-Hand Balance Check
                var txs = await Db.AccountTransactions.Where(t => t.AccountId == acc.AccountId).ToListAsync();
                var credits = txs.Where(t => t.TransactionType == "CREDIT").Sum(t => t.Amount);
                var debits = txs.Where(t => t.TransactionType == "DEBIT").Sum(t => t.Amount);
                var available = credits - debits;

                if (amount > available)
                {
                    throw new ArgumentException($"Insufficient funds in {acc.AccountName}. Available Cash In-Hand is ₹{available:N2}, but attempted to spend ₹{amount:N2}. Please record a partner investment first.");
                }

                var expenseTx = new AccountTransaction
                {
                    AccountId = acc.AccountId,
                    TransactionType = "DEBIT",
                    Amount = amount,
                    ReferenceType = "EXPENSE",
                    ReferenceId = sharedRefId,
                    Description = description,
                    CreatedBy = createdBy,
                    CreatedAt = DateTime.UtcNow
                };

                await Db.AccountTransactions.AddAsync(expenseTx);
                resultList.Add(expenseTx);
            }

            await Db.SaveChangesAsync();
            return resultList;
        }

        public async Task<AccountTransaction> UpdateExpenseAsync(int id, string description, decimal amount, int accountId)
        {
            var expense = await GetExpenseByIdAsync(id);
            if (expense == null)
                throw new ArgumentException("Expense not found.");

            var acc = await Db.BusinessAccounts.FindAsync(accountId);
            if (acc == null)
                throw new ArgumentException("Business account not found.");

            // In-Hand Balance Check (adjusting for old amount)
            var txs = await Db.AccountTransactions.Where(t => t.AccountId == acc.AccountId).ToListAsync();
            var credits = txs.Where(t => t.TransactionType == "CREDIT").Sum(t => t.Amount);
            var debits = txs.Where(t => t.TransactionType == "DEBIT" && t.TransactionId != id).Sum(t => t.Amount);
            var available = credits - debits;

            if (amount > available)
            {
                throw new ArgumentException($"Insufficient funds in {acc.AccountName}. Available Cash In-Hand is ₹{available:N2}, but attempted to spend ₹{amount:N2}. Please record a partner investment first.");
            }

            expense.AccountId = accountId;
            expense.Amount = amount;
            expense.Description = description;

            Db.AccountTransactions.Update(expense);
            await Db.SaveChangesAsync();
            return expense;
        }

        public async Task<bool> DeleteExpenseAsync(int id)
        {
            var expense = await GetExpenseByIdAsync(id);
            if (expense == null)
                return false;

             Db.AccountTransactions.Remove(expense);
            await Db.SaveChangesAsync();
            return true;
        }
    }
}
