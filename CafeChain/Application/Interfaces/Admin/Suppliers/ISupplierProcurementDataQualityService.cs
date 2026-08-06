using CafeChain.Application.DTOs.Admin.Suppliers;

namespace CafeChain.Application.Interfaces.Admin.Suppliers;

public interface ISupplierProcurementDataQualityService
{
    Task<SupplierProcurementDataQualityReportDTO> InspectAsync(
        CancellationToken cancellationToken = default);
}
