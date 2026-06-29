namespace CafeChain.Application.DTOs.POS
{
    /// <summary>
    /// Response DTO cho GET /api/v1/pos/toppings
    /// Maps: Topping → POSToppingDto
    /// Cũng dùng nested trong POSMenuItemDto.AvailableToppings
    /// </summary>
    public class POSToppingDto
    {
        /// <summary>Topping.ToppingId</summary>
        public int Id { get; set; }

        /// <summary>Topping.Name</summary>
        public string Name { get; set; } = null!;

        /// <summary>Topping.Price</summary>
        public decimal Price { get; set; }

        /// <summary>Topping.ImageUrl (nullable)</summary>
        public string? ImageUrl { get; set; }
    }
}
