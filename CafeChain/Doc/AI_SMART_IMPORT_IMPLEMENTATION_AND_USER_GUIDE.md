# AI Smart Import CafeChain — hướng dẫn triển khai và sử dụng

## 1. Thành phần đã triển khai

- Model/EF: `ImportSession`, `ImportGroup`, `ImportItem`, `ImportAudit`.
- Migration mới: `20260812143000_AddAISmartImport`; không sửa migration cũ.
- Pipeline: OpenXML guard/parser, region/header detector, schema registry, deterministic mapping, Ollama structured mapping, normalization/validation/reference/duplicate/dependency.
- API cookie Admin + anti-forgery dưới `/api/ai-import`.
- Admin UI: `/Admin/AIImport`.
- RBAC `AI_IMPORT` trong `Scripts/SeedAll.sql`.
- Confirm dùng `IRequestDeduplicationService` và transaction toàn phiên.

## 2. Chuẩn bị môi trường local

### Database

Kiểm tra connection string local rồi chạy:

```powershell
dotnet restore CafeChain/CafeChain.csproj
dotnet ef database update --project CafeChain/CafeChain.csproj
```

Sau đó chạy `CafeChain/Scripts/SeedAll.sql` trên đúng database CafeChain. Script RBAC idempotent: có thể chạy lại; nhóm/quyền/matrix được reconcile theo code ổn định.

Nếu máy không có `dotnet-ef` global, dùng cách quản lý migration hiện tại của team hoặc cài tool đúng phiên bản EF 8. Không xóa database production và không sửa migration đã áp dụng.

### Ollama

1. Cài và chạy Ollama trên máy local.
2. Pull đúng model đã khai báo trong `appsettings.json`, ví dụ:

```powershell
ollama pull qwen2.5:7b
ollama serve
```

3. Cấu hình bằng `appsettings.Local.json`, user secrets hoặc biến môi trường:

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

Không commit API key/secret. Ollama local không cần credential trong cấu hình mặc định. Smart Import vẫn xử lý file chuẩn khi Ollama offline; chỉ vùng/header không chuẩn cần AI và sẽ báo mã lỗi có kiểu để sửa mapping/reanalyze.

### Cấu hình giới hạn

Section `AIImport` trong `appsettings.json` có file/zip/sheet/row/column/cell/region/sample/session/page/confidence limit. Chỉ nâng giới hạn sau khi đánh giá RAM, thời gian request và rủi ro zip bomb.

### Khởi chạy

```powershell
dotnet build CafeChain/CafeChain.csproj
dotnet run --project CafeChain/CafeChain.csproj
```

Mở URL ứng dụng local, đăng nhập Admin và vào sidebar **AI Smart Import**, hoặc mở trực tiếp `/Admin/AIImport`.

## 3. Tài khoản demo và quyền

Mật khẩu demo local/test: `The@1712`.

| Tài khoản | Vai trò | Khả năng mặc định |
|---|---|---|
| `owner@cafechain.vn` | Chủ doanh nghiệp | Upload/Analyze và Confirm cả 5 entity |
| `accountantwarehouse@cafechain.vn` | Kế toán/kho | Upload/Analyze cả 5; Confirm Nguyên liệu và Nhà cung cấp |

Credential trên chỉ dùng local/demo/test, tuyệt đối không dùng production. Production phải thay password, dùng secret management và quy trình cấp quyền chính thức.

Quản trị hệ thống mặc định không có quyền nghiệp vụ Smart Import. Đây là chủ đích least privilege, không phải lỗi sidebar.

## 4. Chuẩn bị Excel

Chỉ dùng `.xlsx`, tối đa 10 MB. Mỗi bảng nên có một hàng header rõ ràng, sau đó là dữ liệu. Có thể có nhiều sheet/vùng.

Header khuyến nghị:

- Category: `CategoryCode`, `Name`, `Icon`, `Active`.
- Drink: `DrinkCode`, `Name`, `Description`, `Category`, `ProductType`.
- Size: `SizeCode`, `Name`, `Description`, `SizeType`.
- Ingredient: `Code`, `Name`, `BaseUnit`.
- Supplier: `Name`, `TaxCode`, `Address`, `Note`, `PrimaryPhone`, `PrimaryContactName`, `PrimaryContactPhone`, `PrimaryContactEmail`, `PrimaryContactPosition`.

Alias tiếng Việt thông dụng như “Mã danh mục”, “Tên đồ uống”, “Đơn vị cơ sở”, “Mã số thuế” được deterministic mapper nhận diện.

Lưu ý:

- Không đặt `StoreId`, `BranchId`, ID database, SQL hoặc lệnh trong Excel.
- Không dùng sheet/dòng/cột ẩn cho dữ liệu cần import; hệ thống bỏ qua và cảnh báo.
- Formula nên có cached value. Hệ thống không tính/chạy formula.
- ProductType và Unit phải active, tồn tại sẵn bằng mã hoặc tên.
- Category có thể nằm trong cùng file và sẽ được tạo trước Drink.
- `SizeType` dùng `Cup` hoặc `Volume`.
- Mã số thuế nhà cung cấp dùng 10 số hoặc dạng `0123456789-001`.

## 5. Quy trình sử dụng UI

### Upload và Analyze

1. Chọn hoặc thả tệp `.xlsx`.
2. Có thể chọn gợi ý entity; nên để **Tự động** với file có header rõ.
3. Chọn **Analyze**.
4. File chuẩn không gọi AI. File mơ hồ mới gọi Ollama với header/vùng/mẫu giới hạn.

### Preview

Kiểm tra:

- tổng dòng/Hợp lệ/Cảnh báo/Lỗi/Cần xem lại/Bỏ qua;
- từng sheet và vùng;
- entity, confidence và mapping;
- raw value → normalized value;
- dòng nguồn và lỗi/cảnh báo.

Nếu entity/mapping sai, chọn lại entity, ánh xạ từng field rồi **Lưu mapping**. Toàn vùng được revalidate và PreviewVersion tăng.

### Sửa dòng

Chọn **Sửa** tại dòng:

- chỉnh normalized value hoặc reference bằng mã/tên;
- chọn xác nhận cảnh báo;
- chọn `SKIP` nếu không muốn tạo;
- với Nhà cung cấp gần trùng, kiểm tra cảnh báo rồi nhập lý do vẫn tạo. Backend tạo warning token đúng policy supplier hiện hữu.

Mỗi lần lưu sẽ revalidate. Nếu người khác/tab khác vừa sửa, UI nhận `PREVIEW_ĐÃ_THAY_ĐỔI` và tải preview mới.

### Confirm

Confirm chỉ bật hợp lệ khi session ở `READY_TO_PREVIEW`. Backend từ chối nếu còn:

- `ERROR`;
- `REVIEW_REQUIRED`;
- `WARNING` chưa xác nhận;
- thiếu quyền Create của bất kỳ entity cần tạo.

JavaScript sinh một `Idempotency-Key` và giữ nguyên key khi retry request. Không tự đổi key khi bị timeout chưa rõ kết quả. Cùng key/cùng payload sẽ nhận lại response cũ; không tạo trùng.

Confirm là nguyên tử toàn phiên. Một lỗi ở bất kỳ dòng nào rollback mọi dữ liệu được tạo trong phiên.

### Cancel và History

- **Cancel** hủy phiên còn ở Preview/Failed bằng conditional transition.
- **Lịch sử** chỉ hiển thị phiên do account hiện tại upload.
- Session hết hạn sau 24 giờ mặc định.

## 6. API contract

| Method | Endpoint | Quyền chính |
|---|---|---|
| `POST` | `/api/ai-import/analyze` | Upload + Analyze |
| `POST` | `/api/ai-import/{id}/reanalyze` | Analyze |
| `GET` | `/api/ai-import/{id}` | View |
| `PATCH` | `/api/ai-import/{id}/groups/{groupId}` | Analyze |
| `PATCH` | `/api/ai-import/{id}/items/{itemId}` | Analyze |
| `POST` | `/api/ai-import/{id}/confirm` | Confirm + entity Create |
| `POST` | `/api/ai-import/{id}/cancel` | Cancel |
| `GET` | `/api/ai-import/history` | History |

Mutation dùng cookie Admin và anti-forgery header `RequestVerificationToken`. Confirm bắt buộc thêm `Idempotency-Key` và body `expectedPreviewVersion`.

## 7. Kiểm tra quyền

Nếu không thấy sidebar hoặc nhận 403:

1. Kiểm tra account active và có role active.
2. Chạy lại `SeedAll.sql` trên đúng database.
3. Kiểm tra permission code `AIImport.View/Upload/Analyze/Confirm/Cancel/History` active.
4. Kiểm tra RolePermission hoặc AccountPermissionOverride deny.
5. Với Confirm, kiểm tra thêm:
   - `Category.Create`
   - `Drink.Create`
   - `Size.Create`
   - `Ingredient.Create`
   - `Supplier.Create`
6. Đăng xuất/đăng nhập lại nếu cookie/permission snapshot cũ.

Không cấp `AIImport.*` cho Quản trị hệ thống chỉ để “cho thấy menu”; phải theo quyết định phân quyền nghiệp vụ.

## 8. Lỗi thường gặp

| Mã/lỗi | Cách xử lý |
|---|---|
| `ĐỊNH_DẠNG_KHÔNG_HỖ_TRỢ` | Chuyển file sang `.xlsx` thật, không chỉ đổi đuôi |
| `FILE_QUÁ_LỚN` | Giảm file hoặc chia theo giới hạn MVP |
| `FILE_BỊ_HỎNG` | Mở/lưu lại bằng Excel; bỏ password protection |
| `DỮ_LIỆU_VƯỢT_GIỚI_HẠN_MVP` | Chia sheet/file; bỏ vùng thừa |
| `OLLAMA_OFFLINE` / `OLLAMA_TIMEOUT` | Kiểm tra `ollama serve`, model, BaseUrl; file chuẩn vẫn không cần AI |
| `AI_OUTPUT_NGOÀI_WHITELIST` | Sửa mapping thủ công hoặc đổi header rõ hơn |
| `REFERENCE_KHÔNG_HỢP_LỆ` | Kích hoạt/tạo sẵn ProductType/Unit; sửa mã/tên Category |
| `PREVIEW_ĐÃ_THAY_ĐỔI` | Tải preview mới, kiểm tra lại rồi thao tác |
| `IDEMPOTENCY_KEY_REUSED` | Không tái dùng key với payload/version khác; thao tác mới cần key mới |
| `PHIÊN_ĐÃ_XỬ_LÝ` | Mở History để xem kết quả, không Confirm lần hai bằng key khác |
| `PREVIEW_CHƯA_SẴN_SÀNG` | Sửa lỗi/review; xác nhận mọi warning hoặc SKIP |
| `KHÔNG_CÓ_QUYỀN_TẠO_ENTITY` | Cấp đúng `*.Create` hoặc tách file cho entity được phép |
| `NHÀ_CUNG_CẤP_GẦN_TRÙNG` | Kiểm tra kết quả tương tự, nhập lý do hợp lệ nếu vẫn tạo |

## 9. Kiểm tra kỹ thuật sau triển khai

```powershell
dotnet build CafeChain/CafeChain.csproj --no-restore --nologo
dotnet test CafeChain.Tests/CafeChain.Tests.csproj --no-build --nologo
```

Với SQL Server integration test, cấu hình `CAFECHAIN_TEST_SQLSERVER_CONNECTION_STRING` theo convention test của repo rồi chạy suite SQL disposable database. Kiểm tra riêng:

- migration xuất hiện trong `__EFMigrationsHistory`;
- `SeedAll.sql` chạy hai lần;
- role counts đúng contract;
- sidebar đúng Owner/Kế toán-kho;
- Confirm cùng key không tạo thêm;
- Confirm/Cancel và hai Confirm song song chỉ một transition thắng;
- Category + Drink rollback toàn phiên khi Drink lỗi;
- unique collision trả mã nghiệp vụ, không lộ `SqlException`.

Baseline trước Smart Import có 0 build error và 677 warning sẵn có. Chỉ đánh giá regression theo error/test; không quy các warning cũ cho Smart Import.
