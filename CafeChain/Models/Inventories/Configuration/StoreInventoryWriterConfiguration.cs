using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Stores;
using System.ComponentModel.DataAnnotations;

namespace CafeChain.Models.Inventories.Configuration
{
    public class StoreInventoryWriterConfiguration
    {
        public int StoreId { get; set; }
        public InventoryWriterMode WriterMode { get; set; } = InventoryWriterMode.LegacyRecipe;
        public bool HasEverActivatedPreparedItem { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public virtual Store Store { get; set; } = null!;
    }
}
