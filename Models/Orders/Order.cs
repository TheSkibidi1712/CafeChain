using CafeChain.Models.Customers;
using CafeChain.Models.Payments;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using CafeChain.Models.Vouchers;
namespace CafeChain.Models.Orders
{
    public class Order
    {
        public int OrdId { get; set; }

        public int? CusId { get; set; }
        public int StoId { get; set; }
        public int OrSId { get; set; }
        public int OrTId { get; set; }
        public int? TabId { get; set; }
        public int? StaId { get; set; }

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
    }
}
