using CafeChain.Models.Drinks;
using CafeChain.ViewModels;

namespace CafeChain.Application.Interfaces
{
    public interface IDrinkService
    {
        // Trả về một Tuple chứa 3 thông tin: Danh mục, Đồ uống và Tổng số trang
        Task<(List<DrinkCategory> Categories, List<Drink> Drinks, int TotalPages)> GetMenuDataAsync(
            string? keyword,
            int? categoryId,
            decimal minPrice,
            decimal maxPrice,
            string sortBy,
            int page,
            int pageSize);

        Task<DrinkDetailViewModel?> GetDrinkDetailAsync(int drinkId);

        Task<(bool Success, string Message, string NewAverageRating)> SubmitReviewAsync(
            int customerId, int drinkId, int stars, string comment, Microsoft.AspNetCore.Http.IFormFileCollection? images, string webRootPath);

        Task<(bool Success, string Message, string Action, CafeChain.Models.Enums.Customer.ReactionType Type, int TotalCount)> ToggleReactionAsync(
            int ratingId, int customerId, CafeChain.Models.Enums.Customer.ReactionType type);

        Task<(bool Success, string Message, int? RatingId, string? ImageUrl)> SubmitReplyAsync(
            int customerId, int parentRatingId, int drinkId, string? comment, System.IO.Stream? imageStream, string? fileName, long? fileLength, string webRootPath);

        Task<bool> CheckDrinkAvailabilityAsync(int drinkId, int storeId);
    }
}