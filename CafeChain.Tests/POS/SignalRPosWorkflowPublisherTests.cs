using CafeChain.Application.DTOs.POS;
using CafeChain.Hubs;
using CafeChain.Infrastructure.Realtime;
using CafeChain.Models.Operations;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace CafeChain.Tests.POS;

public sealed class SignalRPosWorkflowPublisherTests
{
    [Fact]
    public async Task Pos_session_change_targets_terminal_session_and_scoped_managers()
    {
        var (hub, proxy, capturedGroups) = CreateHubContext();
        var sessionId = Guid.NewGuid();
        var notification = new PosAccessSessionChangedDto
        {
            SessionId = sessionId,
            StoreId = 31,
            TerminalId = "POS-31-A",
            Status = PosAccessSessionStatuses.Revoked
        };

        await new SignalRPosAccessSessionPublisher(hub.Object).PublishAsync(notification);

        Assert.Equal(new[]
        {
            WorkShiftGroups.ForTerminal(notification.TerminalId),
            WorkShiftGroups.ForSession(sessionId),
            WorkShiftGroups.ForSessionManagementStore(notification.StoreId)
        }, capturedGroups.Value);
        proxy.Verify(x => x.SendCoreAsync(
            "PosAccessSessionChanged",
            It.Is<object?[]>(args => ReferenceEquals(args[0], notification)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Late_open_change_targets_scoped_managers_and_requester()
    {
        var (hub, proxy, capturedGroups) = CreateHubContext();
        var notification = new WorkShiftOpenApprovalChangedDto(
            Guid.NewGuid(), 41, 501, WorkShiftOpenApprovalStatuses.Approved, DateTime.UtcNow);

        await new SignalRWorkShiftOpenApprovalPublisher(hub.Object).PublishAsync(notification);

        Assert.Equal(new[]
        {
            WorkShiftGroups.ForLateApprovalStore(notification.StoreId),
            WorkShiftGroups.ForStaff(notification.RequestedByStaffId)
        }, capturedGroups.Value);
        proxy.Verify(x => x.SendCoreAsync(
            "LateOpenApprovalChanged",
            It.Is<object?[]>(args => ReferenceEquals(args[0], notification)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static (
        Mock<IHubContext<WorkShiftHub>> Hub,
        Mock<IClientProxy> Proxy,
        GroupCapture CapturedGroups) CreateHubContext()
    {
        var proxy = new Mock<IClientProxy>();
        proxy.Setup(x => x.SendCoreAsync(
                It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var clients = new Mock<IHubClients>();
        var capturedGroups = new GroupCapture();
        clients.Setup(x => x.Groups(It.IsAny<IReadOnlyList<string>>()))
            .Callback<IReadOnlyList<string>>(groups => capturedGroups.Value = groups.ToArray())
            .Returns(proxy.Object);
        var hub = new Mock<IHubContext<WorkShiftHub>>();
        hub.SetupGet(x => x.Clients).Returns(clients.Object);
        return (hub, proxy, capturedGroups);
    }

    private sealed class GroupCapture
    {
        public string[] Value { get; set; } = Array.Empty<string>();
    }
}
