namespace CafeChain.ViewModels.Orders
{
    public class OrderDetailViewModel
    {
        // Thông tin chung
        public int OrderId { get; set; }
        public string FormattedOrderId => $"#CC{OrderId:D5}";
        public DateTime CreatedAt { get; set; }
        public string? StoreName { get; set; }
        public string? PaymentMethodName { get; set; }
        public string StatusName { get; set; }
        public string Source { get; set; }
        public string ImageUrl { get; set; } // Thêm thuộc tính này

        // Thông tin giao hàng (Snapshot)
        public string ReceiverName { get; set; }
        public string ReceiverPhone { get; set; }
        public string DeliveryAddress { get; set; }
        public string Note { get; set; }

        // Tài chính (Snapshot)
        public decimal SubTotal { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal VoucherDiscount { get; set; }
        public decimal PointDiscount { get; set; }
        public decimal FinalTotal { get; set; }

        // Danh sách sản phẩm (Snapshot)
        public List<OrderItemViewModel> Items { get; set; } = new List<OrderItemViewModel>();
    }
}
