namespace CafeChain.Models.Customers
{
    public class CustomerPhone
    {
        public int CustomerPhoneId { get; set; }
        public int CustomerId { get; set; }
        public string Phone { get; set; }
        public bool IsDefault { get; set; } = false; // Mặc định tạo ra là false
        public virtual Customer Customer { get; set; }
    }
}
