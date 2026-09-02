using System.Collections.Generic;
using System.Threading.Tasks;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Application.Repositories
{
    public interface IExpenseRepository : IBaseRepository<AccountTransaction>
    {
        Task<IEnumerable<AccountTransaction>> GetExpensesOnlyAsync();
        Task<AccountTransaction> GetExpenseByIdAsync(int id);
        Task<List<AccountTransaction>> CreateExpenseAsync(string description, decimal amount, int accountId, string createdBy, List<VinayagaPlates.Contracts.DTOs.ExpenseContributionRequest>? contributions = null);
        Task<AccountTransaction> UpdateExpenseAsync(int id, string description, decimal amount, int accountId);
        Task<bool> DeleteExpenseAsync(int id);
        Task<AccountTransaction> GetByIdAsync(int id);
        void Update(AccountTransaction tx);
        void Delete(AccountTransaction tx);
    }
}
