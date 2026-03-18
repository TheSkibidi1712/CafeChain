using CafeChain.Models.Drinks;

namespace CafeChain.Models.Orders
{
    public class OrderTopping
    {
        public int OrTgId { get; set; }

        public int OrDId { get; set; }
        public int TopId { get; set; }

        public string ToppingName { get; set; }
        public decimal Price { get; set; }

        public virtual OrderDetail OrderDetail { get; set; }
        public virtual Topping Topping { get; set; }
    }
}
