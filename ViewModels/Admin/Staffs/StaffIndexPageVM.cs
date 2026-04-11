using CafeChain.ViewModels.Shared;
using System.ComponentModel.DataAnnotations;

namespace CafeChain.ViewModels.Admin.Staffs
{
    public class StaffIndexPageVM
    {
        public PaginatedListViewModel<StaffIndexVM> StaffList { get; set; }
        public int TotalStaff { get; set; }
        public int ActiveCount { get; set; }
        public int InactiveCount { get; set; }
        public string SearchTerm { get; set; }
        public int? RoleFilter { get; set; }
    }
}
