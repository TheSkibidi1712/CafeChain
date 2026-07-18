using System.ComponentModel.DataAnnotations;
using CafeChain.Models.Staffs;

namespace CafeChain.Models.Inventories.Procurement;

public sealed class PurchaseOrderBatchDocumentRevision
{
    public int PurchaseOrderBatchDocumentRevisionId { get; set; }
    public int PurchaseOrderBatchId { get; set; }
    public int RevisionNumber { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public int GeneratedByStaffId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StorageReference { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public string SnapshotJson { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? SentChannel { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public int? SentByStaffId { get; set; }
    public string? SentNote { get; set; }
    public string? SentIdempotencyKey { get; set; }
    public DateTime? SupersededAtUtc { get; set; }
    public int? SupersededByRevisionId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public PurchaseOrderBatch PurchaseOrderBatch { get; set; } = null!;
    public Staff GeneratedByStaff { get; set; } = null!;
    public Staff? SentByStaff { get; set; }
    public PurchaseOrderBatchDocumentRevision? SupersededByRevision { get; set; }
}
