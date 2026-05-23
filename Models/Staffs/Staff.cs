using CafeChain.Models.Payments;
using CafeChain.Models.Stores;
using CafeChain.Models.Orders;
using CafeChain.Models.Customers;
using System.ComponentModel.DataAnnotations;
using CafeChain.Models.Inventories.Documents;

namespace CafeChain.Models.Staffs
{
    public class Staff
    {
        public int StaffId { get; set; }
        public int AccountId { get; set; }
        public string FullName { get; set; }
        public string? TaxCode { get; set; }
        public string? CCCD { get; set; }
        public int Gender { get; set; } // 0=Nữ, 1=Nam, 2=Khác
        public DateTime? StartDate { get; set; }
        public int EmployeeStatus { get; set; } // 1=Thử việc, 2=Chính thức, 3=Nghỉ việc
        public int SalaryType { get; set; } // 1=Fixed, 2=Hourly
        public decimal BaseSalary { get; set; }
        public decimal Allowance { get; set; }
        public decimal ProbationRate { get; set; }
        public decimal OvertimeRate { get; set; }

        public string? SocialInsuranceNumber { get; set; }
        [StringLength(15, MinimumLength = 10, ErrorMessage = "Mã số BHYT phải từ 10 đến 15 ký tự")]
        public string? HealthInsuranceNumber { get; set; }

        public string? FaceDescriptor { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public int StoreId { get; set; }

        // Cloudinary
        public string? AvatarUrl { get; set; }

        public string? AvatarPublicId { get; set; }
        public bool Active { get; set; }

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
        public virtual ICollection<StaffDependent> StaffDependents { get; set; }
        public virtual ICollection<Order> Orders { get; set; }
    }
}
