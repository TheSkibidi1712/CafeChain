using CafeChain.Models.Inventories;
using CafeChain.Models.Payments;
using CafeChain.Models.Stores;
using CafeChain.Models.Customers;

namespace CafeChain.Models.Staffs
{
    public class Staff
    {
        public int StaffId { get; set; }
        public int AccountId { get; set; }
        public string FullName { get; set; }
        public string? TaxCode { get; set; }
        public string? CCCD { get; set; }
        public decimal? Salary { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public int StoreId { get; set; }

        public bool Active { get; set; }
        public string AvatarUrl { get; set; }
        public DateTime CreatedAt { get; set; }

        public virtual Store Store { get; set; }
        public  virtual Account Account { get; set; }
        public virtual ICollection<StaffBank> StaffBanks { get; set; }
        public virtual ICollection<StaffScope> StaffScopes { get; set; }
        public virtual ICollection<StaffShift> StaffShifts { get; set; }
        public virtual ICollection<CashSession> CashSessions { get; set; }
        public virtual ICollection<InventoryDocument> InventoryDocuments { get; set; }
        public virtual ICollection<StaffPhone> StaffPhones { get; set; }
        public virtual ICollection<StaffAddress> StaffAddresses { get; set; }
    }
}
