namespace CafeChain.Application.DTOs.Admin.Ingredients
{
    public class AdminIngredientDTO
    {
        public int IngredientId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string BaseUnitName { get; set; }
        public bool Active { get; set; }
    }
}
