namespace CafeChain.ViewModels.Orders
{
    public class OrderItemViewModel
    {
        public string Name { get; set; }
        public string SizeName { get; set; }
        public List<string> ToppingNames { get; set; } = new List<string>();
        public int Quantity { get; set; }
        public decimal Price { get; set; } // Giữ snapshot giá từ OrderDetail
        public string Note { get; set; }
        public string ImageUrl { get; set; }
    }
}
