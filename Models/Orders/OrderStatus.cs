namespace CafeChain.Models.Orders
{
    public class OrderStatus
    {
        public int OrSId { get; set; }
        public string Name { get; set; }

        public virtual ICollection<Order> Orders { get; set; }
    }
}
