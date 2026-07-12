using CafeChain.Application.DTOs.AI;

namespace CafeChain.Application.Interfaces.AI;

public interface IAIService
{
    Task<InventoryInputSuggestionResultDTO> SuggestInventoryInputAsync(InventoryInputSuggestionRequestDTO request, CancellationToken cancellationToken = default);
    Task<CategorySuggestionResultDTO> SuggestCategoriesAsync(CancellationToken cancellationToken = default);
    Task<SupplierSuggestionResultDTO> SuggestSupplierAsync(SupplierSuggestionRequestDTO request, CancellationToken cancellationToken = default);
    Task<OllamaHealthDTO> CheckHealthAsync(CancellationToken cancellationToken = default);
}
