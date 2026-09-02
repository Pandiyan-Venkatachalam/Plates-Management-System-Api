namespace VinayagaPlates.Contracts.DTOs
{
    public record CategoryCreateRequest(string CategoryName);
    
    public record VariantCreateRequest(string VariantName);
    
    public record UnitCreateRequest(string UnitName);

    public record ProductCreateRequest(
        string ProductCode, 
        string ProductName, 
        int CategoryId, 
        int VariantId, 
        int UnitId, 
        int MinStockAlert);

    public record ProductResponse(
        int ProductId,
        string ProductCode,
        string ProductName,
        int CategoryId,
        string CategoryName,
        int VariantId,
        string VariantName,
        int UnitId,
        string UnitName,
        int CurrentStock,
        int MinStockAlert,
        bool IsActive);

    public record StockAlertResponse(
        int ProductId,
        string ProductName,
        int CurrentStock,
        int MinStockAlert);
}
