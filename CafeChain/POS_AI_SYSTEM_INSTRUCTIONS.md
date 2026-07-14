# 🤖 HƯỚNG DẪN CẤU HÌNH & ĐẶC TẢ PHÁT TRIỂN HỆ THỐNG POS (CAFECHAIN)
## 📑 MASTER TECHNICAL SPECIFICATION & SYSTEM PROMPT SHEET
> **MỤC TIÊU:** Tài liệu này là bộ khung đặc tả kỹ thuật tối thượng (System Specs & Coding Prompt) tích hợp toàn bộ bối cảnh hệ thống, cấu trúc CSDL thực tế, API Contracts, các lỗi thuật toán cần vá, các phân hệ mới và đặc tả chi tiết **7 giao diện Figma trực quan**. Hãy cung cấp tài liệu này cho các mô hình AI thế hệ mới (LLMs) để tiến hành phát triển mã nguồn chính xác 100% không sai lệch.

---

## 1. 🏗️ KIẾN TRÚC HỆ THỐNG & BỐI CẢNH DỰ ÁN (TECH STACK & ARCHITECTURE)

Hệ thống POS (Point of Sale) tại quầy của chuỗi F&B CafeChain được lập trình theo tiêu chuẩn doanh nghiệp lớn:
* **Framework:** ASP.NET Core MVC, .NET 8 (Kiến trúc phân tầng N-Tier).
* **Database:** SQL Server, truy vấn thông qua Entity Framework Core (EF Core).
* **Frontend:** Razor Pages (HTML, CSS vanilla, jQuery, SweetAlert2, FontAwesome).
* **Biometrics:** Xác thực sinh trắc học Face ID qua client-side `face-api.js` (xuất vector 128 chiều) và đối chiếu khoảng cách toán học Euclidean ở Server-side C#.

---

## 2. 🗄️ CẤU TRÚC THỰC THỂ CSDL CHI TIẾT (EXACT DATABASE SCHEMAS)

Để đảm bảo không bao giờ gọi sai tên cột (Column Name), dưới đây là mã nguồn C# định nghĩa chính xác các Class Entity trong hệ thống:

```csharp
// ==========================================
// MODELS/STAFFS/STAFF.CS
// ==========================================
public class Staff {
    public int StaffId { get; set; }
    public int AccountId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? TaxCode { get; set; }
    public string? CCCD { get; set; }
    public int Gender { get; set; } // 0=Nữ, 1=Nam, 2=Khác
    public DateTime? StartDate { get; set; }
    public int EmployeeStatus { get; set; } // 1=Thử việc, 2=Chính thức, 3=Nghỉ việc
    public int SalaryType { get; set; } // 1=Fixed, 2=Hourly
    public decimal BaseSalary { get; set; }
    public decimal Allowance { get; set; }
    public decimal ProbationRate { get; set; }
    public decimal OvertimeRate { get; set; }
    public string? FaceDescriptor { get; set; } // Vector 128-dim dạng chuỗi JSON: "[0.12, -0.45, ...]"
    // NOTE (#143): Staff.PinHash removed — supervisor approval uses one-time OTP challenges, not fixed PIN.
    public int StoreId { get; set; }
    public string? AvatarUrl { get; set; }
    public bool Active { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual Store Store { get; set; } = null!;
    public virtual Account Account { get; set; } = null!;
}

// ==========================================
// MODELS/STAFFS/STAFFSHIFT.CS (Chấm công nhân sự)
// ==========================================
public class StaffShift {
    public int StaffShiftId { get; set; }
    public int StaffId { get; set; }
    public int? ShiftId { get; set; }
    public bool IsAdHoc { get; set; } // Ca tự do tự tạo lúc check-in
    public TimeSpan? CustomStartTime { get; set; }
    public TimeSpan? CustomEndTime { get; set; }
    public DateTime WorkDate { get; set; }
    public DateTime? ActualCheckIn { get; set; }
    public DateTime? ActualCheckOut { get; set; }
    public decimal? PayrollHours { get; set; } // Làm tròn 15 phút (0.25h, 0.5h, ...)
    public int StatusId { get; set; } // 1=PLANNED, 2=CHECKED_IN (In Progress), 3=COMPLETED, 4=BREAK

    public virtual Staff Staff { get; set; } = null!;
    public virtual Shift? Shift { get; set; }
    public virtual StaffShiftStatus Status { get; set; } = null!;
}

// ==========================================
// MODELS/STORES/POSTERMINAL.CS (Mới - Định danh thiết bị)
// ==========================================
public class PosTerminal {
    [Key]
    public string TerminalId { get; set; } = string.Empty; // GUID ẩn lưu ở LocalStorage
    public int StoreId { get; set; }
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty; // Tên thân thiện: "POS Quầy 1", "POS Take Away"
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public virtual Store Store { get; set; } = null!;
    public virtual ICollection<WorkShift> WorkShifts { get; set; } = new List<WorkShift>();
}

// ==========================================
// MODELS/STORES/WORKSHIFT.CS (Két tiền thu ngân POS)
// ==========================================
public class WorkShift {
    public int ShiftId { get; set; }
    public int StoreId { get; set; }
    public int UserId { get; set; } // StaffId thực hiện mở ca két
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public decimal StartingCash { get; set; } // Tiền lẻ đầu ca giao cho két
    public decimal ExpectedEndingCash { get; set; } // Tiền kỳ vọng = StartingCash + Cash Sales
    public decimal? ActualEndingCash { get; set; } // Tiền đếm tay thực tế khi đóng ca
    public string Status { get; set; } = "Open"; // "Open" | "Closed"
    public string? DiscrepancyReason { get; set; } // Lý do chênh lệch (bắt buộc nếu lệch != 0)
    public string? PosTerminalId { get; set; } // Khóa ca két gắn cứng theo thiết bị POS Terminal (FK)

    public virtual Store Store { get; set; } = null!;
    public virtual Staff User { get; set; } = null!;
    public virtual PosTerminal? PosTerminal { get; set; }
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}

// ==========================================
// MODELS/STAFFS/ATTENDANCELOG.CS (Nhật ký IP)
// ==========================================
public class AttendanceLog {
    public int Id { get; set; }
    public int UserId { get; set; }
    public int StoreId { get; set; }
    public DateTime CheckInTime { get; set; } = DateTime.UtcNow;
    public string IpAddress { get; set; } = string.Empty; // IP thực tế từ HttpContext
    public bool IsFaceVerified { get; set; }
    public string Status { get; set; } = "Valid";

    public virtual Staff User { get; set; } = null!;
    public virtual Store Store { get; set; } = null!;
}

// ==========================================
// MODELS/ORDERS/INVOICEAUDITLOG.CS (Mới - Audit Trưởng ca)
// ==========================================
public class InvoiceAuditLog {
    public int Id { get; set; }
    public int? OrderId { get; set; } // ID hóa đơn
    public int CashierId { get; set; } // StaffId của thu ngân
    public int SupervisorId { get; set; } // StaffId của Ca trưởng duyệt bypass
    public string ActionName { get; set; } = string.Empty; // "VOID_INVOICE", "SOFT_VOUCHER_BYPASS", "OPEN_SHIFT_LATE", "PRICE_OVERRIDE"
    public string Reason { get; set; } = string.Empty;
    public decimal? DiscountValue { get; set; } // Giá trị ưu đãi được áp dụng trong trường hợp Bypass Voucher
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public virtual Staff Cashier { get; set; } = null!;
    public virtual Staff Supervisor { get; set; } = null!;
}
```

---

## 3. 📄 HỢP ĐỒNG API & DTO CHI TIẾT (EXACT API CONTRACTS)

Mọi giao tiếp Client-Server qua AJAX bắt buộc phải tuân thủ nghiêm ngặt định dạng dữ liệu (DTO) dưới đây:

### A. DTOs định nghĩa trong `CafeChain.Application.DTOs.POS`
```csharp
public class POSOrderCommitDto {
    public List<POSOrderItemDto> Items { get; set; } = new List<POSOrderItemDto>();
    public int? CustomerId { get; set; }
    public string? VoucherCode { get; set; }
    public int PointsUsed { get; set; }
    public List<PaymentLineDto> Payments { get; set; } = new List<PaymentLineDto>(); // Phục vụ thanh toán hỗn hợp
    public int OrderTypeId { get; set; } = 1; // 1=DineIn, 2=TakeAway
    public decimal ReceivedAmount { get; set; }
    public string? Note { get; set; }
}

public class PaymentLineDto {
    public int PaymentMethodId { get; set; } // 1=Tiền mặt, 2=QR Chuyển khoản VietQR/PayOS
    public decimal Amount { get; set; }
}

public class POSOrderItemDto {
    public int DrinkId { get; set; }
    public int? SizeId { get; set; }
    public int Quantity { get; set; } = 1;
    public string? Note { get; set; }
    public List<POSOrderToppingDto> Toppings { get; set; } = new List<POSOrderToppingDto>();
}

public class POSOrderToppingDto {
    public int ToppingId { get; set; }
}

public class CloseShiftRequestDto {
    public decimal ActualEndingCash { get; set; }
    public string? DiscrepancyReason { get; set; }
}

public class QuickCustomerRegisterDto {
    public string Phone { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public DateTime? DateOfBirth { get; set; }
}

public class BypassAuthorizationRequest {
    public string Pin { get; set; } = null!;
    public string ActionName { get; set; } = null!; // "SOFT_VOUCHER_BYPASS", "OPEN_SHIFT_LATE", etc.
    public int? TargetId { get; set; }
    public string Reason { get; set; } = null!;
    public decimal? DiscountValue { get; set; }
}
```

### B. Mẫu API Endpoints
```csharp
// POST /Admin/AdminPOS/RegisterCustomer
[HttpPost]
public async Task<IActionResult> RegisterCustomer([FromBody] QuickCustomerRegisterDto dto) {
    // Logic: Sinh CustomerCode = "KH" + Ticks, lưu Customer & CustomerPhone có IsDefault = true
}

// NOTE (#143): AuthorizeBypass / fixed supervisor PIN removed.
// Supervisor approval uses OTP challenge request/verify/consume (OtpApprovalService + WorkShiftService).

// POST /Admin/AdminPOS/RegisterTerminal
[HttpPost]
public async Task<IActionResult> RegisterTerminal([FromBody] PosTerminalRegisterDto dto) {
    // Logic: Lưu hoặc cập nhật tên thân thiện cho PosTerminal qua TerminalId (GUID)
}
```

---

## 4. 🚨 CÁC LỖI THUẬT TOÁN CƠ BẢN CẦN SỬA NGAY (CORE BUG FIXES)

Khi tiến hành code dự án, bắt buộc phải sửa triệt để 4 lỗi logic hiện tại:

### Lỗi 1: Logic lọc chấm công 30 phút gây khóa POS (`HrAttendanceService.cs`)
* **Lỗi:** Logic cũ so sánh `CheckInTime >= DateTime.UtcNow.AddMinutes(-30)`. Nhân viên check-in quá 30 phút sẽ bị chặn không thể vào POS.
* **Cách sửa:** Đổi sang kiểm tra ca chấm công `StaffShift` đang mở:
  ```csharp
  public async Task<bool> VerifyRecentCheckInAsync(int userId, int storeId)
  {
      var today = DateTime.Today;
      var yesterday = today.AddDays(-1);
      return await _context.StaffShifts.AnyAsync(ss => 
          ss.StaffId == userId 
          && (ss.WorkDate.Date == today || ss.WorkDate.Date == yesterday)
          && ss.ActualCheckIn.HasValue 
          && !ss.ActualCheckOut.HasValue);
  }
  ```

### Lỗi 2: Thuật toán đối soát tiền mặt đóng ca bị sai dòng tiền (`WorkShiftService.cs`)
* **Lỗi:** Dòng code `if (totalCashSales == 0)` tự động lấy tổng doanh thu tất cả các đơn hàng gán làm Doanh thu tiền mặt khi trong ca không phát sinh đơn tiền mặt.
* **Cách sửa:** Loại bỏ khối lệnh `if (totalCashSales == 0)`. Tính chính xác tổng tiền mặt thu được thực tế bán ra. Nếu không có giao dịch tiền mặt, `totalCashSales = 0`. Tiền kỳ vọng cuối ca bằng đúng `StartingCash`.

### Lỗi 3: Lỗ hổng bảo mật áp dụng Voucher (`AdminPOSController.cs`)
* **Lỗi:** API `CommitOrder` tự tính giảm giá Voucher bằng truy vấn trực tiếp không qua kiểm tra nghiệp vụ ràng buộc.
* **Cách sửa:** Gọi dịch vụ `IAdminVoucherService.ValidateVoucherAsync` trên Server-side trước khi thanh toán.

### Lỗi 4: Ghi log địa chỉ IP ảo `"192.168.1.100"` (`AttendanceActionService.cs`)
* **Cách sửa:** Truyền IP thực tế từ API Controller gửi sang Service bằng `HttpContext.Connection.RemoteIpAddress?.ToString()`.

---

## 5. 🧮 ĐẶC TẢ CÁC THUẬT TOÁN NGHIỆP VỤ BẮT BUỘC (MANDATORY ALGORITHMS)

### A. Thuật toán so khớp vector khuôn mặt (FaceID Matching)
* **Input:** Vector nhân viên trong CSDL (`Staff.FaceDescriptor` dạng chuỗi JSON `float[]`) và Vector quét từ client gửi lên (`faceDescriptor`).
* **Thuật toán:** Tính toán khoảng cách Euclidean (Euclidean Distance).
* **Mã nguồn:**
  ```csharp
  private float CalculateEuclideanDistance(float[] source, float[] target)
  {
      if (source == null || target == null || source.Length != target.Length || source.Length == 0)
          return float.MaxValue;
      float sum = 0;
      for (int i = 0; i < source.Length; i++)
      {
          float diff = source[i] - target[i];
          sum += diff * diff;
      }
      return (float)Math.Sqrt(sum);
  }
  // Ngưỡng so khớp (Threshold) cho phép vượt qua là <= 0.6f (Chuẩn face-api)
  ```

### B. Thuật toán làm tròn 15 phút tính giờ lương (15-Minute Rounding)
* **Input:** Giờ chấm công vào thực tế (`ActualCheckIn`) và Giờ chấm công ra thực tế (`ActualCheckOut`).
* **Thuật toán làm tròn:**
  $$\text{PayrollHours} = \frac{\text{Round}(\text{RawHours} \times 4)}{4}$$
* **Mã nguồn:**
  ```csharp
  var totalMinutes = (checkOut - checkIn).TotalMinutes;
  var roundedMinutes = Math.Round(totalMinutes / 15.0, MidpointRounding.AwayFromZero) * 15;
  todayShift.PayrollHours = Math.Round((decimal)(roundedMinutes / 60.0), 2);
  ```

---

## 6. 🎨 7 THIẾT KẾ FIGMA GIAO DIỆN & YÊU CẦU UI/UX ĐẠT CHUẨN PREMIUM

Để hệ thống CafeChain POS đạt chất lượng hoàn mỹ và trải nghiệm tối ưu, mã nguồn HTML/CSS/JS phải tuân thủ nghiêm ngặt cấu trúc thiết kế từ 7 hình ảnh Figma sau:

### Hình 1: Màn hình Bán hàng POS chính (Main Sales POS Screen)
* **Header thanh lịch:**
  - Cổng POS phải tràn viền (Full-width), ẩn hoàn toàn thanh điều hướng Admin mặc định.
  - Hiển thị logo CafeChain góc trái kèm tên thương hiệu.
  - Góc giữa: Hai huy hiệu trạng thái: `Ca đang mở - 08:00` (icon két tiền, chấm tròn cam/xanh) và `Online` (chấm xanh lá). Hiển thị thêm tên thân thiện của thiết bị POS bên cạnh trạng thái.
  - Góc phải: Nút `Đóng ca` màu đỏ rực bo tròn 8px và nút `Quay lại` màu trắng viền đen.
* **Menu sản phẩm trực quan:**
  - Thanh tìm kiếm sản phẩm bo góc 12px viền nhạt.
  - Các tab danh mục bo tròn dạng kẹp viên thuốc (`border-radius: 20px`), tab đang hoạt động có màu cam đỏ gradient nổi bật.
  - **Lưới sản phẩm (Product Grid):** Card đồ uống có **bo góc 14px**, ảnh nền là mảng màu pastel đơn sắc dịu mắt kết hợp icon vector ở giữa cực kỳ sang trọng (thay vì ảnh sản phẩm thô). Tên sản phẩm hiển thị đậm màu đen và đơn giá màu cam tươi (VD: `35,000đ`) ngay dưới mảng màu.
* **Bảng giỏ hàng (Cart Panel bên phải):**
  - **Tìm kiếm khách hàng:** Ô nhập SĐT bo góc 10px kèm nút tìm kiếm màu cam đất. Bổ sung nút `+` màu xanh bên cạnh ô tìm kiếm khách hàng.
  - **Huy hiệu khách hàng thành viên:** Hiển thị thẻ màu xanh lá cây nhạt viền xanh đậm bo góc 10px: `Nguyen Van A — 2,500 điểm` kèm nút `x` đỏ góc phải để xóa nhanh.
  - **Order Type Toggle:** Nút toggle chuyển đổi dạng tab phẳng bo góc 10px: `Tại quán` (màu cam đỏ chủ đạo) và `Mang đi` (màu xám nền nhạt).
  - **Chi tiết dòng giỏ hàng:** Dưới tên đồ uống bắt buộc phải hiển thị **dòng chữ nhỏ mô tả Customization** thụt lề (VD: `Size M • Trân châu đen` hoặc `Size L • Thạch dừa`). Số lượng điều khiển bằng cụm nút `-` và `+` bo góc 10px phẳng, nút thùng rác màu đỏ nhạt bên phải.
  - **Tóm tắt thanh toán:** Căn biên phải, hiển thị: `Tạm tính`, `Voucher giảm` (màu xanh lục `-25,000đ`), `Điểm tích lũy` (màu xanh lục `-5,000đ`), và `Tổng cộng` chữ lớn đậm màu cam (VD: `170,000đ`).
  - **Nút Thanh Toán:** Màu cam rực gradient lớn bo góc 14px tràn viền góc dưới.

### Hình 2: Modal Mở Ca Két Tiền (Open WorkShift Modal)
* **Giao diện Modal dạng Card đứng bo góc 24px:**
  - Icon két tiền màu cam lớn trên nền tròn cam nhạt ở đầu.
  - Tiêu đề căn giữa: **Mở Ca Két Tiền** kèm mô tả nhỏ hướng dẫn đếm tiền lẻ.
* **Thẻ thông tin nhân viên (Staff Card):**
  - Khung xám bo góc 12px hiển thị Avatar viết tắt dạng chữ tròn (VD: `NV` màu cam), tên nhân viên: `Nguyễn Văn An`, chức danh: `Thu ngân (Cashier)`, chi nhánh: `Chi nhánh Quận 1`. Hiển thị Tên thân thiện của thiết bị POS (Ví dụ: `POS Quầy 1`) đang kết nối.
* **Nhập số tiền lẻ đầu ca:**
  - Nhãn `Tiền lẻ đầu ca (Starting Cash)` kèm icon tiền xu.
  - Ô nhập số tiền mặt thiết kế siêu lớn, viền nhạt bo tròn 14px, **chữ số màu xanh lá cây đậm nổi bật** (VD: `1,000,000`).
  - Phím đếm nhanh bo góc 10px ở dưới: `500K`, `1,000K`, `1,500K`, `2,000K`.
  - **Hộp thông tin màu vàng nhạt (Info Banner):** Icon bóng đèn cảnh báo nhắc nhở đếm kỹ tiền két lẻ. Nếu lệch định mức chuẩn 1.000.000đ, yêu cầu nhập số thực tế đếm được để hệ thống đánh dấu lệch ca trước.
* **Nút xác nhận:** `Xác Nhận Mở Ca` màu cam rực bo góc 14px kèm icon khóa.

### Hình 3: Modal Đóng Ca Két Tiền & Đối Soát (Close WorkShift Modal)
* **Giao diện Modal lớn toàn màn hình, chia 2 cột rõ rệt:**
  - Tiêu đề: **Đóng Ca Két Tiền** kèm phụ đề đếm tiền mặt thực tế và hoàn tất bàn giao ca.
* **Cột trái - Tóm tắt ca làm việc (WorkShift Summary):**
  - Các ô thông số dạng Grid bo góc 10px nền xám nhạt:
    - *Thời gian mở ca:* `08:00 AM`
    - *Thời gian hiện tại:* `16:35 PM`
    - *Thời lượng ca:* `8 giờ 35 phút`
    - *Tổng đơn hàng:* `47 đơn`
    - *Tiền lẻ đầu ca (Starting Cash):* `1,000,000đ`
  - **Bảng kê doanh thu ca:** Liệt kê dòng tiền rõ ràng:
    - *Tiền mặt thu được:* `3,450,000đ`
    - *Tiền qua QR / Chuyển khoản:* `2,180,000đ`
    - *Tiền thối ra:* `-650,000đ`
    - **Tổng doanh thu ròng:** Chữ in đậm màu cam đất cỡ lớn `4,980,000đ`.
* **Cột phải - Kiểm két thực tế & Cảnh báo lệch (Cash Audit & Discrepancy Alert):**
  - **Tiền mặt thực tế tại két (đếm tay):** Ô nhập số khổng lồ nổi bật chữ đậm đen `3,750,000` kèm phím đếm nhanh `500K` đến `5,000K`.
  - **Hộp cảnh báo chênh lệch màu hồng nhạt (Discrepancy Box):** Tự động xuất hiện bằng hiệu ứng mượt mà khi `ActualEndingCash != ExpectedEndingCash`.
    - Tiêu đề: `Phát hiện chênh lệch két tiền!` kèm icon cảnh báo đỏ.
    - So sánh: *Tiền mặt lý thuyết (Expected):* `3,800,000đ` | *Tiền mặt thực tế (Actual):* `3,750,000đ`.
    - Số tiền chênh lệch hiển thị siêu to màu đỏ tiêu cực: **`-50,000đ`** (hoặc xanh lục tích cực nếu thừa).
  - **Lý do chênh lệch (bắt buộc):** Một ô nhập textarea bo góc 12px yêu cầu bắt buộc giải trình lý do (VD: *"Thối nhầm cho khách đơn #1234"*). Nút Đóng ca sẽ **bị khóa (disabled)** cho đến khi điền lý do này.
* **Nút bấm dưới chân:** Nút `Hủy` (xám phẳng) và `XÁC NHẬN ĐÓNG CA` màu cam kèm icon khóa.

### Hình 4: Modal Thanh Toán Đa Kênh (Multi-channel Checkout Modal)
* **Giao diện Modal rộng, tích hợp menu Tab linh hoạt:**
  - Tiêu đề: **Thanh toán Đa Kênh** - Huy hiệu tổng tiền phải thu nổi bật `345,000đ`.
  Bo góc modal 20px.
* **Hệ thống Tab phương thức thanh toán:**
  - Tab 1: `Tiền mặt` (Có icon tiền xu, gạch chân màu cam đỏ hoạt động).
  - Tab 2: `Quét mã QR` (Icon mã QR).
  - Tab 3: `Chia thanh toán` (Icon split payment).
* **Cột trái - Bàn phím số Numpad ảo (Cash Numpad):**
  - Ô nhập `Tiền khách đưa` siêu lớn chữ đậm đen `500,000đ`.
  - Các phím đếm nhanh: `Đúng tiền`, `10K`, `20K`, `50K`, `100K`, `200K`, `500K`, `1,000K` và nút `Xóa` viền đỏ.
  - Bàn phím số Numpad ảo kích thước phím lớn bo góc 12px phù hợp chạm ngón tay trên màn hình POS cảm ứng.
* **Cột phải - Tích hợp VietQR / PayOS động & Tóm tắt:**
  - Khung quét mã QR VietQR nét đứt tinh tế bo góc 16px. Hiển thị mã QR động được sinh từ hệ thống ngân hàng liên kết.
  - Thông tin cổng thụ hưởng: Ngân hàng: `VietinBank` | Số tài khoản: `113000xxxxxx` | Chủ TK: `CAFECHAIN CORP`. Logo PayOS và VietQR ở dưới.
  - Đồng hồ đếm ngược thời gian hết hạn mã QR chuyển khoản: `Hết hạn trong 04:32`.
  - Bảng đối soát thu chi nhanh:
    - *Tổng tiền:* `345,000đ`
    - *Khách đưa:* `500,000đ`
    - *Tiền thối lại:* Chữ to màu xanh lục đậm **`155,000đ`**.
  - Nút Switch bật/tắt chế độ `Chia thanh toán` (Split payment) nhanh ở dưới cùng.
* **Nút chân:** Nút `Hủy` và Nút `XÁC NHẬN THANH TOÁN` màu cam lớn viền bo cong.

### Hình 5: OTP phê duyệt Trưởng ca (one-time OTP — #139–#143)
* **Giao diện OTP (không còn PIN 4 số cố định):**
  - Tiêu đề: **OTP phê duyệt** — mã 6 ký tự alphanumeric gửi email Ca trưởng.
  - Nhập OTP, gửi lại OTP (cooldown), thông báo OTP hết hạn / sai mã.
  - Không còn keypad PIN 4 số / `Staff.PinHash` / `AuthorizeBypass`.

### Hình 6: Modal/Màn hình Báo Giao Dịch Thành Công (Payment Success Screen)
* **Thiết kế giao diện:** Dạng thẻ Card dọc (Vertical Card) bo góc 24px cao cấp.
* **Header thông báo thành công:**
  - Biểu tượng tích xanh lá cây tròn (`border-radius: 50%`) ở trung tâm trên nền xanh lục gradient nhạt, mềm mại.
  - Tiêu đề đậm màu xanh lục: **Thanh toán thành công!**
  - Dòng mô tả phụ: *Giao dịch đã được ghi nhận vào hệ thống*.
* **Thông tin đơn hàng chi tiết (Order Invoice Detail):**
  - Thẻ nhỏ hiển thị Mã đơn bo góc 6px dạng xám phẳng: `ĐƠN #1247` kèm thời gian thực: `31/05/2026 • 14:35`.
  - **Bảng danh sách đồ uống (Itemized Bill):** Thiết kế tối giản, sạch sẽ:
    - Hiển thị số lượng màu xám phía trước tên (VD: `2x Cà phê sữa đá (M)`, `1x Trà đào cam sả (L)`).
    - Căn lề phải hiển thị tổng dòng tiền của món (VD: `90,000đ`, `55,000đ`).
* **Bảng kê tài chính (Financial Summary Card):**
  - Liệt kê: *Tạm tính* (`200,000đ`), *Voucher giảm* (in xanh lục nhạt `-25,000đ`), *Điểm tích lũy* (in xanh lục nhạt `-5,000đ`).
  - Dòng **Tổng thanh toán** nổi bật in đậm chữ cam lớn: **`170,000đ`**.
* **Hộp thông tin tiền thối lại (Change Return Box):**
  - Một khung chứa riêng có nền màu xanh lục nhạt bo tròn 16px, viền xanh lá mỏng.
  - Dòng chữ trên: *Tiền mặt: 200,000đ* ngăn cách bởi nét đứt tinh tế.
  - Tiêu đề xanh lục ở giữa: **TIỀN THỐI LẠI** và số tiền khổng lồ in đậm **`30,000đ`**.
* **Thanh thông tin tích điểm Loyalty:**
  - Khung màu cam/vàng rất nhạt bo góc 10px hiển thị icon ngôi sao cam: `★ Khách hàng tích được +8 điểm từ đơn hàng này`.
* **Cơ chế đóng tự động (Auto-redirect Countdown):**
  - Phía dưới hiển thị dòng chữ mờ: *Tự động chuyển sau 10 giây* để tự động đóng modal hoặc chuyển sang đơn mới nếu thu ngân không bấm nút.
* **Cặp nút hành động chân trang:**
  - Nút `In hóa đơn` màu trắng viền nhạt bo góc 12px có icon máy in (phía trái).
  - Nút `Đơn tiếp theo` màu cam đỏ chủ đạo cỡ lớn bo góc 12px có mũi tên chỉ sang phải (phía phải) để xóa sạch giỏ hàng hiện tại và quay lại màn hình POS sẵn sàng bán hàng.

### Hình 7: Modal Đăng Ký Nhanh Khách Hàng (Quick Customer Registration Modal)
* **Thiết kế giao diện:** Dạng thẻ popup nhỏ bo góc 20px.
* **Tiêu đề:** **Đăng Ký Nhanh Hội Viên**
* **Nội dung Form:**
  - Ô nhập SĐT: Bị vô hiệu hóa (disabled), tự động điền (pre-filled) số điện thoại từ ô tìm kiếm khách hàng trước đó.
  - Ô nhập Họ và tên: Bắt buộc (required), bo góc 10px.
  - Ô nhập Ngày sinh: Lựa chọn (optional) bo góc 10px để tính quà tặng sinh nhật.
* **Nút chân trang:** Nút `Hủy` và Nút `Xác nhận tạo` màu cam rực. Tự động lưu CSDL, đóng modal và gắn trực tiếp Customer vừa tạo vào hóa đơn hiện tại.

---

## 7. 🛠️ ĐẶC TẢ CHI TIẾT CÁC PHÂN HỆ CẦN LẬP TRÌNH MỚI (NEW FEATURES SPECIFICATION)

### 1. Phân hệ Ủy Quyền Trưởng Ca (Supervisor PIN Bypass System)
* **Yêu cầu giao diện (Client-side):**
  * Kích hoạt lớp phủ Backdrop mờ khóa toàn bộ tương tác bán hàng khi xảy ra thao tác nhạy cảm.
  * Hiển thị bảng số (Numpad) ảo và 4 ô vuông nhập mã PIN (chấm tròn cam đất đậm).
  * Hiển thị Banner bảo mật thử PIN: `"Còn 3 lần thử. Sai 5 lần sẽ khóa 15 phút."`
* **Lĩnh vực bảo mật Brute-force (Server-side):**
  * Tích hợp cơ chế khóa API trong 15 phút nếu nhập sai quá 5 lần. Sử dụng bộ nhớ đệm Cache hoặc bảng `RequestDeduplications` ghi lại số lần thử thất bại của `StaffId`.
* **Quy tắc Duyệt Ngoại Lệ Voucher (Voucher Bypass PIN):**
  * **Cho phép bypass đối với Lỗi Nghiệp Vụ Mềm:** Thiếu một phần nhỏ hạn mức tối thiểu (`MinOrderValue`), chính sách khách hàng VIP thân thiết, chiến dịch chăm sóc khách hàng đặc biệt của cửa hàng.
  * **CẤM TUYỆT ĐỐI bypass đối với các Lỗi Hệ Thống Nghiêm Trọng:** Voucher hết hạn sử dụng (`EndDate < Now`), Voucher đã bị khóa/thu hồi (`Active = false`), Voucher vượt quá lượt dùng tối đa, hoặc Voucher khóa đối tượng khách hàng.
  * Khi duyệt bypass, Server lưu lại `InvoiceAuditLog` ghi rõ: ID Ca trưởng phê duyệt, thời gian duyệt, lý do ngoại lệ, và số tiền ưu đãi được duyệt áp dụng (`DiscountValue`).

### 2. Phân hệ Chế độ Ngoại tuyến & Đồng bộ tự động (Offline Mode & Sync Engine)
* **Client-side Queue:** Lưu đơn hàng ngoại tuyến mã hóa vào `LocalStorage` dưới khóa `CafeChain_Offline_Orders`.
* **Đồng bộ tự động:** Khi có mạng, tự động duyệt mảng và đẩy tuần tự lên API `/Admin/AdminPOS/SyncOfflineOrders`. Sau khi Server phản hồi `success: true`, lập tức xóa đơn khỏi hàng đợi.
* **Server-side Recipe Inventory Deduction:**
  * Đồng bộ đơn offline lên Server phải trừ kho nguyên vật liệu tương ứng với `Recipe` của sản phẩm. Gọi `_inventoryDeductionService.DeductForOrder(newOrder.OrderId)`.

### 3. Phân hệ Thanh Toán Hỗn Hợp (Split Payments)
* **Nghiệp vụ:** Nhận mảng thanh toán `Payments` từ DTO `POSOrderCommitDto`.
* **Database mapping:**
  * Tạo các bản ghi thực thể `Payment` tương ứng.
  * Ví dụ:
    - QR/Bank Transfer: `PaymentMethodId = 2` | `Amount = 60,000đ` | `PaymentStatusId = 2 (Paid)`
    - Cash: `PaymentMethodId = 1` | `Amount = 40,000đ` | `PaymentStatusId = 2 (Paid)`
  * Tổng tiền thanh toán khớp chính xác đơn hàng.

### 4. Ràng buộc Két Tiền POS theo Thiết bị (POS Terminal Lock)
* **Đăng ký thiết bị:** Trình duyệt sinh một GUID ẩn lưu ở `LocalStorage`. Quản lý chi nhánh có giao diện đặt tên thân thiện (Ví dụ: "POS Quầy 1", "POS Take Away"). Tên này được lưu trong bảng `PosTerminals` và hiển thị trên mọi báo cáo ca két, đối soát két và nhật ký hoạt động.
* **Logic chặn mở ca trễ (Late Register Opening Guard):**
  * So sánh thời điểm mở ca két thực tế với giờ bắt đầu ca chấm công nhân sự được phân lịch hôm nay (`StaffShift.Shift.StartTime`).
  * Nếu: `Giờ hiện tại > Shift.StartTime + 30 phút` $\rightarrow$ yêu cầu OTP phê duyệt (`OPEN_SHIFT_LATE`) trước khi cho phép mở ca/bán hàng.
* **Hạn chế phiên két:** Mỗi ca két `WorkShift` liên kết với một `PosTerminalId` duy nhất trong DB.

### 5. Ràng buộc Giới Hạn Sử Dụng Điểm Thành Viên (Loyalty Point Safety Guard)
* **Server-side constraint:** `PointDiscount <= (SubTotal * 0.50)`.
* Ví dụ: Hóa đơn 100k, điểm tích lũy quy đổi tối đa là 50 điểm (tương ứng 50.000đ). Cấm thanh toán 100% hóa đơn bằng điểm.

### 6. Đăng ký nhanh Khách hàng hội viên (Quick Customer Registration Flow)
* **Logic Client-side:**
  * Thu ngân nhập SĐT $\rightarrow$ Tìm kiếm $\rightarrow$ Không tìm thấy khách hàng.
  * POS hiển thị nút `+` (Thêm mới) màu xanh bên cạnh ô nhập.
  * Click xác nhận $\rightarrow$ Mở Modal **Hình 7 (Đăng Ký Nhanh Hội Viên)**.
  * Trường SĐT được tự động điền và khóa chỉnh sửa $\rightarrow$ Thu ngân gõ Họ tên và nhấp tạo.
  * AJAX gọi API `POST /Admin/AdminPOS/RegisterCustomer` $\rightarrow$ Nhận phản hồi thành công $\rightarrow$ Tự động gán khách hàng vừa tạo vào hóa đơn hiện tại để bắt đầu tính điểm.
* **Logic Server-side:**
  * Kiểm tra trùng lặp SĐT trong bảng `CustomerPhones`. Nếu đã tồn tại, chặn lại trả về lỗi `400`.
  * Khởi tạo transaction:
    1. Tạo bản ghi `Customer` mới. Tự động sinh `CustomerCode = "KH" + DateTime.Now.Ticks`. Đặt `CurrentPoints = 0`, `TotalSpent = 0`, `Active = true`, hạng thành viên mặc định là `"New Member"`.
    2. Tạo bản ghi `CustomerPhone` mới liên kết tới `CustomerId`, gán `Phone` nhận từ client và thiết lập `IsDefault = true`.
  * Commit transaction và trả thông tin khách hàng mới về Client (Không tạo tài khoản đăng nhập `Account`).

### 7. Phân hệ Tự Động In Phiếu Chế Biến / Tem Nhãn Bar (Kitchen Ticket Auto Printing) - [MỚI]
* **Tách biệt kiến trúc (Separation of Concerns):** Phân tách độc lập hoàn toàn giữa bước "Đơn hàng xác nhận thành công" (`Order Confirmed`) và bước "Sinh/in phiếu chế biến" (`Print Production Ticket`) để phục vụ tích hợp KDS trong tương lai.
* **Quy trình hoạt động:**
  * Sau khi đơn hàng thanh toán thành công (online/cash) hoặc đơn hàng ngoại tuyến được đồng bộ thành công $\rightarrow$ POS Client gọi dịch vụ sinh dữ liệu in phiếu chế biến.
  * Dữ liệu in chứa: Mã đơn hàng, tên sản phẩm, các toppings đính kèm sản phẩm, ghi chú pha chế (`Note`) và thông tin customization (Size ly, lượng đường, lượng đá).
  * Gửi lệnh in tự động đến máy in tem nhãn nhiệt được cấu hình tại cửa hàng để nhân viên sử dụng dán lên ly nước, theo dõi thứ tự pha chế và chuyển cho Barista khu vực quầy Bar.

---

## 8. ĐẢM BẢO TÍNH MINH BẠCH & AN TOÀN BẢO MẬT (ZERO-TRUST SECURITY)

1. **Vá lỗ hổng IDOR:**
   * Tuyệt đối không nhận `accountId` từ client body/URL.
   * Lấy trực tiếp từ Cookie Claims bằng `User.FindFirst(ClaimTypes.NameIdentifier)?.Value`.
2. **Theo vết an ninh mạng (IP Tracking):**
   * Đọc IP thực tế: `HttpContext.Connection.RemoteIpAddress?.ToString()`.
3. **Mã hóa và băm mật khẩu:**
   * PIN Trưởng ca và mật khẩu tài khoản bắt buộc băm bằng **BCrypt** trước khi lưu.

---

## 9. TIÊU CHUẨN HIỆU NĂNG ỔN ĐỊNH (PERFORMANCE STANDARDS)

1. **Thin Controller - Fat Service:**
   * Controller chỉ đón nhận và điều phối. Logic xử lý, tính toán, và truy vấn DB nằm hoàn toàn ở lớp Service.
2. **Tối ưu Asynchronous:**
   * Tất cả thao tác đọc ghi dữ liệu từ SQL Server bắt buộc chạy bất đồng bộ thông qua `async/await`.
3. **Cơ chế Single-Tab/Device Lock:**
   * Sử dụng SignalR phát sóng sự kiện đóng/mở ca để tự động cập nhật hoặc khóa giao diện POS trên tất cả các phiên trình duyệt đang mở cùng thiết bị.

---
*(Hãy copy toàn bộ tài liệu này dán vào system prompt hoặc tệp tin ngữ cảnh huấn luyện cho bất kỳ AI Model nào lập trình tiếp dự án CafeChain).*
