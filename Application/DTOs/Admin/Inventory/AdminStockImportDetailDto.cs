namespace CafeChain.Application.DTOs.Admin.Inventory
{
    public class AdminStockImportDetailDto
    {
        public int IngredientId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
