using CafeChain.Models.Customers;
using CafeChain.Models.Orders;
namespace CafeChain.Models.Loyalties
{
    public class PointTransaction
    {
        public int PointTransactionId { get; set; }

        public int CustomerId { get; set; }
        public int? OrderId { get; set; }

        public int Points { get; set; } // luôn dương

        public int PointTransactionTypeId { get; set; }

        public int BalanceAfter { get; set; } // 🔥 snapshot

        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiredAt { get; set; }

        public virtual Customer Customer { get; set; }
        public virtual Order Order { get; set; }
        public virtual PointTransactionType Type { get; set; }
    }
}
