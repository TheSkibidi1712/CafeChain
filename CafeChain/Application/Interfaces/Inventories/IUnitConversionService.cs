using CafeChain.Application.Results;
using CafeChain.Application.DTOs.Inventories;

namespace CafeChain.Application.Interfaces.Inventories
{
    /// <summary>
    /// Shared unit conversion for POS availability, inventory deduction, and COGS.
    /// Never silently treats mismatched units as equal.
    /// </summary>
    public interface IUnitConversionService
    {
        /// <summary>
        /// Convert quantity from fromUnitId to ingredient BaseUnit (or explicit toUnitId).
        /// </summary>
        Task<ServiceResult<decimal>> ConvertAsync(
            int ingredientId,
            decimal quantity,
            int fromUnitId,
            int? toUnitId = null);

        /// <summary>
        /// Returns active, server-validated units that can be converted to the ingredient base unit.
        /// </summary>
        Task<ServiceResult<IReadOnlyList<InventoryUnitOptionDTO>>> GetActiveUnitOptionsAsync(
            int ingredientId,
            CancellationToken cancellationToken = default);
    }
}
