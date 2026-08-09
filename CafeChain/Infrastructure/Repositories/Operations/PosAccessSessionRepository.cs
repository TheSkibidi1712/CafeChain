using CafeChain.Data;
using CafeChain.Infrastructure.Interfaces.Operations;
using CafeChain.Models.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CafeChain.Infrastructure.Repositories.Operations;

public sealed class PosAccessSessionRepository : IPosAccessSessionRepository
{
    private readonly AppDbContext _db;
    private IDbContextTransaction? _ownedTransaction;
    public PosAccessSessionRepository(AppDbContext db) => _db = db;

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (!_db.Database.IsRelational() || _db.Database.CurrentTransaction != null) return;
        _ownedTransaction = await _db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_ownedTransaction == null) return;
        await _ownedTransaction.CommitAsync(cancellationToken);
        await _ownedTransaction.DisposeAsync();
        _ownedTransaction = null;
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_ownedTransaction == null) return;
        await _ownedTransaction.RollbackAsync(cancellationToken);
        await _ownedTransaction.DisposeAsync();
        _ownedTransaction = null;
    }

    public async Task<IReadOnlyList<PosAccessSession>> CreateReplacingActiveAsync(
        PosAccessSession session,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = _db.Database.IsRelational() && _db.Database.CurrentTransaction == null
            ? await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken)
            : null;

        var active = await _db.PosAccessSessions
            .Where(x => x.TerminalId == session.TerminalId && x.Status == PosAccessSessionStatuses.Active)
            .ToListAsync(cancellationToken);
        foreach (var existing in active)
        {
            existing.Status = PosAccessSessionStatuses.Replaced;
            existing.EndedAtUtc = nowUtc;
            existing.EndReason = "POS access session mới đã thay thế phiên này.";
        }

        _db.PosAccessSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);
        if (transaction != null) await transaction.CommitAsync(cancellationToken);
        return active;
    }

    public Task<PosAccessSession?> GetByPublicIdAsync(
        Guid publicId,
        bool tracking,
        CancellationToken cancellationToken = default)
    {
        IQueryable<PosAccessSession> query = _db.PosAccessSessions
            .Include(x => x.Account)
            .Include(x => x.Staff)
            .Include(x => x.Store)
            .Include(x => x.Terminal)
            .Include(x => x.WorkShift);
        if (!tracking) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(x => x.PublicId == publicId, cancellationToken);
    }

    public Task<PosAccessSession?> GetByJwtIdAsync(
        string jwtId,
        bool tracking,
        CancellationToken cancellationToken = default)
    {
        IQueryable<PosAccessSession> query = _db.PosAccessSessions
            .Include(x => x.Account)
            .Include(x => x.Staff)
            .Include(x => x.Store)
            .Include(x => x.Terminal)
            .Include(x => x.WorkShift);
        if (!tracking) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(x => x.JwtId == jwtId, cancellationToken);
    }

    public async Task<IReadOnlyList<PosAccessSession>> GetActiveForStoresAsync(
        IReadOnlyCollection<int> storeIds, CancellationToken cancellationToken = default)
    {
        if (storeIds.Count == 0) return Array.Empty<PosAccessSession>();
        return await _db.PosAccessSessions
            .AsNoTracking()
            .Include(x => x.Staff)
            .Include(x => x.Terminal)
            .Where(x => storeIds.Contains(x.StoreId) && x.Status == PosAccessSessionStatuses.Active)
            .OrderByDescending(x => x.IssuedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PosAccessSession>> GetDueForExpiryAsync(
        DateTime nowUtc, int take, CancellationToken cancellationToken = default) =>
        await _db.PosAccessSessions
            .Where(x => x.Status == PosAccessSessionStatuses.Active && x.ExpiresAtUtc <= nowUtc)
            .OrderBy(x => x.ExpiresAtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);

    public async Task BindWorkShiftAsync(Guid publicId, int workShiftId, CancellationToken cancellationToken = default)
    {
        var session = await _db.PosAccessSessions.SingleOrDefaultAsync(
            x => x.PublicId == publicId && x.Status == PosAccessSessionStatuses.Active,
            cancellationToken);
        if (session == null) throw new InvalidOperationException("POS access session không còn hoạt động.");
        if (session.WorkShiftId.HasValue && session.WorkShiftId != workShiftId)
            throw new DbUpdateConcurrencyException("POS access session đã bind WorkShift khác.");
        session.WorkShiftId = workShiftId;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
