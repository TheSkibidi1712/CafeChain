namespace CafeChain.Application.DTOs.Admin.Dashboard
{
    public class TopToppingDto
    {
        public int ToppingId { get; set; }
        public string ToppingName { get; set; }

        public int TotalUsed { get; set; }
        public decimal Revenue { get; set; }
    }
}
