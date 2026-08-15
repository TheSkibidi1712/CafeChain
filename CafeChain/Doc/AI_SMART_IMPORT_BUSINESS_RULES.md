# AI Smart Import CafeChain — quy tắc nghiệp vụ

> Cập nhật 15/08/2026: baseline chính thức là `20260815105817_InitialCreate`; thay đổi OCR runtime và multi-file nằm trong forward migration `20260815141744_AddAIImportOcrRuntimeAndMultiFile`. OCR vẫn đi qua Preview/Confirm hiện hữu và mặc định tắt.

## 1. Mục tiêu và thuật ngữ

AI Smart Import nhập master data toàn hệ thống từ `.xlsx`, `.docx` và PDF có lớp text theo luồng:

`Tài liệu nguồn → Đoạn nguồn → Bản ghi ứng viên → Preview → Bản ghi được tạo`

- **Tài liệu nguồn**: file người dùng upload.
- **Đoạn nguồn**: sheet/region Excel, bảng/đoạn DOCX hoặc page/block PDF.
- **Bản ghi ứng viên**: dữ liệu được trích xuất nhưng chưa được phép ghi database.
- **Evidence**: đoạn nguyên văn chứng minh dữ liệu AI trả về có trong tài liệu.
- **Source locator**: vị trí tổng quát để quay lại nguồn: sheet/row, paragraph/table hoặc page/block/bounding box.
- **Extraction mode**: cách tạo candidate, ví dụ `DOCX_TABLE_DETERMINISTIC` hoặc `PDF_TEXT_AI_EXTRACTION`.
- **OCR Snapshot**: page/block/word/span/polygon/confidence và provider metadata tối thiểu trong source snapshot; không chứa binary hoặc ảnh render.
- **Field Provenance**: nguồn gốc riêng cho từng field (`TEXT_LAYER`, `OCR`, `AI_AFTER_TEXT`, `AI_AFTER_OCR`) kèm locator, raw evidence, normalized value và confidence theo tầng.
- **Lỗi theo trường**: lỗi schema có `Field`, được gắn trực tiếp vào input tương ứng trong modal sửa dòng.
- **Lỗi cấp dòng**: lỗi/review không gắn với một input cụ thể, ví dụ xung đột payload hoặc confidence thấp; phải được giải thích ở vùng tổng hợp của modal.
- **Xác nhận thủ công**: hành động người dùng mở candidate `REVIEW_REQUIRED`, kiểm tra normalized data và bấm **Lưu và kiểm tra lại**. Hành động này chỉ hoàn tất review do confidence thấp khi toàn bộ validation khác đã đạt.

DOCX/PDF chỉ mở rộng cách lấy dữ liệu nguồn. Mọi candidate tiếp tục đi qua cùng ImportSchema, normalization, validation, reference, duplicate, dependency, PreviewVersion, RBAC, idempotency, transaction và CRUD service như Excel.

### 1.1 Invariant extraction và confidence

- Không có pipeline OCR/Confirm thứ hai. Security preflight PDF chạy trước classifier/provider; text-only PDF không gọi OCR.
- Trang PDF được phân loại `TEXT_BASED`, `IMAGE_BASED` hoặc `MIXED`. Khi OCR tắt, trang cần OCR trả `PDF_CẦN_OCR` mà không resolve/call provider.
- Production provider là `TesseractLocal`. Chỉ trang IMAGE/MIXED được rasterize bằng PDFium ở DPI runtime rồi chuyển cho Tesseract CLI; PDF text-only không render và không gọi OCR.
- Source, layout, OCR và AI confidence được lưu/hiển thị riêng; cấm tạo “final confidence”. Critical field OCR dưới `0,85` tạo `OCR_CONFIDENCE_THẤP` và manual review dù AI confidence cao.
- AI-after-OCR chỉ được chấp nhận khi evidence là substring của OCR semantic chunk; retry tối đa một lần cho transport/transient hoặc malformed JSON, không nới whitelist/schema/evidence.
- Cùng business key và payload giữa chunk được bỏ trùng; cùng key khác payload tạo `XUNG_ĐỘT_TRÍCH_XUẤT`.

### 1.2 Quy tắc nguồn nâng cao

- DOCX chỉ duyệt body; header/footer/comment/footnote/endnote bị cô lập. Logical grid xử lý `gridSpan`/vertical merge nhưng locator vẫn trỏ ô vật lý gốc. Revision, ownership ô gộp hoặc nested-table boundary mơ hồ bắt review.
- PDF chuẩn hóa rotation `0/90/180/270`, bbox về top-left, Unicode/ligature/zero-width/NBSP trước business key nhưng giữ raw evidence. Header/footer lặp được lọc theo text và vị trí; table qua trang chỉ ghép khi schema/geometry tương thích.
- Excel region có `SourceRegionId`, bounding/header/data range và layout confidence ổn định. Projection Category/Drink chỉ sinh candidate khi row có đủ required field; parser chỉ phát source issue, còn reference/duplicate vẫn chạy ở Preview/Confirm.

### 1.3 Retention và audit

- Snapshot lưu OCR text/word/span/polygon/confidence/provider/version; không lưu PDF binary hoặc rendered image.
- Reanalyze khi còn snapshot chỉ chạy semantic extraction, không gọi OCR. Thiếu snapshot/binary khi cần phân tích lại trả `CẦN_TẢI_LẠI_TỆP_NGUỒN`.
- Snapshot bị purge ở `COMPLETED`, `CANCELLED`, `EXPIRED`. Audit chỉ giữ usage/page count/provider/version/extraction version/confidence summary; không giữ raw OCR, full evidence, prompt, key, token hoặc secret.

## 2. Phạm vi dữ liệu

Chỉ hỗ trợ `CREATE` cho năm entity:

1. Category — Danh mục
2. Drink — Đồ uống
3. Size
4. Ingredient — Nguyên liệu
5. Supplier — Nhà cung cấp

Không hỗ trợ Topping, giá/ảnh đồ uống, contact phụ, Unit, ProductType, công thức/BOM, giao dịch mua hàng, `UPDATE`, `UPSERT` hoặc `DELETE`. Dữ liệu được nhận diện ngoài whitelist phải hiện cảnh báo `KHÔNG_THUỘC_PHẠM_VI` và không được Confirm.

Đây là master data toàn hệ thống. File không được cung cấp `StoreId`, `BranchId` hoặc database ID; session và idempotency dùng global scope `StoreId = 0`.

## 3. Nguồn sự thật khi tạo dữ liệu

Confirm không ghi trực tiếp bằng `DbContext.Add`. Hệ thống gọi đúng các service CRUD hiện hữu:

- `IAdminCategoryService.CreateCategoryAsync`
- `IAdminDrinkService.CreateDrinkAsync`
- `IAdminSizeService.CreateSizeAsync`
- `IAdminIngredientService.CreateAsync`
- `IAdminSupplierService.CreateAsync`

Validation và unique constraint trong CRUD vẫn là lớp quyết định cuối. Smart Import chỉ bổ sung guard tài liệu, extraction, preview validation, reference, duplicate, dependency và transaction toàn phiên.

## 4. Schema nghiệp vụ

### Category

| Field | Quy tắc |
|---|---|
| `CategoryCode` | Bắt buộc, trim, uppercase, 2–30 ký tự |
| `Name` | Bắt buộc, trim, 2–100 ký tự |
| `Icon` | Rỗng hoặc đúng một grapheme Unicode; tối đa lưu trữ 10 ký tự |
| `Active` | Không bắt buộc, mặc định `true` |

Hard duplicate theo `CategoryCode` hoặc `Name`. Category có dependency order trước Drink.

Ở UI, giá trị Icon không hợp lệ do gõ/dán chữ, số, HTML hoặc nhiều biểu tượng bị chặn và input quay lại giá trị hợp lệ gần nhất. Emoji ghép như `❤️` hoặc `👩‍🍳` được tính là một grapheme; việc chặn phía client không thay thế `CategoryIconPolicy` và schema validation phía server.

### Drink

| Field | Quy tắc |
|---|---|
| `DrinkCode` | Bắt buộc, trim, uppercase, tối đa 50 ký tự |
| `Name` | Bắt buộc, trim, tối đa 200 ký tự |
| `Description` | Không bắt buộc, tối đa 1.000 ký tự |
| `Category` | Resolve bằng mã/tên Category active hoặc Category được tạo trong cùng phiên |
| `ProductType` | Resolve bằng mã/tên ProductType active có sẵn |

Hard duplicate theo `DrinkCode` hoặc `Name`. Không nhập giá hoặc ảnh.

### Size

| Field | Quy tắc |
|---|---|
| `SizeCode` | Bắt buộc, trim, uppercase, tối đa 20 ký tự |
| `Name` | Bắt buộc, trim, tối đa 50 ký tự |
| `Description` | Không bắt buộc, tối đa 300 ký tự |
| `SizeType` | Chỉ `Cup` hoặc `Volume` |

Hard duplicate theo `SizeCode` hoặc `Name`.

### Ingredient

| Field | Quy tắc |
|---|---|
| `Code` | Bắt buộc, trim, uppercase, tối đa 50 ký tự |
| `Name` | Bắt buộc, trim, tối đa 200 ký tự |
| `BaseUnit` | Resolve bằng mã/tên Unit active có sẵn |

Hard duplicate theo `Code` hoặc `Name`. Smart Import không tạo Unit.

### Supplier

| Field | Quy tắc |
|---|---|
| `Name` | Bắt buộc, tối đa 200 ký tự |
| `PrimaryPhone` | Bắt buộc |
| `PrimaryContactName` | Bắt buộc |
| `TaxCode` | Không bắt buộc; 10 chữ số hoặc dạng `10-3` |
| `Address` | Tối đa 500 ký tự |
| `Note` | Tối đa 1.000 ký tự |
| `PrimaryContactPhone/Email/Position` | Theo CRUD/DB hiện hữu |

Mã Supplier do service sinh. Chỉ `TaxCode` là hard duplicate. Name, hotline, contact phone/email và address là soft duplicate; phải giữ warning token, reason, actor, payload và expiry của `AdminSupplierService`.

## 5. Pipeline nguồn

### Excel

- Xác minh OpenXML, size, expanded size và compression ratio.
- Đọc shared/inline string, number, boolean, date style và cached formula value.
- Bỏ qua sheet/dòng/cột ẩn; không chạy formula hoặc macro.
- Phát hiện region/header và mapping deterministic; chỉ dùng AI mapping khi confidence chưa đủ.

### DOCX

- Chỉ nhận `.docx` OpenXML thật; `.doc` trả `ĐỊNH_DẠNG_DOC_CŨ_KHÔNG_HỖ_TRỢ`, `.docm` bị từ chối.
- Đọc bảng và các record key-value trong paragraph theo thứ tự tài liệu.
- Bảng rõ và key-value rõ dùng deterministic extraction. List/narrative hoặc schema chưa rõ chỉ được gửi AI dưới dạng text chunk giới hạn.
- Field command và nội dung liên kết động bị từ chối. Tài liệu có Track Changes chưa được chấp nhận hoặc bảng có ô gộp vẫn được trích xuất bảo thủ nhưng mọi candidate liên quan bị hạ confidence để bắt buộc `REVIEW_REQUIRED`.
- Locator DOCX giữ section/paragraph/table/row/cell thực tế; source trace theo field không dùng một locator chung cho toàn record.

### PDF

- Chỉ nhận PDF có signature thật và lớp text đọc được.
- Word được dựng từ letter bằng PdfPig; line/cell/table được dựng theo tọa độ và reading order. Header/footer lặp được loại bỏ.
- Khi ghép `Word.Text`, parser trim từng word rồi chèn đúng một khoảng trắng để tránh giá trị PDF bị biến thành chuỗi có nhiều khoảng trắng nội bộ.
- Key-value và bảng tọa độ rõ dùng deterministic extraction; narrative không rõ dùng AI text extraction.
- PDF không có text, image-only hoặc có vùng ảnh đáng kể mà parser không thể chứng minh đã lấy đủ dữ liệu trả `PDF_CẦN_OCR` khi request không chọn `UseOcr`. Ngưỡng diện tích ảnh mặc định là 15% diện tích trang và nằm trong `AIImportOptions`; hệ thống không giả vờ dùng model text để đọc ảnh.
- Locator PDF gồm page/block/bounding-box và text offset trong snapshot đã trích xuất.

## 6. Ranh giới AI và evidence

AI chỉ chạy sau deterministic extraction và chỉ nhận semantic chunk tối đa theo cấu hình. Nội dung tài liệu luôn được coi là dữ liệu không tin cậy.

Output chỉ được chấp nhận khi:

- JSON đúng structured schema;
- entity thuộc năm entity được phép;
- field thuộc ImportSchema và không phải database ID;
- confidence trong `[0,1]`; candidate thấp hơn `ReviewConfidenceThreshold` vẫn được giữ cùng warning `AI_CONFIDENCE_THẤP` và ban đầu ở `REVIEW_REQUIRED`, không bị âm thầm loại bỏ;
- evidence là substring nguyên văn của chunk;
- mọi giá trị không rỗng xuất hiện trong evidence;
- không có SQL, lệnh hoặc chỉ dẫn làm thay đổi whitelist/schema.

Output sai bị loại và phát sinh mã như `AI_JSON_KHÔNG_HỢP_LỆ`, `NGUỒN_DỮ_LIỆU_AI_KHÔNG_HỢP_LỆ` hoặc `AI_TRÍCH_XUẤT_KHÔNG_CÓ_BẰNG_CHỨNG`. AI không quyết định dữ liệu được ghi database.

Candidate confidence thấp có thể chuyển sang `VALID` sau xác nhận thủ công, nhưng chỉ khi schema validation không còn lỗi. Xác nhận thủ công không được miễn lỗi field, reference, duplicate, conflict, warning chưa xác nhận hoặc quyền Create. Các lần revalidate sau phải giữ kết quả xác nhận thủ công thay vì tự đưa candidate hợp lệ trở lại `REVIEW_REQUIRED` chỉ vì confidence nguồn không đổi.

Chunk overlap không được tạo dữ liệu kép. Candidate cùng business key và cùng normalized payload được giữ nguồn đầu, nguồn sau chuyển `SKIP` với `TRÙNG_TRONG_FILE`; payload khác chuyển `REVIEW_REQUIRED` với `XUNG_ĐỘT_DỮ_LIỆU_TRONG_TÀI_LIỆU`.

## 7. Security guard

Extension, Content-Type và signature phải khớp. Toàn file bị từ chối khi phát hiện:

- file hỏng, mã hóa hoặc có mật khẩu;
- DOCX macro, OLE, embedded binary, external relationship/hyperlink/resource hoặc field command;
- PDF embedded file, JavaScript, Launch hoặc URI action;
- expanded size/compression ratio hoặc resource count vượt giới hạn.

Hệ thống không fetch URL, không mở ứng dụng ngoài, không execute macro/script/command và không log full document hoặc prompt chứa dữ liệu nguồn.

## 8. Giới hạn mặc định

Mọi giới hạn nằm trong `AIImportOptions`:

- 10 MiB/file; DOCX expanded tối đa 100 MiB; compression ratio 100:1;
- Excel: 20 sheet, 10.000 dòng/sheet, 20.000 dòng tổng, 100 cột/sheet, 200.000 cell;
- DOCX: 20.000 paragraph, 200 table, 20.000 table row, 200.000 cell;
- PDF: 200 page, 20.000 text block, 1.000 image; vùng ảnh từ 15% diện tích trang trở lên mặc định yêu cầu OCR để tránh nhập thiếu;
- tối đa 1.000.000 ký tự trích xuất;
- 100 AI chunk, 12.000 ký tự/chunk, overlap 500 ký tự;
- session lifetime mặc định 24 giờ;
- OCR theo từng lần import mặc định không được chọn (`UseOcr=false`).

Controller không hard-code 10 MB. Resource filter lấy giới hạn request từ `AIImportOptions`; service luôn kiểm tra lại `IFormFile.Length`.

## 9. Preview, duplicate và state

Validation dùng một hợp đồng chung cho cả Excel, DOCX và PDF: `Code`, `Message`, `Field?`, `Severity`, `SourceLocator?`, `Metadata?`. `Field` rỗng là lỗi cấp dòng; `Metadata.resolution` cho UI biết phải sửa field, remap group, xác nhận warning, review thủ công hay SKIP conflict. Status được giải quyết theo thứ tự `ERROR > REVIEW_REQUIRED > WARNING > VALID`; `SKIPPED` chỉ do action SKIP và `IMPORTED` chỉ xuất hiện sau Confirm.

Cột nguồn được phân loại:

- `MAPPED`: đang ánh xạ vào ImportSchema;
- `IGNORED`: metadata hoặc projection entity khác đã biết rõ, được hiển thị nhưng không tạo blocker;
- `UNKNOWN`: có dữ liệu thì tạo warning, không âm thầm bỏ;
- `FORBIDDEN`: scope, database ID, actor/quyền, SQL hoặc command có dữ liệu tạo `CỘT_CẤM` và chặn dòng.

Header trùng giữ source key theo cột như `Tên [B]`, `Tên [D]`; backend không tự chọn cột đầu tiên. Hai cột cùng khớp một target tạo `XUNG_ĐỘT_ÁNH_XẠ` cho đến khi người dùng remap rõ ràng. Excel có thể tách Category và Drink từ cùng region khi cả hai projection đủ required field; Category được deduplicate và lập dependency trước Drink. Vùng nguồn dùng chung cell tạo `VÙNG_DỮ_LIỆU_CHỒNG_LẤN` thay vì nhập một cell hai lần.

- `VALID`: hợp lệ.
- `WARNING`: phải xác nhận cảnh báo.
- `ERROR`: không Confirm được.
- `REVIEW_REQUIRED`: mapping/conflict/confidence cần xử lý.
- `SKIPPED`: không tạo.
- `IMPORTED`: đã tạo thành công.

Mọi PATCH group/item và Reanalyze kiểm tra `expectedPreviewVersion`, revalidate và tăng `PreviewVersion`. Client stale nhận HTTP 409 `PREVIEW_ĐÃ_THAY_ĐỔI`.

Xác nhận thủ công được lưu riêng cùng account, thời điểm và hash normalized payload. Nó chỉ giải quyết `AI_CONFIDENCE_THẤP`, Track Changes hoặc ô gộp được đánh dấu `MANUAL_REVIEW`; không giải quyết reference ambiguity, conflict, overlap, record-boundary hoặc lỗi schema. Khi payload thay đổi, xác nhận cũ tự mất hiệu lực.

Quy tắc modal sửa dòng:

- lỗi theo trường từ server phải tiếp tục được hiển thị và giữ trạng thái invalid khi modal chạy client validation lần đầu;
- lỗi server của một trường chỉ được gỡ khỏi UI sau khi người dùng thực sự thay đổi trường đó, rồi server vẫn là lớp xác nhận cuối sau PATCH;
- lỗi cấp dòng phải được liệt kê ở đầu modal; nếu có field tương ứng, người dùng có thể chọn tên field để cuộn/focus đến input;
- `REVIEW_REQUIRED` chỉ do confidence thấp phải hiển thị hướng dẫn kiểm tra và bấm **Lưu và kiểm tra lại**, không được dùng thông báo chung “sửa trường được đánh dấu” khi không có field lỗi;
- warning Supplier phải hiển thị đầy đủ nội dung match, checkbox xác nhận và lý do override khi được yêu cầu;
- header/footer modal luôn nằm trong khung; fields, cảnh báo và dữ liệu nguồn dùng một vùng cuộn chung để các nút hành động luôn truy cập được trên desktop/mobile;
- lỗi field/dòng và warning nghiệp vụ vẫn inline. SweetAlert2 chỉ dùng cho phản hồi thao tác và hộp xác nhận như Analyze, Reanalyze, Save/SKIP, Confirm, Cancel, History hoặc lỗi API; SweetAlert phát sinh khi native dialog mở phải nằm trên dialog.

State machine:

`UPLOADED → ANALYZING → VALIDATING → READY_TO_PREVIEW → IMPORTING → COMPLETED`

Trạng thái ngoại lệ: `FAILED`, `CANCELLED`, `EXPIRED`. Session và history chỉ thuộc account đã upload.

## 10. Idempotency, transaction và quyền

Confirm dùng `IRequestDeduplicationService.BeginScopedAsync` với action `AIImport.Confirm`, actor thật, `ReferenceId = ImportSessionId` và `StoreId = 0`. Snapshot idempotency gồm session, PreviewVersion và lựa chọn/value của item.

Begin idempotency, claim session, tạo đủ entity, ghi kết quả và `MarkSuccessAsync` nằm trong transaction `Serializable`. Category chạy trước Drink. Một lỗi hoặc unique race rollback toàn phiên và không lộ `SqlException`.

Quyền Smart Import:

- `AIImport.View`
- `AIImport.Upload`
- `AIImport.Analyze`
- `AIImport.Confirm`
- `AIImport.Cancel`
- `AIImport.History`

Confirm còn kiểm tra quyền `Category.Create`, `Drink.Create`, `Size.Create`, `Ingredient.Create` hoặc `Supplier.Create` tương ứng.

## 11. Persistence, retention và audit

- Session lưu `SourceFormat`, metadata và snapshot text đã trích xuất; không lưu binary upload.
- Group lưu source label, locator và extraction mode.
- Group còn lưu metadata phân loại cột và issue cấp vùng; Item lưu raw/normalized data, source trace, locator, evidence, AI/OCR confidence, source issue và trạng thái manual review.
- Xác nhận manual review phải là thao tác riêng trong modal, chỉ áp dụng cho reason có resolution `MANUAL_REVIEW`. Warning acknowledgement và Supplier warning token không thay thế xác nhận này. Reanalyze phải claim theo `expectedPreviewVersion`; request thua concurrency trả `409 PREVIEW_ĐÃ_THAY_ĐỔI` và không được ghi đè preview.
- Reference và hard duplicate được preload theo batch. Supplier soft duplicate cũng preload Supplier/phone/contact một lần cho các candidate trong lượt validation, nhưng normalization, matched signals và warning token vẫn do `AdminSupplierService` làm nguồn sự thật.
- Item PATCH revalidate item, cohort cùng business key cũ/mới và Drink tham chiếu Category liên quan. Group PATCH revalidate group, cohort thuộc entity cũ/mới và Drink phụ thuộc; không quét lại entity không liên quan. Analyze, Reanalyze và Confirm vẫn kiểm tra toàn phiên.
- Quyền Create, dependency order và business-key policy của năm entity phải lấy từ `AIImportEntityRegistry`; Confirm execution plan luôn Category trước Drink, không phụ thuộc thứ tự nguồn.
- Audit lưu format, extraction mode, `OcrUsed`, `OcrPageCount` và `AiChunkCount` cùng actor/state/result hiện hữu.
- Snapshot text chỉ tồn tại khi session còn cần reanalyze; bị xóa khi `COMPLETED`, `CANCELLED` hoặc `EXPIRED`.
- Raw data/evidence không được ghi vào application log hoặc history response.

Baseline thật là `20260815105817_InitialCreate`. Forward migration `20260815141744_AddAIImportOcrRuntimeAndMultiFile` bổ sung OCR snapshot cấp phiên, `ImportSourceDocuments` và liên kết nguồn của Group; không sửa baseline.

Vì baseline đã được squash, database development/test cũ phải được tạo lại hoặc có migration chuyển tiếp riêng trước khi deploy. Không được giả định `database update` có thể nâng trực tiếp một database đã ghi migration ID cũ lên baseline mới, và không được tự động xóa database production.

## 12. Quy tắc duplicate header, Cancel, OCR runtime và multi-file

- `sourceKey` là identity theo vị trí cột. `Name [B]` và `Name [C]` không được collapse thành `Name`.
- `XUNG_ĐỘT_ÁNH_XẠ` mang `resolution=REMAP_GROUP`, `targetField` và `candidateSourceKeys`. Chọn nguồn áp dụng toàn Group/Region, tăng `PreviewVersion` và chỉ giải quyết conflict của target field đã chọn.
- Cột không được chọn vẫn xuất hiện trong dữ liệu nguồn bổ sung. `CỘT_KHÔNG_XÁC_ĐỊNH` yêu cầu acknowledgement và không được ghi vào entity.
- Cancel thành công đóng mọi dialog, xóa draft phía client, purge source snapshot và vô hiệu hóa response cũ. Cancel thất bại hoặc stale version phải giữ draft; Cancel lặp lại trên phiên đã `CANCELLED` là idempotent.
- OCR có hai điều kiện: executable/model local đạt health `READY` và request chọn `UseOcr`. System Settings quản lý languages/DPI/timeout/resource limit và health nhưng không có switch bật/tắt toàn hệ thống. OCR không có API key, không gửi tài liệu ra cloud và không ghi ảnh/text/đường dẫn tạm vào log.
- Health check chỉ `READY` khi chạy được Tesseract và đủ mọi model trong `OcrLanguages` (mặc định `vie+eng`). Fingerprint gồm provider/path/languages; trạng thái health của cấu hình cũ không được tái sử dụng sau khi cấu hình thay đổi.
- Tesseract chạy LSTM-only `--oem 1`, page segmentation tự động `--psm 3`, trả TSV word-level. Confidence `0..100` được chuẩn hóa về `0..1`; bounding box pixel và page number được giữ trong OCR Snapshot.
- Timeout/cancel phải kết thúc cả process tree. Thư mục tạm riêng theo request luôn bị xóa trong `finally`.
- Một `ImportSession` có nhiều `ImportSourceDocument`; mỗi Group giữ `ImportSourceDocumentId`. Guard chạy độc lập từng file rồi candidate hội tụ về validation/reference/duplicate/dependency toàn phiên.
- File lỗi không bị silent-drop. Phiên vẫn hiển thị preview của file hợp lệ nhưng Confirm bị khóa cho tới khi nguồn lỗi được loại bỏ.
- Cùng business key/payload giữa file dùng `TRÙNG_TRONG_PHIÊN` và mặc định SKIP bản sau. Payload khác dùng `XUNG_ĐỘT_DỮ_LIỆU_GIỮA_CÁC_TỆP` và bắt buộc review.
- Confirm dùng một Idempotency-Key và transaction `Serializable` cho toàn phiên; một lỗi persistence rollback toàn bộ.
