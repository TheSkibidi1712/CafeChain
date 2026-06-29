using System.Collections.Generic;

namespace CafeChain.Application.DTOs.POS
{
    /// <summary>
    /// Response DTO cho POST /api/v1/pos/orders/commit
    /// </summary>
    public class POSOrderCommitResponseDto
    {
        public bool Success { get; set; }

        /// <summary>OrderId vừa tạo (hoặc order cũ nếu idempotent)</summary>
        public int OrderId { get; set; }

        /// <summary>ClientOrderId trả lại cho Frontend match queue</summary>
        public string? ClientOrderId { get; set; }

        /// <summary>Tổng tiền server tính</summary>
        public decimal Total { get; set; }

        /// <summary>Tiền thừa = ReceivedAmount - Total</summary>
        public decimal ChangeAmount { get; set; }

        public string Message { get; set; } = null!;

        /// <summary>
        /// Cảnh báo thiếu hụt nguyên liệu (từ Issue #03B side-effects).
        /// Null nếu kho đủ hoặc chưa chạy inventory deduction.
        /// </summary>
        public List<string>? InventoryWarnings { get; set; }
    }
}
