using System.Collections.Generic;

namespace CafeChain.Application.DTOs.POS
{
    /// <summary>
    /// Response DTO cho GET /api/v1/pos/orders (lịch sử đơn hàng)
    /// </summary>
    public class POSOrderHistoryDto
    {
        public int OrderId { get; set; }
        public string? ClientOrderId { get; set; }
        public int StoreId { get; set; }
        public string StoreName { get; set; } = null!;
        public int? WorkShiftId { get; set; }
        public string? Source { get; set; }
        public string OrderType { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public decimal Total { get; set; }
        public string PaymentMethod { get; set; } = null!;
        public int OrderStatusId { get; set; }
        public string OrderStatusName { get; set; } = null!;
        public int PaymentStatusId { get; set; }
        public string PaymentStatusName { get; set; } = null!;
        public string StaffName { get; set; } = null!;
        public string? Note { get; set; }
        public List<POSPaymentHistoryDto> Payments { get; set; } = new();
        public List<POSOrderDetailHistoryDto> OrderDetails { get; set; } = new();
    }

    /// <summary>
    /// Một dòng thanh toán trong lịch sử POS.
    /// </summary>
    public class POSPaymentHistoryDto
    {
        public int PaymentMethodId { get; set; }
        public string PaymentMethod { get; set; } = null!;
        public int PaymentStatusId { get; set; }
        public string PaymentStatus { get; set; } = null!;
        public decimal Amount { get; set; }
        public decimal? ReceivedAmount { get; set; }
        public decimal? ChangeAmount { get; set; }
        public DateTime? PaidAt { get; set; }
        public string? TransactionCode { get; set; }
    }

    /// <summary>
    /// Chi tiết 1 dòng sản phẩm trong đơn hàng lịch sử
    /// </summary>
    public class POSOrderDetailHistoryDto
    {
        public string DrinkName { get; set; } = null!;
        public string? SizeName { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal LineTotal { get; set; }
        public int? IceLevelPercent { get; set; }
        public decimal? BaseIceQuantityBaseUnit { get; set; }
        public decimal? AppliedIceQuantityBaseUnit { get; set; }
        public string? Note { get; set; }

        /// <summary>Tên topping dạng string[] (flatten)</summary>
        public List<string> Toppings { get; set; } = new();
    }
}
