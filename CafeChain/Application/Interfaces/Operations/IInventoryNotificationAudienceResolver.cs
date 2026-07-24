namespace CafeChain.Application.Interfaces.Operations;

public sealed record InventoryNotificationRecipient(
    int StaffId,
    int AccountId,
    string? Email,
    string FullName);

public interface IInventoryNotificationAudienceResolver
{
    Task<IReadOnlyList<InventoryNotificationRecipient>> ResolveAsync(
        int storeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> ResolveStoreIdsAsync(
        int staffId,
        CancellationToken cancellationToken = default);
}
