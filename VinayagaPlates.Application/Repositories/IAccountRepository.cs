using System.Collections.Generic;
using System.Threading.Tasks;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Application.Repositories
{
    public interface IAccountRepository : IBaseRepository<BusinessAccount>
    {
        Task<IEnumerable<AccountTransaction>> GetTransactionsAsync();
        Task AddTransactionAsync(AccountTransaction tx);
        Task<BusinessAccount> GetByNameAsync(string name);
        Task AddAuditLogAsync(AuditLog log);
        Task<IEnumerable<AuditLog>> GetAuditLogsAsync();
        Task<BusinessAccount> GetByIdAsync(int id);
        void Update(BusinessAccount account);
        void Delete(BusinessAccount account);
    }
}
