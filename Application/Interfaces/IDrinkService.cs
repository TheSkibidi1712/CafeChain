using CafeChain.Models.Drinks;
using CafeChain.ViewModels;

namespace CafeChain.Application.Interfaces
{
    public interface IDrinkService
    {
        // Trả về một Tuple chứa 3 thông tin: Danh mục, Đồ uống và Tổng số trang
        Task<(List<DrinkCategory> Categories, List<Drink> Drinks, int TotalPages)> GetMenuDataAsync(
            int? categoryId,
            decimal minPrice,
            decimal maxPrice,
            string sortBy,
            int page,
            int pageSize);
        Task<DrinkDetailViewModel> GetDrinkDetailAsync(int drinkId);
    }
}