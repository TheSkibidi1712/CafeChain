using CafeChain.Models.Operations;

namespace CafeChain.Infrastructure.Interfaces.Operations;

public interface IStaffNotificationRepository
{
    Task<int> CountAsync(int recipientStaffId, bool unreadOnly, IReadOnlyCollection<int>? allowedStoreIds);
    Task<List<StaffNotification>> GetPageAsync(
        int recipientStaffId,
        int skip,
        int take,
        IReadOnlyCollection<int>? allowedStoreIds);
    Task<StaffNotification?> GetAsync(
        int recipientStaffId,
        int notificationId,
        IReadOnlyCollection<int>? allowedStoreIds,
        bool tracking = true);
    Task<List<StaffNotification>> GetUnreadAsync(
        int recipientStaffId,
        IReadOnlyCollection<int>? allowedStoreIds);
    Task SaveChangesAsync();
}
