using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Accounts;
using CafeChain.Application.Interfaces.Inventories;
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

        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<StockShortageReportService> _logger;

        public StockShortageReportService(
            AppDbContext context,
            IEmailService emailService,
            ILogger<StockShortageReportService> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
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
            var (alertType, severity) = MapTypeSeverity(inventory.AvailableQty);

            var openAlert = await _context.StockAlerts
                .FirstOrDefaultAsync(a =>
                    a.StoreId == storeId &&
                    a.Status == StockAlertStatuses.Open &&
                    a.IngredientId == inventory.IngredientId &&
                    a.RecipeId == inventory.RecipeId);

            string createdOrUpdated;
            if (openAlert == null)
            {
                openAlert = new StockAlert
                {
                    StoreId = storeId,
                    IngredientId = inventory.IngredientId,
                    RecipeId = inventory.RecipeId,
                    AlertType = alertType,
                    Severity = severity,
                    Status = StockAlertStatuses.Open,
                    CurrentQtySnapshot = inventory.AvailableQty,
                    ThresholdSnapshot = inventory.MinStockLevel,
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
                openAlert.AlertType = alertType;
                openAlert.Severity = severity;
                openAlert.CurrentQtySnapshot = inventory.AvailableQty;
                openAlert.ThresholdSnapshot = inventory.MinStockLevel;
                openAlert.Source = StockAlertSources.SalesReport;
                openAlert.Note = note; // latest note only — no history append
                openAlert.ReportedByStaffId = reportedByStaffId;
                openAlert.ReportedAt = now;
                openAlert.UpdatedAt = now;
                createdOrUpdated = "updated";
            }

            await _context.SaveChangesAsync(); // need StockAlertId

            var recipients = await ResolveRecipientsAsync(storeId);
            var result = new StockShortageReportResultDto
            {
                StockAlertId = openAlert.StockAlertId,
                CreatedOrUpdated = createdOrUpdated
            };

            if (recipients.Count == 0)
            {
                result.Warnings.Add("Chưa tìm thấy người nhận thông báo phù hợp.");
            }

            var title = $"[Kho chi nhánh] Báo thiếu hàng — {itemName}";
            var body =
                $"Cửa hàng: {store?.Name ?? $"#{storeId}"}\n" +
                $"Mặt hàng: {itemName} ({itemTypeLabel})\n" +
                $"Tồn hiện tại: {inventory.AvailableQty:N3}\n" +
                $"Người báo: {reporter.FullName}\n" +
                $"Thời gian: {now:yyyy-MM-dd HH:mm} UTC\n" +
                $"Ghi chú: {note}";

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
                        inventory.AvailableQty,
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

        private static (string alertType, string severity) MapTypeSeverity(decimal availableQty)
        {
            if (availableQty <= 0)
                return (StockAlertTypes.OutOfStock, StockAlertSeverities.Urgent);
            return (StockAlertTypes.LowStock, StockAlertSeverities.Warning);
        }

        private static string TruncateSafe(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= max ? value : value[..max];
        }
    }
}
