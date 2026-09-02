using System;
using System.Text.Json.Serialization;

namespace VinayagaPlates.Domain.Entities
{
    public class CustomerPricing : IAuditable
    {
        public int CustomerPricingId { get; set; }
        public int CustomerId { get; set; }
        [JsonIgnore]
        public Customer Customer { get; set; }
        public int ProductId { get; set; }
        public Product Product { get; set; }
        public decimal CustomPrice { get; set; }
        public string CreatedBy { get; set; } = "SYSTEM";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string UpdatedBy { get; set; } = "";
        public DateTime? UpdatedAt { get; set; }

        // Date-ranged historical customer price controls
        public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
        public DateTime EffectiveTo { get; set; } = DateTime.MaxValue;
        public string Status { get; set; } = "ACTIVE"; // ACTIVE, EXPIRED
    }
}
