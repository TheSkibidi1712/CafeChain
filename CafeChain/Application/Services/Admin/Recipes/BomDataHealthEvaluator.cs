using System;
using System.Collections.Generic;
using System.Linq;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Costing;
using CafeChain.Application.Interfaces.Admin.Recipes;
using CafeChain.Models.Drinks;
using CafeChain.ViewModels.Admin.Recipes;

namespace CafeChain.Application.Services.Admin.Recipes
{
    public sealed class BomDataHealthEvaluator : IBomDataHealthEvaluator
    {
        private static readonly HashSet<string> QuoteIssueCodes = new(StringComparer.Ordinal)
        {
            CostIssueCodes.MissingSupplierOffer,
            CostIssueCodes.MultiplePrimarySuppliers,
            CostIssueCodes.InactiveSupplierOffer,
            CostIssueCodes.MissingPackageQuantity,
            CostIssueCodes.InvalidPackageQuantity,
            CostIssueCodes.MissingPackagePrice,
            CostIssueCodes.ZeroPackagePrice,
            CostIssueCodes.MissingPackageUnit,
            CostIssueCodes.InactivePackageUnit,
            CostIssueCodes.RejectedPackagingUnit
        };

        private static readonly HashSet<string> ConversionIssueCodes = new(StringComparer.Ordinal)
        {
            CostIssueCodes.MissingUnitConversion,
            CostIssueCodes.ConflictingUnitConversion
        };

        private static readonly HashSet<string> ChildCostIssueCodes = new(StringComparer.Ordinal)
        {
            CostIssueCodes.MissingRecipe,
            CostIssueCodes.MissingRecipeDetails,
            CostIssueCodes.MissingRecipeOutput,
            CostIssueCodes.InvalidRecipeOutput,
            CostIssueCodes.MissingChildRecipe,
            CostIssueCodes.LegacyChildRecipeWithoutOutput,
            CostIssueCodes.RecipeCycle,
            CostIssueCodes.MaxDepthExceeded
        };

        public BomHealthStatusVM EvaluateConfiguration(Recipe recipe)
        {
            ArgumentNullException.ThrowIfNull(recipe);

            if (!recipe.Active || !string.Equals(recipe.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return Status(
                    BomConfigurationHealthCodes.Inactive,
                    "Không hoạt động",
                    Reason(BomConfigurationHealthCodes.Inactive, "Công thức đang ngừng hoạt động.", recipe.RecipeId));
            }

            var reasons = new List<BomHealthReasonVM>();
            var details = recipe.RecipeDetails?.ToList() ?? new List<RecipeDetail>();
            if (details.Count == 0)
            {
                reasons.Add(Reason(
                    BomConfigurationHealthCodes.MissingComponents,
                    "Công thức chưa có thành phần BOM.",
                    recipe.RecipeId));
            }

            foreach (var detail in details)
            {
                var hasIngredient = detail.IngredientId.HasValue;
                var hasChild = detail.ChildRecipeId.HasValue;
                if (hasIngredient == hasChild)
                {
                    reasons.Add(Reason(
                        BomConfigurationHealthCodes.InvalidPreparedItemMapping,
                        $"Dòng BOM #{detail.RecipeDetailId} phải tham chiếu đúng một nguyên liệu hoặc công thức BTP.",
                        recipe.RecipeId));
                }

                if (detail.UnitId <= 0 || detail.Unit == null || !detail.Unit.Active)
                {
                    reasons.Add(Reason(
                        BomConfigurationHealthCodes.MissingComponentUnit,
                        $"Dòng BOM #{detail.RecipeDetailId} thiếu đơn vị định lượng còn hiệu lực.",
                        recipe.RecipeId));
                }

                if (hasChild
                    && (detail.ChildRecipe == null
                        || !detail.ChildRecipe.PreparedItemId.HasValue
                        || detail.ChildRecipe.PreparedItem == null
                        || !detail.ChildRecipe.PreparedItem.Active))
                {
                    reasons.Add(Reason(
                        BomConfigurationHealthCodes.InvalidPreparedItemMapping,
                        $"Bán thành phẩm đầu vào ở dòng #{detail.RecipeDetailId} chưa có liên kết tồn kho hợp lệ.",
                        recipe.RecipeId));
                }
            }

            var isSubRecipe = !recipe.DrinkId.HasValue && !recipe.ToppingId.HasValue;
            if (isSubRecipe)
            {
                if (!recipe.PreparedItemId.HasValue)
                {
                    reasons.Add(Reason(
                        BomConfigurationHealthCodes.MissingOutputIdentity,
                        "Công thức bán thành phẩm chưa chọn bán thành phẩm đầu ra.",
                        recipe.RecipeId,
                        "Chọn BTP đầu ra"));
                }
                else if (recipe.PreparedItem == null || !recipe.PreparedItem.Active)
                {
                    reasons.Add(Reason(
                        BomConfigurationHealthCodes.InvalidPreparedItemMapping,
                        "Bán thành phẩm đầu ra không tồn tại hoặc đã ngừng hoạt động.",
                        recipe.RecipeId,
                        "Sửa liên kết BTP"));
                }

                if (!recipe.OutputQuantity.HasValue || recipe.OutputQuantity.Value <= 0)
                {
                    reasons.Add(Reason(
                        BomConfigurationHealthCodes.MissingOutputQuantity,
                        "Công thức BTP thiếu sản lượng đầu ra lớn hơn 0.",
                        recipe.RecipeId));
                }

                if (!recipe.OutputUnitId.HasValue || recipe.OutputUnit == null || !recipe.OutputUnit.Active)
                {
                    reasons.Add(Reason(
                        BomConfigurationHealthCodes.MissingOutputUnit,
                        "Công thức BTP thiếu đơn vị đầu ra còn hiệu lực.",
                        recipe.RecipeId));
                }
            }

            if (reasons.Count == 0)
                return Status(BomConfigurationHealthCodes.Complete, "Hoàn chỉnh", complete: true);

            var primary = ConfigurationPriority(reasons.Select(x => x.Code));
            return Status(primary, ConfigurationLabel(primary), reasons.ToArray());
        }

        public BomHealthStatusVM EvaluateCosting(CostCalculationResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            if (result.IsComplete && result.TotalCost.HasValue)
                return Status(BomCostingHealthCodes.Complete, "Đầy đủ", complete: true);

            var issues = result.Issues?.ToList() ?? new List<CostIssue>();
            var code = CostingPriority(issues.Select(x => x.Code));
            var reasons = issues.Select(x => CostReason(x, code)).ToArray();
            if (reasons.Length == 0)
            {
                reasons = new[]
                {
                    new BomHealthReasonVM
                    {
                        Code = BomCostingHealthCodes.Indeterminate,
                        GroupCode = BomCostingHealthCodes.Indeterminate,
                        Message = "Không xác định được giá vốn do kết quả tính không đầy đủ.",
                        CtaLabel = "Kiểm tra BOM",
                        CtaController = "AdminRecipe",
                        CtaAction = "Index"
                    }
                };
            }

            return Status(code, CostingLabel(code), reasons);
        }

        private static BomHealthReasonVM CostReason(CostIssue issue, string groupCode)
        {
            var reason = new BomHealthReasonVM
            {
                Code = issue.Code,
                GroupCode = ClassifyCostIssueCode(issue.Code),
                Message = issue.Message,
                CtaLabel = "Kiểm tra BOM",
                CtaController = "AdminRecipe",
                CtaAction = "Edit",
                CtaId = issue.RecipeId
            };

            if (QuoteIssueCodes.Contains(issue.Code))
            {
                reason.CtaLabel = "Cập nhật báo giá NCC";
                reason.CtaController = "AdminSupplier";
                reason.CtaAction = "Index";
                reason.CtaId = null;
            }
            else if (ConversionIssueCodes.Contains(issue.Code))
            {
                reason.CtaLabel = "Cấu hình quy đổi";
                reason.CtaController = "AdminUnitConversion";
                reason.CtaAction = "Index";
                reason.CtaId = null;
            }
            else if (groupCode == BomCostingHealthCodes.MissingChildCost)
            {
                reason.CtaLabel = "Sửa BOM liên quan";
            }

            return reason;
        }

        private static BomHealthReasonVM Reason(
            string code,
            string message,
            int recipeId,
            string label = "Sửa công thức")
            => new()
            {
                Code = code,
                GroupCode = code,
                Message = message,
                CtaLabel = label,
                CtaController = "AdminRecipe",
                CtaAction = "Edit",
                CtaId = recipeId
            };

        private static BomHealthStatusVM Status(
            string code,
            string label,
            params BomHealthReasonVM[] reasons)
            => Status(code, label, false, reasons);

        private static BomHealthStatusVM Status(
            string code,
            string label,
            bool complete,
            params BomHealthReasonVM[] reasons)
            => new()
            {
                Code = code,
                Label = label,
                IsComplete = complete,
                Reasons = reasons.ToList()
            };

        private static string ConfigurationPriority(IEnumerable<string> codes)
        {
            var set = codes.ToHashSet(StringComparer.Ordinal);
            if (set.Contains(BomConfigurationHealthCodes.InvalidPreparedItemMapping))
                return BomConfigurationHealthCodes.InvalidPreparedItemMapping;
            if (set.Contains(BomConfigurationHealthCodes.MissingOutputIdentity))
                return BomConfigurationHealthCodes.MissingOutputIdentity;
            if (set.Contains(BomConfigurationHealthCodes.MissingOutputQuantity))
                return BomConfigurationHealthCodes.MissingOutputQuantity;
            if (set.Contains(BomConfigurationHealthCodes.MissingOutputUnit)
                || set.Contains(BomConfigurationHealthCodes.MissingComponentUnit))
                return BomConfigurationHealthCodes.MissingOutputUnit;
            return BomConfigurationHealthCodes.MissingComponents;
        }

        private static string ConfigurationLabel(string code) => code switch
        {
            BomConfigurationHealthCodes.MissingComponents => "Thiếu thành phần",
            BomConfigurationHealthCodes.MissingOutputIdentity => "Thiếu bán thành phẩm đầu ra",
            BomConfigurationHealthCodes.MissingOutputQuantity => "Thiếu sản lượng",
            BomConfigurationHealthCodes.MissingOutputUnit => "Thiếu đơn vị",
            BomConfigurationHealthCodes.InvalidPreparedItemMapping => "Liên kết BTP không hợp lệ",
            _ => "Chưa hoàn chỉnh"
        };

        private static string CostingPriority(IEnumerable<string> codes)
        {
            var set = codes.ToHashSet(StringComparer.Ordinal);
            if (set.Overlaps(QuoteIssueCodes))
                return BomCostingHealthCodes.MissingQuote;
            if (set.Overlaps(ConversionIssueCodes))
                return BomCostingHealthCodes.MissingConversion;
            if (set.Overlaps(ChildCostIssueCodes))
                return BomCostingHealthCodes.MissingChildCost;
            return BomCostingHealthCodes.Indeterminate;
        }

        private static string ClassifyCostIssueCode(string code)
        {
            if (QuoteIssueCodes.Contains(code))
                return BomCostingHealthCodes.MissingQuote;
            if (ConversionIssueCodes.Contains(code))
                return BomCostingHealthCodes.MissingConversion;
            if (ChildCostIssueCodes.Contains(code))
                return BomCostingHealthCodes.MissingChildCost;
            return BomCostingHealthCodes.Indeterminate;
        }

        private static string CostingLabel(string code) => code switch
        {
            BomCostingHealthCodes.MissingQuote => "Thiếu báo giá",
            BomCostingHealthCodes.MissingConversion => "Thiếu quy đổi",
            BomCostingHealthCodes.MissingChildCost => "Thiếu giá BTP con",
            _ => "Không xác định được giá vốn"
        };
    }
}
