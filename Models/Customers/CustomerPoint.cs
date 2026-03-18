namespace CafeChain.Models.Customers
{
    public class CustomerPoint
    {
        public int CusPoId { get; set; }
        public int CusId { get; set; }
        public int Points { get; set; }

        public virtual Customer Customer { get; set; }
    }
}
