using CafeChain.Models.Customers;

namespace CafeChain.ViewModels.Customers
{
    public class CustomerProfileViewModel
    {
        public Customer Customer { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; } // SĐT lúc đăng ký (có thể làm số chính)

        // Loyalty properties
        public int TotalPoints { get; set; }
        public string CurrentTierName { get; set; }
        public string NextTierName { get; set; }
        public int PointsNeeded { get; set; }
        public double ProgressPercentage { get; set; }
    }
}