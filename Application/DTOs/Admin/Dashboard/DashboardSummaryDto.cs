namespace CafeChain.Application.DTOs.Admin.Dashboard
{
    public class DashboardSummaryDto
    {
        public int TotalOrders { get; set; }
        public decimal Revenue { get; set; }
        public int TotalCustomers { get; set; }
        public int TodayOrders { get; set; }
    }
}
