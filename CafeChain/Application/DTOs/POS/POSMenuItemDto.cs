using System.Collections.Generic;

namespace CafeChain.Application.DTOs.POS
{
    /// <summary>
    /// Response DTO cho GET /api/v1/pos/menu-items
    /// Maps: Drink + DrinkSize + DrinkTopping → POSMenuItemDto
    /// </summary>
    public class POSMenuItemDto
    {
        /// <summary>Drink.DrinkId</summary>
        public int Id { get; set; }

        /// <summary>Drink.Name</summary>
        public string Name { get; set; } = null!;

        /// <summary>Giá base (Size S / size nhỏ nhất)</summary>
        public decimal Price { get; set; }

        /// <summary>Drink.CategoryId</summary>
        public int CategoryId { get; set; }

        /// <summary>DrinkImage.ImageUrl (first, nullable)</summary>
        public string? Image { get; set; }

        /// <summary>Co the ban ngay tai POS hay khong.</summary>
        public bool IsAvailable { get; set; }

        /// <summary>Ma trang thai kha dung: Available | MissingRecipe | MissingInventory | InsufficientStock | TemporarilyUnavailable.</summary>
        public string AvailabilityStatus { get; set; } = "Available";

        /// <summary>Ly do tieng Viet khi IsAvailable = false.</summary>
        public string? AvailabilityReason { get; set; }

        /// <summary>Danh sách size khả dụng + giá</summary>
        public List<POSMenuItemSizeDto> Sizes { get; set; } = new();

        /// <summary>Danh sách topping khả dụng cho món này tại store</summary>
        public List<POSToppingDto> AvailableToppings { get; set; } = new();
    }

    /// <summary>
    /// Size + giá của 1 MenuItem — nested trong POSMenuItemDto
    /// </summary>
    public class POSMenuItemSizeDto
    {
        public int StoreMenuItemId { get; set; }

        public int DrinkSizeId { get; set; }

        /// <summary>Size.SizeId</summary>
        public int SizeId { get; set; }

        /// <summary>Size.Name (S/M/L)</summary>
        public string SizeName { get; set; } = null!;

        /// <summary>DrinkSize.Price</summary>
        public decimal Price { get; set; }

        public decimal GlobalPrice { get; set; }

        public decimal? StoreOverride { get; set; }

        public string PriceSource { get; set; } = string.Empty;

        public bool IsAvailable { get; set; }

        public string AvailabilityStatus { get; set; } = string.Empty;

        public string? AvailabilityReason { get; set; }

        /// <summary>True when the flattened active recipe contains the store canonical ice ingredient.</summary>
        public bool SupportsIceCustomization { get; set; }

        /// <summary>Canonical ice quantity for one drink in its base unit; catalog/version snapshot only.</summary>
        public decimal? BaseIceQuantityBaseUnit { get; set; }

        public List<POSToppingPolicyDto> ToppingPolicies { get; set; } = new();
    }

    public class POSToppingPolicyDto
    {
        public int ToppingId { get; set; }
        public bool IsDefaultSelected { get; set; }
        public bool IsRequired { get; set; }
        public string PriceTreatment { get; set; } = string.Empty;
        public string CostTreatment { get; set; } = string.Empty;
        public decimal QuantityPerDrink { get; set; }
    }
}
