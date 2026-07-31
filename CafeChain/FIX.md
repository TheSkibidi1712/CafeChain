# REFACTOR PHÂN QUYỀN KHO & CUNG ỨNG, SEED RBAC
# VÀ XÂY DỰNG TÀI LIỆU TOÀN BỘ NGHIỆP VỤ AI CAFECHAIN

Bạn hãy đóng vai trò đồng thời là:

- Senior ASP.NET Core MVC Developer.
- Senior Backend Architect.
- Senior Security Engineer chuyên RBAC, Permission và StaffScope.
- Senior SQL Server/EF Core Developer.
- Senior AI Engineer.
- Technical Writer có kinh nghiệm viết tài liệu dự án tốt nghiệp.
- Tester chuyên kiểm thử phân quyền, cross-store access và SQL seed.

Bạn phải inspect trực tiếp source code hiện tại trước khi chỉnh sửa.

Không được chỉ sửa giao diện hoặc thêm seed mang tính mô tả. Permission phải được
thực thi đồng bộ tại:

- Menu.
- View.
- Controller.
- API.
- Application Service.
- StaffScope.
- SeedAll.
- PermissionConstants.
- Test.

======================================================================
I. MỤC TIÊU TỔNG THỂ
======================================================================

Thực hiện ba nhóm công việc:

1. Bổ sung và chuẩn hóa quyền cho chức năng:

   Kho & Cung ứng → Gợi ý nhập hàng.

2. Rà soát toàn bộ nhóm menu Kho & Cung ứng trong Admin Layout, loại bỏ
   các kiểm tra phân quyền hard-code theo role và chuyển sang permission-first.

3. Inspect toàn bộ chức năng AI đang có trong dự án, sau đó tạo:

   - Một tài liệu kỹ thuật và nghiệp vụ AI đầy đủ trong folder Doc.
   - Một tài liệu thuyết trình ngắn gọn phục vụ bảo vệ dự án tốt nghiệp.

Không thêm module nghiệp vụ mới nếu source code hiện tại không có.

Không mô tả một chức năng AI là đã hoàn thiện khi source chỉ mới có giao diện,
stub, mock hoặc thiết kế chưa sử dụng được.

======================================================================
II. FILE VÀ THÀNH PHẦN BẮT BUỘC PHẢI INSPECT
======================================================================

Trước khi sửa, phải kiểm tra tối thiểu:

1. Scripts/SeedAll.sql.
2. Application/Constants/RoleConstants.cs.
3. Application/Constants/PermissionConstants.cs.
4. Các Entity và Configuration:
   - Role.
   - PermissionGroup.
   - Permission.
   - RolePermission.
   - AccountPermissionOverride.
   - StaffScope.
5. Service resolve effective permission.
6. Service resolve StaffScope và store access.
7. Areas/Admin/Views/Shared/_AdminLayout.cshtml.
8. AdminReorderSuggestionsController.
9. AdminRestockRequestsController.
10. Các service/repository của ReorderSuggestion và RestockRequest.
11. Toàn bộ controller được hiển thị trong nhóm menu Kho & Cung ứng.
12. View, partial, modal và JavaScript của các form Kho & Cung ứng.
13. Các authorization filter, attribute, policy và helper hiện có.
14. Toàn bộ service, controller, view và cấu hình liên quan AI.
15. Các tài liệu AI hiện có trong Resources, docs hoặc Doc.
16. Các test liên quan permission, StaffScope, reorder và AI.

Phải lấy cấu trúc source thực tế làm chuẩn.

Không tự tạo controller, service, repository hoặc permission trùng chức năng
với thành phần đang tồn tại.

======================================================================
III. CHỐT NGHIỆP VỤ GỢI Ý NHẬP HÀNG
======================================================================

Thông tin chức năng:

- Menu:
  Kho & Cung ứng → Gợi ý nhập hàng.

- Route xem danh sách:
  GET /Admin/AdminReorderSuggestions/Index

- Permission xem:
  ReorderSuggestion.View

- Permission tạo nháp, tạo mới hoặc bổ sung yêu cầu nhập:
  Restock.Create

Các role được cấp quyền theo yêu cầu mới:

1. Chủ doanh nghiệp.
2. Quản trị hệ thống.
3. Quản lý chi nhánh.

Phải resolve RoleCode từ RoleConstants hoặc dữ liệu role hiện tại.

Không được tự giả định RoleId.

Không hard-code RoleId theo số nếu có thể tra cứu bằng RoleCode.

----------------------------------------------------------------------
1. PHẠM VI DỮ LIỆU
----------------------------------------------------------------------

Quy tắc phạm vi:

- Chủ doanh nghiệp:
  sử dụng Effective StaffScope đã được cấu hình cho tài khoản.

- Quản lý chi nhánh:
  chỉ truy cập các cửa hàng thuộc StaffScope của mình.

- Quản trị hệ thống:
  có global store scope riêng cho chức năng Gợi ý nhập hàng, nhưng chỉ
  bao gồm các cửa hàng đang Active.

Global scope của Quản trị hệ thống trong yêu cầu này chỉ áp dụng cho module
Gợi ý nhập hàng và các action liên quan đã được chốt.

Không được tự mở rộng global business scope của Quản trị hệ thống sang:

- Phiếu kho.
- Đơn đặt hàng.
- Nhận hàng.
- Chuyển kho.
- Điều chỉnh tồn.
- Nhà cung cấp.
- Doanh thu.
- Đơn hàng.
- Các module kinh doanh khác.

Trường hợp muốn dùng global scope cho module khác phải có permission và
nghiệp vụ riêng.

----------------------------------------------------------------------
2. QUY TẮC CHỐNG SỬA STOREID
----------------------------------------------------------------------

Backend không được tin:

- storeId trong URL.
- storeId trong query string.
- storeId trong form.
- storeId trong hidden input.
- storeId trong JSON body.
- danh sách StoreId do JavaScript gửi lên.

Phải thực hiện:

RequestedStoreIds
INTERSECT
EffectiveStoreIds

Đối với Quản lý chi nhánh và Chủ doanh nghiệp:

EffectiveStoreIds phải được resolve từ StaffScope hiện tại.

Đối với Quản trị hệ thống:

EffectiveStoreIds là danh sách Store đang Active do backend truy vấn.

Nếu request chứa Store ngoài EffectiveStoreIds:

- Không được mở rộng scope.
- Không được âm thầm lấy dữ liệu ngoài quyền.
- Trả Forbid hoặc validation error phù hợp.
- Ghi audit khi có dấu hiệu cross-store tampering.

Nếu không truyền storeId:

- Chỉ trả dữ liệu trong EffectiveStoreIds.
- Không mặc định sử dụng toàn bộ Store trong database.

======================================================================
IV. BỔ SUNG PERMISSION VÀ ROLEPERMISSION VÀO SEEDALL
======================================================================

Kiểm tra trong SeedAll xem hai permission sau đã tồn tại chưa:

1. ReorderSuggestion.View
2. Restock.Create

Không được insert trùng nếu permission đã tồn tại.

----------------------------------------------------------------------
1. REORDERSUGGESTION.VIEW
----------------------------------------------------------------------

Thông tin chuẩn:

Code:
ReorderSuggestion.View

Name:
Xem gợi ý nhập hàng

Action:
View

Description:
Xem danh sách gợi ý nhập hàng trong phạm vi cửa hàng được phép truy cập

PermissionGroup:
Sử dụng nhóm Kho & Cung ứng hoặc nhóm phù hợp đang tồn tại trong SeedAll.

Active:
true

Role được cấp:

- Chủ doanh nghiệp.
- Quản trị hệ thống.
- Quản lý chi nhánh.

----------------------------------------------------------------------
2. RESTOCK.CREATE
----------------------------------------------------------------------

Thông tin chuẩn:

Code:
Restock.Create

Name:
Tạo yêu cầu nhập hàng

Action:
Create

Description:
Tạo mới, tạo nháp hoặc bổ sung yêu cầu nhập hàng từ gợi ý nhập hàng
trong phạm vi cửa hàng được phép thao tác

Role được cấp theo yêu cầu mới:

- Chủ doanh nghiệp.
- Quản trị hệ thống.
- Quản lý chi nhánh.

Trước khi cập nhật, phải kiểm tra Restock.Create có đang được sử dụng ở các
module khác hay không.

Không được thay đổi ý nghĩa permission theo cách làm hỏng các action hiện tại.

Nếu Restock.Create đang bảo vệ nhiều action khác nhau, phải báo cáo rõ:

- Action nào đang dùng.
- Role nào bị ảnh hưởng.
- Global scope của SystemAdmin có áp dụng hay không.
- Có cần tách permission chi tiết hơn hay không.

Mặc định, global store scope của SystemAdmin chỉ áp dụng tại
AdminReorderSuggestions và luồng tạo yêu cầu nhập từ gợi ý.

Không tự dùng global scope cho tất cả action có Restock.Create.

----------------------------------------------------------------------
3. SEED ROLEPERMISSION
----------------------------------------------------------------------

Phải tạo danh sách expected role-permission theo business key:

RoleCode + PermissionCode

Ví dụ logic:

- Chủ doanh nghiệp + ReorderSuggestion.View.
- Chủ doanh nghiệp + Restock.Create.
- Quản trị hệ thống + ReorderSuggestion.View.
- Quản trị hệ thống + Restock.Create.
- Quản lý chi nhánh + ReorderSuggestion.View.
- Quản lý chi nhánh + Restock.Create.

Không được hard-code RolePermissionId nếu bảng cho phép resolve bằng khóa
nghiệp vụ.

Phải bảo đảm unique:

RoleId + PermissionId

Chạy SeedAll nhiều lần không được tạo duplicate.

----------------------------------------------------------------------
4. CÁCH VIẾT SEED AN TOÀN
----------------------------------------------------------------------

Block seed phải có cấu trúc tương đương:

SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    -- Resolve PermissionGroup
    -- Upsert Permission
    -- Resolve Role bằng RoleCode
    -- Reconcile RolePermission
    -- Validation

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;

Không kiểm tra tồn tại chỉ bằng PermissionId.

Khóa nghiệp vụ chính của Permission là:

Permission.Code

Khóa nghiệp vụ của role là:

Role.Code hoặc RoleCode thực tế trong source.

Nếu thiếu một role bắt buộc:

- Throw lỗi rõ tên role bị thiếu.
- Không insert RolePermission với RoleId null.
- Không âm thầm tạo một role mới có Code tự nghĩ ra.

----------------------------------------------------------------------
5. ĐỒNG BỘ PERMISSIONCONSTANTS
----------------------------------------------------------------------

Bảo đảm PermissionConstants.cs có:

- ReorderSuggestion.View.
- Restock.Create.

Nếu đã tồn tại thì tái sử dụng.

Không tạo hai constant khác nhau cho cùng một Code.

Không để:

- Seed dùng một chuỗi.
- Controller dùng một chuỗi khác.
- View dùng một chuỗi khác.

Tất cả phải dùng cùng PermissionCode.

======================================================================
V. ÁP DỤNG PERMISSION VÀO ADMINREORDERSUGGESTIONS
======================================================================

----------------------------------------------------------------------
1. ROUTE INDEX
----------------------------------------------------------------------

Route:

GET /Admin/AdminReorderSuggestions/Index

Phải yêu cầu:

ReorderSuggestion.View

Không được chỉ kiểm:

- RequireAdminPanelAccess.
- User.IsInRole(...).
- IsOwner.
- IsSystemAdmin.
- IsStoreManager.
- HasAnyRole.
- Một helper role hard-code tương đương.

Luồng bắt buộc:

Authentication
→ Account status
→ ReorderSuggestion.View
→ Resolve EffectiveStoreIds
→ Validate requested store
→ Query dữ liệu trong scope
→ Render response

----------------------------------------------------------------------
2. NÚT TẠO HOẶC BỔ SUNG YÊU CẦU NHẬP
----------------------------------------------------------------------

UI chỉ hiển thị nút khi người dùng có:

Restock.Create

Backend action tương ứng cũng phải kiểm:

Restock.Create

Không được chỉ ẩn nút ở View.

Người dùng gọi URL hoặc API trực tiếp mà không có permission phải nhận 403.

Action phải tiếp tục kiểm:

- Store thuộc EffectiveStoreIds.
- Reorder suggestion tồn tại.
- Suggestion thuộc đúng Store.
- Ingredient thuộc đúng Store.
- Trạng thái suggestion cho phép tạo yêu cầu.
- Không tạo yêu cầu nhập trùng ngoài ý muốn.
- Không tin quantity hoặc storeId do client gửi nếu backend có dữ liệu chuẩn.

----------------------------------------------------------------------
3. CHỐNG DOUBLE CLICK
----------------------------------------------------------------------

Đối với action tạo hoặc bổ sung yêu cầu nhập:

- Có Antiforgery Token.
- Disable nút khi request đang chạy.
- Có loading state.
- Có RequestKey hoặc idempotency key.
- Backend kiểm tra request trùng.
- Dùng transaction.
- Replay cùng RequestKey không tạo thêm yêu cầu hoặc dòng chi tiết.
- Response lần replay phải trả kết quả trước đó hoặc thông báo phù hợp.
- Không chỉ dựa vào JavaScript để chống double click.

Nếu dự án đã có RequestDeduplication thì phải tái sử dụng.

Không tạo cơ chế chống trùng thứ hai có chức năng tương tự.

======================================================================
VI. RÀ SOÁT TOÀN BỘ ADMIN LAYOUT KHO & CUNG ỨNG
======================================================================

Mở trực tiếp:

Areas/Admin/Views/Shared/_AdminLayout.cshtml

Xác định chính xác tất cả menu con đang nằm trong nhóm:

Kho & Cung ứng

Không được dựa hoàn toàn vào danh sách phỏng đoán.

Sau khi xác định menu, lập bảng audit gồm:

| Menu | Controller | Action | HTTP Method | Permission hiện tại | Role hard-code | StaffScope | Permission chốt |

Tối thiểu phải kiểm tra các module sau nếu chúng thực sự xuất hiện trong nhóm
Kho & Cung ứng hoặc được route từ nhóm này:

- Nguyên liệu.
- Đơn vị và quy đổi.
- Bán thành phẩm.
- Công thức/BOM.
- Lệnh sơ chế.
- Tồn kho cửa hàng.
- Ngưỡng tồn kho.
- Cảnh báo kho.
- Thông báo kho.
- Gợi ý nhập hàng.
- Yêu cầu nhập hàng.
- Đề nghị mua hàng.
- Tổng hợp đề nghị mua.
- PO gộp.
- Đơn đặt hàng.
- Nhận hàng tại chi nhánh.
- Nhà cung cấp.
- Chất lượng nhà cung cấp.
- Phiếu kho.
- Chuyển kho.
- Các form vận hành kho khác đang được render trong Admin Layout.

Nếu một module trong danh sách không nằm trong source hoặc đã bị loại bỏ,
phải ghi rõ, không tự tạo lại.

======================================================================
VII. LOẠI BỎ PHÂN QUYỀN HARD-CODE
======================================================================

Tìm toàn bộ các dạng kiểm tra như:

- User.IsInRole(...).
- User.IsInAnyRole(...).
- HasAnyRole(...).
- IsOwner(...).
- IsSystemAdmin(...).
- IsStoreManager(...).
- IsWarehouseRole(...).
- role == "...".
- switch theo RoleCode.
- danh sách role viết trực tiếp trong controller.
- danh sách role viết trong service.
- kiểm tra role trực tiếp trong Razor.
- kiểm tra role trực tiếp trong JavaScript.
- global bypass theo SystemAdmin.
- chỉ dùng RequireAdminPanelAccess cho action nghiệp vụ.
- helper CanManage hoặc CanWrite được suy ra từ role.

Các kiểm tra trên không được tiếp tục làm cổng phân quyền chính.

Phải chuyển sang:

Permission-first authorization

Ví dụ:

[RequirePermission(PermissionConstants.ReorderSuggestionView)]

hoặc policy/authorization handler tương đương đang có trong dự án.

----------------------------------------------------------------------
1. PHÂN BIỆT PERMISSION VÀ BUSINESS RULE
----------------------------------------------------------------------

Permission trả lời câu hỏi:

“Người dùng có quyền gọi action này không?”

StaffScope trả lời:

“Người dùng được thao tác dữ liệu cửa hàng nào?”

Business rule trả lời:

“Trạng thái nghiệp vụ hiện tại có cho phép chuyển bước không?”

Không dùng role hard-code thay cho ba lớp trên.

Role chỉ được sử dụng tập trung tại:

- Role-to-permission seed.
- Scope resolver đặc biệt đã được chốt.
- Business policy thật sự không thể biểu diễn bằng permission hiện tại.

Không được rải User.IsInRole trong nhiều controller/service.

Trường hợp SystemAdmin có global active-store scope cho ReorderSuggestion,
logic này phải nằm trong một scope resolver hoặc authorization service tập
trung, không viết lại tại từng action.

----------------------------------------------------------------------
2. ACTION GET VÀ POST PHẢI DÙNG QUYỀN RIÊNG
----------------------------------------------------------------------

Không dùng một permission View cho action ghi dữ liệu.

Ví dụ:

- Index/Details:
  *.View

- Create:
  *.Create

- Update:
  *.Update

- Submit:
  *.Submit

- Approve:
  *.Approve

- Confirm:
  *.Confirm

- Cancel:
  *.Cancel

- Receive:
  *.Receive

- Export:
  *.Export

- Toggle:
  *.ToggleStatus

Nếu action hiện tại chưa có permission phù hợp:

1. Xác định action nghiệp vụ thực tế.
2. Đề xuất PermissionCode cụ thể.
3. Bổ sung vào PermissionConstants.
4. Bổ sung Permission vào SeedAll.
5. Gán đúng role.
6. Áp dụng tại Controller/API/View.
7. Viết test.

Không tự dùng quyền gần giống chỉ để tránh tạo permission mới.

----------------------------------------------------------------------
3. KHÔNG XÓA BUSINESS VALIDATION
----------------------------------------------------------------------

Khi bỏ role hard-code, không được làm mất:

- Kiểm tra Store.
- StaffScope.
- Trạng thái chứng từ.
- Quyền truy cập tài nguyên đích.
- Separation of duties.
- Người tạo không tự duyệt nếu nghiệp vụ cấm.
- Chứng từ đã posting không bị hủy trực tiếp.
- Kiểm tra nguyên liệu, supplier, PO hoặc inventory state.
- Audit log.
- Idempotency.

Chỉ thay đổi cách kiểm soát quyền truy cập từ role hard-code sang permission.

======================================================================
VIII. ĐỒNG BỘ MENU VÀ VIEW
======================================================================

----------------------------------------------------------------------
1. NHÓM MENU
----------------------------------------------------------------------

Nhóm Kho & Cung ứng chỉ hiển thị khi người dùng có ít nhất một permission View
của một menu con trong nhóm.

Không hiển thị nhóm cho mọi role có AdminPanelAccess.

----------------------------------------------------------------------
2. MENU GỢI Ý NHẬP HÀNG
----------------------------------------------------------------------

Menu chỉ hiển thị khi có:

ReorderSuggestion.View

Không kiểm trực tiếp role trong Razor.

----------------------------------------------------------------------
3. NÚT TẠO YÊU CẦU NHẬP
----------------------------------------------------------------------

Nút chỉ hiển thị khi có:

Restock.Create

Không dùng:

- IsOwner.
- IsAdmin.
- IsStoreManager.
- CanManage.
- CanWrite.

nếu các biến đó chỉ được suy ra từ role.

----------------------------------------------------------------------
4. BẢO MẬT API
----------------------------------------------------------------------

Ẩn menu hoặc nút không thay thế bảo mật Backend.

Mọi action phải kiểm permission độc lập.

URL trực tiếp không có quyền phải nhận 403.

Không được trả thành công chỉ vì người dùng biết endpoint.

======================================================================
IX. KIỂM TRA STAFFSCOPE
======================================================================

Mọi query của Kho & Cung ứng phải được rà soát:

- Có filter theo EffectiveStoreIds hay không.
- Có tin storeId từ client hay không.
- Có global bypass role hay không.
- Có query dữ liệu trước rồi mới kiểm scope hay không.
- Có leak tên Store hoặc entity ngoài scope hay không.
- Có export toàn bộ Store ngoài scope hay không.

Thứ tự đúng:

1. Resolve actor.
2. Resolve effective permission.
3. Resolve EffectiveStoreIds.
4. Validate requested resource/store.
5. Query dữ liệu theo scope.
6. Thực hiện nghiệp vụ.
7. Audit.

Không nên:

1. Query toàn bộ entity.
2. Trả NotFound hoặc View.
3. Sau đó mới kiểm tra scope.

======================================================================
X. TÀI LIỆU TOÀN BỘ NGHIỆP VỤ AI
======================================================================

Tạo file:

Doc/AI_FEATURES_BUSINESS_AND_TECHNICAL_GUIDE.md

Tên file có thể điều chỉnh theo convention hiện tại, nhưng phải nằm trong
folder Doc và tên phải thể hiện rõ đây là tài liệu AI tổng hợp.

----------------------------------------------------------------------
1. NGUYÊN TẮC VIẾT
----------------------------------------------------------------------

Phải inspect toàn bộ project để xác định chức năng AI thực tế.

Không chỉ mô tả AI Dashboard.

Tìm kiếm tối thiểu các từ khóa:

- AI.
- Ollama.
- Gemini.
- ComfyUI.
- Pexels.
- Prompt.
- Skill.
- Analyst.
- Suggestion.
- Forecast.
- Anomaly.
- Reorder.
- Optimization.
- ImageGeneration.
- Vision.
- Embedding.
- Recommendation.

Tài liệu phải phân biệt rõ trạng thái:

- Đã hoàn thiện và đang sử dụng.
- Đã có code nhưng chưa nối UI.
- Có giao diện nhưng backend chưa hoàn chỉnh.
- Đang dùng fallback.
- Chỉ là thiết kế hoặc tài liệu.
- Legacy/deprecated.
- Ngoài phạm vi hiện tại.

Không được gom mọi file có chữ AI thành một chức năng đang hoạt động.

----------------------------------------------------------------------
2. CẤU TRÚC FILE AI_FEATURES_BUSINESS_AND_TECHNICAL_GUIDE.MD
----------------------------------------------------------------------

Tài liệu phải có tối thiểu các phần:

# 1. Giới thiệu

- Mục tiêu sử dụng AI trong CafeChain.
- Phạm vi AI.
- AI hỗ trợ người dùng, không tự thay thế quyết định nghiệp vụ.
- Các giới hạn hiện tại.

# 2. Danh sách chức năng AI

Lập bảng:

| STT | Chức năng | Form/Module | Người dùng | Input | Output | Provider | Trạng thái |

Mỗi chức năng phải ghi đúng source.

Ví dụ các nhóm cần kiểm tra, không được mặc định là đã tồn tại:

- AI Dashboard.
- Phân tích doanh thu.
- Phân tích đơn hàng.
- Top sản phẩm.
- Phân tích tồn kho.
- Gợi ý nhập hàng.
- Phát hiện bất thường.
- Đánh giá nhà cung cấp.
- Gợi ý đồ uống.
- Gợi ý Size.
- Gợi ý Topping.
- Tạo nội dung.
- Lấy ảnh từ Pexels.
- Tạo ảnh bằng ComfyUI.
- Tối ưu lịch làm việc.
- Dự báo.
- Các AI khác tìm thấy trong source.

# 3. Kiến trúc AI tổng thể

Mô tả:

Controller
→ AI Application Service
→ Skill/Rule Loader
→ Evidence/Data Service
→ AI Provider
→ Validation
→ Fallback
→ Response DTO
→ View

Có thể dùng Mermaid diagram nếu phù hợp.

# 4. Provider và tích hợp

Với từng provider thực tế:

- Vai trò.
- Base URL/config.
- Request.
- Response.
- Timeout.
- Retry.
- Health check.
- Fallback.
- Giới hạn.
- Bảo mật.

Không ghi API key hoặc secret thật vào tài liệu.

# 5. Skill và rule

Ghi rõ:

- Folder lưu skill.
- Cách load skill.
- Prompt system.
- Business rule.
- Validation.
- Cách version skill.
- Điều xảy ra khi thiếu skill.
- Cách chống prompt injection.

# 6. Luồng xử lý từng chức năng AI

Mỗi chức năng phải có:

1. Mục đích.
2. Role/permission sử dụng.
3. Input.
4. Validation input.
5. Cách lấy dữ liệu.
6. StaffScope.
7. Prompt hoặc rule.
8. Provider được gọi.
9. Validate output.
10. Fallback.
11. Kết quả hiển thị.
12. Trường hợp lỗi.
13. Log/audit.
14. Giới hạn.

# 7. AI Dashboard

Mô tả đầy đủ:

- BusinessIntent.
- AnswerFocus.
- DynamicFocus nếu đã có.
- DataPlan.
- EvidencePack.
- ChartPlan.
- AnalysisContext.
- DataStatus.
- StaffScope.
- Fallback.
- Các câu hỏi mẫu.
- Ý nghĩa từng biểu đồ.
- Cách chống AI trả lời lạc đề.

# 8. AI gợi ý nhập hàng

Phải phân biệt:

- Rule-based reorder calculation.
- Forecast nếu có.
- AI diễn giải.
- Tạo yêu cầu nhập hàng.
- Permission.
- StaffScope.
- Số lượng đề xuất.
- Lead time.
- Package quantity.
- Supplier.
- Unit conversion.
- Fallback.

Không được gọi một phép tính deterministic là mô hình AI nếu source không
dùng AI.

# 9. AI tạo nội dung và hình ảnh

Nếu dự án có Pexels/ComfyUI:

- Luồng tạo prompt.
- Tìm ảnh Pexels.
- Chấm điểm độ phù hợp.
- Điều kiện chuyển sang ComfyUI.
- Workflow ComfyUI.
- Node mapping.
- Timeout.
- Validate file.
- Lưu Cloudinary/local storage.
- Fallback khi lỗi.
- Bản quyền và nguồn ảnh.

# 10. Validation và bảo mật

Bao gồm:

- Permission.
- StaffScope.
- Prompt injection.
- Output schema.
- Evidence validation.
- File validation.
- URL validation.
- Timeout.
- Size limit.
- Rate limit.
- Không gửi dữ liệu ngoài scope.
- Không log secret.
- Không cho LLM thực thi SQL.
- Chống double click.
- Request deduplication.

# 11. Hướng dẫn sử dụng

Với mỗi chức năng:

- Vào menu nào.
- Chọn filter gì.
- Nhập dữ liệu gì.
- Bấm nút nào.
- Cách đọc kết quả.
- Cách đọc biểu đồ.
- Cách xử lý khi AI lỗi.
- Quyền cần có.

# 12. Xử lý lỗi

Lập bảng:

| Lỗi | Nguyên nhân | Cách hệ thống xử lý | Cách người dùng xử lý |

Bao gồm:

- Ollama không chạy.
- Timeout.
- JSON sai schema.
- Không đủ dữ liệu.
- Không có quyền.
- Store ngoài scope.
- ComfyUI lỗi.
- Pexels không tìm thấy ảnh.
- File quá lớn.
- Provider không phản hồi.

# 13. Kiểm thử

- Unit test.
- Integration test.
- Authorization test.
- StaffScope test.
- Prompt injection test.
- Fallback test.
- Output schema test.
- Image generation test nếu có.
- Cross-store test.

# 14. Hạn chế và hướng phát triển

Phải phân biệt:

- Hạn chế source hiện tại.
- Hướng cải tiến đề xuất.
- Không trình bày hướng phát triển như chức năng đã hoàn thiện.

# 15. Danh sách file nguồn

Liệt kê các file chính đã đối chiếu.

======================================================================
XI. TÀI LIỆU THUYẾT TRÌNH BẢO VỆ
======================================================================

Tạo file:

Doc/AI_PROJECT_DEFENSE_PRESENTATION.md

Đây là tài liệu dùng để thuyết trình, không phải bản sao rút gọn máy móc của
tài liệu kỹ thuật.

Mục tiêu:

- Thuyết trình trong khoảng 5–7 phút.
- Ngôn ngữ dễ hiểu.
- Tập trung vào giá trị nghiệp vụ.
- Không quá nhiều code.
- Có thể dùng làm kịch bản trình bày hoặc chuyển thành slide sau này.

----------------------------------------------------------------------
1. CẤU TRÚC ĐỀ XUẤT
----------------------------------------------------------------------

# Slide 1 — Giới thiệu

- Tên đề tài.
- Vấn đề CafeChain cần giải quyết.
- Vì sao tích hợp AI.

# Slide 2 — Các chức năng AI chính

Chỉ chọn các chức năng thực sự đã triển khai.

Mỗi chức năng mô tả trong một câu.

# Slide 3 — Kiến trúc tổng thể

Mô tả ngắn:

Dữ liệu nghiệp vụ
→ Bộ kiểm tra quyền và phạm vi
→ AI Service
→ Provider
→ Validation
→ Kết quả và fallback

# Slide 4 — AI Dashboard

Trình bày:

- Người dùng đặt câu hỏi.
- Backend lấy dữ liệu đúng StaffScope.
- AI phân tích Evidence.
- Trả đoạn phân tích và biểu đồ.
- Không bịa dữ liệu ngoài hệ thống.

# Slide 5 — Gợi ý nhập hàng

Trình bày:

- Theo dõi tồn kho.
- Tốc độ tiêu thụ.
- Ngưỡng tồn.
- Lead time.
- Số lượng đề xuất.
- Chuyển thành yêu cầu nhập hàng.

Phải nói đúng source:

- Phần nào là rule-based.
- Phần nào thực sự dùng AI.

# Slide 6 — AI nội dung/hình ảnh

Chỉ thêm nếu source thực sự có:

- Gợi ý nội dung.
- Pexels.
- ComfyUI.
- Luồng fallback.

# Slide 7 — Bảo mật và độ tin cậy

Nêu ngắn:

- Permission.
- StaffScope.
- Không tin storeId từ client.
- Evidence-first.
- Output validation.
- Fallback.
- Chống double click.

# Slide 8 — Kết quả đạt được

Nêu các lợi ích thực tế:

- Giảm thời gian đọc dữ liệu.
- Hỗ trợ phát hiện vấn đề.
- Hỗ trợ quyết định nhập hàng.
- Hạn chế thao tác thủ công.
- Vẫn giữ quyền quyết định cho con người.

# Slide 9 — Demo đề xuất

Viết kịch bản demo từng bước:

1. Đăng nhập bằng role phù hợp.
2. Mở AI Dashboard.
3. Chọn Store và kỳ dữ liệu.
4. Chọn câu hỏi.
5. Xem context.
6. Đối chiếu biểu đồ.
7. Mở Gợi ý nhập hàng.
8. Tạo yêu cầu nhập.
9. Minh họa StaffScope hoặc permission.

# Slide 10 — Hạn chế và hướng phát triển

Chỉ nêu 3–5 ý quan trọng.

----------------------------------------------------------------------
2. SPEAKER NOTE
----------------------------------------------------------------------

Mỗi slide phải có:

- Nội dung hiển thị ngắn.
- Phần “Lời thuyết trình đề xuất”.
- Thời lượng dự kiến.
- Điểm cần nhấn mạnh.

Không viết mỗi slide thành một đoạn văn quá dài.

----------------------------------------------------------------------
3. CÂU HỎI HỘI ĐỒNG CÓ THỂ HỎI
----------------------------------------------------------------------

Cuối file, thêm phần:

“Câu hỏi phản biện và cách trả lời”

Tối thiểu gồm:

1. Vì sao dùng AI mà không chỉ dùng stored procedure?
2. Phần nào là AI, phần nào là rule-based?
3. Làm sao chống AI bịa dữ liệu?
4. Nếu Ollama bị lỗi thì hệ thống hoạt động thế nào?
5. AI có truy cập toàn bộ database không?
6. Làm sao bảo vệ dữ liệu giữa các chi nhánh?
7. Vì sao cần biểu đồ cùng đoạn phân tích?
8. Gợi ý nhập hàng được tính như thế nào?
9. SystemAdmin có được xem toàn bộ dữ liệu không?
10. Vì sao vẫn cần người dùng xác nhận kết quả AI?

Câu trả lời phải đúng với source thực tế.

Không phóng đại khả năng hệ thống.

======================================================================
XII. TEST BẮT BUỘC CHO PERMISSION VÀ STAFFSCOPE
======================================================================

Phải bổ sung hoặc cập nhật test cho các trường hợp:

----------------------------------------------------------------------
1. REORDERSUGGESTION.VIEW
----------------------------------------------------------------------

- Chủ doanh nghiệp có quyền mở Index.
- Quản trị hệ thống có quyền mở Index.
- Quản lý chi nhánh có quyền mở Index.
- Role không được cấp quyền nhận 403.
- Account override Deny chặn được dù role đang có grant.
- Gọi URL trực tiếp không có quyền nhận 403.

----------------------------------------------------------------------
2. RESTOCK.CREATE
----------------------------------------------------------------------

- Có permission thì nút được hiển thị.
- Không có permission thì nút không hiển thị.
- Không có permission gọi API trực tiếp nhận 403.
- Replay cùng RequestKey không tạo yêu cầu nhập trùng.
- Double click không tạo hai request.

----------------------------------------------------------------------
3. STAFFSCOPE
----------------------------------------------------------------------

- Quản lý chi nhánh Store A không xem suggestion của Store B.
- Sửa storeId trên URL không mở rộng phạm vi.
- Sửa storeId trong body không mở rộng phạm vi.
- Chủ doanh nghiệp chỉ xem Store thuộc Effective StaffScope.
- SystemAdmin xem được mọi Store Active trong module này.
- SystemAdmin không xem Store Inactive.
- Global scope của SystemAdmin không tự áp dụng sang module khác.
- Export hoặc API lookup cũng phải bị giới hạn giống Index.

----------------------------------------------------------------------
4. SEEDALL
----------------------------------------------------------------------

- Chạy SeedAll lần đầu thành công.
- Chạy SeedAll lần hai không tạo Permission trùng.
- Không tạo RolePermission trùng.
- Permission.Code unique.
- RolePermission unique theo RoleId + PermissionId.
- Ba role mục tiêu có đủ hai permission.
- AccountPermissionOverride không bị thay đổi.
- Không thay đổi các grant không liên quan ngoài phạm vi yêu cầu.

======================================================================
XIII. VALIDATION SQL CUỐI SEED
======================================================================

Sau khi seed, phải kiểm tra:

1. ReorderSuggestion.View tồn tại đúng một dòng.

2. Restock.Create tồn tại đúng một dòng.

3. Hai permission có PermissionGroup hợp lệ.

4. Hai permission đang Active.

5. Ba role mục tiêu tồn tại.

6. Mỗi role có đúng một RolePermission cho từng permission.

7. Không có RolePermission duplicate.

8. Không có Permission.Code duplicate.

9. Không có foreign key mồ côi.

10. AccountPermissionOverride không bị xóa.

In báo cáo cuối seed:

| RoleCode | RoleName | PermissionCode | Granted |

Đồng thời in tổng PermissionCount của từng role sau khi chạy.

======================================================================
XIV. BUILD VÀ KIỂM TRA
======================================================================

Sau khi sửa phải chạy:

1. dotnet restore.
2. dotnet build.
3. dotnet test.
4. Chạy SeedAll trên database test.
5. Chạy lại SeedAll lần thứ hai.
6. Kiểm tra route Index bằng các role mục tiêu.
7. Kiểm tra role không có quyền.
8. Kiểm tra cross-store tampering.
9. Kiểm tra nút Create và API Create.
10. Kiểm tra hai file tài liệu Markdown.

Không tuyên bố test thành công nếu chưa thực sự chạy.

Nếu môi trường không thể chạy:

- Ghi rõ lệnh chưa chạy.
- Ghi nguyên nhân.
- Không ghi “đã kiểm thử thành công”.
- Cung cấp checklist để người dùng tự chạy.

======================================================================
XV. KẾT QUẢ BÀN GIAO
======================================================================

Sau khi hoàn thành, phải báo cáo:

1. Danh sách file đã đọc.

2. Danh sách file đã sửa.

3. Vị trí block SeedAll đã thêm hoặc sửa.

4. PermissionGroup đã sử dụng.

5. Permission đã thêm hoặc cập nhật.

6. RolePermission đã thêm.

7. RolePermission đã thu hồi nếu có.

8. Cách resolve SystemAdmin global active-store scope.

9. Cách bảo vệ StaffScope của role khác.

10. Danh sách role hard-code đã loại bỏ.

11. Danh sách controller/action đã chuyển sang permission.

12. Danh sách permission mới phát hiện còn thiếu.

13. Những PermissionConstants đã bổ sung.

14. Các thay đổi trong _AdminLayout.cshtml.

15. Cách chống double click và request trùng.

16. Các test đã thêm.

17. Kết quả build/test/seed.

18. Đường dẫn hai tài liệu:

    - Doc/AI_FEATURES_BUSINESS_AND_TECHNICAL_GUIDE.md
    - Doc/AI_PROJECT_DEFENSE_PRESENTATION.md

19. Những chức năng AI đang hoàn thiện, chưa hoàn thiện hoặc legacy.

20. Những vấn đề còn tồn tại.

Không được chỉ trả lời:

“Đã thêm quyền và hoàn thành tài liệu.”

Phải ghi rõ:

- File.
- Class.
- Method.
- Route.
- PermissionCode.
- Role.
- StaffScope.
- Validation.
- Test.

======================================================================
XVI. CÁC ĐIỀU CẤM
======================================================================

Không được:

1. Chỉ thêm menu nhưng không bảo vệ route.

2. Chỉ ẩn nút nhưng API vẫn gọi được.

3. Chỉ kiểm role mà không kiểm permission.

4. Chỉ kiểm permission mà bỏ StaffScope.

5. Cho việc sửa storeId mở rộng phạm vi.

6. Cho SystemAdmin thấy Store Inactive.

7. Áp dụng global scope của SystemAdmin sang mọi module kinh doanh.

8. Hard-code RoleId nếu có thể resolve bằng RoleCode.

9. Insert Permission trùng Code.

10. Insert RolePermission trùng.

11. Xóa AccountPermissionOverride.

12. Dùng RequireAdminPanelAccess làm bảo mật duy nhất cho action nghiệp vụ.

13. Giữ User.IsInRole rải rác trong controller/service làm cổng phân quyền.

14. Dùng một permission View cho mọi action ghi dữ liệu.

15. Xóa business validation khi refactor authorization.

16. Tạo lại service hoặc helper đã tồn tại.

17. Ghi secret, API key hoặc connection string vào tài liệu AI.

18. Gọi một phép tính rule-based là mô hình AI khi source không dùng AI.

19. Mô tả tính năng chưa hoàn thiện như chức năng đã chạy ổn định.

20. Tuyên bố đã build/test khi chưa chạy.

======================================================================
XVII. ACCEPTANCE CRITERIA
======================================================================

Công việc chỉ được xem là hoàn thành khi:

1. Menu Gợi ý nhập hàng kiểm ReorderSuggestion.View.

2. Route Index kiểm ReorderSuggestion.View.

3. Nút và action tạo yêu cầu nhập kiểm Restock.Create.

4. Chủ doanh nghiệp, Quản trị hệ thống và Quản lý chi nhánh được seed
   đúng hai permission.

5. Quản lý chi nhánh không thể xem Store ngoài StaffScope.

6. Chủ doanh nghiệp tuân thủ Effective StaffScope hiện tại.

7. SystemAdmin chỉ có global scope trên Store Active trong module Gợi ý
   nhập hàng.

8. Việc sửa storeId không mở rộng quyền.

9. SeedAll chạy lại không tạo duplicate.

10. PermissionConstants, SeedAll, Controller, View và test sử dụng cùng
    PermissionCode.

11. Các form Kho & Cung ứng không còn dùng role hard-code làm cổng
    authorization chính.

12. Mọi action ghi dữ liệu dùng permission phù hợp.

13. UI và API được bảo vệ đồng bộ.

14. Không làm mất business validation hoặc StaffScope.

15. File tài liệu AI tổng hợp đầy đủ chức năng thực tế của dự án.

16. File thuyết trình đủ dùng cho bài trình bày khoảng 5–7 phút.

17. Tài liệu phân biệt rõ chức năng AI, rule-based, fallback và chức năng
    chưa hoàn thiện.

18. Có báo cáo thay đổi, validation và kết quả kiểm thử đầy đủ.