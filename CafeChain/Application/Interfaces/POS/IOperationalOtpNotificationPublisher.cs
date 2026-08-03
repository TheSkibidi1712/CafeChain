using CafeChain.Application.DTOs.POS;

namespace CafeChain.Application.Interfaces.POS;

public interface IOperationalOtpNotificationPublisher
{
    Task PublishIssuedAsync(
        int approverStaffId,
        OperationalOtpIssuedDto notification,
        CancellationToken cancellationToken = default);

    Task PublishChangedAsync(
        int approverStaffId,
        OperationalOtpNotificationChangedDto notification,
        CancellationToken cancellationToken = default);
}
