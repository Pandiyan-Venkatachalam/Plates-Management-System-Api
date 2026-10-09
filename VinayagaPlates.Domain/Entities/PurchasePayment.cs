using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VinayagaPlates.Domain.Entities
{
    public class PurchasePayment
    {
        [Key]
        public int PaymentId { get; set; }

        public int PurchaseId { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public int AccountId { get; set; } // Which account this was paid from

        [StringLength(50)]
        public string PaymentMethod { get; set; } = "CASH";

        [StringLength(255)]
        public string Notes { get; set; }

        [StringLength(100)]
        public string CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("PurchaseId")]
        public Purchase Purchase { get; set; }

        [ForeignKey("AccountId")]
        public BusinessAccount Account { get; set; }
    }
}
