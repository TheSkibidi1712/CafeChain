use master
go

use CafeChain
go

/*
    Deprecated standalone dashboard seed.

    Dashboard v1.3 fixtures now live in Batch 13 of Scripts/SeedAll.sql so the
    product, supplier, inventory, POS, procurement and workforce data share one
    deterministic and idempotent contract.

    This compatibility script is intentionally read-only. Run SeedAll.sql first.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() IS NULL
    THROW 53200, N'No database is selected.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.Orders WHERE Source=N'DEMO_DASHBOARD_V13')
    THROW 53201, N'Dashboard v1.3 fixtures are missing. Run Scripts/SeedAll.sql.', 1;

IF EXISTS
(
    SELECT 1
    FROM dbo.Payments AS p
    INNER JOIN dbo.Orders AS o ON o.OrderId=p.OrderId
    WHERE o.Source=N'DEMO_DASHBOARD_V13' AND p.CashSessionId IS NOT NULL
)
    THROW 53202, N'Dashboard v1.3 payments must not depend on CashSessions.', 1;

IF EXISTS
(
    SELECT 1
    FROM dbo.StaffShifts AS ss
    INNER JOIN dbo.StaffShiftStatuses AS status ON status.StaffShiftStatusId=ss.StatusId
    INNER JOIN dbo.Staffs AS st ON st.StaffId=ss.StaffId
    WHERE st.StoreId=1
      AND ss.WorkDate>='2026-01-15' AND ss.WorkDate<'2026-01-18'
      AND status.Code NOT IN (N'SCHEDULED',N'CANCELLED')
)
    THROW 53203, N'Dashboard v1.3 schedules only support SCHEDULED/CANCELLED.', 1;

SELECT N'DEMO_DASHBOARD_V13' AS SeedMarker,
       (SELECT COUNT(*) FROM dbo.Orders WHERE Source=N'DEMO_DASHBOARD_V13') AS DemoOrders,
       (SELECT COUNT(*) FROM dbo.WorkShifts
        WHERE StoreId=1 AND StartTime IN ('2026-01-15T06:00:00','2026-01-15T12:00:00','2026-01-16T06:00:00','2026-01-18T06:00:00')) AS DemoWorkShifts,
       (SELECT COUNT(*) FROM dbo.PurchaseOrders WHERE Note=N'DEMO_DASHBOARD_V13') AS DemoPurchaseOrders,
       (SELECT COUNT(*) FROM dbo.SupplierReceiptIssues WHERE Description=N'DEMO_DASHBOARD_V13 supplier issue') AS DemoSupplierIssues;
GO
