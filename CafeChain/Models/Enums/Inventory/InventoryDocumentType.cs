namespace CafeChain.Models.Enums.Inventory
{
    public enum InventoryDocumentType
    {
        IMPORT = 1,
        EXPORT = 2,
        WASTE = 3,
        STOCK_TAKE = 4,
        PRODUCTION_IN = 5,
        PRODUCTION_OUT = 6,
        SALES_DEDUCTION = 7,
        ADJUSTMENT_IN = 8,
        [Obsolete("Chuyển kho nội bộ đã được tách sang InventoryTransfer. Không dùng để tạo chứng từ mới.")]
        INTERNAL_IMPORT = 9
    }
}
