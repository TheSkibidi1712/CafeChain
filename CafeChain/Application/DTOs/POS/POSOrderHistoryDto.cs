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
        public string OrderType { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public decimal Total { get; set; }
        public string PaymentMethod { get; set; } = null!;
        public string StaffName { get; set; } = null!;
        public List<POSOrderDetailHistoryDto> OrderDetails { get; set; } = new();
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

        /// <summary>Tên topping dạng string[] (flatten)</summary>
        public List<string> Toppings { get; set; } = new();
    }
}
