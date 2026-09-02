using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VinayagaPlates.Domain.Entities
{
    public class Customer : IAuditable
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public string CreatedBy { get; set; } = "SYSTEM";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string UpdatedBy { get; set; } = "";
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; } = false;

        [JsonIgnore]
        public ICollection<CustomerPricing> Pricings { get; set; } = new List<CustomerPricing>();

        [JsonIgnore]
        public ICollection<Sale> Sales { get; set; } = new List<Sale>();
    }
}
