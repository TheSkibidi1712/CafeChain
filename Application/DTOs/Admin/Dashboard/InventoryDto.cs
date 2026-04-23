namespace CafeChain.Application.DTOs.Admin.Dashboard
{
    public class InventoryDto
    {
        public int IngredientId { get; set; }
        public string Name { get; set; }

        public decimal TotalImport { get; set; }
        public decimal TotalExport { get; set; }
        public decimal TotalWaste { get; set; }

        public decimal CurrentStock { get; set; }
    }
}
