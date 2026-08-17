# Quy tắc nghiệp vụ Dashboard, AI, tín hiệu vận hành, so sánh nhà cung cấp và AI Smart Import

> Cập nhật AI Smart Import (16/08/2026): baseline `20260815152712_InitialCreate` đã gồm OCR runtime/multi-file; forward migration hiện hành là `20260816170000_AddPreparedItemTargetStockLevel`. Các invariant RBAC, PreviewVersion, idempotency, transaction `Serializable`, full revalidation và CRUD source-of-truth không thay đổi.

> Cập nhật theo mã nguồn, AI skill/schema, giao diện Dashboard và `Scripts/SeedAll.sql` ngày 14/08/2026.

Tài liệu này mô tả hợp đồng nghiệp vụ của Dashboard quản trị, AI Dashboard, tín hiệu bất thường vận hành, so sánh nhà cung cấp và AI Smart Import. Backend là nguồn quyết định về quyền, phạm vi dữ liệu, số liệu và dữ liệu được phép tạo; giao diện chỉ trình bày kết quả đã được backend cho phép. Mô hình AI chỉ hiểu cấu trúc hoặc diễn giải dữ liệu có bằng chứng, không được tự quyết định hoặc thực hiện nghiệp vụ.

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
| Xem/tải/phân tích/hủy/xem lịch sử AI Smart Import | Có | — | — | Có | — |
| Xác nhận AI Smart Import cho Category/Drink/Size | Có | — | — | — | — |
| Xác nhận AI Smart Import cho Ingredient/Supplier | Có | — | — | Có | — |
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
| Mở AI Smart Import | `AIImport.View` |
| Tải và phân tích file mới | `AIImport.Upload`; các lần sửa mapping/dòng hoặc phân tích lại cần `AIImport.Analyze` |
| Xác nhận nhập | `AIImport.Confirm` + quyền `*.Create` của từng entity còn được chọn trong preview |
| Hủy phiên / xem lịch sử | `AIImport.Cancel` / `AIImport.History`; phiên phải thuộc tài khoản hiện tại |
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

### 4.1 Mô hình nghiệp vụ và luồng xử lý

AI Dashboard là trợ lý phân tích **chỉ đọc**, không phải chatbot tổng quát. Mỗi lần bấm **Phân tích** là một yêu cầu độc lập, có đúng một trọng tâm trả lời và dùng ngữ cảnh bộ lọc Dashboard tại thời điểm gửi yêu cầu.

Luồng chính của người dùng là **Phân tích**:

```text
Câu hỏi + DashboardContext hiện tại
→ BusinessIntent + một AnswerFocus
→ DataPlan từ catalog cho phép
→ kiểm tra quyền của widget chính và widget bổ trợ
→ truy vấn chỉ đọc trong Effective StaffScope
→ EvidencePack + trạng thái chất lượng dữ liệu
→ kết quả/biểu đồ deterministic
→ Ollama diễn giải theo skill và JSON schema, nếu được bật
→ kiểm tra EvidenceId, tên thực thể, số liệu, cấu trúc và phạm vi hành động
→ trả kết quả grounded hoặc fallback deterministic cùng một layout
```

- Câu hỏi không được trống, giao diện yêu cầu tối thiểu 3 ký tự và backend giới hạn tối đa 500 ký tự.
- `BusinessIntent` xác định nhóm nghiệp vụ; `AnswerFocus` xác định duy nhất điều phải trả lời. Câu hỏi ngoài 16 mẫu chuẩn được xếp vào focus động nhưng vẫn chỉ được ánh xạ tới widget có sẵn.
- `DataPlan` chỉ chứa nguồn dữ liệu, trường, chỉ số, bộ lọc, sắp xếp, giới hạn, loại biểu đồ, quy tắc chất lượng và họ fallback đã được catalog cho phép; mô hình không được cung cấp tên bảng, câu SQL hoặc stored procedure để chạy.
- Bộ lọc kỳ và cửa hàng đang chọn trên Dashboard là phạm vi quyết định. Tên cửa hàng hoặc khoảng thời gian viết trong câu hỏi không được dùng để vượt hoặc âm thầm thay bộ lọc; hệ thống phải cảnh báo khi có khác biệt.
- Mọi biểu đồ chính và bổ trợ đều phải được cấp quyền trước khi truy vấn.
- `EvidencePack` chỉ chứa dữ liệu backend đã giới hạn theo kỳ, cửa hàng và quyền: số liệu, đơn vị hiển thị, thực thể, trạng thái dữ liệu và `EvidenceId` dùng để đối soát.
- Ngữ cảnh phân tích được lưu tạm theo nhân viên trong tối đa thời gian cấu hình, mặc định 10 phút, và phải được kiểm tra quyền lại khi dùng. Ngữ cảnh hết hạn phải yêu cầu người dùng phân tích lại.
- Giới hạn tần suất được áp dụng theo người dùng; vượt giới hạn trả trạng thái quá nhiều yêu cầu, không bỏ qua phân quyền.
- Khi người dùng đổi bộ lọc trong lúc đang phân tích, request cũ phải bị hủy. `ContextId`, thứ tự request và `FilterFingerprint` ngăn phản hồi cũ ghi đè kết quả của bộ lọc mới.

### 4.2 Danh mục trọng tâm câu hỏi

Catalog hiện có 16 trọng tâm chuẩn. Mỗi trọng tâm có widget chính và kiểu trả lời xác định; widget bổ trợ, nếu có, chỉ được dùng sau khi qua kiểm tra quyền.

| Nhóm | Câu hỏi/trọng tâm chuẩn | Widget chính | Kiểu trả lời |
|---|---|---|---|
| Tổng quan | Điều cần chú ý trong kỳ | Cảnh báo vận hành | Ưu tiên vận hành |
| Doanh thu | So sánh doanh thu với kỳ trước | Xu hướng doanh thu thuần | So sánh trực tiếp |
| Doanh thu | Cửa hàng hoạt động kém hơn | Xếp hạng cửa hàng | Xếp hạng tăng dần |
| Doanh thu | Yếu tố có thể liên quan đến biến động doanh thu | Xu hướng doanh thu thuần | So sánh trực tiếp, không khẳng định nguyên nhân |
| Doanh thu | Thống kê doanh thu theo ngày | Xu hướng doanh thu thuần | Thống kê thực tế |
| Đơn hàng | Số đơn và tỷ lệ hủy theo cửa hàng | Tổng hợp trạng thái đơn | Xếp hạng |
| Đơn hàng | Phương thức thanh toán dùng nhiều nhất | Cơ cấu phương thức thanh toán | Xếp hạng |
| Sản phẩm | Sản phẩm bán chạy | Top sản phẩm | Xếp theo số lượng bán |
| Sản phẩm | Danh mục bán chạy | Hiệu quả danh mục | Xếp hạng |
| Sản phẩm | Sản phẩm bán chậm | Sản phẩm sản lượng thấp | Xếp hạng tăng dần |
| Sản phẩm | Sản phẩm biên lợi nhuận thấp | Sản phẩm biên lợi nhuận thấp | Xếp hạng tăng dần, chỉ dùng COGS đầy đủ |
| Kho | Nguyên liệu có nguy cơ thiếu | Nguy cơ thiếu tồn | Cảnh báo rủi ro |
| Kho | Nguyên liệu nên đặt lại trước | Gợi ý đặt lại | Ưu tiên vận hành |
| Kho | Xu hướng tiêu thụ nguyên liệu | Tiêu thụ nguyên liệu | Xu hướng |
| Nhà cung cấp | Rủi ro chất lượng hoặc đơn mua quá hạn | Chất lượng nhà cung cấp | Cảnh báo rủi ro |
| Vận hành | Bất thường cần chú ý | Cảnh báo vận hành | Cảnh báo rủi ro |

Quy tắc riêng:

- Top sản phẩm sắp theo số lượng bán giảm dần; nếu bằng nhau, ưu tiên doanh thu thuần rồi mã sản phẩm để kết quả ổn định.
- Tỷ lệ hủy được tính theo tổng số đơn, không lấy trung bình đơn giản giữa các cửa hàng.
- Tỷ lệ từ chối của nhà cung cấp được tính theo lượng hàng nhận, không lấy trung bình đơn giản giữa các nhà cung cấp.
- Không kết luận xu hướng khi có ít hơn hai điểm thời gian.
- Không kết luận biên lợi nhuận khi COGS chưa đầy đủ.
- Không đưa số lượng đặt lại thành khuyến nghị mua khi nhà cung cấp, quy cách, giá, quy đổi hoặc thời gian giao chưa hợp lệ.

### 4.3 Sáu kiểu trả lời

| Kiểu | Quy tắc trình bày |
|---|---|
| So sánh trực tiếp | Nêu giá trị hiện tại, kỳ so sánh và mức chênh lệch; không kéo sang chủ đề khác |
| Xếp hạng | Nêu thứ tự đúng metric và chiều sắp xếp; không tự sinh khuyến nghị nếu câu hỏi chỉ yêu cầu xếp hạng |
| Xu hướng | Chỉ mô tả tăng/giảm/ổn định khi đủ chuỗi dữ liệu |
| Cảnh báo rủi ro | Ưu tiên mức nghiêm trọng/cao, mô tả tín hiệu và bước xác minh; không đoán nguyên nhân |
| Ưu tiên vận hành | Nêu tối đa ba việc đáng chú ý và một bước kiểm tra cụ thể cho việc đứng đầu |
| Thống kê thực tế | Trả số liệu trực tiếp; không thêm hành động hoặc lời khuyên ngoài yêu cầu |

Không được so sánh độ lớn giữa các đơn vị không tương thích như tiền, ngày, khối lượng và thể tích để quyết định mức ưu tiên.

### 4.4 Hợp đồng câu trả lời tập trung

Câu trả lời được trình bày theo thứ tự:

1. **Phạm vi kết quả**: kỳ Dashboard, cửa hàng, trạng thái dữ liệu và độ tin cậy.
2. **Trả lời trực tiếp**: 2–4 câu ngắn, chỉ nêu đúng trọng tâm.
3. **Số liệu chứng minh**: tối đa ba ý; mỗi ý phải tham chiếu evidence hợp lệ và khớp số liệu, đơn vị, thực thể, kỳ dữ liệu backend.
4. **Việc cần kiểm tra**: chỉ xuất hiện với rủi ro hoặc ưu tiên vận hành; đây là bước xác minh, không phải lệnh tự động.
5. **Biểu đồ chính và bảng dữ liệu**: cùng dùng rows từ widget chính đã cấp quyền.
6. **Giới hạn dữ liệu**: nêu thiếu kỳ so sánh, thiếu COGS, thiếu cấu hình, thiếu evidence cấp thực thể hoặc widget lỗi.
7. **Xem nguồn dữ liệu**: vùng thu gọn chứa bộ lọc, widget, trạng thái, EvidenceId và lý do fallback để đối soát.

Các trường narrative Dashboard phiên bản cũ vẫn có thể còn trong DTO để tương thích, nhưng renderer hiện tại không dùng chúng làm phần trả lời chính. Kết quả tập trung không hiển thị riêng các mục **Nhận định**, **Khuyến nghị**, **Tổng quan** hoặc **Kết luận** kiểu cũ.

Mô hình AI không được:

- bịa tên cửa hàng, sản phẩm, nhân viên, nhà cung cấp hoặc con số không có trong dữ liệu dẫn chứng;
- kết luận nguyên nhân, gian lận hoặc cá nhân chịu trách nhiệm chỉ từ một tín hiệu;
- hiển thị khóa kỹ thuật, chỉ dẫn nội bộ hoặc lỗi nhà cung cấp mô hình trong nội dung chính;
- tạo, sửa, duyệt chứng từ; tiếp nhận/xử lý tín hiệu; chọn nhà cung cấp; hoặc thay đổi tồn kho;
- biến đề xuất thành quyết định bắt buộc.

### 4.5 Kiểm chứng phản hồi AI

- AI chỉ nhận payload grounded chứa đúng một focus, thực thể/chủ đề được phép, bộ lọc, trạng thái dữ liệu và `EvidencePack` đã giới hạn.
- Skill và JSON schema bắt buộc riêng cho parser và giải thích; JSON có trường lạ, sai enum, sai cấu trúc hoặc sai định danh phân tích bị từ chối.
- `usedEvidenceIds`, từng số liệu chứng minh và việc cần kiểm tra phải tham chiếu EvidenceId có thật. Mỗi mục chỉ được dẫn từ một đến ba evidence.
- Mọi tên thực thể và claim số phải có trong evidence backend. Số bịa, EvidenceId giả, SQL, prompt nội bộ, chỉ dẫn ngoài phạm vi hoặc action không phù hợp focus làm toàn bộ phần AI bị loại.
- Dữ liệu từ câu hỏi và dataset được coi là dữ liệu, không phải chỉ dẫn có quyền thay đổi quy tắc hệ thống.
- Nội dung chính không hiển thị `AnalysisId`, widget key, enum, EvidenceId, SQL, prompt, mã đơn vị kỹ thuật hoặc lỗi nhà cung cấp mô hình.

### 4.6 Trạng thái dữ liệu và độ tin cậy

| Trạng thái hiển thị | Ý nghĩa nghiệp vụ |
|---|---|
| Đầy đủ | Các dataset bắt buộc trả dữ liệu hợp lệ |
| Một phần | Có dữ liệu nhưng ít nhất một widget, kỳ so sánh hoặc phần evidence chưa đầy đủ |
| Thiếu dữ liệu giá vốn | Không được kết luận chính xác biên lợi nhuận cho phần bị thiếu COGS |
| Thiếu cấu hình | Dữ liệu phụ thuộc ngưỡng/BOM/cấu hình chưa sẵn sàng |
| Không có dữ liệu | Không có quan sát phù hợp trong kỳ và phạm vi đã chọn |
| Lỗi dữ liệu | Các nguồn bắt buộc không truy vấn được; không dùng AI để lấp dữ liệu |

Độ tin cậy là chất lượng của bằng chứng, không phải xác suất kết luận đúng. Backend bắt đầu ở mức cao hơn khi dữ liệu đầy đủ và giảm khi mẫu dưới 10, thiếu kỳ so sánh, thiếu evidence cấp thực thể hoặc có widget lỗi; trạng thái không có dữ liệu/lỗi có độ tin cậy bằng 0.

### 4.7 Không có dữ liệu và giải thích dự phòng

- Không có dữ liệu thì trả rõ kỳ/cửa hàng không có dữ liệu phù hợp; không gọi mô hình để suy đoán.
- Mô hình bị tắt, hết thời gian, lỗi kết nối, JSON sai, sai cấu trúc, dùng EvidenceId/tên/số ngoài dữ liệu dẫn chứng hoặc vi phạm focus thì phản hồi đó bị loại.
- Fallback dùng một trong các họ `Ranking`, `Comparison`, `Trend`, `Risk`, `Statistics`, `OperationalPriority` hoặc `NoData` theo `DataPlan`.
- Phần dự phòng phải giữ nguyên số liệu, biểu đồ, cảnh báo và mức độ tin cậy do backend tạo.
- Fallback trả cùng DTO và layout với kết quả AI để người dùng không phải đổi cách đọc.
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
- Phần giải thích dùng skill/schema riêng và phải echo đúng mã tín hiệu, định danh, giá trị hiện tại và mức thông thường từ backend. Sai định danh, sai số, JSON sai hoặc vượt 1.000 ký tự thì dùng lời giải thích dự phòng xác định.
- Khi AI bị tắt, không sẵn sàng hoặc phản hồi quá thời gian, người dùng vẫn nhận được bản giải thích tiếng Việt từ dữ liệu phát hiện; nghiệp vụ xem/tiếp nhận/xử lý không phụ thuộc mô hình.
- Nội dung chính dùng tên tiếng Việt. Mã chỉ số và phiên bản phát hiện chỉ giữ trong DTO/audit; điểm chênh lệch chuẩn hóa chỉ dùng để đối soát trong vùng **Cơ sở phát hiện**, không đưa vào lời giải thích chính.

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
- Popup deterministic phải nêu điểm tổng, năm điểm thành phần, số phiếu đã xác nhận, dữ liệu còn thiếu và lý do có/không có hạng.
- DTO và API tiếp tục giữ các mã nội bộ như `confidence`, `purchaseMode`, `leadTimeSource`; lớp trình bày chịu trách nhiệm ánh xạ.
- Nếu phản hồi từ mô hình còn chứa thuật ngữ kỹ thuật đã biết, sai cấu trúc hoặc số không khớp, hệ thống loại phản hồi và dùng giải thích tiếng Việt xác định.
- Phản hồi AI phải echo đúng `SupplierId`, điểm tổng và đủ năm điểm thành phần. Sai một giá trị, JSON sai hoặc giải thích vượt 1.000 ký tự đều chuyển sang fallback tiếng Việt.
- AI không được xếp hạng lại, thay trọng số, chọn nhà cung cấp hoặc biến điểm thành quyết định mua. Điểm và hạng đã có trước khi AI được gọi.
- Việc tính chi phí, điểm, đủ điều kiện và thứ hạng là quy tắc backend, không phụ thuộc mô hình AI. Endpoint giải thích chỉ nhận `SupplierId`, điểm tổng, năm điểm thành phần, mức độ tin cậy và cảnh báo; endpoint này cần thêm `Dashboard.AI.Use` ngoài các quyền xem dữ liệu so sánh.

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

## 7. AI Smart Import

### 7.1 Mục tiêu, thuật ngữ và phạm vi

AI Smart Import nhập master data toàn hệ thống theo luồng:

```text
Tài liệu nguồn
→ đoạn nguồn
→ bản ghi ứng viên
→ chuẩn hóa/kiểm tra
→ preview có thể sửa
→ xác nhận nguyên tử
→ bản ghi được tạo qua CRUD service hiện hữu
```

- **Tài liệu nguồn:** file `.xlsx`, `.docx` hoặc PDF có lớp text do người dùng upload.
- **Đoạn nguồn:** sheet/region Excel, paragraph/table DOCX hoặc page/block PDF.
- **Bản ghi ứng viên:** dữ liệu đã trích xuất nhưng chưa được phép ghi database.
- **Source locator:** vị trí quay lại nguồn, như sheet/row, paragraph/table/cell hoặc page/block/bounding box.
- **Evidence:** đoạn nguyên văn chứng minh dữ liệu AI trả về tồn tại trong tài liệu.
- **Extraction mode:** cách tạo ứng viên, deterministic hay AI fallback.
- **Normalized data:** dữ liệu đã trim, chuẩn hóa mã, boolean, reference và schema để người dùng sửa/xác nhận.
- **Xác nhận thủ công:** người dùng đối chiếu ứng viên confidence thấp với evidence/locator rồi bấm **Lưu và kiểm tra lại**; hành động này không miễn bất kỳ lỗi schema, reference, duplicate, quyền hoặc warning nào.

Chỉ hỗ trợ thao tác `CREATE` cho năm entity:

| Entity | Dữ liệu chính | Reference/giới hạn đặc biệt |
|---|---|---|
| Category | `CategoryCode`, `Name`, `Icon`, `Active` | Icon rỗng hoặc đúng một grapheme Unicode; Category tạo trước Drink |
| Drink | `DrinkCode`, `Name`, `Description`, `Category`, `ProductType` | Category có sẵn hoặc cùng phiên; ProductType phải có sẵn; không nhập giá/ảnh |
| Size | `SizeCode`, `Name`, `Description`, `SizeType` | `SizeType` chỉ `Cup` hoặc `Volume` |
| Ingredient | `Code`, `Name`, `BaseUnit` | Unit phải có sẵn; Smart Import không tạo Unit |
| Supplier | thông tin pháp lý, địa chỉ, ghi chú, điện thoại và đầu mối chính | Mã do service sinh; chỉ TaxCode là hard duplicate |

Không hỗ trợ Topping, Unit, ProductType, công thức/BOM, giá/ảnh đồ uống, contact phụ, chứng từ/giao dịch, `UPDATE`, `UPSERT` hoặc `DELETE`. Dữ liệu ngoài whitelist phải nhận `KHÔNG_THUỘC_PHẠM_VI` và không được Confirm.

Smart Import là master data toàn hệ thống, dùng global scope `StoreId = 0`. File không được cung cấp `StoreId`, `BranchId`, database ID, SQL hoặc lệnh thực thi.

### 7.2 Nguồn sự thật và quy tắc entity

Confirm không ghi trực tiếp bằng `DbContext.Add`. Hệ thống phải gọi đúng service tạo Category, Drink, Size, Ingredient hoặc Supplier hiện hữu; validation CRUD và unique constraint là lớp quyết định cuối.

Quy tắc duplicate:

- Category: hard duplicate theo mã hoặc tên.
- Drink: hard duplicate theo mã hoặc tên.
- Size: hard duplicate theo mã hoặc tên.
- Ingredient: hard duplicate theo mã hoặc tên.
- Supplier: chỉ `TaxCode` là hard duplicate. Tên, hotline, điện thoại/email đầu mối và địa chỉ là soft duplicate; muốn tiếp tục phải dùng warning token của `AdminSupplierService`, xác nhận cảnh báo và nhập lý do khi được yêu cầu.

Category `Icon` chỉ chấp nhận rỗng hoặc một grapheme Unicode hoàn chỉnh. Chữ, số, HTML, emoji kèm văn bản hoặc nhiều biểu tượng bị từ chối; emoji ghép như `❤️` và `👩‍🍳` vẫn tính là một biểu tượng. UI giữ giá trị hợp lệ gần nhất khi người dùng gõ/dán sai, nhưng backend `CategoryIconPolicy` luôn kiểm tra lại. Giới hạn lưu trữ vẫn là 10 ký tự.

### 7.3 Pipeline theo định dạng

| Định dạng | Cách trích xuất | Trường hợp bắt buộc xem lại hoặc từ chối |
|---|---|---|
| Excel `.xlsx` | OpenXML, shared/inline string, number, boolean, date style, cached formula; phát hiện sheet/region/header | Bỏ sheet/dòng/cột ẩn; không chạy formula/macro; mapping không rõ mới dùng AI |
| Word `.docx` | Body-only, logical merged grid, revision-aware text, table/key-value và semantic block | `.doc`/`.docm` không hỗ trợ; ownership merge/revision/nested boundary mơ hồ bắt review; active content bị từ chối |
| PDF text/OCR/mixed | Security preflight, page classifier, rotation/top-left, Unicode, table/key-value; OCR `prebuilt-read` chỉ cho trang cần | OCR tắt trả `PDF_CẦN_OCR`; provider/resource/output lỗi trả typed code; reading order/table qua trang mơ hồ bắt review |

DOCX/PDF chỉ thay đổi bước lấy dữ liệu nguồn. Mọi ứng viên sau đó dùng chung ImportSchema, normalization, reference, duplicate, dependency, preview, PATCH, RBAC, idempotency, transaction và CRUD Confirm với Excel.

OCR production dùng Tesseract local với model `tessdata_fast` `vie+eng`; `UseOcr=false` vẫn là mặc định theo từng lần import và health check phải xác minh executable cùng model. Source/layout/OCR/AI confidence tách riêng, critical field OCR dưới `0,85` bắt manual review. Snapshot không lưu binary/ảnh render và Confirm không gọi OCR lại.

### 7.4 Ranh giới AI và bằng chứng

Deterministic extraction luôn chạy trước. AI chỉ nhận semantic chunk giới hạn khi cấu trúc không đủ rõ và nội dung tài liệu luôn được coi là dữ liệu không tin cậy.

Output AI chỉ hợp lệ khi:

- JSON đúng schema có cấu trúc;
- entity/field thuộc whitelist và không phải database ID;
- confidence nằm trong `[0,1]`;
- evidence là substring nguyên văn của chunk và bao phủ mọi giá trị không rỗng;
- không chứa SQL, lệnh hoặc chỉ dẫn thay đổi whitelist/schema.

Sai JSON, evidence giả hoặc output ngoài schema phải bị loại bằng lỗi typed. Ứng viên dưới `ReviewConfidenceThreshold`, mặc định 0,70, vẫn được giữ với `AI_CONFIDENCE_THẤP` và `REVIEW_REQUIRED`; không được âm thầm bỏ qua.

Semantic AI dùng table/section/heading/paragraph/page block nguyên tử, tối đa hai attempt cho transport/transient hoặc malformed JSON. Taxonomy metadata gồm `AI_TRANSPORT_ERROR`, `AI_SCHEMA_ERROR`, `AI_SEMANTIC_EVIDENCE_ERROR`; cùng key khác payload giữa chunk tạo `XUNG_ĐỘT_TRÍCH_XUẤT`.

Cùng business key và cùng normalized payload trong một tài liệu chỉ giữ ứng viên đầu; nguồn lặp chuyển `SKIPPED` với `TRÙNG_TRONG_FILE`. Cùng key nhưng payload khác chuyển `REVIEW_REQUIRED` với `XUNG_ĐỘT_DỮ_LIỆU_TRONG_TÀI_LIỆU`. Chunk overlap không được tạo dữ liệu kép.

### 7.5 Guard bảo mật và giới hạn

Extension, Content-Type và signature phải khớp. Toàn file bị từ chối khi hỏng, có mật khẩu/mã hóa, vượt resource limit hoặc chứa nội dung chủ động:

- DOCX macro, OLE, embedded binary, external relationship/hyperlink/resource hoặc field command;
- PDF embedded file, JavaScript, Launch hoặc URI action;
- ZIP bomb hoặc compression ratio/expanded size vượt giới hạn.

Hệ thống không fetch URL, mở ứng dụng ngoài, chạy macro/script/command hoặc ghi raw document/prompt đầy đủ vào log.

| Giới hạn mặc định | Giá trị |
|---|---:|
| Kích thước mỗi file | 10 MiB |
| DOCX expanded / compression ratio | 100 MiB / 100:1 |
| Excel | 20 sheet; 10.000 dòng/sheet; 20.000 dòng tổng; 100 cột/sheet; 200.000 cell |
| DOCX | 20.000 paragraph; 200 table; 20.000 table row; 200.000 cell |
| PDF | 200 page; 20.000 text block; 1.000 image |
| Ngưỡng vùng ảnh PDF mặc định | 15% diện tích trang |
| Tổng ký tự trích xuất | 1.000.000 |
| AI chunk | 100 chunk; 12.000 ký tự/chunk; overlap 500 |
| Thời hạn session | 24 giờ |

Tất cả giới hạn nằm trong `AIImportOptions`; request-size filter đọc cùng cấu hình và service kiểm tra lại kích thước file thực.

### 7.6 Preview, sửa dòng và trạng thái

Mọi vấn đề validation có hợp đồng thống nhất gồm mã, thông điệp, field tùy chọn, severity, source locator và metadata xử lý. Severity được ưu tiên `ERROR > REVIEW > WARNING`; UI không phân tích chuỗi thông báo để đoán field hoặc hành động.

Đối với cột nguồn:

- `MAPPED` được dùng cho normalized data;
- `IGNORED` là metadata/projection đã biết và được hiển thị là bị bỏ qua;
- `UNKNOWN` có dữ liệu tạo warning;
- `FORBIDDEN` như StoreId, BranchId, database ID, actor, role, SQL hoặc command tạo lỗi chặn.

Header trùng giữ định danh cột riêng và bắt buộc remap. Workbook có thể có nhiều sheet, nhiều bảng ngang/dọc và Category–Drink trong cùng region; vùng chồng lấn không được auto import. Reference trả trạng thái rõ ràng, trong đó match code/name không duy nhất chuyển `REFERENCE_KHÔNG_DUY_NHẤT`, không lấy record đầu tiên.

| Trạng thái candidate | Ý nghĩa |
|---|---|
| `VALID` | Đủ điều kiện tạo nếu toàn phiên sẵn sàng |
| `WARNING` | Có cảnh báo phải đọc và xác nhận |
| `ERROR` | Có lỗi schema/reference/duplicate; không Confirm được |
| `REVIEW_REQUIRED` | Mapping, conflict hoặc confidence cần người dùng xử lý |
| `SKIPPED` | Người dùng/hệ thống quyết định không tạo |
| `IMPORTED` | Đã tạo thành công |

Mọi PATCH group/item và Reanalyze phải gửi `expectedPreviewVersion`; backend revalidate và tăng `PreviewVersion`. Client stale nhận HTTP 409 `PREVIEW_ĐÃ_THAY_ĐỔI` và phải tải lại.

Manual review là trạng thái riêng gồm account, thời điểm và hash normalized payload. Chỉ review reason được phép như confidence thấp, Track Changes hoặc ô gộp mới hiển thị checkbox đối chiếu bằng chứng; người dùng phải chủ động chọn checkbox rồi bấm **Lưu và kiểm tra lại**. Payload đổi làm xác nhận hết hiệu lực; conflict, overlap, reference ambiguity, record boundary và lỗi schema không thể bị bypass bằng manual review. Checkbox warning và Supplier server token là hai cơ chế độc lập.

Phân tích lại dùng `expectedPreviewVersion` bắt buộc và optimistic concurrency claim. Nếu một request khác đã claim hoặc thay preview, request cũ trả `409 PREVIEW_ĐÃ_THAY_ĐỔI` và không được ghi đè dữ liệu mới.

Từ Phase 9, sửa một dòng hoặc một group chỉ kiểm tra lại dependency closure liên quan: business-key cohort cũ/mới và Drink phụ thuộc Category. Confirm vẫn full validation và lập execution plan từ registry tập trung, nên Category luôn đứng trước Drink. Reanalyze lỗi sau khi claim chuyển phiên sang `FAILED`, không để phiên treo ở `ANALYZING`.

Trong modal sửa dòng:

- lỗi theo trường nằm cạnh đúng input và chỉ được gỡ tạm khi người dùng thật sự thay đổi trường đó;
- lỗi cấp dòng/review nằm ở đầu modal; tên field có thể focus tới input tương ứng;
- review chỉ do confidence thấp phải hướng dẫn đối chiếu evidence rồi **Lưu và kiểm tra lại**, không hiển thị sai rằng có field được đánh dấu;
- warning Supplier phải hiển thị match, checkbox xác nhận và lý do override;
- header/footer luôn nằm trong khung, còn field, cảnh báo và dữ liệu nguồn dùng một vùng cuộn chung trên desktop/mobile;
- lỗi field/dòng và warning vẫn inline; SweetAlert2 chỉ dùng cho thông báo thao tác, xác nhận và lỗi API, kể cả khi native dialog đang mở.

State machine của session:

`UPLOADED → ANALYZING → VALIDATING → READY_TO_PREVIEW → IMPORTING → COMPLETED`

Trạng thái ngoại lệ: `FAILED`, `CANCELLED`, `EXPIRED`. Session và history chỉ thuộc tài khoản đã upload.

### 7.7 Confirm, idempotency, transaction và quyền

Confirm bị chặn nếu còn `ERROR`, `REVIEW_REQUIRED` hoặc warning chưa xác nhận. Việc xác nhận thủ công ứng viên confidence thấp chỉ được giữ khi mọi validation khác đã đạt.

Confirm dùng `Idempotency-Key`, session ID, `PreviewVersion` và lựa chọn/value của item. Toàn bộ claim session, tạo entity, ghi kết quả và đánh dấu idempotency thành công nằm trong transaction `Serializable`. Category tạo trước Drink; một lỗi hoặc unique race rollback toàn phiên và không lộ SQL exception.

Quyền Smart Import:

- `AIImport.View`
- `AIImport.Upload`
- `AIImport.Analyze`
- `AIImport.Confirm`
- `AIImport.Cancel`
- `AIImport.History`

`AIImport.Confirm` không thay thế quyền tạo entity. Preview có Category, Drink, Size, Ingredient hoặc Supplier còn được chọn phải kiểm thêm `Category.Create`, `Drink.Create`, `Size.Create`, `Ingredient.Create` hoặc `Supplier.Create` tương ứng.

Theo seed mặc định, Chủ doanh nghiệp có toàn bộ quyền Smart Import và quyền tạo cả năm entity. Kế toán/Kho có toàn bộ quyền Smart Import nhưng chỉ có quyền tạo Ingredient và Supplier; vì vậy phải `SKIP` Category/Drink/Size hoặc chuyển phiên cho người có quyền phù hợp trước khi Confirm.

### 7.8 Lưu trữ, retention và audit

- Session lưu format, extraction version, metadata và snapshot text/OCR tối thiểu; không lưu binary upload/rendered image.
- Group lưu stable source region, locator, extraction mode và layout confidence.
- Item lưu raw/normalized data, source trace, field evidence/provenance và source/layout/OCR/AI confidence tách riêng.
- Audit lưu format/mode, OCR usage/page/provider/version/confidence summary, AI chunk count, actor, trạng thái và kết quả; không lưu raw OCR/full evidence/full prompt.
- Snapshot bị xóa khi session `COMPLETED`, `CANCELLED` hoặc `EXPIRED`; raw data/evidence/secret không xuất hiện trong application log hoặc history response.

Migration theo đúng thứ tự `20260815152712_InitialCreate` (baseline chính thức) rồi `20260816170000_AddPreparedItemTargetStockLevel` (forward migration). Database development/test đã ghi migration ID khác phải được nâng cấp có kiểm soát hoặc tạo lại nếu disposable; không tự động xóa database production.

## 8. Phạm vi cửa hàng và ngày kinh doanh

- `StaffScope` lấy từ phân công còn hoạt động và quyết định danh sách cửa hàng, nhãn phạm vi và khả năng chọn tỉnh/phường/cửa hàng hoặc tổng hợp nhiều cửa hàng.
- Tổng hợp nhiều cửa hàng chỉ chứa cửa hàng thực sự thuộc phạm vi.
- Bộ lọc dùng tỉnh, phường và cửa hàng thật; không tạo mã vùng giả.
- Ngày kinh doanh dùng `Asia/Ho_Chi_Minh`, dự phòng `SE Asia Standard Time` trên Windows.
- Dữ liệu đơn hàng cũ dùng khoảng ngày kinh doanh địa phương; dữ liệu lưu UTC dùng khoảng UTC chuyển đổi từ ngày Việt Nam.
- Doanh thu thuần của Dashboard và tín hiệu doanh thu dùng chung nguồn `dbo.ufn_AnalyticsOrderFacts`; không tạo công thức doanh thu thứ hai.

## 9. Cổng tính năng, theo dõi và triển khai

| Nhãn nghiệp vụ | Hành vi |
|---|---|
| Tắt | Không tính hoặc không chạy worker; nghiệp vụ thủ công vẫn hoạt động |
| Chế độ quan sát | Có tính và ghi số liệu theo dõi; không tự gửi thông báo hoặc gây tác động nghiệp vụ ngoài ý muốn |
| Danh sách cửa hàng thử nghiệm | Chỉ chạy tại cửa hàng được liệt kê; danh sách rỗng không có nghĩa là toàn chuỗi |
| Triển khai toàn bộ | Chỉ bật rõ ràng sau khi đạt điều kiện nghiệm thu |

Giới hạn vận hành mặc định của AI Dashboard:

| Cấu hình | Mặc định hiện tại |
|---|---:|
| Độ dài câu hỏi tối đa | 500 ký tự |
| Khoảng phân tích tối đa | 366 ngày |
| Thời gian lưu context/kết quả | 10 phút |
| Số request tối đa theo người dùng | 20/phút |
| Intent parser | Bật trong cấu hình chung |
| Diễn giải bằng mô hình | Tắt trong cấu hình chung; môi trường Development đang bật |

Tắt intent parser hoặc mô hình không làm mất catalog/fallback deterministic. Timeout của provider, health check và trạng thái mô hình không được dùng để bỏ qua authorization.

Đối với so sánh nhà cung cấp, các khóa runtime `supplier_intelligence_enabled`, `supplier_intelligence_shadow_mode`, `supplier_intelligence_full_rollout` và `supplier_intelligence_store_allowlist` trong `SystemSettings` được ưu tiên; chỉ khi không có khóa runtime nào mới dùng cấu hình ứng dụng. `SeedAll.sql` chỉ tạo cấu hình còn thiếu cho **CafeChain Thủ Dầu Một** với chế độ quan sát bật và triển khai toàn bộ tắt; không ghi đè cấu hình quản trị đã tồn tại.

Bảng theo dõi lần chạy không lưu thông tin nhận dạng cá nhân, câu hỏi đầy đủ hoặc bí mật. Chưa được mô tả tính năng là sẵn sàng cho toàn hệ thống trước khi kiểm thử bảo mật, tính xác định, hồi quy, giải thích dự phòng, nhật ký và dữ liệu thử nghiệm đều đạt.

## 10. Kiểm thử bắt buộc

- Quyền theo vai trò, nhiều vai trò, quyền ghi đè cho phép/từ chối và tài khoản không có phạm vi.
- Sửa `StoreId`/URL ngoài phạm vi phải bị từ chối trước khi truy vấn dữ liệu.
- Phần và biểu đồ Dashboard chỉ xuất hiện đúng tổ hợp quyền.
- AI Dashboard: câu hỏi hợp lệ, không có dữ liệu, mô hình hết thời gian, JSON sai, bịa tên/số, chèn chỉ dẫn độc hại, hết hạn ngữ cảnh và giới hạn tần suất.
- AI Dashboard: đủ 16 câu hỏi canonical ánh xạ đúng `AnswerFocus`/widget/kiểu trả lời; câu hỏi động không tạo nguồn dữ liệu ngoài catalog.
- Evidence contract: EvidenceId giả, claim số không grounded, action sai focus, nội dung SQL/prompt và narrative không có evidence đều bị loại.
- Chất lượng dữ liệu: `OK`, `PARTIAL`, `PARTIAL_COGS`, `MISSING_CONFIG`, `NO_DATA`, `ERROR`; độ tin cậy giảm đúng khi mẫu nhỏ, thiếu baseline/evidence hoặc widget lỗi.
- UI AI: hủy request khi đổi bộ lọc, kiểm `ContextId`/`FilterFingerprint`/request sequence, biểu đồ lỗi chuyển sang bảng và nội dung động được đưa vào DOM dưới dạng text an toàn.
- Tín hiệu vận hành: phát hiện lặp không trùng, chuyển trạng thái, phiên bản dòng, phản hồi, nhật ký và giải thích không kết luận sai phạm.
- Giải thích typed cho tín hiệu/nhà cung cấp: echo sai định danh hoặc số, thuật ngữ kỹ thuật, timeout và JSON sai phải dùng fallback xác định.
- So sánh nhà cung cấp: 0, 1 và từ 2 ứng viên đủ điều kiện; mua đóng gói/mua rời; thiếu ngày giao; thiếu lịch sử; nội dung tiếng Việt; không tự chọn nhà cung cấp.
- Chạy `SeedAll.sql` hai lần và xác nhận lần hai không tăng đơn, phiếu nhận, biến động tồn hoặc lớp giá vốn.
- Hai nhà cung cấp Topping & Syrup TP.HCM và Bình Dương tại cửa hàng thí điểm phải có điểm, hạng và không còn cảnh báo thiếu số phiếu sau khi seed.
- AI Smart Import guard: extension/signature giả, file hỏng/mật khẩu, ZIP bomb, active content, resource limit và PDF cần OCR.
- Parser: Excel regression; DOCX table/key-value/list/ô gộp/Track Changes; PDF reading order/table/header-footer/text-image-mixed.
- AI extraction: chunk/overlap, prompt injection, invalid JSON, evidence giả, confidence thấp, field/entity/ID/SQL/lệnh ngoài schema.
- Preview UI: lỗi field/lỗi dòng, modal cuộn desktop/mobile, SweetAlert trên native dialog, Icon một grapheme, warning Supplier và stale `PreviewVersion`.
- Confirm: đủ năm entity, reference Category→Drink, duplicate/conflict, entity Create permission, idempotency retry, concurrent duplicate, transaction `Serializable` và rollback toàn phiên.

## 11. Liên kết

- [Hướng dẫn người dùng Dashboard, AI, tín hiệu vận hành và so sánh nhà cung cấp](./DASHBOARD_AI_ANOMALY_SUPPLIER_USER_GUIDE.md)
- [Hướng dẫn nghiệp vụ và kỹ thuật các chức năng AI](./AI_FEATURES_BUSINESS_AND_TECHNICAL_GUIDE.md)
- [Hướng dẫn quản lý phạm vi nhân viên](./STAFF_SCOPE_MANAGEMENT_GUIDE.md)
- [Hướng dẫn StaffHub/POS](./STAFFHUB_USER_BUSINESS_FLOWS.md)
- [Quy tắc StaffHub/POS](./STAFFHUB_POS_WORKSHIFT_BUSINESS_RULES.md)
- [Quy tắc nghiệp vụ AI Smart Import chuyên sâu](./AI_SMART_IMPORT_BUSINESS_RULES.md)
- [Hướng dẫn triển khai và sử dụng AI Smart Import chuyên sâu](./AI_SMART_IMPORT_IMPLEMENTATION_AND_USER_GUIDE.md)

Quyền mã xác thực dùng một lần và đăng ký thiết bị bán hàng độc lập với Dashboard/AI. Không cấp `Dashboard.AI.Use` để thay quyền vận hành POS và không đưa mã xác thực vào dữ liệu dẫn chứng AI.

## 12. Bổ sung hợp đồng AI Smart Import

- Duplicate header được giải quyết bằng Group Mapping theo source key vị trí; chỉnh tay normalized value không giải quyết `XUNG_ĐỘT_ÁNH_XẠ`.
- Cancel chỉ được coi thành công khi server chuyển trạng thái; client phải đóng dialog và vô hiệu hóa preview cũ sau success, không làm mất draft khi fail.
- System Settings dùng hai tab độc lập: âm kho và OCR. Lưu tab OCR không thay đổi policy âm kho và ngược lại; cả hai dùng quyền `Settings.View`/`Settings.Update` hiện hữu.
- OCR effective bằng Tesseract executable + model local đã health-check `READY` AND import `UseOcr`. System Settings không có switch OCR toàn hệ thống. OCR không có secret, không gửi tài liệu ra cloud; response/audit không chứa đường dẫn máy chủ, ảnh render hoặc OCR text đầy đủ.
- Multi-file dùng `ImportSession 1—N ImportSourceDocument`; duplicate/reference/dependency được đánh giá xuyên file, còn Confirm/idempotency/transaction vẫn cấp phiên.
- Nguồn lỗi chặn Confirm; không tự nhập riêng phần hợp lệ và không âm thầm bỏ file.
