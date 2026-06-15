# 🤖 TASK 4: TÁI CẤU TRÚC RAZOR VIEW & JAVASCRIPT HỖ TRỢ 7 FIGMA SCREENS
> **Mục tiêu:** Cải tiến giao diện POS tại tệp Razor View và file Javascript tương ứng để đáp ứng hoàn toàn trải nghiệm người dùng theo 7 màn hình Figma Premium, tích hợp logic đăng ký nhanh khách hàng, quản lý thiết bị POS Terminal (GUID), in tự động, và các luồng modal PIN bypass.

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
* 📄 [Index.cshtml](file:///d:/FPL_KY2/DATN/BE/CafeChain/Areas/Admin/Views/AdminPOS/Index.cshtml) [MODIFY]
* 📄 [pos-app.js](file:///d:/FPL_KY2/DATN/BE/CafeChain/wwwroot/js/pos-app.js) [MODIFY]

---

## 3. CÁC BƯỚC THỰC THI (STEP-BY-STEP INSTRUCTIONS)

### 🔹 Bước 1: Điều chỉnh HTML trong `Index.cshtml`
* **Header POS (Hình 1):** Bổ sung hiển thị tên thiết bị POS đang hoạt động:
  ```html
  <span id="headerTerminalName" style="font-size:12px;opacity:0.8;margin-left:6px;background:rgba(255,255,255,0.15);padding:2px 8px;border-radius:4px;"> POS Thiết Bị</span>
  ```
* **Tìm kiếm khách hàng (Hình 1):** Bổ sung nút `+` (Đăng ký nhanh) màu xanh lá cạnh nút tìm kiếm:
  ```html
  <button type="button" class="btn-quick-reg" id="btnOpenQuickReg" onclick="openQuickRegModal()" style="padding: 8px 12px; border: none; border-radius: 10px; background: #22c55e; color: white; cursor: pointer;"><i class="fas fa-plus"></i></button>
  ```
* **Modal Đăng ký nhanh Khách hàng mới (Hình 7) [NEW]:** Bổ sung mã HTML modal này ở cuối file:
  ```html
  <!-- ====== QUICK REGISTER CUSTOMER MODAL (Hình 7) ====== -->
  <div class="pos-modal-overlay" id="quickRegOverlay">
      <div class="shift-modal" style="width: 360px;">
          <h5 style="text-align: center; font-weight: 800; margin-bottom: 12px;">Đăng Ký Nhanh Hội Viên</h5>
          <div class="mb-3">
              <label class="form-label" style="font-size:12px;font-weight:600;">Số điện thoại</label>
              <input type="text" id="quickRegPhone" class="form-control" disabled style="background:#f1f5f9;cursor:not-allowed;font-weight:600;text-align:center;">
          </div>
          <div class="mb-3">
              <label class="form-label" style="font-size:12px;font-weight:600;">Họ và tên <span class="text-danger">*</span></label>
              <input type="text" id="quickRegName" class="form-control" placeholder="Nhập tên khách hàng" required style="border-radius:10px;">
          </div>
          <div class="mb-3">
              <label class="form-label" style="font-size:12px;font-weight:600;">Ngày sinh (Tùy chọn)</label>
              <input type="date" id="quickRegDob" class="form-control" style="border-radius:10px;">
          </div>
          <div style="display:flex;gap:10px;margin-top:16px;">
              <button class="btn-pin-cancel" onclick="closeQuickRegModal()" style="flex:1;">Hủy</button>
              <button class="btn-checkout" onclick="submitQuickRegister()" style="flex:1;box-shadow:none;border-radius:10px;">Xác nhận tạo</button>
          </div>
      </div>
  </div>
  ```

### 🔹 Bước 2: Cập nhật JavaScript quản lý Thiết bị (Terminal GUID)
* Khi POS load lần đầu, kiểm tra `localStorage` xem đã có `pos_terminal_id` chưa.
* Nếu chưa có, sinh một GUID ngẫu nhiên lưu vào `localStorage`.
* Gửi AJAX gọi `/Admin/AdminPOS/RegisterTerminal` để khai báo thiết bị lên DB:
  `{ TerminalId: guid, Name: "Thiết bị POS " + guid.substring(0, 5), StoreId: storeId }`
* Hiển thị Tên thiết bị lên Header và các thẻ thông tin mở ca.

### 🔹 Bước 3: Cải tiến logic Mở ca két tiền (Hình 2) & Chốt mở ca trễ
* Khi nhân viên nhấn "Mở ca", gửi `posTerminalId` kèm theo lên backend API `/OpenShift`.
* **Xử lý trễ ca (>30 phút):**
  - Nếu API mở ca phản hồi mã lỗi/thông báo yêu cầu bypass trễ ca (`LATE_OPENING_REQUIRES_BYPASS`):
    1. Hiển thị Lớp phủ xác thực PIN Trưởng ca (Hình 5).
    2. Gán nhãn sự kiện bảo mật: `pinActionBadge.textContent = "Mở ca trễ > 30 phút"`.
    3. Khi Trưởng ca nhập mã PIN hợp lệ, gọi API `AuthorizeBypass` kèm `ActionName: "OPEN_SHIFT_LATE"`.
    4. Nếu bypass thành công, tự động gọi lại lệnh Mở ca két tiền và cho phép vào hệ thống POS.

### 🔹 Bước 4: Tích hợp Thanh toán động VietQR & Split Payments (Hình 4)
* **VietQR / PayOS động:**
  - Khi chọn tab "Quét mã QR", gọi API tạo hóa đơn tạm và lấy `checkoutUrl` (từ PayOS).
  - Sinh mã QR và hiển thị trực tiếp lên modal checkout.
  - Tự động chạy đồng hồ đếm ngược 5 phút. Lắng nghe Webhook hoặc API check status để duyệt đơn tự động.
* **Chia thanh toán (Split Payments):**
  - Cho phép người dùng bật Switch chia thanh toán.
  - Hiển thị danh sách phương thức (Tiền mặt, QR) và ô nhập tiền mặt tương ứng.
  - Kiểm tra xem tổng tiền mặt + tiền QR có khớp chính xác `SumTotal` hay không trước khi mở khóa nút "Xác nhận thanh toán".

### 🔹 Bước 5: Phân hệ In tự động phiếu pha chế (Bar Ticket Auto Print)
* Sau khi thanh toán thành công, hiển thị Modal Success (Hình 6).
* Tách biệt luồng in: Khi modal success mở hoặc khi nhấn "In hóa đơn", kích hoạt hàm JavaScript `printReceipt()`:
  - Sinh một payload HTML hoặc văn bản tối giản chứa thông tin pha chế: Mã đơn, Tên nước, Size ly, Customization (Size, đường, đá, toppings), Note pha chế.
  - Gửi lệnh in (hoặc gọi `window.print()` bằng layout CSS in nhiệt riêng biệt) để chuyển giao cho Barista pha chế.

---

## 4. KẾ HOẠCH XÁC MINH (VERIFICATION PLAN)
* Kiểm tra tải trang POS: Xác minh xem GUID thiết bị có tự động tạo trong `localStorage` và cập nhật tên thiết bị lên header không.
* Nhấp nút `+` cạnh tìm kiếm khách hàng khi SĐT trống hoặc chưa tìm $\rightarrow$ Hệ thống phải yêu cầu nhập SĐT trước.
* Nhập SĐT lạ $\rightarrow$ Tìm kiếm báo không thấy $\rightarrow$ Bấm nút `+` $\rightarrow$ Modal Đăng ký nhanh (Hình 7) phải hiển thị với SĐT điền sẵn và bị khóa. Điền tên bấm lưu $\rightarrow$ Khách hàng được gắn vào giỏ hàng thành công.
* Thử áp dụng voucher không đủ điều kiện tối thiểu $\rightarrow$ POS hiển thị modal PIN Trưởng ca $\rightarrow$ Nhập đúng PIN bypass $\rightarrow$ Đơn hàng áp dụng voucher thành công.
* Test in nhiệt: Bấm nút `In hóa đơn` trên Modal Success và xem kết quả format in Barista.
