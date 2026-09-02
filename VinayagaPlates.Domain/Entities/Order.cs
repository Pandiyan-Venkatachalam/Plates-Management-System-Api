using System;
using System.Collections.Generic;

namespace VinayagaPlates.Domain.Entities
{
    public class Order : IAuditable
    {
        public int OrderId { get; set; }
        public string OrderNo { get; set; }
        public int CustomerId { get; set; }
        public Customer Customer { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime ExpectedDate { get; set; }
        public string Status { get; set; } = "DRAFT"; // DRAFT, CONFIRMED, RESERVED, CONVERTED, CANCELLED
        public string Priority { get; set; } = "NORMAL";
        public string Notes { get; set; } = "";
        public string CreatedBy { get; set; } = "SYSTEM";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string UpdatedBy { get; set; } = "";
        public DateTime? UpdatedAt { get; set; }

        public ICollection<OrderDetail> Details { get; set; } = new List<OrderDetail>();
    }
}
