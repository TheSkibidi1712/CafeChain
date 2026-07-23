using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Infrastructure.Interfaces.Operations;
using CafeChain.Models.Operations;

namespace CafeChain.Application.Services.Inventories;

public sealed class InventoryReorderNotificationService : IInventoryReorderNotificationService
{
    private static readonly HashSet<string> RecipientRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        RoleConstants.BusinessOwner,
        RoleConstants.AreaManager,
        RoleConstants.StoreManager,
        RoleConstants.AccountantWarehouse
    };

    private readonly IReorderSuggestionService _suggestions;
    private readonly IInventoryReorderNotificationRepository _repository;
    private readonly IScopeAuthorizationService _scopeAuthorization;
    private readonly IAdminPermissionService _permissions;

    public InventoryReorderNotificationService(
        IReorderSuggestionService suggestions,
        IInventoryReorderNotificationRepository repository,
        IScopeAuthorizationService scopeAuthorization,
        IAdminPermissionService permissions)
    {
        _suggestions = suggestions;
        _repository = repository;
        _scopeAuthorization = scopeAuthorization;
        _permissions = permissions;
    }

    public async Task<ReorderNotificationRefreshResult> RefreshStoreAsync(
        int storeId,
        int analysisWindowDays,
        CancellationToken cancellationToken = default)
    {
        var candidates = await _repository.GetRecipientCandidatesAsync();
        var recipients = new List<ReorderNotificationRecipientRow>();
        foreach (var candidate in candidates.Where(x => x.RoleNames.Any(RecipientRoles.Contains)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await _scopeAuthorization.CanAccessStoreAsync(candidate.StaffId, storeId)) continue;
            var permission = await _permissions.HasPermissionAsync(
                candidate.AccountId, PermissionConstants.AppAdminDashboard, storeId);
            if (permission.IsSuccess && permission.Data?.Allowed == true)
                recipients.Add(candidate);
        }

        var activeKeys = new HashSet<string>(StringComparer.Ordinal);
        var canResolve = false;
        var created = 0;
        var updated = 0;
        if (recipients.Count > 0)
        {
            var calculationActor = recipients[0];
            var calculated = await _suggestions.GetForStoreAsync(
                storeId,
                calculationActor.StaffId,
                calculationActor.RoleNames,
                analysisWindowDays);
            if (calculated.IsSuccess && calculated.Data != null)
            {
                canResolve = true;
                var actionable = calculated.Data.Items.Where(x =>
                    x.Status == ReorderSuggestionStatuses.Ready
                    && x.SuggestedBaseQuantity > 0
                    && (x.RecommendationLevel == ReorderRecommendationLevels.Urgent
                        || x.RecommendationLevel == ReorderRecommendationLevels.NearReorder));

                foreach (var recipient in recipients)
                foreach (var item in actionable)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var key = $"{recipient.StaffId}:{storeId}:{item.IngredientId}:{StaffNotificationTypes.InventoryReorderAlert}";
                    activeKeys.Add(key);
                    var severity = item.RecommendationLevel == ReorderRecommendationLevels.Urgent ? "CRITICAL" : "WARNING";
                    var title = item.RecommendationLevel == ReorderRecommendationLevels.Urgent
                        ? $"Cần nhập ngay: {item.IngredientName}"
                        : $"Sắp đến điểm nhập: {item.IngredientName}";
                    var body = $"Tồn khả dụng {item.UsableQuantity:N3} {item.BaseUnitCode}; "
                        + $"tồn dự kiến {item.ProjectedQuantity:N3}; đề xuất {item.SuggestedBaseQuantity:N3} {item.BaseUnitCode}.";
                    var existing = await _repository.GetByDeduplicationKeyAsync(key);
                    if (existing == null)
                    {
                        _repository.Add(new StaffNotification
                        {
                            StoreId = storeId,
                            RecipientStaffId = recipient.StaffId,
                            Type = StaffNotificationTypes.InventoryReorderAlert,
                            Title = title,
                            Body = body,
                            Severity = severity,
                            DeduplicationKey = key,
                            EntityType = StaffNotificationEntityTypes.InventoryReorder,
                            EntityId = item.IngredientId,
                            CreatedAt = DateTime.UtcNow
                        });
                        created++;
                        continue;
                    }

                    var severityEscalated = !string.Equals(existing.Severity, severity, StringComparison.Ordinal);
                    var changed = severityEscalated
                        || !string.Equals(existing.Title, title, StringComparison.Ordinal)
                        || !string.Equals(existing.Body, body, StringComparison.Ordinal)
                        || existing.ResolvedAt.HasValue;
                    if (!changed) continue;
                    existing.Title = title;
                    existing.Body = body;
                    existing.Severity = severity;
                    existing.ResolvedAt = null;
                    existing.UpdatedAt = DateTime.UtcNow;
                    if (severityEscalated)
                    {
                        existing.IsRead = false;
                        existing.ReadAt = null;
                    }
                    updated++;
                }
            }
        }

        var resolved = 0;
        if (canResolve)
        {
            foreach (var notification in await _repository.GetActiveForStoreAsync(storeId))
            {
                if (notification.DeduplicationKey == null || activeKeys.Contains(notification.DeduplicationKey)) continue;
                notification.ResolvedAt = DateTime.UtcNow;
                notification.UpdatedAt = DateTime.UtcNow;
                resolved++;
            }
        }
        if (created + updated + resolved > 0)
            await _repository.SaveChangesAsync();
        return new ReorderNotificationRefreshResult(created, updated, resolved);
    }
}
