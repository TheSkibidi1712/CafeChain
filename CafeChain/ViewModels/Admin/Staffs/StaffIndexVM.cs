namespace CafeChain.ViewModels.Admin.Staffs
{
    public class StaffIndexVM
    {
        public int StaffId { get; set; }
        public int AccountId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string AvatarUrl { get; set; }
        public string StoreName { get; set; }
        public List<string> RoleNames { get; set; } = new();
        public List<int> RoleIds { get; set; } = new(); // 🔥 Để kiểm tra quyền
        public bool Active { get; set; }
        public string DefaultPhone { get; set; }
        public bool CanEdit { get; set; } = true; // 🔥 BUG 2: Frontend Security
    }
}
