using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace CafeChain.ViewModels.Cart
{
    public class CheckoutViewModel
    {
        // ====== FORM DATA (User Inputs - ID Based) ======
        [Required(ErrorMessage = "Vui lòng chọn địa chỉ nhận hàng")]
        public int SelectedAddressId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn số điện thoại liên hệ")]
        public int SelectedPhoneId { get; set; }

        public Guid CheckoutToken { get; set; }

        public string? Note { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn phương thức thanh toán")]
        public int PaymentMethodId { get; set; }

        public string? VoucherCode { get; set; }

        /// Số điểm thưởng khách muốn sử dụng (null = không dùng)
        public int? PointsUsed { get; set; }

        /// Loại đơn hàng: 1=DineIn, 2=TakeAway, 3=Delivery
        public int OrderTypeId { get; set; } = 3; // Default: Delivery

        // [FIX 2 - Phase 4] Checksum chống Bóng Ma Đa Tab
        public decimal ExpectedTotal { get; set; }

        // ====== DISPLAY DATA (Calculated / Read-only) ======
        public List<CartItemViewModel> Items { get; set; } = new List<CartItemViewModel>();

        public decimal SubTotal { get; set; }

        public decimal ShippingFee { get; set; } = 15000;

        public decimal VoucherDiscount { get; set; }

        /// [FIX BUG 1] Số tiền giảm từ điểm thưởng - PHẢI được tính vào Total
        public decimal PointDiscount { get; set; }

        /// [FIX BUG 1] Công thức Total đầy đủ: bao gồm cả Point Discount
        public decimal Total => SubTotal + ShippingFee - VoucherDiscount - PointDiscount;
    }
}
