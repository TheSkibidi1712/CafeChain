using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Inventories
{
    /// <summary>
    /// Read-only StoreInventory identity resolver for the additive #115 transition.
    /// PreparedItem wins display identity when a compatibility RecipeId + PreparedItemId row exists.
    /// </summary>
    public sealed class InventoryItemIdentityResolver : IInventoryItemIdentityResolver
    {
        private readonly AppDbContext _context;

        public InventoryItemIdentityResolver(AppDbContext context)
        {
            _context = context;
        }

        public async Task<InventoryItemIdentitySnapshot?> ResolveStoreInventoryAsync(int storeInventoryId)
        {
            if (storeInventoryId <= 0)
                return null;

            var inventory = await _context.StoreInventories
                .AsNoTracking()
                .Include(x => x.Ingredient).ThenInclude(x => x.BaseUnit)
                .Include(x => x.Recipe).ThenInclude(x => x.OutputUnit)
                .Include(x => x.PreparedItem).ThenInclude(x => x!.BaseUnit)
                .FirstOrDefaultAsync(x => x.StoreInventoryId == storeInventoryId);

            return inventory == null ? null : BuildSnapshot(inventory);
        }

        internal static InventoryItemIdentitySnapshot BuildSnapshot(Models.Stores.StoreInventory inventory)
        {
            var issues = new List<string>();
            var hasIngredient = inventory.IngredientId.HasValue;
            var hasRecipe = inventory.RecipeId.HasValue;
            var hasPreparedItem = inventory.PreparedItemId.HasValue;

            if (hasIngredient)
            {
                if (hasRecipe || hasPreparedItem)
                    issues.Add(InventoryIdentityValidationIssueCodes.InvalidIngredientCombination);

                return new InventoryItemIdentitySnapshot
                {
                    StoreInventoryId = inventory.StoreInventoryId,
                    StoreId = inventory.StoreId,
                    InventoryItemType = InventoryItemIdentityTypes.Ingredient,
                    IngredientId = inventory.IngredientId,
                    Code = inventory.Ingredient?.Code ?? $"INGREDIENT-{inventory.IngredientId}",
                    Name = inventory.Ingredient?.Name ?? $"Nguyên liệu #{inventory.IngredientId}",
                    BaseUnitId = inventory.Ingredient?.BaseUnitId,
                    BaseUnitCode = inventory.Ingredient?.BaseUnit?.UnitCode,
                    QuantitySemanticsStatus = QuantitySemanticsStatuses.NotApplicable,
                    ValidationIssues = issues
                };
            }

            if (hasPreparedItem)
            {
                if (inventory.PreparedItem == null)
                    issues.Add(InventoryIdentityValidationIssueCodes.MissingPreparedItem);
                else if (!inventory.PreparedItem.Active || inventory.PreparedItem.BaseUnit == null || !inventory.PreparedItem.BaseUnit.Active)
                    issues.Add(InventoryIdentityValidationIssueCodes.InactivePreparedItem);

                if (hasRecipe)
                {
                    if (inventory.Recipe == null)
                        issues.Add(InventoryIdentityValidationIssueCodes.MissingRecipe);
                    else if (inventory.Recipe.PreparedItemId != inventory.PreparedItemId)
                        issues.Add(InventoryIdentityValidationIssueCodes.RecipePreparedItemMismatch);
                }

                return new InventoryItemIdentitySnapshot
                {
                    StoreInventoryId = inventory.StoreInventoryId,
                    StoreId = inventory.StoreId,
                    InventoryItemType = InventoryItemIdentityTypes.PreparedItem,
                    PreparedItemId = inventory.PreparedItemId,
                    LegacyRecipeId = inventory.RecipeId,
                    Code = inventory.PreparedItem?.Code ?? $"PREPARED-{inventory.PreparedItemId}",
                    Name = inventory.PreparedItem?.Name ?? $"BTP #{inventory.PreparedItemId}",
                    BaseUnitId = inventory.PreparedItem?.BaseUnitId,
                    BaseUnitCode = inventory.PreparedItem?.BaseUnit?.UnitCode,
                    HasCompatibilityRecipe = hasRecipe,
                    QuantitySemanticsStatus = inventory.QuantitySemanticsStatus switch
                    {
                        InventoryQuantitySemanticsStatus.BaseUnitConfirmed => QuantitySemanticsStatuses.BaseUnitQuantityConfirmed,
                        InventoryQuantitySemanticsStatus.LegacyBatch => QuantitySemanticsStatuses.LegacyBatchQuantity,
                        InventoryQuantitySemanticsStatus.Incompatible => QuantitySemanticsStatuses.UnitIncompatible,
                        _ => hasRecipe ? QuantitySemanticsStatuses.Unknown : QuantitySemanticsStatuses.BaseUnitQuantityConfirmed
                    },
                    ValidationIssues = issues.OrderBy(x => x, StringComparer.Ordinal).ToList()
                };
            }

            if (hasRecipe)
            {
                if (inventory.Recipe == null)
                    issues.Add(InventoryIdentityValidationIssueCodes.MissingRecipe);

                return new InventoryItemIdentitySnapshot
                {
                    StoreInventoryId = inventory.StoreInventoryId,
                    StoreId = inventory.StoreId,
                    InventoryItemType = InventoryItemIdentityTypes.LegacyRecipe,
                    LegacyRecipeId = inventory.RecipeId,
                    Code = inventory.Recipe?.RecipeCode ?? $"RECIPE-{inventory.RecipeId}",
                    Name = inventory.Recipe?.Name ?? $"Công thức #{inventory.RecipeId}",
                    BaseUnitId = null,
                    BaseUnitCode = null,
                    IsLegacyUnmapped = true,
                    QuantitySemanticsStatus = QuantitySemanticsStatuses.Unknown,
                    ValidationIssues = issues
                };
            }

            issues.Add(InventoryIdentityValidationIssueCodes.NoIdentity);
            return new InventoryItemIdentitySnapshot
            {
                StoreInventoryId = inventory.StoreInventoryId,
                StoreId = inventory.StoreId,
                ValidationIssues = issues
            };
        }
    }
}
