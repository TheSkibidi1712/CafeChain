using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.Services.Admin.PreparedItems;
using CafeChain.Application.Services.Admin.Recipes;
using CafeChain.Application.Services.Inventories;
using CafeChain.Areas.Admin.Controllers;
using CafeChain.Helpers;
using CafeChain.Models.Drinks;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CafeChain.Tests.POS
{
    public sealed class BomListSemanticsAuthorizationIssue145Tests : IntegrationTestBase
    {
        [Fact]
        public async Task List_TabsUseTypeSpecificRows_AndBtpNormalizationIgnoresYieldPercentage()
        {
            using var context = CreateDbContext();
            var gram = context.Units.First(x => x.UnitCode == "g");
            var kilogram = context.Units.First(x => x.UnitCode == "kg");
            var ingredient = new Ingredient
            {
                IngredientId = 1451,
                Code = "ING-145",
                Name = "Nguyên liệu 145",
                BaseUnitId = gram.UnitId,
                Active = true
            };
            var preparedItem = new PreparedItem
            {
                PreparedItemId = 1451,
                Code = "BTP-145",
                Name = "BTP 145",
                BaseUnitId = gram.UnitId,
                Active = true
            };
            context.Ingredients.Add(ingredient);
            context.PreparedItems.Add(preparedItem);
            context.Recipes.AddRange(
                RecipeWithIngredient(1451, "POS-145", ingredient.IngredientId, gram.UnitId, drinkId: 145),
                RecipeWithIngredient(1452, "TOP-145", ingredient.IngredientId, gram.UnitId, toppingId: 145),
                new Recipe
                {
                    RecipeId = 1453,
                    RecipeCode = "REC-BTP-145",
                    Name = "BTP 145",
                    PreparedItemId = preparedItem.PreparedItemId,
                    OutputQuantity = 2m,
                    OutputUnitId = kilogram.UnitId,
                    YieldPercentage = 42m,
                    Active = true,
                    Status = "Active",
                    RecipeDetails = new List<RecipeDetail>
                    {
                        new()
                        {
                            IngredientId = ingredient.IngredientId,
                            Quantity = 10m,
                            UnitId = gram.UnitId
                        }
                    }
                });
            await context.SaveChangesAsync();

            var service = CreateQueryService(context);
            var pos = await service.GetIndexPageAsync("POS", "POS-145");
            var topping = await service.GetIndexPageAsync("TOPPING", "TOP-145");
            var btp = await service.GetIndexPageAsync("SUBRECIPE", "BTP-145");

            Assert.Equal(1451, Assert.Single(pos.Items).RecipeId);
            Assert.Equal(1452, Assert.Single(topping.Items).RecipeId);
            var btpRow = Assert.Single(btp.Items);
            Assert.Equal(1453, btpRow.RecipeId);
            Assert.Equal(2000m, btpRow.NormalizedQuantityInBase);
            Assert.Equal("2000 g", btpRow.NormalizedOutputDisplay);
            Assert.NotEqual(840m, btpRow.NormalizedQuantityInBase);
            Assert.Equal("2 kg", btpRow.OutputPerBatchDisplay);
        }

        [Fact]
        public void RecipeWriteActions_RequireServerSidePermissionsWithoutRoleMatrix()
        {
            var writeMethods = typeof(AdminRecipeController)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(x => x.Name is "Create" or "Edit" or "Delete")
                .ToList();

            Assert.NotEmpty(writeMethods);
            foreach (var method in writeMethods)
            {
                var authorize = method.GetCustomAttribute<CafeChain.Application.Authorization.RequirePermissionAttribute>();
                Assert.NotNull(authorize);
                var permission = method.Name switch
                {
                    "Create" => PermissionConstants.RecipeCreate,
                    "Edit" => PermissionConstants.RecipeUpdate,
                    _ => PermissionConstants.RecipeDelete
                };
                Assert.Equal(
                    CafeChain.Application.Authorization.RequirePermissionAttribute.PolicyPrefix + permission,
                    authorize!.Policy);
                Assert.Null(authorize.Roles);
            }
        }

        [Fact]
        public void RecipeList_RendersSeparateColumnsAndDeleteConfirmation()
        {
            var view = File.ReadAllText(Path.Combine(
                FindRepoRoot(),
                "CafeChain",
                "Areas",
                "Admin",
                "Views",
                "AdminRecipe",
                "Index.cshtml"));

            Assert.Contains("Món bán", view, StringComparison.Ordinal);
            Assert.Contains("Topping", view, StringComparison.Ordinal);
            Assert.Contains("Bán thành phẩm", view, StringComparison.Ordinal);
            Assert.Contains("Giá vốn/phần", view, StringComparison.Ordinal);
            Assert.Contains("Giá vốn/mẻ", view, StringComparison.Ordinal);
            Assert.Contains("Giá vốn/đơn vị", view, StringComparison.Ordinal);
            Assert.Contains("if (type == \"SUBRECIPE\")", view, StringComparison.Ordinal);
            Assert.Contains("confirm('Xóa công thức", view, StringComparison.Ordinal);
            Assert.Contains("Model.CanWrite", view, StringComparison.Ordinal);
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

        private static Recipe RecipeWithIngredient(
            int recipeId,
            string code,
            int ingredientId,
            int unitId,
            int? drinkId = null,
            int? toppingId = null)
            => new()
            {
                RecipeId = recipeId,
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
                        IngredientId = ingredientId,
                        Quantity = 15m,
                        UnitId = unitId
                    }
                }
            };

        private static ClaimsPrincipal UserWithRole(string role)
        {
            return new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.Role, role) },
                "Test"));
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
