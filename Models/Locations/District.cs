using CafeChain.Models.Customers;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Locations
{
    /// <summary>
    /// Cấp 2 trong hệ thống địa chỉ 3 cấp: Tỉnh/TP → Quận/Huyện → Phường/Xã
    /// </summary>
    public class District
    {
        public int DistrictId { get; set; }
        public string Name { get; set; } = string.Empty;

        // FK → Province
        public int? ProvinceId { get; set; }

        // Navigation Properties
        public virtual Province Province { get; set; } = null!;
        public virtual ICollection<Ward> Wards { get; set; } = new List<Ward>();
    }
}
