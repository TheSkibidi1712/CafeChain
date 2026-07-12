using CafeChain.Application.DTOs.Inventories.Cutover;

namespace CafeChain.Application.Interfaces.Inventories
{
    /// <summary>Issue #124 — read-only database schema contract probe (no migrate/apply).</summary>
    public interface IInventorySchemaReadinessProbe
    {
        Task<InventorySchemaReadinessReport> ProbeAsync(CancellationToken cancellationToken = default);
    }
}
