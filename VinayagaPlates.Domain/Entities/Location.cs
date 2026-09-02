using System;

namespace VinayagaPlates.Domain.Entities
{
    public class Location : IAuditable
    {
        public int LocationId { get; set; }
        public string LocationName { get; set; }
        public bool IsActive { get; set; } = true;
        public string CreatedBy { get; set; } = "SYSTEM";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string UpdatedBy { get; set; } = "";
        public DateTime? UpdatedAt { get; set; }
    }
}
