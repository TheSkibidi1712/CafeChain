using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;

namespace CafeChain.Models.Orders
{
    public class OrderTopping
    {
        public int OrderToppingId { get; set; }

        public int OrderDetailId { get; set; }
        public int ToppingId { get; set; }

        public string ToppingName { get; set; }

        /// <summary>Selling price authority (Topping.Price). Not COGS.</summary>
        public decimal Price { get; set; }

        /// <summary>Issue #133 actual FIFO COGS for topping BOM consumption.</summary>
        public SalesCostStatus CostStatus { get; set; } = SalesCostStatus.Pending;
        public decimal? TotalCogs { get; set; }

        public virtual OrderDetail OrderDetail { get; set; }
        public virtual Topping Topping { get; set; }
    }
}
