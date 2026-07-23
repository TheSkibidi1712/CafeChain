using CafeChain.Data;
using CafeChain.Infrastructure.Interfaces.Operations;
using CafeChain.Models.Operations;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Infrastructure.Repositories.Operations;

public sealed class InventoryReorderNotificationRepository : IInventoryReorderNotificationRepository
{
    private readonly AppDbContext _context;

    public InventoryReorderNotificationRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<ReorderNotificationRecipientRow>> GetRecipientCandidatesAsync()
    {
        var rows = await _context.Staffs.AsNoTracking()
            .Where(x => x.Active && x.Account.Active)
            .Select(x => new
            {
                x.StaffId,
                x.AccountId,
                Roles = x.Account.AccountRoles
                    .Where(ar => ar.Role.Active)
                    .Select(ar => ar.Role.Name)
                    .ToList()
            })
            .ToListAsync();
        return rows.Select(x => new ReorderNotificationRecipientRow(x.StaffId, x.AccountId, x.Roles)).ToList();
    }

    public Task<StaffNotification?> GetByDeduplicationKeyAsync(string key) =>
        _context.StaffNotifications.FirstOrDefaultAsync(x => x.DeduplicationKey == key);

    public Task<List<StaffNotification>> GetActiveForStoreAsync(int storeId) =>
        _context.StaffNotifications
            .Where(x => x.StoreId == storeId
                && x.Type == "INVENTORY_REORDER_ALERT"
                && x.ResolvedAt == null)
            .ToListAsync();

    public Task<List<StaffNotification>> GetActiveForStoreAsync(int storeId, string type) =>
        _context.StaffNotifications.Where(x => x.StoreId == storeId && x.Type == type && x.ResolvedAt == null).ToListAsync();

    public void Add(StaffNotification notification) => _context.StaffNotifications.Add(notification);

    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}
