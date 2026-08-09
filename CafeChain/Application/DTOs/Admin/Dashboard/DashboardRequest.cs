namespace CafeChain.Application.DTOs.Admin.Dashboard
{
    public class DashboardRequest
    {
        public DateTime FromDate { get; set; } = DateTime.Now.AddDays(-7);
        public DateTime ToDate { get; set; } = DateTime.Today;

        public int? StoreId { get; set; }
        public int? ProvinceId { get; set; }
        public int? WardId { get; set; }
        public int? StaffId { get; set; } // 🔥 ADD

    }
}
