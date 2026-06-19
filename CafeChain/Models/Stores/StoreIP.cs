using System;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Stores
{
    public class StoreIP
    {
        public int Id { get; set; }
        public int StoreId { get; set; }

        /// <summary>Địa chỉ IP, hỗ trợ cả IP Local (e.g. 192.168.1.5) hoặc dãy IP (192.168.1.*) hoặc Public IP</summary>
        public string IPAddress { get; set; } = string.Empty;

        /// <summary>Cờ phân biệt IP nội bộ hay IP Public ngoài mạng</summary>
        public bool IsPublicNetwork { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? Notes { get; set; }

        // Navigation property
        public virtual Store Store { get; set; }
    }
}
