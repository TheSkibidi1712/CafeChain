using CafeChain.Models.Operations;

namespace CafeChain.Application.Interfaces.Operations;

public sealed record InventoryNotificationDeliveryRequest(
    int StoreId,
    string Type,
    string Title,
    string Body,
    string Severity,
    string EntityType,
    int EntityId,
    string ChangeKind,
    string? DeduplicationKey = null,
    int? CooldownMinutes = null,
    IReadOnlyCollection<int>? RecipientStaffIds = null);

public sealed record InventoryNotificationDeliveryResult(
    int CreatedCount,
    int UpdatedCount,
    int ResolvedCount,
    bool Published,
    IReadOnlyList<StaffNotification> EmailCandidates);

public interface IInventoryNotificationDeliveryService
{
    Task<InventoryNotificationDeliveryResult> DeliverAsync(
        InventoryNotificationDeliveryRequest request,
        CancellationToken cancellationToken = default);

    Task<InventoryNotificationDeliveryResult> ResolveAsync(
        int storeId,
        string type,
        string entityType,
        int entityId,
        string severity,
        CancellationToken cancellationToken = default);

    Task<InventoryNotificationDeliveryResult> ResolveByDeduplicationKeyAsync(
        string deduplicationKey,
        CancellationToken cancellationToken = default);
}
