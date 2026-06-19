namespace CafeChain.Application.DTOs.Admin.Dashboard
{
    public class CashFlowDto
    {
        public int CashSessionId { get; set; }
        public int StaffId { get; set; }

        public DateTime OpenTime { get; set; }
        public DateTime? CloseTime { get; set; }

        public decimal? StartCash { get; set; }

        public decimal CashIn { get; set; }
        public decimal NonCashIn { get; set; }

        public decimal TotalRevenue { get; set; }
    }
}
