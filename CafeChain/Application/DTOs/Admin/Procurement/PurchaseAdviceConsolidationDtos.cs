using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Models.Enums.Inventory;

namespace CafeChain.Application.DTOs.Admin.Procurement;

public sealed class PurchaseAdviceConsolidationFilterDto
{
    public int? StoreId { get; set; }
    public int? AreaId { get; set; }
    public string? Status { get; set; }
    public DateTime? NeededByDate { get; set; }
    public int? IngredientId { get; set; }
    public string? Priority { get; set; }
    public int? SupplierId { get; set; }
}

public sealed class PurchaseAdviceConsolidationLineDto
{
    public int PurchaseAdviceLineId { get; set; }
    public int PurchaseAdviceId { get; set; }
    public string AdviceNumber { get; set; } = string.Empty;
    public string AdviceStatus { get; set; } = string.Empty;
    public int StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public int? AreaId { get; set; }
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public decimal RequestedPurchaseBaseQuantity { get; set; }
    public decimal AllocatedToPoBaseQuantity { get; set; }
    public decimal ClosedBaseQuantity { get; set; }
    public decimal RemainingToOrderBaseQuantity { get; set; }
    public int BaseUnitId { get; set; }
    public string BaseUnitName { get; set; } = string.Empty;
    public decimal? RequestedProcurementQuantity { get; set; }
    public decimal? AllocatedToPoProcurementQuantity { get; set; }
    public decimal? ClosedProcurementQuantity { get; set; }
    public decimal? RemainingToOrderProcurementQuantity { get; set; }
    public int? ProcurementUnitId { get; set; }
    public string? ProcurementUnitName { get; set; }
    public DateTime NeededByDate { get; set; }
    public string Priority { get; set; } = string.Empty;
    public int RestockRequestId { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public IReadOnlyList<int> CompatibleSupplierIds { get; set; } = Array.Empty<int>();
}

public sealed class PurchaseAdviceConsolidationOptionDto
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
}

public sealed class PurchaseAdviceConsolidationPageDto
{
    public PurchaseAdviceConsolidationFilterDto Filter { get; set; } = new();
    public IReadOnlyList<PurchaseAdviceConsolidationLineDto> Lines { get; set; } = Array.Empty<PurchaseAdviceConsolidationLineDto>();
    public IReadOnlyList<PurchaseAdviceConsolidationOptionDto> Stores { get; set; } = Array.Empty<PurchaseAdviceConsolidationOptionDto>();
    public IReadOnlyList<PurchaseAdviceConsolidationOptionDto> Areas { get; set; } = Array.Empty<PurchaseAdviceConsolidationOptionDto>();
    public IReadOnlyList<PurchaseAdviceConsolidationOptionDto> Ingredients { get; set; } = Array.Empty<PurchaseAdviceConsolidationOptionDto>();
    public IReadOnlyList<PurchaseAdviceConsolidationOptionDto> Suppliers { get; set; } = Array.Empty<PurchaseAdviceConsolidationOptionDto>();
    public IReadOnlyList<PurchaseAdviceOfferDto> Offers { get; set; } = Array.Empty<PurchaseAdviceOfferDto>();
    public AdminActorContext Actor { get; set; } = new();
}

public sealed class PurchaseAdviceOfferDto
{
    public int IngredientSupplierId { get; set; }
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public int IngredientId { get; set; }
    public int? PackageUnitId { get; set; }
    public string PackageUnitName { get; set; } = string.Empty;
    public decimal? PackageQuantity { get; set; }
    public decimal PackageBaseQuantity { get; set; }
    public decimal? PackageProcurementQuantity { get; set; }
    public int? ProcurementUnitId { get; set; }
    public string? ProcurementUnitName { get; set; }
    public int MinimumOrderPackageCount { get; set; }
    public int LeadTimeDays { get; set; }
    public decimal CurrentPackagePrice { get; set; }
    public bool AllowsLoosePurchase { get; set; }
    public decimal? CurrentProcurementUnitPrice { get; set; }
    public int? LooseProcurementUnitId { get; set; }
    public string? LooseProcurementUnitName { get; set; }
    public string Currency { get; set; } = "VND";
    public string? Specification { get; set; }
}

public class PurchaseAdviceConsolidationPreviewRequest
{
    public int SupplierId { get; set; }
    public List<PurchaseAdviceConsolidationSelectionRequest> Lines { get; set; } = new();
}

public sealed class PurchaseAdviceConsolidationSelectionRequest
{
    public int PurchaseAdviceLineId { get; set; }
    public int IngredientSupplierId { get; set; }
    public PurchaseMode PurchaseMode { get; set; } = PurchaseMode.Packaged;
    public int? PackageCount { get; set; }
    public decimal? OrderedProcurementQuantity { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class PurchaseAdviceConsolidationPreviewDto
{
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public IReadOnlyList<PurchaseAdviceConsolidationGroupDto> Groups { get; set; } = Array.Empty<PurchaseAdviceConsolidationGroupDto>();
    public decimal TotalAmount { get; set; }
    public int StoreCount { get; set; }
    public int LineCount { get; set; }
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}

public sealed class PurchaseAdviceConsolidationGroupDto
{
    public PurchaseMode PurchaseMode { get; set; } = PurchaseMode.Packaged;
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public int IngredientSupplierId { get; set; }
    public int? PackageUnitId { get; set; }
    public string PackageUnitName { get; set; } = string.Empty;
    public decimal? PackageQuantity { get; set; }
    public decimal PackageBaseQuantity { get; set; }
    public decimal? PackageProcurementQuantity { get; set; }
    public decimal? PackagePriceSnapshot { get; set; }
    public decimal? UnitPricePerProcurementUnit { get; set; }
    public string Currency { get; set; } = "VND";
    public string? Specification { get; set; }
    public int LeadTimeDays { get; set; }
    public int MinimumOrderPackageCount { get; set; }
    public int? PackageCount { get; set; }
    public decimal DemandCoveredBaseQuantity { get; set; }
    public decimal OrderedBaseQuantity { get; set; }
    public decimal RoundingSurplusBaseQuantity { get; set; }
    public decimal? DemandCoveredProcurementQuantity { get; set; }
    public decimal? OrderedProcurementQuantity { get; set; }
    public decimal? RoundingSurplusProcurementQuantity { get; set; }
    public int? ProcurementUnitId { get; set; }
    public string? ProcurementUnitName { get; set; }
    // Compatibility alias for existing views/controllers; always equals OrderedBaseQuantity.
    public decimal AllocatedBaseQuantity { get; set; }
    public decimal LineTotal { get; set; }
    public IReadOnlyList<PurchaseAdviceConsolidationAllocationDto> Allocations { get; set; } = Array.Empty<PurchaseAdviceConsolidationAllocationDto>();
}

public sealed class PurchaseAdviceConsolidationAllocationDto
{
    public PurchaseMode PurchaseMode { get; set; } = PurchaseMode.Packaged;
    public int PurchaseAdviceLineId { get; set; }
    public string AdviceNumber { get; set; } = string.Empty;
    public int StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public int RestockRequestId { get; set; }
    public int? SuggestedPackageCount { get; set; }
    public int? PackageCount { get; set; }
    public decimal DemandCoveredBaseQuantity { get; set; }
    public decimal OrderedBaseQuantity { get; set; }
    public decimal RoundingSurplusBaseQuantity { get; set; }
    public decimal? DemandCoveredProcurementQuantity { get; set; }
    public decimal? OrderedProcurementQuantity { get; set; }
    public decimal? RoundingSurplusProcurementQuantity { get; set; }
    public int? ProcurementUnitId { get; set; }
    public string? ProcurementUnitName { get; set; }
    // Compatibility alias for existing batch creation; always equals OrderedBaseQuantity.
    public decimal AllocatedBaseQuantity { get; set; }
    public decimal RemainingBeforeAllocation { get; set; }
    public decimal? RemainingProcurementBeforeAllocation { get; set; }
    public DateTime NeededByDate { get; set; }
    public string LineRowVersion { get; set; } = string.Empty;
}
