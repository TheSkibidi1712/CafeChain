namespace CafeChain.Models.Customers
{
    public class CustomerBank
    {
        public int CustomerBankId { get; set; }
        public int CustomerId { get; set; }
        public string BankName { get; set; }
        public string AccountNumber { get; set; }

        public virtual Customer Customer { get; set; }
    }
}
