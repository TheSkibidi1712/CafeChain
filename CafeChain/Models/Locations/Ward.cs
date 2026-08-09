using CafeChain.Models.Customers;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Locations
{
    /// <summary>
    /// Cấp 3 trong hệ thống địa chỉ 3 cấp: Phường/Xã/Thị trấn.
    /// Đơn vị hành chính cấp xã, liên kết trực tiếp với tỉnh/thành phố.
    /// </summary>
    public class Ward
    {
        public int WardId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }

        // Mô hình hành chính hai cấp: xã/phường/đặc khu thuộc trực tiếp tỉnh/thành phố.
        public int ProvinceId { get; set; }

        // Navigation Properties
        public virtual Province Province { get; set; } = null!;
        public virtual ICollection<Store> Stores { get; set; } = new List<Store>();
        public virtual ICollection<CustomerAddress> CustomerAddresses { get; set; } = new List<CustomerAddress>();
    }
}
