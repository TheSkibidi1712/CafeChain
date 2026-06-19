namespace CafeChain.Models.Orders
{
    public class OrderType
    {
        public int OrderTypeId { get; set; }
        public string Name { get; set; }

        public virtual ICollection<Order> Orders { get; set; }
    }
}
