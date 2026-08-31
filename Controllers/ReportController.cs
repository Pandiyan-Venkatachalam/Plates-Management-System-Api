using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VinayagaPlates.Application;
using VinayagaPlates.Application.Repositories;
using VinayagaPlates.Application.Services;
using VinayagaPlates.Contracts.DTOs;

namespace VinayagaPlates.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReportController : ControllerBase
    {
        private readonly VpmsService _vpms;
        private readonly IAccountRepository _accountRepo;
        private readonly ApplicationDbContext _db;

        public ReportController(VpmsService vpms, IAccountRepository accountRepo, ApplicationDbContext db)
        {
            _vpms = vpms;
            _accountRepo = accountRepo;
            _db = db;
        }

        [HttpGet("get-dashboard-stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var stats = await _vpms.GetDashboardStatsAsync();
            var response = ApiResponse<object>.Success(stats, "Dashboard statistics retrieved successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("get-audit-history")]
        [Authorize(Policy = "AdminPartnerPolicy")]
        public async Task<IActionResult> GetAuditHistory()
        {
            var logs = await _accountRepo.GetAuditLogsAsync();
            var response = ApiResponse<object>.Success(logs, "Audit logs retrieved successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("profit-loss")]
        public async Task<IActionResult> GetProfitLossReport([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            var saleQuery = _db.SaleDetails
                .Include(sd => sd.Product.Variant)
                .Include(sd => sd.Batch.Location)
                .Include(sd => sd.Sale)
                .AsQueryable();

            var purchaseQuery = _db.PurchaseDetails
                .Include(pd => pd.Purchase.Supplier)
                .Include(pd => pd.Product.Variant)
                .AsQueryable();

            var batchQuery = _db.InventoryBatches
                .Include(b => b.Location)
                .AsQueryable();

            var expenseQuery = _db.AccountTransactions
                .Where(t => t.ReferenceType == "EXPENSE")
                .AsQueryable();

            if (fromDate.HasValue)
            {
                var fromUtc = DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Utc);
                saleQuery = saleQuery.Where(sd => sd.Sale.SaleDate >= fromUtc);
                purchaseQuery = purchaseQuery.Where(pd => pd.Purchase.PurchaseDate >= fromUtc);
                batchQuery = batchQuery.Where(b => b.ReceivedDate >= fromUtc);
                expenseQuery = expenseQuery.Where(t => t.CreatedAt >= fromUtc);
            }

            if (toDate.HasValue)
            {
                var toUtc = DateTime.SpecifyKind(toDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
                saleQuery = saleQuery.Where(sd => sd.Sale.SaleDate <= toUtc);
                purchaseQuery = purchaseQuery.Where(pd => pd.Purchase.PurchaseDate <= toUtc);
                batchQuery = batchQuery.Where(b => b.ReceivedDate <= toUtc);
                expenseQuery = expenseQuery.Where(t => t.CreatedAt <= toUtc);
            }

            var saleDetails = await saleQuery.ToListAsync();
            // Retrieve all purchase details for metadata lookup (supplier names)
            var allPurchaseDetails = await _db.PurchaseDetails
                .Include(pd => pd.Purchase.Supplier)
                .Include(pd => pd.Product.Variant)
                .ToListAsync();

            // Retrieve all batches for all-time initial quantity lookup
            var allBatches = await _db.InventoryBatches
                .Include(b => b.Location)
                .ToListAsync();

            var filteredExpenses = await expenseQuery.ToListAsync();
            decimal totalExpenses = filteredExpenses.Sum(e => e.Amount);

            var flatList = saleDetails.Select(sd => {
                var pDetail = allPurchaseDetails.FirstOrDefault(pd => pd.BatchId == sd.BatchId);
                var supplierName = pDetail?.Purchase?.Supplier?.SupplierName ?? "Direct Intake";
                var variantName = sd.Product?.Variant?.VariantName ?? "Default Sizing";
                var locationName = sd.Batch?.Location?.LocationName ?? "Default Godown";
                var batchNumber = sd.Batch?.BatchNumber ?? "Unknown Batch";

                decimal revenue = sd.Quantity * sd.UnitPrice;
                decimal cost = sd.Quantity * (sd.Batch?.LandedUnitCost > 0 ? sd.Batch.LandedUnitCost : sd.Batch?.UnitCost ?? 0);
                decimal profit = revenue - cost;

                return new {
                    sd.SaleDetailId,
                    sd.ProductId,
                    ProductName = sd.Product?.ProductName ?? "",
                    VariantName = variantName,
                    BatchId = sd.BatchId,
                    BatchNumber = batchNumber,
                    SupplierName = supplierName,
                    LocationName = locationName,
                    Quantity = sd.Quantity,
                    UnitPrice = sd.UnitPrice,
                    Revenue = revenue,
                    Cost = cost,
                    Profit = profit
                };
            }).ToList();

            var supplierWise = flatList
                .GroupBy(x => x.SupplierName)
                .Select(g => new {
                    Category = g.Key,
                    TotalQuantity = g.Sum(x => x.Quantity),
                    TotalPurchasedQuantity = allPurchaseDetails
                        .Where(pd => (pd.Purchase?.Supplier?.SupplierName ?? "Direct Intake") == g.Key)
                        .Sum(pd => pd.Quantity),
                    TotalRevenue = g.Sum(x => x.Revenue),
                    TotalCost = g.Sum(x => x.Cost),
                    NetProfit = g.Sum(x => x.Profit)
                }).ToList();

            var batchWise = flatList
                .GroupBy(x => x.BatchNumber)
                .Select(g => new {
                    Category = g.Key,
                    TotalQuantity = g.Sum(x => x.Quantity),
                    TotalPurchasedQuantity = allBatches
                        .Where(b => b.BatchNumber == g.Key)
                        .Sum(b => b.InitialQuantity),
                    TotalRevenue = g.Sum(x => x.Revenue),
                    TotalCost = g.Sum(x => x.Cost),
                    NetProfit = g.Sum(x => x.Profit)
                }).ToList();

            var sizeWise = flatList
                .GroupBy(x => x.VariantName)
                .Select(g => new {
                    Category = g.Key,
                    TotalQuantity = g.Sum(x => x.Quantity),
                    TotalPurchasedQuantity = allPurchaseDetails
                        .Where(pd => (pd.Product?.Variant?.VariantName ?? "Default Sizing") == g.Key)
                        .Sum(pd => pd.Quantity),
                    TotalRevenue = g.Sum(x => x.Revenue),
                    TotalCost = g.Sum(x => x.Cost),
                    NetProfit = g.Sum(x => x.Profit)
                }).ToList();

            var locationWise = flatList
                .GroupBy(x => x.LocationName)
                .Select(g => new {
                    Category = g.Key,
                    TotalQuantity = g.Sum(x => x.Quantity),
                    TotalPurchasedQuantity = allBatches
                        .Where(b => (b.Location?.LocationName ?? "Default Godown") == g.Key)
                        .Sum(b => b.InitialQuantity),
                    TotalRevenue = g.Sum(x => x.Revenue),
                    TotalCost = g.Sum(x => x.Cost),
                    NetProfit = g.Sum(x => x.Profit)
                }).ToList();

            var result = new {
                TotalRevenue = flatList.Sum(x => x.Revenue),
                TotalCost = flatList.Sum(x => x.Cost),
                TotalExpenses = totalExpenses,
                TotalProfit = flatList.Sum(x => x.Revenue) - flatList.Sum(x => x.Cost) - totalExpenses,
                SupplierWise = supplierWise,
                BatchWise = batchWise,
                SizeWise = sizeWise,
                LocationWise = locationWise
            };

            var response = ApiResponse<object>.Success(result, "Profit & Loss report generated successfully.");
            return StatusCode(response.StatusCode, response);
        }
    }
}
