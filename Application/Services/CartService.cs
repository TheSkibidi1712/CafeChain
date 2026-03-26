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
        public void RemoveFromCart(string cartItemId)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(c => c.CartItemId == cartItemId); // Tìm bằng CartItemId
            if (item != null)
            {
                cart.Remove(item);
                Session.SetString(CartSessionKey, JsonSerializer.Serialize(cart));
            }
        }
        public void UpdateQuantity(string cartItemId, int quantity)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(c => c.CartItemId == cartItemId); // Tìm bằng CartItemId
            if (item != null)
            {
                if (quantity > 0)
                {
                    item.Quantity = quantity;
                    Session.SetString(CartSessionKey, JsonSerializer.Serialize(cart));
                }
                else
                {
                    RemoveFromCart(cartItemId);
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

            // 2. Bắt đầu tính giá
            decimal unitPrice = size.Price;

            // Khởi tạo Item (Thay vì dùng ToppingsDescription, ta dùng 2 List mới)
            var item = new CartItem
            {
                CartItemId = Guid.NewGuid().ToString(),
                DrinkId = drink.DrinkId,
                Name = drink.Name,
                ImageUrl = drink.DrinkImages.FirstOrDefault()?.ImageUrl ?? "/images/default.jpg",
                Quantity = request.Quantity,
                SizeName = size.Size.Name,
                // Khởi tạo 2 List rỗng
                AddedToppings = new List<string>(),
                RemovedToppings = new List<string>(),
                // 🔥 1. CHUẨN HÓA GHI CHÚ: Cắt khoảng trắng dư thừa, nếu null thì gán chuỗi rỗng để dễ so sánh
                Note = string.IsNullOrWhiteSpace(request.Note) ? "" : request.Note.Trim()
            };

            // Tính tiền Topping MUA THÊM
            if (request.OptionalToppingIds != null && request.OptionalToppingIds.Any())
            {
                var extraToppings = await _context.DrinkToppings.Include(dt => dt.Topping)
                                        .Where(dt => dt.DrinkId == request.DrinkId && request.OptionalToppingIds.Contains(dt.ToppingId))
                                        .ToListAsync();
                foreach (var t in extraToppings)
                {
                    if (t.Topping == null)
                    {
                        continue;
                    }
                    unitPrice += t.Topping.Price; // Cộng tiền
                    item.AddedToppings.Add($"{t.Topping.Name} (+{t.Topping.Price.ToString("N0")}đ)"); // Nhét vào mảng Added
                }
            }

            // Xử lý Topping MẶC ĐỊNH BỊ BỎ ĐI
            if (request.RemovedDefaultToppingIds != null && request.RemovedDefaultToppingIds.Any())
            {
                var removedToppings = await _context.DrinkDefaultToppings.Include(dt => dt.Topping)
                                        .Where(dt => dt.DrinkId == request.DrinkId && request.RemovedDefaultToppingIds.Contains(dt.ToppingId))
                                        .ToListAsync();
                foreach (var r in removedToppings)
                {
                    item.RemovedToppings.Add(r.Topping.Name); // Nhét vào mảng Removed
                }
            }

            // Chốt giá cuối cùng cho ly nước (Size + Topping mua thêm)
            item.Price = unitPrice;

            // 3. Lưu vào Giỏ hàng
            var cart = GetCart();

            // Gộp chung nếu khách bấm 2 lần y hệt nhau (Dùng string.Join để so sánh 2 mảng)
            var existingItem = cart.FirstOrDefault(c =>
                c.DrinkId == item.DrinkId &&
                c.SizeName == item.SizeName &&
                string.Join(",", c.AddedToppings.OrderBy(x => x)) == string.Join(",", item.AddedToppings.OrderBy(x => x)) &&
                string.Join(",", c.RemovedToppings.OrderBy(x => x)) == string.Join(",", item.RemovedToppings.OrderBy(x => x)) &&
                (c.Note ?? "") == item.Note // Thêm dòng này để ép check Ghi chú
            );

            if (existingItem != null)
            {
                existingItem.Quantity += item.Quantity; // Gộp số lượng
            }
            else
            {
                cart.Add(item); // Tạo dòng mới
            }

            // 4. Cập nhật lại Session
            _httpContextAccessor.HttpContext.Session.SetString(CartSessionKey, JsonSerializer.Serialize(cart));

            return true;
        }
    }
}