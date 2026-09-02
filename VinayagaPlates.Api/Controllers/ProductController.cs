using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VinayagaPlates.Application.Constants;
using VinayagaPlates.Application.Security;
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
    public class ProductController : ControllerBase
    {
        private readonly IProductRepository _productRepo;
        private readonly VpmsService _vpms;

        public ProductController(IProductRepository productRepo, VpmsService vpms)
        {
            _productRepo = productRepo;
            _vpms = vpms;
        }

        [HttpGet]
        public async Task<IActionResult> GetProducts()
        {
            var list = await _productRepo.GetProductsWithDetailsAsync();

            var resp = list
                .Where(p => !p.IsDeleted)
                .Select(p => new ProductResponse(
                    p.ProductId,
                    p.ProductCode,
                    p.ProductName,
                    p.CategoryId,
                    p.Category?.CategoryName ?? string.Empty,
                    p.VariantId,
                    p.Variant?.VariantName ?? string.Empty,
                    p.UnitId,
                    p.Unit?.UnitName ?? string.Empty,
                    p.InventoryBatches.Sum(b => b.CurrentQuantity),
                    p.MinStockAlert,
                    p.IsActive
                )).ToList();

            var response = ApiResponse<IEnumerable<ProductResponse>>.Success(resp, "Products retrieved successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var list = await _productRepo.GetProductsWithDetailsAsync();
            var p = list.FirstOrDefault(prod => prod.ProductId == id && !prod.IsDeleted);
            if (p == null)
            {
                var fail = ApiResponse<object>.Fail("Product not found.", 404);
                return StatusCode(fail.StatusCode, fail);
            }

            var resp = new ProductResponse(
                p.ProductId,
                p.ProductCode,
                p.ProductName,
                p.CategoryId,
                p.Category?.CategoryName ?? string.Empty,
                p.VariantId,
                p.Variant?.VariantName ?? string.Empty,
                p.UnitId,
                p.Unit?.UnitName ?? string.Empty,
                p.InventoryBatches.Sum(b => b.CurrentQuantity),
                p.MinStockAlert,
                p.IsActive
            );

            var response = ApiResponse<ProductResponse>.Success(resp, "Product retrieved successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [Authorize(Policy = "AdminPartnerPolicy")]
        [HttpPost]
        public async Task<IActionResult> CreateProduct([FromBody] ProductCreateRequest req)
        {
            try
            {
                var user = User.Identity?.Name ?? "SYSTEM";
                var prod = new Product
                {
                    ProductCode = "PRD-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                    ProductName = req.ProductName,
                    CategoryId = req.CategoryId,
                    VariantId = req.VariantId,
                    UnitId = req.UnitId,
                    MinStockAlert = req.MinStockAlert,
                    IsActive = true
                };

                var created = await _vpms.CreateProductAsync(prod, user);
                var response = ApiResponse<Product>.Success(created, "Product created successfully.", 201);
                return StatusCode(response.StatusCode, response);
            }
            catch (Exception ex)
            {
                var errorResponse = ApiResponse<string>.Fail($"Error creating product: {ex.Message}", 500);
                return StatusCode(errorResponse.StatusCode, errorResponse);
            }
        }

        [Authorize(Policy = "AdminPartnerPolicy")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] ProductCreateRequest req)
        {
            try
            {
                var user = User.Identity?.Name ?? "SYSTEM";
                var list = await _productRepo.GetProductsWithDetailsAsync();
                var prod = list.FirstOrDefault(p => p.ProductId == id && !p.IsDeleted);
                if (prod == null)
                {
                    var fail = ApiResponse<object>.Fail("Product not found.", 404);
                    return StatusCode(fail.StatusCode, fail);
                }

                var oldValues = $"Name: {prod.ProductName}, CatId: {prod.CategoryId}, VarId: {prod.VariantId}, UnitId: {prod.UnitId}, Alert: {prod.MinStockAlert}";

                prod.ProductName = req.ProductName;
                prod.CategoryId = req.CategoryId;
                prod.VariantId = req.VariantId;
                prod.UnitId = req.UnitId;
                prod.MinStockAlert = req.MinStockAlert;
                prod.UpdatedBy = user;
                prod.UpdatedAt = DateTime.UtcNow;

                _productRepo.Update(prod);
                await _productRepo.SaveChangesAsync();

                var newValues = $"Name: {prod.ProductName}, CatId: {prod.CategoryId}, VarId: {prod.VariantId}, UnitId: {prod.UnitId}, Alert: {prod.MinStockAlert}";
                await _vpms.LogAuditAsync(user, "UPDATE_PRODUCT", "TB_PRODUCT", prod.ProductId.ToString(), oldValues, newValues);

                var response = ApiResponse<Product>.Success(prod, "Product updated successfully.");
                return StatusCode(response.StatusCode, response);
            }
            catch (Exception ex)
            {
                var errorResponse = ApiResponse<string>.Fail($"Error updating product: {ex.Message}", 500);
                return StatusCode(errorResponse.StatusCode, errorResponse);
            }
        }

        [Authorize(Policy = "AdminPartnerPolicy")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var user = User.Identity?.Name ?? "SYSTEM";
                var list = await _productRepo.GetProductsWithDetailsAsync();
                var prod = list.FirstOrDefault(p => p.ProductId == id && !p.IsDeleted);
                if (prod == null)
                {
                    var fail = ApiResponse<object>.Fail("Product not found.", 404);
                    return StatusCode(fail.StatusCode, fail);
                }

                prod.IsDeleted = true;
                prod.UpdatedBy = user;
                prod.UpdatedAt = DateTime.UtcNow;

                _productRepo.Update(prod);
                await _productRepo.SaveChangesAsync();

                await _vpms.LogAuditAsync(user, "DELETE_PRODUCT", "TB_PRODUCT", prod.ProductId.ToString(), prod.ProductName, "DELETED");

                var response = ApiResponse<object>.Success(null, "Product deleted successfully.");
                return StatusCode(response.StatusCode, response);
            }
            catch (Exception ex)
            {
                var errorResponse = ApiResponse<string>.Fail($"Error deleting product: {ex.Message}", 500);
                return StatusCode(errorResponse.StatusCode, errorResponse);
            }
        }

        [HttpGet("get-stock-alerts")]
        public async Task<IActionResult> GetStockAlerts()
        {
            var list = await _productRepo.GetProductsWithDetailsAsync();

            var alerts = list
                .Where(p => !p.IsDeleted)
                .Select(p => new StockAlertResponse(
                    p.ProductId,
                    p.ProductName,
                    p.InventoryBatches.Sum(b => b.CurrentQuantity),
                    p.MinStockAlert
                ))
                .Where(a => a.CurrentStock <= a.MinStockAlert)
                .ToList();

            var response = ApiResponse<IEnumerable<StockAlertResponse>>.Success(alerts, "Stock alerts retrieved successfully.");
            return StatusCode(response.StatusCode, response);
        }
    }
}
