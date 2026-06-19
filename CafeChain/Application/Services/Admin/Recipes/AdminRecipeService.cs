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

        // Giới hạn tối đa 5 tầng BOM để tránh StackOverflow
        private const int MAX_BOM_DEPTH = 5;

        public AdminRecipeService(AppDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // CREATE: Hardened V4 — Zero-Trust Validation
        // ============================================================
        public async Task<ServiceResult> CreateRecipeAsync(RecipeCreateVM model)
        {
            if (model.Details == null || model.Details.Count == 0)
            {
                return ServiceResult.Failure("Công thức phải chứa ít nhất một thành phần (Details trống).");
            }

            // === VALIDATION LAYER (Zero-Trust) ===
            var validationResult = ValidateDetails(model.Details);
            if (!validationResult.IsSuccess)
                return validationResult;

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Validate tồn tại trong DB
                var dbValidation = await ValidateDetailsExistInDbAsync(model.Details);
                if (!dbValidation.IsSuccess)
                {
                    await transaction.RollbackAsync();
                    return dbValidation;
                }

                // Validate UnitId hợp lệ cho từng Ingredient
                var unitValidation = await ValidateUnitMappingsAsync(model.Details);
                if (!unitValidation.IsSuccess)
                {
                    await transaction.RollbackAsync();
                    return unitValidation;
                }

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

                // Kiểm tra Depth Limit cho các ChildRecipe
                var childRecipeIds = recipe.RecipeDetails
                    .Where(rd => rd.ChildRecipeId.HasValue)
                    .Select(rd => rd.ChildRecipeId.Value)
                    .ToList();

                if (childRecipeIds.Any())
                {
                    foreach (var childId in childRecipeIds)
                    {
                        // Với bản CREATE mới (chưa có RecipeId), chỉ cần kiểm tra
                        // depth limit của cây con — recipe mới không thể bị tham chiếu ngược
                        var depthResult = await CheckDepthLimitAsync(childId, 1);
                        if (!depthResult.IsSuccess)
                        {
                            await transaction.RollbackAsync();
                            return depthResult;
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

            // === VALIDATION LAYER (Zero-Trust) ===
            var validationResult = ValidateDetails(model.Details);
            if (!validationResult.IsSuccess)
                return validationResult;

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Validate tồn tại trong DB
                var dbValidation = await ValidateDetailsExistInDbAsync(model.Details);
                if (!dbValidation.IsSuccess)
                {
                    await transaction.RollbackAsync();
                    return dbValidation;
                }

                // Validate UnitId hợp lệ cho từng Ingredient
                var unitValidation = await ValidateUnitMappingsAsync(model.Details);
                if (!unitValidation.IsSuccess)
                {
                    await transaction.RollbackAsync();
                    return unitValidation;
                }

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

                // 3. DFS Anti-Loop + Depth Limit
                foreach (var childId in newChildRecipeIds)
                {
                    // Self-reference check
                    if (childId == recipeId)
                    {
                        return ServiceResult.Failure("Không thể thêm chính mình làm bán thành phẩm (Self-reference)!");
                    }

                    // DFS: Kiểm tra childRecipe có đường đi ngược về recipeId không
                    if (await HasCircularDependencyAsync(childId, recipeId, new HashSet<int>(), 0))
                    {
                        return ServiceResult.Failure($"Phát hiện vòng lặp công thức! Bán thành phẩm #{childId} đã sử dụng công thức này làm thành phần.");
                    }

                    // Depth Limit: tầng hiện tại là 1 (recipe gốc = tầng 0)
                    var depthResult = await CheckDepthLimitAsync(childId, 1);
                    if (!depthResult.IsSuccess)
                    {
                        await transaction.RollbackAsync();
                        return depthResult;
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
        // PRIVATE: Validate Details — Quantity, YieldPercentage, trùng lặp
        // ============================================================
        private ServiceResult ValidateDetails(List<RecipeDetailVM> details)
        {
            foreach (var d in details)
            {
                // Quantity phải > 0
                if (d.Quantity <= 0)
                {
                    return ServiceResult.Failure(
                        $"Định lượng phải lớn hơn 0 (ItemCode: {d.ItemCode}, Quantity: {d.Quantity}).");
                }

                // YieldPercentage không được = 0 (Division by Zero)
                if (d.YieldPercentage == 0)
                {
                    return ServiceResult.Failure(
                        $"Tỷ lệ thu hồi không được = 0% — gây lỗi chia cho 0 (ItemCode: {d.ItemCode}).");
                }
            }

            // Kiểm tra trùng lặp ItemCode cùng cấp
            var duplicateItems = details
                .GroupBy(d => d.ItemCode)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateItems.Any())
            {
                return ServiceResult.Failure(
                    $"Trùng thành phần trong cùng một cấp BOM: {string.Join(", ", duplicateItems)}. Mỗi nguyên liệu/bán thành phẩm chỉ được xuất hiện 1 lần.");
            }

            return ServiceResult.Success();
        }

        // ============================================================
        // PRIVATE: Validate tồn tại trong DB (Ingredient/Recipe Active)
        // ============================================================
        private async Task<ServiceResult> ValidateDetailsExistInDbAsync(List<RecipeDetailVM> details)
        {
            foreach (var d in details)
            {
                if (string.IsNullOrWhiteSpace(d.ItemCode)) continue;

                if (d.ItemCode.StartsWith("ING_"))
                {
                    if (int.TryParse(d.ItemCode.Substring(4), out int ingId))
                    {
                        bool exists = await _context.Ingredients
                            .AnyAsync(i => i.IngredientId == ingId && i.Active);
                        if (!exists)
                        {
                            return ServiceResult.Failure(
                                $"Nguyên liệu #{ingId} không tồn tại hoặc đã ngưng hoạt động.");
                        }
                    }
                }
                else if (d.ItemCode.StartsWith("REC_"))
                {
                    if (int.TryParse(d.ItemCode.Substring(4), out int recId))
                    {
                        bool exists = await _context.Recipes
                            .AnyAsync(r => r.RecipeId == recId && r.Status == "Active");
                        if (!exists)
                        {
                            return ServiceResult.Failure(
                                $"Bán thành phẩm #{recId} không tồn tại hoặc đã lưu trữ (Archived).");
                        }
                    }
                }
            }

            return ServiceResult.Success();
        }

        // ============================================================
        // PRIVATE: Validate UnitId hợp lệ cho Ingredient
        // ============================================================
        private async Task<ServiceResult> ValidateUnitMappingsAsync(List<RecipeDetailVM> details)
        {
            foreach (var d in details)
            {
                if (string.IsNullOrWhiteSpace(d.ItemCode) || !d.ItemCode.StartsWith("ING_"))
                    continue; // Chỉ validate Unit cho Ingredient, SubRecipe dùng unit "Phần"

                if (!int.TryParse(d.ItemCode.Substring(4), out int ingId))
                    continue;

                var ingredient = await _context.Ingredients
                    .Include(i => i.UnitConversions)
                    .FirstOrDefaultAsync(i => i.IngredientId == ingId);

                if (ingredient == null) continue; // Đã validate ở bước trước

                // Tập hợp tất cả UnitId hợp lệ: BaseUnit + tất cả Unit trong UnitConversion
                var allowedUnitIds = ingredient.UnitConversions
                    .SelectMany(uc => new[] { uc.FromUnitId, uc.ToUnitId })
                    .Append(ingredient.BaseUnitId)
                    .Distinct()
                    .ToHashSet();

                if (!allowedUnitIds.Contains(d.UnitId))
                {
                    return ServiceResult.Failure(
                        $"Đơn vị tính (UnitId: {d.UnitId}) không hợp lệ cho nguyên liệu '{ingredient.Name}'. " +
                        $"Các đơn vị cho phép: [{string.Join(", ", allowedUnitIds)}].");
                }
            }

            return ServiceResult.Success();
        }

        // ============================================================
        // PRIVATE: DFS Anti-Loop Detection + Depth Limit
        // ============================================================
        /// <summary>
        /// Kiểm tra xem từ currentRecipeId, có đường đi nào dẫn tới targetRecipeId không (DFS).
        /// Nếu có → Circular Dependency! Giới hạn tối đa MAX_BOM_DEPTH tầng.
        /// </summary>
        private async Task<bool> HasCircularDependencyAsync(
            int currentRecipeId, int targetRecipeId, HashSet<int> visited, int currentDepth)
        {
            // Depth Limit — ngừng duyệt nếu vượt quá giới hạn
            if (currentDepth > MAX_BOM_DEPTH)
                return false; // Không phải vòng lặp, nhưng depth đã vượt (sẽ bị chặn bởi CheckDepthLimit)

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

                // Đệ quy sâu hơn với depth + 1
                if (await HasCircularDependencyAsync(childId, targetRecipeId, visited, currentDepth + 1))
                    return true;
            }

            return false;
        }

        // ============================================================
        // PRIVATE: Kiểm tra Depth Limit cho cây BOM
        // ============================================================
        /// <summary>
        /// Kiểm tra xem từ recipeId, cây BOM có vượt quá MAX_BOM_DEPTH tầng không.
        /// </summary>
        private async Task<ServiceResult> CheckDepthLimitAsync(int recipeId, int currentDepth)
        {
            if (currentDepth > MAX_BOM_DEPTH)
            {
                return ServiceResult.Failure(
                    $"Cây BOM vượt quá {MAX_BOM_DEPTH} tầng đệ quy (tại Recipe #{recipeId}). " +
                    $"Hệ thống giới hạn tối đa {MAX_BOM_DEPTH} cấp để đảm bảo hiệu suất.");
            }

            var childRecipeIds = await _context.RecipeDetails
                .Where(rd => rd.RecipeId == recipeId && rd.ChildRecipeId.HasValue)
                .Select(rd => rd.ChildRecipeId.Value)
                .ToListAsync();

            foreach (var childId in childRecipeIds)
            {
                var result = await CheckDepthLimitAsync(childId, currentDepth + 1);
                if (!result.IsSuccess)
                    return result;
            }

            return ServiceResult.Success();
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
