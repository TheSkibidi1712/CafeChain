# HƯỚNG DẪN FRONTEND 2 LÀM LẠI ADMIN UI CAFECHAIN — CHI TIẾT V2


> **Vai trò:** Frontend 2  
> **Tài liệu thao tác:** đặt file này tại `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.  
> **Mục tiêu:** triển khai lại giao diện Admin theo từng đợt có kiểm soát; đồng bộ toàn bộ module, form thêm/sửa/xóa, danh sách, chi tiết, modal và responsive; chỉ push để nhóm trưởng duyệt, không tự merge.


**Ownership chính:** Category, Ingredient, UnitConversion, Dashboard, Inventory/Alerts, Restock/Reorder, Purchase Advice/Batch, Purchase Orders, Branch Receipts, Operational Ice, Staff, Permission, StaffShift, ShiftOptimization, Store, Supplier, SupplierQuality, Profile, Notifications và Settings.

Frontend 2 không được sửa Shared, `_ViewStart`, `_ViewImports` hoặc `admin-unified-depth.css`. Mọi module phải kế thừa Core của Frontend 1 và dùng token `--cc-*`.



## Bộ tài liệu bắt buộc phải đặt trong `docs`

Trước khi dùng Antigravity, bảo đảm trong dự án có đúng các file sau:

```text
docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md
docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md
```

Ngoài ra đặt file hướng dẫn tương ứng của người đang làm vào `docs`.

Quy tắc đọc tài liệu:

- Hai file `CAFECHAIN_ADMIN_UI_UX_FRONTEND_*.md` là **nguồn chuẩn về design system, ownership và danh sách file**.
- File hướng dẫn này là **nguồn chuẩn về thứ tự thực hiện, prompt, kiểm thử, commit và push**.
- Antigravity phải đọc cả hai file đặc tả chính để hiểu ngôn ngữ thiết kế chung, nhưng chỉ được sửa phạm vi của frontend đang phụ trách.
- Nếu source thực tế và danh sách ownership khác nhau, dừng code và báo nhóm trưởng; không tự mở rộng phạm vi.


## Khóa phạm vi tuyệt đối

Mọi đợt trong tài liệu này đều phải tuân thủ đồng thời các điều sau:

1. Chỉ được chỉnh đúng các file `.cshtml` và `.css` được liệt kê trong đợt đang làm.
2. Nghiêm cấm chỉnh `.js`, `.ts`, controller, service, repository, model, view model, DTO, API, middleware, filter, authorization, database, migration, SQL, stored procedure, seed và permission seed.
3. Không thay đổi nghiệp vụ, trạng thái, quyền, StaffScope, route, action, HTTP method, validation, form submit hoặc thứ tự workflow.
4. Không được thêm, xóa, đổi tên, thay thế hoặc di chuyển cấu trúc thẻ HTML/Razor hiện có trong `.cshtml`.
5. Giữ nguyên `id`, `name`, `for`, `value`, `type`, `role`, `aria-*`, `asp-*`, `data-*`, event inline, hidden input, antiforgery, modal/tab/collapse/drawer/table/form ID và mọi JavaScript hook.
6. Không đổi table thành card list; không đổi form full-page thành modal; không đổi modal thành trang; không đổi input/select/textarea thành component khác; không đổi tab thành accordion.
7. Khi thật sự cần thêm class để style, phải giữ toàn bộ class cũ. Không được xóa hoặc rename class chưa chứng minh là thuần visual.
8. Không thêm UI framework, JavaScript library, icon library, font hoặc package mới.
9. Không format hàng loạt `.cshtml`, không đổi line ending và không “dọn code” ngoài phạm vi.
10. Không chỉnh StaffHub, ứng dụng POS riêng, Online, `AdminOrder/History.cshtml`, Voucher hoặc Wheel.
11. Mọi selector phải được scope trong Admin; không dùng global `button`, `input`, `table`, `.card` hoặc `.modal` có thể lan sang khu vực khác.
12. Không được tuyên bố hoàn thành khi chưa có kiểm tra diff, build, desktop, tablet, mobile và regression chức năng.


## Visual contract chung bắt buộc cho cả Frontend 1 và Frontend 2

Không frontend nào được tự tạo phong cách riêng. Tất cả module phải dùng cùng contract sau:

### Màu và surface

| Vai trò | Giá trị |
|---|---|
| Canvas Admin | `#F7F4F0` |
| Surface chính | `#FFFDFB` |
| Surface nổi/modal | `#FFFFFF` |
| Surface phụ/filter | `#FBF7F2` |
| Selected/active | `#F4E9DF` |
| Primary brown | `#70482F` |
| Primary hover | `#3D2418` |
| Heading đậm | `#2B1A12` hoặc `#201812` |
| Text phụ | `#66584F` |
| Border | `#E9DED4` |
| Border control | `#D8C5B6` |
| Success | `#2F6F5E` |
| Warning | `#99623B` |
| Danger | `#991B1B` |
| Info | `#3F5F7A` |

Không dùng caramel làm body text nhỏ. Không dùng nâu thay cho màu trạng thái.

### Typography và chiều sâu

- Giữ font `Inter` hiện có.
- Page title desktop `32–38px`, mobile `24–28px`, weight `800`.
- Section title `18–20px`; card title `15–16px`; body `14px`; form label `13px`; table header `12px`.
- Page header phải nổi bật bằng khoảng trắng, accent trái, border và shadow mềm; không dùng text-shadow nặng.
- Tiêu đề, label và nội dung chính phải có tương phản cao; cấm chữ nâu nhạt trên nền kem làm chìm nội dung.

### Kích thước component

| Component | Chuẩn |
|---|---|
| Button nhỏ | `36px` |
| Button mặc định | `44px` |
| Button lớn | `48px` |
| Icon button | `40×40px` |
| Table action | `32–36px` |
| Input/select | `44px` |
| Textarea | tối thiểu `108–120px` |
| Control radius | `10px` |
| Card radius | `16px` |
| Header radius | `20px` |
| Modal radius | `18px` |
| Card padding | `20–24px` |
| Table row | `52px`, compact `46–48px` |

### Quy chuẩn loại trang

**Index/List:** page header → summary/KPI nếu có → filter → table/list → pagination. Nút thêm mới là primary; lọc và reset không được cạnh tranh CTA.

**Create/Edit:** cùng một form visual contract, cùng label/control/validation/section/action footer. Desktop tối đa 2 cột khi hợp lý; mobile 1 cột. Save là primary; Cancel/Back là secondary.

**Delete/Reject/Cancel có hậu quả:** chỉ dùng giao diện modal/confirm hiện có; danger rõ nhưng không đổi hook hoặc flow. Đóng modal và quay lại không được dùng danger.

**Details/Approval:** mã và trạng thái ở trên; summary rõ; bảng dòng chi tiết căn số sang phải; action chuyển trạng thái tách khỏi navigation.

**Modal:** giữ nguyên ID và `data-bs-*`; body cuộn; footer nhìn thấy; nút cùng chiều cao; close có vùng click rõ.

**Empty/loading/error:** phải đồng bộ typography, spacing và semantic color; no-data không dùng danger.

### Responsive và accessibility

Bắt buộc kiểm tra `1440×900`, `1280×720`, `1024×768`, `768×1024`, `390×844`, zoom `100%` và `125%`.

- Table giữ DOM và cuộn ngang trong wrapper.
- Header action wrap, không che title.
- Form 2 cột → 1 cột.
- Modal không mất footer.
- Không ẩn cột hoặc action nghiệp vụ.
- `:focus-visible` rõ; contrast body text đạt WCAG AA; trạng thái không chỉ dựa vào màu.


## Quy trình Git và duyệt bắt buộc

### Trước mỗi đợt

```bash
git checkout develop
git pull origin develop
git status --short
```

Chỉ tạo branch mới khi working tree sạch. Mỗi đợt dùng một branch riêng theo tên tài liệu quy định.

### Sau khi code

```bash
git status --short
git diff --name-only
git diff --stat
git diff --check
dotnet build
```

Kiểm tra riêng `.cshtml`:

```bash
git diff --word-diff=plain -- "*.cshtml"
```

Frontend phải đọc diff để xác nhận thay đổi `.cshtml` chỉ liên quan class/style visual được phép, không đổi thẻ, binding hoặc hook.

### Commit và push

- Stage từng file chính xác; cấm `git add .`.
- Xem `git diff --cached` trước commit.
- Chỉ `git push`; không tự merge vào `develop`.
- Không force-push trừ khi nhóm trưởng yêu cầu.
- Sau khi push, gửi branch, commit hash, file đã chỉnh, ảnh trước/sau, test đã chạy và rủi ro còn lại cho nhóm trưởng.
- Đợt phụ thuộc chỉ bắt đầu khi nhóm trưởng xác nhận nhánh trước đã được merge vào `develop`.


# Thứ tự thực hiện bắt buộc

1. AUDIT TỔNG VÀ CHỜ CORE FE1
2. MASTER DATA — CATEGORY, INGREDIENT VÀ UNIT CONVERSION
3. DASHBOARD ADMIN — 6 NHÓM DỮ LIỆU VÀ AI
4. INVENTORY CORE — TỒN KHO, NGƯỠNG, CẢNH BÁO VÀ BẤT THƯỜNG
5. RESTOCK REQUEST VÀ REORDER SUGGESTIONS
6. PURCHASE ADVICE, CONSOLIDATION VÀ ORDER BATCH
7. PURCHASE ORDERS — CREATE, INDEX VÀ DETAILS
8. BRANCH RECEIPTS — NHẬN HÀNG VÀ XÁC NHẬN THỰC NHẬN
9. QUẢN LÝ ĐÁ THEO CA — INDEX, DETAILS VÀ REPORT
10. STAFF VÀ PERMISSION
11. STAFF SHIFT VÀ SHIFT OPTIMIZATION
12. STORE MANAGEMENT — INDEX, CREATE, EDIT VÀ MAP
13. SUPPLIER VÀ SUPPLIER QUALITY
14. PROFILE, NOTIFICATIONS VÀ SETTINGS
15. FINAL REGRESSION FRONTEND 2 VÀ ĐỐI CHIẾU TOÀN ADMIN

Không bỏ qua audit, verification hoặc dependency. Không gom nhiều đợt vào một prompt và không yêu cầu Antigravity “làm hết giao diện” trong một lần.

Frontend 2 chỉ audit trước. Mọi implementation bị chặn cho tới khi nhóm trưởng xác nhận Core Frontend 1 đã merge vào `develop`.


# ĐỢT 1 — AUDIT TỔNG VÀ CHỜ CORE FE1

## Mục tiêu

- Đọc hai đặc tả chính, xác nhận ownership 57 view/19 CSS.
- Chụp baseline Dashboard, Master Data, Inventory, Procurement, Ice, HR, Store và System.
- Xác định selector/hook và dependency với Core FE1.

## Quy tắc

- Đây là audit read-only. Không tạo diff và không sửa source.
- Không dùng Prompt B/Prompt C trong đợt này.
- Chỉ chuyển sang đợt triển khai khi audit kết luận phạm vi rõ và dependency đã đáp ứng.

## Prompt audit bắt buộc

```text
Bạn đang làm Frontend 2 của CafeChain.

BẮT BUỘC đọc đầy đủ, theo thứ tự:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Toàn bộ source của các file được liệt kê cho đợt `AUDIT TỔNG VÀ CHỜ CORE FE1`.

ĐÂY CHỈ LÀ PHASE A — AUDIT READ-ONLY. TUYỆT ĐỐI CHƯA SỬA CODE.

Phạm vi đợt này:
- Toàn bộ file thuộc ownership chỉ được đọc; không file nào được phép chỉnh trong đợt audit.

Hãy trả báo cáo theo đúng cấu trúc:

A. Scope confirmation
- Liệt kê đúng từng file được phép chỉnh.
- Liệt kê file liên quan đã đọc nhưng không được chỉnh.
- Xác nhận không sửa file ownership của frontend còn lại.

B. DOM và functional freeze
- Liệt kê form, table, modal, tab, collapse, partial, hidden input và dynamic region hiện có.
- Liệt kê `id`, `name`, `data-*`, `asp-*`, class/hook có khả năng được JS/Bootstrap/Select2/chart/map/drag-drop sử dụng.
- Nêu chính xác phần nào trong `.cshtml` chỉ được bổ sung class và phần nào phải để nguyên hoàn toàn.

C. Current UI audit
- Page header hiện tại.
- Button variants và kích thước hiện tại.
- Form control, validation, Select2 hiện tại.
- Table/list/pagination hiện tại.
- Modal/confirm/empty/loading/error hiện tại.
- Màu, token, spacing, radius, shadow và `!important` bị trùng hoặc lệch.

D. Target mapping
- Lập bảng `existing selector → visual contract --cc-* → file CSS sẽ chỉnh`.
- Chỉ ra selector nào dùng CSS chung và selector nào phải giữ exception nghiệp vụ.
- Mô tả cách làm Index/Create/Edit/Details/Delete/Modal đồng bộ mà không đổi DOM.

E. Risk register
- Specificity và thứ tự load CSS.
- Inline style và `<style>` cục bộ.
- Plugin control.
- Responsive table/modal.
- Nguy cơ selector lan sang StaffHub/POS/module frontend còn lại.

F. Test plan
- Trang và luồng cần test trước/sau.
- Viewport cần chụp.
- Functional regression bắt buộc.

Kết luận bằng một trong hai trạng thái:
- `READY FOR IMPLEMENTATION`: phạm vi rõ, không cần sửa JS/backend/DOM.
- `BLOCKED`: ghi đúng vấn đề cần nhóm trưởng giải quyết.

Sau báo cáo phải dừng. Không được chỉnh code hoặc tuyên bố đã hoàn thành giao diện.
```

## Baseline cần lưu ngoài source

- Xác nhận baseline build pass.
- Xác nhận Core FE1 chưa merge thì trạng thái là BLOCKED FOR IMPLEMENTATION.

Chạy:

```bash
git checkout develop
git pull origin develop
git status --short
git rev-parse HEAD
dotnet build
```

**Không commit và không push source trong đợt audit.** Báo cáo audit gửi trực tiếp cho nhóm trưởng.


# ĐỢT 2 — MASTER DATA — CATEGORY, INGREDIENT VÀ UNIT CONVERSION

## Điều kiện bắt đầu

Core FE1 đã được nhóm trưởng merge vào `develop`.

- Nhóm trưởng đã xác nhận các dependency cần thiết đã merge vào `develop`.
- `git status --short` sạch.
- Branch phải được tạo mới từ `develop` vừa pull.

```bash
git checkout develop
git pull origin develop
git checkout -b feature/admin-ui-fe2-master-data
```

**Mức rủi ro:** Cao — CRUD/modal và validation.

## File được phép chỉnh

- `Areas/Admin/Views/AdminCategory/Create.cshtml`
- `Areas/Admin/Views/AdminCategory/Edit.cshtml`
- `Areas/Admin/Views/AdminCategory/Index.cshtml`
- `Areas/Admin/Views/AdminIngredient/Index.cshtml`
- `Areas/Admin/Views/AdminUnitConversion/Create.cshtml`
- `Areas/Admin/Views/AdminUnitConversion/Edit.cshtml`
- `Areas/Admin/Views/AdminUnitConversion/Index.cshtml`
- `wwwroot/css/Admin/Category/Category.css`
- `wwwroot/css/Admin/Ingredient/ingredient.css`
- `wwwroot/css/unit-conversion.css`

Cấm chỉnh file ngoài danh sách này. File liên quan khác chỉ được đọc để hiểu hook và visual context.

## Kết quả giao diện phải đạt

- Dùng Master Data làm phép thử đầu tiên cho Core FE1.
- Category, Ingredient và Unit Conversion phải cùng header/form/table/modal contract.
- Đồng bộ đầy đủ Index/Create/Edit/Delete/validation.

## Quy chuẩn chi tiết theo loại trang

- Index: header, filter, table, action, badge, pagination/empty.
- Create/Edit: cùng form sections, controls, validation và action footer.
- Modal Ingredient/Delete giữ ID, body/footer và semantics.
- Unit conversion numeric/unit fields căn đúng và dễ đọc.

## Phần phải bảo toàn tuyệt đối

- Category/Ingredient modal IDs và scripts.
- Unit codes, conversion values, `asp-*`, hidden IDs và validation.
- Không sửa Core; lỗi token phải handoff FE1.

## Prompt A — Audit read-only

Sao chép nguyên khối:

```text
Bạn đang làm Frontend 2 của CafeChain.

BẮT BUỘC đọc đầy đủ, theo thứ tự:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Toàn bộ source của các file được liệt kê cho đợt `MASTER DATA — CATEGORY, INGREDIENT VÀ UNIT CONVERSION`.

ĐÂY CHỈ LÀ PHASE A — AUDIT READ-ONLY. TUYỆT ĐỐI CHƯA SỬA CODE.

Phạm vi đợt này:
- `Areas/Admin/Views/AdminCategory/Create.cshtml`
- `Areas/Admin/Views/AdminCategory/Edit.cshtml`
- `Areas/Admin/Views/AdminCategory/Index.cshtml`
- `Areas/Admin/Views/AdminIngredient/Index.cshtml`
- `Areas/Admin/Views/AdminUnitConversion/Create.cshtml`
- `Areas/Admin/Views/AdminUnitConversion/Edit.cshtml`
- `Areas/Admin/Views/AdminUnitConversion/Index.cshtml`
- `wwwroot/css/Admin/Category/Category.css`
- `wwwroot/css/Admin/Ingredient/ingredient.css`
- `wwwroot/css/unit-conversion.css`

Hãy trả báo cáo theo đúng cấu trúc:

A. Scope confirmation
- Liệt kê đúng từng file được phép chỉnh.
- Liệt kê file liên quan đã đọc nhưng không được chỉnh.
- Xác nhận không sửa file ownership của frontend còn lại.

B. DOM và functional freeze
- Liệt kê form, table, modal, tab, collapse, partial, hidden input và dynamic region hiện có.
- Liệt kê `id`, `name`, `data-*`, `asp-*`, class/hook có khả năng được JS/Bootstrap/Select2/chart/map/drag-drop sử dụng.
- Nêu chính xác phần nào trong `.cshtml` chỉ được bổ sung class và phần nào phải để nguyên hoàn toàn.

C. Current UI audit
- Page header hiện tại.
- Button variants và kích thước hiện tại.
- Form control, validation, Select2 hiện tại.
- Table/list/pagination hiện tại.
- Modal/confirm/empty/loading/error hiện tại.
- Màu, token, spacing, radius, shadow và `!important` bị trùng hoặc lệch.

D. Target mapping
- Lập bảng `existing selector → visual contract --cc-* → file CSS sẽ chỉnh`.
- Chỉ ra selector nào dùng CSS chung và selector nào phải giữ exception nghiệp vụ.
- Mô tả cách làm Index/Create/Edit/Details/Delete/Modal đồng bộ mà không đổi DOM.

E. Risk register
- Specificity và thứ tự load CSS.
- Inline style và `<style>` cục bộ.
- Plugin control.
- Responsive table/modal.
- Nguy cơ selector lan sang StaffHub/POS/module frontend còn lại.

F. Test plan
- Trang và luồng cần test trước/sau.
- Viewport cần chụp.
- Functional regression bắt buộc.

Kết luận bằng một trong hai trạng thái:
- `READY FOR IMPLEMENTATION`: phạm vi rõ, không cần sửa JS/backend/DOM.
- `BLOCKED`: ghi đúng vấn đề cần nhóm trưởng giải quyết.

Sau báo cáo phải dừng. Không được chỉnh code hoặc tuyên bố đã hoàn thành giao diện.
```

Chỉ chuyển sang Prompt B khi báo cáo kết thúc bằng `READY FOR IMPLEMENTATION` và frontend đã đọc, đối chiếu đúng file/hook.

## Prompt B — Triển khai chính thức

```text
Bạn đang làm Frontend 2 của CafeChain.

BẮT BUỘC đọc lại:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Báo cáo audit Phase A vừa được duyệt.

PHASE B — CHỈ TRIỂN KHAI ĐỢT `MASTER DATA — CATEGORY, INGREDIENT VÀ UNIT CONVERSION`.

CHỈ ĐƯỢC CHỈNH:
- `Areas/Admin/Views/AdminCategory/Create.cshtml`
- `Areas/Admin/Views/AdminCategory/Edit.cshtml`
- `Areas/Admin/Views/AdminCategory/Index.cshtml`
- `Areas/Admin/Views/AdminIngredient/Index.cshtml`
- `Areas/Admin/Views/AdminUnitConversion/Create.cshtml`
- `Areas/Admin/Views/AdminUnitConversion/Edit.cshtml`
- `Areas/Admin/Views/AdminUnitConversion/Index.cshtml`
- `wwwroot/css/Admin/Category/Category.css`
- `wwwroot/css/Admin/Ingredient/ingredient.css`
- `wwwroot/css/unit-conversion.css`

MỤC TIÊU NGHIỆP VỤ VÀ GIAO DIỆN:
- Dùng Master Data làm phép thử đầu tiên cho Core FE1.
- Category, Ingredient và Unit Conversion phải cùng header/form/table/modal contract.
- Đồng bộ đầy đủ Index/Create/Edit/Delete/validation.

QUY CHUẨN THEO TRANG/FORM:
- Index: header, filter, table, action, badge, pagination/empty.
- Create/Edit: cùng form sections, controls, validation và action footer.
- Modal Ingredient/Delete giữ ID, body/footer và semantics.
- Unit conversion numeric/unit fields căn đúng và dễ đọc.

PHẦN PHẢI ĐÓNG BĂNG:
- Category/Ingredient modal IDs và scripts.
- Unit codes, conversion values, `asp-*`, hidden IDs và validation.
- Không sửa Core; lỗi token phải handoff FE1.

RÀNG BUỘC TUYỆT ĐỐI:
- Chỉ chỉnh `.cshtml` và `.css` nêu trên.
- Không chỉnh JavaScript, backend, database, SQL, migration, seed, permission hoặc nghiệp vụ.
- Không thêm/xóa/đổi tên/di chuyển cấu trúc thẻ `.cshtml`.
- Giữ nguyên `id`, `name`, `data-*`, `asp-*`, route, action, method, hidden input, validation, modal/tab/collapse và mọi hook.
- Không thêm framework/package/font/script.
- Không format hàng loạt.
- Không dùng selector global lan khỏi Admin.
- Không tạo design token cạnh tranh với `--cc-*`.
- Không dùng `!important` nếu selector đúng có thể giải quyết; exception phải có comment module và lý do.
- Không chỉnh Online, Voucher, Wheel, StaffHub hoặc POS riêng.

CÁCH TRIỂN KHAI BẮT BUỘC:
1. Giữ CSS module cho bố cục đặc thù; dùng token `--cc-*` để đồng bộ màu, type, spacing, radius và shadow.
2. Ưu tiên CSS selector tương thích với markup hiện tại; `.cshtml` chỉ bổ sung class visual khi CSS hiện tại không thể xử lý an toàn.
3. Đồng bộ đầy đủ normal/hover/active/focus/disabled/loading/error/empty state.
4. Đồng bộ Index/Create/Edit/Details/Delete/Modal trong phạm vi; không chỉ sửa trang Index.
5. Sửa responsive ngay trong đợt, không để dành toàn bộ đến cuối.
6. Sau mỗi nhóm selector, kiểm tra một trang đại diện để phát hiện conflict sớm.

KIỂM TRA BẮT BUỘC SAU KHI CODE:
- Category Create/Edit/Delete/Toggle.
- Ingredient modal/form/action.
- UnitConversion Create/Edit/Delete và validation.
- Responsive table/form/modal.

Trước khi kết luận, chạy và báo kết quả:
- `git diff --name-only`
- `git diff --stat`
- `git diff --check`
- `dotnet build`
- kiểm tra diff `.cshtml` để xác nhận không thay DOM/hook/binding.

OUTPUT CUỐI CÙNG PHẢI CÓ:
1. Danh sách chính xác file đã chỉnh và file chỉ đọc.
2. Tóm tắt thay đổi theo từng file.
3. Bảng selector cũ → contract mới.
4. Danh sách trạng thái UI đã hoàn thiện.
5. Danh sách viewport và luồng đã test, kèm kết quả đạt/chưa đạt.
6. Xác nhận không chỉnh file bị cấm, JavaScript, backend, database, seed, nghiệp vụ hoặc DOM.
7. CSS exception/`!important` còn lại và lý do.
8. Lỗi/rủi ro chưa xử lý; không được che giấu.
9. Kết luận chỉ được là `PASS — READY TO COMMIT` hoặc `FAIL — DO NOT COMMIT`.

Không được tuyên bố PASS nếu chưa kiểm tra toàn bộ mục trên.
```

## Prompt C — Kiểm tra độc lập sau khi làm

```text
Hãy thực hiện PHASE C — INDEPENDENT VERIFICATION cho đợt `MASTER DATA — CATEGORY, INGREDIENT VÀ UNIT CONVERSION`. Không sửa thêm code trong bước này.

Đọc:
- hai file đặc tả chính trong `docs`;
- `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`;
- toàn bộ `git diff` hiện tại.

Kiểm tra độc lập:
1. File thay đổi có đúng ownership và đúng danh sách đợt không.
2. Có file `.js`, `.ts`, `.cs`, `.sql`, migration, seed hoặc backend nào thay đổi không.
3. `.cshtml` có thay cấu trúc thẻ, thứ tự DOM, `id`, `name`, `asp-*`, `data-*`, form action/method, hidden input hoặc hook không.
4. Index/Create/Edit/Details/Delete/Modal trong phạm vi đã dùng cùng visual contract chưa.
5. Button, input, table, modal, badge, page header và validation có cùng kích thước/semantics chưa.
6. Có selector global hoặc `!important` không cần thiết gây conflict không.
7. Desktop/tablet/mobile/zoom có overflow, chữ chìm, action bị che hoặc modal mất footer không.
8. Các chức năng sau còn hoạt động nguyên vẹn không:
- Category Create/Edit/Delete/Toggle.
- Ingredient modal/form/action.
- UnitConversion Create/Edit/Delete và validation.
- Responsive table/form/modal.
9. `dotnet build`, `git diff --check` có pass không.
10. So sánh ảnh trước/sau; chỉ ra trang nào còn lệch với module đã chuẩn hóa.

Trả kết quả dạng bảng: `Tiêu chí | PASS/FAIL | Bằng chứng | Việc cần sửa`.
Kết luận duy nhất:
- `VERIFIED PASS — MAY COMMIT AND PUSH`, hoặc
- `VERIFIED FAIL — MUST FIX BEFORE COMMIT`.
Không sửa code trong Phase C.
```

## Checklist thủ công của frontend

- Category Create/Edit/Delete/Toggle.
- Ingredient modal/form/action.
- UnitConversion Create/Edit/Delete và validation.
- Responsive table/form/modal.

Ngoài kiểm thử chức năng, bắt buộc chụp ít nhất:

- Ảnh trước và sau ở `1440×900`.
- Ảnh sau ở `1024×768`.
- Ảnh sau ở `390×844`.
- Một ảnh thể hiện form hoặc modal có validation.
- Một ảnh thể hiện table/list và action.

## Commit và push

Chỉ commit khi Prompt C trả `VERIFIED PASS — MAY COMMIT AND PUSH` và frontend đã tự kiểm tra.

```bash
git add "Areas/Admin/Views/AdminCategory/Create.cshtml"
git add "Areas/Admin/Views/AdminCategory/Edit.cshtml"
git add "Areas/Admin/Views/AdminCategory/Index.cshtml"
git add "Areas/Admin/Views/AdminIngredient/Index.cshtml"
git add "Areas/Admin/Views/AdminUnitConversion/Create.cshtml"
git add "Areas/Admin/Views/AdminUnitConversion/Edit.cshtml"
git add "Areas/Admin/Views/AdminUnitConversion/Index.cshtml"
git add "wwwroot/css/Admin/Category/Category.css"
git add "wwwroot/css/Admin/Ingredient/ingredient.css"
git add "wwwroot/css/unit-conversion.css"
git diff --cached --name-only
git diff --cached --stat
git diff --cached --check
git commit -m "style(admin-master-data): unify category ingredient and unit conversion UI"
git push -u origin feature/admin-ui-fe2-master-data
```

Sau push: gửi nhóm trưởng branch, commit hash, ảnh trước/sau, checklist, build result và mọi exception còn lại. **Không tự merge.**


# ĐỢT 3 — DASHBOARD ADMIN — 6 NHÓM DỮ LIỆU VÀ AI

## Điều kiện bắt đầu

Core FE1 đã merge; Master Data được duyệt để xác nhận contract hoạt động.

- Nhóm trưởng đã xác nhận các dependency cần thiết đã merge vào `develop`.
- `git status --short` sạch.
- Branch phải được tạo mới từ `develop` vừa pull.

```bash
git checkout develop
git pull origin develop
git checkout -b feature/admin-ui-fe2-dashboard
```

**Mức rủi ro:** Rất cao — chart, tab, filter và AI hooks.

## File được phép chỉnh

- `Areas/Admin/Views/Dashboard/Guide.cshtml`
- `Areas/Admin/Views/Dashboard/Index.cshtml`
- `wwwroot/css/Admin/Dashboard/dashboard.css`

Cấm chỉnh file ngoài danh sách này. File liên quan khác chỉ được đọc để hiểu hook và visual context.

## Kết quả giao diện phải đạt

- Dashboard phải nổi bật page title, filter context, KPI, cảnh báo, chart/evidence và AI result.
- Giữ đủ 6 nhóm dữ liệu + AI, không biến tab POS/WorkShift thành ứng dụng POS.
- Loading/no data/partial/error có hierarchy và semantics.

## Quy chuẩn chi tiết theo loại trang

- Filter bar có surface riêng, controls 44px, Apply primary.
- KPI value 24–30px; chart cards cùng padding/radius/title.
- AI result: context → conclusion → evidence/chart → limitation/recommendation.
- Guide dùng cùng typography/card/callout với Dashboard.

## Phần phải bảo toàn tuyệt đối

- Tab IDs, chart canvas, filter inputs, AI preset/question hooks và script blocks.
- Dữ liệu, label, granularity, top N, StaffScope/permission.

## Prompt A — Audit read-only

Sao chép nguyên khối:

```text
Bạn đang làm Frontend 2 của CafeChain.

BẮT BUỘC đọc đầy đủ, theo thứ tự:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Toàn bộ source của các file được liệt kê cho đợt `DASHBOARD ADMIN — 6 NHÓM DỮ LIỆU VÀ AI`.

ĐÂY CHỈ LÀ PHASE A — AUDIT READ-ONLY. TUYỆT ĐỐI CHƯA SỬA CODE.

Phạm vi đợt này:
- `Areas/Admin/Views/Dashboard/Guide.cshtml`
- `Areas/Admin/Views/Dashboard/Index.cshtml`
- `wwwroot/css/Admin/Dashboard/dashboard.css`

Hãy trả báo cáo theo đúng cấu trúc:

A. Scope confirmation
- Liệt kê đúng từng file được phép chỉnh.
- Liệt kê file liên quan đã đọc nhưng không được chỉnh.
- Xác nhận không sửa file ownership của frontend còn lại.

B. DOM và functional freeze
- Liệt kê form, table, modal, tab, collapse, partial, hidden input và dynamic region hiện có.
- Liệt kê `id`, `name`, `data-*`, `asp-*`, class/hook có khả năng được JS/Bootstrap/Select2/chart/map/drag-drop sử dụng.
- Nêu chính xác phần nào trong `.cshtml` chỉ được bổ sung class và phần nào phải để nguyên hoàn toàn.

C. Current UI audit
- Page header hiện tại.
- Button variants và kích thước hiện tại.
- Form control, validation, Select2 hiện tại.
- Table/list/pagination hiện tại.
- Modal/confirm/empty/loading/error hiện tại.
- Màu, token, spacing, radius, shadow và `!important` bị trùng hoặc lệch.

D. Target mapping
- Lập bảng `existing selector → visual contract --cc-* → file CSS sẽ chỉnh`.
- Chỉ ra selector nào dùng CSS chung và selector nào phải giữ exception nghiệp vụ.
- Mô tả cách làm Index/Create/Edit/Details/Delete/Modal đồng bộ mà không đổi DOM.

E. Risk register
- Specificity và thứ tự load CSS.
- Inline style và `<style>` cục bộ.
- Plugin control.
- Responsive table/modal.
- Nguy cơ selector lan sang StaffHub/POS/module frontend còn lại.

F. Test plan
- Trang và luồng cần test trước/sau.
- Viewport cần chụp.
- Functional regression bắt buộc.

Kết luận bằng một trong hai trạng thái:
- `READY FOR IMPLEMENTATION`: phạm vi rõ, không cần sửa JS/backend/DOM.
- `BLOCKED`: ghi đúng vấn đề cần nhóm trưởng giải quyết.

Sau báo cáo phải dừng. Không được chỉnh code hoặc tuyên bố đã hoàn thành giao diện.
```

Chỉ chuyển sang Prompt B khi báo cáo kết thúc bằng `READY FOR IMPLEMENTATION` và frontend đã đọc, đối chiếu đúng file/hook.

## Prompt B — Triển khai chính thức

```text
Bạn đang làm Frontend 2 của CafeChain.

BẮT BUỘC đọc lại:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Báo cáo audit Phase A vừa được duyệt.

PHASE B — CHỈ TRIỂN KHAI ĐỢT `DASHBOARD ADMIN — 6 NHÓM DỮ LIỆU VÀ AI`.

CHỈ ĐƯỢC CHỈNH:
- `Areas/Admin/Views/Dashboard/Guide.cshtml`
- `Areas/Admin/Views/Dashboard/Index.cshtml`
- `wwwroot/css/Admin/Dashboard/dashboard.css`

MỤC TIÊU NGHIỆP VỤ VÀ GIAO DIỆN:
- Dashboard phải nổi bật page title, filter context, KPI, cảnh báo, chart/evidence và AI result.
- Giữ đủ 6 nhóm dữ liệu + AI, không biến tab POS/WorkShift thành ứng dụng POS.
- Loading/no data/partial/error có hierarchy và semantics.

QUY CHUẨN THEO TRANG/FORM:
- Filter bar có surface riêng, controls 44px, Apply primary.
- KPI value 24–30px; chart cards cùng padding/radius/title.
- AI result: context → conclusion → evidence/chart → limitation/recommendation.
- Guide dùng cùng typography/card/callout với Dashboard.

PHẦN PHẢI ĐÓNG BĂNG:
- Tab IDs, chart canvas, filter inputs, AI preset/question hooks và script blocks.
- Dữ liệu, label, granularity, top N, StaffScope/permission.

RÀNG BUỘC TUYỆT ĐỐI:
- Chỉ chỉnh `.cshtml` và `.css` nêu trên.
- Không chỉnh JavaScript, backend, database, SQL, migration, seed, permission hoặc nghiệp vụ.
- Không thêm/xóa/đổi tên/di chuyển cấu trúc thẻ `.cshtml`.
- Giữ nguyên `id`, `name`, `data-*`, `asp-*`, route, action, method, hidden input, validation, modal/tab/collapse và mọi hook.
- Không thêm framework/package/font/script.
- Không format hàng loạt.
- Không dùng selector global lan khỏi Admin.
- Không tạo design token cạnh tranh với `--cc-*`.
- Không dùng `!important` nếu selector đúng có thể giải quyết; exception phải có comment module và lý do.
- Không chỉnh Online, Voucher, Wheel, StaffHub hoặc POS riêng.

CÁCH TRIỂN KHAI BẮT BUỘC:
1. Giữ CSS module cho bố cục đặc thù; dùng token `--cc-*` để đồng bộ màu, type, spacing, radius và shadow.
2. Ưu tiên CSS selector tương thích với markup hiện tại; `.cshtml` chỉ bổ sung class visual khi CSS hiện tại không thể xử lý an toàn.
3. Đồng bộ đầy đủ normal/hover/active/focus/disabled/loading/error/empty state.
4. Đồng bộ Index/Create/Edit/Details/Delete/Modal trong phạm vi; không chỉ sửa trang Index.
5. Sửa responsive ngay trong đợt, không để dành toàn bộ đến cuối.
6. Sau mỗi nhóm selector, kiểm tra một trang đại diện để phát hiện conflict sớm.

KIỂM TRA BẮT BUỘC SAU KHI CODE:
- Tất cả tab và filter.
- Chart render/resize.
- AI preset/custom, loading/no-data/partial/error.
- Responsive KPI/chart/tab/filter; không overflow.

Trước khi kết luận, chạy và báo kết quả:
- `git diff --name-only`
- `git diff --stat`
- `git diff --check`
- `dotnet build`
- kiểm tra diff `.cshtml` để xác nhận không thay DOM/hook/binding.

OUTPUT CUỐI CÙNG PHẢI CÓ:
1. Danh sách chính xác file đã chỉnh và file chỉ đọc.
2. Tóm tắt thay đổi theo từng file.
3. Bảng selector cũ → contract mới.
4. Danh sách trạng thái UI đã hoàn thiện.
5. Danh sách viewport và luồng đã test, kèm kết quả đạt/chưa đạt.
6. Xác nhận không chỉnh file bị cấm, JavaScript, backend, database, seed, nghiệp vụ hoặc DOM.
7. CSS exception/`!important` còn lại và lý do.
8. Lỗi/rủi ro chưa xử lý; không được che giấu.
9. Kết luận chỉ được là `PASS — READY TO COMMIT` hoặc `FAIL — DO NOT COMMIT`.

Không được tuyên bố PASS nếu chưa kiểm tra toàn bộ mục trên.
```

## Prompt C — Kiểm tra độc lập sau khi làm

```text
Hãy thực hiện PHASE C — INDEPENDENT VERIFICATION cho đợt `DASHBOARD ADMIN — 6 NHÓM DỮ LIỆU VÀ AI`. Không sửa thêm code trong bước này.

Đọc:
- hai file đặc tả chính trong `docs`;
- `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`;
- toàn bộ `git diff` hiện tại.

Kiểm tra độc lập:
1. File thay đổi có đúng ownership và đúng danh sách đợt không.
2. Có file `.js`, `.ts`, `.cs`, `.sql`, migration, seed hoặc backend nào thay đổi không.
3. `.cshtml` có thay cấu trúc thẻ, thứ tự DOM, `id`, `name`, `asp-*`, `data-*`, form action/method, hidden input hoặc hook không.
4. Index/Create/Edit/Details/Delete/Modal trong phạm vi đã dùng cùng visual contract chưa.
5. Button, input, table, modal, badge, page header và validation có cùng kích thước/semantics chưa.
6. Có selector global hoặc `!important` không cần thiết gây conflict không.
7. Desktop/tablet/mobile/zoom có overflow, chữ chìm, action bị che hoặc modal mất footer không.
8. Các chức năng sau còn hoạt động nguyên vẹn không:
- Tất cả tab và filter.
- Chart render/resize.
- AI preset/custom, loading/no-data/partial/error.
- Responsive KPI/chart/tab/filter; không overflow.
9. `dotnet build`, `git diff --check` có pass không.
10. So sánh ảnh trước/sau; chỉ ra trang nào còn lệch với module đã chuẩn hóa.

Trả kết quả dạng bảng: `Tiêu chí | PASS/FAIL | Bằng chứng | Việc cần sửa`.
Kết luận duy nhất:
- `VERIFIED PASS — MAY COMMIT AND PUSH`, hoặc
- `VERIFIED FAIL — MUST FIX BEFORE COMMIT`.
Không sửa code trong Phase C.
```

## Checklist thủ công của frontend

- Tất cả tab và filter.
- Chart render/resize.
- AI preset/custom, loading/no-data/partial/error.
- Responsive KPI/chart/tab/filter; không overflow.

Ngoài kiểm thử chức năng, bắt buộc chụp ít nhất:

- Ảnh trước và sau ở `1440×900`.
- Ảnh sau ở `1024×768`.
- Ảnh sau ở `390×844`.
- Một ảnh thể hiện form hoặc modal có validation.
- Một ảnh thể hiện table/list và action.

## Commit và push

Chỉ commit khi Prompt C trả `VERIFIED PASS — MAY COMMIT AND PUSH` và frontend đã tự kiểm tra.

```bash
git add "Areas/Admin/Views/Dashboard/Guide.cshtml"
git add "Areas/Admin/Views/Dashboard/Index.cshtml"
git add "wwwroot/css/Admin/Dashboard/dashboard.css"
git diff --cached --name-only
git diff --cached --stat
git diff --cached --check
git commit -m "style(admin-dashboard): unify analytics dashboard UI"
git push -u origin feature/admin-ui-fe2-dashboard
```

Sau push: gửi nhóm trưởng branch, commit hash, ảnh trước/sau, checklist, build result và mọi exception còn lại. **Không tự merge.**


# ĐỢT 4 — INVENTORY CORE — TỒN KHO, NGƯỠNG, CẢNH BÁO VÀ BẤT THƯỜNG

## Điều kiện bắt đầu

Core FE1 đã merge. Đợt này phải được nhóm trưởng merge trước Restock/Reorder.

- Nhóm trưởng đã xác nhận các dependency cần thiết đã merge vào `develop`.
- `git status --short` sạch.
- Branch phải được tạo mới từ `develop` vừa pull.

```bash
git checkout develop
git pull origin develop
git checkout -b feature/admin-ui-fe2-inventory-core
```

**Mức rủi ro:** Rất cao — partial/modal và là nền cho procurement.

## File được phép chỉnh

- `Areas/Admin/Views/AdminStoreInventory/Index.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_InventoryTablePartial.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_PaginationPartial.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_StoreTabsPartial.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_TransactionModalPartial.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_TransactionPartial.cshtml`
- `Areas/Admin/Views/AdminInventoryThresholds/Index.cshtml`
- `Areas/Admin/Views/AdminStockAlerts/Details.cshtml`
- `Areas/Admin/Views/AdminStockAlerts/Index.cshtml`
- `Areas/Admin/Views/AdminOperationalAnomalies/Index.cshtml`
- `wwwroot/css/Admin/InventoryOperations/inventory-operations.css`
- `wwwroot/css/Admin/StoreInventory/storeinventory.css`

Cấm chỉnh file ngoài danh sách này. File liên quan khác chỉ được đọc để hiểu hook và visual context.

## Kết quả giao diện phải đạt

- Tồn kho, ngưỡng, cảnh báo và bất thường dùng chung `cc-warehouse` visual contract.
- Phân biệt tín hiệu/cảnh báo với hành động xử lý.
- Partial và transaction modal ghép thành một trải nghiệm thống nhất.

## Quy chuẩn chi tiết theo loại trang

- StoreInventory: store tabs, table, pagination, transaction modal.
- Thresholds: form/table ngưỡng, warning semantics.
- StockAlerts: Index/Details, severity/status/action rõ.
- Anomalies: summary/filter/table/callout rõ, không lạm dụng đỏ.

## Phần phải bảo toàn tuyệt đối

- Store tabs, pagination params, transaction modal IDs/hooks.
- Threshold/alert action forms, IDs, status mapping.
- Không sửa procurement Core hoặc FE1 Core.

## Prompt A — Audit read-only

Sao chép nguyên khối:

```text
Bạn đang làm Frontend 2 của CafeChain.

BẮT BUỘC đọc đầy đủ, theo thứ tự:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Toàn bộ source của các file được liệt kê cho đợt `INVENTORY CORE — TỒN KHO, NGƯỠNG, CẢNH BÁO VÀ BẤT THƯỜNG`.

ĐÂY CHỈ LÀ PHASE A — AUDIT READ-ONLY. TUYỆT ĐỐI CHƯA SỬA CODE.

Phạm vi đợt này:
- `Areas/Admin/Views/AdminStoreInventory/Index.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_InventoryTablePartial.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_PaginationPartial.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_StoreTabsPartial.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_TransactionModalPartial.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_TransactionPartial.cshtml`
- `Areas/Admin/Views/AdminInventoryThresholds/Index.cshtml`
- `Areas/Admin/Views/AdminStockAlerts/Details.cshtml`
- `Areas/Admin/Views/AdminStockAlerts/Index.cshtml`
- `Areas/Admin/Views/AdminOperationalAnomalies/Index.cshtml`
- `wwwroot/css/Admin/InventoryOperations/inventory-operations.css`
- `wwwroot/css/Admin/StoreInventory/storeinventory.css`

Hãy trả báo cáo theo đúng cấu trúc:

A. Scope confirmation
- Liệt kê đúng từng file được phép chỉnh.
- Liệt kê file liên quan đã đọc nhưng không được chỉnh.
- Xác nhận không sửa file ownership của frontend còn lại.

B. DOM và functional freeze
- Liệt kê form, table, modal, tab, collapse, partial, hidden input và dynamic region hiện có.
- Liệt kê `id`, `name`, `data-*`, `asp-*`, class/hook có khả năng được JS/Bootstrap/Select2/chart/map/drag-drop sử dụng.
- Nêu chính xác phần nào trong `.cshtml` chỉ được bổ sung class và phần nào phải để nguyên hoàn toàn.

C. Current UI audit
- Page header hiện tại.
- Button variants và kích thước hiện tại.
- Form control, validation, Select2 hiện tại.
- Table/list/pagination hiện tại.
- Modal/confirm/empty/loading/error hiện tại.
- Màu, token, spacing, radius, shadow và `!important` bị trùng hoặc lệch.

D. Target mapping
- Lập bảng `existing selector → visual contract --cc-* → file CSS sẽ chỉnh`.
- Chỉ ra selector nào dùng CSS chung và selector nào phải giữ exception nghiệp vụ.
- Mô tả cách làm Index/Create/Edit/Details/Delete/Modal đồng bộ mà không đổi DOM.

E. Risk register
- Specificity và thứ tự load CSS.
- Inline style và `<style>` cục bộ.
- Plugin control.
- Responsive table/modal.
- Nguy cơ selector lan sang StaffHub/POS/module frontend còn lại.

F. Test plan
- Trang và luồng cần test trước/sau.
- Viewport cần chụp.
- Functional regression bắt buộc.

Kết luận bằng một trong hai trạng thái:
- `READY FOR IMPLEMENTATION`: phạm vi rõ, không cần sửa JS/backend/DOM.
- `BLOCKED`: ghi đúng vấn đề cần nhóm trưởng giải quyết.

Sau báo cáo phải dừng. Không được chỉnh code hoặc tuyên bố đã hoàn thành giao diện.
```

Chỉ chuyển sang Prompt B khi báo cáo kết thúc bằng `READY FOR IMPLEMENTATION` và frontend đã đọc, đối chiếu đúng file/hook.

## Prompt B — Triển khai chính thức

```text
Bạn đang làm Frontend 2 của CafeChain.

BẮT BUỘC đọc lại:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Báo cáo audit Phase A vừa được duyệt.

PHASE B — CHỈ TRIỂN KHAI ĐỢT `INVENTORY CORE — TỒN KHO, NGƯỠNG, CẢNH BÁO VÀ BẤT THƯỜNG`.

CHỈ ĐƯỢC CHỈNH:
- `Areas/Admin/Views/AdminStoreInventory/Index.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_InventoryTablePartial.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_PaginationPartial.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_StoreTabsPartial.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_TransactionModalPartial.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_TransactionPartial.cshtml`
- `Areas/Admin/Views/AdminInventoryThresholds/Index.cshtml`
- `Areas/Admin/Views/AdminStockAlerts/Details.cshtml`
- `Areas/Admin/Views/AdminStockAlerts/Index.cshtml`
- `Areas/Admin/Views/AdminOperationalAnomalies/Index.cshtml`
- `wwwroot/css/Admin/InventoryOperations/inventory-operations.css`
- `wwwroot/css/Admin/StoreInventory/storeinventory.css`

MỤC TIÊU NGHIỆP VỤ VÀ GIAO DIỆN:
- Tồn kho, ngưỡng, cảnh báo và bất thường dùng chung `cc-warehouse` visual contract.
- Phân biệt tín hiệu/cảnh báo với hành động xử lý.
- Partial và transaction modal ghép thành một trải nghiệm thống nhất.

QUY CHUẨN THEO TRANG/FORM:
- StoreInventory: store tabs, table, pagination, transaction modal.
- Thresholds: form/table ngưỡng, warning semantics.
- StockAlerts: Index/Details, severity/status/action rõ.
- Anomalies: summary/filter/table/callout rõ, không lạm dụng đỏ.

PHẦN PHẢI ĐÓNG BĂNG:
- Store tabs, pagination params, transaction modal IDs/hooks.
- Threshold/alert action forms, IDs, status mapping.
- Không sửa procurement Core hoặc FE1 Core.

RÀNG BUỘC TUYỆT ĐỐI:
- Chỉ chỉnh `.cshtml` và `.css` nêu trên.
- Không chỉnh JavaScript, backend, database, SQL, migration, seed, permission hoặc nghiệp vụ.
- Không thêm/xóa/đổi tên/di chuyển cấu trúc thẻ `.cshtml`.
- Giữ nguyên `id`, `name`, `data-*`, `asp-*`, route, action, method, hidden input, validation, modal/tab/collapse và mọi hook.
- Không thêm framework/package/font/script.
- Không format hàng loạt.
- Không dùng selector global lan khỏi Admin.
- Không tạo design token cạnh tranh với `--cc-*`.
- Không dùng `!important` nếu selector đúng có thể giải quyết; exception phải có comment module và lý do.
- Không chỉnh Online, Voucher, Wheel, StaffHub hoặc POS riêng.

CÁCH TRIỂN KHAI BẮT BUỘC:
1. Giữ CSS module cho bố cục đặc thù; dùng token `--cc-*` để đồng bộ màu, type, spacing, radius và shadow.
2. Ưu tiên CSS selector tương thích với markup hiện tại; `.cshtml` chỉ bổ sung class visual khi CSS hiện tại không thể xử lý an toàn.
3. Đồng bộ đầy đủ normal/hover/active/focus/disabled/loading/error/empty state.
4. Đồng bộ Index/Create/Edit/Details/Delete/Modal trong phạm vi; không chỉ sửa trang Index.
5. Sửa responsive ngay trong đợt, không để dành toàn bộ đến cuối.
6. Sau mỗi nhóm selector, kiểm tra một trang đại diện để phát hiện conflict sớm.

KIỂM TRA BẮT BUỘC SAU KHI CODE:
- Store tab/filter/pagination/transaction modal.
- Threshold save/validation.
- Alert Index→Details→action.
- Anomaly filters.
- Responsive data-heavy tables/modal.

Trước khi kết luận, chạy và báo kết quả:
- `git diff --name-only`
- `git diff --stat`
- `git diff --check`
- `dotnet build`
- kiểm tra diff `.cshtml` để xác nhận không thay DOM/hook/binding.

OUTPUT CUỐI CÙNG PHẢI CÓ:
1. Danh sách chính xác file đã chỉnh và file chỉ đọc.
2. Tóm tắt thay đổi theo từng file.
3. Bảng selector cũ → contract mới.
4. Danh sách trạng thái UI đã hoàn thiện.
5. Danh sách viewport và luồng đã test, kèm kết quả đạt/chưa đạt.
6. Xác nhận không chỉnh file bị cấm, JavaScript, backend, database, seed, nghiệp vụ hoặc DOM.
7. CSS exception/`!important` còn lại và lý do.
8. Lỗi/rủi ro chưa xử lý; không được che giấu.
9. Kết luận chỉ được là `PASS — READY TO COMMIT` hoặc `FAIL — DO NOT COMMIT`.

Không được tuyên bố PASS nếu chưa kiểm tra toàn bộ mục trên.
```

## Prompt C — Kiểm tra độc lập sau khi làm

```text
Hãy thực hiện PHASE C — INDEPENDENT VERIFICATION cho đợt `INVENTORY CORE — TỒN KHO, NGƯỠNG, CẢNH BÁO VÀ BẤT THƯỜNG`. Không sửa thêm code trong bước này.

Đọc:
- hai file đặc tả chính trong `docs`;
- `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`;
- toàn bộ `git diff` hiện tại.

Kiểm tra độc lập:
1. File thay đổi có đúng ownership và đúng danh sách đợt không.
2. Có file `.js`, `.ts`, `.cs`, `.sql`, migration, seed hoặc backend nào thay đổi không.
3. `.cshtml` có thay cấu trúc thẻ, thứ tự DOM, `id`, `name`, `asp-*`, `data-*`, form action/method, hidden input hoặc hook không.
4. Index/Create/Edit/Details/Delete/Modal trong phạm vi đã dùng cùng visual contract chưa.
5. Button, input, table, modal, badge, page header và validation có cùng kích thước/semantics chưa.
6. Có selector global hoặc `!important` không cần thiết gây conflict không.
7. Desktop/tablet/mobile/zoom có overflow, chữ chìm, action bị che hoặc modal mất footer không.
8. Các chức năng sau còn hoạt động nguyên vẹn không:
- Store tab/filter/pagination/transaction modal.
- Threshold save/validation.
- Alert Index→Details→action.
- Anomaly filters.
- Responsive data-heavy tables/modal.
9. `dotnet build`, `git diff --check` có pass không.
10. So sánh ảnh trước/sau; chỉ ra trang nào còn lệch với module đã chuẩn hóa.

Trả kết quả dạng bảng: `Tiêu chí | PASS/FAIL | Bằng chứng | Việc cần sửa`.
Kết luận duy nhất:
- `VERIFIED PASS — MAY COMMIT AND PUSH`, hoặc
- `VERIFIED FAIL — MUST FIX BEFORE COMMIT`.
Không sửa code trong Phase C.
```

## Checklist thủ công của frontend

- Store tab/filter/pagination/transaction modal.
- Threshold save/validation.
- Alert Index→Details→action.
- Anomaly filters.
- Responsive data-heavy tables/modal.

Ngoài kiểm thử chức năng, bắt buộc chụp ít nhất:

- Ảnh trước và sau ở `1440×900`.
- Ảnh sau ở `1024×768`.
- Ảnh sau ở `390×844`.
- Một ảnh thể hiện form hoặc modal có validation.
- Một ảnh thể hiện table/list và action.

## Commit và push

Chỉ commit khi Prompt C trả `VERIFIED PASS — MAY COMMIT AND PUSH` và frontend đã tự kiểm tra.

```bash
git add "Areas/Admin/Views/AdminStoreInventory/Index.cshtml"
git add "Areas/Admin/Views/AdminStoreInventory/Partials/_InventoryTablePartial.cshtml"
git add "Areas/Admin/Views/AdminStoreInventory/Partials/_PaginationPartial.cshtml"
git add "Areas/Admin/Views/AdminStoreInventory/Partials/_StoreTabsPartial.cshtml"
git add "Areas/Admin/Views/AdminStoreInventory/Partials/_TransactionModalPartial.cshtml"
git add "Areas/Admin/Views/AdminStoreInventory/Partials/_TransactionPartial.cshtml"
git add "Areas/Admin/Views/AdminInventoryThresholds/Index.cshtml"
git add "Areas/Admin/Views/AdminStockAlerts/Details.cshtml"
git add "Areas/Admin/Views/AdminStockAlerts/Index.cshtml"
git add "Areas/Admin/Views/AdminOperationalAnomalies/Index.cshtml"
git add "wwwroot/css/Admin/InventoryOperations/inventory-operations.css"
git add "wwwroot/css/Admin/StoreInventory/storeinventory.css"
git diff --cached --name-only
git diff --cached --stat
git diff --cached --check
git commit -m "style(admin-inventory): establish unified inventory operations UI"
git push -u origin feature/admin-ui-fe2-inventory-core
```

Sau push: gửi nhóm trưởng branch, commit hash, ảnh trước/sau, checklist, build result và mọi exception còn lại. **Không tự merge.**


# ĐỢT 5 — RESTOCK REQUEST VÀ REORDER SUGGESTIONS

## Điều kiện bắt đầu

Inventory Core đã được nhóm trưởng merge.

- Nhóm trưởng đã xác nhận các dependency cần thiết đã merge vào `develop`.
- `git status --short` sạch.
- Branch phải được tạo mới từ `develop` vừa pull.

```bash
git checkout develop
git pull origin develop
git checkout -b feature/admin-ui-fe2-restock-reorder
```

**Mức rủi ro:** Rất cao — workflow, AI explanation và nhiều form.

## File được phép chỉnh

- `Areas/Admin/Views/AdminRestockRequests/CreateCentralPlanner.cshtml`
- `Areas/Admin/Views/AdminRestockRequests/CreateManual.cshtml`
- `Areas/Admin/Views/AdminRestockRequests/Details.cshtml`
- `Areas/Admin/Views/AdminRestockRequests/Index.cshtml`
- `Areas/Admin/Views/AdminReorderSuggestions/Index.cshtml`
- `wwwroot/css/Admin/Procurement/procurement-design-system.css`
- `wwwroot/css/Admin/Procurement/reorder-suggestions.css`

Cấm chỉnh file ngoài danh sách này. File liên quan khác chỉ được đọc để hiểu hook và visual context.

## Kết quả giao diện phải đạt

- Thể hiện rõ cảnh báo → yêu cầu nhập → gợi ý AI, không lẫn với PO.
- Hai form Create dùng cùng contract nhưng giữ khác biệt nghiệp vụ.
- AI explanation là panel phụ; tạo/xác nhận request là action chính.

## Quy chuẩn chi tiết theo loại trang

- Index: status/filter/table/action.
- CreateManual/CreateCentralPlanner: section rõ, line items, validation, footer.
- Details: context, status, evidence, action workflow.
- Reorder: recommendation table, explanation panel, confirm/create action.

## Phần phải bảo toàn tuyệt đối

- Request status/action forms, item IDs, planner/manual logic.
- AI data, modal IDs, selected rows và scripts.

## Prompt A — Audit read-only

Sao chép nguyên khối:

```text
Bạn đang làm Frontend 2 của CafeChain.

BẮT BUỘC đọc đầy đủ, theo thứ tự:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Toàn bộ source của các file được liệt kê cho đợt `RESTOCK REQUEST VÀ REORDER SUGGESTIONS`.

ĐÂY CHỈ LÀ PHASE A — AUDIT READ-ONLY. TUYỆT ĐỐI CHƯA SỬA CODE.

Phạm vi đợt này:
- `Areas/Admin/Views/AdminRestockRequests/CreateCentralPlanner.cshtml`
- `Areas/Admin/Views/AdminRestockRequests/CreateManual.cshtml`
- `Areas/Admin/Views/AdminRestockRequests/Details.cshtml`
- `Areas/Admin/Views/AdminRestockRequests/Index.cshtml`
- `Areas/Admin/Views/AdminReorderSuggestions/Index.cshtml`
- `wwwroot/css/Admin/Procurement/procurement-design-system.css`
- `wwwroot/css/Admin/Procurement/reorder-suggestions.css`

Hãy trả báo cáo theo đúng cấu trúc:

A. Scope confirmation
- Liệt kê đúng từng file được phép chỉnh.
- Liệt kê file liên quan đã đọc nhưng không được chỉnh.
- Xác nhận không sửa file ownership của frontend còn lại.

B. DOM và functional freeze
- Liệt kê form, table, modal, tab, collapse, partial, hidden input và dynamic region hiện có.
- Liệt kê `id`, `name`, `data-*`, `asp-*`, class/hook có khả năng được JS/Bootstrap/Select2/chart/map/drag-drop sử dụng.
- Nêu chính xác phần nào trong `.cshtml` chỉ được bổ sung class và phần nào phải để nguyên hoàn toàn.

C. Current UI audit
- Page header hiện tại.
- Button variants và kích thước hiện tại.
- Form control, validation, Select2 hiện tại.
- Table/list/pagination hiện tại.
- Modal/confirm/empty/loading/error hiện tại.
- Màu, token, spacing, radius, shadow và `!important` bị trùng hoặc lệch.

D. Target mapping
- Lập bảng `existing selector → visual contract --cc-* → file CSS sẽ chỉnh`.
- Chỉ ra selector nào dùng CSS chung và selector nào phải giữ exception nghiệp vụ.
- Mô tả cách làm Index/Create/Edit/Details/Delete/Modal đồng bộ mà không đổi DOM.

E. Risk register
- Specificity và thứ tự load CSS.
- Inline style và `<style>` cục bộ.
- Plugin control.
- Responsive table/modal.
- Nguy cơ selector lan sang StaffHub/POS/module frontend còn lại.

F. Test plan
- Trang và luồng cần test trước/sau.
- Viewport cần chụp.
- Functional regression bắt buộc.

Kết luận bằng một trong hai trạng thái:
- `READY FOR IMPLEMENTATION`: phạm vi rõ, không cần sửa JS/backend/DOM.
- `BLOCKED`: ghi đúng vấn đề cần nhóm trưởng giải quyết.

Sau báo cáo phải dừng. Không được chỉnh code hoặc tuyên bố đã hoàn thành giao diện.
```

Chỉ chuyển sang Prompt B khi báo cáo kết thúc bằng `READY FOR IMPLEMENTATION` và frontend đã đọc, đối chiếu đúng file/hook.

## Prompt B — Triển khai chính thức

```text
Bạn đang làm Frontend 2 của CafeChain.

BẮT BUỘC đọc lại:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Báo cáo audit Phase A vừa được duyệt.

PHASE B — CHỈ TRIỂN KHAI ĐỢT `RESTOCK REQUEST VÀ REORDER SUGGESTIONS`.

CHỈ ĐƯỢC CHỈNH:
- `Areas/Admin/Views/AdminRestockRequests/CreateCentralPlanner.cshtml`
- `Areas/Admin/Views/AdminRestockRequests/CreateManual.cshtml`
- `Areas/Admin/Views/AdminRestockRequests/Details.cshtml`
- `Areas/Admin/Views/AdminRestockRequests/Index.cshtml`
- `Areas/Admin/Views/AdminReorderSuggestions/Index.cshtml`
- `wwwroot/css/Admin/Procurement/procurement-design-system.css`
- `wwwroot/css/Admin/Procurement/reorder-suggestions.css`

MỤC TIÊU NGHIỆP VỤ VÀ GIAO DIỆN:
- Thể hiện rõ cảnh báo → yêu cầu nhập → gợi ý AI, không lẫn với PO.
- Hai form Create dùng cùng contract nhưng giữ khác biệt nghiệp vụ.
- AI explanation là panel phụ; tạo/xác nhận request là action chính.

QUY CHUẨN THEO TRANG/FORM:
- Index: status/filter/table/action.
- CreateManual/CreateCentralPlanner: section rõ, line items, validation, footer.
- Details: context, status, evidence, action workflow.
- Reorder: recommendation table, explanation panel, confirm/create action.

PHẦN PHẢI ĐÓNG BĂNG:
- Request status/action forms, item IDs, planner/manual logic.
- AI data, modal IDs, selected rows và scripts.

RÀNG BUỘC TUYỆT ĐỐI:
- Chỉ chỉnh `.cshtml` và `.css` nêu trên.
- Không chỉnh JavaScript, backend, database, SQL, migration, seed, permission hoặc nghiệp vụ.
- Không thêm/xóa/đổi tên/di chuyển cấu trúc thẻ `.cshtml`.
- Giữ nguyên `id`, `name`, `data-*`, `asp-*`, route, action, method, hidden input, validation, modal/tab/collapse và mọi hook.
- Không thêm framework/package/font/script.
- Không format hàng loạt.
- Không dùng selector global lan khỏi Admin.
- Không tạo design token cạnh tranh với `--cc-*`.
- Không dùng `!important` nếu selector đúng có thể giải quyết; exception phải có comment module và lý do.
- Không chỉnh Online, Voucher, Wheel, StaffHub hoặc POS riêng.

CÁCH TRIỂN KHAI BẮT BUỘC:
1. Giữ CSS module cho bố cục đặc thù; dùng token `--cc-*` để đồng bộ màu, type, spacing, radius và shadow.
2. Ưu tiên CSS selector tương thích với markup hiện tại; `.cshtml` chỉ bổ sung class visual khi CSS hiện tại không thể xử lý an toàn.
3. Đồng bộ đầy đủ normal/hover/active/focus/disabled/loading/error/empty state.
4. Đồng bộ Index/Create/Edit/Details/Delete/Modal trong phạm vi; không chỉ sửa trang Index.
5. Sửa responsive ngay trong đợt, không để dành toàn bộ đến cuối.
6. Sau mỗi nhóm selector, kiểm tra một trang đại diện để phát hiện conflict sớm.

KIỂM TRA BẮT BUỘC SAU KHI CODE:
- Manual/CentralPlanner create và validation.
- Index/filter/details/status actions.
- Reorder suggestion, modal/explanation và create request.
- Responsive table/form.

Trước khi kết luận, chạy và báo kết quả:
- `git diff --name-only`
- `git diff --stat`
- `git diff --check`
- `dotnet build`
- kiểm tra diff `.cshtml` để xác nhận không thay DOM/hook/binding.

OUTPUT CUỐI CÙNG PHẢI CÓ:
1. Danh sách chính xác file đã chỉnh và file chỉ đọc.
2. Tóm tắt thay đổi theo từng file.
3. Bảng selector cũ → contract mới.
4. Danh sách trạng thái UI đã hoàn thiện.
5. Danh sách viewport và luồng đã test, kèm kết quả đạt/chưa đạt.
6. Xác nhận không chỉnh file bị cấm, JavaScript, backend, database, seed, nghiệp vụ hoặc DOM.
7. CSS exception/`!important` còn lại và lý do.
8. Lỗi/rủi ro chưa xử lý; không được che giấu.
9. Kết luận chỉ được là `PASS — READY TO COMMIT` hoặc `FAIL — DO NOT COMMIT`.

Không được tuyên bố PASS nếu chưa kiểm tra toàn bộ mục trên.
```

## Prompt C — Kiểm tra độc lập sau khi làm

```text
Hãy thực hiện PHASE C — INDEPENDENT VERIFICATION cho đợt `RESTOCK REQUEST VÀ REORDER SUGGESTIONS`. Không sửa thêm code trong bước này.

Đọc:
- hai file đặc tả chính trong `docs`;
- `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`;
- toàn bộ `git diff` hiện tại.

Kiểm tra độc lập:
1. File thay đổi có đúng ownership và đúng danh sách đợt không.
2. Có file `.js`, `.ts`, `.cs`, `.sql`, migration, seed hoặc backend nào thay đổi không.
3. `.cshtml` có thay cấu trúc thẻ, thứ tự DOM, `id`, `name`, `asp-*`, `data-*`, form action/method, hidden input hoặc hook không.
4. Index/Create/Edit/Details/Delete/Modal trong phạm vi đã dùng cùng visual contract chưa.
5. Button, input, table, modal, badge, page header và validation có cùng kích thước/semantics chưa.
6. Có selector global hoặc `!important` không cần thiết gây conflict không.
7. Desktop/tablet/mobile/zoom có overflow, chữ chìm, action bị che hoặc modal mất footer không.
8. Các chức năng sau còn hoạt động nguyên vẹn không:
- Manual/CentralPlanner create và validation.
- Index/filter/details/status actions.
- Reorder suggestion, modal/explanation và create request.
- Responsive table/form.
9. `dotnet build`, `git diff --check` có pass không.
10. So sánh ảnh trước/sau; chỉ ra trang nào còn lệch với module đã chuẩn hóa.

Trả kết quả dạng bảng: `Tiêu chí | PASS/FAIL | Bằng chứng | Việc cần sửa`.
Kết luận duy nhất:
- `VERIFIED PASS — MAY COMMIT AND PUSH`, hoặc
- `VERIFIED FAIL — MUST FIX BEFORE COMMIT`.
Không sửa code trong Phase C.
```

## Checklist thủ công của frontend

- Manual/CentralPlanner create và validation.
- Index/filter/details/status actions.
- Reorder suggestion, modal/explanation và create request.
- Responsive table/form.

Ngoài kiểm thử chức năng, bắt buộc chụp ít nhất:

- Ảnh trước và sau ở `1440×900`.
- Ảnh sau ở `1024×768`.
- Ảnh sau ở `390×844`.
- Một ảnh thể hiện form hoặc modal có validation.
- Một ảnh thể hiện table/list và action.

## Commit và push

Chỉ commit khi Prompt C trả `VERIFIED PASS — MAY COMMIT AND PUSH` và frontend đã tự kiểm tra.

```bash
git add "Areas/Admin/Views/AdminRestockRequests/CreateCentralPlanner.cshtml"
git add "Areas/Admin/Views/AdminRestockRequests/CreateManual.cshtml"
git add "Areas/Admin/Views/AdminRestockRequests/Details.cshtml"
git add "Areas/Admin/Views/AdminRestockRequests/Index.cshtml"
git add "Areas/Admin/Views/AdminReorderSuggestions/Index.cshtml"
git add "wwwroot/css/Admin/Procurement/procurement-design-system.css"
git add "wwwroot/css/Admin/Procurement/reorder-suggestions.css"
git diff --cached --name-only
git diff --cached --stat
git diff --cached --check
git commit -m "style(admin-restock): unify restock and reorder suggestion UI"
git push -u origin feature/admin-ui-fe2-restock-reorder
```

Sau push: gửi nhóm trưởng branch, commit hash, ảnh trước/sau, checklist, build result và mọi exception còn lại. **Không tự merge.**


# ĐỢT 6 — PURCHASE ADVICE, CONSOLIDATION VÀ ORDER BATCH

## Điều kiện bắt đầu

Restock/Reorder đã được nhóm trưởng merge.

- Nhóm trưởng đã xác nhận các dependency cần thiết đã merge vào `develop`.
- `git status --short` sạch.
- Branch phải được tạo mới từ `develop` vừa pull.

```bash
git checkout develop
git pull origin develop
git checkout -b feature/admin-ui-fe2-purchase-advice
```

**Mức rủi ro:** Rất cao — chuỗi trạng thái và nhiều form.

## File được phép chỉnh

- `Areas/Admin/Views/AdminPurchaseAdvices/Create.cshtml`
- `Areas/Admin/Views/AdminPurchaseAdvices/Details.cshtml`
- `Areas/Admin/Views/AdminPurchaseAdvices/Edit.cshtml`
- `Areas/Admin/Views/AdminPurchaseAdvices/Index.cshtml`
- `Areas/Admin/Views/AdminPurchaseAdviceConsolidation/Index.cshtml`
- `Areas/Admin/Views/AdminPurchaseOrderBatches/Details.cshtml`
- `Areas/Admin/Views/AdminPurchaseOrderBatches/Index.cshtml`
- `wwwroot/css/Admin/PurchaseAdvice/purchase-advice.css`
- `wwwroot/css/Admin/Procurement/procurement-design-system.css`

Cấm chỉnh file ngoài danh sách này. File liên quan khác chỉ được đọc để hiểu hook và visual context.

## Kết quả giao diện phải đạt

- Đồng bộ PA Create/Edit/Index/Details và bước Consolidation/Batch.
- Trạng thái Draft/Submitted/In Review/Approved/Rejected nhất quán.
- Action chuyển trạng thái tách khỏi navigation và destructive action.

## Quy chuẩn chi tiết theo loại trang

- PA Index: filter/status/table/pagination.
- Create/Edit: supplier/store/items/notes/summary/action footer.
- Details: mã, trạng thái, evidence, line items và approval actions.
- Consolidation/Batch: selection, summary, table và status.

## Phần phải bảo toàn tuyệt đối

- Status transitions, approval forms, selected item IDs và totals.
- Batch/consolidation hooks, hidden fields và script.

## Prompt A — Audit read-only

Sao chép nguyên khối:

```text
Bạn đang làm Frontend 2 của CafeChain.

BẮT BUỘC đọc đầy đủ, theo thứ tự:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Toàn bộ source của các file được liệt kê cho đợt `PURCHASE ADVICE, CONSOLIDATION VÀ ORDER BATCH`.

ĐÂY CHỈ LÀ PHASE A — AUDIT READ-ONLY. TUYỆT ĐỐI CHƯA SỬA CODE.

Phạm vi đợt này:
- `Areas/Admin/Views/AdminPurchaseAdvices/Create.cshtml`
- `Areas/Admin/Views/AdminPurchaseAdvices/Details.cshtml`
- `Areas/Admin/Views/AdminPurchaseAdvices/Edit.cshtml`
- `Areas/Admin/Views/AdminPurchaseAdvices/Index.cshtml`
- `Areas/Admin/Views/AdminPurchaseAdviceConsolidation/Index.cshtml`
- `Areas/Admin/Views/AdminPurchaseOrderBatches/Details.cshtml`
- `Areas/Admin/Views/AdminPurchaseOrderBatches/Index.cshtml`
- `wwwroot/css/Admin/PurchaseAdvice/purchase-advice.css`
- `wwwroot/css/Admin/Procurement/procurement-design-system.css`

Hãy trả báo cáo theo đúng cấu trúc:

A. Scope confirmation
- Liệt kê đúng từng file được phép chỉnh.
- Liệt kê file liên quan đã đọc nhưng không được chỉnh.
- Xác nhận không sửa file ownership của frontend còn lại.

B. DOM và functional freeze
- Liệt kê form, table, modal, tab, collapse, partial, hidden input và dynamic region hiện có.
- Liệt kê `id`, `name`, `data-*`, `asp-*`, class/hook có khả năng được JS/Bootstrap/Select2/chart/map/drag-drop sử dụng.
- Nêu chính xác phần nào trong `.cshtml` chỉ được bổ sung class và phần nào phải để nguyên hoàn toàn.

C. Current UI audit
- Page header hiện tại.
- Button variants và kích thước hiện tại.
- Form control, validation, Select2 hiện tại.
- Table/list/pagination hiện tại.
- Modal/confirm/empty/loading/error hiện tại.
- Màu, token, spacing, radius, shadow và `!important` bị trùng hoặc lệch.

D. Target mapping
- Lập bảng `existing selector → visual contract --cc-* → file CSS sẽ chỉnh`.
- Chỉ ra selector nào dùng CSS chung và selector nào phải giữ exception nghiệp vụ.
- Mô tả cách làm Index/Create/Edit/Details/Delete/Modal đồng bộ mà không đổi DOM.

E. Risk register
- Specificity và thứ tự load CSS.
- Inline style và `<style>` cục bộ.
- Plugin control.
- Responsive table/modal.
- Nguy cơ selector lan sang StaffHub/POS/module frontend còn lại.

F. Test plan
- Trang và luồng cần test trước/sau.
- Viewport cần chụp.
- Functional regression bắt buộc.

Kết luận bằng một trong hai trạng thái:
- `READY FOR IMPLEMENTATION`: phạm vi rõ, không cần sửa JS/backend/DOM.
- `BLOCKED`: ghi đúng vấn đề cần nhóm trưởng giải quyết.

Sau báo cáo phải dừng. Không được chỉnh code hoặc tuyên bố đã hoàn thành giao diện.
```

Chỉ chuyển sang Prompt B khi báo cáo kết thúc bằng `READY FOR IMPLEMENTATION` và frontend đã đọc, đối chiếu đúng file/hook.

## Prompt B — Triển khai chính thức

```text
Bạn đang làm Frontend 2 của CafeChain.

BẮT BUỘC đọc lại:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Báo cáo audit Phase A vừa được duyệt.

PHASE B — CHỈ TRIỂN KHAI ĐỢT `PURCHASE ADVICE, CONSOLIDATION VÀ ORDER BATCH`.

CHỈ ĐƯỢC CHỈNH:
- `Areas/Admin/Views/AdminPurchaseAdvices/Create.cshtml`
- `Areas/Admin/Views/AdminPurchaseAdvices/Details.cshtml`
- `Areas/Admin/Views/AdminPurchaseAdvices/Edit.cshtml`
- `Areas/Admin/Views/AdminPurchaseAdvices/Index.cshtml`
- `Areas/Admin/Views/AdminPurchaseAdviceConsolidation/Index.cshtml`
- `Areas/Admin/Views/AdminPurchaseOrderBatches/Details.cshtml`
- `Areas/Admin/Views/AdminPurchaseOrderBatches/Index.cshtml`
- `wwwroot/css/Admin/PurchaseAdvice/purchase-advice.css`
- `wwwroot/css/Admin/Procurement/procurement-design-system.css`

MỤC TIÊU NGHIỆP VỤ VÀ GIAO DIỆN:
- Đồng bộ PA Create/Edit/Index/Details và bước Consolidation/Batch.
- Trạng thái Draft/Submitted/In Review/Approved/Rejected nhất quán.
- Action chuyển trạng thái tách khỏi navigation và destructive action.

QUY CHUẨN THEO TRANG/FORM:
- PA Index: filter/status/table/pagination.
- Create/Edit: supplier/store/items/notes/summary/action footer.
- Details: mã, trạng thái, evidence, line items và approval actions.
- Consolidation/Batch: selection, summary, table và status.

PHẦN PHẢI ĐÓNG BĂNG:
- Status transitions, approval forms, selected item IDs và totals.
- Batch/consolidation hooks, hidden fields và script.

RÀNG BUỘC TUYỆT ĐỐI:
- Chỉ chỉnh `.cshtml` và `.css` nêu trên.
- Không chỉnh JavaScript, backend, database, SQL, migration, seed, permission hoặc nghiệp vụ.
- Không thêm/xóa/đổi tên/di chuyển cấu trúc thẻ `.cshtml`.
- Giữ nguyên `id`, `name`, `data-*`, `asp-*`, route, action, method, hidden input, validation, modal/tab/collapse và mọi hook.
- Không thêm framework/package/font/script.
- Không format hàng loạt.
- Không dùng selector global lan khỏi Admin.
- Không tạo design token cạnh tranh với `--cc-*`.
- Không dùng `!important` nếu selector đúng có thể giải quyết; exception phải có comment module và lý do.
- Không chỉnh Online, Voucher, Wheel, StaffHub hoặc POS riêng.

CÁCH TRIỂN KHAI BẮT BUỘC:
1. Giữ CSS module cho bố cục đặc thù; dùng token `--cc-*` để đồng bộ màu, type, spacing, radius và shadow.
2. Ưu tiên CSS selector tương thích với markup hiện tại; `.cshtml` chỉ bổ sung class visual khi CSS hiện tại không thể xử lý an toàn.
3. Đồng bộ đầy đủ normal/hover/active/focus/disabled/loading/error/empty state.
4. Đồng bộ Index/Create/Edit/Details/Delete/Modal trong phạm vi; không chỉ sửa trang Index.
5. Sửa responsive ngay trong đợt, không để dành toàn bộ đến cuối.
6. Sau mỗi nhóm selector, kiểm tra một trang đại diện để phát hiện conflict sớm.

KIỂM TRA BẮT BUỘC SAU KHI CODE:
- PA create/edit/details/status.
- Consolidation selection/submit.
- Batch Index/Details/action.
- Validation/numeric/table responsive.

Trước khi kết luận, chạy và báo kết quả:
- `git diff --name-only`
- `git diff --stat`
- `git diff --check`
- `dotnet build`
- kiểm tra diff `.cshtml` để xác nhận không thay DOM/hook/binding.

OUTPUT CUỐI CÙNG PHẢI CÓ:
1. Danh sách chính xác file đã chỉnh và file chỉ đọc.
2. Tóm tắt thay đổi theo từng file.
3. Bảng selector cũ → contract mới.
4. Danh sách trạng thái UI đã hoàn thiện.
5. Danh sách viewport và luồng đã test, kèm kết quả đạt/chưa đạt.
6. Xác nhận không chỉnh file bị cấm, JavaScript, backend, database, seed, nghiệp vụ hoặc DOM.
7. CSS exception/`!important` còn lại và lý do.
8. Lỗi/rủi ro chưa xử lý; không được che giấu.
9. Kết luận chỉ được là `PASS — READY TO COMMIT` hoặc `FAIL — DO NOT COMMIT`.

Không được tuyên bố PASS nếu chưa kiểm tra toàn bộ mục trên.
```

## Prompt C — Kiểm tra độc lập sau khi làm

```text
Hãy thực hiện PHASE C — INDEPENDENT VERIFICATION cho đợt `PURCHASE ADVICE, CONSOLIDATION VÀ ORDER BATCH`. Không sửa thêm code trong bước này.

Đọc:
- hai file đặc tả chính trong `docs`;
- `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`;
- toàn bộ `git diff` hiện tại.

Kiểm tra độc lập:
1. File thay đổi có đúng ownership và đúng danh sách đợt không.
2. Có file `.js`, `.ts`, `.cs`, `.sql`, migration, seed hoặc backend nào thay đổi không.
3. `.cshtml` có thay cấu trúc thẻ, thứ tự DOM, `id`, `name`, `asp-*`, `data-*`, form action/method, hidden input hoặc hook không.
4. Index/Create/Edit/Details/Delete/Modal trong phạm vi đã dùng cùng visual contract chưa.
5. Button, input, table, modal, badge, page header và validation có cùng kích thước/semantics chưa.
6. Có selector global hoặc `!important` không cần thiết gây conflict không.
7. Desktop/tablet/mobile/zoom có overflow, chữ chìm, action bị che hoặc modal mất footer không.
8. Các chức năng sau còn hoạt động nguyên vẹn không:
- PA create/edit/details/status.
- Consolidation selection/submit.
- Batch Index/Details/action.
- Validation/numeric/table responsive.
9. `dotnet build`, `git diff --check` có pass không.
10. So sánh ảnh trước/sau; chỉ ra trang nào còn lệch với module đã chuẩn hóa.

Trả kết quả dạng bảng: `Tiêu chí | PASS/FAIL | Bằng chứng | Việc cần sửa`.
Kết luận duy nhất:
- `VERIFIED PASS — MAY COMMIT AND PUSH`, hoặc
- `VERIFIED FAIL — MUST FIX BEFORE COMMIT`.
Không sửa code trong Phase C.
```

## Checklist thủ công của frontend

- PA create/edit/details/status.
- Consolidation selection/submit.
- Batch Index/Details/action.
- Validation/numeric/table responsive.

Ngoài kiểm thử chức năng, bắt buộc chụp ít nhất:

- Ảnh trước và sau ở `1440×900`.
- Ảnh sau ở `1024×768`.
- Ảnh sau ở `390×844`.
- Một ảnh thể hiện form hoặc modal có validation.
- Một ảnh thể hiện table/list và action.

## Commit và push

Chỉ commit khi Prompt C trả `VERIFIED PASS — MAY COMMIT AND PUSH` và frontend đã tự kiểm tra.

```bash
git add "Areas/Admin/Views/AdminPurchaseAdvices/Create.cshtml"
git add "Areas/Admin/Views/AdminPurchaseAdvices/Details.cshtml"
git add "Areas/Admin/Views/AdminPurchaseAdvices/Edit.cshtml"
git add "Areas/Admin/Views/AdminPurchaseAdvices/Index.cshtml"
git add "Areas/Admin/Views/AdminPurchaseAdviceConsolidation/Index.cshtml"
git add "Areas/Admin/Views/AdminPurchaseOrderBatches/Details.cshtml"
git add "Areas/Admin/Views/AdminPurchaseOrderBatches/Index.cshtml"
git add "wwwroot/css/Admin/PurchaseAdvice/purchase-advice.css"
git add "wwwroot/css/Admin/Procurement/procurement-design-system.css"
git diff --cached --name-only
git diff --cached --stat
git diff --cached --check
git commit -m "style(admin-purchase-advice): unify purchase planning workflow UI"
git push -u origin feature/admin-ui-fe2-purchase-advice
```

Sau push: gửi nhóm trưởng branch, commit hash, ảnh trước/sau, checklist, build result và mọi exception còn lại. **Không tự merge.**


# ĐỢT 7 — PURCHASE ORDERS — CREATE, INDEX VÀ DETAILS

## Điều kiện bắt đầu

Purchase Advice/Batch đã được nhóm trưởng merge.

- Nhóm trưởng đã xác nhận các dependency cần thiết đã merge vào `develop`.
- `git status --short` sạch.
- Branch phải được tạo mới từ `develop` vừa pull.

```bash
git checkout develop
git pull origin develop
git checkout -b feature/admin-ui-fe2-purchase-orders
```

**Mức rủi ro:** Rất cao — chứng từ và workflow.

## File được phép chỉnh

- `Areas/Admin/Views/AdminPurchaseOrders/Create.cshtml`
- `Areas/Admin/Views/AdminPurchaseOrders/Details.cshtml`
- `Areas/Admin/Views/AdminPurchaseOrders/Index.cshtml`
- `wwwroot/css/Admin/Procurement/procurement-design-system.css`

Cấm chỉnh file ngoài danh sách này. File liên quan khác chỉ được đọc để hiểu hook và visual context.

## Kết quả giao diện phải đạt

- PO dùng document contract thống nhất với chứng từ Admin.
- Mã PO, supplier, branch, status, totals và line items nổi bật.
- Create/Details/action workflow rõ, không lẫn action điều hướng.

## Quy chuẩn chi tiết theo loại trang

- Index: filter/status/table/pagination.
- Create: document info, supplier, items, totals, notes, footer.
- Details: summary, status, line table, timeline/action nếu có.

## Phần phải bảo toàn tuyệt đối

- PO status transitions, supplier/item IDs, totals và submit.
- Batch/draft source data, hidden input và scripts.

## Prompt A — Audit read-only

Sao chép nguyên khối:

```text
Bạn đang làm Frontend 2 của CafeChain.

BẮT BUỘC đọc đầy đủ, theo thứ tự:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Toàn bộ source của các file được liệt kê cho đợt `PURCHASE ORDERS — CREATE, INDEX VÀ DETAILS`.

ĐÂY CHỈ LÀ PHASE A — AUDIT READ-ONLY. TUYỆT ĐỐI CHƯA SỬA CODE.

Phạm vi đợt này:
- `Areas/Admin/Views/AdminPurchaseOrders/Create.cshtml`
- `Areas/Admin/Views/AdminPurchaseOrders/Details.cshtml`
- `Areas/Admin/Views/AdminPurchaseOrders/Index.cshtml`
- `wwwroot/css/Admin/Procurement/procurement-design-system.css`

Hãy trả báo cáo theo đúng cấu trúc:

A. Scope confirmation
- Liệt kê đúng từng file được phép chỉnh.
- Liệt kê file liên quan đã đọc nhưng không được chỉnh.
- Xác nhận không sửa file ownership của frontend còn lại.

B. DOM và functional freeze
- Liệt kê form, table, modal, tab, collapse, partial, hidden input và dynamic region hiện có.
- Liệt kê `id`, `name`, `data-*`, `asp-*`, class/hook có khả năng được JS/Bootstrap/Select2/chart/map/drag-drop sử dụng.
- Nêu chính xác phần nào trong `.cshtml` chỉ được bổ sung class và phần nào phải để nguyên hoàn toàn.

C. Current UI audit
- Page header hiện tại.
- Button variants và kích thước hiện tại.
- Form control, validation, Select2 hiện tại.
- Table/list/pagination hiện tại.
- Modal/confirm/empty/loading/error hiện tại.
- Màu, token, spacing, radius, shadow và `!important` bị trùng hoặc lệch.

D. Target mapping
- Lập bảng `existing selector → visual contract --cc-* → file CSS sẽ chỉnh`.
- Chỉ ra selector nào dùng CSS chung và selector nào phải giữ exception nghiệp vụ.
- Mô tả cách làm Index/Create/Edit/Details/Delete/Modal đồng bộ mà không đổi DOM.

E. Risk register
- Specificity và thứ tự load CSS.
- Inline style và `<style>` cục bộ.
- Plugin control.
- Responsive table/modal.
- Nguy cơ selector lan sang StaffHub/POS/module frontend còn lại.

F. Test plan
- Trang và luồng cần test trước/sau.
- Viewport cần chụp.
- Functional regression bắt buộc.

Kết luận bằng một trong hai trạng thái:
- `READY FOR IMPLEMENTATION`: phạm vi rõ, không cần sửa JS/backend/DOM.
- `BLOCKED`: ghi đúng vấn đề cần nhóm trưởng giải quyết.

Sau báo cáo phải dừng. Không được chỉnh code hoặc tuyên bố đã hoàn thành giao diện.
```

Chỉ chuyển sang Prompt B khi báo cáo kết thúc bằng `READY FOR IMPLEMENTATION` và frontend đã đọc, đối chiếu đúng file/hook.

## Prompt B — Triển khai chính thức

```text
Bạn đang làm Frontend 2 của CafeChain.

BẮT BUỘC đọc lại:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Báo cáo audit Phase A vừa được duyệt.

PHASE B — CHỈ TRIỂN KHAI ĐỢT `PURCHASE ORDERS — CREATE, INDEX VÀ DETAILS`.

CHỈ ĐƯỢC CHỈNH:
- `Areas/Admin/Views/AdminPurchaseOrders/Create.cshtml`
- `Areas/Admin/Views/AdminPurchaseOrders/Details.cshtml`
- `Areas/Admin/Views/AdminPurchaseOrders/Index.cshtml`
- `wwwroot/css/Admin/Procurement/procurement-design-system.css`

MỤC TIÊU NGHIỆP VỤ VÀ GIAO DIỆN:
- PO dùng document contract thống nhất với chứng từ Admin.
- Mã PO, supplier, branch, status, totals và line items nổi bật.
- Create/Details/action workflow rõ, không lẫn action điều hướng.

QUY CHUẨN THEO TRANG/FORM:
- Index: filter/status/table/pagination.
- Create: document info, supplier, items, totals, notes, footer.
- Details: summary, status, line table, timeline/action nếu có.

PHẦN PHẢI ĐÓNG BĂNG:
- PO status transitions, supplier/item IDs, totals và submit.
- Batch/draft source data, hidden input và scripts.

RÀNG BUỘC TUYỆT ĐỐI:
- Chỉ chỉnh `.cshtml` và `.css` nêu trên.
- Không chỉnh JavaScript, backend, database, SQL, migration, seed, permission hoặc nghiệp vụ.
- Không thêm/xóa/đổi tên/di chuyển cấu trúc thẻ `.cshtml`.
- Giữ nguyên `id`, `name`, `data-*`, `asp-*`, route, action, method, hidden input, validation, modal/tab/collapse và mọi hook.
- Không thêm framework/package/font/script.
- Không format hàng loạt.
- Không dùng selector global lan khỏi Admin.
- Không tạo design token cạnh tranh với `--cc-*`.
- Không dùng `!important` nếu selector đúng có thể giải quyết; exception phải có comment module và lý do.
- Không chỉnh Online, Voucher, Wheel, StaffHub hoặc POS riêng.

CÁCH TRIỂN KHAI BẮT BUỘC:
1. Giữ CSS module cho bố cục đặc thù; dùng token `--cc-*` để đồng bộ màu, type, spacing, radius và shadow.
2. Ưu tiên CSS selector tương thích với markup hiện tại; `.cshtml` chỉ bổ sung class visual khi CSS hiện tại không thể xử lý an toàn.
3. Đồng bộ đầy đủ normal/hover/active/focus/disabled/loading/error/empty state.
4. Đồng bộ Index/Create/Edit/Details/Delete/Modal trong phạm vi; không chỉ sửa trang Index.
5. Sửa responsive ngay trong đợt, không để dành toàn bộ đến cuối.
6. Sau mỗi nhóm selector, kiểm tra một trang đại diện để phát hiện conflict sớm.

KIỂM TRA BẮT BUỘC SAU KHI CODE:
- Create từ luồng hợp lệ, validation và totals.
- Index/filter/details.
- Status actions.
- Responsive document table/action.

Trước khi kết luận, chạy và báo kết quả:
- `git diff --name-only`
- `git diff --stat`
- `git diff --check`
- `dotnet build`
- kiểm tra diff `.cshtml` để xác nhận không thay DOM/hook/binding.

OUTPUT CUỐI CÙNG PHẢI CÓ:
1. Danh sách chính xác file đã chỉnh và file chỉ đọc.
2. Tóm tắt thay đổi theo từng file.
3. Bảng selector cũ → contract mới.
4. Danh sách trạng thái UI đã hoàn thiện.
5. Danh sách viewport và luồng đã test, kèm kết quả đạt/chưa đạt.
6. Xác nhận không chỉnh file bị cấm, JavaScript, backend, database, seed, nghiệp vụ hoặc DOM.
7. CSS exception/`!important` còn lại và lý do.
8. Lỗi/rủi ro chưa xử lý; không được che giấu.
9. Kết luận chỉ được là `PASS — READY TO COMMIT` hoặc `FAIL — DO NOT COMMIT`.

Không được tuyên bố PASS nếu chưa kiểm tra toàn bộ mục trên.
```

## Prompt C — Kiểm tra độc lập sau khi làm

```text
Hãy thực hiện PHASE C — INDEPENDENT VERIFICATION cho đợt `PURCHASE ORDERS — CREATE, INDEX VÀ DETAILS`. Không sửa thêm code trong bước này.

Đọc:
- hai file đặc tả chính trong `docs`;
- `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`;
- toàn bộ `git diff` hiện tại.

Kiểm tra độc lập:
1. File thay đổi có đúng ownership và đúng danh sách đợt không.
2. Có file `.js`, `.ts`, `.cs`, `.sql`, migration, seed hoặc backend nào thay đổi không.
3. `.cshtml` có thay cấu trúc thẻ, thứ tự DOM, `id`, `name`, `asp-*`, `data-*`, form action/method, hidden input hoặc hook không.
4. Index/Create/Edit/Details/Delete/Modal trong phạm vi đã dùng cùng visual contract chưa.
5. Button, input, table, modal, badge, page header và validation có cùng kích thước/semantics chưa.
6. Có selector global hoặc `!important` không cần thiết gây conflict không.
7. Desktop/tablet/mobile/zoom có overflow, chữ chìm, action bị che hoặc modal mất footer không.
8. Các chức năng sau còn hoạt động nguyên vẹn không:
- Create từ luồng hợp lệ, validation và totals.
- Index/filter/details.
- Status actions.
- Responsive document table/action.
9. `dotnet build`, `git diff --check` có pass không.
10. So sánh ảnh trước/sau; chỉ ra trang nào còn lệch với module đã chuẩn hóa.

Trả kết quả dạng bảng: `Tiêu chí | PASS/FAIL | Bằng chứng | Việc cần sửa`.
Kết luận duy nhất:
- `VERIFIED PASS — MAY COMMIT AND PUSH`, hoặc
- `VERIFIED FAIL — MUST FIX BEFORE COMMIT`.
Không sửa code trong Phase C.
```

## Checklist thủ công của frontend

- Create từ luồng hợp lệ, validation và totals.
- Index/filter/details.
- Status actions.
- Responsive document table/action.

Ngoài kiểm thử chức năng, bắt buộc chụp ít nhất:

- Ảnh trước và sau ở `1440×900`.
- Ảnh sau ở `1024×768`.
- Ảnh sau ở `390×844`.
- Một ảnh thể hiện form hoặc modal có validation.
- Một ảnh thể hiện table/list và action.

## Commit và push

Chỉ commit khi Prompt C trả `VERIFIED PASS — MAY COMMIT AND PUSH` và frontend đã tự kiểm tra.

```bash
git add "Areas/Admin/Views/AdminPurchaseOrders/Create.cshtml"
git add "Areas/Admin/Views/AdminPurchaseOrders/Details.cshtml"
git add "Areas/Admin/Views/AdminPurchaseOrders/Index.cshtml"
git add "wwwroot/css/Admin/Procurement/procurement-design-system.css"
git diff --cached --name-only
git diff --cached --stat
git diff --cached --check
git commit -m "style(admin-purchase-order): unify purchase order UI"
git push -u origin feature/admin-ui-fe2-purchase-orders
```

Sau push: gửi nhóm trưởng branch, commit hash, ảnh trước/sau, checklist, build result và mọi exception còn lại. **Không tự merge.**


# ĐỢT 8 — BRANCH RECEIPTS — NHẬN HÀNG VÀ XÁC NHẬN THỰC NHẬN

## Điều kiện bắt đầu

Purchase Orders đã được nhóm trưởng merge.

- Nhóm trưởng đã xác nhận các dependency cần thiết đã merge vào `develop`.
- `git status --short` sạch.
- Branch phải được tạo mới từ `develop` vừa pull.

```bash
git checkout develop
git pull origin develop
git checkout -b feature/admin-ui-fe2-branch-receipts
```

**Mức rủi ro:** Rất cao — PO draft, receive và nhập kho.

## File được phép chỉnh

- `Areas/Admin/Views/AdminBranchReceipts/Create.cshtml`
- `Areas/Admin/Views/AdminBranchReceipts/Details.cshtml`
- `Areas/Admin/Views/AdminBranchReceipts/Index.cshtml`
- `Areas/Admin/Views/AdminBranchReceipts/PurchaseOrderDraft.cshtml`
- `wwwroot/css/Admin/Procurement/procurement-design-system.css`

Cấm chỉnh file ngoài danh sách này. File liên quan khác chỉ được đọc để hiểu hook và visual context.

## Kết quả giao diện phải đạt

- Thể hiện rõ PO dự kiến → số lượng thực nhận → chênh lệch → xác nhận.
- Index/Create/Details/PurchaseOrderDraft dùng cùng document contract.
- Chênh lệch có ngữ cảnh, không dùng danger cho mọi sai số.

## Quy chuẩn chi tiết theo loại trang

- Index: status/filter/table.
- PO Draft: thông tin nguồn và dòng dự kiến.
- Create: actual quantities, notes, summary, validation/action.
- Details: received/expected/difference và status.

## Phần phải bảo toàn tuyệt đối

- PO link, quantity binding, receipt status/action và import flow.
- Hidden IDs, calculation values và scripts.

## Prompt A — Audit read-only

Sao chép nguyên khối:

```text
Bạn đang làm Frontend 2 của CafeChain.

BẮT BUỘC đọc đầy đủ, theo thứ tự:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Toàn bộ source của các file được liệt kê cho đợt `BRANCH RECEIPTS — NHẬN HÀNG VÀ XÁC NHẬN THỰC NHẬN`.

ĐÂY CHỈ LÀ PHASE A — AUDIT READ-ONLY. TUYỆT ĐỐI CHƯA SỬA CODE.

Phạm vi đợt này:
- `Areas/Admin/Views/AdminBranchReceipts/Create.cshtml`
- `Areas/Admin/Views/AdminBranchReceipts/Details.cshtml`
- `Areas/Admin/Views/AdminBranchReceipts/Index.cshtml`
- `Areas/Admin/Views/AdminBranchReceipts/PurchaseOrderDraft.cshtml`
- `wwwroot/css/Admin/Procurement/procurement-design-system.css`

Hãy trả báo cáo theo đúng cấu trúc:

A. Scope confirmation
- Liệt kê đúng từng file được phép chỉnh.
- Liệt kê file liên quan đã đọc nhưng không được chỉnh.
- Xác nhận không sửa file ownership của frontend còn lại.

B. DOM và functional freeze
- Liệt kê form, table, modal, tab, collapse, partial, hidden input và dynamic region hiện có.
- Liệt kê `id`, `name`, `data-*`, `asp-*`, class/hook có khả năng được JS/Bootstrap/Select2/chart/map/drag-drop sử dụng.
- Nêu chính xác phần nào trong `.cshtml` chỉ được bổ sung class và phần nào phải để nguyên hoàn toàn.

C. Current UI audit
- Page header hiện tại.
- Button variants và kích thước hiện tại.
- Form control, validation, Select2 hiện tại.
- Table/list/pagination hiện tại.
- Modal/confirm/empty/loading/error hiện tại.
- Màu, token, spacing, radius, shadow và `!important` bị trùng hoặc lệch.

D. Target mapping
- Lập bảng `existing selector → visual contract --cc-* → file CSS sẽ chỉnh`.
- Chỉ ra selector nào dùng CSS chung và selector nào phải giữ exception nghiệp vụ.
- Mô tả cách làm Index/Create/Edit/Details/Delete/Modal đồng bộ mà không đổi DOM.

E. Risk register
- Specificity và thứ tự load CSS.
- Inline style và `<style>` cục bộ.
- Plugin control.
- Responsive table/modal.
- Nguy cơ selector lan sang StaffHub/POS/module frontend còn lại.

F. Test plan
- Trang và luồng cần test trước/sau.
- Viewport cần chụp.
- Functional regression bắt buộc.

Kết luận bằng một trong hai trạng thái:
- `READY FOR IMPLEMENTATION`: phạm vi rõ, không cần sửa JS/backend/DOM.
- `BLOCKED`: ghi đúng vấn đề cần nhóm trưởng giải quyết.

Sau báo cáo phải dừng. Không được chỉnh code hoặc tuyên bố đã hoàn thành giao diện.
```

Chỉ chuyển sang Prompt B khi báo cáo kết thúc bằng `READY FOR IMPLEMENTATION` và frontend đã đọc, đối chiếu đúng file/hook.

## Prompt B — Triển khai chính thức

```text
Bạn đang làm Frontend 2 của CafeChain.

BẮT BUỘC đọc lại:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Báo cáo audit Phase A vừa được duyệt.

PHASE B — CHỈ TRIỂN KHAI ĐỢT `BRANCH RECEIPTS — NHẬN HÀNG VÀ XÁC NHẬN THỰC NHẬN`.

CHỈ ĐƯỢC CHỈNH:
- `Areas/Admin/Views/AdminBranchReceipts/Create.cshtml`
- `Areas/Admin/Views/AdminBranchReceipts/Details.cshtml`
- `Areas/Admin/Views/AdminBranchReceipts/Index.cshtml`
- `Areas/Admin/Views/AdminBranchReceipts/PurchaseOrderDraft.cshtml`
- `wwwroot/css/Admin/Procurement/procurement-design-system.css`

MỤC TIÊU NGHIỆP VỤ VÀ GIAO DIỆN:
- Thể hiện rõ PO dự kiến → số lượng thực nhận → chênh lệch → xác nhận.
- Index/Create/Details/PurchaseOrderDraft dùng cùng document contract.
- Chênh lệch có ngữ cảnh, không dùng danger cho mọi sai số.

QUY CHUẨN THEO TRANG/FORM:
- Index: status/filter/table.
- PO Draft: thông tin nguồn và dòng dự kiến.
- Create: actual quantities, notes, summary, validation/action.
- Details: received/expected/difference và status.

PHẦN PHẢI ĐÓNG BĂNG:
- PO link, quantity binding, receipt status/action và import flow.
- Hidden IDs, calculation values và scripts.

RÀNG BUỘC TUYỆT ĐỐI:
- Chỉ chỉnh `.cshtml` và `.css` nêu trên.
- Không chỉnh JavaScript, backend, database, SQL, migration, seed, permission hoặc nghiệp vụ.
- Không thêm/xóa/đổi tên/di chuyển cấu trúc thẻ `.cshtml`.
- Giữ nguyên `id`, `name`, `data-*`, `asp-*`, route, action, method, hidden input, validation, modal/tab/collapse và mọi hook.
- Không thêm framework/package/font/script.
- Không format hàng loạt.
- Không dùng selector global lan khỏi Admin.
- Không tạo design token cạnh tranh với `--cc-*`.
- Không dùng `!important` nếu selector đúng có thể giải quyết; exception phải có comment module và lý do.
- Không chỉnh Online, Voucher, Wheel, StaffHub hoặc POS riêng.

CÁCH TRIỂN KHAI BẮT BUỘC:
1. Giữ CSS module cho bố cục đặc thù; dùng token `--cc-*` để đồng bộ màu, type, spacing, radius và shadow.
2. Ưu tiên CSS selector tương thích với markup hiện tại; `.cshtml` chỉ bổ sung class visual khi CSS hiện tại không thể xử lý an toàn.
3. Đồng bộ đầy đủ normal/hover/active/focus/disabled/loading/error/empty state.
4. Đồng bộ Index/Create/Edit/Details/Delete/Modal trong phạm vi; không chỉ sửa trang Index.
5. Sửa responsive ngay trong đợt, không để dành toàn bộ đến cuối.
6. Sau mỗi nhóm selector, kiểm tra một trang đại diện để phát hiện conflict sớm.

KIỂM TRA BẮT BUỘC SAU KHI CODE:
- Mở PO Draft, tạo receipt, validation số lượng.
- Index/details/status.
- Difference display và totals.
- Responsive table/form.

Trước khi kết luận, chạy và báo kết quả:
- `git diff --name-only`
- `git diff --stat`
- `git diff --check`
- `dotnet build`
- kiểm tra diff `.cshtml` để xác nhận không thay DOM/hook/binding.

OUTPUT CUỐI CÙNG PHẢI CÓ:
1. Danh sách chính xác file đã chỉnh và file chỉ đọc.
2. Tóm tắt thay đổi theo từng file.
3. Bảng selector cũ → contract mới.
4. Danh sách trạng thái UI đã hoàn thiện.
5. Danh sách viewport và luồng đã test, kèm kết quả đạt/chưa đạt.
6. Xác nhận không chỉnh file bị cấm, JavaScript, backend, database, seed, nghiệp vụ hoặc DOM.
7. CSS exception/`!important` còn lại và lý do.
8. Lỗi/rủi ro chưa xử lý; không được che giấu.
9. Kết luận chỉ được là `PASS — READY TO COMMIT` hoặc `FAIL — DO NOT COMMIT`.

Không được tuyên bố PASS nếu chưa kiểm tra toàn bộ mục trên.
```

## Prompt C — Kiểm tra độc lập sau khi làm

```text
Hãy thực hiện PHASE C — INDEPENDENT VERIFICATION cho đợt `BRANCH RECEIPTS — NHẬN HÀNG VÀ XÁC NHẬN THỰC NHẬN`. Không sửa thêm code trong bước này.

Đọc:
- hai file đặc tả chính trong `docs`;
- `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`;
- toàn bộ `git diff` hiện tại.

Kiểm tra độc lập:
1. File thay đổi có đúng ownership và đúng danh sách đợt không.
2. Có file `.js`, `.ts`, `.cs`, `.sql`, migration, seed hoặc backend nào thay đổi không.
3. `.cshtml` có thay cấu trúc thẻ, thứ tự DOM, `id`, `name`, `asp-*`, `data-*`, form action/method, hidden input hoặc hook không.
4. Index/Create/Edit/Details/Delete/Modal trong phạm vi đã dùng cùng visual contract chưa.
5. Button, input, table, modal, badge, page header và validation có cùng kích thước/semantics chưa.
6. Có selector global hoặc `!important` không cần thiết gây conflict không.
7. Desktop/tablet/mobile/zoom có overflow, chữ chìm, action bị che hoặc modal mất footer không.
8. Các chức năng sau còn hoạt động nguyên vẹn không:
- Mở PO Draft, tạo receipt, validation số lượng.
- Index/details/status.
- Difference display và totals.
- Responsive table/form.
9. `dotnet build`, `git diff --check` có pass không.
10. So sánh ảnh trước/sau; chỉ ra trang nào còn lệch với module đã chuẩn hóa.

Trả kết quả dạng bảng: `Tiêu chí | PASS/FAIL | Bằng chứng | Việc cần sửa`.
Kết luận duy nhất:
- `VERIFIED PASS — MAY COMMIT AND PUSH`, hoặc
- `VERIFIED FAIL — MUST FIX BEFORE COMMIT`.
Không sửa code trong Phase C.
```

## Checklist thủ công của frontend

- Mở PO Draft, tạo receipt, validation số lượng.
- Index/details/status.
- Difference display và totals.
- Responsive table/form.

Ngoài kiểm thử chức năng, bắt buộc chụp ít nhất:

- Ảnh trước và sau ở `1440×900`.
- Ảnh sau ở `1024×768`.
- Ảnh sau ở `390×844`.
- Một ảnh thể hiện form hoặc modal có validation.
- Một ảnh thể hiện table/list và action.

## Commit và push

Chỉ commit khi Prompt C trả `VERIFIED PASS — MAY COMMIT AND PUSH` và frontend đã tự kiểm tra.

```bash
git add "Areas/Admin/Views/AdminBranchReceipts/Create.cshtml"
git add "Areas/Admin/Views/AdminBranchReceipts/Details.cshtml"
git add "Areas/Admin/Views/AdminBranchReceipts/Index.cshtml"
git add "Areas/Admin/Views/AdminBranchReceipts/PurchaseOrderDraft.cshtml"
git add "wwwroot/css/Admin/Procurement/procurement-design-system.css"
git diff --cached --name-only
git diff --cached --stat
git diff --cached --check
git commit -m "style(admin-branch-receipt): unify branch receipt UI"
git push -u origin feature/admin-ui-fe2-branch-receipts
```

Sau push: gửi nhóm trưởng branch, commit hash, ảnh trước/sau, checklist, build result và mọi exception còn lại. **Không tự merge.**


# ĐỢT 9 — QUẢN LÝ ĐÁ THEO CA — INDEX, DETAILS VÀ REPORT

## Điều kiện bắt đầu

Core FE1 đã merge; không phụ thuộc procurement nhưng làm sau để giảm song song.

- Nhóm trưởng đã xác nhận các dependency cần thiết đã merge vào `develop`.
- `git status --short` sạch.
- Branch phải được tạo mới từ `develop` vừa pull.

```bash
git checkout develop
git pull origin develop
git checkout -b feature/admin-ui-fe2-operational-ice
```

**Mức rủi ro:** Rất cao — 17 form và workflow theo ca.

## File được phép chỉnh

- `Areas/Admin/Views/AdminOperationalIce/Details.cshtml`
- `Areas/Admin/Views/AdminOperationalIce/Index.cshtml`
- `Areas/Admin/Views/AdminOperationalIce/Report.cshtml`
- `wwwroot/css/Admin/OperationalIce/operational-ice.css`

Cấm chỉnh file ngoài danh sách này. File liên quan khác chỉ được đọc để hiểu hook và visual context.

## Kết quả giao diện phải đạt

- Tiến trình ca, định mức, cấp đầu ca, bổ sung, lý thuyết, chênh lệch và chi phí rõ.
- Chỉ action đúng bước hiện tại là primary.
- Report tối ưu đọc/in, không gradient hoặc shadow quá nhiều.

## Quy chuẩn chi tiết theo loại trang

- Index: filter/summary/list/status/action.
- Details: progress/timeline, KPI, forms theo trạng thái, approval.
- Report: header, summary, table, print readability.

## Phần phải bảo toàn tuyệt đối

- Mọi action form theo trạng thái, WorkShift link, IDs và permission.
- Calculation values, thresholds và report data.

## Prompt A — Audit read-only

Sao chép nguyên khối:

```text
Bạn đang làm Frontend 2 của CafeChain.

BẮT BUỘC đọc đầy đủ, theo thứ tự:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Toàn bộ source của các file được liệt kê cho đợt `QUẢN LÝ ĐÁ THEO CA — INDEX, DETAILS VÀ REPORT`.

ĐÂY CHỈ LÀ PHASE A — AUDIT READ-ONLY. TUYỆT ĐỐI CHƯA SỬA CODE.

Phạm vi đợt này:
- `Areas/Admin/Views/AdminOperationalIce/Details.cshtml`
- `Areas/Admin/Views/AdminOperationalIce/Index.cshtml`
- `Areas/Admin/Views/AdminOperationalIce/Report.cshtml`
- `wwwroot/css/Admin/OperationalIce/operational-ice.css`

Hãy trả báo cáo theo đúng cấu trúc:

A. Scope confirmation
- Liệt kê đúng từng file được phép chỉnh.
- Liệt kê file liên quan đã đọc nhưng không được chỉnh.
- Xác nhận không sửa file ownership của frontend còn lại.

B. DOM và functional freeze
- Liệt kê form, table, modal, tab, collapse, partial, hidden input và dynamic region hiện có.
- Liệt kê `id`, `name`, `data-*`, `asp-*`, class/hook có khả năng được JS/Bootstrap/Select2/chart/map/drag-drop sử dụng.
- Nêu chính xác phần nào trong `.cshtml` chỉ được bổ sung class và phần nào phải để nguyên hoàn toàn.

C. Current UI audit
- Page header hiện tại.
- Button variants và kích thước hiện tại.
- Form control, validation, Select2 hiện tại.
- Table/list/pagination hiện tại.
- Modal/confirm/empty/loading/error hiện tại.
- Màu, token, spacing, radius, shadow và `!important` bị trùng hoặc lệch.

D. Target mapping
- Lập bảng `existing selector → visual contract --cc-* → file CSS sẽ chỉnh`.
- Chỉ ra selector nào dùng CSS chung và selector nào phải giữ exception nghiệp vụ.
- Mô tả cách làm Index/Create/Edit/Details/Delete/Modal đồng bộ mà không đổi DOM.

E. Risk register
- Specificity và thứ tự load CSS.
- Inline style và `<style>` cục bộ.
- Plugin control.
- Responsive table/modal.
- Nguy cơ selector lan sang StaffHub/POS/module frontend còn lại.

F. Test plan
- Trang và luồng cần test trước/sau.
- Viewport cần chụp.
- Functional regression bắt buộc.

Kết luận bằng một trong hai trạng thái:
- `READY FOR IMPLEMENTATION`: phạm vi rõ, không cần sửa JS/backend/DOM.
- `BLOCKED`: ghi đúng vấn đề cần nhóm trưởng giải quyết.

Sau báo cáo phải dừng. Không được chỉnh code hoặc tuyên bố đã hoàn thành giao diện.
```

Chỉ chuyển sang Prompt B khi báo cáo kết thúc bằng `READY FOR IMPLEMENTATION` và frontend đã đọc, đối chiếu đúng file/hook.

## Prompt B — Triển khai chính thức

```text
Bạn đang làm Frontend 2 của CafeChain.

BẮT BUỘC đọc lại:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Báo cáo audit Phase A vừa được duyệt.

PHASE B — CHỈ TRIỂN KHAI ĐỢT `QUẢN LÝ ĐÁ THEO CA — INDEX, DETAILS VÀ REPORT`.

CHỈ ĐƯỢC CHỈNH:
- `Areas/Admin/Views/AdminOperationalIce/Details.cshtml`
- `Areas/Admin/Views/AdminOperationalIce/Index.cshtml`
- `Areas/Admin/Views/AdminOperationalIce/Report.cshtml`
- `wwwroot/css/Admin/OperationalIce/operational-ice.css`

MỤC TIÊU NGHIỆP VỤ VÀ GIAO DIỆN:
- Tiến trình ca, định mức, cấp đầu ca, bổ sung, lý thuyết, chênh lệch và chi phí rõ.
- Chỉ action đúng bước hiện tại là primary.
- Report tối ưu đọc/in, không gradient hoặc shadow quá nhiều.

QUY CHUẨN THEO TRANG/FORM:
- Index: filter/summary/list/status/action.
- Details: progress/timeline, KPI, forms theo trạng thái, approval.
- Report: header, summary, table, print readability.

PHẦN PHẢI ĐÓNG BĂNG:
- Mọi action form theo trạng thái, WorkShift link, IDs và permission.
- Calculation values, thresholds và report data.

RÀNG BUỘC TUYỆT ĐỐI:
- Chỉ chỉnh `.cshtml` và `.css` nêu trên.
- Không chỉnh JavaScript, backend, database, SQL, migration, seed, permission hoặc nghiệp vụ.
- Không thêm/xóa/đổi tên/di chuyển cấu trúc thẻ `.cshtml`.
- Giữ nguyên `id`, `name`, `data-*`, `asp-*`, route, action, method, hidden input, validation, modal/tab/collapse và mọi hook.
- Không thêm framework/package/font/script.
- Không format hàng loạt.
- Không dùng selector global lan khỏi Admin.
- Không tạo design token cạnh tranh với `--cc-*`.
- Không dùng `!important` nếu selector đúng có thể giải quyết; exception phải có comment module và lý do.
- Không chỉnh Online, Voucher, Wheel, StaffHub hoặc POS riêng.

CÁCH TRIỂN KHAI BẮT BUỘC:
1. Giữ CSS module cho bố cục đặc thù; dùng token `--cc-*` để đồng bộ màu, type, spacing, radius và shadow.
2. Ưu tiên CSS selector tương thích với markup hiện tại; `.cshtml` chỉ bổ sung class visual khi CSS hiện tại không thể xử lý an toàn.
3. Đồng bộ đầy đủ normal/hover/active/focus/disabled/loading/error/empty state.
4. Đồng bộ Index/Create/Edit/Details/Delete/Modal trong phạm vi; không chỉ sửa trang Index.
5. Sửa responsive ngay trong đợt, không để dành toàn bộ đến cuối.
6. Sau mỗi nhóm selector, kiểm tra một trang đại diện để phát hiện conflict sớm.

KIỂM TRA BẮT BUỘC SAU KHI CODE:
- Mở ca, cấp đầu/bổ sung/xác nhận/chốt/duyệt theo quyền khả dụng.
- Index/filter/details/report.
- Print preview.
- Responsive forms/timeline/table.

Trước khi kết luận, chạy và báo kết quả:
- `git diff --name-only`
- `git diff --stat`
- `git diff --check`
- `dotnet build`
- kiểm tra diff `.cshtml` để xác nhận không thay DOM/hook/binding.

OUTPUT CUỐI CÙNG PHẢI CÓ:
1. Danh sách chính xác file đã chỉnh và file chỉ đọc.
2. Tóm tắt thay đổi theo từng file.
3. Bảng selector cũ → contract mới.
4. Danh sách trạng thái UI đã hoàn thiện.
5. Danh sách viewport và luồng đã test, kèm kết quả đạt/chưa đạt.
6. Xác nhận không chỉnh file bị cấm, JavaScript, backend, database, seed, nghiệp vụ hoặc DOM.
7. CSS exception/`!important` còn lại và lý do.
8. Lỗi/rủi ro chưa xử lý; không được che giấu.
9. Kết luận chỉ được là `PASS — READY TO COMMIT` hoặc `FAIL — DO NOT COMMIT`.

Không được tuyên bố PASS nếu chưa kiểm tra toàn bộ mục trên.
```

## Prompt C — Kiểm tra độc lập sau khi làm

```text
Hãy thực hiện PHASE C — INDEPENDENT VERIFICATION cho đợt `QUẢN LÝ ĐÁ THEO CA — INDEX, DETAILS VÀ REPORT`. Không sửa thêm code trong bước này.

Đọc:
- hai file đặc tả chính trong `docs`;
- `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`;
- toàn bộ `git diff` hiện tại.

Kiểm tra độc lập:
1. File thay đổi có đúng ownership và đúng danh sách đợt không.
2. Có file `.js`, `.ts`, `.cs`, `.sql`, migration, seed hoặc backend nào thay đổi không.
3. `.cshtml` có thay cấu trúc thẻ, thứ tự DOM, `id`, `name`, `asp-*`, `data-*`, form action/method, hidden input hoặc hook không.
4. Index/Create/Edit/Details/Delete/Modal trong phạm vi đã dùng cùng visual contract chưa.
5. Button, input, table, modal, badge, page header và validation có cùng kích thước/semantics chưa.
6. Có selector global hoặc `!important` không cần thiết gây conflict không.
7. Desktop/tablet/mobile/zoom có overflow, chữ chìm, action bị che hoặc modal mất footer không.
8. Các chức năng sau còn hoạt động nguyên vẹn không:
- Mở ca, cấp đầu/bổ sung/xác nhận/chốt/duyệt theo quyền khả dụng.
- Index/filter/details/report.
- Print preview.
- Responsive forms/timeline/table.
9. `dotnet build`, `git diff --check` có pass không.
10. So sánh ảnh trước/sau; chỉ ra trang nào còn lệch với module đã chuẩn hóa.

Trả kết quả dạng bảng: `Tiêu chí | PASS/FAIL | Bằng chứng | Việc cần sửa`.
Kết luận duy nhất:
- `VERIFIED PASS — MAY COMMIT AND PUSH`, hoặc
- `VERIFIED FAIL — MUST FIX BEFORE COMMIT`.
Không sửa code trong Phase C.
```

## Checklist thủ công của frontend

- Mở ca, cấp đầu/bổ sung/xác nhận/chốt/duyệt theo quyền khả dụng.
- Index/filter/details/report.
- Print preview.
- Responsive forms/timeline/table.

Ngoài kiểm thử chức năng, bắt buộc chụp ít nhất:

- Ảnh trước và sau ở `1440×900`.
- Ảnh sau ở `1024×768`.
- Ảnh sau ở `390×844`.
- Một ảnh thể hiện form hoặc modal có validation.
- Một ảnh thể hiện table/list và action.

## Commit và push

Chỉ commit khi Prompt C trả `VERIFIED PASS — MAY COMMIT AND PUSH` và frontend đã tự kiểm tra.

```bash
git add "Areas/Admin/Views/AdminOperationalIce/Details.cshtml"
git add "Areas/Admin/Views/AdminOperationalIce/Index.cshtml"
git add "Areas/Admin/Views/AdminOperationalIce/Report.cshtml"
git add "wwwroot/css/Admin/OperationalIce/operational-ice.css"
git diff --cached --name-only
git diff --cached --stat
git diff --cached --check
git commit -m "style(admin-operational-ice): unify operational ice workflow UI"
git push -u origin feature/admin-ui-fe2-operational-ice
```

Sau push: gửi nhóm trưởng branch, commit hash, ảnh trước/sau, checklist, build result và mọi exception còn lại. **Không tự merge.**


# ĐỢT 10 — STAFF VÀ PERMISSION

## Điều kiện bắt đầu

Core FE1 đã merge. Đợt này phải merge trước StaffShift.

- Nhóm trưởng đã xác nhận các dependency cần thiết đã merge vào `develop`.
- `git status --short` sạch.
- Branch phải được tạo mới từ `develop` vừa pull.

```bash
git checkout develop
git pull origin develop
git checkout -b feature/admin-ui-fe2-staff-permission
```

**Mức rủi ro:** Rất cao — role/scope, modal và permission matrix.

## File được phép chỉnh

- `Areas/Admin/Views/AdminStaff/Edit.cshtml`
- `Areas/Admin/Views/AdminStaff/Index.cshtml`
- `Areas/Admin/Views/AdminStaff/_CreateStaffModal.cshtml`
- `Areas/Admin/Views/AdminPermission/Index.cshtml`
- `wwwroot/css/Admin/Staff/staff.css`
- `wwwroot/css/Admin/Permissions/admin-permissions.css`

Cấm chỉnh file ngoài danh sách này. File liên quan khác chỉ được đọc để hiểu hook và visual context.

## Kết quả giao diện phải đạt

- Staff Index/Create/Edit và Permission Matrix cùng HR contract.
- Role, scope, store và account hierarchy rõ.
- Matrix dễ quét theo hàng/cột, checkbox/focus rõ.

## Quy chuẩn chi tiết theo loại trang

- Staff Index: header/filter/table/status/action.
- Create/Edit: 3 nhóm thông tin chung, vai trò/phạm vi, công việc/tài khoản.
- Permission: role/user override/store scope tách rõ; modal cùng contract.

## Phần phải bảo toàn tuyệt đối

- Role/scope logic, store selector, checkbox names/values và modal IDs.
- Permission conditions, hidden IDs, scripts.

## Prompt A — Audit read-only

Sao chép nguyên khối:

```text
Bạn đang làm Frontend 2 của CafeChain.

BẮT BUỘC đọc đầy đủ, theo thứ tự:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Toàn bộ source của các file được liệt kê cho đợt `STAFF VÀ PERMISSION`.

ĐÂY CHỈ LÀ PHASE A — AUDIT READ-ONLY. TUYỆT ĐỐI CHƯA SỬA CODE.

Phạm vi đợt này:
- `Areas/Admin/Views/AdminStaff/Edit.cshtml`
- `Areas/Admin/Views/AdminStaff/Index.cshtml`
- `Areas/Admin/Views/AdminStaff/_CreateStaffModal.cshtml`
- `Areas/Admin/Views/AdminPermission/Index.cshtml`
- `wwwroot/css/Admin/Staff/staff.css`
- `wwwroot/css/Admin/Permissions/admin-permissions.css`

Hãy trả báo cáo theo đúng cấu trúc:

A. Scope confirmation
- Liệt kê đúng từng file được phép chỉnh.
- Liệt kê file liên quan đã đọc nhưng không được chỉnh.
- Xác nhận không sửa file ownership của frontend còn lại.

B. DOM và functional freeze
- Liệt kê form, table, modal, tab, collapse, partial, hidden input và dynamic region hiện có.
- Liệt kê `id`, `name`, `data-*`, `asp-*`, class/hook có khả năng được JS/Bootstrap/Select2/chart/map/drag-drop sử dụng.
- Nêu chính xác phần nào trong `.cshtml` chỉ được bổ sung class và phần nào phải để nguyên hoàn toàn.

C. Current UI audit
- Page header hiện tại.
- Button variants và kích thước hiện tại.
- Form control, validation, Select2 hiện tại.
- Table/list/pagination hiện tại.
- Modal/confirm/empty/loading/error hiện tại.
- Màu, token, spacing, radius, shadow và `!important` bị trùng hoặc lệch.

D. Target mapping
- Lập bảng `existing selector → visual contract --cc-* → file CSS sẽ chỉnh`.
- Chỉ ra selector nào dùng CSS chung và selector nào phải giữ exception nghiệp vụ.
- Mô tả cách làm Index/Create/Edit/Details/Delete/Modal đồng bộ mà không đổi DOM.

E. Risk register
- Specificity và thứ tự load CSS.
- Inline style và `<style>` cục bộ.
- Plugin control.
- Responsive table/modal.
- Nguy cơ selector lan sang StaffHub/POS/module frontend còn lại.

F. Test plan
- Trang và luồng cần test trước/sau.
- Viewport cần chụp.
- Functional regression bắt buộc.

Kết luận bằng một trong hai trạng thái:
- `READY FOR IMPLEMENTATION`: phạm vi rõ, không cần sửa JS/backend/DOM.
- `BLOCKED`: ghi đúng vấn đề cần nhóm trưởng giải quyết.

Sau báo cáo phải dừng. Không được chỉnh code hoặc tuyên bố đã hoàn thành giao diện.
```

Chỉ chuyển sang Prompt B khi báo cáo kết thúc bằng `READY FOR IMPLEMENTATION` và frontend đã đọc, đối chiếu đúng file/hook.

## Prompt B — Triển khai chính thức

```text
Bạn đang làm Frontend 2 của CafeChain.

BẮT BUỘC đọc lại:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Báo cáo audit Phase A vừa được duyệt.

PHASE B — CHỈ TRIỂN KHAI ĐỢT `STAFF VÀ PERMISSION`.

CHỈ ĐƯỢC CHỈNH:
- `Areas/Admin/Views/AdminStaff/Edit.cshtml`
- `Areas/Admin/Views/AdminStaff/Index.cshtml`
- `Areas/Admin/Views/AdminStaff/_CreateStaffModal.cshtml`
- `Areas/Admin/Views/AdminPermission/Index.cshtml`
- `wwwroot/css/Admin/Staff/staff.css`
- `wwwroot/css/Admin/Permissions/admin-permissions.css`

MỤC TIÊU NGHIỆP VỤ VÀ GIAO DIỆN:
- Staff Index/Create/Edit và Permission Matrix cùng HR contract.
- Role, scope, store và account hierarchy rõ.
- Matrix dễ quét theo hàng/cột, checkbox/focus rõ.

QUY CHUẨN THEO TRANG/FORM:
- Staff Index: header/filter/table/status/action.
- Create/Edit: 3 nhóm thông tin chung, vai trò/phạm vi, công việc/tài khoản.
- Permission: role/user override/store scope tách rõ; modal cùng contract.

PHẦN PHẢI ĐÓNG BĂNG:
- Role/scope logic, store selector, checkbox names/values và modal IDs.
- Permission conditions, hidden IDs, scripts.

RÀNG BUỘC TUYỆT ĐỐI:
- Chỉ chỉnh `.cshtml` và `.css` nêu trên.
- Không chỉnh JavaScript, backend, database, SQL, migration, seed, permission hoặc nghiệp vụ.
- Không thêm/xóa/đổi tên/di chuyển cấu trúc thẻ `.cshtml`.
- Giữ nguyên `id`, `name`, `data-*`, `asp-*`, route, action, method, hidden input, validation, modal/tab/collapse và mọi hook.
- Không thêm framework/package/font/script.
- Không format hàng loạt.
- Không dùng selector global lan khỏi Admin.
- Không tạo design token cạnh tranh với `--cc-*`.
- Không dùng `!important` nếu selector đúng có thể giải quyết; exception phải có comment module và lý do.
- Không chỉnh Online, Voucher, Wheel, StaffHub hoặc POS riêng.

CÁCH TRIỂN KHAI BẮT BUỘC:
1. Giữ CSS module cho bố cục đặc thù; dùng token `--cc-*` để đồng bộ màu, type, spacing, radius và shadow.
2. Ưu tiên CSS selector tương thích với markup hiện tại; `.cshtml` chỉ bổ sung class visual khi CSS hiện tại không thể xử lý an toàn.
3. Đồng bộ đầy đủ normal/hover/active/focus/disabled/loading/error/empty state.
4. Đồng bộ Index/Create/Edit/Details/Delete/Modal trong phạm vi; không chỉ sửa trang Index.
5. Sửa responsive ngay trong đợt, không để dành toàn bộ đến cuối.
6. Sau mỗi nhóm selector, kiểm tra một trang đại diện để phát hiện conflict sớm.

KIỂM TRA BẮT BUỘC SAU KHI CODE:
- Staff Create/Edit/validation/status.
- Role/scope/store behavior.
- Permission matrix, modal, save và checkbox.
- Keyboard/focus và responsive matrix.

Trước khi kết luận, chạy và báo kết quả:
- `git diff --name-only`
- `git diff --stat`
- `git diff --check`
- `dotnet build`
- kiểm tra diff `.cshtml` để xác nhận không thay DOM/hook/binding.

OUTPUT CUỐI CÙNG PHẢI CÓ:
1. Danh sách chính xác file đã chỉnh và file chỉ đọc.
2. Tóm tắt thay đổi theo từng file.
3. Bảng selector cũ → contract mới.
4. Danh sách trạng thái UI đã hoàn thiện.
5. Danh sách viewport và luồng đã test, kèm kết quả đạt/chưa đạt.
6. Xác nhận không chỉnh file bị cấm, JavaScript, backend, database, seed, nghiệp vụ hoặc DOM.
7. CSS exception/`!important` còn lại và lý do.
8. Lỗi/rủi ro chưa xử lý; không được che giấu.
9. Kết luận chỉ được là `PASS — READY TO COMMIT` hoặc `FAIL — DO NOT COMMIT`.

Không được tuyên bố PASS nếu chưa kiểm tra toàn bộ mục trên.
```

## Prompt C — Kiểm tra độc lập sau khi làm

```text
Hãy thực hiện PHASE C — INDEPENDENT VERIFICATION cho đợt `STAFF VÀ PERMISSION`. Không sửa thêm code trong bước này.

Đọc:
- hai file đặc tả chính trong `docs`;
- `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`;
- toàn bộ `git diff` hiện tại.

Kiểm tra độc lập:
1. File thay đổi có đúng ownership và đúng danh sách đợt không.
2. Có file `.js`, `.ts`, `.cs`, `.sql`, migration, seed hoặc backend nào thay đổi không.
3. `.cshtml` có thay cấu trúc thẻ, thứ tự DOM, `id`, `name`, `asp-*`, `data-*`, form action/method, hidden input hoặc hook không.
4. Index/Create/Edit/Details/Delete/Modal trong phạm vi đã dùng cùng visual contract chưa.
5. Button, input, table, modal, badge, page header và validation có cùng kích thước/semantics chưa.
6. Có selector global hoặc `!important` không cần thiết gây conflict không.
7. Desktop/tablet/mobile/zoom có overflow, chữ chìm, action bị che hoặc modal mất footer không.
8. Các chức năng sau còn hoạt động nguyên vẹn không:
- Staff Create/Edit/validation/status.
- Role/scope/store behavior.
- Permission matrix, modal, save và checkbox.
- Keyboard/focus và responsive matrix.
9. `dotnet build`, `git diff --check` có pass không.
10. So sánh ảnh trước/sau; chỉ ra trang nào còn lệch với module đã chuẩn hóa.

Trả kết quả dạng bảng: `Tiêu chí | PASS/FAIL | Bằng chứng | Việc cần sửa`.
Kết luận duy nhất:
- `VERIFIED PASS — MAY COMMIT AND PUSH`, hoặc
- `VERIFIED FAIL — MUST FIX BEFORE COMMIT`.
Không sửa code trong Phase C.
```

## Checklist thủ công của frontend

- Staff Create/Edit/validation/status.
- Role/scope/store behavior.
- Permission matrix, modal, save và checkbox.
- Keyboard/focus và responsive matrix.

Ngoài kiểm thử chức năng, bắt buộc chụp ít nhất:

- Ảnh trước và sau ở `1440×900`.
- Ảnh sau ở `1024×768`.
- Ảnh sau ở `390×844`.
- Một ảnh thể hiện form hoặc modal có validation.
- Một ảnh thể hiện table/list và action.

## Commit và push

Chỉ commit khi Prompt C trả `VERIFIED PASS — MAY COMMIT AND PUSH` và frontend đã tự kiểm tra.

```bash
git add "Areas/Admin/Views/AdminStaff/Edit.cshtml"
git add "Areas/Admin/Views/AdminStaff/Index.cshtml"
git add "Areas/Admin/Views/AdminStaff/_CreateStaffModal.cshtml"
git add "Areas/Admin/Views/AdminPermission/Index.cshtml"
git add "wwwroot/css/Admin/Staff/staff.css"
git add "wwwroot/css/Admin/Permissions/admin-permissions.css"
git diff --cached --name-only
git diff --cached --stat
git diff --cached --check
git commit -m "style(admin-hr): unify staff and permission UI"
git push -u origin feature/admin-ui-fe2-staff-permission
```

Sau push: gửi nhóm trưởng branch, commit hash, ảnh trước/sau, checklist, build result và mọi exception còn lại. **Không tự merge.**


# ĐỢT 11 — STAFF SHIFT VÀ SHIFT OPTIMIZATION

## Điều kiện bắt đầu

Staff/Permission đã được nhóm trưởng merge.

- Nhóm trưởng đã xác nhận các dependency cần thiết đã merge vào `develop`.
- `git status --short` sạch.
- Branch phải được tạo mới từ `develop` vừa pull.

```bash
git checkout develop
git pull origin develop
git checkout -b feature/admin-ui-fe2-staff-shift
```

**Mức rủi ro:** Rất cao — scheduler, modal, dropdown và drag/drop hook.

## File được phép chỉnh

- `Areas/Admin/Views/AdminStaffShift/Index.cshtml`
- `Areas/Admin/Views/AdminShiftOptimization/Index.cshtml`
- `wwwroot/css/Admin/StaffShift/admin-staff-shift.css`
- `wwwroot/css/Admin/StaffShift/shift-optimization.css`

Cấm chỉnh file ngoài danh sách này. File liên quan khác chỉ được đọc để hiểu hook và visual context.

## Kết quả giao diện phải đạt

- Lịch tuần ưu tiên tên nhân viên, ngày, ca, trạng thái và ca qua đêm.
- Optimization card/form/result cùng HR contract.
- Không để chữ tối trên nền tối hoặc nâu nhạt trên kem.

## Quy chuẩn chi tiết theo loại trang

- Scheduler: sticky header nếu CSS cho phép, cột nhân viên đủ rộng, today/cancelled/+1 rõ.
- Modal create/edit/cancel shift cùng contract.
- Optimization: filter/input/result/recommendation rõ, không lấn lịch.

## Phần phải bảo toàn tuyệt đối

- Drag/drop/dropdown/modal IDs, schedule data attributes và scripts.
- Shift status, overnight logic, role/scope.

## Prompt A — Audit read-only

Sao chép nguyên khối:

```text
Bạn đang làm Frontend 2 của CafeChain.

BẮT BUỘC đọc đầy đủ, theo thứ tự:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Toàn bộ source của các file được liệt kê cho đợt `STAFF SHIFT VÀ SHIFT OPTIMIZATION`.

ĐÂY CHỈ LÀ PHASE A — AUDIT READ-ONLY. TUYỆT ĐỐI CHƯA SỬA CODE.

Phạm vi đợt này:
- `Areas/Admin/Views/AdminStaffShift/Index.cshtml`
- `Areas/Admin/Views/AdminShiftOptimization/Index.cshtml`
- `wwwroot/css/Admin/StaffShift/admin-staff-shift.css`
- `wwwroot/css/Admin/StaffShift/shift-optimization.css`

Hãy trả báo cáo theo đúng cấu trúc:

A. Scope confirmation
- Liệt kê đúng từng file được phép chỉnh.
- Liệt kê file liên quan đã đọc nhưng không được chỉnh.
- Xác nhận không sửa file ownership của frontend còn lại.

B. DOM và functional freeze
- Liệt kê form, table, modal, tab, collapse, partial, hidden input và dynamic region hiện có.
- Liệt kê `id`, `name`, `data-*`, `asp-*`, class/hook có khả năng được JS/Bootstrap/Select2/chart/map/drag-drop sử dụng.
- Nêu chính xác phần nào trong `.cshtml` chỉ được bổ sung class và phần nào phải để nguyên hoàn toàn.

C. Current UI audit
- Page header hiện tại.
- Button variants và kích thước hiện tại.
- Form control, validation, Select2 hiện tại.
- Table/list/pagination hiện tại.
- Modal/confirm/empty/loading/error hiện tại.
- Màu, token, spacing, radius, shadow và `!important` bị trùng hoặc lệch.

D. Target mapping
- Lập bảng `existing selector → visual contract --cc-* → file CSS sẽ chỉnh`.
- Chỉ ra selector nào dùng CSS chung và selector nào phải giữ exception nghiệp vụ.
- Mô tả cách làm Index/Create/Edit/Details/Delete/Modal đồng bộ mà không đổi DOM.

E. Risk register
- Specificity và thứ tự load CSS.
- Inline style và `<style>` cục bộ.
- Plugin control.
- Responsive table/modal.
- Nguy cơ selector lan sang StaffHub/POS/module frontend còn lại.

F. Test plan
- Trang và luồng cần test trước/sau.
- Viewport cần chụp.
- Functional regression bắt buộc.

Kết luận bằng một trong hai trạng thái:
- `READY FOR IMPLEMENTATION`: phạm vi rõ, không cần sửa JS/backend/DOM.
- `BLOCKED`: ghi đúng vấn đề cần nhóm trưởng giải quyết.

Sau báo cáo phải dừng. Không được chỉnh code hoặc tuyên bố đã hoàn thành giao diện.
```

Chỉ chuyển sang Prompt B khi báo cáo kết thúc bằng `READY FOR IMPLEMENTATION` và frontend đã đọc, đối chiếu đúng file/hook.

## Prompt B — Triển khai chính thức

```text
Bạn đang làm Frontend 2 của CafeChain.

BẮT BUỘC đọc lại:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Báo cáo audit Phase A vừa được duyệt.

PHASE B — CHỈ TRIỂN KHAI ĐỢT `STAFF SHIFT VÀ SHIFT OPTIMIZATION`.

CHỈ ĐƯỢC CHỈNH:
- `Areas/Admin/Views/AdminStaffShift/Index.cshtml`
- `Areas/Admin/Views/AdminShiftOptimization/Index.cshtml`
- `wwwroot/css/Admin/StaffShift/admin-staff-shift.css`
- `wwwroot/css/Admin/StaffShift/shift-optimization.css`

MỤC TIÊU NGHIỆP VỤ VÀ GIAO DIỆN:
- Lịch tuần ưu tiên tên nhân viên, ngày, ca, trạng thái và ca qua đêm.
- Optimization card/form/result cùng HR contract.
- Không để chữ tối trên nền tối hoặc nâu nhạt trên kem.

QUY CHUẨN THEO TRANG/FORM:
- Scheduler: sticky header nếu CSS cho phép, cột nhân viên đủ rộng, today/cancelled/+1 rõ.
- Modal create/edit/cancel shift cùng contract.
- Optimization: filter/input/result/recommendation rõ, không lấn lịch.

PHẦN PHẢI ĐÓNG BĂNG:
- Drag/drop/dropdown/modal IDs, schedule data attributes và scripts.
- Shift status, overnight logic, role/scope.

RÀNG BUỘC TUYỆT ĐỐI:
- Chỉ chỉnh `.cshtml` và `.css` nêu trên.
- Không chỉnh JavaScript, backend, database, SQL, migration, seed, permission hoặc nghiệp vụ.
- Không thêm/xóa/đổi tên/di chuyển cấu trúc thẻ `.cshtml`.
- Giữ nguyên `id`, `name`, `data-*`, `asp-*`, route, action, method, hidden input, validation, modal/tab/collapse và mọi hook.
- Không thêm framework/package/font/script.
- Không format hàng loạt.
- Không dùng selector global lan khỏi Admin.
- Không tạo design token cạnh tranh với `--cc-*`.
- Không dùng `!important` nếu selector đúng có thể giải quyết; exception phải có comment module và lý do.
- Không chỉnh Online, Voucher, Wheel, StaffHub hoặc POS riêng.

CÁCH TRIỂN KHAI BẮT BUỘC:
1. Giữ CSS module cho bố cục đặc thù; dùng token `--cc-*` để đồng bộ màu, type, spacing, radius và shadow.
2. Ưu tiên CSS selector tương thích với markup hiện tại; `.cshtml` chỉ bổ sung class visual khi CSS hiện tại không thể xử lý an toàn.
3. Đồng bộ đầy đủ normal/hover/active/focus/disabled/loading/error/empty state.
4. Đồng bộ Index/Create/Edit/Details/Delete/Modal trong phạm vi; không chỉ sửa trang Index.
5. Sửa responsive ngay trong đợt, không để dành toàn bộ đến cuối.
6. Sau mỗi nhóm selector, kiểm tra một trang đại diện để phát hiện conflict sớm.

KIỂM TRA BẮT BUỘC SAU KHI CODE:
- Xem tuần, đổi tuần/filter.
- Mở/create/edit/cancel modal.
- Drag/drop hoặc dropdown flow hiện có.
- Optimization input/result.
- Tablet/mobile scroll trong scheduler.

Trước khi kết luận, chạy và báo kết quả:
- `git diff --name-only`
- `git diff --stat`
- `git diff --check`
- `dotnet build`
- kiểm tra diff `.cshtml` để xác nhận không thay DOM/hook/binding.

OUTPUT CUỐI CÙNG PHẢI CÓ:
1. Danh sách chính xác file đã chỉnh và file chỉ đọc.
2. Tóm tắt thay đổi theo từng file.
3. Bảng selector cũ → contract mới.
4. Danh sách trạng thái UI đã hoàn thiện.
5. Danh sách viewport và luồng đã test, kèm kết quả đạt/chưa đạt.
6. Xác nhận không chỉnh file bị cấm, JavaScript, backend, database, seed, nghiệp vụ hoặc DOM.
7. CSS exception/`!important` còn lại và lý do.
8. Lỗi/rủi ro chưa xử lý; không được che giấu.
9. Kết luận chỉ được là `PASS — READY TO COMMIT` hoặc `FAIL — DO NOT COMMIT`.

Không được tuyên bố PASS nếu chưa kiểm tra toàn bộ mục trên.
```

## Prompt C — Kiểm tra độc lập sau khi làm

```text
Hãy thực hiện PHASE C — INDEPENDENT VERIFICATION cho đợt `STAFF SHIFT VÀ SHIFT OPTIMIZATION`. Không sửa thêm code trong bước này.

Đọc:
- hai file đặc tả chính trong `docs`;
- `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`;
- toàn bộ `git diff` hiện tại.

Kiểm tra độc lập:
1. File thay đổi có đúng ownership và đúng danh sách đợt không.
2. Có file `.js`, `.ts`, `.cs`, `.sql`, migration, seed hoặc backend nào thay đổi không.
3. `.cshtml` có thay cấu trúc thẻ, thứ tự DOM, `id`, `name`, `asp-*`, `data-*`, form action/method, hidden input hoặc hook không.
4. Index/Create/Edit/Details/Delete/Modal trong phạm vi đã dùng cùng visual contract chưa.
5. Button, input, table, modal, badge, page header và validation có cùng kích thước/semantics chưa.
6. Có selector global hoặc `!important` không cần thiết gây conflict không.
7. Desktop/tablet/mobile/zoom có overflow, chữ chìm, action bị che hoặc modal mất footer không.
8. Các chức năng sau còn hoạt động nguyên vẹn không:
- Xem tuần, đổi tuần/filter.
- Mở/create/edit/cancel modal.
- Drag/drop hoặc dropdown flow hiện có.
- Optimization input/result.
- Tablet/mobile scroll trong scheduler.
9. `dotnet build`, `git diff --check` có pass không.
10. So sánh ảnh trước/sau; chỉ ra trang nào còn lệch với module đã chuẩn hóa.

Trả kết quả dạng bảng: `Tiêu chí | PASS/FAIL | Bằng chứng | Việc cần sửa`.
Kết luận duy nhất:
- `VERIFIED PASS — MAY COMMIT AND PUSH`, hoặc
- `VERIFIED FAIL — MUST FIX BEFORE COMMIT`.
Không sửa code trong Phase C.
```

## Checklist thủ công của frontend

- Xem tuần, đổi tuần/filter.
- Mở/create/edit/cancel modal.
- Drag/drop hoặc dropdown flow hiện có.
- Optimization input/result.
- Tablet/mobile scroll trong scheduler.

Ngoài kiểm thử chức năng, bắt buộc chụp ít nhất:

- Ảnh trước và sau ở `1440×900`.
- Ảnh sau ở `1024×768`.
- Ảnh sau ở `390×844`.
- Một ảnh thể hiện form hoặc modal có validation.
- Một ảnh thể hiện table/list và action.

## Commit và push

Chỉ commit khi Prompt C trả `VERIFIED PASS — MAY COMMIT AND PUSH` và frontend đã tự kiểm tra.

```bash
git add "Areas/Admin/Views/AdminStaffShift/Index.cshtml"
git add "Areas/Admin/Views/AdminShiftOptimization/Index.cshtml"
git add "wwwroot/css/Admin/StaffShift/admin-staff-shift.css"
git add "wwwroot/css/Admin/StaffShift/shift-optimization.css"
git diff --cached --name-only
git diff --cached --stat
git diff --cached --check
git commit -m "style(admin-schedule): unify staff shift and optimization UI"
git push -u origin feature/admin-ui-fe2-staff-shift
```

Sau push: gửi nhóm trưởng branch, commit hash, ảnh trước/sau, checklist, build result và mọi exception còn lại. **Không tự merge.**


# ĐỢT 12 — STORE MANAGEMENT — INDEX, CREATE, EDIT VÀ MAP

## Điều kiện bắt đầu

Core FE1 đã merge.

- Nhóm trưởng đã xác nhận các dependency cần thiết đã merge vào `develop`.
- `git status --short` sạch.
- Branch phải được tạo mới từ `develop` vừa pull.

```bash
git checkout develop
git pull origin develop
git checkout -b feature/admin-ui-fe2-store
```

**Mức rủi ro:** Rất cao — Leaflet/map và inline style.

## File được phép chỉnh

- `Areas/Admin/Views/AdminStore/Create.cshtml`
- `Areas/Admin/Views/AdminStore/Edit.cshtml`
- `Areas/Admin/Views/AdminStore/Index.cshtml`
- `wwwroot/css/Admin/Store/store-admin.css`

Cấm chỉnh file ngoài danh sách này. File liên quan khác chỉ được đọc để hiểu hook và visual context.

## Kết quả giao diện phải đạt

- Store CRUD cùng form/list contract với Admin.
- Địa chỉ và map đủ rộng, rõ, không phá Leaflet.
- Index/Create/Edit đồng bộ button, validation và status.

## Quy chuẩn chi tiết theo loại trang

- Index: header/filter/table/status/action.
- Create/Edit: sections thông tin, địa chỉ, map và footer.
- Map panel có border/radius, không bị height 0 hoặc overflow.

## Phần phải bảo toàn tuyệt đối

- Leaflet container ID, map scripts, coordinate/address binding.
- Store routes, validation, status và permission.

## Prompt A — Audit read-only

Sao chép nguyên khối:

```text
Bạn đang làm Frontend 2 của CafeChain.

BẮT BUỘC đọc đầy đủ, theo thứ tự:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Toàn bộ source của các file được liệt kê cho đợt `STORE MANAGEMENT — INDEX, CREATE, EDIT VÀ MAP`.

ĐÂY CHỈ LÀ PHASE A — AUDIT READ-ONLY. TUYỆT ĐỐI CHƯA SỬA CODE.

Phạm vi đợt này:
- `Areas/Admin/Views/AdminStore/Create.cshtml`
- `Areas/Admin/Views/AdminStore/Edit.cshtml`
- `Areas/Admin/Views/AdminStore/Index.cshtml`
- `wwwroot/css/Admin/Store/store-admin.css`

Hãy trả báo cáo theo đúng cấu trúc:

A. Scope confirmation
- Liệt kê đúng từng file được phép chỉnh.
- Liệt kê file liên quan đã đọc nhưng không được chỉnh.
- Xác nhận không sửa file ownership của frontend còn lại.

B. DOM và functional freeze
- Liệt kê form, table, modal, tab, collapse, partial, hidden input và dynamic region hiện có.
- Liệt kê `id`, `name`, `data-*`, `asp-*`, class/hook có khả năng được JS/Bootstrap/Select2/chart/map/drag-drop sử dụng.
- Nêu chính xác phần nào trong `.cshtml` chỉ được bổ sung class và phần nào phải để nguyên hoàn toàn.

C. Current UI audit
- Page header hiện tại.
- Button variants và kích thước hiện tại.
- Form control, validation, Select2 hiện tại.
- Table/list/pagination hiện tại.
- Modal/confirm/empty/loading/error hiện tại.
- Màu, token, spacing, radius, shadow và `!important` bị trùng hoặc lệch.

D. Target mapping
- Lập bảng `existing selector → visual contract --cc-* → file CSS sẽ chỉnh`.
- Chỉ ra selector nào dùng CSS chung và selector nào phải giữ exception nghiệp vụ.
- Mô tả cách làm Index/Create/Edit/Details/Delete/Modal đồng bộ mà không đổi DOM.

E. Risk register
- Specificity và thứ tự load CSS.
- Inline style và `<style>` cục bộ.
- Plugin control.
- Responsive table/modal.
- Nguy cơ selector lan sang StaffHub/POS/module frontend còn lại.

F. Test plan
- Trang và luồng cần test trước/sau.
- Viewport cần chụp.
- Functional regression bắt buộc.

Kết luận bằng một trong hai trạng thái:
- `READY FOR IMPLEMENTATION`: phạm vi rõ, không cần sửa JS/backend/DOM.
- `BLOCKED`: ghi đúng vấn đề cần nhóm trưởng giải quyết.

Sau báo cáo phải dừng. Không được chỉnh code hoặc tuyên bố đã hoàn thành giao diện.
```

Chỉ chuyển sang Prompt B khi báo cáo kết thúc bằng `READY FOR IMPLEMENTATION` và frontend đã đọc, đối chiếu đúng file/hook.

## Prompt B — Triển khai chính thức

```text
Bạn đang làm Frontend 2 của CafeChain.

BẮT BUỘC đọc lại:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Báo cáo audit Phase A vừa được duyệt.

PHASE B — CHỈ TRIỂN KHAI ĐỢT `STORE MANAGEMENT — INDEX, CREATE, EDIT VÀ MAP`.

CHỈ ĐƯỢC CHỈNH:
- `Areas/Admin/Views/AdminStore/Create.cshtml`
- `Areas/Admin/Views/AdminStore/Edit.cshtml`
- `Areas/Admin/Views/AdminStore/Index.cshtml`
- `wwwroot/css/Admin/Store/store-admin.css`

MỤC TIÊU NGHIỆP VỤ VÀ GIAO DIỆN:
- Store CRUD cùng form/list contract với Admin.
- Địa chỉ và map đủ rộng, rõ, không phá Leaflet.
- Index/Create/Edit đồng bộ button, validation và status.

QUY CHUẨN THEO TRANG/FORM:
- Index: header/filter/table/status/action.
- Create/Edit: sections thông tin, địa chỉ, map và footer.
- Map panel có border/radius, không bị height 0 hoặc overflow.

PHẦN PHẢI ĐÓNG BĂNG:
- Leaflet container ID, map scripts, coordinate/address binding.
- Store routes, validation, status và permission.

RÀNG BUỘC TUYỆT ĐỐI:
- Chỉ chỉnh `.cshtml` và `.css` nêu trên.
- Không chỉnh JavaScript, backend, database, SQL, migration, seed, permission hoặc nghiệp vụ.
- Không thêm/xóa/đổi tên/di chuyển cấu trúc thẻ `.cshtml`.
- Giữ nguyên `id`, `name`, `data-*`, `asp-*`, route, action, method, hidden input, validation, modal/tab/collapse và mọi hook.
- Không thêm framework/package/font/script.
- Không format hàng loạt.
- Không dùng selector global lan khỏi Admin.
- Không tạo design token cạnh tranh với `--cc-*`.
- Không dùng `!important` nếu selector đúng có thể giải quyết; exception phải có comment module và lý do.
- Không chỉnh Online, Voucher, Wheel, StaffHub hoặc POS riêng.

CÁCH TRIỂN KHAI BẮT BUỘC:
1. Giữ CSS module cho bố cục đặc thù; dùng token `--cc-*` để đồng bộ màu, type, spacing, radius và shadow.
2. Ưu tiên CSS selector tương thích với markup hiện tại; `.cshtml` chỉ bổ sung class visual khi CSS hiện tại không thể xử lý an toàn.
3. Đồng bộ đầy đủ normal/hover/active/focus/disabled/loading/error/empty state.
4. Đồng bộ Index/Create/Edit/Details/Delete/Modal trong phạm vi; không chỉ sửa trang Index.
5. Sửa responsive ngay trong đợt, không để dành toàn bộ đến cuối.
6. Sau mỗi nhóm selector, kiểm tra một trang đại diện để phát hiện conflict sớm.

KIỂM TRA BẮT BUỘC SAU KHI CODE:
- Index/action.
- Create/Edit validation và map load/select.
- Responsive form/map.
- Không có console/map layout regression do CSS.

Trước khi kết luận, chạy và báo kết quả:
- `git diff --name-only`
- `git diff --stat`
- `git diff --check`
- `dotnet build`
- kiểm tra diff `.cshtml` để xác nhận không thay DOM/hook/binding.

OUTPUT CUỐI CÙNG PHẢI CÓ:
1. Danh sách chính xác file đã chỉnh và file chỉ đọc.
2. Tóm tắt thay đổi theo từng file.
3. Bảng selector cũ → contract mới.
4. Danh sách trạng thái UI đã hoàn thiện.
5. Danh sách viewport và luồng đã test, kèm kết quả đạt/chưa đạt.
6. Xác nhận không chỉnh file bị cấm, JavaScript, backend, database, seed, nghiệp vụ hoặc DOM.
7. CSS exception/`!important` còn lại và lý do.
8. Lỗi/rủi ro chưa xử lý; không được che giấu.
9. Kết luận chỉ được là `PASS — READY TO COMMIT` hoặc `FAIL — DO NOT COMMIT`.

Không được tuyên bố PASS nếu chưa kiểm tra toàn bộ mục trên.
```

## Prompt C — Kiểm tra độc lập sau khi làm

```text
Hãy thực hiện PHASE C — INDEPENDENT VERIFICATION cho đợt `STORE MANAGEMENT — INDEX, CREATE, EDIT VÀ MAP`. Không sửa thêm code trong bước này.

Đọc:
- hai file đặc tả chính trong `docs`;
- `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`;
- toàn bộ `git diff` hiện tại.

Kiểm tra độc lập:
1. File thay đổi có đúng ownership và đúng danh sách đợt không.
2. Có file `.js`, `.ts`, `.cs`, `.sql`, migration, seed hoặc backend nào thay đổi không.
3. `.cshtml` có thay cấu trúc thẻ, thứ tự DOM, `id`, `name`, `asp-*`, `data-*`, form action/method, hidden input hoặc hook không.
4. Index/Create/Edit/Details/Delete/Modal trong phạm vi đã dùng cùng visual contract chưa.
5. Button, input, table, modal, badge, page header và validation có cùng kích thước/semantics chưa.
6. Có selector global hoặc `!important` không cần thiết gây conflict không.
7. Desktop/tablet/mobile/zoom có overflow, chữ chìm, action bị che hoặc modal mất footer không.
8. Các chức năng sau còn hoạt động nguyên vẹn không:
- Index/action.
- Create/Edit validation và map load/select.
- Responsive form/map.
- Không có console/map layout regression do CSS.
9. `dotnet build`, `git diff --check` có pass không.
10. So sánh ảnh trước/sau; chỉ ra trang nào còn lệch với module đã chuẩn hóa.

Trả kết quả dạng bảng: `Tiêu chí | PASS/FAIL | Bằng chứng | Việc cần sửa`.
Kết luận duy nhất:
- `VERIFIED PASS — MAY COMMIT AND PUSH`, hoặc
- `VERIFIED FAIL — MUST FIX BEFORE COMMIT`.
Không sửa code trong Phase C.
```

## Checklist thủ công của frontend

- Index/action.
- Create/Edit validation và map load/select.
- Responsive form/map.
- Không có console/map layout regression do CSS.

Ngoài kiểm thử chức năng, bắt buộc chụp ít nhất:

- Ảnh trước và sau ở `1440×900`.
- Ảnh sau ở `1024×768`.
- Ảnh sau ở `390×844`.
- Một ảnh thể hiện form hoặc modal có validation.
- Một ảnh thể hiện table/list và action.

## Commit và push

Chỉ commit khi Prompt C trả `VERIFIED PASS — MAY COMMIT AND PUSH` và frontend đã tự kiểm tra.

```bash
git add "Areas/Admin/Views/AdminStore/Create.cshtml"
git add "Areas/Admin/Views/AdminStore/Edit.cshtml"
git add "Areas/Admin/Views/AdminStore/Index.cshtml"
git add "wwwroot/css/Admin/Store/store-admin.css"
git diff --cached --name-only
git diff --cached --stat
git diff --cached --check
git commit -m "style(admin-store): unify store management UI"
git push -u origin feature/admin-ui-fe2-store
```

Sau push: gửi nhóm trưởng branch, commit hash, ảnh trước/sau, checklist, build result và mọi exception còn lại. **Không tự merge.**


# ĐỢT 13 — SUPPLIER VÀ SUPPLIER QUALITY

## Điều kiện bắt đầu

Core FE1 đã merge; procurement contract đã ổn định.

- Nhóm trưởng đã xác nhận các dependency cần thiết đã merge vào `develop`.
- `git status --short` sạch.
- Branch phải được tạo mới từ `develop` vừa pull.

```bash
git checkout develop
git pull origin develop
git checkout -b feature/admin-ui-fe2-supplier
```

**Mức rủi ro:** Cao — nhiều form/tab/drawer/modal.

## File được phép chỉnh

- `Areas/Admin/Views/AdminSupplier/Index.cshtml`
- `Areas/Admin/Views/AdminSupplierQuality/Create.cshtml`
- `Areas/Admin/Views/AdminSupplierQuality/Index.cshtml`
- `wwwroot/css/Admin/Supplier/supplier.css`

Cấm chỉnh file ngoài danh sách này. File liên quan khác chỉ được đọc để hiểu hook và visual context.

## Kết quả giao diện phải đạt

- Supplier Index/detail tabs/forms và SupplierQuality cùng contract.
- Thông tin liên hệ, trạng thái, chất lượng và action rõ.
- Drawer/modal/tab giữ cơ chế nhưng đồng bộ visual.

## Quy chuẩn chi tiết theo loại trang

- Supplier Index: filter/table/status/action + detail area/tab/modal hiện có.
- Quality Index/Create: form/table/rating/status/validation.
- Danger chỉ delete/deactivate có hậu quả.

## Phần phải bảo toàn tuyệt đối

- Supplier modal/drawer/tab IDs, forms và scripts.
- Quality score/status/value và binding.

## Prompt A — Audit read-only

Sao chép nguyên khối:

```text
Bạn đang làm Frontend 2 của CafeChain.

BẮT BUỘC đọc đầy đủ, theo thứ tự:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Toàn bộ source của các file được liệt kê cho đợt `SUPPLIER VÀ SUPPLIER QUALITY`.

ĐÂY CHỈ LÀ PHASE A — AUDIT READ-ONLY. TUYỆT ĐỐI CHƯA SỬA CODE.

Phạm vi đợt này:
- `Areas/Admin/Views/AdminSupplier/Index.cshtml`
- `Areas/Admin/Views/AdminSupplierQuality/Create.cshtml`
- `Areas/Admin/Views/AdminSupplierQuality/Index.cshtml`
- `wwwroot/css/Admin/Supplier/supplier.css`

Hãy trả báo cáo theo đúng cấu trúc:

A. Scope confirmation
- Liệt kê đúng từng file được phép chỉnh.
- Liệt kê file liên quan đã đọc nhưng không được chỉnh.
- Xác nhận không sửa file ownership của frontend còn lại.

B. DOM và functional freeze
- Liệt kê form, table, modal, tab, collapse, partial, hidden input và dynamic region hiện có.
- Liệt kê `id`, `name`, `data-*`, `asp-*`, class/hook có khả năng được JS/Bootstrap/Select2/chart/map/drag-drop sử dụng.
- Nêu chính xác phần nào trong `.cshtml` chỉ được bổ sung class và phần nào phải để nguyên hoàn toàn.

C. Current UI audit
- Page header hiện tại.
- Button variants và kích thước hiện tại.
- Form control, validation, Select2 hiện tại.
- Table/list/pagination hiện tại.
- Modal/confirm/empty/loading/error hiện tại.
- Màu, token, spacing, radius, shadow và `!important` bị trùng hoặc lệch.

D. Target mapping
- Lập bảng `existing selector → visual contract --cc-* → file CSS sẽ chỉnh`.
- Chỉ ra selector nào dùng CSS chung và selector nào phải giữ exception nghiệp vụ.
- Mô tả cách làm Index/Create/Edit/Details/Delete/Modal đồng bộ mà không đổi DOM.

E. Risk register
- Specificity và thứ tự load CSS.
- Inline style và `<style>` cục bộ.
- Plugin control.
- Responsive table/modal.
- Nguy cơ selector lan sang StaffHub/POS/module frontend còn lại.

F. Test plan
- Trang và luồng cần test trước/sau.
- Viewport cần chụp.
- Functional regression bắt buộc.

Kết luận bằng một trong hai trạng thái:
- `READY FOR IMPLEMENTATION`: phạm vi rõ, không cần sửa JS/backend/DOM.
- `BLOCKED`: ghi đúng vấn đề cần nhóm trưởng giải quyết.

Sau báo cáo phải dừng. Không được chỉnh code hoặc tuyên bố đã hoàn thành giao diện.
```

Chỉ chuyển sang Prompt B khi báo cáo kết thúc bằng `READY FOR IMPLEMENTATION` và frontend đã đọc, đối chiếu đúng file/hook.

## Prompt B — Triển khai chính thức

```text
Bạn đang làm Frontend 2 của CafeChain.

BẮT BUỘC đọc lại:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Báo cáo audit Phase A vừa được duyệt.

PHASE B — CHỈ TRIỂN KHAI ĐỢT `SUPPLIER VÀ SUPPLIER QUALITY`.

CHỈ ĐƯỢC CHỈNH:
- `Areas/Admin/Views/AdminSupplier/Index.cshtml`
- `Areas/Admin/Views/AdminSupplierQuality/Create.cshtml`
- `Areas/Admin/Views/AdminSupplierQuality/Index.cshtml`
- `wwwroot/css/Admin/Supplier/supplier.css`

MỤC TIÊU NGHIỆP VỤ VÀ GIAO DIỆN:
- Supplier Index/detail tabs/forms và SupplierQuality cùng contract.
- Thông tin liên hệ, trạng thái, chất lượng và action rõ.
- Drawer/modal/tab giữ cơ chế nhưng đồng bộ visual.

QUY CHUẨN THEO TRANG/FORM:
- Supplier Index: filter/table/status/action + detail area/tab/modal hiện có.
- Quality Index/Create: form/table/rating/status/validation.
- Danger chỉ delete/deactivate có hậu quả.

PHẦN PHẢI ĐÓNG BĂNG:
- Supplier modal/drawer/tab IDs, forms và scripts.
- Quality score/status/value và binding.

RÀNG BUỘC TUYỆT ĐỐI:
- Chỉ chỉnh `.cshtml` và `.css` nêu trên.
- Không chỉnh JavaScript, backend, database, SQL, migration, seed, permission hoặc nghiệp vụ.
- Không thêm/xóa/đổi tên/di chuyển cấu trúc thẻ `.cshtml`.
- Giữ nguyên `id`, `name`, `data-*`, `asp-*`, route, action, method, hidden input, validation, modal/tab/collapse và mọi hook.
- Không thêm framework/package/font/script.
- Không format hàng loạt.
- Không dùng selector global lan khỏi Admin.
- Không tạo design token cạnh tranh với `--cc-*`.
- Không dùng `!important` nếu selector đúng có thể giải quyết; exception phải có comment module và lý do.
- Không chỉnh Online, Voucher, Wheel, StaffHub hoặc POS riêng.

CÁCH TRIỂN KHAI BẮT BUỘC:
1. Giữ CSS module cho bố cục đặc thù; dùng token `--cc-*` để đồng bộ màu, type, spacing, radius và shadow.
2. Ưu tiên CSS selector tương thích với markup hiện tại; `.cshtml` chỉ bổ sung class visual khi CSS hiện tại không thể xử lý an toàn.
3. Đồng bộ đầy đủ normal/hover/active/focus/disabled/loading/error/empty state.
4. Đồng bộ Index/Create/Edit/Details/Delete/Modal trong phạm vi; không chỉ sửa trang Index.
5. Sửa responsive ngay trong đợt, không để dành toàn bộ đến cuối.
6. Sau mỗi nhóm selector, kiểm tra một trang đại diện để phát hiện conflict sớm.

KIỂM TRA BẮT BUỘC SAU KHI CODE:
- Supplier create/edit/detail/tab/delete/toggle theo chức năng có sẵn.
- SupplierQuality create/index/validation.
- Responsive drawer/modal/table.

Trước khi kết luận, chạy và báo kết quả:
- `git diff --name-only`
- `git diff --stat`
- `git diff --check`
- `dotnet build`
- kiểm tra diff `.cshtml` để xác nhận không thay DOM/hook/binding.

OUTPUT CUỐI CÙNG PHẢI CÓ:
1. Danh sách chính xác file đã chỉnh và file chỉ đọc.
2. Tóm tắt thay đổi theo từng file.
3. Bảng selector cũ → contract mới.
4. Danh sách trạng thái UI đã hoàn thiện.
5. Danh sách viewport và luồng đã test, kèm kết quả đạt/chưa đạt.
6. Xác nhận không chỉnh file bị cấm, JavaScript, backend, database, seed, nghiệp vụ hoặc DOM.
7. CSS exception/`!important` còn lại và lý do.
8. Lỗi/rủi ro chưa xử lý; không được che giấu.
9. Kết luận chỉ được là `PASS — READY TO COMMIT` hoặc `FAIL — DO NOT COMMIT`.

Không được tuyên bố PASS nếu chưa kiểm tra toàn bộ mục trên.
```

## Prompt C — Kiểm tra độc lập sau khi làm

```text
Hãy thực hiện PHASE C — INDEPENDENT VERIFICATION cho đợt `SUPPLIER VÀ SUPPLIER QUALITY`. Không sửa thêm code trong bước này.

Đọc:
- hai file đặc tả chính trong `docs`;
- `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`;
- toàn bộ `git diff` hiện tại.

Kiểm tra độc lập:
1. File thay đổi có đúng ownership và đúng danh sách đợt không.
2. Có file `.js`, `.ts`, `.cs`, `.sql`, migration, seed hoặc backend nào thay đổi không.
3. `.cshtml` có thay cấu trúc thẻ, thứ tự DOM, `id`, `name`, `asp-*`, `data-*`, form action/method, hidden input hoặc hook không.
4. Index/Create/Edit/Details/Delete/Modal trong phạm vi đã dùng cùng visual contract chưa.
5. Button, input, table, modal, badge, page header và validation có cùng kích thước/semantics chưa.
6. Có selector global hoặc `!important` không cần thiết gây conflict không.
7. Desktop/tablet/mobile/zoom có overflow, chữ chìm, action bị che hoặc modal mất footer không.
8. Các chức năng sau còn hoạt động nguyên vẹn không:
- Supplier create/edit/detail/tab/delete/toggle theo chức năng có sẵn.
- SupplierQuality create/index/validation.
- Responsive drawer/modal/table.
9. `dotnet build`, `git diff --check` có pass không.
10. So sánh ảnh trước/sau; chỉ ra trang nào còn lệch với module đã chuẩn hóa.

Trả kết quả dạng bảng: `Tiêu chí | PASS/FAIL | Bằng chứng | Việc cần sửa`.
Kết luận duy nhất:
- `VERIFIED PASS — MAY COMMIT AND PUSH`, hoặc
- `VERIFIED FAIL — MUST FIX BEFORE COMMIT`.
Không sửa code trong Phase C.
```

## Checklist thủ công của frontend

- Supplier create/edit/detail/tab/delete/toggle theo chức năng có sẵn.
- SupplierQuality create/index/validation.
- Responsive drawer/modal/table.

Ngoài kiểm thử chức năng, bắt buộc chụp ít nhất:

- Ảnh trước và sau ở `1440×900`.
- Ảnh sau ở `1024×768`.
- Ảnh sau ở `390×844`.
- Một ảnh thể hiện form hoặc modal có validation.
- Một ảnh thể hiện table/list và action.

## Commit và push

Chỉ commit khi Prompt C trả `VERIFIED PASS — MAY COMMIT AND PUSH` và frontend đã tự kiểm tra.

```bash
git add "Areas/Admin/Views/AdminSupplier/Index.cshtml"
git add "Areas/Admin/Views/AdminSupplierQuality/Create.cshtml"
git add "Areas/Admin/Views/AdminSupplierQuality/Index.cshtml"
git add "wwwroot/css/Admin/Supplier/supplier.css"
git diff --cached --name-only
git diff --cached --stat
git diff --cached --check
git commit -m "style(admin-supplier): unify supplier and quality UI"
git push -u origin feature/admin-ui-fe2-supplier
```

Sau push: gửi nhóm trưởng branch, commit hash, ảnh trước/sau, checklist, build result và mọi exception còn lại. **Không tự merge.**


# ĐỢT 14 — PROFILE, NOTIFICATIONS VÀ SETTINGS

## Điều kiện bắt đầu

Core FE1 đã merge.

- Nhóm trưởng đã xác nhận các dependency cần thiết đã merge vào `develop`.
- `git status --short` sạch.
- Branch phải được tạo mới từ `develop` vừa pull.

```bash
git checkout develop
git pull origin develop
git checkout -b feature/admin-ui-fe2-system
```

**Mức rủi ro:** Trung bình — modal/inline style và settings form.

## File được phép chỉnh

- `Areas/Admin/Views/AdminProfile/MyProfile.cshtml`
- `Areas/Admin/Views/AdminNotifications/Index.cshtml`
- `Areas/Admin/Views/AdminSetting/Index.cshtml`
- `Areas/Admin/Views/AdminSetting/Partials/_NegativeInventorySettings.cshtml`
- `wwwroot/css/Admin/Profile/admin-profile.css`
- `wwwroot/css/Admin/Notifications/admin-notifications.css`
- `wwwroot/css/Admin/Settings/negative-inventory.css`

Cấm chỉnh file ngoài danh sách này. File liên quan khác chỉ được đọc để hiểu hook và visual context.

## Kết quả giao diện phải đạt

- System pages gọn, rõ và cùng Admin contract.
- Profile không có quá nhiều card cạnh tranh; notification unread rõ; settings tập trung.
- Form/modal/validation đồng bộ Core.

## Quy chuẩn chi tiết theo loại trang

- Profile: identity summary, details, avatar/modal nếu có.
- Notifications: unread/read, severity, action và empty state.
- Settings: section card, control/validation/save action; partial không double padding.

## Phần phải bảo toàn tuyệt đối

- Profile modal/hook, notification read actions, settings binding.
- Không sửa auth/account logic.

## Prompt A — Audit read-only

Sao chép nguyên khối:

```text
Bạn đang làm Frontend 2 của CafeChain.

BẮT BUỘC đọc đầy đủ, theo thứ tự:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Toàn bộ source của các file được liệt kê cho đợt `PROFILE, NOTIFICATIONS VÀ SETTINGS`.

ĐÂY CHỈ LÀ PHASE A — AUDIT READ-ONLY. TUYỆT ĐỐI CHƯA SỬA CODE.

Phạm vi đợt này:
- `Areas/Admin/Views/AdminProfile/MyProfile.cshtml`
- `Areas/Admin/Views/AdminNotifications/Index.cshtml`
- `Areas/Admin/Views/AdminSetting/Index.cshtml`
- `Areas/Admin/Views/AdminSetting/Partials/_NegativeInventorySettings.cshtml`
- `wwwroot/css/Admin/Profile/admin-profile.css`
- `wwwroot/css/Admin/Notifications/admin-notifications.css`
- `wwwroot/css/Admin/Settings/negative-inventory.css`

Hãy trả báo cáo theo đúng cấu trúc:

A. Scope confirmation
- Liệt kê đúng từng file được phép chỉnh.
- Liệt kê file liên quan đã đọc nhưng không được chỉnh.
- Xác nhận không sửa file ownership của frontend còn lại.

B. DOM và functional freeze
- Liệt kê form, table, modal, tab, collapse, partial, hidden input và dynamic region hiện có.
- Liệt kê `id`, `name`, `data-*`, `asp-*`, class/hook có khả năng được JS/Bootstrap/Select2/chart/map/drag-drop sử dụng.
- Nêu chính xác phần nào trong `.cshtml` chỉ được bổ sung class và phần nào phải để nguyên hoàn toàn.

C. Current UI audit
- Page header hiện tại.
- Button variants và kích thước hiện tại.
- Form control, validation, Select2 hiện tại.
- Table/list/pagination hiện tại.
- Modal/confirm/empty/loading/error hiện tại.
- Màu, token, spacing, radius, shadow và `!important` bị trùng hoặc lệch.

D. Target mapping
- Lập bảng `existing selector → visual contract --cc-* → file CSS sẽ chỉnh`.
- Chỉ ra selector nào dùng CSS chung và selector nào phải giữ exception nghiệp vụ.
- Mô tả cách làm Index/Create/Edit/Details/Delete/Modal đồng bộ mà không đổi DOM.

E. Risk register
- Specificity và thứ tự load CSS.
- Inline style và `<style>` cục bộ.
- Plugin control.
- Responsive table/modal.
- Nguy cơ selector lan sang StaffHub/POS/module frontend còn lại.

F. Test plan
- Trang và luồng cần test trước/sau.
- Viewport cần chụp.
- Functional regression bắt buộc.

Kết luận bằng một trong hai trạng thái:
- `READY FOR IMPLEMENTATION`: phạm vi rõ, không cần sửa JS/backend/DOM.
- `BLOCKED`: ghi đúng vấn đề cần nhóm trưởng giải quyết.

Sau báo cáo phải dừng. Không được chỉnh code hoặc tuyên bố đã hoàn thành giao diện.
```

Chỉ chuyển sang Prompt B khi báo cáo kết thúc bằng `READY FOR IMPLEMENTATION` và frontend đã đọc, đối chiếu đúng file/hook.

## Prompt B — Triển khai chính thức

```text
Bạn đang làm Frontend 2 của CafeChain.

BẮT BUỘC đọc lại:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Báo cáo audit Phase A vừa được duyệt.

PHASE B — CHỈ TRIỂN KHAI ĐỢT `PROFILE, NOTIFICATIONS VÀ SETTINGS`.

CHỈ ĐƯỢC CHỈNH:
- `Areas/Admin/Views/AdminProfile/MyProfile.cshtml`
- `Areas/Admin/Views/AdminNotifications/Index.cshtml`
- `Areas/Admin/Views/AdminSetting/Index.cshtml`
- `Areas/Admin/Views/AdminSetting/Partials/_NegativeInventorySettings.cshtml`
- `wwwroot/css/Admin/Profile/admin-profile.css`
- `wwwroot/css/Admin/Notifications/admin-notifications.css`
- `wwwroot/css/Admin/Settings/negative-inventory.css`

MỤC TIÊU NGHIỆP VỤ VÀ GIAO DIỆN:
- System pages gọn, rõ và cùng Admin contract.
- Profile không có quá nhiều card cạnh tranh; notification unread rõ; settings tập trung.
- Form/modal/validation đồng bộ Core.

QUY CHUẨN THEO TRANG/FORM:
- Profile: identity summary, details, avatar/modal nếu có.
- Notifications: unread/read, severity, action và empty state.
- Settings: section card, control/validation/save action; partial không double padding.

PHẦN PHẢI ĐÓNG BĂNG:
- Profile modal/hook, notification read actions, settings binding.
- Không sửa auth/account logic.

RÀNG BUỘC TUYỆT ĐỐI:
- Chỉ chỉnh `.cshtml` và `.css` nêu trên.
- Không chỉnh JavaScript, backend, database, SQL, migration, seed, permission hoặc nghiệp vụ.
- Không thêm/xóa/đổi tên/di chuyển cấu trúc thẻ `.cshtml`.
- Giữ nguyên `id`, `name`, `data-*`, `asp-*`, route, action, method, hidden input, validation, modal/tab/collapse và mọi hook.
- Không thêm framework/package/font/script.
- Không format hàng loạt.
- Không dùng selector global lan khỏi Admin.
- Không tạo design token cạnh tranh với `--cc-*`.
- Không dùng `!important` nếu selector đúng có thể giải quyết; exception phải có comment module và lý do.
- Không chỉnh Online, Voucher, Wheel, StaffHub hoặc POS riêng.

CÁCH TRIỂN KHAI BẮT BUỘC:
1. Giữ CSS module cho bố cục đặc thù; dùng token `--cc-*` để đồng bộ màu, type, spacing, radius và shadow.
2. Ưu tiên CSS selector tương thích với markup hiện tại; `.cshtml` chỉ bổ sung class visual khi CSS hiện tại không thể xử lý an toàn.
3. Đồng bộ đầy đủ normal/hover/active/focus/disabled/loading/error/empty state.
4. Đồng bộ Index/Create/Edit/Details/Delete/Modal trong phạm vi; không chỉ sửa trang Index.
5. Sửa responsive ngay trong đợt, không để dành toàn bộ đến cuối.
6. Sau mỗi nhóm selector, kiểm tra một trang đại diện để phát hiện conflict sớm.

KIỂM TRA BẮT BUỘC SAU KHI CODE:
- Profile view/modal/action.
- Notification read/unread/action/empty.
- Settings save/validation.
- Responsive card/form/list.

Trước khi kết luận, chạy và báo kết quả:
- `git diff --name-only`
- `git diff --stat`
- `git diff --check`
- `dotnet build`
- kiểm tra diff `.cshtml` để xác nhận không thay DOM/hook/binding.

OUTPUT CUỐI CÙNG PHẢI CÓ:
1. Danh sách chính xác file đã chỉnh và file chỉ đọc.
2. Tóm tắt thay đổi theo từng file.
3. Bảng selector cũ → contract mới.
4. Danh sách trạng thái UI đã hoàn thiện.
5. Danh sách viewport và luồng đã test, kèm kết quả đạt/chưa đạt.
6. Xác nhận không chỉnh file bị cấm, JavaScript, backend, database, seed, nghiệp vụ hoặc DOM.
7. CSS exception/`!important` còn lại và lý do.
8. Lỗi/rủi ro chưa xử lý; không được che giấu.
9. Kết luận chỉ được là `PASS — READY TO COMMIT` hoặc `FAIL — DO NOT COMMIT`.

Không được tuyên bố PASS nếu chưa kiểm tra toàn bộ mục trên.
```

## Prompt C — Kiểm tra độc lập sau khi làm

```text
Hãy thực hiện PHASE C — INDEPENDENT VERIFICATION cho đợt `PROFILE, NOTIFICATIONS VÀ SETTINGS`. Không sửa thêm code trong bước này.

Đọc:
- hai file đặc tả chính trong `docs`;
- `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`;
- toàn bộ `git diff` hiện tại.

Kiểm tra độc lập:
1. File thay đổi có đúng ownership và đúng danh sách đợt không.
2. Có file `.js`, `.ts`, `.cs`, `.sql`, migration, seed hoặc backend nào thay đổi không.
3. `.cshtml` có thay cấu trúc thẻ, thứ tự DOM, `id`, `name`, `asp-*`, `data-*`, form action/method, hidden input hoặc hook không.
4. Index/Create/Edit/Details/Delete/Modal trong phạm vi đã dùng cùng visual contract chưa.
5. Button, input, table, modal, badge, page header và validation có cùng kích thước/semantics chưa.
6. Có selector global hoặc `!important` không cần thiết gây conflict không.
7. Desktop/tablet/mobile/zoom có overflow, chữ chìm, action bị che hoặc modal mất footer không.
8. Các chức năng sau còn hoạt động nguyên vẹn không:
- Profile view/modal/action.
- Notification read/unread/action/empty.
- Settings save/validation.
- Responsive card/form/list.
9. `dotnet build`, `git diff --check` có pass không.
10. So sánh ảnh trước/sau; chỉ ra trang nào còn lệch với module đã chuẩn hóa.

Trả kết quả dạng bảng: `Tiêu chí | PASS/FAIL | Bằng chứng | Việc cần sửa`.
Kết luận duy nhất:
- `VERIFIED PASS — MAY COMMIT AND PUSH`, hoặc
- `VERIFIED FAIL — MUST FIX BEFORE COMMIT`.
Không sửa code trong Phase C.
```

## Checklist thủ công của frontend

- Profile view/modal/action.
- Notification read/unread/action/empty.
- Settings save/validation.
- Responsive card/form/list.

Ngoài kiểm thử chức năng, bắt buộc chụp ít nhất:

- Ảnh trước và sau ở `1440×900`.
- Ảnh sau ở `1024×768`.
- Ảnh sau ở `390×844`.
- Một ảnh thể hiện form hoặc modal có validation.
- Một ảnh thể hiện table/list và action.

## Commit và push

Chỉ commit khi Prompt C trả `VERIFIED PASS — MAY COMMIT AND PUSH` và frontend đã tự kiểm tra.

```bash
git add "Areas/Admin/Views/AdminProfile/MyProfile.cshtml"
git add "Areas/Admin/Views/AdminNotifications/Index.cshtml"
git add "Areas/Admin/Views/AdminSetting/Index.cshtml"
git add "Areas/Admin/Views/AdminSetting/Partials/_NegativeInventorySettings.cshtml"
git add "wwwroot/css/Admin/Profile/admin-profile.css"
git add "wwwroot/css/Admin/Notifications/admin-notifications.css"
git add "wwwroot/css/Admin/Settings/negative-inventory.css"
git diff --cached --name-only
git diff --cached --stat
git diff --cached --check
git commit -m "style(admin-system): unify profile notifications and settings UI"
git push -u origin feature/admin-ui-fe2-system
```

Sau push: gửi nhóm trưởng branch, commit hash, ảnh trước/sau, checklist, build result và mọi exception còn lại. **Không tự merge.**


# ĐỢT 15 — FINAL REGRESSION FRONTEND 2 VÀ ĐỐI CHIẾU TOÀN ADMIN

## Điều kiện bắt đầu

Nhóm trưởng đã merge toàn bộ đợt FE1 và FE2 cần đối chiếu vào `develop`.

- Nhóm trưởng đã xác nhận các dependency cần thiết đã merge vào `develop`.
- `git status --short` sạch.
- Branch phải được tạo mới từ `develop` vừa pull.

```bash
git checkout develop
git pull origin develop
git checkout -b fix/admin-ui-fe2-final-regression
```

**Mức rủi ro:** Cao — chỉ sửa lỗi trong ownership FE2.

## File được phép chỉnh

- `Areas/Admin/Views/AdminCategory/Create.cshtml`
- `Areas/Admin/Views/AdminCategory/Edit.cshtml`
- `Areas/Admin/Views/AdminCategory/Index.cshtml`
- `Areas/Admin/Views/AdminIngredient/Index.cshtml`
- `Areas/Admin/Views/AdminUnitConversion/Create.cshtml`
- `Areas/Admin/Views/AdminUnitConversion/Edit.cshtml`
- `Areas/Admin/Views/AdminUnitConversion/Index.cshtml`
- `wwwroot/css/Admin/Category/Category.css`
- `wwwroot/css/Admin/Ingredient/ingredient.css`
- `wwwroot/css/unit-conversion.css`
- `Areas/Admin/Views/Dashboard/Guide.cshtml`
- `Areas/Admin/Views/Dashboard/Index.cshtml`
- `wwwroot/css/Admin/Dashboard/dashboard.css`
- `Areas/Admin/Views/AdminStoreInventory/Index.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_InventoryTablePartial.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_PaginationPartial.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_StoreTabsPartial.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_TransactionModalPartial.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_TransactionPartial.cshtml`
- `Areas/Admin/Views/AdminInventoryThresholds/Index.cshtml`
- `Areas/Admin/Views/AdminStockAlerts/Details.cshtml`
- `Areas/Admin/Views/AdminStockAlerts/Index.cshtml`
- `Areas/Admin/Views/AdminOperationalAnomalies/Index.cshtml`
- `wwwroot/css/Admin/InventoryOperations/inventory-operations.css`
- `wwwroot/css/Admin/StoreInventory/storeinventory.css`
- `Areas/Admin/Views/AdminRestockRequests/CreateCentralPlanner.cshtml`
- `Areas/Admin/Views/AdminRestockRequests/CreateManual.cshtml`
- `Areas/Admin/Views/AdminRestockRequests/Details.cshtml`
- `Areas/Admin/Views/AdminRestockRequests/Index.cshtml`
- `Areas/Admin/Views/AdminReorderSuggestions/Index.cshtml`
- `wwwroot/css/Admin/Procurement/procurement-design-system.css`
- `wwwroot/css/Admin/Procurement/reorder-suggestions.css`
- `Areas/Admin/Views/AdminPurchaseAdvices/Create.cshtml`
- `Areas/Admin/Views/AdminPurchaseAdvices/Details.cshtml`
- `Areas/Admin/Views/AdminPurchaseAdvices/Edit.cshtml`
- `Areas/Admin/Views/AdminPurchaseAdvices/Index.cshtml`
- `Areas/Admin/Views/AdminPurchaseAdviceConsolidation/Index.cshtml`
- `Areas/Admin/Views/AdminPurchaseOrderBatches/Details.cshtml`
- `Areas/Admin/Views/AdminPurchaseOrderBatches/Index.cshtml`
- `wwwroot/css/Admin/PurchaseAdvice/purchase-advice.css`
- `Areas/Admin/Views/AdminPurchaseOrders/Create.cshtml`
- `Areas/Admin/Views/AdminPurchaseOrders/Details.cshtml`
- `Areas/Admin/Views/AdminPurchaseOrders/Index.cshtml`
- `Areas/Admin/Views/AdminBranchReceipts/Create.cshtml`
- `Areas/Admin/Views/AdminBranchReceipts/Details.cshtml`
- `Areas/Admin/Views/AdminBranchReceipts/Index.cshtml`
- `Areas/Admin/Views/AdminBranchReceipts/PurchaseOrderDraft.cshtml`
- `Areas/Admin/Views/AdminOperationalIce/Details.cshtml`
- `Areas/Admin/Views/AdminOperationalIce/Index.cshtml`
- `Areas/Admin/Views/AdminOperationalIce/Report.cshtml`
- `wwwroot/css/Admin/OperationalIce/operational-ice.css`
- `Areas/Admin/Views/AdminStaff/Edit.cshtml`
- `Areas/Admin/Views/AdminStaff/Index.cshtml`
- `Areas/Admin/Views/AdminStaff/_CreateStaffModal.cshtml`
- `Areas/Admin/Views/AdminPermission/Index.cshtml`
- `wwwroot/css/Admin/Staff/staff.css`
- `wwwroot/css/Admin/Permissions/admin-permissions.css`
- `Areas/Admin/Views/AdminStaffShift/Index.cshtml`
- `Areas/Admin/Views/AdminShiftOptimization/Index.cshtml`
- `wwwroot/css/Admin/StaffShift/admin-staff-shift.css`
- `wwwroot/css/Admin/StaffShift/shift-optimization.css`
- `Areas/Admin/Views/AdminStore/Create.cshtml`
- `Areas/Admin/Views/AdminStore/Edit.cshtml`
- `Areas/Admin/Views/AdminStore/Index.cshtml`
- `wwwroot/css/Admin/Store/store-admin.css`
- `Areas/Admin/Views/AdminSupplier/Index.cshtml`
- `Areas/Admin/Views/AdminSupplierQuality/Create.cshtml`
- `Areas/Admin/Views/AdminSupplierQuality/Index.cshtml`
- `wwwroot/css/Admin/Supplier/supplier.css`
- `Areas/Admin/Views/AdminProfile/MyProfile.cshtml`
- `Areas/Admin/Views/AdminNotifications/Index.cshtml`
- `Areas/Admin/Views/AdminSetting/Index.cshtml`
- `Areas/Admin/Views/AdminSetting/Partials/_NegativeInventorySettings.cshtml`
- `wwwroot/css/Admin/Profile/admin-profile.css`
- `wwwroot/css/Admin/Notifications/admin-notifications.css`
- `wwwroot/css/Admin/Settings/negative-inventory.css`

Cấm chỉnh file ngoài danh sách này. File liên quan khác chỉ được đọc để hiểu hook và visual context.

## Kết quả giao diện phải đạt

- Đối chiếu Dashboard, Master Data, Inventory, Procurement, Ice, HR, Store và System với Core/FE1.
- Sửa mọi khác biệt button/input/table/modal/header/validation/responsive.
- Không sửa FE1 Core; handoff lỗi token hoặc selector chung cho FE1/nhóm trưởng.

## Quy chuẩn chi tiết theo loại trang

- So sánh ít nhất một Index, Create/Edit, Detail, modal, scheduler, dashboard và document với FE1.
- Không để procurement mỗi bước một style.
- Không để HR, Store hoặc System tách khỏi Admin contract.

## Phần phải bảo toàn tuyệt đối

- Toàn bộ ownership FE1, Shared và admin-unified-depth.css.
- JavaScript/backend/DOM như các đợt trước.

## Prompt A — Audit read-only

Sao chép nguyên khối:

```text
Bạn đang làm Frontend 2 của CafeChain.

BẮT BUỘC đọc đầy đủ, theo thứ tự:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Toàn bộ source của các file được liệt kê cho đợt `FINAL REGRESSION FRONTEND 2 VÀ ĐỐI CHIẾU TOÀN ADMIN`.

ĐÂY CHỈ LÀ PHASE A — AUDIT READ-ONLY. TUYỆT ĐỐI CHƯA SỬA CODE.

Phạm vi đợt này:
- `Areas/Admin/Views/AdminCategory/Create.cshtml`
- `Areas/Admin/Views/AdminCategory/Edit.cshtml`
- `Areas/Admin/Views/AdminCategory/Index.cshtml`
- `Areas/Admin/Views/AdminIngredient/Index.cshtml`
- `Areas/Admin/Views/AdminUnitConversion/Create.cshtml`
- `Areas/Admin/Views/AdminUnitConversion/Edit.cshtml`
- `Areas/Admin/Views/AdminUnitConversion/Index.cshtml`
- `wwwroot/css/Admin/Category/Category.css`
- `wwwroot/css/Admin/Ingredient/ingredient.css`
- `wwwroot/css/unit-conversion.css`
- `Areas/Admin/Views/Dashboard/Guide.cshtml`
- `Areas/Admin/Views/Dashboard/Index.cshtml`
- `wwwroot/css/Admin/Dashboard/dashboard.css`
- `Areas/Admin/Views/AdminStoreInventory/Index.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_InventoryTablePartial.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_PaginationPartial.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_StoreTabsPartial.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_TransactionModalPartial.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_TransactionPartial.cshtml`
- `Areas/Admin/Views/AdminInventoryThresholds/Index.cshtml`
- `Areas/Admin/Views/AdminStockAlerts/Details.cshtml`
- `Areas/Admin/Views/AdminStockAlerts/Index.cshtml`
- `Areas/Admin/Views/AdminOperationalAnomalies/Index.cshtml`
- `wwwroot/css/Admin/InventoryOperations/inventory-operations.css`
- `wwwroot/css/Admin/StoreInventory/storeinventory.css`
- `Areas/Admin/Views/AdminRestockRequests/CreateCentralPlanner.cshtml`
- `Areas/Admin/Views/AdminRestockRequests/CreateManual.cshtml`
- `Areas/Admin/Views/AdminRestockRequests/Details.cshtml`
- `Areas/Admin/Views/AdminRestockRequests/Index.cshtml`
- `Areas/Admin/Views/AdminReorderSuggestions/Index.cshtml`
- `wwwroot/css/Admin/Procurement/procurement-design-system.css`
- `wwwroot/css/Admin/Procurement/reorder-suggestions.css`
- `Areas/Admin/Views/AdminPurchaseAdvices/Create.cshtml`
- `Areas/Admin/Views/AdminPurchaseAdvices/Details.cshtml`
- `Areas/Admin/Views/AdminPurchaseAdvices/Edit.cshtml`
- `Areas/Admin/Views/AdminPurchaseAdvices/Index.cshtml`
- `Areas/Admin/Views/AdminPurchaseAdviceConsolidation/Index.cshtml`
- `Areas/Admin/Views/AdminPurchaseOrderBatches/Details.cshtml`
- `Areas/Admin/Views/AdminPurchaseOrderBatches/Index.cshtml`
- `wwwroot/css/Admin/PurchaseAdvice/purchase-advice.css`
- `Areas/Admin/Views/AdminPurchaseOrders/Create.cshtml`
- `Areas/Admin/Views/AdminPurchaseOrders/Details.cshtml`
- `Areas/Admin/Views/AdminPurchaseOrders/Index.cshtml`
- `Areas/Admin/Views/AdminBranchReceipts/Create.cshtml`
- `Areas/Admin/Views/AdminBranchReceipts/Details.cshtml`
- `Areas/Admin/Views/AdminBranchReceipts/Index.cshtml`
- `Areas/Admin/Views/AdminBranchReceipts/PurchaseOrderDraft.cshtml`
- `Areas/Admin/Views/AdminOperationalIce/Details.cshtml`
- `Areas/Admin/Views/AdminOperationalIce/Index.cshtml`
- `Areas/Admin/Views/AdminOperationalIce/Report.cshtml`
- `wwwroot/css/Admin/OperationalIce/operational-ice.css`
- `Areas/Admin/Views/AdminStaff/Edit.cshtml`
- `Areas/Admin/Views/AdminStaff/Index.cshtml`
- `Areas/Admin/Views/AdminStaff/_CreateStaffModal.cshtml`
- `Areas/Admin/Views/AdminPermission/Index.cshtml`
- `wwwroot/css/Admin/Staff/staff.css`
- `wwwroot/css/Admin/Permissions/admin-permissions.css`
- `Areas/Admin/Views/AdminStaffShift/Index.cshtml`
- `Areas/Admin/Views/AdminShiftOptimization/Index.cshtml`
- `wwwroot/css/Admin/StaffShift/admin-staff-shift.css`
- `wwwroot/css/Admin/StaffShift/shift-optimization.css`
- `Areas/Admin/Views/AdminStore/Create.cshtml`
- `Areas/Admin/Views/AdminStore/Edit.cshtml`
- `Areas/Admin/Views/AdminStore/Index.cshtml`
- `wwwroot/css/Admin/Store/store-admin.css`
- `Areas/Admin/Views/AdminSupplier/Index.cshtml`
- `Areas/Admin/Views/AdminSupplierQuality/Create.cshtml`
- `Areas/Admin/Views/AdminSupplierQuality/Index.cshtml`
- `wwwroot/css/Admin/Supplier/supplier.css`
- `Areas/Admin/Views/AdminProfile/MyProfile.cshtml`
- `Areas/Admin/Views/AdminNotifications/Index.cshtml`
- `Areas/Admin/Views/AdminSetting/Index.cshtml`
- `Areas/Admin/Views/AdminSetting/Partials/_NegativeInventorySettings.cshtml`
- `wwwroot/css/Admin/Profile/admin-profile.css`
- `wwwroot/css/Admin/Notifications/admin-notifications.css`
- `wwwroot/css/Admin/Settings/negative-inventory.css`

Hãy trả báo cáo theo đúng cấu trúc:

A. Scope confirmation
- Liệt kê đúng từng file được phép chỉnh.
- Liệt kê file liên quan đã đọc nhưng không được chỉnh.
- Xác nhận không sửa file ownership của frontend còn lại.

B. DOM và functional freeze
- Liệt kê form, table, modal, tab, collapse, partial, hidden input và dynamic region hiện có.
- Liệt kê `id`, `name`, `data-*`, `asp-*`, class/hook có khả năng được JS/Bootstrap/Select2/chart/map/drag-drop sử dụng.
- Nêu chính xác phần nào trong `.cshtml` chỉ được bổ sung class và phần nào phải để nguyên hoàn toàn.

C. Current UI audit
- Page header hiện tại.
- Button variants và kích thước hiện tại.
- Form control, validation, Select2 hiện tại.
- Table/list/pagination hiện tại.
- Modal/confirm/empty/loading/error hiện tại.
- Màu, token, spacing, radius, shadow và `!important` bị trùng hoặc lệch.

D. Target mapping
- Lập bảng `existing selector → visual contract --cc-* → file CSS sẽ chỉnh`.
- Chỉ ra selector nào dùng CSS chung và selector nào phải giữ exception nghiệp vụ.
- Mô tả cách làm Index/Create/Edit/Details/Delete/Modal đồng bộ mà không đổi DOM.

E. Risk register
- Specificity và thứ tự load CSS.
- Inline style và `<style>` cục bộ.
- Plugin control.
- Responsive table/modal.
- Nguy cơ selector lan sang StaffHub/POS/module frontend còn lại.

F. Test plan
- Trang và luồng cần test trước/sau.
- Viewport cần chụp.
- Functional regression bắt buộc.

Kết luận bằng một trong hai trạng thái:
- `READY FOR IMPLEMENTATION`: phạm vi rõ, không cần sửa JS/backend/DOM.
- `BLOCKED`: ghi đúng vấn đề cần nhóm trưởng giải quyết.

Sau báo cáo phải dừng. Không được chỉnh code hoặc tuyên bố đã hoàn thành giao diện.
```

Chỉ chuyển sang Prompt B khi báo cáo kết thúc bằng `READY FOR IMPLEMENTATION` và frontend đã đọc, đối chiếu đúng file/hook.

## Prompt B — Triển khai chính thức

```text
Bạn đang làm Frontend 2 của CafeChain.

BẮT BUỘC đọc lại:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Báo cáo audit Phase A vừa được duyệt.

PHASE B — CHỈ TRIỂN KHAI ĐỢT `FINAL REGRESSION FRONTEND 2 VÀ ĐỐI CHIẾU TOÀN ADMIN`.

CHỈ ĐƯỢC CHỈNH:
- `Areas/Admin/Views/AdminCategory/Create.cshtml`
- `Areas/Admin/Views/AdminCategory/Edit.cshtml`
- `Areas/Admin/Views/AdminCategory/Index.cshtml`
- `Areas/Admin/Views/AdminIngredient/Index.cshtml`
- `Areas/Admin/Views/AdminUnitConversion/Create.cshtml`
- `Areas/Admin/Views/AdminUnitConversion/Edit.cshtml`
- `Areas/Admin/Views/AdminUnitConversion/Index.cshtml`
- `wwwroot/css/Admin/Category/Category.css`
- `wwwroot/css/Admin/Ingredient/ingredient.css`
- `wwwroot/css/unit-conversion.css`
- `Areas/Admin/Views/Dashboard/Guide.cshtml`
- `Areas/Admin/Views/Dashboard/Index.cshtml`
- `wwwroot/css/Admin/Dashboard/dashboard.css`
- `Areas/Admin/Views/AdminStoreInventory/Index.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_InventoryTablePartial.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_PaginationPartial.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_StoreTabsPartial.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_TransactionModalPartial.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_TransactionPartial.cshtml`
- `Areas/Admin/Views/AdminInventoryThresholds/Index.cshtml`
- `Areas/Admin/Views/AdminStockAlerts/Details.cshtml`
- `Areas/Admin/Views/AdminStockAlerts/Index.cshtml`
- `Areas/Admin/Views/AdminOperationalAnomalies/Index.cshtml`
- `wwwroot/css/Admin/InventoryOperations/inventory-operations.css`
- `wwwroot/css/Admin/StoreInventory/storeinventory.css`
- `Areas/Admin/Views/AdminRestockRequests/CreateCentralPlanner.cshtml`
- `Areas/Admin/Views/AdminRestockRequests/CreateManual.cshtml`
- `Areas/Admin/Views/AdminRestockRequests/Details.cshtml`
- `Areas/Admin/Views/AdminRestockRequests/Index.cshtml`
- `Areas/Admin/Views/AdminReorderSuggestions/Index.cshtml`
- `wwwroot/css/Admin/Procurement/procurement-design-system.css`
- `wwwroot/css/Admin/Procurement/reorder-suggestions.css`
- `Areas/Admin/Views/AdminPurchaseAdvices/Create.cshtml`
- `Areas/Admin/Views/AdminPurchaseAdvices/Details.cshtml`
- `Areas/Admin/Views/AdminPurchaseAdvices/Edit.cshtml`
- `Areas/Admin/Views/AdminPurchaseAdvices/Index.cshtml`
- `Areas/Admin/Views/AdminPurchaseAdviceConsolidation/Index.cshtml`
- `Areas/Admin/Views/AdminPurchaseOrderBatches/Details.cshtml`
- `Areas/Admin/Views/AdminPurchaseOrderBatches/Index.cshtml`
- `wwwroot/css/Admin/PurchaseAdvice/purchase-advice.css`
- `Areas/Admin/Views/AdminPurchaseOrders/Create.cshtml`
- `Areas/Admin/Views/AdminPurchaseOrders/Details.cshtml`
- `Areas/Admin/Views/AdminPurchaseOrders/Index.cshtml`
- `Areas/Admin/Views/AdminBranchReceipts/Create.cshtml`
- `Areas/Admin/Views/AdminBranchReceipts/Details.cshtml`
- `Areas/Admin/Views/AdminBranchReceipts/Index.cshtml`
- `Areas/Admin/Views/AdminBranchReceipts/PurchaseOrderDraft.cshtml`
- `Areas/Admin/Views/AdminOperationalIce/Details.cshtml`
- `Areas/Admin/Views/AdminOperationalIce/Index.cshtml`
- `Areas/Admin/Views/AdminOperationalIce/Report.cshtml`
- `wwwroot/css/Admin/OperationalIce/operational-ice.css`
- `Areas/Admin/Views/AdminStaff/Edit.cshtml`
- `Areas/Admin/Views/AdminStaff/Index.cshtml`
- `Areas/Admin/Views/AdminStaff/_CreateStaffModal.cshtml`
- `Areas/Admin/Views/AdminPermission/Index.cshtml`
- `wwwroot/css/Admin/Staff/staff.css`
- `wwwroot/css/Admin/Permissions/admin-permissions.css`
- `Areas/Admin/Views/AdminStaffShift/Index.cshtml`
- `Areas/Admin/Views/AdminShiftOptimization/Index.cshtml`
- `wwwroot/css/Admin/StaffShift/admin-staff-shift.css`
- `wwwroot/css/Admin/StaffShift/shift-optimization.css`
- `Areas/Admin/Views/AdminStore/Create.cshtml`
- `Areas/Admin/Views/AdminStore/Edit.cshtml`
- `Areas/Admin/Views/AdminStore/Index.cshtml`
- `wwwroot/css/Admin/Store/store-admin.css`
- `Areas/Admin/Views/AdminSupplier/Index.cshtml`
- `Areas/Admin/Views/AdminSupplierQuality/Create.cshtml`
- `Areas/Admin/Views/AdminSupplierQuality/Index.cshtml`
- `wwwroot/css/Admin/Supplier/supplier.css`
- `Areas/Admin/Views/AdminProfile/MyProfile.cshtml`
- `Areas/Admin/Views/AdminNotifications/Index.cshtml`
- `Areas/Admin/Views/AdminSetting/Index.cshtml`
- `Areas/Admin/Views/AdminSetting/Partials/_NegativeInventorySettings.cshtml`
- `wwwroot/css/Admin/Profile/admin-profile.css`
- `wwwroot/css/Admin/Notifications/admin-notifications.css`
- `wwwroot/css/Admin/Settings/negative-inventory.css`

MỤC TIÊU NGHIỆP VỤ VÀ GIAO DIỆN:
- Đối chiếu Dashboard, Master Data, Inventory, Procurement, Ice, HR, Store và System với Core/FE1.
- Sửa mọi khác biệt button/input/table/modal/header/validation/responsive.
- Không sửa FE1 Core; handoff lỗi token hoặc selector chung cho FE1/nhóm trưởng.

QUY CHUẨN THEO TRANG/FORM:
- So sánh ít nhất một Index, Create/Edit, Detail, modal, scheduler, dashboard và document với FE1.
- Không để procurement mỗi bước một style.
- Không để HR, Store hoặc System tách khỏi Admin contract.

PHẦN PHẢI ĐÓNG BĂNG:
- Toàn bộ ownership FE1, Shared và admin-unified-depth.css.
- JavaScript/backend/DOM như các đợt trước.

RÀNG BUỘC TUYỆT ĐỐI:
- Chỉ chỉnh `.cshtml` và `.css` nêu trên.
- Không chỉnh JavaScript, backend, database, SQL, migration, seed, permission hoặc nghiệp vụ.
- Không thêm/xóa/đổi tên/di chuyển cấu trúc thẻ `.cshtml`.
- Giữ nguyên `id`, `name`, `data-*`, `asp-*`, route, action, method, hidden input, validation, modal/tab/collapse và mọi hook.
- Không thêm framework/package/font/script.
- Không format hàng loạt.
- Không dùng selector global lan khỏi Admin.
- Không tạo design token cạnh tranh với `--cc-*`.
- Không dùng `!important` nếu selector đúng có thể giải quyết; exception phải có comment module và lý do.
- Không chỉnh Online, Voucher, Wheel, StaffHub hoặc POS riêng.

CÁCH TRIỂN KHAI BẮT BUỘC:
1. Giữ CSS module cho bố cục đặc thù; dùng token `--cc-*` để đồng bộ màu, type, spacing, radius và shadow.
2. Ưu tiên CSS selector tương thích với markup hiện tại; `.cshtml` chỉ bổ sung class visual khi CSS hiện tại không thể xử lý an toàn.
3. Đồng bộ đầy đủ normal/hover/active/focus/disabled/loading/error/empty state.
4. Đồng bộ Index/Create/Edit/Details/Delete/Modal trong phạm vi; không chỉ sửa trang Index.
5. Sửa responsive ngay trong đợt, không để dành toàn bộ đến cuối.
6. Sau mỗi nhóm selector, kiểm tra một trang đại diện để phát hiện conflict sớm.

KIỂM TRA BẮT BUỘC SAU KHI CODE:
- Full build.
- Test toàn bộ workflow FE2.
- 5 viewport + zoom 125%.
- Kiểm tra chart, map, scheduler, Select2, modal và workflow.
- Lập bảng PASS/FAIL từng module.

Trước khi kết luận, chạy và báo kết quả:
- `git diff --name-only`
- `git diff --stat`
- `git diff --check`
- `dotnet build`
- kiểm tra diff `.cshtml` để xác nhận không thay DOM/hook/binding.

OUTPUT CUỐI CÙNG PHẢI CÓ:
1. Danh sách chính xác file đã chỉnh và file chỉ đọc.
2. Tóm tắt thay đổi theo từng file.
3. Bảng selector cũ → contract mới.
4. Danh sách trạng thái UI đã hoàn thiện.
5. Danh sách viewport và luồng đã test, kèm kết quả đạt/chưa đạt.
6. Xác nhận không chỉnh file bị cấm, JavaScript, backend, database, seed, nghiệp vụ hoặc DOM.
7. CSS exception/`!important` còn lại và lý do.
8. Lỗi/rủi ro chưa xử lý; không được che giấu.
9. Kết luận chỉ được là `PASS — READY TO COMMIT` hoặc `FAIL — DO NOT COMMIT`.

Không được tuyên bố PASS nếu chưa kiểm tra toàn bộ mục trên.
```

## Prompt C — Kiểm tra độc lập sau khi làm

```text
Hãy thực hiện PHASE C — INDEPENDENT VERIFICATION cho đợt `FINAL REGRESSION FRONTEND 2 VÀ ĐỐI CHIẾU TOÀN ADMIN`. Không sửa thêm code trong bước này.

Đọc:
- hai file đặc tả chính trong `docs`;
- `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`;
- toàn bộ `git diff` hiện tại.

Kiểm tra độc lập:
1. File thay đổi có đúng ownership và đúng danh sách đợt không.
2. Có file `.js`, `.ts`, `.cs`, `.sql`, migration, seed hoặc backend nào thay đổi không.
3. `.cshtml` có thay cấu trúc thẻ, thứ tự DOM, `id`, `name`, `asp-*`, `data-*`, form action/method, hidden input hoặc hook không.
4. Index/Create/Edit/Details/Delete/Modal trong phạm vi đã dùng cùng visual contract chưa.
5. Button, input, table, modal, badge, page header và validation có cùng kích thước/semantics chưa.
6. Có selector global hoặc `!important` không cần thiết gây conflict không.
7. Desktop/tablet/mobile/zoom có overflow, chữ chìm, action bị che hoặc modal mất footer không.
8. Các chức năng sau còn hoạt động nguyên vẹn không:
- Full build.
- Test toàn bộ workflow FE2.
- 5 viewport + zoom 125%.
- Kiểm tra chart, map, scheduler, Select2, modal và workflow.
- Lập bảng PASS/FAIL từng module.
9. `dotnet build`, `git diff --check` có pass không.
10. So sánh ảnh trước/sau; chỉ ra trang nào còn lệch với module đã chuẩn hóa.

Trả kết quả dạng bảng: `Tiêu chí | PASS/FAIL | Bằng chứng | Việc cần sửa`.
Kết luận duy nhất:
- `VERIFIED PASS — MAY COMMIT AND PUSH`, hoặc
- `VERIFIED FAIL — MUST FIX BEFORE COMMIT`.
Không sửa code trong Phase C.
```

## Checklist thủ công của frontend

- Full build.
- Test toàn bộ workflow FE2.
- 5 viewport + zoom 125%.
- Kiểm tra chart, map, scheduler, Select2, modal và workflow.
- Lập bảng PASS/FAIL từng module.

Ngoài kiểm thử chức năng, bắt buộc chụp ít nhất:

- Ảnh trước và sau ở `1440×900`.
- Ảnh sau ở `1024×768`.
- Ảnh sau ở `390×844`.
- Một ảnh thể hiện form hoặc modal có validation.
- Một ảnh thể hiện table/list và action.

## Commit và push

Chỉ commit khi Prompt C trả `VERIFIED PASS — MAY COMMIT AND PUSH` và frontend đã tự kiểm tra.

```bash
# Chỉ các path ownership FE2; file không đổi sẽ không được stage
git add "Areas/Admin/Views/AdminCategory/Create.cshtml"
git add "Areas/Admin/Views/AdminCategory/Edit.cshtml"
git add "Areas/Admin/Views/AdminCategory/Index.cshtml"
git add "Areas/Admin/Views/AdminIngredient/Index.cshtml"
git add "Areas/Admin/Views/AdminUnitConversion/Create.cshtml"
git add "Areas/Admin/Views/AdminUnitConversion/Edit.cshtml"
git add "Areas/Admin/Views/AdminUnitConversion/Index.cshtml"
git add "wwwroot/css/Admin/Category/Category.css"
git add "wwwroot/css/Admin/Ingredient/ingredient.css"
git add "wwwroot/css/unit-conversion.css"
git add "Areas/Admin/Views/Dashboard/Guide.cshtml"
git add "Areas/Admin/Views/Dashboard/Index.cshtml"
git add "wwwroot/css/Admin/Dashboard/dashboard.css"
git add "Areas/Admin/Views/AdminStoreInventory/Index.cshtml"
git add "Areas/Admin/Views/AdminStoreInventory/Partials/_InventoryTablePartial.cshtml"
git add "Areas/Admin/Views/AdminStoreInventory/Partials/_PaginationPartial.cshtml"
git add "Areas/Admin/Views/AdminStoreInventory/Partials/_StoreTabsPartial.cshtml"
git add "Areas/Admin/Views/AdminStoreInventory/Partials/_TransactionModalPartial.cshtml"
git add "Areas/Admin/Views/AdminStoreInventory/Partials/_TransactionPartial.cshtml"
git add "Areas/Admin/Views/AdminInventoryThresholds/Index.cshtml"
git add "Areas/Admin/Views/AdminStockAlerts/Details.cshtml"
git add "Areas/Admin/Views/AdminStockAlerts/Index.cshtml"
git add "Areas/Admin/Views/AdminOperationalAnomalies/Index.cshtml"
git add "wwwroot/css/Admin/InventoryOperations/inventory-operations.css"
git add "wwwroot/css/Admin/StoreInventory/storeinventory.css"
git add "Areas/Admin/Views/AdminRestockRequests/CreateCentralPlanner.cshtml"
git add "Areas/Admin/Views/AdminRestockRequests/CreateManual.cshtml"
git add "Areas/Admin/Views/AdminRestockRequests/Details.cshtml"
git add "Areas/Admin/Views/AdminRestockRequests/Index.cshtml"
git add "Areas/Admin/Views/AdminReorderSuggestions/Index.cshtml"
git add "wwwroot/css/Admin/Procurement/procurement-design-system.css"
git add "wwwroot/css/Admin/Procurement/reorder-suggestions.css"
git add "Areas/Admin/Views/AdminPurchaseAdvices/Create.cshtml"
git add "Areas/Admin/Views/AdminPurchaseAdvices/Details.cshtml"
git add "Areas/Admin/Views/AdminPurchaseAdvices/Edit.cshtml"
git add "Areas/Admin/Views/AdminPurchaseAdvices/Index.cshtml"
git add "Areas/Admin/Views/AdminPurchaseAdviceConsolidation/Index.cshtml"
git add "Areas/Admin/Views/AdminPurchaseOrderBatches/Details.cshtml"
git add "Areas/Admin/Views/AdminPurchaseOrderBatches/Index.cshtml"
git add "wwwroot/css/Admin/PurchaseAdvice/purchase-advice.css"
git add "Areas/Admin/Views/AdminPurchaseOrders/Create.cshtml"
git add "Areas/Admin/Views/AdminPurchaseOrders/Details.cshtml"
git add "Areas/Admin/Views/AdminPurchaseOrders/Index.cshtml"
git add "Areas/Admin/Views/AdminBranchReceipts/Create.cshtml"
git add "Areas/Admin/Views/AdminBranchReceipts/Details.cshtml"
git add "Areas/Admin/Views/AdminBranchReceipts/Index.cshtml"
git add "Areas/Admin/Views/AdminBranchReceipts/PurchaseOrderDraft.cshtml"
git add "Areas/Admin/Views/AdminOperationalIce/Details.cshtml"
git add "Areas/Admin/Views/AdminOperationalIce/Index.cshtml"
git add "Areas/Admin/Views/AdminOperationalIce/Report.cshtml"
git add "wwwroot/css/Admin/OperationalIce/operational-ice.css"
git add "Areas/Admin/Views/AdminStaff/Edit.cshtml"
git add "Areas/Admin/Views/AdminStaff/Index.cshtml"
git add "Areas/Admin/Views/AdminStaff/_CreateStaffModal.cshtml"
git add "Areas/Admin/Views/AdminPermission/Index.cshtml"
git add "wwwroot/css/Admin/Staff/staff.css"
git add "wwwroot/css/Admin/Permissions/admin-permissions.css"
git add "Areas/Admin/Views/AdminStaffShift/Index.cshtml"
git add "Areas/Admin/Views/AdminShiftOptimization/Index.cshtml"
git add "wwwroot/css/Admin/StaffShift/admin-staff-shift.css"
git add "wwwroot/css/Admin/StaffShift/shift-optimization.css"
git add "Areas/Admin/Views/AdminStore/Create.cshtml"
git add "Areas/Admin/Views/AdminStore/Edit.cshtml"
git add "Areas/Admin/Views/AdminStore/Index.cshtml"
git add "wwwroot/css/Admin/Store/store-admin.css"
git add "Areas/Admin/Views/AdminSupplier/Index.cshtml"
git add "Areas/Admin/Views/AdminSupplierQuality/Create.cshtml"
git add "Areas/Admin/Views/AdminSupplierQuality/Index.cshtml"
git add "wwwroot/css/Admin/Supplier/supplier.css"
git add "Areas/Admin/Views/AdminProfile/MyProfile.cshtml"
git add "Areas/Admin/Views/AdminNotifications/Index.cshtml"
git add "Areas/Admin/Views/AdminSetting/Index.cshtml"
git add "Areas/Admin/Views/AdminSetting/Partials/_NegativeInventorySettings.cshtml"
git add "wwwroot/css/Admin/Profile/admin-profile.css"
git add "wwwroot/css/Admin/Notifications/admin-notifications.css"
git add "wwwroot/css/Admin/Settings/negative-inventory.css"
git diff --cached --name-only
git diff --cached --stat
git diff --cached --check
git commit -m "fix(admin-ui-fe2): resolve final visual regression"
git push -u origin fix/admin-ui-fe2-final-regression
```

Sau push: gửi nhóm trưởng branch, commit hash, ảnh trước/sau, checklist, build result và mọi exception còn lại. **Không tự merge.**


# Cổng phụ thuộc bắt buộc của Frontend 2

```text
FE1 Core được nhóm trưởng merge
    ↓
Master Data để xác nhận Core
    ↓
Dashboard có thể làm độc lập
    ↓
Inventory Core
    ↓
Restock/Reorder
    ↓
Purchase Advice/Consolidation/Batch
    ↓
Purchase Orders
    ↓
Branch Receipts
```

Chuỗi HR:

```text
Staff/Permission
    ↓
StaffShift/ShiftOptimization
```

- Không sửa `Shared`, `_ViewStart`, `_ViewImports`, `admin-unified-depth.css` hoặc file ownership FE1.
- Nếu Core thiếu selector/token, tạo handoff có selector, ảnh và tác động; không tự sửa Core.
- Dashboard, Ice, Store, Supplier và System có thể triển khai sau Core, nhưng tài liệu sắp thứ tự để giảm số branch song song và dễ review.

# Thứ tự push chốt Frontend 2

1. `feature/admin-ui-fe2-master-data`.
2. `feature/admin-ui-fe2-dashboard`.
3. `feature/admin-ui-fe2-inventory-core` — phải merge trước Restock/Reorder.
4. `feature/admin-ui-fe2-restock-reorder` — phải merge trước Purchase Advice.
5. `feature/admin-ui-fe2-purchase-advice` — phải merge trước PO.
6. `feature/admin-ui-fe2-purchase-orders` — phải merge trước Receipt.
7. `feature/admin-ui-fe2-branch-receipts`.
8. `feature/admin-ui-fe2-operational-ice`.
9. `feature/admin-ui-fe2-staff-permission` — phải merge trước Shift.
10. `feature/admin-ui-fe2-staff-shift`.
11. `feature/admin-ui-fe2-store`.
12. `feature/admin-ui-fe2-supplier`.
13. `feature/admin-ui-fe2-system`.
14. `fix/admin-ui-fe2-final-regression`.


# Mẫu báo cáo gửi nhóm trưởng sau mỗi push

```md
# Báo cáo UI — [Frontend] — [Đợt/module]

## Branch và commit
- Branch:
- Commit hash:
- Dependency đã merge:

## File đã chỉnh
- ...

## Phần giao diện đã hoàn thành
- Index/List:
- Create/Edit:
- Details/Approval:
- Delete/Confirm/Modal:
- Responsive:
- Accessibility:

## Khóa phạm vi
- [ ] Không chỉnh JavaScript/Backend/Database/Seed.
- [ ] Không thay DOM, id, name, asp-*, data-* hoặc hook.
- [ ] Không chỉnh file ngoài ownership.
- [ ] Không chỉnh Online/Voucher/Wheel/StaffHub/POS.

## Kiểm thử
- Build:
- Diff check:
- Desktop:
- Tablet:
- Mobile:
- Zoom 125%:
- Chức năng:

## Ảnh bằng chứng
- Before:
- After desktop:
- After mobile:
- Validation/modal/table:

## Exception và rủi ro
- ...

## Kết luận
- READY FOR LEAD REVIEW / NOT READY
```

# Điều kiện hoàn thành cuối cùng

Một frontend chỉ được xem là hoàn thành khi:

- Tất cả đợt thuộc ownership đã được nhóm trưởng duyệt và merge.
- Không còn trang CRUD nào trong phạm vi sử dụng visual contract khác biệt rõ rệt.
- Nền Admin, header, button, form, table, modal, badge, validation và responsive đồng bộ.
- Không có chữ chìm, overflow toàn trang, modal mất footer hoặc action khó phân biệt.
- `dotnet build` và `git diff --check` pass.
- Không có thay đổi JavaScript, backend, database, seed, nghiệp vụ hoặc DOM hook.
- Final regression có ảnh và checklist bằng chứng; không chỉ xác nhận bằng lời.
