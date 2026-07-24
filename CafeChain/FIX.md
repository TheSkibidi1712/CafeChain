# PROMPT: Refactor Thông báo Kho bằng SignalR và Thiết kế lại AI Dashboard

Hãy đóng vai trò là **Senior ASP.NET Core MVC Developer + AI Engineer có nhiều năm kinh nghiệm**, ưu tiên kiến trúc Layered Architecture, SignalR, thiết kế hệ thống AI và khả năng bảo trì lâu dài.

Bạn hãy **inspect kỹ code hiện tại trước khi chỉnh sửa**. Không được tự ý thay đổi những module nằm ngoài phạm vi yêu cầu.

---

# I. PHẠM VI ĐƯỢC PHÉP CHỈNH SỬA

Trong task này, bạn **CHỈ được phép chỉnh sửa hai nhóm chức năng sau**:

1. **Thông báo kho liên quan đến thiếu hàng từ POS.**
2. **AI Dashboard và các thành phần trực tiếp phục vụ AI Dashboard.**

Ngoài hai phạm vi trên:

* Không refactor module khác.
* Không thay đổi nghiệp vụ khác.
* Không tự ý chỉnh sửa POS.
* Không sửa cấu trúc hệ thống nếu không thật sự cần thiết.
* Không tự ý đổi model/database/schema nếu chưa chứng minh được sự cần thiết.

Đặc biệt:

> **Không sửa `POS Service` nếu không thực sự cần thiết.**

Nếu chỉ cần lấy sự kiện hoặc dữ liệu có sẵn từ POS để phát thông báo thì phải tận dụng logic hiện tại.

Nếu bắt buộc phải chỉnh sửa POS Service, trước tiên phải:

* Giải thích lý do.
* Chỉ ra chính xác đoạn nào cần sửa.
* Chứng minh không thể giải quyết sạch hơn ở Notification/Inventory layer.
* Giữ thay đổi ở mức tối thiểu.

---

# II. REFACTOR THÔNG BÁO KHO + SIGNALR

Hiện tại hệ thống đã có chức năng thông báo kho nhưng tôi muốn bổ sung cơ chế realtime bằng **SignalR**, đặc biệt trong trường hợp POS phát hiện thiếu nguyên liệu/hàng hóa.

## 1. Inspect nghiệp vụ hiện tại

Trước tiên hãy inspect toàn bộ các thành phần liên quan đến:

* Notification.
* Inventory Notification.
* Stock Alert.
* Reorder Suggestion.
* POS kiểm tra tồn kho.
* Thiếu nguyên liệu khi bán hàng.
* Notification Bell.
* Unread Notification.
* Background Service nếu đang có.
* Hub/SignalR nếu dự án đã có.
* Các repository/service liên quan.

Phải tận dụng code hiện tại tối đa, tránh tạo hệ thống Notification thứ hai chạy song song.

---

## 2. SignalR cho cảnh báo thiếu hàng từ POS

Khi POS phát hiện một nguyên liệu hoặc hàng hóa không đủ để phục vụ đơn hàng, hệ thống cần có khả năng tạo cảnh báo kho và gửi realtime tới những người có quyền phù hợp.

Luồng nghiệp vụ mong muốn:

```text
POS
 ↓
Kiểm tra tồn kho/BOM
 ↓
Phát hiện nguyên liệu thiếu hoặc tồn thấp
 ↓
Inventory/Notification Service
 ↓
Lưu Notification vào Database nếu nghiệp vụ yêu cầu
 ↓
SignalR Hub
 ↓
Frontend nhận Notification realtime
 ↓
Badge chuông + Toast/Notification
```

Không để SignalR trở thành nơi chứa business logic.

SignalR chỉ chịu trách nhiệm:

* Push realtime.
* Gửi notification tới đúng user/role/store.
* Đồng bộ badge hoặc danh sách notification trên UI.

Business logic vẫn phải nằm trong Service phù hợp.

---

## 3. Phạm vi người nhận

Hãy inspect Role/Permission hiện tại để xác định chính xác ai được nhận thông báo kho.

Không hard-code role nếu hệ thống đang sử dụng Permission.

Ưu tiên cơ chế:

```text
Permission
→ Store Scope
→ SignalR Group
→ User
```

Ví dụ có thể tổ chức group dạng:

```text
store:{StoreId}
permission:{PermissionCode}
```

hoặc kiến trúc phù hợp hơn với hệ thống hiện tại.

Một nhân viên chỉ được nhận cảnh báo thuộc phạm vi cửa hàng/quyền mà họ được phép xem.

---

## 4. Chống spam thông báo

POS có thể liên tục bán cùng một sản phẩm nên không được tạo hàng chục notification giống nhau.

Hãy thiết kế cơ chế chống duplicate/throttling, ví dụ dựa trên:

* StoreId
* IngredientId/ProductId
* Notification Type
* Trạng thái cảnh báo
* Khoảng thời gian gần nhất

Ví dụ:

```text
Store 1
+
Ingredient 15
+
LOW_STOCK
```

không được liên tục tạo notification mới trong thời gian rất ngắn.

Nếu notification cũ vẫn đang ACTIVE/UNREAD thì ưu tiên cập nhật hoặc bỏ qua notification trùng tùy nghiệp vụ phù hợp.

---

# III. LƯU Ý VỀ FRONTEND

Nếu cần chỉnh sửa giao diện, JavaScript hoặc SignalR Client:

> UI nằm trong project **`CafeChain.Frontend`**.

Không được tìm hoặc chỉnh nhầm UI ở project `CafeChain` nếu giao diện thực tế thuộc `CafeChain.Frontend`.

Hãy inspect rõ cấu trúc solution trước khi sửa.

Frontend cần hỗ trợ tối thiểu:

* Nhận SignalR realtime.
* Cập nhật badge số notification chưa đọc.
* Hiển thị toast/thông báo phù hợp.
* Không reload toàn trang.
* Không tạo duplicate notification trên UI.
* Reconnect SignalR khi kết nối bị mất nếu cần.

---

# IV. INSPECT TOÀN BỘ AI TRONG DỰ ÁN

Trước khi chỉnh AI Dashboard, hãy inspect toàn bộ nghiệp vụ AI hiện đang tồn tại trong project.

Bao gồm nhưng không giới hạn:

* AIService.
* Ollama.
* Gemini nếu có.
* Prompt Builder.
* Skill.md.
* `Resources/skills`.
* AI Analyst.
* AI Dashboard.
* AI Suggestion.
* AI Image.
* Pexels.
* ComfyUI.
* Reorder AI.
* Inventory AI.
* Supplier/Price AI.
* Các rule/statistic engine hiện có.
* Stored Procedure hoặc query phục vụ AI.
* JSON parser/output schema.
* Cache AI nếu có.

Mục đích của bước này là:

> Hiểu toàn bộ kiến trúc AI hiện tại trước khi chỉnh AI Dashboard, tránh tạo thêm một AI architecture mới không tương thích với hệ thống.

---

# V. ĐỌC KỸ FILE AI ANALYST

Tôi sẽ cung cấp file **AI Analyst**.

Bạn phải đọc kỹ file này và phân tích:

* Vai trò của AI Analyst.
* Input.
* Output.
* Rule.
* Prompt.
* Skill.
* Data source.
* Business constraints.
* Những AI nào sử dụng chung logic.
* Những phần nào dành riêng cho Dashboard.

Không được chỉ đọc tên file rồi suy đoán.

Phải đối chiếu nội dung AI Analyst với implementation thực tế trong source code.

---

# VI. THIẾT KẾ LẠI AI DASHBOARD

AI Dashboard hiện tại đang có vấn đề:

> AI bị quá cố định, chủ yếu trả về những thống kê hoặc phỏng đoán được lập trình sẵn.

Tôi không muốn AI Dashboard chỉ hoạt động theo kiểu:

```text
if revenue < x
→ trả câu A

if stock < y
→ trả câu B
```

Rule vẫn có thể tồn tại để:

* Detect anomaly.
* Validate dữ liệu.
* Tạo cảnh báo chắc chắn.
* Làm guardrail.

Nhưng AI phải có khả năng **phân tích linh hoạt dữ liệu thực tế**.

---

# VII. AI DASHBOARD PHẢI PHÂN TÍCH THEO DỮ LIỆU

AI Dashboard phải có thể nhận dữ liệu động như:

* Doanh thu.
* Số đơn.
* AOV.
* Đơn hủy.
* Đồ uống bán chạy.
* Đồ uống bán chậm.
* Giờ cao điểm.
* Ngày cao điểm.
* Store performance.
* Category performance.
* Inventory.
* Ingredient consumption.
* Low stock.
* Reorder.
* Supplier.
* Giá nhập.
* Profit/Margin nếu dữ liệu có.
* Xu hướng ngày/tuần/tháng.
* So sánh kỳ.
* Anomaly.

AI phải dựa vào dữ liệu hiện tại để tự tìm insight thay vì chỉ điền dữ liệu vào các câu có sẵn.

Ví dụ thay vì hard-code:

```text
Doanh thu giảm hơn 20% → cảnh báo doanh thu giảm.
```

AI nên có thể phân tích:

```text
Doanh thu Store A giảm 18%, nhưng số đơn chỉ giảm 3%.
Nguyên nhân chính có thể đến từ AOV giảm mạnh.

Nhóm đồ uống Premium giảm 31%, trong khi nhóm trà vẫn ổn định.

Khung giờ 18:00–21:00 giảm rõ nhất so với 7 ngày trước.
```

Những kết luận mang tính nguyên nhân phải được biểu đạt đúng mức độ chắc chắn, không được bịa.

---

# VIII. AI DASHBOARD PHẢI HỖ TRỢ CÂU HỎI TỰ DO

Người dùng có thể hỏi AI Dashboard các câu như:

```text
Tại sao doanh thu hôm nay giảm?
```

```text
Chi nhánh nào đang hoạt động kém?
```

```text
Đồ uống nào nên đẩy bán?
```

```text
Có nguyên liệu nào sắp thiếu không?
```

```text
So sánh doanh thu tuần này và tuần trước.
```

```text
Tại sao tỷ lệ hủy đơn tăng?
```

```text
Tôi nên chú ý điều gì hôm nay?
```

```text
Phân tích tình hình kinh doanh tháng này.
```

```text
Tạo cho tôi thống kê doanh thu 7 ngày gần nhất.
```

AI phải hiểu intent và lấy đúng dataset cần thiết thay vì sử dụng một prompt cố định cho mọi câu hỏi.

---

# IX. THIẾT KẾ AI THEO INTENT

Có thể thiết kế pipeline theo hướng:

```text
User Question
      ↓
Intent Detection
      ↓
Data Requirement Planning
      ↓
Dashboard Data Service
      ↓
Structured Data / DTO
      ↓
AI Analyst
      ↓
Insight / Statistics / Recommendation
      ↓
Frontend
```

Ví dụ intent:

```text
REVENUE_ANALYSIS
SALES_TREND
ORDER_ANALYSIS
PRODUCT_PERFORMANCE
STORE_COMPARISON
INVENTORY_ANALYSIS
REORDER_ANALYSIS
SUPPLIER_ANALYSIS
ANOMALY_DETECTION
GENERAL_BUSINESS_SUMMARY
STATISTICS_REQUEST
```

Không bắt buộc phải dùng đúng tên trên nếu code hiện tại có cấu trúc tốt hơn.

---

# X. KHÔNG CHO AI QUERY DATABASE TỰ DO

Không cho LLM tự sinh SQL rồi chạy trực tiếp vào database một cách không kiểm soát.

AI chỉ được lấy dữ liệu qua:

```text
Repository
↓
Service
↓
DTO
↓
AI Context
```

hoặc Stored Procedure/query đã được kiểm soát.

AI không được:

* tự chạy DELETE;
* tự chạy UPDATE;
* tự sinh arbitrary SQL;
* tự truy cập DbContext;
* tự thay đổi dữ liệu Dashboard.

Dashboard AI mặc định là read-only.

---

# XI. GIỮ ĐÚNG LAYERED ARCHITECTURE

Tiếp tục tuân thủ architecture của project.

## Controller

```text
Controller
↓
Service
```

Controller không được dùng:

* AppDbContext.
* Repository trực tiếp.
* AI provider trực tiếp.

Controller chỉ:

* Nhận request.
* Validate request.
* Gọi service.
* Trả response.

---

## Service

```text
Service
↓
Repository
```

Service không dùng AppDbContext trực tiếp.

Business logic nằm ở Service.

---

## Repository

Repository chịu trách nhiệm:

* Query dữ liệu.
* Stored Procedure nếu cần.
* Persistence.

Không đặt business rule hoặc AI prompt trong Repository.

---

# XII. AI OUTPUT PHẢI CÓ CẤU TRÚC

Không nên để AI Dashboard chỉ trả về một chuỗi text không kiểm soát.

Ưu tiên structured output tương tự:

```json
{
  "summary": "...",
  "insights": [],
  "statistics": [],
  "anomalies": [],
  "recommendations": [],
  "confidence": 0.0,
  "dataPeriod": {
    "from": "...",
    "to": "..."
  }
}
```

Schema có thể điều chỉnh theo model hiện tại.

Frontend có thể từ đó hiển thị:

* Summary.
* Insight Card.
* Warning.
* Statistic.
* Recommendation.
* Chart data nếu phù hợp.

---

# XIII. PHÂN BIỆT FACT VÀ AI INFERENCE

AI Dashboard phải phân biệt rõ:

### FACT

Dữ liệu lấy trực tiếp từ hệ thống.

Ví dụ:

```text
Doanh thu hôm nay giảm 17,8% so với trung bình 7 ngày.
```

### INFERENCE

Nhận định AI suy ra từ dữ liệu.

Ví dụ:

```text
Mức giảm có thể liên quan đến doanh số nhóm đồ uống Premium giảm mạnh trong khung giờ tối.
```

Không được trình bày inference như một fact chắc chắn.

---

# XIV. AI KHÔNG ĐƯỢC HALLUCINATE

Nếu không đủ dữ liệu:

AI phải trả:

```text
Không đủ dữ liệu để kết luận.
```

hoặc:

```text
Chưa có đủ dữ liệu trong giai đoạn được chọn để xác định nguyên nhân.
```

Không được tự tạo:

* Doanh thu.
* Số đơn.
* Tỷ lệ.
* Store.
* Drink.
* Ingredient.
* Supplier.
* Trend.
* Nguyên nhân.

---

# XV. RULE ENGINE VẪN ĐƯỢC GIỮ LẠI

Các rule hiện có như:

```text
Revenue today < avg7d - 20%
Cancelled orders > avg7d + 30%
Top seller drop > 40%
Stock < MinThreshold
```

không nhất thiết phải xóa.

Hãy đánh giá chúng.

Nếu hợp lý thì giữ chúng làm:

```text
Rule Engine
      ↓
Deterministic Signals
      ↓
AI Analyst
      ↓
Contextual Explanation
```

Tức là:

* Rule phát hiện tín hiệu.
* AI phân tích tín hiệu trong bối cảnh rộng hơn.
* AI giải thích nguyên nhân khả dĩ.
* AI đưa ra insight/recommendation dựa trên nhiều dataset.

Không để Rule Engine biến AI thành hệ thống template.

---

# XVI. PROMPT/SKILL CHO AI DASHBOARD

Hãy inspect các file Skill hiện tại.

Nếu Dashboard AI đã có Skill thì refactor Skill đó.

Nếu cần chỉnh Prompt thì prompt phải yêu cầu AI:

1. Chỉ sử dụng dữ liệu được cung cấp.
2. Không tự tạo số liệu.
3. So sánh nhiều metric trước khi kết luận.
4. Nêu rõ period phân tích.
5. Phân biệt fact và inference.
6. Ưu tiên insight có business impact.
7. Không đưa recommendation chung chung.
8. Không lặp lại toàn bộ raw data.
9. Nếu không có anomaly thì nói rõ hệ thống đang ổn định.
10. Nếu dữ liệu không đủ thì không đoán.

---

# XVII. DOCUMENT NGHIỆP VỤ AI

Ngoài source code, hãy tạo thêm **một file tài liệu `.docx`** giải thích toàn bộ AI hiện có trong dự án.

Tài liệu phải dành cho cả:

* Developer.
* Người sử dụng hệ thống.
* Người cần bảo trì project sau này.

Tên file có thể theo dạng:

```text
CafeChain_AI_Business_And_User_Guide.docx
```

---

# XVIII. NỘI DUNG FILE DOC

Tài liệu phải có tối thiểu các phần:

## 1. Tổng quan hệ thống AI

Giải thích:

* Hệ thống hiện đang có những AI nào.
* Mục đích từng AI.
* AI nào sử dụng Ollama.
* AI nào sử dụng Pexels.
* AI nào sử dụng ComfyUI.
* AI nào sử dụng Rule Engine.
* AI nào sử dụng dữ liệu Dashboard.

---

## 2. Kiến trúc AI

Mô tả luồng:

```text
Frontend
↓
Controller
↓
AI/Application Service
↓
Data Service / Repository
↓
Prompt + Skill
↓
AI Provider
↓
Structured Result
↓
Frontend
```

Giải thích nhiệm vụ của từng layer.

---

## 3. AI Dashboard

Giải thích chi tiết:

* Cách hoạt động.
* Data source.
* Intent.
* Prompt.
* Skill.
* Structured output.
* Rule Engine.
* Fact vs Inference.
* Cách AI tạo insight.
* Cách AI tạo statistic.
* Cách AI trả lời câu hỏi tự do.

---

## 4. Inventory/Reorder AI

Nếu project đang có:

* Giải thích cách kiểm tra tồn kho.
* Threshold.
* Consumption.
* Reorder suggestion.
* Notification.

---

## 5. AI Supplier/Price

Nếu project có chức năng:

* So sánh NCC.
* Giá nhập.
* Package quantity.
* Unit.
* Price history.

thì giải thích chi tiết cách hoạt động.

---

## 6. AI Image

Giải thích toàn bộ pipeline:

```text
AI Suggestion
↓
Search Query
↓
Pexels
↓
Match validation
↓
Fallback
↓
ComfyUI
↓
Generated Image
```

Bao gồm:

* Khi nào dùng Pexels.
* Khi nào fallback ComfyUI.
* Prompt ảnh được tạo thế nào.
* Workflow ComfyUI.
* Checkpoint.
* Positive Prompt.
* Negative Prompt.

---

## 7. Ollama

Hướng dẫn:

* Cài Ollama.
* Kiểm tra Ollama đang chạy.
* Kiểm tra model.
* Endpoint.
* Cách test.
* Khi nào project gọi Ollama.
* Xử lý khi Ollama offline.
* Timeout.
* Fallback nếu đang có.

---

## 8. ComfyUI

Hướng dẫn từng bước:

* Cài.
* Chạy.
* Checkpoint.
* Workflow.
* Port.
* Node configuration.
* Test workflow.
* Project kết nối như thế nào.
* Các lỗi phổ biến.

---

## 9. Pexels

Hướng dẫn:

* API key nằm ở configuration nào.
* Search flow.
* Query generation.
* Match score.
* Fallback.

Không ghi API key thật vào tài liệu.

---

## 10. SignalR Notification

Giải thích:

```text
POS
↓
Inventory Alert
↓
Notification
↓
SignalR
↓
CafeChain.Frontend
```

Bao gồm:

* Hub.
* Group.
* Permission.
* Store scope.
* Badge.
* Reconnect.
* Duplicate prevention.

---

## 11. Hướng dẫn sử dụng AI Dashboard

Viết hướng dẫn cho user theo từng bước.

Ví dụ:

```text
Bước 1: Mở Dashboard.
Bước 2: Chọn cửa hàng/phạm vi nếu có.
Bước 3: Chọn thời gian.
Bước 4: Nhập câu hỏi AI.
Bước 5: AI lấy dữ liệu.
Bước 6: Xem Summary / Insight / Recommendation.
```

Có thêm ví dụ câu hỏi.

---

## 12. Troubleshooting

Phải có bảng lỗi phổ biến:

```text
Ollama không chạy
Không tìm thấy model
Timeout
JSON AI invalid
Pexels không có ảnh phù hợp
ComfyUI offline
SignalR disconnect
Notification không realtime
Không nhận notification do permission
AI Dashboard không có dữ liệu
```

Với mỗi lỗi phải có:

* Nguyên nhân.
* Cách kiểm tra.
* Cách xử lý.

---

# XIX. QUY TRÌNH THỰC HIỆN

Không được bắt đầu sửa code ngay lập tức.

Hãy thực hiện theo thứ tự:

### Bước 1 — Inspect

Đọc toàn bộ source liên quan:

```text
Notification
Inventory
POS stock checking
SignalR
Dashboard
AI
AI Analyst
Skill
Ollama
Pexels
ComfyUI
```

### Bước 2 — Current Architecture

Trình bày kiến trúc hiện tại.

### Bước 3 — Problem Analysis

Chỉ ra:

```text
Current behavior
Problem
Root cause
Impact
```

### Bước 4 — Proposed Architecture

Đề xuất kiến trúc mới nhưng phải tận dụng code hiện tại.

### Bước 5 — Impacted Files

Liệt kê rõ:

```text
File
Reason
Changes
```

### Bước 6 — Implementation

Sau đó mới bắt đầu chỉnh sửa.

### Bước 7 — Verification

Kiểm tra lại:

* Compile.
* Dependency Injection.
* SignalR.
* Permission.
* Store scope.
* Notification duplicate.
* AI structured output.
* AI prompt.
* Null handling.
* Error handling.
* Ollama offline.
* Data empty.
* AI invalid response.

### Bước 8 — Documentation

Cuối cùng tạo file `.docx` hướng dẫn đầy đủ.

---

# XX. NHỮNG ĐIỀU CẤM

Không được:

* Refactor toàn bộ project.
* Sửa POS Service nếu không cần.
* Đổi nghiệp vụ POS.
* Đổi Inventory Transaction.
* Đổi BOM.
* Đổi Order flow.
* Thêm AI mới không liên quan.
* Tạo database/schema mới tùy tiện.
* Cho AI query database trực tiếp.
* Đưa AppDbContext vào Service.
* Đưa Repository vào Controller.
* Hard-code user/role/store nếu hệ thống đã có Permission Scope.
* Hard-code câu trả lời AI.
* Fake dữ liệu AI.
* Fake số liệu Dashboard.
* Tạo notification liên tục không chống duplicate.
* Đặt business logic trong SignalR Hub.
* Chỉnh UI sai project.

Nhớ rằng:

> **UI nếu cần chỉnh sửa nằm ở `CafeChain.Frontend`, không phải project `CafeChain` thông thường.**

---

# XXI. KẾT QUẢ CUỐI CÙNG PHẢI BÁO CÁO

Sau khi hoàn thành, hãy tổng hợp:

```text
1. Files đã inspect
2. Kiến trúc AI hiện tại
3. Các vấn đề phát hiện
4. Kiến trúc SignalR Notification sau khi chỉnh
5. Luồng POS → Inventory Alert → Notification → SignalR
6. Kiến trúc AI Dashboard sau khi chỉnh
7. Các intent AI Dashboard hỗ trợ
8. Các rule được giữ lại
9. Các hard-code/template đã loại bỏ
10. Files đã sửa
11. Files mới tạo
12. Các method quan trọng đã sửa
13. Database có thay đổi hay không
14. POS Service có bị sửa hay không và lý do
15. Cách test SignalR
16. Cách test AI Dashboard
17. Các test case đã kiểm tra
18. Đường dẫn file tài liệu `.docx`
```

Mục tiêu cuối cùng là:

> **Thông báo thiếu hàng từ POS phải realtime, đúng store, đúng quyền và không spam bằng SignalR; AI Dashboard phải trở thành một AI Analyst thực sự có khả năng hiểu câu hỏi, lựa chọn dữ liệu cần phân tích, tạo thống kê và insight động dựa trên dữ liệu thực tế thay vì chỉ trả về các template/rule cố định. Đồng thời toàn bộ hệ thống AI phải được tài liệu hóa rõ ràng để có thể cài đặt, sử dụng, kiểm tra và bảo trì.**
