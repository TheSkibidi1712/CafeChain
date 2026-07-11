using System.Threading.Tasks;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Admin.Recipes
{
    /// <summary>
    /// Validates and normalizes Recipe BTP output into PreparedItem base unit (Issue #112 / ADR-0006).
    /// Does not persist normalized qty; does not apply YieldPercentage.
    /// </summary>
    public interface IRecipeOutputNormalizer
    {
        /// <summary>
        /// Validate PreparedItem + OutputUnit + OutputQuantity and convert to PreparedItem.BaseUnitId.
        /// </summary>
        Task<ServiceResult<RecipeOutputNormalizationResult>> NormalizeAsync(
            int preparedItemId,
            decimal outputQuantity,
            int outputUnitId);
    }

    public sealed class RecipeOutputNormalizationResult
    {
        public int PreparedItemId { get; init; }
        public string PreparedItemCode { get; init; } = "";
        public string PreparedItemName { get; init; } = "";
        public int BaseUnitId { get; init; }
        public string BaseUnitCode { get; init; } = "";
        public string BaseUnitName { get; init; } = "";
        public int OutputUnitId { get; init; }
        public string OutputUnitCode { get; init; } = "";
        public string OutputUnitName { get; init; } = "";
        public decimal OutputQuantity { get; init; }

        /// <summary>OutputQuantity converted to PreparedItem.BaseUnitId. Never multiplies YieldPercentage.</summary>
        public decimal NormalizedQuantityInBase { get; init; }

        public string PreviewText =>
            $"{OutputQuantity:0.####} {OutputUnitCode} = {NormalizedQuantityInBase:0.####} {BaseUnitCode}";
    }
}
