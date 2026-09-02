using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VinayagaPlates.Domain.Entities
{
    public class Sale : IAuditable
    {
        public int SaleId { get; set; }
        public string SaleNumber { get; set; }
        public int CustomerId { get; set; }

        [JsonIgnore]
        public Customer Customer { get; set; }
        public DateTime SaleDate { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public string PaymentStatus { get; set; }
        public string Status { get; set; }
        public string CreatedBy { get; set; } = "SYSTEM";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string UpdatedBy { get; set; } = "";
        public DateTime? UpdatedAt { get; set; }

        public ICollection<SaleDetail> Details { get; set; } = new List<SaleDetail>();
    }
}
