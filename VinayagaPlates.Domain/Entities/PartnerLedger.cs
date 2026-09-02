using System;

namespace VinayagaPlates.Domain.Entities
{
    public class PartnerLedger
    {
        public int LedgerId { get; set; }
        public int PartnerId { get; set; }
        public Partner Partner { get; set; }
        public string TransactionType { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
