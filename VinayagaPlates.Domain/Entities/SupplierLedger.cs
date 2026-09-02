using System;

namespace VinayagaPlates.Domain.Entities
{
    public class SupplierLedger : IAuditable
    {
        public int LedgerId { get; set; }
        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; }
        public string TransactionType { get; set; } // PURCHASE, PAYMENT, CREDIT, DEBIT, RETURN, ADJUSTMENT
        public decimal Amount { get; set; }
        public int? ReferenceId { get; set; }
        public string ReferenceType { get; set; } // PURCHASE, PAYMENT, ADJUSTMENT
        public string Description { get; set; } = "";
        public string CreatedBy { get; set; } = "SYSTEM";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string UpdatedBy { get; set; } = "";
        public DateTime? UpdatedAt { get; set; }
    }
}
