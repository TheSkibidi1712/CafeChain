using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CafeChain.Application.DTOs.Admin.StoreInventories;
using CafeChain.Infrastrusture.Repositories.Admin.StoreInventories;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Stores;
using Xunit;

namespace CafeChain.Tests.POS
{
    public sealed class PreparedItemInventoryIssue147Tests : IntegrationTestBase
    {
        [Fact]
        public async Task PreparedItemTab_FiltersBeforePaging_AndUsesCanonicalIdentity()
        {
            using var context = CreateDbContext();
            await SeedAsync(context);
            var repository = new AdminStoreInventoryRepository(context);

            var (prepared, preparedTotal) = await repository.GetPagedAsync(
                new List<int> { 147 },
                147,
                InventoryCatalogTypes.PreparedItems,
                null,
                1,
                10);
            var (ingredients, ingredientTotal) = await repository.GetPagedAsync(
                new List<int> { 147 },
                147,
                InventoryCatalogTypes.Ingredients,
                null,
                1,
                10);

            Assert.Equal(1, preparedTotal);
            var preparedRow = Assert.Single(prepared);
            Assert.Equal(1471, preparedRow.PreparedItemId);
            Assert.Equal("BTP-147", preparedRow.ItemCode);
            Assert.Equal(InventoryCatalogTypes.PreparedItems, preparedRow.ItemType);
            Assert.Equal("g", preparedRow.UnitCode);

            Assert.Equal(1, ingredientTotal);
            var ingredientRow = Assert.Single(ingredients);
            Assert.Null(ingredientRow.PreparedItemId);
            Assert.Equal("ING-147", ingredientRow.ItemCode);
        }

        [Fact]
        public async Task PreparedItemTab_UsesLatestActualCostLayerAndProductionSource()
        {
            using var context = CreateDbContext();
            await SeedAsync(context);
            var repository = new AdminStoreInventoryRepository(context);

            var (rows, _) = await repository.GetPagedAsync(
                new List<int> { 147 },
                147,
                InventoryCatalogTypes.PreparedItems,
                "BTP-147",
                1,
                10);

            var row = Assert.Single(rows);
            Assert.Equal(28.5m, row.LastUnitPrice);
            Assert.Equal("ACTUAL_LAYER", row.CostEvidenceStatus);
            Assert.Equal(14702, row.SourceProductionRunId);
            Assert.NotNull(row.LatestCostLayerId);
            Assert.Null(row.LastSupplierName);
        }

        [Fact]
        public void InventoryUi_PreservesTabStateAndLabelsActualCostEvidence()
        {
            var root = FindRepoRoot();
            var index = File.ReadAllText(Path.Combine(
                root,
                "CafeChain",
                "Areas",
                "Admin",
                "Views",
                "AdminStoreInventory",
                "Index.cshtml"));
            var table = File.ReadAllText(Path.Combine(
                root,
                "CafeChain",
                "Areas",
                "Admin",
                "Views",
                "AdminStoreInventory",
                "Partials",
                "_InventoryTablePartial.cshtml"));
            var paging = File.ReadAllText(Path.Combine(
                root,
                "CafeChain",
                "Areas",
                "Admin",
                "Views",
                "AdminStoreInventory",
                "Partials",
                "_PaginationPartial.cshtml"));

            Assert.Contains("Nguyên liệu", index, StringComparison.Ordinal);
            Assert.Contains("Bán thành phẩm", index, StringComparison.Ordinal);
            Assert.Contains("inventoryType", index, StringComparison.Ordinal);
            Assert.Contains("inventoryType = Model.ActiveTab", paging, StringComparison.Ordinal);
            Assert.Contains("Giá vốn thực tế", table, StringComparison.Ordinal);
            Assert.Contains("Lệnh sơ chế", table, StringComparison.Ordinal);
            Assert.Contains("bán thành phẩm vẫn là định danh tồn kho chính", table, StringComparison.Ordinal);
            Assert.DoesNotContain("compatibility", table, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("cost layer", table, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Giá vốn ước tính", table, StringComparison.Ordinal);
        }

        private static async Task SeedAsync(CafeChain.Data.AppDbContext context)
        {
            var now = DateTime.UtcNow;
            var unit = context.Units.FirstOrDefault(x => x.UnitCode == "g");
            if (unit == null)
            {
                unit = new Unit
                {
                    UnitCode = "g",
                    Name = "Gram",
                    Type = UnitType.KhoiLuong,
                    Active = true
                };
                context.Units.Add(unit);
                await context.SaveChangesAsync();
            }
            context.Stores.Add(new Store
            {
                StoreId = 147,
                Name = "Store 147",
                Address = "Test",
                Phone = "147",
                Active = true,
                CreatedAt = now
            });
            context.Ingredients.Add(new Ingredient
            {
                IngredientId = 1472,
                Code = "ING-147",
                Name = "Nguyên liệu 147",
                BaseUnitId = unit.UnitId,
                Active = true
            });
            context.PreparedItems.Add(new PreparedItem
            {
                PreparedItemId = 1471,
                Code = "BTP-147",
                Name = "BTP 147",
                BaseUnitId = unit.UnitId,
                Active = true
            });
            context.StoreInventories.AddRange(
                new StoreInventory
                {
                    StoreInventoryId = 1471,
                    StoreId = 147,
                    IngredientId = 1472,
                    AvailableQty = 500m,
                    ReservedQty = 20m,
                    LastUpdated = now,
                    RowVersion = new byte[] { 0 }
                },
                new StoreInventory
                {
                    StoreInventoryId = 1472,
                    StoreId = 147,
                    PreparedItemId = 1471,
                    AvailableQty = 120m,
                    ReservedQty = 15m,
                    LastUpdated = now,
                    RowVersion = new byte[] { 0 }
                });
            context.InventoryCostLayers.AddRange(
                new InventoryCostLayer
                {
                    InventoryCostLayerId = 1471,
                    StoreId = 147,
                    PreparedItemId = 1471,
                    Quantity = 100m,
                    RemainingQuantity = 10m,
                    UnitCost = 20m,
                    SourceProductionRunId = 14701,
                    CreatedAt = now.AddHours(-2)
                },
                new InventoryCostLayer
                {
                    InventoryCostLayerId = 1472,
                    StoreId = 147,
                    PreparedItemId = 1471,
                    Quantity = 100m,
                    RemainingQuantity = 100m,
                    UnitCost = 28.5m,
                    SourceProductionRunId = 14702,
                    CreatedAt = now.AddHours(-1)
                });
            await context.SaveChangesAsync();
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "CafeChain")))
                dir = dir.Parent;

            return dir?.FullName ?? throw new DirectoryNotFoundException("Không tìm thấy repo root.");
        }
    }
}
