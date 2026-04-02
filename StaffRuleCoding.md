# STAFF MODULE: STRICT CODING RULES & TECHNICAL ACCEPTANCE CRITERIA

**Bối cảnh:** Bạn đang thực thi Epic "Quản lý nhân sự - Phân quyền phi tập trung". Để đảm bảo chất lượng code chuẩn Enterprise, không phát sinh Hidden Bugs và an toàn tuyệt đối, bạn BẮT BUỘC phải tuân thủ 6 Quy tắc lập trình (Coding Rules) dưới đây trong suốt quá trình triển khai 8 Phases.

## 🛑 RULE 1: CHỐNG LỖI ĐỆ QUY DỮ LIỆU (CIRCULAR REFERENCE PREVENTION)
* **Tuyệt đối không** trả về (return) các Entity gốc của Entity Framework (như `Staff`, `Account`) trực tiếp ra Controller hoặc View.
* **Bắt buộc:** Tầng Service phải thực hiện Mapping (thủ công hoặc dùng AutoMapper) từ Entity sang ViewModels (`StaffIndexVM`, `StaffEditVM`). Các Navigation Properties (`Staff.Account`, `Staff.StaffScopes`) chỉ được dùng nội bộ trong Service/Repository để rút trích dữ liệu dạng chuỗi (String/Int) đẩy lên DTO.

## 🛑 RULE 2: MODEL BINDING CHO MẢNG ĐỘNG (HTML/JS)
* Khi render mã HTML và JavaScript tĩnh cho chức năng "Thêm Số điện thoại/Địa chỉ" ở màn hình Create/Edit (`Create.cshtml`, `Edit.cshtml`), tên của thẻ `<input>` **BẮT BUỘC** phải tuân thủ cú pháp Index của ASP.NET Core MVC.
* **Cú pháp đúng:** `name="Phones[0]"`, `name="Phones[1]"`, `name="Addresses[0]"`.
* **Tuyệt đối cấm:** Dùng `name="Phones[]"` hoặc biến JS đếm index sai lệch khiến Controller nhận `List<string> Phones` bị rỗng (null).

## 🛑 RULE 3: BẢO MẬT CSRF CHO AJAX SWEETALERT2
* Tính năng Toggle Status (Khóa/Mở tài khoản) gọi qua AJAX/Fetch tích hợp SweetAlert2 **phải được bảo vệ bởi Anti-Forgery Token**.
* **Cách làm:** Trên file `.cshtml`, nhúng `@Html.AntiForgeryToken()`. Trong file JS, lấy giá trị token này và đính kèm vào `headers: { 'RequestVerificationToken': token }` của lệnh Fetch/AJAX trước khi gửi POST request lên Controller. 
* Tầng Controller bắt buộc gắn attribute `[ValidateAntiForgeryToken]` cho hàm xử lý POST này.

## 🛑 RULE 4: CHIẾN LƯỢC TRANSACTION & EF CORE TRACKING
* Đối với luồng Ghi (Write) dữ liệu 1-Nhiều (`AccountRoles`, `StaffPhones`, `StaffAddresses`) trong hàm `UpdateStaffTransactionAsync`, **tuyệt đối không** dùng vòng lặp để Update từng thực thể con.
* **Bắt buộc dùng chiến lược "Clear & Replace":** `_context.RemoveRange()` toàn bộ danh sách cũ, sau đó `_context.AddRangeAsync()` danh sách mới. Mọi thao tác này BẤT DI BẤT DỊCH phải nằm gọn trong `using var transaction = await _context.Database.BeginTransactionAsync();`.

## 🛑 RULE 5: QUYỀN LỰC PHI TẬP TRUNG (DECENTRALIZED RBAC)
* Tầng Service (`AdminStaffService`) đóng vai trò là Lớp khiên. Không tin tưởng `StoreId` hoặc `RoleIds` gửi từ UI nếu User là `Store Manager`.
* **Bắt buộc Hard-Override:** Đọc Claims của người dùng hiện tại. Nếu Role là `Store Manager`, hãy can thiệp ghi đè `model.StoreId` bằng đúng Cửa hàng của họ, và ném `Exception` ngay lập tức nếu mảng `RoleIds` chứa các quyền cấp cao (Admin, Manager).

## 🛑 RULE 6: LÀM SẠCH DỮ LIỆU RÁC (DATA SANITIZATION)
* Trước khi map `List<string> Phones` và `Addresses` từ ViewModel sang Entity để lưu DB, **phải chạy lệnh LINQ lọc bỏ các phần tử rỗng**: `.Where(x => !string.IsNullOrWhiteSpace(x)).ToList()`.
* Đảm bảo phần tử đầu tiên luôn được gán cứng `IsDefault = true`.

> **Lệnh thực thi:** Hãy phản hồi "ĐÃ HIỂU RÕ 6 CODING RULES". Sau đó, bạn có thể bắt đầu Generate code cho Phase 1 và Phase 2 trong Task Tracker.
---------------------------------------------------------------------------------------------------------------------------------------------
[CODE AUDIT & REFACTOR: STRICT "THIN CONTROLLER" IMPLEMENTATION]

File AdminStaffController của bạn đang vi phạm nghiêm trọng nguyên tắc Thin Controller của kiến trúc N-Tier. Yêu cầu bạn refactor lại toàn bộ class này theo 4 yêu cầu ép buộc sau:

1. Xóa bỏ hoàn toàn AppDbContext khỏi Controller:

Gỡ bỏ AppDbContext khỏi Dependency Injection của Controller. Controller TUYỆT ĐỐI không được gọi _context.

Đẩy toàn bộ logic lấy list Roles, Stores, ScopeTypes trong hàm PrepareViewBagForForm xuống IAdminStaffService. Tạo một hàm mới ở Service: Task<StaffFormMasterDataVM> GetMasterDataForFormAsync(int? storeId).

2. Rút gọn Logic Upload Avatar:

Cắt toàn bộ hàm SaveAvatarAsync (xử lý IFormFile) ném vào tầng Service (hoặc một IFileService riêng). Controller chỉ việc truyền model.AvatarFile xuống cho Service tự lo liệu việc lưu file và cập nhật URL vào DB. Xóa cái đoạn gọi _context.Staffs.Update lố bịch ở hàm Create đi.

3. Gom nhóm Logic đọc Claims:

Không copy-paste đoạn code User.IsInRole("Store Manager") nhiều lần. Viết một thuộc tính (property) hoặc phương thức dùng chung int? GetCurrentManagerStoreId() để tái sử dụng trong các hàm GET.

4. Bảo mật Anti-Forgery Token cho AJAX:

Hàm ToggleStatus đang thiếu [ValidateAntiForgeryToken]. Phải bổ sung ngay lập tức.

Yêu cầu xuất ra bản code mới của AdminStaffController cực kỳ gọn gàng, chỉ chứa các lệnh gọi _staffService và điều hướng View/Redirect.

[CRITICAL BUG FIX: ACCOUNT CONTROLLER & LOGIN REDIRECT]

File AccountController.cs của bạn đang chứa 2 logic bug nghiêm trọng khiến Admin đăng nhập bị đẩy ra trang Khách hàng và lỗi luồng Phân quyền. Yêu cầu refactor ngay lập tức hàm Login (POST) theo các chỉ định sau:

1. Fix lỗi Redirect Role (So khớp chuỗi sai):
Tên Role trong DB là "Admin System", "Store Manager", "Ward Manager", "Province Manager", "Cashier". Lệnh switch-case hiện tại đang check sai chữ.

Hãy thay lệnh switch bằng các câu lệnh if-else sử dụng .Contains().

Logic đúng: * Nếu role chứa chữ "Admin" hoặc "Manager" -> RedirectToAction("Index", "Dashboard", new { area = "Admin" })

Nếu role chứa chữ "Cashier" -> RedirectToAction("Index", "Pos", new { area = "Cashier" })

Các trường hợp còn lại (hoặc rỗng) -> Về trang Home (Customer).

2. Bổ sung Claim "StoreId" (Bắt buộc cho Decentralized RBAC):
Trong khối tạo claims lúc SignIn, bạn ĐANG THIẾU claim StoreId.

Hãy cập nhật lại LoginDto (hoặc đối tượng trả về từ _accountService.LoginAsync) để đảm bảo nó query và trả về thêm StoreId (nếu User là Staff).

Trong AccountController, thêm logic: if (result.Data.StoreId.HasValue) claims.Add(new Claim("StoreId", result.Data.StoreId.ToString()));

3. Khắc phục lỗi Crash Profile Khách hàng:
Tại view/layout của trang Khách hàng (Frontend), nút bấm dẫn vào "Profile Khách hàng" phải được bọc trong điều kiện: @if(User.IsInRole("Customer")). Nếu Admin lạc ra ngoài trang Home thì không hiển thị nút này để tránh lỗi NullReference khi query bảng Customers.

Hãy phản hồi lại bằng code cập nhật cho AccountController (đoạn hàm Login POST) và báo cáo việc bạn đã update DTO/Service để lấy được StoreId chưa.