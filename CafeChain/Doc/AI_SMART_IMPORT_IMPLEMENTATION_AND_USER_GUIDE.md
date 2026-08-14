# AI Smart Import CafeChain — hướng dẫn triển khai và sử dụng

## 1. Capability hiện tại

AI Smart Import hỗ trợ:

- Excel `.xlsx`;
- Word `.docx` OpenXML;
- PDF có lớp text;
- deterministic extraction cho Excel, DOCX table/key-value và PDF table/key-value;
- Ollama structured fallback có chunk, whitelist và evidence validation;
- Preview/PATCH/reanalyze/history trên cùng `/api/ai-import`;
- modal sửa dòng giữ lỗi server đúng field, hiển thị lỗi cấp dòng/warning và dùng vùng cuộn responsive có footer luôn truy cập được;
- xác nhận thủ công candidate confidence thấp bằng **Lưu và kiểm tra lại** mà không bỏ qua schema validation;
- Confirm nguyên tử cho Category, Drink, Size, Ingredient và Supplier.

Không hỗ trợ `.doc`, `.docm`, PDF scan/image-only, OCR/Vision, file có mật khẩu, active content, `UPDATE`, `UPSERT` hoặc `DELETE`.

## 2. Kiến trúc module

Interface chính `IAIImportDocumentPipeline` che toàn bộ việc chọn format, parser và AI fallback khỏi `AIImportService`.

Các adapter nguồn:

- `AIImportExcelSourceParser`: bọc `IAIImportExcelParser` và `IAIImportRegionAnalyzer` cũ để giữ backward compatibility.
- `AIImportDocxSourceParser`: guard ZIP/OpenXML, table và paragraph key-value.
- `AIImportPdfSourceParser`: PdfPig word extraction, line/cell/table theo tọa độ và key-value.
- `AIImportDocumentAiExtractor`: chunk text, gọi `IOllamaClient.ChatStructuredAsync`, xác minh whitelist/evidence rồi tạo candidate.

Mô hình trung gian gồm `AIImportSourceDocument`, `AIImportSourceGroup`, `AIImportSourceCandidate` và `AIImportSourceLocator`. `AIImportService` chỉ quản lý session/state, persistence, validation, duplicate/reference/dependency, CRUD Confirm và DTO.

Dependency PDF được pin `PdfPig 0.1.15`. DOCX tiếp tục dùng `DocumentFormat.OpenXml 3.5.1`.

## 3. Database và migration

Filesystem hiện tại chỉ có một migration baseline:

`20260813071843_InitialCreate`

Baseline này tạo toàn bộ schema AI Import, bao gồm:

- Session: `SourceFormat`, `SourceMetadataJson`, `SourceSnapshotJson`.
- Group: `SourceLabel`, `SourceLocatorJson`, `ExtractionMode`.
- Item: `SourceLocatorJson`, `EvidenceSnippet`, `AiConfidence`, `OcrConfidence`.
- Audit: `SourceFormat`, `ExtractionMode`, `OcrUsed`, `OcrPageCount`, `AiChunkCount`.

Các cột Sheet/Region/Row tương thích Excel vẫn được giữ. Giá trị mặc định trong model cho session mới là `XLSX` khi luồng tạo không cung cấp format.

Chạy local:

```powershell
dotnet restore CafeChain/CafeChain.csproj
dotnet ef database update --project CafeChain/CafeChain.csproj
```

Lưu ý chuyển đổi: kế hoạch trước đây dự kiến giữ `20260812160337_InitialCreate` và thêm `20260813062128_AddDocumentSourcesToAIImport`, nhưng hai file đó không còn trong workspace hiện tại. Nếu database development/test đã ghi migration ID cũ, hãy tạo lại database disposable hoặc viết migration chuyển tiếp có kiểm soát. Không xóa/tạo lại production và không tự động migrate production ngoài quy trình release.

## 4. Cấu hình

Section `AIImport` trong `appsettings.json` là nguồn duy nhất cho giới hạn:

```json
{
  "AIImport": {
    "AllowedExtensions": [ ".xlsx", ".docx", ".pdf" ],
    "MaxFileBytes": 10485760,
    "MaxExpandedBytes": 104857600,
    "MaxCompressionRatio": 100,
    "DocxMaxParagraphs": 20000,
    "DocxMaxTables": 200,
    "DocxMaxTableRows": 20000,
    "DocxMaxCells": 200000,
    "DocumentMaxExtractedCharacters": 1000000,
    "PdfMaxPages": 200,
    "PdfMaxTextBlocks": 20000,
    "PdfMaxImages": 1000,
    "PdfOcrImageAreaRatioThreshold": 0.15,
    "MaxAIChunks": 100,
    "AIChunkMaxCharacters": 12000,
    "AIChunkOverlapCharacters": 500,
    "OcrEnabled": false
  }
}
```

`AIImportRequestSizeLimitAttribute` áp dụng limit multipart theo cấu hình trước model binding. Service kiểm tra lại kích thước file, vì request limit không thay thế business validation.

### Ollama

Ollama chỉ cần cho mapping/extraction không đủ rõ:

```json
{
  "Ollama": {
    "BaseUrl": "http://localhost:11434/",
    "Model": "qwen2.5:7b",
    "TimeoutSeconds": 120,
    "Think": false
  }
}
```

File deterministic không bắt buộc gọi AI. Nếu AI offline và tài liệu cần fallback, session trả warning/error typed để người dùng sửa cấu trúc hoặc reanalyze sau.

## 5. Tài khoản demo và quyền

Mật khẩu demo local/test: `The@1712`.

| Tài khoản | Vai trò | Khả năng mặc định |
|---|---|---|
| `owner@cafechain.vn` | Chủ doanh nghiệp | Upload/Analyze và Confirm cả 5 entity |
| `accountantwarehouse@cafechain.vn` | Kế toán/kho | Upload/Analyze cả 5; Confirm Ingredient và Supplier |

Credential trên không dùng production. Production phải đổi mật khẩu, quản lý secret và cấp quyền chính thức.

## 6. Chuẩn bị tài liệu

### Excel

- Dùng `.xlsx` thật, header rõ và dữ liệu sau header.
- Có thể có nhiều sheet/region.
- Không dùng sheet/dòng/cột ẩn cho dữ liệu cần nhập.
- Formula phải có cached value; hệ thống không tính formula.

### DOCX

- Dùng bảng có hàng đầu là header, hoặc record key-value như `Mã danh mục: CAT01`.
- Tách nhiều record bằng heading hoặc paragraph trống.
- Ô gộp và Track Changes chưa resolve được parser đánh dấu review; nên Accept/Reject revisions và bỏ merge trước khi upload để có kết quả deterministic nhất.
- Field command và nội dung liên kết động bị từ chối. Locator giữ section/paragraph/table/row/cell tới từng field.
- Không nhúng OLE, macro, remote image hoặc external hyperlink.

### PDF

- PDF phải chọn/copy được text.
- Table nên có cột thẳng hàng và header rõ; parser dựng cell theo khoảng cách tọa độ.
- PDF scan, ảnh chụp, trang ảnh không có text hoặc vùng ảnh đáng kể không thể chứng minh đã trích xuất đủ sẽ trả `PDF_CẦN_OCR`. Ngưỡng mặc định là 15% diện tích trang qua `PdfOcrImageAreaRatioThreshold`.
- Không nhúng attachment, JavaScript, Launch hoặc URI action.

Header/nhãn khuyến nghị:

- Category: `CategoryCode`, `Name`, `Icon`, `Active`.
- Drink: `DrinkCode`, `Name`, `Description`, `Category`, `ProductType`.
- Size: `SizeCode`, `Name`, `Description`, `SizeType`.
- Ingredient: `Code`, `Name`, `BaseUnit`.
- Supplier: `Name`, `TaxCode`, `Address`, `Note`, `PrimaryPhone`, `PrimaryContactName`, `PrimaryContactPhone`, `PrimaryContactEmail`, `PrimaryContactPosition`.

Alias tiếng Việt như “Mã danh mục”, “Tên đồ uống”, “Đơn vị cơ sở” và “Mã số thuế” được schema registry nhận diện.

## 7. Quy trình UI

### Analyze

1. Chọn hoặc thả `.xlsx`, `.docx` hoặc `.pdf`, tối đa 10 MiB.
2. Chọn entity hint nếu tài liệu chỉ chứa một loại; để **Tự động** khi có header rõ.
3. Chọn **Phân tích**.
4. SweetAlert hiển thị kết quả thao tác; lỗi field/dòng vẫn hiển thị inline.

### Preview

UI hiển thị:

- format và các extraction mode;
- source label và locator theo sheet/paragraph/table/page/block;
- raw → normalized value;
- evidence và AI confidence nếu có;
- lỗi, warning, review và action.

Mapping sai có thể được chỉnh ở group. Sửa candidate dùng form cùng quy tắc CRUD; Category Icon chỉ nhận rỗng hoặc đúng một biểu tượng Unicode hoàn chỉnh.

Modal sửa candidate có các quy tắc hiển thị sau:

- lỗi server có `field` được gắn vào đúng input và không bị client validation xóa ngay khi mở modal;
- lỗi field chỉ được bỏ khỏi trạng thái tạm trên client sau khi người dùng thay đổi input đó; PATCH tiếp theo vẫn revalidate toàn bộ ở backend;
- lỗi không có field và lý do `REVIEW_REQUIRED` được liệt kê ở đầu modal thay vì yêu cầu người dùng tìm một ô không tồn tại;
- tên field trong danh sách lỗi có thể đưa focus tới input tương ứng;
- warning Supplier hiển thị nội dung match, checkbox xác nhận và lý do override;
- header/footer cố định trong khung modal, còn fields, cảnh báo và dữ liệu nguồn nằm trong một vùng cuộn chung. Khi mở bản ghi khác, vùng cuộn được reset để không giữ vị trí của bản ghi trước;
- SweetAlert2 vẫn nằm phía trên native dialog khi thao tác Save/SKIP hoặc API phát sinh thông báo.

Đối với Icon, client theo dõi giá trị hợp lệ gần nhất. Nếu người dùng gõ/dán chữ, số, HTML hoặc nhiều biểu tượng, input khôi phục giá trị trước đó và hiện lỗi. `Intl.Segmenter` cho phép emoji ghép như `❤️` và `👩‍🍳` được tính là một biểu tượng; backend vẫn kiểm tra lại bằng `CategoryIconPolicy`.

Supplier gần trùng yêu cầu lý do override. Backend tạo warning token gắn actor/payload; không thể bỏ qua bằng cách chỉ bật checkbox client.

Candidate `REVIEW_REQUIRED` chỉ do confidence thấp được xử lý như sau:

1. Mở **Sửa dòng** và đối chiếu normalized data với evidence/source locator.
2. Sửa trường nếu cần.
3. Chọn **Lưu và kiểm tra lại**.
4. Backend đánh dấu lần PATCH đó là xác nhận thủ công. Nếu schema/reference/duplicate validation đều đạt, candidate chuyển sang `VALID`; nếu vẫn có lỗi thật, candidate tiếp tục là `ERROR` hoặc `REVIEW_REQUIRED` phù hợp.

Việc xác nhận thủ công được giữ qua lần revalidate của session; confidence nguồn thấp không tự đưa candidate hợp lệ trở lại review. Đây không phải cơ chế bỏ qua lỗi hay warning.

### Confirm

Confirm bị chặn nếu còn `ERROR`, `REVIEW_REQUIRED` hoặc warning chưa xác nhận. Client giữ cùng `Idempotency-Key` khi retry request chưa rõ kết quả.

Confirm là transaction `Serializable` toàn phiên. Category được tạo trước Drink; một lỗi rollback mọi entity đã tạo trong request.

### Reanalyze, Cancel và History

- Excel reanalyze lại mapping region chưa rõ.
- DOCX/PDF reanalyze dùng snapshot text đã trích xuất; không cần giữ binary upload.
- Cancel chuyển phiên Preview/Failed sang `CANCELLED` bằng conditional update.
- History chỉ trả session của account hiện tại và có source format/extraction modes.
- Snapshot text bị xóa khi session Completed, Cancelled hoặc Expired.

## 8. API contract

Không có endpoint riêng cho DOCX/PDF.

| Method | Endpoint | Quyền chính |
|---|---|---|
| `POST` | `/api/ai-import/analyze` | Upload + Analyze |
| `POST` | `/api/ai-import/{id}/reanalyze` | Analyze |
| `GET` | `/api/ai-import/{id}` | View |
| `GET` | `/api/ai-import/{id}/editor-options` | View |
| `PATCH` | `/api/ai-import/{id}/groups/{groupId}` | Analyze |
| `PATCH` | `/api/ai-import/{id}/items/{itemId}` | Analyze |
| `POST` | `/api/ai-import/{id}/confirm` | Confirm + entity Create |
| `POST` | `/api/ai-import/{id}/cancel` | Cancel |
| `GET` | `/api/ai-import/history` | History |

Mutation dùng cookie Admin và anti-forgery header `RequestVerificationToken`. Confirm bắt buộc có `Idempotency-Key` và `expectedPreviewVersion`.

Reanalyze cũng là mutation có optimistic concurrency: body bắt buộc `{ "expectedPreviewVersion": n }`. Group PATCH, Item PATCH, Reanalyze, Confirm và Cancel stale đều trả `409 PREVIEW_ĐÃ_THAY_ĐỔI`.

Response session/group/item bổ sung backward-compatible:

- `sourceFormat`, `sourceMetadata`, `extractionModes`;
- `sourceLabel`, `extractionMode`, `sourceLocator`;
- `evidenceSnippet`, `aiConfidence`, `ocrConfidence`.
- `issues[]` với `code`, `message`, `field`, `severity`, `sourceLocator`, `metadata`; `errors[]`, `warnings[]` và `position` cũ vẫn được giữ;
- `sourceColumns[]` với source key, label, classification và target field;
- `manualReviewConfirmed`, `manualReviewConfirmedAtUtc`.

Item PATCH có thêm `manualReviewConfirmed`. Với reason cho phép review, modal hiển thị checkbox đối chiếu bằng chứng riêng; client chỉ gửi `true` khi người dùng chủ động chọn checkbox rồi bấm **Lưu và kiểm tra lại**. Backend lưu account/thời điểm/hash payload và tự vô hiệu hóa khi payload đổi. Checkbox warning và Supplier server token vẫn độc lập với xác nhận này.

Reanalyze nhận `expectedPreviewVersion` bắt buộc và claim trạng thái `ANALYZING` bằng optimistic concurrency trước khi thay preview. Hai request cùng version chỉ có một request thắng; request còn lại nhận `409 PREVIEW_ĐÃ_THAY_ĐỔI` và không ghi đè kết quả mới.

### Kiến trúc validation-first

- `AIImportCandidateValidator` là module chung cho normalization, schema issue và status của ba format.
- `AIImportReferenceResolver` trả `FOUND`, `NOT_FOUND`, `AMBIGUOUS`, `INACTIVE`, `PENDING_IN_SESSION` hoặc `FORBIDDEN`; không chọn record đầu tiên.
- `AIImportBusinessKeys` tập trung hard business key và giữ Supplier soft match bên `AdminSupplierService`.
- Preview validator batch-preload reference/hard duplicate key; Supplier soft duplicate dùng `FindDuplicateMatchesBatchAsync` để nạp tập Supplier/phone/contact một lần cho cả batch nhưng vẫn dùng đúng normalization và signal policy của `AdminSupplierService`.

### Phase 9: module và scoped revalidation

- `AIImportEntityRegistry` là nguồn tập trung cho business key, quyền Create và thứ tự dependency của năm entity.
- `AIImportPreviewValidator` cùng `AIImportResolutionEngine` xác định candidate issue và dependency closure.
- Group/Item PATCH chỉ revalidate group/item bị sửa, cohort business key cũ/mới và Drink phụ thuộc Category; Analyze, Reanalyze và Confirm vẫn full validation.
- `AIImportAnalysisCoordinator`, `AIImportPreviewMutationCoordinator`, `AIImportConfirmCoordinator` và `AIImportSessionQuery` gom state transition, preview mutation, execution plan và query ordering ra khỏi nhánh nghiệp vụ tương ứng.
- Reference và hard duplicate query chỉ lấy các code/name xuất hiện trong validation scope. Supplier vẫn batch một lần và warning token không đổi.
- Reanalyze lỗi sau conditional claim chuyển session sang `FAILED` với `PHÂN_TÍCH_LẠI_THẤT_BẠI`, không để phiên mắc ở `ANALYZING`.
- `AIImportEntityCreator` build DTO rồi gọi năm CRUD service; coordinator không tự `DbContext.Add` entity nghiệp vụ.
- Parser adapter chỉ tạo source document/group/candidate và issue nguồn; database-sensitive validation chạy ở preview và chạy lại khi Confirm.

Migration áp dụng theo thứ tự `20260813071843_InitialCreate` rồi `20260813183911_AddAIImportValidationState`. Migration mới thêm `SourceColumnsJson`, `IssuesJson`, `SourceIssuesJson` và các cột manual-review; không chỉnh baseline đã squash.

Locator có field tùy format: sheet/region/row/column; section/paragraph/table/tableRow/tableColumn; page/block/boundingBox/textStart/textEnd.

## 9. Mã lỗi thường gặp

| Mã | Xử lý |
|---|---|
| `ĐỊNH_DẠNG_DOC_CŨ_KHÔNG_HỖ_TRỢ` | Chuyển `.doc` sang `.docx` thật |
| `ĐỊNH_DẠNG_KHÔNG_KHỚP_NỘI_DUNG` | Không đổi đuôi giả; upload đúng MIME/signature |
| `DOCX_BỊ_HỎNG` / `PDF_BỊ_HỎNG` | Mở và lưu lại bằng ứng dụng tin cậy |
| `DOCX_CÓ_MẬT_KHẨU` / `PDF_CÓ_MẬT_KHẨU` | Gỡ password/encryption trước khi upload |
| `NỘI_DUNG_CHỦ_ĐỘNG_KHÔNG_ĐƯỢC_HỖ_TRỢ` | Loại macro, OLE, attachment, external action/link |
| `DOCX_VƯỢT_GIỚI_HẠN` / `PDF_VƯỢT_GIỚI_HẠN` | Chia tài liệu hoặc giảm resource |
| `DOCX_CẤU_TRÚC_KHÔNG_RÕ` / `BỐ_CỤC_PDF_KHÔNG_RÕ` | Dùng table/key-value rõ hơn hoặc bật Ollama để reanalyze |
| `THỨ_TỰ_ĐỌC_PDF_KHÔNG_RÕ` | Xuất lại PDF một cột/bảng rõ hoặc dùng nguồn XLSX/DOCX |
| `KHÔNG_XÁC_ĐỊNH_RANH_GIỚI_BẢN_GHI` | Tách record bằng heading/paragraph trống hoặc một hàng cho mỗi record |
| `CỘT_CẤM` | Xóa StoreId/BranchId/DB ID/audit/SQL/command khỏi file hoặc SKIP dòng |
| `CỘT_KHÔNG_XÁC_ĐỊNH` | Kiểm tra cột bị bỏ qua và acknowledge warning nếu đúng |
| `XUNG_ĐỘT_ÁNH_XẠ` | Chọn source key cụ thể khi header/alias trùng |
| `REFERENCE_KHÔNG_DUY_NHẤT` | Dùng code duy nhất thay vì tên mơ hồ |
| `PDF_CẦN_OCR` | Chuyển sang PDF searchable text; OCR chưa được hỗ trợ |
| `DOCX_Ô_GỘP_CẦN_XEM_LẠI` / `DOCX_TRACK_CHANGE_CẦN_XEM_LẠI` | Kiểm tra thủ công hoặc bỏ merge/Accept Changes rồi upload lại |
| `AI_CONFIDENCE_THẤP` | Candidate vẫn được giữ nhưng phải sửa/xác nhận ở trạng thái review |
| `CHUNK_VƯỢT_GIỚI_HẠN` | Chia nhỏ tài liệu; hệ thống không bỏ âm thầm phần text vượt giới hạn |
| `AI_TRÍCH_XUẤT_KHÔNG_CÓ_BẰNG_CHỨNG` | Làm rõ tài liệu; AI output không được backend chấp nhận |
| `XUNG_ĐỘT_DỮ_LIỆU_TRONG_TÀI_LIỆU` | Chọn/SKIP bản ghi có cùng key nhưng payload khác |
| `REFERENCE_KHÔNG_HỢP_LỆ` | Sửa/tạo sẵn Category, ProductType hoặc Unit phù hợp |
| `PREVIEW_ĐÃ_THAY_ĐỔI` | Tải preview mới rồi thao tác lại |
| `PREVIEW_CHƯA_SẴN_SÀNG` | Sửa lỗi/review hoặc SKIP candidate |
| `NHÀ_CUNG_CẤP_GẦN_TRÙNG` | Kiểm tra match và nhập lý do nếu vẫn tạo |

Error response không trả stack trace, inner exception, raw document hoặc SQL detail.

## 10. Kiểm thử và vận hành

```powershell
dotnet build CafeChain/CafeChain.csproj --no-restore --nologo
dotnet test CafeChain.Tests/CafeChain.Tests.csproj --no-build --nologo
```

Nhóm AI Import:

```powershell
dotnet test CafeChain.Tests/CafeChain.Tests.csproj --no-build --filter FullyQualifiedName~AIImport
```

Kết quả xác minh tại thời điểm cập nhật tài liệu:

- build ứng dụng: thành công, `0 error`; lượt incremental ban đầu báo `0 warning`, còn lượt biên dịch lại toàn bộ source hiển thị các warning nullable/obsolete tồn tại ở nhiều module ngoài phạm vi AI Import, vì vậy không coi repository là clean-warning;
- nhóm AI Import không phụ thuộc SQL Server: `72/72` test đạt, bao gồm entity registry, scoped dependency closure, Confirm execution plan và contract module Phase 9;
- nhóm Supplier duplicate contract sau khi thêm batch matcher: `31/31` test đạt;
- 6 test `AIImportSqlServerTests` chưa chạy tới nghiệp vụ: `localhost\SQLEXPRESS02` trả `Failed to generate SSPI context`; thử LocalDB disposable cũng thất bại ở bước tạo automatic instance. Đây là giới hạn xác thực/instance của môi trường và không được ghi là test đạt;
- full suite không-SQL: `2099/2132` test đạt. Có 33 test lỗi ở các contract/UI/seed ngoài AI Smart Import; 32 lỗi thuộc baseline đã ghi nhận, lỗi còn lại là contract `AdminAiSupplierRefactorTests` yêu cầu nhãn “Thông tin kỹ thuật” trong view anomaly không thuộc phạm vi và không bị refactor này chỉnh sửa;
- full suite có SQL không thể hoàn tất trong giới hạn thời gian vì các nhóm SQL integration cùng không kết nối được test instance. Không dùng database production để thay thế.

Với SQL integration, cấu hình `CAFECHAIN_TEST_SQLSERVER_CONNECTION_STRING` theo convention `{Database}` của test. Không trỏ test disposable database vào production.

## 11. Giới hạn đã biết

- Không có OCR/Vision; `OcrConfidence` luôn `null`, `OcrUsed=false`.
- PDF table reconstruction là heuristic tọa độ; bảng nhiều cột lồng, rotation hoặc layout quá phức tạp có thể cần AI/review.
- PdfPig có thể trả `Word.Text` kèm khoảng trắng; parser hiện trim từng word và ghép bằng một khoảng trắng, nhưng vẫn không tự sửa các khoảng trắng có chủ ý nằm bên trong một word.
- DOCX merged cell và tracked changes chưa resolve không được tự suy diễn: candidate bị bắt buộc review; field command bị từ chối.
- Không lưu binary nguồn; reanalyze DOCX/PDF dựa trên snapshot text đã trích xuất cho tới khi session kết thúc/hết hạn.
- AI không thay thế CRUD validation và không đảm bảo candidate được Confirm.
