namespace CafeChain.Models.Enums.Inventory
{
    public enum InventoryDocumentPurpose
    {
        NONE = 0,

        // ================= NHẬP KHO =================
        PURCHASE = 1,        // nhập mua từ NCC
        RETURN_FROM_CUSTOMER = 2, // khách trả hàng
        INTERNAL_IN = 3,     // nhập nội bộ (từ chi nhánh khác)
        ADJUSTMENT_IN = 4,   // điều chỉnh tăng (sai sót, kiểm kê)

        // ================= XUẤT KHO =================
        SALE = 5,           // bán hàng
        INTERNAL_OUT = 6,   // chuyển nội bộ
        GIFT = 7,           // tặng
        DEBT = 8,           // cho nợ
        SAMPLE = 9,         // dùng thử / test
        ADJUSTMENT_OUT = 10, // điều chỉnh giảm

        // ================= KIỂM KÊ =================
        STOCK_TAKE_BALANCE = 11, // cân bằng tồn (auto từ kiểm kê)
        STOCK_TAKE_LOSS = 12,    // hao hụt
        STOCK_TAKE_GAIN = 13,    // dư

        // ================= HỦY KHO =================
        DAMAGED = 14,        // hư hỏng
        EXPIRED = 15,        // hết hạn
        BROKEN = 16,         // vỡ / lỗi
        CONTAMINATED = 17,   // nhiễm bẩn (thực phẩm)
        LOST = 18            // thất lạc
    }
}
