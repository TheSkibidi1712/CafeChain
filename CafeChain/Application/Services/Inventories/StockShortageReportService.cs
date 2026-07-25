using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Accounts;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Operations;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Operations;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CafeChain.Application.Services.Inventories
{
    /// <summary>
    /// Issue #98 — manual shortage report (SALES_REPORT).
    /// Creates/updates OPEN StockAlert even when MinStockLevel is null.
    /// Writes StaffNotification rows; email is non-blocking after DB commit.
    /// </summary>
    public class StockShortageReportService : IStockShortageReportService
    {
        private const int MaxNoteLength = 500;
        private const int MinNoteLength = 5;
        private const int MaxReasonLength = 500;
        private const int MinReasonLength = 5;

        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<StockShortageReportService> _logger;
        private readonly IInventoryNotificationDeliveryService? _notificationDelivery;
        private readonly IInventoryNotificationAudienceResolver? _audienceResolver;

        public StockShortageReportService(
            AppDbContext context,
            IEmailService emailService,
            ILogger<StockShortageReportService> logger,
            IInventoryNotificationDeliveryService? notificationDelivery = null,
            IInventoryNotificationAudienceResolver? audienceResolver = null)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
            _notificationDelivery = notificationDelivery;
            _audienceResolver = audienceResolver;
        }

        public async Task<ServiceResult<StockShortageReportResultDto>> ReportShortageAsync(
            int storeId,
            int reportedByStaffId,
            StockShortageReportRequestDto request)
        {
            if (storeId <= 0)
                return ServiceResult<StockShortageReportResultDto>.Failure("StoreId không hợp lệ.");
            if (reportedByStaffId <= 0)
                return ServiceResult<StockShortageReportResultDto>.Failure("StaffId không hợp lệ.");
            if (request == null)
                return ServiceResult<StockShortageReportResultDto>.Failure("Request không hợp lệ.");

            var note = (request.Note ?? string.Empty).Trim();
            if (note.Length < MinNoteLength)
                return ServiceResult<StockShortageReportResultDto>.Failure(
                    $"Ghi chú báo thiếu hàng phải có ít nhất {MinNoteLength} ký tự.");
            if (note.Length > MaxNoteLength)
                return ServiceResult<StockShortageReportResultDto>.Failure(
                    $"Ghi chú không được vượt quá {MaxNoteLength} ký tự.");

            var inventory = await ResolveInventoryAsync(storeId, request);
            if (inventory == null)
            {
                return ServiceResult<StockShortageReportResultDto>.Failure(
                    "Không tìm thấy tồn kho tại cửa hàng hiện tại.");
            }

            var usableQty = CalculateUsableQuantity(inventory);
            var decisionResult = ResolveShortageDecision(inventory, usableQty, request);
            if (!decisionResult.IsSuccess)
            {
                return ServiceResult<StockShortageReportResultDto>.Failure(
                    decisionResult.Message ?? "Dữ liệu báo thiếu hàng không hợp lệ.");
            }
            var decision = decisionResult.Data!;

            var reporter = await _context.Staffs
                .AsNoTracking()
                .Include(s => s.Account)
                .FirstOrDefaultAsync(s =>
                    s.StaffId == reportedByStaffId &&
                    s.StoreId == storeId &&
                    s.Active);

            if (reporter == null)
            {
                return ServiceResult<StockShortageReportResultDto>.Failure(
                    "Không tìm thấy nhân viên báo cáo hợp lệ tại cửa hàng này.");
            }

            var store = await _context.Stores
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.StoreId == storeId);

            var itemName = ResolveItemName(inventory);
            var itemTypeLabel = inventory.IngredientId.HasValue ? "Nguyên liệu" : "Bán thành phẩm";

            var now = DateTime.UtcNow;
            var preparedItemId = inventory.PreparedItemId ?? inventory.Recipe?.PreparedItemId;
            var recipeId = preparedItemId.HasValue ? null : inventory.RecipeId;

            var openAlert = await _context.StockAlerts
                .FirstOrDefaultAsync(a =>
                    a.StoreId == storeId &&
                    StockAlertStatuses.ActiveValues.Contains(a.Status) &&
                    (inventory.IngredientId.HasValue
                        ? a.IngredientId == inventory.IngredientId
                          && a.RecipeId == null
                          && a.PreparedItemId == null
                        : preparedItemId.HasValue
                            ? a.PreparedItemId == preparedItemId
                              || (inventory.RecipeId.HasValue && a.RecipeId == inventory.RecipeId)
                            : a.RecipeId == inventory.RecipeId));

            string createdOrUpdated;
            string? previousStatus = null;
            string? previousType = null;
            string? previousSeverity = null;
            if (openAlert == null)
            {
                openAlert = new StockAlert
                {
                    StoreId = storeId,
                    IngredientId = inventory.IngredientId,
                    RecipeId = recipeId,
                    PreparedItemId = preparedItemId,
                    AlertType = decision.AlertType,
                    Severity = decision.Severity,
                    Status = StockAlertStatuses.Open,
                    CurrentQtySnapshot = usableQty,
                    ThresholdSnapshot = decision.DecisionTargetBaseQuantity,
                    Source = StockAlertSources.SalesReport,
                    Note = note,
                    ReportedByStaffId = reportedByStaffId,
                    ReportedAt = now,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _context.StockAlerts.Add(openAlert);
                createdOrUpdated = "created";
            }
            else
            {
                previousStatus = openAlert.Status;
                previousType = openAlert.AlertType;
                previousSeverity = openAlert.Severity;
                if (decision.IsOutOfThresholdDemand)
                {
                    openAlert.Status = StockAlertStatuses.Open;
                    openAlert.ConfirmedByStaffId = null;
                    openAlert.ConfirmedAt = null;
                    openAlert.ManagerNote = null;
                }
                openAlert.AlertType = decision.AlertType;
                openAlert.Severity = decision.Severity;
                openAlert.CurrentQtySnapshot = usableQty;
                openAlert.ThresholdSnapshot = decision.DecisionTargetBaseQuantity;
                openAlert.Source = StockAlertSources.SalesReport;
                openAlert.Note = note; // latest note only — no history append
                openAlert.ReportedByStaffId = reportedByStaffId;
                openAlert.ReportedAt = now;
                openAlert.UpdatedAt = now;
                createdOrUpdated = "updated";
            }

            _context.StockAlertTransitions.Add(new StockAlertTransition
            {
                StockAlert = openAlert,
                PreviousStatus = previousStatus,
                NewStatus = openAlert.Status,
                PreviousAlertType = previousType,
                NewAlertType = openAlert.AlertType,
                PreviousSeverity = previousSeverity,
                NewSeverity = openAlert.Severity,
                OnHandSnapshot = inventory.AvailableQty,
                ReservedSnapshot = inventory.ReservedQty,
                AvailableSnapshot = usableQty,
                MinLevelSnapshot = inventory.MinStockLevel,
                SourceType = StockAlertSources.SalesReport,
                Reason = decision.IsOutOfThresholdDemand
                    ? decision.Reason
                    : note,
                ActorStaffId = reportedByStaffId,
                CreatedAtUtc = now
            });

            await _context.SaveChangesAsync(); // need StockAlertId

            var recipients = _audienceResolver == null
                ? await ResolveRecipientsAsync(storeId)
                : [];
            var result = new StockShortageReportResultDto
            {
                StockAlertId = openAlert.StockAlertId,
                CreatedOrUpdated = createdOrUpdated,
                AlertType = decision.AlertType,
                IsOutOfThresholdDemand = decision.IsOutOfThresholdDemand,
                AvailableBaseQuantity = usableQty,
                MinimumThresholdBaseQuantity = inventory.MinStockLevel,
                DecisionTargetBaseQuantity = decision.DecisionTargetBaseQuantity,
                SuggestedBaseQuantity = decision.SuggestedBaseQuantity
            };

            if (_audienceResolver == null && recipients.Count == 0)
            {
                result.Warnings.Add("Chưa tìm thấy người nhận thông báo phù hợp.");
            }

            var title = $"[Kho chi nhánh] Báo thiếu hàng — {itemName}";
            var body =
                $"Cửa hàng: {store?.Name ?? $"#{storeId}"}\n" +
                $"Mặt hàng: {itemName} ({itemTypeLabel})\n" +
                $"Tồn vật lý: {inventory.AvailableQty:N3}\n" +
                $"Đang giữ chỗ: {inventory.ReservedQty:N3}\n" +
                $"Khả dụng: {usableQty:N3}\n" +
                $"Phân loại: {(decision.IsOutOfThresholdDemand ? "Nhu cầu bổ sung ngoài ngưỡng" : "Thiếu theo ngưỡng tối thiểu")}\n" +
                (decision.IsOutOfThresholdDemand
                    ? $"Mục tiêu quyết định: {decision.DecisionTargetBaseQuantity:N3}\nLý do: {decision.Reason}\n"
                    : string.Empty) +
                $"Người báo: {reporter.FullName}\n" +
                $"Thời gian: {now:yyyy-MM-dd HH:mm} UTC\n" +
                $"Ghi chú: {note}";

            if (_notificationDelivery != null && _audienceResolver != null)
            {
                var audience = await _audienceResolver.ResolveAsync(storeId);
                if (audience.Count == 0)
                    result.Warnings.Add("Chưa tìm thấy người nhận có quyền Notification.View trong phạm vi cửa hàng.");

                var delivery = await _notificationDelivery.DeliverAsync(
                    new InventoryNotificationDeliveryRequest(
                        storeId,
                        StaffNotificationTypes.StockShortageReport,
                        title,
                        body,
                        severity,
                        StaffNotificationEntityTypes.StockAlert,
                        openAlert.StockAlertId,
                        createdOrUpdated == "created"
                            ? InventoryNotificationChangeKinds.Created
                            : InventoryNotificationChangeKinds.Updated));
                result.NotificationCount = delivery.CreatedCount + delivery.UpdatedCount;

                foreach (var notification in delivery.EmailCandidates)
                {
                    var recipient = audience.FirstOrDefault(x => x.StaffId == notification.RecipientStaffId);
                    var email = recipient?.Email?.Trim();
                    if (string.IsNullOrWhiteSpace(email))
                        continue;

                    result.EmailAttempted = true;
                    notification.EmailAttempted = true;
                    try
                    {
                        var subject = $"[Kho chi nhánh] Báo thiếu hàng — {store?.Name ?? $"Cửa hàng #{storeId}"}";
                        var html = _emailService.BuildStockShortageReportEmail(
                            store?.Name ?? $"Cửa hàng #{storeId}",
                            itemName,
                            itemTypeLabel,
                            usableQty,
                            note,
                            reporter.FullName,
                            now);
                        await _emailService.SendAsync(email, subject, html);
                        notification.EmailSent = true;
                        result.EmailSentCount++;
                    }
                    catch (Exception ex)
                    {
                        result.EmailFailedCount++;
                        notification.EmailSent = false;
                        notification.EmailErrorSummary = TruncateSafe($"{ex.GetType().Name}: {ex.Message}", 500);
                        _logger.LogWarning(
                            ex,
                            "[StockShortage] EMAIL_FAILED StoreId={StoreId} RecipientStaffId={RecipientStaffId} AlertId={AlertId} ErrorType={ErrorType}",
                            storeId,
                            notification.RecipientStaffId,
                            openAlert.StockAlertId,
                            ex.GetType().Name);
                    }
                }

                if (delivery.EmailCandidates.Any(x => x.EmailAttempted))
                    await _context.SaveChangesAsync();

                return ServiceResult<StockShortageReportResultDto>.Success(
                    result,
                    "Đã gửi thông báo trong hệ thống cho Quản lý chi nhánh, Kế toán/kho và người có quyền trong phạm vi cửa hàng.");
            }

            var notificationEntities = new List<StaffNotification>();
            foreach (var recipient in recipients)
            {
                var n = new StaffNotification
                {
                    StoreId = storeId,
                    RecipientStaffId = recipient.StaffId,
                    Type = StaffNotificationTypes.StockShortageReport,
                    Title = title.Length > 200 ? title[..200] : title,
                    Body = body.Length > 2000 ? body[..2000] : body,
                    EntityType = StaffNotificationEntityTypes.StockAlert,
                    EntityId = openAlert.StockAlertId,
                    IsRead = false,
                    CreatedAt = now,
                    EmailAttempted = false,
                    EmailSent = false
                };
                notificationEntities.Add(n);
                _context.StaffNotifications.Add(n);
            }

            await _context.SaveChangesAsync();
            result.NotificationCount = notificationEntities.Count;

            // Email AFTER DB commit — failures never rollback alert/notifications.
            foreach (var n in notificationEntities)
            {
                var recipient = recipients.First(r => r.StaffId == n.RecipientStaffId);
                var email = recipient.Account?.Email?.Trim();
                if (string.IsNullOrWhiteSpace(email))
                    continue;

                result.EmailAttempted = true;
                n.EmailAttempted = true;

                try
                {
                    var subject = $"[Kho chi nhánh] Báo thiếu hàng — {store?.Name ?? $"Cửa hàng #{storeId}"}";
                    var html = _emailService.BuildStockShortageReportEmail(
                        store?.Name ?? $"Cửa hàng #{storeId}",
                        itemName,
                        itemTypeLabel,
                        usableQty,
                        note,
                        reporter.FullName,
                        now);

                    await _emailService.SendAsync(email, subject, html);
                    n.EmailSent = true;
                    result.EmailSentCount++;
                }
                catch (Exception ex)
                {
                    result.EmailFailedCount++;
                    n.EmailSent = false;
                    n.EmailErrorSummary = TruncateSafe($"{ex.GetType().Name}: {ex.Message}", 500);
                    _logger.LogWarning(
                        ex,
                        "[StockShortage] EMAIL_FAILED StoreId={StoreId} RecipientStaffId={RecipientStaffId} AlertId={AlertId} ErrorType={ErrorType}",
                        storeId,
                        recipient.StaffId,
                        openAlert.StockAlertId,
                        ex.GetType().Name);
                }
            }

            if (notificationEntities.Any(x => x.EmailAttempted))
                await _context.SaveChangesAsync();

            return ServiceResult<StockShortageReportResultDto>.Success(
                result,
                "Đã gửi yêu cầu kiểm tra tồn kho cho Quản lý chi nhánh và Kế toán/kho.");
        }

        private async Task<StoreInventory?> ResolveInventoryAsync(
            int storeId,
            StockShortageReportRequestDto request)
        {
            if (request.StoreInventoryId is > 0)
            {
                return await _context.StoreInventories
                    .Include(i => i.Ingredient)
                    .Include(i => i.Recipe)
                    .Include(i => i.PreparedItem)
                    .FirstOrDefaultAsync(i =>
                        i.StoreInventoryId == request.StoreInventoryId.Value &&
                        i.StoreId == storeId);
            }

            var hasIng = request.IngredientId is > 0;
            var hasRec = request.RecipeId is > 0;
            if (hasIng == hasRec)
                return null;

            return await _context.StoreInventories
                .Include(i => i.Ingredient)
                .Include(i => i.Recipe)
                .Include(i => i.PreparedItem)
                .FirstOrDefaultAsync(i =>
                    i.StoreId == storeId &&
                    i.IngredientId == (hasIng ? request.IngredientId : null) &&
                    i.RecipeId == (hasRec ? request.RecipeId : null));
        }

        private async Task<List<Staff>> ResolveRecipientsAsync(int storeId)
        {
            var roles = new[]
            {
                RoleConstants.StoreManager,
                RoleConstants.AccountantWarehouse
            };

            return await _context.Staffs
                .Include(s => s.Account)
                    .ThenInclude(a => a!.AccountRoles)
                        .ThenInclude(ar => ar.Role)
                .Where(s =>
                    s.StoreId == storeId &&
                    s.Active &&
                    s.Account != null &&
                    s.Account.Active &&
                    s.Account.AccountRoles.Any(ar =>
                        ar.Role != null &&
                        ar.Role.Active &&
                        roles.Contains(ar.Role.Name)))
                .AsNoTracking()
                .ToListAsync();
        }

        private static string ResolveItemName(StoreInventory inventory)
        {
            if (inventory.IngredientId.HasValue)
            {
                return inventory.Ingredient?.Name
                    ?? $"Nguyên liệu #{inventory.IngredientId}";
            }

            if (inventory.PreparedItemId.HasValue || inventory.Recipe?.PreparedItemId != null)
            {
                if (!string.IsNullOrWhiteSpace(inventory.PreparedItem?.Name))
                    return inventory.PreparedItem.Name;
                if (!string.IsNullOrWhiteSpace(inventory.PreparedItem?.Code))
                    return inventory.PreparedItem.Code;
            }

            if (inventory.RecipeId.HasValue)
            {
                if (!string.IsNullOrWhiteSpace(inventory.Recipe?.Name))
                    return inventory.Recipe.Name;
                if (!string.IsNullOrWhiteSpace(inventory.Recipe?.RecipeCode))
                    return inventory.Recipe.RecipeCode;
                return $"Bán thành phẩm #{inventory.RecipeId}";
            }

            return "Mặt hàng không xác định";
        }

        private static ServiceResult<ShortageDecision> ResolveShortageDecision(
            StoreInventory inventory,
            decimal availableQty,
            StockShortageReportRequestDto request)
        {
            var minimum = inventory.MinStockLevel;
            var isOutOfThresholdDemand = !minimum.HasValue || availableQty >= minimum.Value;

            if (!isOutOfThresholdDemand)
            {
                if (request.TargetStockBaseQuantity.HasValue
                    || request.ForecastDemandUntilDeliveryBaseQuantity.HasValue)
                {
                    return ServiceResult<ShortageDecision>.Failure(
                        "Mặt hàng đang thiếu theo ngưỡng; không cần nhập mục tiêu hoặc dự báo thủ công.");
                }

                var alertType = availableQty <= 0
                    ? StockAlertTypes.OutOfStock
                    : StockAlertTypes.LowStock;
                var severity = availableQty <= 0
                    ? StockAlertSeverities.Urgent
                    : StockAlertSeverities.Warning;
                return ServiceResult<ShortageDecision>.Success(new ShortageDecision(
                    false,
                    alertType,
                    severity,
                    minimum,
                    Math.Max(0m, minimum!.Value - availableQty),
                    null));
            }

            var reason = (request.Reason ?? string.Empty).Trim();
            if (reason.Length < MinReasonLength)
            {
                return ServiceResult<ShortageDecision>.Failure(
                    $"Lý do báo thiếu ngoài ngưỡng phải có ít nhất {MinReasonLength} ký tự.");
            }
            if (reason.Length > MaxReasonLength)
            {
                return ServiceResult<ShortageDecision>.Failure(
                    $"Lý do không được vượt quá {MaxReasonLength} ký tự.");
            }

            var hasTarget = request.TargetStockBaseQuantity.HasValue;
            var hasForecast = request.ForecastDemandUntilDeliveryBaseQuantity.HasValue;
            if (hasTarget == hasForecast)
            {
                return ServiceResult<ShortageDecision>.Failure(
                    "Báo thiếu ngoài ngưỡng phải chọn đúng một dữ liệu: mục tiêu tồn hoặc dự báo nhu cầu.");
            }

            decimal decisionTarget;
            if (hasTarget)
            {
                decisionTarget = request.TargetStockBaseQuantity!.Value;
                if (decisionTarget <= availableQty)
                {
                    return ServiceResult<ShortageDecision>.Failure(
                        "Mục tiêu tồn phải lớn hơn lượng khả dụng hiện tại.");
                }
            }
            else
            {
                var forecast = request.ForecastDemandUntilDeliveryBaseQuantity!.Value;
                if (forecast <= 0)
                {
                    return ServiceResult<ShortageDecision>.Failure(
                        "Dự báo nhu cầu đến khi nhận hàng phải lớn hơn 0.");
                }
                decisionTarget = availableQty + forecast;
            }

            var manualAlertType = availableQty <= 0 && !minimum.HasValue
                ? StockAlertTypes.OutOfStock
                : StockAlertTypes.ManualReview;
            var manualSeverity = manualAlertType == StockAlertTypes.OutOfStock
                ? StockAlertSeverities.Urgent
                : StockAlertSeverities.Review;

            return ServiceResult<ShortageDecision>.Success(new ShortageDecision(
                true,
                manualAlertType,
                manualSeverity,
                decisionTarget,
                decisionTarget - availableQty,
                reason));
        }

        private static decimal CalculateUsableQuantity(StoreInventory inventory) =>
            inventory.AvailableQty - inventory.ReservedQty;

        private static string TruncateSafe(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= max ? value : value[..max];
        }

        private sealed record ShortageDecision(
            bool IsOutOfThresholdDemand,
            string AlertType,
            string Severity,
            decimal? DecisionTargetBaseQuantity,
            decimal SuggestedBaseQuantity,
            string? Reason);
    }
}
