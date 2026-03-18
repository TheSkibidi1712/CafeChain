namespace CafeChain.Models.Locations
{
    public class Province
    {
        public int ProvinceId { get; set; }
        public string Name { get; set; }
        public int? CountryId { get; set; }

        public virtual Country Country { get; set; }
        public virtual ICollection<Ward> Wards { get; set; }
    }
}
