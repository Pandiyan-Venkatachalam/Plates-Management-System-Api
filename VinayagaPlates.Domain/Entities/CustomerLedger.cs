using System;

namespace VinayagaPlates.Domain.Entities
{
    public class CustomerLedger : IAuditable
    {
        public int LedgerId { get; set; }
        public int CustomerId { get; set; }
        public Customer Customer { get; set; }
        public string TransactionType { get; set; } // SALE, PAYMENT, CREDIT, DEBIT, RETURN, ADJUSTMENT
        public decimal Amount { get; set; }
        public int? ReferenceId { get; set; }
        public string ReferenceType { get; set; } // SALE, PAYMENT, ADJUSTMENT
        public string Description { get; set; } = "";
        public string CreatedBy { get; set; } = "SYSTEM";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string UpdatedBy { get; set; } = "";
        public DateTime? UpdatedAt { get; set; }
    }
}
