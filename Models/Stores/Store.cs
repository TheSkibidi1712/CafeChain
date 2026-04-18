using System.ComponentModel.DataAnnotations.Schema;
using CafeChain.Models.Inventories;
using CafeChain.Models.Locations;
using CafeChain.Models.Orders;
using CafeChain.Models.Payments;
using CafeChain.Models.Staffs;

namespace CafeChain.Models.Stores
{
    public class Store
    {
        public int StoreId { get; set; }
        public string Name { get; set; } = string.Empty;

        /// <summary>Số nhà, tên đường — phần địa chỉ chi tiết</summary>
        public string Address { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public bool Active { get; set; }
        public DateTime CreatedAt { get; set; }

        // ─── Địa chỉ 3 cấp chuẩn quốc gia ───────────────────────────────────────

        /// <summary>FK tới Ward (Phường/Xã) — giữ nguyên liên kết cũ</summary>
        public int? WardId { get; set; }

        /// <summary>FK tới District (Quận/Huyện) — Thêm mới, Nullable, tối ưu truy vấn</summary>
        public int? DistrictId { get; set; }

        /// <summary>FK tới Province (Tỉnh/TP) — Thêm mới, Nullable, tối ưu truy vấn</summary>
        public int? ProvinceId { get; set; }

        // ─── Toạ độ GPS (dùng cho Store Locator / Google Maps) ──────────────────

        /// <summary>Vĩ độ — decimal(9,6) để không bị làm tròn số GPS</summary>
        [Column(TypeName = "decimal(9,6)")]
        public decimal? Latitude { get; set; }

        /// <summary>Kinh độ — decimal(9,6) để không bị làm tròn số GPS</summary>
        [Column(TypeName = "decimal(9,6)")]
        public decimal? Longitude { get; set; }

        // ─── Navigation Properties ────────────────────────────────────────────────
        public virtual Ward? Ward { get; set; }
        public virtual District? District { get; set; }
        public virtual Province? Province { get; set; }

        public virtual ICollection<Staff> Staffs { get; set; } = new List<Staff>();
        public virtual ICollection<StoreDrink> StoreDrinks { get; set; } = new List<StoreDrink>();
        public virtual ICollection<StoreTopping> StoreToppings { get; set; } = new List<StoreTopping>();
        public virtual ICollection<StoreInventory> StoreInventories { get; set; } = new List<StoreInventory>();
        public virtual ICollection<InventoryDocument> InventoryDocuments { get; set; } = new List<InventoryDocument>();
        public virtual ICollection<Shift> Shifts { get; set; } = new List<Shift>();
        public virtual ICollection<CashSession> CashSessions { get; set; } = new List<CashSession>();
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
        public virtual ICollection<InventoryTransfer> ExportTransfers { get; set; } = new List<InventoryTransfer>();
        public virtual ICollection<InventoryTransfer> ImportTransfers { get; set; } = new List<InventoryTransfer>();
    }
}
