using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VinayagaPlates.Application.Repositories;
using VinayagaPlates.Application.Services;
using VinayagaPlates.Contracts.DTOs;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UnitController : ControllerBase
    {
        private readonly IUnitRepository _unitRepo;
        private readonly VpmsService _vpms;

        public UnitController(IUnitRepository unitRepo, VpmsService vpms)
        {
            _unitRepo = unitRepo;
            _vpms = vpms;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var data = await _unitRepo.GetAllAsync();
            var response = ApiResponse<object>.Success(data, "Units retrieved successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var unit = await _unitRepo.GetByIdAsync(id);
            if (unit == null)
            {
                var fail = ApiResponse<object>.Fail("Unit not found.", 404);
                return StatusCode(fail.StatusCode, fail);
            }
            var response = ApiResponse<ProductUnit>.Success(unit, "Unit retrieved successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] UnitCreateRequest req)
        {
            var username = User.Identity?.Name ?? "SYSTEM";
            var unit = new ProductUnit
            {
                UnitName = req.UnitName,
                CreatedBy = username,
                CreatedAt = DateTime.UtcNow
            };

            await _unitRepo.AddAsync(unit);
            await _unitRepo.SaveChangesAsync();

            await _vpms.LogAuditAsync(username, "CREATE_UNIT", "TB_PRODUCT_UNIT", unit.UnitId.ToString(), null, unit.UnitName);

            var response = ApiResponse<ProductUnit>.Success(unit, "Unit created successfully.", 201);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UnitCreateRequest req)
        {
            var username = User.Identity?.Name ?? "SYSTEM";
            var unit = await _unitRepo.GetByIdAsync(id);
            if (unit == null)
            {
                var fail = ApiResponse<object>.Fail("Unit not found.", 404);
                return StatusCode(fail.StatusCode, fail);
            }

            var oldName = unit.UnitName;
            unit.UnitName = req.UnitName;
            unit.UpdatedBy = username;
            unit.UpdatedAt = DateTime.UtcNow;

            _unitRepo.Update(unit);
            await _unitRepo.SaveChangesAsync();

            await _vpms.LogAuditAsync(username, "UPDATE_UNIT", "TB_PRODUCT_UNIT", unit.UnitId.ToString(), oldName, unit.UnitName);

            var response = ApiResponse<ProductUnit>.Success(unit, "Unit updated successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpDelete("{id:int}")]
        [Authorize(Policy = "AdminPartnerPolicy")]
        public async Task<IActionResult> Delete(int id)
        {
            var username = User.Identity?.Name ?? "SYSTEM";
            var unit = await _unitRepo.GetByIdAsync(id);
            if (unit == null)
            {
                var fail = ApiResponse<object>.Fail("Unit not found.", 404);
                return StatusCode(fail.StatusCode, fail);
            }

            _unitRepo.Delete(unit);
            await _unitRepo.SaveChangesAsync();

            await _vpms.LogAuditAsync(username, "DELETE_UNIT", "TB_PRODUCT_UNIT", id.ToString(), unit.UnitName, null);

            var response = ApiResponse<object>.Success(null, "Unit deleted successfully.");
            return StatusCode(response.StatusCode, response);
        }
    }
}
