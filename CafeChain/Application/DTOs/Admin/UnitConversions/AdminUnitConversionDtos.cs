using System.Collections.Generic;

namespace CafeChain.Application.DTOs.Admin.UnitConversions
{
    /// <summary>#127 Physical standard row (read-only from registry).</summary>
    public class PhysicalStandardDto
    {
        public string FromCode { get; set; } = "";
        public string ToCode { get; set; } = "";
        public decimal FromQuantity { get; set; } = 1m;
        public decimal ToQuantity { get; set; }
        public string Dimension { get; set; } = "";
        public string Source { get; set; } = "Hệ thống";
        public bool Editable { get; set; }
        public string DisplayText { get; set; } = "";
    }

    public class AdminUnitConversionEvaluateRequest
    {
        public int? UnitConversionId { get; set; }
        public int IngredientId { get; set; }
        public int FromUnitId { get; set; }
        public decimal FromQuantity { get; set; }
        public int ToUnitId { get; set; }
        public decimal ToQuantity { get; set; }
        public bool PackageConflictAcknowledged { get; set; }
    }

    public class AdminUnitConversionEvaluateResult
    {
        public bool IsValid { get; set; }
        public string? ErrorCode { get; set; }
        public string? Message { get; set; }

        public bool IsPhysicalStandard { get; set; }
        public bool HasPhysicalConflict { get; set; }
        public decimal? PhysicalExpectedFactor { get; set; }

        public bool IsCrossDimension { get; set; }
        public bool IsMassVolumeCross { get; set; }

        public bool HasPackageConflict { get; set; }
        public bool RequiresPackageAcknowledgement { get; set; }
        public decimal? PrimaryPackageQuantity { get; set; }
        public string? PrimaryPackageUnitCode { get; set; }
        public string? PrimaryPackageUnitName { get; set; }
        public decimal? PrimaryPackagePrice { get; set; }
        public decimal? ProposedPackageLikeQuantity { get; set; }
        public int? PrimarySupplierId { get; set; }
        public string? PrimarySupplierName { get; set; }

        public decimal? Factor { get; set; }
        public decimal? ReverseFactor { get; set; }
        public string? FromUnitCode { get; set; }
        public string? ToUnitCode { get; set; }
        public string? FromUnitName { get; set; }
        public string? ToUnitName { get; set; }
        public string? FromDimension { get; set; }
        public string? ToDimension { get; set; }
        public bool FromIsPackagingCount { get; set; }
        public bool ToIsPackagingCount { get; set; }

        public List<string> Warnings { get; set; } = new();
        public List<string> Codes { get; set; } = new();
    }

    public class AdminUnitConversionRowDto
    {
        public int UnitConversionId { get; set; }
        public int IngredientId { get; set; }
        public int FromUnitId { get; set; }
        public string FromUnitCode { get; set; } = "";
        public string FromUnitName { get; set; } = "";
        public decimal FromQuantity { get; set; }
        public int ToUnitId { get; set; }
        public string ToUnitCode { get; set; } = "";
        public string ToUnitName { get; set; } = "";
        public decimal ToQuantity { get; set; }
        public bool Active { get; set; }
        public decimal Factor { get; set; }
        public decimal ReverseFactor { get; set; }
        public string StatusKey { get; set; } = "ok";
        public string StatusLabel { get; set; } = "Hợp lệ";
        public bool IsCrossDimensionMassVolume { get; set; }
        public bool HasPackageConflict { get; set; }
        public bool AllowEdit { get; set; } = true;
    }

    public class AdminPackageSummaryDto
    {
        public int? IngredientSupplierId { get; set; }
        public int? SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public decimal? PackageQuantity { get; set; }
        public string? PackageUnitCode { get; set; }
        public string? PackageUnitName { get; set; }
        public decimal? PackagePrice { get; set; }
        public decimal? BaseUnitCost { get; set; }
        public string? BaseUnitCode { get; set; }
        public bool IsPrimary { get; set; }
        public bool IsComplete { get; set; }
        public string? IncompleteReason { get; set; }
        public string DisplayPackage { get; set; } = "—";
        public string DisplayBaseCost { get; set; } = "—";
    }

    public class AdminIngredientConversionGroupDto
    {
        public int IngredientId { get; set; }
        public string IngredientCode { get; set; } = "";
        public string IngredientName { get; set; } = "";
        public int BaseUnitId { get; set; }
        public string BaseUnitCode { get; set; } = "";
        public string BaseUnitName { get; set; } = "";
        public List<AdminUnitConversionRowDto> Conversions { get; set; } = new();
        public AdminPackageSummaryDto? PrimaryPackage { get; set; }
        public string GroupStatusKey { get; set; } = "ok";
        public string GroupStatusLabel { get; set; } = "Hợp lệ";
        public bool HasPackageConflict { get; set; }
        public bool HasReviewRows { get; set; }
    }

    public class AdminUnitConversionIndexDto
    {
        public List<PhysicalStandardDto> PhysicalStandards { get; set; } = new();
        public List<AdminIngredientConversionGroupDto> Groups { get; set; } = new();
        public string? Search { get; set; }
        public string? StatusFilter { get; set; }
        public int TotalGroups { get; set; }
        public int ConflictGroupCount { get; set; }
    }

    public class AdminIngredientOptionDto
    {
        public int IngredientId { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string BaseUnitCode { get; set; } = "";
    }

    public class AdminUnitOptionDto
    {
        public int UnitId { get; set; }
        public string UnitCode { get; set; } = "";
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public bool IsPackagingCount { get; set; }
        public bool IsPhysicalStandard { get; set; }
    }
}
