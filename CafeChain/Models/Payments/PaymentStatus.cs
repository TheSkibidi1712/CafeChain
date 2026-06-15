namespace CafeChain.Models.Payments
{
    public class PaymentStatus
    {
        public int PaymentStatusId { get; set; }
        public string Name { get; set; }
        public string Code { get; set; } // 🔥 PENDING, SUCCESS, FAILED
        public string BadgeColor { get; set; }

        public virtual ICollection<Payment> Payments { get; set; }
    }
}
