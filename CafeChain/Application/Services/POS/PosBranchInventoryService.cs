using CafeChain.Application.DTOs.POS;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Results;
using CafeChain.Data;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.POS
{
    /// <summary>
    /// Issue #96 — read-only StoreInventory listing for current store (ADR-0004 one-level identity).
    /// </summary>
    public class PosBranchInventoryService : IPosBranchInventoryService
    {
        public const string ThresholdStatusUnconfigured = "Chưa cấu hình ngưỡng tối thiểu";
        public const string ThresholdStatusOut = "Hết hàng";
        public const string ThresholdStatusLow = "Gần hết";
        public const string ThresholdStatusNormal = "Bình thường";
        public const string QuantityStatusNegative = "Tồn âm";
        public const string QuantityStatusOut = "Hết hàng";
        public const string QuantityStatusInStock = "Còn hàng";
        public const string ItemTypeIngredient = "Ingredient";
        public const string ItemTypeRecipe = "Recipe";
        public const string ItemTypePreparedItem = "PreparedItem";
        public const string StockFilterOut = "OUT";
        public const string StockFilterLow = "LOW";
        public const string StockFilterNormal = "NORMAL";
        public const string StockFilterUnconfigured = "UNCONFIGURED";

        private readonly AppDbContext _context;

        public PosBranchInventoryService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ServiceResult<POSBranchInventoryListDto>> GetBranchInventoryAsync(
            int storeId,
            string? search,
            string? itemType,
            int page,
            int pageSize,
            string? stockStatus = null)
        {
            if (storeId <= 0)
                return ServiceResult<POSBranchInventoryListDto>.Failure("StoreId không hợp lệ.");

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 100) pageSize = 100;

            var normalizedType = NormalizeItemType(itemType);
            if (itemType != null && normalizedType == null && !string.IsNullOrWhiteSpace(itemType))
            {
                return ServiceResult<POSBranchInventoryListDto>.Failure(
                    "itemType phải là Ingredient, Recipe, PreparedItem, hoặc để trống.");
            }

            var query = _context.StoreInventories
                .AsNoTracking()
                .Where(i => i.StoreId == storeId);

            var normalizedStockStatus = NormalizeStockStatus(stockStatus);
            if (!string.IsNullOrWhiteSpace(stockStatus) && normalizedStockStatus == null)
            {
                return ServiceResult<POSBranchInventoryListDto>.Failure(
                    "stockStatus phải là OUT, LOW, NORMAL, UNCONFIGURED, hoặc để trống.");
            }

            if (normalizedType == ItemTypeIngredient)
            {
                query = query.Where(i => i.IngredientId != null);
            }
            else if (normalizedType == ItemTypeRecipe)
            {
                query = query.Where(i => i.IngredientId == null && (i.RecipeId != null || i.PreparedItemId != null));
            }
            else if (normalizedType == ItemTypePreparedItem)
            {
                query = query.Where(i => i.IngredientId == null && i.PreparedItemId != null);
            }

            if (normalizedStockStatus == StockFilterOut)
            {
                query = query.Where(i => i.AvailableQty - i.ReservedQty <= 0);
            }
            else if (normalizedStockStatus == StockFilterLow)
            {
                query = query.Where(i =>
                    i.MinStockLevel.HasValue &&
                    i.AvailableQty - i.ReservedQty > 0 &&
                    i.AvailableQty - i.ReservedQty <= i.MinStockLevel.Value);
            }
            else if (normalizedStockStatus == StockFilterNormal)
            {
                query = query.Where(i =>
                    i.MinStockLevel.HasValue &&
                    i.AvailableQty - i.ReservedQty > i.MinStockLevel.Value);
            }
            else if (normalizedStockStatus == StockFilterUnconfigured)
            {
                query = query.Where(i => !i.MinStockLevel.HasValue);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim();
                query = query.Where(i =>
                    (i.IngredientId != null &&
                     (i.Ingredient.Name.Contains(keyword) ||
                      (i.Ingredient.Code != null && i.Ingredient.Code.Contains(keyword)))) ||
                    (i.RecipeId != null &&
                     ((i.Recipe.Name != null && i.Recipe.Name.Contains(keyword)) ||
                      (i.Recipe.RecipeCode != null && i.Recipe.RecipeCode.Contains(keyword)))) ||
                    (i.PreparedItemId != null &&
                     (i.PreparedItem.Name.Contains(keyword) ||
                      i.PreparedItem.Code.Contains(keyword))));
            }

            var total = await query.CountAsync();

            var rows = await query
                .OrderBy(i => i.IngredientId != null
                    ? i.Ingredient.Name
                    : (i.PreparedItem != null ? i.PreparedItem.Name : (i.Recipe != null ? i.Recipe.Name : "")))
                .ThenBy(i => i.StoreInventoryId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(i => new
                {
                    i.StoreInventoryId,
                    i.StoreId,
                    i.IngredientId,
                    i.RecipeId,
                    i.PreparedItemId,
                    IngredientName = i.Ingredient != null ? i.Ingredient.Name : null,
                    IngredientCode = i.Ingredient != null ? i.Ingredient.Code : null,
                    UnitCode = i.Ingredient != null && i.Ingredient.BaseUnit != null
                        ? i.Ingredient.BaseUnit.UnitCode
                        : null,
                    UnitName = i.Ingredient != null && i.Ingredient.BaseUnit != null
                        ? i.Ingredient.BaseUnit.Name
                        : null,
                    RecipeName = i.Recipe != null ? i.Recipe.Name : null,
                    RecipeCode = i.Recipe != null ? i.Recipe.RecipeCode : null,
                    PreparedItemName = i.PreparedItem != null ? i.PreparedItem.Name : null,
                    PreparedItemCode = i.PreparedItem != null ? i.PreparedItem.Code : null,
                    PreparedItemUnitCode = i.PreparedItem != null && i.PreparedItem.BaseUnit != null
                        ? i.PreparedItem.BaseUnit.UnitCode
                        : null,
                    PreparedItemUnitName = i.PreparedItem != null && i.PreparedItem.BaseUnit != null
                        ? i.PreparedItem.BaseUnit.Name
                        : null,
                    i.AvailableQty,
                    i.ReservedQty,
                    i.MinStockLevel,
                    i.LastUpdated
                })
                .ToListAsync();

            var items = rows.Select(r =>
            {
                var isIngredient = r.IngredientId.HasValue;
                var itemId = isIngredient ? r.IngredientId!.Value : (r.PreparedItemId ?? r.RecipeId ?? 0);
                string itemName;
                string? itemCode;
                string unitName;

                if (isIngredient)
                {
                    itemName = r.IngredientName ?? $"Nguyên liệu #{itemId}";
                    itemCode = r.IngredientCode;
                    unitName = !string.IsNullOrWhiteSpace(r.UnitCode)
                        ? r.UnitCode!
                        : (!string.IsNullOrWhiteSpace(r.UnitName) ? r.UnitName! : "—");
                }
                else
                {
                    itemName = !string.IsNullOrWhiteSpace(r.PreparedItemName)
                        ? r.PreparedItemName!
                        : !string.IsNullOrWhiteSpace(r.RecipeName)
                        ? r.RecipeName!
                        : (!string.IsNullOrWhiteSpace(r.RecipeCode)
                            ? r.RecipeCode!
                            : $"Bán thành phẩm #{itemId}");
                    itemCode = r.PreparedItemCode ?? r.RecipeCode;
                    // RecipeId-backed quantities may still represent legacy batches.
                    // Do not label them as authoritative PreparedItem base-unit quantities.
                    unitName = !r.RecipeId.HasValue && !string.IsNullOrWhiteSpace(r.PreparedItemUnitCode)
                        ? r.PreparedItemUnitCode!
                        : (!r.RecipeId.HasValue && !string.IsNullOrWhiteSpace(r.PreparedItemUnitName)
                            ? r.PreparedItemUnitName!
                            : "—");
                }

                return new POSBranchInventoryItemDto
                {
                    StoreInventoryId = r.StoreInventoryId,
                    StoreId = r.StoreId,
                    ItemType = isIngredient
                        ? ItemTypeIngredient
                        : (r.PreparedItemId.HasValue && !r.RecipeId.HasValue ? ItemTypePreparedItem : ItemTypeRecipe),
                    ItemId = itemId,
                    LegacyRecipeId = r.RecipeId,
                    PreparedItemId = r.PreparedItemId,
                    IsLegacyUnmapped = !isIngredient && r.RecipeId.HasValue && !r.PreparedItemId.HasValue,
                    QuantitySemanticsStatus = isIngredient
                        ? QuantitySemanticsStatuses.NotApplicable
                        : r.RecipeId.HasValue
                            ? QuantitySemanticsStatuses.Unknown
                            : QuantitySemanticsStatuses.BaseUnitQuantityConfirmed,
                    ItemName = itemName,
                    ItemCode = itemCode,
                    OnHandQty = r.AvailableQty,
                    AvailableQty = r.AvailableQty,
                    ReservedQty = r.ReservedQty,
                    UsableQty = CalculateUsableQuantity(r.AvailableQty, r.ReservedQty),
                    UnitName = unitName,
                    MinStockLevel = r.MinStockLevel,
                    ThresholdConfigured = r.MinStockLevel.HasValue,
                    ThresholdStatus = MapThresholdStatus(
                        CalculateUsableQuantity(r.AvailableQty, r.ReservedQty),
                        r.MinStockLevel),
                    QuantityStatus = MapQuantityStatus(
                        CalculateUsableQuantity(r.AvailableQty, r.ReservedQty)),
                    LastUpdated = r.LastUpdated
                };
            }).ToList();

            return ServiceResult<POSBranchInventoryListDto>.Success(new POSBranchInventoryListDto
            {
                StoreId = storeId,
                Page = page,
                PageSize = pageSize,
                Total = total,
                Items = items
            });
        }

        public static string MapQuantityStatus(decimal availableQty)
        {
            if (availableQty < 0) return QuantityStatusNegative;
            if (availableQty == 0) return QuantityStatusOut;
            return QuantityStatusInStock;
        }

        /// <summary>
        /// Issue #97 — threshold display using MinStockLevel when configured.
        /// </summary>
        public static string MapThresholdStatus(decimal availableQty, decimal? minStockLevel)
        {
            if (!minStockLevel.HasValue)
                return ThresholdStatusUnconfigured;
            if (availableQty <= 0)
                return ThresholdStatusOut;
            if (availableQty <= minStockLevel.Value)
                return ThresholdStatusLow;
            return ThresholdStatusNormal;
        }

        public static decimal CalculateUsableQuantity(decimal onHandQty, decimal reservedQty) =>
            onHandQty - reservedQty;

        private static string? NormalizeItemType(string? itemType)
        {
            if (string.IsNullOrWhiteSpace(itemType))
                return null;

            if (itemType.Equals(ItemTypeIngredient, StringComparison.OrdinalIgnoreCase))
                return ItemTypeIngredient;
            if (itemType.Equals(ItemTypeRecipe, StringComparison.OrdinalIgnoreCase))
                return ItemTypeRecipe;
            if (itemType.Equals(ItemTypePreparedItem, StringComparison.OrdinalIgnoreCase))
                return ItemTypePreparedItem;

            return null;
        }

        private static string? NormalizeStockStatus(string? stockStatus)
        {
            if (string.IsNullOrWhiteSpace(stockStatus))
                return null;

            var value = stockStatus.Trim().ToUpperInvariant();
            return value is StockFilterOut or StockFilterLow or StockFilterNormal or StockFilterUnconfigured
                ? value
                : null;
        }
    }
}
