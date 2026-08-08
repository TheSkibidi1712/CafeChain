using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Results;
using CafeChain.Models.Operations;

namespace CafeChain.Application.Interfaces.POS;

public interface IPosAccessSessionService
{
    Task<PosAccessSession> CreateAsync(int accountId, int staffId, int storeId, string terminalId,
        int exchangeContextId, int? workShiftId, DateTime expiresAtUtc,
        CancellationToken cancellationToken = default,
        bool publishAfterCommit = true);
    Task FlushPendingPublicationsAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<PosAccessSessionDto>> ValidateAsync(Guid publicId, string jwtId,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<PosAccessSessionDto>> GetAsync(Guid publicId, CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyList<PosAccessSessionDto>>> GetActiveAsync(
        IReadOnlyCollection<int> storeIds, CancellationToken cancellationToken = default);
    Task<ServiceResult> BindWorkShiftAsync(Guid publicId, int workShiftId, CancellationToken cancellationToken = default);
    Task<ServiceResult> EndAsync(Guid publicId, string status, int? endedByStaffId, string reason,
        CancellationToken cancellationToken = default);
    Task<int> ExpireDueAsync(CancellationToken cancellationToken = default);
}

public interface IPosAccessSessionPublisher
{
    Task PublishAsync(PosAccessSessionChangedDto notification, CancellationToken cancellationToken = default);
}
