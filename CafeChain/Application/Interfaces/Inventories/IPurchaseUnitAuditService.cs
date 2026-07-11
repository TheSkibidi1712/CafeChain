using System.Threading.Tasks;
using CafeChain.Application.DTOs.Auditing;

namespace CafeChain.Application.Interfaces.Inventories
{
    /// <summary>
    /// Read-only purchase/unit data audit for Issue #113 Checkpoint A.
    /// Uses #117 IEstimatedBomCostService — never mutates data.
    /// </summary>
    public interface IPurchaseUnitAuditService
    {
        Task<PurchaseUnitAuditReport> RunAuditAsync();
    }
}
