using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.InventoryThresholds;
using CafeChain.Application.Interfaces.Admin.StoreInventories;
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

        /// <summary>Roles allowed to change MinStockLevel (not AccountantWarehouse / sales / supervisor).</summary>
        public static readonly string[] EditRoleNames =
        {
            RoleConstants.StoreManager,
            RoleConstants.AreaManager,
            RoleConstants.BusinessOwner,
            RoleConstants.SystemAdmin
        };

        private readonly AppDbContext _context;
        private readonly ILogger<InventoryThresholdService> _logger;

        public InventoryThresholdService(
            AppDbContext context,
            ILogger<InventoryThresholdService> logger)
        {
            _context = context;
            _logger = logger;
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
            decimal? minStockLevel)
        {
            if (accountId <= 0)
                return ServiceResult.Failure("Không xác định được tài khoản.");

            if (!await AccountHasEditRoleAsync(accountId))
            {
                return ServiceResult.Failure("Bạn không có quyền cập nhật ngưỡng tồn kho.");
            }

            if (storeInventoryId <= 0)
                return ServiceResult.Failure("Mã tồn kho không hợp lệ.");

            if (minStockLevel.HasValue && minStockLevel.Value < 0)
            {
                return ServiceResult.Failure("Ngưỡng tồn tối thiểu không được âm.");
            }

            var row = await _context.StoreInventories
                .FirstOrDefaultAsync(i => i.StoreInventoryId == storeInventoryId);

            if (row == null)
                return ServiceResult.Failure("Không tìm thấy dòng tồn kho.");

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

            await _context.SaveChangesAsync();

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

        /// <summary>True if account has an active edit role for MinStockLevel.</summary>
        public async Task<bool> AccountHasEditRoleAsync(int accountId)
        {
            if (accountId <= 0)
                return false;

            return await _context.Accounts
                .AsNoTracking()
                .Where(a => a.AccountId == accountId && a.Active)
                .SelectMany(a => a.AccountRoles)
                .AnyAsync(ar =>
                    ar.Role != null &&
                    ar.Role.Active &&
                    EditRoleNames.Contains(ar.Role.Name));
        }

        private async Task<List<InventoryStoreTabDto>> GetAccessibleStoresAsync(int accountId)
        {
            var staff = await _context.Staffs
                .AsNoTracking()
                .Include(x => x.StaffScopes)
                    .ThenInclude(x => x.ScopeType)
                .FirstOrDefaultAsync(x => x.AccountId == accountId && x.Active);

            if (staff == null)
                return new List<InventoryStoreTabDto>();

            var query = BuildStoreScopeQuery(staff);
            return await query
                .OrderBy(x => x.Name)
                .Select(x => new InventoryStoreTabDto
                {
                    StoreId = x.StoreId,
                    StoreName = x.Name
                })
                .ToListAsync();
        }

        private async Task<HashSet<int>> GetAccessibleStoreIdsAsync(int accountId)
        {
            var stores = await GetAccessibleStoresAsync(accountId);
            return stores.Select(s => s.StoreId).ToHashSet();
        }

        private IQueryable<Store> BuildStoreScopeQuery(Staff staff)
        {
            var activeStores = _context.Stores
                .AsNoTracking()
                .Where(x => x.Active);

            var scopes = staff.StaffScopes?
                .Where(x => x.ScopeRefId > 0)
                .ToList()
                ?? new List<StaffScope>();

            if (HasScope(scopes, "COUNTRY", 1))
                return activeStores;

            var storeScopeIds = GetScopeRefIds(scopes, "STORE", 4);
            var provinceScopeIds = GetScopeRefIds(scopes, "PROVINCE", 2);
            var wardScopeIds = GetScopeRefIds(scopes, "WARD", 3);

            if (staff.StoreId > 0)
                storeScopeIds.Add(staff.StoreId);

            storeScopeIds = storeScopeIds.Distinct().ToList();
            provinceScopeIds = provinceScopeIds.Distinct().ToList();
            wardScopeIds = wardScopeIds.Distinct().ToList();

            if (!storeScopeIds.Any() && !provinceScopeIds.Any() && !wardScopeIds.Any())
                return activeStores.Where(x => false);

            return activeStores.Where(x =>
                storeScopeIds.Contains(x.StoreId) ||
                (x.ProvinceId.HasValue && provinceScopeIds.Contains(x.ProvinceId.Value)) ||
                (x.WardId.HasValue && wardScopeIds.Contains(x.WardId.Value)));
        }

        private static List<int> GetScopeRefIds(
            IEnumerable<StaffScope> scopes,
            string code,
            int scopeTypeId)
        {
            return scopes
                .Where(x => IsScope(x, code, scopeTypeId))
                .Select(x => x.ScopeRefId)
                .Distinct()
                .ToList();
        }

        private static bool HasScope(
            IEnumerable<StaffScope> scopes,
            string code,
            int scopeTypeId) =>
            scopes.Any(x => IsScope(x, code, scopeTypeId));

        private static bool IsScope(StaffScope scope, string code, int scopeTypeId) =>
            scope.ScopeTypeId == scopeTypeId ||
            string.Equals(scope.ScopeType?.Code, code, StringComparison.OrdinalIgnoreCase);

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
                LastUpdated = i.LastUpdated
            };
        }
    }
}
