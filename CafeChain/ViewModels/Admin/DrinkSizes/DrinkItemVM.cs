namespace CafeChain.ViewModels.Admin.DrinkSizes
{
    public class DrinkItemVM
    {
        public int DrinkId { get; set; }

        public string Name { get; set; }

        public string ImageUrl { get; set; }

        public string Description { get; set; }

        public string CategoryName { get; set; }

        public string ProductTypeName { get; set; }

        public bool IsAssigned { get; set; }

        public int? DrinkSizeId { get; set; }

        public decimal? Price { get; set; }

        public bool? Active { get; set; }
    }
}
