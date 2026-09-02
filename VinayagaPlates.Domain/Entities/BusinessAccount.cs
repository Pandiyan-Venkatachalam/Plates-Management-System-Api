using System;
using System.Collections.Generic;

namespace VinayagaPlates.Domain.Entities
{
    public class BusinessAccount : IAuditable
    {
        public int AccountId { get; set; }
        public string AccountName { get; set; }
        public string AccountType { get; set; }
        public string CreatedBy { get; set; } = "SYSTEM";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string UpdatedBy { get; set; } = "";
        public DateTime? UpdatedAt { get; set; }

        public ICollection<AccountTransaction> Transactions { get; set; } = new List<AccountTransaction>();
    }
}
