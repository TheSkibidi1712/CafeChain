using System.Collections.Generic;

namespace CafeChain.Application.DTOs.POS
{
    /// <summary>
    /// DTO chính cho thanh toán POS tại quầy
    /// </summary>
    public class POSOrderCommitDto
    {
        public List<POSOrderItemDto> Items { get; set; } = new List<POSOrderItemDto>();

        /// <summary>Khách hàng thành viên (nullable nếu khách vãng lai)</summary>
        public int? CustomerId { get; set; }

        /// <summary>Mã voucher giảm giá (nullable)</summary>
        public string? VoucherCode { get; set; }

        /// <summary>Số điểm loyalty khách muốn dùng</summary>
        public int PointsUsed { get; set; }

        /// <summary>Danh sách các dòng thanh toán hỗn hợp (Split Payments)</summary>
        public List<PaymentLineDto> Payments { get; set; } = new List<PaymentLineDto>();

        /// <summary>[Deprecated] Phương thức thanh toán đơn lẻ — dùng Payments thay thế</summary>
        public int PaymentMethodId { get; set; } = 1;

        /// <summary>Loại đơn: 1=DineIn, 2=TakeAway</summary>
        public int OrderTypeId { get; set; } = 1;

        /// <summary>Tiền khách đưa (để tính tiền thừa)</summary>
        public decimal ReceivedAmount { get; set; }

        /// <summary>Ghi chú đơn hàng</summary>
        public string? Note { get; set; }
    }

    /// <summary>
    /// Một dòng thanh toán trong Split Payments
    /// </summary>
    public class PaymentLineDto
    {
        /// <summary>1=Tiền mặt, 2=QR Chuyển khoản VietQR/PayOS</summary>
        public int PaymentMethodId { get; set; }
        public decimal Amount { get; set; }
    }

    /// <summary>
    /// Một dòng item trong giỏ hàng POS
    /// </summary>
    public class POSOrderItemDto
    {
        public int DrinkId { get; set; }
        public int? SizeId { get; set; }
        public int Quantity { get; set; } = 1;
        public string? Note { get; set; }
        public List<POSOrderToppingDto> Toppings { get; set; } = new List<POSOrderToppingDto>();
    }

    /// <summary>
    /// Topping cho một item POS
    /// </summary>
    public class POSOrderToppingDto
    {
        public int ToppingId { get; set; }
    }

    /// <summary>
    /// DTO request đóng ca két tiền
    /// </summary>
    public class CloseShiftRequestDto
    {
        /// <summary>Tiền mặt thực tế đếm được trong hộc kéo</summary>
        public decimal ActualEndingCash { get; set; }

        /// <summary>Lý do chênh lệch (bắt buộc nếu lệch != 0)</summary>
        public string? DiscrepancyReason { get; set; }
    }
}
