using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VinayagaPlates.Application;
using VinayagaPlates.Application.Repositories;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Infrastructure.Repositories
{
    public class CustomerRepository : BaseRepository<Customer>, ICustomerRepository
    {
        public CustomerRepository(ApplicationDbContext db) : base(db)
        {
        }

        public async Task<IEnumerable<Customer>> GetCustomersAsync() =>
            await Db.Customers.ToListAsync();

        public async Task AddCustomerAsync(Customer customer) =>
            await Db.Customers.AddAsync(customer);
    }
}
