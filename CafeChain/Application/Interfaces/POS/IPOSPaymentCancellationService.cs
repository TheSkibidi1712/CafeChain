using CafeChain.Application.DTOs.POS;

namespace CafeChain.Application.Interfaces.POS;

public interface IPOSPaymentCancellationService
{
    Task<POSPaymentOperationResultDto> CancelPaymentAsync(
        CancelPaymentRequestDto request,
        int actorStaffId,
        int storeId,
        CancellationToken cancellationToken = default);

    Task<POSPaymentOperationResultDto> CancelTemporaryCashAsync(
        CancelTemporaryCashRequestDto request,
        int actorStaffId,
        int storeId,
        CancellationToken cancellationToken = default);
}
