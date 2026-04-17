namespace CafeChain.Models.Enums.Inventory
{
    public enum InventoryDocumentPurpose
    {
        NONE = 0,

        // ================= NHẬP KHO =================
        IMPORT_PURCHASE = 1,     // nhập từ NCC
        IMPORT_INTERNAL = 2,     // nhập nội bộ
        IMPORT_ADJUSTMENT = 3,   // điều chỉnh tăng

        // ================= XUẤT KHO =================
        SALE = 5,
        INTERNAL_OUT = 6,
        GIFT = 7,
        DEBT = 8,
        SAMPLE = 9,
        ADJUSTMENT_OUT = 10,

        // ================= KIỂM KÊ =================
        STOCK_TAKE = 11,         // kiểm kê (chung)

        // ================= HỦY =================
        DAMAGED = 12,
        EXPIRED = 13,
        BROKEN = 14,
        CONTAMINATED = 15,
        LOST = 16
    }
}
