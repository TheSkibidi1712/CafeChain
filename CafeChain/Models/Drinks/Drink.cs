using CafeChain.Models.Customers;
using CafeChain.Models.Stores;
using CafeChain.Models.Orders;
namespace CafeChain.Models.Drinks
{
    public class Drink
    {
        public int DrinkId { get; set; }
        public int? CategoryId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int ProductTypeId { get; set; }
        public bool Active { get; set; }
        public DateTime CreatedAt { get; set; }
        
        public decimal? CalculatedCogs { get; set; } // Giá vốn tự động tính từ BOM

        public virtual DrinkCategory Category { get; set; }

        public virtual ProductType ProductType { get; set; }
        public virtual ICollection<DrinkImage> DrinkImages { get; set; }
        public virtual ICollection<DrinkSize> DrinkSizes { get; set; }
        public virtual ICollection<DrinkTopping> DrinkToppings { get; set; }
        public virtual ICollection<DrinkDefaultTopping> DrinkDefaultToppings { get; set; }
        public virtual ICollection<StoreDrink> StoreDrinks { get; set; }
        public virtual ICollection<Recipe> Recipes { get; set; }
        public virtual ICollection<Rating> Ratings { get; set; }
        public virtual ICollection<OrderDetail> OrderDetails { get; set; }
    }
}
