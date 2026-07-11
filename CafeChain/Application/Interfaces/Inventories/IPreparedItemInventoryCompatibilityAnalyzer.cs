using CafeChain.Application.DTOs.Inventories;

namespace CafeChain.Application.Interfaces.Inventories
{
    /// <summary>Read-only dry-run analyzer. It never writes or applies a mapping.</summary>
    public interface IPreparedItemInventoryCompatibilityAnalyzer
    {
        Task<PreparedItemInventoryCompatibilityReport> AnalyzeAsync(
            int storeInventoryId,
            int proposedPreparedItemId);
    }
}
