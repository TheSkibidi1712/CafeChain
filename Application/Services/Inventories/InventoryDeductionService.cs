using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CafeChain.Application.Services.Inventories
{
    public class InventoryDeductionService : IInventoryDeductionService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<InventoryDeductionService> _logger;

        public InventoryDeductionService(AppDbContext context, ILogger<InventoryDeductionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<decimal> CalculateRecipeCogsAsync(int recipeId)
        {
            var recipe = await _context.Recipes
                .Include(r => r.RecipeDetails)
                .FirstOrDefaultAsync(r => r.RecipeId == recipeId);

            if (recipe == null) return 0;

            decimal totalCost = 0;

            foreach (var detail in recipe.RecipeDetails)
            {
                decimal detailCost = 0;

                if (detail.IngredientId.HasValue)
                {
                    // Lấy giá vốn từ Supplier (Mock: Lấy giá của Supplier chính hoặc đầu tiên)
                    var supplier = await _context.IngredientSuppliers
                        .Where(s => s.IngredientId == detail.IngredientId.Value)
                        .OrderByDescending(s => s.IsPrimary)
                        .FirstOrDefaultAsync();

                    decimal unitPrice = supplier?.Price ?? 0;
                    
                    // TODO: Apply Unit Conversion if supplier.UnitId != detail.UnitId
                    detailCost = unitPrice * detail.Quantity;
                }
                else if (detail.ChildRecipeId.HasValue)
                {
                    // Đệ quy tính giá vốn của Bán Thành Phẩm
                    decimal childCost = await CalculateRecipeCogsAsync(detail.ChildRecipeId.Value);
                    detailCost = childCost * detail.Quantity;
                }

                totalCost += detailCost;
            }

            // Tính toán tỷ lệ hao hụt (YieldPercentage)
            if (recipe.YieldPercentage > 0 && recipe.YieldPercentage < 100)
            {
                totalCost = totalCost / (recipe.YieldPercentage / 100m);
            }

            return totalCost;
        }

        public async Task<ServiceResult> DeductStockForOrderAsync(List<POSSoldItemDto> soldItems, int storeId)
        {
            if (soldItems == null || !soldItems.Any())
                return ServiceResult.Failure("Không có sản phẩm nào để xuất kho.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var item in soldItems)
                {
                    // Tìm công thức liên kết với Drink (Tạm thời map theo DrinkId, bỏ qua SizeId vì chưa có mapping Recipe theo Size)
                    var recipe = await _context.Recipes
                        .Include(r => r.RecipeDetails)
                        .FirstOrDefaultAsync(r => r.DrinkId == item.DrinkId && r.Active);

                    if (recipe == null)
                    {
                        _logger.LogWarning($"Không tìm thấy công thức (BOM) hoạt động cho DrinkId: {item.DrinkId}");
                        continue;
                    }

                    // STRICT OPTION B: Xuất thẳng Bán Thành Phẩm / Nguyên Liệu, KHÔNG bóc tách đệ quy
                    foreach (var detail in recipe.RecipeDetails)
                    {
                        decimal requiredQty = detail.Quantity * item.Quantity; // Tổng số lượng cần xuất (tính theo số lượng ly)
                        
                        // Xử lý Unit Conversion (Giả lập chuyển đổi nếu cần)
                        // decimal convertedQty = UnitConversionService.ConvertToStockUnit(requiredQty, detail.UnitId);
                        decimal convertedQty = requiredQty; 

                        // Tìm trong StoreInventory
                        var inventoryItem = await GetOrCreateInventoryItem(storeId, detail.IngredientId, detail.ChildRecipeId);

                        decimal beforeQty = inventoryItem.AvailableQty;
                        inventoryItem.AvailableQty -= convertedQty; // Cho phép âm kho (Soft-block)

                        // Ghi log InventoryTransaction
                        _context.InventoryTransactions.Add(new InventoryTransaction
                        {
                            StoreInventoryId = inventoryItem.StoreInventoryId,
                            Type = InventoryDocumentType.SALES_DEDUCTION,
                            Quantity = convertedQty,
                            BeforeQty = beforeQty,
                            AfterQty = inventoryItem.AvailableQty,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return ServiceResult.Success("Trừ kho bán hàng thành công.");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi tranh chấp dữ liệu khi trừ kho.");
                return ServiceResult.Failure("Lỗi hệ thống: Có nhiều giao dịch đồng thời đang tranh chấp kho. Vui lòng thử lại.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi xuất kho bán hàng.");
                return ServiceResult.Failure($"Lỗi xuất kho: {ex.Message}");
            }
        }

        private async Task<StoreInventory> GetOrCreateInventoryItem(int storeId, int? ingredientId, int? recipeId)
        {
            var item = await _context.StoreInventories
                .FirstOrDefaultAsync(i => i.StoreId == storeId && i.IngredientId == ingredientId && i.RecipeId == recipeId);
                
            if (item == null)
            {
                item = new StoreInventory 
                { 
                    StoreId = storeId, 
                    IngredientId = ingredientId, 
                    RecipeId = recipeId, 
                    AvailableQty = 0,
                    ReservedQty = 0,
                    LastUpdated = DateTime.UtcNow
                };
                _context.StoreInventories.Add(item);
                await _context.SaveChangesAsync(); // Cần Save ngay để có StoreInventoryId cho log Transaction
            }
            return item;
        }
    }
}
