# Báo cáo nghiệp vụ Gợi ý nhập hàng và Dashboard AI

Tài liệu này là báo cáo triển khai theo mục 94 của `FIX.md`, đồng thời là hướng dẫn vận hành nhanh cho Chủ doanh nghiệp, Quản lý cửa hàng và Kế toán/Kho.

Nguyên tắc xuyên suốt:

```text
Rule tính toán
→ AI giải thích
→ Con người xác nhận
→ Server kiểm tra lại
→ Yêu cầu nhập hàng
→ Đề nghị mua hàng
→ Đơn đặt hàng
→ Nhận hàng
→ Tồn kho
```

AI không tự đặt hàng, tự duyệt chứng từ hoặc tự thay đổi tồn kho.

## Hướng dẫn sử dụng nhanh

### Vị trí và quyền

- Menu: **Kho & Cung ứng → Gợi ý nhập hàng**.
- Route: `GET /Admin/AdminReorderSuggestions/Index`.
- Quyền xem: `ReorderSuggestion.View`.
- Quyền tạo hoặc bổ sung yêu cầu nhập: `Restock.Create`.
- Backend giới hạn role nghiệp vụ theo `StaffScope`; SystemAdmin có global scope trên cửa hàng active. Việc sửa `storeId` trên URL hoặc request không mở rộng phạm vi của các role khác.

### Cách sử dụng

1. Chọn cửa hàng và kỳ phân tích 30, 60 hoặc 90 ngày, sau đó nhấn **Tính lại**.
2. Hệ thống deterministic kiểm tra toàn bộ nguyên liệu của cửa hàng được chọn.
3. Sau khi tính thành công, modal tự mở nếu có dòng `URGENT`, `NEAR_REORDER` hoặc `PROCUREMENT_IN_PROGRESS` còn lượng cần bổ sung.
4. Kiểm tra tồn khả dụng, điểm đặt hàng, hàng đang về, lượng pipeline bao phủ, lượng còn thiếu, quy cách mua và Nhà cung cấp.
5. Các dòng hợp lệ được chọn sẵn. Có thể bỏ chọn trước khi nhấn **Tạo yêu cầu nhập**.
6. Xác nhận lần hai. Giao diện gửi tuần tự từng yêu cầu bằng hợp đồng Confirm hiện có và hiển thị kết quả riêng cho từng nguyên liệu.
7. Mở yêu cầu đã tạo để tiếp tục luồng **Yêu cầu nhập → Đề nghị mua → Đơn đặt hàng → Nhận hàng**.

`DATA_INCOMPLETE` chỉ xuất hiện như cảnh báo và không được xác nhận. Người dùng phải bổ sung ngưỡng, lịch sử tiêu thụ, nguồn cung, giá, thời gian giao, MOQ hoặc quy đổi còn thiếu.

Nút **Giải thích** mới gọi AI. Việc tải trang và mở modal không gọi Ollama.

---

## A. Những vấn đề đã phát hiện

### 1. Pipeline mua hàng có nguy cơ bao phủ sai nhu cầu

**Vấn đề:** RestockRequest, Purchase Advice (PA) và Purchase Order (PO) là các bước của cùng một nhu cầu nhưng có thể bị xem như các lượng độc lập.

**Nguyên nhân:** Logic cũ lấy số dư RestockRequest rồi trừ phần PO quy về pipeline, chưa biểu diễn đầy đủ phần PA chỉ bao phủ một phần.

**Ảnh hưởng nghiệp vụ:** Một RestockRequest 100 có PA 30 có thể bị coi là đã bao phủ đủ 100, làm mất đề xuất 70 còn thiếu; hoặc cùng một lượng có thể bị đếm ở nhiều stage.

### 2. Fingerprint chưa chứa đủ dữ liệu quyết định

**Vấn đề:** Token có thể chưa stale khi Nhà cung cấp, quy cách, MOQ hoặc chi phí thay đổi.

**Nguyên nhân:** Decision fingerprint cũ tập trung vào tồn, nhu cầu và một phần thông tin offer.

**Ảnh hưởng nghiệp vụ:** Người dùng có thể xác nhận một gợi ý dựa trên điều kiện mua không còn đúng.

### 3. Xử lý nhiều nguyên liệu còn rời rạc

**Vấn đề:** Trang có bảng và nút xác nhận từng dòng nhưng chưa tự gom các nguyên liệu gần điểm đặt hàng.

**Nguyên nhân:** Chưa có lớp trình bày modal và điều phối tuần tự các Confirm hiện có.

**Ảnh hưởng nghiệp vụ:** Quản lý phải rà từng dòng, dễ bỏ sót nguyên liệu và mất thời gian tạo yêu cầu.

### 4. AI Dashboard nằm chung với các widget

**Vấn đề:** Khu vực câu hỏi/kết quả AI chiếm không gian của Dashboard nghiệp vụ.

**Nguyên nhân:** Khối AI được đặt trước thanh tab thay vì có tab riêng.

**Ảnh hưởng nghiệp vụ:** Giao diện khó tập trung; việc ẩn/hiện kết quả AI và chuyển nhóm widget không rõ ràng.

### 5. Biểu đồ phụ thuộc vào thay đổi kích thước cửa sổ

**Vấn đề:** Một số chart chỉ xuất hiện sau khi phóng to hoặc thu nhỏ trình duyệt.

**Nguyên nhân:** ECharts có thể được khởi tạo khi tab/container chưa có kích thước ổn định; `window.resize` sau đó mới buộc chart tính lại.

**Ảnh hưởng nghiệp vụ:** Người dùng thấy vùng biểu đồ trắng dù dữ liệu đã tải thành công.

---

## B. Những file đã sửa

### Nghiệp vụ gợi ý nhập hàng

- `Application/Services/Inventories/ReorderSuggestionService.cs`
  - Chuẩn hóa coverage theo lineage RestockRequest → PA → PO.
  - Giữ PO đang về trong `IncomingQuantity`, không trừ lặp trong procurement coverage.
  - Tăng calculation version từ `REORDER_RULES_V2` lên `REORDER_RULES_V3`.
- `Application/DTOs/Admin/Procurement/ReorderSuggestionConfirmationDtos.cs`
  - Mở rộng dữ liệu quyết định được ký bằng supplier, package, MOQ và estimated cost.
- `Application/Services/Inventories/ReorderSuggestionTokenService.cs`
  - Đưa các trường quyết định mới vào canonical fingerprint.

### Giao diện

- `Areas/Admin/Views/AdminReorderSuggestions/Index.cshtml`
  - Thêm modal tự quét, chọn nhiều, xác nhận lần hai và kết quả theo từng dòng.
  - Giữ nguyên Confirm từng nguyên liệu, antiforgery token và suggestion token.
- `wwwroot/css/Admin/Procurement/reorder-suggestions.css`
  - Thêm trạng thái canonical, bố cục modal/kết quả và responsive.
- `Areas/Admin/Views/Dashboard/Index.cshtml`
  - Thêm tab **Hỏi AI** và `tabpanel` riêng; giữ các DOM ID và endpoint AI cũ.
- `wwwroot/js/Admin/Dashboard/dashboard.js` và `dashboard-intelligence.js`
  - Quản lý vòng đời chart khi đổi tab/ẩn hiện và deferred resize bằng `requestAnimationFrame` + `ResizeObserver`.
- `wwwroot/css/Admin/Dashboard/dashboard.css`
  - Ổn định kích thước chart và kiểu hiển thị tab/panel AI.

### Tài liệu

- `Doc/AI_REORDER_DASHBOARD_IMPLEMENTATION.md`
  - Báo cáo A–K và hướng dẫn sử dụng gợi ý nhập.
- `Doc/AI_DASHBOARD_USER_GUIDE.md`
  - Bổ sung cách mở tab AI và hành vi chart.
- `docs/user-guides/dashboard-analytics.md`
  - Cập nhật từ sáu tab nghiệp vụ thành sáu tab dữ liệu cộng một tab AI.
- `docs/user-guides/inventory-alerts-cogs-bom-operations.md`
  - Bổ sung modal gợi ý nhập và phân biệt StockAlert với ReorderSuggestion.
- `CafeChain.Tests/ReorderSuggestionPipelineCoverageTests.cs`,
  `CafeChain.Tests/ReorderSuggestionTokenServiceTests.cs` và
  `CafeChain.Tests/ReorderSuggestionBulkModalContractTests.cs`
  - Khóa các contract lineage, fingerprint và giao diện bulk modal.
- `CafeChain.Tests/DashboardIntelligenceP0P1ContractTests.cs`
  - Khóa contract tab AI và vòng đời ECharts sau khi panel hiển thị.

`FIX.md` không bị chỉnh sửa bởi phạm vi triển khai này; mọi thay đổi đã có trong worktree của người dùng được giữ nguyên.

---

## C. Những method đã sửa

| Class/module | Method hoặc vùng xử lý | Logic cũ | Logic mới |
|---|---|---|---|
| `ReorderSuggestionService` | Tính item và procurement coverage | Chủ yếu lấy số dư RestockRequest rồi loại phần PO đã quy về pipeline | Theo từng root RestockRequest: dùng stage downstream cao nhất; PA chỉ bao phủ `Requested - Allocated - Closed`; draft PO là coverage không-incoming; approved/sent/partial PO chỉ là incoming; mọi lượng được clamp theo root và raw demand |
| `ReorderSuggestionContractMapper` | `ToDecision` | Chưa ánh xạ đủ quyết định supplier/package/MOQ/cost | Ánh xạ toàn bộ trường có thể làm thay đổi quyết định nhập |
| `ReorderSuggestionTokenService` | `ComputeDecisionFingerprint` | Hash chưa chứa đủ điều kiện mua | Hash canonical gồm tồn, nhu cầu, trạng thái, supplier, giá, đơn vị/quy cách, MOQ và chi phí |
| `AdminReorderSuggestions/Index` | Xử lý confirm phía trình duyệt | Xác nhận từng dòng và điều hướng tới một request | Giữ luồng một dòng; thêm điều phối tuần tự nhiều dòng, RequestKey riêng, retry và tổng hợp kết quả |
| `Dashboard/Index` | Chuyển tab | Chỉ có sáu tab dữ liệu, AI nằm ngoài tab | Có tab AI riêng; tab AI không gọi `GetSection`, normal tab không làm mất kết quả AI; hỗ trợ Arrow/Home/End và ARIA |
| `dashboard.js` | `renderChart`, dispose và chuyển tab | Chart có thể init trước khi layout ổn định | Chỉ init khi có kích thước, resize trễ, quan sát container và dispose observer/instance |
| `dashboard-intelligence.js` | `renderChart`, `disposeCharts`, ẩn/hiện kết quả | Chủ yếu phụ thuộc `window.resize` | Resize khi tab/kết quả hiện, có observer và deferred layout; vẫn giữ table fallback |

Nội dung câu trả lời của Dashboard AI không được chỉnh sửa trong đợt này; chỉ thay đổi vị trí hiển thị và vòng đời chart.

---

## D. Công thức cuối cùng

Với một nguyên liệu trong một cửa hàng và một kỳ phân tích:

```text
AvailableStock
  = OnHandQuantity - ReservedQuantity

AverageDailyConsumption
  = (SALES_DEDUCTION + PRODUCTION_OUT hợp lệ trong kỳ)
    / AnalysisWindowDays

ReorderPoint
  = AverageDailyConsumption × LeadTimeDays + MinimumStock

IncomingQuantity
  = Σ max(0, OrderedBaseQuantity
             - AcceptedBaseQuantity
             - ClosedRemainingQuantity)
    của PO còn hiệu lực

ProjectedStock
  = AvailableStock + IncomingQuantity

RawDemand
  = max(0, ReorderPoint - ProjectedStock)

ProcurementCoveredQuantity
  = min(RawDemand, active non-incoming coverage
                   theo lineage RestockRequest → PA → PO)

RemainingDemand
  = max(0, RawDemand - ProcurementCoveredQuantity)

SuggestedPackageQuantity
  = 0, nếu RemainingDemand = 0
  = max(ceil(RemainingDemand / PackageBaseQuantity),
        MinimumOrderPackageCount), nếu RemainingDemand > 0

FinalSuggestedQuantity
  = SuggestedPackageQuantity × PackageBaseQuantity
```

`SuggestedPackageQuantity` là số gói mua; `FinalSuggestedQuantity` là lượng theo đơn vị tồn kho cơ sở. Hai trường không được dùng thay thế cho nhau.

Nếu thiếu dữ liệu bắt buộc, kết quả là `DATA_INCOMPLETE`; hệ thống không tự điền số 0 để tiếp tục tính.
Tồn âm vẫn được giữ theo chính sách hiện hành và không bị giao diện tự sửa về 0.

---

## E. Procurement pipeline

Hệ thống nhóm các chứng từ theo RestockRequest gốc:

```text
RestockRequest 100
└─ PA đang bao phủ 30
   └─ PO đang về một phần
```

Quy tắc chống đếm trùng:

1. RestockRequest chưa có PA/PO downstream được tính một lần theo số dư chưa hoàn tất.
2. Khi có PA, chỉ `max(0, RequestedPA - AllocatedToPO - ClosedPA)` còn active được tính là procurement coverage.
3. Draft PO thay lower stage và là non-incoming coverage. PO approved/sent/partially-received được tính trong `IncomingQuantity`, không cộng lại RestockRequest hoặc PA.
4. PA được nhận diện bằng `IsActiveReservation` và số dư dòng, không chỉ bằng trạng thái header.
5. Coverage không vượt số dư RestockRequest gốc và không vượt `RawDemand`.

Ví dụ:

```text
RawDemand = 100
PA active = 30
PO incoming = 0

ProcurementCoveredQuantity = 30
RemainingDemand = 70
```

Nếu cùng 30 đơn vị đi qua RestockRequest → PA → PO thì 30 đó chỉ ảnh hưởng nhu cầu một lần.

---

## F. Double click / Idempotency

### Frontend

- Vô hiệu hóa nút và checkbox khi từng request đang gửi.
- Mỗi nguyên liệu có một `RequestKey` riêng tạo bằng UUID.
- Retry cùng một nguyên liệu dùng lại key cũ; không tạo key mới cho cùng lần quyết định.
- Xử lý tuần tự giúp hiển thị trạng thái độc lập và không tạo burst request.
- Không cập nhật thành công lạc quan trước khi server trả kết quả.

### Backend

- Action idempotency là `REORDER_SUGGESTION_CONFIRM`.
- `RequestKey` được lưu với payload/result; gửi lại cùng key nhận kết quả replay.
- Confirm mở transaction, khóa theo cửa hàng/nguyên liệu, tính lại bằng `ReorderSuggestionService`, so fingerprint rồi mới tạo hoặc bổ sung RestockRequest.
- Hai quản lý xác nhận đồng thời không được tạo hai nhu cầu độc lập cho cùng quyết định; conflict hoặc request đang xử lý được trả bằng error code rõ ràng.
- Snapshot nghiệp vụ tại thời điểm xác nhận được lưu bất biến để audit.

---

## G. Security

| Kiểm soát | Cách áp dụng |
|---|---|
| Permission | `ReorderSuggestion.View` để xem; Confirm kiểm tra thêm `Restock.Create` |
| StoreScope | Backend resolve cửa hàng từ actor và từ chối store ngoài scope |
| CSRF | GET không tạo chứng từ; Explain/Confirm POST bắt buộc antiforgery token |
| XSS | Razor encode dữ liệu server; JavaScript dùng `textContent`; không render raw HTML từ AI |
| Input validation | `storeId`, `ingredientId`, token và RequestKey có DataAnnotations và server validation |
| Overposting | Confirm không nhận quantity, status, supplier hoặc cost từ client |
| SQL injection | Dữ liệu qua repository/EF hoặc stored procedure có tham số; AI không sinh SQL |
| AI output safety | Output phải qua schema/evidence validation; AI không có quyền gọi Confirm hoặc thay đổi chứng từ |

Suggestion token gắn với staff, store, ingredient, kỳ phân tích, thời hạn và decision fingerprint. Server luôn tính lại; token hợp lệ không có nghĩa dữ liệu cũ vẫn được chấp nhận.

Calculation version được tăng từ `REORDER_RULES_V2` lên `REORDER_RULES_V3`; mọi token phát hành trước khi triển khai semantics lineage/fingerprint mới tự hết hiệu lực an toàn.

---

## H. AI

### AI nhận

- Snapshot deterministic của nguyên liệu: tồn, giữ chỗ, ROP, tiêu thụ, incoming, pipeline, remaining demand, package/MOQ, supplier, status và reason codes.
- Với Dashboard: Fact, Statistic, Anomaly và Entity Evidence đã được backend giới hạn theo filter và StaffScope.

### AI được trả

- `Summary`
- `Explanation`
- `Risk`
- `RecommendedActionText`

Các phần trên là diễn giải và khuyến nghị kiểm tra. Số lượng cuối cùng vẫn do service deterministic quyết định.

### AI bị cấm

- Không query database hoặc sinh SQL.
- Không sửa số lượng, trạng thái, supplier hoặc giá.
- Không gọi Confirm, tự tạo RestockRequest, PA, PO hay phiếu nhận.
- Không tự duyệt hoặc tự thay đổi tồn kho.
- Không bịa số liệu khi evidence thiếu.

### Fallback

Nếu Ollama tắt, timeout, lỗi HTTP, JSON/schema không hợp lệ hoặc số liệu không khớp evidence, hệ thống trả deterministic fallback. Modal tự quét không phụ thuộc Ollama.

---

## I. Dashboard

### Nguồn dữ liệu

- Trước refactor, widget gợi ý nhập có thể phụ thuộc dataset/procedure Dashboard riêng và có nguy cơ lệch công thức.
- Sau refactor, `DashboardService.BuildReorderWidgetAsync` gọi `IReorderSuggestionService.CalculateForStoresAsync`; Dashboard chỉ map DTO sang widget.
- Cửa sổ phân tích của widget Dashboard được ghi rõ là 30 ngày. Form Gợi ý nhập cho phép người dùng chọn 30/60/90 ngày.
- `usp_Inventory_ReorderSuggestions` không còn là nguồn tính cho widget gợi ý nhập. Nếu script/procedure vẫn còn trong database để tương thích triển khai cũ, nó là legacy và không được dùng làm nguồn quyết định hoặc nguồn Confirm.

### Tab AI và chart

- Dashboard có sáu tab dữ liệu và một tab **Hỏi AI**.
- Tab AI giữ nguyên prompt/result DOM ID, endpoint và nội dung câu trả lời.
- URL có `aiQuestion` mở tab AI, điền câu hỏi và focus nhưng không tự gửi.
- Chart thường và chart AI được resize sau khi container thực sự hiển thị; không cần zoom trình duyệt.

Nội dung câu trả lời AI Dashboard đã được refactor theo `AnswerFocus`,
`DashboardDataPlan` và `EvidencePack`. Endpoint cũ vẫn giữ nguyên; frontend chỉ
mở rộng renderer để hiển thị context, kết luận, chart/evidence, giới hạn và chỉ
hiện recommendation khi backend cho phép.

---

## J. Tests

Trạng thái phải được cập nhật bằng kết quả thực tế; `NOT RUN` không được hiểu là PASS.

| Nhóm kiểm thử | Kịch bản | Trạng thái tại thời điểm lập tài liệu |
|---|---|---|
| Reorder calculation | `NEAR_REORDER` khi available chưa dưới minimum nhưng projected dưới ROP | PASS |
| Pipeline | Raw demand 100, PA active 30, remaining 70 | PASS — `Active_purchase_advice_covers_only_its_unallocated_residual` |
| Pipeline | PA/PO thay thế lower stage, không đếm trùng | PASS — approved PO và draft PO đều được kiểm tra |
| Incoming | PO 100, accepted 70, incoming 30 | PASS — `Partial_purchase_order_uses_only_unreceived_unclosed_quantity` |
| Package/MOQ | Demand 13, package 5, MOQ 4 → 4 gói/20 đơn vị | PASS |
| Data quality | Thiếu threshold/history → `DATA_INCOMPLETE` | PASS |
| Data quality | Thiếu supplier/conversion → `DATA_INCOMPLETE` | PASS |
| Token | Token gắn staff/store/ingredient, chống sửa và hết hạn | PASS |
| Token | Đổi supplier/package/MOQ làm fingerprint thay đổi | PASS |
| Token | Đổi giá/package unit làm fingerprint thay đổi | PASS |
| Confirm contract | DTO whitelist, Permission, StoreScope, CSRF, server revalidation, lock và replay RequestKey | PASS — source/reflection contract |
| Modal contract | Không POST khi tải trang; auto-open; chọn sẵn; xử lý tuần tự; lỗi từng dòng | PASS — 3/3 source contract |
| Procurement minimalism | Giao diện/luồng mua hàng không tự bỏ qua các stage kiểm soát | PASS — nằm trong nhóm modal + procurement 27/27 |
| Dashboard intelligence contract | Tab AI, ARIA, `aiQuestion`, schema/fallback liên quan | PASS — 13/13 |
| Dashboard Guide catalog | 16 câu, 16 focus canonical duy nhất, đúng widget/style, không `Dynamic` | PASS |
| SystemAdmin RBAC V2 | Exact 161 active grant, global store scope, `Deny` và account inactive vẫn chặn | PASS |
| Phase 3/4 source contract | Guide deep-link, ID/label và hành vi tương thích | PASS — 4/4 |
| AI Dashboard UI contract | Tab/panel và UI contract | PASS — 5/5 |
| JavaScript syntax | `node --check` cho hai file Dashboard | PASS — 2/2 |
| Chart lifecycle | Deferred double-rAF, `ResizeObserver`, resize event, dispose và fallback | PASS — source/contract; browser visual chưa chạy |
| Browser E2E | Desktop/mobile, đổi tab và resize không cần zoom | NOT RUN — browser runtime chưa khả dụng |
| AI tự đặt/duyệt/nhận hàng | Cố ý không thuộc thiết kế | NOT IMPLEMENTED |
| ML forecast | Cố ý không thuộc thiết kế | NOT IMPLEMENTED |

File `BomToppingConsumptionSourcesIssue149Tests.cs` thuộc module khác và không được sửa trong phạm vi này. Nội dung gây lỗi trước đó hiện đang được comment trong worktree nên không còn chặn compilation; trạng thái đó không được dùng để nhận công sửa lỗi cho thay đổi này.

---

## K. Build

Các lệnh nghiệm thu:

```powershell
dotnet build CafeChain/CafeChain.csproj --no-restore

dotnet test CafeChain.Tests/CafeChain.Tests.csproj `
  --filter "FullyQualifiedName~ReorderSuggestionPipelineCoverageTests|FullyQualifiedName~ReorderSuggestionTokenServiceTests|FullyQualifiedName~ReorderSuggestionConfirmationContractTests|FullyQualifiedName~ReorderSuggestionIssue176Tests|FullyQualifiedName~ReorderSuggestionBulkModalContractTests|FullyQualifiedName~DashboardIntelligenceP0P1ContractTests|FullyQualifiedName~Phase3Phase4RefactorSourceTests|FullyQualifiedName~AiDashboardAndShiftOptimizationUiContractTests|FullyQualifiedName~ProcurementOperationalMinimalismIssue240Tests"

node --check CafeChain/wwwroot/js/Admin/Dashboard/dashboard.js
node --check CafeChain/wwwroot/js/Admin/Dashboard/dashboard-intelligence.js
```

| Lệnh | Kết quả |
|---|---|
| `dotnet build CafeChain/CafeChain.csproj --no-restore` sau thay đổi cuối | PASS — 0 error, 653 warning nền |
| Combined targeted tests trên source đã build | PASS — 90/90 |
| `node --check` hai file Dashboard | PASS — 2/2 |
| Suite không phụ thuộc SQL Server | PASS — 1.558/1.558 |
| SeedAll + analytics SQL contract | PASS — chạy SeedAll hai lần, không duplicate, override không đổi và exact matrix đúng |
| Full test project gồm mọi fixture SQL chạy song song | FAIL do môi trường — `Failed to generate SSPI context`; SQL target chạy riêng PASS |

Full suite được chạy bằng `--no-build` ngay sau lần combined test đã build source thành công; kết quả này hợp lệ để phát hiện hồi quy runtime nhưng không thể hoàn tất do môi trường SQL Server. Các dòng `NOT RUN` khác được giữ rõ ràng vì chưa có bằng chứng chạy thực tế và không được suy diễn thành PASS.

---

## L. Refactor AI Dashboard theo AnswerFocus

Nguyên nhân câu trả lời cũ dễ lặp là data plan tổng quát thường nạp cùng một tập
widget lớn cho nhiều câu hỏi. Narrative vì thế nhận evidence gần giống nhau,
dù người dùng đang hỏi doanh thu, thanh toán, sản phẩm bán chậm hay nhập hàng.

Trang **Hướng dẫn Dashboard & AI** hiện nhận `DashboardGuidePageDto` từ
`DashboardQuestionCatalog`, thay cho danh sách hard-code trong Razor. Catalog có
đúng 16 câu canonical thuộc bốn nhóm; mỗi câu khai báo một focus duy nhất, widget
chính và answer style. Liên kết `aiQuestion` chỉ mở tab AI, điền và focus input,
không tự gửi request phân tích.

Pipeline mới:

```text
Câu hỏi
→ QuestionUnderstanding
→ AnswerFocus canonical hoặc DYNAMIC
→ DashboardDataPlan allowlist
→ chỉ tải primary/supporting widget
→ DashboardEvidencePack theo StaffScope
→ chart plan + deterministic fallback theo focus
→ Ollama giải thích (nếu output qua grounding validation)
```

Các mapping chính:

| AnswerFocus | Primary/supporting data |
|---|---|
| `OPERATIONAL_PRIORITIES` | `OperationalAlerts` |
| `REVENUE_COMPARISON`, `DAILY_REVENUE_STATISTICS` | `NetSalesTrend` |
| `STORE_UNDERPERFORMANCE` | `StoreRanking` |
| `REVENUE_DRIVER` | Net sales + order/AOV evidence liên quan |
| `ORDER_CANCELLATION_BY_STORE` | `OrderStatusSummary` |
| `PAYMENT_USAGE` | `PaymentMethodMix`, xếp theo `TotalTransactions` |
| top product/category | dataset riêng, xếp theo `TotalSold` |
| low volume | `LowVolumeProducts` |
| low margin | `LowMarginProducts`; chỉ kết luận khi COGS complete |
| shortage | `InventoryShortageRisk` |
| reorder | dữ liệu live từ `ReorderSuggestionService` |
| consumption | `IngredientConsumptionTrend` |
| supplier risk | quality + overdue + issue evidence |
| anomaly | `OperationalAlerts` |

Dynamic focus chỉ ánh xạ metric–entity–dimension vào registry widget có sẵn,
không sinh SQL động. Bốn style deterministic là `EXECUTIVE_DIAGNOSTIC`,
`TRANSACTION_RANKING_ANALYSIS`, `OPERATIONAL_ACTION_ANALYSIS` và
`RISK_INVESTIGATION_ANALYSIS`. Recommendation mặc định là `null`; chỉ câu hỏi
hành động hoặc reorder đủ dữ liệu mới được phép hiển thị.

Payload Ollama chỉ gồm câu hỏi, focus, filter, evidence pack và chart summary.
Output chứa SQL, prompt leakage, EvidenceId/entity/số ngoài evidence hoặc sai
schema bị loại và chuyển sang fallback đúng focus.

---

## M. RBAC_CAFECHAIN29_V2

`SeedAll.sql` không còn cấp quyền kiểu insert-only. Block
`RBAC_CAFECHAIN29_V2` chạy trong transaction với `XACT_ABORT`, resolve đúng tám
role bằng `Roles.Name`, upsert permission theo `Code`, thêm grant thiếu và thu
hồi grant dư trong managed catalog. `AccountPermissionOverrides` được snapshot
và so sánh `EXCEPT` hai chiều, nên seed không thay đổi override tài khoản.

Catalog quản lý có 165 dòng; bốn quyền delete mồ côi vẫn tồn tại inactive và
không có role grant: `Drink.Delete`, `Category.Delete`, `Size.Delete`,
`Topping.Delete`. Số grant sau reconcile:

| Role | Số quyền |
|---|---:|
| Chủ doanh nghiệp | 138 |
| Quản lý vùng | 53 |
| Quản lý chi nhánh | 84 |
| Nhân viên bán hàng | 6 |
| Kế toán/kho | 100 |
| Quản trị hệ thống | 161 |
| Khách hàng | 0 |
| Ca trưởng | 29 |

Quyền reorder của các role nghiệp vụ vẫn giữ nguyên: CDN/QLV/QLCN/KTK được
`ReorderSuggestion.View`; QLCN/KTK có `Restock.Create`. SystemAdmin nhận toàn bộ
161 permission active, được đi qua role gate Dashboard/reorder và có global scope
trên cửa hàng active. Override `Deny`, account inactive, CSRF, trạng thái chứng từ,
idempotency và audit vẫn được thực thi.

Role SystemAdmin đứng trên BusinessOwner trong quản trị nhân sự/RBAC. SystemAdmin
có thể quản lý BusinessOwner; chiều ngược lại bị từ chối. Ma trận quyền role
SystemAdmin là read-only và được reconcile từ seed, trong khi account override
`Deny` vẫn có thể giới hạn một tài khoản cụ thể.

Các thao tác lưu role, role-permission, scope và account override có
`RequestKey` ổn định khi retry. Backend kiểm payload hash, thực hiện
RequestDeduplication + mutation + AuditLog trong một transaction. Cùng key/cùng
payload replay an toàn; cùng key/payload khác trả
`IDEMPOTENCY_KEY_REUSED`.

---

## N. Kết quả nghiệm thu ngày 30/07/2026

| Lệnh/nhóm | Kết quả thật |
|---|---|
| Build ứng dụng | PASS — 0 error, 653 warning nền |
| Build test project | PASS — 0 error, 22 warning test (combined hiển thị 675 warning) |
| AI/reorder/RBAC targeted | PASS — 90/90 |
| Regression không phụ thuộc SQL | PASS — 1.558/1.558 |
| SeedAll + analytics contract trên SQL Server, chạy seed hai lần | PASS — 1/1 |
| Full SQL suite chạy song song | FAIL (environment) — nhiều fixture báo `Failed to generate SSPI context`; test SQL mục tiêu chạy riêng vẫn PASS |
| Browser E2E desktop/mobile | NOT RUN — runtime kiểm thử trả về danh sách browser rỗng; không có browser để thực hiện kiểm tra trực quan |

Kết quả build không được ghi là “0 warning”: repository hiện có warning nullable,
obsolete contract và một cảnh báo EF tồn tại ngoài phạm vi refactor này.
