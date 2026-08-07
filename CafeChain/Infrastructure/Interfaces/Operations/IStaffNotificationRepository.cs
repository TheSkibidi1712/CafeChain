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
    Task<List<OtpChallenge>> GetActiveOtpChallengesAsync(
        int recipientStaffId,
        IReadOnlyCollection<int> challengeIds,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
    Task<List<OtpChallenge>> GetOtpChallengesAsync(
        int recipientStaffId,
        IReadOnlyCollection<int> challengeIds,
        CancellationToken cancellationToken = default);
    Task<StaffNotification?> GetByDeduplicationKeyAsync(
        string key,
        CancellationToken cancellationToken = default);
    Task<StaffNotification?> GetActiveByDeduplicationKeyAsync(
        string key,
        CancellationToken cancellationToken = default);
    Task<List<StaffNotification>> GetActiveByEntityAsync(
        int storeId,
        string type,
        string entityType,
        int entityId,
        CancellationToken cancellationToken = default);
    void Add(StaffNotification notification);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
