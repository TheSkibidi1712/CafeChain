# 🤖 TASK 5: BẢO MẬT ZERO-TRUST, AUDIT NHẬT KÝ & THIẾT LẬP DEPENDENCY INJECTION
> **Mục tiêu:** Rà soát và đảm bảo an ninh hệ thống chống lỗ hổng IDOR, cấu hình đầy đủ Dependency Injection trong `Program.cs`, kiểm soát phiên kết nối thiết bị POS và ghi nhận đầy đủ nhật ký vết kiểm toán (Audit Logs) cho các hành động đặc quyền.

---

## 1. TÀI LIỆU THAM KHẢO NGỮ CẢNH
* Cấu trúc CSDL và DTO tham khảo tại: [POS_AI_SYSTEM_INSTRUCTIONS.md](file:///d:/FPL_KY2/DATN/BE/CafeChain/POS_AI_SYSTEM_INSTRUCTIONS.md)
* Quy tắc kiến trúc & an ninh mạng: [dotnet-architecture.md](file:///d:/FPL_KY2/DATN/BE/CafeChain/.agent/rules/dotnet-architecture.md)

---

## 2. QUY TẮC KIẾN TRÚC BẮT BUỘC TUÂN THỦ (ARCHITECTURAL COMPLIANCE)
Model AI khi thực thi task này bắt buộc phải tuân thủ nghiêm ngặt các quy tắc kiến trúc sau:
1. **Thin Controller / Fat Service**: Đảm bảo tất cả các API AJAX tương tác từ giao diện client (Index.cshtml, pos-app.js) đến server chỉ gọi tới các phương thức API controller mỏng và được điều hướng xử lý trong Service Layer.
2. **Zero-Trust Security & Anti-IDOR**: Client JavaScript không bao giờ gửi `staffId` hay `storeId` trực tiếp lên server để truy vấn hay thanh toán, tất cả endpoints trên controller phải tự động phân giải qua server-side claims.
3. **No Direct Entity Exposure**: Phản hồi từ Ajax của các API (Ví dụ: `RegisterCustomer`, `CommitOrder`) chỉ nhận dữ liệu DTO/ViewModel được định dạng sẵn, không nhận thực thể DB gốc.

---

## 2. DANH SÁCH FILE CẦN THAO TÁC (TARGET FILES)
* 📄 [AdminPOSController.cs](file:///d:/FPL_KY2/DATN/BE/CafeChain/Areas/Admin/Controllers/AdminPOSController.cs) [MODIFY]
* 📄 [Program.cs](file:///d:/FPL_KY2/DATN/BE/CafeChain/Program.cs) [MODIFY]
* 📄 [pos-premium.css](file:///d:/FPL_KY2/DATN/BE/CafeChain/wwwroot/css/pos-premium.css) [MODIFY]

---

## 3. CÁC BƯỚC THỰC THI (STEP-BY-STEP INSTRUCTIONS)

### 🔹 Bước 1: Rà soát & Vá lỗ hổng IDOR tại `AdminPOSController.cs`
* Đảm bảo mọi API Endpoint trong `AdminPOSController` như `GetActiveShift`, `OpenShift`, `CloseShift`, `CommitOrder`, và `GetCloseShiftData` không bao giờ tin tưởng vào `userId` hoặc `storeId` được gửi từ Client thông qua Body hoặc Query URL.
* Luôn sử dụng phương thức private `ResolveUserStoreAsync()` (đọc trực tiếp từ cookie `Claims` ở Server) để giải quyết danh tính nhân viên và chi nhánh cửa hàng.

### 🔹 Bước 2: Theo vết an ninh mạng (IP Tracking) trong Attendance & Audit Logs
* Khi gọi `IAttendanceActionService.SubmitTimeActionAsync` từ `AttendanceController.cs`, truyền IP thực tế động trích xuất từ `HttpContext` của thiết bị client:
  ```csharp
  var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
  ```
* Gỡ bỏ mọi giá trị IPAddress gán cứng trong Attendance logs ở tầng database hoặc dịch vụ.

### 🔹 Bước 3: Cấu hình và Xác minh Dependency Injection trong `Program.cs`
* Đảm bảo các Repository và Service liên quan đến phân hệ POS đã được đăng ký Scope phù hợp:
  ```csharp
  builder.Services.AddScoped<IWorkShiftService, WorkShiftService>();
  builder.Services.AddScoped<ISupervisorAuthService, SupervisorAuthService>();
  builder.Services.AddScoped<IPOSOrderService, POSOrderService>();
  builder.Services.AddScoped<IPOSOrderRepository, POSOrderRepository>();
  builder.Services.AddScoped<ISupervisorRepository, SupervisorRepository>();
  ```
* Bật MemoryCache cho ứng dụng (`builder.Services.AddMemoryCache()`) để phục vụ lưu trữ đếm số lần nhập PIN sai (Brute-force lockout).

### 🔹 Bước 4: Tinh chỉnh Style hiển thị Modal Xác Thực Trưởng Ca & Đăng ký nhanh
* Mở file `wwwroot/css/pos-premium.css` và bổ sung các rules CSS dành riêng cho Modal Đăng ký nhanh (Hình 7) và nút `+`:
  ```css
  /* Quick register customer modal styles */
  #quickRegOverlay {
      display: none;
      position: fixed;
      inset: 0;
      z-index: 1000;
      background: rgba(15,23,42,0.8);
      align-items: center;
      justify-content: center;
      backdrop-filter: blur(4px);
  }
  #quickRegOverlay.active {
      display: flex;
  }
  
  .btn-quick-reg:hover {
      background: #16a34a !important;
      transform: scale(1.05);
  }
  ```

---

## 4. KẾ HOẠCH XÁC MINH (VERIFICATION PLAN)
* Chạy biên dịch dự án: `dotnet build`.
* Kiểm tra kiểm toán: Thực hiện gọi API `CommitOrder` với voucher bị bypass và kiểm tra cơ sở dữ liệu bảng `InvoiceAuditLogs` xem có được ghi nhận chính xác `SupervisorId`, `CashierId`, `ActionName = "SOFT_VOUCHER_BYPASS"`, và số tiền ưu đãi `DiscountValue` hay không.
* Thử nhập mã PIN sai liên tiếp 5 lần khi bypass: Hệ thống phải khóa quyền truy cập API xác thực PIN trong 15 phút, trả về thông báo lỗi lockout.
