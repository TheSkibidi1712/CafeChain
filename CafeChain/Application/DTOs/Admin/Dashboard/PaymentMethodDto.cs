namespace CafeChain.Application.DTOs.Admin.Dashboard
{
    public class PaymentMethodDto
    {
        public string Name { get; set; }
        public int TotalTransactions { get; set; }
        public decimal Revenue { get; set; }
    }
}
