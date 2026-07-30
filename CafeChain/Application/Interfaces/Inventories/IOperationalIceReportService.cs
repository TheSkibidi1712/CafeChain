using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Inventories;

public interface IOperationalIceReportService
{
    Task<ServiceResult<OperationalIceReportDto>> BuildAsync(
        int iceAllocationId,
        CancellationToken cancellationToken = default);
}

public interface IOperationalIceReportPdfRenderer
{
    byte[] Render(OperationalIceReportDto report, DateTime generatedAtUtc);
}
