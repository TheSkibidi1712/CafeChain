# Hướng dẫn nghiệp vụ và kỹ thuật các chức năng AI của CafeChain

## 1. Mục đích và phạm vi

Tài liệu này mô tả đúng các chức năng AI có trong mã nguồn CafeChain tại thời điểm hiện tại. AI là lớp hỗ trợ đọc, phân tích và gợi ý; không tự duyệt chứng từ, không tự nhập kho, không tự đặt hàng và không thay thế các validation nghiệp vụ.

Luồng tổng quát:

`Câu hỏi/Context → BusinessIntent → AnswerFocus → DataPlan → truy vấn có scope → EvidencePack → biểu đồ/bảng → LLM có kiểm chứng hoặc fallback deterministic → UI`

Các route Dashboard `Parse`, `Execute`, `Explain`, `Analyze` được giữ tương thích. Luồng chính cho người dùng là `Analyze`.

## 2. Kiến trúc

| Lớp | Trách nhiệm |
|---|---|
| Controller | Nhận request, antiforgery/authentication, trả envelope HTTP |
| Application service | Hiểu câu hỏi, lập kế hoạch dữ liệu, điều phối widget, kiểm scope và dựng evidence |
| Repository/stored procedure | Chỉ đọc dữ liệu trong kỳ và danh sách Store đã được server cho phép |
| AIService | Gọi provider, kiểm JSON/evidence/numeric claim, trả fallback nếu không hợp lệ |
| Skill/schema | Giới hạn nhiệm vụ và cấu trúc JSON của từng chức năng |
| Razor/JavaScript | Hiển thị kết quả; không quyết định quyền hoặc số liệu nghiệp vụ |

Những thành phần được tái sử dụng gồm `DashboardQuestionCatalog`, `DashboardWidgetCatalog`, `DashboardIntelligenceService`, `AIService`, `AISkillCatalog`, `IAdminPermissionService`, `IScopeAuthorizationService` và `IAdminStoreScopeResolver`. Không có chatbot đa lượt, lịch sử hội thoại hay SQL động.

## 3. Provider và cấu hình

### Ollama

- Dùng cho gợi ý master data, giải thích gợi ý nhập hàng, giải thích Dashboard, forecast và anomaly.
- Client gửi structured prompt và nhận JSON; timeout, HTTP lỗi, JSON lỗi hoặc vi phạm evidence đều chuyển sang fallback.
- Health check chỉ cho biết provider/model có sẵn, không bỏ qua authorization.

### Pexels và ComfyUI

- Thuộc pipeline hình ảnh cho đồ uống/topping.
- Pexels tìm ứng viên ảnh theo metadata; ComfyUI có thể sinh ảnh theo visual specification.
- Pipeline có validation, scoring và fallback; không được ghi API key hoặc URL bí mật vào tài liệu/log.

### Quy tắc cấu hình

- Secret phải đến từ secret store hoặc environment của môi trường triển khai.
- Không log prompt đầy đủ chứa dữ liệu nhạy cảm; log chỉ giữ loại feature, thời gian, kích thước payload và loại lỗi.
- Tắt provider không làm hỏng nghiệp vụ chính vì các luồng bắt buộc có fallback hoặc thông báo không khả dụng.

## 4. Trạng thái chức năng thực tế

| Chức năng | Backend | UI/route | Fallback | Trạng thái |
|---|---|---|---|---|
| Gợi ý danh mục, đồ uống, size, topping | Có | Các màn hình admin master data | Mẫu deterministic và uniqueness policy | Đang hoạt động |
| Pipeline ảnh Pexels/ComfyUI | Có | Gắn vào đồ uống/topping | Giữ ảnh/mô tả an toàn khi provider lỗi | Đang hoạt động khi cấu hình provider |
| Giải thích gợi ý nhập hàng | Có | Reorder Suggestions/Explain | Giải thích theo rule | Đang hoạt động |
| AI Dashboard tập trung | Có | Dashboard/Analyze | 7 family deterministic | Đang hoạt động |
| Forecast doanh thu/sản phẩm | Có | `AdminIntelligence/Forecast` và worker | Model runner, cảnh báo chất lượng | Có backend/API; UI chính chưa phải luồng nổi bật |
| So sánh nhà cung cấp | Có | `AdminIntelligence` | Xếp hạng deterministic | Có backend/API |
| POS recommendation | Có | Worker/repository | Rule/model nội bộ | Backend nền; không phải chatbot UI |
| Operational anomaly | Có | Controller anomaly, notification, worker | Rule robust-score | Đang hoạt động ở backend/API |
| Các DTO narrative Dashboard v1 | Còn để tương thích | Renderer mới không dùng | Không áp dụng | Legacy/deprecated |

## 5. AI Dashboard

### 5.1 Hiểu câu hỏi

`DashboardQuestionUnderstandingDto` xác định:

- `BusinessIntent` và một `AnswerFocus` duy nhất;
- thực thể/chủ đề được phép và chủ đề bị loại trừ;
- metric, dimension, ranking/trend/comparison;
- kỳ dữ liệu và danh sách Store hiệu lực;
- có được phép trả action hay không.

16 câu hỏi hướng dẫn ánh xạ vào 16 focus canonical. Câu hỏi khác dùng focus động nhưng vẫn ánh xạ vào widget có sẵn; hệ thống không tạo SQL.

### 5.2 DataPlan và EvidencePack

DataPlan chỉ chọn `PrimaryWidget` và các `SupportingWidgets` được catalog cho phép. Mỗi plan chứa fields, metrics, filter, sort, limit, chart type, fallback family và các section được phép hiển thị.

Evidence do backend tạo, gồm metric/value/unit hiển thị, entity, Store, kỳ dữ liệu và trạng thái chất lượng. Tên thực thể và số liệu ngoài evidence bị validator từ chối.

### 5.3 Sáu AnswerStyle

- `DIRECT_COMPARISON`: so sánh hai kỳ hoặc hai nhóm.
- `RANKING`: xếp hạng cao/thấp.
- `TREND`: mô tả chuỗi thời gian khi đủ điểm.
- `RISK_ALERT`: ưu tiên tín hiệu rủi ro, không đoán nguyên nhân.
- `OPERATIONAL_PRIORITY`: nêu việc cần chú ý và bước kiểm tra read-only.
- `FACTUAL_STATISTICS`: thống kê trực tiếp, không thêm khuyến nghị.

### 5.4 Contract trả lời

- `DirectAnswer`: 2–4 câu, trả lời thẳng trọng tâm.
- `ProofPoints`: tối đa 3 ý, mỗi ý gắn evidence nội bộ.
- `ActionToCheck`: chỉ có với rủi ro/ưu tiên vận hành.
- `SectionConfig`: điều khiển chart/table/action/limitations.
- `DataSource`: vùng thu gọn chứa filter, widget, EvidenceId và lý do fallback.

UI chính không hiển thị AnalysisId, widget key, enum, EvidenceId hay lỗi provider. Các chi tiết kỹ thuật chỉ nằm trong “Xem nguồn dữ liệu”. JavaScript dùng `AbortController`, request sequence, context ID và filter fingerprint để response cũ không ghi đè response mới.

### 5.5 Top sản phẩm

- Widget: `TopProducts`.
- Xếp `TotalSold DESC`, hòa thì `NetSales DESC`, cuối cùng theo `DrinkId` để ổn định.
- Chart: HorizontalBar; chart và table dùng cùng rows.
- Nội dung trả lời chỉ xếp hạng, không tự sinh khuyến nghị.

### 5.6 Bất thường vận hành

- Tối đa 3 proof points ưu tiên Critical/High trước.
- Đơn vị kỹ thuật được đổi sang tiếng Việt, ví dụ `DAY → ngày`, `HOUR → giờ`.
- Chỉ mô tả tín hiệu và điều cần kiểm tra; không bịa nguyên nhân.

### 5.7 Fallback

Các family: `Ranking`, `Comparison`, `Trend`, `Risk`, `Statistics`, `OperationalPriority`, `NoData`. Fallback trả cùng DTO/layout với LLM. Lý do provider lỗi không xuất hiện trong nội dung chính.

## 6. Gợi ý nhập hàng và quyền

- Xem: `ReorderSuggestion.View`.
- Xác nhận/tạo hoặc bổ sung RestockRequest: `Restock.Create`.
- Authorization là permission-first và vẫn kiểm account override.
- BusinessOwner/StoreManager theo Effective StaffScope.
- SystemAdmin chỉ có all-active-store scope trong module Reorder Suggestions; Dashboard và module khác vẫn theo StaffScope.
- Store ngoài scope trả 403 và tạo `AuditLog` action `STORE_SCOPE_DENIED`.
- Server luôn tính lại quantity; token, fingerprint, RequestKey, transaction và concurrency guard vẫn là nguồn sự thật.

## 7. Bảng audit Kho & Cung ứng

| Nhóm/route chính | Permission GET | Permission ghi | Scope/validation giữ nguyên |
|---|---|---|---|
| StoreInventory | `Inventory.View` | `Inventory.Adjust/Export` theo action | Admin store resolver |
| InventoryThreshold | `InventoryThreshold.View` | `InventoryThreshold.Update` | Store resolver, threshold validation |
| StockAlert | `StockAlert.View` | `Resolve/CreateRestockRequest` | Store resolver, state/row-version |
| OperationalIce | `OperationalIce.View` | `Manage/Approve/Policy` | Store resolver, shift rules |
| RestockRequest | `Restock.View` | `Create/Submit/Approve/Reject/Cancel/Update/CloseRemaining` | Workflow service, transaction/SoD |
| ReorderSuggestion | `ReorderSuggestion.View` | `Restock.Create` | Module scope, token/dedup/audit |
| PurchaseAdvice | `PurchaseAdvice.View/Consolidate` | Permission tương ứng action | Workflow state |
| PurchaseOrder/batch | `PurchaseOrder.View/ViewBatch` | Create/Approve/Send/Cancel/Export... | Allocation, state, row-version |
| Receipt | `Receipt.View` | Create/UpdateDraft/Confirm... | Posting service, inventory transaction |
| Supplier/quality | `Supplier.View`, `SupplierQuality.View` | Create/Update/Transition | Supplier status and quality workflow |
| Ingredient/unit | `Ingredient.View`, `UnitConversion.View` | Create/Update/ToggleStatus | Master-data validation |
| BOM/production | `PreparedItem.View`, `Recipe.View`, `ProductionOrder.View` | Permission action | BOM integrity, production transaction |
| Phiếu/chuyển kho | `InventoryDocument.View`, `InventoryTransfer.View` | Permission action | StaffScope, state, discrepancy/return rules |

Menu cha chỉ hiện khi có ít nhất một quyền con; nút ghi lấy từ effective permission. Backend kiểm độc lập nên gọi URL trực tiếp không thể vượt quyền.

## 8. Bảo mật và độ tin cậy

- Permission policy kiểm account active, role active, role grant và account override.
- Mọi StoreId do client gửi phải là tập con của Effective StaffScope; không âm thầm bỏ phần ngoài scope.
- LLM chỉ nhận dataset đã giới hạn, không có quyền truy cập database.
- Validator chặn JSON sai schema, EvidenceId sai, entity/số bịa, numeric claim không grounded, prompt injection và action sai focus.
- Các thao tác ghi quan trọng giữ antiforgery, RequestKey, transaction, row-version và audit.

## 9. Sử dụng và xử lý lỗi

1. Chọn đúng kỳ và cửa hàng trên Dashboard.
2. Chọn câu hỏi hướng dẫn hoặc nhập câu hỏi ngắn, một trọng tâm.
3. Đọc trả lời trực tiếp và tối đa 3 proof points.
4. Chỉ dùng “Việc cần kiểm tra” như bước xác minh; thực hiện nghiệp vụ ở module chuyên trách.
5. Mở “Xem nguồn dữ liệu” khi cần đối soát filter/evidence.

Nếu kết quả là NoData, kiểm tra kỳ, StoreScope và dữ liệu nguồn. Nếu fallback, facts/chart vẫn do backend tạo; provider có thể đang tắt, timeout hoặc trả JSON không hợp lệ. Không dùng fallback để suy ra nguyên nhân ngoài evidence.

## 10. Kiểm thử và giới hạn

Kiểm thử bắt buộc gồm RBAC/override/403, StaffScope và tampering, replay/double-click/concurrency của Reorder, SeedAll chạy hai lần, 16 câu hỏi Dashboard, Top Products, anomaly, no-data, timeout, JSON sai, fabricated claim, prompt injection, recommendation gate, section visibility và stale-response guard.

### Dữ liệu demo AI gợi ý nhập hàng

Sau khi chạy `Scripts/SeedAll.sql`, marker `DEMO_AI_REORDER_TEST_V1` bảo đảm Store 1 có nguyên liệu **Hạt chia** (`DEMO_ING_CHIA_SEED`) ở trạng thái cần nhập khẩn cấp. Seed chỉ chuẩn bị tồn, ngưỡng, lịch sử tiêu thụ và nguồn cung; số lượng gợi ý vẫn do backend tính.

- Mở **Kho & Cung ứng → Gợi ý nhập hàng**, chọn Store 1 và kiểm tra dòng Hạt chia có số lượng, số gói, chi phí và nút giải thích.
- Khi Ollama khả dụng, nút giải thích dùng model; khi provider không khả dụng, cùng thao tác trả về fallback deterministic.
- Tại AI Dashboard, hỏi **“Nguyên liệu nào cần đặt lại?”**; Hạt chia phải xuất hiện trong dữ liệu ưu tiên nhập hàng.

Giới hạn hiện tại: không hội thoại đa lượt; không SQL động; forecast/POS/anomaly có phần backend nền chưa đồng đều về UI; chất lượng kết quả phụ thuộc dữ liệu nguồn; LLM không được phép tự thực thi hành động.
