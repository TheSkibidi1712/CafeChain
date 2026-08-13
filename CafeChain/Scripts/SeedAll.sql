/* Run against the database selected by the caller/SSMS connection.
   Never silently switch to a similarly named production database, and never
   allow demo/default seed data to be written to a SQL Server system database. */
use CafeChain
go

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ============================================================
   VIETNAM TWO-TIER LOCATION PREREQUISITE
   Run Scripts/SeedDataDiaChi.sql after migrations and before SeedAll.
   ============================================================ */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;



    DECLARE @DemoProvinceId int=(SELECT ProvinceId FROM dbo.Provinces WHERE RTRIM(Code)=N'79' AND IsActive=1);
    DECLARE @ThuDauMotWardId int=(SELECT WardId FROM dbo.Wards WHERE RTRIM(Code)=N'25747' AND ProvinceId=@DemoProvinceId AND IsActive=1);
    DECLARE @ThuanAnWardId int=(SELECT WardId FROM dbo.Wards WHERE RTRIM(Code)=N'25978' AND ProvinceId=@DemoProvinceId AND IsActive=1);
    DECLARE @DiAnWardId int=(SELECT WardId FROM dbo.Wards WHERE RTRIM(Code)=N'25942' AND ProvinceId=@DemoProvinceId AND IsActive=1);

    IF @DemoProvinceId IS NULL OR @ThuDauMotWardId IS NULL OR @ThuanAnWardId IS NULL OR @DiAnWardId IS NULL
        THROW 53442,N'SEEDALL_LOCATION: thiếu business key 79/25747/25978/25942 trong catalog địa chỉ.',1;

    UPDATE dbo.Stores SET ProvinceId=@DemoProvinceId,WardId=@ThuDauMotWardId WHERE Name=N'CafeChain Thủ Dầu Một';
    UPDATE dbo.Stores SET ProvinceId=@DemoProvinceId,WardId=@ThuanAnWardId WHERE Name=N'CafeChain Thuận An';
    UPDATE dbo.Stores SET ProvinceId=@DemoProvinceId,WardId=@DiAnWardId WHERE Name=N'CafeChain Dĩ An';

    IF NOT EXISTS(SELECT 1 FROM dbo.Stores WHERE Name=N'CafeChain Thủ Dầu Một' AND ProvinceId=@DemoProvinceId AND WardId=@ThuDauMotWardId)
       OR NOT EXISTS(SELECT 1 FROM dbo.Stores WHERE Name=N'CafeChain Thuận An' AND ProvinceId=@DemoProvinceId AND WardId=@ThuanAnWardId)
       OR NOT EXISTS(SELECT 1 FROM dbo.Stores WHERE Name=N'CafeChain Dĩ An' AND ProvinceId=@DemoProvinceId AND WardId=@DiAnWardId)
        THROW 53443,N'SEEDALL_LOCATION: không gắn được ba cửa hàng demo vào địa giới hai cấp.',1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/* ============================================================
   RBAC FOUNDATION BOOTSTRAP
   Scripts/SeedAll.sql is authoritative for RBAC defaults. EF creates
   the schema only, so a fresh database has no Role, PermissionGroup,
   Permission or AccountRole foundation rows.

   This batch is additive and idempotent:
   - inserts only missing fixed defaults;
   - rejects identity/code/name conflicts;
   - never deletes custom role/permission/account assignments.
   It must run before every demo/POS batch that resolves a role.
   ============================================================ */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.PermissionGroups',N'U') IS NULL
       OR OBJECT_ID(N'dbo.Permissions',N'U') IS NULL
       OR OBJECT_ID(N'dbo.Roles',N'U') IS NULL
       OR OBJECT_ID(N'dbo.AccountRoles',N'U') IS NULL
       OR OBJECT_ID(N'dbo.Accounts',N'U') IS NULL
        THROW 53690,N'RBAC_FOUNDATION: thiếu bảng RBAC hoặc Accounts bắt buộc.',1;

    DECLARE @FoundationPermissionGroups TABLE
    (
        PermissionGroupId int NOT NULL PRIMARY KEY,
        Code nvarchar(50) NOT NULL UNIQUE,
        Name nvarchar(150) NOT NULL UNIQUE,
        DisplayOrder int NOT NULL,
        Active bit NOT NULL
    );
    INSERT @FoundationPermissionGroups VALUES
      (1,N'DRINK',N'Quản lý đồ uống',1,1),
      (2,N'TOPPING',N'Quản lý Topping',2,1),
      (3,N'ORDER',N'Quản lý đơn hàng',3,1),
      (4,N'CUSTOMER',N'Quản lý khách hàng',4,1),
      (5,N'SYSTEM',N'Hệ thống',999,1);

    IF EXISTS
    (
        SELECT 1
        FROM @FoundationPermissionGroups x
        JOIN dbo.PermissionGroups g
          ON g.PermissionGroupId=x.PermissionGroupId OR g.Code=x.Code OR g.Name=x.Name
        WHERE g.PermissionGroupId<>x.PermissionGroupId
           OR g.Code<>x.Code OR g.Name<>x.Name
           OR g.DisplayOrder<>x.DisplayOrder OR g.Active<>x.Active
    )
        THROW 53691,N'RBAC_FOUNDATION: PermissionGroup nền xung đột ID, Code, Name hoặc contract.',1;

    SET IDENTITY_INSERT dbo.PermissionGroups ON;
    INSERT dbo.PermissionGroups(PermissionGroupId,Code,Name,DisplayOrder,Active)
    SELECT x.PermissionGroupId,x.Code,x.Name,x.DisplayOrder,x.Active
    FROM @FoundationPermissionGroups x
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.PermissionGroups g
        WHERE g.PermissionGroupId=x.PermissionGroupId
    );
    SET IDENTITY_INSERT dbo.PermissionGroups OFF;

    DECLARE @FoundationRoles TABLE
    (
        RoleId int NOT NULL PRIMARY KEY,
        Name nvarchar(100) NOT NULL UNIQUE,
        Active bit NOT NULL,
        IsStoreLevel bit NOT NULL,
        CreatedAt datetime2 NOT NULL
    );
    INSERT @FoundationRoles VALUES
      (1,N'Chủ doanh nghiệp',1,0,'2026-01-01'),
      (2,N'Quản lý vùng',1,0,'2026-01-01'),
      (3,N'Quản lý chi nhánh',1,1,'2026-01-01'),
      (4,N'Nhân viên bán hàng',1,1,'2026-01-01'),
      (5,N'Kế toán/kho',1,1,'2026-01-01'),
      (6,N'Quản trị hệ thống',1,0,'2026-01-01'),
      (8,N'Ca trưởng',1,1,'2026-01-01');

    IF EXISTS
    (
        SELECT 1
        FROM @FoundationRoles x
        JOIN dbo.Roles r ON r.RoleId=x.RoleId OR r.Name=x.Name
        WHERE r.RoleId<>x.RoleId OR r.Name<>x.Name
           OR r.Active<>x.Active OR r.IsStoreLevel<>x.IsStoreLevel
    )
        THROW 53692,N'RBAC_FOUNDATION: Role nền xung đột ID, Name hoặc contract.',1;

    SET IDENTITY_INSERT dbo.Roles ON;
    INSERT dbo.Roles(RoleId,Name,Active,IsStoreLevel,CreatedAt)
    SELECT x.RoleId,x.Name,x.Active,x.IsStoreLevel,x.CreatedAt
    FROM @FoundationRoles x
    WHERE NOT EXISTS(SELECT 1 FROM dbo.Roles r WHERE r.RoleId=x.RoleId);
    SET IDENTITY_INSERT dbo.Roles OFF;

    DECLARE @FoundationPermissions TABLE
    (
        PermissionId int NOT NULL PRIMARY KEY,
        PermissionGroupId int NOT NULL,
        Code nvarchar(100) NOT NULL UNIQUE,
        Name nvarchar(200) NOT NULL,
        Action nvarchar(50) NOT NULL,
        Description nvarchar(500) NULL,
        Active bit NOT NULL,
        CreatedAt datetime2 NOT NULL
    );
    INSERT @FoundationPermissions VALUES
      (1,1,N'Drink.View',N'Xem đồ uống',N'View',N'Xem danh sách đồ uống',1,'2025-01-01'),
      (2,1,N'Drink.Create',N'Thêm đồ uống',N'Create',N'Tạo mới đồ uống',1,'2025-01-01'),
      (3,1,N'Drink.Update',N'Cập nhật đồ uống',N'Update',N'Cập nhật thông tin đồ uống',1,'2025-01-01'),
      (4,1,N'Drink.Delete',N'Xóa đồ uống',N'Delete',N'Xóa hoặc vô hiệu đồ uống',1,'2025-01-01'),
      (27,5,N'System.Permission.Manage',N'Quản lý phân quyền',N'Manage',N'Xem danh sách bảng phân quyền',1,'2025-01-01');

    IF EXISTS
    (
        SELECT 1
        FROM @FoundationPermissions x
        JOIN dbo.Permissions p
          ON p.PermissionId=x.PermissionId OR p.Code=x.Code
             OR (p.PermissionGroupId=x.PermissionGroupId AND p.Action=x.Action)
        WHERE p.PermissionId<>x.PermissionId
           OR p.PermissionGroupId<>x.PermissionGroupId
           OR p.Code<>x.Code OR p.Name<>x.Name OR p.Action<>x.Action
           OR ISNULL(p.Description,N'')<>ISNULL(x.Description,N'')
           OR p.CreatedAt<>x.CreatedAt
    )
        THROW 53693,N'RBAC_FOUNDATION: Permission nền xung đột ID, Code hoặc Group/Action.',1;

    SET IDENTITY_INSERT dbo.Permissions ON;
    INSERT dbo.Permissions
    (PermissionId,PermissionGroupId,Code,Name,Action,Description,Active,CreatedAt)
    SELECT x.PermissionId,x.PermissionGroupId,x.Code,x.Name,x.Action,x.Description,x.Active,x.CreatedAt
    FROM @FoundationPermissions x
    WHERE NOT EXISTS(SELECT 1 FROM dbo.Permissions p WHERE p.PermissionId=x.PermissionId);
    SET IDENTITY_INSERT dbo.Permissions OFF;

    DECLARE @FoundationAccountRoles TABLE
    (
        AccountId int NOT NULL,
        RoleId int NOT NULL,
        PRIMARY KEY(AccountId,RoleId)
    );
    INSERT @FoundationAccountRoles VALUES
      (1,1),(2,2),(3,3),(4,4),(5,5),(6,6),(15,8);

    INSERT dbo.AccountRoles(AccountId,RoleId)
    SELECT x.AccountId,x.RoleId
    FROM @FoundationAccountRoles x
    JOIN dbo.Accounts a ON a.AccountId=x.AccountId
    JOIN dbo.Roles r ON r.RoleId=x.RoleId
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.AccountRoles ar
        WHERE ar.AccountId=x.AccountId AND ar.RoleId=x.RoleId
    );

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    BEGIN TRY SET IDENTITY_INSERT dbo.PermissionGroups OFF; END TRY BEGIN CATCH END CATCH;
    BEGIN TRY SET IDENTITY_INSERT dbo.Permissions OFF; END TRY BEGIN CATCH END CATCH;
    BEGIN TRY SET IDENTITY_INSERT dbo.Roles OFF; END TRY BEGIN CATCH END CATCH;
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/* ================================================================
   BATCH 18 - BTP OUTPUT SNAPSHOTS + INVENTORY PROCUREMENT V2

   Contract:
   - Backfill the 71 SeedAll-owned production runs with immutable output
     snapshots while retaining ContractVersion 1 and legacy run counts.
   - Replace SeedAll ADJUSTMENT_IN opening/buffer documents with completed
     PurchaseOrder -> confirmed BranchReceipt -> BRANCH_RECEIPT_IN evidence.
   - Replace the three manual adjustment-out lines with one EXPORT, one
     STOCK_TAKE and one WASTE document without changing final on-hand stock.
   - Remove only the unused SeedAll StockTakeSession fixture.
   - A v2 marker makes the upgrade safe on both clean and v1 databases.
   ================================================================ */
CREATE PROCEDURE #SeedAllInventoryProcurementV2
AS
BEGIN
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.PreparedItems',N'U') IS NULL
       OR OBJECT_ID(N'dbo.Recipes',N'U') IS NULL
       OR OBJECT_ID(N'dbo.RecipeDetails',N'U') IS NULL
       OR OBJECT_ID(N'dbo.ProductionRuns',N'U') IS NULL
       OR OBJECT_ID(N'dbo.PurchaseOrders',N'U') IS NULL
       OR OBJECT_ID(N'dbo.PurchaseOrderLines',N'U') IS NULL
       OR OBJECT_ID(N'dbo.BranchReceipts',N'U') IS NULL
       OR OBJECT_ID(N'dbo.BranchReceiptLines',N'U') IS NULL
       OR OBJECT_ID(N'dbo.PurchaseOrderReceiptPostings',N'U') IS NULL
       OR OBJECT_ID(N'dbo.InventoryDocuments',N'U') IS NULL
       OR OBJECT_ID(N'dbo.InventoryDocumentDetails',N'U') IS NULL
       OR OBJECT_ID(N'dbo.InventoryDocumentSnapshots',N'U') IS NULL
       OR OBJECT_ID(N'dbo.InventoryDocumentSnapshotDetails',N'U') IS NULL
       OR OBJECT_ID(N'dbo.InventoryTransactions',N'U') IS NULL
       OR OBJECT_ID(N'dbo.InventoryCostLayers',N'U') IS NULL
       OR OBJECT_ID(N'dbo.InventoryCostAllocations',N'U') IS NULL
       OR OBJECT_ID(N'dbo.StockTakeSessions',N'U') IS NULL
       OR OBJECT_ID(N'dbo.StockTakeDetails',N'U') IS NULL
       OR OBJECT_ID(N'dbo.SystemSettings',N'U') IS NULL
        THROW 53640,N'SEEDALL_INVENTORY_PROCUREMENT_V2: schema thiếu bảng bắt buộc.',1;

    /* ------------------------------------------------------------
       18.1 Canonical per-batch output contract for eleven active BTPs.
       ------------------------------------------------------------ */
    DECLARE @BtpOutputContract TABLE
    (
        PreparedItemCode nvarchar(100) NOT NULL PRIMARY KEY,
        ExpectedOutput decimal(18,5) NOT NULL,
        ExpectedUnitCode nvarchar(50) NOT NULL
    );
    INSERT @BtpOutputContract(PreparedItemCode,ExpectedOutput,ExpectedUnitCode) VALUES
    (N'DEMO_PREP_VIET_COFFEE',1000,N'ml'),
    (N'DEMO_PREP_ESPRESSO',600,N'ml'),
    (N'DEMO_PREP_BLACK_TEA',2000,N'ml'),
    (N'DEMO_PREP_OOLONG_TEA',2000,N'ml'),
    (N'DEMO_PREP_SUGAR_SYRUP',1500,N'ml'),
    (N'DEMO_PREP_SALTED_CREAM',1000,N'ml'),
    (N'DEMO_PREP_CHEESE_CREAM',1000,N'ml'),
    (N'DEMO_PREP_BLACK_PEARL',40,N'DEMO_PORTION'),
    (N'DEMO_PREP_ALOE_BASE',1000,N'g'),
    (N'DEMO_PREP_COCONUT_JELLY_BASE',1000,N'g'),
    (N'DEMO_PREP_KHUC_BACH_BASE',1000,N'g');

    DECLARE @ResolvedBtp TABLE
    (
        PreparedItemId int NOT NULL PRIMARY KEY,
        RecipeId int NOT NULL UNIQUE,
        OutputQuantity decimal(18,5) NOT NULL,
        OutputUnitId int NOT NULL
    );
    INSERT @ResolvedBtp(PreparedItemId,RecipeId,OutputQuantity,OutputUnitId)
    SELECT p.PreparedItemId,r.RecipeId,r.OutputQuantity,r.OutputUnitId
    FROM @BtpOutputContract c
    JOIN dbo.PreparedItems p ON p.Code=c.PreparedItemCode AND p.Active=1
    JOIN dbo.Units pu ON pu.UnitId=p.BaseUnitId AND pu.UnitCode=c.ExpectedUnitCode
    JOIN dbo.Recipes r ON r.PreparedItemId=p.PreparedItemId
        AND r.Active=1 AND r.Status=N'Active'
        AND r.OutputQuantity=c.ExpectedOutput
        AND r.OutputUnitId=p.BaseUnitId;

    IF (SELECT COUNT(*) FROM @ResolvedBtp)<>11
       OR EXISTS
       (
           SELECT p.PreparedItemId
           FROM dbo.PreparedItems p
           JOIN @BtpOutputContract c ON c.PreparedItemCode=p.Code
           JOIN dbo.Recipes r ON r.PreparedItemId=p.PreparedItemId
               AND r.Active=1 AND r.Status=N'Active'
           GROUP BY p.PreparedItemId
           HAVING COUNT(*)<>1
       )
       OR EXISTS
       (
           SELECT 1
           FROM @ResolvedBtp b
           WHERE NOT EXISTS
           (
               SELECT 1 FROM dbo.RecipeDetails d
               WHERE d.RecipeId=b.RecipeId AND d.Quantity>0
           )
       )
        THROW 53641,N'SEEDALL_BTP_OUTPUT_V2: thiếu BTP, active recipe duy nhất, output/base unit hoặc thành phần BOM.',1;

    DECLARE @SeedProductionRuns TABLE(ProductionRunId int NOT NULL PRIMARY KEY);
    INSERT @SeedProductionRuns(ProductionRunId)
    SELECT pr.ProductionRunId
    FROM dbo.ProductionRuns pr
    JOIN @ResolvedBtp b ON b.RecipeId=pr.RecipeId
    WHERE pr.Notes LIKE N'DEMO opening valuation source:%'
       OR pr.Notes LIKE N'DEMO_REORDER_V14_PROD_S%';

    IF (SELECT COUNT(*) FROM @SeedProductionRuns)<>71
        THROW 53642,N'SEEDALL_BTP_OUTPUT_V2: số production run SeedAll phải đúng 71.',1;

    UPDATE pr
    SET pr.ExpectedOutputPerBatchBase=b.OutputQuantity,
        pr.ExpectedOutputBase=CONVERT(decimal(18,3),ROUND(pr.RequestedRunCount*b.OutputQuantity,3)),
        pr.OutputBaseUnitId=b.OutputUnitId,
        pr.ContractVersion=1,
        pr.PlannedBatchCount=NULL
    FROM dbo.ProductionRuns pr
    JOIN @SeedProductionRuns seedRun ON seedRun.ProductionRunId=pr.ProductionRunId
    JOIN @ResolvedBtp b ON b.RecipeId=pr.RecipeId
    WHERE pr.ExpectedOutputPerBatchBase<>b.OutputQuantity
       OR pr.ExpectedOutputPerBatchBase IS NULL
       OR pr.ExpectedOutputBase<>CONVERT(decimal(18,3),ROUND(pr.RequestedRunCount*b.OutputQuantity,3))
       OR pr.ExpectedOutputBase IS NULL
       OR pr.OutputBaseUnitId<>b.OutputUnitId
       OR pr.OutputBaseUnitId IS NULL
       OR pr.ContractVersion<>1
       OR pr.PlannedBatchCount IS NOT NULL;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.ProductionRuns pr
        JOIN @SeedProductionRuns seedRun ON seedRun.ProductionRunId=pr.ProductionRunId
        JOIN @ResolvedBtp b ON b.RecipeId=pr.RecipeId
        WHERE pr.ExpectedOutputPerBatchBase<>b.OutputQuantity
           OR pr.ExpectedOutputBase<>CONVERT(decimal(18,3),ROUND(pr.RequestedRunCount*b.OutputQuantity,3))
           OR pr.OutputBaseUnitId<>b.OutputUnitId
           OR pr.ContractVersion<>1
           OR pr.PlannedBatchCount IS NOT NULL
    ) THROW 53643,N'SEEDALL_BTP_OUTPUT_V2: production output snapshot không khớp contract.',1;

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.SystemSettings
        WHERE SettingKey=N'seedall_inventory_procurement_v2'
          AND SettingValue=N'completed'
    )
    BEGIN
        /* --------------------------------------------------------
           18.2 Capture only the four SeedAll v1 inbound documents.
           Transaction/layer identities are retained; only their source
           evidence is moved from document detail to receipt line.
           -------------------------------------------------------- */
        DECLARE @InboundSource TABLE
        (
            SourceDocumentId int NOT NULL,
            SourceDetailId int NOT NULL PRIMARY KEY,
            SourceKind nvarchar(20) NOT NULL,
            StoreId int NOT NULL,
            StaffId int NOT NULL,
            EventAt datetime2 NOT NULL,
            IngredientId int NOT NULL,
            BaseUnitId int NOT NULL,
            BaseQuantity decimal(18,3) NOT NULL,
            UnitCost decimal(18,4) NOT NULL,
            InventoryTransactionId int NOT NULL UNIQUE,
            InventoryCostLayerId int NOT NULL UNIQUE
        );

        INSERT @InboundSource
        SELECT h.InventoryDocumentId,d.InventoryDocumentDetailId,
               CASE WHEN h.RequestKey IN(N'DEMO_OPENING_STORE1_INGREDIENTS',N'DEMO_REORDER_V14_OPENING_STORE3')
                    THEN N'OPENING' ELSE N'BUFFER' END,
               h.StoreId,h.StaffId,h.DocumentDate,d.IngredientId,d.UnitId,d.BaseQuantity,d.CostPrice,
               t.InventoryTransactionId,l.InventoryCostLayerId
        FROM dbo.InventoryDocuments h
        JOIN dbo.InventoryDocumentDetails d ON d.InventoryDocumentId=h.InventoryDocumentId
        JOIN dbo.InventoryTransactions t ON t.InventoryDocumentDetailId=d.InventoryDocumentDetailId AND t.[Type]=8
        JOIN dbo.InventoryCostLayers l ON l.SourceInventoryDocumentDetailId=d.InventoryDocumentDetailId
        WHERE h.RequestKey IN
        (
            N'DEMO_OPENING_STORE1_INGREDIENTS',
            N'DEMO_REORDER_V14_OPENING_STORE3',
            N'DEMO_REORDER_V14_SALES_BUFFER_S1_ING00001',
            N'DEMO_REORDER_V14_SALES_BUFFER_S3_ING00001'
        );

        IF (SELECT COUNT(*) FROM @InboundSource WHERE SourceKind=N'OPENING' AND StoreId=1)<>50
           OR (SELECT COUNT(*) FROM @InboundSource WHERE SourceKind=N'OPENING' AND StoreId=3)<>50
           OR (SELECT COUNT(*) FROM @InboundSource WHERE SourceKind=N'BUFFER')<>2
           OR EXISTS(SELECT 1 FROM @InboundSource WHERE BaseQuantity<=0 OR UnitCost<=0)
            THROW 53644,N'SEEDALL_INVENTORY_PROCUREMENT_V2: fixture inbound v1 phải có 50+50 opening và 2 buffer lines.',1;

        IF EXISTS
        (
            SELECT 1
            FROM @InboundSource src
            WHERE EXISTS(SELECT 1 FROM dbo.InventoryCostAllocations a WHERE a.InventoryDocumentDetailId=src.SourceDetailId)
               OR EXISTS(SELECT 1 FROM dbo.InventoryNegativeCostGaps g WHERE g.InventoryDocumentDetailId=src.SourceDetailId)
               OR EXISTS(SELECT 1 FROM dbo.InventoryNegativeApprovals a WHERE a.InventoryDocumentId=src.SourceDocumentId)
        ) THROW 53645,N'SEEDALL_INVENTORY_PROCUREMENT_V2: opening/buffer v1 có tham chiếu ngoài contract, không tự xóa.',1;

        DECLARE @ProcurementSource TABLE
        (
            SourceDetailId int NOT NULL PRIMARY KEY,
            SourceDocumentId int NOT NULL,
            SourceKind nvarchar(20) NOT NULL,
            StoreId int NOT NULL,
            StaffId int NOT NULL,
            EventAt datetime2 NOT NULL,
            IngredientId int NOT NULL,
            BaseUnitId int NOT NULL,
            BaseQuantity decimal(18,3) NOT NULL,
            UnitCost decimal(18,4) NOT NULL,
            InventoryTransactionId int NOT NULL,
            InventoryCostLayerId int NOT NULL,
            IngredientSupplierId int NOT NULL,
            SupplierId int NOT NULL,
            PackageUnitId int NOT NULL,
            PackageQuantity decimal(18,5) NOT NULL,
            PackageBaseQuantity decimal(18,5) NOT NULL,
            PackageCount decimal(18,5) NOT NULL,
            OfferPackagePrice decimal(18,2) NOT NULL,
            ActualPackagePrice decimal(18,2) NOT NULL,
            PoCode nvarchar(50) NOT NULL,
            ReceiptCode nvarchar(50) NOT NULL,
            ReceiptKey nvarchar(100) NOT NULL,
            LineKey nvarchar(100) NOT NULL
        );

        INSERT @ProcurementSource
        SELECT src.SourceDetailId,src.SourceDocumentId,src.SourceKind,src.StoreId,src.StaffId,src.EventAt,
               src.IngredientId,src.BaseUnitId,src.BaseQuantity,src.UnitCost,
               src.InventoryTransactionId,src.InventoryCostLayerId,
               offer.IngredientSupplierId,offer.SupplierId,offer.UnitId,offer.PackageQuantity,
               offer.PackageQuantity*factor.FactorToBase,
               CONVERT(decimal(18,5),src.BaseQuantity/(offer.PackageQuantity*factor.FactorToBase)),
               offer.CurrentPrice,
               CONVERT(decimal(18,2),ROUND(src.UnitCost*offer.PackageQuantity*factor.FactorToBase,2)),
               CONCAT(N'SIV2-PO-',src.SourceKind,N'-S',src.StoreId,N'-SUP',offer.SupplierId),
               CONCAT(N'SIV2-BR-',src.SourceKind,N'-S',src.StoreId,N'-SUP',offer.SupplierId),
               CONCAT(N'SEEDALL_INV_V2_',src.SourceKind,N'_S',src.StoreId,N'_SUP',offer.SupplierId),
               CONCAT(N'SEEDALL_INV_V2_',src.SourceKind,N'_S',src.StoreId,N'_ING',RIGHT(N'00000'+CONVERT(nvarchar(10),src.IngredientId),5))
        FROM @InboundSource src
        JOIN dbo.Ingredients ingredient ON ingredient.IngredientId=src.IngredientId
            AND ingredient.Active=1 AND ingredient.BaseUnitId=src.BaseUnitId
        JOIN dbo.IngredientSuppliers offer ON offer.IngredientId=src.IngredientId
            AND offer.Active=1 AND offer.IsPrimary=1
            AND offer.PackageQuantity>0 AND offer.CurrentPrice>0
        OUTER APPLY
        (
            SELECT CONVERT(decimal(18,8),CASE
                WHEN offer.UnitId=ingredient.BaseUnitId THEN 1
                ELSE
                (
                    SELECT TOP(1) uc.ToQuantity/NULLIF(uc.FromQuantity,0)
                    FROM dbo.UnitConversions uc
                    WHERE uc.IngredientId=ingredient.IngredientId
                      AND uc.FromUnitId=offer.UnitId
                      AND uc.ToUnitId=ingredient.BaseUnitId
                      AND uc.Active=1
                    ORDER BY uc.UnitConversionId
                ) END) FactorToBase
        ) factor
        WHERE factor.FactorToBase>0
          AND EXISTS
          (
              SELECT 1 FROM dbo.SupplierStores ss
              WHERE ss.SupplierId=offer.SupplierId AND ss.StoreId=src.StoreId AND ss.Active=1
          );

        IF (SELECT COUNT(*) FROM @ProcurementSource)<>102
           OR EXISTS(SELECT 1 FROM @ProcurementSource WHERE PackageBaseQuantity<=0 OR PackageCount<=0 OR ActualPackagePrice<=0)
            THROW 53646,N'SEEDALL_INVENTORY_PROCUREMENT_V2: không resolve đủ primary supplier/package/conversion/store scope.',1;

        INSERT dbo.PurchaseOrders
        (Code,StoreId,SupplierId,[Status],OrderDate,ExpectedDeliveryAtUtc,CreatedByStaffId,
         ApprovedByStaffId,SentByStaffId,CreatedAtUtc,UpdatedAtUtc,ApprovedAtUtc,SentAtUtc,
         CompletedAtUtc,CancelledAtUtc,Note)
        SELECT g.PoCode,g.StoreId,g.SupplierId,N'COMPLETED',g.EventAt,g.EventAt,g.StaffId,
               g.StaffId,g.StaffId,g.EventAt,g.EventAt,g.EventAt,g.EventAt,g.EventAt,NULL,
               N'SEEDALL_INVENTORY_PROCUREMENT_V2'
        FROM
        (
            SELECT PoCode,StoreId,SupplierId,MIN(EventAt) EventAt,MIN(StaffId) StaffId
            FROM @ProcurementSource
            GROUP BY PoCode,StoreId,SupplierId
        ) g
        WHERE NOT EXISTS(SELECT 1 FROM dbo.PurchaseOrders po WHERE po.Code=g.PoCode);

        INSERT dbo.PurchaseOrderLines
        (PurchaseOrderId,RestockRequestId,PurchaseAdviceLineId,IngredientId,IngredientSupplierId,
         PackageUnitIdSnapshot,PackageQuantitySnapshot,PackagePriceSnapshot,PackageCount,
         PurchaseMode,OrderedPackageCount,OrderedBaseQuantity,OrderedPackQuantity,
         PackSizeProcurementQuantity,ProcurementUnitId,OrderedProcurementQuantity,
         UnitPricePerPackage,UnitPricePerProcurementUnit,RoundingSurplusProcurementQuantity,
         AcceptedPackQuantity,AcceptedProcurementQuantity,ClosedProcurementQuantity,
         InventoryPostingBaseQuantity,InventoryBaseUnitId,ProcurementToInventoryFactor,
         ClosedRemainingQuantity,PromisedLeadTimeDaysSnapshot,Note)
        SELECT po.PurchaseOrderId,NULL,NULL,src.IngredientId,src.IngredientSupplierId,
               src.PackageUnitId,src.PackageQuantity,src.OfferPackagePrice,CEILING(src.PackageCount),
               N'Packaged',CEILING(src.PackageCount),src.BaseQuantity,CEILING(src.PackageCount),
               NULL,NULL,NULL,src.OfferPackagePrice,NULL,0,
               src.PackageCount,NULL,0,src.BaseQuantity,src.BaseUnitId,src.PackageBaseQuantity,
               0,COALESCE(offer.LeadTimeDays,0),src.LineKey
        FROM @ProcurementSource src
        JOIN dbo.PurchaseOrders po ON po.Code=src.PoCode
        JOIN dbo.IngredientSuppliers offer ON offer.IngredientSupplierId=src.IngredientSupplierId
        WHERE NOT EXISTS
        (
            SELECT 1 FROM dbo.PurchaseOrderLines line
            WHERE line.PurchaseOrderId=po.PurchaseOrderId AND line.Note=src.LineKey
        );

        INSERT dbo.BranchReceipts
        (ReceiptCode,StoreId,SupplierId,PurchaseOrderId,SourceInventoryTransferId,[Status],ReceiptKey,
         ReferenceNumber,ReceivedAt,ReceivedByStaffId,ConfirmedAt,ConfirmedByStaffId,Notes,
         CreatedAt,CreatedByStaffId)
        SELECT g.ReceiptCode,g.StoreId,g.SupplierId,po.PurchaseOrderId,NULL,N'CONFIRMED',g.ReceiptKey,
               CONCAT(N'SIV2-',g.StoreId,N'-',g.SupplierId),g.EventAt,g.StaffId,g.EventAt,g.StaffId,
               N'SEEDALL_INVENTORY_PROCUREMENT_V2',g.EventAt,g.StaffId
        FROM
        (
            SELECT ReceiptCode,ReceiptKey,PoCode,StoreId,SupplierId,MIN(EventAt) EventAt,MIN(StaffId) StaffId
            FROM @ProcurementSource
            GROUP BY ReceiptCode,ReceiptKey,PoCode,StoreId,SupplierId
        ) g
        JOIN dbo.PurchaseOrders po ON po.Code=g.PoCode
        WHERE NOT EXISTS(SELECT 1 FROM dbo.BranchReceipts br WHERE br.ReceiptCode=g.ReceiptCode);

        INSERT dbo.BranchReceiptLines
        (BranchReceiptId,RestockRequestId,PurchaseOrderLineId,SourceInventoryTransferDetailId,
         SourceTransferCostAllocationId,RestockRequestFulfillmentId,IngredientId,PreparedItemId,RecipeId,
         InputQuantity,InputUnitId,ReceivedBaseQuantity,RejectedBaseQuantity,ReceivedPackQuantity,
         AcceptedPackQuantity,ReceivedProcurementQuantity,RejectedProcurementQuantity,
         AcceptedProcurementQuantity,InventoryPostingBaseQuantity,ProcurementUnitId,InventoryBaseUnitId,
         ProcurementToInventoryFactor,PurchaseMode,RejectionReason,RejectionIssueType,BaseUnitId,
         SupplierId,IngredientSupplierId,ActualPackagePrice,PackageQuantitySnapshot,
         PackageUnitIdSnapshot,BaseUnitCostSnapshot,LineTotalCost,InventoryTransactionId,CreatedAt)
        SELECT br.BranchReceiptId,NULL,line.PurchaseOrderLineId,NULL,NULL,NULL,src.IngredientId,NULL,NULL,
               src.PackageCount,src.PackageUnitId,src.BaseQuantity,0,src.PackageCount,src.PackageCount,
               NULL,NULL,NULL,src.BaseQuantity,NULL,src.BaseUnitId,src.PackageBaseQuantity,N'Packaged',
               NULL,NULL,src.BaseUnitId,src.SupplierId,src.IngredientSupplierId,src.ActualPackagePrice,
               src.PackageQuantity,src.PackageUnitId,src.UnitCost,ROUND(src.BaseQuantity*src.UnitCost,2),
               NULL,src.EventAt
        FROM @ProcurementSource src
        JOIN dbo.BranchReceipts br ON br.ReceiptCode=src.ReceiptCode
        JOIN dbo.PurchaseOrders po ON po.PurchaseOrderId=br.PurchaseOrderId
        JOIN dbo.PurchaseOrderLines line ON line.PurchaseOrderId=po.PurchaseOrderId AND line.Note=src.LineKey
        WHERE NOT EXISTS
        (
            SELECT 1 FROM dbo.BranchReceiptLines brl
            WHERE brl.BranchReceiptId=br.BranchReceiptId AND brl.PurchaseOrderLineId=line.PurchaseOrderLineId
        );

        INSERT dbo.PurchaseOrderReceiptPostings
        (PurchaseOrderLineId,BranchReceiptLineId,AcceptedBaseQuantity,RejectedBaseQuantity,
         AcceptedProcurementQuantity,RejectedProcurementQuantity,InventoryPostingBaseQuantity,
         ProcurementUnitId,InventoryBaseUnitId,ProcurementToInventoryFactor,PurchaseMode,
         CreatedByStaffId,CreatedAtUtc)
        SELECT line.PurchaseOrderLineId,brl.BranchReceiptLineId,src.BaseQuantity,0,NULL,NULL,
               src.BaseQuantity,NULL,src.BaseUnitId,src.PackageBaseQuantity,N'Packaged',src.StaffId,src.EventAt
        FROM @ProcurementSource src
        JOIN dbo.BranchReceipts br ON br.ReceiptCode=src.ReceiptCode
        JOIN dbo.PurchaseOrders po ON po.PurchaseOrderId=br.PurchaseOrderId
        JOIN dbo.PurchaseOrderLines line ON line.PurchaseOrderId=po.PurchaseOrderId AND line.Note=src.LineKey
        JOIN dbo.BranchReceiptLines brl ON brl.BranchReceiptId=br.BranchReceiptId
            AND brl.PurchaseOrderLineId=line.PurchaseOrderLineId
        WHERE NOT EXISTS
        (
            SELECT 1 FROM dbo.PurchaseOrderReceiptPostings posting
            WHERE posting.BranchReceiptLineId=brl.BranchReceiptLineId
        );

        UPDATE tx
        SET tx.[Type]=14,
            tx.StockStatus=1,
            tx.InventoryDocumentId=NULL,
            tx.InventoryDocumentDetailId=NULL,
            tx.BranchReceiptLineId=brl.BranchReceiptLineId
        FROM dbo.InventoryTransactions tx
        JOIN @ProcurementSource src ON src.InventoryTransactionId=tx.InventoryTransactionId
        JOIN dbo.BranchReceipts br ON br.ReceiptCode=src.ReceiptCode
        JOIN dbo.PurchaseOrders po ON po.PurchaseOrderId=br.PurchaseOrderId
        JOIN dbo.PurchaseOrderLines line ON line.PurchaseOrderId=po.PurchaseOrderId AND line.Note=src.LineKey
        JOIN dbo.BranchReceiptLines brl ON brl.BranchReceiptId=br.BranchReceiptId
            AND brl.PurchaseOrderLineId=line.PurchaseOrderLineId;

        UPDATE layer
        SET layer.SourceInventoryDocumentDetailId=NULL,
            layer.SourceBranchReceiptLineId=brl.BranchReceiptLineId
        FROM dbo.InventoryCostLayers layer
        JOIN @ProcurementSource src ON src.InventoryCostLayerId=layer.InventoryCostLayerId
        JOIN dbo.BranchReceipts br ON br.ReceiptCode=src.ReceiptCode
        JOIN dbo.PurchaseOrders po ON po.PurchaseOrderId=br.PurchaseOrderId
        JOIN dbo.PurchaseOrderLines line ON line.PurchaseOrderId=po.PurchaseOrderId AND line.Note=src.LineKey
        JOIN dbo.BranchReceiptLines brl ON brl.BranchReceiptId=br.BranchReceiptId
            AND brl.PurchaseOrderLineId=line.PurchaseOrderLineId;

        UPDATE brl
        SET brl.InventoryTransactionId=src.InventoryTransactionId
        FROM dbo.BranchReceiptLines brl
        JOIN dbo.BranchReceipts br ON br.BranchReceiptId=brl.BranchReceiptId
        JOIN dbo.PurchaseOrders po ON po.PurchaseOrderId=br.PurchaseOrderId
        JOIN dbo.PurchaseOrderLines line ON line.PurchaseOrderLineId=brl.PurchaseOrderLineId
        JOIN @ProcurementSource src ON src.ReceiptCode=br.ReceiptCode AND src.LineKey=line.Note;

        /* Remove the four inbound documents only after all durable references
           have been re-homed to BranchReceiptLine. */
        DELETE snapshot
        FROM dbo.InventoryDocumentSnapshots snapshot
        JOIN (SELECT DISTINCT SourceDocumentId FROM @InboundSource) sourceDoc
          ON sourceDoc.SourceDocumentId=snapshot.InventoryDocumentId;

        DELETE document
        FROM dbo.InventoryDocuments document
        JOIN (SELECT DISTINCT SourceDocumentId FROM @InboundSource) sourceDoc
          ON sourceDoc.SourceDocumentId=document.InventoryDocumentId;

        /* --------------------------------------------------------
           18.3 Replace one manual adjustment-out document with the
           supported EXPORT, STOCK_TAKE and WASTE workflows.
           -------------------------------------------------------- */
        DECLARE @OldAdjustmentDocumentId int=
        (
            SELECT InventoryDocumentId FROM dbo.InventoryDocuments
            WHERE RequestKey=N'SEEDALL_ADJ_OUT_20260102'
        );
        IF @OldAdjustmentDocumentId IS NULL
           OR (SELECT COUNT(*) FROM dbo.InventoryDocumentDetails WHERE InventoryDocumentId=@OldAdjustmentDocumentId)<>3
           OR EXISTS
           (
               SELECT 1 FROM dbo.InventoryDocumentDetails
               WHERE InventoryDocumentId=@OldAdjustmentDocumentId AND IngredientId NOT IN(1,2,7)
           )
            THROW 53647,N'SEEDALL_INVENTORY_PROCUREMENT_V2: adjustment-out v1 thiếu hoặc drift.',1;

        DECLARE @AdjustmentActor int=(SELECT StaffId FROM dbo.InventoryDocuments WHERE InventoryDocumentId=@OldAdjustmentDocumentId);
        DECLARE @AdjustmentStore int=(SELECT StoreId FROM dbo.InventoryDocuments WHERE InventoryDocumentId=@OldAdjustmentDocumentId);
        DECLARE @AdjustmentAt datetime2=(SELECT DocumentDate FROM dbo.InventoryDocuments WHERE InventoryDocumentId=@OldAdjustmentDocumentId);
        DECLARE @WasteDocumentId int,@StockTakeDocumentId int;

        DELETE FROM dbo.InventoryDocumentSnapshots WHERE InventoryDocumentId=@OldAdjustmentDocumentId;

        INSERT dbo.InventoryDocuments
        (Code,StoreId,StaffId,DocumentDate,[Type],[Status],RequestKey,IsProcessing,ConfirmedAt,
         ConfirmedBy,Purpose,PartnerType,PartnerId,PartnerName,SupplierId,Note,AllowNegativeStock,
         NegativeReason,TotalAmount,VatAmount,FinalAmount)
        SELECT N'SEEDALL_WASTE_20260102',@AdjustmentStore,@AdjustmentActor,DATEADD(MINUTE,10,@AdjustmentAt),
               3,3,N'SEEDALL_WASTE_20260102',0,DATEADD(MINUTE,10,@AdjustmentAt),@AdjustmentActor,
               12,0,NULL,NULL,NULL,N'Hủy nguyên liệu hư hỏng trong dữ liệu demo',0,NULL,
               d.CostAmount,0,d.CostAmount
        FROM dbo.InventoryDocumentDetails d
        WHERE d.InventoryDocumentId=@OldAdjustmentDocumentId AND d.IngredientId=7;
        SET @WasteDocumentId=CONVERT(int,SCOPE_IDENTITY());

        INSERT dbo.InventoryDocuments
        (Code,StoreId,StaffId,DocumentDate,[Type],[Status],RequestKey,IsProcessing,ConfirmedAt,
         ConfirmedBy,Purpose,PartnerType,PartnerId,PartnerName,SupplierId,Note,AllowNegativeStock,
         NegativeReason,TotalAmount,VatAmount,FinalAmount)
        VALUES(N'SEEDALL_STOCKTAKE_20260102',@AdjustmentStore,@AdjustmentActor,DATEADD(MINUTE,20,@AdjustmentAt),
               4,3,N'SEEDALL_STOCKTAKE_20260102',0,DATEADD(MINUTE,20,@AdjustmentAt),@AdjustmentActor,
               11,0,NULL,NULL,NULL,N'Kiểm kê thực tế thấp hơn hệ thống 100 base unit',0,NULL,0,0,0);
        SET @StockTakeDocumentId=CONVERT(int,SCOPE_IDENTITY());

        UPDATE dbo.InventoryDocuments
        SET Code=N'SEEDALL_EXPORT_20260102',RequestKey=N'SEEDALL_EXPORT_20260102',
            [Type]=2,Purpose=5,Note=N'Xuất nguyên liệu phục vụ vận hành demo',
            TotalAmount=(SELECT CostAmount FROM dbo.InventoryDocumentDetails
                         WHERE InventoryDocumentId=@OldAdjustmentDocumentId AND IngredientId=1),
            VatAmount=0,
            FinalAmount=(SELECT CostAmount FROM dbo.InventoryDocumentDetails
                         WHERE InventoryDocumentId=@OldAdjustmentDocumentId AND IngredientId=1)
        WHERE InventoryDocumentId=@OldAdjustmentDocumentId;

        UPDATE dbo.InventoryDocumentDetails
        SET InventoryDocumentId=@WasteDocumentId,Note=N'SEEDALL_WASTE_DAMAGED_ING00007'
        WHERE InventoryDocumentId=@OldAdjustmentDocumentId AND IngredientId=7;

        UPDATE dbo.InventoryDocumentDetails
        SET InventoryDocumentId=@StockTakeDocumentId,
            Quantity=91100,BaseQuantity=91100,UnitPrice=0,CostPrice=0,CostAmount=0,
            Note=N'SEEDALL_STOCKTAKE_ACTUAL_ING00002',TotalAmount=0
        WHERE InventoryDocumentId=@OldAdjustmentDocumentId AND IngredientId=2;

        UPDATE dbo.InventoryDocumentDetails
        SET Note=N'SEEDALL_EXPORT_SALE_ING00001'
        WHERE InventoryDocumentId=@OldAdjustmentDocumentId AND IngredientId=1;

        UPDATE tx
        SET tx.[Type]=CASE detail.IngredientId WHEN 1 THEN 2 WHEN 2 THEN 9 WHEN 7 THEN 3 END,
            tx.StockStatus=CASE WHEN detail.IngredientId=2 THEN 5 ELSE 1 END,
            tx.InventoryDocumentId=detail.InventoryDocumentId
        FROM dbo.InventoryTransactions tx
        JOIN dbo.InventoryDocumentDetails detail ON detail.InventoryDocumentDetailId=tx.InventoryDocumentDetailId
        WHERE detail.InventoryDocumentId IN(@OldAdjustmentDocumentId,@WasteDocumentId,@StockTakeDocumentId);

        /* Immutable snapshots for the three supported documents. */
        INSERT dbo.InventoryDocumentSnapshots
        (InventoryDocumentId,[Type],Purpose,[Status],NegativeApprovalId,BeforeQty,AfterQty,
         EffectiveMaxNegativeQty,PolicyVersion,CostComplete,Code,DocumentDate,StoreName,StaffName,
         PartnerName,TotalAmount,VatAmount,FinalAmount,CreatedAt)
        SELECT document.InventoryDocumentId,document.[Type],document.Purpose,document.[Status],NULL,NULL,NULL,
               NULL,NULL,1,document.Code,document.DocumentDate,store.Name,staff.FullName,
               document.PartnerName,COALESCE(document.TotalAmount,0),COALESCE(document.VatAmount,0),
               COALESCE(document.FinalAmount,0),document.ConfirmedAt
        FROM dbo.InventoryDocuments document
        JOIN dbo.Stores store ON store.StoreId=document.StoreId
        JOIN dbo.Staffs staff ON staff.StaffId=document.StaffId
        WHERE document.InventoryDocumentId IN(@OldAdjustmentDocumentId,@WasteDocumentId,@StockTakeDocumentId)
          AND NOT EXISTS
          (
              SELECT 1 FROM dbo.InventoryDocumentSnapshots snapshot
              WHERE snapshot.InventoryDocumentId=document.InventoryDocumentId
          );

        INSERT dbo.InventoryDocumentSnapshotDetails
        (InventoryDocumentSnapshotId,ItemName,UnitName,Quantity,UnitPrice,TotalAmount)
        SELECT snapshot.InventoryDocumentSnapshotId,ingredient.Name,unit.Name,detail.Quantity,
               COALESCE(detail.UnitPrice,0),COALESCE(detail.TotalAmount,0)
        FROM dbo.InventoryDocumentSnapshots snapshot
        JOIN dbo.InventoryDocumentDetails detail ON detail.InventoryDocumentId=snapshot.InventoryDocumentId
        JOIN dbo.Ingredients ingredient ON ingredient.IngredientId=detail.IngredientId
        JOIN dbo.Units unit ON unit.UnitId=detail.UnitId
        WHERE snapshot.InventoryDocumentId IN(@OldAdjustmentDocumentId,@WasteDocumentId,@StockTakeDocumentId)
          AND NOT EXISTS
          (
              SELECT 1 FROM dbo.InventoryDocumentSnapshotDetails snapshotDetail
              WHERE snapshotDetail.InventoryDocumentSnapshotId=snapshot.InventoryDocumentSnapshotId
                AND snapshotDetail.ItemName=ingredient.Name
          );

        /* Schema-only legacy stock take is not an operational workflow. */
        DELETE detail
        FROM dbo.StockTakeDetails detail
        JOIN dbo.StockTakeSessions session ON session.StockTakeSessionId=detail.StockTakeSessionId
        WHERE session.Code=N'SEEDALL_STOCKTAKE_20260103';
        DELETE FROM dbo.StockTakeSessions WHERE Code=N'SEEDALL_STOCKTAKE_20260103';

        IF EXISTS(SELECT 1 FROM dbo.SystemSettings WHERE SettingKey=N'seedall_inventory_procurement_v2')
            UPDATE dbo.SystemSettings
            SET SettingValue=N'completed',
                Description=N'SeedAll opening/buffer inventory uses PO, BranchReceipt, BRANCH_RECEIPT_IN and FIFO evidence.'
            WHERE SettingKey=N'seedall_inventory_procurement_v2';
        ELSE
            INSERT dbo.SystemSettings(SettingKey,SettingValue,Description)
            VALUES(N'seedall_inventory_procurement_v2',N'completed',
                   N'SeedAll opening/buffer inventory uses PO, BranchReceipt, BRANCH_RECEIPT_IN and FIFO evidence.');
    END;

    /* ------------------------------------------------------------
       18.4 Replay-safe acceptance checks.
       ------------------------------------------------------------ */
    IF EXISTS
    (
        SELECT 1 FROM dbo.InventoryDocuments
        WHERE RequestKey IN
        (
            N'DEMO_OPENING_STORE1_INGREDIENTS',
            N'SEEDALL_ADJ_OUT_20260102',
            N'DEMO_REORDER_V14_OPENING_STORE3',
            N'DEMO_REORDER_V14_SALES_BUFFER_S1_ING00001',
            N'DEMO_REORDER_V14_SALES_BUFFER_S3_ING00001'
        )
    ) THROW 53648,N'SEEDALL_INVENTORY_PROCUREMENT_V2: còn chứng từ adjustment/opening v1.',1;

    IF (SELECT COUNT(*) FROM dbo.InventoryDocuments
        WHERE RequestKey IN(N'SEEDALL_EXPORT_20260102',N'SEEDALL_WASTE_20260102',N'SEEDALL_STOCKTAKE_20260102'))<>3
       OR EXISTS
       (
           SELECT 1 FROM dbo.InventoryDocuments
           WHERE RequestKey=N'SEEDALL_EXPORT_20260102' AND ([Type]<>2 OR Purpose<>5 OR [Status]<>3)
           UNION ALL
           SELECT 1 FROM dbo.InventoryDocuments
           WHERE RequestKey=N'SEEDALL_WASTE_20260102' AND ([Type]<>3 OR Purpose<>12 OR [Status]<>3)
           UNION ALL
           SELECT 1 FROM dbo.InventoryDocuments
           WHERE RequestKey=N'SEEDALL_STOCKTAKE_20260102' AND ([Type]<>4 OR Purpose<>11 OR [Status]<>3)
       )
        THROW 53649,N'SEEDALL_INVENTORY_PROCUREMENT_V2: thiếu hoặc sai EXPORT/WASTE/STOCK_TAKE contract.',1;

    IF (SELECT COUNT(*)
        FROM dbo.BranchReceiptLines line
        JOIN dbo.BranchReceipts receipt ON receipt.BranchReceiptId=line.BranchReceiptId
        WHERE receipt.Notes=N'SEEDALL_INVENTORY_PROCUREMENT_V2')<>102
       OR EXISTS
       (
           SELECT 1
           FROM dbo.BranchReceiptLines line
           JOIN dbo.BranchReceipts receipt ON receipt.BranchReceiptId=line.BranchReceiptId
           LEFT JOIN dbo.PurchaseOrders po ON po.PurchaseOrderId=receipt.PurchaseOrderId
           LEFT JOIN dbo.PurchaseOrderLines pol ON pol.PurchaseOrderLineId=line.PurchaseOrderLineId
           LEFT JOIN dbo.PurchaseOrderReceiptPostings posting ON posting.BranchReceiptLineId=line.BranchReceiptLineId
           LEFT JOIN dbo.InventoryTransactions tx ON tx.BranchReceiptLineId=line.BranchReceiptLineId
           LEFT JOIN dbo.InventoryCostLayers layer ON layer.SourceBranchReceiptLineId=line.BranchReceiptLineId
           WHERE receipt.Notes=N'SEEDALL_INVENTORY_PROCUREMENT_V2'
             AND (receipt.[Status]<>N'CONFIRMED' OR po.[Status]<>N'COMPLETED'
               OR pol.PurchaseOrderLineId IS NULL OR posting.PurchaseOrderReceiptPostingId IS NULL
               OR tx.InventoryTransactionId IS NULL OR tx.[Type]<>14
               OR tx.InventoryDocumentId IS NOT NULL OR tx.InventoryDocumentDetailId IS NOT NULL
               OR layer.InventoryCostLayerId IS NULL OR layer.SourceInventoryDocumentDetailId IS NOT NULL
               OR line.InventoryTransactionId<>tx.InventoryTransactionId)
       )
        THROW 53650,N'SEEDALL_INVENTORY_PROCUREMENT_V2: PO/receipt/posting/transaction/FIFO evidence thiếu hoặc drift.',1;

    IF EXISTS(SELECT 1 FROM dbo.StockTakeSessions WHERE Code=N'SEEDALL_STOCKTAKE_20260103')
        THROW 53651,N'SEEDALL_INVENTORY_PROCUREMENT_V2: legacy StockTakeSession seed vẫn còn.',1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.StoreInventories inventory
        WHERE inventory.StoreId=1
          AND inventory.IngredientId IS NOT NULL
          AND ABS(inventory.AvailableQty-
              (
                  SELECT COALESCE(SUM(CASE WHEN tx.[Type] IN(1,5,8,11,13,14,15)
                                           THEN tx.Quantity ELSE -tx.Quantity END),0)
                  FROM dbo.InventoryTransactions tx
                  WHERE tx.StoreInventoryId=inventory.StoreInventoryId
              ))>0.001
    ) THROW 53652,N'SEEDALL_INVENTORY_PROCUREMENT_V2: StoreInventory không cân với signed ledger.',1;

    IF EXISTS
    (
        SELECT 1 FROM dbo.InventoryCostLayers
        WHERE RemainingQuantity<0 OR RemainingQuantity>Quantity
    ) THROW 53653,N'SEEDALL_INVENTORY_PROCUREMENT_V2: FIFO remaining quantity không hợp lệ.',1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT N'SEEDALL_INVENTORY_PROCUREMENT_V2' SeedMarker,
       (SELECT COUNT(*) FROM dbo.ProductionRuns
        WHERE Notes LIKE N'DEMO opening valuation source:%' OR Notes LIKE N'DEMO_REORDER_V14_PROD_S%') SeedProductionRuns,
       (SELECT COUNT(*) FROM dbo.ProductionRuns
        WHERE (Notes LIKE N'DEMO opening valuation source:%' OR Notes LIKE N'DEMO_REORDER_V14_PROD_S%')
          AND (ExpectedOutputPerBatchBase IS NULL OR ExpectedOutputBase IS NULL OR OutputBaseUnitId IS NULL)) MissingOutputSnapshots,
       (SELECT COUNT(*) FROM dbo.BranchReceiptLines line
        JOIN dbo.BranchReceipts receipt ON receipt.BranchReceiptId=line.BranchReceiptId
        WHERE receipt.Notes=N'SEEDALL_INVENTORY_PROCUREMENT_V2') ProcurementReceiptLines,
       (SELECT COUNT(*) FROM dbo.InventoryDocuments
        WHERE RequestKey IN(N'SEEDALL_EXPORT_20260102',N'SEEDALL_WASTE_20260102',N'SEEDALL_STOCKTAKE_20260102')) SupportedInventoryDocuments;
END;
GO

/* ============================================================
   SUPPLIER INTELLIGENCE PILOT SETTINGS V1
   - SystemSettings is the runtime override source.
   - Existing values are preserved so rerunning SeedAll never
     overwrites an operator's rollout decision.
   ============================================================ */
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @SupplierPilotStoreId int =
    (
        SELECT TOP (1) StoreId
        FROM dbo.Stores
        WHERE Name=N'CafeChain Thủ Dầu Một' AND Active=1
        ORDER BY StoreId
    );

    IF @SupplierPilotStoreId IS NULL
        THROW 53530,N'SUPPLIER_INTELLIGENCE_PILOT_V1: không tìm thấy Store pilot CafeChain Thủ Dầu Một.',1;

    DECLARE @SupplierPilotAllowlist nvarchar(100)=CONVERT(nvarchar(100),@SupplierPilotStoreId);

    IF NOT EXISTS(SELECT 1 FROM dbo.SystemSettings WHERE SettingKey=N'supplier_intelligence_enabled')
        INSERT dbo.SystemSettings(SettingKey,SettingValue,Description)
        VALUES(N'supplier_intelligence_enabled',N'true',N'Bật Supplier Intelligence theo feature gate trong SystemSettings.');

    IF NOT EXISTS(SELECT 1 FROM dbo.SystemSettings WHERE SettingKey=N'supplier_intelligence_shadow_mode')
        INSERT dbo.SystemSettings(SettingKey,SettingValue,Description)
        VALUES(N'supplier_intelligence_shadow_mode',N'true',N'Chỉ tính và hiển thị dữ liệu pilot; không tự tạo tác động mua hàng.');

    IF NOT EXISTS(SELECT 1 FROM dbo.SystemSettings WHERE SettingKey=N'supplier_intelligence_full_rollout')
        INSERT dbo.SystemSettings(SettingKey,SettingValue,Description)
        VALUES(N'supplier_intelligence_full_rollout',N'false',N'Không bật Supplier Intelligence toàn chuỗi khi chưa qua exit gate.');

    IF NOT EXISTS(SELECT 1 FROM dbo.SystemSettings WHERE SettingKey=N'supplier_intelligence_store_allowlist')
        INSERT dbo.SystemSettings(SettingKey,SettingValue,Description)
        VALUES(N'supplier_intelligence_store_allowlist',@SupplierPilotAllowlist,N'Danh sách StoreId pilot, phân tách bằng dấu phẩy.');

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.SystemSettings
        WHERE SettingKey=N'supplier_intelligence_enabled'
          AND LOWER(LTRIM(RTRIM(SettingValue))) IN (N'true',N'false')
    )
        THROW 53531,N'SUPPLIER_INTELLIGENCE_PILOT_V1: enabled phải là true hoặc false.',1;

    COMMIT TRANSACTION;
    PRINT N'SUPPLIER_INTELLIGENCE_PILOT_V1: feature settings are ready; existing custom values were preserved.';
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/* ============================================================
   FIXED POS TEST IDENTITIES
   - Must run before any later batch creates identity rows.
   - AccountId/StaffId 16 and 17 continue directly after migration seed 15.
   - Password hash is copied from salesstaff; POS operator PIN is never seeded.
   ============================================================ */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @PosTestPasswordHash nvarchar(500), @PosTestGender int, @PosTestEmployeeStatus int;
    DECLARE @PosTestSalesRoleId int, @PosTestStoreScopeTypeId int;

    SELECT @PosTestPasswordHash=a.PasswordHash,
           @PosTestGender=s.Gender,
           @PosTestEmployeeStatus=s.EmployeeStatus
    FROM dbo.Accounts a
    JOIN dbo.Staffs s ON s.AccountId=a.AccountId
    WHERE a.Email=N'salesstaff@cafechain.vn' AND a.Active=1 AND s.Active=1 AND s.StoreId=1;

    SELECT @PosTestSalesRoleId=RoleId FROM dbo.Roles WHERE Name=N'Nhân viên bán hàng' AND Active=1;
    SELECT @PosTestStoreScopeTypeId=ScopeTypeId FROM dbo.ScopeTypes WHERE Code=N'STORE';

    IF @PosTestPasswordHash IS NULL OR @PosTestSalesRoleId IS NULL OR @PosTestStoreScopeTypeId IS NULL
       OR NOT EXISTS(SELECT 1 FROM dbo.Stores WHERE StoreId=1 AND Name=N'CafeChain Thủ Dầu Một')
       OR NOT EXISTS(SELECT 1 FROM dbo.Stores WHERE StoreId=3 AND Name=N'CafeChain Dĩ An')
        THROW 53700,N'POS_TEST_IDENTITIES: thiếu salesstaff, role, STORE scope hoặc Store 1/3 nền.',1;

    IF EXISTS(SELECT 1 FROM dbo.Accounts WHERE AccountId=16 AND
              (Email<>N'salesstaff2@cafechain.vn' OR PasswordHash<>@PosTestPasswordHash OR Active<>1 OR RequiresPasswordChange<>0))
       OR EXISTS(SELECT 1 FROM dbo.Accounts WHERE Email=N'salesstaff2@cafechain.vn' AND AccountId<>16)
        THROW 53701,N'POS_TEST_IDENTITIES: AccountId/email 16 đã tồn tại với payload khác.',1;
    IF EXISTS(SELECT 1 FROM dbo.Accounts WHERE AccountId=17 AND
              (Email<>N'salesstaff3@cafechain.vn' OR PasswordHash<>@PosTestPasswordHash OR Active<>1 OR RequiresPasswordChange<>0))
       OR EXISTS(SELECT 1 FROM dbo.Accounts WHERE Email=N'salesstaff3@cafechain.vn' AND AccountId<>17)
        THROW 53702,N'POS_TEST_IDENTITIES: AccountId/email 17 đã tồn tại với payload khác.',1;

    SET IDENTITY_INSERT dbo.Accounts ON;
    IF NOT EXISTS(SELECT 1 FROM dbo.Accounts WHERE AccountId=16)
        INSERT dbo.Accounts(AccountId,Email,PasswordHash,Active,RequiresPasswordChange,CreatedAt,FailedLoginAttempts,LockoutEnd)
        VALUES(16,N'salesstaff2@cafechain.vn',@PosTestPasswordHash,1,0,'2026-08-06T00:00:00',0,NULL);
    IF NOT EXISTS(SELECT 1 FROM dbo.Accounts WHERE AccountId=17)
        INSERT dbo.Accounts(AccountId,Email,PasswordHash,Active,RequiresPasswordChange,CreatedAt,FailedLoginAttempts,LockoutEnd)
        VALUES(17,N'salesstaff3@cafechain.vn',@PosTestPasswordHash,1,0,'2026-08-06T00:00:00',0,NULL);
    SET IDENTITY_INSERT dbo.Accounts OFF;

    IF EXISTS(SELECT 1 FROM dbo.AccountRoles WHERE AccountId IN(16,17) AND RoleId<>@PosTestSalesRoleId)
        THROW 53703,N'POS_TEST_IDENTITIES: tài khoản test có role ngoài Nhân viên bán hàng.',1;
    IF NOT EXISTS(SELECT 1 FROM dbo.AccountRoles WHERE AccountId=16 AND RoleId=@PosTestSalesRoleId)
        INSERT dbo.AccountRoles(AccountId,RoleId) VALUES(16,@PosTestSalesRoleId);
    IF NOT EXISTS(SELECT 1 FROM dbo.AccountRoles WHERE AccountId=17 AND RoleId=@PosTestSalesRoleId)
        INSERT dbo.AccountRoles(AccountId,RoleId) VALUES(17,@PosTestSalesRoleId);

    IF EXISTS(SELECT 1 FROM dbo.Staffs WHERE StaffId=16 AND
              (AccountId<>16 OR FullName<>N'Nhân viên bán hàng 2' OR StoreId<>1 OR Active<>1 OR PosPinHash IS NOT NULL))
       OR EXISTS(SELECT 1 FROM dbo.Staffs WHERE AccountId=16 AND StaffId<>16)
        THROW 53704,N'POS_TEST_IDENTITIES: StaffId/AccountId 16 đã tồn tại với payload khác.',1;
    IF EXISTS(SELECT 1 FROM dbo.Staffs WHERE StaffId=17 AND
              (AccountId<>17 OR FullName<>N'Nhân viên bán hàng 3' OR StoreId<>3 OR Active<>1 OR PosPinHash IS NOT NULL))
       OR EXISTS(SELECT 1 FROM dbo.Staffs WHERE AccountId=17 AND StaffId<>17)
        THROW 53705,N'POS_TEST_IDENTITIES: StaffId/AccountId 17 đã tồn tại với payload khác.',1;

    SET IDENTITY_INSERT dbo.Staffs ON;
    IF NOT EXISTS(SELECT 1 FROM dbo.Staffs WHERE StaffId=16)
        INSERT dbo.Staffs(StaffId,AccountId,FullName,CCCD,Gender,StartDate,EmployeeStatus,DateOfBirth,StoreId,
                          AvatarUrl,AvatarPublicId,Active,PosPinHash,PosPinFailedAttempts,PosPinLockedUntilUtc,CreatedAt)
        VALUES(16,16,N'Nhân viên bán hàng 2',NULL,@PosTestGender,'2026-08-06',@PosTestEmployeeStatus,NULL,1,
               NULL,NULL,1,NULL,0,NULL,'2026-08-06T00:00:00');
    IF NOT EXISTS(SELECT 1 FROM dbo.Staffs WHERE StaffId=17)
        INSERT dbo.Staffs(StaffId,AccountId,FullName,CCCD,Gender,StartDate,EmployeeStatus,DateOfBirth,StoreId,
                          AvatarUrl,AvatarPublicId,Active,PosPinHash,PosPinFailedAttempts,PosPinLockedUntilUtc,CreatedAt)
        VALUES(17,17,N'Nhân viên bán hàng 3',NULL,@PosTestGender,'2026-08-06',@PosTestEmployeeStatus,NULL,3,
               NULL,NULL,1,NULL,0,NULL,'2026-08-06T00:00:00');
    SET IDENTITY_INSERT dbo.Staffs OFF;

    IF EXISTS(SELECT 1 FROM dbo.StaffScopes WHERE StaffId=16 AND
              (ScopeTypeId<>@PosTestStoreScopeTypeId OR ScopeRefId<>1))
       OR EXISTS(SELECT 1 FROM dbo.StaffScopes WHERE StaffId=17 AND
              (ScopeTypeId<>@PosTestStoreScopeTypeId OR ScopeRefId<>3))
        THROW 53706,N'POS_TEST_IDENTITIES: StaffScope test khác STORE/Store contract.',1;
    IF NOT EXISTS(SELECT 1 FROM dbo.StaffScopes WHERE StaffId=16 AND ScopeTypeId=@PosTestStoreScopeTypeId AND ScopeRefId=1)
        INSERT dbo.StaffScopes(StaffId,ScopeTypeId,ScopeRefId) VALUES(16,@PosTestStoreScopeTypeId,1);
    IF NOT EXISTS(SELECT 1 FROM dbo.StaffScopes WHERE StaffId=17 AND ScopeTypeId=@PosTestStoreScopeTypeId AND ScopeRefId=3)
        INSERT dbo.StaffScopes(StaffId,ScopeTypeId,ScopeRefId) VALUES(17,@PosTestStoreScopeTypeId,3);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    SET IDENTITY_INSERT dbo.Accounts OFF;
    SET IDENTITY_INSERT dbo.Staffs OFF;
    THROW;
END CATCH;
GO

/* ============================================================
   BATCH 16 - DEMO_COVERAGE_V16 branch receipt integrity
   - Remediates the two historical confirmed demo receipts.
   - Adds Store 2 and enough confirmed receipts for supplier analytics.
   - Inventory/FIFO/posting writes are guarded by durable business keys.
   ============================================================ */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

CREATE OR ALTER PROCEDURE dbo.SeedDemoCoverageV16
AS
BEGIN
BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @CoverageNow datetime2(7)=SYSUTCDATETIME();
    DECLARE @CoverageStore1 int=(SELECT StoreId FROM dbo.Stores WHERE StoreId=1 AND Active=1);
    DECLARE @CoverageStore2 int=(SELECT StoreId FROM dbo.Stores WHERE StoreId=2 AND Active=1);
    DECLARE @CoverageStore3 int=(SELECT StoreId FROM dbo.Stores WHERE StoreId=3 AND Active=1);
    DECLARE @CoverageSupplier int=COALESCE(
        (SELECT SupplierId FROM dbo.Suppliers WHERE Code=N'DEMO_SUP_VIET_COFFEE'),
        (SELECT SupplierId FROM dbo.Suppliers WHERE SupplierId=6));
    DECLARE @CoverageOffer int=(SELECT TOP(1) IngredientSupplierId
                                FROM dbo.IngredientSuppliers
                                WHERE SupplierId=@CoverageSupplier AND IngredientId=14 AND Active=1
                                ORDER BY IngredientSupplierId);
    DECLARE @CoverageIngredient int=(SELECT IngredientId FROM dbo.Ingredients WHERE IngredientId=14);
    DECLARE @CoverageBaseUnit int=(SELECT BaseUnitId FROM dbo.Ingredients WHERE IngredientId=14);
    DECLARE @CoverageProcurementUnit int=(SELECT UnitId FROM dbo.IngredientSuppliers WHERE IngredientSupplierId=@CoverageOffer);
    DECLARE @CoveragePackageQty decimal(18,5)=(SELECT PackageQuantity FROM dbo.IngredientSuppliers WHERE IngredientSupplierId=@CoverageOffer);
    DECLARE @CoveragePackagePrice decimal(18,2)=(SELECT CurrentPrice FROM dbo.IngredientSuppliers WHERE IngredientSupplierId=@CoverageOffer);
    DECLARE @CoverageFactor decimal(18,6)=1000;
    DECLARE @CoverageUnitCost decimal(18,6)=@CoveragePackagePrice/@CoverageFactor;

    IF @CoverageStore1 IS NULL OR @CoverageStore2 IS NULL OR @CoverageStore3 IS NULL
       OR @CoverageSupplier IS NULL OR @CoverageOffer IS NULL OR @CoverageBaseUnit IS NULL
        THROW 53610,N'DEMO_COVERAGE_V16: missing store, supplier, offer, ingredient, or unit foundation.',1;

    DECLARE @CoverageStore2Email nvarchar(256)=N'demo.manager.thuanan@cafechain.local';
    DECLARE @CoverageStore2Account int;
    DECLARE @CoverageStore2Staff int;
    DECLARE @CoverageManagerRole int=(SELECT TOP(1) RoleId FROM dbo.Roles WHERE Name=N'Quản lý chi nhánh' OR RoleId=3 ORDER BY CASE WHEN RoleId=3 THEN 0 ELSE 1 END);
    DECLARE @CoverageSourcePassword nvarchar(max)=(SELECT TOP(1) PasswordHash FROM dbo.Accounts ORDER BY AccountId);
    DECLARE @CoverageStoreScopeType int=(SELECT TOP(1) ScopeTypeId FROM dbo.ScopeTypes WHERE Name=N'Store' OR ScopeTypeId=1 ORDER BY ScopeTypeId);

    IF @CoverageManagerRole IS NULL OR @CoverageSourcePassword IS NULL OR @CoverageStoreScopeType IS NULL
        THROW 53611,N'DEMO_COVERAGE_V16: missing role, password fixture, or Store scope type.',1;

    IF NOT EXISTS(SELECT 1 FROM dbo.Accounts WHERE Email=@CoverageStore2Email)
        INSERT dbo.Accounts(Email,PasswordHash,Active,RequiresPasswordChange,CreatedAt,FailedLoginAttempts,LockoutEnd)
        VALUES(@CoverageStore2Email,@CoverageSourcePassword,1,0,'2026-02-01T07:00:00',0,NULL);
    SELECT @CoverageStore2Account=AccountId FROM dbo.Accounts WHERE Email=@CoverageStore2Email;

    IF NOT EXISTS(SELECT 1 FROM dbo.AccountRoles WHERE AccountId=@CoverageStore2Account AND RoleId=@CoverageManagerRole)
        INSERT dbo.AccountRoles(AccountId,RoleId) VALUES(@CoverageStore2Account,@CoverageManagerRole);

    IF NOT EXISTS(SELECT 1 FROM dbo.Staffs WHERE AccountId=@CoverageStore2Account)
        INSERT dbo.Staffs(AccountId,FullName,CCCD,Gender,StartDate,EmployeeStatus,DateOfBirth,StoreId,
                          AvatarUrl,AvatarPublicId,Active,CreatedAt)
        VALUES(@CoverageStore2Account,N'Quản lý demo Thuận An',NULL,1,'2026-02-01',2,NULL,@CoverageStore2,
               NULL,NULL,1,'2026-02-01T07:00:00');
    SELECT @CoverageStore2Staff=StaffId FROM dbo.Staffs WHERE AccountId=@CoverageStore2Account;

    IF NOT EXISTS(SELECT 1 FROM dbo.StaffScopes
                  WHERE StaffId=@CoverageStore2Staff AND ScopeTypeId=@CoverageStoreScopeType AND ScopeRefId=@CoverageStore2)
        INSERT dbo.StaffScopes(StaffId,ScopeTypeId,ScopeRefId)
        VALUES(@CoverageStore2Staff,@CoverageStoreScopeType,@CoverageStore2);

    DECLARE @CoverageStaff1 int=(SELECT TOP(1) StaffId FROM dbo.Staffs WHERE StoreId=1 AND Active=1 ORDER BY StaffId);
    DECLARE @CoverageStaff3 int=(SELECT TOP(1) StaffId FROM dbo.Staffs WHERE StoreId=3 AND Active=1 ORDER BY StaffId);
    IF @CoverageStaff1 IS NULL OR @CoverageStore2Staff IS NULL OR @CoverageStaff3 IS NULL
        THROW 53612,N'DEMO_COVERAGE_V16: each store needs an active receipt actor.',1;

    IF NOT EXISTS(SELECT 1 FROM dbo.SupplierStores WHERE SupplierId=@CoverageSupplier AND StoreId=@CoverageStore2)
        INSERT dbo.SupplierStores(SupplierId,StoreId,Active,LeadTimeOverrideDays,DeliverySchedule,Note,CreatedAt,UpdatedAt)
        VALUES(@CoverageSupplier,@CoverageStore2,1,1,N'Thứ 2-4-6',N'DEMO_COVERAGE_V16 receipt scope','2026-02-01','2026-02-01');

    INSERT dbo.StoreInventories(StoreId,IngredientId,RecipeId,PreparedItemId,AvailableQty,ReservedQty,MinStockLevel,LastUpdated)
    SELECT s.StoreId,@CoverageIngredient,NULL,NULL,0,0,500,'2026-02-01'
    FROM (VALUES(@CoverageStore1),(@CoverageStore2),(@CoverageStore3)) s(StoreId)
    WHERE NOT EXISTS(SELECT 1 FROM dbo.StoreInventories si
                     WHERE si.StoreId=s.StoreId AND si.IngredientId=@CoverageIngredient);

    /* Historical rows used package quantities as base quantities. Repair only before any posting exists. */
    UPDATE brl
       SET InputQuantity=10,InputUnitId=@CoverageProcurementUnit,
           ReceivedBaseQuantity=8000,RejectedBaseQuantity=2000,
           ReceivedPackQuantity=10,AcceptedPackQuantity=8,
           ReceivedProcurementQuantity=10,RejectedProcurementQuantity=2,AcceptedProcurementQuantity=8,
           InventoryPostingBaseQuantity=8000,ProcurementUnitId=@CoverageProcurementUnit,
           InventoryBaseUnitId=@CoverageBaseUnit,ProcurementToInventoryFactor=@CoverageFactor,
           PurchaseMode=N'Packaged',BaseUnitId=@CoverageBaseUnit,
           ActualPackagePrice=@CoveragePackagePrice,PackageQuantitySnapshot=@CoveragePackageQty,
           PackageUnitIdSnapshot=@CoverageProcurementUnit,BaseUnitCostSnapshot=@CoverageUnitCost,
           LineTotalCost=ROUND(8000*@CoverageUnitCost,2)
    FROM dbo.BranchReceiptLines brl
    JOIN dbo.BranchReceipts br ON br.BranchReceiptId=brl.BranchReceiptId
    WHERE br.ReceiptCode IN(N'DEMO-DASH-V13-BR-001',N'DEMO-AI-ROLLING-BR-S3')
      AND brl.InventoryTransactionId IS NULL
      AND NOT EXISTS(SELECT 1 FROM dbo.PurchaseOrderReceiptPostings p WHERE p.BranchReceiptLineId=brl.BranchReceiptLineId);

    UPDATE pol
       SET PackageCount=10,PurchaseMode=N'Packaged',OrderedPackageCount=10,OrderedBaseQuantity=10000,
           OrderedPackQuantity=10,PackSizeProcurementQuantity=1,ProcurementUnitId=@CoverageProcurementUnit,
           OrderedProcurementQuantity=10,UnitPricePerPackage=@CoveragePackagePrice,
           UnitPricePerProcurementUnit=NULL,AcceptedPackQuantity=8,AcceptedProcurementQuantity=8,
           InventoryPostingBaseQuantity=8000,InventoryBaseUnitId=@CoverageBaseUnit,
           ProcurementToInventoryFactor=@CoverageFactor,ClosedRemainingQuantity=2000
    FROM dbo.PurchaseOrderLines pol
    JOIN dbo.BranchReceiptLines brl ON brl.PurchaseOrderLineId=pol.PurchaseOrderLineId
    JOIN dbo.BranchReceipts br ON br.BranchReceiptId=brl.BranchReceiptId
    WHERE br.ReceiptCode IN(N'DEMO-DASH-V13-BR-001',N'DEMO-AI-ROLLING-BR-S3')
      AND NOT EXISTS(SELECT 1 FROM dbo.PurchaseOrderReceiptPostings p WHERE p.BranchReceiptLineId=brl.BranchReceiptLineId);

    UPDATE rr
       SET RequestedQuantity=10000,SuggestedQuantity=10000,Status=N'PROCESSING',
           UpdatedAt=@CoverageNow,ClosedRemainingQuantity=2000
    FROM dbo.RestockRequests rr
    JOIN dbo.BranchReceiptLines brl ON brl.RestockRequestId=rr.RestockRequestId
    JOIN dbo.BranchReceipts br ON br.BranchReceiptId=brl.BranchReceiptId
    WHERE br.ReceiptCode IN(N'DEMO-DASH-V13-BR-001',N'DEMO-AI-ROLLING-BR-S3')
      AND NOT EXISTS(SELECT 1 FROM dbo.RestockFulfillmentPostings p
                     WHERE p.SourceDocumentType=N'BRANCH_RECEIPT'
                       AND p.SourceDocumentId=br.BranchReceiptId
                       AND p.SourceDocumentLineId=brl.BranchReceiptLineId);

    DECLARE @ReceiptScenario TABLE
    (
        ScenarioCode nvarchar(30) PRIMARY KEY, StoreId int NOT NULL, ActorStaffId int NOT NULL,
        ReceiptStatus nvarchar(16) NOT NULL, OrderedBase decimal(18,3) NOT NULL,
        AcceptedBase decimal(18,3) NOT NULL, RejectedBase decimal(18,3) NOT NULL,
        InputPackages decimal(18,3) NOT NULL, EventAt datetime2(0) NOT NULL
    );
    INSERT @ReceiptScenario VALUES
      (N'S1-FULL-001',@CoverageStore1,@CoverageStaff1,N'CONFIRMED',1000,1000,0,1,'2026-02-03T09:00:00'),
      (N'S2-PART-001',@CoverageStore2,@CoverageStore2Staff,N'CONFIRMED',1000,800,200,1,'2026-02-04T09:00:00'),
      (N'S2-FULL-002',@CoverageStore2,@CoverageStore2Staff,N'CONFIRMED',1000,1000,0,1,'2026-02-05T09:00:00'),
      (N'S3-DRAFT-001',@CoverageStore3,@CoverageStaff3,N'DRAFT',1000,1000,0,1,'2026-02-06T09:00:00');

    INSERT dbo.PurchaseOrders
    (Code,StoreId,SupplierId,Status,OrderDate,ExpectedDeliveryAtUtc,CreatedByStaffId,
     ApprovedByStaffId,SentByStaffId,CreatedAtUtc,UpdatedAtUtc,ApprovedAtUtc,SentAtUtc,Note)
    SELECT CONCAT(N'DEMO-COVERAGE-V16-PO-',x.ScenarioCode),x.StoreId,@CoverageSupplier,N'MARKED_AS_SENT',
           DATEADD(DAY,-2,x.EventAt),DATEADD(HOUR,-1,x.EventAt),x.ActorStaffId,x.ActorStaffId,x.ActorStaffId,
           DATEADD(DAY,-2,x.EventAt),DATEADD(DAY,-1,x.EventAt),DATEADD(DAY,-2,x.EventAt),
           DATEADD(DAY,-2,x.EventAt),N'DEMO_COVERAGE_V16'
    FROM @ReceiptScenario x
    WHERE NOT EXISTS(SELECT 1 FROM dbo.PurchaseOrders p
                     WHERE p.Code=CONCAT(N'DEMO-COVERAGE-V16-PO-',x.ScenarioCode));

    INSERT dbo.PurchaseOrderLines
    (PurchaseOrderId,RestockRequestId,IngredientId,IngredientSupplierId,
     PackageUnitIdSnapshot,PackageQuantitySnapshot,PackagePriceSnapshot,PackageCount,
     PurchaseMode,OrderedPackageCount,OrderedBaseQuantity,OrderedPackQuantity,
     PackSizeProcurementQuantity,ProcurementUnitId,OrderedProcurementQuantity,
     UnitPricePerPackage,UnitPricePerProcurementUnit,RoundingSurplusProcurementQuantity,
     ClosedProcurementQuantity,InventoryBaseUnitId,ProcurementToInventoryFactor,
     ClosedRemainingQuantity,PromisedLeadTimeDaysSnapshot,Note)
    SELECT po.PurchaseOrderId,NULL,@CoverageIngredient,@CoverageOffer,
           @CoverageProcurementUnit,@CoveragePackageQty,@CoveragePackagePrice,x.InputPackages,
           N'Packaged',x.InputPackages,x.OrderedBase,x.InputPackages,
           @CoveragePackageQty,@CoverageProcurementUnit,x.InputPackages,
           @CoveragePackagePrice,NULL,0,0,@CoverageBaseUnit,@CoverageFactor,
           CASE WHEN x.ReceiptStatus=N'CONFIRMED' THEN x.RejectedBase ELSE 0 END,
           1,CONCAT(N'DEMO_COVERAGE_V16_POL_',x.ScenarioCode)
    FROM @ReceiptScenario x
    JOIN dbo.PurchaseOrders po ON po.Code=CONCAT(N'DEMO-COVERAGE-V16-PO-',x.ScenarioCode)
    WHERE NOT EXISTS(SELECT 1 FROM dbo.PurchaseOrderLines l
                     WHERE l.Note=CONCAT(N'DEMO_COVERAGE_V16_POL_',x.ScenarioCode));

    INSERT dbo.BranchReceipts
    (ReceiptCode,StoreId,SupplierId,PurchaseOrderId,SourceInventoryTransferId,
     Status,ReceiptKey,ReferenceNumber,ReceivedAt,ReceivedByStaffId,
     ConfirmedAt,ConfirmedByStaffId,Notes,CreatedAt,CreatedByStaffId)
    SELECT CONCAT(N'DEMO-COVERAGE-V16-BR-',x.ScenarioCode),x.StoreId,@CoverageSupplier,po.PurchaseOrderId,NULL,
           x.ReceiptStatus,CONCAT(N'DEMO_COVERAGE_V16_RECEIPT_',x.ScenarioCode),CONCAT(N'INV-',x.ScenarioCode),
           x.EventAt,x.ActorStaffId,
           CASE WHEN x.ReceiptStatus=N'CONFIRMED' THEN DATEADD(MINUTE,10,x.EventAt) END,
           CASE WHEN x.ReceiptStatus=N'CONFIRMED' THEN x.ActorStaffId END,
           CONCAT(N'DEMO_COVERAGE_V16 ',x.ReceiptStatus),x.EventAt,x.ActorStaffId
    FROM @ReceiptScenario x
    JOIN dbo.PurchaseOrders po ON po.Code=CONCAT(N'DEMO-COVERAGE-V16-PO-',x.ScenarioCode)
    WHERE NOT EXISTS(SELECT 1 FROM dbo.BranchReceipts br
                     WHERE br.ReceiptCode=CONCAT(N'DEMO-COVERAGE-V16-BR-',x.ScenarioCode));

    INSERT dbo.BranchReceiptLines
    (BranchReceiptId,RestockRequestId,PurchaseOrderLineId,IngredientId,PreparedItemId,RecipeId,
     InputQuantity,InputUnitId,ReceivedBaseQuantity,RejectedBaseQuantity,
     ReceivedPackQuantity,AcceptedPackQuantity,ReceivedProcurementQuantity,
     RejectedProcurementQuantity,AcceptedProcurementQuantity,InventoryPostingBaseQuantity,
     ProcurementUnitId,InventoryBaseUnitId,ProcurementToInventoryFactor,PurchaseMode,
     RejectionReason,RejectionIssueType,BaseUnitId,SupplierId,IngredientSupplierId,
     ActualPackagePrice,PackageQuantitySnapshot,PackageUnitIdSnapshot,
     BaseUnitCostSnapshot,LineTotalCost,CreatedAt)
    SELECT br.BranchReceiptId,NULL,pol.PurchaseOrderLineId,@CoverageIngredient,NULL,NULL,
           x.InputPackages,@CoverageProcurementUnit,x.AcceptedBase,x.RejectedBase,
           x.InputPackages,x.AcceptedBase/@CoverageFactor,x.InputPackages,
           x.RejectedBase/@CoverageFactor,x.AcceptedBase/@CoverageFactor,x.AcceptedBase,
           @CoverageProcurementUnit,@CoverageBaseUnit,@CoverageFactor,N'Packaged',
           CASE WHEN x.RejectedBase>0 THEN N'Bao bì hư hỏng khi giao' END,
           CASE WHEN x.RejectedBase>0 THEN N'PACKAGING_FAILURE' END,
           @CoverageBaseUnit,@CoverageSupplier,@CoverageOffer,@CoveragePackagePrice,
           @CoveragePackageQty,@CoverageProcurementUnit,@CoverageUnitCost,
           ROUND(x.AcceptedBase*@CoverageUnitCost,2),x.EventAt
    FROM @ReceiptScenario x
    JOIN dbo.BranchReceipts br ON br.ReceiptCode=CONCAT(N'DEMO-COVERAGE-V16-BR-',x.ScenarioCode)
    JOIN dbo.PurchaseOrderLines pol ON pol.Note=CONCAT(N'DEMO_COVERAGE_V16_POL_',x.ScenarioCode)
    WHERE NOT EXISTS(SELECT 1 FROM dbo.BranchReceiptLines l WHERE l.BranchReceiptId=br.BranchReceiptId);

    INSERT dbo.PurchaseOrderReceiptPostings
    (PurchaseOrderLineId,BranchReceiptLineId,AcceptedBaseQuantity,RejectedBaseQuantity,
     AcceptedProcurementQuantity,RejectedProcurementQuantity,InventoryPostingBaseQuantity,
     ProcurementUnitId,InventoryBaseUnitId,ProcurementToInventoryFactor,PurchaseMode,
     CreatedByStaffId,CreatedAtUtc)
    SELECT brl.PurchaseOrderLineId,brl.BranchReceiptLineId,brl.ReceivedBaseQuantity,brl.RejectedBaseQuantity,
           brl.AcceptedProcurementQuantity,brl.RejectedProcurementQuantity,
           COALESCE(brl.InventoryPostingBaseQuantity,brl.ReceivedBaseQuantity),
           brl.ProcurementUnitId,brl.InventoryBaseUnitId,brl.ProcurementToInventoryFactor,
           brl.PurchaseMode,br.ConfirmedByStaffId,COALESCE(br.ConfirmedAt,@CoverageNow)
    FROM dbo.BranchReceipts br
    JOIN dbo.BranchReceiptLines brl ON brl.BranchReceiptId=br.BranchReceiptId
    WHERE br.Status=N'CONFIRMED'
      AND (br.ReceiptCode IN(N'DEMO-DASH-V13-BR-001',N'DEMO-AI-ROLLING-BR-S3')
           OR br.ReceiptCode LIKE N'DEMO-COVERAGE-V16-BR-%')
      AND brl.PurchaseOrderLineId IS NOT NULL
      AND NOT EXISTS(SELECT 1 FROM dbo.PurchaseOrderReceiptPostings p
                     WHERE p.BranchReceiptLineId=brl.BranchReceiptLineId);

    INSERT dbo.RestockFulfillmentPostings
    (RestockRequestId,SourceDocumentType,SourceDocumentId,SourceDocumentLineId,
     IngredientId,PreparedItemId,Quantity,BaseUnitId,CreatedAtUtc)
    SELECT brl.RestockRequestId,N'BRANCH_RECEIPT',br.BranchReceiptId,brl.BranchReceiptLineId,
           brl.IngredientId,brl.PreparedItemId,
           COALESCE(brl.InventoryPostingBaseQuantity,brl.ReceivedBaseQuantity),
           brl.BaseUnitId,COALESCE(br.ConfirmedAt,@CoverageNow)
    FROM dbo.BranchReceipts br
    JOIN dbo.BranchReceiptLines brl ON brl.BranchReceiptId=br.BranchReceiptId
    WHERE br.Status=N'CONFIRMED' AND brl.RestockRequestId IS NOT NULL
      AND br.ReceiptCode IN(N'DEMO-DASH-V13-BR-001',N'DEMO-AI-ROLLING-BR-S3')
      AND NOT EXISTS(SELECT 1 FROM dbo.RestockFulfillmentPostings p
                     WHERE p.SourceDocumentType=N'BRANCH_RECEIPT'
                       AND p.SourceDocumentId=br.BranchReceiptId
                       AND p.SourceDocumentLineId=brl.BranchReceiptLineId);

    DECLARE @PostingLines TABLE(BranchReceiptLineId int PRIMARY KEY, Done bit NOT NULL DEFAULT 0);
    INSERT @PostingLines(BranchReceiptLineId)
    SELECT brl.BranchReceiptLineId
    FROM dbo.BranchReceipts br
    JOIN dbo.BranchReceiptLines brl ON brl.BranchReceiptId=br.BranchReceiptId
    WHERE br.Status=N'CONFIRMED'
      AND (br.ReceiptCode IN(N'DEMO-DASH-V13-BR-001',N'DEMO-AI-ROLLING-BR-S3')
           OR br.ReceiptCode LIKE N'DEMO-COVERAGE-V16-BR-%')
      AND brl.InventoryTransactionId IS NULL
      AND NOT EXISTS(SELECT 1 FROM dbo.InventoryTransactions it WHERE it.BranchReceiptLineId=brl.BranchReceiptLineId);

    WHILE EXISTS(SELECT 1 FROM @PostingLines WHERE Done=0)
    BEGIN
        DECLARE @LineId int=(SELECT TOP(1) BranchReceiptLineId FROM @PostingLines WHERE Done=0 ORDER BY BranchReceiptLineId);
        DECLARE @LineStore int,@LineIngredient int,@LineQty decimal(18,3),@LineCost decimal(18,6),
                @LineAt datetime2(7),@StoreInventoryId int,@BeforeQty decimal(18,3),@InventoryTransactionId int;
        SELECT @LineStore=br.StoreId,@LineIngredient=brl.IngredientId,
               @LineQty=COALESCE(brl.InventoryPostingBaseQuantity,brl.ReceivedBaseQuantity),
               @LineCost=brl.BaseUnitCostSnapshot,@LineAt=COALESCE(br.ConfirmedAt,@CoverageNow)
        FROM dbo.BranchReceiptLines brl JOIN dbo.BranchReceipts br ON br.BranchReceiptId=brl.BranchReceiptId
        WHERE brl.BranchReceiptLineId=@LineId;
        SELECT @StoreInventoryId=StoreInventoryId,@BeforeQty=AvailableQty
        FROM dbo.StoreInventories WITH(UPDLOCK,HOLDLOCK)
        WHERE StoreId=@LineStore AND IngredientId=@LineIngredient;
        IF @StoreInventoryId IS NULL THROW 53613,N'DEMO_COVERAGE_V16: receipt inventory identity missing.',1;

        UPDATE dbo.StoreInventories
           SET AvailableQty=AvailableQty+@LineQty,LastUpdated=@CoverageNow
        WHERE StoreInventoryId=@StoreInventoryId;

        INSERT dbo.InventoryTransactions
        (StoreInventoryId,Type,Quantity,BeforeQty,AfterQty,UnitCost,TotalCost,BranchReceiptLineId,CreatedAt)
        VALUES(@StoreInventoryId,14,@LineQty,@BeforeQty,@BeforeQty+@LineQty,@LineCost,
               ROUND(@LineQty*@LineCost,2),@LineId,@LineAt);
        SET @InventoryTransactionId=CONVERT(int,SCOPE_IDENTITY());

        INSERT dbo.InventoryCostLayers
        (IngredientId,PreparedItemId,StoreId,Quantity,RemainingQuantity,UnitCost,CreatedAt,SourceBranchReceiptLineId)
        VALUES(@LineIngredient,NULL,@LineStore,@LineQty,@LineQty,@LineCost,@LineAt,@LineId);

        UPDATE dbo.BranchReceiptLines SET InventoryTransactionId=@InventoryTransactionId
        WHERE BranchReceiptLineId=@LineId;
        UPDATE @PostingLines SET Done=1 WHERE BranchReceiptLineId=@LineId;
    END

    UPDATE rr
       SET Status=N'COMPLETED',SourcingStatus=N'FULFILLED',UpdatedAt=@CoverageNow
    FROM dbo.RestockRequests rr
    WHERE EXISTS(SELECT 1 FROM dbo.RestockFulfillmentPostings p
                 WHERE p.RestockRequestId=rr.RestockRequestId AND p.SourceDocumentType=N'BRANCH_RECEIPT');

    INSERT dbo.RestockRequestTransitions
    (RestockRequestId,PreviousStatus,NewStatus,ActorStaffId,OccurredAtUtc,Reason)
    SELECT DISTINCT brl.RestockRequestId,N'PROCESSING',N'COMPLETED',br.ConfirmedByStaffId,
           COALESCE(br.ConfirmedAt,@CoverageNow),N'DEMO_COVERAGE_V16 confirmed receipt remediation'
    FROM dbo.BranchReceipts br
    JOIN dbo.BranchReceiptLines brl ON brl.BranchReceiptId=br.BranchReceiptId
    WHERE brl.RestockRequestId IS NOT NULL
      AND br.ReceiptCode IN(N'DEMO-DASH-V13-BR-001',N'DEMO-AI-ROLLING-BR-S3')
      AND NOT EXISTS(SELECT 1 FROM dbo.RestockRequestTransitions t
                     WHERE t.RestockRequestId=brl.RestockRequestId
                       AND t.Reason=N'DEMO_COVERAGE_V16 confirmed receipt remediation');

    UPDATE po
       SET Status=CASE WHEN br.Status=N'CONFIRMED' THEN N'COMPLETED' ELSE po.Status END,
           CompletedAtUtc=CASE WHEN br.Status=N'CONFIRMED' THEN COALESCE(po.CompletedAtUtc,br.ConfirmedAt,@CoverageNow) ELSE po.CompletedAtUtc END,
           UpdatedAtUtc=@CoverageNow
    FROM dbo.PurchaseOrders po
    JOIN dbo.BranchReceipts br ON br.PurchaseOrderId=po.PurchaseOrderId
    WHERE br.ReceiptCode IN(N'DEMO-DASH-V13-BR-001',N'DEMO-AI-ROLLING-BR-S3')
       OR br.ReceiptCode LIKE N'DEMO-COVERAGE-V16-BR-%';

    INSERT dbo.SupplierReceiptIssues
    (SupplierId,StoreId,PurchaseOrderId,PurchaseOrderLineId,BranchReceiptId,BranchReceiptLineId,
     IssueType,Status,AffectedBaseQuantity,Description,ReportedByStaffId,ReportedAtUtc,UpdatedAtUtc)
    SELECT br.SupplierId,br.StoreId,br.PurchaseOrderId,brl.PurchaseOrderLineId,
           br.BranchReceiptId,brl.BranchReceiptLineId,brl.RejectionIssueType,N'OPEN',
           brl.RejectedBaseQuantity,CONCAT(N'DEMO_COVERAGE_V16_REJECTION_',brl.BranchReceiptLineId),
           br.ConfirmedByStaffId,COALESCE(br.ConfirmedAt,@CoverageNow),COALESCE(br.ConfirmedAt,@CoverageNow)
    FROM dbo.BranchReceipts br
    JOIN dbo.BranchReceiptLines brl ON brl.BranchReceiptId=br.BranchReceiptId
    WHERE br.Status=N'CONFIRMED' AND brl.RejectedBaseQuantity>0
      AND (br.ReceiptCode IN(N'DEMO-DASH-V13-BR-001',N'DEMO-AI-ROLLING-BR-S3')
           OR br.ReceiptCode LIKE N'DEMO-COVERAGE-V16-BR-%')
      AND NOT EXISTS(SELECT 1 FROM dbo.SupplierReceiptIssues i WHERE i.BranchReceiptLineId=brl.BranchReceiptLineId);

    INSERT dbo.SupplierReceiptIssueTransitions
    (SupplierReceiptIssueId,PreviousStatus,NewStatus,ActorStaffId,Reason,OccurredAtUtc)
    SELECT i.SupplierReceiptIssueId,N'OPEN',N'OPEN',i.ReportedByStaffId,
           N'DEMO_COVERAGE_V16 issue recorded',i.ReportedAtUtc
    FROM dbo.SupplierReceiptIssues i
    JOIN dbo.BranchReceipts br ON br.BranchReceiptId=i.BranchReceiptId
    WHERE (br.ReceiptCode IN(N'DEMO-DASH-V13-BR-001',N'DEMO-AI-ROLLING-BR-S3')
           OR br.ReceiptCode LIKE N'DEMO-COVERAGE-V16-BR-%')
      AND NOT EXISTS(SELECT 1 FROM dbo.SupplierReceiptIssueTransitions t
                     WHERE t.SupplierReceiptIssueId=i.SupplierReceiptIssueId
                       AND t.Reason=N'DEMO_COVERAGE_V16 issue recorded');

    IF (SELECT COUNT(DISTINCT StoreId) FROM dbo.BranchReceipts
        WHERE ReceiptCode IN(N'DEMO-DASH-V13-BR-001',N'DEMO-AI-ROLLING-BR-S3')
           OR ReceiptCode LIKE N'DEMO-COVERAGE-V16-BR-%')<>3
        THROW 53620,N'DEMO_COVERAGE_V16: branch receipts do not cover all three stores.',1;
    IF (SELECT COUNT(*) FROM dbo.BranchReceipts WHERE SupplierId=@CoverageSupplier AND Status=N'CONFIRMED')<5
        THROW 53621,N'DEMO_COVERAGE_V16: supplier analytics needs at least five confirmed receipts.',1;
    IF EXISTS
    (
        SELECT 1 FROM dbo.BranchReceipts br
        JOIN dbo.BranchReceiptLines brl ON brl.BranchReceiptId=br.BranchReceiptId
        WHERE br.Status=N'CONFIRMED'
          AND (br.ReceiptCode IN(N'DEMO-DASH-V13-BR-001',N'DEMO-AI-ROLLING-BR-S3')
               OR br.ReceiptCode LIKE N'DEMO-COVERAGE-V16-BR-%')
          AND (brl.InventoryTransactionId IS NULL
               OR NOT EXISTS(SELECT 1 FROM dbo.InventoryCostLayers l WHERE l.SourceBranchReceiptLineId=brl.BranchReceiptLineId)
               OR (brl.PurchaseOrderLineId IS NOT NULL AND NOT EXISTS
                   (SELECT 1 FROM dbo.PurchaseOrderReceiptPostings p WHERE p.BranchReceiptLineId=brl.BranchReceiptLineId)))
    ) THROW 53622,N'DEMO_COVERAGE_V16: confirmed receipt ledger is incomplete.',1;
    IF EXISTS
    (
        SELECT 1 FROM dbo.BranchReceipts br
        JOIN dbo.BranchReceiptLines brl ON brl.BranchReceiptId=br.BranchReceiptId
        WHERE br.Status=N'DRAFT' AND br.ReceiptCode LIKE N'DEMO-COVERAGE-V16-BR-%'
          AND (brl.InventoryTransactionId IS NOT NULL
               OR EXISTS(SELECT 1 FROM dbo.PurchaseOrderReceiptPostings p WHERE p.BranchReceiptLineId=brl.BranchReceiptLineId))
    ) THROW 53623,N'DEMO_COVERAGE_V16: draft receipt unexpectedly posted inventory.',1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
END;
GO

IF EXISTS(SELECT 1 FROM dbo.BranchReceipts WHERE ReceiptCode LIKE N'DEMO-COVERAGE-V16-BR-%')
SELECT N'DEMO_COVERAGE_V16_RECEIPTS' SeedMarker,
       (SELECT COUNT(*) FROM dbo.BranchReceipts WHERE Status=N'CONFIRMED') ConfirmedReceipts,
       (SELECT COUNT(DISTINCT StoreId) FROM dbo.BranchReceipts) StoresWithReceipts,
       (SELECT COUNT(*) FROM dbo.InventoryTransactions WHERE BranchReceiptLineId IS NOT NULL) ReceiptTransactions,
       (SELECT COUNT(*) FROM dbo.InventoryCostLayers WHERE SourceBranchReceiptLineId IS NOT NULL) ReceiptCostLayers,
       (SELECT COUNT(*) FROM dbo.PurchaseOrderReceiptPostings) PurchaseOrderPostings,
       (SELECT COUNT(*) FROM dbo.RestockFulfillmentPostings) RestockPostings;
GO

/*
    CafeChain consolidated seed - Batch 14/14
    Tables completed:
      1. DrinkCategories
      2. Drinks
      3. DrinkImages
      4. DrinkSizes
      5. Toppings
      6. DrinkToppings
      7. DrinkDefaultToppings
      8. StoreToppings
      9. Units
     10. Ingredients
     11. UnitConversions
     12. PreparedItems
     13. Recipes
     14. RecipeDetails
     15. DrinkSizeToppingPolicies
     16. Suppliers
     17. SupplierPhones
     18. SupplierContacts
     19. SupplierStores
     20. IngredientSuppliers
     21. IngredientSupplierPriceHistories
     22. StoreDrinks
     23. StoreMenuItems
     24. PosCatalogStates
     25. InventoryDocuments
     26. InventoryDocumentDetails
     27. StoreInventories
     28. InventoryTransactions
     29. ProductionRuns
     30. InventoryCostLayers
     31. InventoryCostAllocations
     32. InventoryDocumentSnapshots
     33. InventoryDocumentSnapshotDetails
     34. StockTakeSessions
     35. StockTakeDetails
     36. InventoryTransfers
     37. InventoryTransferDetails
     38. PermissionGroups
     39. Permissions
     40. RolePermissions

    Execution contract:
      - Run after migration 20260721155454_InitialCreate.
      - EF HasData owns IDs 1-3 of DrinkCategories, IDs 1-6 of Drinks,
        IDs 1-24 of DrinkImages, ProductTypes and Sizes.
      - Part1 values are preserved. Store1 duplicates are mapped to Part1 rows.
      - Re-running this batch does not create duplicate rows.
      - A conflicting primary key or business key stops the batch without mutation.
      - Batch 14 adds one Store 3 demo POS staff account; no Location data is inserted.
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
GO



IF OBJECT_ID(N'dbo.DrinkCategories', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Drinks', N'U') IS NULL
   OR OBJECT_ID(N'dbo.DrinkImages', N'U') IS NULL
   OR OBJECT_ID(N'dbo.DrinkSizes', N'U') IS NULL
    THROW 52002, N'Schema thiếu một trong các bảng của SeedAll Batch 01.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.ProductTypes WHERE ProductTypeId = 1 AND Code = N'HANDCRAFTED' AND Active = 1)
   OR NOT EXISTS (SELECT 1 FROM dbo.ProductTypes WHERE ProductTypeId = 2 AND Code = N'RETAIL' AND Active = 1)
    THROW 52003, N'Thiếu ProductType nền HANDCRAFTED/RETAIL.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.Sizes WHERE SizeId = 1 AND SizeCode = N'S' AND Active = 1)
   OR NOT EXISTS (SELECT 1 FROM dbo.Sizes WHERE SizeId = 2 AND SizeCode = N'M' AND Active = 1)
   OR NOT EXISTS (SELECT 1 FROM dbo.Sizes WHERE SizeId = 3 AND SizeCode = N'L' AND Active = 1)
   OR NOT EXISTS (SELECT 1 FROM dbo.Sizes WHERE SizeId = 5 AND SizeCode = N'150ML' AND Active = 1)
    THROW 52004, N'Thiếu Size nền S/M/L/150ML được Part1 sử dụng.', 1;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    /* ============================================================
       01. DRINK CATEGORIES

       Source analysis:
       - EF HasData: IDs 1-3.
       - Part1: IDs 4-8, retained without changing ID/code/name/value.
       - Store1 duplicate DEMO_CAT_FRUIT_TEA is mapped to TRATRAICAY.
       - Store1 duplicate DEMO_CAT_FRAPPE is mapped to DAXAY.
       - Five non-duplicate Store1 categories receive deterministic IDs 9-13.
       Final expected count on a clean migrated database: 13.
       ============================================================ */

    DECLARE @CategorySeed TABLE
    (
        CategoryId int NOT NULL PRIMARY KEY,
        CategoryCode nvarchar(30) NOT NULL UNIQUE,
        Name nvarchar(150) NOT NULL UNIQUE,
        Icon nvarchar(10) NULL,
        Active bit NOT NULL
    );

    INSERT @CategorySeed(CategoryId, CategoryCode, Name, Icon, Active)
    VALUES
        (4, N'TRATRAICAY', N'Trà trái cây', N'🍹', 1),
        (5, N'TRANONG', N'Trà nóng', N'🍵', 1),
        (6, N'NUOCEP', N'Nước ép', N'🧃', 1),
        (7, N'DAXAY', N'Đá xay', N'🥤', 1),
        (8, N'SUACHUA', N'Sữa chua', N'🍶', 1),
        (9, N'DEMO_CAT_VIET_COFFEE', N'Cà phê Việt', N'☕', 1),
        (10, N'DEMO_CAT_MODERN_COFFEE', N'Cà phê hiện đại', N'🥛', 1),
        (11, N'DEMO_CAT_MILK_TEA', N'Trà sữa thủ công', N'🧋', 1),
        (12, N'DEMO_CAT_MATCHA_LATTE', N'Matcha & Latte', N'🍵', 1),
        (13, N'DEMO_CAT_TOPPING', N'Topping', N'➕', 1);

    IF EXISTS
    (
        SELECT 1
        FROM @CategorySeed x
        JOIN dbo.DrinkCategories c
          ON c.CategoryId = x.CategoryId
          OR c.CategoryCode = x.CategoryCode
          OR c.Name = x.Name
        WHERE c.CategoryId <> x.CategoryId
           OR c.CategoryCode <> x.CategoryCode
           OR c.Name <> x.Name
           OR ISNULL(c.Icon, N'') <> ISNULL(x.Icon, N'')
           OR c.Active <> x.Active
    )
        THROW 52010, N'DrinkCategories có ID/Code/Name xung đột với SeedAll.', 1;

    SET IDENTITY_INSERT dbo.DrinkCategories ON;

    INSERT dbo.DrinkCategories(CategoryId, CategoryCode, Name, Icon, Active)
    SELECT x.CategoryId, x.CategoryCode, x.Name, x.Icon, x.Active
    FROM @CategorySeed x
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.DrinkCategories c WHERE c.CategoryId = x.CategoryId
    );

    SET IDENTITY_INSERT dbo.DrinkCategories OFF;

    /* Duplicate decisions retained as executable documentation. */
    DECLARE @CategoryAliases TABLE
    (
        SourceCode nvarchar(30) PRIMARY KEY,
        CanonicalCode nvarchar(30) NOT NULL,
        Reason nvarchar(300) NOT NULL
    );

    INSERT @CategoryAliases(SourceCode, CanonicalCode, Reason)
    VALUES
        (N'DEMO_CAT_FRUIT_TEA', N'TRATRAICAY', N'Trùng tên và ý nghĩa nghiệp vụ với Part1; giữ Part1.'),
        (N'DEMO_CAT_FRAPPE', N'DAXAY', N'Trùng tên và ý nghĩa nghiệp vụ với Part1; giữ Part1.');

    /* ============================================================
       02. DRINKS

       Mapping of duplicate Store1 products:
       DEMO_DRINK_BAC_XIU          -> CF_BacXiu
       DEMO_DRINK_AMERICANO        -> CF_Americano
       DEMO_DRINK_PEACH_ORANGE_TEA -> TTC_CamSa
       DEMO_DRINK_LYCHEE_TEA       -> TTC_Vai
       DEMO_DRINK_OOLONG_MILK_TEA  -> TS_OLong

       IDs 7-30 are the unchanged Part1 rows.
       IDs 31-39 are non-duplicate Store1 rows.
       IDs 40-50 are meaningful extension products from the approved plan.
       ============================================================ */

    DECLARE @DrinkSeed TABLE
    (
        DrinkId int NOT NULL PRIMARY KEY,
        CategoryId int NOT NULL,
        ProductTypeId int NOT NULL,
        DrinkCode nvarchar(50) NOT NULL UNIQUE,
        Name nvarchar(200) NOT NULL UNIQUE,
        Description nvarchar(1000) NULL,
        Active bit NOT NULL,
        CreatedAt datetime2(0) NOT NULL,
        CalculatedCogs decimal(18,2) NULL
    );

    INSERT @DrinkSeed
    (DrinkId, CategoryId, ProductTypeId, DrinkCode, Name, Description, Active, CreatedAt, CalculatedCogs)
    VALUES
        (7, 1, 1, N'CF_BacXiu', N'Bạc xỉu', N'Cà phê sữa nhiều sữa, vị béo ngọt nhẹ.', 1, '2025-01-01', NULL),
        (8, 1, 1, N'CF_Latte', N'Latte', N'Cà phê espresso kết hợp sữa tươi.', 1, '2025-01-01', NULL),
        (9, 1, 1, N'CF_Cappuccino', N'Cappuccino', N'Cà phê espresso cùng bọt sữa dày.', 1, '2025-01-01', NULL),
        (10, 1, 1, N'CF_Americano', N'Americano', N'Cà phê espresso pha loãng với nước nóng.', 1, '2025-01-01', NULL),
        (11, 1, 1, N'CF_ColdBrew', N'Cold Brew', N'Cà phê ủ lạnh vị thanh nhẹ.', 1, '2025-01-01', NULL),
        (12, 2, 1, N'TS_Matcha', N'Trà sữa matcha', N'Trà sữa vị matcha thơm dịu.', 1, '2025-01-01', NULL),
        (13, 2, 1, N'TS_KhoaiMon', N'Trà sữa khoai môn', N'Trà sữa vị khoai môn béo nhẹ.', 1, '2025-01-01', NULL),
        (14, 2, 1, N'TS_OLong', N'Trà sữa ô long', N'Trà sữa pha từ trà ô long đậm vị.', 1, '2025-01-01', NULL),
        (15, 2, 1, N'TS_Caramel', N'Trà sữa caramel', N'Trà sữa kết hợp caramel ngọt thơm.', 1, '2025-01-01', NULL),
        (16, 3, 2, N'PEPSI', N'Pepsi', N'Nước ngọt có gas Pepsi mát lạnh.', 1, '2025-01-01', NULL),
        (17, 3, 2, N'SPRITE', N'Sprite', N'Nước ngọt có gas vị chanh Sprite.', 1, '2025-01-01', NULL),
        (18, 3, 2, N'7UP', N'7 Up', N'Nước ngọt có gas vị chanh 7 Up.', 1, '2025-01-01', NULL),
        (19, 3, 2, N'FANTACAM', N'Fanta cam', N'Nước ngọt có gas vị cam Fanta.', 1, '2025-01-01', NULL),
        (20, 3, 2, N'AQUAFINA', N'Aquafina', N'Nước suối đóng chai Aquafina.', 1, '2025-01-01', NULL),
        (21, 4, 1, N'TTC_CamSa', N'Trà đào cam sả', N'Trà đào kết hợp cam và sả thơm mát.', 1, '2025-01-01', NULL),
        (22, 4, 1, N'TTC_Vai', N'Trà vải', N'Trà vải ngọt thanh, dùng lạnh.', 1, '2025-01-01', NULL),
        (23, 4, 1, N'TTC_Chanh', N'Trà chanh', N'Trà chanh thanh mát giải khát.', 1, '2025-01-01', NULL),
        (24, 4, 1, N'TTC_Dau', N'Trà dâu', N'Trà dâu vị chua ngọt dễ uống.', 1, '2025-01-01', NULL),
        (25, 5, 1, N'TN_HongTra', N'Hồng trà nóng', N'Hồng trà nóng truyền thống.', 1, '2025-01-01', NULL),
        (26, 5, 1, N'TN_TraGungMatOng', N'Trà gừng mật ong', N'Trà nóng với gừng và mật ong.', 1, '2025-01-01', NULL),
        (27, 6, 1, N'NE_Cam', N'Nước cam ép', N'Nước cam tươi nguyên chất.', 1, '2025-01-01', NULL),
        (28, 6, 1, N'NE_ChanhDay', N'Nước chanh dây', N'Nước chanh dây chua ngọt mát lạnh.', 1, '2025-01-01', NULL),
        (29, 8, 1, N'SC_Dau', N'Sữa chua dâu', N'Sữa chua vị dâu mát lạnh.', 1, '2025-01-01', NULL),
        (30, 8, 1, N'SC_VietQuat', N'Sữa chua việt quất', N'Sữa chua vị việt quất thơm ngon.', 1, '2025-01-01', NULL),
        (31, 9, 1, N'DEMO_DRINK_VIET_BLACK', N'Cà phê đen đá', N'Dữ liệu demo Store 1 - Cà phê đen đá', 1, '2026-01-01', NULL),
        (32, 9, 1, N'DEMO_DRINK_VIET_MILK', N'Cà phê sữa đá', N'Dữ liệu demo Store 1 - Cà phê sữa đá', 1, '2026-01-01', NULL),
        (33, 9, 1, N'DEMO_DRINK_SALTED_COFFEE', N'Cà phê muối', N'Dữ liệu demo Store 1 - Cà phê muối', 1, '2026-01-01', NULL),
        (34, 10, 1, N'DEMO_DRINK_COFFEE_LATTE', N'Latte cà phê', N'Dữ liệu demo Store 1 - Latte cà phê', 1, '2026-01-01', NULL),
        (35, 4, 1, N'DEMO_DRINK_PASSION_TEA', N'Trà chanh dây', N'Dữ liệu demo Store 1 - Trà chanh dây', 1, '2026-01-01', NULL),
        (36, 11, 1, N'DEMO_DRINK_TRAD_MILK_TEA', N'Trà sữa truyền thống đặc biệt', N'Dữ liệu demo Store 1 - Trà sữa truyền thống đặc biệt', 1, '2026-01-01', NULL),
        (37, 12, 1, N'DEMO_DRINK_MATCHA_LATTE', N'Matcha latte', N'Dữ liệu demo Store 1 - Matcha latte', 1, '2026-01-01', NULL),
        (38, 12, 1, N'DEMO_DRINK_CHOCOLATE_LATTE', N'Chocolate latte', N'Dữ liệu demo Store 1 - Chocolate latte', 1, '2026-01-01', NULL),
        (39, 7, 1, N'DEMO_DRINK_MATCHA_FRAPPE', N'Matcha đá xay', N'Dữ liệu demo Store 1 - Matcha đá xay', 1, '2026-01-01', NULL),
        (40, 10, 1, N'DEMO_DRINK_COLD_BREW_ORANGE', N'Cold brew cam', N'Cold brew kết hợp cam tươi, vị thanh và ít ngọt.', 1, '2026-01-01', NULL),
        (41, 10, 1, N'DEMO_DRINK_MOCHA', N'Mocha', N'Espresso, chocolate và sữa tươi.', 1, '2026-01-01', NULL),
        (42, 10, 1, N'DEMO_DRINK_CARAMEL_MACCHIATO', N'Caramel macchiato', N'Espresso, sữa tươi và syrup caramel.', 1, '2026-01-01', NULL),
        (43, 9, 1, N'DEMO_DRINK_COCONUT_COFFEE', N'Cà phê dừa', N'Cốt cà phê Việt kết hợp nước cốt dừa.', 1, '2026-01-01', NULL),
        (44, 4, 1, N'DEMO_DRINK_HONEY_LEMON_TEA', N'Trà chanh mật ong', N'Trà đen, chanh vàng và mật ong.', 1, '2026-01-01', NULL),
        (45, 4, 1, N'DEMO_DRINK_MANGO_TEA', N'Trà xoài', N'Trà đen kết hợp puree xoài.', 1, '2026-01-01', NULL),
        (46, 11, 1, N'DEMO_DRINK_STRAWBERRY_MILK_TEA', N'Trà sữa dâu', N'Trà đen, puree dâu và sữa tươi; khác sản phẩm Trà dâu của Part1.', 1, '2026-01-01', NULL),
        (47, 4, 1, N'DEMO_DRINK_LYCHEE_OOLONG', N'Trà ô long vải', N'Cốt trà ô long kết hợp vải ngâm.', 1, '2026-01-01', NULL),
        (48, 12, 1, N'DEMO_DRINK_OAT_MATCHA', N'Matcha sữa yến mạch', N'Matcha kết hợp sữa yến mạch.', 1, '2026-01-01', NULL),
        (49, 12, 1, N'DEMO_DRINK_COCONUT_CHOCOLATE', N'Chocolate dừa', N'Chocolate kết hợp nước cốt dừa và sữa tươi.', 1, '2026-01-01', NULL),
        (50, 8, 1, N'DEMO_DRINK_PASSION_YOGURT', N'Sữa chua chanh dây', N'Sữa chua kết hợp mứt chanh dây.', 1, '2026-01-01', NULL),
        (51, 10, 1, N'ZZ_DRINK_CHEESE_CREAM_COFFEE', N'Cà phê kem cheese', N'Espresso kết hợp lớp kem cheese mặn béo.', 1, '2026-01-01', NULL),
        (52, 10, 1, N'ZZ_DRINK_HONEY_LEMON_COLD_BREW', N'Cold brew mật ong chanh vàng', N'Cold brew thanh nhẹ kết hợp mật ong và chanh vàng.', 1, '2026-01-01', NULL),
        (53, 9, 1, N'ZZ_DRINK_BLACK_PEARL_MILK_COFFEE', N'Cà phê sữa trân châu đen',  N'Cà phê sữa Việt kết hợp trân châu đen đã nấu.', 1, '2026-01-01', NULL), 
        (54, 10, 1, N'ZZ_DRINK_HONEY_OAT_ESPRESSO', N'Espresso mật ong yến mạch', N'Espresso kết hợp sữa yến mạch và mật ong.', 1, '2026-01-01', NULL), 
        (55, 9, 1, N'ZZ_DRINK_FLAN_MILK_COFFEE', N'Cà phê sữa flan', N'Cà phê sữa Việt dùng cùng bánh flan caramel.', 1, '2026-01-01', NULL),
        (56, 10, 1, N'ZZ_DRINK_LYCHEE_ALOE_COLD_BREW', N'Cold brew vải nha đam', N'Cold brew kết hợp vải ngâm và nha đam.', 1, '2026-01-01', NULL),
        (57, 10, 1, N'ZZ_DRINK_SALTED_COCONUT_ESPRESSO', N'Espresso dừa kem muối', N'Espresso, nước cốt dừa và lớp kem muối.', 1, '2026-01-01', NULL),
        (58, 9, 1, N'ZZ_DRINK_BROWN_SUGAR_COCONUT_JELLY_COFFEE', N'Cà phê đường đen thạch dừa', N'Cà phê Việt kết hợp đường đen và thạch dừa.', 1, '2026-01-01', NULL),
        (59, 9, 1, N'ZZ_DRINK_KHUC_BACH_MILK_COFFEE', N'Cà phê sữa khúc bạch', N'Cà phê sữa Việt dùng cùng khúc bạch.', 1, '2026-01-01', NULL),
        (60, 10, 1, N'ZZ_DRINK_MANGO_PASSION_COLD_BREW', N'Cold brew xoài chanh dây', N'Cold brew kết hợp puree xoài và chanh dây.', 1, '2026-01-01', NULL),
        (61, 4, 1, N'ZZ_DRINK_PEACH_ALOE_OOLONG', N'Ô long đào nha đam', N'Trà ô long kết hợp đào ngâm và nha đam.', 1, '2026-01-01', NULL),
        (62, 4, 1, N'ZZ_DRINK_LYCHEE_CHIA_BLACK_TEA', N'Hồng trà vải hạt chia', N'Hồng trà kết hợp vải ngâm và hạt chia.', 1, '2026-01-01', NULL),
        (63, 4, 1, N'ZZ_DRINK_MANGO_COCONUT_JELLY_OOLONG', N'Ô long xoài thạch dừa', N'Trà ô long kết hợp puree xoài và thạch dừa.', 1, '2026-01-01', NULL),
        (64, 4, 1, N'ZZ_DRINK_ORANGE_ALOE_BLACK_TEA', N'Hồng trà cam nha đam', N'Hồng trà, cam tươi và nha đam.', 1, '2026-01-01', NULL),
        (65, 4, 1, N'ZZ_DRINK_PASSION_CHIA_TEA', N'Trà chanh dây hạt chia', N'Trà đen kết hợp chanh dây và hạt chia.', 1, '2026-01-01', NULL),
        (66, 4, 1, N'ZZ_DRINK_STRAWBERRY_COCONUT_JELLY_OOLONG', N'Ô long dâu thạch dừa', N'Trà ô long kết hợp puree dâu và thạch dừa.', 1, '2026-01-01', NULL),
        (67, 4, 1, N'ZZ_DRINK_PEACH_KHUC_BACH_TEA', N'Trà đào khúc bạch', N'Trà đen, đào ngâm và khúc bạch.', 1, '2026-01-01', NULL),
        (68, 4, 1, N'ZZ_DRINK_LYCHEE_ALOE_TEA', N'Trà vải nha đam', N'Trà đen kết hợp vải ngâm và nha đam.', 1, '2026-01-01', NULL),
        (69, 4, 1, N'ZZ_DRINK_MANGO_CHIA_TEA', N'Trà xoài hạt chia', N'Trà đen kết hợp puree xoài và hạt chia.', 1, '2026-01-01', NULL),
        (70, 4, 1, N'ZZ_DRINK_ORANGE_PASSION_TEA', N'Trà cam chanh dây', N'Trà đen kết hợp cam tươi và chanh dây.', 1, '2026-01-01', NULL),
        (71, 11, 1, N'ZZ_DRINK_BROWN_SUGAR_PEARL_MILK_TEA', N'Trà sữa đường đen trân châu', N'Trà sữa kết hợp đường đen và trân châu đen.', 1, '2026-01-01', NULL),
        (72, 11, 1, N'ZZ_DRINK_FLAN_MILK_TEA', N'Trà sữa flan', N'Trà sữa thủ công dùng cùng bánh flan.', 1, '2026-01-01', NULL),
        (73, 11, 1, N'ZZ_DRINK_KHUC_BACH_MILK_TEA', N'Trà sữa khúc bạch', N'Trà sữa thủ công kết hợp khúc bạch.', 1, '2026-01-01', NULL),
        (74, 11, 1, N'ZZ_DRINK_ALOE_MILK_TEA', N'Trà sữa nha đam', N'Trà sữa thủ công kết hợp nha đam.', 1, '2026-01-01', NULL),
        (75, 11, 1, N'ZZ_DRINK_COCONUT_JELLY_MILK_TEA', N'Trà sữa thạch dừa', N'Trà sữa thủ công kết hợp thạch dừa.', 1, '2026-01-01', NULL),
        (76, 11, 1, N'ZZ_DRINK_CHEESE_CREAM_MILK_TEA', N'Trà sữa kem cheese', N'Trà sữa thủ công phủ lớp kem cheese.', 1, '2026-01-01', NULL),
        (77, 12, 1, N'ZZ_DRINK_STRAWBERRY_CHEESE_MATCHA', N'Matcha dâu kem cheese', N'Matcha kết hợp puree dâu và lớp kem cheese.', 1, '2026-01-01', NULL),
        (78, 12, 1, N'ZZ_DRINK_MANGO_COCONUT_JELLY_MATCHA', N'Matcha xoài thạch dừa', N'Matcha kết hợp puree xoài và thạch dừa.', 1, '2026-01-01', NULL),
        (79, 12, 1, N'ZZ_DRINK_SALTED_CARAMEL_CHOCOLATE', N'Chocolate caramel kem muối', N'Chocolate kết hợp caramel và lớp kem muối.', 1, '2026-01-01', NULL),
        (80, 8, 1, N'ZZ_DRINK_MANGO_ALOE_YOGURT', N'Sữa chua xoài nha đam', N'Sữa chua kết hợp puree xoài và nha đam.', 1, '2026-01-01', NULL);

    IF EXISTS
    (
        SELECT 1
        FROM @DrinkSeed x
        JOIN dbo.Drinks d
          ON d.DrinkId = x.DrinkId
          OR d.DrinkCode = x.DrinkCode
          OR d.Name = x.Name
        WHERE d.DrinkId <> x.DrinkId
           OR d.CategoryId <> x.CategoryId
           OR d.ProductTypeId <> x.ProductTypeId
           OR d.DrinkCode <> x.DrinkCode
           OR d.Name <> x.Name
           OR ISNULL(d.Description, N'') <> ISNULL(x.Description, N'')
           OR d.Active <> x.Active
           OR d.CreatedAt <> x.CreatedAt
           OR ISNULL(d.CalculatedCogs, -1) <> ISNULL(x.CalculatedCogs, -1)
    )
        THROW 52011, N'Drinks có ID/Code/Name xung đột với SeedAll.', 1;

    SET IDENTITY_INSERT dbo.Drinks ON;

    INSERT dbo.Drinks
    (DrinkId, CategoryId, ProductTypeId, DrinkCode, Name, Description, Active, CreatedAt, CalculatedCogs)
    SELECT x.DrinkId, x.CategoryId, x.ProductTypeId, x.DrinkCode, x.Name,
           x.Description, x.Active, x.CreatedAt, x.CalculatedCogs
    FROM @DrinkSeed x
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Drinks d WHERE d.DrinkId = x.DrinkId);

    SET IDENTITY_INSERT dbo.Drinks OFF;

    DECLARE @DrinkAliases TABLE
    (
        SourceCode nvarchar(50) PRIMARY KEY,
        CanonicalCode nvarchar(50) NOT NULL,
        Reason nvarchar(300) NOT NULL
    );

    INSERT @DrinkAliases(SourceCode, CanonicalCode, Reason)
    VALUES
        (N'DEMO_DRINK_BAC_XIU', N'CF_BacXiu', N'Trùng tên và sản phẩm với Part1; giữ DrinkId 7 và giá Part1.'),
        (N'DEMO_DRINK_AMERICANO', N'CF_Americano', N'Trùng tên và sản phẩm với Part1; giữ DrinkId 10 và giá Part1.'),
        (N'DEMO_DRINK_PEACH_ORANGE_TEA', N'TTC_CamSa', N'Trùng tên và sản phẩm với Part1; giữ DrinkId 21 và giá Part1.'),
        (N'DEMO_DRINK_LYCHEE_TEA', N'TTC_Vai', N'Trùng tên và sản phẩm với Part1; giữ DrinkId 22 và giá Part1.'),
        (N'DEMO_DRINK_OOLONG_MILK_TEA', N'TS_OLong', N'Trùng tên và sản phẩm với Part1; giữ DrinkId 14 và giá Part1.');

    /* ============================================================
       03. DRINK IMAGES
       Part1 IDs 25-120 are preserved exactly. Store1 and the extension
       do not supply image assets, so no synthetic URLs are invented.
       ============================================================ */

    DECLARE @DrinkImageSeed TABLE
    (
        DrinkImageId int NOT NULL PRIMARY KEY,
        DrinkId int NOT NULL,
        ImageUrl nvarchar(1000) NOT NULL,
        PublicId nvarchar(300) NOT NULL,
        CreatedAt datetime2(0) NOT NULL,
        IsDefault bit NOT NULL
    );

    INSERT @DrinkImageSeed
    (DrinkImageId, DrinkId, ImageUrl, PublicId, CreatedAt, IsDefault)
    VALUES
        (25,7,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803073/bacxiu1_q6fhhe.jpg',N'bacxiu1_q6fhhe','2025-01-01',1),
        (26,7,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803074/bacxiu2_atahke.jpg',N'bacxiu2_atahke','2025-01-01',0),
        (27,7,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803074/bacxiu3_brxajy.jpg',N'bacxiu3_brxajy','2025-01-01',0),
        (28,7,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803075/bacxiu4_ayhqox.jpg',N'bacxiu4_ayhqox','2025-01-01',0),
        (29,8,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803289/latte1_ifyn8n.jpg',N'latte1_ifyn8n','2025-01-01',1),
        (30,8,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803292/latte2_ofwhe2.jpg',N'latte2_ofwhe2.jpg','2025-01-01',0),
        (31,8,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803313/latte3_tqw6oj.jpg',N'latte3_tqw6oj','2025-01-01',0),
        (32,8,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803294/latte4_lkbvhn.jpg',N'latte4_lkbvhn','2025-01-01',0),
        (33,9,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803078/cappuchino1_fnkswe.jpg',N'cappuchino1_fnkswe','2025-01-01',1),
        (34,9,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803078/cappuchino2_iq4cwh.jpg',N'cappuchino2_iq4cwh','2025-01-01',0),
        (35,9,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803079/cappuchino3_wssf8t.jpg',N'cappuchino3_wssf8t','2025-01-01',0),
        (36,9,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803080/cappuchino4_wfpips.jpg',N'cappuchino4_wfpips','2025-01-01',0),
        (37,10,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803068/americano1_yaaozq.jpg',N'americano1_yaaozq','2025-01-01',1),
        (38,10,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803069/americano2_tbhq5n.jpg',N'americano2_tbhq5n','2025-01-01',0),
        (39,10,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803069/americano3_gdsrdz.jpg',N'americano3_gdsrdz','2025-01-01',0),
        (40,10,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803070/americano4_uo3o7l.jpg',N'americano4_uo3o7l','2025-01-01',0),
        (41,11,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803222/coldbrew1_qxl7om.jpg',N'coldbrew1_qxl7om','2025-01-01',1),
        (42,11,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803223/coldbrew2_rajsaf.jpg',N'coldbrew2_rajsaf','2025-01-01',0),
        (43,11,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803223/coldbrew3_xofgcq.jpg',N'coldbrew3_xofgcq','2025-01-01',0),
        (44,11,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803224/coldbrew4_rxeehn.jpg',N'coldbrew4_rxeehn','2025-01-01',0),
        (45,12,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779802887/trasuamatcha1_adzwz9.jpg',N'trasuamatcha1_adzwz9','2025-01-01',1),
        (46,12,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779802887/trasuamatcha2_wiqapx.jpg',N'trasuamatcha2_wiqapx','2025-01-01',0),
        (47,12,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779802887/trasuamatcha3_zu39ls.jpg',N'trasuamatcha3_zu39ls','2025-01-01',0),
        (48,12,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779802888/trasuamatcha4_pmuk2u.jpg',N'trasuamatcha4_pmuk2u','2025-01-01',0),
        (49,13,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803385/trasuakhoaimon1_xhuzjd.jpg',N'trasuakhoaimon1_xhuzjd','2025-01-01',1),
        (50,13,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779802885/trasuakhoaimon2_porpv4.jpg',N'trasuakhoaimon2_porpv4','2025-01-01',0),
        (51,13,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779802886/trasuakhoaimon3_oazwaw.jpg',N'trasuakhoaimon3_oazwaw','2025-01-01',0),
        (52,13,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779802886/trasuakhoaimon4_rguwhh.jpg',N'trasuakhoaimon4_rguwhh','2025-01-01',0),
        (53,14,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779802888/trasuaolong1_avbiod.jpg',N'trasuaolong1_avbiod','2025-01-01',1),
        (54,14,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779802889/trasuaolong2_qadazn.jpg',N'trasuaolong2_qadazn','2025-01-01',0),
        (55,14,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779802889/trasuaolong3_iycj0o.jpg',N'trasuaolong3_iycj0o','2025-01-01',0),
        (56,14,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779802890/trasuaolong4_steo3o.jpg',N'trasuaolong4_steo3o','2025-01-01',0),
        (57,15,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803382/trasuacaramel1_vzkq6l.jpg',N'trasuacaramel1_vzkq6l','2025-01-01',1),
        (58,15,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803383/trasuacaramel2_zdghmk.jpg',N'trasuacaramel2_zdghmk','2025-01-01',0),
        (59,15,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803383/trasuacaramel3_j89qc3.jpg',N'trasuacaramel3_j89qc3','2025-01-01',0),
        (60,15,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803384/trasuacaramel4_ncqvpy.jpg',N'trasuacaramel4_ncqvpy','2025-01-01',0),
        (61,16,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803297/pepsi1_mwqrut.jpg',N'pepsi1_mwqrut','2025-01-01',1),
        (62,16,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803299/pepsi2_pi9aig.jpg',N'pepsi2_pi9aig','2025-01-01',0),
        (63,16,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803304/pepsi3_jxow1z.jpg',N'pepsi3_jxow1z','2025-01-01',0),
        (64,16,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803306/pepsi4_dpham4.jpg',N'pepsi4_dpham4','2025-01-01',0),
        (65,17,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803307/sprite1_i0vshk.jpg',N'sprite1_i0vshk','2025-01-01',1),
        (66,17,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803310/sprite2_v76ugn.jpg',N'sprite2_v76ugn','2025-01-01',0),
        (67,17,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803311/sprite3_tkqebj.jpg',N'sprite3_tkqebj','2025-01-01',0),
        (68,17,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803317/sprite4_fkuzps.jpg',N'sprite4_fkuzps','2025-01-01',0),
        (69,18,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803066/7up1_t7wqoe.jpg',N'7up1_t7wqoe','2025-01-01',1),
        (70,18,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803066/7up2_x1wk6m.jpg',N'7up2_x1wk6m','2025-01-01',0),
        (71,18,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803067/7up3_vifoed.jpg',N'7up3_vifoed','2025-01-01',0),
        (72,18,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803068/7up4_ndseyj.jpg',N'7up4_ndseyj','2025-01-01',0),
        (73,19,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803280/fanta1_aipxdb.jpg',N'fanta1_aipxdb','2025-01-01',1),
        (74,19,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803282/fanta2_q1hdyd.jpg',N'fanta2_q1hdyd','2025-01-01',0),
        (75,19,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803282/fanta3_a4otsc.jpg',N'fanta3_a4otsc','2025-01-01',0),
        (76,19,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803282/fanta4_yinqc2.jpg',N'fanta4_yinqc2','2025-01-01',0),
        (77,20,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803071/aquafina1_fgtxjk.jpg',N'aquafina1_fgtxjk','2025-01-01',1),
        (78,20,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803071/aquafina2_sqn9xg.jpg',N'aquafina2_sqn9xg','2025-01-01',0),
        (79,20,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803072/aquafina3_w59pij.jpg',N'aquafina3_w59pij','2025-01-01',0),
        (80,20,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803072/auquafina4_sfykzy.jpg',N'auquafina4_sfykzy','2025-01-01',0),
        (81,21,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803359/tradaocamsa1_gtgy9v.jpg',N'tradaocamsa1_gtgy9v','2025-01-01',1),
        (82,21,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803376/tradaocamsa2_a3uya1.jpg',N'tradaocamsa2_a3uya1','2025-01-01',0),
        (83,21,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803376/tradaocamsa3_w2iaxq.jpg',N'tradaocamsa3_w2iaxq','2025-01-01',0),
        (84,21,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803377/tradaocamsa4_xvruyb.jpg',N'tradaocamsa4_xvruyb','2025-01-01',0),
        (85,22,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803064/travai1_gjgy4i.jpg',N'travai1_gjgy4i','2025-01-01',1),
        (86,22,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803064/travai2_puznwn.jpg',N'travai2_puznwn','2025-01-01',0),
        (87,22,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803065/travai3_aak0m3.jpg',N'travai3_aak0m3','2025-01-01',0),
        (88,22,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803065/travai4_qmedkw.jpg',N'travai4_qmedkw','2025-01-01',0),
        (89,23,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803319/trachanh1_iguhan.jpg',N'trachanh1_iguhan','2025-01-01',1),
        (90,23,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803319/trachanh2_ysbvoa.jpg',N'trachanh2_ysbvoa','2025-01-01',0),
        (91,23,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803320/trachanh3_qvt4di.jpg',N'trachanh3_qvt4di','2025-01-01',0),
        (92,23,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803320/trachanh4_anhyev.jpg',N'trachanh4_anhyev','2025-01-01',0),
        (93,24,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803377/tradau1_eyyoss.jpg',N'tradau1_eyyoss','2025-01-01',1),
        (94,24,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803378/tradau2_pobg4v.jpg',N'tradau2_pobg4v','2025-01-01',0),
        (95,24,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803379/tradau3_n051tu.jpg',N'tradau3_n051tu','2025-01-01',0),
        (96,24,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803379/tradau4_adiaxt.jpg',N'tradau4_adiaxt','2025-01-01',0),
        (97,25,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803392/hongtra1_ceyimn.jpg',N'hongtra1_ceyimn','2025-01-01',1),
        (98,25,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803286/hongtra2_o0886y.jpg',N'hongtra2_o0886y','2025-01-01',0),
        (99,25,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803287/hongtra3_ahkurl.jpg',N'hongtra3_ahkurl','2025-01-01',0),
        (100,25,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803288/hongtra4_zulo0e.jpg',N'hongtra4_zulo0e','2025-01-01',0),
        (101,26,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803380/tragungmatong1_xccyqg.jpg',N'tragungmatong1_xccyqg','2025-01-01',1),
        (102,26,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803381/tragungmatong2_s6yy53.jpg',N'tragungmatong2_s6yy53','2025-01-01',0),
        (103,26,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803381/tragungmatong3_n1k20v.jpg',N'tragungmatong3_n1k20v','2025-01-01',0),
        (104,26,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803382/tragungmatong4_jxm8yp.jpg',N'tragungmatong4_jxm8yp','2025-01-01',0),
        (105,27,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803075/camep1_yqwkhh.jpg',N'camep1_yqwkhh','2025-01-01',1),
        (106,27,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803076/camep2_wvicee.jpg',N'camep2_wvicee','2025-01-01',0),
        (107,27,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803076/camep3_vbwstf.jpg',N'camep3_vbwstf','2025-01-01',0),
        (108,27,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803077/camep4_wnn5ws.jpg',N'camep4_wnn5ws','2025-01-01',0),
        (109,28,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803278/chanhday1_ghggxh.jpg',N'chanhday1_ghggxh','2025-01-01',1),
        (110,28,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803279/chanhday2_oc6r7u.jpg',N'chanhday2_oc6r7u','2025-01-01',0),
        (111,28,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803279/chanhday3_fdaoci.jpg',N'chanhday3_fdaoci','2025-01-01',0),
        (112,28,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803280/chanhday4_yav5tv.jpg',N'chanhday4_yav5tv','2025-01-01',0),
        (113,29,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803320/suachuadau1_jketgw.jpg',N'suachuadau1_jketgw','2025-01-01',1),
        (114,29,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803318/suachuadau2_hfder5.jpg',N'suachuadau2_hfder5','2025-01-01',0),
        (115,29,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803317/suachuadau3_ecsf4t.jpg',N'suachuadau3_ecsf4t','2025-01-01',0),
        (116,29,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803317/suachuadau4_myyptt.jpg',N'suachuadau4_myyptt','2025-01-01',0),
        (117,30,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803319/suachuavietquat1_gqwvhs.jpg',N'suachuavietquat1_gqwvhs','2025-01-01',1),
        (118,30,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803318/suachuavietquat2_t07rjy.jpg',N'suachuavietquat2_t07rjy','2025-01-01',0),
        (119,30,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803318/suachuavietquat3_olncpb.jpg',N'suachuavietquat3_olncpb','2025-01-01',0),
        (120,30,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803320/suachuavietquat4_bsjzb3.jpg',N'suachuavietquat4_bsjzb3','2025-01-01',0),
        (121,31,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877829/caphedenda1_qzwiut.jpg',N'caphedenda1_qzwiut','2025-01-01',1),
        (122,31,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877830/caphedenda2_sp3iqs.webp',N'caphedenda2_sp3iqs','2025-01-01',0),
        (123,31,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877830/caphedenda3_qh4s4e.jpg',N'caphedenda3_qh4s4e','2025-01-01',0),
        (124,31,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877830/caphedenda4_ccfejc.webp',N'caphedenda4_ccfejc','2025-01-01',0),

        (125,32,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877837/caphesuada1_wcyhpi.jpg',N'caphesuada1_wcyhpi','2025-01-01',1),
        (126,32,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877837/caphesuada2_vnleni.jpg',N'caphesuada2_vnleni','2025-01-01',0),
        (127,32,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877838/caphesuada3_hn2jzy.jpg',N'caphesuada3_hn2jzy','2025-01-01',0),
        (128,32,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877839/caphesuada4_ur5g2k.jpg',N'caphesuada4_ur5g2k','2025-01-01',0),

        (129,33,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877834/caphemuoi1_bsknve.jpg',N'caphemuoi1_bsknve','2025-01-01',1),
        (130,33,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877835/caphemuoi2_xzrh0q.jpg',N'caphemuoi2_xzrh0q','2025-01-01',0),
        (131,33,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877835/caphemuoi3_v0ufhk.jpg',N'caphemuoi3_v0ufhk','2025-01-01',0),
        (132,33,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877836/caphemuoi4_e0ukln.webp',N'caphemuoi4_e0ukln','2025-01-01',0),

        (133,34,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877850/lattecaphe1_tr0rcq.jpg',N'lattecaphe1_tr0rcq','2025-01-01',1),
        (134,34,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877850/lattecaphe2_mha2yf.webp',N'lattecaphe2_mha2yf','2025-01-01',0),
        (135,34,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877851/lattecaphe3_sguszk.webp',N'lattecaphe3_sguszk','2025-01-01',0),
        (136,34,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877851/lattecaphe4_pyevbw.jpg',N'lattecaphe4_pyevbw','2025-01-01',0),

        (137,35,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877902/trachanhday1_dnydos.jpg',N'trachanhday1_dnydos','2025-01-01',1),
        (138,35,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877902/trachanhday2_decbve.jpg',N'trachanhday2_decbve','2025-01-01',0),
        (139,35,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877903/trachanhday3_pdoird.jpg',N'trachanhday3_pdoird','2025-01-01',0),
        (140,35,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877904/trachanhday4_duis9w.jpg',N'trachanhday4_duis9w','2025-01-01',0),
        (141,36,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877956/trasuatruyenthongdacbiet1_ltzbdu.jpg',N'trasuatruyenthongdacbiet1_ltzbdu','2025-01-01',1),
        (142,36,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877955/trasuatruyenthongdacbiet2_ckyeys.jpg',N'trasuatruyenthongdacbiet2_ckyeys','2025-01-01',0),
        (143,36,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877956/trasuatruyenthongdacbiet3_oudysf.png',N'trasuatruyenthongdacbiet3_oudysf','2025-01-01',0),
        (144,36,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877957/trasuatruyenthongdacbiet4_spwcd9.jpg',N'trasuatruyenthongdacbiet4_spwcd9','2025-01-01',0),

        (145,37,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877878/matchalatte1_tcllnd.jpg',N'matchalatte1_tcllnd','2025-01-01',1),
        (146,37,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877879/matchalatte2_qlskxc.jpg',N'matchalatte2_qlskxc','2025-01-01',0),
        (147,37,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877879/matchalatte3_avuolk.jpg',N'matchalatte3_avuolk','2025-01-01',0),
        (148,37,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877880/matchalatte4_mvtqfh.jpg',N'matchalatte4_mvtqfh','2025-01-01',0),

        (149,38,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877871/chocolatelatte1_xg8av1.webp',N'chocolatelatte1_xg8av1','2025-01-01',1),
        (150,38,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877872/chocolatelatte2_bbrxol.jpg',N'chocolatelatte2_bbrxol','2025-01-01',0),
        (151,38,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877872/chocolatelatte3_lrighe.jpg',N'chocolatelatte3_lrighe','2025-01-01',0),
        (152,38,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877872/chocolatelatte4_elw2yu.jpg',N'chocolatelatte4_elw2yu','2025-01-01',0),

        (153,39,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877875/matchadaxay1_vqwxct.jpg',N'matchadaxay1_vqwxct','2025-01-01',1),
        (154,39,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877876/matchadaxay2_wq9cgj.jpg',N'matchadaxay2_wq9cgj','2025-01-01',0),
        (155,39,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877877/matchadaxay3_fe5yxq.webp',N'matchadaxay3_fe5yxq','2025-01-01',0),
        (156,39,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877877/matchadaxay4_zmuxyj.jpg',N'matchadaxay4_zmuxyj','2025-01-01',0),
        
        (157,40,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784896192/coldbrewcam1_k7eucz.webp',N'coldbrewcam1_k7eucz','2025-01-01',1),
        (158,40,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784896193/coldbrewcam2_uq7fwb.jpg',N'coldbrewcam2_uq7fwb','2025-01-01',0),
        (159,40,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784896193/coldbrewcam3_lfht6x.jpg',N'coldbrewcam3_lfht6x','2025-01-01',0),
        (160,40,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784896192/coldbrewcam4_fazzqo.jpg',N'coldbrewcam4_fazzqo','2025-01-01',0),

        (161,41,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877852/mocha1_koeukd.jpg',N'mocha1_koeukd','2025-01-01',1),
        (162,41,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877852/mocha2_xcmnry.jpg',N'mocha2_xcmnry','2025-01-01',0),
        (163,41,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877853/mocha3_l0pdhz.webp',N'mocha3_l0pdhz','2025-01-01',0),
        (164,41,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877852/mocha4_w9pmjh.jpg',N'mocha4_w9pmjh','2025-01-01',0),

        (165,42,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877842/carameomacchiato1_laop8z.jpg',N'carameomacchiato1_laop8z','2025-01-01',1),
        (166,42,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877842/carameomacchiato2_bu9tzb.jpg',N'carameomacchiato2_bu9tzb','2025-01-01',0),
        (167,42,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877842/carameomacchiato3_kwtx6v.webp',N'carameomacchiato3_kwtx6v','2025-01-01',0),
        (168,42,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877843/carameomacchiato4_lj6skt.jpg',N'carameomacchiato4_lj6skt','2025-01-01',0),

        (169,43,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877830/caphedua1_ud21lr.jpg',N'caphedua1_ud21lr','2025-01-01',1),
        (170,43,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877830/caphedua2_rgximd.jpg',N'caphedua2_rgximd','2025-01-01',0),
        (171,43,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877830/caphedua3_zzrr6w.jpg',N'caphedua3_zzrr6w','2025-01-01',0),
        (172,43,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877831/caphedua4_uwkiqr.webp',N'caphedua4_uwkiqr','2025-01-01',0),

        (173,44,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877907/trachanhmatong1_elnqi2.jpg',N'trachanhmatong1_elnqi2','2025-01-01',1),
        (174,44,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877906/trachanhmatong2_xh8ed0.jpg',N'trachanhmatong2_xh8ed0','2025-01-01',0),
        (175,44,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877910/trachanhmatong3_gcoge1.jpg',N'trachanhmatong3_gcoge1','2025-01-01',0),
        (176,44,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877908/trachanhmatong4_vt550r.jpg',N'trachanhmatong4_vt550r','2025-01-01',0),

        (177,45,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877919/traxoai1_zyykyf.jpg',N'traxoai1_zyykyf','2025-01-01',1),
        (178,45,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877920/traxoai2_da6ejf.jpg',N'traxoai2_da6ejf','2025-01-01',0),
        (179,45,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877921/traxoai3_ubccno.jpg',N'traxoai3_ubccno','2025-01-01',0),
        (180,45,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877921/traxoai4_efoeul.jpg',N'traxoai4_efoeul','2025-01-01',0),
        (181,46,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877934/trasuadau1_hrdvpw.jpg',N'trasuadau1_hrdvpw','2025-01-01',1),
        (182,46,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877935/trasuadau2_nyxwe7.jpg',N'trasuadau2_nyxwe7','2025-01-01',0),
        (183,46,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877937/trasuadau3_ldctra.jpg',N'trasuadau3_ldctra','2025-01-01',0),
        (184,46,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877938/trasuadau4_lmlrsy.jpg',N'trasuadau4_lmlrsy','2025-01-01',0),

        (185,47,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877914/traolongvai1_rmhf4h.jpg',N'traolongvai1_rmhf4h','2025-01-01',1),
        (186,47,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877915/traolongvai2_hyebia.jpg',N'traolongvai2_hyebia','2025-01-01',0),
        (187,47,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877915/traolongvai3_p11jhb.jpg',N'traolongvai3_p11jhb','2025-01-01',0),
        (188,47,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877916/traolongvai4_teiqt3.jpg',N'traolongvai4_teiqt3','2025-01-01',0),

        (189,48,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877880/matchasuayenmach1_otv46d.jpg',N'matchasuayenmach1_otv46d','2025-01-01',1),
        (190,48,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877881/matchasuayenmach2_kcwkso.jpg',N'matchasuayenmach2_kcwkso','2025-01-01',0),
        (191,48,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877882/matchasuayenmach3_u6ktxq.jpg',N'matchasuayenmach3_u6ktxq','2025-01-01',0),
        (192,48,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877882/matchasuayenmach4_re3nvc.jpg',N'matchasuayenmach4_re3nvc','2025-01-01',0),

        (193,49,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877861/chocolatedua1_uficrw.jpg',N'chocolatedua1_uficrw','2025-01-01',1),
        (194,49,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877862/chocolatedua2_fk6nt1.jpg',N'chocolatedua2_fk6nt1','2025-01-01',0),
        (195,49,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877862/chocolatedua3_cqy0un.jpg',N'chocolatedua3_cqy0un','2025-01-01',0),
        (196,49,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877863/chocolatedua4_piotka.jpg',N'chocolatedua4_piotka','2025-01-01',0),

        (197,50,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877864/suachuachanhday1_fxhgml.jpg',N'suachuachanhday1_fxhgml','2025-01-01',1),
        (198,50,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877864/suachuachanhday2_d72dyx.jpg',N'suachuachanhday2_d72dyx','2025-01-01',0),
        (199,50,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877865/suachuachanhday3_hjcqf3.jpg',N'suachuachanhday3_hjcqf3','2025-01-01',0),
        (200,50,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877865/suachuachanhday4_w6fms2.jpg',N'suachuachanhday4_w6fms2','2025-01-01',0),
        (201,51,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877833/caphekemcheese1_rxuamk.webp',N'caphekemcheese1_rxuamk','2025-01-01',1),
        (202,51,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877833/caphekemcheese2_j4ovtf.jpg',N'caphekemcheese2_j4ovtf','2025-01-01',0),
        (203,51,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877834/caphekemcheese3_vqejeb.jpg',N'caphekemcheese3_vqejeb','2025-01-01',0),
        (204,51,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877834/caphekemcheese4_bafcy5.jpg',N'caphekemcheese4_bafcy5','2025-01-01',0),

        (205,52,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877843/coldbrewmatongchanhvang1_qjcjid.jpg',N'coldbrewmatongchanhvang1_qjcjid','2025-01-01',1),
        (206,52,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877844/coldbrewmatongchanhvang2_mshbeu.webp',N'coldbrewmatongchanhvang2_mshbeu','2025-01-01',0),
        (207,52,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877844/coldbrewmatongchanhvang3_fd7bbz.jpg',N'coldbrewmatongchanhvang3_fd7bbz','2025-01-01',0),
        (208,52,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877844/coldbrewmatongchanhvang4_acb5j5.jpg',N'coldbrewmatongchanhvang4_acb5j5','2025-01-01',0),

        (209,53,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877840/caphesuatranchauden1_cdp5ah.jpg',N'caphesuatranchauden1_cdp5ah','2025-01-01',1),
        (210,53,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877841/caphesuatranchauden2_iyp8sq.jpg',N'caphesuatranchauden2_iyp8sq','2025-01-01',0),
        (211,53,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877841/caphesuatranchauden3_bqe9rb.png',N'caphesuatranchauden3_bqe9rb','2025-01-01',0),
        (212,53,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877842/caphesuatranchauden4_owehqy.jpg',N'caphesuatranchauden4_owehqy','2025-01-01',0),

        (213,54,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877854/espressomatongyenmanh1_chylji.png',N'espressomatongyenmanh1_chylji','2025-01-01',1),
        (214,54,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877849/espressomatongyenmanh2_rcvqeb.webp',N'espressomatongyenmanh2_rcvqeb','2025-01-01',0),
        (215,54,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877849/espressomatongyenmanh3_xiudq9.jpg',N'espressomatongyenmanh3_xiudq9','2025-01-01',0),
        (216,54,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877850/espressomatongyenmanh4_xa8w5k.jpg',N'espressomatongyenmanh4_xa8w5k','2025-01-01',0),

        (217,55,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877836/caphesuabanhflan1_q40pda.jpg',N'caphesuabanhflan1_q40pda','2025-01-01',1),
        (218,55,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877836/caphesuabanhflan2_cwnugu.jpg',N'caphesuabanhflan2_cwnugu','2025-01-01',0),
        (219,55,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877837/caphesuabanhflan3_d788gt.jpg',N'caphesuabanhflan3_d788gt','2025-01-01',0),
        (220,55,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877837/caphesuabanhflan4_hn7cgw.jpg',N'caphesuabanhflan4_hn7cgw','2025-01-01',0),

        (221,56,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877845/coldbrewvainhadam1_mbvf3d.jpg',N'coldbrewvainhadam1_mbvf3d','2025-01-01',1),
        (222,56,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877845/coldbrewvainhadam2_z3vt5x.webp',N'coldbrewvainhadam2_z3vt5x','2025-01-01',0),
        (223,56,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877845/coldbrewvainhadam3_otljog.jpg',N'coldbrewvainhadam3_otljog','2025-01-01',0),
        (224,56,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877846/coldbrewvainhadam4_r6oqzf.jpg',N'coldbrewvainhadam4_r6oqzf','2025-01-01',0),

        (225,57,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877848/espressoduakemmuoi1_mfvbja.webp',N'espressoduakemmuoi1_mfvbja','2025-01-01',1),
        (226,57,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877848/espressoduakemmuoi2_xf55am.jpg',N'espressoduakemmuoi2_xf55am','2025-01-01',0),
        (227,57,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877848/espressoduakemmuoi3_fznq16.jpg',N'espressoduakemmuoi3_fznq16','2025-01-01',0),
        (228,57,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877849/espressoduakemmuoi4_iwicre.jpg',N'espressoduakemmuoi4_iwicre','2025-01-01',0),

        (229,58,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877830/capheduongdenthachdua1_nv1tfa.jpg',N'capheduongdenthachdua1_nv1tfa','2025-01-01',1),
        (230,58,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877831/capheduongdenthachdua2_u7jmqo.jpg',N'capheduongdenthachdua2_u7jmqo','2025-01-01',0),
        (231,58,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877832/capheduongdenthachdua3_ubmkyc.jpg',N'capheduongdenthachdua3_ubmkyc','2025-01-01',0),
        (232,58,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877833/capheduongdenthachdua4_jdgwst.jpg',N'capheduongdenthachdua4_jdgwst','2025-01-01',0),

        (233,59,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877838/caphesuakhucbach1_debtec.png',N'caphesuakhucbach1_debtec','2025-01-01',1),
        (234,59,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877839/caphesuakhucbach2_jiuzo1.jpg',N'caphesuakhucbach2_jiuzo1','2025-01-01',0),
        (235,59,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877839/caphesuakhucbach3_p35m86.jpg',N'caphesuakhucbach3_p35m86','2025-01-01',0),
        (236,59,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877840/caphesuakhucbach4_dnkn45.jpg',N'caphesuakhucbach4_dnkn45','2025-01-01',0),

        (237,60,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877847/coldbrewxoaichanhday1_mipj6f.jpg',N'coldbrewxoaichanhday1_mipj6f','2025-01-01',1),
        (238,60,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877846/coldbrewxoaichanhday2_nwokpc.jpg',N'coldbrewxoaichanhday2_nwokpc','2025-01-01',0),
        (239,60,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877847/coldbrewxoaichanhday3_rqykgd.jpg',N'coldbrewxoaichanhday3_rqykgd','2025-01-01',0),
        (240,60,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877847/coldbrewxoaichanhday4_uyv7xp.jpg',N'coldbrewxoaichanhday4_uyv7xp','2025-01-01',0),

        (241,61,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877891/olongdaonhadam1_diawpz.jpg',N'olongdaonhadam1_diawpz','2025-01-01',1),
        (242,61,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877892/olongdaonhadam2_nkptbr.jpg',N'olongdaonhadam2_nkptbr','2025-01-01',0),
        (243,61,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877893/olongdaonhadam3_cubnhq.jpg',N'olongdaonhadam3_cubnhq','2025-01-01',0),
        (244,61,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877893/olongdaonhadam4_wigsya.jpg',N'olongdaonhadam4_wigsya','2025-01-01',0),

        (245,62,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877927/hongtravaihatchia1_acn0oz.jpg',N'hongtravaihatchia1_acn0oz','2025-01-01',1),
        (246,62,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877890/hongtravaihatchia2_aiuirm.jpg',N'hongtravaihatchia2_aiuirm','2025-01-01',0),
        (247,62,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877890/hongtravaihatchia3_qgf30h.jpg',N'hongtravaihatchia3_qgf30h','2025-01-01',0),
        (248,62,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877891/hongtravaihatchia4_gip86h.jpg',N'hongtravaihatchia4_gip86h','2025-01-01',0),

        (249,63,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877897/olongxoaithachdua1_mzrtqn.jpg',N'olongxoaithachdua1_mzrtqn','2025-01-01',1),
        (250,63,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877897/olongxoaithachdua2_ncrxxr.jpg',N'olongxoaithachdua2_ncrxxr','2025-01-01',0),
        (251,63,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877899/olongxoaithachdua3_ey4ehp.jpg',N'olongxoaithachdua3_ey4ehp','2025-01-01',0),
        (252,63,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877898/olongxoaithachdua4_sarvzp.jpg',N'olongxoaithachdua4_sarvzp','2025-01-01',0),

        (253,64,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877924/hongtracamnhadam1_uelwrt.jpg',N'hongtracamnhadam1_uelwrt','2025-01-01',1),
        (254,64,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877925/hongtracamnhadam2_b4wbjz.jpg',N'hongtracamnhadam2_b4wbjz','2025-01-01',0),
        (255,64,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877926/hongtracamnhadam3_kjy1jy.jpg',N'hongtracamnhadam3_kjy1jy','2025-01-01',0),
        (256,64,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877926/hongtracamnhadam4_hqrzbq.jpg',N'hongtracamnhadam4_hqrzbq','2025-01-01',0),

        (257,65,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877904/trachanhdayhatchia1_fjoon4.jpg',N'trachanhdayhatchia1_fjoon4','2025-01-01',1),
        (258,65,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877905/trachanhdayhatchia2_ovgc0n.jpg',N'trachanhdayhatchia2_ovgc0n','2025-01-01',0),
        (259,65,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877906/trachanhdayhatchia3_hxhhcy.jpg',N'trachanhdayhatchia3_hxhhcy','2025-01-01',0),
        (260,65,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877907/trachanhdayhatchia4_otflxs.webp',N'trachanhdayhatchia4_otflxs','2025-01-01',0),

        (261,66,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784900158/olongdauthachdua1_dxycpz.jpg',N'olongdauthachdua1_dxycpz','2025-01-01',1),
        (262,66,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784900158/olongdauthachdua2_r4ovat.jpg',N'olongdauthachdua2_r4ovat','2025-01-01',0),
        (263,66,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784900158/olongdauthachdua3_unepoz.jpg',N'olongdauthachdua3_unepoz','2025-01-01',0),
        (264,66,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784900163/olongdauthachdua4_mzplvu.jpg',N'olongdauthachdua4_mzplvu','2025-01-01',0),

        (265,67,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877909/tradaokhucbach1_b7yutr.png',N'tradaokhucbach1_b7yutr','2025-01-01',1),
        (266,67,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877909/tradaokhucbach2_waosnv.png',N'tradaokhucbach2_waosnv','2025-01-01',0),
        (267,67,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877912/tradaokhucbach3_hom7sm.png',N'tradaokhucbach3_hom7sm','2025-01-01',0),
        (268,67,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877911/tradaokhucbach4_q3rx7b.png',N'tradaokhucbach4_q3rx7b','2025-01-01',0),

        (269,68,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877917/travainhadam1_pl70nz.jpg',N'travainhadam1_pl70nz','2025-01-01',1),
        (270,68,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877918/travainhadam2_u3zdji.jpg',N'travainhadam2_u3zdji','2025-01-01',0),
        (271,68,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877918/travainhadam3_inlzsi.jpg',N'travainhadam3_inlzsi','2025-01-01',0),
        (272,68,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877919/travainhadam4_emqs1u.jpg',N'travainhadam4_emqs1u','2025-01-01',0),

        (273,69,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877922/traxoaihatchia1_iilb8b.jpg',N'traxoaihatchia1_iilb8b','2025-01-01',1),
        (274,69,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877923/traxoaihatchia2_gkewyg.jpg',N'traxoaihatchia2_gkewyg','2025-01-01',0),
        (275,69,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877923/traxoaihatchia3_azcoyd.jpg',N'traxoaihatchia3_azcoyd','2025-01-01',0),
        (276,69,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877924/traxoaihatchia4_ufpgki.jpg',N'traxoaihatchia4_ufpgki','2025-01-01',0),

        (277,70,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877899/tracamchanhday1_vht9qm.jpg',N'tracamchanhday1_vht9qm','2025-01-01',1),
        (278,70,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877900/tracamchanhday2_zmrxll.jpg',N'tracamchanhday2_zmrxll','2025-01-01',0),
        (279,70,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877900/tracamchanhday3_i3ycx1.jpg',N'tracamchanhday3_i3ycx1','2025-01-01',0),
        (280,70,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877901/tracamchanhday4_oxqdps.jpg',N'tracamchanhday4_oxqdps','2025-01-01',0),

        (281,71,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877952/trasuatranchauduongden1_eoo69p.jpg',N'trasuatranchauduongden1_eoo69p','2025-01-01',1),
        (282,71,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877953/trasuatranchauduongden2_a8jhd0.jpg',N'trasuatranchauduongden2_a8jhd0','2025-01-01',0),
        (283,71,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877953/trasuatranchauduongden3_umwdwe.jpg',N'trasuatranchauduongden3_umwdwe','2025-01-01',0),
        (284,71,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877954/trasuatranchauduongden4_nrraud.jpg',N'trasuatranchauduongden4_nrraud','2025-01-01',0),

        (285,72,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877938/trasuabanhflan1_f6hnzj.jpg',N'trasuabanhflan1_f6hnzj','2025-01-01',1),
        (286,72,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877934/trasuabanhflan2_zebn15.jpg',N'trasuabanhflan2_zebn15','2025-01-01',0),
        (287,72,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877933/trasuabanhflan3_pv8uhc.jpg',N'trasuabanhflan3_pv8uhc','2025-01-01',0),
        (288,72,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784899745/trasuabanhflan4_y4uhzr.jpg',N'trasuabanhflan4_y4uhzr','2025-01-01',0),

        (289,73,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877942/trasuakhucbach1_rd1ses.jpg',N'trasuakhucbach1_rd1ses','2025-01-01',1),
        (290,73,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877944/trasuakhucbach2_dyzs8i.jpg',N'trasuakhucbach2_dyzs8i','2025-01-01',0),
        (291,73,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877943/trasuakhucbach3_wk62l6.jpg',N'trasuakhucbach3_wk62l6','2025-01-01',0),
        (292,73,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877944/trasuakhucbach4_eo3sxq.jpg',N'trasuakhucbach4_eo3sxq','2025-01-01',0),

        (293,74,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877945/trasuanhadam1_srkrfw.jpg',N'trasuanhadam1_srkrfw','2025-01-01',1),
        (294,74,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877946/trasuanhadam2_n4qm37.jpg',N'trasuanhadam2_n4qm37','2025-01-01',0),
        (295,74,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877947/trasuanhadam3_pdnlb3.jpg',N'trasuanhadam3_pdnlb3','2025-01-01',0),
        (296,74,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877948/trasuanhadam4_yf0k0i.jpg',N'trasuanhadam4_yf0k0i','2025-01-01',0),

        (297,75,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877948/trasuathachdua1_z490br.jpg',N'trasuathachdua1_z490br','2025-01-01',1),
        (298,75,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877949/trasuathachdua2_awlkyt.jpg',N'trasuathachdua2_awlkyt','2025-01-01',0),
        (299,75,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877950/trasuathachdua3_y7u8gj.jpg',N'trasuathachdua3_y7u8gj','2025-01-01',0),
        (300,75,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877951/trasuathachdua4_cfehqv.jpg',N'trasuathachdua4_cfehqv','2025-01-01',0),

        (301,76,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877939/trasuakemcheese1_k0hn62.jpg',N'trasuakemcheese1_k0hn62','2025-01-01',1),
        (302,76,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877940/trasuakemcheese2_xlrv8b.jpg',N'trasuakemcheese2_xlrv8b','2025-01-01',0),
        (303,76,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877941/trasuakemcheese3_bd2wcb.jpg',N'trasuakemcheese3_bd2wcb','2025-01-01',0),
        (304,76,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877942/trasuakemcheese4_umrxes.jpg',N'trasuakemcheese4_umrxes','2025-01-01',0),

        (305,77,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877873/matchadaukemcheese1_ucdj2z.jpg',N'matchadaukemcheese1_ucdj2z','2025-01-01',1),
        (306,77,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877874/matchadaukemcheese2_zt0avz.jpg',N'matchadaukemcheese2_zt0avz','2025-01-01',0),
        (307,77,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877874/matchadaukemcheese3_rsbqak.jpg',N'matchadaukemcheese3_rsbqak','2025-01-01',0),
        (308,77,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877875/matchadaukemcheese4_zeunpz.jpg',N'matchadaukemcheese4_zeunpz','2025-01-01',0),

        (309,78,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877884/matchaxoaithachdua1_f5u70u.jpg',N'matchaxoaithachdua1_f5u70u','2025-01-01',1),
        (310,78,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877883/matchaxoaithachdua2_vdyhyg.jpg',N'matchaxoaithachdua2_vdyhyg','2025-01-01',0),
        (311,78,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877883/matchaxoaithachdua3_vncenx.jpg',N'matchaxoaithachdua3_vncenx','2025-01-01',0),
        (312,78,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877884/matchaxoaithachdua4_toxufc.jpg',N'matchaxoaithachdua4_toxufc','2025-01-01',0),

        (313,79,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877861/chocolatecaramelkemmuoi1_oz3wmt.jpg',N'chocolatecaramelkemmuoi1_oz3wmt','2025-01-01',1),
        (314,79,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877861/chocolatecaramelkemmuoi2_nuklu1.jpg',N'chocolatecaramelkemmuoi2_nuklu1','2025-01-01',0),
        (315,79,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877860/chocolatecaramelkemmuo3_cs8o7i.jpg',N'chocolatecaramelkemmuo3_cs8o7i','2025-01-01',0),
        (316,79,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877861/chocolatecaramelkemmuoi4_cdfqif.jpg',N'chocolatecaramelkemmuoi4_cdfqif','2025-01-01',0),

        (317,80,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877866/suachuaxoainhadam1_bvksik.jpg',N'suachuaxoainhadam1_bvksik','2025-01-01',1),
        (318,80,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877866/suachuaxoainhadam2_vlywtc.jpg',N'suachuaxoainhadam2_vlywtc','2025-01-01',0),
        (319,80,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877866/suachuaxoainhadam3_xzdjyi.jpg',N'suachuaxoainhadam3_xzdjyi','2025-01-01',0),
        (320,80,N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877867/suachuaxoainhadam4_fw8vyo.jpg',N'suachuaxoainhadam4_fw8vyo','2025-01-01',0);
    
    IF EXISTS
    (
        SELECT 1
        FROM @DrinkImageSeed x
        JOIN dbo.DrinkImages i ON i.DrinkImageId = x.DrinkImageId
        WHERE i.DrinkId <> x.DrinkId
           OR i.ImageUrl <> x.ImageUrl
           OR i.PublicId <> x.PublicId
           OR i.CreatedAt <> x.CreatedAt
           OR i.IsDefault <> x.IsDefault
    )
        THROW 52012, N'DrinkImages có ID xung đột với Part1.', 1;

    SET IDENTITY_INSERT dbo.DrinkImages ON;

    INSERT dbo.DrinkImages(DrinkImageId, DrinkId, ImageUrl, PublicId, CreatedAt, IsDefault)
    SELECT x.DrinkImageId, x.DrinkId, x.ImageUrl, x.PublicId, x.CreatedAt, x.IsDefault
    FROM @DrinkImageSeed x
    WHERE NOT EXISTS (SELECT 1 FROM dbo.DrinkImages i WHERE i.DrinkImageId = x.DrinkImageId);

    SET IDENTITY_INSERT dbo.DrinkImages OFF;

    /* ============================================================
       04. DRINK SIZES
       - 54 unchanged Part1 rows.
       - Duplicate Store1 drinks reuse Part1 M/L rows and Part1 prices.
       - 18 rows for nine retained Store1 drinks.
       - 22 rows for eleven extension drinks.
       ============================================================ */

    DECLARE @DrinkSizeSeed TABLE
    (
        DrinkId int NOT NULL,
        SizeId int NOT NULL,
        Price decimal(18,2) NOT NULL,
        Active bit NOT NULL,
        PRIMARY KEY (DrinkId, SizeId)
    );

    INSERT @DrinkSizeSeed(DrinkId, SizeId, Price, Active)
    VALUES
        (7,1,28000,1),(7,2,33000,1),(7,3,38000,1),
        (8,1,35000,1),(8,2,40000,1),(8,3,45000,1),
        (9,1,35000,1),(9,2,40000,1),(9,3,45000,1),
        (10,1,32000,1),(10,2,37000,1),(10,3,42000,1),
        (11,1,38000,1),(11,2,43000,1),(11,3,48000,1),
        (12,1,32000,1),(12,2,37000,1),(12,3,42000,1),
        (13,1,32000,1),(13,2,37000,1),(13,3,42000,1),
        (14,1,33000,1),(14,2,38000,1),(14,3,43000,1),
        (15,1,34000,1),(15,2,39000,1),(15,3,44000,1),
        (16,5,15000,1),(17,5,15000,1),(18,5,15000,1),(19,5,15000,1),(20,5,12000,1),
        (21,1,35000,1),(21,2,40000,1),(21,3,45000,1),
        (22,1,32000,1),(22,2,37000,1),(22,3,42000,1),
        (23,1,25000,1),(23,2,30000,1),(23,3,35000,1),
        (24,1,32000,1),(24,2,37000,1),(24,3,42000,1),
        (25,1,22000,1),(26,1,28000,1),
        (27,1,30000,1),(27,2,35000,1),
        (28,1,32000,1),(28,2,37000,1),
        (29,1,32000,1),(29,2,37000,1),
        (30,1,32000,1),(30,2,37000,1),
        (31,2,25000,1),(31,3,30000,1),
        (32,2,30000,1),(32,3,35000,1),
        (33,2,38000,1),(33,3,43000,1),
        (34,2,42000,1),(34,3,48000,1),
        (35,2,37000,1),(35,3,43000,1),
        (36,2,35000,1),(36,3,41000,1),
        (37,2,45000,1),(37,3,52000,1),
        (38,2,42000,1),(38,3,49000,1),
        (39,2,52000,1),(39,3,59000,1),
        (40,2,45000,1),(40,3,52000,1),
        (41,2,48000,1),(41,3,55000,1),
        (42,2,50000,1),(42,3,58000,1),
        (43,2,42000,1),(43,3,49000,1),
        (44,2,35000,1),(44,3,41000,1),
        (45,2,39000,1),(45,3,45000,1),
        (46,2,39000,1),(46,3,45000,1),
        (47,2,41000,1),(47,3,47000,1),
        (48,2,48000,1),(48,3,55000,1),
        (49,2,47000,1),(49,3,54000,1),
        (50,2,42000,1),(50,3,48000,1),

        (51,2,49000,1),(51,3,55000,1), 
        (52,2,47000,1),(52,3,55000,1),
        (53,2,45000,1),(53,3,50000,1),
        (54,2,50000,1),(54,3,55000,1),
        (55,2,46000,1),(55,3,50000,1),
        (56,2,49000,1),(56,3,55000,1),
        (57,2,52000,1),(57,3,60000,1),
        (58,2,47000,1),(58,3,55000,1),
        (59,2,47000,1),(59,3,55000,1),
        (60,2,49000,1),(60,3,55000,1),

        (61,2,43000,1),(61,3,50000,1),
        (62,2,43000,1),(62,3,50000,1),
        (63,2,45000,1),(63,3,50000,1),
        (64,2,43000,1),(64,3,50000,1),
        (65,2,42000,1),(65,3,50000,1),
        (66,2,45000,1),(66,3,50000,1),
        (67,2,45000,1),(67,3,50000,1),
        (68,2,43000,1),(68,3,50000,1),
        (69,2,43000,1),(69,3,50000,1),
        (70,2,44000,1),(70,3,50000,1),

        (71,2,45000,1),(71,3,50000,1),
        (72,2,45000,1),(72,3,50000,1),
        (73,2,45000,1),(73,3,50000,1),
        (74,2,43000,1),(74,3,50000,1),
        (75,2,43000,1),(75,3,50000,1),
        (76,2,46000,1),(76,3,50000,1),
        (77,2,52000,1),(77,3,55000,1),
        (78,2,52000,1),(78,3,55000,1),
        (79,2,50000,1),(79,3,55000,1),
        (80,2,45000,1),(80,3,40000,1);

    IF EXISTS
    (
        SELECT 1
        FROM @DrinkSizeSeed x
        JOIN dbo.DrinkSizes ds ON ds.DrinkId = x.DrinkId AND ds.SizeId = x.SizeId
        WHERE ds.Price <> x.Price OR ds.Active <> x.Active
    )
        THROW 52013, N'DrinkSizes có quan hệ Drink/Size trùng nhưng khác giá hoặc trạng thái.', 1;

    INSERT dbo.DrinkSizes(DrinkId, SizeId, Price, Active, UpdatedAtUtc)
    SELECT x.DrinkId, x.SizeId, x.Price, x.Active, CAST('2026-01-01T00:00:00' AS datetime2(7))
    FROM @DrinkSizeSeed x
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.DrinkSizes ds
        WHERE ds.DrinkId = x.DrinkId AND ds.SizeId = x.SizeId
    );

    /* Batch-level invariants before commit. */
    IF (SELECT COUNT(*) FROM dbo.DrinkCategories WHERE CategoryId BETWEEN 1 AND 13) <> 13
        THROW 52020, N'Số DrinkCategories canonical sau Batch 01 phải bằng 13.', 1;

    IF (SELECT COUNT(*) FROM dbo.Drinks WHERE DrinkId BETWEEN 1 AND 80) <> 80
        THROW 52021, N'Số Drinks canonical sau Batch 01 phải bằng 80.', 1;

    IF (SELECT COUNT(*) FROM dbo.DrinkImages WHERE DrinkImageId BETWEEN 1 AND 320) <> 320
        THROW 52022, N'Số DrinkImages nền + Part1 sau Batch 01 phải bằng 320.', 1;

    IF EXISTS
    (
        SELECT DrinkCode FROM dbo.Drinks GROUP BY DrinkCode HAVING COUNT(*) > 1
    ) OR EXISTS
    (
        SELECT Name FROM dbo.Drinks GROUP BY Name HAVING COUNT(*) > 1
    )
        THROW 52023, N'Phát hiện duplicate DrinkCode hoặc Drink.Name.', 1;

    IF EXISTS
    (
        SELECT DrinkId, SizeId FROM dbo.DrinkSizes
        GROUP BY DrinkId, SizeId HAVING COUNT(*) > 1
    )
        THROW 52024, N'Phát hiện duplicate DrinkSizes theo DrinkId/SizeId.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM @DrinkSizeSeed x
        LEFT JOIN dbo.Drinks d ON d.DrinkId = x.DrinkId
        LEFT JOIN dbo.Sizes s ON s.SizeId = x.SizeId
        WHERE d.DrinkId IS NULL OR s.SizeId IS NULL OR x.Price <= 0
    )
        THROW 52025, N'DrinkSizes có FK không hợp lệ hoặc giá không dương.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    BEGIN TRY
        SET IDENTITY_INSERT dbo.DrinkCategories OFF;
    END TRY
    BEGIN CATCH
    END CATCH;

    BEGIN TRY
        SET IDENTITY_INSERT dbo.Drinks OFF;
    END TRY
    BEGIN CATCH
    END CATCH;

    BEGIN TRY
        SET IDENTITY_INSERT dbo.DrinkImages OFF;
    END TRY
    BEGIN CATCH
    END CATCH;

    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/* ============================================================
   BATCH 01 READ-ONLY VERIFICATION
   ============================================================ */

SELECT N'DrinkCategories' AS Entity,
       COUNT(*) AS TotalRows,
       MIN(CategoryId) AS MinId,
       MAX(CategoryId) AS MaxId,
       SUM(CASE WHEN CategoryId BETWEEN 4 AND 8 THEN 1 ELSE 0 END) AS Part1Rows,
       SUM(CASE WHEN CategoryId BETWEEN 9 AND 13 THEN 1 ELSE 0 END) AS Store1Rows
FROM dbo.DrinkCategories
UNION ALL
SELECT N'Drinks', COUNT(*), MIN(DrinkId), MAX(DrinkId),
       SUM(CASE WHEN DrinkId BETWEEN 7 AND 30 THEN 1 ELSE 0 END),
       SUM(CASE WHEN DrinkId BETWEEN 31 AND 39 THEN 1 ELSE 0 END)
FROM dbo.Drinks
UNION ALL
SELECT N'DrinkImages', COUNT(*), MIN(DrinkImageId), MAX(DrinkImageId),
       SUM(CASE WHEN DrinkImageId BETWEEN 25 AND 320 THEN 1 ELSE 0 END), 0
FROM dbo.DrinkImages
UNION ALL
SELECT N'DrinkSizes', COUNT(*), MIN(DrinkSizeId), MAX(DrinkSizeId),
       SUM(CASE WHEN DrinkId BETWEEN 7 AND 30 THEN 1 ELSE 0 END),
       SUM(CASE WHEN DrinkId BETWEEN 31 AND 39 THEN 1 ELSE 0 END)
FROM dbo.DrinkSizes;

SELECT N'DrinkCategories' AS [Table], N'TRATRAICAY' AS RetainedCode,
       N'DEMO_CAT_FRUIT_TEA' AS RemovedStore1Code,
       N'Giữ Part1 vì trùng tên và ý nghĩa nghiệp vụ.' AS Decision
UNION ALL
SELECT N'DrinkCategories', N'DAXAY', N'DEMO_CAT_FRAPPE', N'Giữ Part1 vì trùng tên và ý nghĩa nghiệp vụ.'
UNION ALL
SELECT N'Drinks', N'CF_BacXiu', N'DEMO_DRINK_BAC_XIU', N'Giữ Part1 DrinkId 7 và giá Part1.'
UNION ALL
SELECT N'Drinks', N'CF_Americano', N'DEMO_DRINK_AMERICANO', N'Giữ Part1 DrinkId 10 và giá Part1.'
UNION ALL
SELECT N'Drinks', N'TTC_CamSa', N'DEMO_DRINK_PEACH_ORANGE_TEA', N'Giữ Part1 DrinkId 21 và giá Part1.'
UNION ALL
SELECT N'Drinks', N'TTC_Vai', N'DEMO_DRINK_LYCHEE_TEA', N'Giữ Part1 DrinkId 22 và giá Part1.'
UNION ALL
SELECT N'Drinks', N'TS_OLong', N'DEMO_DRINK_OOLONG_MILK_TEA', N'Giữ Part1 DrinkId 14 và giá Part1.';

/* ============================================================
   BATCH 02/12
   Tables in this batch:
     1. Toppings
     2. DrinkToppings
     3. DrinkDefaultToppings
     4. StoreToppings

   Source and duplicate analysis:
     - EF HasData owns Topping IDs 1-6, DrinkTopping IDs 1-12,
       DrinkDefaultTopping IDs 1-6 and StoreTopping IDs 1-4.
     - Part1 does not specify identity values for these four tables.
       Deterministic IDs continue immediately after the EF ranges.
     - Store1 topping aliases:
         DEMO_TOP_BLACK_PEARL  -> TC_DEN
         DEMO_TOP_WHITE_PEARL  -> TC_TRANG
         DEMO_TOP_FLAN         -> BH_FLAN
         DEMO_TOP_TARO_JELLY   -> TH_KM
         DEMO_TOP_CHEESE_CREAM -> KEMCHEESE
       Only DEMO_TOP_ESPRESSO_SHOT remains a new Topping row.
     - This batch never updates or deletes an existing row. An exact row is
       skipped; a conflicting ID or business key aborts the transaction.
   ============================================================ */
GO

IF OBJECT_ID(N'dbo.Toppings', N'U') IS NULL
   OR OBJECT_ID(N'dbo.DrinkToppings', N'U') IS NULL
   OR OBJECT_ID(N'dbo.DrinkDefaultToppings', N'U') IS NULL
   OR OBJECT_ID(N'dbo.StoreToppings', N'U') IS NULL
    THROW 52100, N'Schema thiếu một trong các bảng của SeedAll Batch 02.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.Stores WHERE StoreId = 1 AND Active = 1)
   OR NOT EXISTS (SELECT 1 FROM dbo.Stores WHERE StoreId = 2 AND Active = 1)
   OR NOT EXISTS (SELECT 1 FROM dbo.Stores WHERE StoreId = 3 AND Active = 1)
    THROW 52101, N'Thiếu Store nền 1, 2 hoặc 3.', 1;

IF (SELECT COUNT(*) FROM dbo.Drinks WHERE DrinkId BETWEEN 1 AND 50) <> 50
    THROW 52102, N'Batch 01 chưa hoàn tất đủ DrinkId 1-50.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.Toppings WHERE ToppingId = 1 AND ToppingCode = N'TC_DEN' AND Name = N'Trân châu đen' AND Price = 5000 AND Active = 1)
   OR NOT EXISTS (SELECT 1 FROM dbo.Toppings WHERE ToppingId = 2 AND ToppingCode = N'TC_TRANG' AND Name = N'Trân châu trắng' AND Price = 5000 AND Active = 1)
   OR NOT EXISTS (SELECT 1 FROM dbo.Toppings WHERE ToppingId = 3 AND ToppingCode = N'PM_VIEN' AND Name = N'Phô mai viên' AND Price = 7000 AND Active = 1)
   OR NOT EXISTS (SELECT 1 FROM dbo.Toppings WHERE ToppingId = 4 AND ToppingCode = N'KB_CM' AND Name = N'Khúc bạch chân mèo' AND Price = 7000 AND Active = 1)
   OR NOT EXISTS (SELECT 1 FROM dbo.Toppings WHERE ToppingId = 5 AND ToppingCode = N'TH_KM' AND Name = N'Thạch khoai môn' AND Price = 6000 AND Active = 1)
   OR NOT EXISTS (SELECT 1 FROM dbo.Toppings WHERE ToppingId = 6 AND ToppingCode = N'BH_FLAN' AND Name = N'Bánh flan' AND Price = 6000 AND Active = 1)
    THROW 52103, N'Topping nền IDs 1-6 không đúng dữ liệu EF HasData.', 1;
GO

IF EXISTS
(
    SELECT 1
    FROM dbo.SystemSettings
    WHERE SettingKey = N'seedall_foundation_inventory_v1'
      AND SettingValue = N'completed'
)
BEGIN
    PRINT N'SeedAll Batch 02 skipped: foundation inventory v1 is already complete.';
    GOTO SeedAllBatch02Complete;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    /* ============================================================
       05. TOPPINGS

       Mapping:
         IDs  1-6  : EF HasData (verified above, not reinserted)
         IDs  7-12 : Part1, unchanged code/name/price/image/status
         ID   13   : retained Store1 espresso shot
         IDs 14-50 : meaningful extension catalog
       ============================================================ */

    DECLARE @ToppingSeed TABLE
    (
        ToppingId int NOT NULL PRIMARY KEY,
        ToppingCode nvarchar(50) NOT NULL UNIQUE,
        Name nvarchar(150) NOT NULL UNIQUE,
        Price decimal(18,2) NOT NULL,
        ImageUrl nvarchar(1000) NULL,
        ImagePublicId nvarchar(300) NULL,
        Active bit NOT NULL
    );

    INSERT @ToppingSeed
    (ToppingId, ToppingCode, Name, Price, ImageUrl, ImagePublicId, Active)
    VALUES
        (7,  N'KEMCHEESE',             N'Kem cheese',              8000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779804081/kemcheese_inzxak.jpg',    N'kemcheese_inzxak',    1),
        (8,  N'TH_Dao',                N'Thạch đào',               6000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779804077/thachdao_vcihwd.jpg',     N'thachdao_vcihwd',     1),
        (9,  N'NHADAM',                N'Nha đam',                 5000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779803870/nhadam_vaytet.jpg',       N'nhadam_vaytet',       1),
        (10, N'HATCHIA',               N'Hạt chia',                4000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779804081/hatchia_raldyn.jpg',      N'hatchia_raldyn',      1),
        (11, N'TH_Dua',                N'Thạch dừa',               5000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779804078/thachdua_lmh1ia.jpg',     N'thachdua_lmh1ia',     1),
        (12, N'PUDDINGTRUNG',          N'Pudding trứng',           7000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1779804048/puddingtrung_noep2j.jpg', N'puddingtrung_noep2j', 1),
        (13, N'DEMO_TOP_ESPRESSO_SHOT',N'Shot espresso',          10000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784901746/shotespressoextra4_nnd2bv.jpg', N'shotespressoextra4_nnd2bv', 1),
        (14, N'TC_HOANGKIM',           N'Trân châu hoàng kim',     7000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784879023/tranchauhoangkim1_hklfjq.jpg', N'tranchauhoangkim1_hklfjq', 1),
        (15, N'TC_DUONGDEN',           N'Trân châu đường đen',     8000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784879021/tranchauduongden2_adakme.jpg', N'tranchauduongden2_adakme', 1),
        (16, N'TC_MINI',               N'Trân châu mini',          6000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784879029/tranchaumini3_vivudb.jpg', N'tranchaumini3_vivudb', 1),
        (17, N'TC_KHOAIMON',           N'Trân châu khoai môn',     8000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784902088/tranchaukhoaimonextra1_duwdo9.webp', N'tranchaukhoaimonextra1_duwdo9', 1),
        (18, N'TH_CAFE',               N'Thạch cà phê',            6000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784879033/thachcaphe1_t874au.jpg', N'thachcaphe1_t874au', 1),
        (19, N'TH_MATCHA',             N'Thạch matcha',            7000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784879004/thachmatcha3_gqs8ll.jpg', N'thachmatcha3_gqs8ll', 1),
        (20, N'TH_VAI',                N'Thạch vải',               7000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784879011/thachvai1_rbkvmf.jpg', N'thachvai1_rbkvmf', 1),
        (21, N'TH_XOAI',               N'Thạch xoài',              7000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784879019/thachxoai4_narlbr.jpg', N'thachxoai4_narlbr', 1),
        (22, N'TH_DAU',                N'Thạch dâu',               7000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784878998/thachdau3_kfufgj.jpg', N'thachdau3_kfufgj', 1),
        (23, N'TH_CHANHDAY',           N'Thạch chanh dây',         7000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784878994/thachchanhday3_l7kmer.jpg', N'thachchanhday3_l7kmer', 1),
        (24, N'TH_MATONGCHANH',        N'Thạch mật ong chanh',     7000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784879008/thachmatongchanh3_aw29xm.jpg', N'thachmatongchanh3_aw29xm', 1),
        (25, N'TH_SUAYENMACH',         N'Thạch sữa yến mạch',      8000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784879009/thachsuayenmach1_p02z3f.jpg', N'thachsuayenmach1_p02z3f', 1),
        (26, N'TRAIDAO',               N'Đào miếng',               8000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784878949/daomieng3_qprspm.jpg', N'daomieng3_qprspm', 1),
        (27, N'TRAIVAI',               N'Vải ngâm',                8000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784878987/vaingam4_ixqqzi.jpg', N'vaingam4_ixqqzi', 1),
        (28, N'XOAI_HAT',              N'Xoài cắt hạt lựu',        8000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784878988/xoaicathatluu1_wqf8jl.jpg', N'xoaicathatluu1_wqf8jl', 1),
        (29, N'DAU_TUOI',              N'Dâu tươi',                9000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784878951/dautuoi1_m4gya4.jpg', N'dautuoi1_m4gya4', 1),
        (30, N'TEP_CAM',               N'Tép cam',                 7000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784878981/tepcam1_ehgvly.jpg', N'tepcam1_ehgvly', 1),
        (31, N'CHANHDAY_HAT',          N'Chanh dây hạt',           7000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784878945/chanhdayhat1_ix91w1.jpg', N'chanhdayhat1_ix91w1', 1),
        (32, N'PUDDING_VANILLA',       N'Pudding vanilla',         7000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784878978/puddingvanilla2_gse1mw.jpg', N'puddingvanilla2_gse1mw', 1),
        (33, N'PUDDING_SOCOLA',        N'Pudding chocolate',       8000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784878969/puddingchocolate3_h3wc4f.jpg', N'puddingchocolate3_h3wc4f', 1),
        (34, N'PUDDING_MATCHA',        N'Pudding matcha',          8000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784878975/puddingmatcha2_umsfyv.jpg', N'puddingmatcha2_umsfyv', 1),
        (35, N'PUDDING_KHOAIMON',      N'Pudding khoai môn',       8000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784878974/puddingkhoaimon4_zpwkim.jpg', N'puddingkhoaimon4_zpwkim', 1),
        (36, N'KEMMUOI',               N'Kem muối',                9000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784878960/kemmuoi4_c8fzgs.jpg', N'kemmuoi4_c8fzgs', 1),
        (37, N'KEMSUATUOI',            N'Kem sữa tươi',            9000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784878963/kemsuatuoi3_ih5lfp.jpg', N'kemsuatuoi3_ih5lfp', 1),
        (38, N'KEMDUA',                N'Kem dừa',                 9000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784878955/kemdua2_fg2zfd.webp', N'kemdua2_fg2zfd', 1),
        (39, N'KEMYENMACH',            N'Kem yến mạch',           10000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784878967/kemyenmach4_bplait.jpg', N'kemyenmach4_bplait', 1),
        (40, N'SOT_CARAMEL',           N'Sốt caramel',             6000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877973/sotcaramel1_q9bw0j.webp', N'sotcaramel1_q9bw0j', 1),
        (41, N'SOT_SOCOLA',            N'Sốt chocolate',           6000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877980/sotchocolate2_kjta7g.jpg', N'sotchocolate2_kjta7g', 1),
        (42, N'SOT_DAU',               N'Sốt dâu',                 6000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877983/sotdau2_gqhyle.jpg', N'sotdau2_gqhyle', 1),
        (43, N'SOT_XOAI',              N'Sốt xoài',                6000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784878018/sotxoai1_nekvmw.jpg', N'sotxoai1_nekvmw', 1),
        (44, N'SOT_MATONG',            N'Sốt mật ong',             5000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877990/sotmatong1_epyjsw.jpg', N'sotmatong1_epyjsw', 1),
        (45, N'SOT_DUONGDEN',          N'Sốt đường đen',           6000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877988/sotduongden3_ecdfyq.jpg', N'sotduongden3_ecdfyq', 1),
        (46, N'SHOT_MATCHA',           N'Shot matcha',             8000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784877975/shotmatchaextra3_wrzes3.jpg', N'shotmatchaextra3_wrzes3', 1),
        (47, N'SUA_YENMACH_THEM',      N'Sữa yến mạch thêm',       8000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784878134/suayenmachextra2_zajfg8.jpg', N'suayenmachextra2_zajfg8', 1),
        (48, N'COT_DUA_THEM',          N'Nước cốt dừa thêm',       8000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784878908/nuoccotduaextra3_aj4k3w.webp', N'nuoccotduaextra3_aj4k3w', 1),
        (49, N'SUA_CHUA_THEM',         N'Sữa chua thêm',           8000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784878093/suachuaextra1_r3olab.webp', N'suachuaextra1_r3olab', 1),
        (50, N'SYRUP_CARAMEL_THEM',    N'Syrup caramel thêm',      6000,  N'https://res.cloudinary.com/dzfizobk8/image/upload/v1784878152/syrulcaramel4_cdjs7i.jpg', N'syrulcaramel4_cdjs7i', 1);

    IF EXISTS
    (
        SELECT 1
        FROM @ToppingSeed x
        JOIN dbo.Toppings t
          ON t.ToppingId = x.ToppingId
          OR t.ToppingCode = x.ToppingCode
          OR t.Name = x.Name
        WHERE t.ToppingId <> x.ToppingId
           OR t.ToppingCode <> x.ToppingCode
           OR t.Name <> x.Name
           OR t.Price <> x.Price
           OR ISNULL(t.ImageUrl, N'') <> ISNULL(x.ImageUrl, N'')
           OR ISNULL(t.ImagePublicId, N'') <> ISNULL(x.ImagePublicId, N'')
           OR t.Active <> x.Active
    )
        THROW 52110, N'Toppings có ID, Code hoặc Name xung đột với SeedAll Batch 02.', 1;

    SET IDENTITY_INSERT dbo.Toppings ON;

    INSERT dbo.Toppings
    (ToppingId, ToppingCode, Name, Price, ImageUrl, ImagePublicId, Active)
    SELECT x.ToppingId, x.ToppingCode, x.Name, x.Price, x.ImageUrl, x.ImagePublicId, x.Active
    FROM @ToppingSeed x
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.Toppings t WHERE t.ToppingId = x.ToppingId
    );

    SET IDENTITY_INSERT dbo.Toppings OFF;

    /* ============================================================
       06. DRINK TOPPINGS

       IDs 13-37: Part1 relationships.
       IDs 38-54: retained Store1 relationships after aliases.
       IDs 55-95: extension drink compatibility relationships.
       ============================================================ */

    DECLARE @DrinkToppingSeed TABLE
    (
        DrinkToppingId int NOT NULL PRIMARY KEY,
        DrinkId int NOT NULL,
        ToppingId int NOT NULL,
        Active bit NOT NULL,
        UNIQUE (DrinkId, ToppingId)
    );

    INSERT @DrinkToppingSeed(DrinkToppingId, DrinkId, ToppingId, Active)
    VALUES
        (13,12,1,1),(14,12,2,1),(15,12,3,1),(16,12,7,1),
        (17,13,1,1),(18,13,2,1),(19,13,5,1),(20,13,6,1),
        (21,14,1,1),(22,14,2,1),(23,14,7,1),
        (24,15,1,1),(25,15,2,1),(26,15,3,1),(27,15,7,1),
        (28,21,8,1),(29,21,9,1),(30,21,10,1),
        (31,22,8,1),(32,22,9,1),
        (33,23,9,1),(34,23,10,1),
        (35,24,8,1),(36,24,9,1),(37,24,10,1),
        (38,36,1,1),(39,36,2,1),(40,36,6,1),(41,36,5,1),(42,36,7,1),
        (43,14,6,1),(44,14,5,1),
        (45,37,1,1),(46,37,6,1),(47,37,7,1),
        (48,38,1,1),(49,38,6,1),(50,38,7,1),
        (51,39,1,1),(52,39,7,1),
        (53,10,13,1),(54,34,13,1),
        (55,40,13,1),(56,40,30,1),(57,40,44,1),
        (58,41,13,1),(59,41,37,1),(60,41,41,1),
        (61,42,13,1),(62,42,37,1),(63,42,40,1),(64,42,50,1),
        (65,43,13,1),(66,43,38,1),(67,43,48,1),
        (68,44,10,1),(69,44,24,1),(70,44,44,1),
        (71,45,10,1),(72,45,21,1),(73,45,28,1),(74,45,43,1),
        (75,46,1,1),(76,46,7,1),(77,46,22,1),(78,46,29,1),(79,46,42,1),
        (80,47,2,1),(81,47,9,1),(82,47,20,1),(83,47,27,1),
        (84,48,19,1),(85,48,39,1),(86,48,46,1),(87,48,47,1),
        (88,49,11,1),(89,49,38,1),(90,49,41,1),(91,49,48,1),
        (92,50,10,1),(93,50,23,1),(94,50,31,1),(95,50,49,1),
        (96,51,7,1),
        (97,51,13,1),
        (98,52,10,1),
        (99,52,44,1),
        (100,53,1,1),
        (101,53,6,1),
        (102,54,39,1),
        (103,54,44,1),
        (104,55,6,1),
        (105,55,1,1),
        (106,56,9,1),
        (107,56,27,1),
        (108,57,36,1),
        (109,57,48,1),
        (110,58,45,1),
        (111,58,11,1),
        (112,59,4,1),
        (113,59,1,1),
        (114,60,43,1),
        (115,60,31,1),
        (116,61,9,1),
        (117,61,26,1),
        (118,62,10,1),
        (119,62,27,1),
        (120,63,11,1),
        (121,63,28,1),
        (122,64,9,1),
        (123,64,30,1),
        (124,65,10,1),
        (125,65,31,1),
        (126,66,11,1),
        (127,66,29,1),
        (128,67,4,1),
        (129,67,26,1),
        (130,68,9,1),
        (131,68,27,1),
        (132,69,10,1),
        (133,69,28,1),
        (134,70,30,1),
        (135,70,31,1),
        (136,71,15,1),
        (137,71,7,1),
        (138,72,6,1),
        (139,72,1,1),
        (140,73,4,1),
        (141,73,1,1),
        (142,74,9,1),
        (143,74,2,1),
        (144,75,11,1),
        (145,75,1,1),
        (146,76,7,1),
        (147,76,1,1),
        (148,77,7,1),
        (149,77,29,1),
        (150,78,11,1),
        (151,78,28,1),
        (152,79,36,1),
        (153,79,40,1),
        (154,80,9,1),
        (155,80,28,1);

    IF EXISTS
    (
        SELECT 1
        FROM @DrinkToppingSeed x
        JOIN dbo.DrinkToppings dt
          ON dt.DrinkToppingId = x.DrinkToppingId
          OR (dt.DrinkId = x.DrinkId AND dt.ToppingId = x.ToppingId)
        WHERE dt.DrinkToppingId <> x.DrinkToppingId
           OR dt.DrinkId <> x.DrinkId
           OR dt.ToppingId <> x.ToppingId
           OR dt.Active <> x.Active
    )
        THROW 52111, N'DrinkToppings có ID hoặc quan hệ Drink/Topping xung đột.', 1;

    SET IDENTITY_INSERT dbo.DrinkToppings ON;

    INSERT dbo.DrinkToppings(DrinkToppingId, DrinkId, ToppingId, Active)
    SELECT x.DrinkToppingId, x.DrinkId, x.ToppingId, x.Active
    FROM @DrinkToppingSeed x
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.DrinkToppings dt WHERE dt.DrinkToppingId = x.DrinkToppingId
    );

    SET IDENTITY_INSERT dbo.DrinkToppings OFF;

    /* ============================================================
       07. DRINK DEFAULT TOPPINGS

       IDs 1-6 remain EF HasData. IDs 7-14 preserve all Part1 pairs.
       Store1 and extension products receive no synthetic defaults.
       ============================================================ */

    DECLARE @DrinkDefaultToppingSeed TABLE
    (
        DrinkDefaultToppingId int NOT NULL PRIMARY KEY,
        DrinkId int NOT NULL,
        ToppingId int NOT NULL,
        UNIQUE (DrinkId, ToppingId)
    );

    INSERT @DrinkDefaultToppingSeed(DrinkDefaultToppingId, DrinkId, ToppingId)
    VALUES
        (7,3,1),
        (8,12,1),
        (9,13,5),
        (10,14,1),
        (11,15,1),
        (12,21,8),
        (13,22,8),
        (14,24,8),
        (15,53,1),
        (16,55,6),
        (17,58,45),
        (18,59,4),
        (19,67,4),
        (20,71,15),
        (21,72,6),
        (22,73,4),
        (23,77,7),
        (24,78,11),
        (25,79,36);

    IF EXISTS
    (
        SELECT 1
        FROM @DrinkDefaultToppingSeed x
        JOIN dbo.DrinkDefaultToppings ddt
          ON ddt.DrinkDefaultToppingId = x.DrinkDefaultToppingId
          OR (ddt.DrinkId = x.DrinkId AND ddt.ToppingId = x.ToppingId)
        WHERE ddt.DrinkDefaultToppingId <> x.DrinkDefaultToppingId
           OR ddt.DrinkId <> x.DrinkId
           OR ddt.ToppingId <> x.ToppingId
    )
        THROW 52112, N'DrinkDefaultToppings có ID hoặc quan hệ Drink/Topping xung đột.', 1;

    SET IDENTITY_INSERT dbo.DrinkDefaultToppings ON;

    INSERT dbo.DrinkDefaultToppings(DrinkDefaultToppingId, DrinkId, ToppingId)
    SELECT x.DrinkDefaultToppingId, x.DrinkId, x.ToppingId
    FROM @DrinkDefaultToppingSeed x
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.DrinkDefaultToppings ddt
        WHERE ddt.DrinkDefaultToppingId = x.DrinkDefaultToppingId
    );

    SET IDENTITY_INSERT dbo.DrinkDefaultToppings OFF;

    /* ============================================================
       08. STORE TOPPINGS

       EF HasData already publishes Topping 1-2 for Store 1.
       IDs 5-52 publish Topping 3-50 for Store 1.
       ============================================================ */

    DECLARE @StoreToppingSeed TABLE
    (
        StoreToppingId int NOT NULL PRIMARY KEY,
        StoreId int NOT NULL,
        ToppingId int NOT NULL,
        Active bit NOT NULL,
        UNIQUE (StoreId, ToppingId)
    );

    INSERT @StoreToppingSeed(StoreToppingId, StoreId, ToppingId, Active)
    VALUES
        (5,1,3,1),(6,1,4,1),(7,1,5,1),(8,1,6,1),(9,1,7,1),(10,1,8,1),
        (11,1,9,1),(12,1,10,1),(13,1,11,1),(14,1,12,1),(15,1,13,1),
        (16,1,14,1),(17,1,15,1),(18,1,16,1),(19,1,17,1),(20,1,18,1),
        (21,1,19,1),(22,1,20,1),(23,1,21,1),(24,1,22,1),(25,1,23,1),
        (26,1,24,1),(27,1,25,1),(28,1,26,1),(29,1,27,1),(30,1,28,1),
        (31,1,29,1),(32,1,30,1),(33,1,31,1),(34,1,32,1),(35,1,33,1),
        (36,1,34,1),(37,1,35,1),(38,1,36,1),(39,1,37,1),(40,1,38,1),
        (41,1,39,1),(42,1,40,1),(43,1,41,1),(44,1,42,1),(45,1,43,1),
        (46,1,44,1),(47,1,45,1),(48,1,46,1),(49,1,47,1),(50,1,48,1),
        (51,1,49,1),(52,1,50,1);

    IF EXISTS
    (
        SELECT 1
        FROM dbo.StoreToppings
        GROUP BY StoreId, ToppingId
        HAVING COUNT(*) > 1
    )
        THROW 52113, N'StoreToppings đang có nhiều dòng cho cùng Store/Topping.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM @StoreToppingSeed x
        JOIN dbo.StoreToppings st
          ON st.StoreToppingId = x.StoreToppingId
          OR (st.StoreId = x.StoreId AND st.ToppingId = x.ToppingId)
        WHERE st.StoreToppingId <> x.StoreToppingId
           OR st.StoreId <> x.StoreId
           OR st.ToppingId <> x.ToppingId
           OR st.Active <> x.Active
    )
        THROW 52114, N'StoreToppings có ID hoặc quan hệ Store/Topping xung đột.', 1;

    SET IDENTITY_INSERT dbo.StoreToppings ON;

    INSERT dbo.StoreToppings(StoreToppingId, StoreId, ToppingId, Active)
    SELECT x.StoreToppingId, x.StoreId, x.ToppingId, x.Active
    FROM @StoreToppingSeed x
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.StoreToppings st WHERE st.StoreToppingId = x.StoreToppingId
    );

    SET IDENTITY_INSERT dbo.StoreToppings OFF;

    /* ============================================================
       BATCH 02 ACCEPTANCE CHECKS
       ============================================================ */

    IF (SELECT COUNT(*) FROM dbo.Toppings) <> 50
        THROW 52120, N'Tổng số Toppings sau Batch 02 phải bằng 50.', 1;

    IF (SELECT COUNT(*) FROM dbo.DrinkToppings) <> 155
        THROW 52121, N'Tổng số DrinkToppings sau Batch 02 phải bằng 155.', 1;

    IF (SELECT COUNT(*) FROM dbo.DrinkDefaultToppings) <> 25
        THROW 52122, N'Tổng số DrinkDefaultToppings sau Batch 02 phải bằng 25.', 1;

    IF (SELECT COUNT(*) FROM dbo.StoreToppings) < 52
        THROW 52123, N'Batch 02 phải có tối thiểu 52 StoreToppings (Store 1 đủ 50 topping).', 1;

    IF (SELECT COUNT(*) FROM dbo.StoreToppings WHERE StoreId = 1 AND Active = 1) <> 50
        THROW 52124, N'Store 1 phải có đúng 50 Toppings đang hoạt động.', 1;

    IF EXISTS
    (
        SELECT ToppingCode FROM dbo.Toppings GROUP BY ToppingCode HAVING COUNT(*) > 1
    ) OR EXISTS
    (
        SELECT Name FROM dbo.Toppings GROUP BY Name HAVING COUNT(*) > 1
    )
        THROW 52125, N'Phát hiện duplicate ToppingCode hoặc Topping.Name.', 1;

    IF EXISTS
    (
        SELECT DrinkId, ToppingId
        FROM dbo.DrinkToppings
        GROUP BY DrinkId, ToppingId
        HAVING COUNT(*) > 1
    ) OR EXISTS
    (
        SELECT DrinkId, ToppingId
        FROM dbo.DrinkDefaultToppings
        GROUP BY DrinkId, ToppingId
        HAVING COUNT(*) > 1
    ) OR EXISTS
    (
        SELECT StoreId, ToppingId
        FROM dbo.StoreToppings
        GROUP BY StoreId, ToppingId
        HAVING COUNT(*) > 1
    )
        THROW 52126, N'Phát hiện duplicate business key trong bảng liên kết Topping.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.Toppings
        WHERE Price <= 0
    )
        THROW 52127, N'Toppings có giá không dương.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.DrinkToppings dt
        LEFT JOIN dbo.Drinks d ON d.DrinkId = dt.DrinkId
        LEFT JOIN dbo.Toppings t ON t.ToppingId = dt.ToppingId
        WHERE d.DrinkId IS NULL OR t.ToppingId IS NULL
    ) OR EXISTS
    (
        SELECT 1
        FROM dbo.DrinkDefaultToppings ddt
        LEFT JOIN dbo.Drinks d ON d.DrinkId = ddt.DrinkId
        LEFT JOIN dbo.Toppings t ON t.ToppingId = ddt.ToppingId
        WHERE d.DrinkId IS NULL OR t.ToppingId IS NULL
    ) OR EXISTS
    (
        SELECT 1
        FROM dbo.StoreToppings st
        LEFT JOIN dbo.Stores s ON s.StoreId = st.StoreId
        LEFT JOIN dbo.Toppings t ON t.ToppingId = st.ToppingId
        WHERE s.StoreId IS NULL OR t.ToppingId IS NULL
    )
        THROW 52128, N'Phát hiện khóa ngoại không hợp lệ trong Batch 02.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    BEGIN TRY
        SET IDENTITY_INSERT dbo.Toppings OFF;
    END TRY
    BEGIN CATCH
    END CATCH;

    BEGIN TRY
        SET IDENTITY_INSERT dbo.DrinkToppings OFF;
    END TRY
    BEGIN CATCH
    END CATCH;

    BEGIN TRY
        SET IDENTITY_INSERT dbo.DrinkDefaultToppings OFF;
    END TRY
    BEGIN CATCH
    END CATCH;

    BEGIN TRY
        SET IDENTITY_INSERT dbo.StoreToppings OFF;
    END TRY
    BEGIN CATCH
    END CATCH;

    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
SeedAllBatch02Complete:
GO

/* ============================================================
   BATCH 02 READ-ONLY VERIFICATION
   ============================================================ */

SELECT N'Toppings' AS Entity,
       COUNT(*) AS TotalRows,
       MIN(ToppingId) AS MinId,
       MAX(ToppingId) AS MaxId,
       SUM(CASE WHEN ToppingId BETWEEN 1 AND 6 THEN 1 ELSE 0 END) AS FoundationRows,
       SUM(CASE WHEN ToppingId BETWEEN 7 AND 12 THEN 1 ELSE 0 END) AS Part1Rows,
       SUM(CASE WHEN ToppingId = 13 THEN 1 ELSE 0 END) AS Store1Rows,
       SUM(CASE WHEN ToppingId BETWEEN 14 AND 50 THEN 1 ELSE 0 END) AS ExtensionRows
FROM dbo.Toppings
UNION ALL
SELECT N'DrinkToppings', COUNT(*), MIN(DrinkToppingId), MAX(DrinkToppingId),
       SUM(CASE WHEN DrinkToppingId BETWEEN 1 AND 12 THEN 1 ELSE 0 END),
       SUM(CASE WHEN DrinkToppingId BETWEEN 13 AND 37 THEN 1 ELSE 0 END),
       SUM(CASE WHEN DrinkToppingId BETWEEN 38 AND 54 THEN 1 ELSE 0 END),
       SUM(CASE WHEN DrinkToppingId BETWEEN 55 AND 155 THEN 1 ELSE 0 END)
FROM dbo.DrinkToppings
UNION ALL
SELECT N'DrinkDefaultToppings', COUNT(*), MIN(DrinkDefaultToppingId), MAX(DrinkDefaultToppingId),
       SUM(CASE WHEN DrinkDefaultToppingId BETWEEN 1 AND 6 THEN 1 ELSE 0 END),
       SUM(CASE WHEN DrinkDefaultToppingId BETWEEN 7 AND 25 THEN 1 ELSE 0 END), 0, 0
FROM dbo.DrinkDefaultToppings
UNION ALL
SELECT N'StoreToppings', COUNT(*), MIN(StoreToppingId), MAX(StoreToppingId),
       SUM(CASE WHEN StoreToppingId BETWEEN 1 AND 4 THEN 1 ELSE 0 END), 0,
       SUM(CASE WHEN StoreToppingId BETWEEN 5 AND 52 THEN 1 ELSE 0 END), 0
FROM dbo.StoreToppings;

SELECT N'Toppings' AS [Table], N'TC_DEN' AS RetainedCode,
       N'DEMO_TOP_BLACK_PEARL' AS RemovedStore1Code,
       N'Giữ EF ToppingId 1 vì trùng ý nghĩa nghiệp vụ.' AS Decision
UNION ALL
SELECT N'Toppings', N'TC_TRANG', N'DEMO_TOP_WHITE_PEARL', N'Giữ EF ToppingId 2 vì trùng ý nghĩa nghiệp vụ.'
UNION ALL
SELECT N'Toppings', N'BH_FLAN', N'DEMO_TOP_FLAN', N'Giữ EF ToppingId 6 vì trùng ý nghĩa nghiệp vụ.'
UNION ALL
SELECT N'Toppings', N'TH_KM', N'DEMO_TOP_TARO_JELLY', N'Giữ EF ToppingId 5 vì trùng ý nghĩa nghiệp vụ.'
UNION ALL
SELECT N'Toppings', N'KEMCHEESE', N'DEMO_TOP_CHEESE_CREAM', N'Giữ Part1 ToppingId 7 và giá Part1.';

/* ============================================================
   BATCH 03/12
   Tables in this batch:
     1. Units
     2. Ingredients
     3. UnitConversions
     4. PreparedItems

   Source and duplicate analysis:
     - EF HasData owns Unit IDs 1-12, Ingredient IDs 1-13 and the
       24 UnitConversion rows whose IDs end at 72.
     - Part1 has no rows for these four tables.
     - Store1 adds DEMO_PORTION and DEMO_CARTON.
     - Seven Store1 ingredients are aliases of EF ingredients:
         DEMO_ING_CONDENSED_MILK -> ING00002
         DEMO_ING_BLACK_TEA      -> ING00003
         DEMO_ING_SUGAR          -> ING00006
         DEMO_ING_ICE            -> ING00007
         DEMO_ING_MATCHA         -> ING00009
         DEMO_ING_DAIRY_CREAM    -> ING00010
         DEMO_ING_WATER          -> ING00013
       Their offers, recipes and inventory will use the canonical IDs in
       later batches; duplicate Ingredient rows are not recreated.
     - New conversions are physical kg -> g or l -> ml only.
   ============================================================ */
GO

IF OBJECT_ID(N'dbo.Units', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Ingredients', N'U') IS NULL
   OR OBJECT_ID(N'dbo.UnitConversions', N'U') IS NULL
   OR OBJECT_ID(N'dbo.PreparedItems', N'U') IS NULL
    THROW 52200, N'Schema thiếu một trong các bảng của SeedAll Batch 03.', 1;

IF (SELECT COUNT(*) FROM dbo.Toppings) <> 50
   OR (SELECT COUNT(*) FROM dbo.DrinkToppings) <> 155
   OR (SELECT COUNT(*) FROM dbo.DrinkDefaultToppings) <> 25
   OR (SELECT COUNT(*) FROM dbo.StoreToppings) < 52
    THROW 52201, N'Batch 02 chưa hoàn tất đúng contract.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.Units WHERE UnitId = 1 AND UnitCode = N'g' AND Name = N'Gram' AND [Type] = 1 AND Active = 1)
   OR NOT EXISTS (SELECT 1 FROM dbo.Units WHERE UnitId = 2 AND UnitCode = N'kg' AND Name = N'Kilogram' AND [Type] = 1 AND Active = 1)
   OR NOT EXISTS (SELECT 1 FROM dbo.Units WHERE UnitId = 3 AND UnitCode = N'ml' AND Name = N'Milliliter' AND [Type] = 2 AND Active = 1)
   OR NOT EXISTS (SELECT 1 FROM dbo.Units WHERE UnitId = 4 AND UnitCode = N'l' AND Name = N'Liter' AND [Type] = 2 AND Active = 1)
   OR NOT EXISTS (SELECT 1 FROM dbo.Units WHERE UnitId = 9 AND UnitCode = N'pcs' AND [Type] = 3 AND Active = 1)
    THROW 52202, N'Thiếu hoặc sai Unit nền g/kg/ml/l/pcs.', 1;

IF (SELECT COUNT(*) FROM dbo.Ingredients WHERE IngredientId BETWEEN 1 AND 13) <> 13
   OR (SELECT COUNT(*) FROM dbo.UnitConversions WHERE UnitConversionId BETWEEN 1 AND 72) NOT IN (21,24)
    THROW 52203, N'Dữ liệu Ingredient hoặc UnitConversion EF nền không đúng contract.', 1;
GO

IF EXISTS
(
    SELECT 1
    FROM dbo.SystemSettings
    WHERE SettingKey = N'seedall_foundation_inventory_v1'
      AND SettingValue = N'completed'
)
BEGIN
    PRINT N'SeedAll Batch 03 skipped: foundation inventory v1 is already complete.';
    GOTO SeedAllBatch03Complete;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    /* ============================================================
       09. UNITS

       IDs 1-12 remain EF HasData. Store1 count units use IDs 13-14.
       ============================================================ */

    DECLARE @UnitSeed TABLE
    (
        UnitId int NOT NULL PRIMARY KEY,
        UnitCode nvarchar(20) NOT NULL UNIQUE,
        Name nvarchar(100) NOT NULL UNIQUE,
        [Type] int NOT NULL,
        Active bit NOT NULL
    );

    INSERT @UnitSeed(UnitId, UnitCode, Name, [Type], Active)
    VALUES
        (13, N'DEMO_PORTION', N'Phần', 3, 1),
        (14, N'DEMO_CARTON',  N'Thùng', 3, 1);

    IF EXISTS
    (
        SELECT 1
        FROM @UnitSeed x
        JOIN dbo.Units u
          ON u.UnitId = x.UnitId OR u.UnitCode = x.UnitCode OR u.Name = x.Name
        WHERE u.UnitId <> x.UnitId
           OR u.UnitCode <> x.UnitCode
           OR u.Name <> x.Name
           OR u.[Type] <> x.[Type]
           OR u.Active <> x.Active
    )
        THROW 52210, N'Units có ID, Code hoặc Name xung đột với SeedAll Batch 03.', 1;

    SET IDENTITY_INSERT dbo.Units ON;

    INSERT dbo.Units(UnitId, UnitCode, Name, [Type], Active)
    SELECT x.UnitId, x.UnitCode, x.Name, x.[Type], x.Active
    FROM @UnitSeed x
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Units u WHERE u.UnitId = x.UnitId);

    SET IDENTITY_INSERT dbo.Units OFF;

    /* ============================================================
       10. INGREDIENTS

       IDs 14-37: 24 non-duplicate Store1 ingredients.
       IDs 38-50: 13 ingredients required by new drinks/toppings.
       ============================================================ */

    DECLARE @IngredientAliases TABLE
    (
        SourceCode nvarchar(50) NOT NULL PRIMARY KEY,
        CanonicalIngredientId int NOT NULL,
        CanonicalCode nvarchar(50) NOT NULL
    );

    INSERT @IngredientAliases(SourceCode, CanonicalIngredientId, CanonicalCode)
    VALUES
        (N'DEMO_ING_CONDENSED_MILK', 2,  N'ING00002'),
        (N'DEMO_ING_BLACK_TEA',      3,  N'ING00003'),
        (N'DEMO_ING_SUGAR',          6,  N'ING00006'),
        (N'DEMO_ING_ICE',            7,  N'ING00007'),
        (N'DEMO_ING_MATCHA',         9,  N'ING00009'),
        (N'DEMO_ING_DAIRY_CREAM',    10, N'ING00010'),
        (N'DEMO_ING_WATER',          13, N'ING00013');

    IF EXISTS
    (
        SELECT 1
        FROM @IngredientAliases a
        LEFT JOIN dbo.Ingredients i
          ON i.IngredientId = a.CanonicalIngredientId AND i.Code = a.CanonicalCode
        WHERE i.IngredientId IS NULL
    )
        THROW 52211, N'Không resolve được một hoặc nhiều Ingredient canonical.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.Ingredients i
        JOIN @IngredientAliases a ON a.SourceCode = i.Code
    )
        THROW 52212, N'Database đã có Ingredient alias Store1; SeedAll không tự xóa hoặc remap dữ liệu legacy.', 1;

    DECLARE @IngredientSeed TABLE
    (
        IngredientId int NOT NULL PRIMARY KEY,
        Code nvarchar(50) NOT NULL UNIQUE,
        Name nvarchar(200) NOT NULL UNIQUE,
        BaseUnitId int NOT NULL,
        Active bit NOT NULL
    );

    INSERT @IngredientSeed(IngredientId, Code, Name, BaseUnitId, Active)
    VALUES
        (14, N'DEMO_ING_VIET_COFFEE',       N'Cà phê rang xay',          1,  1),
        (15, N'DEMO_ING_ESPRESSO_BEAN',     N'Hạt espresso',             1,  1),
        (16, N'DEMO_ING_FRESH_MILK',        N'Sữa tươi',                 3,  1),
        (17, N'DEMO_ING_SALT',              N'Muối',                     1,  1),
        (18, N'DEMO_ING_SUGAR_SYRUP',       N'Syrup đường đóng chai',    3,  1),
        (19, N'DEMO_ING_OOLONG_TEA',        N'Trà ô long khô',           1,  1),
        (20, N'DEMO_ING_CANNED_PEACH',      N'Đào ngâm',                 1,  1),
        (21, N'DEMO_ING_CANNED_LYCHEE',     N'Vải ngâm',                 1,  1),
        (22, N'DEMO_ING_PASSION_JAM',       N'Mứt chanh dây',            1,  1),
        (23, N'DEMO_ING_ORANGE',            N'Cam tươi',                 1,  1),
        (24, N'DEMO_ING_LEMONGRASS',        N'Sả',                       1,  1),
        (25, N'DEMO_ING_CHOCOLATE',         N'Bột chocolate',            1,  1),
        (26, N'DEMO_ING_FRAPPE',            N'Bột frappe',               1,  1),
        (27, N'DEMO_ING_BLACK_PEARL_DRY',   N'Trân châu đen khô',        1,  1),
        (28, N'DEMO_ING_WHITE_PEARL',       N'Trân châu trắng',         13,  1),
        (29, N'DEMO_ING_TARO_JELLY_POWDER', N'Bột rau câu khoai môn',    1,  1),
        (30, N'DEMO_ING_FLAN_POWDER',       N'Bột flan',                 1,  1),
        (31, N'DEMO_ING_CHEESE_POWDER',     N'Bột kem cheese',           1,  1),
        (32, N'DEMO_ING_CUP_M',             N'Ly nhựa M',                9,  1),
        (33, N'DEMO_ING_CUP_L',             N'Ly nhựa L',                9,  1),
        (34, N'DEMO_ING_LID_M',             N'Nắp ly M',                 9,  1),
        (35, N'DEMO_ING_LID_L',             N'Nắp ly L',                 9,  1),
        (36, N'DEMO_ING_STRAW',             N'Ống hút',                  9,  1),
        (37, N'DEMO_ING_BAG',               N'Túi mang đi',              9,  1),
        (38, N'DEMO_ING_HONEY',             N'Mật ong',                  1,  1),
        (39, N'DEMO_ING_YELLOW_LEMON',      N'Chanh vàng',               1,  1),
        (40, N'DEMO_ING_MANGO_PUREE',       N'Puree xoài',               1,  1),
        (41, N'DEMO_ING_STRAWBERRY_PUREE',  N'Puree dâu',                1,  1),
        (42, N'DEMO_ING_OAT_MILK',          N'Sữa yến mạch',             3,  1),
        (43, N'DEMO_ING_CARAMEL_SYRUP',     N'Syrup caramel',            3,  1),
        (44, N'DEMO_ING_COCONUT_MILK',      N'Nước cốt dừa',             3,  1),
        (45, N'DEMO_ING_YOGURT',            N'Sữa chua',                 1,  1),
        (46, N'DEMO_ING_CHEESE_CUBE',       N'Phô mai viên',             9,  1),
        (47, N'DEMO_ING_KHUC_BACH_POWDER',  N'Bột khúc bạch',            1,  1),
        (48, N'DEMO_ING_ALOE_VERA',         N'Nha đam',                  1,  1),
        (49, N'DEMO_ING_CHIA_SEED',         N'Hạt chia',                 1,  1),
        (50, N'DEMO_ING_COCONUT_JELLY',     N'Thạch dừa',                1,  1);

    IF EXISTS
    (
        SELECT 1
        FROM @IngredientSeed x
        JOIN dbo.Ingredients i
          ON i.IngredientId = x.IngredientId OR i.Code = x.Code OR i.Name = x.Name
        WHERE i.IngredientId <> x.IngredientId
           OR i.Code <> x.Code
           OR i.Name <> x.Name
           OR i.BaseUnitId <> x.BaseUnitId
           OR i.Active <> x.Active
    )
        THROW 52213, N'Ingredients có ID, Code hoặc Name xung đột với SeedAll Batch 03.', 1;

    SET IDENTITY_INSERT dbo.Ingredients ON;

    INSERT dbo.Ingredients(IngredientId, Code, Name, BaseUnitId, Active)
    SELECT x.IngredientId, x.Code, x.Name, x.BaseUnitId, x.Active
    FROM @IngredientSeed x
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.Ingredients i WHERE i.IngredientId = x.IngredientId
    );

    SET IDENTITY_INSERT dbo.Ingredients OFF;

    /* ============================================================
       11. UNIT CONVERSIONS

       The physical EF rows remain unchanged. IDs 73-101 add one physical
       purchase/display-unit conversion for each retained/new mass or volume
       item. Commercial package quantities belong to IngredientSuppliers and
       are never represented as UnitConversions.
       ============================================================ */

    DECLARE @UnitConversionSeed TABLE
    (
        UnitConversionId int NOT NULL PRIMARY KEY,
        IngredientId int NOT NULL,
        FromUnitId int NOT NULL,
        FromQuantity decimal(18,5) NOT NULL,
        ToUnitId int NOT NULL,
        ToQuantity decimal(18,5) NOT NULL,
        Active bit NOT NULL,
        UNIQUE (IngredientId, FromUnitId, ToUnitId)
    );

    INSERT @UnitConversionSeed
    (UnitConversionId, IngredientId, FromUnitId, FromQuantity, ToUnitId, ToQuantity, Active)
    VALUES
        (73,14,2,1,1,1000,1),
        (74,15,2,1,1,1000,1),
        (75,16,4,1,3,1000,1),
        (76,17,2,1,1,1000,1),
        (77,18,4,1,3,1000,1),
        (78,19,2,1,1,1000,1),
        (79,20,2,1,1,1000,1),
        (80,21,2,1,1,1000,1),
        (81,22,2,1,1,1000,1),
        (82,23,2,1,1,1000,1),
        (83,24,2,1,1,1000,1),
        (84,25,2,1,1,1000,1),
        (85,26,2,1,1,1000,1),
        (86,27,2,1,1,1000,1),
        (87,29,2,1,1,1000,1),
        (88,30,2,1,1,1000,1),
        (89,31,2,1,1,1000,1),
        (90,38,2,1,1,1000,1),
        (91,39,2,1,1,1000,1),
        (92,40,2,1,1,1000,1),
        (93,41,2,1,1,1000,1),
        (94,42,4,1,3,1000,1),
        (95,43,4,1,3,1000,1),
        (96,44,4,1,3,1000,1),
        (97,45,2,1,1,1000,1),
        (98,47,2,1,1,1000,1),
        (99,48,2,1,1,1000,1),
        (100,49,2,1,1,1000,1),
        (101,50,2,1,1,1000,1);

    IF EXISTS
    (
        SELECT 1
        FROM @UnitConversionSeed x
        JOIN dbo.UnitConversions c
          ON c.UnitConversionId = x.UnitConversionId
          OR (c.IngredientId = x.IngredientId AND c.FromUnitId = x.FromUnitId AND c.ToUnitId = x.ToUnitId)
        WHERE c.UnitConversionId <> x.UnitConversionId
           OR c.IngredientId <> x.IngredientId
           OR c.FromUnitId <> x.FromUnitId
           OR c.FromQuantity <> x.FromQuantity
           OR c.ToUnitId <> x.ToUnitId
           OR c.ToQuantity <> x.ToQuantity
           OR c.Active <> x.Active
    )
        THROW 52214, N'UnitConversions có ID hoặc quan hệ Ingredient/Unit xung đột.', 1;

    SET IDENTITY_INSERT dbo.UnitConversions ON;

    INSERT dbo.UnitConversions
    (UnitConversionId, IngredientId, FromUnitId, FromQuantity, ToUnitId, ToQuantity, Active)
    SELECT x.UnitConversionId, x.IngredientId, x.FromUnitId, x.FromQuantity,
           x.ToUnitId, x.ToQuantity, x.Active
    FROM @UnitConversionSeed x
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.UnitConversions c WHERE c.UnitConversionId = x.UnitConversionId
    );

    SET IDENTITY_INSERT dbo.UnitConversions OFF;

    /* ============================================================
       12. PREPARED ITEMS

       Store1 is the only source. PreparedItem is the stable inventory
       identity; recipe versions are seeded in the next batch.
       ============================================================ */

    DECLARE @PreparedItemSeed TABLE
    (
        PreparedItemId int NOT NULL PRIMARY KEY,
        Code nvarchar(50) NOT NULL UNIQUE,
        Name nvarchar(200) NOT NULL UNIQUE,
        BaseUnitId int NOT NULL,
        Description nvarchar(500) NULL,
        Active bit NOT NULL
    );

    INSERT @PreparedItemSeed(PreparedItemId, Code, Name, BaseUnitId, Description, Active)
    VALUES
        (1, N'DEMO_PREP_VIET_COFFEE',  N'Cốt cà phê Việt',        3,  N'Bán thành phẩm demo Store 1', 1),
        (2, N'DEMO_PREP_ESPRESSO',     N'Espresso shot',          3,  N'Bán thành phẩm demo Store 1', 1),
        (3, N'DEMO_PREP_BLACK_TEA',    N'Cốt trà đen',            3,  N'Bán thành phẩm demo Store 1', 1),
        (4, N'DEMO_PREP_OOLONG_TEA',   N'Cốt trà ô long',         3,  N'Bán thành phẩm demo Store 1', 1),
        (5, N'DEMO_PREP_SUGAR_SYRUP',  N'Syrup đường',            3,  N'Bán thành phẩm demo Store 1', 1),
        (6, N'DEMO_PREP_SALTED_CREAM', N'Kem muối',               3,  N'Bán thành phẩm demo Store 1', 1),
        (7, N'DEMO_PREP_CHEESE_CREAM', N'Kem cheese',             3,  N'Bán thành phẩm demo Store 1', 1),
        (8, N'DEMO_PREP_BLACK_PEARL', N'Tran chau den da nau', 13, N'Ban thanh pham demo Store 1', 1),
        (9, N'DEMO_PREP_ALOE_BASE', N'Aloe vera base', 1, N'AI dashboard prepared-item fixture', 1),
        (10, N'DEMO_PREP_COCONUT_JELLY_BASE', N'Coconut jelly base', 1, N'AI dashboard prepared-item fixture', 1),
        (11, N'DEMO_PREP_KHUC_BACH_BASE', N'Khuc bach base', 1, N'AI dashboard prepared-item fixture', 1),
        (12, N'DEMO_PREP_LEGACY_CREAM', N'Legacy cream', 3, N'Archived prepared-item fixture', 0);

    IF EXISTS
    (
        SELECT 1
        FROM @PreparedItemSeed x
        JOIN dbo.PreparedItems p
          ON p.PreparedItemId = x.PreparedItemId OR p.Code = x.Code OR p.Name = x.Name
        WHERE p.PreparedItemId <> x.PreparedItemId
           OR p.Code <> x.Code
           OR p.Name <> x.Name
           OR p.BaseUnitId <> x.BaseUnitId
           OR ISNULL(p.Description, N'') <> ISNULL(x.Description, N'')
           OR p.Active <> x.Active
    )
        THROW 52215, N'PreparedItems có ID, Code hoặc Name xung đột với Store1.', 1;

    SET IDENTITY_INSERT dbo.PreparedItems ON;

    INSERT dbo.PreparedItems(PreparedItemId, Code, Name, BaseUnitId, Description, Active)
    SELECT x.PreparedItemId, x.Code, x.Name, x.BaseUnitId, x.Description, x.Active
    FROM @PreparedItemSeed x
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.PreparedItems p WHERE p.PreparedItemId = x.PreparedItemId
    );

    SET IDENTITY_INSERT dbo.PreparedItems OFF;

    /* ============================================================
       BATCH 03 ACCEPTANCE CHECKS
       ============================================================ */

    IF (SELECT COUNT(*) FROM dbo.Units) <> 14
        THROW 52220, N'Tổng số Units sau Batch 03 phải bằng 14.', 1;

    IF (SELECT COUNT(*) FROM dbo.Ingredients) <> 50
        THROW 52221, N'Tổng số Ingredients sau Batch 03 phải bằng 50.', 1;

    /* Three legacy EF package conversions still exist until the normalization
       batch immediately below removes them. No new package conversion is added. */
    IF (SELECT COUNT(*) FROM dbo.UnitConversions) <> 53
        THROW 52222, N'Tổng số UnitConversions trước normalization phải bằng 53.', 1;

    IF (SELECT COUNT(*) FROM dbo.PreparedItems) <> 12
        THROW 52223, N'PreparedItems count must be 12.', 1;

    IF EXISTS
    (
        SELECT UnitCode FROM dbo.Units GROUP BY UnitCode HAVING COUNT(*) > 1
    ) OR EXISTS
    (
        SELECT Code FROM dbo.Ingredients GROUP BY Code HAVING COUNT(*) > 1
    ) OR EXISTS
    (
        SELECT Code FROM dbo.PreparedItems GROUP BY Code HAVING COUNT(*) > 1
    ) OR EXISTS
    (
        SELECT IngredientId, FromUnitId, ToUnitId
        FROM dbo.UnitConversions
        GROUP BY IngredientId, FromUnitId, ToUnitId
        HAVING COUNT(*) > 1
    )
        THROW 52224, N'Phát hiện duplicate business key trong Batch 03.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM @UnitConversionSeed c
        JOIN dbo.Ingredients i ON i.IngredientId = c.IngredientId
        WHERE c.FromQuantity <> 1
           OR c.FromUnitId = c.ToUnitId
           OR c.ToUnitId <> i.BaseUnitId
           OR NOT (c.ToQuantity = 1000 AND
                    ((c.FromUnitId = 2 AND c.ToUnitId = 1)
                     OR (c.FromUnitId = 4 AND c.ToUnitId = 3)))
    )
        THROW 52225, N'UnitConversion mới không đúng kg-g hoặc l-ml/base unit.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.UnitConversions c
        LEFT JOIN dbo.Ingredients i ON i.IngredientId = c.IngredientId
        LEFT JOIN dbo.Units uf ON uf.UnitId = c.FromUnitId
        LEFT JOIN dbo.Units ut ON ut.UnitId = c.ToUnitId
        WHERE i.IngredientId IS NULL OR uf.UnitId IS NULL OR ut.UnitId IS NULL
           OR c.FromQuantity <= 0 OR c.ToQuantity <= 0 OR c.FromUnitId = c.ToUnitId
    ) OR EXISTS
    (
        SELECT 1
        FROM dbo.PreparedItems p
        LEFT JOIN dbo.Units u ON u.UnitId = p.BaseUnitId
        WHERE u.UnitId IS NULL
    )
        THROW 52226, N'Phát hiện FK hoặc quantity không hợp lệ trong Batch 03.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    BEGIN TRY
        SET IDENTITY_INSERT dbo.Units OFF;
    END TRY
    BEGIN CATCH
    END CATCH;

    BEGIN TRY
        SET IDENTITY_INSERT dbo.Ingredients OFF;
    END TRY
    BEGIN CATCH
    END CATCH;

    BEGIN TRY
        SET IDENTITY_INSERT dbo.UnitConversions OFF;
    END TRY
    BEGIN CATCH
    END CATCH;

    BEGIN TRY
        SET IDENTITY_INSERT dbo.PreparedItems OFF;
    END TRY
    BEGIN CATCH
    END CATCH;

    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
SeedAllBatch03Complete:
GO

/* ============================================================
   BATCH 03A - INVENTORY UOM / SUPPLIER CONTENT NORMALIZATION

   This batch always runs, including when foundation_inventory_v1 is already
   complete. It repairs only deterministic SeedAll-owned records and accepts
   both the legacy representation and the canonical representation.

   IngredientSupplier.UnitId is the physical content unit. PackageQuantity is
   the total content of one purchased package expressed in Ingredient.BaseUnit.
   Commercial package forms remain catalog/display concepts and never become
   UnitConversions.
   ============================================================ */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @GramUnitId int=(SELECT UnitId FROM dbo.Units WHERE UnitCode=N'g');
    DECLARE @MilliliterUnitId int=(SELECT UnitId FROM dbo.Units WHERE UnitCode=N'ml');
    DECLARE @PieceUnitId int=(SELECT UnitId FROM dbo.Units WHERE UnitCode=N'pcs');

    IF @GramUnitId IS NULL OR @MilliliterUnitId IS NULL OR @PieceUnitId IS NULL
        THROW 52230,N'SEED_UOM_NORMALIZATION: thiếu Unit g, ml hoặc pcs.',1;

    DECLARE @CanonicalIngredientBase TABLE
    (
        IngredientId int NOT NULL PRIMARY KEY,
        Code nvarchar(50) NOT NULL UNIQUE,
        BaseUnitId int NOT NULL
    );
    INSERT @CanonicalIngredientBase(IngredientId,Code,BaseUnitId) VALUES
      (1,N'ING00001',@GramUnitId),
      (2,N'ING00002',@MilliliterUnitId),
      (8,N'ING00008',@MilliliterUnitId),
      (10,N'ING00010',@MilliliterUnitId),
      (13,N'ING00013',@MilliliterUnitId),
      (14,N'DEMO_ING_VIET_COFFEE',@GramUnitId),
      (15,N'DEMO_ING_ESPRESSO_BEAN',@GramUnitId),
      (16,N'DEMO_ING_FRESH_MILK',@MilliliterUnitId),
      (18,N'DEMO_ING_SUGAR_SYRUP',@MilliliterUnitId),
      (32,N'DEMO_ING_CUP_M',@PieceUnitId),
      (33,N'DEMO_ING_CUP_L',@PieceUnitId),
      (34,N'DEMO_ING_LID_M',@PieceUnitId),
      (35,N'DEMO_ING_LID_L',@PieceUnitId),
      (36,N'DEMO_ING_STRAW',@PieceUnitId),
      (37,N'DEMO_ING_BAG',@PieceUnitId),
      (42,N'DEMO_ING_OAT_MILK',@MilliliterUnitId),
      (43,N'DEMO_ING_CARAMEL_SYRUP',@MilliliterUnitId),
      (44,N'DEMO_ING_COCONUT_MILK',@MilliliterUnitId);

    IF EXISTS
    (
        SELECT 1 FROM @CanonicalIngredientBase x
        LEFT JOIN dbo.Ingredients i ON i.IngredientId=x.IngredientId AND i.Code=x.Code
        WHERE i.IngredientId IS NULL
    ) THROW 52231,N'SEED_UOM_NORMALIZATION: Ingredient target không khớp deterministic ID/code.',1;

    UPDATE i SET BaseUnitId=x.BaseUnitId
    FROM dbo.Ingredients i
    JOIN @CanonicalIngredientBase x ON x.IngredientId=i.IngredientId AND x.Code=i.Code
    WHERE i.BaseUnitId<>x.BaseUnitId;

    /* Remove only the nine known legacy package conversions. A conflicting
       row/key is not silently overwritten or deleted. */
    DECLARE @LegacyPackageConversion TABLE
    (
        UnitConversionId int NOT NULL PRIMARY KEY,
        IngredientId int NOT NULL,
        FromUnitId int NOT NULL,
        FromQuantity decimal(18,5) NOT NULL,
        ToUnitId int NOT NULL,
        ToQuantity decimal(18,5) NOT NULL,
        UNIQUE(IngredientId,FromUnitId,ToUnitId)
    );
    INSERT @LegacyPackageConversion VALUES
      (70,8,10,1,3,750),(71,2,11,1,3,300),(72,13,11,1,3,500),
      (102,32,14,1,9,1000),(103,33,14,1,9,1000),
      (104,34,14,1,9,1000),(105,35,14,1,9,1000),
      (106,36,14,1,9,2000),(107,37,14,1,9,500);

    IF EXISTS
    (
        SELECT 1
        FROM @LegacyPackageConversion x
        JOIN dbo.UnitConversions c
          ON c.UnitConversionId=x.UnitConversionId
          OR (c.IngredientId=x.IngredientId AND c.FromUnitId=x.FromUnitId AND c.ToUnitId=x.ToUnitId)
        WHERE c.UnitConversionId<>x.UnitConversionId OR c.IngredientId<>x.IngredientId
           OR c.FromUnitId<>x.FromUnitId OR c.FromQuantity<>x.FromQuantity
           OR c.ToUnitId<>x.ToUnitId OR c.ToQuantity<>x.ToQuantity
    ) THROW 52232,N'SEED_UOM_NORMALIZATION: legacy package conversion có payload xung đột.',1;

    DELETE c
    FROM dbo.UnitConversions c
    JOIN @LegacyPackageConversion x ON x.UnitConversionId=c.UnitConversionId
       AND x.IngredientId=c.IngredientId AND x.FromUnitId=c.FromUnitId
       AND x.FromQuantity=c.FromQuantity AND x.ToUnitId=c.ToUnitId
       AND x.ToQuantity=c.ToQuantity;

    DECLARE @FoundationOfferKey TABLE
    (
        IngredientSupplierId int NOT NULL PRIMARY KEY,
        IngredientId int NOT NULL,
        SupplierId int NOT NULL
    );
    INSERT @FoundationOfferKey VALUES
      (1,6,1),(2,2,2),(3,1,3),(4,8,4),(5,10,2),
      (6,9,5),(7,5,3),(8,4,1),(9,3,4);

    IF EXISTS
    (
        SELECT 1 FROM @FoundationOfferKey x
        LEFT JOIN dbo.IngredientSuppliers o ON o.IngredientSupplierId=x.IngredientSupplierId
        WHERE o.IngredientSupplierId IS NULL OR o.IngredientId<>x.IngredientId OR o.SupplierId<>x.SupplierId
    ) THROW 52233,N'SEED_UOM_NORMALIZATION: foundation offer không khớp deterministic business key.',1;

    IF EXISTS
    (
        SELECT 1 FROM dbo.IngredientSuppliers o
        WHERE (o.IngredientSupplierId BETWEEN 10 AND 40 AND ISNULL(o.Note,N'') NOT LIKE N'DEMO_OFFER_%')
           OR (o.IngredientSupplierId BETWEEN 41 AND 100 AND ISNULL(o.Note,N'') NOT LIKE N'SEEDALL_%')
    ) THROW 52234,N'SEED_UOM_NORMALIZATION: offer ID trong dải SeedAll không có seed marker hợp lệ.',1;

    DECLARE @SeedOfferContent TABLE
    (
        IngredientSupplierId int NOT NULL PRIMARY KEY,
        LegacyUnitId int NOT NULL,
        LegacyQuantity decimal(18,5) NOT NULL,
        BaseUnitId int NOT NULL,
        CanonicalQuantity decimal(18,5) NOT NULL
    );

    INSERT @SeedOfferContent
    (IngredientSupplierId,LegacyUnitId,LegacyQuantity,BaseUnitId,CanonicalQuantity)
    SELECT o.IngredientSupplierId,o.UnitId,o.PackageQuantity,i.BaseUnitId,
           CONVERT(decimal(18,5),o.PackageQuantity*factor.FactorToBase)
    FROM dbo.IngredientSuppliers o
    JOIN dbo.Ingredients i ON i.IngredientId=o.IngredientId
    JOIN dbo.Units sourceUnit ON sourceUnit.UnitId=o.UnitId
    JOIN dbo.Units baseUnit ON baseUnit.UnitId=i.BaseUnitId
    CROSS APPLY
    (
        SELECT CONVERT(decimal(18,5),CASE
          WHEN o.UnitId=i.BaseUnitId THEN 1
          WHEN LOWER(sourceUnit.UnitCode)=N'kg' AND LOWER(baseUnit.UnitCode)=N'g' THEN 1000
          WHEN LOWER(sourceUnit.UnitCode)=N'g' AND LOWER(baseUnit.UnitCode)=N'kg' THEN 0.001
          WHEN LOWER(sourceUnit.UnitCode)=N'l' AND LOWER(baseUnit.UnitCode)=N'ml' THEN 1000
          WHEN LOWER(sourceUnit.UnitCode)=N'ml' AND LOWER(baseUnit.UnitCode)=N'l' THEN 0.001
          WHEN LOWER(sourceUnit.UnitCode)=N'demo_carton' AND LOWER(baseUnit.UnitCode)=N'pcs'
            THEN CASE o.IngredientId WHEN 36 THEN 2000 WHEN 37 THEN 500 ELSE 1000 END
          ELSE NULL END) FactorToBase
    ) factor
    WHERE o.PackageQuantity>0 AND factor.FactorToBase>0
      AND
      (
          EXISTS(SELECT 1 FROM @FoundationOfferKey f WHERE f.IngredientSupplierId=o.IngredientSupplierId)
          OR (o.IngredientSupplierId BETWEEN 10 AND 40 AND o.Note LIKE N'DEMO_OFFER_%')
          OR (o.IngredientSupplierId BETWEEN 41 AND 100 AND o.Note LIKE N'SEEDALL_%')
      );

    IF EXISTS
    (
        SELECT 1 FROM dbo.IngredientSuppliers o
        WHERE
        (
            EXISTS(SELECT 1 FROM @FoundationOfferKey f WHERE f.IngredientSupplierId=o.IngredientSupplierId)
            OR (o.IngredientSupplierId BETWEEN 10 AND 40 AND o.Note LIKE N'DEMO_OFFER_%')
            OR (o.IngredientSupplierId BETWEEN 41 AND 100 AND o.Note LIKE N'SEEDALL_%')
        )
        AND NOT EXISTS(SELECT 1 FROM @SeedOfferContent x WHERE x.IngredientSupplierId=o.IngredientSupplierId)
    ) THROW 52235,N'SEED_UOM_NORMALIZATION: offer SeedAll không quy đổi an toàn được về Ingredient.BaseUnit.',1;

    UPDATE h SET PackageUnitId=x.BaseUnitId,PackageQuantity=x.CanonicalQuantity
    FROM dbo.IngredientSupplierPriceHistories h
    JOIN @SeedOfferContent x ON x.IngredientSupplierId=h.IngredientSupplierId
    WHERE h.IngredientSupplierPriceHistoryId BETWEEN 1 AND 294
      AND (ISNULL(h.PackageUnitId,-1)<>x.BaseUnitId OR ISNULL(h.PackageQuantity,-1)<>x.CanonicalQuantity);

    UPDATE line
       SET PackageUnitIdSnapshot=x.BaseUnitId,
           PackageQuantitySnapshot=x.CanonicalQuantity,
           PackSizeProcurementQuantity=CASE
             WHEN line.PackSizeProcurementQuantity=x.LegacyQuantity THEN x.CanonicalQuantity
             ELSE line.PackSizeProcurementQuantity END,
           ProcurementUnitId=CASE
             WHEN line.ProcurementUnitId=x.LegacyUnitId THEN x.BaseUnitId
             ELSE line.ProcurementUnitId END
    FROM dbo.PurchaseOrderLines line
    JOIN dbo.PurchaseOrders po ON po.PurchaseOrderId=line.PurchaseOrderId
    JOIN @SeedOfferContent x ON x.IngredientSupplierId=line.IngredientSupplierId
    WHERE po.Code LIKE N'SIV2-%' OR po.Note LIKE N'DEMO_%' OR po.Note LIKE N'SEEDALL_%';

    UPDATE line
       SET PackageUnitIdSnapshot=x.BaseUnitId,
           PackageQuantitySnapshot=x.CanonicalQuantity,
           InputUnitId=CASE WHEN line.InputUnitId=x.LegacyUnitId THEN x.BaseUnitId ELSE line.InputUnitId END,
           ProcurementUnitId=CASE
             WHEN line.ProcurementUnitId=x.LegacyUnitId THEN x.BaseUnitId
             ELSE line.ProcurementUnitId END
    FROM dbo.BranchReceiptLines line
    JOIN dbo.BranchReceipts receipt ON receipt.BranchReceiptId=line.BranchReceiptId
    JOIN @SeedOfferContent x ON x.IngredientSupplierId=line.IngredientSupplierId
    WHERE receipt.ReceiptCode LIKE N'SIV2-%' OR receipt.ReceiptCode LIKE N'DEMO-%'
       OR receipt.Notes LIKE N'DEMO_%' OR receipt.Notes LIKE N'SEEDALL_%';

    UPDATE line
       SET PackageUnitId=x.BaseUnitId,
           PackageQuantitySnapshot=x.CanonicalQuantity,
           ProcurementUnitId=CASE
             WHEN line.ProcurementUnitId=x.LegacyUnitId THEN x.BaseUnitId
             ELSE line.ProcurementUnitId END
    FROM dbo.PurchaseOrderBatchLines line
    JOIN dbo.PurchaseOrderBatches batchHeader
      ON batchHeader.PurchaseOrderBatchId=line.PurchaseOrderBatchId
    JOIN @SeedOfferContent x ON x.IngredientSupplierId=line.IngredientSupplierId
    WHERE batchHeader.RequestKey LIKE N'DEMO_%' OR batchHeader.Note LIKE N'DEMO_%'
       OR line.Note LIKE N'DEMO_%' OR line.Note LIKE N'SEEDALL_%';

    UPDATE o
       SET UnitId=x.BaseUnitId,PackageQuantity=x.CanonicalQuantity
    FROM dbo.IngredientSuppliers o
    JOIN @SeedOfferContent x ON x.IngredientSupplierId=o.IngredientSupplierId
    WHERE o.UnitId<>x.BaseUnitId OR o.PackageQuantity<>x.CanonicalQuantity;

    IF EXISTS
    (
        SELECT 1 FROM @CanonicalIngredientBase x
        JOIN dbo.Ingredients i ON i.IngredientId=x.IngredientId
        WHERE i.BaseUnitId<>x.BaseUnitId
    ) THROW 52236,N'SEED_UOM_NORMALIZATION: Ingredient base unit chưa canonical.',1;

    IF EXISTS
    (
        SELECT 1 FROM dbo.UnitConversions c
        WHERE c.UnitConversionId IN(70,71,72,102,103,104,105,106,107)
    ) THROW 52237,N'SEED_UOM_NORMALIZATION: vẫn còn package UnitConversion legacy.',1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/* ============================================================
   BATCH 03 READ-ONLY VERIFICATION
   ============================================================ */

SELECT N'Units' AS Entity,
       COUNT(*) AS TotalRows,
       MIN(UnitId) AS MinId,
       MAX(UnitId) AS MaxId,
       SUM(CASE WHEN UnitId BETWEEN 1 AND 12 THEN 1 ELSE 0 END) AS FoundationRows,
       SUM(CASE WHEN UnitId BETWEEN 13 AND 14 THEN 1 ELSE 0 END) AS Store1Rows,
       0 AS ExtensionRows
FROM dbo.Units
UNION ALL
SELECT N'Ingredients', COUNT(*), MIN(IngredientId), MAX(IngredientId),
       SUM(CASE WHEN IngredientId BETWEEN 1 AND 13 THEN 1 ELSE 0 END),
       SUM(CASE WHEN IngredientId BETWEEN 14 AND 37 THEN 1 ELSE 0 END),
       SUM(CASE WHEN IngredientId BETWEEN 38 AND 50 THEN 1 ELSE 0 END)
FROM dbo.Ingredients
UNION ALL
SELECT N'UnitConversions', COUNT(*), MIN(UnitConversionId), MAX(UnitConversionId),
       SUM(CASE WHEN UnitConversionId BETWEEN 1 AND 72 THEN 1 ELSE 0 END),
       SUM(CASE WHEN UnitConversionId BETWEEN 73 AND 89 THEN 1 ELSE 0 END),
       SUM(CASE WHEN UnitConversionId BETWEEN 90 AND 101 THEN 1 ELSE 0 END)
FROM dbo.UnitConversions
UNION ALL
SELECT N'PreparedItems', COUNT(*), MIN(PreparedItemId), MAX(PreparedItemId),
       0, SUM(CASE WHEN PreparedItemId BETWEEN 1 AND 12 THEN 1 ELSE 0 END), 0
FROM dbo.PreparedItems;

SELECT N'Ingredients' AS [Table], a.CanonicalCode AS RetainedCode,
       a.SourceCode AS RemovedStore1Code,
       N'Giữ Ingredient EF canonical; remap offer, BOM và tồn kho ở batch sau.' AS Decision
FROM
(
    VALUES
        (N'DEMO_ING_CONDENSED_MILK', N'ING00002'),
        (N'DEMO_ING_BLACK_TEA',      N'ING00003'),
        (N'DEMO_ING_SUGAR',          N'ING00006'),
        (N'DEMO_ING_ICE',            N'ING00007'),
        (N'DEMO_ING_MATCHA',         N'ING00009'),
        (N'DEMO_ING_DAIRY_CREAM',    N'ING00010'),
        (N'DEMO_ING_WATER',          N'ING00013')
) a(SourceCode, CanonicalCode);

/* ============================================================
   BATCH 04/12 - RECIPES, RECIPE DETAILS, TOPPING POLICIES
   - EF Recipes 1-6 and RecipeDetails 1-21 remain unchanged.
   - Store1: 42 recipe headers and all 255 BOM lines are retained.
   - Seven duplicate Store1 ingredients resolve to canonical Ingredient codes.
   - Duplicate black/white pearl headers remain Archived; EF recipes stay active.
   - Extensions: 22 M/L drink recipes and 44 topping cost recipes.
   ============================================================ */
IF EXISTS (SELECT 1 FROM dbo.SystemSettings
           WHERE SettingKey=N'seedall_foundation_inventory_v1' AND SettingValue=N'completed')
BEGIN
 PRINT N'SeedAll Batch 04 skipped: foundation inventory v1 is already complete.';
 GOTO SeedAllBatch04Complete;
END;
BEGIN TRY
 BEGIN TRANSACTION;
 IF OBJECT_ID(N'dbo.Recipes',N'U') IS NULL OR OBJECT_ID(N'dbo.RecipeDetails',N'U') IS NULL
    OR OBJECT_ID(N'dbo.DrinkSizeToppingPolicies',N'U') IS NULL
  THROW 52300,N'Schema thiếu bảng bắt buộc của SeedAll Batch 04.',1;

 IF (SELECT COUNT(*) FROM dbo.Recipes WHERE RecipeId BETWEEN 1 AND 6)<>6
 OR EXISTS(SELECT 1 FROM (VALUES
   (1,N'RCP_CF_SUA',1,1,NULL),(2,N'RCP_CF_DEN',2,1,NULL),(3,N'RCP_TS',3,1,NULL),
   (4,N'RCP_TS_SOCOLA',4,1,NULL),(5,N'RCP_TC_DEN',NULL,NULL,1),(6,N'RCP_TC_TRANG',NULL,NULL,2)
 )x(RecipeId,RecipeCode,DrinkId,SizeId,ToppingId)
 LEFT JOIN dbo.Recipes r ON r.RecipeId=x.RecipeId
 WHERE r.RecipeId IS NULL OR r.RecipeCode<>x.RecipeCode
 OR ISNULL(r.DrinkId,-1)<>ISNULL(x.DrinkId,-1) OR ISNULL(r.SizeId,-1)<>ISNULL(x.SizeId,-1)
 OR ISNULL(r.ToppingId,-1)<>ISNULL(x.ToppingId,-1) OR r.Active<>1 OR r.Status<>N'Active')
  THROW 52301,N'Recipes EF IDs 1-6 thiếu hoặc khác contract migration.',1;

 IF (SELECT COUNT(*) FROM dbo.RecipeDetails WHERE RecipeDetailId BETWEEN 1 AND 21)<>21
 OR EXISTS(SELECT 1 FROM dbo.RecipeDetails WHERE RecipeDetailId BETWEEN 1 AND 21
    AND(Quantity<=0 OR NOT((IngredientId IS NOT NULL AND ChildRecipeId IS NULL)
                       OR(IngredientId IS NULL AND ChildRecipeId IS NOT NULL))))
  THROW 52302,N'RecipeDetails EF IDs 1-21 thiếu hoặc vi phạm contract.',1;

 DECLARE @ActorStaffId int;
 SELECT TOP(1) @ActorStaffId=s.StaffId FROM dbo.Staffs s
 JOIN dbo.Accounts a ON a.AccountId=s.AccountId AND a.Active=1
 JOIN dbo.AccountRoles ar ON ar.AccountId=a.AccountId
 JOIN dbo.Roles r ON r.RoleId=ar.RoleId AND r.Active=1
 WHERE s.StoreId=1 AND s.Active=1 AND r.Name=N'Chủ doanh nghiệp' ORDER BY s.StaffId;
 IF @ActorStaffId IS NULL THROW 52303,N'Store 1 thiếu Staff active có role Chủ doanh nghiệp.',1;

 DECLARE @RecipeSeed TABLE(RecipeId int PRIMARY KEY,RecipeCode nvarchar(50) UNIQUE,Name nvarchar(200),
 YieldPercentage decimal(18,2),Active bit,Status nvarchar(20),EffectiveDate datetime2,
 DrinkId int NULL,SizeId int NULL,ToppingId int NULL,PreparedItemId int NULL,
 OutputQuantity decimal(18,5) NULL,OutputUnitId int NULL);
 INSERT @RecipeSeed VALUES
(7,N'DEMO_RECIPE_PREP_VIET_COFFEE',N'BOM Cốt cà phê Việt',100,1,N'Active','2026-01-01',NULL,NULL,NULL,1,1000,3),
(8,N'DEMO_RECIPE_PREP_ESPRESSO',N'BOM Espresso shot',100,1,N'Active','2026-01-01',NULL,NULL,NULL,2,600,3),
(9,N'DEMO_RECIPE_PREP_BLACK_TEA',N'BOM Cốt trà đen',100,1,N'Active','2026-01-01',NULL,NULL,NULL,3,2000,3),
(10,N'DEMO_RECIPE_PREP_OOLONG_TEA',N'BOM Cốt trà ô long',100,1,N'Active','2026-01-01',NULL,NULL,NULL,4,2000,3),
(11,N'DEMO_RECIPE_PREP_SUGAR_SYRUP',N'BOM Syrup đường',100,1,N'Active','2026-01-01',NULL,NULL,NULL,5,1500,3),
(12,N'DEMO_RECIPE_PREP_SALTED_CREAM',N'BOM Kem muối',100,1,N'Active','2026-01-01',NULL,NULL,NULL,6,1000,3),
(13,N'DEMO_RECIPE_PREP_CHEESE_CREAM',N'BOM Kem cheese',100,1,N'Active','2026-01-01',NULL,NULL,NULL,7,1000,3),
(14,N'DEMO_RECIPE_PREP_BLACK_PEARL',N'BOM Trân châu đen đã nấu',100,1,N'Active','2026-01-01',NULL,NULL,NULL,8,40,13),
(15,N'DEMO_RECIPE_TOP_BLACK_PEARL',N'BOM Trân châu đen nấu mới',100,0,N'Archived','2026-01-01',NULL,NULL,1,NULL,NULL,NULL),
(16,N'DEMO_RECIPE_TOP_WHITE_PEARL',N'BOM Trân châu trắng nấu mới',100,0,N'Archived','2026-01-01',NULL,NULL,2,NULL,NULL,NULL),
(17,N'DEMO_RECIPE_TOP_FLAN',N'BOM Bánh flan caramel',100,1,N'Active','2026-01-01',NULL,NULL,6,NULL,NULL,NULL),
(18,N'DEMO_RECIPE_TOP_TARO_JELLY',N'BOM Thạch khoai môn dẻo',100,1,N'Active','2026-01-01',NULL,NULL,5,NULL,NULL,NULL),
(19,N'DEMO_RECIPE_TOP_CHEESE_CREAM',N'BOM Kem cheese',100,1,N'Active','2026-01-01',NULL,NULL,7,NULL,NULL,NULL),
(20,N'DEMO_RECIPE_TOP_ESPRESSO_SHOT',N'BOM Shot espresso',100,1,N'Active','2026-01-01',NULL,NULL,13,NULL,NULL,NULL),
(21,N'DEMO_RECIPE_SKU_VIET_BLACK_M',N'BOM Cà phê đen đá M',100,1,N'Active','2026-01-01',31,2,NULL,NULL,NULL,NULL),
(22,N'DEMO_RECIPE_SKU_VIET_BLACK_L',N'BOM Cà phê đen đá L',100,1,N'Active','2026-01-01',31,3,NULL,NULL,NULL,NULL),
(23,N'DEMO_RECIPE_SKU_VIET_MILK_M',N'BOM Cà phê sữa đá M',100,1,N'Active','2026-01-01',32,2,NULL,NULL,NULL,NULL),
(24,N'DEMO_RECIPE_SKU_VIET_MILK_L',N'BOM Cà phê sữa đá L',100,1,N'Active','2026-01-01',32,3,NULL,NULL,NULL,NULL),
(25,N'DEMO_RECIPE_SKU_BAC_XIU_M',N'BOM Bạc xỉu M',100,1,N'Active','2026-01-01',7,2,NULL,NULL,NULL,NULL),
(26,N'DEMO_RECIPE_SKU_BAC_XIU_L',N'BOM Bạc xỉu L',100,1,N'Active','2026-01-01',7,3,NULL,NULL,NULL,NULL),
(27,N'DEMO_RECIPE_SKU_SALTED_COFFEE_M',N'BOM Cà phê muối M',100,1,N'Active','2026-01-01',33,2,NULL,NULL,NULL,NULL),
(28,N'DEMO_RECIPE_SKU_SALTED_COFFEE_L',N'BOM Cà phê muối L',100,1,N'Active','2026-01-01',33,3,NULL,NULL,NULL,NULL),
(29,N'DEMO_RECIPE_SKU_AMERICANO_M',N'BOM Americano M',100,1,N'Active','2026-01-01',10,2,NULL,NULL,NULL,NULL),
(30,N'DEMO_RECIPE_SKU_AMERICANO_L',N'BOM Americano L',100,1,N'Active','2026-01-01',10,3,NULL,NULL,NULL,NULL),
(31,N'DEMO_RECIPE_SKU_COFFEE_LATTE_M',N'BOM Latte cà phê M',100,1,N'Active','2026-01-01',34,2,NULL,NULL,NULL,NULL),
(32,N'DEMO_RECIPE_SKU_COFFEE_LATTE_L',N'BOM Latte cà phê L',100,1,N'Active','2026-01-01',34,3,NULL,NULL,NULL,NULL),
(33,N'DEMO_RECIPE_SKU_PEACH_ORANGE_TEA_M',N'BOM Trà đào cam sả M',100,1,N'Active','2026-01-01',21,2,NULL,NULL,NULL,NULL),
(34,N'DEMO_RECIPE_SKU_PEACH_ORANGE_TEA_L',N'BOM Trà đào cam sả L',100,1,N'Active','2026-01-01',21,3,NULL,NULL,NULL,NULL),
(35,N'DEMO_RECIPE_SKU_LYCHEE_TEA_M',N'BOM Trà vải M',100,1,N'Active','2026-01-01',22,2,NULL,NULL,NULL,NULL),
(36,N'DEMO_RECIPE_SKU_LYCHEE_TEA_L',N'BOM Trà vải L',100,1,N'Active','2026-01-01',22,3,NULL,NULL,NULL,NULL),
(37,N'DEMO_RECIPE_SKU_PASSION_TEA_M',N'BOM Trà chanh dây M',100,1,N'Active','2026-01-01',35,2,NULL,NULL,NULL,NULL),
(38,N'DEMO_RECIPE_SKU_PASSION_TEA_L',N'BOM Trà chanh dây L',100,1,N'Active','2026-01-01',35,3,NULL,NULL,NULL,NULL),
(39,N'DEMO_RECIPE_SKU_TRAD_MILK_TEA_M',N'BOM Trà sữa truyền thống đặc biệt M',100,1,N'Active','2026-01-01',36,2,NULL,NULL,NULL,NULL),
(40,N'DEMO_RECIPE_SKU_TRAD_MILK_TEA_L',N'BOM Trà sữa truyền thống đặc biệt L',100,1,N'Active','2026-01-01',36,3,NULL,NULL,NULL,NULL),
(41,N'DEMO_RECIPE_SKU_OOLONG_MILK_TEA_M',N'BOM Trà sữa ô long M',100,1,N'Active','2026-01-01',14,2,NULL,NULL,NULL,NULL),
(42,N'DEMO_RECIPE_SKU_OOLONG_MILK_TEA_L',N'BOM Trà sữa ô long L',100,1,N'Active','2026-01-01',14,3,NULL,NULL,NULL,NULL),
(43,N'DEMO_RECIPE_SKU_MATCHA_LATTE_M',N'BOM Matcha latte M',100,1,N'Active','2026-01-01',37,2,NULL,NULL,NULL,NULL),
(44,N'DEMO_RECIPE_SKU_MATCHA_LATTE_L',N'BOM Matcha latte L',100,1,N'Active','2026-01-01',37,3,NULL,NULL,NULL,NULL),
(45,N'DEMO_RECIPE_SKU_CHOCOLATE_LATTE_M',N'BOM Chocolate latte M',100,1,N'Active','2026-01-01',38,2,NULL,NULL,NULL,NULL),
(46,N'DEMO_RECIPE_SKU_CHOCOLATE_LATTE_L',N'BOM Chocolate latte L',100,1,N'Active','2026-01-01',38,3,NULL,NULL,NULL,NULL),
(47,N'DEMO_RECIPE_SKU_MATCHA_FRAPPE_M',N'BOM Matcha đá xay M',100,1,N'Active','2026-01-01',39,2,NULL,NULL,NULL,NULL),
(48,N'DEMO_RECIPE_SKU_MATCHA_FRAPPE_L',N'BOM Matcha đá xay L',100,1,N'Active','2026-01-01',39,3,NULL,NULL,NULL,NULL),
(49,N'DEMO_RECIPE_SKU_COLD_BREW_ORANGE_M',N'BOM Cold brew cam M',100,1,N'Active','2026-01-01',40,2,NULL,NULL,NULL,NULL),
(50,N'DEMO_RECIPE_SKU_COLD_BREW_ORANGE_L',N'BOM Cold brew cam L',100,1,N'Active','2026-01-01',40,3,NULL,NULL,NULL,NULL),
(51,N'DEMO_RECIPE_SKU_MOCHA_M',N'BOM Mocha M',100,1,N'Active','2026-01-01',41,2,NULL,NULL,NULL,NULL),
(52,N'DEMO_RECIPE_SKU_MOCHA_L',N'BOM Mocha L',100,1,N'Active','2026-01-01',41,3,NULL,NULL,NULL,NULL),
(53,N'DEMO_RECIPE_SKU_CARAMEL_MACCHIATO_M',N'BOM Caramel macchiato M',100,1,N'Active','2026-01-01',42,2,NULL,NULL,NULL,NULL),
(54,N'DEMO_RECIPE_SKU_CARAMEL_MACCHIATO_L',N'BOM Caramel macchiato L',100,1,N'Active','2026-01-01',42,3,NULL,NULL,NULL,NULL),
(55,N'DEMO_RECIPE_SKU_COCONUT_COFFEE_M',N'BOM Cà phê dừa M',100,1,N'Active','2026-01-01',43,2,NULL,NULL,NULL,NULL),
(56,N'DEMO_RECIPE_SKU_COCONUT_COFFEE_L',N'BOM Cà phê dừa L',100,1,N'Active','2026-01-01',43,3,NULL,NULL,NULL,NULL),
(57,N'DEMO_RECIPE_SKU_HONEY_LEMON_TEA_M',N'BOM Trà chanh mật ong M',100,1,N'Active','2026-01-01',44,2,NULL,NULL,NULL,NULL),
(58,N'DEMO_RECIPE_SKU_HONEY_LEMON_TEA_L',N'BOM Trà chanh mật ong L',100,1,N'Active','2026-01-01',44,3,NULL,NULL,NULL,NULL),
(59,N'DEMO_RECIPE_SKU_MANGO_TEA_M',N'BOM Trà xoài M',100,1,N'Active','2026-01-01',45,2,NULL,NULL,NULL,NULL),
(60,N'DEMO_RECIPE_SKU_MANGO_TEA_L',N'BOM Trà xoài L',100,1,N'Active','2026-01-01',45,3,NULL,NULL,NULL,NULL),
(61,N'DEMO_RECIPE_SKU_STRAWBERRY_MILK_TEA_M',N'BOM Trà sữa dâu M',100,1,N'Active','2026-01-01',46,2,NULL,NULL,NULL,NULL),
(62,N'DEMO_RECIPE_SKU_STRAWBERRY_MILK_TEA_L',N'BOM Trà sữa dâu L',100,1,N'Active','2026-01-01',46,3,NULL,NULL,NULL,NULL),
(63,N'DEMO_RECIPE_SKU_LYCHEE_OOLONG_M',N'BOM Trà ô long vải M',100,1,N'Active','2026-01-01',47,2,NULL,NULL,NULL,NULL),
(64,N'DEMO_RECIPE_SKU_LYCHEE_OOLONG_L',N'BOM Trà ô long vải L',100,1,N'Active','2026-01-01',47,3,NULL,NULL,NULL,NULL),
(65,N'DEMO_RECIPE_SKU_OAT_MATCHA_M',N'BOM Matcha sữa yến mạch M',100,1,N'Active','2026-01-01',48,2,NULL,NULL,NULL,NULL),
(66,N'DEMO_RECIPE_SKU_OAT_MATCHA_L',N'BOM Matcha sữa yến mạch L',100,1,N'Active','2026-01-01',48,3,NULL,NULL,NULL,NULL),
(67,N'DEMO_RECIPE_SKU_COCONUT_CHOCOLATE_M',N'BOM Chocolate dừa M',100,1,N'Active','2026-01-01',49,2,NULL,NULL,NULL,NULL),
(68,N'DEMO_RECIPE_SKU_COCONUT_CHOCOLATE_L',N'BOM Chocolate dừa L',100,1,N'Active','2026-01-01',49,3,NULL,NULL,NULL,NULL),
(69,N'DEMO_RECIPE_SKU_PASSION_YOGURT_M',N'BOM Sữa chua chanh dây M',100,1,N'Active','2026-01-01',50,2,NULL,NULL,NULL,NULL),
(70,N'DEMO_RECIPE_SKU_PASSION_YOGURT_L',N'BOM Sữa chua chanh dây L',100,1,N'Active','2026-01-01',50,3,NULL,NULL,NULL,NULL),
(71,N'DEMO_RECIPE_TOP_PM_VIEN',N'BOM Phô mai viên',100,1,N'Active','2026-01-01',NULL,NULL,3,NULL,NULL,NULL),
(72,N'DEMO_RECIPE_TOP_KB_CM',N'BOM Khúc bạch',100,1,N'Active','2026-01-01',NULL,NULL,4,NULL,NULL,NULL),
(73,N'DEMO_RECIPE_TOP_TH_Dao',N'BOM Thạch đào',100,1,N'Active','2026-01-01',NULL,NULL,8,NULL,NULL,NULL),
(74,N'DEMO_RECIPE_TOP_NHADAM',N'BOM Nha đam',100,1,N'Active','2026-01-01',NULL,NULL,9,NULL,NULL,NULL),
(75,N'DEMO_RECIPE_TOP_HATCHIA',N'BOM Hạt chia',100,1,N'Active','2026-01-01',NULL,NULL,10,NULL,NULL,NULL),
(76,N'DEMO_RECIPE_TOP_TH_Dua',N'BOM Thạch dừa',100,1,N'Active','2026-01-01',NULL,NULL,11,NULL,NULL,NULL),
(77,N'DEMO_RECIPE_TOP_PUDDINGTRUNG',N'BOM Pudding trứng',100,1,N'Active','2026-01-01',NULL,NULL,12,NULL,NULL,NULL),
(78,N'DEMO_RECIPE_TOP_TC_HOANGKIM',N'BOM Trân châu hoàng kim',100,1,N'Active','2026-01-01',NULL,NULL,14,NULL,NULL,NULL),
(79,N'DEMO_RECIPE_TOP_TC_DUONGDEN',N'BOM Trân châu đường đen',100,1,N'Active','2026-01-01',NULL,NULL,15,NULL,NULL,NULL),
(80,N'DEMO_RECIPE_TOP_TC_MINI',N'BOM Trân châu mini',100,1,N'Active','2026-01-01',NULL,NULL,16,NULL,NULL,NULL),
(81,N'DEMO_RECIPE_TOP_TC_KHOAIMON',N'BOM Trân châu khoai môn',100,1,N'Active','2026-01-01',NULL,NULL,17,NULL,NULL,NULL),
(82,N'DEMO_RECIPE_TOP_TH_CAFE',N'BOM Thạch cà phê',100,1,N'Active','2026-01-01',NULL,NULL,18,NULL,NULL,NULL),
(83,N'DEMO_RECIPE_TOP_TH_MATCHA',N'BOM Thạch matcha',100,1,N'Active','2026-01-01',NULL,NULL,19,NULL,NULL,NULL),
(84,N'DEMO_RECIPE_TOP_TH_VAI',N'BOM Thạch vải',100,1,N'Active','2026-01-01',NULL,NULL,20,NULL,NULL,NULL),
(85,N'DEMO_RECIPE_TOP_TH_XOAI',N'BOM Thạch xoài',100,1,N'Active','2026-01-01',NULL,NULL,21,NULL,NULL,NULL),
(86,N'DEMO_RECIPE_TOP_TH_DAU',N'BOM Thạch dâu',100,1,N'Active','2026-01-01',NULL,NULL,22,NULL,NULL,NULL),
(87,N'DEMO_RECIPE_TOP_TH_CHANHDAY',N'BOM Thạch chanh dây',100,1,N'Active','2026-01-01',NULL,NULL,23,NULL,NULL,NULL),
(88,N'DEMO_RECIPE_TOP_TH_MATONGCHANH',N'BOM Thạch mật ong chanh',100,1,N'Active','2026-01-01',NULL,NULL,24,NULL,NULL,NULL),
(89,N'DEMO_RECIPE_TOP_TH_SUAYENMACH',N'BOM Thạch sữa yến mạch',100,1,N'Active','2026-01-01',NULL,NULL,25,NULL,NULL,NULL),
(90,N'DEMO_RECIPE_TOP_TRAIDAO',N'BOM Đào miếng',100,1,N'Active','2026-01-01',NULL,NULL,26,NULL,NULL,NULL),
(91,N'DEMO_RECIPE_TOP_TRAIVAI',N'BOM Vải ngâm',100,1,N'Active','2026-01-01',NULL,NULL,27,NULL,NULL,NULL),
(92,N'DEMO_RECIPE_TOP_XOAI_HAT',N'BOM Xoài cắt hạt lựu',100,1,N'Active','2026-01-01',NULL,NULL,28,NULL,NULL,NULL),
(93,N'DEMO_RECIPE_TOP_DAU_TUOI',N'BOM Dâu tươi',100,1,N'Active','2026-01-01',NULL,NULL,29,NULL,NULL,NULL),
(94,N'DEMO_RECIPE_TOP_TEP_CAM',N'BOM Tép cam',100,1,N'Active','2026-01-01',NULL,NULL,30,NULL,NULL,NULL),
(95,N'DEMO_RECIPE_TOP_CHANHDAY_HAT',N'BOM Chanh dây hạt',100,1,N'Active','2026-01-01',NULL,NULL,31,NULL,NULL,NULL),
(96,N'DEMO_RECIPE_TOP_PUDDING_VANILLA',N'BOM Pudding vanilla',100,1,N'Active','2026-01-01',NULL,NULL,32,NULL,NULL,NULL),
(97,N'DEMO_RECIPE_TOP_PUDDING_SOCOLA',N'BOM Pudding chocolate',100,1,N'Active','2026-01-01',NULL,NULL,33,NULL,NULL,NULL),
(98,N'DEMO_RECIPE_TOP_PUDDING_MATCHA',N'BOM Pudding matcha',100,1,N'Active','2026-01-01',NULL,NULL,34,NULL,NULL,NULL),
(99,N'DEMO_RECIPE_TOP_PUDDING_KHOAIMON',N'BOM Pudding khoai môn',100,1,N'Active','2026-01-01',NULL,NULL,35,NULL,NULL,NULL),
(100,N'DEMO_RECIPE_TOP_KEMMUOI',N'BOM Kem muối',100,1,N'Active','2026-01-01',NULL,NULL,36,NULL,NULL,NULL),
(101,N'DEMO_RECIPE_TOP_KEMSUATUOI',N'BOM Kem sữa tươi',100,1,N'Active','2026-01-01',NULL,NULL,37,NULL,NULL,NULL),
(102,N'DEMO_RECIPE_TOP_KEMDUA',N'BOM Kem dừa',100,1,N'Active','2026-01-01',NULL,NULL,38,NULL,NULL,NULL),
(103,N'DEMO_RECIPE_TOP_KEMYENMACH',N'BOM Kem yến mạch',100,1,N'Active','2026-01-01',NULL,NULL,39,NULL,NULL,NULL),
(104,N'DEMO_RECIPE_TOP_SOT_CARAMEL',N'BOM Sốt caramel',100,1,N'Active','2026-01-01',NULL,NULL,40,NULL,NULL,NULL),
(105,N'DEMO_RECIPE_TOP_SOT_SOCOLA',N'BOM Sốt chocolate',100,1,N'Active','2026-01-01',NULL,NULL,41,NULL,NULL,NULL),
(106,N'DEMO_RECIPE_TOP_SOT_DAU',N'BOM Sốt dâu',100,1,N'Active','2026-01-01',NULL,NULL,42,NULL,NULL,NULL),
(107,N'DEMO_RECIPE_TOP_SOT_XOAI',N'BOM Sốt xoài',100,1,N'Active','2026-01-01',NULL,NULL,43,NULL,NULL,NULL),
(108,N'DEMO_RECIPE_TOP_SOT_MATONG',N'BOM Sốt mật ong',100,1,N'Active','2026-01-01',NULL,NULL,44,NULL,NULL,NULL),
(109,N'DEMO_RECIPE_TOP_SOT_DUONGDEN',N'BOM Sốt đường đen',100,1,N'Active','2026-01-01',NULL,NULL,45,NULL,NULL,NULL),
(110,N'DEMO_RECIPE_TOP_SHOT_MATCHA',N'BOM Shot matcha',100,1,N'Active','2026-01-01',NULL,NULL,46,NULL,NULL,NULL),
(111,N'DEMO_RECIPE_TOP_SUA_YENMACH_THEM',N'BOM Sữa yến mạch thêm',100,1,N'Active','2026-01-01',NULL,NULL,47,NULL,NULL,NULL),
(112,N'DEMO_RECIPE_TOP_COT_DUA_THEM',N'BOM Nước cốt dừa thêm',100,1,N'Active','2026-01-01',NULL,NULL,48,NULL,NULL,NULL),
(113,N'DEMO_RECIPE_TOP_SUA_CHUA_THEM',N'BOM Sữa chua thêm',100,1,N'Active','2026-01-01',NULL,NULL,49,NULL,NULL,NULL),
(114,N'DEMO_RECIPE_TOP_SYRUP_CARAMEL_THEM',N'BOM Syrup caramel thêm',100,1,N'Active','2026-01-01',NULL,NULL,50,NULL,NULL,NULL),
(115,N'ZZ_RCP_CHEESE_CREAM_COFFEE_M',N'BOM Cà phê kem cheese M',100,1,N'Active','2026-01-01',51,2,NULL,NULL,NULL,NULL),
(116,N'ZZ_RCP_HONEY_LEMON_COLD_BREW_M',N'BOM Cold brew mật ong chanh vàng M',100,1,N'Active','2026-01-01',52,2,NULL,NULL,NULL,NULL),
(117,N'ZZ_RCP_BLACK_PEARL_MILK_COFFEE_M',N'BOM Cà phê sữa trân châu đen M',100,1,N'Active','2026-01-01',53,2,NULL,NULL,NULL,NULL),
(118,N'ZZ_RCP_HONEY_OAT_ESPRESSO_M',N'BOM Espresso mật ong yến mạch M',100,1,N'Active','2026-01-01',54,2,NULL,NULL,NULL,NULL),
(119,N'ZZ_RCP_FLAN_MILK_COFFEE_M',N'BOM Cà phê sữa flan M',100,1,N'Active','2026-01-01',55,2,NULL,NULL,NULL,NULL),
(120,N'ZZ_RCP_LYCHEE_ALOE_COLD_BREW_M',N'BOM Cold brew vải nha đam M',100,1,N'Active','2026-01-01',56,2,NULL,NULL,NULL,NULL),
(121,N'ZZ_RCP_SALTED_COCONUT_ESPRESSO_M',N'BOM Espresso dừa kem muối M',100,1,N'Active','2026-01-01',57,2,NULL,NULL,NULL,NULL),
(122,N'ZZ_RCP_BROWN_SUGAR_COCONUT_COFFEE_M',N'BOM Cà phê đường đen thạch dừa M',100,1,N'Active','2026-01-01',58,2,NULL,NULL,NULL,NULL),
(123,N'ZZ_RCP_KHUC_BACH_MILK_COFFEE_M',N'BOM Cà phê sữa khúc bạch M',100,1,N'Active','2026-01-01',59,2,NULL,NULL,NULL,NULL),
(124,N'ZZ_RCP_MANGO_PASSION_COLD_BREW_M',N'BOM Cold brew xoài chanh dây M',100,1,N'Active','2026-01-01',60,2,NULL,NULL,NULL,NULL),

(125,N'ZZ_RCP_PEACH_ALOE_OOLONG_M',N'BOM Ô long đào nha đam M',100,1,N'Active','2026-01-01',61,2,NULL,NULL,NULL,NULL),
(126,N'ZZ_RCP_LYCHEE_CHIA_BLACK_TEA_M',N'BOM Hồng trà vải hạt chia M',100,1,N'Active','2026-01-01',62,2,NULL,NULL,NULL,NULL),
(127,N'ZZ_RCP_MANGO_COCONUT_OOLONG_M',N'BOM Ô long xoài thạch dừa M',100,1,N'Active','2026-01-01',63,2,NULL,NULL,NULL,NULL),
(128,N'ZZ_RCP_ORANGE_ALOE_BLACK_TEA_M',N'BOM Hồng trà cam nha đam M',100,1,N'Active','2026-01-01',64,2,NULL,NULL,NULL,NULL),
(129,N'ZZ_RCP_PASSION_CHIA_TEA_M',N'BOM Trà chanh dây hạt chia M',100,1,N'Active','2026-01-01',65,2,NULL,NULL,NULL,NULL),
(130,N'ZZ_RCP_STRAWBERRY_COCONUT_OOLONG_M',N'BOM Ô long dâu thạch dừa M',100,1,N'Active','2026-01-01',66,2,NULL,NULL,NULL,NULL),
(131,N'ZZ_RCP_PEACH_KHUC_BACH_TEA_M',N'BOM Trà đào khúc bạch M',100,1,N'Active','2026-01-01',67,2,NULL,NULL,NULL,NULL),
(132,N'ZZ_RCP_LYCHEE_ALOE_TEA_M',N'BOM Trà vải nha đam M',100,1,N'Active','2026-01-01',68,2,NULL,NULL,NULL,NULL),
(133,N'ZZ_RCP_MANGO_CHIA_TEA_M',N'BOM Trà xoài hạt chia M',100,1,N'Active','2026-01-01',69,2,NULL,NULL,NULL,NULL),
(134,N'ZZ_RCP_ORANGE_PASSION_TEA_M',N'BOM Trà cam chanh dây M',100,1,N'Active','2026-01-01',70,2,NULL,NULL,NULL,NULL),

(135,N'ZZ_RCP_BROWN_SUGAR_PEARL_MILK_TEA_M',N'BOM Trà sữa đường đen trân châu M',100,1,N'Active','2026-01-01',71,2,NULL,NULL,NULL,NULL),
(136,N'ZZ_RCP_FLAN_MILK_TEA_M',N'BOM Trà sữa flan M',100,1,N'Active','2026-01-01',72,2,NULL,NULL,NULL,NULL),
(137,N'ZZ_RCP_KHUC_BACH_MILK_TEA_M',N'BOM Trà sữa khúc bạch M',100,1,N'Active','2026-01-01',73,2,NULL,NULL,NULL,NULL),
(138,N'ZZ_RCP_ALOE_MILK_TEA_M',N'BOM Trà sữa nha đam M',100,1,N'Active','2026-01-01',74,2,NULL,NULL,NULL,NULL),
(139,N'ZZ_RCP_COCONUT_JELLY_MILK_TEA_M',N'BOM Trà sữa thạch dừa M',100,1,N'Active','2026-01-01',75,2,NULL,NULL,NULL,NULL),
(140,N'ZZ_RCP_CHEESE_CREAM_MILK_TEA_M',N'BOM Trà sữa kem cheese M',100,1,N'Active','2026-01-01',76,2,NULL,NULL,NULL,NULL),

(141,N'ZZ_RCP_STRAWBERRY_CHEESE_MATCHA_M',N'BOM Matcha dâu kem cheese M',100,1,N'Active','2026-01-01',77,2,NULL,NULL,NULL,NULL),
(142,N'ZZ_RCP_MANGO_COCONUT_MATCHA_M',N'BOM Matcha xoài thạch dừa M',100,1,N'Active','2026-01-01',78,2,NULL,NULL,NULL,NULL),
(143,N'ZZ_RCP_SALTED_CARAMEL_CHOCOLATE_M',N'BOM Chocolate caramel kem muối M',100,1,N'Active','2026-01-01',79,2,NULL,NULL,NULL,NULL),
(144,N'ZZ_RCP_MANGO_ALOE_YOGURT_M',N'BOM Sua chua xoai nha dam M',100,1,N'Active','2026-01-01',80,2,NULL,NULL,NULL,NULL),
(145,N'DEMO_RECIPE_PREP_ALOE_BASE',N'BOM Aloe vera base',100,1,N'Active','2026-01-01',NULL,NULL,NULL,9,1000,1),
(146,N'DEMO_RECIPE_PREP_COCONUT_JELLY_BASE',N'BOM Coconut jelly base',100,1,N'Active','2026-01-01',NULL,NULL,NULL,10,1000,1),
(147,N'DEMO_RECIPE_PREP_KHUC_BACH_BASE',N'BOM Khuc bach base',100,1,N'Active','2026-01-01',NULL,NULL,NULL,11,1000,1),
(148,N'DEMO_RECIPE_PREP_LEGACY_CREAM',N'BOM Legacy cream',100,0,N'Archived','2026-01-01',NULL,NULL,NULL,12,1000,3);

 IF (SELECT COUNT(*) FROM @RecipeSeed)<>142 THROW 52304,N'Batch 04 phải có 142 Recipe mới.',1;

 IF EXISTS(SELECT 1 FROM @RecipeSeed x
 LEFT JOIN dbo.Drinks d ON d.DrinkId=x.DrinkId LEFT JOIN dbo.Sizes z ON z.SizeId=x.SizeId
 LEFT JOIN dbo.Toppings t ON t.ToppingId=x.ToppingId LEFT JOIN dbo.PreparedItems p ON p.PreparedItemId=x.PreparedItemId
 LEFT JOIN dbo.Units u ON u.UnitId=x.OutputUnitId
 WHERE(x.DrinkId IS NOT NULL AND d.DrinkId IS NULL) OR(x.SizeId IS NOT NULL AND z.SizeId IS NULL)
 OR(x.ToppingId IS NOT NULL AND t.ToppingId IS NULL) OR(x.PreparedItemId IS NOT NULL AND p.PreparedItemId IS NULL)
 OR(x.OutputUnitId IS NOT NULL AND u.UnitId IS NULL)
 OR NOT((x.PreparedItemId IS NULL AND x.OutputQuantity IS NULL AND x.OutputUnitId IS NULL)
     OR(x.PreparedItemId IS NOT NULL AND x.OutputQuantity>0 AND x.OutputUnitId IS NOT NULL)))
  THROW 52305,N'Recipe seed có FK hoặc output contract không hợp lệ.',1;

 IF EXISTS(SELECT 1 FROM @RecipeSeed x JOIN dbo.Recipes r ON r.RecipeId=x.RecipeId OR r.RecipeCode=x.RecipeCode
 WHERE r.RecipeId<>x.RecipeId OR r.RecipeCode<>x.RecipeCode OR r.Name<>x.Name
 OR r.YieldPercentage<>x.YieldPercentage OR r.Active<>x.Active OR r.Status<>x.Status
 OR ISNULL(r.EffectiveDate,'1900-01-01')<>ISNULL(x.EffectiveDate,'1900-01-01') OR r.ParentVersionId IS NOT NULL
 OR ISNULL(r.DrinkId,-1)<>ISNULL(x.DrinkId,-1) OR ISNULL(r.SizeId,-1)<>ISNULL(x.SizeId,-1)
 OR ISNULL(r.ToppingId,-1)<>ISNULL(x.ToppingId,-1) OR ISNULL(r.PreparedItemId,-1)<>ISNULL(x.PreparedItemId,-1)
 OR ISNULL(r.OutputQuantity,-1)<>ISNULL(x.OutputQuantity,-1) OR ISNULL(r.OutputUnitId,-1)<>ISNULL(x.OutputUnitId,-1))
  THROW 52306,N'Recipes có ID hoặc RecipeCode xung đột.',1;

 IF EXISTS(SELECT 1 FROM @RecipeSeed x JOIN dbo.Recipes r ON x.Active=1 AND x.PreparedItemId IS NOT NULL
  AND r.PreparedItemId=x.PreparedItemId AND r.Active=1 WHERE r.RecipeCode<>x.RecipeCode)
 OR EXISTS(SELECT 1 FROM @RecipeSeed x JOIN dbo.Recipes r ON x.Active=1 AND x.DrinkId IS NOT NULL AND x.SizeId IS NOT NULL
  AND r.DrinkId=x.DrinkId AND r.SizeId=x.SizeId AND r.ToppingId IS NULL AND r.Active=1 AND r.Status=N'Active'
  WHERE r.RecipeCode<>x.RecipeCode)
 OR EXISTS(SELECT 1 FROM @RecipeSeed x JOIN dbo.Recipes r ON x.Active=1 AND x.ToppingId IS NOT NULL
  AND r.ToppingId=x.ToppingId AND r.Active=1 AND r.Status=N'Active' WHERE r.RecipeCode<>x.RecipeCode)
  THROW 52307,N'Mục tiêu Recipe đã có active recipe khác contract.',1;

 SET IDENTITY_INSERT dbo.Recipes ON;
 INSERT dbo.Recipes(RecipeId,RecipeCode,Name,YieldPercentage,Active,Status,EffectiveDate,ParentVersionId,
 DrinkId,SizeId,ToppingId,PreparedItemId,OutputQuantity,OutputUnitId)
 SELECT RecipeId,RecipeCode,Name,YieldPercentage,Active,Status,EffectiveDate,NULL,
 DrinkId,SizeId,ToppingId,PreparedItemId,OutputQuantity,OutputUnitId FROM @RecipeSeed x
 WHERE NOT EXISTS(SELECT 1 FROM dbo.Recipes r WHERE r.RecipeId=x.RecipeId);
 SET IDENTITY_INSERT dbo.Recipes OFF;

 DECLARE @Component TABLE(SortOrder int PRIMARY KEY,RecipeCode nvarchar(50),SourceType nchar(1),
 SourceCode nvarchar(50),Quantity decimal(18,3),UnitCode nvarchar(20));
 INSERT @Component VALUES
(1,N'DEMO_RECIPE_PREP_VIET_COFFEE',N'I',N'DEMO_ING_VIET_COFFEE',250,N'g'),
(2,N'DEMO_RECIPE_PREP_VIET_COFFEE',N'I',N'ING00013',1200,N'ml'),
(3,N'DEMO_RECIPE_PREP_ESPRESSO',N'I',N'DEMO_ING_ESPRESSO_BEAN',300,N'g'),
(4,N'DEMO_RECIPE_PREP_ESPRESSO',N'I',N'ING00013',800,N'ml'),
(5,N'DEMO_RECIPE_PREP_BLACK_TEA',N'I',N'ING00003',80,N'g'),
(6,N'DEMO_RECIPE_PREP_BLACK_TEA',N'I',N'ING00013',2200,N'ml'),
(7,N'DEMO_RECIPE_PREP_OOLONG_TEA',N'I',N'DEMO_ING_OOLONG_TEA',80,N'g'),
(8,N'DEMO_RECIPE_PREP_OOLONG_TEA',N'I',N'ING00013',2200,N'ml'),
(9,N'DEMO_RECIPE_PREP_SUGAR_SYRUP',N'I',N'ING00006',1000,N'g'),
(10,N'DEMO_RECIPE_PREP_SUGAR_SYRUP',N'I',N'ING00013',800,N'ml'),
(11,N'DEMO_RECIPE_PREP_SALTED_CREAM',N'I',N'ING00010',600,N'ml'),
(12,N'DEMO_RECIPE_PREP_SALTED_CREAM',N'I',N'DEMO_ING_FRESH_MILK',350,N'ml'),
(13,N'DEMO_RECIPE_PREP_SALTED_CREAM',N'I',N'DEMO_ING_SALT',8,N'g'),
(14,N'DEMO_RECIPE_PREP_CHEESE_CREAM',N'I',N'DEMO_ING_CHEESE_POWDER',250,N'g'),
(15,N'DEMO_RECIPE_PREP_CHEESE_CREAM',N'I',N'DEMO_ING_FRESH_MILK',500,N'ml'),
(16,N'DEMO_RECIPE_PREP_CHEESE_CREAM',N'I',N'ING00010',250,N'ml'),
(17,N'DEMO_RECIPE_PREP_BLACK_PEARL',N'I',N'DEMO_ING_BLACK_PEARL_DRY',1000,N'g'),
(18,N'DEMO_RECIPE_PREP_BLACK_PEARL',N'P',N'DEMO_PREP_SUGAR_SYRUP',400,N'ml'),
(19,N'DEMO_RECIPE_PREP_BLACK_PEARL',N'I',N'ING00013',3000,N'ml'),
(20,N'DEMO_RECIPE_TOP_BLACK_PEARL',N'P',N'DEMO_PREP_BLACK_PEARL',1,N'DEMO_PORTION'),
(21,N'DEMO_RECIPE_TOP_WHITE_PEARL',N'I',N'DEMO_ING_WHITE_PEARL',1,N'DEMO_PORTION'),
(22,N'DEMO_RECIPE_TOP_FLAN',N'I',N'DEMO_ING_FLAN_POWDER',35,N'g'),
(23,N'DEMO_RECIPE_TOP_TARO_JELLY',N'I',N'DEMO_ING_TARO_JELLY_POWDER',30,N'g'),
(24,N'DEMO_RECIPE_TOP_CHEESE_CREAM',N'P',N'DEMO_PREP_CHEESE_CREAM',35,N'ml'),
(25,N'DEMO_RECIPE_TOP_ESPRESSO_SHOT',N'P',N'DEMO_PREP_ESPRESSO',30,N'ml'),
(26,N'DEMO_RECIPE_SKU_VIET_BLACK_M',N'P',N'DEMO_PREP_VIET_COFFEE',85,N'ml'),
(27,N'DEMO_RECIPE_SKU_VIET_BLACK_M',N'P',N'DEMO_PREP_SUGAR_SYRUP',12,N'ml'),
(28,N'DEMO_RECIPE_SKU_VIET_BLACK_M',N'I',N'ING00007',180,N'g'),
(29,N'DEMO_RECIPE_SKU_VIET_BLACK_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(30,N'DEMO_RECIPE_SKU_VIET_BLACK_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(31,N'DEMO_RECIPE_SKU_VIET_BLACK_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(32,N'DEMO_RECIPE_SKU_VIET_BLACK_M',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(33,N'DEMO_RECIPE_SKU_VIET_BLACK_L',N'P',N'DEMO_PREP_VIET_COFFEE',105,N'ml'),
(34,N'DEMO_RECIPE_SKU_VIET_BLACK_L',N'P',N'DEMO_PREP_SUGAR_SYRUP',16,N'ml'),
(35,N'DEMO_RECIPE_SKU_VIET_BLACK_L',N'I',N'ING00007',230,N'g'),
(36,N'DEMO_RECIPE_SKU_VIET_BLACK_L',N'I',N'DEMO_ING_CUP_L',1,N'pcs'),
(37,N'DEMO_RECIPE_SKU_VIET_BLACK_L',N'I',N'DEMO_ING_LID_L',1,N'pcs'),
(38,N'DEMO_RECIPE_SKU_VIET_BLACK_L',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(39,N'DEMO_RECIPE_SKU_VIET_BLACK_L',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(40,N'DEMO_RECIPE_SKU_VIET_MILK_M',N'P',N'DEMO_PREP_VIET_COFFEE',60,N'ml'),
(41,N'DEMO_RECIPE_SKU_VIET_MILK_M',N'I',N'ING00002',30,N'ml'),
(42,N'DEMO_RECIPE_SKU_VIET_MILK_M',N'I',N'ING00007',180,N'g'),
(43,N'DEMO_RECIPE_SKU_VIET_MILK_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(44,N'DEMO_RECIPE_SKU_VIET_MILK_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(45,N'DEMO_RECIPE_SKU_VIET_MILK_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(46,N'DEMO_RECIPE_SKU_VIET_MILK_M',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(47,N'DEMO_RECIPE_SKU_VIET_MILK_L',N'P',N'DEMO_PREP_VIET_COFFEE',80,N'ml'),
(48,N'DEMO_RECIPE_SKU_VIET_MILK_L',N'I',N'ING00002',40,N'ml'),
(49,N'DEMO_RECIPE_SKU_VIET_MILK_L',N'I',N'ING00007',230,N'g'),
(50,N'DEMO_RECIPE_SKU_VIET_MILK_L',N'I',N'DEMO_ING_CUP_L',1,N'pcs'),
(51,N'DEMO_RECIPE_SKU_VIET_MILK_L',N'I',N'DEMO_ING_LID_L',1,N'pcs'),
(52,N'DEMO_RECIPE_SKU_VIET_MILK_L',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(53,N'DEMO_RECIPE_SKU_VIET_MILK_L',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(54,N'DEMO_RECIPE_SKU_BAC_XIU_M',N'P',N'DEMO_PREP_VIET_COFFEE',35,N'ml'),
(55,N'DEMO_RECIPE_SKU_BAC_XIU_M',N'I',N'ING00002',35,N'ml'),
(56,N'DEMO_RECIPE_SKU_BAC_XIU_M',N'I',N'DEMO_ING_FRESH_MILK',100,N'ml'),
(57,N'DEMO_RECIPE_SKU_BAC_XIU_M',N'I',N'ING00007',170,N'g'),
(58,N'DEMO_RECIPE_SKU_BAC_XIU_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(59,N'DEMO_RECIPE_SKU_BAC_XIU_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(60,N'DEMO_RECIPE_SKU_BAC_XIU_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(61,N'DEMO_RECIPE_SKU_BAC_XIU_M',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(62,N'DEMO_RECIPE_SKU_BAC_XIU_L',N'P',N'DEMO_PREP_VIET_COFFEE',45,N'ml'),
(63,N'DEMO_RECIPE_SKU_BAC_XIU_L',N'I',N'ING00002',45,N'ml'),
(64,N'DEMO_RECIPE_SKU_BAC_XIU_L',N'I',N'DEMO_ING_FRESH_MILK',135,N'ml'),
(65,N'DEMO_RECIPE_SKU_BAC_XIU_L',N'I',N'ING00007',220,N'g'),
(66,N'DEMO_RECIPE_SKU_BAC_XIU_L',N'I',N'DEMO_ING_CUP_L',1,N'pcs'),
(67,N'DEMO_RECIPE_SKU_BAC_XIU_L',N'I',N'DEMO_ING_LID_L',1,N'pcs'),
(68,N'DEMO_RECIPE_SKU_BAC_XIU_L',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(69,N'DEMO_RECIPE_SKU_BAC_XIU_L',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(70,N'DEMO_RECIPE_SKU_SALTED_COFFEE_M',N'P',N'DEMO_PREP_VIET_COFFEE',60,N'ml'),
(71,N'DEMO_RECIPE_SKU_SALTED_COFFEE_M',N'I',N'ING00002',25,N'ml'),
(72,N'DEMO_RECIPE_SKU_SALTED_COFFEE_M',N'P',N'DEMO_PREP_SALTED_CREAM',35,N'ml'),
(73,N'DEMO_RECIPE_SKU_SALTED_COFFEE_M',N'I',N'ING00007',170,N'g'),
(74,N'DEMO_RECIPE_SKU_SALTED_COFFEE_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(75,N'DEMO_RECIPE_SKU_SALTED_COFFEE_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(76,N'DEMO_RECIPE_SKU_SALTED_COFFEE_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(77,N'DEMO_RECIPE_SKU_SALTED_COFFEE_M',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(78,N'DEMO_RECIPE_SKU_SALTED_COFFEE_L',N'P',N'DEMO_PREP_VIET_COFFEE',80,N'ml'),
(79,N'DEMO_RECIPE_SKU_SALTED_COFFEE_L',N'I',N'ING00002',35,N'ml'),
(80,N'DEMO_RECIPE_SKU_SALTED_COFFEE_L',N'P',N'DEMO_PREP_SALTED_CREAM',45,N'ml'),
(81,N'DEMO_RECIPE_SKU_SALTED_COFFEE_L',N'I',N'ING00007',220,N'g'),
(82,N'DEMO_RECIPE_SKU_SALTED_COFFEE_L',N'I',N'DEMO_ING_CUP_L',1,N'pcs'),
(83,N'DEMO_RECIPE_SKU_SALTED_COFFEE_L',N'I',N'DEMO_ING_LID_L',1,N'pcs'),
(84,N'DEMO_RECIPE_SKU_SALTED_COFFEE_L',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(85,N'DEMO_RECIPE_SKU_SALTED_COFFEE_L',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(86,N'DEMO_RECIPE_SKU_AMERICANO_M',N'P',N'DEMO_PREP_ESPRESSO',70,N'ml'),
(87,N'DEMO_RECIPE_SKU_AMERICANO_M',N'I',N'ING00013',120,N'ml'),
(88,N'DEMO_RECIPE_SKU_AMERICANO_M',N'I',N'ING00007',160,N'g'),
(89,N'DEMO_RECIPE_SKU_AMERICANO_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(90,N'DEMO_RECIPE_SKU_AMERICANO_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(91,N'DEMO_RECIPE_SKU_AMERICANO_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(92,N'DEMO_RECIPE_SKU_AMERICANO_M',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(93,N'DEMO_RECIPE_SKU_AMERICANO_L',N'P',N'DEMO_PREP_ESPRESSO',75,N'ml'),
(94,N'DEMO_RECIPE_SKU_AMERICANO_L',N'I',N'ING00013',160,N'ml'),
(95,N'DEMO_RECIPE_SKU_AMERICANO_L',N'I',N'ING00007',210,N'g'),
(96,N'DEMO_RECIPE_SKU_AMERICANO_L',N'I',N'DEMO_ING_CUP_L',1,N'pcs'),
(97,N'DEMO_RECIPE_SKU_AMERICANO_L',N'I',N'DEMO_ING_LID_L',1,N'pcs'),
(98,N'DEMO_RECIPE_SKU_AMERICANO_L',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(99,N'DEMO_RECIPE_SKU_AMERICANO_L',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(100,N'DEMO_RECIPE_SKU_COFFEE_LATTE_M',N'P',N'DEMO_PREP_ESPRESSO',60,N'ml'),
(101,N'DEMO_RECIPE_SKU_COFFEE_LATTE_M',N'I',N'DEMO_ING_FRESH_MILK',160,N'ml'),
(102,N'DEMO_RECIPE_SKU_COFFEE_LATTE_M',N'P',N'DEMO_PREP_SUGAR_SYRUP',10,N'ml'),
(103,N'DEMO_RECIPE_SKU_COFFEE_LATTE_M',N'I',N'ING00007',120,N'g'),
(104,N'DEMO_RECIPE_SKU_COFFEE_LATTE_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(105,N'DEMO_RECIPE_SKU_COFFEE_LATTE_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(106,N'DEMO_RECIPE_SKU_COFFEE_LATTE_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(107,N'DEMO_RECIPE_SKU_COFFEE_LATTE_M',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(108,N'DEMO_RECIPE_SKU_COFFEE_LATTE_L',N'P',N'DEMO_PREP_ESPRESSO',68,N'ml'),
(109,N'DEMO_RECIPE_SKU_COFFEE_LATTE_L',N'I',N'DEMO_ING_FRESH_MILK',210,N'ml'),
(110,N'DEMO_RECIPE_SKU_COFFEE_LATTE_L',N'P',N'DEMO_PREP_SUGAR_SYRUP',14,N'ml'),
(111,N'DEMO_RECIPE_SKU_COFFEE_LATTE_L',N'I',N'ING00007',160,N'g'),
(112,N'DEMO_RECIPE_SKU_COFFEE_LATTE_L',N'I',N'DEMO_ING_CUP_L',1,N'pcs'),
(113,N'DEMO_RECIPE_SKU_COFFEE_LATTE_L',N'I',N'DEMO_ING_LID_L',1,N'pcs'),
(114,N'DEMO_RECIPE_SKU_COFFEE_LATTE_L',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(115,N'DEMO_RECIPE_SKU_COFFEE_LATTE_L',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(116,N'DEMO_RECIPE_SKU_PEACH_ORANGE_TEA_M',N'P',N'DEMO_PREP_BLACK_TEA',150,N'ml'),
(117,N'DEMO_RECIPE_SKU_PEACH_ORANGE_TEA_M',N'I',N'DEMO_ING_CANNED_PEACH',60,N'g'),
(118,N'DEMO_RECIPE_SKU_PEACH_ORANGE_TEA_M',N'I',N'DEMO_ING_ORANGE',30,N'g'),
(119,N'DEMO_RECIPE_SKU_PEACH_ORANGE_TEA_M',N'I',N'DEMO_ING_LEMONGRASS',6,N'g'),
(120,N'DEMO_RECIPE_SKU_PEACH_ORANGE_TEA_M',N'P',N'DEMO_PREP_SUGAR_SYRUP',20,N'ml'),
(121,N'DEMO_RECIPE_SKU_PEACH_ORANGE_TEA_M',N'I',N'ING00007',150,N'g'),
(122,N'DEMO_RECIPE_SKU_PEACH_ORANGE_TEA_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(123,N'DEMO_RECIPE_SKU_PEACH_ORANGE_TEA_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(124,N'DEMO_RECIPE_SKU_PEACH_ORANGE_TEA_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(125,N'DEMO_RECIPE_SKU_PEACH_ORANGE_TEA_M',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(126,N'DEMO_RECIPE_SKU_PEACH_ORANGE_TEA_L',N'P',N'DEMO_PREP_BLACK_TEA',200,N'ml'),
(127,N'DEMO_RECIPE_SKU_PEACH_ORANGE_TEA_L',N'I',N'DEMO_ING_CANNED_PEACH',80,N'g'),
(128,N'DEMO_RECIPE_SKU_PEACH_ORANGE_TEA_L',N'I',N'DEMO_ING_ORANGE',40,N'g'),
(129,N'DEMO_RECIPE_SKU_PEACH_ORANGE_TEA_L',N'I',N'DEMO_ING_LEMONGRASS',8,N'g'),
(130,N'DEMO_RECIPE_SKU_PEACH_ORANGE_TEA_L',N'P',N'DEMO_PREP_SUGAR_SYRUP',27,N'ml'),
(131,N'DEMO_RECIPE_SKU_PEACH_ORANGE_TEA_L',N'I',N'ING00007',200,N'g'),
(132,N'DEMO_RECIPE_SKU_PEACH_ORANGE_TEA_L',N'I',N'DEMO_ING_CUP_L',1,N'pcs'),
(133,N'DEMO_RECIPE_SKU_PEACH_ORANGE_TEA_L',N'I',N'DEMO_ING_LID_L',1,N'pcs'),
(134,N'DEMO_RECIPE_SKU_PEACH_ORANGE_TEA_L',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(135,N'DEMO_RECIPE_SKU_PEACH_ORANGE_TEA_L',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(136,N'DEMO_RECIPE_SKU_LYCHEE_TEA_M',N'P',N'DEMO_PREP_BLACK_TEA',150,N'ml'),
(137,N'DEMO_RECIPE_SKU_LYCHEE_TEA_M',N'I',N'DEMO_ING_CANNED_LYCHEE',70,N'g'),
(138,N'DEMO_RECIPE_SKU_LYCHEE_TEA_M',N'P',N'DEMO_PREP_SUGAR_SYRUP',18,N'ml'),
(139,N'DEMO_RECIPE_SKU_LYCHEE_TEA_M',N'I',N'ING00007',150,N'g'),
(140,N'DEMO_RECIPE_SKU_LYCHEE_TEA_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(141,N'DEMO_RECIPE_SKU_LYCHEE_TEA_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(142,N'DEMO_RECIPE_SKU_LYCHEE_TEA_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(143,N'DEMO_RECIPE_SKU_LYCHEE_TEA_M',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(144,N'DEMO_RECIPE_SKU_LYCHEE_TEA_L',N'P',N'DEMO_PREP_BLACK_TEA',200,N'ml'),
(145,N'DEMO_RECIPE_SKU_LYCHEE_TEA_L',N'I',N'DEMO_ING_CANNED_LYCHEE',90,N'g'),
(146,N'DEMO_RECIPE_SKU_LYCHEE_TEA_L',N'P',N'DEMO_PREP_SUGAR_SYRUP',24,N'ml'),
(147,N'DEMO_RECIPE_SKU_LYCHEE_TEA_L',N'I',N'ING00007',200,N'g'),
(148,N'DEMO_RECIPE_SKU_LYCHEE_TEA_L',N'I',N'DEMO_ING_CUP_L',1,N'pcs'),
(149,N'DEMO_RECIPE_SKU_LYCHEE_TEA_L',N'I',N'DEMO_ING_LID_L',1,N'pcs'),
(150,N'DEMO_RECIPE_SKU_LYCHEE_TEA_L',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(151,N'DEMO_RECIPE_SKU_LYCHEE_TEA_L',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(152,N'DEMO_RECIPE_SKU_PASSION_TEA_M',N'P',N'DEMO_PREP_BLACK_TEA',150,N'ml'),
(153,N'DEMO_RECIPE_SKU_PASSION_TEA_M',N'I',N'DEMO_ING_PASSION_JAM',66,N'g'),
(154,N'DEMO_RECIPE_SKU_PASSION_TEA_M',N'P',N'DEMO_PREP_SUGAR_SYRUP',10,N'ml'),
(155,N'DEMO_RECIPE_SKU_PASSION_TEA_M',N'I',N'ING00007',150,N'g'),
(156,N'DEMO_RECIPE_SKU_PASSION_TEA_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(157,N'DEMO_RECIPE_SKU_PASSION_TEA_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(158,N'DEMO_RECIPE_SKU_PASSION_TEA_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(159,N'DEMO_RECIPE_SKU_PASSION_TEA_M',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(160,N'DEMO_RECIPE_SKU_PASSION_TEA_L',N'P',N'DEMO_PREP_BLACK_TEA',200,N'ml'),
(161,N'DEMO_RECIPE_SKU_PASSION_TEA_L',N'I',N'DEMO_ING_PASSION_JAM',60,N'g'),
(162,N'DEMO_RECIPE_SKU_PASSION_TEA_L',N'P',N'DEMO_PREP_SUGAR_SYRUP',14,N'ml'),
(163,N'DEMO_RECIPE_SKU_PASSION_TEA_L',N'I',N'ING00007',200,N'g'),
(164,N'DEMO_RECIPE_SKU_PASSION_TEA_L',N'I',N'DEMO_ING_CUP_L',1,N'pcs'),
(165,N'DEMO_RECIPE_SKU_PASSION_TEA_L',N'I',N'DEMO_ING_LID_L',1,N'pcs'),
(166,N'DEMO_RECIPE_SKU_PASSION_TEA_L',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(167,N'DEMO_RECIPE_SKU_PASSION_TEA_L',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(168,N'DEMO_RECIPE_SKU_TRAD_MILK_TEA_M',N'P',N'DEMO_PREP_BLACK_TEA',150,N'ml'),
(169,N'DEMO_RECIPE_SKU_TRAD_MILK_TEA_M',N'I',N'DEMO_ING_FRESH_MILK',90,N'ml'),
(170,N'DEMO_RECIPE_SKU_TRAD_MILK_TEA_M',N'I',N'ING00010',30,N'ml'),
(171,N'DEMO_RECIPE_SKU_TRAD_MILK_TEA_M',N'P',N'DEMO_PREP_SUGAR_SYRUP',20,N'ml'),
(172,N'DEMO_RECIPE_SKU_TRAD_MILK_TEA_M',N'I',N'ING00007',150,N'g'),
(173,N'DEMO_RECIPE_SKU_TRAD_MILK_TEA_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(174,N'DEMO_RECIPE_SKU_TRAD_MILK_TEA_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(175,N'DEMO_RECIPE_SKU_TRAD_MILK_TEA_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(176,N'DEMO_RECIPE_SKU_TRAD_MILK_TEA_M',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(177,N'DEMO_RECIPE_SKU_TRAD_MILK_TEA_L',N'P',N'DEMO_PREP_BLACK_TEA',200,N'ml'),
(178,N'DEMO_RECIPE_SKU_TRAD_MILK_TEA_L',N'I',N'DEMO_ING_FRESH_MILK',120,N'ml'),
(179,N'DEMO_RECIPE_SKU_TRAD_MILK_TEA_L',N'I',N'ING00010',28,N'ml'),
(180,N'DEMO_RECIPE_SKU_TRAD_MILK_TEA_L',N'P',N'DEMO_PREP_SUGAR_SYRUP',27,N'ml'),
(181,N'DEMO_RECIPE_SKU_TRAD_MILK_TEA_L',N'I',N'ING00007',200,N'g'),
(182,N'DEMO_RECIPE_SKU_TRAD_MILK_TEA_L',N'I',N'DEMO_ING_CUP_L',1,N'pcs'),
(183,N'DEMO_RECIPE_SKU_TRAD_MILK_TEA_L',N'I',N'DEMO_ING_LID_L',1,N'pcs'),
(184,N'DEMO_RECIPE_SKU_TRAD_MILK_TEA_L',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(185,N'DEMO_RECIPE_SKU_TRAD_MILK_TEA_L',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(186,N'DEMO_RECIPE_SKU_OOLONG_MILK_TEA_M',N'P',N'DEMO_PREP_OOLONG_TEA',150,N'ml'),
(187,N'DEMO_RECIPE_SKU_OOLONG_MILK_TEA_M',N'I',N'DEMO_ING_FRESH_MILK',90,N'ml'),
(188,N'DEMO_RECIPE_SKU_OOLONG_MILK_TEA_M',N'I',N'ING00010',27,N'ml'),
(189,N'DEMO_RECIPE_SKU_OOLONG_MILK_TEA_M',N'P',N'DEMO_PREP_SUGAR_SYRUP',20,N'ml'),
(190,N'DEMO_RECIPE_SKU_OOLONG_MILK_TEA_M',N'I',N'ING00007',150,N'g'),
(191,N'DEMO_RECIPE_SKU_OOLONG_MILK_TEA_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(192,N'DEMO_RECIPE_SKU_OOLONG_MILK_TEA_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(193,N'DEMO_RECIPE_SKU_OOLONG_MILK_TEA_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(194,N'DEMO_RECIPE_SKU_OOLONG_MILK_TEA_M',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(195,N'DEMO_RECIPE_SKU_OOLONG_MILK_TEA_L',N'P',N'DEMO_PREP_OOLONG_TEA',200,N'ml'),
(196,N'DEMO_RECIPE_SKU_OOLONG_MILK_TEA_L',N'I',N'DEMO_ING_FRESH_MILK',120,N'ml'),
(197,N'DEMO_RECIPE_SKU_OOLONG_MILK_TEA_L',N'I',N'ING00010',30,N'ml'),
(198,N'DEMO_RECIPE_SKU_OOLONG_MILK_TEA_L',N'P',N'DEMO_PREP_SUGAR_SYRUP',27,N'ml'),
(199,N'DEMO_RECIPE_SKU_OOLONG_MILK_TEA_L',N'I',N'ING00007',200,N'g'),
(200,N'DEMO_RECIPE_SKU_OOLONG_MILK_TEA_L',N'I',N'DEMO_ING_CUP_L',1,N'pcs'),
(201,N'DEMO_RECIPE_SKU_OOLONG_MILK_TEA_L',N'I',N'DEMO_ING_LID_L',1,N'pcs'),
(202,N'DEMO_RECIPE_SKU_OOLONG_MILK_TEA_L',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(203,N'DEMO_RECIPE_SKU_OOLONG_MILK_TEA_L',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(204,N'DEMO_RECIPE_SKU_MATCHA_LATTE_M',N'I',N'ING00009',8,N'g'),
(205,N'DEMO_RECIPE_SKU_MATCHA_LATTE_M',N'I',N'DEMO_ING_FRESH_MILK',150,N'ml'),
(206,N'DEMO_RECIPE_SKU_MATCHA_LATTE_M',N'P',N'DEMO_PREP_SUGAR_SYRUP',15,N'ml'),
(207,N'DEMO_RECIPE_SKU_MATCHA_LATTE_M',N'I',N'ING00007',150,N'g'),
(208,N'DEMO_RECIPE_SKU_MATCHA_LATTE_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(209,N'DEMO_RECIPE_SKU_MATCHA_LATTE_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(210,N'DEMO_RECIPE_SKU_MATCHA_LATTE_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(211,N'DEMO_RECIPE_SKU_MATCHA_LATTE_M',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(212,N'DEMO_RECIPE_SKU_MATCHA_LATTE_L',N'I',N'ING00009',11,N'g'),
(213,N'DEMO_RECIPE_SKU_MATCHA_LATTE_L',N'I',N'DEMO_ING_FRESH_MILK',205,N'ml'),
(214,N'DEMO_RECIPE_SKU_MATCHA_LATTE_L',N'P',N'DEMO_PREP_SUGAR_SYRUP',20,N'ml'),
(215,N'DEMO_RECIPE_SKU_MATCHA_LATTE_L',N'I',N'ING00007',200,N'g'),
(216,N'DEMO_RECIPE_SKU_MATCHA_LATTE_L',N'I',N'DEMO_ING_CUP_L',1,N'pcs'),
(217,N'DEMO_RECIPE_SKU_MATCHA_LATTE_L',N'I',N'DEMO_ING_LID_L',1,N'pcs'),
(218,N'DEMO_RECIPE_SKU_MATCHA_LATTE_L',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(219,N'DEMO_RECIPE_SKU_MATCHA_LATTE_L',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(220,N'DEMO_RECIPE_SKU_CHOCOLATE_LATTE_M',N'I',N'DEMO_ING_CHOCOLATE',23,N'g'),
(221,N'DEMO_RECIPE_SKU_CHOCOLATE_LATTE_M',N'I',N'DEMO_ING_FRESH_MILK',150,N'ml'),
(222,N'DEMO_RECIPE_SKU_CHOCOLATE_LATTE_M',N'P',N'DEMO_PREP_SUGAR_SYRUP',12,N'ml'),
(223,N'DEMO_RECIPE_SKU_CHOCOLATE_LATTE_M',N'I',N'ING00007',150,N'g'),
(224,N'DEMO_RECIPE_SKU_CHOCOLATE_LATTE_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(225,N'DEMO_RECIPE_SKU_CHOCOLATE_LATTE_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(226,N'DEMO_RECIPE_SKU_CHOCOLATE_LATTE_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(227,N'DEMO_RECIPE_SKU_CHOCOLATE_LATTE_M',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(228,N'DEMO_RECIPE_SKU_CHOCOLATE_LATTE_L',N'I',N'DEMO_ING_CHOCOLATE',27,N'g'),
(229,N'DEMO_RECIPE_SKU_CHOCOLATE_LATTE_L',N'I',N'DEMO_ING_FRESH_MILK',205,N'ml'),
(230,N'DEMO_RECIPE_SKU_CHOCOLATE_LATTE_L',N'P',N'DEMO_PREP_SUGAR_SYRUP',16,N'ml'),
(231,N'DEMO_RECIPE_SKU_CHOCOLATE_LATTE_L',N'I',N'ING00007',200,N'g'),
(232,N'DEMO_RECIPE_SKU_CHOCOLATE_LATTE_L',N'I',N'DEMO_ING_CUP_L',1,N'pcs'),
(233,N'DEMO_RECIPE_SKU_CHOCOLATE_LATTE_L',N'I',N'DEMO_ING_LID_L',1,N'pcs'),
(234,N'DEMO_RECIPE_SKU_CHOCOLATE_LATTE_L',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(235,N'DEMO_RECIPE_SKU_CHOCOLATE_LATTE_L',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(236,N'DEMO_RECIPE_SKU_MATCHA_FRAPPE_M',N'I',N'ING00009',10,N'g'),
(237,N'DEMO_RECIPE_SKU_MATCHA_FRAPPE_M',N'I',N'DEMO_ING_FRESH_MILK',130,N'ml'),
(238,N'DEMO_RECIPE_SKU_MATCHA_FRAPPE_M',N'I',N'DEMO_ING_FRAPPE',20,N'g'),
(239,N'DEMO_RECIPE_SKU_MATCHA_FRAPPE_M',N'P',N'DEMO_PREP_SUGAR_SYRUP',15,N'ml'),
(240,N'DEMO_RECIPE_SKU_MATCHA_FRAPPE_M',N'I',N'ING00010',20,N'ml'),
(241,N'DEMO_RECIPE_SKU_MATCHA_FRAPPE_M',N'I',N'ING00007',220,N'g'),
(242,N'DEMO_RECIPE_SKU_MATCHA_FRAPPE_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(243,N'DEMO_RECIPE_SKU_MATCHA_FRAPPE_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(244,N'DEMO_RECIPE_SKU_MATCHA_FRAPPE_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(245,N'DEMO_RECIPE_SKU_MATCHA_FRAPPE_M',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(246,N'DEMO_RECIPE_SKU_MATCHA_FRAPPE_L',N'I',N'ING00009',11,N'g'),
(247,N'DEMO_RECIPE_SKU_MATCHA_FRAPPE_L',N'I',N'DEMO_ING_FRESH_MILK',165,N'ml'),
(248,N'DEMO_RECIPE_SKU_MATCHA_FRAPPE_L',N'I',N'DEMO_ING_FRAPPE',22,N'g'),
(249,N'DEMO_RECIPE_SKU_MATCHA_FRAPPE_L',N'P',N'DEMO_PREP_SUGAR_SYRUP',18,N'ml'),
(250,N'DEMO_RECIPE_SKU_MATCHA_FRAPPE_L',N'I',N'ING00010',25,N'ml'),
(251,N'DEMO_RECIPE_SKU_MATCHA_FRAPPE_L',N'I',N'ING00007',270,N'g'),
(252,N'DEMO_RECIPE_SKU_MATCHA_FRAPPE_L',N'I',N'DEMO_ING_CUP_L',1,N'pcs'),
(253,N'DEMO_RECIPE_SKU_MATCHA_FRAPPE_L',N'I',N'DEMO_ING_LID_L',1,N'pcs'),
(254,N'DEMO_RECIPE_SKU_MATCHA_FRAPPE_L',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(255,N'DEMO_RECIPE_SKU_MATCHA_FRAPPE_L',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(256,N'DEMO_RECIPE_SKU_COLD_BREW_ORANGE_M',N'P',N'DEMO_PREP_VIET_COFFEE',90,N'ml'),
(257,N'DEMO_RECIPE_SKU_COLD_BREW_ORANGE_M',N'I',N'DEMO_ING_ORANGE',50,N'g'),
(258,N'DEMO_RECIPE_SKU_COLD_BREW_ORANGE_M',N'P',N'DEMO_PREP_SUGAR_SYRUP',12,N'ml'),
(259,N'DEMO_RECIPE_SKU_COLD_BREW_ORANGE_M',N'I',N'ING00007',160,N'g'),
(260,N'DEMO_RECIPE_SKU_COLD_BREW_ORANGE_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(261,N'DEMO_RECIPE_SKU_COLD_BREW_ORANGE_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(262,N'DEMO_RECIPE_SKU_COLD_BREW_ORANGE_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(263,N'DEMO_RECIPE_SKU_COLD_BREW_ORANGE_M',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(264,N'DEMO_RECIPE_SKU_COLD_BREW_ORANGE_L',N'P',N'DEMO_PREP_VIET_COFFEE',120,N'ml'),
(265,N'DEMO_RECIPE_SKU_COLD_BREW_ORANGE_L',N'I',N'DEMO_ING_ORANGE',70,N'g'),
(266,N'DEMO_RECIPE_SKU_COLD_BREW_ORANGE_L',N'P',N'DEMO_PREP_SUGAR_SYRUP',18,N'ml'),
(267,N'DEMO_RECIPE_SKU_COLD_BREW_ORANGE_L',N'I',N'ING00007',210,N'g'),
(268,N'DEMO_RECIPE_SKU_COLD_BREW_ORANGE_L',N'I',N'DEMO_ING_CUP_L',1,N'pcs'),
(269,N'DEMO_RECIPE_SKU_COLD_BREW_ORANGE_L',N'I',N'DEMO_ING_LID_L',1,N'pcs'),
(270,N'DEMO_RECIPE_SKU_COLD_BREW_ORANGE_L',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(271,N'DEMO_RECIPE_SKU_COLD_BREW_ORANGE_L',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(272,N'DEMO_RECIPE_SKU_MOCHA_M',N'P',N'DEMO_PREP_ESPRESSO',30,N'ml'),
(273,N'DEMO_RECIPE_SKU_MOCHA_M',N'I',N'DEMO_ING_CHOCOLATE',20,N'g'),
(274,N'DEMO_RECIPE_SKU_MOCHA_M',N'I',N'DEMO_ING_FRESH_MILK',150,N'ml'),
(275,N'DEMO_RECIPE_SKU_MOCHA_M',N'P',N'DEMO_PREP_SUGAR_SYRUP',12,N'ml'),
(276,N'DEMO_RECIPE_SKU_MOCHA_M',N'I',N'ING00007',150,N'g'),
(277,N'DEMO_RECIPE_SKU_MOCHA_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(278,N'DEMO_RECIPE_SKU_MOCHA_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(279,N'DEMO_RECIPE_SKU_MOCHA_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(280,N'DEMO_RECIPE_SKU_MOCHA_M',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(281,N'DEMO_RECIPE_SKU_MOCHA_L',N'P',N'DEMO_PREP_ESPRESSO',45,N'ml'),
(282,N'DEMO_RECIPE_SKU_MOCHA_L',N'I',N'DEMO_ING_CHOCOLATE',26,N'g'),
(283,N'DEMO_RECIPE_SKU_MOCHA_L',N'I',N'DEMO_ING_FRESH_MILK',200,N'ml'),
(284,N'DEMO_RECIPE_SKU_MOCHA_L',N'P',N'DEMO_PREP_SUGAR_SYRUP',16,N'ml'),
(285,N'DEMO_RECIPE_SKU_MOCHA_L',N'I',N'ING00007',200,N'g'),
(286,N'DEMO_RECIPE_SKU_MOCHA_L',N'I',N'DEMO_ING_CUP_L',1,N'pcs'),
(287,N'DEMO_RECIPE_SKU_MOCHA_L',N'I',N'DEMO_ING_LID_L',1,N'pcs'),
(288,N'DEMO_RECIPE_SKU_MOCHA_L',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(289,N'DEMO_RECIPE_SKU_MOCHA_L',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(290,N'DEMO_RECIPE_SKU_CARAMEL_MACCHIATO_M',N'P',N'DEMO_PREP_ESPRESSO',30,N'ml'),
(291,N'DEMO_RECIPE_SKU_CARAMEL_MACCHIATO_M',N'I',N'DEMO_ING_FRESH_MILK',160,N'ml'),
(292,N'DEMO_RECIPE_SKU_CARAMEL_MACCHIATO_M',N'I',N'DEMO_ING_CARAMEL_SYRUP',20,N'ml'),
(293,N'DEMO_RECIPE_SKU_CARAMEL_MACCHIATO_M',N'I',N'ING00007',140,N'g'),
(294,N'DEMO_RECIPE_SKU_CARAMEL_MACCHIATO_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(295,N'DEMO_RECIPE_SKU_CARAMEL_MACCHIATO_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(296,N'DEMO_RECIPE_SKU_CARAMEL_MACCHIATO_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(297,N'DEMO_RECIPE_SKU_CARAMEL_MACCHIATO_M',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(298,N'DEMO_RECIPE_SKU_CARAMEL_MACCHIATO_L',N'P',N'DEMO_PREP_ESPRESSO',45,N'ml'),
(299,N'DEMO_RECIPE_SKU_CARAMEL_MACCHIATO_L',N'I',N'DEMO_ING_FRESH_MILK',210,N'ml'),
(300,N'DEMO_RECIPE_SKU_CARAMEL_MACCHIATO_L',N'I',N'DEMO_ING_CARAMEL_SYRUP',28,N'ml'),
(301,N'DEMO_RECIPE_SKU_CARAMEL_MACCHIATO_L',N'I',N'ING00007',190,N'g'),
(302,N'DEMO_RECIPE_SKU_CARAMEL_MACCHIATO_L',N'I',N'DEMO_ING_CUP_L',1,N'pcs'),
(303,N'DEMO_RECIPE_SKU_CARAMEL_MACCHIATO_L',N'I',N'DEMO_ING_LID_L',1,N'pcs'),
(304,N'DEMO_RECIPE_SKU_CARAMEL_MACCHIATO_L',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(305,N'DEMO_RECIPE_SKU_CARAMEL_MACCHIATO_L',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(306,N'DEMO_RECIPE_SKU_COCONUT_COFFEE_M',N'P',N'DEMO_PREP_VIET_COFFEE',60,N'ml'),
(307,N'DEMO_RECIPE_SKU_COCONUT_COFFEE_M',N'I',N'DEMO_ING_COCONUT_MILK',100,N'ml'),
(308,N'DEMO_RECIPE_SKU_COCONUT_COFFEE_M',N'I',N'ING00002',25,N'ml'),
(309,N'DEMO_RECIPE_SKU_COCONUT_COFFEE_M',N'I',N'ING00007',170,N'g'),
(310,N'DEMO_RECIPE_SKU_COCONUT_COFFEE_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(311,N'DEMO_RECIPE_SKU_COCONUT_COFFEE_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(312,N'DEMO_RECIPE_SKU_COCONUT_COFFEE_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(313,N'DEMO_RECIPE_SKU_COCONUT_COFFEE_M',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(314,N'DEMO_RECIPE_SKU_COCONUT_COFFEE_L',N'P',N'DEMO_PREP_VIET_COFFEE',80,N'ml'),
(315,N'DEMO_RECIPE_SKU_COCONUT_COFFEE_L',N'I',N'DEMO_ING_COCONUT_MILK',140,N'ml'),
(316,N'DEMO_RECIPE_SKU_COCONUT_COFFEE_L',N'I',N'ING00002',35,N'ml'),
(317,N'DEMO_RECIPE_SKU_COCONUT_COFFEE_L',N'I',N'ING00007',220,N'g'),
(318,N'DEMO_RECIPE_SKU_COCONUT_COFFEE_L',N'I',N'DEMO_ING_CUP_L',1,N'pcs'),
(319,N'DEMO_RECIPE_SKU_COCONUT_COFFEE_L',N'I',N'DEMO_ING_LID_L',1,N'pcs'),
(320,N'DEMO_RECIPE_SKU_COCONUT_COFFEE_L',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(321,N'DEMO_RECIPE_SKU_COCONUT_COFFEE_L',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(322,N'DEMO_RECIPE_SKU_HONEY_LEMON_TEA_M',N'P',N'DEMO_PREP_BLACK_TEA',150,N'ml'),
(323,N'DEMO_RECIPE_SKU_HONEY_LEMON_TEA_M',N'I',N'DEMO_ING_HONEY',20,N'g'),
(324,N'DEMO_RECIPE_SKU_HONEY_LEMON_TEA_M',N'I',N'DEMO_ING_YELLOW_LEMON',25,N'g'),
(325,N'DEMO_RECIPE_SKU_HONEY_LEMON_TEA_M',N'P',N'DEMO_PREP_SUGAR_SYRUP',10,N'ml'),
(326,N'DEMO_RECIPE_SKU_HONEY_LEMON_TEA_M',N'I',N'ING00007',150,N'g'),
(327,N'DEMO_RECIPE_SKU_HONEY_LEMON_TEA_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(328,N'DEMO_RECIPE_SKU_HONEY_LEMON_TEA_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(329,N'DEMO_RECIPE_SKU_HONEY_LEMON_TEA_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(330,N'DEMO_RECIPE_SKU_HONEY_LEMON_TEA_M',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(331,N'DEMO_RECIPE_SKU_HONEY_LEMON_TEA_L',N'P',N'DEMO_PREP_BLACK_TEA',200,N'ml'),
(332,N'DEMO_RECIPE_SKU_HONEY_LEMON_TEA_L',N'I',N'DEMO_ING_HONEY',28,N'g'),
(333,N'DEMO_RECIPE_SKU_HONEY_LEMON_TEA_L',N'I',N'DEMO_ING_YELLOW_LEMON',35,N'g'),
(334,N'DEMO_RECIPE_SKU_HONEY_LEMON_TEA_L',N'P',N'DEMO_PREP_SUGAR_SYRUP',14,N'ml'),
(335,N'DEMO_RECIPE_SKU_HONEY_LEMON_TEA_L',N'I',N'ING00007',200,N'g'),
(336,N'DEMO_RECIPE_SKU_HONEY_LEMON_TEA_L',N'I',N'DEMO_ING_CUP_L',1,N'pcs'),
(337,N'DEMO_RECIPE_SKU_HONEY_LEMON_TEA_L',N'I',N'DEMO_ING_LID_L',1,N'pcs'),
(338,N'DEMO_RECIPE_SKU_HONEY_LEMON_TEA_L',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(339,N'DEMO_RECIPE_SKU_HONEY_LEMON_TEA_L',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(340,N'DEMO_RECIPE_SKU_MANGO_TEA_M',N'P',N'DEMO_PREP_BLACK_TEA',150,N'ml'),
(341,N'DEMO_RECIPE_SKU_MANGO_TEA_M',N'I',N'DEMO_ING_MANGO_PUREE',60,N'g'),
(342,N'DEMO_RECIPE_SKU_MANGO_TEA_M',N'P',N'DEMO_PREP_SUGAR_SYRUP',15,N'ml'),
(343,N'DEMO_RECIPE_SKU_MANGO_TEA_M',N'I',N'ING00007',150,N'g'),
(344,N'DEMO_RECIPE_SKU_MANGO_TEA_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(345,N'DEMO_RECIPE_SKU_MANGO_TEA_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(346,N'DEMO_RECIPE_SKU_MANGO_TEA_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(347,N'DEMO_RECIPE_SKU_MANGO_TEA_M',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(348,N'DEMO_RECIPE_SKU_MANGO_TEA_L',N'P',N'DEMO_PREP_BLACK_TEA',200,N'ml'),
(349,N'DEMO_RECIPE_SKU_MANGO_TEA_L',N'I',N'DEMO_ING_MANGO_PUREE',85,N'g'),
(350,N'DEMO_RECIPE_SKU_MANGO_TEA_L',N'P',N'DEMO_PREP_SUGAR_SYRUP',20,N'ml'),
(351,N'DEMO_RECIPE_SKU_MANGO_TEA_L',N'I',N'ING00007',200,N'g'),
(352,N'DEMO_RECIPE_SKU_MANGO_TEA_L',N'I',N'DEMO_ING_CUP_L',1,N'pcs'),
(353,N'DEMO_RECIPE_SKU_MANGO_TEA_L',N'I',N'DEMO_ING_LID_L',1,N'pcs'),
(354,N'DEMO_RECIPE_SKU_MANGO_TEA_L',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(355,N'DEMO_RECIPE_SKU_MANGO_TEA_L',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(356,N'DEMO_RECIPE_SKU_STRAWBERRY_MILK_TEA_M',N'P',N'DEMO_PREP_BLACK_TEA',140,N'ml'),
(357,N'DEMO_RECIPE_SKU_STRAWBERRY_MILK_TEA_M',N'I',N'DEMO_ING_STRAWBERRY_PUREE',45,N'g'),
(358,N'DEMO_RECIPE_SKU_STRAWBERRY_MILK_TEA_M',N'I',N'DEMO_ING_FRESH_MILK',100,N'ml'),
(359,N'DEMO_RECIPE_SKU_STRAWBERRY_MILK_TEA_M',N'I',N'ING00010',20,N'ml'),
(360,N'DEMO_RECIPE_SKU_STRAWBERRY_MILK_TEA_M',N'P',N'DEMO_PREP_SUGAR_SYRUP',15,N'ml'),
(361,N'DEMO_RECIPE_SKU_STRAWBERRY_MILK_TEA_M',N'I',N'ING00007',150,N'g'),
(362,N'DEMO_RECIPE_SKU_STRAWBERRY_MILK_TEA_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(363,N'DEMO_RECIPE_SKU_STRAWBERRY_MILK_TEA_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(364,N'DEMO_RECIPE_SKU_STRAWBERRY_MILK_TEA_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(365,N'DEMO_RECIPE_SKU_STRAWBERRY_MILK_TEA_M',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(366,N'DEMO_RECIPE_SKU_STRAWBERRY_MILK_TEA_L',N'P',N'DEMO_PREP_BLACK_TEA',190,N'ml'),
(367,N'DEMO_RECIPE_SKU_STRAWBERRY_MILK_TEA_L',N'I',N'DEMO_ING_STRAWBERRY_PUREE',65,N'g'),
(368,N'DEMO_RECIPE_SKU_STRAWBERRY_MILK_TEA_L',N'I',N'DEMO_ING_FRESH_MILK',140,N'ml'),
(369,N'DEMO_RECIPE_SKU_STRAWBERRY_MILK_TEA_L',N'I',N'ING00010',28,N'ml'),
(370,N'DEMO_RECIPE_SKU_STRAWBERRY_MILK_TEA_L',N'P',N'DEMO_PREP_SUGAR_SYRUP',20,N'ml'),
(371,N'DEMO_RECIPE_SKU_STRAWBERRY_MILK_TEA_L',N'I',N'ING00007',200,N'g'),
(372,N'DEMO_RECIPE_SKU_STRAWBERRY_MILK_TEA_L',N'I',N'DEMO_ING_CUP_L',1,N'pcs'),
(373,N'DEMO_RECIPE_SKU_STRAWBERRY_MILK_TEA_L',N'I',N'DEMO_ING_LID_L',1,N'pcs'),
(374,N'DEMO_RECIPE_SKU_STRAWBERRY_MILK_TEA_L',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(375,N'DEMO_RECIPE_SKU_STRAWBERRY_MILK_TEA_L',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(376,N'DEMO_RECIPE_SKU_LYCHEE_OOLONG_M',N'P',N'DEMO_PREP_OOLONG_TEA',150,N'ml'),
(377,N'DEMO_RECIPE_SKU_LYCHEE_OOLONG_M',N'I',N'DEMO_ING_CANNED_LYCHEE',70,N'g'),
(378,N'DEMO_RECIPE_SKU_LYCHEE_OOLONG_M',N'P',N'DEMO_PREP_SUGAR_SYRUP',18,N'ml'),
(379,N'DEMO_RECIPE_SKU_LYCHEE_OOLONG_M',N'I',N'ING00007',150,N'g'),
(380,N'DEMO_RECIPE_SKU_LYCHEE_OOLONG_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(381,N'DEMO_RECIPE_SKU_LYCHEE_OOLONG_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(382,N'DEMO_RECIPE_SKU_LYCHEE_OOLONG_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(383,N'DEMO_RECIPE_SKU_LYCHEE_OOLONG_M',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(384,N'DEMO_RECIPE_SKU_LYCHEE_OOLONG_L',N'P',N'DEMO_PREP_OOLONG_TEA',200,N'ml'),
(385,N'DEMO_RECIPE_SKU_LYCHEE_OOLONG_L',N'I',N'DEMO_ING_CANNED_LYCHEE',90,N'g'),
(386,N'DEMO_RECIPE_SKU_LYCHEE_OOLONG_L',N'P',N'DEMO_PREP_SUGAR_SYRUP',24,N'ml'),
(387,N'DEMO_RECIPE_SKU_LYCHEE_OOLONG_L',N'I',N'ING00007',200,N'g'),
(388,N'DEMO_RECIPE_SKU_LYCHEE_OOLONG_L',N'I',N'DEMO_ING_CUP_L',1,N'pcs'),
(389,N'DEMO_RECIPE_SKU_LYCHEE_OOLONG_L',N'I',N'DEMO_ING_LID_L',1,N'pcs'),
(390,N'DEMO_RECIPE_SKU_LYCHEE_OOLONG_L',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(391,N'DEMO_RECIPE_SKU_LYCHEE_OOLONG_L',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(392,N'DEMO_RECIPE_SKU_OAT_MATCHA_M',N'I',N'ING00009',8,N'g'),
(393,N'DEMO_RECIPE_SKU_OAT_MATCHA_M',N'I',N'DEMO_ING_OAT_MILK',160,N'ml'),
(394,N'DEMO_RECIPE_SKU_OAT_MATCHA_M',N'P',N'DEMO_PREP_SUGAR_SYRUP',15,N'ml'),
(395,N'DEMO_RECIPE_SKU_OAT_MATCHA_M',N'I',N'ING00007',150,N'g'),
(396,N'DEMO_RECIPE_SKU_OAT_MATCHA_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(397,N'DEMO_RECIPE_SKU_OAT_MATCHA_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(398,N'DEMO_RECIPE_SKU_OAT_MATCHA_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(399,N'DEMO_RECIPE_SKU_OAT_MATCHA_M',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(400,N'DEMO_RECIPE_SKU_OAT_MATCHA_L',N'I',N'ING00009',11,N'g'),
(401,N'DEMO_RECIPE_SKU_OAT_MATCHA_L',N'I',N'DEMO_ING_OAT_MILK',220,N'ml'),
(402,N'DEMO_RECIPE_SKU_OAT_MATCHA_L',N'P',N'DEMO_PREP_SUGAR_SYRUP',20,N'ml'),
(403,N'DEMO_RECIPE_SKU_OAT_MATCHA_L',N'I',N'ING00007',200,N'g'),
(404,N'DEMO_RECIPE_SKU_OAT_MATCHA_L',N'I',N'DEMO_ING_CUP_L',1,N'pcs'),
(405,N'DEMO_RECIPE_SKU_OAT_MATCHA_L',N'I',N'DEMO_ING_LID_L',1,N'pcs'),
(406,N'DEMO_RECIPE_SKU_OAT_MATCHA_L',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(407,N'DEMO_RECIPE_SKU_OAT_MATCHA_L',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(408,N'DEMO_RECIPE_SKU_COCONUT_CHOCOLATE_M',N'I',N'DEMO_ING_CHOCOLATE',22,N'g'),
(409,N'DEMO_RECIPE_SKU_COCONUT_CHOCOLATE_M',N'I',N'DEMO_ING_COCONUT_MILK',90,N'ml'),
(410,N'DEMO_RECIPE_SKU_COCONUT_CHOCOLATE_M',N'I',N'DEMO_ING_FRESH_MILK',80,N'ml'),
(411,N'DEMO_RECIPE_SKU_COCONUT_CHOCOLATE_M',N'P',N'DEMO_PREP_SUGAR_SYRUP',12,N'ml'),
(412,N'DEMO_RECIPE_SKU_COCONUT_CHOCOLATE_M',N'I',N'ING00007',150,N'g'),
(413,N'DEMO_RECIPE_SKU_COCONUT_CHOCOLATE_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(414,N'DEMO_RECIPE_SKU_COCONUT_CHOCOLATE_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(415,N'DEMO_RECIPE_SKU_COCONUT_CHOCOLATE_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(416,N'DEMO_RECIPE_SKU_COCONUT_CHOCOLATE_M',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(417,N'DEMO_RECIPE_SKU_COCONUT_CHOCOLATE_L',N'I',N'DEMO_ING_CHOCOLATE',28,N'g'),
(418,N'DEMO_RECIPE_SKU_COCONUT_CHOCOLATE_L',N'I',N'DEMO_ING_COCONUT_MILK',125,N'ml'),
(419,N'DEMO_RECIPE_SKU_COCONUT_CHOCOLATE_L',N'I',N'DEMO_ING_FRESH_MILK',110,N'ml'),
(420,N'DEMO_RECIPE_SKU_COCONUT_CHOCOLATE_L',N'P',N'DEMO_PREP_SUGAR_SYRUP',16,N'ml'),
(421,N'DEMO_RECIPE_SKU_COCONUT_CHOCOLATE_L',N'I',N'ING00007',200,N'g'),
(422,N'DEMO_RECIPE_SKU_COCONUT_CHOCOLATE_L',N'I',N'DEMO_ING_CUP_L',1,N'pcs'),
(423,N'DEMO_RECIPE_SKU_COCONUT_CHOCOLATE_L',N'I',N'DEMO_ING_LID_L',1,N'pcs'),
(424,N'DEMO_RECIPE_SKU_COCONUT_CHOCOLATE_L',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(425,N'DEMO_RECIPE_SKU_COCONUT_CHOCOLATE_L',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(426,N'DEMO_RECIPE_SKU_PASSION_YOGURT_M',N'I',N'DEMO_ING_YOGURT',150,N'g'),
(427,N'DEMO_RECIPE_SKU_PASSION_YOGURT_M',N'I',N'DEMO_ING_PASSION_JAM',55,N'g'),
(428,N'DEMO_RECIPE_SKU_PASSION_YOGURT_M',N'P',N'DEMO_PREP_SUGAR_SYRUP',10,N'ml'),
(429,N'DEMO_RECIPE_SKU_PASSION_YOGURT_M',N'I',N'ING00007',120,N'g'),
(430,N'DEMO_RECIPE_SKU_PASSION_YOGURT_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(431,N'DEMO_RECIPE_SKU_PASSION_YOGURT_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(432,N'DEMO_RECIPE_SKU_PASSION_YOGURT_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(433,N'DEMO_RECIPE_SKU_PASSION_YOGURT_M',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(434,N'DEMO_RECIPE_SKU_PASSION_YOGURT_L',N'I',N'DEMO_ING_YOGURT',210,N'g'),
(435,N'DEMO_RECIPE_SKU_PASSION_YOGURT_L',N'I',N'DEMO_ING_PASSION_JAM',75,N'g'),
(436,N'DEMO_RECIPE_SKU_PASSION_YOGURT_L',N'P',N'DEMO_PREP_SUGAR_SYRUP',14,N'ml'),
(437,N'DEMO_RECIPE_SKU_PASSION_YOGURT_L',N'I',N'ING00007',170,N'g'),
(438,N'DEMO_RECIPE_SKU_PASSION_YOGURT_L',N'I',N'DEMO_ING_CUP_L',1,N'pcs'),
(439,N'DEMO_RECIPE_SKU_PASSION_YOGURT_L',N'I',N'DEMO_ING_LID_L',1,N'pcs'),
(440,N'DEMO_RECIPE_SKU_PASSION_YOGURT_L',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(441,N'DEMO_RECIPE_SKU_PASSION_YOGURT_L',N'I',N'DEMO_ING_BAG',1,N'pcs'),
(442,N'DEMO_RECIPE_TOP_PM_VIEN',N'I',N'DEMO_ING_CHEESE_CUBE',1,N'pcs'),
(443,N'DEMO_RECIPE_TOP_KB_CM',N'I',N'DEMO_ING_KHUC_BACH_POWDER',35,N'g'),
(444,N'DEMO_RECIPE_TOP_TH_Dao',N'I',N'DEMO_ING_CANNED_PEACH',40,N'g'),
(445,N'DEMO_RECIPE_TOP_NHADAM',N'I',N'DEMO_ING_ALOE_VERA',40,N'g'),
(446,N'DEMO_RECIPE_TOP_HATCHIA',N'I',N'DEMO_ING_CHIA_SEED',10,N'g'),
(447,N'DEMO_RECIPE_TOP_TH_Dua',N'I',N'DEMO_ING_COCONUT_JELLY',40,N'g'),
(448,N'DEMO_RECIPE_TOP_PUDDINGTRUNG',N'I',N'DEMO_ING_FLAN_POWDER',35,N'g'),
(449,N'DEMO_RECIPE_TOP_PUDDINGTRUNG',N'I',N'ING00006',5,N'g'),
(450,N'DEMO_RECIPE_TOP_TC_HOANGKIM',N'I',N'DEMO_ING_BLACK_PEARL_DRY',35,N'g'),
(451,N'DEMO_RECIPE_TOP_TC_DUONGDEN',N'P',N'DEMO_PREP_BLACK_PEARL',1,N'DEMO_PORTION'),
(452,N'DEMO_RECIPE_TOP_TC_DUONGDEN',N'I',N'ING00012',15,N'g'),
(453,N'DEMO_RECIPE_TOP_TC_MINI',N'P',N'DEMO_PREP_BLACK_PEARL',1,N'DEMO_PORTION'),
(454,N'DEMO_RECIPE_TOP_TC_KHOAIMON',N'I',N'DEMO_ING_TARO_JELLY_POWDER',30,N'g'),
(455,N'DEMO_RECIPE_TOP_TH_CAFE',N'I',N'DEMO_ING_VIET_COFFEE',20,N'g'),
(456,N'DEMO_RECIPE_TOP_TH_CAFE',N'I',N'ING00011',25,N'g'),
(457,N'DEMO_RECIPE_TOP_TH_MATCHA',N'I',N'ING00009',8,N'g'),
(458,N'DEMO_RECIPE_TOP_TH_MATCHA',N'I',N'ING00011',25,N'g'),
(459,N'DEMO_RECIPE_TOP_TH_VAI',N'I',N'DEMO_ING_CANNED_LYCHEE',35,N'g'),
(460,N'DEMO_RECIPE_TOP_TH_XOAI',N'I',N'DEMO_ING_MANGO_PUREE',35,N'g'),
(461,N'DEMO_RECIPE_TOP_TH_DAU',N'I',N'DEMO_ING_STRAWBERRY_PUREE',35,N'g'),
(462,N'DEMO_RECIPE_TOP_TH_CHANHDAY',N'I',N'DEMO_ING_PASSION_JAM',35,N'g'),
(463,N'DEMO_RECIPE_TOP_TH_MATONGCHANH',N'I',N'DEMO_ING_HONEY',15,N'g'),
(464,N'DEMO_RECIPE_TOP_TH_MATONGCHANH',N'I',N'DEMO_ING_YELLOW_LEMON',10,N'g'),
(465,N'DEMO_RECIPE_TOP_TH_SUAYENMACH',N'I',N'DEMO_ING_OAT_MILK',30,N'ml'),
(466,N'DEMO_RECIPE_TOP_TH_SUAYENMACH',N'I',N'ING00011',20,N'g'),
(467,N'DEMO_RECIPE_TOP_TRAIDAO',N'I',N'DEMO_ING_CANNED_PEACH',50,N'g'),
(468,N'DEMO_RECIPE_TOP_TRAIVAI',N'I',N'DEMO_ING_CANNED_LYCHEE',50,N'g'),
(469,N'DEMO_RECIPE_TOP_XOAI_HAT',N'I',N'DEMO_ING_MANGO_PUREE',45,N'g'),
(470,N'DEMO_RECIPE_TOP_DAU_TUOI',N'I',N'DEMO_ING_STRAWBERRY_PUREE',45,N'g'),
(471,N'DEMO_RECIPE_TOP_TEP_CAM',N'I',N'DEMO_ING_ORANGE',40,N'g'),
(472,N'DEMO_RECIPE_TOP_CHANHDAY_HAT',N'I',N'DEMO_ING_PASSION_JAM',40,N'g'),
(473,N'DEMO_RECIPE_TOP_PUDDING_VANILLA',N'I',N'ING00008',10,N'ml'),
(474,N'DEMO_RECIPE_TOP_PUDDING_VANILLA',N'I',N'ING00011',25,N'g'),
(475,N'DEMO_RECIPE_TOP_PUDDING_SOCOLA',N'I',N'DEMO_ING_FLAN_POWDER',30,N'g'),
(476,N'DEMO_RECIPE_TOP_PUDDING_SOCOLA',N'I',N'DEMO_ING_CHOCOLATE',10,N'g'),
(477,N'DEMO_RECIPE_TOP_PUDDING_MATCHA',N'I',N'DEMO_ING_FLAN_POWDER',30,N'g'),
(478,N'DEMO_RECIPE_TOP_PUDDING_MATCHA',N'I',N'ING00009',6,N'g'),
(479,N'DEMO_RECIPE_TOP_PUDDING_KHOAIMON',N'I',N'DEMO_ING_TARO_JELLY_POWDER',35,N'g'),
(480,N'DEMO_RECIPE_TOP_KEMMUOI',N'P',N'DEMO_PREP_SALTED_CREAM',35,N'ml'),
(481,N'DEMO_RECIPE_TOP_KEMSUATUOI',N'I',N'ING00010',35,N'ml'),
(482,N'DEMO_RECIPE_TOP_KEMDUA',N'I',N'DEMO_ING_COCONUT_MILK',35,N'ml'),
(483,N'DEMO_RECIPE_TOP_KEMDUA',N'I',N'ING00010',15,N'ml'),
(484,N'DEMO_RECIPE_TOP_KEMYENMACH',N'I',N'DEMO_ING_OAT_MILK',35,N'ml'),
(485,N'DEMO_RECIPE_TOP_KEMYENMACH',N'I',N'ING00010',10,N'ml'),
(486,N'DEMO_RECIPE_TOP_SOT_CARAMEL',N'I',N'DEMO_ING_CARAMEL_SYRUP',20,N'ml'),
(487,N'DEMO_RECIPE_TOP_SOT_SOCOLA',N'I',N'DEMO_ING_CHOCOLATE',15,N'g'),
(488,N'DEMO_RECIPE_TOP_SOT_DAU',N'I',N'DEMO_ING_STRAWBERRY_PUREE',20,N'g'),
(489,N'DEMO_RECIPE_TOP_SOT_XOAI',N'I',N'DEMO_ING_MANGO_PUREE',20,N'g'),
(490,N'DEMO_RECIPE_TOP_SOT_MATONG',N'I',N'DEMO_ING_HONEY',20,N'g'),
(491,N'DEMO_RECIPE_TOP_SOT_DUONGDEN',N'I',N'ING00012',20,N'g'),
(492,N'DEMO_RECIPE_TOP_SOT_DUONGDEN',N'I',N'ING00013',10,N'ml'),
(493,N'DEMO_RECIPE_TOP_SHOT_MATCHA',N'I',N'ING00009',5,N'g'),
(494,N'DEMO_RECIPE_TOP_SUA_YENMACH_THEM',N'I',N'DEMO_ING_OAT_MILK',40,N'ml'),
(495,N'DEMO_RECIPE_TOP_COT_DUA_THEM',N'I',N'DEMO_ING_COCONUT_MILK',40,N'ml'),
(496,N'DEMO_RECIPE_TOP_SUA_CHUA_THEM',N'I',N'DEMO_ING_YOGURT',40,N'g'),
(497,N'DEMO_RECIPE_TOP_SYRUP_CARAMEL_THEM',N'I',N'DEMO_ING_CARAMEL_SYRUP',20,N'ml'),
(498,N'ZZ_RCP_CHEESE_CREAM_COFFEE_M',N'P',N'DEMO_PREP_ESPRESSO',45,N'ml'),
(499,N'ZZ_RCP_CHEESE_CREAM_COFFEE_M',N'I',N'DEMO_ING_FRESH_MILK',110,N'ml'),
(500,N'ZZ_RCP_CHEESE_CREAM_COFFEE_M',N'P',N'DEMO_PREP_CHEESE_CREAM',30,N'ml'),
(501,N'ZZ_RCP_CHEESE_CREAM_COFFEE_M',N'I',N'ING00007',140,N'g'),
(502,N'ZZ_RCP_CHEESE_CREAM_COFFEE_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(503,N'ZZ_RCP_CHEESE_CREAM_COFFEE_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(504,N'ZZ_RCP_CHEESE_CREAM_COFFEE_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),

(505,N'ZZ_RCP_HONEY_LEMON_COLD_BREW_M',N'P',N'DEMO_PREP_VIET_COFFEE',90,N'ml'),
(506,N'ZZ_RCP_HONEY_LEMON_COLD_BREW_M',N'I',N'DEMO_ING_HONEY',12,N'g'),
(507,N'ZZ_RCP_HONEY_LEMON_COLD_BREW_M',N'I',N'DEMO_ING_YELLOW_LEMON',18,N'g'),
(508,N'ZZ_RCP_HONEY_LEMON_COLD_BREW_M',N'I',N'ING00007',160,N'g'),
(509,N'ZZ_RCP_HONEY_LEMON_COLD_BREW_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(510,N'ZZ_RCP_HONEY_LEMON_COLD_BREW_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(511,N'ZZ_RCP_HONEY_LEMON_COLD_BREW_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),

(512,N'ZZ_RCP_BLACK_PEARL_MILK_COFFEE_M',N'P',N'DEMO_PREP_VIET_COFFEE',60,N'ml'),
(513,N'ZZ_RCP_BLACK_PEARL_MILK_COFFEE_M',N'I',N'ING00002',30,N'ml'),
(514,N'ZZ_RCP_BLACK_PEARL_MILK_COFFEE_M',N'I',N'DEMO_ING_FRESH_MILK',60,N'ml'),
(515,N'ZZ_RCP_BLACK_PEARL_MILK_COFFEE_M',N'I',N'ING00007',170,N'g'),
(516,N'ZZ_RCP_BLACK_PEARL_MILK_COFFEE_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(517,N'ZZ_RCP_BLACK_PEARL_MILK_COFFEE_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(518,N'ZZ_RCP_BLACK_PEARL_MILK_COFFEE_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),

(519,N'ZZ_RCP_HONEY_OAT_ESPRESSO_M',N'P',N'DEMO_PREP_ESPRESSO',45,N'ml'),
(520,N'ZZ_RCP_HONEY_OAT_ESPRESSO_M',N'I',N'DEMO_ING_OAT_MILK',140,N'ml'),
(521,N'ZZ_RCP_HONEY_OAT_ESPRESSO_M',N'I',N'DEMO_ING_HONEY',10,N'g'),
(522,N'ZZ_RCP_HONEY_OAT_ESPRESSO_M',N'I',N'ING00007',140,N'g'),
(523,N'ZZ_RCP_HONEY_OAT_ESPRESSO_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(524,N'ZZ_RCP_HONEY_OAT_ESPRESSO_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(525,N'ZZ_RCP_HONEY_OAT_ESPRESSO_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),

(526,N'ZZ_RCP_FLAN_MILK_COFFEE_M',N'P',N'DEMO_PREP_VIET_COFFEE',60,N'ml'),
(527,N'ZZ_RCP_FLAN_MILK_COFFEE_M',N'I',N'ING00002',30,N'ml'),
(528,N'ZZ_RCP_FLAN_MILK_COFFEE_M',N'I',N'DEMO_ING_FRESH_MILK',80,N'ml'),
(529,N'ZZ_RCP_FLAN_MILK_COFFEE_M',N'I',N'ING00007',160,N'g'),
(530,N'ZZ_RCP_FLAN_MILK_COFFEE_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(531,N'ZZ_RCP_FLAN_MILK_COFFEE_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(532,N'ZZ_RCP_FLAN_MILK_COFFEE_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),

(533,N'ZZ_RCP_LYCHEE_ALOE_COLD_BREW_M',N'P',N'DEMO_PREP_VIET_COFFEE',90,N'ml'),
(534,N'ZZ_RCP_LYCHEE_ALOE_COLD_BREW_M',N'I',N'DEMO_ING_CANNED_LYCHEE',30,N'g'),
(535,N'ZZ_RCP_LYCHEE_ALOE_COLD_BREW_M',N'I',N'DEMO_ING_ALOE_VERA',30,N'g'),
(536,N'ZZ_RCP_LYCHEE_ALOE_COLD_BREW_M',N'I',N'ING00007',160,N'g'),
(537,N'ZZ_RCP_LYCHEE_ALOE_COLD_BREW_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(538,N'ZZ_RCP_LYCHEE_ALOE_COLD_BREW_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(539,N'ZZ_RCP_LYCHEE_ALOE_COLD_BREW_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),

(540,N'ZZ_RCP_SALTED_COCONUT_ESPRESSO_M',N'P',N'DEMO_PREP_ESPRESSO',45,N'ml'),
(541,N'ZZ_RCP_SALTED_COCONUT_ESPRESSO_M',N'I',N'DEMO_ING_COCONUT_MILK',100,N'ml'),
(542,N'ZZ_RCP_SALTED_COCONUT_ESPRESSO_M',N'P',N'DEMO_PREP_SALTED_CREAM',30,N'ml'),
(543,N'ZZ_RCP_SALTED_COCONUT_ESPRESSO_M',N'I',N'ING00007',140,N'g'),
(544,N'ZZ_RCP_SALTED_COCONUT_ESPRESSO_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(545,N'ZZ_RCP_SALTED_COCONUT_ESPRESSO_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(546,N'ZZ_RCP_SALTED_COCONUT_ESPRESSO_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),

(547,N'ZZ_RCP_BROWN_SUGAR_COCONUT_COFFEE_M',N'P',N'DEMO_PREP_VIET_COFFEE',60,N'ml'),
(548,N'ZZ_RCP_BROWN_SUGAR_COCONUT_COFFEE_M',N'I',N'ING00002',25,N'ml'),
(549,N'ZZ_RCP_BROWN_SUGAR_COCONUT_COFFEE_M',N'I',N'DEMO_ING_COCONUT_JELLY',30,N'g'),
(550,N'ZZ_RCP_BROWN_SUGAR_COCONUT_COFFEE_M',N'I',N'ING00007',160,N'g'),
(551,N'ZZ_RCP_BROWN_SUGAR_COCONUT_COFFEE_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(552,N'ZZ_RCP_BROWN_SUGAR_COCONUT_COFFEE_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(553,N'ZZ_RCP_BROWN_SUGAR_COCONUT_COFFEE_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),

(554,N'ZZ_RCP_KHUC_BACH_MILK_COFFEE_M',N'P',N'DEMO_PREP_VIET_COFFEE',60,N'ml'),
(555,N'ZZ_RCP_KHUC_BACH_MILK_COFFEE_M',N'I',N'ING00002',30,N'ml'),
(556,N'ZZ_RCP_KHUC_BACH_MILK_COFFEE_M',N'I',N'DEMO_ING_FRESH_MILK',80,N'ml'),
(557,N'ZZ_RCP_KHUC_BACH_MILK_COFFEE_M',N'I',N'ING00007',160,N'g'),
(558,N'ZZ_RCP_KHUC_BACH_MILK_COFFEE_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(559,N'ZZ_RCP_KHUC_BACH_MILK_COFFEE_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(560,N'ZZ_RCP_KHUC_BACH_MILK_COFFEE_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),

(561,N'ZZ_RCP_MANGO_PASSION_COLD_BREW_M',N'P',N'DEMO_PREP_VIET_COFFEE',90,N'ml'),
(562,N'ZZ_RCP_MANGO_PASSION_COLD_BREW_M',N'I',N'DEMO_ING_MANGO_PUREE',25,N'g'),
(563,N'ZZ_RCP_MANGO_PASSION_COLD_BREW_M',N'I',N'DEMO_ING_PASSION_JAM',20,N'g'),
(564,N'ZZ_RCP_MANGO_PASSION_COLD_BREW_M',N'I',N'ING00007',160,N'g'),
(565,N'ZZ_RCP_MANGO_PASSION_COLD_BREW_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(566,N'ZZ_RCP_MANGO_PASSION_COLD_BREW_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(567,N'ZZ_RCP_MANGO_PASSION_COLD_BREW_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),

(568,N'ZZ_RCP_PEACH_ALOE_OOLONG_M',N'P',N'DEMO_PREP_OOLONG_TEA',150,N'ml'),
(569,N'ZZ_RCP_PEACH_ALOE_OOLONG_M',N'I',N'DEMO_ING_CANNED_PEACH',35,N'g'),
(570,N'ZZ_RCP_PEACH_ALOE_OOLONG_M',N'I',N'DEMO_ING_ALOE_VERA',25,N'g'),
(571,N'ZZ_RCP_PEACH_ALOE_OOLONG_M',N'I',N'ING00007',150,N'g'),
(572,N'ZZ_RCP_PEACH_ALOE_OOLONG_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(573,N'ZZ_RCP_PEACH_ALOE_OOLONG_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(574,N'ZZ_RCP_PEACH_ALOE_OOLONG_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),

(575,N'ZZ_RCP_LYCHEE_CHIA_BLACK_TEA_M',N'P',N'DEMO_PREP_BLACK_TEA',150,N'ml'),
(576,N'ZZ_RCP_LYCHEE_CHIA_BLACK_TEA_M',N'I',N'DEMO_ING_CANNED_LYCHEE',35,N'g'),
(577,N'ZZ_RCP_LYCHEE_CHIA_BLACK_TEA_M',N'I',N'DEMO_ING_CHIA_SEED',8,N'g'),
(578,N'ZZ_RCP_LYCHEE_CHIA_BLACK_TEA_M',N'I',N'ING00007',150,N'g'),
(579,N'ZZ_RCP_LYCHEE_CHIA_BLACK_TEA_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(580,N'ZZ_RCP_LYCHEE_CHIA_BLACK_TEA_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(581,N'ZZ_RCP_LYCHEE_CHIA_BLACK_TEA_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),

(582,N'ZZ_RCP_MANGO_COCONUT_OOLONG_M',N'P',N'DEMO_PREP_OOLONG_TEA',150,N'ml'),
(583,N'ZZ_RCP_MANGO_COCONUT_OOLONG_M',N'I',N'DEMO_ING_MANGO_PUREE',30,N'g'),
(584,N'ZZ_RCP_MANGO_COCONUT_OOLONG_M',N'I',N'DEMO_ING_COCONUT_JELLY',30,N'g'),
(585,N'ZZ_RCP_MANGO_COCONUT_OOLONG_M',N'I',N'ING00007',150,N'g'),
(586,N'ZZ_RCP_MANGO_COCONUT_OOLONG_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(587,N'ZZ_RCP_MANGO_COCONUT_OOLONG_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(588,N'ZZ_RCP_MANGO_COCONUT_OOLONG_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),

(589,N'ZZ_RCP_ORANGE_ALOE_BLACK_TEA_M',N'P',N'DEMO_PREP_BLACK_TEA',150,N'ml'),
(590,N'ZZ_RCP_ORANGE_ALOE_BLACK_TEA_M',N'I',N'DEMO_ING_ORANGE',40,N'g'),
(591,N'ZZ_RCP_ORANGE_ALOE_BLACK_TEA_M',N'I',N'DEMO_ING_ALOE_VERA',25,N'g'),
(592,N'ZZ_RCP_ORANGE_ALOE_BLACK_TEA_M',N'I',N'ING00007',150,N'g'),
(593,N'ZZ_RCP_ORANGE_ALOE_BLACK_TEA_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(594,N'ZZ_RCP_ORANGE_ALOE_BLACK_TEA_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(595,N'ZZ_RCP_ORANGE_ALOE_BLACK_TEA_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),

(596,N'ZZ_RCP_PASSION_CHIA_TEA_M',N'P',N'DEMO_PREP_BLACK_TEA',150,N'ml'),
(597,N'ZZ_RCP_PASSION_CHIA_TEA_M',N'I',N'DEMO_ING_PASSION_JAM',35,N'g'),
(598,N'ZZ_RCP_PASSION_CHIA_TEA_M',N'I',N'DEMO_ING_CHIA_SEED',8,N'g'),
(599,N'ZZ_RCP_PASSION_CHIA_TEA_M',N'I',N'ING00007',150,N'g'),
(600,N'ZZ_RCP_PASSION_CHIA_TEA_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(601,N'ZZ_RCP_PASSION_CHIA_TEA_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(602,N'ZZ_RCP_PASSION_CHIA_TEA_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),

(603,N'ZZ_RCP_STRAWBERRY_COCONUT_OOLONG_M',N'P',N'DEMO_PREP_OOLONG_TEA',150,N'ml'),
(604,N'ZZ_RCP_STRAWBERRY_COCONUT_OOLONG_M',N'I',N'DEMO_ING_STRAWBERRY_PUREE',30,N'g'),
(605,N'ZZ_RCP_STRAWBERRY_COCONUT_OOLONG_M',N'I',N'DEMO_ING_COCONUT_JELLY',30,N'g'),
(606,N'ZZ_RCP_STRAWBERRY_COCONUT_OOLONG_M',N'I',N'ING00007',150,N'g'),
(607,N'ZZ_RCP_STRAWBERRY_COCONUT_OOLONG_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(608,N'ZZ_RCP_STRAWBERRY_COCONUT_OOLONG_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(609,N'ZZ_RCP_STRAWBERRY_COCONUT_OOLONG_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),

(610,N'ZZ_RCP_PEACH_KHUC_BACH_TEA_M',N'P',N'DEMO_PREP_BLACK_TEA',150,N'ml'),
(611,N'ZZ_RCP_PEACH_KHUC_BACH_TEA_M',N'I',N'DEMO_ING_CANNED_PEACH',35,N'g'),
(612,N'ZZ_RCP_PEACH_KHUC_BACH_TEA_M',N'P',N'DEMO_PREP_SUGAR_SYRUP',10,N'ml'),
(613,N'ZZ_RCP_PEACH_KHUC_BACH_TEA_M',N'I',N'ING00007',150,N'g'),
(614,N'ZZ_RCP_PEACH_KHUC_BACH_TEA_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(615,N'ZZ_RCP_PEACH_KHUC_BACH_TEA_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(616,N'ZZ_RCP_PEACH_KHUC_BACH_TEA_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),

(617,N'ZZ_RCP_LYCHEE_ALOE_TEA_M',N'P',N'DEMO_PREP_BLACK_TEA',150,N'ml'),
(618,N'ZZ_RCP_LYCHEE_ALOE_TEA_M',N'I',N'DEMO_ING_CANNED_LYCHEE',35,N'g'),
(619,N'ZZ_RCP_LYCHEE_ALOE_TEA_M',N'I',N'DEMO_ING_ALOE_VERA',25,N'g'),
(620,N'ZZ_RCP_LYCHEE_ALOE_TEA_M',N'I',N'ING00007',150,N'g'),
(621,N'ZZ_RCP_LYCHEE_ALOE_TEA_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(622,N'ZZ_RCP_LYCHEE_ALOE_TEA_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(623,N'ZZ_RCP_LYCHEE_ALOE_TEA_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),

(624,N'ZZ_RCP_MANGO_CHIA_TEA_M',N'P',N'DEMO_PREP_BLACK_TEA',150,N'ml'),
(625,N'ZZ_RCP_MANGO_CHIA_TEA_M',N'I',N'DEMO_ING_MANGO_PUREE',30,N'g'),
(626,N'ZZ_RCP_MANGO_CHIA_TEA_M',N'I',N'DEMO_ING_CHIA_SEED',8,N'g'),
(627,N'ZZ_RCP_MANGO_CHIA_TEA_M',N'I',N'ING00007',150,N'g'),
(628,N'ZZ_RCP_MANGO_CHIA_TEA_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(629,N'ZZ_RCP_MANGO_CHIA_TEA_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(630,N'ZZ_RCP_MANGO_CHIA_TEA_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),

(631,N'ZZ_RCP_ORANGE_PASSION_TEA_M',N'P',N'DEMO_PREP_BLACK_TEA',150,N'ml'),
(632,N'ZZ_RCP_ORANGE_PASSION_TEA_M',N'I',N'DEMO_ING_ORANGE',30,N'g'),
(633,N'ZZ_RCP_ORANGE_PASSION_TEA_M',N'I',N'DEMO_ING_PASSION_JAM',25,N'g'),
(634,N'ZZ_RCP_ORANGE_PASSION_TEA_M',N'I',N'ING00007',150,N'g'),
(635,N'ZZ_RCP_ORANGE_PASSION_TEA_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(636,N'ZZ_RCP_ORANGE_PASSION_TEA_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(637,N'ZZ_RCP_ORANGE_PASSION_TEA_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),

(638,N'ZZ_RCP_BROWN_SUGAR_PEARL_MILK_TEA_M',N'P',N'DEMO_PREP_BLACK_TEA',120,N'ml'),
(639,N'ZZ_RCP_BROWN_SUGAR_PEARL_MILK_TEA_M',N'I',N'DEMO_ING_FRESH_MILK',100,N'ml'),
(640,N'ZZ_RCP_BROWN_SUGAR_PEARL_MILK_TEA_M',N'P',N'DEMO_PREP_SUGAR_SYRUP',12,N'ml'),
(641,N'ZZ_RCP_BROWN_SUGAR_PEARL_MILK_TEA_M',N'I',N'ING00007',150,N'g'),
(642,N'ZZ_RCP_BROWN_SUGAR_PEARL_MILK_TEA_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(643,N'ZZ_RCP_BROWN_SUGAR_PEARL_MILK_TEA_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(644,N'ZZ_RCP_BROWN_SUGAR_PEARL_MILK_TEA_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),

(645,N'ZZ_RCP_FLAN_MILK_TEA_M',N'P',N'DEMO_PREP_BLACK_TEA',120,N'ml'),
(646,N'ZZ_RCP_FLAN_MILK_TEA_M',N'I',N'DEMO_ING_FRESH_MILK',100,N'ml'),
(647,N'ZZ_RCP_FLAN_MILK_TEA_M',N'P',N'DEMO_PREP_SUGAR_SYRUP',12,N'ml'),
(648,N'ZZ_RCP_FLAN_MILK_TEA_M',N'I',N'ING00007',150,N'g'),
(649,N'ZZ_RCP_FLAN_MILK_TEA_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(650,N'ZZ_RCP_FLAN_MILK_TEA_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(651,N'ZZ_RCP_FLAN_MILK_TEA_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),

(652,N'ZZ_RCP_KHUC_BACH_MILK_TEA_M',N'P',N'DEMO_PREP_BLACK_TEA',120,N'ml'),
(653,N'ZZ_RCP_KHUC_BACH_MILK_TEA_M',N'I',N'DEMO_ING_FRESH_MILK',100,N'ml'),
(654,N'ZZ_RCP_KHUC_BACH_MILK_TEA_M',N'P',N'DEMO_PREP_SUGAR_SYRUP',12,N'ml'),
(655,N'ZZ_RCP_KHUC_BACH_MILK_TEA_M',N'I',N'ING00007',150,N'g'),
(656,N'ZZ_RCP_KHUC_BACH_MILK_TEA_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(657,N'ZZ_RCP_KHUC_BACH_MILK_TEA_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(658,N'ZZ_RCP_KHUC_BACH_MILK_TEA_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),

(659,N'ZZ_RCP_ALOE_MILK_TEA_M',N'P',N'DEMO_PREP_BLACK_TEA',120,N'ml'),
(660,N'ZZ_RCP_ALOE_MILK_TEA_M',N'I',N'DEMO_ING_FRESH_MILK',90,N'ml'),
(661,N'ZZ_RCP_ALOE_MILK_TEA_M',N'I',N'DEMO_ING_ALOE_VERA',30,N'g'),
(662,N'ZZ_RCP_ALOE_MILK_TEA_M',N'I',N'ING00007',150,N'g'),
(663,N'ZZ_RCP_ALOE_MILK_TEA_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(664,N'ZZ_RCP_ALOE_MILK_TEA_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(665,N'ZZ_RCP_ALOE_MILK_TEA_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),

(666,N'ZZ_RCP_COCONUT_JELLY_MILK_TEA_M',N'P',N'DEMO_PREP_BLACK_TEA',120,N'ml'),
(667,N'ZZ_RCP_COCONUT_JELLY_MILK_TEA_M',N'I',N'DEMO_ING_FRESH_MILK',90,N'ml'),
(668,N'ZZ_RCP_COCONUT_JELLY_MILK_TEA_M',N'I',N'DEMO_ING_COCONUT_JELLY',30,N'g'),
(669,N'ZZ_RCP_COCONUT_JELLY_MILK_TEA_M',N'I',N'ING00007',150,N'g'),
(670,N'ZZ_RCP_COCONUT_JELLY_MILK_TEA_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(671,N'ZZ_RCP_COCONUT_JELLY_MILK_TEA_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(672,N'ZZ_RCP_COCONUT_JELLY_MILK_TEA_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),

(673,N'ZZ_RCP_CHEESE_CREAM_MILK_TEA_M',N'P',N'DEMO_PREP_BLACK_TEA',120,N'ml'),
(674,N'ZZ_RCP_CHEESE_CREAM_MILK_TEA_M',N'I',N'DEMO_ING_FRESH_MILK',90,N'ml'),
(675,N'ZZ_RCP_CHEESE_CREAM_MILK_TEA_M',N'P',N'DEMO_PREP_CHEESE_CREAM',30,N'ml'),
(676,N'ZZ_RCP_CHEESE_CREAM_MILK_TEA_M',N'I',N'ING00007',150,N'g'),
(677,N'ZZ_RCP_CHEESE_CREAM_MILK_TEA_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(678,N'ZZ_RCP_CHEESE_CREAM_MILK_TEA_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(679,N'ZZ_RCP_CHEESE_CREAM_MILK_TEA_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),

(680,N'ZZ_RCP_STRAWBERRY_CHEESE_MATCHA_M',N'I',N'ING00009',8,N'g'),
(681,N'ZZ_RCP_STRAWBERRY_CHEESE_MATCHA_M',N'I',N'DEMO_ING_FRESH_MILK',120,N'ml'),
(682,N'ZZ_RCP_STRAWBERRY_CHEESE_MATCHA_M',N'I',N'DEMO_ING_STRAWBERRY_PUREE',25,N'g'),
(683,N'ZZ_RCP_STRAWBERRY_CHEESE_MATCHA_M',N'I',N'ING00007',150,N'g'),
(684,N'ZZ_RCP_STRAWBERRY_CHEESE_MATCHA_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(685,N'ZZ_RCP_STRAWBERRY_CHEESE_MATCHA_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(686,N'ZZ_RCP_STRAWBERRY_CHEESE_MATCHA_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),

(687,N'ZZ_RCP_MANGO_COCONUT_MATCHA_M',N'I',N'ING00009',8,N'g'),
(688,N'ZZ_RCP_MANGO_COCONUT_MATCHA_M',N'I',N'DEMO_ING_FRESH_MILK',110,N'ml'),
(689,N'ZZ_RCP_MANGO_COCONUT_MATCHA_M',N'I',N'DEMO_ING_MANGO_PUREE',25,N'g'),
(690,N'ZZ_RCP_MANGO_COCONUT_MATCHA_M',N'I',N'ING00007',150,N'g'),
(691,N'ZZ_RCP_MANGO_COCONUT_MATCHA_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(692,N'ZZ_RCP_MANGO_COCONUT_MATCHA_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(693,N'ZZ_RCP_MANGO_COCONUT_MATCHA_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),

(694,N'ZZ_RCP_SALTED_CARAMEL_CHOCOLATE_M',N'I',N'DEMO_ING_CHOCOLATE',20,N'g'),
(695,N'ZZ_RCP_SALTED_CARAMEL_CHOCOLATE_M',N'I',N'DEMO_ING_FRESH_MILK',120,N'ml'),
(696,N'ZZ_RCP_SALTED_CARAMEL_CHOCOLATE_M',N'I',N'DEMO_ING_CARAMEL_SYRUP',15,N'ml'),
(697,N'ZZ_RCP_SALTED_CARAMEL_CHOCOLATE_M',N'I',N'ING00007',150,N'g'),
(698,N'ZZ_RCP_SALTED_CARAMEL_CHOCOLATE_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(699,N'ZZ_RCP_SALTED_CARAMEL_CHOCOLATE_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(700,N'ZZ_RCP_SALTED_CARAMEL_CHOCOLATE_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),

(701,N'ZZ_RCP_MANGO_ALOE_YOGURT_M',N'I',N'DEMO_ING_YOGURT',140,N'g'),
(702,N'ZZ_RCP_MANGO_ALOE_YOGURT_M',N'I',N'DEMO_ING_MANGO_PUREE',30,N'g'),
(703,N'ZZ_RCP_MANGO_ALOE_YOGURT_M',N'I',N'DEMO_ING_ALOE_VERA',25,N'g'),
(704,N'ZZ_RCP_MANGO_ALOE_YOGURT_M',N'I',N'ING00007',120,N'g'),
(705,N'ZZ_RCP_MANGO_ALOE_YOGURT_M',N'I',N'DEMO_ING_CUP_M',1,N'pcs'),
(706,N'ZZ_RCP_MANGO_ALOE_YOGURT_M',N'I',N'DEMO_ING_LID_M',1,N'pcs'),
(707,N'ZZ_RCP_MANGO_ALOE_YOGURT_M',N'I',N'DEMO_ING_STRAW',1,N'pcs'),
(708,N'DEMO_RECIPE_PREP_ALOE_BASE',N'I',N'DEMO_ING_ALOE_VERA',1000,N'g'),
(709,N'DEMO_RECIPE_PREP_COCONUT_JELLY_BASE',N'I',N'DEMO_ING_COCONUT_JELLY',1000,N'g'),
(710,N'DEMO_RECIPE_PREP_KHUC_BACH_BASE',N'I',N'DEMO_ING_KHUC_BACH_POWDER',1000,N'g'),
(711,N'DEMO_RECIPE_PREP_LEGACY_CREAM',N'I',N'DEMO_ING_OAT_MILK',1000,N'ml');

 IF (SELECT COUNT(*) FROM @Component)<>711
 OR EXISTS(SELECT 1 FROM @Component WHERE Quantity<=0 OR SourceType NOT IN(N'I',N'P'))
 OR EXISTS(SELECT RecipeCode,SourceType,SourceCode FROM @Component GROUP BY RecipeCode,SourceType,SourceCode HAVING COUNT(*)>1)
  THROW 52308,N'Bộ 711 component BOM sai quantity hoặc trùng nguồn.',1;

 IF EXISTS(SELECT 1 FROM @Component c LEFT JOIN dbo.Recipes r ON r.RecipeCode=c.RecipeCode
 LEFT JOIN dbo.Units u ON u.UnitCode=c.UnitCode
 LEFT JOIN dbo.Ingredients i ON c.SourceType=N'I' AND i.Code=c.SourceCode
 LEFT JOIN dbo.PreparedItems p ON c.SourceType=N'P' AND p.Code=c.SourceCode
 LEFT JOIN dbo.Recipes ch ON ch.PreparedItemId=p.PreparedItemId AND ch.Active=1 AND ch.Status=N'Active'
 WHERE r.RecipeId IS NULL OR u.UnitId IS NULL OR(c.SourceType=N'I' AND i.IngredientId IS NULL)
 OR(c.SourceType=N'P' AND ch.RecipeId IS NULL))
  THROW 52309,N'Không resolve được Recipe, Unit, Ingredient hoặc child Recipe.',1;

 DECLARE @DetailSeed TABLE(RecipeDetailId int PRIMARY KEY,RecipeId int,IngredientId int NULL,
 ChildRecipeId int NULL,Quantity decimal(18,3),UnitId int);
 INSERT @DetailSeed
 SELECT 21+c.SortOrder,r.RecipeId,CASE WHEN c.SourceType=N'I' THEN i.IngredientId END,
 CASE WHEN c.SourceType=N'P' THEN ch.RecipeId END,c.Quantity,u.UnitId FROM @Component c
 JOIN dbo.Recipes r ON r.RecipeCode=c.RecipeCode JOIN dbo.Units u ON u.UnitCode=c.UnitCode
 LEFT JOIN dbo.Ingredients i ON c.SourceType=N'I' AND i.Code=c.SourceCode
 LEFT JOIN dbo.PreparedItems p ON c.SourceType=N'P' AND p.Code=c.SourceCode
 LEFT JOIN dbo.Recipes ch ON ch.PreparedItemId=p.PreparedItemId AND ch.Active=1 AND ch.Status=N'Active';

 IF (SELECT COUNT(*) FROM @DetailSeed)<>711
 OR EXISTS(SELECT RecipeId,IngredientId FROM @DetailSeed WHERE IngredientId IS NOT NULL GROUP BY RecipeId,IngredientId HAVING COUNT(*)>1)
 OR EXISTS(SELECT RecipeId,ChildRecipeId FROM @DetailSeed WHERE ChildRecipeId IS NOT NULL GROUP BY RecipeId,ChildRecipeId HAVING COUNT(*)>1)
 OR EXISTS(SELECT 1 FROM @DetailSeed WHERE Quantity<=0 OR NOT((IngredientId IS NOT NULL AND ChildRecipeId IS NULL)
 OR(IngredientId IS NULL AND ChildRecipeId IS NOT NULL)))
  THROW 52310,N'RecipeDetails resolve bị trùng hoặc vi phạm XOR/quantity.',1;

 IF EXISTS(SELECT 1 FROM @DetailSeed x JOIN dbo.RecipeDetails rd ON rd.RecipeDetailId=x.RecipeDetailId
 OR(rd.RecipeId=x.RecipeId AND((rd.IngredientId IS NOT NULL AND rd.IngredientId=x.IngredientId)
 OR(rd.ChildRecipeId IS NOT NULL AND rd.ChildRecipeId=x.ChildRecipeId)))
 WHERE rd.RecipeDetailId<>x.RecipeDetailId OR rd.RecipeId<>x.RecipeId
 OR ISNULL(rd.IngredientId,-1)<>ISNULL(x.IngredientId,-1) OR ISNULL(rd.ChildRecipeId,-1)<>ISNULL(x.ChildRecipeId,-1)
 OR rd.Quantity<>x.Quantity OR rd.UnitId<>x.UnitId)
  THROW 52311,N'RecipeDetails có ID hoặc business source xung đột.',1;

 IF EXISTS(SELECT 1 FROM @DetailSeed x JOIN dbo.Ingredients i ON i.IngredientId=x.IngredientId
 WHERE x.UnitId<>i.BaseUnitId AND NOT EXISTS(SELECT 1 FROM dbo.UnitConversions uc
 WHERE uc.IngredientId=i.IngredientId AND uc.FromUnitId=x.UnitId AND uc.ToUnitId=i.BaseUnitId
 AND uc.Active=1 AND uc.FromQuantity>0 AND uc.ToQuantity>0))
 OR EXISTS(SELECT 1 FROM @DetailSeed x JOIN dbo.Recipes ch ON ch.RecipeId=x.ChildRecipeId
 WHERE ch.OutputUnitId IS NULL OR ch.OutputUnitId<>x.UnitId)
  THROW 52312,N'Unit của BOM không tương thích base/output unit.',1;

 SET IDENTITY_INSERT dbo.RecipeDetails ON;
 INSERT dbo.RecipeDetails(RecipeDetailId,RecipeId,IngredientId,ChildRecipeId,Quantity,UnitId)
 SELECT * FROM @DetailSeed x WHERE NOT EXISTS(SELECT 1 FROM dbo.RecipeDetails rd WHERE rd.RecipeDetailId=x.RecipeDetailId);
 SET IDENTITY_INSERT dbo.RecipeDetails OFF;

 DECLARE @CycleCount int=0;
 ;WITH E AS(SELECT RecipeId,ChildRecipeId FROM dbo.RecipeDetails WHERE ChildRecipeId IS NOT NULL),
 P AS(SELECT RecipeId RootId,ChildRecipeId CurrentId,
 CAST(N'|'+CONVERT(nvarchar(20),RecipeId)+N'|'+CONVERT(nvarchar(20),ChildRecipeId)+N'|' AS nvarchar(max)) Path,
 CAST(IIF(RecipeId=ChildRecipeId,1,0) AS bit) Cy FROM E
 UNION ALL SELECT p.RootId,e.ChildRecipeId,CAST(p.Path+CONVERT(nvarchar(20),e.ChildRecipeId)+N'|' AS nvarchar(max)),
 CAST(IIF(p.Path LIKE N'%|'+CONVERT(nvarchar(20),e.ChildRecipeId)+N'|%',1,0) AS bit)
 FROM P p JOIN E e ON e.RecipeId=p.CurrentId WHERE p.Cy=0)
 SELECT @CycleCount=COUNT(*) FROM P WHERE Cy=1 OPTION(MAXRECURSION 32767);
 IF @CycleCount>0 THROW 52313,N'Phát hiện chu trình trong RecipeDetails.',1;

 DECLARE @PolicyDrinks TABLE(DrinkId int PRIMARY KEY);
 INSERT @PolicyDrinks 
 VALUES
 (10),(14),(34),(36),(37),(38),(39),(40),(41),(42),
 (43),(44),(45),(46),(47),(48),(49),(50),
 (51),(52),(53),(54),(55),(56),(57),(58),(59),(60),
 (61),(62),(63),(64),(65),(66),(67),(68),(69),(70),
 (71),(72),(73),(74),(75),(76),(77),(78),(79),(80);

 DECLARE @PolicySeed TABLE(DrinkSizeToppingPolicyId int PRIMARY KEY,DrinkSizeId int,ToppingId int,
 IsDefaultSelected bit,IsRequired bit,PriceTreatment nvarchar(40),CostTreatment nvarchar(40),
 QuantityPerDrink decimal(18,5),IsActive bit,CreatedByStaffId int,UpdatedByStaffId int NULL,
 CreatedAtUtc datetime2,UpdatedAtUtc datetime2,UNIQUE(DrinkSizeId,ToppingId));

 INSERT @PolicySeed
 SELECT ROW_NUMBER()OVER(ORDER BY ds.DrinkId,ds.SizeId,dt.ToppingId),ds.DrinkSizeId,dt.ToppingId,0,0,
 N'ADD_TOPPING_PRICE',N'ADD_TOPPING_RECIPE_COST',1,1,@ActorStaffId,NULL,'2026-01-01','2026-01-01'
 FROM @PolicyDrinks p JOIN dbo.DrinkSizes ds ON ds.DrinkId=p.DrinkId AND ds.SizeId IN(2,3) AND ds.Active=1
 JOIN dbo.DrinkToppings dt ON dt.DrinkId=p.DrinkId AND dt.Active=1;
 IF (SELECT COUNT(*) FROM @PolicySeed)<>242 THROW 52314,N'Policy canonical phải có đúng 242 dòng.',1;

 IF EXISTS(SELECT 1 FROM @PolicySeed x JOIN dbo.DrinkSizeToppingPolicies p
 ON p.DrinkSizeToppingPolicyId=x.DrinkSizeToppingPolicyId OR(p.DrinkSizeId=x.DrinkSizeId AND p.ToppingId=x.ToppingId)
 WHERE p.DrinkSizeToppingPolicyId<>x.DrinkSizeToppingPolicyId OR p.DrinkSizeId<>x.DrinkSizeId OR p.ToppingId<>x.ToppingId
 OR p.IsDefaultSelected<>x.IsDefaultSelected OR p.IsRequired<>x.IsRequired OR p.PriceTreatment<>x.PriceTreatment
 OR p.CostTreatment<>x.CostTreatment OR p.QuantityPerDrink<>x.QuantityPerDrink OR p.IsActive<>x.IsActive
 OR p.CreatedByStaffId<>x.CreatedByStaffId OR ISNULL(p.UpdatedByStaffId,-1)<>ISNULL(x.UpdatedByStaffId,-1)
 OR p.CreatedAtUtc<>x.CreatedAtUtc OR p.UpdatedAtUtc<>x.UpdatedAtUtc)
  THROW 52315,N'DrinkSizeToppingPolicies có ID hoặc business key xung đột.',1;

 SET IDENTITY_INSERT dbo.DrinkSizeToppingPolicies ON;
 INSERT dbo.DrinkSizeToppingPolicies(DrinkSizeToppingPolicyId,DrinkSizeId,ToppingId,IsDefaultSelected,IsRequired,
 PriceTreatment,CostTreatment,QuantityPerDrink,IsActive,CreatedByStaffId,UpdatedByStaffId,CreatedAtUtc,UpdatedAtUtc)
 SELECT * FROM @PolicySeed x WHERE NOT EXISTS(SELECT 1 FROM dbo.DrinkSizeToppingPolicies p
 WHERE p.DrinkSizeToppingPolicyId=x.DrinkSizeToppingPolicyId);
 SET IDENTITY_INSERT dbo.DrinkSizeToppingPolicies OFF;

 IF (SELECT COUNT(*) FROM dbo.Recipes WHERE RecipeId BETWEEN 1 AND 148)<>148
 OR (SELECT COUNT(*) FROM dbo.RecipeDetails WHERE RecipeDetailId BETWEEN 1 AND 732)<>732
 OR (SELECT COUNT(*) FROM dbo.DrinkSizeToppingPolicies 
    WHERE DrinkSizeToppingPolicyId BETWEEN 1 AND 242)<>242
  THROW 52316,N'Row count cuối Batch 04 không đúng contract.',1;

 IF EXISTS(SELECT RecipeCode FROM dbo.Recipes 
            WHERE RecipeId BETWEEN 1 AND 148 
            GROUP BY RecipeCode 
            HAVING COUNT(*)>1)
 OR EXISTS(SELECT DrinkId,SizeId FROM dbo.Recipes WHERE DrinkId IS NOT NULL AND SizeId IS NOT NULL
 AND ToppingId IS NULL AND Active=1 AND Status=N'Active' GROUP BY DrinkId,SizeId HAVING COUNT(*)>1)
 OR EXISTS(SELECT PreparedItemId FROM dbo.Recipes WHERE PreparedItemId IS NOT NULL AND Active=1 GROUP BY PreparedItemId HAVING COUNT(*)>1)
 OR EXISTS(SELECT ToppingId FROM dbo.Recipes WHERE ToppingId IS NOT NULL AND Active=1 AND Status=N'Active' GROUP BY ToppingId HAVING COUNT(*)>1)
 OR EXISTS(SELECT DrinkSizeId,ToppingId FROM dbo.DrinkSizeToppingPolicies WHERE IsActive=1 GROUP BY DrinkSizeId,ToppingId HAVING COUNT(*)>1)
  THROW 52317,N'Phát hiện duplicate active recipe hoặc topping policy.',1;
 COMMIT;
END TRY
BEGIN CATCH
 BEGIN TRY SET IDENTITY_INSERT dbo.Recipes OFF; END TRY BEGIN CATCH END CATCH;
 BEGIN TRY SET IDENTITY_INSERT dbo.RecipeDetails OFF; END TRY BEGIN CATCH END CATCH;
 BEGIN TRY SET IDENTITY_INSERT dbo.DrinkSizeToppingPolicies OFF; END TRY BEGIN CATCH END CATCH;
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
SeedAllBatch04Complete:
GO

/* BATCH 04 READ-ONLY VERIFICATION */
SELECT N'Recipes' Entity,COUNT(*) TotalRows,MIN(RecipeId) MinId,MAX(RecipeId) MaxId,
SUM(IIF(RecipeId BETWEEN 1 AND 6,1,0)) FoundationRows,SUM(IIF(RecipeId BETWEEN 7 AND 48,1,0)) Store1Rows,
SUM(IIF(RecipeId BETWEEN 49 AND 148,1,0)) ExtensionRows FROM dbo.Recipes
UNION ALL SELECT N'RecipeDetails',COUNT(*),MIN(RecipeDetailId),MAX(RecipeDetailId),
SUM(IIF(RecipeDetailId BETWEEN 1 AND 21,1,0)),SUM(IIF(RecipeDetailId BETWEEN 22 AND 276,1,0)),
SUM(IIF(RecipeDetailId BETWEEN 277 AND 732,1,0)) FROM dbo.RecipeDetails
UNION ALL SELECT N'DrinkSizeToppingPolicies',COUNT(*),MIN(DrinkSizeToppingPolicyId),MAX(DrinkSizeToppingPolicyId),
0,SUM(IIF(DrinkSizeToppingPolicyId BETWEEN 1 AND 40,1,0)),SUM(IIF(DrinkSizeToppingPolicyId BETWEEN 41 AND 242,1,0))
FROM dbo.DrinkSizeToppingPolicies;

SELECT N'Ingredients' [Table],a.CanonicalCode RetainedCode,a.SourceCode RemovedStore1Code,
N'Giữ canonical; RecipeDetails Store1 tham chiếu canonical code.' Decision FROM(VALUES
(N'DEMO_ING_CONDENSED_MILK',N'ING00002'),(N'DEMO_ING_BLACK_TEA',N'ING00003'),
(N'DEMO_ING_SUGAR',N'ING00006'),(N'DEMO_ING_ICE',N'ING00007'),(N'DEMO_ING_MATCHA',N'ING00009'),
(N'DEMO_ING_DAIRY_CREAM',N'ING00010'),(N'DEMO_ING_WATER',N'ING00013'))a(SourceCode,CanonicalCode);

SELECT N'Toppings' [Table],a.CanonicalCode RetainedCode,a.SourceCode RemovedStore1Code,a.Decision FROM(VALUES
(N'DEMO_TOP_BLACK_PEARL',N'TC_DEN',N'Giữ header Store1 Archived; active dùng RCP_TC_DEN.'),
(N'DEMO_TOP_WHITE_PEARL',N'TC_TRANG',N'Giữ header Store1 Archived; active dùng RCP_TC_TRANG.'),
(N'DEMO_TOP_FLAN',N'BH_FLAN',N'Remap BOM vào topping canonical.'),
(N'DEMO_TOP_TARO_JELLY',N'TH_KM',N'Remap BOM vào topping canonical.'),
(N'DEMO_TOP_CHEESE_CREAM',N'KEMCHEESE',N'Remap BOM vào topping canonical.'))a(SourceCode,CanonicalCode,Decision);

/* ============================================================
   BATCH 05/12 - SUPPLIERS AND STORE SCOPE

   Source analysis:
     - Suppliers 1-5, SupplierPhones 1-6 and SupplierContacts 1-5
       belong to EF HasData and remain unchanged.
     - Store1 contributes Suppliers 6-10 with one phone/contact each.
     - IDs 11-50 add 40 suppliers in five meaningful supply groups.
     - SupplierStores 1-50 scope every supplier to Store 1.
   ============================================================ */
IF EXISTS (SELECT 1 FROM dbo.SystemSettings
           WHERE SettingKey=N'seedall_foundation_inventory_v1' AND SettingValue=N'completed')
BEGIN
 PRINT N'SeedAll Batch 05 skipped: foundation inventory v1 is already complete.';
 GOTO SeedAllBatch05Complete;
END;
BEGIN TRY
 BEGIN TRANSACTION;

 IF OBJECT_ID(N'dbo.Suppliers',N'U') IS NULL OR OBJECT_ID(N'dbo.SupplierPhones',N'U') IS NULL
 OR OBJECT_ID(N'dbo.SupplierContacts',N'U') IS NULL OR OBJECT_ID(N'dbo.SupplierStores',N'U') IS NULL
  THROW 52400,N'Schema thiếu bảng bắt buộc của SeedAll Batch 05.',1;

 IF (SELECT COUNT(*) FROM dbo.Suppliers WHERE SupplierId BETWEEN 1 AND 5)<>5
 OR EXISTS(SELECT 1 FROM (VALUES
  (1,N'SUP001',N'Nhà cung cấp A',N'Thành phố Hồ Chí Minh',N'Nhà cung cấp nguyên liệu chính'),
  (2,N'SUP002',N'Nhà cung cấp B',N'TP HCM',N'Nhà cung cấp sữa và kem'),
  (3,N'SUP003',N'Nhà cung cấp C',N'Đồng Nai',N'Nhà cung cấp cà phê'),
  (4,N'SUP004',N'Nhà cung cấp D',N'Hà Nội',N'Nhà cung cấp syrup và trà'),
  (5,N'SUP005',N'Nhà cung cấp E',N'Đà Nẵng',N'Nhà cung cấp matcha')
 )x(SupplierId,Code,Name,Address,Note)
 LEFT JOIN dbo.Suppliers s ON s.SupplierId=x.SupplierId
 WHERE s.SupplierId IS NULL OR s.Code<>x.Code OR s.Name<>x.Name OR s.TaxCode IS NOT NULL
 OR s.Address<>x.Address OR s.Active<>1 OR s.CreatedAt<>'2025-01-01'
 OR s.UpdatedAt<>'2025-01-01' OR s.Note<>x.Note)
  THROW 52401,N'Suppliers EF IDs 1-5 thiếu hoặc khác contract migration.',1;

 IF (SELECT COUNT(*) FROM dbo.SupplierPhones WHERE SupplierPhoneId BETWEEN 1 AND 6)<>6
 OR EXISTS(SELECT 1 FROM (VALUES
 (1,1,N'0901111111',1,N'Hotline'),(2,1,N'0901111112',0,N'Kho hàng'),
 (3,2,N'0902222222',1,N'Hotline'),(4,3,N'0903333333',1,N'Hotline'),
 (5,4,N'0904444444',1,N'Hotline'),(6,5,N'0905555555',1,N'Hotline')
 )x(Id,SupplierId,PhoneNumber,IsPrimary,Description)
 LEFT JOIN dbo.SupplierPhones p ON p.SupplierPhoneId=x.Id
 WHERE p.SupplierPhoneId IS NULL OR p.SupplierId<>x.SupplierId OR p.PhoneNumber<>x.PhoneNumber
 OR p.IsPrimary<>x.IsPrimary OR p.Description<>x.Description)
  THROW 52402,N'SupplierPhones EF IDs 1-6 thiếu hoặc khác contract migration.',1;

 IF (SELECT COUNT(*) FROM dbo.SupplierContacts WHERE SupplierContactId BETWEEN 1 AND 5)<>5
 OR EXISTS(SELECT 1 FROM (VALUES
 (1,1,N'Nguyễn Văn A',N'a@supplier.com',N'0901111111',N'Manager',N'Liên hệ chính'),
 (2,2,N'Trần Văn B',N'b@supplier.com',N'0902222222',N'Sales',N'Phụ trách bán hàng'),
 (3,3,N'Lê Văn C',N'c@supplier.com',N'0903333333',N'Owner',N'Chủ doanh nghiệp'),
 (4,4,N'Phạm Văn D',N'd@supplier.com',N'0904444444',N'Director',N'Giám đốc'),
 (5,5,N'Hoàng Văn E',N'e@supplier.com',N'0905555555',N'Manager',N'Quản lý kinh doanh')
 )x(Id,SupplierId,Name,Email,PhoneNumber,Position,Note)
 LEFT JOIN dbo.SupplierContacts c ON c.SupplierContactId=x.Id
 WHERE c.SupplierContactId IS NULL OR c.SupplierId<>x.SupplierId OR c.Name<>x.Name
 OR c.Email<>x.Email OR c.PhoneNumber<>x.PhoneNumber OR c.Position<>x.Position
 OR c.IsPrimary<>1 OR c.Active<>1 OR c.Note<>x.Note)
  THROW 52403,N'SupplierContacts EF IDs 1-5 thiếu hoặc khác contract migration.',1;

 DECLARE @SupplierSeed TABLE(SupplierId int PRIMARY KEY,Code nvarchar(50) UNIQUE,Name nvarchar(200) UNIQUE,
 TaxCode nvarchar(14) UNIQUE,Address nvarchar(500),Active bit,CreatedAt datetime2,UpdatedAt datetime2,Note nvarchar(1000));
 INSERT @SupplierSeed VALUES
(6,N'DEMO_SUP_COFFEE',N'Nhà cung cấp Cà phê Demo',N'3708888001',N'Thành phố Hồ Chí Minh - dữ liệu demo',1,'2026-01-01','2026-01-01',N'DEMO supplier - không phải dữ liệu doanh nghiệp thật'),
(7,N'DEMO_SUP_DAIRY',N'Nhà cung cấp Sữa & Kem Demo',N'0318888002',N'TP.HCM - dữ liệu demo',1,'2026-01-01','2026-01-01',N'DEMO supplier - không phải dữ liệu doanh nghiệp thật'),
(8,N'DEMO_SUP_PACKAGING',N'Nhà cung cấp Bao bì Demo',N'1108888005-001',N'Tỉnh Tây Ninh - dữ liệu demo',1,'2026-01-01','2026-01-01',N'DEMO supplier - không phải dữ liệu doanh nghiệp thật'),
(9,N'DEMO_SUP_TEA_FRUIT',N'Nhà cung cấp Trà & Trái cây Demo',N'5808888003',N'Lâm Đồng - dữ liệu demo',1,'2026-01-01','2026-01-01',N'DEMO supplier - không phải dữ liệu doanh nghiệp thật'),
(10,N'DEMO_SUP_TOPPING',N'Nhà cung cấp Topping Demo',N'3608888004',N'Đồng Nai - dữ liệu demo',1,'2026-01-01','2026-01-01',N'DEMO supplier - không phải dữ liệu doanh nghiệp thật'),
(11,N'DEMO_SUP_COFFEE_TEA_01',N'Đối tác Demo Cà phê & Trà TP.HCM',N'9000000011',N'TP.HCM - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Cà phê & Trà'),
(12,N'DEMO_SUP_COFFEE_TEA_02',N'Đối tác Demo Cà phê & Trà Bình Dương',N'9000000012',N'Thành phố Hồ Chí Minh - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Cà phê & Trà'),
(13,N'DEMO_SUP_COFFEE_TEA_03',N'Đối tác Demo Cà phê & Trà Đồng Nai',N'9000000013',N'Đồng Nai - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Cà phê & Trà'),
(14,N'DEMO_SUP_COFFEE_TEA_04',N'Đối tác Demo Cà phê & Trà Long An',N'9000000014',N'Tỉnh Tây Ninh - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Cà phê & Trà'),
(15,N'DEMO_SUP_COFFEE_TEA_05',N'Đối tác Demo Cà phê & Trà Lâm Đồng',N'9000000015',N'Lâm Đồng - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Cà phê & Trà'),
(16,N'DEMO_SUP_COFFEE_TEA_06',N'Đối tác Demo Cà phê & Trà Đắk Lắk',N'9000000016',N'Đắk Lắk - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Cà phê & Trà'),
(17,N'DEMO_SUP_COFFEE_TEA_07',N'Đối tác Demo Cà phê & Trà Hà Nội',N'9000000017',N'Hà Nội - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Cà phê & Trà'),
(18,N'DEMO_SUP_COFFEE_TEA_08',N'Đối tác Demo Cà phê & Trà Đà Nẵng',N'9000000018',N'Đà Nẵng - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Cà phê & Trà'),
(19,N'DEMO_SUP_DAIRY_01',N'Đối tác Demo Sữa & Kem TP.HCM',N'9000000019',N'TP.HCM - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Sữa & Kem'),
(20,N'DEMO_SUP_DAIRY_02',N'Đối tác Demo Sữa & Kem Bình Dương',N'9000000020',N'Thành phố Hồ Chí Minh - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Sữa & Kem'),
(21,N'DEMO_SUP_DAIRY_03',N'Đối tác Demo Sữa & Kem Đồng Nai',N'9000000021',N'Đồng Nai - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Sữa & Kem'),
(22,N'DEMO_SUP_DAIRY_04',N'Đối tác Demo Sữa & Kem Long An',N'9000000022',N'Tỉnh Tây Ninh - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Sữa & Kem'),
(23,N'DEMO_SUP_DAIRY_05',N'Đối tác Demo Sữa & Kem Lâm Đồng',N'9000000023',N'Lâm Đồng - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Sữa & Kem'),
(24,N'DEMO_SUP_DAIRY_06',N'Đối tác Demo Sữa & Kem Đắk Lắk',N'9000000024',N'Đắk Lắk - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Sữa & Kem'),
(25,N'DEMO_SUP_DAIRY_07',N'Đối tác Demo Sữa & Kem Hà Nội',N'9000000025',N'Hà Nội - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Sữa & Kem'),
(26,N'DEMO_SUP_DAIRY_08',N'Đối tác Demo Sữa & Kem Đà Nẵng',N'9000000026',N'Đà Nẵng - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Sữa & Kem'),
(27,N'DEMO_SUP_FRUIT_01',N'Đối tác Demo Trái cây TP.HCM',N'9000000027',N'TP.HCM - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Trái cây'),
(28,N'DEMO_SUP_FRUIT_02',N'Đối tác Demo Trái cây Bình Dương',N'9000000028',N'Thành phố Hồ Chí Minh - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Trái cây'),
(29,N'DEMO_SUP_FRUIT_03',N'Đối tác Demo Trái cây Đồng Nai',N'9000000029',N'Đồng Nai - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Trái cây'),
(30,N'DEMO_SUP_FRUIT_04',N'Đối tác Demo Trái cây Long An',N'9000000030',N'Tỉnh Tây Ninh - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Trái cây'),
(31,N'DEMO_SUP_FRUIT_05',N'Đối tác Demo Trái cây Lâm Đồng',N'9000000031',N'Lâm Đồng - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Trái cây'),
(32,N'DEMO_SUP_FRUIT_06',N'Đối tác Demo Trái cây Đắk Lắk',N'9000000032',N'Đắk Lắk - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Trái cây'),
(33,N'DEMO_SUP_FRUIT_07',N'Đối tác Demo Trái cây Hà Nội',N'9000000033',N'Hà Nội - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Trái cây'),
(34,N'DEMO_SUP_FRUIT_08',N'Đối tác Demo Trái cây Đà Nẵng',N'9000000034',N'Đà Nẵng - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Trái cây'),
(35,N'DEMO_SUP_TOPPING_SYRUP_01',N'Đối tác Demo Topping & Syrup TP.HCM',N'9000000035',N'TP.HCM - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Topping & Syrup'),
(36,N'DEMO_SUP_TOPPING_SYRUP_02',N'Đối tác Demo Topping & Syrup Bình Dương',N'9000000036',N'Thành phố Hồ Chí Minh - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Topping & Syrup'),
(37,N'DEMO_SUP_TOPPING_SYRUP_03',N'Đối tác Demo Topping & Syrup Đồng Nai',N'9000000037',N'Đồng Nai - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Topping & Syrup'),
(38,N'DEMO_SUP_TOPPING_SYRUP_04',N'Đối tác Demo Topping & Syrup Long An',N'9000000038',N'Tỉnh Tây Ninh - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Topping & Syrup'),
(39,N'DEMO_SUP_TOPPING_SYRUP_05',N'Đối tác Demo Topping & Syrup Lâm Đồng',N'9000000039',N'Lâm Đồng - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Topping & Syrup'),
(40,N'DEMO_SUP_TOPPING_SYRUP_06',N'Đối tác Demo Topping & Syrup Đắk Lắk',N'9000000040',N'Đắk Lắk - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Topping & Syrup'),
(41,N'DEMO_SUP_TOPPING_SYRUP_07',N'Đối tác Demo Topping & Syrup Hà Nội',N'9000000041',N'Hà Nội - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Topping & Syrup'),
(42,N'DEMO_SUP_TOPPING_SYRUP_08',N'Đối tác Demo Topping & Syrup Đà Nẵng',N'9000000042',N'Đà Nẵng - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Topping & Syrup'),
(43,N'DEMO_SUP_PACKAGING_01',N'Đối tác Demo Bao bì TP.HCM',N'9000000043',N'TP.HCM - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Bao bì'),
(44,N'DEMO_SUP_PACKAGING_02',N'Đối tác Demo Bao bì Bình Dương',N'9000000044',N'Thành phố Hồ Chí Minh - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Bao bì'),
(45,N'DEMO_SUP_PACKAGING_03',N'Đối tác Demo Bao bì Đồng Nai',N'9000000045',N'Đồng Nai - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Bao bì'),
(46,N'DEMO_SUP_PACKAGING_04',N'Đối tác Demo Bao bì Long An',N'9000000046',N'Tỉnh Tây Ninh - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Bao bì'),
(47,N'DEMO_SUP_PACKAGING_05',N'Đối tác Demo Bao bì Lâm Đồng',N'9000000047',N'Lâm Đồng - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Bao bì'),
(48,N'DEMO_SUP_PACKAGING_06',N'Đối tác Demo Bao bì Đắk Lắk',N'9000000048',N'Đắk Lắk - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Bao bì'),
(49,N'DEMO_SUP_PACKAGING_07',N'Đối tác Demo Bao bì Hà Nội',N'9000000049',N'Hà Nội - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Bao bì'),
(50,N'DEMO_SUP_PACKAGING_08',N'Đối tác Demo Bao bì Đà Nẵng',N'9000000050',N'Đà Nẵng - dữ liệu demo',1,'2026-01-01','2026-01-01',N'Nhóm cung ứng demo: Bao bì');

 IF (SELECT COUNT(*) FROM @SupplierSeed)<>45
 OR EXISTS(SELECT 1 FROM @SupplierSeed WHERE Active<>1 OR TaxCode IS NULL OR
 NOT((LEN(TaxCode)=10 AND TaxCode NOT LIKE N'%[^0-9]%')
 OR(LEN(TaxCode)=14 AND SUBSTRING(TaxCode,11,1)=N'-'
 AND REPLACE(TaxCode,N'-',N'') NOT LIKE N'%[^0-9]%')))
 THROW 52404,N'Bộ Supplier mới sai số lượng hoặc TaxCode không canonical.',1;

 UPDATE s
 SET s.Address=x.Address
 FROM dbo.Suppliers s
 JOIN @SupplierSeed x ON x.SupplierId=s.SupplierId AND x.Code=s.Code AND x.TaxCode=s.TaxCode AND x.Name=s.Name
 WHERE (s.Address=N'Bình Dương - dữ liệu demo' AND x.Address=N'Thành phố Hồ Chí Minh - dữ liệu demo')
    OR (s.Address=N'Long An - dữ liệu demo' AND x.Address=N'Tỉnh Tây Ninh - dữ liệu demo');

 IF EXISTS(SELECT 1 FROM @SupplierSeed x JOIN dbo.Suppliers s
 ON s.SupplierId=x.SupplierId OR s.Code=x.Code OR s.TaxCode=x.TaxCode OR s.Name=x.Name
 WHERE s.SupplierId<>x.SupplierId OR s.Code<>x.Code OR s.Name<>x.Name OR s.TaxCode<>x.TaxCode
 OR s.Address<>x.Address OR s.Active<>x.Active OR s.CreatedAt<>x.CreatedAt
 OR s.UpdatedAt<>x.UpdatedAt OR s.Note<>x.Note)
  THROW 52405,N'Suppliers có ID, Code, TaxCode hoặc Name xung đột.',1;

 SET IDENTITY_INSERT dbo.Suppliers ON;
 INSERT dbo.Suppliers(SupplierId,Code,Name,TaxCode,Address,Active,CreatedAt,UpdatedAt,Note)
 SELECT SupplierId,Code,Name,TaxCode,Address,Active,CreatedAt,UpdatedAt,Note FROM @SupplierSeed x
 WHERE NOT EXISTS(SELECT 1 FROM dbo.Suppliers s WHERE s.SupplierId=x.SupplierId);
 SET IDENTITY_INSERT dbo.Suppliers OFF;

 DECLARE @PhoneSeed TABLE(SupplierPhoneId int PRIMARY KEY,SupplierId int,PhoneNumber nvarchar(20),
 IsPrimary bit,Description nvarchar(200),UNIQUE(SupplierId,PhoneNumber));
 INSERT @PhoneSeed VALUES
(7,6,N'0901000001',1,N'Hotline demo'),
(8,7,N'0901000002',1,N'Hotline demo'),
(9,8,N'0901000005',1,N'Hotline demo'),
(10,9,N'0901000003',1,N'Hotline demo'),
(11,10,N'0901000004',1,N'Hotline demo'),
(12,11,N'0980000011',1,N'Hotline đối tác demo'),
(13,12,N'0980000012',1,N'Hotline đối tác demo'),
(14,13,N'0980000013',1,N'Hotline đối tác demo'),
(15,14,N'0980000014',1,N'Hotline đối tác demo'),
(16,15,N'0980000015',1,N'Hotline đối tác demo'),
(17,16,N'0980000016',1,N'Hotline đối tác demo'),
(18,17,N'0980000017',1,N'Hotline đối tác demo'),
(19,18,N'0980000018',1,N'Hotline đối tác demo'),
(20,19,N'0980000019',1,N'Hotline đối tác demo'),
(21,20,N'0980000020',1,N'Hotline đối tác demo'),
(22,21,N'0980000021',1,N'Hotline đối tác demo'),
(23,22,N'0980000022',1,N'Hotline đối tác demo'),
(24,23,N'0980000023',1,N'Hotline đối tác demo'),
(25,24,N'0980000024',1,N'Hotline đối tác demo'),
(26,25,N'0980000025',1,N'Hotline đối tác demo'),
(27,26,N'0980000026',1,N'Hotline đối tác demo'),
(28,27,N'0980000027',1,N'Hotline đối tác demo'),
(29,28,N'0980000028',1,N'Hotline đối tác demo'),
(30,29,N'0980000029',1,N'Hotline đối tác demo'),
(31,30,N'0980000030',1,N'Hotline đối tác demo'),
(32,31,N'0980000031',1,N'Hotline đối tác demo'),
(33,32,N'0980000032',1,N'Hotline đối tác demo'),
(34,33,N'0980000033',1,N'Hotline đối tác demo'),
(35,34,N'0980000034',1,N'Hotline đối tác demo'),
(36,35,N'0980000035',1,N'Hotline đối tác demo'),
(37,36,N'0980000036',1,N'Hotline đối tác demo'),
(38,37,N'0980000037',1,N'Hotline đối tác demo'),
(39,38,N'0980000038',1,N'Hotline đối tác demo'),
(40,39,N'0980000039',1,N'Hotline đối tác demo'),
(41,40,N'0980000040',1,N'Hotline đối tác demo'),
(42,41,N'0980000041',1,N'Hotline đối tác demo'),
(43,42,N'0980000042',1,N'Hotline đối tác demo'),
(44,43,N'0980000043',1,N'Hotline đối tác demo'),
(45,44,N'0980000044',1,N'Hotline đối tác demo'),
(46,45,N'0980000045',1,N'Hotline đối tác demo'),
(47,46,N'0980000046',1,N'Hotline đối tác demo'),
(48,47,N'0980000047',1,N'Hotline đối tác demo'),
(49,48,N'0980000048',1,N'Hotline đối tác demo'),
(50,49,N'0980000049',1,N'Hotline đối tác demo'),
(51,50,N'0980000050',1,N'Hotline đối tác demo');

 IF (SELECT COUNT(*) FROM @PhoneSeed)<>45
 OR EXISTS(SELECT 1 FROM @PhoneSeed WHERE LEN(PhoneNumber)<>10 OR PhoneNumber LIKE N'%[^0-9]%' OR IsPrimary<>1)
  THROW 52406,N'Bộ SupplierPhone mới sai số lượng hoặc format.',1;

 IF EXISTS(SELECT 1 FROM @PhoneSeed x LEFT JOIN dbo.Suppliers s ON s.SupplierId=x.SupplierId WHERE s.SupplierId IS NULL)
  THROW 52407,N'SupplierPhone tham chiếu Supplier không tồn tại.',1;

 IF EXISTS(SELECT 1 FROM @PhoneSeed x JOIN dbo.SupplierPhones p
 ON p.SupplierPhoneId=x.SupplierPhoneId OR(p.SupplierId=x.SupplierId AND p.PhoneNumber=x.PhoneNumber)
 WHERE p.SupplierPhoneId<>x.SupplierPhoneId OR p.SupplierId<>x.SupplierId
 OR p.PhoneNumber<>x.PhoneNumber OR p.IsPrimary<>x.IsPrimary OR p.Description<>x.Description)
 OR EXISTS(SELECT 1 FROM @PhoneSeed x JOIN dbo.SupplierPhones p
 ON p.SupplierId=x.SupplierId AND p.IsPrimary=1 WHERE p.SupplierPhoneId<>x.SupplierPhoneId)
  THROW 52408,N'SupplierPhones có ID/business key hoặc primary phone xung đột.',1;

 SET IDENTITY_INSERT dbo.SupplierPhones ON;
 INSERT dbo.SupplierPhones(SupplierPhoneId,SupplierId,PhoneNumber,IsPrimary,Description)
 SELECT * FROM @PhoneSeed x WHERE NOT EXISTS(SELECT 1 FROM dbo.SupplierPhones p WHERE p.SupplierPhoneId=x.SupplierPhoneId);
 SET IDENTITY_INSERT dbo.SupplierPhones OFF;

 DECLARE @ContactSeed TABLE(SupplierContactId int PRIMARY KEY,SupplierId int,Name nvarchar(150),
 Email nvarchar(150),PhoneNumber nvarchar(20),Position nvarchar(100),IsPrimary bit,Active bit,Note nvarchar(1000),
 UNIQUE(SupplierId,Email));
 INSERT @ContactSeed VALUES
(6,6,N'Liên hệ Demo',N'coffee.demo@example.invalid',N'0901000001',N'Điều phối demo',1,1,N'Dữ liệu demo, không gửi email thật'),
(7,7,N'Liên hệ Demo',N'dairy.demo@example.invalid',N'0901000002',N'Điều phối demo',1,1,N'Dữ liệu demo, không gửi email thật'),
(8,8,N'Liên hệ Demo',N'packaging.demo@example.invalid',N'0901000005',N'Điều phối demo',1,1,N'Dữ liệu demo, không gửi email thật'),
(9,9,N'Liên hệ Demo',N'tea.demo@example.invalid',N'0901000003',N'Điều phối demo',1,1,N'Dữ liệu demo, không gửi email thật'),
(10,10,N'Liên hệ Demo',N'topping.demo@example.invalid',N'0901000004',N'Điều phối demo',1,1,N'Dữ liệu demo, không gửi email thật'),
(11,11,N'Điều phối viên Demo 11',N'supplier11@cafechain.invalid',N'0980000011',N'Điều phối Cà phê & Trà',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(12,12,N'Điều phối viên Demo 12',N'supplier12@cafechain.invalid',N'0980000012',N'Điều phối Cà phê & Trà',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(13,13,N'Điều phối viên Demo 13',N'supplier13@cafechain.invalid',N'0980000013',N'Điều phối Cà phê & Trà',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(14,14,N'Điều phối viên Demo 14',N'supplier14@cafechain.invalid',N'0980000014',N'Điều phối Cà phê & Trà',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(15,15,N'Điều phối viên Demo 15',N'supplier15@cafechain.invalid',N'0980000015',N'Điều phối Cà phê & Trà',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(16,16,N'Điều phối viên Demo 16',N'supplier16@cafechain.invalid',N'0980000016',N'Điều phối Cà phê & Trà',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(17,17,N'Điều phối viên Demo 17',N'supplier17@cafechain.invalid',N'0980000017',N'Điều phối Cà phê & Trà',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(18,18,N'Điều phối viên Demo 18',N'supplier18@cafechain.invalid',N'0980000018',N'Điều phối Cà phê & Trà',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(19,19,N'Điều phối viên Demo 19',N'supplier19@cafechain.invalid',N'0980000019',N'Điều phối Sữa & Kem',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(20,20,N'Điều phối viên Demo 20',N'supplier20@cafechain.invalid',N'0980000020',N'Điều phối Sữa & Kem',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(21,21,N'Điều phối viên Demo 21',N'supplier21@cafechain.invalid',N'0980000021',N'Điều phối Sữa & Kem',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(22,22,N'Điều phối viên Demo 22',N'supplier22@cafechain.invalid',N'0980000022',N'Điều phối Sữa & Kem',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(23,23,N'Điều phối viên Demo 23',N'supplier23@cafechain.invalid',N'0980000023',N'Điều phối Sữa & Kem',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(24,24,N'Điều phối viên Demo 24',N'supplier24@cafechain.invalid',N'0980000024',N'Điều phối Sữa & Kem',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(25,25,N'Điều phối viên Demo 25',N'supplier25@cafechain.invalid',N'0980000025',N'Điều phối Sữa & Kem',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(26,26,N'Điều phối viên Demo 26',N'supplier26@cafechain.invalid',N'0980000026',N'Điều phối Sữa & Kem',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(27,27,N'Điều phối viên Demo 27',N'supplier27@cafechain.invalid',N'0980000027',N'Điều phối Trái cây',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(28,28,N'Điều phối viên Demo 28',N'supplier28@cafechain.invalid',N'0980000028',N'Điều phối Trái cây',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(29,29,N'Điều phối viên Demo 29',N'supplier29@cafechain.invalid',N'0980000029',N'Điều phối Trái cây',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(30,30,N'Điều phối viên Demo 30',N'supplier30@cafechain.invalid',N'0980000030',N'Điều phối Trái cây',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(31,31,N'Điều phối viên Demo 31',N'supplier31@cafechain.invalid',N'0980000031',N'Điều phối Trái cây',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(32,32,N'Điều phối viên Demo 32',N'supplier32@cafechain.invalid',N'0980000032',N'Điều phối Trái cây',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(33,33,N'Điều phối viên Demo 33',N'supplier33@cafechain.invalid',N'0980000033',N'Điều phối Trái cây',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(34,34,N'Điều phối viên Demo 34',N'supplier34@cafechain.invalid',N'0980000034',N'Điều phối Trái cây',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(35,35,N'Điều phối viên Demo 35',N'supplier35@cafechain.invalid',N'0980000035',N'Điều phối Topping & Syrup',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(36,36,N'Điều phối viên Demo 36',N'supplier36@cafechain.invalid',N'0980000036',N'Điều phối Topping & Syrup',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(37,37,N'Điều phối viên Demo 37',N'supplier37@cafechain.invalid',N'0980000037',N'Điều phối Topping & Syrup',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(38,38,N'Điều phối viên Demo 38',N'supplier38@cafechain.invalid',N'0980000038',N'Điều phối Topping & Syrup',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(39,39,N'Điều phối viên Demo 39',N'supplier39@cafechain.invalid',N'0980000039',N'Điều phối Topping & Syrup',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(40,40,N'Điều phối viên Demo 40',N'supplier40@cafechain.invalid',N'0980000040',N'Điều phối Topping & Syrup',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(41,41,N'Điều phối viên Demo 41',N'supplier41@cafechain.invalid',N'0980000041',N'Điều phối Topping & Syrup',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(42,42,N'Điều phối viên Demo 42',N'supplier42@cafechain.invalid',N'0980000042',N'Điều phối Topping & Syrup',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(43,43,N'Điều phối viên Demo 43',N'supplier43@cafechain.invalid',N'0980000043',N'Điều phối Bao bì',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(44,44,N'Điều phối viên Demo 44',N'supplier44@cafechain.invalid',N'0980000044',N'Điều phối Bao bì',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(45,45,N'Điều phối viên Demo 45',N'supplier45@cafechain.invalid',N'0980000045',N'Điều phối Bao bì',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(46,46,N'Điều phối viên Demo 46',N'supplier46@cafechain.invalid',N'0980000046',N'Điều phối Bao bì',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(47,47,N'Điều phối viên Demo 47',N'supplier47@cafechain.invalid',N'0980000047',N'Điều phối Bao bì',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(48,48,N'Điều phối viên Demo 48',N'supplier48@cafechain.invalid',N'0980000048',N'Điều phối Bao bì',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(49,49,N'Điều phối viên Demo 49',N'supplier49@cafechain.invalid',N'0980000049',N'Điều phối Bao bì',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế'),
(50,50,N'Điều phối viên Demo 50',N'supplier50@cafechain.invalid',N'0980000050',N'Điều phối Bao bì',1,1,N'Dữ liệu kiểm thử, không liên hệ thực tế');

 IF (SELECT COUNT(*) FROM @ContactSeed)<>45
 OR EXISTS(SELECT 1 FROM @ContactSeed WHERE Email NOT LIKE N'%@%.invalid' OR LEN(PhoneNumber)<>10
 OR PhoneNumber LIKE N'%[^0-9]%' OR IsPrimary<>1 OR Active<>1)
  THROW 52409,N'Bộ SupplierContact mới sai số lượng hoặc format.',1;

 IF EXISTS(SELECT 1 FROM @ContactSeed x LEFT JOIN dbo.Suppliers s ON s.SupplierId=x.SupplierId WHERE s.SupplierId IS NULL)
  THROW 52410,N'SupplierContact tham chiếu Supplier không tồn tại.',1;

 IF EXISTS(SELECT 1 FROM @ContactSeed x JOIN dbo.SupplierContacts c
 ON c.SupplierContactId=x.SupplierContactId OR(c.SupplierId=x.SupplierId AND c.Email=x.Email)
 WHERE c.SupplierContactId<>x.SupplierContactId OR c.SupplierId<>x.SupplierId OR c.Name<>x.Name
 OR c.Email<>x.Email OR c.PhoneNumber<>x.PhoneNumber OR c.Position<>x.Position
 OR c.IsPrimary<>x.IsPrimary OR c.Active<>x.Active OR c.Note<>x.Note)
 OR EXISTS(SELECT 1 FROM @ContactSeed x JOIN dbo.SupplierContacts c
 ON c.SupplierId=x.SupplierId AND c.IsPrimary=1 AND c.Active=1 WHERE c.SupplierContactId<>x.SupplierContactId)
  THROW 52411,N'SupplierContacts có ID/business key hoặc primary contact xung đột.',1;

 SET IDENTITY_INSERT dbo.SupplierContacts ON;
 INSERT dbo.SupplierContacts(SupplierContactId,SupplierId,Name,Email,PhoneNumber,Position,IsPrimary,Active,Note)
 SELECT * FROM @ContactSeed x WHERE NOT EXISTS(SELECT 1 FROM dbo.SupplierContacts c WHERE c.SupplierContactId=x.SupplierContactId);
 SET IDENTITY_INSERT dbo.SupplierContacts OFF;

 DECLARE @StoreSeed TABLE(SupplierStoreId int PRIMARY KEY,SupplierId int UNIQUE,StoreId int,Active bit,
 LeadTimeOverrideDays int,DeliverySchedule nvarchar(300),Note nvarchar(1000),CreatedAt datetime2,UpdatedAt datetime2,
 UNIQUE(SupplierId,StoreId));
 INSERT @StoreSeed VALUES
(1,1,1,1,2,N'Thứ 2-4-6',N'SEEDALL_FOUNDATION_SCOPE','2026-01-01','2026-01-01'),
(2,2,1,1,2,N'Hằng ngày',N'SEEDALL_FOUNDATION_SCOPE','2026-01-01','2026-01-01'),
(3,3,1,1,3,N'Thứ 2-5',N'SEEDALL_FOUNDATION_SCOPE','2026-01-01','2026-01-01'),
(4,4,1,1,5,N'Thứ 3-6',N'SEEDALL_FOUNDATION_SCOPE','2026-01-01','2026-01-01'),
(5,5,1,1,5,N'Thứ 4-7',N'SEEDALL_FOUNDATION_SCOPE','2026-01-01','2026-01-01'),
(6,6,1,1,3,N'Lịch giao demo: Thứ 2-4-6',N'DEMO_SUPPLIER_STORE_1','2026-01-01','2026-01-01'),
(7,7,1,1,2,N'Lịch giao demo: Thứ 2-4-6',N'DEMO_SUPPLIER_STORE_1','2026-01-01','2026-01-01'),
(8,8,1,1,2,N'Lịch giao demo: Thứ 2-4-6',N'DEMO_SUPPLIER_STORE_1','2026-01-01','2026-01-01'),
(9,9,1,1,3,N'Lịch giao demo: Thứ 2-4-6',N'DEMO_SUPPLIER_STORE_1','2026-01-01','2026-01-01'),
(10,10,1,1,4,N'Lịch giao demo: Thứ 2-4-6',N'DEMO_SUPPLIER_STORE_1','2026-01-01','2026-01-01'),
(11,11,1,1,3,N'Thứ 2-4-6',N'Phạm vi Store 1 - Cà phê & Trà','2026-01-01','2026-01-01'),
(12,12,1,1,4,N'Thứ 2-4-6',N'Phạm vi Store 1 - Cà phê & Trà','2026-01-01','2026-01-01'),
(13,13,1,1,3,N'Thứ 2-4-6',N'Phạm vi Store 1 - Cà phê & Trà','2026-01-01','2026-01-01'),
(14,14,1,1,5,N'Thứ 2-4-6',N'Phạm vi Store 1 - Cà phê & Trà','2026-01-01','2026-01-01'),
(15,15,1,1,4,N'Thứ 2-4-6',N'Phạm vi Store 1 - Cà phê & Trà','2026-01-01','2026-01-01'),
(16,16,1,1,2,N'Thứ 2-4-6',N'Phạm vi Store 1 - Cà phê & Trà','2026-01-01','2026-01-01'),
(17,17,1,1,3,N'Thứ 2-4-6',N'Phạm vi Store 1 - Cà phê & Trà','2026-01-01','2026-01-01'),
(18,18,1,1,5,N'Thứ 2-4-6',N'Phạm vi Store 1 - Cà phê & Trà','2026-01-01','2026-01-01'),
(19,19,1,1,2,N'Hằng ngày',N'Phạm vi Store 1 - Sữa & Kem','2026-01-01','2026-01-01'),
(20,20,1,1,2,N'Hằng ngày',N'Phạm vi Store 1 - Sữa & Kem','2026-01-01','2026-01-01'),
(21,21,1,1,3,N'Hằng ngày',N'Phạm vi Store 1 - Sữa & Kem','2026-01-01','2026-01-01'),
(22,22,1,1,2,N'Hằng ngày',N'Phạm vi Store 1 - Sữa & Kem','2026-01-01','2026-01-01'),
(23,23,1,1,4,N'Hằng ngày',N'Phạm vi Store 1 - Sữa & Kem','2026-01-01','2026-01-01'),
(24,24,1,1,3,N'Hằng ngày',N'Phạm vi Store 1 - Sữa & Kem','2026-01-01','2026-01-01'),
(25,25,1,1,2,N'Hằng ngày',N'Phạm vi Store 1 - Sữa & Kem','2026-01-01','2026-01-01'),
(26,26,1,1,4,N'Hằng ngày',N'Phạm vi Store 1 - Sữa & Kem','2026-01-01','2026-01-01'),
(27,27,1,1,2,N'Thứ 3-5-7',N'Phạm vi Store 1 - Trái cây','2026-01-01','2026-01-01'),
(28,28,1,1,3,N'Thứ 3-5-7',N'Phạm vi Store 1 - Trái cây','2026-01-01','2026-01-01'),
(29,29,1,1,2,N'Thứ 3-5-7',N'Phạm vi Store 1 - Trái cây','2026-01-01','2026-01-01'),
(30,30,1,1,3,N'Thứ 3-5-7',N'Phạm vi Store 1 - Trái cây','2026-01-01','2026-01-01'),
(31,31,1,1,4,N'Thứ 3-5-7',N'Phạm vi Store 1 - Trái cây','2026-01-01','2026-01-01'),
(32,32,1,1,2,N'Thứ 3-5-7',N'Phạm vi Store 1 - Trái cây','2026-01-01','2026-01-01'),
(33,33,1,1,3,N'Thứ 3-5-7',N'Phạm vi Store 1 - Trái cây','2026-01-01','2026-01-01'),
(34,34,1,1,4,N'Thứ 3-5-7',N'Phạm vi Store 1 - Trái cây','2026-01-01','2026-01-01'),
(35,35,1,1,3,N'Thứ 2-5',N'Phạm vi Store 1 - Topping & Syrup','2026-01-01','2026-01-01'),
(36,36,1,1,4,N'Thứ 2-5',N'Phạm vi Store 1 - Topping & Syrup','2026-01-01','2026-01-01'),
(37,37,1,1,3,N'Thứ 2-5',N'Phạm vi Store 1 - Topping & Syrup','2026-01-01','2026-01-01'),
(38,38,1,1,5,N'Thứ 2-5',N'Phạm vi Store 1 - Topping & Syrup','2026-01-01','2026-01-01'),
(39,39,1,1,4,N'Thứ 2-5',N'Phạm vi Store 1 - Topping & Syrup','2026-01-01','2026-01-01'),
(40,40,1,1,3,N'Thứ 2-5',N'Phạm vi Store 1 - Topping & Syrup','2026-01-01','2026-01-01'),
(41,41,1,1,5,N'Thứ 2-5',N'Phạm vi Store 1 - Topping & Syrup','2026-01-01','2026-01-01'),
(42,42,1,1,4,N'Thứ 2-5',N'Phạm vi Store 1 - Topping & Syrup','2026-01-01','2026-01-01'),
(43,43,1,1,2,N'Thứ 3-6',N'Phạm vi Store 1 - Bao bì','2026-01-01','2026-01-01'),
(44,44,1,1,3,N'Thứ 3-6',N'Phạm vi Store 1 - Bao bì','2026-01-01','2026-01-01'),
(45,45,1,1,2,N'Thứ 3-6',N'Phạm vi Store 1 - Bao bì','2026-01-01','2026-01-01'),
(46,46,1,1,4,N'Thứ 3-6',N'Phạm vi Store 1 - Bao bì','2026-01-01','2026-01-01'),
(47,47,1,1,3,N'Thứ 3-6',N'Phạm vi Store 1 - Bao bì','2026-01-01','2026-01-01'),
(48,48,1,1,2,N'Thứ 3-6',N'Phạm vi Store 1 - Bao bì','2026-01-01','2026-01-01'),
(49,49,1,1,4,N'Thứ 3-6',N'Phạm vi Store 1 - Bao bì','2026-01-01','2026-01-01'),
(50,50,1,1,3,N'Thứ 3-6',N'Phạm vi Store 1 - Bao bì','2026-01-01','2026-01-01');

 IF (SELECT COUNT(*) FROM @StoreSeed)<>50 OR EXISTS(SELECT 1 FROM @StoreSeed
 WHERE StoreId<>1 OR Active<>1 OR LeadTimeOverrideDays<0)
  THROW 52412,N'Bộ SupplierStore phải có đúng 50 phạm vi active tại Store 1.',1;

 IF NOT EXISTS(SELECT 1 FROM dbo.Stores WHERE StoreId=1)
 OR EXISTS(SELECT 1 FROM @StoreSeed x LEFT JOIN dbo.Suppliers s ON s.SupplierId=x.SupplierId WHERE s.SupplierId IS NULL)
  THROW 52413,N'SupplierStore tham chiếu Store hoặc Supplier không tồn tại.',1;

 IF EXISTS(SELECT 1 FROM @StoreSeed x JOIN dbo.SupplierStores ss
 ON ss.SupplierStoreId=x.SupplierStoreId OR(ss.SupplierId=x.SupplierId AND ss.StoreId=x.StoreId)
 WHERE ss.SupplierStoreId<>x.SupplierStoreId OR ss.SupplierId<>x.SupplierId OR ss.StoreId<>x.StoreId
 OR ss.Active<>x.Active OR ISNULL(ss.LeadTimeOverrideDays,-1)<>x.LeadTimeOverrideDays
 OR ss.DeliverySchedule<>x.DeliverySchedule OR ss.Note<>x.Note
 OR ss.CreatedAt<>x.CreatedAt OR ss.UpdatedAt<>x.UpdatedAt)
  THROW 52414,N'SupplierStores có ID hoặc business key xung đột.',1;

 SET IDENTITY_INSERT dbo.SupplierStores ON;
 INSERT dbo.SupplierStores(SupplierStoreId,SupplierId,StoreId,Active,LeadTimeOverrideDays,
 DeliverySchedule,Note,CreatedAt,UpdatedAt)
 SELECT * FROM @StoreSeed x WHERE NOT EXISTS(SELECT 1 FROM dbo.SupplierStores ss WHERE ss.SupplierStoreId=x.SupplierStoreId);
 SET IDENTITY_INSERT dbo.SupplierStores OFF;

 IF (SELECT COUNT(*) FROM dbo.Suppliers)<>50 OR(SELECT COUNT(*) FROM dbo.SupplierPhones)<>51
 OR(SELECT COUNT(*) FROM dbo.SupplierContacts)<>50 OR(SELECT COUNT(*) FROM dbo.SupplierStores)<50
  THROW 52415,N'Row count cuối Batch 05 không đúng contract.',1;

 IF EXISTS(SELECT Code FROM dbo.Suppliers GROUP BY Code HAVING COUNT(*)>1)
 OR EXISTS(SELECT TaxCode FROM dbo.Suppliers WHERE TaxCode IS NOT NULL GROUP BY TaxCode HAVING COUNT(*)>1)
 OR EXISTS(SELECT SupplierId,PhoneNumber FROM dbo.SupplierPhones GROUP BY SupplierId,PhoneNumber HAVING COUNT(*)>1)
 OR EXISTS(SELECT SupplierId,Email FROM dbo.SupplierContacts WHERE Email IS NOT NULL GROUP BY SupplierId,Email HAVING COUNT(*)>1)
 OR EXISTS(SELECT SupplierId,StoreId FROM dbo.SupplierStores GROUP BY SupplierId,StoreId HAVING COUNT(*)>1)
 OR EXISTS(SELECT SupplierId FROM dbo.SupplierPhones WHERE IsPrimary=1 GROUP BY SupplierId HAVING COUNT(*)<>1)
 OR EXISTS(SELECT SupplierId FROM dbo.SupplierContacts WHERE IsPrimary=1 AND Active=1 GROUP BY SupplierId HAVING COUNT(*)<>1)
 OR(SELECT COUNT(DISTINCT SupplierId) FROM dbo.SupplierPhones WHERE IsPrimary=1)<>50
 OR(SELECT COUNT(DISTINCT SupplierId) FROM dbo.SupplierContacts WHERE IsPrimary=1 AND Active=1)<>50
  THROW 52416,N'Phát hiện duplicate hoặc số primary phone/contact không đúng.',1;

 COMMIT;
END TRY
BEGIN CATCH
 BEGIN TRY SET IDENTITY_INSERT dbo.Suppliers OFF; END TRY BEGIN CATCH END CATCH;
 BEGIN TRY SET IDENTITY_INSERT dbo.SupplierPhones OFF; END TRY BEGIN CATCH END CATCH;
 BEGIN TRY SET IDENTITY_INSERT dbo.SupplierContacts OFF; END TRY BEGIN CATCH END CATCH;
 BEGIN TRY SET IDENTITY_INSERT dbo.SupplierStores OFF; END TRY BEGIN CATCH END CATCH;
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
SeedAllBatch05Complete:
GO

/* BATCH 05 READ-ONLY VERIFICATION */
SELECT N'Suppliers' Entity,COUNT(*) TotalRows,MIN(SupplierId) MinId,MAX(SupplierId) MaxId,
SUM(IIF(SupplierId BETWEEN 1 AND 5,1,0)) FoundationRows,
SUM(IIF(SupplierId BETWEEN 6 AND 10,1,0)) Store1Rows,
SUM(IIF(SupplierId BETWEEN 11 AND 50,1,0)) ExtensionRows FROM dbo.Suppliers
UNION ALL SELECT N'SupplierPhones',COUNT(*),MIN(SupplierPhoneId),MAX(SupplierPhoneId),
SUM(IIF(SupplierPhoneId BETWEEN 1 AND 6,1,0)),SUM(IIF(SupplierPhoneId BETWEEN 7 AND 11,1,0)),
SUM(IIF(SupplierPhoneId BETWEEN 12 AND 51,1,0)) FROM dbo.SupplierPhones
UNION ALL SELECT N'SupplierContacts',COUNT(*),MIN(SupplierContactId),MAX(SupplierContactId),
SUM(IIF(SupplierContactId BETWEEN 1 AND 5,1,0)),SUM(IIF(SupplierContactId BETWEEN 6 AND 10,1,0)),
SUM(IIF(SupplierContactId BETWEEN 11 AND 50,1,0)) FROM dbo.SupplierContacts
UNION ALL SELECT N'SupplierStores',COUNT(*),MIN(SupplierStoreId),MAX(SupplierStoreId),
SUM(IIF(SupplierStoreId BETWEEN 1 AND 5,1,0)),SUM(IIF(SupplierStoreId BETWEEN 6 AND 10,1,0)),
SUM(IIF(SupplierStoreId BETWEEN 11 AND 50,1,0)) FROM dbo.SupplierStores;

SELECT N'Orphan SupplierPhone' Issue,COUNT(*) IssueCount FROM dbo.SupplierPhones p
LEFT JOIN dbo.Suppliers s ON s.SupplierId=p.SupplierId WHERE s.SupplierId IS NULL
UNION ALL SELECT N'Orphan SupplierContact',COUNT(*) FROM dbo.SupplierContacts c
LEFT JOIN dbo.Suppliers s ON s.SupplierId=c.SupplierId WHERE s.SupplierId IS NULL
UNION ALL SELECT N'Orphan SupplierStore',COUNT(*) FROM dbo.SupplierStores ss
LEFT JOIN dbo.Suppliers s ON s.SupplierId=ss.SupplierId
LEFT JOIN dbo.Stores st ON st.StoreId=ss.StoreId WHERE s.SupplierId IS NULL OR st.StoreId IS NULL
UNION ALL SELECT N'Duplicate TaxCode',COUNT(*) FROM
(SELECT TaxCode FROM dbo.Suppliers WHERE TaxCode IS NOT NULL GROUP BY TaxCode HAVING COUNT(*)>1)x
UNION ALL SELECT N'Duplicate Phone',COUNT(*) FROM
(SELECT SupplierId,PhoneNumber FROM dbo.SupplierPhones GROUP BY SupplierId,PhoneNumber HAVING COUNT(*)>1)x
UNION ALL SELECT N'Duplicate Contact Email',COUNT(*) FROM
(SELECT SupplierId,Email FROM dbo.SupplierContacts WHERE Email IS NOT NULL GROUP BY SupplierId,Email HAVING COUNT(*)>1)x
UNION ALL SELECT N'Duplicate Store Scope',COUNT(*) FROM
(SELECT SupplierId,StoreId FROM dbo.SupplierStores GROUP BY SupplierId,StoreId HAVING COUNT(*)>1)x;

/* ============================================================
   BATCH 06/12 - INGREDIENT SUPPLIER OFFERS AND PRICE HISTORY

   Mapping:
     - IngredientSuppliers 1-9 and price histories 1-3 are EF HasData.
     - IDs 10-40 retain all 31 Store1 offers after ingredient aliases.
     - IDs 41-100 add relevant alternatives so every ingredient has
       exactly two active suppliers and exactly one primary supplier.
     - Histories 4-294 use fixed 2025-01, 2025-07 and 2026-01 dates.
   ============================================================ */
IF EXISTS (SELECT 1 FROM dbo.SystemSettings
           WHERE SettingKey=N'seedall_foundation_inventory_v1' AND SettingValue=N'completed')
BEGIN
 PRINT N'SeedAll Batch 06 skipped: foundation inventory v1 is already complete.';
 GOTO SeedAllBatch06Complete;
END;
BEGIN TRY
 BEGIN TRANSACTION;

 IF OBJECT_ID(N'dbo.IngredientSuppliers',N'U') IS NULL
 OR OBJECT_ID(N'dbo.IngredientSupplierPriceHistories',N'U') IS NULL
  THROW 52600,N'Schema thiếu bảng bắt buộc của SeedAll Batch 06.',1;

 IF (SELECT COUNT(*) FROM dbo.IngredientSuppliers WHERE IngredientSupplierId BETWEEN 1 AND 9)<>9
 OR EXISTS(SELECT 1 FROM (VALUES
 (1,6,1,1,CAST(1000 AS decimal(18,5)),CAST(22000 AS decimal(18,2)),1,1,1,N'Đường Biên Hòa'),
 (2,2,2,3,380,27000,24,2,1,N'Sữa đặc demo lon 380 ml (synthetic)'),
 (3,1,3,1,1000,140000,5,3,1,N'Cà phê hạt'),
 (4,8,4,3,750,250000,6,4,1,N'Syrup Torani'),
 (5,10,2,3,1000,95000,12,2,1,N'Kem béo Rich'),
 (6,9,5,1,500,450000,1,5,1,N'Matcha Nhật'),
 (7,5,3,1,1000,180000,2,3,1,N'Bột cacao'),
 (8,4,1,1,1000,85000,2,2,1,N'Bột sữa'),
 (9,3,4,1,200,120000,1,5,1,N'Trà đen demo 100 túi × 2 g (synthetic)')
 )x(Id,IngredientId,SupplierId,UnitId,PackageQuantity,CurrentPrice,MOQ,LeadTime,IsPrimary,Note)
 LEFT JOIN dbo.IngredientSuppliers o ON o.IngredientSupplierId=x.Id
 WHERE o.IngredientSupplierId IS NULL OR o.IngredientId<>x.IngredientId OR o.SupplierId<>x.SupplierId
 OR o.UnitId<>x.UnitId OR o.PackageQuantity<>x.PackageQuantity OR o.CurrentPrice<>x.CurrentPrice
 OR o.MinimumOrderPackageCount<>x.MOQ OR o.LeadTimeDays<>x.LeadTime OR o.IsPrimary<>x.IsPrimary
 OR o.Active<>1 OR o.Note<>x.Note)
  THROW 52601,N'IngredientSuppliers EF IDs 1-9 thiếu hoặc khác contract migration.',1;

/* ============================================================
   FIX LEGACY PRICE HISTORY #2

   IngredientSupplier #2 đã có package evidence hợp lệ:
   - PackageQuantity is expressed in Ingredient.BaseUnit
   - UnitId equals Ingredient.BaseUnitId

   PriceHistory legacy bị thiếu hai field này.
   Chỉ backfill khi row vẫn đang ở legacy NULL state.
   ============================================================ */
UPDATE ph
SET
    ph.PackageQuantity = o.PackageQuantity,
    ph.PackageUnitId   = o.UnitId
FROM dbo.IngredientSupplierPriceHistories ph
JOIN dbo.IngredientSuppliers o
    ON o.IngredientSupplierId = ph.IngredientSupplierId
WHERE
    ph.IngredientSupplierPriceHistoryId = 2
    AND ph.IngredientSupplierId = 2
    AND ph.IsCurrent = 1

    -- Chỉ sửa đúng legacy state.
    AND ph.PackageQuantity IS NULL
    AND ph.PackageUnitId IS NULL

    -- Không tự bịa dữ liệu.
    AND o.PackageQuantity IS NOT NULL
    AND o.PackageQuantity > 0
    AND o.UnitId IS NOT NULL;

 IF (
    SELECT COUNT(*)
    FROM dbo.IngredientSupplierPriceHistories
    WHERE IngredientSupplierPriceHistoryId BETWEEN 1 AND 3
) <> 3
OR EXISTS
(
    SELECT 1
    FROM
    (
        VALUES
            (1, 1, CAST(22000 AS decimal(18,2)), CAST(1000 AS decimal(18,5)), 1),
            (2, 2, CAST(27000 AS decimal(18,2)), CAST(380 AS decimal(18,5)), 3),
            (3, 3, CAST(140000 AS decimal(18,2)), CAST(1000 AS decimal(18,5)), 1)
    ) 
    x
    (
        Id,
        OfferId,
        Price,
        PackageQuantity,
        PackageUnitId
    )

    LEFT JOIN dbo.IngredientSupplierPriceHistories h
        ON h.IngredientSupplierPriceHistoryId = x.Id

    WHERE
        h.IngredientSupplierPriceHistoryId IS NULL

        OR h.IngredientSupplierId <> x.OfferId
        OR h.Price <> x.Price

        OR ISNULL(h.PackageQuantity,-1)
            <> ISNULL(x.PackageQuantity,-1)

        OR ISNULL(h.PackageUnitId,-1)
            <> ISNULL(x.PackageUnitId,-1)

        OR h.EffectiveDate <> '2025-01-01'
        OR h.IsCurrent <> 1
        OR h.Note <> N'Giá ban đầu'
        OR h.CreatedByStaffId IS NOT NULL
)
THROW 52602, N'Price histories EF IDs 1-3 thiếu hoặc khác contract migration.', 1;

 DECLARE @ActorStaffId int;
 SELECT TOP(1) @ActorStaffId=s.StaffId FROM dbo.Staffs s
 JOIN dbo.Accounts a ON a.AccountId=s.AccountId AND a.Active=1
 JOIN dbo.AccountRoles ar ON ar.AccountId=a.AccountId
 JOIN dbo.Roles r ON r.RoleId=ar.RoleId AND r.Active=1
 WHERE s.StoreId=1 AND s.Active=1 AND r.Name=N'Chủ doanh nghiệp' ORDER BY s.StaffId;
 IF @ActorStaffId IS NULL THROW 52603,N'Store 1 thiếu Staff active có role Chủ doanh nghiệp.',1;

 DECLARE @OfferSeed TABLE(IngredientSupplierId int PRIMARY KEY,IngredientId int,SupplierId int,UnitId int,
 PackageQuantity decimal(18,5),CurrentPrice decimal(18,2),MinimumOrderPackageCount int,LeadTimeDays int,
 IsPrimary bit,Active bit,Note nvarchar(1000),CreatedAt datetime2,UpdatedAt datetime2,
 UNIQUE(IngredientId,SupplierId));
 INSERT @OfferSeed VALUES
(10,14,6,1,1000,180000,1,1,1,1,N'DEMO_OFFER_VIET_COFFEE','2026-01-01','2026-01-01'),
(11,15,6,1,1000,240000,1,2,1,1,N'DEMO_OFFER_ESPRESSO_BEAN','2026-01-01','2026-01-01'),
(12,2,7,3,9120,648000,1,3,0,1,N'DEMO_OFFER_CONDENSED_MILK','2026-01-01','2026-01-01'),
(13,16,7,3,12000,384000,1,4,1,1,N'DEMO_OFFER_FRESH_MILK','2026-01-01','2026-01-01'),
(14,10,7,3,12000,1140000,1,5,0,1,N'DEMO_OFFER_DAIRY_CREAM','2026-01-01','2026-01-01'),
(15,17,10,1,1000,15000,1,1,1,1,N'DEMO_OFFER_SALT','2026-01-01','2026-01-01'),
(16,6,9,1,1000,22000,1,2,0,1,N'DEMO_OFFER_SUGAR','2026-01-01','2026-01-01'),
(17,18,10,3,5000,120000,1,3,1,1,N'DEMO_OFFER_SUGAR_SYRUP','2026-01-01','2026-01-01'),
(18,3,9,1,500,120000,1,4,0,1,N'DEMO_OFFER_BLACK_TEA','2026-01-01','2026-01-01'),
(19,19,9,1,500,140000,1,5,1,1,N'DEMO_OFFER_OOLONG_TEA','2026-01-01','2026-01-01'),
(20,20,9,1,10000,800000,1,1,1,1,N'DEMO_OFFER_CANNED_PEACH','2026-01-01','2026-01-01'),
(21,21,9,1,10000,850000,1,2,1,1,N'DEMO_OFFER_CANNED_LYCHEE','2026-01-01','2026-01-01'),
(22,22,9,1,5000,450000,1,3,1,1,N'DEMO_OFFER_PASSION_JAM','2026-01-01','2026-01-01'),
(23,23,9,1,10000,350000,1,4,1,1,N'DEMO_OFFER_ORANGE','2026-01-01','2026-01-01'),
(24,24,9,1,5000,125000,1,5,1,1,N'DEMO_OFFER_LEMONGRASS','2026-01-01','2026-01-01'),
(25,9,9,1,500,450000,1,1,0,1,N'DEMO_OFFER_MATCHA','2026-01-01','2026-01-01'),
(26,25,10,1,1000,300000,1,2,1,1,N'DEMO_OFFER_CHOCOLATE','2026-01-01','2026-01-01'),
(27,26,10,1,1000,180000,1,3,1,1,N'DEMO_OFFER_FRAPPE','2026-01-01','2026-01-01'),
(28,27,10,1,1000,80000,1,4,1,1,N'DEMO_OFFER_BLACK_PEARL_DRY','2026-01-01','2026-01-01'),
(29,28,10,13,500,1250000,1,5,1,1,N'DEMO_OFFER_WHITE_PEARL','2026-01-01','2026-01-01'),
(30,29,10,1,1000,160000,1,1,1,1,N'DEMO_OFFER_TARO_JELLY_POWDER','2026-01-01','2026-01-01'),
(31,30,10,1,1000,180000,1,2,1,1,N'DEMO_OFFER_FLAN_POWDER','2026-01-01','2026-01-01'),
(32,31,7,1,1000,220000,1,3,1,1,N'DEMO_OFFER_CHEESE_POWDER','2026-01-01','2026-01-01'),
(33,13,9,3,20000,30000,1,4,1,1,N'DEMO_OFFER_WATER','2026-01-01','2026-01-01'),
(34,7,9,1,20000,40000,1,5,1,1,N'DEMO_OFFER_ICE','2026-01-01','2026-01-01'),
(35,32,8,9,1000,900000,1,1,1,1,N'DEMO_OFFER_CUP_M','2026-01-01','2026-01-01'),
(36,33,8,9,1000,1050000,1,2,1,1,N'DEMO_OFFER_CUP_L','2026-01-01','2026-01-01'),
(37,34,8,9,1000,300000,1,3,1,1,N'DEMO_OFFER_LID_M','2026-01-01','2026-01-01'),
(38,35,8,9,1000,350000,1,4,1,1,N'DEMO_OFFER_LID_L','2026-01-01','2026-01-01'),
(39,36,8,9,2000,300000,1,5,1,1,N'DEMO_OFFER_STRAW','2026-01-01','2026-01-01'),
(40,37,8,9,500,250000,1,1,1,1,N'DEMO_OFFER_BAG','2026-01-01','2026-01-01'),
(41,1,11,1,1000,148400,5,3,0,1,N'SEEDALL_ALT_ING_001','2026-01-01','2026-01-01'),
(42,4,22,1,1000,90100,2,2,0,1,N'SEEDALL_ALT_ING_004','2026-01-01','2026-01-01'),
(43,5,39,1,1000,190800,2,4,0,1,N'SEEDALL_ALT_ING_005','2026-01-01','2026-01-01'),
(44,7,33,1,20000,42400,1,3,0,1,N'SEEDALL_ALT_ING_007','2026-01-01','2026-01-01'),
(45,8,42,3,750,265000,6,4,0,1,N'SEEDALL_ALT_ING_008','2026-01-01','2026-01-01'),
(46,11,37,1,1000,42000,1,3,1,1,N'SEEDALL_PRIMARY_ING_011','2026-01-01','2026-01-01'),
(47,11,38,1,1000,44500,1,5,0,1,N'SEEDALL_ALT_ING_011','2026-01-01','2026-01-01'),
(48,12,38,1,1000,65000,1,5,1,1,N'SEEDALL_PRIMARY_ING_012','2026-01-01','2026-01-01'),
(49,12,39,1,1000,68900,1,4,0,1,N'SEEDALL_ALT_ING_012','2026-01-01','2026-01-01'),
(50,13,31,3,20000,31800,1,4,0,1,N'SEEDALL_ALT_ING_013','2026-01-01','2026-01-01'),
(51,14,16,1,1000,190800,1,2,0,1,N'SEEDALL_ALT_ING_014','2026-01-01','2026-01-01'),
(52,15,17,1,1000,254400,1,3,0,1,N'SEEDALL_ALT_ING_015','2026-01-01','2026-01-01'),
(53,16,26,3,12000,407000,1,4,0,1,N'SEEDALL_ALT_ING_016','2026-01-01','2026-01-01'),
(54,17,35,1,1000,15900,1,3,0,1,N'SEEDALL_ALT_ING_017','2026-01-01','2026-01-01'),
(55,18,36,3,5000,127200,1,4,0,1,N'SEEDALL_ALT_ING_018','2026-01-01','2026-01-01'),
(56,19,13,1,500,148400,1,3,0,1,N'SEEDALL_ALT_ING_019','2026-01-01','2026-01-01'),
(57,20,30,1,10000,848000,1,3,0,1,N'SEEDALL_ALT_ING_020','2026-01-01','2026-01-01'),
(58,21,31,1,10000,901000,1,4,0,1,N'SEEDALL_ALT_ING_021','2026-01-01','2026-01-01'),
(59,22,32,1,5000,477000,1,2,0,1,N'SEEDALL_ALT_ING_022','2026-01-01','2026-01-01'),
(60,23,33,1,10000,371000,1,3,0,1,N'SEEDALL_ALT_ING_023','2026-01-01','2026-01-01'),
(61,24,34,1,5000,132500,1,4,0,1,N'SEEDALL_ALT_ING_024','2026-01-01','2026-01-01'),
(62,25,11,1,1000,318000,1,3,0,1,N'SEEDALL_ALT_ING_025','2026-01-01','2026-01-01'),
(63,26,36,1,1000,190800,1,4,0,1,N'SEEDALL_ALT_ING_026','2026-01-01','2026-01-01'),
(64,27,37,1,1000,84800,1,3,0,1,N'SEEDALL_ALT_ING_027','2026-01-01','2026-01-01'),
(65,28,38,13,500,1325000,1,5,0,1,N'SEEDALL_ALT_ING_028','2026-01-01','2026-01-01'),
(66,29,39,1,1000,169600,1,4,0,1,N'SEEDALL_ALT_ING_029','2026-01-01','2026-01-01'),
(67,30,40,1,1000,190800,1,3,0,1,N'SEEDALL_ALT_ING_030','2026-01-01','2026-01-01'),
(68,31,25,1,1000,233200,1,2,0,1,N'SEEDALL_ALT_ING_031','2026-01-01','2026-01-01'),
(69,32,50,9,1000,954000,1,3,0,1,N'SEEDALL_ALT_ING_032','2026-01-01','2026-01-01'),
(70,33,43,9,1000,1113000,1,2,0,1,N'SEEDALL_ALT_ING_033','2026-01-01','2026-01-01'),
(71,34,44,9,1000,318000,1,3,0,1,N'SEEDALL_ALT_ING_034','2026-01-01','2026-01-01'),
(72,35,45,9,1000,371000,1,2,0,1,N'SEEDALL_ALT_ING_035','2026-01-01','2026-01-01'),
(73,36,46,9,2000,318000,1,4,0,1,N'SEEDALL_ALT_ING_036','2026-01-01','2026-01-01'),
(74,37,47,9,500,265000,1,3,0,1,N'SEEDALL_ALT_ING_037','2026-01-01','2026-01-01'),
(75,38,32,1,1000,120000,2,2,1,1,N'SEEDALL_PRIMARY_ING_038','2026-01-01','2026-01-01'),
(76,38,33,1,1000,127200,2,3,0,1,N'SEEDALL_ALT_ING_038','2026-01-01','2026-01-01'),
(77,39,33,1,5000,180000,2,3,1,1,N'SEEDALL_PRIMARY_ING_039','2026-01-01','2026-01-01'),
(78,39,34,1,5000,190800,2,4,0,1,N'SEEDALL_ALT_ING_039','2026-01-01','2026-01-01'),
(79,40,34,1,5000,520000,2,4,1,1,N'SEEDALL_PRIMARY_ING_040','2026-01-01','2026-01-01'),
(80,40,27,1,5000,551200,2,2,0,1,N'SEEDALL_ALT_ING_040','2026-01-01','2026-01-01'),
(81,41,27,1,5000,650000,2,2,1,1,N'SEEDALL_PRIMARY_ING_041','2026-01-01','2026-01-01'),
(82,41,28,1,5000,689000,2,3,0,1,N'SEEDALL_ALT_ING_041','2026-01-01','2026-01-01'),
(83,42,20,3,12000,720000,6,2,1,1,N'SEEDALL_PRIMARY_ING_042','2026-01-01','2026-01-01'),
(84,42,21,3,12000,763200,6,3,0,1,N'SEEDALL_ALT_ING_042','2026-01-01','2026-01-01'),
(85,43,37,3,5000,650000,2,3,1,1,N'SEEDALL_PRIMARY_ING_043','2026-01-01','2026-01-01'),
(86,43,38,3,5000,689000,2,5,0,1,N'SEEDALL_ALT_ING_043','2026-01-01','2026-01-01'),
(87,44,22,3,12000,540000,6,2,1,1,N'SEEDALL_PRIMARY_ING_044','2026-01-01','2026-01-01'),
(88,44,23,3,12000,572400,6,4,0,1,N'SEEDALL_ALT_ING_044','2026-01-01','2026-01-01'),
(89,45,23,1,5000,300000,4,4,1,1,N'SEEDALL_PRIMARY_ING_045','2026-01-01','2026-01-01'),
(90,45,24,1,5000,318000,4,3,0,1,N'SEEDALL_ALT_ING_045','2026-01-01','2026-01-01'),
(91,46,24,9,100,450000,2,3,1,1,N'SEEDALL_PRIMARY_ING_046','2026-01-01','2026-01-01'),
(92,46,25,9,100,477000,2,2,0,1,N'SEEDALL_ALT_ING_046','2026-01-01','2026-01-01'),
(93,47,41,1,1000,160000,1,5,1,1,N'SEEDALL_PRIMARY_ING_047','2026-01-01','2026-01-01'),
(94,47,42,1,1000,169600,1,4,0,1,N'SEEDALL_ALT_ING_047','2026-01-01','2026-01-01'),
(95,48,34,1,5000,260000,2,4,1,1,N'SEEDALL_PRIMARY_ING_048','2026-01-01','2026-01-01'),
(96,48,27,1,5000,275600,2,2,0,1,N'SEEDALL_ALT_ING_048','2026-01-01','2026-01-01'),
(97,49,35,1,1000,180000,1,3,1,1,N'SEEDALL_PRIMARY_ING_049','2026-01-01','2026-01-01'),
(98,49,36,1,1000,190800,1,4,0,1,N'SEEDALL_ALT_ING_049','2026-01-01','2026-01-01'),
(99,50,36,1,5000,220000,2,4,1,1,N'SEEDALL_PRIMARY_ING_050','2026-01-01','2026-01-01'),
(100,50,37,1,5000,233200,2,3,0,1,N'SEEDALL_ALT_ING_050','2026-01-01','2026-01-01');

 IF (SELECT COUNT(*) FROM @OfferSeed)<>91 OR EXISTS(SELECT 1 FROM @OfferSeed
 WHERE PackageQuantity<=0 OR CurrentPrice<=0 OR MinimumOrderPackageCount<=0 OR LeadTimeDays<0 OR Active<>1)
  THROW 52604,N'Bộ 91 offer mới sai số lượng, package, price, MOQ hoặc lead time.',1;

 IF EXISTS(SELECT 1 FROM @OfferSeed x
 LEFT JOIN dbo.Ingredients i ON i.IngredientId=x.IngredientId
 LEFT JOIN dbo.Suppliers s ON s.SupplierId=x.SupplierId
 LEFT JOIN dbo.Units u ON u.UnitId=x.UnitId
 LEFT JOIN dbo.SupplierStores ss ON ss.SupplierId=x.SupplierId AND ss.StoreId=1 AND ss.Active=1
 WHERE i.IngredientId IS NULL OR s.SupplierId IS NULL OR u.UnitId IS NULL OR ss.SupplierStoreId IS NULL)
  THROW 52605,N'Offer tham chiếu Ingredient, Supplier, Unit hoặc Store scope không tồn tại.',1;

 IF EXISTS(SELECT 1 FROM @OfferSeed x JOIN dbo.IngredientSuppliers o
 ON o.IngredientSupplierId=x.IngredientSupplierId OR(o.IngredientId=x.IngredientId AND o.SupplierId=x.SupplierId)
 WHERE o.IngredientSupplierId<>x.IngredientSupplierId OR o.IngredientId<>x.IngredientId
 OR o.SupplierId<>x.SupplierId OR o.UnitId<>x.UnitId OR o.PackageQuantity<>x.PackageQuantity
 OR o.CurrentPrice<>x.CurrentPrice OR o.MinimumOrderPackageCount<>x.MinimumOrderPackageCount
 OR o.LeadTimeDays<>x.LeadTimeDays OR o.IsPrimary<>x.IsPrimary OR o.Active<>x.Active
 OR o.Note<>x.Note OR o.CreatedAt<>x.CreatedAt OR o.UpdatedAt<>x.UpdatedAt)
  THROW 52606,N'IngredientSuppliers có ID hoặc business key xung đột.',1;

 IF EXISTS(SELECT 1 FROM @OfferSeed x JOIN dbo.Ingredients i ON i.IngredientId=x.IngredientId
 WHERE x.UnitId<>i.BaseUnitId AND NOT EXISTS(SELECT 1 FROM dbo.UnitConversions uc
 WHERE uc.IngredientId=i.IngredientId AND uc.FromUnitId=x.UnitId AND uc.ToUnitId=i.BaseUnitId
 AND uc.Active=1 AND uc.FromQuantity>0 AND uc.ToQuantity>0))
  THROW 52607,N'Package unit không quy đổi được về base unit của Ingredient.',1;

 SET IDENTITY_INSERT dbo.IngredientSuppliers ON;
 INSERT dbo.IngredientSuppliers(IngredientSupplierId,IngredientId,SupplierId,UnitId,PackageQuantity,
 CurrentPrice,MinimumOrderPackageCount,LeadTimeDays,IsPrimary,Active,Note,CreatedAt,UpdatedAt)
 SELECT * FROM @OfferSeed x WHERE NOT EXISTS(SELECT 1 FROM dbo.IngredientSuppliers o
 WHERE o.IngredientSupplierId=x.IngredientSupplierId);
 SET IDENTITY_INSERT dbo.IngredientSuppliers OFF;

 IF EXISTS(SELECT IngredientId FROM dbo.IngredientSuppliers WHERE Active=1
 GROUP BY IngredientId HAVING COUNT(*)<>2 OR SUM(CONVERT(int,IsPrimary))<>1)
 OR(SELECT COUNT(DISTINCT IngredientId) FROM dbo.IngredientSuppliers WHERE Active=1)<>50
  THROW 52608,N'Mỗi Ingredient phải có đúng hai offer active và một primary.',1;

 DECLARE @HistorySeed TABLE(IngredientSupplierPriceHistoryId int PRIMARY KEY,IngredientSupplierId int,
 Price decimal(18,2),PackageQuantity decimal(18,5),PackageUnitId int,EffectiveDate datetime2,
 IsCurrent bit,Note nvarchar(1000),CreatedByStaffId int,CreatedAtUtc datetime2,
 UNIQUE(IngredientSupplierId,EffectiveDate));
 INSERT @HistorySeed VALUES
(4,4,225000,750,3,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(5,4,237500,750,3,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(6,4,250000,750,3,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(7,5,85500,1,4,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(8,5,90300,1,4,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(9,5,95000,1,4,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(10,6,405000,500,1,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(11,6,427500,500,1,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(12,6,450000,500,1,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(13,7,162000,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(14,7,171000,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(15,7,180000,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(16,8,76500,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(17,8,80800,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(18,8,85000,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(19,9,108000,200,1,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(20,9,114000,200,1,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(21,9,120000,200,1,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(22,10,162000,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(23,10,171000,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(24,10,180000,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(25,11,216000,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(26,11,228000,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(27,11,240000,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(28,12,583200,9120,3,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(29,12,615600,9120,3,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(30,12,648000,9120,3,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(31,13,345600,12,4,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(32,13,364800,12,4,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(33,13,384000,12,4,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(34,14,1026000,12,4,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(35,14,1083000,12,4,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(36,14,1140000,12,4,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(37,15,13500,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(38,15,14300,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(39,15,15000,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(40,16,19800,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(41,16,20900,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(42,16,22000,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(43,17,108000,5,4,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(44,17,114000,5,4,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(45,17,120000,5,4,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(46,18,108000,500,1,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(47,18,114000,500,1,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(48,18,120000,500,1,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(49,19,126000,500,1,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(50,19,133000,500,1,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(51,19,140000,500,1,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(52,20,720000,10000,1,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(53,20,760000,10000,1,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(54,20,800000,10000,1,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(55,21,765000,10000,1,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(56,21,807500,10000,1,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(57,21,850000,10000,1,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(58,22,405000,5000,1,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(59,22,427500,5000,1,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(60,22,450000,5000,1,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(61,23,315000,10,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(62,23,332500,10,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(63,23,350000,10,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(64,24,112500,5,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(65,24,118800,5,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(66,24,125000,5,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(67,25,405000,500,1,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(68,25,427500,500,1,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(69,25,450000,500,1,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(70,26,270000,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(71,26,285000,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(72,26,300000,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(73,27,162000,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(74,27,171000,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(75,27,180000,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(76,28,72000,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(77,28,76000,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(78,28,80000,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(79,29,1125000,500,13,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(80,29,1187500,500,13,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(81,29,1250000,500,13,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(82,30,144000,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(83,30,152000,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(84,30,160000,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(85,31,162000,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(86,31,171000,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(87,31,180000,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(88,32,198000,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(89,32,209000,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(90,32,220000,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(91,33,27000,20,4,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(92,33,28500,20,4,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(93,33,30000,20,4,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(94,34,36000,20,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(95,34,38000,20,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(96,34,40000,20,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(97,35,810000,1,14,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(98,35,855000,1,14,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(99,35,900000,1,14,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(100,36,945000,1,14,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(101,36,997500,1,14,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(102,36,1050000,1,14,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(103,37,270000,1,14,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(104,37,285000,1,14,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(105,37,300000,1,14,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(106,38,315000,1,14,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(107,38,332500,1,14,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(108,38,350000,1,14,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(109,39,270000,1,14,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(110,39,285000,1,14,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(111,39,300000,1,14,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(112,40,225000,1,14,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(113,40,237500,1,14,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(114,40,250000,1,14,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(115,41,133600,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(116,41,141000,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(117,41,148400,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(118,42,81100,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(119,42,85600,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(120,42,90100,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(121,43,171700,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(122,43,181300,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(123,43,190800,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(124,44,38200,20,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(125,44,40300,20,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(126,44,42400,20,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(127,45,238500,750,3,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(128,45,251800,750,3,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(129,45,265000,750,3,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(130,46,37800,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(131,46,39900,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(132,46,42000,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(133,47,40100,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(134,47,42300,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(135,47,44500,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(136,48,58500,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(137,48,61800,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(138,48,65000,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(139,49,62000,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(140,49,65500,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(141,49,68900,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(142,50,28600,20,4,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(143,50,30200,20,4,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(144,50,31800,20,4,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(145,51,171700,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(146,51,181300,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(147,51,190800,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(148,52,229000,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(149,52,241700,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(150,52,254400,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(151,53,366300,12,4,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(152,53,386700,12,4,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(153,53,407000,12,4,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(154,54,14300,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(155,54,15100,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(156,54,15900,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(157,55,114500,5,4,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(158,55,120800,5,4,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(159,55,127200,5,4,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(160,56,133600,500,1,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(161,56,141000,500,1,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(162,56,148400,500,1,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(163,57,763200,10000,1,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(164,57,805600,10000,1,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(165,57,848000,10000,1,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(166,58,810900,10000,1,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(167,58,856000,10000,1,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(168,58,901000,10000,1,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(169,59,429300,5000,1,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(170,59,453200,5000,1,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(171,59,477000,5000,1,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(172,60,333900,10,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(173,60,352500,10,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(174,60,371000,10,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(175,61,119300,5,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(176,61,125900,5,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(177,61,132500,5,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(178,62,286200,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(179,62,302100,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(180,62,318000,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(181,63,171700,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(182,63,181300,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(183,63,190800,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(184,64,76300,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(185,64,80600,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(186,64,84800,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(187,65,1192500,500,13,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(188,65,1258800,500,13,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(189,65,1325000,500,13,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(190,66,152600,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(191,66,161100,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(192,66,169600,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(193,67,171700,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(194,67,181300,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(195,67,190800,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(196,68,209900,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(197,68,221500,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(198,68,233200,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(199,69,858600,1,14,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(200,69,906300,1,14,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(201,69,954000,1,14,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(202,70,1001700,1,14,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(203,70,1057400,1,14,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(204,70,1113000,1,14,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(205,71,286200,1,14,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(206,71,302100,1,14,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(207,71,318000,1,14,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(208,72,333900,1,14,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(209,72,352500,1,14,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(210,72,371000,1,14,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(211,73,286200,1,14,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(212,73,302100,1,14,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(213,73,318000,1,14,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(214,74,238500,1,14,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(215,74,251800,1,14,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(216,74,265000,1,14,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(217,75,108000,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(218,75,114000,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(219,75,120000,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(220,76,114500,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(221,76,120800,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(222,76,127200,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(223,77,162000,5,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(224,77,171000,5,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(225,77,180000,5,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(226,78,171700,5,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(227,78,181300,5,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(228,78,190800,5,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(229,79,468000,5,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(230,79,494000,5,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(231,79,520000,5,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(232,80,496100,5,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(233,80,523600,5,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(234,80,551200,5,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(235,81,585000,5,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(236,81,617500,5,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(237,81,650000,5,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(238,82,620100,5,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(239,82,654600,5,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(240,82,689000,5,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(241,83,648000,12,4,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(242,83,684000,12,4,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(243,83,720000,12,4,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(244,84,686900,12,4,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(245,84,725000,12,4,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(246,84,763200,12,4,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(247,85,585000,5,4,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(248,85,617500,5,4,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(249,85,650000,5,4,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(250,86,620100,5,4,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(251,86,654600,5,4,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(252,86,689000,5,4,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(253,87,486000,12,4,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(254,87,513000,12,4,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(255,87,540000,12,4,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(256,88,515200,12,4,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(257,88,543800,12,4,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(258,88,572400,12,4,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(259,89,270000,5000,1,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(260,89,285000,5000,1,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(261,89,300000,5000,1,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(262,90,286200,5000,1,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(263,90,302100,5000,1,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(264,90,318000,5000,1,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(265,91,405000,100,9,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(266,91,427500,100,9,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(267,91,450000,100,9,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(268,92,429300,100,9,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(269,92,453200,100,9,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(270,92,477000,100,9,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(271,93,144000,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(272,93,152000,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(273,93,160000,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(274,94,152600,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(275,94,161100,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(276,94,169600,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(277,95,234000,5000,1,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(278,95,247000,5000,1,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(279,95,260000,5000,1,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(280,96,248000,5000,1,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(281,96,261800,5000,1,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(282,96,275600,5000,1,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(283,97,162000,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(284,97,171000,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(285,97,180000,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(286,98,171700,1,2,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(287,98,181300,1,2,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(288,98,190800,1,2,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(289,99,198000,5000,1,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(290,99,209000,5000,1,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(291,99,220000,5000,1,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01'),
(292,100,209900,5000,1,'2025-01-01',0,N'Giá lịch sử 2025-01',@ActorStaffId,'2025-01-01'),
(293,100,221500,5000,1,'2025-07-01',0,N'Giá giữa kỳ 2025-07',@ActorStaffId,'2025-07-01'),
(294,100,233200,5000,1,'2026-01-01',1,N'Giá hiện tại 2026-01',@ActorStaffId,'2026-01-01');

 /* Price history snapshots inherit the canonical content definition from the
    parent offer. Historical price/effective-date facts are left unchanged. */
 UPDATE h
    SET PackageQuantity=o.PackageQuantity,PackageUnitId=o.UnitId
 FROM @HistorySeed h
 JOIN dbo.IngredientSuppliers o ON o.IngredientSupplierId=h.IngredientSupplierId;

 IF (SELECT COUNT(*) FROM @HistorySeed)<>291 OR EXISTS(SELECT 1 FROM @HistorySeed
 WHERE Price<=0 OR PackageQuantity<=0 OR PackageUnitId IS NULL)
  THROW 52609,N'Bộ 291 price history mới sai số lượng, price hoặc package snapshot.',1;

 IF EXISTS(SELECT 1 FROM @HistorySeed x
 LEFT JOIN dbo.IngredientSuppliers o ON o.IngredientSupplierId=x.IngredientSupplierId
 LEFT JOIN dbo.Units u ON u.UnitId=x.PackageUnitId
 WHERE o.IngredientSupplierId IS NULL OR u.UnitId IS NULL
 OR x.PackageQuantity<>o.PackageQuantity OR x.PackageUnitId<>o.UnitId
 OR(x.IsCurrent=1 AND x.Price<>o.CurrentPrice))
  THROW 52610,N'Price history không khớp parent offer/package snapshot.',1;

 IF EXISTS(SELECT 1 FROM @HistorySeed x JOIN dbo.IngredientSupplierPriceHistories h
 ON h.IngredientSupplierPriceHistoryId=x.IngredientSupplierPriceHistoryId
 OR(h.IngredientSupplierId=x.IngredientSupplierId AND h.EffectiveDate=x.EffectiveDate)
 WHERE h.IngredientSupplierPriceHistoryId<>x.IngredientSupplierPriceHistoryId
 OR h.IngredientSupplierId<>x.IngredientSupplierId OR h.Price<>x.Price
 OR h.PackageQuantity<>x.PackageQuantity OR h.PackageUnitId<>x.PackageUnitId
 OR h.EffectiveDate<>x.EffectiveDate OR h.IsCurrent<>x.IsCurrent OR h.Note<>x.Note
 OR h.CreatedByStaffId<>x.CreatedByStaffId OR h.CreatedAtUtc<>x.CreatedAtUtc)
  THROW 52611,N'Price histories có ID hoặc offer/effective date xung đột.',1;

 SET IDENTITY_INSERT dbo.IngredientSupplierPriceHistories ON;
 INSERT dbo.IngredientSupplierPriceHistories(IngredientSupplierPriceHistoryId,IngredientSupplierId,
 Price,PackageQuantity,PackageUnitId,EffectiveDate,IsCurrent,Note,CreatedByStaffId,CreatedAtUtc)
 SELECT * FROM @HistorySeed x WHERE NOT EXISTS(SELECT 1 FROM dbo.IngredientSupplierPriceHistories h
 WHERE h.IngredientSupplierPriceHistoryId=x.IngredientSupplierPriceHistoryId);
 SET IDENTITY_INSERT dbo.IngredientSupplierPriceHistories OFF;

 IF (SELECT COUNT(*) FROM dbo.IngredientSuppliers)<>100
 OR(SELECT COUNT(*) FROM dbo.IngredientSupplierPriceHistories)<>294
  THROW 52612,N'Row count cuối Batch 06 không đúng contract.',1;

 IF EXISTS(SELECT IngredientId,SupplierId FROM dbo.IngredientSuppliers
 GROUP BY IngredientId,SupplierId HAVING COUNT(*)>1)
 OR EXISTS(SELECT IngredientSupplierId FROM dbo.IngredientSupplierPriceHistories
 WHERE IsCurrent=1 GROUP BY IngredientSupplierId HAVING COUNT(*)<>1)
 OR(SELECT COUNT(DISTINCT IngredientSupplierId) FROM dbo.IngredientSupplierPriceHistories WHERE IsCurrent=1)<>100
 OR EXISTS(SELECT 1 FROM dbo.IngredientSuppliers o
 LEFT JOIN dbo.IngredientSupplierPriceHistories h ON h.IngredientSupplierId=o.IngredientSupplierId AND h.IsCurrent=1
 WHERE h.IngredientSupplierPriceHistoryId IS NULL OR h.Price<>o.CurrentPrice
 OR(o.IngredientSupplierId<>2 AND(h.PackageQuantity<>o.PackageQuantity OR h.PackageUnitId<>o.UnitId)))
  THROW 52613,N'Duplicate offer, current price hoặc package snapshot không hợp lệ.',1;

 IF EXISTS
 (
     SELECT 1
     FROM dbo.IngredientSuppliers o
     JOIN dbo.Ingredients i ON i.IngredientId=o.IngredientId
     WHERE o.IngredientSupplierId BETWEEN 1 AND 100
       AND (o.UnitId<>i.BaseUnitId OR o.PackageQuantity IS NULL OR o.PackageQuantity<=0
            OR o.CurrentPrice<=0 OR o.Active<>1)
 ) THROW 52615,N'SeedAll offer chưa canonical hoặc chưa procurement-ready.',1;

 IF EXISTS
 (
     SELECT 1
     FROM dbo.IngredientSuppliers o
     LEFT JOIN dbo.Ingredients i ON i.IngredientId=o.IngredientId
     LEFT JOIN dbo.Suppliers s ON s.SupplierId=o.SupplierId
     LEFT JOIN dbo.Units u ON u.UnitId=o.UnitId
     WHERE o.IngredientSupplierId BETWEEN 1 AND 100
       AND
       (
           i.IngredientId IS NULL OR i.Active<>1
           OR s.SupplierId IS NULL OR s.Active<>1
           OR u.UnitId IS NULL OR u.Active<>1
           OR NOT EXISTS
           (
               SELECT 1
               FROM dbo.SupplierStores scope
               WHERE scope.SupplierId=o.SupplierId AND scope.StoreId=1 AND scope.Active=1
           )
       )
 ) THROW 52616,N'SeedAll offer is missing an active supplier, ingredient, unit, or Store 1 scope.',1;

 IF EXISTS(SELECT 1 FROM dbo.IngredientSupplierPriceHistories currentRow
 WHERE currentRow.IsCurrent=1 AND EXISTS(SELECT 1 FROM dbo.IngredientSupplierPriceHistories laterRow
 WHERE laterRow.IngredientSupplierId=currentRow.IngredientSupplierId
 AND laterRow.EffectiveDate>currentRow.EffectiveDate))
  THROW 52614,N'Giá current không phải mốc EffectiveDate mới nhất.',1;

 COMMIT;
END TRY
BEGIN CATCH
 BEGIN TRY SET IDENTITY_INSERT dbo.IngredientSuppliers OFF; END TRY BEGIN CATCH END CATCH;
 BEGIN TRY SET IDENTITY_INSERT dbo.IngredientSupplierPriceHistories OFF; END TRY BEGIN CATCH END CATCH;
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
SeedAllBatch06Complete:
GO

/* BATCH 06 READ-ONLY VERIFICATION */
SELECT N'IngredientSuppliers' Entity,COUNT(*) TotalRows,MIN(IngredientSupplierId) MinId,
MAX(IngredientSupplierId) MaxId,SUM(IIF(IngredientSupplierId BETWEEN 1 AND 9,1,0)) FoundationRows,
SUM(IIF(IngredientSupplierId BETWEEN 10 AND 40,1,0)) Store1Rows,
SUM(IIF(IngredientSupplierId BETWEEN 41 AND 100,1,0)) ExtensionRows
FROM dbo.IngredientSuppliers
UNION ALL
SELECT N'IngredientSupplierPriceHistories',COUNT(*),MIN(IngredientSupplierPriceHistoryId),
MAX(IngredientSupplierPriceHistoryId),SUM(IIF(IngredientSupplierPriceHistoryId BETWEEN 1 AND 21,1,0)),
SUM(IIF(IngredientSupplierPriceHistoryId BETWEEN 22 AND 114,1,0)),
SUM(IIF(IngredientSupplierPriceHistoryId BETWEEN 115 AND 294,1,0))
FROM dbo.IngredientSupplierPriceHistories;

SELECT N'Orphan Offer' Issue,COUNT(*) IssueCount FROM dbo.IngredientSuppliers o
LEFT JOIN dbo.Ingredients i ON i.IngredientId=o.IngredientId
LEFT JOIN dbo.Suppliers s ON s.SupplierId=o.SupplierId
LEFT JOIN dbo.Units u ON u.UnitId=o.UnitId
WHERE i.IngredientId IS NULL OR s.SupplierId IS NULL OR u.UnitId IS NULL
UNION ALL SELECT N'Orphan Price History',COUNT(*) FROM dbo.IngredientSupplierPriceHistories h
LEFT JOIN dbo.IngredientSuppliers o ON o.IngredientSupplierId=h.IngredientSupplierId
LEFT JOIN dbo.Units u ON u.UnitId=h.PackageUnitId
WHERE o.IngredientSupplierId IS NULL OR(h.PackageUnitId IS NOT NULL AND u.UnitId IS NULL)
UNION ALL SELECT N'Duplicate Offer',COUNT(*) FROM
(SELECT IngredientId,SupplierId FROM dbo.IngredientSuppliers GROUP BY IngredientId,SupplierId HAVING COUNT(*)>1)x
UNION ALL SELECT N'Multiple Current Price',COUNT(*) FROM
(SELECT IngredientSupplierId FROM dbo.IngredientSupplierPriceHistories WHERE IsCurrent=1
 GROUP BY IngredientSupplierId HAVING COUNT(*)>1)x
UNION ALL SELECT N'Invalid Package Or Price',COUNT(*) FROM dbo.IngredientSuppliers
WHERE PackageQuantity<=0 OR CurrentPrice<=0 OR MinimumOrderPackageCount<=0 OR LeadTimeDays<0;

/* ============================================================
   BATCH 07/12 - STORE MENU PUBLICATION AND POS CATALOG STATE

   Mapping:
     - StoreDrinks 1-6 are EF HasData and remain unchanged.
     - Store1 source drinks use StoreDrink IDs 7-20 after aliases.
     - Exact-BOM foundation and extension drinks use IDs 21-33.
     - StoreMenuItems 1-28 retain Store1 SKU markers, IDs 29-32
       publish EF exact recipes and IDs 33-54 publish extension BOMs.
     - PriceOverride stays NULL: DrinkSizes/Part1 remains authoritative.
   ============================================================ */
IF EXISTS (SELECT 1 FROM dbo.SystemSettings
           WHERE SettingKey=N'seedall_foundation_inventory_v1' AND SettingValue=N'completed')
BEGIN
 PRINT N'SeedAll Batch 07 skipped: foundation inventory v1 is already complete.';
 GOTO SeedAllBatch07Complete;
END;
BEGIN TRY
 BEGIN TRANSACTION;

 IF OBJECT_ID(N'dbo.StoreDrinks',N'U') IS NULL OR OBJECT_ID(N'dbo.StoreMenuItems',N'U') IS NULL
 OR OBJECT_ID(N'dbo.PosCatalogStates',N'U') IS NULL
  THROW 52800,N'Schema thiếu bảng bắt buộc của SeedAll Batch 07.',1;

 IF (SELECT COUNT(*) FROM dbo.StoreDrinks WHERE StoreDrinkId BETWEEN 1 AND 6)<>6
 OR EXISTS(SELECT 1 FROM (VALUES
 (1,1,1,1),(2,1,2,1),(3,2,1,1),(4,2,3,1),(5,3,2,1),(6,3,4,1)
 )x(Id,StoreId,DrinkId,Active)
 LEFT JOIN dbo.StoreDrinks sd ON sd.StoreDrinkId=x.Id
 WHERE sd.StoreDrinkId IS NULL OR sd.StoreId<>x.StoreId OR sd.DrinkId<>x.DrinkId OR sd.Active<>x.Active)
  THROW 52801,N'StoreDrinks EF IDs 1-6 thiếu hoặc khác contract migration.',1;

 DECLARE @StoreDrinkSeed TABLE(StoreDrinkId int PRIMARY KEY,StoreId int,DrinkId int,Active bit,
 UNIQUE(StoreId,DrinkId));
 INSERT @StoreDrinkSeed VALUES
(7,1,31,1),
(8,1,32,1),
(9,1,7,1),
(10,1,33,1),
(11,1,10,1),
(12,1,34,1),
(13,1,21,1),
(14,1,22,1),
(15,1,35,1),
(16,1,36,1),
(17,1,14,1),
(18,1,37,1),
(19,1,38,1),
(20,1,39,1),
(21,1,3,1),
(22,1,4,1),
(23,1,40,1),
(24,1,41,1),
(25,1,42,1),
(26,1,43,1),
(27,1,44,1),
(28,1,45,1),
(29,1,46,1),
(30,1,47,1),
(31,1,48,1),
(32,1,49,1),
(33,1,50,1),

(34,1,51,1),
(35,1,52,1),
(36,1,53,1),
(37,1,54,1),
(38,1,55,1),
(39,1,56,1),
(40,1,57,1),
(41,1,58,1),
(42,1,59,1),
(43,1,60,1),

(44,1,61,1),
(45,1,62,1),
(46,1,63,1),
(47,1,64,1),
(48,1,65,1),
(49,1,66,1),
(50,1,67,1),
(51,1,68,1),
(52,1,69,1),
(53,1,70,1),

(54,1,71,1),
(55,1,72,1),
(56,1,73,1),
(57,1,74,1),
(58,1,75,1),
(59,1,76,1),
(60,1,77,1),
(61,1,78,1),
(62,1,79,1),
(63,1,80,1);


 IF (SELECT COUNT(*) FROM @StoreDrinkSeed)<>57
 OR EXISTS(SELECT 1 FROM @StoreDrinkSeed x LEFT JOIN dbo.Drinks d ON d.DrinkId=x.DrinkId
 LEFT JOIN dbo.Stores s ON s.StoreId=x.StoreId
 WHERE d.DrinkId IS NULL OR s.StoreId IS NULL OR d.Active<>1 OR x.StoreId<>1 OR x.Active<>1)
  THROW 52802,N'Bộ StoreDrink mới sai số lượng, FK hoặc trạng thái.',1;

 IF EXISTS(SELECT 1 FROM @StoreDrinkSeed x JOIN dbo.StoreDrinks sd
 ON sd.StoreDrinkId=x.StoreDrinkId OR(sd.StoreId=x.StoreId AND sd.DrinkId=x.DrinkId)
 WHERE sd.StoreDrinkId<>x.StoreDrinkId OR sd.StoreId<>x.StoreId
 OR sd.DrinkId<>x.DrinkId OR sd.Active<>x.Active)
  THROW 52803,N'StoreDrinks có ID hoặc Store/Drink business key xung đột.',1;

 SET IDENTITY_INSERT dbo.StoreDrinks ON;
 INSERT dbo.StoreDrinks(StoreDrinkId,StoreId,DrinkId,Active)
 SELECT * FROM @StoreDrinkSeed x WHERE NOT EXISTS(SELECT 1 FROM dbo.StoreDrinks sd
 WHERE sd.StoreDrinkId=x.StoreDrinkId);
 SET IDENTITY_INSERT dbo.StoreDrinks OFF;

 DECLARE @ActorStaffId int;
 SELECT TOP(1) @ActorStaffId=s.StaffId FROM dbo.Staffs s
 JOIN dbo.Accounts a ON a.AccountId=s.AccountId AND a.Active=1
 JOIN dbo.AccountRoles ar ON ar.AccountId=a.AccountId
 JOIN dbo.Roles r ON r.RoleId=ar.RoleId AND r.Active=1
 WHERE s.StoreId=1 AND s.Active=1 AND r.Name=N'Chủ doanh nghiệp' ORDER BY s.StaffId;
 IF @ActorStaffId IS NULL THROW 52804,N'Store 1 thiếu Staff active có role Chủ doanh nghiệp.',1;

 DECLARE @MenuContract TABLE(StoreMenuItemId int PRIMARY KEY,DrinkId int,SizeId int,IsEnabled bit,
 PriceOverride decimal(18,2) NULL,EffectiveFromUtc datetime2,EffectiveToUtc datetime2 NULL,
 DisplayOrder int,PauseReason nvarchar(500) NULL,Note nvarchar(1000),PublishedAtUtc datetime2,
 PublishedByStaffId int,CreatedAtUtc datetime2,UpdatedAtUtc datetime2,UNIQUE(DrinkId,SizeId));

 INSERT @MenuContract VALUES
(1,10,3,1,NULL,'2026-01-01',NULL,51,NULL,N'DEMO_SKU_AMERICANO_L','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(2,10,2,1,NULL,'2026-01-01',NULL,50,NULL,N'DEMO_SKU_AMERICANO_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(3,7,3,1,NULL,'2026-01-01',NULL,31,NULL,N'DEMO_SKU_BAC_XIU_L','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(4,7,2,1,NULL,'2026-01-01',NULL,30,NULL,N'DEMO_SKU_BAC_XIU_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(5,34,3,1,NULL,'2026-01-01',NULL,61,NULL,N'DEMO_SKU_COFFEE_LATTE_L','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(6,34,2,1,NULL,'2026-01-01',NULL,60,NULL,N'DEMO_SKU_COFFEE_LATTE_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(7,38,3,1,NULL,'2026-01-01',NULL,131,NULL,N'DEMO_SKU_CHOCOLATE_LATTE_L','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(8,38,2,1,NULL,'2026-01-01',NULL,130,NULL,N'DEMO_SKU_CHOCOLATE_LATTE_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(9,22,3,1,NULL,'2026-01-01',NULL,81,NULL,N'DEMO_SKU_LYCHEE_TEA_L','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(10,22,2,1,NULL,'2026-01-01',NULL,80,NULL,N'DEMO_SKU_LYCHEE_TEA_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(11,39,3,1,NULL,'2026-01-01',NULL,141,NULL,N'DEMO_SKU_MATCHA_FRAPPE_L','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(12,39,2,1,NULL,'2026-01-01',NULL,140,NULL,N'DEMO_SKU_MATCHA_FRAPPE_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(13,37,3,1,NULL,'2026-01-01',NULL,121,NULL,N'DEMO_SKU_MATCHA_LATTE_L','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(14,37,2,1,NULL,'2026-01-01',NULL,120,NULL,N'DEMO_SKU_MATCHA_LATTE_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(15,14,3,1,NULL,'2026-01-01',NULL,111,NULL,N'DEMO_SKU_OOLONG_MILK_TEA_L','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(16,14,2,1,NULL,'2026-01-01',NULL,110,NULL,N'DEMO_SKU_OOLONG_MILK_TEA_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(17,35,3,1,NULL,'2026-01-01',NULL,91,NULL,N'DEMO_SKU_PASSION_TEA_L','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(18,35,2,1,NULL,'2026-01-01',NULL,90,NULL,N'DEMO_SKU_PASSION_TEA_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(19,21,3,1,NULL,'2026-01-01',NULL,71,NULL,N'DEMO_SKU_PEACH_ORANGE_TEA_L','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(20,21,2,1,NULL,'2026-01-01',NULL,70,NULL,N'DEMO_SKU_PEACH_ORANGE_TEA_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(21,33,3,1,NULL,'2026-01-01',NULL,41,NULL,N'DEMO_SKU_SALTED_COFFEE_L','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(22,33,2,1,NULL,'2026-01-01',NULL,40,NULL,N'DEMO_SKU_SALTED_COFFEE_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(23,36,3,1,NULL,'2026-01-01',NULL,101,NULL,N'DEMO_SKU_TRAD_MILK_TEA_L','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(24,36,2,1,NULL,'2026-01-01',NULL,100,NULL,N'DEMO_SKU_TRAD_MILK_TEA_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(25,31,3,1,NULL,'2026-01-01',NULL,11,NULL,N'DEMO_SKU_VIET_BLACK_L','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(26,31,2,1,NULL,'2026-01-01',NULL,10,NULL,N'DEMO_SKU_VIET_BLACK_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(27,32,3,1,NULL,'2026-01-01',NULL,21,NULL,N'DEMO_SKU_VIET_MILK_L','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(28,32,2,1,NULL,'2026-01-01',NULL,20,NULL,N'DEMO_SKU_VIET_MILK_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(29,1,1,1,NULL,'2026-01-01',NULL,1,NULL,N'SEEDALL_SKU_CF_Sua_S','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(30,2,1,1,NULL,'2026-01-01',NULL,2,NULL,N'SEEDALL_SKU_CF_Den_S','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(31,3,1,1,NULL,'2026-01-01',NULL,3,NULL,N'SEEDALL_SKU_TS_TruyenThong_S','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(32,4,1,1,NULL,'2026-01-01',NULL,4,NULL,N'SEEDALL_SKU_TS_Socola_S','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(33,40,2,1,NULL,'2026-01-01',NULL,150,NULL,N'DEMO_SKU_COLD_BREW_ORANGE_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(34,40,3,1,NULL,'2026-01-01',NULL,151,NULL,N'DEMO_SKU_COLD_BREW_ORANGE_L','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(35,41,2,1,NULL,'2026-01-01',NULL,160,NULL,N'DEMO_SKU_MOCHA_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(36,41,3,1,NULL,'2026-01-01',NULL,161,NULL,N'DEMO_SKU_MOCHA_L','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(37,42,2,1,NULL,'2026-01-01',NULL,170,NULL,N'DEMO_SKU_CARAMEL_MACCHIATO_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(38,42,3,1,NULL,'2026-01-01',NULL,171,NULL,N'DEMO_SKU_CARAMEL_MACCHIATO_L','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(39,43,2,1,NULL,'2026-01-01',NULL,180,NULL,N'DEMO_SKU_COCONUT_COFFEE_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(40,43,3,1,NULL,'2026-01-01',NULL,181,NULL,N'DEMO_SKU_COCONUT_COFFEE_L','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(41,44,2,1,NULL,'2026-01-01',NULL,190,NULL,N'DEMO_SKU_HONEY_LEMON_TEA_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(42,44,3,1,NULL,'2026-01-01',NULL,191,NULL,N'DEMO_SKU_HONEY_LEMON_TEA_L','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(43,45,2,1,NULL,'2026-01-01',NULL,200,NULL,N'DEMO_SKU_MANGO_TEA_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(44,45,3,1,NULL,'2026-01-01',NULL,201,NULL,N'DEMO_SKU_MANGO_TEA_L','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(45,46,2,1,NULL,'2026-01-01',NULL,210,NULL,N'DEMO_SKU_STRAWBERRY_MILK_TEA_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(46,46,3,1,NULL,'2026-01-01',NULL,211,NULL,N'DEMO_SKU_STRAWBERRY_MILK_TEA_L','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(47,47,2,1,NULL,'2026-01-01',NULL,220,NULL,N'DEMO_SKU_LYCHEE_OOLONG_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(48,47,3,1,NULL,'2026-01-01',NULL,221,NULL,N'DEMO_SKU_LYCHEE_OOLONG_L','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(49,48,2,1,NULL,'2026-01-01',NULL,230,NULL,N'DEMO_SKU_OAT_MATCHA_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(50,48,3,1,NULL,'2026-01-01',NULL,231,NULL,N'DEMO_SKU_OAT_MATCHA_L','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(51,49,2,1,NULL,'2026-01-01',NULL,240,NULL,N'DEMO_SKU_COCONUT_CHOCOLATE_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(52,49,3,1,NULL,'2026-01-01',NULL,241,NULL,N'DEMO_SKU_COCONUT_CHOCOLATE_L','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(53,50,2,1,NULL,'2026-01-01',NULL,250,NULL,N'DEMO_SKU_PASSION_YOGURT_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(54,50,3,1,NULL,'2026-01-01',NULL,251,NULL,N'DEMO_SKU_PASSION_YOGURT_L','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(55,51,2,1,NULL,'2026-01-01',NULL,300,NULL,N'ZZ_POS_CHEESE_CREAM_COFFEE_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(56,52,2,1,NULL,'2026-01-01',NULL,301,NULL,N'ZZ_POS_HONEY_LEMON_COLD_BREW_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(57,53,2,1,NULL,'2026-01-01',NULL,302,NULL,N'ZZ_POS_BLACK_PEARL_MILK_COFFEE_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(58,54,2,1,NULL,'2026-01-01',NULL,303,NULL,N'ZZ_POS_HONEY_OAT_ESPRESSO_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(59,55,2,1,NULL,'2026-01-01',NULL,304,NULL,N'ZZ_POS_FLAN_MILK_COFFEE_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(60,56,2,1,NULL,'2026-01-01',NULL,305,NULL,N'ZZ_POS_LYCHEE_ALOE_COLD_BREW_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(61,57,2,1,NULL,'2026-01-01',NULL,306,NULL,N'ZZ_POS_SALTED_COCONUT_ESPRESSO_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(62,58,2,1,NULL,'2026-01-01',NULL,307,NULL,N'ZZ_POS_BROWN_SUGAR_COCONUT_JELLY_COFFEE_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(63,59,2,1,NULL,'2026-01-01',NULL,308,NULL,N'ZZ_POS_KHUC_BACH_MILK_COFFEE_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(64,60,2,1,NULL,'2026-01-01',NULL,309,NULL,N'ZZ_POS_MANGO_PASSION_COLD_BREW_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),

(65,61,2,1,NULL,'2026-01-01',NULL,310,NULL,N'ZZ_POS_PEACH_ALOE_OOLONG_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(66,62,2,1,NULL,'2026-01-01',NULL,311,NULL,N'ZZ_POS_LYCHEE_CHIA_BLACK_TEA_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(67,63,2,1,NULL,'2026-01-01',NULL,312,NULL,N'ZZ_POS_MANGO_COCONUT_JELLY_OOLONG_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(68,64,2,1,NULL,'2026-01-01',NULL,313,NULL,N'ZZ_POS_ORANGE_ALOE_BLACK_TEA_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(69,65,2,1,NULL,'2026-01-01',NULL,314,NULL,N'ZZ_POS_PASSION_CHIA_TEA_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(70,66,2,1,NULL,'2026-01-01',NULL,315,NULL,N'ZZ_POS_STRAWBERRY_COCONUT_JELLY_OOLONG_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(71,67,2,1,NULL,'2026-01-01',NULL,316,NULL,N'ZZ_POS_PEACH_KHUC_BACH_TEA_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(72,68,2,1,NULL,'2026-01-01',NULL,317,NULL,N'ZZ_POS_LYCHEE_ALOE_TEA_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(73,69,2,1,NULL,'2026-01-01',NULL,318,NULL,N'ZZ_POS_MANGO_CHIA_TEA_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(74,70,2,1,NULL,'2026-01-01',NULL,319,NULL,N'ZZ_POS_ORANGE_PASSION_TEA_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),

(75,71,2,1,NULL,'2026-01-01',NULL,320,NULL,N'ZZ_POS_BROWN_SUGAR_PEARL_MILK_TEA_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(76,72,2,1,NULL,'2026-01-01',NULL,321,NULL,N'ZZ_POS_FLAN_MILK_TEA_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(77,73,2,1,NULL,'2026-01-01',NULL,322,NULL,N'ZZ_POS_KHUC_BACH_MILK_TEA_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(78,74,2,1,NULL,'2026-01-01',NULL,323,NULL,N'ZZ_POS_ALOE_MILK_TEA_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(79,75,2,1,NULL,'2026-01-01',NULL,324,NULL,N'ZZ_POS_COCONUT_JELLY_MILK_TEA_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(80,76,2,1,NULL,'2026-01-01',NULL,325,NULL,N'ZZ_POS_CHEESE_CREAM_MILK_TEA_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(81,77,2,1,NULL,'2026-01-01',NULL,326,NULL,N'ZZ_POS_STRAWBERRY_CHEESE_MATCHA_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(82,78,2,1,NULL,'2026-01-01',NULL,327,NULL,N'ZZ_POS_MANGO_COCONUT_JELLY_MATCHA_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(83,79,2,1,NULL,'2026-01-01',NULL,328,NULL,N'ZZ_POS_SALTED_CARAMEL_CHOCOLATE_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01'),
(84,80,2,1,NULL,'2026-01-01',NULL,329,NULL,N'ZZ_POS_MANGO_ALOE_YOGURT_M','2026-01-01',@ActorStaffId,'2026-01-01','2026-01-01');

 IF (SELECT COUNT(*) FROM @MenuContract)<>84 OR EXISTS(SELECT 1 FROM @MenuContract
 WHERE IsEnabled<>1 OR PriceOverride IS NOT NULL OR EffectiveToUtc IS NOT NULL
 OR DisplayOrder<0 OR PauseReason IS NOT NULL)
  THROW 52805,N'Bộ StoreMenuItem contract phải có đúng 84 SKU global-price active.',1;

 DECLARE @MenuSeed TABLE(StoreMenuItemId int PRIMARY KEY,StoreId int,DrinkSizeId int,IsEnabled bit,
 PriceOverride decimal(18,2) NULL,EffectiveFromUtc datetime2,EffectiveToUtc datetime2 NULL,
 DisplayOrder int,PauseReason nvarchar(500) NULL,Note nvarchar(1000),PublishedAtUtc datetime2,
 PublishedByStaffId int,CreatedAtUtc datetime2,UpdatedAtUtc datetime2,UNIQUE(StoreId,DrinkSizeId));

 INSERT @MenuSeed
 SELECT c.StoreMenuItemId,1,ds.DrinkSizeId,c.IsEnabled,c.PriceOverride,c.EffectiveFromUtc,c.EffectiveToUtc,
 c.DisplayOrder,c.PauseReason,c.Note,c.PublishedAtUtc,c.PublishedByStaffId,c.CreatedAtUtc,c.UpdatedAtUtc
 FROM @MenuContract c
 JOIN dbo.DrinkSizes ds ON ds.DrinkId=c.DrinkId AND ds.SizeId=c.SizeId AND ds.Active=1
 JOIN dbo.StoreDrinks sd ON sd.StoreId=1 AND sd.DrinkId=c.DrinkId AND sd.Active=1
 JOIN dbo.Recipes r ON r.DrinkId=c.DrinkId AND r.SizeId=c.SizeId AND r.ToppingId IS NULL
  AND r.Active=1 AND r.Status=N'Active';

 IF (SELECT COUNT(*) FROM @MenuSeed)<>84
  THROW 52806,N'Không resolve đủ 84 DrinkSize có StoreDrink và exact active recipe.',1;

 IF EXISTS(SELECT 1 FROM @MenuSeed x JOIN dbo.StoreMenuItems sm
 ON sm.StoreMenuItemId=x.StoreMenuItemId OR(sm.StoreId=x.StoreId AND sm.DrinkSizeId=x.DrinkSizeId)
 WHERE sm.StoreMenuItemId<>x.StoreMenuItemId OR sm.StoreId<>x.StoreId OR sm.DrinkSizeId<>x.DrinkSizeId
 OR sm.IsEnabled<>x.IsEnabled OR sm.PriceOverride IS NOT NULL
 OR sm.EffectiveFromUtc<>x.EffectiveFromUtc OR sm.EffectiveToUtc IS NOT NULL
 OR sm.DisplayOrder<>x.DisplayOrder OR sm.PauseReason IS NOT NULL OR sm.Note<>x.Note
 OR sm.PublishedAtUtc<>x.PublishedAtUtc OR sm.PublishedByStaffId<>x.PublishedByStaffId
 OR sm.CreatedAtUtc<>x.CreatedAtUtc OR sm.UpdatedAtUtc<>x.UpdatedAtUtc)
  THROW 52807,N'StoreMenuItems có ID hoặc Store/DrinkSize business key xung đột.',1;

 SET IDENTITY_INSERT dbo.StoreMenuItems ON;
 INSERT dbo.StoreMenuItems(StoreMenuItemId,StoreId,DrinkSizeId,IsEnabled,PriceOverride,
 EffectiveFromUtc,EffectiveToUtc,DisplayOrder,PauseReason,Note,PublishedAtUtc,
 PublishedByStaffId,CreatedAtUtc,UpdatedAtUtc)
 SELECT * FROM @MenuSeed x WHERE NOT EXISTS(SELECT 1 FROM dbo.StoreMenuItems sm
 WHERE sm.StoreMenuItemId=x.StoreMenuItemId);
 SET IDENTITY_INSERT dbo.StoreMenuItems OFF;

 IF EXISTS(SELECT 1 FROM @MenuSeed x JOIN dbo.DrinkSizes ds ON ds.DrinkSizeId=x.DrinkSizeId
 WHERE ds.Price<=0 OR ds.Active<>1 OR x.PriceOverride IS NOT NULL)
  THROW 52808,N'SKU publish có giá global không dương hoặc price override trái contract.',1;

 IF EXISTS(SELECT 1 FROM dbo.PosCatalogStates ps
 WHERE ps.PosCatalogStateId=1 OR ps.StoreId=1
 GROUP BY ps.PosCatalogStateId,ps.StoreId,ps.Version,ps.PayloadHash,ps.UpdatedAtUtc
 HAVING ps.PosCatalogStateId<>1 OR ps.StoreId<>1 OR ps.Version<>1
 OR ps.PayloadHash IS NOT NULL OR ps.UpdatedAtUtc<>'2026-01-01')
  THROW 52809,N'PosCatalogState Store 1 xung đột với contract SeedAll.',1;

 SET IDENTITY_INSERT dbo.PosCatalogStates ON;
 INSERT dbo.PosCatalogStates(PosCatalogStateId,StoreId,Version,PayloadHash,UpdatedAtUtc)
 SELECT 1,1,1,NULL,'2026-01-01'
 WHERE NOT EXISTS(SELECT 1 FROM dbo.PosCatalogStates WHERE PosCatalogStateId=1);
 SET IDENTITY_INSERT dbo.PosCatalogStates OFF;

 IF (SELECT COUNT(*) FROM dbo.StoreDrinks)<63 OR(SELECT COUNT(*) FROM dbo.StoreMenuItems)<84
 OR(SELECT COUNT(*) FROM dbo.PosCatalogStates)<>1
  THROW 52810,N'Row count cuối Batch 07 không đúng contract.',1;

 IF (SELECT COUNT(*) FROM dbo.StoreDrinks WHERE StoreId=1 AND Active=1)<>59
 OR EXISTS(SELECT StoreId,DrinkId FROM dbo.StoreDrinks GROUP BY StoreId,DrinkId HAVING COUNT(*)>1)
 OR EXISTS(SELECT StoreId,DrinkSizeId FROM dbo.StoreMenuItems GROUP BY StoreId,DrinkSizeId HAVING COUNT(*)>1)
 OR EXISTS(SELECT StoreId FROM dbo.PosCatalogStates GROUP BY StoreId HAVING COUNT(*)>1)
 OR EXISTS(SELECT 1 FROM dbo.StoreMenuItems sm JOIN dbo.DrinkSizes ds ON ds.DrinkSizeId=sm.DrinkSizeId
 LEFT JOIN dbo.Recipes r ON r.DrinkId=ds.DrinkId AND r.SizeId=ds.SizeId AND r.ToppingId IS NULL
 AND r.Active=1 AND r.Status=N'Active'
 WHERE sm.StoreId=1 AND sm.IsEnabled=1 AND(r.RecipeId IS NULL OR sm.PriceOverride IS NOT NULL))
  THROW 52811,N'Duplicate catalog key, missing exact BOM hoặc price override không hợp lệ.',1;

 COMMIT;
END TRY
BEGIN CATCH
 BEGIN TRY SET IDENTITY_INSERT dbo.StoreDrinks OFF; END TRY BEGIN CATCH END CATCH;
 BEGIN TRY SET IDENTITY_INSERT dbo.StoreMenuItems OFF; END TRY BEGIN CATCH END CATCH;
 BEGIN TRY SET IDENTITY_INSERT dbo.PosCatalogStates OFF; END TRY BEGIN CATCH END CATCH;
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
SeedAllBatch07Complete:
GO

/* BATCH 07 READ-ONLY VERIFICATION */
SELECT N'StoreDrinks' Entity,COUNT(*) TotalRows,MIN(StoreDrinkId) MinId,MAX(StoreDrinkId) MaxId,
SUM(IIF(StoreDrinkId BETWEEN 1 AND 6,1,0)) FoundationRows,
SUM(IIF(StoreDrinkId BETWEEN 7 AND 20,1,0)) Store1Rows,
SUM(IIF(StoreDrinkId BETWEEN 21 AND 63,1,0)) ExtensionRows FROM dbo.StoreDrinks

UNION ALL SELECT N'StoreMenuItems',COUNT(*),MIN(StoreMenuItemId),MAX(StoreMenuItemId),
SUM(IIF(StoreMenuItemId BETWEEN 29 AND 32,1,0)),SUM(IIF(StoreMenuItemId BETWEEN 1 AND 28,1,0)),
SUM(IIF(StoreMenuItemId BETWEEN 33 AND 84,1,0)) FROM dbo.StoreMenuItems

UNION ALL SELECT N'PosCatalogStates',COUNT(*),MIN(PosCatalogStateId),MAX(PosCatalogStateId),
0,SUM(IIF(PosCatalogStateId=1,1,0)),0 FROM dbo.PosCatalogStates;

SELECT N'Orphan StoreDrink' Issue,COUNT(*) IssueCount FROM dbo.StoreDrinks sd
LEFT JOIN dbo.Stores s ON s.StoreId=sd.StoreId LEFT JOIN dbo.Drinks d ON d.DrinkId=sd.DrinkId
WHERE s.StoreId IS NULL OR d.DrinkId IS NULL
UNION ALL SELECT N'Orphan StoreMenuItem',COUNT(*) FROM dbo.StoreMenuItems sm
LEFT JOIN dbo.Stores s ON s.StoreId=sm.StoreId LEFT JOIN dbo.DrinkSizes ds ON ds.DrinkSizeId=sm.DrinkSizeId
WHERE s.StoreId IS NULL OR ds.DrinkSizeId IS NULL
UNION ALL SELECT N'Missing Exact BOM',COUNT(*) FROM dbo.StoreMenuItems sm
JOIN dbo.DrinkSizes ds ON ds.DrinkSizeId=sm.DrinkSizeId
LEFT JOIN dbo.Recipes r ON r.DrinkId=ds.DrinkId AND r.SizeId=ds.SizeId AND r.ToppingId IS NULL
 AND r.Active=1 AND r.Status=N'Active'
WHERE sm.StoreId=1 AND sm.IsEnabled=1 AND r.RecipeId IS NULL
UNION ALL SELECT N'Unexpected Price Override',COUNT(*) FROM dbo.StoreMenuItems
WHERE StoreId=1 AND PriceOverride IS NOT NULL
UNION ALL SELECT N'Duplicate Menu Business Key',COUNT(*) FROM
(SELECT StoreId,DrinkSizeId FROM dbo.StoreMenuItems GROUP BY StoreId,DrinkSizeId HAVING COUNT(*)>1)x;

/* ================================================================
   BATCH 08/12 - OPENING INVENTORY AND AUDIT MOVEMENTS

   Source and mapping:
   - Store1 opening quantities/costs are retained for its 31 source
     ingredients; seven duplicate ingredient codes use canonical IDs.
   - IDs 1-2 of StoreInventories are EF rows. Their documented opening
     balance is promoted once, only from the exact migration baseline.
   - StoreInventory IDs 5-52 represent Store 1 ingredients 3-50.
   - IDs 53-60 retain the eight Store1 PreparedItem opening quantities and
     PRODUCTION_IN movements; Batch 09 binds them to completed production runs.
   - One confirmed opening document has 50 lines. One confirmed adjustment-
     out document has three small test lines.
   ================================================================ */
IF EXISTS (SELECT 1 FROM dbo.SystemSettings
           WHERE SettingKey=N'seedall_foundation_inventory_v1' AND SettingValue=N'completed')
BEGIN
 PRINT N'SeedAll Batch 08 skipped: foundation inventory v1 is already complete.';
 GOTO SeedAllBatch08Complete;
END;
BEGIN TRY
 BEGIN TRANSACTION;

 IF OBJECT_ID(N'dbo.InventoryDocuments',N'U') IS NULL
 OR OBJECT_ID(N'dbo.InventoryDocumentDetails',N'U') IS NULL
 OR OBJECT_ID(N'dbo.StoreInventories',N'U') IS NULL
 OR OBJECT_ID(N'dbo.InventoryTransactions',N'U') IS NULL
  THROW 52900,N'Schema thiếu bảng bắt buộc của SeedAll Batch 08.',1;

 DECLARE @InventoryActorStaffId int,@InventoryActorAccountId int;
 SELECT TOP(1) @InventoryActorStaffId=s.StaffId,@InventoryActorAccountId=s.AccountId
 FROM dbo.Staffs s
 JOIN dbo.Accounts a ON a.AccountId=s.AccountId AND a.Active=1
 JOIN dbo.AccountRoles ar ON ar.AccountId=a.AccountId
 JOIN dbo.Roles r ON r.RoleId=ar.RoleId AND r.Active=1
 WHERE s.StoreId=1 AND s.Active=1 AND r.Name=N'Chủ doanh nghiệp'
 ORDER BY s.StaffId;
 IF @InventoryActorStaffId IS NULL OR @InventoryActorAccountId IS NULL
  THROW 52901,N'Store 1 thiếu Staff/Account Chủ doanh nghiệp active.',1;

 IF (SELECT COUNT(*) FROM dbo.StoreInventories WHERE StoreInventoryId BETWEEN 1 AND 4)<>4
 OR EXISTS(SELECT 1 FROM (VALUES(1,1,1),(2,1,2),(3,2,1),(4,3,2))x(Id,StoreId,IngredientId)
 LEFT JOIN dbo.StoreInventories si ON si.StoreInventoryId=x.Id
 WHERE si.StoreInventoryId IS NULL OR si.StoreId<>x.StoreId OR si.IngredientId<>x.IngredientId
 OR si.RecipeId IS NOT NULL OR si.PreparedItemId IS NOT NULL OR si.ReservedQty<>0)
  THROW 52902,N'StoreInventories EF IDs 1-4 thiếu hoặc khác identity contract migration.',1;

 DECLARE @InventorySeed TABLE(
  IngredientId int PRIMARY KEY,OpeningQty decimal(18,3),AdjustmentQty decimal(18,3),
  MinStockLevel decimal(18,3),SourceUnitCost decimal(18,2) NULL,LineMarker nvarchar(100));
 INSERT @InventorySeed VALUES
 (1,100,10,20,NULL,N'SEEDALL_OPENING_ING00001'),
 (2,91200,100,15000,71.05,N'DEMO_OFFER_CONDENSED_MILK'),
 (3,8000,0,1500,240,N'DEMO_OFFER_BLACK_TEA'),
 (4,10000,0,2000,NULL,N'SEEDALL_OPENING_ING00004'),
 (5,8000,0,1500,NULL,N'SEEDALL_OPENING_ING00005'),
 (6,50000,0,8000,22,N'DEMO_OFFER_SUGAR'),
 (7,300000,500,50000,2,N'DEMO_OFFER_ICE'),
 (8,15000,0,3000,NULL,N'SEEDALL_OPENING_ING00008'),
 (9,6000,0,1000,900,N'DEMO_OFFER_MATCHA'),
 (10,30000,0,5000,95,N'DEMO_OFFER_DAIRY_CREAM'),
 (11,10000,0,2000,NULL,N'SEEDALL_OPENING_ING00011'),
 (12,10000,0,2000,NULL,N'SEEDALL_OPENING_ING00012'),
 (13,300000,0,50000,1.50,N'DEMO_OFFER_WATER'),
 (14,12000,0,2500,180,N'DEMO_OFFER_VIET_COFFEE'),
 (15,10000,0,2000,240,N'DEMO_OFFER_ESPRESSO_BEAN'),
 (16,240000,0,40000,32,N'DEMO_OFFER_FRESH_MILK'),
 (17,5000,0,500,15,N'DEMO_OFFER_SALT'),
 (18,20000,0,3000,24,N'DEMO_OFFER_SUGAR_SYRUP'),
 (19,8000,0,1500,280,N'DEMO_OFFER_OOLONG_TEA'),
 (20,60000,0,10000,80,N'DEMO_OFFER_CANNED_PEACH'),
 (21,60000,0,10000,85,N'DEMO_OFFER_CANNED_LYCHEE'),
 (22,40000,0,6000,90,N'DEMO_OFFER_PASSION_JAM'),
 (23,30000,0,5000,35,N'DEMO_OFFER_ORANGE'),
 (24,12000,0,2000,25,N'DEMO_OFFER_LEMONGRASS'),
 (25,8000,0,1500,300,N'DEMO_OFFER_CHOCOLATE'),
 (26,8000,0,1500,180,N'DEMO_OFFER_FRAPPE'),
 (27,20000,0,4000,80,N'DEMO_OFFER_BLACK_PEARL_DRY'),
 (28,500,0,80,2500,N'DEMO_OFFER_WHITE_PEARL'),
 (29,10000,0,2000,160,N'DEMO_OFFER_TARO_JELLY_POWDER'),
 (30,10000,0,2000,180,N'DEMO_OFFER_FLAN_POWDER'),
 (31,12000,0,2000,220,N'DEMO_OFFER_CHEESE_POWDER'),
 (32,1000,0,200,900,N'DEMO_OFFER_CUP_M'),
 (33,1000,0,200,1050,N'DEMO_OFFER_CUP_L'),
 (34,1000,0,200,300,N'DEMO_OFFER_LID_M'),
 (35,1000,0,200,350,N'DEMO_OFFER_LID_L'),
 (36,2000,0,400,150,N'DEMO_OFFER_STRAW'),
 (37,500,0,100,500,N'DEMO_OFFER_BAG'),
 (38,8000,0,1500,NULL,N'SEEDALL_OPENING_HONEY'),
 (39,15000,0,2500,NULL,N'SEEDALL_OPENING_YELLOW_LEMON'),
 (40,30000,0,5000,NULL,N'SEEDALL_OPENING_MANGO_PUREE'),
 (41,30000,0,5000,NULL,N'SEEDALL_OPENING_STRAWBERRY_PUREE'),
 (42,60000,0,10000,NULL,N'SEEDALL_OPENING_OAT_MILK'),
 (43,20000,0,3000,NULL,N'SEEDALL_OPENING_CARAMEL_SYRUP'),
 (44,50000,0,8000,NULL,N'SEEDALL_OPENING_COCONUT_MILK'),
 (45,30000,0,5000,NULL,N'SEEDALL_OPENING_YOGURT'),
 (46,500,0,80,NULL,N'SEEDALL_OPENING_CHEESE_CUBE'),
 (47,10000,0,2000,NULL,N'SEEDALL_OPENING_KHUC_BACH'),
 (48,40000,0,6000,NULL,N'SEEDALL_OPENING_ALOE_VERA'),
 (49,8000,0,1500,NULL,N'SEEDALL_OPENING_CHIA_SEED'),
 (50,40000,0,6000,NULL,N'SEEDALL_OPENING_COCONUT_JELLY');

 IF (SELECT COUNT(*) FROM @InventorySeed)<>50
 OR EXISTS(SELECT 1 FROM @InventorySeed WHERE OpeningQty<=0 OR AdjustmentQty<0
 OR AdjustmentQty>=OpeningQty OR MinStockLevel<0)
  THROW 52903,N'Bộ opening inventory phải có 50 nguyên liệu và số lượng hợp lệ.',1;

 DECLARE @InventoryContract TABLE(
  StoreInventoryId int PRIMARY KEY,IngredientId int UNIQUE,OpeningQty decimal(18,3),
  AdjustmentQty decimal(18,3),FinalQty decimal(18,3),MinStockLevel decimal(18,3),
  UnitCost decimal(18,2),BaseUnitId int,LineMarker nvarchar(100));
 INSERT @InventoryContract
 SELECT CASE WHEN x.IngredientId<=2 THEN x.IngredientId ELSE x.IngredientId+2 END,
 x.IngredientId,x.OpeningQty,x.AdjustmentQty,x.OpeningQty-x.AdjustmentQty,x.MinStockLevel,
 COALESCE(x.SourceUnitCost,ROUND(o.CurrentPrice/NULLIF(o.PackageQuantity*
  CASE WHEN o.UnitId=i.BaseUnitId THEN 1 ELSE uc.ToQuantity/NULLIF(uc.FromQuantity,0) END,0),2)),
 i.BaseUnitId,x.LineMarker
 FROM @InventorySeed x
 JOIN dbo.Ingredients i ON i.IngredientId=x.IngredientId AND i.Active=1
 JOIN dbo.IngredientSuppliers o ON o.IngredientId=i.IngredientId AND o.Active=1 AND o.IsPrimary=1
 LEFT JOIN dbo.UnitConversions uc ON uc.IngredientId=i.IngredientId AND uc.FromUnitId=o.UnitId
  AND uc.ToUnitId=i.BaseUnitId AND uc.Active=1;

 IF (SELECT COUNT(*) FROM @InventoryContract)<>50
 OR EXISTS(SELECT 1 FROM @InventoryContract WHERE UnitCost<=0 OR FinalQty<0)
  THROW 52904,N'Không tính được opening cost/base-unit cost cho đủ 50 nguyên liệu.',1;

 IF EXISTS(SELECT 1 FROM @InventoryContract x JOIN dbo.StoreInventories si
 ON si.StoreInventoryId=x.StoreInventoryId OR(si.StoreId=1 AND si.IngredientId=x.IngredientId)
 WHERE si.StoreInventoryId<>x.StoreInventoryId OR si.StoreId<>1 OR si.IngredientId<>x.IngredientId
 OR si.RecipeId IS NOT NULL OR si.PreparedItemId IS NOT NULL OR si.ReservedQty<>0
 OR si.MaxNegativeQty IS NOT NULL OR si.BtpIdentityState IS NOT NULL
 OR si.QuantitySemanticsStatus IS NOT NULL OR si.SupersededByStoreInventoryId IS NOT NULL
 OR si.QuantitySemanticsEvidenceType IS NOT NULL OR si.QuantitySemanticsEvidenceReference IS NOT NULL
 OR si.QuantitySemanticsReviewedAt IS NOT NULL OR si.QuantitySemanticsReviewedByAccountId IS NOT NULL
 OR NOT(
   (si.AvailableQty=x.FinalQty AND si.MinStockLevel=x.MinStockLevel AND si.LastUpdated='2026-01-02')
   OR(x.IngredientId=1 AND si.AvailableQty=100 AND si.MinStockLevel IS NULL AND si.LastUpdated='2025-01-01')
   OR(x.IngredientId=2 AND si.AvailableQty=50 AND si.MinStockLevel IS NULL AND si.LastUpdated='2025-01-01')
   OR EXISTS(
       SELECT 1
       FROM dbo.InventoryTransactions postSeed
       WHERE postSeed.StoreInventoryId=si.StoreInventoryId
         AND (postSeed.InventoryTransactionId>64 OR postSeed.CreatedAt>'2026-01-02')
   )
 ))
  THROW 52905,N'StoreInventory ingredient có ID/business key hoặc số dư xung đột.',1;

 UPDATE si SET AvailableQty=x.FinalQty,MinStockLevel=x.MinStockLevel,LastUpdated='2026-01-02'
 FROM dbo.StoreInventories si JOIN @InventoryContract x ON x.StoreInventoryId=si.StoreInventoryId
 WHERE x.IngredientId IN(1,2) AND si.AvailableQty<>x.FinalQty
   AND NOT EXISTS(
       SELECT 1
       FROM dbo.InventoryTransactions postSeed
       WHERE postSeed.StoreInventoryId=si.StoreInventoryId
         AND (postSeed.InventoryTransactionId>64 OR postSeed.CreatedAt>'2026-01-02')
   );

 SET IDENTITY_INSERT dbo.StoreInventories ON;
 INSERT dbo.StoreInventories(StoreInventoryId,StoreId,IngredientId,RecipeId,PreparedItemId,
 BtpIdentityState,QuantitySemanticsStatus,SupersededByStoreInventoryId,
 QuantitySemanticsEvidenceType,QuantitySemanticsEvidenceReference,QuantitySemanticsReviewedAt,
 QuantitySemanticsReviewedByAccountId,AvailableQty,ReservedQty,MaxNegativeQty,MinStockLevel,LastUpdated)
 SELECT x.StoreInventoryId,1,x.IngredientId,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,
 x.FinalQty,0,NULL,x.MinStockLevel,'2026-01-02'
 FROM @InventoryContract x WHERE x.IngredientId>=3
 AND NOT EXISTS(SELECT 1 FROM dbo.StoreInventories si WHERE si.StoreInventoryId=x.StoreInventoryId);

 DECLARE @PreparedInventory TABLE(StoreInventoryId int PRIMARY KEY,PreparedItemId int UNIQUE,
 RecipeId int UNIQUE,OpeningQty decimal(18,3),UnitCost decimal(18,2),
 MinStockLevel decimal(18,3),EvidenceReference nvarchar(500));
 INSERT @PreparedInventory
 SELECT 52+p.PreparedItemId,p.PreparedItemId,r.RecipeId,x.OpeningQty,x.UnitCost,x.MinStockLevel,
 N'DEMO_PRODUCTION_OPENING_'+p.Code
 FROM (VALUES
  (1,CAST(5000 AS decimal(18,3)),CAST(43 AS decimal(18,2)),CAST(500 AS decimal(18,3))),
  (2,CAST(3000 AS decimal(18,3)),CAST(112 AS decimal(18,2)),CAST(450 AS decimal(18,3))),
  (3,CAST(8000 AS decimal(18,3)),CAST(12 AS decimal(18,2)),CAST(800 AS decimal(18,3))),
  (4,CAST(8000 AS decimal(18,3)),CAST(14 AS decimal(18,2)),CAST(800 AS decimal(18,3))),
  (5,CAST(6000 AS decimal(18,3)),CAST(16 AS decimal(18,2)),CAST(600 AS decimal(18,3))),
  (6,CAST(3000 AS decimal(18,3)),CAST(69 AS decimal(18,2)),CAST(450 AS decimal(18,3))),
  (7,CAST(3000 AS decimal(18,3)),CAST(95 AS decimal(18,2)),CAST(450 AS decimal(18,3))),
  (8,CAST(100 AS decimal(18,3)),CAST(2300 AS decimal(18,2)),CAST(15 AS decimal(18,3))),
  (9,CAST(2000 AS decimal(18,3)),CAST(18 AS decimal(18,2)),CAST(250 AS decimal(18,3))),
  (10,CAST(2000 AS decimal(18,3)),CAST(22 AS decimal(18,2)),CAST(250 AS decimal(18,3))),
  (11,CAST(2000 AS decimal(18,3)),CAST(28 AS decimal(18,2)),CAST(180 AS decimal(18,3)))
 )x(PreparedItemId,OpeningQty,UnitCost,MinStockLevel)
 JOIN dbo.PreparedItems p ON p.PreparedItemId=x.PreparedItemId
 JOIN dbo.Recipes r ON r.PreparedItemId=p.PreparedItemId
  AND r.Active=1 AND r.Status=N'Active'
 WHERE p.PreparedItemId BETWEEN 1 AND 11 AND p.Active=1;
 IF (SELECT COUNT(*) FROM @PreparedInventory)<>11
  THROW 52906,N'Không resolve đúng một active Recipe cho đủ 11 PreparedItem.',1;

 IF EXISTS(SELECT 1 FROM @PreparedInventory x JOIN dbo.StoreInventories si
 ON si.StoreInventoryId=x.StoreInventoryId OR(si.StoreId=1 AND si.RecipeId=x.RecipeId)
 WHERE si.StoreInventoryId<>x.StoreInventoryId OR si.StoreId<>1 OR si.IngredientId IS NOT NULL
 OR si.RecipeId<>x.RecipeId OR si.PreparedItemId<>x.PreparedItemId OR si.BtpIdentityState<>1
 OR si.QuantitySemanticsStatus<>1 OR si.SupersededByStoreInventoryId IS NOT NULL
 OR si.QuantitySemanticsEvidenceType<>1 OR si.QuantitySemanticsEvidenceReference<>x.EvidenceReference
 OR si.QuantitySemanticsReviewedAt<>'2026-01-01'
 OR si.QuantitySemanticsReviewedByAccountId<>@InventoryActorAccountId
 OR si.ReservedQty<>0 OR si.MaxNegativeQty IS NOT NULL
 OR si.MinStockLevel<>x.MinStockLevel
 OR (
      (si.AvailableQty<>x.OpeningQty OR si.LastUpdated<>'2026-01-01')
      AND NOT EXISTS(
          SELECT 1
          FROM dbo.InventoryTransactions postSeed
          WHERE postSeed.StoreInventoryId=si.StoreInventoryId
            AND (postSeed.InventoryTransactionId>64 OR postSeed.CreatedAt>'2026-01-01')
      )
    ))
  THROW 52907,N'StoreInventory PreparedItem có identity hoặc lifecycle xung đột.',1;

 INSERT dbo.StoreInventories(StoreInventoryId,StoreId,IngredientId,RecipeId,PreparedItemId,
 BtpIdentityState,QuantitySemanticsStatus,SupersededByStoreInventoryId,
 QuantitySemanticsEvidenceType,QuantitySemanticsEvidenceReference,QuantitySemanticsReviewedAt,
 QuantitySemanticsReviewedByAccountId,AvailableQty,ReservedQty,MaxNegativeQty,MinStockLevel,LastUpdated)
 SELECT x.StoreInventoryId,1,NULL,x.RecipeId,x.PreparedItemId,1,1,NULL,1,x.EvidenceReference,
 '2026-01-01',@InventoryActorAccountId,x.OpeningQty,0,NULL,x.MinStockLevel,'2026-01-01'
 FROM @PreparedInventory x WHERE NOT EXISTS(SELECT 1 FROM dbo.StoreInventories si
 WHERE si.StoreInventoryId=x.StoreInventoryId);
 SET IDENTITY_INSERT dbo.StoreInventories OFF;

 DECLARE @OpeningTotal decimal(18,2),@AdjustmentTotal decimal(18,2);
 SELECT @OpeningTotal=SUM(ROUND(OpeningQty*UnitCost,2)) FROM @InventoryContract;
 SELECT @AdjustmentTotal=SUM(ROUND(AdjustmentQty*UnitCost,2)) FROM @InventoryContract WHERE AdjustmentQty>0;

 DECLARE @DocumentSeed TABLE(InventoryDocumentId int PRIMARY KEY,Code nvarchar(50) UNIQUE,
 StoreId int,StaffId int,DocumentDate datetime2,[Type] int,[Status] int,RequestKey nvarchar(100) UNIQUE,
 IsProcessing bit,ConfirmedAt datetime2,ConfirmedBy int,Purpose int,PartnerType int,PartnerId int NULL,
 PartnerName nvarchar(200) NULL,SupplierId int NULL,Note nvarchar(500),NegativeReason nvarchar(1000) NULL,
 TotalAmount decimal(18,2),VatAmount decimal(18,2),FinalAmount decimal(18,2));
 INSERT @DocumentSeed VALUES
 (1,N'DEMO_OPENING_STORE1_INGREDIENTS',1,@InventoryActorStaffId,'2026-01-01',8,3,
  N'DEMO_OPENING_STORE1_INGREDIENTS',0,'2026-01-01',@InventoryActorStaffId,3,0,NULL,NULL,NULL,
  N'Opening balance nguyên liệu demo Store 1',NULL,@OpeningTotal,0,@OpeningTotal),
 (2,N'SEEDALL_ADJ_OUT_20260102',1,@InventoryActorStaffId,'2026-01-02',2,3,
  N'SEEDALL_ADJ_OUT_20260102',0,'2026-01-02',@InventoryActorStaffId,10,0,NULL,NULL,NULL,
  N'Điều chỉnh giảm nhỏ để kiểm thử đối soát kho',NULL,@AdjustmentTotal,0,@AdjustmentTotal);

 IF EXISTS(SELECT 1 FROM @DocumentSeed x JOIN dbo.InventoryDocuments d
 ON d.InventoryDocumentId=x.InventoryDocumentId OR d.Code=x.Code OR d.RequestKey=x.RequestKey
 WHERE d.InventoryDocumentId<>x.InventoryDocumentId OR d.Code<>x.Code OR d.StoreId<>x.StoreId
 OR d.StaffId<>x.StaffId OR d.DocumentDate<>x.DocumentDate OR d.[Type]<>x.[Type]
 OR d.[Status]<>x.[Status] OR d.RequestKey<>x.RequestKey OR d.IsProcessing<>x.IsProcessing
 OR d.ConfirmedAt<>x.ConfirmedAt OR d.ConfirmedBy<>x.ConfirmedBy OR d.Purpose<>x.Purpose
 OR d.PartnerType<>x.PartnerType OR d.PartnerId IS NOT NULL OR d.PartnerName IS NOT NULL
 OR d.SupplierId IS NOT NULL OR d.Note<>x.Note OR d.NegativeReason IS NOT NULL
 OR d.TotalAmount<>x.TotalAmount OR d.VatAmount<>x.VatAmount OR d.FinalAmount<>x.FinalAmount)
  THROW 52908,N'InventoryDocument có ID, Code, RequestKey hoặc giá trị xung đột.',1;

 SET IDENTITY_INSERT dbo.InventoryDocuments ON;
 INSERT dbo.InventoryDocuments(InventoryDocumentId,Code,StoreId,StaffId,DocumentDate,[Type],[Status],
 RequestKey,IsProcessing,ConfirmedAt,ConfirmedBy,Purpose,PartnerType,PartnerId,PartnerName,SupplierId,
 Note,NegativeReason,TotalAmount,VatAmount,FinalAmount)
 SELECT * FROM @DocumentSeed x WHERE NOT EXISTS(SELECT 1 FROM dbo.InventoryDocuments d
 WHERE d.InventoryDocumentId=x.InventoryDocumentId);
 SET IDENTITY_INSERT dbo.InventoryDocuments OFF;

 DECLARE @DetailSeed TABLE(InventoryDocumentDetailId int PRIMARY KEY,InventoryDocumentId int,
 IngredientId int,Quantity decimal(18,3),BaseQuantity decimal(18,3),UnitId int,
 UnitPrice decimal(18,2),CostPrice decimal(18,2),CostAmount decimal(18,2),
 Note nvarchar(500),TotalAmount decimal(18,2),UNIQUE(InventoryDocumentId,IngredientId));
 INSERT @DetailSeed
 SELECT x.IngredientId,1,x.IngredientId,x.OpeningQty,x.OpeningQty,x.BaseUnitId,x.UnitCost,x.UnitCost,
 ROUND(x.OpeningQty*x.UnitCost,2),x.LineMarker,ROUND(x.OpeningQty*x.UnitCost,2)
 FROM @InventoryContract x;
 INSERT @DetailSeed
 SELECT 50+ROW_NUMBER() OVER(ORDER BY x.IngredientId),2,x.IngredientId,x.AdjustmentQty,
 x.AdjustmentQty,x.BaseUnitId,x.UnitCost,x.UnitCost,ROUND(x.AdjustmentQty*x.UnitCost,2),
 N'SEEDALL_ADJUSTMENT_OUT_'+i.Code,ROUND(x.AdjustmentQty*x.UnitCost,2)
 FROM @InventoryContract x JOIN dbo.Ingredients i ON i.IngredientId=x.IngredientId
 WHERE x.AdjustmentQty>0;
 IF (SELECT COUNT(*) FROM @DetailSeed)<>53
  THROW 52909,N'InventoryDocumentDetails phải có 50 opening và 3 adjustment lines.',1;

 IF EXISTS(SELECT 1 FROM @DetailSeed x JOIN dbo.InventoryDocumentDetails d
 ON d.InventoryDocumentDetailId=x.InventoryDocumentDetailId
 OR(d.InventoryDocumentId=x.InventoryDocumentId AND d.IngredientId=x.IngredientId)
 WHERE d.InventoryDocumentDetailId<>x.InventoryDocumentDetailId
 OR d.InventoryDocumentId<>x.InventoryDocumentId OR d.IngredientId<>x.IngredientId
 OR d.Quantity<>x.Quantity OR d.BaseQuantity<>x.BaseQuantity OR d.UnitId<>x.UnitId
 OR d.UnitPrice<>x.UnitPrice OR d.CostPrice<>x.CostPrice OR d.CostAmount<>x.CostAmount
 OR d.Note<>x.Note OR d.TotalAmount<>x.TotalAmount)
  THROW 52910,N'InventoryDocumentDetail có ID hoặc document/ingredient key xung đột.',1;

 SET IDENTITY_INSERT dbo.InventoryDocumentDetails ON;
 INSERT dbo.InventoryDocumentDetails(InventoryDocumentDetailId,InventoryDocumentId,IngredientId,
 Quantity,BaseQuantity,UnitId,UnitPrice,CostPrice,CostAmount,Note,TotalAmount)
 SELECT * FROM @DetailSeed x WHERE NOT EXISTS(SELECT 1 FROM dbo.InventoryDocumentDetails d
 WHERE d.InventoryDocumentDetailId=x.InventoryDocumentDetailId);
 SET IDENTITY_INSERT dbo.InventoryDocumentDetails OFF;

 DECLARE @TransactionSeed TABLE(InventoryTransactionId int PRIMARY KEY,StoreInventoryId int,
 [Type] int,StockStatus int,Quantity decimal(18,3),BeforeQty decimal(18,3),AfterQty decimal(18,3),
 UnitCost decimal(18,2),TotalCost decimal(18,2),InventoryDocumentId int,
 InventoryDocumentDetailId int UNIQUE,CreatedAt datetime2);
 INSERT @TransactionSeed
 SELECT x.IngredientId,x.StoreInventoryId,8,1,x.OpeningQty,0,x.OpeningQty,x.UnitCost,
 ROUND(x.OpeningQty*x.UnitCost,2),1,x.IngredientId,'2026-01-01'
 FROM @InventoryContract x;
 INSERT @TransactionSeed
 SELECT d.InventoryDocumentDetailId,dboSi.StoreInventoryId,9,1,d.BaseQuantity,x.OpeningQty,x.FinalQty,
 x.UnitCost,d.CostAmount,2,d.InventoryDocumentDetailId,'2026-01-02'
 FROM @DetailSeed d JOIN @InventoryContract x ON x.IngredientId=d.IngredientId
 JOIN dbo.StoreInventories dboSi ON dboSi.StoreId=1 AND dboSi.IngredientId=d.IngredientId
 WHERE d.InventoryDocumentId=2;

 IF (SELECT COUNT(*) FROM @TransactionSeed)<>53
 OR EXISTS(SELECT 1 FROM @TransactionSeed WHERE Quantity<=0 OR AfterQty<0
 OR TotalCost<>ROUND(Quantity*UnitCost,2))
  THROW 52911,N'InventoryTransaction contract sai số lượng hoặc giá vốn.',1;

 IF EXISTS(SELECT 1 FROM @TransactionSeed x JOIN dbo.InventoryTransactions t
 ON t.InventoryTransactionId=x.InventoryTransactionId
 OR(t.InventoryDocumentDetailId=x.InventoryDocumentDetailId AND t.[Type]=x.[Type])
 WHERE (t.InventoryTransactionId<>x.InventoryTransactionId OR t.StoreInventoryId<>x.StoreInventoryId
 OR t.[Type]<>x.[Type] OR t.StockStatus<>x.StockStatus OR t.Quantity<>x.Quantity
 OR t.BeforeQty<>x.BeforeQty OR t.AfterQty<>x.AfterQty OR t.UnitCost<>x.UnitCost
 OR t.TotalCost<>x.TotalCost OR t.InventoryDocumentId<>x.InventoryDocumentId
 OR t.InventoryDocumentDetailId<>x.InventoryDocumentDetailId
 OR t.InventoryTransferId IS NOT NULL OR t.InventoryTransferDetailId IS NOT NULL
 OR t.ReferenceOrderId IS NOT NULL OR t.ProductionRunId IS NOT NULL OR t.SourceRecipeId IS NOT NULL
 OR t.InventoryConsolidationRunId IS NOT NULL OR t.BranchReceiptLineId IS NOT NULL
 OR t.OrderRefundId IS NOT NULL OR t.CreatedAt<>x.CreatedAt))
  THROW 52912,N'InventoryTransaction có ID hoặc document-detail/type key xung đột.',1;

 SET IDENTITY_INSERT dbo.InventoryTransactions ON;
 INSERT dbo.InventoryTransactions(InventoryTransactionId,StoreInventoryId,[Type],StockStatus,
 Quantity,BeforeQty,AfterQty,UnitCost,TotalCost,InventoryDocumentId,InventoryDocumentDetailId,
 InventoryTransferId,InventoryTransferDetailId,ReferenceOrderId,ProductionRunId,SourceRecipeId,
 InventoryConsolidationRunId,BranchReceiptLineId,OrderRefundId,CreatedAt)
 SELECT x.InventoryTransactionId,x.StoreInventoryId,x.[Type],x.StockStatus,x.Quantity,x.BeforeQty,
 x.AfterQty,x.UnitCost,x.TotalCost,x.InventoryDocumentId,x.InventoryDocumentDetailId,
 NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,x.CreatedAt
 FROM @TransactionSeed x WHERE NOT EXISTS(SELECT 1 FROM dbo.InventoryTransactions t
 WHERE t.InventoryTransactionId=x.InventoryTransactionId);

 DECLARE @PreparedTransaction TABLE(InventoryTransactionId int PRIMARY KEY,StoreInventoryId int UNIQUE,
 PreparedItemId int UNIQUE,RecipeId int UNIQUE,Quantity decimal(18,3),UnitCost decimal(18,2),
 TotalCost decimal(18,2));
 INSERT @PreparedTransaction
 SELECT 53+x.PreparedItemId,x.StoreInventoryId,x.PreparedItemId,x.RecipeId,x.OpeningQty,x.UnitCost,
 ROUND(x.OpeningQty*x.UnitCost,2) FROM @PreparedInventory x;

 IF EXISTS(SELECT 1 FROM @PreparedTransaction x JOIN dbo.InventoryTransactions t
 ON t.InventoryTransactionId=x.InventoryTransactionId
 OR(t.StoreInventoryId=x.StoreInventoryId AND t.[Type]=5 AND t.SourceRecipeId=x.RecipeId)
 WHERE (t.InventoryTransactionId<>x.InventoryTransactionId OR t.StoreInventoryId<>x.StoreInventoryId
 OR t.[Type]<>5 OR t.StockStatus<>1 OR t.Quantity<>x.Quantity OR t.BeforeQty<>0
 OR t.AfterQty<>x.Quantity OR t.UnitCost<>x.UnitCost OR t.TotalCost<>x.TotalCost
 OR t.InventoryDocumentId IS NOT NULL OR t.InventoryDocumentDetailId IS NOT NULL
 OR t.InventoryTransferId IS NOT NULL OR t.InventoryTransferDetailId IS NOT NULL
 OR t.ReferenceOrderId IS NOT NULL OR t.SourceRecipeId<>x.RecipeId
 OR(t.ProductionRunId IS NOT NULL AND t.ProductionRunId<>x.PreparedItemId)
 OR t.InventoryConsolidationRunId IS NOT NULL OR t.BranchReceiptLineId IS NOT NULL
 OR t.OrderRefundId IS NOT NULL OR t.CreatedAt<>'2026-01-01')
 AND NOT EXISTS(
     SELECT 1
     FROM dbo.InventoryTransactions postSeed
     WHERE postSeed.StoreInventoryId=t.StoreInventoryId
       AND (postSeed.InventoryTransactionId>64 OR postSeed.CreatedAt>'2026-01-01')
  ))
  THROW 52915,N'PreparedItem PRODUCTION_IN movement có identity hoặc valuation xung đột.',1;

 INSERT dbo.InventoryTransactions(InventoryTransactionId,StoreInventoryId,[Type],StockStatus,
 Quantity,BeforeQty,AfterQty,UnitCost,TotalCost,InventoryDocumentId,InventoryDocumentDetailId,
 InventoryTransferId,InventoryTransferDetailId,ReferenceOrderId,ProductionRunId,SourceRecipeId,
 InventoryConsolidationRunId,BranchReceiptLineId,OrderRefundId,CreatedAt)
 SELECT x.InventoryTransactionId,x.StoreInventoryId,5,1,x.Quantity,0,x.Quantity,x.UnitCost,x.TotalCost,
 NULL,NULL,NULL,NULL,NULL,NULL,x.RecipeId,NULL,NULL,NULL,'2026-01-01'
 FROM @PreparedTransaction x WHERE NOT EXISTS(SELECT 1 FROM dbo.InventoryTransactions t
 WHERE t.InventoryTransactionId=x.InventoryTransactionId);
 SET IDENTITY_INSERT dbo.InventoryTransactions OFF;

 IF (SELECT COUNT(*) FROM dbo.InventoryDocuments)<2
 OR(SELECT COUNT(*) FROM dbo.InventoryDocumentDetails)<53
 OR(SELECT COUNT(*) FROM dbo.StoreInventories)<63
 OR(SELECT COUNT(*) FROM dbo.InventoryTransactions)<64
  THROW 52913,N'Row count cuối Batch 08 không đúng contract database sạch.',1;

 IF EXISTS(SELECT 1 FROM @InventoryContract x JOIN dbo.StoreInventories si
 ON si.StoreId=1 AND si.IngredientId=x.IngredientId
 WHERE si.AvailableQty<>x.FinalQty
   AND NOT EXISTS(
       SELECT 1
       FROM dbo.InventoryTransactions postSeed
       WHERE postSeed.StoreInventoryId=si.StoreInventoryId
         AND (postSeed.InventoryTransactionId>64 OR postSeed.CreatedAt>'2026-01-02')
   ))
 OR EXISTS(SELECT 1 FROM dbo.StoreInventories si WHERE si.StoreId=1 AND si.IngredientId IS NOT NULL
 AND si.AvailableQty<>(SELECT COALESCE(SUM(CASE WHEN t.[Type] IN(1,5,8,11,13,14,15)
 THEN t.Quantity ELSE -t.Quantity END),0) FROM dbo.InventoryTransactions t
 WHERE t.StoreInventoryId=si.StoreInventoryId))
 OR EXISTS(SELECT InventoryDocumentId,IngredientId FROM dbo.InventoryDocumentDetails
 GROUP BY InventoryDocumentId,IngredientId HAVING COUNT(*)>1)
 OR EXISTS(SELECT InventoryDocumentDetailId,[Type] FROM dbo.InventoryTransactions
 WHERE InventoryDocumentDetailId IS NOT NULL GROUP BY InventoryDocumentDetailId,[Type] HAVING COUNT(*)>1)
  THROW 52914,N'Tồn kho, movement audit hoặc business key Batch 08 không cân bằng.',1;

 COMMIT;
END TRY
BEGIN CATCH
 BEGIN TRY SET IDENTITY_INSERT dbo.StoreInventories OFF; END TRY BEGIN CATCH END CATCH;
 BEGIN TRY SET IDENTITY_INSERT dbo.InventoryDocuments OFF; END TRY BEGIN CATCH END CATCH;
 BEGIN TRY SET IDENTITY_INSERT dbo.InventoryDocumentDetails OFF; END TRY BEGIN CATCH END CATCH;
 BEGIN TRY SET IDENTITY_INSERT dbo.InventoryTransactions OFF; END TRY BEGIN CATCH END CATCH;
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
SeedAllBatch08Complete:
GO

/* BATCH 08 READ-ONLY VERIFICATION */
SELECT N'InventoryDocuments' Entity,COUNT(*) TotalRows,MIN(InventoryDocumentId) MinId,
MAX(InventoryDocumentId) MaxId FROM dbo.InventoryDocuments
UNION ALL SELECT N'InventoryDocumentDetails',COUNT(*),MIN(InventoryDocumentDetailId),
MAX(InventoryDocumentDetailId) FROM dbo.InventoryDocumentDetails
UNION ALL SELECT N'StoreInventories',COUNT(*),MIN(StoreInventoryId),MAX(StoreInventoryId)
FROM dbo.StoreInventories
UNION ALL SELECT N'InventoryTransactions',COUNT(*),MIN(InventoryTransactionId),
MAX(InventoryTransactionId) FROM dbo.InventoryTransactions;

SELECT N'Orphan Document Detail' Issue,COUNT(*) IssueCount FROM dbo.InventoryDocumentDetails d
LEFT JOIN dbo.InventoryDocuments h ON h.InventoryDocumentId=d.InventoryDocumentId
LEFT JOIN dbo.Ingredients i ON i.IngredientId=d.IngredientId
LEFT JOIN dbo.Units u ON u.UnitId=d.UnitId
WHERE h.InventoryDocumentId IS NULL OR i.IngredientId IS NULL OR u.UnitId IS NULL
UNION ALL SELECT N'Orphan Inventory Transaction',COUNT(*) FROM dbo.InventoryTransactions t
LEFT JOIN dbo.StoreInventories si ON si.StoreInventoryId=t.StoreInventoryId
LEFT JOIN dbo.InventoryDocumentDetails d ON d.InventoryDocumentDetailId=t.InventoryDocumentDetailId
WHERE si.StoreInventoryId IS NULL OR(t.InventoryDocumentDetailId IS NOT NULL AND d.InventoryDocumentDetailId IS NULL)
UNION ALL SELECT N'Store 1 Inventory Mismatch',COUNT(*) FROM dbo.StoreInventories si
WHERE si.StoreId=1 AND si.AvailableQty<>(SELECT COALESCE(SUM(CASE
 WHEN t.[Type] IN(1,5,8,11,13,14,15) THEN t.Quantity ELSE -t.Quantity END),0)
 FROM dbo.InventoryTransactions t WHERE t.StoreInventoryId=si.StoreInventoryId)
UNION ALL SELECT N'Duplicate Document Ingredient',COUNT(*) FROM(SELECT InventoryDocumentId,IngredientId
 FROM dbo.InventoryDocumentDetails GROUP BY InventoryDocumentId,IngredientId HAVING COUNT(*)>1)x
UNION ALL SELECT N'Duplicate Detail Movement Type',COUNT(*) FROM(SELECT InventoryDocumentDetailId,[Type]
 FROM dbo.InventoryTransactions WHERE InventoryDocumentDetailId IS NOT NULL
 GROUP BY InventoryDocumentDetailId,[Type] HAVING COUNT(*)>1)x;

/* ================================================================
   BATCH 09/12 - PRODUCTION VALUATION AND FIFO COST EVIDENCE

   Mapping:
   - ProductionRun IDs 1-8 retain Store1 RequestKey/fingerprint, opening
     quantity and output valuation for PreparedItem IDs 1-8.
   - CostLayer IDs 1-50 are sourced by opening detail IDs 1-50.
   - CostLayer IDs 51-58 are sourced by ProductionRun IDs 1-8.
   - Allocation IDs 1-3 consume layers 1,2,7 for adjustment details 51-53.
   - Existing PRODUCTION_IN transactions 54-61 are linked once to the
     corresponding ProductionRun; no inventory quantity is changed here.
   ================================================================ */
IF EXISTS (SELECT 1 FROM dbo.SystemSettings
           WHERE SettingKey=N'seedall_foundation_inventory_v1' AND SettingValue=N'completed')
BEGIN
 PRINT N'SeedAll Batch 09 skipped: foundation inventory v1 is already complete.';
 GOTO SeedAllBatch09Complete;
END;
BEGIN TRY
 BEGIN TRANSACTION;

 IF OBJECT_ID(N'dbo.ProductionRuns',N'U') IS NULL
 OR OBJECT_ID(N'dbo.InventoryCostLayers',N'U') IS NULL
 OR OBJECT_ID(N'dbo.InventoryCostAllocations',N'U') IS NULL
 OR OBJECT_ID(N'dbo.InventoryTransactions',N'U') IS NULL
  THROW 53000,N'Schema thiếu bảng bắt buộc của SeedAll Batch 09.',1;

 DECLARE @CostActorStaffId int;
 SELECT TOP(1) @CostActorStaffId=s.StaffId FROM dbo.Staffs s
 JOIN dbo.Accounts a ON a.AccountId=s.AccountId AND a.Active=1
 JOIN dbo.AccountRoles ar ON ar.AccountId=a.AccountId
 JOIN dbo.Roles r ON r.RoleId=ar.RoleId AND r.Active=1
 WHERE s.StoreId=1 AND s.Active=1 AND r.Name=N'Chủ doanh nghiệp' ORDER BY s.StaffId;
 IF @CostActorStaffId IS NULL
  THROW 53001,N'Store 1 thiếu Staff Chủ doanh nghiệp active cho production valuation.',1;

 DECLARE @ProductionSeed TABLE(ProductionRunId int PRIMARY KEY,PreparedItemId int UNIQUE,
 RecipeId int UNIQUE,RequestedRunCount decimal(18,5),RequestKey uniqueidentifier UNIQUE,
 RequestFingerprint char(64),OpeningQty decimal(18,3),OutputUnitCost decimal(18,8),
 TotalInputCost decimal(18,2),Notes nvarchar(500));
 INSERT @ProductionSeed
 SELECT x.ProductionRunId,x.PreparedItemId,r.RecipeId,x.RequestedRunCount,x.RequestKey,
 x.RequestFingerprint,x.OpeningQty,x.OutputUnitCost,ROUND(x.OpeningQty*x.OutputUnitCost,2),
 N'DEMO opening valuation source: '+p.Code
 FROM (VALUES
 (1,1,CAST(5 AS decimal(18,5)),'c95e9689-1266-4ad8-a89d-dd9ab65ffdfb',N'81EA4866A2B0FEB86BE46A0D7A859AC0BDA00A5B68C5E69756C2864BAA14C740',CAST(5000 AS decimal(18,3)),CAST(43 AS decimal(18,8))),
 (2,2,CAST(5 AS decimal(18,5)),'8b9ca17d-3256-4f75-9b28-ed4d08be6324',N'0C900BDE0ABCBBEAFABC127321AD4CFF275446D8EF1EC3E0F3119336FCA700C7',CAST(3000 AS decimal(18,3)),CAST(112 AS decimal(18,8))),
 (3,3,CAST(4 AS decimal(18,5)),'5274daf6-f36e-46e5-8174-62992d2f560c',N'3001CC03BB39EDA38C2F301DC4793B06252087D2A9177D31D55B0FE65759763A',CAST(8000 AS decimal(18,3)),CAST(12 AS decimal(18,8))),
 (4,4,CAST(4 AS decimal(18,5)),'6544b65d-fbf2-4b03-9848-dc403f05df7a',N'A4C1066E6E805601094B400549872F3A01E6CD8CE497CA1DC9E578C54B98D380',CAST(8000 AS decimal(18,3)),CAST(14 AS decimal(18,8))),
 (5,5,CAST(4 AS decimal(18,5)),'64e343f4-90e0-45b5-9270-2f96df6e4aca',N'0E15D8052CA4BACE1704CA8929E546E2874FFF08EEE76EF4AD6D637D421EA38B',CAST(6000 AS decimal(18,3)),CAST(16 AS decimal(18,8))),
 (6,6,CAST(3 AS decimal(18,5)),'8d0abc8f-861c-4705-bc51-36ad89e8bad2',N'CE4636D8C5B6C1A06EBAE23E7073E26992070E310FFD6AE952D008181FA95D15',CAST(3000 AS decimal(18,3)),CAST(69 AS decimal(18,8))),
 (7,7,CAST(3 AS decimal(18,5)),'c06cdaf3-8cb3-4fe5-97db-3962518dc0bf',N'2F3FCC4704FCD3F2F39EBA97FBDF7AF672F7F17896021E66B2DBF28B7D5A1AAA',CAST(3000 AS decimal(18,3)),CAST(95 AS decimal(18,8))),
 (8,8,CAST(2.5 AS decimal(18,5)),'5e0a6a7c-5b92-48d6-bf67-514be80192ef',N'418DDAE1043F07E04E434A9686DB21E5CECEC7EF8046253592C55084238014F4',CAST(100 AS decimal(18,3)),CAST(2300 AS decimal(18,8))),
 (9,9,CAST(2 AS decimal(18,5)),'5e0a6a7c-5b92-48d6-bf67-514be8019209',N'B5F5AA2F1A4A40E8D36D0D9A100000000000000000000000000000000000000',CAST(2000 AS decimal(18,3)),CAST(18 AS decimal(18,8))),
 (10,10,CAST(2 AS decimal(18,5)),'5e0a6a7c-5b92-48d6-bf67-514be8019210',N'B5F5AA2F1A4A40E8D36D0D9A100000000000000000000000000000000000010',CAST(2000 AS decimal(18,3)),CAST(22 AS decimal(18,8))),
 (11,11,CAST(2 AS decimal(18,5)),'5e0a6a7c-5b92-48d6-bf67-514be8019211',N'B5F5AA2F1A4A40E8D36D0D9A100000000000000000000000000000000000011',CAST(2000 AS decimal(18,3)),CAST(28 AS decimal(18,8)))
 )x(ProductionRunId,PreparedItemId,RequestedRunCount,RequestKey,RequestFingerprint,OpeningQty,OutputUnitCost)
 JOIN dbo.PreparedItems p ON p.PreparedItemId=x.PreparedItemId AND p.Active=1
 JOIN dbo.Recipes r ON r.PreparedItemId=p.PreparedItemId AND r.Active=1 AND r.Status=N'Active';

 IF (SELECT COUNT(*) FROM @ProductionSeed)<>11
 OR EXISTS(SELECT 1 FROM @ProductionSeed x JOIN dbo.Recipes r ON r.RecipeId=x.RecipeId
 WHERE x.RequestedRunCount<=0 OR x.RequestedRunCount>9999 OR r.OutputQuantity*x.RequestedRunCount<>x.OpeningQty)
  THROW 53002,N'ProductionRun source không khớp Recipe output hoặc opening quantity.',1;

 IF EXISTS(SELECT 1 FROM @ProductionSeed x JOIN dbo.ProductionRuns pr
 ON pr.ProductionRunId=x.ProductionRunId OR(pr.StoreId=1 AND pr.RequestKey=x.RequestKey)
 WHERE pr.ProductionRunId<>x.ProductionRunId OR pr.StoreId<>1 OR pr.RecipeId<>x.RecipeId
 OR pr.RequestedRunCount<>x.RequestedRunCount OR pr.RequestKey<>x.RequestKey
 OR pr.RequestFingerprint<>x.RequestFingerprint OR pr.Status<>2 OR pr.Notes<>x.Notes
 OR pr.CreatedByStaffId<>@CostActorStaffId OR pr.CreatedAt<>'2026-01-01'
 OR pr.ConfirmedAt<>'2026-01-01' OR pr.CompletedAt<>'2026-01-01'
 OR pr.CompletedByStaffId<>@CostActorStaffId OR pr.ValuationStatus<>1
 OR pr.TotalInputCost<>x.TotalInputCost OR pr.OutputUnitCost<>x.OutputUnitCost
 OR pr.ValuedAtUtc<>'2026-01-01')
  THROW 53003,N'ProductionRun có ID, RequestKey hoặc valuation contract xung đột.',1;

 SET IDENTITY_INSERT dbo.ProductionRuns ON;
 INSERT dbo.ProductionRuns(ProductionRunId,StoreId,RecipeId,RequestedRunCount,RequestKey,
 RequestFingerprint,Status,Notes,CreatedByStaffId,CreatedAt,ConfirmedAt,CompletedAt,
 CompletedByStaffId,ValuationStatus,TotalInputCost,OutputUnitCost,ValuedAtUtc)
 SELECT x.ProductionRunId,1,x.RecipeId,x.RequestedRunCount,x.RequestKey,x.RequestFingerprint,2,
 x.Notes,@CostActorStaffId,'2026-01-01','2026-01-01','2026-01-01',@CostActorStaffId,1,
 x.TotalInputCost,x.OutputUnitCost,'2026-01-01'
 FROM @ProductionSeed x WHERE NOT EXISTS(SELECT 1 FROM dbo.ProductionRuns pr
 WHERE pr.ProductionRunId=x.ProductionRunId);
 SET IDENTITY_INSERT dbo.ProductionRuns OFF;

 IF EXISTS(SELECT 1 FROM @ProductionSeed x JOIN dbo.InventoryTransactions t
 ON t.InventoryTransactionId=53+x.PreparedItemId
 WHERE t.StoreInventoryId<>52+x.PreparedItemId OR t.[Type]<>5 OR t.SourceRecipeId<>x.RecipeId
 OR(t.ProductionRunId IS NOT NULL AND t.ProductionRunId<>x.ProductionRunId))
 OR(SELECT COUNT(*) FROM dbo.InventoryTransactions WHERE InventoryTransactionId BETWEEN 54 AND 64)<>11
  THROW 53004,N'Không resolve được tám PRODUCTION_IN movements của Batch 08.',1;

 UPDATE t SET ProductionRunId=x.ProductionRunId
 FROM dbo.InventoryTransactions t JOIN @ProductionSeed x
 ON t.InventoryTransactionId=53+x.PreparedItemId
 WHERE t.ProductionRunId IS NULL;

 IF EXISTS(SELECT 1 FROM @ProductionSeed x JOIN dbo.InventoryTransactions t
 ON t.InventoryTransactionId=53+x.PreparedItemId
 WHERE t.ProductionRunId<>x.ProductionRunId OR t.Quantity<>x.OpeningQty
 OR t.UnitCost<>CONVERT(decimal(18,2),x.OutputUnitCost)
 OR t.TotalCost<>x.TotalInputCost OR t.BeforeQty<>0 OR t.AfterQty<>x.OpeningQty)
  THROW 53005,N'PRODUCTION_IN movement không khớp output valuation.',1;

 DECLARE @LayerSeed TABLE(InventoryCostLayerId int PRIMARY KEY,IngredientId int NULL,
 PreparedItemId int NULL,StoreId int,Quantity decimal(18,3),RemainingQuantity decimal(18,3),
 UnitCost decimal(18,2),CreatedAt datetime2,SourceProductionRunId int NULL,
 SourceOrderRefundId int NULL,SourceInventoryDocumentDetailId int NULL,
 SourceBranchReceiptLineId int NULL,SourceTransferCostAllocationId bigint NULL);
 INSERT @LayerSeed
 SELECT i.IngredientId,i.IngredientId,NULL,1,t.Quantity,
        CASE WHEN si.AvailableQty>t.Quantity THEN t.Quantity ELSE si.AvailableQty END,
        t.UnitCost,'2026-01-01',
 NULL,NULL,d.InventoryDocumentDetailId,NULL,NULL
 FROM dbo.Ingredients i
 JOIN dbo.StoreInventories si ON si.StoreId=1 AND si.IngredientId=i.IngredientId
 JOIN dbo.InventoryDocumentDetails d ON d.InventoryDocumentId=1 AND d.IngredientId=i.IngredientId
 JOIN dbo.InventoryTransactions t ON t.InventoryDocumentDetailId=d.InventoryDocumentDetailId AND t.[Type]=8
 WHERE i.IngredientId BETWEEN 1 AND 50;
 INSERT @LayerSeed
 SELECT 50+x.PreparedItemId,NULL,x.PreparedItemId,1,x.OpeningQty,x.OpeningQty,
 CONVERT(decimal(18,2),x.OutputUnitCost),'2026-01-01',x.ProductionRunId,NULL,NULL,NULL,NULL
 FROM @ProductionSeed x;

 IF (SELECT COUNT(*) FROM @LayerSeed)<>61
 OR EXISTS(SELECT 1 FROM @LayerSeed WHERE Quantity<=0 OR RemainingQuantity<0
 OR RemainingQuantity>Quantity OR UnitCost<=0
 OR NOT((IngredientId IS NOT NULL AND PreparedItemId IS NULL)
     OR(IngredientId IS NULL AND PreparedItemId IS NOT NULL)))
  THROW 53006,N'FIFO layer contract sai identity, quantity hoặc unit cost.',1;

 IF EXISTS(SELECT 1 FROM @LayerSeed x JOIN dbo.InventoryCostLayers l
 ON l.InventoryCostLayerId=x.InventoryCostLayerId
 OR(x.SourceProductionRunId IS NOT NULL AND l.SourceProductionRunId=x.SourceProductionRunId)
 OR(x.SourceInventoryDocumentDetailId IS NOT NULL AND l.SourceInventoryDocumentDetailId=x.SourceInventoryDocumentDetailId)
 WHERE (l.InventoryCostLayerId<>x.InventoryCostLayerId
 OR ISNULL(l.IngredientId,-1)<>ISNULL(x.IngredientId,-1)
 OR ISNULL(l.PreparedItemId,-1)<>ISNULL(x.PreparedItemId,-1) OR l.StoreId<>x.StoreId
 OR l.Quantity<>x.Quantity OR l.RemainingQuantity<>x.RemainingQuantity OR l.UnitCost<>x.UnitCost
 OR l.CreatedAt<>x.CreatedAt
 OR ISNULL(l.SourceProductionRunId,-1)<>ISNULL(x.SourceProductionRunId,-1)
 OR l.SourceOrderRefundId IS NOT NULL
 OR ISNULL(l.SourceInventoryDocumentDetailId,-1)<>ISNULL(x.SourceInventoryDocumentDetailId,-1)
 OR l.SourceBranchReceiptLineId IS NOT NULL OR l.SourceTransferCostAllocationId IS NOT NULL)
 AND NOT EXISTS(
     SELECT 1
     FROM dbo.InventoryTransactions postSeed
     JOIN dbo.StoreInventories postSi ON postSi.StoreInventoryId=postSeed.StoreInventoryId
     WHERE postSi.StoreId=x.StoreId
       AND ((x.IngredientId IS NOT NULL AND postSi.IngredientId=x.IngredientId)
         OR (x.PreparedItemId IS NOT NULL AND postSi.PreparedItemId=x.PreparedItemId))
       AND (postSeed.InventoryTransactionId>64 OR postSeed.CreatedAt>'2026-01-01')
 ))
  THROW 53007,N'InventoryCostLayer có ID hoặc source business key xung đột.',1;

 SET IDENTITY_INSERT dbo.InventoryCostLayers ON;
 INSERT dbo.InventoryCostLayers(InventoryCostLayerId,IngredientId,PreparedItemId,StoreId,Quantity,
 RemainingQuantity,UnitCost,CreatedAt,SourceProductionRunId,SourceOrderRefundId,
 SourceInventoryDocumentDetailId,SourceBranchReceiptLineId,SourceTransferCostAllocationId)
 SELECT * FROM @LayerSeed x WHERE NOT EXISTS(SELECT 1 FROM dbo.InventoryCostLayers l
 WHERE l.InventoryCostLayerId=x.InventoryCostLayerId);
 SET IDENTITY_INSERT dbo.InventoryCostLayers OFF;

 DECLARE @AllocationSeed TABLE(InventoryCostAllocationId int PRIMARY KEY,
 InventoryDocumentDetailId int UNIQUE,InventoryCostLayerId int UNIQUE,
 Quantity decimal(18,3),UnitCost decimal(18,2));
 INSERT @AllocationSeed
 SELECT ROW_NUMBER() OVER(ORDER BY d.IngredientId),d.InventoryDocumentDetailId,l.InventoryCostLayerId,
 d.BaseQuantity,l.UnitCost
 FROM dbo.InventoryDocumentDetails d
 JOIN dbo.InventoryCostLayers l ON l.StoreId=1 AND l.IngredientId=d.IngredientId
  AND l.PreparedItemId IS NULL AND l.InventoryCostLayerId BETWEEN 1 AND 50
 WHERE d.InventoryDocumentId=2;
 IF (SELECT COUNT(*) FROM @AllocationSeed)<>3
 OR EXISTS(SELECT 1 FROM @AllocationSeed WHERE Quantity<=0 OR UnitCost<=0)
  THROW 53008,N'Không resolve đủ ba FIFO allocation adjustment-out.',1;

 IF EXISTS(SELECT 1 FROM @AllocationSeed x JOIN dbo.InventoryCostAllocations a
 ON a.InventoryCostAllocationId=x.InventoryCostAllocationId
 OR(a.InventoryDocumentDetailId=x.InventoryDocumentDetailId
 AND a.InventoryCostLayerId=x.InventoryCostLayerId)
 WHERE a.InventoryCostAllocationId<>x.InventoryCostAllocationId
 OR a.InventoryDocumentDetailId<>x.InventoryDocumentDetailId
 OR a.InventoryCostLayerId<>x.InventoryCostLayerId OR a.Quantity<>x.Quantity
 OR a.UnitCost<>x.UnitCost)
  THROW 53009,N'InventoryCostAllocation có ID hoặc detail/layer key xung đột.',1;

 SET IDENTITY_INSERT dbo.InventoryCostAllocations ON;
 INSERT dbo.InventoryCostAllocations(InventoryCostAllocationId,InventoryDocumentDetailId,
 InventoryCostLayerId,Quantity,UnitCost)
 SELECT * FROM @AllocationSeed x WHERE NOT EXISTS(SELECT 1 FROM dbo.InventoryCostAllocations a
 WHERE a.InventoryCostAllocationId=x.InventoryCostAllocationId);
 SET IDENTITY_INSERT dbo.InventoryCostAllocations OFF;

 IF (SELECT COUNT(*) FROM dbo.ProductionRuns)<11
 OR(SELECT COUNT(*) FROM dbo.InventoryCostLayers)<61
 OR(SELECT COUNT(*) FROM dbo.InventoryCostAllocations)<3
 OR(SELECT COUNT(*) FROM dbo.InventoryTransactions)<64
  THROW 53010,N'Row count cuối Batch 09 không đúng contract database sạch.',1;

 IF NOT EXISTS(
     SELECT 1 FROM dbo.InventoryTransactions postSeed
     WHERE postSeed.InventoryTransactionId>64 OR postSeed.CreatedAt>'2026-01-01'
 )
 AND (
 EXISTS(SELECT 1 FROM dbo.StoreInventories si WHERE si.StoreId=1
 AND si.AvailableQty<>(SELECT COALESCE(SUM(l.RemainingQuantity),0)
 FROM dbo.InventoryCostLayers l WHERE l.StoreId=si.StoreId
 AND((si.IngredientId IS NOT NULL AND l.IngredientId=si.IngredientId AND l.PreparedItemId IS NULL)
 OR(si.PreparedItemId IS NOT NULL AND l.PreparedItemId=si.PreparedItemId AND l.IngredientId IS NULL))))
 OR EXISTS(SELECT 1 FROM dbo.InventoryCostLayers l WHERE l.StoreId=1
 AND l.Quantity-l.RemainingQuantity<>(SELECT COALESCE(SUM(a.Quantity),0)
 FROM dbo.InventoryCostAllocations a WHERE a.InventoryCostLayerId=l.InventoryCostLayerId))
 OR EXISTS(SELECT 1 FROM dbo.InventoryDocumentDetails d WHERE d.InventoryDocumentId=2
 AND d.BaseQuantity<>(SELECT COALESCE(SUM(a.Quantity),0) FROM dbo.InventoryCostAllocations a
 WHERE a.InventoryDocumentDetailId=d.InventoryDocumentDetailId))
 OR EXISTS(SELECT SourceProductionRunId FROM dbo.InventoryCostLayers
 WHERE SourceProductionRunId IS NOT NULL GROUP BY SourceProductionRunId HAVING COUNT(*)>1)
 OR EXISTS(SELECT SourceInventoryDocumentDetailId FROM dbo.InventoryCostLayers
 WHERE SourceInventoryDocumentDetailId IS NOT NULL GROUP BY SourceInventoryDocumentDetailId HAVING COUNT(*)>1)
 )
  THROW 53011,N'FIFO remaining, allocation hoặc inventory reconciliation không cân bằng.',1;

 COMMIT;
END TRY
BEGIN CATCH
 BEGIN TRY SET IDENTITY_INSERT dbo.ProductionRuns OFF; END TRY BEGIN CATCH END CATCH;
 BEGIN TRY SET IDENTITY_INSERT dbo.InventoryCostLayers OFF; END TRY BEGIN CATCH END CATCH;
 BEGIN TRY SET IDENTITY_INSERT dbo.InventoryCostAllocations OFF; END TRY BEGIN CATCH END CATCH;
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
SeedAllBatch09Complete:
GO

/* BATCH 09 READ-ONLY VERIFICATION */
SELECT N'ProductionRuns' Entity,COUNT(*) TotalRows,MIN(ProductionRunId) MinId,
MAX(ProductionRunId) MaxId FROM dbo.ProductionRuns
UNION ALL SELECT N'InventoryCostLayers',COUNT(*),MIN(InventoryCostLayerId),MAX(InventoryCostLayerId)
FROM dbo.InventoryCostLayers
UNION ALL SELECT N'InventoryCostAllocations',COUNT(*),MIN(InventoryCostAllocationId),
MAX(InventoryCostAllocationId) FROM dbo.InventoryCostAllocations;

SELECT N'Inventory versus FIFO mismatch' Issue,COUNT(*) IssueCount FROM dbo.StoreInventories si
WHERE si.StoreId=1 AND si.AvailableQty<>(SELECT COALESCE(SUM(l.RemainingQuantity),0)
 FROM dbo.InventoryCostLayers l WHERE l.StoreId=si.StoreId
 AND((si.IngredientId IS NOT NULL AND l.IngredientId=si.IngredientId AND l.PreparedItemId IS NULL)
 OR(si.PreparedItemId IS NOT NULL AND l.PreparedItemId=si.PreparedItemId AND l.IngredientId IS NULL)))
UNION ALL SELECT N'Layer allocation mismatch',COUNT(*) FROM dbo.InventoryCostLayers l
WHERE l.StoreId=1 AND l.Quantity-l.RemainingQuantity<>(SELECT COALESCE(SUM(a.Quantity),0)
 FROM dbo.InventoryCostAllocations a WHERE a.InventoryCostLayerId=l.InventoryCostLayerId)
UNION ALL SELECT N'Adjustment detail allocation mismatch',COUNT(*) FROM dbo.InventoryDocumentDetails d
WHERE d.InventoryDocumentId=2 AND d.BaseQuantity<>(SELECT COALESCE(SUM(a.Quantity),0)
 FROM dbo.InventoryCostAllocations a WHERE a.InventoryDocumentDetailId=d.InventoryDocumentDetailId)
UNION ALL SELECT N'Production movement mismatch',COUNT(*) FROM dbo.ProductionRuns pr
LEFT JOIN dbo.InventoryTransactions t ON t.ProductionRunId=pr.ProductionRunId AND t.[Type]=5
WHERE t.InventoryTransactionId IS NULL OR t.Quantity*ISNULL(t.UnitCost,0)<>pr.TotalInputCost;

/* ================================================================
   BATCH 10/12 - IMMUTABLE DOCUMENT SNAPSHOTS AND STOCK TAKE

   Source and mapping:
   - Snapshot IDs 1-2 map one-to-one to confirmed documents 1-2.
   - SnapshotDetail IDs 1-53 map one-to-one to document details 1-53.
   - StockTakeSession ID 1 is a fixed Store 1 count on 2026-01-03.
   - Six detail rows cover three matching and three differing counts.
   - Stock take rows are observations only; this batch does not mutate stock.
   ================================================================ */
IF EXISTS (SELECT 1 FROM dbo.SystemSettings
           WHERE SettingKey=N'seedall_foundation_inventory_v1' AND SettingValue=N'completed')
BEGIN
 PRINT N'SeedAll Batch 10 skipped: foundation inventory v1 is already complete.';
 GOTO SeedAllBatch10Complete;
END;
BEGIN TRY
 BEGIN TRANSACTION;

 IF OBJECT_ID(N'dbo.InventoryDocumentSnapshots',N'U') IS NULL
 OR OBJECT_ID(N'dbo.InventoryDocumentSnapshotDetails',N'U') IS NULL
 OR OBJECT_ID(N'dbo.StockTakeSessions',N'U') IS NULL
 OR OBJECT_ID(N'dbo.StockTakeDetails',N'U') IS NULL
  THROW 53100,N'Schema thiếu bảng bắt buộc của SeedAll Batch 10.',1;

 DECLARE @StockTakeActorStaffId int;
 SELECT TOP(1) @StockTakeActorStaffId=s.StaffId FROM dbo.Staffs s
 JOIN dbo.Accounts a ON a.AccountId=s.AccountId AND a.Active=1
 JOIN dbo.AccountRoles ar ON ar.AccountId=a.AccountId
 JOIN dbo.Roles r ON r.RoleId=ar.RoleId AND r.Active=1
 WHERE s.StoreId=1 AND s.Active=1 AND r.Name=N'Chủ doanh nghiệp' ORDER BY s.StaffId;
 IF @StockTakeActorStaffId IS NULL
  THROW 53101,N'Store 1 thiếu Staff Chủ doanh nghiệp active cho snapshot/stock take.',1;

 DECLARE @SnapshotSeed TABLE(InventoryDocumentSnapshotId int PRIMARY KEY,
 InventoryDocumentId int UNIQUE,[Type] int,Purpose int,[Status] int,NegativeApprovalId bigint NULL,
 BeforeQty decimal(18,3) NULL,AfterQty decimal(18,3) NULL,EffectiveMaxNegativeQty decimal(18,3) NULL,
 PolicyVersion nvarchar(100) NULL,CostComplete bit,Code nvarchar(50),DocumentDate datetime2,
 StoreName nvarchar(200),StaffName nvarchar(200),PartnerName nvarchar(200) NULL,
 TotalAmount decimal(18,2),VatAmount decimal(18,2),FinalAmount decimal(18,2),CreatedAt datetime2);
 INSERT @SnapshotSeed
 SELECT d.InventoryDocumentId,d.InventoryDocumentId,d.[Type],d.Purpose,d.[Status],NULL,NULL,NULL,NULL,NULL,
 CONVERT(bit,CASE WHEN NOT EXISTS(SELECT 1 FROM dbo.InventoryDocumentDetails dd
  WHERE dd.InventoryDocumentId=d.InventoryDocumentId
  AND(dd.CostPrice IS NULL OR dd.CostAmount IS NULL)) THEN 1 ELSE 0 END),
 d.Code,d.DocumentDate,s.Name,st.FullName,d.PartnerName,COALESCE(d.TotalAmount,0),
 COALESCE(d.VatAmount,0),COALESCE(d.FinalAmount,0),'2026-01-03'
 FROM dbo.InventoryDocuments d JOIN dbo.Stores s ON s.StoreId=d.StoreId
 JOIN dbo.Staffs st ON st.StaffId=d.StaffId
 WHERE d.InventoryDocumentId IN(1,2) AND d.[Status]=3;

 IF (SELECT COUNT(*) FROM @SnapshotSeed)<>2 OR EXISTS(SELECT 1 FROM @SnapshotSeed
 WHERE CostComplete<>1 OR LEN(Code)=0 OR LEN(StoreName)=0 OR LEN(StaffName)=0)
  THROW 53102,N'Không resolve đủ hai confirmed document snapshot có cost hoàn chỉnh.',1;

 IF EXISTS(SELECT 1 FROM @SnapshotSeed x JOIN dbo.InventoryDocumentSnapshots s
 ON s.InventoryDocumentSnapshotId=x.InventoryDocumentSnapshotId
 OR s.InventoryDocumentId=x.InventoryDocumentId
 WHERE s.InventoryDocumentSnapshotId<>x.InventoryDocumentSnapshotId
 OR s.InventoryDocumentId<>x.InventoryDocumentId OR s.[Type]<>x.[Type]
 OR s.Purpose<>x.Purpose OR s.[Status]<>x.[Status] OR s.NegativeApprovalId IS NOT NULL
 OR s.BeforeQty IS NOT NULL OR s.AfterQty IS NOT NULL OR s.EffectiveMaxNegativeQty IS NOT NULL
 OR s.PolicyVersion IS NOT NULL OR s.CostComplete<>x.CostComplete OR s.Code<>x.Code
 OR s.DocumentDate<>x.DocumentDate OR s.StoreName<>x.StoreName OR s.StaffName<>x.StaffName
 OR ISNULL(s.PartnerName,N'')<>ISNULL(x.PartnerName,N'') OR s.TotalAmount<>x.TotalAmount
 OR s.VatAmount<>x.VatAmount OR s.FinalAmount<>x.FinalAmount OR s.CreatedAt<>x.CreatedAt)
  THROW 53103,N'InventoryDocumentSnapshot có ID/document key hoặc nội dung xung đột.',1;

 SET IDENTITY_INSERT dbo.InventoryDocumentSnapshots ON;
 INSERT dbo.InventoryDocumentSnapshots(InventoryDocumentSnapshotId,InventoryDocumentId,[Type],Purpose,
 [Status],NegativeApprovalId,BeforeQty,AfterQty,EffectiveMaxNegativeQty,PolicyVersion,CostComplete,
 Code,DocumentDate,StoreName,StaffName,PartnerName,TotalAmount,VatAmount,FinalAmount,CreatedAt)
 SELECT * FROM @SnapshotSeed x WHERE NOT EXISTS(SELECT 1 FROM dbo.InventoryDocumentSnapshots s
 WHERE s.InventoryDocumentSnapshotId=x.InventoryDocumentSnapshotId);
 SET IDENTITY_INSERT dbo.InventoryDocumentSnapshots OFF;

 DECLARE @SnapshotDetailSeed TABLE(Id int PRIMARY KEY,InventoryDocumentSnapshotId int,
 ItemName nvarchar(200),UnitName nvarchar(100),Quantity decimal(18,3),
 UnitPrice decimal(18,2),TotalAmount decimal(18,2));
 INSERT @SnapshotDetailSeed
 SELECT d.InventoryDocumentDetailId,d.InventoryDocumentId,i.Name,u.Name,d.Quantity,
 COALESCE(d.UnitPrice,0),COALESCE(d.TotalAmount,0)
 FROM dbo.InventoryDocumentDetails d JOIN dbo.Ingredients i ON i.IngredientId=d.IngredientId
 JOIN dbo.Units u ON u.UnitId=d.UnitId
 WHERE d.InventoryDocumentId IN(1,2);
 IF (SELECT COUNT(*) FROM @SnapshotDetailSeed)<>53
  THROW 53104,N'Không resolve đủ 53 snapshot detail từ chứng từ Batch 08.',1;

 IF EXISTS(SELECT 1 FROM @SnapshotDetailSeed x JOIN dbo.InventoryDocumentSnapshotDetails d
 ON d.Id=x.Id
 WHERE d.InventoryDocumentSnapshotId<>x.InventoryDocumentSnapshotId OR d.ItemName<>x.ItemName
 OR d.UnitName<>x.UnitName OR d.Quantity<>x.Quantity OR d.UnitPrice<>x.UnitPrice
 OR d.TotalAmount<>x.TotalAmount)
 OR EXISTS(SELECT 1 FROM @SnapshotDetailSeed x JOIN dbo.InventoryDocumentSnapshotDetails d
 ON d.InventoryDocumentSnapshotId=x.InventoryDocumentSnapshotId AND d.ItemName=x.ItemName
 WHERE d.Id<>x.Id)
  THROW 53105,N'InventoryDocumentSnapshotDetail có ID hoặc snapshot/item key xung đột.',1;

 SET IDENTITY_INSERT dbo.InventoryDocumentSnapshotDetails ON;
 INSERT dbo.InventoryDocumentSnapshotDetails(Id,InventoryDocumentSnapshotId,ItemName,UnitName,
 Quantity,UnitPrice,TotalAmount)
 SELECT * FROM @SnapshotDetailSeed x WHERE NOT EXISTS(SELECT 1
 FROM dbo.InventoryDocumentSnapshotDetails d WHERE d.Id=x.Id);
 SET IDENTITY_INSERT dbo.InventoryDocumentSnapshotDetails OFF;

 DECLARE @StockTakeSessionSeed TABLE(StockTakeSessionId int PRIMARY KEY,StoreId int,StaffId int,
 Code nvarchar(50) UNIQUE,CreatedAt datetime2);
 INSERT @StockTakeSessionSeed VALUES
 (1,1,@StockTakeActorStaffId,N'SEEDALL_STOCKTAKE_20260103','2026-01-03');
 IF EXISTS(SELECT 1 FROM @StockTakeSessionSeed x JOIN dbo.StockTakeSessions s
 ON s.StockTakeSessionId=x.StockTakeSessionId OR s.Code=x.Code
 WHERE s.StockTakeSessionId<>x.StockTakeSessionId OR s.StoreId<>x.StoreId
 OR s.StaffId<>x.StaffId OR s.Code<>x.Code OR s.CreatedAt<>x.CreatedAt)
  THROW 53106,N'StockTakeSession có ID hoặc Code xung đột.',1;

 SET IDENTITY_INSERT dbo.StockTakeSessions ON;
 INSERT dbo.StockTakeSessions(StockTakeSessionId,StoreId,StaffId,Code,CreatedAt)
 SELECT * FROM @StockTakeSessionSeed x WHERE NOT EXISTS(SELECT 1 FROM dbo.StockTakeSessions s
 WHERE s.StockTakeSessionId=x.StockTakeSessionId);
 SET IDENTITY_INSERT dbo.StockTakeSessions OFF;

 DECLARE @StockTakeDetailSeed TABLE(StockTakeDetailId int PRIMARY KEY,StockTakeSessionId int,
 IngredientId int UNIQUE,SystemQuantity decimal(18,3),ActualQuantity decimal(18,3),Note nvarchar(500) NULL);
 INSERT @StockTakeDetailSeed
 SELECT x.StockTakeDetailId,1,x.IngredientId,si.AvailableQty,
 si.AvailableQty+x.DifferenceQty,x.Note
 FROM (VALUES
 (1,1,CAST(0 AS decimal(18,3)),N'Khớp tồn hệ thống'),
 (2,2,CAST(-50 AS decimal(18,3)),N'Lệch thiếu 50 ml khi kiểm đếm'),
 (3,7,CAST(100 AS decimal(18,3)),N'Lệch thừa 100 g đá viên'),
 (4,14,CAST(0 AS decimal(18,3)),N'Khớp tồn hệ thống'),
 (5,32,CAST(-5 AS decimal(18,3)),N'Lệch thiếu 5 ly do hư hỏng'),
 (6,42,CAST(0 AS decimal(18,3)),N'Khớp tồn hệ thống')
 )x(StockTakeDetailId,IngredientId,DifferenceQty,Note)
 JOIN dbo.StoreInventories si ON si.StoreId=1 AND si.IngredientId=x.IngredientId;
 IF (SELECT COUNT(*) FROM @StockTakeDetailSeed)<>6
 OR EXISTS(SELECT 1 FROM @StockTakeDetailSeed WHERE SystemQuantity<0 OR ActualQuantity<0)
 OR(SELECT COUNT(*) FROM @StockTakeDetailSeed WHERE ActualQuantity=SystemQuantity)<>3
 OR(SELECT COUNT(*) FROM @StockTakeDetailSeed WHERE ActualQuantity<>SystemQuantity)<>3
  THROW 53107,N'StockTakeDetails phải có sáu dòng gồm ba khớp và ba lệch hợp lệ.',1;

 IF EXISTS(SELECT 1 FROM @StockTakeDetailSeed x JOIN dbo.StockTakeDetails d
 ON d.StockTakeDetailId=x.StockTakeDetailId
 OR(d.StockTakeSessionId=x.StockTakeSessionId AND d.IngredientId=x.IngredientId)
 WHERE (d.StockTakeDetailId<>x.StockTakeDetailId
 OR d.StockTakeSessionId<>x.StockTakeSessionId OR d.IngredientId<>x.IngredientId
 OR d.SystemQuantity<>x.SystemQuantity OR d.ActualQuantity<>x.ActualQuantity
 OR ISNULL(d.Note,N'')<>ISNULL(x.Note,N''))
 AND NOT EXISTS(
     SELECT 1
     FROM dbo.InventoryTransactions postSeed
     JOIN dbo.StoreInventories postSi ON postSi.StoreInventoryId=postSeed.StoreInventoryId
     WHERE postSi.StoreId=1 AND postSi.IngredientId=x.IngredientId
       AND (postSeed.InventoryTransactionId>64 OR postSeed.CreatedAt>'2026-01-01')
 ))
  THROW 53108,N'StockTakeDetail có ID hoặc session/ingredient key xung đột.',1;

 SET IDENTITY_INSERT dbo.StockTakeDetails ON;
 INSERT dbo.StockTakeDetails(StockTakeDetailId,StockTakeSessionId,IngredientId,
 SystemQuantity,ActualQuantity,Note)
 SELECT * FROM @StockTakeDetailSeed x WHERE NOT EXISTS(SELECT 1 FROM dbo.StockTakeDetails d
 WHERE d.StockTakeDetailId=x.StockTakeDetailId);
 SET IDENTITY_INSERT dbo.StockTakeDetails OFF;

 IF (SELECT COUNT(*) FROM dbo.InventoryDocumentSnapshots)<>2
 OR(SELECT COUNT(*) FROM dbo.InventoryDocumentSnapshotDetails)<>53
 OR(SELECT COUNT(*) FROM dbo.StockTakeSessions)<>1
 OR(SELECT COUNT(*) FROM dbo.StockTakeDetails)<>6
  THROW 53109,N'Row count cuối Batch 10 không đúng contract database sạch.',1;

 IF EXISTS(SELECT InventoryDocumentId FROM dbo.InventoryDocumentSnapshots
 GROUP BY InventoryDocumentId HAVING COUNT(*)>1)
 OR EXISTS(SELECT 1 FROM dbo.InventoryDocumentSnapshots s
 WHERE (SELECT COUNT(*) FROM dbo.InventoryDocumentSnapshotDetails d
  WHERE d.InventoryDocumentSnapshotId=s.InventoryDocumentSnapshotId)
 <>(SELECT COUNT(*) FROM dbo.InventoryDocumentDetails d
  WHERE d.InventoryDocumentId=s.InventoryDocumentId))
 OR EXISTS(SELECT StockTakeSessionId,IngredientId FROM dbo.StockTakeDetails
 GROUP BY StockTakeSessionId,IngredientId HAVING COUNT(*)>1)
 OR EXISTS(SELECT 1 FROM dbo.StockTakeDetails d JOIN dbo.StockTakeSessions s
 ON s.StockTakeSessionId=d.StockTakeSessionId WHERE s.StoreId<>1)
  THROW 53110,N'Snapshot completeness hoặc stock-take business key không hợp lệ.',1;

 COMMIT;
END TRY
BEGIN CATCH
 BEGIN TRY SET IDENTITY_INSERT dbo.InventoryDocumentSnapshots OFF; END TRY BEGIN CATCH END CATCH;
 BEGIN TRY SET IDENTITY_INSERT dbo.InventoryDocumentSnapshotDetails OFF; END TRY BEGIN CATCH END CATCH;
 BEGIN TRY SET IDENTITY_INSERT dbo.StockTakeSessions OFF; END TRY BEGIN CATCH END CATCH;
 BEGIN TRY SET IDENTITY_INSERT dbo.StockTakeDetails OFF; END TRY BEGIN CATCH END CATCH;
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
SeedAllBatch10Complete:
GO

/* BATCH 10 READ-ONLY VERIFICATION */
SELECT N'InventoryDocumentSnapshots' Entity,COUNT(*) TotalRows,MIN(InventoryDocumentSnapshotId) MinId,
MAX(InventoryDocumentSnapshotId) MaxId FROM dbo.InventoryDocumentSnapshots
UNION ALL SELECT N'InventoryDocumentSnapshotDetails',COUNT(*),MIN(Id),MAX(Id)
FROM dbo.InventoryDocumentSnapshotDetails
UNION ALL SELECT N'StockTakeSessions',COUNT(*),MIN(StockTakeSessionId),MAX(StockTakeSessionId)
FROM dbo.StockTakeSessions
UNION ALL SELECT N'StockTakeDetails',COUNT(*),MIN(StockTakeDetailId),MAX(StockTakeDetailId)
FROM dbo.StockTakeDetails;

SELECT N'Orphan Snapshot Detail' Issue,COUNT(*) IssueCount FROM dbo.InventoryDocumentSnapshotDetails d
LEFT JOIN dbo.InventoryDocumentSnapshots s ON s.InventoryDocumentSnapshotId=d.InventoryDocumentSnapshotId
WHERE s.InventoryDocumentSnapshotId IS NULL
UNION ALL SELECT N'Snapshot Detail Count Mismatch',COUNT(*) FROM dbo.InventoryDocumentSnapshots s
WHERE (SELECT COUNT(*) FROM dbo.InventoryDocumentSnapshotDetails d
 WHERE d.InventoryDocumentSnapshotId=s.InventoryDocumentSnapshotId)
<>(SELECT COUNT(*) FROM dbo.InventoryDocumentDetails d WHERE d.InventoryDocumentId=s.InventoryDocumentId)
UNION ALL SELECT N'Orphan StockTake Detail',COUNT(*) FROM dbo.StockTakeDetails d
LEFT JOIN dbo.StockTakeSessions s ON s.StockTakeSessionId=d.StockTakeSessionId
LEFT JOIN dbo.Ingredients i ON i.IngredientId=d.IngredientId
WHERE s.StockTakeSessionId IS NULL OR i.IngredientId IS NULL
UNION ALL SELECT N'Duplicate StockTake Ingredient',COUNT(*) FROM(SELECT StockTakeSessionId,IngredientId
FROM dbo.StockTakeDetails GROUP BY StockTakeSessionId,IngredientId HAVING COUNT(*)>1)x;

/* ================================================================
   BATCH 11/12 - DRAFT STORE-TO-STORE TRANSFER

   Contract:
   - One draft transfer from Store 1 to Store 2, fixed at 2026-01-04.
   - Three ingredient lines use each ingredient's base unit and FIFO unit cost.
   - Draft lines have zero dispatched/received quantities and no stock snapshot.
   - No transfer movement or completed workflow is synthesized by seed SQL.
   ================================================================ */
IF EXISTS (SELECT 1 FROM dbo.SystemSettings
           WHERE SettingKey=N'seedall_foundation_inventory_v1' AND SettingValue=N'completed')
BEGIN
 PRINT N'SeedAll Batch 11 skipped: foundation inventory v1 is already complete.';
 GOTO SeedAllBatch11Complete;
END;
BEGIN TRY
 BEGIN TRANSACTION;

 IF OBJECT_ID(N'dbo.InventoryTransfers',N'U') IS NULL
 OR OBJECT_ID(N'dbo.InventoryTransferDetails',N'U') IS NULL
  THROW 53200,N'Schema thiếu bảng bắt buộc của SeedAll Batch 11.',1;

 DECLARE @TransferActorStaffId int;
 SELECT TOP(1) @TransferActorStaffId=s.StaffId FROM dbo.Staffs s
 JOIN dbo.Accounts a ON a.AccountId=s.AccountId AND a.Active=1
 JOIN dbo.AccountRoles ar ON ar.AccountId=a.AccountId
 JOIN dbo.Roles r ON r.RoleId=ar.RoleId AND r.Active=1
 WHERE s.StoreId=1 AND s.Active=1 AND r.Name=N'Chủ doanh nghiệp' ORDER BY s.StaffId;
 IF @TransferActorStaffId IS NULL
 OR NOT EXISTS(SELECT 1 FROM dbo.Stores WHERE StoreId=2 AND Active=1)
  THROW 53201,N'Thiếu Staff Store 1 hoặc Store 2 active cho transfer draft.',1;

 DECLARE @TransferSeed TABLE(InventoryTransferId int PRIMARY KEY,Code nvarchar(50) UNIQUE,
 RequestKey nvarchar(100) UNIQUE,FromStoreId int,ToStoreId int,[Type] int,Purpose int,[Status] int,
 DocumentDate datetime2,CreatedByStaffId int,ConfirmedByStaffId int NULL,CancelledByStaffId int NULL,
 ConfirmedAt datetime2 NULL,DispatchedAt datetime2 NULL,CancelledAt datetime2 NULL,
 CreatedAt datetime2,Note nvarchar(500));
 INSERT @TransferSeed VALUES
 (1,N'SEEDALL_TRANSFER_20260104',N'SEEDALL_TRANSFER_20260104',1,2,1,1,1,
 '2026-01-04',@TransferActorStaffId,NULL,NULL,NULL,NULL,NULL,'2026-01-04',
 N'Phiếu chuyển kho draft Store 1 sang Store 2 để kiểm thử workflow');

 IF EXISTS(SELECT 1 FROM @TransferSeed x JOIN dbo.InventoryTransfers t
 ON t.InventoryTransferId=x.InventoryTransferId OR t.Code=x.Code OR t.RequestKey=x.RequestKey
 WHERE t.InventoryTransferId<>x.InventoryTransferId OR t.Code<>x.Code OR t.RequestKey<>x.RequestKey
 OR t.FromStoreId<>x.FromStoreId OR t.ToStoreId<>x.ToStoreId OR t.[Type]<>x.[Type]
 OR t.Purpose<>x.Purpose OR t.[Status]<>x.[Status] OR t.DocumentDate<>x.DocumentDate
 OR t.CreatedByStaffId<>x.CreatedByStaffId OR t.ConfirmedByStaffId IS NOT NULL
 OR t.CancelledByStaffId IS NOT NULL OR t.ConfirmedAt IS NOT NULL
 OR t.DispatchedAt IS NOT NULL OR t.CancelledAt IS NOT NULL
 OR t.CreatedAt<>x.CreatedAt OR t.Note<>x.Note)
  THROW 53202,N'InventoryTransfer có ID, Code, RequestKey hoặc lifecycle xung đột.',1;

 SET IDENTITY_INSERT dbo.InventoryTransfers ON;
 INSERT dbo.InventoryTransfers(InventoryTransferId,Code,RequestKey,FromStoreId,ToStoreId,[Type],
 Purpose,[Status],DocumentDate,CreatedByStaffId,ConfirmedByStaffId,CancelledByStaffId,
 ConfirmedAt,DispatchedAt,CancelledAt,CreatedAt,Note)
 SELECT * FROM @TransferSeed x WHERE NOT EXISTS(SELECT 1 FROM dbo.InventoryTransfers t
 WHERE t.InventoryTransferId=x.InventoryTransferId);
 SET IDENTITY_INSERT dbo.InventoryTransfers OFF;

 DECLARE @TransferDetailSeed TABLE(InventoryTransferDetailId int PRIMARY KEY,
 InventoryTransferId int,IngredientId int UNIQUE,PreparedItemId int NULL,RestockRequestId int NULL,
 RestockRequestFulfillmentId int NULL,UnitId int,Quantity decimal(18,3),BaseQuantity decimal(18,3),
 DispatchedBaseQuantity decimal(18,3),ReceivedBaseQuantity decimal(18,3),
 SourceBeforeQty decimal(18,3) NULL,SourceAfterQty decimal(18,3) NULL,
 DestinationBeforeQty decimal(18,3) NULL,DestinationAfterQty decimal(18,3) NULL,
 UnitPrice decimal(18,2),Note nvarchar(500));
 INSERT @TransferDetailSeed
 SELECT x.DetailId,1,x.IngredientId,NULL,NULL,NULL,i.BaseUnitId,x.Quantity,x.Quantity,0,0,
 NULL,NULL,NULL,NULL,l.UnitCost,x.Note
 FROM (VALUES
 (1,1,CAST(20 AS decimal(18,3)),N'Draft: cà phê hạt theo base unit g'),
 (2,2,CAST(500 AS decimal(18,3)),N'Draft: sữa đặc theo base unit ml'),
 (3,32,CAST(20 AS decimal(18,3)),N'Draft: ly M theo base unit pcs')
 )x(DetailId,IngredientId,Quantity,Note)
 JOIN dbo.Ingredients i ON i.IngredientId=x.IngredientId AND i.Active=1
 JOIN dbo.StoreInventories si ON si.StoreId=1 AND si.IngredientId=i.IngredientId
 JOIN dbo.InventoryCostLayers l ON l.StoreId=1 AND l.IngredientId=i.IngredientId
  AND l.PreparedItemId IS NULL AND l.RemainingQuantity>=x.Quantity;

 IF (SELECT COUNT(*) FROM @TransferDetailSeed)<>3
 OR EXISTS(SELECT 1 FROM @TransferDetailSeed WHERE Quantity<=0 OR BaseQuantity<=0
 OR DispatchedBaseQuantity<>0 OR ReceivedBaseQuantity<>0 OR UnitPrice<0)
  THROW 53203,N'Không resolve đủ ba transfer detail có quantity/FIFO cost hợp lệ.',1;

 IF EXISTS(SELECT 1 FROM @TransferDetailSeed x JOIN dbo.InventoryTransferDetails d
 ON d.InventoryTransferDetailId=x.InventoryTransferDetailId
 OR(d.InventoryTransferId=x.InventoryTransferId AND d.IngredientId=x.IngredientId)
 WHERE d.InventoryTransferDetailId<>x.InventoryTransferDetailId
 OR d.InventoryTransferId<>x.InventoryTransferId OR d.IngredientId<>x.IngredientId
 OR d.PreparedItemId IS NOT NULL OR d.RestockRequestId IS NOT NULL
 OR d.RestockRequestFulfillmentId IS NOT NULL OR d.UnitId<>x.UnitId
 OR d.Quantity<>x.Quantity OR d.BaseQuantity<>x.BaseQuantity
 OR d.DispatchedBaseQuantity<>0 OR d.ReceivedBaseQuantity<>0
 OR d.SourceBeforeQty IS NOT NULL OR d.SourceAfterQty IS NOT NULL
 OR d.DestinationBeforeQty IS NOT NULL OR d.DestinationAfterQty IS NOT NULL
 OR d.UnitPrice<>x.UnitPrice OR d.Note<>x.Note)
  THROW 53204,N'InventoryTransferDetail có ID hoặc transfer/ingredient key xung đột.',1;

 SET IDENTITY_INSERT dbo.InventoryTransferDetails ON;
 INSERT dbo.InventoryTransferDetails(InventoryTransferDetailId,InventoryTransferId,IngredientId,
 PreparedItemId,RestockRequestId,RestockRequestFulfillmentId,UnitId,Quantity,BaseQuantity,
 DispatchedBaseQuantity,ReceivedBaseQuantity,SourceBeforeQty,SourceAfterQty,
 DestinationBeforeQty,DestinationAfterQty,UnitPrice,Note)
 SELECT * FROM @TransferDetailSeed x WHERE NOT EXISTS(SELECT 1 FROM dbo.InventoryTransferDetails d
 WHERE d.InventoryTransferDetailId=x.InventoryTransferDetailId);
 SET IDENTITY_INSERT dbo.InventoryTransferDetails OFF;

 IF (SELECT COUNT(*) FROM dbo.InventoryTransfers)<>1
 OR(SELECT COUNT(*) FROM dbo.InventoryTransferDetails)<>3
  THROW 53205,N'Row count cuối Batch 11 không đúng contract database sạch.',1;

 IF EXISTS(SELECT 1 FROM dbo.InventoryTransfers t WHERE t.InventoryTransferId=1
 AND(t.[Status]<>1 OR t.FromStoreId=t.ToStoreId OR t.ConfirmedAt IS NOT NULL
 OR t.DispatchedAt IS NOT NULL OR t.CancelledAt IS NOT NULL))
 OR EXISTS(SELECT InventoryTransferId,IngredientId FROM dbo.InventoryTransferDetails
 WHERE IngredientId IS NOT NULL GROUP BY InventoryTransferId,IngredientId HAVING COUNT(*)>1)
 OR EXISTS(SELECT 1 FROM dbo.InventoryTransferDetails d WHERE d.InventoryTransferId=1
 AND(d.DispatchedBaseQuantity<>0 OR d.ReceivedBaseQuantity<>0
 OR NOT((d.IngredientId IS NOT NULL AND d.PreparedItemId IS NULL)
     OR(d.IngredientId IS NULL AND d.PreparedItemId IS NOT NULL))))
 OR EXISTS(SELECT 1 FROM dbo.InventoryTransactions t
 WHERE t.InventoryTransferId=1 OR t.InventoryTransferDetailId IN(1,2,3))
  THROW 53206,N'Transfer draft có lifecycle, duplicate identity hoặc movement ngoài contract.',1;

 COMMIT;
END TRY
BEGIN CATCH
 BEGIN TRY SET IDENTITY_INSERT dbo.InventoryTransfers OFF; END TRY BEGIN CATCH END CATCH;
 BEGIN TRY SET IDENTITY_INSERT dbo.InventoryTransferDetails OFF; END TRY BEGIN CATCH END CATCH;
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
SeedAllBatch11Complete:
GO

IF NOT EXISTS
(
 SELECT 1 FROM dbo.SystemSettings
 WHERE SettingKey=N'seedall_foundation_inventory_v1'
)
BEGIN
 INSERT dbo.SystemSettings(SettingKey,SettingValue,Description)
 VALUES(N'seedall_foundation_inventory_v1',N'completed',
        N'Dấu phiên bản giúp SeedAll không tạo lại dữ liệu nền menu, kho và FIFO.');
END
ELSE
BEGIN
 UPDATE dbo.SystemSettings
 SET SettingValue=N'completed',
     Description=N'Dấu phiên bản giúp SeedAll không tạo lại dữ liệu nền menu, kho và FIFO.'
 WHERE SettingKey=N'seedall_foundation_inventory_v1';
END;
GO

/* BATCH 11 READ-ONLY VERIFICATION */
SELECT N'InventoryTransfers' Entity,COUNT(*) TotalRows,MIN(InventoryTransferId) MinId,
MAX(InventoryTransferId) MaxId FROM dbo.InventoryTransfers
UNION ALL SELECT N'InventoryTransferDetails',COUNT(*),MIN(InventoryTransferDetailId),
MAX(InventoryTransferDetailId) FROM dbo.InventoryTransferDetails;

SELECT N'Orphan Transfer Detail' Issue,COUNT(*) IssueCount FROM dbo.InventoryTransferDetails d
LEFT JOIN dbo.InventoryTransfers t ON t.InventoryTransferId=d.InventoryTransferId
LEFT JOIN dbo.Ingredients i ON i.IngredientId=d.IngredientId
LEFT JOIN dbo.Units u ON u.UnitId=d.UnitId
WHERE t.InventoryTransferId IS NULL OR i.IngredientId IS NULL OR u.UnitId IS NULL
UNION ALL SELECT N'Duplicate Transfer Ingredient',COUNT(*) FROM(SELECT InventoryTransferId,IngredientId
FROM dbo.InventoryTransferDetails WHERE IngredientId IS NOT NULL
GROUP BY InventoryTransferId,IngredientId HAVING COUNT(*)>1)x
UNION ALL SELECT N'Unexpected Draft Movement',COUNT(*) FROM dbo.InventoryTransactions
WHERE InventoryTransferId=1 OR InventoryTransferDetailId IN(1,2,3)
UNION ALL SELECT N'Invalid Draft Lifecycle',COUNT(*) FROM dbo.InventoryTransfers
WHERE InventoryTransferId=1 AND([Status]<>1 OR ConfirmedAt IS NOT NULL
OR DispatchedAt IS NOT NULL OR CancelledAt IS NOT NULL);

/* ============================================================
   BATCH 12B - ACTIVE ADMIN PERMISSION CATALOG
   Expected clean totals after Batch 12 + 12B:
   - 25 PermissionGroups
   - 145 Permissions
   - 418 RolePermissions

   PermissionId 100 is intentionally reserved for migration rollback.
   PermissionGroupId 22 is reserved for OPERATIONAL_ICE.
   PermissionIds 200-203 are reserved for Operational Ice permissions.

   This batch is insert-only and idempotent;
   contract conflicts abort the transaction.
   ============================================================ */

IF OBJECT_ID(N'dbo.PermissionGroups', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Permissions', N'U') IS NULL
   OR OBJECT_ID(N'dbo.RolePermissions', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Roles', N'U') IS NULL
    THROW 53300, N'Schema thiếu bảng phân quyền bắt buộc cho Batch 12.', 1;
GO

BEGIN TRY
 BEGIN TRANSACTION;

 /* Foundation rows are owned by EF migration and must not be rewritten. */
 IF NOT EXISTS(SELECT 1 FROM dbo.PermissionGroups WHERE PermissionGroupId=1 AND Code=N'DRINK' AND Name=N'Quản lý đồ uống' AND DisplayOrder=1 AND Active=1)
 OR NOT EXISTS(SELECT 1 FROM dbo.PermissionGroups WHERE PermissionGroupId=2 AND Code=N'TOPPING' AND Name=N'Quản lý Topping' AND DisplayOrder=2 AND Active=1)
 OR NOT EXISTS(SELECT 1 FROM dbo.PermissionGroups WHERE PermissionGroupId=3 AND Code=N'ORDER' AND Name=N'Quản lý đơn hàng' AND DisplayOrder=3 AND Active=1)
 OR NOT EXISTS(SELECT 1 FROM dbo.PermissionGroups WHERE PermissionGroupId=4 AND Code=N'CUSTOMER' AND Name=N'Quản lý khách hàng' AND DisplayOrder=4 AND Active=1)
 OR NOT EXISTS(SELECT 1 FROM dbo.PermissionGroups WHERE PermissionGroupId=5 AND Code=N'SYSTEM' AND Name=N'Hệ thống' AND DisplayOrder=999 AND Active=1)
  THROW 53301,N'PermissionGroup nền của EF không đúng contract.',1;

 IF NOT EXISTS(SELECT 1 FROM dbo.Permissions WHERE PermissionId=1 AND PermissionGroupId=1 AND Code=N'Drink.View' AND Name=N'Xem đồ uống' AND Action=N'View' AND Description=N'Xem danh sách đồ uống' AND Active=1 AND CreatedAt='2025-01-01')
 OR NOT EXISTS(SELECT 1 FROM dbo.Permissions WHERE PermissionId=2 AND PermissionGroupId=1 AND Code=N'Drink.Create' AND Name=N'Thêm đồ uống' AND Action=N'Create' AND Description=N'Tạo mới đồ uống' AND Active=1 AND CreatedAt='2025-01-01')
 OR NOT EXISTS(SELECT 1 FROM dbo.Permissions WHERE PermissionId=3 AND PermissionGroupId=1 AND Code=N'Drink.Update' AND Name=N'Cập nhật đồ uống' AND Action=N'Update' AND Description=N'Cập nhật thông tin đồ uống' AND Active=1 AND CreatedAt='2025-01-01')
 OR NOT EXISTS(SELECT 1 FROM dbo.Permissions WHERE PermissionId=4 AND PermissionGroupId=1 AND Code=N'Drink.Delete' AND Name=N'Xóa đồ uống' AND Action=N'Delete' AND Description=N'Xóa hoặc vô hiệu đồ uống' AND CreatedAt='2025-01-01')
 OR NOT EXISTS(SELECT 1 FROM dbo.Permissions WHERE PermissionId=27 AND PermissionGroupId=5 AND Code=N'System.Permission.Manage' AND Name=N'Quản lý phân quyền' AND Action=N'Manage' AND Description=N'Xem danh sách bảng phân quyền' AND Active=1 AND CreatedAt='2025-01-01')
  THROW 53302,N'Permission nền của EF không đúng contract.',1;

 IF EXISTS(SELECT RequiredRoleId FROM(VALUES(1),(2),(3),(4),(5),(6),(8))r(RequiredRoleId)
 WHERE NOT EXISTS(SELECT 1 FROM dbo.Roles ro WHERE ro.RoleId=r.RequiredRoleId AND ro.Active=1))
  THROW 53303,N'Thiếu Role active được Part7 tham chiếu.',1;

 DECLARE @PermissionGroupSeed TABLE(PermissionGroupId int PRIMARY KEY,Code nvarchar(50) UNIQUE,
 Name nvarchar(150) UNIQUE,DisplayOrder int,Active bit);
 INSERT @PermissionGroupSeed VALUES
 (6,N'CATEGORY',N'Quản lý danh mục',5,1),
 (7,N'SIZE',N'Quản lý Size',6,1),
 (8,N'APPLICATION',N'Truy cập ứng dụng',0,1);

 IF EXISTS(SELECT 1 FROM @PermissionGroupSeed x JOIN dbo.PermissionGroups g
 ON g.PermissionGroupId=x.PermissionGroupId OR g.Code=x.Code OR g.Name=x.Name
 WHERE g.PermissionGroupId<>x.PermissionGroupId OR g.Code<>x.Code OR g.Name<>x.Name
 OR g.DisplayOrder<>x.DisplayOrder OR g.Active<>x.Active)
  THROW 53304,N'PermissionGroup Part7 xung đột ID, Code, Name hoặc contract.',1;

 SET IDENTITY_INSERT dbo.PermissionGroups ON;
 INSERT dbo.PermissionGroups(PermissionGroupId,Code,Name,DisplayOrder,Active)
 SELECT * FROM @PermissionGroupSeed x WHERE NOT EXISTS(SELECT 1 FROM dbo.PermissionGroups g
 WHERE g.PermissionGroupId=x.PermissionGroupId);
 SET IDENTITY_INSERT dbo.PermissionGroups OFF;

 DECLARE @PermissionSeed TABLE(PermissionId int PRIMARY KEY,PermissionGroupId int,
 Code nvarchar(100) UNIQUE,Name nvarchar(200),Action nvarchar(50),Description nvarchar(500),
 Active bit,CreatedAt datetime2);
 INSERT @PermissionSeed VALUES
 (5,6,N'Category.View',N'Xem danh mục',N'View',N'Xem danh sách và chi tiết danh mục đồ uống',1,'2026-01-01'),
 (6,6,N'Category.Create',N'Thêm danh mục',N'Create',N'Tạo mới danh mục đồ uống',1,'2026-01-01'),
 (7,6,N'Category.Update',N'Cập nhật danh mục',N'Update',N'Cập nhật thông tin danh mục đồ uống',1,'2026-01-01'),
 (8,6,N'Category.Delete',N'Xóa danh mục',N'Delete',N'Xóa hoặc vô hiệu hóa danh mục đồ uống',0,'2026-01-01'),
 (9,6,N'Category.ToggleStatus',N'Ẩn / hiện danh mục',N'ToggleStatus',N'Cho phép bật hoặc tắt trạng thái hiển thị của danh mục',1,'2026-01-01'),
 (10,7,N'Size.View',N'Xem Size',N'View',N'Xem danh sách và chi tiết Size',1,'2026-01-01'),
 (11,7,N'Size.Create',N'Thêm Size',N'Create',N'Tạo mới Size',1,'2026-01-01'),
 (12,7,N'Size.Update',N'Cập nhật Size',N'Update',N'Cập nhật thông tin Size',1,'2026-01-01'),
 (13,7,N'Size.Delete',N'Xóa Size',N'Delete',N'Xóa hoặc vô hiệu hóa Size',0,'2026-01-01'),
 (14,7,N'Size.ToggleStatus',N'Khóa / mở khóa Size',N'ToggleStatus',N'Cho phép bật hoặc tắt trạng thái hoạt động của Size',1,'2026-01-01'),
 (15,7,N'Size.AssignDrink',N'Liên kết Size với đồ uống',N'AssignDrink',N'Cho phép liên kết Size với đồ uống',1,'2026-01-01'),
 (16,2,N'Topping.View',N'Xem Topping',N'View',N'Xem danh sách và chi tiết Topping',1,'2026-01-01'),
 (17,2,N'Topping.Create',N'Thêm Topping',N'Create',N'Tạo mới Topping',1,'2026-01-01'),
 (18,2,N'Topping.Update',N'Cập nhật Topping',N'Update',N'Cập nhật thông tin Topping',1,'2026-01-01'),
 (19,2,N'Topping.Delete',N'Xóa Topping',N'Delete',N'Xóa hoặc vô hiệu hóa Topping',0,'2026-01-01'),
 (20,2,N'Topping.ToggleStatus',N'Khóa / mở khóa Topping',N'ToggleStatus',N'Cho phép bật hoặc tắt trạng thái hoạt động của Topping',1,'2026-01-01'),
 (21,2,N'Topping.AssignDrink',N'Liên kết Topping với đồ uống',N'AssignDrink',N'Cho phép liên kết Topping với đồ uống',1,'2026-01-01'),
 (22,1,N'Drink.ToggleStatus',N'Đổi trạng thái đồ uống',N'ToggleStatus',N'Cho phép chuyển trạng thái đang bán / ngừng bán của đồ uống',1,'2026-01-01'),
 (23,1,N'Drink.UpdateImage',N'Cập nhật hình ảnh đồ uống',N'UpdateImage',N'Cho phép cập nhật hình ảnh đồ uống',1,'2026-01-01'),
 (24,8,N'App.AdminDashboard',N'Truy cập Dashboard',N'AdminDashboard',N'Mở Dashboard Analytics trong Admin Panel.',1,'2026-01-01'),
 (25,8,N'App.StaffHub',N'Truy cập StaffHub',N'StaffHub',N'Mở lịch cá nhân và tác vụ nhân viên.',1,'2026-01-01'),
 (26,8,N'App.POS',N'Truy cập POS',N'POS',N'Mở màn hình bán hàng.',1,'2026-01-01');

 IF EXISTS(SELECT 1 FROM @PermissionSeed x JOIN dbo.Permissions p
 ON p.PermissionId=x.PermissionId OR p.Code=x.Code
 OR(p.PermissionGroupId=x.PermissionGroupId AND p.Action=x.Action)
 WHERE p.PermissionId<>x.PermissionId OR p.PermissionGroupId<>x.PermissionGroupId
 OR p.Code<>x.Code OR p.Name<>x.Name OR p.Action<>x.Action
 OR ISNULL(p.Description,N'')<>ISNULL(x.Description,N'')
 OR (p.Active<>x.Active AND x.Code NOT IN
    (N'Drink.Delete',N'Category.Delete',N'Size.Delete',N'Topping.Delete'))
 OR p.CreatedAt<>x.CreatedAt)
  THROW 53305,N'Permission Part7 xung đột ID, Code, Group/Action hoặc contract.',1;

 SET IDENTITY_INSERT dbo.Permissions ON;
 INSERT dbo.Permissions(PermissionId,PermissionGroupId,Code,Name,Action,Description,Active,CreatedAt)
 SELECT * FROM @PermissionSeed x WHERE NOT EXISTS(SELECT 1 FROM dbo.Permissions p
 WHERE p.PermissionId=x.PermissionId);
 SET IDENTITY_INSERT dbo.Permissions OFF;

 DECLARE @RolePermissionSeed TABLE(RoleId int,PermissionId int,
 PRIMARY KEY(RoleId,PermissionId));
 INSERT @RolePermissionSeed VALUES
 (1,5),(1,6),(1,7),(1,8),(1,9),
 (1,10),(1,11),(1,12),(1,13),(1,14),(1,15),
 (1,16),(1,17),(1,18),(1,19),(1,20),(1,21),
 (1,22),(1,23),(1,24),
 (2,24),(3,24),(5,24),(6,24),
 (1,25),(2,25),(3,25),(4,25),(5,25),(6,25),(8,25),
 (3,26),(4,26),(8,26);

 IF (SELECT COUNT(*) FROM @RolePermissionSeed)<>34
 OR EXISTS(SELECT 1 FROM @RolePermissionSeed x
 LEFT JOIN dbo.Roles r ON r.RoleId=x.RoleId
 LEFT JOIN dbo.Permissions p ON p.PermissionId=x.PermissionId
 WHERE r.RoleId IS NULL OR p.PermissionId IS NULL)
  THROW 53306,N'RolePermission Part7 không đủ 34 cặp hoặc có FK không hợp lệ.',1;

 INSERT dbo.RolePermissions(RoleId,PermissionId)
 SELECT x.RoleId,x.PermissionId
 FROM @RolePermissionSeed x
 JOIN dbo.Permissions p
   ON p.PermissionId=x.PermissionId
  AND p.Active=1
 WHERE NOT EXISTS
 (
     SELECT 1
     FROM dbo.RolePermissions rp
     WHERE rp.RoleId=x.RoleId
       AND rp.PermissionId=x.PermissionId
 );

 IF EXISTS(SELECT Code FROM dbo.PermissionGroups GROUP BY Code HAVING COUNT(*)>1)
 OR EXISTS(SELECT Name FROM dbo.PermissionGroups GROUP BY Name HAVING COUNT(*)>1)
 OR EXISTS(SELECT Code FROM dbo.Permissions GROUP BY Code HAVING COUNT(*)>1)
 OR EXISTS(SELECT PermissionGroupId,Action FROM dbo.Permissions
 GROUP BY PermissionGroupId,Action HAVING COUNT(*)>1)
 OR EXISTS(SELECT 1 FROM dbo.Permissions p LEFT JOIN dbo.PermissionGroups g
 ON g.PermissionGroupId=p.PermissionGroupId WHERE g.PermissionGroupId IS NULL)
 OR EXISTS(SELECT 1 FROM dbo.RolePermissions rp LEFT JOIN dbo.Roles r ON r.RoleId=rp.RoleId
 LEFT JOIN dbo.Permissions p ON p.PermissionId=rp.PermissionId
 WHERE r.RoleId IS NULL OR p.PermissionId IS NULL)
 OR EXISTS
 (
    SELECT 1
    FROM @RolePermissionSeed x
    JOIN dbo.Permissions p
      ON p.PermissionId=x.PermissionId
     AND p.Active=1
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.RolePermissions rp
        WHERE rp.RoleId=x.RoleId
          AND rp.PermissionId=x.PermissionId
    )
 )
  THROW 53308,N'Duplicate, orphan hoặc thiếu RolePermission sau Batch 12.',1;

 COMMIT;
END TRY
BEGIN CATCH
 BEGIN TRY SET IDENTITY_INSERT dbo.PermissionGroups OFF; END TRY BEGIN CATCH END CATCH;
 BEGIN TRY SET IDENTITY_INSERT dbo.Permissions OFF; END TRY BEGIN CATCH END CATCH;
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
GO

/* ============================================================
   BATCH 12B - ACTIVE ADMIN PERMISSION CATALOG
   PermissionId 100 is intentionally reserved for migration rollback.
   This batch is insert-only and idempotent; contract conflicts abort.
   ============================================================ */
BEGIN TRY
 BEGIN TRANSACTION;

 DECLARE @AdminPermissionGroupSeed TABLE(PermissionGroupId int PRIMARY KEY,Code nvarchar(50) UNIQUE,
 Name nvarchar(150) UNIQUE,DisplayOrder int,Active bit);
 INSERT @AdminPermissionGroupSeed VALUES
 (9,N'INGREDIENT',N'Nguyên liệu',10,1),
 (10,N'UNIT_CONVERSION',N'Đơn vị và quy đổi',11,1),
 (11,N'INVENTORY',N'Tồn kho',12,1),
 (12,N'STOCK_ALERT',N'Cảnh báo kho',13,1),
 (13,N'RESTOCK',N'Yêu cầu nhập hàng',14,1),
 (14,N'PURCHASE_ADVICE',N'Đề nghị mua hàng',15,1),
 (15,N'PURCHASE_ORDER',N'Đơn đặt hàng',16,1),
 (16,N'RECEIPT',N'Nhận hàng',17,1),
 (17,N'SUPPLIER',N'Nhà cung cấp',18,1),
 (18,N'INVENTORY_DOCUMENT',N'Phiếu kho',19,1),
 (19,N'INVENTORY_TRANSFER',N'Chuyển kho',20,1),
 (20,N'STAFF',N'Nhân viên',21,1),
 (21,N'SHIFT',N'Lịch làm việc',22,1),
 (22,N'OPERATIONAL_ICE',N'Quản lý đá vận hành',23,1),
 (23,N'STORE',N'Cửa hàng',24,1),
 (24,N'SETTINGS',N'Cài đặt hệ thống',25,1),
 (25,N'BOM',N'BOM và sản xuất',26,1),
 (26,N'REORDER_SUGGESTION',N'Gợi ý nhập hàng',27,1);

 IF EXISTS(SELECT 1 FROM @AdminPermissionGroupSeed x JOIN dbo.PermissionGroups g
 ON g.PermissionGroupId=x.PermissionGroupId OR g.Code=x.Code OR g.Name=x.Name
 WHERE g.PermissionGroupId<>x.PermissionGroupId OR g.Code<>x.Code OR g.Name<>x.Name
 OR g.DisplayOrder<>x.DisplayOrder OR g.Active<>x.Active)
  THROW 53320,N'PermissionGroup active-admin xung đột contract.',1;

 SET IDENTITY_INSERT dbo.PermissionGroups ON;
 INSERT dbo.PermissionGroups(PermissionGroupId,Code,Name,DisplayOrder,Active)
 SELECT x.PermissionGroupId,x.Code,x.Name,x.DisplayOrder,x.Active FROM @AdminPermissionGroupSeed x
 WHERE NOT EXISTS(SELECT 1 FROM dbo.PermissionGroups g WHERE g.PermissionGroupId=x.PermissionGroupId);
 SET IDENTITY_INSERT dbo.PermissionGroups OFF;

 DECLARE @AdminPermissionSeed TABLE(PermissionId int PRIMARY KEY,PermissionGroupId int,
 Code nvarchar(100) UNIQUE,Name nvarchar(200),Action nvarchar(50),Description nvarchar(500),
 Active bit,CreatedAt datetime2);
 INSERT @AdminPermissionSeed VALUES
 (28,9,N'Ingredient.View',N'Xem nguyên liệu',N'View',N'Xem nguyên liệu',1,'2026-01-01'),
 (29,9,N'Ingredient.Create',N'Tạo nguyên liệu',N'Create',N'Tạo nguyên liệu',1,'2026-01-01'),
 (30,9,N'Ingredient.Update',N'Cập nhật nguyên liệu',N'Update',N'Cập nhật nguyên liệu',1,'2026-01-01'),
 (31,9,N'Ingredient.ToggleStatus',N'Đổi trạng thái nguyên liệu',N'ToggleStatus',N'Đổi trạng thái nguyên liệu',1,'2026-01-01'),

 (32,10,N'UnitConversion.View',N'Xem quy đổi',N'View',N'Xem quy đổi',1,'2026-01-01'),
 (33,10,N'UnitConversion.Create',N'Tạo quy đổi',N'Create',N'Tạo quy đổi',1,'2026-01-01'),
 (34,10,N'UnitConversion.Update',N'Cập nhật quy đổi',N'Update',N'Cập nhật quy đổi',1,'2026-01-01'),
 (35,10,N'UnitConversion.ToggleStatus',N'Đổi trạng thái quy đổi',N'ToggleStatus',N'Đổi trạng thái quy đổi',1,'2026-01-01'),

 (36,11,N'Inventory.View',N'Xem tồn kho',N'View',N'Xem tồn kho',1,'2026-01-01'),
 (37,11,N'Inventory.Adjust',N'Điều chỉnh tồn kho',N'Adjust',N'Điều chỉnh tồn kho',0,'2026-01-01'),
 (38,11,N'Inventory.Export',N'Xuất dữ liệu tồn kho',N'Export',N'Xuất dữ liệu tồn kho',0,'2026-01-01'),

 (39,12,N'StockAlert.View',N'Xem cảnh báo kho',N'View',N'Xem cảnh báo kho',1,'2026-01-01'),
 (40,12,N'StockAlert.Resolve',N'Xử lý cảnh báo kho',N'Resolve',N'Xử lý cảnh báo kho',1,'2026-01-01'),
 (41,12,N'StockAlert.Configure',N'Cấu hình cảnh báo kho',N'Configure',N'Cấu hình cảnh báo kho',0,'2026-01-01'),
 (42,12,N'StockAlert.Export',N'Xuất cảnh báo kho',N'Export',N'Xuất cảnh báo kho',0,'2026-01-01'),
 (131,12,N'StockAlert.Create',N'Báo thiếu nguyên liệu',N'Create',N'Tạo cảnh báo thiếu nguyên liệu từ nghiệp vụ cửa hàng',0,'2026-01-01'),
 (132,12,N'StockAlert.CreateRestockRequest',N'Tạo yêu cầu nhập từ cảnh báo',N'CreateRestockRequest',N'Tạo yêu cầu nhập hàng từ cảnh báo kho đã được xác nhận',1,'2026-01-01'),

 (43,13,N'Restock.View',N'Xem yêu cầu nhập',N'View',N'Xem yêu cầu nhập',1,'2026-01-01'),
 (44,13,N'Restock.Create',N'Tạo yêu cầu nhập hàng',N'Create',N'Tạo mới, tạo nháp hoặc bổ sung yêu cầu nhập hàng từ gợi ý nhập hàng trong phạm vi cửa hàng được phép thao tác',1,'2026-01-01'),
 (45,13,N'Restock.Submit',N'Gửi yêu cầu nhập',N'Submit',N'Gửi yêu cầu nhập',1,'2026-01-01'),
 (46,13,N'Restock.Approve',N'Duyệt yêu cầu nhập',N'Approve',N'Duyệt yêu cầu nhập',1,'2026-01-01'),
 (47,13,N'Restock.Reject',N'Từ chối yêu cầu nhập',N'Reject',N'Từ chối yêu cầu nhập',1,'2026-01-01'),
 (48,13,N'Restock.Cancel',N'Hủy yêu cầu nhập',N'Cancel',N'Hủy yêu cầu nhập',1,'2026-01-01'),
 (133,13,N'Restock.Update',N'Cập nhật yêu cầu nhập',N'Update',N'Cập nhật yêu cầu nhập trước khi gửi hoặc khi trạng thái cho phép',1,'2026-01-01'),
 (134,13,N'Restock.CloseRemaining',N'Đóng phần còn lại yêu cầu nhập',N'CloseRemaining',N'Đóng phần nhu cầu nhập còn lại không tiếp tục xử lý',1,'2026-01-01'),
 (135,13,N'Restock.CreatePurchaseOrder',N'Tạo đơn đặt hàng từ yêu cầu nhập',N'CreatePurchaseOrder',N'Tạo đơn đặt hàng mua ngoài từ phần nhu cầu nhập được phân bổ',0,'2026-01-01'),
 (136,13,N'Restock.CreateTransfer',N'Tạo điều chuyển từ yêu cầu nhập',N'CreateTransfer',N'Tạo phiếu điều chuyển từ phần nhu cầu nhập được phân bổ',0,'2026-01-01'),


 (49,14,N'PurchaseAdvice.View',N'Xem đề nghị mua',N'View',N'Xem đề nghị mua',1,'2026-01-01'),
 (50,14,N'PurchaseAdvice.Create',N'Tạo đề nghị mua',N'Create',N'Tạo đề nghị mua',1,'2026-01-01'),
 (51,14,N'PurchaseAdvice.Submit',N'Gửi đề nghị mua',N'Submit',N'Gửi đề nghị mua',1,'2026-01-01'),
 (52,14,N'PurchaseAdvice.Review',N'Bắt đầu duyệt đề nghị mua',N'Review',N'Bắt đầu duyệt đề nghị mua',1,'2026-01-01'),
 (53,14,N'PurchaseAdvice.Approve',N'Duyệt đề nghị mua',N'Approve',N'Duyệt đề nghị mua',0,'2026-01-01'),
 (54,14,N'PurchaseAdvice.Reject',N'Từ chối đề nghị mua',N'Reject',N'Từ chối đề nghị mua',1,'2026-01-01'),
 (55,14,N'PurchaseAdvice.Consolidate',N'Tổng hợp đề nghị mua',N'Consolidate',N'Tổng hợp đề nghị mua',1,'2026-01-01'),
 (137,14,N'PurchaseAdvice.SelectSupplier',N'Chọn nhà cung cấp',N'SelectSupplier',N'Chọn nhà cung cấp và quy cách mua cho đề nghị mua hàng',1,'2026-01-01'),
 (138,14,N'PurchaseAdvice.CreatePurchaseOrder',N'Tạo đơn đặt hàng từ đề nghị mua',N'CreatePurchaseOrder',N'Tạo đơn đặt hàng từ đề nghị mua đã được tổng hợp',0,'2026-01-01'),

 (56,15,N'PurchaseOrder.View',N'Xem đơn đặt hàng',N'View',N'Xem đơn đặt hàng',1,'2026-01-01'),
 (57,15,N'PurchaseOrder.Create',N'Tạo đơn đặt hàng',N'Create',N'Tạo đơn đặt hàng',1,'2026-01-01'),
 (58,15,N'PurchaseOrder.Update',N'Cập nhật đơn đặt hàng',N'Update',N'Cập nhật đơn đặt hàng',0,'2026-01-01'),
 (59,15,N'PurchaseOrder.Send',N'Gửi nhà cung cấp',N'Send',N'Gửi nhà cung cấp',1,'2026-01-01'),
 (60,15,N'PurchaseOrder.Receive',N'Nhận hàng từ PO',N'Receive',N'Nhận hàng từ PO',0,'2026-01-01'),
 (61,15,N'PurchaseOrder.Cancel',N'Hủy đơn đặt hàng',N'Cancel',N'Hủy đơn đặt hàng',1,'2026-01-01'),
 (62,15,N'PurchaseOrder.ViewBatch',N'Xem batch PO',N'ViewBatch',N'Xem batch PO',1,'2026-01-01'),
 (63,15,N'PurchaseOrder.CreateBatch',N'Tạo batch PO',N'CreateBatch',N'Tạo batch PO',1,'2026-01-01'),
 (64,15,N'PurchaseOrder.Consolidate',N'Tổng hợp PO',N'Consolidate',N'Tổng hợp PO',0,'2026-01-01'),
 (139,15,N'PurchaseOrder.Submit',N'Gửi đơn đặt hàng để duyệt',N'Submit',N'Chuyển đơn đặt hàng từ bản nháp sang trạng thái chờ duyệt',0,'2026-01-01'),
 (140,15,N'PurchaseOrder.Approve',N'Duyệt đơn đặt hàng',N'Approve',N'Duyệt cam kết đặt hàng với nhà cung cấp',1,'2026-01-01'),
 (141,15,N'PurchaseOrder.RejectApproval',N'Từ chối duyệt đơn đặt hàng',N'RejectApproval',N'Từ chối đơn đặt hàng đang chờ duyệt',0,'2026-01-01'),
 (142,15,N'PurchaseOrder.OverrideAllocation',N'Duyệt vượt phân bổ',N'OverrideAllocation',N'Cho phép đơn đặt hàng vượt số lượng đã được phân bổ khi có lý do',0,'2026-01-01'),
 (143,15,N'PurchaseOrder.Export',N'Xuất đơn đặt hàng',N'Export',N'Xuất tài liệu đơn đặt hàng để gửi hoặc lưu chứng từ',1,'2026-01-01'),

 (65,16,N'Receipt.View',N'Xem phiếu nhận hàng',N'View',N'Xem phiếu nhận hàng',1,'2026-01-01'),
 (66,16,N'Receipt.Create',N'Tạo phiếu nhận hàng',N'Create',N'Tạo phiếu nhận hàng',1,'2026-01-01'),
 (67,16,N'Receipt.Confirm',N'Xác nhận nhận hàng',N'Confirm',N'Xác nhận nhận hàng',1,'2026-01-01'),
 (68,16,N'Receipt.Reject',N'Ghi nhận hàng bị từ chối',N'Reject',N'Ghi nhận hàng bị từ chối',0,'2026-01-01'),
 (69,16,N'Receipt.Cancel',N'Hủy phiếu nhận hàng',N'Cancel',N'Hủy phiếu nhận hàng',0,'2026-01-01'),
 (144,16,N'Receipt.UpdateDraft',N'Cập nhật phiếu nhận bản nháp',N'UpdateDraft',N'Cập nhật phiếu nhận trước khi xác nhận nhập kho',1,'2026-01-01'),
 (145,16,N'Receipt.RecordSupplierIssue',N'Ghi nhận sự cố nhà cung cấp',N'RecordSupplierIssue',N'Ghi nhận sự cố hoặc lý do liên quan đến hàng giao từ nhà cung cấp',0,'2026-01-01'),
 (146,16,N'Receipt.ViewCost',N'Xem giá vốn phiếu nhận',N'ViewCost',N'Xem giá vốn và giá trị của phiếu nhận hàng',1,'2026-01-01'),

 (70,17,N'Supplier.View',N'Xem nhà cung cấp',N'View',N'Xem nhà cung cấp',1,'2026-01-01'),
 (71,17,N'Supplier.Create',N'Tạo nhà cung cấp',N'Create',N'Tạo nhà cung cấp',1,'2026-01-01'),
 (72,17,N'Supplier.Update',N'Cập nhật nhà cung cấp',N'Update',N'Cập nhật nhà cung cấp',1,'2026-01-01'),
 (73,17,N'Supplier.ToggleStatus',N'Đổi trạng thái nhà cung cấp',N'ToggleStatus',N'Đổi trạng thái nhà cung cấp',1,'2026-01-01'),
 (74,17,N'Supplier.ViewQuality',N'Xem chất lượng nhà cung cấp',N'ViewQuality',N'Xem chất lượng nhà cung cấp',0,'2026-01-01'),

 (75,18,N'InventoryDocument.View',N'Xem phiếu kho',N'View',N'Xem phiếu kho',1,'2026-01-01'),
 (76,18,N'InventoryDocument.CreateDraft',N'Tạo nháp phiếu kho',N'CreateDraft',N'Tạo nháp phiếu kho',1,'2026-01-01'),
 (77,18,N'InventoryDocument.Submit',N'Gửi phiếu kho',N'Submit',N'Gửi phiếu kho',1,'2026-01-01'),
 (78,18,N'InventoryDocument.Confirm',N'Xác nhận phiếu kho',N'Confirm',N'Xác nhận phiếu kho',1,'2026-01-01'),
 (79,18,N'InventoryDocument.ApproveNegative',N'Duyệt xuất âm',N'ApproveNegative',N'Duyệt xuất âm',1,'2026-01-01'),
 (80,18,N'InventoryDocument.Cancel',N'Hủy phiếu kho',N'Cancel',N'Hủy phiếu kho',1,'2026-01-01'),
 (81,18,N'InventoryDocument.Export',N'Xuất phiếu kho',N'Export',N'Xuất phiếu kho',1,'2026-01-01'),

 (82,19,N'InventoryTransfer.View',N'Xem chuyển kho',N'View',N'Xem chuyển kho',1,'2026-01-01'),
 (83,19,N'InventoryTransfer.CreateDraft',N'Tạo nháp chuyển kho',N'CreateDraft',N'Tạo nháp chuyển kho',1,'2026-01-01'),
 (84,19,N'InventoryTransfer.UpdateDraft',N'Sửa nháp chuyển kho',N'UpdateDraft',N'Sửa nháp chuyển kho',1,'2026-01-01'),
 (85,19,N'InventoryTransfer.Dispatch',N'Xuất kho nguồn',N'Dispatch',N'Xuất kho nguồn',1,'2026-01-01'),
 (86,19,N'InventoryTransfer.Receive',N'Nhận kho đích',N'Receive',N'Nhận kho đích',1,'2026-01-01'),
 (87,19,N'InventoryTransfer.Cancel',N'Hủy chuyển kho',N'Cancel',N'Hủy chuyển kho',1,'2026-01-01'),
 (88,19,N'InventoryTransfer.Export',N'Xuất dữ liệu chuyển kho',N'Export',N'Xuất dữ liệu chuyển kho',0,'2026-01-01'),

 (89,3,N'Order.View',N'Xem đơn hàng',N'View',N'Xem đơn hàng',1,'2026-01-01'),
 (90,3,N'Order.UpdateStatus',N'Cập nhật trạng thái đơn',N'UpdateStatus',N'Cập nhật trạng thái đơn',1,'2026-01-01'),
 (91,3,N'Order.Cancel',N'Hủy đơn hàng',N'Cancel',N'Hủy đơn hàng',1,'2026-01-01'),
 (92,3,N'Order.Refund',N'Hoàn tiền đơn hàng',N'Refund',N'Hoàn tiền đơn hàng',0,'2026-01-01'),
 (93,3,N'Order.Export',N'Xuất đơn hàng',N'Export',N'Xuất đơn hàng',1,'2026-01-01'),

 (94,20,N'Staff.View',N'Xem nhân viên',N'View',N'Xem nhân viên',1,'2026-01-01'),
 (95,20,N'Staff.Create',N'Tạo nhân viên',N'Create',N'Tạo nhân viên',1,'2026-01-01'),
 (96,20,N'Staff.Update',N'Cập nhật nhân viên',N'Update',N'Cập nhật nhân viên',1,'2026-01-01'),
 (97,20,N'Staff.ToggleStatus',N'Đổi trạng thái nhân viên',N'ToggleStatus',N'Đổi trạng thái nhân viên',1,'2026-01-01'),
 (98,20,N'Staff.ResetPassword',N'Đặt lại mật khẩu',N'ResetPassword',N'Đặt lại mật khẩu',1,'2026-01-01'),

 (99,21,N'Shift.View',N'Xem lịch làm việc',N'View',N'Xem lịch làm việc',1,'2026-01-01'),
 (101,21,N'Shift.Create',N'Tạo lịch làm việc',N'Create',N'Tạo lịch làm việc',1,'2026-01-01'),
 (102,21,N'Shift.Update',N'Cập nhật lịch làm việc',N'Update',N'Cập nhật lịch làm việc',1,'2026-01-01'),
 (103,21,N'Shift.Cancel',N'Hủy lịch làm việc',N'Cancel',N'Hủy lịch làm việc và giữ lịch sử',1,'2026-01-01'),

 (147,22, N'OperationalIce.View', N'Xem quản lý đá vận hành', N'View', N'Xem ca vận hành, phân bổ và đối soát đá', 1,'2026-07-29'),
 (148,22, N'OperationalIce.Manage', N'Vận hành phân bổ đá', N'Manage', N'Tạo ca, mở phân bổ, cấp bổ sung và bàn giao đá', 0,'2026-07-29'),
 (149,22, N'OperationalIce.Approve', N'Duyệt đối soát đá', N'Approve', N'Duyệt cấp bổ sung và chênh lệch đá cuối ca', 0,'2026-07-29'),
 (150,22, N'OperationalIce.Policy', N'Cấu hình chính sách đá', N'Policy', N'Cấu hình định mức và ngưỡng đối soát đá theo cửa hàng', 0,'2026-07-29'),

 (108,23,N'Store.View',N'Xem cửa hàng',N'View',N'Xem cửa hàng',1,'2026-01-01'),
 (109,23,N'Store.Create',N'Tạo cửa hàng',N'Create',N'Tạo cửa hàng',1,'2026-01-01'),
 (110,23,N'Store.Update',N'Cập nhật cửa hàng',N'Update',N'Cập nhật cửa hàng',1,'2026-01-01'),
 (111,23,N'Store.ToggleStatus',N'Đổi trạng thái cửa hàng',N'ToggleStatus',N'Đổi trạng thái cửa hàng',1,'2026-01-01'),

 (112,24,N'Settings.View',N'Xem cài đặt hệ thống',N'View',N'Xem cài đặt hệ thống',1,'2026-01-01'),
 (113,24,N'Settings.Update',N'Cập nhật cài đặt hệ thống',N'Update',N'Cập nhật cài đặt hệ thống',1,'2026-01-01'),

 (114,25,N'Recipe.View',N'Xem BOM',N'View',N'Xem BOM',1,'2026-01-01'),
 (115,25,N'Recipe.Create',N'Tạo BOM',N'Create',N'Tạo BOM',1,'2026-01-01'),
 (116,25,N'Recipe.Update',N'Cập nhật BOM',N'Update',N'Cập nhật BOM',1,'2026-01-01'),

 (117,25,N'PreparedItem.View',N'Xem bán thành phẩm',N'PreparedItemView',N'Xem bán thành phẩm',1,'2026-01-01'),
 (118,25,N'PreparedItem.Create',N'Tạo bán thành phẩm',N'PreparedItemCreate',N'Tạo bán thành phẩm',1,'2026-01-01'),
 (119,25,N'PreparedItem.Update',N'Cập nhật bán thành phẩm',N'PreparedItemUpdate',N'Cập nhật bán thành phẩm',1,'2026-01-01'),

 (120,25,N'ProductionOrder.View',N'Xem lệnh sản xuất',N'ProductionOrderView',N'Xem lệnh sản xuất',1,'2026-01-01'),
 (121,25,N'ProductionOrder.Create',N'Tạo lệnh sản xuất',N'ProductionOrderCreate',N'Tạo lệnh sản xuất',1,'2026-01-01'),
 (122,25,N'ProductionOrder.Confirm',N'Xác nhận lệnh sản xuất',N'ProductionOrderConfirm',N'Xác nhận lệnh sản xuất',1,'2026-01-01'),
 (151,25,N'ProductionOrder.Plan',N'Lập kế hoạch sản xuất',N'Plan',N'Lập kế hoạch số mẻ sản xuất trong phạm vi cửa hàng',1,'2026-08-09'),
 (152,25,N'ProductionOrder.Release',N'Phát hành lệnh sản xuất',N'Release',N'Phát hành lệnh đã lập kế hoạch để ca vận hành tiếp nhận',1,'2026-08-09'),
 (153,25,N'ProductionOrder.Start',N'Bắt đầu lệnh sản xuất',N'Start',N'Bắt đầu thực hiện lệnh sản xuất đã phát hành',1,'2026-08-09'),
 (154,25,N'ProductionOrder.RecordActual',N'Ghi nhận sản xuất thực tế',N'RecordActual',N'Xác nhận đầu vào và sản lượng thực tế của lệnh sản xuất',1,'2026-08-09'),
 (155,25,N'ProductionOrder.AcceptOutput',N'Xác nhận đầu ra sản xuất',N'AcceptOutput',N'Tiêu thụ đầu vào FIFO và nhập sản lượng đạt vào tồn kho',1,'2026-08-09'),
 (156,25,N'ProductionOrder.ApproveVariance',N'Duyệt chênh lệch sản xuất',N'ApproveVariance',N'Duyệt chênh lệch sản lượng vượt ngưỡng theo maker-checker',1,'2026-08-09'),
 (157,25,N'ProductionOrder.Cancel',N'Hủy lệnh sản xuất',N'Cancel',N'Hủy lệnh sản xuất chưa bắt đầu và giữ lịch sử',1,'2026-08-09'),
 (158,25,N'Restock.SelectProductionSource',N'Chọn nguồn sản xuất cho yêu cầu',N'SelectProductionSource',N'Chọn nguồn sản xuất khi resolver xác nhận item và cửa hàng đủ điều kiện',1,'2026-08-09'),

 (123,1,N'StoreMenu.View',N'Xem menu cửa hàng',N'StoreMenuView',N'Xem menu cửa hàng',1,'2026-01-01'),
 (124,1,N'StoreMenu.Update',N'Cập nhật menu cửa hàng',N'StoreMenuUpdate',N'Cập nhật menu cửa hàng',1,'2026-01-01'),

 (125,1,N'Profitability.View',N'Xem vốn và lợi nhuận',N'ProfitabilityView',N'Xem vốn và lợi nhuận',1,'2026-01-01'),

 (126,11,N'InventoryThreshold.View',N'Xem ngưỡng tồn',N'ThresholdView',N'Xem ngưỡng tồn',1,'2026-01-01'),
 (127,11,N'InventoryThreshold.Update',N'Cập nhật ngưỡng tồn',N'ThresholdUpdate',N'Cập nhật ngưỡng tồn',1,'2026-01-01'),

 (128,12,N'Notification.View',N'Xem thông báo kho',N'NotificationView',N'Xem thông báo kho',1,'2026-01-01'),

 (129,26,N'ReorderSuggestion.View',N'Xem gợi ý nhập hàng',N'View',N'Xem danh sách gợi ý nhập hàng trong phạm vi cửa hàng được phép truy cập',1,'2026-01-01'),

 (130,17,N'SupplierQuality.View',N'Xem báo cáo chất lượng NCC',N'SupplierQualityView',N'Xem báo cáo chất lượng NCC',1,'2026-01-01');


 IF EXISTS(SELECT 1 FROM @AdminPermissionSeed WHERE PermissionId=100)
  THROW 53321,N'PermissionId 100 được dành riêng cho rollback.',1;

 UPDATE p
 SET PermissionGroupId=x.PermissionGroupId,
     Name=x.Name,
     Action=x.Action,
     Description=x.Description,
     Active=x.Active
 FROM dbo.Permissions p
 JOIN @AdminPermissionSeed x ON x.Code=p.Code
 WHERE x.Code IN(N'ReorderSuggestion.View',N'Restock.Create');

 IF EXISTS(SELECT 1 FROM @AdminPermissionSeed x JOIN dbo.Permissions p
 ON p.PermissionId=x.PermissionId OR p.Code=x.Code
 OR(p.PermissionGroupId=x.PermissionGroupId AND p.Action=x.Action)
 WHERE p.PermissionId<>x.PermissionId OR p.PermissionGroupId<>x.PermissionGroupId
 OR p.Code<>x.Code OR p.Name<>x.Name OR p.Action<>x.Action
 OR ISNULL(p.Description,N'')<>ISNULL(x.Description,N'') OR p.Active<>x.Active
 OR p.CreatedAt<>x.CreatedAt)
  THROW 53322,N'Permission active-admin xung đột ID, Code hoặc Group/Action.',1;

 SET IDENTITY_INSERT dbo.Permissions ON;
 INSERT dbo.Permissions(PermissionId,PermissionGroupId,Code,Name,Action,Description,Active,CreatedAt)
 SELECT * FROM @AdminPermissionSeed x WHERE NOT EXISTS(
 SELECT 1 FROM dbo.Permissions p WHERE p.PermissionId=x.PermissionId);
 SET IDENTITY_INSERT dbo.Permissions OFF;

 DECLARE @AdminRolePermissionSeed TABLE(RoleId int,PermissionId int,PRIMARY KEY(RoleId,PermissionId));
 INSERT @AdminRolePermissionSeed VALUES
 (1,28),
 (1,29),
 (1,30),
 (1,31),
 (1,32),
 (1,33),
 (1,34),
 (1,35),
 (1,36),
 (1,37),
 (1,38),
 (1,39),
 (1,40),
 (1,41),
 (1,42),
 (1,43),
 (1,44),
 (1,45),
 (1,46),
 (1,47),
 (1,48),
 (1,49),
 (1,50),
 (1,51),
 (1,52),
 (1,53),
 (1,54),
 (1,55),
 (1,56),
 (1,59),
 (1,61),
 (1,62),
 (1,63),
 (1,64),
 (1,65),
 (1,69),
 (1,70),
 (1,71),
 (1,72),
 (1,73),
 (1,74),
 (1,75),
 (1,76),
 (1,77),
 (1,78),
 (1,79),
 (1,80),
 (1,81),
 (1,82),
 (1,83),
 (1,84),
 (1,85),
 (1,86),
 (1,87),
 (1,88),
 (1,89),
 (1,90),
 (1,91),
 (1,92),
 (1,93),
 (1,94),
 (1,95),
 (1,96),
 (1,97),
 (1,98),
 (1,99),
 (1,101),
 (1,102),
 (1,103),
 (1,108),
 (1,109),
 (1,110),
 (1,111),
 (1,112),
 (1,113),
 (1,114),
 (1,115),
 (1,116),
 (1,117),
 (1,118),
 (1,119),
 (1,120),
 (1,121),
 (1,122),
 (1,123),
 (1,124),
 (1,125),
 (1,126),
 (1,127),
 (1,128),
 (1,129),
 (1,130),
 (1,140), -- PurchaseOrder.Approve
 (1,141), -- PurchaseOrder.RejectApproval
 (1,142), -- PurchaseOrder.OverrideAllocation
 (1,143), -- PurchaseOrder.Export
 (1,146), -- Receipt.ViewCost
 (1,147),
 (2,28),
 (2,32),
 (2,36),
 (2,38),
 (2,39),
 (2,40),
 (2,43),
 (2,49),
 (2,56),
 (2,62),
 (2,65),
 (2,70),
 (2,74),
 (2,75),
 (2,82),
 (2,89),
 (2,93),
 (2,94),
 (2,99),
 (2,101),
 (2,102),
 (2,103),
 (2,108),
 (2,110),
 (2,112),
 (2,114),
 (2,117),
 (2,120),
 (2,123),
 (2,125),
 (2,126),
 (2,128),
 (2,129),
 (2,130),
 (2,146), -- Receipt.ViewCost
 (2,147),
 (3,36),
 (3,39),
 (3,40),
 (3,43),
 (3,44),
 (3,45),
 (3,48),
 (3,49),
 (3,50),
 (3,51),
 (3,60),
 (3,65),
 (3,66),
 (3,67),
 (3,68),
 (3,75),
 (3,76),
 (3,77),
 (3,80),
 (3,82),
 (3,83),
 (3,84),
 (3,85),
 (3,86),
 (3,87),
 (3,89),
 (3,90),
 (3,91),
 (3,92),
 (3,94),
 (3,96),
 (3,97),
 (3,99),
 (3,101),
 (3,102),
 (3,103),
 (3,108),
 (3,114),
 (3,117),
 (3,120),
 (3,123),
 (3,124),
 (3,126),
 (3,128),
 (3,131), -- StockAlert.Create
 (3,132), -- StockAlert.CreateRestockRequest
 (3,133), -- Restock.Update
 (3,144), -- Receipt.UpdateDraft
 (3,145), -- Receipt.RecordSupplierIssue
 (3,147),
 (4,147),
 (5,28),
 (5,29),
 (5,30),
 (5,31),
 (5,32),
 (5,33),
 (5,34),
 (5,35),
 (5,36),
 (5,37),
 (5,38),
 (5,39),
 (5,40),
 (5,41),
 (5,42),
 (5,43),
 (5,44),
 (5,45),
 (5,46),
 (5,47),
 (5,48),
 (5,49),
 (5,50),
 (5,51),
 (5,52),
 (5,53),
 (5,54),
 (5,55),
 (5,56),
 (5,57),
 (5,58),
 (5,59),
 (5,61),
 (5,62),
 (5,63),
 (5,64),
 (5,65),
 (5,69),
 (5,70),
 (5,71),
 (5,72),
 (5,73),
 (5,74),
 (5,75),
 (5,76),
 (5,77),
 (5,78),
 (5,79),
 (5,80),
 (5,81),
 (5,82),
 (5,83),
 (5,84),
 (5,85),
 (5,86),
 (5,87),
 (5,88),
 (5,114),
 (5,115),
 (5,116),
 (5,117),
 (5,118),
 (5,119),
 (5,120),
 (5,121),
 (5,122),
 (5,125),
 (5,126),
 (5,127),
 (5,128),
 (5,129),
 (5,131), -- StockAlert.Create
 (5,134), -- Restock.CloseRemaining
 (5,135), -- Restock.CreatePurchaseOrder
 (5,136), -- Restock.CreateTransfer
 (5,137), -- PurchaseAdvice.SelectSupplier
 (5,138), -- PurchaseAdvice.CreatePurchaseOrder
 (5,139), -- PurchaseOrder.Submit
 (5,143), -- PurchaseOrder.Export
 (5,146), -- Receipt.ViewCost
 (5,147),
 (6,28),
 (6,29),
 (6,30),
 (6,31),
 (6,32),
 (6,33),
 (6,34),
 (6,35),
 (6,36),
 (6,37),
 (6,38),
 (6,39),
 (6,40),
 (6,41),
 (6,42),
 (6,43),
 (6,44),
 (6,45),
 (6,46),
 (6,47),
 (6,48),
 (6,49),
 (6,50),
 (6,51),
 (6,52),
 (6,53),
 (6,54),
 (6,55),
 (6,56),
 (6,57),
 (6,58),
 (6,59),
 (6,60),
 (6,61),
 (6,62),
 (6,63),
 (6,64),
 (6,65),
 (6,66),
 (6,67),
 (6,68),
 (6,69),
 (6,70),
 (6,71),
 (6,72),
 (6,73),
 (6,74),
 (6,75),
 (6,76),
 (6,77),
 (6,78),
 (6,79),
 (6,80),
 (6,81),
 (6,82),
 (6,83),
 (6,84),
 (6,85),
 (6,86),
 (6,87),
 (6,88),
 (6,89),
 (6,90),
 (6,91),
 (6,92),
 (6,93),
 (6,94),
 (6,95),
 (6,96),
 (6,97),
 (6,98),
 (6,99),
 (6,101),
 (6,102),
 (6,103),
 (6,108),
 (6,109),
 (6,110),
 (6,111),
 (6,112),
 (6,113),
 (6,114),
 (6,115),
 (6,116),
 (6,117),
 (6,118),
 (6,119),
 (6,120),
 (6,121),
 (6,122),
 (6,123),
 (6,124),
 (6,125),
 (6,126),
 (6,127),
 (6,128),
 (6,129),
 (6,130),
 (6,147),
 (8,147);


IF EXISTS
(
    SELECT 1
    FROM @AdminRolePermissionSeed x
    LEFT JOIN dbo.Roles r
      ON r.RoleId=x.RoleId
     AND r.Active=1
    LEFT JOIN dbo.Permissions p
      ON p.PermissionId=x.PermissionId
    WHERE r.RoleId IS NULL
       OR p.PermissionId IS NULL
)
    THROW 53323,
        N'RolePermission active-admin chứa FK không hợp lệ.',
        1;

 IF EXISTS
(
    SELECT 1
    FROM @AdminRolePermissionSeed x
    LEFT JOIN dbo.Roles r
      ON r.RoleId=x.RoleId
     AND r.Active=1
    LEFT JOIN dbo.Permissions p
      ON p.PermissionId=x.PermissionId
    WHERE r.RoleId IS NULL
       OR p.PermissionId IS NULL
)
    THROW 53323,
        N'RolePermission active-admin chứa FK không hợp lệ.',
        1;

INSERT dbo.RolePermissions(RoleId,PermissionId)
SELECT x.RoleId,x.PermissionId
FROM @AdminRolePermissionSeed x
JOIN dbo.Permissions p
  ON p.PermissionId=x.PermissionId
 AND p.Active=1
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.RolePermissions rp
    WHERE rp.RoleId=x.RoleId
      AND rp.PermissionId=x.PermissionId
);

IF EXISTS
(
    SELECT 1
    FROM @AdminPermissionGroupSeed x
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.PermissionGroups g
        WHERE g.PermissionGroupId=x.PermissionGroupId
    )
)
OR EXISTS
(
    SELECT 1
    FROM @AdminPermissionSeed x
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.Permissions p
        WHERE p.PermissionId=x.PermissionId
    )
)
OR EXISTS
(
    SELECT 1
    FROM @AdminRolePermissionSeed x
    JOIN dbo.Permissions p
      ON p.PermissionId=x.PermissionId
     AND p.Active=1
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.RolePermissions rp
        WHERE rp.RoleId=x.RoleId
          AND rp.PermissionId=x.PermissionId
    )
)
OR EXISTS
(
    SELECT Code
    FROM dbo.Permissions
    GROUP BY Code
    HAVING COUNT(*)>1
)
OR EXISTS
(
    SELECT PermissionGroupId,Action
    FROM dbo.Permissions
    GROUP BY PermissionGroupId,Action
    HAVING COUNT(*)>1
)
    THROW 53324,
        N'Catalog RBAC active-admin thiếu dữ liệu hoặc trùng khóa nghiệp vụ.',
        1;


 COMMIT;
END TRY
BEGIN CATCH
 BEGIN TRY SET IDENTITY_INSERT dbo.PermissionGroups OFF; END TRY BEGIN CATCH END CATCH;
 BEGIN TRY SET IDENTITY_INSERT dbo.Permissions OFF; END TRY BEGIN CATCH END CATCH;
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
GO

/* ============================================================
   RBAC_CAFECHAIN_FINAL_V3 - LEAST PRIVILEGE RECONCILIATION
   Compatibility marker: RBAC_CAFECHAIN29_V2 is superseded by this contract.
   Replaces original SeedAll.sql lines 6909-7558.
   - Preserves AccountPermissionOverrides exactly.
   - Uses permission Code and role Name as stable identities.
   - Adds Dashboard AI/financial and Operational Anomaly permissions.
   - Deactivates 29 main-catalog orphan/legacy permissions.
   - Keeps the explicit POS/WorkShift capability catalog.
   ============================================================ */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRANSACTION;

 IF OBJECT_ID(N'dbo.PermissionGroups',N'U') IS NULL
 OR OBJECT_ID(N'dbo.Permissions',N'U') IS NULL
 OR OBJECT_ID(N'dbo.Roles',N'U') IS NULL
 OR OBJECT_ID(N'dbo.RolePermissions',N'U') IS NULL
 OR OBJECT_ID(N'dbo.AccountPermissionOverrides',N'U') IS NULL
  THROW 53340,N'RBAC_CAFECHAIN_FINAL_V3: thiếu bảng RBAC bắt buộc.',1;

 DROP TABLE IF EXISTS #ExpectedRolePermissions;
 DROP TABLE IF EXISTS #PermissionMatrix;
 DROP TABLE IF EXISTS #PosPermissionMatrix;
 DROP TABLE IF EXISTS #NewPermissionCatalog;
 DROP TABLE IF EXISTS #RoleMap;
 DROP TABLE IF EXISTS #OverrideBefore;
 DROP TABLE IF EXISTS #ManagedPermissionCodes;
 DROP TABLE IF EXISTS #ExpectedRoleCounts;

 SELECT AccountPermissionOverrideId,AccountId,PermissionId,Effect,Reason
 INTO #OverrideBefore
 FROM dbo.AccountPermissionOverrides;

 CREATE TABLE #RoleMap
 (
  RoleKey nvarchar(10) NOT NULL PRIMARY KEY,
  RoleName nvarchar(100) NOT NULL UNIQUE,
  RoleId int NULL
 );
 INSERT #RoleMap(RoleKey,RoleName) VALUES
 (N'CDN',N'Chủ doanh nghiệp'),
 (N'QLV',N'Quản lý vùng'),
 (N'QLCN',N'Quản lý chi nhánh'),
 (N'NVBH',N'Nhân viên bán hàng'),
 (N'KTK',N'Kế toán/kho'),
 (N'QTHT',N'Quản trị hệ thống'),
 (N'CT',N'Ca trưởng');

 IF EXISTS
 (
  SELECT rm.RoleName
  FROM #RoleMap rm
  LEFT JOIN dbo.Roles r ON r.Name=rm.RoleName AND r.Active=1
  GROUP BY rm.RoleName
  HAVING COUNT(r.RoleId)<>1
 )
  THROW 53341,N'RBAC_CAFECHAIN_FINAL_V3: mỗi role phải khớp đúng một dòng active.',1;

 UPDATE rm SET RoleId=r.RoleId
 FROM #RoleMap rm
 JOIN dbo.Roles r ON r.Name=rm.RoleName AND r.Active=1;

 /* Dedicated permission group for per-widget Dashboard grants. The identity
    value is intentionally database-generated so existing installations that
    already created POS_WORKSHIFT remain compatible. */
 IF EXISTS
 (
  SELECT 1
  FROM dbo.PermissionGroups
  WHERE (Code=N'DASHBOARD_WIDGET' AND Name<>N'Widget Dashboard')
     OR (Name=N'Widget Dashboard' AND Code<>N'DASHBOARD_WIDGET')
 )
  THROW 53349,N'RBAC_CAFECHAIN_FINAL_V3: nhóm quyền Dashboard widget xung đột Code hoặc Name.',1;

 IF NOT EXISTS(SELECT 1 FROM dbo.PermissionGroups WHERE Code=N'DASHBOARD_WIDGET')
  INSERT dbo.PermissionGroups(Code,Name,DisplayOrder,Active)
  VALUES(N'DASHBOARD_WIDGET',N'Widget Dashboard',9,1);
 ELSE
  UPDATE dbo.PermissionGroups
  SET DisplayOrder=9,Active=1
 WHERE Code=N'DASHBOARD_WIDGET';

 IF NOT EXISTS(SELECT 1 FROM dbo.PermissionGroups WHERE Code=N'AI_IMPORT')
  INSERT dbo.PermissionGroups(Code,Name,DisplayOrder,Active)
  VALUES(N'AI_IMPORT',N'AI Smart Import',10,1);
 ELSE
  UPDATE dbo.PermissionGroups
  SET Name=N'AI Smart Import',DisplayOrder=10,Active=1
  WHERE Code=N'AI_IMPORT';

 /* New/dynamic permissions. PermissionId remains database-generated;
    Code is the stable identity, matching the current SeedAll design. */
 CREATE TABLE #NewPermissionCatalog
 (
  Code nvarchar(100) NOT NULL PRIMARY KEY,
  GroupCode nvarchar(50) NOT NULL,
  Name nvarchar(200) NOT NULL,
  Action nvarchar(50) NOT NULL,
  Description nvarchar(500) NOT NULL
 );
 INSERT #NewPermissionCatalog VALUES
  (N'ReorderSuggestion.View',N'REORDER_SUGGESTION',N'Xem gợi ý nhập hàng',N'View',N'Xem danh sách gợi ý nhập hàng trong phạm vi cửa hàng được phép truy cập'),
  (N'Restock.Create',N'RESTOCK',N'Tạo yêu cầu nhập hàng',N'Create',N'Tạo mới, tạo nháp hoặc bổ sung yêu cầu nhập hàng từ gợi ý nhập hàng trong phạm vi cửa hàng được phép thao tác'),
  (N'OperationalIce.ConfigurePolicy',N'OPERATIONAL_ICE',N'Cấu hình chính sách đá',N'ConfigurePolicy',N'Cấu hình định mức và ngưỡng đối soát đá trong phạm vi cửa hàng'),
  (N'OperationalIce.CreateShift',N'OPERATIONAL_ICE',N'Tạo ca vận hành đá',N'CreateShift',N'Tạo và cập nhật kế hoạch ca vận hành đá trong phạm vi cửa hàng'),
  (N'OperationalIce.OpenShift',N'OPERATIONAL_ICE',N'Mở ca vận hành đá',N'OpenShift',N'Xác nhận cấp đầu ca và mở phân bổ đá'),
  (N'OperationalIce.LinkWorkShift',N'OPERATIONAL_ICE',N'Liên kết WorkShift POS',N'LinkWorkShift',N'Liên kết WorkShift POS hợp lệ vào ca vận hành đá'),
  (N'OperationalIce.RequestSupplement',N'OPERATIONAL_ICE',N'Yêu cầu cấp bổ sung đá',N'RequestSupplement',N'Gửi yêu cầu cấp bổ sung cho ca vận hành đá được phân công'),
  (N'OperationalIce.ApproveSupplement',N'OPERATIONAL_ICE',N'Duyệt cấp bổ sung đá',N'ApproveSupplement',N'Duyệt hoặc từ chối yêu cầu cấp bổ sung đá'),
  (N'OperationalIce.Handoff',N'OPERATIONAL_ICE',N'Bàn giao đá giữa ca',N'Handoff',N'Xác nhận bàn giao đá giữa các ca cùng ngày'),
  (N'OperationalIce.SubmitClose',N'OPERATIONAL_ICE',N'Gửi chốt ca đá',N'SubmitClose',N'Gửi số liệu chốt ca vận hành đá'),
  (N'OperationalIce.ApproveVariance',N'OPERATIONAL_ICE',N'Duyệt chênh lệch đá',N'ApproveVariance',N'Duyệt hao hụt hoặc hoàn tất đối soát chênh lệch đá'),
  (N'OperationalIce.CancelScheduledShift',N'OPERATIONAL_ICE',N'Hủy ca đá chưa mở',N'CancelScheduledShift',N'Hủy ca vận hành đá còn ở trạng thái kế hoạch'),
  (N'OperationalIce.ViewReport',N'OPERATIONAL_ICE',N'Xem báo cáo ca đá',N'ViewReport',N'Xem và tải báo cáo vận hành đá trong phạm vi được cấp'),
  (N'StoreMenu.OverridePrice',N'DRINK',N'Ghi đè giá menu cửa hàng',N'OverridePrice',N'Ghi đè giá bán tại menu cửa hàng'),
  (N'Profitability.UpdatePrice',N'DRINK',N'Cập nhật giá bán',N'UpdatePrice',N'Cập nhật giá bán toàn hệ thống'),
  (N'Profitability.UpdateToppingPolicy',N'DRINK',N'Cập nhật chính sách topping',N'UpdateToppingPolicy',N'Cập nhật chính sách topping theo món và size'),
  (N'PreparedItem.ToggleStatus',N'BOM',N'Đổi trạng thái bán thành phẩm',N'PreparedItemToggleStatus',N'Kích hoạt hoặc ngưng bán thành phẩm'),
  (N'Recipe.Delete',N'BOM',N'Xóa hoặc ngưng công thức',N'RecipeDelete',N'Xóa hoặc ngưng công thức theo nghiệp vụ'),
  (N'PurchaseAdvice.Update',N'PURCHASE_ADVICE',N'Cập nhật đề nghị mua',N'Update',N'Cập nhật đề nghị mua ở trạng thái cho phép'),
  (N'PurchaseAdvice.Cancel',N'PURCHASE_ADVICE',N'Hủy đề nghị mua',N'Cancel',N'Hủy đề nghị mua trước khi bị khóa nghiệp vụ'),
  (N'PurchaseOrder.CloseRemaining',N'PURCHASE_ORDER',N'Đóng phần còn lại PO',N'CloseRemaining',N'Đóng số lượng còn lại của dòng PO'),
  (N'SupplierQuality.Create',N'SUPPLIER',N'Ghi nhận chất lượng nhà cung cấp',N'SupplierQualityCreate',N'Ghi nhận sự cố hoặc chất lượng nhà cung cấp'),
  (N'SupplierQuality.Transition',N'SUPPLIER',N'Chuyển trạng thái sự cố nhà cung cấp',N'SupplierQualityTransition',N'Xác minh hoặc đóng sự cố nhà cung cấp'),
  (N'InventoryTransfer.RequestReturn',N'INVENTORY_TRANSFER',N'Yêu cầu trả hàng điều chuyển',N'RequestReturn',N'Yêu cầu trả hàng trong luồng điều chuyển'),
  (N'InventoryTransfer.ConfirmReturn',N'INVENTORY_TRANSFER',N'Xác nhận trả hàng điều chuyển',N'ConfirmReturn',N'Xác nhận trả hàng trong luồng điều chuyển'),
  (N'InventoryTransfer.ResolveDiscrepancy',N'INVENTORY_TRANSFER',N'Xử lý chênh lệch điều chuyển',N'ResolveDiscrepancy',N'Xử lý thiếu hụt hoặc chênh lệch điều chuyển cuối'),
  (N'Order.RefundRequest',N'ORDER',N'Tạo yêu cầu hoàn tiền',N'RefundRequest',N'Tạo yêu cầu hoàn tiền đơn hàng'),
  (N'Order.RefundConfirm',N'ORDER',N'Xác nhận hoàn tiền',N'RefundConfirm',N'Xác nhận yêu cầu hoàn tiền đơn hàng'),
  (N'System.Diagnostics.View',N'SYSTEM',N'Xem chẩn đoán hệ thống',N'DiagnosticsView',N'Xem health và diagnostics kỹ thuật'),
  (N'System.Cutover.View',N'SYSTEM',N'Xem trạng thái cutover',N'CutoverView',N'Xem trạng thái cutover'),
  (N'System.Cutover.Manage',N'SYSTEM',N'Quản lý cutover',N'CutoverManage',N'Kích hoạt hoặc chặn cutover'),
  (N'System.LegacyConsolidation.View',N'SYSTEM',N'Xem hợp nhất dữ liệu legacy',N'LegacyConsolidationView',N'Xem audit và dry-run hợp nhất BTP legacy'),
  (N'System.LegacyConsolidation.Manage',N'SYSTEM',N'Quản lý hợp nhất dữ liệu legacy',N'LegacyConsolidationManage',N'Thực thi hợp nhất BTP legacy'),
  (N'Dashboard.Executive.View',N'APPLICATION',N'Xem Dashboard điều hành cấp chủ doanh nghiệp',N'ExecutiveView',N'Xem các chỉ số điều hành và chiến lược toàn chuỗi'),
  (N'Dashboard.Operations.View',N'APPLICATION',N'Xem Dashboard vận hành',N'OperationsView',N'Xem các chỉ số vận hành trong phạm vi được phân công'),
  (N'Dashboard.Inventory.View',N'APPLICATION',N'Xem Dashboard tồn kho',N'InventoryView',N'Xem section tồn kho trên Dashboard trong phạm vi được phân công'),
  (N'Dashboard.Procurement.View',N'APPLICATION',N'Xem Dashboard mua hàng',N'ProcurementView',N'Xem section mua hàng trên Dashboard trong phạm vi được phân công'),
  (N'Dashboard.Product.View',N'APPLICATION',N'Xem Dashboard sản phẩm',N'ProductView',N'Xem section sản phẩm, giá vốn và lợi nhuận theo quyền'),
  (N'Dashboard.Workforce.View',N'APPLICATION',N'Xem Dashboard nhân sự',N'WorkforceView',N'Xem section nhân sự và lịch làm việc trong phạm vi được phân công');

 INSERT #NewPermissionCatalog VALUES
  (N'Dashboard.AI.Use',N'APPLICATION',N'Sử dụng AI Dashboard',N'AIUse',N'Giải thích evidence Dashboard đã được backend cho phép'),
  (N'Dashboard.FinancialSummary.View',N'APPLICATION',N'Xem tổng hợp tài chính',N'FinancialSummaryView',N'Xem doanh thu, lợi nhuận và tổng hợp tài chính trong StaffScope'),
  (N'OperationalAnomaly.View',N'APPLICATION',N'Xem bất thường vận hành',N'AnomalyView',N'Xem tín hiệu bất thường trong StaffScope'),
  (N'OperationalAnomaly.Acknowledge',N'APPLICATION',N'Ghi nhận bất thường',N'AnomalyAcknowledge',N'Ghi nhận đã tiếp nhận tín hiệu bất thường'),
  (N'OperationalAnomaly.Resolve',N'APPLICATION',N'Giải quyết bất thường',N'AnomalyResolve',N'Đóng tín hiệu sau khi kiểm tra'),
  (N'OperationalAnomaly.Feedback',N'APPLICATION',N'Phản hồi bất thường',N'AnomalyFeedback',N'Ghi Useful, NotUseful hoặc FalsePositive cho pilot');

 INSERT #NewPermissionCatalog VALUES
  (N'Dashboard.Widget.NetSalesTrend.View',N'DASHBOARD_WIDGET',N'Xem xu hướng doanh thu',N'ViewNetSalesTrend',N'Xem widget xu hướng doanh thu trong StaffScope'),
  (N'Dashboard.Widget.StoreRanking.View',N'DASHBOARD_WIDGET',N'Xem xếp hạng cửa hàng',N'ViewStoreRanking',N'Xem widget xếp hạng cửa hàng trong StaffScope'),
  (N'Dashboard.Widget.PaymentMethodMix.View',N'DASHBOARD_WIDGET',N'Xem phương thức thanh toán',N'ViewPaymentMethodMix',N'Xem widget mức sử dụng phương thức thanh toán trong StaffScope'),
  (N'Dashboard.Widget.OrderHeatmap.View',N'DASHBOARD_WIDGET',N'Xem phân bố đơn theo giờ',N'ViewOrderHeatmap',N'Xem widget phân bố đơn theo ngày và giờ trong StaffScope'),
  (N'Dashboard.Widget.OperationalAlerts.View',N'DASHBOARD_WIDGET',N'Xem cảnh báo vận hành',N'ViewOperationalAlerts',N'Xem widget cảnh báo vận hành trong StaffScope'),
  (N'Dashboard.Widget.OrderStatusSummary.View',N'DASHBOARD_WIDGET',N'Xem tình trạng đơn hàng',N'ViewOrderStatusSummary',N'Xem widget tình trạng đơn hàng trong StaffScope'),
  (N'Dashboard.Widget.WorkShiftCashDiscrepancy.View',N'DASHBOARD_WIDGET',N'Xem chênh lệch tiền mặt theo ca',N'ViewWorkShiftCashDiscrepancy',N'Xem widget chênh lệch tiền mặt theo ca trong StaffScope'),
  (N'Dashboard.Widget.WorkShiftSales.View',N'DASHBOARD_WIDGET',N'Xem doanh thu theo ca',N'ViewWorkShiftSales',N'Xem widget doanh thu theo ca trong StaffScope'),
  (N'Dashboard.Widget.WorkShiftPaymentMix.View',N'DASHBOARD_WIDGET',N'Xem thanh toán theo ca',N'ViewWorkShiftPaymentMix',N'Xem widget thanh toán theo ca trong StaffScope'),
  (N'Dashboard.Widget.OfflineReconciliationExceptions.View',N'DASHBOARD_WIDGET',N'Xem đối soát đơn ngoại tuyến',N'ViewOfflineReconciliation',N'Xem widget đối soát đơn ngoại tuyến trong StaffScope'),
  (N'Dashboard.Widget.HourlyOrders.View',N'DASHBOARD_WIDGET',N'Xem đơn hàng theo giờ',N'ViewHourlyOrders',N'Xem widget đơn hàng theo giờ trong StaffScope'),
  (N'Dashboard.Widget.WorkShiftTopDiscrepancies.View',N'DASHBOARD_WIDGET',N'Xem ca chênh lệch tiền mặt cao',N'ViewWorkShiftTopDiscrepancies',N'Xem widget ca có chênh lệch tiền mặt cao trong StaffScope'),
  (N'Dashboard.Widget.WorkShiftKpis.View',N'DASHBOARD_WIDGET',N'Xem chỉ số vận hành ca',N'ViewWorkShiftKpis',N'Xem widget chỉ số vận hành ca trong StaffScope'),
  (N'Dashboard.Widget.InventoryShortageRisk.View',N'DASHBOARD_WIDGET',N'Xem nguyên liệu dưới ngưỡng tồn',N'ViewInventoryShortageRisk',N'Xem widget nguyên liệu dưới ngưỡng tồn trong StaffScope'),
  (N'Dashboard.Widget.InventoryMovementByType.View',N'DASHBOARD_WIDGET',N'Xem biến động kho',N'ViewInventoryMovementByType',N'Xem widget biến động kho trong StaffScope'),
  (N'Dashboard.Widget.InventoryThresholdRisk.View',N'DASHBOARD_WIDGET',N'Xem rủi ro ngưỡng tồn kho',N'ViewInventoryThresholdRisk',N'Xem widget rủi ro ngưỡng tồn kho trong StaffScope'),
  (N'Dashboard.Widget.InventoryReorderSuggestions.View',N'DASHBOARD_WIDGET',N'Xem gợi ý nhập hàng',N'ViewInventoryReorderSuggestions',N'Xem widget gợi ý nhập hàng trong StaffScope'),
  (N'Dashboard.Widget.InventoryWasteByStoreIngredient.View',N'DASHBOARD_WIDGET',N'Xem hao hụt kho',N'ViewInventoryWaste',N'Xem widget hao hụt kho trong StaffScope'),
  (N'Dashboard.Widget.InventoryFifoLayerAge.View',N'DASHBOARD_WIDGET',N'Xem tuổi lớp tồn FIFO',N'ViewInventoryFifoLayerAge',N'Xem widget tuổi lớp tồn FIFO trong StaffScope'),
  (N'Dashboard.Widget.IngredientConsumptionTrend.View',N'DASHBOARD_WIDGET',N'Xem xu hướng tiêu thụ nguyên liệu',N'ViewIngredientConsumptionTrend',N'Xem widget xu hướng tiêu thụ nguyên liệu trong StaffScope'),
  (N'Dashboard.Widget.PurchaseOrderPipeline.View',N'DASHBOARD_WIDGET',N'Xem tiến độ đơn mua hàng',N'ViewPurchaseOrderPipeline',N'Xem widget tiến độ đơn mua hàng trong StaffScope'),
  (N'Dashboard.Widget.OverduePurchaseOrders.View',N'DASHBOARD_WIDGET',N'Xem đơn mua hàng quá hạn',N'ViewOverduePurchaseOrders',N'Xem widget đơn mua hàng quá hạn trong StaffScope'),
  (N'Dashboard.Widget.SupplierQuality.View',N'DASHBOARD_WIDGET',N'Xem chất lượng nhà cung cấp',N'ViewSupplierQuality',N'Xem widget chất lượng nhà cung cấp trong StaffScope'),
  (N'Dashboard.Widget.PurchasePriceTrend.View',N'DASHBOARD_WIDGET',N'Xem xu hướng giá mua',N'ViewPurchasePriceTrend',N'Xem widget xu hướng giá mua trong StaffScope'),
  (N'Dashboard.Widget.ProcurementSpendBreakdown.View',N'DASHBOARD_WIDGET',N'Xem chi phí mua hàng',N'ViewProcurementSpendBreakdown',N'Xem widget chi phí mua hàng trong StaffScope'),
  (N'Dashboard.Widget.SupplierIssueMix.View',N'DASHBOARD_WIDGET',N'Xem sự cố nhà cung cấp',N'ViewSupplierIssueMix',N'Xem widget sự cố nhà cung cấp trong StaffScope'),
  (N'Dashboard.Widget.TopProducts.View',N'DASHBOARD_WIDGET',N'Xem sản phẩm bán chạy',N'ViewTopProducts',N'Xem widget sản phẩm bán chạy trong StaffScope'),
  (N'Dashboard.Widget.VolumeMarginMatrix.View',N'DASHBOARD_WIDGET',N'Xem số lượng và biên lợi nhuận',N'ViewVolumeMarginMatrix',N'Xem widget số lượng và biên lợi nhuận trong StaffScope'),
  (N'Dashboard.Widget.SizeMargin.View',N'DASHBOARD_WIDGET',N'Xem hiệu quả theo kích cỡ',N'ViewSizeMargin',N'Xem widget hiệu quả theo kích cỡ trong StaffScope'),
  (N'Dashboard.Widget.TopToppings.View',N'DASHBOARD_WIDGET',N'Xem Topping bán chạy',N'ViewTopToppings',N'Xem widget Topping bán chạy trong StaffScope'),
  (N'Dashboard.Widget.BomHealth.View',N'DASHBOARD_WIDGET',N'Xem tình trạng BOM',N'ViewBomHealth',N'Xem widget tình trạng BOM trong StaffScope'),
  (N'Dashboard.Widget.HighConsumptionLowEfficiency.View',N'DASHBOARD_WIDGET',N'Xem sản phẩm hiệu quả thấp',N'ViewHighConsumptionLowEfficiency',N'Xem widget sản phẩm tiêu thụ cao nhưng hiệu quả thấp trong StaffScope'),
  (N'Dashboard.Widget.CategoryPerformance.View',N'DASHBOARD_WIDGET',N'Xem hiệu quả danh mục',N'ViewCategoryPerformance',N'Xem widget hiệu quả danh mục trong StaffScope'),
  (N'Dashboard.Widget.ProductPeriodPerformance.View',N'DASHBOARD_WIDGET',N'Xem hiệu quả sản phẩm theo kỳ',N'ViewProductPeriodPerformance',N'Xem widget hiệu quả sản phẩm theo kỳ trong StaffScope'),
  (N'Dashboard.Widget.LowVolumeProducts.View',N'DASHBOARD_WIDGET',N'Xem sản phẩm bán chậm',N'ViewLowVolumeProducts',N'Xem widget sản phẩm bán chậm trong StaffScope'),
  (N'Dashboard.Widget.LowMarginProducts.View',N'DASHBOARD_WIDGET',N'Xem sản phẩm biên lợi nhuận thấp',N'ViewLowMarginProducts',N'Xem widget sản phẩm biên lợi nhuận thấp trong StaffScope'),
  (N'Dashboard.Widget.WorkforceShiftStatus.View',N'DASHBOARD_WIDGET',N'Xem tình trạng ca nhân sự',N'ViewWorkforceShiftStatus',N'Xem widget tình trạng ca nhân sự trong StaffScope'),
  (N'Dashboard.Widget.WorkforceHourlyDemand.View',N'DASHBOARD_WIDGET',N'Xem nhu cầu nhân sự theo giờ',N'ViewWorkforceHourlyDemand',N'Xem widget nhu cầu nhân sự theo giờ trong StaffScope'),
  (N'Dashboard.Widget.WorkforceStaffPerformance.View',N'DASHBOARD_WIDGET',N'Xem hiệu suất nhân sự',N'ViewWorkforceStaffPerformance',N'Xem widget hiệu suất nhân sự trong StaffScope');

 INSERT #NewPermissionCatalog VALUES
  (N'AIImport.View',N'AI_IMPORT',N'Xem AI Smart Import',N'View',N'Xem giao diện và preview AI Smart Import'),
  (N'AIImport.Upload',N'AI_IMPORT',N'Tải Excel cho AI Smart Import',N'Upload',N'Tải tệp .xlsx an toàn để tạo phiên Smart Import'),
  (N'AIImport.Analyze',N'AI_IMPORT',N'Phân tích AI Smart Import',N'Analyze',N'Phân tích, sửa mapping và revalidate preview'),
  (N'AIImport.Confirm',N'AI_IMPORT',N'Xác nhận AI Smart Import',N'Confirm',N'Confirm nguyên tử toàn phiên; vẫn bắt buộc quyền Create của từng entity'),
  (N'AIImport.Cancel',N'AI_IMPORT',N'Hủy phiên AI Smart Import',N'Cancel',N'Hủy phiên Smart Import thuộc tài khoản hiện tại'),
  (N'AIImport.History',N'AI_IMPORT',N'Xem lịch sử AI Smart Import',N'History',N'Xem lịch sử phiên Smart Import thuộc tài khoản hiện tại');

 IF EXISTS
 (
  SELECT 1
  FROM #NewPermissionCatalog c
  LEFT JOIN dbo.PermissionGroups g ON g.Code=c.GroupCode AND g.Active=1
  GROUP BY c.Code,c.GroupCode
  HAVING COUNT(g.PermissionGroupId)<>1
 )
  THROW 53342,N'RBAC_CAFECHAIN_FINAL_V3: PermissionGroup của permission động thiếu hoặc trùng.',1;

 IF EXISTS
 (
  SELECT 1
  FROM #NewPermissionCatalog c
  JOIN dbo.Permissions p ON p.Code=c.Code
  JOIN dbo.PermissionGroups g ON g.Code=c.GroupCode
  WHERE p.PermissionGroupId<>g.PermissionGroupId
     OR p.Action<>c.Action
 )
  THROW 53343,N'RBAC_CAFECHAIN_FINAL_V3: permission động xung đột Code, Group hoặc Action.',1;

 UPDATE p
 SET PermissionGroupId=g.PermissionGroupId,
     Name=c.Name,
     Action=c.Action,
     Description=c.Description,
     Active=1
 FROM dbo.Permissions p
 JOIN #NewPermissionCatalog c ON c.Code=p.Code
 JOIN dbo.PermissionGroups g ON g.Code=c.GroupCode;

 INSERT dbo.Permissions(PermissionGroupId,Code,Name,Action,Description,Active,CreatedAt)
 SELECT g.PermissionGroupId,c.Code,c.Name,c.Action,c.Description,1,SYSUTCDATETIME()
 FROM #NewPermissionCatalog c
 JOIN dbo.PermissionGroups g ON g.Code=c.GroupCode
 WHERE NOT EXISTS(SELECT 1 FROM dbo.Permissions p WHERE p.Code=c.Code);

 /* Main catalog permissions that must stay inactive until a real
    server-side action is implemented and permission-checked. */
 UPDATE dbo.Permissions
 SET Active=0
 WHERE Code IN
 (
  N'Drink.Delete',
  N'Category.Delete',
  N'Size.Delete',
  N'Topping.Delete',
  N'Inventory.Adjust',
  N'Inventory.Export',
  N'InventoryTransfer.Export',
  N'Order.Refund',
  N'PurchaseAdvice.Approve',
  N'PurchaseAdvice.CreatePurchaseOrder',
  N'PurchaseOrder.Update',
  N'PurchaseOrder.Receive',
  N'PurchaseOrder.Consolidate',
  N'PurchaseOrder.Submit',
  N'PurchaseOrder.RejectApproval',
  N'PurchaseOrder.OverrideAllocation',
  N'Receipt.Reject',
  N'Receipt.Cancel',
  N'Receipt.RecordSupplierIssue',
  N'Restock.CreatePurchaseOrder',
  N'Restock.CreateTransfer',
  N'StockAlert.Configure',
  N'StockAlert.Create',
  N'StockAlert.Export',
  N'Supplier.ViewQuality',
  N'OperationalIce.Manage',
  N'OperationalIce.Approve',
  N'OperationalIce.Policy'
 );

 /* POS permission group and catalog. */
 IF EXISTS
 (
  SELECT 1 FROM dbo.PermissionGroups
  WHERE (Code=N'POS_WORKSHIFT' AND Name<>N'Phiên POS và trách nhiệm két')
     OR (Name=N'Phiên POS và trách nhiệm két' AND Code<>N'POS_WORKSHIFT')
 )
  THROW 53501,N'RBAC_CAFECHAIN_FINAL_V3: POS PermissionGroup xung đột Code hoặc Name.',1;

 IF NOT EXISTS(SELECT 1 FROM dbo.PermissionGroups WHERE Code=N'POS_WORKSHIFT')
  INSERT dbo.PermissionGroups(Code,Name,DisplayOrder,Active)
  VALUES(N'POS_WORKSHIFT',N'Phiên POS và trách nhiệm két',28,1);

 DECLARE @WorkShiftPermissionGroupId int=
 (
  SELECT PermissionGroupId
  FROM dbo.PermissionGroups
  WHERE Code=N'POS_WORKSHIFT'
 );

 DECLARE @WorkShiftPermissionCatalog TABLE
 (
  Code nvarchar(100) NOT NULL PRIMARY KEY,
  Name nvarchar(200) NOT NULL,
  Action nvarchar(50) NOT NULL UNIQUE,
  Description nvarchar(500) NOT NULL,
  Active bit NOT NULL
 );
 INSERT @WorkShiftPermissionCatalog VALUES
  (N'POS.WorkShift.View',N'Xem phiên POS',N'View',N'Xem phiên chịu trách nhiệm POS/két trong phạm vi cửa hàng được cấp',1),
  (N'POS.WorkShift.Open',N'Mở phiên POS',N'Open',N'Mở phiên chịu trách nhiệm POS/két khi đáp ứng lịch, terminal và phạm vi cửa hàng',1),
  (N'POS.WorkShift.Close',N'Đóng phiên POS',N'Close',N'Kiểm đếm và đóng phiên chịu trách nhiệm POS/két',1),
  (N'POS.WorkShift.OpenOutsideSchedule',N'Mở POS ngoài lịch',N'OpenOutsideSchedule',N'Yêu cầu mở POS ngoài lịch; vẫn bắt buộc lý do và phê duyệt',1),
  (N'POS.WorkShift.ApproveOutsideSchedule',N'Duyệt mở POS ngoài lịch',N'ApproveOutsideSchedule',N'Phê duyệt mở POS ngoài lịch trong StaffScope',1),
  (N'POS.WorkShift.CloseException',N'Đóng phiên POS ngoại lệ',N'CloseException',N'Đóng ngoại lệ và chuyển phiên cũ sang trạng thái cần đối soát',1),
  (N'POS.WorkShift.Reconcile',N'Đối soát lại phiên POS',N'Reconcile',N'Đối soát payment hoặc đơn offline đồng bộ muộn trên phiên gốc',1),
  (N'POS.WorkShift.OverrideTerminal',N'Đăng ký terminal POS',N'OverrideTerminal',N'Phê duyệt đăng ký hoặc kích hoạt terminal POS trong StaffScope',1),
  (N'POS.WorkShift.RejectTerminal',N'Từ chối đăng ký terminal POS',N'RejectTerminal',N'Từ chối yêu cầu đăng ký terminal POS trong StaffScope; bắt buộc lý do và audit',1),
  (N'POS.WorkShift.ApproveLateOpen',N'Duyệt mở ca trễ',N'ApproveLateOpen',N'Duyệt, từ chối hoặc chuyển ngoài lịch cho yêu cầu mở ca trễ trên 30 phút',1),
  (N'POS.Session.Manage',N'Quản lý phiên truy cập POS',N'ManagePosSession',N'Kết thúc hoặc thu hồi POS access session trong đúng StaffScope',1),
  (N'POS.Operator.Switch',N'Đổi người thao tác POS',N'SwitchOperator',N'Chuyển Current Operator trong đúng StaffScope',1),
  (N'POS.Operator.ManageOwnPin',N'Quản lý PIN POS cá nhân',N'ManageOwnPin',N'Thiết lập hoặc thay đổi PIN POS của chính nhân viên tại StaffHub',1),
  (N'POS.Terminal.RequestRegistration',N'Yêu cầu đăng ký terminal POS',N'RequestTerminalRegistration',N'Gửi, gửi lại hoặc hủy yêu cầu đăng ký terminal từ StaffHub',1);

 IF EXISTS
 (
  SELECT 1
  FROM @WorkShiftPermissionCatalog c
  JOIN dbo.Permissions p ON p.Code=c.Code
  WHERE p.PermissionGroupId<>@WorkShiftPermissionGroupId
     OR p.Action<>c.Action
 )
 OR EXISTS
 (
  SELECT 1
  FROM @WorkShiftPermissionCatalog c
  JOIN dbo.Permissions p
    ON p.PermissionGroupId=@WorkShiftPermissionGroupId
   AND p.Action=c.Action
  WHERE p.Code<>c.Code
 )
  THROW 53502,N'RBAC_CAFECHAIN_FINAL_V3: POS permission xung đột Code, Group hoặc Action.',1;

 UPDATE p
 SET Name=c.Name,
     Description=c.Description,
     Active=c.Active
 FROM dbo.Permissions p
 JOIN @WorkShiftPermissionCatalog c ON c.Code=p.Code;

 INSERT dbo.Permissions(PermissionGroupId,Code,Name,Action,Description,Active,CreatedAt)
 SELECT @WorkShiftPermissionGroupId,c.Code,c.Name,c.Action,c.Description,c.Active,SYSUTCDATETIME()
 FROM @WorkShiftPermissionCatalog c
 WHERE NOT EXISTS(SELECT 1 FROM dbo.Permissions p WHERE p.Code=c.Code);

 /* Full main RBAC matrix.
    Column order: CDN, QLV, QLCN, NVBH, KTK, QTHT, KH, CT. */
 CREATE TABLE #PermissionMatrix
 (
  PermissionCode nvarchar(100) NOT NULL PRIMARY KEY,
  CDN bit NOT NULL,
  QLV bit NOT NULL,
  QLCN bit NOT NULL,
  NVBH bit NOT NULL,
  KTK bit NOT NULL,
  QTHT bit NOT NULL,
  KH bit NOT NULL,
  CT bit NOT NULL
 );
 INSERT #PermissionMatrix VALUES
  (N'Drink.View',1,1,1,0,1,0,0,0),
  (N'Drink.Create',1,0,0,0,0,0,0,0),
  (N'Drink.Update',1,0,0,0,0,0,0,0),
  (N'Drink.Delete',0,0,0,0,0,0,0,0),
  (N'Drink.ToggleStatus',1,0,0,0,0,0,0,0),
  (N'Drink.UpdateImage',1,0,0,0,0,0,0,0),
  (N'StoreMenu.View',1,1,1,0,1,0,0,0),
  (N'StoreMenu.Update',1,0,1,0,0,0,0,0),
  (N'Profitability.View',1,1,1,0,1,0,0,0),
  (N'Category.View',1,1,1,0,1,0,0,0),
  (N'Category.Create',1,0,0,0,0,0,0,0),
  (N'Category.Update',1,0,0,0,0,0,0,0),
  (N'Category.Delete',0,0,0,0,0,0,0,0),
  (N'Category.ToggleStatus',1,0,0,0,0,0,0,0),
  (N'Size.View',1,1,1,0,1,0,0,0),
  (N'Size.Create',1,0,0,0,0,0,0,0),
  (N'Size.Update',1,0,0,0,0,0,0,0),
  (N'Size.Delete',0,0,0,0,0,0,0,0),
  (N'Size.ToggleStatus',1,0,0,0,0,0,0,0),
  (N'Size.AssignDrink',1,0,0,0,0,0,0,0),
  (N'Topping.View',1,1,1,0,1,0,0,0),
  (N'Topping.Create',1,0,0,0,0,0,0,0),
  (N'Topping.Update',1,0,0,0,0,0,0,0),
  (N'Topping.Delete',0,0,0,0,0,0,0,0),
  (N'Topping.ToggleStatus',1,0,0,0,0,0,0,0),
  (N'Topping.AssignDrink',1,0,0,0,0,0,0,0),
  (N'App.AdminDashboard',1,1,1,0,1,0,0,0),
  (N'Dashboard.Executive.View',1,0,0,0,0,0,0,0),
  (N'Dashboard.Operations.View',1,1,1,0,0,0,0,0),
  (N'Dashboard.Inventory.View',1,1,1,0,1,0,0,0),
  (N'Dashboard.Procurement.View',1,1,1,0,1,0,0,0),
  (N'Dashboard.Product.View',1,1,1,0,1,0,0,0),
  (N'Dashboard.Workforce.View',1,1,1,0,0,0,0,0),
  (N'App.StaffHub',1,1,1,1,1,0,0,1),
  (N'App.POS',0,0,1,1,0,0,0,1),
  (N'System.Permission.Manage',1,0,0,0,0,1,0,0),
  (N'Ingredient.View',1,1,1,0,1,0,0,0),
  (N'Ingredient.Create',1,0,0,0,1,0,0,0),
  (N'Ingredient.Update',1,0,0,0,1,0,0,0),
  (N'Ingredient.ToggleStatus',1,0,0,0,1,0,0,0),
  (N'UnitConversion.View',1,1,1,0,1,0,0,0),
  (N'UnitConversion.Create',1,0,0,0,1,0,0,0),
  (N'UnitConversion.Update',1,0,0,0,1,0,0,0),
  (N'UnitConversion.ToggleStatus',1,0,0,0,1,0,0,0),
  (N'Inventory.View',1,1,1,0,1,0,0,0),
  (N'Inventory.Adjust',0,0,0,0,0,0,0,0),
  (N'Inventory.Export',0,0,0,0,0,0,0,0),
  (N'InventoryThreshold.View',1,1,1,0,1,0,0,0),
  (N'InventoryThreshold.Update',1,1,1,0,0,0,0,0),
  (N'StockAlert.View',1,1,1,1,1,0,0,1),
  (N'StockAlert.Resolve',1,0,1,0,0,0,0,0),
  (N'StockAlert.Configure',0,0,0,0,0,0,0,0),
  (N'StockAlert.Export',0,0,0,0,0,0,0,0),
  (N'Notification.View',1,1,1,1,1,0,0,1),
  (N'StockAlert.Create',0,0,0,0,0,0,0,0),
  (N'StockAlert.CreateRestockRequest',0,0,1,0,0,0,0,0),
  (N'Restock.View',1,1,1,0,1,0,0,1),
  (N'Restock.Create',1,0,1,0,1,0,0,0),
  (N'Restock.Submit',0,0,1,0,1,0,0,0),
  (N'Restock.Approve',0,0,0,0,1,0,0,0),
  (N'Restock.Reject',0,0,0,0,1,0,0,0),
  (N'Restock.Cancel',1,0,1,0,1,0,0,0),
  (N'ReorderSuggestion.View',1,1,1,0,1,0,0,0),
  (N'Restock.Update',0,0,1,0,1,0,0,0),
  (N'Restock.CloseRemaining',1,0,0,0,1,0,0,0),
  (N'Restock.CreatePurchaseOrder',0,0,0,0,0,0,0,0),
  (N'Restock.CreateTransfer',0,0,0,0,0,0,0,0),
  (N'PurchaseAdvice.View',1,1,1,0,1,0,0,0),
  (N'PurchaseAdvice.Create',0,0,0,0,1,0,0,0),
  (N'PurchaseAdvice.Submit',0,0,0,0,1,0,0,0),
  (N'PurchaseAdvice.Review',0,0,0,0,1,0,0,0),
  (N'PurchaseAdvice.Approve',0,0,0,0,0,0,0,0),
  (N'PurchaseAdvice.Reject',0,0,0,0,1,0,0,0),
  (N'PurchaseAdvice.Consolidate',0,0,0,0,1,0,0,0),
  (N'PurchaseAdvice.SelectSupplier',0,0,0,0,0,0,0,0),
  (N'PurchaseAdvice.CreatePurchaseOrder',0,0,0,0,0,0,0,0),
  (N'PurchaseOrder.View',1,1,1,0,1,0,0,1),
  (N'PurchaseOrder.Create',0,0,0,0,1,0,0,0),
  (N'PurchaseOrder.Update',0,0,0,0,0,0,0,0),
  (N'PurchaseOrder.Send',0,0,0,0,1,0,0,0),
  (N'PurchaseOrder.Receive',0,0,0,0,0,0,0,0),
  (N'PurchaseOrder.Cancel',1,0,0,0,1,0,0,0),
  (N'PurchaseOrder.ViewBatch',1,1,0,0,1,0,0,0),
  (N'PurchaseOrder.CreateBatch',0,0,0,0,1,0,0,0),
  (N'PurchaseOrder.Consolidate',0,0,0,0,0,0,0,0),
  (N'PurchaseOrder.Submit',0,0,0,0,0,0,0,0),
  (N'PurchaseOrder.Approve',1,0,0,0,0,0,0,0),
  (N'PurchaseOrder.RejectApproval',0,0,0,0,0,0,0,0),
  (N'PurchaseOrder.OverrideAllocation',0,0,0,0,0,0,0,0),
  (N'PurchaseOrder.Export',0,0,0,0,1,0,0,0),
  (N'Receipt.View',1,1,1,0,1,0,0,1),
  (N'Receipt.Create',0,0,1,0,0,0,0,1),
  (N'Receipt.Confirm',0,0,1,0,0,0,0,1),
  (N'Receipt.Reject',0,0,0,0,0,0,0,0),
  (N'Receipt.Cancel',0,0,0,0,0,0,0,0),
  (N'Receipt.UpdateDraft',0,0,1,0,0,0,0,1),
  (N'Receipt.RecordSupplierIssue',0,0,0,0,0,0,0,0),
  (N'Receipt.ViewCost',1,1,0,0,1,0,0,0),
  (N'Supplier.View',1,1,1,0,1,0,0,0),
  (N'Supplier.Create',1,0,0,0,1,0,0,0),
  (N'Supplier.Update',1,0,0,0,1,0,0,0),
  (N'Supplier.ToggleStatus',1,0,0,0,1,0,0,0),
  (N'Supplier.ViewQuality',0,0,0,0,0,0,0,0),
  (N'SupplierQuality.View',1,1,1,0,1,0,0,0),
  (N'InventoryDocument.View',1,1,1,0,1,0,0,0),
  (N'InventoryDocument.CreateDraft',0,0,1,0,1,0,0,0),
  (N'InventoryDocument.Submit',0,0,1,0,1,0,0,0),
  (N'InventoryDocument.Confirm',0,0,0,0,1,0,0,0),
  (N'InventoryDocument.ApproveNegative',1,0,0,0,1,0,0,0),
  (N'InventoryDocument.Cancel',1,0,1,0,1,0,0,0),
  (N'InventoryDocument.Export',1,1,1,0,1,0,0,0),
  (N'InventoryTransfer.View',1,1,1,0,1,0,0,1),
  (N'InventoryTransfer.CreateDraft',1,0,0,0,1,0,0,0),
  (N'InventoryTransfer.UpdateDraft',1,0,0,0,1,0,0,0),
  (N'InventoryTransfer.Dispatch',0,0,1,0,1,0,0,1),
  (N'InventoryTransfer.Receive',0,0,1,0,1,0,0,1),
  (N'InventoryTransfer.Cancel',1,0,0,0,1,0,0,0),
  (N'InventoryTransfer.Export',0,0,0,0,0,0,0,0),
  (N'Order.View',1,1,1,0,1,0,0,0),
  (N'Order.UpdateStatus',0,0,1,1,0,0,0,1),
  (N'Order.Cancel',1,0,1,0,0,0,0,1),
  (N'Order.Refund',0,0,0,0,0,0,0,0),
  (N'Order.Export',1,1,1,0,1,0,0,0),
  (N'Staff.View',1,1,1,0,0,1,0,0),
  (N'Staff.Create',1,1,1,0,0,0,0,0),
  (N'Staff.Update',1,1,1,0,0,0,0,0),
  (N'Staff.ToggleStatus',1,1,1,0,0,1,0,0),
  (N'Staff.ResetPassword',1,1,1,0,0,1,0,0),
  (N'Shift.View',1,1,1,0,0,0,0,0),
  (N'Shift.Create',1,1,1,0,0,0,0,0),
  (N'Shift.Update',1,1,1,0,0,0,0,0),
  (N'Shift.Cancel',1,1,1,0,0,0,0,0),
  (N'Store.View',1,1,1,0,1,1,0,0),
  (N'Store.Create',1,0,0,0,0,0,0,0),
  (N'Store.Update',1,1,0,0,0,0,0,0),
  (N'Store.ToggleStatus',1,0,0,0,0,0,0,0),
  (N'Settings.View',1,0,0,0,0,0,0,0),
  (N'Settings.Update',1,0,0,0,0,0,0,0),
  (N'Recipe.View',1,1,1,0,1,0,0,0),
  (N'Recipe.Create',1,0,0,0,1,0,0,0),
  (N'Recipe.Update',1,0,0,0,1,0,0,0),
  (N'PreparedItem.View',1,1,1,0,1,0,0,0),
  (N'PreparedItem.Create',1,0,0,0,1,0,0,0),
  (N'PreparedItem.Update',1,0,0,0,1,0,0,0),
  (N'ProductionOrder.View',1,1,1,0,1,0,0,1),
  (N'ProductionOrder.Create',1,0,1,0,1,0,0,1),
  (N'ProductionOrder.Confirm',1,0,1,0,1,0,0,1),
  (N'ProductionOrder.Plan',0,0,1,0,0,1,0,0),
  (N'ProductionOrder.Release',0,0,1,0,0,1,0,0),
  (N'ProductionOrder.Start',0,0,0,0,0,1,0,1),
  (N'ProductionOrder.RecordActual',0,0,0,0,0,1,0,1),
  (N'ProductionOrder.AcceptOutput',0,0,1,0,0,1,0,0),
  (N'ProductionOrder.ApproveVariance',1,0,0,0,0,1,0,0),
  (N'ProductionOrder.Cancel',0,0,1,0,0,1,0,0),
  (N'Restock.SelectProductionSource',0,0,0,0,1,1,0,0),
  (N'OperationalIce.View',1,1,1,0,1,0,0,1),
  (N'OperationalIce.Manage',0,0,0,0,0,0,0,0),
  (N'OperationalIce.Approve',0,0,0,0,0,0,0,0),
  (N'OperationalIce.Policy',0,0,0,0,0,0,0,0),
  (N'OperationalIce.ConfigurePolicy',1,0,1,0,0,0,0,0),
  (N'OperationalIce.CreateShift',1,0,1,0,0,0,0,0),
  (N'OperationalIce.OpenShift',1,0,1,0,0,0,0,0),
  (N'OperationalIce.LinkWorkShift',1,0,1,0,0,0,0,0),
  (N'OperationalIce.RequestSupplement',1,0,1,0,0,0,0,1),
  (N'OperationalIce.ApproveSupplement',1,0,1,0,0,0,0,0),
  (N'OperationalIce.Handoff',1,0,1,0,0,0,0,1),
  (N'OperationalIce.SubmitClose',1,0,1,0,0,0,0,1),
  (N'OperationalIce.ApproveVariance',1,0,1,0,0,0,0,0),
  (N'OperationalIce.CancelScheduledShift',1,0,1,0,0,0,0,0),
  (N'OperationalIce.ViewReport',1,1,1,0,1,0,0,1),
  (N'StoreMenu.OverridePrice',1,0,0,0,0,0,0,0),
  (N'Profitability.UpdatePrice',1,0,0,0,0,0,0,0),
  (N'Profitability.UpdateToppingPolicy',1,0,0,0,0,0,0,0),
  (N'PreparedItem.ToggleStatus',1,0,0,0,1,0,0,0),
  (N'Recipe.Delete',1,0,0,0,1,0,0,0),
  (N'PurchaseAdvice.Update',1,0,0,0,1,0,0,0),
  (N'PurchaseAdvice.Cancel',1,0,0,0,1,0,0,0),
  (N'PurchaseOrder.CloseRemaining',1,0,0,0,1,0,0,0),
  (N'SupplierQuality.Create',0,0,1,0,1,0,0,1),
  (N'SupplierQuality.Transition',1,0,0,0,1,0,0,0),
  (N'InventoryTransfer.RequestReturn',0,0,1,0,1,0,0,1),
  (N'InventoryTransfer.ConfirmReturn',0,0,1,0,1,0,0,1),
  (N'InventoryTransfer.ResolveDiscrepancy',1,1,0,0,1,0,0,0),
  (N'Order.RefundRequest',1,1,1,0,0,0,0,1),
  (N'Order.RefundConfirm',1,1,1,0,0,0,0,0),
  (N'System.Diagnostics.View',1,0,0,0,0,1,0,0),
  (N'System.Cutover.View',1,0,0,0,1,1,0,0),
  (N'System.Cutover.Manage',1,0,0,0,0,1,0,0),
  (N'System.LegacyConsolidation.View',1,1,0,0,1,1,0,0),
 (N'System.LegacyConsolidation.Manage',1,0,0,0,0,1,0,0);

 INSERT #PermissionMatrix VALUES
  (N'AIImport.View',1,0,0,0,1,0,0,0),
  (N'AIImport.Upload',1,0,0,0,1,0,0,0),
  (N'AIImport.Analyze',1,0,0,0,1,0,0,0),
  (N'AIImport.Confirm',1,0,0,0,1,0,0,0),
  (N'AIImport.Cancel',1,0,0,0,1,0,0,0),
  (N'AIImport.History',1,0,0,0,1,0,0,0);

 INSERT #PermissionMatrix VALUES
  (N'Dashboard.AI.Use',1,1,1,0,1,0,0,0),
  (N'Dashboard.FinancialSummary.View',1,1,1,0,1,0,0,0),
  (N'OperationalAnomaly.View',1,1,1,0,0,0,0,0),
  (N'OperationalAnomaly.Acknowledge',1,1,1,0,0,0,0,0),
  (N'OperationalAnomaly.Resolve',1,1,1,0,0,0,0,0),
  (N'OperationalAnomaly.Feedback',1,1,1,0,0,0,0,0);

 INSERT #PermissionMatrix VALUES
  (N'Dashboard.Widget.NetSalesTrend.View',1,0,0,0,0,0,0,0),
  (N'Dashboard.Widget.StoreRanking.View',1,0,0,0,0,0,0,0),
  (N'Dashboard.Widget.PaymentMethodMix.View',1,0,0,0,0,0,0,0),
  (N'Dashboard.Widget.OrderHeatmap.View',1,0,0,0,0,0,0,0),
  (N'Dashboard.Widget.OperationalAlerts.View',1,0,0,0,0,0,0,0),
  (N'Dashboard.Widget.OrderStatusSummary.View',1,0,0,0,0,0,0,0),
  (N'Dashboard.Widget.WorkShiftCashDiscrepancy.View',1,1,1,0,0,0,0,0),
  (N'Dashboard.Widget.WorkShiftSales.View',1,1,1,0,0,0,0,0),
  (N'Dashboard.Widget.WorkShiftPaymentMix.View',1,1,1,0,0,0,0,0),
  (N'Dashboard.Widget.OfflineReconciliationExceptions.View',1,1,1,0,0,0,0,0),
  (N'Dashboard.Widget.HourlyOrders.View',1,1,1,0,0,0,0,0),
  (N'Dashboard.Widget.WorkShiftTopDiscrepancies.View',1,1,1,0,0,0,0,0),
  (N'Dashboard.Widget.WorkShiftKpis.View',1,1,1,0,0,0,0,0),
  (N'Dashboard.Widget.InventoryShortageRisk.View',1,1,1,0,1,0,0,0),
  (N'Dashboard.Widget.InventoryMovementByType.View',1,1,1,0,1,0,0,0),
  (N'Dashboard.Widget.InventoryThresholdRisk.View',1,1,1,0,1,0,0,0),
  (N'Dashboard.Widget.InventoryReorderSuggestions.View',1,1,1,0,1,0,0,0),
  (N'Dashboard.Widget.InventoryWasteByStoreIngredient.View',1,1,1,0,1,0,0,0),
  (N'Dashboard.Widget.InventoryFifoLayerAge.View',1,1,1,0,1,0,0,0),
  (N'Dashboard.Widget.IngredientConsumptionTrend.View',1,1,1,0,1,0,0,0),
  (N'Dashboard.Widget.PurchaseOrderPipeline.View',1,1,1,0,1,0,0,0),
  (N'Dashboard.Widget.OverduePurchaseOrders.View',1,1,1,0,1,0,0,0),
  (N'Dashboard.Widget.SupplierQuality.View',1,1,1,0,1,0,0,0),
  (N'Dashboard.Widget.PurchasePriceTrend.View',1,1,0,0,1,0,0,0),
  (N'Dashboard.Widget.ProcurementSpendBreakdown.View',1,1,0,0,1,0,0,0),
  (N'Dashboard.Widget.SupplierIssueMix.View',1,1,1,0,1,0,0,0),
  (N'Dashboard.Widget.TopProducts.View',1,1,1,0,1,0,0,0),
  (N'Dashboard.Widget.VolumeMarginMatrix.View',1,1,1,0,1,0,0,0),
  (N'Dashboard.Widget.SizeMargin.View',1,1,1,0,1,0,0,0),
  (N'Dashboard.Widget.TopToppings.View',1,1,1,0,1,0,0,0),
  (N'Dashboard.Widget.BomHealth.View',1,1,1,0,1,0,0,0),
  (N'Dashboard.Widget.HighConsumptionLowEfficiency.View',1,1,1,0,1,0,0,0),
  (N'Dashboard.Widget.CategoryPerformance.View',1,1,1,0,1,0,0,0),
  (N'Dashboard.Widget.ProductPeriodPerformance.View',1,1,1,0,1,0,0,0),
  (N'Dashboard.Widget.LowVolumeProducts.View',1,1,1,0,1,0,0,0),
  (N'Dashboard.Widget.LowMarginProducts.View',1,1,1,0,1,0,0,0),
  (N'Dashboard.Widget.WorkforceShiftStatus.View',1,1,1,0,0,0,0,0),
  (N'Dashboard.Widget.WorkforceHourlyDemand.View',1,1,1,0,0,0,0,0),
  (N'Dashboard.Widget.WorkforceStaffPerformance.View',1,1,1,0,0,0,0,0);

 UPDATE #PermissionMatrix SET CDN=1,QLV=1,QLCN=1,KTK=1
 WHERE PermissionCode=N'PurchaseAdvice.SelectSupplier';

 UPDATE #PermissionMatrix SET QTHT=0
 WHERE PermissionCode NOT LIKE N'System.%';

 /* Make the main catalog status exactly match the target matrix:
    any row with at least one grant bit is active; all-zero rows are inactive. */
 UPDATE p
 SET Active=CONVERT(bit,CASE
   WHEN m.CDN=1 OR m.QLV=1 OR m.QLCN=1 OR m.NVBH=1
     OR m.KTK=1 OR m.QTHT=1 OR m.KH=1 OR m.CT=1
   THEN 1 ELSE 0 END)
 FROM dbo.Permissions p
 JOIN #PermissionMatrix m ON m.PermissionCode=p.Code;

 /* Granular POS matrix; fourteen active permissions are managed. */
 CREATE TABLE #PosPermissionMatrix
 (
  PermissionCode nvarchar(100) NOT NULL PRIMARY KEY,
  CDN bit NOT NULL,
  QLV bit NOT NULL,
  QLCN bit NOT NULL,
  NVBH bit NOT NULL,
  KTK bit NOT NULL,
  QTHT bit NOT NULL,
  KH bit NOT NULL,
  CT bit NOT NULL
 );
 INSERT #PosPermissionMatrix VALUES
  (N'POS.WorkShift.View',1,1,1,1,0,0,0,1),
  (N'POS.WorkShift.Open',0,0,1,1,0,0,0,1),
  (N'POS.WorkShift.Close',0,0,1,1,0,0,0,1),
  (N'POS.WorkShift.OpenOutsideSchedule',0,0,1,1,0,0,0,1),
  (N'POS.WorkShift.ApproveOutsideSchedule',1,1,1,0,0,0,0,1),
  (N'POS.WorkShift.CloseException',1,1,1,0,0,0,0,0),
  (N'POS.WorkShift.Reconcile',1,1,1,0,0,0,0,0),
  (N'POS.WorkShift.OverrideTerminal',1,1,1,0,0,0,0,0),
  (N'POS.WorkShift.RejectTerminal',1,0,1,0,0,0,0,0),
  (N'POS.WorkShift.ApproveLateOpen',1,1,1,0,0,0,0,0),
  (N'POS.Session.Manage',1,1,1,0,0,0,0,0),
  (N'POS.Operator.Switch',0,0,1,1,0,0,0,1),
  (N'POS.Operator.ManageOwnPin',0,0,1,1,0,0,0,1),
  (N'POS.Terminal.RequestRegistration',0,0,1,1,0,0,0,1);

 /* System Admin receives every active main/admin permission. POS operational
    permissions remain explicitly scoped by #PosPermissionMatrix. */
 IF EXISTS
 (
  SELECT 1
  FROM #PermissionMatrix m
  JOIN dbo.Permissions p ON p.Code=m.PermissionCode
  WHERE 1=0 AND ((p.Active=1 AND m.QTHT<>1)
     OR (p.Active=0 AND m.QTHT<>0))
 )
  THROW 53503,N'RBAC_CAFECHAIN_FINAL_V3: SystemAdmin chưa có toàn bộ permission active.',1;

 CREATE TABLE #ManagedPermissionCodes
 (
  PermissionCode nvarchar(100) NOT NULL PRIMARY KEY
 );
 INSERT #ManagedPermissionCodes(PermissionCode)
 SELECT PermissionCode FROM #PermissionMatrix
 UNION
 SELECT Code FROM @WorkShiftPermissionCatalog;

 IF EXISTS
 (
  SELECT 1
  FROM #PermissionMatrix m
  LEFT JOIN dbo.Permissions p ON p.Code=m.PermissionCode
  WHERE p.PermissionId IS NULL
 )
 OR EXISTS
 (
  SELECT 1
  FROM #PosPermissionMatrix m
  LEFT JOIN dbo.Permissions p ON p.Code=m.PermissionCode
  WHERE p.PermissionId IS NULL OR p.Active<>1
 )
  THROW 53344,N'RBAC_CAFECHAIN_FINAL_V3: permission trong ma trận thiếu hoặc POS active sai.',1;

 IF EXISTS
 (
  SELECT 1
  FROM dbo.Permissions p
  JOIN #PermissionMatrix m ON m.PermissionCode=p.Code
  WHERE p.Active=0
    AND (m.CDN=1 OR m.QLV=1 OR m.QLCN=1 OR m.NVBH=1
      OR m.KTK=1 OR m.QTHT=1 OR m.KH=1 OR m.CT=1)
 )
  THROW 53345,N'RBAC_CAFECHAIN_FINAL_V3: permission inactive vẫn có bit cấp trong ma trận.',1;

 CREATE TABLE #ExpectedRolePermissions
 (
  RoleId int NOT NULL,
  PermissionId int NOT NULL,
  RoleName nvarchar(100) NOT NULL,
  PermissionCode nvarchar(100) NOT NULL,
  PRIMARY KEY(RoleId,PermissionId)
 );

 INSERT #ExpectedRolePermissions(RoleId,PermissionId,RoleName,PermissionCode)
 SELECT rm.RoleId,p.PermissionId,rm.RoleName,m.PermissionCode
 FROM
 (
  SELECT * FROM #PermissionMatrix
  UNION ALL
  SELECT * FROM #PosPermissionMatrix
 ) m
 CROSS APPLY
 (
  VALUES
   (N'CDN',m.CDN),
   (N'QLV',m.QLV),
   (N'QLCN',m.QLCN),
   (N'NVBH',m.NVBH),
   (N'KTK',m.KTK),
   (N'QTHT',m.QTHT),
   (N'KH',m.KH),
   (N'CT',m.CT)
 ) grantMatrix(RoleKey,IsGranted)
 JOIN #RoleMap rm ON rm.RoleKey=grantMatrix.RoleKey
 JOIN dbo.Permissions p ON p.Code=m.PermissionCode
 WHERE grantMatrix.IsGranted=1
   AND p.Active=1;

 /* Remove every inactive grant and every managed grant not in target. */
 DELETE rp
 FROM dbo.RolePermissions rp
 JOIN #RoleMap rm ON rm.RoleId=rp.RoleId
 JOIN dbo.Permissions p ON p.PermissionId=rp.PermissionId
 LEFT JOIN #ManagedPermissionCodes mc ON mc.PermissionCode=p.Code
 WHERE p.Active=0
    OR
    (
     mc.PermissionCode IS NOT NULL
     AND NOT EXISTS
     (
      SELECT 1
      FROM #ExpectedRolePermissions e
      WHERE e.RoleId=rp.RoleId
        AND e.PermissionId=rp.PermissionId
     )
    );

 INSERT dbo.RolePermissions(RoleId,PermissionId)
 SELECT e.RoleId,e.PermissionId
 FROM #ExpectedRolePermissions e
 WHERE NOT EXISTS
 (
  SELECT 1
  FROM dbo.RolePermissions rp
  WHERE rp.RoleId=e.RoleId
    AND rp.PermissionId=e.PermissionId
 );

 IF EXISTS
 (
  SELECT RoleId,PermissionId
  FROM #ExpectedRolePermissions
  EXCEPT
  SELECT rp.RoleId,rp.PermissionId
  FROM dbo.RolePermissions rp
  JOIN #RoleMap rm ON rm.RoleId=rp.RoleId
  JOIN dbo.Permissions p ON p.PermissionId=rp.PermissionId
  JOIN #ManagedPermissionCodes mc ON mc.PermissionCode=p.Code
 )
 OR EXISTS
 (
  SELECT rp.RoleId,rp.PermissionId
  FROM dbo.RolePermissions rp
  JOIN #RoleMap rm ON rm.RoleId=rp.RoleId
  JOIN dbo.Permissions p ON p.PermissionId=rp.PermissionId
  JOIN #ManagedPermissionCodes mc ON mc.PermissionCode=p.Code
  EXCEPT
  SELECT RoleId,PermissionId
  FROM #ExpectedRolePermissions
 )
  THROW 53346,N'RBAC_CAFECHAIN_FINAL_V3: RolePermission khác ma trận expected.',1;

 IF EXISTS
 (
  SELECT 1
  FROM dbo.RolePermissions rp
  JOIN #RoleMap rm ON rm.RoleId=rp.RoleId
  JOIN dbo.Permissions p ON p.PermissionId=rp.PermissionId
  WHERE p.Active=0
 )
  THROW 53347,N'RBAC_CAFECHAIN_FINAL_V3: role vẫn còn grant permission inactive.',1;

 IF EXISTS
 (
  SELECT AccountPermissionOverrideId,AccountId,PermissionId,Effect,Reason
  FROM #OverrideBefore
  EXCEPT
  SELECT AccountPermissionOverrideId,AccountId,PermissionId,Effect,Reason
  FROM dbo.AccountPermissionOverrides
 )
 OR EXISTS
 (
  SELECT AccountPermissionOverrideId,AccountId,PermissionId,Effect,Reason
  FROM dbo.AccountPermissionOverrides
  EXCEPT
  SELECT AccountPermissionOverrideId,AccountId,PermissionId,Effect,Reason
  FROM #OverrideBefore
 )
  THROW 53348,N'RBAC_CAFECHAIN_FINAL_V3: AccountPermissionOverride đã bị thay đổi.',1;

 CREATE TABLE #ExpectedRoleCounts
 (
  RoleKey nvarchar(10) NOT NULL PRIMARY KEY,
  ExpectedCount int NOT NULL
 );
 INSERT #ExpectedRoleCounts VALUES
  (N'CDN',187),
  (N'QLV',101),
  (N'QLCN',138),
  (N'NVBH',12),
  (N'KTK',124),
  (N'CT',37);

 INSERT #ExpectedRoleCounts(RoleKey,ExpectedCount)
 SELECT N'QTHT',COUNT(*)
 FROM #ExpectedRolePermissions e
 JOIN #RoleMap rm ON rm.RoleId=e.RoleId
 WHERE rm.RoleKey=N'QTHT';

 DECLARE @RoleCountMismatches TABLE
 (
  RoleKey nvarchar(10) NOT NULL PRIMARY KEY,
  RoleName nvarchar(100) NOT NULL,
  ExpectedCount int NOT NULL,
  ActualCount int NOT NULL
 );

 INSERT @RoleCountMismatches(RoleKey,RoleName,ExpectedCount,ActualCount)
 SELECT c.RoleKey,rm.RoleName,c.ExpectedCount,x.ActualCount
 FROM #ExpectedRoleCounts c
 JOIN #RoleMap rm ON rm.RoleKey=c.RoleKey
 CROSS APPLY
 (
  SELECT COUNT(*) AS ActualCount
  FROM #ExpectedRolePermissions e
  WHERE e.RoleId=rm.RoleId
 ) x
 WHERE x.ActualCount<>c.ExpectedCount;

 IF EXISTS(SELECT 1 FROM @RoleCountMismatches)
 BEGIN
  SELECT RoleKey,RoleName,ExpectedCount,ActualCount
  FROM @RoleCountMismatches
  ORDER BY RoleKey;

  THROW 53349,N'RBAC_CAFECHAIN_FINAL_V3: số quyền expected theo role không đúng contract.',1;
 END;

 COMMIT TRANSACTION;

 SELECT N'RBAC_CAFECHAIN_FINAL_V3' AS RbacVersion,
        rm.RoleName,
        c.ExpectedCount,
        COUNT(rp.PermissionId) AS ActualManagedPermissionCount
 FROM #RoleMap rm
 JOIN #ExpectedRoleCounts c ON c.RoleKey=rm.RoleKey
 LEFT JOIN dbo.RolePermissions rp
   ON rp.RoleId=rm.RoleId
  AND EXISTS
  (
   SELECT 1
   FROM dbo.Permissions p
   JOIN #ManagedPermissionCodes mc ON mc.PermissionCode=p.Code
   WHERE p.PermissionId=rp.PermissionId
  )
 GROUP BY rm.RoleName,c.ExpectedCount
 ORDER BY rm.RoleName;

 SELECT p.PermissionId,p.Code,p.Active
 FROM dbo.Permissions p
 WHERE p.Code LIKE N'Dashboard.%'
    OR p.Code LIKE N'POS.%'
 ORDER BY p.Code;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
GO

/* BATCH 12 READ-ONLY VERIFICATION */
SELECT N'PermissionGroups' Entity,COUNT(*) TotalRows,MIN(PermissionGroupId) MinId,
MAX(PermissionGroupId) MaxId FROM dbo.PermissionGroups
UNION ALL SELECT N'Permissions',COUNT(*),MIN(PermissionId),MAX(PermissionId) FROM dbo.Permissions
UNION ALL SELECT N'RolePermissions',COUNT(*),MIN(PermissionId),MAX(PermissionId) FROM dbo.RolePermissions;

SELECT N'Duplicate PermissionGroup Code/Name' Issue,COUNT(*) IssueCount FROM(
 SELECT Code FROM dbo.PermissionGroups GROUP BY Code HAVING COUNT(*)>1
 UNION ALL SELECT Name FROM dbo.PermissionGroups GROUP BY Name HAVING COUNT(*)>1)x
UNION ALL SELECT N'Duplicate Permission Code',COUNT(*) FROM(
 SELECT Code FROM dbo.Permissions GROUP BY Code HAVING COUNT(*)>1)x
UNION ALL SELECT N'Duplicate Permission Group/Action',COUNT(*) FROM(
 SELECT PermissionGroupId,Action FROM dbo.Permissions GROUP BY PermissionGroupId,Action HAVING COUNT(*)>1)x
UNION ALL SELECT N'Orphan Permission',COUNT(*) FROM dbo.Permissions p LEFT JOIN dbo.PermissionGroups g
 ON g.PermissionGroupId=p.PermissionGroupId WHERE g.PermissionGroupId IS NULL
UNION ALL SELECT N'Orphan RolePermission',COUNT(*) FROM dbo.RolePermissions rp
 LEFT JOIN dbo.Roles r ON r.RoleId=rp.RoleId LEFT JOIN dbo.Permissions p ON p.PermissionId=rp.PermissionId
 WHERE r.RoleId IS NULL OR p.PermissionId IS NULL;

/* ============================================================
   FINAL CONSOLIDATED READ-ONLY ACCEPTANCE REPORT
   Every IssueCount must be zero. These queries do not mutate data.
   ============================================================ */
SELECT Entity,TotalRows FROM(VALUES
 (N'DrinkCategories',(SELECT COUNT(*) FROM dbo.DrinkCategories)),
 (N'Drinks',(SELECT COUNT(*) FROM dbo.Drinks)),
 (N'Toppings',(SELECT COUNT(*) FROM dbo.Toppings)),
 (N'Ingredients',(SELECT COUNT(*) FROM dbo.Ingredients)),
 (N'Recipes',(SELECT COUNT(*) FROM dbo.Recipes)),
 (N'Suppliers',(SELECT COUNT(*) FROM dbo.Suppliers)),
 (N'IngredientSuppliers',(SELECT COUNT(*) FROM dbo.IngredientSuppliers)),
 (N'InventoryDocuments',(SELECT COUNT(*) FROM dbo.InventoryDocuments)),
 (N'InventoryTransactions',(SELECT COUNT(*) FROM dbo.InventoryTransactions)),
 (N'InventoryCostLayers',(SELECT COUNT(*) FROM dbo.InventoryCostLayers)),
 (N'Permissions',(SELECT COUNT(*) FROM dbo.Permissions)),
 (N'RolePermissions',(SELECT COUNT(*) FROM dbo.RolePermissions))
)x(Entity,TotalRows);

SELECT N'Duplicate Drink Code' Issue,COUNT(*) IssueCount FROM(
 SELECT DrinkCode FROM dbo.Drinks GROUP BY DrinkCode HAVING COUNT(*)>1)x
UNION ALL SELECT N'Duplicate Topping Code',COUNT(*) FROM(
 SELECT ToppingCode FROM dbo.Toppings GROUP BY ToppingCode HAVING COUNT(*)>1)x
UNION ALL SELECT N'Duplicate Ingredient Code',COUNT(*) FROM(
 SELECT Code FROM dbo.Ingredients GROUP BY Code HAVING COUNT(*)>1)x
UNION ALL SELECT N'Duplicate Supplier Code',COUNT(*) FROM(
 SELECT Code FROM dbo.Suppliers GROUP BY Code HAVING COUNT(*)>1)x
UNION ALL SELECT N'Multiple Current Supplier Price',COUNT(*) FROM(
 SELECT IngredientSupplierId FROM dbo.IngredientSupplierPriceHistories WHERE IsCurrent=1
 GROUP BY IngredientSupplierId HAVING COUNT(*)>1)x
UNION ALL SELECT N'Orphan Recipe Detail',COUNT(*) FROM dbo.RecipeDetails rd
 LEFT JOIN dbo.Recipes r ON r.RecipeId=rd.RecipeId
 LEFT JOIN dbo.Ingredients i ON i.IngredientId=rd.IngredientId
 LEFT JOIN dbo.Recipes cr ON cr.RecipeId=rd.ChildRecipeId
 WHERE r.RecipeId IS NULL OR (rd.IngredientId IS NOT NULL AND i.IngredientId IS NULL)
 OR (rd.ChildRecipeId IS NOT NULL AND cr.RecipeId IS NULL)
UNION ALL SELECT N'Invalid Recipe Detail XOR',COUNT(*) FROM dbo.RecipeDetails
 WHERE (IngredientId IS NULL AND ChildRecipeId IS NULL)
 OR (IngredientId IS NOT NULL AND ChildRecipeId IS NOT NULL) OR Quantity<=0
UNION ALL SELECT N'Missing Exact Active BOM',COUNT(*) FROM dbo.StoreMenuItems sm
 JOIN dbo.DrinkSizes ds ON ds.DrinkSizeId=sm.DrinkSizeId
 LEFT JOIN dbo.Recipes r ON r.DrinkId=ds.DrinkId AND r.SizeId=ds.SizeId
 AND r.ToppingId IS NULL AND r.Active=1 AND r.Status=N'Active'
 WHERE sm.StoreId=1 AND sm.IsEnabled=1 AND r.RecipeId IS NULL
UNION ALL SELECT N'Inventory Transaction Mismatch',COUNT(*) FROM dbo.StoreInventories si
 OUTER APPLY(SELECT SUM(CASE WHEN t.[Type] IN(1,5,8,11,13,14,15)
 THEN t.Quantity ELSE -t.Quantity END) TransactionQty FROM dbo.InventoryTransactions t
 WHERE t.StoreInventoryId=si.StoreInventoryId)q
 WHERE si.StoreId=1 AND si.AvailableQty<>ISNULL(q.TransactionQty,0)
UNION ALL SELECT N'Cost Layer Remaining Out Of Range',COUNT(*) FROM dbo.InventoryCostLayers
 WHERE RemainingQuantity<0 OR RemainingQuantity>Quantity
UNION ALL SELECT N'Orphan Permission Grant',COUNT(*) FROM dbo.RolePermissions rp
 LEFT JOIN dbo.Roles r ON r.RoleId=rp.RoleId LEFT JOIN dbo.Permissions p ON p.PermissionId=rp.PermissionId
 WHERE r.RoleId IS NULL OR p.PermissionId IS NULL;

/* ============================================================
   BATCH 13/14 - DASHBOARD ANALYTICS V1.3 TEST DATA
   Canonical marker: DEMO_DASHBOARD_V13
   Fixed range for testing: 2026-01-15 through 2026-01-18.
   This batch deliberately does not create CashSessions or attendance/payroll data.
   ============================================================ */
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRANSACTION;

 DECLARE @DashboardStoreId int=1;
 DECLARE @DashboardSalesStaffId int=(
   SELECT TOP(1) StaffId FROM dbo.Staffs
   WHERE StoreId=@DashboardStoreId AND Active=1 AND FullName=N'Nhân viên bán hàng'
   ORDER BY StaffId);
 DECLARE @DashboardActorStaffId int=(
   SELECT TOP(1) StaffId FROM dbo.Staffs
   WHERE StoreId=@DashboardStoreId AND Active=1
   ORDER BY StaffId);
 DECLARE @ScheduledStatusId int=(SELECT StaffShiftStatusId FROM dbo.StaffShiftStatuses WHERE Code=N'SCHEDULED');
 DECLARE @CancelledStatusId int=(SELECT StaffShiftStatusId FROM dbo.StaffShiftStatuses WHERE Code=N'CANCELLED');

 IF @DashboardSalesStaffId IS NULL OR @DashboardActorStaffId IS NULL
   THROW 53100,N'DEMO_DASHBOARD_V13 requires active Store 1 staff.',1;
 IF @ScheduledStatusId IS NULL OR @CancelledStatusId IS NULL
   THROW 53101,N'InitialCreate phải có trạng thái SCHEDULED/CANCELLED trước khi chạy Batch 13.',1;

 IF NOT EXISTS(SELECT 1 FROM dbo.Shifts WHERE StoreId=@DashboardStoreId AND Notes=N'DEMO_DASHBOARD_V13_OVERNIGHT')
 BEGIN
   INSERT dbo.Shifts(Name,StartTime,EndTime,IsOvernight,Duration,Active,StoreId,Notes)
   VALUES(N'Ca đêm dashboard','22:00','06:00',1,'08:00',1,@DashboardStoreId,N'DEMO_DASHBOARD_V13_OVERNIGHT');
 END;

 DECLARE @MorningShiftId int=(SELECT TOP(1) ShiftId FROM dbo.Shifts WHERE StoreId=@DashboardStoreId AND StartTime='06:00' ORDER BY ShiftId);
 DECLARE @AfternoonShiftId int=(SELECT TOP(1) ShiftId FROM dbo.Shifts WHERE StoreId=@DashboardStoreId AND StartTime='12:00' ORDER BY ShiftId);
 DECLARE @OvernightShiftId int=(SELECT ShiftId FROM dbo.Shifts WHERE StoreId=@DashboardStoreId AND Notes=N'DEMO_DASHBOARD_V13_OVERNIGHT');

 IF @MorningShiftId IS NULL OR @AfternoonShiftId IS NULL OR @OvernightShiftId IS NULL
   THROW 53102,N'DEMO_DASHBOARD_V13 requires morning, afternoon and overnight shifts.',1;

 IF NOT EXISTS(SELECT 1 FROM dbo.StaffShifts WHERE StaffId=@DashboardSalesStaffId AND ShiftId=@MorningShiftId AND WorkDate='2026-01-15')
   INSERT dbo.StaffShifts(StaffId,ShiftId,CustomStartTime,CustomEndTime,WorkDate,StatusId)
   VALUES(@DashboardSalesStaffId,@MorningShiftId,NULL,NULL,'2026-01-15',@ScheduledStatusId);
 IF NOT EXISTS(SELECT 1 FROM dbo.StaffShifts WHERE StaffId=@DashboardSalesStaffId AND ShiftId=@AfternoonShiftId AND WorkDate='2026-01-15')
   INSERT dbo.StaffShifts(StaffId,ShiftId,CustomStartTime,CustomEndTime,WorkDate,StatusId)
   VALUES(@DashboardSalesStaffId,@AfternoonShiftId,'13:00','17:30','2026-01-15',@ScheduledStatusId);
 IF NOT EXISTS(SELECT 1 FROM dbo.StaffShifts WHERE StaffId=@DashboardSalesStaffId AND ShiftId=@OvernightShiftId AND WorkDate='2026-01-16')
   INSERT dbo.StaffShifts(StaffId,ShiftId,CustomStartTime,CustomEndTime,WorkDate,StatusId)
   VALUES(@DashboardSalesStaffId,@OvernightShiftId,NULL,NULL,'2026-01-16',@ScheduledStatusId);
 IF NOT EXISTS(SELECT 1 FROM dbo.StaffShifts WHERE StaffId=@DashboardSalesStaffId AND ShiftId=@MorningShiftId AND WorkDate='2026-01-17')
   INSERT dbo.StaffShifts(StaffId,ShiftId,CustomStartTime,CustomEndTime,WorkDate,StatusId)
   VALUES(@DashboardSalesStaffId,@MorningShiftId,NULL,NULL,'2026-01-17',@CancelledStatusId);

 DECLARE @WorkShiftSeed TABLE(
   Marker nvarchar(80) PRIMARY KEY,StartAt datetime2,EndAt datetime2 NULL,
   ExpectedCash decimal(18,2),ActualCash decimal(18,2) NULL,IsException bit,RequiresReconciliation bit,LateSync bit);
 INSERT @WorkShiftSeed VALUES
 (N'DEMO_DASHBOARD_V13_20260115_AM','2026-01-15T06:00:00','2026-01-15T12:00:00',650000,650000,0,0,0),
 (N'DEMO_DASHBOARD_V13_20260115_PM','2026-01-15T12:00:00','2026-01-15T18:00:00',750000,720000,0,1,0),
 (N'DEMO_DASHBOARD_V13_20260116_OFFLINE','2026-01-16T06:00:00','2026-01-16T12:00:00',680000,670000,1,1,1),
 (N'DEMO_DASHBOARD_V13_20260118_OPEN','2026-01-18T06:00:00',NULL,500000,NULL,0,0,0);

 INSERT dbo.WorkShifts(
   StoreId,UserId,StartTimeUtc,EndTimeUtc,BusinessDate,OpenContext,CloseType,ClosedByStaffId,CloseReason,ExpiryWarningLevel,
   StartingCash,ExpectedEndingCash,ActualEndingCash,
   CashDiscrepancy,Status,DiscrepancyReason,IsExceptionClosed,ExceptionCloseReason,
   ExceptionClosedByStaffId,ExceptionClosedAt,OfflineOrderCountAtClose,OfflineEstimatedTotalAtClose,
   OfflineCashTotalAtClose,RequiresReconciliation,HasLateOfflineSync,LateOfflineSyncCount,
   LastLateOfflineSyncedAtUtc,PosTerminalId)
 SELECT @DashboardStoreId,@DashboardSalesStaffId,DATEADD(HOUR,-7,x.StartAt),DATEADD(HOUR,-7,x.EndAt),CONVERT(date,x.StartAt),N'LEGACY',
   CASE WHEN x.IsException=1 THEN N'EXCEPTION' WHEN x.EndAt IS NOT NULL THEN N'NORMAL' END,
   CASE WHEN x.IsException=1 THEN @DashboardActorStaffId END,
   CASE WHEN x.IsException=1 THEN N'DEMO_DASHBOARD_V13 offline exception' END,
   0,500000,x.ExpectedCash,x.ActualCash,
   CASE WHEN x.ActualCash IS NULL THEN NULL ELSE x.ActualCash-x.ExpectedCash END,
   CASE WHEN x.EndAt IS NULL THEN N'OPEN' WHEN x.IsException=1 THEN N'RECONCILIATION_REQUIRED' ELSE N'CLOSED' END,
   CASE WHEN x.ActualCash<>x.ExpectedCash THEN N'DEMO_DASHBOARD_V13 cash discrepancy' END,
   x.IsException,CASE WHEN x.IsException=1 THEN N'DEMO_DASHBOARD_V13 offline exception' END,
   CASE WHEN x.IsException=1 THEN @DashboardActorStaffId END,
   CASE WHEN x.IsException=1 THEN x.EndAt END,
   CASE WHEN x.IsException=1 THEN 2 ELSE 0 END,
   CASE WHEN x.IsException=1 THEN 75000 ELSE 0 END,
   CASE WHEN x.IsException=1 THEN 50000 ELSE 0 END,
   x.RequiresReconciliation,x.LateSync,CASE WHEN x.LateSync=1 THEN 1 ELSE 0 END,
   CASE WHEN x.LateSync=1 THEN DATEADD(minute,30,x.EndAt) END,NULL
 FROM @WorkShiftSeed x
 WHERE NOT EXISTS(SELECT 1 FROM dbo.WorkShifts w
   WHERE w.StoreId=@DashboardStoreId AND w.UserId=@DashboardSalesStaffId AND w.StartTimeUtc=DATEADD(HOUR,-7,x.StartAt));

 DECLARE @DashboardOrders TABLE(
   ClientOrderId uniqueidentifier PRIMARY KEY,CreatedAt datetime2,OrderStatusId int,PaymentStatusId int,
   PaymentMethodId int,DrinkCode nvarchar(50),SizeCode nvarchar(20),Quantity int,
   DetailPrice decimal(18,2),Total decimal(18,2),CostStatus int,TotalCogs decimal(18,2) NULL,
   ShiftMarker nvarchar(80));
 INSERT @DashboardOrders VALUES
 ('31000000-0000-0000-0000-000000000001','2026-01-15T07:15:00',5,2,1,N'CF_BacXiu',N'M',1,33000,33000,1,10000,N'DEMO_DASHBOARD_V13_20260115_AM'),
 ('31000000-0000-0000-0000-000000000002','2026-01-15T13:20:00',5,2,2,N'CF_Latte',N'L',1,45000,50000,1,17000,N'DEMO_DASHBOARD_V13_20260115_PM'),
 ('31000000-0000-0000-0000-000000000003','2026-01-16T07:40:00',5,2,3,N'TS_Matcha',N'M',2,37000,74000,0,NULL,N'DEMO_DASHBOARD_V13_20260116_OFFLINE'),
 ('31000000-0000-0000-0000-000000000004','2026-01-16T09:05:00',5,2,2,N'CF_BacXiu',N'M',1,33000,33000,1,10000,N'DEMO_DASHBOARD_V13_20260116_OFFLINE'),
 ('31000000-0000-0000-0000-000000000005','2026-01-15T15:00:00',4,1,1,N'CF_BacXiu',N'M',1,33000,33000,0,NULL,N'DEMO_DASHBOARD_V13_20260115_PM'),
 ('31000000-0000-0000-0000-000000000006','2026-01-15T16:00:00',6,1,1,N'CF_BacXiu',N'M',1,33000,33000,0,NULL,N'DEMO_DASHBOARD_V13_20260115_PM');

 INSERT dbo.Orders(
   CustomerId,StoreId,OrderStatusId,PaymentStatusId,OrderTypeId,TableId,StaffId,WorkShiftId,
   ClientOrderId,Source,Note,ShippingFee,SubTotal,VoucherDiscount,PointDiscount,PointsUsed,
   Total,CostStatus,TotalCogs,GrossProfit,CostedAtUtc,CreatedAt)
 SELECT NULL,@DashboardStoreId,x.OrderStatusId,x.PaymentStatusId,2,NULL,@DashboardSalesStaffId,w.ShiftId,
   x.ClientOrderId,N'DEMO_DASHBOARD_V13',N'DEMO_DASHBOARD_V13 analytics fixture',0,x.Total,0,0,0,
   x.Total,x.CostStatus,x.TotalCogs,CASE WHEN x.TotalCogs IS NULL THEN NULL ELSE x.Total-x.TotalCogs END,
   CASE WHEN x.TotalCogs IS NULL THEN NULL ELSE x.CreatedAt END,x.CreatedAt
 FROM @DashboardOrders x
 JOIN @WorkShiftSeed ws ON ws.Marker=x.ShiftMarker
 JOIN dbo.WorkShifts w ON w.StoreId=@DashboardStoreId AND w.UserId=@DashboardSalesStaffId AND w.StartTimeUtc=DATEADD(HOUR,-7,ws.StartAt)
 WHERE NOT EXISTS(SELECT 1 FROM dbo.Orders o WHERE o.ClientOrderId=x.ClientOrderId);

 INSERT dbo.OrderDetails(
   OrderId,DrinkId,SizeId,StoreMenuItemId,DrinkSizeId,DrinkName,SizeName,Price,
   AcceptedBasePrice,PriceSource,AcceptedCatalogVersion,Quantity,Note,CostStatus,UnitCogs,TotalCogs)
 SELECT o.OrderId,d.DrinkId,s.SizeId,sm.StoreMenuItemId,ds.DrinkSizeId,d.Name,s.Name,x.DetailPrice,
   x.DetailPrice,N'DEMO_DASHBOARD_V13',1,x.Quantity,N'DEMO_DASHBOARD_V13',x.CostStatus,
   CASE WHEN x.TotalCogs IS NULL THEN NULL ELSE x.TotalCogs/x.Quantity END,x.TotalCogs
 FROM @DashboardOrders x
 JOIN dbo.Orders o ON o.ClientOrderId=x.ClientOrderId
 JOIN dbo.Drinks d ON d.DrinkCode=x.DrinkCode
 JOIN dbo.Sizes s ON s.SizeCode=x.SizeCode
 JOIN dbo.DrinkSizes ds ON ds.DrinkId=d.DrinkId AND ds.SizeId=s.SizeId
 LEFT JOIN dbo.StoreMenuItems sm ON sm.StoreId=@DashboardStoreId AND sm.DrinkSizeId=ds.DrinkSizeId
 WHERE NOT EXISTS(SELECT 1 FROM dbo.OrderDetails od WHERE od.OrderId=o.OrderId);

 INSERT dbo.OrderToppings(OrderDetailId,ToppingId,ToppingName,Price,CostStatus,TotalCogs)
 SELECT od.OrderDetailId,t.ToppingId,t.Name,t.Price,1,2000
 FROM dbo.Orders o
 JOIN dbo.OrderDetails od ON od.OrderId=o.OrderId
 JOIN dbo.Toppings t ON t.ToppingId=1
 WHERE o.ClientOrderId='31000000-0000-0000-0000-000000000002'
   AND NOT EXISTS(SELECT 1 FROM dbo.OrderToppings ot WHERE ot.OrderDetailId=od.OrderDetailId AND ot.ToppingId=t.ToppingId);

 INSERT dbo.Payments(OrderId,Amount,ReceivedAmount,ChangeAmount,PaymentMethodId,PaymentStatusId,CashSessionId,TransactionCode,PaidAt)
 SELECT o.OrderId,x.Total,x.Total,0,x.PaymentMethodId,2,NULL,
   N'DEMO_DASHBOARD_V13_'+RIGHT(CONVERT(nvarchar(36),x.ClientOrderId),12),x.CreatedAt
 FROM @DashboardOrders x JOIN dbo.Orders o ON o.ClientOrderId=x.ClientOrderId
 WHERE x.OrderStatusId=5
   AND NOT EXISTS(SELECT 1 FROM dbo.Payments p WHERE p.TransactionCode=N'DEMO_DASHBOARD_V13_'+RIGHT(CONVERT(nvarchar(36),x.ClientOrderId),12));

 IF NOT EXISTS(SELECT 1 FROM dbo.OrderRefunds WHERE RefundKey='32000000-0000-0000-0000-000000000001')
 BEGIN
   INSERT dbo.OrderRefunds(
     OrderId,StoreId,RefundKey,Status,PaymentMethodId,Reason,RefundAmount,CostStatus,ReversedCogs,
     InventoryReversalStatus,RequestedAtUtc,RequestedByStaffId,ProcessingAtUtc,CompletedAtUtc,CompletedByStaffId)
   SELECT o.OrderId,@DashboardStoreId,'32000000-0000-0000-0000-000000000001',3,2,
     N'DEMO_DASHBOARD_V13 full refund',o.Total,1,o.TotalCogs,2,
     '2026-01-16T09:30:00',@DashboardSalesStaffId,'2026-01-16T09:31:00','2026-01-16T09:32:00',@DashboardActorStaffId
   FROM dbo.Orders o WHERE o.ClientOrderId='31000000-0000-0000-0000-000000000004';
 END;

 DECLARE @CoffeeIngredientId int=14;
 DECLARE @CoffeeOfferId int=10;
 DECLARE @CoffeeSupplierId int=6;
 IF NOT EXISTS(SELECT 1 FROM dbo.IngredientSuppliers WHERE IngredientSupplierId=@CoffeeOfferId AND IngredientId=@CoffeeIngredientId AND SupplierId=@CoffeeSupplierId)
   THROW 53103,N'DEMO_DASHBOARD_V13 requires DEMO_OFFER_VIET_COFFEE.',1;

 IF NOT EXISTS(SELECT 1 FROM dbo.RestockRequests WHERE Note=N'DEMO_DASHBOARD_V13_RESTOCK')
 BEGIN
   INSERT dbo.RestockRequests(
     StockAlertId,StoreId,IngredientId,RecipeId,PreparedItemId,RequestedQuantity,SuggestedQuantity,
     SuggestionAnalysisWindowDays,SuggestionAvailableSnapshot,SuggestionMinLevelSnapshot,
     SuggestionAverageDailyUsageSnapshot,SuggestionLeadTimeDaysSnapshot,SuggestionIncomingQuantitySnapshot,
    ReferenceCode,SuggestionReason,Status,Priority,CreatedByStaffId,CreatedAt,UpdatedAt,Note,
     HandledByStaffId,HandledAt,AcceptedByStaffId,AcceptedAtUtc,ProcessingNote,ClosedRemainingQuantity)
  VALUES(NULL,@DashboardStoreId,@CoffeeIngredientId,NULL,NULL,10,12,30,2,5,1,1,0,
    N'RR-DEMO-DASH-V13-001',
     N'DEMO_DASHBOARD_V13 low stock',N'PARTIALLY_RECEIVED',N'HIGH',@DashboardActorStaffId,
     '2026-01-15T08:00:00','2026-01-16T10:00:00',N'DEMO_DASHBOARD_V13_RESTOCK',
     @DashboardActorStaffId,'2026-01-15T08:10:00',@DashboardActorStaffId,'2026-01-15T08:10:00',
     N'DEMO_DASHBOARD_V13 procurement',0);
 END;
 DECLARE @DashboardRestockId int=(SELECT RestockRequestId FROM dbo.RestockRequests WHERE Note=N'DEMO_DASHBOARD_V13_RESTOCK');

 IF NOT EXISTS(SELECT 1 FROM dbo.PurchaseOrders WHERE Code=N'DEMO-DASH-V13-PO-PARTIAL')
   INSERT dbo.PurchaseOrders(
     Code,StoreId,SupplierId,Status,OrderDate,ExpectedDeliveryAtUtc,CreatedByStaffId,
     ApprovedByStaffId,SentByStaffId,CreatedAtUtc,UpdatedAtUtc,ApprovedAtUtc,SentAtUtc,Note)
   VALUES(N'DEMO-DASH-V13-PO-PARTIAL',@DashboardStoreId,@CoffeeSupplierId,N'PARTIALLY_RECEIVED',
     '2026-01-15T08:30:00','2026-01-16T08:30:00',@DashboardActorStaffId,@DashboardActorStaffId,@DashboardActorStaffId,
     '2026-01-15T08:30:00','2026-01-16T10:00:00','2026-01-15T08:35:00','2026-01-15T08:40:00',N'DEMO_DASHBOARD_V13');
 IF NOT EXISTS(SELECT 1 FROM dbo.PurchaseOrders WHERE Code=N'DEMO-DASH-V13-PO-OVERDUE')
   INSERT dbo.PurchaseOrders(
     Code,StoreId,SupplierId,Status,OrderDate,ExpectedDeliveryAtUtc,CreatedByStaffId,
     ApprovedByStaffId,SentByStaffId,CreatedAtUtc,UpdatedAtUtc,ApprovedAtUtc,SentAtUtc,Note)
   VALUES(N'DEMO-DASH-V13-PO-OVERDUE',@DashboardStoreId,@CoffeeSupplierId,N'MARKED_AS_SENT',
     '2026-01-17T08:30:00','2026-01-18T08:30:00',@DashboardActorStaffId,@DashboardActorStaffId,@DashboardActorStaffId,
     '2026-01-17T08:30:00','2026-01-17T08:40:00','2026-01-17T08:35:00','2026-01-17T08:40:00',N'DEMO_DASHBOARD_V13');

 DECLARE @PartialPoId int=(SELECT PurchaseOrderId FROM dbo.PurchaseOrders WHERE Code=N'DEMO-DASH-V13-PO-PARTIAL');
 DECLARE @OverduePoId int=(SELECT PurchaseOrderId FROM dbo.PurchaseOrders WHERE Code=N'DEMO-DASH-V13-PO-OVERDUE');
 IF NOT EXISTS(SELECT 1 FROM dbo.PurchaseOrderLines WHERE PurchaseOrderId=@PartialPoId AND Note=N'DEMO_DASHBOARD_V13_PO_LINE')
   INSERT dbo.PurchaseOrderLines(
     PurchaseOrderId,RestockRequestId,IngredientId,IngredientSupplierId,PackageUnitIdSnapshot,
     PackageQuantitySnapshot,PackagePriceSnapshot,PackageCount,PurchaseMode,OrderedPackageCount,
     UnitPricePerPackage,OrderedBaseQuantity,ClosedRemainingQuantity,
     PromisedLeadTimeDaysSnapshot,Note)
   SELECT @PartialPoId,@DashboardRestockId,@CoffeeIngredientId,@CoffeeOfferId,UnitId,
     PackageQuantity,CurrentPrice,10,N'Packaged',10,CurrentPrice,10,0,LeadTimeDays,N'DEMO_DASHBOARD_V13_PO_LINE'
   FROM dbo.IngredientSuppliers WHERE IngredientSupplierId=@CoffeeOfferId;
 IF NOT EXISTS(SELECT 1 FROM dbo.PurchaseOrderLines WHERE PurchaseOrderId=@OverduePoId AND Note=N'DEMO_DASHBOARD_V13_OVERDUE_LINE')
   INSERT dbo.PurchaseOrderLines(
     PurchaseOrderId,RestockRequestId,IngredientId,IngredientSupplierId,PackageUnitIdSnapshot,
     PackageQuantitySnapshot,PackagePriceSnapshot,PackageCount,PurchaseMode,OrderedPackageCount,
     UnitPricePerPackage,OrderedBaseQuantity,ClosedRemainingQuantity,
     PromisedLeadTimeDaysSnapshot,Note)
   SELECT @OverduePoId,NULL,@CoffeeIngredientId,@CoffeeOfferId,UnitId,
     PackageQuantity,CurrentPrice,5,N'Packaged',5,CurrentPrice,5,0,LeadTimeDays,N'DEMO_DASHBOARD_V13_OVERDUE_LINE'
   FROM dbo.IngredientSuppliers WHERE IngredientSupplierId=@CoffeeOfferId;

 DECLARE @PartialPoLineId int=(SELECT PurchaseOrderLineId FROM dbo.PurchaseOrderLines WHERE PurchaseOrderId=@PartialPoId AND Note=N'DEMO_DASHBOARD_V13_PO_LINE');
 IF NOT EXISTS(SELECT 1 FROM dbo.BranchReceipts WHERE ReceiptCode=N'DEMO-DASH-V13-BR-001')
   INSERT dbo.BranchReceipts(
     ReceiptCode,StoreId,SupplierId,PurchaseOrderId,Status,ReceiptKey,ReferenceNumber,
     ReceivedAt,ReceivedByStaffId,ConfirmedAt,ConfirmedByStaffId,Notes,CreatedAt,CreatedByStaffId)
   VALUES(N'DEMO-DASH-V13-BR-001',@DashboardStoreId,@CoffeeSupplierId,@PartialPoId,N'CONFIRMED',
     N'DEMO_DASHBOARD_V13_RECEIPT_001',N'DEMO-V13-INVOICE','2026-01-16T09:00:00',@DashboardActorStaffId,
     '2026-01-16T09:10:00',@DashboardActorStaffId,N'DEMO_DASHBOARD_V13 analytical receipt',
     '2026-01-16T09:00:00',@DashboardActorStaffId);
 DECLARE @DashboardReceiptId int=(SELECT BranchReceiptId FROM dbo.BranchReceipts WHERE ReceiptCode=N'DEMO-DASH-V13-BR-001');

 IF NOT EXISTS(SELECT 1 FROM dbo.BranchReceiptLines WHERE BranchReceiptId=@DashboardReceiptId AND PurchaseOrderLineId=@PartialPoLineId)
   INSERT dbo.BranchReceiptLines(
     BranchReceiptId,RestockRequestId,PurchaseOrderLineId,IngredientId,PreparedItemId,RecipeId,
     InputQuantity,InputUnitId,ReceivedBaseQuantity,RejectedBaseQuantity,RejectionReason,RejectionIssueType,
     BaseUnitId,SupplierId,IngredientSupplierId,ActualPackagePrice,PackageQuantitySnapshot,
     PackageUnitIdSnapshot,BaseUnitCostSnapshot,LineTotalCost,CreatedAt)
   SELECT @DashboardReceiptId,@DashboardRestockId,@PartialPoLineId,@CoffeeIngredientId,NULL,NULL,
     10,o.UnitId,8,2,N'Bao bì rách',N'PACKAGING_FAILURE',i.BaseUnitId,@CoffeeSupplierId,@CoffeeOfferId,
     o.CurrentPrice,o.PackageQuantity,o.UnitId,o.CurrentPrice/o.PackageQuantity,
     8*(o.CurrentPrice/o.PackageQuantity),'2026-01-16T09:00:00'
   FROM dbo.IngredientSuppliers o JOIN dbo.Ingredients i ON i.IngredientId=o.IngredientId
   WHERE o.IngredientSupplierId=@CoffeeOfferId;
 DECLARE @DashboardReceiptLineId int=(SELECT BranchReceiptLineId FROM dbo.BranchReceiptLines WHERE BranchReceiptId=@DashboardReceiptId AND PurchaseOrderLineId=@PartialPoLineId);

 IF NOT EXISTS(SELECT 1 FROM dbo.SupplierReceiptIssues WHERE BranchReceiptLineId=@DashboardReceiptLineId AND Description=N'DEMO_DASHBOARD_V13 supplier issue')
   INSERT dbo.SupplierReceiptIssues(
     SupplierId,StoreId,PurchaseOrderId,PurchaseOrderLineId,BranchReceiptId,BranchReceiptLineId,
     IssueType,Status,AffectedBaseQuantity,Description,ReportedByStaffId,ReportedAtUtc,UpdatedAtUtc)
   VALUES(@CoffeeSupplierId,@DashboardStoreId,@PartialPoId,@PartialPoLineId,@DashboardReceiptId,@DashboardReceiptLineId,
     N'PACKAGING_FAILURE',N'OPEN',2,N'DEMO_DASHBOARD_V13 supplier issue',@DashboardActorStaffId,
     '2026-01-16T09:15:00','2026-01-16T09:15:00');

 IF (SELECT COUNT(*) FROM dbo.Orders WHERE Source=N'DEMO_DASHBOARD_V13')<>6
   THROW 53110,N'DEMO_DASHBOARD_V13 order count mismatch.',1;
 IF (SELECT COUNT(*) FROM dbo.WorkShifts
     WHERE StoreId=@DashboardStoreId AND UserId=@DashboardSalesStaffId
       AND StartTimeUtc IN ('2026-01-14T23:00:00','2026-01-15T05:00:00','2026-01-15T23:00:00','2026-01-17T23:00:00'))<>4
   THROW 53111,N'DEMO_DASHBOARD_V13 WorkShift count mismatch.',1;
 IF (SELECT COUNT(*) FROM dbo.StaffShifts ss JOIN dbo.Shifts sh ON sh.ShiftId=ss.ShiftId
     WHERE ss.StaffId=@DashboardSalesStaffId AND ss.WorkDate BETWEEN '2026-01-15' AND '2026-01-17'
       AND (sh.Notes=N'DEMO_DASHBOARD_V13_OVERNIGHT' OR sh.StoreId=@DashboardStoreId))<4
   THROW 53112,N'DEMO_DASHBOARD_V13 StaffShift count mismatch.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SupplierReceiptIssues WHERE Description=N'DEMO_DASHBOARD_V13 supplier issue')
   THROW 53113,N'DEMO_DASHBOARD_V13 procurement fixture missing.',1;

 COMMIT TRANSACTION;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
GO

SELECT N'DEMO_DASHBOARD_V13' AS SeedMarker,
       (SELECT COUNT(*) FROM dbo.Orders WHERE Source=N'DEMO_DASHBOARD_V13') AS DemoOrders,
       (SELECT COUNT(*) FROM dbo.WorkShifts
        WHERE StoreId=1 AND StartTimeUtc IN ('2026-01-14T23:00:00','2026-01-15T05:00:00','2026-01-15T23:00:00','2026-01-17T23:00:00')) AS DemoWorkShifts,
       (SELECT COUNT(*) FROM dbo.PurchaseOrders WHERE Note=N'DEMO_DASHBOARD_V13') AS DemoPurchaseOrders,
       (SELECT COUNT(*) FROM dbo.SupplierReceiptIssues WHERE Description=N'DEMO_DASHBOARD_V13 supplier issue') AS DemoSupplierIssues;
GO

/* ================================================================
   BATCH 14/14 - DEMO_REORDER_V14
   Store 3 foundation + rolling POS/BOM/FIFO/COGS history

   Contract:
   - No model/enum/migration changes.
   - No IDENTITY hard-codes in this batch. Business keys only.
   - One UTC anchor per run; all analytical fixtures stay in rolling 30 days.
   - First run applies stock/FIFO exactly once.
   - Replay only rebases fixture timestamps after validating the fixture contract.
   - Any partial fixture or business-key payload drift aborts the whole batch.
   ================================================================ */
IF EXISTS
(
    SELECT 1
    FROM dbo.SystemSettings
    WHERE SettingKey=N'seedall_inventory_procurement_v2'
      AND SettingValue=N'completed'
)
BEGIN
    PRINT N'SeedAll Batch 14 skipped: inventory procurement v2 already owns opening/buffer evidence.';
END
ELSE
BEGIN
BEGIN TRY
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    DECLARE @SeedMarker nvarchar(50)=N'DEMO_REORDER_V14';
    DECLARE @SeedAnchorUtc datetime2(0)=CONVERT(datetime2(0),SYSUTCDATETIME());
    DECLARE @SeedDayUtc datetime2(0)=DATEADD(DAY,DATEDIFF(DAY,0,@SeedAnchorUtc),0);
    /* Operational fixtures stay strictly inside the rolling window; no current-day fixed clock is used. */
    DECLARE @WindowStartUtc datetime2(0)=DATEADD(DAY,-30,@SeedAnchorUtc);
    DECLARE @Store1Id int,@Store3Id int,@Store1StaffId int,@Store3StaffId int,@Store3AccountId int;
    DECLARE @SalesRoleId int,@StoreScopeTypeId int,@PaidStatusId int,@BankMethodId int,@CompletedOrderStatusId int,@TakeAwayTypeId int;

    IF OBJECT_ID(N'dbo.Accounts',N'U') IS NULL OR OBJECT_ID(N'dbo.AccountRoles',N'U') IS NULL
    OR OBJECT_ID(N'dbo.Staffs',N'U') IS NULL OR OBJECT_ID(N'dbo.StaffScopes',N'U') IS NULL
    OR OBJECT_ID(N'dbo.Stores',N'U') IS NULL OR OBJECT_ID(N'dbo.StoreDrinks',N'U') IS NULL
    OR OBJECT_ID(N'dbo.StoreMenuItems',N'U') IS NULL OR OBJECT_ID(N'dbo.StoreToppings',N'U') IS NULL
    OR OBJECT_ID(N'dbo.SupplierStores',N'U') IS NULL OR OBJECT_ID(N'dbo.StoreInventories',N'U') IS NULL
    OR OBJECT_ID(N'dbo.InventoryDocuments',N'U') IS NULL OR OBJECT_ID(N'dbo.InventoryDocumentDetails',N'U') IS NULL
    OR OBJECT_ID(N'dbo.InventoryTransactions',N'U') IS NULL OR OBJECT_ID(N'dbo.InventoryCostLayers',N'U') IS NULL
    OR OBJECT_ID(N'dbo.ProductionRuns',N'U') IS NULL OR OBJECT_ID(N'dbo.ProductionCostAllocations',N'U') IS NULL
    OR OBJECT_ID(N'dbo.Orders',N'U') IS NULL OR OBJECT_ID(N'dbo.OrderDetails',N'U') IS NULL
    OR OBJECT_ID(N'dbo.OrderToppings',N'U') IS NULL OR OBJECT_ID(N'dbo.Payments',N'U') IS NULL
    OR OBJECT_ID(N'dbo.WorkShifts',N'U') IS NULL OR OBJECT_ID(N'dbo.SalesCostAllocations',N'U') IS NULL
    OR OBJECT_ID(N'dbo.Recipes',N'U') IS NULL OR OBJECT_ID(N'dbo.RecipeDetails',N'U') IS NULL
    OR OBJECT_ID(N'dbo.UnitConversions',N'U') IS NULL OR OBJECT_ID(N'dbo.IngredientSuppliers',N'U') IS NULL
    OR OBJECT_ID(N'dbo.IngredientSupplierPriceHistories',N'U') IS NULL OR OBJECT_ID(N'dbo.Suppliers',N'U') IS NULL
    OR OBJECT_ID(N'dbo.Ingredients',N'U') IS NULL OR OBJECT_ID(N'dbo.PreparedItems',N'U') IS NULL
    OR OBJECT_ID(N'dbo.Drinks',N'U') IS NULL OR OBJECT_ID(N'dbo.DrinkSizes',N'U') IS NULL OR OBJECT_ID(N'dbo.Sizes',N'U') IS NULL
    OR OBJECT_ID(N'dbo.DrinkSizeToppingPolicies',N'U') IS NULL OR OBJECT_ID(N'dbo.Toppings',N'U') IS NULL
    OR OBJECT_ID(N'dbo.Roles',N'U') IS NULL OR OBJECT_ID(N'dbo.ScopeTypes',N'U') IS NULL
    OR OBJECT_ID(N'dbo.PaymentStatuses',N'U') IS NULL OR OBJECT_ID(N'dbo.PaymentMethods',N'U') IS NULL
    OR OBJECT_ID(N'dbo.OrderStatuses',N'U') IS NULL OR OBJECT_ID(N'dbo.OrderTypes',N'U') IS NULL
        THROW 53400,N'DEMO_REORDER_V14: schema thiếu bảng bắt buộc.',1;

    SELECT @Store1Id=StoreId FROM dbo.Stores WHERE Name=N'CafeChain Thủ Dầu Một';
    SELECT @Store3Id=StoreId FROM dbo.Stores WHERE Name=N'CafeChain Dĩ An';
    IF @Store1Id IS NULL OR @Store3Id IS NULL OR @Store1Id=@Store3Id
        THROW 53401,N'DEMO_REORDER_V14: không resolve được Store 1 / Store 3 bằng business key.',1;

    SELECT @SalesRoleId=RoleId FROM dbo.Roles WHERE Name=N'Nhân viên bán hàng' AND Active=1;
    SELECT @StoreScopeTypeId=ScopeTypeId FROM dbo.ScopeTypes WHERE Code=N'STORE';
    SELECT @PaidStatusId=PaymentStatusId FROM dbo.PaymentStatuses WHERE Code=N'PAID';
    SELECT @BankMethodId=PaymentMethodId FROM dbo.PaymentMethods WHERE Code=N'BANK';
    SELECT @CompletedOrderStatusId=OrderStatusId FROM dbo.OrderStatuses WHERE Name=N'Hoàn thành';
    SELECT @TakeAwayTypeId=OrderTypeId FROM dbo.OrderTypes WHERE Name=N'Take Away';
    IF @SalesRoleId IS NULL OR @StoreScopeTypeId IS NULL OR @PaidStatusId IS NULL OR @BankMethodId IS NULL
       OR @CompletedOrderStatusId IS NULL OR @TakeAwayTypeId IS NULL
        THROW 53402,N'DEMO_REORDER_V14: thiếu role/scope/status/payment/order type nền.',1;

    /* Resolve first-run/replay before any inventory mutation. A replay with missing opening evidence is partial and must fail closed. */
    DECLARE @ExistingOrders int=(SELECT COUNT(*) FROM dbo.Orders WHERE Source=@SeedMarker);
    DECLARE @ExistingRuns int=(SELECT COUNT(*) FROM dbo.ProductionRuns WHERE Notes LIKE N'DEMO_REORDER_V14_PROD_S%');
    DECLARE @ExistingShifts int=(SELECT COUNT(*) FROM dbo.WorkShifts WHERE DiscrepancyReason LIKE N'DEMO_REORDER_V14_SHIFT_S%');
    DECLARE @ExistingPayments int=(SELECT COUNT(*) FROM dbo.Payments WHERE TransactionCode LIKE N'DEMO_REORDER_V14_PAY_S%');
    DECLARE @IsReplay bit=0;

    IF @ExistingOrders=0 AND @ExistingRuns=0 AND @ExistingShifts=0 AND @ExistingPayments=0 SET @IsReplay=0;
    ELSE IF @ExistingOrders=100 AND @ExistingRuns=60 AND @ExistingShifts=60 AND @ExistingPayments=100 SET @IsReplay=1;
    ELSE THROW 53428,N'DEMO_REORDER_V14: phát hiện fixture partial; rollback để tránh trừ kho/FIFO lần hai.',1;

    IF @IsReplay=1 AND NOT EXISTS(SELECT 1 FROM dbo.InventoryDocuments WHERE RequestKey=N'DEMO_REORDER_V14_OPENING_STORE3')
        THROW 53495,N'DEMO_REORDER_V14: operational fixture tồn tại nhưng Store3 opening evidence bị thiếu.',1;

    /* ------------------------------------------------------------
       14.1 Store 3 demo sales identity - no hard-coded identity
       ------------------------------------------------------------ */
    DECLARE @Store3DemoEmail nvarchar(256)=N'demo.sales.dian@cafechain.local';
    DECLARE @SourceSalesAccountId int,@SourceSalesStaffId int,@SourcePasswordHash nvarchar(max),
            @SourceGender int,@SourceEmployeeStatus int;

    SELECT TOP(1)
        @SourceSalesAccountId=a.AccountId,@SourceSalesStaffId=s.StaffId,@SourcePasswordHash=a.PasswordHash,
        @SourceGender=s.Gender,@SourceEmployeeStatus=s.EmployeeStatus
    FROM dbo.Accounts a
    JOIN dbo.Staffs s ON s.AccountId=a.AccountId
    JOIN dbo.AccountRoles ar ON ar.AccountId=a.AccountId AND ar.RoleId=@SalesRoleId
    WHERE a.Email=N'salesstaff@cafechain.vn' AND a.Active=1 AND s.Active=1 AND s.StoreId=@Store1Id;

    IF @SourceSalesAccountId IS NULL OR @SourcePasswordHash IS NULL
        THROW 53403,N'DEMO_REORDER_V14: thiếu account bán hàng Store 1 làm nguồn password/profile.',1;

    IF EXISTS(
        SELECT 1 FROM dbo.Accounts a
        WHERE a.Email=@Store3DemoEmail
          AND (a.Active<>1 OR a.RequiresPasswordChange<>0 OR NULLIF(LTRIM(RTRIM(a.PasswordHash)),N'') IS NULL)
    ) THROW 53404,N'DEMO_REORDER_V14: account Store 3 cùng email nhưng payload khác contract.',1;

    IF NOT EXISTS(SELECT 1 FROM dbo.Accounts WHERE Email=@Store3DemoEmail)
        INSERT dbo.Accounts(Email,PasswordHash,Active,RequiresPasswordChange,CreatedAt,FailedLoginAttempts,LockoutEnd)
        VALUES(@Store3DemoEmail,@SourcePasswordHash,1,0,@SeedAnchorUtc,0,NULL);

    SELECT @Store3AccountId=AccountId FROM dbo.Accounts WHERE Email=@Store3DemoEmail;

    IF EXISTS(
        SELECT 1 FROM dbo.AccountRoles ar
        WHERE ar.AccountId=@Store3AccountId AND ar.RoleId<>@SalesRoleId
    ) THROW 53405,N'DEMO_REORDER_V14: demo account Store 3 đang có role ngoài Nhân viên bán hàng.',1;

    IF NOT EXISTS(SELECT 1 FROM dbo.AccountRoles WHERE AccountId=@Store3AccountId AND RoleId=@SalesRoleId)
        INSERT dbo.AccountRoles(AccountId,RoleId) VALUES(@Store3AccountId,@SalesRoleId);

    IF EXISTS(
        SELECT 1 FROM dbo.Staffs s
        WHERE s.AccountId=@Store3AccountId
          AND (s.StoreId<>@Store3Id OR s.Active<>1 OR s.FullName<>N'Nhân viên bán hàng demo Dĩ An')
    ) THROW 53406,N'DEMO_REORDER_V14: Staff demo Store 3 cùng account nhưng payload khác contract.',1;

    IF NOT EXISTS(SELECT 1 FROM dbo.Staffs WHERE AccountId=@Store3AccountId)
        INSERT dbo.Staffs(AccountId,FullName,CCCD,Gender,StartDate,EmployeeStatus,DateOfBirth,StoreId,
                          AvatarUrl,AvatarPublicId,Active,CreatedAt)
        SELECT @Store3AccountId,N'Nhân viên bán hàng demo Dĩ An',NULL,@SourceGender,@SeedAnchorUtc,
               @SourceEmployeeStatus,NULL,@Store3Id,NULL,NULL,1,@SeedAnchorUtc;

    SELECT @Store3StaffId=StaffId FROM dbo.Staffs WHERE AccountId=@Store3AccountId;

    IF EXISTS(
        SELECT 1 FROM dbo.StaffScopes ss
        WHERE ss.StaffId=@Store3StaffId
          AND (ss.ScopeTypeId<>@StoreScopeTypeId OR ss.ScopeRefId<>@Store3Id)
    ) THROW 53407,N'DEMO_REORDER_V14: StaffScope demo Store 3 khác STORE/Store3 contract.',1;

    IF NOT EXISTS(SELECT 1 FROM dbo.StaffScopes WHERE StaffId=@Store3StaffId AND ScopeTypeId=@StoreScopeTypeId AND ScopeRefId=@Store3Id)
        INSERT dbo.StaffScopes(StaffId,ScopeTypeId,ScopeRefId) VALUES(@Store3StaffId,@StoreScopeTypeId,@Store3Id);

    SELECT @Store1StaffId=s.StaffId
    FROM dbo.Accounts a JOIN dbo.Staffs s ON s.AccountId=a.AccountId
    WHERE a.Email=N'salesstaff@cafechain.vn' AND a.Active=1 AND s.Active=1 AND s.StoreId=@Store1Id;
    IF @Store1StaffId IS NULL THROW 53408,N'DEMO_REORDER_V14: thiếu Staff bán hàng Store 1.',1;

    /* ------------------------------------------------------------
       14.2 Make DEMO_ING_SUGAR_SYRUP part of a real fruit-tea BOM.
       Business keys only; base unit of the ingredient is used.
       ------------------------------------------------------------ */
    DECLARE @BottleSyrupIngredientId int=(SELECT IngredientId FROM dbo.Ingredients WHERE Code=N'DEMO_ING_SUGAR_SYRUP' AND Active=1);
    DECLARE @BottleSyrupBaseUnitId int=(SELECT BaseUnitId FROM dbo.Ingredients WHERE IngredientId=@BottleSyrupIngredientId);
    DECLARE @PeachTeaMRecipeId int=(SELECT RecipeId FROM dbo.Recipes WHERE RecipeCode=N'DEMO_RECIPE_SKU_PEACH_ORANGE_TEA_M' AND Active=1 AND Status=N'Active');
    DECLARE @PeachTeaLRecipeId int=(SELECT RecipeId FROM dbo.Recipes WHERE RecipeCode=N'DEMO_RECIPE_SKU_PEACH_ORANGE_TEA_L' AND Active=1 AND Status=N'Active');
    IF @BottleSyrupIngredientId IS NULL OR @BottleSyrupBaseUnitId IS NULL OR @PeachTeaMRecipeId IS NULL OR @PeachTeaLRecipeId IS NULL
        THROW 53409,N'DEMO_REORDER_V14: không resolve được syrup đóng chai hoặc BOM trà đào cam sả.',1;

    IF EXISTS(
        SELECT 1 FROM dbo.RecipeDetails rd
        WHERE rd.RecipeId=@PeachTeaMRecipeId AND rd.IngredientId=@BottleSyrupIngredientId
          AND (rd.ChildRecipeId IS NOT NULL OR rd.UnitId<>@BottleSyrupBaseUnitId OR rd.Quantity<>CAST(5 AS decimal(18,3)))
    ) OR EXISTS(
        SELECT 1 FROM dbo.RecipeDetails rd
        WHERE rd.RecipeId=@PeachTeaLRecipeId AND rd.IngredientId=@BottleSyrupIngredientId
          AND (rd.ChildRecipeId IS NOT NULL OR rd.UnitId<>@BottleSyrupBaseUnitId OR rd.Quantity<>CAST(7 AS decimal(18,3)))
    ) THROW 53410,N'DEMO_REORDER_V14: syrup đã có trong fruit-tea BOM nhưng payload khác contract.',1;

    IF NOT EXISTS(SELECT 1 FROM dbo.RecipeDetails WHERE RecipeId=@PeachTeaMRecipeId AND IngredientId=@BottleSyrupIngredientId)
        INSERT dbo.RecipeDetails(RecipeId,IngredientId,ChildRecipeId,Quantity,UnitId)
        VALUES(@PeachTeaMRecipeId,@BottleSyrupIngredientId,NULL,5,@BottleSyrupBaseUnitId);
    IF NOT EXISTS(SELECT 1 FROM dbo.RecipeDetails WHERE RecipeId=@PeachTeaLRecipeId AND IngredientId=@BottleSyrupIngredientId)
        INSERT dbo.RecipeDetails(RecipeId,IngredientId,ChildRecipeId,Quantity,UnitId)
        VALUES(@PeachTeaLRecipeId,@BottleSyrupIngredientId,NULL,7,@BottleSyrupBaseUnitId);
    /* ------------------------------------------------------------
   14.2B Complete real BOM coverage for legacy ingredients.

   - ING00008:
       Vanilla syrup -> Caramel Macchiato M/L.
   - DEMO_ING_WHITE_PEARL:
       Ready white pearl -> Trà sữa truyền thống đặc biệt M/L.

   Không tạo InventoryTransaction giả.
   Consumption sau đó vẫn phải đi:
   OrderDetail -> RecipeDetail -> SALES_DEDUCTION -> FIFO.
   ------------------------------------------------------------ */

    DECLARE @VanillaIngredientId int =
    (
        SELECT IngredientId
        FROM dbo.Ingredients
        WHERE Code = N'ING00008'
          AND Active = 1
    );

    DECLARE @VanillaBaseUnitId int =
    (
        SELECT BaseUnitId
        FROM dbo.Ingredients
        WHERE IngredientId = @VanillaIngredientId
    );

    DECLARE @CaramelMacchiatoMRecipeId int =
    (
        SELECT RecipeId
        FROM dbo.Recipes
        WHERE RecipeCode = N'DEMO_RECIPE_SKU_CARAMEL_MACCHIATO_M'
          AND Active = 1
          AND Status = N'Active'
    );

    DECLARE @CaramelMacchiatoLRecipeId int =
    (
        SELECT RecipeId
        FROM dbo.Recipes
        WHERE RecipeCode = N'DEMO_RECIPE_SKU_CARAMEL_MACCHIATO_L'
          AND Active = 1
          AND Status = N'Active'
    );


    DECLARE @WhitePearlIngredientId int =
    (
        SELECT IngredientId
        FROM dbo.Ingredients
        WHERE Code = N'DEMO_ING_WHITE_PEARL'
          AND Active = 1
    );

    DECLARE @WhitePearlBaseUnitId int =
    (
        SELECT BaseUnitId
        FROM dbo.Ingredients
        WHERE IngredientId = @WhitePearlIngredientId
    );

    DECLARE @TradMilkTeaMRecipeId int =
    (
        SELECT RecipeId
        FROM dbo.Recipes
        WHERE RecipeCode = N'DEMO_RECIPE_SKU_TRAD_MILK_TEA_M'
          AND Active = 1
          AND Status = N'Active'
    );

    DECLARE @TradMilkTeaLRecipeId int =
    (
        SELECT RecipeId
        FROM dbo.Recipes
        WHERE RecipeCode = N'DEMO_RECIPE_SKU_TRAD_MILK_TEA_L'
          AND Active = 1
          AND Status = N'Active'
    );


    /* ============================================================
       Validate business keys
       ============================================================ */
    IF @VanillaIngredientId IS NULL
       OR @VanillaBaseUnitId IS NULL
       OR @CaramelMacchiatoMRecipeId IS NULL
       OR @CaramelMacchiatoLRecipeId IS NULL
    BEGIN
        ;THROW 53505,
               N'DEMO_REORDER_V14: không resolve được ING00008 hoặc Caramel Macchiato M/L.',
               1;
    END;


    IF @WhitePearlIngredientId IS NULL
       OR @WhitePearlBaseUnitId IS NULL
       OR @TradMilkTeaMRecipeId IS NULL
       OR @TradMilkTeaLRecipeId IS NULL
    BEGIN
        ;THROW 53506,
               N'DEMO_REORDER_V14: không resolve được white pearl hoặc Trà sữa truyền thống M/L.',
               1;
    END;


    /* ============================================================
       Contract drift check - Vanilla

       Demo BOM:
       M = 10 ml
       L = 15 ml
       ============================================================ */
    IF EXISTS
    (
        SELECT 1
        FROM dbo.RecipeDetails rd
        WHERE rd.RecipeId = @CaramelMacchiatoMRecipeId
          AND rd.IngredientId = @VanillaIngredientId
          AND
          (
              rd.ChildRecipeId IS NOT NULL
              OR rd.UnitId <> @VanillaBaseUnitId
              OR rd.Quantity <> CAST(10 AS decimal(18,3))
          )
    )
    OR EXISTS
    (
        SELECT 1
        FROM dbo.RecipeDetails rd
        WHERE rd.RecipeId = @CaramelMacchiatoLRecipeId
          AND rd.IngredientId = @VanillaIngredientId
          AND
          (
              rd.ChildRecipeId IS NOT NULL
              OR rd.UnitId <> @VanillaBaseUnitId
              OR rd.Quantity <> CAST(15 AS decimal(18,3))
          )
    )
    BEGIN
        ;THROW 53507,
               N'DEMO_REORDER_V14: vanilla syrup đã có trong Caramel Macchiato nhưng payload khác contract.',
               1;
    END;


    /* ============================================================
       Contract drift check - White Pearl

       Ingredient này có BaseUnit = DEMO_PORTION.
       BOM legacy của chính seed cũng dùng 1 portion.
       ============================================================ */
    IF EXISTS
    (
        SELECT 1
        FROM dbo.RecipeDetails rd
        WHERE rd.RecipeId = @TradMilkTeaMRecipeId
          AND rd.IngredientId = @WhitePearlIngredientId
          AND
          (
              rd.ChildRecipeId IS NOT NULL
              OR rd.UnitId <> @WhitePearlBaseUnitId
              OR rd.Quantity <> CAST(1 AS decimal(18,3))
          )
    )
    OR EXISTS
    (
        SELECT 1
        FROM dbo.RecipeDetails rd
        WHERE rd.RecipeId = @TradMilkTeaLRecipeId
          AND rd.IngredientId = @WhitePearlIngredientId
          AND
          (
              rd.ChildRecipeId IS NOT NULL
              OR rd.UnitId <> @WhitePearlBaseUnitId
              OR rd.Quantity <> CAST(1 AS decimal(18,3))
          )
    )
    BEGIN
        ;THROW 53508,
               N'DEMO_REORDER_V14: white pearl đã có trong Trà sữa truyền thống nhưng payload khác contract.',
               1;
    END;


    /* ============================================================
       Add Vanilla Syrup to real drink BOM
       ============================================================ */
    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.RecipeDetails
        WHERE RecipeId = @CaramelMacchiatoMRecipeId
          AND IngredientId = @VanillaIngredientId
    )
    BEGIN
        INSERT dbo.RecipeDetails
        (
            RecipeId,
            IngredientId,
            ChildRecipeId,
            Quantity,
            UnitId
        )
        VALUES
        (
            @CaramelMacchiatoMRecipeId,
            @VanillaIngredientId,
            NULL,
            10,
            @VanillaBaseUnitId
        );
    END;


    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.RecipeDetails
        WHERE RecipeId = @CaramelMacchiatoLRecipeId
          AND IngredientId = @VanillaIngredientId
    )
    BEGIN
        INSERT dbo.RecipeDetails
        (
            RecipeId,
            IngredientId,
            ChildRecipeId,
            Quantity,
            UnitId
        )
        VALUES
        (
            @CaramelMacchiatoLRecipeId,
            @VanillaIngredientId,
            NULL,
            15,
            @VanillaBaseUnitId
        );
    END;


    /* ============================================================
       Add ready White Pearl to real drink BOM.

       Không sửa/activate DEMO_RECIPE_TOP_WHITE_PEARL archived.
       Không thay Legacy Recipe identity.
       ============================================================ */
    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.RecipeDetails
        WHERE RecipeId = @TradMilkTeaMRecipeId
          AND IngredientId = @WhitePearlIngredientId
    )
    BEGIN
        INSERT dbo.RecipeDetails
        (
            RecipeId,
            IngredientId,
            ChildRecipeId,
            Quantity,
            UnitId
        )
        VALUES
        (
            @TradMilkTeaMRecipeId,
            @WhitePearlIngredientId,
            NULL,
            1,
            @WhitePearlBaseUnitId
        );
    END;


    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.RecipeDetails
        WHERE RecipeId = @TradMilkTeaLRecipeId
          AND IngredientId = @WhitePearlIngredientId
    )
    BEGIN
        INSERT dbo.RecipeDetails
        (
            RecipeId,
            IngredientId,
            ChildRecipeId,
            Quantity,
            UnitId
        )
        VALUES
        (
            @TradMilkTeaLRecipeId,
            @WhitePearlIngredientId,
            NULL,
            1,
            @WhitePearlBaseUnitId
        );
    END;

    /* ------------------------------------------------------------
       14.3 Clone Store 1 availability/configuration to Store 3
       ------------------------------------------------------------ */
    IF EXISTS(
        SELECT 1 FROM dbo.StoreDrinks src
        JOIN dbo.StoreDrinks dst ON dst.StoreId=@Store3Id AND dst.DrinkId=src.DrinkId
        WHERE src.StoreId=@Store1Id AND dst.Active<>src.Active
    ) THROW 53484,N'DEMO_REORDER_V14: StoreDrink Store3 cùng business key nhưng khác Store1.',1;

    INSERT dbo.StoreDrinks(StoreId,DrinkId,Active)
    SELECT @Store3Id,sd.DrinkId,sd.Active
    FROM dbo.StoreDrinks sd
    WHERE sd.StoreId=@Store1Id
      AND NOT EXISTS(SELECT 1 FROM dbo.StoreDrinks x WHERE x.StoreId=@Store3Id AND x.DrinkId=sd.DrinkId);

    DECLARE @ExtraDrinkNeeded int=30-(SELECT COUNT(*) FROM dbo.StoreDrinks WHERE StoreId=@Store1Id AND Active=1);
    DECLARE @Store3ExtraDrinks TABLE(DrinkId int PRIMARY KEY);
    IF @ExtraDrinkNeeded>0
    BEGIN
        INSERT @Store3ExtraDrinks(DrinkId)
        SELECT TOP(@ExtraDrinkNeeded) d.DrinkId
        FROM dbo.Drinks d
        WHERE d.Active=1
          AND EXISTS(SELECT 1 FROM dbo.DrinkSizes ds WHERE ds.DrinkId=d.DrinkId AND ds.Active=1)
          AND NOT EXISTS(SELECT 1 FROM dbo.StoreDrinks src WHERE src.StoreId=@Store1Id AND src.DrinkId=d.DrinkId)
        ORDER BY d.DrinkCode;

        IF (SELECT COUNT(*) FROM @Store3ExtraDrinks)<>@ExtraDrinkNeeded
            THROW 53485,N'DEMO_REORDER_V14: master hiện có không đủ Drink business key để Store3 đạt 30 StoreDrinks.',1;

        IF EXISTS(
            SELECT 1 FROM @Store3ExtraDrinks x
            JOIN dbo.StoreDrinks dst ON dst.StoreId=@Store3Id AND dst.DrinkId=x.DrinkId
            WHERE dst.Active<>1
        ) THROW 53486,N'DEMO_REORDER_V14: Store3 extra StoreDrink business-key payload drift.',1;

        INSERT dbo.StoreDrinks(StoreId,DrinkId,Active)
        SELECT @Store3Id,x.DrinkId,1 FROM @Store3ExtraDrinks x
        WHERE NOT EXISTS(SELECT 1 FROM dbo.StoreDrinks dst WHERE dst.StoreId=@Store3Id AND dst.DrinkId=x.DrinkId);
    END;

    IF (SELECT COUNT(*) FROM dbo.StoreDrinks WHERE StoreId=@Store3Id AND Active=1)<30
        THROW 53411,N'DEMO_REORDER_V14: Store 3 không đạt tối thiểu 30 StoreDrinks từ master hiện có.',1;

    IF EXISTS(
        SELECT 1
        FROM dbo.StoreMenuItems src
        JOIN dbo.StoreMenuItems dst ON dst.StoreId=@Store3Id AND dst.DrinkSizeId=src.DrinkSizeId
        WHERE src.StoreId=@Store1Id
          AND (dst.IsEnabled<>src.IsEnabled
            OR ISNULL(dst.PriceOverride,-1)<>ISNULL(src.PriceOverride,-1)
            OR ISNULL(dst.EffectiveFromUtc,'19000101')<>ISNULL(src.EffectiveFromUtc,'19000101')
            OR ISNULL(dst.EffectiveToUtc,'19000101')<>ISNULL(src.EffectiveToUtc,'19000101')
            OR dst.DisplayOrder<>src.DisplayOrder
            OR ISNULL(dst.PauseReason,N'')<>ISNULL(src.PauseReason,N'')
            OR ISNULL(dst.Note,N'')<>ISNULL(src.Note,N''))
    ) THROW 53412,N'DEMO_REORDER_V14: StoreMenuItem Store 3 cùng business key nhưng khác cấu hình Store 1.',1;

    INSERT dbo.StoreMenuItems(StoreId,DrinkSizeId,IsEnabled,PriceOverride,EffectiveFromUtc,EffectiveToUtc,
                              DisplayOrder,PauseReason,Note,PublishedAtUtc,PublishedByStaffId,CreatedAtUtc,UpdatedAtUtc)
    SELECT @Store3Id,src.DrinkSizeId,src.IsEnabled,src.PriceOverride,src.EffectiveFromUtc,src.EffectiveToUtc,
           src.DisplayOrder,src.PauseReason,src.Note,@SeedAnchorUtc,@Store3StaffId,@SeedAnchorUtc,@SeedAnchorUtc
    FROM dbo.StoreMenuItems src
    WHERE src.StoreId=@Store1Id
      AND NOT EXISTS(SELECT 1 FROM dbo.StoreMenuItems dst WHERE dst.StoreId=@Store3Id AND dst.DrinkSizeId=src.DrinkSizeId);

    IF (SELECT COUNT(*) FROM dbo.StoreMenuItems WHERE StoreId=@Store3Id)<30
        THROW 53413,N'DEMO_REORDER_V14: Store 3 không đạt tối thiểu 30 StoreMenuItems.',1;

    IF EXISTS(
        SELECT 1 FROM dbo.StoreToppings src
        JOIN dbo.StoreToppings dst ON dst.StoreId=@Store3Id AND dst.ToppingId=src.ToppingId
        WHERE src.StoreId=@Store1Id AND dst.Active<>src.Active
    ) THROW 53414,N'DEMO_REORDER_V14: StoreTopping Store 3 cùng business key nhưng khác Store 1.',1;

    INSERT dbo.StoreToppings(StoreId,ToppingId,Active)
    SELECT @Store3Id,src.ToppingId,src.Active
    FROM dbo.StoreToppings src
    WHERE src.StoreId=@Store1Id
      AND NOT EXISTS(SELECT 1 FROM dbo.StoreToppings dst WHERE dst.StoreId=@Store3Id AND dst.ToppingId=src.ToppingId);

    IF (SELECT COUNT(*) FROM dbo.StoreToppings WHERE StoreId=@Store3Id AND Active=1)<30
        THROW 53415,N'DEMO_REORDER_V14: Store 3 không đạt tối thiểu 30 StoreToppings active.',1;

    IF EXISTS(
        SELECT 1 FROM dbo.SupplierStores src
        JOIN dbo.SupplierStores dst ON dst.StoreId=@Store3Id AND dst.SupplierId=src.SupplierId
        WHERE src.StoreId=@Store1Id
          AND (dst.Active<>src.Active
            OR ISNULL(dst.LeadTimeOverrideDays,-1)<>ISNULL(src.LeadTimeOverrideDays,-1)
            OR ISNULL(dst.DeliverySchedule,N'')<>ISNULL(src.DeliverySchedule,N''))
    ) THROW 53416,N'DEMO_REORDER_V14: SupplierStore Store 3 cùng business key nhưng khác Store 1.',1;

    INSERT dbo.SupplierStores(SupplierId,StoreId,Active,LeadTimeOverrideDays,DeliverySchedule,Note,CreatedAt,UpdatedAt)
    SELECT src.SupplierId,@Store3Id,src.Active,src.LeadTimeOverrideDays,src.DeliverySchedule,
           N'DEMO_REORDER_V14 | cloned Store1 supplier scope',@SeedAnchorUtc,@SeedAnchorUtc
    FROM dbo.SupplierStores src
    WHERE src.StoreId=@Store1Id
      AND NOT EXISTS(SELECT 1 FROM dbo.SupplierStores dst WHERE dst.StoreId=@Store3Id AND dst.SupplierId=src.SupplierId);

    IF (SELECT COUNT(*) FROM dbo.SupplierStores WHERE StoreId=@Store3Id AND Active=1)<50
        THROW 53417,N'DEMO_REORDER_V14: Store 3 không có đủ 50 SupplierStores active.',1;

    /* Supplier and package/base-unit evidence required by Reorder. */
    IF (SELECT COUNT(*) FROM dbo.InventoryDocumentDetails d JOIN dbo.InventoryDocuments h ON h.InventoryDocumentId=d.InventoryDocumentId WHERE h.RequestKey=N'DEMO_OPENING_STORE1_INGREDIENTS')<>50
    OR (SELECT COUNT(DISTINCT d.IngredientId) FROM dbo.InventoryDocumentDetails d JOIN dbo.InventoryDocuments h ON h.InventoryDocumentId=d.InventoryDocumentId WHERE h.RequestKey=N'DEMO_OPENING_STORE1_INGREDIENTS')<>50
        THROW 53418,N'DEMO_REORDER_V14: source opening contract không có đúng 50 distinct ingredients.',1;

    IF EXISTS(
        SELECT 1
        FROM dbo.InventoryDocumentDetails seedLine
        JOIN dbo.InventoryDocuments seedDoc ON seedDoc.InventoryDocumentId=seedLine.InventoryDocumentId AND seedDoc.RequestKey=N'DEMO_OPENING_STORE1_INGREDIENTS'
        JOIN dbo.Ingredients i ON i.IngredientId=seedLine.IngredientId AND i.Active=1
        WHERE NOT EXISTS(
              SELECT 1
              FROM dbo.IngredientSuppliers o
              JOIN dbo.Suppliers s ON s.SupplierId=o.SupplierId AND s.Active=1
              JOIN dbo.SupplierStores ss ON ss.SupplierId=s.SupplierId AND ss.StoreId=@Store3Id AND ss.Active=1
              WHERE o.IngredientId=i.IngredientId AND o.Active=1 AND o.IsPrimary=1
                AND o.PackageQuantity>0 AND o.CurrentPrice>0 AND o.LeadTimeDays IS NOT NULL AND o.LeadTimeDays>=0
                AND EXISTS(SELECT 1 FROM dbo.IngredientSupplierPriceHistories ph
                           WHERE ph.IngredientSupplierId=o.IngredientSupplierId AND ph.IsCurrent=1
                             AND ph.Price>0 AND ph.PackageQuantity>0 AND ph.PackageUnitId IS NOT NULL
                             AND (ph.PackageUnitId=i.BaseUnitId OR EXISTS(
                                 SELECT 1 FROM dbo.UnitConversions phuc
                                 WHERE phuc.IngredientId=i.IngredientId AND phuc.FromUnitId=ph.PackageUnitId
                                   AND phuc.ToUnitId=i.BaseUnitId AND phuc.Active=1
                                   AND phuc.FromQuantity>0 AND phuc.ToQuantity>0)))
                AND (o.UnitId=i.BaseUnitId OR EXISTS(
                    SELECT 1 FROM dbo.UnitConversions uc
                    WHERE uc.IngredientId=i.IngredientId AND uc.FromUnitId=o.UnitId
                      AND uc.ToUnitId=i.BaseUnitId AND uc.Active=1 AND uc.FromQuantity>0 AND uc.ToQuantity>0))
          )
    ) THROW 53419,N'DEMO_REORDER_V14: thiếu supplier/package/unit conversion/price/lead-time active cho ít nhất một ingredient.',1;

    /* ------------------------------------------------------------
       14.4 Store 3 opening inventory. Clone Store1 opening document
       evidence, not the current on-hand quantity.
       ------------------------------------------------------------ */
    DECLARE @Store1OpeningDocId int=(SELECT InventoryDocumentId FROM dbo.InventoryDocuments WHERE RequestKey=N'DEMO_OPENING_STORE1_INGREDIENTS');
    DECLARE @Store3OpeningKey nvarchar(100)=N'DEMO_REORDER_V14_OPENING_STORE3';
    DECLARE @Store3OpeningDocId int=(SELECT InventoryDocumentId FROM dbo.InventoryDocuments WHERE RequestKey=@Store3OpeningKey);
    IF @Store1OpeningDocId IS NULL OR (SELECT COUNT(*) FROM dbo.InventoryDocumentDetails WHERE InventoryDocumentId=@Store1OpeningDocId)<>50
        THROW 53420,N'DEMO_REORDER_V14: opening document Store 1 không đủ 50 lines làm source evidence.',1;

    /* EF/migration may already contain a Store3 ingredient balance without ledger/cost evidence.
       Do not silently rewrite it to zero. Reconcile it through an auditable STOCK_TAKE/ADJUSTMENT_OUT first,
       then create the new opening evidence from zero. */
    DECLARE @Store3ReconcileKey nvarchar(100)=N'DEMO_REORDER_V14_RECONCILE_STORE3';
    DECLARE @Store3ReconcileDocId int=(SELECT InventoryDocumentId FROM dbo.InventoryDocuments WHERE RequestKey=@Store3ReconcileKey);

    IF @Store3OpeningDocId IS NULL
    BEGIN
        IF @Store3ReconcileDocId IS NOT NULL
            THROW 53471,N'DEMO_REORDER_V14: có reconciliation Store3 nhưng thiếu opening document; fixture partial.',1;

        IF EXISTS(SELECT 1 FROM dbo.StoreInventories WHERE StoreId=@Store3Id AND IngredientId IS NOT NULL AND AvailableQty<0)
            THROW 53472,N'DEMO_REORDER_V14: Store3 có legacy ingredient quantity âm trước opening; không tự sửa âm thầm.',1;

        IF EXISTS(
            SELECT 1 FROM dbo.StoreInventories si
            WHERE si.StoreId=@Store3Id AND si.IngredientId IS NOT NULL
              AND (si.RecipeId IS NOT NULL OR si.PreparedItemId IS NOT NULL OR si.BtpIdentityState IS NOT NULL
                OR si.QuantitySemanticsStatus IS NOT NULL OR si.SupersededByStoreInventoryId IS NOT NULL
                OR si.QuantitySemanticsEvidenceType IS NOT NULL OR si.QuantitySemanticsEvidenceReference IS NOT NULL
                OR si.QuantitySemanticsReviewedAt IS NOT NULL OR si.QuantitySemanticsReviewedByAccountId IS NOT NULL
                OR si.ReservedQty<>0)
        ) THROW 53489,N'DEMO_REORDER_V14: Store3 legacy ingredient row có identity/lifecycle/reserved payload không hợp lệ để reconcile.',1;

        IF EXISTS(
            SELECT 1 FROM dbo.StoreInventories si
            WHERE si.StoreId=@Store3Id AND si.IngredientId IS NOT NULL
              AND (EXISTS(SELECT 1 FROM dbo.InventoryTransactions t WHERE t.StoreInventoryId=si.StoreInventoryId)
                OR EXISTS(SELECT 1 FROM dbo.InventoryCostLayers l WHERE l.StoreId=@Store3Id AND l.IngredientId=si.IngredientId))
        ) THROW 53421,N'DEMO_REORDER_V14: Store 3 đã có ingredient ledger/cost evidence ngoài fixture; không được overwrite opening.',1;

        IF EXISTS(SELECT 1 FROM dbo.StoreInventories WHERE StoreId=@Store3Id AND IngredientId IS NOT NULL AND AvailableQty>0)
        BEGIN
            INSERT dbo.InventoryDocuments(Code,StoreId,StaffId,DocumentDate,[Type],[Status],RequestKey,IsProcessing,
                                          ConfirmedAt,ConfirmedBy,Purpose,PartnerType,PartnerId,PartnerName,SupplierId,
                                          Note,AllowNegativeStock,NegativeReason,TotalAmount,VatAmount,FinalAmount)
            VALUES(N'DEMO_REORDER_V14_RECON_STORE3',@Store3Id,@Store3StaffId,DATEADD(MINUTE,30,DATEADD(DAY,-29,@SeedDayUtc)),
                   4,3,@Store3ReconcileKey,0,DATEADD(MINUTE,30,DATEADD(DAY,-29,@SeedDayUtc)),@Store3StaffId,11,0,
                   NULL,NULL,NULL,N'DEMO_REORDER_V14 reconcile legacy Store3 quantity without ledger',0,NULL,0,0,0);
            SET @Store3ReconcileDocId=SCOPE_IDENTITY();

            INSERT dbo.InventoryDocumentDetails(InventoryDocumentId,IngredientId,Quantity,BaseQuantity,UnitId,
                                                UnitPrice,CostPrice,CostAmount,Note,TotalAmount)
            SELECT @Store3ReconcileDocId,si.IngredientId,si.AvailableQty,si.AvailableQty,i.BaseUnitId,
                   NULL,NULL,NULL,N'DEMO_REORDER_V14_RECON_'+i.Code,0
            FROM dbo.StoreInventories si
            JOIN dbo.Ingredients i ON i.IngredientId=si.IngredientId
            WHERE si.StoreId=@Store3Id AND si.IngredientId IS NOT NULL AND si.AvailableQty>0;

            INSERT dbo.InventoryTransactions(StoreInventoryId,[Type],StockStatus,Quantity,BeforeQty,AfterQty,UnitCost,TotalCost,
                                              InventoryDocumentId,InventoryDocumentDetailId,InventoryTransferId,InventoryTransferDetailId,
                                              ReferenceOrderId,ProductionRunId,SourceRecipeId,InventoryConsolidationRunId,BranchReceiptLineId,
                                              OrderRefundId,CreatedAt)
            SELECT si.StoreInventoryId,9,5,d.BaseQuantity,si.AvailableQty,0,NULL,NULL,@Store3ReconcileDocId,d.InventoryDocumentDetailId,
                   NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,DATEADD(MINUTE,30,DATEADD(DAY,-29,@SeedDayUtc))
            FROM dbo.InventoryDocumentDetails d
            JOIN dbo.StoreInventories si ON si.StoreId=@Store3Id AND si.IngredientId=d.IngredientId
            WHERE d.InventoryDocumentId=@Store3ReconcileDocId;

            UPDATE si SET si.AvailableQty=0,si.ReservedQty=0,si.LastUpdated=DATEADD(MINUTE,30,DATEADD(DAY,-29,@SeedDayUtc))
            FROM dbo.StoreInventories si
            WHERE si.StoreId=@Store3Id AND si.IngredientId IS NOT NULL AND si.AvailableQty>0;
        END;
        INSERT dbo.InventoryDocuments(Code,StoreId,StaffId,DocumentDate,[Type],[Status],RequestKey,IsProcessing,
                                      ConfirmedAt,ConfirmedBy,Purpose,PartnerType,PartnerId,PartnerName,SupplierId,
                                      Note,AllowNegativeStock,NegativeReason,TotalAmount,VatAmount,FinalAmount)
        SELECT N'DEMO_REORDER_V14_OPENING_STORE3',@Store3Id,@Store3StaffId,DATEADD(HOUR,1,DATEADD(DAY,-29,@SeedDayUtc)),
               8,3,@Store3OpeningKey,0,DATEADD(HOUR,1,DATEADD(DAY,-29,@SeedDayUtc)),@Store3StaffId,3,0,NULL,NULL,NULL,
               N'DEMO_REORDER_V14 opening evidence cloned from Store1',0,NULL,
               SUM(ISNULL(d.TotalAmount,ROUND(d.BaseQuantity*ISNULL(d.CostPrice,0),2))),0,
               SUM(ISNULL(d.TotalAmount,ROUND(d.BaseQuantity*ISNULL(d.CostPrice,0),2)))
        FROM dbo.InventoryDocumentDetails d WHERE d.InventoryDocumentId=@Store1OpeningDocId;

        SET @Store3OpeningDocId=SCOPE_IDENTITY();

        INSERT dbo.InventoryDocumentDetails(InventoryDocumentId,IngredientId,Quantity,BaseQuantity,UnitId,
                                            UnitPrice,CostPrice,CostAmount,Note,TotalAmount)
        SELECT @Store3OpeningDocId,d.IngredientId,d.Quantity,d.BaseQuantity,d.UnitId,
               d.UnitPrice,d.CostPrice,d.CostAmount,N'DEMO_REORDER_V14_OPENING_'+i.Code,d.TotalAmount
        FROM dbo.InventoryDocumentDetails d
        JOIN dbo.Ingredients i ON i.IngredientId=d.IngredientId
        WHERE d.InventoryDocumentId=@Store1OpeningDocId;

        /* Insert missing ingredient identities at zero, then apply opening together with evidence. */
        INSERT dbo.StoreInventories(StoreId,IngredientId,RecipeId,PreparedItemId,BtpIdentityState,QuantitySemanticsStatus,
                                    SupersededByStoreInventoryId,QuantitySemanticsEvidenceType,QuantitySemanticsEvidenceReference,
                                    QuantitySemanticsReviewedAt,QuantitySemanticsReviewedByAccountId,
                                    AvailableQty,ReservedQty,MaxNegativeQty,MinStockLevel,LastUpdated)
        SELECT @Store3Id,d.IngredientId,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,0,0,NULL,srcSi.MinStockLevel,@SeedAnchorUtc
        FROM dbo.InventoryDocumentDetails d
        JOIN dbo.StoreInventories srcSi ON srcSi.StoreId=@Store1Id AND srcSi.IngredientId=d.IngredientId
        WHERE d.InventoryDocumentId=@Store1OpeningDocId
          AND NOT EXISTS(SELECT 1 FROM dbo.StoreInventories dst WHERE dst.StoreId=@Store3Id AND dst.IngredientId=d.IngredientId);

        IF (SELECT COUNT(*) FROM dbo.StoreInventories WHERE StoreId=@Store3Id AND IngredientId IS NOT NULL)<>50
            THROW 53422,N'DEMO_REORDER_V14: Store 3 không resolve đúng 50 ingredient StoreInventories.',1;

        UPDATE si
        SET si.AvailableQty=d.BaseQuantity,si.ReservedQty=0,si.MaxNegativeQty=NULL,
            si.MinStockLevel=srcSi.MinStockLevel,si.LastUpdated=@SeedAnchorUtc
        FROM dbo.StoreInventories si
        JOIN dbo.InventoryDocumentDetails d ON d.InventoryDocumentId=@Store3OpeningDocId AND d.IngredientId=si.IngredientId
        JOIN dbo.StoreInventories srcSi ON srcSi.StoreId=@Store1Id AND srcSi.IngredientId=d.IngredientId
        WHERE si.StoreId=@Store3Id;

        INSERT dbo.InventoryTransactions(StoreInventoryId,[Type],StockStatus,Quantity,BeforeQty,AfterQty,UnitCost,TotalCost,
                                          InventoryDocumentId,InventoryDocumentDetailId,InventoryTransferId,InventoryTransferDetailId,
                                          ReferenceOrderId,ProductionRunId,SourceRecipeId,InventoryConsolidationRunId,BranchReceiptLineId,
                                          OrderRefundId,CreatedAt)
        SELECT si.StoreInventoryId,8,5,d.BaseQuantity,0,d.BaseQuantity,d.CostPrice,
               ISNULL(d.CostAmount,ROUND(d.BaseQuantity*d.CostPrice,2)),@Store3OpeningDocId,d.InventoryDocumentDetailId,
               NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,DATEADD(HOUR,1,DATEADD(DAY,-29,@SeedDayUtc))
        FROM dbo.InventoryDocumentDetails d
        JOIN dbo.StoreInventories si ON si.StoreId=@Store3Id AND si.IngredientId=d.IngredientId
        WHERE d.InventoryDocumentId=@Store3OpeningDocId;

        INSERT dbo.InventoryCostLayers(IngredientId,PreparedItemId,StoreId,Quantity,RemainingQuantity,UnitCost,CreatedAt,
                                       SourceProductionRunId,SourceOrderRefundId,SourceInventoryDocumentDetailId,
                                       SourceBranchReceiptLineId,SourceTransferCostAllocationId,SourceTransferDiscrepancyPostingId)
        SELECT d.IngredientId,NULL,@Store3Id,d.BaseQuantity,d.BaseQuantity,d.CostPrice,
               DATEADD(HOUR,1,DATEADD(DAY,-29,@SeedDayUtc)),NULL,NULL,d.InventoryDocumentDetailId,NULL,NULL,NULL
        FROM dbo.InventoryDocumentDetails d WHERE d.InventoryDocumentId=@Store3OpeningDocId;
    END
    ELSE
    BEGIN
        IF EXISTS(
            SELECT 1
            FROM dbo.InventoryDocuments d
            WHERE d.InventoryDocumentId=@Store3OpeningDocId
              AND (d.Code<>N'DEMO_REORDER_V14_OPENING_STORE3' OR d.StoreId<>@Store3Id OR d.StaffId<>@Store3StaffId
                OR d.[Type]<>8 OR d.[Status]<>3 OR d.Purpose<>3 OR d.PartnerType<>0 OR d.IsProcessing<>0)
        ) THROW 53423,N'DEMO_REORDER_V14: opening Store 3 business-key payload drift.',1;

        IF @Store3ReconcileDocId IS NOT NULL
        BEGIN
            IF EXISTS(
                SELECT 1 FROM dbo.InventoryDocuments d
                WHERE d.InventoryDocumentId=@Store3ReconcileDocId
                  AND (d.Code<>N'DEMO_REORDER_V14_RECON_STORE3' OR d.StoreId<>@Store3Id OR d.StaffId<>@Store3StaffId
                    OR d.[Type]<>4 OR d.[Status]<>3 OR d.Purpose<>11 OR d.PartnerType<>0 OR d.IsProcessing<>0)
            ) THROW 53473,N'DEMO_REORDER_V14: Store3 reconciliation payload drift.',1;

            IF EXISTS(
                SELECT 1
                FROM dbo.InventoryDocumentDetails d
                LEFT JOIN dbo.InventoryTransactions t ON t.InventoryDocumentDetailId=d.InventoryDocumentDetailId AND t.[Type]=9
                WHERE d.InventoryDocumentId=@Store3ReconcileDocId
                  AND (t.InventoryTransactionId IS NULL OR t.InventoryDocumentId<>@Store3ReconcileDocId
                    OR t.Quantity<>d.BaseQuantity OR t.BeforeQty<>d.BaseQuantity OR t.AfterQty<>0)
            ) THROW 53474,N'DEMO_REORDER_V14: Store3 reconciliation detail/transaction payload drift.',1;
        END;

        IF (SELECT COUNT(*) FROM dbo.InventoryDocumentDetails WHERE InventoryDocumentId=@Store3OpeningDocId)<>50
        OR (SELECT COUNT(*) FROM dbo.InventoryTransactions WHERE InventoryDocumentId=@Store3OpeningDocId AND [Type]=8)<>50
        OR (SELECT COUNT(*) FROM dbo.InventoryCostLayers l JOIN dbo.InventoryDocumentDetails d ON d.InventoryDocumentDetailId=l.SourceInventoryDocumentDetailId WHERE d.InventoryDocumentId=@Store3OpeningDocId)<>50
            THROW 53424,N'DEMO_REORDER_V14: opening Store 3 thiếu detail/transaction/cost-layer evidence.',1;

        IF EXISTS(
            SELECT 1
            FROM dbo.InventoryDocumentDetails src
            JOIN dbo.InventoryDocumentDetails dst ON dst.InventoryDocumentId=@Store3OpeningDocId AND dst.IngredientId=src.IngredientId
            WHERE src.InventoryDocumentId=@Store1OpeningDocId
              AND (dst.BaseQuantity<>src.BaseQuantity OR dst.UnitId<>src.UnitId OR ISNULL(dst.CostPrice,-1)<>ISNULL(src.CostPrice,-1))
        ) THROW 53425,N'DEMO_REORDER_V14: opening Store 3 detail payload drift so với source Store1.',1;

        IF EXISTS(
            SELECT 1
            FROM dbo.InventoryDocumentDetails d
            LEFT JOIN dbo.StoreInventories si ON si.StoreId=@Store3Id AND si.IngredientId=d.IngredientId
            LEFT JOIN dbo.InventoryTransactions t ON t.InventoryDocumentDetailId=d.InventoryDocumentDetailId AND t.[Type]=8
            WHERE d.InventoryDocumentId=@Store3OpeningDocId
              AND (si.StoreInventoryId IS NULL OR t.InventoryTransactionId IS NULL OR t.StoreInventoryId<>si.StoreInventoryId
                OR t.InventoryDocumentId<>@Store3OpeningDocId OR t.Quantity<>d.BaseQuantity OR t.BeforeQty<>0 OR t.AfterQty<>d.BaseQuantity
                OR ISNULL(t.UnitCost,-1)<>ISNULL(d.CostPrice,-1)
                OR ISNULL(t.TotalCost,-1)<>ISNULL(d.CostAmount,ROUND(d.BaseQuantity*ISNULL(d.CostPrice,0),2)))
        ) THROW 53487,N'DEMO_REORDER_V14: Store3 opening transaction payload drift.',1;

        IF EXISTS(
            SELECT 1
            FROM dbo.InventoryDocumentDetails d
            LEFT JOIN dbo.InventoryCostLayers l ON l.SourceInventoryDocumentDetailId=d.InventoryDocumentDetailId
            WHERE d.InventoryDocumentId=@Store3OpeningDocId
              AND (l.InventoryCostLayerId IS NULL OR l.StoreId<>@Store3Id OR l.IngredientId<>d.IngredientId OR l.PreparedItemId IS NOT NULL
                OR l.Quantity<>d.BaseQuantity OR ISNULL(l.UnitCost,-1)<>ISNULL(d.CostPrice,-1)
                OR l.RemainingQuantity<0 OR l.RemainingQuantity>l.Quantity)
        ) THROW 53488,N'DEMO_REORDER_V14: Store3 opening cost-layer payload drift.',1;
    END;

    /* Clone the current canonical Recipe+PreparedItem identity shape from Store 1 at zero.
       Production fixtures are the only source of Store3 BTP quantity/layers. */
    IF EXISTS(
        SELECT 1 FROM dbo.StoreInventories src
        JOIN dbo.PreparedItems p ON p.PreparedItemId=src.PreparedItemId
        JOIN dbo.StoreInventories dst ON dst.StoreId=@Store3Id AND dst.RecipeId=src.RecipeId
        WHERE src.StoreId=@Store1Id AND src.IngredientId IS NULL AND src.PreparedItemId IS NOT NULL AND src.BtpIdentityState=1
          AND (dst.IngredientId IS NOT NULL OR dst.PreparedItemId<>src.PreparedItemId OR dst.BtpIdentityState<>src.BtpIdentityState
            OR ISNULL(dst.QuantitySemanticsStatus,-1)<>ISNULL(src.QuantitySemanticsStatus,-1) OR dst.SupersededByStoreInventoryId IS NOT NULL
            OR ISNULL(dst.QuantitySemanticsEvidenceType,-1)<>ISNULL(src.QuantitySemanticsEvidenceType,-1)
            OR ISNULL(dst.QuantitySemanticsEvidenceReference,N'')<>N'DEMO_REORDER_V14_BTP_'+p.Code
            OR ISNULL(dst.QuantitySemanticsReviewedByAccountId,-1)<>@Store3AccountId OR dst.ReservedQty<>0
            OR dst.MaxNegativeQty IS NOT NULL OR ISNULL(dst.MinStockLevel,-1)<>ISNULL(src.MinStockLevel,-1))
    ) THROW 53426,N'DEMO_REORDER_V14: Store3 BTP identity row cùng Recipe business key nhưng khác contract.',1;

    INSERT dbo.StoreInventories(StoreId,IngredientId,RecipeId,PreparedItemId,BtpIdentityState,QuantitySemanticsStatus,
                                SupersededByStoreInventoryId,QuantitySemanticsEvidenceType,QuantitySemanticsEvidenceReference,
                                QuantitySemanticsReviewedAt,QuantitySemanticsReviewedByAccountId,
                                AvailableQty,ReservedQty,MaxNegativeQty,MinStockLevel,LastUpdated)
    SELECT @Store3Id,NULL,src.RecipeId,src.PreparedItemId,src.BtpIdentityState,src.QuantitySemanticsStatus,NULL,
           src.QuantitySemanticsEvidenceType,N'DEMO_REORDER_V14_BTP_'+p.Code,@SeedAnchorUtc,@Store3AccountId,
           0,0,NULL,src.MinStockLevel,@SeedAnchorUtc
    FROM dbo.StoreInventories src
    JOIN dbo.PreparedItems p ON p.PreparedItemId=src.PreparedItemId
    WHERE src.StoreId=@Store1Id AND src.IngredientId IS NULL AND src.PreparedItemId IS NOT NULL AND src.BtpIdentityState=1
      AND NOT EXISTS(SELECT 1 FROM dbo.StoreInventories dst WHERE dst.StoreId=@Store3Id AND dst.RecipeId=src.RecipeId);

    /* ------------------------------------------------------------
       14.5 Fixed fixture keys and first-run/replay state
       ------------------------------------------------------------ */
    DECLARE @FixtureStores TABLE(StoreId int PRIMARY KEY,StaffId int NOT NULL,StoreNo int NOT NULL);
    INSERT @FixtureStores VALUES(@Store1Id,@Store1StaffId,1),(@Store3Id,@Store3StaffId,3);

    /* ------------------------------------------------------------
   14.5A Audited sales stock buffer for legacy ING00001

   Batch 08 hiện có:
   - Store 1 opening ING00001 = 100
   - Adjustment OUT = 10
   => Store 1 thực tế còn 90.

   Trong khi BOM sales Batch 14 cần tổng cộng 110 ING00001
   cho mỗi store.

   Không sửa opening cũ của Batch 08.
   Không UPDATE tồn trực tiếp mà không có chứng từ.

   Vì vậy tạo một ADJUSTMENT_IN có đầy đủ:
   InventoryDocument
   -> InventoryDocumentDetail
   -> InventoryTransaction
   -> InventoryCostLayer
   -> StoreInventory
   ------------------------------------------------------------ */

DECLARE @SalesBufferIngredientId int =
(
    SELECT IngredientId
    FROM dbo.Ingredients
    WHERE Code = N'ING00001'
      AND Active = 1
);

DECLARE @SalesBufferBaseUnitId int =
(
    SELECT BaseUnitId
    FROM dbo.Ingredients
    WHERE IngredientId = @SalesBufferIngredientId
);

-- Thêm 100 base-unit cho mỗi Store.
DECLARE @SalesBufferQty decimal(18,3) =
    CAST(100 AS decimal(18,3));

DECLARE @SalesBufferUnitCost decimal(18,2);

DECLARE @SalesBufferAt datetime2(0) =
    DATEADD(
        HOUR,
        2,
        DATEADD(DAY,-29,@SeedDayUtc)
    );

/* ============================================================
   Tính cost dựa trên supplier thật.
   Không hard-code UnitCost.
   ============================================================ */
SELECT TOP(1)
    @SalesBufferUnitCost =
        CONVERT(
            decimal(18,2),
            ROUND(
                o.CurrentPrice /
                NULLIF(
                    o.PackageQuantity *
                    CASE
                        WHEN o.UnitId = i.BaseUnitId
                            THEN 1
                        ELSE
                            uc.ToQuantity /
                            NULLIF(uc.FromQuantity,0)
                    END,
                    0
                ),
                2
            )
        )
FROM dbo.IngredientSuppliers o

JOIN dbo.Ingredients i
    ON i.IngredientId = o.IngredientId

LEFT JOIN dbo.UnitConversions uc
    ON uc.IngredientId = i.IngredientId
   AND uc.FromUnitId = o.UnitId
   AND uc.ToUnitId = i.BaseUnitId
   AND uc.Active = 1
   AND uc.FromQuantity > 0
   AND uc.ToQuantity > 0

WHERE
    o.IngredientId = @SalesBufferIngredientId
    AND o.Active = 1
    AND o.IsPrimary = 1
    AND o.PackageQuantity > 0
    AND o.CurrentPrice > 0

    AND
    (
        o.UnitId = i.BaseUnitId

        OR uc.UnitConversionId IS NOT NULL
    )

ORDER BY
    o.IngredientSupplierId;


/* ============================================================
   Validate dữ liệu nguồn.
   ============================================================ */
IF @SalesBufferIngredientId IS NULL
   OR @SalesBufferBaseUnitId IS NULL
   OR @SalesBufferUnitCost IS NULL
   OR @SalesBufferUnitCost <= 0
BEGIN
    ;THROW 53500,
           N'DEMO_REORDER_V14: không resolve được ING00001/base-unit/current cost cho stock buffer.',
           1;
END;


/* ============================================================
   FIRST RUN
   ============================================================ */
IF @IsReplay = 0
BEGIN

    /* Không chấp nhận fixture partial. */
    IF EXISTS
    (
        SELECT 1
        FROM dbo.InventoryDocuments
        WHERE RequestKey IN
        (
            N'DEMO_REORDER_V14_SALES_BUFFER_S1_ING00001',
            N'DEMO_REORDER_V14_SALES_BUFFER_S3_ING00001'
        )
    )
    BEGIN
        ;THROW 53501,
              N'DEMO_REORDER_V14: stock-buffer document tồn tại khi operational fixture chưa tồn tại; fixture partial.',
              1;
    END;


    DECLARE
        @BufferStoreId int,
        @BufferStaffId int,
        @BufferStoreNo int,

        @BufferInventoryId int,
        @BufferDocId int,
        @BufferDetailId int,

        @BufferBefore decimal(18,3),

        @BufferRequestKey nvarchar(100),
        @BufferCode nvarchar(50);


    DECLARE sales_buffer_cursor
        CURSOR LOCAL FAST_FORWARD
    FOR
        SELECT
            StoreId,
            StaffId,
            StoreNo
        FROM @FixtureStores
        ORDER BY StoreNo;


    OPEN sales_buffer_cursor;

    FETCH NEXT
    FROM sales_buffer_cursor
    INTO
        @BufferStoreId,
        @BufferStaffId,
        @BufferStoreNo;


    WHILE @@FETCH_STATUS = 0
    BEGIN

        /* ====================================================
           Resolve StoreInventory ingredient thật.
           ==================================================== */
        SELECT
            @BufferInventoryId =
                si.StoreInventoryId,

            @BufferBefore =
                si.AvailableQty

        FROM dbo.StoreInventories si

        WHERE
            si.StoreId = @BufferStoreId

            AND si.IngredientId =
                @SalesBufferIngredientId

            AND si.RecipeId IS NULL
            AND si.PreparedItemId IS NULL;


        IF @BufferInventoryId IS NULL
           OR @BufferBefore < 0
        BEGIN
            ;THROW 53502,
                  N'DEMO_REORDER_V14: không resolve được StoreInventory ING00001 hợp lệ cho sales buffer.',
                  1;
        END;


        SET @BufferRequestKey =
            CONCAT(
                N'DEMO_REORDER_V14_SALES_BUFFER_S',
                @BufferStoreNo,
                N'_ING00001'
            );


        SET @BufferCode =
            CONCAT(
                N'DEMO_V14_BUF_S',
                @BufferStoreNo,
                N'_ING00001'
            );


        /* ====================================================
           1. InventoryDocument
           ==================================================== */
        INSERT dbo.InventoryDocuments
        (
            Code,
            StoreId,
            StaffId,
            DocumentDate,

            [Type],
            [Status],

            RequestKey,
            IsProcessing,

            ConfirmedAt,
            ConfirmedBy,

            Purpose,
            PartnerType,

            PartnerId,
            PartnerName,
            SupplierId,

            Note,

            AllowNegativeStock,
            NegativeReason,

            TotalAmount,
            VatAmount,
            FinalAmount
        )
        VALUES
        (
            @BufferCode,
            @BufferStoreId,
            @BufferStaffId,
            @SalesBufferAt,

            8,
            3,

            @BufferRequestKey,
            0,

            @SalesBufferAt,
            @BufferStaffId,

            3,
            0,

            NULL,
            NULL,
            NULL,

            N'DEMO_REORDER_V14 audited stock buffer for legacy ING00001 BOM demand',

            0,
            NULL,

            ROUND(
                @SalesBufferQty *
                @SalesBufferUnitCost,
                2
            ),

            0,

            ROUND(
                @SalesBufferQty *
                @SalesBufferUnitCost,
                2
            )
        );


        SET @BufferDocId =
            SCOPE_IDENTITY();


        /* ====================================================
           2. InventoryDocumentDetail
           ==================================================== */
        INSERT dbo.InventoryDocumentDetails
        (
            InventoryDocumentId,
            IngredientId,

            Quantity,
            BaseQuantity,

            UnitId,

            UnitPrice,
            CostPrice,
            CostAmount,

            Note,
            TotalAmount
        )
        VALUES
        (
            @BufferDocId,
            @SalesBufferIngredientId,

            @SalesBufferQty,
            @SalesBufferQty,

            @SalesBufferBaseUnitId,

            @SalesBufferUnitCost,
            @SalesBufferUnitCost,

            ROUND(
                @SalesBufferQty *
                @SalesBufferUnitCost,
                2
            ),

            CONCAT(
                N'DEMO_REORDER_V14_SALES_BUFFER_S',
                @BufferStoreNo,
                N'_ING00001'
            ),

            ROUND(
                @SalesBufferQty *
                @SalesBufferUnitCost,
                2
            )
        );


        SET @BufferDetailId =
            SCOPE_IDENTITY();


        /* ====================================================
           3. InventoryTransaction ADJUSTMENT_IN
           ==================================================== */
        INSERT dbo.InventoryTransactions
        (
            StoreInventoryId,

            [Type],
            StockStatus,

            Quantity,

            BeforeQty,
            AfterQty,

            UnitCost,
            TotalCost,

            InventoryDocumentId,
            InventoryDocumentDetailId,

            InventoryTransferId,
            InventoryTransferDetailId,

            ReferenceOrderId,
            ProductionRunId,
            SourceRecipeId,

            InventoryConsolidationRunId,
            BranchReceiptLineId,

            OrderRefundId,

            CreatedAt
        )
        VALUES
        (
            @BufferInventoryId,

            8,
            5,

            @SalesBufferQty,

            @BufferBefore,

            @BufferBefore +
            @SalesBufferQty,

            @SalesBufferUnitCost,

            ROUND(
                @SalesBufferQty *
                @SalesBufferUnitCost,
                2
            ),

            @BufferDocId,
            @BufferDetailId,

            NULL,
            NULL,

            NULL,
            NULL,
            NULL,

            NULL,
            NULL,

            NULL,

            @SalesBufferAt
        );


        /* ====================================================
           4. FIFO CostLayer
           ==================================================== */
        INSERT dbo.InventoryCostLayers
        (
            IngredientId,
            PreparedItemId,

            StoreId,

            Quantity,
            RemainingQuantity,

            UnitCost,

            CreatedAt,

            SourceProductionRunId,
            SourceOrderRefundId,

            SourceInventoryDocumentDetailId,

            SourceBranchReceiptLineId,
            SourceTransferCostAllocationId,
            SourceTransferDiscrepancyPostingId
        )
        VALUES
        (
            @SalesBufferIngredientId,
            NULL,

            @BufferStoreId,

            @SalesBufferQty,
            @SalesBufferQty,

            @SalesBufferUnitCost,

            @SalesBufferAt,

            NULL,
            NULL,

            @BufferDetailId,

            NULL,
            NULL,
            NULL
        );


        /* ====================================================
           5. Cập nhật StoreInventory.
           Đây không phải update tồn vô căn cứ vì phía trên
           đã có Document + Detail + Transaction + CostLayer.
           ==================================================== */
        UPDATE dbo.StoreInventories
        SET
            AvailableQty =
                @BufferBefore +
                @SalesBufferQty,

            LastUpdated =
                @SalesBufferAt

        WHERE
            StoreInventoryId =
                @BufferInventoryId;


        FETCH NEXT
        FROM sales_buffer_cursor
        INTO
            @BufferStoreId,
            @BufferStaffId,
            @BufferStoreNo;

    END;


    CLOSE sales_buffer_cursor;
    DEALLOCATE sales_buffer_cursor;

END

/* ============================================================
   REPLAY
   Không cộng tồn lần hai.
   ============================================================ */
ELSE
BEGIN

    IF
    (
        SELECT COUNT(*)
        FROM dbo.InventoryDocuments
        WHERE RequestKey IN
        (
            N'DEMO_REORDER_V14_SALES_BUFFER_S1_ING00001',
            N'DEMO_REORDER_V14_SALES_BUFFER_S3_ING00001'
        )
    ) <> 2
    BEGIN
        ;THROW 53503,
              N'DEMO_REORDER_V14: replay thiếu đúng 2 stock-buffer documents ING00001.',
              1;
    END;


    /* ========================================================
       Kiểm tra payload có bị sửa khác contract không.
       ======================================================== */
    IF EXISTS
    (
        SELECT 1

        FROM dbo.InventoryDocuments h

        JOIN dbo.InventoryDocumentDetails d
            ON d.InventoryDocumentId =
               h.InventoryDocumentId

        JOIN dbo.InventoryTransactions t
            ON t.InventoryDocumentDetailId =
               d.InventoryDocumentDetailId

           AND t.InventoryDocumentId =
               h.InventoryDocumentId

           AND t.[Type] = 8

        JOIN dbo.StoreInventories si
            ON si.StoreInventoryId =
               t.StoreInventoryId

        LEFT JOIN dbo.InventoryCostLayers l
            ON l.SourceInventoryDocumentDetailId =
               d.InventoryDocumentDetailId

        WHERE
            h.RequestKey IN
            (
                N'DEMO_REORDER_V14_SALES_BUFFER_S1_ING00001',
                N'DEMO_REORDER_V14_SALES_BUFFER_S3_ING00001'
            )

            AND
            (
                   h.[Type] <> 8
                OR h.[Status] <> 3

                OR h.Purpose <> 3
                OR h.PartnerType <> 0

                OR h.IsProcessing <> 0
                OR h.AllowNegativeStock <> 0

                OR d.IngredientId <>
                   @SalesBufferIngredientId

                OR d.BaseQuantity <>
                   @SalesBufferQty

                OR d.Quantity <>
                   @SalesBufferQty

                OR d.UnitId <>
                   @SalesBufferBaseUnitId

                OR d.CostPrice <>
                   @SalesBufferUnitCost

                OR t.Quantity <>
                   @SalesBufferQty

                OR ABS(
                    (t.AfterQty - t.BeforeQty)
                    - t.Quantity
                ) > 0.001

                OR si.StoreId <>
                   h.StoreId

                OR si.IngredientId <>
                   @SalesBufferIngredientId

                OR l.InventoryCostLayerId IS NULL

                OR l.StoreId <>
                   h.StoreId

                OR l.IngredientId <>
                   @SalesBufferIngredientId

                OR l.PreparedItemId IS NOT NULL

                OR l.Quantity <>
                   @SalesBufferQty

                OR l.RemainingQuantity < 0

                OR l.RemainingQuantity >
                   l.Quantity

                OR l.UnitCost <>
                   @SalesBufferUnitCost
            )
    )
    BEGIN
        ;THROW 53504,
              N'DEMO_REORDER_V14: replay stock-buffer ING00001 payload drift.',
              1;
    END;


    /* ========================================================
       Replay chỉ cập nhật thời gian fixture.
       TUYỆT ĐỐI không cộng AvailableQty thêm lần nữa.
       ======================================================== */
    UPDATE dbo.InventoryDocuments
    SET
        DocumentDate =
            @SalesBufferAt,

        ConfirmedAt =
            @SalesBufferAt

    WHERE RequestKey IN
    (
        N'DEMO_REORDER_V14_SALES_BUFFER_S1_ING00001',
        N'DEMO_REORDER_V14_SALES_BUFFER_S3_ING00001'
    );


    UPDATE t
    SET
        t.CreatedAt =
            @SalesBufferAt

    FROM dbo.InventoryTransactions t

    JOIN dbo.InventoryDocuments h
        ON h.InventoryDocumentId =
           t.InventoryDocumentId

    WHERE
        h.RequestKey IN
        (
            N'DEMO_REORDER_V14_SALES_BUFFER_S1_ING00001',
            N'DEMO_REORDER_V14_SALES_BUFFER_S3_ING00001'
        )

        AND t.[Type] = 8;


    UPDATE l
    SET
        l.CreatedAt =
            @SalesBufferAt

    FROM dbo.InventoryCostLayers l

    JOIN dbo.InventoryDocumentDetails d
        ON d.InventoryDocumentDetailId =
           l.SourceInventoryDocumentDetailId

    JOIN dbo.InventoryDocuments h
        ON h.InventoryDocumentId =
           d.InventoryDocumentId

    WHERE
        h.RequestKey IN
        (
            N'DEMO_REORDER_V14_SALES_BUFFER_S1_ING00001',
            N'DEMO_REORDER_V14_SALES_BUFFER_S3_ING00001'
        );

END;

    DECLARE @ShiftSeed TABLE(StoreId int,Seq int,StaffId int,Marker nvarchar(100),StartAt datetime2(0),EndAt datetime2(0),PRIMARY KEY(StoreId,Seq));
    ;WITH n AS(SELECT 1 Seq UNION ALL SELECT Seq+1 FROM n WHERE Seq<30)
    INSERT @ShiftSeed
    SELECT fs.StoreId,n.Seq,fs.StaffId,
           CONCAT(N'DEMO_REORDER_V14_SHIFT_S',fs.StoreNo,N'_',RIGHT(CONCAT(N'000',n.Seq),3)),
           DATEADD(HOUR,CASE WHEN n.Seq%2=1 THEN 7 ELSE 15 END,DATEADD(DAY,-15+((n.Seq-1)/2),@SeedDayUtc)),
           DATEADD(HOUR,CASE WHEN n.Seq%2=1 THEN 15 ELSE 23 END,DATEADD(DAY,-15+((n.Seq-1)/2),@SeedDayUtc))
    FROM @FixtureStores fs CROSS JOIN n OPTION(MAXRECURSION 30);

    DECLARE @PreparedRecipeOrder TABLE(RecipeRank int PRIMARY KEY,RecipeCode nvarchar(100) UNIQUE);
    INSERT @PreparedRecipeOrder VALUES
    (1,N'DEMO_RECIPE_PREP_VIET_COFFEE'),(2,N'DEMO_RECIPE_PREP_ESPRESSO'),
    (3,N'DEMO_RECIPE_PREP_BLACK_TEA'),(4,N'DEMO_RECIPE_PREP_OOLONG_TEA'),
    (5,N'DEMO_RECIPE_PREP_SUGAR_SYRUP'),(6,N'DEMO_RECIPE_PREP_SALTED_CREAM'),
    (7,N'DEMO_RECIPE_PREP_CHEESE_CREAM'),(8,N'DEMO_RECIPE_PREP_BLACK_PEARL'),
    (9,N'DEMO_RECIPE_PREP_ALOE_BASE'),(10,N'DEMO_RECIPE_PREP_COCONUT_JELLY_BASE'),
    (11,N'DEMO_RECIPE_PREP_KHUC_BACH_BASE');

    IF EXISTS(SELECT 1 FROM @PreparedRecipeOrder x LEFT JOIN dbo.Recipes r ON r.RecipeCode=x.RecipeCode AND r.Active=1 AND r.Status=N'Active' WHERE r.RecipeId IS NULL OR r.PreparedItemId IS NULL OR r.OutputQuantity<=0 OR r.OutputUnitId IS NULL)
        THROW 53427,N'DEMO_REORDER_V14: thiếu active PreparedItem recipe/output identity.',1;

    DECLARE @ProdSeed TABLE(StoreId int,Seq int,StaffId int,RecipeId int,RequestKey uniqueidentifier,RequestFingerprint varchar(64),Notes nvarchar(200),RunAt datetime2(0),PRIMARY KEY(StoreId,Seq));
    ;WITH n AS(SELECT 1 Seq UNION ALL SELECT Seq+1 FROM n WHERE Seq<30)
    INSERT @ProdSeed
    SELECT fs.StoreId,n.Seq,fs.StaffId,r.RecipeId,
           CONVERT(uniqueidentifier,CONCAT(CASE WHEN fs.StoreNo=1 THEN N'e141' ELSE N'e143' END,N'0000-0000-4000-8000-',RIGHT(CONCAT(N'000000000000',n.Seq),12))),
           CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(N'DEMO_REORDER_V14|PROD|S',fs.StoreNo,N'|',r.RecipeCode,N'|1')),2),
           CONCAT(N'DEMO_REORDER_V14_PROD_S',fs.StoreNo,N'_',RIGHT(CONCAT(N'000',n.Seq),3)),
           DATEADD(MINUTE,360+n.Seq,DATEADD(DAY,-29+((n.Seq-1)/3),@SeedDayUtc))
    FROM @FixtureStores fs CROSS JOIN n
    JOIN @PreparedRecipeOrder pro ON pro.RecipeRank=((n.Seq-1)%11)+1
    JOIN dbo.Recipes r ON r.RecipeCode=pro.RecipeCode AND r.Active=1 AND r.Status=N'Active'
    OPTION(MAXRECURSION 30);

    DECLARE @OrderSeed TABLE(StoreId int,Seq int,StaffId int,StoreNo int,ClientOrderId uniqueidentifier,ShiftSeq int,CreatedAt datetime2(0),PRIMARY KEY(StoreId,Seq));
    ;WITH n AS(SELECT 1 Seq UNION ALL SELECT Seq+1 FROM n WHERE Seq<50)
    INSERT @OrderSeed
    SELECT fs.StoreId,n.Seq,fs.StaffId,fs.StoreNo,
           CONVERT(uniqueidentifier,CONCAT(CASE WHEN fs.StoreNo=1 THEN N'd141' ELSE N'd143' END,N'0000-0000-4000-8000-',RIGHT(CONCAT(N'000000000000',n.Seq),12))),
           1+((n.Seq-1)%30),
           DATEADD(MINUTE,30+((n.Seq-1)%120),sh.StartAt)
    FROM @FixtureStores fs CROSS JOIN n
    JOIN @ShiftSeed sh ON sh.StoreId=fs.StoreId AND sh.Seq=1+((n.Seq-1)%30)
    OPTION(MAXRECURSION 50);

    IF EXISTS(SELECT 1 FROM @ShiftSeed WHERE StartAt<@WindowStartUtc OR EndAt>@SeedAnchorUtc)
    OR EXISTS(SELECT 1 FROM @ProdSeed WHERE RunAt<@WindowStartUtc OR RunAt>@SeedAnchorUtc)
    OR EXISTS(SELECT 1 FROM @OrderSeed WHERE CreatedAt<@WindowStartUtc OR CreatedAt>@SeedAnchorUtc)
        THROW 53483,N'DEMO_REORDER_V14: fixture timestamp nằm ngoài rolling 30 days hoặc trong tương lai.',1;

    IF (SELECT MAX(RunAt) FROM @ProdSeed)>=(SELECT MIN(CreatedAt) FROM @OrderSeed)
        THROW 53494,N'DEMO_REORDER_V14: production timeline phải kết thúc trước POS timeline để BeforeQty/AfterQty phản ánh đúng ledger chronology.',1;

    /* Existing fixture business-key payload must match before any replay timestamp update. */
    IF @IsReplay=1
    BEGIN
        IF EXISTS(
            SELECT 1 FROM @ShiftSeed x
            LEFT JOIN dbo.WorkShifts ws ON ws.DiscrepancyReason=x.Marker
            WHERE ws.ShiftId IS NULL OR ws.StoreId<>x.StoreId OR ws.UserId<>x.StaffId OR ws.Status<>N'CLOSED'
               OR ws.StartingCash<>500000 OR ws.ExpectedEndingCash<>500000 OR ws.ActualEndingCash<>500000
               OR ws.CashDiscrepancy<>0 OR ws.IsExceptionClosed<>0 OR ws.RequiresReconciliation<>0 OR ws.HasLateOfflineSync<>0
        ) THROW 53429,N'DEMO_REORDER_V14: WorkShift payload drift.',1;

        IF EXISTS(
            SELECT 1 FROM @ProdSeed x
            LEFT JOIN dbo.ProductionRuns pr ON pr.StoreId=x.StoreId AND pr.RequestKey=x.RequestKey
            WHERE pr.ProductionRunId IS NULL OR pr.RecipeId<>x.RecipeId OR pr.RequestedRunCount<>1
               OR pr.RequestFingerprint<>x.RequestFingerprint OR pr.Notes<>x.Notes OR pr.CreatedByStaffId<>x.StaffId
               OR pr.CompletedByStaffId<>x.StaffId OR pr.Status<>2 OR pr.ValuationStatus<>1
               OR pr.TotalInputCost IS NULL OR pr.OutputUnitCost IS NULL
        ) THROW 53430,N'DEMO_REORDER_V14: ProductionRun payload drift.',1;

        IF EXISTS(
            SELECT 1 FROM @OrderSeed x
            LEFT JOIN dbo.Orders o ON o.ClientOrderId=x.ClientOrderId
            LEFT JOIN @ShiftSeed sh ON sh.StoreId=x.StoreId AND sh.Seq=x.ShiftSeq
            LEFT JOIN dbo.WorkShifts ws ON ws.DiscrepancyReason=sh.Marker
            WHERE o.OrderId IS NULL OR o.StoreId<>x.StoreId OR o.OrderStatusId<>@CompletedOrderStatusId
               OR o.PaymentStatusId<>@PaidStatusId OR o.OrderTypeId<>@TakeAwayTypeId OR o.StaffId<>x.StaffId
               OR o.WorkShiftId<>ws.ShiftId OR o.Source<>@SeedMarker
               OR o.Note<>CONCAT(N'DEMO_REORDER_V14_ORDER_S',x.StoreNo,N'_',RIGHT(CONCAT(N'000',x.Seq),3))
               OR o.CustomerId IS NOT NULL OR o.TableId IS NOT NULL OR o.RecommendationSessionId IS NOT NULL
               OR o.ShippingFee<>0 OR o.VoucherDiscount<>0 OR o.PointDiscount<>0 OR o.PointsUsed<>0
               OR o.CostStatus NOT IN(1,2)
        ) THROW 53431,N'DEMO_REORDER_V14: Order payload drift.',1;

        IF EXISTS(
            SELECT 1 FROM dbo.Payments p JOIN dbo.Orders o ON o.OrderId=p.OrderId AND o.Source=@SeedMarker
            WHERE p.PaymentStatusId<>@PaidStatusId OR p.PaymentMethodId<>@BankMethodId OR p.Amount<>o.Total
               OR p.TransactionCode NOT LIKE N'DEMO_REORDER_V14_PAY_S%'
        ) THROW 53432,N'DEMO_REORDER_V14: Payment payload drift.',1;

        IF EXISTS(
            SELECT 1 FROM dbo.InventoryTransactions t
            JOIN dbo.Orders o ON o.OrderId=t.ReferenceOrderId AND o.Source=@SeedMarker
            WHERE t.[Type]<>7 OR t.Quantity<=0 OR ABS((t.BeforeQty-t.AfterQty)-t.Quantity)>0.001
               OR t.AfterQty<0 OR t.SourceRecipeId IS NULL OR t.UnitCost IS NULL OR t.TotalCost IS NULL
        ) OR EXISTS(
            SELECT 1 FROM dbo.InventoryTransactions t
            JOIN dbo.Orders o ON o.OrderId=t.ReferenceOrderId AND o.Source=@SeedMarker
            LEFT JOIN dbo.SalesCostAllocations a ON a.InventoryTransactionId=t.InventoryTransactionId
            WHERE t.[Type]=7
            GROUP BY t.InventoryTransactionId,t.Quantity,t.TotalCost
            HAVING ABS(SUM(ISNULL(a.Quantity,0))-t.Quantity)>0.001
                OR ABS(SUM(ISNULL(a.TotalCost,0))-t.TotalCost)>0.01
        ) THROW 53433,N'DEMO_REORDER_V14: Sales transaction/FIFO allocation payload drift.',1;

        IF EXISTS(
            SELECT 1 FROM dbo.InventoryTransactions t
            JOIN dbo.ProductionRuns pr ON pr.ProductionRunId=t.ProductionRunId AND pr.Notes LIKE N'DEMO_REORDER_V14_PROD_S%'
            LEFT JOIN dbo.ProductionCostAllocations a ON a.InventoryTransactionId=t.InventoryTransactionId
            WHERE t.[Type]=6
            GROUP BY t.InventoryTransactionId,t.Quantity
            HAVING ABS(SUM(ISNULL(a.Quantity,0))-t.Quantity)>0.001
        ) THROW 53434,N'DEMO_REORDER_V14: Production FIFO allocation payload drift.',1;
    END;

    /* ------------------------------------------------------------
       14.6 WorkShifts (PK is ShiftId; Orders.WorkShiftId is the FK)
       ------------------------------------------------------------ */
    IF @IsReplay=0
    BEGIN
        INSERT dbo.WorkShifts(StoreId,UserId,StartTimeUtc,EndTimeUtc,BusinessDate,OpenContext,CloseType,ExpiryWarningLevel,StartingCash,ExpectedEndingCash,ActualEndingCash,
                              CashDiscrepancy,[Status],DiscrepancyReason,IsExceptionClosed,ExceptionCloseReason,
                              ExceptionClosedByStaffId,ExceptionClosedAt,OfflineOrderCountAtClose,OfflineEstimatedTotalAtClose,
                              OfflineCashTotalAtClose,RequiresReconciliation,HasLateOfflineSync,LateOfflineSyncCount,
                              LastLateOfflineSyncedAtUtc,PosTerminalId)
        SELECT StoreId,StaffId,StartAt,EndAt,CONVERT(date,DATEADD(HOUR,7,StartAt)),N'LEGACY',N'NORMAL',0,500000,500000,500000,0,N'CLOSED',Marker,0,NULL,NULL,NULL,0,0,0,0,0,0,NULL,NULL
        FROM @ShiftSeed;
    END
    ELSE
    BEGIN
        UPDATE ws SET ws.StartTimeUtc=x.StartAt,ws.EndTimeUtc=x.EndAt,
                      ws.BusinessDate=CONVERT(date,DATEADD(HOUR,7,x.StartAt)),ws.OpenContext=N'LEGACY',
                      ws.CloseType=N'NORMAL',ws.Status=N'CLOSED'
        FROM dbo.WorkShifts ws JOIN @ShiftSeed x ON x.Marker=ws.DiscrepancyReason;
    END;

    /* ------------------------------------------------------------
       14.7 Production fixtures. First run applies FIFO once.
       Each run uses cumulative supply intervals over remaining layers.
       ------------------------------------------------------------ */
    IF @IsReplay=0
    BEGIN
        DECLARE @RunDemand TABLE(
            DemandId int IDENTITY(1,1) PRIMARY KEY,StoreInventoryId int,IngredientId int NULL,PreparedItemId int NULL,
            SourceRecipeId int,Quantity decimal(18,3));
        DECLARE @AggRunDemand TABLE(StoreInventoryId int PRIMARY KEY,IngredientId int NULL,PreparedItemId int NULL,SourceRecipeId int,Quantity decimal(18,3));
        DECLARE @CurrentProdStoreId int,@CurrentProdSeq int,@CurrentProdStaffId int,@CurrentProdRecipeId int,
                @CurrentProdRequestKey uniqueidentifier,@CurrentProdFingerprint varchar(64),@CurrentProdNotes nvarchar(200),@CurrentProdAt datetime2(0),
                @CurrentRunId int,@RunInputCost decimal(18,2),@OutputQty decimal(18,3),@OutputUnitCost decimal(18,8),
                @OutputPreparedItemId int,@OutputUnitId int,@PreparedBaseUnitId int,@OutputInventoryId int,@OutputBefore decimal(18,3),@OutputMin decimal(18,3);

        DECLARE prod_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT StoreId,Seq,StaffId,RecipeId,RequestKey,RequestFingerprint,Notes,RunAt FROM @ProdSeed ORDER BY RunAt,StoreId;
        OPEN prod_cursor;
        FETCH NEXT FROM prod_cursor INTO @CurrentProdStoreId,@CurrentProdSeq,@CurrentProdStaffId,@CurrentProdRecipeId,@CurrentProdRequestKey,@CurrentProdFingerprint,@CurrentProdNotes,@CurrentProdAt;
        WHILE @@FETCH_STATUS=0
        BEGIN
            INSERT dbo.ProductionRuns(StoreId,RecipeId,RequestedRunCount,RequestKey,RequestFingerprint,[Status],Notes,
                                      CreatedByStaffId,CreatedAt,ConfirmedAt,CompletedAt,CompletedByStaffId,ValuationStatus,
                                      TotalInputCost,OutputUnitCost,ValuedAtUtc)
            VALUES(@CurrentProdStoreId,@CurrentProdRecipeId,1,@CurrentProdRequestKey,@CurrentProdFingerprint,1,@CurrentProdNotes,
                   @CurrentProdStaffId,@CurrentProdAt,@CurrentProdAt,NULL,NULL,0,NULL,NULL,NULL);
            SET @CurrentRunId=SCOPE_IDENTITY();

            DELETE FROM @RunDemand;

            /* Direct ingredient requirements: recipe qty -> ingredient base unit exactly through UnitConversions. */
            INSERT @RunDemand(StoreInventoryId,IngredientId,PreparedItemId,SourceRecipeId,Quantity)
            SELECT si.StoreInventoryId,rd.IngredientId,NULL,@CurrentProdRecipeId,
                   CONVERT(decimal(18,3),ROUND(rd.Quantity *
                     CASE WHEN rd.UnitId=i.BaseUnitId THEN 1
                          ELSE uc.ToQuantity/NULLIF(uc.FromQuantity,0) END,3))
            FROM dbo.RecipeDetails rd
            JOIN dbo.Ingredients i ON i.IngredientId=rd.IngredientId
            JOIN dbo.StoreInventories si ON si.StoreId=@CurrentProdStoreId AND si.IngredientId=i.IngredientId
            LEFT JOIN dbo.UnitConversions uc ON uc.IngredientId=i.IngredientId AND uc.FromUnitId=rd.UnitId
                 AND uc.ToUnitId=i.BaseUnitId AND uc.Active=1
            WHERE rd.RecipeId=@CurrentProdRecipeId AND rd.IngredientId IS NOT NULL
              AND (rd.UnitId=i.BaseUnitId OR uc.UnitConversionId IS NOT NULL);

            IF EXISTS(
                SELECT 1 FROM dbo.RecipeDetails rd JOIN dbo.Ingredients i ON i.IngredientId=rd.IngredientId
                WHERE rd.RecipeId=@CurrentProdRecipeId AND rd.IngredientId IS NOT NULL AND rd.UnitId<>i.BaseUnitId
                  AND NOT EXISTS(SELECT 1 FROM dbo.UnitConversions uc WHERE uc.IngredientId=i.IngredientId
                                 AND uc.FromUnitId=rd.UnitId AND uc.ToUnitId=i.BaseUnitId AND uc.Active=1 AND uc.FromQuantity>0 AND uc.ToQuantity>0)
            ) THROW 53435,N'DEMO_REORDER_V14: thiếu UnitConversion cho production recipe detail.',1;

            /* Child PreparedItem requirement keeps the exact RecipeId+PreparedItem identity. */
            INSERT @RunDemand(StoreInventoryId,IngredientId,PreparedItemId,SourceRecipeId,Quantity)
            SELECT si.StoreInventoryId,NULL,cr.PreparedItemId,cr.RecipeId,CONVERT(decimal(18,3),rd.Quantity)
            FROM dbo.RecipeDetails rd
            JOIN dbo.Recipes cr ON cr.RecipeId=rd.ChildRecipeId AND cr.PreparedItemId IS NOT NULL
            JOIN dbo.PreparedItems p ON p.PreparedItemId=cr.PreparedItemId AND p.Active=1
            JOIN dbo.StoreInventories si ON si.StoreId=@CurrentProdStoreId AND si.RecipeId=cr.RecipeId
                 AND si.PreparedItemId=cr.PreparedItemId AND si.BtpIdentityState=1
            WHERE rd.RecipeId=@CurrentProdRecipeId AND rd.ChildRecipeId IS NOT NULL AND rd.UnitId=p.BaseUnitId;

            IF EXISTS(
                SELECT 1 FROM dbo.RecipeDetails rd
                JOIN dbo.Recipes cr ON cr.RecipeId=rd.ChildRecipeId
                LEFT JOIN dbo.PreparedItems p ON p.PreparedItemId=cr.PreparedItemId
                LEFT JOIN dbo.StoreInventories si ON si.StoreId=@CurrentProdStoreId AND si.RecipeId=cr.RecipeId AND si.PreparedItemId=cr.PreparedItemId AND si.BtpIdentityState=1
                WHERE rd.RecipeId=@CurrentProdRecipeId AND rd.ChildRecipeId IS NOT NULL
                  AND (cr.PreparedItemId IS NULL OR p.PreparedItemId IS NULL OR rd.UnitId<>p.BaseUnitId OR si.StoreInventoryId IS NULL)
            ) THROW 53436,N'DEMO_REORDER_V14: child production BTP không có canonical Recipe+PreparedItem cost identity; không tự chuyển writer mode.',1;

            IF NOT EXISTS(SELECT 1 FROM @RunDemand) THROW 53437,N'DEMO_REORDER_V14: ProductionRun không có BOM input.',1;

            /* Aggregate same stock identity inside one run to satisfy unique Production transaction index. */
            DELETE FROM @AggRunDemand;
            INSERT @AggRunDemand
            SELECT StoreInventoryId,MAX(IngredientId),MAX(PreparedItemId),MIN(SourceRecipeId),SUM(Quantity)
            FROM @RunDemand GROUP BY StoreInventoryId;

            IF EXISTS(SELECT 1 FROM @AggRunDemand d JOIN dbo.StoreInventories si ON si.StoreInventoryId=d.StoreInventoryId WHERE d.Quantity<=0 OR si.AvailableQty<d.Quantity)
                THROW 53438,N'DEMO_REORDER_V14: production demand vượt tồn khả dụng.',1;

            IF EXISTS(
                SELECT 1 FROM @AggRunDemand d
                OUTER APPLY(SELECT SUM(l.RemainingQuantity) Qty FROM dbo.InventoryCostLayers l
                            WHERE l.StoreId=@CurrentProdStoreId AND l.RemainingQuantity>0
                              AND ((d.IngredientId IS NOT NULL AND l.IngredientId=d.IngredientId AND l.PreparedItemId IS NULL)
                                OR (d.PreparedItemId IS NOT NULL AND l.PreparedItemId=d.PreparedItemId AND l.IngredientId IS NULL))) s
                WHERE ISNULL(s.Qty,0)<d.Quantity
            ) THROW 53439,N'DEMO_REORDER_V14: production demand thiếu FIFO layer; không tạo cost gap giả.',1;

            INSERT dbo.InventoryTransactions(StoreInventoryId,[Type],StockStatus,Quantity,BeforeQty,AfterQty,UnitCost,TotalCost,
                                              InventoryDocumentId,InventoryDocumentDetailId,InventoryTransferId,InventoryTransferDetailId,
                                              ReferenceOrderId,ProductionRunId,SourceRecipeId,InventoryConsolidationRunId,BranchReceiptLineId,
                                              OrderRefundId,CreatedAt)
            SELECT d.StoreInventoryId,6,CASE WHEN si.AvailableQty-d.Quantity<=ISNULL(si.MinStockLevel,-1) THEN 2 ELSE 1 END,
                   d.Quantity,si.AvailableQty,si.AvailableQty-d.Quantity,NULL,NULL,NULL,NULL,NULL,NULL,NULL,@CurrentRunId,d.SourceRecipeId,NULL,NULL,NULL,@CurrentProdAt
            FROM @AggRunDemand d JOIN dbo.StoreInventories si ON si.StoreInventoryId=d.StoreInventoryId;

            /* FIFO overlap = [demandStart,demandEnd] intersect [supplyStart,supplyEnd].
               For one run demandStart=0 for each stock identity; supply remains cumulative. */
            ;WITH Demand AS(
                SELECT d.StoreInventoryId,d.IngredientId,d.PreparedItemId,d.Quantity,
                       t.InventoryTransactionId,CAST(0 AS decimal(38,6)) DemandStart,CAST(d.Quantity AS decimal(38,6)) DemandEnd
                FROM @AggRunDemand d
                JOIN dbo.InventoryTransactions t ON t.ProductionRunId=@CurrentRunId AND t.StoreInventoryId=d.StoreInventoryId AND t.[Type]=6
            ),Supply0 AS(
                SELECT d.StoreInventoryId,l.InventoryCostLayerId,l.RemainingQuantity,l.UnitCost,l.CreatedAt,
                       SUM(CONVERT(decimal(38,6),l.RemainingQuantity)) OVER(PARTITION BY d.StoreInventoryId ORDER BY l.CreatedAt,l.InventoryCostLayerId ROWS UNBOUNDED PRECEDING) SupplyEnd
                FROM Demand d
                JOIN dbo.InventoryCostLayers l ON l.StoreId=@CurrentProdStoreId AND l.RemainingQuantity>0
                  AND ((d.IngredientId IS NOT NULL AND l.IngredientId=d.IngredientId AND l.PreparedItemId IS NULL)
                    OR (d.PreparedItemId IS NOT NULL AND l.PreparedItemId=d.PreparedItemId AND l.IngredientId IS NULL))
            ),Supply AS(
                SELECT *,SupplyEnd-CONVERT(decimal(38,6),RemainingQuantity) SupplyStart FROM Supply0
            ),Slices AS(
                SELECT d.InventoryTransactionId,s.InventoryCostLayerId,s.UnitCost,
                       CONVERT(decimal(18,3),
                         CASE WHEN d.DemandEnd<s.SupplyEnd THEN d.DemandEnd ELSE s.SupplyEnd END
                         -CASE WHEN d.DemandStart>s.SupplyStart THEN d.DemandStart ELSE s.SupplyStart END) Qty
                FROM Demand d JOIN Supply s ON s.StoreInventoryId=d.StoreInventoryId
                WHERE d.DemandEnd>s.SupplyStart AND s.SupplyEnd>d.DemandStart
            )
            INSERT dbo.ProductionCostAllocations(ProductionRunId,InventoryTransactionId,InventoryCostLayerId,Quantity,UnitCost,TotalCost,CreatedAtUtc)
            SELECT @CurrentRunId,InventoryTransactionId,InventoryCostLayerId,Qty,UnitCost,ROUND(Qty*UnitCost,2),@CurrentProdAt
            FROM Slices WHERE Qty>0;

            IF EXISTS(
                SELECT t.InventoryTransactionId,t.Quantity,SUM(ISNULL(a.Quantity,0)) AllocQty
                FROM dbo.InventoryTransactions t LEFT JOIN dbo.ProductionCostAllocations a ON a.InventoryTransactionId=t.InventoryTransactionId
                WHERE t.ProductionRunId=@CurrentRunId AND t.[Type]=6
                GROUP BY t.InventoryTransactionId,t.Quantity HAVING ABS(t.Quantity-SUM(ISNULL(a.Quantity,0)))>0.001
            ) THROW 53440,N'DEMO_REORDER_V14: production FIFO allocation không phủ đủ demand.',1;

            UPDATE l SET l.RemainingQuantity=l.RemainingQuantity-x.Qty
            FROM dbo.InventoryCostLayers l
            JOIN(SELECT InventoryCostLayerId,SUM(Quantity) Qty FROM dbo.ProductionCostAllocations WHERE ProductionRunId=@CurrentRunId GROUP BY InventoryCostLayerId)x
              ON x.InventoryCostLayerId=l.InventoryCostLayerId;

            UPDATE si SET si.AvailableQty=si.AvailableQty-d.Quantity,si.LastUpdated=@CurrentProdAt
            FROM dbo.StoreInventories si JOIN @AggRunDemand d ON d.StoreInventoryId=si.StoreInventoryId;

            UPDATE t SET t.UnitCost=x.UnitCost,t.TotalCost=x.TotalCost
            FROM dbo.InventoryTransactions t
            JOIN(SELECT InventoryTransactionId,ROUND(SUM(TotalCost)/NULLIF(SUM(Quantity),0),2) UnitCost,SUM(TotalCost) TotalCost
                 FROM dbo.ProductionCostAllocations WHERE ProductionRunId=@CurrentRunId GROUP BY InventoryTransactionId)x
              ON x.InventoryTransactionId=t.InventoryTransactionId;

            SELECT @RunInputCost=SUM(TotalCost) FROM dbo.ProductionCostAllocations WHERE ProductionRunId=@CurrentRunId;
            SELECT @OutputPreparedItemId=r.PreparedItemId,@OutputQty=CONVERT(decimal(18,3),r.OutputQuantity),@OutputUnitId=r.OutputUnitId
            FROM dbo.Recipes r WHERE r.RecipeId=@CurrentProdRecipeId;
            SELECT @PreparedBaseUnitId=BaseUnitId FROM dbo.PreparedItems WHERE PreparedItemId=@OutputPreparedItemId;
            IF @OutputPreparedItemId IS NULL OR @OutputQty<=0 OR @OutputUnitId<>@PreparedBaseUnitId
                THROW 53441,N'DEMO_REORDER_V14: production output không phải PreparedItem base-unit identity.',1;

            SELECT @OutputInventoryId=StoreInventoryId,@OutputBefore=AvailableQty,@OutputMin=MinStockLevel
            FROM dbo.StoreInventories
            WHERE StoreId=@CurrentProdStoreId AND RecipeId=@CurrentProdRecipeId AND PreparedItemId=@OutputPreparedItemId AND BtpIdentityState=1;
            IF @OutputInventoryId IS NULL THROW 53442,N'DEMO_REORDER_V14: thiếu canonical output StoreInventory cho PreparedItem.',1;

            SET @OutputUnitCost=CONVERT(decimal(18,8),@RunInputCost/NULLIF(@OutputQty,0));

            INSERT dbo.InventoryTransactions(StoreInventoryId,[Type],StockStatus,Quantity,BeforeQty,AfterQty,UnitCost,TotalCost,
                                              InventoryDocumentId,InventoryDocumentDetailId,InventoryTransferId,InventoryTransferDetailId,
                                              ReferenceOrderId,ProductionRunId,SourceRecipeId,InventoryConsolidationRunId,BranchReceiptLineId,
                                              OrderRefundId,CreatedAt)
            VALUES(@OutputInventoryId,5,CASE WHEN @OutputBefore+@OutputQty<=ISNULL(@OutputMin,-1) THEN 2 ELSE 1 END,
                   @OutputQty,@OutputBefore,@OutputBefore+@OutputQty,ROUND(@OutputUnitCost,2),@RunInputCost,
                   NULL,NULL,NULL,NULL,NULL,@CurrentRunId,@CurrentProdRecipeId,NULL,NULL,NULL,@CurrentProdAt);

            UPDATE dbo.StoreInventories SET AvailableQty=AvailableQty+@OutputQty,LastUpdated=@CurrentProdAt WHERE StoreInventoryId=@OutputInventoryId;

            INSERT dbo.InventoryCostLayers(IngredientId,PreparedItemId,StoreId,Quantity,RemainingQuantity,UnitCost,CreatedAt,
                                           SourceProductionRunId,SourceOrderRefundId,SourceInventoryDocumentDetailId,
                                           SourceBranchReceiptLineId,SourceTransferCostAllocationId,SourceTransferDiscrepancyPostingId)
            VALUES(NULL,@OutputPreparedItemId,@CurrentProdStoreId,@OutputQty,@OutputQty,ROUND(@OutputUnitCost,2),@CurrentProdAt,
                   @CurrentRunId,NULL,NULL,NULL,NULL,NULL);

            UPDATE dbo.ProductionRuns
            SET [Status]=2,CompletedAt=@CurrentProdAt,CompletedByStaffId=@CurrentProdStaffId,ValuationStatus=1,
                TotalInputCost=@RunInputCost,OutputUnitCost=@OutputUnitCost,ValuedAtUtc=@CurrentProdAt
            WHERE ProductionRunId=@CurrentRunId;

            IF EXISTS(SELECT 1 FROM dbo.InventoryCostLayers WHERE RemainingQuantity<0)
            OR EXISTS(SELECT 1 FROM dbo.StoreInventories WHERE AvailableQty<0)
                THROW 53443,N'DEMO_REORDER_V14: production làm tồn/layer âm.',1;

            FETCH NEXT FROM prod_cursor INTO @CurrentProdStoreId,@CurrentProdSeq,@CurrentProdStaffId,@CurrentProdRecipeId,@CurrentProdRequestKey,@CurrentProdFingerprint,@CurrentProdNotes,@CurrentProdAt;
        END;
        CLOSE prod_cursor; DEALLOCATE prod_cursor;
    END
    ELSE
    BEGIN
        UPDATE pr SET pr.CreatedAt=x.RunAt,pr.ConfirmedAt=x.RunAt,pr.CompletedAt=x.RunAt,pr.ValuedAtUtc=x.RunAt
        FROM dbo.ProductionRuns pr JOIN @ProdSeed x ON x.StoreId=pr.StoreId AND x.RequestKey=pr.RequestKey;

        UPDATE t SET t.CreatedAt=x.RunAt
        FROM dbo.InventoryTransactions t
        JOIN dbo.ProductionRuns pr ON pr.ProductionRunId=t.ProductionRunId
        JOIN @ProdSeed x ON x.StoreId=pr.StoreId AND x.RequestKey=pr.RequestKey;

        UPDATE a SET a.CreatedAtUtc=x.RunAt
        FROM dbo.ProductionCostAllocations a
        JOIN dbo.ProductionRuns pr ON pr.ProductionRunId=a.ProductionRunId
        JOIN @ProdSeed x ON x.StoreId=pr.StoreId AND x.RequestKey=pr.RequestKey;

        UPDATE l SET l.CreatedAt=x.RunAt
        FROM dbo.InventoryCostLayers l
        JOIN dbo.ProductionRuns pr ON pr.ProductionRunId=l.SourceProductionRunId
        JOIN @ProdSeed x ON x.StoreId=pr.StoreId AND x.RequestKey=pr.RequestKey;
    END;

    /* ------------------------------------------------------------
       14.8 POS orders/details/toppings/payments
       50 orders/store; all Store1 cloned menu SKUs are distributed across
       54 detail rows/store to maximize exact DrinkCode+SizeCode BOM coverage.
       ------------------------------------------------------------ */
    DECLARE @MenuSeed TABLE(StoreId int,MenuRank int,StoreMenuItemId int,DrinkSizeId int,DrinkId int,SizeId int,
                            DrinkName nvarchar(200),SizeName nvarchar(200),SellPrice decimal(18,2),BasePrice decimal(18,2),PRIMARY KEY(StoreId,MenuRank));
    ;WITH M AS(
        SELECT sm.StoreId,sm.StoreMenuItemId,sm.DrinkSizeId,ds.DrinkId,ds.SizeId,d.Name DrinkName,s.Name SizeName,
               CONVERT(decimal(18,2),COALESCE(sm.PriceOverride,ds.Price)) SellPrice,CONVERT(decimal(18,2),ds.Price) BasePrice,
               ROW_NUMBER() OVER(PARTITION BY sm.StoreId ORDER BY d.DrinkCode,s.SizeCode) rn
        FROM dbo.StoreMenuItems sm
        JOIN dbo.DrinkSizes ds ON ds.DrinkSizeId=sm.DrinkSizeId AND ds.Active=1
        JOIN dbo.Drinks d ON d.DrinkId=ds.DrinkId AND d.Active=1
        JOIN dbo.Sizes s ON s.SizeId=ds.SizeId AND s.Active=1
        WHERE sm.StoreId IN(@Store1Id,@Store3Id) AND sm.IsEnabled=1
          AND EXISTS(SELECT 1 FROM dbo.Recipes r WHERE r.DrinkId=ds.DrinkId AND r.SizeId=ds.SizeId AND r.Active=1 AND r.Status=N'Active')
    )
    INSERT @MenuSeed SELECT StoreId,rn,StoreMenuItemId,DrinkSizeId,DrinkId,SizeId,DrinkName,SizeName,SellPrice,BasePrice FROM M WHERE rn<=54;

    IF (SELECT COUNT(*) FROM @MenuSeed WHERE StoreId=@Store1Id)<>54 OR (SELECT COUNT(*) FROM @MenuSeed WHERE StoreId=@Store3Id)<>54
        THROW 53444,N'DEMO_REORDER_V14: cần đúng 54 enabled StoreMenuItems có exact active BOM ở mỗi Store.',1;

    /* ------------------------------------------------------------
   14.8A Ensure real size-level policy for the two legacy toppings
   whose BOM already exists but was unreachable by POS fixture.

   PM_VIEN -> DEMO_ING_CHEESE_CUBE
   KB_CM   -> DEMO_ING_KHUC_BACH_POWDER

   Chỉ sử dụng DrinkToppings compatibility đã tồn tại.
   Không tự tạo quan hệ DrinkTopping mới.
   ------------------------------------------------------------ */

    DECLARE @CheeseCubeToppingId int =
    (
        SELECT ToppingId
        FROM dbo.Toppings
        WHERE ToppingCode = N'PM_VIEN'  
          AND Active = 1
    );

    DECLARE @KhucBachToppingId int =
    (
        SELECT ToppingId
        FROM dbo.Toppings
        WHERE ToppingCode = N'KB_CM'
          AND Active = 1
    );


    /* ============================================================
       Tìm một DrinkSize thật nằm trong 54 menu fixture
       và đã có compatibility với CẢ HAI topping.

       Seed nền hiện tại có DrinkToppings cho PM_VIEN / KB_CM;
       không bịa compatibility mới.
       ============================================================ */
    DECLARE @CoverageDrinkSizeId int =
    (
        SELECT TOP(1)
            m.DrinkSizeId

        FROM @MenuSeed m

        WHERE
            m.StoreId = @Store1Id

            AND EXISTS
            (
                SELECT 1
                FROM dbo.DrinkToppings dt
                WHERE dt.DrinkId = m.DrinkId
                  AND dt.ToppingId = @CheeseCubeToppingId
            )

            AND EXISTS
            (
                SELECT 1
                FROM dbo.DrinkToppings dt
                WHERE dt.DrinkId = m.DrinkId
                  AND dt.ToppingId = @KhucBachToppingId
            )

        ORDER BY
            m.MenuRank
    );


    IF @CheeseCubeToppingId IS NULL
       OR @KhucBachToppingId IS NULL
       OR @CoverageDrinkSizeId IS NULL
    BEGIN
        ;THROW 53509,
               N'DEMO_REORDER_V14: không tìm được menu DrinkSize có compatibility thật cho PM_VIEN và KB_CM.',
               1;
    END;


    /* ============================================================
       Existing active policy must respect contract.
       ============================================================ */
    IF EXISTS
    (
        SELECT 1
        FROM dbo.DrinkSizeToppingPolicies p

        WHERE
            p.DrinkSizeId = @CoverageDrinkSizeId
            AND p.ToppingId IN
            (
                @CheeseCubeToppingId,
                @KhucBachToppingId
            )
            AND p.IsActive = 1

            AND
            (
                p.IsDefaultSelected <> 0
                OR p.IsRequired <> 0

                OR p.PriceTreatment
                    <> N'ADD_TOPPING_PRICE'

                OR p.CostTreatment
                    <> N'ADD_TOPPING_RECIPE_COST'

                OR p.QuantityPerDrink
                    <> CAST(1 AS decimal(18,5))
            )
    )
    BEGIN
        ;THROW 53510,
               N'DEMO_REORDER_V14: PM_VIEN/KB_CM policy tồn tại nhưng khác contract.',
               1;
    END;


    /* ============================================================
       PM_VIEN policy
       ============================================================ */
    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.DrinkSizeToppingPolicies p

        WHERE
            p.DrinkSizeId = @CoverageDrinkSizeId
            AND p.ToppingId = @CheeseCubeToppingId
            AND p.IsActive = 1
    )
    BEGIN

        INSERT dbo.DrinkSizeToppingPolicies
        (
            DrinkSizeId,
            ToppingId,

            IsDefaultSelected,
            IsRequired,

            PriceTreatment,
            CostTreatment,

            QuantityPerDrink,
            IsActive,

            CreatedByStaffId,
            UpdatedByStaffId,

            CreatedAtUtc,
            UpdatedAtUtc
        )
        VALUES
        (
            @CoverageDrinkSizeId,
            @CheeseCubeToppingId,

            0,
            0,

            N'ADD_TOPPING_PRICE',
            N'ADD_TOPPING_RECIPE_COST',

            1,
            1,

            @Store1StaffId,
            NULL,

            @SeedAnchorUtc,
            @SeedAnchorUtc
        );

    END;


    /* ============================================================
       KB_CM policy
       ============================================================ */
    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.DrinkSizeToppingPolicies p

        WHERE
            p.DrinkSizeId = @CoverageDrinkSizeId
            AND p.ToppingId = @KhucBachToppingId
            AND p.IsActive = 1
    )
    BEGIN

        INSERT dbo.DrinkSizeToppingPolicies
        (
            DrinkSizeId,
            ToppingId,

            IsDefaultSelected,
            IsRequired,

            PriceTreatment,
            CostTreatment,

            QuantityPerDrink,
            IsActive,

            CreatedByStaffId,
            UpdatedByStaffId,

            CreatedAtUtc,
            UpdatedAtUtc
        )
        VALUES
        (
            @CoverageDrinkSizeId,
            @KhucBachToppingId,

            0,
            0,

            N'ADD_TOPPING_PRICE',
            N'ADD_TOPPING_RECIPE_COST',

            1,
            1,

            @Store1StaffId,
            NULL,

            @SeedAnchorUtc,
            @SeedAnchorUtc
        );

    END;

    IF @IsReplay=1
    BEGIN
        /* 54 deterministic details per store, keyed by ClientOrderId + StoreMenuItem business identity. */
        IF (SELECT COUNT(*) FROM dbo.OrderDetails od JOIN dbo.Orders o ON o.OrderId=od.OrderId WHERE o.Source=@SeedMarker AND o.StoreId=@Store1Id)<>54
        OR (SELECT COUNT(*) FROM dbo.OrderDetails od JOIN dbo.Orders o ON o.OrderId=od.OrderId WHERE o.Source=@SeedMarker AND o.StoreId=@Store3Id)<>54
            THROW 53475,N'DEMO_REORDER_V14: OrderDetail fixture count drift.',1;

        IF EXISTS(
            SELECT 1
            FROM @MenuSeed m
            JOIN @OrderSeed os ON os.StoreId=m.StoreId AND os.Seq=CASE WHEN m.MenuRank<=50 THEN m.MenuRank ELSE m.MenuRank-50 END
            LEFT JOIN dbo.Orders o ON o.ClientOrderId=os.ClientOrderId
            LEFT JOIN dbo.OrderDetails od ON od.OrderId=o.OrderId AND od.StoreMenuItemId=m.StoreMenuItemId
            WHERE od.OrderDetailId IS NULL OR od.DrinkId<>m.DrinkId OR od.SizeId<>m.SizeId OR od.DrinkSizeId<>m.DrinkSizeId
               OR od.DrinkName<>m.DrinkName OR ISNULL(od.SizeName,N'')<>ISNULL(m.SizeName,N'') OR od.Price<>m.SellPrice
               OR ISNULL(od.AcceptedBasePrice,-1)<>m.BasePrice
               OR od.PriceSource<>CASE WHEN m.SellPrice=m.BasePrice THEN N'GLOBAL' ELSE N'STORE_OVERRIDE' END
               OR od.AcceptedCatalogVersion IS NOT NULL OR od.Quantity<>1 OR od.Note<>@SeedMarker OR od.CostStatus NOT IN(1,2)
        ) THROW 53476,N'DEMO_REORDER_V14: OrderDetail business-key payload drift.',1;

        IF EXISTS(
            SELECT 1
            FROM dbo.OrderDetails od
            JOIN dbo.Orders o ON o.OrderId=od.OrderId AND o.Source=@SeedMarker
            WHERE NOT EXISTS(
                SELECT 1 FROM @MenuSeed m
                JOIN @OrderSeed os ON os.StoreId=m.StoreId AND os.Seq=CASE WHEN m.MenuRank<=50 THEN m.MenuRank ELSE m.MenuRank-50 END
                WHERE os.ClientOrderId=o.ClientOrderId AND m.StoreMenuItemId=od.StoreMenuItemId)
        ) THROW 53477,N'DEMO_REORDER_V14: có OrderDetail ngoài deterministic menu fixture contract.',1;

        IF EXISTS(
            SELECT 1
            FROM dbo.OrderToppings ot
            JOIN dbo.OrderDetails od ON od.OrderDetailId=ot.OrderDetailId
            JOIN dbo.Orders o ON o.OrderId=od.OrderId AND o.Source=@SeedMarker
            LEFT JOIN dbo.Toppings tp ON tp.ToppingId=ot.ToppingId AND tp.Active=1
            LEFT JOIN dbo.StoreToppings st ON st.StoreId=o.StoreId AND st.ToppingId=ot.ToppingId AND st.Active=1
            LEFT JOIN dbo.DrinkSizeToppingPolicies pol ON pol.DrinkSizeId=od.DrinkSizeId AND pol.ToppingId=ot.ToppingId
                 AND pol.IsActive=1 AND pol.CostTreatment=N'ADD_TOPPING_RECIPE_COST'
            WHERE tp.ToppingId IS NULL OR st.StoreToppingId IS NULL OR pol.DrinkSizeToppingPolicyId IS NULL
               OR ot.ToppingName<>tp.Name OR ot.Price<>CONVERT(decimal(18,2),CASE WHEN pol.PriceTreatment=N'ADD_TOPPING_PRICE' THEN tp.Price ELSE 0 END)
               OR ot.CostStatus NOT IN(1,2)
        ) THROW 53478,N'DEMO_REORDER_V14: OrderTopping payload drift/policy không còn hợp lệ.',1;

        IF EXISTS(
            SELECT 1
            FROM @OrderSeed os
            LEFT JOIN dbo.Orders o ON o.ClientOrderId=os.ClientOrderId
            LEFT JOIN dbo.Payments p ON p.OrderId=o.OrderId
              AND p.TransactionCode=CONCAT(N'DEMO_REORDER_V14_PAY_S',os.StoreNo,N'_',RIGHT(CONCAT(N'000',os.Seq),3))
            WHERE p.PaymentId IS NULL OR p.PaymentStatusId<>@PaidStatusId OR p.PaymentMethodId<>@BankMethodId
               OR p.Amount<>o.Total OR p.CashSessionId IS NOT NULL
        ) THROW 53479,N'DEMO_REORDER_V14: payment transaction business-key payload drift.',1;
    END;

    IF @IsReplay=0
    BEGIN
        INSERT dbo.Orders(CustomerId,StoreId,OrderStatusId,PaymentStatusId,OrderTypeId,TableId,StaffId,WorkShiftId,
                          ClientOrderId,RecommendationSessionId,Source,Note,PaymentReference,ReceiverName,ReceiverPhone,
                          DeliveryAddress,ShippingFee,SubTotal,VoucherDiscount,PointDiscount,PointsUsed,Total,
                          CostStatus,TotalCogs,GrossProfit,CostedAtUtc,CreatedAt)
        SELECT NULL,o.StoreId,@CompletedOrderStatusId,@PaidStatusId,@TakeAwayTypeId,NULL,o.StaffId,ws.ShiftId,
               o.ClientOrderId,NULL,@SeedMarker,CONCAT(N'DEMO_REORDER_V14_ORDER_S',o.StoreNo,N'_',RIGHT(CONCAT(N'000',o.Seq),3)),
               NULL,NULL,NULL,NULL,0,0,0,0,0,0,0,NULL,NULL,NULL,o.CreatedAt
        FROM @OrderSeed o
        JOIN @ShiftSeed sh ON sh.StoreId=o.StoreId AND sh.Seq=o.ShiftSeq
        JOIN dbo.WorkShifts ws ON ws.DiscrepancyReason=sh.Marker;

        /* 54 detail rows per store: ranks 1-50 on orders 1-50; ranks 51-54 on orders 1-4. */
        INSERT dbo.OrderDetails(OrderId,DrinkId,SizeId,StoreMenuItemId,DrinkSizeId,DrinkName,SizeName,Price,
                                AcceptedBasePrice,PriceSource,AcceptedCatalogVersion,Quantity,Note,CostStatus,UnitCogs,TotalCogs)
        SELECT o.OrderId,m.DrinkId,m.SizeId,m.StoreMenuItemId,m.DrinkSizeId,m.DrinkName,m.SizeName,m.SellPrice,
               m.BasePrice,CASE WHEN m.SellPrice=m.BasePrice THEN N'GLOBAL' ELSE N'STORE_OVERRIDE' END,NULL,1,@SeedMarker,0,NULL,NULL
        FROM @MenuSeed m
        JOIN @OrderSeed os ON os.StoreId=m.StoreId AND os.Seq=CASE WHEN m.MenuRank<=50 THEN m.MenuRank ELSE m.MenuRank-50 END
        JOIN dbo.Orders o ON o.ClientOrderId=os.ClientOrderId;

        /* 30 topping BOMs/store, greedily preferring toppings that cover ingredients
           not already consumed by production/direct drink BOM. */
        DECLARE @EligibleTopping TABLE(
            StoreId int,ToppingId int,OrderDetailId int,ToppingName nvarchar(200),Price decimal(18,2),ToppingCode nvarchar(100),
            PRIMARY KEY(StoreId,ToppingId));
        ;WITH C AS(
            SELECT os.StoreId,p.ToppingId,od.OrderDetailId,t.Name,
                   CONVERT(decimal(18,2),CASE WHEN p.PriceTreatment=N'ADD_TOPPING_PRICE' THEN t.Price ELSE 0 END) Price,
                   t.ToppingCode,
                   ROW_NUMBER() OVER(PARTITION BY os.StoreId,p.ToppingId ORDER BY od.OrderDetailId) rn
            FROM dbo.Orders o
            JOIN @OrderSeed os ON os.ClientOrderId=o.ClientOrderId
            JOIN dbo.OrderDetails od ON od.OrderId=o.OrderId
            JOIN dbo.DrinkSizeToppingPolicies p ON p.DrinkSizeId=od.DrinkSizeId AND p.IsActive=1
                 AND p.CostTreatment=N'ADD_TOPPING_RECIPE_COST'
            JOIN dbo.StoreToppings st ON st.StoreId=o.StoreId AND st.ToppingId=p.ToppingId AND st.Active=1
            JOIN dbo.Toppings t ON t.ToppingId=p.ToppingId AND t.Active=1
            WHERE EXISTS(SELECT 1 FROM dbo.Recipes r WHERE r.ToppingId=t.ToppingId AND r.Active=1 AND r.Status=N'Active')
        )
        INSERT @EligibleTopping
        SELECT StoreId,ToppingId,OrderDetailId,Name,Price,ToppingCode FROM C WHERE rn=1;

        IF (SELECT COUNT(*) FROM @EligibleTopping WHERE StoreId=@Store1Id)<30
        OR (SELECT COUNT(*) FROM @EligibleTopping WHERE StoreId=@Store3Id)<30
            THROW 53445,N'DEMO_REORDER_V14: không có đủ 30 topping policy+BOM hợp lệ cho mỗi Store.',1;

        DECLARE @CoveredIngredient TABLE(StoreId int,IngredientId int,PRIMARY KEY(StoreId,IngredientId));
        /* Real production consumption already posted in 14.7. */
        INSERT @CoveredIngredient
        SELECT DISTINCT si.StoreId,si.IngredientId
        FROM dbo.InventoryTransactions t
        JOIN dbo.ProductionRuns pr ON pr.ProductionRunId=t.ProductionRunId AND pr.Notes LIKE N'DEMO_REORDER_V14_PROD_S%'
        JOIN dbo.StoreInventories si ON si.StoreInventoryId=t.StoreInventoryId
        WHERE t.[Type]=6 AND si.IngredientId IS NOT NULL;

        /* Direct ingredient requirements of the 54 selected drink BOMs. */
        INSERT @CoveredIngredient(StoreId,IngredientId)
        SELECT DISTINCT o.StoreId,rd.IngredientId
        FROM dbo.Orders o JOIN dbo.OrderDetails od ON od.OrderId=o.OrderId
        JOIN dbo.Recipes r ON r.DrinkId=od.DrinkId AND r.SizeId=od.SizeId AND r.Active=1 AND r.Status=N'Active'
        JOIN dbo.RecipeDetails rd ON rd.RecipeId=r.RecipeId AND rd.IngredientId IS NOT NULL
        WHERE o.Source=@SeedMarker
          AND NOT EXISTS(SELECT 1 FROM @CoveredIngredient c WHERE c.StoreId=o.StoreId AND c.IngredientId=rd.IngredientId);

        DECLARE @PickedTopping TABLE(StoreId int,ToppingId int,PickNo int,PRIMARY KEY(StoreId,ToppingId));
        DECLARE @PickStoreId int,@PickNo int,@PickToppingId int;
        DECLARE topping_store_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT StoreId FROM @FixtureStores ORDER BY StoreId;
        OPEN topping_store_cursor;
        FETCH NEXT FROM topping_store_cursor INTO @PickStoreId;
        WHILE @@FETCH_STATUS=0
        BEGIN
            SET @PickNo=1;
            WHILE @PickNo<=30
            BEGIN
                SET @PickToppingId=NULL;
                SELECT TOP(1) @PickToppingId=e.ToppingId
                FROM @EligibleTopping e
                OUTER APPLY(
                    SELECT COUNT(DISTINCT rd.IngredientId) NewIngredientCount
                    FROM dbo.Recipes r
                    JOIN dbo.RecipeDetails rd ON rd.RecipeId=r.RecipeId AND rd.IngredientId IS NOT NULL
                    WHERE r.ToppingId=e.ToppingId AND r.Active=1 AND r.Status=N'Active'
                      AND NOT EXISTS(SELECT 1 FROM @CoveredIngredient c WHERE c.StoreId=@PickStoreId AND c.IngredientId=rd.IngredientId)
                )score
                WHERE e.StoreId=@PickStoreId
                  AND NOT EXISTS(SELECT 1 FROM @PickedTopping p WHERE p.StoreId=e.StoreId AND p.ToppingId=e.ToppingId)
                ORDER BY ISNULL(score.NewIngredientCount,0) DESC,e.ToppingCode;

                IF @PickToppingId IS NULL THROW 53469,N'DEMO_REORDER_V14: không thể chọn đủ topping fixture.',1;
                INSERT @PickedTopping VALUES(@PickStoreId,@PickToppingId,@PickNo);

                INSERT @CoveredIngredient(StoreId,IngredientId)
                SELECT DISTINCT @PickStoreId,rd.IngredientId
                FROM dbo.Recipes r JOIN dbo.RecipeDetails rd ON rd.RecipeId=r.RecipeId AND rd.IngredientId IS NOT NULL
                WHERE r.ToppingId=@PickToppingId AND r.Active=1 AND r.Status=N'Active'
                  AND NOT EXISTS(SELECT 1 FROM @CoveredIngredient c WHERE c.StoreId=@PickStoreId AND c.IngredientId=rd.IngredientId);
                SET @PickNo+=1;
            END;
            FETCH NEXT FROM topping_store_cursor INTO @PickStoreId;
        END;
        CLOSE topping_store_cursor; DEALLOCATE topping_store_cursor;

        INSERT dbo.OrderToppings(OrderDetailId,ToppingId,ToppingName,Price,CostStatus,TotalCogs)
        SELECT e.OrderDetailId,e.ToppingId,e.ToppingName,e.Price,0,NULL
        FROM @PickedTopping p JOIN @EligibleTopping e ON e.StoreId=p.StoreId AND e.ToppingId=p.ToppingId;


        IF (SELECT COUNT(*) FROM dbo.OrderToppings ot JOIN dbo.OrderDetails od ON od.OrderDetailId=ot.OrderDetailId JOIN dbo.Orders o ON o.OrderId=od.OrderId WHERE o.Source=@SeedMarker AND o.StoreId=@Store1Id)<>30
        OR (SELECT COUNT(*) FROM dbo.OrderToppings ot JOIN dbo.OrderDetails od ON od.OrderDetailId=ot.OrderDetailId JOIN dbo.Orders o ON o.OrderId=od.OrderId WHERE o.Source=@SeedMarker AND o.StoreId=@Store3Id)<>30
            THROW 53447,N'DEMO_REORDER_V14: không resolve được 30 OrderToppings hợp lệ mỗi Store.',1;

        UPDATE o SET o.SubTotal=x.SubTotal,o.Total=x.SubTotal
        FROM dbo.Orders o
        CROSS APPLY(
            SELECT CONVERT(decimal(18,2),
                ISNULL((SELECT SUM(od.Price*od.Quantity) FROM dbo.OrderDetails od WHERE od.OrderId=o.OrderId),0)+
                ISNULL((SELECT SUM(ot.Price) FROM dbo.OrderToppings ot JOIN dbo.OrderDetails od ON od.OrderDetailId=ot.OrderDetailId WHERE od.OrderId=o.OrderId),0)) SubTotal
        )x
        WHERE o.Source=@SeedMarker;

        INSERT dbo.Payments(OrderId,Amount,ReceivedAmount,ChangeAmount,PaymentMethodId,PaymentStatusId,CashSessionId,TransactionCode,PaidAt)
        SELECT o.OrderId,o.Total,NULL,NULL,@BankMethodId,@PaidStatusId,NULL,
               CONCAT(N'DEMO_REORDER_V14_PAY_S',os.StoreNo,N'_',RIGHT(CONCAT(N'000',os.Seq),3)),DATEADD(MINUTE,5,os.CreatedAt)
        FROM dbo.Orders o JOIN @OrderSeed os ON os.ClientOrderId=o.ClientOrderId;
    END
    ELSE
    BEGIN
        UPDATE o SET o.CreatedAt=x.CreatedAt,o.CostedAtUtc=CASE WHEN o.CostStatus=1 THEN x.CreatedAt ELSE NULL END
        FROM dbo.Orders o JOIN @OrderSeed x ON x.ClientOrderId=o.ClientOrderId;
        UPDATE p SET p.PaidAt=DATEADD(MINUTE,5,x.CreatedAt)
        FROM dbo.Payments p JOIN dbo.Orders o ON o.OrderId=p.OrderId JOIN @OrderSeed x ON x.ClientOrderId=o.ClientOrderId;
    END;

    /* ------------------------------------------------------------
       14.9 SALES_DEDUCTION + FIFO SalesCostAllocation on first run.
       Demand is built only from real RecipeDetails.
       ------------------------------------------------------------ */
    IF @IsReplay=0
    BEGIN
        DECLARE @SalesDemand TABLE(
            DemandId int IDENTITY(1,1) PRIMARY KEY,StoreId int,OrderId int,OrderDetailId int,OrderToppingId int NULL,
            StoreInventoryId int,IngredientId int NULL,PreparedItemId int NULL,SourceRecipeId int,
            Quantity decimal(18,3),DemandAt datetime2(0));

        DECLARE @IncompleteDetail TABLE(OrderDetailId int PRIMARY KEY,Reason nvarchar(300));
        DECLARE @IncompleteTopping TABLE(OrderToppingId int PRIMARY KEY,Reason nvarchar(300));

        /* Legacy child Recipe without a valid PreparedItem cost identity is deliberately not fabricated. */
        INSERT @IncompleteDetail(OrderDetailId,Reason)
        SELECT DISTINCT od.OrderDetailId,N'Legacy ChildRecipe has no valid PreparedItem FIFO identity'
        FROM dbo.Orders o JOIN dbo.OrderDetails od ON od.OrderId=o.OrderId
        JOIN dbo.Recipes r ON r.DrinkId=od.DrinkId AND r.SizeId=od.SizeId AND r.Active=1 AND r.Status=N'Active'
        JOIN dbo.RecipeDetails rd ON rd.RecipeId=r.RecipeId AND rd.ChildRecipeId IS NOT NULL
        JOIN dbo.Recipes cr ON cr.RecipeId=rd.ChildRecipeId
        LEFT JOIN dbo.PreparedItems pi ON pi.PreparedItemId=cr.PreparedItemId
        LEFT JOIN dbo.StoreInventories si ON si.StoreId=o.StoreId AND si.RecipeId=cr.RecipeId
             AND si.PreparedItemId=cr.PreparedItemId AND si.BtpIdentityState=1
        WHERE o.Source=@SeedMarker
          AND (cr.PreparedItemId IS NULL OR pi.PreparedItemId IS NULL OR rd.UnitId<>pi.BaseUnitId OR si.StoreInventoryId IS NULL);

        INSERT @IncompleteTopping(OrderToppingId,Reason)
        SELECT DISTINCT ot.OrderToppingId,N'Legacy topping ChildRecipe has no valid PreparedItem FIFO identity'
        FROM dbo.Orders o JOIN dbo.OrderDetails od ON od.OrderId=o.OrderId JOIN dbo.OrderToppings ot ON ot.OrderDetailId=od.OrderDetailId
        JOIN dbo.Recipes r ON r.ToppingId=ot.ToppingId AND r.Active=1 AND r.Status=N'Active'
        JOIN dbo.RecipeDetails rd ON rd.RecipeId=r.RecipeId AND rd.ChildRecipeId IS NOT NULL
        JOIN dbo.Recipes cr ON cr.RecipeId=rd.ChildRecipeId
        LEFT JOIN dbo.PreparedItems pi ON pi.PreparedItemId=cr.PreparedItemId
        LEFT JOIN dbo.StoreInventories si ON si.StoreId=o.StoreId AND si.RecipeId=cr.RecipeId
             AND si.PreparedItemId=cr.PreparedItemId AND si.BtpIdentityState=1
        WHERE o.Source=@SeedMarker
          AND (cr.PreparedItemId IS NULL OR pi.PreparedItemId IS NULL OR rd.UnitId<>pi.BaseUnitId OR si.StoreInventoryId IS NULL);

        /* Drink direct ingredients. */
        INSERT @SalesDemand(StoreId,OrderId,OrderDetailId,OrderToppingId,StoreInventoryId,IngredientId,PreparedItemId,SourceRecipeId,Quantity,DemandAt)
        SELECT o.StoreId,o.OrderId,od.OrderDetailId,NULL,si.StoreInventoryId,rd.IngredientId,NULL,r.RecipeId,
               CONVERT(decimal(18,3),ROUND(rd.Quantity*od.Quantity*
                 CASE WHEN rd.UnitId=i.BaseUnitId THEN 1 ELSE uc.ToQuantity/NULLIF(uc.FromQuantity,0) END,3)),o.CreatedAt
        FROM dbo.Orders o
        JOIN dbo.OrderDetails od ON od.OrderId=o.OrderId
        JOIN dbo.Recipes r ON r.DrinkId=od.DrinkId AND r.SizeId=od.SizeId AND r.Active=1 AND r.Status=N'Active'
        JOIN dbo.RecipeDetails rd ON rd.RecipeId=r.RecipeId AND rd.IngredientId IS NOT NULL
        JOIN dbo.Ingredients i ON i.IngredientId=rd.IngredientId
        JOIN dbo.StoreInventories si ON si.StoreId=o.StoreId AND si.IngredientId=rd.IngredientId
        LEFT JOIN dbo.UnitConversions uc ON uc.IngredientId=i.IngredientId AND uc.FromUnitId=rd.UnitId AND uc.ToUnitId=i.BaseUnitId AND uc.Active=1
        WHERE o.Source=@SeedMarker AND (rd.UnitId=i.BaseUnitId OR uc.UnitConversionId IS NOT NULL);

        /* Drink child PreparedItems, preserving Recipe+PreparedItem identity. */
        INSERT @SalesDemand(StoreId,OrderId,OrderDetailId,OrderToppingId,StoreInventoryId,IngredientId,PreparedItemId,SourceRecipeId,Quantity,DemandAt)
        SELECT o.StoreId,o.OrderId,od.OrderDetailId,NULL,si.StoreInventoryId,NULL,cr.PreparedItemId,cr.RecipeId,
               CONVERT(decimal(18,3),rd.Quantity*od.Quantity),o.CreatedAt
        FROM dbo.Orders o
        JOIN dbo.OrderDetails od ON od.OrderId=o.OrderId
        JOIN dbo.Recipes r ON r.DrinkId=od.DrinkId AND r.SizeId=od.SizeId AND r.Active=1 AND r.Status=N'Active'
        JOIN dbo.RecipeDetails rd ON rd.RecipeId=r.RecipeId AND rd.ChildRecipeId IS NOT NULL
        JOIN dbo.Recipes cr ON cr.RecipeId=rd.ChildRecipeId AND cr.PreparedItemId IS NOT NULL
        JOIN dbo.PreparedItems p ON p.PreparedItemId=cr.PreparedItemId
        JOIN dbo.StoreInventories si ON si.StoreId=o.StoreId AND si.RecipeId=cr.RecipeId AND si.PreparedItemId=cr.PreparedItemId AND si.BtpIdentityState=1
        WHERE o.Source=@SeedMarker AND rd.UnitId=p.BaseUnitId;

        /* Topping direct ingredients. */
        INSERT @SalesDemand(StoreId,OrderId,OrderDetailId,OrderToppingId,StoreInventoryId,IngredientId,PreparedItemId,SourceRecipeId,Quantity,DemandAt)
        SELECT o.StoreId,o.OrderId,od.OrderDetailId,ot.OrderToppingId,si.StoreInventoryId,rd.IngredientId,NULL,r.RecipeId,
               CONVERT(decimal(18,3),ROUND(rd.Quantity*pol.QuantityPerDrink*
                 CASE WHEN rd.UnitId=i.BaseUnitId THEN 1 ELSE uc.ToQuantity/NULLIF(uc.FromQuantity,0) END,3)),o.CreatedAt
        FROM dbo.Orders o JOIN dbo.OrderDetails od ON od.OrderId=o.OrderId JOIN dbo.OrderToppings ot ON ot.OrderDetailId=od.OrderDetailId
        JOIN dbo.DrinkSizeToppingPolicies pol ON pol.DrinkSizeId=od.DrinkSizeId AND pol.ToppingId=ot.ToppingId AND pol.IsActive=1 AND pol.CostTreatment=N'ADD_TOPPING_RECIPE_COST'
        JOIN dbo.Recipes r ON r.ToppingId=ot.ToppingId AND r.Active=1 AND r.Status=N'Active'
        JOIN dbo.RecipeDetails rd ON rd.RecipeId=r.RecipeId AND rd.IngredientId IS NOT NULL
        JOIN dbo.Ingredients i ON i.IngredientId=rd.IngredientId
        JOIN dbo.StoreInventories si ON si.StoreId=o.StoreId AND si.IngredientId=rd.IngredientId
        LEFT JOIN dbo.UnitConversions uc ON uc.IngredientId=i.IngredientId AND uc.FromUnitId=rd.UnitId AND uc.ToUnitId=i.BaseUnitId AND uc.Active=1
        WHERE o.Source=@SeedMarker AND (rd.UnitId=i.BaseUnitId OR uc.UnitConversionId IS NOT NULL);

        /* Topping child PreparedItems. */
        INSERT @SalesDemand(StoreId,OrderId,OrderDetailId,OrderToppingId,StoreInventoryId,IngredientId,PreparedItemId,SourceRecipeId,Quantity,DemandAt)
        SELECT o.StoreId,o.OrderId,od.OrderDetailId,ot.OrderToppingId,si.StoreInventoryId,NULL,cr.PreparedItemId,cr.RecipeId,
               CONVERT(decimal(18,3),rd.Quantity*pol.QuantityPerDrink),o.CreatedAt
        FROM dbo.Orders o JOIN dbo.OrderDetails od ON od.OrderId=o.OrderId JOIN dbo.OrderToppings ot ON ot.OrderDetailId=od.OrderDetailId
        JOIN dbo.DrinkSizeToppingPolicies pol ON pol.DrinkSizeId=od.DrinkSizeId AND pol.ToppingId=ot.ToppingId AND pol.IsActive=1 AND pol.CostTreatment=N'ADD_TOPPING_RECIPE_COST'
        JOIN dbo.Recipes r ON r.ToppingId=ot.ToppingId AND r.Active=1 AND r.Status=N'Active'
        JOIN dbo.RecipeDetails rd ON rd.RecipeId=r.RecipeId AND rd.ChildRecipeId IS NOT NULL
        JOIN dbo.Recipes cr ON cr.RecipeId=rd.ChildRecipeId AND cr.PreparedItemId IS NOT NULL
        JOIN dbo.PreparedItems p ON p.PreparedItemId=cr.PreparedItemId
        JOIN dbo.StoreInventories si ON si.StoreId=o.StoreId AND si.RecipeId=cr.RecipeId AND si.PreparedItemId=cr.PreparedItemId AND si.BtpIdentityState=1
        WHERE o.Source=@SeedMarker AND rd.UnitId=p.BaseUnitId;

        /* Fail closed on any recipe conversion or child identity that could not be represented above. */
        IF EXISTS(
            SELECT 1 FROM dbo.Orders o JOIN dbo.OrderDetails od ON od.OrderId=o.OrderId
            JOIN dbo.Recipes r ON r.DrinkId=od.DrinkId AND r.SizeId=od.SizeId AND r.Active=1 AND r.Status=N'Active'
            JOIN dbo.RecipeDetails rd ON rd.RecipeId=r.RecipeId JOIN dbo.Ingredients i ON i.IngredientId=rd.IngredientId
            WHERE o.Source=@SeedMarker AND rd.IngredientId IS NOT NULL AND rd.UnitId<>i.BaseUnitId
              AND NOT EXISTS(SELECT 1 FROM dbo.UnitConversions uc WHERE uc.IngredientId=i.IngredientId AND uc.FromUnitId=rd.UnitId AND uc.ToUnitId=i.BaseUnitId AND uc.Active=1 AND uc.FromQuantity>0 AND uc.ToQuantity>0)
        ) OR EXISTS(
            SELECT 1 FROM dbo.Orders o JOIN dbo.OrderDetails od ON od.OrderId=o.OrderId
            JOIN dbo.OrderToppings ot ON ot.OrderDetailId=od.OrderDetailId
            JOIN dbo.DrinkSizeToppingPolicies pol ON pol.DrinkSizeId=od.DrinkSizeId AND pol.ToppingId=ot.ToppingId
                 AND pol.IsActive=1 AND pol.CostTreatment=N'ADD_TOPPING_RECIPE_COST'
            JOIN dbo.Recipes r ON r.ToppingId=ot.ToppingId AND r.Active=1 AND r.Status=N'Active'
            JOIN dbo.RecipeDetails rd ON rd.RecipeId=r.RecipeId JOIN dbo.Ingredients i ON i.IngredientId=rd.IngredientId
            WHERE o.Source=@SeedMarker AND rd.IngredientId IS NOT NULL AND rd.UnitId<>i.BaseUnitId
              AND NOT EXISTS(SELECT 1 FROM dbo.UnitConversions uc WHERE uc.IngredientId=i.IngredientId AND uc.FromUnitId=rd.UnitId AND uc.ToUnitId=i.BaseUnitId AND uc.Active=1 AND uc.FromQuantity>0 AND uc.ToQuantity>0)
        ) THROW 53446,N'DEMO_REORDER_V14: thiếu UnitConversion cho sales/topping BOM.',1;

        /* Invalid legacy child identities are tracked in @IncompleteDetail/@IncompleteTopping above. */

        /* For stable transaction business key, one order/source-recipe/stock identity must aggregate to one transaction. */
        DECLARE @SalesAgg TABLE(
            StoreId int,OrderId int,StoreInventoryId int,IngredientId int NULL,PreparedItemId int NULL,SourceRecipeId int,
            Quantity decimal(18,3),DemandAt datetime2(0),PRIMARY KEY(OrderId,StoreInventoryId,SourceRecipeId));
        INSERT @SalesAgg
        SELECT StoreId,OrderId,StoreInventoryId,MAX(IngredientId),MAX(PreparedItemId),SourceRecipeId,SUM(Quantity),MIN(DemandAt)
        FROM @SalesDemand GROUP BY StoreId,OrderId,StoreInventoryId,SourceRecipeId;

        IF EXISTS(SELECT 1 FROM @SalesAgg a JOIN dbo.StoreInventories si ON si.StoreInventoryId=a.StoreInventoryId WHERE a.Quantity<=0)
            THROW 53448,N'DEMO_REORDER_V14: sales demand quantity không hợp lệ.',1;

        /* Precheck final stock and layer supply before mutation. */
IF EXISTS
(
    SELECT
        a.StoreInventoryId,
        SUM(a.Quantity) AS DemandQty,
        MAX(si.AvailableQty) AS AvailableQty

    FROM @SalesAgg a

    JOIN dbo.StoreInventories si
        ON si.StoreInventoryId=a.StoreInventoryId

    GROUP BY a.StoreInventoryId

    HAVING
        SUM(a.Quantity)>MAX(si.AvailableQty)
)
BEGIN

    SELECT
        a.StoreId,
        a.StoreInventoryId,

        MAX(a.IngredientId) AS IngredientId,
        MAX(i.Code) AS IngredientCode,
        MAX(i.Name) AS IngredientName,

        MAX(a.PreparedItemId) AS PreparedItemId,
        MAX(pi.Code) AS PreparedItemCode,
        MAX(pi.Name) AS PreparedItemName,

        SUM(a.Quantity) AS DemandQty,
        MAX(si.AvailableQty) AS AvailableQty,

        SUM(a.Quantity)
            - MAX(si.AvailableQty)
            AS ShortageQty

    FROM @SalesAgg a

    JOIN dbo.StoreInventories si
        ON si.StoreInventoryId=a.StoreInventoryId

    LEFT JOIN dbo.Ingredients i
        ON i.IngredientId=a.IngredientId

    LEFT JOIN dbo.PreparedItems pi
        ON pi.PreparedItemId=a.PreparedItemId

    GROUP BY
        a.StoreId,
        a.StoreInventoryId

    HAVING
        SUM(a.Quantity)>MAX(si.AvailableQty)

    ORDER BY
        a.StoreId,
        a.StoreInventoryId;


    THROW 53449,
          N'DEMO_REORDER_V14: sales demand vượt tồn khả dụng. Xem bảng ShortageQty phía trên.',
          1;

END;


        IF EXISTS(
            SELECT 1
            FROM(
                SELECT StoreInventoryId,MAX(StoreId) StoreId,MAX(IngredientId) IngredientId,MAX(PreparedItemId) PreparedItemId,SUM(Quantity) DemandQty
                FROM @SalesAgg GROUP BY StoreInventoryId
            )d
            OUTER APPLY(
                SELECT SUM(l.RemainingQuantity) SupplyQty
                FROM dbo.InventoryCostLayers l
                WHERE l.StoreId=d.StoreId AND l.RemainingQuantity>0
                  AND ((d.IngredientId IS NOT NULL AND l.IngredientId=d.IngredientId AND l.PreparedItemId IS NULL)
                    OR (d.PreparedItemId IS NOT NULL AND l.PreparedItemId=d.PreparedItemId AND l.IngredientId IS NULL))
            )s
            WHERE ISNULL(s.SupplyQty,0)<d.DemandQty
        ) THROW 53450,N'DEMO_REORDER_V14: sales demand thiếu FIFO layer; không tạo SalesCostGap giả.',1;

        ;WITH D AS(
            SELECT a.*,si.AvailableQty StartQty,si.MinStockLevel,
                   SUM(a.Quantity) OVER(PARTITION BY a.StoreInventoryId ORDER BY a.DemandAt,a.OrderId,a.SourceRecipeId ROWS UNBOUNDED PRECEDING) CumQty
            FROM @SalesAgg a JOIN dbo.StoreInventories si ON si.StoreInventoryId=a.StoreInventoryId
        )
        INSERT dbo.InventoryTransactions(StoreInventoryId,[Type],StockStatus,Quantity,BeforeQty,AfterQty,UnitCost,TotalCost,
                                          InventoryDocumentId,InventoryDocumentDetailId,InventoryTransferId,InventoryTransferDetailId,
                                          ReferenceOrderId,ProductionRunId,SourceRecipeId,InventoryConsolidationRunId,BranchReceiptLineId,
                                          OrderRefundId,CreatedAt)
        SELECT StoreInventoryId,7,CASE WHEN StartQty-CumQty<=ISNULL(MinStockLevel,-1) THEN 2 ELSE 1 END,Quantity,
               StartQty-(CumQty-Quantity),StartQty-CumQty,NULL,NULL,NULL,NULL,NULL,NULL,OrderId,NULL,SourceRecipeId,NULL,NULL,NULL,DemandAt
        FROM D;

        DECLARE @SalesLayer TABLE(StoreInventoryId int,InventoryCostLayerId int,RemainingQuantity decimal(18,3),UnitCost decimal(18,2),CreatedAt datetime2(0));
        INSERT @SalesLayer
        SELECT DISTINCT a.StoreInventoryId,l.InventoryCostLayerId,l.RemainingQuantity,l.UnitCost,l.CreatedAt
        FROM @SalesAgg a
        JOIN dbo.InventoryCostLayers l ON l.StoreId=a.StoreId AND l.RemainingQuantity>0
          AND ((a.IngredientId IS NOT NULL AND l.IngredientId=a.IngredientId AND l.PreparedItemId IS NULL)
            OR (a.PreparedItemId IS NOT NULL AND l.PreparedItemId=a.PreparedItemId AND l.IngredientId IS NULL));

        ;WITH Demand0 AS(
            /* Detail/topping demands themselves form the cumulative demand intervals.
               This preserves attribution while the transaction stays aggregated by Order+Recipe+Stock identity. */
            SELECT sd.*,t.InventoryTransactionId,
                   SUM(CONVERT(decimal(38,6),sd.Quantity)) OVER(
                       PARTITION BY sd.StoreInventoryId
                       ORDER BY sd.DemandAt,sd.OrderId,sd.SourceRecipeId,
                                CASE WHEN sd.OrderToppingId IS NULL THEN 0 ELSE 1 END,sd.OrderDetailId,sd.OrderToppingId,sd.DemandId
                       ROWS UNBOUNDED PRECEDING) DemandEnd
            FROM @SalesDemand sd
            JOIN dbo.InventoryTransactions t
              ON t.ReferenceOrderId=sd.OrderId AND t.StoreInventoryId=sd.StoreInventoryId
             AND t.SourceRecipeId=sd.SourceRecipeId AND t.[Type]=7
        ),Demand AS(
            SELECT *,DemandEnd-CONVERT(decimal(38,6),Quantity) DemandStart FROM Demand0
        ),Supply0 AS(
            SELECT l.*,
                   SUM(CONVERT(decimal(38,6),l.RemainingQuantity)) OVER(
                       PARTITION BY l.StoreInventoryId ORDER BY l.CreatedAt,l.InventoryCostLayerId ROWS UNBOUNDED PRECEDING) SupplyEnd
            FROM @SalesLayer l
        ),Supply AS(
            SELECT *,SupplyEnd-CONVERT(decimal(38,6),RemainingQuantity) SupplyStart FROM Supply0
        ),Slices AS(
            SELECT d.OrderId,d.OrderDetailId,d.OrderToppingId,d.InventoryTransactionId,s.InventoryCostLayerId,
                   d.IngredientId,d.PreparedItemId,s.UnitCost,
                   CONVERT(decimal(18,3),
                     CASE WHEN d.DemandEnd<s.SupplyEnd THEN d.DemandEnd ELSE s.SupplyEnd END
                    -CASE WHEN d.DemandStart>s.SupplyStart THEN d.DemandStart ELSE s.SupplyStart END) Qty
            FROM Demand d
            JOIN Supply s ON s.StoreInventoryId=d.StoreInventoryId
            WHERE d.DemandEnd>s.SupplyStart AND s.SupplyEnd>d.DemandStart
        )
        INSERT dbo.SalesCostAllocations(OrderId,OrderDetailId,OrderToppingId,InventoryTransactionId,InventoryCostLayerId,
                                        IngredientId,PreparedItemId,Quantity,UnitCost,TotalCost,CreatedAtUtc)
        SELECT x.OrderId,x.OrderDetailId,x.OrderToppingId,x.InventoryTransactionId,x.InventoryCostLayerId,
               x.IngredientId,x.PreparedItemId,x.Qty,x.UnitCost,ROUND(x.Qty*x.UnitCost,2),o.CreatedAt
        FROM Slices x JOIN dbo.Orders o ON o.OrderId=x.OrderId
        WHERE x.Qty>0;

        /* Every sales transaction must be fully explained by durable FIFO slices. */
        IF EXISTS(
            SELECT t.InventoryTransactionId,t.Quantity,SUM(ISNULL(a.Quantity,0)) AllocQty
            FROM dbo.InventoryTransactions t
            JOIN dbo.Orders o ON o.OrderId=t.ReferenceOrderId AND o.Source=@SeedMarker
            LEFT JOIN dbo.SalesCostAllocations a ON a.InventoryTransactionId=t.InventoryTransactionId
            WHERE t.[Type]=7
            GROUP BY t.InventoryTransactionId,t.Quantity
            HAVING ABS(t.Quantity-SUM(ISNULL(a.Quantity,0)))>0.001
        ) THROW 53451,N'DEMO_REORDER_V14: SalesCostAllocation không phủ đủ transaction quantity.',1;

        UPDATE l SET l.RemainingQuantity=l.RemainingQuantity-x.Qty
        FROM dbo.InventoryCostLayers l
        JOIN(
            SELECT a.InventoryCostLayerId,SUM(a.Quantity) Qty
            FROM dbo.SalesCostAllocations a JOIN dbo.Orders o ON o.OrderId=a.OrderId AND o.Source=@SeedMarker
            GROUP BY a.InventoryCostLayerId
        )x ON x.InventoryCostLayerId=l.InventoryCostLayerId;

        UPDATE si SET si.AvailableQty=si.AvailableQty-x.Qty,si.LastUpdated=@SeedAnchorUtc
        FROM dbo.StoreInventories si
        JOIN(SELECT StoreInventoryId,SUM(Quantity) Qty FROM @SalesAgg GROUP BY StoreInventoryId)x ON x.StoreInventoryId=si.StoreInventoryId;

        UPDATE t SET t.UnitCost=x.UnitCost,t.TotalCost=x.TotalCost
        FROM dbo.InventoryTransactions t
        JOIN(
            SELECT a.InventoryTransactionId,ROUND(SUM(a.TotalCost)/NULLIF(SUM(a.Quantity),0),2) UnitCost,SUM(a.TotalCost) TotalCost
            FROM dbo.SalesCostAllocations a JOIN dbo.Orders o ON o.OrderId=a.OrderId AND o.Source=@SeedMarker
            GROUP BY a.InventoryTransactionId
        )x ON x.InventoryTransactionId=t.InventoryTransactionId;

        UPDATE ot SET ot.CostStatus=1,ot.TotalCogs=x.TotalCogs
        FROM dbo.OrderToppings ot
        JOIN(SELECT a.OrderToppingId,SUM(a.TotalCost) TotalCogs FROM dbo.SalesCostAllocations a
             JOIN dbo.Orders o ON o.OrderId=a.OrderId AND o.Source=@SeedMarker WHERE a.OrderToppingId IS NOT NULL GROUP BY a.OrderToppingId)x
          ON x.OrderToppingId=ot.OrderToppingId
        WHERE NOT EXISTS(SELECT 1 FROM @IncompleteTopping z WHERE z.OrderToppingId=ot.OrderToppingId);

        UPDATE ot SET ot.CostStatus=2,ot.TotalCogs=NULL
        FROM dbo.OrderToppings ot JOIN @IncompleteTopping z ON z.OrderToppingId=ot.OrderToppingId;

        UPDATE od SET od.CostStatus=1,od.TotalCogs=x.TotalCogs,od.UnitCogs=CONVERT(decimal(18,4),x.TotalCogs/NULLIF(od.Quantity,0))
        FROM dbo.OrderDetails od
        JOIN(SELECT a.OrderDetailId,SUM(a.TotalCost) TotalCogs FROM dbo.SalesCostAllocations a
             JOIN dbo.Orders o ON o.OrderId=a.OrderId AND o.Source=@SeedMarker WHERE a.OrderToppingId IS NULL GROUP BY a.OrderDetailId)x
          ON x.OrderDetailId=od.OrderDetailId
        WHERE NOT EXISTS(SELECT 1 FROM @IncompleteDetail z WHERE z.OrderDetailId=od.OrderDetailId);

        UPDATE od SET od.CostStatus=2,od.TotalCogs=NULL,od.UnitCogs=NULL
        FROM dbo.OrderDetails od JOIN @IncompleteDetail z ON z.OrderDetailId=od.OrderDetailId;

        IF EXISTS(
            SELECT 1 FROM dbo.OrderDetails od JOIN dbo.Orders o ON o.OrderId=od.OrderId AND o.Source=@SeedMarker
            WHERE od.CostStatus=0 OR (od.CostStatus=1 AND (od.TotalCogs IS NULL OR od.UnitCogs IS NULL OR ABS(od.UnitCogs*od.Quantity-od.TotalCogs)>0.02))
               OR (od.CostStatus=2 AND (od.TotalCogs IS NOT NULL OR od.UnitCogs IS NOT NULL))
        ) OR EXISTS(
            SELECT 1 FROM dbo.OrderToppings ot JOIN dbo.OrderDetails od ON od.OrderDetailId=ot.OrderDetailId JOIN dbo.Orders o ON o.OrderId=od.OrderId AND o.Source=@SeedMarker
            WHERE ot.CostStatus=0 OR (ot.CostStatus=1 AND ot.TotalCogs IS NULL) OR (ot.CostStatus=2 AND ot.TotalCogs IS NOT NULL)
        ) THROW 53452,N'DEMO_REORDER_V14: line COGS status/evidence không nhất quán.',1;

        /* Complete only when every line has complete evidence. Incomplete orders never store a partial known COGS. */
        UPDATE o SET o.CostStatus=1,o.TotalCogs=x.TotalCogs,o.GrossProfit=o.Total-x.TotalCogs,o.CostedAtUtc=o.CreatedAt
        FROM dbo.Orders o
        CROSS APPLY(
            SELECT CONVERT(decimal(18,2),
              ISNULL((SELECT SUM(od.TotalCogs) FROM dbo.OrderDetails od WHERE od.OrderId=o.OrderId),0)+
              ISNULL((SELECT SUM(ot.TotalCogs) FROM dbo.OrderToppings ot JOIN dbo.OrderDetails od ON od.OrderDetailId=ot.OrderDetailId WHERE od.OrderId=o.OrderId),0)) TotalCogs
        )x
        WHERE o.Source=@SeedMarker
          AND NOT EXISTS(SELECT 1 FROM dbo.OrderDetails od WHERE od.OrderId=o.OrderId AND od.CostStatus<>1)
          AND NOT EXISTS(SELECT 1 FROM dbo.OrderToppings ot JOIN dbo.OrderDetails od ON od.OrderDetailId=ot.OrderDetailId WHERE od.OrderId=o.OrderId AND ot.CostStatus<>1);

        UPDATE o SET o.CostStatus=2,o.TotalCogs=NULL,o.GrossProfit=NULL,o.CostedAtUtc=NULL
        FROM dbo.Orders o
        WHERE o.Source=@SeedMarker
          AND (EXISTS(SELECT 1 FROM dbo.OrderDetails od WHERE od.OrderId=o.OrderId AND od.CostStatus=2)
            OR EXISTS(SELECT 1 FROM dbo.OrderToppings ot JOIN dbo.OrderDetails od ON od.OrderDetailId=ot.OrderDetailId WHERE od.OrderId=o.OrderId AND ot.CostStatus=2));


        IF EXISTS(SELECT 1 FROM dbo.StoreInventories WHERE AvailableQty<0)
        OR EXISTS(SELECT 1 FROM dbo.InventoryCostLayers WHERE RemainingQuantity<0)
            THROW 53453,N'DEMO_REORDER_V14: sales consumption làm tồn/layer âm.',1;
    END
    ELSE
    BEGIN
        UPDATE t SET t.CreatedAt=o.CreatedAt
        FROM dbo.InventoryTransactions t JOIN dbo.Orders o ON o.OrderId=t.ReferenceOrderId AND o.Source=@SeedMarker
        WHERE t.[Type]=7;
        UPDATE a SET a.CreatedAtUtc=o.CreatedAt
        FROM dbo.SalesCostAllocations a JOIN dbo.Orders o ON o.OrderId=a.OrderId AND o.Source=@SeedMarker;
    END;

    /* Rebase Store3 opening timestamps on replay only; quantity/layer remaining is never reset. */
    IF @IsReplay=1
    BEGIN
        IF @Store3ReconcileDocId IS NOT NULL
        BEGIN
            UPDATE dbo.InventoryDocuments
            SET DocumentDate=DATEADD(MINUTE,30,DATEADD(DAY,-29,@SeedDayUtc)),ConfirmedAt=DATEADD(MINUTE,30,DATEADD(DAY,-29,@SeedDayUtc))
            WHERE InventoryDocumentId=@Store3ReconcileDocId;
            UPDATE t SET t.CreatedAt=DATEADD(MINUTE,30,DATEADD(DAY,-29,@SeedDayUtc))
            FROM dbo.InventoryTransactions t WHERE t.InventoryDocumentId=@Store3ReconcileDocId AND t.[Type]=9;
        END;

        UPDATE dbo.InventoryDocuments SET DocumentDate=DATEADD(HOUR,1,DATEADD(DAY,-29,@SeedDayUtc)),ConfirmedAt=DATEADD(HOUR,1,DATEADD(DAY,-29,@SeedDayUtc))
        WHERE InventoryDocumentId=@Store3OpeningDocId;
        UPDATE t SET t.CreatedAt=DATEADD(HOUR,1,DATEADD(DAY,-29,@SeedDayUtc))
        FROM dbo.InventoryTransactions t WHERE t.InventoryDocumentId=@Store3OpeningDocId AND t.[Type]=8;
        UPDATE l SET l.CreatedAt=DATEADD(HOUR,1,DATEADD(DAY,-29,@SeedDayUtc))
        FROM dbo.InventoryCostLayers l JOIN dbo.InventoryDocumentDetails d ON d.InventoryDocumentDetailId=l.SourceInventoryDocumentDetailId
        WHERE d.InventoryDocumentId=@Store3OpeningDocId;
    END;

    /* ------------------------------------------------------------
       14.10 Acceptance gates for Reorder/POS/BOM/FIFO
       ------------------------------------------------------------ */
    IF (SELECT COUNT(*) FROM dbo.Orders WHERE Source=@SeedMarker AND StoreId=@Store1Id)<>50
    OR (SELECT COUNT(*) FROM dbo.Orders WHERE Source=@SeedMarker AND StoreId=@Store3Id)<>50
        THROW 53454,N'DEMO_REORDER_V14: phải có đúng 50 Orders mỗi Store.',1;

    IF (SELECT COUNT(*) FROM dbo.OrderDetails od JOIN dbo.Orders o ON o.OrderId=od.OrderId WHERE o.Source=@SeedMarker AND o.StoreId=@Store1Id)<>54
    OR (SELECT COUNT(*) FROM dbo.OrderDetails od JOIN dbo.Orders o ON o.OrderId=od.OrderId WHERE o.Source=@SeedMarker AND o.StoreId=@Store3Id)<>54
        THROW 53455,N'DEMO_REORDER_V14: phải có đúng 54 OrderDetails mỗi Store.',1;

    IF (SELECT COUNT(*) FROM dbo.Payments p JOIN dbo.Orders o ON o.OrderId=p.OrderId WHERE o.Source=@SeedMarker AND o.StoreId=@Store1Id AND p.PaymentStatusId=@PaidStatusId)<>50
    OR (SELECT COUNT(*) FROM dbo.Payments p JOIN dbo.Orders o ON o.OrderId=p.OrderId WHERE o.Source=@SeedMarker AND o.StoreId=@Store3Id AND p.PaymentStatusId=@PaidStatusId)<>50
        THROW 53456,N'DEMO_REORDER_V14: phải có đúng 50 paid Payments mỗi Store.',1;

    IF (SELECT COUNT(*) FROM dbo.OrderToppings ot JOIN dbo.OrderDetails od ON od.OrderDetailId=ot.OrderDetailId JOIN dbo.Orders o ON o.OrderId=od.OrderId WHERE o.Source=@SeedMarker AND o.StoreId=@Store1Id)<>30
    OR (SELECT COUNT(*) FROM dbo.OrderToppings ot JOIN dbo.OrderDetails od ON od.OrderDetailId=ot.OrderDetailId JOIN dbo.Orders o ON o.OrderId=od.OrderId WHERE o.Source=@SeedMarker AND o.StoreId=@Store3Id)<>30
        THROW 53457,N'DEMO_REORDER_V14: phải có đúng 30 OrderToppings mỗi Store.',1;

    IF (SELECT COUNT(*) FROM dbo.WorkShifts WHERE StoreId=@Store1Id AND DiscrepancyReason LIKE N'DEMO_REORDER_V14_SHIFT_S1_%' AND Status=N'CLOSED')<>30
    OR (SELECT COUNT(*) FROM dbo.WorkShifts WHERE StoreId=@Store3Id AND DiscrepancyReason LIKE N'DEMO_REORDER_V14_SHIFT_S3_%' AND Status=N'CLOSED')<>30
        THROW 53458,N'DEMO_REORDER_V14: phải có đúng 30 closed WorkShifts mỗi Store.',1;

    IF (SELECT COUNT(*) FROM dbo.ProductionRuns WHERE StoreId=@Store1Id AND Notes LIKE N'DEMO_REORDER_V14_PROD_S1_%' AND Status=2)<>30
    OR (SELECT COUNT(*) FROM dbo.ProductionRuns WHERE StoreId=@Store3Id AND Notes LIKE N'DEMO_REORDER_V14_PROD_S3_%' AND Status=2)<>30
        THROW 53459,N'DEMO_REORDER_V14: phải có đúng 30 completed ProductionRuns mỗi Store.',1;

    /* Recompute expected production BOM demand from current RecipeDetails and compare to durable movements. */
    DECLARE @ExpectedProdCheck TABLE(
        ProductionRunId int,StoreInventoryId int,SourceRecipeId int,Quantity decimal(18,3),
        PRIMARY KEY(ProductionRunId,StoreInventoryId,SourceRecipeId));
    INSERT @ExpectedProdCheck(ProductionRunId,StoreInventoryId,SourceRecipeId,Quantity)
    SELECT x.ProductionRunId,x.StoreInventoryId,x.SourceRecipeId,SUM(x.Quantity)
    FROM(
        SELECT pr.ProductionRunId,si.StoreInventoryId,pr.RecipeId SourceRecipeId,
               CONVERT(decimal(18,3),ROUND(rd.Quantity*pr.RequestedRunCount*
                 CASE WHEN rd.UnitId=i.BaseUnitId THEN 1 ELSE uc.ToQuantity/NULLIF(uc.FromQuantity,0) END,3)) Quantity
        FROM dbo.ProductionRuns pr
        JOIN dbo.RecipeDetails rd ON rd.RecipeId=pr.RecipeId AND rd.IngredientId IS NOT NULL
        JOIN dbo.Ingredients i ON i.IngredientId=rd.IngredientId
        JOIN dbo.StoreInventories si ON si.StoreId=pr.StoreId AND si.IngredientId=rd.IngredientId
        LEFT JOIN dbo.UnitConversions uc ON uc.IngredientId=i.IngredientId AND uc.FromUnitId=rd.UnitId
             AND uc.ToUnitId=i.BaseUnitId AND uc.Active=1
        WHERE pr.Notes LIKE N'DEMO_REORDER_V14_PROD_S%'
          AND (rd.UnitId=i.BaseUnitId OR uc.UnitConversionId IS NOT NULL)
        UNION ALL
        SELECT pr.ProductionRunId,si.StoreInventoryId,cr.RecipeId,
               CONVERT(decimal(18,3),rd.Quantity*pr.RequestedRunCount)
        FROM dbo.ProductionRuns pr
        JOIN dbo.RecipeDetails rd ON rd.RecipeId=pr.RecipeId AND rd.ChildRecipeId IS NOT NULL
        JOIN dbo.Recipes cr ON cr.RecipeId=rd.ChildRecipeId AND cr.PreparedItemId IS NOT NULL
        JOIN dbo.PreparedItems pi ON pi.PreparedItemId=cr.PreparedItemId
        JOIN dbo.StoreInventories si ON si.StoreId=pr.StoreId AND si.RecipeId=cr.RecipeId
             AND si.PreparedItemId=cr.PreparedItemId AND si.BtpIdentityState=1
        WHERE pr.Notes LIKE N'DEMO_REORDER_V14_PROD_S%' AND rd.UnitId=pi.BaseUnitId
    )x
    GROUP BY x.ProductionRunId,x.StoreInventoryId,x.SourceRecipeId;

    IF EXISTS(
        SELECT 1
        FROM @ExpectedProdCheck e
        FULL OUTER JOIN(
            SELECT t.ProductionRunId,t.StoreInventoryId,t.SourceRecipeId,SUM(t.Quantity) Quantity,COUNT(*) TxCount
            FROM dbo.InventoryTransactions t
            JOIN dbo.ProductionRuns pr ON pr.ProductionRunId=t.ProductionRunId AND pr.Notes LIKE N'DEMO_REORDER_V14_PROD_S%'
            WHERE t.[Type]=6
            GROUP BY t.ProductionRunId,t.StoreInventoryId,t.SourceRecipeId
        )a ON a.ProductionRunId=e.ProductionRunId AND a.StoreInventoryId=e.StoreInventoryId AND a.SourceRecipeId=e.SourceRecipeId
        WHERE e.ProductionRunId IS NULL OR a.ProductionRunId IS NULL OR a.TxCount<>1 OR ABS(e.Quantity-a.Quantity)>0.001
    ) THROW 53490,N'DEMO_REORDER_V14: PRODUCTION_OUT payload không khớp BOM đã quy đổi base unit.',1;

    IF EXISTS(
        SELECT 1
        FROM dbo.ProductionRuns pr
        JOIN dbo.Recipes r ON r.RecipeId=pr.RecipeId
        LEFT JOIN dbo.StoreInventories si ON si.StoreId=pr.StoreId AND si.RecipeId=r.RecipeId
             AND si.PreparedItemId=r.PreparedItemId AND si.BtpIdentityState=1
        LEFT JOIN dbo.InventoryTransactions t ON t.ProductionRunId=pr.ProductionRunId AND t.StoreInventoryId=si.StoreInventoryId AND t.[Type]=5
        LEFT JOIN dbo.InventoryCostLayers l ON l.SourceProductionRunId=pr.ProductionRunId
        WHERE pr.Notes LIKE N'DEMO_REORDER_V14_PROD_S%'
          AND (si.StoreInventoryId IS NULL OR t.InventoryTransactionId IS NULL OR l.InventoryCostLayerId IS NULL
            OR t.SourceRecipeId<>pr.RecipeId OR t.Quantity<>CONVERT(decimal(18,3),r.OutputQuantity)
            OR ABS((t.AfterQty-t.BeforeQty)-t.Quantity)>0.001 OR t.TotalCost<>pr.TotalInputCost
            OR l.PreparedItemId<>r.PreparedItemId OR l.IngredientId IS NOT NULL OR l.StoreId<>pr.StoreId
            OR l.Quantity<>CONVERT(decimal(18,3),r.OutputQuantity) OR l.RemainingQuantity<0 OR l.RemainingQuantity>l.Quantity
            OR ABS(l.UnitCost-ROUND(pr.OutputUnitCost,2))>0.01)
    ) THROW 53491,N'DEMO_REORDER_V14: PRODUCTION_IN/output cost-layer payload drift.',1;

    /* Recompute sales/topping demand to verify transaction quantity and allocation attribution on replay too. */
    DECLARE @ExpectedSalesCheck TABLE(
        StoreId int,OrderId int,OrderDetailId int,OrderToppingId int NULL,StoreInventoryId int,
        IngredientId int NULL,PreparedItemId int NULL,SourceRecipeId int,Quantity decimal(18,3));

    INSERT @ExpectedSalesCheck
    SELECT o.StoreId,o.OrderId,od.OrderDetailId,NULL,si.StoreInventoryId,rd.IngredientId,NULL,r.RecipeId,
           CONVERT(decimal(18,3),ROUND(rd.Quantity*od.Quantity*
             CASE WHEN rd.UnitId=i.BaseUnitId THEN 1 ELSE uc.ToQuantity/NULLIF(uc.FromQuantity,0) END,3))
    FROM dbo.Orders o JOIN dbo.OrderDetails od ON od.OrderId=o.OrderId
    JOIN dbo.Recipes r ON r.DrinkId=od.DrinkId AND r.SizeId=od.SizeId AND r.Active=1 AND r.Status=N'Active'
    JOIN dbo.RecipeDetails rd ON rd.RecipeId=r.RecipeId AND rd.IngredientId IS NOT NULL
    JOIN dbo.Ingredients i ON i.IngredientId=rd.IngredientId
    JOIN dbo.StoreInventories si ON si.StoreId=o.StoreId AND si.IngredientId=rd.IngredientId
    LEFT JOIN dbo.UnitConversions uc ON uc.IngredientId=i.IngredientId AND uc.FromUnitId=rd.UnitId AND uc.ToUnitId=i.BaseUnitId AND uc.Active=1
    WHERE o.Source=@SeedMarker AND (rd.UnitId=i.BaseUnitId OR uc.UnitConversionId IS NOT NULL);

    INSERT @ExpectedSalesCheck
    SELECT o.StoreId,o.OrderId,od.OrderDetailId,NULL,si.StoreInventoryId,NULL,cr.PreparedItemId,cr.RecipeId,
           CONVERT(decimal(18,3),rd.Quantity*od.Quantity)
    FROM dbo.Orders o JOIN dbo.OrderDetails od ON od.OrderId=o.OrderId
    JOIN dbo.Recipes r ON r.DrinkId=od.DrinkId AND r.SizeId=od.SizeId AND r.Active=1 AND r.Status=N'Active'
    JOIN dbo.RecipeDetails rd ON rd.RecipeId=r.RecipeId AND rd.ChildRecipeId IS NOT NULL
    JOIN dbo.Recipes cr ON cr.RecipeId=rd.ChildRecipeId AND cr.PreparedItemId IS NOT NULL
    JOIN dbo.PreparedItems pi ON pi.PreparedItemId=cr.PreparedItemId
    JOIN dbo.StoreInventories si ON si.StoreId=o.StoreId AND si.RecipeId=cr.RecipeId AND si.PreparedItemId=cr.PreparedItemId AND si.BtpIdentityState=1
    WHERE o.Source=@SeedMarker AND rd.UnitId=pi.BaseUnitId;

    INSERT @ExpectedSalesCheck
    SELECT o.StoreId,o.OrderId,od.OrderDetailId,ot.OrderToppingId,si.StoreInventoryId,rd.IngredientId,NULL,r.RecipeId,
           CONVERT(decimal(18,3),ROUND(rd.Quantity*pol.QuantityPerDrink*
             CASE WHEN rd.UnitId=i.BaseUnitId THEN 1 ELSE uc.ToQuantity/NULLIF(uc.FromQuantity,0) END,3))
    FROM dbo.Orders o JOIN dbo.OrderDetails od ON od.OrderId=o.OrderId JOIN dbo.OrderToppings ot ON ot.OrderDetailId=od.OrderDetailId
    JOIN dbo.DrinkSizeToppingPolicies pol ON pol.DrinkSizeId=od.DrinkSizeId AND pol.ToppingId=ot.ToppingId AND pol.IsActive=1 AND pol.CostTreatment=N'ADD_TOPPING_RECIPE_COST'
    JOIN dbo.Recipes r ON r.ToppingId=ot.ToppingId AND r.Active=1 AND r.Status=N'Active'
    JOIN dbo.RecipeDetails rd ON rd.RecipeId=r.RecipeId AND rd.IngredientId IS NOT NULL
    JOIN dbo.Ingredients i ON i.IngredientId=rd.IngredientId
    JOIN dbo.StoreInventories si ON si.StoreId=o.StoreId AND si.IngredientId=rd.IngredientId
    LEFT JOIN dbo.UnitConversions uc ON uc.IngredientId=i.IngredientId AND uc.FromUnitId=rd.UnitId AND uc.ToUnitId=i.BaseUnitId AND uc.Active=1
    WHERE o.Source=@SeedMarker AND (rd.UnitId=i.BaseUnitId OR uc.UnitConversionId IS NOT NULL);

    INSERT @ExpectedSalesCheck
    SELECT o.StoreId,o.OrderId,od.OrderDetailId,ot.OrderToppingId,si.StoreInventoryId,NULL,cr.PreparedItemId,cr.RecipeId,
           CONVERT(decimal(18,3),rd.Quantity*pol.QuantityPerDrink)
    FROM dbo.Orders o JOIN dbo.OrderDetails od ON od.OrderId=o.OrderId JOIN dbo.OrderToppings ot ON ot.OrderDetailId=od.OrderDetailId
    JOIN dbo.DrinkSizeToppingPolicies pol ON pol.DrinkSizeId=od.DrinkSizeId AND pol.ToppingId=ot.ToppingId AND pol.IsActive=1 AND pol.CostTreatment=N'ADD_TOPPING_RECIPE_COST'
    JOIN dbo.Recipes r ON r.ToppingId=ot.ToppingId AND r.Active=1 AND r.Status=N'Active'
    JOIN dbo.RecipeDetails rd ON rd.RecipeId=r.RecipeId AND rd.ChildRecipeId IS NOT NULL
    JOIN dbo.Recipes cr ON cr.RecipeId=rd.ChildRecipeId AND cr.PreparedItemId IS NOT NULL
    JOIN dbo.PreparedItems pi ON pi.PreparedItemId=cr.PreparedItemId
    JOIN dbo.StoreInventories si ON si.StoreId=o.StoreId AND si.RecipeId=cr.RecipeId AND si.PreparedItemId=cr.PreparedItemId AND si.BtpIdentityState=1
    WHERE o.Source=@SeedMarker AND rd.UnitId=pi.BaseUnitId;

    IF EXISTS(
        SELECT 1
        FROM(
            SELECT OrderId,StoreInventoryId,SourceRecipeId,SUM(Quantity) Quantity
            FROM @ExpectedSalesCheck GROUP BY OrderId,StoreInventoryId,SourceRecipeId
        )e
        FULL OUTER JOIN(
            SELECT t.ReferenceOrderId OrderId,t.StoreInventoryId,t.SourceRecipeId,SUM(t.Quantity) Quantity,COUNT(*) TxCount
            FROM dbo.InventoryTransactions t JOIN dbo.Orders o ON o.OrderId=t.ReferenceOrderId AND o.Source=@SeedMarker
            WHERE t.[Type]=7 GROUP BY t.ReferenceOrderId,t.StoreInventoryId,t.SourceRecipeId
        )a ON a.OrderId=e.OrderId AND a.StoreInventoryId=e.StoreInventoryId AND a.SourceRecipeId=e.SourceRecipeId
        WHERE e.OrderId IS NULL OR a.OrderId IS NULL OR a.TxCount<>1 OR ABS(e.Quantity-a.Quantity)>0.001
    ) THROW 53492,N'DEMO_REORDER_V14: SALES_DEDUCTION payload không khớp BOM/policy đã quy đổi base unit.',1;

    IF EXISTS(
        SELECT 1
        FROM(
            SELECT OrderId,OrderDetailId,OrderToppingId,StoreInventoryId,SourceRecipeId,SUM(Quantity) Quantity
            FROM @ExpectedSalesCheck
            GROUP BY OrderId,OrderDetailId,OrderToppingId,StoreInventoryId,SourceRecipeId
        )e
        FULL OUTER JOIN(
            SELECT a.OrderId,a.OrderDetailId,a.OrderToppingId,t.StoreInventoryId,t.SourceRecipeId,SUM(a.Quantity) Quantity
            FROM dbo.SalesCostAllocations a
            JOIN dbo.InventoryTransactions t ON t.InventoryTransactionId=a.InventoryTransactionId AND t.[Type]=7
            JOIN dbo.Orders o ON o.OrderId=a.OrderId AND o.Source=@SeedMarker
            GROUP BY a.OrderId,a.OrderDetailId,a.OrderToppingId,t.StoreInventoryId,t.SourceRecipeId
        )a ON a.OrderId=e.OrderId AND a.OrderDetailId=e.OrderDetailId
            AND ISNULL(a.OrderToppingId,-1)=ISNULL(e.OrderToppingId,-1)
            AND a.StoreInventoryId=e.StoreInventoryId AND a.SourceRecipeId=e.SourceRecipeId
        WHERE e.OrderId IS NULL OR a.OrderId IS NULL OR ABS(e.Quantity-a.Quantity)>0.001
    ) THROW 53493,N'DEMO_REORDER_V14: SalesCostAllocation attribution không khớp OrderDetail/Topping BOM demand.',1;

    IF (SELECT COUNT(*) FROM dbo.InventoryTransactions t JOIN dbo.StoreInventories si ON si.StoreInventoryId=t.StoreInventoryId
        WHERE si.StoreId=@Store1Id AND t.[Type] IN(6,7) AND t.CreatedAt>=@WindowStartUtc AND t.CreatedAt<=@SeedAnchorUtc)<30
    OR (SELECT COUNT(*) FROM dbo.InventoryTransactions t JOIN dbo.StoreInventories si ON si.StoreInventoryId=t.StoreInventoryId
        WHERE si.StoreId=@Store3Id AND t.[Type] IN(6,7) AND t.CreatedAt>=@WindowStartUtc AND t.CreatedAt<=@SeedAnchorUtc)<30
        THROW 53460,N'DEMO_REORDER_V14: thiếu 30 movement tiêu thụ hợp lệ trong rolling 30 days.',1;

    IF (SELECT COUNT(*) FROM dbo.SalesCostAllocations a JOIN dbo.Orders o ON o.OrderId=a.OrderId WHERE o.Source=@SeedMarker AND o.StoreId=@Store1Id)<30
    OR (SELECT COUNT(*) FROM dbo.SalesCostAllocations a JOIN dbo.Orders o ON o.OrderId=a.OrderId WHERE o.Source=@SeedMarker AND o.StoreId=@Store3Id)<30
        THROW 53461,N'DEMO_REORDER_V14: thiếu SalesCostAllocations.',1;

    /* Allocation semantic integrity: Order/detail/topping/transaction/layer and cost identity must agree. */
    IF EXISTS(
        SELECT 1
        FROM dbo.SalesCostAllocations a
        JOIN dbo.Orders o ON o.OrderId=a.OrderId AND o.Source=@SeedMarker
        LEFT JOIN dbo.OrderDetails od ON od.OrderDetailId=a.OrderDetailId
        LEFT JOIN dbo.OrderToppings ot ON ot.OrderToppingId=a.OrderToppingId
        LEFT JOIN dbo.InventoryTransactions t ON t.InventoryTransactionId=a.InventoryTransactionId
        LEFT JOIN dbo.StoreInventories si ON si.StoreInventoryId=t.StoreInventoryId
        LEFT JOIN dbo.InventoryCostLayers l ON l.InventoryCostLayerId=a.InventoryCostLayerId
        WHERE od.OrderDetailId IS NULL OR od.OrderId<>a.OrderId
           OR (a.OrderToppingId IS NOT NULL AND (ot.OrderToppingId IS NULL OR ot.OrderDetailId<>a.OrderDetailId))
           OR t.InventoryTransactionId IS NULL OR t.[Type]<>7 OR t.ReferenceOrderId<>a.OrderId OR t.SourceRecipeId IS NULL
           OR si.StoreInventoryId IS NULL OR si.StoreId<>o.StoreId OR l.InventoryCostLayerId IS NULL OR l.StoreId<>o.StoreId
           OR a.Quantity<=0 OR a.UnitCost<>l.UnitCost OR ABS(a.TotalCost-ROUND(a.Quantity*a.UnitCost,2))>0.01
           OR (a.IngredientId IS NULL AND a.PreparedItemId IS NULL)
           OR (a.IngredientId IS NOT NULL AND a.PreparedItemId IS NOT NULL)
           OR (a.IngredientId IS NOT NULL AND (ISNULL(si.IngredientId,-1)<>a.IngredientId OR si.PreparedItemId IS NOT NULL
                OR ISNULL(l.IngredientId,-1)<>a.IngredientId OR l.PreparedItemId IS NOT NULL))
           OR (a.PreparedItemId IS NOT NULL AND (ISNULL(si.PreparedItemId,-1)<>a.PreparedItemId OR si.IngredientId IS NOT NULL
                OR ISNULL(l.PreparedItemId,-1)<>a.PreparedItemId OR l.IngredientId IS NOT NULL))
    ) THROW 53496,N'DEMO_REORDER_V14: SalesCostAllocation semantic identity/link payload drift.',1;

    IF EXISTS(
        SELECT 1
        FROM dbo.ProductionCostAllocations a
        JOIN dbo.ProductionRuns pr ON pr.ProductionRunId=a.ProductionRunId AND pr.Notes LIKE N'DEMO_REORDER_V14_PROD_S%'
        LEFT JOIN dbo.InventoryTransactions t ON t.InventoryTransactionId=a.InventoryTransactionId
        LEFT JOIN dbo.StoreInventories si ON si.StoreInventoryId=t.StoreInventoryId
        LEFT JOIN dbo.InventoryCostLayers l ON l.InventoryCostLayerId=a.InventoryCostLayerId
        WHERE t.InventoryTransactionId IS NULL OR t.ProductionRunId<>a.ProductionRunId OR t.[Type]<>6
           OR si.StoreInventoryId IS NULL OR si.StoreId<>pr.StoreId OR l.InventoryCostLayerId IS NULL OR l.StoreId<>pr.StoreId
           OR a.Quantity<=0 OR a.UnitCost<>l.UnitCost OR ABS(a.TotalCost-ROUND(a.Quantity*a.UnitCost,2))>0.01
           OR (si.IngredientId IS NOT NULL AND (ISNULL(l.IngredientId,-1)<>si.IngredientId OR l.PreparedItemId IS NOT NULL))
           OR (si.PreparedItemId IS NOT NULL AND (ISNULL(l.PreparedItemId,-1)<>si.PreparedItemId OR l.IngredientId IS NOT NULL))
    ) THROW 53497,N'DEMO_REORDER_V14: ProductionCostAllocation semantic identity/link payload drift.',1;

    /* Strong FIFO invariant: a younger layer may not be allocated while an older eligible layer still has quantity. */
    IF EXISTS(
        SELECT 1
        FROM dbo.SalesCostAllocations a
        JOIN dbo.Orders o ON o.OrderId=a.OrderId AND o.Source=@SeedMarker
        JOIN dbo.InventoryTransactions t ON t.InventoryTransactionId=a.InventoryTransactionId
        JOIN dbo.InventoryCostLayers l ON l.InventoryCostLayerId=a.InventoryCostLayerId
        WHERE EXISTS(
            SELECT 1 FROM dbo.InventoryCostLayers older
            WHERE older.StoreId=l.StoreId AND older.RemainingQuantity>0 AND older.CreatedAt<=t.CreatedAt
              AND ((l.IngredientId IS NOT NULL AND older.IngredientId=l.IngredientId AND older.PreparedItemId IS NULL)
                OR (l.PreparedItemId IS NOT NULL AND older.PreparedItemId=l.PreparedItemId AND older.IngredientId IS NULL))
              AND (older.CreatedAt<l.CreatedAt OR (older.CreatedAt=l.CreatedAt AND older.InventoryCostLayerId<l.InventoryCostLayerId))
        )
    ) OR EXISTS(
        SELECT 1
        FROM dbo.ProductionCostAllocations a
        JOIN dbo.ProductionRuns pr ON pr.ProductionRunId=a.ProductionRunId AND pr.Notes LIKE N'DEMO_REORDER_V14_PROD_S%'
        JOIN dbo.InventoryTransactions t ON t.InventoryTransactionId=a.InventoryTransactionId
        JOIN dbo.InventoryCostLayers l ON l.InventoryCostLayerId=a.InventoryCostLayerId
        WHERE EXISTS(
            SELECT 1 FROM dbo.InventoryCostLayers older
            WHERE older.StoreId=l.StoreId AND older.RemainingQuantity>0 AND older.CreatedAt<=t.CreatedAt
              AND ((l.IngredientId IS NOT NULL AND older.IngredientId=l.IngredientId AND older.PreparedItemId IS NULL)
                OR (l.PreparedItemId IS NOT NULL AND older.PreparedItemId=l.PreparedItemId AND older.IngredientId IS NULL))
              AND (older.CreatedAt<l.CreatedAt OR (older.CreatedAt=l.CreatedAt AND older.InventoryCostLayerId<l.InventoryCostLayerId))
        )
    ) THROW 53498,N'DEMO_REORDER_V14: phát hiện allocation vi phạm FIFO layer ordering.',1;

    IF (SELECT COUNT(*) FROM dbo.StoreInventories WHERE StoreId=@Store1Id AND IngredientId IS NOT NULL)<50
    OR (SELECT COUNT(*) FROM dbo.StoreInventories WHERE StoreId=@Store3Id AND IngredientId IS NOT NULL)<50
        THROW 53462,N'DEMO_REORDER_V14: mỗi Store phải có ít nhất 50 ingredient StoreInventories.',1;

    IF (SELECT COUNT(*) FROM dbo.InventoryCostLayers l JOIN dbo.InventoryDocumentDetails d ON d.InventoryDocumentDetailId=l.SourceInventoryDocumentDetailId WHERE d.InventoryDocumentId=@Store3OpeningDocId)<50
        THROW 53463,N'DEMO_REORDER_V14: Store 3 phải có ít nhất 50 opening cost layers.',1;

    IF EXISTS(
        SELECT 1
        FROM dbo.InventoryDocumentDetails seedLine
        JOIN dbo.InventoryDocuments seedDoc ON seedDoc.InventoryDocumentId=seedLine.InventoryDocumentId AND seedDoc.RequestKey=N'DEMO_OPENING_STORE1_INGREDIENTS'
        JOIN dbo.Ingredients i ON i.IngredientId=seedLine.IngredientId AND i.Active=1
        WHERE NOT EXISTS(
              SELECT 1
              FROM dbo.StoreInventories si
              JOIN dbo.InventoryTransactions t ON t.StoreInventoryId=si.StoreInventoryId
              WHERE si.StoreId=@Store1Id AND si.IngredientId=i.IngredientId AND t.[Type] IN(6,7)
                AND t.CreatedAt>=@WindowStartUtc AND t.CreatedAt<=@SeedAnchorUtc
          )
    ) THROW 53464,N'DEMO_REORDER_V14: Store 1 còn ingredient không có consumption movement thật trong rolling 30 days.',1;

    IF EXISTS(
        SELECT 1
        FROM dbo.InventoryDocumentDetails seedLine
        JOIN dbo.InventoryDocuments seedDoc ON seedDoc.InventoryDocumentId=seedLine.InventoryDocumentId AND seedDoc.RequestKey=N'DEMO_OPENING_STORE1_INGREDIENTS'
        JOIN dbo.Ingredients i ON i.IngredientId=seedLine.IngredientId AND i.Active=1
        WHERE NOT EXISTS(
              SELECT 1
              FROM dbo.StoreInventories si
              JOIN dbo.InventoryTransactions t ON t.StoreInventoryId=si.StoreInventoryId
              WHERE si.StoreId=@Store3Id AND si.IngredientId=i.IngredientId AND t.[Type] IN(6,7)
                AND t.CreatedAt>=@WindowStartUtc AND t.CreatedAt<=@SeedAnchorUtc
          )
    ) THROW 53465,N'DEMO_REORDER_V14: Store 3 còn ingredient không có consumption movement thật trong rolling 30 days.',1;

    IF EXISTS(SELECT 1 FROM dbo.StoreInventories WHERE StoreId IN(@Store1Id,@Store3Id) AND AvailableQty<0)
    OR EXISTS(SELECT 1 FROM dbo.InventoryCostLayers WHERE StoreId IN(@Store1Id,@Store3Id) AND RemainingQuantity<0)
        THROW 53466,N'DEMO_REORDER_V14: invariant no-negative stock/layer bị vi phạm.',1;

    IF EXISTS(
        SELECT 1 FROM dbo.Orders o WHERE o.Source=@SeedMarker
          AND (o.CostStatus NOT IN(1,2)
            OR (o.CostStatus=1 AND (o.TotalCogs IS NULL OR o.GrossProfit IS NULL OR o.CostedAtUtc IS NULL
                OR ABS(o.GrossProfit-(o.Total-o.TotalCogs))>0.01
                OR ABS(o.TotalCogs-(
                    ISNULL((SELECT SUM(od.TotalCogs) FROM dbo.OrderDetails od WHERE od.OrderId=o.OrderId),0)
                   +ISNULL((SELECT SUM(ot.TotalCogs) FROM dbo.OrderToppings ot JOIN dbo.OrderDetails od ON od.OrderDetailId=ot.OrderDetailId WHERE od.OrderId=o.OrderId),0)))>0.01))
            OR (o.CostStatus=2 AND (o.TotalCogs IS NOT NULL OR o.GrossProfit IS NOT NULL OR o.CostedAtUtc IS NOT NULL)))
    ) THROW 53467,N'DEMO_REORDER_V14: Order CostStatus/COGS evidence không nhất quán.',1;

    IF EXISTS(
        SELECT 1 FROM dbo.OrderDetails od JOIN dbo.Orders o ON o.OrderId=od.OrderId AND o.Source=@SeedMarker
        WHERE od.CostStatus NOT IN(1,2)
           OR (od.CostStatus=1 AND (od.TotalCogs IS NULL OR od.UnitCogs IS NULL OR ABS(od.UnitCogs*od.Quantity-od.TotalCogs)>0.02))
           OR (od.CostStatus=2 AND (od.TotalCogs IS NOT NULL OR od.UnitCogs IS NOT NULL))
    ) OR EXISTS(
        SELECT 1 FROM dbo.OrderToppings ot JOIN dbo.OrderDetails od ON od.OrderDetailId=ot.OrderDetailId
        JOIN dbo.Orders o ON o.OrderId=od.OrderId AND o.Source=@SeedMarker
        WHERE ot.CostStatus NOT IN(1,2)
           OR (ot.CostStatus=1 AND ot.TotalCogs IS NULL)
           OR (ot.CostStatus=2 AND ot.TotalCogs IS NOT NULL)
    ) THROW 53480,N'DEMO_REORDER_V14: detail/topping CostStatus/COGS evidence không nhất quán.',1;

    IF EXISTS(
        SELECT 1 FROM dbo.Orders o WHERE o.Source=@SeedMarker AND o.CostStatus=1
          AND (EXISTS(SELECT 1 FROM dbo.OrderDetails od WHERE od.OrderId=o.OrderId AND od.CostStatus<>1)
            OR EXISTS(SELECT 1 FROM dbo.OrderToppings ot JOIN dbo.OrderDetails od ON od.OrderDetailId=ot.OrderDetailId WHERE od.OrderId=o.OrderId AND ot.CostStatus<>1))
    ) OR EXISTS(
        SELECT 1 FROM dbo.Orders o WHERE o.Source=@SeedMarker AND o.CostStatus=2
          AND NOT (EXISTS(SELECT 1 FROM dbo.OrderDetails od WHERE od.OrderId=o.OrderId AND od.CostStatus=2)
            OR EXISTS(SELECT 1 FROM dbo.OrderToppings ot JOIN dbo.OrderDetails od ON od.OrderDetailId=ot.OrderDetailId WHERE od.OrderId=o.OrderId AND ot.CostStatus=2))
    ) THROW 53481,N'DEMO_REORDER_V14: Order CostStatus không khớp line evidence.',1;

    /* Incomplete is allowed only for an actual legacy ChildRecipe without a valid PreparedItem cost identity. */
    IF EXISTS(
        SELECT 1 FROM dbo.OrderDetails od JOIN dbo.Orders o ON o.OrderId=od.OrderId AND o.Source=@SeedMarker
        WHERE od.CostStatus=2
          AND NOT EXISTS(
              SELECT 1 FROM dbo.Recipes r JOIN dbo.RecipeDetails rd ON rd.RecipeId=r.RecipeId AND rd.ChildRecipeId IS NOT NULL
              JOIN dbo.Recipes cr ON cr.RecipeId=rd.ChildRecipeId
              LEFT JOIN dbo.PreparedItems pi ON pi.PreparedItemId=cr.PreparedItemId
              LEFT JOIN dbo.StoreInventories si ON si.StoreId=o.StoreId AND si.RecipeId=cr.RecipeId AND si.PreparedItemId=cr.PreparedItemId AND si.BtpIdentityState=1
              WHERE r.DrinkId=od.DrinkId AND r.SizeId=od.SizeId AND r.Active=1 AND r.Status=N'Active'
                AND (cr.PreparedItemId IS NULL OR pi.PreparedItemId IS NULL OR rd.UnitId<>pi.BaseUnitId OR si.StoreInventoryId IS NULL)
          )
    ) THROW 53470,N'DEMO_REORDER_V14: có Incomplete detail không được giải thích bởi legacy BTP identity.',1;

    IF EXISTS(
        SELECT 1 FROM dbo.OrderToppings ot
        JOIN dbo.OrderDetails od ON od.OrderDetailId=ot.OrderDetailId
        JOIN dbo.Orders o ON o.OrderId=od.OrderId AND o.Source=@SeedMarker
        WHERE ot.CostStatus=2
          AND NOT EXISTS(
              SELECT 1 FROM dbo.Recipes r
              JOIN dbo.RecipeDetails rd ON rd.RecipeId=r.RecipeId AND rd.ChildRecipeId IS NOT NULL
              JOIN dbo.Recipes cr ON cr.RecipeId=rd.ChildRecipeId
              LEFT JOIN dbo.PreparedItems pi ON pi.PreparedItemId=cr.PreparedItemId
              LEFT JOIN dbo.StoreInventories si ON si.StoreId=o.StoreId AND si.RecipeId=cr.RecipeId
                   AND si.PreparedItemId=cr.PreparedItemId AND si.BtpIdentityState=1
              WHERE r.ToppingId=ot.ToppingId AND r.Active=1 AND r.Status=N'Active'
                AND (cr.PreparedItemId IS NULL OR pi.PreparedItemId IS NULL OR rd.UnitId<>pi.BaseUnitId OR si.StoreInventoryId IS NULL)
          )
    ) THROW 53482,N'DEMO_REORDER_V14: có Incomplete topping không được giải thích bởi legacy BTP identity.',1;

    /* Reorder prerequisites: 50 ingredients/store, min threshold + real 30-day usage + active offer/scope. */
    IF EXISTS(
        SELECT 1
        FROM dbo.InventoryDocumentDetails seedLine
        JOIN dbo.InventoryDocuments seedDoc ON seedDoc.InventoryDocumentId=seedLine.InventoryDocumentId AND seedDoc.RequestKey=N'DEMO_OPENING_STORE1_INGREDIENTS'
        JOIN dbo.Ingredients i ON i.IngredientId=seedLine.IngredientId AND i.Active=1
        CROSS JOIN (VALUES(@Store1Id),(@Store3Id))s(StoreId)
        LEFT JOIN dbo.StoreInventories si ON si.StoreId=s.StoreId AND si.IngredientId=i.IngredientId
        WHERE si.StoreInventoryId IS NULL OR si.MinStockLevel IS NULL
    ) THROW 53468,N'DEMO_REORDER_V14: Reorder prerequisite thiếu StoreInventory/minimum threshold.',1;

    /* This batch deliberately never inserts PA/PO/Receiving nor SalesCostGap. */

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF CURSOR_STATUS('local','prod_cursor')>=0
    BEGIN
        CLOSE prod_cursor;
        DEALLOCATE prod_cursor;
    END;
    IF CURSOR_STATUS('local','topping_store_cursor')>=0
    BEGIN
        CLOSE topping_store_cursor;
        DEALLOCATE topping_store_cursor;
    END;
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
END;
GO

SELECT N'DEMO_REORDER_V14' AS SeedMarker,
       SYSUTCDATETIME() AS VerifiedAtUtc,
       (SELECT COUNT(*) FROM dbo.Orders o JOIN dbo.Stores s ON s.StoreId=o.StoreId WHERE o.Source=N'DEMO_REORDER_V14' AND s.Name=N'CafeChain Thủ Dầu Một') AS Store1Orders,
       (SELECT COUNT(*) FROM dbo.Orders o JOIN dbo.Stores s ON s.StoreId=o.StoreId WHERE o.Source=N'DEMO_REORDER_V14' AND s.Name=N'CafeChain Dĩ An') AS Store3Orders,
       (SELECT COUNT(*) FROM dbo.ProductionRuns WHERE Notes LIKE N'DEMO_REORDER_V14_PROD_S%') AS ProductionRuns,
       (SELECT COUNT(*) FROM dbo.WorkShifts WHERE DiscrepancyReason LIKE N'DEMO_REORDER_V14_SHIFT_S%') AS WorkShifts,
       (SELECT COUNT(*) FROM dbo.Payments WHERE TransactionCode LIKE N'DEMO_REORDER_V14_PAY_S%') AS Payments;
GO

/* ================================================================
   SUPPLIER COMPARISON HISTORY V1
   Marker: DEMO_SUPPLIER_COMPARISON_HISTORY_V1

   Contract:
   - Every active supplier with a valid Store 1 offer receives five
     completed PO -> confirmed receipt samples in the rolling 180-day
     supplier-quality window.
   - Samples are fully linked to receipt postings, inventory movements
     and FIFO cost layers.
   - Fixed business keys make reruns idempotent. Existing sample dates
     are refreshed, but stock is posted only for receipt lines that do
     not already have a BRANCH_RECEIPT_IN movement.
   - This batch runs before the rolling low-stock fixtures so their
     thresholds are calculated from the final physical stock position.
   ================================================================ */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Stores',N'U') IS NULL
       OR OBJECT_ID(N'dbo.Staffs',N'U') IS NULL
       OR OBJECT_ID(N'dbo.Suppliers',N'U') IS NULL
       OR OBJECT_ID(N'dbo.SupplierStores',N'U') IS NULL
       OR OBJECT_ID(N'dbo.IngredientSuppliers',N'U') IS NULL
       OR OBJECT_ID(N'dbo.Ingredients',N'U') IS NULL
       OR OBJECT_ID(N'dbo.UnitConversions',N'U') IS NULL
       OR OBJECT_ID(N'dbo.PurchaseOrders',N'U') IS NULL
       OR OBJECT_ID(N'dbo.PurchaseOrderLines',N'U') IS NULL
       OR OBJECT_ID(N'dbo.BranchReceipts',N'U') IS NULL
       OR OBJECT_ID(N'dbo.BranchReceiptLines',N'U') IS NULL
       OR OBJECT_ID(N'dbo.PurchaseOrderReceiptPostings',N'U') IS NULL
       OR OBJECT_ID(N'dbo.StoreInventories',N'U') IS NULL
       OR OBJECT_ID(N'dbo.InventoryTransactions',N'U') IS NULL
       OR OBJECT_ID(N'dbo.InventoryCostLayers',N'U') IS NULL
       OR OBJECT_ID(N'dbo.SystemSettings',N'U') IS NULL
        THROW 53540,N'DEMO_SUPPLIER_COMPARISON_HISTORY_V1: schema thiếu bảng bắt buộc.',1;

    DECLARE @SupplierComparisonNow datetime2(0)=SYSUTCDATETIME();
    DECLARE @SupplierComparisonStoreId int=(
        SELECT TOP(1) StoreId
        FROM dbo.Stores
        WHERE Name=N'CafeChain Thủ Dầu Một' AND Active=1
        ORDER BY StoreId);
    DECLARE @SupplierComparisonStaffId int=(
        SELECT TOP(1) StaffId
        FROM dbo.Staffs
        WHERE StoreId=@SupplierComparisonStoreId AND Active=1
        ORDER BY StaffId);

    IF @SupplierComparisonStoreId IS NULL OR @SupplierComparisonStaffId IS NULL
        THROW 53541,N'DEMO_SUPPLIER_COMPARISON_HISTORY_V1: thiếu cửa hàng pilot hoặc nhân viên active.',1;

    DECLARE @SupplierComparisonSlots TABLE
    (
        SampleNo int NOT NULL PRIMARY KEY,
        DaysAgo int NOT NULL UNIQUE
    );
    INSERT @SupplierComparisonSlots VALUES (1,155),(2,125),(3,95),(4,65),(5,35);

    DECLARE @SupplierComparisonOffers TABLE
    (
        SupplierId int NOT NULL PRIMARY KEY,
        IngredientSupplierId int NOT NULL UNIQUE,
        IngredientId int NOT NULL,
        PackageUnitId int NOT NULL,
        BaseUnitId int NOT NULL,
        PackageQuantity decimal(18,3) NOT NULL,
        PackageBaseQuantity decimal(18,3) NOT NULL,
        PackagePrice decimal(18,2) NOT NULL,
        LeadTimeDays int NOT NULL,
        StoreInventoryId int NOT NULL
    );

    ;WITH EligibleOffers AS
    (
        SELECT offer.SupplierId,offer.IngredientSupplierId,offer.IngredientId,
               offer.UnitId PackageUnitId,ingredient.BaseUnitId,
               CONVERT(decimal(18,3),ROUND(offer.PackageQuantity,3)) PackageQuantity,
               CONVERT(decimal(18,3),ROUND(offer.PackageQuantity*factor.FactorToBase,3)) PackageBaseQuantity,
               offer.CurrentPrice PackagePrice,COALESCE(offer.LeadTimeDays,0) LeadTimeDays,
               inventory.StoreInventoryId,
               ROW_NUMBER() OVER(PARTITION BY offer.SupplierId ORDER BY offer.IngredientSupplierId) RowNo
        FROM dbo.IngredientSuppliers offer
        JOIN dbo.Suppliers supplier ON supplier.SupplierId=offer.SupplierId AND supplier.Active=1
        JOIN dbo.SupplierStores scope ON scope.SupplierId=offer.SupplierId
            AND scope.StoreId=@SupplierComparisonStoreId AND scope.Active=1
        JOIN dbo.Ingredients ingredient ON ingredient.IngredientId=offer.IngredientId AND ingredient.Active=1
        JOIN dbo.StoreInventories inventory ON inventory.StoreId=@SupplierComparisonStoreId
            AND inventory.IngredientId=offer.IngredientId AND inventory.PreparedItemId IS NULL
        OUTER APPLY
        (
            SELECT CONVERT(decimal(18,8),CASE
                WHEN offer.UnitId=ingredient.BaseUnitId THEN 1
                ELSE
                (
                    SELECT TOP(1) conversion.ToQuantity/NULLIF(conversion.FromQuantity,0)
                    FROM dbo.UnitConversions conversion
                    WHERE conversion.IngredientId=ingredient.IngredientId
                      AND conversion.FromUnitId=offer.UnitId
                      AND conversion.ToUnitId=ingredient.BaseUnitId
                      AND conversion.Active=1
                      AND conversion.FromQuantity>0
                      AND conversion.ToQuantity>0
                    ORDER BY conversion.UnitConversionId
                ) END) FactorToBase
        ) factor
        WHERE offer.Active=1
          AND offer.PackageQuantity>0
          AND offer.CurrentPrice>0
          AND factor.FactorToBase>0
    )
    INSERT @SupplierComparisonOffers
    (SupplierId,IngredientSupplierId,IngredientId,PackageUnitId,BaseUnitId,
     PackageQuantity,PackageBaseQuantity,PackagePrice,LeadTimeDays,StoreInventoryId)
    SELECT SupplierId,IngredientSupplierId,IngredientId,PackageUnitId,BaseUnitId,
           PackageQuantity,PackageBaseQuantity,PackagePrice,LeadTimeDays,StoreInventoryId
    FROM EligibleOffers
    WHERE RowNo=1;

    IF NOT EXISTS(SELECT 1 FROM @SupplierComparisonOffers)
       OR EXISTS(SELECT 1 FROM @SupplierComparisonOffers
                 WHERE PackageQuantity<=0 OR PackageBaseQuantity<=0 OR PackagePrice<=0)
       OR EXISTS
       (
           SELECT 1
           FROM dbo.Suppliers supplier
           JOIN dbo.SupplierStores scope ON scope.SupplierId=supplier.SupplierId
               AND scope.StoreId=@SupplierComparisonStoreId AND scope.Active=1
           WHERE supplier.Active=1
             AND EXISTS(SELECT 1 FROM dbo.IngredientSuppliers offer
                        WHERE offer.SupplierId=supplier.SupplierId AND offer.Active=1
                          AND offer.PackageQuantity>0 AND offer.CurrentPrice>0)
             AND NOT EXISTS(SELECT 1 FROM @SupplierComparisonOffers fixture
                            WHERE fixture.SupplierId=supplier.SupplierId)
       )
        THROW 53542,N'DEMO_SUPPLIER_COMPARISON_HISTORY_V1: không resolve đủ offer, quy đổi hoặc tồn kho cho nhà cung cấp pilot.',1;

    DECLARE @SupplierComparisonFixture TABLE
    (
        SupplierId int NOT NULL,
        IngredientSupplierId int NOT NULL,
        IngredientId int NOT NULL,
        PackageUnitId int NOT NULL,
        BaseUnitId int NOT NULL,
        PackageQuantity decimal(18,3) NOT NULL,
        PackageBaseQuantity decimal(18,3) NOT NULL,
        PackagePrice decimal(18,2) NOT NULL,
        LeadTimeDays int NOT NULL,
        StoreInventoryId int NOT NULL,
        SampleNo int NOT NULL,
        OrderAt datetime2(0) NOT NULL,
        ExpectedAt datetime2(0) NOT NULL,
        ReceivedAt datetime2(0) NOT NULL,
        PoCode nvarchar(50) NOT NULL UNIQUE,
        ReceiptCode nvarchar(50) NOT NULL UNIQUE,
        ReceiptKey nvarchar(100) NOT NULL UNIQUE,
        LineNote nvarchar(100) NOT NULL UNIQUE,
        PRIMARY KEY(SupplierId,SampleNo)
    );
    INSERT @SupplierComparisonFixture
    SELECT offer.SupplierId,offer.IngredientSupplierId,offer.IngredientId,
           offer.PackageUnitId,offer.BaseUnitId,offer.PackageQuantity,
           offer.PackageBaseQuantity,offer.PackagePrice,offer.LeadTimeDays,
           offer.StoreInventoryId,slot.SampleNo,
           DATEADD(DAY,-offer.LeadTimeDays,DATEADD(DAY,-slot.DaysAgo,@SupplierComparisonNow)),
           DATEADD(DAY,-slot.DaysAgo,@SupplierComparisonNow),
           DATEADD(DAY,-slot.DaysAgo,@SupplierComparisonNow),
           CONCAT(N'DEMO-SCMP-V1-PO-',offer.SupplierId,N'-',slot.SampleNo),
           CONCAT(N'DEMO-SCMP-V1-BR-',offer.SupplierId,N'-',slot.SampleNo),
           CONCAT(N'DEMO_SCMP_V1_RECEIPT_S',@SupplierComparisonStoreId,N'_SUP',offer.SupplierId,N'_',slot.SampleNo),
           CONCAT(N'DEMO_SCMP_V1_LINE_SUP',offer.SupplierId,N'_',slot.SampleNo)
    FROM @SupplierComparisonOffers offer
    CROSS JOIN @SupplierComparisonSlots slot;

    INSERT dbo.PurchaseOrders
    (Code,StoreId,SupplierId,[Status],OrderDate,ExpectedDeliveryAtUtc,CreatedByStaffId,
     ApprovedByStaffId,SentByStaffId,CreatedAtUtc,UpdatedAtUtc,ApprovedAtUtc,SentAtUtc,
     CompletedAtUtc,CancelledAtUtc,Note)
    SELECT fixture.PoCode,@SupplierComparisonStoreId,fixture.SupplierId,N'COMPLETED',
           fixture.OrderAt,fixture.ExpectedAt,@SupplierComparisonStaffId,
           @SupplierComparisonStaffId,@SupplierComparisonStaffId,fixture.OrderAt,fixture.ReceivedAt,
           fixture.OrderAt,fixture.OrderAt,fixture.ReceivedAt,NULL,
           N'DEMO_SUPPLIER_COMPARISON_HISTORY_V1'
    FROM @SupplierComparisonFixture fixture
    WHERE NOT EXISTS(SELECT 1 FROM dbo.PurchaseOrders po WHERE po.Code=fixture.PoCode);

    INSERT dbo.PurchaseOrderLines
    (PurchaseOrderId,RestockRequestId,PurchaseAdviceLineId,IngredientId,IngredientSupplierId,
     PackageUnitIdSnapshot,PackageQuantitySnapshot,PackagePriceSnapshot,PackageCount,
     PurchaseMode,OrderedPackageCount,OrderedBaseQuantity,OrderedPackQuantity,
     PackSizeProcurementQuantity,ProcurementUnitId,OrderedProcurementQuantity,
     UnitPricePerPackage,UnitPricePerProcurementUnit,RoundingSurplusProcurementQuantity,
     AcceptedPackQuantity,AcceptedProcurementQuantity,ClosedProcurementQuantity,
     InventoryPostingBaseQuantity,InventoryBaseUnitId,ProcurementToInventoryFactor,
     ClosedRemainingQuantity,PromisedLeadTimeDaysSnapshot,Note)
    SELECT po.PurchaseOrderId,NULL,NULL,fixture.IngredientId,fixture.IngredientSupplierId,
           fixture.PackageUnitId,fixture.PackageQuantity,fixture.PackagePrice,1,
           N'Packaged',1,fixture.PackageBaseQuantity,1,
           NULL,NULL,NULL,fixture.PackagePrice,NULL,0,
           1,NULL,0,fixture.PackageBaseQuantity,fixture.BaseUnitId,fixture.PackageBaseQuantity,
           0,fixture.LeadTimeDays,fixture.LineNote
    FROM @SupplierComparisonFixture fixture
    JOIN dbo.PurchaseOrders po ON po.Code=fixture.PoCode
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.PurchaseOrderLines line
        WHERE line.PurchaseOrderId=po.PurchaseOrderId AND line.Note=fixture.LineNote
    );

    INSERT dbo.BranchReceipts
    (ReceiptCode,StoreId,SupplierId,PurchaseOrderId,SourceInventoryTransferId,[Status],ReceiptKey,
     ReferenceNumber,ReceivedAt,ReceivedByStaffId,ConfirmedAt,ConfirmedByStaffId,Notes,
     CreatedAt,CreatedByStaffId)
    SELECT fixture.ReceiptCode,@SupplierComparisonStoreId,fixture.SupplierId,po.PurchaseOrderId,
           NULL,N'CONFIRMED',fixture.ReceiptKey,
           CONCAT(N'DEMO-SCMP-INVOICE-',fixture.SupplierId,N'-',fixture.SampleNo),
           fixture.ReceivedAt,@SupplierComparisonStaffId,fixture.ReceivedAt,@SupplierComparisonStaffId,
           N'DEMO_SUPPLIER_COMPARISON_HISTORY_V1',fixture.ReceivedAt,@SupplierComparisonStaffId
    FROM @SupplierComparisonFixture fixture
    JOIN dbo.PurchaseOrders po ON po.Code=fixture.PoCode
    WHERE NOT EXISTS(SELECT 1 FROM dbo.BranchReceipts receipt
                     WHERE receipt.ReceiptCode=fixture.ReceiptCode);

    INSERT dbo.BranchReceiptLines
    (BranchReceiptId,RestockRequestId,PurchaseOrderLineId,SourceInventoryTransferDetailId,
     SourceTransferCostAllocationId,RestockRequestFulfillmentId,IngredientId,PreparedItemId,RecipeId,
     InputQuantity,InputUnitId,ReceivedBaseQuantity,RejectedBaseQuantity,ReceivedPackQuantity,
     AcceptedPackQuantity,ReceivedProcurementQuantity,RejectedProcurementQuantity,
     AcceptedProcurementQuantity,InventoryPostingBaseQuantity,ProcurementUnitId,InventoryBaseUnitId,
     ProcurementToInventoryFactor,PurchaseMode,RejectionReason,RejectionIssueType,BaseUnitId,
     SupplierId,IngredientSupplierId,ActualPackagePrice,PackageQuantitySnapshot,
     PackageUnitIdSnapshot,BaseUnitCostSnapshot,LineTotalCost,InventoryTransactionId,CreatedAt)
    SELECT receipt.BranchReceiptId,NULL,line.PurchaseOrderLineId,NULL,NULL,NULL,
           fixture.IngredientId,NULL,NULL,1,fixture.PackageUnitId,fixture.PackageBaseQuantity,0,1,1,
           NULL,NULL,NULL,fixture.PackageBaseQuantity,NULL,fixture.BaseUnitId,
           fixture.PackageBaseQuantity,N'Packaged',NULL,NULL,fixture.BaseUnitId,
           fixture.SupplierId,fixture.IngredientSupplierId,fixture.PackagePrice,fixture.PackageQuantity,
           fixture.PackageUnitId,fixture.PackagePrice/NULLIF(fixture.PackageBaseQuantity,0),
           fixture.PackagePrice,NULL,fixture.ReceivedAt
    FROM @SupplierComparisonFixture fixture
    JOIN dbo.PurchaseOrders po ON po.Code=fixture.PoCode
    JOIN dbo.PurchaseOrderLines line ON line.PurchaseOrderId=po.PurchaseOrderId
        AND line.Note=fixture.LineNote
    JOIN dbo.BranchReceipts receipt ON receipt.ReceiptCode=fixture.ReceiptCode
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.BranchReceiptLines receiptLine
        WHERE receiptLine.BranchReceiptId=receipt.BranchReceiptId
          AND receiptLine.PurchaseOrderLineId=line.PurchaseOrderLineId
    );

    INSERT dbo.PurchaseOrderReceiptPostings
    (PurchaseOrderLineId,BranchReceiptLineId,AcceptedBaseQuantity,RejectedBaseQuantity,
     AcceptedProcurementQuantity,RejectedProcurementQuantity,InventoryPostingBaseQuantity,
     ProcurementUnitId,InventoryBaseUnitId,ProcurementToInventoryFactor,PurchaseMode,
     CreatedByStaffId,CreatedAtUtc)
    SELECT line.PurchaseOrderLineId,receiptLine.BranchReceiptLineId,
           fixture.PackageBaseQuantity,0,NULL,NULL,fixture.PackageBaseQuantity,
           NULL,fixture.BaseUnitId,fixture.PackageBaseQuantity,N'Packaged',
           @SupplierComparisonStaffId,fixture.ReceivedAt
    FROM @SupplierComparisonFixture fixture
    JOIN dbo.PurchaseOrders po ON po.Code=fixture.PoCode
    JOIN dbo.PurchaseOrderLines line ON line.PurchaseOrderId=po.PurchaseOrderId
        AND line.Note=fixture.LineNote
    JOIN dbo.BranchReceipts receipt ON receipt.ReceiptCode=fixture.ReceiptCode
    JOIN dbo.BranchReceiptLines receiptLine ON receiptLine.BranchReceiptId=receipt.BranchReceiptId
        AND receiptLine.PurchaseOrderLineId=line.PurchaseOrderLineId
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.PurchaseOrderReceiptPostings posting
        WHERE posting.BranchReceiptLineId=receiptLine.BranchReceiptLineId
    );

    DECLARE @SupplierComparisonPendingInventory TABLE
    (
        BranchReceiptLineId int NOT NULL PRIMARY KEY,
        StoreInventoryId int NOT NULL,
        IngredientId int NOT NULL,
        Quantity decimal(18,3) NOT NULL,
        UnitCost decimal(18,2) NOT NULL,
        TotalCost decimal(18,2) NOT NULL,
        CreatedAt datetime2(0) NOT NULL,
        RunningBefore decimal(18,3) NOT NULL,
        RunningAfter decimal(18,3) NOT NULL
    );
    INSERT @SupplierComparisonPendingInventory
    SELECT receiptLine.BranchReceiptLineId,fixture.StoreInventoryId,fixture.IngredientId,
           fixture.PackageBaseQuantity,
           CONVERT(decimal(18,2),ROUND(fixture.PackagePrice/NULLIF(fixture.PackageBaseQuantity,0),2)),
           fixture.PackagePrice,fixture.ReceivedAt,
           SUM(fixture.PackageBaseQuantity) OVER
           (PARTITION BY fixture.StoreInventoryId ORDER BY fixture.ReceivedAt,receiptLine.BranchReceiptLineId
            ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)-fixture.PackageBaseQuantity,
           SUM(fixture.PackageBaseQuantity) OVER
           (PARTITION BY fixture.StoreInventoryId ORDER BY fixture.ReceivedAt,receiptLine.BranchReceiptLineId
            ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)
    FROM @SupplierComparisonFixture fixture
    JOIN dbo.BranchReceipts receipt ON receipt.ReceiptCode=fixture.ReceiptCode
    JOIN dbo.BranchReceiptLines receiptLine ON receiptLine.BranchReceiptId=receipt.BranchReceiptId
        AND receiptLine.IngredientSupplierId=fixture.IngredientSupplierId
    WHERE receiptLine.InventoryTransactionId IS NULL
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.InventoryTransactions movement
          WHERE movement.BranchReceiptLineId=receiptLine.BranchReceiptLineId AND movement.[Type]=14
      );

    INSERT dbo.InventoryTransactions
    (StoreInventoryId,[Type],StockStatus,Quantity,BeforeQty,AfterQty,UnitCost,TotalCost,
     InventoryDocumentId,InventoryDocumentDetailId,InventoryTransferId,InventoryTransferDetailId,
     ReferenceOrderId,ProductionRunId,SourceRecipeId,InventoryConsolidationRunId,
     BranchReceiptLineId,OrderRefundId,CreatedAt)
    SELECT pending.StoreInventoryId,14,1,pending.Quantity,
           inventory.AvailableQty+pending.RunningBefore,
           inventory.AvailableQty+pending.RunningAfter,
           pending.UnitCost,pending.TotalCost,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,
           pending.BranchReceiptLineId,NULL,pending.CreatedAt
    FROM @SupplierComparisonPendingInventory pending
    JOIN dbo.StoreInventories inventory ON inventory.StoreInventoryId=pending.StoreInventoryId;

    UPDATE inventory
       SET inventory.AvailableQty=inventory.AvailableQty+added.TotalQuantity,
           inventory.LastUpdated=@SupplierComparisonNow
    FROM dbo.StoreInventories inventory
    JOIN
    (
        SELECT StoreInventoryId,SUM(Quantity) TotalQuantity
        FROM @SupplierComparisonPendingInventory
        GROUP BY StoreInventoryId
    ) added ON added.StoreInventoryId=inventory.StoreInventoryId;

    UPDATE receiptLine
       SET receiptLine.InventoryTransactionId=movement.InventoryTransactionId
    FROM dbo.BranchReceiptLines receiptLine
    JOIN dbo.InventoryTransactions movement
      ON movement.BranchReceiptLineId=receiptLine.BranchReceiptLineId AND movement.[Type]=14
    JOIN dbo.BranchReceipts receipt ON receipt.BranchReceiptId=receiptLine.BranchReceiptId
    WHERE receipt.Notes=N'DEMO_SUPPLIER_COMPARISON_HISTORY_V1'
      AND receiptLine.InventoryTransactionId IS NULL;

    INSERT dbo.InventoryCostLayers
    (IngredientId,PreparedItemId,StoreId,Quantity,RemainingQuantity,UnitCost,CreatedAt,
     SourceProductionRunId,SourceOrderRefundId,SourceInventoryDocumentDetailId,
     SourceBranchReceiptLineId,SourceTransferCostAllocationId,SourceTransferDiscrepancyPostingId)
    SELECT fixture.IngredientId,NULL,@SupplierComparisonStoreId,
           fixture.PackageBaseQuantity,fixture.PackageBaseQuantity,
           CONVERT(decimal(18,2),ROUND(fixture.PackagePrice/NULLIF(fixture.PackageBaseQuantity,0),2)),
           fixture.ReceivedAt,NULL,NULL,NULL,receiptLine.BranchReceiptLineId,NULL,NULL
    FROM @SupplierComparisonFixture fixture
    JOIN dbo.BranchReceipts receipt ON receipt.ReceiptCode=fixture.ReceiptCode
    JOIN dbo.BranchReceiptLines receiptLine ON receiptLine.BranchReceiptId=receipt.BranchReceiptId
        AND receiptLine.IngredientSupplierId=fixture.IngredientSupplierId
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.InventoryCostLayers layer
        WHERE layer.SourceBranchReceiptLineId=receiptLine.BranchReceiptLineId
    );

    /* Refresh only seed-owned rolling dates. Quantities and stock remain unchanged. */
    UPDATE po
       SET po.[Status]=N'COMPLETED',po.OrderDate=fixture.OrderAt,
           po.ExpectedDeliveryAtUtc=fixture.ExpectedAt,po.UpdatedAtUtc=fixture.ReceivedAt,
           po.CompletedAtUtc=fixture.ReceivedAt
    FROM dbo.PurchaseOrders po
    JOIN @SupplierComparisonFixture fixture ON fixture.PoCode=po.Code
    WHERE po.Note=N'DEMO_SUPPLIER_COMPARISON_HISTORY_V1';

    UPDATE receipt
       SET receipt.[Status]=N'CONFIRMED',receipt.ReceivedAt=fixture.ReceivedAt,
           receipt.ConfirmedAt=fixture.ReceivedAt,receipt.CreatedAt=fixture.ReceivedAt
    FROM dbo.BranchReceipts receipt
    JOIN @SupplierComparisonFixture fixture ON fixture.ReceiptCode=receipt.ReceiptCode
    WHERE receipt.Notes=N'DEMO_SUPPLIER_COMPARISON_HISTORY_V1';

    UPDATE receiptLine SET receiptLine.CreatedAt=fixture.ReceivedAt
    FROM dbo.BranchReceiptLines receiptLine
    JOIN dbo.BranchReceipts receipt ON receipt.BranchReceiptId=receiptLine.BranchReceiptId
    JOIN @SupplierComparisonFixture fixture ON fixture.ReceiptCode=receipt.ReceiptCode
    WHERE receipt.Notes=N'DEMO_SUPPLIER_COMPARISON_HISTORY_V1';

    UPDATE posting SET posting.CreatedAtUtc=fixture.ReceivedAt
    FROM dbo.PurchaseOrderReceiptPostings posting
    JOIN dbo.BranchReceiptLines receiptLine ON receiptLine.BranchReceiptLineId=posting.BranchReceiptLineId
    JOIN dbo.BranchReceipts receipt ON receipt.BranchReceiptId=receiptLine.BranchReceiptId
    JOIN @SupplierComparisonFixture fixture ON fixture.ReceiptCode=receipt.ReceiptCode
    WHERE receipt.Notes=N'DEMO_SUPPLIER_COMPARISON_HISTORY_V1';

    UPDATE movement SET movement.CreatedAt=fixture.ReceivedAt
    FROM dbo.InventoryTransactions movement
    JOIN dbo.BranchReceiptLines receiptLine ON receiptLine.BranchReceiptLineId=movement.BranchReceiptLineId
    JOIN dbo.BranchReceipts receipt ON receipt.BranchReceiptId=receiptLine.BranchReceiptId
    JOIN @SupplierComparisonFixture fixture ON fixture.ReceiptCode=receipt.ReceiptCode
    WHERE receipt.Notes=N'DEMO_SUPPLIER_COMPARISON_HISTORY_V1' AND movement.[Type]=14;

    UPDATE layer SET layer.CreatedAt=fixture.ReceivedAt
    FROM dbo.InventoryCostLayers layer
    JOIN dbo.BranchReceiptLines receiptLine ON receiptLine.BranchReceiptLineId=layer.SourceBranchReceiptLineId
    JOIN dbo.BranchReceipts receipt ON receipt.BranchReceiptId=receiptLine.BranchReceiptId
    JOIN @SupplierComparisonFixture fixture ON fixture.ReceiptCode=receipt.ReceiptCode
    WHERE receipt.Notes=N'DEMO_SUPPLIER_COMPARISON_HISTORY_V1';

    DECLARE @SupplierComparisonExpectedCount int=(SELECT COUNT(*) FROM @SupplierComparisonOffers)*5;
    IF (SELECT COUNT(*) FROM dbo.PurchaseOrders
        WHERE Note=N'DEMO_SUPPLIER_COMPARISON_HISTORY_V1')<>@SupplierComparisonExpectedCount
       OR (SELECT COUNT(*) FROM dbo.BranchReceipts
           WHERE Notes=N'DEMO_SUPPLIER_COMPARISON_HISTORY_V1')<>@SupplierComparisonExpectedCount
       OR (SELECT COUNT(*) FROM dbo.BranchReceiptLines receiptLine
           JOIN dbo.BranchReceipts receipt ON receipt.BranchReceiptId=receiptLine.BranchReceiptId
           WHERE receipt.Notes=N'DEMO_SUPPLIER_COMPARISON_HISTORY_V1')<>@SupplierComparisonExpectedCount
       OR (SELECT COUNT(*) FROM dbo.PurchaseOrderReceiptPostings posting
           JOIN dbo.BranchReceiptLines receiptLine ON receiptLine.BranchReceiptLineId=posting.BranchReceiptLineId
           JOIN dbo.BranchReceipts receipt ON receipt.BranchReceiptId=receiptLine.BranchReceiptId
           WHERE receipt.Notes=N'DEMO_SUPPLIER_COMPARISON_HISTORY_V1')<>@SupplierComparisonExpectedCount
       OR (SELECT COUNT(*) FROM dbo.InventoryTransactions movement
           JOIN dbo.BranchReceiptLines receiptLine ON receiptLine.BranchReceiptLineId=movement.BranchReceiptLineId
           JOIN dbo.BranchReceipts receipt ON receipt.BranchReceiptId=receiptLine.BranchReceiptId
           WHERE receipt.Notes=N'DEMO_SUPPLIER_COMPARISON_HISTORY_V1' AND movement.[Type]=14)<>@SupplierComparisonExpectedCount
       OR (SELECT COUNT(*) FROM dbo.InventoryCostLayers layer
           JOIN dbo.BranchReceiptLines receiptLine ON receiptLine.BranchReceiptLineId=layer.SourceBranchReceiptLineId
           JOIN dbo.BranchReceipts receipt ON receipt.BranchReceiptId=receiptLine.BranchReceiptId
           WHERE receipt.Notes=N'DEMO_SUPPLIER_COMPARISON_HISTORY_V1')<>@SupplierComparisonExpectedCount
        THROW 53543,N'DEMO_SUPPLIER_COMPARISON_HISTORY_V1: số chứng từ hoặc bằng chứng tồn kho không đúng contract.',1;

    IF EXISTS
    (
        SELECT fixture.SupplierId
        FROM @SupplierComparisonOffers fixture
        LEFT JOIN dbo.BranchReceipts receipt ON receipt.SupplierId=fixture.SupplierId
            AND receipt.StoreId=@SupplierComparisonStoreId
            AND receipt.[Status]=N'CONFIRMED'
            AND receipt.ReceivedAt>=DATEADD(DAY,-180,@SupplierComparisonNow)
            AND receipt.ReceivedAt<DATEADD(DAY,1,@SupplierComparisonNow)
        GROUP BY fixture.SupplierId
        HAVING COUNT(receipt.BranchReceiptId)<5
    ) OR EXISTS
    (
        SELECT fixture.SupplierId
        FROM @SupplierComparisonOffers fixture
        LEFT JOIN dbo.PurchaseOrders po ON po.SupplierId=fixture.SupplierId
            AND po.StoreId=@SupplierComparisonStoreId
            AND po.[Status]=N'COMPLETED'
            AND po.ExpectedDeliveryAtUtc IS NOT NULL
            AND po.CompletedAtUtc>=DATEADD(DAY,-180,@SupplierComparisonNow)
            AND po.CompletedAtUtc<DATEADD(DAY,1,@SupplierComparisonNow)
            AND EXISTS
            (
                SELECT 1 FROM dbo.PurchaseOrderLines line
                JOIN dbo.PurchaseOrderReceiptPostings posting
                  ON posting.PurchaseOrderLineId=line.PurchaseOrderLineId
                WHERE line.PurchaseOrderId=po.PurchaseOrderId
            )
        GROUP BY fixture.SupplierId
        HAVING COUNT(po.PurchaseOrderId)<5
    )
        THROW 53544,N'DEMO_SUPPLIER_COMPARISON_HISTORY_V1: nhà cung cấp pilot chưa đủ 5 mẫu nhận hàng và ngày giao dự kiến.',1;

    IF NOT EXISTS(SELECT 1 FROM dbo.SystemSettings
                  WHERE SettingKey=N'seedall_supplier_comparison_history_v1')
        INSERT dbo.SystemSettings(SettingKey,SettingValue,Description)
        VALUES(N'seedall_supplier_comparison_history_v1',N'completed',
               N'Đã tạo 5 mẫu nhập hàng trong 180 ngày cho mọi nhà cung cấp tại cửa hàng pilot.');
    ELSE
        UPDATE dbo.SystemSettings
           SET SettingValue=N'completed'
         WHERE SettingKey=N'seedall_supplier_comparison_history_v1';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT N'DEMO_SUPPLIER_COMPARISON_HISTORY_V1' AS SeedMarker,
       SYSUTCDATETIME() AS VerifiedAtUtc,
       (SELECT COUNT(*) FROM dbo.PurchaseOrders
        WHERE Note=N'DEMO_SUPPLIER_COMPARISON_HISTORY_V1') AS DemoPurchaseOrders,
       (SELECT COUNT(*) FROM dbo.BranchReceipts
        WHERE Notes=N'DEMO_SUPPLIER_COMPARISON_HISTORY_V1') AS DemoConfirmedReceipts;
GO

/* ================================================================
   BATCH 15 - AI DASHBOARD ROLLING FIXTURE
   Marker: DEMO_AI_DASHBOARD_ROLLING_V1

   This fixture complements the fixed V13 contract with current-window
   order status/refund, waste, reorder and procurement evidence. It does
   not alter POS flow or existing user data.
   ================================================================ */
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @AiDay datetime2(0)=DATEADD(DAY,DATEDIFF(DAY,0,SYSUTCDATETIME()),0);
    DECLARE @AiStore1StaffId int=(SELECT TOP(1) StaffId FROM dbo.Staffs WHERE StoreId=1 AND Active=1 ORDER BY StaffId);
    DECLARE @AiStore3StaffId int=(SELECT TOP(1) StaffId FROM dbo.Staffs WHERE StoreId=3 AND Active=1 ORDER BY StaffId);
    DECLARE @AiCoffeeIngredientId int=(SELECT IngredientId FROM dbo.Ingredients WHERE Code=N'DEMO_ING_VIET_COFFEE');
    DECLARE @AiCoffeeOfferId int=(SELECT IngredientSupplierId FROM dbo.IngredientSuppliers WHERE IngredientSupplierId=10 AND Active=1);
    DECLARE @AiCoffeeSupplierId int=(SELECT SupplierId FROM dbo.IngredientSuppliers WHERE IngredientSupplierId=@AiCoffeeOfferId);

    IF @AiStore1StaffId IS NULL OR @AiStore3StaffId IS NULL OR @AiCoffeeIngredientId IS NULL
        THROW 53500,N'AI rolling fixture requires active staff at Store 1/3 and coffee ingredient.',1;

    DECLARE @AiOrders TABLE
    (
        ClientOrderId uniqueidentifier PRIMARY KEY,
        StoreId int NOT NULL,
        StaffId int NOT NULL,
        CreatedAt datetime2(0) NOT NULL,
        OrderStatusId int NOT NULL,
        PaymentStatusId int NOT NULL,
        PaymentMethodId int NOT NULL,
        DrinkCode nvarchar(50) NOT NULL,
        SizeCode nvarchar(20) NOT NULL,
        Quantity int NOT NULL,
        Total decimal(18,2) NOT NULL,
        CostStatus int NOT NULL,
        TotalCogs decimal(18,2) NULL,
        RefundKey uniqueidentifier NULL
    );

    INSERT @AiOrders VALUES
    ('41000000-0000-0000-0001-000000000001',1,@AiStore1StaffId,DATEADD(DAY,-1,@AiDay),5,2,1,N'CF_BacXiu',N'M',2,100000,1,30000,NULL),
    ('41000000-0000-0000-0001-000000000002',1,@AiStore1StaffId,DATEADD(DAY,-2,@AiDay),5,2,2,N'CF_Latte',N'L',1,80000,1,25000,'42000000-0000-0000-0001-000000000002'),
    ('41000000-0000-0000-0001-000000000003',1,@AiStore1StaffId,DATEADD(DAY,-3,@AiDay),5,2,3,N'TS_Matcha',N'M',1,60000,1,20000,NULL),
    ('41000000-0000-0000-0001-000000000004',1,@AiStore1StaffId,DATEADD(DAY,-4,@AiDay),5,2,1,N'CF_BacXiu',N'M',1,70000,1,22000,NULL),
    ('41000000-0000-0000-0001-000000000005',1,@AiStore1StaffId,DATEADD(DAY,-5,@AiDay),6,1,1,N'CF_BacXiu',N'M',1,33000,0,NULL,NULL),
    ('41000000-0000-0000-0001-000000000006',1,@AiStore1StaffId,DATEADD(DAY,-6,@AiDay),5,2,2,N'CF_Latte',N'L',1,83000,1,28000,NULL),
    ('41000000-0000-0000-0003-000000000001',3,@AiStore3StaffId,DATEADD(DAY,-1,@AiDay),5,2,1,N'CF_BacXiu',N'M',1,33000,1,10000,NULL),
    ('41000000-0000-0000-0003-000000000002',3,@AiStore3StaffId,DATEADD(DAY,-2,@AiDay),5,2,2,N'CF_Latte',N'L',2,100000,1,95000,NULL),
    ('41000000-0000-0000-0003-000000000003',3,@AiStore3StaffId,DATEADD(DAY,-3,@AiDay),5,2,3,N'TS_Matcha',N'M',1,37000,0,NULL,NULL),
    ('41000000-0000-0000-0003-000000000004',3,@AiStore3StaffId,DATEADD(DAY,-4,@AiDay),5,2,1,N'CF_BacXiu',N'M',1,33000,1,10000,'42000000-0000-0000-0003-000000000004'),
    ('41000000-0000-0000-0003-000000000005',3,@AiStore3StaffId,DATEADD(DAY,-5,@AiDay),6,1,1,N'CF_BacXiu',N'M',1,33000,0,NULL,NULL),
    ('41000000-0000-0000-0003-000000000006',3,@AiStore3StaffId,DATEADD(DAY,-6,@AiDay),6,1,2,N'CF_Latte',N'L',1,50000,0,NULL,NULL),
    ('41000000-0000-0000-0001-000000000007',1,@AiStore1StaffId,DATEADD(DAY,-7,@AiDay),5,2,1,N'TS_Matcha',N'M',2,74000,0,NULL,NULL),
    ('41000000-0000-0000-0001-000000000008',1,@AiStore1StaffId,DATEADD(DAY,-8,@AiDay),5,2,2,N'CF_Latte',N'L',1,50000,1,17000,NULL),
    ('41000000-0000-0000-0001-000000000009',1,@AiStore1StaffId,DATEADD(DAY,-9,@AiDay),5,2,3,N'CF_BacXiu',N'M',1,33000,1,10000,NULL),
    ('41000000-0000-0000-0001-000000000010',1,@AiStore1StaffId,DATEADD(DAY,-10,@AiDay),6,1,1,N'TS_Matcha',N'M',1,37000,0,NULL,NULL),
    ('41000000-0000-0000-0001-000000000011',1,@AiStore1StaffId,DATEADD(DAY,-11,@AiDay),5,2,2,N'CF_Latte',N'L',2,100000,1,34000,'42000000-0000-0000-0001-000000000011'),
    ('41000000-0000-0000-0001-000000000012',1,@AiStore1StaffId,DATEADD(DAY,-12,@AiDay),5,2,1,N'CF_BacXiu',N'M',1,33000,1,10000,NULL),
    ('41000000-0000-0000-0001-000000000013',1,@AiStore1StaffId,DATEADD(DAY,-13,@AiDay),6,1,3,N'CF_Latte',N'L',1,50000,0,NULL,NULL),
    ('41000000-0000-0000-0001-000000000014',1,@AiStore1StaffId,DATEADD(DAY,-14,@AiDay),5,2,1,N'TS_Matcha',N'M',1,37000,0,NULL,NULL),
    ('41000000-0000-0000-0001-000000000015',1,@AiStore1StaffId,DATEADD(DAY,-15,@AiDay),5,2,2,N'CF_BacXiu',N'M',2,66000,1,20000,NULL),
    ('41000000-0000-0000-0003-000000000007',3,@AiStore3StaffId,DATEADD(DAY,-7,@AiDay),5,2,1,N'TS_Matcha',N'M',1,37000,0,NULL,NULL),
    ('41000000-0000-0000-0003-000000000008',3,@AiStore3StaffId,DATEADD(DAY,-8,@AiDay),5,2,2,N'CF_Latte',N'L',1,50000,1,17000,NULL),
    ('41000000-0000-0000-0003-000000000009',3,@AiStore3StaffId,DATEADD(DAY,-9,@AiDay),6,1,3,N'CF_BacXiu',N'M',1,33000,0,NULL,NULL),
    ('41000000-0000-0000-0003-000000000010',3,@AiStore3StaffId,DATEADD(DAY,-10,@AiDay),5,2,1,N'CF_BacXiu',N'M',2,66000,1,20000,NULL),
    ('41000000-0000-0000-0003-000000000011',3,@AiStore3StaffId,DATEADD(DAY,-11,@AiDay),5,2,2,N'TS_Matcha',N'M',1,37000,0,NULL,NULL),
    ('41000000-0000-0000-0003-000000000012',3,@AiStore3StaffId,DATEADD(DAY,-12,@AiDay),5,2,3,N'CF_Latte',N'L',2,100000,1,34000,'42000000-0000-0000-0003-000000000012'),
    ('41000000-0000-0000-0003-000000000013',3,@AiStore3StaffId,DATEADD(DAY,-13,@AiDay),6,1,1,N'CF_BacXiu',N'M',1,33000,0,NULL,NULL),
    ('41000000-0000-0000-0003-000000000014',3,@AiStore3StaffId,DATEADD(DAY,-14,@AiDay),5,2,2,N'TS_Matcha',N'M',1,37000,0,NULL,NULL),
    ('41000000-0000-0000-0003-000000000015',3,@AiStore3StaffId,DATEADD(DAY,-15,@AiDay),5,2,1,N'CF_Latte',N'L',1,50000,1,17000,NULL);

    INSERT dbo.Orders
    (
        CustomerId,StoreId,OrderStatusId,PaymentStatusId,OrderTypeId,TableId,StaffId,WorkShiftId,
        ClientOrderId,Source,Note,ShippingFee,SubTotal,VoucherDiscount,PointDiscount,PointsUsed,
        Total,CostStatus,TotalCogs,GrossProfit,CostedAtUtc,CreatedAt
    )
    SELECT NULL,x.StoreId,x.OrderStatusId,x.PaymentStatusId,2,NULL,x.StaffId,NULL,
           x.ClientOrderId,N'DEMO_AI_DASHBOARD_ROLLING_V1',
           CASE WHEN x.StoreId=1 THEN N'AI_DASHBOARD_SCENARIO_NORMAL'
                ELSE N'AI_DASHBOARD_SCENARIO_ANOMALY' END,
           0,x.Total,0,0,0,x.Total,x.CostStatus,
           x.TotalCogs,CASE WHEN x.TotalCogs IS NULL THEN NULL ELSE x.Total-x.TotalCogs END,
           CASE WHEN x.TotalCogs IS NULL THEN NULL ELSE x.CreatedAt END,x.CreatedAt
    FROM @AiOrders x
    WHERE NOT EXISTS(SELECT 1 FROM dbo.Orders o WHERE o.ClientOrderId=x.ClientOrderId);

    /* Rebase the fixture on every replay without inserting duplicate business rows. */
    UPDATE o
       SET o.OrderStatusId=x.OrderStatusId,o.PaymentStatusId=x.PaymentStatusId,
           o.Note=CASE WHEN x.StoreId=1 THEN N'AI_DASHBOARD_SCENARIO_NORMAL'
                       ELSE N'AI_DASHBOARD_SCENARIO_ANOMALY' END,
           o.SubTotal=x.Total,o.Total=x.Total,o.CostStatus=x.CostStatus,o.TotalCogs=x.TotalCogs,
           o.GrossProfit=CASE WHEN x.TotalCogs IS NULL THEN NULL ELSE x.Total-x.TotalCogs END,
           o.CostedAtUtc=CASE WHEN x.TotalCogs IS NULL THEN NULL ELSE x.CreatedAt END,
           o.CreatedAt=x.CreatedAt
    FROM dbo.Orders o JOIN @AiOrders x ON x.ClientOrderId=o.ClientOrderId;

    INSERT dbo.OrderDetails
    (
        OrderId,DrinkId,SizeId,StoreMenuItemId,DrinkSizeId,DrinkName,SizeName,Price,
        AcceptedBasePrice,PriceSource,AcceptedCatalogVersion,Quantity,Note,CostStatus,UnitCogs,TotalCogs
    )
    SELECT o.OrderId,d.DrinkId,s.SizeId,sm.StoreMenuItemId,ds.DrinkSizeId,d.Name,s.Name,
           x.Total/NULLIF(x.Quantity,0),x.Total/NULLIF(x.Quantity,0),
           N'DEMO_AI_DASHBOARD_ROLLING_V1',1,x.Quantity,
           N'AI Dashboard rolling analytics fixture',x.CostStatus,
           CASE WHEN x.TotalCogs IS NULL THEN NULL ELSE x.TotalCogs/NULLIF(x.Quantity,0) END,x.TotalCogs
    FROM @AiOrders x
    JOIN dbo.Orders o ON o.ClientOrderId=x.ClientOrderId
    JOIN dbo.Drinks d ON d.DrinkCode=x.DrinkCode
    JOIN dbo.Sizes s ON s.SizeCode=x.SizeCode
    JOIN dbo.DrinkSizes ds ON ds.DrinkId=d.DrinkId AND ds.SizeId=s.SizeId
    LEFT JOIN dbo.StoreMenuItems sm ON sm.StoreId=x.StoreId AND sm.DrinkSizeId=ds.DrinkSizeId
    WHERE NOT EXISTS(SELECT 1 FROM dbo.OrderDetails od WHERE od.OrderId=o.OrderId);

    UPDATE od
       SET od.Price=x.Total/NULLIF(x.Quantity,0),od.AcceptedBasePrice=x.Total/NULLIF(x.Quantity,0),
           od.Quantity=x.Quantity,od.CostStatus=x.CostStatus,
           od.UnitCogs=CASE WHEN x.TotalCogs IS NULL THEN NULL ELSE x.TotalCogs/NULLIF(x.Quantity,0) END,
           od.TotalCogs=x.TotalCogs
    FROM dbo.OrderDetails od
    JOIN dbo.Orders o ON o.OrderId=od.OrderId
    JOIN @AiOrders x ON x.ClientOrderId=o.ClientOrderId
    WHERE o.Source=N'DEMO_AI_DASHBOARD_ROLLING_V1';

    INSERT dbo.Payments
    (
        OrderId,Amount,ReceivedAmount,ChangeAmount,PaymentMethodId,PaymentStatusId,
        CashSessionId,TransactionCode,PaidAt
    )
    SELECT o.OrderId,x.Total,x.Total,0,x.PaymentMethodId,2,NULL,
           CONCAT(N'DEMO_AI_DASHBOARD_ROLLING_V1_',CONVERT(nvarchar(36),x.ClientOrderId)),x.CreatedAt
    FROM @AiOrders x
    JOIN dbo.Orders o ON o.ClientOrderId=x.ClientOrderId
    WHERE x.OrderStatusId=5
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.Payments p
          WHERE p.TransactionCode=CONCAT(N'DEMO_AI_DASHBOARD_ROLLING_V1_',CONVERT(nvarchar(36),x.ClientOrderId))
      );

    UPDATE p
       SET p.Amount=x.Total,p.ReceivedAmount=x.Total,p.PaidAt=x.CreatedAt,
           p.PaymentMethodId=x.PaymentMethodId
    FROM dbo.Payments p
    JOIN dbo.Orders o ON o.OrderId=p.OrderId
    JOIN @AiOrders x ON x.ClientOrderId=o.ClientOrderId
    WHERE p.TransactionCode=CONCAT(N'DEMO_AI_DASHBOARD_ROLLING_V1_',CONVERT(nvarchar(36),x.ClientOrderId));

    INSERT dbo.OrderRefunds
    (
        OrderId,StoreId,RefundKey,Status,PaymentMethodId,Reason,RefundAmount,CostStatus,ReversedCogs,
        InventoryReversalStatus,RequestedAtUtc,RequestedByStaffId,ProcessingAtUtc,CompletedAtUtc,CompletedByStaffId
    )
    SELECT o.OrderId,x.StoreId,x.RefundKey,3,2,N'AI rolling fixture refund',o.Total,1,o.TotalCogs,2,
           DATEADD(HOUR,2,x.CreatedAt),x.StaffId,DATEADD(HOUR,2,x.CreatedAt),
           DATEADD(HOUR,3,x.CreatedAt),x.StaffId
    FROM @AiOrders x
    JOIN dbo.Orders o ON o.ClientOrderId=x.ClientOrderId
    WHERE x.RefundKey IS NOT NULL
      AND NOT EXISTS(SELECT 1 FROM dbo.OrderRefunds r WHERE r.RefundKey=x.RefundKey);

    UPDATE r
       SET r.RefundAmount=o.Total,r.ReversedCogs=o.TotalCogs,
           r.RequestedAtUtc=DATEADD(HOUR,2,x.CreatedAt),
           r.ProcessingAtUtc=DATEADD(HOUR,2,x.CreatedAt),
           r.CompletedAtUtc=DATEADD(HOUR,3,x.CreatedAt)
    FROM dbo.OrderRefunds r
    JOIN @AiOrders x ON x.RefundKey=r.RefundKey
    JOIN dbo.Orders o ON o.ClientOrderId=x.ClientOrderId;

    /* Type 3 is the existing waste movement contract. */
    DECLARE @AiWaste TABLE(StoreInventoryId int PRIMARY KEY);
    INSERT dbo.InventoryTransactions
    (
        StoreInventoryId,[Type],StockStatus,Quantity,BeforeQty,AfterQty,UnitCost,TotalCost,CreatedAt
    )
    OUTPUT inserted.StoreInventoryId INTO @AiWaste(StoreInventoryId)
    SELECT si.StoreInventoryId,3,1,2,si.AvailableQty,si.AvailableQty-2,12,24,
           DATEADD(DAY,-2,@AiDay)
    FROM dbo.StoreInventories si
    WHERE si.StoreId IN (1,3) AND si.IngredientId=@AiCoffeeIngredientId
      AND si.AvailableQty>=2
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.InventoryTransactions t
          WHERE t.StoreInventoryId=si.StoreInventoryId AND t.[Type]=3
            AND t.UnitCost=12 AND t.TotalCost=24
      );
    UPDATE si
       SET si.AvailableQty=si.AvailableQty-2,
           si.LastUpdated=DATEADD(DAY,-2,@AiDay)
    FROM dbo.StoreInventories si
    JOIN @AiWaste w ON w.StoreInventoryId=si.StoreInventoryId;

    UPDATE t SET t.CreatedAt=DATEADD(DAY,-2,@AiDay)
    FROM dbo.InventoryTransactions t
    JOIN dbo.StoreInventories si ON si.StoreInventoryId=t.StoreInventoryId
    WHERE si.StoreId IN (1,3) AND si.IngredientId=@AiCoffeeIngredientId
      AND t.[Type]=3 AND t.UnitCost=12 AND t.TotalCost=24;

    /* NORMAL keeps stock above threshold; ANOMALY uses the same FIFO quantity but a higher alert threshold. */
    UPDATE si
       SET si.MinStockLevel=CASE WHEN si.StoreId=1
                                THEN CASE WHEN si.AvailableQty>10 THEN si.AvailableQty-10 ELSE 0 END
                                ELSE si.AvailableQty-si.ReservedQty+10 END
    FROM dbo.StoreInventories si
    WHERE si.StoreId IN (1,3) AND si.IngredientId=@AiCoffeeIngredientId;

    DECLARE @AiRestock TABLE(StoreId int PRIMARY KEY,StaffId int,Note nvarchar(100),CreatedAt datetime2(0));
    INSERT @AiRestock VALUES
      (1,@AiStore1StaffId,N'DEMO_AI_DASHBOARD_ROLLING_V1_RESTOCK_S1',DATEADD(DAY,-1,@AiDay)),
      (3,@AiStore3StaffId,N'DEMO_AI_DASHBOARD_ROLLING_V1_RESTOCK_S3',DATEADD(DAY,-1,@AiDay));

    INSERT dbo.RestockRequests
    (
        StockAlertId,StoreId,IngredientId,RecipeId,PreparedItemId,RequestedQuantity,SuggestedQuantity,
        SuggestionAnalysisWindowDays,SuggestionAvailableSnapshot,SuggestionMinLevelSnapshot,
        SuggestionAverageDailyUsageSnapshot,SuggestionLeadTimeDaysSnapshot,SuggestionIncomingQuantitySnapshot,
       ReferenceCode,SuggestionReason,Status,Priority,CreatedByStaffId,CreatedAt,UpdatedAt,Note,
        HandledByStaffId,HandledAt,AcceptedByStaffId,AcceptedAtUtc,ProcessingNote,ClosedRemainingQuantity
    )
    SELECT NULL,x.StoreId,@AiCoffeeIngredientId,NULL,NULL,10,12,30,2,5,1,2,0,
           CONCAT(N'RR-DEMO-AI-',x.StoreId),
           N'AI rolling low-stock fixture',N'OPEN',N'HIGH',x.StaffId,x.CreatedAt,x.CreatedAt,x.Note,
           NULL,NULL,NULL,NULL,NULL,0
    FROM @AiRestock x
    WHERE NOT EXISTS(SELECT 1 FROM dbo.RestockRequests r WHERE r.Note=x.Note);

    UPDATE rr SET rr.CreatedAt=x.CreatedAt,rr.UpdatedAt=x.CreatedAt
    FROM dbo.RestockRequests rr JOIN @AiRestock x ON x.Note=rr.Note;

    INSERT dbo.PurchaseOrders
    (
        Code,StoreId,SupplierId,Status,OrderDate,ExpectedDeliveryAtUtc,CreatedByStaffId,
        ApprovedByStaffId,SentByStaffId,CreatedAtUtc,UpdatedAtUtc,ApprovedAtUtc,SentAtUtc,Note
    )
    SELECT CONCAT(N'DEMO-AI-ROLLING-PO-S',x.StoreId),x.StoreId,@AiCoffeeSupplierId,N'MARKED_AS_SENT',
           DATEADD(DAY,-3,x.CreatedAt),DATEADD(DAY,-2,x.CreatedAt),x.StaffId,x.StaffId,x.StaffId,
           x.CreatedAt,x.CreatedAt,x.CreatedAt,DATEADD(MINUTE,10,x.CreatedAt),
           N'DEMO_AI_DASHBOARD_ROLLING_V1'
    FROM @AiRestock x
    WHERE @AiCoffeeSupplierId IS NOT NULL
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.PurchaseOrders p
          WHERE p.Code=CONCAT(N'DEMO-AI-ROLLING-PO-S',x.StoreId)
      );

    UPDATE po
       SET po.OrderDate=DATEADD(DAY,-3,x.CreatedAt),
           po.ExpectedDeliveryAtUtc=DATEADD(DAY,-2,x.CreatedAt),
           po.CreatedAtUtc=DATEADD(DAY,-3,x.CreatedAt),
           po.UpdatedAtUtc=DATEADD(DAY,-3,x.CreatedAt),
           po.ApprovedAtUtc=DATEADD(DAY,-3,x.CreatedAt),
           po.SentAtUtc=DATEADD(MINUTE,10,DATEADD(DAY,-3,x.CreatedAt))
    FROM dbo.PurchaseOrders po
    JOIN @AiRestock x ON po.Code=CONCAT(N'DEMO-AI-ROLLING-PO-S',x.StoreId)
    WHERE po.Note=N'DEMO_AI_DASHBOARD_ROLLING_V1';

    INSERT dbo.PurchaseOrderLines
    (
        PurchaseOrderId,RestockRequestId,IngredientId,IngredientSupplierId,PackageUnitIdSnapshot,
        PackageQuantitySnapshot,PackagePriceSnapshot,PackageCount,PurchaseMode,OrderedPackageCount,
        UnitPricePerPackage,OrderedBaseQuantity,
        InventoryBaseUnitId,ProcurementToInventoryFactor,
        ClosedRemainingQuantity,PromisedLeadTimeDaysSnapshot,Note
    )
    SELECT po.PurchaseOrderId,rr.RestockRequestId,@AiCoffeeIngredientId,@AiCoffeeOfferId,
           offer.UnitId,offer.PackageQuantity,offer.CurrentPrice,5,N'Packaged',5,
           offer.CurrentPrice,
           5*offer.PackageQuantity*CASE
               WHEN offer.UnitId=ingredient.BaseUnitId THEN 1
               WHEN LOWER(packageUnit.UnitCode)=N'kg' AND LOWER(baseUnit.UnitCode)=N'g' THEN 1000
               WHEN LOWER(packageUnit.UnitCode)=N'g' AND LOWER(baseUnit.UnitCode)=N'kg' THEN 0.001
               WHEN LOWER(packageUnit.UnitCode)=N'l' AND LOWER(baseUnit.UnitCode)=N'ml' THEN 1000
               WHEN LOWER(packageUnit.UnitCode)=N'ml' AND LOWER(baseUnit.UnitCode)=N'l' THEN 0.001
           END,
           ingredient.BaseUnitId,
           CASE
               WHEN offer.UnitId=ingredient.BaseUnitId THEN 1
               WHEN LOWER(packageUnit.UnitCode)=N'kg' AND LOWER(baseUnit.UnitCode)=N'g' THEN 1000
               WHEN LOWER(packageUnit.UnitCode)=N'g' AND LOWER(baseUnit.UnitCode)=N'kg' THEN 0.001
               WHEN LOWER(packageUnit.UnitCode)=N'l' AND LOWER(baseUnit.UnitCode)=N'ml' THEN 1000
               WHEN LOWER(packageUnit.UnitCode)=N'ml' AND LOWER(baseUnit.UnitCode)=N'l' THEN 0.001
           END,
           0,offer.LeadTimeDays,
           CONCAT(N'DEMO_AI_DASHBOARD_ROLLING_V1_LINE_S',rr.StoreId)
    FROM dbo.PurchaseOrders po
    JOIN dbo.RestockRequests rr ON rr.Note=CONCAT(N'DEMO_AI_DASHBOARD_ROLLING_V1_RESTOCK_S',po.StoreId)
    JOIN dbo.IngredientSuppliers offer ON offer.IngredientSupplierId=@AiCoffeeOfferId
    JOIN dbo.Ingredients ingredient ON ingredient.IngredientId=@AiCoffeeIngredientId
    JOIN dbo.Units packageUnit ON packageUnit.UnitId=offer.UnitId
    JOIN dbo.Units baseUnit ON baseUnit.UnitId=ingredient.BaseUnitId
    WHERE po.Note=N'DEMO_AI_DASHBOARD_ROLLING_V1'
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.PurchaseOrderLines l
          WHERE l.PurchaseOrderId=po.PurchaseOrderId
            AND l.Note=CONCAT(N'DEMO_AI_DASHBOARD_ROLLING_V1_LINE_S',rr.StoreId)
      );

    /* Repair only the deterministic rolling fixture before it has receipt/closure evidence. */
    UPDATE line
       SET line.OrderedBaseQuantity=5*line.PackageQuantitySnapshot*CASE
               WHEN line.PackageUnitIdSnapshot=ingredient.BaseUnitId THEN 1
               WHEN LOWER(packageUnit.UnitCode)=N'kg' AND LOWER(baseUnit.UnitCode)=N'g' THEN 1000
               WHEN LOWER(packageUnit.UnitCode)=N'g' AND LOWER(baseUnit.UnitCode)=N'kg' THEN 0.001
               WHEN LOWER(packageUnit.UnitCode)=N'l' AND LOWER(baseUnit.UnitCode)=N'ml' THEN 1000
               WHEN LOWER(packageUnit.UnitCode)=N'ml' AND LOWER(baseUnit.UnitCode)=N'l' THEN 0.001
           END,
           line.InventoryBaseUnitId=ingredient.BaseUnitId,
           line.ProcurementToInventoryFactor=CASE
               WHEN line.PackageUnitIdSnapshot=ingredient.BaseUnitId THEN 1
               WHEN LOWER(packageUnit.UnitCode)=N'kg' AND LOWER(baseUnit.UnitCode)=N'g' THEN 1000
               WHEN LOWER(packageUnit.UnitCode)=N'g' AND LOWER(baseUnit.UnitCode)=N'kg' THEN 0.001
               WHEN LOWER(packageUnit.UnitCode)=N'l' AND LOWER(baseUnit.UnitCode)=N'ml' THEN 1000
               WHEN LOWER(packageUnit.UnitCode)=N'ml' AND LOWER(baseUnit.UnitCode)=N'l' THEN 0.001
           END,
           line.ClosedRemainingQuantity=0,
           line.ClosedProcurementQuantity=0,
           line.CloseRemainingReason=NULL,
           line.ClosedRemainingByStaffId=NULL,
           line.ClosedRemainingAtUtc=NULL
    FROM dbo.PurchaseOrderLines line
    JOIN dbo.Ingredients ingredient ON ingredient.IngredientId=line.IngredientId
    JOIN dbo.Units packageUnit ON packageUnit.UnitId=line.PackageUnitIdSnapshot
    JOIN dbo.Units baseUnit ON baseUnit.UnitId=ingredient.BaseUnitId
    WHERE line.Note LIKE N'DEMO_AI_DASHBOARD_ROLLING_V1_LINE_S%'
      AND line.PurchaseMode=N'Packaged'
      AND line.OrderedPackageCount=5
      AND line.PackageQuantitySnapshot IS NOT NULL
      AND line.CloseRemainingReason IS NULL
      AND line.ClosedRemainingByStaffId IS NULL
      AND line.ClosedRemainingAtUtc IS NULL
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.PurchaseOrderReceiptPostings posting
          WHERE posting.PurchaseOrderLineId=line.PurchaseOrderLineId
      );

    /* ANOMALY: cash discrepancy in the rolling window. */
    IF NOT EXISTS(SELECT 1 FROM dbo.WorkShifts WHERE DiscrepancyReason=N'DEMO_AI_DASHBOARD_ROLLING_V1_CASH_ANOMALY')
      INSERT dbo.WorkShifts
      (
        StoreId,UserId,StartTimeUtc,EndTimeUtc,BusinessDate,OpenContext,CloseType,ExpiryWarningLevel,StartingCash,ExpectedEndingCash,ActualEndingCash,
        CashDiscrepancy,[Status],DiscrepancyReason,IsExceptionClosed,ExceptionCloseReason,
        ExceptionClosedByStaffId,ExceptionClosedAt,OfflineOrderCountAtClose,OfflineEstimatedTotalAtClose,
        OfflineCashTotalAtClose,RequiresReconciliation,HasLateOfflineSync,LateOfflineSyncCount,
        LastLateOfflineSyncedAtUtc,PosTerminalId
      )
      VALUES
      (
        3,@AiStore3StaffId,DATEADD(HOUR,6,DATEADD(DAY,-1,@AiDay)),
        DATEADD(HOUR,12,DATEADD(DAY,-1,@AiDay)),CONVERT(date,DATEADD(HOUR,13,DATEADD(DAY,-1,@AiDay))),N'LEGACY',N'NORMAL',0,500000,500000,420000,-80000,
        N'CLOSED',N'DEMO_AI_DASHBOARD_ROLLING_V1_CASH_ANOMALY',0,NULL,NULL,NULL,
        0,0,0,1,0,0,NULL,NULL
      );

    UPDATE ws
       SET ws.StartTimeUtc=DATEADD(HOUR,6,DATEADD(DAY,-1,@AiDay)),
           ws.EndTimeUtc=DATEADD(HOUR,12,DATEADD(DAY,-1,@AiDay)),
           ws.BusinessDate=CONVERT(date,DATEADD(HOUR,13,DATEADD(DAY,-1,@AiDay))),
           ws.OpenContext=N'LEGACY',ws.CloseType=N'NORMAL',ws.Status=N'CLOSED',
           ws.ExpectedEndingCash=500000,ws.ActualEndingCash=420000,
           ws.CashDiscrepancy=-80000,ws.RequiresReconciliation=1
    FROM dbo.WorkShifts ws
    WHERE ws.DiscrepancyReason=N'DEMO_AI_DASHBOARD_ROLLING_V1_CASH_ANOMALY';

    /* ANOMALY: supplier rejection and issue, tied to rolling PO business keys. */
    DECLARE @AiPo3Id int=(SELECT PurchaseOrderId FROM dbo.PurchaseOrders WHERE Code=N'DEMO-AI-ROLLING-PO-S3');
    DECLARE @AiPo3LineId int=(SELECT TOP(1) PurchaseOrderLineId FROM dbo.PurchaseOrderLines WHERE PurchaseOrderId=@AiPo3Id ORDER BY PurchaseOrderLineId);
    DECLARE @AiRestock3Id int=(SELECT RestockRequestId FROM dbo.RestockRequests WHERE Note=N'DEMO_AI_DASHBOARD_ROLLING_V1_RESTOCK_S3');
    IF @AiPo3Id IS NOT NULL AND @AiPo3LineId IS NOT NULL
       AND NOT EXISTS(SELECT 1 FROM dbo.BranchReceipts WHERE ReceiptCode=N'DEMO-AI-ROLLING-BR-S3')
      INSERT dbo.BranchReceipts
      (
        ReceiptCode,StoreId,SupplierId,PurchaseOrderId,[Status],ReceiptKey,ReferenceNumber,
        ReceivedAt,ReceivedByStaffId,ConfirmedAt,ConfirmedByStaffId,Notes,CreatedAt,CreatedByStaffId
      )
      VALUES
      (
        N'DEMO-AI-ROLLING-BR-S3',3,@AiCoffeeSupplierId,@AiPo3Id,N'CONFIRMED',
        N'DEMO_AI_DASHBOARD_ROLLING_V1_RECEIPT_S3',N'DEMO-AI-ROLLING-INVOICE-S3',
        DATEADD(HOUR,9,DATEADD(DAY,-2,@AiDay)),@AiStore3StaffId,
        DATEADD(HOUR,10,DATEADD(DAY,-2,@AiDay)),@AiStore3StaffId,
        N'AI_DASHBOARD_SCENARIO_ANOMALY supplier receipt',
        DATEADD(HOUR,9,DATEADD(DAY,-2,@AiDay)),@AiStore3StaffId
      );

    DECLARE @AiReceipt3Id int=(SELECT BranchReceiptId FROM dbo.BranchReceipts WHERE ReceiptCode=N'DEMO-AI-ROLLING-BR-S3');
    IF @AiReceipt3Id IS NOT NULL
       AND NOT EXISTS(SELECT 1 FROM dbo.BranchReceiptLines WHERE BranchReceiptId=@AiReceipt3Id AND PurchaseOrderLineId=@AiPo3LineId)
      INSERT dbo.BranchReceiptLines
      (
        BranchReceiptId,RestockRequestId,PurchaseOrderLineId,IngredientId,PreparedItemId,RecipeId,
        InputQuantity,InputUnitId,ReceivedBaseQuantity,RejectedBaseQuantity,RejectionReason,RejectionIssueType,
        BaseUnitId,SupplierId,IngredientSupplierId,ActualPackagePrice,PackageQuantitySnapshot,
        PackageUnitIdSnapshot,BaseUnitCostSnapshot,LineTotalCost,CreatedAt
      )
      SELECT @AiReceipt3Id,@AiRestock3Id,@AiPo3LineId,@AiCoffeeIngredientId,NULL,NULL,
             10,o.UnitId,8,2,N'Bao bì rách',N'PACKAGING_FAILURE',
             i.BaseUnitId,@AiCoffeeSupplierId,@AiCoffeeOfferId,o.CurrentPrice,o.PackageQuantity,
             o.UnitId,o.CurrentPrice/NULLIF(o.PackageQuantity,0),
             8*(o.CurrentPrice/NULLIF(o.PackageQuantity,0)),DATEADD(HOUR,9,DATEADD(DAY,-2,@AiDay))
      FROM dbo.IngredientSuppliers o
      JOIN dbo.Ingredients i ON i.IngredientId=o.IngredientId
      WHERE o.IngredientSupplierId=@AiCoffeeOfferId;

    DECLARE @AiReceipt3LineId int=(SELECT TOP(1) BranchReceiptLineId FROM dbo.BranchReceiptLines WHERE BranchReceiptId=@AiReceipt3Id AND PurchaseOrderLineId=@AiPo3LineId);
    IF @AiReceipt3LineId IS NOT NULL
       AND NOT EXISTS(SELECT 1 FROM dbo.SupplierReceiptIssues WHERE Description=N'DEMO_AI_DASHBOARD_ROLLING_V1_SUPPLIER_ISSUE')
      INSERT dbo.SupplierReceiptIssues
      (
        SupplierId,StoreId,PurchaseOrderId,PurchaseOrderLineId,BranchReceiptId,BranchReceiptLineId,
        IssueType,[Status],AffectedBaseQuantity,Description,ReportedByStaffId,ReportedAtUtc,UpdatedAtUtc
      )
      VALUES
      (
        @AiCoffeeSupplierId,3,@AiPo3Id,@AiPo3LineId,@AiReceipt3Id,@AiReceipt3LineId,
        N'PACKAGING_FAILURE',N'OPEN',2,N'DEMO_AI_DASHBOARD_ROLLING_V1_SUPPLIER_ISSUE',
        @AiStore3StaffId,DATEADD(HOUR,10,DATEADD(DAY,-2,@AiDay)),
        DATEADD(HOUR,10,DATEADD(DAY,-2,@AiDay))
      );

    UPDATE br
       SET br.ReceivedAt=DATEADD(HOUR,9,DATEADD(DAY,-2,@AiDay)),
           br.ConfirmedAt=DATEADD(HOUR,10,DATEADD(DAY,-2,@AiDay)),
           br.CreatedAt=DATEADD(HOUR,9,DATEADD(DAY,-2,@AiDay))
    FROM dbo.BranchReceipts br WHERE br.ReceiptCode=N'DEMO-AI-ROLLING-BR-S3';
    UPDATE brl SET brl.CreatedAt=DATEADD(HOUR,9,DATEADD(DAY,-2,@AiDay))
    FROM dbo.BranchReceiptLines brl WHERE brl.BranchReceiptId=@AiReceipt3Id;
    UPDATE issue
       SET issue.ReportedAtUtc=DATEADD(HOUR,10,DATEADD(DAY,-2,@AiDay)),
           issue.UpdatedAtUtc=DATEADD(HOUR,10,DATEADD(DAY,-2,@AiDay))
    FROM dbo.SupplierReceiptIssues issue
    WHERE issue.Description=N'DEMO_AI_DASHBOARD_ROLLING_V1_SUPPLIER_ISSUE';

    IF (SELECT COUNT(*) FROM dbo.Orders WHERE Source=N'DEMO_AI_DASHBOARD_ROLLING_V1')<>30
        THROW 53501,N'AI rolling fixture phải có 30 orders.',1;
    IF (SELECT COUNT(*) FROM dbo.Orders WHERE Source=N'DEMO_AI_DASHBOARD_ROLLING_V1' AND StoreId=1)<>15
        OR (SELECT COUNT(*) FROM dbo.Orders WHERE Source=N'DEMO_AI_DASHBOARD_ROLLING_V1' AND StoreId=3)<>15
        THROW 53504,N'AI rolling fixture phải phân bổ 15 orders cho mỗi store.',1;
    IF (SELECT COUNT(*) FROM dbo.Orders WHERE Source=N'DEMO_AI_DASHBOARD_ROLLING_V1' AND OrderStatusId=5)<20
        OR (SELECT COUNT(*) FROM dbo.Orders WHERE Source=N'DEMO_AI_DASHBOARD_ROLLING_V1' AND OrderStatusId=6)<5
        OR (SELECT COUNT(*) FROM dbo.OrderRefunds r JOIN dbo.Orders o ON o.OrderId=r.OrderId WHERE o.Source=N'DEMO_AI_DASHBOARD_ROLLING_V1')<4
        THROW 53505,N'AI rolling fixture thiếu phân bố completed/cancelled/refunded.',1;
    IF (SELECT COUNT(*) FROM dbo.RestockRequests WHERE Note LIKE N'DEMO_AI_DASHBOARD_ROLLING_V1_RESTOCK_S%')<>2
        THROW 53502,N'AI rolling fixture phải có 2 restock requests.',1;
    IF NOT EXISTS(SELECT 1 FROM dbo.WorkShifts WHERE DiscrepancyReason=N'DEMO_AI_DASHBOARD_ROLLING_V1_CASH_ANOMALY' AND ABS(CashDiscrepancy)>=50000)
        OR NOT EXISTS(SELECT 1 FROM dbo.SupplierReceiptIssues WHERE Description=N'DEMO_AI_DASHBOARD_ROLLING_V1_SUPPLIER_ISSUE')
        THROW 53506,N'AI rolling fixture missing cash discrepancy or supplier issue.',1;
    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.StoreInventories
        WHERE StoreId=3 AND IngredientId=@AiCoffeeIngredientId
          AND AvailableQty-ReservedQty<=MinStockLevel
    ) THROW 53507,N'AI rolling fixture missing low-stock anomaly.',1;
    IF (SELECT COUNT(*) FROM dbo.PurchaseOrders WHERE Note=N'DEMO_AI_DASHBOARD_ROLLING_V1')<>2
        THROW 53503,N'AI rolling fixture phải có 2 purchase orders.',1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT N'DEMO_AI_DASHBOARD_ROLLING_V1' AS SeedMarker,
       SYSUTCDATETIME() AS VerifiedAtUtc,
       (SELECT COUNT(*) FROM dbo.Orders WHERE Source=N'DEMO_AI_DASHBOARD_ROLLING_V1') AS DemoOrders,
       (SELECT COUNT(*) FROM dbo.Orders WHERE Source=N'DEMO_AI_DASHBOARD_ROLLING_V1' AND Note=N'AI_DASHBOARD_SCENARIO_NORMAL') AS NormalOrders,
       (SELECT COUNT(*) FROM dbo.Orders WHERE Source=N'DEMO_AI_DASHBOARD_ROLLING_V1' AND Note=N'AI_DASHBOARD_SCENARIO_ANOMALY') AS AnomalyOrders,
       (SELECT COUNT(*) FROM dbo.RestockRequests WHERE Note LIKE N'DEMO_AI_DASHBOARD_ROLLING_V1_RESTOCK_S%') AS DemoRestocks,
       (SELECT COUNT(*) FROM dbo.PurchaseOrders WHERE Note=N'DEMO_AI_DASHBOARD_ROLLING_V1') AS DemoPurchaseOrders;
GO

/* ================================================================
   BATCH 15B - AI REORDER EXPLANATION / DASHBOARD TEST FIXTURE
   Marker: DEMO_AI_REORDER_TEST_V1

   The deterministic reorder service must calculate the suggestion.
   This batch only makes one seed-owned inventory row actionable; it
   never inserts a suggested quantity, restock request or procurement
   document and never changes physical stock.
   ================================================================ */
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @ReorderTestNow datetime2(0)=SYSUTCDATETIME();
    DECLARE @ReorderTestFrom datetime2(0)=DATEADD(DAY,-30,@ReorderTestNow);
    DECLARE @ReorderTestStoreId int=(
        SELECT StoreId FROM dbo.Stores
        WHERE Name=N'CafeChain Thủ Dầu Một' AND Active=1);
    DECLARE @ReorderTestIngredientId int=(
        SELECT IngredientId FROM dbo.Ingredients
        WHERE Code=N'DEMO_ING_CHIA_SEED' AND Active=1);
    DECLARE @ReorderTestInventoryId int=(
        SELECT StoreInventoryId FROM dbo.StoreInventories
        WHERE StoreId=@ReorderTestStoreId
          AND IngredientId=@ReorderTestIngredientId);

    IF @ReorderTestStoreId IS NULL
       OR @ReorderTestIngredientId IS NULL
       OR @ReorderTestInventoryId IS NULL
        THROW 53520,N'DEMO_AI_REORDER_TEST_V1: missing Store 1, chia ingredient or inventory.',1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.InventoryTransactions t
        WHERE t.StoreInventoryId=@ReorderTestInventoryId
          AND t.[Type] IN(6,7)
          AND t.CreatedAt>=@ReorderTestFrom
          AND t.CreatedAt<@ReorderTestNow
          AND t.Quantity>0
    ) THROW 53521,N'DEMO_AI_REORDER_TEST_V1: missing rolling 30-day consumption.',1;

    IF
    (
        SELECT COUNT(*)
        FROM dbo.IngredientSuppliers offer
        JOIN dbo.Suppliers supplier
          ON supplier.SupplierId=offer.SupplierId AND supplier.Active=1
        JOIN dbo.SupplierStores scope
          ON scope.SupplierId=supplier.SupplierId
         AND scope.StoreId=@ReorderTestStoreId AND scope.Active=1
        JOIN dbo.IngredientSupplierPriceHistories price
          ON price.IngredientSupplierId=offer.IngredientSupplierId
         AND price.IsCurrent=1 AND price.EffectiveDate<=@ReorderTestNow
        WHERE offer.IngredientId=@ReorderTestIngredientId
          AND offer.Active=1 AND offer.IsPrimary=1
          AND offer.PackageQuantity>0 AND offer.CurrentPrice>0
          AND offer.MinimumOrderPackageCount>=0
          AND offer.LeadTimeDays IS NOT NULL AND offer.LeadTimeDays>=0
          AND price.Price>0 AND price.PackageQuantity>0
          AND price.PackageUnitId IS NOT NULL
    )<>1 THROW 53522,N'DEMO_AI_REORDER_TEST_V1: primary supplier/package/price contract is invalid.',1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.PurchaseOrderLines line
        JOIN dbo.PurchaseOrders po ON po.PurchaseOrderId=line.PurchaseOrderId
        WHERE po.StoreId=@ReorderTestStoreId
          AND line.IngredientId=@ReorderTestIngredientId
          AND po.Status IN(N'DRAFT',N'APPROVED',N'MARKED_AS_SENT',N'PARTIALLY_RECEIVED')
    ) OR EXISTS
    (
        SELECT 1
        FROM dbo.PurchaseAdviceLines line
        JOIN dbo.PurchaseAdvices advice ON advice.PurchaseAdviceId=line.PurchaseAdviceId
        WHERE advice.StoreId=@ReorderTestStoreId
          AND line.IngredientId=@ReorderTestIngredientId
          AND line.IsActiveReservation=1
    ) THROW 53523,N'DEMO_AI_REORDER_TEST_V1: chia fixture must not have active procurement coverage.',1;

    /* Preserve stock/reservations. Only the seed-owned threshold is made urgent. */
    UPDATE dbo.StoreInventories
       SET MinStockLevel=AvailableQty-ReservedQty+10000
     WHERE StoreInventoryId=@ReorderTestInventoryId;

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.StoreInventories
        WHERE StoreInventoryId=@ReorderTestInventoryId
          AND AvailableQty-ReservedQty<MinStockLevel
          AND MinStockLevel-(AvailableQty-ReservedQty)=10000
    ) THROW 53524,N'DEMO_AI_REORDER_TEST_V1: urgent threshold was not established.',1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT N'DEMO_AI_REORDER_TEST_V1' AS SeedMarker,
       SYSUTCDATETIME() AS VerifiedAtUtc,
       s.StoreId,
       s.Name AS StoreName,
       i.IngredientId,
       i.Code AS IngredientCode,
       i.Name AS IngredientName,
       si.AvailableQty-si.ReservedQty AS AvailableStock,
       si.MinStockLevel,
       (SELECT COUNT(*) FROM dbo.InventoryTransactions t
        WHERE t.StoreInventoryId=si.StoreInventoryId AND t.[Type] IN(6,7)
          AND t.CreatedAt>=DATEADD(DAY,-30,SYSUTCDATETIME())
          AND t.CreatedAt<SYSUTCDATETIME()) AS ConsumptionMovementCount,
       (SELECT COUNT(*) FROM dbo.IngredientSuppliers offer
        JOIN dbo.SupplierStores scope ON scope.SupplierId=offer.SupplierId
        WHERE offer.IngredientId=i.IngredientId AND offer.Active=1 AND offer.IsPrimary=1
          AND scope.StoreId=s.StoreId AND scope.Active=1) AS PrimarySupplierCount,
       N'READY_FOR_DETERMINISTIC_CALCULATION' AS DataStatus
FROM dbo.Stores s
JOIN dbo.StoreInventories si ON si.StoreId=s.StoreId
JOIN dbo.Ingredients i ON i.IngredientId=si.IngredientId
WHERE s.Name=N'CafeChain Thủ Dầu Một'
  AND i.Code=N'DEMO_ING_CHIA_SEED';
GO

EXEC dbo.SeedDemoCoverageV16;
DROP PROCEDURE dbo.SeedDemoCoverageV16;
GO

SELECT N'DEMO_COVERAGE_V16_RECEIPTS' SeedMarker,
       (SELECT COUNT(*) FROM dbo.BranchReceipts WHERE Status=N'CONFIRMED') ConfirmedReceipts,
       (SELECT COUNT(DISTINCT StoreId) FROM dbo.BranchReceipts) StoresWithReceipts,
       (SELECT COUNT(*) FROM dbo.InventoryTransactions WHERE BranchReceiptLineId IS NOT NULL) ReceiptTransactions,
       (SELECT COUNT(*) FROM dbo.InventoryCostLayers WHERE SourceBranchReceiptLineId IS NOT NULL) ReceiptCostLayers,
       (SELECT COUNT(*) FROM dbo.PurchaseOrderReceiptPostings) PurchaseOrderPostings,
       (SELECT COUNT(*) FROM dbo.RestockFulfillmentPostings) RestockPostings;
GO

/* ============================================================
   BATCH 17 - DEMO_COVERAGE_V17 cross-module business scenarios
   ============================================================ */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @Coverage17Now datetime2(7)='2026-07-20T08:00:00';
    DECLARE @Coverage17Order int=(SELECT TOP(1) OrderId FROM dbo.Orders WHERE Source=N'DEMO_DASHBOARD_V13' ORDER BY OrderId);
    DECLARE @Coverage17Staff int=(SELECT TOP(1) StaffId FROM dbo.Staffs WHERE StoreId=1 AND Active=1 ORDER BY StaffId);
    DECLARE @Coverage17Staff2 int=(SELECT TOP(1) StaffId FROM dbo.Staffs WHERE StoreId=2 AND Active=1 ORDER BY StaffId);
    DECLARE @Coverage17Staff3 int=(SELECT TOP(1) StaffId FROM dbo.Staffs WHERE StoreId=3 AND Active=1 ORDER BY StaffId);
    DECLARE @Coverage17Shift int=(SELECT TOP(1) ShiftId FROM dbo.Shifts WHERE StoreId=1 ORDER BY ShiftId);
    DECLARE @Coverage17Restock int=(SELECT TOP(1) brl.RestockRequestId
                                    FROM dbo.BranchReceiptLines brl
                                    JOIN dbo.BranchReceipts br ON br.BranchReceiptId=brl.BranchReceiptId
                                    WHERE br.ReceiptCode=N'DEMO-DASH-V13-BR-001');
    DECLARE @Coverage17Pol int=(SELECT TOP(1) pol.PurchaseOrderLineId
                               FROM dbo.PurchaseOrderLines pol
                               WHERE pol.Note=N'DEMO_COVERAGE_V16_POL_S1-FULL-001');
    DECLARE @Coverage17Po int=(SELECT PurchaseOrderId FROM dbo.PurchaseOrderLines WHERE PurchaseOrderLineId=@Coverage17Pol);
    DECLARE @Coverage17Offer int=(SELECT IngredientSupplierId FROM dbo.PurchaseOrderLines WHERE PurchaseOrderLineId=@Coverage17Pol);
    DECLARE @Coverage17Supplier int=(SELECT SupplierId FROM dbo.PurchaseOrders WHERE PurchaseOrderId=@Coverage17Po);
    DECLARE @Coverage17BaseUnit int=(SELECT BaseUnitId FROM dbo.Ingredients WHERE IngredientId=14);
    DECLARE @Coverage17ProcUnit int=(SELECT ProcurementUnitId FROM dbo.PurchaseOrderLines WHERE PurchaseOrderLineId=@Coverage17Pol);

    IF @Coverage17Order IS NULL OR @Coverage17Staff IS NULL OR @Coverage17Staff2 IS NULL
       OR @Coverage17Staff3 IS NULL OR @Coverage17Shift IS NULL OR @Coverage17Restock IS NULL
       OR @Coverage17Pol IS NULL OR @Coverage17Offer IS NULL
        THROW 53700,N'DEMO_COVERAGE_V17: prerequisite demo business keys are missing.',1;

    /* POS terminals and payment webhook ledger. */
    INSERT dbo.PosTerminals(TerminalId,StoreId,Name,Active,CreatedAtUtc)
    SELECT v.TerminalId,v.StoreId,v.Name,1,@Coverage17Now
    FROM (VALUES
          (N'DEMO_COVERAGE_V17_POS_S1',1,N'POS Demo Chi nhánh 1'),
          (N'DEMO_COVERAGE_V17_POS_S2',2,N'POS Demo Chi nhánh 2'),
          (N'DEMO_COVERAGE_V17_POS_S3',3,N'POS Demo Chi nhánh 3')) v(TerminalId,StoreId,Name)
    WHERE NOT EXISTS(SELECT 1 FROM dbo.PosTerminals p WHERE p.TerminalId=v.TerminalId);
    IF NOT EXISTS(SELECT 1 FROM dbo.TransactionLogs WHERE TransactionId=N'DEMO_COVERAGE_V17_PAYOS')
        INSERT dbo.TransactionLogs(OrderId,TransactionId,Amount,Description,Status,RawPayload,CreatedAt)
        SELECT @Coverage17Order,N'DEMO_COVERAGE_V17_PAYOS',Total,N'Đối soát thanh toán demo',
               N'PAID',N'{"marker":"DEMO_COVERAGE_V17","provider":"PAYOS"}',@Coverage17Now
        FROM dbo.Orders WHERE OrderId=@Coverage17Order;

    /* Forecast, recommendations, anomaly, and workforce optimization. */
    DECLARE @Coverage17Forecast bigint;
    IF NOT EXISTS(SELECT 1 FROM dbo.ForecastRuns
                  WHERE StoreId=1 AND SeriesType=N'REVENUE' AND EntityId IS NULL
                    AND TrainingToExclusive='2026-07-20' AND HorizonDays=7 AND ModelVersion=N'demo-v17')
        INSERT dbo.ForecastRuns
        (SeriesType,StoreId,EntityId,TrainingFrom,TrainingToExclusive,HorizonDays,ModelType,ModelVersion,
         SampleCount,Mae,Wape,QualityStatus,WarningJson,CreatedAtUtc,ExpiresAtUtc,InputDataVersion)
        VALUES(N'REVENUE',1,NULL,'2026-06-20','2026-07-20',7,N'ROBUST_BASELINE',N'demo-v17',
               30,12000,0.0830,N'GOOD',N'[]',@Coverage17Now,'2026-08-20',N'DEMO_COVERAGE_V17');
    SELECT @Coverage17Forecast=ForecastRunId FROM dbo.ForecastRuns
    WHERE StoreId=1 AND SeriesType=N'REVENUE' AND EntityId IS NULL
      AND TrainingToExclusive='2026-07-20' AND HorizonDays=7 AND ModelVersion=N'demo-v17';
    INSERT dbo.ForecastPoints(ForecastRunId,ForecastDate,PointForecast,LowerBound,UpperBound)
    SELECT @Coverage17Forecast,v.ForecastDate,v.PointForecast,v.LowerBound,v.UpperBound
    FROM (VALUES
          (CONVERT(datetime2,'2026-07-20'),CONVERT(decimal(19,4),3200000),CONVERT(decimal(19,4),2800000),CONVERT(decimal(19,4),3600000)),
          (CONVERT(datetime2,'2026-07-21'),CONVERT(decimal(19,4),3400000),CONVERT(decimal(19,4),3000000),CONVERT(decimal(19,4),3800000)))
         v(ForecastDate,PointForecast,LowerBound,UpperBound)
    WHERE NOT EXISTS(SELECT 1 FROM dbo.ForecastPoints p
                     WHERE p.ForecastRunId=@Coverage17Forecast AND p.ForecastDate=v.ForecastDate);

    IF NOT EXISTS(SELECT 1 FROM dbo.PosRecommendationCatalog
                  WHERE StoreId=1 AND TriggerDrinkId=1 AND RecommendedDrinkId=2 AND ModelVersion=N'demo-v17')
        INSERT dbo.PosRecommendationCatalog
        (StoreId,TriggerDrinkId,RecommendedDrinkId,Support,Confidence,Lift,Margin,Rank,ModelVersion,GeneratedAtUtc,ExpiresAtUtc)
        VALUES(1,1,2,0.120000,0.440000,1.310000,14000,1,N'demo-v17',@Coverage17Now,'2026-08-20');
    DECLARE @Coverage17Session uniqueidentifier='17171717-1717-1717-1717-171717171717';
    DECLARE @Coverage17Exposure bigint;
    IF NOT EXISTS(SELECT 1 FROM dbo.PosRecommendationExposures WHERE RecommendationSessionId=@Coverage17Session)
        INSERT dbo.PosRecommendationExposures
        (RecommendationSessionId,StoreId,OrderId,Variant,ModelVersion,CreatedAtUtc,ConvertedAtUtc)
        VALUES(@Coverage17Session,1,@Coverage17Order,N'TREATMENT',N'demo-v17',@Coverage17Now,DATEADD(MINUTE,2,@Coverage17Now));
    SELECT @Coverage17Exposure=PosRecommendationExposureId
    FROM dbo.PosRecommendationExposures WHERE RecommendationSessionId=@Coverage17Session;
    IF NOT EXISTS(SELECT 1 FROM dbo.PosRecommendationExposureItems
                  WHERE PosRecommendationExposureId=@Coverage17Exposure AND TriggerDrinkId=1 AND RecommendedDrinkId=2)
        INSERT dbo.PosRecommendationExposureItems
        (PosRecommendationExposureId,TriggerDrinkId,RecommendedDrinkId,Rank,WasDisplayed,WasClicked,WasAdded,WasPurchased)
        VALUES(@Coverage17Exposure,1,2,1,1,1,1,1);

    IF NOT EXISTS(SELECT 1 FROM dbo.OperationalAnomalies
                  WHERE StoreId=1 AND MetricCode=N'CASH_DISCREPANCY' AND PeriodKey=N'DEMO_COVERAGE_V17')
        INSERT dbo.OperationalAnomalies
        (StoreId,MetricCode,PeriodKey,BusinessDate,DetectionVersion,
         CurrentValue,BaselineValue,AbsoluteDeviation,PercentageDeviation,
         RobustScore,WindowFromUtc,WindowToExclusiveUtc,SampleCount,Severity,Confidence,Status,
         ReasonCodesJson,CreatedAtUtc,UpdatedAtUtc,AcknowledgedAtUtc,AcknowledgedByStaffId,
         ResolvedAtUtc,ResolvedByStaffId,ResolutionNote,Feedback,FeedbackNote,FeedbackByStaffId)
        VALUES(1,N'CASH_DISCREPANCY',N'DEMO_COVERAGE_V17','2026-07-19',N'v1',
               75000,5000,70000,14,4.2,
               '2026-07-19','2026-07-20',30,N'HIGH',N'HIGH',N'ACKNOWLEDGED',
               N'["ABOVE_BASELINE"]',@Coverage17Now,@Coverage17Now,@Coverage17Now,@Coverage17Staff,
               NULL,NULL,NULL,N'Useful',N'Demo manager acknowledged',@Coverage17Staff);

    IF NOT EXISTS(SELECT 1 FROM dbo.StaffAvailabilityRules
                  WHERE StaffId=@Coverage17Staff AND DayOfWeek=1 AND EffectiveFrom='2026-07-01')
        INSERT dbo.StaffAvailabilityRules
        (StaffId,DayOfWeek,StartTime,EndTime,EffectiveFrom,EffectiveTo,Active,CreatedByStaffId,CreatedAtUtc)
        VALUES(@Coverage17Staff,1,'06:00','14:00','2026-07-01',NULL,1,@Coverage17Staff,@Coverage17Now);
    IF NOT EXISTS(SELECT 1 FROM dbo.StaffAvailabilityExceptions
                  WHERE StaffId=@Coverage17Staff AND Date='2026-07-21')
        INSERT dbo.StaffAvailabilityExceptions
        (StaffId,Date,StartTime,EndTime,IsAvailable,Reason,CreatedByStaffId,CreatedAtUtc)
        VALUES(@Coverage17Staff,'2026-07-21','08:00','12:00',1,N'DEMO_COVERAGE_V17 hỗ trợ cao điểm',@Coverage17Staff,@Coverage17Now);
    IF NOT EXISTS(SELECT 1 FROM dbo.StaffTimeOffs
                  WHERE StaffId=@Coverage17Staff AND FromUtc='2026-08-01T00:00:00')
        INSERT dbo.StaffTimeOffs
        (StaffId,FromUtc,ToUtc,Status,Reason,RequestedByStaffId,ReviewedByStaffId,CreatedAtUtc,ReviewedAtUtc)
        VALUES(@Coverage17Staff,'2026-08-01','2026-08-02',N'APPROVED',N'DEMO_COVERAGE_V17 nghỉ phép',
               @Coverage17Staff,@Coverage17Staff,@Coverage17Now,@Coverage17Now);
    IF NOT EXISTS(SELECT 1 FROM dbo.StaffWorkConstraints
                  WHERE StaffId=@Coverage17Staff AND EffectiveFrom='2026-07-01')
        INSERT dbo.StaffWorkConstraints
        (StaffId,EffectiveFrom,EffectiveTo,TargetWeeklyHours,MaxWeeklyHours,MaxDailyHours,
         MinimumRestMinutes,CreatedByStaffId,CreatedAtUtc)
        VALUES(@Coverage17Staff,'2026-07-01',NULL,40,48,8,480,@Coverage17Staff,@Coverage17Now);

    INSERT dbo.StoreStaffingRequirements
    (StoreId,ShiftId,DayOfWeek,MinimumStaff,TargetStaff,MaximumStaff,RequiredRoleId,
     EffectiveFrom,EffectiveTo,Active,CreatedByStaffId,CreatedAtUtc)
    SELECT s.StoreId,sh.ShiftId,1,1,2,4,NULL,'2026-07-01',NULL,1,s.StaffId,@Coverage17Now
    FROM (VALUES(1,@Coverage17Staff),(2,@Coverage17Staff2),(3,@Coverage17Staff3)) s(StoreId,StaffId)
    CROSS APPLY(SELECT TOP(1) ShiftId FROM dbo.Shifts WHERE StoreId=s.StoreId ORDER BY ShiftId) sh
    WHERE NOT EXISTS(SELECT 1 FROM dbo.StoreStaffingRequirements r
                     WHERE r.StoreId=s.StoreId AND r.ShiftId=sh.ShiftId
                       AND r.DayOfWeek=1 AND r.EffectiveFrom='2026-07-01');

    DECLARE @Coverage17Proposal uniqueidentifier='17171717-0000-0000-0000-171717171717';
    IF NOT EXISTS(SELECT 1 FROM dbo.ScheduleOptimizationProposals WHERE ScheduleOptimizationProposalId=@Coverage17Proposal)
        INSERT dbo.ScheduleOptimizationProposals
        (ScheduleOptimizationProposalId,StoreId,FromDate,ToDate,ConstraintVersion,ForecastRunId,Status,
         ScoreBreakdownJson,ViolationsJson,CreatedByStaffId,CreatedAtUtc,ExpiresAtUtc,AppliedAtUtc)
        VALUES(@Coverage17Proposal,1,'2026-07-20','2026-07-26',N'demo-v17',@Coverage17Forecast,N'APPLIED',
               N'{"coverage":0.98,"fairness":0.94}',N'[]',@Coverage17Staff,@Coverage17Now,'2026-08-01',@Coverage17Now);
    IF NOT EXISTS(SELECT 1 FROM dbo.ScheduleOptimizationAssignments
                  WHERE ScheduleOptimizationProposalId=@Coverage17Proposal AND StaffId=@Coverage17Staff
                    AND ShiftId=@Coverage17Shift AND WorkDate='2026-07-21')
        INSERT dbo.ScheduleOptimizationAssignments
        (ScheduleOptimizationProposalId,StaffId,ShiftId,WorkDate,StartTime,EndTime,ReasonCodesJson)
        VALUES(@Coverage17Proposal,@Coverage17Staff,@Coverage17Shift,'2026-07-21','06:00','14:00',N'["FORECAST_COVERAGE"]');

    /* Stock alert -> sourcing -> purchase advice -> PO batch. */
    DECLARE @Coverage17Alert int;
    IF NOT EXISTS(SELECT 1 FROM dbo.StockAlerts WHERE Note=N'DEMO_COVERAGE_V17_STOCK_ALERT')
        INSERT dbo.StockAlerts
        (StoreId,IngredientId,RecipeId,PreparedItemId,AlertType,Severity,Status,CurrentQtySnapshot,
         ThresholdSnapshot,Source,Note,ReportedByStaffId,ReportedAt,ConfirmedByStaffId,ConfirmedAt,
         ManagerNote,CreatedAt,UpdatedAt,ResolvedAt,ResolvedReason)
        VALUES(2,14,NULL,NULL,N'LOW_STOCK',N'MEDIUM',N'RESOLVED',300,500,N'MANUAL',
               N'DEMO_COVERAGE_V17_STOCK_ALERT',@Coverage17Staff2,@Coverage17Now,@Coverage17Staff2,@Coverage17Now,
               N'Đã lập phương án mua',@Coverage17Now,@Coverage17Now,@Coverage17Now,N'Purchase advice created');
    SELECT @Coverage17Alert=StockAlertId FROM dbo.StockAlerts WHERE Note=N'DEMO_COVERAGE_V17_STOCK_ALERT';
    IF NOT EXISTS(SELECT 1 FROM dbo.StockAlertTransitions
                  WHERE StockAlertId=@Coverage17Alert AND Reason=N'DEMO_COVERAGE_V17 transition')
        INSERT dbo.StockAlertTransitions
        (StockAlertId,PreviousStatus,NewStatus,PreviousAlertType,NewAlertType,PreviousSeverity,NewSeverity,
         OnHandSnapshot,ReservedSnapshot,AvailableSnapshot,MinLevelSnapshot,SourceType,SourceId,Reason,ActorStaffId,CreatedAtUtc)
        VALUES(@Coverage17Alert,N'CONFIRMED',N'RESOLVED',N'LOW_STOCK',N'LOW_STOCK',N'MEDIUM',N'MEDIUM',
               300,0,300,500,N'PURCHASE_ADVICE',NULL,N'DEMO_COVERAGE_V17 transition',@Coverage17Staff2,@Coverage17Now);

    IF NOT EXISTS(SELECT 1 FROM dbo.RestockRequestFulfillments
                  WHERE RestockRequestId=@Coverage17Restock AND Notes=N'DEMO_COVERAGE_V17 fulfillment')
        INSERT dbo.RestockRequestFulfillments
        (RestockRequestId,SourceType,InventoryDocumentDetailId,Status,PlannedBaseQuantity,CreatedAt,CreatedByStaffId,Notes)
        VALUES(@Coverage17Restock,N'PURCHASE',NULL,N'COMPLETED',8000,@Coverage17Now,@Coverage17Staff,N'DEMO_COVERAGE_V17 fulfillment');

    DECLARE @Coverage17Advice int;
    IF NOT EXISTS(SELECT 1 FROM dbo.PurchaseAdvices WHERE RequestKey=N'DEMO_COVERAGE_V17_ADVICE')
        INSERT dbo.PurchaseAdvices
        (AdviceNumber,RequestKey,StoreId,RequestedByStaffId,Status,NeededByDate,Priority,Note,
         SubmittedAtUtc,ReviewedAtUtc,ReviewedByStaffId,CreatedAtUtc,UpdatedAtUtc)
        VALUES(N'DEMO-ADV-V17-001',N'DEMO_COVERAGE_V17_ADVICE',1,@Coverage17Staff,N'APPROVED',
               '2026-07-25',N'HIGH',N'DEMO_COVERAGE_V17 purchase advice',
               @Coverage17Now,@Coverage17Now,@Coverage17Staff,@Coverage17Now,@Coverage17Now);
    SELECT @Coverage17Advice=PurchaseAdviceId FROM dbo.PurchaseAdvices WHERE RequestKey=N'DEMO_COVERAGE_V17_ADVICE';

    DECLARE @Coverage17AdviceLine int;
    IF NOT EXISTS(SELECT 1 FROM dbo.PurchaseAdviceLines
                  WHERE PurchaseAdviceId=@Coverage17Advice AND Note=N'DEMO_COVERAGE_V17 advice line')
        INSERT dbo.PurchaseAdviceLines
        (PurchaseAdviceId,RestockRequestId,IngredientId,RequestedPurchaseBaseQuantity,
         AllocatedToPoBaseQuantity,AcceptedBaseQuantity,ClosedBaseQuantity,BaseUnitId,
         RequestedProcurementQuantity,PurchaseMode,AllocatedToPoProcurementQuantity,
         AcceptedProcurementQuantity,ClosedProcurementQuantity,ProcurementUnitId,
         RestockSourcingAllocationId,NeededByDate,Note,IsActiveReservation)
        VALUES(@Coverage17Advice,@Coverage17Restock,14,1000,1000,1000,0,@Coverage17BaseUnit,
               1,N'Packaged',1,1,0,@Coverage17ProcUnit,NULL,'2026-07-25',
               N'DEMO_COVERAGE_V17 advice line',0);
    SELECT @Coverage17AdviceLine=PurchaseAdviceLineId FROM dbo.PurchaseAdviceLines
    WHERE PurchaseAdviceId=@Coverage17Advice AND Note=N'DEMO_COVERAGE_V17 advice line';

    DECLARE @Coverage17Sourcing int;
    IF NOT EXISTS(SELECT 1 FROM dbo.RestockSourcingAllocations
                  WHERE RestockRequestId=@Coverage17Restock AND Reason=N'DEMO_COVERAGE_V17 sourcing')
        INSERT dbo.RestockSourcingAllocations
        (RestockRequestId,DecisionType,ProcurementQuantity,ProcurementUnitId,Status,
         SourceDocumentType,SourceDocumentId,SourceDocumentLineId,PurchaseAdviceLineId,
         PurchaseOrderLineId,InventoryTransferId,ProductionRunId,Reason,CreatedByStaffId,CreatedAtUtc)
        VALUES(@Coverage17Restock,N'PURCHASE',1,@Coverage17ProcUnit,N'COMPLETED',
               N'PURCHASE_ORDER',@Coverage17Po,@Coverage17Pol,@Coverage17AdviceLine,
               @Coverage17Pol,NULL,NULL,N'DEMO_COVERAGE_V17 sourcing',@Coverage17Staff,@Coverage17Now);
    SELECT @Coverage17Sourcing=RestockSourcingAllocationId FROM dbo.RestockSourcingAllocations
    WHERE RestockRequestId=@Coverage17Restock AND Reason=N'DEMO_COVERAGE_V17 sourcing';
    UPDATE dbo.PurchaseAdviceLines SET RestockSourcingAllocationId=@Coverage17Sourcing
    WHERE PurchaseAdviceLineId=@Coverage17AdviceLine AND RestockSourcingAllocationId IS NULL;

    IF NOT EXISTS(SELECT 1 FROM dbo.PurchaseAdviceTransitions
                  WHERE PurchaseAdviceId=@Coverage17Advice AND Reason=N'DEMO_COVERAGE_V17 approved')
        INSERT dbo.PurchaseAdviceTransitions(PurchaseAdviceId,PreviousStatus,NewStatus,ActorStaffId,OccurredAtUtc,Reason)
        VALUES(@Coverage17Advice,N'SUBMITTED',N'APPROVED',@Coverage17Staff,@Coverage17Now,N'DEMO_COVERAGE_V17 approved');

    DECLARE @Coverage17Batch int;
    IF NOT EXISTS(SELECT 1 FROM dbo.PurchaseOrderBatches WHERE RequestKey=N'DEMO_COVERAGE_V17_PO_BATCH')
        INSERT dbo.PurchaseOrderBatches
        (BatchNumber,RequestKey,SupplierId,Status,Currency,ExpectedDeliveryFrom,ExpectedDeliveryTo,
         Note,CreatedByStaffId,ApprovedByStaffId,ApprovedAtUtc,CreatedAtUtc,UpdatedAtUtc)
        VALUES(N'DEMO-POB-V17-001',N'DEMO_COVERAGE_V17_PO_BATCH',@Coverage17Supplier,N'APPROVED',N'VND',
               '2026-07-24','2026-07-25',N'DEMO_COVERAGE_V17 supplier consolidation',
               @Coverage17Staff,@Coverage17Staff,@Coverage17Now,@Coverage17Now,@Coverage17Now);
    SELECT @Coverage17Batch=PurchaseOrderBatchId FROM dbo.PurchaseOrderBatches WHERE RequestKey=N'DEMO_COVERAGE_V17_PO_BATCH';

    DECLARE @Coverage17BatchLine int;
    IF NOT EXISTS(SELECT 1 FROM dbo.PurchaseOrderBatchLines
                  WHERE PurchaseOrderBatchId=@Coverage17Batch AND IngredientId=14)
        INSERT dbo.PurchaseOrderBatchLines
        (PurchaseOrderBatchId,IngredientId,IngredientSupplierId,PackageUnitId,PackageQuantitySnapshot,
         TotalPackageCount,PurchaseMode,OrderedPackageCount,TotalBaseQuantity,TotalProcurementQuantity,
         UnitPricePerPackage,UnitPricePerProcurementUnit,DemandCoveredProcurementQuantity,
         RoundingSurplusProcurementQuantity,ProcurementUnitId,PackagePriceSnapshot,LineTotal,Currency,Note)
        SELECT @Coverage17Batch,14,@Coverage17Offer,PackageUnitIdSnapshot,PackageQuantitySnapshot,
               1,N'Packaged',1,1000,1,UnitPricePerPackage,NULL,1,0,ProcurementUnitId,
               UnitPricePerPackage,UnitPricePerPackage,N'VND',N'DEMO_COVERAGE_V17 batch line'
        FROM dbo.PurchaseOrderLines WHERE PurchaseOrderLineId=@Coverage17Pol;
    SELECT @Coverage17BatchLine=PurchaseOrderBatchLineId FROM dbo.PurchaseOrderBatchLines
    WHERE PurchaseOrderBatchId=@Coverage17Batch AND IngredientId=14;

    IF NOT EXISTS(SELECT 1 FROM dbo.PurchaseOrderLineAllocations WHERE PurchaseOrderLineId=@Coverage17Pol)
        INSERT dbo.PurchaseOrderLineAllocations
        (PurchaseAdviceLineId,PurchaseOrderBatchLineId,PurchaseOrderId,PurchaseOrderLineId,
         AllocatedBaseQuantity,AllocatedPackageQuantity,PurchaseMode,AllocatedProcurementQuantity,
         DemandCoveredProcurementQuantity,RoundingSurplusProcurementQuantity,ProcurementUnitId,CreatedAtUtc)
        VALUES(@Coverage17AdviceLine,@Coverage17BatchLine,@Coverage17Po,@Coverage17Pol,
               1000,1,N'Packaged',1,1,0,@Coverage17ProcUnit,@Coverage17Now);
    IF NOT EXISTS(SELECT 1 FROM dbo.PurchaseOrderBatchDocumentRevisions
                  WHERE PurchaseOrderBatchId=@Coverage17Batch AND RevisionNumber=1)
        INSERT dbo.PurchaseOrderBatchDocumentRevisions
        (PurchaseOrderBatchId,RevisionNumber,GeneratedAtUtc,GeneratedByStaffId,FileName,StorageReference,
         ContentHash,SnapshotJson,Status,SentChannel,SentAtUtc,SentByStaffId,SentNote,
         SentIdempotencyKey,CreatedAtUtc)
        VALUES(@Coverage17Batch,1,@Coverage17Now,@Coverage17Staff,N'demo-po-batch-v17.pdf',
               N'demo://purchase-order-batch/v17/1',
               N'1717171717171717171717171717171717171717171717171717171717171717',
               N'{"marker":"DEMO_COVERAGE_V17","revision":1}',N'SENT',N'EMAIL',@Coverage17Now,
               @Coverage17Staff,N'Demo supplier document',N'DEMO_COVERAGE_V17_SEND_1',@Coverage17Now);

    /* Consolidation evidence and transfer cost lineage. */
    DECLARE @Coverage17Consolidation int;
    DECLARE @Coverage17StoreInventory int=(SELECT TOP(1) StoreInventoryId FROM dbo.StoreInventories
                                           WHERE StoreId=1 AND PreparedItemId IS NOT NULL ORDER BY StoreInventoryId);
    DECLARE @Coverage17Prepared int=(SELECT PreparedItemId FROM dbo.StoreInventories WHERE StoreInventoryId=@Coverage17StoreInventory);
    DECLARE @Coverage17Available decimal(18,3)=(SELECT AvailableQty FROM dbo.StoreInventories WHERE StoreInventoryId=@Coverage17StoreInventory);
    IF NOT EXISTS(SELECT 1 FROM dbo.InventoryConsolidationRuns
                  WHERE StoreId=1 AND RequestKey='17171717-1717-0000-0000-171717171717')
        INSERT dbo.InventoryConsolidationRuns
        (StoreId,RequestKey,RunType,Status,ManifestVersion,QueryContractVersion,ManifestHash,DryRunHash,
         EnvironmentFingerprint,ManifestJson,ReportJson,RequestedByStaffId,ApprovedByStaffId,ExecutedByStaffId,
         CreatedAt,DryRunAt,CompletedAt,BeforeAvailableTotal,BeforeReservedTotal,AfterAvailableTotal,AfterReservedTotal)
        VALUES(1,'17171717-1717-0000-0000-171717171717',1,5,N'demo-v17',N'demo-v17',
               N'1717171717171717171717171717171717171717171717171717171717171717',
               N'1717171717171717171717171717171717171717171717171717171717171717',
               N'DEMO_COVERAGE_V17',N'{"mode":"audit-no-op"}',N'{"result":"no-op"}',
               @Coverage17Staff,@Coverage17Staff,@Coverage17Staff,@Coverage17Now,@Coverage17Now,@Coverage17Now,
               @Coverage17Available,0,@Coverage17Available,0);
    SELECT @Coverage17Consolidation=InventoryConsolidationRunId FROM dbo.InventoryConsolidationRuns
    WHERE StoreId=1 AND RequestKey='17171717-1717-0000-0000-171717171717';
    IF NOT EXISTS(SELECT 1 FROM dbo.InventoryConsolidationLines
                  WHERE InventoryConsolidationRunId=@Coverage17Consolidation
                    AND StoreInventoryId=@Coverage17StoreInventory AND LineRole=2)
        INSERT dbo.InventoryConsolidationLines
        (InventoryConsolidationRunId,StoreInventoryId,LineRole,PreparedItemId,SourceRecipeId,
         BeforeAvailableQty,BeforeReservedQty,BeforeMinStockLevel,BeforeMaxNegativeQty,
         BeforeIdentityState,BeforeQuantitySemantics,ApprovedConversionFactor,
         ApprovedConversionFromUnitId,ApprovedConversionToUnitId,ConvertedAvailableQty,
         ConvertedReservedQty,AfterAvailableQty,AfterReservedQty,EvidenceType,EvidenceReference,IsTargetCreated)
        SELECT @Coverage17Consolidation,StoreInventoryId,2,PreparedItemId,RecipeId,
               AvailableQty,ReservedQty,MinStockLevel,NULL,NULL,NULL,NULL,NULL,NULL,
               AvailableQty,ReservedQty,AvailableQty,ReservedQty,
               N'DEMO_AUDIT_NO_OP',N'DEMO_COVERAGE_V17',0
        FROM dbo.StoreInventories WHERE StoreInventoryId=@Coverage17StoreInventory;

    DECLARE @Coverage17TransferDetail int=(SELECT TOP(1) itd.InventoryTransferDetailId
                                           FROM dbo.InventoryTransferDetails itd
                                           JOIN dbo.InventoryTransfers it ON it.InventoryTransferId=itd.InventoryTransferId
                                           WHERE it.Code=N'SEEDALL_TRANSFER_20260104' AND itd.IngredientId IS NOT NULL
                                           ORDER BY itd.InventoryTransferDetailId);
    DECLARE @Coverage17Layer int=(SELECT TOP(1) l.InventoryCostLayerId
                                  FROM dbo.InventoryTransferDetails itd
                                  JOIN dbo.InventoryTransfers it ON it.InventoryTransferId=itd.InventoryTransferId
                                  JOIN dbo.InventoryCostLayers l ON l.StoreId=it.FromStoreId AND l.IngredientId=itd.IngredientId
                                  WHERE itd.InventoryTransferDetailId=@Coverage17TransferDetail
                                    AND l.RemainingQuantity>0 ORDER BY l.InventoryCostLayerId);
    IF @Coverage17TransferDetail IS NOT NULL AND @Coverage17Layer IS NOT NULL
       AND NOT EXISTS(SELECT 1 FROM dbo.InventoryTransferCostAllocations
                      WHERE InventoryTransferDetailId=@Coverage17TransferDetail
                        AND SourceInventoryCostLayerId=@Coverage17Layer)
        INSERT dbo.InventoryTransferCostAllocations
        (InventoryTransferDetailId,SourceInventoryCostLayerId,Quantity,ReceivedQuantity,UnitCost,TotalCost,CreatedAt)
        SELECT @Coverage17TransferDetail,@Coverage17Layer,1,0,UnitCost,ROUND(UnitCost,2),@Coverage17Now
        FROM dbo.InventoryCostLayers WHERE InventoryCostLayerId=@Coverage17Layer;

    IF NOT EXISTS(SELECT 1 FROM dbo.StaffNotifications WHERE DeduplicationKey=N'DEMO_COVERAGE_V17_STOCK_ALERT')
        INSERT dbo.StaffNotifications
        (StoreId,RecipientStaffId,Type,Title,Body,Severity,DeduplicationKey,UpdatedAt,ResolvedAt,
         EntityType,EntityId,IsRead,ReadAt,CreatedAt,EmailAttempted,EmailSent,EmailErrorSummary)
        VALUES(2,@Coverage17Staff2,N'STOCK_ALERT_RESOLVED',N'Cảnh báo tồn kho demo',
               N'Kịch bản DEMO_COVERAGE_V17 đã có purchase advice.',N'INFO',
               N'DEMO_COVERAGE_V17_STOCK_ALERT',@Coverage17Now,@Coverage17Now,
               N'StockAlert',@Coverage17Alert,1,@Coverage17Now,@Coverage17Now,0,0,NULL);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

EXEC #SeedAllInventoryProcurementV2;
DROP PROCEDURE #SeedAllInventoryProcurementV2;
GO
