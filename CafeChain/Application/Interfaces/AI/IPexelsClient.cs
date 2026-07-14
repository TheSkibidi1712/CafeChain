using CafeChain.Application.DTOs.AI;

namespace CafeChain.Application.Interfaces.AI;

public interface IPexelsClient
{
    Task<PexelsSearchResultDTO> SearchAsync(
        PexelsSearchRequestDTO request,
        CancellationToken cancellationToken = default);

    Task<PexelsImageResultDTO> DownloadPhotoAsync(
        long photoId,
        CancellationToken cancellationToken = default);
}
