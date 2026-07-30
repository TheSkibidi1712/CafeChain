using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Operations;
using CafeChain.Application.Options;
using CafeChain.Infrastructure.Interfaces.Operations;
using CafeChain.Models.Operations;
using Microsoft.Extensions.Options;

namespace CafeChain.Application.Services.Operations;

public sealed class InventoryNotificationDeliveryService : IInventoryNotificationDeliveryService
{
    private readonly IInventoryNotificationAudienceResolver _audience;
    private readonly IStaffNotificationRepository _repository;
    private readonly IInventoryNotificationPublisher _publisher;
    private readonly InventoryNotificationOptions _options;
    private readonly TimeProvider _timeProvider;

    public InventoryNotificationDeliveryService(
        IInventoryNotificationAudienceResolver audience,
        IStaffNotificationRepository repository,
        IInventoryNotificationPublisher publisher,
        IOptions<InventoryNotificationOptions> options,
        TimeProvider? timeProvider = null)
    {
        _audience = audience;
        _repository = repository;
        _publisher = publisher;
        _options = options.Value;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<InventoryNotificationDeliveryResult> DeliverAsync(
        InventoryNotificationDeliveryRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var cooldownMinutes = request.CooldownMinutes ?? _options.InventoryCooldownMinutes;
        var cooldown = TimeSpan.FromMinutes(Math.Clamp(cooldownMinutes, 1, 7 * 24 * 60));
        var meaningfulVersion = NormalizeMeaningfulVersion(request.MeaningfulVersion);
        var recipients = request.RequiredPermissionCodes is { Count: > 0 }
            ? await _audience.ResolveForPermissionsAsync(
                request.StoreId,
                request.RequiredPermissionCodes,
                cancellationToken)
            : await _audience.ResolveAsync(request.StoreId, cancellationToken);
        if (request.RecipientStaffIds is { Count: > 0 })
        {
            var allowedRecipientIds = request.RecipientStaffIds.ToHashSet();
            recipients = recipients
                .Where(x => allowedRecipientIds.Contains(x.StaffId))
                .ToList();
        }
        var created = 0;
        var updated = 0;
        var shouldToast = false;
        var emailCandidates = new List<StaffNotification>();

        foreach (var recipient in recipients)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = BuildKey(recipient.StaffId, request);
            var existing = await _repository.GetByDeduplicationKeyAsync(key, cancellationToken);
            if (existing == null)
            {
                var notification = new StaffNotification
                {
                    StoreId = request.StoreId,
                    RecipientStaffId = recipient.StaffId,
                    Type = request.Type,
                    Title = Truncate(request.Title, 200),
                    Body = Truncate(request.Body, 2000),
                    Severity = request.Severity,
                    DeduplicationKey = key,
                    MeaningfulVersion = meaningfulVersion,
                    EntityType = request.EntityType,
                    EntityId = request.EntityId,
                    IsRead = false,
                    CreatedAt = now
                };
                _repository.Add(notification);
                emailCandidates.Add(notification);
                created++;
                shouldToast = true;
                continue;
            }

            var wasResolved = existing.ResolvedAt.HasValue;
            var severityEscalated = SeverityRank(request.Severity) > SeverityRank(existing.Severity);
            var lastChangedAt = existing.UpdatedAt ?? existing.CreatedAt;
            var cooldownElapsed = now - lastChangedAt >= cooldown;
            var title = Truncate(request.Title, 200);
            var body = Truncate(request.Body, 2000);
            var contentChanged = !string.Equals(existing.Title, title, StringComparison.Ordinal)
                || !string.Equals(existing.Body, body, StringComparison.Ordinal);
            var severityChanged = !string.Equals(existing.Severity, request.Severity, StringComparison.OrdinalIgnoreCase);
            var versionChanged = meaningfulVersion != null
                && !string.Equals(existing.MeaningfulVersion, meaningfulVersion, StringComparison.Ordinal);

            if (meaningfulVersion != null)
            {
                // A stable version suppresses raw content noise. An elapsed cooldown is
                // the only reason to remind for the same version.
                if (!wasResolved && !versionChanged && !cooldownElapsed)
                    continue;
            }
            else
            {
                // Preserve the existing generic notification behavior for callers that
                // do not opt into version-aware delivery.
                if (!wasResolved && !contentChanged && !severityChanged && !cooldownElapsed)
                    continue;
            }

            existing.Title = title;
            existing.Body = body;
            existing.Severity = request.Severity;
            existing.ResolvedAt = null;
            existing.UpdatedAt = now;

            if (meaningfulVersion != null)
                existing.MeaningfulVersion = meaningfulVersion;

            var shouldNotify = wasResolved
                || severityEscalated
                || cooldownElapsed
                || versionChanged;
            if (shouldNotify)
            {
                existing.IsRead = false;
                existing.ReadAt = null;
                shouldToast = true;
            }

            if (severityEscalated)
                emailCandidates.Add(existing);
            updated++;
        }

        if (created + updated > 0)
            await _repository.SaveChangesAsync(cancellationToken);

        var published = created + updated > 0;
        if (published)
        {
            await _publisher.PublishAsync(new InventoryNotificationChangedDto(
                Guid.NewGuid().ToString("N"),
                request.StoreId,
                request.Type,
                request.Severity,
                created > 0
                    ? InventoryNotificationChangeKinds.Created
                    : request.ChangeKind,
                request.EntityType,
                request.EntityId,
                shouldToast,
                now), cancellationToken);
        }

        return new InventoryNotificationDeliveryResult(
            created,
            updated,
            0,
            published,
            emailCandidates);
    }

    public async Task<InventoryNotificationDeliveryResult> ResolveAsync(
        int storeId,
        string type,
        string entityType,
        int entityId,
        string severity,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var active = await _repository.GetActiveByEntityAsync(
            storeId,
            type,
            entityType,
            entityId,
            cancellationToken);
        foreach (var notification in active)
        {
            notification.ResolvedAt = now;
            notification.UpdatedAt = now;
        }

        if (active.Count > 0)
        {
            await _repository.SaveChangesAsync(cancellationToken);
            await _publisher.PublishAsync(new InventoryNotificationChangedDto(
                Guid.NewGuid().ToString("N"),
                storeId,
                type,
                severity,
                InventoryNotificationChangeKinds.Resolved,
                entityType,
                entityId,
                false,
                now), cancellationToken);
        }

        return new InventoryNotificationDeliveryResult(0, 0, active.Count, active.Count > 0, []);
    }

    public async Task<InventoryNotificationDeliveryResult> ResolveByDeduplicationKeyAsync(
        string deduplicationKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deduplicationKey))
            return new InventoryNotificationDeliveryResult(0, 0, 0, false, []);

        var notification = await _repository.GetActiveByDeduplicationKeyAsync(
            deduplicationKey.Trim(), cancellationToken);
        if (notification == null)
            return new InventoryNotificationDeliveryResult(0, 0, 0, false, []);

        notification.ResolvedAt = _timeProvider.GetUtcNow().UtcDateTime;
        notification.UpdatedAt = notification.ResolvedAt;
        await _repository.SaveChangesAsync(cancellationToken);
        await _publisher.PublishAsync(new InventoryNotificationChangedDto(
            Guid.NewGuid().ToString("N"),
            notification.StoreId,
            notification.Type,
            notification.Severity,
            InventoryNotificationChangeKinds.Resolved,
            notification.EntityType,
            notification.EntityId,
            false,
            notification.ResolvedAt.Value), cancellationToken);

        return new InventoryNotificationDeliveryResult(0, 0, 1, true, []);
    }

    private static string BuildKey(int staffId, InventoryNotificationDeliveryRequest request) =>
        string.IsNullOrWhiteSpace(request.DeduplicationKey)
            ? $"{staffId}:{request.StoreId}:{request.Type}:{request.EntityType}:{request.EntityId}"
            : $"{staffId}:{request.DeduplicationKey.Trim()}";

    private static int SeverityRank(string severity) => severity.ToUpperInvariant() switch
    {
        "URGENT" or "CRITICAL" => 3,
        "WARNING" => 2,
        _ => 1
    };

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private static string? NormalizeMeaningfulVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();
        return Truncate(normalized, 64);
    }
}
