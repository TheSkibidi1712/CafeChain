# Nghiệp vụ chuẩn StaffHub và phiên POS WorkShift

> Xem các tình huống màn hình → backend → kết quả tại [Phân tích luồng nghiệp vụ người dùng StaffHub](./STAFFHUB_USER_BUSINESS_FLOWS.md).

## 1. Phạm vi và thuật ngữ

Tài liệu này là nguồn nghiệp vụ chuẩn cho StaffHub và vòng đời phiên POS. Ba khái niệm phải tách biệt:

- `Shift`: mẫu giờ dự kiến của cửa hàng.
- `StaffShift`: lịch dự kiến đã phân cho nhân viên; không chứng minh nhân viên có mặt và không dùng tính lương.
- `WorkShift`: phiên chịu trách nhiệm POS/két, tiền đầu phiên, giao dịch và đối soát.

Hệ thống không tạo chấm công, giờ công, tăng ca, lương hoặc `StaffShift` giả khi mở POS ngoài lịch. Thuật ngữ giao diện dùng “lịch dự kiến”, “mở POS ngoài lịch”, “thời lượng phiên POS”, “chờ chốt két” và “cần đối soát lại”.

## 2. Thời gian và lịch qua đêm

Mọi mốc WorkShift được lưu UTC; lịch được tính tại `Asia/Ho_Chi_Minh`.

```text
PlannedStartLocal = WorkDate + (CustomStartTime ?? Shift.StartTime)
PlannedEndLocal   = WorkDate + (CustomEndTime   ?? Shift.EndTime)
Nếu PlannedEndLocal <= PlannedStartLocal thì cộng PlannedEndLocal thêm 1 ngày.
```

Khi mở POS phải tìm lịch ngày trước, ngày hiện tại và ngày tiếp theo trong cùng cửa hàng, bỏ lịch đã hủy. `BusinessDate` là `WorkDate` của lịch nguồn; với phiên ngoài lịch là ngày địa phương lúc mở và không đổi khi qua nửa đêm.

Chính sách mặc định:

- Mở sớm tối đa 30 phút.
- Trong lịch: từ mốc mở sớm đến 15 phút sau giờ bắt đầu.
- Mở trễ: sau 15 phút, tối đa 30 phút sau giờ kết thúc.
- Trễ trên 15 phút cần lý do; trễ trên 30 phút cần OTP.
- Ngoài các khoảng trên là `OUTSIDE_SCHEDULE`, luôn cần lý do 10–500 ký tự và OTP.

Các ngưỡng nằm trong `WorkShift` options, không hard-code ở view/controller.

## 3. Ngữ cảnh mở và vòng đời

`OpenContext` có `WITHIN_SCHEDULE`, `LATE_FOR_SCHEDULE`, `OUTSIDE_SCHEDULE`; `LEGACY` chỉ dùng backfill dữ liệu cũ.

```text
OPEN ──start close──────────────> CLOSING ──đối soát xong──> CLOSED
  │
  └─ngoài lịch đủ 6 giờ────────> EXPIRED_PENDING_CLOSE
                                  ├─kiểm đếm, đóng thường──> CLOSED
                                  └─đóng ngoại lệ──────────> RECONCILIATION_REQUIRED
                                                               └─reconcile──> CLOSED
```

- Chỉ `OPEN` được tạo đơn/thanh toán mới.
- `CLOSING`, `EXPIRED_PENDING_CLOSE`, `RECONCILIATION_REQUIRED`, `CLOSED` đều khóa giao dịch mới.
- Callback payment đã khởi tạo hợp lệ và đồng bộ muộn tiếp tục cập nhật WorkShift gốc, không chuyển sang phiên mới.
- Mỗi terminal và mỗi nhân viên chỉ có tối đa một phiên thuộc nhóm trách nhiệm active (`OPEN`, `CLOSING`, `EXPIRED_PENDING_CLOSE`).

## 4. Mở POS

Backend lấy account, staff, store và quyền từ identity; không tin `StaffId`, `StoreId` hay thời gian do client gửi. Terminal phải tồn tại, active và thuộc đúng store. Terminal mới chỉ được đăng ký sau OTP có permission `POS.WorkShift.OverrideTerminal` trong đúng scope.

Mở ngoài lịch không tạo `StaffShift`. Hệ thống đặt:

```text
AutoCloseAtUtc = StartTimeUtc + 6 giờ
```

Không gia hạn phiên cũ. Muốn tiếp tục bán phải đóng/đóng ngoại lệ, kiểm đếm và mở phiên mới bằng lý do, OTP và tiền đầu phiên mới. Không sao chép tự động tiền cuối phiên cũ.

## 5. Hết hạn và worker

Worker chạy mỗi phút, cảnh báo một lần tại 30, 10 và 1 phút, sau đó xử lý hết hạn. SignalR chỉ gửi tới group store/terminal/staff liên quan; backend vẫn là nguồn khóa khi SignalR mất kết nối.

Phiên chỉ tự đóng `AUTO_EMPTY_SHIFT` khi hoàn toàn rỗng: không order/payment/offline/reconciliation, tiền đầu phiên bằng 0 và không có dữ liệu cần kiểm đếm. Khi đó ba số tiền đều bằng 0. Mọi trường hợp khác chuyển `EXPIRED_PENDING_CLOSE`; không tự điền tiền thực tế.

## 6. Đóng, ngoại lệ và đối soát

Đóng thường:

```text
ExpectedEndingCash = StartingCash + tổng payment tiền mặt hợp lệ
CashDiscrepancy    = ActualEndingCash - ExpectedEndingCash
```

Dự án chưa có thực thể thu/chi két nên không giả định thêm thành phần. `ActualEndingCash` do người kiểm đếm nhập; backend tự tính hai số còn lại. Chênh lệch khác 0 cần lý do, vượt ngưỡng cần OTP. Không đóng thường khi payment đang xử lý hoặc còn manifest offline.

Đóng ngoại lệ cần permission, lý do, OTP và kiểm đếm tiền. Phiên chuyển `RECONCILIATION_REQUIRED`; đơn/payment muộn giữ nguyên `WorkShiftId`. Reconcile chỉ hoàn tất khi hết blocker, manifest hiện tại không còn đơn offline và số đơn offline đã đóng ngoại lệ đã được server xác nhận đồng bộ đầy đủ. Sau đó backend tính lại số liệu và ghi audit điều chỉnh.

## 7. Idempotency, concurrency và OTP

Các mutation dùng `RequestKey` ổn định. Retry cùng actor/action/store/payload trả kết quả cũ; cùng key khác payload bị từ chối; request đang xử lý có lease; dữ liệu được giữ tối thiểu 24 giờ.

Transaction, rowversion và filtered unique index cùng bảo vệ double-click, hai thiết bị, worker chạy đồng thời với đóng ca và nhiều application instance.

OTP gồm đúng 6 ký tự lấy ngẫu nhiên bằng nguồn mật mã từ `ABCDEFGHJKLMNPQRSTUVWXYZ23456789`; không bắt buộc mỗi mã có cả chữ và số. Backend từ chối ký tự đặc biệt, khoảng trắng nội bộ, chữ có dấu, emoji và `O/0/I/1`. OTP được hash, dùng một lần, hết hạn sau 5 phút và tối đa 3 lần sai/challenge. Trong cửa sổ 15 phút, backend giới hạn tối đa 5 challenge theo nhân viên, 10 theo terminal, 20 theo IP và 10 theo device fingerprint. Xác nhận sai cũng bị cộng dồn và chặn theo nhân viên/IP/device. IP và dấu vân tay thiết bị chỉ được lưu dưới dạng SHA-256, không lưu giá trị thô; resend chỉ được phép sau 60 giây và không xóa số lần nhập sai đã tích lũy. Challenge bind requester, approver, action, store, terminal, WorkShift và request key khi áp dụng. Permission/scope người duyệt được kiểm tra lại khi verify, resend và consume. Audit chỉ ghi metadata/kết quả, tuyệt đối không ghi OTP/PIN.

Người duyệt hợp lệ được ưu tiên theo thứ tự Ca trưởng → Quản lý chi nhánh → Quản lý vùng → Chủ doanh nghiệp → người khác có permission. OTP được gửi đồng thời qua Gmail bắt buộc và event SignalR riêng của đúng `ApproverStaffId`. `StaffNotification` loại `OPERATIONAL_OTP_REQUEST` chỉ chứa metadata; mã không nằm trong body, audit hoặc log. `OtpChallenge.ProtectedOtpPayload` giữ ciphertext Data Protection để đúng approver xem lại mã trong chuông khi challenge còn `Pending` và chưa hết hạn. API này không cache. Khi resend, mã cũ mất hiệu lực và cùng notification được cập nhật; khi verify/lock/cancel/expire, payload bị xóa và notification được resolve.

## 8. Permission

- `POS.WorkShift.View`
- `POS.WorkShift.Open`
- `POS.WorkShift.Close`
- `POS.WorkShift.OpenOutsideSchedule`
- `POS.WorkShift.ApproveOutsideSchedule`
- `POS.WorkShift.CloseException`
- `POS.WorkShift.Reconcile`
- `POS.WorkShift.OverrideTerminal`

`SeedAll.sql` là nguồn seed duy nhất. BusinessOwner, AreaManager, StoreManager và ShiftSupervisor nhận đủ tám quyền theo scope; SalesStaff nhận View/Open/Close/OpenOutsideSchedule. SystemAdmin, AccountantWarehouse và Customer không tự nhận quyền nghiệp vụ này.

## 9. API và mã lỗi

API chính:

- `POST /StaffHub/PreviewOpenPos` (anti-forgery, preflight không mutation)
- `POST /StaffHub/IssuePosToken` (chỉ gọi sau preview/xác nhận)
- `POST /api/v1/pos/shifts/open-assessment`
- `POST /api/v1/pos/shifts/open`
- `POST /api/v1/pos/shifts/{id}/start-closing`
- `POST /api/v1/pos/shifts/{id}/close`
- `POST /api/v1/pos/shifts/{id}/close-exception`
- `POST /api/v1/pos/shifts/{id}/reconcile`
- `POST /api/v1/pos/terminals/register`
- `POST /api/v1/pos/session/exchange`

StaffHub đánh giá lịch trước khi phát mã. `WITHIN_SCHEDULE` đi thẳng; `LATE_FOR_SCHEDULE` và `OUTSIDE_SCHEDULE` hiện modal chỉ để xác nhận, không nhập tiền/lý do/OTP. Nút Hủy không tạo exchange code. Sau khi tiếp tục, StaffHub phát mã trao đổi một lần 60 giây, chỉ lưu hash và chuyển qua URL fragment. React xóa fragment trước network I/O rồi POST đổi sang JWT; JWT không đi qua query string.

Frontend xử lý theo `errorCode`, gồm các mã ổn định: `POS_PERMISSION_REQUIRED`, `STORE_SCOPE_DENIED`, `TERMINAL_NOT_FOUND`, `TERMINAL_INACTIVE`, `TERMINAL_ALREADY_HAS_OPEN_SHIFT`, `STAFF_ALREADY_HAS_OPEN_SHIFT`, `OUTSIDE_SCHEDULE_REASON_REQUIRED`, `OUTSIDE_SCHEDULE_APPROVAL_REQUIRED`, `WORKSHIFT_EXPIRED`, `WORKSHIFT_PENDING_CLOSE`, `PAYMENT_IN_PROGRESS`, `OFFLINE_ORDERS_PENDING`, `DUPLICATE_REQUEST`, `CONCURRENCY_CONFLICT`.

## 10. Migration và dữ liệu

Phiên bản hiện hành dùng migration khởi tạo hợp nhất `20260802183312_InitialCreate`. Migration chỉ chứa schema, constraint và index; không seed RBAC. Nó dùng để tạo database mới, không được chạy chồng lên database đã có lịch sử migration cũ nếu chưa backup và lập kế hoạch baseline/chuyển dữ liệu.

Permission, role-permission và fixture tương thích schema mới nằm trong `Scripts/SeedAll.sql`. Script dùng business key, dọn temp table trước khi tái tạo và dùng marker `seedall_foundation_inventory_v1` để các batch nền không nhân đôi khi chạy lại.

## 11. Kiểm thử nghiệm thu tối thiểu

- Lịch qua đêm ngày trước được tìm đúng và hiển thị đủ hai ngày.
- Ngoài lịch cần lý do/OTP, không tạo StaffShift, hết hạn đúng 6 giờ.
- Hai request đồng thời chỉ tạo một WorkShift; retry cùng key không tạo thêm dữ liệu.
- Backend chặn order/payment sau hạn dù sửa JavaScript hoặc mất SignalR.
- Phiên rỗng tiền đầu 0 tự đóng; phiên có tiền/giao dịch chờ kiểm đếm.
- Đóng thường chặn payment/offline; đóng ngoại lệ và sync muộn giữ WorkShift gốc.
- OTP sai action/store/staff/terminal/request key, hết hạn hoặc reuse đều bị từ chối.
- Gmail và SignalR riêng tư nhận cùng mã; chỉ approver nhận event có mã.
- `StaffNotifications.Body`, audit và log không chứa OTP; payload xem lại là ciphertext; resend không tạo notification trùng.
- Sửa StoreId/StaffId ở payload không mở rộng quyền.
