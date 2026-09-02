using System;

namespace VinayagaPlates.Domain.Entities
{
    public class PurchaseExpenseAllocation
    {
        public int AllocationId { get; set; }
        public int PurchaseExpenseId { get; set; }
        public PurchaseExpense PurchaseExpense { get; set; }
        public int PurchaseDetailId { get; set; }
        public PurchaseDetail PurchaseDetail { get; set; }
        public decimal AllocatedAmount { get; set; }
        public string CreatedBy { get; set; } = "SYSTEM";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
