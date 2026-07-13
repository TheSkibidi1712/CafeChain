using CafeChain.Application.DTOs.AI;

namespace CafeChain.Application.Interfaces.AI;

public interface IComfyUIClient
{
    Task<ComfyUIImageResultDTO> GenerateImageAsync(
        ComfyUIImageRequestDTO request,
        CancellationToken cancellationToken = default);
}
