using CafeChain.Application.DTOs;
using CafeChain.Application.Interfaces;
using CafeChain.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CafeChain.Application.Services
{
    public class CartService : ICartService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly AppDbContext _context; // Tiêm DB vào đây
        private const string CartSessionKey = "UserCart";


        public CartService(IHttpContextAccessor httpContextAccessor, AppDbContext context)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = context;
        }

        private ISession Session => _httpContextAccessor.HttpContext.Session;

        public List<CartItem> GetCart()
        {
            var sessionData = Session.GetString(CartSessionKey);
            return sessionData == null ? new List<CartItem>() : JsonSerializer.Deserialize<List<CartItem>>(sessionData);
        }

        public void AddToCart(CartItem item)
        {
            var cart = GetCart();
            var existingItem = cart.FirstOrDefault(c => c.DrinkId == item.DrinkId);

            if (existingItem != null)
                existingItem.Quantity += item.Quantity;
            else
                cart.Add(item);

            Session.SetString(CartSessionKey, JsonSerializer.Serialize(cart));
        }

        public int GetTotalCount() => GetCart().Sum(c => c.Quantity);

        // Các hàm Remove và Clear cài đặt tương tự...
        public void RemoveFromCart(int drinkId) {
            var cart = GetCart();
            var item = cart.FirstOrDefault(c => c.DrinkId == drinkId);
            if (item != null)
            {
                cart.Remove(item);
                Session.SetString(CartSessionKey, JsonSerializer.Serialize(cart));
            }
        }
        public void UpdateQuantity(int drinkId, int quantity)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(c => c.DrinkId == drinkId);
            if (item != null)
            {
                if (quantity > 0)
                {
                    item.Quantity = quantity;
                    Session.SetString(CartSessionKey, JsonSerializer.Serialize(cart));
                }
                else
                {
                    RemoveFromCart(drinkId); // Nếu tụt xuống 0 thì xóa luôn
                }
            }
        }
        public async Task<bool> AddDrinkToCartAsync(int drinkId)
        {
            var drink = await _context.Drinks
                .Include(d => d.DrinkImages)
                .Include(d => d.DrinkSizes)
                .FirstOrDefaultAsync(d => d.DrinkId == drinkId);

            if (drink == null) return false;

            var item = new CartItem
            {
                DrinkId = drink.DrinkId,
                Name = drink.Name,
                ImageUrl = drink.DrinkImages.FirstOrDefault()?.ImageUrl,
                Price = drink.DrinkSizes.FirstOrDefault()?.Price ?? 0,
                Quantity = 1
            };

            // Tận dụng lại hàm AddToCart cũ bác đã viết để xử lý Session
            AddToCart(item);
            return true;
        }
        public void ClearCart() { Session.Remove(CartSessionKey); }
        // Nhớ using CafeChain.Application.DTOs; ở trên cùng nhé

        public async Task<bool> AddToCartAdvanceAsync(AddToCartRequest request)
        {
            // 1. Lấy thông tin Drink và Size
            var drink = await _context.Drinks.Include(d => d.DrinkImages).FirstOrDefaultAsync(d => d.DrinkId == request.DrinkId);
            if (drink == null) return false;

            var size = await _context.DrinkSizes.Include(s => s.Size)
                                     .FirstOrDefaultAsync(s => s.DrinkId == request.DrinkId && s.SizeId == request.SizeId);
            if (size == null) return false;

            // 2. Bắt đầu tính giá và tạo chuỗi mô tả
            decimal unitPrice = size.Price;
            List<string> toppingDescList = new List<string>();

            // Tính tiền Topping MUA THÊM
            if (request.OptionalToppingIds != null && request.OptionalToppingIds.Any())
            {
                var extraToppings = await _context.DrinkToppings.Include(dt => dt.Topping)
                                        .Where(dt => dt.DrinkId == request.DrinkId && request.OptionalToppingIds.Contains(dt.ToppingId))
                                        .ToListAsync();
                foreach (var t in extraToppings)
                {
                    unitPrice += t.Topping.Price; // Cộng tiền
                    toppingDescList.Add($"+ {t.Topping.Name}"); // Ghi chú lại
                }
            }

            // Xử lý Topping MẶC ĐỊNH BỊ BỎ ĐI (Giá 0đ nên không trừ tiền, chỉ ghi chú cho Barista biết)
            if (request.RemovedDefaultToppingIds != null && request.RemovedDefaultToppingIds.Any())
            {
                var removedToppings = await _context.DrinkDefaultToppings.Include(dt => dt.Topping)
                                        .Where(dt => dt.DrinkId == request.DrinkId && request.RemovedDefaultToppingIds.Contains(dt.ToppingId))
                                        .ToListAsync();
                foreach (var r in removedToppings)
                {
                    toppingDescList.Add($"- Không lấy {r.Topping.Name}"); // Ghi chú lại
                }
            }

            // 3. Đóng gói vào CartItem
            var item = new CartItem
            {
                CartItemId = Guid.NewGuid().ToString(),
                DrinkId = drink.DrinkId,
                Name = drink.Name,
                ImageUrl = drink.DrinkImages.FirstOrDefault()?.ImageUrl,
                Price = unitPrice,
                Quantity = request.Quantity,
                SizeName = size.Size.Name,
                ToppingsDescription = string.Join(", ", toppingDescList)
            };

            // 4. Lưu vào Giỏ hàng
            var cart = GetCart();

            // Gộp chung nếu khách bấm 2 lần y hệt nhau (Cùng món, cùng size, cùng topping)
            var existingItem = cart.FirstOrDefault(c => c.DrinkId == item.DrinkId && c.SizeName == item.SizeName && c.ToppingsDescription == item.ToppingsDescription);
            if (existingItem != null)
            {
                existingItem.Quantity += item.Quantity;
            }
            else
            {
                cart.Add(item);
            }

            // Cập nhật lại Session (Dùng dòng code tương tự hàm AddToCart cũ của bác)
            _httpContextAccessor.HttpContext.Session.SetString(CartSessionKey, JsonSerializer.Serialize(cart));

            return true;
        }
    }
}