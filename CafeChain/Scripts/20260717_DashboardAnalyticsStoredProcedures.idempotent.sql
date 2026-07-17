/* CafeChain dashboard analytics — SQL Server, idempotent, schema-aligned 2026-07-17. */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER FUNCTION dbo.ufn_AnalyticsStoreScope(@StoreIds nvarchar(max))
RETURNS TABLE
AS
RETURN
(
    SELECT s.StoreId
    FROM dbo.Stores AS s
    WHERE @StoreIds IS NULL
       OR LTRIM(RTRIM(@StoreIds)) = N''
       OR EXISTS
       (
           SELECT 1
           FROM STRING_SPLIT(@StoreIds, N',') AS value
           WHERE TRY_CONVERT(int, LTRIM(RTRIM(value.value))) = s.StoreId
       )
);
GO

CREATE OR ALTER PROCEDURE dbo.usp_Dashboard_NetSalesTrend
    @FromDate date, @ToDate date, @StoreIds nvarchar(max),
    @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    IF @FromDate IS NULL OR @ToDate IS NULL OR @FromDate > @ToDate THROW 50001, 'Invalid date range.', 1;
    DECLARE @ToExclusive datetime2 = DATEADD(day, 1, CONVERT(datetime2, @ToDate));
    ;WITH Dates AS
    (
        SELECT @FromDate AS BucketDate
        UNION ALL SELECT DATEADD(day, 1, BucketDate) FROM Dates WHERE BucketDate < @ToDate
    ), Events AS
    (
        SELECT CONVERT(date, o.CreatedAt) AS EventDate, 1 AS OrderCount,
               CONVERT(decimal(19,2), o.Total - o.ShippingFee) AS NetSales
        FROM dbo.Orders AS o
        INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = o.StoreId
        WHERE o.OrderStatusId = 5 AND o.CreatedAt >= @FromDate AND o.CreatedAt < @ToExclusive
        UNION ALL
        SELECT CONVERT(date, r.CompletedAtUtc), 0,
               -CONVERT(decimal(19,2), o.Total - o.ShippingFee)
        FROM dbo.OrderRefunds AS r
        INNER JOIN dbo.Orders AS o ON o.OrderId = r.OrderId AND o.OrderStatusId = 5
        INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = r.StoreId
        WHERE r.Status = 3 AND r.CompletedAtUtc >= @FromDate AND r.CompletedAtUtc < @ToExclusive
    )
    SELECT d.BucketDate, COALESCE(SUM(e.OrderCount), 0) AS TotalOrders,
           COALESCE(SUM(e.NetSales), 0) AS NetSales,
           CASE WHEN SUM(CASE WHEN e.EventDate IS NOT NULL THEN 1 ELSE 0 END) = 0 THEN 'NO_DATA' ELSE 'AVAILABLE' END AS DataStatus
    FROM Dates AS d
    LEFT JOIN Events AS e ON e.EventDate = d.BucketDate
    GROUP BY d.BucketDate
    ORDER BY d.BucketDate
    OPTION (MAXRECURSION 32767);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Inventory_ShortageRisk
    @FromDate date, @ToDate date, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) si.StoreInventoryId, si.StoreId, si.IngredientId, i.Name AS IngredientName,
           si.AvailableQty, si.ReservedQty, si.MinStockLevel,
           CASE WHEN si.AvailableQty < 0 THEN 'CRITICAL' WHEN si.MinStockLevel IS NULL THEN 'UNCONFIGURED'
                WHEN si.AvailableQty <= si.MinStockLevel THEN 'HIGH' ELSE 'NORMAL' END AS RiskLevel,
           CASE WHEN si.MinStockLevel IS NULL THEN 'THRESHOLD_NOT_CONFIGURED' ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.StoreInventories AS si
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=si.StoreId
    LEFT JOIN dbo.Ingredients AS i ON i.IngredientId=si.IngredientId
    WHERE si.IngredientId IS NOT NULL
    ORDER BY CASE WHEN si.AvailableQty < 0 THEN 0 WHEN si.MinStockLevel IS NULL THEN 2 ELSE 1 END,
             si.AvailableQty-COALESCE(si.MinStockLevel,0), si.StoreInventoryId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Inventory_MovementByType
    @FromDate date, @ToDate date, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CONVERT(date,it.CreatedAt) AS MovementDate, it.Type AS TransactionType,
           COUNT_BIG(it.InventoryTransactionId) AS TransactionCount,
           SUM(it.Quantity) AS Quantity, COALESCE(SUM(it.TotalCost),0) AS TotalCost, 'AVAILABLE' AS DataStatus
    FROM dbo.InventoryTransactions AS it
    INNER JOIN dbo.StoreInventories AS si ON si.StoreInventoryId=it.StoreInventoryId
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=si.StoreId
    WHERE it.CreatedAt>=@FromDate AND it.CreatedAt<DATEADD(day,1,CONVERT(datetime2,@ToDate))
    GROUP BY CONVERT(date,it.CreatedAt),it.Type ORDER BY MovementDate,it.Type;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Inventory_ThresholdRisk
    @FromDate date, @ToDate date, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT si.StoreInventoryId,si.StoreId,si.IngredientId,i.Name AS IngredientName,
           si.AvailableQty,si.ReservedQty,si.MinStockLevel,si.MaxNegativeQty,
           si.AvailableQty-COALESCE(si.MinStockLevel,0) AS QuantityAboveMinimum,
           CASE WHEN si.MinStockLevel IS NULL THEN 'THRESHOLD_NOT_CONFIGURED'
                WHEN si.AvailableQty<0 THEN 'NEGATIVE' WHEN si.AvailableQty<=si.MinStockLevel THEN 'BELOW_MINIMUM'
                ELSE 'HEALTHY' END AS DataStatus
    FROM dbo.StoreInventories AS si INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=si.StoreId
    LEFT JOIN dbo.Ingredients AS i ON i.IngredientId=si.IngredientId
    WHERE si.IngredientId IS NOT NULL ORDER BY si.StoreId,QuantityAboveMinimum;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Inventory_ReorderSuggestions
    @FromDate date, @ToDate date, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) rr.RestockRequestId,rr.StoreId,rr.IngredientId,i.Name AS IngredientName,
           rr.RequestedQuantity,rr.SuggestedQuantity,rr.SuggestionAverageDailyUsageSnapshot,
           rr.SuggestionLeadTimeDaysSnapshot,rr.SuggestionIncomingQuantitySnapshot,rr.SuggestionReason,
           rr.Status,rr.Priority,rr.CreatedAt,'AVAILABLE' AS DataStatus
    FROM dbo.RestockRequests AS rr INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=rr.StoreId
    LEFT JOIN dbo.Ingredients AS i ON i.IngredientId=rr.IngredientId
    WHERE rr.CreatedAt>=@FromDate AND rr.CreatedAt<DATEADD(day,1,CONVERT(datetime2,@ToDate))
    ORDER BY CASE rr.Priority WHEN 'URGENT' THEN 0 WHEN 'HIGH' THEN 1 ELSE 2 END,rr.CreatedAt DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Inventory_WasteByStoreIngredient
    @FromDate date, @ToDate date, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) si.StoreId,s.Name AS StoreName,si.IngredientId,i.Name AS IngredientName,
           SUM(ABS(it.Quantity)) AS WasteQuantity,COALESCE(SUM(ABS(it.TotalCost)),0) AS WasteValue,
           COUNT_BIG(it.InventoryTransactionId) AS TransactionCount,'AVAILABLE' AS DataStatus
    FROM dbo.InventoryTransactions AS it INNER JOIN dbo.StoreInventories AS si ON si.StoreInventoryId=it.StoreInventoryId
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=si.StoreId
    INNER JOIN dbo.Stores AS s ON s.StoreId=si.StoreId LEFT JOIN dbo.Ingredients AS i ON i.IngredientId=si.IngredientId
    WHERE it.Type=3 AND it.CreatedAt>=@FromDate AND it.CreatedAt<DATEADD(day,1,CONVERT(datetime2,@ToDate))
    GROUP BY si.StoreId,s.Name,si.IngredientId,i.Name ORDER BY WasteValue DESC,WasteQuantity DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Inventory_FifoLayerAge
    @FromDate date, @ToDate date, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) l.InventoryCostLayerId,l.StoreId,l.IngredientId,l.PreparedItemId,
           l.RemainingQuantity,l.UnitCost,l.CreatedAt,DATEDIFF(day,l.CreatedAt,SYSUTCDATETIME()) AS AgeDays,
           l.RemainingQuantity*l.UnitCost AS RemainingValue,
           CASE WHEN l.RemainingQuantity<=0 THEN 'DEPLETED' WHEN DATEDIFF(day,l.CreatedAt,SYSUTCDATETIME())>=90 THEN 'AGED_90_PLUS'
                WHEN DATEDIFF(day,l.CreatedAt,SYSUTCDATETIME())>=30 THEN 'AGED_30_PLUS' ELSE 'CURRENT' END AS DataStatus
    FROM dbo.InventoryCostLayers AS l INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=l.StoreId
    WHERE l.RemainingQuantity>0 ORDER BY AgeDays DESC,l.InventoryCostLayerId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Procurement_PurchaseOrderPipeline
    @FromDate date,@ToDate date,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT po.Status,COUNT_BIG(po.PurchaseOrderId) AS PurchaseOrderCount,
           COALESCE(SUM(line.OrderValue),0) AS OrderedValue,'AVAILABLE' AS DataStatus
    FROM dbo.PurchaseOrders AS po INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=po.StoreId
    OUTER APPLY(SELECT SUM(pol.PackagePriceSnapshot*pol.PackageCount) AS OrderValue FROM dbo.PurchaseOrderLines AS pol WHERE pol.PurchaseOrderId=po.PurchaseOrderId) line
    WHERE po.OrderDate>=@FromDate AND po.OrderDate<DATEADD(day,1,CONVERT(datetime2,@ToDate))
    GROUP BY po.Status ORDER BY PurchaseOrderCount DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Procurement_OverduePurchaseOrders
    @FromDate date,@ToDate date,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) po.PurchaseOrderId,po.Code,po.StoreId,po.SupplierId,s.Name AS SupplierName,
           po.Status,po.OrderDate,po.ExpectedDeliveryAtUtc,DATEDIFF(day,po.ExpectedDeliveryAtUtc,SYSUTCDATETIME()) AS OverdueDays,
           'OVERDUE' AS DataStatus
    FROM dbo.PurchaseOrders AS po INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=po.StoreId
    INNER JOIN dbo.Suppliers AS s ON s.SupplierId=po.SupplierId
    WHERE po.ExpectedDeliveryAtUtc<SYSUTCDATETIME() AND po.Status NOT IN('COMPLETED','CANCELLED')
    ORDER BY OverdueDays DESC,po.PurchaseOrderId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Procurement_SupplierQuality
    @FromDate date,@ToDate date,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) br.SupplierId,s.Name AS SupplierName,
           SUM(brl.ReceivedBaseQuantity) AS AcceptedBaseQuantity,SUM(brl.RejectedBaseQuantity) AS RejectedBaseQuantity,
           CONVERT(decimal(9,4),COALESCE(SUM(brl.RejectedBaseQuantity)/NULLIF(SUM(brl.ReceivedBaseQuantity+brl.RejectedBaseQuantity),0),0)) AS RejectionRate,
           COUNT_BIG(DISTINCT br.BranchReceiptId) AS ReceiptCount,'AVAILABLE' AS DataStatus
    FROM dbo.BranchReceipts AS br INNER JOIN dbo.BranchReceiptLines AS brl ON brl.BranchReceiptId=br.BranchReceiptId
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=br.StoreId
    LEFT JOIN dbo.Suppliers AS s ON s.SupplierId=br.SupplierId
    WHERE br.Status='CONFIRMED' AND br.ReceivedAt>=@FromDate AND br.ReceivedAt<DATEADD(day,1,CONVERT(datetime2,@ToDate))
    GROUP BY br.SupplierId,s.Name ORDER BY RejectionRate DESC,RejectedBaseQuantity DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Procurement_PurchasePriceTrend
    @FromDate date,@ToDate date,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CONVERT(date,br.ReceivedAt) AS ReceiptDate,brl.IngredientId,i.Name AS IngredientName,
           AVG(brl.BaseUnitCostSnapshot) AS AverageBaseUnitCost,MIN(brl.BaseUnitCostSnapshot) AS MinimumBaseUnitCost,
           MAX(brl.BaseUnitCostSnapshot) AS MaximumBaseUnitCost,SUM(brl.ReceivedBaseQuantity) AS ReceivedBaseQuantity,'AVAILABLE' AS DataStatus
    FROM dbo.BranchReceipts AS br INNER JOIN dbo.BranchReceiptLines AS brl ON brl.BranchReceiptId=br.BranchReceiptId
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=br.StoreId LEFT JOIN dbo.Ingredients AS i ON i.IngredientId=brl.IngredientId
    WHERE br.Status='CONFIRMED' AND brl.IngredientId IS NOT NULL AND br.ReceivedAt>=@FromDate AND br.ReceivedAt<DATEADD(day,1,CONVERT(datetime2,@ToDate))
    GROUP BY CONVERT(date,br.ReceivedAt),brl.IngredientId,i.Name ORDER BY ReceiptDate,brl.IngredientId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Procurement_SpendBreakdown
    @FromDate date,@ToDate date,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) br.SupplierId,s.Name AS SupplierName,br.StoreId,
           SUM(brl.LineTotalCost) AS Spend,COUNT_BIG(DISTINCT br.BranchReceiptId) AS ReceiptCount,'AVAILABLE' AS DataStatus
    FROM dbo.BranchReceipts AS br INNER JOIN dbo.BranchReceiptLines AS brl ON brl.BranchReceiptId=br.BranchReceiptId
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=br.StoreId LEFT JOIN dbo.Suppliers AS s ON s.SupplierId=br.SupplierId
    WHERE br.Status='CONFIRMED' AND br.ReceivedAt>=@FromDate AND br.ReceivedAt<DATEADD(day,1,CONVERT(datetime2,@ToDate))
    GROUP BY br.SupplierId,s.Name,br.StoreId ORDER BY Spend DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Procurement_SupplierIssueMix
    @FromDate date,@ToDate date,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT issue.IssueType,issue.Status,COUNT_BIG(issue.SupplierReceiptIssueId) AS IssueCount,
           SUM(issue.AffectedBaseQuantity) AS AffectedBaseQuantity,'AVAILABLE' AS DataStatus
    FROM dbo.SupplierReceiptIssues AS issue INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=issue.StoreId
    WHERE issue.ReportedAtUtc>=@FromDate AND issue.ReportedAtUtc<DATEADD(day,1,CONVERT(datetime2,@ToDate))
    GROUP BY issue.IssueType,issue.Status ORDER BY IssueCount DESC,issue.IssueType;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Dashboard_StoreRanking
    @FromDate date, @ToDate date, @StoreIds nvarchar(max),
    @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ToExclusive datetime2 = DATEADD(day, 1, CONVERT(datetime2, @ToDate));
    SELECT TOP (ISNULL(NULLIF(@Top, 0), 10)) s.StoreId, s.Name AS StoreName,
           COUNT_BIG(o.OrderId) AS TotalOrders,
           COALESCE(SUM(o.Total - o.ShippingFee - CASE WHEN r.OrderRefundId IS NULL THEN 0 ELSE o.Total - o.ShippingFee END), 0) AS NetSales,
           COALESCE(AVG(CONVERT(decimal(19,2), o.Total - o.ShippingFee)), 0) AS AverageOrderValue,
           CASE WHEN COUNT_BIG(o.OrderId) = 0 THEN 'NO_DATA' ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope
    INNER JOIN dbo.Stores AS s ON s.StoreId = scope.StoreId
    LEFT JOIN dbo.Orders AS o ON o.StoreId = s.StoreId AND o.OrderStatusId = 5
        AND o.CreatedAt >= @FromDate AND o.CreatedAt < @ToExclusive
    LEFT JOIN dbo.OrderRefunds AS r ON r.OrderId = o.OrderId AND r.Status = 3
    GROUP BY s.StoreId, s.Name
    ORDER BY NetSales DESC, s.StoreId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Dashboard_PaymentMethodMix
    @FromDate date, @ToDate date, @StoreIds nvarchar(max),
    @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ToExclusive datetime2 = DATEADD(day, 1, CONVERT(datetime2, @ToDate));
    SELECT pm.PaymentMethodId, pm.Code AS PaymentMethodCode, pm.Name AS PaymentMethodName,
           COUNT_BIG(p.PaymentId) AS TotalTransactions,
           COALESCE(SUM(p.Amount), 0) AS Amount,
           CONVERT(decimal(9,4), COALESCE(SUM(p.Amount) / NULLIF(SUM(SUM(p.Amount)) OVER (), 0), 0)) AS Share,
           'AVAILABLE' AS DataStatus
    FROM dbo.Payments AS p
    INNER JOIN dbo.PaymentMethods AS pm ON pm.PaymentMethodId = p.PaymentMethodId
    INNER JOIN dbo.Orders AS o ON o.OrderId = p.OrderId AND o.OrderStatusId = 5
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = o.StoreId
    WHERE p.PaymentStatusId = 2 AND COALESCE(p.PaidAt, o.CreatedAt) >= @FromDate
      AND COALESCE(p.PaidAt, o.CreatedAt) < @ToExclusive
    GROUP BY pm.PaymentMethodId, pm.Code, pm.Name
    ORDER BY Amount DESC, pm.PaymentMethodId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Dashboard_OrderHeatmap
    @FromDate date, @ToDate date, @StoreIds nvarchar(max),
    @Granularity varchar(10) = 'Hour', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ToExclusive datetime2 = DATEADD(day, 1, CONVERT(datetime2, @ToDate));
    ;WITH WeekDays AS (SELECT value AS IsoWeekday FROM (VALUES(1),(2),(3),(4),(5),(6),(7)) d(value)),
    Hours AS (SELECT 0 AS HourOfDay UNION ALL SELECT HourOfDay + 1 FROM Hours WHERE HourOfDay < 23),
    Actual AS
    (
        SELECT 1 + (DATEDIFF(day, '19000101', CONVERT(date, o.CreatedAt)) % 7) AS IsoWeekday,
               DATEPART(hour, o.CreatedAt) AS HourOfDay, COUNT_BIG(o.OrderId) AS TotalOrders,
               SUM(o.Total - o.ShippingFee) AS NetSales
        FROM dbo.Orders AS o
        INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = o.StoreId
        WHERE o.OrderStatusId = 5 AND o.CreatedAt >= @FromDate AND o.CreatedAt < @ToExclusive
        GROUP BY 1 + (DATEDIFF(day, '19000101', CONVERT(date, o.CreatedAt)) % 7), DATEPART(hour, o.CreatedAt)
    )
    SELECT w.IsoWeekday, h.HourOfDay, COALESCE(a.TotalOrders, 0) AS TotalOrders,
           COALESCE(a.NetSales, 0) AS NetSales,
           CASE WHEN a.TotalOrders IS NULL THEN 'NO_DATA' ELSE 'AVAILABLE' END AS DataStatus
    FROM WeekDays AS w CROSS JOIN Hours AS h
    LEFT JOIN Actual AS a ON a.IsoWeekday = w.IsoWeekday AND a.HourOfDay = h.HourOfDay
    ORDER BY w.IsoWeekday, h.HourOfDay OPTION (MAXRECURSION 24);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Dashboard_OperationalAlerts
    @FromDate date, @ToDate date, @StoreIds nvarchar(max),
    @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top, 0), 10)) alert.AlertType, alert.StoreId, alert.EntityId,
           alert.Severity, alert.AlertValue, alert.Message, alert.DataStatus
    FROM
    (
        SELECT 'LOW_STOCK' AS AlertType, si.StoreId, si.StoreInventoryId AS EntityId,
               CASE WHEN si.AvailableQty < 0 THEN 'CRITICAL' ELSE 'WARNING' END AS Severity,
               si.AvailableQty AS AlertValue, CONCAT('Tồn dưới ngưỡng: ', i.Name) AS Message, 'AVAILABLE' AS DataStatus
        FROM dbo.StoreInventories AS si
        INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = si.StoreId
        LEFT JOIN dbo.Ingredients AS i ON i.IngredientId = si.IngredientId
        WHERE si.MinStockLevel IS NOT NULL AND si.AvailableQty <= si.MinStockLevel
        UNION ALL
        SELECT 'CASH_DISCREPANCY', w.StoreId, w.ShiftId,
               CASE WHEN ABS(COALESCE(w.CashDiscrepancy, 0)) >= 50000 THEN 'CRITICAL' ELSE 'WARNING' END,
               COALESCE(w.CashDiscrepancy, 0), CONCAT('Chênh lệch WorkShift #', w.ShiftId), 'AVAILABLE'
        FROM dbo.WorkShifts AS w
        INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = w.StoreId
        WHERE w.EndTime >= @FromDate AND w.EndTime < DATEADD(day, 1, CONVERT(datetime2, @ToDate))
          AND ABS(COALESCE(w.CashDiscrepancy, 0)) > 0
        UNION ALL
        SELECT 'OVERDUE_PO', po.StoreId, po.PurchaseOrderId, 'WARNING',
               DATEDIFF(day, po.ExpectedDeliveryAtUtc, SYSUTCDATETIME()), CONCAT('PO quá hạn: ', po.Code), 'AVAILABLE'
        FROM dbo.PurchaseOrders AS po
        INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = po.StoreId
        WHERE po.ExpectedDeliveryAtUtc < SYSUTCDATETIME() AND po.Status NOT IN ('COMPLETED', 'CANCELLED')
    ) AS alert
    ORDER BY CASE alert.Severity WHEN 'CRITICAL' THEN 0 ELSE 1 END, ABS(alert.AlertValue) DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Operations_WorkShiftCashDiscrepancy
    @FromDate date, @ToDate date, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT w.ShiftId AS WorkShiftId, w.StoreId, s.Name AS StoreName, w.UserId AS StaffId, st.FullName,
           w.StartTime, w.EndTime, w.StartingCash, w.ExpectedEndingCash, w.ActualEndingCash,
           w.CashDiscrepancy, w.DiscrepancyReason, w.IsExceptionClosed, w.RequiresReconciliation,
           CASE WHEN w.EndTime IS NULL THEN 'OPEN' ELSE 'CLOSED' END AS DataStatus
    FROM dbo.WorkShifts AS w
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = w.StoreId
    INNER JOIN dbo.Stores AS s ON s.StoreId = w.StoreId
    INNER JOIN dbo.Staffs AS st ON st.StaffId = w.UserId
    WHERE w.StartTime < DATEADD(day, 1, CONVERT(datetime2, @ToDate))
      AND COALESCE(w.EndTime, DATEADD(day, 1, CONVERT(datetime2, @ToDate))) >= @FromDate
    ORDER BY w.StartTime DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Operations_WorkShiftSales
    @FromDate date, @ToDate date, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT w.ShiftId AS WorkShiftId, w.StoreId, COUNT_BIG(o.OrderId) AS TotalOrders,
           COALESCE(SUM(o.Total - o.ShippingFee), 0) AS NetSales,
           COALESCE(AVG(CONVERT(decimal(19,2), o.Total - o.ShippingFee)), 0) AS AverageOrderValue,
           CASE WHEN COUNT_BIG(o.OrderId) = 0 THEN 'NO_DATA' ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.WorkShifts AS w
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = w.StoreId
    LEFT JOIN dbo.Orders AS o ON o.WorkShiftId = w.ShiftId AND o.OrderStatusId = 5
    WHERE w.StartTime >= @FromDate AND w.StartTime < DATEADD(day, 1, CONVERT(datetime2, @ToDate))
    GROUP BY w.ShiftId, w.StoreId ORDER BY w.ShiftId DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Operations_WorkShiftPaymentMix
    @FromDate date, @ToDate date, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT o.WorkShiftId, o.StoreId, pm.PaymentMethodId, pm.Code AS PaymentMethodCode, pm.Name AS PaymentMethodName,
           COUNT_BIG(p.PaymentId) AS TotalTransactions, SUM(p.Amount) AS Amount, 'AVAILABLE' AS DataStatus
    FROM dbo.Payments AS p INNER JOIN dbo.Orders AS o ON o.OrderId = p.OrderId AND o.OrderStatusId = 5
    INNER JOIN dbo.PaymentMethods AS pm ON pm.PaymentMethodId = p.PaymentMethodId
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = o.StoreId
    WHERE o.WorkShiftId IS NOT NULL AND p.PaymentStatusId = 2 AND o.CreatedAt >= @FromDate
      AND o.CreatedAt < DATEADD(day, 1, CONVERT(datetime2, @ToDate))
    GROUP BY o.WorkShiftId, o.StoreId, pm.PaymentMethodId, pm.Code, pm.Name
    ORDER BY o.WorkShiftId DESC, Amount DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Operations_OfflineReconciliationExceptions
    @FromDate date, @ToDate date, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT w.ShiftId AS WorkShiftId, w.StoreId, w.IsExceptionClosed, w.OfflineOrderCountAtClose,
           w.OfflineEstimatedTotalAtClose, w.OfflineCashTotalAtClose, w.RequiresReconciliation,
           w.HasLateOfflineSync, w.LateOfflineSyncCount, w.LastLateOfflineSyncedAt,
           CASE WHEN w.RequiresReconciliation = 1 THEN 'REQUIRES_RECONCILIATION' ELSE 'LATE_SYNC' END AS DataStatus
    FROM dbo.WorkShifts AS w INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = w.StoreId
    WHERE w.StartTime >= @FromDate AND w.StartTime < DATEADD(day, 1, CONVERT(datetime2, @ToDate))
      AND (w.IsExceptionClosed = 1 OR w.RequiresReconciliation = 1 OR w.HasLateOfflineSync = 1)
    ORDER BY w.StartTime DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Operations_HourlyOrders
    @FromDate date, @ToDate date, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Hour', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    ;WITH Hours AS (SELECT 0 AS HourOfDay UNION ALL SELECT HourOfDay + 1 FROM Hours WHERE HourOfDay < 23),
    Actual AS
    (
        SELECT DATEPART(hour, o.CreatedAt) AS HourOfDay, COUNT_BIG(o.OrderId) AS TotalOrders,
               SUM(o.Total - o.ShippingFee) AS NetSales
        FROM dbo.Orders AS o INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = o.StoreId
        WHERE o.OrderStatusId = 5 AND o.CreatedAt >= @FromDate
          AND o.CreatedAt < DATEADD(day, 1, CONVERT(datetime2, @ToDate))
        GROUP BY DATEPART(hour, o.CreatedAt)
    )
    SELECT h.HourOfDay, COALESCE(a.TotalOrders, 0) AS TotalOrders, COALESCE(a.NetSales, 0) AS NetSales,
           CASE WHEN a.TotalOrders IS NULL THEN 'NO_DATA' ELSE 'AVAILABLE' END AS DataStatus
    FROM Hours AS h LEFT JOIN Actual AS a ON a.HourOfDay = h.HourOfDay
    ORDER BY h.HourOfDay OPTION (MAXRECURSION 24);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Operations_WorkShiftTopDiscrepancies
    @FromDate date, @ToDate date, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) w.ShiftId AS WorkShiftId, w.StoreId, w.UserId AS StaffId,
           w.CashDiscrepancy, ABS(COALESCE(w.CashDiscrepancy,0)) AS AbsoluteDiscrepancy,
           w.DiscrepancyReason, w.EndTime, 'AVAILABLE' AS DataStatus
    FROM dbo.WorkShifts AS w INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = w.StoreId
    WHERE w.EndTime >= @FromDate AND w.EndTime < DATEADD(day,1,CONVERT(datetime2,@ToDate))
      AND w.CashDiscrepancy IS NOT NULL ORDER BY ABS(w.CashDiscrepancy) DESC, w.ShiftId DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Operations_WorkShiftKpis
    @FromDate date, @ToDate date, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT_BIG(w.ShiftId) AS TotalWorkShifts,
           SUM(CASE WHEN w.EndTime IS NULL THEN 1 ELSE 0 END) AS OpenWorkShifts,
           SUM(CASE WHEN w.IsExceptionClosed = 1 THEN 1 ELSE 0 END) AS ExceptionClosedCount,
           SUM(CASE WHEN w.RequiresReconciliation = 1 THEN 1 ELSE 0 END) AS ReconciliationCount,
           COALESCE(SUM(ABS(COALESCE(w.CashDiscrepancy,0))),0) AS AbsoluteCashDiscrepancy,
           CASE WHEN COUNT_BIG(w.ShiftId)=0 THEN 'NO_DATA' ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.WorkShifts AS w INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=w.StoreId
    WHERE w.StartTime >= @FromDate AND w.StartTime < DATEADD(day,1,CONVERT(datetime2,@ToDate));
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Product_TopProducts
    @FromDate date,@ToDate date,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) od.DrinkId,od.DrinkName,
           SUM(od.Quantity) AS TotalSold,
           SUM((od.Price-COALESCE(t.ToppingUnitPrice,0))*od.Quantity) AS ProductRevenue,
           SUM(CASE WHEN od.CostStatus=1 THEN od.TotalCogs ELSE 0 END) AS ConfirmedCogs,
           SUM(CASE WHEN od.CostStatus=1 THEN (od.Price-COALESCE(t.ToppingUnitPrice,0))*od.Quantity-COALESCE(od.TotalCogs,0) ELSE 0 END) AS ConfirmedGrossProfit,
           CASE WHEN SUM(CASE WHEN od.CostStatus<>1 THEN 1 ELSE 0 END)>0 THEN 'PARTIAL_COGS' ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.OrderDetails AS od INNER JOIN dbo.Orders AS o ON o.OrderId=od.OrderId AND o.OrderStatusId=5
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=o.StoreId
    OUTER APPLY(SELECT SUM(ot.Price) AS ToppingUnitPrice FROM dbo.OrderToppings AS ot WHERE ot.OrderDetailId=od.OrderDetailId) t
    WHERE o.CreatedAt>=@FromDate AND o.CreatedAt<DATEADD(day,1,CONVERT(datetime2,@ToDate))
    GROUP BY od.DrinkId,od.DrinkName ORDER BY ProductRevenue DESC,TotalSold DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Product_VolumeMarginMatrix
    @FromDate date,@ToDate date,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) od.DrinkId,od.DrinkName,SUM(od.Quantity) AS Volume,
           SUM((od.Price-COALESCE(t.ToppingUnitPrice,0))*od.Quantity) AS Revenue,
           SUM(CASE WHEN od.CostStatus=1 THEN od.TotalCogs ELSE 0 END) AS ConfirmedCogs,
           CONVERT(decimal(9,4),COALESCE(SUM(CASE WHEN od.CostStatus=1 THEN (od.Price-COALESCE(t.ToppingUnitPrice,0))*od.Quantity-COALESCE(od.TotalCogs,0) ELSE 0 END)
             /NULLIF(SUM(CASE WHEN od.CostStatus=1 THEN (od.Price-COALESCE(t.ToppingUnitPrice,0))*od.Quantity ELSE 0 END),0),0)) AS ConfirmedMarginRate,
           CASE WHEN SUM(CASE WHEN od.CostStatus<>1 THEN 1 ELSE 0 END)>0 THEN 'PARTIAL_COGS' ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.OrderDetails AS od INNER JOIN dbo.Orders AS o ON o.OrderId=od.OrderId AND o.OrderStatusId=5
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=o.StoreId
    OUTER APPLY(SELECT SUM(ot.Price) AS ToppingUnitPrice FROM dbo.OrderToppings AS ot WHERE ot.OrderDetailId=od.OrderDetailId) t
    WHERE o.CreatedAt>=@FromDate AND o.CreatedAt<DATEADD(day,1,CONVERT(datetime2,@ToDate))
    GROUP BY od.DrinkId,od.DrinkName ORDER BY Volume DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Product_SizeMargin
    @FromDate date,@ToDate date,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT od.SizeId,COALESCE(od.SizeName,'Không size') AS SizeName,SUM(od.Quantity) AS TotalSold,
           SUM((od.Price-COALESCE(t.ToppingUnitPrice,0))*od.Quantity) AS Revenue,
           SUM(CASE WHEN od.CostStatus=1 THEN od.TotalCogs ELSE 0 END) AS ConfirmedCogs,
           SUM(CASE WHEN od.CostStatus=1 THEN (od.Price-COALESCE(t.ToppingUnitPrice,0))*od.Quantity-COALESCE(od.TotalCogs,0) ELSE 0 END) AS ConfirmedGrossProfit,
           CASE WHEN SUM(CASE WHEN od.CostStatus<>1 THEN 1 ELSE 0 END)>0 THEN 'PARTIAL_COGS' ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.OrderDetails AS od INNER JOIN dbo.Orders AS o ON o.OrderId=od.OrderId AND o.OrderStatusId=5
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=o.StoreId
    OUTER APPLY(SELECT SUM(ot.Price) AS ToppingUnitPrice FROM dbo.OrderToppings AS ot WHERE ot.OrderDetailId=od.OrderDetailId) t
    WHERE o.CreatedAt>=@FromDate AND o.CreatedAt<DATEADD(day,1,CONVERT(datetime2,@ToDate))
    GROUP BY od.SizeId,od.SizeName ORDER BY Revenue DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Product_TopToppings
    @FromDate date,@ToDate date,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) ot.ToppingId,ot.ToppingName,
           SUM(od.Quantity) AS TotalUsed,SUM(ot.Price*od.Quantity) AS Revenue,
           SUM(CASE WHEN ot.CostStatus=1 THEN ot.TotalCogs ELSE 0 END) AS ConfirmedCogs,
           CASE WHEN SUM(CASE WHEN ot.CostStatus<>1 THEN 1 ELSE 0 END)>0 THEN 'PARTIAL_COGS' ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.OrderToppings AS ot INNER JOIN dbo.OrderDetails AS od ON od.OrderDetailId=ot.OrderDetailId
    INNER JOIN dbo.Orders AS o ON o.OrderId=od.OrderId AND o.OrderStatusId=5
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=o.StoreId
    WHERE o.CreatedAt>=@FromDate AND o.CreatedAt<DATEADD(day,1,CONVERT(datetime2,@ToDate))
    GROUP BY ot.ToppingId,ot.ToppingName ORDER BY Revenue DESC,TotalUsed DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Product_BomHealth
    @FromDate date,@ToDate date,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) d.DrinkId,d.DrinkCode,d.Name AS DrinkName,
           COUNT(DISTINCT r.RecipeId) AS RecipeCount,COUNT(rd.RecipeDetailId) AS RecipeLineCount,
           SUM(CASE WHEN rd.IngredientId IS NULL AND rd.ChildRecipeId IS NULL THEN 1 ELSE 0 END) AS InvalidLineCount,
           CASE WHEN COUNT(DISTINCT r.RecipeId)=0 THEN 'MISSING_BOM'
                WHEN SUM(CASE WHEN rd.IngredientId IS NULL AND rd.ChildRecipeId IS NULL THEN 1 ELSE 0 END)>0 THEN 'INVALID_BOM'
                ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.Drinks AS d LEFT JOIN dbo.Recipes AS r ON r.DrinkId=d.DrinkId AND r.Active=1
    LEFT JOIN dbo.RecipeDetails AS rd ON rd.RecipeId=r.RecipeId
    GROUP BY d.DrinkId,d.DrinkCode,d.Name ORDER BY InvalidLineCount DESC,RecipeCount,d.DrinkId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Product_HighConsumptionLowEfficiency
    @FromDate date,@ToDate date,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) od.DrinkId,od.DrinkName,SUM(od.Quantity) AS TotalSold,
           SUM(CASE WHEN od.CostStatus=1 THEN od.TotalCogs ELSE 0 END) AS ConfirmedCogs,
           SUM(CASE WHEN od.CostStatus=1 THEN od.Price*od.Quantity-COALESCE(od.TotalCogs,0) ELSE 0 END) AS ConfirmedGrossProfit,
           CASE WHEN SUM(CASE WHEN od.CostStatus<>1 THEN 1 ELSE 0 END)>0 THEN 'PARTIAL_COGS' ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.OrderDetails AS od INNER JOIN dbo.Orders AS o ON o.OrderId=od.OrderId AND o.OrderStatusId=5
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=o.StoreId
    WHERE o.CreatedAt>=@FromDate AND o.CreatedAt<DATEADD(day,1,CONVERT(datetime2,@ToDate))
    GROUP BY od.DrinkId,od.DrinkName
    ORDER BY ConfirmedCogs DESC,ConfirmedGrossProfit ASC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Workforce_ShiftStatus
    @FromDate date,@ToDate date,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ss.StaffShiftId,ss.StaffId,st.FullName,st.StoreId,ss.WorkDate,ss.ActualCheckIn,ss.ActualCheckOut,
           ss.PayrollHours,ss.StatusId,status.Code AS StatusCode,ss.IsAdHoc,
           'CURRENT_STAFF_STORE_SCOPE' AS DataStatus
    FROM dbo.StaffShifts AS ss INNER JOIN dbo.Staffs AS st ON st.StaffId=ss.StaffId
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=st.StoreId
    INNER JOIN dbo.StaffShiftStatuses AS status ON status.StaffShiftStatusId=ss.StatusId
    WHERE ss.WorkDate>=@FromDate AND ss.WorkDate<DATEADD(day,1,CONVERT(datetime2,@ToDate))
    ORDER BY ss.WorkDate DESC,ss.StaffShiftId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Workforce_HourlyDemand
    @FromDate date,@ToDate date,@StoreIds nvarchar(max),@Granularity varchar(10)='Hour',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    ;WITH Hours AS(SELECT 0 AS HourOfDay UNION ALL SELECT HourOfDay+1 FROM Hours WHERE HourOfDay<23),
    Demand AS
    (
        SELECT DATEPART(hour,o.CreatedAt) AS HourOfDay,COUNT_BIG(o.OrderId) AS TotalOrders,SUM(o.Total-o.ShippingFee) AS NetSales
        FROM dbo.Orders AS o INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=o.StoreId
        WHERE o.OrderStatusId=5 AND o.CreatedAt>=@FromDate AND o.CreatedAt<DATEADD(day,1,CONVERT(datetime2,@ToDate))
        GROUP BY DATEPART(hour,o.CreatedAt)
    ),Staffing AS
    (
        SELECT h.HourOfDay,COUNT_BIG(ss.StaffShiftId) AS StaffShiftCount
        FROM Hours AS h INNER JOIN dbo.StaffShifts AS ss ON ss.ActualCheckIn IS NOT NULL
          AND h.HourOfDay BETWEEN DATEPART(hour,ss.ActualCheckIn) AND DATEPART(hour,COALESCE(ss.ActualCheckOut,ss.ActualCheckIn))
        INNER JOIN dbo.Staffs AS st ON st.StaffId=ss.StaffId INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=st.StoreId
        WHERE ss.WorkDate>=@FromDate AND ss.WorkDate<DATEADD(day,1,CONVERT(datetime2,@ToDate)) GROUP BY h.HourOfDay
    )
    SELECT h.HourOfDay,COALESCE(d.TotalOrders,0) AS TotalOrders,COALESCE(d.NetSales,0) AS NetSales,
           COALESCE(s.StaffShiftCount,0) AS StaffShiftCount,
           CONVERT(decimal(19,2),COALESCE(d.TotalOrders/NULLIF(CONVERT(decimal(19,2),s.StaffShiftCount),0),0)) AS OrdersPerStaff,
           CASE WHEN d.TotalOrders IS NULL AND s.StaffShiftCount IS NULL THEN 'NO_DATA' ELSE 'CURRENT_STAFF_STORE_SCOPE' END AS DataStatus
    FROM Hours AS h LEFT JOIN Demand AS d ON d.HourOfDay=h.HourOfDay LEFT JOIN Staffing AS s ON s.HourOfDay=h.HourOfDay
    ORDER BY h.HourOfDay OPTION(MAXRECURSION 24);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Workforce_StaffPerformance
    @FromDate date,@ToDate date,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) st.StaffId,st.FullName,st.StoreId,
           COUNT_BIG(DISTINCT o.OrderId) AS TotalOrders,COALESCE(SUM(o.Total-o.ShippingFee),0) AS NetSales,
           COALESCE(hours.PayrollHours,0) AS PayrollHours,
           CONVERT(decimal(19,2),COALESCE(SUM(o.Total-o.ShippingFee)/NULLIF(hours.PayrollHours,0),0)) AS SalesPerPayrollHour,
           'CURRENT_STAFF_STORE_SCOPE' AS DataStatus
    FROM dbo.Staffs AS st INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=st.StoreId
    LEFT JOIN dbo.Orders AS o ON o.StaffId=st.StaffId AND o.OrderStatusId=5 AND o.CreatedAt>=@FromDate AND o.CreatedAt<DATEADD(day,1,CONVERT(datetime2,@ToDate))
    OUTER APPLY(SELECT SUM(ss.PayrollHours) AS PayrollHours FROM dbo.StaffShifts AS ss WHERE ss.StaffId=st.StaffId
      AND ss.WorkDate>=@FromDate AND ss.WorkDate<DATEADD(day,1,CONVERT(datetime2,@ToDate))) hours
    GROUP BY st.StaffId,st.FullName,st.StoreId,hours.PayrollHours
    ORDER BY NetSales DESC,st.StaffId;
END;
GO

/* Legacy compatibility contracts used by the current DashboardRepository. */
CREATE OR ALTER PROCEDURE dbo.sp_Revenue_By_Store
    @FromDate datetime,@ToDate datetime,@StoreIds nvarchar(max)=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT s.StoreId,s.Name,COUNT_BIG(o.OrderId) AS TotalOrders,
           COALESCE(SUM(o.Total-o.ShippingFee-CASE WHEN r.OrderRefundId IS NULL THEN 0 ELSE o.Total-o.ShippingFee END),0) AS Revenue
    FROM dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope INNER JOIN dbo.Stores AS s ON s.StoreId=scope.StoreId
    LEFT JOIN dbo.Orders AS o ON o.StoreId=s.StoreId AND o.OrderStatusId=5 AND o.CreatedAt>=@FromDate AND o.CreatedAt<DATEADD(day,1,CONVERT(date,@ToDate))
    LEFT JOIN dbo.OrderRefunds AS r ON r.OrderId=o.OrderId AND r.Status=3
    GROUP BY s.StoreId,s.Name ORDER BY Revenue DESC,s.StoreId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Revenue_Filtered
    @FromDate datetime,@ToDate datetime,@StoreIds nvarchar(max)=NULL,@ProvinceId int=NULL,@DistrictId int=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CONVERT(date,o.CreatedAt) AS [Date],COUNT_BIG(o.OrderId) AS TotalOrders,
           COALESCE(SUM(o.Total-o.ShippingFee-CASE WHEN r.OrderRefundId IS NULL THEN 0 ELSE o.Total-o.ShippingFee END),0) AS Revenue
    FROM dbo.Orders AS o INNER JOIN dbo.Stores AS s ON s.StoreId=o.StoreId
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=o.StoreId
    LEFT JOIN dbo.OrderRefunds AS r ON r.OrderId=o.OrderId AND r.Status=3
    WHERE o.OrderStatusId=5 AND o.CreatedAt>=@FromDate AND o.CreatedAt<DATEADD(day,1,CONVERT(date,@ToDate))
      AND (@ProvinceId IS NULL OR s.ProvinceId=@ProvinceId) AND (@DistrictId IS NULL OR s.DistrictId=@DistrictId)
    GROUP BY CONVERT(date,o.CreatedAt) ORDER BY [Date];
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Inventory_Summary @StoreId int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT i.IngredientId,i.Name,
           COALESCE(SUM(CASE WHEN it.Type IN(1,5,8,11,13,14,15) THEN ABS(it.Quantity) ELSE 0 END),0) AS TotalImport,
           COALESCE(SUM(CASE WHEN it.Type IN(2,6,7,9,10,12) THEN ABS(it.Quantity) ELSE 0 END),0) AS TotalExport,
           COALESCE(SUM(CASE WHEN it.Type=3 THEN ABS(it.Quantity) ELSE 0 END),0) AS TotalWaste,
           si.AvailableQty AS CurrentStock
    FROM dbo.StoreInventories AS si INNER JOIN dbo.Ingredients AS i ON i.IngredientId=si.IngredientId
    LEFT JOIN dbo.InventoryTransactions AS it ON it.StoreInventoryId=si.StoreInventoryId
    WHERE si.StoreId=@StoreId GROUP BY i.IngredientId,i.Name,si.AvailableQty ORDER BY i.Name;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Waste_Report
    @FromDate datetime,@ToDate datetime,@StoreIds nvarchar(max)=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT si.StoreId,s.Name AS StoreName,i.IngredientId,i.Name AS IngredientName,
           SUM(ABS(it.Quantity)) AS TotalWasteQty,COALESCE(SUM(ABS(it.TotalCost)),0) AS TotalWasteValue
    FROM dbo.InventoryTransactions AS it INNER JOIN dbo.StoreInventories AS si ON si.StoreInventoryId=it.StoreInventoryId
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=si.StoreId
    INNER JOIN dbo.Stores AS s ON s.StoreId=si.StoreId INNER JOIN dbo.Ingredients AS i ON i.IngredientId=si.IngredientId
    WHERE it.Type=3 AND it.CreatedAt>=@FromDate AND it.CreatedAt<DATEADD(day,1,CONVERT(date,@ToDate))
    GROUP BY si.StoreId,s.Name,i.IngredientId,i.Name ORDER BY TotalWasteQty DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Cash_Flow_Today @StoreIds nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT cs.CashSessionId,cs.StaffId,cs.OpenTime,cs.CloseTime,cs.StartCash,
           COALESCE(SUM(CASE WHEN pm.Code='CASH' THEN p.Amount ELSE 0 END),0) AS CashIn,
           COALESCE(SUM(CASE WHEN pm.Code<>'CASH' THEN p.Amount ELSE 0 END),0) AS NonCashIn,
           COALESCE(SUM(p.Amount),0) AS TotalRevenue
    FROM dbo.CashSessions AS cs INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=cs.StoreId
    LEFT JOIN dbo.Payments AS p ON p.CashSessionId=cs.CashSessionId AND p.PaymentStatusId=2
    LEFT JOIN dbo.PaymentMethods AS pm ON pm.PaymentMethodId=p.PaymentMethodId
    WHERE cs.OpenTime>=CONVERT(date,GETDATE()) AND cs.OpenTime<DATEADD(day,1,CONVERT(date,GETDATE()))
    GROUP BY cs.CashSessionId,cs.StaffId,cs.OpenTime,cs.CloseTime,cs.StartCash ORDER BY cs.OpenTime DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Top_Selling_Drinks_Filtered
    @Top int=10,@FromDate datetime,@ToDate datetime,@StoreIds nvarchar(max)=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) od.DrinkId,od.DrinkName,SUM(od.Quantity) AS TotalSold,
           SUM((od.Price-COALESCE(t.ToppingUnitPrice,0))*od.Quantity) AS Revenue
    FROM dbo.OrderDetails AS od INNER JOIN dbo.Orders AS o ON o.OrderId=od.OrderId AND o.OrderStatusId=5
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=o.StoreId
    OUTER APPLY(SELECT SUM(ot.Price) AS ToppingUnitPrice FROM dbo.OrderToppings AS ot WHERE ot.OrderDetailId=od.OrderDetailId) t
    WHERE o.CreatedAt>=@FromDate AND o.CreatedAt<DATEADD(day,1,CONVERT(date,@ToDate))
    GROUP BY od.DrinkId,od.DrinkName ORDER BY TotalSold DESC,Revenue DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Top_Toppings_Filtered
    @FromDate datetime,@ToDate datetime,@StoreIds nvarchar(max)=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ot.ToppingId,ot.ToppingName,SUM(od.Quantity) AS TotalUsed,SUM(ot.Price*od.Quantity) AS Revenue
    FROM dbo.OrderToppings AS ot INNER JOIN dbo.OrderDetails AS od ON od.OrderDetailId=ot.OrderDetailId
    INNER JOIN dbo.Orders AS o ON o.OrderId=od.OrderId AND o.OrderStatusId=5
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=o.StoreId
    WHERE o.CreatedAt>=@FromDate AND o.CreatedAt<DATEADD(day,1,CONVERT(date,@ToDate))
    GROUP BY ot.ToppingId,ot.ToppingName ORDER BY TotalUsed DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Top_Customers @Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) c.CustomerId,c.FullName,COUNT_BIG(o.OrderId) AS TotalOrders,
           SUM(o.Total-o.ShippingFee) AS TotalSpent
    FROM dbo.Orders AS o INNER JOIN dbo.Customers AS c ON c.CustomerId=o.CustomerId
    WHERE o.OrderStatusId=5 GROUP BY c.CustomerId,c.FullName ORDER BY TotalSpent DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Revenue_By_PaymentMethod_Filtered
    @FromDate datetime,@ToDate datetime,@StoreIds nvarchar(max)=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT pm.Name,COUNT_BIG(p.PaymentId) AS TotalTransactions,COALESCE(SUM(p.Amount),0) AS Revenue
    FROM dbo.Payments AS p INNER JOIN dbo.PaymentMethods AS pm ON pm.PaymentMethodId=p.PaymentMethodId
    INNER JOIN dbo.Orders AS o ON o.OrderId=p.OrderId AND o.OrderStatusId=5
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=o.StoreId
    WHERE p.PaymentStatusId=2 AND o.CreatedAt>=@FromDate AND o.CreatedAt<DATEADD(day,1,CONVERT(date,@ToDate))
    GROUP BY pm.Name ORDER BY Revenue DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Order_Status_Stats
AS
BEGIN
    SET NOCOUNT ON;
    SELECT os.Name,COUNT_BIG(o.OrderId) AS TotalOrders FROM dbo.OrderStatuses AS os
    LEFT JOIN dbo.Orders AS o ON o.OrderStatusId=os.OrderStatusId GROUP BY os.Name ORDER BY os.Name;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Revenue_By_Hour
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DATEPART(hour,o.CreatedAt) AS HourOfDay,COUNT_BIG(o.OrderId) AS TotalOrders,
           SUM(o.Total-o.ShippingFee) AS Revenue FROM dbo.Orders AS o WHERE o.OrderStatusId=5
    GROUP BY DATEPART(hour,o.CreatedAt) ORDER BY HourOfDay;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Staff_Performance_Filtered
    @FromDate datetime,@ToDate datetime,@StoreIds nvarchar(max)=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT st.StaffId,st.FullName,COUNT_BIG(o.OrderId) AS TotalOrders,
           COALESCE(SUM(o.Total-o.ShippingFee),0) AS Revenue
    FROM dbo.Staffs AS st INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=st.StoreId
    LEFT JOIN dbo.Orders AS o ON o.StaffId=st.StaffId AND o.OrderStatusId=5
      AND o.CreatedAt>=@FromDate AND o.CreatedAt<DATEADD(day,1,CONVERT(date,@ToDate))
    GROUP BY st.StaffId,st.FullName ORDER BY Revenue DESC,TotalOrders DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Dashboard_Summary_Filtered
    @FromDate datetime,@ToDate datetime,@StoreIds nvarchar(max)=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT_BIG(o.OrderId) AS TotalOrders,
           COALESCE(SUM(o.Total-o.ShippingFee-CASE WHEN r.OrderRefundId IS NULL THEN 0 ELSE o.Total-o.ShippingFee END),0) AS Revenue,
           COUNT(DISTINCT o.CustomerId) AS TotalCustomers,
           SUM(CASE WHEN o.CreatedAt>=CONVERT(date,GETDATE()) AND o.CreatedAt<DATEADD(day,1,CONVERT(date,GETDATE())) THEN 1 ELSE 0 END) AS TodayOrders
    FROM dbo.Orders AS o INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=o.StoreId
    LEFT JOIN dbo.OrderRefunds AS r ON r.OrderId=o.OrderId AND r.Status=3
    WHERE o.OrderStatusId=5 AND o.CreatedAt>=@FromDate AND o.CreatedAt<DATEADD(day,1,CONVERT(date,@ToDate));
END;
GO
