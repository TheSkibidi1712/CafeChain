using CafeChain.Application.Interfaces;
using CafeChain.Data;
using CafeChain.Models.Customers;
using CafeChain.Models.Drinks;
using CafeChain.Models.Inventories;
using CafeChain.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace CafeChain.Application.Services
{
    public class DrinkService : IDrinkService
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<DrinkService> _logger;
        private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;

        public DrinkService(AppDbContext context, IMemoryCache cache, ILogger<DrinkService> logger, Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
            _env = env;
        }

        public async Task<(List<DrinkCategory> Categories, List<Drink> Drinks, int TotalPages)> GetMenuDataAsync(
            string? keyword, int? categoryId, decimal minPrice, decimal maxPrice, string sortBy, int page, int pageSize)
        {
            // Lấy danh mục
            var categories = await _context.DrinkCategories.Where(c => c.Active).ToListAsync();

            // Khởi tạo Query
            var query = _context.Drinks
                .Include(d => d.DrinkImages)
                .Include(d => d.DrinkSizes)
                .Include(d => d.Ratings)
                .Where(d => d.Active)
                .AsQueryable();

            // Lọc theo từ khóa
            if (!string.IsNullOrEmpty(keyword))
            {
                string searchKeyword = keyword.ToLower();
                query = query.Where(d => d.Name.ToLower().Contains(searchKeyword));
            }

            // Lọc theo danh mục
            if (categoryId.HasValue && categoryId > 0)
            {
                query = query.Where(d => d.CategoryId == categoryId);
            }

            // Lọc theo giá (Căn cứ vào giá của Size mặc định/đầu tiên để đồng bộ với hiển thị trên Card)
            query = query.Where(d => d.DrinkSizes
                .OrderBy(s => s.DrinkSizeId)
                .Take(1)
                .Any(s => s.Price >= minPrice && s.Price <= maxPrice));

            // Sắp xếp
            switch (sortBy)
            {
                case "price_asc":
                    query = query.OrderBy(d => d.DrinkSizes.Min(s => s.Price));
                    break;
                case "price_desc":
                    query = query.OrderByDescending(d => d.DrinkSizes.Min(s => s.Price));
                    break;
                default:
                    query = query.OrderByDescending(d => d.DrinkId);
                    break;
            }

            // Phân trang
            int totalItems = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var drinks = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (categories, drinks, totalPages == 0 ? 1 : totalPages);
        }
        public async Task<DrinkDetailViewModel?> GetDrinkDetailAsync(int drinkId)
        {
            var drink = await _context.Drinks
                .Include(d => d.DrinkImages)
                .Include(d => d.DrinkSizes)
                    .ThenInclude(ds => ds.Size)
                .Include(d => d.DrinkDefaultToppings)
                    .ThenInclude(dt => dt.Topping)
                .Include(d => d.DrinkToppings)
                    .ThenInclude(dt => dt.Topping)
                .Include(d => d.Category)
                .Include(d => d.Ratings)
                    .ThenInclude(r => r.Customer)
                .FirstOrDefaultAsync(d => d.DrinkId == drinkId && d.Active);

            if (drink == null) return null;

            var relatedDrinks = await _context.Drinks
                .Include(d => d.DrinkImages)
                .Include(d => d.DrinkSizes)
                .Where(d => d.CategoryId == drink.CategoryId && d.DrinkId != drinkId && d.Active)
                .Take(4)
                .ToListAsync();

            if (drink.Ratings != null)
            {
                foreach (var rating in drink.Ratings)
                {
                    await _context.Entry(rating).Collection(r => r.Replies).Query().Include(r => r.Customer).Include(r => r.Images).Include(r => r.Reactions).LoadAsync();
                    await _context.Entry(rating).Collection(r => r.Images).LoadAsync();
                    await _context.Entry(rating).Collection(r => r.Reactions).LoadAsync();
                }
            }

            return new DrinkDetailViewModel
            {
                Drink = drink,
                RelatedDrinks = relatedDrinks,
                DefaultToppings = drink.DrinkDefaultToppings?.ToList() ?? new List<DrinkDefaultTopping>(),
                OptionalToppings = drink.DrinkToppings?.ToList() ?? new List<DrinkTopping>(),
                Ratings = drink.Ratings != null
                    ? drink.Ratings.Where(r => r.ParentRatingId == null).OrderByDescending(r => r.CreatedAt).ToList()
                    : new List<Rating>()
            };
        }

        public async Task<(bool Success, string Message, string NewAverageRating)> SubmitReviewAsync(
            int customerId, int drinkId, int stars, string comment, Microsoft.AspNetCore.Http.IFormFileCollection? images, string webRootPath)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingReview = await _context.Ratings
                    .FirstOrDefaultAsync(r => r.CustomerId == customerId && r.DrinkId == drinkId);

                Rating ratingToHandle = existingReview;

                if (existingReview != null)
                {
                    existingReview.Stars = stars;
                    existingReview.Comment = comment;
                    existingReview.CreatedAt = DateTime.Now;
                    _context.Ratings.Update(existingReview);
                }
                else
                {
                    ratingToHandle = new Rating
                    {
                        DrinkId = drinkId,
                        CustomerId = customerId,
                        Stars = stars,
                        Comment = comment,
                        CreatedAt = DateTime.Now
                    };
                    _context.Ratings.Add(ratingToHandle);
                }

                await _context.SaveChangesAsync(); // Cần SaveChanges để lấy RatingId nếu là add mới

                // Xử lý upload danh sách ảnh
                if (images != null && images.Any())
                {
                    // Tùy chọn: Xóa ảnh cũ nếu là Edit (tùy nghiệp vụ, ở đây ta đơn giản hóa: cứ up mới là xóa ảnh cũ)
                    var oldImages = _context.Set<RatingImage>().Where(ri => ri.RatingId == ratingToHandle.RatingId);
                    if (oldImages.Any())
                    {
                        _context.Set<RatingImage>().RemoveRange(oldImages);
                        await _context.SaveChangesAsync();
                    }

                    int savedCount = 0;
                    foreach (var file in images)
                    {
                        if (savedCount >= 5) break; // Tối đa 5 ảnh

                        if (file.Length > 1 * 1024 * 1024) continue; // Bỏ qua file > 1MB

                        var extension = Path.GetExtension(file.FileName).ToLower();
                        if (extension != ".jpg" && extension != ".jpeg" && extension != ".png") continue;

                        var newFileName = Guid.NewGuid().ToString() + extension;
                        var uploadsFolder = Path.Combine(webRootPath, "images", "ratings");
                        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                        var filePath = Path.Combine(uploadsFolder, newFileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        _context.Set<RatingImage>().Add(new RatingImage
                        {
                            RatingId = ratingToHandle.RatingId,
                            ImageUrl = $"/images/ratings/{newFileName}",
                            CreatedAt = DateTime.Now
                        });
                        savedCount++;
                    }
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                var newAvgRating = _context.Ratings.Where(r => r.DrinkId == drinkId).Average(r => r.Stars);

                return (true, existingReview != null ? "Đã cập nhật lại đánh giá của bạn!" : "Tuyệt vời! Cảm ơn bạn đã đánh giá!", newAvgRating.ToString("0.0"));
            }
            catch (Exception ex)
            {
                var innerMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return (false, "Lỗi Database: " + innerMessage, "");
            }
        }

        public async Task<(bool Success, string Message, string Action, CafeChain.Models.Enums.Customer.ReactionType Type, int TotalCount)> ToggleReactionAsync(
            int ratingId, int customerId, CafeChain.Models.Enums.Customer.ReactionType type)
        {
            try
            {
                var existingReaction = await _context.Set<RatingReaction>()
                    .FirstOrDefaultAsync(r => r.RatingId == ratingId && r.CustomerId == customerId);

                string actionResult = "";
                if (existingReaction != null)
                {
                    if (existingReaction.Type == type)
                    {
                        _context.Set<RatingReaction>().Remove(existingReaction);
                        actionResult = "removed";
                    }
                    else
                    {
                        existingReaction.Type = type;
                        existingReaction.CreatedAt = DateTime.Now;
                        _context.Set<RatingReaction>().Update(existingReaction);
                        actionResult = "updated";
                    }
                }
                else
                {
                    var newReaction = new RatingReaction
                    {
                        RatingId = ratingId,
                        CustomerId = customerId,
                        Type = type,
                        CreatedAt = DateTime.Now
                    };
                    _context.Set<RatingReaction>().Add(newReaction);
                    actionResult = "added";
                }

                await _context.SaveChangesAsync();

                var newCounts = await _context.Set<RatingReaction>()
                    .Where(r => r.RatingId == ratingId)
                    .GroupBy(r => true)
                    .Select(g => new { Total = g.Count() })
                    .FirstOrDefaultAsync();

                return (true, "", actionResult, type, newCounts?.Total ?? 0);
            }
            catch (Exception ex)
            {
                return (false, "Lỗi Database: " + ex.Message, "", type, 0);
            }
        }

        public async Task<(bool Success, string Message, int? RatingId, string? ImageUrl)> SubmitReplyAsync(
            int customerId, int parentRatingId, int drinkId, string? comment, Stream? imageStream, string? fileName, long? fileLength, string webRootPath)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var newReply = new Rating
                {
                    DrinkId = drinkId,
                    CustomerId = customerId,
                    ParentRatingId = parentRatingId,
                    Stars = 5,
                    Comment = comment,
                    CreatedAt = DateTime.Now,
                    IsDeleted = false
                };

                _context.Ratings.Add(newReply);
                await _context.SaveChangesAsync();

                string? imageUrl = null;
                if (imageStream != null && fileName != null && fileLength.HasValue && fileLength.Value > 0)
                {
                    if (fileLength.Value > 2 * 1024 * 1024)
                    {
                        await transaction.RollbackAsync();
                        return (false, "Kích thước ảnh tối đa là 2MB.", null, null);
                    }

                    var extension = Path.GetExtension(fileName).ToLower();
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
                    if (!allowedExtensions.Contains(extension))
                    {
                        await transaction.RollbackAsync();
                        return (false, "Chỉ chỉ chấp nhận file định dạng .jpg hoặc .png.", null, null);
                    }

                    var newFileName = Guid.NewGuid().ToString() + extension;
                    var uploadsFolder = Path.Combine(webRootPath, "images", "ratings");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                    var filePath = Path.Combine(uploadsFolder, newFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageStream.CopyToAsync(stream);
                    }

                    imageUrl = $"/images/ratings/{newFileName}";
                    
                    var newImage = new RatingImage
                    {
                        RatingId = newReply.RatingId,
                        ImageUrl = imageUrl,
                        CreatedAt = DateTime.Now
                    };
                    _context.Set<RatingImage>().Add(newImage);
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                return (true, "Đã gửi bình luận!", newReply.RatingId, imageUrl);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, "Lỗi hệ thống: " + ex.Message, null, null);
            }
        }

        public async Task<bool> CheckDrinkAvailabilityAsync(int drinkId, int storeId)
        {
            _logger.LogWarning(">>>>>> ĐÃ VÀO HÀM CHECK TỒN KHO CHO DRINK ID: {DrinkId} <<<<<<", drinkId);
            // 1. Kiểm tra Hardcode StoreId: Nếu storeId == 0, ép nó bằng 1 (StoreId mặc định)
            if (storeId <= 0) storeId = 1;

            string cacheKey = $"DrinkAvailability_{storeId}_{drinkId}";
            
            // 2. Kiểm tra Cache (Set 1s để debug theo FIX.md)
            if (_cache.TryGetValue(cacheKey, out bool isAvailable))
            {
                return isAvailable;
            }

            try
            {
                var requiredIngredients = new Dictionary<int, decimal>();
                var recipes = await _context.Recipes
                    .Include(r => r.RecipeDetails)
                    .Where(r => r.DrinkId == drinkId)
                    .ToListAsync();

                // Nếu không có công thức, coi như luôn sẵn sàng
                if (!recipes.Any()) return true;

                var ingredientCache = new Dictionary<int, CafeChain.Models.Inventories.Ingredient>();

                foreach (var recipe in recipes)
                {
                    await ProcessRecipeDetailsRecursiveAsync(recipe.RecipeDetails, 1, requiredIngredients, ingredientCache);
                }

                if (!requiredIngredients.Any()) return true;

                var ingredientIds = requiredIngredients.Keys.ToList();
                var inventories = await _context.StoreInventories
                    .Where(si => si.StoreId == storeId && ingredientIds.Contains(si.IngredientId))
                    .ToDictionaryAsync(si => si.IngredientId);

                isAvailable = true;
                foreach (var req in requiredIngredients)
                {
                    int ingredientId = req.Key;
                    decimal requiredQty = req.Value;

                    if (!inventories.TryGetValue(ingredientId, out var inv))
                    {
                        // 1. Ghi log cụ thể khi thiếu bản ghi (Missing Inventory Record)
                        _logger.LogWarning("Missing Inventory Record: StoreId {StoreId} does not have an entry for IngredientId {IngredientId}", storeId, ingredientId);
                        
                        // 2. Cơ chế Bỏ qua nguyên liệu phụ (Chỉ dành cho môi trường Dev)
                        if (_env.IsDevelopment())
                        {
                            _logger.LogInformation("[DEV BYPASS] Missing inventory record for IngredientId {IngredientId} - Bypassing sold out check.", ingredientId);
                            continue; // Coi như còn hàng ở môi trường Dev
                        }

                        isAvailable = false;
                        break;
                    }
                    
                    if (inv.AvailableQty < requiredQty)
                    {
                        _logger.LogWarning("[SOLD OUT DETECTED] DrinkId: {DrinkId} | StoreId: {StoreId} | Failed at IngredientId: {IngredientId} | Required: {Required} | Available: {Available}", 
                            drinkId, storeId, ingredientId, requiredQty, inv.AvailableQty);
                        isAvailable = false;
                        break;
                    }
                }

                // Lưu cache 1 giây để phục vụ debug live
                _cache.Set(cacheKey, isAvailable, TimeSpan.FromSeconds(1));
                return isAvailable;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking availability for DrinkId {DrinkId} at StoreId {StoreId}", drinkId, storeId);
                return false;
            }
        }

        private async Task ProcessRecipeDetailsRecursiveAsync(
            IEnumerable<RecipeDetail> details,
            int multiplier,
            Dictionary<int, decimal> result,
            Dictionary<int, CafeChain.Models.Inventories.Ingredient> ingredientCache,
            int depth = 0)
        {
            // 4. Xử lý Đệ quy an toàn: Chống lặp vô tận (max 10 level)
            if (depth > 10) return;

            foreach (var detail in details)
            {
                if (detail.IngredientId.HasValue)
                {
                    int ingredientId = detail.IngredientId.Value;
                    if (!ingredientCache.TryGetValue(ingredientId, out var ingredient))
                    {
                        ingredient = await _context.Ingredients
                            .Include(i => i.UnitConversions)
                            .FirstOrDefaultAsync(i => i.IngredientId == ingredientId);

                        if (ingredient != null) ingredientCache[ingredientId] = ingredient;
                    }

                    decimal quantityInBaseUnit = detail.Quantity;

                    // 3. Rà soát Quy đổi đơn vị (Unit Conversion)
                    if (ingredient != null && detail.UnitId != ingredient.BaseUnitId)
                    {
                        var conversion = ingredient.UnitConversions
                            .FirstOrDefault(c => c.FromUnitId == detail.UnitId && c.ToUnitId == ingredient.BaseUnitId);

                        if (conversion != null && conversion.FromQuantity != 0)
                        {
                            quantityInBaseUnit = (detail.Quantity / conversion.FromQuantity) * conversion.ToQuantity;
                        }
                        else
                        {
                            _logger.LogWarning("[UNIT CONVERSION MISSING] IngredientId: {IngredientId} needs conversion from UnitId {FromUnit} to {ToBaseUnit}", 
                                ingredientId, detail.UnitId, ingredient.BaseUnitId);
                        }
                    }

                    if (result.ContainsKey(ingredientId))
                        result[ingredientId] += quantityInBaseUnit * multiplier;
                    else
                        result[ingredientId] = quantityInBaseUnit * multiplier;
                }
                else if (detail.ChildRecipeId.HasValue)
                {
                    var childRecipe = await _context.Recipes
                        .Include(r => r.RecipeDetails)
                        .FirstOrDefaultAsync(r => r.RecipeId == detail.ChildRecipeId.Value);

                    if (childRecipe != null)
                    {
                        await ProcessRecipeDetailsRecursiveAsync(childRecipe.RecipeDetails, multiplier, result, ingredientCache, depth + 1);
                    }
                }
            }
        }
    }
}