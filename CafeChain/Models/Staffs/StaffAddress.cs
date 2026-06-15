namespace CafeChain.Models.Staffs
{
    public class StaffAddress
    {
        public int StaffAddressId { get; set; }
        public int StaffId { get; set; }
        public string Address { get; set; }
        public bool IsDefault { get; set; }

        public virtual Staff Staff { get; set; }
    }
}
