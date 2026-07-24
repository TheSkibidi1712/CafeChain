using CafeChain.Application.DTOs.POS;

namespace CafeChain.Application.Interfaces.Operations;

public interface IInventoryNotificationPublisher
{
    Task PublishAsync(
        InventoryNotificationChangedDto notification,
        CancellationToken cancellationToken = default);
}
