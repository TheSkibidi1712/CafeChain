namespace CafeChain.Application.DTOs.POS
{
    /// <summary>
    /// DTO cho request xác thực PIN Trưởng ca — tách ra khỏi Controller
    /// </summary>
    public class SupervisorAuthRequestDto
    {
        public string Pin { get; set; } = string.Empty;
        public string ActionName { get; set; } = string.Empty;
        public int TargetId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
