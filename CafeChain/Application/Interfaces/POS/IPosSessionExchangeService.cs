using CafeChain.Application.DTOs.POS;

namespace CafeChain.Application.Interfaces.POS;

public interface IPosSessionExchangeService
{
    Task<PosSessionExchangeTicketDto> IssueAsync(int accountId, int staffId, int storeId,
        CancellationToken cancellationToken = default);

    Task<PosSessionTokenDto?> ExchangeAsync(string exchangeCode,
        CancellationToken cancellationToken = default);
}
