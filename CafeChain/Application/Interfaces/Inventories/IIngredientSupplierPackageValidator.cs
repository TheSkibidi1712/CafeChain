using CafeChain.Application.Results;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Suppliers;

namespace CafeChain.Application.Interfaces.Inventories
{
    /// <summary>
    /// Package-definition validation for IngredientSupplier offers (#111).
    /// Does not compute cost completeness (#117).
    /// </summary>
    public interface IIngredientSupplierPackageValidator
    {
        /// <summary>
        /// Validate package definition fields for create/update.
        /// </summary>
        /// <param name="requirePackageQuantity">True for new Active offers, or when package/pricing fields are being edited.</param>
        Task<ServiceResult> ValidateAsync(
            int ingredientId,
            int supplierId,
            int unitId,
            decimal? packageQuantity,
            decimal currentPrice,
            bool isActive,
            bool requirePackageQuantity,
            int? excludeIngredientSupplierId = null);

        /// <summary>
        /// Package definition indicator — not cost completeness.
        /// </summary>
        Task<bool> HasCompletePackageDefinitionAsync(
            int ingredientId,
            int unitId,
            decimal? packageQuantity);

        Task<bool> HasCompletePackageDefinitionAsync(IngredientSupplier offer);

        Task<SupplierPackageReadinessResult> EvaluateReadinessAsync(
            IngredientSupplier offer);

        Task<IReadOnlyDictionary<int, SupplierPackageReadinessResult>> EvaluateReadinessAsync(
            IEnumerable<IngredientSupplier> offers);

        Task<SupplierPackageReadinessResult> EvaluateProcurementEligibilityAsync(
            IngredientSupplier offer,
            PurchaseMode purchaseMode,
            int? storeId = null);
    }
}
