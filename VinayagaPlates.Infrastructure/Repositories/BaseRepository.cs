using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VinayagaPlates.Application;
using VinayagaPlates.Application.Repositories;

namespace VinayagaPlates.Infrastructure.Repositories
{
    public class BaseRepository<T> : IBaseRepository<T> where T : class
    {
        protected readonly ApplicationDbContext Db;

        public BaseRepository(ApplicationDbContext db)
        {
            Db = db;
        }

        public async Task<T> GetByIdAsync(int id) => await Db.Set<T>().FindAsync(id);

        public async Task<IEnumerable<T>> GetAllAsync() => await Db.Set<T>().ToListAsync();

        public async Task AddAsync(T entity) => await Db.Set<T>().AddAsync(entity);

        public void Update(T entity) => Db.Set<T>().Update(entity);

        public void Delete(T entity) => Db.Set<T>().Remove(entity);

        public async Task SaveChangesAsync() => await Db.SaveChangesAsync();
    }
}
