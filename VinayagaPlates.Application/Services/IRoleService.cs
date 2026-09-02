using System.Collections.Generic;
using System.Threading.Tasks;
using VinayagaPlates.Application.DTOs;

namespace VinayagaPlates.Application.Services
{
    public interface IRoleService
    {
        Task<IReadOnlyList<RoleDto>> GetAllAsync();
        Task<RoleDto?> GetByIdAsync(int id);
        Task<RoleDto> CreateAsync(RoleDto dto, string? createdBy);
        Task<bool> UpdateAsync(int id, RoleDto dto, string? updatedBy);
        Task<bool> DeleteAsync(int id, string? deletedBy);
    }
}
