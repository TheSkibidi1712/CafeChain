namespace CafeChain.Application.DTOs.POS
{
    /// <summary>
    /// Response DTO cho GET /api/v1/pos/categories
    /// Maps: DrinkCategory → POSCategoryDto
    /// </summary>
    public class POSCategoryDto
    {
        /// <summary>DrinkCategory.CategoryId</summary>
        public int Id { get; set; }

        /// <summary>DrinkCategory.Name</summary>
        public string Name { get; set; } = null!;

        /// <summary>DrinkCategory.Icon — emoji cho POS sidebar</summary>
        public string? Icon { get; set; }

        /// <summary>Số món active trong danh mục tại store (COUNT từ StoreDrink)</summary>
        public int Count { get; set; }
    }

    public sealed class POSCatalogSnapshotDto
    {
        public int StoreId { get; set; }
        public long Version { get; set; }
        public DateTime GeneratedAtUtc { get; set; }
        public IReadOnlyList<POSCategoryDto> Categories { get; set; } = Array.Empty<POSCategoryDto>();
        public IReadOnlyList<POSMenuItemDto> MenuItems { get; set; } = Array.Empty<POSMenuItemDto>();
    }
}
