using System.ComponentModel.DataAnnotations;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;

namespace CafeChain.Models.Inventories.Stock
{
    /// <summary>Single authority for quantity actually fulfilled by a confirmed warehouse document line.</summary>
    public class RestockFulfillmentPosting
    {
        public int RestockFulfillmentPostingId { get; set; }
        public int RestockRequestId { get; set; }
        public string SourceDocumentType { get; set; } = string.Empty;
        public int SourceDocumentId { get; set; }
        public int SourceDocumentLineId { get; set; }
        public int? IngredientId { get; set; }
        public int? PreparedItemId { get; set; }
        public decimal Quantity { get; set; }
        public int BaseUnitId { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public virtual RestockRequest RestockRequest { get; set; } = null!;
        public virtual Ingredient? Ingredient { get; set; }
        public virtual PreparedItem? PreparedItem { get; set; }
        public virtual Unit BaseUnit { get; set; } = null!;
    }
}
