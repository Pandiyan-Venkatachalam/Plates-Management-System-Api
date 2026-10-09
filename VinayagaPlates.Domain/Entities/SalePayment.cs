using System;
using System.Text.Json.Serialization;

namespace VinayagaPlates.Domain.Entities
{
    public class SalePayment : IAuditable
    {
        public int PaymentId { get; set; }
        public int SaleId { get; set; }

        [JsonIgnore]
        public Sale Sale { get; set; }

        public decimal Amount { get; set; }
        public int AccountId { get; set; }
        
        [JsonIgnore]
        public BusinessAccount Account { get; set; }
        
        public string PaymentMethod { get; set; } = "CASH";
        public string Notes { get; set; } = "";

        // IAuditable fields
        public string CreatedBy { get; set; } = "SYSTEM";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string UpdatedBy { get; set; } = "";
        public DateTime? UpdatedAt { get; set; }
    }
}
