using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.RestockRequests;
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
    /// Issue #100 — StoreManager creates RestockRequest from CONFIRMED StockAlert;
    /// notifies AccountantWarehouse. No inventory mutation / InventoryDocument.
    /// </summary>
    public class RestockRequestService : IRestockRequestService
    {
        private const int MaxNoteLength = 500;

        private readonly AppDbContext _context;
        private readonly ILogger<RestockRequestService> _logger;

        public RestockRequestService(
            AppDbContext context,
            ILogger<RestockRequestService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ServiceResult<CreateRestockRequestResultDto>> CreateFromConfirmedAlertAsync(
            int alertId,
            int managerStaffId,
            int managerStoreId,
            decimal requestedQuantity,
            string? note,
            string? priority)
        {
            if (requestedQuantity <= 0)
            {
                return ServiceResult<CreateRestockRequestResultDto>.Failure(
                    "Số lượng yêu cầu phải lớn hơn 0.");
            }

            var noteText = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            if (noteText != null && noteText.Length > MaxNoteLength)
            {
                return ServiceResult<CreateRestockRequestResultDto>.Failure(
                    "Ghi chú tối đa 500 ký tự.");
            }

            if (managerStaffId <= 0 || managerStoreId <= 0)
            {
                return ServiceResult<CreateRestockRequestResultDto>.Failure(
                    "Thiếu thông tin quản lý cửa hàng.");
            }

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
                return ServiceResult<CreateRestockRequestResultDto>.Failure(
                    "Chỉ Quản lý chi nhánh được tạo yêu cầu nhập hàng.");
            }

            var alert = await _context.StockAlerts
                .Include(a => a.Ingredient)
                .Include(a => a.Recipe)
                .Include(a => a.Store)
                .FirstOrDefaultAsync(a => a.StockAlertId == alertId);

            if (alert == null)
            {
                return ServiceResult<CreateRestockRequestResultDto>.Failure(
                    "Không tìm thấy cảnh báo.");
            }

            if (alert.StoreId != managerStoreId)
            {
                return ServiceResult<CreateRestockRequestResultDto>.Failure(
                    "Cảnh báo không thuộc cửa hàng của bạn.");
            }

            if (alert.Status != StockAlertStatuses.Confirmed)
            {
                return ServiceResult<CreateRestockRequestResultDto>.Failure(
                    $"Chỉ tạo yêu cầu nhập hàng từ cảnh báo đã xác nhận (CONFIRMED). Trạng thái hiện tại: {alert.Status}.");
            }

            var hasOpen = await _context.RestockRequests
                .AnyAsync(r =>
                    r.StockAlertId == alertId &&
                    r.Status == RestockRequestStatuses.Submitted);

            if (hasOpen)
            {
                return ServiceResult<CreateRestockRequestResultDto>.Failure(
                    "Cảnh báo này đã có yêu cầu nhập hàng đang mở.");
            }

            var resolvedPriority = ResolvePriority(priority, alert.AlertType);
            decimal? suggested = null;
            if (alert.ThresholdSnapshot.HasValue)
            {
                suggested = Math.Max(0m, alert.ThresholdSnapshot.Value - alert.CurrentQtySnapshot);
            }

            var now = DateTime.UtcNow;
            var request = new RestockRequest
            {
                StockAlertId = alert.StockAlertId,
                StoreId = alert.StoreId,
                IngredientId = alert.IngredientId,
                RecipeId = alert.RecipeId,
                RequestedQuantity = requestedQuantity,
                SuggestedQuantity = suggested,
                Status = RestockRequestStatuses.Submitted,
                Priority = resolvedPriority,
                CreatedByStaffId = managerStaffId,
                CreatedAt = now,
                UpdatedAt = now,
                Note = noteText
            };

            _context.RestockRequests.Add(request);
            await _context.SaveChangesAsync(); // need Id for notifications

            var recipients = await ResolveAccountantWarehouseAsync(alert.StoreId);
            foreach (var staffId in recipients)
            {
                var itemName = ResolveItemName(alert);
                var itemType = alert.IngredientId.HasValue ? "Nguyên liệu" : "Bán thành phẩm";
                var storeName = alert.Store?.Name ?? $"Cửa hàng #{alert.StoreId}";
                var body =
                    $"Yêu cầu nhập hàng mới từ Quản lý chi nhánh.\n" +
                    $"Mặt hàng: {itemName} ({itemType})\n" +
                    $"Số lượng yêu cầu: {requestedQuantity:N3}\n" +
                    $"Cửa hàng: {storeName}\n" +
                    $"Ưu tiên: {resolvedPriority}" +
                    (string.IsNullOrEmpty(noteText) ? "" : $"\nGhi chú: {noteText}");

                _context.StaffNotifications.Add(new StaffNotification
                {
                    StoreId = alert.StoreId,
                    RecipientStaffId = staffId,
                    Type = StaffNotificationTypes.RestockRequestSubmitted,
                    Title = "Yêu cầu nhập hàng mới",
                    Body = body.Length > 2000 ? body[..2000] : body,
                    EntityType = StaffNotificationEntityTypes.RestockRequest,
                    EntityId = request.RestockRequestId,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    EmailAttempted = false,
                    EmailSent = false
                });
            }

            if (recipients.Count > 0)
                await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[RestockRequest] SUBMITTED Id={Id} AlertId={AlertId} StoreId={StoreId} ByStaffId={StaffId} Recipients={Count}",
                request.RestockRequestId, alert.StockAlertId, alert.StoreId, managerStaffId, recipients.Count);

            var dto = new CreateRestockRequestResultDto
            {
                RestockRequestId = request.RestockRequestId,
                NotifiedAccountantWarehouse = recipients.Count > 0,
                RecipientCount = recipients.Count
            };

            if (recipients.Count == 0)
            {
                return ServiceResult<CreateRestockRequestResultDto>.Success(
                    dto,
                    "Đã gửi yêu cầu nhập hàng. Chưa tìm thấy Kế toán/kho để nhận thông báo.");
            }

            return ServiceResult<CreateRestockRequestResultDto>.Success(
                dto,
                "Đã gửi yêu cầu nhập hàng cho Kế toán/kho.");
        }

        public async Task<ServiceResult<RestockRequestListResultDto>> ListForStoreAsync(
            int storeId,
            string? statusFilter,
            int page,
            int pageSize)
        {
            if (storeId <= 0)
                return ServiceResult<RestockRequestListResultDto>.Failure("StoreId không hợp lệ.");

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var query = _context.RestockRequests
                .AsNoTracking()
                .Include(r => r.Ingredient)
                .Include(r => r.Recipe)
                .Include(r => r.CreatedByStaff)
                .Where(r => r.StoreId == storeId);

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                var st = statusFilter.Trim().ToUpperInvariant();
                query = query.Where(r => r.Status == st);
            }

            var total = await query.CountAsync();
            var rows = await query
                .OrderByDescending(r => r.CreatedAt)
                .ThenByDescending(r => r.RestockRequestId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return ServiceResult<RestockRequestListResultDto>.Success(new RestockRequestListResultDto
            {
                StoreId = storeId,
                StatusFilter = statusFilter,
                Page = page,
                PageSize = pageSize,
                Total = total,
                Items = rows.Select(MapListItem).ToList()
            });
        }

        public async Task<ServiceResult<RestockRequestDetailDto>> GetDetailAsync(
            int requestId,
            int viewerStoreId)
        {
            if (viewerStoreId <= 0)
                return ServiceResult<RestockRequestDetailDto>.Failure("StoreId không hợp lệ.");

            var r = await _context.RestockRequests
                .AsNoTracking()
                .Include(x => x.Ingredient)
                .Include(x => x.Recipe)
                .Include(x => x.CreatedByStaff)
                .Include(x => x.Store)
                .Include(x => x.StockAlert)
                .FirstOrDefaultAsync(x => x.RestockRequestId == requestId);

            if (r == null)
                return ServiceResult<RestockRequestDetailDto>.Failure("Không tìm thấy yêu cầu nhập hàng.");

            if (r.StoreId != viewerStoreId)
                return ServiceResult<RestockRequestDetailDto>.Failure("Yêu cầu không thuộc cửa hàng của bạn.");

            return ServiceResult<RestockRequestDetailDto>.Success(MapDetail(r));
        }

        public async Task<ServiceResult<RestockRequestListItemDto?>> GetOpenByAlertAsync(
            int stockAlertId,
            int storeId)
        {
            if (storeId <= 0 || stockAlertId <= 0)
                return ServiceResult<RestockRequestListItemDto?>.Failure("Tham số không hợp lệ.");

            var r = await _context.RestockRequests
                .AsNoTracking()
                .Include(x => x.Ingredient)
                .Include(x => x.Recipe)
                .Include(x => x.CreatedByStaff)
                .Where(x =>
                    x.StockAlertId == stockAlertId &&
                    x.StoreId == storeId &&
                    x.Status == RestockRequestStatuses.Submitted)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            return ServiceResult<RestockRequestListItemDto?>.Success(
                r == null ? null : MapListItem(r));
        }

        private async Task<List<int>> ResolveAccountantWarehouseAsync(int storeId)
        {
            return await _context.Staffs
                .AsNoTracking()
                .Where(s =>
                    s.StoreId == storeId &&
                    s.Active &&
                    s.Account != null &&
                    s.Account.Active &&
                    s.Account.AccountRoles.Any(ar =>
                        ar.Role != null &&
                        ar.Role.Active &&
                        ar.Role.Name == RoleConstants.AccountantWarehouse))
                .Select(s => s.StaffId)
                .Distinct()
                .ToListAsync();
        }

        private static string ResolvePriority(string? priority, string alertType)
        {
            if (!string.IsNullOrWhiteSpace(priority))
            {
                var p = priority.Trim().ToUpperInvariant();
                if (p is RestockRequestPriorities.Normal
                    or RestockRequestPriorities.High
                    or RestockRequestPriorities.Urgent)
                    return p;
            }

            if (string.Equals(alertType, StockAlertTypes.OutOfStock, StringComparison.OrdinalIgnoreCase))
                return RestockRequestPriorities.Urgent;
            if (string.Equals(alertType, StockAlertTypes.LowStock, StringComparison.OrdinalIgnoreCase))
                return RestockRequestPriorities.High;
            return RestockRequestPriorities.Normal;
        }

        private static RestockRequestListItemDto MapListItem(RestockRequest r) => new()
        {
            RestockRequestId = r.RestockRequestId,
            StockAlertId = r.StockAlertId,
            StoreId = r.StoreId,
            ItemName = ResolveItemName(r),
            ItemTypeLabel = r.IngredientId.HasValue ? "Nguyên liệu" : "Bán thành phẩm",
            RequestedQuantity = r.RequestedQuantity,
            SuggestedQuantity = r.SuggestedQuantity,
            Status = r.Status,
            Priority = r.Priority,
            Note = r.Note,
            CreatedByName = r.CreatedByStaff?.FullName,
            CreatedAt = r.CreatedAt
        };

        private static RestockRequestDetailDto MapDetail(RestockRequest r)
        {
            var dto = new RestockRequestDetailDto
            {
                RestockRequestId = r.RestockRequestId,
                StockAlertId = r.StockAlertId,
                StoreId = r.StoreId,
                ItemName = ResolveItemName(r),
                ItemTypeLabel = r.IngredientId.HasValue ? "Nguyên liệu" : "Bán thành phẩm",
                RequestedQuantity = r.RequestedQuantity,
                SuggestedQuantity = r.SuggestedQuantity,
                Status = r.Status,
                Priority = r.Priority,
                Note = r.Note,
                CreatedByName = r.CreatedByStaff?.FullName,
                CreatedAt = r.CreatedAt,
                IngredientId = r.IngredientId,
                RecipeId = r.RecipeId,
                CreatedByStaffId = r.CreatedByStaffId,
                UpdatedAt = r.UpdatedAt,
                StoreName = r.Store?.Name,
                AlertType = r.StockAlert?.AlertType,
                AlertStatus = r.StockAlert?.Status,
                AlertCurrentQtySnapshot = r.StockAlert?.CurrentQtySnapshot,
                AlertThresholdSnapshot = r.StockAlert?.ThresholdSnapshot
            };
            return dto;
        }

        private static string ResolveItemName(RestockRequest r)
        {
            if (r.IngredientId.HasValue)
                return r.Ingredient?.Name ?? $"Nguyên liệu #{r.IngredientId}";
            if (r.RecipeId.HasValue)
            {
                if (!string.IsNullOrWhiteSpace(r.Recipe?.Name)) return r.Recipe.Name;
                if (!string.IsNullOrWhiteSpace(r.Recipe?.RecipeCode)) return r.Recipe.RecipeCode;
                return $"Bán thành phẩm #{r.RecipeId}";
            }
            return "Mặt hàng không xác định";
        }

        private static string ResolveItemName(StockAlert a)
        {
            if (a.IngredientId.HasValue)
                return a.Ingredient?.Name ?? $"Nguyên liệu #{a.IngredientId}";
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
