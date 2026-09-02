using System.Collections.Generic;

namespace VinayagaPlates.Domain.Entities
{
    public class OrderDetail
    {
        public int OrderDetailId { get; set; }
        public int OrderId { get; set; }
        public Order Order { get; set; }
        public int ProductId { get; set; }
        public Product Product { get; set; }
        public int OrderedQuantity { get; set; }
        public int ReservedQuantity { get; set; }
        public int DeliveredQuantity { get; set; }
        public decimal SellingPrice { get; set; }

        public ICollection<StockReservation> Reservations { get; set; } = new List<StockReservation>();
    }
}
