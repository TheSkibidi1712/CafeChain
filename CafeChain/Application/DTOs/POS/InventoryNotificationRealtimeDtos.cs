namespace CafeChain.Application.DTOs.POS;

public static class InventoryNotificationChangeKinds
{
    public const string Created = "Created";
    public const string Updated = "Updated";
    public const string Escalated = "Escalated";
    public const string Resolved = "Resolved";
}

public sealed record InventoryNotificationChangedDto(
    string EventId,
    int StoreId,
    string Type,
    string Severity,
    string ChangeKind,
    string EntityType,
    int EntityId,
    bool ShouldToast,
    DateTime OccurredAt);
