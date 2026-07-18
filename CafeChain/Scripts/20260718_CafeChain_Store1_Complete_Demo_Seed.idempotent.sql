/*
    Store 1 seed chạy SAU EF HasData, Part1_SeedDataDrink.sql,
    Part7_SeedDataPermission.sql và SeedDataDiaChi.sql.

    Quyền sở hữu:
      - Không sửa ID/dữ liệu do Configuration, Part1, Part7 hoặc DiaChi tạo.
      - Quan hệ mới luôn resolve FK bằng business code.
      - Chỉ dữ liệu có DEMO_* / DEMO-STORE1-* thuộc script này.
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

DECLARE @SeedUtc datetime2(0) = '2026-01-15T08:00:00';
DECLARE @HistoryUtc datetime2(0) = '2026-01-01T00:00:00';
DECLARE @StoreId int = 1;
DECLARE @DestinationStoreId int = 2;
DECLARE @ActorStaffId int;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Stores', N'U') IS NULL
       OR OBJECT_ID(N'dbo.Drinks', N'U') IS NULL
       OR OBJECT_ID(N'dbo.IngredientSuppliers', N'U') IS NULL
        THROW 51000, N'Database đích chưa có schema CafeChain yêu cầu.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.Stores WHERE StoreId=@StoreId AND Active=1)
        THROW 51001, N'Thiếu StoreId 1 active từ EF Configuration.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.Drinks WHERE DrinkId BETWEEN 7 AND 30)
        THROW 51002, N'Chưa chạy Part1_SeedDataDrink.sql trước seed Store 1.', 1;

    SELECT TOP (1) @ActorStaffId=StaffId
    FROM dbo.Staffs
    WHERE StoreId=@StoreId AND Active=1
    ORDER BY CASE WHEN StaffId=1 THEN 0 ELSE 1 END, StaffId;

    IF @ActorStaffId IS NULL
        THROW 51003, N'Store 1 không có Staff active để làm audit actor.', 1;

    /* 1. Kích hoạt menu Part1 cho Store 1; không thay đổi dữ liệu Drink gốc. */
    INSERT dbo.StoreDrinks(StoreId,DrinkId,Active)
    SELECT @StoreId,d.DrinkId,1
    FROM dbo.Drinks d
    WHERE d.DrinkId BETWEEN 7 AND 30
      AND d.Active=1
      AND NOT EXISTS (
          SELECT 1 FROM dbo.StoreDrinks sd
          WHERE sd.StoreId=@StoreId AND sd.DrinkId=d.DrinkId);

    UPDATE sd SET Active=1
    FROM dbo.StoreDrinks sd
    JOIN dbo.Drinks d ON d.DrinkId=sd.DrinkId
    WHERE sd.StoreId=@StoreId AND d.DrinkId BETWEEN 7 AND 30 AND sd.Active=0;

    INSERT dbo.StoreMenuItems(
        StoreId,DrinkSizeId,IsEnabled,PriceOverride,EffectiveFromUtc,EffectiveToUtc,
        DisplayOrder,PauseReason,Note,PublishedAtUtc,PublishedByStaffId,CreatedAtUtc,UpdatedAtUtc)
    SELECT @StoreId,ds.DrinkSizeId,1,NULL,@SeedUtc,NULL,
           ROW_NUMBER() OVER (ORDER BY d.DrinkId,ds.SizeId),NULL,
           N'DEMO_PART1_SKU_'+CONVERT(nvarchar(20),d.DrinkId)+N'_'+CONVERT(nvarchar(20),ds.SizeId),
           @SeedUtc,@ActorStaffId,@SeedUtc,@SeedUtc
    FROM dbo.DrinkSizes ds
    JOIN dbo.Drinks d ON d.DrinkId=ds.DrinkId
    WHERE d.DrinkId BETWEEN 7 AND 30
      AND ds.Active=1
      AND NOT EXISTS (
          SELECT 1 FROM dbo.StoreMenuItems sm
          WHERE sm.StoreId=@StoreId AND sm.DrinkSizeId=ds.DrinkSizeId);

    /* 2. BOM tối thiểu cho đồ uống pha chế Part1; FK được resolve bằng code. */
    INSERT dbo.Recipes(
        RecipeCode,Name,YieldPercentage,Active,Status,EffectiveDate,
        ParentVersionId,DrinkId,SizeId,ToppingId,PreparedItemId,OutputQuantity,OutputUnitId)
    SELECT N'DEMO_PART1_RECIPE_'+d.DrinkCode+N'_'+s.SizeCode,
           N'Công thức demo '+d.Name+N' '+s.Name,
           100,1,N'Active',@SeedUtc,NULL,d.DrinkId,s.SizeId,NULL,NULL,NULL,NULL
    FROM dbo.DrinkSizes ds
    JOIN dbo.Drinks d ON d.DrinkId=ds.DrinkId
    JOIN dbo.Sizes s ON s.SizeId=ds.SizeId
    WHERE d.DrinkId BETWEEN 7 AND 30
      AND d.ProductTypeId=1
      AND ds.Active=1
      AND NOT EXISTS (
          SELECT 1 FROM dbo.Recipes r
          WHERE r.DrinkId=d.DrinkId AND r.SizeId=s.SizeId AND r.Active=1);

    INSERT dbo.RecipeDetails(RecipeId,IngredientId,ChildRecipeId,Quantity,UnitId)
    SELECT r.RecipeId,
           CASE
             WHEN d.CategoryId=1 THEN (SELECT IngredientId FROM dbo.Ingredients WHERE Code=N'ING00001')
             WHEN d.CategoryId IN (2,4,5) THEN (SELECT IngredientId FROM dbo.Ingredients WHERE Code=N'ING00003')
             WHEN d.CategoryId=8 THEN (SELECT IngredientId FROM dbo.Ingredients WHERE Code=N'ING00010')
             ELSE (SELECT IngredientId FROM dbo.Ingredients WHERE Code=N'ING00013')
           END,
           NULL,
           CASE WHEN d.CategoryId=1 THEN 18 WHEN d.CategoryId IN (2,4,5) THEN 8 ELSE 120 END,
           CASE WHEN d.CategoryId IN (8,6) THEN 3 ELSE 1 END
    FROM dbo.Recipes r
    JOIN dbo.Drinks d ON d.DrinkId=r.DrinkId
    WHERE r.RecipeCode LIKE N'DEMO_PART1_RECIPE[_]%'
      AND NOT EXISTS (SELECT 1 FROM dbo.RecipeDetails rd WHERE rd.RecipeId=r.RecipeId);

    /* 3. Supplier: hai active và một inactive. */
    DECLARE @Suppliers TABLE(
        Code nvarchar(50) PRIMARY KEY, Name nvarchar(200), TaxCode nvarchar(14),
        Active bit, LeadTimeDays int);
    INSERT @Suppliers VALUES
      (N'DEMO_SUP_COFFEE',N'Nhà cung cấp Cà phê Demo',N'0319000001',1,2),
      (N'DEMO_SUP_TEA_FRUIT',N'Nhà cung cấp Trà và Nguyên liệu tươi Demo',N'0319000002',1,1),
      (N'DEMO_SUP_INACTIVE',N'Nhà cung cấp ngừng hoạt động Demo',N'0319000003',0,5);

    INSERT dbo.Suppliers(Code,Name,TaxCode,Address,Active,CreatedAt,UpdatedAt,Note)
    SELECT x.Code,x.Name,x.TaxCode,N'TP. Hồ Chí Minh',x.Active,@SeedUtc,@SeedUtc,N'DEMO_STORE1_SUPPLIER'
    FROM @Suppliers x
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Suppliers s WHERE s.Code=x.Code);

    UPDATE s SET Name=x.Name,TaxCode=x.TaxCode,Active=x.Active,UpdatedAt=@SeedUtc,Note=N'DEMO_STORE1_SUPPLIER'
    FROM dbo.Suppliers s JOIN @Suppliers x ON x.Code=s.Code
    WHERE s.Note=N'DEMO_STORE1_SUPPLIER';

    INSERT dbo.SupplierStores(
        SupplierId,StoreId,Active,LeadTimeOverrideDays,DeliverySchedule,Note,CreatedAt,UpdatedAt)
    SELECT s.SupplierId,@StoreId,x.Active,x.LeadTimeDays,
           CASE WHEN x.Active=1 THEN N'Thứ 2 - Thứ 4 - Thứ 6' ELSE N'Ngừng giao hàng' END,
           N'DEMO_STORE1_SUPPLIER',@SeedUtc,@SeedUtc
    FROM @Suppliers x JOIN dbo.Suppliers s ON s.Code=x.Code
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.SupplierStores ss
        WHERE ss.SupplierId=s.SupplierId AND ss.StoreId=@StoreId);

    UPDATE ss SET Active=x.Active,LeadTimeOverrideDays=x.LeadTimeDays,UpdatedAt=@SeedUtc
    FROM dbo.SupplierStores ss
    JOIN dbo.Suppliers s ON s.SupplierId=ss.SupplierId
    JOIN @Suppliers x ON x.Code=s.Code
    WHERE ss.StoreId=@StoreId AND ss.Note=N'DEMO_STORE1_SUPPLIER';

    /* 4. Gói mua và lịch sử giá. PackageQuantity là lượng trong UnitId của một gói. */
    DECLARE @Offers TABLE(
        Marker nvarchar(80) PRIMARY KEY, SupplierCode nvarchar(50), IngredientCode nvarchar(50),
        UnitCode nvarchar(20), PackageQuantity decimal(18,5), OldPrice decimal(18,2),
        CurrentPrice decimal(18,2), Moq int, LeadTime int, IsPrimary bit);
    INSERT @Offers VALUES
      (N'DEMO_OFFER_COFFEE',N'DEMO_SUP_COFFEE',N'ING00001',N'kg',1,165000,180000,2,2,1),
      (N'DEMO_OFFER_SUGAR',N'DEMO_SUP_TEA_FRUIT',N'ING00006',N'kg',1,20000,22000,5,1,1),
      (N'DEMO_OFFER_BLACK_TEA',N'DEMO_SUP_TEA_FRUIT',N'ING00003',N'g',500,105000,120000,2,1,1),
      (N'DEMO_OFFER_MATCHA',N'DEMO_SUP_TEA_FRUIT',N'ING00009',N'g',500,420000,450000,1,1,1),
      (N'DEMO_OFFER_CONDENSED_MILK',N'DEMO_SUP_TEA_FRUIT',N'ING00002',N'ml',4560,315000,324000,1,1,1),
      (N'DEMO_OFFER_DAIRY_CREAM',N'DEMO_SUP_TEA_FRUIT',N'ING00010',N'l',12,1080000,1140000,1,1,1),
      (N'DEMO_OFFER_WATER',N'DEMO_SUP_TEA_FRUIT',N'ING00013',N'l',20,28000,30000,1,1,0);

    INSERT dbo.IngredientSuppliers(
        IngredientId,SupplierId,UnitId,PackageQuantity,CurrentPrice,
        MinimumOrderPackageCount,LeadTimeDays,IsPrimary,Active,Note,CreatedAt,UpdatedAt)
    SELECT i.IngredientId,s.SupplierId,u.UnitId,x.PackageQuantity,x.CurrentPrice,
           x.Moq,x.LeadTime,x.IsPrimary,1,x.Marker,@SeedUtc,@SeedUtc
    FROM @Offers x
    JOIN dbo.Ingredients i ON i.Code=x.IngredientCode
    JOIN dbo.Suppliers s ON s.Code=x.SupplierCode
    JOIN dbo.Units u ON u.UnitCode=x.UnitCode
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.IngredientSuppliers o
        WHERE o.IngredientId=i.IngredientId AND o.SupplierId=s.SupplierId);

    UPDATE o SET UnitId=u.UnitId,PackageQuantity=x.PackageQuantity,CurrentPrice=x.CurrentPrice,
                 MinimumOrderPackageCount=x.Moq,LeadTimeDays=x.LeadTime,IsPrimary=x.IsPrimary,
                 Active=1,Note=x.Marker,UpdatedAt=@SeedUtc
    FROM dbo.IngredientSuppliers o
    JOIN dbo.Ingredients i ON i.IngredientId=o.IngredientId
    JOIN dbo.Suppliers s ON s.SupplierId=o.SupplierId
    JOIN @Offers x ON x.IngredientCode=i.Code AND x.SupplierCode=s.Code
    JOIN dbo.Units u ON u.UnitCode=x.UnitCode;

    INSERT dbo.IngredientSupplierPriceHistories(
        IngredientSupplierId,Price,PackageQuantity,PackageUnitId,EffectiveDate,
        IsCurrent,Note,CreatedByStaffId,CreatedAtUtc)
    SELECT o.IngredientSupplierId,x.OldPrice,x.PackageQuantity,u.UnitId,@HistoryUtc,
           0,N'Giá cũ '+x.Marker,@ActorStaffId,@HistoryUtc
    FROM @Offers x
    JOIN dbo.Ingredients i ON i.Code=x.IngredientCode
    JOIN dbo.Suppliers s ON s.Code=x.SupplierCode
    JOIN dbo.IngredientSuppliers o ON o.IngredientId=i.IngredientId AND o.SupplierId=s.SupplierId
    JOIN dbo.Units u ON u.UnitCode=x.UnitCode
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.IngredientSupplierPriceHistories h
        WHERE h.IngredientSupplierId=o.IngredientSupplierId AND h.IsCurrent=0);

    INSERT dbo.IngredientSupplierPriceHistories(
        IngredientSupplierId,Price,PackageQuantity,PackageUnitId,EffectiveDate,
        IsCurrent,Note,CreatedByStaffId,CreatedAtUtc)
    SELECT o.IngredientSupplierId,x.CurrentPrice,x.PackageQuantity,u.UnitId,@SeedUtc,
           1,N'Giá hiện tại '+x.Marker,@ActorStaffId,@SeedUtc
    FROM @Offers x
    JOIN dbo.Ingredients i ON i.Code=x.IngredientCode
    JOIN dbo.Suppliers s ON s.Code=x.SupplierCode
    JOIN dbo.IngredientSuppliers o ON o.IngredientId=i.IngredientId AND o.SupplierId=s.SupplierId
    JOIN dbo.Units u ON u.UnitCode=x.UnitCode
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.IngredientSupplierPriceHistories h
        WHERE h.IngredientSupplierId=o.IngredientSupplierId AND h.IsCurrent=1);

    UPDATE h SET Price=x.CurrentPrice,PackageQuantity=x.PackageQuantity,
                 PackageUnitId=u.UnitId,EffectiveDate=@SeedUtc,Note=N'Giá hiện tại '+x.Marker
    FROM dbo.IngredientSupplierPriceHistories h
    JOIN dbo.IngredientSuppliers o ON o.IngredientSupplierId=h.IngredientSupplierId
    JOIN dbo.Ingredients i ON i.IngredientId=o.IngredientId
    JOIN dbo.Suppliers s ON s.SupplierId=o.SupplierId
    JOIN @Offers x ON x.IngredientCode=i.Code AND x.SupplierCode=s.Code
    JOIN dbo.Units u ON u.UnitCode=x.UnitCode
    WHERE h.IsCurrent=1;

    /* 5. Tồn đầu kỳ, transaction và FIFO layer cân bằng. */
    DECLARE @Opening TABLE(IngredientCode nvarchar(50) PRIMARY KEY,Quantity decimal(18,5),UnitCost decimal(18,4));
    INSERT @Opening VALUES
      (N'ING00001',20000,180),(N'ING00002',30000,71.0526),(N'ING00003',10000,240),
      (N'ING00006',30000,22),(N'ING00009',5000,900),(N'ING00010',24000,95),(N'ING00013',100000,1.5);

    INSERT dbo.StoreInventories(StoreId,IngredientId,RecipeId,PreparedItemId,AvailableQty,ReservedQty,MaxNegativeQty,MinStockLevel,LastUpdated)
    SELECT @StoreId,i.IngredientId,NULL,NULL,0,0,0,x.Quantity*0.10,@SeedUtc
    FROM @Opening x JOIN dbo.Ingredients i ON i.Code=x.IngredientCode
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.StoreInventories si
        WHERE si.StoreId=@StoreId AND si.IngredientId=i.IngredientId);

    DECLARE @OpeningDocumentId int;
    SELECT @OpeningDocumentId=InventoryDocumentId
    FROM dbo.InventoryDocuments WHERE RequestKey=N'DEMO-STORE1-OPENING-20260115';

    IF @OpeningDocumentId IS NULL
    BEGIN
        INSERT dbo.InventoryDocuments(
            Code,StoreId,StaffId,DocumentDate,Type,Status,RequestKey,IsProcessing,
            ConfirmedAt,ConfirmedBy,Purpose,PartnerType,PartnerId,PartnerName,SupplierId,
            Note,NegativeReason,TotalAmount,VatAmount,FinalAmount)
        VALUES(N'DEMO-OPENING-STORE1-20260115',@StoreId,@ActorStaffId,@SeedUtc,8,3,
               N'DEMO-STORE1-OPENING-20260115',0,@SeedUtc,@ActorStaffId,3,0,NULL,NULL,NULL,
               N'Tồn đầu kỳ cố định cho kiểm thử Store 1',NULL,0,0,0);
        SET @OpeningDocumentId=SCOPE_IDENTITY();
    END;

    INSERT dbo.InventoryDocumentDetails(
        InventoryDocumentId,IngredientId,Quantity,BaseQuantity,UnitId,
        UnitPrice,CostPrice,CostAmount,Note,TotalAmount)
    SELECT @OpeningDocumentId,i.IngredientId,x.Quantity,x.Quantity,i.BaseUnitId,
           x.UnitCost,x.UnitCost,x.Quantity*x.UnitCost,N'DEMO_STORE1_OPENING',x.Quantity*x.UnitCost
    FROM @Opening x JOIN dbo.Ingredients i ON i.Code=x.IngredientCode
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.InventoryDocumentDetails d
        WHERE d.InventoryDocumentId=@OpeningDocumentId AND d.IngredientId=i.IngredientId);

    DECLARE @NewPosting TABLE(
        DetailId int PRIMARY KEY,StoreInventoryId int,Quantity decimal(18,5),
        BeforeQty decimal(18,5),UnitCost decimal(18,4));
    INSERT @NewPosting
    SELECT d.InventoryDocumentDetailId,si.StoreInventoryId,d.BaseQuantity,si.AvailableQty,d.CostPrice
    FROM dbo.InventoryDocumentDetails d
    JOIN dbo.StoreInventories si ON si.StoreId=@StoreId AND si.IngredientId=d.IngredientId
    WHERE d.InventoryDocumentId=@OpeningDocumentId
      AND NOT EXISTS (
          SELECT 1 FROM dbo.InventoryTransactions t
          WHERE t.InventoryDocumentDetailId=d.InventoryDocumentDetailId AND t.Type=8);

    UPDATE si SET AvailableQty=si.AvailableQty+p.Quantity,LastUpdated=@SeedUtc
    FROM dbo.StoreInventories si JOIN @NewPosting p ON p.StoreInventoryId=si.StoreInventoryId;

    INSERT dbo.InventoryTransactions(
        StoreInventoryId,Type,StockStatus,Quantity,BeforeQty,AfterQty,UnitCost,TotalCost,
        InventoryDocumentId,InventoryDocumentDetailId,CreatedAt)
    SELECT p.StoreInventoryId,8,1,p.Quantity,p.BeforeQty,p.BeforeQty+p.Quantity,
           p.UnitCost,p.Quantity*p.UnitCost,@OpeningDocumentId,p.DetailId,@SeedUtc
    FROM @NewPosting p;

    INSERT dbo.InventoryCostLayers(
        IngredientId,PreparedItemId,StoreId,Quantity,RemainingQuantity,UnitCost,CreatedAt,
        SourceInventoryDocumentDetailId)
    SELECT d.IngredientId,NULL,@StoreId,d.BaseQuantity,d.BaseQuantity,d.CostPrice,@SeedUtc,d.InventoryDocumentDetailId
    FROM dbo.InventoryDocumentDetails d
    WHERE d.InventoryDocumentId=@OpeningDocumentId
      AND NOT EXISTS (
          SELECT 1 FROM dbo.InventoryCostLayers l
          WHERE l.SourceInventoryDocumentDetailId=d.InventoryDocumentDetailId);

    /* 6. Phiếu chuyển kho nháp: đủ dữ liệu để chạy workflow xác nhận thực tế. */
    IF EXISTS (SELECT 1 FROM dbo.Stores WHERE StoreId=@DestinationStoreId AND Active=1)
       AND NOT EXISTS (SELECT 1 FROM dbo.InventoryTransfers WHERE RequestKey=N'DEMO-TRANSFER-STORE1-STORE2')
    BEGIN
        INSERT dbo.InventoryTransfers(
            Code,RequestKey,FromStoreId,ToStoreId,Type,Purpose,Status,DocumentDate,
            CreatedByStaffId,CreatedAt,Note)
        VALUES(N'DEMO-CK-STORE1-STORE2',N'DEMO-TRANSFER-STORE1-STORE2',@StoreId,@DestinationStoreId,
               1,1,1,@SeedUtc,@ActorStaffId,@SeedUtc,N'Phiếu chuyển kho nháp để kiểm thử xác nhận');

        DECLARE @TransferId int=SCOPE_IDENTITY();
        INSERT dbo.InventoryTransferDetails(
            InventoryTransferId,IngredientId,PreparedItemId,RestockRequestId,RestockRequestFulfillmentId,
            UnitId,Quantity,BaseQuantity,DispatchedBaseQuantity,ReceivedBaseQuantity,UnitPrice,Note)
        SELECT @TransferId,i.IngredientId,NULL,NULL,NULL,i.BaseUnitId,1000,1000,0,0,180,N'DEMO_TRANSFER_LINE'
        FROM dbo.Ingredients i WHERE i.Code=N'ING00001';
    END;

    /* 7. Fail-fast invariants. */
    IF EXISTS (
        SELECT IngredientSupplierId
        FROM dbo.IngredientSupplierPriceHistories
        GROUP BY IngredientSupplierId
        HAVING SUM(CASE WHEN IsCurrent=1 THEN 1 ELSE 0 END)>1)
        THROW 51010, N'Có nhiều hơn một giá hiện tại cho cùng gói mua.', 1;

    IF EXISTS (
        SELECT 1 FROM @Offers x
        LEFT JOIN dbo.Ingredients i ON i.Code=x.IngredientCode
        LEFT JOIN dbo.Suppliers s ON s.Code=x.SupplierCode
        LEFT JOIN dbo.IngredientSuppliers o ON o.IngredientId=i.IngredientId AND o.SupplierId=s.SupplierId
        WHERE o.IngredientSupplierId IS NULL)
        THROW 51011, N'Thiếu mapping Supplier - Ingredient sau khi seed.', 1;

    COMMIT;

    SELECT N'STORE1_SEED_OK' AS Result,
           (SELECT COUNT(*) FROM dbo.StoreMenuItems WHERE StoreId=@StoreId AND Note LIKE N'DEMO_PART1_SKU[_]%') AS Part1SkuCount,
           (SELECT COUNT(*) FROM dbo.IngredientSuppliers WHERE Note LIKE N'DEMO_OFFER[_]%') AS DemoOfferCount;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    THROW;
END CATCH;
GO
