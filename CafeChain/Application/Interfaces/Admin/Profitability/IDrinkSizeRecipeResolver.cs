using CafeChain.Application.DTOs.Admin.Profitability;

namespace CafeChain.Application.Interfaces.Admin.Profitability
{
    public interface IDrinkSizeRecipeResolver
    {
        Task<DrinkSizeRecipeResolution> ResolveExactAsync(int drinkId, int sizeId, DateTime asOfUtc, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<DrinkSizeRecipeHealthRow>> GetDataHealthAsync(DateTime asOfUtc, CancellationToken cancellationToken = default);
    }
}
