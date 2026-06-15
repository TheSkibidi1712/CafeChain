using System;

namespace CafeChain.Application.DTOs.Admin
{
    public class AdminOrderKanbanDto
    {
        public int OrderId { get; set; }
        public string FormattedOrderId => $"#CC{OrderId:D5}";
        public DateTime CreatedAt { get; set; }
        public int OrderStatusId { get; set; }
        public decimal Total { get; set; }
        public string OrderTypeName { get; set; }
        public int OrderTypeId { get; set; }
        public string Note { get; set; }
        public string CustomerName { get; set; }

        /// <summary>
        /// Danh sách tóm tắt món ăn hiển thị trực tiếp trên card (Phase 1).
        /// Format: "[Qty]x [DrinkName] - [Size] - [Topping1, Topping2]"
        /// </summary>
        public List<string> ItemSummaries { get; set; } = new();

        /// <summary>
        /// Tổng số món (dùng hiển thị "...và X món khác" nếu cắt bớt)
        /// </summary>
        public int TotalItemCount { get; set; }
    }

    public class AdminOrderDetailDto
    {
        public int OrderId { get; set; }
        public string FormattedOrderId => $"#CC{OrderId:D5}";
        public string ReceiverName { get; set; }
        public string ReceiverPhone { get; set; }
        public string DeliveryAddress { get; set; }
        public string Note { get; set; }
        public decimal Total { get; set; }
        public int OrderStatusId { get; set; }
        public int OrderTypeId { get; set; }
        public string OrderTypeName { get; set; }
        public List<AdminOrderItemDto> Items { get; set; } = new();
    }

    public class AdminOrderItemDto
    {
        public string DrinkName { get; set; }
        public string SizeName { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public string Note { get; set; }
        public List<string> Toppings { get; set; } = new();
    }
}
