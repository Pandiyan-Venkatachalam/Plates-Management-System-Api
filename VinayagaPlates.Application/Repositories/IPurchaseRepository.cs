using System.Collections.Generic;
using System.Threading.Tasks;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Application.Repositories
{
    public interface IPurchaseRepository : IBaseRepository<Purchase>
    {
        Task<IEnumerable<Purchase>> GetPurchasesWithDetailsAsync();
        Task<Purchase> GetPurchaseWithDetailsByIdAsync(int id);
        Task<Purchase> GetByIdAsync(int id);
        void Update(Purchase purchase);
        void Delete(Purchase purchase);
    }
}
