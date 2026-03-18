namespace CafeChain.Models.Inventories
{
    public class StockImportDetail
    {
        public int Id { get; set; }
        public int StockImportId { get; set; }
        public int IngredientId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public virtual StockImport StockImport { get; set; }
        public virtual Ingredient Ingredient { get; set; }
    }
}
