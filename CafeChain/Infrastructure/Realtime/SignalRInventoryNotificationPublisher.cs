using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Operations;
using CafeChain.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CafeChain.Infrastructure.Realtime;

public sealed class SignalRInventoryNotificationPublisher : IInventoryNotificationPublisher
{
    private readonly IHubContext<InventoryNotificationHub> _hub;

    public SignalRInventoryNotificationPublisher(IHubContext<InventoryNotificationHub> hub) =>
        _hub = hub;

    public Task PublishAsync(
        InventoryNotificationChangedDto notification,
        CancellationToken cancellationToken = default) =>
        _hub.Clients
            .Group(InventoryNotificationGroups.ForStore(notification.StoreId))
            .SendAsync("InventoryNotificationChanged", notification, cancellationToken);
}
