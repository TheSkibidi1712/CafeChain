// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Linq;
// using System.Threading.Tasks;
// using CafeChain.Application.DTOs.Admin.Production;
// using CafeChain.Application.DTOs.Inventories;
// using CafeChain.Application.Interfaces.Inventories;
// using CafeChain.Application.Results;
// using CafeChain.Application.Services.Admin.Production;
// using CafeChain.Application.Services.Admin.Recipes;
// using CafeChain.Application.Services.Inventories;
// using CafeChain.Models.Drinks;
// using CafeChain.Models.Enums.Inventory;
// using CafeChain.Models.Inventories.Costing;
// using CafeChain.Models.Inventories.Ingredients;
// using CafeChain.Models.Inventories.PreparedItems;
// using CafeChain.Models.Stores;
// using Microsoft.Extensions.Logging.Abstractions;
// using Moq;
// using Xunit;

// namespace CafeChain.Tests.POS
// {
//     public sealed class ProductionReadinessIssue148Tests : IntegrationTestBase
//     {
//         [Fact]
//         public async Task Preview_NormalizesOutputAndInputs_UsesStoreUsableStockAndDoesNotMutateInventory()
//         {
//             using var context = CreateDbContext();
//             var fixture = await SeedProductionFixtureAsync(context, ingredientAvailable: 1500m);
//             var service = CreateService(context, InventoryWriterMode.PreparedItem);

//             var result = await service.PreviewAsync(fixture.StoreId, fixture.RootRecipeId, 2m);

//             Assert.True(result.IsSuccess);
//             var preview = Assert.IsType<ProductionReadinessPreviewDto>(result.Data);
//             Assert.True(preview.IsReady);
//             Assert.Equal(2m, preview.OutputQuantityPerRun);
//             Assert.Equal("kg", preview.OutputUnitCode);
//             Assert.Equal(4m, preview.RawTotalOutput);
//             Assert.Equal(4000m, preview.NormalizedTotalOutput);
//             Assert.Equal("g", preview.OutputBaseUnitCode);
//             Assert.Equal(2.8m, preview.MaxSupportedRunCount);
//             Assert.True(preview.CostEvidenceComplete);
//             Assert.Equal(3000m, preview.ProjectedFifoInputCost);

//             var ingredient = preview.Inputs.Single(x => x.IngredientId == fixture.IngredientId);
//             Assert.Equal(500m, ingredient.RequiredPerRun);
//             Assert.Equal(1000m, ingredient.RequiredTotal);
//             Assert.Equal(1500m, ingredient.CurrentQuantity);
//             Assert.Equal(100m, ingredient.ReservedQuantity);
//             Assert.Equal(1400m, ingredient.UsableQuantity);
//             Assert.Equal(0m, ingredient.ShortageQuantity);

//             var child = preview.Inputs.Single(x => x.PreparedItemId == fixture.ChildPreparedItemId);
//             Assert.Equal(fixture.ChildRecipeId, child.ChildRecipeId);
//             Assert.Equal(100m, child.RequiredPerRun);
//             Assert.Equal(200m, child.RequiredTotal);
//             Assert.Equal(350m, child.UsableQuantity);

//             Assert.Equal(1500m, context.StoreInventories.Single(x => x.IngredientId == fixture.IngredientId).AvailableQty);
//             Assert.Equal(400m, context.StoreInventories.Single(x => x.PreparedItemId == fixture.ChildPreparedItemId).AvailableQty);
//             Assert.Equal(1000m, context.InventoryCostLayers.Single(x => x.IngredientId == fixture.IngredientId).RemainingQuantity);
//             Assert.Equal(200m, context.InventoryCostLayers.Single(x => x.PreparedItemId == fixture.ChildPreparedItemId).RemainingQuantity);
//         }

//         [Fact]
//         public async Task Preview_ReportsIngredientShortageAndMaxRuns_FromAvailableMinusReserved()
//         {
//             using var context = CreateDbContext();
//             var fixture = await SeedProductionFixtureAsync(context, ingredientAvailable: 600m);
//             var service = CreateService(context, InventoryWriterMode.PreparedItem);

//             var result = await service.PreviewAsync(fixture.StoreId, fixture.RootRecipeId, 2m);

//             var preview = Assert.IsType<ProductionReadinessPreviewDto>(result.Data);
//             Assert.False(preview.IsReady);
//             Assert.Equal(1m, preview.MaxSupportedRunCount);
//             var ingredient = preview.Inputs.Single(x => x.IngredientId == fixture.IngredientId);
//             Assert.Equal(500m, ingredient.UsableQuantity);
//             Assert.Equal(500m, ingredient.ShortageQuantity);
//             Assert.Contains(preview.Reasons, x =>
//                 x.Code == ProductionReadinessCodes.IngredientShortage
//                 && x.Blocking);
//         }

//         [Fact]
//         public async Task Preview_BlocksWhenWriterModeIsNotPreparedItem()
//         {
//             using var context = CreateDbContext();
//             var fixture = await SeedProductionFixtureAsync(context, ingredientAvailable: 1500m);
//             var service = CreateService(context, InventoryWriterMode.LegacyRecipe);

//             var preview = (await service.PreviewAsync(fixture.StoreId, fixture.RootRecipeId, 1m)).Data;

//             Assert.NotNull(preview);
//             Assert.False(preview!.IsReady);
//             Assert.Equal("LegacyRecipe", preview.WriterMode);
//             Assert.Contains(preview.Reasons, x =>
//                 x.Code == ProductionReadinessCodes.WriterMode);
//         }

//         [Fact]
//         public async Task RecipeOptions_ExcludePosAndTopping_AndDisableInvalidBtpOutputContract()
//         {
//             using var context = CreateDbContext();
//             var fixture = await SeedProductionFixtureAsync(context, ingredientAvailable: 1500m);
//             var drinkId = context.Drinks.Select(x => x.DrinkId).First();
//             var toppingId = context.Toppings.Select(x => x.ToppingId).First();
//             context.Recipes.AddRange(
//                 new Recipe
//                 {
//                     RecipeId = 14850,
//                     RecipeCode = "POS-148",
//                     Name = "POS không được chọn 148",
//                     DrinkId = drinkId,
//                     Active = true,
//                     Status = "Active"
//                 },
//                 new Recipe
//                 {
//                     RecipeId = 14851,
//                     RecipeCode = "TOP-148",
//                     Name = "Topping không được chọn 148",
//                     ToppingId = toppingId,
//                     Active = true,
//                     Status = "Active"
//                 },
//                 new Recipe
//                 {
//                     RecipeId = 14852,
//                     RecipeCode = "BTP-INVALID-148",
//                     Name = "BTP thiếu output 148",
//                     Active = true,
//                     Status = "Active"
//                 });
//             await context.SaveChangesAsync();

//             var options = await CreateService(context, InventoryWriterMode.PreparedItem).GetRecipeOptionsAsync();

//             Assert.DoesNotContain(options, x => x.RecipeId == 14850);
//             Assert.DoesNotContain(options, x => x.RecipeId == 14851);
//             Assert.Contains(options, x => x.RecipeId == fixture.RootRecipeId && x.Selectable);
//             Assert.Contains(options, x => x.RecipeId == 14852 && !x.Selectable
//                 && x.DisabledReason!.Contains("PreparedItem", StringComparison.Ordinal));
//         }

//         [Fact]
//         public void ProductionView_UsesStoreReadinessAndKeepsConfirmSeparateFromStockExecution()
//         {
//             var root = FindRepoRoot();
//             var view = File.ReadAllText(Path.Combine(
//                 root, "CafeChain", "Areas", "Admin", "Views", "AdminProductionOrder", "Create.cshtml"));
//             var service = File.ReadAllText(Path.Combine(
//                 root, "CafeChain", "Application", "Services", "Admin", "Production", "ProductionReadinessService.cs"));

//             Assert.Contains("id=\"storeSelect\"", view, StringComparison.Ordinal);
//             Assert.Contains("Chỉ hiển thị công thức BTP Active", view, StringComparison.Ordinal);
//             Assert.Contains("normalizedTotalOutput", view, StringComparison.Ordinal);
//             Assert.Contains("currentQuantity", view, StringComparison.Ordinal);
//             Assert.Contains("reservedQuantity", view, StringComparison.Ordinal);
//             Assert.Contains("usableQuantity", view, StringComparison.Ordinal);
//             Assert.Contains("projectedFifoInputCost", view, StringComparison.Ordinal);
//             Assert.Contains("previewReady", view, StringComparison.Ordinal);
//             Assert.Contains("ExecuteStock", view, StringComparison.Ordinal);
//             Assert.Contains("ghi nhận lệnh", view, StringComparison.OrdinalIgnoreCase);
//             Assert.Contains("Read-only readiness projection", service, StringComparison.Ordinal);
//             Assert.DoesNotContain("estimatedOutput = batches", view, StringComparison.Ordinal);
//             Assert.DoesNotContain("const stockVisual = '---'", view, StringComparison.Ordinal);
//         }

//         private static ProductionReadinessService CreateService(
//             CafeChain.Data.AppDbContext context,
//             InventoryWriterMode mode)
//         {
//             var physical = new PhysicalUnitConversionService(
//                 context,
//                 NullLogger<PhysicalUnitConversionService>.Instance);
//             var conversion = new UnitConversionService(
//                 context,
//                 NullLogger<UnitConversionService>.Instance,
//                 physical);
//             var normalizer = new RecipeOutputNormalizer(context, physical);
//             var cost = new EstimatedBomCostService(
//                 context,
//                 conversion,
//                 physical,
//                 normalizer,
//                 NullLogger<EstimatedBomCostService>.Instance);
//             var writerMode = new Mock<IInventoryWriterModeService>();
//             writerMode.Setup(x => x.GetStatusAsync(It.IsAny<int>()))
//                 .ReturnsAsync((int storeId) => ServiceResult<InventoryWriterModeStatusDto>.Success(new InventoryWriterModeStatusDto
//                 {
//                     StoreId = storeId,
//                     WriterMode = mode,
//                     RowVersion = new byte[] { 1 },
//                     UpdatedAt = DateTime.UtcNow
//                 }));

//             return new ProductionReadinessService(
//                 context,
//                 normalizer,
//                 conversion,
//                 physical,
//                 writerMode.Object,
//                 cost,
//                 new IInventoryWriterCapabilityProvider[] { new ProductionPreparedWriterCapabilityProvider() });
//         }

//         private static async Task<ProductionFixture> SeedProductionFixtureAsync(
//             CafeChain.Data.AppDbContext context,
//             decimal ingredientAvailable)
//         {
//             var gram = context.Units.First(x => x.UnitCode == "g");
//             var kilogram = context.Units.First(x => x.UnitCode == "kg");
//             var store = new Store
//             {
//                 StoreId = 14801,
//                 Name = "Cửa hàng readiness 148",
//                 Address = "Test",
//                 Phone = "000",
//                 Active = true,
//                 CreatedAt = DateTime.UtcNow
//             };
//             var ingredient = new Ingredient
//             {
//                 IngredientId = 14801,
//                 Code = "ING-148",
//                 Name = "Nguyên liệu readiness 148",
//                 BaseUnitId = gram.UnitId,
//                 Active = true
//             };
//             var outputPi = new PreparedItem
//             {
//                 PreparedItemId = 14801,
//                 Code = "PI-OUTPUT-148",
//                 Name = "BTP đầu ra 148",
//                 BaseUnitId = gram.UnitId,
//                 Active = true
//             };
//             var childPi = new PreparedItem
//             {
//                 PreparedItemId = 14802,
//                 Code = "PI-CHILD-148",
//                 Name = "BTP đầu vào 148",
//                 BaseUnitId = gram.UnitId,
//                 Active = true
//             };
//             var childRecipe = new Recipe
//             {
//                 RecipeId = 14802,
//                 RecipeCode = "RCP-CHILD-148",
//                 Name = "Recipe BTP con 148",
//                 PreparedItemId = childPi.PreparedItemId,
//                 OutputQuantity = 100m,
//                 OutputUnitId = gram.UnitId,
//                 Active = false,
//                 Status = "Archived"
//             };
//             var rootRecipe = new Recipe
//             {
//                 RecipeId = 14801,
//                 RecipeCode = "RCP-ROOT-148",
//                 Name = "Recipe sản xuất 148",
//                 PreparedItemId = outputPi.PreparedItemId,
//                 OutputQuantity = 2m,
//                 OutputUnitId = kilogram.UnitId,
//                 Active = true,
//                 Status = "Active",
//                 RecipeDetails = new List<RecipeDetail>
//                 {
//                     new()
//                     {
//                         IngredientId = ingredient.IngredientId,
//                         Quantity = 0.5m,
//                         UnitId = kilogram.UnitId
//                     },
//                     new()
//                     {
//                         ChildRecipeId = childRecipe.RecipeId,
//                         Quantity = 100m,
//                         UnitId = gram.UnitId
//                     }
//                 }
//             };

//             context.Stores.Add(store);
//             context.Ingredients.Add(ingredient);
//             context.PreparedItems.AddRange(outputPi, childPi);
//             context.Recipes.AddRange(childRecipe, rootRecipe);
//             context.StoreInventories.AddRange(
//                 new StoreInventory
//                 {
//                     StoreInventoryId = 14801,
//                     StoreId = store.StoreId,
//                     IngredientId = ingredient.IngredientId,
//                     AvailableQty = ingredientAvailable,
//                     ReservedQty = 100m,
//                     LastUpdated = DateTime.UtcNow,
//                     RowVersion = new byte[] { 1 }
//                 },
//                 new StoreInventory
//                 {
//                     StoreInventoryId = 14802,
//                     StoreId = store.StoreId,
//                     PreparedItemId = childPi.PreparedItemId,
//                     BtpIdentityState = BtpIdentityState.Canonical,
//                     QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
//                     AvailableQty = 400m,
//                     ReservedQty = 50m,
//                     LastUpdated = DateTime.UtcNow,
//                     RowVersion = new byte[] { 1 }
//                 });
//             context.InventoryCostLayers.AddRange(
//                 new InventoryCostLayer
//                 {
//                     InventoryCostLayerId = 14801,
//                     StoreId = store.StoreId,
//                     IngredientId = ingredient.IngredientId,
//                     Quantity = 1000m,
//                     RemainingQuantity = 1000m,
//                     UnitCost = 2m,
//                     CreatedAt = DateTime.UtcNow.AddMinutes(-2)
//                 },
//                 new InventoryCostLayer
//                 {
//                     InventoryCostLayerId = 14802,
//                     StoreId = store.StoreId,
//                     PreparedItemId = childPi.PreparedItemId,
//                     Quantity = 200m,
//                     RemainingQuantity = 200m,
//                     UnitCost = 5m,
//                     CreatedAt = DateTime.UtcNow.AddMinutes(-1)
//                 });
//             await context.SaveChangesAsync();

//             return new ProductionFixture(
//                 store.StoreId,
//                 rootRecipe.RecipeId,
//                 childRecipe.RecipeId,
//                 ingredient.IngredientId,
//                 childPi.PreparedItemId);
//         }

//         private static string FindRepoRoot()
//         {
//             var dir = new DirectoryInfo(AppContext.BaseDirectory);
//             while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "CafeChain")))
//                 dir = dir.Parent;
//             return dir?.FullName ?? throw new DirectoryNotFoundException("Không tìm thấy repo root.");
//         }

//         private sealed record ProductionFixture(
//             int StoreId,
//             int RootRecipeId,
//             int ChildRecipeId,
//             int IngredientId,
//             int ChildPreparedItemId);
//     }
// }
