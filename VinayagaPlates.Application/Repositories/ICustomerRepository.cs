using System.Collections.Generic;
using System.Threading.Tasks;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Application.Repositories
{
    public interface ICustomerRepository : IBaseRepository<Customer>
    {
        Task<IEnumerable<Customer>> GetCustomersAsync();
        Task AddCustomerAsync(Customer customer);
        Task<Customer> GetByIdAsync(int id);
        void Update(Customer customer);
        void Delete(Customer customer);
    }
}
