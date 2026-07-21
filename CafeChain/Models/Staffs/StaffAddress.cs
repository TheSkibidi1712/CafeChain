namespace CafeChain.Models.Staffs
{
    public class StaffAddress
    {
        public int StaffAddressId { get; set; }
        public int StaffId { get; set; }
        public int? ProvinceId { get; set; }
        public int? DistrictId { get; set; }
        public int? WardId { get; set; }
        public string Address { get; set; }
        public bool IsDefault { get; set; }

        public virtual Staff Staff { get; set; }
        public virtual Locations.Province? Province { get; set; }
        public virtual Locations.District? District { get; set; }
        public virtual Locations.Ward? Ward { get; set; }
    }
}
