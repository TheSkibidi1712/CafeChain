namespace CafeChain.Models.Payments
{
    public class PaymentMethod
    {
        public int PaymentMethodId { get; set; }
        public string Name { get; set; }
        public string Code { get; set; } // 🔥 CASH, MOMO, BANK

        public virtual ICollection<Payment> Payments { get; set; }
    }
}
