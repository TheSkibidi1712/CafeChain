namespace CafeChain.Application.DTOs.Admin.DrinkSizes
{
    public class DrinkSizeDto
    {
        public int DrinkSizeId { get; set; }
        public int DrinkId { get; set; }
        public int SizeId { get; set; }
        public decimal Price { get; set; }
        public bool Active { get; set; }
    }
}
