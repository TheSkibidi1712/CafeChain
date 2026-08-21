using CafeChain.Application.DTOs.StaffHub;

namespace CafeChain.Application.Interfaces.StaffHub;

public interface IStaffScheduleNotificationPublisher
{
    Task PublishAsync(StaffScheduleChangedDto notification, CancellationToken cancellationToken = default);
}
