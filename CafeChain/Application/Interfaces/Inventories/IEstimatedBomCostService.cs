using System.Collections.Generic;
using System.Threading.Tasks;
using CafeChain.Application.DTOs.Costing;

namespace CafeChain.Application.Interfaces.Inventories
{
    /// <summary>
    /// EstimatedBomCost — package-normalized design estimate (Issue #117 / ADR-0005).
    /// Not StoreOperationalCost and not HistoricalOrderCogs.
    /// Does not mutate stock. Does not apply YieldPercentage as a second cost factor.
    /// </summary>
    public interface IEstimatedBomCostService
    {
        /// <summary>Resolve package price → Ingredient base-unit cost for one ingredient.</summary>
        Task<IngredientBaseUnitCostResult> ResolveIngredientBaseUnitCostAsync(int ingredientId);

        /// <summary>Recursive recipe EstimatedBomCost with COMPLETE/INCOMPLETE.</summary>
        Task<CostCalculationResult> CalculateRecipeEstimatedCostAsync(int recipeId);

        /// <summary>
        /// Calculates several recipe roots in one request-scoped pass and reuses child-recipe results.
        /// Intended for admin read models; it does not change costing authority or mutate data.
        /// </summary>
        Task<IReadOnlyDictionary<int, CostCalculationResult>> CalculateRecipesEstimatedCostAsync(
            IEnumerable<int> recipeIds);
    }
}
