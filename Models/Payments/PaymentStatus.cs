namespace CafeChain.Models.Payments
{
    public class PaymentStatus
    {
        public int PaySId { get; set; }
        public string Name { get; set; }
        public string Code { get; set; } // 🔥 PENDING, SUCCESS, FAILED

        public virtual ICollection<Payment> Payments { get; set; }
    }
}
