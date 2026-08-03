# PROMPT REFACTOR STAFFHUB, CA QUA ĐÊM, CA NGOÀI LỊCH VÀ WORKSHIFT POS TỰ HẾT HẠN SAU 6 GIỜ

## 1. Vai trò thực hiện

Hãy đóng vai:

* Senior Software Engineer có ít nhất 20 năm kinh nghiệm.
* Business Analyst am hiểu vận hành thực tế chuỗi cửa hàng cà phê.
* Chuyên gia ASP.NET Core MVC, Layered Architecture, EF Core và SQL Server.
* Chuyên gia về authorization, concurrency, idempotency, audit, SignalR và bảo mật ứng dụng.

Hãy đọc, inspect và đối chiếu mã nguồn hiện tại trước khi sửa.

Không được suy đoán tên file, class, bảng, cột, route hoặc service chưa tồn tại. Nếu hệ thống đã có cơ chế tương đương thì phải tái sử dụng và mở rộng, không tạo trùng.

---

# 2. Bối cảnh dự án

Dự án là hệ thống CafeChain dành cho chuỗi nhỏ từ 2–5 cửa hàng.

Hệ thống hiện có:

* Hồ sơ nhân viên.
* Role, Permission và `StaffScope`.
* Mẫu ca `Shift`.
* Lịch dự kiến `StaffShift`.
* StaffHub để nhân viên xem lịch.
* POS.
* POS terminal.
* Phiên chịu trách nhiệm POS/két `WorkShift`.
* Đơn hàng, thanh toán và đối soát tiền.
* Đơn offline hoặc cơ chế đồng bộ offline.
* Audit hoặc lịch sử thao tác.
* SignalR hoặc cơ chế thông báo thời gian thực.
* Cơ chế Request Deduplication nếu đã tồn tại.

Dự án không có và không được bổ sung:

* Chấm công.
* Check-in/check-out.
* Face ID chấm công.
* Ghi nhận giờ làm thực tế.
* Tính giờ công.
* Tính tăng ca.
* Tính lương.
* Phụ cấp ca.
* Khấu trừ lương.
* Tự động tạo lịch làm việc để hợp thức hóa thời gian mở POS.

---

# 3. Mục tiêu refactor

Refactor toàn bộ luồng StaffHub và WorkShift để giải quyết:

1. Nhân viên có lịch bình thường.
2. Nhân viên không có lịch nhưng cần mở POS hỗ trợ đột xuất.
3. Nhân viên mở POS trễ so với lịch.
4. Ca làm việc hoặc phiên POS đi qua nửa đêm.
5. Ca ngoài lịch chỉ được vận hành tối đa 6 giờ.
6. Ca ngoài lịch tự ngừng nhận giao dịch khi đủ 6 giờ.
7. Không tự động đóng sổ tiền khi chưa kiểm đếm.
8. Chuyển trách nhiệm POS/két giữa hai nhân viên.
9. Đóng ca khi còn đơn offline hoặc thanh toán đang xử lý.
10. Ngăn double click, duplicate request và race condition.
11. Ngăn brute-force OTP, mã PIN quản lý và đăng nhập.
12. Bảo đảm phân quyền và store scope.
13. Ghi audit đầy đủ cho các thao tác nhạy cảm.
14. Không biến `WorkShift` thành dữ liệu chấm công.

---

# 4. Nguyên tắc nghiệp vụ bắt buộc

## 4.1 Tách biệt ba khái niệm

### `Shift`

Là mẫu giờ làm việc dự kiến của cửa hàng, ví dụ:

* Ca sáng.
* Ca chiều.
* Ca tối.
* Ca qua đêm.

### `StaffShift`

Là lịch dự kiến đã phân cho nhân viên.

`StaffShift` không phải:

* Bằng chứng có mặt.
* Giờ vào thực tế.
* Giờ ra thực tế.
* Giờ công.
* Thời gian tính lương.

### `WorkShift`

Là phiên chịu trách nhiệm POS/két.

`WorkShift` dùng để xác định:

* Ai đang chịu trách nhiệm terminal.
* Ai mở phiên.
* Ai đóng phiên.
* Thời gian phiên POS.
* Tiền đầu phiên.
* Tiền kỳ vọng cuối phiên.
* Tiền thực tế.
* Chênh lệch.
* Đơn và thanh toán thuộc phiên nào.

Không được dùng `WorkShift` để tính giờ công.

---

## 4.2 Không tạo “ca tự do”

Khi nhân viên không có lịch:

```text
Không có StaffShift phù hợp
→ Không tạo StaffShift mới
→ Không tạo lịch giả
→ Phân loại là mở POS ngoài lịch
→ Yêu cầu lý do
→ Yêu cầu phê duyệt
→ Tạo WorkShift độc lập
```

Không sử dụng các thuật ngữ:

* Ca tự do.
* Ca chấm công.
* Giờ công.
* Số giờ làm thực tế.

Sử dụng thống nhất:

* Mở POS ngoài lịch.
* Phiên POS ngoài lịch.
* Thời lượng phiên POS.
* Thời gian giữ trách nhiệm két.

---

# 5. Mô hình trạng thái WorkShift

Hãy kiểm tra trạng thái hiện có và mở rộng phù hợp. Không tạo enum hoặc cột trùng chức năng.

Tối thiểu cần biểu diễn được các trạng thái nghiệp vụ sau:

## `OPEN`

* WorkShift đang hoạt động.
* Có thể nhận đơn mới.
* Có thể nhận thanh toán.

## `CLOSING`

* Đã bắt đầu quá trình chốt ca.
* Khóa tạo đơn mới.
* Đang chờ xử lý giao dịch, đồng bộ hoặc kiểm đếm.

## `EXPIRED_PENDING_CLOSE`

* Chỉ áp dụng cho WorkShift ngoài lịch đã đủ 6 giờ.
* Không được nhận đơn mới.
* Không được tạo thanh toán mới.
* Chưa thể đóng tài chính vì chưa kiểm đếm tiền.
* Yêu cầu nhân viên hoặc quản lý thực hiện đóng ca.

## `CLOSED`

* Đã kết thúc.
* Đã ghi `EndTime`.
* Đã hoàn tất đối soát hoặc phiên không phát sinh giao dịch.

## `RECONCILIATION_REQUIRED`

* Phiên đã được đóng ngoại lệ.
* Có đơn offline, lỗi đồng bộ hoặc dữ liệu cần đối soát lại.
* Không nhận thêm giao dịch.
* Các đơn đồng bộ muộn vẫn thuộc WorkShift cũ.

Nếu model hiện tại chỉ hỗ trợ `Open` và `Closed`, hãy thiết kế cách mở rộng tối thiểu, tránh làm ảnh hưởng các báo cáo hoặc nghiệp vụ đang chạy.

---

# 6. Xử lý lịch và ca qua đêm

## 6.1 Không so sánh riêng phần giờ

Không được chỉ so sánh `TimeOnly`, giờ hoặc phút trong cùng ngày.

Phải chuyển lịch thành khoảng thời gian tuyệt đối:

```text
PlannedStart = WorkDate + EffectiveStartTime
PlannedEnd   = WorkDate + EffectiveEndTime
```

Trong đó:

```text
EffectiveStartTime =
CustomStartTime nếu có
ngược lại Shift.StartTime
```

```text
EffectiveEndTime =
CustomEndTime nếu có
ngược lại Shift.EndTime
```

Nếu:

```text
PlannedEnd <= PlannedStart
```

thì:

```text
PlannedEnd = PlannedEnd + 1 ngày
```

Ví dụ:

```text
WorkDate: 02/08/2026
Start: 22:00
End: 06:00

PlannedStart: 02/08/2026 22:00
PlannedEnd:   03/08/2026 06:00
```

---

## 6.2 Tìm lịch liên quan

Khi nhân viên mở POS, backend phải kiểm tra tối thiểu:

* Lịch có `WorkDate` là ngày hiện tại.
* Lịch có `WorkDate` là ngày trước đó.
* Lịch qua đêm đang bao phủ thời điểm hiện tại.
* Lịch đã bắt đầu nhưng nhân viên mở trễ.
* Lịch đã bị hủy không được tính là lịch hợp lệ.

Ví dụ:

Nhân viên mở POS lúc `03/08/2026 02:00`.

Backend phải kiểm tra lịch ngày `02/08/2026` có khung `22:00–06:00` trước khi kết luận nhân viên không có lịch.

---

## 6.3 BusinessDate

Mỗi WorkShift nên có hoặc xác định được `BusinessDate`.

Quy tắc đề xuất:

* Nếu WorkShift được mở từ lịch dự kiến, `BusinessDate` là `WorkDate` của lịch nguồn.
* Nếu WorkShift ngoài lịch, `BusinessDate` là ngày địa phương của cửa hàng tại thời điểm mở.
* `BusinessDate` không thay đổi khi WorkShift đi qua nửa đêm.
* Tất cả thời điểm hệ thống nên lưu UTC.
* Khi hiển thị, chuyển theo timezone cấu hình của cửa hàng hoặc timezone hệ thống.

Ví dụ:

```text
Mở ngoài lịch: 02/08/2026 23:30
Tự hết hạn:    03/08/2026 05:30
BusinessDate:  02/08/2026
```

Không được tách WorkShift chỉ vì đồng hồ chuyển sang ngày mới.

---

# 7. Phân loại thời điểm mở POS

Backend phải phân loại yêu cầu mở POS thành một trong các trường hợp:

## `WITHIN_SCHEDULE`

* Có `StaffShift` trạng thái hợp lệ.
* Thời điểm mở nằm trong khoảng lịch cho phép.

## `LATE_FOR_SCHEDULE`

* Có lịch liên quan.
* Thời điểm mở muộn hơn thời gian dự kiến.
* Vẫn nằm trong ngưỡng được xem là mở trễ theo chính sách.

## `OUTSIDE_SCHEDULE`

* Không có lịch phù hợp.
* Lịch đã bị hủy.
* Mở trước hoặc sau lịch quá xa.
* Mở tại cửa hàng không thuộc lịch.
* Lịch gần nhất không còn đủ điều kiện để xem là mở trễ.

Các ngưỡng mở sớm, mở trễ phải được đọc từ cấu hình hiện có hoặc bổ sung thành cấu hình nghiệp vụ. Không hard-code rải rác trong controller, JavaScript hoặc view.

---

# 8. Luồng mở POS trong lịch

## Điều kiện

* Tài khoản đang hoạt động.
* Nhân viên đang hoạt động.
* Có permission mở POS.
* Thuộc đúng `StaffScope`.
* Terminal đang hoạt động.
* Terminal thuộc đúng cửa hàng.
* Không có WorkShift mở trên terminal.
* Nhân viên không có WorkShift mở xung đột.
* Có `StaffShift` hợp lệ.
* Thời điểm mở nằm trong lịch cho phép.

## Luồng xử lý

1. Nhân viên chọn mở POS.
2. FE tạo `RequestKey` duy nhất.
3. Backend lấy `StaffId`, quyền và store scope từ claims.
4. Không tin `StaffId` hoặc `StoreId` do client tự gửi.
5. Backend tìm StaffShift liên quan.
6. Phân loại là `WITHIN_SCHEDULE`.
7. Validate tiền đầu ca.
8. Kiểm tra lại terminal và nhân viên trong transaction.
9. Tạo WorkShift.
10. Ghi `StartTime` từ server.
11. Lưu liên kết lịch nguồn nếu dự án triển khai `SourceStaffShiftId`.
12. Ghi audit.
13. Trả kết quả mở ca.
14. Nếu client retry cùng `RequestKey`, trả lại kết quả cũ, không tạo thêm WorkShift.

---

# 9. Luồng mở POS trễ

## Xử lý

1. Backend tìm lịch đang bao phủ thời điểm hiện tại.
2. Nếu không có, tìm lịch gần nhất đã bắt đầu hoặc vừa kết thúc.
3. Xác định độ trễ bằng thời gian server.
4. Không sử dụng thời gian do client gửi.
5. Phân loại `LATE_FOR_SCHEDULE`.
6. Yêu cầu nhập lý do nếu vượt ngưỡng cấu hình.
7. Yêu cầu phê duyệt nếu vượt ngưỡng cần duyệt.
8. Kiểm tra người duyệt.
9. Ghi audit:

   * Lịch dự kiến.
   * Thời gian dự kiến.
   * Thời gian mở.
   * Số phút trễ.
   * Lý do.
   * Người phê duyệt.
10. Tạo WorkShift sau khi vượt qua toàn bộ validation.

Không tự tạo StaffShift mới để thay thế lịch cũ.

---

# 10. Luồng mở POS ngoài lịch

## 10.1 Điều kiện

* Nhân viên có permission mở POS.
* Nhân viên thuộc đúng store scope.
* Không có lịch phù hợp.
* Terminal hợp lệ.
* Không có WorkShift xung đột.
* Có lý do cụ thể.
* Có người có quyền phê duyệt.

## 10.2 Người được phê duyệt

Không hard-code duy nhất theo tên role.

Backend phải kiểm tra permission phê duyệt và `StaffScope`.

Đề xuất permission:

* `POS.WorkShift.View`.
* `POS.WorkShift.Open`.
* `POS.WorkShift.Close`.
* `POS.WorkShift.OpenOutsideSchedule`.
* `POS.WorkShift.ApproveOutsideSchedule`.
* `POS.WorkShift.CloseException`.
* `POS.WorkShift.Reconcile`.
* `POS.WorkShift.OverrideTerminal`.

Phân quyền mặc định phù hợp bối cảnh hiện tại:

* Nhân viên bán hàng: mở và đóng WorkShift của bản thân khi được cấp quyền.
* Quản lý chi nhánh: phê duyệt ngoài lịch trong cửa hàng quản lý.
* Quản lý vùng: phê duyệt trong các cửa hàng thuộc scope.
* Chủ doanh nghiệp: phê duyệt trong phạm vi doanh nghiệp.
* Quản trị hệ thống: không mặc định là người duyệt nghiệp vụ; chỉ được duyệt nếu được gán permission tương ứng.
* Kế toán/kho: không mặc định có quyền mở hoặc duyệt POS.

## 10.3 Validation lý do

Lý do:

* Bắt buộc.
* Trim khoảng trắng.
* Độ dài tối thiểu đề xuất 10 ký tự.
* Độ dài tối đa đề xuất 500 ký tự.
* Không chấp nhận nội dung chỉ gồm dấu câu hoặc khoảng trắng.
* Escape khi hiển thị.
* Không render HTML trực tiếp.
* Có thể chặn các nội dung quá chung chung như “bận”, “mở ca”, “test” nếu dự án có rule phù hợp.

Ví dụ hợp lệ:

* Thay nhân viên ca chính nghỉ đột xuất.
* Hỗ trợ xử lý lượng đơn tăng cao.
* Hỗ trợ cửa hàng theo điều phối của quản lý.
* Nhân viên được gọi đến thay ca khẩn cấp.

## 10.4 Luồng xử lý

```text
Nhân viên yêu cầu mở POS
→ Backend kiểm tra lịch
→ Phân loại OUTSIDE_SCHEDULE
→ Yêu cầu lý do
→ Tạo challenge phê duyệt
→ Quản lý xác nhận
→ Kiểm tra lại scope và permission
→ Kiểm tra lại terminal trong transaction
→ Tạo WorkShift
→ AutoCloseAt = StartTime + 6 giờ
→ Ghi audit
→ Gửi SignalR
```

Không tạo `StaffShift`.

---

# 11. Quy tắc WorkShift ngoài lịch tối đa 6 giờ

## 11.1 Thời hạn

Mỗi WorkShift ngoài lịch phải có thời hạn:

```text
AutoCloseAtUtc = StartTimeUtc + 6 giờ
```

Chỉ áp dụng cho WorkShift được phân loại `OUTSIDE_SCHEDULE`.

Không áp dụng mặc định cho:

* WorkShift mở trong lịch.
* WorkShift mở trễ nhưng vẫn thuộc lịch.
* WorkShift đã đóng.
* WorkShift đã chuyển sang trạng thái chờ đối soát.

Thời hạn phải được tính bằng thời gian server.

---

## 11.2 Không gia hạn trực tiếp WorkShift cũ

Khi đủ 6 giờ:

* Không cho phép sửa `AutoCloseAtUtc` để kéo dài tùy tiện.
* Không cho phép nhân viên tiếp tục dùng WorkShift cũ.
* Không cho phép manager chỉ bấm “gia hạn thêm” mà không tạo phiên mới.

Nếu cửa hàng vẫn cần tiếp tục bán:

```text
Khóa WorkShift cũ
→ Chốt và đối soát
→ Mở WorkShift ngoài lịch mới
→ Nhập lý do mới
→ Phê duyệt mới
→ Tạo thời hạn 6 giờ mới
```

Điều này giúp:

* Tách trách nhiệm tiền.
* Tránh một WorkShift kéo dài cả ngày.
* Tránh sử dụng ca ngoài lịch để bỏ qua lịch dự kiến.
* Có dấu vết phê duyệt theo từng giai đoạn.

---

## 11.3 Cảnh báo trước khi hết hạn

Dùng SignalR hoặc cơ chế thông báo hiện có để cảnh báo:

* Trước 30 phút.
* Trước 10 phút.
* Trước 1 phút.
* Khi đã hết hạn.

Thông báo gửi tới:

* Nhân viên đang sử dụng POS.
* Quản lý chi nhánh đang online.
* Các tài khoản có permission xử lý WorkShift trong store scope.

Không gửi thông báo toàn hệ thống cho các cửa hàng không liên quan.

---

## 11.4 Xử lý đúng thời điểm đủ 6 giờ

Tại hoặc ngay sau `AutoCloseAtUtc`, hệ thống phải:

1. Khóa tạo đơn mới.
2. Khóa thêm item vào đơn mới.
3. Khóa tạo thanh toán mới.
4. Không hủy giao dịch đang xử lý giữa chừng.
5. Chuyển WorkShift sang `EXPIRED_PENDING_CLOSE`.
6. Hiển thị modal bắt buộc chốt phiên.
7. Gửi SignalR cho quản lý.
8. Ghi audit sự kiện hết hạn.
9. Không tự điền `ActualEndingCash`.
10. Không tự giả định tiền thực tế bằng tiền kỳ vọng.

Endpoint tạo đơn và thanh toán phải kiểm tra lại trạng thái WorkShift ở backend. Không chỉ khóa nút ở FE.

Mã lỗi nghiệp vụ nên ổn định, ví dụ:

* `WORKSHIFT_EXPIRED`.
* `WORKSHIFT_NOT_OPEN`.
* `WORKSHIFT_PENDING_CLOSE`.

Không phụ thuộc hoàn toàn vào nội dung message để FE phân loại lỗi.

---

## 11.5 Trường hợp được tự đóng hoàn toàn

Chỉ cho phép tự chuyển trực tiếp sang `CLOSED` nếu đồng thời thỏa tất cả điều kiện:

* Không có đơn nào thuộc WorkShift.
* Không có thanh toán nào.
* Không có payment đang xử lý.
* Không có dữ liệu offline.
* Không có lỗi đồng bộ.
* `StartingCash = 0`.
* Không có điều chỉnh tiền.
* Không có giao dịch két.
* Không có dữ liệu cần kiểm đếm.

Khi đó:

```text
ExpectedEndingCash = 0
ActualEndingCash = 0
CashDiscrepancy = 0
CloseType = AUTO_EMPTY_SHIFT
```

Nếu chỉ một điều kiện không thỏa, không được tự đóng tài chính.

---

## 11.6 Trường hợp có tiền hoặc giao dịch

Nếu WorkShift đã có:

* Tiền đầu ca.
* Đơn hàng.
* Thanh toán.
* Giao dịch tiền mặt.
* Đơn offline.
* Dữ liệu chưa đồng bộ.

Thì sau 6 giờ:

```text
OPEN
→ EXPIRED_PENDING_CLOSE
→ Nhân viên hoặc quản lý kiểm đếm
→ Đóng thông thường hoặc đóng ngoại lệ
```

Không cho mở WorkShift mới trên cùng terminal trước khi WorkShift cũ được đóng hoặc đóng ngoại lệ hợp lệ.

---

# 12. Cơ chế xử lý tự động

Hãy kiểm tra dự án đang dùng:

* `BackgroundService`.
* Hosted Service.
* Hangfire.
* Quartz.
* Scheduler khác.
* SQL Agent.
* Cơ chế polling hiện có.

Ưu tiên tái sử dụng cơ chế hiện hữu.

Đối với hệ thống 2–5 cửa hàng, không cần đưa thêm hạ tầng phức tạp nếu chưa cần thiết.

## Worker đề xuất

Worker chạy tối đa mỗi 1 phút:

1. Lấy thời gian UTC của server.
2. Tìm WorkShift:

   * Loại ngoài lịch.
   * Trạng thái `OPEN`.
   * `AutoCloseAtUtc <= now`.
3. Lock hoặc kiểm soát concurrency từng WorkShift.
4. Kiểm tra lại trạng thái trong transaction.
5. Nếu phiên hoàn toàn rỗng, tự đóng.
6. Nếu có dữ liệu, chuyển `EXPIRED_PENDING_CLOSE`.
7. Ghi audit.
8. Gửi SignalR.
9. Commit.
10. Không xử lý lại WorkShift đã được worker khác xử lý.

Nếu ứng dụng có nhiều instance, phải dùng:

* Database lock.
* Distributed lock hiện có.
* RowVersion kết hợp transaction.
* Hoặc cơ chế bảo đảm chỉ một worker xử lý một WorkShift.

Không được dựa vào biến `static` hoặc lock trong RAM để bảo vệ dữ liệu giữa nhiều instance.

---

# 13. Luồng đóng WorkShift thông thường

## Điều kiện trước khi đóng

* WorkShift tồn tại.
* WorkShift thuộc đúng store scope.
* Người thao tác có permission.
* WorkShift chưa đóng.
* Không còn payment đang xử lý.
* Không còn order ở trạng thái không cho phép đóng.
* Không còn đơn offline chưa đồng bộ.
* Không có lỗi đồng bộ bắt buộc xử lý.
* `ActualEndingCash` hợp lệ.

## Công thức

```text
ExpectedEndingCash
= StartingCash
+ Tổng tiền mặt hợp lệ
+ Các khoản tiền vào két hợp lệ
- Các khoản tiền ra két hợp lệ
```

Phải kiểm tra model hiện tại có nghiệp vụ tiền vào/ra két hay không. Không tự thêm công thức nếu dự án chưa có loại giao dịch đó.

```text
CashDiscrepancy
= ActualEndingCash - ExpectedEndingCash
```

## Luồng

1. Chuyển WorkShift sang `CLOSING`.
2. Khóa nhận đơn mới.
3. Kiểm tra payment đang xử lý.
4. Kiểm tra offline.
5. Backend tính `ExpectedEndingCash`.
6. Nhân viên nhập `ActualEndingCash`.
7. Backend tính chênh lệch.
8. Chênh lệch khác 0 thì bắt buộc lý do.
9. Vượt ngưỡng thì yêu cầu phê duyệt.
10. Ghi `EndTime` từ server.
11. Chuyển sang `CLOSED`.
12. Ghi người đóng.
13. Ghi audit.
14. Gửi SignalR cập nhật terminal.
15. Giải phóng terminal cho WorkShift mới.

Không cho FE tự gửi hoặc tự tính giá trị tiền kỳ vọng làm nguồn dữ liệu cuối cùng.

---

# 14. Đóng WorkShift ngoại lệ

Chỉ sử dụng khi:

* Mất mạng kéo dài.
* Đơn offline không thể đồng bộ.
* Hệ thống thanh toán bên thứ ba đang lỗi.
* Cần chuyển ca khẩn cấp.
* Có lỗi kỹ thuật không thể xử lý tại thời điểm đóng.

## Điều kiện

* Người thao tác có permission `CloseException`.
* Có lý do.
* Có phê duyệt.
* Audit đầy đủ.
* Không thay đổi `WorkShiftId` của đơn cũ.

## Xử lý

1. Khóa nhận giao dịch mới.
2. Ghi trạng thái cần đối soát.
3. Ghi số lượng đơn offline.
4. Ghi số lượng payment chưa xác nhận.
5. Ghi lý do.
6. Ghi người phê duyệt.
7. Ghi thời điểm đóng ngoại lệ.
8. Chuyển sang `RECONCILIATION_REQUIRED`.
9. Cho phép terminal mở WorkShift mới sau khi hoàn tất bước bàn giao tiền bắt buộc.
10. Khi dữ liệu offline đồng bộ lại, vẫn gắn với WorkShift cũ.
11. Không chuyển doanh thu sang WorkShift mới.
12. Cập nhật lại dữ liệu đối soát của WorkShift cũ.
13. Ghi lịch sử điều chỉnh sau đóng.

---

# 15. Chuyển ca và bàn giao két

Không sửa WorkShift cũ thành ca mới.

Luồng bắt buộc:

```text
Khóa nhận đơn ca cũ
→ Hoàn tất giao dịch
→ Đồng bộ offline
→ Tính tiền kỳ vọng
→ Kiểm đếm tiền thực tế
→ Đóng WorkShift cũ
→ Xác định doanh thu cần rút
→ Xác định tiền lẻ để lại
→ Người nhận kiểm đếm
→ Mở WorkShift mới
```

## Tiền đầu ca mới

Không được áp dụng:

```text
NextStartingCash = PreviousActualEndingCash
```

Tiền đầu ca mới phải là:

```text
Số tiền lẻ thực tế người nhận kiểm đếm và xác nhận
```

Nếu có chức năng bàn giao hiện hữu, tái sử dụng.

Nếu chưa có biên bản bàn giao, không được mô tả UI như hệ thống đã có quan hệ tự động giữa WorkShift cũ và mới.

Có thể lưu tham chiếu `PreviousWorkShiftId` ở mức tùy chọn nếu thực sự cần truy vết, nhưng không được dùng để tự động sao chép tiền.

---

# 16. Permission và StaffScope

Mọi endpoint phải kiểm tra backend.

Không được chỉ dựa vào:

* Nút có hiển thị hay không.
* Role name trong JavaScript.
* `StoreId` do client gửi.
* Hidden input.
* Query string.
* Local storage.

Backend phải lấy:

* `AccountId`.
* `StaffId`.
* Permission.
* Role.
* Store scope.

Từ identity, claims và cơ chế scope hiện có.

SystemAdmin có global scope trên cửa hàng active chỉ khi chính sách hiện tại cho phép. Việc sửa `storeId`, `terminalId`, `staffId` trong request không được mở rộng phạm vi của các role khác.

Phải kiểm tra:

* Terminal thuộc Store nào.
* Người mở thuộc Store hoặc scope nào.
* Người duyệt có scope với Store đó không.
* WorkShift có thuộc Store đó không.
* Lịch có thuộc cùng cửa hàng không.
* Người đóng có quyền trên WorkShift đó không.

---

# 17. Validation bắt buộc

## 17.1 Mở WorkShift

* Account active.
* Staff active.
* Store active.
* Terminal active.
* Terminal thuộc Store.
* Người dùng có permission.
* Store thuộc scope.
* Không có WorkShift mở trên terminal.
* Không có WorkShift mở xung đột của nhân viên.
* `StartingCash >= 0`.
* Tiền theo đơn vị VND phải là số nguyên nếu hệ thống không sử dụng tiền lẻ thập phân.
* Không vượt giới hạn tiền mặt cấu hình.
* Lý do ngoài lịch hợp lệ.
* Phê duyệt còn hiệu lực.
* Challenge phê duyệt đúng Store, terminal, nhân viên và hành động.
* Không dùng lại challenge.
* `RequestKey` hợp lệ.

## 17.2 Đóng WorkShift

* WorkShift tồn tại.
* WorkShift chưa đóng.
* WorkShift đúng Store.
* Người dùng có permission.
* `ActualEndingCash >= 0`.
* Không tin `ExpectedEndingCash` từ client.
* Không tin `CashDiscrepancy` từ client.
* Chênh lệch khác 0 phải có lý do.
* Chênh lệch vượt ngưỡng phải có phê duyệt.
* Không còn payment đang xử lý.
* Không còn offline nếu đóng thường.
* RowVersion đúng.
* Request chưa được xử lý trước đó.

## 17.3 StaffShift

* Nhân viên và Shift thuộc cùng Store.
* Custom start và custom end phải có đủ cả hai.
* Tính đúng lịch qua đêm.
* Không giao nhau với lịch hiệu lực khác.
* Lịch bị hủy không dùng để mở POS bình thường.
* Không tự tạo StaffShift từ WorkShift ngoài lịch.

---

# 18. Chống double click và duplicate request

Không được chỉ disable button ở FE.

Phải thực hiện đồng thời ở FE và backend.

## 18.1 Frontend

Khi người dùng bấm:

* Mở ca.
* Phê duyệt.
* Đóng ca.
* Đóng ngoại lệ.
* Xác nhận bàn giao.

FE phải:

1. Disable nút ngay lập tức.
2. Hiển thị loading.
3. Không tạo nhiều request song song.
4. Giữ nguyên `RequestKey` khi retry cùng thao tác.
5. Chỉ tạo `RequestKey` mới khi người dùng bắt đầu một thao tác nghiệp vụ mới.
6. Không tự động retry POST bằng `RequestKey` mới.
7. Khi timeout, kiểm tra trạng thái server trước khi cho phép thực hiện lại.
8. Không cho đóng modal trong lúc request quan trọng đang commit nếu việc đóng gây mất trạng thái.

## 18.2 Backend idempotency

Dùng `RequestDeduplication` hiện có hoặc cơ chế tương đương.

Tối thiểu lưu:

* RequestKey.
* ActionName.
* StaffId hoặc AccountId.
* WorkShiftId nếu có.
* StoreId.
* Request hash.
* Trạng thái xử lý.
* Kết quả.
* Thời điểm tạo.
* Thời điểm hết hạn.

Các action cần idempotency:

* Mở WorkShift.
* Phê duyệt mở ngoài lịch.
* Đóng WorkShift.
* Đóng ngoại lệ.
* Xác nhận bàn giao.
* Reconcile WorkShift.

Khi nhận trùng `RequestKey`:

* Nếu request trước thành công: trả lại kết quả cũ.
* Nếu đang xử lý: trả trạng thái đang xử lý.
* Nếu request body khác: từ chối vì reuse key sai.
* Nếu request trước thất bại có thể retry: áp dụng chính sách rõ ràng.
* Không insert thêm WorkShift.

Thời gian lưu deduplication đề xuất tối thiểu 24 giờ cho thao tác POS nhạy cảm.

---

# 19. Chống race condition

Tình huống cần xử lý:

* Hai lần bấm mở ca đồng thời.
* Hai thiết bị cùng mở một terminal.
* Hai nhân viên cùng mở terminal.
* Worker auto-expire chạy đồng thời với nhân viên đóng ca.
* Nhân viên đóng ca trong lúc payment callback cập nhật.
* Quản lý và nhân viên cùng đóng một WorkShift.
* Hai instance ứng dụng cùng chạy background worker.

## Biện pháp bắt buộc

1. Kiểm tra business rule trong transaction.
2. Kiểm tra lại trạng thái ngay trước insert/update.
3. Sử dụng `RowVersion` hoặc concurrency token.
4. Dùng isolation hoặc locking phù hợp.
5. Có unique constraint hoặc filtered unique index nếu SQL Server và schema cho phép.

Mục tiêu dữ liệu:

```text
Mỗi PosTerminalId chỉ có tối đa một WorkShift đang hoạt động.
```

Và nếu chính sách dự án yêu cầu:

```text
Mỗi StaffId chỉ có tối đa một WorkShift đang hoạt động.
```

Có thể dùng:

* Filtered unique index.
* Transaction `Serializable`.
* `UPDLOCK/HOLDLOCK`.
* Application lock.
* Cơ chế locking hiện hữu.

Hãy lựa chọn phương án phù hợp kiến trúc hiện tại, không triển khai kiểm tra kiểu:

```text
if (!exists)
{
    insert
}
```

mà không có bảo vệ transaction.

---

# 20. Chống brute-force OTP, PIN và phê duyệt

## 20.1 Challenge phải gắn với hành động

OTP hoặc PIN phê duyệt phải bind với:

* Người yêu cầu.
* Người duyệt.
* Store.
* Terminal.
* Loại hành động.
* WorkShift nếu có.
* Thời điểm hết hạn.
* RequestKey hoặc challenge ID.

OTP dùng để duyệt mở ngoài lịch không được dùng lại để:

* Đóng ngoại lệ.
* Duyệt chênh lệch.
* Mở terminal khác.
* Duyệt cho nhân viên khác.
* Duyệt ở cửa hàng khác.

## 20.2 Chính sách đề xuất

* Challenge hết hạn sau 3–5 phút.
* Một challenge chỉ dùng một lần.
* Lưu hash, không lưu OTP rõ.
* Tối đa 3 lần sai trên một challenge.
* Tối đa 5 lần sai trên một tài khoản trong 15 phút.
* Rate limit theo account, IP, device và terminal.
* Sau nhiều lần sai, áp dụng cooldown tăng dần.
* Không cho tạo challenge liên tục.
* Giới hạn số lần gửi lại OTP.
* Audit cả lần đúng và lần sai.
* Không log OTP hoặc PIN rõ.
* Không trả message phân biệt “tài khoản tồn tại” hay “mã quản lý đúng một phần”.

## 20.3 Đăng nhập

Sử dụng lockout của ASP.NET Identity hoặc cơ chế hiện tại:

* Giới hạn số lần đăng nhập sai.
* Cooldown hoặc khóa tạm thời.
* Cookie bảo mật.
* Session timeout phù hợp.
* Re-authentication cho hành động đặc biệt nhạy cảm nếu cần.

SystemAdmin không được bỏ qua tất cả kiểm soát chỉ vì là tài khoản kỹ thuật.

---

# 21. Bảo mật request

Bắt buộc kiểm tra:

* Anti-forgery token cho form MVC.
* Authorization policy ở backend.
* Không tin dữ liệu hidden input.
* Validate và normalize input.
* Không render lý do dưới dạng raw HTML.
* Không để lộ stack trace cho người dùng.
* Log lỗi kỹ thuật bằng correlation ID.
* Cookie `HttpOnly`.
* Cookie `Secure` trong production.
* `SameSite` phù hợp.
* Không ghi OTP, PIN hoặc token vào log.
* Không truyền thông tin nhạy cảm qua query string.
* Không dùng thời gian client làm nguồn quyết định.
* Không cho client tự gửi `StartTime`, `EndTime` hoặc `AutoCloseAt`.
* Không cho client tự gửi người phê duyệt mà không xác thực lại.
* Rate limit các endpoint mở ca, đóng ca, gửi OTP và xác nhận OTP.

---

# 22. Xử lý payment và đơn offline

## 22.1 Khi WorkShift hết hạn

* Không nhận order mới.
* Không bắt buộc hủy payment đang xử lý.
* Payment callback đã phát sinh trước thời điểm khóa vẫn phải được xử lý idempotent.
* Đơn phải giữ nguyên WorkShiftId.
* Không chuyển đơn sang WorkShift tiếp theo.

## 22.2 Khi mất mạng

* Không cho đóng thường nếu còn đơn offline.
* Hiển thị số lượng đơn chưa đồng bộ.
* Cho retry đồng bộ.
* Nếu bắt buộc đóng, sử dụng đóng ngoại lệ.
* Đơn đồng bộ sau vẫn thuộc WorkShift gốc.
* Báo cáo phải thể hiện WorkShift cần đối soát lại.

## 22.3 Callback thanh toán

Callback phải:

* Idempotent.
* Không ghi nhận thanh toán hai lần.
* Kiểm tra trạng thái hiện tại.
* Không từ chối callback hợp lệ chỉ vì WorkShift vừa hết hạn.
* Không chuyển payment sang WorkShift mới.
* Ghi nhận thời điểm phát sinh và thời điểm callback riêng nếu model hỗ trợ.

---

# 23. Giao diện StaffHub

## Khi có lịch

Hiển thị:

* Tên ca.
* Ngày làm.
* Giờ bắt đầu.
* Giờ kết thúc.
* Dấu hiệu qua ngày hôm sau.
* Giờ riêng nếu có.
* Trạng thái lịch.

Ví dụ:

```text
Ca tối
02/08/2026 22:00 → 03/08/2026 06:00
```

Không chỉ hiển thị:

```text
22:00–06:00
```

vì dễ gây hiểu nhầm ngày.

## Khi không có lịch

Hiển thị:

```text
Chưa có lịch — Thời gian nghỉ hoặc chưa được phân ca.
```

Không tự tạo lịch và không hiển thị “ca tự do”.

## Nhãn giao diện

Sử dụng:

* Lịch dự kiến.
* Mở POS ngoài lịch.
* Thời lượng phiên POS.
* Thời gian giữ trách nhiệm két.
* Phiên đã hết hạn.
* Chờ chốt két.
* Cần đối soát lại.

Không sử dụng:

* Chấm công.
* Giờ công.
* Đi làm thực tế.
* Tăng ca.
* Ca tự do.

---

# 24. Giao diện mở POS ngoài lịch

Modal phải hiển thị:

* Tên nhân viên.
* Cửa hàng.
* Terminal.
* Thời gian hiện tại.
* Trạng thái không có lịch phù hợp.
* Trường nhập lý do.
* Thông báo phiên chỉ được hoạt động tối đa 6 giờ.
* Thời điểm dự kiến tự hết hạn.
* Người hoặc phương thức phê duyệt.
* Trạng thái OTP.
* Nút xác nhận có chống double click.

Thông báo rõ:

```text
Việc mở POS ngoài lịch chỉ tạo phiên chịu trách nhiệm POS/két.
Hệ thống không tạo lịch làm việc hoặc dữ liệu chấm công.
```

---

# 25. Giao diện khi gần hết 6 giờ

Hiển thị countdown dựa trên:

```text
AutoCloseAtUtc - serverNow
```

Không tính countdown hoàn toàn từ đồng hồ client.

FE có thể dùng thời gian server trả về để đồng bộ.

Cảnh báo:

* 30 phút: cảnh báo nhẹ.
* 10 phút: cảnh báo nổi bật.
* 1 phút: yêu cầu chuẩn bị chốt.
* Hết hạn: khóa màn tạo đơn và mở modal chốt ca.

Nếu SignalR bị mất kết nối, backend vẫn phải chặn giao dịch sau khi hết hạn.

---

# 26. Giao diện đóng ca

Hiển thị:

* Thời gian bắt đầu.
* Thời gian hết hạn nếu là ngoài lịch.
* Thời lượng phiên POS.
* Tiền đầu ca.
* Tổng tiền mặt hợp lệ.
* Tiền cuối ca kỳ vọng.
* Tiền thực tế.
* Chênh lệch.
* Lý do chênh lệch.
* Số payment đang xử lý.
* Số đơn offline.
* Trạng thái đồng bộ.
* Trạng thái cần phê duyệt.
* Loại đóng: thông thường, hết hạn hoặc ngoại lệ.

Không hiển thị thời lượng dưới tên “giờ làm”.

---

# 27. Audit bắt buộc

Audit cho:

* Mở POS trong lịch.
* Mở POS trễ.
* Mở POS ngoài lịch.
* Phê duyệt ngoài lịch.
* OTP/PIN sai.
* OTP/PIN đúng.
* Worker cảnh báo hết hạn.
* WorkShift tự hết hạn.
* WorkShift rỗng được tự đóng.
* WorkShift chuyển chờ chốt.
* Đóng thông thường.
* Đóng ngoại lệ.
* Chênh lệch tiền.
* Phê duyệt chênh lệch.
* Đồng bộ đơn sau đóng.
* Reconcile WorkShift.
* Xung đột terminal.
* Duplicate request.
* Concurrency conflict.

Audit tối thiểu:

* Action.
* AccountId.
* StaffId.
* ApproverStaffId.
* StoreId.
* TerminalId.
* WorkShiftId.
* StaffShiftId nếu có.
* RequestKey.
* Lý do.
* Trạng thái trước.
* Trạng thái sau.
* Timestamp UTC.
* IP hoặc device information nếu hệ thống đang lưu.
* Kết quả thành công/thất bại.
* Correlation ID.

Không lưu OTP hoặc PIN rõ trong audit.

---

# 28. Dữ liệu cần cân nhắc bổ sung

Trước khi thêm cột, hãy tìm trường tương đương hiện có.

Nếu chưa có, cân nhắc tối thiểu:

## WorkShift

* `BusinessDate`.
* `SourceStaffShiftId` nullable.
* `OpenContext`:

  * WithinSchedule.
  * LateForSchedule.
  * OutsideSchedule.
* `OutsideScheduleReason`.
* `ApprovedByStaffId`.
* `ApprovedAtUtc`.
* `AutoCloseAtUtc`.
* `ExpiredAtUtc`.
* `CloseType`.
* `ClosedByStaffId`.
* `CloseReason`.
* `RequiresReconciliation`.
* `RowVersion`.

Không bắt buộc tạo toàn bộ nếu model đã có cơ chế khác biểu diễn tương đương.

`SourceStaffShiftId` là nullable:

* Có lịch thì có thể lưu nguồn.
* Ngoài lịch thì null.
* Không tạo StaffShift giả.

---

# 29. Kiến trúc triển khai

Tuân thủ Layered Architecture:

```text
Controller
→ Service
→ Repository
→ Database
```

## Controller

* Nhận request.
* Model validation.
* Lấy identity context.
* Gọi service.
* Không tự tính nghiệp vụ tiền.
* Không tự kiểm tra lịch phức tạp.
* Không trực tiếp truy cập DbContext nếu kiến trúc không cho phép.

## Service

* Phân loại lịch.
* Kiểm tra permission và scope thông qua abstraction hiện có.
* Xử lý phê duyệt.
* Tính AutoCloseAt.
* Tính tiền kỳ vọng.
* Tính chênh lệch.
* Xử lý idempotency.
* Điều phối transaction.
* Ghi audit.
* Gửi notification sau commit phù hợp.

## Repository

* Query lịch qua đêm.
* Query WorkShift đang mở.
* Lock dữ liệu khi cần.
* Thực hiện transaction.
* SaveChanges.
* Hỗ trợ concurrency.

Không đưa business rule quan trọng vào JavaScript hoặc View.

---

# 30. Error handling

Sử dụng error code ổn định, ví dụ:

* `POS_PERMISSION_REQUIRED`.
* `STORE_SCOPE_DENIED`.
* `TERMINAL_NOT_FOUND`.
* `TERMINAL_INACTIVE`.
* `TERMINAL_ALREADY_HAS_OPEN_SHIFT`.
* `STAFF_ALREADY_HAS_OPEN_SHIFT`.
* `OUTSIDE_SCHEDULE_REASON_REQUIRED`.
* `OUTSIDE_SCHEDULE_APPROVAL_REQUIRED`.
* `APPROVAL_EXPIRED`.
* `APPROVAL_ALREADY_USED`.
* `INVALID_APPROVER_SCOPE`.
* `WORKSHIFT_EXPIRED`.
* `WORKSHIFT_PENDING_CLOSE`.
* `WORKSHIFT_ALREADY_CLOSED`.
* `PAYMENT_IN_PROGRESS`.
* `OFFLINE_ORDERS_PENDING`.
* `CASH_DISCREPANCY_REASON_REQUIRED`.
* `CASH_DISCREPANCY_APPROVAL_REQUIRED`.
* `DUPLICATE_REQUEST`.
* `CONCURRENCY_CONFLICT`.

Message hiển thị cho người dùng bằng tiếng Việt, nhưng FE nên dựa vào error code thay vì so sánh chuỗi message.

---

# 31. Các trường hợp biên bắt buộc xử lý

1. Nhân viên mở POS lúc 23:59.
2. WorkShift hết hạn sau nửa đêm.
3. Nhân viên có lịch qua đêm từ ngày hôm trước.
4. Lịch bị hủy nhưng FE chưa refresh.
5. Nhân viên thay đổi `StoreId` trong request.
6. Terminal bị vô hiệu hóa sau khi modal đã mở.
7. Quản lý mất quyền trong lúc OTP đang chờ.
8. OTP hết hạn trước khi xác nhận.
9. Hai quản lý cùng phê duyệt.
10. Hai thiết bị cùng gửi mở WorkShift.
11. Double click mở ca.
12. Request timeout nhưng database đã insert.
13. Worker hết hạn chạy cùng lúc nhân viên đóng ca.
14. Payment callback đến sau khi WorkShift hết hạn.
15. Payment callback đến sau khi đóng ngoại lệ.
16. Đơn offline đồng bộ sau WorkShift đã đóng.
17. StartingCash bằng 0.
18. StartingCash lớn bất thường.
19. ActualEndingCash nhỏ hơn 0.
20. Chênh lệch bằng 0.
21. Chênh lệch vượt ngưỡng.
22. Một nhân viên đăng nhập trên hai máy.
23. Terminal có WorkShift cũ bị treo nhiều ngày.
24. Server restart trước thời điểm AutoCloseAt.
25. SignalR mất kết nối.
26. Background worker chạy nhiều instance.
27. RowVersion đã thay đổi.
28. Người dùng back browser và gửi lại form cũ.
29. RequestKey được dùng lại với body khác.
30. WorkShift ngoài lịch không có giao dịch và hết hạn.
31. WorkShift ngoài lịch có StartingCash nhưng không có đơn.
32. WorkShift có đơn nhưng toàn bộ là thanh toán không tiền mặt.
33. WorkShift còn payment pending.
34. Quản lý muốn tiếp tục bán sau 6 giờ.
35. Một nhân viên làm liên tục qua hai giai đoạn nhưng cửa hàng yêu cầu đối soát riêng.

---

# 32. Test case và tiêu chí nghiệm thu

## StaffHub và lịch

1. Không có lịch thì hiển thị “Chưa có lịch”.
2. Không tự tạo StaffShift.
3. Lịch qua đêm hiển thị đủ hai ngày.
4. Lịch ngày trước được tìm thấy khi bao phủ sau nửa đêm.
5. Lịch bị hủy không được sử dụng.
6. Không hiển thị chấm công hoặc giờ công.

## Mở POS

7. Không có permission thì bị từ chối.
8. Ngoài store scope thì bị từ chối.
9. Có lịch hợp lệ thì mở bình thường.
10. Mở trễ được phân loại đúng.
11. Không có lịch thì bắt buộc đi qua ngoài lịch.
12. Ngoài lịch thiếu lý do thì bị từ chối.
13. Ngoài lịch thiếu phê duyệt thì bị từ chối.
14. Người duyệt sai scope thì bị từ chối.
15. Mở ngoài lịch thành công không tạo StaffShift.
16. `AutoCloseAtUtc = StartTimeUtc + 6 giờ`.
17. Terminal không có hai WorkShift mở.
18. Nhân viên không có hai WorkShift xung đột.
19. Hai request đồng thời chỉ tạo một WorkShift.
20. Retry cùng RequestKey trả kết quả cũ.

## Hết hạn 6 giờ

21. Trước hạn vẫn nhận được đơn.
22. Đủ 6 giờ thì backend chặn đơn mới.
23. WorkShift chuyển `EXPIRED_PENDING_CLOSE`.
24. FE không thể bỏ qua khóa bằng cách sửa JavaScript.
25. WorkShift rỗng, StartingCash bằng 0 được tự đóng.
26. WorkShift có StartingCash không được tự điền ActualEndingCash.
27. WorkShift có giao dịch phải chờ kiểm đếm.
28. SignalR cảnh báo đúng Store.
29. Mất SignalR vẫn bị backend chặn.
30. Server restart không làm mất AutoCloseAt.
31. Worker chạy hai instance không xử lý trùng.
32. Muốn tiếp tục sau 6 giờ phải mở WorkShift mới.

## Đóng ca

33. Không đóng thường khi payment đang xử lý.
34. Không đóng thường khi còn đơn offline.
35. ExpectedEndingCash được tính ở backend.
36. CashDiscrepancy được tính ở backend.
37. Chênh lệch khác 0 bắt buộc lý do.
38. Vượt ngưỡng bắt buộc phê duyệt.
39. Đóng thành công giải phóng terminal.
40. Double click đóng ca không tạo hai lần xử lý.
41. RowVersion sai trả concurrency conflict.

## Offline và ngoại lệ

42. Đóng ngoại lệ bắt buộc permission.
43. Đóng ngoại lệ bắt buộc lý do.
44. Đơn offline giữ WorkShiftId cũ.
45. Đồng bộ muộn không chuyển doanh thu sang WorkShift mới.
46. WorkShift được đánh dấu cần đối soát.
47. Reconcile có audit.

## Chuyển ca

48. WorkShift cũ phải ngừng nhận đơn.
49. WorkShift cũ được đóng trước khi mở phiên mới.
50. StartingCash phiên mới do người nhận nhập.
51. Không tự copy toàn bộ ActualEndingCash.
52. Đơn ca cũ không chuyển sang phiên mới.
53. Ca qua nửa đêm vẫn giữ BusinessDate đúng.

## Bảo mật

54. OTP hết hạn không dùng được.
55. OTP không dùng lại được.
56. OTP của Store A không duyệt cho Store B.
57. OTP của nhân viên A không duyệt cho nhân viên B.
58. Quá số lần sai bị rate limit.
59. OTP và PIN không xuất hiện trong log.
60. Sửa StoreId trên request không mở rộng scope.

---

# 33. Những việc không được thực hiện

Không được:

1. Thêm chấm công.
2. Thêm bảng lương.
3. Thêm tính giờ làm.
4. Thêm tăng ca.
5. Dùng WorkShift để kết luận nhân viên làm đủ giờ.
6. Tự tạo StaffShift khi mở ngoài lịch.
7. Tự động nhập ActualEndingCash dựa trên ExpectedEndingCash.
8. Tự copy toàn bộ tiền cuối ca làm tiền đầu ca mới.
9. Cho phép WorkShift ngoài lịch hoạt động quá 6 giờ.
10. Gia hạn WorkShift cũ để bỏ qua quy trình đóng ca.
11. Chỉ chống double click bằng JavaScript.
12. Chỉ kiểm tra terminal trước transaction.
13. Tin StoreId, StaffId hoặc thời gian do client gửi.
14. Hard-code quyền chỉ bằng tên role.
15. Cho SystemAdmin mặc định bỏ qua kiểm soát nghiệp vụ.
16. Chuyển đơn offline sang WorkShift mới.
17. Đóng tài chính tự động khi chưa kiểm đếm tiền.
18. Thêm thư viện hoặc hạ tầng lớn khi cơ chế hiện tại đã đáp ứng.
19. Sửa POS Service ngoài phạm vi nếu không cần thiết.
20. Làm thay đổi các nghiệp vụ không liên quan.

---

# 34. Kết quả đầu ra bắt buộc

Sau khi inspect và hoàn thiện, hãy trả về:

## 34.1 Phân tích hiện trạng

* Luồng hiện tại đang hoạt động như thế nào.
* File, class, service, repository và JavaScript liên quan.
* Các lỗi nghiệp vụ hiện có.
* Các rủi ro bảo mật.
* Các rủi ro concurrency.
* Các phần có thể tái sử dụng.
* Các phần bắt buộc phải thay đổi.

## 34.2 Kế hoạch sửa

Trình bày theo thứ tự:

1. Database/model.
2. Repository.
3. Service.
4. Controller.
5. Authorization.
6. Background worker.
7. SignalR.
8. JavaScript.
9. View.
10. Audit.
11. Test.

## 34.3 Danh sách file sửa

Với mỗi file, ghi:

* Đường dẫn file.
* Mục đích sửa.
* Method hoặc vùng code sửa.
* Không ghi chung chung.

## 34.4 Mã nguồn

* Viết đầy đủ method được sửa.
* Không chỉ đưa pseudo-code nếu có đủ source để triển khai.
* Không bỏ `using`, dependency hoặc registration cần thiết.
* Tuân thủ async/await.
* Có transaction.
* Có cancellation token nếu kiến trúc hiện tại sử dụng.
* Có xử lý lỗi và audit.
* Không phá Layered Architecture.

## 34.5 Migration hoặc SQL

Nếu cần thay đổi database:

* Ghi rõ migration.
* Index.
* Unique constraint.
* Filtered index.
* Default value.
* Backfill dữ liệu cũ.
* Cách rollback.
* Không làm mất WorkShift hiện hữu.

## 34.6 Test

Bổ sung:

* Unit test service.
* Integration test database.
* Test concurrency.
* Test idempotency.
* Test worker.
* Test ca qua đêm.
* Test OTP brute-force.
* Test store scope.
* Test offline.
* Test SignalR ở mức phù hợp.

## 34.7 Báo cáo cuối

Chốt rõ:

* Đã sửa gì.
* Chưa sửa gì.
* Lý do.
* Rủi ro còn lại.
* Cấu hình cần thiết.
* Cách kiểm thử thủ công.
* Kết quả test thực tế.

Không được tuyên bố test pass nếu chưa thực sự chạy test.
