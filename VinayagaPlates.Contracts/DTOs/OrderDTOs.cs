using System;
using System.Collections.Generic;

namespace VinayagaPlates.Contracts.DTOs
{
    public record OrderDetailRequest(
        int ProductId,
        int OrderedQuantity,
        decimal SellingPrice);

    public record OrderCreateRequest(
        int CustomerId,
        DateTime OrderDate,
        DateTime ExpectedDate,
        string Priority,
        string Notes,
        List<OrderDetailRequest> Details);

    public record OrderUpdateRequest(
        int CustomerId,
        DateTime OrderDate,
        DateTime ExpectedDate,
        string Priority,
        string Status,
        string Notes,
        List<OrderDetailRequest> Details);

    public record OrderDetailResponse(
        int OrderDetailId,
        int OrderId,
        int ProductId,
        string ProductName,
        string? ProductCode,
        int OrderedQuantity,
        int ReservedQuantity,
        int DeliveredQuantity,
        decimal SellingPrice,
        decimal Subtotal);

    public record OrderResponse(
        int OrderId,
        string OrderNo,
        int CustomerId,
        string CustomerName,
        string? CustomerPhone,
        DateTime OrderDate,
        DateTime ExpectedDate,
        string Status,
        string Priority,
        string Notes,
        decimal TotalAmount,
        int TotalItems,
        string CreatedBy,
        DateTime CreatedAt,
        string? UpdatedBy,
        DateTime? UpdatedAt,
        List<OrderDetailResponse> Details);

    public record OrderConvertToSaleRequest(
        int OrderId,
        decimal PaidAmount,
        string PaymentMethodAccountName,
        string? Notes = null);
}
