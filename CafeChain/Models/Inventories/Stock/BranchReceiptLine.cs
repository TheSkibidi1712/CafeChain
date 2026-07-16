using System.ComponentModel.DataAnnotations;
using CafeChain.Models.Drinks;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Inventories.Transfers;
using CafeChain.Models.Inventories.Procurement;

namespace CafeChain.Models.Inventories.Stock
{
    /// <summary>
    /// Issue #128 — one restock receipt line with durable cost/quantity snapshots.
    /// Inventory posts only when parent BranchReceipt is CONFIRMED.
    /// </summary>
    public class BranchReceiptLine
    {
        public int BranchReceiptLineId { get; set; }

        public int BranchReceiptId { get; set; }

        public int? RestockRequestId { get; set; }
        public int? PurchaseOrderLineId { get; set; }
        public int? SourceInventoryTransferDetailId { get; set; }
        public long? SourceTransferCostAllocationId { get; set; }

        public int? RestockRequestFulfillmentId { get; set; }

        public int? IngredientId { get; set; }

        public int? PreparedItemId { get; set; }

        /// <summary>Compatibility metadata only (legacy BTP). New lines: null when PreparedItem.</summary>
        public int? RecipeId { get; set; }

        public decimal InputQuantity { get; set; }

        public int InputUnitId { get; set; }

        public decimal ReceivedBaseQuantity { get; set; }
        public decimal RejectedBaseQuantity { get; set; }
        public string? RejectionReason { get; set; }
        public string? RejectionIssueType { get; set; }

        public int BaseUnitId { get; set; }

        public int? SupplierId { get; set; }

        public int? IngredientSupplierId { get; set; }

        public decimal? ActualPackagePrice { get; set; }

        public decimal? PackageQuantitySnapshot { get; set; }

        public int? PackageUnitIdSnapshot { get; set; }

        public decimal BaseUnitCostSnapshot { get; set; }

        public decimal LineTotalCost { get; set; }

        public int? InventoryTransactionId { get; set; }

        public DateTime CreatedAt { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public virtual BranchReceipt BranchReceipt { get; set; } = null!;
        public virtual RestockRequest? RestockRequest { get; set; }
        public virtual PurchaseOrderLine? PurchaseOrderLine { get; set; }
        public virtual InventoryTransferDetail? SourceInventoryTransferDetail { get; set; }
        public virtual InventoryTransferCostAllocation? SourceTransferCostAllocation { get; set; }
        public virtual RestockRequestFulfillment? RestockRequestFulfillment { get; set; }
        public virtual Ingredient? Ingredient { get; set; }
        public virtual PreparedItem? PreparedItem { get; set; }
        public virtual Recipe? Recipe { get; set; }
        public virtual Unit InputUnit { get; set; } = null!;
        public virtual Unit BaseUnit { get; set; } = null!;
        public virtual Supplier? Supplier { get; set; }
        public virtual IngredientSupplier? IngredientSupplier { get; set; }
        public virtual Unit? PackageUnitSnapshot { get; set; }
        public virtual InventoryTransaction? InventoryTransaction { get; set; }
    }
}
