using System.ComponentModel.DataAnnotations;

namespace CafeChain.Models.Inventories.Suppliers
{
    public class SupplierDuplicateWarning
    {
        public long SupplierDuplicateWarningId { get; set; }
        public Guid PublicId { get; set; }
        public int RequestedByStaffId { get; set; }
        public string Status { get; set; } = "Pending";
        public string PayloadHash { get; set; } = "";
        public string WarningFingerprint { get; set; } = "";
        public string MatchedSupplierIdsJson { get; set; } = "[]";
        public string MatchedSignalsJson { get; set; } = "[]";
        public string? OverrideReason { get; set; }
        public int? CreatedSupplierId { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public DateTime? UsedAtUtc { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}
