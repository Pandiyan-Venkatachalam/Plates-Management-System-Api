using System.Collections.Generic;
using System.Threading.Tasks;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Application.Repositories
{
    public interface ISalesRepository : IBaseRepository<Sale>
    {
        Task<IEnumerable<Sale>> GetSalesWithDetailsAsync();
        Task<Sale?> GetSaleWithDetailsByIdAsync(int id);
        Task<Sale> GetByIdAsync(int id);
        void Update(Sale sale);
        void Delete(Sale sale);
    }
}
