using CafeChain.Models.Loyalties;
using CafeChain.Models.Orders;
using CafeChain.Models.Vouchers;
namespace CafeChain.Models.Customers
{
    public class Customer
    {
        public int CustomerId { get; set; }
        public int AccountId { get; set; }
        public string FullName { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public DateTime CreatedAt { get; set; }
        public string AvatarUrl { get; set; }
        public bool Active { get; set; }

        public virtual ICollection<CustomerPhone> CustomerPhones { get; set; }
        public virtual ICollection<CustomerAddress> CustomerAddresses { get; set; }
        public virtual ICollection<CustomerBank> CustomerBanks { get; set; }
        public virtual Account Account { get; set; }

        public virtual ICollection<Order> Orders { get; set; }
        public virtual ICollection<Rating> Ratings { get; set; }
        public virtual ICollection<CustomerPoint> CustomerPoints { get; set; }
        public virtual ICollection<PointTransaction> PointTransactions { get; set; }
        public virtual ICollection<VoucherUsage> VoucherUsages { get; set; }
        public virtual ICollection<CustomerVoucher> CustomerVouchers { get; set; }
    }
}
