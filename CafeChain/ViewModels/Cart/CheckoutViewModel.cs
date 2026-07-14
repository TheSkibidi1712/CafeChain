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

        /// <summary>Compatibility — non-null rejected server-side (FEATURE_NOT_AVAILABLE).</summary>
        public int? SelectedVoucherId { get; set; }

        /// <summary>Compatibility — points &gt; 0 rejected server-side (FEATURE_NOT_AVAILABLE).</summary>
        public int? PointsUsed { get; set; }

        public int OrderTypeId { get; set; } = 3; // Default: Delivery

        public int AssignedStoreId { get; set; }

        // [FIX 2 - Phase 4] Checksum chống Bóng Ma Đa Tab
        public decimal ExpectedTotal { get; set; }

        // ====== DISPLAY DATA (Calculated / Read-only) ======
        public List<CartItemViewModel> Items { get; set; } = new List<CartItemViewModel>();

        public decimal SubTotal { get; set; }

        public decimal ShippingFee { get; set; } = 15000;

        /// <summary>Historical/display only — always 0 for new checkouts (soft-removal).</summary>
        public decimal VoucherDiscount { get; set; }

        /// <summary>Historical/display only — always 0 for new checkouts (soft-removal).</summary>
        public decimal PointDiscount { get; set; }

        /// <summary>Selling total without voucher/loyalty: SubTotal + ShippingFee.</summary>
        public decimal Total => SubTotal + ShippingFee;
    }
}
