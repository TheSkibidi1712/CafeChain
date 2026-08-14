# AI Smart Import CafeChain — quy tắc nghiệp vụ

## 1. Mục tiêu và thuật ngữ

AI Smart Import nhập master data toàn hệ thống từ `.xlsx`, `.docx` và PDF có lớp text theo luồng:

`Tài liệu nguồn → Đoạn nguồn → Bản ghi ứng viên → Preview → Bản ghi được tạo`

- **Tài liệu nguồn**: file người dùng upload.
- **Đoạn nguồn**: sheet/region Excel, bảng/đoạn DOCX hoặc page/block PDF.
- **Bản ghi ứng viên**: dữ liệu được trích xuất nhưng chưa được phép ghi database.
- **Evidence**: đoạn nguyên văn chứng minh dữ liệu AI trả về có trong tài liệu.
- **Source locator**: vị trí tổng quát để quay lại nguồn: sheet/row, paragraph/table hoặc page/block/bounding box.
- **Extraction mode**: cách tạo candidate, ví dụ `DOCX_TABLE_DETERMINISTIC` hoặc `PDF_TEXT_AI_EXTRACTION`.
- **Lỗi theo trường**: lỗi schema có `Field`, được gắn trực tiếp vào input tương ứng trong modal sửa dòng.
- **Lỗi cấp dòng**: lỗi/review không gắn với một input cụ thể, ví dụ xung đột payload hoặc confidence thấp; phải được giải thích ở vùng tổng hợp của modal.
- **Xác nhận thủ công**: hành động người dùng mở candidate `REVIEW_REQUIRED`, kiểm tra normalized data và bấm **Lưu và kiểm tra lại**. Hành động này chỉ hoàn tất review do confidence thấp khi toàn bộ validation khác đã đạt.

DOCX/PDF chỉ mở rộng cách lấy dữ liệu nguồn. Mọi candidate tiếp tục đi qua cùng ImportSchema, normalization, validation, reference, duplicate, dependency, PreviewVersion, RBAC, idempotency, transaction và CRUD service như Excel.

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
- PDF không có text, image-only hoặc có vùng ảnh đáng kể mà parser không thể chứng minh đã lấy đủ dữ liệu trả `PDF_CẦN_OCR`. Ngưỡng diện tích ảnh mặc định là 15% diện tích trang và nằm trong `AIImportOptions`. `OcrEnabled=false`; hệ thống không giả vờ dùng model text để đọc ảnh.
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
- `OcrEnabled=false`.

Controller không hard-code 10 MB. Resource filter lấy giới hạn request từ `AIImportOptions`; service luôn kiểm tra lại `IFormFile.Length`.

## 9. Preview, duplicate và state

- `VALID`: hợp lệ.
- `WARNING`: phải xác nhận cảnh báo.
- `ERROR`: không Confirm được.
- `REVIEW_REQUIRED`: mapping/conflict/confidence cần xử lý.
- `SKIPPED`: không tạo.
- `IMPORTED`: đã tạo thành công.

Mọi PATCH group/item kiểm tra `expectedPreviewVersion`, revalidate và tăng `PreviewVersion`. Client stale nhận HTTP 409 `PREVIEW_ĐÃ_THAY_ĐỔI`.

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
- Item lưu raw/normalized data, source trace, locator, evidence, AI/OCR confidence.
- Audit lưu format, extraction mode, `OcrUsed`, `OcrPageCount` và `AiChunkCount` cùng actor/state/result hiện hữu.
- Snapshot text chỉ tồn tại khi session còn cần reanalyze; bị xóa khi `COMPLETED`, `CANCELLED` hoặc `EXPIRED`.
- Raw data/evidence không được ghi vào application log hoặc history response.

Workspace hiện tại đã được tạo lại thành một baseline duy nhất `20260813071843_InitialCreate`; migration này đã chứa trực tiếp toàn bộ cột nguồn tài liệu ở trên. Hai migration được mô tả trong kế hoạch cũ — `20260812160337_InitialCreate` và `20260813062128_AddDocumentSourcesToAIImport` — không còn tồn tại trên filesystem hiện tại.

Vì baseline đã được squash, database development/test cũ phải được tạo lại hoặc có migration chuyển tiếp riêng trước khi deploy. Không được giả định `database update` có thể nâng trực tiếp một database đã ghi migration ID cũ lên baseline mới, và không được tự động xóa database production.
