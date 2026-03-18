namespace CafeChain.Models.Locations
{
    public class Province
    {
        public int ProId { get; set; }
        public string Name { get; set; }
        public int? CouId { get; set; }

        public virtual Country Country { get; set; }
        public virtual ICollection<Ward> Wards { get; set; }
    }
}
