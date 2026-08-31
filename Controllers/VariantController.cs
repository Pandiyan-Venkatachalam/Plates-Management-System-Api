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
    public class VariantController : ControllerBase
    {
        private readonly IVariantRepository _variantRepo;
        private readonly VpmsService _vpms;

        public VariantController(IVariantRepository variantRepo, VpmsService vpms)
        {
            _variantRepo = variantRepo;
            _vpms = vpms;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var data = await _variantRepo.GetAllAsync();
            var response = ApiResponse<object>.Success(data, "Variants retrieved successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var vari = await _variantRepo.GetByIdAsync(id);
            if (vari == null)
            {
                var fail = ApiResponse<object>.Fail("Variant not found.", 404);
                return StatusCode(fail.StatusCode, fail);
            }
            var response = ApiResponse<ProductVariant>.Success(vari, "Variant retrieved successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] VariantCreateRequest req)
        {
            var username = User.Identity?.Name ?? "SYSTEM";
            var vari = new ProductVariant
            {
                VariantName = req.VariantName,
                CreatedBy = username,
                CreatedAt = DateTime.UtcNow
            };

            await _variantRepo.AddAsync(vari);
            await _variantRepo.SaveChangesAsync();

            await _vpms.LogAuditAsync(username, "CREATE_VARIANT", "TB_PRODUCT_VARIANT", vari.VariantId.ToString(), null, vari.VariantName);

            var response = ApiResponse<ProductVariant>.Success(vari, "Variant created successfully.", 201);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] VariantCreateRequest req)
        {
            var username = User.Identity?.Name ?? "SYSTEM";
            var vari = await _variantRepo.GetByIdAsync(id);
            if (vari == null)
            {
                var fail = ApiResponse<object>.Fail("Variant not found.", 404);
                return StatusCode(fail.StatusCode, fail);
            }

            var oldName = vari.VariantName;
            vari.VariantName = req.VariantName;
            vari.UpdatedBy = username;
            vari.UpdatedAt = DateTime.UtcNow;

            _variantRepo.Update(vari);
            await _variantRepo.SaveChangesAsync();

            await _vpms.LogAuditAsync(username, "UPDATE_VARIANT", "TB_PRODUCT_VARIANT", vari.VariantId.ToString(), oldName, vari.VariantName);

            var response = ApiResponse<ProductVariant>.Success(vari, "Variant updated successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpDelete("{id:int}")]
        [Authorize(Policy = "AdminPartnerPolicy")]
        public async Task<IActionResult> Delete(int id)
        {
            var username = User.Identity?.Name ?? "SYSTEM";
            var vari = await _variantRepo.GetByIdAsync(id);
            if (vari == null)
            {
                var fail = ApiResponse<object>.Fail("Variant not found.", 404);
                return StatusCode(fail.StatusCode, fail);
            }

            _variantRepo.Delete(vari);
            await _variantRepo.SaveChangesAsync();

            await _vpms.LogAuditAsync(username, "DELETE_VARIANT", "TB_PRODUCT_VARIANT", id.ToString(), vari.VariantName, null);

            var response = ApiResponse<object>.Success(null, "Variant deleted successfully.");
            return StatusCode(response.StatusCode, response);
        }
    }
}
