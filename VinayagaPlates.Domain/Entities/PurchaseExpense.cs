using System;
using System.Collections.Generic;

namespace VinayagaPlates.Domain.Entities
{
    public class PurchaseExpense : IAuditable
    {
        public int PurchaseExpenseId { get; set; }
        public int PurchaseId { get; set; }
        public Purchase Purchase { get; set; }
        public int ExpenseTypeId { get; set; } // Transport, Loading, Unloading, Handling, etc.
        public decimal Amount { get; set; }
        public string AllocationMethod { get; set; } = "QUANTITY_BASED"; // QUANTITY_BASED, VALUE_BASED
        public string Description { get; set; } = "";
        public string CreatedBy { get; set; } = "SYSTEM";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string UpdatedBy { get; set; } = "";
        public DateTime? UpdatedAt { get; set; }

        public ICollection<PurchaseExpenseAllocation> Allocations { get; set; } = new List<PurchaseExpenseAllocation>();
    }
}
