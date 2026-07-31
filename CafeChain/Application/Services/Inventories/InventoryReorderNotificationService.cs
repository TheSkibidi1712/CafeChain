using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Operations;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Infrastructure.Interfaces.Operations;
using CafeChain.Models.Operations;

namespace CafeChain.Application.Services.Inventories;

public sealed class InventoryReorderNotificationService
    : IInventoryReorderNotificationService
{
    private readonly IReorderSuggestionService _suggestions;
    private readonly IInventoryReorderNotificationRepository _repository;
    private readonly IScopeAuthorizationService _scopeAuthorization;
    private readonly IAdminPermissionService _permissions;
    private readonly IInventoryNotificationDeliveryService? _notificationDelivery;

    public InventoryReorderNotificationService(
        IReorderSuggestionService suggestions,
        IInventoryReorderNotificationRepository repository,
        IScopeAuthorizationService scopeAuthorization,
        IAdminPermissionService permissions,
        IInventoryNotificationDeliveryService? notificationDelivery = null)
    {
        _suggestions = suggestions;
        _repository = repository;
        _scopeAuthorization = scopeAuthorization;
        _permissions = permissions;
        _notificationDelivery = notificationDelivery;
    }

    public async Task<ReorderNotificationRefreshResult> RefreshStoreAsync(
        int storeId,
        int analysisWindowDays,
        CancellationToken cancellationToken = default)
    {
        if (storeId <= 0)
            return new(0, 0, 0);

        var calculated = await _suggestions.CalculateForStoreAsync(
            storeId,
            analysisWindowDays,
            cancellationToken: cancellationToken);
        if (_notificationDelivery != null && calculated.IsSuccess && calculated.Data != null)
            return await RefreshWithDeliveryAsync(
                storeId,
                calculated.Data,
                cancellationToken);

        // Compatibility path for older test seams and deployments where the
        // delivery abstraction has not been registered yet.
        return await RefreshLegacyAsync(
            storeId,
            analysisWindowDays,
            calculated,
            cancellationToken);
    }

    private async Task<ReorderNotificationRefreshResult> RefreshWithDeliveryAsync(
        int storeId,
        ReorderSuggestionListDto list,
        CancellationToken cancellationToken)
    {
        var created = 0;
        var updated = 0;
        var resolved = 0;
        var actionable = list.Items
            .Where(IsActionable)
            .ToDictionary(x => x.IngredientId);

        foreach (var item in list.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (actionable.ContainsKey(item.IngredientId))
            {
                var urgent = item.SuggestionStatus == ReorderRecommendationLevels.Urgent;
                var delivery = await _notificationDelivery!.DeliverAsync(
                    new InventoryNotificationDeliveryRequest(
                        storeId,
                        StaffNotificationTypes.InventoryReorderAlert,
                        urgent
                            ? $"Cần nhập ngay: {item.IngredientName}"
                            : $"Sắp đến điểm nhập: {item.IngredientName}",
                        $"Tồn khả dụng {item.AvailableStock:N3} {item.BaseUnitCode}; "
                        + $"đề xuất cuối {item.FinalSuggestedQuantity:N3} {item.BaseUnitCode}.",
                        urgent ? "CRITICAL" : "WARNING",
                        StaffNotificationEntityTypes.InventoryReorder,
                        item.IngredientId,
                        urgent
                            ? InventoryNotificationChangeKinds.Escalated
                            : InventoryNotificationChangeKinds.Updated,
                        MeaningfulVersion: item.MeaningfulSuggestionVersion,
                        CooldownMinutes: 240,
                        RequiredPermissionCodes:
                        [PermissionConstants.ReorderSuggestionView]),
                    cancellationToken);
                created += delivery.CreatedCount;
                updated += delivery.UpdatedCount;
            }
            else
            {
                var resolution = await _notificationDelivery!.ResolveAsync(
                    storeId,
                    StaffNotificationTypes.InventoryReorderAlert,
                    StaffNotificationEntityTypes.InventoryReorder,
                    item.IngredientId,
                    "WARNING",
                    cancellationToken);
                resolved += resolution.ResolvedCount;
            }
        }

        return new(created, updated, resolved);
    }

    private async Task<ReorderNotificationRefreshResult> RefreshLegacyAsync(
        int storeId,
        int analysisWindowDays,
        ServiceResult<ReorderSuggestionListDto> calculated,
        CancellationToken cancellationToken)
    {
        var candidates = await _repository.GetRecipientCandidatesAsync();
        var recipients = new List<ReorderNotificationRecipientRow>();
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await _scopeAuthorization.CanAccessStoreAsync(candidate.StaffId, storeId))
                continue;
            var permission = await _permissions.HasPermissionAsync(
                candidate.AccountId,
                PermissionConstants.NotificationView,
                storeId);
            // Kept only for legacy seams with no Notification.View decision.
            if (permission?.Data == null)
            {
                permission = await _permissions.HasPermissionAsync(
                    candidate.AccountId,
                    PermissionConstants.AppAdminDashboard,
                    storeId);
            }
            if (permission?.IsSuccess == true && permission.Data?.Allowed == true)
                recipients.Add(candidate);
        }

        if (calculated == null || !calculated.IsSuccess || calculated.Data == null)
        {
            var actor = recipients.FirstOrDefault();
            if (actor == null)
                return new(0, 0, 0);
            calculated = await _suggestions.CalculateForStoreAsync(
                storeId,
                analysisWindowDays,
                cancellationToken: cancellationToken);
        }
        if (calculated == null || !calculated.IsSuccess || calculated.Data == null)
            return new(0, 0, 0);

        var actionable = calculated.Data.Items.Where(IsActionable).ToArray();
        var activeKeys = new HashSet<string>(StringComparer.Ordinal);
        var created = 0;
        var updated = 0;
        foreach (var recipient in recipients)
        foreach (var item in actionable)
        {
            var key =
                $"{recipient.StaffId}:{storeId}:{item.IngredientId}:{StaffNotificationTypes.InventoryReorderAlert}";
            activeKeys.Add(key);
            var existing = await _repository.GetByDeduplicationKeyAsync(key);
            var urgent = item.SuggestionStatus == ReorderRecommendationLevels.Urgent;
            if (existing == null)
            {
                _repository.Add(new StaffNotification
                {
                    StoreId = storeId,
                    RecipientStaffId = recipient.StaffId,
                    Type = StaffNotificationTypes.InventoryReorderAlert,
                    Title = urgent
                        ? $"Cần nhập ngay: {item.IngredientName}"
                        : $"Sắp đến điểm nhập: {item.IngredientName}",
                    Body = $"Đề xuất cuối {item.FinalSuggestedQuantity:N3} {item.BaseUnitCode}.",
                    Severity = urgent ? "CRITICAL" : "WARNING",
                    DeduplicationKey = key,
                    EntityType = StaffNotificationEntityTypes.InventoryReorder,
                    EntityId = item.IngredientId,
                    CreatedAt = DateTime.UtcNow
                });
                created++;
            }
            else if (existing.ResolvedAt.HasValue)
            {
                existing.ResolvedAt = null;
                existing.UpdatedAt = DateTime.UtcNow;
                updated++;
            }
        }

        foreach (var notification in await _repository.GetActiveForStoreAsync(storeId))
        {
            if (notification.DeduplicationKey == null
                || activeKeys.Contains(notification.DeduplicationKey))
                continue;
            notification.ResolvedAt = DateTime.UtcNow;
            notification.UpdatedAt = DateTime.UtcNow;
        }

        if (created + updated > 0)
            await _repository.SaveChangesAsync();
        return new(created, updated, 0);
    }

    private static bool IsActionable(ReorderSuggestionItemDto item) =>
        (item.FinalSuggestedQuantity ?? item.SuggestedBaseQuantity) > 0m
        && item.SuggestionStatus is
            ReorderRecommendationLevels.Urgent
            or ReorderRecommendationLevels.NearReorder
            or ReorderRecommendationLevels.ProcurementInProgress;
}
