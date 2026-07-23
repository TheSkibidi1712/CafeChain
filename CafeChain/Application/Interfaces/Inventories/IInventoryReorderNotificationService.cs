namespace CafeChain.Application.Interfaces.Inventories;

public sealed record ReorderNotificationRefreshResult(int Created, int Updated, int Resolved);

public interface IInventoryReorderNotificationService
{
    Task<ReorderNotificationRefreshResult> RefreshStoreAsync(
        int storeId,
        int analysisWindowDays,
        CancellationToken cancellationToken = default);
}
