namespace CafeChain.Application.DTOs.Admin.Toppings
{
    public class ToppingDto
    {
        public int ToppingId { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public string ImageUrl { get; set; }
        public bool Active { get; set; }
    }
}
