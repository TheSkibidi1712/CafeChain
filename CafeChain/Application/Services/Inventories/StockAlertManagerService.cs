using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.StockAlerts;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
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
        private readonly IScopeAuthorizationService _scopeAuthorization;
        private readonly ILogger<StockAlertManagerService> _logger;

        public StockAlertManagerService(
            AppDbContext context,
            IScopeAuthorizationService scopeAuthorization,
            ILogger<StockAlertManagerService> logger)
        {
            _context = context;
            _scopeAuthorization = scopeAuthorization;
            _logger = logger;
        }

        public async Task<ServiceResult> ConfirmAsync(
            int alertId,
            int managerStaffId,
            int managerStoreId,
            string note,
            string? rowVersion)
        {
            var text = (note ?? string.Empty).Trim();
            if (text.Length < 1 || text.Length > MaxTextLength)
                return ServiceResult.Failure("Vui lòng nhập ghi chú xác nhận (1–500 ký tự).");

            var gate = await LoadOpenAlertForManagerAsync(alertId, managerStaffId, managerStoreId);
            if (!gate.IsSuccess)
                return ServiceResult.Failure(gate.Message);

            var alert = gate.Data!;
            var versionFailure = ApplyRequiredRowVersion(alert, rowVersion);
            if (versionFailure != null)
                return versionFailure;
            var now = DateTime.UtcNow;
            var previousStatus = alert.Status;

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

            await RecordTransitionAsync(alert, previousStatus, alert.AlertType, alert.Severity, managerStaffId, text);

            if (!await SaveManagerTransitionAsync())
                return ResourceChanged();

            _logger.LogInformation(
                "[StockAlert] CONFIRMED AlertId={AlertId} StoreId={StoreId} ByStaffId={StaffId}",
                alert.StockAlertId, alert.StoreId, managerStaffId);

            return ServiceResult.Success("Đã xác nhận cảnh báo kho.");
        }

        public async Task<ServiceResult> RejectAsync(
            int alertId,
            int managerStaffId,
            int managerStoreId,
            string reason,
            string? rowVersion)
        {
            var text = (reason ?? string.Empty).Trim();
            if (text.Length < 1 || text.Length > MaxTextLength)
                return ServiceResult.Failure("Vui lòng nhập lý do báo sai (1–500 ký tự).");

            var gate = await LoadOpenAlertForManagerAsync(alertId, managerStaffId, managerStoreId);
            if (!gate.IsSuccess)
                return ServiceResult.Failure(gate.Message);

            var alert = gate.Data!;
            var versionFailure = ApplyRequiredRowVersion(alert, rowVersion);
            if (versionFailure != null)
                return versionFailure;
            var now = DateTime.UtcNow;
            var previousStatus = alert.Status;

            alert.Status = StockAlertStatuses.Rejected;
            alert.RejectedByStaffId = managerStaffId;
            alert.RejectedAt = now;
            alert.RejectReason = text;
            alert.UpdatedAt = now;

            await NotifyReporterAsync(
                alert,
                StaffNotificationTypes.StockAlertRejected,
                "Quản lý đã báo sai cảnh báo kho",
                $"Quản lý chi nhánh đã báo sai cảnh báo kho.\nLý do: {text}");

            await RecordTransitionAsync(alert, previousStatus, alert.AlertType, alert.Severity, managerStaffId, text);

            if (!await SaveManagerTransitionAsync())
                return ResourceChanged();

            _logger.LogInformation(
                "[StockAlert] REJECTED AlertId={AlertId} StoreId={StoreId} ByStaffId={StaffId}",
                alert.StockAlertId, alert.StoreId, managerStaffId);

            return ServiceResult.Success("Đã báo sai cảnh báo kho.");
        }

        public async Task<ServiceResult> CloseAsync(
            int alertId,
            int managerStaffId,
            int managerStoreId,
            string reason,
            string? rowVersion)
        {
            var text = (reason ?? string.Empty).Trim();
            if (text.Length < 1 || text.Length > MaxTextLength)
                return ServiceResult.Failure("Vui lòng nhập lý do đóng cảnh báo (1–500 ký tự).");

            var alert = await _context.StockAlerts
                .Include(a => a.Ingredient)
                .Include(a => a.Recipe)
                .Include(a => a.PreparedItem)
                .FirstOrDefaultAsync(a => a.StockAlertId == alertId);
            if (alert == null)
                return ServiceResult.Failure("Không tìm thấy cảnh báo.");
            if (alert.StoreId != managerStoreId)
                return ServiceResult.Failure("Cảnh báo không thuộc cửa hàng của bạn.");
            if (alert.Status is not (StockAlertStatuses.Open or StockAlertStatuses.Confirmed))
                return ServiceResult.Failure($"Không thể đóng cảnh báo ở trạng thái {alert.Status}.");

            if (!await IsAuthorizedManagerAsync(managerStaffId, managerStoreId))
                return ServiceResult.Failure("Bạn không có quyền đóng cảnh báo tại cửa hàng này.");

            var versionFailure = ApplyRequiredRowVersion(alert, rowVersion);
            if (versionFailure != null)
                return versionFailure;

            var previousStatus = alert.Status;
            alert.Status = StockAlertStatuses.Closed;
            alert.UpdatedAt = DateTime.UtcNow;
            alert.ResolvedReason = text;
            await RecordTransitionAsync(alert, previousStatus, alert.AlertType, alert.Severity, managerStaffId, text);

            if (!await SaveManagerTransitionAsync())
                return ResourceChanged();

            return ServiceResult.Success("Đã đóng cảnh báo kho.");
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

            var items = rows.Select(MapListItem).ToList();
            await PopulateCurrentInventoryAsync(storeId, items);

            return ServiceResult<StockAlertListResultDto>.Success(new StockAlertListResultDto
            {
                StoreId = storeId,
                StatusFilter = statusFilter,
                Page = page,
                PageSize = pageSize,
                Total = total,
                Items = items
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
                .Include(a => a.Transitions)
                    .ThenInclude(t => t.ActorStaff)
                .FirstOrDefaultAsync(a => a.StockAlertId == alertId);

            if (alert == null)
                return ServiceResult<StockAlertDetailDto>.Failure("Không tìm thấy cảnh báo.");

            if (alert.StoreId != managerStoreId)
                return ServiceResult<StockAlertDetailDto>.Failure("Cảnh báo không thuộc cửa hàng của bạn.");

            var detail = MapDetail(alert);
            await PopulateCurrentInventoryAsync(managerStoreId, new[] { detail });
            detail.RecentMovements = await LoadRecentMovementsAsync(detail);
            detail.Transitions = alert.Transitions
                .OrderBy(t => t.CreatedAtUtc)
                .ThenBy(t => t.StockAlertTransitionId)
                .Select(t => new StockAlertTransitionDto
                {
                    StockAlertTransitionId = t.StockAlertTransitionId,
                    PreviousStatus = t.PreviousStatus,
                    NewStatus = t.NewStatus,
                    PreviousAlertType = t.PreviousAlertType,
                    NewAlertType = t.NewAlertType,
                    PreviousSeverity = t.PreviousSeverity,
                    NewSeverity = t.NewSeverity,
                    OnHandSnapshot = t.OnHandSnapshot,
                    ReservedSnapshot = t.ReservedSnapshot,
                    AvailableSnapshot = t.AvailableSnapshot,
                    MinLevelSnapshot = t.MinLevelSnapshot,
                    SourceType = t.SourceType,
                    SourceId = t.SourceId,
                    Reason = t.Reason,
                    ActorStaffId = t.ActorStaffId,
                    ActorName = t.ActorStaff?.FullName,
                    CreatedAtUtc = t.CreatedAtUtc
                })
                .ToList();
            return ServiceResult<StockAlertDetailDto>.Success(detail);
        }

        private async Task<ServiceResult<StockAlert>> LoadOpenAlertForManagerAsync(
            int alertId,
            int managerStaffId,
            int managerStoreId)
        {
            if (managerStaffId <= 0 || managerStoreId <= 0)
                return ServiceResult<StockAlert>.Failure("Thiếu thông tin quản lý cửa hàng.");

            if (!await IsAuthorizedManagerAsync(managerStaffId, managerStoreId))
            {
                return ServiceResult<StockAlert>.Failure(
                    "Bạn không có quyền xác nhận/từ chối cảnh báo tại cửa hàng này.");
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

        private async Task<bool> IsAuthorizedManagerAsync(int staffId, int storeId)
        {
            var staff = await _context.Staffs
                .AsNoTracking()
                .Where(s => s.StaffId == staffId && s.Active)
                .Select(s => new
                {
                    s.StoreId,
                    Roles = s.Account.AccountRoles
                        .Where(ar => ar.Role != null && ar.Role.Active)
                        .Select(ar => ar.Role.Name)
                        .ToList()
                })
                .FirstOrDefaultAsync();
            if (staff == null)
                return false;
            if (staff.Roles.Contains(RoleConstants.BusinessOwner))
                return true;
            if (staff.Roles.Contains(RoleConstants.AreaManager))
                return await _scopeAuthorization.CanAccessStoreAsync(staffId, storeId);
            return staff.Roles.Contains(RoleConstants.StoreManager) && staff.StoreId == storeId;
        }

        private async Task RecordTransitionAsync(
            StockAlert alert,
            string? previousStatus,
            string? previousType,
            string? previousSeverity,
            int actorStaffId,
            string reason)
        {
            var inventory = await _context.StoreInventories
                .AsNoTracking()
                .Include(i => i.Recipe)
                .Where(i => i.StoreId == alert.StoreId)
                .Where(i => alert.IngredientId.HasValue
                    ? i.IngredientId == alert.IngredientId
                    : i.PreparedItemId == alert.PreparedItemId
                      || i.Recipe!.PreparedItemId == alert.PreparedItemId)
                .OrderBy(i => i.StoreInventoryId)
                .FirstOrDefaultAsync();

            var onHand = inventory?.AvailableQty ?? alert.CurrentQtySnapshot;
            var reserved = inventory?.ReservedQty ?? 0m;
            _context.StockAlertTransitions.Add(new StockAlertTransition
            {
                StockAlertId = alert.StockAlertId,
                PreviousStatus = previousStatus,
                NewStatus = alert.Status,
                PreviousAlertType = previousType,
                NewAlertType = alert.AlertType,
                PreviousSeverity = previousSeverity,
                NewSeverity = alert.Severity,
                OnHandSnapshot = onHand,
                ReservedSnapshot = reserved,
                AvailableSnapshot = onHand - reserved,
                MinLevelSnapshot = inventory?.MinStockLevel ?? alert.ThresholdSnapshot,
                SourceType = "MANAGER_ACTION",
                Reason = reason,
                ActorStaffId = actorStaffId,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        private async Task<bool> SaveManagerTransitionAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                _context.ChangeTracker.Clear();
                return false;
            }
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
            IngredientId = a.IngredientId,
            RecipeId = a.RecipeId,
            PreparedItemId = a.PreparedItemId,
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
            IsOutOfThresholdManualDemand = a.AlertType == StockAlertTypes.ManualReview,
            DecisionTargetBaseQuantity = a.AlertType == StockAlertTypes.ManualReview
                ? a.ThresholdSnapshot
                : null,
            ReporterNote = a.Note,
            ReporterName = a.ReportedByStaff?.FullName,
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt
        };

        private async Task PopulateCurrentInventoryAsync(
            int storeId,
            IEnumerable<StockAlertListItemDto> alerts)
        {
            var rows = await _context.StoreInventories
                .AsNoTracking()
                .Include(i => i.Ingredient)
                    .ThenInclude(i => i!.BaseUnit)
                .Include(i => i.Recipe)
                .Include(i => i.PreparedItem)
                    .ThenInclude(i => i!.BaseUnit)
                .Where(i => i.StoreId == storeId)
                .ToListAsync();

            foreach (var alert in alerts)
            {
                var inventory = rows.FirstOrDefault(i =>
                    alert.IngredientId.HasValue
                        ? i.IngredientId == alert.IngredientId
                        : alert.PreparedItemId.HasValue
                            ? i.PreparedItemId == alert.PreparedItemId
                              || i.Recipe?.PreparedItemId == alert.PreparedItemId
                            : i.RecipeId == alert.RecipeId);

                if (inventory == null)
                    continue;

                alert.HasCurrentInventory = true;
                alert.OnHandQty = inventory.AvailableQty;
                alert.ReservedQty = inventory.ReservedQty;
                alert.AvailableQty = inventory.AvailableQty - inventory.ReservedQty;
                alert.CurrentMinimumThresholdBaseQuantity = inventory.MinStockLevel;
                alert.IsOutOfThresholdManualDemand =
                    alert.Source == StockAlertSources.SalesReport
                    && (!inventory.MinStockLevel.HasValue
                        || alert.CurrentQtySnapshot >= inventory.MinStockLevel.Value);
                alert.DecisionTargetBaseQuantity =
                    alert.IsOutOfThresholdManualDemand
                        ? alert.ThresholdSnapshot
                        : null;
                alert.BaseUnitName = inventory.Ingredient?.BaseUnit?.Name
                    ?? inventory.PreparedItem?.BaseUnit?.Name;
            }
        }

        private static StockAlertDetailDto MapDetail(StockAlert a)
        {
            var dto = new StockAlertDetailDto
            {
                StockAlertId = a.StockAlertId,
                RowVersion = Convert.ToBase64String(a.RowVersion ?? Array.Empty<byte>()),
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
                IsOutOfThresholdManualDemand = a.AlertType == StockAlertTypes.ManualReview,
                DecisionTargetBaseQuantity = a.AlertType == StockAlertTypes.ManualReview
                    ? a.ThresholdSnapshot
                    : null,
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

        private ServiceResult? ApplyRequiredRowVersion(StockAlert alert, string? rowVersion)
        {
            if (string.IsNullOrWhiteSpace(rowVersion))
            {
                return ServiceResult.Failure(
                    "Thiếu phiên bản dữ liệu. Vui lòng tải lại trang.",
                    errorCode: BranchReceiptErrorCodes.ValidationRowVersionRequired);
            }

            byte[] expected;
            try
            {
                expected = Convert.FromBase64String(rowVersion);
            }
            catch (FormatException)
            {
                return ServiceResult.Failure(
                    "Phiên bản dữ liệu không hợp lệ. Vui lòng tải lại trang.",
                    errorCode: BranchReceiptErrorCodes.ValidationRowVersionRequired);
            }

            if (expected.Length == 0 || !alert.RowVersion.SequenceEqual(expected))
                return ResourceChanged();

            _context.Entry(alert).Property(x => x.RowVersion).OriginalValue = expected;
            return null;
        }

        private static ServiceResult ResourceChanged() =>
            ServiceResult.Failure(
                "Dữ liệu đã được người khác cập nhật. Vui lòng tải lại trước khi thao tác.",
                errorCode: BranchReceiptErrorCodes.ResourceChanged);

        private async Task<List<StockAlertMovementDto>> LoadRecentMovementsAsync(
            StockAlertDetailDto alert)
        {
            var inventoryId = await _context.StoreInventories
                .AsNoTracking()
                .Where(i => i.StoreId == alert.StoreId)
                .Where(i => alert.IngredientId.HasValue
                    ? i.IngredientId == alert.IngredientId
                    : alert.PreparedItemId.HasValue
                        ? i.PreparedItemId == alert.PreparedItemId
                          || i.Recipe!.PreparedItemId == alert.PreparedItemId
                        : i.RecipeId == alert.RecipeId)
                .OrderBy(i => i.StoreInventoryId)
                .Select(i => (int?)i.StoreInventoryId)
                .FirstOrDefaultAsync();

            if (!inventoryId.HasValue)
                return new List<StockAlertMovementDto>();

            return await _context.InventoryTransactions
                .AsNoTracking()
                .Where(t => t.StoreInventoryId == inventoryId.Value)
                .OrderByDescending(t => t.CreatedAt)
                .ThenByDescending(t => t.InventoryTransactionId)
                .Take(8)
                .Select(t => new StockAlertMovementDto
                {
                    InventoryTransactionId = t.InventoryTransactionId,
                    Type = t.Type.ToString(),
                    Quantity = t.Quantity,
                    BeforeQty = t.BeforeQty,
                    AfterQty = t.AfterQty,
                    CreatedAt = t.CreatedAt,
                    InventoryDocumentId = t.InventoryDocumentId,
                    InventoryTransferId = t.InventoryTransferId,
                    ReferenceOrderId = t.ReferenceOrderId,
                    ProductionRunId = t.ProductionRunId,
                    BranchReceiptLineId = t.BranchReceiptLineId
                })
                .ToListAsync();
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
