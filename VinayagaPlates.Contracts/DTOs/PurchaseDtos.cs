using System;
using System.Collections.Generic;

namespace VinayagaPlates.Contracts.DTOs
{
    public record PurchaseDetailRequest(int ProductId, int Quantity, decimal UnitCost);

    public record PurchaseExpenseRequest(int ExpenseTypeId, decimal Amount, string AllocationMethod, string Description);

    public record PaymentContributionRequest(string AccountName, decimal Amount);

    public record PurchaseCreateRequest(
        int SupplierId,
        DateTime PurchaseDate,
        List<PurchaseDetailRequest> Details,
        List<PurchaseExpenseRequest> Expenses,
        decimal PaidAmount,
        string PaymentMethodAccountName,
        decimal Adjustment = 0,
        List<PaymentContributionRequest>? PaymentContributions = null);

    public record InventoryBatchResponse(
        int BatchId,
        string BatchNumber,
        int ProductId,
        string ProductName,
        int InitialQuantity,
        int CurrentQuantity,
        decimal UnitCost,
        decimal LandedUnitCost,
        DateTime CreatedAt);

    public record StockAdjustmentRequest(
        int BatchId,
        int NewQuantity,
        string Description);

    public record SupplierCreateRequest(
        string SupplierName,
        string? ContactPerson,
        string? Phone,
        string? Email,
        string? Address);

    public record SupplierResponse(
        int SupplierId,
        string SupplierName,
        string? ContactPerson,
        string? Phone,
        string? Email,
        string? Address);

    public record BatchCreateRequest(
        string BatchNumber,
        int ProductId,
        int InitialQuantity,
        int CurrentQuantity,
        decimal UnitCost,
        int? LocationId,
        DateTime ReceivedDate,
        decimal LandedUnitCost,
        decimal TotalLandedCost,
        string Status);

    public record BatchUpdateRequest(
        string BatchNumber,
        int ProductId,
        int InitialQuantity,
        int CurrentQuantity,
        decimal UnitCost,
        int? LocationId,
        DateTime ReceivedDate,
        decimal LandedUnitCost,
        decimal TotalLandedCost,
        string Status);

    public record PurchaseUpdateRequest(
        int SupplierId,
        DateTime PurchaseDate,
        List<PurchaseDetailRequest> Details,
        decimal TotalAmount,
        decimal PaidAmount,
        string PaymentStatus,
        string Status);
}
