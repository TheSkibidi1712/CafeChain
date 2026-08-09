using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Inventories;

public interface IPurchaseOrderConsistencyService
{
    Task<PurchaseOrderConsistencyReportDto> DryRunAsync();
    Task<ServiceResult<PurchaseOrderConsistencyReportDto>> RepairSafeAsync(int actorStaffId);
}
