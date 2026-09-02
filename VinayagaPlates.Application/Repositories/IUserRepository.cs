using System.Threading.Tasks;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Application.Repositories
{
    public interface IUserRepository : IBaseRepository<User>
    {
        Task<User> GetByUsernameAsync(string username);
        Task<bool> RoleExistsAsync(string roleName);
        Task AddUserRoleAsync(UserRole userRole);
    }
}
