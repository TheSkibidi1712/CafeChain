# 🤖 TASK 1: SỬA 4 LỖI THUẬT TOÁN CỐT LÕI (BUG FIXES)
> **Mục tiêu:** Vá triệt để các lỗi logic nghiêm trọng hiện tại liên quan đến an ninh chấm công, tính sai dòng tiền mặt ca két, bỏ qua xác thực voucher ở server, và gán cứng địa chỉ IP log.

---

## 1. TÀI LIỆU THAM KHẢO NGỮ CẢNH
* Cấu trúc CSDL và DTO tham khảo tại: [POS_AI_SYSTEM_INSTRUCTIONS.md](file:///d:/FPL_KY2/DATN/BE/CafeChain/POS_AI_SYSTEM_INSTRUCTIONS.md)
* Quy tắc kiến trúc & an ninh mạng: [dotnet-architecture.md](file:///d:/FPL_KY2/DATN/BE/CafeChain/.agent/rules/dotnet-architecture.md)

---

## 2. QUY TẮC KIẾN TRÚC BẮT BUỘC TUÂN THỦ (ARCHITECTURAL COMPLIANCE)
Model AI khi thực thi task này bắt buộc phải tuân thủ nghiêm ngặt các quy tắc kiến trúc sau:
1. **Repository Pattern**: Tất cả truy cập cơ sở dữ liệu phải được thực hiện thông qua Interface Repository thay vì tiêm hoặc truy vấn trực tiếp `AppDbContext` trong các class Service hoặc Controller.
2. **Thin Controller**: Controller chỉ chịu trách nhiệm điều phối request, check `ModelState`, gọi Service và trả về View/JSON. Không viết logic nghiệp vụ trong controller.
3. **No Direct Entity Exposure**: Tuyệt đối không expose thực thể DB (`Staff`, `Store`) trực tiếp cho Client. Tất cả giao tiếp qua API đều phải sử dụng các lớp DTO/ViewModel an toàn đã được định nghĩa.
4. **Zero-Trust Security & Anti-IDOR**: Trích xuất danh tính (ID, StoreId) trực tiếp từ Cookie Claims ở Server-side, tuyệt đối không tin tưởng các trường input ẩn hoặc query parameters được truyền từ client.
5. **Asynchronous (Async/Await)**: Mọi thao tác truy cập I/O database bắt buộc sử dụng phương thức bất đồng bộ.

---

## 2. DANH SÁCH FILE CẦN THAO TÁC (TARGET FILES)
* 📄 [HrAttendanceService.cs](file:///d:/FPL_KY2/DATN/BE/CafeChain/Application/Services/Attendance/HrAttendanceService.cs)
* 📄 [WorkShiftService.cs](file:///d:/FPL_KY2/DATN/BE/CafeChain/Application/Services/POS/WorkShiftService.cs)
* 📄 [AdminPOSController.cs](file:///d:/FPL_KY2/DATN/BE/CafeChain/Areas/Admin/Controllers/AdminPOSController.cs)
* 📄 [AttendanceActionService.cs](file:///d:/FPL_KY2/DATN/BE/CafeChain/Application/Services/Attendance/AttendanceActionService.cs)

---

## 3. CÁC BƯỚC THỰC THI (STEP-BY-STEP INSTRUCTIONS)

### 🔹 Bước 1: Sửa lỗi khóa POS quá 30 phút trong `HrAttendanceService.cs`
* **Vấn đề:** Logic cũ quét log chấm công trong 30 phút gây khóa POS của cashier đang làm việc.
* **Hành động:** 
  1. Gỡ bỏ logic so sánh `DateTime.UtcNow.AddMinutes(-30)`.
  2. Thay bằng truy vấn CSDL kiểm tra xem nhân viên (`userId`) có ca chấm công `StaffShift` nào trong ngày hôm nay hoặc hôm qua đang hoạt động (đã check-in và chưa check-out: `ss.ActualCheckIn.HasValue && !ss.ActualCheckOut.HasValue`) hay không.
  3. Giữ lệnh `return true;` ở trên cùng kèm chú thích `// BYPASS_MODE` để dễ bật/tắt khi debug.

### 🔹 Bước 2: Sửa lỗi đối soát tiền mặt đóng ca trong `WorkShiftService.cs`
* **Vấn đề:** Logic `if (totalCashSales == 0)` tự động gán toàn bộ doanh thu (cả chuyển khoản QR) thành tiền mặt.
* **Hành động:** 
  1. Xóa hoàn toàn khối lệnh `if (totalCashSales == 0)` (dòng 98-104).
  2. Đảm bảo `totalCashSales` chỉ tính tổng tiền mặt của các hóa đơn có phương thức thanh toán tiền mặt (`PaymentMethodId == 1`).
  3. Tính toán tiền thối khách hàng: `ExpectedEndingCash = StartingCash + totalCashSales - Tiền thối` (nếu có).

### 🔹 Bước 3: Sửa lỗ hổng bảo mật voucher ở `AdminPOSController.cs`
* **Vấn đề:** `CommitOrder` tự tính giảm giá voucher mà không gọi hàm xác thực điều kiện voucher.
* **Hành động:**
  1. Tiêm `IAdminVoucherService` vào constructor của `AdminPOSController`.
  2. Trong API Action `CommitOrder`, thay thế logic tính voucher thô sơ bằng cách gọi:
     `var voucherResult = await _voucherService.ValidateVoucherAsync(dto.VoucherCode, dto.CustomerId ?? 0, subTotal);`
  3. Nếu `voucherResult.Success == false`, chặn đơn hàng ngay lập tức và trả về mã lỗi `400 BadRequest` cùng thông báo từ service.

### 🔹 Bước 4: Sửa lỗi IPAddress gán cứng trong `AttendanceActionService.cs`
* **Vấn đề:** API ghi log IP Address mặc định `"192.168.1.100"`.
* **Hành động:**
  1. Thêm tham số `string ipAddress` vào định nghĩa phương thức `SubmitTimeActionAsync` trong interface `IAttendanceActionService` và file triển khai `AttendanceActionService.cs`.
  2. Cập nhật nơi gọi API trong `AttendanceController.cs` để trích xuất IP thực tế thiết bị:
     `var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";`
     Truyền `clientIp` vào lệnh gọi service.

---

## 4. KẾ HOẠCH XÁC MINH (VERIFICATION PLAN)
* Chạy `dotnet build` đảm bảo không có lỗi biên dịch.
* Test thử chấm công bằng Face ID: Xem IP trong bảng `AttendanceLogs` có khớp với IP thật hoặc Loopback `::1` không (thay vì `192.168.1.100`).
* Test thử áp voucher sai điều kiện lên API `CommitOrder`: Server phải trả lỗi `400` thay vì vẫn cho tạo đơn thành công.
* Đóng ca két tiền mặt khi ca đó chỉ có doanh thu chuyển khoản: Tiền kỳ vọng cuối ca `ExpectedEndingCash` phải bằng đúng `StartingCash` đầu ca.
