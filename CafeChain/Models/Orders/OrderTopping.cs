using CafeChain.Models.Drinks;

namespace CafeChain.Models.Orders
{
    public class OrderTopping
    {
        public int OrderToppingId { get; set; }

        public int OrderDetailId { get; set; }
        public int ToppingId { get; set; }

        public string ToppingName { get; set; }
        public decimal Price { get; set; }

        public virtual OrderDetail OrderDetail { get; set; }
        public virtual Topping Topping { get; set; }
    }
}
