using CafeChain.Application.DTOs.AI;

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
}
