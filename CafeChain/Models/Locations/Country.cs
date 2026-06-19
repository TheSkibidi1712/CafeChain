namespace CafeChain.Models.Locations
{
    public class Country
    {
        public int CountryId { get; set; }
        public string Name { get; set; }

        public virtual ICollection<Province> Provinces { get; set; }
    }
}
