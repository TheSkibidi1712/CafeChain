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

        /// <summary>
        /// ADR-0002: UUID v4 sinh tại iPad lúc nhấn "Thanh toán" — Idempotency Key cho Offline Order.
        /// Null cho đơn online hoặc legacy client chưa gửi UUID.
        /// </summary>
        public Guid? ClientOrderId { get; set; }

        /// <summary>
        /// Compatibility only — voucher out of product scope.
        /// Non-empty values are rejected with FEATURE_NOT_AVAILABLE (not silently ignored).
        /// </summary>
        public string? VoucherCode { get; set; }

        /// <summary>
        /// Compatibility only — loyalty/điểm thưởng out of product scope.
        /// Values &gt; 0 are rejected with FEATURE_NOT_AVAILABLE (not silently ignored).
        /// </summary>
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

        /// <summary>
        /// ADR-0003: Skip silent print trigger sau commit (cho test/debug).
        /// Default = false → luôn trigger print khi commit thành công.
        /// </summary>
        public bool SkipPrint { get; set; } = false;
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
        public int? StoreMenuItemId { get; set; }
        public int? DrinkSizeId { get; set; }
        public decimal? AcceptedBasePrice { get; set; }
        public decimal? AcceptedUnitPrice { get; set; }
        public string? PriceSource { get; set; }
        public long? CatalogVersion { get; set; }
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
        public decimal? AcceptedPrice { get; set; }
        public string? Name { get; set; }
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

        /// <summary>
        /// PublicId của OTP challenge đã được Ca trưởng duyệt.
        /// Bắt buộc khi chênh lệch két tiền vượt ngưỡng cho phép.
        /// </summary>
        public Guid? OtpChallengePublicId { get; set; }
    }

    /// <summary>
    /// DTO request đóng WorkShift ngoại lệ khi còn Offline Order local chưa sync.
    /// Requires OtpChallengePublicId (inherited from CloseShiftRequestDto).
    /// </summary>
    public class CloseShiftExceptionRequestDto : CloseShiftRequestDto
    {
        /// <summary>Lý do đóng ngoại lệ, bắt buộc.</summary>
        public string? ExceptionReason { get; set; }

        /// <summary>Tóm tắt Offline Order local tại POS, không làm nguồn sự thật backend.</summary>
        public OfflineQueueSummaryDto OfflineQueueSummary { get; set; } = new();
    }

    /// <summary>
    /// Tóm tắt hàng đợi Offline Order local gửi kèm khi đóng ca ngoại lệ.
    /// </summary>
    public class OfflineQueueSummaryDto
    {
        public int OfflineOrderCount { get; set; }
        public decimal EstimatedTotal { get; set; }
        public decimal LocalCashTotal { get; set; }
    }

    /// <summary>
    /// DTO đăng ký nhanh khách hàng hội viên từ POS
    /// </summary>
    public class QuickCustomerRegisterDto
    {
        public string Phone { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public DateTime? DateOfBirth { get; set; }
    }

    /// <summary>
    /// DTO đăng ký/cập nhật thiết bị POS Terminal
    /// </summary>
    public class PosTerminalRegisterDto
    {
        public string TerminalId { get; set; } = null!;
        public string Name { get; set; } = null!;
        public int StoreId { get; set; }
    }

    /// <summary>
    /// DTO hủy giao dịch VietQR/PayOS đang chờ thanh toán trên POS.
    /// </summary>
    public class CancelPaymentRequestDto
    {
        public int OrderId { get; set; }
        public string? Reason { get; set; }
        public bool CashReturnedConfirmed { get; set; }
        public bool KeepTemporaryCash { get; set; }
        public decimal ReturnedAmount { get; set; }
        public string? RequestKey { get; set; }
    }

    public class CancelTemporaryCashRequestDto
    {
        public Guid ClientOrderId { get; set; }
        public decimal PendingCashAmount { get; set; }
        public decimal ReturnedAmount { get; set; }
        public bool CashReturnedConfirmed { get; set; }
        public string? Reason { get; set; }
        public string? RequestKey { get; set; }
    }
}

