using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.InventoryThresholds;
using CafeChain.Application.Interfaces.Admin.StoreInventories;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CafeChain.Application.Services.Admin.StoreInventories
{
    /// <summary>
    /// Issue #104 — Admin configure MinStockLevel only (no qty mutation).
    /// Store scope mirrors AdminStoreInventoryRepository (StaffScopes + Staff.StoreId).
    /// Edit roles: StoreManager, AreaManager, BusinessOwner, SystemAdmin only.
    /// </summary>
    public class InventoryThresholdService : IInventoryThresholdService
    {
        private const int MaxPageSize = 100;

        private readonly AppDbContext _context;
        private readonly ILogger<InventoryThresholdService> _logger;
        private readonly IScopeAuthorizationService _scopeAuthorization;
        private readonly IAdminPermissionService _permissions;

        public InventoryThresholdService(
            AppDbContext context,
            ILogger<InventoryThresholdService> logger,
            IScopeAuthorizationService scopeAuthorization,
            IAdminPermissionService permissions)
        {
            _context = context;
            _logger = logger;
            _scopeAuthorization = scopeAuthorization;
            _permissions = permissions;
        }

        public async Task<ServiceResult<InventoryThresholdListResultDto>> ListAsync(
            int accountId,
            int storeId,
            string? search,
            int page,
            int pageSize)
        {
            if (accountId <= 0)
                return ServiceResult<InventoryThresholdListResultDto>.Failure("Không xác định được tài khoản.");

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > MaxPageSize) pageSize = MaxPageSize;

            var stores = await GetAccessibleStoresAsync(accountId);
            if (stores.Count == 0)
            {
                return ServiceResult<InventoryThresholdListResultDto>.Success(new InventoryThresholdListResultDto
                {
                    Page = page,
                    PageSize = pageSize
                });
            }

            var selectedStoreId = storeId > 0 && stores.Any(s => s.StoreId == storeId)
                ? storeId
                : stores[0].StoreId;

            var allowedIds = stores.Select(s => s.StoreId).ToList();
            var query = _context.StoreInventories
                .AsNoTracking()
                .Include(i => i.Ingredient)!.ThenInclude(ing => ing!.BaseUnit)
                .Include(i => i.Recipe)
                .Include(i => i.Store)
                .Where(i => allowedIds.Contains(i.StoreId) && i.StoreId == selectedStoreId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var kw = search.Trim();
                query = query.Where(i =>
                    (i.IngredientId.HasValue && i.Ingredient != null && i.Ingredient.Name.Contains(kw)) ||
                    (i.RecipeId.HasValue && i.Recipe != null &&
                     ((i.Recipe.Name != null && i.Recipe.Name.Contains(kw)) ||
                      (i.Recipe.RecipeCode != null && i.Recipe.RecipeCode.Contains(kw)))));
            }

            var total = await query.CountAsync();
            var rows = await query
                .OrderBy(i => i.IngredientId.HasValue ? 0 : 1)
                .ThenBy(i => i.Ingredient != null ? i.Ingredient.Name : i.Recipe!.Name)
                .ThenBy(i => i.StoreInventoryId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return ServiceResult<InventoryThresholdListResultDto>.Success(new InventoryThresholdListResultDto
            {
                SelectedStoreId = selectedStoreId,
                Search = search,
                Page = page,
                PageSize = pageSize,
                Total = total,
                Stores = stores,
                Items = rows.Select(MapItem).ToList()
            });
        }

        public async Task<ServiceResult> UpdateMinStockLevelAsync(
            int accountId,
            int storeInventoryId,
            decimal? minStockLevel,
            string? rowVersion)
        {
            if (accountId <= 0)
                return ServiceResult.Failure("Không xác định được tài khoản.");

            if (storeInventoryId <= 0)
                return ServiceResult.Failure("Mã tồn kho không hợp lệ.");

            if (minStockLevel.HasValue && minStockLevel.Value < 0)
            {
                return ServiceResult.Failure("Ngưỡng tồn tối thiểu không được âm.");
            }

            if (string.IsNullOrWhiteSpace(rowVersion))
            {
                return ServiceResult.Failure(
                    "Thiếu phiên bản dữ liệu. Vui lòng tải lại trang.",
                    errorCode: "VALIDATION_ROW_VERSION_REQUIRED");
            }

            byte[] expectedVersion;
            try
            {
                expectedVersion = Convert.FromBase64String(rowVersion);
            }
            catch (FormatException)
            {
                return ServiceResult.Failure(
                    "Phiên bản dữ liệu không hợp lệ. Vui lòng tải lại trang.",
                    errorCode: "VALIDATION_ROW_VERSION_REQUIRED");
            }

            var targetStoreId = await _context.StoreInventories
                .AsNoTracking()
                .Where(i => i.StoreInventoryId == storeInventoryId)
                .Select(i => (int?)i.StoreId)
                .SingleOrDefaultAsync();
            if (!targetStoreId.HasValue)
                return ServiceResult.Failure("Không tìm thấy dòng tồn kho.");

            var permission = await _permissions.HasPermissionAsync(
                accountId,
                PermissionConstants.InventoryThresholdUpdate,
                targetStoreId.Value);
            if (!permission.IsSuccess || permission.Data?.Allowed != true)
                return ServiceResult.Failure("Bạn không có quyền cập nhật ngưỡng tồn kho.");

            var row = await _context.StoreInventories
                .FirstOrDefaultAsync(i =>
                    i.StoreInventoryId == storeInventoryId
                    && i.StoreId == targetStoreId.Value);

            if (row == null)
                return ServiceResult.Failure("Không tìm thấy dòng tồn kho.");

            if (!row.RowVersion.SequenceEqual(expectedVersion))
            {
                return ServiceResult.Failure(
                    "Dữ liệu đã được người khác cập nhật. Vui lòng tải lại trang.",
                    errorCode: "RESOURCE_CHANGED_BY_ANOTHER_USER");
            }

            var allowed = await GetAccessibleStoreIdsAsync(accountId);
            if (!allowed.Contains(row.StoreId))
            {
                return ServiceResult.Failure("Bạn không có quyền cập nhật ngưỡng tồn kho.");
            }

            // Clear when null; otherwise set non-negative value. Never touch quantities.
            var beforeQty = row.AvailableQty;
            var beforeReserved = row.ReservedQty;

            row.MinStockLevel = minStockLevel;
            row.LastUpdated = DateTime.UtcNow;

            _context.Entry(row).Property(x => x.RowVersion).OriginalValue = expectedVersion;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return ServiceResult.Failure(
                    "Dữ liệu đã được người khác cập nhật. Vui lòng tải lại trang.",
                    errorCode: "RESOURCE_CHANGED_BY_ANOTHER_USER");
            }

            // Defensive: quantities must remain unchanged after save.
            if (row.AvailableQty != beforeQty || row.ReservedQty != beforeReserved)
            {
                _logger.LogError(
                    "[InventoryThreshold] Unexpected quantity change StoreInventoryId={Id}",
                    storeInventoryId);
                return ServiceResult.Failure("Lỗi hệ thống: tồn kho bị thay đổi ngoài ý muốn.");
            }

            _logger.LogInformation(
                "[InventoryThreshold] Updated MinStockLevel={Min} StoreInventoryId={Id} StoreId={StoreId} AccountId={AccountId}",
                minStockLevel, storeInventoryId, row.StoreId, accountId);

            return ServiceResult.Success("Cập nhật ngưỡng tồn kho thành công.");
        }

        private async Task<List<InventoryStoreTabDto>> GetAccessibleStoresAsync(int accountId)
        {
            var staffId = await _context.Staffs
                .AsNoTracking()
                .Where(x => x.AccountId == accountId && x.Active && x.Account.Active)
                .Select(x => x.StaffId)
                .SingleOrDefaultAsync();

            if (staffId <= 0)
                return new List<InventoryStoreTabDto>();

            var stores = await _scopeAuthorization.GetAllowedStoresAsync(staffId);
            return stores
                .Where(x => x.Active)
                .OrderBy(x => x.Name)
                .Select(x => new InventoryStoreTabDto
                {
                    StoreId = x.StoreId,
                    StoreName = x.Name
                })
                .ToList();
        }

        private async Task<HashSet<int>> GetAccessibleStoreIdsAsync(int accountId)
        {
            var stores = await GetAccessibleStoresAsync(accountId);
            return stores.Select(s => s.StoreId).ToHashSet();
        }

        private static InventoryThresholdItemDto MapItem(StoreInventory i)
        {
            string name;
            string typeLabel;
            string? unit = null;

            if (i.IngredientId.HasValue)
            {
                name = i.Ingredient?.Name ?? $"Nguyên liệu #{i.IngredientId}";
                typeLabel = "Nguyên liệu";
                unit = i.Ingredient?.BaseUnit?.UnitCode;
            }
            else if (i.RecipeId.HasValue)
            {
                if (!string.IsNullOrWhiteSpace(i.Recipe?.Name))
                    name = i.Recipe.Name;
                else if (!string.IsNullOrWhiteSpace(i.Recipe?.RecipeCode))
                    name = i.Recipe.RecipeCode;
                else
                    name = $"Bán thành phẩm #{i.RecipeId}";
                typeLabel = "Bán thành phẩm";
            }
            else
            {
                name = "Mặt hàng không xác định";
                typeLabel = "—";
            }

            return new InventoryThresholdItemDto
            {
                StoreInventoryId = i.StoreInventoryId,
                StoreId = i.StoreId,
                StoreName = i.Store?.Name,
                ItemName = name,
                ItemTypeLabel = typeLabel,
                UnitCode = unit,
                AvailableQty = i.AvailableQty,
                ReservedQty = i.ReservedQty,
                MinStockLevel = i.MinStockLevel,
                RowVersion = Convert.ToBase64String(i.RowVersion ?? Array.Empty<byte>()),
                LastUpdated = i.LastUpdated
            };
        }
    }
}
