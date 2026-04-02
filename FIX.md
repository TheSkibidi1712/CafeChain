[BUG FIX & UI POLISH: STAFF MODULE]

Quá trình test đã phát hiện 3 lỗi liên quan đến UI/UX và Validation. Yêu cầu bạn tiến hành sửa ngay lập tức các vấn đề sau:

🔥 BUG 1: LỖI CRASH DATABASE KHI TAXCODE NULL

Nguyên nhân: Cột TaxCode trong DB là NOT NULL, nhưng UI cho phép nhập rỗng. Khi lưu, EF Core ném lỗi DbUpdateException.

Cách sửa (Tại AdminStaffService): Trong hàm CreateStaffAsync và UpdateStaffAsync, trước khi map dữ liệu sang Entity Staff, BẮT BUỘC phải xử lý Fallback cho TaxCode:
staff.TaxCode = string.IsNullOrWhiteSpace(model.TaxCode) ? "" : model.TaxCode.Trim(); (Gán thành chuỗi rỗng nếu người dùng không nhập).

🔥 BUG 2: LỖI FONT/ENCODE CHỮ TIẾNG VIỆT TRÊN SWEETALERT2

Nguyên nhân: Các thông báo lỗi/thành công chứa tiếng Việt (từ TempData hoặc JSON) đang bị mã hóa HTML (HTML-encoded) hiển thị thành các ký tự &#xE2; trên Popup.

Cách sửa (Tại file .cshtml hoặc js): - Nếu render từ TempData vào script SweetAlert, hãy đảm bảo bọc nó trong @Html.Raw(...). Ví dụ: text: '@Html.Raw(TempData["Error"])'.

Kiểm tra lại toàn bộ file Index.cshtml và Edit.cshtml chỗ gọi SweetAlert để đảm bảo chữ tiếng Việt hiển thị chuẩn xác.

🔥 BUG 3: CĂN CHỈNH NÚT BẤM (UI POLISH TRANG EDIT)

Vấn đề: Ở trang Edit (Edit.cshtml), nút "Hủy" (Cancel) đang nằm cách quá xa nút "Lưu thay đổi" (Save).

Cách sửa: Bọc cụm nút bấm này vào một div sử dụng class của Bootstrap: <div class="d-flex justify-content-end gap-3 mt-4">...</div> để 2 nút nằm sát nhau ở góc phải bên dưới form.

🔥 TASK 4: REVIEW VALIDATION

Kiểm tra lại toàn bộ StaffCreateVM và StaffEditVM.

Đảm bảo các trường bắt buộc (FullName, Email) có [Required(ErrorMessage = "...")] đầy đủ bằng tiếng Việt.

Đảm bảo Salary có [Range(0, double.MaxValue)].

Đảm bảo logic check trùng Email và Số điện thoại (IsDefault) ở tầng Service vẫn hoạt động đúng.

Hãy phản hồi lại bằng các đoạn code C# (Service) và HTML/CSS đã được fix.