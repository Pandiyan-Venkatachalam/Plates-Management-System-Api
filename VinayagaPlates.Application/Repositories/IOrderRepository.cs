using System.Collections.Generic;
using System.Threading.Tasks;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Application.Repositories
{
    public interface IOrderRepository : IBaseRepository<Order>
    {
        Task<IEnumerable<Order>> GetOrdersWithDetailsAsync();
        Task<Order?> GetOrderWithDetailsByIdAsync(int id);
        void Update(Order order);
        void Delete(Order order);
    }
}
