namespace CafeChain.Application.DTOs.StaffHub;

public sealed class StaffScheduleChangedDto
{
    public int StaffShiftId { get; init; }
    public int StoreId { get; init; }
    public int StaffId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public DateTime WorkDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? RowVersion { get; init; }
    public DateTime ServerNowUtc { get; init; }
}
