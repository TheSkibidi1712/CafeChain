namespace CafeChain.Application.DTOs
{
    public class AddToCartRequest
    {
        public int DrinkId { get; set; }
        public int SizeId { get; set; }
        public int Quantity { get; set; }
        public List<int> OptionalToppingIds { get; set; } = new List<int>();
        public List<int> RemovedDefaultToppingIds { get; set; } = new List<int>();
        public string? Note { get; set; } // 🔥 Thêm dòng này để hứng ghi chú từ Frontend gửi lên
    }
}