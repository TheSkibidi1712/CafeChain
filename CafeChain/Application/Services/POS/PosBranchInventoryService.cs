using CafeChain.Application.DTOs.POS;
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
        public const string QuantityStatusNegative = "Tồn âm";
        public const string QuantityStatusOut = "Hết hàng";
        public const string QuantityStatusInStock = "Còn hàng";
        public const string ItemTypeIngredient = "Ingredient";
        public const string ItemTypeRecipe = "Recipe";

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
            int pageSize)
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
                    "itemType phải là Ingredient, Recipe, hoặc để trống.");
            }

            var query = _context.StoreInventories
                .AsNoTracking()
                .Where(i => i.StoreId == storeId);

            if (normalizedType == ItemTypeIngredient)
            {
                query = query.Where(i => i.IngredientId != null);
            }
            else if (normalizedType == ItemTypeRecipe)
            {
                query = query.Where(i => i.RecipeId != null);
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
                      (i.Recipe.RecipeCode != null && i.Recipe.RecipeCode.Contains(keyword)))));
            }

            var total = await query.CountAsync();

            var rows = await query
                .OrderBy(i => i.IngredientId != null
                    ? i.Ingredient.Name
                    : (i.Recipe != null ? i.Recipe.Name : ""))
                .ThenBy(i => i.StoreInventoryId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(i => new
                {
                    i.StoreInventoryId,
                    i.StoreId,
                    i.IngredientId,
                    i.RecipeId,
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
                    i.AvailableQty,
                    i.ReservedQty,
                    i.LastUpdated
                })
                .ToListAsync();

            var items = rows.Select(r =>
            {
                var isIngredient = r.IngredientId.HasValue;
                var itemId = isIngredient ? r.IngredientId!.Value : (r.RecipeId ?? 0);
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
                    itemName = !string.IsNullOrWhiteSpace(r.RecipeName)
                        ? r.RecipeName!
                        : (!string.IsNullOrWhiteSpace(r.RecipeCode)
                            ? r.RecipeCode!
                            : $"Bán thành phẩm #{itemId}");
                    itemCode = r.RecipeCode;
                    unitName = "—";
                }

                return new POSBranchInventoryItemDto
                {
                    StoreInventoryId = r.StoreInventoryId,
                    StoreId = r.StoreId,
                    ItemType = isIngredient ? ItemTypeIngredient : ItemTypeRecipe,
                    ItemId = itemId,
                    ItemName = itemName,
                    ItemCode = itemCode,
                    AvailableQty = r.AvailableQty,
                    ReservedQty = r.ReservedQty,
                    UnitName = unitName,
                    MinStockLevel = null,
                    ThresholdConfigured = false,
                    ThresholdStatus = ThresholdStatusUnconfigured,
                    QuantityStatus = MapQuantityStatus(r.AvailableQty),
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

        private static string? NormalizeItemType(string? itemType)
        {
            if (string.IsNullOrWhiteSpace(itemType))
                return null;

            if (itemType.Equals(ItemTypeIngredient, StringComparison.OrdinalIgnoreCase))
                return ItemTypeIngredient;
            if (itemType.Equals(ItemTypeRecipe, StringComparison.OrdinalIgnoreCase))
                return ItemTypeRecipe;

            return null;
        }
    }
}
