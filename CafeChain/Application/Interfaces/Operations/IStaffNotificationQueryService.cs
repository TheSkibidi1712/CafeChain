using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Operations
{
    /// <summary>
    /// Issue #101 — shared read/mark service for StaffNotification (POS + Admin).
    /// Always scoped to a single recipientStaffId.
    /// </summary>
    public interface IStaffNotificationQueryService
    {
        Task<ServiceResult<StaffNotificationUnreadCountDto>> GetUnreadCountAsync(int recipientStaffId);

        Task<ServiceResult<StaffNotificationListDto>> GetListAsync(
            int recipientStaffId,
            int page,
            int pageSize,
            string? targetUrlChannel);

        Task<ServiceResult<StaffNotificationMarkReadResultDto>> MarkReadAsync(
            int recipientStaffId,
            int notificationId);

        Task<ServiceResult<StaffNotificationMarkReadResultDto>> MarkAllReadAsync(int recipientStaffId);
    }
}
