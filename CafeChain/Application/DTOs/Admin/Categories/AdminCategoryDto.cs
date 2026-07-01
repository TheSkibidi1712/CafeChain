namespace CafeChain.Application.DTOs.Admin.Categories
{
    public class AdminCategoryDto
    {
        public int CategoryId { get; set; }
        
        public string CategoryCode { get; set; }

        public string Name { get; set; }

        public bool Active { get; set; }

        public string? Icon { get; set; }
    }
}