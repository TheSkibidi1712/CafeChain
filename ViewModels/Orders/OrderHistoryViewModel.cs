namespace CafeChain.ViewModels.Orders
{
    public class OrderHistoryViewModel
    {
        public int OrderId { get; set; }
        public string FormattedOrderId => $"#CC{OrderId:D5}";
        public DateTime CreatedAt { get; set; }
        public string? StoreName { get; set; }
        public int OrderStatusId { get; set; }
        public string StatusName { get; set; }
        public decimal TotalAmount { get; set; }
        
        // Dùng để render Ảnh sản phẩm list
        public string FirstItemName { get; set; }
        public string FirstItemImageUrl { get; set; }
        public int AdditionalItemsCount { get; set; }
    }
}
