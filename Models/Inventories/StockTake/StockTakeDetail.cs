using CafeChain.Models.Inventories.Ingredients;

namespace CafeChain.Models.Inventories.StockTake
{
    public class StockTakeDetail
    {
        public int StockTakeDetailId { get; set; }

        public int StockTakeSessionId { get; set; }

        public int IngredientId { get; set; }

        public decimal SystemQuantity { get; set; }
        public decimal ActualQuantity { get; set; }

        public decimal Difference => ActualQuantity - SystemQuantity;

        public string? Note { get; set; }

        public virtual StockTakeSession StockTakeSession { get; set; }
        public virtual Ingredient Ingredient { get; set; }
    }
}
