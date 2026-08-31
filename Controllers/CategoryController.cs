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
    public class CategoryController : ControllerBase
    {
        private readonly ICategoryRepository _categoryRepo;
        private readonly VpmsService _vpms;

        public CategoryController(ICategoryRepository categoryRepo, VpmsService vpms)
        {
            _categoryRepo = categoryRepo;
            _vpms = vpms;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var data = await _categoryRepo.GetAllAsync();
            var response = ApiResponse<object>.Success(data, "Categories retrieved successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var cat = await _categoryRepo.GetByIdAsync(id);
            if (cat == null)
            {
                var fail = ApiResponse<object>.Fail("Category not found.", 404);
                return StatusCode(fail.StatusCode, fail);
            }
            var response = ApiResponse<ProductCategory>.Success(cat, "Category retrieved successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CategoryCreateRequest req)
        {
            var username = User.Identity?.Name ?? "SYSTEM";
            var cat = new ProductCategory
            {
                CategoryName = req.CategoryName,
                CreatedBy = username,
                CreatedAt = DateTime.UtcNow
            };

            await _categoryRepo.AddAsync(cat);
            await _categoryRepo.SaveChangesAsync();

            await _vpms.LogAuditAsync(username, "CREATE_CATEGORY", "TB_PRODUCT_CATEGORY", cat.CategoryId.ToString(), null, cat.CategoryName);

            var response = ApiResponse<ProductCategory>.Success(cat, "Category created successfully.", 201);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] CategoryCreateRequest req)
        {
            var username = User.Identity?.Name ?? "SYSTEM";
            var cat = await _categoryRepo.GetByIdAsync(id);
            if (cat == null)
            {
                var fail = ApiResponse<object>.Fail("Category not found.", 404);
                return StatusCode(fail.StatusCode, fail);
            }

            var oldName = cat.CategoryName;
            cat.CategoryName = req.CategoryName;
            cat.UpdatedBy = username;
            cat.UpdatedAt = DateTime.UtcNow;

            _categoryRepo.Update(cat);
            await _categoryRepo.SaveChangesAsync();

            await _vpms.LogAuditAsync(username, "UPDATE_CATEGORY", "TB_PRODUCT_CATEGORY", cat.CategoryId.ToString(), oldName, cat.CategoryName);

            var response = ApiResponse<ProductCategory>.Success(cat, "Category updated successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpDelete("{id:int}")]
        [Authorize(Policy = "AdminPartnerPolicy")]
        public async Task<IActionResult> Delete(int id)
        {
            var username = User.Identity?.Name ?? "SYSTEM";
            var cat = await _categoryRepo.GetByIdAsync(id);
            if (cat == null)
            {
                var fail = ApiResponse<object>.Fail("Category not found.", 404);
                return StatusCode(fail.StatusCode, fail);
            }

            _categoryRepo.Delete(cat);
            await _categoryRepo.SaveChangesAsync();

            await _vpms.LogAuditAsync(username, "DELETE_CATEGORY", "TB_PRODUCT_CATEGORY", id.ToString(), cat.CategoryName, null);

            var response = ApiResponse<object>.Success(null, "Category deleted successfully.");
            return StatusCode(response.StatusCode, response);
        }
    }
}
