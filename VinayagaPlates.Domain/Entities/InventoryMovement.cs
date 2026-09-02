using System;

namespace VinayagaPlates.Domain.Entities
{
    public class InventoryMovement
    {
        public int MovementId { get; set; }
        public int ProductId { get; set; }
        public Product Product { get; set; }
        public int? BatchId { get; set; }
        public InventoryBatch Batch { get; set; }
        public int? LocationId { get; set; }
        public Location Location { get; set; }
        public string MovementType { get; set; } // PURCHASE_IN, SALE_OUT, TRANSFER_IN, TRANSFER_OUT, etc.
        public string Direction { get; set; } // IN, OUT
        public int Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost { get; set; }
        public string ReferenceType { get; set; }
        public string ReferenceId { get; set; }
        public string Description { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
