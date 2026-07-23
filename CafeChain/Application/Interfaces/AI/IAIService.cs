using CafeChain.Application.DTOs.AI;
using CafeChain.Application.DTOs.Admin.Dashboard;

namespace CafeChain.Application.Interfaces.AI;

public interface IAIService
{
    Task<DrinkSuggestionResultDTO> SuggestDrinkAsync(DrinkSuggestionRequestDTO request, CancellationToken cancellationToken = default);
    Task<SizeSuggestionResultDTO> SuggestSizeAsync(SizeSuggestionRequestDTO request, CancellationToken cancellationToken = default);
    Task<ToppingSuggestionResultDTO> SuggestToppingAsync(ToppingSuggestionRequestDTO request, CancellationToken cancellationToken = default);
    Task<CategorySuggestionResultDTO> SuggestCategoriesAsync(
        CategorySuggestionRequestDTO request,
        CancellationToken cancellationToken = default);
    Task<OllamaHealthDTO> CheckHealthAsync(CancellationToken cancellationToken = default);
    Task<InventoryReorderExplanationResultDto> ExplainInventoryReorderAsync(
        InventoryReorderExplanationContextDto context,
        CancellationToken cancellationToken = default);
    Task<DashboardIntentParseResultDto> ParseDashboardIntentAsync(
        DashboardPromptRequestDto request,
        IReadOnlyList<string> allowedStoreNames,
        CancellationToken cancellationToken = default);
    Task<DashboardExplanationResultDto> ExplainDashboardInsightAsync(
        DashboardInsightExplanationContextDto context,
        CancellationToken cancellationToken = default);
    Task<TypedExplanationResultDto> ExplainForecastAsync(ForecastExplanationContextDto context, CancellationToken cancellationToken = default);
    Task<TypedExplanationResultDto> ExplainSupplierScoreAsync(SupplierExplanationContextDto context, CancellationToken cancellationToken = default);
    Task<TypedExplanationResultDto> ExplainShiftProposalAsync(ShiftProposalExplanationContextDto context, CancellationToken cancellationToken = default);
    Task<TypedExplanationResultDto> ExplainAnomalyAsync(AnomalyExplanationContextDto context, CancellationToken cancellationToken = default);
}
