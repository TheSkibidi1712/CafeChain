namespace CafeChain.Application.DTOs.Admin.Dashboard
{
    public class WasteDto
    {
        public int StoreId { get; set; }
        public string StoreName { get; set; }

        public int IngredientId { get; set; }
        public string IngredientName { get; set; }

        public decimal TotalWasteQty { get; set; }
        public decimal TotalWasteValue { get; set; }
    }
}
