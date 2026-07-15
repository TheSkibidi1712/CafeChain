using CafeChain.Application.DTOs.POS;

namespace CafeChain.Application.Interfaces.POS
{
    public interface IPOSCatalogSnapshotService
    {
        Task<POSCatalogSnapshotDto> BuildAsync(
            int storeId,
            DateTime asOfUtc,
            CancellationToken cancellationToken = default);
    }
}
