using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Operations;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Results;
using CafeChain.Infrastructure.Interfaces.Operations;
using CafeChain.Models.Operations;

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

        private readonly IStaffNotificationRepository _repository;
        private readonly IOtpProtectedPayloadService? _otpProtectedPayload;
        private readonly TimeProvider _timeProvider;

        public StaffNotificationQueryService(
            IStaffNotificationRepository repository,
            IOtpProtectedPayloadService? otpProtectedPayload = null,
            TimeProvider? timeProvider = null)
        {
            _repository = repository;
            _otpProtectedPayload = otpProtectedPayload;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        public async Task<ServiceResult<StaffNotificationUnreadCountDto>> GetUnreadCountAsync(
            int recipientStaffId,
            IReadOnlyCollection<int>? allowedStoreIds = null)
        {
            if (recipientStaffId <= 0)
                return ServiceResult<StaffNotificationUnreadCountDto>.Failure("StaffId không hợp lệ.");

            var count = await _repository.CountAsync(recipientStaffId, true, allowedStoreIds);

            return ServiceResult<StaffNotificationUnreadCountDto>.Success(new StaffNotificationUnreadCountDto
            {
                UnreadCount = count
            });
        }

        public async Task<ServiceResult<StaffNotificationListDto>> GetListAsync(
            int recipientStaffId,
            int page,
            int pageSize,
            string? targetUrlChannel,
            IReadOnlyCollection<int>? allowedStoreIds = null)
        {
            if (recipientStaffId <= 0)
                return ServiceResult<StaffNotificationListDto>.Failure("StaffId không hợp lệ.");

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 50) pageSize = 50;

            var total = await _repository.CountAsync(recipientStaffId, false, allowedStoreIds);
            var unread = await _repository.CountAsync(recipientStaffId, true, allowedStoreIds);
            var rows = await _repository.GetPageAsync(
                recipientStaffId, (page - 1) * pageSize, pageSize, allowedStoreIds);

            var channel = string.IsNullOrWhiteSpace(targetUrlChannel)
                ? ChannelPos
                : targetUrlChannel.Trim().ToLowerInvariant();

            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var otpIds = rows
                .Where(n =>
                    string.Equals(n.Type, StaffNotificationTypes.OperationalOtpRequest, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(n.EntityType, StaffNotificationEntityTypes.OtpChallenge, StringComparison.OrdinalIgnoreCase))
                .Select(n => n.OtpChallengeId ?? n.EntityId)
                .Distinct()
                .ToArray();
            var otpChallenges = otpIds.Length == 0
                ? new List<OtpChallenge>()
                : await _repository.GetOtpChallengesAsync(recipientStaffId, otpIds)
                    ?? new List<OtpChallenge>();
            var otpByChallengeId = otpChallenges.ToDictionary(x => x.OtpChallengeId);

            var items = rows.Select(n =>
            {
                OperationalOtpNotificationDto? operationalOtp = null;
                var challengeId = n.OtpChallengeId ?? n.EntityId;
                if (string.Equals(n.Type, StaffNotificationTypes.OperationalOtpRequest, StringComparison.OrdinalIgnoreCase)
                    && otpByChallengeId.TryGetValue(challengeId, out var challenge)
                    && challenge.StoreId == n.StoreId)
                {
                    var status = MapOperationalOtpStatus(challenge, nowUtc);
                    var isWaiting = status == "Waiting";
                    operationalOtp = new OperationalOtpNotificationDto
                    {
                        ChallengePublicId = challenge.PublicId,
                        ActionType = challenge.ActionType,
                        TerminalId = challenge.TerminalId ?? string.Empty,
                        TerminalName = challenge.TerminalName ?? challenge.TerminalId ?? string.Empty,
                        StoreName = challenge.Store?.Name ?? string.Empty,
                        RequestedByStaffId = challenge.RequestedByStaffId,
                        RequestedByName = challenge.RequestedByStaff?.FullName ?? string.Empty,
                        ApproverStaffId = challenge.ApproverStaffId,
                        ApproverName = challenge.ApproverStaff?.FullName ?? string.Empty,
                        ConfirmedByStaffId = challenge.ConfirmedByStaffId,
                        ConfirmedByName = challenge.ConfirmedByStaff?.FullName,
                        SentAtUtc = challenge.CreatedAt,
                        ExpiresAtUtc = challenge.ExpiresAt,
                        ServerNowUtc = nowUtc,
                        Status = status,
                        RemainingSeconds = isWaiting
                            ? Math.Max(0, (int)Math.Ceiling((challenge.ExpiresAt - nowUtc).TotalSeconds))
                            : 0,
                        CanRevealOtp = isWaiting && challenge.ProtectedOtpPayload != null,
                        CanContinueTerminalConfirmation = isWaiting
                            && challenge.ActionType == OtpConstants.ActionTypes.RegisterTerminal
                    };
                }

                return MapItem(n, channel, operationalOtp);
            }).ToList();

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
            int notificationId,
            IReadOnlyCollection<int>? allowedStoreIds = null)
        {
            if (recipientStaffId <= 0)
                return ServiceResult<StaffNotificationMarkReadResultDto>.Failure("StaffId không hợp lệ.");
            if (notificationId <= 0)
                return ServiceResult<StaffNotificationMarkReadResultDto>.Failure("NotificationId không hợp lệ.");

            var n = await _repository.GetAsync(recipientStaffId, notificationId, allowedStoreIds);

            if (n == null)
            {
                return ServiceResult<StaffNotificationMarkReadResultDto>.Failure(
                    "Không tìm thấy thông báo hoặc bạn không có quyền truy cập.");
            }

            if (!n.IsRead)
            {
                n.IsRead = true;
                n.ReadAt = DateTime.UtcNow;
                await _repository.SaveChangesAsync();
                return ServiceResult<StaffNotificationMarkReadResultDto>.Success(
                    new StaffNotificationMarkReadResultDto { MarkedCount = 1 });
            }

            return ServiceResult<StaffNotificationMarkReadResultDto>.Success(
                new StaffNotificationMarkReadResultDto { MarkedCount = 0 });
        }

        public async Task<ServiceResult<StaffNotificationMarkReadResultDto>> MarkAllReadAsync(
            int recipientStaffId,
            IReadOnlyCollection<int>? allowedStoreIds = null)
        {
            if (recipientStaffId <= 0)
                return ServiceResult<StaffNotificationMarkReadResultDto>.Failure("StaffId không hợp lệ.");

            var unread = await _repository.GetUnreadAsync(recipientStaffId, allowedStoreIds);

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

            await _repository.SaveChangesAsync();
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

            // POS SPA: branch inventory. Admin: stock alert details (#99).
            if (string.Equals(channel, ChannelAdmin, StringComparison.OrdinalIgnoreCase))
                return null; // set with entityId in MapItem for Admin

            return "/inventory";
        }

        /// <summary>Admin deep-link with entity id (Issue #99 / #100).</summary>
        public static string? MapAdminTargetUrl(string entityType, string type, int entityId, int storeId = 0)
        {
            if (entityId <= 0)
                return null;

            var isRestock =
                string.Equals(entityType, StaffNotificationEntityTypes.RestockRequest, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, StaffNotificationTypes.RestockRequestSubmitted, StringComparison.OrdinalIgnoreCase);

            if (isRestock)
                return $"/Admin/AdminRestockRequests/Details/{entityId}";

            var isReorder =
                string.Equals(entityType, StaffNotificationEntityTypes.InventoryReorder, StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, StaffNotificationTypes.InventoryReorderAlert, StringComparison.OrdinalIgnoreCase);
            if (isReorder && storeId > 0)
                return $"/Admin/AdminReorderSuggestions?storeId={storeId}#ingredient-{entityId}";

            if (string.Equals(type, StaffNotificationTypes.OperationalAnomaly, StringComparison.OrdinalIgnoreCase) && storeId > 0)
                return $"/Admin/AdminOperationalAnomalies?targetStoreId={storeId}#anomaly-{entityId}";

            var isStockAlert =
                string.Equals(entityType, StaffNotificationEntityTypes.StockAlert, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, StaffNotificationTypes.StockShortageReport, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, StaffNotificationTypes.StockAlertConfirmed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, StaffNotificationTypes.StockAlertRejected, StringComparison.OrdinalIgnoreCase);

            if (!isStockAlert)
                return null;

            return $"/Admin/AdminStockAlerts/Details/{entityId}";
        }

        private static StaffNotificationItemDto MapItem(
            StaffNotification n,
            string channel,
            OperationalOtpNotificationDto? operationalOtp)
        {
            string? targetUrl;
            if (string.Equals(channel, ChannelAdmin, StringComparison.OrdinalIgnoreCase))
                targetUrl = MapAdminTargetUrl(n.EntityType, n.Type, n.EntityId, n.StoreId);
            else
                targetUrl = MapTargetUrl(n.EntityType, n.Type, channel);

            return new StaffNotificationItemDto
            {
                NotificationId = n.StaffNotificationId,
                StoreId = n.StoreId,
                Type = n.Type,
                Title = n.Title,
                Body = n.Body,
                Severity = n.Severity,
                IsResolved = n.ResolvedAt.HasValue,
                EntityType = n.EntityType,
                EntityId = n.EntityId,
                IsRead = n.IsRead,
                ReadAt = n.ReadAt,
                CreatedAt = n.CreatedAt,
                EmailAttempted = n.EmailAttempted,
                EmailSent = n.EmailSent,
                EmailDeliveryHint = MapEmailDeliveryHint(n.EmailAttempted, n.EmailSent),
                TargetUrl = targetUrl,
                ActiveOtp = null,
                OperationalOtp = operationalOtp
            };
        }

        private static string MapOperationalOtpStatus(OtpChallenge challenge, DateTime nowUtc)
        {
            if (challenge.Status == OtpConstants.Statuses.Used) return "Used";
            if (challenge.Status == OtpConstants.Statuses.Expired || challenge.ExpiresAt <= nowUtc) return "Expired";
            if (challenge.Status is OtpConstants.Statuses.Cancelled or OtpConstants.Statuses.Locked) return "Cancelled";
            return "Waiting";
        }
    }
}
