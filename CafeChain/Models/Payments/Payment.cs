using CafeChain.Models.Orders;
namespace CafeChain.Models.Payments
{
    public class Payment
    {
        public int PaymentId { get; set; }

        public int OrderId { get; set; }
        public decimal Amount { get; set; }
        public decimal? ReceivedAmount { get; set; }
        public decimal? ChangeAmount { get; set; }

        public int PaymentMethodId { get; set; }
        public int PaymentStatusId { get; set; }

        public int? CashSessionId { get; set; } // 🔥 thêm

        public string? TransactionCode { get; set; } // 🔥 thêm

        public DateTime? PaidAt { get; set; }

        public virtual Order Order { get; set; }
        public virtual PaymentMethod PaymentMethod { get; set; }
        public virtual PaymentStatus PaymentStatus { get; set; }
        public virtual CashSession CashSession { get; set; }
    }
}
