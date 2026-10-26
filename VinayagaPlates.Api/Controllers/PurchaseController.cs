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
            var purchaseIdInts = data.Select(p => p.PurchaseId).ToList();

            var payments = await _db.PurchasePayments
                .Include(p => p.Account)
                .Where(p => purchaseIdInts.Contains(p.PurchaseId))
                .ToListAsync();
            var paymentLookup = payments.ToLookup(p => p.PurchaseId);

            // Auto-heal any existing purchases that have PaidAmount > 0 but missing PurchasePayments
            var unlinkedPaid = data.Where(p => p.PaidAmount > 0 && !paymentLookup[p.PurchaseId].Any()).ToList();
            if (unlinkedPaid.Any())
            {
                var defaultAccount = await _db.BusinessAccounts.FirstOrDefaultAsync(a => a.AccountName.ToLower().Contains("pandiyan")) 
                                     ?? await _db.BusinessAccounts.FirstOrDefaultAsync();

                if (defaultAccount != null)
                {
                    foreach (var unlinked in unlinkedPaid)
                    {
                        var targetAcc = (!string.IsNullOrEmpty(unlinked.CreatedBy) 
                            ? await _db.BusinessAccounts.FirstOrDefaultAsync(a => a.AccountName.ToLower() == unlinked.CreatedBy.ToLower()) 
                            : null) ?? defaultAccount;

                        var newPay = new PurchasePayment
                        {
                            PurchaseId = unlinked.PurchaseId,
                            Amount = unlinked.PaidAmount,
                            AccountId = targetAcc.AccountId,
                            PaymentMethod = "CASH",
                            Notes = "Initial payment upon purchase creation",
                            CreatedBy = unlinked.CreatedBy ?? "SYSTEM",
                            CreatedAt = unlinked.PurchaseDate != default ? unlinked.PurchaseDate : unlinked.CreatedAt
                        };
                        _db.PurchasePayments.Add(newPay);

                        // Check if AccountTransaction already exists for this purchase
                        var existsTx = await _db.AccountTransactions.AnyAsync(t => 
                            t.ReferenceType == "PURCHASE" && 
                            (t.ReferenceId == unlinked.PurchaseId.ToString() || t.ReferenceId == unlinked.PurchaseNumber));

                        if (!existsTx)
                        {
                            _db.AccountTransactions.Add(new AccountTransaction
                            {
                                AccountId = targetAcc.AccountId,
                                TransactionType = "DEBIT",
                                Amount = unlinked.PaidAmount,
                                ReferenceType = "PURCHASE",
                                ReferenceId = unlinked.PurchaseId.ToString(),
                                Description = $"Paid ₹{unlinked.PaidAmount:N2} for Purchase {unlinked.PurchaseNumber}",
                                CreatedBy = unlinked.CreatedBy ?? "SYSTEM",
                                CreatedAt = unlinked.PurchaseDate != default ? unlinked.PurchaseDate : unlinked.CreatedAt
                            });
                        }
                    }
                    await _db.SaveChangesAsync();

                    // Reload payments and lookup
                    payments = await _db.PurchasePayments
                        .Include(p => p.Account)
                        .Where(p => purchaseIdInts.Contains(p.PurchaseId))
                        .ToListAsync();
                    paymentLookup = payments.ToLookup(p => p.PurchaseId);
                }
            }

            var linkedTxs = await _db.AccountTransactions
                .Include(t => t.Account)
                .Where(t => t.ReferenceType == "PURCHASE" && (purchaseIds.Contains(t.ReferenceId) || purchaseNumbers.Contains(t.ReferenceId)))
                .ToListAsync();

            var txLookup = linkedTxs.ToLookup(t => t.ReferenceId);

            var resp = data.Select(p => {
                var paymentList = paymentLookup[p.PurchaseId].ToList();
                string accName = "";
                int accId = 0;

                if (paymentList.Any())
                {
                    accName = string.Join(", ", paymentList.Select(x => x.Account?.AccountName).Where(n => !string.IsNullOrEmpty(n)).Distinct());
                    accId = paymentList.First().AccountId;
                }
                else
                {
                    var tx = txLookup[p.PurchaseId.ToString()].FirstOrDefault() ?? txLookup[p.PurchaseNumber].FirstOrDefault();
                    accName = tx?.Account?.AccountName ?? "";
                    accId = tx?.AccountId ?? 0;
                }

                return new {
                    p.PurchaseId,
                    p.PurchaseNumber,
                    p.SupplierId,
                    SupplierName = p.Supplier?.SupplierName ?? "Unknown",
                    p.PurchaseDate,
                    p.TotalAmount,
                    p.PaidAmount,
                    BalanceAmount = Math.Max(0, p.TotalAmount - p.PaidAmount),
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
            using (var transaction = await _db.Database.BeginTransactionAsync())
            {
                try
                {
                    var purchase = await _db.Purchases
                        .Include(p => p.Details)
                        .Include(p => p.Supplier)
                        .FirstOrDefaultAsync(p => p.PurchaseId == id);

                    if (purchase == null)
                        return NotFound(ApiResponse<object>.Fail("Purchase not found.", 404));

                    // 1. Inventory reversal check
                    foreach (var d in purchase.Details)
                    {
                        if (d.BatchId > 0)
                        {
                            var batch = await _db.InventoryBatches.FindAsync(d.BatchId);
                            if (batch != null)
                            {
                                if (batch.CurrentQuantity < batch.InitialQuantity)
                                {
                                    return BadRequest(ApiResponse<object>.Fail($"Cannot delete purchase because {batch.InitialQuantity - batch.CurrentQuantity} units from batch {batch.BatchNumber} have already been sold in Sales.", 400));
                                }

                                var movements = await _db.InventoryMovements
                                    .Where(m => m.BatchId == batch.BatchId || (m.ReferenceType == "PURCHASE" && m.ReferenceId == id.ToString()))
                                    .ToListAsync();
                                if (movements.Any())
                                {
                                    _db.InventoryMovements.RemoveRange(movements);
                                }

                                _db.InventoryBatches.Remove(batch);
                            }
                        }
                    }

                    // 2. Remove purchase payments and linked account transactions
                    var payments = await _db.PurchasePayments.Where(p => p.PurchaseId == id).ToListAsync();
                    var paymentIdStrs = payments.Select(p => p.PaymentId.ToString()).ToList();

                    var linkedTxs = await _db.AccountTransactions
                        .Where(t => t.ReferenceType == "PURCHASE" && 
                            (t.ReferenceId == id.ToString() || 
                             t.ReferenceId == purchase.PurchaseNumber || 
                             paymentIdStrs.Contains(t.ReferenceId)))
                        .ToListAsync();

                    if (linkedTxs.Any())
                    {
                        _db.AccountTransactions.RemoveRange(linkedTxs);
                    }

                    if (payments.Any())
                    {
                        _db.PurchasePayments.RemoveRange(payments);
                    }

                    // 3. Remove expense allocations and purchase expenses if any
                    var expenseAllocations = await _db.PurchaseExpenseAllocations
                        .Where(pea => pea.PurchaseDetail.PurchaseId == id)
                        .ToListAsync();
                    if (expenseAllocations.Any())
                    {
                        _db.PurchaseExpenseAllocations.RemoveRange(expenseAllocations);
                    }

                    var expenses = await _db.PurchaseExpenses
                        .Where(pe => pe.PurchaseId == id)
                        .ToListAsync();
                    if (expenses.Any())
                    {
                        _db.PurchaseExpenses.RemoveRange(expenses);
                    }

                    // 4. Remove purchase details and purchase
                    _db.PurchaseDetails.RemoveRange(purchase.Details);
                    _db.Purchases.Remove(purchase);

                    await _db.SaveChangesAsync();
                    await transaction.CommitAsync();

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
                    var response = ApiResponse<object>.Success(details, "Purchase deleted, stock reverted, and payment refunded to account.");
                    return StatusCode(response.StatusCode, response);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return StatusCode(500, ApiResponse<object>.Fail($"Error deleting purchase: {ex.Message}", 500));
                }
            }
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
