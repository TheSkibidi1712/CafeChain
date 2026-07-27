# PHÂN TÍCH NGHIỆP VỤ ỨNG DỤNG AI CHO CAFECHAIN

## 1. Executive Summary

CafeChain chỉ nên sử dụng AI tại những điểm cần hiểu ngôn ngữ tự nhiên, giải thích dữ liệu hoặc tối ưu bài toán có nhiều ràng buộc. Các cảnh báo có điều kiện rõ ràng phải dùng rule hoặc thống kê; dự báo số liệu phải dùng mô hình forecasting, không để LLM tự đoán.

Ba hướng có giá trị thực tế nhất là:

1. Dashboard hỏi đáp bằng tiếng Việt, nhưng AI chỉ chuyển câu hỏi thành một `AnalyticsRequest` có whitelist. Dữ liệu vẫn được lấy qua Dashboard Service, Repository và stored procedure được kiểm soát.
2. Forecast doanh thu, số lượng sản phẩm và nhu cầu nguyên liệu bằng mô hình chuỗi thời gian; LLM chỉ giải thích kết quả.
3. Cảnh báo vận hành cho ca làm việc, kho và doanh thu bằng rule/statistics, tái sử dụng `StaffNotification` hiện có.

Hệ thống hiện đã có Dashboard repository/stored procedure, StoreScope/StaffScope, Ollama cho một số gợi ý master data, Pexels/ComfyUI cho ảnh và `StaffNotification`. Thiết kế mới phải tích hợp với các thành phần này, không tạo một hệ thống song song.

## 2. Nguyên tắc phân loại

| Loại | Sử dụng khi | Không nên dùng khi |
| --- | --- | --- |
| RULE | Điều kiện nghiệp vụ xác định, cần kết quả đúng tuyệt đối | Cần dự đoán hoặc hiểu văn bản tự do |
| STATISTICS | So sánh, xu hướng, ngưỡng bất thường, KPI | Dữ liệu quá ít hoặc quy tắc đã đủ |
| FORECASTING / ML | Dự báo giá trị tương lai từ lịch sử | Không có lịch sử đủ dài hoặc dữ liệu sai lệch |
| LLM / AI | Hiểu câu hỏi, sinh nội dung, giải thích kết quả | Tính tồn kho, tiền, quyền hoặc số forecast |
| HYBRID | Kết hợp rule/statistics/model với giải thích ngôn ngữ | Có thể giải quyết đầy đủ bằng một rule đơn giản |

## 3. AI Use Case Matrix

| Module | Use case | Type | Priority | Complexity | Business value |
| --- | --- | --- | --- | --- | --- |
| Dashboard | Hỏi dữ liệu bằng tiếng Việt | HYBRID: LLM + Analytics | High | Medium | High |
| Dashboard | Doanh thu hôm nay thấp bất thường | STATISTICS | High | Low | High |
| Dashboard | Giải thích KPI thay đổi | HYBRID | Medium | Medium | High |
| Dashboard | Forecast doanh thu 7/30 ngày | FORECASTING / ML | High | Medium | High |
| Sales | Best seller/slow seller | STATISTICS | High | Low | High |
| Sales | Cross-sell/combo recommendation | STATISTICS / ML | Medium | Medium | Medium |
| Inventory | Tồn dưới minimum | RULE | High | Low | High |
| Inventory | Dự báo ngày hết hàng | FORECASTING + RULE | High | Medium | High |
| Inventory | Waste/kiểm kê chênh lệch bất thường | STATISTICS | High | Medium | High |
| Purchase | Gợi ý lượng cần mua | FORECASTING + RULE | High | Medium | High |
| Supplier | So sánh supplier theo tổng chi phí/rủi ro | RULE + STATISTICS | High | Medium | High |
| Supplier | Giải thích supplier được đề xuất | LLM trên dữ liệu đã tính | Medium | Low | Medium |
| Shift | Nhân viên chưa có lịch | RULE | High | Low | High |
| Shift | Trùng ca, quá giờ, thiếu người | RULE | High | Medium | High |
| Shift | Gợi ý lịch theo nhu cầu dự kiến | HYBRID: Forecast + Optimization | Medium | High | Medium |
| Drink/Menu | Gợi ý tên, mô tả, image prompt | LLM / AI | Medium | Low | Medium |
| Drink/Menu | Gợi ý giá bán theo cost/margin | RULE + STATISTICS | Medium | Medium | High |

## 4. Dashboard AI Architecture

### 4.1 Luồng an toàn

```text
User Prompt
    ↓
AI Intent Parser (structured JSON only)
    ↓
Validate AnalyticsRequest against whitelist
    ↓
Resolve StaffScope / StoreScope
    ↓
Analytics Service
    ↓
Dashboard Repository / approved Stored Procedure
    ↓
Bounded Dataset
    ↓
Chart Builder
    ↓
Optional LLM Explanation
```

Authorization phải được giải quyết trước khi Repository nhận yêu cầu. Store Manager hỏi dữ liệu toàn chuỗi chỉ được trả dữ liệu trong các Store được phép; không query toàn chuỗi rồi mới lọc kết quả.

### 4.2 Analytics Request DTO đề xuất

```json
{
  "metric": "Revenue",
  "dimensions": ["Store"],
  "period": "Last7Days",
  "comparison": null,
  "filters": [],
  "limit": 20,
  "chart": "Bar"
}
```

Các trường đều là enum/allowlist. V1 chỉ nên hỗ trợ một catalog nhỏ:

- Metric: `Revenue`, `OrderCount`, `AverageOrderValue`, `QuantitySold`, `WasteQuantity`, `StockOnHand`.
- Dimension: `Store`, `Product`, `Day`, `Hour`, `Category`.
- Period: các preset và date range đã được validate.
- Comparison: `PreviousPeriod`, `PreviousWeek`, `PreviousMonth`, `PreviousYear`.
- Chart: `Kpi`, `Line`, `Bar`, `StackedBar`, `Pie`, `Table`.

LLM không được cung cấp `StoreId` cuối cùng. Service phải intersect filter do AI hiểu với scope của người dùng.

### 4.3 Mapping và chart

- Mỗi tổ hợp metric/dimension hợp lệ được map vào handler hoặc stored procedure đã duyệt.
- Không truyền tên table, column, SQL fragment hoặc stored procedure từ output của AI.
- Dataset có giới hạn số dòng, thời gian và kích thước.
- Chart Builder kiểm tra dữ liệu trước khi chọn biểu đồ: time-series dùng Line; so sánh nhóm dùng Bar; một KPI dùng KPI card; bảng dùng khi có nhiều chiều.
- LLM explanation chỉ nhận dataset đã tổng hợp và scoped, không nhận toàn bộ record giao dịch.

### 4.4 Đánh giá AI generate Stored Procedure động

Ưu điểm duy nhất là linh hoạt khi thử nghiệm câu hỏi mới. Tuy nhiên không phù hợp để chạy runtime trên production:

- SQL injection và prompt injection có thể đưa thêm câu lệnh ngoài ý định.
- Quyền DDL/EXECUTE cao có thể sửa hoặc xóa dữ liệu/schema.
- Hallucination tạo sai table, join, điều kiện hoàn tiền hoặc trạng thái đơn hàng.
- Schema drift làm procedure sinh bởi AI nhanh chóng lỗi thời.
- Query không có index phù hợp có thể scan bảng, lock, timeout và ảnh hưởng POS.
- Khó áp dụng StoreScope nhất quán, dễ lộ doanh thu hoặc dữ liệu nhân sự ngoài quyền.
- Không có review, versioning, rollback và performance baseline đáng tin cậy.

**Kết luận:** không cho AI tạo và chạy SQL/stored procedure trong ứng dụng production. AI chỉ có thể draft SQL trong môi trường developer/sandbox; SQL phải được review, test, đưa vào source control và triển khai như migration/script bình thường.

## 5. Forecast Architecture

```text
Completed Sales / Orders / Inventory History
    ↓
Data Quality + Feature Preparation
    ↓
Baseline and Forecast Model
    ↓
Backtest + Error Metrics
    ↓
Forecast Result + Confidence Range
    ↓
Dashboard / Reorder Rules
    ↓
Optional LLM Explanation
```

### 5.1 Dữ liệu đầu vào

- Đơn hàng hoàn tất, dòng sản phẩm, số lượng, doanh thu thực thu và thời gian.
- Store, product/category, thứ trong tuần, giờ và mùa vụ trong dữ liệu.
- Voucher/promotion, giá bán và thay đổi giá nếu lịch sử lưu đủ.
- Stock-out để phân biệt “không có nhu cầu” với “không còn hàng để bán”.
- Công thức sản phẩm để quy đổi product forecast thành nhu cầu nguyên liệu.
- Ngày lễ, sự kiện và thời tiết chỉ thêm khi có nguồn ổn định, lịch sử đủ dài và quyền sử dụng dữ liệu rõ ràng.

Không đưa đơn nháp, đơn hủy hoặc doanh thu chưa hoàn tất vào target. Refund phải được trừ theo cùng quy tắc với Dashboard tài chính.

### 5.2 Phương án cho quy mô nhỏ/vừa

| Phương án | Vai trò | Đánh giá |
| --- | --- | --- |
| Seasonal naive / moving average | Baseline bắt buộc | Dễ giải thích, ít vận hành |
| Exponential smoothing | Forecast v1 | Phù hợp xu hướng và seasonality đơn giản |
| Linear regression | Forecast có feature | Dùng khi promotion, ngày trong tuần và giá có chất lượng |
| ML.NET | Bước nâng cấp trong .NET | Giảm chi phí vận hành service riêng |
| Python model/service | Mô hình nâng cao | Chỉ dùng khi dữ liệu và đội vận hành đủ lớn |
| LLM | Giải thích | Không dùng làm model dự báo số |

V1 nên forecast doanh thu theo Store/ngày cho 7 và 30 ngày; product forecast chỉ áp dụng cho sản phẩm có đủ số ngày bán. Luôn so sánh model với seasonal baseline bằng MAE/WAPE và không triển khai model nếu không tốt hơn baseline ổn định.

## 6. Shift Intelligence

### 6.1 Shift Rule Engine

Các chức năng sau không cần AI:

- Active Staff không có `StaffShift` ngày mai hoặc trong kỳ được chọn.
- Hai ca của cùng nhân viên bị chồng thời gian.
- Ca không đạt minimum staff sau khi có cấu hình định biên.
- Tổng giờ vượt giới hạn được cấu hình.
- Nhân viên được xếp vào Store ngoài phạm vi làm việc.

“Nhân viên chưa có lịch” chỉ là cảnh báo thông tin cho tới khi hệ thống có dữ liệu availability, ngày nghỉ, loại hợp đồng và số giờ cam kết. Không được tự kết luận mọi Active Staff đều bắt buộc làm ngày mai.

### 6.2 AI/Optimization cho ca

AI chỉ có giá trị khi gợi ý lịch dựa trên:

- Nhu cầu nhân sự theo forecast doanh thu/đơn hàng theo giờ.
- Availability, kỹ năng/vai trò, Store được phép làm.
- Giới hạn giờ, nghỉ giữa ca, overtime và tính công bằng.
- Lịch hiện tại và các ca bắt buộc.

Bài toán xếp lịch nên dùng constraint solver/optimization. LLM giải thích “vì sao lịch này được đề xuất”, không tự quyết định lịch và không tự ghi lịch khi chưa được Manager duyệt.

## 7. Inventory Intelligence

| Use case | Input | Output | Công nghệ |
| --- | --- | --- | --- |
| Tồn dưới minimum | StoreInventory, ngưỡng | Danh sách cần chú ý | RULE |
| Dự báo ngày hết hàng | Consumption history, current stock | Days-to-stockout | FORECASTING |
| Reorder suggestion | Forecast, lead time, MOQ, pack conversion | Số lượng đề nghị mua | FORECASTING + RULE |
| Tiêu thụ bất thường | InventoryTransaction theo ngày | Điểm bất thường | STATISTICS |
| Chênh lệch kiểm kê bất thường | System/Actual/Difference | Cảnh báo và nhóm nguyên nhân | STATISTICS |
| Waste tăng đột biến | Phiếu hủy theo Store/ingredient | So sánh baseline | STATISTICS |

Tất cả đề xuất nhập hàng phải tôn trọng unit conversion, package quantity, MOQ, tồn khả dụng, đơn mua đang mở và lead time. AI không được tạo PA/PO tự động; người có quyền phải duyệt.

## 8. Supplier Intelligence

Không chọn supplier chỉ theo giá hiển thị. Điểm đề xuất cần được tính từ dữ liệu có thể kiểm chứng:

- Giá quy đổi về cùng base unit và package quantity.
- MOQ và tổng tiền tối thiểu.
- Lead time trung bình và độ biến động.
- Tỷ lệ giao đúng hạn, giao thiếu, hàng lỗi/từ chối.
- Price history và thời gian hiệu lực offer.
- Supplier/offer/store đang Active.

V1 dùng weighted score có trọng số cấu hình và hiển thị breakdown. LLM chỉ chuyển breakdown thành lời giải thích, ví dụ: “Supplier B có giá base thấp hơn 8% và tỷ lệ đúng hạn cao hơn, dù MOQ lớn hơn”. Không cho LLM tự tạo điểm hoặc bịa dữ liệu thiếu.

## 9. Sales, POS và Menu

- Best seller, slow seller và product mix là thống kê SQL.
- Cross-sell/combo dựa trên các sản phẩm cùng xuất hiện trong order; cần ngưỡng support/confidence tối thiểu và loại bỏ dữ liệu quá ít.
- Upsell có thể dùng rule theo size/topping/margin trước khi dùng recommendation model.
- Giá bán gợi ý dựa trên cost, target margin, VAT/fee và mặt bằng giá nội bộ; LLM chỉ giải thích.
- Giữ nguyên luồng AI tên/mô tả, Pexels và ComfyUI hiện có nếu đang hoạt động; không đưa AI trở lại form InventoryDocument.

## 10. Notification Architecture

Tái sử dụng `StaffNotification` với `StoreId`, `RecipientStaffId`, `Type`, `Title`, `Body`, `EntityType`, `EntityId`, `CreatedAt`, `IsRead`, `ReadAt` và trạng thái email hiện có. Chưa tạo model Notification mới.

Các category đề xuất được biểu diễn bằng `Type` trong phase đầu:

- `SHIFT_ALERT`
- `INVENTORY_ALERT`
- `REVENUE_ALERT`
- `PURCHASE_ALERT`
- `AI_INSIGHT`

Chống spam:

- Dedupe theo Recipient + Store + Type + Entity + time bucket.
- Cooldown cho cùng một insight; cập nhật notification đang mở thay vì tạo lại.
- Chỉ gửi khi vượt ngưỡng có ý nghĩa và tự resolve khi điều kiện hết.
- Gom cảnh báo mức thấp thành daily digest; cảnh báo mức cao gửi ngay.
- Cho phép cấu hình kênh và category theo vai trò.
- Không gửi lại cùng danh sách “chưa có lịch ngày mai” sau mỗi lần scheduler chạy.

`Severity` nên được bổ sung trong phase triển khai notification nếu `Type` không đủ biểu đạt, không thêm schema trong giai đoạn phân tích này.

## 11. Security và Governance

- Xác thực và authorization trước khi query; AI không được bypass StaffScope/StoreScope.
- Intent DTO phải validate enum, khoảng ngày, limit, dimension và filter.
- Repository chỉ gọi query/stored procedure trong catalog được duyệt.
- Analytics connection nên read-only, không có DDL/DML và không truy cập bảng nhạy cảm ngoài nhu cầu.
- Timeout, row limit, rate limit và cancellation token bắt buộc.
- Không đưa PII nhân viên/khách hàng vào prompt nếu chỉ cần dữ liệu tổng hợp.
- Lưu audit: người hỏi, intent đã parse, scope, metric handler, thời gian, số dòng và lỗi; không nhất thiết lưu nguyên prompt nếu chứa dữ liệu nhạy cảm.
- Structured output phải reject unknown fields/value; không “cố hiểu” output sai schema.
- Prompt injection trong dữ liệu hoặc câu hỏi không được thay đổi quyền, catalog hoặc system instruction.
- Insight phải kèm khoảng thời gian, nguồn KPI và mức tin cậy; không trình bày forecast như số chắc chắn.
- Không cho AI tự tạo PO, lịch, điều chỉnh tồn hoặc thay đổi giá nếu chưa có bước duyệt nghiệp vụ.

## 12. Implementation Priority

### Phase 1 – Inventory Reorder Intelligence (đã có nền tảng)

1. Rule tính `UsableStock`, `ProjectedStock`, reorder point và số lượng theo package/MOQ.
2. Không gợi ý trùng khi RestockRequest/PA đang xử lý; chỉ PO còn lại được tính là hàng đang về.
3. `StaffNotification` có dedupe, severity, resolve và lọc Recipient + StoreScope.
4. Skill `inventory-reorder-explanation` chỉ giải thích kết quả rule khi người dùng yêu cầu.
5. Ollama lỗi, timeout hoặc sai schema luôn fallback về giải thích deterministic.

### Phase 2 – Dashboard Intelligence

1. Rule/statistics insight cho doanh thu, waste, stock và ca làm việc.
2. Natural-language analytics qua intent DTO và metric/dimension/filter whitelist.
3. StoreScope được áp dụng trước khi gọi Dashboard Service/Repository hiện có.
4. AI chỉ giải thích dataset; không sinh hoặc thực thi SQL/stored procedure production.

### Phase 3 – Forecast và Supplier Intelligence

1. Baseline, moving average hoặc exponential smoothing cho doanh thu Store 7/30 ngày.
2. Forecast sản phẩm khi đủ lịch sử và quy đổi sang nhu cầu nguyên liệu qua recipe/conversion.
3. Supplier scoring deterministic từ giá base-unit, MOQ, lead time, on-time rate và chất lượng giao.
4. LLM chỉ giải thích forecast/score; không tự chọn supplier hay trình bày forecast như số chắc chắn.

### Phase 4 – Optimization và Recommendation nâng cao

1. Constraint-based shift scheduling kết hợp availability, giới hạn giờ và forecast nhu cầu.
2. Cross-sell/upsell theo support/confidence/margin, có A/B testing.
3. Anomaly detection nâng cao và phân tích nguyên nhân đa biến.
4. Python model service chỉ khi baseline/ML.NET không đáp ứng và đã có năng lực vận hành.
5. Mọi mutation tiếp tục yêu cầu người dùng review/confirm.

## 13. Tiêu chí nghiệm thu cho mỗi use case AI

Mỗi tính năng chỉ được triển khai khi trả lời được:

- Người dùng và quyết định nghiệp vụ được hỗ trợ là ai/cái gì?
- Dữ liệu đầu vào đã tồn tại, đúng scope và đủ chất lượng chưa?
- Có giải pháp RULE/STATISTICS đơn giản hơn không?
- Output có thể kiểm chứng và có fallback không?
- Quyền dữ liệu được áp dụng trước query chưa?
- Có metric đánh giá giá trị và độ chính xác không?
- Có giới hạn, audit, rollback và human approval cho mutation không?

Nếu không có câu trả lời rõ ràng, use case chưa nên gắn nhãn AI hoặc đưa vào production.

## 14. Hiện trạng kỹ thuật dùng làm nền cho Phase 2–4

Thiết kế các phase tiếp theo phải dựa trên những contract đang tồn tại, không tạo một analytics stack song song:

- `DashboardService` đã áp dụng `IScopeAuthorizationService.GetAllowedStoresAsync()` trước khi gọi repository.
- `DashboardAnalyticsWidget` hiện có 35 widget thuộc 6 nhóm: Executive, Operations, Inventory, Procurement, Product và Workforce.
- `DashboardRepository` đã gọi stored procedure cố định và truyền danh sách Store đã được cấp quyền.
- `DashboardAnalyticsResponse` đã có `StoreIds`, khoảng thời gian, granularity, data status và warnings.
- `SupplierQualityService` đã tính được On-time rate, Fill rate, Rejection rate, Issue rate và Average delay days.
- `IngredientSupplier` đã có giá gói hiện tại, package quantity, MOQ, lead time, primary supplier và price history.
- `OrderDetail` đã có Drink, Size, quantity, selling price và FIFO COGS; dữ liệu này đủ để nghiên cứu basket recommendation không cá nhân hóa.
- `StaffShift` mới thể hiện lịch đã xếp; hệ thống chưa có dữ liệu availability, giới hạn hợp đồng và định mức nhân sự đầy đủ để tối ưu lịch an toàn.

Nguyên tắc dependency:

```text
Phase 1 Reorder + Notification foundation
                ↓
Phase 2 Dashboard intent + deterministic insight
                ↓
Phase 3 Forecast + supplier scoring
                ↓
Phase 4 Optimization + advanced recommendation
```

Không phase nào được bỏ qua StoreScope, schema validation, audit hoặc human approval đã thiết lập từ Phase 1.

## 15. Thiết kế chi tiết Phase 2 – Dashboard Intelligence

### 15.1 Mục tiêu và phạm vi

Phase 2 trả lời hai nhu cầu khác nhau:

1. Tự động phát hiện KPI đáng chú ý bằng RULE/STATISTICS.
2. Cho phép Owner/Manager hỏi dữ liệu Dashboard bằng tiếng Việt qua LLM intent parser.

LLM không tính KPI, không sinh SQL và không quyết định StoreScope. LLM chỉ chuyển câu hỏi thành intent có cấu trúc và giải thích dataset đã được hệ thống trả về.

### 15.2 Kiến trúc đề xuất

```text
AdminDashboardAiController
        ↓
DashboardIntelligenceService
        ├── DashboardIntentParser (LLM, optional)
        ├── DashboardIntentValidator (whitelist)
        ├── DashboardScopeResolver (existing authorization)
        ├── IDashboardService (existing)
        ├── DashboardInsightEngine (rule/statistics)
        ├── ChartRecommendationPolicy (deterministic)
        └── IAIService explanation (optional)
```

`DashboardIntelligenceService` được phép gọi `IDashboardService`; controller không gọi AI, repository hoặc stored procedure trực tiếp. Không tạo `AiDashboardRepository` nếu dữ liệu đã được `DashboardRepository` cung cấp.

### 15.3 Intent contract

Contract đề xuất cho parser:

```json
{
  "intentVersion": "v1",
  "widget": "NetSalesTrend",
  "period": {
    "type": "LastNDays",
    "value": 7
  },
  "comparison": "PreviousPeriod",
  "granularity": "Day",
  "top": 10,
  "storeSelector": {
    "mode": "AllowedScope",
    "storeName": null
  },
  "chart": "Line"
}
```

Whitelist bắt buộc:

- `widget`: chỉ giá trị trong `DashboardAnalyticsWidget` được công bố cho AI.
- `period.type`: `Today`, `Yesterday`, `LastNDays`, `ThisWeek`, `LastWeek`, `ThisMonth`, `LastMonth`, `Custom`.
- `comparison`: `None`, `PreviousPeriod`, `PreviousWeek`, `PreviousMonth`, `PreviousYear`.
- `granularity`: `Hour`, `Day`, `Week`, `Month`.
- `top`: 1–100.
- `chart`: `Kpi`, `Line`, `Bar`, `StackedBar`, `Heatmap`, `Table`.
- Không có field SQL, table, column, stored procedure hoặc arbitrary filter expression.

AI không được trả `StoreIds` làm quyền truy cập. Nếu người dùng nêu tên cửa hàng, service resolve tên đó trong danh sách Store đã được `DashboardService` cho phép; không khớp scope thì trả lỗi nghiệp vụ.

### 15.4 Catalog intent Phase 2

| Câu hỏi | Widget hiện có | Chart | Xử lý |
| --- | --- | --- | --- |
| Doanh thu 7 ngày gần đây | `NetSalesTrend` | Line | Existing Dashboard service |
| Doanh thu từng chi nhánh | `StoreRanking` | Bar | Existing Dashboard service |
| Top 10 sản phẩm tháng này | `TopProducts` | Bar/Table | Existing Dashboard service |
| Doanh thu theo giờ | `HourlyOrders` hoặc `OrderHeatmap` | Line/Heatmap | Existing Dashboard service |
| Waste tăng ở đâu | `InventoryWasteByStoreIngredient` | Bar/Table | Insight engine so sánh hai kỳ |
| PO nào trễ | `OverduePurchaseOrders` | Table | Existing Dashboard service |
| Chất lượng nhà cung cấp | `SupplierQuality`/`SupplierIssueMix` | Bar/Table | Existing Dashboard service |
| Tình trạng ca | `WorkforceShiftStatus` | Table | Existing Dashboard service |

Nếu một câu hỏi không map được đúng một widget hoặc một comparison plan hợp lệ, parser trả `UNSUPPORTED_INTENT`; không fallback sang SQL tự do.

### 15.5 Insight engine không dùng LLM

Các insight đầu tiên nên dùng rule/statistics:

| Insight | Công thức đề xuất | Điều kiện phát cảnh báo |
| --- | --- | --- |
| Doanh thu giảm | So kỳ hiện tại với kỳ trước cùng độ dài | Giảm vượt threshold cấu hình và đủ order mẫu |
| Waste tăng | Waste value/quantity hiện tại so baseline | Tăng vượt cả giá trị tuyệt đối và tỷ lệ |
| Cash discrepancy | Tổng absolute discrepancy hoặc shift outlier | Vượt ngưỡng tiền cấu hình |
| Stock risk | Dùng Inventory Threshold/Reorder result | Chỉ URGENT hoặc escalation |
| Supplier risk | Rejection/issue/late delivery | Đủ sample và vượt threshold |
| Thiếu lịch/trùng lịch | Query StaffShift deterministic | Có vi phạm thực tế |

Không chỉ dùng phần trăm. Ví dụ doanh thu giảm 50% từ 2 đơn xuống 1 đơn không nên tạo insight nghiêm trọng nếu volume thấp. Mỗi rule cần cả minimum sample và materiality threshold.

### 15.6 So sánh kỳ

Comparison không cần stored procedure động:

```text
Validated Intent
    ↓
Current Window → IDashboardService.GetAnalyticsAsync
Previous Window → IDashboardService.GetAnalyticsAsync
    ↓
Deterministic Comparison DTO
```

Kết quả phải lưu rõ:

- Current value.
- Baseline value.
- Absolute difference.
- Percentage difference khi mẫu số hợp lệ.
- Sample size.
- Data status/warnings của cả hai kỳ.

### 15.7 Chart policy

Chart được hệ thống chọn theo widget, AI chỉ được đề xuất trong whitelist:

- Time series → Line.
- Ranking/category → Bar.
- Day/hour matrix → Heatmap.
- Status pipeline → Stacked bar.
- Một số KPI → KPI card.
- Dữ liệu audit/exception → Table.

UI không render chart type ngoài catalog và không dùng HTML/JavaScript do AI tạo.

### 15.8 Skills và schemas dự kiến

```text
Resources/AI/skills/dashboard-intent-parser/SKILL.md
Resources/AI/schemas/dashboard-intent.schema.json

Resources/AI/skills/dashboard-insight-explanation/SKILL.md
Resources/AI/schemas/dashboard-insight-explanation.schema.json
```

`dashboard-intent-parser` chỉ parse. `dashboard-insight-explanation` chỉ giải thích dataset/scored insight. Hai skill không được gộp để tránh parser tự diễn giải hoặc tự thay đổi metric.

### 15.9 API contract dự kiến

```text
POST /Admin/AdminDashboardIntelligence/Parse
POST /Admin/AdminDashboardIntelligence/Execute
POST /Admin/AdminDashboardIntelligence/Explain
```

- `Parse`: trả validated intent để người dùng xem trước.
- `Execute`: nhận intent đã validate, service áp scope lại rồi query.
- `Explain`: nhận dataset identity hoặc result DTO phía server, không tin dataset do client gửi lại.
- Mỗi request có trace ID, timeout, row limit và rate limit theo Staff.

### 15.10 Notification Phase 2

Tái sử dụng `StaffNotification` và dedupe framework Phase 1:

```text
RecipientStaffId + StoreId + MetricCode + PeriodKey + INSIGHT_TYPE
```

Chỉ notification cho deterministic insight. LLM explanation không quyết định có gửi notification hay severity. Nên dùng daily digest cho WARNING; chỉ escalation material mới gửi ngay.

### 15.11 Chia lát triển khai

1. **Phase 2A:** insight rules và comparison DTO, chưa dùng LLM.
2. **Phase 2B:** intent parser với 5–8 intent phổ biến.
3. **Phase 2C:** chart builder và explanation theo nút bấm.
4. **Phase 2D:** notification digest và mở rộng intent catalog sau telemetry.

### 15.12 Exit gate Phase 2

- 100% intent map vào enum/whitelist; không có raw SQL field.
- Store ngoài scope bị từ chối trước query.
- Unsupported prompt trả lỗi rõ ràng, không hallucinate dataset.
- Các câu hỏi chuẩn đạt intent accuracy mục tiêu trên bộ test tiếng Việt có version.
- Ollama tắt vẫn xem Dashboard và chạy insight rules bình thường.
- Latency, row count, timeout và audit được đo.

## 16. Thiết kế chi tiết Phase 3 – Forecast và Supplier Intelligence

### 16.1 Tách Forecasting khỏi LLM

```text
Historical Series
    ↓
Data Quality Gate
    ↓
Forecast Model Runner
    ↓
Backtest + Model Selection
    ↓
Forecast Result + Interval
    ↓
Optional LLM Explanation
```

LLM không tạo `PointForecast`, lower/upper bound hoặc chọn model dựa trên cảm tính.

### 16.2 Data source thực tế

#### Revenue forecast

- Dùng daily/hourly series từ `NetSalesTrend` theo StoreScope.
- Trừ/ghi nhận refund theo định nghĩa net sales đang dùng trong Dashboard.
- Không trộn Store khác nhau vào một series nếu chưa chuẩn hóa.

#### Product forecast

- Cần series `StoreId + DrinkId + Date + SoldQuantity`.
- `TopProducts` hiện chỉ aggregate cả kỳ nên chưa đủ làm time series sản phẩm.
- Khi triển khai phải thêm query/stored procedure được review và version-control, ví dụ `sp_Forecast_ProductDailySeries`; không cho AI sinh procedure.

#### Inventory demand forecast

- Product forecast phải qua recipe/BOM và unit conversion hợp lệ.
- Không forecast nguyên liệu từ sản phẩm có BOM invalid hoặc incomplete.
- Stock-out phải được đánh dấu; ngày bán bằng 0 do hết hàng không được hiểu đơn giản là không có nhu cầu.

### 16.3 Data quality gate

Threshold là configuration, giá trị ban đầu đề xuất:

- Revenue Store: tối thiểu 56 ngày, ưu tiên 84 ngày để có weekday seasonality.
- Product: tối thiểu 84 ngày và số ngày có bán đạt tỷ lệ tối thiểu.
- Không quá tỷ lệ missing bucket cho phép.
- Không có timezone/bucket ambiguity.
- Price change, promotion và stock-out phải được đánh dấu nếu dữ liệu có.

Nếu không đạt gate:

```text
INSUFFICIENT_HISTORY
SPARSE_SERIES
STOCK_OUT_BIAS
MISSING_BOM
INVALID_CONVERSION
UNSTABLE_PRICE
```

Hệ thống không tạo một con số forecast giả để lấp UI.

### 16.4 Model ladder

Chọn model theo độ phức tạp tăng dần:

1. Seasonal naive theo cùng thứ trong tuần.
2. Moving average.
3. Exponential smoothing/Holt-Winters.
4. ML.NET time-series khi dữ liệu đủ và backtest chứng minh tốt hơn baseline.
5. Python model service chỉ khi các phương án trên không đáp ứng và có khả năng vận hành model riêng.

Không chọn model theo tên “AI”. Chọn bằng rolling time-series validation với MAE/WAPE; không random split dữ liệu thời gian.

### 16.5 Forecast contract

```json
{
  "forecastRunId": "server-generated-id",
  "seriesType": "StoreRevenue",
  "storeId": 3,
  "entityId": null,
  "trainingFrom": "2026-01-01",
  "trainingToExclusive": "2026-05-01",
  "horizonDays": 7,
  "modelType": "SeasonalNaive",
  "modelVersion": "v1",
  "quality": {
    "sampleCount": 120,
    "mae": 1250000,
    "wape": 12.4,
    "status": "ACCEPTABLE"
  },
  "points": [
    {
      "date": "2026-05-01",
      "pointForecast": 18000000,
      "lowerBound": 14500000,
      "upperBound": 21500000
    }
  ],
  "warnings": []
}
```

UI luôn hiển thị model, training cutoff, sai số backtest và khoảng dự báo; không chỉ hiển thị point forecast.

### 16.6 Batch architecture

- Forecast được tính bằng worker theo lịch, không train model trong request Dashboard.
- Revenue có thể chạy hằng ngày sau khi chốt dữ liệu ngày trước.
- Product/inventory chỉ chạy cho series đạt data gate.
- UI đọc kết quả gần nhất còn hiệu lực; có nút refresh có kiểm soát cho người có quyền.
- Worker xử lý từng Store độc lập và lỗi Store này không làm hỏng Store khác.

Khi cần audit/reproducibility, Phase 3 mới xem xét schema tối thiểu `ForecastRun` và `ForecastPoint`. Không tạo schema ở Phase 2 hoặc trước khi contract ổn định.

### 16.7 Inventory forecast integration

```text
Product Quantity Forecast
    ↓ Recipe/BOM + Conversion
Ingredient Demand Forecast
    ↓ Current/Reserved/Incoming
Projected Coverage Days
    ↓ Existing Reorder Rule
Reorder Suggestion
```

Forecast không thay thế reorder rule Phase 1. Nó chỉ bổ sung demand signal và coverage days. Quantity cuối vẫn qua MOQ, package conversion, pending PA/PO và human review.

### 16.8 Supplier scoring deterministic

Score được tính theo từng `Store + Ingredient + Supplier`, không có một điểm supplier chung cho mọi nguyên liệu.

Nguồn dữ liệu:

- Base-unit price từ CurrentPrice/PackageQuantity sau conversion.
- MOQ package count.
- Lead time và Store override.
- `SupplierPerformanceDto`: OnTimeRate, FillRate, RejectionRate, IssueRate, AverageDelayDays.
- Price history và độ biến động giá.
- Active Store/Supplier/offer.

Trọng số khởi đầu đề xuất, phải cấu hình và được nghiệp vụ duyệt:

| Nhóm | Trọng số |
| --- | ---: |
| Giá base-unit | 30% |
| On-time rate | 20% |
| Fill rate | 20% |
| Quality/rejection/issue | 20% |
| Lead time | 10% |

MOQ là constraint/penalty theo nhu cầu, không nên cộng điểm độc lập. Giá trị thiếu không được mặc định là tốt; supplier có dưới minimum sample phải mang trạng thái `INSUFFICIENT_DATA` và confidence thấp.

### 16.9 Supplier recommendation contract

```json
{
  "storeId": 3,
  "ingredientId": 15,
  "calculatedAtUtc": "2026-05-01T00:00:00Z",
  "requiredBaseQuantity": 50,
  "candidates": [
    {
      "supplierId": 8,
      "ingredientSupplierId": 21,
      "score": 82.5,
      "confidence": "MEDIUM",
      "packageCount": 5,
      "estimatedAmount": 575000,
      "componentScores": {
        "price": 90,
        "onTime": 80,
        "fill": 85,
        "quality": 78,
        "leadTime": 70
      },
      "warnings": []
    }
  ]
}
```

AI chỉ giải thích `componentScores`; AI không thay đổi ranking hoặc tự chọn supplier khi confidence thấp.

### 16.10 Skills dự kiến

```text
forecast-result-explanation
supplier-score-explanation
```

Forecast skill phải echo model/version/point/bounds/quality. Supplier skill phải echo supplier IDs, total score và component scores. Echo mismatch hoặc unknown field bị reject.

### 16.11 Chia lát triển khai

1. **Phase 3A:** revenue seasonal baseline + backtest + UI confidence.
2. **Phase 3B:** product daily series và sparse-series gate.
3. **Phase 3C:** inventory demand conversion và coverage days.
4. **Phase 3D:** supplier score deterministic.
5. **Phase 3E:** explanation/notification sau khi metrics ổn định.

### 16.12 Exit gate Phase 3

- Baseline backtest có version và không dùng future leakage.
- Forecast kém hơn seasonal baseline không được promote.
- Mọi result có bounds, quality status và training cutoff.
- Supplier score tái lập được từ dữ liệu và weight version.
- Không có supplier/forecast ngoài StoreScope.
- Không tự tạo PA/PO hoặc thay đổi supplier primary.

## 17. Thiết kế chi tiết Phase 4 – Cảnh báo lịch và Recommendation nâng cao

### 17.1 Cấu hình lịch và phạm vi nghiệp vụ

`StaffShift` và `Shift` tiếp tục là nguồn lịch chính thức. Màn hình **Cấu hình lịch & cảnh báo** chỉ quản lý:

- Availability định kỳ của nhân viên.
- Giới hạn giờ/ngày, giờ/tuần và thời gian nghỉ tối thiểu.
- Vai trò và minimum/target/maximum staffing theo Store, ca và ngày.
- Leave/time-off đã duyệt.

Hệ thống không tự tạo phương án phân công và không ghi `StaffShift`. Quản lý vẫn phân lịch thủ công qua service lịch hiện có.

### 17.2 Kiến trúc cảnh báo thiếu lịch

```text
Staffing Requirement
    + Existing StaffShift
    + Staff Availability
    + Time-off
    + Work Constraints/Role
        ↓
Rule-based Gap Detector
        ↓
Persist + Dedupe + Resolve
        ↓
SignalR Notification
        ↓
Manager opens manual schedule
```

Worker chỉ kiểm tra cửa hàng active và phạm vi thời gian được cấu hình. Rule backend quyết định ca có thiếu người; Ollama không tham gia quyết định và downtime AI không ảnh hưởng cảnh báo.

### 17.3 Điều kiện ứng viên phù hợp

- Staff/Account/Role active và thuộc đúng Store.
- Availability bao phủ toàn bộ ca.
- Không nằm trong time-off đã duyệt.
- Không trùng ca, kể cả ca qua đêm.
- Đủ thời gian nghỉ tối thiểu giữa hai ca.
- Không vượt giới hạn giờ ngày/tuần.
- Đúng vai trò bắt buộc của định mức.

Thông báo chỉ liệt kê ứng viên để quản lý kiểm tra; hệ thống không tự phân công người vào ca.

### 17.4 Dedupe, nhắc lại và resolve

- Dedupe theo người nhận, Store, định mức và ngày làm việc.
- Nếu ca vẫn thiếu, cập nhật cùng thông báo sau cooldown thay vì tạo dòng trùng.
- Khi ca đủ người hoặc ra ngoài cửa sổ kiểm tra, thông báo đang hoạt động được resolve.
- Người nhận phải có quyền xem thông báo, quyền xem lịch và StoreScope phù hợp.

### 17.5 Giữ dữ liệu lịch sử

Hai bảng lưu phương án phân công lịch sử và EF mapping tương ứng được giữ để bảo toàn dữ liệu. Không còn endpoint, service, AI skill hoặc giao diện tạo/đọc/áp dụng dữ liệu lịch sử này.

### 17.6 Giới hạn bắt buộc

- Không tự xếp hoặc thay đổi lịch.
- Không dùng LLM làm solver.
- Không gửi cảnh báo ngoài StoreScope.
- Không phụ thuộc Ollama để phát hiện, lưu hoặc phát cảnh báo.

### 17.7 Cross-sell/Upsell không cá nhân hóa

Phase đầu dùng basket association theo Store/period:

```text
Orders + OrderDetails + OrderToppings
        ↓
Eligible Completed/Counted Baskets
        ↓
Support + Confidence + Lift
        ↓
Margin + Menu Availability Filter
        ↓
Recommendation Catalog
```

Ví dụ rule:

- Support = số basket có A và B / tổng basket.
- Confidence(A→B) = basket có A và B / basket có A.
- Lift = Confidence(A→B) / Support(B).
- Chỉ gợi ý item đang bán tại Store, đủ inventory/menu availability và margin không âm.
- Loại combo đã có sẵn hoặc pair bị nghiệp vụ cấm.

Không dùng thông tin nhạy cảm hoặc hồ sơ khách hàng khi chưa có consent và use case rõ ràng.

### 17.8 Recommendation contract

```json
{
  "storeId": 3,
  "triggerDrinkId": 10,
  "generatedAtUtc": "2026-05-01T00:00:00Z",
  "modelVersion": "association-v1",
  "items": [
    {
      "drinkId": 18,
      "support": 0.12,
      "confidence": 0.34,
      "lift": 1.48,
      "confirmedMargin": 18000,
      "reasonCode": "FREQUENTLY_BOUGHT_TOGETHER"
    }
  ]
}
```

UI text có thể được LLM diễn đạt, nhưng thứ tự item phải do score deterministic tạo.

### 17.9 A/B testing prerequisite

Không thể đánh giá recommendation chỉ từ order cuối cùng. Cần audit exposure:

- Recommendation/version nào đã hiển thị.
- Store/POS session và thời điểm.
- Trigger item.
- Item được đề xuất.
- Người dùng có click/add/buy hay không.
- Control/treatment group.

Chỉ tạo schema exposure/conversion khi bước triển khai Phase 4 được duyệt. Không ghi PII không cần thiết.

### 17.10 Advanced anomaly detection

Thứ tự công nghệ:

1. Threshold/materiality rule.
2. Seasonal baseline.
3. Median/MAD hoặc robust z-score.
4. Multivariate model khi có đủ feature và nhãn điều tra.

Các metric ưu tiên:

- Revenue/order count bất thường theo Store và hour/day.
- Waste/adjustment tăng đột biến.
- Cash discrepancy lặp lại.
- Supplier rejection/late delivery tăng.
- Product volume giảm khi không có stock-out.

Anomaly chỉ là tín hiệu cần xem xét, không phải kết luận gian lận. Notification phải chứa metric, baseline, deviation, data window và confidence.

### 17.11 Human approval và safety

- Lịch nhân viên: Manager tự review và phân ca thủ công; cảnh báo không tự ghi lịch.
- Recommendation: chỉ hiển thị, không tự thêm vào order.
- Menu/price: không tự thay đổi.
- Inventory/Purchase: không tự mutation.
- Anomaly: không tự khóa nhân viên/supplier hoặc tạo quyết định kỷ luật.
- Mọi explanation phải dựa trên reason code/dataset đã tính.

### 17.12 Chia lát triển khai

1. **Phase 4A:** bổ sung dữ liệu availability/requirements/constraint/time-off.
2. **Phase 4B:** cảnh báo thiếu lịch rule-based, dedupe, resolve và SignalR.
3. **Phase 4C:** basket association offline + POS recommendation read-only.
4. **Phase 4D:** exposure logging và A/B testing.
5. **Phase 4E:** robust anomaly detection.

### 17.13 Exit gate Phase 4

- Cảnh báo thiếu lịch đúng định mức, StoreScope và điều kiện ứng viên.
- Dedupe, cooldown và resolve không tạo thông báo trùng hoặc tồn đọng sai.
- Recommendation không bypass menu/inventory/StoreScope.
- Có control group và conversion metric trước khi tuyên bố business value.
- Anomaly false-positive rate được theo dõi và có feedback workflow.
- LLM downtime không ảnh hưởng scheduling, POS hoặc alert core.

## 18. Contract dùng chung, observability và rollout

### 18.1 Feature flags

Mỗi capability cần flag độc lập:

```text
DashboardIntelligence:IntentParserEnabled
DashboardIntelligence:ExplanationEnabled
Forecasting:RevenueEnabled
Forecasting:ProductEnabled
SupplierIntelligence:ScoringEnabled
StaffScheduleNotifications:Enabled
PosRecommendation:Enabled
AnomalyDetection:Enabled
```

Không dùng một flag `AI.Enabled` để bật/tắt toàn bộ business rule.

### 18.2 Versioning

Mọi kết quả phải mang version phù hợp:

- Intent schema version.
- Insight rule version.
- Forecast model/version và training cutoff.
- Supplier weight version.
- Constraint version.
- Recommendation model version.
- Skill/schema version nếu explanation được lưu/audit.

### 18.3 Logging tối thiểu

- Feature/skill name.
- Actor StaffId hoặc pseudonymous audit identity.
- Store scope count, không log dữ liệu ngoài scope.
- Request type, data window, row count.
- Model/rule/version.
- Elapsed time, success/failure và fallback.
- Không log full prompt, PII, token, secret, password hoặc PIN.

### 18.4 Rate limit và caching

- Intent parse: rate limit theo Staff.
- Dashboard dataset: cache theo validated intent + allowed StoreIds + data version/TTL.
- Explanation: on-demand, không tự gọi cho toàn dashboard.
- Forecast: batch result cache/persistence, không train mỗi request.
- Recommendation: offline materialization theo Store/version.

Cache key bắt buộc chứa scope; không tái sử dụng dataset toàn chuỗi cho Store Manager.

### 18.5 Rollout strategy

```text
Development/Test
    ↓ Shadow calculation
    ↓ Internal Owner/Area Manager pilot
    ↓ Selected Stores
    ↓ Measured rollout
    ↓ Wider enablement
```

Mỗi phase cần dashboard theo dõi accuracy, latency, fallback rate, notification dismissal, user adoption và business KPI. Không mở toàn hệ thống chỉ vì test kỹ thuật đã pass.

### 18.6 Definition of Done cho Phase 2–4

Một phase chỉ hoàn thành khi:

1. Contract/schema được version và test.
2. Authorization/StoreScope test trước query.
3. Rule/model có baseline và metric đánh giá.
4. AI invalid/offline/timeout có fallback.
5. Không có mutation ngoài human approval.
6. Notification có dedupe/cooldown/resolve.
7. Logging không chứa dữ liệu nhạy cảm.
8. Có rollback/feature flag.
9. Có data-quality status, không bịa kết quả khi thiếu dữ liệu.
10. Có acceptance test trên dữ liệu nhiều Store và cross-scope.
