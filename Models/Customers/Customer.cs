using CafeChain.Models.Loyalties;
using CafeChain.Models.Orders;
using CafeChain.Models.Vouchers;
using System.Reflection;
using CafeChain.Models.Enums.Customer;
namespace CafeChain.Models.Customers
{
    public class Customer
    {
        public int CustomerId { get; set; }
        public int? AccountId { get; set; }
        public string CustomerCode { get; set; }
        public Gender? Gender { get; set; }
        public string FullName { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public CustomerCategory Category { get; set; }
        public int? MemberLevelId { get; set; }
        public decimal TotalSpent { get; set; } = 0;
        public int TotalOrders { get; set; } = 0;
        public int CurrentPoints { get; set; } = 0;
        public DateTime? LastOrderDate { get; set; }
        public string AvatarUrl { get; set; }
        public bool Active { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }

        public DateTime? DeletedAt { get; set; }

        public virtual ICollection<CustomerPhone> CustomerPhones { get; set; } = new List<CustomerPhone>();
        public virtual ICollection<CustomerAddress> CustomerAddresses { get; set; } = new List<CustomerAddress>();
        public virtual ICollection<CustomerBank> CustomerBanks { get; set; } = new List<CustomerBank>();
        public virtual Account? Account { get; set; }
        public virtual MemberLevel? MemberLevel { get; set; }

        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
        public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();
        public virtual ICollection<PointTransaction> PointTransactions { get; set; } = new List<PointTransaction>();
        public virtual ICollection<VoucherUsage> VoucherUsages { get; set; } = new List<VoucherUsage>();
        public virtual ICollection<CustomerVoucher> CustomerVouchers { get; set; } = new List<CustomerVoucher>();
    }
}
