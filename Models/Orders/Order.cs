using CafeChain.Models.Customers;
using CafeChain.Models.Payments;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using CafeChain.Models.Vouchers;
using CafeChain.Models.Loyalties;
using System.ComponentModel.DataAnnotations;
namespace CafeChain.Models.Orders
{
    public class Order
    {
        public int OrderId { get; set; }

        public int? CustomerId { get; set; }
        public int StoreId { get; set; }
        public int OrderStatusId { get; set; }
        public int PaymentStatusId { get; set; }
        public int OrderTypeId { get; set; }
        public int? TableId { get; set; }
        public int? StaffId { get; set; }

        /// <summary>
        /// Ca làm việc POS — null cho đơn Online
        /// </summary>
        public int? WorkShiftId { get; set; }

        public string? Source { get; set; }
        public string? Note { get; set; }
        public string? PaymentReference { get; set; }

        
        // ====== RECEIVER INFORMATION (Zero-Trust Delivery) ======
        [Required(ErrorMessage = "Vui lòng nhập tên người nhận")]
        [MaxLength(100)]
        public string ReceiverName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        public string ReceiverPhone { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ giao hàng")]
        public string DeliveryAddress { get; set; }

        [Required]
        public decimal ShippingFee { get; set; }

        // ====== MONEY BREAKDOWN ======
        public decimal SubTotal { get; set; }          // Tổng tiền gốc (chưa giảm)
        public decimal VoucherDiscount { get; set; }   // Giảm từ voucher
        public decimal PointDiscount { get; set; }     // Giảm từ điểm
        public int PointsUsed { get; set; }            // Số điểm dùng

        public decimal Total { get; set; }             // Tổng cuối (SubTotal - Discount)

        public DateTime CreatedAt { get; set; }

        public virtual Customer Customer { get; set; }
        public virtual Store Store { get; set; }
        public virtual Staff Staff { get; set; }
        public virtual WorkShift WorkShift { get; set; }
        public virtual OrderStatus OrderStatus { get; set; }
        public virtual PaymentStatus PaymentStatus { get; set; }
        public virtual OrderType OrderType { get; set; }

        public virtual ICollection<OrderDetail> OrderDetails { get; set; }
        public virtual ICollection<Payment> Payments { get; set; }
        public virtual ICollection<OrderVoucher> OrderVouchers { get; set; }
        public virtual ICollection<PointTransaction> PointTransactions { get; set; }
    }
}
