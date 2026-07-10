namespace CafeChain.Application.DTOs.Admin.Drinks
{
    public class AdminDrinkDTO
    {
        public int DrinkId { get; set; }
        public string DrinkCode { get; set; }
        public string Name { get; set; }
        public string CategoryName { get; set; }
        public string ProductTypeName { get; set; }
        public string Description { get; set; }
        public bool Active { get; set; }
        public string ImageUrl { get; set; }
    }
}
