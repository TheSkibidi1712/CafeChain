using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CafeChain.Infrastructure.Realtime;

public sealed class SignalRWorkShiftOpenApprovalPublisher : IWorkShiftOpenApprovalPublisher
{
    private readonly IHubContext<WorkShiftHub> _hub;
    public SignalRWorkShiftOpenApprovalPublisher(IHubContext<WorkShiftHub> hub) => _hub = hub;

    public Task PublishAsync(WorkShiftOpenApprovalChangedDto notification,
        CancellationToken cancellationToken = default) =>
        _hub.Clients.Groups(
                WorkShiftGroups.ForStore(notification.StoreId),
                WorkShiftGroups.ForStaff(notification.RequestedByStaffId))
            .SendAsync("LateOpenApprovalChanged", notification, cancellationToken);
}
