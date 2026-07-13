using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;

namespace CafeChain.Models.Orders
{
    public class OrderDetail
    {
        public int OrderDetailId { get; set; }

        public int OrderId { get; set; } 

        public int DrinkId { get; set; }
        public int? SizeId { get; set; }
        public string DrinkName { get; set; }
        public string? SizeName { get; set; }

        /// <summary>Selling price authority (DrinkSize + toppings). Not COGS.</summary>
        public decimal Price { get; set; }
        public int Quantity { get; set; }

        public string Note { get; set; }

        /// <summary>Issue #133 actual FIFO COGS for drink BOM (toppings may be separate).</summary>
        public SalesCostStatus CostStatus { get; set; } = SalesCostStatus.Pending;
        public decimal? UnitCogs { get; set; }
        public decimal? TotalCogs { get; set; }

        public virtual Order Order { get; set; }
        public virtual Size Size { get; set; }
        public virtual Drink Drink { get; set; }
        public virtual ICollection<OrderTopping> OrderToppings { get; set; }
    }
}
