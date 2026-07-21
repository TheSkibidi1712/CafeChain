namespace CafeChain.Models.Enums.Inventory
{
    public enum InventoryTransactionTypeEnum
    {
        IMPORT = 1,
        EXPORT = 2,
        WASTE = 3,
        STOCK_TAKE = 4,
        PRODUCTION_IN = 5,
        PRODUCTION_OUT = 6,
        SALES_DEDUCTION = 7,
        ADJUSTMENT_IN = 8,
        ADJUSTMENT_OUT = 9,
        OUT_TRANSFER = 10,
        IN_TRANSFER = 11,
        /// <summary>Issue #123 — consolidate qty off legacy/source BTP row.</summary>
        CONSOLIDATION_OUT = 12,
        /// <summary>Issue #123 — consolidate qty onto canonical target row.</summary>
        CONSOLIDATION_IN = 13,
        /// <summary>Issue #128 — branch restock receipt stock-in (only on BranchReceipt confirm).</summary>
        BRANCH_RECEIPT_IN = 14,

        /// <summary>Issue #134 — full-order cash refund stock return (quantity absolute positive).</summary>
        SALES_RETURN = 15,

        /// <summary>SC-02 — source warehouse confirms physical transfer return.</summary>
        TRANSFER_RETURN_IN = 16
    }
}
