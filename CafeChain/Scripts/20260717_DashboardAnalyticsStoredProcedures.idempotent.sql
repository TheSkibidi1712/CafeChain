use master
go

use CafeChain
go

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

CREATE OR ALTER FUNCTION dbo.ufn_AnalyticsBucketStart(
    @Value datetime2,
    @Granularity varchar(10))
RETURNS datetime2
AS
BEGIN
    DECLARE @Mode varchar(10)=UPPER(LTRIM(RTRIM(COALESCE(@Granularity,'DAY'))));
    RETURN CASE @Mode
        WHEN 'HOUR' THEN DATEADD(hour,DATEDIFF(hour,CONVERT(datetime2,'19000101'),@Value),CONVERT(datetime2,'19000101'))
        WHEN 'WEEK' THEN DATEADD(day,-(DATEDIFF(day,CONVERT(date,'19000101'),CONVERT(date,@Value)) % 7),CONVERT(datetime2,CONVERT(date,@Value)))
        WHEN 'MONTH' THEN CONVERT(datetime2,DATEFROMPARTS(YEAR(@Value),MONTH(@Value),1))
        ELSE CONVERT(datetime2,CONVERT(date,@Value))
    END;
END;
GO

CREATE OR ALTER FUNCTION dbo.ufn_AnalyticsNextBucket(
    @BucketStart datetime2,
    @Granularity varchar(10))
RETURNS datetime2
AS
BEGIN
    DECLARE @Mode varchar(10)=UPPER(LTRIM(RTRIM(COALESCE(@Granularity,'DAY'))));
    RETURN CASE @Mode
        WHEN 'HOUR' THEN DATEADD(hour,1,@BucketStart)
        WHEN 'WEEK' THEN DATEADD(day,7,@BucketStart)
        WHEN 'MONTH' THEN DATEADD(month,1,@BucketStart)
        ELSE DATEADD(day,1,@BucketStart)
    END;
END;
GO

CREATE OR ALTER FUNCTION dbo.ufn_AnalyticsOrderFacts(
    @FromDate datetime2,
    @ToExclusive datetime2)
RETURNS TABLE
AS
RETURN
(
    SELECT o.OrderId, o.StoreId, o.StaffId, o.WorkShiftId, o.CreatedAt,
           CONVERT(decimal(19,2), CASE WHEN o.Total-o.ShippingFee < 0 THEN 0 ELSE o.Total-o.ShippingFee END) AS GrossSales,
           CONVERT(decimal(19,2), CASE
               WHEN COALESCE(refund.CompletedRefundAmount,0) > o.Total-o.ShippingFee THEN
                   CASE WHEN o.Total-o.ShippingFee < 0 THEN 0 ELSE o.Total-o.ShippingFee END
               ELSE COALESCE(refund.CompletedRefundAmount,0)
           END) AS CompletedRefundAmount,
           CONVERT(decimal(19,2), CASE
               WHEN o.Total-o.ShippingFee-COALESCE(refund.CompletedRefundAmount,0) < 0 THEN 0
               ELSE o.Total-o.ShippingFee-COALESCE(refund.CompletedRefundAmount,0)
           END) AS NetSales,
           CONVERT(bigint, CASE WHEN COALESCE(refund.CompletedRefundAmount,0) >= o.Total-o.ShippingFee THEN 0 ELSE 1 END) AS CountedOrder
    FROM dbo.Orders AS o
    OUTER APPLY
    (
        SELECT SUM(r.RefundAmount) AS CompletedRefundAmount
        FROM dbo.OrderRefunds AS r
        WHERE r.OrderId=o.OrderId AND r.Status=3
    ) AS refund
    WHERE o.OrderStatusId=5 AND o.CreatedAt>=@FromDate AND o.CreatedAt<@ToExclusive
);
GO

CREATE OR ALTER PROCEDURE dbo.usp_Dashboard_NetSalesTrend
    @FromDate datetime2, @ToDate datetime2, @StoreIds nvarchar(max),
    @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    IF @FromDate IS NULL OR @ToDate IS NULL OR @FromDate > @ToDate
    BEGIN
        ;THROW 50001, 'Invalid date range.', 1;
    END;
    IF @FromDate = @ToDate
        RETURN;
    IF DATEDIFF(day,@FromDate,@ToDate)>3660
    BEGIN
        ;THROW 50003, 'Date range cannot exceed 3660 days.', 1;
    END;
    SET @Granularity=UPPER(LTRIM(RTRIM(COALESCE(@Granularity,'DAY'))));
    IF @Granularity NOT IN ('HOUR','DAY','WEEK','MONTH')
    BEGIN
        ;THROW 50002, 'Invalid granularity.', 1;
    END;
    DECLARE @ToExclusive datetime2 = @ToDate;
    DECLARE @FirstBucket datetime2=dbo.ufn_AnalyticsBucketStart(CONVERT(datetime2,@FromDate),@Granularity);
    DECLARE @LastBucket datetime2=dbo.ufn_AnalyticsBucketStart(DATEADD(second,-1,@ToExclusive),@Granularity);
    ;WITH Buckets AS
    (
        SELECT @FirstBucket AS BucketDate
        UNION ALL
        SELECT dbo.ufn_AnalyticsNextBucket(BucketDate,@Granularity)
        FROM Buckets
        WHERE BucketDate < @LastBucket
    ), Events AS
    (
        SELECT dbo.ufn_AnalyticsBucketStart(f.CreatedAt,@Granularity) AS EventDate,
               f.CountedOrder AS OrderCount,f.NetSales
        FROM dbo.ufn_AnalyticsOrderFacts(@FromDate,@ToExclusive) AS f
        INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=f.StoreId
    )
    SELECT d.BucketDate, COALESCE(SUM(e.OrderCount), 0) AS TotalOrders,
           COALESCE(SUM(e.NetSales), 0) AS NetSales,
           CASE WHEN SUM(CASE WHEN e.EventDate IS NOT NULL THEN 1 ELSE 0 END) = 0 THEN 'NO_DATA' ELSE 'AVAILABLE' END AS DataStatus
    FROM Buckets AS d
    LEFT JOIN Events AS e ON e.EventDate = d.BucketDate
    GROUP BY d.BucketDate
    ORDER BY d.BucketDate
    OPTION (MAXRECURSION 0);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Inventory_ShortageRisk
    @FromDate datetime2, @ToDate datetime2, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) si.StoreInventoryId, si.StoreId, s.Name AS StoreName,
           si.IngredientId, i.Code AS IngredientCode, i.Name AS IngredientName, u.UnitCode AS Unit,
           si.AvailableQty AS OnHandQuantity, si.ReservedQty AS ReservedQuantity,
           si.AvailableQty-si.ReservedQty AS AvailableQuantity, si.MinStockLevel AS MinimumStock,
           CASE WHEN si.MinStockLevel IS NULL THEN 0
                WHEN si.MinStockLevel>(si.AvailableQty-si.ReservedQty)
                THEN si.MinStockLevel-(si.AvailableQty-si.ReservedQty) ELSE 0 END AS ShortageQuantity,
           CASE WHEN si.MinStockLevel IS NULL THEN 0
                WHEN si.MinStockLevel>(si.AvailableQty-si.ReservedQty)
                THEN si.MinStockLevel-(si.AvailableQty-si.ReservedQty) ELSE 0 END AS SuggestedReorderQuantity,
           CASE WHEN si.AvailableQty-si.ReservedQty < 0 THEN 'CRITICAL' WHEN si.MinStockLevel IS NULL THEN 'UNCONFIGURED'
                WHEN si.AvailableQty-si.ReservedQty <= si.MinStockLevel THEN 'HIGH' ELSE 'NORMAL' END AS RiskLevel,
           CASE WHEN si.MinStockLevel IS NULL THEN 'THRESHOLD_NOT_CONFIGURED' ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.StoreInventories AS si
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=si.StoreId
    INNER JOIN dbo.Stores AS s ON s.StoreId=si.StoreId
    LEFT JOIN dbo.Ingredients AS i ON i.IngredientId=si.IngredientId
    LEFT JOIN dbo.Units AS u ON u.UnitId=i.BaseUnitId
    WHERE si.IngredientId IS NOT NULL
    ORDER BY CASE WHEN si.AvailableQty-si.ReservedQty < 0 THEN 0 WHEN si.MinStockLevel IS NULL THEN 2 ELSE 1 END,
             (si.AvailableQty-si.ReservedQty)-COALESCE(si.MinStockLevel,0), si.StoreInventoryId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Inventory_MovementByType
    @FromDate datetime2, @ToDate datetime2, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SET @Granularity=UPPER(LTRIM(RTRIM(COALESCE(@Granularity,'DAY'))));
    IF @Granularity NOT IN ('HOUR','DAY','WEEK','MONTH')
    BEGIN
        ;THROW 50002, 'Invalid granularity.', 1;
    END;
    SELECT dbo.ufn_AnalyticsBucketStart(it.CreatedAt,@Granularity) AS MovementDate, it.Type AS TransactionType,
           COUNT_BIG(it.InventoryTransactionId) AS TransactionCount,
           SUM(it.Quantity) AS Quantity, COALESCE(SUM(it.TotalCost),0) AS TotalCost, 'AVAILABLE' AS DataStatus
    FROM dbo.InventoryTransactions AS it
    INNER JOIN dbo.StoreInventories AS si ON si.StoreInventoryId=it.StoreInventoryId
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=si.StoreId
    WHERE it.CreatedAt>=@FromDate AND it.CreatedAt<@ToDate
    GROUP BY dbo.ufn_AnalyticsBucketStart(it.CreatedAt,@Granularity),it.Type ORDER BY MovementDate,it.Type;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Inventory_ThresholdRisk
    @FromDate datetime2, @ToDate datetime2, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
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

CREATE OR ALTER PROCEDURE dbo.usp_Inventory_WasteByStoreIngredient
    @FromDate datetime2, @ToDate datetime2, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) si.StoreId,s.Name AS StoreName,si.IngredientId,i.Name AS IngredientName,
           SUM(ABS(it.Quantity)) AS WasteQuantity,COALESCE(SUM(ABS(it.TotalCost)),0) AS WasteValue,
           COUNT_BIG(it.InventoryTransactionId) AS TransactionCount,'AVAILABLE' AS DataStatus
    FROM dbo.InventoryTransactions AS it INNER JOIN dbo.StoreInventories AS si ON si.StoreInventoryId=it.StoreInventoryId
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=si.StoreId
    INNER JOIN dbo.Stores AS s ON s.StoreId=si.StoreId LEFT JOIN dbo.Ingredients AS i ON i.IngredientId=si.IngredientId
    WHERE it.Type=3 AND it.CreatedAt>=@FromDate AND it.CreatedAt<@ToDate
    GROUP BY si.StoreId,s.Name,si.IngredientId,i.Name ORDER BY WasteValue DESC,WasteQuantity DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Inventory_FifoLayerAge
    @FromDate datetime2, @ToDate datetime2, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
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
    @FromDate datetime2,@ToDate datetime2,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT po.Status,COUNT_BIG(po.PurchaseOrderId) AS PurchaseOrderCount,
           COALESCE(SUM(line.OrderValue),0) AS OrderedValue,'AVAILABLE' AS DataStatus
    FROM dbo.PurchaseOrders AS po INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=po.StoreId
    OUTER APPLY(SELECT SUM(pol.PackagePriceSnapshot*pol.PackageCount) AS OrderValue FROM dbo.PurchaseOrderLines AS pol WHERE pol.PurchaseOrderId=po.PurchaseOrderId) line
    WHERE po.OrderDate>=@FromDate AND po.OrderDate<@ToDate
    GROUP BY po.Status ORDER BY PurchaseOrderCount DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Procurement_OverduePurchaseOrders
    @FromDate datetime2,@ToDate datetime2,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) po.PurchaseOrderId,po.Code,po.StoreId,st.Name AS StoreName,po.SupplierId,s.Name AS SupplierName,
           po.Status,po.OrderDate,po.ExpectedDeliveryAtUtc,DATEDIFF(day,po.ExpectedDeliveryAtUtc,SYSUTCDATETIME()) AS OverdueDays,
           COALESCE(line.OrderedValue,0) AS OrderedValue,
           'OVERDUE' AS DataStatus
    FROM dbo.PurchaseOrders AS po INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=po.StoreId
    INNER JOIN dbo.Suppliers AS s ON s.SupplierId=po.SupplierId
    INNER JOIN dbo.Stores AS st ON st.StoreId=po.StoreId
    OUTER APPLY
    (
        SELECT SUM(pol.PackagePriceSnapshot*pol.PackageCount) AS OrderedValue
        FROM dbo.PurchaseOrderLines AS pol
        WHERE pol.PurchaseOrderId=po.PurchaseOrderId
    ) AS line
    WHERE po.ExpectedDeliveryAtUtc<SYSUTCDATETIME() AND po.Status NOT IN('COMPLETED','CANCELLED')
    ORDER BY OverdueDays DESC,po.PurchaseOrderId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Procurement_SupplierQuality
    @FromDate datetime2,@ToDate datetime2,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
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
    WHERE br.Status='CONFIRMED' AND br.ReceivedAt>=@FromDate AND br.ReceivedAt<@ToDate
    GROUP BY br.SupplierId,s.Name ORDER BY RejectionRate DESC,RejectedBaseQuantity DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Procurement_PurchasePriceTrend
    @FromDate datetime2,@ToDate datetime2,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SET @Granularity=UPPER(LTRIM(RTRIM(COALESCE(@Granularity,'DAY'))));
    IF @Granularity NOT IN ('HOUR','DAY','WEEK','MONTH')
    BEGIN
        ;THROW 50002, 'Invalid granularity.', 1;
    END;
    SELECT dbo.ufn_AnalyticsBucketStart(br.ReceivedAt,@Granularity) AS ReceiptDate,brl.IngredientId,i.Name AS IngredientName,
           AVG(brl.BaseUnitCostSnapshot) AS AverageBaseUnitCost,MIN(brl.BaseUnitCostSnapshot) AS MinimumBaseUnitCost,
           MAX(brl.BaseUnitCostSnapshot) AS MaximumBaseUnitCost,SUM(brl.ReceivedBaseQuantity) AS ReceivedBaseQuantity,'AVAILABLE' AS DataStatus
    FROM dbo.BranchReceipts AS br INNER JOIN dbo.BranchReceiptLines AS brl ON brl.BranchReceiptId=br.BranchReceiptId
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=br.StoreId LEFT JOIN dbo.Ingredients AS i ON i.IngredientId=brl.IngredientId
    WHERE br.Status='CONFIRMED' AND brl.IngredientId IS NOT NULL AND br.ReceivedAt>=@FromDate AND br.ReceivedAt<@ToDate
    GROUP BY dbo.ufn_AnalyticsBucketStart(br.ReceivedAt,@Granularity),brl.IngredientId,i.Name ORDER BY ReceiptDate,brl.IngredientId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Procurement_SpendBreakdown
    @FromDate datetime2,@ToDate datetime2,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) br.SupplierId,s.Name AS SupplierName,br.StoreId,
           SUM(brl.LineTotalCost) AS Spend,COUNT_BIG(DISTINCT br.BranchReceiptId) AS ReceiptCount,'AVAILABLE' AS DataStatus
    FROM dbo.BranchReceipts AS br INNER JOIN dbo.BranchReceiptLines AS brl ON brl.BranchReceiptId=br.BranchReceiptId
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=br.StoreId LEFT JOIN dbo.Suppliers AS s ON s.SupplierId=br.SupplierId
    WHERE br.Status='CONFIRMED' AND br.ReceivedAt>=@FromDate AND br.ReceivedAt<@ToDate
    GROUP BY br.SupplierId,s.Name,br.StoreId ORDER BY Spend DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Procurement_SupplierIssueMix
    @FromDate datetime2,@ToDate datetime2,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT issue.SupplierId,s.Name AS SupplierName,issue.StoreId,st.Name AS StoreName,
           issue.IssueType,issue.Status,COUNT_BIG(issue.SupplierReceiptIssueId) AS IssueCount,
           SUM(issue.AffectedBaseQuantity) AS AffectedBaseQuantity,'AVAILABLE' AS DataStatus
    FROM dbo.SupplierReceiptIssues AS issue INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=issue.StoreId
    INNER JOIN dbo.Suppliers AS s ON s.SupplierId=issue.SupplierId
    INNER JOIN dbo.Stores AS st ON st.StoreId=issue.StoreId
    WHERE issue.ReportedAtUtc>=@FromDate AND issue.ReportedAtUtc<@ToDate
    GROUP BY issue.SupplierId,s.Name,issue.StoreId,st.Name,issue.IssueType,issue.Status
    ORDER BY IssueCount DESC,issue.IssueType;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Dashboard_StoreRanking
    @FromDate datetime2, @ToDate datetime2, @StoreIds nvarchar(max),
    @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ToExclusive datetime2 = @ToDate;
    SELECT TOP (ISNULL(NULLIF(@Top, 0), 10)) s.StoreId, s.Name AS StoreName,
           COALESCE(SUM(f.CountedOrder),0) AS TotalOrders,
           COALESCE(SUM(f.NetSales),0) AS NetSales,
           CONVERT(decimal(19,2),COALESCE(SUM(f.NetSales)/NULLIF(SUM(f.CountedOrder),0),0)) AS AverageOrderValue,
           CONVERT(int,DENSE_RANK() OVER (ORDER BY COALESCE(SUM(f.NetSales),0) DESC)) AS [Rank],
           CONVERT(decimal(9,4),COALESCE(
               COALESCE(SUM(f.NetSales),0) / NULLIF(SUM(COALESCE(SUM(f.NetSales),0)) OVER (),0) * 100,0)) AS ContributionPercent,
           CASE WHEN COALESCE(SUM(f.CountedOrder),0) = 0 THEN 'NO_DATA' ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope
    INNER JOIN dbo.Stores AS s ON s.StoreId = scope.StoreId
    LEFT JOIN dbo.ufn_AnalyticsOrderFacts(@FromDate,@ToExclusive) AS f ON f.StoreId=s.StoreId
    GROUP BY s.StoreId, s.Name
    ORDER BY NetSales DESC, s.StoreId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Dashboard_PaymentMethodMix
    @FromDate datetime2, @ToDate datetime2, @StoreIds nvarchar(max),
    @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ToExclusive datetime2 = @ToDate;
    ;WITH PaymentEvents AS
    (
        SELECT p.PaymentMethodId,CONVERT(bigint,CASE WHEN f.CountedOrder=1 THEN 1 ELSE 0 END) AS TransactionCount,
               CONVERT(decimal(19,2),p.Amount) AS Amount
        FROM dbo.Payments AS p
        INNER JOIN dbo.ufn_AnalyticsOrderFacts(@FromDate,@ToExclusive) AS f ON f.OrderId=p.OrderId
        INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=f.StoreId
        WHERE p.PaymentStatusId=2
        UNION ALL
        SELECT r.PaymentMethodId,CONVERT(bigint,0),-f.CompletedRefundAmount
        FROM dbo.OrderRefunds AS r
        INNER JOIN dbo.ufn_AnalyticsOrderFacts(@FromDate,@ToExclusive) AS f ON f.OrderId=r.OrderId
        INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=f.StoreId
        WHERE r.Status=3
    ), Totals AS
    (
        SELECT PaymentMethodId,SUM(TransactionCount) AS TotalTransactions,SUM(Amount) AS Amount
        FROM PaymentEvents GROUP BY PaymentMethodId
    )
    SELECT pm.PaymentMethodId, pm.Code AS PaymentMethodCode, pm.Name AS PaymentMethodName,
           t.TotalTransactions,t.Amount,
           CONVERT(decimal(9,4), COALESCE(t.TotalTransactions * 1.0 / NULLIF(SUM(t.TotalTransactions) OVER (), 0) * 100, 0)) AS TransactionShare,
           CONVERT(decimal(9,4), COALESCE(t.Amount / NULLIF(SUM(t.Amount) OVER (), 0) * 100, 0)) AS RevenueShare,
           'AVAILABLE' AS DataStatus
    FROM Totals AS t INNER JOIN dbo.PaymentMethods AS pm ON pm.PaymentMethodId=t.PaymentMethodId
    WHERE t.Amount<>0 OR t.TotalTransactions<>0
    ORDER BY TotalTransactions DESC, Amount DESC, pm.PaymentMethodId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Dashboard_OrderHeatmap
    @FromDate datetime2, @ToDate datetime2, @StoreIds nvarchar(max),
    @Granularity varchar(10) = 'Hour', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ToExclusive datetime2 = @ToDate;
    ;WITH WeekDays AS (SELECT value AS IsoWeekday FROM (VALUES(1),(2),(3),(4),(5),(6),(7)) d(value)),
    Hours AS (SELECT 0 AS HourOfDay UNION ALL SELECT HourOfDay + 1 FROM Hours WHERE HourOfDay < 23),
    Actual AS
    (
        SELECT 1 + (DATEDIFF(day, '19000101', CONVERT(date, f.CreatedAt)) % 7) AS IsoWeekday,
               DATEPART(hour, f.CreatedAt) AS HourOfDay, SUM(f.CountedOrder) AS TotalOrders,
               SUM(f.NetSales) AS NetSales
        FROM dbo.ufn_AnalyticsOrderFacts(@FromDate,@ToExclusive) AS f
        INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=f.StoreId
        GROUP BY 1 + (DATEDIFF(day, '19000101', CONVERT(date, f.CreatedAt)) % 7), DATEPART(hour, f.CreatedAt)
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
    @FromDate datetime2, @ToDate datetime2, @StoreIds nvarchar(max),
    @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top, 0), 10)) alert.AlertType, alert.StoreId, alert.StoreName,
           alert.EntityType, alert.EntityId, alert.EntityCode, alert.EntityName,
           alert.Severity, alert.AlertValue, alert.Unit, alert.Message, alert.DataStatus
    FROM
    (
        SELECT 'LOW_STOCK' AS AlertType, si.StoreId, s.Name AS StoreName,
               'INGREDIENT' AS EntityType, i.IngredientId AS EntityId, i.Code AS EntityCode, i.Name AS EntityName,
                CASE WHEN si.AvailableQty-si.ReservedQty < 0 THEN 'CRITICAL' ELSE 'WARNING' END AS Severity,
               si.AvailableQty-si.ReservedQty AS AlertValue, u.UnitCode AS Unit,
               CONCAT(N'Tồn dưới ngưỡng: ', i.Name) AS Message, 'AVAILABLE' AS DataStatus
        FROM dbo.StoreInventories AS si
        INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = si.StoreId
        INNER JOIN dbo.Stores AS s ON s.StoreId=si.StoreId
        LEFT JOIN dbo.Ingredients AS i ON i.IngredientId = si.IngredientId
        LEFT JOIN dbo.Units AS u ON u.UnitId=i.BaseUnitId
        WHERE si.MinStockLevel IS NOT NULL AND si.AvailableQty-si.ReservedQty <= si.MinStockLevel
        UNION ALL
        SELECT 'CASH_DISCREPANCY', w.StoreId, s.Name, 'WORK_SHIFT', w.ShiftId,
               CONVERT(nvarchar(50),w.ShiftId), CONCAT('WorkShift #',w.ShiftId),
               CASE WHEN ABS(COALESCE(w.CashDiscrepancy, 0)) >= 50000 THEN 'CRITICAL' ELSE 'WARNING' END,
               COALESCE(w.CashDiscrepancy, 0), 'VND', CONCAT(N'Chênh lệch WorkShift #', w.ShiftId), 'AVAILABLE'
        FROM dbo.WorkShifts AS w
        INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = w.StoreId
        INNER JOIN dbo.Stores AS s ON s.StoreId=w.StoreId
        WHERE w.EndTimeUtc >= @FromDate AND w.EndTimeUtc < @ToDate
          AND ABS(COALESCE(w.CashDiscrepancy, 0)) > 0
        UNION ALL
        SELECT 'OVERDUE_PO', po.StoreId, st.Name, 'PURCHASE_ORDER', po.PurchaseOrderId,
               po.Code,po.Code,'WARNING',DATEDIFF(day, po.ExpectedDeliveryAtUtc, SYSUTCDATETIME()),
               'DAY',CONCAT(N'PO quá hạn: ', po.Code), 'AVAILABLE'
        FROM dbo.PurchaseOrders AS po
        INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = po.StoreId
        INNER JOIN dbo.Stores AS st ON st.StoreId=po.StoreId
        WHERE po.ExpectedDeliveryAtUtc < SYSUTCDATETIME() AND po.Status NOT IN ('COMPLETED', 'CANCELLED')
        UNION ALL
        SELECT 'SUPPLIER_ISSUE', issue.StoreId,st.Name,'SUPPLIER',issue.SupplierId,
               CONVERT(nvarchar(50),issue.SupplierId),s.Name,
               CASE WHEN issue.Status='OPEN' THEN 'WARNING' ELSE 'INFO' END,
               issue.AffectedBaseQuantity,COALESCE(u.UnitCode,N''),
               CONCAT(
                   N'Sự cố nhà cung cấp: ',s.Name,N' - ',
                   CASE issue.IssueType
                       WHEN 'LATE_DELIVERY' THEN N'giao hàng trễ'
                       WHEN 'SHORT_DELIVERY' THEN N'giao thiếu hàng'
                       WHEN 'WRONG_ITEM' THEN N'giao sai mặt hàng'
                       WHEN 'DAMAGED' THEN N'hàng hóa hư hỏng'
                       WHEN 'EXPIRED' THEN N'hàng hóa hết hạn'
                       WHEN 'QUALITY_FAILURE' THEN N'không đạt chất lượng'
                       WHEN 'PACKAGING_FAILURE' THEN N'sự cố bao bì'
                       WHEN 'DOCUMENT_MISMATCH' THEN N'sai lệch chứng từ'
                       ELSE COALESCE(NULLIF(issue.Description,N''),N'sự cố khác')
                   END),'AVAILABLE'
        FROM dbo.SupplierReceiptIssues AS issue
        INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=issue.StoreId
        INNER JOIN dbo.Stores AS st ON st.StoreId=issue.StoreId
        INNER JOIN dbo.Suppliers AS s ON s.SupplierId=issue.SupplierId
        LEFT JOIN dbo.BranchReceiptLines AS brl ON brl.BranchReceiptLineId=issue.BranchReceiptLineId
        LEFT JOIN dbo.Units AS u ON u.UnitId=brl.BaseUnitId
        WHERE issue.ReportedAtUtc>=@FromDate AND issue.ReportedAtUtc<@ToDate
    ) AS alert
    ORDER BY CASE alert.Severity WHEN 'CRITICAL' THEN 0 WHEN 'WARNING' THEN 1 ELSE 2 END,
             CASE alert.AlertType
                 WHEN 'CASH_DISCREPANCY' THEN 0
                 WHEN 'LOW_STOCK' THEN 1
                 WHEN 'OVERDUE_PO' THEN 2
                 WHEN 'SUPPLIER_ISSUE' THEN 3
                 ELSE 4
             END,
             ABS(alert.AlertValue) DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Operations_WorkShiftCashDiscrepancy
    @FromDate datetime2, @ToDate datetime2, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT w.ShiftId AS WorkShiftId, w.StoreId, s.Name AS StoreName, w.UserId AS StaffId, st.FullName,
           w.StartTimeUtc AS StartTime, w.EndTimeUtc AS EndTime, w.StartingCash, w.ExpectedEndingCash, w.ActualEndingCash,
           w.CashDiscrepancy, w.DiscrepancyReason, w.IsExceptionClosed, w.RequiresReconciliation,
           CASE WHEN w.EndTimeUtc IS NULL THEN 'OPEN' ELSE 'CLOSED' END AS DataStatus
    FROM dbo.WorkShifts AS w
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = w.StoreId
    INNER JOIN dbo.Stores AS s ON s.StoreId = w.StoreId
    INNER JOIN dbo.Staffs AS st ON st.StaffId = w.UserId
    WHERE w.StartTimeUtc < @ToDate
      AND COALESCE(w.EndTimeUtc, @ToDate) >= @FromDate
    ORDER BY w.StartTimeUtc DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Operations_WorkShiftSales
    @FromDate datetime2, @ToDate datetime2, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT w.ShiftId AS WorkShiftId, w.StoreId, COALESCE(SUM(f.CountedOrder),0) AS TotalOrders,
           COALESCE(SUM(f.NetSales),0) AS NetSales,
           CONVERT(decimal(19,2),COALESCE(SUM(f.NetSales)/NULLIF(SUM(f.CountedOrder),0),0)) AS AverageOrderValue,
           CASE WHEN COALESCE(SUM(f.CountedOrder),0) = 0 THEN 'NO_DATA' ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.WorkShifts AS w
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = w.StoreId
    LEFT JOIN dbo.ufn_AnalyticsOrderFacts(@FromDate,@ToDate) AS f ON f.WorkShiftId=w.ShiftId
    WHERE w.StartTimeUtc >= @FromDate AND w.StartTimeUtc < @ToDate
    GROUP BY w.ShiftId, w.StoreId ORDER BY w.ShiftId DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Operations_WorkShiftPaymentMix
    @FromDate datetime2, @ToDate datetime2, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    ;WITH PaymentEvents AS
    (
        SELECT f.WorkShiftId,f.StoreId,p.PaymentMethodId,
               CONVERT(bigint,CASE WHEN f.CountedOrder=1 THEN 1 ELSE 0 END) AS TransactionCount,
               CONVERT(decimal(19,2),p.Amount) AS Amount
        FROM dbo.Payments AS p INNER JOIN dbo.ufn_AnalyticsOrderFacts(@FromDate,@ToDate) AS f ON f.OrderId=p.OrderId
        WHERE p.PaymentStatusId=2
        UNION ALL
        SELECT f.WorkShiftId,f.StoreId,r.PaymentMethodId,CONVERT(bigint,0),-f.CompletedRefundAmount
        FROM dbo.OrderRefunds AS r INNER JOIN dbo.ufn_AnalyticsOrderFacts(@FromDate,@ToDate) AS f ON f.OrderId=r.OrderId
        WHERE r.Status=3
    )
    SELECT e.WorkShiftId,e.StoreId,pm.PaymentMethodId,pm.Code AS PaymentMethodCode,pm.Name AS PaymentMethodName,
           SUM(e.TransactionCount) AS TotalTransactions,SUM(e.Amount) AS Amount,'AVAILABLE' AS DataStatus
    FROM PaymentEvents AS e
    INNER JOIN dbo.PaymentMethods AS pm ON pm.PaymentMethodId=e.PaymentMethodId
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=e.StoreId
    WHERE e.WorkShiftId IS NOT NULL
    GROUP BY e.WorkShiftId,e.StoreId,pm.PaymentMethodId,pm.Code,pm.Name
    HAVING SUM(e.Amount)<>0 OR SUM(e.TransactionCount)<>0
    ORDER BY e.WorkShiftId DESC,Amount DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Operations_OfflineReconciliationExceptions
    @FromDate datetime2, @ToDate datetime2, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT w.ShiftId AS WorkShiftId, w.StoreId, w.IsExceptionClosed, w.OfflineOrderCountAtClose,
           w.OfflineEstimatedTotalAtClose, w.OfflineCashTotalAtClose, w.RequiresReconciliation,
           w.HasLateOfflineSync, w.LateOfflineSyncCount, w.LastLateOfflineSyncedAtUtc AS LastLateOfflineSyncedAt,
           CASE WHEN w.RequiresReconciliation = 1 THEN 'REQUIRES_RECONCILIATION' ELSE 'LATE_SYNC' END AS DataStatus
    FROM dbo.WorkShifts AS w INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = w.StoreId
    WHERE w.StartTimeUtc >= @FromDate AND w.StartTimeUtc < @ToDate
      AND (w.IsExceptionClosed = 1 OR w.RequiresReconciliation = 1 OR w.HasLateOfflineSync = 1)
    ORDER BY w.StartTimeUtc DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Operations_HourlyOrders
    @FromDate datetime2, @ToDate datetime2, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Hour', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    ;WITH Hours AS (SELECT 0 AS HourOfDay UNION ALL SELECT HourOfDay + 1 FROM Hours WHERE HourOfDay < 23),
    Actual AS
    (
        SELECT DATEPART(hour,f.CreatedAt) AS HourOfDay,SUM(f.CountedOrder) AS TotalOrders,SUM(f.NetSales) AS NetSales
        FROM dbo.ufn_AnalyticsOrderFacts(@FromDate,@ToDate) AS f
        INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=f.StoreId
        GROUP BY DATEPART(hour,f.CreatedAt)
    )
    SELECT h.HourOfDay, COALESCE(a.TotalOrders, 0) AS TotalOrders, COALESCE(a.NetSales, 0) AS NetSales,
           CASE WHEN a.TotalOrders IS NULL THEN 'NO_DATA' ELSE 'AVAILABLE' END AS DataStatus
    FROM Hours AS h LEFT JOIN Actual AS a ON a.HourOfDay = h.HourOfDay
    ORDER BY h.HourOfDay OPTION (MAXRECURSION 24);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Operations_WorkShiftTopDiscrepancies
    @FromDate datetime2, @ToDate datetime2, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) w.ShiftId AS WorkShiftId, w.StoreId, w.UserId AS StaffId,
           w.CashDiscrepancy, ABS(COALESCE(w.CashDiscrepancy,0)) AS AbsoluteDiscrepancy,
           w.DiscrepancyReason, w.EndTimeUtc AS EndTime, 'AVAILABLE' AS DataStatus
    FROM dbo.WorkShifts AS w INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = w.StoreId
    WHERE w.EndTimeUtc >= @FromDate AND w.EndTimeUtc < @ToDate
      AND w.CashDiscrepancy IS NOT NULL ORDER BY ABS(w.CashDiscrepancy) DESC, w.ShiftId DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Operations_WorkShiftKpis
    @FromDate datetime2, @ToDate datetime2, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT_BIG(w.ShiftId) AS TotalWorkShifts,
           SUM(CASE WHEN w.EndTimeUtc IS NULL THEN 1 ELSE 0 END) AS OpenWorkShifts,
           SUM(CASE WHEN w.IsExceptionClosed = 1 THEN 1 ELSE 0 END) AS ExceptionClosedCount,
           SUM(CASE WHEN w.RequiresReconciliation = 1 THEN 1 ELSE 0 END) AS ReconciliationCount,
           COALESCE(SUM(ABS(COALESCE(w.CashDiscrepancy,0))),0) AS AbsoluteCashDiscrepancy,
           CASE WHEN COUNT_BIG(w.ShiftId)=0 THEN 'NO_DATA' ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.WorkShifts AS w INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=w.StoreId
    WHERE w.StartTimeUtc >= @FromDate AND w.StartTimeUtc < @ToDate;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Product_TopProducts
    @FromDate datetime2,@ToDate datetime2,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) od.DrinkId,od.DrinkName,d.CategoryId,c.Name AS CategoryName,
           SUM(od.Quantity) AS TotalSold,
           SUM((od.Price-COALESCE(t.ToppingUnitPrice,0))*od.Quantity) AS NetSales,
           CONVERT(decimal(9,4),COALESCE(
               SUM(od.Quantity) * 1.0
               / NULLIF(SUM(SUM(od.Quantity)) OVER (),0) * 100,0)) AS QuantityShare,
           CONVERT(decimal(9,4),COALESCE(
               SUM((od.Price-COALESCE(t.ToppingUnitPrice,0))*od.Quantity)
               / NULLIF(SUM(SUM((od.Price-COALESCE(t.ToppingUnitPrice,0))*od.Quantity)) OVER (),0) * 100,0)) AS RevenueShare,
           SUM(CASE WHEN od.CostStatus=1 THEN od.TotalCogs ELSE 0 END) AS ConfirmedCogs,
           SUM(CASE WHEN od.CostStatus=1 THEN (od.Price-COALESCE(t.ToppingUnitPrice,0))*od.Quantity-COALESCE(od.TotalCogs,0) ELSE 0 END) AS ConfirmedGrossProfit,
           CONVERT(decimal(9,4),COALESCE(
               SUM(CASE WHEN od.CostStatus=1 THEN (od.Price-COALESCE(t.ToppingUnitPrice,0))*od.Quantity-COALESCE(od.TotalCogs,0) ELSE 0 END)
               / NULLIF(SUM(CASE WHEN od.CostStatus=1 THEN (od.Price-COALESCE(t.ToppingUnitPrice,0))*od.Quantity ELSE 0 END),0),0)) AS ConfirmedMarginRate,
           CONVERT(decimal(9,4),COALESCE(
               SUM((od.Price-COALESCE(t.ToppingUnitPrice,0))*od.Quantity)
               / NULLIF(SUM(SUM((od.Price-COALESCE(t.ToppingUnitPrice,0))*od.Quantity)) OVER (),0) * 100,0)) AS ContributionPercent,
           CASE WHEN SUM(CASE WHEN od.CostStatus<>1 THEN 1 ELSE 0 END)>0 THEN 'PARTIAL_COGS' ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.OrderDetails AS od
    INNER JOIN dbo.ufn_AnalyticsOrderFacts(@FromDate,@ToDate) AS f ON f.OrderId=od.OrderId AND f.CountedOrder=1
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=f.StoreId
    LEFT JOIN dbo.Drinks AS d ON d.DrinkId=od.DrinkId
    LEFT JOIN dbo.DrinkCategories AS c ON c.CategoryId=d.CategoryId
    OUTER APPLY(SELECT SUM(ot.Price) AS ToppingUnitPrice FROM dbo.OrderToppings AS ot WHERE ot.OrderDetailId=od.OrderDetailId) t
    WHERE f.CreatedAt>=@FromDate AND f.CreatedAt<@ToDate
    GROUP BY od.DrinkId,od.DrinkName,d.CategoryId,c.Name
    ORDER BY TotalSold DESC, NetSales DESC, od.DrinkId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Product_VolumeMarginMatrix
    @FromDate datetime2,@ToDate datetime2,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) od.DrinkId,od.DrinkName,SUM(od.Quantity) AS Volume,
           SUM((od.Price-COALESCE(t.ToppingUnitPrice,0))*od.Quantity) AS Revenue,
           SUM(CASE WHEN od.CostStatus=1 THEN od.TotalCogs ELSE 0 END) AS ConfirmedCogs,
           CONVERT(decimal(9,4),COALESCE(SUM(CASE WHEN od.CostStatus=1 THEN (od.Price-COALESCE(t.ToppingUnitPrice,0))*od.Quantity-COALESCE(od.TotalCogs,0) ELSE 0 END)
             /NULLIF(SUM(CASE WHEN od.CostStatus=1 THEN (od.Price-COALESCE(t.ToppingUnitPrice,0))*od.Quantity ELSE 0 END),0),0)) AS ConfirmedMarginRate,
           CASE WHEN SUM(CASE WHEN od.CostStatus<>1 THEN 1 ELSE 0 END)>0 THEN 'PARTIAL_COGS' ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.OrderDetails AS od
    INNER JOIN dbo.ufn_AnalyticsOrderFacts(@FromDate,@ToDate) AS f ON f.OrderId=od.OrderId AND f.CountedOrder=1
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=f.StoreId
    OUTER APPLY(SELECT SUM(ot.Price) AS ToppingUnitPrice FROM dbo.OrderToppings AS ot WHERE ot.OrderDetailId=od.OrderDetailId) t
    WHERE f.CreatedAt>=@FromDate AND f.CreatedAt<@ToDate
    GROUP BY od.DrinkId,od.DrinkName ORDER BY Volume DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Product_SizeMargin
    @FromDate datetime2,@ToDate datetime2,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT od.SizeId,COALESCE(od.SizeName,N'Không size') AS SizeName,SUM(od.Quantity) AS TotalSold,
           SUM((od.Price-COALESCE(t.ToppingUnitPrice,0))*od.Quantity) AS Revenue,
           SUM(CASE WHEN od.CostStatus=1 THEN od.TotalCogs ELSE 0 END) AS ConfirmedCogs,
           SUM(CASE WHEN od.CostStatus=1 THEN (od.Price-COALESCE(t.ToppingUnitPrice,0))*od.Quantity-COALESCE(od.TotalCogs,0) ELSE 0 END) AS ConfirmedGrossProfit,
           CASE WHEN SUM(CASE WHEN od.CostStatus<>1 THEN 1 ELSE 0 END)>0 THEN 'PARTIAL_COGS' ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.OrderDetails AS od
    INNER JOIN dbo.ufn_AnalyticsOrderFacts(@FromDate,@ToDate) AS f ON f.OrderId=od.OrderId AND f.CountedOrder=1
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=f.StoreId
    OUTER APPLY(SELECT SUM(ot.Price) AS ToppingUnitPrice FROM dbo.OrderToppings AS ot WHERE ot.OrderDetailId=od.OrderDetailId) t
    WHERE f.CreatedAt>=@FromDate AND f.CreatedAt<@ToDate
    GROUP BY od.SizeId,od.SizeName ORDER BY Revenue DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Product_TopToppings
    @FromDate datetime2,@ToDate datetime2,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) ot.ToppingId,ot.ToppingName,
           SUM(od.Quantity) AS TotalUsed,SUM(ot.Price*od.Quantity) AS Revenue,
           SUM(CASE WHEN ot.CostStatus=1 THEN ot.TotalCogs ELSE 0 END) AS ConfirmedCogs,
           CASE WHEN SUM(CASE WHEN ot.CostStatus<>1 THEN 1 ELSE 0 END)>0 THEN 'PARTIAL_COGS' ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.OrderToppings AS ot INNER JOIN dbo.OrderDetails AS od ON od.OrderDetailId=ot.OrderDetailId
    INNER JOIN dbo.ufn_AnalyticsOrderFacts(@FromDate,@ToDate) AS f ON f.OrderId=od.OrderId AND f.CountedOrder=1
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=f.StoreId
    WHERE f.CreatedAt>=@FromDate AND f.CreatedAt<@ToDate
    GROUP BY ot.ToppingId,ot.ToppingName ORDER BY Revenue DESC,TotalUsed DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Product_BomHealth
    @FromDate datetime2,@ToDate datetime2,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
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
    @FromDate datetime2,@ToDate datetime2,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) od.DrinkId,od.DrinkName,SUM(od.Quantity) AS TotalSold,
           SUM(CASE WHEN od.CostStatus=1 THEN od.TotalCogs ELSE 0 END) AS ConfirmedCogs,
           SUM(CASE WHEN od.CostStatus=1 THEN od.Price*od.Quantity-COALESCE(od.TotalCogs,0) ELSE 0 END) AS ConfirmedGrossProfit,
           CASE WHEN SUM(CASE WHEN od.CostStatus<>1 THEN 1 ELSE 0 END)>0 THEN 'PARTIAL_COGS' ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.OrderDetails AS od
    INNER JOIN dbo.ufn_AnalyticsOrderFacts(@FromDate,@ToDate) AS f ON f.OrderId=od.OrderId AND f.CountedOrder=1
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=f.StoreId
    WHERE f.CreatedAt>=@FromDate AND f.CreatedAt<@ToDate
    GROUP BY od.DrinkId,od.DrinkName
    ORDER BY ConfirmedCogs DESC,ConfirmedGrossProfit ASC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Workforce_ShiftStatus
    @FromDate datetime2,@ToDate datetime2,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ss.StaffShiftId,ss.StaffId,st.FullName,sh.StoreId,ss.WorkDate,sh.ShiftId,sh.Name AS ShiftName,
           planned.PlannedStartAt,
           DATEADD(day,CASE WHEN sh.IsOvernight=1 OR planned.EndTime<=planned.StartTime THEN 1 ELSE 0 END,
               DATEADD(day,DATEDIFF(day,0,ss.WorkDate),CONVERT(datetime2,planned.EndTime))) AS PlannedEndAt,
           status.Code AS StatusCode,
           CONVERT(bit,CASE WHEN sh.IsOvernight=1 OR planned.EndTime<=planned.StartTime THEN 1 ELSE 0 END) AS IsOvernight,
           'PLANNED_SCHEDULE' AS DataStatus
    FROM dbo.StaffShifts AS ss
    INNER JOIN dbo.Staffs AS st ON st.StaffId=ss.StaffId
    INNER JOIN dbo.Shifts AS sh ON sh.ShiftId=ss.ShiftId
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=sh.StoreId
    INNER JOIN dbo.StaffShiftStatuses AS status ON status.StaffShiftStatusId=ss.StatusId
    CROSS APPLY
    (
        SELECT COALESCE(ss.CustomStartTime,sh.StartTime) AS StartTime,
               COALESCE(ss.CustomEndTime,sh.EndTime) AS EndTime,
               DATEADD(day,DATEDIFF(day,0,ss.WorkDate),
                   CONVERT(datetime2,COALESCE(ss.CustomStartTime,sh.StartTime))) AS PlannedStartAt
    ) AS planned
    WHERE ss.WorkDate>=@FromDate AND ss.WorkDate<@ToDate
      AND status.Code IN ('SCHEDULED','CANCELLED')
    ORDER BY ss.WorkDate DESC,ss.StaffShiftId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Workforce_HourlyDemand
    @FromDate datetime2,@ToDate datetime2,@StoreIds nvarchar(max),@Granularity varchar(10)='Hour',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    ;WITH Hours AS(SELECT 0 AS HourOfDay UNION ALL SELECT HourOfDay+1 FROM Hours WHERE HourOfDay<23),
    Demand AS
    (
        SELECT DATEPART(hour,f.CreatedAt) AS HourOfDay,SUM(f.CountedOrder) AS TotalOrders
        FROM dbo.ufn_AnalyticsOrderFacts(@FromDate,@ToDate) AS f
        INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=f.StoreId
        GROUP BY DATEPART(hour,f.CreatedAt)
    ),Schedules AS
    (
        SELECT ss.StaffShiftId,
               DATEADD(day,DATEDIFF(day,0,ss.WorkDate),CONVERT(datetime2,COALESCE(ss.CustomStartTime,sh.StartTime))) AS StartAt,
               DATEADD(day,CASE WHEN sh.IsOvernight=1 OR COALESCE(ss.CustomEndTime,sh.EndTime)<=COALESCE(ss.CustomStartTime,sh.StartTime) THEN 1 ELSE 0 END,
                   DATEADD(day,DATEDIFF(day,0,ss.WorkDate),CONVERT(datetime2,COALESCE(ss.CustomEndTime,sh.EndTime)))) AS EndAt
        FROM dbo.StaffShifts AS ss
        INNER JOIN dbo.Shifts AS sh ON sh.ShiftId=ss.ShiftId
        INNER JOIN dbo.StaffShiftStatuses AS status ON status.StaffShiftStatusId=ss.StatusId
        INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=sh.StoreId
        WHERE ss.WorkDate>=DATEADD(day,-1,@FromDate) AND ss.WorkDate<@ToDate
          AND status.Code='SCHEDULED'
    ),Staffing AS
    (
        SELECT h.HourOfDay,COUNT_BIG(s.StaffShiftId) AS ScheduledStaffCount
        FROM Hours AS h
        INNER JOIN Schedules AS s
          ON DATEPART(hour,s.StartAt)<=h.HourOfDay
             AND (CONVERT(date,s.EndAt)>CONVERT(date,s.StartAt) OR h.HourOfDay<DATEPART(hour,s.EndAt))
          OR CONVERT(date,s.EndAt)>CONVERT(date,s.StartAt) AND h.HourOfDay<DATEPART(hour,s.EndAt)
        GROUP BY h.HourOfDay
    )
    SELECT h.HourOfDay,COALESCE(d.TotalOrders,0) AS TotalOrders,
           COALESCE(s.ScheduledStaffCount,0) AS ScheduledStaffCount,
           CASE WHEN d.TotalOrders IS NULL AND s.ScheduledStaffCount IS NULL THEN 'NO_DATA' ELSE 'PLANNED_SCHEDULE' END AS DataStatus
    FROM Hours AS h LEFT JOIN Demand AS d ON d.HourOfDay=h.HourOfDay LEFT JOIN Staffing AS s ON s.HourOfDay=h.HourOfDay
    ORDER BY h.HourOfDay OPTION(MAXRECURSION 24);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Workforce_StaffPerformance
    @FromDate datetime2,@ToDate datetime2,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) st.StaffId,st.FullName,st.StoreId,
           COALESCE(shifts.WorkShiftCount,0) AS WorkShiftCount,
           COALESCE(sales.TotalOrders,0) AS TotalOrders,COALESCE(sales.NetSales,0) AS NetSales,
           CONVERT(decimal(19,2),COALESCE(sales.NetSales/NULLIF(CONVERT(decimal(19,2),sales.TotalOrders),0),0)) AS AverageOrderValue,
           CONVERT(decimal(19,2),COALESCE(CONVERT(decimal(19,2),sales.TotalOrders)/NULLIF(shifts.WorkShiftCount,0),0)) AS OrdersPerWorkShift,
           CASE WHEN COALESCE(shifts.WorkShiftCount,0)=0 AND COALESCE(sales.TotalOrders,0)=0 THEN 'NO_DATA' ELSE 'POS_ACTIVITY' END AS DataStatus
    FROM dbo.Staffs AS st INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=st.StoreId
    OUTER APPLY
    (
        SELECT COUNT_BIG(w.ShiftId) AS WorkShiftCount
        FROM dbo.WorkShifts AS w
        WHERE w.UserId=st.StaffId AND w.StoreId=st.StoreId
          AND w.StartTimeUtc>=@FromDate AND w.StartTimeUtc<@ToDate
    ) AS shifts
    OUTER APPLY
    (
        SELECT SUM(f.CountedOrder) AS TotalOrders,SUM(f.NetSales) AS NetSales
        FROM dbo.ufn_AnalyticsOrderFacts(@FromDate,@ToDate) AS f
        WHERE f.StaffId=st.StaffId AND f.StoreId=st.StoreId
    ) AS sales
    ORDER BY NetSales DESC,st.StaffId;
END;
GO

/*
   Legacy compatibility contracts used by the current DashboardRepository.
   Create local stubs first because SQL Server resolves names beginning with
   sp_ against master; CREATE OR ALTER can otherwise bind to a master fixture
   and fail with error 208 instead of creating the procedure in CafeChain.
*/
IF OBJECT_ID(N'dbo.sp_Revenue_By_Store',N'P') IS NULL EXEC(N'CREATE PROCEDURE dbo.sp_Revenue_By_Store AS RETURN 0;');
IF OBJECT_ID(N'dbo.sp_Revenue_Filtered',N'P') IS NULL EXEC(N'CREATE PROCEDURE dbo.sp_Revenue_Filtered AS RETURN 0;');
IF OBJECT_ID(N'dbo.sp_Inventory_Summary',N'P') IS NULL EXEC(N'CREATE PROCEDURE dbo.sp_Inventory_Summary AS RETURN 0;');
IF OBJECT_ID(N'dbo.sp_Waste_Report',N'P') IS NULL EXEC(N'CREATE PROCEDURE dbo.sp_Waste_Report AS RETURN 0;');
IF OBJECT_ID(N'dbo.sp_Cash_Flow_Today',N'P') IS NULL EXEC(N'CREATE PROCEDURE dbo.sp_Cash_Flow_Today AS RETURN 0;');
IF OBJECT_ID(N'dbo.sp_Top_Selling_Drinks_Filtered',N'P') IS NULL EXEC(N'CREATE PROCEDURE dbo.sp_Top_Selling_Drinks_Filtered AS RETURN 0;');
IF OBJECT_ID(N'dbo.sp_Top_Toppings_Filtered',N'P') IS NULL EXEC(N'CREATE PROCEDURE dbo.sp_Top_Toppings_Filtered AS RETURN 0;');
IF OBJECT_ID(N'dbo.sp_Revenue_By_PaymentMethod_Filtered',N'P') IS NULL EXEC(N'CREATE PROCEDURE dbo.sp_Revenue_By_PaymentMethod_Filtered AS RETURN 0;');
IF OBJECT_ID(N'dbo.sp_Order_Status_Stats',N'P') IS NULL EXEC(N'CREATE PROCEDURE dbo.sp_Order_Status_Stats AS RETURN 0;');
IF OBJECT_ID(N'dbo.sp_Revenue_By_Hour',N'P') IS NULL EXEC(N'CREATE PROCEDURE dbo.sp_Revenue_By_Hour AS RETURN 0;');
IF OBJECT_ID(N'dbo.sp_Staff_Performance_Filtered',N'P') IS NULL EXEC(N'CREATE PROCEDURE dbo.sp_Staff_Performance_Filtered AS RETURN 0;');
IF OBJECT_ID(N'dbo.sp_Dashboard_Summary_Filtered',N'P') IS NULL EXEC(N'CREATE PROCEDURE dbo.sp_Dashboard_Summary_Filtered AS RETURN 0;');
GO

ALTER PROCEDURE dbo.sp_Revenue_By_Store
    @FromDate datetime,@ToDate datetime,@StoreIds nvarchar(max)=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT s.StoreId,s.Name,COALESCE(SUM(f.CountedOrder),0) AS TotalOrders,
           COALESCE(SUM(f.NetSales),0) AS Revenue
    FROM dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope INNER JOIN dbo.Stores AS s ON s.StoreId=scope.StoreId
    LEFT JOIN dbo.ufn_AnalyticsOrderFacts(@FromDate,@ToDate) AS f ON f.StoreId=s.StoreId
    GROUP BY s.StoreId,s.Name ORDER BY Revenue DESC,s.StoreId;
END;
GO

ALTER PROCEDURE dbo.sp_Revenue_Filtered
    @FromDate datetime,@ToDate datetime,@StoreIds nvarchar(max)=NULL,@ProvinceId int=NULL,@WardId int=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CONVERT(date,f.CreatedAt) AS [Date],SUM(f.CountedOrder) AS TotalOrders,
           COALESCE(SUM(f.NetSales),0) AS Revenue
    FROM dbo.ufn_AnalyticsOrderFacts(@FromDate,@ToDate) AS f
    INNER JOIN dbo.Stores AS s ON s.StoreId=f.StoreId
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=f.StoreId
    WHERE f.CreatedAt>=@FromDate AND f.CreatedAt<@ToDate
      AND (@ProvinceId IS NULL OR s.ProvinceId=@ProvinceId) AND (@WardId IS NULL OR s.WardId=@WardId)
    GROUP BY CONVERT(date,f.CreatedAt) ORDER BY [Date];
END;
GO

ALTER PROCEDURE dbo.sp_Inventory_Summary @StoreId int
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

ALTER PROCEDURE dbo.sp_Waste_Report
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

ALTER PROCEDURE dbo.sp_Cash_Flow_Today
    @StoreIds nvarchar(max),@FromDate date=NULL,@ToDate date=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET @FromDate=COALESCE(@FromDate,CONVERT(date,SYSUTCDATETIME()));
    SET @ToDate=COALESCE(@ToDate,@FromDate);
    ;WITH PaymentEvents AS
    (
        SELECT f.WorkShiftId,f.StoreId,p.PaymentMethodId,CONVERT(decimal(19,2),p.Amount) AS Amount
        FROM dbo.Payments AS p
        INNER JOIN dbo.ufn_AnalyticsOrderFacts(@FromDate,@ToDate) AS f ON f.OrderId=p.OrderId
        WHERE p.PaymentStatusId=2
        UNION ALL
        SELECT f.WorkShiftId,f.StoreId,r.PaymentMethodId,-f.CompletedRefundAmount
        FROM dbo.OrderRefunds AS r
        INNER JOIN dbo.ufn_AnalyticsOrderFacts(@FromDate,@ToDate) AS f ON f.OrderId=r.OrderId
        WHERE r.Status=3
    )
    SELECT w.ShiftId AS CashSessionId,w.UserId AS StaffId,w.StartTimeUtc AS OpenTime,w.EndTimeUtc AS CloseTime,w.StartingCash AS StartCash,
           COALESCE(SUM(CASE WHEN pm.Code='CASH' THEN e.Amount ELSE 0 END),0) AS CashIn,
           COALESCE(SUM(CASE WHEN pm.Code<>'CASH' THEN e.Amount ELSE 0 END),0) AS NonCashIn,
           COALESCE(SUM(e.Amount),0) AS TotalRevenue
    FROM dbo.WorkShifts AS w
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=w.StoreId
    LEFT JOIN PaymentEvents AS e ON e.WorkShiftId=w.ShiftId
    LEFT JOIN dbo.PaymentMethods AS pm ON pm.PaymentMethodId=e.PaymentMethodId
    WHERE w.StartTimeUtc>=@FromDate AND w.StartTimeUtc<@ToDate
    GROUP BY w.ShiftId,w.UserId,w.StartTimeUtc,w.EndTimeUtc,w.StartingCash ORDER BY w.StartTimeUtc DESC;
END;
GO

ALTER PROCEDURE dbo.sp_Top_Selling_Drinks_Filtered
    @Top int=10,@FromDate datetime,@ToDate datetime,@StoreIds nvarchar(max)=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) od.DrinkId,od.DrinkName,SUM(od.Quantity) AS TotalSold,
           SUM((od.Price-COALESCE(t.ToppingUnitPrice,0))*od.Quantity) AS Revenue
    FROM dbo.OrderDetails AS od
    INNER JOIN dbo.ufn_AnalyticsOrderFacts(@FromDate,@ToDate) AS f ON f.OrderId=od.OrderId AND f.CountedOrder=1
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=f.StoreId
    OUTER APPLY(SELECT SUM(ot.Price) AS ToppingUnitPrice FROM dbo.OrderToppings AS ot WHERE ot.OrderDetailId=od.OrderDetailId) t
    WHERE f.CreatedAt>=@FromDate AND f.CreatedAt<@ToDate
    GROUP BY od.DrinkId,od.DrinkName ORDER BY TotalSold DESC,Revenue DESC;
END;
GO

ALTER PROCEDURE dbo.sp_Top_Toppings_Filtered
    @FromDate datetime,@ToDate datetime,@StoreIds nvarchar(max)=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ot.ToppingId,ot.ToppingName,SUM(od.Quantity) AS TotalUsed,SUM(ot.Price*od.Quantity) AS Revenue
    FROM dbo.OrderToppings AS ot INNER JOIN dbo.OrderDetails AS od ON od.OrderDetailId=ot.OrderDetailId
    INNER JOIN dbo.ufn_AnalyticsOrderFacts(@FromDate,@ToDate) AS f ON f.OrderId=od.OrderId AND f.CountedOrder=1
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=f.StoreId
    WHERE f.CreatedAt>=@FromDate AND f.CreatedAt<@ToDate
    GROUP BY ot.ToppingId,ot.ToppingName ORDER BY TotalUsed DESC;
END;
GO

DROP PROCEDURE IF EXISTS dbo.sp_Top_Customers;
GO

ALTER PROCEDURE dbo.sp_Revenue_By_PaymentMethod_Filtered
    @FromDate datetime,@ToDate datetime,@StoreIds nvarchar(max)=NULL
AS
BEGIN
    SET NOCOUNT ON;
    ;WITH Events AS
    (
        SELECT p.PaymentMethodId,CONVERT(bigint,CASE WHEN f.CountedOrder=1 THEN 1 ELSE 0 END) AS TransactionCount,
               CONVERT(decimal(19,2),p.Amount) AS Amount
        FROM dbo.Payments AS p
        INNER JOIN dbo.ufn_AnalyticsOrderFacts(@FromDate,@ToDate) AS f ON f.OrderId=p.OrderId
        INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=f.StoreId
        WHERE p.PaymentStatusId=2
        UNION ALL
        SELECT r.PaymentMethodId,CONVERT(bigint,0),-f.CompletedRefundAmount
        FROM dbo.OrderRefunds AS r
        INNER JOIN dbo.ufn_AnalyticsOrderFacts(@FromDate,@ToDate) AS f ON f.OrderId=r.OrderId
        INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=f.StoreId
        WHERE r.Status=3
    )
    SELECT pm.Name,SUM(e.TransactionCount) AS TotalTransactions,SUM(e.Amount) AS Revenue
    FROM Events AS e INNER JOIN dbo.PaymentMethods AS pm ON pm.PaymentMethodId=e.PaymentMethodId
    GROUP BY pm.Name HAVING SUM(e.Amount)<>0 OR SUM(e.TransactionCount)<>0 ORDER BY Revenue DESC;
END;
GO

ALTER PROCEDURE dbo.sp_Order_Status_Stats
AS
BEGIN
    SET NOCOUNT ON;
    SELECT os.Name,COUNT_BIG(o.OrderId) AS TotalOrders FROM dbo.OrderStatuses AS os
    LEFT JOIN dbo.Orders AS o ON o.OrderStatusId=os.OrderStatusId GROUP BY os.Name ORDER BY os.Name;
END;
GO

ALTER PROCEDURE dbo.sp_Revenue_By_Hour
    @FromDate datetime=NULL,@ToDate datetime=NULL,@StoreIds nvarchar(max)=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET @FromDate=COALESCE(@FromDate,CONVERT(datetime,'19000101'));
    SET @ToDate=COALESCE(@ToDate,CONVERT(datetime,'99991230'));
    SELECT DATEPART(hour,f.CreatedAt) AS HourOfDay,SUM(f.CountedOrder) AS TotalOrders,SUM(f.NetSales) AS Revenue
    FROM dbo.ufn_AnalyticsOrderFacts(@FromDate,@ToDate) AS f
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=f.StoreId
    GROUP BY DATEPART(hour,f.CreatedAt) ORDER BY HourOfDay;
END;
GO

ALTER PROCEDURE dbo.sp_Staff_Performance_Filtered
    @FromDate datetime,@ToDate datetime,@StoreIds nvarchar(max)=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT st.StaffId,st.FullName,COALESCE(SUM(f.CountedOrder),0) AS TotalOrders,
           COALESCE(SUM(f.NetSales),0) AS Revenue
    FROM dbo.Staffs AS st INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=st.StoreId
    LEFT JOIN dbo.ufn_AnalyticsOrderFacts(@FromDate,@ToDate) AS f
      ON f.StaffId=st.StaffId AND f.StoreId=st.StoreId
    GROUP BY st.StaffId,st.FullName ORDER BY Revenue DESC,TotalOrders DESC;
END;
GO

ALTER PROCEDURE dbo.sp_Dashboard_Summary_Filtered
    @FromDate datetime,@ToDate datetime,@StoreIds nvarchar(max)=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COALESCE(SUM(f.CountedOrder),0) AS TotalOrders,
           COALESCE(SUM(f.NetSales),0) AS Revenue,
           COALESCE(SUM(CASE WHEN CONVERT(date,f.CreatedAt)=CONVERT(date,@ToDate) THEN f.CountedOrder ELSE 0 END),0) AS TodayOrders
    FROM dbo.ufn_AnalyticsOrderFacts(@FromDate,@ToDate) AS f
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=f.StoreId;
END;
GO

/* AI Dashboard v2 read-only datasets. These procedures accept the same
   validated scope contract as the existing Dashboard procedures. */
CREATE OR ALTER PROCEDURE dbo.usp_Dashboard_OrderStatusSummary
    @FromDate datetime2, @ToDate datetime2, @StoreIds nvarchar(max),
    @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT s.StoreId, s.Name AS StoreName,
           COUNT_BIG(o.OrderId) AS TotalOrders,
           SUM(CASE WHEN o.OrderStatusId = 5 THEN 1 ELSE 0 END) AS CompletedOrders,
           SUM(CASE WHEN o.OrderStatusId = 6 THEN 1 ELSE 0 END) AS CancelledOrders,
           CONVERT(decimal(9,4), COALESCE(
               SUM(CASE WHEN o.OrderStatusId = 6 THEN 1 ELSE 0 END) * 1.0
               / NULLIF(COUNT_BIG(o.OrderId), 0), 0)) AS CancellationRate,
           CASE WHEN COUNT_BIG(o.OrderId) = 0 THEN 'NO_DATA' ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.Orders AS o
    INNER JOIN dbo.Stores AS s ON s.StoreId = o.StoreId
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = o.StoreId
    WHERE o.CreatedAt >= @FromDate
      AND o.CreatedAt < @ToDate
    GROUP BY s.StoreId, s.Name
    ORDER BY CancellationRate DESC, s.StoreId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Product_CategoryPerformance
    @FromDate datetime2, @ToDate datetime2, @StoreIds nvarchar(max),
    @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top, 0), 10))
           d.CategoryId, COALESCE(c.Name, N'Chưa phân loại') AS CategoryName,
           SUM(od.Quantity) AS TotalSold,
           SUM((od.Price - COALESCE(t.ToppingUnitPrice, 0)) * od.Quantity) AS Revenue,
           SUM(CASE WHEN od.CostStatus = 1 THEN od.TotalCogs ELSE 0 END) AS ConfirmedCogs,
           SUM(CASE WHEN od.CostStatus = 1
               THEN (od.Price - COALESCE(t.ToppingUnitPrice, 0)) * od.Quantity - COALESCE(od.TotalCogs, 0)
               ELSE 0 END) AS ConfirmedGrossProfit,
           CONVERT(decimal(9,4), COALESCE(
               SUM(CASE WHEN od.CostStatus = 1
                   THEN (od.Price - COALESCE(t.ToppingUnitPrice, 0)) * od.Quantity - COALESCE(od.TotalCogs, 0)
                   ELSE 0 END)
               / NULLIF(SUM(CASE WHEN od.CostStatus = 1
                   THEN (od.Price - COALESCE(t.ToppingUnitPrice, 0)) * od.Quantity ELSE 0 END), 0), 0)) AS ConfirmedMarginRate,
           CONVERT(decimal(9,4), COALESCE(
               SUM((od.Price - COALESCE(t.ToppingUnitPrice, 0)) * od.Quantity)
               / NULLIF(SUM(SUM((od.Price - COALESCE(t.ToppingUnitPrice, 0)) * od.Quantity)) OVER (), 0) * 100, 0)) AS ContributionPercent,
           CASE WHEN SUM(CASE WHEN od.CostStatus <> 1 THEN 1 ELSE 0 END) > 0
                THEN 'PARTIAL_COGS' ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.OrderDetails AS od
    INNER JOIN dbo.Drinks AS d ON d.DrinkId = od.DrinkId
    LEFT JOIN dbo.DrinkCategories AS c ON c.CategoryId = d.CategoryId
    INNER JOIN dbo.ufn_AnalyticsOrderFacts(@FromDate, @ToDate) AS f
        ON f.OrderId = od.OrderId AND f.CountedOrder = 1
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = f.StoreId
    OUTER APPLY (SELECT SUM(ot.Price) AS ToppingUnitPrice
                 FROM dbo.OrderToppings AS ot WHERE ot.OrderDetailId = od.OrderDetailId) AS t
    GROUP BY d.CategoryId, c.Name
    ORDER BY TotalSold DESC, Revenue DESC, d.CategoryId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Product_PeriodPerformance
    @FromDate datetime2, @ToDate datetime2, @StoreIds nvarchar(max),
    @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top, 0), 10))
           od.DrinkId, od.DrinkName, SUM(od.Quantity) AS TotalSold,
           SUM((od.Price - COALESCE(t.ToppingUnitPrice, 0)) * od.Quantity) AS Revenue,
           SUM(CASE WHEN od.CostStatus = 1 THEN od.TotalCogs ELSE 0 END) AS ConfirmedCogs,
           SUM(CASE WHEN od.CostStatus = 1
               THEN (od.Price - COALESCE(t.ToppingUnitPrice, 0)) * od.Quantity - COALESCE(od.TotalCogs, 0)
               ELSE 0 END) AS ConfirmedGrossProfit,
           CONVERT(decimal(9,4), COALESCE(
               SUM(CASE WHEN od.CostStatus = 1
                   THEN (od.Price - COALESCE(t.ToppingUnitPrice, 0)) * od.Quantity - COALESCE(od.TotalCogs, 0)
                   ELSE 0 END)
               / NULLIF(SUM(CASE WHEN od.CostStatus = 1
                   THEN (od.Price - COALESCE(t.ToppingUnitPrice, 0)) * od.Quantity ELSE 0 END), 0), 0)) AS ConfirmedMarginRate,
           CONVERT(decimal(9,4), COALESCE(
               SUM((od.Price - COALESCE(t.ToppingUnitPrice, 0)) * od.Quantity)
               / NULLIF(SUM(SUM((od.Price - COALESCE(t.ToppingUnitPrice, 0)) * od.Quantity)) OVER (), 0) * 100, 0)) AS ContributionPercent,
           CASE WHEN SUM(CASE WHEN od.CostStatus <> 1 THEN 1 ELSE 0 END) > 0
                THEN 'PARTIAL_COGS' ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.OrderDetails AS od
    INNER JOIN dbo.ufn_AnalyticsOrderFacts(@FromDate, @ToDate) AS f
        ON f.OrderId = od.OrderId AND f.CountedOrder = 1
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = f.StoreId
    OUTER APPLY (SELECT SUM(ot.Price) AS ToppingUnitPrice
                 FROM dbo.OrderToppings AS ot WHERE ot.OrderDetailId = od.OrderDetailId) AS t
    GROUP BY od.DrinkId, od.DrinkName
    ORDER BY Revenue DESC, TotalSold DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Product_LowVolumeProducts
    @FromDate datetime2, @ToDate datetime2, @StoreIds nvarchar(max),
    @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top, 0), 10))
           od.DrinkId, od.DrinkName, SUM(od.Quantity) AS TotalSold,
           SUM((od.Price - COALESCE(t.ToppingUnitPrice, 0)) * od.Quantity) AS Revenue,
           SUM(CASE WHEN od.CostStatus = 1 THEN od.TotalCogs ELSE 0 END) AS ConfirmedCogs,
           SUM(CASE WHEN od.CostStatus = 1
               THEN (od.Price - COALESCE(t.ToppingUnitPrice, 0)) * od.Quantity - COALESCE(od.TotalCogs, 0)
               ELSE 0 END) AS ConfirmedGrossProfit,
           CONVERT(decimal(9,4), COALESCE(
               SUM(CASE WHEN od.CostStatus = 1
                   THEN (od.Price - COALESCE(t.ToppingUnitPrice, 0)) * od.Quantity - COALESCE(od.TotalCogs, 0)
                   ELSE 0 END)
               / NULLIF(SUM(CASE WHEN od.CostStatus = 1
                   THEN (od.Price - COALESCE(t.ToppingUnitPrice, 0)) * od.Quantity ELSE 0 END), 0), 0)) AS ConfirmedMarginRate,
           CONVERT(decimal(9,4), COALESCE(
               SUM((od.Price - COALESCE(t.ToppingUnitPrice, 0)) * od.Quantity)
               / NULLIF(SUM(SUM((od.Price - COALESCE(t.ToppingUnitPrice, 0)) * od.Quantity)) OVER (), 0) * 100, 0)) AS ContributionPercent,
           CASE WHEN SUM(CASE WHEN od.CostStatus <> 1 THEN 1 ELSE 0 END) > 0
                THEN 'PARTIAL_COGS' ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.OrderDetails AS od
    INNER JOIN dbo.ufn_AnalyticsOrderFacts(@FromDate, @ToDate) AS f
        ON f.OrderId = od.OrderId AND f.CountedOrder = 1
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = f.StoreId
    OUTER APPLY (SELECT SUM(ot.Price) AS ToppingUnitPrice
                 FROM dbo.OrderToppings AS ot WHERE ot.OrderDetailId = od.OrderDetailId) AS t
    GROUP BY od.DrinkId, od.DrinkName
    ORDER BY TotalSold ASC, Revenue ASC, od.DrinkId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Product_LowMarginProducts
    @FromDate datetime2, @ToDate datetime2, @StoreIds nvarchar(max),
    @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top, 0), 10))
           od.DrinkId, od.DrinkName, SUM(od.Quantity) AS TotalSold,
           SUM((od.Price - COALESCE(t.ToppingUnitPrice, 0)) * od.Quantity) AS Revenue,
           SUM(od.TotalCogs) AS ConfirmedCogs,
           SUM((od.Price - COALESCE(t.ToppingUnitPrice, 0)) * od.Quantity - od.TotalCogs) AS ConfirmedGrossProfit,
           CONVERT(decimal(9,4), COALESCE(
               SUM((od.Price - COALESCE(t.ToppingUnitPrice, 0)) * od.Quantity - od.TotalCogs)
               / NULLIF(SUM((od.Price - COALESCE(t.ToppingUnitPrice, 0)) * od.Quantity), 0), 0)) AS ConfirmedMarginRate,
           CONVERT(decimal(9,4), COALESCE(
               SUM((od.Price - COALESCE(t.ToppingUnitPrice, 0)) * od.Quantity)
               / NULLIF(SUM(SUM((od.Price - COALESCE(t.ToppingUnitPrice, 0)) * od.Quantity)) OVER (), 0) * 100, 0)) AS ContributionPercent,
           'AVAILABLE' AS DataStatus
    FROM dbo.OrderDetails AS od
    INNER JOIN dbo.ufn_AnalyticsOrderFacts(@FromDate, @ToDate) AS f
        ON f.OrderId = od.OrderId AND f.CountedOrder = 1
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = f.StoreId
    OUTER APPLY (SELECT SUM(ot.Price) AS ToppingUnitPrice
                 FROM dbo.OrderToppings AS ot WHERE ot.OrderDetailId = od.OrderDetailId) AS t
    GROUP BY od.DrinkId, od.DrinkName
    HAVING SUM(CASE WHEN od.CostStatus <> 1 OR od.TotalCogs IS NULL THEN 1 ELSE 0 END) = 0
    ORDER BY ConfirmedMarginRate ASC, TotalSold DESC, od.DrinkId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Inventory_IngredientConsumptionTrend
    @FromDate datetime2, @ToDate datetime2, @StoreIds nvarchar(max),
    @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SET @Granularity = UPPER(LTRIM(RTRIM(COALESCE(@Granularity, 'DAY'))));
    IF @Granularity NOT IN ('HOUR', 'DAY', 'WEEK', 'MONTH')
        THROW 50002, 'Invalid granularity.', 1;
    SELECT TOP (ISNULL(NULLIF(@Top, 0), 10))
           dbo.ufn_AnalyticsBucketStart(it.CreatedAt, @Granularity) AS BucketDate,
           si.StoreId, si.IngredientId, i.Name AS IngredientName,
           SUM(ABS(it.Quantity)) AS ConsumedQuantity,
           COALESCE(SUM(ABS(it.TotalCost)), 0) AS ConfirmedCost,
           COUNT_BIG(it.InventoryTransactionId) AS TransactionCount,
           'AVAILABLE' AS DataStatus
    FROM dbo.InventoryTransactions AS it
    INNER JOIN dbo.StoreInventories AS si ON si.StoreInventoryId = it.StoreInventoryId
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = si.StoreId
    LEFT JOIN dbo.Ingredients AS i ON i.IngredientId = si.IngredientId
    WHERE si.IngredientId IS NOT NULL
      AND it.Type = 7
      AND it.CreatedAt >= @FromDate
      AND it.CreatedAt < @ToDate
    GROUP BY dbo.ufn_AnalyticsBucketStart(it.CreatedAt, @Granularity),
             si.StoreId, si.IngredientId, i.Name
    ORDER BY BucketDate, ConfirmedCost DESC;
END;
GO
