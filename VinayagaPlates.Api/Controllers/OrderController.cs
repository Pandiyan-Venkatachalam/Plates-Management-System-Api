using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VinayagaPlates.Application.Repositories;
using VinayagaPlates.Application.Services;
using VinayagaPlates.Contracts.DTOs;

namespace VinayagaPlates.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrderController : ControllerBase
    {
        private readonly IOrderRepository _orderRepo;
        private readonly VpmsService _vpms;

        public OrderController(IOrderRepository orderRepo, VpmsService vpms)
        {
            _orderRepo = orderRepo;
            _vpms = vpms;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var data = await _orderRepo.GetOrdersWithDetailsAsync();
            var resp = data.Select(o => new OrderResponse(
                o.OrderId,
                o.OrderNo,
                o.CustomerId,
                o.Customer?.CustomerName ?? "Unknown",
                o.Customer?.Phone,
                o.OrderDate,
                o.ExpectedDate,
                o.Status,
                o.Priority,
                o.Notes ?? "",
                o.Details.Sum(d => d.OrderedQuantity * d.SellingPrice),
                o.Details.Sum(d => d.OrderedQuantity),
                o.CreatedBy,
                o.CreatedAt,
                o.UpdatedBy,
                o.UpdatedAt,
                o.Details.Select(d => new OrderDetailResponse(
                    d.OrderDetailId,
                    d.OrderId,
                    d.ProductId,
                    d.Product?.ProductName ?? $"Product #{d.ProductId}",
                    d.Product?.ProductCode,
                    d.OrderedQuantity,
                    d.ReservedQuantity,
                    d.DeliveredQuantity,
                    d.SellingPrice,
                    d.OrderedQuantity * d.SellingPrice
                )).ToList()
            )).ToList();

            var response = ApiResponse<object>.Success(resp, "Orders retrieved successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var o = await _orderRepo.GetOrderWithDetailsByIdAsync(id);
            if (o == null)
            {
                var notFound = ApiResponse<string>.Fail("Order not found.", 404);
                return StatusCode(404, notFound);
            }

            var resp = new OrderResponse(
                o.OrderId,
                o.OrderNo,
                o.CustomerId,
                o.Customer?.CustomerName ?? "Unknown",
                o.Customer?.Phone,
                o.OrderDate,
                o.ExpectedDate,
                o.Status,
                o.Priority,
                o.Notes ?? "",
                o.Details.Sum(d => d.OrderedQuantity * d.SellingPrice),
                o.Details.Sum(d => d.OrderedQuantity),
                o.CreatedBy,
                o.CreatedAt,
                o.UpdatedBy,
                o.UpdatedAt,
                o.Details.Select(d => new OrderDetailResponse(
                    d.OrderDetailId,
                    d.OrderId,
                    d.ProductId,
                    d.Product?.ProductName ?? $"Product #{d.ProductId}",
                    d.Product?.ProductCode,
                    d.OrderedQuantity,
                    d.ReservedQuantity,
                    d.DeliveredQuantity,
                    d.SellingPrice,
                    d.OrderedQuantity * d.SellingPrice
                )).ToList()
            );

            var response = ApiResponse<OrderResponse>.Success(resp, "Order details retrieved successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] OrderCreateRequest req)
        {
            try
            {
                var username = User.Identity?.Name ?? "SYSTEM";
                var order = await _vpms.CreateOrderAsync(req, username);

                var response = ApiResponse<object>.Success(new { order.OrderId, order.OrderNo }, "Order created successfully as draft.", 201);
                return StatusCode(201, response);
            }
            catch (ArgumentException ex)
            {
                var response = ApiResponse<string>.Fail(ex.Message, 400);
                return StatusCode(400, response);
            }
            catch (Exception ex)
            {
                var response = ApiResponse<string>.Fail($"Failed to create order: {ex.Message}", 500);
                return StatusCode(500, response);
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] OrderUpdateRequest req)
        {
            try
            {
                var username = User.Identity?.Name ?? "SYSTEM";
                var order = await _vpms.UpdateOrderAsync(id, req, username);

                var response = ApiResponse<object>.Success(new { order.OrderId, order.OrderNo }, "Order updated successfully.");
                return StatusCode(200, response);
            }
            catch (ArgumentException ex)
            {
                var response = ApiResponse<string>.Fail(ex.Message, 400);
                return StatusCode(400, response);
            }
            catch (InvalidOperationException ex)
            {
                var response = ApiResponse<string>.Fail(ex.Message, 400);
                return StatusCode(400, response);
            }
            catch (Exception ex)
            {
                var response = ApiResponse<string>.Fail($"Failed to update order: {ex.Message}", 500);
                return StatusCode(500, response);
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var username = User.Identity?.Name ?? "SYSTEM";
                await _vpms.DeleteOrderAsync(id, username);

                var response = ApiResponse<string>.Success(null!, "Order deleted successfully.");
                return StatusCode(200, response);
            }
            catch (ArgumentException ex)
            {
                var response = ApiResponse<string>.Fail(ex.Message, 404);
                return StatusCode(404, response);
            }
            catch (InvalidOperationException ex)
            {
                var response = ApiResponse<string>.Fail(ex.Message, 400);
                return StatusCode(400, response);
            }
            catch (Exception ex)
            {
                var response = ApiResponse<string>.Fail($"Failed to delete order: {ex.Message}", 500);
                return StatusCode(500, response);
            }
        }

        [HttpPost("{id:int}/convert-to-sale")]
        public async Task<IActionResult> ConvertToSale(int id, [FromBody] OrderConvertToSaleRequest req)
        {
            try
            {
                var username = User.Identity?.Name ?? "SYSTEM";
                var convertReq = req with { OrderId = id };
                var sale = await _vpms.ConvertOrderToSaleAsync(convertReq, username);

                var response = ApiResponse<object>.Success(new { sale.SaleId, sale.SaleNumber, sale.TotalAmount, sale.PaidAmount }, "Order converted into Sale successfully!", 201);
                return StatusCode(201, response);
            }
            catch (ArgumentException ex)
            {
                var response = ApiResponse<string>.Fail(ex.Message, 400);
                return StatusCode(400, response);
            }
            catch (InvalidOperationException ex)
            {
                var response = ApiResponse<string>.Fail(ex.Message, 400);
                return StatusCode(400, response);
            }
            catch (Exception ex)
            {
                var response = ApiResponse<string>.Fail($"Failed to convert order: {ex.Message}", 500);
                return StatusCode(500, response);
            }
        }
    }
}
