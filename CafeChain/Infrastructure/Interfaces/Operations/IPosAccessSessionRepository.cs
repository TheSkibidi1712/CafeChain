using CafeChain.Models.Operations;

namespace CafeChain.Infrastructure.Interfaces.Operations;

public interface IPosAccessSessionRepository
{
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PosAccessSession>> CreateReplacingActiveAsync(PosAccessSession session, DateTime nowUtc, CancellationToken cancellationToken = default);
    Task<PosAccessSession?> GetByPublicIdAsync(Guid publicId, bool tracking, CancellationToken cancellationToken = default);
    Task<PosAccessSession?> GetByJwtIdAsync(string jwtId, bool tracking, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PosAccessSession>> GetActiveForStoresAsync(
        IReadOnlyCollection<int> storeIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PosAccessSession>> GetDueForExpiryAsync(
        DateTime nowUtc, int take, CancellationToken cancellationToken = default);
    Task<bool> TryEndActiveAsync(
        Guid publicId,
        string status,
        DateTime endedAtUtc,
        int? endedByStaffId,
        string reason,
        CancellationToken cancellationToken = default);
    Task BindWorkShiftAsync(Guid publicId, int workShiftId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
