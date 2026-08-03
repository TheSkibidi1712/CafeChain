namespace CafeChain.Models.Systems
{
    public class RequestDeduplication
    {
        public int RequestDeduplicationId { get; set; }

        public string RequestKey { get; set; } = null!;

        public string ActionName { get; set; } = null!;

        public int StaffId { get; set; }

        public int? AccountId { get; set; }

        public int StoreId { get; set; }

        public int? ReferenceId { get; set; }

        public string Status { get; set; } = null!;

        public string? RequestBody { get; set; }

        public string PayloadHash { get; set; } = string.Empty;

        public string? ResponseBody { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime ExpiredAt { get; set; }

        public DateTime? ProcessingLeaseUntilUtc { get; set; }

        public byte[]? RowVersion { get; set; }
    }
}
