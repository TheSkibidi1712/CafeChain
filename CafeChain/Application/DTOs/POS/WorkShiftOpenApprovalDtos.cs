namespace CafeChain.Application.DTOs.POS;

public sealed class WorkShiftOpenApprovalDto
{
    public Guid PublicId { get; set; }
    public string RequestKey { get; set; } = string.Empty;
    public int StoreId { get; set; }
    public int RequestedByStaffId { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public int? DecidedByStaffId { get; set; }
    public string? DecidedByName { get; set; }
    public int? SourceStaffShiftId { get; set; }
    public string TerminalId { get; set; } = string.Empty;
    public int MinutesLate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? DecisionReason { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public DateTime ServerNowUtc { get; set; }
    public string? RowVersion { get; set; }
}

public sealed class CreateWorkShiftOpenApprovalRequestDto
{
    public string TerminalId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string RequestKey { get; set; } = string.Empty;
}

public sealed class DecideWorkShiftOpenApprovalRequestDto
{
    public string Decision { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string RequestKey { get; set; } = string.Empty;
    public string? RowVersion { get; set; }
}

public sealed record WorkShiftOpenApprovalChangedDto(
    Guid PublicId,
    int StoreId,
    int RequestedByStaffId,
    string Status,
    DateTime OccurredAtUtc);
