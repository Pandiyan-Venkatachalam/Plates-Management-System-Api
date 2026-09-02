using System.Collections.Generic;
using System.Threading.Tasks;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Application.Repositories
{
    public interface ISupplierRepository : IBaseRepository<Supplier>
    {
        Task<IEnumerable<Supplier>> GetSuppliersAsync();
        Task AddSupplierAsync(Supplier supplier);
        Task<Supplier> GetByIdAsync(int id);
        void Update(Supplier supplier);
        void Delete(Supplier supplier);
    }
}
