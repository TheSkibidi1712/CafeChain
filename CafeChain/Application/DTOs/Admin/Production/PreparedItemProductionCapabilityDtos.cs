namespace CafeChain.Application.DTOs.Admin.Production;

public sealed class PreparedItemProductionCapabilityPageDto
{
    public int StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string? Search { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public bool CanManageGlobalCapability { get; set; }
    public bool CanManageStoreCapability { get; set; }
    public List<PreparedItemProductionCapabilityItemDto> Items { get; set; } = [];
}

public sealed class PreparedItemProductionCapabilityItemDto
{
    public int PreparedItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string BaseUnitCode { get; set; } = string.Empty;
    public bool CanProduceGlobally { get; set; }
    public string? GlobalRowVersion { get; set; }
    public bool CanProduceAtStore { get; set; }
    public string? StoreRowVersion { get; set; }
    public bool HasCanonicalInventory { get; set; }
}
