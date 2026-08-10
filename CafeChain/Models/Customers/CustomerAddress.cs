using System.ComponentModel.DataAnnotations.Schema;
using CafeChain.Models.Locations;

namespace CafeChain.Models.Customers
{
    public class CustomerAddress
    {
        public int CustomerAddressId { get; set; }
        public int CustomerId { get; set; }

        /// <summary>Số nhà, tên đường — phần địa chỉ chi tiết do người dùng nhập tự do</summary>
        public string Address { get; set; } = string.Empty;

        // ─── Địa chỉ 3 cấp chuẩn quốc gia ───────────────────────────────────────

        /// <summary>FK tới Ward (Phường/Xã) — Nullable để không vi phạm dữ liệu cũ</summary>
        public int? WardId { get; set; }

        /// <summary>FK trực tiếp tới đơn vị hành chính cấp xã.</summary>
        /// <summary>FK tới Province (Tỉnh/TP) — Thêm mới để tối ưu truy vấn báo cáo</summary>
        public int? ProvinceId { get; set; }

        // ─── Toạ độ GPS (lấy tự động từ GeocodingService khi Save) ──────────────

        /// <summary>Vĩ độ — BẮT BUỘC dùng decimal(9,6) để không bị cắt xén số GPS</summary>
        [Column(TypeName = "decimal(9,6)")]
        public decimal? Latitude { get; set; }

        /// <summary>Kinh độ — BẮT BUỘC dùng decimal(9,6) để không bị cắt xén số GPS</summary>
        [Column(TypeName = "decimal(9,6)")]
        public decimal? Longitude { get; set; }

        public bool IsDefault { get; set; } = false;
        public bool IsDeleted { get; set; } = false;

        // ─── Navigation Properties ────────────────────────────────────────────────
        public virtual Customer Customer { get; set; } = null!;
        public virtual Ward? Ward { get; set; }
        public virtual Province? Province { get; set; }

        // ─── Computed Display Property ────────────────────────────────────────────
        /// <summary>
        /// Tự động ghép chuỗi địa chỉ đầy đủ để hiển thị UI.
        /// Ưu tiên dùng 3 cấp: Phường - Quận - Tỉnh
        /// </summary>
        public string DisplayAddress
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(Address)) parts.Add(Address);
                if (Ward != null) parts.Add(Ward.Name);
                if (Province != null) parts.Add(Province.Name);
                return parts.Count > 0 ? string.Join(", ", parts) : string.Empty;
            }
        }
    }
}
