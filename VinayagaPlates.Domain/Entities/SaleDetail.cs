using System.Text.Json.Serialization;

namespace VinayagaPlates.Domain.Entities
{
    public class SaleDetail
    {
        public int SaleDetailId { get; set; }
        public int SaleId { get; set; }

        [JsonIgnore]
        public Sale Sale { get; set; }
        public int ProductId { get; set; }
        public Product Product { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public int BatchId { get; set; }

        [JsonIgnore]
        public InventoryBatch Batch { get; set; }
    }
}
