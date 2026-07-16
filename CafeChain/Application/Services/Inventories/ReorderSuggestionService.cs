using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Suppliers;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Inventories
{
    public sealed class ReorderSuggestionService : IReorderSuggestionService
    {
        private readonly AppDbContext _context;
        private readonly IPhysicalUnitConversionService _conversion;
        private readonly IReorderIncomingQuantityProvider _incomingProvider;
        private readonly IScopeAuthorizationService _scopeAuthorization;

        public ReorderSuggestionService(
            AppDbContext context,
            IPhysicalUnitConversionService conversion,
            IReorderIncomingQuantityProvider incomingProvider,
            IScopeAuthorizationService scopeAuthorization)
        {
            _context = context;
            _conversion = conversion;
            _incomingProvider = incomingProvider;
            _scopeAuthorization = scopeAuthorization;
        }

        public async Task<ServiceResult<ReorderSuggestionListDto>> GetForStoreAsync(
            int storeId,
            int actorStaffId,
            IReadOnlyCollection<string> actorRoles,
            int analysisWindowDays = 30)
        {
            if (storeId <= 0 || actorStaffId <= 0)
                return ServiceResult<ReorderSuggestionListDto>.Failure("Thiếu thông tin cửa hàng hoặc người thao tác.");
            if (analysisWindowDays is < 1 or > 365)
                return ServiceResult<ReorderSuggestionListDto>.Failure("Khoảng phân tích phải từ 1 đến 365 ngày.");
            if (!await CanViewStoreAsync(storeId, actorStaffId, actorRoles))
                return ServiceResult<ReorderSuggestionListDto>.Failure("Bạn không có quyền xem gợi ý nhập hàng của cửa hàng này.");

            var store = await _context.Stores.AsNoTracking()
                .Where(x => x.StoreId == storeId)
                .Select(x => new { x.StoreId, x.Name })
                .FirstOrDefaultAsync();
            if (store == null)
                return ServiceResult<ReorderSuggestionListDto>.Failure("Không tìm thấy cửa hàng.");

            var inventories = await _context.StoreInventories
                .AsNoTracking()
                .Include(x => x.Ingredient)
                    .ThenInclude(x => x.BaseUnit)
                .Where(x => x.StoreId == storeId && x.IngredientId.HasValue && x.Ingredient.Active)
                .OrderBy(x => x.Ingredient.Name)
                .ThenBy(x => x.IngredientId)
                .ToListAsync();

            var ingredientIds = inventories
                .Select(x => x.IngredientId!.Value)
                .Distinct()
                .ToArray();
            var fromUtc = DateTime.UtcNow.AddDays(-analysisWindowDays);
            var usageRows = await _context.InventoryTransactions
                .AsNoTracking()
                .Where(x => x.CreatedAt >= fromUtc
                    && x.StoreInventory.StoreId == storeId
                    && x.StoreInventory.IngredientId.HasValue
                    && (x.Type == InventoryTransactionTypeEnum.SALES_DEDUCTION
                        || x.Type == InventoryTransactionTypeEnum.PRODUCTION_OUT))
                .Select(x => new { IngredientId = x.StoreInventory.IngredientId!.Value, x.Quantity })
                .ToListAsync();
            var usage = usageRows
                .GroupBy(x => x.IngredientId)
                .ToDictionary(
                    g => g.Key,
                    g => new { Quantity = g.Sum(x => x.Quantity), Count = g.Count() });

            var offers = await _context.IngredientSuppliers
                .AsNoTracking()
                .Include(x => x.Supplier)
                    .ThenInclude(x => x.SupplierStores)
                .Where(x => ingredientIds.Contains(x.IngredientId)
                    && x.Active
                    && x.Supplier.Active
                    && x.Supplier.SupplierStores.Any(ss => ss.StoreId == storeId && ss.Active))
                .ToListAsync();

            var incoming = await _incomingProvider.GetIncomingBaseQuantitiesAsync(storeId, ingredientIds);
            var activeRequests = await _context.RestockRequests
                .AsNoTracking()
                .Where(x => x.StoreId == storeId
                    && x.IngredientId.HasValue
                    && RestockRequestStatuses.ActiveValues.Contains(x.Status))
                .GroupBy(x => x.IngredientId!.Value)
                .Select(g => new { IngredientId = g.Key, RequestId = g.Min(x => x.RestockRequestId) })
                .ToDictionaryAsync(x => x.IngredientId, x => x.RequestId);

            var result = new ReorderSuggestionListDto
            {
                StoreId = store.StoreId,
                StoreName = store.Name ?? $"Cửa hàng #{store.StoreId}",
                AnalysisWindowDays = analysisWindowDays,
                CalculatedAtUtc = DateTime.UtcNow
            };

            foreach (var inventory in inventories)
            {
                var ingredientId = inventory.IngredientId!.Value;
                var item = new ReorderSuggestionItemDto
                {
                    StoreId = storeId,
                    StoreName = result.StoreName,
                    IngredientId = ingredientId,
                    IngredientCode = inventory.Ingredient.Code ?? string.Empty,
                    IngredientName = inventory.Ingredient.Name ?? $"Nguyên liệu #{ingredientId}",
                    BaseUnitCode = inventory.Ingredient.BaseUnit?.UnitCode ?? string.Empty,
                    AvailableQuantity = inventory.AvailableQty,
                    MinLevel = inventory.MinStockLevel,
                    IncomingApprovedPoQuantity = incoming.GetValueOrDefault(ingredientId),
                    ActiveRestockRequestId = activeRequests.GetValueOrDefault(ingredientId) is var requestId && requestId > 0
                        ? requestId
                        : null
                };

                if (!inventory.MinStockLevel.HasValue)
                {
                    SetStatus(item, ReorderSuggestionStatuses.MissingThreshold, "Chưa cấu hình ngưỡng tồn kho tối thiểu.");
                    result.Items.Add(item);
                    continue;
                }

                if (!usage.TryGetValue(ingredientId, out var usageRow) || usageRow.Count == 0)
                {
                    SetStatus(item, ReorderSuggestionStatuses.InsufficientHistory, "Chưa có dữ liệu xuất kho tiêu thụ hợp lệ trong kỳ phân tích.");
                    result.Items.Add(item);
                    continue;
                }

                item.AverageDailyUsage = usageRow.Quantity / analysisWindowDays;
                var selected = await SelectOfferAsync(
                    offers.Where(x => x.IngredientId == ingredientId).ToList(),
                    storeId,
                    inventory.Ingredient.BaseUnitId);
                if (!selected.IsSuccess)
                {
                    SetStatus(item, selected.Status, selected.Reason);
                    result.Items.Add(item);
                    continue;
                }

                var offer = selected.Offer!;
                item.IngredientSupplierId = offer.IngredientSupplierId;
                item.SupplierId = offer.SupplierId;
                item.SupplierCode = offer.Supplier.Code;
                item.SupplierName = offer.Supplier.Name;
                item.PackagePrice = offer.CurrentPrice;
                item.PackageBaseQuantity = selected.PackageBaseQuantity;
                item.MinimumOrderPackageCount = offer.MinimumOrderPackageCount;
                item.LeadTimeDays = selected.LeadTimeDays;

                var reorderPoint = item.AverageDailyUsage.Value * selected.LeadTimeDays!.Value
                    + inventory.MinStockLevel.Value;
                var shortageBeforeIncoming = reorderPoint - inventory.AvailableQty;
                var suggested = Math.Max(0m, shortageBeforeIncoming - item.IncomingApprovedPoQuantity);
                item.ReorderPoint = reorderPoint;
                item.SuggestedBaseQuantity = suggested;

                if (suggested <= 0)
                {
                    if (shortageBeforeIncoming > 0 && item.IncomingApprovedPoQuantity > 0)
                        SetStatus(item, ReorderSuggestionStatuses.IncomingCoversDemand, "Lượng hàng đang về đã bao phủ nhu cầu dự kiến.");
                    else
                        SetStatus(item, ReorderSuggestionStatuses.NoReorderNeeded, "Tồn khả dụng đang đáp ứng điểm đặt hàng.");
                    item.SuggestedPackageCount = 0;
                    item.EstimatedAmount = 0;
                    result.Items.Add(item);
                    continue;
                }

                var packageCount = (int)Math.Ceiling(suggested / selected.PackageBaseQuantity!.Value);
                if (offer.MinimumOrderPackageCount.HasValue)
                    packageCount = Math.Max(packageCount, offer.MinimumOrderPackageCount.Value);
                item.SuggestedPackageCount = packageCount;
                item.EstimatedAmount = packageCount * offer.CurrentPrice;
                SetStatus(item, ReorderSuggestionStatuses.Ready, "Đủ dữ liệu để tạo yêu cầu nhập nháp.");
                result.Items.Add(item);
            }

            return ServiceResult<ReorderSuggestionListDto>.Success(result);
        }

        private async Task<OfferSelection> SelectOfferAsync(
            IReadOnlyCollection<IngredientSupplier> offers,
            int storeId,
            int baseUnitId)
        {
            if (offers.Count == 0)
                return OfferSelection.Fail(ReorderSuggestionStatuses.NoActiveSupplier, "Không có nguồn cung đang hoạt động cho cửa hàng.");

            var converted = new List<OfferSelection>();
            foreach (var offer in offers)
            {
                if (!offer.PackageQuantity.HasValue || offer.PackageQuantity <= 0)
                {
                    converted.Add(OfferSelection.Fail(ReorderSuggestionStatuses.InvalidConversion, "Nguồn cung chưa có quy cách gói hợp lệ.", offer));
                    continue;
                }

                var conversion = await _conversion.ConvertAsync(offer.PackageQuantity.Value, offer.UnitId, baseUnitId);
                if (!conversion.IsSuccess || conversion.Data <= 0)
                {
                    converted.Add(OfferSelection.Fail(ReorderSuggestionStatuses.InvalidConversion, "Không quy đổi được gói mua sang đơn vị tồn kho cơ sở.", offer));
                    continue;
                }

                if (offer.CurrentPrice <= 0)
                {
                    converted.Add(OfferSelection.Fail(ReorderSuggestionStatuses.MissingCost, "Nguồn cung chưa có giá gói mua hợp lệ.", offer));
                    continue;
                }

                var leadTime = offer.Supplier.SupplierStores
                    .Where(x => x.StoreId == storeId && x.Active)
                    .Select(x => x.LeadTimeOverrideDays)
                    .FirstOrDefault() ?? offer.LeadTimeDays;
                if (!leadTime.HasValue)
                {
                    converted.Add(OfferSelection.Fail(ReorderSuggestionStatuses.MissingLeadTime, "Nguồn cung chưa có thời gian giao hàng.", offer, conversion.Data));
                    continue;
                }

                converted.Add(OfferSelection.Success(offer, conversion.Data, leadTime.Value));
            }

            var primary = converted
                .Where(x => x.Offer?.IsPrimary == true)
                .OrderBy(x => x.Offer!.IngredientSupplierId)
                .FirstOrDefault();
            if (primary != null)
                return primary;

            var fallback = converted
                .Where(x => x.IsSuccess)
                .OrderBy(x => x.Offer!.CurrentPrice / x.PackageBaseQuantity!.Value)
                .ThenBy(x => x.LeadTimeDays)
                .ThenBy(x => x.Offer!.IngredientSupplierId)
                .FirstOrDefault();
            return fallback ?? converted.OrderBy(x => x.Offer?.IngredientSupplierId).First();
        }

        private async Task<bool> CanViewStoreAsync(
            int storeId,
            int actorStaffId,
            IReadOnlyCollection<string> actorRoles)
        {
            var roles = actorRoles.ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (roles.Contains(RoleConstants.BusinessOwner) || roles.Contains(RoleConstants.AccountantWarehouse))
                return true;
            if (roles.Contains(RoleConstants.AreaManager))
                return await _scopeAuthorization.CanAccessStoreAsync(actorStaffId, storeId);
            if (!roles.Contains(RoleConstants.StoreManager))
                return false;
            return await _context.Staffs.AsNoTracking()
                .AnyAsync(x => x.StaffId == actorStaffId && x.Active && x.StoreId == storeId);
        }

        private static void SetStatus(ReorderSuggestionItemDto item, string status, string reason)
        {
            item.Status = status;
            item.Reason = reason;
        }

        private sealed class OfferSelection
        {
            public bool IsSuccess { get; private init; }
            public string Status { get; private init; } = ReorderSuggestionStatuses.Unknown;
            public string Reason { get; private init; } = string.Empty;
            public IngredientSupplier? Offer { get; private init; }
            public decimal? PackageBaseQuantity { get; private init; }
            public int? LeadTimeDays { get; private init; }

            public static OfferSelection Success(IngredientSupplier offer, decimal packageBaseQuantity, int leadTimeDays) => new()
            {
                IsSuccess = true,
                Offer = offer,
                PackageBaseQuantity = packageBaseQuantity,
                LeadTimeDays = leadTimeDays
            };

            public static OfferSelection Fail(
                string status,
                string reason,
                IngredientSupplier? offer = null,
                decimal? packageBaseQuantity = null) => new()
            {
                Status = status,
                Reason = reason,
                Offer = offer,
                PackageBaseQuantity = packageBaseQuantity
            };
        }
    }
}
