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
            var resp = data.Select(s => new {
                s.SaleId,
                s.SaleNumber,
                s.CustomerId,
                CustomerName = s.Customer?.CustomerName ?? "Unknown",
                s.SaleDate,
                s.TotalAmount,
                s.PaidAmount,
                BalanceAmount = s.TotalAmount - s.PaidAmount,
                s.PaymentStatus,
                s.Status,
                Details = s.Details.Select(d => new {
                    d.SaleDetailId,
                    d.ProductId,
                    d.BatchId,
                    d.Quantity,
                    d.UnitPrice
                }).ToList()
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
                var response = ApiResponse<Sale>.Success(result, "Sale recorded successfully.", 201);
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
                        populated.CustomerId,
                        CustomerName = populated.Customer?.CustomerName ?? "Unknown",
                        populated.SaleDate,
                        populated.TotalAmount,
                        populated.PaidAmount,
                        BalanceAmount = populated.TotalAmount - populated.PaidAmount,
                        populated.PaymentStatus,
                        populated.Status,
                        Details = populated.Details.Select(d => new {
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

                return Ok(ApiResponse<object>.Success(null, "Sale updated successfully."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message, 400));
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSale(int id)
        {
            using (var transaction = await _db.Database.BeginTransactionAsync())
            {
                try
                {
                    var sale = await _db.Sales
                        .Include(s => s.Details)
                        .FirstOrDefaultAsync(s => s.SaleId == id);

                    if (sale == null)
                        return NotFound(ApiResponse<object>.Fail("Sale not found.", 404));

                    // 1. Restore the batch quantities (revert deduction)
                    foreach (var d in sale.Details)
                    {
                        if (d.BatchId > 0)
                        {
                            var batch = await _db.InventoryBatches.FindAsync(d.BatchId);
                            if (batch != null)
                            {
                                batch.CurrentQuantity += d.Quantity;
                            }
                        }
                    }

                    // 2. Remove child sale details
                    _db.SaleDetails.RemoveRange(sale.Details);
                    await _db.SaveChangesAsync();

                    // 3. Remove parent sale record
                    _db.Sales.Remove(sale);
                    await _db.SaveChangesAsync();

                    await transaction.CommitAsync();

                    var response = ApiResponse<object>.Success(null, "Sale deleted successfully and stock reverted.");
                    return StatusCode(response.StatusCode, response);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return StatusCode(500, ApiResponse<object>.Fail($"Error deleting sale: {ex.Message}", 500));
                }
            }
        }
    }
}
