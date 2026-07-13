using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Costing;
using CafeChain.Application.Services.Admin.Recipes;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.ViewModels.Admin.Recipes;
using Xunit;

namespace CafeChain.Tests.POS
{
    public sealed class BomDataHealthIssue146Tests
    {
        private readonly BomDataHealthEvaluator _evaluator = new();

        [Fact]
        public void Configuration_PosRecipe_DoesNotRequireBatchOutput()
        {
            var recipe = ValidRecipe(drinkId: 10);

            var result = _evaluator.EvaluateConfiguration(recipe);

            Assert.True(result.IsComplete);
            Assert.Equal(BomConfigurationHealthCodes.Complete, result.Code);
        }

        [Fact]
        public void Configuration_ToppingRecipe_DoesNotRequireBatchOutput()
        {
            var recipe = ValidRecipe(toppingId: 20);

            var result = _evaluator.EvaluateConfiguration(recipe);

            Assert.True(result.IsComplete);
        }

        [Fact]
        public void Configuration_PreparedItem_RequiresIdentityYieldAndUnit()
        {
            var recipe = ValidRecipe();
            recipe.PreparedItemId = null;
            recipe.PreparedItem = null;
            recipe.OutputQuantity = null;
            recipe.OutputUnitId = null;
            recipe.OutputUnit = null;

            var result = _evaluator.EvaluateConfiguration(recipe);

            Assert.False(result.IsComplete);
            Assert.Contains(result.Reasons, x => x.Code == BomConfigurationHealthCodes.MissingOutputIdentity);
            Assert.Contains(result.Reasons, x => x.Code == BomConfigurationHealthCodes.MissingOutputQuantity);
            Assert.Contains(result.Reasons, x => x.Code == BomConfigurationHealthCodes.MissingOutputUnit);
        }

        [Fact]
        public void Configuration_MissingComponentsAndInvalidChildMapping_AreExact()
        {
            var missing = ValidRecipe(drinkId: 10);
            missing.RecipeDetails = new List<RecipeDetail>();
            var missingResult = _evaluator.EvaluateConfiguration(missing);
            Assert.Contains(missingResult.Reasons, x => x.Code == BomConfigurationHealthCodes.MissingComponents);

            var invalid = ValidRecipe(drinkId: 10);
            invalid.RecipeDetails = new List<RecipeDetail>
            {
                new()
                {
                    RecipeDetailId = 8,
                    ChildRecipeId = 99,
                    Quantity = 1,
                    UnitId = 1,
                    Unit = ActiveUnit()
                }
            };

            var invalidResult = _evaluator.EvaluateConfiguration(invalid);
            Assert.Contains(invalidResult.Reasons, x =>
                x.Code == BomConfigurationHealthCodes.InvalidPreparedItemMapping
                && x.Message.Contains("PreparedItem", StringComparison.Ordinal));
        }

        [Fact]
        public void Configuration_InactiveRecipe_IsReportedSeparately()
        {
            var recipe = ValidRecipe(drinkId: 10);
            recipe.Active = false;

            var result = _evaluator.EvaluateConfiguration(recipe);

            Assert.Equal(BomConfigurationHealthCodes.Inactive, result.Code);
            Assert.Equal("Không hoạt động", result.Label);
        }

        [Theory]
        [InlineData(CostIssueCodes.MissingSupplierOffer, BomCostingHealthCodes.MissingQuote, "AdminSupplier")]
        [InlineData(CostIssueCodes.ZeroPackagePrice, BomCostingHealthCodes.MissingQuote, "AdminSupplier")]
        [InlineData(CostIssueCodes.MissingUnitConversion, BomCostingHealthCodes.MissingConversion, "AdminUnitConversion")]
        [InlineData(CostIssueCodes.ConflictingUnitConversion, BomCostingHealthCodes.MissingConversion, "AdminUnitConversion")]
        [InlineData(CostIssueCodes.MissingChildRecipe, BomCostingHealthCodes.MissingChildCost, "AdminRecipe")]
        [InlineData(CostIssueCodes.LegacyChildRecipeWithoutOutput, BomCostingHealthCodes.MissingChildCost, "AdminRecipe")]
        [InlineData("UNMAPPED_COST_CODE", BomCostingHealthCodes.Indeterminate, "AdminRecipe")]
        public void Costing_MapsIssueCodeAndPreservesExactReason(
            string issueCode,
            string expectedGroup,
            string expectedController)
        {
            var result = _evaluator.EvaluateCosting(CostCalculationResult.Incomplete(
                Array.Empty<CostLineResult>(),
                new[] { new CostIssue { Code = issueCode, Message = "Lý do chính xác", RecipeId = 11 } }));

            Assert.Equal(expectedGroup, result.Code);
            var reason = Assert.Single(result.Reasons);
            Assert.Equal(issueCode, reason.Code);
            Assert.Equal("Lý do chính xác", reason.Message);
            Assert.Equal(expectedController, reason.CtaController);
        }

        [Fact]
        public void Costing_Complete_KeepsAuthoritativeTotal()
        {
            var source = CostCalculationResult.Complete(12500m, Array.Empty<CostLineResult>());

            var result = _evaluator.EvaluateCosting(source);

            Assert.True(result.IsComplete);
            Assert.Equal(BomCostingHealthCodes.Complete, result.Code);
            Assert.Empty(result.Reasons);
        }

        [Fact]
        public void DataHealthView_DoesNotMixStoreReadiness()
        {
            var root = FindRepoRoot();
            var view = File.ReadAllText(Path.Combine(
                root,
                "CafeChain",
                "Areas",
                "Admin",
                "Views",
                "AdminRecipe",
                "DataHealth.cshtml"));

            Assert.Contains("Tình trạng dữ liệu BOM", view, StringComparison.Ordinal);
            Assert.Contains("Trạng thái cửa hàng được đánh giá riêng", view, StringComparison.Ordinal);
            Assert.DoesNotContain("Store readiness", view, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Đủ cho", view, StringComparison.OrdinalIgnoreCase);
        }

        private static Recipe ValidRecipe(int? drinkId = null, int? toppingId = null)
        {
            var unit = ActiveUnit();
            var preparedItem = new PreparedItem
            {
                PreparedItemId = 30,
                Code = "BTP-30",
                Name = "BTP test",
                BaseUnitId = unit.UnitId,
                BaseUnit = unit,
                Active = true
            };

            return new Recipe
            {
                RecipeId = 11,
                RecipeCode = "REC-11",
                Name = "Công thức test",
                Active = true,
                Status = "Active",
                DrinkId = drinkId,
                ToppingId = toppingId,
                PreparedItemId = drinkId.HasValue || toppingId.HasValue ? null : preparedItem.PreparedItemId,
                PreparedItem = drinkId.HasValue || toppingId.HasValue ? null : preparedItem,
                OutputQuantity = drinkId.HasValue || toppingId.HasValue ? null : 1m,
                OutputUnitId = drinkId.HasValue || toppingId.HasValue ? null : unit.UnitId,
                OutputUnit = drinkId.HasValue || toppingId.HasValue ? null : unit,
                RecipeDetails = new List<RecipeDetail>
                {
                    new()
                    {
                        RecipeDetailId = 1,
                        IngredientId = 1,
                        Quantity = 10m,
                        UnitId = unit.UnitId,
                        Unit = unit
                    }
                }
            };
        }

        private static Unit ActiveUnit() => new()
        {
            UnitId = 1,
            UnitCode = "g",
            Name = "Gram",
            Type = UnitType.KhoiLuong,
            Active = true
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
