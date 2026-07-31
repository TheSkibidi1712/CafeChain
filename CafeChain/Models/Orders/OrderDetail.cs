using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Orders
{
    public class OrderDetail
    {
        public int OrderDetailId { get; set; }

        public int OrderId { get; set; } 

        public int DrinkId { get; set; }
        public int? SizeId { get; set; }
        public int? StoreMenuItemId { get; set; }
        public int? DrinkSizeId { get; set; }
        public string DrinkName { get; set; }
        public string? SizeName { get; set; }

        /// <summary>Selling price authority (DrinkSize + toppings). Not COGS.</summary>
        public decimal Price { get; set; }
        public decimal? AcceptedBasePrice { get; set; }
        public string? PriceSource { get; set; }
        public long? AcceptedCatalogVersion { get; set; }
        public int Quantity { get; set; }

        public string Note { get; set; }

        /// <summary>
        /// Structured POS ice customization snapshot. Null means a legacy order line or a recipe
        /// that did not contain the store's canonical ice ingredient at sale time.
        /// </summary>
        public int? IceLevelPercent { get; set; }
        public int? IceIngredientId { get; set; }

        /// <summary>Total canonical ice quantity for this order line, in the ingredient base unit.</summary>
        public decimal? BaseIceQuantityBaseUnit { get; set; }

        /// <summary>Applied canonical ice quantity after IceLevelPercent, in the ingredient base unit.</summary>
        public decimal? AppliedIceQuantityBaseUnit { get; set; }

        /// <summary>Issue #133 actual FIFO COGS for drink BOM (toppings may be separate).</summary>
        public SalesCostStatus CostStatus { get; set; } = SalesCostStatus.Pending;
        public decimal? UnitCogs { get; set; }
        public decimal? TotalCogs { get; set; }

        public virtual Order Order { get; set; }
        public virtual Size Size { get; set; }
        public virtual Drink Drink { get; set; }
        public virtual StoreMenuItem? StoreMenuItem { get; set; }
        public virtual DrinkSize? DrinkSize { get; set; }
        public virtual Models.Inventories.Ingredients.Ingredient? IceIngredient { get; set; }
        public virtual ICollection<OrderTopping> OrderToppings { get; set; }
    }
}
