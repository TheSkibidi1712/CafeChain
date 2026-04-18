namespace CafeChain.Models.Orders
{
    public class OrderStatus
    {
        public int OrderStatusId { get; set; }
        public string Name { get; set; }
        public string BadgeColor { get; set; }

        public virtual ICollection<Order> Orders { get; set; }
    }
}
