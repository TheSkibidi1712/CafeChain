namespace CafeChain.Models.Customers
{
    public class CustomerPhone
    {
        public int CustomerPhoneId { get; set; }
        public int CustomerId { get; set; }
        public string Phone { get; set; }

        public virtual Customer Customer { get; set; }
    }
}
