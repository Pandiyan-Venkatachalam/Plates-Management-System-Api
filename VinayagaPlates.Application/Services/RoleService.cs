using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using VinayagaPlates.Application.DTOs;
using VinayagaPlates.Application.Repositories;
using VinayagaPlates.Application.Services;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Application.Services
{
    public class RoleService : IRoleService
    {
        private readonly IRoleRepository _repository;
        private readonly IMapper _mapper;

        public RoleService(IRoleRepository repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        public async Task<IReadOnlyList<RoleDto>> GetAllAsync()
        {
            var roles = await _repository.GetAllAsync();
            return _mapper.Map<IReadOnlyList<RoleDto>>(roles);
        }

        public async Task<RoleDto?> GetByIdAsync(int id)
        {
            var role = await _repository.GetByIdAsync(id);
            return role == null ? null : _mapper.Map<RoleDto>(role);
        }

        public async Task<RoleDto> CreateAsync(RoleDto dto, string? createdBy)
        {
            var role = _mapper.Map<Role>(dto);
            await _repository.AddAsync(role, createdBy);
            return _mapper.Map<RoleDto>(role);
        }

        public async Task<bool> UpdateAsync(int id, RoleDto dto, string? updatedBy)
        {
            var existing = await _repository.GetByIdAsync(id);
            if (existing == null) return false;
            // Map updated fields
            _mapper.Map(dto, existing);
            await _repository.UpdateAsync(existing, updatedBy);
            return true;
        }

        public async Task<bool> DeleteAsync(int id, string? deletedBy)
        {
            var existing = await _repository.GetByIdAsync(id);
            if (existing == null) return false;
            await _repository.SoftDeleteAsync(id, deletedBy);
            return true;
        }
    }
}
