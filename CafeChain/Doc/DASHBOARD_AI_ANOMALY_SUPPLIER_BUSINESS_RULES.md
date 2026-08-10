# Quy tắc nghiệp vụ Dashboard, AI, tín hiệu vận hành và so sánh nhà cung cấp

> Cập nhật theo mã nguồn và `Scripts/SeedAll.sql` ngày 10/08/2026.

Tài liệu này mô tả hợp đồng nghiệp vụ của Dashboard quản trị, AI Dashboard, tín hiệu bất thường vận hành và so sánh nhà cung cấp. Backend là nguồn quyết định về quyền, phạm vi dữ liệu và số liệu; giao diện chỉ trình bày kết quả đã được backend cho phép. Mô hình AI chỉ diễn giải dữ liệu dẫn chứng, không được tự quyết định hoặc thực hiện nghiệp vụ.

## 1. Nguyên tắc chung

- Quyền được kiểm tra theo mã quyền hiệu lực, không suy ra trực tiếp từ tên vai trò.
- Mọi dữ liệu cửa hàng phải nằm trong phạm vi nhân viên đang còn hiệu lực (`StaffScope`).
- Giao diện ẩn menu hoặc nút chỉ để hỗ trợ sử dụng; backend luôn kiểm tra lại khi nhận yêu cầu.
- Số liệu, điểm, thứ hạng và trạng thái do backend tính. JavaScript không được tự tính lại kết quả nghiệp vụ.
- AI không truy cập cơ sở dữ liệu trực tiếp, không tạo câu SQL và không được nhận dữ liệu ngoài phạm vi đã cấp quyền.
- Lỗi hoặc việc tắt nhà cung cấp mô hình AI không được làm hỏng nghiệp vụ chính. Hệ thống phải trả kết quả xác định theo quy tắc hoặc thông báo không đủ dữ liệu.
- Các tên mã tiếng Anh vẫn được giữ trong cơ sở dữ liệu và hợp đồng API để tương thích, nhưng nội dung chính dành cho người dùng phải hiển thị bằng tiếng Việt.

## 2. Mô hình phân quyền hiệu lực

Một yêu cầu chỉ được chấp nhận khi đồng thời thỏa các lớp kiểm tra sau:

```text
Tài khoản và nhân viên còn hoạt động
→ quyền từ các vai trò đang hoạt động
→ quyền ghi đè ở cấp tài khoản
→ quyền mở ứng dụng
→ quyền phần Dashboard/chức năng/thao tác
→ phạm vi cửa hàng của nhân viên
→ cổng tính năng theo cửa hàng, nếu có
```

Quy tắc hợp quyền:

- Nhiều vai trò được hợp các quyền cho phép nhưng không tự mở rộng phạm vi cửa hàng.
- Từ chối ở cấp tài khoản có ưu tiên cao hơn quyền cho phép từ vai trò hoặc quyền cho phép khác.
- Quyền cho phép ở cấp tài khoản chỉ có hiệu lực cùng các điều kiện tài khoản, nhân viên, phạm vi và cổng tính năng tương ứng.
- Không có cửa hàng hợp lệ trong `StaffScope` thì Dashboard và các chức năng cần phạm vi cửa hàng bị từ chối.
- Cửa hàng do phía trình duyệt gửi lên phải thuộc toàn bộ phạm vi được phép. Hệ thống không âm thầm bỏ cửa hàng vượt quyền rồi tiếp tục tính.
- `SystemAdmin` là vai trò kỹ thuật. Mặc định vai trò này chỉ có các quyền `System.*`, không tự có doanh thu, lợi nhuận, nhân sự, tồn kho hoặc dữ liệu nhà cung cấp.
- `Scripts/SeedAll.sql` là nguồn mặc định cho danh mục quyền và quyền theo vai trò. Quyền ghi đè tài khoản và phân công phạm vi đang có phải được bảo toàn khi cập nhật dữ liệu seed.

### 2.1 Ma trận quyền mặc định liên quan

Dấu **Có** trong bảng là quyền mặc định sau khi chạy `SeedAll.sql`, trước khi áp dụng quyền ghi đè tài khoản. Bán hàng, Khách hàng và Ca trưởng không có quyền mở Admin Dashboard; Ca trưởng chỉ có một số quyền vận hành nhận hàng riêng.

| Nhóm quyền | Chủ doanh nghiệp | Quản lý vùng | Quản lý chi nhánh | Kế toán/Kho | Quản trị hệ thống |
|---|---:|---:|---:|---:|---:|
| Mở Admin Dashboard (`App.AdminDashboard`) | Có | Có | Có | Có | — |
| Dùng AI Dashboard (`Dashboard.AI.Use`) | Có | Có | Có | Có | — |
| Dashboard điều hành cấp chuỗi | Có | — | — | — | — |
| Dashboard vận hành và nhân sự | Có | Có | Có | — | — |
| Dashboard tồn kho, mua hàng và sản phẩm | Có | Có | Có | Có | — |
| Xem/tiếp nhận/xử lý/phản hồi tín hiệu vận hành | Có | Có | Có | — | — |
| Xem dữ liệu để so sánh nhà cung cấp | Có | Có | Có | Có | — |
| Chọn nhà cung cấp (`PurchaseAdvice.SelectSupplier`) | Có | Có | Có | Có | — |
| Tổng hợp đề nghị mua (`PurchaseAdvice.Consolidate`) | — | — | — | Có | — |
| Tạo/gửi đơn đặt hàng | — | — | — | Có | — |
| Duyệt đơn đặt hàng (`PurchaseOrder.Approve`) | Có | — | — | — | — |
| Quản lý phân quyền (`System.Permission.Manage`) | Có | — | — | — | Có |

Ma trận này thể hiện nguyên tắc tách nhiệm vụ mặc định: Kế toán/Kho tổng hợp và tạo đơn đặt hàng; Chủ doanh nghiệp duyệt cam kết đặt hàng. Việc cấp thêm quyền phải có chủ đích và vẫn không được vượt `StaffScope`.

### 2.2 Tổ hợp quyền theo chức năng

| Chức năng | Điều kiện bắt buộc |
|---|---|
| Mở Dashboard | `App.AdminDashboard` + ít nhất một phần Dashboard hợp lệ + ít nhất một cửa hàng trong `StaffScope` |
| Xem một biểu đồ/chỉ số | Điều kiện mở Dashboard + quyền phần + quyền dữ liệu của biểu đồ |
| Phân tích bằng AI Dashboard | Điều kiện mở Dashboard + `Dashboard.AI.Use` + quyền của tất cả biểu đồ được dùng làm dữ liệu dẫn chứng |
| Xem tín hiệu vận hành | `App.AdminDashboard` + `OperationalAnomaly.View` + cửa hàng trong `StaffScope` |
| Tiếp nhận tín hiệu | Quyền xem + `OperationalAnomaly.Acknowledge` |
| Đánh dấu đã xử lý | Quyền xem + `OperationalAnomaly.Resolve` |
| Gửi phản hồi | Quyền xem + `OperationalAnomaly.Feedback` |
| Giải thích tín hiệu bằng AI | Quyền xem + `Dashboard.AI.Use` |
| Lấy bảng so sánh nhà cung cấp | `App.AdminDashboard` + `PurchaseAdvice.View` + `SupplierQuality.View` + `StaffScope` + cổng tính năng của cửa hàng |
| Giải thích điểm nhà cung cấp bằng AI | Điều kiện lấy bảng so sánh + `Dashboard.AI.Use` |
| Mở hàng chờ tổng hợp | `PurchaseAdvice.Consolidate` |
| Chọn nhà cung cấp trong luồng tổng hợp | Quyền tổng hợp + `PurchaseAdvice.SelectSupplier` |
| Tạo/gửi đơn đặt hàng | Quyền theo luồng đề nghị mua + `PurchaseOrder.Create` hoặc `PurchaseOrder.CreateBatch` khi tạo; `PurchaseOrder.Send` khi gửi |
| Duyệt đơn đặt hàng | `PurchaseOrder.Approve` và các điều kiện trạng thái/số lượng/phạm vi của đơn |

Hai mã cũ `PurchaseAdvice.Approve` và `PurchaseAdvice.CreatePurchaseOrder` đang không hoạt động trong ma trận seed. Luồng hiện tại dùng quyền thao tác của đề nghị mua kết hợp với `PurchaseOrder.Create`/`PurchaseOrder.CreateBatch`; không dùng mã cũ để thay thế kiểm tra backend.

## 3. Phân quyền phần và biểu đồ Dashboard

### 3.1 Điều kiện mở từng phần

| Phần Dashboard | Quyền phần | Ít nhất một quyền dữ liệu bắt buộc |
|---|---|---|
| Điều hành | `Dashboard.Executive.View` | `Dashboard.FinancialSummary.View` hoặc `Order.View` |
| Vận hành | `Dashboard.Operations.View` | `POS.WorkShift.View` |
| Tồn kho | `Dashboard.Inventory.View` | `Inventory.View` |
| Mua hàng | `Dashboard.Procurement.View` | `PurchaseOrder.View` hoặc `PurchaseAdvice.View` |
| Sản phẩm | `Dashboard.Product.View` | `Drink.View` |
| Nhân sự | `Dashboard.Workforce.View` | `Shift.View` |

### 3.2 Quyền dữ liệu của nhóm biểu đồ

| Nhóm biểu đồ/chỉ số | Quyền dữ liệu |
|---|---|
| Doanh thu thuần, xếp hạng cửa hàng, phương thức thanh toán | `Dashboard.FinancialSummary.View` |
| Bản đồ nhiệt đơn hàng, trạng thái đơn, đơn theo giờ | `Order.View` |
| Ca làm, doanh số ca, chênh lệch tiền, đối soát ngoại tuyến | `POS.WorkShift.View`; biểu đồ có danh tính nhân viên cần thêm `Staff.View` |
| Tồn kho và biến động kho | `Inventory.View` |
| Ngưỡng tồn và gợi ý nhập | `InventoryThreshold.View` hoặc `ReorderSuggestion.View` theo biểu đồ |
| Chất lượng và sự cố nhà cung cấp | `SupplierQuality.View` |
| Giá mua và chi phí mua hàng | `Receipt.ViewCost` |
| Tiến độ và đơn đặt hàng quá hạn | `PurchaseOrder.View` |
| Biên lợi nhuận, giá vốn, hiệu quả sản phẩm | `Profitability.View` |
| Danh sách sản phẩm, đồ uống, topping và tình trạng công thức | `Drink.View` |
| Lịch và hiệu suất nhân sự | `Shift.View`; hiệu suất cá nhân cần thêm `Staff.View` và `POS.WorkShift.View` |

`IDashboardAuthorizationService` trả danh sách phần, biểu đồ, khả năng thao tác, khả năng dùng AI và phạm vi cửa hàng. Nếu không còn phần Dashboard hợp lệ hoặc không có cửa hàng hợp lệ, yêu cầu bị từ chối trước khi truy vấn dữ liệu nghiệp vụ.

## 4. Nghiệp vụ AI Dashboard

### 4.1 Luồng xử lý

Luồng chính của người dùng là **Phân tích**:

```text
Câu hỏi + bộ lọc Dashboard hiện tại
→ xác định mục tiêu nghiệp vụ
→ lập kế hoạch dùng các biểu đồ có sẵn
→ kiểm tra quyền của toàn bộ biểu đồ
→ truy vấn dữ liệu trong StaffScope
→ tạo gói dữ liệu dẫn chứng
→ tính nhận định/biểu đồ theo quy tắc
→ mô hình AI diễn giải nếu được bật
→ kiểm tra cấu trúc, tên thực thể và mọi con số
→ trả kết quả AI hợp lệ hoặc phần giải thích dự phòng bằng tiếng Việt
```

- Câu hỏi tối đa 500 ký tự và nên có một trọng tâm.
- Bộ lọc kỳ và cửa hàng đang chọn trên Dashboard là phạm vi quyết định. Tên cửa hàng hoặc khoảng thời gian viết trong câu hỏi không được dùng để vượt hoặc âm thầm thay bộ lọc; hệ thống phải cảnh báo khi có khác biệt.
- Kế hoạch dữ liệu chỉ được chọn từ danh mục biểu đồ đã định nghĩa; không sinh SQL động.
- Mọi biểu đồ chính và bổ trợ đều phải được cấp quyền trước khi truy vấn.
- Ngữ cảnh phân tích được lưu tạm theo nhân viên và phải được kiểm tra quyền lại khi yêu cầu giải thích. Ngữ cảnh hết hạn phải yêu cầu người dùng phân tích lại.
- Giới hạn tần suất được áp dụng theo người dùng; vượt giới hạn trả trạng thái quá nhiều yêu cầu, không bỏ qua phân quyền.

### 4.2 Hợp đồng câu trả lời

Câu trả lời được trình bày theo thứ tự:

1. **Trả lời trực tiếp**: nêu đúng trọng tâm trong vài câu.
2. **Dữ liệu làm căn cứ**: tối đa ba ý có số liệu, đơn vị, thực thể và kỳ dữ liệu khớp dữ liệu backend.
3. **Việc cần kiểm tra**: chỉ xuất hiện với rủi ro hoặc ưu tiên vận hành; đây là bước xác minh, không phải lệnh tự động.
4. **Biểu đồ hoặc bảng**: dùng đúng dữ liệu đã cấp quyền.
5. **Nguồn dữ liệu và giới hạn**: cho phép đối soát bộ lọc, tình trạng dữ liệu và lý do dùng giải thích dự phòng.

Mô hình AI không được:

- bịa tên cửa hàng, sản phẩm, nhân viên, nhà cung cấp hoặc con số không có trong dữ liệu dẫn chứng;
- kết luận nguyên nhân, gian lận hoặc cá nhân chịu trách nhiệm chỉ từ một tín hiệu;
- hiển thị khóa kỹ thuật, chỉ dẫn nội bộ hoặc lỗi nhà cung cấp mô hình trong nội dung chính;
- tạo, sửa, duyệt chứng từ; tiếp nhận/xử lý tín hiệu; chọn nhà cung cấp; hoặc thay đổi tồn kho;
- biến đề xuất thành quyết định bắt buộc.

### 4.3 Không có dữ liệu và giải thích dự phòng

- Không có dữ liệu thì trả rõ kỳ/cửa hàng không có dữ liệu phù hợp; không gọi mô hình để suy đoán.
- Mô hình bị tắt, hết thời gian, lỗi kết nối, JSON sai, sai cấu trúc, dùng tên hoặc số ngoài dữ liệu dẫn chứng thì phản hồi đó bị loại.
- Phần dự phòng phải giữ nguyên số liệu, biểu đồ, cảnh báo và mức độ tin cậy do backend tạo.
- Lý do kỹ thuật chỉ xuất hiện trong vùng nguồn dữ liệu hoặc nhật ký, không xuất hiện như kết luận kinh doanh.
- AI là lớp diễn giải phụ trợ; dữ liệu xác định theo quy tắc vẫn sử dụng được khi mô hình không chạy.

## 5. Tín hiệu bất thường vận hành phiên bản 1

### 5.1 Phát hiện

- Worker chỉ phân tích ngày kinh doanh đã hoàn chỉnh theo múi giờ Việt Nam.
- Cửa sổ so sánh là 28 ngày và cần tối thiểu 14 quan sát thật; ngày thiếu dữ liệu không được tự đổi thành 0.
- Nếu có ít nhất bốn lần cùng thứ trong tuần, hệ thống ưu tiên mức thông thường của cùng thứ; nếu không, dùng tối đa 14 quan sát gần nhất.
- Phương pháp xác định dùng trung vị, độ lệch tuyệt đối trung vị, mức lệch tương đối tối thiểu 25% và điểm lệch chuẩn hóa tối thiểu 3,5.
- Doanh thu cần lệch tuyệt đối tối thiểu 500.000 đồng; chênh lệch tiền mặt cần tối thiểu 100.000 đồng.
- Mức **Rất cần ưu tiên** khi điểm lệch tuyệt đối từ 5 hoặc tỷ lệ lệch từ 50%; trường hợp còn lại đạt ngưỡng là **Cần ưu tiên**.
- Khóa duy nhất gồm cửa hàng + ngày kinh doanh + chỉ số + phiên bản phát hiện `v1`. Chạy lại cập nhật dữ liệu dẫn chứng nhưng không tạo bản ghi hoặc thông báo trùng, không đặt lại trạng thái xử lý.

### 5.2 Xử lý và AI giải thích

- Trạng thái hợp lệ: **Mới → Đã tiếp nhận → Đã xử lý**; không tự mở lại hoặc tự đóng.
- Phản hồi độc lập gồm **Hữu ích**, **Không hữu ích**, **Cảnh báo không phù hợp**.
- Mỗi thao tác ghi kiểm tra quyền riêng, phạm vi cửa hàng, phiên bản dòng và lưu nhật ký người thực hiện, thời gian, trạng thái cũ/mới, ghi chú hoặc phản hồi.
- AI chỉ diễn đạt: điều được phát hiện, so sánh với mức thông thường, dữ liệu nên kiểm tra và cảnh báo đây chưa phải kết luận.
- Nội dung chính dùng tên tiếng Việt. Mã chỉ số, phiên bản phát hiện và điểm chuẩn hóa chỉ nằm trong **Thông tin kỹ thuật**.

## 6. So sánh nhà cung cấp phiên bản 1

### 6.1 Điều kiện ứng viên và số lượng mua

Ứng viên chỉ được tính khi nhà cung cấp, quy cách cung ứng nguyên liệu và phạm vi phục vụ cửa hàng đều đang hoạt động; giá, số lượng tối thiểu và quy đổi đơn vị phải hợp lệ.

- Mua đóng gói: `Số gói cần mua = max(làm tròn lên(nhu cầu / lượng cơ sở mỗi gói), số gói tối thiểu)`.
- Mua rời: áp dụng lượng mua tối thiểu và bước tăng số lượng của quy cách.
- Backend trả số lượng mua thực tế, lượng dư, tỷ lệ dư và tổng chi phí. Giao diện không tự tính lại.
- Quy cách được chọn trong popup chỉ là lựa chọn người dùng; bảng so sánh không tự ghi lựa chọn vào đề nghị mua.

### 6.2 Điểm và điều kiện xếp hạng

Lịch sử hiệu suất dùng 180 ngày gần nhất. Trọng số phiên bản `v1`:

| Thành phần | Trọng số |
|---|---:|
| Giá mua | 30% |
| Giao đúng hẹn | 20% |
| Đáp ứng đủ số lượng | 20% |
| Chất lượng hàng | 20% |
| Thời gian giao | 10% |

- Thành phần thiếu dữ liệu giữ giá trị rỗng, không tự đổi thành 0 hoặc 100.
- Thời gian giao tạm tính 30 ngày chỉ giúp lập kế hoạch số lượng; không được dùng để hoàn tất điểm xếp hạng.
- Mặc định cần ít nhất 5 phiếu nhận đã xác nhận trong 180 ngày. Thông báo thiếu dữ liệu phải dùng ngưỡng cấu hình, ví dụ: **“Nhà cung cấp mới có 2/5 phiếu nhận đã xác nhận trong 180 ngày gần nhất.”**
- **Mức độ tin cậy cao**: từ 20 phiếu; **Mức độ tin cậy vừa phải**: 5–19 phiếu; dưới 5 phiếu là **Chưa đủ dữ liệu**.
- Chỉ ứng viên có mức cao/vừa phải và đủ cả năm thành phần mới **Đủ điều kiện xếp hạng**.
- Không có ứng viên đủ điều kiện: giải thích rõ chưa thể xếp hạng.
- Chỉ một ứng viên đủ điều kiện: có thể hiển thị điểm nhưng chỉ dùng tham khảo, không gọi là tốt nhất.
- Từ hai ứng viên đủ điều kiện: xếp **Hạng 1, Hạng 2...** theo kết quả cạnh tranh. Đây vẫn là hỗ trợ quyết định, không phải đề xuất bắt buộc.

### 6.3 Trình bày và giải thích AI

Popup phải dùng các nhãn tiếng Việt sau:

| Mã/thuật ngữ nội bộ | Nội dung hiển thị |
|---|---|
| `ShadowMode` | Dữ liệu thử nghiệm — chế độ quan sát |
| `ranking`, `rankable` | xếp hạng, đủ điều kiện xếp hạng |
| `HIGH` | Mức độ tin cậy cao |
| `MEDIUM` | Mức độ tin cậy vừa phải |
| `INSUFFICIENT_DATA` | Chưa đủ dữ liệu |
| `fallback` | dữ liệu hoặc cách giải thích tạm tính/dự phòng, tùy ngữ cảnh |
| `unknown` hoặc mã không nhận diện | Chưa xác định |
| `metric` | chỉ số đánh giá |
| `confidence` | mức độ tin cậy |

- Nội dung người dùng không được để lộ các từ/mã ở cột trái.
- Giải thích phải nêu điểm tổng, năm điểm thành phần, số phiếu đã xác nhận, dữ liệu còn thiếu và lý do có/không có hạng.
- DTO và API tiếp tục giữ các mã nội bộ như `confidence`, `purchaseMode`, `leadTimeSource`; lớp trình bày chịu trách nhiệm ánh xạ.
- Nếu phản hồi từ mô hình còn chứa thuật ngữ kỹ thuật đã biết, sai cấu trúc hoặc số không khớp, hệ thống loại phản hồi và dùng giải thích tiếng Việt xác định.
- Việc tính chi phí, điểm, đủ điều kiện và thứ hạng là quy tắc backend, không phụ thuộc mô hình AI. Chỉ nút giải thích mới cần `Dashboard.AI.Use`.

### 6.4 Chọn nhà cung cấp và tạo đơn

Luồng nghiệp vụ:

```text
Đề nghị mua đang xem xét
→ mở hàng chờ tổng hợp
→ xem bảng so sánh
→ người có quyền tự chọn nhà cung cấp/quy cách
→ kiểm tra bản tổng hợp
→ Kế toán/Kho tạo đơn hoặc lô đơn
→ backend kiểm tra lại giá, quy đổi, số lượng tối thiểu, phần còn thiếu và phạm vi
→ Chủ doanh nghiệp duyệt theo quyền
```

- Chế độ quan sát không tự chọn nhà cung cấp, không tự tạo/duyệt đơn và không chặn mua thủ công.
- Người tạo không được tự duyệt chính đơn hoặc lô đơn của mình, kể cả khi tài khoản đồng thời có quyền duyệt.
- Khi tạo đơn từ kết quả so sánh, nhật ký phải lưu ảnh chụp cửa hàng, nguyên liệu, nhu cầu, danh sách ứng viên, điểm, mức độ tin cậy, cảnh báo, phiên bản tính, nhà cung cấp người dùng chọn, người thao tác, thời gian và đơn được tạo.

### 6.5 Dữ liệu thử nghiệm tại CafeChain Thủ Dầu Một

Batch `DEMO_SUPPLIER_COMPARISON_HISTORY_V1` trong `SeedAll.sql`:

- lấy toàn bộ nhà cung cấp đang hoạt động có phạm vi và quy cách mua hợp lệ tại CafeChain Thủ Dầu Một;
- chọn một quy cách đại diện ổn định cho mỗi nhà cung cấp;
- tạo 5 đơn đã hoàn tất và 5 phiếu nhận đã xác nhận, cách ngày hiện tại khoảng 35, 65, 95, 125 và 155 ngày;
- có ngày giao dự kiến, dòng đơn, dòng nhận, số lượng chấp nhận, đối soát nhận hàng, biến động tồn và lớp giá vốn;
- chỉ cộng tồn và tạo bút toán cho chứng từ mới; chạy lại chỉ làm mới ngày của dữ liệu seed vào cửa sổ 180 ngày;
- dùng mã nghiệp vụ cố định và kiểm tra cuối batch để không trùng đơn, phiếu, biến động tồn hoặc lớp giá vốn;
- được đặt trước các dữ liệu thử nghiệm tồn thấp để ngưỡng nhập hàng được tính từ tồn thực tế sau khi nhận.

Kết quả mong đợi sau khi seed là mỗi nhà cung cấp hợp lệ tại cửa hàng thí điểm có ít nhất 5 phiếu xác nhận và đủ điều kiện mức tin cậy vừa phải; khác biệt thứ hạng đến từ giá và thời gian giao của quy cách.

## 7. Phạm vi cửa hàng và ngày kinh doanh

- `StaffScope` lấy từ phân công còn hoạt động và quyết định danh sách cửa hàng, nhãn phạm vi và khả năng chọn tỉnh/phường/cửa hàng hoặc tổng hợp nhiều cửa hàng.
- Tổng hợp nhiều cửa hàng chỉ chứa cửa hàng thực sự thuộc phạm vi.
- Bộ lọc dùng tỉnh, phường và cửa hàng thật; không tạo mã vùng giả.
- Ngày kinh doanh dùng `Asia/Ho_Chi_Minh`, dự phòng `SE Asia Standard Time` trên Windows.
- Dữ liệu đơn hàng cũ dùng khoảng ngày kinh doanh địa phương; dữ liệu lưu UTC dùng khoảng UTC chuyển đổi từ ngày Việt Nam.
- Doanh thu thuần của Dashboard và tín hiệu doanh thu dùng chung nguồn `dbo.ufn_AnalyticsOrderFacts`; không tạo công thức doanh thu thứ hai.

## 8. Cổng tính năng, theo dõi và triển khai

| Nhãn nghiệp vụ | Hành vi |
|---|---|
| Tắt | Không tính hoặc không chạy worker; nghiệp vụ thủ công vẫn hoạt động |
| Chế độ quan sát | Có tính và ghi số liệu theo dõi; không tự gửi thông báo hoặc gây tác động nghiệp vụ ngoài ý muốn |
| Danh sách cửa hàng thử nghiệm | Chỉ chạy tại cửa hàng được liệt kê; danh sách rỗng không có nghĩa là toàn chuỗi |
| Triển khai toàn bộ | Chỉ bật rõ ràng sau khi đạt điều kiện nghiệm thu |

`SeedAll.sql` chỉ tạo cấu hình còn thiếu cho so sánh nhà cung cấp tại **CafeChain Thủ Dầu Một** với chế độ quan sát bật và triển khai toàn bộ tắt; không ghi đè cấu hình quản trị đã tồn tại. Bảng theo dõi lần chạy không lưu thông tin nhận dạng cá nhân, câu hỏi đầy đủ hoặc bí mật. Chưa được mô tả tính năng là sẵn sàng cho toàn hệ thống trước khi kiểm thử bảo mật, tính xác định, hồi quy, giải thích dự phòng, nhật ký và dữ liệu thử nghiệm đều đạt.

## 9. Kiểm thử bắt buộc

- Quyền theo vai trò, nhiều vai trò, quyền ghi đè cho phép/từ chối và tài khoản không có phạm vi.
- Sửa `StoreId`/URL ngoài phạm vi phải bị từ chối trước khi truy vấn dữ liệu.
- Phần và biểu đồ Dashboard chỉ xuất hiện đúng tổ hợp quyền.
- AI Dashboard: câu hỏi hợp lệ, không có dữ liệu, mô hình hết thời gian, JSON sai, bịa tên/số, chèn chỉ dẫn độc hại, hết hạn ngữ cảnh và giới hạn tần suất.
- Tín hiệu vận hành: phát hiện lặp không trùng, chuyển trạng thái, phiên bản dòng, phản hồi, nhật ký và giải thích không kết luận sai phạm.
- So sánh nhà cung cấp: 0, 1 và từ 2 ứng viên đủ điều kiện; mua đóng gói/mua rời; thiếu ngày giao; thiếu lịch sử; nội dung tiếng Việt; không tự chọn nhà cung cấp.
- Chạy `SeedAll.sql` hai lần và xác nhận lần hai không tăng đơn, phiếu nhận, biến động tồn hoặc lớp giá vốn.
- Hai nhà cung cấp Topping & Syrup TP.HCM và Bình Dương tại cửa hàng thí điểm phải có điểm, hạng và không còn cảnh báo thiếu số phiếu sau khi seed.

## 10. Liên kết

- [Hướng dẫn người dùng Dashboard, AI, tín hiệu vận hành và so sánh nhà cung cấp](./DASHBOARD_AI_ANOMALY_SUPPLIER_USER_GUIDE.md)
- [Hướng dẫn nghiệp vụ và kỹ thuật các chức năng AI](./AI_FEATURES_BUSINESS_AND_TECHNICAL_GUIDE.md)
- [Hướng dẫn quản lý phạm vi nhân viên](./STAFF_SCOPE_MANAGEMENT_GUIDE.md)
- [Hướng dẫn StaffHub/POS](./STAFFHUB_USER_BUSINESS_FLOWS.md)
- [Quy tắc StaffHub/POS](./STAFFHUB_POS_WORKSHIFT_BUSINESS_RULES.md)

Quyền mã xác thực dùng một lần và đăng ký thiết bị bán hàng độc lập với Dashboard/AI. Không cấp `Dashboard.AI.Use` để thay quyền vận hành POS và không đưa mã xác thực vào dữ liệu dẫn chứng AI.
