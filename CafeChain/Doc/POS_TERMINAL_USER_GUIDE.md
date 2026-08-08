# Hướng dẫn Terminal POS

## 1. Terminal POS là gì?

Terminal là định danh của một thiết bị hoặc quầy bán hàng, ví dụ `Quầy chính`, `Quầy mang đi` hoặc `POS 02`. Terminal không phải nhân viên, lịch làm việc hay WorkShift.

- `Shift`: mẫu giờ dự kiến.
- `StaffShift`: lịch đã phân cho nhân viên.
- `WorkShift`: phiên chịu trách nhiệm POS/két.
- `Terminal`: thiết bị/quầy mà WorkShift sử dụng.

Nhân viên bắt buộc phải chọn một Terminal active thuộc đúng cửa hàng trước khi mở POS. Một Terminal chỉ có tối đa một WorkShift ở trạng thái `OPEN`, `CLOSING` hoặc `EXPIRED_PENDING_CLOSE`.

## 2. Chọn Terminal để mở POS

1. Đăng nhập StaffHub.
2. Tại **Terminal POS**, chọn đúng quầy đang sử dụng.
3. Bấm **Mở POS**.
4. Backend kiểm tra Terminal tồn tại, active, đúng cửa hàng và đang trống.
5. StaffHub tiếp tục đánh giá đúng lịch, trễ hoặc ngoài lịch.
6. Sau khi đủ điều kiện, StaffHub chuyển sang POS; WorkShift chỉ được tạo khi người dùng bấm **Xác nhận mở ca** với tiền đầu ca.

Không thể sửa `TerminalId`, `StoreId` hoặc URL để vượt kiểm tra backend.

## 3. Khi nào cần đăng ký Terminal mới?

Chỉ đăng ký khi cửa hàng có thiết bị/quầy mới chưa tồn tại. Không tạo Terminal mới để né lỗi `TERMINAL_ALREADY_HAS_OPEN_SHIFT`.

Tài khoản demo:

| Vai trò | Tài khoản | Mục đích |
|---|---|---|
| Requester | `salesstaff@cafechain.vn` | gửi yêu cầu đăng ký |
| Ca trưởng | `shiftsupervisor@cafechain.vn` | nhận OTP ngoài lịch; mặc định không xác nhận Terminal |
| Quản lý chi nhánh | `storemanager@cafechain.vn` | xác nhận Terminal |

Mật khẩu demo đã seed: `The@1712`. Không dùng credential production trong tài liệu.

## 4. Requester gửi yêu cầu đăng ký

1. Đăng nhập `salesstaff@cafechain.vn` và mở StaffHub.
2. Bấm **Đăng ký terminal**.
3. Nhập tên dễ nhận biết.
4. Bấm **Gửi yêu cầu xác nhận Terminal**.
5. UI hiển thị đang chờ Store Manager/người có `POS.WorkShift.OverrideTerminal` trong đúng StaffScope.

Yêu cầu có TTL 5 phút, cooldown gửi lại và được bind với requester, approver, Store, Terminal cùng RequestKey. Đóng modal không làm mất yêu cầu; mở lại sẽ khôi phục trạng thái từ backend.

## 5. Store Manager xác nhận Terminal

1. Đăng nhập `storemanager@cafechain.vn`.
2. Mở **Thông báo** tại `/Admin/AdminNotifications` hoặc bấm popup **Có yêu cầu xác nhận POS mới** → **Mở Thông báo**.
3. Tìm notification **Yêu cầu OTP: Xác nhận đăng ký terminal POS**.
4. Bấm **Xem OTP**. Mã được điền vào ô xác nhận nhưng hệ thống không tự submit.
5. Kiểm tra tên Terminal, requester và cửa hàng.
6. Bấm **Xác nhận Terminal**.

Chỉ notification `REGISTER_POS_TERMINAL` hiển thị form **Xác nhận Terminal**. Backend tạo Terminal, consume OTP, resolve notification, audit và phát realtime trong một transaction. Double-click hoặc replay cùng RequestKey không được tạo hai Terminal.

## 6. Phân biệt với OTP mở POS ngoài lịch

Notification ngoài lịch có tên **Xác nhận mở POS ngoài lịch** và chỉ hiển thị **Xem OTP**. Ca trưởng/người duyệt cung cấp mã cho requester; requester nhập mã tại StaffHub rồi bấm **Xác nhận OTP**. Notification ngoài lịch tuyệt đối không hiển thị form **Xác nhận Terminal**.

Ưu tiên người duyệt OTP ngoài lịch:

```text
Ca trưởng
→ Store Manager
→ Area Manager
→ Business Owner
```

Candidate phải active, có email hợp lệ, không phải requester, có `POS.WorkShift.ApproveOutsideSchedule` và StaffScope chứa Store.

## 7. Gửi lại, hủy và hết hạn

- **Gửi lại OTP** chỉ hoạt động sau cooldown; mã cũ bị vô hiệu và expiry mới được phát realtime.
- **Hủy yêu cầu xác nhận Terminal** chuyển challenge chưa dùng sang `CANCELLED`, resolve notification và xóa protected OTP; không xóa audit.
- OTP hết hạn thật hiển thị `Expired`; requester phải tạo yêu cầu mới.
- SMTP lỗi không hủy challenge. Người duyệt vẫn dùng **Thông báo → Xem OTP**.
- Popup, SignalR, list DTO, notification body và log không chứa OTP plaintext.

## 8. Trạng thái và lỗi thường gặp

| Trạng thái/lỗi | Ý nghĩa |
|---|---|
| `Waiting` | đang chờ người duyệt |
| `Approved` | OTP mở ngoài lịch đã được requester xác minh |
| `Used` | Terminal đã được xác nhận hoặc OTP đã được consume |
| `Expired` | hết TTL |
| `Cancelled` | requester đã hủy |
| `Locked` | vượt số lần thử |
| `NO_ELIGIBLE_APPROVER` | không có người duyệt đúng quyền, email và scope |
| `OTP_CONTEXT_MISMATCH` | sai notification, challenge, requester hoặc Store |
| `TERMINAL_ALREADY_HAS_OPEN_SHIFT` | quầy đang có phiên chịu trách nhiệm |

Giờ gửi/hết hạn hiển thị theo `Asia/Ho_Chi_Minh`; countdown dùng `ExpiresAtUtc - ServerNowUtc`, không dựa vào đồng hồ trình duyệt.

## 9. PIN thao tác và Current Operator

1. Thẻ **PIN thao tác POS** tại StaffHub hiển thị **Chưa thiết lập** hoặc **Đã thiết lập** từ backend.
2. Sau khi lưu PIN, badge chuyển xanh, nút thành **Đổi PIN** và vẫn đúng khi reload.
3. Tại **Quản lý ca**, bấm **Đổi người thao tác**, chọn nhân viên và nhập PIN cá nhân của chính người sắp thao tác.
4. Chức năng này dùng để bàn giao thao tác bán hàng trên cùng quầy mà không phải đóng két rồi mở ca mới. Order, Payment và audit phát sinh sau đó ghi nhận nhân viên thực tế đang thao tác.
5. Trang Két và header bán hàng hiển thị người **Đang thao tác** cùng thời điểm đổi; các tab cùng Terminal/Store cập nhật qua SignalR.
6. `WorkShift.UserId`, người chịu trách nhiệm két, tiền đầu ca và trách nhiệm tài chính vẫn thuộc người mở ca. Đây không phải bàn giao két hay chuyển quyền sở hữu WorkShift.
7. Backend chỉ cho đổi khi account còn hoạt động, có `POS.Operator.Switch` và thuộc đúng StaffScope. PIN là mã cá nhân, không được chia sẻ.

## 10. Xử lý mở ca trễ từ 30 phút

1. Nhân viên nhập lý do và bấm **Gửi yêu cầu Manager** tại StaffHub; luồng này không dùng OTP.
2. Người có `POS.WorkShift.ApproveLateOpen` và đúng StaffScope nhận thông báo.
3. Từ **Thông báo**, bấm **Xem và duyệt yêu cầu** hoặc mở menu **Nhân sự & Vận hành → Duyệt mở ca trễ**.
4. Trễ từ 30 đến 45 phút: Manager có thể **Duyệt mở ca**, **Từ chối** hoặc **Chuyển ngoài lịch**.
5. Trễ trên 45 phút: **Duyệt mở ca** bị khóa; chỉ còn **Từ chối** và **Chuyển ngoài lịch**.
6. Nếu chuyển ngoài lịch, hệ thống đổi ngữ cảnh mở ca nhưng không sửa lịch nhân viên và không tạo WorkShift tại màn duyệt.
7. Requester sang POS nhập tiền đầu ca; WorkShift ngoài lịch chỉ được tạo khi bấm **Xác nhận mở ca**.
8. Nếu từ chối, requester không được tiếp tục bằng lịch cũ.

Tại POS trước khi xác nhận tiền đầu ca, **Hủy mở ca và quay lại StaffHub** sẽ kết thúc POS access session chưa bind, vô hiệu exchange context và hủy OTP/approval chưa dùng. Nếu WorkShift đã tồn tại, backend trả 409 và giữ nguyên ca.

## 11. Liên kết nghiệp vụ

- [Luồng người dùng StaffHub/POS](./STAFFHUB_USER_BUSINESS_FLOWS.md)
- [Quy tắc nghiệp vụ StaffHub/POS/WorkShift](./STAFFHUB_POS_WORKSHIFT_BUSINESS_RULES.md)
- [Hướng dẫn Dashboard/AI/Anomaly/Supplier](./DASHBOARD_AI_ANOMALY_SUPPLIER_USER_GUIDE.md)
