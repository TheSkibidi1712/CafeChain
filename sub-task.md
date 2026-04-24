Phân tích & Viết User Stories dựa trên Source Code CafeChain
Xin lỗi bạn vì sự nhầm lẫn trước đó. Tôi đã tiến hành truy cập trực tiếp vào mã nguồn hệ thống (các Models AttendanceLog.cs, StoreIP.cs, StaffShift.cs, Shift.cs và Controller AdminProductionOrderController.cs) để đảm bảo các User Stories dưới đây phản ánh chính xác 100% kiến trúc và Logic đang có của dự án.

Dưới đây là các User Story chi tiết, mỗi Story có đúng 5 Sub-tasks kỹ thuật (FE/BE) và KHÔNG chứa task QA theo yêu cầu của bạn.

1. Module Chấm công BYOD & Sinh trắc học (Dựa trên AttendanceLog.cs)
User Story: Là một Nhân viên, tôi muốn chấm công vào/ra ca bằng điện thoại cá nhân thông qua nhận diện khuôn mặt, để đảm bảo tính xác thực mà không cần máy chấm công vật lý.

Acceptance Criteria (AC):
AC1: Ghi nhận chính xác IpAddress lúc chấm công vào AttendanceLog.
AC2: Hệ thống phải gọi API xác thực khuôn mặt và cập nhật cờ IsFaceVerified thành true khi khớp.
AC3: Lưu lại trạng thái Status (Valid/Invalid) để đối soát.
Sub-tasks (5):
[BE] Xây dựng API Create AttendanceLog: Nhận payload từ Frontend, lấy CheckInTime theo DateTime.UtcNow và lấy IpAddress từ HTTP Request.
[BE] Tích hợp Sinh trắc học (Biometric API): Xử lý dữ liệu nhận diện khuôn mặt/FaceID và tự động lật cờ IsFaceVerified = true nếu xác thực thành công.
[BE] Xây dựng Service Đối soát: Map AttendanceLog mồ côi vào đúng StaffShiftId dựa trên StoreId và UserId.
[FE] Build UI Chấm công Web/Mobile: Tích hợp thư viện kích hoạt Camera/FaceID của thiết bị khi nhấn nút "Check-in".
[FE] Xử lý hiển thị Trạng thái (Status): Đổi màu nút Check-in thành xanh/đỏ tương ứng với Status trả về từ Backend.
2. Module Nhân sự (Dựa trên cấu trúc Staff, StaffDependent, StaffBank)
User Story: Là một Quản lý Nhân sự, tôi muốn quản lý hồ sơ chi tiết của nhân viên bao gồm thông tin cá nhân, người phụ thuộc và tài khoản ngân hàng để phục vụ việc tính lương.

Acceptance Criteria (AC):
AC1: Cho phép CRUD dữ liệu nhân viên lưu vào các bảng quan hệ Staffs, StaffAddresses, StaffPhones.
AC2: Quản lý được danh sách StaffDependents (Người phụ thuộc) để giảm trừ thuế.
AC3: Quản lý được thông tin StaffBanks (Ngân hàng) để chuyển khoản lương.
Sub-tasks (5):
[BE] Xây dựng API CRUD Hồ sơ Nhân viên: Xử lý Mapping dữ liệu lưu vào Staff, StaffAddresses, StaffPhones.
[BE] Xây dựng API Quản lý Tài chính: CRUD cho bảng StaffBanks và StaffDependents.
[BE] Cấu hình EF Core Validation: Rào Entity Framework bắt lỗi Unique Constraint cho CCCD và Số điện thoại.
[FE] Dựng Layout Profile đa tab: Build giao diện 3 Tab: Thông tin chung, Địa chỉ/Liên hệ, Ngân hàng/Phụ thuộc.
[FE] Tích hợp Dynamic Form List: Xây dựng tính năng Thêm/Xóa dòng động (Dynamic fields) cho tab Người phụ thuộc.
3. Module Lịch làm việc (Dựa trên StaffShift.cs và Shift.cs)
User Story: Là Cửa hàng trưởng, tôi muốn phân ca làm việc chi tiết cho nhân viên bao gồm cả ca qua đêm và ca tự do, để đảm bảo cửa hàng vận hành trơn tru.

Acceptance Criteria (AC):
AC1: Quản lý được ca qua đêm thông qua cờ IsOvernight và ca tự do qua IsFreeShift.
AC2: Tính toán tổng số giờ làm việc thực tế và lưu vào PayrollHours sau khi Check-out.
AC3: Cho phép nhân viên làm vãng lai đột xuất thông qua cờ IsAdHoc = true.
Sub-tasks (5):
[BE] Xây dựng API Phân ca: Tạo record StaffShift hỗ trợ thời gian tùy biến CustomStartTime và CustomEndTime.
[BE] Thuật toán Overlap & Overnight: Viết LINQ query kiểm tra đụng độ ca và xử lý logic cộng thêm 1 ngày nếu Shift.IsOvernight = true.
[BE] Xây dựng Background Job Tính giờ: Tự động tính khoảng thời gian giữa ActualCheckIn và ActualCheckOut, làm tròn 15 phút, lưu vào PayrollHours.
[FE] Tích hợp UI FullCalendar: Render dữ liệu StaffShift lên lịch tuần/tháng, đổ màu thẻ ca theo biến StatusId.
[FE] Xây dựng Form Phân ca Ad-hoc: Tích hợp Checkbox đánh dấu IsAdHoc và bộ input chọn thời gian Custom Time.
4. Module Quản lý Địa chỉ IP (Dựa trên StoreIP.cs)
User Story: Là một System Admin, tôi muốn cấu hình dải địa chỉ IP (StoreIP) cho từng cửa hàng, để chặn các thiết bị truy cập từ xa vào hệ thống POS và Chấm công nội bộ.

Acceptance Criteria (AC):
AC1: CRUD danh sách IP cho từng cửa hàng, lưu vào cột IPAddress.
AC2: Phân biệt được IP nội bộ hay Public qua cờ IsPublicNetwork.
AC3: Hệ thống Middleware chặn các request vi phạm nếu IP thiết bị không nằm trong danh sách IsActive = true.
Sub-tasks (5):
[BE] Xây dựng Custom Middleware Interceptor: Đọc HttpContext.Connection.RemoteIpAddress và đối chiếu với bảng StoreIPs.
[BE] Xây dựng API Quản lý Store IP: Thực hiện CRUD dữ liệu bảng StoreIP và kiểm tra logic IsActive.
[BE] Viết Logic Regex Subnet: Cấu hình Middleware hiểu được chuẩn IP Wildcard mạng nội bộ (Ví dụ: 192.168.1.*).
[FE] Build Form Cấu hình IP: Trong trang quản lý Store, dựng lưới Grid hiển thị và thêm mới IP/Ghi chú (Notes).
[FE] Xử lý Access Denied: Thiết kế màn hình lỗi HTTP 403 thân thiện khi Middleware từ chối IP của thiết bị.
5. Module BOM & Lệnh Sơ Chế (Dựa trên AdminProductionOrderController.cs)
User Story: Là một Quản lý Kho, tôi muốn tạo Lệnh sơ chế (Nấu BTP) để hệ thống tự động trừ kho nguyên liệu thô và cộng kho bán thành phẩm dựa trên số mẻ (Batches) và công thức BOM đa cấp.

Acceptance Criteria (AC):
AC1: Giao diện cho phép xem trước số lượng nguyên liệu bị trừ (CalculateIngredients).
AC2: Khi thực thi (Execute), hệ thống tạo Transaction xuất kho nguyên liệu thô (PRODUCTION_OUT).
AC3: Sau khi trừ thô, hệ thống tạo Transaction nhập kho lượng Bán thành phẩm (PRODUCTION_IN) và cập nhật StoreInventories.AvailableQty.
Sub-tasks (5):
[BE] API Preview Calculation: Viết logic tính toán (CalculateIngredients) dựa trên Base Quantity, Batches và YieldPercentage.
[BE] API Execute - Deduct Raw Materials: Sử dụng IDbContextTransaction, lặp qua RecipeDetails để giảm AvailableQty của nguyên liệu thô.
[BE] API Execute - Add Sub-recipe: Sinh dòng InventoryTransaction cộng tồn kho AvailableQty cho Bán thành phẩm sau khi hoàn tất.
[FE] Build UI Lệnh Sơ Chế (Production Order): Dựng form nhập số Batches và dùng AJAX hiển thị lưới Preview lượng tiêu hao.
[FE] Xử lý Logic Catch Error Kho hụt: Bắt lỗi RollbackAsync từ Backend và render danh sách "Không đủ tồn kho" lên màn hình SweetAlert2.