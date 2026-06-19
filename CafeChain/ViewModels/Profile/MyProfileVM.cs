namespace CafeChain.ViewModels.Profile
{
    /// <summary>
    /// ViewModel chỉ đọc — hiển thị thông tin hồ sơ nhân viên.
    /// KHÔNG chứa trường tài chính (BaseSalary, Allowance) để chặn rò rỉ dữ liệu.
    /// </summary>
    public class MyProfileVM
    {
        // === Thông tin cá nhân (Read-only) ===
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string? CCCD { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public int Gender { get; set; } // 0=Nữ, 1=Nam, 2=Khác

        // === Thông tin công việc (Read-only) ===
        public string RoleName { get; set; } = "";
        public string StoreName { get; set; } = "";
        public int EmployeeStatus { get; set; } // 1=Thử việc, 2=Chính thức, 3=Nghỉ việc
        public bool Active { get; set; }
        public DateTime? StartDate { get; set; }

        // === Ảnh đại diện (Editable) ===
        public string? AvatarUrl { get; set; }

        // === Liên hệ (Editable) ===
        public string? PhoneNumber { get; set; }
    }
}
