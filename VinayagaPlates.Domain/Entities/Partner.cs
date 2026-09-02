using System;
using System.Collections.Generic;

namespace VinayagaPlates.Domain.Entities
{
    public class Partner : IAuditable
    {
        public int PartnerId { get; set; }
        public string PartnerName { get; set; }
        public string ContactPhone { get; set; }
        public string CreatedBy { get; set; } = "SYSTEM";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string UpdatedBy { get; set; } = "";
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; } = false;

        public ICollection<PartnerLedger> Ledgers { get; set; } = new List<PartnerLedger>();
    }
}
