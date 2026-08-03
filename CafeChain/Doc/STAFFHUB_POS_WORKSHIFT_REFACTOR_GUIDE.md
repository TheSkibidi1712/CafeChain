# Hướng dẫn refactor StaffHub và WorkShift POS

Tài liệu này mô tả phiên bản hiện hành sau khi hợp nhất migration, cách cài đặt và cách vận hành StaffHub/WorkShift. `WorkShift` chỉ là phiên chịu trách nhiệm POS/két; không phải dữ liệu chấm công, giờ công hoặc tính lương.

Phân tích chi tiết từng tình huống người dùng nằm tại [STAFFHUB_USER_BUSINESS_FLOWS.md](./STAFFHUB_USER_BUSINESS_FLOWS.md).

## 1. Phạm vi đã triển khai

- Lịch dự kiến `StaffShift` hỗ trợ ca qua đêm bằng khoảng thời gian tuyệt đối.
- Mở POS được phân loại `WITHIN_SCHEDULE`, `LATE_FOR_SCHEDULE`, `OUTSIDE_SCHEDULE` hoặc `LEGACY`.
- Mở ngoài lịch bắt buộc lý do và phê duyệt OTP, không tạo `StaffShift` giả.
- Phiên ngoài lịch hết hạn sau sáu giờ theo thời gian máy chủ.
- Trạng thái WorkShift gồm `OPEN`, `CLOSING`, `EXPIRED_PENDING_CLOSE`, `CLOSED`, `RECONCILIATION_REQUIRED`.
- Worker cảnh báo tại 30/10/1 phút, tự đóng duy nhất phiên hoàn toàn rỗng và chuyển các phiên còn tiền/giao dịch sang chờ chốt.
- Mở, đóng, đóng ngoại lệ, reconcile và đăng ký terminal có `RequestKey` chống gửi trùng.
- Backend kiểm tra permission, store scope, terminal, trạng thái và thời hạn; không tin `StaffId`, `StoreId` hoặc thời gian do trình duyệt gửi.
- `POSPaymentController` chỉ điều phối HTTP; hủy payment, hoàn tiền tạm, transaction và idempotency actor+store nằm trong `POSPaymentCancellationService`.
- POS nhận sự kiện SignalR, đồng thời polling lại trạng thái khi reconnect/focus.
- StaffHub dùng mã exchange một lần thay vì truyền JWT qua query string.
- Audit WorkShift dùng `AuditLog`, không ghi nhầm vào `InvoiceAuditLog`.
- OTP gồm 6 ký tự chữ/số từ alphabet không gây nhầm, dùng thời gian máy chủ, revalidate người duyệt khi verify/resend, khóa resend 60 giây, rate-limit SQL 15 phút theo nhân viên/terminal/IP/device và giữ nguyên bộ đếm sai khi resend. IP/device chỉ được lưu dưới dạng SHA-256. Đúng approver có thể xem lại mã còn hiệu lực từ ciphertext Data Protection trong chuông hợp nhất.

## 2. Database và migration hiện hành

Mã nguồn hiện dùng một migration khởi tạo hoàn chỉnh và một migration tăng dần:

```text
Migrations/20260802183312_InitialCreate.cs
Migrations/20260803050636_AddProtectedOperationalOtpPayload.cs
```

Migration chỉ tạo schema, constraint và index; quyền/role không được seed trong migration. Migration tăng dần mới chỉ thêm cột ciphertext nullable cho OTP, không sửa dữ liệu seed.

Đây là migration khởi tạo hợp nhất, phù hợp để tạo database mới. Nếu database đang dùng có lịch sử migration cũ, hãy sao lưu trước; không chạy migration khởi tạo này chồng lên schema đã tồn tại. Với môi trường phát triển có thể tạo database mới rồi import dữ liệu cần giữ theo quy trình riêng.

Áp dụng schema:

```powershell
dotnet ef database update --project CafeChain/CafeChain.csproj
```

Các bảo vệ quan trọng trong schema:

- `ExpiryWarningLevel` có mặc định `0`.
- Tiền đầu/cuối phiên không âm và là số nguyên VND.
- Filtered unique index ngăn hai phiên active trên cùng terminal.
- Filtered unique index ngăn một nhân viên có hai phiên active.
- `RowVersion` bảo vệ cập nhật đồng thời.

## 3. Chạy SeedAll an toàn

`CafeChain/Scripts/SeedAll.sql` là nguồn seed duy nhất cho permission, role-permission và dữ liệu demo. Script không còn hard-code `USE [CafeChain]`; nó chạy trên database đang được chọn và từ chối database hệ thống.

Ví dụ với SQL Server Express:

```powershell
sqlcmd -S "localhost\SQLEXPRESS02" -E -d CafeChain `
  -i "CafeChain\Scripts\SeedAll.sql" -b -f 65001
```

Có thể chạy lại cùng lệnh. Marker `seedall_foundation_inventory_v1` trong `SystemSettings` giúp các batch nền về menu/inventory/FIFO không nhân đôi dữ liệu; các batch còn lại lookup theo business key hoặc mã quyền.

Tám permission được seed:

- `POS.WorkShift.View`
- `POS.WorkShift.Open`
- `POS.WorkShift.Close`
- `POS.WorkShift.OpenOutsideSchedule`
- `POS.WorkShift.ApproveOutsideSchedule`
- `POS.WorkShift.CloseException`
- `POS.WorkShift.Reconcile`
- `POS.WorkShift.OverrideTerminal`

Nhân viên bán hàng nhận bốn quyền View/Open/Close/OpenOutsideSchedule. Ca trưởng, quản lý chi nhánh, quản lý vùng và chủ doanh nghiệp nhận đủ tám quyền trong scope. SystemAdmin, kế toán/kho và khách hàng không tự nhận quyền nghiệp vụ WorkShift.

## 4. Cấu hình và chạy ứng dụng

Backend:

```powershell
dotnet restore CafeChain/CafeChain.csproj
dotnet build CafeChain/CafeChain.csproj
dotnet run --project CafeChain/CafeChain.csproj
```

Frontend:

```powershell
cd CafeChain.Frontend
npm ci
npm run build
npm run lint
npm run dev
```

Timezone nghiệp vụ là `Asia/Ho_Chi_Minh`; các thời điểm WorkShift lưu UTC và được chuyển sang giờ cửa hàng khi hiển thị.

## 5. Cách sử dụng

### 5.1 Vào POS từ StaffHub

1. Đăng nhập StaffHub.
2. Chọn mở POS.
3. Backend tạo mã exchange dùng một lần, thời hạn ngắn.
4. POS đổi mã qua POST để nhận phiên đăng nhập; JWT không xuất hiện trên query string.

### 5.2 Terminal chưa đăng ký

Nếu assessment trả `TERMINAL_NOT_FOUND`, POS hiển thị phần đăng ký terminal:

1. Nhập tên terminal.
2. Yêu cầu OTP cho action `REGISTER_POS_TERMINAL`.
3. Người duyệt phải có `POS.WorkShift.OverrideTerminal` và đúng store scope.
4. Xác nhận OTP rồi đăng ký terminal.
5. POS giữ nguyên `RequestKey` nếu request timeout và được gửi lại.

Terminal mới không được tự active chỉ từ GUID do trình duyệt tạo.

### 5.3 Mở WorkShift

POS gọi assessment trước khi mở:

- `WITHIN_SCHEDULE`: mở theo lịch hợp lệ.
- `LATE_FOR_SCHEDULE`: trễ trên 15 phút cần lý do; trễ trên 30 phút cần OTP.
- `OUTSIDE_SCHEDULE`: luôn cần lý do 10–500 ký tự và OTP; `AutoCloseAtUtc = StartTimeUtc + 6 giờ`.

Tiền đầu phiên là số nguyên VND không âm. Client không gửi `StaffId`, `StoreId`, `StartTimeUtc`, `AutoCloseAtUtc`, tiền kỳ vọng hoặc chênh lệch.

### 5.4 Khi phiên gần hết hạn

POS tính countdown từ `serverNowUtc`, tự đồng bộ lại qua polling/SignalR và khóa tạo order/payment ngay khi đạt deadline. Worker phát cảnh báo 30/10/1 phút.

- Phiên hoàn toàn rỗng và tiền đầu phiên bằng 0: tự đóng `AUTO_EMPTY_SHIFT`.
- Phiên có tiền, order, payment hoặc dữ liệu cần kiểm đếm: chuyển `EXPIRED_PENDING_CLOSE`.
- Không tự điền `ActualEndingCash` và không gia hạn phiên cũ.

Muốn tiếp tục bán phải chốt hoặc đóng ngoại lệ phiên cũ, sau đó mở phiên mới với lý do, phê duyệt và tiền đầu phiên được kiểm đếm lại.

### 5.5 Đóng và reconcile

1. `start-closing` chuyển phiên sang `CLOSING`, khóa giao dịch mới và trả preview do backend tính.
2. Người dùng nhập tiền thực tế.
3. Backend tự tính tiền kỳ vọng và chênh lệch.
4. Payment đang xử lý hoặc order offline chưa đồng bộ sẽ chặn đóng thường.
5. Đóng ngoại lệ cần permission, lý do, OTP và kiểm đếm; trạng thái thành `RECONCILIATION_REQUIRED`.
6. Order/payment đồng bộ muộn vẫn giữ WorkShift cũ.
7. Người có quyền `Reconcile` chỉ được chốt khi manifest không còn đơn offline và server đã xác nhận đủ số đơn offline ghi nhận lúc đóng ngoại lệ; không chuyển doanh thu sang phiên mới.

Không tự sao chép toàn bộ tiền cuối phiên cũ thành tiền đầu phiên mới.

## 6. API chính

- `POST /api/v1/pos/shifts/open-assessment`
- `POST /api/v1/pos/shifts/open`
- `POST /api/v1/pos/shifts/{id}/start-closing`
- `POST /api/v1/pos/shifts/{id}/close`
- `POST /api/v1/pos/shifts/{id}/close-exception`
- `POST /api/v1/pos/shifts/{id}/reconcile`
- `POST /api/v1/pos/terminals/register`
- SignalR hub: `/hubs/workshifts`

Frontend phải phân nhánh theo `errorCode`, không so sánh message tiếng Việt. Các mã thường gặp gồm `TERMINAL_NOT_FOUND`, `WORKSHIFT_EXPIRED`, `WORKSHIFT_PENDING_CLOSE`, `PAYMENT_IN_PROGRESS`, `OFFLINE_ORDERS_PENDING`, `DUPLICATE_REQUEST`, `CONCURRENCY_CONFLICT`.

## 7. Các lỗi SeedAll đã được xử lý

- `#OverrideBefore` tồn tại: temp table được drop trước khi tạo lại.
- `Invalid column name 'StartTime'`: fixture dùng `StartTimeUtc`/`EndTimeUtc`.
- `ExpiryWarningLevel` bị NULL: model, migration và fixture đều có giá trị mặc định `0`.
- Các lỗi AI/demo thiếu staff, ingredient, consumption hoặc receipt actor: batch nền được chạy một lần theo marker và dữ liệu phụ thuộc được giữ ổn định khi replay.

Nếu chạy script trong cùng một cửa sổ SSMS sau lần lỗi cũ, nên reconnect session rồi chạy lại toàn bộ script trên database đích.

## 8. Kiểm tra thủ công khuyến nghị

1. Mở ca theo lịch qua đêm và kiểm tra hiển thị đủ hai ngày.
2. Mở trễ tại các mốc 15/30 phút.
3. Mở ngoài lịch, xác nhận OTP và deadline đúng sáu giờ.
4. Double-click hoặc retry cùng `RequestKey`; chỉ có một WorkShift.
5. Thử mở cùng terminal/nhân viên từ hai thiết bị.
6. Hết hạn phiên rỗng và phiên có tiền để kiểm tra hai nhánh khác nhau.
7. Thử tạo order/payment sau deadline khi ngắt SignalR.
8. Đóng thường khi có payment pending/offline; sau đó đóng ngoại lệ và reconcile.
9. Sửa StoreId/StaffId ở payload và xác nhận backend vẫn từ chối ngoài scope.
10. Chạy `SeedAll.sql` hai lần và so sánh số lượng dữ liệu chính.

## 9. Kết quả kiểm tra tại thời điểm bàn giao

- Backend build: pass, 0 error; 670 warning nền khi biên dịch lại toàn bộ.
- Frontend build và lint: pass; Vite chỉ cảnh báo kích thước chunk/annotation.
- Test mới cho OTP 6 ký tự, validation, ciphertext và đúng người nhận: pass 16/16.
- Lần chạy gần nhất với filter loại tên `SqlServer`: 1729 pass, 1 fail vì test procurement E2E tên legacy vẫn cần SQL Server.
- Full suite lần chạy gần nhất chưa thể xác nhận do SQL Server mặc định không truy cập được (`SNI error 26`); cần chạy lại với `CAFECHAIN_TEST_SQLSERVER_CONNECTION_STRING` hợp lệ.
- Unit test mới cho assessment, lịch qua đêm và expiry worker: pass.
- Nhóm WorkShift/OTP/offline/schedule không cần SQL Server: pass 135/135.
- Nhóm WorkShift/OTP/offline/schedule/payment không cần SQL Server: pass 148/148.
- Lần rà soát cuối theo filter WorkShift/OTP/offline/schedule/payment không-SQL: pass 138/138.
- Nhóm WorkShift/OTP/offline/schedule dùng SQL Server Express: pass 13/13.
- SeedAll chạy liên tiếp hai lần trên database kiểm thử: pass và số lượng bảng chính không đổi.
- Migration tạo database mới, default/index/check constraint: pass bằng SQL Server thực.
- Kết quả lịch sử khi có SQL Server: full suite loại trừ class có tên `SqlServer` pass 1708/1708 và toàn bộ integration pass 1844/1844.
- Nhóm test SQL Server Express đã chạy bằng `CAFECHAIN_TEST_SQLSERVER_CONNECTION_STRING` và pass 13/13; CI cần cung cấp connection string tương đương.
- Backend E2E đã khởi động với database seed và route đăng nhập trả HTTP 200. Browser E2E tương tác chưa chạy được vì runtime Codex không có browser khả dụng; cần thực hiện danh sách kiểm tra thủ công ở trên trước khi phát hành production.
