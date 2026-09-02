using System.Text.Json.Serialization;

namespace VinayagaPlates.Domain.Entities
{
    public class PurchaseDetail
    {
        public int PurchaseDetailId { get; set; }
        public int PurchaseId { get; set; }

        [JsonIgnore]
        public Purchase Purchase { get; set; }
        public int ProductId { get; set; }
        public Product Product { get; set; }
        public int Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public int? BatchId { get; set; }
    }
}
