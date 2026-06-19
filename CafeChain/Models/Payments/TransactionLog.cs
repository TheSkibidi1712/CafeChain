namespace CafeChain.Models.Payments
{
    /// <summary>
    /// Bảng ghi log giao dịch thanh toán từ cổng PayOS (IPN Webhook).
    /// Mục đích: Audit Trail — không bao giờ xóa, chỉ INSERT.
    /// </summary>
    public class TransactionLog
    {
        public int TransactionLogId { get; set; }

        public int OrderId { get; set; }

        /// <summary>Mã giao dịch từ PayOS (paymentLinkId hoặc reference).</summary>
        public string TransactionId { get; set; }

        /// <summary>Số tiền PayOS xác nhận đã nhận.</summary>
        public decimal Amount { get; set; }

        /// <summary>Nội dung chuyển khoản (description từ PayOS).</summary>
        public string Description { get; set; }

        /// <summary>Trạng thái: PAID, CANCELLED, etc.</summary>
        public string Status { get; set; }

        /// <summary>Payload JSON gốc từ Webhook để debug.</summary>
        public string RawPayload { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
