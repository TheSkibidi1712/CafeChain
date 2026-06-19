using CafeChain.Application.DTOs;

namespace CafeChain.Application.Interfaces
{
    public interface ICartService
    {
        // Hàm mới: Nhận ID đồ uống, tự tìm trong DB và thêm vào giỏ Session
        Task<bool> AddDrinkToCartAsync(int drinkId);
        List<CartItem> GetCart();
        void UpdateQuantity(string cartItemId, int quantity);
        void RemoveFromCart(string cartItemId);
        void AddToCart(CartItem item);
        void ClearCart();
        int GetTotalCount();
        Task<bool> AddToCartAdvanceAsync(AddToCartRequest request);
    }
}