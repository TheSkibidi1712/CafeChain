using CafeChain.Application.Results;

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
    }
}
