using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VinayagaPlates.Application.Repositories;
using VinayagaPlates.Contracts.DTOs;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SupplierController : ControllerBase
    {
        private readonly ISupplierRepository _supplierRepo;

        public SupplierController(ISupplierRepository supplierRepo)
        {
            _supplierRepo = supplierRepo;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var data = await _supplierRepo.GetSuppliersAsync();
            var response = ApiResponse<object>.Success(data, "Suppliers retrieved successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var data = await _supplierRepo.GetByIdAsync(id);
            if (data == null)
                return NotFound(ApiResponse<object>.Fail("Supplier not found.", 404));

            var response = ApiResponse<Supplier>.Success(data, "Supplier retrieved successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("create-supplier")]
        public async Task<IActionResult> CreateSupplier([FromBody] SupplierCreateRequest req)
        {
            var supplier = new Supplier
            {
                SupplierName = req.SupplierName,
                ContactPerson = req.ContactPerson ?? "",
                Phone = req.Phone ?? "",
                Email = req.Email ?? "",
                Address = req.Address ?? "",
                CreatedBy = User.Identity?.Name ?? "SYSTEM",
                CreatedAt = DateTime.UtcNow
            };

            await _supplierRepo.AddSupplierAsync(supplier);
            await _supplierRepo.SaveChangesAsync();

            var resp = new SupplierResponse(
                supplier.SupplierId,
                supplier.SupplierName,
                supplier.ContactPerson,
                supplier.Phone,
                supplier.Email,
                supplier.Address
            );

            var response = ApiResponse<SupplierResponse>.Success(resp, "Supplier created successfully.", 201);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSupplier(int id, [FromBody] SupplierCreateRequest req)
        {
            var supplier = await _supplierRepo.GetByIdAsync(id);
            if (supplier == null)
                return NotFound(ApiResponse<object>.Fail("Supplier not found.", 404));

            supplier.SupplierName = req.SupplierName;
            supplier.ContactPerson = req.ContactPerson ?? "";
            supplier.Phone = req.Phone ?? "";
            supplier.Email = req.Email ?? "";
            supplier.Address = req.Address ?? "";
            supplier.UpdatedBy = User.Identity?.Name ?? "SYSTEM";
            supplier.UpdatedAt = DateTime.UtcNow;

            _supplierRepo.Update(supplier);
            await _supplierRepo.SaveChangesAsync();

            var resp = new SupplierResponse(
                supplier.SupplierId,
                supplier.SupplierName,
                supplier.ContactPerson,
                supplier.Phone,
                supplier.Email,
                supplier.Address
            );

            var response = ApiResponse<SupplierResponse>.Success(resp, "Supplier updated successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSupplier(int id)
        {
            var supplier = await _supplierRepo.GetByIdAsync(id);
            if (supplier == null)
                return NotFound(ApiResponse<object>.Fail("Supplier not found.", 404));

            supplier.IsDeleted = true;
            supplier.UpdatedBy = User.Identity?.Name ?? "SYSTEM";
            supplier.UpdatedAt = DateTime.UtcNow;

            _supplierRepo.Update(supplier);
            await _supplierRepo.SaveChangesAsync();

            var response = ApiResponse<object>.Success(null, "Supplier deleted successfully.");
            return StatusCode(response.StatusCode, response);
        }
    }
}
