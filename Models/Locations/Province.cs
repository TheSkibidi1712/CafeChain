namespace CafeChain.Models.Locations
{
    /// <summary>
    /// Cấp 1 trong hệ thống địa chỉ 3 cấp: Tỉnh/Thành phố
    /// </summary>
    public class Province
    {
        public int ProvinceId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? CountryId { get; set; }

        // Navigation Properties
        public virtual Country Country { get; set; } = null!;

        /// <summary>Danh sách Quận/Huyện thuộc tỉnh này (cấu trúc 3 cấp mới)</summary>
        public virtual ICollection<District> Districts { get; set; } = new List<District>();
    }
}
