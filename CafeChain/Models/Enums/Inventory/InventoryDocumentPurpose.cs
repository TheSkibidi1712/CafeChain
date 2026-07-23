namespace CafeChain.Models.Enums.Inventory
{
    public enum InventoryDocumentPurpose
    {
        NONE = 0,

        // ================= NHẬP KHO =================
        IMPORT_PURCHASE = 1,     // nhập từ NCC
        [Obsolete("Chuyển kho nội bộ đã được tách sang InventoryTransfer. Không dùng để tạo chứng từ mới.")]
        IMPORT_INTERNAL = 2,     // nhập nội bộ
        // Legacy/read-only ở tầng nghiệp vụ. Kiểm kê tự sinh transaction ADJUSTMENT_IN khi chênh lệch dương.
        IMPORT_ADJUSTMENT = 3,

        // ================= XUẤT KHO =================
        SALE = 5,
        [Obsolete("Chuyển kho nội bộ đã được tách sang InventoryTransfer. Không dùng để tạo chứng từ mới.")]
        INTERNAL_OUT = 6,
        // Các giá trị legacy phải giữ nguyên mã số để đọc dữ liệu lịch sử.
        GIFT = 7,
        DEBT = 8,
        SAMPLE = 9,
        // Legacy/read-only. Kiểm kê tự sinh transaction ADJUSTMENT_OUT khi chênh lệch âm.
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
