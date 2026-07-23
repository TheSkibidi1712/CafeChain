using CafeChain.Models.Operations;

namespace CafeChain.Infrastructure.Interfaces.Operations;

public sealed record ReorderNotificationRecipientRow(
    int StaffId,
    int AccountId,
    IReadOnlyCollection<string> RoleNames);

public interface IInventoryReorderNotificationRepository
{
    Task<IReadOnlyList<ReorderNotificationRecipientRow>> GetRecipientCandidatesAsync();
    Task<StaffNotification?> GetByDeduplicationKeyAsync(string key);
    Task<List<StaffNotification>> GetActiveForStoreAsync(int storeId);
    async Task<List<StaffNotification>> GetActiveForStoreAsync(int storeId, string type) =>
        (await GetActiveForStoreAsync(storeId))
            .Where(x => string.Equals(x.Type, type, StringComparison.Ordinal))
            .ToList();
    void Add(StaffNotification notification);
    Task SaveChangesAsync();
}
