using CafeChain.Application.Interfaces.Admin.Recipes;
using CafeChain.Application.Results;
using CafeChain.ViewModels.Admin.Recipes;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CafeChain.Application.Services.Admin.Recipes
{
    public class AdminRecipeService : IAdminRecipeService
    {
        private readonly AppDbContext _context;

        public AdminRecipeService(AppDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // CREATE: Giữ nguyên logic V3 ban đầu
        // ============================================================
        public async Task<ServiceResult> CreateRecipeAsync(RecipeCreateVM model)
        {
            if (model.Details == null || model.Details.Count == 0)
            {
                return ServiceResult.Failure("Công thức phải chứa ít nhất một thành phần (Details trống).");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // V3: Name resolution — no free-text allowed
                string recipeName;
                if (model.RecipeType == "POS" && model.DrinkId.HasValue)
                {
                    var drink = await _context.Drinks.FindAsync(model.DrinkId.Value);
                    recipeName = drink?.Name ?? $"POS_Recipe_{model.DrinkId}";
                }
                else if (model.RecipeType == "SUBRECIPE" && !string.IsNullOrWhiteSpace(model.SubRecipeName))
                {
                    recipeName = model.SubRecipeName;
                }
                else
                {
                    recipeName = $"Recipe_{DateTime.Now:yyyyMMdd_HHmmss}";
                }

                var recipe = new Recipe
                {
                    Name = recipeName,
                    DrinkId = model.RecipeType == "POS" ? model.DrinkId : null,
                    YieldPercentage = 100,
                    Active = model.Active,
                    Status = "Active",
                    EffectiveDate = model.EffectiveDate,
                    RecipeDetails = new List<RecipeDetail>()
                };

                // Phân rã ItemCode (MAPPING)
                foreach (var detailVM in model.Details)
                {
                    var detail = ParseRecipeDetail(detailVM);
                    recipe.RecipeDetails.Add(detail);
                }

                // Kiểm tra vòng lặp nếu có ChildRecipe
                var childRecipeIds = recipe.RecipeDetails
                    .Where(rd => rd.ChildRecipeId.HasValue)
                    .Select(rd => rd.ChildRecipeId.Value)
                    .ToList();

                if (childRecipeIds.Any())
                {
                    // Với bản mới (chưa có RecipeId), chỉ check self-reference
                    foreach (var childId in childRecipeIds)
                    {
                        if (await HasCircularDependencyAsync(childId, childId, new HashSet<int>()))
                        {
                            await transaction.RollbackAsync();
                            return ServiceResult.Failure($"Phát hiện vòng lặp công thức (Circular Dependency) trong bán thành phẩm #{childId}!");
                        }
                    }
                }

                await _context.Recipes.AddAsync(recipe);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return ServiceResult.Success("Tạo mới công thức (BOM) thành công!");
            }
            catch (ArgumentException ex)
            {
                await transaction.RollbackAsync();
                return ServiceResult.Failure(ex.Message);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ServiceResult.Failure($"Đã xảy ra lỗi DB Hệ thống: {ex.Message}");
            }
        }

        // ============================================================
        // UPDATE: Versioning — Soft-Delete cũ → Insert mới
        // NEVER OVERWRITE active records
        // ============================================================
        public async Task<ServiceResult> UpdateRecipeAsync(int recipeId, RecipeCreateVM model)
        {
            if (model.Details == null || model.Details.Count == 0)
            {
                return ServiceResult.Failure("Công thức phải chứa ít nhất một thành phần.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Tìm bản gốc đang Active
                var oldRecipe = await _context.Recipes
                    .Include(r => r.RecipeDetails)
                    .FirstOrDefaultAsync(r => r.RecipeId == recipeId && r.Status == "Active");

                if (oldRecipe == null)
                {
                    return ServiceResult.Failure("Không tìm thấy công thức hoặc công thức đã bị lưu trữ (Archived).");
                }

                // 2. Build danh sách ChildRecipeIds mới để kiểm tra vòng lặp
                var newChildRecipeIds = model.Details
                    .Where(d => !string.IsNullOrEmpty(d.ItemCode) && d.ItemCode.StartsWith("REC_"))
                    .Select(d => int.Parse(d.ItemCode.Substring(4)))
                    .ToList();

                // 3. DFS Anti-Loop: Kiểm tra xem recipe mới có tạo vòng lặp không
                foreach (var childId in newChildRecipeIds)
                {
                    // Nếu childRecipe trỏ ngược về recipeId → vòng lặp!
                    if (childId == recipeId)
                    {
                        return ServiceResult.Failure("Không thể thêm chính mình làm bán thành phẩm (Self-reference)!");
                    }

                    if (await HasCircularDependencyAsync(childId, recipeId, new HashSet<int>()))
                    {
                        return ServiceResult.Failure($"Phát hiện vòng lặp công thức! Bán thành phẩm #{childId} đã sử dụng công thức này làm thành phần.");
                    }
                }

                // 4. Soft-Delete bản cũ (NEVER OVERWRITE)
                oldRecipe.Status = "Archived";
                oldRecipe.Active = false;

                // 5. Tạo phiên bản MỚI với ParentVersionId trỏ về bản cũ
                string recipeName;
                if (model.RecipeType == "POS" && model.DrinkId.HasValue)
                {
                    var drink = await _context.Drinks.FindAsync(model.DrinkId.Value);
                    recipeName = drink?.Name ?? oldRecipe.Name;
                }
                else if (model.RecipeType == "SUBRECIPE" && !string.IsNullOrWhiteSpace(model.SubRecipeName))
                {
                    recipeName = model.SubRecipeName;
                }
                else
                {
                    recipeName = oldRecipe.Name;
                }

                var newRecipe = new Recipe
                {
                    Name = recipeName,
                    YieldPercentage = 100,
                    Active = model.Active,
                    Status = "Active",
                    EffectiveDate = model.EffectiveDate,
                    ParentVersionId = oldRecipe.RecipeId, // Audit Trail
                    DrinkId = oldRecipe.DrinkId,
                    ToppingId = oldRecipe.ToppingId,
                    RecipeDetails = new List<RecipeDetail>()
                };

                // 6. Map Details mới
                foreach (var detailVM in model.Details)
                {
                    var detail = ParseRecipeDetail(detailVM);
                    newRecipe.RecipeDetails.Add(detail);
                }

                await _context.Recipes.AddAsync(newRecipe);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return ServiceResult.Success($"Cập nhật công thức thành công! (Phiên bản mới #{newRecipe.RecipeId}, bản cũ #{oldRecipe.RecipeId} đã lưu trữ)");
            }
            catch (ArgumentException ex)
            {
                await transaction.RollbackAsync();
                return ServiceResult.Failure(ex.Message);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ServiceResult.Failure($"Lỗi hệ thống khi cập nhật: {ex.Message}");
            }
        }

        // ============================================================
        // DELETE: Kiểm tra ràng buộc → Soft/Hard Delete
        // ============================================================
        public async Task<ServiceResult> DeleteRecipeAsync(int recipeId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var recipe = await _context.Recipes
                    .Include(r => r.RecipeDetails)
                    .FirstOrDefaultAsync(r => r.RecipeId == recipeId);

                if (recipe == null)
                {
                    return ServiceResult.Failure("Không tìm thấy công thức cần xóa.");
                }

                // === RÀNG BUỘC 1: Kiểm tra xem Recipe có đang được dùng làm ChildRecipe ===
                bool isUsedAsSubRecipe = await _context.RecipeDetails
                    .AnyAsync(rd => rd.ChildRecipeId == recipeId
                                 && rd.Recipe.Status == "Active");

                if (isUsedAsSubRecipe)
                {
                    return ServiceResult.Failure(
                        "Không thể xóa! Bán thành phẩm này đang được sử dụng bởi công thức khác đang hoạt động.");
                }

                // === RÀNG BUỘC 2: Kiểm tra lịch sử OrderDetails (qua DrinkId/ToppingId) ===
                bool hasOrderHistory = false;

                if (recipe.DrinkId.HasValue)
                {
                    hasOrderHistory = await _context.OrderDetails
                        .AnyAsync(od => od.DrinkId == recipe.DrinkId.Value);
                }

                if (!hasOrderHistory && recipe.ToppingId.HasValue)
                {
                    hasOrderHistory = await _context.OrderToppings
                        .AnyAsync(ot => ot.ToppingId == recipe.ToppingId.Value);
                }

                // === RÀNG BUỘC 3: Kiểm tra ProductionOrders (nếu tồn tại) ===
                // ProductionOrder entity chưa có DbSet formal → kiểm tra an toàn qua try/catch
                // Khi entity được migration chính thức, bỏ comment dòng dưới
                // bool hasProductionOrders = await _context.Set<ProductionOrder>()
                //     .AnyAsync(po => po.TargetRecipeId == recipeId);
                // hasOrderHistory = hasOrderHistory || hasProductionOrders;

                if (hasOrderHistory)
                {
                    // SOFT DELETE — giữ lại dữ liệu lịch sử
                    recipe.Status = "Archived";
                    recipe.Active = false;
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return ServiceResult.Success(
                        $"Công thức #{recipeId} đã được lưu trữ (Archived) vì có liên kết lịch sử đơn hàng.");
                }
                else
                {
                    // HARD DELETE — không có ràng buộc nào
                    _context.RecipeDetails.RemoveRange(recipe.RecipeDetails);
                    _context.Recipes.Remove(recipe);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return ServiceResult.Success($"Đã xóa hoàn toàn công thức #{recipeId}.");
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ServiceResult.Failure($"Lỗi hệ thống khi xóa: {ex.Message}");
            }
        }

        // ============================================================
        // PRIVATE: DFS Anti-Loop Detection (Phát hiện vòng lặp đệ quy)
        // ============================================================
        /// <summary>
        /// Kiểm tra xem từ currentRecipeId, có đường đi nào dẫn tới targetRecipeId không (DFS).
        /// Nếu có → Circular Dependency!
        /// </summary>
        private async Task<bool> HasCircularDependencyAsync(int currentRecipeId, int targetRecipeId, HashSet<int> visited)
        {
            // Tránh duyệt lặp (đã thăm node này)
            if (!visited.Add(currentRecipeId))
                return false;

            // Lấy tất cả ChildRecipeId của currentRecipeId
            var childRecipeIds = await _context.RecipeDetails
                .Where(rd => rd.RecipeId == currentRecipeId && rd.ChildRecipeId.HasValue)
                .Select(rd => rd.ChildRecipeId.Value)
                .ToListAsync();

            foreach (var childId in childRecipeIds)
            {
                // Nếu con trực tiếp chính là target → vòng lặp!
                if (childId == targetRecipeId)
                    return true;

                // Đệ quy sâu hơn
                if (await HasCircularDependencyAsync(childId, targetRecipeId, visited))
                    return true;
            }

            return false;
        }

        // ============================================================
        // PRIVATE: Parse ItemCode → RecipeDetail (DRY cho Create + Update)
        // ============================================================
        private RecipeDetail ParseRecipeDetail(RecipeDetailVM detailVM)
        {
            var detail = new RecipeDetail
            {
                Quantity = detailVM.Quantity,
                UnitId = detailVM.UnitId
            };

            if (string.IsNullOrWhiteSpace(detailVM.ItemCode))
            {
                throw new ArgumentException("Mã cấu trúc thành phần (ItemCode) bị rỗng.");
            }

            // 1. Nguyên Liệu Thô (Ingredient)
            if (detailVM.ItemCode.StartsWith("ING_"))
            {
                if (int.TryParse(detailVM.ItemCode.Substring(4), out int ingredientId))
                {
                    detail.IngredientId = ingredientId;
                    detail.ChildRecipeId = null; // Đảm bảo XOR
                }
                else
                {
                    throw new ArgumentException($"Mã tham chiếu nguyên liệu không hợp lệ: {detailVM.ItemCode}");
                }
            }
            // 2. Bán Thành Phẩm (Child Recipe)
            else if (detailVM.ItemCode.StartsWith("REC_"))
            {
                if (int.TryParse(detailVM.ItemCode.Substring(4), out int childRecipeId))
                {
                    detail.ChildRecipeId = childRecipeId;
                    detail.IngredientId = null; // Đảm bảo XOR
                }
                else
                {
                    throw new ArgumentException($"Mã tham chiếu công thức con không hợp lệ: {detailVM.ItemCode}");
                }
            }
            // 3. Fallback
            else
            {
                throw new ArgumentException($"Định dạng ItemCode không hợp lệ: {detailVM.ItemCode}. Bắt buộc tiền tố ING_ hoặc REC_.");
            }

            return detail;
        }
    }
}
