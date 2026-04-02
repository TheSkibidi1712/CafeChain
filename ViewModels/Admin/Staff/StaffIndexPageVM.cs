using CafeChain.ViewModels.Shared;

namespace CafeChain.ViewModels.Admin.Staff
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
