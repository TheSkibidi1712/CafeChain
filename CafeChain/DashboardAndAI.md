# MASTER PROMPT – HOÀN THIỆN DASHBOARD, PHÂN QUYỀN VÀ AI CHO CAFECHAIN

Bạn đóng vai trò **Senior Software Architect + Senior Backend Engineer + Security/RBAC Engineer + Business Analyst** chịu trách nhiệm rà soát và hoàn thiện các nghiệp vụ Dashboard và AI trong dự án **CafeChain**.

Mục tiêu là sửa hệ thống trên cấu trúc hiện có, ưu tiên thay đổi ít phá vỡ kiến trúc, tái sử dụng RBAC, StaffScope, Dashboard, repository/service hiện tại và các permission đã có.

Không thiết kế lại toàn bộ dự án nếu không thật sự cần thiết.

---

# I. NGUYÊN TẮC BẮT BUỘC

## 1. Backend là nguồn authoritative

Mọi dữ liệu nghiệp vụ quan trọng phải do backend xác định.

Bao gồm:

* Permission.
* StaffScope.
* AllowedSections.
* AllowedCapabilities.
* Store hợp lệ.
* Dashboard metrics.
* NetSales.
* Operational Anomaly baseline.
* Operational Anomaly score.
* Supplier score.
* Supplier ranking.
* Confidence.
* MOQ.
* Package conversion.
* Total purchasing cost.
* Evidence cho AI.

Frontend không tự suy luận quyền.

AI không tự tính lại dữ liệu authoritative.

---

## 2. AI chỉ giải thích

AI chỉ được phép:

* Giải thích dữ liệu backend đã tính.
* Tóm tắt evidence.
* So sánh các lựa chọn đã được backend cung cấp.
* Đưa ra câu hỏi hoặc yếu tố người dùng nên kiểm tra.

AI không được:

* Tự tạo SQL.
* Truy vấn trực tiếp database.
* Tự sửa dữ liệu.
* Tự tạo chứng từ.
* Tự tạo hoặc duyệt PO.
* Tự chọn nhà cung cấp thay người dùng.
* Tự resolve anomaly.
* Kết luận gian lận.
* Kết luận cá nhân chịu trách nhiệm.
* Sử dụng dữ liệu ngoài permission.
* Sử dụng dữ liệu ngoài StaffScope.

Nếu AI service/Ollama timeout hoặc unavailable, nghiệp vụ chính vẫn phải hoạt động.

Backend deterministic vẫn trả được:

* metric,
* score,
* confidence,
* warning,
* evidence

mà không phụ thuộc AI.

---

# II. KIẾN TRÚC PHÂN QUYỀN CHUNG

Hệ thống chỉ giữ **một Admin Dashboard chung**.

Không tạo Dashboard riêng cho từng role.

Authorization runtime phải dựa trên:

```text
Dashboard Entry Permission
+
Module Permission
+
Action/Capability Permission
+
StaffScope
```

Role chỉ dùng để:

* nhóm quyền,
* seed quyền mặc định,
* quản trị quyền thuận tiện.

Không hard-code nghiệp vụ kiểu:

```text
if user.Role == ...
```

nếu permission hiện có có thể giải quyết vấn đề.

---

# III. QUYỀN VÀO DASHBOARD

Giữ:

```text
App.AdminDashboard
```

nhưng permission này **chỉ có ý nghĩa cho phép mở Admin Dashboard**.

Nó không đồng nghĩa người dùng được xem mọi section.

Quy trình authorization:

```text
User
→ App.AdminDashboard?
→ Calculate AllowedSections
→ Calculate AllowedCapabilities
→ Resolve StaffScope
→ Validate requested Store
→ Query data
```

Nếu không có `App.AdminDashboard`:

```text
403 Forbidden
```

Không load Dashboard data.

---

# IV. MA TRẬN ROLE MẶC ĐỊNH

Ma trận này dùng cho seed/default policy.

Runtime vẫn dựa trên permission thực tế.

## BusinessOwner

Được phép:

* Admin Dashboard.
* Toàn bộ chuỗi theo scope được gán.
* Dashboard operation.
* POS/WorkShift.
* Inventory.
* Purchasing.
* Product.
* Profitability.
* Staff.
* Shift.
* AI Dashboard.
* Operational Anomaly.
* Supplier Intelligence.

Có thể approve PO nếu có permission tương ứng.

---

## AreaManager

Được phép:

* Dashboard.
* Dữ liệu các Store thuộc vùng/cụm được giao.
* So sánh Store trong vùng.
* Inventory.
* Purchasing.
* POS/WorkShift.
* Product.
* Staff/Shift nếu permission có.
* Operational Anomaly trong scope.
* Supplier Intelligence trong scope.
* AI trên dữ liệu trong scope.

Không được xem Store/vùng ngoài scope.

---

## StoreManager

Được phép:

* Dashboard.
* Một hoặc nhiều Store được giao.
* Vận hành.
* POS/WorkShift.
* Inventory.
* Purchasing.
* Product.
* Staff/Shift nếu có permission.
* Operational Anomaly của Store.
* Supplier Intelligence của Store.
* AI của Store.

Không được xem:

* toàn chuỗi,
* vùng khác,
* Store khác,
* ranking toàn chuỗi nếu không có quyền.

---

## AccountantWarehouse

Được vào Dashboard nếu được cấp permission.

Chỉ được xem nghiệp vụ phục vụ:

* đối soát,
* doanh thu cần thiết,
* thanh toán,
* WorkShift cần đối soát,
* Inventory,
* Purchasing,
* Supplier,
* Cost,
* Profitability nếu có permission.

Không mặc định được xem:

* Staff,
* employee performance,
* full shift scheduling,
* dữ liệu nhân sự không phục vụ nghiệp vụ kế toán/kho.

StaffScope có thể là:

* Store,
* nhiều Store,
* Area,
* toàn chuỗi.

Không suy luận phạm vi chỉ từ một `StoreId`.

---

## SystemAdmin

SystemAdmin là role kỹ thuật.

Không mặc định đồng nghĩa BusinessOwner.

Không mặc định được xem:

* Revenue.
* Profit.
* Staff.
* Supplier commercial data.
* Dữ liệu kinh doanh nhạy cảm.

Chỉ được xem khi có:

```text
Permission cụ thể
+
StaffScope cụ thể
```

---

## ShiftSupervisor / SalesStaff

Không mặc định được vào Admin Dashboard.

Tiếp tục sử dụng các màn phù hợp như:

* StaffHub.
* POS.
* WorkShift.
* màn vận hành theo ca.

---

# V. ĐỒNG BỘ SEED QUYỀN

Rà soát toàn bộ:

```text
Scripts/SeedAll.sql
EF Configuration
EF Migration
```

Xác định **một nguồn seed/migration authoritative**.

Các nguồn còn lại phải đồng bộ với nguồn đó.

Yêu cầu:

* Không để cùng role nhưng môi trường khác nhau có quyền khác nhau.
* Seeder phải idempotent.
* Chạy nhiều lần không tạo duplicate.
* Không xóa permission tùy chỉnh của account khi seed lại.
* Không mở rộng quyền ngoài ma trận đã thống nhất.

Đảm bảo tối thiểu các role cần Dashboard có `App.AdminDashboard` theo policy hiện tại.

---

# VI. ALLOWED SECTIONS

Tạo một service tập trung, ví dụ:

```text
IDashboardAuthorizationService
```

Service chịu trách nhiệm tính:

```text
AllowedSections
AllowedCapabilities
AllowedStoreIds / Scope information
CanUseAi
```

Không duplicate logic permission ở nhiều Controller.

DTO Dashboard tối thiểu nên thể hiện:

```text
AllowedSections
AllowedCapabilities
Scope
CanUseAi
```

Scope nên có khả năng cung cấp:

* Scope type.
* Scope name.
* AllowedStoreIds.
* Có được chọn Store hay không.
* Có được chọn Area hay không.
* Có được xem aggregate nhiều Store hay không.

---

# VII. SECTION ↔ PERMISSION

Sử dụng permission hiện có trong hệ thống trước.

Không tự tạo permission mới nếu permission phù hợp đã tồn tại.

Mapping cơ bản:

```text
Dashboard
    App.AdminDashboard

POS / WorkShift
    POS.WorkShift.View

Inventory
    Inventory.View

Purchasing
    PurchaseAdvice.View
    hoặc PurchaseOrder.View

Product
    Drink.View

Profitability
    Profitability.View

Staff
    Staff.View

Shift
    Shift.View

Reorder Suggestion
    ReorderSuggestion.View
```

Riêng section Điều hành phải rà soát permission hiện có cho:

* Order.
* Revenue.
* Payment.
* Financial summary.

Không dùng `App.AdminDashboard` để thay thế permission nghiệp vụ bị thiếu.

---

# VIII. ACTION/CAPABILITY AUTHORIZATION

Không chỉ kiểm tra quyền xem section.

Phải phân biệt quyền xem và quyền thay đổi nghiệp vụ.

Ví dụ capability:

```text
Dashboard.View
Anomaly.View
Anomaly.Acknowledge
Anomaly.Resolve
Anomaly.Feedback

SupplierIntelligence.View
SupplierIntelligence.Compare
SupplierIntelligence.SelectSupplier

PurchaseOrder.Create
PurchaseOrder.Approve

AiDashboard.Use
```

Tên permission thực tế phải ưu tiên permission đã tồn tại trong dự án.

Nếu dự án chưa có capability tương ứng thì mới bổ sung permission mới theo naming convention hiện có.

Không cấp quyền ghi chỉ vì có quyền xem.

---

# IX. BACKEND AUTHORIZATION PIPELINE

Đối với mọi Dashboard API:

```text
1. Resolve CurrentUser.
2. Kiểm tra App.AdminDashboard.
3. Resolve permissions.
4. Calculate AllowedSections.
5. Calculate AllowedCapabilities.
6. Kiểm tra section/action yêu cầu.
7. Resolve StaffScope.
8. Validate StoreId/AreaId/filter.
9. Sau khi tất cả hợp lệ mới query repository.
```

Nếu section/action không được phép:

```text
403 Forbidden
```

Không:

* gọi repository trước rồi lọc response,
* trả dữ liệu một phần,
* log dữ liệu nhạy cảm vào response,
* phụ thuộc việc frontend đã ẩn button.

---

# X. STAFFSCOPE

StaffScope là nguồn giới hạn dữ liệu.

Phải bảo đảm:

* Không có scope => deny by default đối với dữ liệu cần scope.
* StoreId ngoài scope => 403.
* AreaId ngoài scope => 403.
* Query aggregate chỉ aggregate Store nằm trong scope.
* Không tự mở rộng scope vì account có nhiều role.
* Scope phải lấy từ assignment thật.

Nếu account có nhiều role:

* permission Allow được hợp theo cơ chế hiện có,
* account-level Deny nếu dự án hỗ trợ phải ưu tiên Allow,
* scope chỉ là tập phạm vi được gán hợp lệ,
* không suy luận scope từ tên role.

---

# XI. FRONTEND DASHBOARD

Không khai báo cố định tất cả tab cho mọi người dùng.

Frontend nhận:

```text
AllowedSections
AllowedCapabilities
Scope
```

từ backend.

Sau đó:

* Chỉ render section hợp lệ.
* Không render button/action không có capability.
* Không tạo URL/API request cho section không được phép.
* Tab mặc định = section đầu tiên người dùng được phép xem.
* Không có section nào => không mở Dashboard.

Bộ lọc:

## BusinessOwner

Có thể:

* toàn chuỗi,
* Area,
* Store,
* thời gian.

## AreaManager

Chỉ hiển thị:

* Area được giao,
* Store trong Area đó.

## StoreManager

Nếu một Store:

* ẩn hoặc lock Store filter.
* ẩn Area filter không cần thiết.

Nếu nhiều Store:

* chỉ hiển thị các Store được giao.

## AccountantWarehouse

Filter dựa hoàn toàn trên StaffScope.

Không tự mặc định toàn chuỗi.

---

# XII. WIDGET-LEVEL DATA PROTECTION

Một section có thể chứa nhiều widget với mức nhạy cảm khác nhau.

Do đó cần tránh tư duy:

```text
Có section => xem mọi widget
```

Nếu cần, backend phải xây:

```text
AllowedWidgets
```

hoặc capability tương đương.

Ví dụ AccountantWarehouse có thể được xem:

* Revenue summary phục vụ đối soát.
* Payment.
* Inventory.
* Purchasing.

nhưng không xem:

* Staff performance.
* Employee ranking.
* lịch nhân sự chi tiết.

Frontend chỉ là lớp hiển thị.

Backend API/widget vẫn phải authorization độc lập.

---

# XIII. AI DASHBOARD

AI authorization phải thỏa đồng thời:

```text
App/Admin access
+
Relevant Section Permission
+
Relevant Widget/Capability Permission
+
StaffScope
```

AI pipeline:

```text
User question
→ classify requested business domain
→ authorization check
→ scope check
→ build deterministic DataPlan
→ fetch authorized structured data
→ build evidence
→ AI explanation
```

Nếu domain không có quyền:

```text
Không tạo DataPlan
Không query repository
Không gửi dữ liệu sang AI
Trả access denied phù hợp
```

Không được query xong mới lọc dữ liệu trước khi gửi AI.

---

# XIV. OPERATIONAL ANOMALY

## Mục tiêu

Phát hiện các tín hiệu vận hành khác thường theo Store cho:

* Revenue.
* Order count.
* Inventory loss/adjustment.
* Cash discrepancy.
* Supplier incident.
* Product output decline.

Đây chỉ là **tín hiệu cần kiểm tra**, không phải kết luận sai phạm.

---

# XV. ANOMALY DATA PIPELINE

Luồng:

```text
Scheduled Worker
→ resolve enabled/pilot Stores
→ resolve BusinessDate
→ load real observations
→ validate sample
→ calculate baseline
→ calculate robust deviation
→ evaluate business thresholds
→ upsert anomaly
→ notify authorized users
→ user View/Acknowledge/Resolve/Feedback
→ AI explanation when requested
```

---

# XVI. BUSINESS DATE

Không dùng UTC date thuần làm ngày kinh doanh.

Sử dụng BusinessDate theo timezone cấu hình của hệ thống.

Đối với CafeChain hiện tại phải tương thích timezone Việt Nam.

Mọi metric cùng một ngày phải sử dụng cùng BusinessDate:

* Dashboard NetSales.
* Orders.
* WorkShift.
* Inventory.
* Anomaly.

Không để Worker chốt ngày khác Dashboard.

---

# XVII. MISSING DATA

Đây là quy tắc bắt buộc.

```text
Missing observation ≠ 0
```

Ngày không có dữ liệu không được biến thành:

```text
Revenue = 0
OrderCount = 0
...
```

trừ khi dữ liệu nghiệp vụ thực sự xác nhận giá trị bằng 0.

SampleCount phải đếm **observation thật**.

Nếu sample dưới mức tối thiểu:

```text
Không phát anomaly dựa trên baseline không đủ mẫu.
```

Có thể lưu trạng thái:

```text
INSUFFICIENT_BASELINE_DATA
```

hoặc bỏ qua detection tùy cấu trúc hiện tại.

---

# XVIII. ANOMALY BASELINE V1

Giữ baseline hiện tại nếu project đang implement:

```text
Analysis window: 28 days
Minimum observations: 14

Revenue minimum absolute difference:
500,000 VND

Cash discrepancy minimum absolute difference:
100,000 VND

Minimum relative difference:
25%

Robust threshold:
3.5
```

Severity:

```text
HIGH:
đạt ngưỡng anomaly

CRITICAL:
robust score >= 5
hoặc deviation >= 50%
```

Không thay đổi threshold tùy tiện trong lần sửa này.

Nếu cần configurable thì tạo config nhưng giữ default tương thích nghiệp vụ hiện tại.

---

# XIX. NET SALES CONSISTENCY

Revenue dùng cho anomaly phải thống nhất với định nghĩa NetSales của Dashboard.

Không tạo một công thức Revenue thứ hai.

Nếu Dashboard đã có authoritative calculation/service thì tái sử dụng.

Nếu chưa thể tái sử dụng trực tiếp thì phải đảm bảo cùng business rules:

* completed order,
* cancel/refund handling,
* discount,
* tax/service rules nếu có,
* BusinessDate.

---

# XX. ANOMALY STATE

State machine:

```text
OPEN
→ ACKNOWLEDGED
→ RESOLVED
```

Không cho state transition không hợp lệ.

Ví dụ:

```text
RESOLVED → ACKNOWLEDGED
```

không được xảy ra trừ khi nghiệp vụ có explicit reopen.

Nếu hỗ trợ reopen phải có action riêng và audit.

Mọi transition lưu:

* user,
* timestamp,
* previous state,
* new state,
* optional note.

---

# XXI. ANOMALY IDEMPOTENCY

Worker chạy nhiều lần không được tạo nhiều anomaly trùng nhau cho cùng detection.

Xây business key/idempotency phù hợp, ví dụ dựa trên:

```text
Store
BusinessDate
Metric
DetectionWindow/Version
```

hoặc cấu trúc phù hợp với model hiện tại.

Nếu cùng anomaly vẫn tồn tại:

* update dữ liệu nếu cần,
* không spam notification.

---

# XXII. ANOMALY FEEDBACK

UI phải hỗ trợ tối thiểu:

```text
Acknowledge
Resolve
Feedback
```

Feedback nên cho phép các trạng thái như:

```text
Useful
NotUseful
FalsePositive
```

hoặc mapping tương thích model hiện tại.

Feedback phục vụ pilot evaluation.

Không để feedback thay đổi dữ liệu nguồn.

---

# XXIII. ANOMALY AI EXPLANATION

AI chỉ nhận evidence đã authorization.

Prompt AI không được yêu cầu:

* tìm thủ phạm,
* kết luận gian lận,
* suy đoán vượt evidence.

Cách diễn đạt phù hợp:

```text
Phát hiện...
So với baseline...
Các dữ liệu nên kiểm tra thêm...
Chưa đủ cơ sở xác định nguyên nhân...
```

---

# XXIV. SUPPLIER INTELLIGENCE

## Mục tiêu

Hỗ trợ người dùng so sánh nhà cung cấp theo:

```text
Store
+
Ingredient
+
RequiredBaseQuantity
```

AI không tự chọn NCC.

Người dùng cuối cùng chịu trách nhiệm lựa chọn.

---

# XXV. SUPPLIER CANDIDATE

Candidate chỉ hợp lệ khi:

* Supplier active.
* IngredientSupplier active.
* SupplierStore active.
* Package price > 0.
* Package quantity > 0.
* Unit conversion hợp lệ.

Candidate không hợp lệ phải bị loại trước scoring.

Không đưa dữ liệu lỗi vào score rồi chỉ giảm confidence.

---

# XXVI. PACKAGE/MOQ

Backend tính:

```text
PackageBaseQuantity
=
convert package quantity into ingredient base unit
```

Sau đó:

```text
RequiredPackages
=
max(
    ceil(RequiredBaseQuantity / PackageBaseQuantity),
    MOQ
)
```

Chi phí:

```text
TotalCost
=
RequiredPackages × PackagePrice
```

Cần cung cấp thêm nếu có thể:

```text
PurchasedBaseQuantity
ExcessBaseQuantity
ExcessRatio
```

để người dùng thấy tác động của MOQ.

---

# XXVII. SUPPLIER PERFORMANCE WINDOW

Giữ v1:

```text
180 days
```

Metrics:

* On-time rate.
* Fill rate.
* Rejection rate.
* Issue rate.
* Average delay.

Không lấy dữ liệu ngoài Store/scope cần phân tích.

---

# XXVIII. SUPPLIER SCORE V1

Giữ trọng số business hiện có:

```text
Price / base unit      30%
On-time delivery       20%
Fill rate              20%
Quality                 20%
Lead time               10%
```

Backend chịu trách nhiệm deterministic scoring.

AI không tự thay weight.

Nếu sau này muốn thay weight phải qua business configuration/version.

Score phải lưu version nếu cần audit:

```text
ScoringVersion = v1
```

---

# XXIX. CONFIDENCE

Receipt confirmed:

```text
>= 20
    HIGH

5–19
    MEDIUM

< 5
    INSUFFICIENT_DATA
```

Confidence và Score là hai khái niệm khác nhau.

Không dùng score cao để che confidence thấp.

---

# XXX. RANKABILITY

Bổ sung khái niệm:

```text
Rankable
```

Candidate:

### HIGH / MEDIUM confidence

Có thể tham gia ranking nếu các metric bắt buộc hợp lệ.

### INSUFFICIENT_DATA

Có thể hiển thị để tham khảo nhưng:

* không được gọi là “best supplier”,
* không được gắn nhãn “recommended” chắc chắn,
* phải hiển thị cảnh báo dữ liệu hạn chế.

Nếu chỉ có một candidate hợp lệ:

```text
Không gọi đây là ranking cạnh tranh.
```

Hiển thị:

```text
Chỉ có một nhà cung cấp hợp lệ cho điều kiện hiện tại.
```

---

# XXXI. MISSING SUPPLIER DATA

Không được dùng:

```text
QualityScore = 100
```

chỉ vì không có receipt.

Missing data phải được biểu diễn là missing/unknown.

Nếu metric thiếu:

* không biến thành điểm tốt nhất,
* ghi DataQualityWarning,
* giảm confidence/rankability phù hợp.

Nếu ExpectedDelivery thiếu:

```text
ExpectedDeliveryMissing = true
```

hoặc warning tương đương.

Nếu lead time null và hệ thống hiện fallback 30 ngày:

* có thể giữ fallback để tránh breaking change,
* nhưng phải đánh dấu đây là fallback,
* không trình bày 30 ngày như dữ liệu đã xác nhận.

Ví dụ:

```text
LeadTimeDays = 30
LeadTimeSource = FALLBACK
```

---

# XXXII. SUPPLIER RESULT DTO

Kết quả so sánh nên cung cấp đủ dữ liệu để UI không tự tính lại.

Ví dụ:

```text
Supplier
PackagePrice
PackageBaseQuantity
RequiredPackages
PurchasedBaseQuantity
ExcessQuantity
TotalCost

PriceScore
OnTimeScore
FillScore
QualityScore
LeadTimeScore

OverallScore
Confidence
Rankable

ReceiptCount

DataQualityWarnings
ScoreVersion
```

Tên field cần phù hợp conventions hiện có.

---

# XXXIII. SUPPLIER UI

Supplier Intelligence phải xuất hiện trong luồng mua hàng thực tế.

Ưu tiên tích hợp tại:

```text
Purchase Advice UNDER_REVIEW
→ Supplier comparison
→ Select supplier
→ Create PO
→ approval flow
```

Có thể đồng thời có view-only summary trong Dashboard Purchasing/Supplier.

Không xây một màn AI độc lập hoàn toàn tách khỏi quy trình mua hàng nếu không cần thiết.

---

# XXXIV. SUPPLIER AUDIT SNAPSHOT

Khi người dùng dùng Supplier Intelligence để chọn NCC và tạo PO, lưu snapshot đủ để audit.

Snapshot nên gồm:

* Store.
* Ingredient.
* Required quantity.
* Candidate suppliers.
* Scores.
* Confidence.
* Price.
* Package/MOQ result.
* Warnings.
* Scoring version.
* Selected supplier.
* Selecting user.
* Timestamp.

Không cần lưu raw AI reasoning.

Mục tiêu là có thể trả lời:

```text
Tại thời điểm tạo PO,
hệ thống đã cung cấp thông tin gì cho người dùng?
```

---

# XXXV. SUPPLIER AI EXPLANATION

AI chỉ giải thích structured comparison.

Ví dụ:

```text
NCC A có độ ổn định cao hơn nhưng MOQ làm tăng lượng mua dư.

NCC B có tổng chi phí thấp hơn và sát nhu cầu hơn, nhưng dữ liệu giao đúng hạn kém hơn.

Quyết định cần cân nhắc ngày cần hàng, độ tin cậy dữ liệu và lượng tồn hiện tại.
```

Không dùng:

```text
Hãy chọn NCC A.
```

trừ khi nghiệp vụ sau này cho phép recommendation rõ ràng và được business phê duyệt.

Ở scope hiện tại AI không tự chọn.

---

# XXXVI. FEATURE FLAG

Không sử dụng duy nhất:

```text
Enabled = true/false
```

cho toàn hệ thống.

Mỗi feature cần hỗ trợ ít nhất:

```text
Enabled
ShadowMode
StoreAllowlist
```

Áp dụng cho:

```text
Operational Anomaly
Supplier Intelligence
```

Logic:

```text
Enabled = false
→ feature không hoạt động production

Enabled = true + ShadowMode = true
→ backend tính nhưng không tạo tác động/notification production không mong muốn

Enabled = true + StoreAllowlist
→ chỉ Store pilot chạy

Full rollout
→ chỉ sau exit gate
```

Không để bật một flag làm Worker quét ngay toàn bộ active Store.

---

# XXXVII. PILOT OPERATIONAL ANOMALY

Pilot một Store hoặc nhóm Store nhỏ.

Thu thập:

* Detection count.
* False positive.
* Useful feedback.
* NotUseful feedback.
* Missing-data frequency.
* Baseline sample quality.
* Notification volume.
* Processing errors.

Không mở toàn chuỗi cho đến khi số liệu pilot chấp nhận được.

---

# XXXVIII. PILOT SUPPLIER INTELLIGENCE

Theo dõi:

* Candidate count.
* Insufficient-data ratio.
* Missing ExpectedDelivery.
* Missing lead time.
* Number of one-candidate cases.
* User selected top-ranked candidate hay không.
* MOQ/excess effects.
* Scoring anomalies.
* Conversion errors.

Không đánh giá chất lượng chỉ bằng việc người dùng có chọn top score hay không.

---

# XXXIX. SECURITY RULES

Các case phải bị chặn tại backend:

## StoreId tampering

User sửa URL sang Store ngoài scope:

```text
403
```

## Hidden section API

Gọi API của tab bị ẩn:

```text
403
```

## Hidden action API

Có View nhưng gọi Resolve/Create/Approve:

```text
403
```

## AI unauthorized domain

Không tạo DataPlan.

Không query database.

Không gửi evidence.

## Supplier cross-scope

Không được compare NCC bằng dữ liệu Store ngoài StaffScope.

## Anomaly cross-scope

Không được View/Acknowledge/Resolve anomaly Store ngoài scope.

---

# XL. AUDIT

Các action quan trọng phải có audit phù hợp:

* Acknowledge anomaly.
* Resolve anomaly.
* Feedback anomaly.
* Supplier selected.
* PO created từ Supplier Intelligence.
* PO approved.

Audit không được phụ thuộc AI.

---

# XLI. LOGGING

Không ghi vào log:

* prompt chứa dữ liệu nhạy cảm quá mức,
* full employee records,
* secret,
* credential,
* unnecessary customer PII.

Log technical information cần thiết:

* UserId.
* StoreId.
* Feature.
* authorization result.
* request correlation id.
* error category.

---

# XLII. THỨ TỰ TRIỂN KHAI BẮT BUỘC

Thực hiện tuần tự.

## BƯỚC 1 – RÀ SOÁT SOURCE CODE

Tìm và xác định:

* Dashboard Controller.
* Dashboard View.
* Dashboard DTO/ViewModel.
* Permission definitions.
* Role seed.
* StaffScope service.
* `SeedAll.sql`.
* EF Configuration.
* EF Migration.
* POS/WorkShift permission.
* Inventory permission.
* Purchasing permission.
* Product permission.
* Profitability permission.
* Staff permission.
* Shift permission.
* AI Dashboard.
* Operational Anomaly worker/service/controller/view.
* Supplier scoring service/controller.
* Feature flags.
* Purchase Advice.
* Purchase Order flow.

Không sửa code trước khi hiểu dependency chính.

---

## BƯỚC 2 – CHỐT AUTHORIZATION DESIGN

Xác định:

```text
Dashboard Entry Permission
AllowedSections
AllowedCapabilities
StaffScope
```

Tái sử dụng permission hiện có tối đa.

Ghi rõ permission nào được map vào section/action nào.

---

## BƯỚC 3 – SỬA SEED/MIGRATION

Đồng bộ:

```text
SeedAll.sql
EF Configuration
EF Migration
```

Bảo đảm idempotent.

Không làm mất custom assignment.

---

## BƯỚC 4 – IMPLEMENT DASHBOARD AUTHORIZATION SERVICE

Tập trung logic:

* permissions,
* sections,
* capabilities,
* scope.

Không duplicate ở controller/view.

---

## BƯỚC 5 – SỬA DASHBOARD BACKEND

API phải kiểm tra:

```text
Section
→ Action
→ StaffScope
→ Store/Area
→ Query
```

Unauthorized => 403.

---

## BƯỚC 6 – SỬA DASHBOARD FRONTEND

Render:

* section,
* widget,
* button,
* filters

theo response authorization từ backend.

Không hard-code role.

---

## BƯỚC 7 – SỬA AI AUTHORIZATION

AI phải dùng cùng authorization service.

Không tạo một permission model thứ hai riêng cho AI.

Đảm bảo:

```text
Permission
+
Capability
+
StaffScope
+
Evidence
```

---

## BƯỚC 8 – SỬA OPERATIONAL ANOMALY DATA

Ưu tiên P0:

1. Missing date không thành 0.
2. BusinessDate/timezone đúng.
3. Sample count dữ liệu thật.
4. Revenue thống nhất NetSales.
5. Detection idempotent.
6. State transition hợp lệ.

---

## BƯỚC 9 – SỬA OPERATIONAL ANOMALY PERMISSION/UI

Bổ sung:

* View.
* Acknowledge.
* Resolve.
* Feedback.
* Store scope.
* Notification scope.

---

## BƯỚC 10 – SỬA ANOMALY FEATURE FLAG

Bổ sung:

```text
Enabled
ShadowMode
StoreAllowlist
```

Không quét toàn chuỗi ngoài ý muốn.

---

## BƯỚC 11 – SỬA SUPPLIER SCORING

Ưu tiên:

1. Candidate filtering.
2. Unit conversion.
3. MOQ.
4. Total cost.
5. Score deterministic.
6. Confidence.
7. Rankability.
8. Missing data.
9. ExpectedDelivery warnings.
10. LeadTime fallback warning.
11. One-candidate handling.

---

## BƯỚC 12 – IMPLEMENT SUPPLIER INTELLIGENCE UI

Tích hợp vào Purchase Advice/Purchase Order.

Không để feature chỉ tồn tại ở API.

---

## BƯỚC 13 – IMPLEMENT SUPPLIER AUDIT SNAPSHOT

Lưu structured snapshot khi lựa chọn NCC được sử dụng để tạo PO.

---

## BƯỚC 14 – SỬA SUPPLIER FEATURE FLAG

Bổ sung:

```text
Enabled
ShadowMode
StoreAllowlist
```

---

## BƯỚC 15 – CLEANUP VÀ CONSISTENCY REVIEW

Rà soát:

* duplicated authorization,
* role hard-code,
* inconsistent permission names,
* timezone,
* null handling,
* score rounding,
* status enums,
* API error format,
* frontend error handling,
* unnecessary dead code.

Không thêm feature ngoài scope.

---

# XLIII. NHỮNG PHẦN KHÔNG LÀM TRONG LẦN NÀY

Không mở rộng thành:

* Sales Forecast.
* POS Recommendation.
* AI scheduling.
* Multi-turn autonomous chatbot.
* Dynamic SQL.
* AI auto-PO.
* AI auto-approve PO.
* AI fraud detection/conclusion.
* AI tự sửa dữ liệu.

Giữ scope tập trung.

---

# XLIV. TEST – PHẢI LÀ BƯỚC CUỐI CÙNG

**Không viết hoặc chạy unit test, integration test, contract test, E2E test trong các bước triển khai phía trên.**

Chỉ sau khi hoàn tất:

* authorization,
* seed/migration,
* backend,
* frontend,
* AI,
* Operational Anomaly,
* Supplier Intelligence,
* feature flag,
* audit,
* cleanup

mới bắt đầu phase test.

Không quay sang phát triển feature mới sau khi phase test bắt đầu, trừ việc sửa lỗi test phát hiện.

---

# XLV. FINAL TEST PHASE

Sau khi toàn bộ implementation hoàn tất mới thực hiện các nhóm test sau.

## A. Authorization test

### BusinessOwner

Kiểm tra:

* vào Dashboard,
* toàn bộ section hợp lệ,
* Store toàn chuỗi trong scope,
* AI toàn scope,
* Anomaly,
* Supplier Intelligence.

### AreaManager

Kiểm tra:

* chỉ Store trong vùng,
* StoreId ngoài vùng => 403,
* aggregate không chứa Store ngoài vùng,
* AI không leak dữ liệu vùng khác,
* Anomaly đúng scope,
* Supplier Intelligence đúng scope.

### StoreManager

Kiểm tra:

* chỉ Store được giao,
* filter bị lock/ẩn hợp lý,
* Store khác => 403,
* AI đúng Store,
* anomaly đúng Store.

### AccountantWarehouse

Kiểm tra:

* vào Dashboard khi có quyền,
* Inventory/Purchasing hoạt động,
* financial widgets đúng quyền,
* Staff section/widget bị ẩn nếu không có Staff.View,
* direct Staff API => 403,
* AI không trả dữ liệu Staff ngoài quyền.

### SystemAdmin

Kiểm tra:

* không tự có business data,
* chỉ xem được khi explicit permission + scope.

### ShiftSupervisor / SalesStaff

Kiểm tra:

* không thấy Admin Dashboard nếu không có permission,
* direct Dashboard URL => 403,
* Dashboard API => 403,
* StaffHub/POS vẫn hoạt động bình thường.

---

## B. Multi-role test

Kiểm tra:

* permission aggregation.
* Deny precedence nếu hệ thống có account-level Deny.
* Scope không tự mở rộng.
* Không có privilege escalation.

---

## C. Seed/Migration test

Database mới:

* migration đầy đủ quyền.
* SeedAll đồng bộ.
* chạy seed nhiều lần không duplicate.
* Dev/Test/Production policy nhất quán.

---

## D. API security test

Thử:

* StoreId tampering.
* AreaId tampering.
* hidden section API.
* unauthorized action API.
* unauthorized AI domain.
* unauthorized anomaly.
* unauthorized supplier comparison.

Kỳ vọng:

```text
403
```

và repository nghiệp vụ không bị gọi trước authorization.

---

## E. Operational Anomaly deterministic test

Test:

* median/MAD.
* 28-day window.
* minimum 14 observations.
* missing dates.
* real zero.
* Revenue threshold.
* Cash discrepancy threshold.
* 25% deviation.
* robust score 3.5.
* CRITICAL >=5.
* CRITICAL >=50%.
* BusinessDate.
* NetSales consistency.
* idempotent worker.
* state transition.
* duplicate notification.
* StaffScope.

---

## F. Operational Anomaly UI test

Test:

* list.
* detail.
* acknowledge.
* resolve.
* feedback.
* permission.
* cross-store denial.
* notification deep link.

---

## G. Supplier deterministic test

Test:

* candidate filtering.
* package conversion.
* rounding/ceil.
* MOQ.
* total cost.
* excess quantity.
* score normalization.
* score weight.
* confidence HIGH.
* confidence MEDIUM.
* INSUFFICIENT_DATA.
* missing Quality data.
* missing ExpectedDelivery.
* null LeadTime.
* fallback warning.
* one candidate.
* multiple candidates.
* cross-store scope.
* deterministic output.

---

## H. Supplier workflow test

Test:

```text
PA UNDER_REVIEW
→ compare suppliers
→ select supplier
→ create PO
→ approval
```

Kiểm tra quyền riêng từng action.

---

## I. Supplier audit test

Đảm bảo snapshot lưu đúng:

* candidate,
* score,
* confidence,
* warnings,
* selected supplier,
* user,
* timestamp,
* scoring version.

---

## J. AI test

Test AI chỉ sau deterministic/backend tests.

Kiểm tra:

* Authorized evidence only.
* No cross-scope leakage.
* Không dynamic SQL.
* Không sửa dữ liệu.
* Không tự chọn NCC.
* Không kết luận gian lận.
* Không tự resolve anomaly.
* Timeout fallback.
* Ollama unavailable fallback.
* Invalid AI response fallback.

AI failure không được làm hỏng nghiệp vụ deterministic.

---

## K. Feature flag test

Test:

```text
OFF
ShadowMode
One Store allowlist
Multiple Store allowlist
Full rollout
```

Đảm bảo Worker không quét Store ngoài allowlist.

---

## L. Regression test

Cuối cùng mới chạy regression cho:

* Dashboard cũ.
* POS.
* WorkShift.
* Inventory.
* Purchase Advice.
* Purchase Order.
* StaffHub.
* RBAC.
* StaffScope.

Không để thay đổi Dashboard/AI phá các nghiệp vụ hiện hữu.

---

# XLVI. EXIT GATE

Chỉ coi feature đủ điều kiện bật khi:

```text
1. P0 business issues đã sửa.
2. Authorization hoàn tất.
3. StaffScope được kiểm soát backend.
4. Seed/Migration đồng bộ.
5. Deterministic tests pass.
6. Authorization/security tests pass.
7. AI fallback hoạt động.
8. Shadow mode chạy ổn.
9. Pilot Store đạt yêu cầu.
10. False positive / supplier data quality được đánh giá.
11. Audit hoạt động.
12. Regression pass.
```

Sau đó mới cân nhắc bật toàn chuỗi.

---

# XLVII. OUTPUT BẮT BUỘC CỦA QUÁ TRÌNH THỰC HIỆN

Khi thực hiện task, cuối cùng phải báo cáo theo format:

```text
1. Files inspected
2. Current architecture discovered
3. Business inconsistencies found
4. Authorization changes
5. Seed/Migration changes
6. Backend changes
7. Frontend changes
8. AI changes
9. Operational Anomaly changes
10. Supplier Intelligence changes
11. Feature flag changes
12. Audit changes
13. Tests added/run – PHẦN CUỐI
14. Test results
15. Remaining risks
16. Recommended rollout state
```

Trong mục `Remaining risks`, không che giấu:

* phần chưa xác minh,
* test không chạy được,
* dependency thiếu,
* migration chưa áp dụng,
* dữ liệu pilot chưa đủ.

Không tuyên bố production-ready nếu chưa qua exit gate.

---

# MỤC TIÊU CUỐI CÙNG

Sau khi hoàn tất, CafeChain phải đạt được:

```text
Một Dashboard chung
+
RBAC đúng
+
StaffScope đúng
+
Backend chống vượt quyền
+
UI theo đúng quyền
+
AI không vượt quyền
+
Operational Anomaly đáng tin cậy
+
Supplier Intelligence minh bạch
+
Feature rollout có kiểm soát
+
Audit đầy đủ
+
Test được thực hiện ở bước cuối
```

Ưu tiên cao nhất:

```text
Correctness
> Authorization/Security
> Data consistency
> Explainability
> Maintainability
> AI convenience
```

Không hy sinh correctness hoặc authorization để làm AI trông thông minh hơn.
