using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VinayagaPlates.Domain.Entities
{
    public class ProductCategory : IAuditable
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public bool IsActive { get; set; } = true;
        public string CreatedBy { get; set; } = "SYSTEM";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string UpdatedBy { get; set; } = "";
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; } = false;

        [JsonIgnore]
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
