using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VinayagaPlates.Domain.Entities
{
    public class Supplier : IAuditable
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; }
        public string ContactPerson { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public string CreatedBy { get; set; } = "SYSTEM";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string UpdatedBy { get; set; } = "";
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; } = false;

        [JsonIgnore]
        public ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
    }
}
