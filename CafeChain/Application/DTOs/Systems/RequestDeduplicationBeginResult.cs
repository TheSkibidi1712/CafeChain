using CafeChain.Models.Systems;

namespace CafeChain.Application.DTOs.Systems
{
    public class RequestDeduplicationBeginResult
    {
        public bool CanProcess { get; set; }
        public bool IsDuplicate { get; set; }
        public string? Status { get; set; }
        public int? ReferenceId { get; set; }
        public string? ResponseBody { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ErrorCode { get; set; }
        public RequestDeduplication? Entry { get; set; }
    }
}
