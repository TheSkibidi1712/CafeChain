# Phân tích luồng nghiệp vụ người dùng StaffHub

## 1. Mục đích và nguồn quy tắc

Tài liệu này mô tả StaffHub theo từng tình huống mà nhân viên và quản lý gặp trong thực tế. Mỗi luồng nêu rõ giao diện hiển thị gì, backend kiểm tra gì, kết quả dữ liệu và cách xử lý khi thất bại.

Nguồn quy tắc chuẩn là [Nghiệp vụ StaffHub và WorkShift](./STAFFHUB_POS_WORKSHIFT_BUSINESS_RULES.md). Nếu có mâu thuẫn, tài liệu quy tắc chuẩn và cấu hình `WorkShiftOptions` trong mã nguồn được ưu tiên.

Ba khái niệm phải tách biệt:

- `Shift`: mẫu giờ dự kiến của cửa hàng.
- `StaffShift`: lịch dự kiến đã phân cho nhân viên.
- `WorkShift`: phiên chịu trách nhiệm POS/két.

Việc mở POS không chứng minh nhân viên có mặt, không tạo giờ công và không dùng tính lương.

## 2. Tổng quan luồng từ StaffHub sang POS

```text
Đăng nhập
→ Kiểm tra App.StaffHub
→ Hiển thị hồ sơ và lịch dự kiến
→ Nhân viên chọn Mở POS
→ StaffHub POST PreviewOpenPos kèm anti-forgery token
→ Backend đánh giá lịch mà không tạo terminal, OTP, exchange code hoặc WorkShift
→ WITHIN_SCHEDULE: StaffHub gọi IssuePosToken ngay
→ LATE_FOR_SCHEDULE / OUTSIDE_SCHEDULE: StaffHub hiện modal xác nhận
→ Hủy: đóng modal, không phát exchange code
→ Tiếp tục sang POS: StaffHub POST IssuePosToken
→ Backend phát mã exchange dùng một lần
→ Trình duyệt chuyển mã qua URL fragment
→ POS xóa fragment và POST đổi mã lấy phiên xác thực
→ POS gọi open-assessment
→ Backend phân loại WITHIN_SCHEDULE / LATE_FOR_SCHEDULE / OUTSIDE_SCHEDULE
→ Thu thập tiền đầu phiên, lý do và OTP khi cần
→ Backend kiểm tra lại trong transaction
→ Tạo WorkShift hoặc trả mã lỗi ổn định
```

JWT không được truyền bằng query string. `StaffId`, `StoreId`, thời gian mở và thời hạn phiên không lấy từ client.

### 2.1 Ma trận tài khoản dùng để kiểm thử

Không ghi mật khẩu vào tài liệu. Người kiểm thử dùng mật khẩu seed của môi trường hoặc đặt lại qua cơ chế quản trị.

| Tài khoản | Vai trò kiểm thử | Phiên trình duyệt | Dùng trong luồng |
|---|---|---|---|
| `salesstaff@cafechain.vn` | Người yêu cầu; xem StaffHub, mở/đóng phiên của mình, mở ngoài lịch | Trình duyệt A hoặc cửa sổ thường | Hầu hết luồng mở POS |
| `shiftsupervisor@cafechain.vn` | Người duyệt ưu tiên số 1 trong đúng cửa hàng | Trình duyệt B hoặc cửa sổ ẩn danh | Nhận Gmail, badge và OTP realtime |
| `storemanager@cafechain.vn` | Người duyệt dự phòng, kiểm thử Dashboard và đóng ngoại lệ | Trình duyệt C | Fallback khi Ca trưởng inactive/mất quyền |
| Tài khoản Quản lý vùng | Người duyệt dự phòng theo `StaffScope` | Phiên riêng | Kiểm thử nhiều cửa hàng |
| Tài khoản Chủ doanh nghiệp | Người duyệt dự phòng theo scope doanh nghiệp | Phiên riêng | Fallback cuối nhóm vai trò chuẩn |
| Tài khoản Kế toán/kho | Có thể xem StaffHub/Dashboard nhưng không mặc định mở POS | Phiên riêng | Kiểm thử ẩn nút và 403 |
| Tài khoản SystemAdmin | Không mặc định duyệt nghiệp vụ WorkShift | Phiên riêng | Kiểm thử không bypass permission |
| Tài khoản Khách hàng | Không được vào StaffHub | Phiên riêng | Kiểm thử authorization |

Thứ tự chọn người duyệt là Ca trưởng → Quản lý chi nhánh → Quản lý vùng → Chủ doanh nghiệp → tài khoản khác thực sự có permission. Thứ tự không thay thế kiểm tra account/staff active, email, permission và store scope.

## 3. Bảng quyết định phân loại mở POS

| Tình huống theo giờ cửa hàng | Phân loại | Lý do | OTP | Thời hạn |
|---|---|---:|---:|---|
| Từ 30 phút trước giờ bắt đầu đến 15 phút sau giờ bắt đầu | `WITHIN_SCHEDULE` | Không | Không | Không áp dụng giới hạn ngoài lịch |
| Sau 15 phút kể từ giờ bắt đầu, nhưng không quá 30 phút sau giờ kết thúc | `LATE_FOR_SCHEDULE` | Có | Khi trễ trên 30 phút | Không áp dụng giới hạn ngoài lịch |
| Không có lịch hợp lệ, lịch bị hủy, sai store, mở quá sớm hoặc quá muộn | `OUTSIDE_SCHEDULE` | Có, 10–500 ký tự | Có | `StartTimeUtc + 6 giờ` |

Ranh giới được tính bằng thời gian server. Lịch ứng viên phải bao gồm ngày trước, ngày hiện tại và ngày tiếp theo để không bỏ sót ca qua đêm.

## 4. Bảng trạng thái WorkShift

| Trạng thái | Tạo order/payment mới | Ý nghĩa | Chuyển tiếp hợp lệ |
|---|---:|---|---|
| `OPEN` | Có | Nhân viên đang chịu trách nhiệm terminal/két | `CLOSING`, `EXPIRED_PENDING_CLOSE`, `CLOSED` nếu phiên rỗng hết hạn |
| `CLOSING` | Không | Đã bắt đầu chốt, đang kiểm tra giao dịch và tiền | `CLOSED` hoặc `RECONCILIATION_REQUIRED` |
| `EXPIRED_PENDING_CLOSE` | Không | Phiên ngoài lịch đã đủ 6 giờ và cần kiểm đếm | `CLOSED` hoặc `RECONCILIATION_REQUIRED` |
| `RECONCILIATION_REQUIRED` | Không | Đã đóng ngoại lệ, còn dữ liệu cần đối soát | `CLOSED` sau reconcile |
| `CLOSED` | Không | Phiên đã hoàn tất | Không mở lại phiên cũ |

## 5. Các luồng xem StaffHub

### 5.1 Đăng nhập và truy cập StaffHub

- **Điều kiện:** tài khoản đã xác thực, có `App.StaffHub` và claim `StaffId` hợp lệ.
- **StaffHub hiển thị:** họ tên, avatar, cửa hàng, lịch dự kiến của tuần và các nút theo permission.
- **Backend:** policy `StaffHubApp` chạy trước action; service chỉ lấy hồ sơ nhân viên active tương ứng claim.
- **Thất bại:** chưa đăng nhập thì challenge/login; thiếu `StaffId` hoặc hồ sơ không active thì không hiển thị dữ liệu StaffHub.
- **Kết quả dữ liệu:** chỉ đọc lịch, không tạo `StaffShift` hoặc `WorkShift`.

### 5.2 Không có permission StaffHub, POS hoặc Dashboard

- **Không có `App.StaffHub`:** bị từ chối ngay tại controller.
- **Có StaffHub nhưng không có `App.POS`:** xem được lịch nhưng không hiển thị nút **Mở POS**; POST trực tiếp vẫn bị policy từ chối.
- **Không có `App.AdminDashboard`:** không hiển thị nút **Dashboard**; việc tự nhập URL Dashboard vẫn bị backend từ chối.
- **AppLauncher:** luôn hiển thị cho người đã truy cập được StaffHub vì AppLauncher chỉ yêu cầu đăng nhập.
- **Dữ liệu:** không thay đổi permission và không tự cấp quyền từ giao diện.

### 5.3 Nhân viên có lịch bình thường

- **Điều kiện:** có `StaffShift` trong tuần, không bị hủy.
- **StaffHub hiển thị:** tên ca, ngày giờ bắt đầu và kết thúc tuyệt đối, ví dụ `02/08/2026 08:00 → 02/08/2026 16:00`.
- **Người dùng:** có thể chuyển tuần hoặc mở POS nếu có permission.
- **Backend:** chỉ đọc lịch cùng nhân viên/cửa hàng; giờ tùy chỉnh được ưu tiên nếu có đủ cặp bắt đầu/kết thúc.
- **Kết quả:** xem lịch không tạo WorkShift; WorkShift chỉ được tạo sau thao tác mở POS thành công.

### 5.4 Không có lịch

- **Điều kiện:** ngày đang xem không có `StaffShift`.
- **StaffHub hiển thị:** `Chưa có lịch — Thời gian nghỉ hoặc chưa được phân ca.`
- **Backend:** không tự sinh lịch thay thế và không xem việc trống lịch là lỗi.
- **Kết quả:** nếu không mở POS thì không có dữ liệu nào được tạo; nếu mở POS thì chuyển sang luồng 6.5.

### 5.5 Lịch đã bị hủy

- **StaffHub hiển thị:** ca vẫn có thể được liệt kê để người dùng biết lịch sử, nhưng có nhãn **Đã hủy** và không tính vào số ca active.
- **Backend khi assessment:** loại lịch hủy khỏi danh sách lịch hợp lệ.
- **Kết quả:** nếu không còn lịch hợp lệ khác, yêu cầu mở POS được phân loại `OUTSIDE_SCHEDULE`.
- **Không thực hiện:** không khôi phục lịch hủy và không tạo bản sao lịch.

### 5.6 Lịch qua đêm từ ngày hôm trước

- **Ví dụ:** `WorkDate = 02/08/2026`, ca `22:00–06:00`.
- **StaffHub hiển thị:** `02/08/2026 22:00 → 03/08/2026 06:00`, không chỉ hiển thị `22:00–06:00`.
- **Khi mở lúc 03/08/2026 02:00:** backend tìm cả lịch `WorkDate = 02/08/2026`, tạo khoảng thời gian tuyệt đối rồi mới so sánh.
- **Kết quả:** liên kết đúng `SourceStaffShiftId`; `BusinessDate = 02/08/2026` và không bị tách phiên khi qua nửa đêm.

## 6. Các luồng mở POS

### 6.1 Mở POS sớm trong giới hạn 30 phút

- **Điều kiện:** có lịch hợp lệ và thời gian server không sớm hơn 30 phút so với giờ bắt đầu.
- **StaffHub/action:** A bấm **Mở POS** → `POST /StaffHub/PreviewOpenPos`; response `WITHIN_SCHEDULE` nên StaffHub không hiện modal và gọi `POST /StaffHub/IssuePosToken`.
- **POS hiển thị:** assessment lại `WITHIN_SCHEDULE`, tên/thời gian lịch và trường tiền đầu phiên.
- **Lý do/OTP:** không yêu cầu.
- **Backend:** kiểm tra account, staff, store, terminal, permission, scope và xung đột trong transaction.
- **Kết quả:** tạo WorkShift `OPEN`, `OpenContext = WITHIN_SCHEDULE`, có `SourceStaffShiftId` và không có `AutoCloseAtUtc` theo quy tắc ngoài lịch.

### 6.2 Mở POS đúng lịch hoặc trong 15 phút sau giờ bắt đầu

- **StaffHub:** nút Mở POS chạy preview trước; vì kết quả `WITHIN_SCHEDULE`, trang phát exchange rồi chuyển POS mà không hiện modal cảnh báo.
- **POS:** assessment trả `WITHIN_SCHEDULE`; không hiển thị form lý do/OTP.
- **Backend:** dùng thời gian server và liên kết lịch nguồn.
- **Kết quả:** WorkShift `OPEN`, ghi audit `WORKSHIFT_OPENED` và phát notification sau commit.

### 6.3 Mở trễ trên 15 phút nhưng không quá 30 phút

- **Phân loại:** `LATE_FOR_SCHEDULE`.
- **StaffHub/form:** sau `PreviewOpenPos`, hiện modal **Mở POS trễ so với lịch** với nhân viên, cửa hàng, giờ server, khoảng lịch và cảnh báo lý do. Modal không có ô nhập tiền/lý do/OTP.
- **Action:** **Hủy** không tạo exchange; **Tiếp tục sang POS** gọi `IssuePosToken` và khóa double-click.
- **POS hiển thị:** lịch nguồn, số phút trễ và trường lý do.
- **Lý do:** bắt buộc, được trim và kiểm tra độ dài/nội dung.
- **OTP:** chưa bắt buộc khi số phút trễ không vượt 30.
- **Kết quả:** tạo WorkShift `OPEN`, `OpenContext = LATE_FOR_SCHEDULE`, giữ `SourceStaffShiftId`; audit có thời gian dự kiến, thời gian mở, số phút trễ và lý do.

### 6.4 Mở trễ trên 30 phút hoặc sau giờ kết thúc trong grace 30 phút

- **Điều kiện:** vẫn không muộn hơn `PlannedEnd + 30 phút`.
- **Phân loại:** `LATE_FOR_SCHEDULE`.
- **POS hiển thị:** lý do và quy trình OTP phê duyệt.
- **OTP:** action `OPEN_SHIFT_LATE`, bind requester, approver, store, terminal, lịch và payload.
- **Backend:** kiểm tra lại permission/scope người duyệt khi verify và consume.
- **Thất bại:** thiếu OTP trả `LATE_OPENING_REQUIRES_OTP`; OTP không khớp payload trả `OTP_CHALLENGE_PAYLOAD_MISMATCH`.
- **Kết quả:** sau phê duyệt mới tạo WorkShift; không sửa lịch nguồn.

### 6.5 Không có lịch nhưng nhân viên chọn Mở POS

- **Tài khoản/phiên:** Trình duyệt A đăng nhập `salesstaff@cafechain.vn`; trình duyệt B đăng nhập `shiftsupervisor@cafechain.vn` và giữ trang có chuông notification đang online.
- **Dữ liệu chuẩn bị:** A và B active, cùng Store 1; A không có lịch hợp lệ; terminal đã đăng ký/active; A có `App.POS`, `POS.WorkShift.Open`, `POS.WorkShift.OpenOutsideSchedule`; B có `POS.WorkShift.ApproveOutsideSchedule` và đúng scope.
- **Form trước thao tác:** StaffHub của A hiển thị `Chưa có lịch — Thời gian nghỉ hoặc chưa được phân ca.` và nút **Mở POS**. Chưa có trường tiền, lý do hoặc OTP.
- **Action 1:** A bấm **Mở POS**. StaffHub gọi `POST /StaffHub/PreviewOpenPos` với anti-forgery token.
- **Response/form mới:** backend trả `OUTSIDE_SCHEDULE`; StaffHub hiện modal **Mở POS ngoài lịch** gồm nhân viên, cửa hàng, giờ server, hạn dự kiến sau 6 giờ, cảnh báo cần lý do/OTP và tuyên bố không tạo lịch/chấm công. Modal chỉ có **Hủy** và **Tiếp tục sang POS**.
- **Action 2:** nếu A bấm **Hủy**, dialog đóng và không có exchange code. Nếu bấm **Tiếp tục sang POS**, StaffHub gọi `POST /StaffHub/IssuePosToken`, disable nút chống double-click rồi điều hướng bằng one-time exchange code trong URL fragment.
- **Form POS:** POS đổi exchange code qua `POST /api/v1/pos/session/exchange`, đánh giá lại bằng `POST /api/v1/pos/shifts/open-assessment`, sau đó mới hiển thị tiền đầu phiên, terminal và khối ngoài lịch gồm lý do, **Gửi OTP**, ô mã OTP và **Xác nhận**.
- **Action 3:** A nhập tiền đầu phiên, lý do 10–500 ký tự và bấm **Gửi OTP**. Backend chọn B theo ưu tiên, tạo `OtpChallenge` cùng `StaffNotification` trong một transaction.
- **Hai kênh:** cùng một mã được gửi vào Gmail của B và event riêng `OperationalOtpIssued` tới group `staff:{StaffId B}:operational-notifications`. Chuông B tăng badge; nếu B online, popup hiện mã, hạn dùng và nút sao chép.
- **Lưu trữ an toàn:** hàng `StaffNotifications` chỉ chứa requester, store, action, lý do và hạn dùng; `Title`, `Body`, audit và log không chứa mã OTP. `OtpChallenge` giữ BCrypt hash để verify và một payload Data Protection dạng ciphertext để đúng người duyệt xem lại mã còn hiệu lực trong chuông. Nếu B offline, Gmail vẫn là kênh dự phòng.
- **Action 4:** A nhập mã B cung cấp và xác nhận. Backend verify lại requester/approver/action/store/terminal/request key/payload, permission và scope; sau đó kiểm tra terminal/nhân viên trong transaction mở phiên.
- **Lý do:** bắt buộc 10–500 ký tự, không chấp nhận nội dung chỉ có khoảng trắng hoặc dấu câu.
- **OTP:** action `OPEN_SHIFT_OUTSIDE_SCHEDULE`; người duyệt phải có `POS.WorkShift.ApproveOutsideSchedule` và đúng store scope.
- **Backend:** khóa terminal/nhân viên, kiểm tra lại assessment và consume OTP trong transaction.
- **Kết quả:** tạo WorkShift độc lập `OPEN`, `OpenContext = OUTSIDE_SCHEDULE`, `SourceStaffShiftId = null`, `AutoCloseAtUtc = StartTimeUtc + 6 giờ`.
- **Audit/SignalR:** ghi lý do, approver, store, terminal, request key và phát sự kiện sau commit.
- **Không thực hiện:** không tạo `StaffShift`, không tạo dữ liệu chấm công và không hợp thức hóa lịch.
- **Hoàn nguyên test:** đóng WorkShift theo quy trình, resolve notification; nếu chỉ test preview thì bấm Hủy và không cần xóa dữ liệu.

### 6.6 Mở quá sớm, quá muộn hoặc lịch thuộc store khác

- **Assessment:** `OUTSIDE_SCHEDULE`, ngay cả khi có một lịch gần đó nhưng không còn nằm trong cửa sổ cho phép.
- **Xử lý:** giống luồng 6.5; lịch sai store không được dùng làm lịch nguồn.
- **Bảo mật:** thay `StoreId` ở request không mở rộng scope vì store được lấy từ identity và terminal.

### 6.7 Terminal chưa đăng ký, inactive hoặc sai cửa hàng

- **Chưa tồn tại:** backend trả `TERMINAL_NOT_FOUND`; không tự tạo terminal active từ GUID trình duyệt.
- **Đăng ký:** yêu cầu OTP action `REGISTER_POS_TERMINAL`; approver cần `POS.WorkShift.OverrideTerminal` trong đúng scope.
- **Inactive:** trả `TERMINAL_INACTIVE`; modal cũ không thể bỏ qua trạng thái mới của terminal.
- **Sai store:** trả `TERMINAL_STORE_MISMATCH` hoặc `STORE_SCOPE_DENIED`.
- **Kết quả:** chỉ sau khi terminal hợp lệ mới tiếp tục assessment/mở WorkShift.

### 6.8 Terminal hoặc nhân viên đã có WorkShift active

- **Backend:** kiểm tra trong transaction và được bảo vệ thêm bằng filtered unique index.
- **Terminal xung đột:** trả `TERMINAL_ALREADY_HAS_OPEN_SHIFT`.
- **Nhân viên xung đột:** trả `STAFF_ALREADY_HAS_OPEN_SHIFT`.
- **Giao diện:** tải lại WorkShift hiện tại, không tạo request key mới một cách tự động.
- **Audit:** ghi xung đột terminal/concurrency, không ghi thêm WorkShift.

### 6.9 Double-click, timeout và retry

- **Frontend:** disable nút trong lúc mutation; mỗi thao tác nghiệp vụ có một `RequestKey`.
- **Timeout:** giữ nguyên key và payload khi kiểm tra/retry.
- **Cùng key, cùng payload:** backend replay kết quả đã lưu, không insert lần hai.
- **Cùng key, khác payload:** trả `DUPLICATE_REQUEST`.
- **Đang xử lý:** trả trạng thái processing cho đến khi lease hết hạn hoặc thao tác hoàn tất.
- **Race condition:** trả `CONCURRENCY_CONFLICT` nếu rowversion/trạng thái đã thay đổi.

### 6.10 OTP sai, hết hạn, dùng lại hoặc mất quyền

- **Thời hạn:** 5 phút; một challenge chỉ dùng một lần.
- **Định dạng:** đúng 6 ký tự lấy ngẫu nhiên từ `ABCDEFGHJKLMNPQRSTUVWXYZ23456789`; mã có thể toàn chữ hoặc toàn số. Frontend/backend từ chối khoảng trắng nội bộ, ký tự đặc biệt, emoji, chữ có dấu và `O/0/I/1`.
- **Gửi lại:** sau lần gửi đầu phải chờ đủ 60 giây. Form đăng ký terminal và mở POS ngoài lịch/trễ hiển thị countdown; backend vẫn kiểm tra cooldown nếu client bị sửa.
- **Sai mã:** tối đa 3 lần/challenge; rate limit còn áp dụng theo staff, terminal, IP và device trong cửa sổ 15 phút.
- **Hết hạn/dùng lại:** trả `APPROVAL_EXPIRED` hoặc `APPROVAL_ALREADY_USED` theo giai đoạn xử lý.
- **Sai payload:** trả `OTP_CHALLENGE_PAYLOAD_MISMATCH`.
- **Approver mất quyền/sai scope:** trả `OTP_APPROVER_NO_LONGER_ELIGIBLE` hoặc `INVALID_APPROVER_SCOPE`.
- **Kết quả:** không tạo/cập nhật WorkShift; audit chỉ ghi metadata và kết quả, không ghi OTP rõ.

## 7. Hết hạn WorkShift ngoài lịch

### 7.1 Cảnh báo 30, 10 và 1 phút

- **Worker:** chạy mỗi phút, chỉ chọn WorkShift `OUTSIDE_SCHEDULE`, `OPEN` và có deadline.
- **Giao diện:** nhận SignalR theo store/terminal/staff và hiển thị countdown đã đồng bộ với `serverNowUtc`.
- **Chống lặp:** `ExpiryWarningLevel` bảo đảm mỗi mốc chỉ được audit/phát một lần.
- **Trạng thái:** vẫn `OPEN` trước deadline; nhân viên cần chuẩn bị chốt két.

### 7.2 Hết hạn và phiên hoàn toàn rỗng

- **Điều kiện đồng thời:** không có order, không có payment/offline/reconciliation, `StartingCash = 0` và không có dữ liệu cần kiểm đếm.
- **Backend:** worker lock hàng, kiểm tra lại và tự đóng.
- **Kết quả:** `Status = CLOSED`, `CloseType = AUTO_EMPTY_SHIFT`, ba giá trị tiền cuối bằng 0 và ghi `EndTimeUtc`.
- **Audit/SignalR:** sự kiện `AUTO_EMPTY_CLOSED`.

### 7.3 Hết hạn nhưng có tiền, order hoặc payment

- **Backend:** chuyển `OPEN → EXPIRED_PENDING_CLOSE`, ghi `ExpiredAtUtc` và khóa giao dịch mới.
- **POS hiển thị:** phiên đã hết hạn, chờ chốt két; yêu cầu chuyển sang quy trình đóng.
- **Order/payment mới:** backend trả `WORKSHIFT_EXPIRED`, `WORKSHIFT_PENDING_CLOSE` hoặc `WORKSHIFT_NOT_OPEN` tùy endpoint/trạng thái.
- **Payment đã khởi tạo:** callback hợp lệ vẫn được xử lý idempotent và giữ WorkShift cũ.
- **Không thực hiện:** không tự điền `ActualEndingCash` và không giả định tiền thực tế bằng tiền kỳ vọng.

### 7.4 SignalR mất kết nối hoặc server restart

- **SignalR:** chỉ là kênh cập nhật nhanh, không phải nguồn quyết định cuối.
- **Backend:** order/payment initiation kiểm tra `Status` và `AutoCloseAtUtc` trong transaction nên vẫn chặn sau deadline.
- **Restart:** deadline được lưu trong database; worker quét ngay khi khởi động lại.
- **Frontend:** polling `/current` khi reconnect/focus để đồng bộ trạng thái.

## 8. Đóng, chuyển trách nhiệm và đối soát

### 8.1 Đóng WorkShift thông thường

- **Bắt đầu đóng:** `start-closing` chuyển `OPEN/EXPIRED_PENDING_CLOSE → CLOSING` và khóa giao dịch mới.
- **POS hiển thị:** tiền đầu phiên, tổng tiền mặt hợp lệ, tiền kỳ vọng do backend tính và trường tiền thực tế.
- **Công thức hiện hành:** `ExpectedEndingCash = StartingCash + tổng payment tiền mặt hợp lệ`.
- **Chênh lệch:** backend tính `ActualEndingCash - ExpectedEndingCash`; khác 0 cần lý do, vượt ngưỡng cần OTP.
- **Kết quả:** `CLOSED`, ghi người/thời gian đóng, audit và giải phóng terminal.
- **Không tin client:** client không được quyết định tiền kỳ vọng hoặc chênh lệch.

### 8.2 Đóng bị chặn bởi payment pending hoặc đơn offline

- **Payment pending:** trả `PAYMENT_IN_PROGRESS` và hiển thị số blocker.
- **Offline chưa đồng bộ:** trả `OFFLINE_ORDERS_PENDING`.
- **Xử lý thông thường:** chờ payment hoàn tất hoặc retry sync; không chuyển đơn sang WorkShift khác.
- **Kết quả:** WorkShift chưa được `CLOSED`; nếu cần bàn giao khẩn cấp thì dùng luồng đóng ngoại lệ.

### 8.3 Đóng ngoại lệ

- **Điều kiện:** có `POS.WorkShift.CloseException`, lý do, OTP đúng action/scope và đã kiểm đếm tiền.
- **Backend:** lưu số đơn offline/payment blocker, approver, lý do và thời điểm đóng ngoại lệ.
- **Kết quả:** `RECONCILIATION_REQUIRED`, không nhận giao dịch mới; terminal chỉ được giải phóng sau bước bàn giao tiền bắt buộc.
- **Dữ liệu muộn:** vẫn mang `WorkShiftId` cũ.
- **Audit:** `WORKSHIFT_EXCEPTION_CLOSED` và metadata reconciliation; không ghi OTP rõ.

### 8.4 Đồng bộ muộn và reconcile

- **Offline/payment callback muộn:** cập nhật WorkShift gốc, không chuyển doanh thu sang phiên mới.
- **Người thao tác:** cần `POS.WorkShift.Reconcile`; OTP action/payload phải khớp khi chính sách yêu cầu.
- **Backend:** kiểm tra không còn blocker, manifest offline và số đơn đã sync khớp dữ liệu lúc đóng ngoại lệ.
- **Kết quả:** tính lại số liệu, ghi audit `WORKSHIFT_RECONCILED` và chuyển `CLOSED` khi đủ điều kiện.

### 8.5 Muốn tiếp tục bán sau 6 giờ

- **Không cho phép:** sửa `AutoCloseAtUtc`, gia hạn phiên cũ hoặc tiếp tục tạo order trên WorkShift hết hạn.
- **Luồng đúng:** chốt/đóng ngoại lệ phiên cũ → kiểm đếm → mở WorkShift ngoài lịch mới → lý do mới → OTP mới → deadline 6 giờ mới.
- **Tiền đầu phiên:** người nhận nhập số tiền lẻ thực tế đã kiểm đếm.

### 8.6 Chuyển trách nhiệm két sang nhân viên khác

```text
Khóa giao dịch phiên cũ
→ Hoàn tất payment và sync
→ Backend tính tiền kỳ vọng
→ Người giao kiểm đếm và đóng phiên cũ
→ Xác định doanh thu cần rút và tiền lẻ để lại
→ Người nhận kiểm đếm
→ Mở WorkShift mới
```

- Không đổi `UserId` của WorkShift cũ thành người mới.
- Không tự copy `PreviousActualEndingCash` thành `NextStartingCash`.
- Order/payment cũ giữ WorkShift cũ.

## 9. Điều hướng từ StaffHub

### 9.1 Quay về AppLauncher

- Nút **AppLauncher** luôn xuất hiện trong banner StaffHub.
- Route được sinh bằng Razor cho `AppLauncherController.Index`, không phụ thuộc trang trước trong browser history.
- AppLauncher tự tải danh sách ứng dụng theo permission hiện tại.

### 9.2 Quay về Dashboard

- Backend kiểm tra policy `AdminDashboardApp` khi dựng trang StaffHub.
- Chỉ khi policy thành công mới hiển thị nút **Dashboard**.
- Route dùng area `Admin`, controller `Dashboard`, action `Index`.
- Nút Dashboard chung của layout không hiển thị trùng khi đang ở StaffHub.
- Việc ẩn/hiện nút không thay thế authorization của `DashboardController`.

## 10. Ma trận permission theo thao tác

| Thao tác | Permission/policy tối thiểu | Kiểm tra scope |
|---|---|---:|
| Vào StaffHub | `App.StaffHub` / `StaffHubApp` | Theo hồ sơ nhân viên |
| Hiển thị/Mở POS | `App.POS`, `POS.WorkShift.Open` | Store từ claims/terminal |
| Mở ngoài lịch | `POS.WorkShift.OpenOutsideSchedule` | Store scope |
| Duyệt mở trễ/ngoài lịch | `POS.WorkShift.ApproveOutsideSchedule` | Store scope của approver |
| Đóng phiên | `POS.WorkShift.Close` | WorkShift/store scope |
| Đóng ngoại lệ | `POS.WorkShift.CloseException` | WorkShift/store scope |
| Reconcile | `POS.WorkShift.Reconcile` | WorkShift/store scope |
| Đăng ký/override terminal | `POS.WorkShift.OverrideTerminal` | Terminal/store scope |
| Vào Dashboard | `App.AdminDashboard` / `AdminDashboardApp` | Theo permission handler |

SystemAdmin không mặc định bỏ qua các permission nghiệp vụ WorkShift.

## 11. Mã lỗi và phản ứng giao diện

| Error code | Ý nghĩa | Phản ứng mong đợi |
|---|---|---|
| `POS_PERMISSION_REQUIRED` | Thiếu quyền POS/WorkShift | Dừng thao tác, không hiện form bỏ qua |
| `STORE_SCOPE_DENIED` | Ngoài store scope | Dừng và yêu cầu đăng nhập/kiểm tra phân quyền |
| `TERMINAL_NOT_FOUND` | Terminal chưa đăng ký | Mở luồng đăng ký có OTP |
| `TERMINAL_INACTIVE` | Terminal bị vô hiệu | Không cho mở; liên hệ người có quyền |
| `TERMINAL_STORE_MISMATCH` | Terminal sai store | Không tự đổi store |
| `TERMINAL_ALREADY_HAS_OPEN_SHIFT` | Terminal có phiên active | Tải WorkShift hiện tại |
| `STAFF_ALREADY_HAS_OPEN_SHIFT` | Nhân viên có phiên active | Tải WorkShift hiện tại |
| `OUTSIDE_SCHEDULE_REASON_REQUIRED` | Thiếu/sai lý do | Focus trường lý do |
| `OUTSIDE_SCHEDULE_APPROVAL_REQUIRED` | Thiếu phê duyệt ngoài lịch | Mở phần OTP |
| `LATE_OPENING_REQUIRES_OTP` | Mở trễ vượt ngưỡng | Mở phần OTP trễ |
| `OTP_RATE_LIMITED` | Vượt giới hạn OTP | Hiển thời gian chờ, không gửi liên tục |
| `WORKSHIFT_EXPIRED` | Phiên đã hết hạn | Khóa bán và chuyển sang chốt két |
| `WORKSHIFT_PENDING_CLOSE` | Phiên đang chờ đóng | Không tạo giao dịch mới |
| `PAYMENT_IN_PROGRESS` | Còn payment đang xử lý | Chờ/reload trạng thái payment |
| `OFFLINE_ORDERS_PENDING` | Còn đơn offline | Retry sync hoặc đóng ngoại lệ |
| `OUTSIDE_SCHEDULE_OFFLINE_NOT_ALLOWED` | Phiên ngoài lịch không cho tạo đơn offline mới | Yêu cầu kết nối lại |
| `DUPLICATE_REQUEST` | Request key bị dùng sai payload | Không tự sinh key mới cho thao tác cũ |
| `CONCURRENCY_CONFLICT` | Dữ liệu đã thay đổi | Tải lại trạng thái server |

Frontend phân nhánh theo `errorCode`, không so sánh chuỗi message tiếng Việt.

## 12. Kịch bản kiểm thử theo tài khoản, form và action

Quy ước: A = `salesstaff@cafechain.vn`, B = `shiftsupervisor@cafechain.vn`, C = `storemanager@cafechain.vn`. Mỗi trình duyệt dùng profile/cửa sổ riêng để cookie và SignalR không đè nhau.

| Trường hợp | Tài khoản và dữ liệu chuẩn bị | Form/action và endpoint | Thay đổi sau response, dữ liệu và hoàn nguyên |
|---|---|---|---|
| Vào StaffHub | A active, có `App.StaffHub` | Đăng nhập A → mở `/StaffHub` | Trang chỉ đọc hồ sơ/lịch; không tạo WorkShift. Đăng xuất để hoàn nguyên phiên. |
| Thiếu quyền StaffHub | Khách hàng hoặc account bỏ `App.StaffHub` | Nhập trực tiếp `/StaffHub` | Login/403; không render form, không mutation. Khôi phục permission nếu đã thay trong DB test. |
| Thiếu quyền POS | Account có StaffHub nhưng bỏ `App.POS` hoặc `POS.WorkShift.Open` | Reload StaffHub | Nút **Mở POS** biến mất; POST trực tiếp bị 403. Khôi phục permission. |
| Có lịch đúng giờ/sớm ≤30 phút | A có StaffShift active bao phủ thời điểm server | **Mở POS** → `PreviewOpenPos` → tự gọi `IssuePosToken` | Không hiện modal StaffHub; POS hiện tiền đầu phiên. Đóng phiên test sau khi mở. |
| Trễ >15 đến ≤30 phút | Điều chỉnh lịch A để giờ bắt đầu trước hiện tại 16–30 phút | **Mở POS** → `PreviewOpenPos` | Modal trễ hiện; **Hủy** không issue; **Tiếp tục** issue. POS thêm trường lý do, chưa cần OTP. Khôi phục StaffShift. |
| Trễ >30 phút | Lịch A đã bắt đầu >30 phút nhưng chưa quá End+30 | Preview → modal → tiếp tục; POS nhập lý do → `POST /api/v1/otp/request` | POS hiện ô OTP; B nhận Gmail + popup/badge. Verify rồi mở; sau test đóng phiên và khôi phục lịch. |
| Không có lịch | Xóa/tạm hủy lịch A ở thời điểm test; B online | A bấm **Mở POS** → `PreviewOpenPos` | Modal ngoài lịch phải xuất hiện trước exchange; DB chưa có challenge/exchange/WorkShift nếu bấm **Hủy**. |
| Lịch bị hủy | StaffShift A status `CANCELLED` | Preview | Backend coi như không có lịch, hiện modal ngoài lịch. Khôi phục status sau test. |
| Ca qua đêm | StaffShift ngày trước `22:00–06:00`, test sau 00:00 | Preview; sau exchange POS gọi `open-assessment` | Hiển thị đủ hai ngày và giữ `BusinessDate` ngày bắt đầu. Đóng phiên/xóa fixture test. |
| Mở ngoài lịch hoàn chỉnh | A không lịch; B active, online, đúng scope; terminal active | A preview → **Tiếp tục** → POS nhập tiền/lý do → `/api/v1/otp/request` → `/verify` → `/pos/shifts/open` | `OPEN`, `OUTSIDE_SCHEDULE`, deadline 6 giờ; notification body không chứa mã, payload xem lại là ciphertext. Đóng WorkShift và resolve notification. |
| Ca trưởng không hợp lệ | B inactive/mất permission/sai scope; C hợp lệ | A gửi OTP | C nhận Gmail/realtime thay B. Khôi phục B sau test. |
| Terminal chưa đăng ký/inactive/sai store | Đổi terminal fixture tương ứng | POS `open-assessment`/`open` | Hiện lỗi terminal/luồng đăng ký; không tự active. Khôi phục terminal. |
| Terminal/nhân viên có phiên active | Tạo/mở một WorkShift active trước | Gửi `/pos/shifts/open` lần nữa | Lỗi xung đột; chỉ một WorkShift. Đóng phiên fixture. |
| Double-click/retry | Giữ cùng RequestKey và payload | Bấm mở hai lần hoặc gửi song song `/open` | Một insert; replay kết quả. Dùng cùng key khác body nhận `DUPLICATE_REQUEST`. Đóng phiên. |
| OTP sai/khóa | A có challenge pending | Gọi `/api/v1/otp/verify` mã sai 3 lần | Challenge `LOCKED`, notification resolved/badge giảm, không mở phiên. Tạo challenge mới sau cooldown nếu cần. |
| OTP hết hạn | Đặt `ExpiresAt` về quá khứ | Reload notification; verify/resend | Query chuông loại metadata hết hạn; verify bị từ chối. Xóa/expire fixture. |
| OTP dùng lại | Verify đúng một lần rồi gọi verify lại | `/api/v1/otp/verify` | Lần hai bị từ chối; notification đã resolved và payload mã hóa bị xóa. |
| Resend OTP | Challenge pending, qua cooldown 60 giây | `/api/v1/otp/resend` | Nút resend chỉ bật khi countdown về 0; Gmail, realtime và chuông nhận mã mới, mã cũ vô hiệu; cùng notification được cập nhật/unread. |
| Approver mất quyền giữa chừng | Tạo challenge khi B hợp lệ, sau đó bỏ permission/scope B | A gọi `/verify` | `OTP_APPROVER_NO_LONGER_ELIGIBLE`; không tạo WorkShift. Resolve/hủy challenge và khôi phục quyền. |
| Cảnh báo 30/10/1 phút | WorkShift ngoài lịch open; đặt deadline gần từng mốc | Chờ worker hoặc gọi handler test | Một cảnh báo/mốc đúng audience; trạng thái còn `OPEN`. Hoàn nguyên deadline/đóng phiên. |
| Hết hạn phiên rỗng | Ngoài lịch, StartingCash=0, không order/payment/offline | Chờ worker | `CLOSED`, `AUTO_EMPTY_SHIFT`, tiền cuối 0. Không cần kiểm đếm. |
| Hết hạn có tiền/giao dịch | StartingCash>0 hoặc có order/payment | Chờ worker | `EXPIRED_PENDING_CLOSE`, UI khóa bán; không tự ghi ActualEndingCash. Đóng theo quy trình. |
| Đóng thường | WorkShift open, không blocker | `/start-closing`, nhập ActualEndingCash, `/close` | Form chuyển preview → nhập tiền → kết quả `CLOSED`; terminal giải phóng. |
| Đóng bị chặn | Có payment pending hoặc manifest offline chưa sync | `/start-closing`/`/close` | Hiện số blocker và mã lỗi; WorkShift không đóng. Hoàn tất payment/sync hoặc dùng ngoại lệ. |
| Đóng ngoại lệ | C có `CloseException`, còn blocker, đã kiểm đếm | Gửi OTP đúng action rồi `/close-exception` | `RECONCILIATION_REQUIRED`; order muộn giữ shift cũ. Sau test đồng bộ và reconcile. |
| Đồng bộ muộn/reconcile | WorkShift reconciliation có dữ liệu đến muộn | Đồng bộ/callback rồi C gọi `/reconcile` | Cập nhật WorkShift cũ, audit adjustment; `CLOSED` khi hết blocker. |
| Tiếp tục sau 6 giờ | Phiên cũ expired | Thử bán/gia hạn; sau đó đóng và mở mới | Bán/gia hạn bị chặn; phiên mới cần tiền/lý do/OTP/RequestKey mới. |
| Chuyển trách nhiệm két | A đang có phiên; nhân viên nhận đăng nhập riêng | A đóng/kiểm đếm; người nhận mở phiên mới | Không đổi owner phiên cũ, không copy toàn bộ ActualEndingCash; đóng phiên mới sau test. |
| SignalR mất kết nối | Tắt mạng/stop hub ở B | A gửi OTP hoặc chờ expiry | Gmail vẫn chứa OTP; metadata badge có sau polling; backend deadline vẫn chặn. Kết nối lại để refresh. |
| Server restart | WorkShift có deadline trong DB | Restart backend trước deadline | Worker tiếp tục theo deadline đã lưu; không mất trạng thái. Đóng fixture. |
| Dashboard/AppLauncher | A và C đăng nhập riêng | Nút **AppLauncher**; C có thêm **Dashboard** | Route đúng; A không có Dashboard nếu policy fail; không tạo dữ liệu. |

### 12.1 Chi tiết form OTP hai kênh

1. Trước `/api/v1/otp/request`, POS có `StartingCash`, `TerminalId`, `Reason`, `RequestKey`; StaffHub không có các input này.
2. Sau request thành công, POS giữ nguyên payload, nhận `OtpChallengePublicId` và mở ô 6 ký tự cùng nút **Xác nhận/Gửi lại**.
3. Gmail và `OperationalOtpIssued` phải chứa cùng mã. Event chỉ đến group của `ApproverStaffId`; không publish vào group store.
4. Chuông tạo một dòng `OPERATIONAL_OTP_REQUEST`; body không có mã rõ. Popup realtime và danh sách chuông của đúng approver hiển thị mã, hạn dùng, countdown và nút **Sao chép mã**.
5. Đóng popup không làm mất mã: đúng approver có thể mở lại chuông trong lúc challenge còn `Pending` và chưa hết hạn. API chứa mã đặt `Cache-Control: private, no-store`.
6. Verify/lock/cancel/expire resolve notification, xóa payload mã hóa và phát `OperationalOtpNotificationChanged` không chứa mã. Resend sau 60 giây thay mã cũ và cập nhật cùng notification.

## 13. Checklist kiểm thử thủ công

- [ ] Không có lịch thì hiển thị đúng thông báo và không tạo `StaffShift`.
- [ ] Lịch qua đêm hiển thị đủ hai ngày và assessment tìm được lịch ngày trước.
- [ ] Lịch bị hủy không được dùng để mở theo lịch.
- [ ] Kiểm tra các mốc mở sớm 30, trễ 15, trễ 30 và sau kết thúc 30 phút.
- [ ] Ngoài lịch thiếu lý do/OTP bị từ chối.
- [ ] Preview ngoài lịch hiện modal StaffHub trước khi phát exchange code; **Hủy** không tạo mutation.
- [ ] StaffHub modal không chứa tiền đầu phiên/lý do/OTP; các trường này chỉ nằm trong POS.
- [ ] Gmail và popup realtime của đúng approver nhận cùng mã OTP.
- [ ] Tài khoản cùng store nhưng không phải approver không nhận `OperationalOtpIssued`.
- [ ] Notification body, audit và log không chứa mã OTP; `ProtectedOtpPayload` là ciphertext, không chứa mã rõ.
- [ ] Đúng approver xem lại được OTP còn hạn trong chuông; staff khác không nhận mã.
- [ ] OTP chỉ nhận 6 ký tự thuộc alphabet cho phép; ký tự đặc biệt, khoảng trắng nội bộ, chữ có dấu, emoji và O/0/I/1 bị từ chối.
- [ ] Resend bị khóa đủ 60 giây, cập nhật một notification, vô hiệu mã cũ và phát mã mới qua Gmail/SignalR/chuông.
- [ ] Mở ngoài lịch thành công có deadline đúng 6 giờ và `SourceStaffShiftId = null`.
- [ ] Terminal chưa đăng ký không bị tự active.
- [ ] Hai thiết bị/double-click chỉ tạo một WorkShift.
- [ ] Retry cùng key/payload replay kết quả; khác payload bị từ chối.
- [ ] OTP sai, hết hạn, reuse, sai store/terminal/staff đều bị từ chối.
- [ ] Cảnh báo 30/10/1 chỉ phát một lần.
- [ ] Phiên rỗng tiền đầu 0 tự đóng; phiên có tiền/giao dịch chuyển chờ chốt.
- [ ] Ngắt SignalR hoặc restart server không làm mất deadline backend.
- [ ] Đóng thường bị chặn khi có payment pending/offline.
- [ ] Đóng ngoại lệ và sync muộn giữ WorkShift cũ.
- [ ] Muốn bán tiếp sau 6 giờ phải mở phiên mới.
- [ ] Tiền đầu phiên mới do người nhận kiểm đếm, không copy tự động.
- [ ] Nút AppLauncher luôn hiện; nút Dashboard và Mở POS phản ánh đúng permission.
- [ ] Sửa `StoreId`, `StaffId` hoặc thời gian trên client không thay đổi quyết định backend.

## 14. Những việc hệ thống không được làm

- Không tạo lịch giả khi mở POS ngoài lịch.
- Không dùng WorkShift để kết luận giờ làm hoặc tính lương.
- Không tự điền tiền thực tế nếu chưa kiểm đếm.
- Không gia hạn WorkShift ngoài lịch đã đủ 6 giờ.
- Không chuyển order/payment/offline sang WorkShift mới.
- Không tin permission, store, staff hoặc thời gian do client tự khai báo.
- Không log OTP, PIN, JWT hoặc mã exchange rõ.
