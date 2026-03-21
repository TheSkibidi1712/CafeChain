using CafeChain.Application.DTOs;

namespace CafeChain.ViewModels
{
    public class CartViewModel
    {
        public List<CartItem> Items { get; set; } = new List<CartItem>();

        // Tạm tính (Tổng tiền các món)
        public decimal SubTotal => Items.Sum(i => i.Total);

        // Phí vận chuyển (Tạm gán cứng 15k như Figma, sau này có thể làm logic tính theo km)
        public decimal ShippingFee { get; set; } = 15000;

        // Giảm giá (Tạm gán 0, sau này ráp logic Voucher vào)
        public decimal Discount { get; set; } = 0;

        // Tổng cộng cuối cùng
        public decimal GrandTotal => SubTotal + ShippingFee - Discount;
    }
}