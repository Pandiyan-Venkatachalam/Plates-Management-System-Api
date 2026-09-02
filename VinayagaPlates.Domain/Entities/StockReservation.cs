using System;

namespace VinayagaPlates.Domain.Entities
{
    public class StockReservation : IAuditable
    {
        public int ReservationId { get; set; }
        public int OrderDetailId { get; set; }
        public OrderDetail OrderDetail { get; set; }
        public int ProductId { get; set; }
        public Product Product { get; set; }
        public int LocationId { get; set; }
        public Location Location { get; set; }
        public int Quantity { get; set; }
        public string Status { get; set; } = "ACTIVE"; // ACTIVE, RELEASED, CONVERTED, CANCELLED
        public DateTime ReservedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReleasedAt { get; set; }
        public string CreatedBy { get; set; } = "SYSTEM";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string UpdatedBy { get; set; } = "";
        public DateTime? UpdatedAt { get; set; }
    }
}
