using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VinayagaPlates.Application.Repositories;
using VinayagaPlates.Application.Services;
using VinayagaPlates.Contracts.DTOs;
using VinayagaPlates.Domain.Entities;

using VinayagaPlates.Application;
using Microsoft.EntityFrameworkCore;

namespace VinayagaPlates.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BatchController : ControllerBase
    {
        private readonly IBatchRepository _batchRepo;
        private readonly VpmsService _vpms;
        private readonly ApplicationDbContext _db;

        public BatchController(IBatchRepository batchRepo, VpmsService vpms, ApplicationDbContext db)
        {
            _batchRepo = batchRepo;
            _vpms = vpms;
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await _batchRepo.GetBatchesAsync();

            var purchaseDetails = await _db.PurchaseDetails
                .Include(pd => pd.Purchase.Supplier)
                .ToListAsync();

            var resp = list.Select(b => {
                var pDetail = purchaseDetails.FirstOrDefault(pd => pd.BatchId == b.BatchId);
                var supplierName = pDetail?.Purchase?.Supplier?.SupplierName ?? "Direct Intake";
                return new {
                    b.BatchId,
                    b.BatchNumber,
                    b.ProductId,
                    ProductName = b.Product?.ProductName ?? "",
                    b.InitialQuantity,
                    b.CurrentQuantity,
                    b.UnitCost,
                    b.LandedUnitCost,
                    b.LocationId,
                    b.ReceivedDate,
                    b.Status,
                    SupplierName = supplierName,
                    b.CreatedAt
                };
            }).ToList();

            var response = ApiResponse<object>.Success(resp, "Batches retrieved successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var b = await _batchRepo.GetBatchByIdAsync(id);
            if (b == null)
                return NotFound(ApiResponse<object>.Fail("Batch not found.", 404));

            var resp = new InventoryBatchResponse(
                b.BatchId,
                b.BatchNumber,
                b.ProductId,
                b.Product?.ProductName ?? "",
                b.InitialQuantity,
                b.CurrentQuantity,
                b.UnitCost,
                b.LandedUnitCost,
                b.CreatedAt
            );

            var response = ApiResponse<InventoryBatchResponse>.Success(resp, "Batch retrieved successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost]
        public async Task<IActionResult> CreateBatch([FromBody] BatchCreateRequest req)
        {
            var batch = new InventoryBatch
            {
                BatchNumber = req.BatchNumber,
                ProductId = req.ProductId,
                InitialQuantity = req.InitialQuantity,
                CurrentQuantity = req.CurrentQuantity,
                UnitCost = req.UnitCost,
                LocationId = req.LocationId,
                ReceivedDate = req.ReceivedDate,
                LandedUnitCost = req.LandedUnitCost,
                TotalLandedCost = req.TotalLandedCost,
                Status = req.Status ?? "PENDING",
                CreatedBy = User.Identity?.Name ?? "SYSTEM",
                CreatedAt = DateTime.UtcNow
            };
            await _batchRepo.AddBatchAsync(batch);
            await _batchRepo.SaveChangesAsync();

            var response = ApiResponse<InventoryBatch>.Success(batch, "Batch created successfully.", 201);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBatch(int id, [FromBody] BatchUpdateRequest req)
        {
            var batch = await _batchRepo.GetBatchByIdAsync(id);
            if (batch == null)
                return NotFound(ApiResponse<object>.Fail("Batch not found.", 404));

            var soldQty = await _db.SaleDetails
                .Where(sd => sd.BatchId == id)
                .SumAsync(sd => sd.Quantity);

            if (req.InitialQuantity < soldQty)
            {
                return BadRequest(ApiResponse<object>.Fail($"Initial quantity cannot be set to {req.InitialQuantity} because {soldQty} items have already been sold from this batch.", 400));
            }

            if (req.CurrentQuantity < 0)
            {
                return BadRequest(ApiResponse<object>.Fail("Current quantity cannot be less than 0.", 400));
            }

            batch.BatchNumber = req.BatchNumber;
            batch.ProductId = req.ProductId;
            batch.InitialQuantity = req.InitialQuantity;
            batch.CurrentQuantity = req.CurrentQuantity;
            batch.UnitCost = req.UnitCost;
            batch.LocationId = req.LocationId;
            batch.ReceivedDate = req.ReceivedDate;
            batch.LandedUnitCost = req.LandedUnitCost;
            batch.TotalLandedCost = req.TotalLandedCost;
            batch.Status = req.Status ?? "PENDING";
            batch.UpdatedBy = User.Identity?.Name ?? "SYSTEM";
            batch.UpdatedAt = DateTime.UtcNow;

            _batchRepo.Update(batch);
            await _batchRepo.SaveChangesAsync();

            var response = ApiResponse<InventoryBatch>.Success(batch, "Batch updated successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBatch(int id)
        {
            var batch = await _batchRepo.GetBatchByIdAsync(id);
            if (batch == null)
                return NotFound(ApiResponse<object>.Fail("Batch not found.", 404));

            bool hasSales = await _db.SaleDetails.AnyAsync(sd => sd.BatchId == id);
            if (hasSales)
                return StatusCode(400, ApiResponse<object>.Fail("Cannot delete this batch because it is already referenced in sales transaction logs.", 400));

            _batchRepo.Delete(batch);
            await _batchRepo.SaveChangesAsync();

            var response = ApiResponse<object>.Success(null, "Batch deleted successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("adjust")]
        public async Task<IActionResult> AdjustStock([FromBody] StockAdjustmentRequest req)
        {
            await _vpms.AdjustStockAsync(req.BatchId, req.NewQuantity, req.Description, User.Identity?.Name ?? "SYSTEM");
            return Ok(new { message = "Stock adjusted successfully." });
        }
    }
}
