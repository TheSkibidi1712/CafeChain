namespace CafeChain.Application.DTOs.Admin.DrinkToppings
{
    public class DrinkToppingDto
    {
        public int DrinkToppingId { get; set; }
        public int DrinkId { get; set; }
        public int ToppingId { get; set; }
        public bool Active { get; set; }
    }
}
