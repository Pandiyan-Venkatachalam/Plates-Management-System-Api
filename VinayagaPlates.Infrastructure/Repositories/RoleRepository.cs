using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VinayagaPlates.Domain.Entities;
using VinayagaPlates.Application.Repositories;
using VinayagaPlates.Application;

namespace VinayagaPlates.Infrastructure.Repositories
{
    public class RoleRepository : IRoleRepository
    {
        private readonly ApplicationDbContext _ctx;
        public RoleRepository(ApplicationDbContext ctx) => _ctx = ctx;

        public async Task<IReadOnlyList<Role>> GetAllAsync()
            => await _ctx.Roles.Where(r => !r.IsDeleted).ToListAsync();

        public async Task<Role?> GetByIdAsync(int id)
            => await _ctx.Roles.FirstOrDefaultAsync(r => r.RoleId == id && !r.IsDeleted);

        public async Task AddAsync(Role role, string? createdBy)
        {
            role.CreatedBy = createdBy ?? "SYSTEM";
            _ctx.Roles.Add(role);
            await _ctx.SaveChangesAsync();
        }

        public async Task UpdateAsync(Role role, string? updatedBy)
        {
            role.UpdatedBy = updatedBy ?? "SYSTEM";
            role.UpdatedAt = DateTime.UtcNow;
            _ctx.Roles.Update(role);
            await _ctx.SaveChangesAsync();
        }

        public async Task SoftDeleteAsync(int roleId, string? deletedBy)
        {
            var role = await GetByIdAsync(roleId);
            if (role == null) return;
            role.IsDeleted = true;
            role.DeletedBy = deletedBy ?? "SYSTEM";
            role.DeletedAt = DateTime.UtcNow;
            await _ctx.SaveChangesAsync();
        }
    }
}
