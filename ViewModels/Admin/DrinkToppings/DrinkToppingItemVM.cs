namespace CafeChain.ViewModels.Admin.DrinkToppings
{
    public class DrinkToppingItemVM
    {
        public int DrinkId { get; set; }

        public string Name { get; set; }

        public string ImageUrl { get; set; }

        public string CategoryName { get; set; }

        public string ProductTypeName { get; set; }

        public bool IsAssigned { get; set; }

        public int? DrinkToppingId { get; set; }

        public bool? Active { get; set; }
    }
}
