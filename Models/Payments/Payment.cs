using CafeChain.Models.Orders;
namespace CafeChain.Models.Payments
{
    public class Payment
    {
        public int PayId { get; set; }

        public int OrdId { get; set; }
        public decimal Amount { get; set; }

        public int PayMId { get; set; }
        public int PaySId { get; set; }

        public int? CashSessionId { get; set; } // 🔥 thêm

        public string? TransactionCode { get; set; } // 🔥 thêm

        public DateTime? PaidAt { get; set; }

        public virtual Order Order { get; set; }
        public virtual PaymentMethod PaymentMethod { get; set; }
        public virtual PaymentStatus PaymentStatus { get; set; }
        public virtual CashSession CashSession { get; set; }
    }
}
