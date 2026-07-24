using System;
using System.Collections.Generic;

namespace CafeChain.Application.DTOs.Admin
{
    /// <summary>
    /// DataTables server-side request wrapper
    /// </summary>
    public class DataTablesRequest
    {
        public int Draw { get; set; }
        public int Start { get; set; }
        public int Length { get; set; }
        public DataTablesSearch Search { get; set; }
        public List<DataTablesOrder> Order { get; set; }
        public List<DataTablesColumn> Columns { get; set; }

        // Custom filters
        public string SearchKeyword { get; set; }
        public string DateFrom { get; set; }
        public string DateTo { get; set; }
        public int? StatusFilter { get; set; }
        public int? PaymentMethodFilter { get; set; }
    }

    public class DataTablesSearch
    {
        public string Value { get; set; }
        public bool Regex { get; set; }
    }

    public class DataTablesOrder
    {
        public int Column { get; set; }
        public string Dir { get; set; }
    }

    public class DataTablesColumn
    {
        public string Data { get; set; }
        public string Name { get; set; }
        public bool Searchable { get; set; }
        public bool Orderable { get; set; }
    }

    /// <summary>
    /// DataTables server-side response wrapper
    /// </summary>
    public class DataTablesResponse<T>
    {
        public int Draw { get; set; }
        public int RecordsTotal { get; set; }
        public int RecordsFiltered { get; set; }
        public List<T> Data { get; set; }
    }

    /// <summary>
    /// Row DTO for Order History DataTable
    /// </summary>
    public class AdminOrderHistoryRowDto
    {
        public int OrderId { get; set; }
        public int StoreId { get; set; }
        public string FormattedOrderId => $"#CC{OrderId:D5}";
        public DateTime CreatedAt { get; set; }
        public string StoreName { get; set; }
        public string StaffName { get; set; }
        public string OrderTypeName { get; set; }
        public decimal Total { get; set; }
        public string PaymentMethodName { get; set; }
        public int PaymentMethodId { get; set; }
        public int OrderStatusId { get; set; }
        public string OrderStatusName { get; set; }
        public string OrderStatusBadge { get; set; }
        public string SyncState { get; set; }
        public string ReceiptState { get; set; }
        public string DrinkLabelState { get; set; }
        public string InventoryPostingState { get; set; }
    }

    public sealed class AdminOrderHistoryPageDto
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public AdminOrderHistoryStatsDto Stats { get; set; } = new();
        public List<AdminOrderHistoryRowDto> Items { get; set; } = new();
    }

    public sealed class AdminOrderHistoryStatsDto
    {
        public int PaidOrders { get; set; }
        public decimal PaidRevenue { get; set; }
        public int RefundedOrders { get; set; }
        public decimal RefundedAmount { get; set; }
    }

    /// <summary>
    /// Detail DTO for Order History Modal
    /// </summary>
    public class AdminOrderHistoryDetailDto
    {
        public int OrderId { get; set; }
        public string FormattedOrderId => $"#CC{OrderId:D5}";
        public DateTime CreatedAt { get; set; }

        public string Note { get; set; }
        public string Source { get; set; }
        public string StoreName { get; set; }
        public string StaffName { get; set; }

        public int OrderStatusId { get; set; }
        public string OrderStatusName { get; set; }
        public string OrderStatusBadge { get; set; }
        public string OrderTypeName { get; set; }

        public string PaymentMethodName { get; set; }
        public string PaymentStatusName { get; set; }
        public string ReceiptState { get; set; }
        public string DrinkLabelState { get; set; }
        public string SyncState { get; set; }
        public string InventoryPostingState { get; set; }
        public int StoreId { get; set; }
        public int? WorkShiftId { get; set; }
        public string ClientOrderId { get; set; }

        // Money
        public decimal SubTotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal Total { get; set; }

        public List<AdminOrderHistoryPaymentDto> Payments { get; set; } = new();
        public List<AdminOrderHistoryItemDto> Items { get; set; } = new();
        public AdminOrderHistoryRefundDto Refund { get; set; }
    }

    public sealed class AdminOrderHistoryRefundDto
    {
        public int OrderRefundId { get; set; }
        public string Status { get; set; }
        public string Reason { get; set; }
        public decimal Amount { get; set; }
        public DateTime RequestedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
    }

    public class AdminOrderHistoryPaymentDto
    {
        public string PaymentMethodName { get; set; }
        public decimal Amount { get; set; }
        public decimal? ReceivedAmount { get; set; }
        public decimal? ChangeAmount { get; set; }
        public DateTime? PaidAt { get; set; }
        public string TransactionCode { get; set; }
    }

    public class AdminOrderHistoryItemDto
    {
        public string DrinkName { get; set; }
        public string SizeName { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public string Note { get; set; }
        public List<AdminOrderHistoryToppingDto> Toppings { get; set; } = new();
    }

    public class AdminOrderHistoryToppingDto
    {
        public string Name { get; set; }
        public decimal Price { get; set; }
    }
}
