using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Profitability;
using CafeChain.Application.Interfaces.Admin.Profitability;

namespace CafeChain.Application.Services.Admin.Profitability
{
    public sealed class PriceSuggestionService : IPriceSuggestionService
    {
        public PriceSuggestionResult Calculate(PriceSuggestionRequest request)
        {
            if (request.EstimatedCost < 0 || request.CurrentSellingPrice < 0)
                return Invalid("Giá vốn và giá bán không được âm.");

            var currentProfit = request.CurrentSellingPrice - request.EstimatedCost;
            decimal? currentMargin = request.CurrentSellingPrice > 0 ? currentProfit / request.CurrentSellingPrice * 100m : null;
            decimal? currentMarkup = request.EstimatedCost > 0 ? currentProfit / request.EstimatedCost * 100m : null;

            if (!ProfitabilityRoundingModes.All.Contains(request.RoundingMode))
                return Invalid("Chế độ làm tròn không hợp lệ.");

            decimal raw;
            switch (request.TargetMode)
            {
                case ProfitabilityTargetModes.Margin:
                    if (request.TargetValue < 0 || request.TargetValue >= 100)
                        return Invalid("Biên lợi nhuận mục tiêu phải từ 0 đến nhỏ hơn 100%.");
                    raw = request.EstimatedCost / (1m - request.TargetValue / 100m);
                    break;
                case ProfitabilityTargetModes.Markup:
                    if (request.TargetValue < 0)
                        return Invalid("Markup mục tiêu không được âm.");
                    raw = request.EstimatedCost * (1m + request.TargetValue / 100m);
                    break;
                case ProfitabilityTargetModes.ProfitAmount:
                    if (request.TargetValue < 0)
                        return Invalid("Số tiền lời mong muốn không được âm.");
                    raw = request.EstimatedCost + request.TargetValue;
                    break;
                default:
                    return Invalid("Chế độ mục tiêu không hợp lệ.");
            }

            var rounded = Round(raw, request.RoundingMode);
            if (rounded < 0) return Invalid("Giá sau làm tròn không hợp lệ.");
            var effectiveProfit = rounded - request.EstimatedCost;

            return new PriceSuggestionResult
            {
                IsValid = true,
                GrossProfit = decimal.Round(currentProfit, 2),
                GrossMarginPercent = RoundPercent(currentMargin),
                MarkupPercent = RoundPercent(currentMarkup),
                RawSuggestedPrice = decimal.Round(raw, 2),
                RoundedSuggestedPrice = decimal.Round(rounded, 2),
                EffectiveGrossProfit = decimal.Round(effectiveProfit, 2),
                EffectiveMarginPercent = rounded > 0 ? RoundPercent(effectiveProfit / rounded * 100m) : null,
                EffectiveMarkupPercent = request.EstimatedCost > 0 ? RoundPercent(effectiveProfit / request.EstimatedCost * 100m) : null
            };
        }

        private static decimal Round(decimal value, string mode) => mode switch
        {
            ProfitabilityRoundingModes.None => value,
            ProfitabilityRoundingModes.Nearest500 => Math.Round(value / 500m, 0, MidpointRounding.AwayFromZero) * 500m,
            ProfitabilityRoundingModes.Ceiling500 => Math.Ceiling(value / 500m) * 500m,
            ProfitabilityRoundingModes.Nearest1000 => Math.Round(value / 1000m, 0, MidpointRounding.AwayFromZero) * 1000m,
            ProfitabilityRoundingModes.Ceiling1000 => Math.Ceiling(value / 1000m) * 1000m,
            _ => value
        };

        private static decimal? RoundPercent(decimal? value) => value.HasValue ? decimal.Round(value.Value, 2) : null;
        private static PriceSuggestionResult Invalid(string message) => new() { IsValid = false, Message = message };
    }
}
