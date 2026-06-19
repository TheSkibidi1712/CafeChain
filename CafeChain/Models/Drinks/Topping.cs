using CafeChain.Models.Orders;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Drinks
{
    public class Topping
    {
        public int ToppingId { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        // Cloudinary
        public string? ImageUrl { get; set; }

        public string? ImagePublicId { get; set; }

        // Status
        public bool Active { get; set; } = true;
        public virtual ICollection<DrinkTopping> DrinkToppings { get; set; }
        public virtual ICollection<StoreTopping> StoreToppings { get; set; }
        public virtual ICollection<OrderTopping> OrderToppings { get; set; }
    }
}
