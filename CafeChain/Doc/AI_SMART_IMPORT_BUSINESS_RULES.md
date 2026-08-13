# AI Smart Import CafeChain — nghiệp vụ thực tế

## 1. Phạm vi MVP

AI Smart Import là luồng Admin nhập master data toàn hệ thống từ `.xlsx` theo chuỗi:

`Upload → Analyze → Preview → sửa/xác nhận → Confirm → Transaction`

Chỉ hỗ trợ tạo mới (`CREATE`) năm loại dữ liệu: Danh mục, Đồ uống, Size, Nguyên liệu và Nhà cung cấp. Không hỗ trợ Topping, giá/ảnh đồ uống, danh sách liên hệ phụ, `UPDATE`, `UPSERT`, `DELETE`, CSV, PDF, OCR hoặc macro.

Năm loại dữ liệu là master data toàn hệ thống. Excel không được cung cấp `StoreId`/`BranchId`; session và idempotency dùng global scope `StoreId = 0`.

## 2. Nguồn sự thật của nghiệp vụ

Smart Import không tự tạo một bộ rule CRUD riêng. Khi Confirm, hệ thống gọi chính các service tạo hiện hữu:

- `IAdminCategoryService.CreateCategoryAsync`
- `IAdminDrinkService.CreateDrinkAsync`
- `IAdminSizeService.CreateSizeAsync`
- `IAdminIngredientService.CreateAsync`
- `IAdminSupplierService.CreateAsync`

Do đó validation cuối, normalization cuối, unique constraint, audit và hành vi supplier duplicate vẫn cùng nguồn với màn hình CRUD. Smart Import bổ sung validation preview sớm, resolve reference, dependency và transaction toàn phiên.

## 3. Schema thực tế

### Danh mục (`Category`)

| Field | Quy tắc |
|---|---|
| `CategoryCode` | Bắt buộc, trim, uppercase khi import, 2–30 ký tự |
| `Name` | Bắt buộc, trim, 2–100 ký tự |
| `Icon` | Không bắt buộc, tối đa 10 ký tự, Unicode hợp lệ và qua `CategoryIconPolicy` |
| `Active` | Không bắt buộc, mặc định `true` |

Trùng chắc chắn nếu mã hoặc tên đã tồn tại. Category có dependency order trước Drink.

### Đồ uống (`Drink`)

| Field | Quy tắc |
|---|---|
| `DrinkCode` | Bắt buộc, trim, uppercase khi import, tối đa 50 ký tự |
| `Name` | Bắt buộc, trim, tối đa 200 ký tự |
| `Description` | Không bắt buộc, tối đa 1.000 ký tự |
| `Category` | Bắt buộc; resolve bằng mã/tên Category active hoặc Category được tạo trước trong cùng phiên |
| `ProductType` | Bắt buộc; resolve bằng mã/tên ProductType active có sẵn |

Trùng chắc chắn nếu mã hoặc tên đã tồn tại. Không import giá và ảnh vì lệnh Create hiện hữu không nhận giá, ảnh không bắt buộc.

### Size (`Size`)

| Field | Quy tắc |
|---|---|
| `SizeCode` | Bắt buộc, trim, uppercase, tối đa 20 ký tự |
| `Name` | Bắt buộc, trim, tối đa 50 ký tự |
| `Description` | Không bắt buộc, tối đa 300 ký tự |
| `SizeType` | Bắt buộc, chỉ `Cup` hoặc `Volume` |

Trùng chắc chắn nếu mã hoặc tên đã tồn tại.

### Nguyên liệu (`Ingredient`)

| Field | Quy tắc |
|---|---|
| `Code` | Bắt buộc, trim, uppercase, tối đa 50 ký tự |
| `Name` | Bắt buộc, trim, tối đa 200 ký tự |
| `BaseUnit` | Bắt buộc; resolve bằng mã/tên Unit active có sẵn |

Trùng chắc chắn nếu mã hoặc tên đã tồn tại. Smart Import không tạo Unit.

### Nhà cung cấp (`Supplier`)

| Field | Quy tắc |
|---|---|
| `Name` | Bắt buộc |
| `PrimaryPhone` | Hotline chính, bắt buộc |
| `PrimaryContactName` | Người liên hệ chính, bắt buộc |
| `TaxCode` | Không bắt buộc; chuẩn 10 chữ số hoặc `10-3` chữ số |
| `Address` | Tối đa 500 ký tự |
| `Note` | Tối đa 1.000 ký tự |
| `PrimaryContactPhone` | Theo giới hạn DB/service hiện hữu |
| `PrimaryContactEmail` | Theo giới hạn DB/service hiện hữu |
| `PrimaryContactPosition` | Theo giới hạn DB/service hiện hữu |

Mã nhà cung cấp do service tự sinh. Mã số thuế trùng là hard duplicate. Các tín hiệu tên, hotline, điện thoại liên hệ, email và địa chỉ là soft duplicate theo `AdminSupplierService`: Preview cảnh báo, người dùng phải kiểm tra và nhập lý do nếu vẫn tạo. Warning token có thời hạn, gắn actor và payload; Confirm dùng token đó, không bỏ qua policy hiện hữu.

## 4. Parser và ranh giới AI

### Backend deterministic

Backend luôn chịu trách nhiệm:

- kiểm tra phần mở rộng/kích thước/gói ZIP OpenXML;
- đọc cell, shared/inline string, number, boolean, date style và cached formula value;
- bỏ qua sheet/dòng/cột ẩn;
- phát hiện vùng/header, schema alias chuẩn, normalization và validation;
- whitelist entity/field, resolve reference, duplicate/dependency;
- quyền, state machine, preview version, idempotency và transaction.

File chuẩn được map deterministic và không gọi Ollama.

### AI/Ollama

Ollama chỉ được gọi khi mapping deterministic chưa đạt ngưỡng tin cậy. Dữ liệu gửi đi chỉ gồm tên sheet, địa chỉ vùng, header và tối đa số dòng mẫu cấu hình. Không gửi credential, secret, raw prompt nội bộ, toàn bộ workbook hoặc ID hệ thống.

`IOllamaClient` hiện hữu nhận JSON Schema. Output bị từ chối khi JSON sai, confidence thấp, entity/field ngoài whitelist, cột nguồn không tồn tại, ID/SQL/lệnh hoặc cấu trúc lạ. Nội dung trong ô Excel được coi là dữ liệu không tin cậy; prompt injection không được thực thi.

AI chỉ đề xuất mapping. Backend luôn quyết định tính hợp lệ và dữ liệu được tạo.

## 5. File guard và giới hạn mặc định

Các giới hạn nằm trong `AIImport` ở `appsettings.json`:

- file 10 MB;
- dữ liệu sau giải nén 100 MB;
- tỷ lệ nén 100:1;
- 20 sheet;
- 10.000 dòng/sheet, 20.000 dòng tổng;
- 100 cột/sheet, 200.000 cell tổng;
- 20 vùng/sheet;
- 20 dòng mẫu AI;
- session hết hạn sau 24 giờ.

Không chạy macro, công thức, external link hoặc embedded command. Công thức chỉ dùng cached value; không có cache thì cảnh báo. Merge dọc chỉ propagate từ ô đầu trong trường hợp chắc chắn; merge mơ hồ chuyển cảnh báo/review.

## 6. Preview, duplicate và warning

- Dòng hợp lệ: `VALID`.
- Dòng có cảnh báo: `WARNING`; phải bật xác nhận trước Confirm.
- Dòng lỗi: `ERROR`; không Confirm được.
- Mapping/reference chưa chắc chắn: `REVIEW_REQUIRED`; không Confirm được.
- Dòng trùng chắc chắn trong file hoặc DB: mặc định `SKIP`/`SKIPPED`.
- Dòng đã tạo: `IMPORTED`.

Mọi PATCH group/item kiểm tra `expectedPreviewVersion`, revalidate và tăng `PreviewVersion`. Client cũ nhận HTTP 409 với `PREVIEW_ĐÃ_THAY_ĐỔI` và phải tải lại.

## 7. State machine

Luồng chuẩn:

`UPLOADED → ANALYZING → VALIDATING → READY_TO_PREVIEW → IMPORTING → COMPLETED`

Trạng thái kết thúc/ngoại lệ: `FAILED`, `CANCELLED`, `EXPIRED`. Claim Confirm và Cancel dùng conditional update để race chỉ có một transition hợp lệ. Session chỉ thuộc actor đã upload; API detail/history không trả session của actor khác.

## 8. Idempotency và transaction

Confirm tái sử dụng `IRequestDeduplicationService.BeginScopedAsync`:

- action: `AIImport.Confirm`;
- `StaffId`: actor thật;
- `AccountId`: account thật;
- `StoreId = 0`;
- `ReferenceId = ImportSessionId`;
- canonical payload: session ID, expected preview version và snapshot lựa chọn/value của item.

Begin idempotency, claim `READY_TO_PREVIEW → IMPORTING`, tạo đủ entity, cập nhật kết quả và `MarkSuccessAsync` cùng nằm trong một transaction `Serializable`.

- Cùng key + cùng payload đã thành công: trả response trước, không tạo lại.
- Cùng key đang xử lý: HTTP 409.
- Cùng key + payload khác: `IDEMPOTENCY_KEY_REUSED`.
- Session completed bằng key khác: `PHIÊN_ĐÃ_XỬ_LÝ`.
- Một lỗi tạo hoặc unique collision: rollback toàn bộ; lỗi SQL không lộ ra client.

Category được tạo trước Drink. Ví dụ Category tạo được nhưng Drink lỗi thì Category cũng bị rollback.

## 9. Quyền

Nhóm `AI_IMPORT` gồm:

- `AIImport.View`
- `AIImport.Upload`
- `AIImport.Analyze`
- `AIImport.Confirm`
- `AIImport.Cancel`
- `AIImport.History`

Seed mặc định cấp sáu quyền cho Chủ doanh nghiệp và Kế toán/kho. Confirm còn kiểm tra quyền `*.Create` của từng entity:

- Chủ doanh nghiệp: mặc định Confirm đủ 5 entity.
- Kế toán/kho: mặc định Analyze đủ 5 nhưng chỉ Confirm Nguyên liệu/Nhà cung cấp.
- Quản lý vùng, Quản lý chi nhánh và Quản trị hệ thống: không được seed Smart Import mặc định.

Quyền Smart Import không thay thế quyền Create nghiệp vụ.

## 10. Audit và dữ liệu nhạy cảm

`ImportAudit` lưu actor, action, trạng thái trước/sau, phiên bản preview/schema/prompt/model, hash idempotency key, mã lỗi và summary kết quả. Không lưu secret, token, raw prompt hoặc credential. Raw/normalized row được lưu trong `ImportItem` để Preview/reanalyze và hết hạn theo session; không đưa dữ liệu này vào log ứng dụng.
