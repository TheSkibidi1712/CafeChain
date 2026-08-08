using CafeChain.Application.DTOs.POS;

namespace CafeChain.Application.Interfaces.POS;

public interface IOperationalOtpNotificationPublisher
{
    Task PublishChangedAsync(
        int approverStaffId,
        OperationalOtpNotificationChangedDto notification,
        CancellationToken cancellationToken = default);

    Task PublishTerminalRegistrationChangedAsync(
        int requesterStaffId,
        TerminalRegistrationChangedDto notification,
        CancellationToken cancellationToken = default);
}
