using System;
using System.Collections.Generic;

namespace VinayagaPlates.Domain.Entities
{
    public class Product : IAuditable
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public int CategoryId { get; set; }
        public ProductCategory Category { get; set; }
        public int VariantId { get; set; }
        public ProductVariant Variant { get; set; }
        public int UnitId { get; set; }
        public ProductUnit Unit { get; set; }
        public int MinStockAlert { get; set; } = 500;
        public bool IsActive { get; set; } = true;
        public string CreatedBy { get; set; } = "SYSTEM";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string UpdatedBy { get; set; } = "";
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; } = false;

        public ICollection<InventoryBatch> InventoryBatches { get; set; } = new List<InventoryBatch>();
    }
}
