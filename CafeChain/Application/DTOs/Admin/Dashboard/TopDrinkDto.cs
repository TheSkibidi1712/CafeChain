namespace CafeChain.Application.DTOs.Admin.Dashboard
{
    public class TopDrinkDto
    {
        public int DrinkId { get; set; }
        public string DrinkName { get; set; }

        public int TotalSold { get; set; }
        public decimal Revenue { get; set; }
    }
}
