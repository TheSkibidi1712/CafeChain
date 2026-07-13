# STAFFHUB BLUEPRINT – Tổng quan Kiến trúc & Luồng Người Dùng

> **Mục tiêu**: Định nghĩa StaffHub là cổng thông tin nhân viên (Employee Portal) duy nhất, tích hợp chấm công, lịch làm việc, cài đặt bảo mật và truy cập POS. Ở giai đoạn đầu chúng ta **tạm bỏ IP‑Geofencing** để tránh cản trở quá trình triển khai.

---

## 1. Kiến trúc tổng thể (High‑level Architecture)

```mermaid
graph TD
    A[Login Page] -->|Success| B[AccountController.RedirectByRole]
    B -->|Admin| C[Admin Dashboard]
    B -->|StaffHub Role| D[StaffHub Dashboard]
    D -->|Module A| D1[Profile & Schedule]
    D -->|Module B| D2[Attendance (Kiosk) Widget]
    D -->|Module C| D3[Settings (PIN / FaceID / Personal Info)]
    D -->|Module D| D4[POS Gateway (POS Access Card)]
    D1 -->|Select Shift| E[Shift Service]
    D2 -->|Check‑in / Check‑out| F[Attendance Service]
    D3 -->|Update PIN / Face| G[Security Service]
    D4 -->|POS Button| H[POS Access Guard]
    H -->|Allowed| I[POS UI]
    H -->|Denied| J[POS Locked View]
```

- **Login** → `AccountController` quyết định role và chuyển hướng.
- **StaffHub Dashboard** là một trang duy nhất (`Views/StaffHub/Index.cshtml`) bao gồm 4 mô-đun con.
- Mọi *action* (check‑in, cập nhật PIN, mở POS) đều gọi qua **Service Layer** để duy trì **N‑Tier Architecture** và tránh truy cập trực tiếp `DbContext`.

---

## 2. Các mô‑đun chính trên Dashboard

### A. Profile & Schedule (Module A)
- Hiển thị thông tin cá nhân: tên, role, chi nhánh.
- Lịch làm việc hôm nay (`StaffShift`): thời gian bắt đầu, kết thúc, trạng thái (chuẩn bị, đang làm, đã tan).
- **Edge Cases**: ca không tồn tại → hiển thị nút "Yêu cầu tạo ca tạm thời".

### B. Attendance (Module B – Kiosk Widget)
- **Trạng thái**: `Not Checked‑in`, `Checked‑in`, `Checked‑out`.
- Hai nút **Vào Ca** / **Tan Ca** kích hoạt `face-api.js` → gửi vector 128‑dim tới `AttendanceActionService`.
- **Bảo mật**:
  1. **IDOR fix** – API không nhận `accountId` từ client, lấy từ Claims.
  2. **Duplicate Check‑in Guard** – Trước khi xử lý, service kiểm tra `StaffShifts` có `ActualCheckIn != null && ActualCheckOut == null`.
  3. **Overnight Shift** – Khi tìm ca, mở rộng tìm ngày hôm trước nếu ca có `IsOvernight`.
- **Conflict Attendance** (nhiều tab):
  - Backend trả `409 Conflict` nếu đã check‑in.
  - Frontend hiển thị SweetAlert2 và tự `reload` hoặc dùng **SignalR** để push cập nhật.

### C. Settings (Module C)
| Tính năng | Mô tả | Ghi chú |
|---|---|---|
| **OTP phê duyệt** | Duyệt thao tác nhạy cảm bằng OTP one-time 6 ký tự (email Ca trưởng). | Không còn PIN cố định / `Staff.PinHash` (#143). |
| **Face ID** | Đăng ký lại khuôn mặt (3 góc: Straight, Left, Right). | Vector lưu trong `Staff.FaceDescriptor` (JSON). |
| **Thông tin cá nhân** | Đổi mật khẩu, cập nhật email, số điện thoại. | Thao tác qua `AccountService`. |

### D. POS Gateway (Module D)
- **Role‑based visibility**: chỉ hiển thị nút *“Đi tới POS”* cho `Cashier` và `ShiftSupervisor`.
- **Check‑in dependency**: Nút chỉ kích hoạt khi `Attendance` báo `Checked‑in`.
- Khi click → gọi `PosAccessGuard` (backend) kiểm tra `StaffShift` còn mở.
- Nếu không hợp lệ → trả về `403 Forbidden` và hiển thị **POS Locked View**.

---

## 3. Quy trình xử lý **Conflict Attendance** (Nhiều tab)

1. **Frontend** gửi yêu cầu `POST /api/Attendance/SubmitTimeAction`.
2. **Backend** (`AttendanceActionService`):
   - Query `StaffShifts` cho ngày hiện tại và `ActualCheckIn != null && ActualCheckOut == null`.
   - Nếu tồn tại → `return ServiceResult.Failure("Bạn đã vào ca ở một phiên làm việc khác.", statusCode: 409);`
3. **Frontend** nhận `409` → `SweetAlert2` hiện thông báo và gọi `window.location.reload()`.
4. **(Tùy chọn nâng cao)**: Dùng **SignalR** broadcast `AttendanceUpdated` tới tất cả session của cùng một `accountId` để tự động cập nhật UI mà không cần reload.

---

## 4. Bảng **Checklist** – Những việc cần thực hiện

| Giai đoạn | Công việc | Trạng thái |
|---|---|---|
| **Phase 1** | Đổi tên Kiosk → StaffHub (Controller, View, Route) | ☐ |
|  | Cập nhật `RedirectByRole` dùng `RoleConstants` và redirect `/StaffHub` | ☐ |
|  | Thêm `StoreId` claim trong `SignInAsync` | ☐ |
| **Phase 2** | Sửa IDOR trong các API Attendance (`SubmitTimeAction`, `RegisterFace`, `GetKioskData`, `FirstLoginChangePassword`) | ☐ |
|  | Refactor `MyBYOD` để gọi `IAttendanceActionService` thay vì DbContext trực tiếp | ☐ |
|  | Thêm guard `Duplicate Check‑in` và **Overnight Shift** logic | ☐ |
| **Phase 3** | Xây dựng UI StaffHub Dashboard (4 mô‑đun) và tích hợp `face‑api.js` | ☐ |
|  | Tạo `Settings` page (PIN, FaceID, Personal Info) | ☐ |
| **Phase 4** | Implement POS Access Guard & POS Locked view | ☐ |
|  | Tích hợp SignalR để push cập nhật Attendance | ☐ |

---

## 5. Kế hoạch thực hiện & Kiểm thử

### Automated
- `dotnet build && dotnet test` để đảm bảo không có compile error.
- Unit tests cho `AttendanceActionService` phải bao phủ:
  - IDOR protection (accountId từ Claims).
  - Duplicate check‑in guard.
  - Overnight shift lookup.

### Manual
1. **Login & Redirect**: Đăng nhập với Cashier → chuyển tới `/StaffHub/Index`.
2. **Attendance**: Click **Vào Ca** → camera bật, gửi vector, backend trả `200`. Mở tab thứ 2, click lại → nhận `409` và reload.
3. **POS Access**: Sau check‑in, nút “Đi tới POS” hiển thị và hoạt động; nếu chưa check‑in, nút khuyết và trả `403`.
4. **Settings**: Cập nhật FaceID, đổi mật khẩu – mọi thay đổi phải ghi vào DB và phản ánh trên UI. (PIN cố định đã gỡ #143; OTP one-time cho supervisor approval.)

---

## 6. Các tài nguyên & Định dạng UI (Premium Look)
- **Màu chủ đạo**: `#1e293b` (dark navy) + gradient `#0ea5e9 → #22c55e` cho các button.
- **Font**: Google Font **Inter**, kích thước 14‑16 px, line‑height 1.5.
- **Micro‑animations**: Hover‑scale 1.05 cho các cards, loading spinner khi chờ Face‑API.
- **Responsive**: Grid 2‑cột trên desktop, 1‑cột trên mobile.
- **Icons**: Feather Icons, SVG inline.

---

## 7. Kết luận
Bản Blueprint trên cung cấp **bức tranh tổng thể** cho StaffHub dưới dạng Employee Portal, mô tả rõ **luồng người dùng**, **kiến trúc backend**, **điều kiện bảo mật**, và **chiến lược xử lý conflict**. Khi các hạng mục trong checklist được hoàn thiện, StaffHub sẽ thay thế hoàn toàn Kiosk cũ và cung cấp trải nghiệm hiện đại, an toàn cho toàn bộ nhân viên cửa hàng.

Nếu bản này đáp ứng yêu cầu, hãy xác nhận để tôi bắt đầu cập nhật các file `.agent` và triển khai code theo thứ tự trong checklist.
