using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VinayagaPlates.Application;
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
        private readonly ApplicationDbContext _db;

        public PurchaseController(IPurchaseRepository purchaseRepo, VpmsService vpms, ApplicationDbContext db)
        {
            _purchaseRepo = purchaseRepo;
            _vpms = vpms;
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var data = await _purchaseRepo.GetPurchasesWithDetailsAsync();
            var purchaseIds = data.Select(p => p.PurchaseId.ToString()).ToHashSet();
            var purchaseNumbers = data.Where(p => !string.IsNullOrEmpty(p.PurchaseNumber)).Select(p => p.PurchaseNumber).ToHashSet();

            var linkedTxs = await _db.AccountTransactions
                .Include(t => t.Account)
                .Where(t => t.ReferenceType == "PURCHASE" && (purchaseIds.Contains(t.ReferenceId) || purchaseNumbers.Contains(t.ReferenceId)))
                .ToListAsync();

            var txLookup = linkedTxs.ToLookup(t => t.ReferenceId);

            var resp = data.Select(p => {
                var tx = txLookup[p.PurchaseId.ToString()].FirstOrDefault() ?? txLookup[p.PurchaseNumber].FirstOrDefault();
                var accName = tx?.Account?.AccountName ?? "";
                var accId = tx?.AccountId ?? 0;

                return new {
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
                    AccountId = accId,
                    AccountName = accName,
                    PaymentMethodAccountName = accName,
                    Details = p.Details.Select(d => new {
                        d.PurchaseDetailId,
                        d.ProductId,
                        d.BatchId,
                        d.Quantity,
                        d.UnitCost
                    }).ToList()
                };
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
            var fullPurchase = await _purchaseRepo.GetPurchaseWithDetailsByIdAsync(result.PurchaseId);
            
            var details = new { 
                PurchaseId = result.PurchaseId, 
                PurchaseNumber = result.PurchaseNumber,
                SupplierName = fullPurchase?.Supplier?.SupplierName ?? $"Supplier ID {result.SupplierId}",
                TotalItems = fullPurchase?.Details?.Sum(d => d.Quantity) ?? 0,
                TotalAmount = result.TotalAmount,
                Status = result.Status
            };

            var response = ApiResponse<object>.Success(details, "Purchase created successfully.", 201);
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
                        SupplierName = populated.Supplier?.SupplierName ?? "Unknown",
                        populated.PurchaseDate,
                        populated.TotalAmount,
                        populated.PaidAmount,
                        BalanceAmount = populated.TotalAmount - populated.PaidAmount,
                        populated.PaymentStatus,
                        populated.Status,
                        TotalItems = populated.Details?.Sum(d => d.Quantity) ?? 0,
                        Details = populated.Details?.Select(d => new {
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

                return Ok(ApiResponse<object>.Success(new { PurchaseId = id }, "Purchase updated successfully."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message, 400));
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePurchase(int id)
        {
            var purchase = await _purchaseRepo.GetPurchaseWithDetailsByIdAsync(id);
            if (purchase == null)
                return NotFound(ApiResponse<object>.Fail("Purchase not found.", 404));

            // Clean up linked AccountTransactions
            var linkedTxs = await _db.AccountTransactions
                .Where(t => t.ReferenceType == "PURCHASE" && (t.ReferenceId == id.ToString() || t.ReferenceId == purchase.PurchaseNumber))
                .ToListAsync();
            if (linkedTxs.Any())
            {
                _db.AccountTransactions.RemoveRange(linkedTxs);
                await _db.SaveChangesAsync();
            }

            _purchaseRepo.Delete(purchase);
            await _purchaseRepo.SaveChangesAsync();

            var details = new { 
                purchase.PurchaseId, 
                purchase.PurchaseNumber, 
                SupplierName = purchase.Supplier?.SupplierName ?? $"Supplier ID {purchase.SupplierId}",
                purchase.TotalAmount, 
                purchase.PaidAmount, 
                purchase.Status, 
                purchase.PurchaseDate,
                TotalItems = purchase.Details?.Sum(d => d.Quantity) ?? 0
            };
            var response = ApiResponse<object>.Success(details, "Purchase deleted successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("{id}/payments")]
        public async Task<IActionResult> GetPayments(int id)
        {
            var payments = await _vpms.GetPurchasePaymentsAsync(id);
            var response = ApiResponse<IEnumerable<object>>.Success(payments.Select(p => new
            {
                p.PaymentId,
                p.Amount,
                AccountName = p.Account?.AccountName,
                p.PaymentMethod,
                p.Notes,
                p.CreatedBy,
                p.CreatedAt
            }), "Purchase payments retrieved successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("{id}/payments")]
        public async Task<IActionResult> AddPayment(int id, [FromBody] PurchasePaymentRequest req)
        {
            try
            {
                var username = User.Identity?.Name ?? "system";
                var payment = await _vpms.AddPurchasePaymentAsync(id, req, username);
                var response = ApiResponse<object>.Success(new { payment.PaymentId, payment.Amount }, "Payment recorded successfully.");
                return StatusCode(response.StatusCode, response);
            }
            catch (Exception ex)
            {
                var err = ApiResponse<object>.Fail(ex.Message, 400);
                return StatusCode(err.StatusCode, err);
            }
        }

        [HttpPost("{id}/refunds")]
        public async Task<IActionResult> AddRefund(int id, [FromBody] PurchaseRefundRequest req)
        {
            try
            {
                var username = User.Identity?.Name ?? "system";
                await _vpms.AddPurchaseRefundAsync(id, req, username);
                var response = ApiResponse<object>.Success(null, "Refund recorded successfully.");
                return StatusCode(response.StatusCode, response);
            }
            catch (Exception ex)
            {
                var err = ApiResponse<object>.Fail(ex.Message, 400);
                return StatusCode(err.StatusCode, err);
            }
        }

        [HttpDelete("{id}/payments/{paymentId}")]
        public async Task<IActionResult> DeletePayment(int id, int paymentId)
        {
            try
            {
                var username = User.Identity?.Name ?? "system";
                await _vpms.DeletePurchasePaymentAsync(id, paymentId, username);
                var response = ApiResponse<object>.Success(null, "Payment deleted successfully.");
                return StatusCode(response.StatusCode, response);
            }
            catch (Exception ex)
            {
                var err = ApiResponse<object>.Fail(ex.Message, 400);
                return StatusCode(err.StatusCode, err);
            }
        }
    }
}
