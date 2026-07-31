using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Inventories;

public interface IReorderSuggestionConfirmationService
{
    Task<ServiceResult<ConfirmReorderSuggestionResultDto>> ConfirmAsync(
        ConfirmReorderSuggestionRequest request,
        AdminActorContext actor,
        CancellationToken cancellationToken = default);
}
