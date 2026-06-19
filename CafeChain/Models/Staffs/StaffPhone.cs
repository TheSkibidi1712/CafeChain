namespace CafeChain.Models.Staffs
{
    public class StaffPhone
    {
        public int StaffPhoneId { get; set; }
        public int StaffId { get; set; }
        public string Phone { get; set; }
        public bool IsDefault { get; set; }

        public virtual Staff Staff { get; set; }
    }
}
