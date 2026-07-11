using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Auditing;
using CafeChain.Application.DTOs.Costing;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CafeChain.Application.Services.Inventories
{
    /// <summary>
    /// Issue #113 Checkpoint A — read-only purchase/unit remediation audit.
    /// Classification uses structured fields only (never Ingredient.Name inference).
    /// </summary>
    public class PurchaseUnitAuditService : IPurchaseUnitAuditService
    {
        private readonly AppDbContext _context;
        private readonly IEstimatedBomCostService _estimatedBomCost;
        private readonly ILogger<PurchaseUnitAuditService> _logger;

        public PurchaseUnitAuditService(
            AppDbContext context,
            IEstimatedBomCostService estimatedBomCost,
            ILogger<PurchaseUnitAuditService> logger)
        {
            _context = context;
            _estimatedBomCost = estimatedBomCost;
            _logger = logger;
        }

        public async Task<PurchaseUnitAuditReport> RunAuditAsync()
        {
            _logger.LogInformation("[PurchaseUnitAudit] Starting read-only audit (no SaveChanges).");

            var offers = await AuditOffersAsync();
            var primaries = await AuditPrimariesAsync(offers);
            var histories = await AuditPriceHistoriesAsync();
            var recipes = await AuditRecipesAsync();

            var report = new PurchaseUnitAuditReport
            {
                GeneratedAtUtc = DateTimeOffset.UtcNow,
                SchemaVersion = "113.A.1",
                Mode = "ReadOnly",
                Offers = offers,
                Primaries = primaries,
                PriceHistories = histories,
                Recipes = recipes,
                Summary = new PurchaseUnitAuditSummary
                {
                    OfferCount = offers.Count,
                    OfferComplete = offers.Count(o => o.Classification == PurchaseUnitRemediationClass.Complete),
                    OfferSafeCandidate = offers.Count(o => o.Classification == PurchaseUnitRemediationClass.SafeRemediationCandidate),
                    OfferBusinessDecision = offers.Count(o => o.Classification == PurchaseUnitRemediationClass.BusinessDecisionRequired),
                    OfferInvalid = offers.Count(o => o.Classification == PurchaseUnitRemediationClass.InvalidConfiguration),
                    IngredientsWithNoActivePrimary = primaries.Count(p => p.ActivePrimaryCount == 0 && p.ActiveOfferCount > 0),
                    IngredientsWithMultipleActivePrimary = primaries.Count(p => p.ActivePrimaryCount > 1),
                    RecipesComplete = recipes.Count(r => r.Status == CostCompletenessStatus.Complete),
                    RecipesIncomplete = recipes.Count(r => r.Status == CostCompletenessStatus.Incomplete),
                    PriceHistoryIssues = histories.Count(h => h.IssueCodes.Count > 0)
                }
            };

            _logger.LogInformation(
                "[PurchaseUnitAudit] Done. Offers={Offers} Complete={Complete} Business={Biz} Invalid={Inv} RecipesComplete={Rc}",
                report.Summary.OfferCount,
                report.Summary.OfferComplete,
                report.Summary.OfferBusinessDecision,
                report.Summary.OfferInvalid,
                report.Summary.RecipesComplete);

            return report;
        }

        private async Task<List<SupplierOfferAuditRow>> AuditOffersAsync()
        {
            var rows = await _context.IngredientSuppliers
                .AsNoTracking()
                .Include(s => s.Ingredient).ThenInclude(i => i.BaseUnit)
                .Include(s => s.Supplier)
                .Include(s => s.Unit)
                .OrderBy(s => s.IngredientId)
                .ThenBy(s => s.IngredientSupplierId)
                .ToListAsync();

            // Preload active primaries per ingredient for classification (no mutation)
            var activeByIngredient = rows
                .Where(r => r.Active)
                .GroupBy(r => r.IngredientId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var result = new List<SupplierOfferAuditRow>();
            foreach (var offer in rows)
            {
                var cost = await _estimatedBomCost.ResolveIngredientBaseUnitCostAsync(offer.IngredientId);
                // Costing is primary-scoped; for non-primary offers re-evaluate package completeness locally via cost service when this offer is the sole primary path is incomplete.

                var costCodes = cost.Issues.Select(i => i.Code).Distinct().ToList();
                var auditCodes = new List<string>();
                var messages = cost.Issues.Select(i => i.Message).ToList();

                var packageUnitActive = offer.Unit?.Active == true;
                var packageDefComplete =
                    offer.Active
                    && offer.PackageQuantity.HasValue
                    && offer.PackageQuantity.Value > 0
                    && offer.CurrentPrice > 0
                    && packageUnitActive
                    && !PackageUnitCodes.IsRejectedCommercialPackaging(offer.Unit?.UnitCode);

                // Structured package metrics when this specific offer has complete package fields
                decimal? normalizedBaseQty = null;
                decimal? baseUnitCost = null;
                if (packageDefComplete
                    && offer.Ingredient != null
                    && offer.PackageQuantity.HasValue)
                {
                    // Prefer #117 result when this offer is the selected primary and complete
                    if (cost.IsComplete
                        && cost.IngredientSupplierId == offer.IngredientSupplierId)
                    {
                        normalizedBaseQty = cost.BaseQuantityPerPackage;
                        baseUnitCost = cost.BaseUnitCost;
                    }
                }

                if (!packageUnitActive && offer.Unit != null)
                {
                    auditCodes.Add(CostIssueCodes.InactivePackageUnit);
                    messages.Add($"Package unit #{offer.UnitId} inactive.");
                }

                if (offer.CurrentPrice <= 0)
                {
                    auditCodes.Add(CostIssueCodes.ZeroPackagePrice);
                    messages.Add("CurrentPrice <= 0.");
                }

                if (offer.PackageQuantity.HasValue && offer.PackageQuantity.Value <= 0)
                {
                    auditCodes.Add(CostIssueCodes.InvalidPackageQuantity);
                    messages.Add("PackageQuantity <= 0.");
                }

                if (PackageUnitCodes.IsRejectedCommercialPackaging(offer.Unit?.UnitCode))
                {
                    auditCodes.Add(CostIssueCodes.RejectedPackagingUnit);
                    messages.Add($"Commercial packaging unit '{offer.Unit?.UnitCode}' is not valid content unit.");
                }

                activeByIngredient.TryGetValue(offer.IngredientId, out var actives);
                actives ??= new List<Models.Inventories.Suppliers.IngredientSupplier>();
                var activePrimaries = actives.Where(a => a.IsPrimary).ToList();
                if (activePrimaries.Count > 1)
                {
                    auditCodes.Add(CostIssueCodes.MultiplePrimarySuppliers);
                    messages.Add("Multiple Active primary offers for ingredient.");
                }

                // Sole active offer, package complete, not primary → safe candidate (owner must still approve)
                var soleCompleteNotPrimary =
                    offer.Active
                    && !offer.IsPrimary
                    && actives.Count == 1
                    && packageDefComplete
                    && !PackageUnitCodes.IsRejectedCommercialPackaging(offer.Unit?.UnitCode);

                if (soleCompleteNotPrimary)
                {
                    auditCodes.Add(PurchaseUnitAuditIssueCodes.SoleCompleteOfferNotPrimary);
                    messages.Add("Sole Active offer has complete package metadata but IsPrimary=false.");
                }

                var classification = ClassifyOffer(
                    offer.Active,
                    packageDefComplete,
                    cost.IsComplete && cost.IngredientSupplierId == offer.IngredientSupplierId,
                    soleCompleteNotPrimary,
                    activePrimaries.Count > 1,
                    offer.CurrentPrice <= 0,
                    !packageUnitActive,
                    PackageUnitCodes.IsRejectedCommercialPackaging(offer.Unit?.UnitCode),
                    offer.PackageQuantity == null || offer.PackageQuantity <= 0);

                // When offer is primary and costing complete, mark COMPLETE even if other messages empty
                if (offer.Active && offer.IsPrimary && cost.IsComplete
                    && cost.IngredientSupplierId == offer.IngredientSupplierId)
                {
                    classification = PurchaseUnitRemediationClass.Complete;
                    normalizedBaseQty = cost.BaseQuantityPerPackage;
                    baseUnitCost = cost.BaseUnitCost;
                }

                result.Add(new SupplierOfferAuditRow
                {
                    IngredientSupplierId = offer.IngredientSupplierId,
                    IngredientId = offer.IngredientId,
                    IngredientCode = offer.Ingredient?.Code,
                    IngredientName = offer.Ingredient?.Name,
                    SupplierId = offer.SupplierId,
                    SupplierCode = offer.Supplier?.Code,
                    SupplierName = offer.Supplier?.Name,
                    Active = offer.Active,
                    IsPrimary = offer.IsPrimary,
                    CurrentPrice = offer.CurrentPrice,
                    PackageQuantity = offer.PackageQuantity,
                    PackageUnitId = offer.UnitId,
                    PackageUnitCode = offer.Unit?.UnitCode,
                    PackageUnitType = offer.Unit?.Type.ToString(),
                    PackageUnitActive = packageUnitActive,
                    BaseUnitId = offer.Ingredient?.BaseUnitId ?? 0,
                    BaseUnitCode = offer.Ingredient?.BaseUnit?.UnitCode,
                    BaseUnitType = offer.Ingredient?.BaseUnit?.Type.ToString(),
                    NormalizedPackageBaseQuantity = normalizedBaseQty,
                    BaseUnitCost = baseUnitCost,
                    PackageDefinitionComplete = packageDefComplete,
                    CostingStatus = cost.IsComplete && cost.IngredientSupplierId == offer.IngredientSupplierId
                        ? CostCompletenessStatus.Complete
                        : CostCompletenessStatus.Incomplete,
                    Classification = classification,
                    RequiresOwnerApproval = classification != PurchaseUnitRemediationClass.Complete,
                    CostIssueCodes = costCodes.OrderBy(c => c, StringComparer.Ordinal).ToList(),
                    AuditIssueCodes = auditCodes.Distinct().OrderBy(c => c, StringComparer.Ordinal).ToList(),
                    Messages = messages.Distinct().OrderBy(m => m, StringComparer.Ordinal).ToList()
                });
            }

            // Deterministic ordering: IngredientId, IngredientSupplierId
            return result
                .OrderBy(r => r.IngredientId)
                .ThenBy(r => r.IngredientSupplierId)
                .ToList();
        }

        private static PurchaseUnitRemediationClass ClassifyOffer(
            bool active,
            bool packageDefComplete,
            bool costingCompleteForThisOffer,
            bool soleCompleteNotPrimary,
            bool multiplePrimary,
            bool zeroPrice,
            bool inactiveUnit,
            bool rejectedPackaging,
            bool missingOrInvalidPackageQty)
        {
            if (multiplePrimary || zeroPrice || inactiveUnit || rejectedPackaging
                || (active && packageDefComplete == false && missingOrInvalidPackageQty == false && zeroPrice))
            {
                // Invalid structural problems first
            }

            if (multiplePrimary || zeroPrice || inactiveUnit || rejectedPackaging)
                return PurchaseUnitRemediationClass.InvalidConfiguration;

            if (costingCompleteForThisOffer)
                return PurchaseUnitRemediationClass.Complete;

            if (soleCompleteNotPrimary)
                return PurchaseUnitRemediationClass.SafeRemediationCandidate;

            // Incomplete package / no primary selection path
            if (!packageDefComplete || missingOrInvalidPackageQty || !active)
                return PurchaseUnitRemediationClass.BusinessDecisionRequired;

            return PurchaseUnitRemediationClass.BusinessDecisionRequired;
        }

        private async Task<List<PrimarySupplierAuditRow>> AuditPrimariesAsync(
            List<SupplierOfferAuditRow> offers)
        {
            var ingredients = await _context.Ingredients
                .AsNoTracking()
                .OrderBy(i => i.IngredientId)
                .Select(i => new { i.IngredientId, i.Code, i.Name })
                .ToListAsync();

            var result = new List<PrimarySupplierAuditRow>();
            foreach (var ing in ingredients)
            {
                var activeOffers = offers.Where(o => o.IngredientId == ing.IngredientId && o.Active).ToList();
                var primaries = activeOffers.Where(o => o.IsPrimary).ToList();
                var codes = new List<string>();
                string status;

                int? selectedId = null;
                var selectedComplete = false;

                if (activeOffers.Count == 0)
                {
                    codes.Add(PurchaseUnitAuditIssueCodes.NoActiveOffer);
                    status = "NO_ACTIVE_OFFER";
                }
                else if (primaries.Count == 0)
                {
                    codes.Add(CostIssueCodes.MissingSupplierOffer);
                    status = "NO_ACTIVE_PRIMARY";
                    if (activeOffers.Count == 1 && activeOffers[0].PackageDefinitionComplete)
                        codes.Add(PurchaseUnitAuditIssueCodes.SoleCompleteOfferNotPrimary);
                }
                else if (primaries.Count > 1)
                {
                    codes.Add(CostIssueCodes.MultiplePrimarySuppliers);
                    status = "MULTIPLE_ACTIVE_PRIMARY";
                }
                else
                {
                    selectedId = primaries[0].IngredientSupplierId;
                    selectedComplete = primaries[0].Classification == PurchaseUnitRemediationClass.Complete
                        || primaries[0].PackageDefinitionComplete;
                    status = selectedComplete ? "PRIMARY_COMPLETE" : "PRIMARY_INCOMPLETE";
                    if (!selectedComplete)
                        codes.AddRange(primaries[0].CostIssueCodes);
                }

                result.Add(new PrimarySupplierAuditRow
                {
                    IngredientId = ing.IngredientId,
                    IngredientCode = ing.Code,
                    IngredientName = ing.Name,
                    ActiveOfferCount = activeOffers.Count,
                    ActivePrimaryCount = primaries.Count,
                    SelectedPrimaryOfferId = selectedId,
                    SelectedPrimaryComplete = selectedComplete && primaries.Count == 1,
                    IssueCodes = codes.Distinct().OrderBy(c => c, StringComparer.Ordinal).ToList(),
                    Status = status
                });
            }

            return result.OrderBy(r => r.IngredientId).ToList();
        }

        private async Task<List<PriceHistoryAuditRow>> AuditPriceHistoriesAsync()
        {
            var offers = await _context.IngredientSuppliers
                .AsNoTracking()
                .Select(o => new
                {
                    o.IngredientSupplierId,
                    o.CurrentPrice,
                    o.PackageQuantity,
                    o.UnitId
                })
                .ToListAsync();

            var histories = await _context.IngredientSupplierPriceHistories
                .AsNoTracking()
                .Include(h => h.PackageUnit)
                .ToListAsync();

            var byOffer = histories.GroupBy(h => h.IngredientSupplierId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var result = new List<PriceHistoryAuditRow>();
            foreach (var offer in offers)
            {
                byOffer.TryGetValue(offer.IngredientSupplierId, out var list);
                list ??= new List<Models.Inventories.Suppliers.IngredientSupplierPriceHistory>();

                var current = list.Where(h => h.IsCurrent).ToList();
                var codes = new List<string>();
                var messages = new List<string>();

                var missingCurrent = current.Count == 0;
                var multipleCurrent = current.Count > 1;
                if (missingCurrent)
                {
                    codes.Add(PurchaseUnitAuditIssueCodes.PriceHistoryMissingCurrent);
                    messages.Add("No IsCurrent price-history row.");
                }
                if (multipleCurrent)
                {
                    codes.Add(PurchaseUnitAuditIssueCodes.PriceHistoryMultipleCurrent);
                    messages.Add("Multiple IsCurrent price-history rows.");
                }

                var incompleteSnapshot = false;
                var snapshotMatch = true;
                var invalidPrice = false;
                var inactiveUnit = false;

                if (current.Count == 1)
                {
                    var h = current[0];
                    if (h.Price <= 0)
                    {
                        invalidPrice = true;
                        codes.Add(PurchaseUnitAuditIssueCodes.PriceHistoryInvalidPrice);
                        messages.Add("Current history price <= 0.");
                    }

                    if (!h.PackageQuantity.HasValue || h.PackageQuantity <= 0 || !h.PackageUnitId.HasValue)
                    {
                        incompleteSnapshot = true;
                        codes.Add(PurchaseUnitAuditIssueCodes.PriceHistoryIncompleteSnapshot);
                        messages.Add("Current history missing package snapshot.");
                    }

                    if (h.PackageUnitId.HasValue)
                    {
                        var unit = h.PackageUnit;
                        if (unit == null || !unit.Active)
                        {
                            inactiveUnit = true;
                            codes.Add(PurchaseUnitAuditIssueCodes.PriceHistoryInactivePackageUnit);
                            messages.Add("History PackageUnit missing or inactive.");
                        }
                    }

                    // Match offer when both sides have package data
                    var priceMatch = h.Price == offer.CurrentPrice;
                    var qtyMatch = h.PackageQuantity == offer.PackageQuantity
                        || (h.PackageQuantity == null && offer.PackageQuantity == null);
                    var unitMatch = (h.PackageUnitId ?? 0) == offer.UnitId
                        || (h.PackageUnitId == null && offer.PackageQuantity == null);
                    snapshotMatch = priceMatch && qtyMatch
                        && (h.PackageUnitId == null || h.PackageUnitId == offer.UnitId);

                    if (!snapshotMatch && current.Count == 1)
                    {
                        // Only flag mismatch when offer has complete package and history has values that differ
                        if (offer.PackageQuantity.HasValue && h.PackageQuantity.HasValue
                            && (h.PackageQuantity != offer.PackageQuantity
                                || h.PackageUnitId != offer.UnitId
                                || h.Price != offer.CurrentPrice))
                        {
                            codes.Add(PurchaseUnitAuditIssueCodes.PriceHistorySnapshotMismatch);
                            messages.Add("Current history package/price snapshot does not match offer.");
                            snapshotMatch = false;
                        }
                        else if (!priceMatch)
                        {
                            codes.Add(PurchaseUnitAuditIssueCodes.PriceHistorySnapshotMismatch);
                            messages.Add("Current history price does not match offer CurrentPrice.");
                            snapshotMatch = false;
                        }
                        else
                        {
                            snapshotMatch = true;
                        }
                    }
                }
                else
                {
                    snapshotMatch = false;
                }

                result.Add(new PriceHistoryAuditRow
                {
                    IngredientSupplierId = offer.IngredientSupplierId,
                    CurrentHistoryCount = current.Count,
                    MissingCurrentHistory = missingCurrent,
                    MultipleCurrentHistories = multipleCurrent,
                    SnapshotMatchesOffer = snapshotMatch,
                    IncompletePackageSnapshot = incompleteSnapshot,
                    InvalidPrice = invalidPrice,
                    InactiveOrUnknownPackageUnit = inactiveUnit,
                    IssueCodes = codes.Distinct().OrderBy(c => c, StringComparer.Ordinal).ToList(),
                    Messages = messages.OrderBy(m => m, StringComparer.Ordinal).ToList()
                });
            }

            return result.OrderBy(r => r.IngredientSupplierId).ToList();
        }

        private async Task<List<RecipeCostAuditRow>> AuditRecipesAsync()
        {
            var recipes = await _context.Recipes
                .AsNoTracking()
                .Where(r => r.Active && r.Status == "Active")
                .OrderBy(r => r.RecipeId)
                .Select(r => new { r.RecipeId, r.RecipeCode, r.Name })
                .ToListAsync();

            var result = new List<RecipeCostAuditRow>();
            foreach (var r in recipes)
            {
                var cost = await _estimatedBomCost.CalculateRecipeEstimatedCostAsync(r.RecipeId);
                result.Add(new RecipeCostAuditRow
                {
                    RecipeId = r.RecipeId,
                    RecipeCode = r.RecipeCode,
                    Name = r.Name,
                    Status = cost.Status,
                    TotalCost = cost.IsComplete ? cost.TotalCost : null,
                    CostIssueCodes = cost.Issues
                        .Select(i => i.Code)
                        .Distinct()
                        .OrderBy(c => c, StringComparer.Ordinal)
                        .ToList(),
                    LineSummaries = cost.Lines
                        .OrderBy(l => l.RecipeDetailId ?? 0)
                        .Select(l =>
                            $"Detail={l.RecipeDetailId} {l.ComponentKind} status={l.Status} cost={l.LineCost?.ToString() ?? "null"} {l.DisplaySummary}")
                        .ToList()
                });
            }

            return result.OrderBy(r => r.RecipeId).ToList();
        }
    }
}
