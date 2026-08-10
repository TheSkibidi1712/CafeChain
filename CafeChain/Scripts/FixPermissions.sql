-- =====================================================================
-- FixPermissions.sql
-- Chèn các quyền POS và Dashboard còn thiếu rồi cấp cho role CDN/QTHT
-- Chạy: sqlcmd -S "(localdb)\MSSQLLocalDB" -d CafeChain -i "Scripts\FixPermissions.sql" -f 65001
-- =====================================================================
SET NOCOUNT ON;
BEGIN TRANSACTION;

-- 1. Đảm bảo PermissionGroup POS_WORKSHIFT tồn tại
IF NOT EXISTS (SELECT 1 FROM dbo.PermissionGroups WHERE Code = N'POS_WORKSHIFT')
    INSERT INTO dbo.PermissionGroups (Code, Name, DisplayOrder, Active)
    VALUES (N'POS_WORKSHIFT', N'Phiên POS và trách nhiệm két', 28, 1);

DECLARE @PosGroupId int = (SELECT PermissionGroupId FROM dbo.PermissionGroups WHERE Code = N'POS_WORKSHIFT');

-- 2. Chèn các quyền POS còn thiếu
INSERT INTO dbo.Permissions (PermissionGroupId, Code, Name, Action, Description, Active, CreatedAt)
SELECT @PosGroupId, x.Code, x.Name, x.Action, x.Description, 1, GETDATE()
FROM (VALUES
    (N'POS.WorkShift.View',                 N'Xem phiên POS',               N'View',                    N'Xem phiên POS trong phạm vi cửa hàng được cấp'),
    (N'POS.WorkShift.Open',                 N'Mở phiên POS',                N'Open',                    N'Mở phiên chịu trách nhiệm POS/két'),
    (N'POS.WorkShift.Close',                N'Đóng phiên POS',              N'Close',                   N'Kiểm đếm và đóng phiên POS/két'),
    (N'POS.WorkShift.OpenOutsideSchedule',  N'Mở POS ngoài lịch',           N'OpenOutsideSchedule',     N'Yêu cầu mở POS ngoài lịch'),
    (N'POS.WorkShift.ApproveOutsideSchedule', N'Duyệt mở POS ngoài lịch',  N'ApproveOutsideSchedule',  N'Phê duyệt mở POS ngoài lịch'),
    (N'POS.WorkShift.CloseException',       N'Đóng phiên POS ngoại lệ',     N'CloseException',          N'Đóng ngoại lệ phiên POS'),
    (N'POS.WorkShift.Reconcile',            N'Đối soát lại phiên POS',       N'Reconcile',               N'Đối soát payment hoặc đơn offline'),
    (N'POS.WorkShift.OverrideTerminal',     N'Đăng ký terminal POS',         N'OverrideTerminal',        N'Phê duyệt đăng ký terminal POS'),
    (N'POS.WorkShift.ApproveLateOpen',      N'Duyệt mở ca trễ',             N'ApproveLateOpen',         N'Duyệt yêu cầu mở ca trễ trên 30 phút'),
    (N'POS.Session.Manage',                 N'Quản lý phiên POS',           N'ManagePosSession',        N'Kết thúc hoặc thu hồi POS access session'),
    (N'POS.Operator.Switch',                N'Đổi người thao tác POS',      N'SwitchOperator',          N'Cho phép chuyển Current Operator POS')
) AS x(Code, Name, Action, Description)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Permissions p WHERE p.Code = x.Code);

-- 3. Cấp tất cả quyền POS và Dashboard cho Role 1 (Chủ doanh nghiệp) và Role 6 (Quản trị hệ thống)
INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId
FROM dbo.Roles r
CROSS JOIN dbo.Permissions p
WHERE r.RoleId IN (1, 6)
  AND p.Active = 1
  AND (
      p.Code LIKE 'POS.%'
      OR p.Code LIKE 'Dashboard%'
      OR p.Code = 'App.AdminDashboard'
  )
  AND NOT EXISTS (
      SELECT 1 FROM dbo.RolePermissions rp
      WHERE rp.RoleId = r.RoleId AND rp.PermissionId = p.PermissionId
  );

-- 4. Kiểm tra kết quả
SELECT p.Code, p.Active
FROM dbo.RolePermissions rp
JOIN dbo.Permissions p ON p.PermissionId = rp.PermissionId
JOIN dbo.AccountRoles ar ON ar.RoleId = rp.RoleId
WHERE ar.AccountId = 1
  AND p.Active = 1
  AND (p.Code LIKE 'POS%' OR p.Code LIKE 'Dashboard%' OR p.Code = 'App.AdminDashboard')
ORDER BY p.Code;

COMMIT TRANSACTION;
PRINT 'FixPermissions: Done.';
