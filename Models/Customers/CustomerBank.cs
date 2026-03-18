namespace CafeChain.Models.Customers
{
    public class CustomerBank
    {
        public int CusBId { get; set; }
        public int CusId { get; set; }
        public string BankName { get; set; }
        public string AccountNumber { get; set; }

        public virtual Customer Customer { get; set; }
    }
}
