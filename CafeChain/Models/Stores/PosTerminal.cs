using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Stores
{
    /// <summary>
    /// Thực thể đại diện cho thiết bị POS Terminal tại quầy
    /// GUID sinh từ localStorage trình duyệt, gắn cứng theo thiết bị
    /// </summary>
    public class PosTerminal
    {
        [Key]
        public string TerminalId { get; set; } = string.Empty;

        public int StoreId { get; set; }

        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public bool Active { get; set; } = true;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        [NotMapped]
        public DateTime CreatedAt { get => CreatedAtUtc; set => CreatedAtUtc = value; }

        // ================= NAVIGATION =================
        public virtual Store Store { get; set; } = null!;
        public virtual ICollection<WorkShift> WorkShifts { get; set; } = new List<WorkShift>();
    }
}
