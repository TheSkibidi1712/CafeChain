using CafeChain.Models.Inventories.Production;

namespace CafeChain.Models.Inventories.Costing
{
    /// <summary>
    /// FIFO cost evidence layer. Exactly one inventory identity:
    /// IngredientId XOR PreparedItemId (#132).
    /// </summary>
    public class InventoryCostLayer
    {
        public int InventoryCostLayerId { get; set; }

        /// <summary>Ingredient stock identity. Null when PreparedItem layer.</summary>
        public int? IngredientId { get; set; }

        /// <summary>PreparedItem stock identity. Null when Ingredient layer.</summary>
        public int? PreparedItemId { get; set; }

        public int StoreId { get; set; }

        public decimal Quantity { get; set; }
        public decimal RemainingQuantity { get; set; }

        public decimal UnitCost { get; set; }

        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Production output layer linkage: one layer per ProductionRun when set.
        /// </summary>
        public int? SourceProductionRunId { get; set; }

        public virtual ProductionRun? SourceProductionRun { get; set; }
    }
}
