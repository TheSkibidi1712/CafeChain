namespace CafeChain.Models.Customers
{
    public class CustomerPoint
    {
        public int CustomerPointId { get; set; }
        public int CustomerId { get; set; }
        public int Points { get; set; }

        public virtual Customer Customer { get; set; }
    }
}
