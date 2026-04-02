using CafeChain.Models.Locations;

namespace CafeChain.Models.Customers
{
    public class CustomerAddress
    {
        public int CustomerAddressId { get; set; }
        public int CustomerId { get; set; }
        public string Address { get; set; } // Giữ lại để làm "Địa chỉ chi tiết / Số nhà"

        // 🔥 Thêm quan hệ với bảng Ward (Nullable để an toàn)
        public int? WardId { get; set; }
        public virtual Ward Ward { get; set; }

        public virtual Customer Customer { get; set; }
        public bool IsDefault { get; set; } = false;

        // 🔥 Chuyên gia: Tự động format địa chỉ khi hiển thị UI mà không bị Dư thừa.
        public string DisplayAddress => Ward != null && Ward.Province != null 
            ? $"{Address}, {Ward.Name}, {Ward.Province.Name}" 
            : Address;
    }
}
