using System;

namespace VinayagaPlates.Domain.Entities
{
    public class PartnerTransaction : IAuditable
    {
        public int PartnerTransactionId { get; set; }
        public int PartnerId { get; set; }
        public Partner Partner { get; set; }
        public string TransactionType { get; set; } // CAPITAL_INVESTMENT, CAPITAL_WITHDRAWAL, EXPENSE_PAID_BY_PARTNER, PROFIT_ALLOCATION, PARTNER_SETTLEMENT
        public decimal Amount { get; set; }
        public int? AccountId { get; set; }
        public BusinessAccount Account { get; set; }
        public string ReferenceType { get; set; } // PARTNER, ACCOUNT
        public int? ReferenceId { get; set; }
        public string Description { get; set; } = "";
        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; } = "SYSTEM";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string UpdatedBy { get; set; } = "";
        public DateTime? UpdatedAt { get; set; }
    }
}
