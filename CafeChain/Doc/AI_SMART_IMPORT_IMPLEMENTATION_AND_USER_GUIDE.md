# AI Smart Import CafeChain — hướng dẫn triển khai và sử dụng

> Cập nhật 15/08/2026: migration history forward-only là `20260815105817_InitialCreate` → `20260815141744_AddAIImportOcrRuntimeAndMultiFile`. OCR dùng Tesseract local, có System Setting, health check và switch theo phiên; không dùng cloud hoặc khóa API.

## 1. Capability hiện tại

AI Smart Import hỗ trợ:

- Excel `.xlsx`;
- Word `.docx` OpenXML;
- PDF text, PDF scan/image và PDF mixed text/OCR khi OCR được cấu hình;
- deterministic extraction cho Excel, DOCX table/key-value và PDF table/key-value;
- Ollama structured fallback có chunk, whitelist và evidence validation;
- Preview/PATCH/reanalyze/history trên cùng `/api/ai-import`;
- modal sửa dòng giữ lỗi server đúng field, hiển thị lỗi cấp dòng/warning và dùng vùng cuộn responsive có footer luôn truy cập được;
- xác nhận thủ công candidate confidence thấp bằng **Lưu và kiểm tra lại** mà không bỏ qua schema validation;
- Confirm nguyên tử cho Category, Drink, Size, Ingredient và Supplier.

Không hỗ trợ `.doc`, `.docm`, file có mật khẩu, active content, OCR cloud, `UPDATE`, `UPSERT` hoặc `DELETE`.

## 2. Kiến trúc module

### Inventory khóa tại Phase 0

| Module | Trách nhiệm hiện tại | Trạng thái | Khoảng trống được phép sửa |
|---|---|---|---|
| Document pipeline và Excel parser | Chọn adapter nguồn, deterministic-first, region/mapping Excel | Khóa regression | Region khó, sparse row và multi-entity |
| DOCX parser | Body-only, logical merged grid, revision-aware text, nested-table isolation và boundary | Đã triển khai/test | Layout quá mơ hồ vẫn bắt review |
| PDF parser | Security preflight, rotation/top-left, Unicode, table/key-value, page classifier và OCR merge | Đã triển khai/test | Layout quá mơ hồ vẫn bắt review |
| Document AI extractor | Semantic block chunk, whitelist/evidence, tối đa hai attempt, typed failure và cross-chunk conflict | Đã triển khai/test | Không nới schema/evidence khi retry |
| Preview/validation/resolution | Schema, reference, duplicate, dependency, confidence tách lớp và manual review | Khóa invariant + OCR | Không có final confidence tổng hợp |
| Confirm/entity creator | Idempotency, conditional claim, Serializable và CRUD service | Khóa invariant | Chỉ tách orchestration; không đổi thứ tự hoặc nguồn tạo |
| Preview UI | Group/item edit, badge TEXT/OCR/MIXED, confidence và field provenance | Đã triển khai | Không lưu image overlay |
| Persistence/audit/retention | OCR snapshot tối thiểu, version/provider/usage audit, purge terminal state | Đã triển khai | Không lưu binary/rendered image |

Hai migration baseline không được sửa sau Phase 0. Mọi schema OCR phải đi bằng migration thứ ba tiến tiếp.

Interface chính `IAIImportDocumentPipeline` che toàn bộ việc chọn format, parser và AI fallback khỏi `AIImportService`.

Các adapter nguồn:

- `AIImportExcelSourceParser`: bọc `IAIImportExcelParser` và `IAIImportRegionAnalyzer` cũ để giữ backward compatibility.
- `AIImportDocxSourceParser`: guard ZIP/OpenXML, table và paragraph key-value.
- `AIImportPdfSourceParser`: PdfPig word extraction, line/cell/table theo tọa độ và key-value.
- `AIImportDocumentAiExtractor`: chunk text, gọi `IOllamaClient.ChatStructuredAsync`, xác minh whitelist/evidence rồi tạo candidate.

Mô hình trung gian gồm `AIImportSourceDocument`, `AIImportSourceGroup`, `AIImportSourceCandidate` và `AIImportSourceLocator`. `AIImportService` chỉ quản lý session/state, persistence, validation, duplicate/reference/dependency, CRUD Confirm và DTO.

Dependency PDF text được pin `PdfPig 0.1.15`; trang scan được rasterize bằng `PDFtoImage 5.2.1`/PDFium. DOCX tiếp tục dùng `DocumentFormat.OpenXml 3.5.1`.

## 3. Database và migration

Filesystem có đúng hai migration theo thứ tự:

1. `20260815105817_InitialCreate` — baseline đã squash, gồm validation state và OCR traceability hiện hữu.
2. `20260815141744_AddAIImportOcrRuntimeAndMultiFile` — OCR runtime snapshot, `ImportSourceDocuments` và liên kết Group → SourceDocument.

Baseline này tạo toàn bộ schema AI Import, bao gồm:

- Session: `SourceFormat`, `SourceMetadataJson`, `SourceSnapshotJson`.
- Group: `SourceLabel`, `SourceLocatorJson`, `ExtractionMode`.
- Item: `SourceLocatorJson`, `EvidenceSnippet`, `AiConfidence`, `OcrConfidence`.
- Audit: `SourceFormat`, `ExtractionMode`, `OcrUsed`, `OcrPageCount`, `AiChunkCount`.

Các cột Sheet/Region/Row tương thích Excel vẫn được giữ. Giá trị mặc định trong model cho session mới là `XLSX` khi luồng tạo không cung cấp format.

Chạy local:

```powershell
dotnet restore CafeChain/CafeChain.csproj
dotnet tool restore
dotnet ef database update --project CafeChain/CafeChain.csproj
```

Repository pin `dotnet-ef` 8.0.0 trong `dotnet-tools.json`; máy mới hoặc clone mới phải chạy `dotnet tool restore` từ repository root. Hai migration trên đã tồn tại, vì vậy không chạy lại `dotnet ef migrations add InitialCreate`.

Database chưa deploy lâu dài có thể tạo lại từ hai migration trên. Không đổi tên/sửa migration đã phát hành và không tự động migrate production ngoài quy trình release.

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
    "OcrProvider": "TesseractLocal",
    "OcrExecutablePath": "tesseract",
    "OcrTessdataPath": "Resources/OCR/tessdata",
    "OcrLanguages": "vie+eng",
    "OcrMaxPages": 50,
    "OcrRenderDpi": 200,
    "OcrMaxRenderedPixelsPerPage": 20000000,
    "OcrMaxTotalRenderedPixels": 200000000,
    "OcrMaxConcurrentPages": 1,
    "OcrPageTimeoutSeconds": 45,
    "OcrTotalTimeoutSeconds": 180,
    "OcrReviewConfidenceThreshold": 0.85
  }
}
```

`AIImportRequestSizeLimitAttribute` áp dụng limit multipart theo cấu hình trước model binding. Service kiểm tra lại kích thước file, vì request limit không thay thế business validation.

OCR chạy local nên không có endpoint hoặc khóa API. User Secrets chỉ dùng cho các dịch vụ khác; mọi secret OCR cũ không còn được bind hay sử dụng. Production adapter chỉ rasterize trang IMAGE/MIXED, chạy `tesseract <image> stdout --tessdata-dir ... -l vie+eng --oem 1 --psm 3 tsv`, rồi đưa word evidence trở lại pipeline deterministic/validation/Preview/Confirm hiện hữu.

### Cài Tesseract local trên Windows

1. Cài Tesseract và Visual C++ Runtime, sau đó bảo đảm lệnh `tesseract --version` chạy được. Lệnh cài gợi ý: `winget install --id UB-Mannheim.TesseractOCR --exact`.
2. Từ repository root chạy `powershell -ExecutionPolicy Bypass -File scripts/setup-tesseract-ocr.ps1`.
3. Script tải bản pin `tessdata_fast 4.1.0` cho `vie` và `eng`, kiểm tra SHA-256 rồi chạy smoke check. File `.traineddata` bị gitignore và không được commit.
4. Mở **Cài đặt hệ thống → OCR & nhận dạng tài liệu**, lưu tham số rồi bấm **Kiểm tra OCR**. Khi trạng thái là `READY`, switch OCR theo từng lần import tự được mở khóa; không có switch OCR toàn hệ thống.

Health check chỉ trả trạng thái, phiên bản engine và khả năng executable/model; không trả command line hay đường dẫn máy chủ. Nếu đổi provider path, tessdata path hoặc languages, fingerprint thay đổi và health cũ trở thành `STALE`. Runtime setting cũ `vi` được đọc tương thích thành `vie+eng`.

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

- PDF có text dùng text pipeline và không gọi OCR; PDF image/mixed chỉ gọi OCR cho trang cần thiết khi request chọn `UseOcr` và health đang `READY`.
- Table nên có cột thẳng hàng và header rõ; parser dựng cell theo khoảng cách tọa độ.
- Khi OCR tắt, PDF scan/mixed vẫn trả `PDF_CẦN_OCR` và provider không được resolve/call. Khi bật nhưng provider/config lỗi, hệ thống trả typed error và không tạo pipeline import thứ hai.
- Critical field có OCR confidence dưới `0,85` bắt buộc manual review dù AI confidence cao. UI hiển thị page, raw evidence, normalized value, bbox/polygon và OCR/AI confidence riêng.
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

Migration áp dụng theo thứ tự `20260815105817_InitialCreate`, rồi `20260815141744_AddAIImportOcrRuntimeAndMultiFile`; không chỉnh sửa baseline.

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
| `PDF_CẦN_OCR` | Lần import chưa chọn OCR; chọn OCR cho PDF scan hoặc chuyển sang PDF searchable text |
| `PDF_OCR_KHÔNG_KHẢ_DỤNG` | Kiểm tra Tesseract executable, Visual C++ Runtime và model local |
| `PDF_OCR_QUÁ_THỜI_GIAN` | Chia tài liệu hoặc kiểm tra provider/network |
| `PDF_OCR_VƯỢT_GIỚI_HẠN` | Giảm số trang/DPI/kích thước hoặc chia tài liệu; hệ thống không truncate |
| `OCR_OUTPUT_KHÔNG_HỢP_LỆ` | Provider không trả đủ page/word/span/polygon hợp lệ |
| `OCR_CONFIDENCE_THẤP` | Đối chiếu field với raw OCR và xác nhận manual review |
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

Kết quả xác minh cuối ngày 15/08/2026:

- build ứng dụng/test project: `0 error`; warning nullable/obsolete hiện hữu ngoài phạm vi vẫn còn nên không ghi clean-warning;
- AI Import không phụ thuộc SQL Server: `90/90` PASS, gồm parser header trùng, OCR/provider/status/resource/cancellation, semantic retry/taxonomy/conflict, multi-file UI/API và migration contract;
- forward migration `20260815141744_AddAIImportOcrRuntimeAndMultiFile` được sinh từ model hiện hành sau baseline `20260815105817_InitialCreate`;
- 6 `AIImportSqlServerTests`: `NOT VERIFIED`, `0/6` chạy tới nghiệp vụ vì disposable connection `localhost\SQLEXPRESS02` lỗi `Failed to generate SSPI context` ngay lúc initialize;
- full suite: `2186/2370` PASS, `184` FAIL; ngoài lỗi SQL/SSPI còn các contract UI/warehouse tồn tại ngoài phạm vi AI Smart Import. Không sửa test để che lỗi và không dùng database production thay thế.

Với SQL integration, cấu hình `CAFECHAIN_TEST_SQLSERVER_CONNECTION_STRING` theo convention `{Database}` của test. Không trỏ test disposable database vào production.

## 11. Giới hạn đã biết

- OCR production hiện dùng Tesseract local. Process runner bật TSV bằng `-c tessedit_create_tsv=1`, vì vậy tessdata chỉ cần `vie.traineddata`/`eng.traineddata` và không cần copy `configs/tsv`. Integration test native là opt-in sau khi chạy script setup; test parser/TSV/health contract vẫn chạy trong suite không cần secret.
- PDF table reconstruction vẫn là heuristic tọa độ; layout nhiều cột/bảng lồng quá mơ hồ tạo review thay vì tự quyết định.
- Text được normalize Unicode/ligature/zero-width/NBSP trước business key nhưng raw evidence vẫn được giữ trong field provenance.
- DOCX merged cell và tracked changes chưa resolve không được tự suy diễn: candidate bị bắt buộc review; field command bị từ chối.
- Không lưu binary nguồn/rendered image. Reanalyze dùng text/OCR snapshot và không gọi OCR; snapshot bị purge ở `COMPLETED`, `CANCELLED`, `EXPIRED`, khi thiếu snapshot phải upload lại.
- AI không thay thế CRUD validation và không đảm bảo candidate được Confirm.

## 12. Hướng dẫn chức năng mới

### Chọn nguồn khi header trùng

Khi thấy badge **Cần chọn nguồn**, chọn đúng `Name [B]`, `Name [C]` hoặc source key tương ứng trong phần ánh xạ. Trong modal sửa dòng có nút **Chọn làm nguồn cho…** cạnh từng lựa chọn; thao tác này cập nhật toàn vùng, không chỉ dòng đang mở. Sau PATCH thành công, preview được tải lại với version mới.

### Cấu hình OCR

Mở **Cài đặt hệ thống → OCR & nhận dạng tài liệu** (`?tab=ocr`). Màn hình chỉ hiển thị provider local, phiên bản Tesseract, languages và trạng thái executable/model. Lưu trạng thái bật, languages, confidence, DPI và resource/timeout limits độc lập với tab âm kho. Dùng **Kiểm tra OCR** để cập nhật trạng thái `READY`, `NOT_CONFIGURED`, `STALE` hoặc `UNAVAILABLE`.

Tại AI Smart Import, switch **OCR cho PDF scan** mặc định OFF và chỉ bật được khi Tesseract health `READY`. PDF có lớp chữ luôn dùng text trước; chỉ trang image/mixed mới gọi OCR. Không chọn OCR trả `PDF_CẦN_OCR`; provider lỗi/timeout/limit trả mã typed tương ứng. System Settings không còn switch bật/tắt toàn hệ thống.

### Một phiên nhiều file

Chọn hoặc kéo thả tối đa 10 file `.xlsx`, `.docx`, `.pdf`; giới hạn mặc định là 10 MiB/file và 50 MiB/phiên. Preview hiển thị trạng thái từng nguồn và filename trên mỗi Group. Nếu một nguồn lỗi, Confirm bị khóa nhưng dữ liệu hợp lệ vẫn được xem; dùng **Loại nguồn** để bỏ toàn bộ Group/candidate của file lỗi. Confirm còn lại vẫn nguyên tử toàn phiên.

### Cancel an toàn

Cancel thành công đóng modal sửa, xóa draft và không nhận response đến muộn. Nếu server trả `PREVIEW_ĐÃ_THAY_ĐỔI` hoặc Cancel thất bại, modal/draft được giữ và client tải lại trạng thái trước khi người dùng quyết định.
