namespace CafeChain.Application.DTOs.Admin.Dashboard
{
    public class StaffPerformanceDto
    {
        public int StaffId { get; set; }
        public string FullName { get; set; }

        public int TotalOrders { get; set; }
        public decimal Revenue { get; set; }
    }
}
