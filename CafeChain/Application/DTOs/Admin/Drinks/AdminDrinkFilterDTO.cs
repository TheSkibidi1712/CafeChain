namespace CafeChain.Application.DTOs.Admin.Drinks
{
    public class AdminDrinkFilterDTO
    {
        public string? Keyword { get; set; }

        public bool? Active { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 10;
    }
}
