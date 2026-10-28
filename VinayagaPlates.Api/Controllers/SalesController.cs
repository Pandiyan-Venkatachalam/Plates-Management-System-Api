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
    public class SalesController : ControllerBase
    {
        private readonly ISalesRepository _salesRepo;
        private readonly VpmsService _vpms;
        private readonly ApplicationDbContext _db;

        public SalesController(ISalesRepository salesRepo, VpmsService vpms, ApplicationDbContext db)
        {
            _salesRepo = salesRepo;
            _vpms = vpms;
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var data = await _salesRepo.GetSalesWithDetailsAsync();
            var saleIds = data.Select(s => s.SaleId.ToString()).ToHashSet();
            var saleNumbers = data.Where(s => !string.IsNullOrEmpty(s.SaleNumber)).Select(s => s.SaleNumber).ToHashSet();
            var saleIdInts = data.Select(s => s.SaleId).ToList();

            // 1. Fetch payments and transactions
            var payments = await _db.SalePayments
                .Include(p => p.Account)
                .Where(p => saleIdInts.Contains(p.SaleId))
                .ToListAsync();
            var paymentLookup = payments.ToLookup(p => p.SaleId);

            // 2. Fetch linked transactions ONLY for legacy fallback (matching SaleNumber strictly)
            var linkedTxs = await _db.AccountTransactions
                .Include(t => t.Account)
                .Where(t => t.ReferenceType == "SALE" && saleNumbers.Contains(t.ReferenceId))
                .ToListAsync();

            var txLookup = linkedTxs.ToLookup(t => t.ReferenceId);

            var resp = data.Select(s => {
                var paymentList = paymentLookup[s.SaleId].ToList();
                List<string> accountNames;

                if (paymentList.Any())
                {
                    // Strictly use the accounts/collectors from the actual payments recorded on this sale
                    accountNames = paymentList
                        .Select(x => !string.IsNullOrWhiteSpace(x.Account?.AccountName) ? x.Account.AccountName : (!string.IsNullOrWhiteSpace(x.PaymentMethod) && x.PaymentMethod != "CASH" ? x.PaymentMethod : null))
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (!accountNames.Any())
                    {
                        accountNames = paymentList
                            .Select(x => x.CreatedBy)
                            .Where(cb => !string.IsNullOrWhiteSpace(cb) && cb != "SYSTEM")
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                    }
                }
                else if (s.PaidAmount > 0)
                {
                    // Fallback only if no SalePayments records exist
                    var txList = txLookup[s.SaleNumber].ToList();
                    accountNames = txList
                        .Select(t => t.Account?.AccountName)
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (!accountNames.Any() && !string.IsNullOrWhiteSpace(s.CreatedBy) && s.CreatedBy != "SYSTEM")
                    {
                        accountNames.Add(s.CreatedBy);
                    }
                }
                else
                {
                    accountNames = new List<string>();
                }

                string accName = string.Join(", ", accountNames);
                int accId = paymentList.FirstOrDefault()?.AccountId ?? 0;

                return new {
                    s.SaleId,
                    s.SaleNumber,
                    s.CustomerId,
                    CustomerName = s.Customer?.CustomerName ?? "Unknown",
                    s.SaleDate,
                    s.TotalAmount,
                    s.PaidAmount,
                    BalanceAmount = Math.Max(0, s.TotalAmount - s.PaidAmount),
                    s.PaymentStatus,
                    s.Status,
                    AccountId = accId,
                    AccountName = accName,
                    PaymentMethodAccountName = accName,
                    Details = s.Details.Select(d => new {
                        d.SaleDetailId,
                        d.ProductId,
                        d.BatchId,
                        d.Quantity,
                        d.UnitPrice
                    }).ToList()
                };
            }).ToList();
            var response = ApiResponse<object>.Success(resp, "Sales retrieved successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var data = await _salesRepo.GetByIdAsync(id);
            if (data == null)
                return NotFound(ApiResponse<object>.Fail("Sale not found.", 404));

            var response = ApiResponse<Sale>.Success(data, "Sale retrieved successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("create-sale")]
        public async Task<IActionResult> CreateSale([FromBody] SaleCreateRequest req)
        {
            var user = User.Identity?.Name ?? "SYSTEM";
            try
            {
                var result = await _vpms.CreateSaleAsync(req, user);
                
                var allSales = await _salesRepo.GetSalesWithDetailsAsync();
                var fullSale = allSales.FirstOrDefault(s => s.SaleId == result.SaleId);
                
                var details = new { 
                    SaleId = result.SaleId, 
                    SaleNumber = result.SaleNumber,
                    CustomerName = fullSale?.Customer?.CustomerName ?? $"Customer ID {result.CustomerId}",
                    TotalItems = fullSale?.Details?.Sum(d => d.Quantity) ?? 0,
                    TotalAmount = result.TotalAmount,
                    Status = result.Status
                };

                var response = ApiResponse<object>.Success(details, "Sale recorded successfully.", 201);
                return StatusCode(response.StatusCode, response);
            }
            catch (InvalidOperationException ex)
            {
                var response = ApiResponse<Sale>.Fail(ex.Message, 400);
                return StatusCode(response.StatusCode, response);
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSale(int id, [FromBody] SaleUpdateRequest req)
        {
            try
            {
                var username = User.Identity?.Name ?? "SYSTEM";
                await _vpms.UpdateSaleAsync(id, req, username);

                var allSales = await _salesRepo.GetSalesWithDetailsAsync();
                var populated = allSales.FirstOrDefault(s => s.SaleId == id);

                if (populated != null)
                {
                    var projected = new {
                        populated.SaleId,
                        populated.SaleNumber,
                        CustomerName = populated.Customer?.CustomerName ?? "Unknown",
                        populated.SaleDate,
                        populated.TotalAmount,
                        populated.PaidAmount,
                        BalanceAmount = populated.TotalAmount - populated.PaidAmount,
                        populated.PaymentStatus,
                        populated.Status,
                        TotalItems = populated.Details?.Sum(d => d.Quantity) ?? 0,
                        Details = populated.Details?.Select(d => new {
                            d.SaleDetailId,
                            d.ProductId,
                            d.BatchId,
                            d.Quantity,
                            d.UnitPrice
                        }).ToList()
                    };
                    var successResponse = ApiResponse<object>.Success(projected, "Sale updated successfully.");
                    return StatusCode(successResponse.StatusCode, successResponse);
                }

                return Ok(ApiResponse<object>.Success(new { SaleId = id }, "Sale updated successfully."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message, 400));
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSale(int id)
        {
            try
            {
                var details = await _vpms.DeleteSaleAsync(id);
                var response = ApiResponse<object>.Success(details, "Sale deleted successfully and stock reverted.");
                return StatusCode(response.StatusCode, response);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ApiResponse<object>.Fail(ex.Message, 404));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Error deleting sale: {ex.Message}", 500));
            }
        }

        // --- SALE PAYMENTS API ---

        [HttpGet("{id}/payments")]
        public async Task<IActionResult> GetSalePayments(int id)
        {
            var payments = await _db.SalePayments
                .Include(p => p.Account)
                .Where(p => p.SaleId == id)
                .OrderBy(p => p.CreatedAt)
                .Select(p => new {
                    p.PaymentId,
                    p.SaleId,
                    p.Amount,
                    p.AccountId,
                    AccountName = p.Account.AccountName,
                    p.PaymentMethod,
                    p.Notes,
                    p.CreatedBy,
                    p.CreatedAt
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.Success(payments, "Payments retrieved successfully."));
        }

        [HttpPost("{id}/payments")]
        public async Task<IActionResult> AddPayment(int id, [FromBody] SalePaymentRequest req)
        {
            try
            {
                if (req.Amount <= 0) return BadRequest(ApiResponse<object>.Fail("Amount must be greater than zero.", 400));
                
                var username = User.Identity?.Name ?? "SYSTEM";
                var payment = await _vpms.AddSalePaymentAsync(id, req, username);
                
                var response = ApiResponse<object>.Success(new { payment.PaymentId }, "Payment added successfully.");
                return StatusCode(201, response);
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message, 400));
            }
        }

        [HttpPost("{id}/refunds")]
        public async Task<IActionResult> AddRefund(int id, [FromBody] SaleRefundRequest req)
        {
            try
            {
                if (req.Amount <= 0) return BadRequest(ApiResponse<object>.Fail("Amount must be greater than zero.", 400));
                
                var username = User.Identity?.Name ?? "SYSTEM";
                await _vpms.AddSaleRefundAsync(id, req, username);
                
                return Ok(ApiResponse<object>.Success(null, "Refund recorded successfully."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message, 400));
            }
        }

        [HttpDelete("{id}/payments/{paymentId}")]
        public async Task<IActionResult> DeletePayment(int id, int paymentId)
        {
            try
            {
                var username = User.Identity?.Name ?? "SYSTEM";
                await _vpms.DeleteSalePaymentAsync(id, paymentId, username);
                
                return Ok(ApiResponse<object>.Success(null, "Payment deleted successfully."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message, 400));
            }
        }
    }
}
