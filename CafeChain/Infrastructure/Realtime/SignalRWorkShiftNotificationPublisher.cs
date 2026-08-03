using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CafeChain.Infrastructure.Realtime;

public sealed class SignalRWorkShiftNotificationPublisher : IWorkShiftNotificationPublisher
{
    private readonly IHubContext<WorkShiftHub> _hub;

    public SignalRWorkShiftNotificationPublisher(IHubContext<WorkShiftHub> hub) => _hub = hub;

    public Task PublishAsync(WorkShiftNotificationDto notification, CancellationToken cancellationToken = default)
    {
        var groups = new List<string>
        {
            WorkShiftGroups.ForStore(notification.StoreId),
            WorkShiftGroups.ForStaff(notification.StaffId)
        };
        if (!string.IsNullOrWhiteSpace(notification.TerminalId))
            groups.Add(WorkShiftGroups.ForTerminal(notification.TerminalId));

        return _hub.Clients.Groups(groups.Distinct(StringComparer.Ordinal).ToList())
            .SendAsync("WorkShiftChanged", notification, cancellationToken);
    }
}
