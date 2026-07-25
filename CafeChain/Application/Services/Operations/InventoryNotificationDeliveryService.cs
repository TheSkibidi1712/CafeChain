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

    public InventoryNotificationDeliveryService(
        IInventoryNotificationAudienceResolver audience,
        IStaffNotificationRepository repository,
        IInventoryNotificationPublisher publisher,
        IOptions<InventoryNotificationOptions> options)
    {
        _audience = audience;
        _repository = repository;
        _publisher = publisher;
        _options = options.Value;
    }

    public async Task<InventoryNotificationDeliveryResult> DeliverAsync(
        InventoryNotificationDeliveryRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var cooldown = TimeSpan.FromMinutes(Math.Clamp(_options.InventoryCooldownMinutes, 1, 24 * 60));
        var recipients = await _audience.ResolveAsync(request.StoreId, cancellationToken);
        var created = 0;
        var updated = 0;
        var shouldToast = false;
        var emailCandidates = new List<StaffNotification>();

        foreach (var recipient in recipients)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = BuildKey(recipient.StaffId, request);
            var existing = await _repository.GetActiveByDeduplicationKeyAsync(key, cancellationToken);
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

            var severityEscalated = SeverityRank(request.Severity) > SeverityRank(existing.Severity);
            var lastChangedAt = existing.UpdatedAt ?? existing.CreatedAt;
            var cooldownElapsed = now - lastChangedAt >= cooldown;
            var title = Truncate(request.Title, 200);
            var body = Truncate(request.Body, 2000);
            var contentChanged = !string.Equals(existing.Title, title, StringComparison.Ordinal)
                || !string.Equals(existing.Body, body, StringComparison.Ordinal);
            var severityChanged = !string.Equals(existing.Severity, request.Severity, StringComparison.OrdinalIgnoreCase);

            // Repeated POS signals during the cooldown are intentionally a no-op.
            // Do not rewrite UpdatedAt, unread state, or publish another toast.
            if (!contentChanged && !severityChanged && !cooldownElapsed)
                continue;

            existing.Title = title;
            existing.Body = body;
            existing.Severity = request.Severity;
            existing.UpdatedAt = now;

            if (severityEscalated || cooldownElapsed)
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
        var now = DateTime.UtcNow;
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

    private static string BuildKey(int staffId, InventoryNotificationDeliveryRequest request) =>
        $"{staffId}:{request.StoreId}:{request.Type}:{request.EntityType}:{request.EntityId}";

    private static int SeverityRank(string severity) => severity.ToUpperInvariant() switch
    {
        "URGENT" or "CRITICAL" => 3,
        "WARNING" => 2,
        _ => 1
    };

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
