using CafeChain.Application.DTOs.AI;

namespace CafeChain.Application.Interfaces.AI;

public interface IPexelsClient
{
    Task<PexelsImageResultDTO> FindImageAsync(
        PexelsImageRequestDTO request,
        CancellationToken cancellationToken = default);
}
