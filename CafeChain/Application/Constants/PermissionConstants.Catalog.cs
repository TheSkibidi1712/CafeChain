namespace CafeChain.Application.Constants
{
    /// <summary>
    /// Authoritative active permission catalog reconciled by RBAC_CAFECHAIN29_V1.
    /// The four legacy *.Delete product permissions are intentionally excluded
    /// because they are inactive compatibility rows.
    /// </summary>
    public static partial class PermissionConstants
    {
        public const string InventoryAdjust = "Inventory.Adjust";
        public const string InventoryExport = "Inventory.Export";
        public const string InventoryView = "Inventory.View";
        public const string InventoryDocumentApproveNegative = "InventoryDocument.ApproveNegative";
        public const string InventoryDocumentCancel = "InventoryDocument.Cancel";
        public const string InventoryDocumentConfirm = "InventoryDocument.Confirm";
        public const string InventoryDocumentCreateDraft = "InventoryDocument.CreateDraft";
        public const string InventoryDocumentExport = "InventoryDocument.Export";
        public const string InventoryDocumentSubmit = "InventoryDocument.Submit";
        public const string InventoryDocumentView = "InventoryDocument.View";
        public const string InventoryThresholdUpdate = "InventoryThreshold.Update";
        public const string InventoryThresholdView = "InventoryThreshold.View";
        public const string InventoryTransferCancel = "InventoryTransfer.Cancel";
        public const string InventoryTransferConfirmReturn = "InventoryTransfer.ConfirmReturn";
        public const string InventoryTransferCreateDraft = "InventoryTransfer.CreateDraft";
        public const string InventoryTransferDispatch = "InventoryTransfer.Dispatch";
        public const string InventoryTransferExport = "InventoryTransfer.Export";
        public const string InventoryTransferReceive = "InventoryTransfer.Receive";
        public const string InventoryTransferRequestReturn = "InventoryTransfer.RequestReturn";
        public const string InventoryTransferResolveDiscrepancy = "InventoryTransfer.ResolveDiscrepancy";
        public const string InventoryTransferUpdateDraft = "InventoryTransfer.UpdateDraft";
        public const string InventoryTransferView = "InventoryTransfer.View";

        public const string OrderCancel = "Order.Cancel";
        public const string OrderExport = "Order.Export";
        public const string OrderRefund = "Order.Refund";
        public const string OrderRefundConfirm = "Order.RefundConfirm";
        public const string OrderRefundRequest = "Order.RefundRequest";
        public const string OrderUpdateStatus = "Order.UpdateStatus";
        public const string OrderView = "Order.View";

        public const string PreparedItemCreate = "PreparedItem.Create";
        public const string PreparedItemToggleStatus = "PreparedItem.ToggleStatus";
        public const string PreparedItemUpdate = "PreparedItem.Update";
        public const string PreparedItemView = "PreparedItem.View";
        public const string ProductionOrderConfirm = "ProductionOrder.Confirm";
        public const string ProductionOrderCreate = "ProductionOrder.Create";
        public const string ProductionOrderView = "ProductionOrder.View";
        public const string ProfitabilityUpdatePrice = "Profitability.UpdatePrice";
        public const string ProfitabilityUpdateToppingPolicy = "Profitability.UpdateToppingPolicy";
        public const string ProfitabilityView = "Profitability.View";

        public const string PurchaseAdviceApprove = "PurchaseAdvice.Approve";
        public const string PurchaseAdviceCancel = "PurchaseAdvice.Cancel";
        public const string PurchaseAdviceConsolidate = "PurchaseAdvice.Consolidate";
        public const string PurchaseAdviceCreate = "PurchaseAdvice.Create";
        public const string PurchaseAdviceCreatePurchaseOrder = "PurchaseAdvice.CreatePurchaseOrder";
        public const string PurchaseAdviceReject = "PurchaseAdvice.Reject";
        public const string PurchaseAdviceReview = "PurchaseAdvice.Review";
        public const string PurchaseAdviceSelectSupplier = "PurchaseAdvice.SelectSupplier";
        public const string PurchaseAdviceSubmit = "PurchaseAdvice.Submit";
        public const string PurchaseAdviceUpdate = "PurchaseAdvice.Update";
        public const string PurchaseAdviceView = "PurchaseAdvice.View";

        public const string PurchaseOrderApprove = "PurchaseOrder.Approve";
        public const string PurchaseOrderCancel = "PurchaseOrder.Cancel";
        public const string PurchaseOrderCloseRemaining = "PurchaseOrder.CloseRemaining";
        public const string PurchaseOrderConsolidate = "PurchaseOrder.Consolidate";
        public const string PurchaseOrderCreate = "PurchaseOrder.Create";
        public const string PurchaseOrderCreateBatch = "PurchaseOrder.CreateBatch";
        public const string PurchaseOrderExport = "PurchaseOrder.Export";
        public const string PurchaseOrderOverrideAllocation = "PurchaseOrder.OverrideAllocation";
        public const string PurchaseOrderReceive = "PurchaseOrder.Receive";
        public const string PurchaseOrderRejectApproval = "PurchaseOrder.RejectApproval";
        public const string PurchaseOrderSend = "PurchaseOrder.Send";
        public const string PurchaseOrderSubmit = "PurchaseOrder.Submit";
        public const string PurchaseOrderUpdate = "PurchaseOrder.Update";
        public const string PurchaseOrderView = "PurchaseOrder.View";
        public const string PurchaseOrderViewBatch = "PurchaseOrder.ViewBatch";

        public const string ReceiptCancel = "Receipt.Cancel";
        public const string ReceiptConfirm = "Receipt.Confirm";
        public const string ReceiptCreate = "Receipt.Create";
        public const string ReceiptRecordSupplierIssue = "Receipt.RecordSupplierIssue";
        public const string ReceiptReject = "Receipt.Reject";
        public const string ReceiptUpdateDraft = "Receipt.UpdateDraft";
        public const string ReceiptView = "Receipt.View";
        public const string ReceiptViewCost = "Receipt.ViewCost";

        public const string RecipeCreate = "Recipe.Create";
        public const string RecipeDelete = "Recipe.Delete";
        public const string RecipeUpdate = "Recipe.Update";
        public const string RecipeView = "Recipe.View";

        public const string RestockApprove = "Restock.Approve";
        public const string RestockCancel = "Restock.Cancel";
        public const string RestockCloseRemaining = "Restock.CloseRemaining";
        public const string RestockCreatePurchaseOrder = "Restock.CreatePurchaseOrder";
        public const string RestockCreateTransfer = "Restock.CreateTransfer";
        public const string RestockReject = "Restock.Reject";
        public const string RestockSubmit = "Restock.Submit";
        public const string RestockUpdate = "Restock.Update";

        public const string StockAlertConfigure = "StockAlert.Configure";
        public const string StockAlertCreate = "StockAlert.Create";
        public const string StockAlertCreateRestockRequest = "StockAlert.CreateRestockRequest";
        public const string StockAlertExport = "StockAlert.Export";
        public const string StockAlertResolve = "StockAlert.Resolve";
        public const string StockAlertView = "StockAlert.View";

        public const string StoreMenuOverridePrice = "StoreMenu.OverridePrice";
        public const string StoreMenuUpdate = "StoreMenu.Update";
        public const string StoreMenuView = "StoreMenu.View";
        public const string SupplierCreate = "Supplier.Create";
        public const string SupplierToggleStatus = "Supplier.ToggleStatus";
        public const string SupplierUpdate = "Supplier.Update";
        public const string SupplierView = "Supplier.View";
        public const string SupplierViewQuality = "Supplier.ViewQuality";
        public const string SupplierQualityCreate = "SupplierQuality.Create";
        public const string SupplierQualityTransition = "SupplierQuality.Transition";
        public const string SupplierQualityView = "SupplierQuality.View";

        public const string SystemCutoverManage = "System.Cutover.Manage";
        public const string SystemCutoverView = "System.Cutover.View";
        public const string SystemDiagnosticsView = "System.Diagnostics.View";
        public const string SystemLegacyConsolidationManage = "System.LegacyConsolidation.Manage";
        public const string SystemLegacyConsolidationView = "System.LegacyConsolidation.View";

        public const string UnitConversionCreate = "UnitConversion.Create";
        public const string UnitConversionToggleStatus = "UnitConversion.ToggleStatus";
        public const string UnitConversionUpdate = "UnitConversion.Update";
        public const string UnitConversionView = "UnitConversion.View";
    }
}
