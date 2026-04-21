using System;

namespace CafeChain.Models.Staffs
{
    public class StaffDependent
    {
        public int StaffDependentId { get; set; }
        public int StaffId { get; set; }
        public string FullName { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? TaxCode { get; set; }
        public string? Relationship { get; set; }
        public DateTime? CreatedAt { get; set; }

        public virtual Staff Staff { get; set; }
    }
}
