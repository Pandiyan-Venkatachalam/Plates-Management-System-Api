using System;

namespace VinayagaPlates.Domain.Entities
{
    public class AccountTransaction
    {
        public int TransactionId { get; set; }
        public int AccountId { get; set; }
        public BusinessAccount Account { get; set; }
        public string TransactionType { get; set; }
        public decimal Amount { get; set; }
        public string ReferenceType { get; set; }
        public string ReferenceId { get; set; }
        public string Description { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
