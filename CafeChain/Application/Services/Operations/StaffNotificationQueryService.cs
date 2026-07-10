using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Operations;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Operations;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Operations
{
    /// <summary>
    /// Issue #101 — list/unread/mark-read for StaffNotification.
    /// Isolation: RecipientStaffId only (never trust client).
    /// </summary>
    public class StaffNotificationQueryService : IStaffNotificationQueryService
    {
        public const string ChannelPos = "pos";
        public const string ChannelAdmin = "admin";

        private readonly AppDbContext _context;

        public StaffNotificationQueryService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ServiceResult<StaffNotificationUnreadCountDto>> GetUnreadCountAsync(int recipientStaffId)
        {
            if (recipientStaffId <= 0)
                return ServiceResult<StaffNotificationUnreadCountDto>.Failure("StaffId không hợp lệ.");

            var count = await _context.StaffNotifications
                .AsNoTracking()
                .CountAsync(n => n.RecipientStaffId == recipientStaffId && !n.IsRead);

            return ServiceResult<StaffNotificationUnreadCountDto>.Success(new StaffNotificationUnreadCountDto
            {
                UnreadCount = count
            });
        }

        public async Task<ServiceResult<StaffNotificationListDto>> GetListAsync(
            int recipientStaffId,
            int page,
            int pageSize,
            string? targetUrlChannel)
        {
            if (recipientStaffId <= 0)
                return ServiceResult<StaffNotificationListDto>.Failure("StaffId không hợp lệ.");

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 50) pageSize = 50;

            var query = _context.StaffNotifications
                .AsNoTracking()
                .Where(n => n.RecipientStaffId == recipientStaffId);

            var total = await query.CountAsync();
            var unread = await query.CountAsync(n => !n.IsRead);

            var rows = await query
                .OrderByDescending(n => n.CreatedAt)
                .ThenByDescending(n => n.StaffNotificationId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var channel = string.IsNullOrWhiteSpace(targetUrlChannel)
                ? ChannelPos
                : targetUrlChannel.Trim().ToLowerInvariant();

            var items = rows.Select(n => MapItem(n, channel)).ToList();

            return ServiceResult<StaffNotificationListDto>.Success(new StaffNotificationListDto
            {
                Page = page,
                PageSize = pageSize,
                Total = total,
                UnreadCount = unread,
                Items = items
            });
        }

        public async Task<ServiceResult<StaffNotificationMarkReadResultDto>> MarkReadAsync(
            int recipientStaffId,
            int notificationId)
        {
            if (recipientStaffId <= 0)
                return ServiceResult<StaffNotificationMarkReadResultDto>.Failure("StaffId không hợp lệ.");
            if (notificationId <= 0)
                return ServiceResult<StaffNotificationMarkReadResultDto>.Failure("NotificationId không hợp lệ.");

            var n = await _context.StaffNotifications
                .FirstOrDefaultAsync(x =>
                    x.StaffNotificationId == notificationId &&
                    x.RecipientStaffId == recipientStaffId);

            if (n == null)
            {
                return ServiceResult<StaffNotificationMarkReadResultDto>.Failure(
                    "Không tìm thấy thông báo hoặc bạn không có quyền truy cập.");
            }

            if (!n.IsRead)
            {
                n.IsRead = true;
                n.ReadAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return ServiceResult<StaffNotificationMarkReadResultDto>.Success(
                    new StaffNotificationMarkReadResultDto { MarkedCount = 1 });
            }

            return ServiceResult<StaffNotificationMarkReadResultDto>.Success(
                new StaffNotificationMarkReadResultDto { MarkedCount = 0 });
        }

        public async Task<ServiceResult<StaffNotificationMarkReadResultDto>> MarkAllReadAsync(int recipientStaffId)
        {
            if (recipientStaffId <= 0)
                return ServiceResult<StaffNotificationMarkReadResultDto>.Failure("StaffId không hợp lệ.");

            var unread = await _context.StaffNotifications
                .Where(n => n.RecipientStaffId == recipientStaffId && !n.IsRead)
                .ToListAsync();

            if (unread.Count == 0)
            {
                return ServiceResult<StaffNotificationMarkReadResultDto>.Success(
                    new StaffNotificationMarkReadResultDto { MarkedCount = 0 });
            }

            var now = DateTime.UtcNow;
            foreach (var n in unread)
            {
                n.IsRead = true;
                n.ReadAt = now;
            }

            await _context.SaveChangesAsync();
            return ServiceResult<StaffNotificationMarkReadResultDto>.Success(
                new StaffNotificationMarkReadResultDto { MarkedCount = unread.Count });
        }

        public static string MapEmailDeliveryHint(bool attempted, bool sent)
        {
            if (!attempted) return "none";
            if (sent) return "sent";
            return "failed";
        }

        public static string? MapTargetUrl(string entityType, string type, string channel)
        {
            var isStockAlert =
                string.Equals(entityType, StaffNotificationEntityTypes.StockAlert, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, StaffNotificationTypes.StockShortageReport, StringComparison.OrdinalIgnoreCase);

            if (!isStockAlert)
                return null;

            // POS SPA: open branch inventory. Admin: no alert detail screen yet (#99/#100).
            if (string.Equals(channel, ChannelAdmin, StringComparison.OrdinalIgnoreCase))
                return null;

            return "/inventory";
        }

        private static StaffNotificationItemDto MapItem(StaffNotification n, string channel)
        {
            return new StaffNotificationItemDto
            {
                NotificationId = n.StaffNotificationId,
                Type = n.Type,
                Title = n.Title,
                Body = n.Body,
                EntityType = n.EntityType,
                EntityId = n.EntityId,
                IsRead = n.IsRead,
                ReadAt = n.ReadAt,
                CreatedAt = n.CreatedAt,
                EmailAttempted = n.EmailAttempted,
                EmailSent = n.EmailSent,
                EmailDeliveryHint = MapEmailDeliveryHint(n.EmailAttempted, n.EmailSent),
                TargetUrl = MapTargetUrl(n.EntityType, n.Type, channel)
            };
        }
    }
}
