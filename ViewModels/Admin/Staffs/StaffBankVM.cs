using System;

namespace CafeChain.ViewModels.Admin.Staffs
{
    public class StaffBankVM
    {
        public string? BankName { get; set; }
        public string? AccountNumber { get; set; }
        public string? AccountHolderName { get; set; }
        public bool IsPrimary { get; set; }
    }
}
