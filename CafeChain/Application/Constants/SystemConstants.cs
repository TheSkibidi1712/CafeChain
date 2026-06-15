namespace CafeChain.Application.Constants
{
    public static class SystemConstants
    {
        /// <summary>
        /// Mapping đúng với bảng OrderStatuses trong DB (6 trạng thái mới chuẩn F&B KDS):
        /// 1=Pending, 2=Preparing, 3=Ready, 4=Delivering, 5=Completed, 6=Cancelled
        /// </summary>
        public static class OrderStatuses
        {
            // Chờ thanh toán (đơn chuyển khoản)
            public const int AwaitingPayment = 7;
            // Chờ xác nhận
            public const int Pending = 1;  
            // Đang pha chế
            public const int Preparing = 2;  
            // Chờ lấy hàng
            public const int Ready = 3;  
            // Đang giao hàng
            public const int Delivering = 4;  
            // Hoàn thành
            public const int Completed = 5;  
            // Đã hủy
            public const int Cancelled = 6;  
        }

        /// <summary>
        /// Mapping đúng với bảng PaymentStatuses trong DB:
        /// 1=Unpaid, 2=Paid, 3=Refunded, 4=Failed
        /// </summary>
        public static class PaymentStatuses
        {
            // Chưa thanh toán
            public const int Unpaid = 1;

            // Đã thanh toán
            public const int Paid = 2;

            // Đã hoàn tiền
            public const int Refunded = 3;

            // Thanh toán thất bại
            public const int Failed = 4;
        }

        public static class OrderTypes
        {
            public const int DineIn   = 1;  // Ăn tại bàn
            public const int TakeAway = 2;  // Mang đi
            public const int Delivery = 3;  // Giao hàng
        }
    }
}
