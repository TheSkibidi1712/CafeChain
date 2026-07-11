namespace CafeChain.Application.DTOs.Admin.InventoryThresholds
{
    public class InventoryThresholdItemDto
    {
        public int StoreInventoryId { get; set; }
        public int StoreId { get; set; }
        public string? StoreName { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemTypeLabel { get; set; } = string.Empty;
        public string? UnitCode { get; set; }
        public decimal AvailableQty { get; set; }
        public decimal ReservedQty { get; set; }
        public decimal? MinStockLevel { get; set; }
        public bool ThresholdConfigured => MinStockLevel.HasValue;
        public string ThresholdStatusLabel =>
            MinStockLevel.HasValue
                ? MinStockLevel.Value.ToString("N3")
                : "Chưa cấu hình ngưỡng tối thiểu";
        public DateTime LastUpdated { get; set; }
    }

    public class InventoryThresholdListResultDto
    {
        public int SelectedStoreId { get; set; }
        public string? Search { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public List<InventoryStoreTabDto> Stores { get; set; } = new();
        public List<InventoryThresholdItemDto> Items { get; set; } = new();
    }

    public class InventoryStoreTabDto
    {
        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
    }
}
