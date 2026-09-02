using System.Collections.Generic;
using System.Threading.Tasks;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Application.Repositories
{
    public interface IRoleRepository
    {
        Task<IReadOnlyList<Role>> GetAllAsync();
        Task<Role?> GetByIdAsync(int id);
        Task AddAsync(Role role, string? createdBy);
        Task UpdateAsync(Role role, string? updatedBy);
        Task SoftDeleteAsync(int roleId, string? deletedBy);
    }
}
