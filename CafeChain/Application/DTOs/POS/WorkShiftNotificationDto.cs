namespace CafeChain.Application.DTOs.POS;

public sealed class WorkShiftNotificationDto
{
    public int WorkShiftId { get; init; }
    public int StoreId { get; init; }
    public int StaffId { get; init; }
    public string? TerminalId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime ServerNowUtc { get; init; }
    public DateTime? AutoCloseAtUtc { get; init; }
    public int? RemainingMinutes { get; init; }
}
