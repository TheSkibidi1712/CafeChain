using CafeChain.Models.Operations;

namespace CafeChain.Infrastructure.Interfaces.Operations;

public interface IWorkShiftOpenApprovalRepository
{
    Task<WorkShiftOpenApprovalRequest?> GetByPublicIdAsync(Guid publicId, bool tracking,
        CancellationToken cancellationToken = default);
    Task<WorkShiftOpenApprovalRequest?> GetPendingAsync(int storeId, int requesterStaffId,
        int sourceStaffShiftId, string terminalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkShiftOpenApprovalRequest>> GetPendingForStoresAsync(
        IReadOnlyCollection<int> storeIds, CancellationToken cancellationToken = default);
    Task AddAsync(WorkShiftOpenApprovalRequest request, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
