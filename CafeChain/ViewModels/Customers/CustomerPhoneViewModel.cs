namespace CafeChain.ViewModels.Customers
{
    /// <summary>
    /// DTO cho thông tin SĐT khách hàng.
    /// Dùng thay cho Entity CustomerPhone để tránh trả Entity ra Controller (Skill.md §1).
    /// </summary>
    public class CustomerPhoneViewModel
    {
        public int CustomerPhoneId { get; set; }
        public string Phone { get; set; }
        public bool IsDefault { get; set; }
    }
}
