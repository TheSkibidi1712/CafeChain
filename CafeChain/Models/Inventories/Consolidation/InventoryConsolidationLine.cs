using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Inventories.Consolidation
{
    /// <summary>Issue #123 — per-row before/after evidence for a consolidation run.</summary>
    public class InventoryConsolidationLine
    {
        public int InventoryConsolidationLineId { get; set; }

        public int InventoryConsolidationRunId { get; set; }

        public int StoreInventoryId { get; set; }

        public InventoryConsolidationLineRole LineRole { get; set; }

        public int PreparedItemId { get; set; }

        public int? SourceRecipeId { get; set; }

        public decimal BeforeAvailableQty { get; set; }

        public decimal BeforeReservedQty { get; set; }

        public decimal? BeforeMinStockLevel { get; set; }

        public decimal? BeforeMaxNegativeQty { get; set; }

        public BtpIdentityState? BeforeIdentityState { get; set; }

        public InventoryQuantitySemanticsStatus? BeforeQuantitySemantics { get; set; }

        public decimal? ApprovedConversionFactor { get; set; }

        public int? ApprovedConversionFromUnitId { get; set; }

        public int? ApprovedConversionToUnitId { get; set; }

        public decimal ConvertedAvailableQty { get; set; }

        public decimal ConvertedReservedQty { get; set; }

        public decimal AfterAvailableQty { get; set; }

        public decimal AfterReservedQty { get; set; }

        public string EvidenceType { get; set; } = string.Empty;

        public string? EvidenceReference { get; set; }

        public bool IsTargetCreated { get; set; }

        public virtual InventoryConsolidationRun ConsolidationRun { get; set; } = null!;
        public virtual StoreInventory StoreInventory { get; set; } = null!;
        public virtual PreparedItem PreparedItem { get; set; } = null!;
    }
}
