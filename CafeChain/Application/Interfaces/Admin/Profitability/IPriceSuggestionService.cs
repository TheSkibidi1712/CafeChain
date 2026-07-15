using CafeChain.Application.DTOs.Admin.Profitability;

namespace CafeChain.Application.Interfaces.Admin.Profitability
{
    public interface IPriceSuggestionService
    {
        PriceSuggestionResult Calculate(PriceSuggestionRequest request);
    }
}
