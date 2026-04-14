namespace CafeChain.Models.Enums.Inventory
{
    public enum InventoryDocumentPurpose
    {
        NONE = 0,

        // ===== NHẬP =====
        PURCHASE = 1,        // nhập mua

        // ===== XUẤT =====
        SALE = 2,            // bán hàng
        INTERNAL = 3,        // chuyển nội bộ
        GIFT = 4,            // tặng
        DEBT = 5,            // cho nợ

        // ===== HỦY =====
        DAMAGED = 6,
        EXPIRED = 7
    }
}
