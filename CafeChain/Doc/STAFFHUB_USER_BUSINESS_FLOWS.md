# Hướng dẫn kiểm thử nghiệp vụ StaffHub và POS

Nguồn quy tắc chuẩn: [STAFFHUB_POS_WORKSHIFT_BUSINESS_RULES.md](./STAFFHUB_POS_WORKSHIFT_BUSINESS_RULES.md).

Thẻ không có phân công phải hiển thị: **“Chưa có lịch — Thời gian nghỉ hoặc chưa được phân ca.”** `AppLauncher` và `AdminDashboardApp` chỉ điều hướng theo permission, không tự tạo WorkShift.

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

## 4. Ca mở sớm — tạo/bind tại StaffHub, tiền tại POS

1. Chuẩn bị lịch bắt đầu trong khoảng mở sớm cho phép; preview phải có `MinutesEarly>0`.
2. Bấm tiếp tục. StaffHub tạo WorkShift; `IssuePosToken` trả `workShiftId>0`, `requiresOpeningCash:true`.
3. POS phải hiển thị form tiền đầu phiên dù `GET current` đã thấy ID này; màn bán hàng chưa nhận giao dịch.
4. Nhập tiền và xác nhận. Response phải trả đúng ID StaffHub đã cấp, không sinh ID thứ hai.
5. Refresh rồi xác nhận lại phải bị từ chối/replay an toàn; không đổi tiền bằng payload khác.

## 5. Ca trễ

### Trễ cần lý do, chưa cần OTP

1. Chuẩn bị thời điểm vượt ngưỡng lý do nhưng chưa vượt ngưỡng OTP.
2. Preview `LATE_FOR_SCHEDULE`; nhập lý do 10–500 ký tự.
3. StaffHub tạo/bind WorkShift; POS chỉ nhập tiền. DB lưu đúng lịch nguồn và lý do nghiệp vụ.

### Trễ cần OTP

1. Chuẩn bị trễ trên ngưỡng phê duyệt; preview phải hiện cụm OTP và nút sang POS bị khóa.
2. Nhập lý do, bấm **Gửi OTP** một lần. SweetAlert2 báo thành công; nút gửi đầu tiên ẩn, terminal/lý do khóa.
3. Lấy OTP từ inbox ca trưởng. Nhập sai một lần: HTTP 400, `OTP_INVALID`, form còn và focus tại input.
4. Nhập đúng: HTTP 200; SweetAlert2 thành công; input, **Xác nhận OTP**, **Gửi lại OTP** đều ẩn; hiện `✓ OTP đã được xác nhận`.
5. Bấm sang POS, nhập tiền; xác nhận cùng WorkShiftId đã tạo tại StaffHub.

## 6. Ngoài lịch và hạn 6 giờ

1. Bảo đảm nhân viên không có lịch hiệu lực; chọn terminal trống.
2. Preview `OUTSIDE_SCHEDULE`, hiển thị hạn dự kiến; nhập lý do và hoàn tất OTP.
3. StaffHub tạo/bind ID; POS nhập tiền và kích hoạt giao dịch.
4. So `Date.parse(autoCloseAtUtc)-Date.parse(startTimeUtc)` hoặc SQL: phải đúng `21600` giây.
5. Ở múi giờ Việt Nam, UI hiển thị giờ địa phương đúng nhưng wire vẫn có `Z`; không được báo hết hạn ngay sau mở.

Quy tắc DB bắt buộc: `AutoCloseAtUtc = StartTimeUtc + 6 giờ`.

## 7. OTP reload, resend, expired và lock

- **Reload Pending:** gửi OTP, reload StaffHub, mở lại modal. `GetOpenPosOtpState` trả challenge của chính requester/store; countdown tiếp tục, terminal/lý do bị khóa.
- **Reload Approved:** xác nhận đúng rồi reload. Cụm nhập/xác nhận/gửi lại vẫn ẩn; nút sang POS bật.
- **Resend:** trước cooldown nút disabled; hết cooldown bấm được, SweetAlert2 báo mã mới, mã cũ vô hiệu.
- **Expired:** để quá TTL; verify trả HTTP 410 + `OTP_EXPIRED`, control vô hiệu.
- **Locked:** nhập sai đến giới hạn; lần cuối HTTP 423 + `OTP_VERIFICATION_LOCKED`.
- **Already used:** verify challenge đã approved/used trả HTTP 409 + `OTP_ALREADY_USED`.
- **Context mismatch:** dùng public ID của staff/store khác trả HTTP 409 + `OTP_CONTEXT_MISMATCH`; response không có OTP/hash/protected payload.
- **Rate limit:** vượt ngưỡng trả HTTP 429 + `OTP_RATE_LIMITED`.

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
5. Bấm **Lưu PIN**. Sau thành công PIN không được hiển thị lại, gửi email, lưu local storage hoặc xuất hiện trong log/DB dạng rõ; DB chỉ có BCrypt hash.

### Đổi Current Operator trên ca của người khác

1. Giữ ca của A=`salesstaff` đang `OPEN` trên `POS-T1`; không đóng và không đăng xuất POS tại terminal này.
2. Tại POS hiện tại chọn **Đổi Current Operator**; chọn/nhập nhân viên B=`salesstaff2` và PIN cá nhân của B.
3. Xác nhận header/operator đổi sang B nhưng `WorkShiftId`, `WorkShift.UserId`, tiền đầu ca và người chịu trách nhiệm két vẫn là A.
4. Tạo order; DB phải ghi B là người thao tác order. Sai PIN tăng bộ đếm/khóa đúng chính sách và không đổi operator.

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
