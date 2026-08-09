using CafeChain.Application.DTOs.Admin.Production;

namespace CafeChain.Application.Interfaces.Admin.Production;

public interface IProductionLegacyAuditService
{
    Task<ProductionLegacyAuditReportDto> DryRunAsync();
}
