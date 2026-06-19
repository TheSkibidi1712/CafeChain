using CafeChain.Application.DTOs;

namespace CafeChain.ViewModels
{
    public class CartViewModel
    {
        public List<CartItem> Items { get; set; } = new List<CartItem>();

        // Tạm tính (Tổng tiền các món)
        public decimal SubTotal => Items.Sum(i => i.Total);

        // Phí vận chuyển (Tạm gán cứng 15k như Figma)
        public decimal ShippingFee { get; set; } = 15000;

        // Giảm giá từ Voucher
        public decimal VoucherDiscount { get; set; } = 0;

        // Giảm giá từ điểm
        public decimal PointDiscount { get; set; } = 0;

        // Số điểm dùng
        public int PointsUsed { get; set; } = 0;

        // Tổng giảm giá
        public decimal Discount => VoucherDiscount + PointDiscount;

        // Tổng cộng cuối cùng
        public decimal GrandTotal => SubTotal + ShippingFee - Discount;
    }
}