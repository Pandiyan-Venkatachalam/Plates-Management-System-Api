using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VinayagaPlates.Application;
using VinayagaPlates.Application.Repositories;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Infrastructure.Repositories
{
    public class UserRepository : BaseRepository<User>, IUserRepository
    {
        public UserRepository(ApplicationDbContext db) : base(db)
        {
        }

        public async Task<User> GetByUsernameAsync(string username)
        {
            return await Db.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r.RolePermissions)
                            .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
        }

        public async Task<bool> RoleExistsAsync(string roleName)
        {
            return await Db.Roles.AnyAsync(r => r.RoleName == roleName);
        }

        public async Task AddUserRoleAsync(UserRole userRole)
        {
            await Db.UserRoles.AddAsync(userRole);
        }
    }
}
