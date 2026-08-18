# PROMPT REFACTOR AI SMART IMPORT — DUPLICATE RECOVERY, SAME-SESSION DEPENDENCY, PREFLIGHT VÀ CONFIRM GATE

## 0. VAI TRÒ

Bạn là **Senior Backend Engineer + Software Architect** phụ trách tiếp tục refactor module **AI Smart Import CafeChain**.

Đây là dự án đã qua nhiều đợt refactor. **Không xây lại module từ đầu, không tạo pipeline import thứ hai và không refactor mù chỉ để code đẹp hơn.**

Trước khi sửa code, hãy đọc trực tiếp toàn bộ source hiện tại liên quan AI Smart Import, đặc biệt:

```text
AIImportController
AIImportService
IAIImportDocumentPipeline
AIImportExcelSourceParser
AIImportDocxSourceParser
AIImportPdfSourceParser
AIImportDocumentAiExtractor

AIImportCandidateValidator
AIImportReferenceResolver
AIImportBusinessKeys
AIImportEntityRegistry
AIImportPreviewValidator
AIImportResolutionEngine

AIImportAnalysisCoordinator
AIImportPreviewMutationCoordinator
AIImportConfirmCoordinator
AIImportSessionQuery
AIImportEntityCreator

ImportSession
ImportSourceDocument
ImportGroup
ImportItem
ImportAudit

AdminSupplierService
AdminCategoryService
AdminDrinkService
AdminSizeService
AdminIngredientService

AI Import View
JavaScript/TypeScript của Preview
Modal sửa dòng
UI ánh xạ cột
UI multi-file
Analyze/Reanalyze/Confirm/Cancel handlers
AIImportOptions
RBAC
EF model/migration
AI Import tests
```

Tài liệu nghiệp vụ hiện tại:

```text
AI_SMART_IMPORT_BUSINESS_RULES
```

là **baseline nghiệp vụ mới nhất**.

Các prompt refactor cũ chỉ dùng để hiểu lịch sử thiết kế và invariant đã chốt.

Nếu source hiện tại khác tài liệu nghiệp vụ, phải xác định rõ đó là bug implementation hay tài liệu chưa cập nhật.

**Các yêu cầu mới trong prompt này được ưu tiên cao hơn rule cũ nếu có xung đột.**

Đặc biệt, yêu cầu mới:

```text
File lỗi nghiêm trọng / sai định dạng / không an toàn
→ KHÔNG tạo ImportSession
```

sẽ thay thế behavior cũ:

```text
File lỗi
→ vẫn mở session
→ preview file hợp lệ
→ khóa Confirm
```

đối với các lỗi có thể phát hiện trong preflight trước phiên.

---

# 1. INVARIANT KHÔNG ĐƯỢC PHÁ

Giữ nguyên pipeline nghiệp vụ:

```text
File nguồn
→ Security / Format Preflight
→ Source Document
→ Group / Region
→ Candidate
→ Normalize
→ Validation
→ Reference
→ Duplicate / Conflict
→ Dependency
→ Preview
→ User Review / Edit / Skip
→ Confirm
→ Full Revalidation
→ Idempotency
→ Serializable Transaction
→ Existing CRUD Service
→ Database
```

Nguyên tắc:

```text
AI phân tích.
Parser/OCR trích xuất.
Backend quyết định.
Người dùng xác nhận.
Database chỉ thay đổi sau Confirm.
```

Không được:

```text
AI → DB
Parser → DB
OCR → DB
Preview → DB
PATCH item → Create entity
```

Chỉ hỗ trợ CREATE:

```text
Category
Drink
Size
Ingredient
Supplier
```

Không tự mở rộng:

```text
UPDATE
UPSERT
DELETE
Topping
Drink Price
Drink Image
Unit creation
ProductType creation
BOM/Recipe
Purchase/Order
```

Confirm tiếp tục dùng các CRUD service hiện hữu làm source of truth.

Giữ nguyên:

```text
PreviewVersion
expectedPreviewVersion
RBAC
session ownership
StoreId = 0
IRequestDeduplicationService
conditional state transition
Serializable transaction
Category → Drink dependency
Supplier warning token
audit/retention
```

Không được sửa migration cũ chỉ để tiện refactor. Nếu thực sự cần persistence mới thì tạo forward migration phù hợp; trước tiên ưu tiên giải pháp không cần thay schema.

**TEST luôn thực hiện cuối cùng. Không sửa test trước để ép code pass.**

---

# 2. PHASE 0 — SOURCE AUDIT TRƯỚC KHI SỬA

Trước khi implement, hãy kiểm tra source thực tế và trả báo cáo ngắn:

```text
Component
Responsibility hiện tại
Rule nghiệp vụ đang giữ
Bug có thể gây ra
Có cần sửa?
```

Phải truy nguyên chính xác root cause của các case sau, không sửa bằng workaround phía UI nếu lỗi nằm ở backend:

```text
1. Supplier soft duplicate không có lý do override.
2. Category cùng session không resolve đúng cho Drink.
3. Một validation issue bị render lặp nhiều lần.
4. Record duplicate đã sửa thành unique nhưng vẫn SKIPPED.
5. File invalid vẫn tạo/open ImportSession.
6. Confirm button không phản ánh blocker thật.
7. Summary counter không cập nhật đúng sau PATCH.
```

Sau khi xác định root cause mới bắt đầu implement.

---

# 3. P0 — SỬA SUPPLIER SOFT DUPLICATE VÀ LÝ DO TẠO MỚI

Case test chính:

```text
E06_all_entities_separate_sheets.xlsx
```

Hiện tại khi Supplier có dữ liệu tương tự Supplier đã tồn tại, UI hiển thị cảnh báo và checkbox xác nhận nhưng không có nơi nhập lý do tại sao vẫn muốn tạo Supplier mới.

Đây là bug.

## 3.1. Phân biệt hard duplicate và soft duplicate

Supplier:

```text
TaxCode
→ HARD DUPLICATE

Name
PrimaryPhone / Hotline
PrimaryContactPhone
PrimaryContactEmail
Address
→ SOFT DUPLICATE theo policy hiện tại
```

Hard duplicate TaxCode:

```text
ERROR
→ không cho override bằng lý do
→ user phải sửa dữ liệu hoặc bỏ qua dòng
```

Soft duplicate:

```text
WARNING
→ user được phép tiếp tục tạo
→ nhưng phải xác nhận đã kiểm tra
→ nhập lý do tạo mới
→ backend cấp/xác thực warning token theo policy AdminSupplierService
```

Không biến soft duplicate thành ERROR chỉ để dễ xử lý.

Không bypass `AdminSupplierService`.

## 3.2. Modal Supplier

Khi server phát hiện Supplier soft duplicate, modal phải hiển thị rõ:

```text
Cảnh báo

Bản ghi tương tự:
<Code/Name Supplier hiện có>

Các tín hiệu trùng:
- Tên
- Hotline
- Email
- Số điện thoại liên hệ
- Địa chỉ
...
chỉ hiển thị signal thực tế match

[ ] Tôi đã kiểm tra và vẫn muốn tạo nhà cung cấp mới

Lý do tạo mới *
[ textarea/input ]
```

Lý do phải được trim và validate theo policy/backend hiện có.

Không tự phát minh max length khác CRUD/service nếu source hiện tại chưa quy định.

Nếu business policy hiện tại chỉ yêu cầu non-empty thì dùng đúng policy đó.

Nút:

```text
Lưu và kiểm tra lại
```

không được giải quyết Supplier warning chỉ bằng checkbox phía client.

Server vẫn là source of truth.

## 3.3. Warning token

Supplier warning phải tiếp tục đảm bảo:

```text
actor
normalized payload
reason
expiry
warning token
```

Token phải gắn với payload hiện tại.

Nếu user sửa bất kỳ field nào có ảnh hưởng tới soft duplicate:

```text
Name
Phone
Email
Address
Contact...
```

thì warning acknowledgement/token cũ phải được coi là stale và duplicate phải được tính lại.

Không reuse token của payload cũ.

Nếu sau khi sửa không còn soft duplicate:

```text
warning biến mất
reason không còn required
old token invalid
```

## 3.4. Không tạo warning trùng

Một Supplier chỉ được render một warning logic cho cùng một matched Supplier/matched signal set.

Không được để:

```text
Cùng một warning
Cùng một field
Cùng một matched Supplier
```

xuất hiện lặp hai lần trong modal hoặc Preview.

---

# 4. P0 — SAME-SESSION CATEGORY → DRINK PHẢI RESOLVE ĐÚNG

Case chính:

```text
E06_all_entities_separate_sheets.xlsx
```

Workbook có nhiều entity.

Ví dụ:

```text
Category:
CategoryCode = CAT_TEA

Drink:
DrinkCode = DR_TEA
Category = CAT_TEA
ProductType = DRINK
```

Category `CAT_TEA` chưa có trong DB nhưng có Category candidate hợp lệ trong cùng ImportSession.

Drink phải resolve Category này thành:

```text
PENDING_IN_SESSION
```

hoặc state tương đương đang dùng trong source.

Không được báo:

```text
Danh mục không tồn tại
```

chỉ vì Category chưa được ghi DB.

## 4.1. Dependency phải chạy toàn session

Reference resolver không được chỉ query Database.

Phải có khả năng resolve:

```text
Existing active DB Category
hoặc
Pending Category candidate trong cùng session
```

Candidate có thể nằm:

```text
sheet khác
region khác
file khác
xuất hiện trước Drink
xuất hiện sau Drink
```

Upload/source order không được ảnh hưởng dependency.

Không fake CategoryId.

Confirm phải:

```text
Create Category
→ nhận ID thật từ CRUD service
→ map dependency
→ Create Drink
```

## 4.2. Category pending chỉ hợp lệ khi candidate cha có khả năng được import

Không coi Category pending là reference hợp lệ nếu Category candidate đang:

```text
ERROR
REVIEW_REQUIRED chưa xử lý
USER_SKIPPED
hard duplicate không giải quyết được
```

Nếu trạng thái Category thay đổi, tất cả Drink liên quan phải được scoped revalidation.

Ví dụ:

```text
Category CAT_TEA VALID
Drink → PENDING_IN_SESSION

user sửa Category CAT_TEA → CAT_COFFEE

→ revalidate Drink đang tham chiếu CAT_TEA
→ nếu không còn Category DB/session phù hợp
→ Drink trở thành lỗi reference
```

## 4.3. ProductType là dependency riêng

Không đánh đồng:

```text
Category
và
ProductType
```

`ProductType` vẫn phải resolve vào ProductType active đã có trong hệ thống.

Smart Import không tạo ProductType.

Nếu:

```text
Category CAT_TEA
```

resolve thành công trong cùng session nhưng:

```text
ProductType = DRINK
```

không tồn tại/không active trong DB, thì chỉ báo lỗi ProductType.

UI phải hiển thị chính xác:

```text
Loại sản phẩm "DRINK" không tồn tại hoặc không hoạt động.
```

Không được báo lỗi Category nếu Category thực tế đã resolve.

## 4.4. Deduplicate ValidationIssue

Ảnh hiện tại có trường hợp cùng lỗi:

```text
Loại sản phẩm không tồn tại.
```

xuất hiện hai lần.

Phải kiểm tra toàn pipeline xem issue bị thêm ở:

```text
Candidate validation
Reference resolver
Preview validator
Resolution engine
UI aggregation
```

hay nhiều layer cùng append một lỗi.

Không sửa bằng cách chỉ `Distinct()` message string ở JavaScript.

Phải deduplicate ở domain/result aggregation dựa trên identity phù hợp, ví dụ:

```text
Code
Field
Target/reference
SourceLocator hoặc semantic identity cần thiết
```

Frontend vẫn có thể defensive-deduplicate nhưng backend phải trả contract sạch.

Một lỗi nghiệp vụ logic chỉ hiển thị một lần.

---

# 5. P0 — SỬA BUG EDIT DUPLICATE NHƯNG VẪN SKIPPED

Case ảnh 3, 4, 5, 6:

```text
Candidate ban đầu trùng bản ghi hiện có
→ hệ thống đánh dấu bỏ qua

User mở "Sửa dòng"
→ đổi CategoryCode/Name thành dữ liệu mới không còn trùng
→ Lưu và kiểm tra lại

Hiện tại:
→ candidate vẫn SKIPPED
→ duplicate warning/error cũ vẫn còn
→ Confirm chỉ tạo được 1 record
```

Behavior này sai.

## 5.1. Phân biệt USER SKIP và SYSTEM AUTO-SKIP

Không được coi mọi `SKIPPED` là trạng thái vĩnh viễn.

Phải phân biệt về mặt domain:

```text
USER_SKIP
SYSTEM_DUPLICATE_SKIP
```

Tên field/model cụ thể phải theo convention source hiện tại; không bắt buộc tạo DB column nếu không cần.

`USER_SKIP`:

```text
do user chủ động bấm "Bỏ qua dòng"
→ giữ nguyên qua revalidation
→ chỉ thay đổi khi user chủ động khôi phục/unskip nếu UI hỗ trợ
```

`SYSTEM_DUPLICATE_SKIP`:

```text
do engine tự xác định same key + same payload
→ derived state
→ phải tính lại khi payload/business key thay đổi
```

Không để auto-skip cũ khóa candidate vĩnh viễn.

## 5.2. Item PATCH phải revalidation theo business key cũ và mới

Khi user bấm:

```text
Lưu và kiểm tra lại
```

flow phải là:

```text
expectedPreviewVersion check
→ capture old normalized payload
→ capture old business key
→ apply new normalized values
→ calculate new business key
→ invalidate derived duplicate state cũ
→ invalidate stale validation issues
→ invalidate stale manual confirmation nếu payload liên quan thay đổi
→ invalidate Supplier token nếu payload Supplier thay đổi
→ revalidate edited item
→ revalidate cohort của old business key
→ revalidate cohort của new business key
→ revalidate dependent Drink/Category nếu liên quan
→ resolve status lại từ đầu
→ PreviewVersion++
→ refresh Preview
```

Nếu dữ liệu mới không còn duplicate và không còn blocker:

```text
SYSTEM_DUPLICATE_SKIP
→ VALID
```

hoặc:

```text
WARNING
REVIEW_REQUIRED
```

theo validation thật hiện tại.

Không giữ `SKIPPED` chỉ vì trước PATCH item từng trùng.

## 5.3. Hard duplicate phải được tính lại toàn bộ

Ví dụ Category:

```text
CategoryCode = CAT_TEA_2
Name = Trà trái cây
```

trùng DB.

User sửa thành:

```text
CategoryCode = CAT_TEA_221
Name = Trà trái cây mới
```

Nếu cả Code và Name đều không còn duplicate:

```text
duplicate issue phải biến mất
candidate không còn auto-skip
candidate phải trở thành importable
```

Nếu Code mới unique nhưng Name vẫn trùng:

```text
vẫn báo đúng duplicate Name
```

Không cache kết quả duplicate cũ theo ItemId.

## 5.4. UI sau Save

Sau PATCH thành công phải sử dụng response mới từ server làm source of truth.

Phải cập nhật:

```text
Status
Issues
Warnings
Normalized values
Counters:
Tổng dòng
Hợp lệ
Cảnh báo
Lỗi
Cần xem lại
Bỏ qua
PreviewVersion
CanConfirm
```

Không chỉ thay text trong modal rồi giữ state Preview cũ.

Nếu sau Save vẫn còn lỗi:

```text
giữ modal mở
focus lỗi đầu tiên
render lỗi mới nhất từ server
```

Nếu đã hợp lệ:

```text
có thể đóng modal theo UX hiện tại
refresh dòng và summary
```

Không tự gửi `action=SKIP` khi user chỉ chỉnh sửa field.

Nút **Bỏ qua dòng** phải là action riêng biệt.

---

# 6. P0 — FILE INVALID PHẢI BỊ CHẶN TRƯỚC KHI TẠO SESSION

Đây là thay đổi nghiệp vụ mới và **override behavior multi-file cũ**.

Case:

```text
E42_fake_xlsx_contains_pdf.xlsx
```

Hiện tại:

```text
Analyze
→ tạo/open session
→ UI hiện "Tệp không phải gói OpenXML .xlsx hợp lệ."
→ session rỗng vẫn tồn tại
```

Sai.

Case:

```text
E40_suspicious_compression_ratio.xlsx
```

Hiện tại:

```text
Analyze
→ session đã được tạo
→ sau đó mới báo "Tệp Excel có tỷ lệ nén không an toàn."
```

Sai.

## 6.1. Thêm Pre-Session Preflight

Analyze phải chia thành hai tầng:

```text
PHASE A — PREFLIGHT KHÔNG SIDE EFFECT
PHASE B — CREATE SESSION + ANALYZE
```

PHASE A phải hoàn thành trước khi persist ImportSession.

Phải kiểm tra tối thiểu theo format hiện tại:

```text
extension
Content-Type
file signature
file size
aggregate batch size
OpenXML package hợp lệ
XLSX thật
DOCX thật
PDF signature
password/encryption
macro/active content
OLE/embedded binary
external relationship nguy hiểm
PDF JavaScript/Launch/URI/embedded file
expanded size
compression ratio
resource count
các AIImportOptions limits liên quan
minimal parseability
OCR capability nếu source bắt buộc OCR cho request hiện tại
```

Không chạy AI trước security guard.

Không tạo DB record trong preflight.

## 6.2. Fatal preflight failure

Nếu một file fail fatal preflight:

```text
→ dừng Analyze
→ trả typed error
→ KHÔNG tạo ImportSession
→ KHÔNG tạo ImportSourceDocument
→ KHÔNG tạo ImportGroup
→ KHÔNG tạo ImportItem
→ KHÔNG tạo Preview
→ KHÔNG chuyển UI sang màn hình phiên
```

UI chỉ hiển thị lỗi tại khu vực file upload.

Ví dụ:

```text
E42_fake_xlsx_contains_pdf.xlsx
Tệp không phải gói OpenXML .xlsx hợp lệ.

E40_suspicious_compression_ratio.xlsx
Tệp Excel có tỷ lệ nén không an toàn.
```

Giữ tên error code hiện tại nếu backend đã có typed code.

Không tạo code mới chỉ để đổi wording.

## 6.3. Multi-file phải preflight atomic

Nếu upload:

```text
File A hợp lệ
File B hợp lệ
File C hỏng
```

thì:

```text
Preflight A
Preflight B
Preflight C → FAIL

→ không tạo ImportSession cho cả batch
```

UI phải chỉ rõ:

```text
File C
ErrorCode
Message
```

User có thể bỏ File C khỏi danh sách và Analyze lại.

Không silently drop File C.

Không tạo session chứa A/B rồi giấu C.

Đây là behavior mới thay thế rule multi-file cũ đối với **fatal source error**.

## 6.4. Phân biệt file error với business validation

Các lỗi sau là source/preflight blocker:

```text
fake file
invalid OpenXML
corrupt file
password/encrypted
unsafe compression
unsupported active content
security guard fail
resource bomb
unsupported file format
provider bắt buộc nhưng không khả dụng để phân tích source hiện tại
```

Các lỗi dữ liệu như:

```text
missing required field
duplicate record
reference không tồn tại
mapping ambiguity
low confidence
Supplier soft duplicate
unknown column
```

không phải file-corruption error.

Các lỗi nghiệp vụ này vẫn cần tạo Preview để user sửa/xác nhận.

---

# 7. P0 — CENTRAL CONFIRM GATE

Không để frontend tự suy đoán có được Confirm hay không chỉ dựa vào counters.

Backend phải có một nguồn quyết định duy nhất, ví dụ:

```text
CanConfirm
ConfirmBlockers[]
```

Tên implementation theo convention source hiện tại.

Session query/Preview response nên cung cấp đủ dữ liệu để UI render chính xác.

## 7.1. Confirm chỉ được enable khi

Logic tổng quát:

```text
Session == READY_TO_PREVIEW

AND PreviewVersion hiện tại hợp lệ

AND có ít nhất 1 item thực sự sẽ được Create

AND không có fatal SourceDocument

AND không có ERROR

AND không có REVIEW_REQUIRED chưa xử lý

AND không còn unresolved mapping/conflict

AND mọi WARNING yêu cầu acknowledgement đã được xác nhận

AND Supplier warning token/reason còn hợp lệ

AND dependency graph resolve được

AND actor có AIImport.Confirm

AND actor có *.Create của tất cả entity sẽ được tạo

AND session chưa Cancelled/Expired/Failed/Importing/Completed
```

Nếu tất cả item đều `SKIPPED`:

```text
CanConfirm = false
Reason = Không có dữ liệu hợp lệ để nhập.
```

## 7.2. SKIPPED không mặc định là blocker

Một item được user/system bỏ qua sẽ không được Create.

Nếu session vẫn còn item importable khác thì `SKIPPED` tự nó không chặn Confirm.

Nhưng hệ thống phải chắc chắn skip đó hợp lệ và không phá dependency.

Ví dụ:

```text
Category bị skip
Drink phụ thuộc Category đó
```

thì Drink phải được revalidate.

Không được Confirm Drink với dependency giả.

## 7.3. File/source blocker

Nếu vì backward compatibility hoặc session cũ vẫn tồn tại `ImportSourceDocument` ở trạng thái:

```text
FAILED
REJECTED
UNSUPPORTED
FATAL_ERROR
```

hoặc tương đương:

```text
CanConfirm = false
```

Frontend khóa nút:

```text
Xác nhận nhập
```

và hiển thị nguyên nhân.

Backend vẫn phải từ chối Confirm nếu client gọi API thủ công.

Không dựa vào HTML `disabled`.

## 7.4. Warning không phải blocker vĩnh viễn

Ví dụ sheet ẩn:

```text
Trang tính "Ẩn" đang ẩn nên không được nhập.
```

Theo nghiệp vụ hiện tại, sheet/dòng/cột ẩn được bỏ qua.

Do đó không được coi toàn workbook là file hỏng chỉ vì có sheet ẩn.

Nếu workbook còn candidate hợp lệ:

```text
sheet ẩn → bỏ qua
sheet hiển thị → tiếp tục xử lý
```

Confirm chỉ bị khóa nếu warning đó theo business rule hiện tại thực sự yêu cầu acknowledgement/blocker.

Không biến tất cả warning thành ERROR.

---

# 8. P1 — CHUẨN HÓA MODAL SỬA DÒNG

Kiểm tra lại modal cho cả năm entity.

## 8.1. Server validation là source of truth

ValidationResult tiếp tục dùng contract hiện tại:

```text
Code
Message
Field?
Severity
SourceLocator?
Metadata?
```

Field error:

```text
gắn đúng input
input invalid
message ngay gần field
```

Row error:

```text
hiển thị vùng tổng hợp đầu modal
không gắn giả vào field
```

Warning:

```text
hiển thị riêng
không dùng style ERROR
```

## 8.2. Không xóa lỗi server quá sớm

Khi mở modal:

```text
server field errors phải còn hiển thị
```

Client validation lần đầu không được xóa chúng.

Chỉ khi user thực sự thay đổi field tương ứng mới có thể clear visual state cũ.

Sau PATCH:

```text
response server mới
→ thay thế validation state cũ
```

## 8.3. Supplier

Modal Supplier phải hỗ trợ:

```text
matched Supplier
matched signals
warning checkbox
reason override
warning token state
```

Nếu hard duplicate TaxCode:

```text
không hiện UI override giả
```

## 8.4. Modal layout

Giữ:

```text
Header cố định
Footer/action cố định
Body cuộn
```

Trong body gồm:

```text
Field nghiệp vụ
Field error
Row issue
Warning
Dữ liệu nguồn bổ sung
Evidence/Source trace
```

Không để button Save/Skip bị trôi khỏi màn hình.

SweetAlert2 phải nằm trên native dialog nếu được gọi trong lúc modal đang mở.

## 8.5. Save state

Khi đang Save:

```text
disable Save
disable Skip nếu gây race
show loading
không double click
```

Stale:

```text
409 PREVIEW_ĐÃ_THAY_ĐỔI
→ dừng loading
→ refresh Preview
→ không giữ state giả phía client
→ thông báo dữ liệu đã thay đổi
```

---

# 9. P1 — ISSUE AGGREGATION VÀ COUNTER

Kiểm tra lại cách tính:

```text
Tổng dòng
Hợp lệ
Cảnh báo
Lỗi
Cần xem lại
Bỏ qua
```

Counter phải được tính từ Preview state mới nhất của backend.

Không increment/decrement thủ công ở JavaScript theo action vừa bấm nếu server đã trả snapshot mới.

Sau:

```text
Edit item
Skip item
Unskip nếu có
Remap group
Acknowledge warning
Supplier override
Manual review
Reanalyze
```

phải refresh state/counters.

Một Item chỉ thuộc đúng trạng thái cuối cùng tại một thời điểm.

Không được vừa:

```text
SKIPPED
```

lại vừa được tính:

```text
VALID
```

trong summary.

---

# 10. P1 — SCOPED REVALIDATION

Không revalidate toàn session cho mọi keystroke/PATCH nếu không cần.

Item PATCH:

```text
edited item
old business-key cohort
new business-key cohort
affected reference/dependencies
Drink liên quan tới Category cũ/mới
```

Group PATCH:

```text
affected group
old/new entity cohort nếu mapping đổi
affected dependencies
```

Analyze/Reanalyze/Confirm:

```text
full-session validation
```

Confirm bắt buộc full revalidation DB-sensitive dù Preview đang xanh.

---

# 11. P1 — CONFIRM SERVER FLOW KHÔNG ĐƯỢC GIẢM BẢO VỆ

Giữ flow:

```text
Authorize
→ Session ownership
→ Session state
→ PreviewVersion
→ CanConfirm / no blockers
→ Begin idempotency
→ Conditional claim IMPORTING
→ Full DB-sensitive revalidation
→ Reference/dependency plan
→ Supplier token validation
→ Serializable transaction
→ Existing CRUD Create
→ result/audit
→ MarkSuccessAsync
→ Commit
```

Category luôn chạy trước Drink trong execution plan.

Không phụ thuộc:

```text
sheet order
file order
candidate order
```

Nếu:

```text
Category Create success
Drink Create fail
```

thì:

```text
rollback Category
rollback toàn session
```

Không partial success.

---

# 12. P2 — ERROR UX

Các lỗi source/file phải xuất hiện gần file upload, không cần người dùng mở một session rỗng để đọc lỗi.

Response fatal preflight phải trả dữ liệu đủ để UI hiển thị:

```text
FileName
Code
Message
Severity/Fatal
safe metadata nếu cần
```

Không trả:

```text
raw document
OCR full text
stack trace
SqlException
filesystem temp path
API key
secret
warning token
```

Các thông báo UI dùng tiếng Việt rõ ràng.

Technical error code vẫn giữ ổn định để frontend/test dùng.

---

# 13. CẬP NHẬT TÀI LIỆU NGHIỆP VỤ SAU KHI IMPLEMENT

Sau khi code chạy đúng, cập nhật `AI_SMART_IMPORT_BUSINESS_RULES` để phản ánh behavior mới.

Đặc biệt phải sửa rule multi-file cũ:

```text
File lỗi không bị silent-drop.
Phiên vẫn hiển thị preview file hợp lệ nhưng Confirm bị khóa...
```

thành rule mới có hai tầng:

```text
Fatal preflight/source error
→ reject toàn Analyze request
→ không tạo ImportSession.

Business-level candidate error sau khi preflight pass
→ tạo Preview
→ cho user sửa/review
→ Confirm gate quyết định.
```

Đồng thời bổ sung rõ:

```text
SYSTEM_DUPLICATE_SKIP phải recompute khi payload/business key thay đổi.

USER_SKIP giữ nguyên cho tới khi user thay đổi action.

Same-session Category là reference hợp lệ cho Drink nếu Category candidate importable.

Supplier soft duplicate phải có reason + token theo AdminSupplierService.

Confirm eligibility do backend quyết định tập trung.
```

Không để tài liệu và source tiếp tục lệch nhau.

---

# 14. DEFINITION OF DONE

* [ ] `E06_all_entities_separate_sheets.xlsx`: Supplier soft duplicate hiển thị matched data, checkbox và ô lý do tạo mới.
* [ ] Supplier soft duplicate không bypass server warning token.
* [ ] Supplier TaxCode hard duplicate không thể override bằng reason.
* [ ] Category trong cùng session resolve cho Drink bằng pending dependency.
* [ ] Category có thể nằm ở sheet/file khác và source order không ảnh hưởng.
* [ ] ProductType vẫn resolve độc lập từ DB active.
* [ ] Cùng một ProductType/reference error không render lặp hai lần.
* [ ] Candidate duplicate được sửa sang Code/Name unique không còn bị giữ `SKIPPED`.
* [ ] Revalidation chạy cho business key cũ và mới.
* [ ] `USER_SKIP` không bị tự mở lại.
* [ ] `SYSTEM_DUPLICATE_SKIP` được tính lại sau PATCH.
* [ ] PreviewVersion tăng đúng sau mutation.
* [ ] Summary counters cập nhật từ response server.
* [ ] `E42_fake_xlsx_contains_pdf.xlsx` trả lỗi ngay và không tạo ImportSession.
* [ ] `E40_suspicious_compression_ratio.xlsx` trả lỗi ngay và không tạo ImportSession.
* [ ] Multi-file có một fatal file → không tạo session cho cả batch.
* [ ] Fatal Analyze failure không để lại session rỗng/history giả.
* [ ] `E23_hidden_sheet.xlsx`: sheet ẩn được bỏ qua theo rule hiện tại; không tự coi cả workbook là file hỏng nếu còn dữ liệu hợp lệ.
* [ ] Confirm button disabled khi server `CanConfirm=false`.
* [ ] Gọi Confirm API trực tiếp vẫn bị backend chặn nếu có blocker.
* [ ] ERROR chặn Confirm.
* [ ] REVIEW_REQUIRED chưa xử lý chặn Confirm.
* [ ] Supplier warning chưa có acknowledgement/reason/token hợp lệ chặn Confirm.
* [ ] Unresolved mapping/reference/conflict chặn Confirm.
* [ ] Chỉ còn SKIPPED và không có record để Create → Confirm disabled.
* [ ] Có SKIPPED nhưng còn record hợp lệ khác → SKIPPED không tự chặn Confirm.
* [ ] Modal giữ field/row errors chính xác.
* [ ] Modal Supplier có reason UX đúng.
* [ ] Không có duplicate ValidationIssue.
* [ ] Category → Drink transaction vẫn atomic.
* [ ] Idempotency, concurrency, RBAC, ownership và Serializable transaction không regression.
* [ ] Không sửa migration cũ nếu không thực sự cần.
* [ ] `AI_SMART_IMPORT_BUSINESS_RULES` được cập nhật theo behavior mới.
* [ ] Test chỉ được sửa/thêm sau khi implementation hoàn tất.

---

# 15. TEST — CHỈ LÀM SAU CÙNG

Sau khi hoàn thiện toàn bộ nghiệp vụ và code, mới viết/chỉnh test.

Bắt buộc có regression test cho:

```text
SUPPLIER

soft duplicate
→ WARNING
→ reason required
→ acknowledgement
→ valid server warning token
→ Confirm success

payload changed
→ old warning token invalid

TaxCode duplicate
→ ERROR
→ reason không bypass
```

```text
SAME-SESSION CATEGORY

Category CAT_TEA + Drink Category=CAT_TEA
→ PENDING_IN_SESSION
→ no Category reference error

Category nằm sau Drink
→ vẫn resolve

Category nằm file khác
→ vẫn resolve

Category bị edit/skip/error
→ dependent Drink revalidate

ProductType missing
→ đúng 1 ProductType error
```

```text
DUPLICATE EDIT

candidate A unique
candidate B duplicate → SYSTEM SKIPPED

edit B business key thành unique
→ save
→ duplicate issue removed
→ B no longer SKIPPED
→ PreviewVersion++
→ Confirm creates A + B
```

```text
USER SKIP

user chủ động skip item
→ PATCH/revalidation không tự unskip
```

```text
PREFLIGHT

E42_fake_xlsx_contains_pdf.xlsx
→ typed invalid OpenXML error
→ ImportSession count không tăng

E40_suspicious_compression_ratio.xlsx
→ unsafe compression error
→ ImportSession count không tăng

multi-file:
A valid + B invalid
→ Analyze fail
→ không session
→ không SourceDocument orphan
```

```text
CONFIRM GATE

ERROR → disabled
REVIEW_REQUIRED → disabled
unresolved mapping → disabled
Supplier warning unresolved → disabled
fatal source → disabled
all SKIPPED → disabled
VALID records + harmless ignored source → allowed
direct Confirm API với blocker → rejected
```

```text
E23_hidden_sheet.xlsx

hidden sheet ignored
visible valid row retained
không tạo candidate từ hidden sheet
không biến hidden-sheet warning thành fatal file error
```

Cuối cùng chạy build/test theo command convention hiện tại của repository.

Không dùng production database cho integration test.

Nếu SQL integration environment không khả dụng:

```text
NOT VERIFIED
```

không được báo PASS giả.

---

# 16. BÁO CÁO SAU KHI HOÀN THÀNH

Sau khi hoàn tất, trả báo cáo gồm:

```text
A. Root cause từng bug
B. Business rule đã thay đổi
C. Backend component đã sửa
D. Frontend/View/JS đã sửa
E. API contract thay đổi hay giữ nguyên
F. Database/Migration có thay đổi không
G. AI_SMART_IMPORT_BUSINESS_RULES đã cập nhật gì
H. File source đã sửa/tạo
I. Known limitations còn lại
J. Build result
K. Test result
```

Không chỉ trả `"đã sửa xong"`.

Phải giải thích rõ vì sao từng bug xảy ra và component nào chịu trách nhiệm.

---

# CÂU CHỐT

Đợt refactor này ưu tiên **correctness, data integrity và UX state consistency**, không thêm AI capability mới.

Bốn vấn đề phải được xử lý triệt để là:

```text
Supplier soft duplicate
→ có reason + server warning token đúng.

Same-session Category
→ Drink resolve đúng dependency, không báo lỗi giả.

Duplicate candidate sau khi edit
→ derived skip/duplicate state phải được tính lại.

Invalid file
→ fail ở preflight và không tạo/open ImportSession.
```

Sau đó chuẩn hóa `CanConfirm`, modal, issue aggregation và counters để UI luôn phản ánh đúng state backend.

Không fix bằng workaround JavaScript nếu root cause nằm trong validation/reference/duplicate/state engine.
