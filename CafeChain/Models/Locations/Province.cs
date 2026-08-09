namespace CafeChain.Models.Locations
{
    /// <summary>
    /// Cấp tỉnh/thành phố trong hệ thống địa chỉ hành chính hai cấp.
    /// </summary>
    public class Province
    {
        public int ProvinceId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int? CountryId { get; set; }

        // Navigation Properties
        public virtual Country Country { get; set; } = null!;

        /// <summary>Danh sách xã/phường/đặc khu trực thuộc tỉnh/thành phố.</summary>
        public virtual ICollection<Ward> Wards { get; set; } = new List<Ward>();
    }
}
