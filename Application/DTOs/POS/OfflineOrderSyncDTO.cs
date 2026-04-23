using System.Collections.Generic;

namespace CafeChain.Application.DTOs.POS
{
    public class OfflineOrderSyncDTO
    {
        public string LocalId { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal ReceivedAmount { get; set; }
        public decimal ChangeAmount { get; set; }
        public int OrderTypeId { get; set; } // e.g., 1=DineIn, 2=Takeaway
        public int? StoreId { get; set; }
        public int? StaffId { get; set; }
        public string Note { get; set; }
        public List<OfflineOrderDetailDTO> Details { get; set; } = new List<OfflineOrderDetailDTO>();
    }

    public class OfflineOrderDetailDTO
    {
        public int ItemId { get; set; } // DrinkId
        public string ItemName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }
}
