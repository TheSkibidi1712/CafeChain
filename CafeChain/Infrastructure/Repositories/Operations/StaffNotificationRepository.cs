using CafeChain.Data;
using CafeChain.Infrastructure.Interfaces.Operations;
using CafeChain.Models.Operations;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Infrastructure.Repositories.Operations;

public sealed class StaffNotificationRepository : IStaffNotificationRepository
{
    private readonly AppDbContext _context;

    public StaffNotificationRepository(AppDbContext context) => _context = context;

    public Task<int> CountAsync(int recipientStaffId, bool unreadOnly, IReadOnlyCollection<int>? allowedStoreIds)
    {
        var query = Scope(_context.StaffNotifications.AsNoTracking(), recipientStaffId, allowedStoreIds);
        if (unreadOnly) query = query.Where(x => !x.IsRead);
        return query.CountAsync();
    }

    public Task<List<StaffNotification>> GetPageAsync(
        int recipientStaffId,
        int skip,
        int take,
        IReadOnlyCollection<int>? allowedStoreIds) =>
        Scope(_context.StaffNotifications.AsNoTracking(), recipientStaffId, allowedStoreIds)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .ThenByDescending(x => x.StaffNotificationId)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

    public Task<StaffNotification?> GetAsync(
        int recipientStaffId,
        int notificationId,
        IReadOnlyCollection<int>? allowedStoreIds,
        bool tracking = true)
    {
        IQueryable<StaffNotification> query = _context.StaffNotifications;
        if (!tracking) query = query.AsNoTracking();
        return Scope(query, recipientStaffId, allowedStoreIds)
            .FirstOrDefaultAsync(x => x.StaffNotificationId == notificationId);
    }

    public Task<List<StaffNotification>> GetUnreadAsync(
        int recipientStaffId,
        IReadOnlyCollection<int>? allowedStoreIds) =>
        Scope(_context.StaffNotifications, recipientStaffId, allowedStoreIds)
            .Where(x => !x.IsRead)
            .ToListAsync();

    public Task SaveChangesAsync() => _context.SaveChangesAsync();

    private static IQueryable<StaffNotification> Scope(
        IQueryable<StaffNotification> query,
        int recipientStaffId,
        IReadOnlyCollection<int>? allowedStoreIds)
    {
        query = query.Where(x => x.RecipientStaffId == recipientStaffId && x.ResolvedAt == null);
        if (allowedStoreIds != null)
            query = query.Where(x => allowedStoreIds.Contains(x.StoreId));
        return query;
    }
}
