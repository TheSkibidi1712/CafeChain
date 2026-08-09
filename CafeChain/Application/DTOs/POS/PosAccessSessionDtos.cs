using CafeChain.Application.Constants;

namespace CafeChain.Application.DTOs.POS;

public sealed class PosAccessSessionDto
{
    public Guid SessionId { get; set; }
    public int AccountId { get; set; }
    public int StaffId { get; set; }
    public int StoreId { get; set; }
    public string TerminalId { get; set; } = string.Empty;
    public int? WorkShiftId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string AccessMode { get; set; } = PosAccessModes.OpeningCash;
    public string? WorkShiftStatus { get; set; }
    public string RecommendedAction { get; set; } = WorkShiftRecommendedActions.EnterOpeningCash;
    public DateTime IssuedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime ServerNowUtc { get; set; }
    public string? EndReason { get; set; }
}

public static class PosAccessModes
{
    public const string OpeningCash = "OPENING_CASH";
    public const string Active = "ACTIVE";
    public const string PendingClose = "PENDING_CLOSE";
}

public sealed class PosAccessSessionChangedDto
{
    public string EventId { get; set; } = Guid.NewGuid().ToString("N");
    public Guid SessionId { get; set; }
    public int StoreId { get; set; }
    public string TerminalId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}

public sealed class EndPosAccessSessionRequestDto
{
    public string Reason { get; set; } = string.Empty;
    public string? RowVersion { get; set; }
}
