Bạn hãy đóng vai là một Senior Developer có 20 năm kinh nghiệm, chuyên về ASP.NET Core MVC theo mô hình Layered Architecture. Hãy kiểm tra và refactor lại phần giao diện, JavaScript và xử lý quyền truy cập trong khu vực phân quyền của hệ thống.

## Bối cảnh hiện tại

Hiện tại hệ thống đang có một số lỗi giao diện và nội dung thông báo liên quan đến phân quyền:

1. Trong modal `Gắn RolePermission`, nút lưu đang bị lỗi hiển thị:

   * Nội dung trong button bị mờ.
   * Text trong button không hiển thị rõ như ảnh 2 tôi đã gửi.
   * Cần refactor lại để button hiển thị đúng, rõ ràng và đồng bộ style.

2. Trong phần bảng phân quyền, có thể còn các button tương tự cũng bị lỗi hiển thị:

   * Button bị mờ.
   * Text không rõ.
   * Icon hoặc màu nền không đồng bộ.
   * Hover, disabled hoặc loading state có thể đang gây lỗi UI.

3. Modal thông báo khi người dùng không có quyền truy cập vào một form đang hiển thị sai nội dung:

   * Tôi đã đăng nhập rồi nhưng modal vẫn hiển thị nội dung như ảnh 1, kiểu yêu cầu đăng nhập.
   * Điều này là sai nghiệp vụ.
   * Nếu người dùng chưa đăng nhập thì mới hiển thị nội dung yêu cầu đăng nhập như ảnh 1.
   * Nếu người dùng đã đăng nhập nhưng không có quyền, thì phải hiển thị nội dung khác, ví dụ: “Bạn không có quyền truy cập chức năng này. Vui lòng liên hệ cấp trên hoặc quản trị viên để được cấp quyền.”

## Yêu cầu 1: Refactor nút lưu trong modal Gắn RolePermission

Hãy kiểm tra và refactor lại nút lưu trong modal `Gắn RolePermission`.

Cần kiểm tra các phần sau:

* View hoặc Partial chứa modal `Gắn RolePermission`.
* CSS class đang áp dụng cho button lưu.
* JavaScript nếu có xử lý trạng thái loading, disabled hoặc submit.
* Các class Bootstrap hoặc custom CSS có thể làm text bị mờ.
* Màu nền, màu chữ, opacity, disabled state và hover state của button.

Mục tiêu:

* Text trong nút lưu phải hiển thị rõ ràng.
* Button không bị mờ khi đang ở trạng thái bình thường.
* Nếu button bị disabled hoặc loading thì phải có style rõ ràng, không gây nhầm lẫn.
* Style phải đồng bộ với giao diện hiện tại.
* Không làm vỡ layout modal.

---

## Yêu cầu 2: Rà soát các button trong bảng phân quyền

Hãy kiểm tra kỹ toàn bộ các button tương ứng trong phần bảng phân quyền.

Cần rà soát các button như:

* Nút gắn quyền.
* Nút lưu.
* Nút hủy.
* Nút chỉnh sửa.
* Nút xóa hoặc toggle trạng thái nếu có.
* Nút trong modal hoặc partial liên quan đến Role, Permission, RolePermission.

Cần đảm bảo:

* Button hiển thị rõ text và icon.
* Không bị mờ bất thường.
* Không bị lỗi màu chữ trùng màu nền.
* Không bị opacity sai.
* Không bị class `disabled`, `btn-link`, `text-muted`, `opacity-*` hoặc custom CSS làm mờ.
* Hover và focus state vẫn rõ ràng.
* Các button có cùng vai trò thì dùng style thống nhất.

---

## Yêu cầu 3: Refactor modal không có quyền truy cập

Hiện tại modal thông báo không có quyền truy cập đang xử lý sai nội dung.

Hãy refactor lại theo 2 trường hợp rõ ràng:

### Trường hợp 1: Người dùng chưa đăng nhập

Nếu người dùng chưa đăng nhập mà truy cập vào form hoặc chức năng yêu cầu đăng nhập, modal có thể hiển thị như ảnh 1.

Nội dung có thể là:

* Bạn cần đăng nhập để truy cập chức năng này.
* Vui lòng đăng nhập để tiếp tục.
* Có nút chuyển đến trang đăng nhập.

### Trường hợp 2: Người dùng đã đăng nhập nhưng không có quyền

Nếu người dùng đã đăng nhập nhưng không có quyền truy cập, không được hiển thị nội dung yêu cầu đăng nhập nữa.

Nội dung nên đổi thành:

* Bạn không có quyền truy cập chức năng này.
* Vui lòng liên hệ cấp trên hoặc quản trị viên để được cấp quyền.
* Không hiển thị nút đăng nhập.
* Có thể hiển thị nút “Quay lại”, “Đóng” hoặc “Về trang chủ”.

Mục tiêu:

* Phân biệt đúng giữa lỗi chưa đăng nhập và lỗi không có quyền.
* Người dùng đã đăng nhập thì không bị yêu cầu đăng nhập lại.
* Nội dung modal phải đúng nghiệp vụ và dễ hiểu.
* UI modal phải giữ nguyên style hiện tại.

---

## Yêu cầu kiểm tra kỹ thuật

Hãy kiểm tra các phần sau:

* Controller hoặc filter xử lý Unauthorized/Forbidden.
* Middleware hoặc custom authorization logic nếu có.
* View/Partial hiển thị modal không có quyền.
* JavaScript mở modal khi bị chặn quyền.
* Response status code:

  * `401 Unauthorized` cho trường hợp chưa đăng nhập.
  * `403 Forbidden` cho trường hợp đã đăng nhập nhưng không có quyền.
* Cách redirect hoặc trả JSON khi gọi bằng AJAX.
* Cách hiển thị toast/modal khi request bị từ chối.
* Layout hoặc shared view có chứa modal dùng chung.

---

## Nguyên tắc refactor bắt buộc

1. Controller chỉ xử lý điều hướng request, gọi Service hoặc trả View/JSON phù hợp.
2. Không đưa nghiệp vụ phân quyền phức tạp vào View.
3. Logic phân biệt `401` và `403` phải rõ ràng.
4. Nếu request là AJAX thì trả JSON/status code để JS hiển thị modal phù hợp.
5. Nếu request là MVC truyền thống thì redirect hoặc trả view lỗi phù hợp.
6. Không làm thay đổi style tổng thể hiện tại.
7. Không sửa lan man những phần đang hoạt động ổn.
8. Không tự ý bịa thêm file hoặc class nếu tôi chưa cung cấp.
9. Nếu thiếu file để kiểm tra chính xác, hãy yêu cầu tôi gửi thêm.

---

## Kết quả tôi mong muốn

Hãy trả lời theo thứ tự sau:

1. Phân tích nguyên nhân nút lưu trong modal `Gắn RolePermission` bị mờ.
2. Rà soát các button tương ứng trong bảng phân quyền.
3. Phân tích nguyên nhân modal không có quyền đang hiển thị sai nội dung.
4. Đề xuất hướng refactor đúng nghiệp vụ.
5. Liệt kê các file cần kiểm tra và chỉnh sửa.
6. Viết code refactor chi tiết theo từng file.
7. Sửa lại UI button để text hiển thị rõ ràng.
8. Sửa lại modal không có quyền theo 2 trường hợp:

   * Chưa đăng nhập.
   * Đã đăng nhập nhưng không có quyền.
9. Giải thích luồng hoạt động sau khi sửa.
10. Liệt kê các case cần test lại.

## Lưu ý quan trọng

Hãy ưu tiên giữ nguyên style giao diện hiện tại. Chỉ chỉnh những phần cần thiết để button hiển thị rõ, modal đúng nghiệp vụ và phân quyền hoạt động chính xác.

Nếu cần tôi gửi thêm file, hãy yêu cầu các file liên quan như:

* View hoặc Partial của modal `Gắn RolePermission`.
* View bảng phân quyền.
* CSS liên quan đến button/modal.
* JavaScript xử lý RolePermission.
* Controller phân quyền.
* Filter hoặc middleware xử lý quyền truy cập.
* Layout hoặc partial chứa modal thông báo không có quyền.
