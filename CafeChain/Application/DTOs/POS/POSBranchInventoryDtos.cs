namespace CafeChain.Application.DTOs.POS
{
    /// <summary>
    /// Issue #96 — one StoreInventory row for POS “Kho chi nhánh” (read-only).
    /// </summary>
    public class POSBranchInventoryItemDto
    {
        public int StoreInventoryId { get; set; }
        public int StoreId { get; set; }

        /// <summary>Ingredient | Recipe | PreparedItem</summary>
        public string ItemType { get; set; } = string.Empty;

        public int ItemId { get; set; }
        public int? LegacyRecipeId { get; set; }
        public int? PreparedItemId { get; set; }
        public bool IsLegacyUnmapped { get; set; }
        public string QuantitySemanticsStatus { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string? ItemCode { get; set; }

        /// <summary>Physical quantity currently recorded in StoreInventory.</summary>
        public decimal OnHandQty { get; set; }

        /// <summary>Legacy compatibility alias for OnHandQty.</summary>
        public decimal AvailableQty { get; set; }
        public decimal ReservedQty { get; set; }

        /// <summary>Quantity available for operations: OnHandQty - ReservedQty.</summary>
        public decimal UsableQty { get; set; }
        public string UnitName { get; set; } = "—";

        /// <summary>Always null in #96 — MinStockLevel not in schema yet.</summary>
        public decimal? MinStockLevel { get; set; }

        /// <summary>Always false in #96.</summary>
        public bool ThresholdConfigured { get; set; }

        /// <summary>Always “Chưa cấu hình ngưỡng tối thiểu” in #96.</summary>
        public string ThresholdStatus { get; set; } = "Chưa cấu hình ngưỡng tối thiểu";

        /// <summary>Display-only: Tồn âm | Hết hàng | Còn hàng</summary>
        public string QuantityStatus { get; set; } = string.Empty;

        public DateTime LastUpdated { get; set; }
    }

    public class POSBranchInventoryListDto
    {
        public int StoreId { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public List<POSBranchInventoryItemDto> Items { get; set; } = new();
    }
}
