namespace CafeChain.Application.DTOs.POS;

public sealed class OfflineOrderSyncContext
{
    public int ActorStaffId { get; init; }
    public IReadOnlyList<string> ActorRoleNames { get; init; } = Array.Empty<string>();
    public int ClaimedStaffId { get; init; }
    public int ClaimedStoreId { get; init; }
    public int WorkShiftId { get; init; }
    public DateTime SoldAt { get; init; }
}
