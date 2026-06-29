using System.Collections.Generic;

namespace CafeChain.Application.DTOs.POS
{
    /// <summary>
    /// Request DTO cho POST /api/v1/pos/orders/sync-offline (batch sync)
    /// </summary>
    public class OfflineBatchSyncRequestDto
    {
        public List<OfflineOrderSyncDTO> Orders { get; set; } = new();
    }

    /// <summary>
    /// Response DTO cho POST /api/v1/pos/orders/sync-offline
    /// </summary>
    public class OfflineBatchSyncResultDto
    {
        public bool Success { get; set; }
        public List<OfflineSyncItemResult> Results { get; set; } = new();
    }

    /// <summary>
    /// Kết quả xử lý 1 đơn trong batch sync
    /// </summary>
    public class OfflineSyncItemResult
    {
        /// <summary>ClientOrderId gốc từ iPad</summary>
        public string ClientOrderId { get; set; } = null!;

        /// <summary>"created" | "duplicate" | "failed"</summary>
        public string Status { get; set; } = null!;

        /// <summary>OrderId nếu tạo thành công hoặc duplicate</summary>
        public int? OrderId { get; set; }

        /// <summary>Thông báo lỗi nếu failed</summary>
        public string? Error { get; set; }
    }
}
