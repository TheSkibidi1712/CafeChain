using System.ComponentModel.DataAnnotations;
using CafeChain.Application.DTOs.Inventories;

namespace CafeChain.Application.DTOs.Admin.Settings;

public static class NegativeInventoryLimitModes
{
    public const string Blocked = "BLOCKED";
    public const string Default = "DEFAULT";
    public const string Custom = "CUSTOM";
}

public sealed class NegativeInventorySettingsDTO
{
    public bool IsConfigurationValid { get; init; }
    public string? ConfigurationError { get; init; }
    public bool Enabled { get; init; }
    public bool ApprovalRequired { get; init; } = true;
    public decimal DefaultMaxNegativeQuantity { get; init; }
    public string PolicyVersion { get; init; } = string.Empty;
    public int PendingApprovalCount { get; init; }
    public IReadOnlyList<NegativeInventoryStoreItemDTO> Items { get; init; } = [];
}

public sealed class NegativeInventoryStoreItemDTO
{
    public int StoreInventoryId { get; init; }
    public int StoreId { get; init; }
    public string StoreName { get; init; } = string.Empty;
    public string ItemType { get; init; } = string.Empty;
    public int ItemId { get; init; }
    public string ItemCode { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public int BaseUnitId { get; init; }
    public string BaseUnitCode { get; init; } = string.Empty;
    public int DisplayUnitId { get; init; }
    public IReadOnlyList<InventoryUnitOptionDTO> UnitOptions { get; init; } = [];
    public bool StoreActive { get; init; }
    public bool ItemActive { get; init; }
    public decimal AvailableQty { get; init; }
    public decimal ReservedQty { get; init; }
    public decimal? MaxNegativeQty { get; init; }
    public decimal EffectiveMaxNegativeQty { get; init; }
    public string LimitMode { get; init; } = NegativeInventoryLimitModes.Default;
    public bool CanRequestNegative { get; init; }
    public string EligibilityText { get; init; } = string.Empty;
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class UpdateNegativeInventorySettingsDTO
{
    public bool Enabled { get; set; }

    [Range(typeof(decimal), "0", "999999999999999.999")]
    public decimal DefaultMaxNegativeQuantity { get; set; }

    public List<UpdateNegativeInventoryItemDTO> Items { get; set; } = [];
}

public sealed class UpdateNegativeInventoryItemDTO
{
    public int StoreInventoryId { get; set; }
    public int DisplayUnitId { get; set; }
    public string LimitMode { get; set; } = NegativeInventoryLimitModes.Default;
    public decimal? MaxNegativeQuantity { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class NegativeInventorySettingsUpdateResultDTO
{
    public string PolicyVersion { get; init; } = string.Empty;
    public bool Changed { get; init; }
}
