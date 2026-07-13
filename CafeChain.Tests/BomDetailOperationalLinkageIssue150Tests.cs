using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CafeChain.Application.Services.Admin.PreparedItems;
using CafeChain.Application.Services.Admin.Recipes;
using CafeChain.Application.Services.Inventories;
using CafeChain.Application.DTOs.Admin.StoreInventories;
using CafeChain.Application.Interfaces.Admin.Production;
using CafeChain.Application.Interfaces.Admin.Recipes;
using CafeChain.Application.Interfaces.Admin.StoreInventories;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Areas.Admin.Controllers;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Inventories.Production;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Stores;
using CafeChain.ViewModels.Admin.Recipes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;
using Xunit;

namespace CafeChain.Tests.POS
{
    public sealed class BomDetailOperationalLinkageIssue150Tests : IntegrationTestBase
    {
        [Fact]
        public async Task Detail_PreparedItem_ShowsStableIdentityNormalizedOutputAndExactCostReasons()
        {
            using var context = CreateDbContext();
            var gram = context.Units.First(x => x.UnitCode == "g");
            var kilogram = context.Units.First(x => x.UnitCode == "kg");
            var ingredient = Ingredient(15001, gram, "ING-150");
            var output = PreparedItem(15001, gram, "BTP-150");
            var recipe = Recipe(15001, "REC-BTP-150", output, 2m, kilogram);
            recipe.RecipeDetails.Add(new RecipeDetail
            {
                RecipeDetailId = 15001,
                IngredientId = ingredient.IngredientId,
                Quantity = 500m,
                UnitId = gram.UnitId
            });
            context.AddRange(ingredient, output, recipe);
            await context.SaveChangesAsync();

            var detail = await CreateQueryService(context).GetVisualizePageAsync(recipe.RecipeId);

            Assert.NotNull(detail);
            Assert.True(detail!.ShowBatchOutput);
            Assert.Equal("[BTP-150] BTP 150", detail.IdentityDisplay);
            Assert.Equal(2000m, detail.NormalizedOutputQuantity);
            Assert.Equal("g", detail.OutputBaseUnitCode);
            Assert.True(detail.ConfigurationHealth.IsComplete);
            Assert.False(detail.CostingHealth.IsComplete);
            Assert.NotEmpty(detail.CostingHealth.Reasons);
            Assert.All(detail.CostingHealth.Reasons, x => Assert.False(string.IsNullOrWhiteSpace(x.Code)));
            var component = Assert.Single(detail.Components);
            Assert.Equal("Nguyên liệu", component.ComponentType);
            Assert.Equal(500m, component.NormalizedQuantity);
            Assert.Equal("g", component.BaseUnitCode);
        }

        [Fact]
        public async Task Detail_PosAndTopping_DoNotExposeBatchOutput()
        {
            using var context = CreateDbContext();
            var gram = context.Units.First(x => x.UnitCode == "g");
            var ingredient = Ingredient(15011, gram, "ING-150-POS");
            var pos = SaleRecipe(15011, "REC-POS-150", ingredient, gram, drinkId: 15011);
            var topping = SaleRecipe(15012, "REC-TOP-150", ingredient, gram, toppingId: 15012);
            context.AddRange(ingredient, pos, topping);
            await context.SaveChangesAsync();
            var service = CreateQueryService(context);

            var posDetail = await service.GetVisualizePageAsync(pos.RecipeId);
            var toppingDetail = await service.GetVisualizePageAsync(topping.RecipeId);

            Assert.NotNull(posDetail);
            Assert.NotNull(toppingDetail);
            Assert.False(posDetail!.ShowBatchOutput);
            Assert.False(toppingDetail!.ShowBatchOutput);
            Assert.Null(posDetail.EstimatedBatchCost);
            Assert.Null(toppingDetail.EstimatedBatchCost);
        }

        [Fact]
        public async Task Detail_ChildRecipe_KeepsPinnedVersionAndPreparedItemIdentity()
        {
            using var context = CreateDbContext();
            var gram = context.Units.First(x => x.UnitCode == "g");
            var childOutput = PreparedItem(15021, gram, "BTP-CHILD-150");
            var parentOutput = PreparedItem(15022, gram, "BTP-PARENT-150");
            var child = Recipe(15021, "REC-CHILD-150-V3", childOutput, 1000m, gram);
            var parent = Recipe(15022, "REC-PARENT-150", parentOutput, 1000m, gram);
            parent.RecipeDetails.Add(new RecipeDetail
            {
                RecipeDetailId = 15021,
                ChildRecipeId = child.RecipeId,
                Quantity = 200m,
                UnitId = gram.UnitId
            });
            context.AddRange(childOutput, parentOutput, child, parent);
            await context.SaveChangesAsync();

            var detail = await CreateQueryService(context).GetVisualizePageAsync(parent.RecipeId);

            var component = Assert.Single(detail!.Components);
            Assert.Equal(child.RecipeId, component.ChildRecipeId);
            Assert.Equal("REC-CHILD-150-V3", component.ChildRecipeCode);
            Assert.Equal(childOutput.PreparedItemId, component.PreparedItemId);
            Assert.Equal("BTP-CHILD-150", component.PreparedItemCode);
        }

        [Fact]
        public async Task Operational_UsesCanonicalPreparedItemStockAndRecipeSpecificProductionRuns()
        {
            using var context = CreateDbContext();
            var gram = context.Units.First(x => x.UnitCode == "g");
            var output = PreparedItem(15031, gram, "BTP-STOCK-150");
            var recipe = Recipe(15031, "REC-STOCK-150", output, 1000m, gram);
            var store = new Store
            {
                StoreId = 15031,
                Name = "Cửa hàng #150",
                Address = "Test",
                Phone = "000",
                Active = true,
                CreatedAt = DateTime.UtcNow
            };
            var stock = new StoreInventory
            {
                StoreInventoryId = 15031,
                StoreId = store.StoreId,
                PreparedItemId = output.PreparedItemId,
                BtpIdentityState = BtpIdentityState.Canonical,
                QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
                AvailableQty = 1200m,
                ReservedQty = 200m,
                LastUpdated = DateTime.UtcNow
            };
            var run = new ProductionRun
            {
                ProductionRunId = 15031,
                StoreId = store.StoreId,
                RecipeId = recipe.RecipeId,
                RequestedRunCount = 1m,
                RequestKey = Guid.NewGuid(),
                RequestFingerprint = "issue-150",
                Status = ProductionRunStatus.Completed,
                CreatedByStaffId = 15031,
                CreatedAt = DateTime.UtcNow,
                ConfirmedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                ValuationStatus = ProductionValuationStatus.Complete,
                TotalInputCost = 45000m,
                OutputUnitCost = 45m
            };
            context.AddRange(output, recipe, store, stock, run);
            await context.SaveChangesAsync();
            context.InventoryCostLayers.Add(new InventoryCostLayer
            {
                InventoryCostLayerId = 15031,
                PreparedItemId = output.PreparedItemId,
                StoreId = store.StoreId,
                Quantity = 1000m,
                RemainingQuantity = 800m,
                UnitCost = 45m,
                SourceProductionRunId = run.ProductionRunId,
                CreatedAt = DateTime.UtcNow
            });
            context.InventoryTransactions.Add(new InventoryTransaction
            {
                InventoryTransactionId = 15031,
                StoreInventoryId = stock.StoreInventoryId,
                ProductionRunId = run.ProductionRunId,
                Type = InventoryTransactionTypeEnum.PRODUCTION_IN,
                StockStatus = InventoryStockStatus.NORMAL,
                Quantity = 1000m,
                BeforeQty = 200m,
                AfterQty = 1200m,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var operational = await CreateQueryService(context)
                .GetOperationalDetailAsync(recipe.RecipeId, store.StoreId);

            Assert.NotNull(operational);
            Assert.NotNull(operational!.OutputStock);
            Assert.Equal(stock.StoreInventoryId, operational.OutputStock!.StoreInventoryId);
            Assert.Equal(1200m, operational.OutputStock.CurrentQuantity);
            Assert.Equal(200m, operational.OutputStock.ReservedQuantity);
            Assert.Equal(1000m, operational.OutputStock.UsableQuantity);
            Assert.Equal(45m, operational.OutputStock.ActualUnitCost);
            var recent = Assert.Single(operational.RecentRuns);
            Assert.Equal(recipe.RecipeId, run.RecipeId);
            Assert.Equal(1000m, recent.NormalizedOutputQuantity);
            Assert.Equal(45m, recent.ActualOutputUnitCost);
        }

        [Fact]
        public void DetailView_PreservesScopeAuthorizationCostAuthorityAndResponsiveActions()
        {
            var root = FindRepoRoot();
            var controller = File.ReadAllText(Path.Combine(
                root, "CafeChain", "Areas", "Admin", "Controllers", "AdminRecipeController.cs"));
            var view = File.ReadAllText(Path.Combine(
                root, "CafeChain", "Areas", "Admin", "Views", "AdminRecipe", "Visualize.cshtml"));
            var css = File.ReadAllText(Path.Combine(root, "CafeChain", "wwwroot", "css", "recipe-builder.css"));

            Assert.Contains("GetStoresByStaffAsync", controller, StringComparison.Ordinal);
            Assert.Contains("return Forbid()", controller, StringComparison.Ordinal);
            Assert.Contains("Url.IsLocalUrl", controller, StringComparison.Ordinal);
            Assert.Contains("Model.CanWrite", view, StringComparison.Ordinal);
            Assert.Contains("Giá vốn BOM ước tính", view, StringComparison.Ordinal);
            Assert.Contains("Actual FIFO", view, StringComparison.Ordinal);
            Assert.Contains("Chọn cửa hàng", view, StringComparison.Ordinal);
            Assert.Contains("asp-route-returnUrl", view, StringComparison.Ordinal);
            Assert.Contains("@media (max-width: 1023.98px)", css, StringComparison.Ordinal);
            Assert.Contains("grid-template-columns: 1fr", css, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Visualize_UnauthorizedStore_IsForbiddenBeforeOperationalQueries()
        {
            var recipeService = new Mock<IAdminRecipeService>();
            var queryService = new Mock<IAdminRecipeQueryService>();
            var treeService = new Mock<IRecipeBomTreeQueryService>();
            var outputNormalizer = new Mock<IRecipeOutputNormalizer>();
            var estimatedCost = new Mock<IEstimatedBomCostService>();
            var readiness = new Mock<IProductionReadinessService>();
            var inventory = new Mock<IAdminStoreInventoryService>();
            queryService
                .Setup(x => x.GetVisualizePageAsync(15001))
                .ReturnsAsync(new AdminRecipeVisualizePageVM
                {
                    RecipeId = 15001,
                    RecipeTypeKey = "SUBRECIPE"
                });
            inventory
                .Setup(x => x.GetStoresByStaffAsync(77))
                .ReturnsAsync(new List<InventoryStoreDTO>
                {
                    new() { StoreId = 1, StoreName = "Allowed" }
                });

            var controller = new AdminRecipeController(
                recipeService.Object,
                queryService.Object,
                treeService.Object,
                outputNormalizer.Object,
                estimatedCost.Object,
                readiness.Object,
                inventory.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(
                            new[] { new Claim(ClaimTypes.NameIdentifier, "77") },
                            "Test"))
                    }
                }
            };
            var url = new Mock<IUrlHelper>();
            url.Setup(x => x.IsLocalUrl(It.IsAny<string>())).Returns(true);
            controller.Url = url.Object;

            var result = await controller.Visualize(15001, storeId: 2, returnUrl: "/Admin/AdminRecipe");

            Assert.IsType<ForbidResult>(result);
            queryService.Verify(
                x => x.GetOperationalDetailAsync(It.IsAny<int>(), It.IsAny<int>()),
                Times.Never);
            readiness.Verify(
                x => x.PreviewAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>()),
                Times.Never);
        }

        private static AdminRecipeQueryService CreateQueryService(CafeChain.Data.AppDbContext context)
        {
            var physical = new PhysicalUnitConversionService(
                context,
                NullLogger<PhysicalUnitConversionService>.Instance);
            var unitConversion = new UnitConversionService(
                context,
                NullLogger<UnitConversionService>.Instance,
                physical);
            var normalizer = new RecipeOutputNormalizer(context, physical);
            var cost = new EstimatedBomCostService(
                context,
                unitConversion,
                physical,
                normalizer,
                NullLogger<EstimatedBomCostService>.Instance);

            return new AdminRecipeQueryService(
                context,
                normalizer,
                cost,
                new AdminPreparedItemService(context),
                new RecipeBomTreeQueryService(context),
                new BomDataHealthEvaluator());
        }

        private static Ingredient Ingredient(
            int id,
            CafeChain.Models.Inventories.Ingredients.Unit baseUnit,
            string code)
            => new()
            {
                IngredientId = id,
                Code = code,
                Name = code,
                BaseUnitId = baseUnit.UnitId,
                Active = true
            };

        private static PreparedItem PreparedItem(
            int id,
            CafeChain.Models.Inventories.Ingredients.Unit baseUnit,
            string code)
            => new()
            {
                PreparedItemId = id,
                Code = code,
                Name = code.Replace("-", " "),
                BaseUnitId = baseUnit.UnitId,
                Active = true
            };

        private static Recipe Recipe(
            int id,
            string code,
            PreparedItem output,
            decimal outputQuantity,
            CafeChain.Models.Inventories.Ingredients.Unit outputUnit)
            => new()
            {
                RecipeId = id,
                RecipeCode = code,
                Name = code,
                PreparedItemId = output.PreparedItemId,
                OutputQuantity = outputQuantity,
                OutputUnitId = outputUnit.UnitId,
                Active = true,
                Status = "Active",
                EffectiveDate = new DateTime(2026, 7, 13),
                RecipeDetails = new List<RecipeDetail>()
            };

        private static Recipe SaleRecipe(
            int id,
            string code,
            Ingredient ingredient,
            CafeChain.Models.Inventories.Ingredients.Unit unit,
            int? drinkId = null,
            int? toppingId = null)
            => new()
            {
                RecipeId = id,
                RecipeCode = code,
                Name = code,
                DrinkId = drinkId,
                ToppingId = toppingId,
                Active = true,
                Status = "Active",
                RecipeDetails = new List<RecipeDetail>
                {
                    new()
                    {
                        RecipeDetailId = id,
                        IngredientId = ingredient.IngredientId,
                        Quantity = 10m,
                        UnitId = unit.UnitId
                    }
                }
            };

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "CafeChain")))
                dir = dir.Parent;

            return dir?.FullName ?? throw new DirectoryNotFoundException("Không tìm thấy repo root.");
        }
    }
}
