using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CafeChain.Infrastructure.Realtime;

public sealed class SignalRPosAccessSessionPublisher : IPosAccessSessionPublisher
{
    private readonly IHubContext<WorkShiftHub> _hub;
    public SignalRPosAccessSessionPublisher(IHubContext<WorkShiftHub> hub) => _hub = hub;

    public Task PublishAsync(PosAccessSessionChangedDto notification, CancellationToken cancellationToken = default) =>
        _hub.Clients.Groups(
                WorkShiftGroups.ForTerminal(notification.TerminalId),
                WorkShiftGroups.ForSession(notification.SessionId))
            .SendAsync("PosAccessSessionChanged", notification, cancellationToken);
}
