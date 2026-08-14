# Hướng dẫn sử dụng Dashboard, AI, tín hiệu vận hành, so sánh nhà cung cấp và AI Smart Import

> Cập nhật theo mã nguồn, AI skill/schema, giao diện Dashboard và `Scripts/SeedAll.sql` ngày 14/08/2026.

Tài liệu này dành cho người sử dụng và người quản trị phân quyền. Tên mã quyền được giữ trong dấu `` ` `` để quản trị viên có thể đối chiếu cấu hình; giao diện nghiệp vụ phải hiển thị nội dung tiếng Việt.

## 1. Tài khoản thử nghiệm và quyền mặc định

Các tài khoản dưới đây được tạo bởi `Scripts/SeedAll.sql` và dùng mật khẩu thử nghiệm `The@1712`. Chỉ dùng ở môi trường cục bộ hoặc kiểm thử, không dùng cho môi trường thật.

| Vai trò | Tài khoản | Phạm vi sử dụng mặc định |
|---|---|---|
| Chủ doanh nghiệp | `owner@cafechain.vn` | Toàn bộ phần Dashboard theo phạm vi; AI; tín hiệu vận hành; xem so sánh nhà cung cấp; AI Smart Import đủ năm entity; duyệt đơn đặt hàng |
| Quản lý vùng | `areamanager@cafechain.vn` | Dashboard vùng; AI; tín hiệu vận hành; xem so sánh trong các cửa hàng được phân công |
| Quản lý chi nhánh | `storemanager@cafechain.vn` | Dashboard và tín hiệu của chi nhánh; xem so sánh trong cửa hàng được phân công |
| Kế toán/Kho | `accountantwarehouse@cafechain.vn` | Tài chính, tồn kho, mua hàng, AI; Smart Import Ingredient/Supplier; tổng hợp đề nghị và tạo/gửi đơn đặt hàng |
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
| Mở/tải/phân tích AI Smart Import | `AIImport.View` + `AIImport.Upload`; sửa/phân tích lại cần `AIImport.Analyze` |
| Xác nhận AI Smart Import | `AIImport.Confirm` + quyền `*.Create` của từng loại dữ liệu trong preview |
| Hủy/xem lịch sử AI Smart Import | `AIImport.Cancel` / `AIImport.History` |
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

AI Dashboard là trợ lý phân tích chỉ đọc. Mỗi lần phân tích trả lời một trọng tâm bằng dữ liệu của bộ lọc hiện tại; đây không phải cuộc trò chuyện nhiều lượt và AI không tự chạy SQL hay thực hiện thao tác nghiệp vụ.

### 3.1 Chuẩn bị trước khi hỏi

1. Chọn đúng kỳ dữ liệu và cửa hàng trên Dashboard trước khi đặt câu hỏi.
2. Bấm **Áp dụng** để Dashboard tạo ngữ cảnh mới theo đúng bộ lọc.
3. Mở tab **Hỏi AI**. Tài khoản cần `Dashboard.AI.Use` và quyền xem tất cả biểu đồ chính/bổ trợ mà câu hỏi cần dùng.
4. Kiểm tra ba nhãn phạm vi dưới ô hỏi: kỳ, cửa hàng được cấp quyền và mức thời gian.

Bộ lọc trên Dashboard là phạm vi chính thức. Nếu câu hỏi ghi một cửa hàng hoặc thời gian khác, hệ thống vẫn dùng bộ lọc hiện tại và cảnh báo; nội dung câu hỏi không thể mở rộng quyền hoặc thay `StaffScope`.

### 3.2 Cách đặt câu hỏi

- Viết một mục tiêu trong mỗi câu, bằng tiếng Việt rõ ràng.
- Ô hỏi cần ít nhất 3 ký tự và tối đa 500 ký tự.
- Nêu chỉ số muốn xem, cách nhóm hoặc so sánh nếu cần; không yêu cầu AI tạo SQL hay sửa dữ liệu.
- Với câu hỏi xếp hạng, nêu số lượng mong muốn, ví dụ **“Top 5 sản phẩm bán chạy nhất trong kỳ là gì?”**.
- Với câu hỏi rủi ro, hỏi điều cần chú ý hoặc cần kiểm tra; không yêu cầu kết luận gian lận hay quy trách nhiệm.

Trang **Hướng dẫn** của Dashboard có 16 câu hỏi chuẩn, chia thành bốn nhóm:

| Nhóm | Câu hỏi phù hợp |
|---|---|
| Tổng quan và doanh thu | Điều cần chú ý; so sánh doanh thu; cửa hàng hoạt động kém hơn; yếu tố liên quan doanh thu; thống kê doanh thu theo ngày |
| Đơn hàng và sản phẩm | Tỷ lệ hủy theo cửa hàng; phương thức thanh toán; top sản phẩm/danh mục; sản phẩm bán chậm; sản phẩm biên lợi nhuận thấp |
| Kho và đặt hàng | Nguy cơ thiếu nguyên liệu; thứ tự cần đặt lại; xu hướng tiêu thụ nguyên liệu |
| Nhà cung cấp và bất thường | Rủi ro chất lượng/đơn mua quá hạn; bất thường vận hành |

Nút **Dùng câu hỏi này** chỉ điền câu hỏi vào ô để người dùng kiểm tra, không tự gửi yêu cầu.

### 3.3 Thực hiện phân tích

1. Nhập hoặc chọn một câu hỏi phù hợp.
2. Bấm **Phân tích** hoặc nhấn `Enter`.
3. Trong lúc hệ thống hiển thị **Đang tải dữ liệu theo phạm vi quyền và xây dựng evidence...**, không đổi bộ lọc hoặc gửi liên tục.
4. Khi hoàn tất, đọc kỳ, danh sách cửa hàng, trạng thái dữ liệu và độ tin cậy ở đầu kết quả.
5. Đọc **Trả lời trực tiếp**, tối đa ba **Số liệu chứng minh**, **Việc cần kiểm tra** nếu có, biểu đồ, bảng và **Giới hạn dữ liệu**.
6. Mở **Xem nguồn dữ liệu** để đối soát bộ lọc, nguồn truy vấn, EvidenceId và tình trạng fallback.
7. Dùng nút **Ẩn phân tích/Hiện phân tích** khi cần thu gọn hoặc mở lại kết quả.

Nếu đổi bộ lọc khi request đang chạy, hệ thống tự hủy request cũ. Kết quả cũ cũng bị xóa sau khi context Dashboard thay đổi để tránh dùng nhầm phạm vi.

### 3.4 Cách hiểu kết quả

| Phần | Cách sử dụng |
|---|---|
| Kỳ Dashboard/Cửa hàng | Phạm vi backend thực sự đã dùng; phải kiểm tra trước khi đối chiếu nghiệp vụ |
| Trạng thái dữ liệu | Cho biết dữ liệu đầy đủ, một phần, thiếu COGS/cấu hình, không có dữ liệu hay lỗi |
| Độ tin cậy | Phản ánh độ đầy đủ của bằng chứng; không phải xác suất AI đúng |
| Trả lời trực tiếp | Kết luận 2–4 câu theo đúng trọng tâm câu hỏi |
| Số liệu chứng minh | Tối đa ba ý lấy từ dữ liệu backend và có evidence để đối soát |
| Việc cần kiểm tra | Chỉ có với rủi ro/ưu tiên; là bước xác minh read-only, không phải thao tác đã thực hiện |
| Biểu đồ/Bảng dữ liệu | Hai cách trình bày cùng dữ liệu của widget chính; nếu biểu đồ không dựng được, bảng vẫn được dùng |
| Giới hạn dữ liệu | Nêu điều còn thiếu làm hạn chế kết luận |
| Xem nguồn dữ liệu | Chi tiết kỹ thuật phục vụ đối soát bộ lọc, widget, EvidenceId và fallback |

- **Số liệu chứng minh** là dữ liệu do backend lấy từ widget đã được cấp quyền và chuẩn hóa thành evidence.
- **Đầy đủ** nghĩa là các nguồn bắt buộc trả dữ liệu hợp lệ. **Một phần** nghĩa là vẫn có thể đọc kết quả nhưng phải xem giới hạn.
- **Thiếu dữ liệu giá vốn** không được dùng để kết luận chính xác biên lợi nhuận. **Thiếu cấu hình** yêu cầu hoàn thiện ngưỡng, BOM hoặc cấu hình nguồn trước.
- **Không có dữ liệu** nghĩa là kỳ/phạm vi hiện tại không có quan sát phù hợp; hệ thống không đoán thêm để lấp chỗ trống. **Lỗi dữ liệu** nghĩa là nguồn bắt buộc không truy vấn được.
- Độ tin cậy có thể giảm khi mẫu dưới 10, thiếu kỳ so sánh, thiếu evidence cấp thực thể hoặc một widget lỗi.

### 3.5 Kết quả AI và kết quả dự phòng

- **Đã hoàn tất phân tích dựa trên evidence**: phần diễn giải của mô hình đã vượt qua kiểm tra cấu trúc, EvidenceId, tên thực thể và mọi claim số.
- **Đã hoàn tất bằng fallback an toàn**: mô hình bị tắt, timeout, lỗi, trả JSON sai, dùng số/tên không có trong evidence hoặc vi phạm phạm vi. Backend dùng mẫu deterministic phù hợp với loại câu hỏi.
- Fallback vẫn giữ dữ liệu, biểu đồ, bảng, cảnh báo và độ tin cậy; cách đọc kết quả không thay đổi.
- Lý do fallback chỉ phục vụ đối soát trong **Xem nguồn dữ liệu**, không phải kết luận kinh doanh.

Không sao chép EvidenceId, widget key hoặc mã kỹ thuật thành nội dung báo cáo cho người dùng nghiệp vụ; các mã này chỉ dùng khi cần truy vết với nhóm kỹ thuật.

### 3.6 Quy tắc đọc theo loại câu hỏi

- **So sánh:** kiểm tra đủ kỳ hiện tại và kỳ đối chiếu; nếu thiếu baseline, không dùng tỷ lệ thay đổi để kết luận.
- **Xếp hạng:** kiểm tra metric và chiều sắp xếp. Top sản phẩm được xếp theo số lượng bán, không mặc định theo doanh thu.
- **Xu hướng:** cần ít nhất hai điểm thời gian; một điểm không đủ để gọi là tăng hoặc giảm.
- **Biên lợi nhuận:** chỉ dùng các dòng có COGS đầy đủ.
- **Kho/đặt lại:** kiểm tra tồn khả dụng, ngưỡng, quy đổi, quy cách, giá và lead time tại module Kho & Cung ứng trước khi tạo đề nghị.
- **Rủi ro/bất thường:** xem đây là tín hiệu cần xác minh. Không dùng một cảnh báo để kết luận nguyên nhân, gian lận hoặc người chịu trách nhiệm.

### 3.7 Giới hạn hiện tại

- Không có hội thoại đa lượt hoặc lịch sử hội thoại; câu hỏi mới là một lần phân tích mới.
- Không sinh SQL động và không truy vấn ngoài catalog widget.
- AI không tự tạo/duyệt đơn, thay đổi tồn kho, xử lý tín hiệu, chọn nhà cung cấp, sửa lịch hoặc dữ liệu nhân viên.
- Kết quả phụ thuộc dữ liệu nguồn, cấu hình và phạm vi quyền; AI không thể bù cho dữ liệu thiếu.
- Các mục narrative phiên bản cũ như **Nhận định** hoặc **Khuyến nghị** không còn là phần chính của renderer; hành động chỉ xuất hiện dưới dạng **Việc cần kiểm tra** khi đúng trọng tâm.

Nếu câu hỏi cần dữ liệu của bất kỳ biểu đồ chính hoặc bổ trợ nào mà tài khoản không có quyền, backend trả 403 trước khi lấy dữ liệu.

## 4. Xem và xử lý tín hiệu vận hành

1. Đăng nhập Chủ doanh nghiệp, Quản lý vùng hoặc Quản lý chi nhánh có `OperationalAnomaly.View` và phạm vi cửa hàng.
2. Mở **Nhân sự & Vận hành → Tín hiệu vận hành** hoặc `/Admin/AdminOperationalAnomalies`.
3. Chọn cửa hàng trong danh sách được phép.
4. Đọc **Chỉ số cần kiểm tra**, **Giá trị ghi nhận**, **Mức thông thường trước đây**, **Mức chênh lệch**, **Mức cần ưu tiên** và **Trạng thái xử lý**.
5. Chỉ mở **Cơ sở phát hiện** khi cần đối soát ngày nghiệp vụ, mức chênh lệch chuẩn hóa và chất lượng dữ liệu tham chiếu.

Tùy quyền, người dùng có thể:

- bấm **Tiếp nhận** để ghi nhận đã xem;
- bấm **Đánh dấu đã xử lý**, nhập ghi chú để hoàn tất;
- bấm **Phản hồi**, chọn **Hữu ích**, **Không hữu ích** hoặc **Cảnh báo không phù hợp**;
- bấm **Giải thích** khi có thêm `Dashboard.AI.Use` để xem diễn giải và dữ liệu nên kiểm tra.

Khi bấm giải thích, hệ thống gửi đúng tín hiệu và các giá trị do backend phát hiện cho skill/schema riêng. Nếu AI trả sai định danh, sai số, sai định dạng hoặc không sẵn sàng, popup vẫn hiển thị lời giải thích dự phòng bằng tiếng Việt. Dù dùng AI hay fallback, nội dung chỉ mô tả tín hiệu, mức thông thường trước đây và dữ liệu nên đối chiếu; không kết luận nguyên nhân hoặc trách nhiệm cá nhân.

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

Điểm, năm điểm thành phần, mức độ tin cậy, điều kiện xếp hạng và thứ hạng đều được backend tính trước, không phụ thuộc AI. Khi chức năng giải thích AI được gọi từ luồng tích hợp, AI chỉ chuyển đúng các giá trị này thành tiếng Việt; phản hồi sai mã nhà cung cấp, sai điểm, có thuật ngữ kỹ thuật hoặc sai định dạng sẽ được thay bằng giải thích dự phòng. Người dùng vẫn phải tự chọn nhà cung cấp sau khi kiểm tra chi phí, lượng dư và cảnh báo.

Nếu tính năng so sánh đang tắt, cửa hàng ngoài danh sách thử nghiệm, không có quy cách hợp lệ hoặc dữ liệu quy đổi lỗi, người có quyền vẫn tiếp tục quy trình mua thủ công. Không chọn theo thứ hạng mà bỏ qua lượng mua tối thiểu, lượng dư hoặc tổng chi phí.

## 6. Sử dụng AI Smart Import

### 6.1 Phạm vi hỗ trợ

AI Smart Import dùng để tạo mới master data toàn hệ thống từ:

- Excel `.xlsx`;
- Word `.docx` OpenXML;
- PDF có lớp text, có thể chọn/copy nội dung.

Chỉ tạo được Category, Drink, Size, Ingredient và Supplier. Không dùng Smart Import để cập nhật/xóa dữ liệu cũ, nhập Topping, Unit, ProductType, BOM/công thức, giá/ảnh đồ uống hoặc chứng từ mua/kho.

Không upload `.doc`, `.docm`, PDF scan/image-only, file có mật khẩu, macro, nội dung nhúng hoặc liên kết/hành động ngoài. OCR/Vision chưa được hỗ trợ.

### 6.2 Chuẩn bị file

File tối đa 10 MiB. Để giảm review và tăng độ chính xác:

#### Excel

- Dùng `.xlsx` thật, header rõ và dữ liệu bắt đầu ngay dưới header.
- Không đặt dữ liệu cần nhập trong sheet/dòng/cột ẩn.
- Formula phải có cached value vì hệ thống không chạy công thức.
- Có thể đặt nhiều bảng ngang hoặc dọc, nhưng phải có header rõ và không dùng chung cell dữ liệu.
- Không lặp header/alias cho cùng ý nghĩa. Nếu bắt buộc có hai cột cùng nhãn, preview sẽ ghi source key như `Tên [B]` để bạn chọn đúng cột.
- Không đưa StoreId, BranchId, database ID, CreatedBy, RoleId, SQL hoặc command vào file; cột có dữ liệu sẽ bị chặn.

#### DOCX

- Ưu tiên bảng có hàng header hoặc record key-value, ví dụ `Mã danh mục: CAT01`.
- Tách record bằng heading hoặc paragraph trống.
- Accept/Reject Track Changes và bỏ ô gộp nếu có thể; các cấu trúc này bắt buộc người dùng review.
- Không nhúng macro, OLE, remote image, external hyperlink hoặc field command.

#### PDF

- Kiểm tra có thể bôi đen/copy text trước khi upload.
- Bảng nên có header và cột thẳng hàng.
- PDF scan, ảnh chụp hoặc trang có vùng ảnh đáng kể nhưng không chứng minh đọc đủ sẽ trả `PDF_CẦN_OCR`; hãy OCR bằng công cụ tin cậy rồi xuất lại PDF searchable text.
- Không nhúng attachment, JavaScript, Launch hoặc URI action.

Header khuyến nghị:

| Loại | Header chính |
|---|---|
| Category | `CategoryCode`, `Name`, `Icon`, `Active` |
| Drink | `DrinkCode`, `Name`, `Description`, `Category`, `ProductType` |
| Size | `SizeCode`, `Name`, `Description`, `SizeType` |
| Ingredient | `Code`, `Name`, `BaseUnit` |
| Supplier | `Name`, `TaxCode`, `Address`, `Note`, `PrimaryPhone`, `PrimaryContactName`, `PrimaryContactPhone`, `PrimaryContactEmail`, `PrimaryContactPosition` |

Các alias tiếng Việt như **Mã danh mục**, **Tên đồ uống**, **Đơn vị cơ sở** và **Mã số thuế** cũng được nhận diện.

### 6.3 Phân tích file

1. Mở **AI Smart Import** bằng tài khoản có `AIImport.View` và `AIImport.Upload`.
2. Chọn hoặc thả một file `.xlsx`, `.docx` hoặc `.pdf` tối đa 10 MiB.
3. Chọn gợi ý loại dữ liệu nếu file chỉ có một entity; để **Tự động** nếu header đủ rõ hoặc tài liệu có nhiều entity.
4. Bấm **Phân tích**.
5. Đọc SweetAlert kết quả thao tác rồi kiểm tra số dòng hợp lệ, cảnh báo, lỗi, cần xem lại và bỏ qua.

Excel/DOCX/PDF cùng đi qua một pipeline validation. AI chỉ được dùng khi cấu trúc deterministic chưa đủ rõ; AI không quyết định dòng nào được ghi database.

### 6.4 Đọc preview và nguồn dữ liệu

Preview hiển thị:

- format nguồn và extraction mode;
- sheet/region/row đối với Excel;
- section/paragraph/table/row/cell đối với DOCX;
- page/block/bounding box đối với PDF;
- giá trị nguồn và giá trị chuẩn hóa;
- evidence và confidence khi AI được dùng;
- trạng thái, lỗi/cảnh báo và hành động của từng dòng.
- phân loại từng cột nguồn: `MAPPED`, `IGNORED`, `UNKNOWN` hoặc `FORBIDDEN`.

| Trạng thái | Người dùng cần làm |
|---|---|
| Hợp lệ | Đọc lại dữ liệu; có thể giữ để Confirm |
| Cảnh báo | Mở dòng, đọc cảnh báo và xác nhận khi có đủ cơ sở |
| Lỗi | Sửa trường được đánh dấu hoặc bỏ qua dòng |
| Cần xem lại | Đối chiếu normalized data với evidence/locator, sửa nếu cần rồi lưu kiểm tra lại |
| Bỏ qua | Dòng sẽ không được tạo |
| Đã nhập | Dòng đã được transaction Confirm tạo thành công |

Nếu mapping của cả vùng sai, sửa mapping ở group rồi lưu; hệ thống sẽ revalidate toàn bộ dòng liên quan và tăng phiên bản preview.

`UNKNOWN` không bị bỏ âm thầm: nếu có dữ liệu, dòng có warning để bạn kiểm tra. `FORBIDDEN` luôn bị bỏ khỏi mapping và tạo lỗi. Reference mơ hồ hiển thị `REFERENCE_KHÔNG_DUY_NHẤT`; hãy chọn code duy nhất thay vì tên có thể khớp nhiều bản ghi.

### 6.5 Sửa một dòng

1. Bấm **Sửa dòng**.
2. Đọc vùng tổng hợp lỗi ở đầu modal. Bấm tên field nếu muốn cuộn/focus tới input tương ứng.
3. Sửa các input có lỗi inline; lỗi của trường chỉ biến mất tạm sau khi chính trường đó thay đổi.
4. Mở **Dữ liệu nguồn bổ sung** để đối chiếu locator/evidence khi cần.
5. Với Supplier gần trùng, đọc đúng bản ghi match, chọn xác nhận và nhập lý do override nếu form yêu cầu.
6. Bấm **Lưu và kiểm tra lại**. Backend kiểm tra lại toàn bộ schema, reference, duplicate và quyền; client validation không phải kết quả cuối.

Modal dùng một vùng cuộn cho fields, cảnh báo và dữ liệu nguồn; header/footer luôn ở trong khung để các nút vẫn truy cập được trên desktop/mobile. SweetAlert thông báo save/skip hoặc lỗi API phải nằm phía trên modal.

Đối với Category `Icon`:

- để trống hoặc nhập đúng một biểu tượng Unicode;
- `☕`, `❤️`, `👩‍🍳` hợp lệ;
- chữ, số, HTML, hai emoji hoặc emoji kèm văn bản không hợp lệ;
- khi gõ/dán sai, ô khôi phục giá trị hợp lệ gần nhất và báo lỗi;
- có thể dùng **Chọn biểu tượng** hoặc **Xóa**; backend vẫn kiểm tra lại khi lưu.

### 6.6 Xử lý ứng viên confidence thấp và conflict

Với `AI_CONFIDENCE_THẤP`:

1. Mở dòng ở trạng thái **Cần xem lại**.
2. So sánh từng normalized field với evidence và source locator.
3. Sửa nếu cần.
4. Bấm **Lưu và kiểm tra lại** để ghi nhận xác nhận thủ công.

Nếu không còn lỗi schema/reference/duplicate, dòng chuyển sang hợp lệ và lần revalidate sau vẫn giữ xác nhận này. Hệ thống lưu người xác nhận, thời điểm và hash payload; sửa normalized data làm xác nhận cũ tự hết hiệu lực. Xác nhận thủ công không cho phép bỏ qua conflict, overlap, reference ambiguity, record-boundary, lỗi schema hoặc warning chưa xác nhận.

Với hai dòng cùng business key:

- payload giống nhau: giữ bản đầu, bản lặp chuyển **Bỏ qua**;
- payload khác nhau: hiển thị `XUNG_ĐỘT_DỮ_LIỆU_TRONG_TÀI_LIỆU`; người dùng phải chọn/sửa/bỏ qua bản phù hợp, hệ thống không tự đoán.

### 6.7 Phân tích lại, xác nhận, hủy và lịch sử

#### Phân tích lại

- Excel phân tích lại mapping/region chưa rõ.
- DOCX/PDF dùng snapshot text đã trích xuất; hệ thống không giữ binary nguồn.
- Dùng khi đã sửa mapping hoặc AI/provider trước đó chưa sẵn sàng.
- Reanalyze gửi PreviewVersion hiện tại. Nếu tab khác đã sửa phiên, hệ thống trả `PREVIEW_ĐÃ_THAY_ĐỔI`; hãy tải preview mới trước khi thử lại.

#### Xác nhận nhập

1. Đảm bảo không còn lỗi, dòng cần xem lại hoặc warning chưa xác nhận.
2. Kiểm tra lại dòng được chọn/bỏ qua và phiên bản preview.
3. Bấm **Xác nhận nhập** và xác nhận SweetAlert.
4. Hệ thống tạo Category trước Drink và gọi CRUD service tương ứng cho cả năm entity.
5. Chờ SweetAlert hiển thị số dòng đã nhập/bỏ qua; không đóng trang hoặc gửi request mới khi kết quả chưa rõ.

Confirm là transaction toàn phiên: một lỗi làm rollback mọi entity của request. Khi retry sau lỗi mạng chưa rõ kết quả, client phải giữ cùng `Idempotency-Key`; không tạo khóa mới để gửi lặp.

`AIImport.Confirm` chưa đủ để tạo mọi entity. Tài khoản còn cần quyền Create tương ứng:

- `Category.Create`
- `Drink.Create`
- `Size.Create`
- `Ingredient.Create`
- `Supplier.Create`

Chủ doanh nghiệp mặc định xác nhận được cả năm entity. Kế toán/Kho mặc định chỉ xác nhận Ingredient và Supplier; nếu preview còn Category/Drink/Size, hãy bỏ qua các dòng đó hoặc chuyển phiên nghiệp vụ cho người có quyền phù hợp.

#### Hủy và lịch sử

- **Hủy phiên** chỉ áp dụng cho phiên thuộc tài khoản hiện tại và ở trạng thái cho phép hủy.
- **Lịch sử** chỉ hiển thị session của account hiện tại, format và extraction mode; không trả raw document/evidence đầy đủ.
- Snapshot text bị xóa khi phiên hoàn tất, hủy hoặc hết hạn; thời hạn mặc định là 24 giờ.

### 6.8 Lỗi Smart Import thường gặp

| Mã/hiện tượng | Cách xử lý |
|---|---|
| `ĐỊNH_DẠNG_DOC_CŨ_KHÔNG_HỖ_TRỢ` | Chuyển `.doc` sang `.docx` thật |
| `ĐỊNH_DẠNG_KHÔNG_KHỚP_NỘI_DUNG` | Không đổi đuôi giả; upload file đúng MIME/signature |
| `DOCX_BỊ_HỎNG` / `PDF_BỊ_HỎNG` | Mở và lưu lại bằng ứng dụng tin cậy |
| File có mật khẩu | Gỡ password/encryption trước khi upload |
| `NỘI_DUNG_CHỦ_ĐỘNG_KHÔNG_ĐƯỢC_HỖ_TRỢ` | Loại macro, OLE, attachment, external link/action |
| DOCX/PDF vượt giới hạn | Chia file hoặc giảm số trang/bảng/ảnh/resource |
| `PDF_CẦN_OCR` | Chuyển thành PDF searchable text; hệ thống chưa hỗ trợ OCR |
| Ô gộp/Track Changes cần xem lại | Bỏ merge, Accept Changes rồi upload lại hoặc review thủ công |
| `AI_CONFIDENCE_THẤP` | Đối chiếu evidence và lưu kiểm tra lại |
| `AI_TRÍCH_XUẤT_KHÔNG_CÓ_BẰNG_CHỨNG` | Làm rõ cấu trúc tài liệu; output AI đã bị backend từ chối |
| `CỘT_CẤM` | Xóa cột scope/DB ID/audit/SQL/command hoặc bỏ qua dòng |
| `CỘT_KHÔNG_XÁC_ĐỊNH` | Mở dữ liệu bổ sung, xác nhận cột thực sự không cần nhập |
| `XUNG_ĐỘT_ÁNH_XẠ` | Chọn đúng source key theo vị trí cột; không để backend tự chọn |
| `VÙNG_DỮ_LIỆU_CHỒNG_LẤN` | Tách bảng để một cell chỉ thuộc một region hoặc SKIP phần xung đột |
| `REFERENCE_KHÔNG_DUY_NHẤT` | Chọn code duy nhất thay cho tên mơ hồ |
| `KHÔNG_XÁC_ĐỊNH_RANH_GIỚI_BẢN_GHI` | Tách record rõ bằng hàng, heading hoặc paragraph trống |
| `THỨ_TỰ_ĐỌC_PDF_KHÔNG_RÕ` | Xuất lại PDF có reading order/bảng rõ hoặc dùng XLSX/DOCX |
| `REFERENCE_KHÔNG_HỢP_LỆ` | Sửa hoặc tạo sẵn Category, ProductType hay Unit phù hợp |
| `NHÀ_CUNG_CẤP_GẦN_TRÙNG` | Kiểm tra bản ghi match và nhập lý do nếu vẫn tạo |
| `PREVIEW_ĐÃ_THAY_ĐỔI` | Tải preview mới rồi thao tác lại |
| `PREVIEW_CHƯA_SẴN_SÀNG` | Sửa lỗi/review, xác nhận warning hoặc bỏ qua dòng |
| SweetAlert bị modal che | Tải lại asset mới; thông báo chuẩn phải hiển thị trên native dialog |

Không dùng thông báo lỗi để suy ra dữ liệu đã được tạo. Chỉ trạng thái `COMPLETED`, dòng `IMPORTED` và kết quả Confirm idempotent mới là căn cứ hoàn tất.

Khi modal hiển thị checkbox **Tôi đã đối chiếu dữ liệu với bằng chứng nguồn**, hãy mở locator/evidence, kiểm tra đúng bản ghi rồi mới chọn checkbox và bấm **Lưu và kiểm tra lại**. Checkbox này chỉ xác nhận các review reason được phép; nó không xóa lỗi field, xung đột duplicate/reference, cảnh báo chưa xác nhận hay yêu cầu Supplier token.

Sau khi lưu một dòng, hệ thống chỉ kiểm tra lại dòng đó, các dòng trùng business key và Drink phụ thuộc Category liên quan. Vì vậy các dòng không liên quan giữ nguyên trạng thái; nếu Category đổi code/name, hãy kiểm tra các Drink được hệ thống đưa về lỗi/review trong cùng preview mới.

## 7. Quản lý phân quyền

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

## 8. Xử lý lỗi thường gặp

| Hiện tượng | Nguyên nhân cần kiểm tra |
|---|---|
| 403 khi mở Dashboard | Thiếu `App.AdminDashboard`, không có phần hợp lệ, quyền ghi đè bị từ chối hoặc không có phạm vi cửa hàng |
| 403 khi đổi cửa hàng | Cửa hàng không thuộc phân công đang hoạt động; không sửa URL để vượt phạm vi |
| Có phần nhưng biểu đồ bị ẩn | Thiếu quyền dữ liệu riêng của biểu đồ |
| AI báo không có quyền | Thiếu `Dashboard.AI.Use` hoặc thiếu quyền của một biểu đồ chính/bổ trợ |
| Câu hỏi AI bị từ chối | Ô hỏi trống, dưới 3 ký tự, quá 500 ký tự hoặc intent không thuộc catalog an toàn; viết lại thành một mục tiêu rõ |
| AI báo quá nhiều yêu cầu | Chờ rồi gửi lại một lần; không bấm liên tục |
| AI trả giải thích dự phòng | Mô hình bị tắt/lỗi hoặc phản hồi không vượt qua kiểm tra; số liệu backend vẫn dùng được |
| Kết quả biến mất khi đổi bộ lọc | Đây là hành vi đúng: request/context cũ bị hủy để tránh hiển thị dữ liệu sai phạm vi; bấm **Phân tích** lại |
| AI hiển thị “Một phần” | Mở **Giới hạn dữ liệu** và **Xem nguồn dữ liệu**; kiểm tra baseline, COGS, cấu hình và widget lỗi trước khi kết luận |
| AI báo không có dữ liệu | Đối chiếu kỳ, cửa hàng, `StaffScope` và dữ liệu nguồn; không yêu cầu AI suy đoán để lấp dữ liệu |
| Biểu đồ AI không hiển thị | Dùng bảng dữ liệu fallback; thử mở lại tab/đổi kích thước, sau đó báo kỹ thuật nếu bảng cũng lỗi |
| Không có tín hiệu vận hành | Cổng tính năng chưa bật cho cửa hàng, chưa đủ 14 quan sát hoặc biến động chưa vượt ngưỡng |
| Xung đột khi xử lý tín hiệu | Bản ghi đã đổi; tải lại trang và thao tác trên phiên bản mới |
| So sánh trả xung đột | Tính năng chưa bật hoặc cửa hàng ngoài danh sách thử nghiệm |
| Nhà cung cấp chưa có hạng | Chưa đủ số phiếu, thiếu một điểm thành phần, dùng thời gian giao tạm tính hoặc chưa có ít nhất hai ứng viên đủ điều kiện |
| Có quyền so sánh nhưng không mở được hàng chờ tổng hợp | Thiếu `PurchaseAdvice.Consolidate` |
| Tạo đơn thất bại | Kiểm tra trạng thái đề nghị, phiên bản dòng, lượng còn thiếu, quy đổi, lượng mua tối thiểu, quyền tạo đơn và phạm vi |
| Duyệt đơn thất bại | Thiếu `PurchaseOrder.Approve` hoặc đơn chưa ở trạng thái cho phép duyệt |
| Không thấy AI Smart Import | Thiếu `AIImport.View` hoặc tài khoản/nhân viên không còn hoạt động |
| Upload Smart Import bị 403 | Thiếu `AIImport.Upload`; quyền View không thay thế quyền Upload |
| Sửa dòng/phân tích lại bị 403 | Thiếu `AIImport.Analyze` hoặc phiên không thuộc tài khoản hiện tại |
| Confirm Smart Import bị 403 | Thiếu `AIImport.Confirm` hoặc thiếu quyền Create của ít nhất một entity còn được chọn |

## 9. Hướng dẫn liên quan

- [Quy tắc nghiệp vụ Dashboard, AI, tín hiệu vận hành và so sánh nhà cung cấp](./DASHBOARD_AI_ANOMALY_SUPPLIER_BUSINESS_RULES.md)
- [Hướng dẫn nghiệp vụ và kỹ thuật các chức năng AI](./AI_FEATURES_BUSINESS_AND_TECHNICAL_GUIDE.md)
- [Hướng dẫn quản lý phạm vi nhân viên](./STAFF_SCOPE_MANAGEMENT_GUIDE.md)
- [Hướng dẫn StaffHub/POS](./STAFFHUB_USER_BUSINESS_FLOWS.md)
- [Quy tắc StaffHub/POS/ca làm](./STAFFHUB_POS_WORKSHIFT_BUSINESS_RULES.md)
- [Quy tắc nghiệp vụ AI Smart Import chuyên sâu](./AI_SMART_IMPORT_BUSINESS_RULES.md)
- [Hướng dẫn triển khai và sử dụng AI Smart Import chuyên sâu](./AI_SMART_IMPORT_IMPLEMENTATION_AND_USER_GUIDE.md)

Mã xác thực dùng một lần và đăng ký thiết bị bán hàng là luồng POS riêng. Không tìm mã xác thực hoặc nút xác nhận thiết bị trong Dashboard/AI.
