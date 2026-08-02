using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class AlignSupplyChainOperationalIcePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                SET XACT_ABORT ON;

                IF EXISTS
                (
                    SELECT 1
                    FROM dbo.PermissionGroups
                    WHERE (PermissionGroupId = 22 OR Code = N'OPERATIONAL_ICE')
                      AND (PermissionGroupId <> 22
                           OR Code <> N'OPERATIONAL_ICE'
                           OR Name <> N'Quản lý đá vận hành')
                )
                    THROW 53361, N'Nhóm quyền OPERATIONAL_ICE xung đột mã định danh hoặc tên.', 1;

                IF NOT EXISTS (SELECT 1 FROM dbo.PermissionGroups WHERE Code = N'OPERATIONAL_ICE')
                BEGIN
                    SET IDENTITY_INSERT dbo.PermissionGroups ON;
                    INSERT dbo.PermissionGroups(PermissionGroupId,Code,Name,DisplayOrder,Active)
                    VALUES(22,N'OPERATIONAL_ICE',N'Quản lý đá vận hành',23,1);
                    SET IDENTITY_INSERT dbo.PermissionGroups OFF;
                END;

                DECLARE @GroupId int =
                (
                    SELECT PermissionGroupId
                    FROM dbo.PermissionGroups
                    WHERE Code = N'OPERATIONAL_ICE'
                );

                DECLARE @LegacyFoundation table
                (
                    PermissionId int PRIMARY KEY,
                    Code nvarchar(100) UNIQUE,
                    Name nvarchar(200),
                    Action nvarchar(50),
                    Description nvarchar(500),
                    Active bit,
                    CreatedAt datetime2
                );
                INSERT @LegacyFoundation VALUES
                (147,N'OperationalIce.View',N'Xem quản lý đá vận hành',N'View',N'Xem ca vận hành, phân bổ và đối soát đá',1,'2026-07-29'),
                (148,N'OperationalIce.Manage',N'Vận hành phân bổ đá',N'Manage',N'Tạo ca, mở phân bổ, cấp bổ sung và bàn giao đá',0,'2026-07-29'),
                (149,N'OperationalIce.Approve',N'Duyệt đối soát đá',N'Approve',N'Duyệt cấp bổ sung và chênh lệch đá cuối ca',0,'2026-07-29'),
                (150,N'OperationalIce.Policy',N'Cấu hình chính sách đá',N'Policy',N'Cấu hình định mức và ngưỡng đối soát đá theo cửa hàng',0,'2026-07-29');

                IF EXISTS
                (
                    SELECT 1
                    FROM @LegacyFoundation x
                    JOIN dbo.Permissions p
                      ON p.PermissionId = x.PermissionId
                      OR p.Code = x.Code
                      OR (p.PermissionGroupId = @GroupId AND p.Action = x.Action)
                    WHERE p.PermissionId <> x.PermissionId
                       OR p.Code <> x.Code
                       OR p.PermissionGroupId <> @GroupId
                       OR p.Action <> x.Action
                )
                    THROW 53362, N'Quyền Operational Ice lịch sử xung đột ID, Code hoặc Group/Action.', 1;

                SET IDENTITY_INSERT dbo.Permissions ON;
                INSERT dbo.Permissions(PermissionId,PermissionGroupId,Code,Name,Action,Description,Active,CreatedAt)
                SELECT x.PermissionId,@GroupId,x.Code,x.Name,x.Action,x.Description,x.Active,x.CreatedAt
                FROM @LegacyFoundation x
                WHERE NOT EXISTS (SELECT 1 FROM dbo.Permissions p WHERE p.PermissionId = x.PermissionId);
                SET IDENTITY_INSERT dbo.Permissions OFF;

                UPDATE p
                SET Name = x.Name,
                    Description = x.Description,
                    Active = x.Active
                FROM dbo.Permissions p
                JOIN @LegacyFoundation x ON x.PermissionId = p.PermissionId;

                DECLARE @Catalog table
                (
                    Code nvarchar(100) PRIMARY KEY,
                    Name nvarchar(200),
                    Action nvarchar(50),
                    Description nvarchar(500)
                );
                INSERT @Catalog VALUES
                (N'OperationalIce.View',N'Xem quản lý đá vận hành',N'View',N'Xem ca vận hành, phân bổ và đối soát đá'),
                (N'OperationalIce.ConfigurePolicy',N'Cấu hình chính sách đá',N'ConfigurePolicy',N'Cấu hình định mức và ngưỡng đối soát đá trong phạm vi cửa hàng'),
                (N'OperationalIce.CreateShift',N'Tạo ca vận hành đá',N'CreateShift',N'Tạo và cập nhật kế hoạch ca vận hành đá trong phạm vi cửa hàng'),
                (N'OperationalIce.OpenShift',N'Mở ca vận hành đá',N'OpenShift',N'Xác nhận cấp đầu ca và mở phân bổ đá'),
                (N'OperationalIce.LinkWorkShift',N'Liên kết WorkShift POS',N'LinkWorkShift',N'Liên kết WorkShift POS hợp lệ vào ca vận hành đá'),
                (N'OperationalIce.RequestSupplement',N'Yêu cầu cấp bổ sung đá',N'RequestSupplement',N'Gửi yêu cầu cấp bổ sung cho ca vận hành đá được phân công'),
                (N'OperationalIce.ApproveSupplement',N'Duyệt cấp bổ sung đá',N'ApproveSupplement',N'Duyệt hoặc từ chối yêu cầu cấp bổ sung đá'),
                (N'OperationalIce.Handoff',N'Bàn giao đá giữa ca',N'Handoff',N'Xác nhận bàn giao đá giữa các ca cùng ngày'),
                (N'OperationalIce.SubmitClose',N'Gửi chốt ca đá',N'SubmitClose',N'Gửi số liệu chốt ca vận hành đá'),
                (N'OperationalIce.ApproveVariance',N'Duyệt chênh lệch đá',N'ApproveVariance',N'Duyệt hao hụt hoặc hoàn tất đối soát chênh lệch đá'),
                (N'OperationalIce.CancelScheduledShift',N'Hủy ca đá chưa mở',N'CancelScheduledShift',N'Hủy ca vận hành đá còn ở trạng thái kế hoạch'),
                (N'OperationalIce.ViewReport',N'Xem báo cáo ca đá',N'ViewReport',N'Xem và tải báo cáo vận hành đá trong phạm vi được cấp');

                UPDATE p
                SET PermissionGroupId = @GroupId,
                    Name = c.Name,
                    Action = c.Action,
                    Description = c.Description,
                    Active = 1
                FROM dbo.Permissions p
                JOIN @Catalog c ON c.Code = p.Code;

                INSERT dbo.Permissions(PermissionGroupId,Code,Name,Action,Description,Active,CreatedAt)
                SELECT @GroupId,c.Code,c.Name,c.Action,c.Description,1,SYSUTCDATETIME()
                FROM @Catalog c
                WHERE NOT EXISTS (SELECT 1 FROM dbo.Permissions p WHERE p.Code = c.Code);

                UPDATE dbo.Permissions
                SET Active = 0
                WHERE Code IN
                (
                    N'OperationalIce.Manage',
                    N'OperationalIce.Approve',
                    N'OperationalIce.Policy'
                );

                DECLARE @Expected table(RoleName nvarchar(100), PermissionCode nvarchar(100));
                INSERT @Expected VALUES
                (N'Chủ doanh nghiệp',N'OperationalIce.View'),
                (N'Chủ doanh nghiệp',N'OperationalIce.ConfigurePolicy'),
                (N'Chủ doanh nghiệp',N'OperationalIce.CreateShift'),
                (N'Chủ doanh nghiệp',N'OperationalIce.OpenShift'),
                (N'Chủ doanh nghiệp',N'OperationalIce.LinkWorkShift'),
                (N'Chủ doanh nghiệp',N'OperationalIce.RequestSupplement'),
                (N'Chủ doanh nghiệp',N'OperationalIce.ApproveSupplement'),
                (N'Chủ doanh nghiệp',N'OperationalIce.Handoff'),
                (N'Chủ doanh nghiệp',N'OperationalIce.SubmitClose'),
                (N'Chủ doanh nghiệp',N'OperationalIce.ApproveVariance'),
                (N'Chủ doanh nghiệp',N'OperationalIce.CancelScheduledShift'),
                (N'Chủ doanh nghiệp',N'OperationalIce.ViewReport'),
                (N'Quản lý vùng',N'OperationalIce.View'),
                (N'Quản lý vùng',N'OperationalIce.ViewReport'),
                (N'Quản lý chi nhánh',N'OperationalIce.View'),
                (N'Quản lý chi nhánh',N'OperationalIce.ConfigurePolicy'),
                (N'Quản lý chi nhánh',N'OperationalIce.CreateShift'),
                (N'Quản lý chi nhánh',N'OperationalIce.OpenShift'),
                (N'Quản lý chi nhánh',N'OperationalIce.LinkWorkShift'),
                (N'Quản lý chi nhánh',N'OperationalIce.RequestSupplement'),
                (N'Quản lý chi nhánh',N'OperationalIce.ApproveSupplement'),
                (N'Quản lý chi nhánh',N'OperationalIce.Handoff'),
                (N'Quản lý chi nhánh',N'OperationalIce.SubmitClose'),
                (N'Quản lý chi nhánh',N'OperationalIce.ApproveVariance'),
                (N'Quản lý chi nhánh',N'OperationalIce.CancelScheduledShift'),
                (N'Quản lý chi nhánh',N'OperationalIce.ViewReport'),
                (N'Kế toán/kho',N'OperationalIce.View'),
                (N'Kế toán/kho',N'OperationalIce.ViewReport'),
                (N'Ca trưởng',N'OperationalIce.View'),
                (N'Ca trưởng',N'OperationalIce.RequestSupplement'),
                (N'Ca trưởng',N'OperationalIce.Handoff'),
                (N'Ca trưởng',N'OperationalIce.SubmitClose'),
                (N'Ca trưởng',N'OperationalIce.ViewReport'),
                (N'Quản trị hệ thống',N'OperationalIce.View'),
                (N'Quản trị hệ thống',N'OperationalIce.ConfigurePolicy'),
                (N'Quản trị hệ thống',N'OperationalIce.CreateShift'),
                (N'Quản trị hệ thống',N'OperationalIce.OpenShift'),
                (N'Quản trị hệ thống',N'OperationalIce.LinkWorkShift'),
                (N'Quản trị hệ thống',N'OperationalIce.RequestSupplement'),
                (N'Quản trị hệ thống',N'OperationalIce.ApproveSupplement'),
                (N'Quản trị hệ thống',N'OperationalIce.Handoff'),
                (N'Quản trị hệ thống',N'OperationalIce.SubmitClose'),
                (N'Quản trị hệ thống',N'OperationalIce.ApproveVariance'),
                (N'Quản trị hệ thống',N'OperationalIce.CancelScheduledShift'),
                (N'Quản trị hệ thống',N'OperationalIce.ViewReport');

                DELETE rp
                FROM dbo.RolePermissions rp
                JOIN dbo.Permissions p ON p.PermissionId = rp.PermissionId
                WHERE p.Code LIKE N'OperationalIce.%';

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
                WHERE r.Name = N'Ca trưởng'
                  AND p.Code = N'Restock.View'
                  AND NOT EXISTS
                  (
                      SELECT 1 FROM dbo.RolePermissions rp
                      WHERE rp.RoleId = r.RoleId AND rp.PermissionId = p.PermissionId
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                SET XACT_ABORT ON;

                DECLARE @NewCodes table(Code nvarchar(100) PRIMARY KEY);
                INSERT @NewCodes VALUES
                (N'OperationalIce.ConfigurePolicy'),
                (N'OperationalIce.CreateShift'),
                (N'OperationalIce.OpenShift'),
                (N'OperationalIce.LinkWorkShift'),
                (N'OperationalIce.RequestSupplement'),
                (N'OperationalIce.ApproveSupplement'),
                (N'OperationalIce.Handoff'),
                (N'OperationalIce.SubmitClose'),
                (N'OperationalIce.ApproveVariance'),
                (N'OperationalIce.CancelScheduledShift'),
                (N'OperationalIce.ViewReport');

                DELETE rp
                FROM dbo.RolePermissions rp
                JOIN dbo.Permissions p ON p.PermissionId = rp.PermissionId
                JOIN @NewCodes c ON c.Code = p.Code;

                UPDATE p
                SET Active = 0
                FROM dbo.Permissions p
                JOIN @NewCodes c ON c.Code = p.Code;

                UPDATE dbo.Permissions
                SET Active = 1
                WHERE Code IN
                (
                    N'OperationalIce.Manage',
                    N'OperationalIce.Approve',
                    N'OperationalIce.Policy'
                );

                DELETE rp
                FROM dbo.RolePermissions rp
                JOIN dbo.Permissions p ON p.PermissionId = rp.PermissionId
                WHERE p.Code IN
                (
                    N'OperationalIce.Manage',
                    N'OperationalIce.Approve',
                    N'OperationalIce.Policy'
                );

                DECLARE @Legacy table(RoleName nvarchar(100), PermissionCode nvarchar(100));
                INSERT @Legacy VALUES
                (N'Quản lý chi nhánh',N'OperationalIce.Manage'),
                (N'Ca trưởng',N'OperationalIce.Manage'),
                (N'Quản trị hệ thống',N'OperationalIce.Manage'),
                (N'Chủ doanh nghiệp',N'OperationalIce.Approve'),
                (N'Quản lý chi nhánh',N'OperationalIce.Approve'),
                (N'Quản trị hệ thống',N'OperationalIce.Approve'),
                (N'Chủ doanh nghiệp',N'OperationalIce.Policy'),
                (N'Quản lý chi nhánh',N'OperationalIce.Policy'),
                (N'Quản trị hệ thống',N'OperationalIce.Policy');

                INSERT dbo.RolePermissions(RoleId,PermissionId)
                SELECT r.RoleId,p.PermissionId
                FROM @Legacy e
                JOIN dbo.Roles r ON r.Name = e.RoleName
                JOIN dbo.Permissions p ON p.Code = e.PermissionCode
                WHERE NOT EXISTS
                (
                    SELECT 1 FROM dbo.RolePermissions rp
                    WHERE rp.RoleId = r.RoleId AND rp.PermissionId = p.PermissionId
                );

                DELETE rp
                FROM dbo.RolePermissions rp
                JOIN dbo.Roles r ON r.RoleId = rp.RoleId AND r.Name = N'Ca trưởng'
                JOIN dbo.Permissions p ON p.PermissionId = rp.PermissionId AND p.Code = N'Restock.View';
                """);
        }
    }
}
