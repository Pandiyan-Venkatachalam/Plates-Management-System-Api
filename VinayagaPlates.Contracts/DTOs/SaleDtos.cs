using System;
using System.Collections.Generic;

namespace VinayagaPlates.Contracts.DTOs
{
    public record SaleDetailRequest(int ProductId, int Quantity, decimal UnitPrice, int BatchId);

    public record SaleCreateRequest(
        int CustomerId,
        DateTime SaleDate,
        List<SaleDetailRequest> Details,
        decimal PaidAmount,
        string PaymentMethodAccountName,
        decimal Adjustment = 0);

    public record SaleUpdateRequest(
        int CustomerId,
        DateTime SaleDate,
        List<SaleDetailRequest> Details,
        decimal TotalAmount,
        decimal PaidAmount,
        string PaymentStatus,
        string Status,
        decimal Adjustment = 0,
        string? PaymentMethodAccountName = null);
}
