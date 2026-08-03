# Báo cáo triển khai StaffHub và WorkShift POS

Cập nhật: 03/08/2026. Đây là báo cáo cuối của đợt refactor, không phải tài liệu checkpoint.

## Trạng thái mã nguồn

- Migration hiện hành gồm `20260802183312_InitialCreate` và migration tăng dần `20260803050636_AddProtectedOperationalOtpPayload`.
- `Scripts/SeedAll.sql` là nguồn seed duy nhất cho RBAC và fixture; migration không seed permission/role.
- Không sửa `FIX.md` và không tạo chức năng chấm công, lương hoặc sao chép tiền tự động.
- Backend và frontend đều build được tại thời điểm báo cáo.

## Hạng mục đã hoàn tất

- Mở rộng model/state machine WorkShift, UTC fields, BusinessDate, nguồn lịch, expiry, close metadata và rowversion.
- Constraint/index ngăn tiền âm, tiền VND thập phân và WorkShift active trùng terminal/nhân viên.
- Resolver lịch tuyệt đối hỗ trợ ca qua đêm và phân loại mở theo lịch/trễ/ngoài lịch.
- Lý do, OTP và thời hạn sáu giờ cho phiên ngoài lịch; không tạo StaffShift giả.
- Idempotency cho mở, đóng, đóng ngoại lệ, reconcile và đăng ký terminal.
- Worker cảnh báo 30/10/1 phút, auto-close phiên rỗng, pending-close cho phiên cần kiểm đếm.
- Backend guard order/offline/payment theo trạng thái và deadline WorkShift.
- `POSPaymentController` đã là controller mỏng; transaction, cash-return evidence, idempotency actor+store và cancellation token nằm trong `POSPaymentCancellationService`.
- Đóng hai bước, tính tiền phía server, đóng ngoại lệ và reconcile WorkShift cũ; reconcile bị chặn đến khi server xác nhận đủ toàn bộ đơn offline của phiên cũ.
- Permission/scope runtime; terminal enrollment cần approver có quyền OverrideTerminal.
- OTP bind action/store/terminal/WorkShift/request; OTP được hash và consume một lần.
- OTP dùng `TimeProvider`/timezone chung, giới hạn challenge theo nhân viên/terminal/IP/device bằng dữ liệu SQL dùng chung giữa các instance, không reset số lần sai khi resend, đồng thời revalidate permission/scope ở verify và resend. IP/device chỉ lưu SHA-256, không lưu dữ liệu thô.
- OTP đúng/sai được ghi `AuditLog` với bảng nguồn `OtpChallenges`; log/audit không chứa mã OTP.
- OTP vận hành dài đúng 6 ký tự, chỉ dùng `ABCDEFGHJKLMNPQRSTUVWXYZ23456789`; backend chuẩn hóa chữ thường, từ chối ký tự đặc biệt/khoảng trắng nội bộ/ký tự có dấu/emoji và không ánh xạ ký tự dễ nhầm.
- Mã OTP còn hiệu lực được lưu bằng ciphertext Data Protection trong `OtpChallenges`, chỉ đúng người duyệt xem lại được qua chuông; payload bị xóa khi challenge được duyệt, dùng, khóa, hủy hoặc hết hạn. Gửi lại bị khóa 60 giây ở cả backend và giao diện.
- Chuông Admin là trung tâm thông báo hợp nhất; mục “Thông báo kho” riêng đã được loại khỏi sidebar.
- Cập nhật login failure bằng SQL atomic update để tránh mất bộ đếm khi request đồng thời.
- SignalR group theo store permission/staff/terminal; publisher phát đến đủ ba audience, polling fallback và khóa phía frontend theo deadline server.
- Worker SQL Server lấy từng WorkShift bằng `UPDLOCK/READPAST/ROWLOCK` trong transaction để nhiều instance không xử lý cùng một deadline.
- StaffHub hiển thị khoảng lịch qua đêm đầy đủ và dùng one-time POS exchange code.
- POS có RequestKey, countdown, modal ngoài lịch/terminal/reconcile và bỏ nhãn chấm công.
- Audit WorkShift dùng `AuditLog`.
- Sửa SeedAll để chạy lại an toàn với schema `StartTimeUtc`, `ExpiryWarningLevel=0` và temp table cleanup.
- Đồng bộ các trang kho/mua hàng với shared warehouse shell và semantic design token; loại bỏ sáu lỗi full-suite nền.
- Khóa cập nhật POS catalog theo cửa hàng bằng SQL Server `sp_getapplock` trong transaction để nhiều instance không cùng ghi catalog version.

## Kiểm tra thực tế

| Kiểm tra | Kết quả |
|---|---|
| `dotnet build CafeChain/CafeChain.csproj --no-restore` | Pass, 0 error; 670 warning nền khi biên dịch lại toàn bộ |
| Frontend `npm run build` | Pass; cảnh báo chunk/annotation |
| Frontend `npm run lint` | Pass |
| Assessment + resolver + expiry worker | Pass 10/10 |
| Nhóm WorkShift/OTP/offline/schedule không-SQL | Pass 135/135 |
| Nhóm WorkShift/OTP/offline/schedule/payment không-SQL | Pass 148/148 |
| Rà soát cuối WorkShift/OTP/offline/schedule/payment không-SQL | Pass 138/138 theo filter cuối, ngày 03/08/2026 |
| Nhóm WorkShift/OTP/offline/schedule dùng SQL Server Express | Pass 13/13 |
| Operational Ice tương thích trạng thái | Pass 19/19 |
| SeedAll chạy hai lần trên SQL Server | Pass; số lượng bảng chính không đổi |
| Migration/model pending changes | Không có pending model change |
| Default/index/constraint WorkShift trên SQL | Pass |
| InitialCreate thực thi trên database SQL Server mới | Pass 1/1 qua migration contract test |
| Full suite không-SQL (có E2E procurement dùng SQL fixture) | Pass 1708/1708 |
| Full suite gồm toàn bộ SQL Server integration | Pass 1844/1844 |
| OTP generator/validation/ciphertext/đúng người nhận (lần chạy 03/08/2026) | Pass 16/16 |
| Toàn bộ test không có tên `SqlServer` (lần chạy 03/08/2026) | 1729 pass, 1 fail vì test procurement tên legacy vẫn mở SQL Server |
| Full suite lần chạy 03/08/2026 | Không xác nhận được: các integration test không kết nối được SQL Server mặc định, lỗi SNI 26 |

Kết quả 1844/1844 và 1708/1708 ở trên là lần xác minh trước đó khi máy kiểm thử có SQL Server phù hợp. Lần chạy gần nhất sau refactor OTP đã khởi chạy full suite nhưng không thể xác nhận lại nhóm SQL do instance mặc định không truy cập được (`SNI error 26`). Phần không phụ thuộc SQL đạt 1729 test; test thất bại duy nhất trong filter này là procurement E2E có tên legacy nhưng vẫn cần SQL Server. Nhóm mới trực tiếp kiểm tra OTP đạt 16/16.

Mười ba test tích hợp SQL/OTP qua .NET đã được chạy lại trên `localhost\SQLEXPRESS02` và pass 13/13, gồm consume OTP đồng thời, đóng ngoại lệ đúng trạng thái `RECONCILIATION_REQUIRED` và chỉ tạo một WorkShift khi nhiều request mở ca trễ chạy đồng thời. Kết nối Windows Integrated Authentication cần chạy ngoài filesystem sandbox; chạy trong sandbox trả lỗi SSPI, không phải lỗi test nghiệp vụ. Backend E2E đã khởi động trên database seed và route đăng nhập trả HTTP 200; browser E2E tương tác chưa chạy được vì runtime Codex không có browser khả dụng (`agent.browsers.list()` trả danh sách rỗng).

## Kết quả SeedAll

SeedAll đã chạy hai lần trên `CafeChain_SeedAll_Idempotency_20260803`. Sau hai lần, số lượng tương ứng của StoreToppings, SupplierStores, StoreDrinks, StoreMenuItems, InventoryDocs, InventoryDocDetails, StoreInventories, InventoryTransactions, ProductionRuns, InventoryCostLayers, DrinkRecipeDetails, StoreInventoryPolicies, WorkShifts, Orders và Payments không đổi:

```text
101|101|120|168|6|106|124|1254|71|178|738|244|65|136|127
```

Đã xác minh có đúng tám permission `POS.WorkShift.*`; SalesStaff nhận 4 quyền, ShiftSupervisor/StoreManager/AreaManager/BusinessOwner nhận 8 quyền, các role ngoài nghiệp vụ không được cấp mặc định.

## Rủi ro còn lại trước production

- Migration hợp nhất là InitialCreate cho database mới; database đã tồn tại với lịch sử migration cũ cần kế hoạch backup/baseline hoặc chuyển dữ liệu, không áp trực tiếp chồng schema.
- OTP rate limit theo nhân viên/terminal/IP/device dùng dữ liệu SQL và index dùng chung giữa các instance; kiểm thử tải dài hạn với nhiều application instance vật lý vẫn cần thực hiện trước production.
- SignalR giảm độ trễ giao diện nhưng không phải nguồn khóa; backend/database vẫn là nguồn quyết định.
- Cần chạy browser E2E đầy đủ trước production trên máy có browser; môi trường Codex hiện tại không cung cấp browser để thao tác. Integration SQL Server Express cục bộ đã pass nhưng vẫn cần chạy lại trong CI bằng connection string riêng của môi trường.
- Warning biên dịch nền hiện hữu chưa được triệt tiêu. Cần chạy lại full suite với `CAFECHAIN_TEST_SQLSERVER_CONNECTION_STRING` trỏ tới SQL Server khả dụng trước khi phát hành.

## File trọng tâm

- `Models/Stores/WorkShift.cs`, `Data/Configurations/Stores/StoreConfiguration.cs`
- `Models/Operations/OtpChallenge.cs`, `Models/Systems/RequestDeduplication.cs`
- `Migrations/20260802183312_InitialCreate.cs`
- `Migrations/20260803050636_AddProtectedOperationalOtpPayload.cs`
- `Scripts/SeedAll.sql`
- `Application/Services/POS/WorkShiftService.cs`
- `Application/Services/POS/POSPaymentCancellationService.cs`
- `Application/Workers/WorkShiftExpiryWorker.cs`
- `Application/Services/POS/OtpApprovalService.cs`
- `Infrastructure/Repositories/Admin/POS/WorkShiftRepository.cs`
- `Controllers/Api/v1/POSShiftController.cs`, `POSTerminalController.cs`
- `Controllers/Api/v1/POSPaymentController.cs`
- `Hubs/WorkShiftHub.cs`
- `CafeChain.Frontend/src/pages/ShiftSummary.tsx`
- `CafeChain.Frontend/src/POSLayout.tsx`
- `Doc/STAFFHUB_POS_WORKSHIFT_BUSINESS_RULES.md`
- `Doc/STAFFHUB_POS_WORKSHIFT_REFACTOR_GUIDE.md`
