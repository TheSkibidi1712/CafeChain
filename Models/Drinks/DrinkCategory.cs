namespace CafeChain.Models.Drinks
{
    public class DrinkCategory
    {
        public int CatId { get; set; }
        public string Name { get; set; }
        public bool Active { get; set; }

        public virtual ICollection<Drink> Drinks { get; set; }
    }
}
