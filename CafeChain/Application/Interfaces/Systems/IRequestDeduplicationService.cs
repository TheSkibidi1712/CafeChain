using CafeChain.Application.DTOs.Systems;
using CafeChain.Models.Systems;

namespace CafeChain.Application.Interfaces.Systems
{
    public interface IRequestDeduplicationService
    {
        Task<RequestDeduplicationBeginResult> BeginAsync(
            string? requestKey,
            string actionName,
            int staffId,
            object requestBody,
            int? referenceId = null);

        Task MarkSuccessAsync(
            RequestDeduplication entry,
            int referenceId,
            object responseBody);

        Task MarkFailedAsync(
            RequestDeduplication entry,
            object responseBody);
    }
}
