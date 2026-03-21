using CafeChain.Models.Customers;

namespace CafeChain.Models.Drinks
{
    public class Drink
    {
        public int DrinkId { get; set; }
        public int? CategoryId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Active { get; set; }
        public DateTime CreatedAt { get; set; }

        public virtual DrinkCategory Category { get; set; }

        public virtual ICollection<DrinkImage> DrinkImages { get; set; }
        public virtual ICollection<DrinkSize> DrinkSizes { get; set; }
        public virtual ICollection<DrinkTopping> DrinkToppings { get; set; }
        public virtual ICollection<DrinkDefaultTopping> DrinkDefaultToppings { get; set; }
        public virtual ICollection<Recipe> Recipes { get; set; }
        public virtual ICollection<Rating> Ratings { get; set; }
    }
}
