using System;

namespace CafeChain.ViewModels.Admin.Staffs
{
    public class StaffDependentVM
    {
        public string FullName { get; set; } = string.Empty;
        public DateTime? DateOfBirth { get; set; }
        public string? TaxCode { get; set; }
        public string? Relationship { get; set; }
    }
}
