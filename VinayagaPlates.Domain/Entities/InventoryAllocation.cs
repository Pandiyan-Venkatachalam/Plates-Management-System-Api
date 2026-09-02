using System;

namespace VinayagaPlates.Domain.Entities
{
    public class InventoryAllocation
    {
        public int AllocationId { get; set; }
        public int SaleDetailId { get; set; }
        public SaleDetail SaleDetail { get; set; }
        public int BatchId { get; set; }
        public InventoryBatch Batch { get; set; }
        public int Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost { get; set; }
        public string CreatedBy { get; set; } = "SYSTEM";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
