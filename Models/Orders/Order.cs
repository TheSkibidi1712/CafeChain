using CafeChain.Models.Customers;
using CafeChain.Models.Payments;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using CafeChain.Models.Vouchers;
using CafeChain.Models.Loyalties;
namespace CafeChain.Models.Orders
{
    public class Order
    {
        public int OrderId { get; set; }

        public int? CustomerId { get; set; }
        public int StoreId { get; set; }
        public int OrderStatusId { get; set; }
        public int OrderTypeId { get; set; }
        public int? TableId { get; set; }
        public int? StaffId { get; set; }

        public string Source { get; set; }
        public string Note { get; set; }

        public decimal Total { get; set; }
        public DateTime CreatedAt { get; set; }

        public virtual Customer Customer { get; set; }
        public virtual Store Store { get; set; }
        public virtual DiningTable DiningTable { get; set; }
        public virtual Staff Staff { get; set; }
        public virtual OrderStatus OrderStatus { get; set; }
        public virtual OrderType OrderType { get; set; }

        public virtual ICollection<OrderDetail> OrderDetails { get; set; }
        public virtual ICollection<Payment> Payments { get; set; }
        public virtual ICollection<OrderVoucher> OrderVouchers { get; set; }
        public virtual ICollection<KitchenOrder> KitchenOrders { get; set; }
        public virtual ICollection<PointTransaction> PointTransactions { get; set; }
    }
}
