/*
    Dữ liệu analytics Store 1 cho 46 stored procedure trong
    20260717_DashboardAnalyticsStoredProcedures.idempotent.sql.

    Chạy sau 20260718_CafeChain_Store1_Complete_Demo_Seed.idempotent.sql.
    Tất cả dữ liệu cố định, ngoại trừ một CashSession của ngày hiện tại vì
    dbo.sp_Cash_Flow_Today bắt buộc lọc theo GETDATE().
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;

DECLARE @StoreId int=1;
DECLARE @SeedFrom datetime2(0)='2026-01-15T00:00:00';
DECLARE @ActorStaffId int;
DECLARE @SalesStaffId int;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Orders', N'U') IS NULL
       OR OBJECT_ID(N'dbo.PurchaseOrders', N'U') IS NULL
       OR OBJECT_ID(N'dbo.WorkShifts', N'U') IS NULL
        THROW 51101,N'Database đích chưa có schema Dashboard CafeChain yêu cầu.',1;

    SELECT TOP(1) @ActorStaffId=StaffId FROM dbo.Staffs WHERE StoreId=@StoreId AND Active=1 ORDER BY StaffId;
    SELECT TOP(1) @SalesStaffId=StaffId FROM dbo.Staffs WHERE StoreId=@StoreId AND Active=1 ORDER BY CASE WHEN StaffId=4 THEN 0 ELSE 1 END,StaffId;
    IF @ActorStaffId IS NULL OR @SalesStaffId IS NULL
        THROW 51100,N'Thiếu Staff active của Store 1.',1;

    /* 1. WorkShift và StaffShift */
    DECLARE @ShiftSeed TABLE(
        Marker nvarchar(80) PRIMARY KEY,StartAt datetime2(0),EndAt datetime2(0),
        ExpectedCash decimal(18,2),ActualCash decimal(18,2),RequiresReconciliation bit,LateSync bit);
    INSERT @ShiftSeed VALUES
      (N'DEMO-POS-20260115-AM','2026-01-15T06:00:00','2026-01-15T12:00:00',1850000,1830000,0,0),
      (N'DEMO-POS-20260115-PM','2026-01-15T12:00:00','2026-01-15T18:00:00',2250000,2300000,0,0),
      (N'DEMO-POS-20260116-OFFLINE','2026-01-16T06:00:00','2026-01-16T12:00:00',1650000,1600000,1,1);

    INSERT dbo.WorkShifts(
        StoreId,UserId,StartTime,EndTime,StartingCash,ExpectedEndingCash,ActualEndingCash,
        CashDiscrepancy,Status,DiscrepancyReason,IsExceptionClosed,ExceptionCloseReason,
        OfflineOrderCountAtClose,OfflineEstimatedTotalAtClose,OfflineCashTotalAtClose,
        RequiresReconciliation,HasLateOfflineSync,LateOfflineSyncCount,LastLateOfflineSyncedAt,PosTerminalId)
    SELECT @StoreId,@SalesStaffId,x.StartAt,x.EndAt,500000,x.ExpectedCash,x.ActualCash,
           x.ActualCash-x.ExpectedCash,N'Closed',
           CASE WHEN x.ActualCash<>x.ExpectedCash THEN N'Chênh lệch tiền mặt demo' END,
           x.RequiresReconciliation,CASE WHEN x.RequiresReconciliation=1 THEN N'Đóng ngoại lệ để kiểm thử đối soát' END,
           CASE WHEN x.RequiresReconciliation=1 THEN 2 ELSE 0 END,
           CASE WHEN x.RequiresReconciliation=1 THEN 75000 ELSE 0 END,
           CASE WHEN x.RequiresReconciliation=1 THEN 50000 ELSE 0 END,
           x.RequiresReconciliation,x.LateSync,CASE WHEN x.LateSync=1 THEN 1 ELSE 0 END,
           CASE WHEN x.LateSync=1 THEN DATEADD(minute,30,x.EndAt) END,NULL
    FROM @ShiftSeed x
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.WorkShifts w
        WHERE w.StoreId=@StoreId AND w.UserId=@SalesStaffId AND w.StartTime=x.StartAt);

    INSERT dbo.StaffShifts(
        StaffId,ShiftId,IsAdHoc,CustomStartTime,CustomEndTime,WorkDate,
        ActualCheckIn,ActualCheckOut,PayrollHours,StatusId)
    SELECT @SalesStaffId,NULL,1,CONVERT(time,x.StartAt),CONVERT(time,x.EndAt),CONVERT(date,x.StartAt),
           x.StartAt,x.EndAt,DATEDIFF(minute,x.StartAt,x.EndAt)/60.0,3
    FROM @ShiftSeed x
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.StaffShifts ss
        WHERE ss.StaffId=@SalesStaffId AND ss.WorkDate=CONVERT(date,x.StartAt)
          AND ss.ActualCheckIn=x.StartAt);

    INSERT dbo.CashSessions(StaffId,StoreId,StartCash,EndCash,OpenTime,CloseTime,IsClosed)
    SELECT @SalesStaffId,@StoreId,500000,x.ActualCash,x.StartAt,x.EndAt,1
    FROM @ShiftSeed x
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.CashSessions cs
        WHERE cs.StoreId=@StoreId AND cs.StaffId=@SalesStaffId AND cs.OpenTime=x.StartAt);

    /* Ngoại lệ có chủ đích: procedure sp_Cash_Flow_Today dùng GETDATE(). */
    IF NOT EXISTS (
        SELECT 1 FROM dbo.CashSessions
        WHERE StoreId=@StoreId AND OpenTime>=CONVERT(date,GETDATE())
          AND OpenTime<DATEADD(day,1,CONVERT(date,GETDATE())))
    BEGIN
        INSERT dbo.CashSessions(StaffId,StoreId,StartCash,EndCash,OpenTime,CloseTime,IsClosed)
        VALUES(@SalesStaffId,@StoreId,500000,NULL,DATEADD(hour,6,CONVERT(datetime2,CONVERT(date,GETDATE()))),NULL,0);
    END;

    /* 2. Orders, details, toppings và payments */
    DECLARE @Orders TABLE(
        ClientOrderId uniqueidentifier PRIMARY KEY,CreatedAt datetime2(0),StatusId int,
        PaymentStatusId int,PaymentMethodId int,DrinkCode nvarchar(50),SizeCode nvarchar(20),
        Quantity int,Total decimal(18,2),ShiftMarker nvarchar(80),CustomerId int NULL);
    INSERT @Orders VALUES
      ('10000000-0000-0000-0000-000000000001','2026-01-15T07:15:00',5,2,1,N'CF_BacXiu',N'M',1,33000,N'DEMO-POS-20260115-AM',1),
      ('10000000-0000-0000-0000-000000000002','2026-01-15T08:20:00',5,2,2,N'CF_Latte',N'L',1,45000,N'DEMO-POS-20260115-AM',NULL),
      ('10000000-0000-0000-0000-000000000003','2026-01-15T10:40:00',5,2,3,N'TS_Matcha',N'M',2,74000,N'DEMO-POS-20260115-AM',1),
      ('10000000-0000-0000-0000-000000000004','2026-01-15T13:10:00',5,2,1,N'TTC_CamSa',N'L',1,45000,N'DEMO-POS-20260115-PM',NULL),
      ('10000000-0000-0000-0000-000000000005','2026-01-15T15:30:00',5,2,4,N'NE_Cam',N'M',1,30000,N'DEMO-POS-20260115-PM',1),
      ('10000000-0000-0000-0000-000000000006','2026-01-15T17:45:00',4,1,1,N'CF_Americano',N'M',1,32000,N'DEMO-POS-20260115-PM',NULL),
      ('10000000-0000-0000-0000-000000000007','2026-01-16T07:35:00',5,2,1,N'CF_ColdBrew',N'L',1,48000,N'DEMO-POS-20260116-OFFLINE',1),
      ('10000000-0000-0000-0000-000000000008','2026-01-16T09:05:00',5,2,2,N'TS_OLong',N'M',1,38000,N'DEMO-POS-20260116-OFFLINE',NULL),
      ('10000000-0000-0000-0000-000000000009','2026-01-16T11:25:00',6,1,1,N'TTC_Dau',N'M',1,37000,N'DEMO-POS-20260116-OFFLINE',NULL);

    INSERT dbo.Orders(
        CustomerId,StoreId,OrderStatusId,PaymentStatusId,OrderTypeId,TableId,StaffId,WorkShiftId,
        ClientOrderId,Source,Note,ShippingFee,SubTotal,VoucherDiscount,PointDiscount,PointsUsed,
        Total,CostStatus,TotalCogs,GrossProfit,CostedAtUtc,CreatedAt)
    SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.Customers c WHERE c.CustomerId=x.CustomerId) THEN x.CustomerId END,
           @StoreId,x.StatusId,x.PaymentStatusId,2,NULL,@SalesStaffId,w.ShiftId,
           x.ClientOrderId,N'DEMO_DASHBOARD',N'Đơn hàng demo analytics',0,x.Total,0,0,0,
           x.Total,1,x.Total*0.32,x.Total*0.68,x.CreatedAt,x.CreatedAt
    FROM @Orders x
    JOIN @ShiftSeed shiftSeed ON shiftSeed.Marker=x.ShiftMarker
    JOIN dbo.WorkShifts w ON w.StoreId=@StoreId
                         AND w.UserId=@SalesStaffId
                         AND w.StartTime=shiftSeed.StartAt
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Orders o WHERE o.ClientOrderId=x.ClientOrderId);

    INSERT dbo.OrderDetails(
        OrderId,DrinkId,SizeId,StoreMenuItemId,DrinkSizeId,DrinkName,SizeName,Price,
        AcceptedBasePrice,PriceSource,AcceptedCatalogVersion,Quantity,Note,CostStatus,UnitCogs,TotalCogs)
    SELECT o.OrderId,d.DrinkId,s.SizeId,sm.StoreMenuItemId,ds.DrinkSizeId,d.Name,s.Name,
           ds.Price,ds.Price,N'DEMO_SEED',1,x.Quantity,N'',1,
           ROUND(x.Total*0.32/x.Quantity,2),ROUND(x.Total*0.32,2)
    FROM @Orders x
    JOIN dbo.Orders o ON o.ClientOrderId=x.ClientOrderId
    JOIN dbo.Drinks d ON d.DrinkCode=x.DrinkCode
    JOIN dbo.Sizes s ON s.SizeCode=x.SizeCode
    JOIN dbo.DrinkSizes ds ON ds.DrinkId=d.DrinkId AND ds.SizeId=s.SizeId
    LEFT JOIN dbo.StoreMenuItems sm ON sm.StoreId=@StoreId AND sm.DrinkSizeId=ds.DrinkSizeId
    WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderDetails od WHERE od.OrderId=o.OrderId);

    INSERT dbo.OrderToppings(OrderDetailId,ToppingId,ToppingName,Price,CostStatus,TotalCogs)
    SELECT od.OrderDetailId,t.ToppingId,t.Name,t.Price,1,ROUND(t.Price*0.35,2)
    FROM dbo.Orders o
    JOIN dbo.OrderDetails od ON od.OrderId=o.OrderId
    JOIN dbo.Toppings t ON t.ToppingId=1
    WHERE o.ClientOrderId IN (
        '10000000-0000-0000-0000-000000000003',
        '10000000-0000-0000-0000-000000000004')
      AND NOT EXISTS (SELECT 1 FROM dbo.OrderToppings ot WHERE ot.OrderDetailId=od.OrderDetailId AND ot.ToppingId=t.ToppingId);

    INSERT dbo.Payments(
        OrderId,Amount,ReceivedAmount,ChangeAmount,PaymentMethodId,PaymentStatusId,
        CashSessionId,TransactionCode,PaidAt)
    SELECT o.OrderId,x.Total,x.Total,0,x.PaymentMethodId,2,
           CASE WHEN x.PaymentMethodId=1 THEN cs.CashSessionId END,
           N'DEMO_PAY_'+RIGHT(CONVERT(nvarchar(36),x.ClientOrderId),12),x.CreatedAt
    FROM @Orders x
    JOIN dbo.Orders o ON o.ClientOrderId=x.ClientOrderId
    LEFT JOIN dbo.CashSessions cs ON cs.StoreId=@StoreId AND cs.StaffId=@SalesStaffId
        AND x.CreatedAt>=cs.OpenTime AND x.CreatedAt<COALESCE(cs.CloseTime,DATEADD(day,1,cs.OpenTime))
    WHERE x.StatusId=5
      AND NOT EXISTS (SELECT 1 FROM dbo.Payments p WHERE p.TransactionCode=N'DEMO_PAY_'+RIGHT(CONVERT(nvarchar(36),x.ClientOrderId),12));

    IF NOT EXISTS (SELECT 1 FROM dbo.OrderRefunds WHERE RefundKey='20000000-0000-0000-0000-000000000001')
    BEGIN
        INSERT dbo.OrderRefunds(
            OrderId,StoreId,RefundKey,Status,PaymentMethodId,Reason,RefundAmount,CostStatus,
            ReversedCogs,InventoryReversalStatus,RequestedAtUtc,RequestedByStaffId,
            ProcessingAtUtc,CompletedAtUtc,CompletedByStaffId)
        SELECT o.OrderId,@StoreId,'20000000-0000-0000-0000-000000000001',3,2,
               N'Hoàn tiền demo Dashboard',o.Total,1,o.TotalCogs,2,
               '2026-01-16T10:00:00',@SalesStaffId,'2026-01-16T10:01:00','2026-01-16T10:02:00',@ActorStaffId
        FROM dbo.Orders o WHERE o.ClientOrderId='10000000-0000-0000-0000-000000000008';
    END;

    /* 3. Restock và procurement */
    DECLARE @CoffeeIngredientId int=(SELECT IngredientId FROM dbo.Ingredients WHERE Code=N'ING00001');
    DECLARE @CoffeeOfferId int=(
        SELECT TOP(1) o.IngredientSupplierId
        FROM dbo.IngredientSuppliers o JOIN dbo.Suppliers s ON s.SupplierId=o.SupplierId
        WHERE o.IngredientId=@CoffeeIngredientId AND s.Code=N'DEMO_SUP_COFFEE');
    DECLARE @CoffeeSupplierId int=(SELECT SupplierId FROM dbo.Suppliers WHERE Code=N'DEMO_SUP_COFFEE');

    IF @CoffeeOfferId IS NULL
        THROW 51101,N'Thiếu DEMO_OFFER_COFFEE; hãy chạy seed Store 1 trước.',1;

    IF NOT EXISTS (SELECT 1 FROM dbo.RestockRequests WHERE Note=N'DEMO_DASHBOARD_RESTOCK')
    BEGIN
        INSERT dbo.RestockRequests(
            StockAlertId,StoreId,IngredientId,RecipeId,PreparedItemId,RequestedQuantity,SuggestedQuantity,
            SuggestionAnalysisWindowDays,SuggestionAvailableSnapshot,SuggestionMinLevelSnapshot,
            SuggestionAverageDailyUsageSnapshot,SuggestionLeadTimeDaysSnapshot,SuggestionIncomingQuantitySnapshot,
            SuggestionReason,Status,Priority,CreatedByStaffId,CreatedAt,UpdatedAt,Note,
            ClosedRemainingQuantity)
        VALUES(NULL,@StoreId,@CoffeeIngredientId,NULL,NULL,5000,6500,30,1000,2000,1200,2,0,
               N'Tồn thấp hơn nhu cầu trong thời gian giao hàng',N'APPROVED',N'HIGH',@ActorStaffId,
               '2026-01-15T08:30:00','2026-01-15T08:30:00',N'DEMO_DASHBOARD_RESTOCK',0);
    END;

    DECLARE @RestockRequestId int=(SELECT TOP(1) RestockRequestId FROM dbo.RestockRequests WHERE Note=N'DEMO_DASHBOARD_RESTOCK');

    IF NOT EXISTS (SELECT 1 FROM dbo.PurchaseOrders WHERE Code=N'DEMO-PO-20260115-SENT')
    BEGIN
        INSERT dbo.PurchaseOrders(
            Code,StoreId,SupplierId,Status,OrderDate,ExpectedDeliveryAtUtc,CreatedByStaffId,
            ApprovedByStaffId,SentByStaffId,CreatedAtUtc,UpdatedAtUtc,ApprovedAtUtc,SentAtUtc,Note)
        VALUES(N'DEMO-PO-20260115-SENT',@StoreId,@CoffeeSupplierId,N'SENT','2026-01-15T09:00:00',
               '2026-01-16T09:00:00',@ActorStaffId,@ActorStaffId,@ActorStaffId,
               '2026-01-15T09:00:00','2026-01-15T09:10:00','2026-01-15T09:05:00','2026-01-15T09:10:00',N'PO quá hạn demo');
    END;

    DECLARE @PurchaseOrderId int=(SELECT PurchaseOrderId FROM dbo.PurchaseOrders WHERE Code=N'DEMO-PO-20260115-SENT');
    IF NOT EXISTS (SELECT 1 FROM dbo.PurchaseOrderLines WHERE PurchaseOrderId=@PurchaseOrderId AND IngredientId=@CoffeeIngredientId)
    BEGIN
        INSERT dbo.PurchaseOrderLines(
            PurchaseOrderId,RestockRequestId,IngredientId,IngredientSupplierId,PackageUnitIdSnapshot,
            PackageQuantitySnapshot,PackagePriceSnapshot,PackageCount,OrderedBaseQuantity,
            PromisedLeadTimeDaysSnapshot,Note)
        SELECT @PurchaseOrderId,@RestockRequestId,@CoffeeIngredientId,@CoffeeOfferId,o.UnitId,
               o.PackageQuantity,o.CurrentPrice,5,5000,COALESCE(o.LeadTimeDays,2),N'DEMO_DASHBOARD_PO_LINE'
        FROM dbo.IngredientSuppliers o WHERE o.IngredientSupplierId=@CoffeeOfferId;
    END;

    DECLARE @PurchaseOrderLineId int=(SELECT TOP(1) PurchaseOrderLineId FROM dbo.PurchaseOrderLines WHERE PurchaseOrderId=@PurchaseOrderId);
    IF NOT EXISTS (SELECT 1 FROM dbo.BranchReceipts WHERE ReceiptCode=N'DEMO-BR-20260116')
    BEGIN
        INSERT dbo.BranchReceipts(
            ReceiptCode,StoreId,SupplierId,PurchaseOrderId,Status,ReceiptKey,ReferenceNumber,
            ReceivedAt,ReceivedByStaffId,ConfirmedAt,ConfirmedByStaffId,Notes,CreatedAt,CreatedByStaffId)
        VALUES(N'DEMO-BR-20260116',@StoreId,@CoffeeSupplierId,@PurchaseOrderId,N'CONFIRMED',
               N'DEMO-BR-STORE1-20260116',N'HD-DEMO-001','2026-01-16T09:30:00',@ActorStaffId,
               '2026-01-16T09:35:00',@ActorStaffId,N'Phiếu nhận hàng demo analytics','2026-01-16T09:30:00',@ActorStaffId);
    END;

    DECLARE @BranchReceiptId int=(SELECT BranchReceiptId FROM dbo.BranchReceipts WHERE ReceiptCode=N'DEMO-BR-20260116');
    IF NOT EXISTS (SELECT 1 FROM dbo.BranchReceiptLines WHERE BranchReceiptId=@BranchReceiptId AND PurchaseOrderLineId=@PurchaseOrderLineId)
    BEGIN
        INSERT dbo.BranchReceiptLines(
            BranchReceiptId,RestockRequestId,PurchaseOrderLineId,IngredientId,InputQuantity,InputUnitId,
            ReceivedBaseQuantity,RejectedBaseQuantity,RejectionReason,RejectionIssueType,BaseUnitId,
            SupplierId,IngredientSupplierId,ActualPackagePrice,PackageQuantitySnapshot,PackageUnitIdSnapshot,
            BaseUnitCostSnapshot,LineTotalCost,CreatedAt)
        SELECT @BranchReceiptId,@RestockRequestId,@PurchaseOrderLineId,@CoffeeIngredientId,5,o.UnitId,
               4800,200,N'Bao bì rách',N'DAMAGED_PACKAGE',i.BaseUnitId,@CoffeeSupplierId,@CoffeeOfferId,
               o.CurrentPrice,o.PackageQuantity,o.UnitId,180,900000,'2026-01-16T09:30:00'
        FROM dbo.IngredientSuppliers o JOIN dbo.Ingredients i ON i.IngredientId=o.IngredientId
        WHERE o.IngredientSupplierId=@CoffeeOfferId;
    END;

    DECLARE @BranchReceiptLineId int=(SELECT TOP(1) BranchReceiptLineId FROM dbo.BranchReceiptLines WHERE BranchReceiptId=@BranchReceiptId);
    IF NOT EXISTS (
        SELECT 1 FROM dbo.SupplierReceiptIssues
        WHERE BranchReceiptLineId=@BranchReceiptLineId AND IssueType=N'DAMAGED_PACKAGE')
    BEGIN
        INSERT dbo.SupplierReceiptIssues(
            SupplierId,StoreId,PurchaseOrderId,PurchaseOrderLineId,BranchReceiptId,BranchReceiptLineId,
            IssueType,Status,AffectedBaseQuantity,Description,ReportedByStaffId,ReportedAtUtc,UpdatedAtUtc)
        VALUES(@CoffeeSupplierId,@StoreId,@PurchaseOrderId,@PurchaseOrderLineId,@BranchReceiptId,@BranchReceiptLineId,
               N'DAMAGED_PACKAGE',N'OPEN',200,N'Bao bì rách khi nhận hàng',@ActorStaffId,
               '2026-01-16T09:31:00','2026-01-16T09:31:00');
    END;

    /* 4. Một movement WASTE để báo cáo kho có dữ liệu. */
    DECLARE @WasteDocumentId int=(SELECT InventoryDocumentId FROM dbo.InventoryDocuments WHERE RequestKey=N'DEMO-DASHBOARD-WASTE');
    IF @WasteDocumentId IS NULL
    BEGIN
        INSERT dbo.InventoryDocuments(
            Code,StoreId,StaffId,DocumentDate,Type,Status,RequestKey,IsProcessing,ConfirmedAt,ConfirmedBy,
            Purpose,PartnerType,Note,TotalAmount,VatAmount,FinalAmount)
        VALUES(N'DEMO-WASTE-20260116',@StoreId,@ActorStaffId,'2026-01-16T16:00:00',3,3,
               N'DEMO-DASHBOARD-WASTE',0,'2026-01-16T16:00:00',@ActorStaffId,12,0,
               N'Hủy nguyên liệu hỏng để kiểm thử Dashboard',36000,0,36000);
        SET @WasteDocumentId=SCOPE_IDENTITY();
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.InventoryDocumentDetails WHERE InventoryDocumentId=@WasteDocumentId)
    BEGIN
        INSERT dbo.InventoryDocumentDetails(
            InventoryDocumentId,IngredientId,Quantity,BaseQuantity,UnitId,UnitPrice,CostPrice,CostAmount,Note,TotalAmount)
        SELECT @WasteDocumentId,i.IngredientId,200,200,i.BaseUnitId,180,180,36000,N'DEMO_DASHBOARD_WASTE',36000
        FROM dbo.Ingredients i WHERE i.Code=N'ING00001';
    END;

    DECLARE @WasteDetailId int=(SELECT TOP(1) InventoryDocumentDetailId FROM dbo.InventoryDocumentDetails WHERE InventoryDocumentId=@WasteDocumentId);
    DECLARE @WasteInventoryId int=(SELECT StoreInventoryId FROM dbo.StoreInventories WHERE StoreId=@StoreId AND IngredientId=@CoffeeIngredientId);
    IF NOT EXISTS (SELECT 1 FROM dbo.InventoryTransactions WHERE InventoryDocumentDetailId=@WasteDetailId AND Type=3)
    BEGIN
        DECLARE @BeforeWaste decimal(18,5)=(SELECT AvailableQty FROM dbo.StoreInventories WHERE StoreInventoryId=@WasteInventoryId);
        UPDATE dbo.StoreInventories SET AvailableQty=AvailableQty-200,LastUpdated='2026-01-16T16:00:00' WHERE StoreInventoryId=@WasteInventoryId;
        INSERT dbo.InventoryTransactions(
            StoreInventoryId,Type,StockStatus,Quantity,BeforeQty,AfterQty,UnitCost,TotalCost,
            InventoryDocumentId,InventoryDocumentDetailId,CreatedAt)
        VALUES(@WasteInventoryId,3,1,200,@BeforeWaste,@BeforeWaste-200,180,36000,@WasteDocumentId,@WasteDetailId,'2026-01-16T16:00:00');
    END;

    COMMIT;

    SELECT N'DASHBOARD_SEED_OK' AS Result,
           (SELECT COUNT(*) FROM dbo.Orders WHERE Source=N'DEMO_DASHBOARD') AS DemoOrders,
           (SELECT COUNT(*) FROM dbo.PurchaseOrders WHERE Code LIKE N'DEMO-PO-%') AS DemoPurchaseOrders,
           (SELECT COUNT(*) FROM dbo.SupplierReceiptIssues WHERE Description LIKE N'%kiểm thử%' OR Description LIKE N'%rách%') AS DemoSupplierIssues;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    THROW;
END CATCH;
GO
