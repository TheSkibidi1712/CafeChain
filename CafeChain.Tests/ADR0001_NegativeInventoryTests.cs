using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace CafeChain.Tests.ADR0001_BlindSelling
{
    /// <summary>
    /// ADR-0001 Integration Tests — Chứng minh Blind Selling + Negative Inventory hoạt động đúng.
    /// 
    /// Luồng test trực tiếp trên InventoryDeductionService.DeductStockForOrderAsync:
    ///   1. Dùng Unit + Ingredient đã được EnsureCreated seed sẵn (HasData)
    ///   2. Seed Recipe + RecipeDetail (BOM 1 tầng: Drink → Ingredient)
    ///   3. Seed StoreInventory với AvailableQty cụ thể
    ///   4. Gọi DeductStockForOrderAsync → trừ kho
    ///   5. Assert: AvailableQty sau deduction + InventoryTransaction log
    /// 
    /// Seed data đã tồn tại từ EnsureCreated (HasData):
    ///   - UnitId=1: "Gram" (BaseUnit)
    ///   - IngredientId=9: "Matcha Nhật Bản 500g" (BaseUnitId=1 → gram → no conversion)
    /// 
    /// Strategy:
    ///   - InventoryDeductionService: REAL — dùng AppDbContext SQLite
    ///   - Không mock bất kỳ dependency nào — full integration test
    /// </summary>
    public class NegativeInventoryTests : IntegrationTestBase
    {
        private readonly InventoryDeductionService _service;
        private readonly AppDbContext _context;

        // Hằng số: dùng data đã seed sẵn từ HasData configurations
        private const int TEST_STORE_ID = 1;
        private const int TEST_DRINK_ID = 100;       // Drink mới (chưa tồn tại trong seed)
        private const int MATCHA_INGREDIENT_ID = 9;   // "Matcha Nhật Bản 500g" — HasData seed
        private const int TAPIOCA_INGREDIENT_ID = 11; // "Bột năng Vĩnh Thuận 400g" — HasData seed
        private const int GRAM_UNIT_ID = 1;            // "Gram" — HasData seed, = BaseUnitId
        private const int SIZE_L_ID = 3;
        private const int PEARL_TOPPING_ID = 99;      // Topping test riêng để không dùng nhầm recipe seed

        public NegativeInventoryTests()
        {
            _context = CreateDbContext();

            var logger = new Mock<ILogger<InventoryDeductionService>>();
            _service = new InventoryDeductionService(_context, logger.Object);

            // Seed Recipe cho Drink=100 (dùng Ingredient=9 đã có sẵn từ HasData)
            SeedRecipe(_context);
        }

        // ═══════════════════════════════════════════════════════════
        // SEED: Recipe (BOM 1 tầng) — dùng Unit + Ingredient có sẵn
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Tạo BOM đơn giản:
        ///   Drink "Matcha Latte Test" (DrinkId=100)
        ///     └─ Recipe "BOM Matcha Test" (Active)
        ///         └─ RecipeDetail: IngredientId=9 "Matcha Nhật Bản" × 2 gram/ly
        /// 
        /// UnitId=1 (Gram) = BaseUnitId → ConvertQuantityToBaseUnitAsync trả nguyên bản
        /// </summary>
        private void SeedRecipe(AppDbContext ctx)
        {
            if (!ctx.Recipes.Any(r => r.DrinkId == TEST_DRINK_ID && r.Active))
            {
                var recipe = new Recipe
                {
                    Name = "BOM Matcha Latte Test",
                    RecipeCode = "BOM-MATCHA-TEST",
                    DrinkId = TEST_DRINK_ID,
                    Active = true,
                    Status = "Active",
                    YieldPercentage = 100,
                    RecipeDetails = new List<RecipeDetail>
                    {
                        new RecipeDetail
                        {
                            IngredientId = MATCHA_INGREDIENT_ID,  // Matcha Nhật Bản (HasData seed)
                            Quantity = 2,                          // 2 gram / ly
                            UnitId = GRAM_UNIT_ID                  // Gram = BaseUnit → no conversion
                        }
                    }
                };
                ctx.Recipes.Add(recipe);
                ctx.SaveChanges();
            }
        }

        /// <summary>
        /// Seed StoreInventory cho Matcha Ingredient với AvailableQty cho trước.
        /// </summary>
        private void SeedInventory(AppDbContext ctx, int storeId, decimal availableQty)
        {
            SeedInventoryForIngredient(ctx, storeId, MATCHA_INGREDIENT_ID, availableQty);
        }

        private void SeedInventoryForIngredient(AppDbContext ctx, int storeId, int ingredientId, decimal availableQty)
        {
            var existing = ctx.StoreInventories
                .FirstOrDefault(si => si.StoreId == storeId && si.IngredientId == ingredientId);

            if (existing != null)
            {
                existing.AvailableQty = availableQty;
            }
            else
            {
                ctx.StoreInventories.Add(new StoreInventory
                {
                    StoreId = storeId,
                    IngredientId = ingredientId,
                    RecipeId = null,
                    AvailableQty = availableQty,
                    ReservedQty = 0,
                    LastUpdated = DateTime.UtcNow
                });
            }
            ctx.SaveChanges();
        }

        private void SeedSizedDrinkAndToppingRecipes(AppDbContext ctx)
        {
            if (!ctx.Recipes.Any(r => r.DrinkId == TEST_DRINK_ID && r.SizeId == SIZE_L_ID && r.Active))
            {
                ctx.Recipes.Add(new Recipe
                {
                    Name = "BOM Matcha Latte Size L Test",
                    RecipeCode = "BOM-MATCHA-L-TEST",
                    DrinkId = TEST_DRINK_ID,
                    SizeId = SIZE_L_ID,
                    Active = true,
                    Status = "Active",
                    YieldPercentage = 100,
                    RecipeDetails = new List<RecipeDetail>
                    {
                        new()
                        {
                            IngredientId = MATCHA_INGREDIENT_ID,
                            Quantity = 5,
                            UnitId = GRAM_UNIT_ID
                        }
                    }
                });
            }

            if (!ctx.Recipes.Any(r => r.ToppingId == PEARL_TOPPING_ID && r.Active))
            {
                ctx.Recipes.Add(new Recipe
                {
                    Name = "BOM Trân châu Test",
                    RecipeCode = "BOM-PEARL-TEST",
                    ToppingId = PEARL_TOPPING_ID,
                    Active = true,
                    Status = "Active",
                    YieldPercentage = 100,
                    RecipeDetails = new List<RecipeDetail>
                    {
                        new()
                        {
                            IngredientId = TAPIOCA_INGREDIENT_ID,
                            Quantity = 3,
                            UnitId = GRAM_UNIT_ID
                        }
                    }
                });
            }

            ctx.SaveChanges();
        }

        // ═══════════════════════════════════════════════════════════
        // TEST 6: 🟢 Kho đủ → trừ đúng
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public async Task DeductStock_WhenSufficientInventory_DeductsCorrectly()
        {
            // Arrange: Kho = 50 gram Matcha, bán 10 ly × 2 gram/ly = cần 20 gram
            SeedInventory(_context, TEST_STORE_ID, availableQty: 50);

            var soldItems = new List<POSSoldItemDto>
            {
                new() { DrinkId = TEST_DRINK_ID, Quantity = 10 }
            };

            // Act
            var result = await _service.DeductStockForOrderAsync(soldItems, TEST_STORE_ID);

            // Assert — Service trả success
            Assert.True(result.IsSuccess, $"DeductStock phải thành công. Message: {result.Message}");

            // Assert — AvailableQty = 50 - 20 = 30
            using var verifyCtx = CreateDbContext();
            var inventory = await verifyCtx.StoreInventories
                .FirstAsync(si => si.StoreId == TEST_STORE_ID && si.IngredientId == MATCHA_INGREDIENT_ID);
            Assert.Equal(30, inventory.AvailableQty);

            // Assert — InventoryTransaction log ghi nhận SALES_DEDUCTION
            var txn = await verifyCtx.InventoryTransactions
                .FirstOrDefaultAsync(t => t.StoreInventoryId == inventory.StoreInventoryId
                                       && t.Type == InventoryTransactionTypeEnum.SALES_DEDUCTION);
            Assert.NotNull(txn);
            Assert.Equal(-20, txn.Quantity);    // xuất kho: 10 ly × 2 gram = -20
            Assert.Equal(50, txn.BeforeQty);    // Kho trước khi trừ
            Assert.Equal(30, txn.AfterQty);     // Kho sau khi trừ
        }

        [Fact]
        public async Task DeductStock_WithSizeLAndPearlTopping_DeductsDrinkAndToppingRecipes()
        {
            // Arrange: Size L dùng 5g matcha, topping trân châu dùng 3g bột năng.
            SeedSizedDrinkAndToppingRecipes(_context);
            SeedInventoryForIngredient(_context, TEST_STORE_ID, MATCHA_INGREDIENT_ID, availableQty: 100);
            SeedInventoryForIngredient(_context, TEST_STORE_ID, TAPIOCA_INGREDIENT_ID, availableQty: 50);

            var soldItems = new List<POSSoldItemDto>
            {
                new()
                {
                    DrinkId = TEST_DRINK_ID,
                    SizeId = SIZE_L_ID,
                    Quantity = 1,
                    Toppings = new List<POSOrderToppingDto>
                    {
                        new() { ToppingId = PEARL_TOPPING_ID }
                    }
                }
            };

            // Act
            var result = await _service.DeductStockForOrderAsync(soldItems, TEST_STORE_ID);

            // Assert
            Assert.True(result.IsSuccess, $"DeductStock phải thành công. Message: {result.Message}");

            using var verifyCtx = CreateDbContext();
            var matchaInventory = await verifyCtx.StoreInventories
                .FirstAsync(si => si.StoreId == TEST_STORE_ID && si.IngredientId == MATCHA_INGREDIENT_ID);
            var tapiocaInventory = await verifyCtx.StoreInventories
                .FirstAsync(si => si.StoreId == TEST_STORE_ID && si.IngredientId == TAPIOCA_INGREDIENT_ID);

            Assert.Equal(95, matchaInventory.AvailableQty);
            Assert.Equal(47, tapiocaInventory.AvailableQty);

            var transactions = await verifyCtx.InventoryTransactions
                .Where(t => t.Type == InventoryTransactionTypeEnum.SALES_DEDUCTION)
                .ToListAsync();

            Assert.Contains(transactions, t =>
                t.StoreInventoryId == matchaInventory.StoreInventoryId &&
                t.Quantity == -5 &&
                t.BeforeQty == 100 &&
                t.AfterQty == 95);

            Assert.Contains(transactions, t =>
                t.StoreInventoryId == tapiocaInventory.StoreInventoryId &&
                t.Quantity == -3 &&
                t.BeforeQty == 50 &&
                t.AfterQty == 47);
        }

        // ═══════════════════════════════════════════════════════════
        // TEST 7: 🟢 Kho thiếu → cho phép âm (ADR-0001 CORE)
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public async Task DeductStock_WhenInsufficientInventory_AllowsNegative_BlindSelling()
        {
            // Arrange: Kho chỉ còn 3 gram Matcha, bán 10 ly × 2 gram/ly = cần 20 gram
            // → AvailableQty = 3 - 20 = -17 (cho phép âm theo ADR-0001)
            SeedInventory(_context, TEST_STORE_ID, availableQty: 3);

            var soldItems = new List<POSSoldItemDto>
            {
                new() { DrinkId = TEST_DRINK_ID, Quantity = 10 }
            };

            // Act
            var result = await _service.DeductStockForOrderAsync(soldItems, TEST_STORE_ID);

            // Assert — KHÔNG reject đơn (ADR-0001: Blind Selling cho phép kho âm)
            Assert.True(result.IsSuccess, $"ADR-0001: Phải cho phép kho âm. Message: {result.Message}");

            // Assert — AvailableQty = 3 - 20 = -17 (trạng thái chờ đối soát)
            using var verifyCtx = CreateDbContext();
            var inventory = await verifyCtx.StoreInventories
                .FirstAsync(si => si.StoreId == TEST_STORE_ID && si.IngredientId == MATCHA_INGREDIENT_ID);
            Assert.Equal(-17, inventory.AvailableQty);

            // Assert — InventoryTransaction log chứng minh kho âm có thể kiểm soát
            var txn = await verifyCtx.InventoryTransactions
                .FirstOrDefaultAsync(t => t.StoreInventoryId == inventory.StoreInventoryId
                                       && t.Type == InventoryTransactionTypeEnum.SALES_DEDUCTION);
            Assert.NotNull(txn);
            Assert.Equal(InventoryTransactionTypeEnum.SALES_DEDUCTION, txn.Type);
            Assert.Equal(-20, txn.Quantity);    // Lượng trừ: 10 ly × 2 gram
            Assert.Equal(3, txn.BeforeQty);     // Kho ban đầu: chỉ còn 3
            Assert.Equal(-17, txn.AfterQty);    // ⚠️ SỐ ÂM — trạng thái chờ đối soát
        }

        // ═══════════════════════════════════════════════════════════
        // TEST 8: 🟢 Drink không có BOM → bỏ qua, không crash
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public async Task DeductStock_WithNoDrinkRecipe_SkipsDeductionGracefully()
        {
            // Arrange: DrinkId=999 không có Recipe nào
            var soldItems = new List<POSSoldItemDto>
            {
                new() { DrinkId = 999, Quantity = 5 }
            };

            // Act
            var result = await _service.DeductStockForOrderAsync(soldItems, TEST_STORE_ID);

            // Assert — Service trả success (skip, không crash)
            Assert.True(result.IsSuccess, "Drink không có BOM phải được bỏ qua, không crash.");

            // Assert — Không tạo InventoryTransaction
            using var verifyCtx = CreateDbContext();
            var txnCount = await verifyCtx.InventoryTransactions.CountAsync();
            Assert.Equal(0, txnCount);
        }

        // ═══════════════════════════════════════════════════════════
        // TEST 9: 🟢 Store mới chưa có kho → tự tạo qty=0 rồi trừ xuống âm
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public async Task DeductStock_WhenInventoryNotExists_AutoCreatesWithZeroThenGoesNegative()
        {
            // Arrange: Store=99 chưa có StoreInventory nào cho Matcha Ingredient
            // KHÔNG seed inventory cho store 99
            const int NEW_STORE_ID = 99;

            var soldItems = new List<POSSoldItemDto>
            {
                new() { DrinkId = TEST_DRINK_ID, Quantity = 2 }  // 2 ly × 2 gram = 4 gram cần trừ
            };

            // Act
            var result = await _service.DeductStockForOrderAsync(soldItems, NEW_STORE_ID);

            // Assert — Service trả success
            Assert.True(result.IsSuccess, $"Phải tự tạo kho mới và cho phép âm. Message: {result.Message}");

            // Assert — GetOrCreateInventoryItem tạo mới qty=0, rồi trừ: 0 - 4 = -4
            using var verifyCtx = CreateDbContext();
            var inventory = await verifyCtx.StoreInventories
                .FirstOrDefaultAsync(si => si.StoreId == NEW_STORE_ID && si.IngredientId == MATCHA_INGREDIENT_ID);

            Assert.NotNull(inventory);
            Assert.Equal(-4, inventory.AvailableQty);   // 0 - (2 ly × 2 gram) = -4

            // Assert — InventoryTransaction log ghi nhận BeforeQty=0, AfterQty=-4
            var txn = await verifyCtx.InventoryTransactions
                .FirstOrDefaultAsync(t => t.StoreInventoryId == inventory.StoreInventoryId
                                       && t.Type == InventoryTransactionTypeEnum.SALES_DEDUCTION);
            Assert.NotNull(txn);
            Assert.Equal(InventoryTransactionTypeEnum.SALES_DEDUCTION, txn.Type);
            Assert.Equal(-4, txn.Quantity);     // Lượng trừ: 2 ly × 2 gram
            Assert.Equal(0, txn.BeforeQty);     // Kho vừa tạo mới = 0
            Assert.Equal(-4, txn.AfterQty);     // ⚠️ SỐ ÂM — store mới bán trước nhập kho sau
        }
    }
}
