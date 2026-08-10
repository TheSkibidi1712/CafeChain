# Hướng dẫn sử dụng nghiệp vụ StaffHub và POS

StaffHub/POS được mở từ **AppLauncher** theo `App.StaffHub`/`App.POS`. Quyền `AdminDashboardApp` là entry policy riêng của Admin Dashboard; có quyền StaffHub/POS không tự cấp quyền vào Dashboard và ngược lại.

Nguồn quy tắc chuẩn: [STAFFHUB_POS_WORKSHIFT_BUSINESS_RULES.md](./STAFFHUB_POS_WORKSHIFT_BUSINESS_RULES.md).

Thẻ không có phân công phải hiển thị: **“Chưa có lịch — Thời gian nghỉ hoặc chưa được phân ca.”** `AppLauncher` và quyền vào `App.AdminDashboard` chỉ điều hướng theo permission, không tự tạo WorkShift.

## 1. Tài khoản và chuẩn bị

| Vai trò | Tài khoản | Mật khẩu | Dùng để |
|---|---|---|---|
| Nhân viên bán hàng | `salesstaff@cafechain.vn` | `The@1712` | StaffHub/POS requester |
| Nhân viên bán hàng 2 | `salesstaff2@cafechain.vn` | `The@1712` | Store 1; test hai nhân viên/hai terminal/Current Operator |
| Nhân viên bán hàng 3 | `salesstaff3@cafechain.vn` | `The@1712` | Store 3; test cô lập cửa hàng |
| Ca trưởng | `shiftsupervisor@cafechain.vn` | `The@1712` | nhận/duyệt OTP |
| Quản lý chi nhánh | `storemanager@cafechain.vn` | `The@1712` | kiểm tra permission/đối soát |

Trước mỗi ca test:

1. Dùng database test, chạy `SeedAll.sql`; xác nhận `salesstaff` và `salesstaff2` ở Store 1, `salesstaff3` ở Store 3. Hai tài khoản mới chưa có PIN.
2. Tạo ít nhất hai terminal active, ví dụ `POS-T1`, `POS-T2`.
3. Đóng/đối soát WorkShift cũ hoặc ghi lại ID nếu đang test resume/expired.
4. Mở DevTools → Network, giữ log; ghi RequestKey, HTTP, `errorCode`, `shiftId`, UTC.
5. Không sửa giờ máy người dùng để giả UTC. Dùng lịch test hoặc cập nhật dữ liệu trong DB test rồi restart worker.

Truy vấn kiểm tra chung:

```sql
SELECT ShiftId, StoreId, UserId, PosTerminalId, Status, OpenContext,
       StartTimeUtc, AutoCloseAtUtc, DATEDIFF(SECOND, StartTimeUtc, AutoCloseAtUtc) AS DurationSeconds,
       StartingCash, ExpectedEndingCash, EndTimeUtc, CloseType
FROM WorkShifts
WHERE UserId = 4 OR PosTerminalId IN (N'POS-T1', N'POS-T2')
ORDER BY ShiftId DESC;
```

## 2. Cấu hình email OTP an toàn

Trong thư mục project backend:

```powershell
dotnet user-secrets set "Email:Username" "dia-chi-gmail-gui-otp"
dotnet user-secrets set "Email:Password" "GMAIL_APP_PASSWORD"
```

Không commit Gmail App Password, không chụp màn hình secret, không ghi secret vào tài liệu/log. Restart backend sau khi set. Xác nhận email của ca trưởng trong Admin trỏ tới inbox bạn kiểm soát; kiểm tra Inbox, Spam và Promotions. Nếu không nhận được, kiểm tra log `OTP_EMAIL_SEND_FAILED`, cấu hình SMTP và quyền approver đúng store.

## 3. Ca đúng lịch — tạo tại POS

1. Đăng nhập `salesstaff@cafechain.vn`, từ AppLauncher bấm **Mở POS**. Trình duyệt phải vào `/StaffHub?openPos=1` và tự mở modal; chọn `POS-T1` nếu URL chưa có `terminalId`.
2. Preview phải là `WITHIN_SCHEDULE`, không sớm; lý do/OTP ẩn.
3. Bấm **Tiếp tục sang POS**. Network `IssuePosToken` trả `workShiftId:null`, `requiresOpeningCash:true`.
4. POS mở trang **Quản lý ca**, nhập tiền đầu phiên là số nguyên không âm, bội 1.000; bấm **Xác nhận mở ca**.
5. Response 201 có `resultCode=OPENED_NEW_WORKSHIFT`, `shiftId>0`, đúng `terminalId`, `requiresOpeningCash=false`; các UTC có `Z`.
6. `GET current` phải trả cùng ID. Vào bán hàng và tạo một order test.

Mở trực tiếp URL Vite khi chưa có context phải tự quay về StaffHub. Nếu lịch đổi từ bình thường sang sớm/trễ/ngoài lịch trước lúc xác nhận, response là HTTP 409 `STAFFHUB_OPEN_REQUIRED`, `recommendedAction=OPEN_STAFFHUB`; POS không được tự hiện form OTP.

Kết quả DB: đúng một `OPEN`, `OpenContext=WITHIN_SCHEDULE`, tiền đầu phiên khớp; không có `StaffShift` mới.

## 4. Ca mở sớm — xác minh tại StaffHub, tạo ca tại POS

1. Chuẩn bị lịch bắt đầu trong khoảng mở sớm cho phép; preview phải có `MinutesEarly>0`.
2. Bấm **Tiếp tục sang POS**. StaffHub chỉ phát exchange context; `IssuePosToken` trả `workShiftId:null`, `requiresOpeningCash:true`.
3. POS hiển thị form tiền đầu phiên; chưa có WorkShift và màn bán hàng chưa nhận giao dịch.
4. Nhập tiền và bấm **Xác nhận mở ca**. Đây là thời điểm duy nhất WorkShift được tạo và POS session được bind.
5. Refresh rồi xác nhận lại phải bị từ chối/replay an toàn; không đổi tiền bằng payload khác.

## 5. Ca trễ

### Trễ cần lý do, chưa cần OTP

1. Chuẩn bị thời điểm vượt ngưỡng lý do nhưng chưa vượt ngưỡng OTP.
2. Preview `LATE_FOR_SCHEDULE`; nhập lý do 10–500 ký tự.
3. StaffHub chỉ phát exchange context; POS nhập tiền và bấm **Xác nhận mở ca** mới tạo/bind WorkShift. DB lưu đúng lịch nguồn và lý do nghiệp vụ.

### Trễ từ 30 phút — yêu cầu Manager, không dùng OTP

1. Chuẩn bị trễ từ 30 phút; preview phải hiện **Gửi yêu cầu Manager**, tuyệt đối không hiện **Gửi OTP**.
2. Nhập lý do 10–500 ký tự, bấm **Gửi yêu cầu Manager**. Terminal/lý do được khóa theo request đang chờ.
3. Manager đúng permission và StaffScope mở **Thông báo**, bấm **Xem và duyệt yêu cầu**; hệ thống dẫn tới **Nhân sự & Vận hành → Duyệt mở ca trễ** và đúng thẻ yêu cầu.
4. Nếu trễ từ 30 đến 45 phút, Manager được chọn **Duyệt mở ca**, **Từ chối** hoặc **Chuyển ngoài lịch**.
5. Nếu trễ trên 45 phút, nút **Duyệt mở ca** bị khóa; Manager chỉ được chọn **Từ chối** hoặc **Chuyển ngoài lịch**. Backend cũng chặn direct POST `APPROVED`.
6. **Chuyển ngoài lịch** không sửa `StaffShift` gốc và không tự tạo WorkShift. Requester nhận cập nhật realtime, tiếp tục sang POS, nhập tiền đầu ca và bấm **Xác nhận mở ca**; lúc đó backend mới tạo WorkShift ngoài lịch.
7. Nếu chọn **Từ chối**, yêu cầu kết thúc và requester không được tiếp tục bằng lịch cũ.

> Lỗi cũ “Quyết định không hợp lệ” xảy ra vì button bị khóa trước khi trình duyệt gửi trường `Decision`. Form hiện dùng hidden field authoritative, nên **Từ chối** và **Chuyển ngoài lịch** luôn gửi đúng quyết định; backend vẫn kiểm permission, StaffScope, RequestKey và row-version.

## 6. Ngoài lịch và hạn 6 giờ

1. Bảo đảm nhân viên không có lịch hiệu lực; chọn terminal trống.
2. Preview `OUTSIDE_SCHEDULE`, hiển thị hạn dự kiến; nhập lý do rồi bấm **Gửi OTP**.
3. Đăng nhập `shiftsupervisor@cafechain.vn`. Ca trưởng có `POS.WorkShift.ApproveOutsideSchedule` và đúng scope được ưu tiên nhận popup **Có yêu cầu xác nhận POS mới**. Popup không chứa mã; bấm **Mở Thông báo** rồi **Xem OTP** và cung cấp mã cho requester.
4. Nếu Ca trưởng thiếu quyền, email, scope hoặc inactive, request mới fallback lần lượt tới `storemanager@cafechain.vn`, Area Manager rồi Business Owner. Không được self-approval/cross-store. Nếu không có candidate hợp lệ, requester nhận `NO_ELIGIBLE_APPROVER`.
5. Notification ngoài lịch không được hiện ô hay nút **Xác nhận Terminal**. Nếu SMTP lỗi, notification nội bộ vẫn là kênh authoritative.
6. Requester nhập OTP, bấm **Xác nhận OTP**, sau đó **Tiếp tục sang POS**. StaffHub vẫn chưa tạo WorkShift.
7. POS nhập tiền và bấm **Xác nhận mở ca** để tạo WorkShift và kích hoạt giao dịch.
8. So `Date.parse(autoCloseAtUtc)-Date.parse(startTimeUtc)` hoặc SQL: phải đúng `21600` giây.
9. Ở múi giờ Việt Nam, UI hiển thị giờ địa phương đúng nhưng wire vẫn có `Z`; không được báo hết hạn ngay sau mở.

Quy tắc DB bắt buộc: `AutoCloseAtUtc = StartTimeUtc + 6 giờ`.

## 7. OTP, gửi lại và hủy yêu cầu mở ca

- **Reload Pending:** gửi OTP, reload StaffHub, mở lại modal. `GetOpenPosOtpState` trả challenge của chính requester/store; countdown tiếp tục, terminal/lý do bị khóa.
- **Thời gian:** thời điểm gửi/hết hạn luôn hiển thị theo giờ Việt Nam; countdown lấy chênh lệch `expiresAtUtc - serverNowUtc`, không dùng đồng hồ máy người dùng. Một OTP mới phải còn gần 5 phút, không được hết hạn ngay.
- **Popup realtime:** popup chỉ báo có yêu cầu và dẫn tới `Thông báo → Xem OTP`; không hiển thị/copy OTP trên popup hoặc payload SignalR. Mỗi yêu cầu chỉ popup một lần trong browser session; resend có hạn mới nên được báo lại.
- **Reload Approved:** xác nhận đúng rồi reload. Cụm nhập/xác nhận/gửi lại vẫn ẩn; nút sang POS bật.
- **Resend:** trước cooldown nút disabled; hết cooldown bấm được, SweetAlert2 báo mã mới, mã cũ vô hiệu.
- **Expired:** để quá TTL; verify trả HTTP 410 + `OTP_EXPIRED`, control vô hiệu.
- **Locked:** nhập sai đến giới hạn; lần cuối HTTP 423 + `OTP_VERIFICATION_LOCKED`.
- **Already used:** verify challenge đã approved/used trả HTTP 409 + `OTP_ALREADY_USED`.
- **Context mismatch:** dùng public ID của staff/store khác trả HTTP 409 + `OTP_CONTEXT_MISMATCH`; response không có OTP/hash/protected payload.
- **Rate limit:** vượt ngưỡng trả HTTP 429 + `OTP_RATE_LIMITED`.

Khi đã tạo OTP hoặc approval nhưng chưa mở WorkShift, nút **Hủy** đổi thành **Hủy yêu cầu mở ca**:

1. Bấm nút và xác nhận cảnh báo.
2. StaffHub gọi backend để chuyển OTP/approval chưa dùng sang `CANCELLED`, resolve notification, xóa protected OTP và kết thúc context truy cập chưa bind.
3. Modal chỉ đóng sau khi backend xác nhận. Nếu API lỗi, modal giữ nguyên để người dùng thử lại; backdrop/Escape không được âm thầm bỏ lại request active.
4. Nếu WorkShift đã được tạo, backend trả 409 và không xóa ca.

Tại màn nhập tiền đầu ca POS, bấm **Hủy mở ca và quay lại StaffHub** để vô hiệu exchange/POS session chưa bind và hủy intent còn hiệu lực. Không có WorkShift nào được tạo. Record `CANCELLED` được giữ để audit; “hủy” không có nghĩa xóa lịch sử.

Về kỹ thuật, exchange ticket dùng trạng thái hợp lệ `EXPIRED` kèm `CancelledAtUtc` trong context; OTP và approval nghiệp vụ mới dùng `CANCELLED`. Không ghi `CANCELLED` vào `RequestDeduplications`, vì bảng này chỉ chấp nhận `PROCESSING`, `SUCCESS`, `FAILED`, `EXPIRED`. Nếu POS session đã bind `WorkShiftId`, backend trả 409 và không hủy ca.

## 8. Resume, đóng và mở lại

- Staff có `OPEN`: StaffHub trả `STAFF_ALREADY_HAS_OPEN_SHIFT`, hiện ID/terminal/thời gian và **Tiếp tục POS**. Exchange resume có đúng ID, `RESUME_WORKSHIFT`, không hỏi tiền.
- `CLOSING`: trả `WORKSHIFT_PENDING_CLOSE`, nút **Hoàn tất đóng ca**.
- `EXPIRED_PENDING_CLOSE`: trả `WORKSHIFT_PENDING_CLOSE`, nút **Kiểm đếm và đóng**.
- `CLOSED`: không khóa. Mở mới phải sinh ID/RequestKey/tiền mới.
- `RECONCILIATION_REQUIRED`: không được chọn làm current; dữ liệu sync muộn vẫn giữ ID cũ.

## 9. Terminal, hai nhân viên và race

1. Đăng nhập A=`salesstaff`, mở `POS-T1`; ghi `ShiftId` và tên người chịu trách nhiệm.
2. Ở phiên trình duyệt riêng, đăng nhập B=`salesstaff2`, chọn `POS-T1`: HTTP 409 `TERMINAL_ALREADY_HAS_OPEN_SHIFT`, modal phải hiện A là người chịu trách nhiệm, `isOwnedByRequester=false`, `recommendedAction=SWITCH_CURRENT_OPERATOR`; **không có Tiếp tục POS**.
3. B chọn terminal trống `POS-T2`: được mở WorkShift riêng. Hai két và hai WorkShift độc lập dù cùng Store 1.
4. A mở lại `POS-T1`: `STAFF_ALREADY_HAS_OPEN_SHIFT`, modal hiện **Tiếp tục POS**, resume đúng ID và không hỏi tiền đầu ca.
5. Đăng nhập C=`salesstaff3`, mở terminal của Store 3: WorkShift Store 1 không được xuất hiện hoặc khóa C. Terminal Store 1 không được chọn/đăng ký trong scope Store 3.
6. Double-click gửi/open: chỉ một challenge/WorkShift; nút có busy guard.
7. Replay cùng RequestKey và cùng tiền trả ID cũ. Cùng key nhưng tiền khác trả `DUPLICATE_REQUEST`.
8. Gửi đồng thời hai key cho cùng staff/terminal: chỉ một thắng; request sau trả lỗi active hoặc `CONCURRENCY_CONFLICT`.

Terminal không tồn tại/inactive/sai store lần lượt phải được xử lý bằng nhóm lỗi bắt đầu từ `TERMINAL_NOT_FOUND`; không tự đăng ký terminal tại POS.

Xác nhận active trùng:

```sql
SELECT UserId, COUNT(*) AS ActiveCount
FROM WorkShifts
WHERE Status IN ('OPEN','CLOSING','EXPIRED_PENDING_CLOSE')
GROUP BY UserId HAVING COUNT(*) > 1;

SELECT PosTerminalId, COUNT(*) AS ActiveCount
FROM WorkShifts
WHERE PosTerminalId IS NOT NULL
  AND Status IN ('OPEN','CLOSING','EXPIRED_PENDING_CLOSE')
GROUP BY PosTerminalId HAVING COUNT(*) > 1;
```

Hai query phải trả 0 dòng.

## 10. Worker hết hạn

Thực hiện trên DB test với một ca rỗng và một ca có order/tiền, đặt `AutoCloseAtUtc` qua hạn rồi chạy worker:

- Cả hai phải thành `EXPIRED_PENDING_CLOSE`.
- `EndTimeUtc`, `ActualEndingCash`, `CloseType` không bị worker tự điền.
- Terminal vẫn bị khóa cho đến khi kiểm đếm/đóng.

## 11. Current Operator, offline và permission

### Thiết lập PIN cá nhân

Thực hiện riêng cho `salesstaff` và `salesstaff2`:

1. Đăng nhập tài khoản cần tạo PIN, vào StaffHub.
2. Tại **PIN thao tác POS**, bấm **Thiết lập hoặc đổi PIN**.
3. Nhập mật khẩu hiện tại `The@1712`.
4. Tạo PIN cá nhân đúng 6 chữ số. Không dùng một số lặp sáu lần, `123456` hoặc `654321`.
5. Bấm **Lưu PIN**. Sau thành công thẻ chuyển ngay sang badge xanh **Đã thiết lập**, nút đổi thành **Đổi PIN** và hiện toast thành công. Reload StaffHub vẫn giữ trạng thái này từ backend.
6. PIN và mật khẩu được xóa khỏi form khi modal đóng. PIN không được hiển thị lại, gửi email, lưu local storage hoặc xuất hiện trong log/DB dạng rõ; DB chỉ có BCrypt hash.

### Đổi Current Operator trên ca của người khác

1. Giữ ca của A=`salesstaff` đang `OPEN` trên `POS-T1`; không đóng và không đăng xuất POS tại terminal này.
2. Tại POS hiện tại chọn **Đổi Current Operator**; chọn/nhập nhân viên B=`salesstaff2` và PIN cá nhân của B.
3. Sau khi PIN đúng, trang **Quản lý ca** hiện tên B kèm badge **Đang thao tác** và thời điểm đổi; header màn bán hàng cũng hiện B là **Người đang thao tác**. Tooltip vẫn chỉ rõ A là người chịu trách nhiệm két.
4. Xác nhận `WorkShiftId`, `WorkShift.UserId`, tiền đầu ca và người chịu trách nhiệm két vẫn là A. Tab POS khác cùng terminal/store tự tải lại tên operator qua SignalR.
5. Tạo order; DB phải ghi B là người thao tác order. Sai PIN tăng bộ đếm/khóa đúng chính sách và không đổi operator.

Ý nghĩa nghiệp vụ:

- Bàn giao thao tác bán hàng trên cùng quầy mà không phải đóng két và mở WorkShift mới.
- Order, Payment và audit sau thời điểm đổi ghi nhận đúng người thực tế thao tác.
- A vẫn là người chịu trách nhiệm két; `WorkShift.UserId`, tiền đầu ca, WorkShiftId và trách nhiệm tài chính không đổi.
- Đây không phải thao tác bàn giao két. Muốn chuyển trách nhiệm tài chính phải đóng/đối soát ca A rồi mở ca mới theo quy trình.
- PIN thuộc cá nhân B, không được cho A hoặc người khác dùng thay. Backend kiểm account active, permission và đúng Store/StaffScope trước khi đổi.

- Nếu B đang ở StaffHub của máy khác, B không được dùng modal xung đột để redirect vào ca A; chỉ thao tác đổi operator tại POS đang chạy trên `POS-T1`.
- Chưa xác nhận tiền đầu ca: màn bán bị khóa; commit order và sync offline trả HTTP 409 `OPENING_CASH_REQUIRED`, `recommendedAction=ENTER_OPENING_CASH`.
- Offline order phải giữ `WorkShiftId` gốc; sync sau đóng không gắn sang ca mới.
- Bỏ `App.POS`/`POS.WorkShift.Open`: nút hoặc endpoint bị 403.
- Bỏ `POS.WorkShift.OpenOutsideSchedule`: ngoài lịch bị 403.
- Approver khác store/hết quyền không được duyệt.
- Mở trực tiếp POS hoặc token/context hết hạn trả `STAFFHUB_OPEN_REQUIRED` và tự quay lại `/StaffHub?openPos=1&terminalId=...`.

## 12. Modal responsive

Lặp lại ở `423×825`, `390×844`, `768×1024`, `1366×768`, zoom 100%:

1. Mở ba modal StaffHub: mở POS, đăng ký terminal, PIN.
2. Mở các modal dài ở Supplier, Product Modifier, Payment Workspace, Branch Inventory, Order History và các trang Admin có form dài.
3. Header/footer hoặc nút hành động phải truy cập được; body cuộn dọc; không tràn ngang, không cần thu nhỏ trình duyệt.
4. Bàn phím mobile mở lên vẫn cuộn tới input/OTP được.

## 13. Thứ tự chạy tự động cuối cùng

1. Targeted xUnit cho WorkShift/OTP/Modal.
2. SQL Server integration.
3. `npm run lint`.
4. `npm run build`.
5. EF pending-model check.
6. Toàn bộ `dotnet test`.

Lưu lại HTTP/error code, ảnh UI bốn viewport và kết quả SQL. Không dùng dữ liệu production để ép hạn/race.

## 14. Luồng cuối sau refactor Terminal, OTP và mở ca trễ

### Đăng ký Terminal

1. Đăng nhập `salesstaff@cafechain.vn`, bấm **Đăng ký terminal**, nhập tên rồi bấm **Gửi yêu cầu xác nhận Terminal**. Double-click hoặc gửi lại cùng `RequestKey` khi challenge còn hiệu lực chỉ trả challenge hiện tại; không tạo OTP, email hay notification thứ hai.
2. Mỗi yêu cầu có một notification có cấu trúc liên kết `OtpChallenge`, lưu Terminal, Store, requester, approver, thời gian gửi/hết hạn và trạng thái `Waiting`, `Used`, `Expired` hoặc `Cancelled`. Các timestamp truyền qua API có hậu tố `Z`; UI chuyển sang `Asia/Ho_Chi_Minh` để hiển thị.
3. Đăng nhập `storemanager@cafechain.vn`, mở `/Admin/AdminNotifications`, chọn notification đăng ký Terminal rồi bấm **Xem OTP**. Cả sáu ô vuông phải nằm trên cùng một hàng, được điền đồng bộ và hiển thị thông báo sẵn sàng; mã không tự submit. OTP plaintext chỉ được trả từ endpoint reveal `no-store` cho đúng recipient có `POS.WorkShift.OverrideTerminal` trong StaffScope.
4. Bấm **Xác nhận Terminal**: UI phải hiện ngay trạng thái đang gửi và chỉ phát một request `POST /Admin/AdminNotifications/ConfirmTerminal`. Nếu quá 15 giây, request bị hủy, nút được bật lại và người dùng nhận thông báo thử lại; không có loading vô hạn.
5. Manager kiểm tra thông tin rồi bấm **Xác nhận Terminal**. Nếu OTP rỗng/thiếu/sai alphabet, form hiển thị lỗi và focus ô cần nhập thay vì im lặng; Network chưa gửi request. Khi OTP hợp lệ, Network phải có đúng một `POST ConfirmTerminal`; backend xác minh, tạo Terminal, consume OTP, audit và cập nhật notification trong transaction. Requester không có endpoint tự hoàn tất đăng ký.
6. Đóng/mở modal không reset thời gian. UI lấy `serverNowUtc`, `expiresAtUtc` và cooldown từ backend. Explicit **Gửi lại OTP** sau cooldown mới rotate mã.
7. Ngay sau commit, backend phát sự kiện sanitized để browser approver tải lại notification và hiện popup điều hướng; sự kiện không chứa OTP. SMTP lỗi không hủy challenge; notification nội bộ vẫn dùng được. Worker chủ động chuyển OTP quá hạn, xóa protected OTP và phát trạng thái sau commit.

Lỗi xác nhận phải hiển thị theo `errorCode`, đặc biệt `OTP_INVALID`, `OTP_EXPIRED`, `OTP_VERIFICATION_LOCKED`, `TERMINAL_APPROVAL_FORBIDDEN`, `TERMINAL_STORE_SCOPE_INVALID`, `TERMINAL_NOT_PENDING`, `TERMINAL_APPROVAL_CONFLICT` và `INVALID_REQUEST_KEY`. Mọi nhánh lỗi phải dừng loading; thành công giữ nút disabled cho tới khi danh sách refresh.
8. Requester có thể **Hủy yêu cầu xác nhận Terminal** khi còn `Waiting`; backend đổi sang `Cancelled`, xóa protected OTP, resolve notification và phát trạng thái mới sau commit. Đóng modal chỉ đóng giao diện, không tự hủy challenge.

### Mở WorkShift trễ

- `0 < late <= 15`: cho mở, hiện số phút trễ và ghi audit; không cần lý do.
- `15 < late < 30`: bắt buộc lý do 10–500 ký tự, cho mở, ghi audit và notification thông tin cho Store Manager.
- `30 <= late <= 45`: không dùng OTP; StaffHub tạo request `PENDING`, Manager đúng scope được chọn `APPROVED`, `REJECTED` hoặc `CONVERTED_TO_OUTSIDE_SCHEDULE`.
- `late > 45`: không dùng OTP; **Duyệt mở ca** bị khóa, Manager đúng scope chỉ chọn `REJECTED` hoặc `CONVERTED_TO_OUTSIDE_SCHEDULE`.
- Sau `planned end + PostEndGraceMinutes`, lịch cũ không được bind lại. Chỉ được chuyển ngoài lịch hoặc Manager tạo lịch bổ sung.
- Mọi request/decision có `RequestKey`, rowversion, audit và realtime `LateOpenApprovalChanged`.

### POS access session

- Exchange ticket tạo `PosAccessSession` và JWT mang session id/JTI. Mỗi Terminal chỉ có một session `ACTIVE`; browser mới thay thế và revoke browser cũ.
- Backend kiểm tra session, token, account/staff/store/Terminal ở mọi mutation quan trọng. Order và thao tác đóng/đối soát ca dùng đúng `WorkShiftId` đã bind trong session.
- Chờ nhập tiền đầu phiên trả `OPENING_CASH_REQUIRED`; đây không phải “Phiên POS đã hết hạn”. Đổi Current Operator không reset hoặc kết thúc session.
- Chỉ trạng thái hết hạn/revoke/logout/Admin end/Terminal lock mới khóa POS. Client nhận `PosAccessSessionChanged` và vẫn revalidate/poll sau reconnect.
- Worker session chủ động persist trạng thái hết hạn và thông báo cho browser POS cùng Manager đúng store scope, không chờ request kế tiếp.
- Countdown dùng deadline server: từ một giờ hiển thị `HH:mm:ss`, dưới một giờ hiển thị `MM:ss`.

## 15. Luồng nghiệm thu authoritative sau refactor (2026-08-09)

Phần này thay thế các bước cũ nếu có mâu thuẫn.

### Truy cập POS bằng URL

1. Chưa mở ca, mở trực tiếp `/order`: React chưa render route nghiệp vụ, gọi `/api/v1/pos/session/current`, rồi quay về StaffHub với thông báo chưa mở ca.
2. WorkShift `OPEN`: API trả `accessMode=ACTIVE`; bán hàng/history/inventory/notification được phép.
3. Sau khi đóng ca hoặc chuyển `RECONCILIATION_REQUIRED`: session thành `WORKSHIFT_ENDED`; refresh hoặc nhập lại URL bị từ chối.
4. Thiếu `App.POS`, sai store, terminal inactive/chưa approved hoặc WorkShift không thuộc đúng staff/store/terminal: backend trả 401/403 và UI không render POS.
5. `OPENING_CASH` và `PENDING_CLOSE` chỉ vào `/shift`; chưa mở ca không được chuyển tác vụ.

### Lịch đổi khi modal đang mở

1. Mở modal và ghi nhận `assessmentVersion` từ preview.
2. Manager đổi giờ của chính `StaffShift`, đổi Shift template hoặc hủy lịch.
3. Requester bấm request OTP/approval/tiếp tục POS; frontend gửi version cũ.
4. Backend đọc DB, trả HTTP 409 `SHIFT_SCHEDULE_CHANGED` cùng assessment mới; OTP/approval không bị consume, WorkShift không được tạo.
5. Modal giữ mở, hiện **“Lịch làm việc của bạn vừa được thay đổi. Vui lòng kiểm tra lại lịch mới trước khi mở ca.”**, thay dữ liệu mới và bắt xác nhận lại.

### Mở sớm, trễ và ngoài lịch

- Trễ trên 30 phút đi theo luồng yêu cầu Manager, không dùng OTP; boundary chi tiết vẫn là 30–45 phút và trên 45 phút như dưới đây.
- Trong 30 phút trước giờ bắt đầu: được tiếp tục theo `WITHIN_SCHEDULE`.
- Sớm hơn 30 phút: nhập lý do 10–500, gửi OTP cho approver có `POS.WorkShift.ApproveOutsideSchedule`, xác nhận rồi mới được phát ticket.
- Trễ đến 15 phút: mở và audit. Trễ 16–29 phút: nhập lý do 10–500. Trễ 30–45 phút: Manager quyết định với lý do. Trễ trên 45 phút: chỉ reject/convert outside.
- Ngoài lịch: requester phải có `POS.WorkShift.OpenOutsideSchedule`, nhập lý do 10–500 và dùng OTP đúng scope.
- Mọi trường hợp chỉ tạo WorkShift sau khi POS xác nhận tiền đầu ca. Việc chỉ mở/đóng modal không đổi trạng thái ca.

### Nhập OTP/PIN và khóa OTP

1. Sáu ô tự chuyển focus; Backspace ở ô rỗng quay lại ô trước; paste chia tối đa sáu ký tự.
2. OTP normalize uppercase và chỉ nhận `ABCDEFGHJKLMNPQRSTUVWXYZ23456789`. PIN chỉ nhận số; các PIN yếu hiện hành vẫn bị chặn.
3. Nhập OTP sai lần thứ 3: backend trả deadline khóa 120 giây; verify và **Gửi lại OTP** bị khóa. Countdown phải giữ đúng sau refresh, đóng/mở modal hoặc login lại.
4. Khi `00:00`, bấm resend để rotate mã và reset attempts; mã cũ không còn dùng được. Rate limit 15 phút vẫn kiểm riêng.
5. **Copy OTP** chỉ có tại notification sau khi người duyệt đã bấm **Xem OTP** hợp lệ. Clipboard chỉ nhận sáu ký tự, không whitespace; hiện **“Đã sao chép mã OTP”**.

### Xác nhận Terminal

1. Requester gửi đăng ký; challenge/notification là `PENDING_APPROVAL`, chưa có `PosTerminal` active.
2. Manager nhận notification, bấm **Xem OTP**, kiểm store/terminal/requester rồi bấm **Xác nhận Terminal**.
3. Backend kiểm recipient, `POS.WorkShift.OverrideTerminal`, StaffScope, challenge/action/state/OTP và commit transaction; success trả `terminalId`, `APPROVED`, `alreadyProcessed`.
4. Double-click bị disable ở UI; request thứ hai vẫn nhận kết quả idempotent, không tạo Terminal trùng.
5. Staff hoặc Manager sai store nhận 403 theo `TERMINAL_APPROVAL_FORBIDDEN`/`TERMINAL_STORE_SCOPE_INVALID`. Challenge không pending nhận 409; không tìm thấy nhận 404.
6. Nếu API lỗi, button luôn thoát loading trong `finally` và UI hiển thị thông báo theo `errorCode`, không parse message.

### Checklist nghiệm thu cuối

- POS direct URL: chưa mở/đã đóng/thiếu permission/terminal sai đều DENY; WorkShift `OPEN` ALLOW.
- Schedule cùng ID nhưng đổi giờ phải trả `SHIFT_SCHEDULE_CHANGED`.
- Boundary sớm `-30` phút ALLOW, `-31` phút cần override; late kiểm đủ 15/30/45.
- OTP kiểm input, paste, normalize, ký tự dấu/đặc biệt, lock 120 giây, refresh và resend rotate.
- Terminal kiểm success, 403 staff, 403 wrong store, expired/locked OTP, double request và loading reset.
- Audit không chứa OTP/PIN; unique active staff/terminal và rowversion phải giữ đúng khi race.

## 16. Xác nhận/từ chối Terminal và quyền button StaffHub

### Người gửi tại StaffHub

1. Button **Mở POS** chỉ xuất hiện khi có `App.POS` và `POS.WorkShift.Open`.
2. Button **Đăng ký terminal** chỉ xuất hiện khi có `App.POS` và `POS.Terminal.RequestRegistration`.
3. Khối **Thiết lập/Đổi PIN** chỉ xuất hiện khi có `App.POS` và `POS.Operator.ManageOwnPin`.
4. Sau khi người duyệt xác nhận, StaffHub nhận realtime, hiện SweetAlert **Terminal đã được xác nhận**, rồi tự reload để Terminal mới xuất hiện.
5. Nếu Chủ doanh nghiệp hoặc Quản lý chi nhánh từ chối, StaffHub hiện SweetAlert cảnh báo, tự reload và cho phép gửi yêu cầu mới cho chính Terminal ID của thiết bị; không sinh một thiết bị giả khác.

Ẩn button chỉ là UX. Gọi trực tiếp endpoint khi thiếu permission vẫn nhận 403.

### Người duyệt tại Admin Thông báo

- Chủ doanh nghiệp và Quản lý chi nhánh có `POS.WorkShift.RejectTerminal` nhìn thấy **Từ chối đăng ký Terminal**.
- Nhập lý do cụ thể từ 10 đến 500 ký tự; UI có counter và xác nhận SweetAlert trước khi gửi.
- Reject không cần xem hoặc nhập OTP. Backend vẫn kiểm notification recipient, permission, StaffScope, store và trạng thái challenge.
- Xác nhận thành công tạo `PosTerminal`; từ chối chuyển challenge sang `REJECTED` và tuyệt đối không tạo Terminal.
- Double-click bị khóa ở UI; backend xử lý lặp idempotent/concurrency có kiểm soát.

Ma trận SeedAll mặc định:

| Quyền | Chủ DN | QL vùng | QL chi nhánh | NV bán hàng | Ca trưởng |
|---|---:|---:|---:|---:|---:|
| `POS.Terminal.RequestRegistration` | 0 | 0 | 1 | 1 | 1 |
| `POS.Operator.ManageOwnPin` | 0 | 0 | 1 | 1 | 1 |
| `POS.Operator.Switch` | 0 | 0 | 1 | 1 | 1 |
| `POS.WorkShift.OverrideTerminal` | 1 | 1 | 1 | 0 | 0 |
| `POS.WorkShift.RejectTerminal` | 1 | 0 | 1 | 0 | 0 |

### Các bước test mới cho nút từ chối Terminal

#### Chuẩn bị

1. Tạo một yêu cầu đăng ký Terminal mới tại StaffHub và giữ challenge ở trạng thái `Waiting`/`PENDING`.
2. Mở hai cửa sổ: requester tại StaffHub và người duyệt tại `/Admin/AdminNotifications`.
3. Dùng Chủ doanh nghiệp hoặc Quản lý chi nhánh thuộc đúng Store để test từ chối. Không dùng challenge đã hết hạn, đã xác nhận hoặc đã từ chối cho ca test success.
4. Mở DevTools → Network, lọc `RejectTerminal`; xóa request cũ trước mỗi ca test.

#### Test validation lý do và lỗi toast lặp

1. Nhập lần lượt từ 1 đến 9 ký tự vào **Lý do từ chối đăng ký Terminal**.
2. Xác nhận counter tăng đúng sau mỗi ký tự.
3. Trong lúc gõ không được xuất hiện toast “Vui lòng kiểm tra và nhập đầy đủ các trường bắt buộc”.
4. Bấm **Từ chối đăng ký Terminal** khi lý do còn dưới 10 ký tự.
5. Kết quả đúng: Network không có `RejectTerminal`; lỗi “Lý do từ chối phải có từ 10 đến 500 ký tự…” xuất hiện cạnh textarea; textarea được focus; không có toast lặp.
6. Nhập thêm cho đủ ít nhất 10 ký tự. Lỗi inline phải được xóa khi người dùng sửa nội dung.

#### Test SweetAlert, gửi request và chống double-click

1. Với lý do hợp lệ, bấm nút từ chối: phải xuất hiện SweetAlert xác nhận; lúc này chưa gửi API.
2. Chọn **Quay lại**: không có request, nút trở lại trạng thái bình thường và có thể bấm lại.
3. Bấm lại rồi chọn **Từ chối** trong SweetAlert.
4. Kết quả đúng: Network có đúng một `POST /Admin/AdminNotifications/RejectTerminal`; request gồm `id`, `Reason` đã trim, `RequestKey` và antiforgery token.
5. Double-click nhanh vào nút hoặc bấm nhiều lần trong lúc xử lý không được tạo request thứ hai; nút chuyển `Đang xử lý…`/`Đang từ chối…` và bị disable.
6. Thành công phải hiện SweetAlert **Đã từ chối**, sau đó trang Admin reload. Notification không còn action confirm/reject.

#### Test StaffHub và dữ liệu authoritative

1. Quan sát cửa sổ requester sau khi người duyệt từ chối.
2. StaffHub phải nhận `TerminalRegistrationChanged`, hiện SweetAlert yêu cầu đã bị từ chối rồi tự reload.
3. Sau reload, requester có thể gửi challenge mới cho cùng Terminal ID của thiết bị; challenge cũ không được tái sử dụng và không được sinh Terminal ID khác.
4. Kiểm tra DB test: challenge cũ là `Rejected`, `ProtectedOtpPayload` đã bị xóa, notification đã resolve và không có `PosTerminal` active được tạo từ challenge đó.
5. Audit phải có `POS_TERMINAL_REGISTRATION_REJECTED`, actor/store/terminal/reason nhưng tuyệt đối không chứa OTP plaintext.

#### Test lỗi và phân quyền

- Quản lý vùng, Nhân viên bán hàng hoặc tài khoản không có `POS.WorkShift.RejectTerminal`: không thấy nút; direct POST phải trả 403.
- Chủ doanh nghiệp và Quản lý chi nhánh đúng StaffScope: thấy nút và có thể từ chối.
- Quản lý chi nhánh Store A xử lý notification Store B: HTTP 403, UI hiển thị lỗi scope và luôn thoát loading.
- Challenge đã `USED`: HTTP 409 `TERMINAL_ALREADY_APPROVED`, không được đổi sang `REJECTED`.
- Challenge đã `REJECTED`: request lặp trả kết quả idempotent, không tạo audit/Terminal trùng.
- Mô phỏng API 400/403/409/500 hoặc mất mạng: feedback phải nằm cạnh form, nút được bật lại trong `finally`, không reload và không loading vô hạn.

#### Regression nút xác nhận Terminal

1. Tạo challenge mới, bấm **Xem OTP** và xác nhận sáu ô được điền đồng bộ.
2. Bấm **Xác nhận Terminal**; Network phải có đúng một `POST ConfirmTerminal`, không bị textarea từ chối hoặc mutation guard chặn.
3. Thành công tạo đúng một `PosTerminal`, challenge chuyển `USED`; StaffHub hiện SweetAlert xác nhận và tự reload.

## 17. Đăng ký và nhận biết Terminal theo từng thiết bị

### Quy tắc người dùng cần hiểu

- Terminal thuộc cửa hàng, không thuộc riêng tài khoản nhân viên.
- Một nhân viên có thể đăng ký nhiều thiết bị vật lý, nhưng mỗi yêu cầu phải được tạo trên chính thiết bị sẽ chạy POS.
- Mỗi trình duyệt giữ một Terminal ID ổn định. Gửi lại sau khi bị từ chối, hết hạn hoặc hủy vẫn dùng ID cũ.
- Trong cùng một thời điểm, một nhân viên chỉ được có một yêu cầu đăng ký Terminal đang chờ. Muốn đăng ký thiết bị thứ hai phải hoàn tất hoặc hủy yêu cầu đang chờ trước.
- Dòng trạng thái trên StaffHub chỉ giúp nhận biết thiết bị. Backend vẫn là nơi quyết định Terminal có active, đúng Store, đúng quyền và có ca hợp lệ hay không.

### Thiết bị chưa liên kết

1. Đăng nhập StaffHub trên thiết bị sẽ dùng làm POS.
2. Card Terminal hiển thị badge **Chưa liên kết** và nút **Đăng ký thiết bị này**.
3. Mở modal, nhập tên dễ nhận biết như `POS-QUAY-01`, rồi gửi yêu cầu.
4. StaffHub tạo một Terminal ID cho trình duyệt và giữ nguyên ID đó qua reload. Card chuyển **Đang chờ xác nhận**.
5. Không xóa dữ liệu trình duyệt, không mở thiết bị khác để gửi cùng lúc và không bấm gửi lặp nhằm tạo Terminal mới.

### Theo dõi trạng thái yêu cầu

| Badge/trạng thái | Ý nghĩa và thao tác |
|---|---|
| **Đang chờ** | Người có quyền cần xác nhận OTP tại Admin Thông báo |
| **Đang xử lý** | OTP đã được duyệt, chờ danh sách Terminal active đồng bộ |
| **Tạm khóa** | OTP bị khóa; chờ hết cooldown hoặc hủy theo nghiệp vụ |
| **Bị từ chối** | Xem lại tên/lý do và gửi lại cho cùng thiết bị |
| **Hết hạn** | Gửi lại để nhận OTP mới; Terminal ID không đổi |
| **Đã hủy** | Có thể đăng ký lại cùng thiết bị |
| **Thiết bị khác** | Tài khoản đang có yêu cầu pending trên thiết bị khác; hoàn tất/hủy yêu cầu đó trước |
| **Đã kích hoạt** | Thiết bị đã sẵn sàng; có thể tiếp tục luồng mở ca/POS |
| **Không khả dụng** | Terminal liên kết không còn active; liên kết Terminal active khác hoặc liên hệ quản lý |
| **Sai cửa hàng** | Thiết bị đang liên kết Store khác; không được sử dụng tại Store hiện tại |

Sau khi Manager xác nhận hoặc từ chối, StaffHub hiện SweetAlert rồi reload đúng một lần. Sau reload phải đọc lại challenge/Terminal từ server để dựng card, không lấy kết quả SweetAlert hoặc `localStorage` làm bằng chứng approval.

### Liên kết Terminal đã có

Áp dụng khi Terminal đã được duyệt từ trước nhưng trình duyệt chưa có liên kết, hoặc dữ liệu trình duyệt vừa bị xóa:

1. Tại card **Chưa liên kết**, chọn **Liên kết Terminal đã có**.
2. Danh sách chỉ được chứa Terminal active của cửa hàng hiện tại.
3. Chọn đúng Terminal vật lý, kiểm tra tên và xác nhận SweetAlert.
4. StaffHub lưu liên kết trên trình duyệt và hiển thị **Thiết bị đã sẵn sàng** nếu Terminal vẫn active.
5. Liên kết này không tạo Terminal, không đổi quyền và không bỏ qua bước mở ca. Nếu không chắc Terminal nào thuộc thiết bị, dừng lại và liên hệ quản lý.

Khi thiết bị ở trạng thái sẵn sàng, modal mở POS cố định Terminal đã liên kết; người dùng không chọn Terminal khác và không thấy nút tạo thêm Terminal trên cùng trình duyệt.

### Đăng ký thêm một thiết bị vật lý

1. Hoàn tất hoặc hủy mọi yêu cầu pending của tài khoản.
2. Đăng nhập StaffHub trên trình duyệt của thiết bị mới.
3. Thiết bị mới phải hiện **Chưa liên kết**; chọn **Đăng ký thiết bị này**.
4. Thực hiện luồng OTP/approval bình thường. Thiết bị cũ đã `READY` vẫn giữ nguyên liên kết và tiếp tục sử dụng được.

Không đăng ký Terminal thứ hai bằng cách bấm lại trên thiết bị đã `READY`. Nếu cần thay liên kết do Terminal cũ inactive, dùng **Liên kết Terminal đã có** hoặc liên hệ quản lý.

### Các bước test sau thay đổi

#### Test định danh ổn định và gửi lại

1. Xóa riêng record `cafechain.staffhub.pos-terminal-device.v1` trên một browser test chưa dùng; reload StaffHub.
2. Xác nhận card là **Chưa liên kết**. Gửi yêu cầu và ghi Terminal ID hiển thị.
3. Reload, đóng/mở modal và gọi state restore; Terminal ID phải giữ nguyên, trạng thái là **Đang chờ**.
4. Lần lượt tạo các ca `REJECTED`, `EXPIRED`, `CANCELLED`, sau đó bấm gửi lại.
5. Network phải gửi cùng Terminal ID; challenge mới được tạo nhưng không sinh ID thiết bị mới.

#### Test approve và thiết bị đã sẵn sàng

1. Manager xác nhận challenge hợp lệ tại Admin Thông báo.
2. StaffHub requester phải hiện SweetAlert xác nhận và reload đúng một lần.
3. Sau reload, card hiển thị **Thiết bị đã sẵn sàng**, đúng tên/Store/Terminal ID.
4. Mở modal đăng ký: không được tạo Terminal thứ hai trên cùng browser.
5. Mở POS: Terminal picker bị cố định đúng Terminal liên kết; backend vẫn từ chối nếu thiếu quyền, chưa mở ca hoặc Terminal vừa bị inactive.

#### Test nhiều thiết bị và challenge không ghi đè

1. Trên thiết bị A tạo yêu cầu và giữ `PENDING`.
2. Đăng nhập cùng nhân viên trên thiết bị B; state phải là **Có yêu cầu ở thiết bị khác**, không được gửi song song.
3. Hoàn tất/hủy A, sau đó B mới được đăng ký Terminal ID riêng.
4. Khi A đã `READY` và B đang pending, reload A: A vẫn `READY`; challenge B không ghi đè Terminal ID của A.

#### Test liên kết Terminal cũ và bảo mật

1. Dùng browser chưa liên kết, mở **Liên kết Terminal đã có**.
2. Xác nhận chỉ có Terminal active đúng Store; Terminal inactive hoặc Store khác không xuất hiện.
3. Chọn một Terminal, hủy SweetAlert: không lưu liên kết. Chọn lại và xác nhận: card chuyển `READY`, không có request tạo/update Terminal ở Network.
4. Sửa thủ công `localStorage` sang ID không tồn tại, inactive hoặc Store khác rồi reload.
5. UI phải hiện **Không khả dụng** hoặc **Sai cửa hàng**; direct URL/API POS vẫn bị backend từ chối.
6. Xóa dữ liệu trình duyệt: liên kết local mất nhưng Terminal backend vẫn còn; dùng liên kết một lần để khôi phục, không tạo duplicate ngoài ý muốn.

#### Test UI và tương thích API

1. Kiểm tra desktop/mobile: card, badge, icon, tên Terminal, Store, mô tả, focus và vùng feedback không tràn hoặc nhảy layout.
2. Khi gửi request, nút loading/disabled rõ ràng; mọi nhánh lỗi phải bật lại nút.
3. Theo dõi Console và Network; không có exception, không có request lặp và không có reload loop.
4. Xác nhận ba endpoint vẫn giữ contract: `RequestTerminalRegistrationOtp`, `GetTerminalRegistrationOtpState`, `CancelTerminalRegistrationOtp`.
5. Xác nhận không có migration/schema/permission mới chỉ để phục vụ liên kết trình duyệt.
