using CafeChain.Models.Orders;
namespace CafeChain.Models.Payments
{
    public class Payment
    {
        public int PaymentId { get; set; }

        public int OrderId { get; set; }
        public decimal Amount { get; set; }
        public decimal? ReceivedAmount { get; set; }
        public decimal? ChangeAmount { get; set; }

        public int PaymentMethodId { get; set; }
        public int PaymentStatusId { get; set; }

        public int? CashSessionId { get; set; } // 🔥 thêm

        public int? StoreId { get; set; }
        public int? WorkShiftId { get; set; }
        public int? PaidByStaffId { get; set; }
        public string? TerminalId { get; set; }

        public string? TransactionCode { get; set; } // 🔥 thêm

        public DateTime? PaidAt { get; set; }

        public virtual Order Order { get; set; }
        public virtual PaymentMethod PaymentMethod { get; set; }
        public virtual PaymentStatus PaymentStatus { get; set; }
        public virtual CashSession CashSession { get; set; }
        public virtual CafeChain.Models.Stores.Store? Store { get; set; }
        public virtual CafeChain.Models.Stores.WorkShift? WorkShift { get; set; }
        public virtual CafeChain.Models.Staffs.Staff? PaidByStaff { get; set; }
        public virtual CafeChain.Models.Stores.PosTerminal? Terminal { get; set; }
    }
}
