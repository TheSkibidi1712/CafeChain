using CafeChain.ViewModels.Admin.Recipes;
using CafeChain.Application.Results;
using System.Threading.Tasks;

namespace CafeChain.Application.Interfaces.Admin.Recipes
{
    public interface IAdminRecipeService
    {
        Task<ServiceResult> CreateRecipeAsync(RecipeCreateVM model);

        /// <summary>
        /// Versioning Update: Soft-Delete bản cũ → Insert bản mới.
        /// Chạy DFS Anti-Loop trước khi lưu.
        /// </summary>
        Task<ServiceResult> UpdateRecipeAsync(int recipeId, RecipeCreateVM model);

        /// <summary>
        /// Xóa công thức: Kiểm tra ràng buộc (ChildRecipe, ProductionOrders, OrderDetails).
        /// Nếu dính lịch sử → Soft-Delete. Nếu không → Hard Delete.
        /// </summary>
        Task<ServiceResult> DeleteRecipeAsync(int recipeId);
    }
}
