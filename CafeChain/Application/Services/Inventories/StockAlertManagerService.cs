using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.StockAlerts;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CafeChain.Application.Services.Inventories
{
    /// <summary>
    /// Issue #99 — StoreManager confirm/reject OPEN StockAlert for their store.
    /// </summary>
    public class StockAlertManagerService : IStockAlertManagerService
    {
        private const int MaxTextLength = 500;

        private readonly AppDbContext _context;
        private readonly ILogger<StockAlertManagerService> _logger;

        public StockAlertManagerService(
            AppDbContext context,
            ILogger<StockAlertManagerService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ServiceResult> ConfirmAsync(
            int alertId,
            int managerStaffId,
            int managerStoreId,
            string note)
        {
            var text = (note ?? string.Empty).Trim();
            if (text.Length < 1 || text.Length > MaxTextLength)
                return ServiceResult.Failure("Vui lòng nhập ghi chú xác nhận (1–500 ký tự).");

            var gate = await LoadOpenAlertForManagerAsync(alertId, managerStaffId, managerStoreId);
            if (!gate.IsSuccess)
                return ServiceResult.Failure(gate.Message);

            var alert = gate.Data!;
            var now = DateTime.UtcNow;

            alert.Status = StockAlertStatuses.Confirmed;
            alert.ConfirmedByStaffId = managerStaffId;
            alert.ConfirmedAt = now;
            alert.ManagerNote = text;
            alert.UpdatedAt = now;
            // Do not overwrite reporter Note / Reject* fields.

            await NotifyReporterAsync(
                alert,
                StaffNotificationTypes.StockAlertConfirmed,
                "Quản lý đã xác nhận cảnh báo kho",
                $"Quản lý chi nhánh đã xác nhận cảnh báo kho.\nGhi chú: {text}");

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[StockAlert] CONFIRMED AlertId={AlertId} StoreId={StoreId} ByStaffId={StaffId}",
                alert.StockAlertId, alert.StoreId, managerStaffId);

            return ServiceResult.Success("Đã xác nhận cảnh báo kho.");
        }

        public async Task<ServiceResult> RejectAsync(
            int alertId,
            int managerStaffId,
            int managerStoreId,
            string reason)
        {
            var text = (reason ?? string.Empty).Trim();
            if (text.Length < 1 || text.Length > MaxTextLength)
                return ServiceResult.Failure("Vui lòng nhập lý do báo sai (1–500 ký tự).");

            var gate = await LoadOpenAlertForManagerAsync(alertId, managerStaffId, managerStoreId);
            if (!gate.IsSuccess)
                return ServiceResult.Failure(gate.Message);

            var alert = gate.Data!;
            var now = DateTime.UtcNow;

            alert.Status = StockAlertStatuses.ManagerRejected;
            alert.RejectedByStaffId = managerStaffId;
            alert.RejectedAt = now;
            alert.RejectReason = text;
            alert.UpdatedAt = now;

            await NotifyReporterAsync(
                alert,
                StaffNotificationTypes.StockAlertRejected,
                "Quản lý đã báo sai cảnh báo kho",
                $"Quản lý chi nhánh đã báo sai cảnh báo kho.\nLý do: {text}");

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[StockAlert] MANAGER_REJECTED AlertId={AlertId} StoreId={StoreId} ByStaffId={StaffId}",
                alert.StockAlertId, alert.StoreId, managerStaffId);

            return ServiceResult.Success("Đã báo sai cảnh báo kho.");
        }

        public async Task<ServiceResult<StockAlertListResultDto>> ListForStoreAsync(
            int storeId,
            string? statusFilter,
            int page,
            int pageSize)
        {
            if (storeId <= 0)
                return ServiceResult<StockAlertListResultDto>.Failure("StoreId không hợp lệ.");

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var query = _context.StockAlerts
                .AsNoTracking()
                .Include(a => a.Ingredient)
                .Include(a => a.Recipe)
                .Include(a => a.PreparedItem)
                .Include(a => a.ReportedByStaff)
                .Where(a => a.StoreId == storeId);

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                var st = statusFilter.Trim().ToUpperInvariant();
                query = query.Where(a => a.Status == st);
            }

            var total = await query.CountAsync();
            var rows = await query
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.StockAlertId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return ServiceResult<StockAlertListResultDto>.Success(new StockAlertListResultDto
            {
                StoreId = storeId,
                StatusFilter = statusFilter,
                Page = page,
                PageSize = pageSize,
                Total = total,
                Items = rows.Select(MapListItem).ToList()
            });
        }

        public async Task<ServiceResult<StockAlertDetailDto>> GetDetailAsync(int alertId, int managerStoreId)
        {
            if (managerStoreId <= 0)
                return ServiceResult<StockAlertDetailDto>.Failure("StoreId không hợp lệ.");

            var alert = await _context.StockAlerts
                .AsNoTracking()
                .Include(a => a.Ingredient)
                .Include(a => a.Recipe)
                .Include(a => a.PreparedItem)
                .Include(a => a.ReportedByStaff)
                .Include(a => a.ConfirmedByStaff)
                .Include(a => a.RejectedByStaff)
                .FirstOrDefaultAsync(a => a.StockAlertId == alertId);

            if (alert == null)
                return ServiceResult<StockAlertDetailDto>.Failure("Không tìm thấy cảnh báo.");

            if (alert.StoreId != managerStoreId)
                return ServiceResult<StockAlertDetailDto>.Failure("Cảnh báo không thuộc cửa hàng của bạn.");

            return ServiceResult<StockAlertDetailDto>.Success(MapDetail(alert));
        }

        private async Task<ServiceResult<StockAlert>> LoadOpenAlertForManagerAsync(
            int alertId,
            int managerStaffId,
            int managerStoreId)
        {
            if (managerStaffId <= 0 || managerStoreId <= 0)
                return ServiceResult<StockAlert>.Failure("Thiếu thông tin quản lý cửa hàng.");

            var isStoreManager = await _context.Staffs
                .AsNoTracking()
                .Where(s => s.StaffId == managerStaffId && s.Active)
                .SelectMany(s => s.Account.AccountRoles)
                .AnyAsync(ar =>
                    ar.Role != null &&
                    ar.Role.Active &&
                    ar.Role.Name == RoleConstants.StoreManager);

            if (!isStoreManager)
            {
                return ServiceResult<StockAlert>.Failure(
                    "Chỉ Quản lý chi nhánh được xác nhận/từ chối cảnh báo.");
            }

            var alert = await _context.StockAlerts
                .Include(a => a.Ingredient)
                .Include(a => a.Recipe)
                .Include(a => a.PreparedItem)
                .FirstOrDefaultAsync(a => a.StockAlertId == alertId);

            if (alert == null)
                return ServiceResult<StockAlert>.Failure("Không tìm thấy cảnh báo.");

            if (alert.StoreId != managerStoreId)
                return ServiceResult<StockAlert>.Failure("Cảnh báo không thuộc cửa hàng của bạn.");

            if (alert.Status != StockAlertStatuses.Open)
            {
                return ServiceResult<StockAlert>.Failure(
                    $"Chỉ xử lý cảnh báo đang OPEN. Trạng thái hiện tại: {alert.Status}.");
            }

            return ServiceResult<StockAlert>.Success(alert);
        }

        private async Task NotifyReporterAsync(
            StockAlert alert,
            string type,
            string title,
            string body)
        {
            if (!alert.ReportedByStaffId.HasValue || alert.ReportedByStaffId.Value <= 0)
                return;

            var reporterActive = await _context.Staffs
                .AsNoTracking()
                .AnyAsync(s =>
                    s.StaffId == alert.ReportedByStaffId.Value &&
                    s.Active);

            if (!reporterActive)
                return;

            var itemName = ResolveItemName(alert);
            var fullBody =
                $"{body}\n" +
                $"Mặt hàng: {itemName}\n" +
                $"Loại cảnh báo: {alert.AlertType}\n" +
                $"Tồn snapshot: {alert.CurrentQtySnapshot:N3}";

            _context.StaffNotifications.Add(new StaffNotification
            {
                StoreId = alert.StoreId,
                RecipientStaffId = alert.ReportedByStaffId.Value,
                Type = type,
                Title = title.Length > 200 ? title[..200] : title,
                Body = fullBody.Length > 2000 ? fullBody[..2000] : fullBody,
                EntityType = StaffNotificationEntityTypes.StockAlert,
                EntityId = alert.StockAlertId,
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
                EmailAttempted = false,
                EmailSent = false
            });
        }

        private static StockAlertListItemDto MapListItem(StockAlert a) => new()
        {
            StockAlertId = a.StockAlertId,
            StoreId = a.StoreId,
            ItemName = ResolveItemName(a),
            ItemTypeLabel = a.IngredientId.HasValue
                ? "Nguyên liệu"
                : (a.PreparedItemId.HasValue ? "Bán thành phẩm (PreparedItem)" : "Bán thành phẩm"),
            AlertType = a.AlertType,
            Severity = a.Severity,
            Status = a.Status,
            Source = a.Source,
            CurrentQtySnapshot = a.CurrentQtySnapshot,
            ThresholdSnapshot = a.ThresholdSnapshot,
            ReporterNote = a.Note,
            ReporterName = a.ReportedByStaff?.FullName,
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt
        };

        private static StockAlertDetailDto MapDetail(StockAlert a)
        {
            var dto = new StockAlertDetailDto
            {
                StockAlertId = a.StockAlertId,
                StoreId = a.StoreId,
                ItemName = ResolveItemName(a),
                ItemTypeLabel = a.IngredientId.HasValue
                    ? "Nguyên liệu"
                    : (a.PreparedItemId.HasValue ? "Bán thành phẩm (PreparedItem)" : "Bán thành phẩm"),
                AlertType = a.AlertType,
                Severity = a.Severity,
                Status = a.Status,
                Source = a.Source,
                CurrentQtySnapshot = a.CurrentQtySnapshot,
                ThresholdSnapshot = a.ThresholdSnapshot,
                ReporterNote = a.Note,
                ReporterName = a.ReportedByStaff?.FullName,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt,
                IngredientId = a.IngredientId,
                RecipeId = a.RecipeId,
                PreparedItemId = a.PreparedItemId,
                ReportedByStaffId = a.ReportedByStaffId,
                ReportedAt = a.ReportedAt,
                ConfirmedByStaffId = a.ConfirmedByStaffId,
                ConfirmedByName = a.ConfirmedByStaff?.FullName,
                ConfirmedAt = a.ConfirmedAt,
                ManagerNote = a.ManagerNote,
                RejectedByStaffId = a.RejectedByStaffId,
                RejectedByName = a.RejectedByStaff?.FullName,
                RejectedAt = a.RejectedAt,
                RejectReason = a.RejectReason,
                ResolvedAt = a.ResolvedAt,
                ResolvedReason = a.ResolvedReason
            };
            return dto;
        }

        private static string ResolveItemName(StockAlert a)
        {
            if (a.IngredientId.HasValue)
                return a.Ingredient?.Name ?? $"Nguyên liệu #{a.IngredientId}";
            if (a.PreparedItemId.HasValue)
            {
                if (!string.IsNullOrWhiteSpace(a.PreparedItem?.Name)) return a.PreparedItem.Name;
                if (!string.IsNullOrWhiteSpace(a.PreparedItem?.Code)) return a.PreparedItem.Code;
                return $"PreparedItem #{a.PreparedItemId}";
            }
            if (a.RecipeId.HasValue)
            {
                if (!string.IsNullOrWhiteSpace(a.Recipe?.Name)) return a.Recipe.Name;
                if (!string.IsNullOrWhiteSpace(a.Recipe?.RecipeCode)) return a.Recipe.RecipeCode;
                return $"Bán thành phẩm #{a.RecipeId}";
            }
            return "Mặt hàng không xác định";
        }
    }
}
