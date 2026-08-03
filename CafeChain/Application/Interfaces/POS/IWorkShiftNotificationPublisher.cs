using CafeChain.Application.DTOs.POS;

namespace CafeChain.Application.Interfaces.POS;

public interface IWorkShiftNotificationPublisher
{
    Task PublishAsync(WorkShiftNotificationDto notification, CancellationToken cancellationToken = default);
}
