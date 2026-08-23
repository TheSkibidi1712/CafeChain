using CafeChain.Application.DTOs.StaffHub;
using CafeChain.Application.Interfaces.StaffHub;
using CafeChain.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CafeChain.Infrastructure.Realtime;

public sealed class SignalRStaffScheduleNotificationPublisher : IStaffScheduleNotificationPublisher
{
    private readonly IHubContext<WorkShiftHub> _hub;

    public SignalRStaffScheduleNotificationPublisher(IHubContext<WorkShiftHub> hub) => _hub = hub;

    public Task PublishAsync(StaffScheduleChangedDto notification, CancellationToken cancellationToken = default) =>
        _hub.Clients.Group(WorkShiftGroups.ForStaff(notification.StaffId))
            .SendAsync("StaffScheduleChanged", notification, cancellationToken);
}
