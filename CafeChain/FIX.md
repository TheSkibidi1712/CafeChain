# PROMPT REFACTOR VÀ HOÀN THIỆN AI DASHBOARD – CAFECHAIN

## 1. Vai trò

Hãy đóng vai đồng thời là:

* Senior Software Architect.
* Senior ASP.NET Core MVC Developer.
* Senior AI Engineer.
* Senior Data/BI Engineer.
* BA/PM có kinh nghiệm triển khai hệ thống quản lý chuỗi F&B.

Nhiệm vụ của bạn là **inspect, phân tích và hoàn thiện AI Dashboard của dự án CafeChain** dựa trên mã nguồn, Stored Procedure, Dashboard Analytics, dữ liệu seed, AI Service và frontend hiện có.

Không được thiết kế lại toàn bộ hệ thống từ đầu.

Phải ưu tiên:

> Sửa đúng kiến trúc hiện tại → sửa tính đúng dữ liệu → sửa Evidence → sửa biểu đồ → tối ưu → kiểm thử.

---

# 2. Mục tiêu cuối cùng

AI Dashboard phải trở thành một công cụ:

> **Read-only Decision Support System**

AI chỉ được:

* Đọc dữ liệu.
* Phân tích dữ liệu.
* Trình bày Fact.
* Giải thích Fact dựa trên Evidence.
* Phát hiện bất thường.
* Đưa Recommendation.
* Gợi ý bước kiểm tra hoặc hành động tiếp theo cho quản lý.

AI **không được thay thế nghiệp vụ backend**.

AI không có quyền tự thay đổi dữ liệu hệ thống.

---

# 3. Kiến trúc bắt buộc phải giữ

Các nguyên tắc sau hiện đang đúng và bắt buộc phải tiếp tục giữ nguyên.

## 3.1. Quyền dữ liệu

AI chỉ được phân tích dữ liệu thuộc những cửa hàng mà người dùng hiện tại được phép truy cập.

Phải tiếp tục tuân thủ:

```text
Current User
→ Permission
→ StaffScope
→ Allowed StoreIds
→ Dashboard Filter
→ Stored Procedure
→ Evidence
→ AI Analysis
```

Không cho phép:

```text
AI
→ tự chọn StoreId ngoài StaffScope
```

---

## 3.2. Nguồn dữ liệu

Stored Procedure và backend là nguồn dữ liệu chính thức.

Luồng chuẩn:

```text
Dashboard Filter
        ↓
Backend
        ↓
Stored Procedure
        ↓
Raw Data
        ↓
EvidenceBuilder
        ↓
Fact / Statistic / Alert / Chart Data
        ↓
Ollama
        ↓
Inference / Recommendation
        ↓
Frontend
```

Không được thay thế Stored Procedure bằng SQL do AI tạo.

---

## 3.3. AI tuyệt đối không sinh SQL tự do

Không được triển khai:

```text
User Question
→ LLM
→ Generate SQL
→ Execute SQL
```

AI chỉ được lựa chọn dữ liệu từ registry/data-plan cố định của backend.

Nếu cần dữ liệu mới thì phải:

1. xác định nghiệp vụ;
2. xác định Stored Procedure hoặc query backend chính thức;
3. đăng ký widget/data source;
4. xây EvidenceBuilder;
5. sau đó AI mới được sử dụng.

---

# 4. Phạm vi AI Dashboard cần hỗ trợ

AI Dashboard phải hỗ trợ các nhóm nghiệp vụ sau.

---

# 4.1. Doanh thu và xu hướng

Phải hỗ trợ:

* Doanh thu theo ngày.
* Doanh thu theo tuần.
* Doanh thu theo tháng.
* So sánh kỳ hiện tại với kỳ trước.
* Ranking cửa hàng.
* Đơn hàng theo ngày.
* Đơn hàng theo giờ.
* Danh mục đóng góp doanh thu.
* Sản phẩm đóng góp doanh thu.

AI có thể đưa ra:

* Revenue trend.
* Tăng/giảm so với kỳ trước.
* Cửa hàng đóng góp nhiều/ít.
* Khoảng thời gian bán tốt/yếu.
* Revenue anomaly.

Nhưng chỉ được nêu tên store/product khi Evidence thực sự có tên thực thể đó.

---

# 4.2. Đơn hàng và thanh toán

Phải hỗ trợ:

* Tổng số đơn.
* Completed order.
* Cancelled order.
* Cancellation rate.
* Order status.
* Orders by hour.
* Orders by day.
* Payment method.
* Day/hour heatmap.

## Quy tắc bắt buộc

Khi nhiều cửa hàng được chọn:

```text
CancellationRate
=
TotalCancelledOrders / TotalOrders
```

Không được:

```text
SUM(StoreCancellationRate)
```

và cũng không được:

```text
AVG(StoreCancellationRate)
```

trừ khi đó thực sự là weighted average theo số đơn.

---

# 4.3. Sản phẩm

Phải hỗ trợ:

* Top Product.
* Product Revenue.
* Product Quantity.
* Category Performance.
* Quantity vs Margin.
* Size Performance.
* Topping Performance.
* BOM status.
* COGS status.
* Product profitability.

Evidence cần có ít nhất khi phù hợp:

```text
ProductId
ProductName
QuantitySold
Revenue
COGS
Margin
MarginPercent
Category
Rank
DataStatus
```

AI không được kết luận sản phẩm nào tốt/xấu nếu Evidence chỉ chứa tổng số sản phẩm hoặc số dòng dữ liệu.

---

# 4.4. Kho và Reorder

Phải hỗ trợ:

* Low stock.
* Available stock.
* Minimum stock.
* Reserved quantity.
* Inventory movement.
* Consumption trend.
* Waste.
* Reorder suggestion.
* Stock-out risk.

Evidence phải ưu tiên cung cấp:

```text
IngredientId
IngredientCode
IngredientName

StoreId
StoreName

OnHandQuantity
ReservedQuantity
AvailableQuantity

MinimumStock
ShortageQuantity

SuggestedReorderQuantity

Unit

Priority
RiskLevel

EvidenceId
```

AI phải có khả năng trả lời:

* Nguyên liệu nào đang thiếu?
* Nguyên liệu nào cần đặt trước?
* Cửa hàng nào có nguy cơ thiếu hàng?
* Mức thiếu là bao nhiêu?
* Đề xuất đặt bao nhiêu?

Nếu Evidence không có tên Ingredient thì AI không được tự suy đoán tên nguyên liệu.

---

# 4.5. Nhà cung cấp và mua hàng

Phải hỗ trợ:

* Supplier quality.
* Rejection rate.
* Purchase price trend.
* Purchase spend.
* Supplier incidents.
* PO overdue.
* PO pipeline.

Evidence cần chứa khi phù hợp:

```text
SupplierId
SupplierName

ReceivedQuantity
RejectedQuantity
RejectionRate

PurchasePrice
PreviousPrice
PriceChangePercent

PurchaseSpend

IssueCount
IssueType

OverduePOCount

LeadTimeDays

DataStatus
```

## Weighted Supplier Rejection Rate

Khi tổng hợp nhiều dòng:

```text
SupplierRejectionRate
=
SUM(RejectedQuantity)
/
SUM(ReceivedQuantity)
```

Không được cộng các phần trăm riêng lẻ.

Không được biến:

* Purchase Price.
* Purchase Spend.
* Supplier Issue.
* Rejection Rate.

thành `COUNT` chỉ vì dataset có nhiều row.

---

# 4.6. Tổng quan điều hành

AI cần hỗ trợ các câu hỏi như:

> Tôi nên chú ý điều gì trong kỳ này?

AI phải tổng hợp:

* Revenue.
* Store ranking.
* Product performance.
* Inventory.
* Supplier.
* Purchase Order.
* WorkShift.
* Operational alerts.

Kết quả phải ưu tiên vấn đề quan trọng nhất.

Mỗi issue cần cố gắng có:

```text
Priority
Issue
AffectedEntity
Evidence
BusinessImpact
RecommendedAction
```

---

# 5. Refactor EvidenceBuilder

Đây là phần quan trọng nhất.

Không được dùng một EvidenceBuilder chung theo kiểu:

```text
Rows.Count
SUM(numericColumn)
```

cho tất cả widget.

Phải xây cơ chế EvidenceBuilder theo từng widget hoặc từng loại dữ liệu.

Ví dụ:

```text
RevenueTrendEvidenceBuilder

StoreRankingEvidenceBuilder

CancellationEvidenceBuilder

TopProductEvidenceBuilder

InventoryRiskEvidenceBuilder

ReorderEvidenceBuilder

SupplierQualityEvidenceBuilder

PurchasePriceEvidenceBuilder

OperationalAlertEvidenceBuilder
```

Có thể dùng Strategy/Registry/Handler nếu phù hợp kiến trúc hiện tại.

Không cần tạo class riêng một cách máy móc nếu có thể thiết kế registry rõ ràng hơn.

Mục tiêu quan trọng là:

> Mỗi Widget phải biết dữ liệu nào là Metric, Dimension, Unit, Baseline, Entity và Sample Size.

---

# 6. Cấu trúc Evidence chuẩn

Hãy chuẩn hóa Evidence theo hướng có thể bao gồm:

```text
EvidenceId

WidgetKey
SectionKey

Title
Description

MetricName
MetricValue
Unit

PreviousValue
Delta
DeltaPercent

SampleSize

DataStatus

EntityType
EntityId
EntityCode
EntityName

StoreId
StoreName

Baseline

Priority
RiskLevel

Metadata
```

Không bắt buộc mọi Evidence phải có toàn bộ field.

Nhưng phải đủ dữ liệu để AI hiểu đúng ý nghĩa nghiệp vụ.

---

# 7. Entity-Level Evidence

AI hiện thiếu tên đối tượng cụ thể.

Phải bổ sung Top-N hoặc Bottom-N Evidence cho:

* Store.
* Product.
* Ingredient.
* Supplier.
* Category.
* Payment Method.
* Alert.

Ví dụ thay vì:

```text
Có 5 nguyên liệu dưới ngưỡng.
```

Evidence nên có:

```text
1. Sữa tươi
Available: 12 lít
Minimum: 20 lít
Shortage: 8 lít
Risk: High

2. Trân châu đường đen
Available: 4 kg
Minimum: 10 kg
Shortage: 6 kg
Risk: Medium
```

---

# 8. Quy tắc chống Hallucination

Phải ép AI tuân thủ:

```text
Không được nêu tên Store/Product/Ingredient/Supplier
nếu tên đó không tồn tại trong Evidence.
```

Recommendation cũng phải tham chiếu EvidenceId.

Ví dụ:

```text
EvidenceId: INV_LOW_STOCK_001
```

Recommendation:

```text
Ưu tiên kiểm tra kế hoạch nhập Sữa tươi tại CafeChain Dĩ An.
Evidence: INV_LOW_STOCK_001
```

Không được tạo Recommendation không có căn cứ.

---

# 9. Phân biệt Fact, Statistic, Inference và Recommendation

Chuẩn hóa rõ:

## Fact

Dữ liệu backend xác định chắc chắn.

Ví dụ:

```text
Doanh thu kỳ này: 125.000.000 VND
```

---

## Statistic

Chỉ số tổng hợp có ý nghĩa thống kê/nghiệp vụ.

Ví dụ:

```text
Cancellation Rate: 4,3%
Average Order Value: 78.000 VND
Revenue Change: -12,5%
```

Không dùng Statistics như bản sao của Fact.

Nếu Statistics hiện tại không có giá trị riêng thì:

* hoặc thiết kế lại;
* hoặc xóa khỏi response contract nếu không còn sử dụng.

Không giữ code thừa chỉ để tương thích nếu không cần thiết.

---

## Inference

Giải thích của AI.

Ví dụ:

```text
Doanh thu giảm có thể liên quan đến lượng đơn trong khung 14h–17h thấp hơn kỳ trước.
```

Phải dùng cách diễn đạt thận trọng như:

* có thể;
* có dấu hiệu;
* dữ liệu cho thấy;
* cần kiểm tra thêm.

---

## Anomaly

Bất thường từ:

* Backend rule.
* Operational Alert.
* Statistical comparison đáng tin cậy.

---

## Recommendation

Đề xuất hành động.

Recommendation không phải lệnh tự động thực thi.

---

# 10. Operational Alerts → Anomaly

Các Operational Alert sau phải được đưa vào Anomaly khi phù hợp:

* Low Stock.
* Cash Discrepancy.
* Overdue PO.
* Supplier Issue.
* Inventory anomaly.
* COGS partial nếu ảnh hưởng phân tích lợi nhuận.

Không được xảy ra trường hợp:

```text
OperationalAlerts = có LowStock nghiêm trọng

nhưng

Anomalies = []
```

và AI kết luận:

> Không có bất thường.

---

# 11. Chuẩn hóa Title

Mỗi widget phải có title riêng.

Không để title fallback sai nghiệp vụ.

Ví dụ:

```text
Revenue Trend
→ Xu hướng doanh thu

Store Ranking
→ Xếp hạng cửa hàng

Cancellation Rate
→ Tỷ lệ hủy đơn

Low Stock
→ Nguyên liệu dưới ngưỡng tồn

Supplier Quality
→ Chất lượng nhà cung cấp

Purchase Price Trend
→ Xu hướng giá mua
```

Không được để widget kho hoặc supplier mang title liên quan tới WorkShift.

---

# 12. Chuẩn hóa Unit

Phải thiết kế Unit/Formatter rõ ràng.

Ví dụ:

```text
VND
COUNT
PERCENT
ORDER
PRODUCT
INGREDIENT
DAY
HOUR
KG
GRAM
LITER
ML
```

Frontend không được đoán đơn vị từ tên field một cách tùy tiện nếu backend đã biết Unit.

## VND

Dùng formatter theo `vi-VN`.

Ví dụ:

```text
125.000.000 ₫
```

hoặc format VND hiện tại của dự án nếu đã thống nhất.

---

# 13. DataStatus

Phải chuẩn hóa:

```text
Complete
Partial
Insufficient
```

Không được xác định Complete chỉ vì Stored Procedure trả về ít nhất một row.

Phải xét:

* Dataset có thực sự có dữ liệu không.
* Row có `NO_DATA` không.
* COGS có Partial không.
* Có widget lỗi không.
* Có missing baseline không.
* Có empty bucket không.
* Planned data có bị nhầm với actual data không.

---

# 14. Quy tắc tổng hợp DataStatus

Ví dụ:

```text
Nếu tất cả widget Complete
→ Complete

Nếu có ít nhất một widget Partial
→ Partial

Nếu widget quan trọng không có đủ dữ liệu
→ Partial hoặc Insufficient

Nếu không có evidence đủ để trả câu hỏi
→ Insufficient
```

Không được để Confidence cao nếu DataStatus là Partial hoặc Insufficient mà không có lý do phù hợp.

---

# 15. Confidence

Confidence phải phản ánh chất lượng Evidence.

Confidence nên giảm khi:

* Sample size thấp.
* Missing baseline.
* COGS partial.
* Widget failed.
* DataStatus Partial.
* Không có entity-level evidence.
* AI phải inference gián tiếp.

Không được để AI tự chọn Confidence tùy ý mà không có constraint/backend validation.

---

# 16. Filter là nguồn phạm vi chính thức

Dashboard filter gồm:

```text
FromDate
ToDate
SelectedStoreIds
```

phải là phạm vi chính thức của phân tích.

Nếu người dùng hỏi:

> Phân tích doanh thu Store 3 tháng trước

nhưng filter hiện tại đang chọn:

```text
Store 1
01/07 → 26/07
```

thì phải xử lý rõ ràng.

Không âm thầm bỏ filter.

Có thể:

* ưu tiên Dashboard Filter;
* và hiển thị warning:

```text
Phân tích sử dụng phạm vi Dashboard hiện tại.
Phần ngày/cửa hàng trong câu hỏi không thay đổi bộ lọc.
```

Nếu hệ thống hiện tại đã chọn policy khác thì giữ policy, nhưng bắt buộc hiển thị rõ phạm vi được sử dụng.

---

# 17. Chống Race Condition khi đổi Filter

Khi người dùng đang chạy AI Analysis rồi đổi filter:

```text
Filter A
→ AI Request A

User Apply Filter B

→ Abort Request A

Filter B
→ AI Request B
```

Response A không được render sau khi filter đã đổi sang B.

Hãy sử dụng:

* AbortController hoặc cơ chế tương đương.
* Filter fingerprint.
* Analysis request fingerprint.

Ví dụ:

```text
Fingerprint =
FromDate
+ ToDate
+ SortedStoreIds
```

Chỉ render nếu fingerprint response khớp fingerprint hiện tại.

---

# 18. Tối ưu Stored Procedure Calls

Hiện tại có khả năng một section bị tải nhiều lần cho từng widget.

Hãy refactor DataPlan.

Sai:

```text
Widget A
→ Load Sales Section

Widget B
→ Load Sales Section

Widget C
→ Load Sales Section
```

Đúng:

```text
Analysis Plan
      ↓
Determine Sections

Sales
Inventory
Supplier
Products

      ↓

Load each Section ONCE
      ↓
Cache in Analysis Context
      ↓
Widgets reuse dataset
```

Nếu cần baseline:

```text
Current Period
→ mỗi Section tối đa một lần

Previous Period
→ mỗi Section tối đa một lần
```

trong cùng một Analysis Request.

Không nhất thiết cache cross-request nếu chưa cần.

---

# 19. Chart Rendering

Hiện backend có chart type nhưng frontend đang render toàn bộ thành table.

Phải sửa.

AI Dashboard cần render thật các loại chart:

```text
Line
Bar
HorizontalBar
Donut
Heatmap
Scatter
Table
```

Không cho AI sinh JavaScript chart.

Chart type phải do:

```text
Widget Registry
hoặc
Backend
```

quyết định.

---

# 20. Mapping biểu đồ đề xuất

## Revenue Trend

```text
Line Chart
```

X:

```text
Date
```

Y:

```text
Revenue
```

---

## Orders by Hour

```text
Line hoặc Bar
```

---

## Store Ranking

```text
Horizontal Bar
```

---

## Top Product

```text
Horizontal Bar
```

---

## Payment Method

```text
Donut
```

hoặc Bar khi dữ liệu không phù hợp Donut.

---

## Day × Hour Order Distribution

```text
Heatmap
```

---

## Quantity vs Margin

```text
Scatter
```

X:

```text
Quantity
```

Y:

```text
Margin
```

---

## Purchase Price Trend

```text
Line
```

---

## Waste

```text
Bar
```

---

## Staffing Demand

```text
Line
```

hoặc Grouped Bar.

---

## PO Pipeline

```text
Stacked Bar
```

hoặc Donut.

---

# 21. Chart Fallback

Nếu dữ liệu không phù hợp chart:

```text
Rows < minimum
hoặc
Missing X/Y
hoặc
All values null
```

thì frontend fallback:

```text
Table
```

Không crash JavaScript.

---

# 22. Tooltip và Formatter

Tooltip phải sử dụng Unit.

Ví dụ:

Revenue:

```text
125.000.000 ₫
```

Cancellation:

```text
4,2%
```

Orders:

```text
125 đơn
```

Ingredient:

```text
12,5 kg
```

Không hiển thị raw decimal khó đọc.

---

# 23. Việt hóa dữ liệu hiển thị

Các field kỹ thuật:

```text
totalRevenue
cancelledCount
availableQty
supplierName
```

không nên xuất trực tiếp cho người dùng.

Phải map sang label tiếng Việt.

Ví dụ:

```text
totalRevenue
→ Doanh thu

cancelledCount
→ Đơn hủy

availableQty
→ Tồn khả dụng

supplierName
→ Nhà cung cấp
```

Có thể dùng metadata/registry.

Không hard-code map rải rác ở nhiều file.

---

# 24. AI Response phải trình bày dễ hiểu

AI Analysis không được trả một đoạn văn dài duy nhất.

Nên chia:

## Tóm tắt

2–5 câu.

## Số liệu chính

Fact/Statistic quan trọng.

## Phân tích

Giải thích từng nhóm số liệu.

## Bất thường

Anomaly.

## Khuyến nghị

Recommendation.

## Biểu đồ/dữ liệu minh họa

Charts.

## Cảnh báo dữ liệu

Partial/Insufficient/Fallback.

---

# 25. Không để AI phân tích quá ngắn

Hiện AI có thể trả phân tích ngắn, khó hiểu.

Hãy cải thiện prompt/schema để AI:

* Giải thích nguyên nhân dựa trên dữ liệu.
* Tách từng luận điểm.
* Nêu Evidence liên quan.
* So sánh current vs baseline nếu có.
* Giải thích chart khi phù hợp.
* Nêu rõ giới hạn dữ liệu.

Nhưng không được kéo dài bằng nội dung chung chung.

Ưu tiên:

```text
Fact
→ Evidence
→ Interpretation
→ Business Impact
→ Recommended Check
```

---

# 26. Ollama Fallback

Khi Ollama:

* Không chạy.
* Timeout.
* Invalid JSON.
* Sai schema.
* Hallucination EvidenceId.

backend vẫn phải trả:

```text
AIStatus = Fallback
```

và vẫn cung cấp:

* Fact.
* Statistics nếu có.
* Backend Anomaly.
* Chart.
* Warning.
* DataStatus.

Không được để toàn bộ AI Dashboard lỗi chỉ vì Ollama không chạy.

---

# 27. Fallback Parser

Mở rộng parser cho câu hỏi tiếng Việt phổ biến.

Ví dụ:

```text
doanh thu
bán hàng
đơn hàng
đơn hủy
thanh toán
sản phẩm
món bán chạy
món bán chậm
kho
nguyên liệu
thiếu hàng
đặt hàng
nhập hàng
nhà cung cấp
PO
mua hàng
ca làm
nhân sự
tổng quan
bất thường
cảnh báo
```

Nhưng fallback parser không được biến thành NLP engine quá phức tạp.

---

# 28. Feature Flag

Phải có khả năng bật/tắt AI theo môi trường.

Ví dụ concept:

```text
AI:
  Enabled: true
```

Development có thể bật.

Production/UAT tùy cấu hình.

Không hard-code.

---

# 29. Logging

Bổ sung logging phù hợp nhưng không log dữ liệu nhạy cảm.

Nên log:

```text
AnalysisId
UserId/StaffId nếu policy cho phép
Store scope
FromDate
ToDate
Intent
Selected sections
AIStatus
DataStatus
Execution time
Fallback reason
Widget failure
```

Không log toàn bộ prompt/evidence chứa dữ liệu nhạy cảm nếu không cần.

---

# 30. AnalysisId

Mỗi lần chạy Analysis nên có:

```text
AnalysisId
```

để trace:

```text
Request
→ Stored Procedures
→ Evidence
→ Ollama
→ Response
```

Phục vụ debug và audit.

---

# 31. Evidence Source Viewer – P2

Sau khi phần chính ổn định có thể cho người dùng:

```text
Xem nguồn dữ liệu
```

Ví dụ:

```text
Khuyến nghị:
Kiểm tra tồn Sữa tươi tại Dĩ An.

Evidence:
Available = 12L
Minimum = 20L
Shortage = 8L
```

Không cần hiển thị raw SQL.

---

# 32. Recommendation Priority

Recommendation nên có:

```text
Priority:
Critical
High
Medium
Low
```

Có thể kèm:

```text
VerifyCondition
```

Ví dụ:

```text
Priority: High

Recommendation:
Kiểm tra kế hoạch bổ sung Sữa tươi.

VerifyCondition:
Xác nhận tồn thực tế và PO đang mở trước khi tạo yêu cầu mua.
```

AI chỉ đề xuất.

Không tự thao tác.

---

# 33. Seed Data

Phải inspect SeedAll hiện tại.

Giữ hai nhóm scenario.

---

## Scenario A – Normal

Rolling theo ngày hiện tại.

Bao gồm:

* Revenue ổn định.
* Completed orders.
* Cancellation thấp.
* Không cash discrepancy nghiêm trọng.
* Stock đủ.
* Supplier bình thường.
* COGS Complete.
* Nhiều payment method hợp lý.
* POS/BOM/FIFO đủ.

---

## Scenario B – Anomaly

Cũng phải nằm trong rolling window gần ngày hiện tại.

Phải có tối thiểu:

* Một Store giảm doanh thu rõ rệt.
* Cancelled order.
* Refund.
* Cash discrepancy.
* Một hoặc nhiều Overdue PO.
* Supplier issue.
* Ingredient dưới Minimum Stock.
* Một Product margin thấp.
* COGS Partial.
* Nhiều Payment Method.
* Inventory anomaly phù hợp.

Mục đích:

Dashboard mặc định vẫn có thể demo được anomaly.

---

# 34. Không phá seed hiện tại

Không xóa các scenario cố định tháng 01/2026 nếu chúng còn dùng để test exception.

Chỉ bổ sung rolling anomaly fixture nếu cần.

Seed phải:

* Idempotent.
* Không double insert.
* Không double stock deduction.
* Không làm hỏng POS/BOM/FIFO chain hiện tại.

---

# 35. Test Backend bắt buộc

Bổ sung hoặc cập nhật test cho:

## Permission

* User không có quyền → không được Analyze.
* Store ngoài StaffScope → không được truy cập.

## Cancellation

Kiểm tra weighted cancellation rate.

## Supplier

Kiểm tra weighted rejection rate.

## Evidence

* Product evidence có ProductName.
* Inventory evidence có IngredientName.
* Supplier evidence có SupplierName.
* Store evidence có StoreName.

## Hallucination

AI output chứa EvidenceId không tồn tại phải bị reject/sanitize.

## DataStatus

Test:

```text
Complete
Partial
Insufficient
```

## Fallback

Ollama unavailable vẫn có backend response.

---

# 36. Test Frontend bắt buộc

Test hoặc kiểm tra rõ:

* Line render.
* Bar render.
* Donut render.
* Heatmap render.
* Scatter render.
* Table fallback.
* Tooltip.
* Unit.
* Empty state.
* Partial warning.
* Insufficient warning.
* Ollama fallback.
* Abort previous request.
* Fingerprint mismatch không render response cũ.

---

# 37. P0 – Bắt buộc hoàn thành trước nghiệm thu

Ưu tiên cao nhất.

Thực hiện đầy đủ:

1. Render chart thật:

   * Line.
   * Bar.
   * Donut.
   * Heatmap.
   * Scatter.

2. EvidenceBuilder theo từng Widget.

3. Chuẩn hóa:

   * Title.
   * Unit.
   * Formatter.

4. Sửa:

   * Weighted Cancellation Rate.
   * Weighted Supplier Rejection Rate.

5. DataStatus phải phản ánh đúng chất lượng dữ liệu.

6. Operational Alert phải được chuyển thành Anomaly phù hợp.

7. Truyền Top-N entity-level Evidence cho AI.

8. Không cho AI nêu entity không tồn tại trong Evidence.

---

# 38. P1 – Hoàn thành trước UAT chính thức

Sau P0 mới thực hiện:

1. Group Stored Procedure calls theo Section.
2. Abort AI request cũ khi đổi filter.
3. Filter fingerprint.
4. Mở rộng Vietnamese fallback parser.
5. Việt hóa field/formatter.
6. Thiết kế lại hoặc xóa Statistics.
7. AI Feature Flag.
8. Backend test.
9. Frontend test.
10. Rolling anomaly seed.
11. Hiển thị rõ filter override.

---

# 39. P2 – Sau khi hệ thống ổn định

Không ưu tiên trước P0/P1.

Bao gồm:

* Evidence Source Viewer.
* Analysis Audit Log.
* Performance telemetry từng Section.
* Fallback reason.
* Recommendation Priority.
* Recommendation VerifyCondition.

---

# 40. Những thứ KHÔNG ĐƯỢC mở rộng

Không triển khai:

* AI tự tạo PO.
* AI tự duyệt PO.
* AI tự đặt hàng.
* AI tự trừ/cộng kho.
* AI tự điều chỉnh tồn.
* AI tự sửa giá bán.
* AI tự tạo discount.
* AI tự lập lịch nhân sự hoàn chỉnh.
* AI tự thay đổi WorkShift.
* AI tự chạy SQL.
* AI tự sinh Stored Procedure.
* AI tự đánh giá/kỷ luật nhân viên.
* AI tự thực thi Recommendation.
* Forecasting dài hạn khi chưa đủ historical data.
* AI accounting thay thế báo cáo kế toán.

AI chỉ:

```text
READ
ANALYZE
EXPLAIN
ALERT
RECOMMEND
```

---

# 41. Không ưu tiên đổi Model AI

Không được giải quyết vấn đề bằng cách đổi sang model Ollama lớn hơn trước.

Thứ tự bắt buộc:

```text
Correct Data
↓
Correct Aggregation
↓
Correct Evidence
↓
Correct Entity
↓
Correct Unit
↓
Correct DataStatus
↓
Correct Chart
↓
Correct Prompt
↓
sau đó mới đánh giá Model
```

Model lớn hơn không sửa được Evidence sai.

---

# 42. Quy tắc khi refactor mã nguồn

Tuân thủ kiến trúc CafeChain hiện tại.

## Controller

Controller chỉ điều phối request.

Không đưa logic phân tích Dashboard lớn vào Controller.

Không dùng trực tiếp DbContext nếu kiến trúc dự án đang yêu cầu thông qua Service/Repository.

---

## Service

Business orchestration nằm trong Service.

Service dùng Repository hoặc Data Access abstraction hiện có.

Không tạo dependency vòng.

---

## Repository/Data layer

Stored Procedure execution nằm tại data layer phù hợp.

Không SaveChanges nhiều lần không cần thiết.

AI Dashboard phần read-only không được phát sinh thay đổi DB ngoài logging nếu thực sự được thiết kế.

---

# 43. Không tự ý tạo file

Trước khi sửa:

1. Inspect project.
2. Tìm file hiện có.
3. Tìm Service/Repository/Controller/DTO/VM/JS/CSS liên quan.
4. Tìm Dashboard widget registry.
5. Tìm AI request/response model.
6. Tìm Stored Procedure execution.
7. Tìm Ollama client.
8. Tìm frontend chart library hiện tại.

Ưu tiên refactor file đang tồn tại.

Chỉ tạo file mới khi thật sự cần thiết và phải giải thích lý do.

Không tự bịa tên file.

---

# 44. Không thay chart library nếu không cần

Nếu dự án đã có:

```text
Chart.js
ApexCharts
ECharts
Highcharts
```

hoặc thư viện tương đương thì ưu tiên tái sử dụng.

Không cài thêm library chỉ để render AI chart nếu library hiện tại đủ khả năng.

---

# 45. Không sửa ngoài AI Dashboard

Phạm vi chính của task là:

```text
AI Dashboard
Dashboard analytics integration
Evidence
AI analysis
Charts
Stored Procedure consumption
Relevant seed/test
```

Không refactor module không liên quan chỉ vì phát hiện code chưa đẹp.

Nếu phát hiện lỗi ngoài scope:

```text
ghi nhận
nhưng không sửa
```

trừ khi lỗi đó trực tiếp chặn AI Dashboard hoạt động.

---

# 46. Cách làm việc bắt buộc

Không sửa code ngay khi chưa inspect.

Thực hiện theo thứ tự:

## Phase 1 – Inspect

Đọc:

* Controller.
* Service.
* Repository.
* DTO.
* ViewModel.
* View.
* JavaScript.
* CSS liên quan.
* AI Service.
* Ollama client.
* Widget registry.
* EvidenceBuilder.
* Dashboard stored procedures.
* Seed.
* Test.

---

## Phase 2 – Current-State Analysis

Lập bảng:

```text
Feature
Current State
Problem
Business Risk
Files Involved
Priority
Proposed Fix
```

Phân loại:

```text
P0
P1
P2
```

---

## Phase 3 – Data correctness

Ưu tiên sửa:

```text
Weighted Rate
Evidence
Entity
Unit
Title
DataStatus
Alert
```

---

## Phase 4 – Frontend

Sau khi dữ liệu đúng mới sửa:

```text
Chart
Formatter
Vietnamese labels
Abort request
Filter fingerprint
```

---

## Phase 5 – Performance

Sau đó mới:

```text
Group Section Query
Baseline reuse
Telemetry
```

---

## Phase 6 – Test

Chạy test nếu môi trường cho phép.

Không được tuyên bố test PASS nếu chưa chạy.

---

# 47. Output tôi yêu cầu từ bạn

Sau khi hoàn thành hãy báo cáo rõ.

## 47.1. Current-State Findings

Liệt kê các lỗi thực sự tìm thấy trong code.

Không chỉ lặp lại prompt.

---

## 47.2. Files Changed

Ví dụ:

```text
File:
CafeChain/Services/AI/DashboardAiService.cs

Changed:
- Refactor EvidenceBuilder.
- Group section queries.
- Validate entity evidence.
```

---

## 47.3. Methods Changed

Ghi rõ:

```text
BuildEvidenceAsync()
BuildWidgetEvidence()
CalculateCancellationRate()
CalculateSupplierRejectionRate()
...
```

Chỉ ghi method thực sự tồn tại sau khi inspect.

---

## 47.4. Database/Stored Procedure Changes

Nếu sửa SQL:

Ghi rõ:

```text
Stored Procedure
Reason
Before
After
```

Không thay SP nếu backend có thể tính đúng mà không cần thay.

---

## 47.5. Frontend Changes

Ghi:

```text
Chart type
Render function
Formatter
Abort mechanism
Fingerprint mechanism
Fallback
```

---

## 47.6. Seed Changes

Ghi rõ scenario nào thêm:

```text
NORMAL
ANOMALY
```

và anomaly nào có thể test.

---

## 47.7. Tests

Phân biệt:

```text
Tests added
Tests executed
Tests passed
Tests failed
Tests not executed
```

Không được đánh đồng việc viết test với test đã chạy thành công.

---

# 48. Acceptance Criteria

Task chỉ được xem là hoàn thành khi các tiêu chí P0 sau đạt.

## Security

* Không query ngoài StaffScope.
* Không bypass permission.
* Không cho AI sinh SQL.

## Fact

* Fact lấy từ backend.
* Không cho AI tự sinh số.

## Entity

Câu hỏi về:

```text
Store
Product
Ingredient
Supplier
```

phải có entity thực sự trong Evidence.

Không đủ Evidence phải trả:

```text
Không đủ dữ liệu để xác định.
```

---

## Rates

Cancellation:

```text
SUM(Cancelled) / SUM(Total)
```

Supplier rejection:

```text
SUM(Rejected) / SUM(Received)
```

---

## Units

* Revenue → VND.
* Ratio → PERCENT.
* Order → COUNT/ORDER.
* Ingredient → Unit thực tế.
* Không dùng COUNT cho monetary metric.

---

## DataStatus

* Empty → Insufficient.
* Partial COGS → Partial.
* Widget failure → Partial.
* Missing critical data → Partial/Insufficient.
* Có row không đồng nghĩa Complete.

---

## Anomaly

Nếu có:

```text
Low Stock
Cash Discrepancy
Overdue PO
Supplier Issue
```

thì AI không được kết luận:

```text
Không có bất thường.
```

---

## Charts

Phải render thật:

```text
Line
Bar
Donut
Heatmap
Scatter
```

Không chỉ hiện table.

Không đủ data:

```text
fallback Table
```

---

## Filter

Kết quả AI phải hiển thị:

* Date range.
* Store scope.

Đổi filter:

```text
old request aborted/ignored
```

---

## Fallback

Ollama unavailable:

```text
Backend facts remain available
Charts remain available
AIStatus = Fallback
```

---

# 49. Kết luận nghiệp vụ bắt buộc giữ

Hướng AI Dashboard hiện tại của CafeChain là đúng.

Không cần redesign thành AI Agent có quyền thao tác hệ thống.

Mục tiêu là hoàn thiện:

```text
Trusted Data
+
Trusted Evidence
+
Controlled AI Explanation
+
Useful Visualization
+
Business Recommendations
```

AI Dashboard phải đóng vai trò:

> Trợ lý phân tích dữ liệu cho quản lý CafeChain, dựa trên dữ liệu backend đã kiểm chứng, có Evidence, có giới hạn quyền, có cảnh báo dữ liệu và không tự thực hiện nghiệp vụ.

---

# 50. Yêu cầu cuối cùng

Hãy bắt đầu bằng việc inspect toàn bộ luồng AI Dashboard hiện tại.

Không sửa mù.

Không giả định tên class, method hoặc file.

Không thay đổi module ngoài phạm vi.

Không đổi Ollama model trước khi sửa Evidence/Data.

Không tuyên bố hoàn thành nếu P0 chưa đạt.

Nếu một yêu cầu trong prompt đã được code hiện tại đáp ứng đúng thì:

```text
KEEP
```

không refactor chỉ để thay đổi style.

Nếu phát hiện giải pháp hiện tại tốt hơn đề xuất trong prompt nhưng vẫn đáp ứng đầy đủ nghiệp vụ:

* giữ giải pháp hiện tại;
* giải thích lý do;
* chứng minh Acceptance Criteria vẫn đạt.

Mục tiêu cuối cùng không phải viết lại nhiều code nhất.

Mục tiêu là:

> **Sửa ít nhất có thể nhưng đủ để AI Dashboard đúng dữ liệu, đúng Evidence, đúng biểu đồ, đúng phạm vi quyền và đủ độ tin cậy để quản lý CafeChain sử dụng.**
