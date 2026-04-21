using CafeChain.Application.Interfaces.Admin.Recipes;
using CafeChain.Application.Results;
using CafeChain.ViewModels.Admin.Recipes;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using System;
using System.Collections.Generic;
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

        public async Task<ServiceResult> CreateRecipeAsync(RecipeCreateVM model)
        {
            if (model.Details == null || model.Details.Count == 0)
            {
                return ServiceResult.Failure("Công thức phải chứa ít nhất một thành phần (Details trống).");
            }

            // Mở Transaction (Toàn vẹn dữ liệu XOR và Batch Insert)
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
                    YieldPercentage = 100,
                    Active = model.Active,
                    RecipeDetails = new List<RecipeDetail>()
                };

                // Logic Phân rã ItemCode (MAPPING)
                foreach (var detailVM in model.Details)
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

                    // 1. Trường hợp là Nguyên Liệu Thô (Ingredient)
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
                    // 2. Trường hợp là Bán Thành Phẩm (Child Recipe)
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
                    // 3. Fallback Exception
                    else
                    {
                        throw new ArgumentException($"Định dạng ItemCode không hợp lệ: {detailVM.ItemCode}. Bắt buộc phải là tiền tố ING_ hoặc REC_.");
                    }

                    recipe.RecipeDetails.Add(detail);
                }

                // Thực thi Transaction DB
                await _context.Recipes.AddAsync(recipe);
                await _context.SaveChangesAsync();
                
                await transaction.CommitAsync();

                return ServiceResult.Success("Tạo mới công thức (BOM) thành công!");
            }
            catch (ArgumentException ex)
            {
                // Rollback ngay lập tức khi lỗi Validation Logic
                await transaction.RollbackAsync();
                return ServiceResult.Failure(ex.Message);
            }
            catch (Exception ex)
            {
                // Bắt lỗi rủi ro Server hoặc DB (VD: Lỗi Timeout, Mất mạng...)
                await transaction.RollbackAsync();
                return ServiceResult.Failure($"Đã xảy ra lỗi DB Hệ thống: {ex.Message}");
            }
        }
    }
}
