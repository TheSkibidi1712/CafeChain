using CafeChain.Models.Customers;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Locations
{
    /// <summary>
    /// Cấp 3 trong hệ thống địa chỉ 3 cấp: Phường/Xã/Thị trấn.
    /// Liên kết tới District thay vì Province (cấu trúc cũ 2 cấp).
    /// </summary>
    public class Ward
    {
        public int WardId { get; set; }
        public string Name { get; set; } = string.Empty;

        // 🔥 ĐỔI: FK trỏ tới District thay vì Province
        public int? DistrictId { get; set; }

        // Navigation Properties
        public virtual District District { get; set; } = null!;
        public virtual ICollection<Store> Stores { get; set; } = new List<Store>();
        public virtual ICollection<CustomerAddress> CustomerAddresses { get; set; } = new List<CustomerAddress>();
    }
}
