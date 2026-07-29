// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Linq;
// using System.Threading.Tasks;
// using CafeChain.Application.Services.Admin.PreparedItems;
// using CafeChain.Application.Services.Admin.Recipes;
// using CafeChain.Application.Services.Inventories;
// using CafeChain.Models.Drinks;
// using CafeChain.Models.Inventories.Ingredients;
// using CafeChain.Models.Inventories.PreparedItems;
// using CafeChain.ViewModels.Admin.Recipes;
// using Microsoft.Extensions.Logging.Abstractions;
// using Xunit;

// namespace CafeChain.Tests.POS
// {
//     public sealed class BomToppingConsumptionSourcesIssue149Tests : IntegrationTestBase
//     {
//         [Fact]
//         public async Task DirectIngredientSource_UsesRecipeDetailIdentityAndQuantity_NotMatchingPreparedItemName()
//         {
//             using var context = CreateDbContext();
//             var gram = context.Units.First(x => x.UnitCode == "g");
//             var topping = new Topping
//             {
//                 ToppingId = 14901,
//                 ToppingCode = "TOP-149-DIRECT",
//                 Name = "Trân châu thử nghiệm 149",
//                 Price = 9000m,
//                 Active = true
//             };
//             var sameNamePreparedItem = new PreparedItem
//             {
//                 PreparedItemId = 14901,
//                 Code = "PI-SAME-NAME-149",
//                 Name = topping.Name,
//                 BaseUnitId = gram.UnitId,
//                 Active = true
//             };
//             var ingredient = new Ingredient
//             {
//                 IngredientId = 14901,
//                 Code = "ING-DIRECT-149",
//                 Name = "Hạt trân châu khô 149",
//                 BaseUnitId = gram.UnitId,
//                 Active = true
//             };
//             context.Toppings.Add(topping);
//             context.PreparedItems.Add(sameNamePreparedItem);
//             context.Ingredients.Add(ingredient);
//             context.Recipes.Add(new Recipe
//             {
//                 RecipeId = 14901,
//                 RecipeCode = "RCP-TOP-DIRECT-149",
//                 Name = topping.Name,
//                 ToppingId = topping.ToppingId,
//                 Active = true,
//                 Status = "Active",
//                 YieldPercentage = 12m,
//                 RecipeDetails = new List<RecipeDetail>
//                 {
//                     new()
//                     {
//                         IngredientId = ingredient.IngredientId,
//                         Quantity = 18.5m,
//                         UnitId = gram.UnitId
//                     }
//                 }
//             });
//             await context.SaveChangesAsync();

//             var sources = await CreateQueryService(context)
//                 .GetToppingConsumptionSourcesAsync(new[] { topping.ToppingId });
//             var source = sources[topping.ToppingId];
//             var component = Assert.Single(source.Components);

//             Assert.Equal(ToppingConsumptionSourceCodes.DirectIngredient, source.SourceCode);
//             Assert.Equal("Nguồn nguyên liệu trực tiếp", source.SourceLabel);
//             Assert.True(source.MappingValid);
//             Assert.Equal(ingredient.IngredientId, component.IngredientId);
//             Assert.Null(component.PreparedItemId);
//             Assert.Equal(18.5m, component.Quantity);
//             Assert.Equal("g", component.UnitCode);
//             Assert.Equal(9000m, topping.Price);
//         }

//         [Fact]
//         public async Task PreparedItemSource_UsesPinnedChildRecipeAndCanonicalPreparedItem_NotLatestOrNameInference()
//         {
//             using var context = CreateDbContext();
//             var gram = context.Units.First(x => x.UnitCode == "g");
//             var topping = new Topping
//             {
//                 ToppingId = 14902,
//                 ToppingCode = "TOP-149-PI",
//                 Name = "Topping BTP 149",
//                 Price = 7000m,
//                 Active = true
//             };
//             var pinnedPi = new PreparedItem
//             {
//                 PreparedItemId = 14902,
//                 Code = "PI-PINNED-149",
//                 Name = "BTP được pin 149",
//                 BaseUnitId = gram.UnitId,
//                 Active = true
//             };
//             var latestPi = new PreparedItem
//             {
//                 PreparedItemId = 14903,
//                 Code = "PI-LATEST-149",
//                 Name = "BTP latest không được dùng 149",
//                 BaseUnitId = gram.UnitId,
//                 Active = true
//             };
//             var pinnedChild = new Recipe
//             {
//                 RecipeId = 14921,
//                 RecipeCode = "RCP-PI-PINNED-149",
//                 Name = "Phiên bản BTP cũ được pin",
//                 PreparedItemId = pinnedPi.PreparedItemId,
//                 OutputQuantity = 100m,
//                 OutputUnitId = gram.UnitId,
//                 Active = false,
//                 Status = "Archived"
//             };
//             var laterChild = new Recipe
//             {
//                 RecipeId = 14922,
//                 RecipeCode = "RCP-PI-LATEST-149",
//                 Name = topping.Name,
//                 PreparedItemId = latestPi.PreparedItemId,
//                 OutputQuantity = 100m,
//                 OutputUnitId = gram.UnitId,
//                 Active = true,
//                 Status = "Active"
//             };
//             context.Toppings.Add(topping);
//             context.PreparedItems.AddRange(pinnedPi, latestPi);
//             context.Recipes.AddRange(pinnedChild, laterChild, new Recipe
//             {
//                 RecipeId = 14902,
//                 RecipeCode = "RCP-TOP-PI-149",
//                 Name = topping.Name,
//                 ToppingId = topping.ToppingId,
//                 Active = true,
//                 Status = "Active",
//                 RecipeDetails = new List<RecipeDetail>
//                 {
//                     new()
//                     {
//                         ChildRecipeId = pinnedChild.RecipeId,
//                         Quantity = 25m,
//                         UnitId = gram.UnitId
//                     }
//                 }
//             });
//             await context.SaveChangesAsync();

//             var source = (await CreateQueryService(context)
//                 .GetToppingConsumptionSourcesAsync(new[] { topping.ToppingId }))[topping.ToppingId];
//             var component = Assert.Single(source.Components);

//             Assert.Equal(ToppingConsumptionSourceCodes.PreparedItem, source.SourceCode);
//             Assert.Equal("Nguồn bán thành phẩm đã sơ chế", source.SourceLabel);
//             Assert.True(source.MappingValid);
//             Assert.Equal(pinnedChild.RecipeId, component.ChildRecipeId);
//             Assert.Equal(pinnedPi.PreparedItemId, component.PreparedItemId);
//             Assert.Equal(pinnedPi.Code, component.PreparedItemCode);
//             Assert.NotEqual(latestPi.PreparedItemId, component.PreparedItemId);
//             Assert.Equal(25m, component.Quantity);
//         }

//         [Fact]
//         public async Task MissingOrInvalidActiveRecipe_ReturnsExplicitStatusAndNeverFakesZeroCost()
//         {
//             using var context = CreateDbContext();
//             var gram = context.Units.First(x => x.UnitCode == "g");
//             var missing = new Topping
//             {
//                 ToppingId = 14903,
//                 ToppingCode = "TOP-149-MISSING",
//                 Name = "Topping chưa có BOM 149",
//                 Price = 6000m,
//                 Active = true
//             };
//             var invalid = new Topping
//             {
//                 ToppingId = 14904,
//                 ToppingCode = "TOP-149-INVALID",
//                 Name = "Topping mapping lỗi 149",
//                 Price = 8000m,
//                 Active = true
//             };
//             var childWithoutPreparedItem = new Recipe
//             {
//                 RecipeId = 14923,
//                 RecipeCode = "RCP-CHILD-NO-PI-149",
//                 Name = "Child không có PreparedItem",
//                 Active = true,
//                 Status = "Active"
//             };
//             context.Toppings.AddRange(missing, invalid);
//             context.Recipes.AddRange(childWithoutPreparedItem, new Recipe
//             {
//                 RecipeId = 14904,
//                 RecipeCode = "RCP-TOP-INVALID-149",
//                 Name = invalid.Name,
//                 ToppingId = invalid.ToppingId,
//                 Active = true,
//                 Status = "Active",
//                 RecipeDetails = new List<RecipeDetail>
//                 {
//                     new()
//                     {
//                         ChildRecipeId = childWithoutPreparedItem.RecipeId,
//                         Quantity = 1m,
//                         UnitId = gram.UnitId
//                     }
//                 }
//             });
//             await context.SaveChangesAsync();

//             var sources = await CreateQueryService(context)
//                 .GetToppingConsumptionSourcesAsync(new[] { missing.ToppingId, invalid.ToppingId });

//             Assert.Equal("Chưa cấu hình nguồn tiêu hao", sources[missing.ToppingId].SourceLabel);
//             Assert.Equal(ToppingConsumptionSourceCodes.NoActiveRecipe, sources[missing.ToppingId].SourceCode);
//             Assert.False(sources[missing.ToppingId].MappingValid);
//             Assert.Null(sources[missing.ToppingId].EstimatedCostPerPortion);

//             Assert.Equal(ToppingConsumptionSourceCodes.MixedOrInvalid, sources[invalid.ToppingId].SourceCode);
//             Assert.Equal("Liên kết nguồn không hợp lệ", sources[invalid.ToppingId].SourceLabel);
//             Assert.False(sources[invalid.ToppingId].MappingValid);
//             Assert.Null(sources[invalid.ToppingId].EstimatedCostPerPortion);
//         }

//         [Fact]
//         public void AdminViews_SeparateSellingPriceEstimatedBomAndActualFifoCost_WithRepairCtas()
//         {
//             var root = FindRepoRoot();
//             var toppingView = File.ReadAllText(Path.Combine(
//                 root, "CafeChain", "Areas", "Admin", "Views", "AdminTopping", "Index.cshtml"));
//             var recipeView = File.ReadAllText(Path.Combine(
//                 root, "CafeChain", "Areas", "Admin", "Views", "AdminRecipe", "Index.cshtml"));
//             var visualizeView = File.ReadAllText(Path.Combine(
//                 root, "CafeChain", "Areas", "Admin", "Views", "AdminRecipe", "Visualize.cshtml"));

//             Assert.Contains("Giá bán từ Topping.Price", toppingView, StringComparison.Ordinal);
//             Assert.Contains("Giá vốn BOM ước tính", toppingView, StringComparison.Ordinal);
//             Assert.Contains("Giá vốn thực tế: FIFO khi bán", toppingView, StringComparison.Ordinal);
//             Assert.Contains("Tạo BOM", toppingView, StringComparison.Ordinal);
//             Assert.Contains("Sửa BOM", toppingView, StringComparison.Ordinal);
//             Assert.Contains("component.QuantityDisplay/phần", toppingView, StringComparison.Ordinal);
//             Assert.Contains("pin Recipe #", recipeView, StringComparison.Ordinal);
//             Assert.Contains("pin Recipe #", visualizeView, StringComparison.Ordinal);
//             Assert.DoesNotContain("PreparedItemName.Contains", recipeView, StringComparison.Ordinal);
//         }

//         private static AdminRecipeQueryService CreateQueryService(CafeChain.Data.AppDbContext context)
//         {
//             var physical = new PhysicalUnitConversionService(
//                 context,
//                 NullLogger<PhysicalUnitConversionService>.Instance);
//             var unitConversion = new UnitConversionService(
//                 context,
//                 NullLogger<UnitConversionService>.Instance,
//                 physical);
//             var normalizer = new RecipeOutputNormalizer(context, physical);
//             var cost = new EstimatedBomCostService(
//                 context,
//                 unitConversion,
//                 physical,
//                 normalizer,
//                 NullLogger<EstimatedBomCostService>.Instance);

//             return new AdminRecipeQueryService(
//                 context,
//                 normalizer,
//                 cost,
//                 new AdminPreparedItemService(context),
//                 new RecipeBomTreeQueryService(context),
//                 new BomDataHealthEvaluator());
//         }

//         private static string FindRepoRoot()
//         {
//             var dir = new DirectoryInfo(AppContext.BaseDirectory);
//             while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "CafeChain")))
//                 dir = dir.Parent;

//             return dir?.FullName ?? throw new DirectoryNotFoundException("Không tìm thấy repo root.");
//         }
//     }
// }
