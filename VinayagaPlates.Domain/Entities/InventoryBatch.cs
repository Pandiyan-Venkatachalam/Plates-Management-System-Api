using System;
using System.Collections.Generic;

namespace VinayagaPlates.Domain.Entities
{
    public class InventoryBatch : IAuditable
    {
        public int BatchId { get; set; }
        public string BatchNumber { get; set; }
        public int ProductId { get; set; }
        public Product Product { get; set; }
        public int InitialQuantity { get; set; }
        public int CurrentQuantity { get; set; }
        public decimal UnitCost { get; set; }
        public string CreatedBy { get; set; } = "SYSTEM";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string UpdatedBy { get; set; } = "";
        public DateTime? UpdatedAt { get; set; }

        // Enterprise Location-Aware & Cost Mappings
        public int? LocationId { get; set; }
        public Location Location { get; set; }
        public DateTime ReceivedDate { get; set; } = DateTime.UtcNow;
        public decimal LandedUnitCost { get; set; } = 0;
        public decimal TotalLandedCost { get; set; } = 0;
        public string Status { get; set; } = "PENDING"; // PENDING, CALCULATED, FINALIZED, ADJUSTED

        public ICollection<InventoryMovement> Movements { get; set; } = new List<InventoryMovement>();
    }
}
