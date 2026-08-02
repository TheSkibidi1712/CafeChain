using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class AlignSupplyChainRolePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                SET XACT_ABORT ON;

                DECLARE @GroupCatalog table
                (
                    PermissionGroupId int PRIMARY KEY,
                    Code nvarchar(50) UNIQUE,
                    Name nvarchar(150) UNIQUE,
                    DisplayOrder int NOT NULL,
                    Active bit NOT NULL
                );
                INSERT @GroupCatalog VALUES
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
                (26,N'REORDER_SUGGESTION',N'Gợi ý nhập hàng',27,1);

                IF EXISTS
                (
                    SELECT 1
                    FROM @GroupCatalog x
                    JOIN dbo.PermissionGroups g
                      ON g.PermissionGroupId = x.PermissionGroupId OR g.Code = x.Code OR g.Name = x.Name
                    WHERE g.PermissionGroupId <> x.PermissionGroupId
                       OR g.Code <> x.Code
                       OR g.Name <> x.Name
                )
                    THROW 53374, N'Không thể đồng bộ phân quyền: nhóm quyền Kho & Cung ứng xung đột contract.', 1;

                SET IDENTITY_INSERT dbo.PermissionGroups ON;
                INSERT dbo.PermissionGroups(PermissionGroupId,Code,Name,DisplayOrder,Active)
                SELECT x.PermissionGroupId,x.Code,x.Name,x.DisplayOrder,x.Active
                FROM @GroupCatalog x
                WHERE NOT EXISTS
                (
                    SELECT 1
                    FROM dbo.PermissionGroups g
                    WHERE g.PermissionGroupId = x.PermissionGroupId
                );
                SET IDENTITY_INSERT dbo.PermissionGroups OFF;

                DECLARE @PermissionCatalog table
                (
                    PermissionId int PRIMARY KEY,
                    PermissionGroupId int NOT NULL,
                    Code nvarchar(100) UNIQUE,
                    Name nvarchar(200) NOT NULL,
                    Action nvarchar(50) NOT NULL,
                    Description nvarchar(500) NOT NULL,
                    Active bit NOT NULL,
                    CreatedAt datetime2 NOT NULL
                );
                INSERT @PermissionCatalog VALUES
                (28,9,N'Ingredient.View',N'Xem nguyên liệu',N'View',N'Xem nguyên liệu',1,'2026-01-01'),
                (29,9,N'Ingredient.Create',N'Tạo nguyên liệu',N'Create',N'Tạo nguyên liệu',1,'2026-01-01'),
                (30,9,N'Ingredient.Update',N'Cập nhật nguyên liệu',N'Update',N'Cập nhật nguyên liệu',1,'2026-01-01'),
                (31,9,N'Ingredient.ToggleStatus',N'Đổi trạng thái nguyên liệu',N'ToggleStatus',N'Đổi trạng thái nguyên liệu',1,'2026-01-01'),
                (32,10,N'UnitConversion.View',N'Xem quy đổi',N'View',N'Xem quy đổi',1,'2026-01-01'),
                (33,10,N'UnitConversion.Create',N'Tạo quy đổi',N'Create',N'Tạo quy đổi',1,'2026-01-01'),
                (34,10,N'UnitConversion.Update',N'Cập nhật quy đổi',N'Update',N'Cập nhật quy đổi',1,'2026-01-01'),
                (35,10,N'UnitConversion.ToggleStatus',N'Đổi trạng thái quy đổi',N'ToggleStatus',N'Đổi trạng thái quy đổi',1,'2026-01-01'),
                (36,11,N'Inventory.View',N'Xem tồn kho',N'View',N'Xem tồn kho',1,'2026-01-01'),
                (37,11,N'Inventory.Adjust',N'Điều chỉnh tồn kho',N'Adjust',N'Điều chỉnh tồn kho',1,'2026-01-01'),
                (38,11,N'Inventory.Export',N'Xuất dữ liệu tồn kho',N'Export',N'Xuất dữ liệu tồn kho',1,'2026-01-01'),
                (39,12,N'StockAlert.View',N'Xem cảnh báo kho',N'View',N'Xem cảnh báo kho',1,'2026-01-01'),
                (40,12,N'StockAlert.Resolve',N'Xử lý cảnh báo kho',N'Resolve',N'Xử lý cảnh báo kho',1,'2026-01-01'),
                (41,12,N'StockAlert.Configure',N'Cấu hình cảnh báo kho',N'Configure',N'Cấu hình cảnh báo kho',1,'2026-01-01'),
                (42,12,N'StockAlert.Export',N'Xuất cảnh báo kho',N'Export',N'Xuất cảnh báo kho',1,'2026-01-01'),
                (131,12,N'StockAlert.Create',N'Báo thiếu nguyên liệu',N'Create',N'Tạo cảnh báo thiếu nguyên liệu từ nghiệp vụ cửa hàng',1,'2026-01-01'),
                (132,12,N'StockAlert.CreateRestockRequest',N'Tạo yêu cầu nhập từ cảnh báo',N'CreateRestockRequest',N'Tạo yêu cầu nhập hàng từ cảnh báo kho đã được xác nhận',1,'2026-01-01'),
                (43,13,N'Restock.View',N'Xem yêu cầu nhập',N'View',N'Xem yêu cầu nhập',1,'2026-01-01'),
                (44,13,N'Restock.Create',N'Tạo yêu cầu nhập hàng',N'Create',N'Tạo mới, tạo nháp hoặc bổ sung yêu cầu nhập hàng từ gợi ý nhập hàng trong phạm vi cửa hàng được phép thao tác',1,'2026-01-01'),
                (45,13,N'Restock.Submit',N'Gửi yêu cầu nhập',N'Submit',N'Gửi yêu cầu nhập',1,'2026-01-01'),
                (46,13,N'Restock.Approve',N'Duyệt yêu cầu nhập',N'Approve',N'Duyệt yêu cầu nhập',1,'2026-01-01'),
                (47,13,N'Restock.Reject',N'Từ chối yêu cầu nhập',N'Reject',N'Từ chối yêu cầu nhập',1,'2026-01-01'),
                (48,13,N'Restock.Cancel',N'Hủy yêu cầu nhập',N'Cancel',N'Hủy yêu cầu nhập',1,'2026-01-01'),
                (133,13,N'Restock.Update',N'Cập nhật yêu cầu nhập',N'Update',N'Cập nhật yêu cầu nhập trước khi gửi hoặc khi trạng thái cho phép',1,'2026-01-01'),
                (134,13,N'Restock.CloseRemaining',N'Đóng phần còn lại yêu cầu nhập',N'CloseRemaining',N'Đóng phần nhu cầu nhập còn lại không tiếp tục xử lý',1,'2026-01-01'),
                (135,13,N'Restock.CreatePurchaseOrder',N'Tạo đơn đặt hàng từ yêu cầu nhập',N'CreatePurchaseOrder',N'Tạo đơn đặt hàng mua ngoài từ phần nhu cầu nhập được phân bổ',1,'2026-01-01'),
                (136,13,N'Restock.CreateTransfer',N'Tạo điều chuyển từ yêu cầu nhập',N'CreateTransfer',N'Tạo phiếu điều chuyển từ phần nhu cầu nhập được phân bổ',1,'2026-01-01'),
                (49,14,N'PurchaseAdvice.View',N'Xem đề nghị mua',N'View',N'Xem đề nghị mua',1,'2026-01-01'),
                (50,14,N'PurchaseAdvice.Create',N'Tạo đề nghị mua',N'Create',N'Tạo đề nghị mua',1,'2026-01-01'),
                (51,14,N'PurchaseAdvice.Submit',N'Gửi đề nghị mua',N'Submit',N'Gửi đề nghị mua',1,'2026-01-01'),
                (52,14,N'PurchaseAdvice.Review',N'Bắt đầu duyệt đề nghị mua',N'Review',N'Bắt đầu duyệt đề nghị mua',1,'2026-01-01'),
                (53,14,N'PurchaseAdvice.Approve',N'Duyệt đề nghị mua',N'Approve',N'Duyệt đề nghị mua',1,'2026-01-01'),
                (54,14,N'PurchaseAdvice.Reject',N'Từ chối đề nghị mua',N'Reject',N'Từ chối đề nghị mua',1,'2026-01-01'),
                (55,14,N'PurchaseAdvice.Consolidate',N'Tổng hợp đề nghị mua',N'Consolidate',N'Tổng hợp đề nghị mua',1,'2026-01-01'),
                (137,14,N'PurchaseAdvice.SelectSupplier',N'Chọn nhà cung cấp',N'SelectSupplier',N'Chọn nhà cung cấp và quy cách mua cho đề nghị mua hàng',1,'2026-01-01'),
                (138,14,N'PurchaseAdvice.CreatePurchaseOrder',N'Tạo đơn đặt hàng từ đề nghị mua',N'CreatePurchaseOrder',N'Tạo đơn đặt hàng từ đề nghị mua đã được tổng hợp',1,'2026-01-01'),
                (56,15,N'PurchaseOrder.View',N'Xem đơn đặt hàng',N'View',N'Xem đơn đặt hàng',1,'2026-01-01'),
                (57,15,N'PurchaseOrder.Create',N'Tạo đơn đặt hàng',N'Create',N'Tạo đơn đặt hàng',1,'2026-01-01'),
                (58,15,N'PurchaseOrder.Update',N'Cập nhật đơn đặt hàng',N'Update',N'Cập nhật đơn đặt hàng',1,'2026-01-01'),
                (59,15,N'PurchaseOrder.Send',N'Gửi nhà cung cấp',N'Send',N'Gửi nhà cung cấp',1,'2026-01-01'),
                (60,15,N'PurchaseOrder.Receive',N'Nhận hàng từ PO',N'Receive',N'Nhận hàng từ PO',1,'2026-01-01'),
                (61,15,N'PurchaseOrder.Cancel',N'Hủy đơn đặt hàng',N'Cancel',N'Hủy đơn đặt hàng',1,'2026-01-01'),
                (62,15,N'PurchaseOrder.ViewBatch',N'Xem batch PO',N'ViewBatch',N'Xem batch PO',1,'2026-01-01'),
                (63,15,N'PurchaseOrder.CreateBatch',N'Tạo batch PO',N'CreateBatch',N'Tạo batch PO',1,'2026-01-01'),
                (64,15,N'PurchaseOrder.Consolidate',N'Tổng hợp PO',N'Consolidate',N'Tổng hợp PO',1,'2026-01-01'),
                (139,15,N'PurchaseOrder.Submit',N'Gửi đơn đặt hàng để duyệt',N'Submit',N'Chuyển đơn đặt hàng từ bản nháp sang trạng thái chờ duyệt',1,'2026-01-01'),
                (140,15,N'PurchaseOrder.Approve',N'Duyệt đơn đặt hàng',N'Approve',N'Duyệt cam kết đặt hàng với nhà cung cấp',1,'2026-01-01'),
                (141,15,N'PurchaseOrder.RejectApproval',N'Từ chối duyệt đơn đặt hàng',N'RejectApproval',N'Từ chối đơn đặt hàng đang chờ duyệt',1,'2026-01-01'),
                (142,15,N'PurchaseOrder.OverrideAllocation',N'Duyệt vượt phân bổ',N'OverrideAllocation',N'Cho phép đơn đặt hàng vượt số lượng đã được phân bổ khi có lý do',1,'2026-01-01'),
                (143,15,N'PurchaseOrder.Export',N'Xuất đơn đặt hàng',N'Export',N'Xuất tài liệu đơn đặt hàng để gửi hoặc lưu chứng từ',1,'2026-01-01'),
                (65,16,N'Receipt.View',N'Xem phiếu nhận hàng',N'View',N'Xem phiếu nhận hàng',1,'2026-01-01'),
                (66,16,N'Receipt.Create',N'Tạo phiếu nhận hàng',N'Create',N'Tạo phiếu nhận hàng',1,'2026-01-01'),
                (67,16,N'Receipt.Confirm',N'Xác nhận nhận hàng',N'Confirm',N'Xác nhận nhận hàng',1,'2026-01-01'),
                (68,16,N'Receipt.Reject',N'Ghi nhận hàng bị từ chối',N'Reject',N'Ghi nhận hàng bị từ chối',1,'2026-01-01'),
                (69,16,N'Receipt.Cancel',N'Hủy phiếu nhận hàng',N'Cancel',N'Hủy phiếu nhận hàng',1,'2026-01-01'),
                (144,16,N'Receipt.UpdateDraft',N'Cập nhật phiếu nhận bản nháp',N'UpdateDraft',N'Cập nhật phiếu nhận trước khi xác nhận nhập kho',1,'2026-01-01'),
                (145,16,N'Receipt.RecordSupplierIssue',N'Ghi nhận sự cố nhà cung cấp',N'RecordSupplierIssue',N'Ghi nhận sự cố hoặc lý do liên quan đến hàng giao từ nhà cung cấp',1,'2026-01-01'),
                (146,16,N'Receipt.ViewCost',N'Xem giá vốn phiếu nhận',N'ViewCost',N'Xem giá vốn và giá trị của phiếu nhận hàng',1,'2026-01-01'),
                (70,17,N'Supplier.View',N'Xem nhà cung cấp',N'View',N'Xem nhà cung cấp',1,'2026-01-01'),
                (71,17,N'Supplier.Create',N'Tạo nhà cung cấp',N'Create',N'Tạo nhà cung cấp',1,'2026-01-01'),
                (72,17,N'Supplier.Update',N'Cập nhật nhà cung cấp',N'Update',N'Cập nhật nhà cung cấp',1,'2026-01-01'),
                (73,17,N'Supplier.ToggleStatus',N'Đổi trạng thái nhà cung cấp',N'ToggleStatus',N'Đổi trạng thái nhà cung cấp',1,'2026-01-01'),
                (74,17,N'Supplier.ViewQuality',N'Xem chất lượng nhà cung cấp',N'ViewQuality',N'Xem chất lượng nhà cung cấp',1,'2026-01-01'),
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
                (88,19,N'InventoryTransfer.Export',N'Xuất dữ liệu chuyển kho',N'Export',N'Xuất dữ liệu chuyển kho',1,'2026-01-01'),
                (126,11,N'InventoryThreshold.View',N'Xem ngưỡng tồn',N'ThresholdView',N'Xem ngưỡng tồn',1,'2026-01-01'),
                (127,11,N'InventoryThreshold.Update',N'Cập nhật ngưỡng tồn',N'ThresholdUpdate',N'Cập nhật ngưỡng tồn',1,'2026-01-01'),
                (128,12,N'Notification.View',N'Xem thông báo kho',N'NotificationView',N'Xem thông báo kho',1,'2026-01-01'),
                (129,26,N'ReorderSuggestion.View',N'Xem gợi ý nhập hàng',N'View',N'Xem danh sách gợi ý nhập hàng trong phạm vi cửa hàng được phép truy cập',1,'2026-01-01'),
                (130,17,N'SupplierQuality.View',N'Xem báo cáo chất lượng NCC',N'SupplierQualityView',N'Xem báo cáo chất lượng NCC',1,'2026-01-01');

                IF EXISTS
                (
                    SELECT 1
                    FROM @PermissionCatalog x
                    JOIN dbo.Permissions p
                      ON p.PermissionId = x.PermissionId
                      OR p.Code = x.Code
                      OR (p.PermissionGroupId = x.PermissionGroupId AND p.Action = x.Action)
                    WHERE p.PermissionId <> x.PermissionId
                       OR p.PermissionGroupId <> x.PermissionGroupId
                       OR p.Code <> x.Code
                       OR p.Name <> x.Name
                       OR p.Action <> x.Action
                       OR ISNULL(p.Description,N'') <> x.Description
                       OR p.Active <> x.Active
                       OR p.CreatedAt <> x.CreatedAt
                )
                    THROW 53375, N'Không thể đồng bộ phân quyền: catalog quyền Kho & Cung ứng xung đột contract.', 1;

                SET IDENTITY_INSERT dbo.Permissions ON;
                INSERT dbo.Permissions(PermissionId,PermissionGroupId,Code,Name,Action,Description,Active,CreatedAt)
                SELECT x.PermissionId,x.PermissionGroupId,x.Code,x.Name,x.Action,x.Description,x.Active,x.CreatedAt
                FROM @PermissionCatalog x
                WHERE NOT EXISTS
                (
                    SELECT 1
                    FROM dbo.Permissions p
                    WHERE p.PermissionId = x.PermissionId
                );
                SET IDENTITY_INSERT dbo.Permissions OFF;

                DECLARE @AdditionalPermissionCatalog table
                (
                    Code nvarchar(100) PRIMARY KEY,
                    GroupCode nvarchar(50) NOT NULL,
                    Name nvarchar(200) NOT NULL,
                    Action nvarchar(50) NOT NULL,
                    Description nvarchar(500) NOT NULL
                );
                INSERT @AdditionalPermissionCatalog VALUES
                (N'PurchaseAdvice.Update',N'PURCHASE_ADVICE',N'Cập nhật đề nghị mua',N'Update',N'Cập nhật đề nghị mua ở trạng thái cho phép'),
                (N'PurchaseAdvice.Cancel',N'PURCHASE_ADVICE',N'Hủy đề nghị mua',N'Cancel',N'Hủy đề nghị mua trước khi bị khóa nghiệp vụ'),
                (N'PurchaseOrder.CloseRemaining',N'PURCHASE_ORDER',N'Đóng phần còn lại PO',N'CloseRemaining',N'Đóng số lượng còn lại của dòng PO'),
                (N'SupplierQuality.Create',N'SUPPLIER',N'Ghi nhận chất lượng nhà cung cấp',N'SupplierQualityCreate',N'Ghi nhận sự cố hoặc chất lượng nhà cung cấp'),
                (N'SupplierQuality.Transition',N'SUPPLIER',N'Chuyển trạng thái sự cố nhà cung cấp',N'SupplierQualityTransition',N'Xác minh hoặc đóng sự cố nhà cung cấp'),
                (N'InventoryTransfer.RequestReturn',N'INVENTORY_TRANSFER',N'Yêu cầu trả hàng điều chuyển',N'RequestReturn',N'Yêu cầu trả hàng trong luồng điều chuyển'),
                (N'InventoryTransfer.ConfirmReturn',N'INVENTORY_TRANSFER',N'Xác nhận trả hàng điều chuyển',N'ConfirmReturn',N'Xác nhận trả hàng trong luồng điều chuyển'),
                (N'InventoryTransfer.ResolveDiscrepancy',N'INVENTORY_TRANSFER',N'Xử lý chênh lệch điều chuyển',N'ResolveDiscrepancy',N'Xử lý thiếu hụt hoặc chênh lệch điều chuyển cuối');

                IF EXISTS
                (
                    SELECT 1
                    FROM @AdditionalPermissionCatalog x
                    LEFT JOIN dbo.PermissionGroups g ON g.Code = x.GroupCode
                    WHERE g.PermissionGroupId IS NULL
                )
                    THROW 53376, N'Không thể đồng bộ phân quyền: thiếu nhóm quyền cho catalog mở rộng.', 1;

                IF EXISTS
                (
                    SELECT 1
                    FROM @AdditionalPermissionCatalog x
                    JOIN dbo.PermissionGroups g ON g.Code = x.GroupCode
                    JOIN dbo.Permissions p
                      ON p.Code = x.Code
                      OR (p.PermissionGroupId = g.PermissionGroupId AND p.Action = x.Action)
                    WHERE p.Code <> x.Code
                       OR p.PermissionGroupId <> g.PermissionGroupId
                       OR p.Name <> x.Name
                       OR p.Action <> x.Action
                       OR ISNULL(p.Description,N'') <> x.Description
                       OR p.Active <> 1
                )
                    THROW 53377, N'Không thể đồng bộ phân quyền: catalog quyền mở rộng xung đột contract.', 1;

                INSERT dbo.Permissions(PermissionGroupId,Code,Name,Action,Description,Active,CreatedAt)
                SELECT g.PermissionGroupId,x.Code,x.Name,x.Action,x.Description,1,SYSUTCDATETIME()
                FROM @AdditionalPermissionCatalog x
                JOIN dbo.PermissionGroups g ON g.Code = x.GroupCode
                WHERE NOT EXISTS
                (
                    SELECT 1
                    FROM dbo.Permissions p
                    WHERE p.Code = x.Code
                );

                DECLARE @RoleMap table(RoleKey nvarchar(10) PRIMARY KEY, RoleName nvarchar(100) UNIQUE);
                INSERT @RoleMap VALUES
                (N'CDN',N'Chủ doanh nghiệp'),
                (N'QLV',N'Quản lý vùng'),
                (N'QLCN',N'Quản lý chi nhánh'),
                (N'NVBH',N'Nhân viên bán hàng'),
                (N'KTK',N'Kế toán/kho'),
                (N'CT',N'Ca trưởng');

                DECLARE @Matrix table
                (
                    PermissionCode nvarchar(100) PRIMARY KEY,
                    CDN bit NOT NULL,
                    QLV bit NOT NULL,
                    QLCN bit NOT NULL,
                    NVBH bit NOT NULL,
                    KTK bit NOT NULL,
                    CT bit NOT NULL
                );
                INSERT @Matrix VALUES
                (N'Ingredient.View',1,1,1,0,1,0),
                (N'Ingredient.Create',1,0,0,0,1,0),
                (N'Ingredient.Update',1,0,0,0,1,0),
                (N'Ingredient.ToggleStatus',1,0,0,0,1,0),
                (N'UnitConversion.View',1,1,1,0,1,0),
                (N'UnitConversion.Create',1,0,0,0,1,0),
                (N'UnitConversion.Update',1,0,0,0,1,0),
                (N'UnitConversion.ToggleStatus',1,0,0,0,1,0),
                (N'Inventory.View',1,1,1,0,1,0),
                (N'Inventory.Adjust',0,0,0,0,1,0),
                (N'Inventory.Export',1,1,1,0,1,0),
                (N'InventoryThreshold.View',1,1,1,0,1,0),
                (N'InventoryThreshold.Update',1,1,1,0,0,0),
                (N'StockAlert.View',1,1,1,1,1,1),
                (N'StockAlert.Resolve',1,0,1,0,0,0),
                (N'StockAlert.Configure',1,1,1,0,0,0),
                (N'StockAlert.Export',1,1,1,0,1,0),
                (N'Notification.View',1,1,1,1,1,1),
                (N'StockAlert.Create',0,0,1,1,0,1),
                (N'StockAlert.CreateRestockRequest',0,0,1,0,0,0),
                (N'Restock.View',1,1,1,0,1,1),
                (N'Restock.Create',1,0,1,0,1,0),
                (N'Restock.Submit',0,0,1,0,1,0),
                (N'Restock.Approve',1,0,0,0,1,0),
                (N'Restock.Reject',1,0,0,0,1,0),
                (N'Restock.Cancel',1,0,1,0,1,0),
                (N'ReorderSuggestion.View',1,1,1,0,1,0),
                (N'Restock.Update',0,0,1,0,1,0),
                (N'Restock.CloseRemaining',1,0,0,0,1,0),
                (N'Restock.CreatePurchaseOrder',1,0,0,0,1,0),
                (N'Restock.CreateTransfer',1,0,0,0,1,0),
                (N'PurchaseAdvice.View',1,1,1,0,1,0),
                (N'PurchaseAdvice.Create',1,0,1,0,1,0),
                (N'PurchaseAdvice.Submit',1,0,1,0,1,0),
                (N'PurchaseAdvice.Review',1,0,0,0,1,0),
                (N'PurchaseAdvice.Approve',1,0,0,0,1,0),
                (N'PurchaseAdvice.Reject',1,0,0,0,1,0),
                (N'PurchaseAdvice.Consolidate',1,0,0,0,1,0),
                (N'PurchaseAdvice.SelectSupplier',1,0,0,0,1,0),
                (N'PurchaseAdvice.CreatePurchaseOrder',1,0,0,0,1,0),
                (N'PurchaseAdvice.Update',1,0,1,0,1,0),
                (N'PurchaseAdvice.Cancel',1,0,1,0,1,0),
                (N'PurchaseOrder.View',1,1,1,0,1,1),
                (N'PurchaseOrder.Create',1,0,0,0,1,0),
                (N'PurchaseOrder.Update',1,0,0,0,1,0),
                (N'PurchaseOrder.Send',1,0,0,0,1,0),
                (N'PurchaseOrder.Receive',0,0,1,0,0,1),
                (N'PurchaseOrder.Cancel',1,0,0,0,1,0),
                (N'PurchaseOrder.ViewBatch',1,1,0,0,1,0),
                (N'PurchaseOrder.CreateBatch',1,0,0,0,1,0),
                (N'PurchaseOrder.Consolidate',1,0,0,0,1,0),
                (N'PurchaseOrder.Submit',1,0,0,0,1,0),
                (N'PurchaseOrder.Approve',1,0,0,0,0,0),
                (N'PurchaseOrder.RejectApproval',1,0,0,0,0,0),
                (N'PurchaseOrder.OverrideAllocation',1,0,0,0,0,0),
                (N'PurchaseOrder.Export',1,0,0,0,1,0),
                (N'PurchaseOrder.CloseRemaining',1,0,0,0,1,0),
                (N'Receipt.View',1,1,1,0,1,1),
                (N'Receipt.Create',0,0,1,0,0,1),
                (N'Receipt.Confirm',0,0,1,0,0,1),
                (N'Receipt.Reject',0,0,1,0,0,1),
                (N'Receipt.Cancel',1,0,1,0,1,1),
                (N'Receipt.UpdateDraft',0,0,1,0,0,1),
                (N'Receipt.RecordSupplierIssue',0,0,1,0,0,1),
                (N'Receipt.ViewCost',1,1,0,0,1,0),
                (N'Supplier.View',1,1,1,0,1,0),
                (N'Supplier.Create',1,0,0,0,1,0),
                (N'Supplier.Update',1,0,0,0,1,0),
                (N'Supplier.ToggleStatus',1,0,0,0,1,0),
                (N'Supplier.ViewQuality',1,1,1,0,1,0),
                (N'SupplierQuality.View',1,1,1,0,1,0),
                (N'SupplierQuality.Create',0,0,1,0,1,1),
                (N'SupplierQuality.Transition',1,0,0,0,1,0),
                (N'InventoryDocument.View',1,1,1,0,1,0),
                (N'InventoryDocument.CreateDraft',0,0,1,0,1,0),
                (N'InventoryDocument.Submit',0,0,1,0,1,0),
                (N'InventoryDocument.Confirm',0,0,0,0,1,0),
                (N'InventoryDocument.ApproveNegative',1,0,0,0,1,0),
                (N'InventoryDocument.Cancel',1,0,1,0,1,0),
                (N'InventoryDocument.Export',1,1,1,0,1,0),
                (N'InventoryTransfer.View',1,1,1,0,1,1),
                (N'InventoryTransfer.CreateDraft',1,0,0,0,1,0),
                (N'InventoryTransfer.UpdateDraft',1,0,0,0,1,0),
                (N'InventoryTransfer.Dispatch',0,0,1,0,1,1),
                (N'InventoryTransfer.Receive',0,0,1,0,1,1),
                (N'InventoryTransfer.Cancel',1,0,0,0,1,0),
                (N'InventoryTransfer.Export',1,1,1,0,1,0),
                (N'InventoryTransfer.RequestReturn',0,0,1,0,1,1),
                (N'InventoryTransfer.ConfirmReturn',0,0,1,0,1,1),
                (N'InventoryTransfer.ResolveDiscrepancy',1,0,0,0,0,0);

                IF EXISTS
                (
                    SELECT 1
                    FROM @RoleMap rm
                    LEFT JOIN dbo.Roles r ON r.Name = rm.RoleName
                    WHERE r.RoleId IS NULL
                )
                    THROW 53371, N'Không thể đồng bộ phân quyền: thiếu vai trò nghiệp vụ bắt buộc.', 1;

                IF EXISTS
                (
                    SELECT 1
                    FROM @Matrix m
                    LEFT JOIN dbo.Permissions p ON p.Code = m.PermissionCode AND p.Active = 1
                    WHERE p.PermissionId IS NULL
                )
                    THROW 53372, N'Không thể đồng bộ phân quyền: thiếu quyền Kho & Cung ứng đang hoạt động.', 1;

                DECLARE @Expected table(RoleName nvarchar(100), PermissionCode nvarchar(100), PRIMARY KEY(RoleName,PermissionCode));
                INSERT @Expected(RoleName,PermissionCode)
                SELECT rm.RoleName,m.PermissionCode
                FROM @Matrix m
                CROSS APPLY
                (
                    VALUES
                    (N'CDN',m.CDN),
                    (N'QLV',m.QLV),
                    (N'QLCN',m.QLCN),
                    (N'NVBH',m.NVBH),
                    (N'KTK',m.KTK),
                    (N'CT',m.CT)
                ) grantMatrix(RoleKey,IsGranted)
                JOIN @RoleMap rm ON rm.RoleKey = grantMatrix.RoleKey
                WHERE grantMatrix.IsGranted = 1;

                DELETE rp
                FROM dbo.RolePermissions rp
                JOIN dbo.Roles r ON r.RoleId = rp.RoleId
                JOIN @RoleMap rm ON rm.RoleName = r.Name
                JOIN dbo.Permissions p ON p.PermissionId = rp.PermissionId
                JOIN @Matrix m ON m.PermissionCode = p.Code;

                INSERT dbo.RolePermissions(RoleId,PermissionId)
                SELECT r.RoleId,p.PermissionId
                FROM @Expected e
                JOIN dbo.Roles r ON r.Name = e.RoleName
                JOIN dbo.Permissions p ON p.Code = e.PermissionCode
                WHERE NOT EXISTS
                (
                    SELECT 1
                    FROM dbo.RolePermissions rp
                    WHERE rp.RoleId = r.RoleId AND rp.PermissionId = p.PermissionId
                );

                INSERT dbo.RolePermissions(RoleId,PermissionId)
                SELECT r.RoleId,p.PermissionId
                FROM dbo.Roles r
                CROSS JOIN dbo.Permissions p
                JOIN @Matrix m ON m.PermissionCode = p.Code
                WHERE r.Name = N'Quản trị hệ thống'
                  AND p.Active = 1
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM dbo.RolePermissions rp
                      WHERE rp.RoleId = r.RoleId AND rp.PermissionId = p.PermissionId
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                THROW 53373, N'Không thể tự động hoàn tác ma trận quyền Kho & Cung ứng mà không làm mất dữ liệu phân quyền lịch sử.', 1;
                """);
        }
    }
}
