namespace CafeChain.Application.DTOs.POS
{
    /// <summary>
    /// Kết quả xác thực PIN supervisor/manager cho thao tác POS nhạy cảm.
    /// Không chứa hoặc log lại PIN gốc.
    /// </summary>
    public class SupervisorPinAuthorizationDto
    {
        public int SupervisorStaffId { get; set; }
        public string SupervisorName { get; set; } = string.Empty;
    }
}
