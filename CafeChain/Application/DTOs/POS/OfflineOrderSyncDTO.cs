using System;
using System.Collections.Generic;

namespace CafeChain.Application.DTOs.POS
{
    public class OfflineOrderSyncDTO
    {
        public string LocalId { get; set; }

        /// <summary>
        /// UUID v4 sinh tại iPad lúc nhấn "Thanh toán" — Idempotency Key (ADR-0002).
        /// Backend kiểm tra trùng trước khi commit. Null = legacy offline order (không có idempotency).
        /// </summary>
        public Guid? ClientOrderId { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal ReceivedAmount { get; set; }
        public decimal ChangeAmount { get; set; }
        public int OrderTypeId { get; set; } // e.g., 1=DineIn, 2=Takeaway
        public int? StoreId { get; set; }
        public int? StaffId { get; set; }
        public int? WorkShiftId { get; set; }
        public DateTime? SoldAt { get; set; }
        public int PaymentMethodId { get; set; } = 1;
        public string Note { get; set; }
        public List<OfflineCartSnapshotItemDTO> CartSnapshot { get; set; } = new();
        public OfflinePaymentSnapshotDTO? PaymentSnapshot { get; set; }
        public List<OfflineOrderDetailDTO> Details { get; set; } = new List<OfflineOrderDetailDTO>();

        /// <summary>Legacy offline payload field — non-empty → FEATURE_NOT_AVAILABLE.</summary>
        public string? VoucherCode { get; set; }

        /// <summary>Legacy offline payload field — &gt; 0 → FEATURE_NOT_AVAILABLE.</summary>
        public int PointsUsed { get; set; }

        /// <summary>Legacy offline payload field — &gt; 0 → FEATURE_NOT_AVAILABLE.</summary>
        public decimal VoucherDiscount { get; set; }

        /// <summary>Legacy offline payload field — &gt; 0 → FEATURE_NOT_AVAILABLE.</summary>
        public decimal PointDiscount { get; set; }
    }

    public class OfflineOrderDetailDTO
    {
        public int ItemId { get; set; } // DrinkId
        public int? StoreMenuItemId { get; set; }
        public int? DrinkSizeId { get; set; }
        public string ItemName { get; set; }
        public int? SizeId { get; set; }
        public int Quantity { get; set; }
        public decimal? AcceptedBasePrice { get; set; }
        public decimal UnitPrice { get; set; }
        public string? PriceSource { get; set; }
        public long? CatalogVersion { get; set; }
        public decimal TotalPrice { get; set; }
        public List<POSOrderToppingDto> Toppings { get; set; } = new List<POSOrderToppingDto>();
    }

    public class OfflineCartSnapshotItemDTO
    {
        public string? CartId { get; set; }
        public int MenuItemId { get; set; }
        public int? StoreMenuItemId { get; set; }
        public int? DrinkSizeId { get; set; }
        public string? Name { get; set; }
        public int? CategoryId { get; set; }
        public int? SizeId { get; set; }
        public string? SizeName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal? EffectivePrice { get; set; }
        public string? PriceSource { get; set; }
        public long? CatalogVersion { get; set; }
        public string? Note { get; set; }
        public string? DetailText { get; set; }
        public List<OfflineCartSnapshotToppingDTO> Toppings { get; set; } = new();
    }

    public class OfflineCartSnapshotToppingDTO
    {
        public int ToppingId { get; set; }
        public string? Name { get; set; }
        public decimal? Price { get; set; }
        public decimal? AcceptedPrice { get; set; }
    }

    public class OfflinePaymentSnapshotDTO
    {
        public string? Method { get; set; }
        public int PaymentMethodId { get; set; } = 1;
        public decimal Amount { get; set; }
        public decimal ReceivedAmount { get; set; }
        public decimal ChangeAmount { get; set; }
        public DateTime? CapturedAt { get; set; }
    }
}
