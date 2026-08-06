using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;

namespace CafeChain.Models.Orders
{
    public class OrderTopping
    {
        public int OrderToppingId { get; set; }

        public int OrderDetailId { get; set; }
        public int ToppingId { get; set; }
        /// <summary>Immutable sale-time topping BOM version. Null for legacy or BOM-included toppings.</summary>
        public int? RecipeIdSnapshot { get; set; }

        public string ToppingName { get; set; }

        /// <summary>Selling price authority (Topping.Price). Not COGS.</summary>
        public decimal Price { get; set; }

        /// <summary>Immutable sale-time topping quantity, expressed as topping recipe portions.</summary>
        public decimal QuantityPerDrinkSnapshot { get; set; } = 1m;

        /// <summary>Unit authority for QuantityPerDrinkSnapshot.</summary>
        public string QuantityUnitSnapshot { get; set; } = "RECIPE_PORTION";

        /// <summary>Immutable sale-time selling-price treatment.</summary>
        public string PriceTreatmentSnapshot { get; set; } = "ADD_TOPPING_PRICE";

        /// <summary>Immutable sale-time inventory/cost treatment.</summary>
        public string CostTreatmentSnapshot { get; set; } = "ADD_TOPPING_RECIPE_COST";

        /// <summary>Issue #133 actual FIFO COGS for topping BOM consumption.</summary>
        public SalesCostStatus CostStatus { get; set; } = SalesCostStatus.Pending;
        public decimal? TotalCogs { get; set; }

        public virtual OrderDetail OrderDetail { get; set; }
        public virtual Topping Topping { get; set; }
        public virtual Recipe? RecipeSnapshot { get; set; }
    }
}
