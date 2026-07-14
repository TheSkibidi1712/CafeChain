using CafeChain.Application.DTOs.Admin.Production;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Admin.Production
{
    public interface IProductionReadinessService
    {
        Task<IReadOnlyList<ProductionRecipeOptionDto>> GetRecipeOptionsAsync();

        Task<ServiceResult<ProductionReadinessPreviewDto>> PreviewAsync(
            int storeId,
            int recipeId,
            decimal runCount);
    }
}
