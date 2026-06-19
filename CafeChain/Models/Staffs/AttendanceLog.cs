using CafeChain.Models.Stores;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CafeChain.Models.Staffs
{
    /// <summary>
    /// HR Audit Trail for BYOD FaceID Check-ins (Source of truth for Interlock)
    /// </summary>
    public class AttendanceLog
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }

        public int StoreId { get; set; }

        /// <summary>
        /// Check-in timestamp (UTC is recommended for system logic)
        /// </summary>
        public DateTime CheckInTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The Wi-Fi IP Address used for check-in
        /// </summary>
        [MaxLength(50)]
        public string IpAddress { get; set; }

        /// <summary>
        /// True if face descriptor matched successfully via FaceAPI
        /// </summary>
        public bool IsFaceVerified { get; set; }

        /// <summary>
        /// Valid or Invalid based on rules (IP, location, etc.)
        /// </summary>
        [MaxLength(20)]
        public string Status { get; set; } = "Valid";

        // ================= NAVIGATION =================
        [ForeignKey("UserId")]
        public virtual Staff User { get; set; }

        [ForeignKey("StoreId")]
        public virtual Store Store { get; set; }
    }
}
