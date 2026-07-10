using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Inventories
{
    /// <summary>
    /// Unit-domain physical conversion (Issue #110 / ADR-0005, ADR-0006).
    /// Does not require IngredientId. Packaging (bottle/can/pack) is not physical.
    /// </summary>
    public interface IPhysicalUnitConversionService
    {
        /// <summary>
        /// Convert quantity between units using global physical rules (kg↔g, l↔ml).
        /// Fail-closed for missing, incompatible, or invalid factors — never raw-quantity fallback.
        /// </summary>
        Task<ServiceResult<decimal>> ConvertAsync(
            decimal quantity,
            int fromUnitId,
            int toUnitId);
    }
}
