[SYSTEM REQUIREMENT & BUSINESS RULES: STAFF PHONES & ADDRESSES REFACTORING]

Bối cảnh: Nhằm tuân thủ chuẩn hóa cơ sở dữ liệu (Database Normalization), chúng ta đã loại bỏ cột Phone và Address khỏi bảng Staffs.
Nhiệm vụ của bạn (Senior ASP.NET Core Developer) là tạo 2 bảng mới StaffPhones và StaffAddresses (Quan hệ 1-N), đồng thời triển khai code Backend (Repository, Service, Controller) và UI tuân thủ NGHIÊM NGẶT các Business Rules dưới đây.

📜 BUSINESS RULE 1: KIẾN TRÚC DỮ LIỆU (DATA MODELS)
Tạo Model StaffPhone: Gồm StaffPhoneId (PK), StaffId (FK), Phone (string, MaxLength 15, Required), IsDefault (bool).

Tạo Model StaffAddress: Gồm StaffAddressId (PK), StaffId (FK), Address (string, MaxLength 300, Required), IsDefault (bool).

Cập nhật AppDbContext.cs để thêm 2 DbSet này và cấu hình quan hệ One-to-Many với bảng Staffs bằng Fluent API (Cascade Delete).

📜 BUSINESS RULE 2: GIỚI HẠN & LÀM SẠCH DỮ LIỆU (VALIDATION & LIMITS)
Tuyệt đối không cho phép rác dữ liệu lọt vào CSDL:

UI/JS Limit: Trên giao diện Form Thêm/Sửa, dùng JavaScript giới hạn tối đa 3 Số điện thoại và 3 Địa chỉ cho mỗi nhân viên. Ẩn nút "Thêm mới" nếu đạt giới hạn.

Backend Filter: Tại tầng Service/Repository, trước khi map dữ liệu từ ViewModel sang Entity, BẮT BUỘC dùng LINQ lọc bỏ các chuỗi rỗng/null (Ví dụ: Admin bấm tạo ô input nhưng không nhập gì).

Logic Mặc định (IsDefault): Quy định cứng: Phần tử hợp lệ đầu tiên (Index 0) trong mảng Phones/Addresses gửi lên sẽ luôn được set IsDefault = true. Các phần tử còn lại set false.

📜 BUSINESS RULE 3: CHIẾN LƯỢC TRANSACTION (CẬP NHẬT DỮ LIỆU)
Bảo vệ tính toàn vẹn dữ liệu trong hàm UpdateStaffTransactionAsync:

Không sử dụng vòng lặp để so sánh và update từng dòng (rất dễ dính lỗi Tracking Conflict của EF Core).

Bắt buộc dùng chiến lược "Clear & Replace": 1. Tìm tất cả StaffPhone và StaffAddress cũ theo StaffId.
2. Dùng _context.StaffPhones.RemoveRange(...) để xóa sạch.
3. Dùng _context.StaffPhones.AddRangeAsync(...) để insert mảng dữ liệu mới vừa được gửi lên từ Form.

Toàn bộ thao tác này phải nằm trong khối using var transaction = await _context.Database.BeginTransactionAsync();.

📜 BUSINESS RULE 4: TÍCH HỢP DECENTRALIZED RBAC (QUYỀN LỰC)
Dù thay đổi cấu trúc bảng, luồng thêm nhân sự vẫn phải giữ nguyên luật phân quyền phi tập trung: Nếu người đang thao tác là Store Manager, backend tự động ghi đè (Hard-Override) StoreId của nhân viên mới thành cửa hàng của Quản lý đó, và chặn việc cấp quyền Admin System.

Yêu cầu đầu ra: Hãy đọc kỹ 4 Business Rules trên. Phản hồi lại bằng mã nguồn cập nhật cho:

StaffPhone.cs, StaffAddress.cs và AppDbContext.cs.

StaffCreateVM, StaffEditVM (Sử dụng List<string> Phones và List<string> Addresses).

Hàm Update trong AdminStaffRepository.cs áp dụng luật Clear & Replace.

Mã HTML/JS tĩnh cho chức năng "Thêm số điện thoại/Địa chỉ" trên Form.

[ADVANCED BUSINESS RULES: STAFF MANAGEMENT & STATE VALIDATION]

Bối cảnh: Form Quản lý Nhân sự (Create/Edit) và Phân quyền đã hình thành cấu trúc cơ bản. Tuy nhiên, để đáp ứng tiêu chuẩn Enterprise QSR, hệ thống cần áp dụng thêm các ràng buộc nghiệp vụ (Business Rules) chặt chẽ ở tầng AdminStaffService trước khi gọi xuống Repository.

Bạn là Senior Backend Developer. Hãy nâng cấp luồng xử lý bằng cách code thêm các logic validation sau:

🔥 RULE 1: RÀNG BUỘC TOÀN VẸN DANH TÍNH (IDENTITY INTEGRITY)

Email & Số điện thoại: Bắt buộc phải check trùng Email trên toàn hệ thống (bảng Accounts). Nếu là số điện thoại (bảng StaffPhones), số ĐIỆN THOẠI MẶC ĐỊNH (IsDefault = true) không được trùng với số mặc định của nhân viên khác.

Mã số thuế / CCCD (TaxCode): Mặc dù có thể rỗng, nhưng nếu đã nhập thì phải là duy nhất (Unique) trên toàn bảng Staffs. Không thể có 2 nhân viên dùng chung 1 CCCD.

🔥 RULE 2: LOGIC KHÓA TÀI KHOẢN & CA LÀM VIỆC (DEACTIVATION LOCK) - CỰC KỲ QUAN TRỌNG
Khi Admin/Quản lý cố gắng vô hiệu hóa một nhân sự (Toggle Staff.Active = false), Tầng Service BẮT BUỘC phải thực hiện lệnh kiểm tra chéo (Cross-check):

Kiểm tra bảng CashSessions: Nếu nhân sự này đang có một ca thu ngân mở (IsClosed = false) -> CHẶN LẠI VÀ NÉM EXCEPTION: "Không thể khóa tài khoản! Nhân viên này chưa kết thúc ca thu ngân (đóng két)."

Kiểm tra bảng StaffShifts: Nếu nhân sự này đang trong ca làm việc (StatusId tương đương với "Đang làm việc" và chưa có ActualCheckOut) -> CHẶN LẠI VÀ NÉM EXCEPTION: "Không thể khóa tài khoản! Nhân viên này chưa check-out khỏi ca làm việc hiện tại."

🔥 RULE 3: KIỂM SOÁT TÍNH HỢP LÝ CỦA ROLE & SCOPE (ROLE-SCOPE ALIGNMENT)
Ngăn chặn việc gán quyền ngớ ngẩn từ phía UI:

Nếu Admin chọn ScopeType = "HQ" (Trụ sở chính), thì trong mảng SelectedRoleIds TUYỆT ĐỐI không được chứa Role Cashier (Thu ngân). (Thu ngân bắt buộc phải gắn với ScopeType = "STORE"). Ném Exception nếu vi phạm.

Quản lý Cửa hàng (Store Manager) không thể tự ý thay đổi Lương (Salary) của nhân viên. Dùng code Backend ép cứng giá trị Salary bằng giá trị cũ (trong hàm Update) nếu User thao tác không phải là Admin System.

🔥 RULE 4: CHÍNH SÁCH MẬT KHẨU MẶC ĐỊNH (DEFAULT SECURITY)

Trong luồng Create Staff, nếu UI không gửi lên Password (chuỗi rỗng), Backend KHÔNG ĐƯỢC báo lỗi. Hãy tự động sinh Mật khẩu mặc định theo cú pháp: Cfc@ + Số điện thoại mặc định. (Ví dụ: Số điện thoại là 0901234567 -> Pass: Cfc@0901234567).

Bắt buộc Hash bằng BCrypt trước khi đưa vào bảng Accounts.

Yêu cầu đầu ra: Hãy cập nhật file AdminStaffService.cs (và Interface tương ứng) để triển khai đủ 4 Rule này. Viết các câu lệnh truy vấn LINQ (.AnyAsync()) để tối ưu hiệu năng khi check trùng lặp và check trạng thái Ca làm việc.