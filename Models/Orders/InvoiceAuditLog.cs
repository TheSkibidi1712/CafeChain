using CafeChain.Models.Staffs;
using System.ComponentModel.DataAnnotations;

namespace CafeChain.Models.Orders
{
    /// <summary>
    /// Nhật ký kiểm soát hành động nhạy cảm POS — Ghi log Trưởng ca duyệt bypass
    /// Ví dụ: Hủy hóa đơn, Giảm giá tay > 15%, Sửa giá đồ uống
    /// </summary>
    public class InvoiceAuditLog
    {
        [Key]
        public int Id { get; set; }

        /// <summary>ID hóa đơn hoặc ID giỏ hàng tạm thời</summary>
        public int OrderId { get; set; }

        /// <summary>StaffId của thu ngân thực hiện thao tác</summary>
        public int CashierId { get; set; }

        /// <summary>StaffId của Ca trưởng duyệt bypass</summary>
        public int SupervisorId { get; set; }

        /// <summary>Tên hành động: "VOID_INVOICE", "MANUAL_DISCOUNT", "PRICE_OVERRIDE"</summary>
        [MaxLength(50)]
        public string ActionName { get; set; } = string.Empty;

        /// <summary>Lý do giải trình</summary>
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // ================= NAVIGATION =================
        public virtual Staff Cashier { get; set; } = null!;
        public virtual Staff Supervisor { get; set; } = null!;
    }
}
