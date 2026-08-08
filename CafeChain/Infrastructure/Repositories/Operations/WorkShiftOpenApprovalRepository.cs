using CafeChain.Data;
using CafeChain.Infrastructure.Interfaces.Operations;
using CafeChain.Models.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CafeChain.Infrastructure.Repositories.Operations;

public sealed class WorkShiftOpenApprovalRepository : IWorkShiftOpenApprovalRepository
{
    private readonly AppDbContext _db;
    private IDbContextTransaction? _transaction;
    public WorkShiftOpenApprovalRepository(AppDbContext db) => _db = db;

    public async Task<WorkShiftOpenApprovalRequest?> GetByPublicIdAsync(
        Guid publicId, bool tracking, CancellationToken cancellationToken = default)
    {
        if (tracking && _db.Database.IsSqlServer())
        {
            await _db.WorkShiftOpenApprovalRequests
                .FromSqlRaw("SELECT * FROM [WorkShiftOpenApprovalRequests] WITH (UPDLOCK, ROWLOCK) WHERE [PublicId] = {0}", publicId)
                .Select(x => x.WorkShiftOpenApprovalRequestId)
                .FirstOrDefaultAsync(cancellationToken);
        }
        IQueryable<WorkShiftOpenApprovalRequest> query = _db.WorkShiftOpenApprovalRequests
            .Include(x => x.RequestedByStaff)
            .Include(x => x.DecidedByStaff)
            .Include(x => x.SourceStaffShift);
        if (!tracking) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(x => x.PublicId == publicId, cancellationToken);
    }

    public Task<WorkShiftOpenApprovalRequest?> GetPendingAsync(
        int storeId, int requesterStaffId, int sourceStaffShiftId, string terminalId,
        CancellationToken cancellationToken = default) =>
        _db.WorkShiftOpenApprovalRequests
            .Include(x => x.RequestedByStaff)
            .OrderByDescending(x => x.RequestedAtUtc)
            .FirstOrDefaultAsync(x => x.StoreId == storeId
                && x.RequestedByStaffId == requesterStaffId
                && x.SourceStaffShiftId == sourceStaffShiftId
                && x.TerminalId == terminalId
                && x.Status == WorkShiftOpenApprovalStatuses.Pending,
                cancellationToken);

    public async Task<IReadOnlyList<WorkShiftOpenApprovalRequest>> GetPendingForStoresAsync(
        IReadOnlyCollection<int> storeIds, CancellationToken cancellationToken = default)
    {
        if (storeIds.Count == 0) return Array.Empty<WorkShiftOpenApprovalRequest>();
        return await _db.WorkShiftOpenApprovalRequests
            .AsNoTracking()
            .Include(x => x.RequestedByStaff)
            .Include(x => x.DecidedByStaff)
            .Where(x => storeIds.Contains(x.StoreId)
                && x.Status == WorkShiftOpenApprovalStatuses.Pending)
            .OrderBy(x => x.ExpiresAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkShiftOpenApprovalRequest>> GetDueForExpiryAsync(
        DateTime nowUtc, int take, CancellationToken cancellationToken = default)
    {
        var pageSize = Math.Clamp(take, 1, 500);
        return await _db.WorkShiftOpenApprovalRequests
            .Where(x => x.Status == WorkShiftOpenApprovalStatuses.Pending
                && x.ExpiresAtUtc <= nowUtc)
            .OrderBy(x => x.ExpiresAtUtc)
            .ThenBy(x => x.WorkShiftOpenApprovalRequestId)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(WorkShiftOpenApprovalRequest request, CancellationToken cancellationToken = default)
    {
        _db.WorkShiftOpenApprovalRequests.Add(request);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _db.SaveChangesAsync(cancellationToken);

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_db.Database.CurrentTransaction != null) return;
        _transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null) return;
        await _transaction.CommitAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null) return;
        await _transaction.RollbackAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }
}
