namespace CafeChain.Models.Drinks
{
    public class Size
    {
        public int SizId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Active { get; set; }

        public virtual ICollection<DrinkSize> DrinkSizes { get; set; }
    }
}
