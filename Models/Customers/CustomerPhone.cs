namespace CafeChain.Models.Customers
{
    public class CustomerPhone
    {
        public int CusPId { get; set; }
        public int CusId { get; set; }
        public string Phone { get; set; }

        public virtual Customer Customer { get; set; }
    }
}
