using System;
using System.Collections.Generic;

namespace VinayagaPlates.Domain.Entities
{
    public class Purchase : IAuditable
    {
        public int PurchaseId { get; set; }
        public string PurchaseNumber { get; set; }
        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; }
        public DateTime PurchaseDate { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public string PaymentStatus { get; set; }
        public string Status { get; set; }
        public string CreatedBy { get; set; } = "SYSTEM";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string UpdatedBy { get; set; } = "";
        public DateTime? UpdatedAt { get; set; }

        public ICollection<PurchaseDetail> Details { get; set; } = new List<PurchaseDetail>();
    }
}
