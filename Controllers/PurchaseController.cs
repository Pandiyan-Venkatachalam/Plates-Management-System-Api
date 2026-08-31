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

namespace VinayagaPlates.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PurchaseController : ControllerBase
    {
        private readonly IPurchaseRepository _purchaseRepo;
        private readonly VpmsService _vpms;

        public PurchaseController(IPurchaseRepository purchaseRepo, VpmsService vpms)
        {
            _purchaseRepo = purchaseRepo;
            _vpms = vpms;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var data = await _purchaseRepo.GetPurchasesWithDetailsAsync();
            var resp = data.Select(p => new {
                p.PurchaseId,
                p.PurchaseNumber,
                p.SupplierId,
                SupplierName = p.Supplier?.SupplierName ?? "Unknown",
                p.PurchaseDate,
                p.TotalAmount,
                p.PaidAmount,
                BalanceAmount = p.TotalAmount - p.PaidAmount,
                p.PaymentStatus,
                p.Status,
                Details = p.Details.Select(d => new {
                    d.PurchaseDetailId,
                    d.ProductId,
                    d.BatchId,
                    d.Quantity,
                    d.UnitCost
                }).ToList()
            }).ToList();
            var response = ApiResponse<object>.Success(resp, "Purchases retrieved successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var data = await _purchaseRepo.GetPurchaseWithDetailsByIdAsync(id);
            if (data == null)
                return NotFound(ApiResponse<object>.Fail("Purchase not found.", 404));

            var response = ApiResponse<Purchase>.Success(data, "Purchase retrieved successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("create-purchase")]
        public async Task<IActionResult> CreatePurchase([FromBody] PurchaseCreateRequest req)
        {
            var user = User.Identity?.Name ?? "SYSTEM";
            var result = await _vpms.CreatePurchaseAsync(req, user);
            var populatedResult = await _purchaseRepo.GetPurchaseWithDetailsByIdAsync(result.PurchaseId);
            var response = ApiResponse<Purchase>.Success(populatedResult ?? result, "Purchase created successfully.", 201);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePurchase(int id, [FromBody] PurchaseUpdateRequest req)
        {
            try
            {
                var username = User.Identity?.Name ?? "SYSTEM";
                await _vpms.UpdatePurchaseAsync(id, req, username);

                var populated = await _purchaseRepo.GetPurchaseWithDetailsByIdAsync(id);
                if (populated != null)
                {
                    var projected = new {
                        populated.PurchaseId,
                        populated.PurchaseNumber,
                        populated.SupplierId,
                        SupplierName = populated.Supplier?.SupplierName ?? "Unknown",
                        populated.PurchaseDate,
                        populated.TotalAmount,
                        populated.PaidAmount,
                        BalanceAmount = populated.TotalAmount - populated.PaidAmount,
                        populated.PaymentStatus,
                        populated.Status,
                        Details = populated.Details.Select(d => new {
                            d.PurchaseDetailId,
                            d.ProductId,
                            d.BatchId,
                            d.Quantity,
                            d.UnitCost
                        }).ToList()
                    };
                    var successResponse = ApiResponse<object>.Success(projected, "Purchase updated successfully.");
                    return StatusCode(successResponse.StatusCode, successResponse);
                }

                return Ok(ApiResponse<object>.Success(null, "Purchase updated successfully."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message, 400));
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePurchase(int id)
        {
            var purchase = await _purchaseRepo.GetByIdAsync(id);
            if (purchase == null)
                return NotFound(ApiResponse<object>.Fail("Purchase not found.", 404));

            _purchaseRepo.Delete(purchase);
            await _purchaseRepo.SaveChangesAsync();

            var response = ApiResponse<object>.Success(null, "Purchase deleted successfully.");
            return StatusCode(response.StatusCode, response);
        }
    }
}
