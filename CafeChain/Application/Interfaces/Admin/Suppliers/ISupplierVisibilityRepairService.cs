using CafeChain.Application.DTOs.Admin.Suppliers;

namespace CafeChain.Application.Interfaces.Admin.Suppliers;

public interface ISupplierVisibilityRepairService
{
    Task<SupplierVisibilityRepairReportDTO> DryRunAsync();
    Task<SupplierVisibilityRepairReportDTO> RepairSafeAsync();
}
