using CafeChain.Models.Drinks;

namespace CafeChain.Models.Orders
{
    public class OrderDetail
    {
        public int OrDId { get; set; }

        public int OrderId { get; set; } 

        public int DriId { get; set; }
        public int? SizId { get; set; }
        public string DrinkName { get; set; }
        public string? SizeName { get; set; }

        public decimal Price { get; set; }
        public int Quantity { get; set; }

        public string Note { get; set; }

        public virtual Order Order { get; set; }
        public virtual Size Size { get; set; }
        public virtual Drink Drink { get; set; }
        public virtual ICollection<OrderTopping> OrderToppings { get; set; }
    }
}
