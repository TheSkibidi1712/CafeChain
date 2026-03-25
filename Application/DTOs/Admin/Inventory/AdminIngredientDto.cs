namespace CafeChain.Application.DTOs.Admin.Inventory
{
    public class AdminIngredientDto
    {
        public int IngredientId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Unit { get; set; }
        public decimal TotalStock { get; set; }
        public string Status { get; set; }
    }
}
