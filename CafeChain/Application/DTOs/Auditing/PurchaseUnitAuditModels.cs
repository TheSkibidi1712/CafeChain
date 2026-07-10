using System;
using System.Collections.Generic;
using CafeChain.Application.DTOs.Costing;

namespace CafeChain.Application.DTOs.Auditing
{
    public enum PurchaseUnitRemediationClass
    {
        Complete = 0,
        SafeRemediationCandidate = 1,
        BusinessDecisionRequired = 2,
        InvalidConfiguration = 3
    }

    public sealed class PurchaseUnitAuditReport
    {
        /// <summary>Report generation timestamp (UTC). Volatile between runs.</summary>
        public DateTimeOffset GeneratedAtUtc { get; init; }

        /// <summary>Alias for tooling that expects GeneratedAt.</summary>
        public DateTimeOffset GeneratedAt => GeneratedAtUtc;

        public string SchemaVersion { get; init; } = "113.A.1";
        public string Mode { get; init; } = "ReadOnly";
        public IReadOnlyList<SupplierOfferAuditRow> Offers { get; init; } = Array.Empty<SupplierOfferAuditRow>();
        public IReadOnlyList<PrimarySupplierAuditRow> Primaries { get; init; } = Array.Empty<PrimarySupplierAuditRow>();
        public IReadOnlyList<PriceHistoryAuditRow> PriceHistories { get; init; } = Array.Empty<PriceHistoryAuditRow>();
        public IReadOnlyList<RecipeCostAuditRow> Recipes { get; init; } = Array.Empty<RecipeCostAuditRow>();
        public PurchaseUnitAuditSummary Summary { get; init; } = new();
    }

    public sealed class PurchaseUnitAuditSummary
    {
        public int OfferCount { get; init; }
        public int OfferComplete { get; init; }
        public int OfferSafeCandidate { get; init; }
        public int OfferBusinessDecision { get; init; }
        public int OfferInvalid { get; init; }
        public int IngredientsWithNoActivePrimary { get; init; }
        public int IngredientsWithMultipleActivePrimary { get; init; }
        public int RecipesComplete { get; init; }
        public int RecipesIncomplete { get; init; }
        public int PriceHistoryIssues { get; init; }
    }

    public sealed class SupplierOfferAuditRow
    {
        public int IngredientSupplierId { get; init; }
        public int IngredientId { get; init; }
        public string? IngredientCode { get; init; }
        public string? IngredientName { get; init; }
        public int SupplierId { get; init; }
        public string? SupplierCode { get; init; }
        public string? SupplierName { get; init; }
        public bool Active { get; init; }
        public bool IsPrimary { get; init; }
        public decimal CurrentPrice { get; init; }
        public decimal? PackageQuantity { get; init; }
        public int PackageUnitId { get; init; }
        public string? PackageUnitCode { get; init; }
        public string? PackageUnitType { get; init; }
        public bool PackageUnitActive { get; init; }
        public int BaseUnitId { get; init; }
        public string? BaseUnitCode { get; init; }
        public string? BaseUnitType { get; init; }
        public decimal? NormalizedPackageBaseQuantity { get; init; }
        public decimal? BaseUnitCost { get; init; }
        public bool PackageDefinitionComplete { get; init; }
        public CostCompletenessStatus CostingStatus { get; init; }
        public PurchaseUnitRemediationClass Classification { get; init; }

        /// <summary>
        /// True for SAFE_REMEDIATION_CANDIDATE / BUSINESS_DECISION_REQUIRED / INVALID.
        /// No automatic mutation is ever applied by Checkpoint A.
        /// </summary>
        public bool RequiresOwnerApproval { get; init; }

        public IReadOnlyList<string> CostIssueCodes { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> AuditIssueCodes { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();
    }

    public sealed class PrimarySupplierAuditRow
    {
        public int IngredientId { get; init; }
        public string? IngredientCode { get; init; }
        public string? IngredientName { get; init; }
        public int ActiveOfferCount { get; init; }
        public int ActivePrimaryCount { get; init; }
        public int? SelectedPrimaryOfferId { get; init; }
        public bool SelectedPrimaryComplete { get; init; }
        public IReadOnlyList<string> IssueCodes { get; init; } = Array.Empty<string>();
        public string Status { get; init; } = "";
    }

    public sealed class PriceHistoryAuditRow
    {
        public int IngredientSupplierId { get; init; }
        public int CurrentHistoryCount { get; init; }
        public bool MissingCurrentHistory { get; init; }
        public bool MultipleCurrentHistories { get; init; }
        public bool SnapshotMatchesOffer { get; init; }
        public bool IncompletePackageSnapshot { get; init; }
        public bool InvalidPrice { get; init; }
        public bool InactiveOrUnknownPackageUnit { get; init; }
        public IReadOnlyList<string> IssueCodes { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();
    }

    public sealed class RecipeCostAuditRow
    {
        public int RecipeId { get; init; }
        public string? RecipeCode { get; init; }
        public string? Name { get; init; }
        public CostCompletenessStatus Status { get; init; }
        public decimal? TotalCost { get; init; }
        public IReadOnlyList<string> CostIssueCodes { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> LineSummaries { get; init; } = Array.Empty<string>();
    }
}
