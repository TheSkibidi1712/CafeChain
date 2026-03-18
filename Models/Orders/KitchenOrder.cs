namespace CafeChain.Models.Orders
{
    public class KitchenOrder
    {
        public int KitchenOrderId { get; set; }

        public int OrderId { get; set; }

        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }

        public virtual Order Order { get; set; }
    }
}
