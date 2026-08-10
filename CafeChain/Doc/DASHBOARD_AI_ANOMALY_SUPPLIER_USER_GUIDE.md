# Hướng dẫn sử dụng Dashboard, AI, tín hiệu vận hành và so sánh nhà cung cấp

> Cập nhật theo mã nguồn và `Scripts/SeedAll.sql` ngày 10/08/2026.

Tài liệu này dành cho người sử dụng và người quản trị phân quyền. Tên mã quyền được giữ trong dấu `` ` `` để quản trị viên có thể đối chiếu cấu hình; giao diện nghiệp vụ phải hiển thị nội dung tiếng Việt.

## 1. Tài khoản thử nghiệm và quyền mặc định

Các tài khoản dưới đây được tạo bởi `Scripts/SeedAll.sql` và dùng mật khẩu thử nghiệm `The@1712`. Chỉ dùng ở môi trường cục bộ hoặc kiểm thử, không dùng cho môi trường thật.

| Vai trò | Tài khoản | Phạm vi sử dụng mặc định |
|---|---|---|
| Chủ doanh nghiệp | `owner@cafechain.vn` | Toàn bộ phần Dashboard theo phạm vi; AI; tín hiệu vận hành; xem so sánh nhà cung cấp; duyệt đơn đặt hàng |
| Quản lý vùng | `areamanager@cafechain.vn` | Dashboard vùng; AI; tín hiệu vận hành; xem so sánh trong các cửa hàng được phân công |
| Quản lý chi nhánh | `storemanager@cafechain.vn` | Dashboard và tín hiệu của chi nhánh; xem so sánh trong cửa hàng được phân công |
| Kế toán/Kho | `accountantwarehouse@cafechain.vn` | Tài chính, tồn kho, mua hàng, AI; tổng hợp đề nghị và tạo/gửi đơn đặt hàng |
| Quản trị hệ thống | `systemadmin@cafechain.vn` | Quản lý kỹ thuật và phân quyền; mặc định không xem dữ liệu kinh doanh |

Nếu menu hoặc nút khác tài liệu, kiểm tra lần lượt: migration và `SeedAll.sql`, tài khoản/nhân viên/vai trò còn hoạt động, quyền ghi đè không bị từ chối và nhân viên có phạm vi cửa hàng đang chọn.

### 1.1 Bảng quyền nhanh

| Việc cần làm | Quyền chính |
|---|---|
| Mở Dashboard | `App.AdminDashboard` + quyền phần + quyền dữ liệu biểu đồ + phạm vi cửa hàng |
| Dùng AI Dashboard | thêm `Dashboard.AI.Use` |
| Xem tín hiệu vận hành | `OperationalAnomaly.View` |
| Tiếp nhận / xử lý / phản hồi tín hiệu | `OperationalAnomaly.Acknowledge` / `OperationalAnomaly.Resolve` / `OperationalAnomaly.Feedback` |
| Yêu cầu AI giải thích tín hiệu | quyền xem + `Dashboard.AI.Use` |
| Xem bảng so sánh nhà cung cấp | `PurchaseAdvice.View` + `SupplierQuality.View` |
| Yêu cầu AI giải thích điểm nhà cung cấp | quyền xem bảng so sánh + `Dashboard.AI.Use` |
| Vào hàng chờ tổng hợp | `PurchaseAdvice.Consolidate` |
| Chọn nhà cung cấp | `PurchaseAdvice.SelectSupplier` cùng quyền của luồng tổng hợp |
| Tạo đơn/lô đơn đặt hàng và gửi | `PurchaseOrder.Create` / `PurchaseOrder.CreateBatch`; khi gửi cần `PurchaseOrder.Send` |
| Duyệt đơn đặt hàng | `PurchaseOrder.Approve` |
| Quản lý phân quyền | `System.Permission.Manage` |

Có quyền nhưng cửa hàng nằm ngoài phạm vi nhân viên thì thao tác vẫn bị từ chối.

## 2. Mở và đọc Dashboard

1. Đăng nhập bằng tài khoản phù hợp.
2. Từ màn hình chọn ứng dụng, bấm **Admin Dashboard** hoặc mở `/Admin/Dashboard`.
3. Hệ thống mở phần đầu tiên mà tài khoản được phép xem. Phần không có quyền sẽ không xuất hiện.
4. Chọn khoảng thời gian, tỉnh/phường/cửa hàng. Chỉ cửa hàng trong phạm vi được phân công mới xuất hiện.
5. Đọc trạng thái dữ liệu và cảnh báo của từng biểu đồ. Có quyền phần nhưng thiếu quyền dữ liệu riêng thì biểu đồ tương ứng vẫn bị ẩn.

Kết quả mặc định theo vai trò:

- **Chủ doanh nghiệp:** thấy mọi phần đã seed trong phạm vi được gán; là vai trò duy nhất mặc định có phần điều hành cấp chuỗi.
- **Quản lý vùng:** thấy vận hành, tồn kho, mua hàng, sản phẩm và nhân sự của các cửa hàng thuộc vùng được gán.
- **Quản lý chi nhánh:** phạm vi một cửa hàng thì bộ chọn bị khóa hoặc ẩn; nhiều cửa hàng chỉ hiện danh sách được gán.
- **Kế toán/Kho:** thấy tài chính, tồn kho, mua hàng và sản phẩm; không mặc định thấy nhân sự hoặc tín hiệu vận hành.
- **Quản trị hệ thống:** không thấy Dashboard kinh doanh theo mặc định. Đây là kết quả đúng, không phải lỗi giao diện.

Không sửa tham số cửa hàng trên URL để thử vượt phạm vi; backend sẽ trả từ chối truy cập.

## 3. Dùng AI Dashboard

### 3.1 Thao tác

1. Chọn đúng kỳ dữ liệu và cửa hàng trên Dashboard trước khi đặt câu hỏi.
2. Mở vùng **AI Dashboard**. Tài khoản cần `Dashboard.AI.Use` và quyền xem các biểu đồ liên quan.
3. Chọn một câu hỏi gợi ý hoặc nhập câu hỏi ngắn, tối đa 500 ký tự và có một trọng tâm, ví dụ: **“Doanh thu tuần này thay đổi thế nào so với kỳ trước?”**
4. Bấm **Phân tích**.
5. Đọc theo thứ tự: **Trả lời trực tiếp**, **Dữ liệu làm căn cứ**, biểu đồ/bảng, **Việc cần kiểm tra** và **Nguồn dữ liệu**.
6. Đối chiếu kỳ, cửa hàng, đơn vị và cảnh báo trước khi dùng kết quả để ra quyết định.

Bộ lọc trên Dashboard là phạm vi chính thức. Nếu câu hỏi ghi một cửa hàng hoặc thời gian khác, hệ thống vẫn dùng bộ lọc hiện tại và phải cảnh báo; nội dung câu hỏi không thể mở rộng quyền.

### 3.2 Cách hiểu kết quả

- **Dữ liệu làm căn cứ** là số liệu do backend lấy từ biểu đồ đã được cấp quyền.
- **Mức độ tin cậy** phản ánh tình trạng và độ đầy đủ của dữ liệu, không phải cam kết kết luận đúng tuyệt đối.
- **Việc cần kiểm tra** là gợi ý xác minh ở màn hình nghiệp vụ, không phải lệnh hệ thống đã thực hiện.
- **Giải thích dự phòng** xuất hiện khi mô hình AI bị tắt, quá thời gian, lỗi hoặc trả nội dung không kiểm chứng được. Số liệu và biểu đồ vẫn do backend tính.
- **Không có dữ liệu** nghĩa là kỳ/phạm vi hiện tại không có dữ liệu phù hợp; hệ thống không đoán thêm để lấp chỗ trống.

AI không tự tạo hoặc duyệt đơn, thay đổi tồn kho, xử lý tín hiệu, chọn nhà cung cấp hay kết luận gian lận. Nếu câu hỏi cần dữ liệu của biểu đồ mà tài khoản không có quyền, backend trả 403 trước khi lấy dữ liệu.

## 4. Xem và xử lý tín hiệu vận hành

1. Đăng nhập Chủ doanh nghiệp, Quản lý vùng hoặc Quản lý chi nhánh có `OperationalAnomaly.View` và phạm vi cửa hàng.
2. Mở **Nhân sự & Vận hành → Tín hiệu vận hành** hoặc `/Admin/AdminOperationalAnomalies`.
3. Chọn cửa hàng trong danh sách được phép.
4. Đọc **Chỉ số cần kiểm tra**, **Giá trị ghi nhận**, **Mức thông thường trước đây**, **Mức chênh lệch**, **Mức cần ưu tiên** và **Trạng thái xử lý**.
5. Chỉ mở **Thông tin kỹ thuật** khi cần đối soát mã chỉ số, phiên bản phát hiện hoặc điểm chuẩn hóa.

Tùy quyền, người dùng có thể:

- bấm **Tiếp nhận** để ghi nhận đã xem;
- bấm **Đánh dấu đã xử lý**, nhập ghi chú để hoàn tất;
- bấm **Phản hồi**, chọn **Hữu ích**, **Không hữu ích** hoặc **Cảnh báo không phù hợp**;
- bấm **Giải thích dễ hiểu** khi có thêm `Dashboard.AI.Use` để xem diễn giải và dữ liệu nên kiểm tra.

Luồng trạng thái là **Mới → Đã tiếp nhận → Đã xử lý**. Tải lại trang sau thao tác để xác nhận bản mới. Lỗi xung đột thường có nghĩa người khác đã cập nhật tín hiệu; hãy tải lại thay vì gửi lại dữ liệu cũ. Tín hiệu chỉ là dấu hiệu cần kiểm tra, không phải kết luận gian lận hoặc xác định người chịu trách nhiệm.

## 5. So sánh nhà cung cấp từ đề nghị mua

### 5.1 Phân công mặc định

- **Kế toán/Kho** là vai trò mặc định vào hàng chờ tổng hợp, chọn nhà cung cấp, kiểm tra bản tổng hợp và tạo/gửi đơn đặt hàng.
- **Chủ doanh nghiệp** xem đơn và duyệt đơn đặt hàng.
- **Chủ doanh nghiệp, Quản lý vùng, Quản lý chi nhánh và Kế toán/Kho** đều có các quyền đọc cần thiết cho dịch vụ so sánh, nhưng màn hình hàng chờ tổng hợp còn yêu cầu `PurchaseAdvice.Consolidate`, mặc định chỉ cấp cho Kế toán/Kho.
- Quyền `PurchaseAdvice.SelectSupplier` không thay thế quyền tổng hợp hoặc quyền tạo đơn.

### 5.2 Các bước thực hiện

1. Đăng nhập `accountantwarehouse@cafechain.vn`.
2. Vào **Kho & Cung ứng → Đề nghị mua hàng**.
3. Mở đề nghị và hoàn tất các bước để đề nghị ở trạng thái **Đang xem xét**.
4. Bấm **Chọn nhà cung cấp và tổng hợp**.
5. Tại từng dòng, bấm **So sánh nhà cung cấp**.
6. Kiểm tra nhà cung cấp, hình thức mua, số gói/số lượng, tổng chi phí, lượng mua dư, điểm, hạng, mức độ tin cậy và cảnh báo.
7. Chọn nhà cung cấp, quy cách và hình thức mua; đánh dấu các dòng cần xử lý rồi bấm **Kiểm tra bản tổng hợp**.
8. Đọc lại lượng mua tối thiểu, số gói hoặc lượng mua rời, phần nhu cầu được đáp ứng, lượng dư và tổng tiền.
9. Bấm **Tạo đơn đặt hàng** hoặc **Tạo lô đơn đặt hàng**. Backend sẽ kiểm tra lại giá, quy đổi, số lượng còn thiếu, phạm vi và trạng thái.
10. Đăng nhập người có `PurchaseOrder.Approve`, mặc định là Chủ doanh nghiệp, mở đơn và bấm **Duyệt** khi thông tin hợp lệ. Người tạo không được tự duyệt chính đơn hoặc lô đơn của mình.

### 5.3 Cách đọc popup so sánh

Tại CafeChain Thủ Dầu Một, đầu popup hiển thị **Dữ liệu thử nghiệm — chế độ quan sát**. Chế độ này chỉ hỗ trợ quyết định, không tự chọn nhà cung cấp và không tự tạo hoặc duyệt đơn.

Mỗi ứng viên có thể hiển thị:

- **Hạng 1, Hạng 2...** khi có ít nhất hai nhà cung cấp đủ điều kiện cạnh tranh;
- **Đủ điều kiện xếp hạng** khi đủ lịch sử và đủ năm điểm thành phần;
- **Chỉ dùng tham khảo** khi chưa thể tạo xếp hạng cạnh tranh;
- **Mức độ tin cậy cao**, **Mức độ tin cậy vừa phải** hoặc **Chưa đủ dữ liệu**;
- điểm **Giá mua**, **Giao đúng hẹn**, **Đáp ứng đủ số lượng**, **Chất lượng hàng** và **Thời gian giao**.

Ngưỡng mặc định:

- từ 20 phiếu nhận đã xác nhận trong 180 ngày: mức độ tin cậy cao;
- từ 5 đến 19 phiếu: mức độ tin cậy vừa phải;
- dưới 5 phiếu: chưa đủ dữ liệu để xếp hạng.

`SeedAll.sql` chuẩn bị cho mỗi nhà cung cấp hợp lệ tại CafeChain Thủ Dầu Một 5 đơn hoàn tất và 5 phiếu nhận đã xác nhận trong 180 ngày. Sau khi chạy seed mới, các nhà cung cấp Topping & Syrup TP.HCM và Bình Dương phải có điểm và hạng thay vì cảnh báo thiếu lịch sử. Chạy lại seed không được tạo thêm chứng từ hoặc cộng tồn lần hai.

Nếu không có nhà cung cấp đủ điều kiện, popup nêu lý do chưa thể xếp hạng. Nếu chỉ có một nhà cung cấp đủ điều kiện, điểm chỉ dùng tham khảo và không được gọi là lựa chọn tốt nhất. Giá thấp nhất cũng không tự động thắng vì điểm còn xét đúng hẹn, đủ số lượng, chất lượng và thời gian giao.

Nếu tính năng so sánh đang tắt, cửa hàng ngoài danh sách thử nghiệm, không có quy cách hợp lệ hoặc dữ liệu quy đổi lỗi, người có quyền vẫn tiếp tục quy trình mua thủ công. Không chọn theo thứ hạng mà bỏ qua lượng mua tối thiểu, lượng dư hoặc tổng chi phí.

## 6. Quản lý phân quyền

Người quản trị cần `System.Permission.Manage`; seed mặc định cấp quyền này cho Chủ doanh nghiệp và Quản trị hệ thống.

Khi cấp hoặc thu hồi quyền:

1. Xác định đúng việc người dùng cần làm và cấp mã quyền nhỏ nhất tương ứng.
2. Kiểm tra vai trò đang hoạt động và các quyền ghi đè ở cấp tài khoản. Quyền từ chối ở cấp tài khoản có ưu tiên cao hơn quyền cho phép từ vai trò.
3. Gán phạm vi cửa hàng cho hồ sơ nhân viên; cấp quyền mà không có phạm vi sẽ không mở dữ liệu cửa hàng.
4. Với Dashboard, cấp cả quyền mở ứng dụng, quyền phần và quyền biểu đồ. Chỉ cấp `Dashboard.AI.Use` không làm xuất hiện dữ liệu bị cấm.
5. Với tín hiệu vận hành, tách quyền xem, tiếp nhận, xử lý và phản hồi theo trách nhiệm.
6. Với mua hàng, giữ tách nhiệm vụ giữa người tổng hợp/tạo đơn và người duyệt đơn. Không cấp `PurchaseOrder.Approve` chỉ để người dùng xem bảng so sánh.
7. Đăng xuất/đăng nhập lại nếu phiên hiện tại chưa nhận quyền mới, sau đó kiểm tra bằng đúng cửa hàng trong phạm vi.

Không cấp quyền kinh doanh rộng cho Quản trị hệ thống chỉ vì tên vai trò là quản trị. Nếu cần kiểm thử dữ liệu kinh doanh, tài khoản này phải được cấp rõ từng quyền và một `StaffScope` như mọi tài khoản khác.

## 7. Xử lý lỗi thường gặp

| Hiện tượng | Nguyên nhân cần kiểm tra |
|---|---|
| 403 khi mở Dashboard | Thiếu `App.AdminDashboard`, không có phần hợp lệ, quyền ghi đè bị từ chối hoặc không có phạm vi cửa hàng |
| 403 khi đổi cửa hàng | Cửa hàng không thuộc phân công đang hoạt động; không sửa URL để vượt phạm vi |
| Có phần nhưng biểu đồ bị ẩn | Thiếu quyền dữ liệu riêng của biểu đồ |
| AI báo không có quyền | Thiếu `Dashboard.AI.Use` hoặc thiếu quyền của một biểu đồ chính/bổ trợ |
| AI báo quá nhiều yêu cầu | Chờ rồi gửi lại một lần; không bấm liên tục |
| AI trả giải thích dự phòng | Mô hình bị tắt/lỗi hoặc phản hồi không vượt qua kiểm tra; số liệu backend vẫn dùng được |
| Không có tín hiệu vận hành | Cổng tính năng chưa bật cho cửa hàng, chưa đủ 14 quan sát hoặc biến động chưa vượt ngưỡng |
| Xung đột khi xử lý tín hiệu | Bản ghi đã đổi; tải lại trang và thao tác trên phiên bản mới |
| So sánh trả xung đột | Tính năng chưa bật hoặc cửa hàng ngoài danh sách thử nghiệm |
| Nhà cung cấp chưa có hạng | Chưa đủ số phiếu, thiếu một điểm thành phần, dùng thời gian giao tạm tính hoặc chưa có ít nhất hai ứng viên đủ điều kiện |
| Có quyền so sánh nhưng không mở được hàng chờ tổng hợp | Thiếu `PurchaseAdvice.Consolidate` |
| Tạo đơn thất bại | Kiểm tra trạng thái đề nghị, phiên bản dòng, lượng còn thiếu, quy đổi, lượng mua tối thiểu, quyền tạo đơn và phạm vi |
| Duyệt đơn thất bại | Thiếu `PurchaseOrder.Approve` hoặc đơn chưa ở trạng thái cho phép duyệt |

## 8. Hướng dẫn liên quan

- [Quy tắc nghiệp vụ Dashboard, AI, tín hiệu vận hành và so sánh nhà cung cấp](./DASHBOARD_AI_ANOMALY_SUPPLIER_BUSINESS_RULES.md)
- [Hướng dẫn nghiệp vụ và kỹ thuật các chức năng AI](./AI_FEATURES_BUSINESS_AND_TECHNICAL_GUIDE.md)
- [Hướng dẫn quản lý phạm vi nhân viên](./STAFF_SCOPE_MANAGEMENT_GUIDE.md)
- [Hướng dẫn StaffHub/POS](./STAFFHUB_USER_BUSINESS_FLOWS.md)
- [Quy tắc StaffHub/POS/ca làm](./STAFFHUB_POS_WORKSHIFT_BUSINESS_RULES.md)

Mã xác thực dùng một lần và đăng ký thiết bị bán hàng là luồng POS riêng. Không tìm mã xác thực hoặc nút xác nhận thiết bị trong Dashboard/AI.
