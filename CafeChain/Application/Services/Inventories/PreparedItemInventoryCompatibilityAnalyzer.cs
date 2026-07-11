using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Data;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Inventories
{
    /// <summary>
    /// #115 read-only compatibility dry run. It reports whether a proposed metadata link
    /// requires owner-reviewed remediation; it never assigns PreparedItemId or changes qty.
    /// </summary>
    public sealed class PreparedItemInventoryCompatibilityAnalyzer : IPreparedItemInventoryCompatibilityAnalyzer
    {
        private readonly AppDbContext _context;
        private readonly IPhysicalUnitConversionService _physicalUnitConversion;

        public PreparedItemInventoryCompatibilityAnalyzer(
            AppDbContext context,
            IPhysicalUnitConversionService physicalUnitConversion)
        {
            _context = context;
            _physicalUnitConversion = physicalUnitConversion;
        }

        public async Task<PreparedItemInventoryCompatibilityReport> AnalyzeAsync(
            int storeInventoryId,
            int proposedPreparedItemId)
        {
            var issues = new List<string>();
            var source = await _context.StoreInventories
                .AsNoTracking()
                .Include(x => x.Recipe).ThenInclude(x => x.OutputUnit)
                .FirstOrDefaultAsync(x => x.StoreInventoryId == storeInventoryId);

            if (source == null)
            {
                return Blocked(storeInventoryId, 0, null, proposedPreparedItemId,
                    new[] { InventoryIdentityValidationIssueCodes.NoIdentity });
            }

            var preparedItem = await _context.PreparedItems
                .AsNoTracking()
                .Include(x => x.BaseUnit)
                .FirstOrDefaultAsync(x => x.PreparedItemId == proposedPreparedItemId);

            if (preparedItem == null || !preparedItem.Active || preparedItem.BaseUnit == null || !preparedItem.BaseUnit.Active)
                issues.Add(preparedItem == null
                    ? InventoryIdentityValidationIssueCodes.MissingPreparedItem
                    : InventoryIdentityValidationIssueCodes.InactivePreparedItem);

            var recipeConsistent = source.Recipe != null
                && source.Recipe.PreparedItemId == proposedPreparedItemId;
            if (source.RecipeId == null || source.Recipe == null)
                issues.Add(InventoryIdentityValidationIssueCodes.NoIdentity);
            else if (!recipeConsistent)
                issues.Add(InventoryIdentityValidationIssueCodes.RecipePreparedItemMismatch);

            // Legacy RecipeId quantities may represent batches rather than net output units.
            // Compatible units are necessary but never enough evidence for mapping in #115.
            var quantitySemantics = source.RecipeId.HasValue
                ? QuantitySemanticsStatuses.Unknown
                : QuantitySemanticsStatuses.NoBtpRow;
            if (quantitySemantics == QuantitySemanticsStatuses.Unknown)
                issues.Add(InventoryIdentityValidationIssueCodes.QuantitySemanticsUnknown);

            var relatedRows = await _context.StoreInventories
                .AsNoTracking()
                .Include(x => x.Recipe)
                .Where(x => x.StoreId == source.StoreId
                    && (x.StoreInventoryId == source.StoreInventoryId
                        || x.PreparedItemId == proposedPreparedItemId
                        || (x.RecipeId != null && x.Recipe != null && x.Recipe.PreparedItemId == proposedPreparedItemId)))
                .OrderBy(x => x.StoreInventoryId)
                .ToListAsync();

            var recipeRows = relatedRows.Where(x => x.RecipeId.HasValue).ToList();
            var unitsCompatible = preparedItem != null && recipeRows.Count > 0;
            foreach (var outputUnitId in recipeRows.Select(x => x.Recipe?.OutputUnitId).Distinct())
            {
                if (!outputUnitId.HasValue || preparedItem == null)
                {
                    unitsCompatible = false;
                    issues.Add(InventoryIdentityValidationIssueCodes.MissingRecipeOutputUnit);
                    continue;
                }

                var conversion = await _physicalUnitConversion.ConvertAsync(
                    1m,
                    outputUnitId.Value,
                    preparedItem.BaseUnitId);
                if (!conversion.IsSuccess)
                {
                    unitsCompatible = false;
                    issues.Add(InventoryIdentityValidationIssueCodes.UnitIncompatible);
                }
            }

            var involvedIds = relatedRows.Select(x => x.StoreInventoryId).ToList();
            var otherRows = relatedRows.Where(x => x.StoreInventoryId != source.StoreInventoryId).ToList();
            if (otherRows.Count > 0)
            {
                issues.Add(otherRows.Any(x => x.PreparedItemId == proposedPreparedItemId)
                    ? InventoryIdentityValidationIssueCodes.ExistingTargetRow
                    : InventoryIdentityValidationIssueCodes.MultipleLegacyRows);
            }

            if (HasConflictingValues(relatedRows.Select(x => x.MinStockLevel)))
                issues.Add(InventoryIdentityValidationIssueCodes.MinStockLevelConflict);
            if (HasConflictingValues(relatedRows.Select(x => x.MaxNegativeQty)))
                issues.Add(InventoryIdentityValidationIssueCodes.MaxNegativeQtyConflict);

            var beforeAvailable = relatedRows.Sum(x => x.AvailableQty);
            var beforeReserved = relatedRows.Sum(x => x.ReservedQty);
            var uniqueIssues = issues.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
            var collision = otherRows.Count > 0
                || uniqueIssues.Contains(InventoryIdentityValidationIssueCodes.MinStockLevelConflict)
                || uniqueIssues.Contains(InventoryIdentityValidationIssueCodes.MaxNegativeQtyConflict);
            var ready = uniqueIssues.Count == 0 && unitsCompatible
                && quantitySemantics == QuantitySemanticsStatuses.BaseUnitQuantityConfirmed;

            return new PreparedItemInventoryCompatibilityReport
            {
                StoreInventoryId = source.StoreInventoryId,
                StoreId = source.StoreId,
                RecipeId = source.RecipeId,
                ProposedPreparedItemId = proposedPreparedItemId,
                RecipeOutputQuantity = source.Recipe?.OutputQuantity,
                RecipeOutputUnitId = source.Recipe?.OutputUnitId,
                PreparedItemBaseUnitId = preparedItem?.BaseUnitId,
                PreparedItemBaseUnitCode = preparedItem?.BaseUnit?.UnitCode,
                AvailableQty = source.AvailableQty,
                ReservedQty = source.ReservedQty,
                MinStockLevel = source.MinStockLevel,
                MaxNegativeQty = source.MaxNegativeQty,
                RecipePreparedItemConsistent = recipeConsistent,
                UnitsPhysicallyCompatible = unitsCompatible,
                QuantitySemanticsStatus = collision
                    ? QuantitySemanticsStatuses.Collision
                    : quantitySemantics,
                CollisionStatus = collision
                    ? CompatibilityCollisionStatuses.Collision
                    : CompatibilityCollisionStatuses.None,
                InvolvedStoreInventoryIds = involvedIds,
                BeforeAvailableTotal = beforeAvailable,
                BeforeReservedTotal = beforeReserved,
                HypotheticalAfterAvailableTotal = beforeAvailable,
                HypotheticalAfterReservedTotal = beforeReserved,
                NumericConservationConfirmed = true,
                ProposedAction = ready
                    ? CompatibilityProposedActions.ReadyForMetadataMapping
                    : CompatibilityProposedActions.Blocked,
                BlockingIssues = uniqueIssues
            };
        }

        private static PreparedItemInventoryCompatibilityReport Blocked(
            int storeInventoryId,
            int storeId,
            int? recipeId,
            int proposedPreparedItemId,
            IReadOnlyList<string> issues)
        {
            return new PreparedItemInventoryCompatibilityReport
            {
                StoreInventoryId = storeInventoryId,
                StoreId = storeId,
                RecipeId = recipeId,
                ProposedPreparedItemId = proposedPreparedItemId,
                QuantitySemanticsStatus = QuantitySemanticsStatuses.NoBtpRow,
                ProposedAction = CompatibilityProposedActions.Blocked,
                BlockingIssues = issues
            };
        }

        private static bool HasConflictingValues(IEnumerable<decimal?> values)
        {
            return values.Distinct().Skip(1).Any();
        }
    }
}
