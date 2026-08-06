using CafeChain.Application.DTOs.POS;

namespace CafeChain.Application.Interfaces.POS;

public interface IPosSessionExchangeService
{
    Task<PosSessionExchangeTicketDto> IssueAsync(PosSessionExchangeContextDto context,
        CancellationToken cancellationToken = default);

    Task<CafeChain.Application.Results.ServiceResult<PosSessionTokenDto>> ExchangeAsync(string exchangeCode,
        CancellationToken cancellationToken = default);

    Task<PosSessionExchangeContextDto?> GetContextAsync(
        int contextId,
        int accountId,
        int staffId,
        int storeId,
        CancellationToken cancellationToken = default);

    Task<bool> CompleteOpeningCashAsync(
        int contextId,
        int accountId,
        int staffId,
        int storeId,
        int workShiftId,
        CancellationToken cancellationToken = default);
}
