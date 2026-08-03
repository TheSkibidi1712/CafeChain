using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CafeChain.Infrastructure.Realtime;

public sealed class SignalROperationalOtpNotificationPublisher : IOperationalOtpNotificationPublisher
{
    private readonly IHubContext<InventoryNotificationHub> _hub;

    public SignalROperationalOtpNotificationPublisher(IHubContext<InventoryNotificationHub> hub) =>
        _hub = hub;

    public Task PublishIssuedAsync(
        int approverStaffId,
        OperationalOtpIssuedDto notification,
        CancellationToken cancellationToken = default) =>
        _hub.Clients
            .Group(InventoryNotificationGroups.ForStaff(approverStaffId))
            .SendAsync("OperationalOtpIssued", notification, cancellationToken);

    public Task PublishChangedAsync(
        int approverStaffId,
        OperationalOtpNotificationChangedDto notification,
        CancellationToken cancellationToken = default) =>
        _hub.Clients
            .Group(InventoryNotificationGroups.ForStaff(approverStaffId))
            .SendAsync("OperationalOtpNotificationChanged", notification, cancellationToken);
}
