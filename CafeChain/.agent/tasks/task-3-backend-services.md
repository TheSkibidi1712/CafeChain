# 🤖 TASK 3: TRIỂN KHAI 7 PHÂN HỆ BACKEND & API ENDPOINTS MỚI
> **Mục tiêu:** Xây dựng logic nghiệp vụ toàn diện cho 7 phân hệ mới: PIN bypass voucher mềm, lưu trữ/đồng bộ hóa ngoại tuyến trừ kho, thanh toán hỗn hợp, khóa thiết bị & mở ca trễ, giới hạn điểm loyalty, đăng ký nhanh khách hàng và tự động in phiếu chế biến.

---

## 1. TÀI LIỆU THAM KHẢO NGỮ CẢNH
* Cấu trúc CSDL và DTO tham khảo tại: [POS_AI_SYSTEM_INSTRUCTIONS.md](file:///d:/FPL_KY2/DATN/BE/CafeChain/POS_AI_SYSTEM_INSTRUCTIONS.md)
* Quy tắc kiến trúc & an ninh mạng: [dotnet-architecture.md](file:///d:/FPL_KY2/DATN/BE/CafeChain/.agent/rules/dotnet-architecture.md)

---

## 2. QUY TẮC KIẾN TRÚC BẮT BUỘC TUÂN THỦ (ARCHITECTURAL COMPLIANCE)
Model AI khi thực thi task này bắt buộc phải tuân thủ nghiêm ngặt các quy tắc kiến trúc sau:
1. **Repository Pattern**: Tất cả truy cập cơ sở dữ liệu phải được thực hiện thông qua Interface Repository thay vì tiêm hoặc truy vấn trực tiếp `AppDbContext` trong các class Service hoặc Controller.
2. **Thin Controller**: Controller chỉ chịu trách nhiệm điều phối request, check `ModelState`, gọi Service và trả về View/JSON. Không viết logic nghiệp vụ hay thực hiện lưu/sửa dữ liệu trực tiếp trong controller.
3. **No Direct Entity Exposure**: Tuyệt đối không expose thực thể DB (`Staff`, `Store`) trực tiếp cho Client. Tất cả giao tiếp qua API đều phải sử dụng các lớp DTO/ViewModel an toàn đã được định nghĩa.
4. **Zero-Trust Security & Anti-IDOR**: Trích xuất danh tính (ID, StoreId) trực tiếp từ Cookie Claims ở Server-side, tuyệt đối không tin tưởng các trường input ẩn hoặc query parameters được truyền từ client.
5. **Asynchronous (Async/Await)**: Mọi thao tác truy cập I/O database bắt buộc sử dụng phương thức bất đồng bộ.

---

## 2. DANH SÁCH FILE CẦN THAO TÁC (TARGET FILES)
* 📄 [IPOSOrderRepository.cs](file:///d:/FPL_KY2/DATN/BE/CafeChain/Infrastructure/Interfaces/Admin/POS/IPOSOrderRepository.cs) [MODIFY]
* 📄 [POSOrderRepository.cs](file:///d:/FPL_KY2/DATN/BE/CafeChain/Infrastructure/Repositories/Admin/POS/POSOrderRepository.cs) [MODIFY]
* 📄 [ISupervisorAuthService.cs](file:///d:/FPL_KY2/DATN/BE/CafeChain/Application/Interfaces/POS/ISupervisorAuthService.cs) [MODIFY]
* 📄 [SupervisorAuthService.cs](file:///d:/FPL_KY2/DATN/BE/CafeChain/Application/Services/POS/SupervisorAuthService.cs) [MODIFY]
* 📄 [IWorkShiftService.cs](file:///d:/FPL_KY2/DATN/BE/CafeChain/Application/Interfaces/POS/IWorkShiftService.cs) [MODIFY]
* 📄 [WorkShiftService.cs](file:///d:/FPL_KY2/DATN/BE/CafeChain/Application/Services/POS/WorkShiftService.cs) [MODIFY]
* 📄 [IPOSOrderService.cs](file:///d:/FPL_KY2/DATN/BE/CafeChain/Application/Interfaces/POS/IPOSOrderService.cs) [MODIFY]
* 📄 [POSOrderService.cs](file:///d:/FPL_KY2/DATN/BE/CafeChain/Application/Services/POS/POSOrderService.cs) [MODIFY]
* 📄 [AdminPOSController.cs](file:///d:/FPL_KY2/DATN/BE/CafeChain/Areas/Admin/Controllers/AdminPOSController.cs) [MODIFY]
* 📄 [AttendanceController.cs](file:///d:/FPL_KY2/DATN/BE/CafeChain/Controllers/AttendanceController.cs) [MODIFY]

---

## 3. CÁC BƯỚC THỰC THI (STEP-BY-STEP INSTRUCTIONS)

### 🔹 Bước 1: Nâng cấp `IPOSOrderRepository` và `POSOrderRepository`
* **Trong `IPOSOrderRepository.cs`:**
  Thêm các phương thức:
  ```csharp
  Task<InvoiceAuditLog?> GetPendingAuditLogAsync(int cashierId, string actionName, int windowMinutes);
  Task UpdateAuditLogOrderIdAsync(int auditLogId, int orderId);
  Task<bool> HasDuplicatePhoneAsync(string phone);
  Task<Customer> RegisterCustomerAsync(Customer customer, CustomerPhone phone);
  Task<PosTerminal?> GetTerminalByIdAsync(string terminalId);
  Task CreateTerminalAsync(PosTerminal terminal);
  Task UpdateTerminalAsync(PosTerminal terminal);
  ```
* **Trong `POSOrderRepository.cs`:**
  Triển khai các phương thức trên:
  ```csharp
  public async Task<InvoiceAuditLog?> GetPendingAuditLogAsync(int cashierId, string actionName, int windowMinutes)
  {
      var cutoff = DateTime.Now.AddMinutes(-windowMinutes);
      return await _context.InvoiceAuditLogs
          .Where(al => al.CashierId == cashierId && al.ActionName == actionName && al.OrderId == null && al.CreatedAt >= cutoff)
          .OrderByDescending(al => al.CreatedAt)
          .FirstOrDefaultAsync();
  }

  public async Task UpdateAuditLogOrderIdAsync(int auditLogId, int orderId)
  {
      var log = await _context.InvoiceAuditLogs.FindAsync(auditLogId);
      if (log != null)
      {
          log.OrderId = orderId;
          await _context.SaveChangesAsync();
      }
  }

  public async Task<bool> HasDuplicatePhoneAsync(string phone)
  {
      return await _context.CustomerPhones.AnyAsync(cp => cp.Phone == phone);
  }

  public async Task<Customer> RegisterCustomerAsync(Customer customer, CustomerPhone phone)
  {
      using var transaction = await _context.Database.BeginTransactionAsync();
      try
      {
          _context.Customers.Add(customer);
          await _context.SaveChangesAsync();

          phone.CustomerId = customer.CustomerId;
          _context.CustomerPhones.Add(phone);
          await _context.SaveChangesAsync();

          await transaction.CommitAsync();
          return customer;
      }
      catch
      {
          await transaction.RollbackAsync();
          throw;
      }
  }

  public async Task<PosTerminal?> GetTerminalByIdAsync(string terminalId)
  {
      return await _context.PosTerminals.FindAsync(terminalId);
  }

  public async Task CreateTerminalAsync(PosTerminal terminal)
  {
      _context.PosTerminals.Add(terminal);
      await _context.SaveChangesAsync();
  }

  public async Task UpdateTerminalAsync(PosTerminal terminal)
  {
      _context.PosTerminals.Update(terminal);
      await _context.SaveChangesAsync();
  }
  ```

### 🔹 Bước 2: Nâng cấp `ISupervisorAuthService` & `SupervisorAuthService` để hỗ trợ Voucher Override
* **Trong `ISupervisorAuthService.cs`:**
  Cập nhật phương thức:
  ```csharp
  Task<ServiceResult> AuthorizePinAsync(string pin, int cashierId, int storeId, string actionName, int? targetId, string reason, decimal? discountValue = null);
  ```
* **Trong `SupervisorAuthService.cs`:**
  1. Cập nhật phương thức triển khai để nhận `discountValue` và gán vào `InvoiceAuditLog`.
  2. Phân loại lỗi voucher khi thực hiện bypass. Nếu `actionName` là `"SOFT_VOUCHER_BYPASS"`:
     - Gọi `IAdminVoucherService.ValidateVoucherAsync` để tìm voucher.
     - Kiểm tra nếu lỗi do hết hạn, bị khóa, hết lượt dùng thì cấm bypass tuyệt đối:
       `return ServiceResult.Failure("CẤM BYPASS: Voucher đã hết hạn, bị khóa hoặc hết lượt sử dụng.");`
     - Chỉ cho phép bypass lỗi mềm như chưa đạt giá trị đơn tối thiểu (`MinOrderValue`).
  3. Ghi `discountValue` vào `InvoiceAuditLog.DiscountValue` khi lưu.

### 🔹 Bước 3: Nâng cấp `IWorkShiftService` & `WorkShiftService` hỗ trợ Thiết bị & Mở ca trễ
* **Trong `IWorkShiftService.cs`:**
  Cập nhật phương thức:
  ```csharp
  Task<ServiceResult> OpenShiftAsync(int userId, int storeId, decimal startingCash, string? posTerminalId);
  ```
* **Trong `WorkShiftService.cs`:**
  1. Cập nhật hàm `OpenShiftAsync` lưu `posTerminalId` vào `newShift.PosTerminalId`.
  2. Thêm kiểm tra Mở ca trễ:
     - Tìm ca làm việc hôm nay của nhân viên: `StaffShift` có `StaffId == userId` và `WorkDate == DateTime.Today` và `ShiftId != null`.
     - Nếu có và không phải `IsFreeShift`: So sánh giờ hiện tại với `Shift.StartTime`.
     - Nếu trễ hơn 30 phút, kiểm tra xem có `InvoiceAuditLog` nào của cashier đó với `ActionName == "OPEN_SHIFT_LATE"` trong 5 phút qua không.
     - Nếu không có, chặn lại và trả về lỗi: `LATE_OPENING_REQUIRES_BYPASS|Ca của bạn bắt đầu lúc...`
     - Nếu có, liên kết `InvoiceAuditLog.OrderId` với `newShift.ShiftId` để xác nhận đã tiêu dùng bypass mở ca trễ.

### 🔹 Bước 4: Nâng cấp `IPOSOrderService` & `POSOrderService`
* **Trong `IPOSOrderService.cs`:**
  Thêm phương thức đăng ký nhanh khách hàng:
  ```csharp
  Task<ServiceResult<object>> RegisterCustomerAsync(QuickCustomerRegisterDto dto);
  ```
* **Trong `POSOrderService.cs`:**
  1. Triển khai `RegisterCustomerAsync`:
     - Kiểm tra trùng SĐT qua `_repository.HasDuplicatePhoneAsync`. Nếu trùng trả về `Failure`.
     - Tạo `Customer` mới: `CustomerCode = "KH" + DateTime.Now.Ticks`, `FullName = dto.FullName`, `MemberLevelId = 1` (New Member), `CurrentPoints = 0`, `TotalSpent = 0`, `Active = true`.
     - Tạo `CustomerPhone` mới: `Phone = dto.Phone`, `IsDefault = true`.
     - Gọi `_repository.RegisterCustomerAsync` và trả về thông tin Customer.
  2. Cập nhật `CommitOrderAsync`:
     - **Tích hợp bypass Voucher:** Khi `VoucherCode` được truyền lên:
       - Gọi `_voucherService.ValidateVoucherAsync(...)`.
       - Nếu validate thất bại, tìm kiếm pending `SOFT_VOUCHER_BYPASS` audit log trong 5 phút qua của Cashier này.
       - Nếu tìm thấy, áp dụng voucher với giá trị giảm giá được duyệt `DiscountValue` trong audit log, và liên kết log này với OrderId mới tạo.
       - Nếu không tìm thấy, ném lỗi validate như bình thường.
     - **Thanh toán hỗn hợp:** Tạo nhiều bản ghi `Payment` tương ứng với mảng `dto.Payments`. Nếu `dto.Payments` trống, tạo mặc định 1 dòng theo `PaymentMethodId` cũ.

### 🔹 Bước 5: Viết API Endpoints mới trong các Controllers
* **Trong `AttendanceController.cs`:**
  Thêm endpoint:
  ```csharp
  [HttpPost("AuthorizeBypass")]
  public async Task<IActionResult> AuthorizeBypass([FromBody] BypassAuthorizationRequest request)
  {
      if (!TryGetAccountId(out int cashierId))
          return Unauthorized(new { success = false, message = "Chưa đăng nhập." });

      // Trích xuất storeId của cashier hiện tại để thực thi
      var kioskDataResult = await _actionService.GetKioskDataAsync(cashierId);
      if (!kioskDataResult.IsSuccess)
          return BadRequest(new { success = false, message = kioskDataResult.Message });
      
      dynamic data = kioskDataResult.Data;
      int storeId = data.storeId;

      var result = await _supervisorAuthService.AuthorizePinAsync(
          request.Pin, cashierId, storeId, request.ActionName, request.TargetId ?? 0, request.Reason, request.DiscountValue);

      if (!result.IsSuccess)
          return BadRequest(new { success = false, message = result.Message });

      return Ok(new { success = true, message = result.Message });
  }
  ```
* **Trong `AdminPOSController.cs`:**
  1. Thêm endpoint đăng ký nhanh:
     ```csharp
     [HttpPost("RegisterCustomer")]
     public async Task<IActionResult> RegisterCustomer([FromBody] QuickCustomerRegisterDto dto)
     {
         var result = await _posOrderService.RegisterCustomerAsync(dto);
         if (!result.IsSuccess)
             return BadRequest(new { success = false, message = result.Message });
         return Ok(new { success = true, data = result.Data });
     }
     ```
  2. Thêm endpoint đăng ký thiết bị POS Terminal:
     ```csharp
     [HttpPost("RegisterTerminal")]
     public async Task<IActionResult> RegisterTerminal([FromBody] PosTerminalRegisterDto dto)
     {
         var terminal = await _repository.GetTerminalByIdAsync(dto.TerminalId);
         if (terminal == null)
         {
             terminal = new PosTerminal
             {
                 TerminalId = dto.TerminalId, Name = dto.Name, StoreId = dto.StoreId, Active = true, CreatedAt = DateTime.Now
             };
             await _repository.CreateTerminalAsync(terminal);
         }
         else
         {
             terminal.Name = dto.Name;
             terminal.StoreId = dto.StoreId;
             await _repository.UpdateTerminalAsync(terminal);
         }
         return Ok(new { success = true, message = "Đăng ký thiết bị thành công." });
     }
     ```
  3. Thêm API đồng bộ offline trừ kho:
     ```csharp
     [HttpPost("SyncOfflineOrders")]
     public async Task<IActionResult> SyncOfflineOrders([FromBody] List<POSOrderCommitDto> orders)
     {
         // Lặp qua danh sách đơn hàng offline, gọi CommitOrderAsync cho từng đơn.
         // Gọi IInventoryDeductionService.DeductStockForOrderAsync sau khi commit thành công.
     }
     ```

---

## 4. KẾ HOẠCH XÁC MINH (VERIFICATION PLAN)
* Chạy biên dịch dự án: `dotnet build`.
* Kiểm tra API `RegisterCustomer` bằng cách gửi request POST qua Postman hoặc Ajax:
  `{ "Phone": "0987654321", "FullName": "Khách Test POS" }`
  $\rightarrow$ Kỳ vọng trả về `success = true` và dữ liệu khách hàng.
* Kiểm tra nhập PIN Trưởng ca bypass voucher không đủ điều kiện đơn tối thiểu $\rightarrow$ Verify xem log `InvoiceAuditLogs` có sinh đúng `DiscountValue` và sau đó đơn hàng được commit thành công.
