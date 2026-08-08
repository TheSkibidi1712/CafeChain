# HƯỚNG DẪN FRONTEND 1 LÀM LẠI ADMIN UI CAFECHAIN — CHI TIẾT V2


> **Vai trò:** Frontend 1  
> **Tài liệu thao tác:** đặt file này tại `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.  
> **Mục tiêu:** triển khai lại giao diện Admin theo từng đợt có kiểm soát; đồng bộ toàn bộ module, form thêm/sửa/xóa, danh sách, chi tiết, modal và responsive; chỉ push để nhóm trưởng duyệt, không tự merge.


**Ownership chính:** Core Design System, Shared/Admin shell, Drink, Size, Topping, StoreMenu, Profitability, Recipe/BOM, PreparedItem, ProductionOrder, InventoryDocument và InventoryTransfer.

Frontend 1 là owner duy nhất của `wwwroot/css/Admin/admin-unified-depth.css` và lớp nền Admin. Trách nhiệm không chỉ là làm module của mình đẹp mà còn tạo contract đủ ổn định để Frontend 2 kế thừa.



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

1. AUDIT TỔNG VÀ LẬP BASELINE, CHƯA CODE
2. CORE ADMIN — NỀN, SIDEBAR, PAGE SHELL VÀ COMPONENT CONTRACT
3. DRINK — INDEX, CREATE, EDIT, TABLE VÀ MODAL
4. SIZE VÀ TOPPING — CRUD MODAL ĐỒNG BỘ
5. STORE MENU VÀ DRINK PROFITABILITY
6. RECIPE CORE — INDEX, CREATE VÀ EDIT
7. RECIPE DATA HEALTH, VISUALIZE VÀ BOM TREE
8. PREPARED ITEM VÀ PRODUCTION ORDER
9. INVENTORY DOCUMENT — INDEX, CREATE, DETAIL VÀ PARTIAL
10. INVENTORY TRANSFER — INDEX, CREATE, DETAIL, TIMELINE VÀ RESOLUTION
11. FINAL REGRESSION FRONTEND 1 VÀ ĐỐI CHIẾU TOÀN ADMIN

Không bỏ qua audit, verification hoặc dependency. Không gom nhiều đợt vào một prompt và không yêu cầu Antigravity “làm hết giao diện” trong một lần.

Đợt Core phải được nhóm trưởng duyệt và merge trước khi bất kỳ module Frontend 2 nào triển khai. Chỉ push, không tự merge.


# ĐỢT 1 — AUDIT TỔNG VÀ LẬP BASELINE, CHƯA CODE

## Mục tiêu

- Đọc đầy đủ hai file đặc tả chính và xác nhận ownership 58 view/13 CSS.
- Chụp baseline các trang đại diện và lập bảng selector/hook.
- Xác định chính xác phần nền nào phải đi qua `admin-unified-depth.css`.

## Quy tắc

- Đây là audit read-only. Không tạo diff và không sửa source.
- Không dùng Prompt B/Prompt C trong đợt này.
- Chỉ chuyển sang đợt triển khai khi audit kết luận phạm vi rõ và dependency đã đáp ứng.

## Prompt audit bắt buộc

```text
Bạn đang làm Frontend 1 của CafeChain.

BẮT BUỘC đọc đầy đủ, theo thứ tự:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Toàn bộ source của các file được liệt kê cho đợt `AUDIT TỔNG VÀ LẬP BASELINE, CHƯA CODE`.

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

- Không test thay đổi code; chỉ xác nhận dự án build được trước khi bắt đầu.
- Lưu hash `develop` làm mốc baseline.

Chạy:

```bash
git checkout develop
git pull origin develop
git status --short
git rev-parse HEAD
dotnet build
```

**Không commit và không push source trong đợt audit.** Báo cáo audit gửi trực tiếp cho nhóm trưởng.


# ĐỢT 2 — CORE ADMIN — NỀN, SIDEBAR, PAGE SHELL VÀ COMPONENT CONTRACT

## Điều kiện bắt đầu

Đợt audit đã được nhóm trưởng xem. Đây là đợt phải push và được nhóm trưởng merge trước khi Frontend 2 triển khai module.

- Nhóm trưởng đã xác nhận các dependency cần thiết đã merge vào `develop`.
- `git status --short` sạch.
- Branch phải được tạo mới từ `develop` vừa pull.

```bash
git checkout develop
git pull origin develop
git checkout -b feature/admin-ui-fe1-core
```

**Mức rủi ro:** Rất cao — ảnh hưởng toàn Admin.

## File được phép chỉnh

- `Areas/Admin/Views/Admin/Index.cshtml`
- `Areas/Admin/Views/Shared/StoreScopeError.cshtml`
- `Areas/Admin/Views/Shared/_AdminLayout.cshtml`
- `Areas/Admin/Views/Shared/_EmptyState.cshtml`
- `Areas/Admin/Views/Shared/_IdentityLabel.cshtml`
- `Areas/Admin/Views/Shared/_QuantityWithUnit.cshtml`
- `Areas/Admin/Views/Shared/_StatusBadge.cshtml`
- `Areas/Admin/Views/Shared/_ValidationScriptsPartial.cshtml`
- `Areas/Admin/Views/_ViewImports.cshtml`
- `Areas/Admin/Views/_ViewStart.cshtml`
- `wwwroot/css/Admin/admin-unified-depth.css`
- `wwwroot/css/admin-white-orange-forms.css`

Cấm chỉnh file ngoài danh sách này. File liên quan khác chỉ được đọc để hiểu hook và visual context.

## Kết quả giao diện phải đạt

- Tạo một nguồn token `--cc-*` duy nhất và alias token cũ thay vì xóa đột ngột.
- Đồng bộ canvas, sidebar 260px, main-content, page padding và typography nền.
- Map toàn bộ họ page header, button, form, table, card/KPI, badge/alert, modal/dropdown về cùng contract.
- Làm nền đủ an toàn để module Frontend 2 kế thừa mà không cần sửa file Core.

## Quy chuẩn chi tiết theo loại trang

- Admin Index và Shared partial phải dùng cùng surface, text, badge, empty/validation style.
- Page header list/dashboard cao `148–162px`; create/detail `126–140px`.
- Primary/secondary/ghost/danger và input/table/modal phải đủ state hover/focus/disabled.
- Không ép layout đặc thù của Dashboard, calendar, map, BOM hoặc document.

## Phần phải bảo toàn tuyệt đối

- Collapse IDs, permission conditions và menu order trong `_AdminLayout.cshtml`.
- Mọi script/link/hook trong Shared.
- `_ViewImports`, `_ViewStart`, `_ValidationScriptsPartial` mặc định chỉ đọc; chỉ sửa khi thật sự cần và nhóm trưởng chấp thuận.

## Prompt A — Audit read-only

Sao chép nguyên khối:

```text
Bạn đang làm Frontend 1 của CafeChain.

BẮT BUỘC đọc đầy đủ, theo thứ tự:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Toàn bộ source của các file được liệt kê cho đợt `CORE ADMIN — NỀN, SIDEBAR, PAGE SHELL VÀ COMPONENT CONTRACT`.

ĐÂY CHỈ LÀ PHASE A — AUDIT READ-ONLY. TUYỆT ĐỐI CHƯA SỬA CODE.

Phạm vi đợt này:
- `Areas/Admin/Views/Admin/Index.cshtml`
- `Areas/Admin/Views/Shared/StoreScopeError.cshtml`
- `Areas/Admin/Views/Shared/_AdminLayout.cshtml`
- `Areas/Admin/Views/Shared/_EmptyState.cshtml`
- `Areas/Admin/Views/Shared/_IdentityLabel.cshtml`
- `Areas/Admin/Views/Shared/_QuantityWithUnit.cshtml`
- `Areas/Admin/Views/Shared/_StatusBadge.cshtml`
- `Areas/Admin/Views/Shared/_ValidationScriptsPartial.cshtml`
- `Areas/Admin/Views/_ViewImports.cshtml`
- `Areas/Admin/Views/_ViewStart.cshtml`
- `wwwroot/css/Admin/admin-unified-depth.css`
- `wwwroot/css/admin-white-orange-forms.css`

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
Bạn đang làm Frontend 1 của CafeChain.

BẮT BUỘC đọc lại:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Báo cáo audit Phase A vừa được duyệt.

PHASE B — CHỈ TRIỂN KHAI ĐỢT `CORE ADMIN — NỀN, SIDEBAR, PAGE SHELL VÀ COMPONENT CONTRACT`.

CHỈ ĐƯỢC CHỈNH:
- `Areas/Admin/Views/Admin/Index.cshtml`
- `Areas/Admin/Views/Shared/StoreScopeError.cshtml`
- `Areas/Admin/Views/Shared/_AdminLayout.cshtml`
- `Areas/Admin/Views/Shared/_EmptyState.cshtml`
- `Areas/Admin/Views/Shared/_IdentityLabel.cshtml`
- `Areas/Admin/Views/Shared/_QuantityWithUnit.cshtml`
- `Areas/Admin/Views/Shared/_StatusBadge.cshtml`
- `Areas/Admin/Views/Shared/_ValidationScriptsPartial.cshtml`
- `Areas/Admin/Views/_ViewImports.cshtml`
- `Areas/Admin/Views/_ViewStart.cshtml`
- `wwwroot/css/Admin/admin-unified-depth.css`
- `wwwroot/css/admin-white-orange-forms.css`

MỤC TIÊU NGHIỆP VỤ VÀ GIAO DIỆN:
- Tạo một nguồn token `--cc-*` duy nhất và alias token cũ thay vì xóa đột ngột.
- Đồng bộ canvas, sidebar 260px, main-content, page padding và typography nền.
- Map toàn bộ họ page header, button, form, table, card/KPI, badge/alert, modal/dropdown về cùng contract.
- Làm nền đủ an toàn để module Frontend 2 kế thừa mà không cần sửa file Core.

QUY CHUẨN THEO TRANG/FORM:
- Admin Index và Shared partial phải dùng cùng surface, text, badge, empty/validation style.
- Page header list/dashboard cao `148–162px`; create/detail `126–140px`.
- Primary/secondary/ghost/danger và input/table/modal phải đủ state hover/focus/disabled.
- Không ép layout đặc thù của Dashboard, calendar, map, BOM hoặc document.

PHẦN PHẢI ĐÓNG BĂNG:
- Collapse IDs, permission conditions và menu order trong `_AdminLayout.cshtml`.
- Mọi script/link/hook trong Shared.
- `_ViewImports`, `_ViewStart`, `_ValidationScriptsPartial` mặc định chỉ đọc; chỉ sửa khi thật sự cần và nhóm trưởng chấp thuận.

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
- Sidebar collapse và active state hoạt động.
- Admin Index, một Product, một Procurement, một HR, một document page không bị selector lan sai.
- Main content không chồng sidebar; không có toàn trang scroll ngang.
- Input/Select2, modal, badge và table vẫn hoạt động.
- StaffHub/POS ngoài Admin không bị ảnh hưởng.

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
Hãy thực hiện PHASE C — INDEPENDENT VERIFICATION cho đợt `CORE ADMIN — NỀN, SIDEBAR, PAGE SHELL VÀ COMPONENT CONTRACT`. Không sửa thêm code trong bước này.

Đọc:
- hai file đặc tả chính trong `docs`;
- `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`;
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
- Sidebar collapse và active state hoạt động.
- Admin Index, một Product, một Procurement, một HR, một document page không bị selector lan sai.
- Main content không chồng sidebar; không có toàn trang scroll ngang.
- Input/Select2, modal, badge và table vẫn hoạt động.
- StaffHub/POS ngoài Admin không bị ảnh hưởng.
9. `dotnet build`, `git diff --check` có pass không.
10. So sánh ảnh trước/sau; chỉ ra trang nào còn lệch với module đã chuẩn hóa.

Trả kết quả dạng bảng: `Tiêu chí | PASS/FAIL | Bằng chứng | Việc cần sửa`.
Kết luận duy nhất:
- `VERIFIED PASS — MAY COMMIT AND PUSH`, hoặc
- `VERIFIED FAIL — MUST FIX BEFORE COMMIT`.
Không sửa code trong Phase C.
```

## Checklist thủ công của frontend

- Sidebar collapse và active state hoạt động.
- Admin Index, một Product, một Procurement, một HR, một document page không bị selector lan sai.
- Main content không chồng sidebar; không có toàn trang scroll ngang.
- Input/Select2, modal, badge và table vẫn hoạt động.
- StaffHub/POS ngoài Admin không bị ảnh hưởng.

Ngoài kiểm thử chức năng, bắt buộc chụp ít nhất:

- Ảnh trước và sau ở `1440×900`.
- Ảnh sau ở `1024×768`.
- Ảnh sau ở `390×844`.
- Một ảnh thể hiện form hoặc modal có validation.
- Một ảnh thể hiện table/list và action.

## Commit và push

Chỉ commit khi Prompt C trả `VERIFIED PASS — MAY COMMIT AND PUSH` và frontend đã tự kiểm tra.

```bash
git add "Areas/Admin/Views/Admin/Index.cshtml"
git add "Areas/Admin/Views/Shared/StoreScopeError.cshtml"
git add "Areas/Admin/Views/Shared/_AdminLayout.cshtml"
git add "Areas/Admin/Views/Shared/_EmptyState.cshtml"
git add "Areas/Admin/Views/Shared/_IdentityLabel.cshtml"
git add "Areas/Admin/Views/Shared/_QuantityWithUnit.cshtml"
git add "Areas/Admin/Views/Shared/_StatusBadge.cshtml"
git add "Areas/Admin/Views/Shared/_ValidationScriptsPartial.cshtml"
git add "Areas/Admin/Views/_ViewImports.cshtml"
git add "Areas/Admin/Views/_ViewStart.cshtml"
git add "wwwroot/css/Admin/admin-unified-depth.css"
git add "wwwroot/css/admin-white-orange-forms.css"
git diff --cached --name-only
git diff --cached --stat
git diff --cached --check
git commit -m "style(admin-core): establish unified CafeChain admin design system"
git push -u origin feature/admin-ui-fe1-core
```

Sau push: gửi nhóm trưởng branch, commit hash, ảnh trước/sau, checklist, build result và mọi exception còn lại. **Không tự merge.**


# ĐỢT 3 — DRINK — INDEX, CREATE, EDIT, TABLE VÀ MODAL

## Điều kiện bắt đầu

Core Admin đã được nhóm trưởng merge vào `develop`.

- Nhóm trưởng đã xác nhận các dependency cần thiết đã merge vào `develop`.
- `git status --short` sạch.
- Branch phải được tạo mới từ `develop` vừa pull.

```bash
git checkout develop
git pull origin develop
git checkout -b feature/admin-ui-fe1-drink
```

**Mức rủi ro:** Cao — có upload/crop/toggle và nhiều modal hook.

## File được phép chỉnh

- `Areas/Admin/Views/AdminDrink/Create.cshtml`
- `Areas/Admin/Views/AdminDrink/Edit.cshtml`
- `Areas/Admin/Views/AdminDrink/Index.cshtml`
- `Areas/Admin/Views/AdminDrink/_DrinkTablePartial.cshtml`
- `wwwroot/css/Admin/Drink/drink.css`
- `wwwroot/css/Admin/AI/ai-image-pipeline.css`

Cấm chỉnh file ngoài danh sách này. File liên quan khác chỉ được đọc để hiểu hook và visual context.

## Kết quả giao diện phải đạt

- Đồng bộ toàn bộ Drink CRUD, không chỉ Index.
- Làm nổi bật tên đồ uống, trạng thái, giá và hành động chính.
- Create/Edit có cùng section, label, input, preview ảnh, validation và footer action.
- Modal và table partial khớp Core Design System.

## Quy chuẩn chi tiết theo loại trang

- Index: header, summary/filter, table, status badge, action 32–36px và empty state.
- Create/Edit: thông tin cơ bản, ảnh, giá, size/topping và trạng thái cùng grid/form contract.
- Delete/toggle/modal hiện có dùng đúng danger/secondary semantics.
- AI image action là secondary/tertiary, không nổi hơn Save.

## Phần phải bảo toàn tuyệt đối

- Upload/crop/preview selectors, hidden input và file input.
- Toggle status, modal IDs, table partial IDs và script blocks.
- Mọi `asp-for`, validation span, option/value và route.

## Prompt A — Audit read-only

Sao chép nguyên khối:

```text
Bạn đang làm Frontend 1 của CafeChain.

BẮT BUỘC đọc đầy đủ, theo thứ tự:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Toàn bộ source của các file được liệt kê cho đợt `DRINK — INDEX, CREATE, EDIT, TABLE VÀ MODAL`.

ĐÂY CHỈ LÀ PHASE A — AUDIT READ-ONLY. TUYỆT ĐỐI CHƯA SỬA CODE.

Phạm vi đợt này:
- `Areas/Admin/Views/AdminDrink/Create.cshtml`
- `Areas/Admin/Views/AdminDrink/Edit.cshtml`
- `Areas/Admin/Views/AdminDrink/Index.cshtml`
- `Areas/Admin/Views/AdminDrink/_DrinkTablePartial.cshtml`
- `wwwroot/css/Admin/Drink/drink.css`
- `wwwroot/css/Admin/AI/ai-image-pipeline.css`

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
Bạn đang làm Frontend 1 của CafeChain.

BẮT BUỘC đọc lại:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Báo cáo audit Phase A vừa được duyệt.

PHASE B — CHỈ TRIỂN KHAI ĐỢT `DRINK — INDEX, CREATE, EDIT, TABLE VÀ MODAL`.

CHỈ ĐƯỢC CHỈNH:
- `Areas/Admin/Views/AdminDrink/Create.cshtml`
- `Areas/Admin/Views/AdminDrink/Edit.cshtml`
- `Areas/Admin/Views/AdminDrink/Index.cshtml`
- `Areas/Admin/Views/AdminDrink/_DrinkTablePartial.cshtml`
- `wwwroot/css/Admin/Drink/drink.css`
- `wwwroot/css/Admin/AI/ai-image-pipeline.css`

MỤC TIÊU NGHIỆP VỤ VÀ GIAO DIỆN:
- Đồng bộ toàn bộ Drink CRUD, không chỉ Index.
- Làm nổi bật tên đồ uống, trạng thái, giá và hành động chính.
- Create/Edit có cùng section, label, input, preview ảnh, validation và footer action.
- Modal và table partial khớp Core Design System.

QUY CHUẨN THEO TRANG/FORM:
- Index: header, summary/filter, table, status badge, action 32–36px và empty state.
- Create/Edit: thông tin cơ bản, ảnh, giá, size/topping và trạng thái cùng grid/form contract.
- Delete/toggle/modal hiện có dùng đúng danger/secondary semantics.
- AI image action là secondary/tertiary, không nổi hơn Save.

PHẦN PHẢI ĐÓNG BĂNG:
- Upload/crop/preview selectors, hidden input và file input.
- Toggle status, modal IDs, table partial IDs và script blocks.
- Mọi `asp-for`, validation span, option/value và route.

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
- Mở Index, lọc/tìm nếu có, pagination và action table.
- Mở Create/Edit; thử dữ liệu hợp lệ và thiếu bắt buộc.
- Upload/preview/crop/toggle ảnh hoạt động như cũ.
- Mở/đóng tất cả modal và kiểm tra footer.
- Desktop/mobile không làm preview hoặc form tràn.

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
Hãy thực hiện PHASE C — INDEPENDENT VERIFICATION cho đợt `DRINK — INDEX, CREATE, EDIT, TABLE VÀ MODAL`. Không sửa thêm code trong bước này.

Đọc:
- hai file đặc tả chính trong `docs`;
- `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`;
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
- Mở Index, lọc/tìm nếu có, pagination và action table.
- Mở Create/Edit; thử dữ liệu hợp lệ và thiếu bắt buộc.
- Upload/preview/crop/toggle ảnh hoạt động như cũ.
- Mở/đóng tất cả modal và kiểm tra footer.
- Desktop/mobile không làm preview hoặc form tràn.
9. `dotnet build`, `git diff --check` có pass không.
10. So sánh ảnh trước/sau; chỉ ra trang nào còn lệch với module đã chuẩn hóa.

Trả kết quả dạng bảng: `Tiêu chí | PASS/FAIL | Bằng chứng | Việc cần sửa`.
Kết luận duy nhất:
- `VERIFIED PASS — MAY COMMIT AND PUSH`, hoặc
- `VERIFIED FAIL — MUST FIX BEFORE COMMIT`.
Không sửa code trong Phase C.
```

## Checklist thủ công của frontend

- Mở Index, lọc/tìm nếu có, pagination và action table.
- Mở Create/Edit; thử dữ liệu hợp lệ và thiếu bắt buộc.
- Upload/preview/crop/toggle ảnh hoạt động như cũ.
- Mở/đóng tất cả modal và kiểm tra footer.
- Desktop/mobile không làm preview hoặc form tràn.

Ngoài kiểm thử chức năng, bắt buộc chụp ít nhất:

- Ảnh trước và sau ở `1440×900`.
- Ảnh sau ở `1024×768`.
- Ảnh sau ở `390×844`.
- Một ảnh thể hiện form hoặc modal có validation.
- Một ảnh thể hiện table/list và action.

## Commit và push

Chỉ commit khi Prompt C trả `VERIFIED PASS — MAY COMMIT AND PUSH` và frontend đã tự kiểm tra.

```bash
git add "Areas/Admin/Views/AdminDrink/Create.cshtml"
git add "Areas/Admin/Views/AdminDrink/Edit.cshtml"
git add "Areas/Admin/Views/AdminDrink/Index.cshtml"
git add "Areas/Admin/Views/AdminDrink/_DrinkTablePartial.cshtml"
git add "wwwroot/css/Admin/Drink/drink.css"
git add "wwwroot/css/Admin/AI/ai-image-pipeline.css"
git diff --cached --name-only
git diff --cached --stat
git diff --cached --check
git commit -m "style(admin-drink): unify drink CRUD UI"
git push -u origin feature/admin-ui-fe1-drink
```

Sau push: gửi nhóm trưởng branch, commit hash, ảnh trước/sau, checklist, build result và mọi exception còn lại. **Không tự merge.**


# ĐỢT 4 — SIZE VÀ TOPPING — CRUD MODAL ĐỒNG BỘ

## Điều kiện bắt đầu

Core đã merge; Drink nên được nhóm trưởng duyệt để làm mẫu Product.

- Nhóm trưởng đã xác nhận các dependency cần thiết đã merge vào `develop`.
- `git status --short` sạch.
- Branch phải được tạo mới từ `develop` vừa pull.

```bash
git checkout develop
git pull origin develop
git checkout -b feature/admin-ui-fe1-size-topping
```

**Mức rủi ro:** Cao — nhiều modal/form trên một trang.

## File được phép chỉnh

- `Areas/Admin/Views/AdminSize/Index.cshtml`
- `Areas/Admin/Views/AdminTopping/Index.cshtml`
- `wwwroot/css/Admin/Size/size.css`
- `wwwroot/css/Admin/Topping/topping.css`

Cấm chỉnh file ngoài danh sách này. File liên quan khác chỉ được đọc để hiểu hook và visual context.

## Kết quả giao diện phải đạt

- Size và Topping phải nhìn như cùng một module Product.
- Đồng bộ table, status, create/edit/delete modal và validation.
- Giữ thao tác nhanh nhưng không biến mọi action thành primary.

## Quy chuẩn chi tiết theo loại trang

- Index: cùng page header, summary/filter/table/pagination với Drink.
- Create/Edit modal: cùng header/body/footer, input 44px, validation và button order.
- Delete confirm dùng danger; close/cancel dùng secondary.

## Phần phải bảo toàn tuyệt đối

- Modal IDs, form IDs, edit data binding, toggle/delete hooks.
- Option/value, `asp-*`, hidden ID và script blocks.

## Prompt A — Audit read-only

Sao chép nguyên khối:

```text
Bạn đang làm Frontend 1 của CafeChain.

BẮT BUỘC đọc đầy đủ, theo thứ tự:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Toàn bộ source của các file được liệt kê cho đợt `SIZE VÀ TOPPING — CRUD MODAL ĐỒNG BỘ`.

ĐÂY CHỈ LÀ PHASE A — AUDIT READ-ONLY. TUYỆT ĐỐI CHƯA SỬA CODE.

Phạm vi đợt này:
- `Areas/Admin/Views/AdminSize/Index.cshtml`
- `Areas/Admin/Views/AdminTopping/Index.cshtml`
- `wwwroot/css/Admin/Size/size.css`
- `wwwroot/css/Admin/Topping/topping.css`

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
Bạn đang làm Frontend 1 của CafeChain.

BẮT BUỘC đọc lại:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Báo cáo audit Phase A vừa được duyệt.

PHASE B — CHỈ TRIỂN KHAI ĐỢT `SIZE VÀ TOPPING — CRUD MODAL ĐỒNG BỘ`.

CHỈ ĐƯỢC CHỈNH:
- `Areas/Admin/Views/AdminSize/Index.cshtml`
- `Areas/Admin/Views/AdminTopping/Index.cshtml`
- `wwwroot/css/Admin/Size/size.css`
- `wwwroot/css/Admin/Topping/topping.css`

MỤC TIÊU NGHIỆP VỤ VÀ GIAO DIỆN:
- Size và Topping phải nhìn như cùng một module Product.
- Đồng bộ table, status, create/edit/delete modal và validation.
- Giữ thao tác nhanh nhưng không biến mọi action thành primary.

QUY CHUẨN THEO TRANG/FORM:
- Index: cùng page header, summary/filter/table/pagination với Drink.
- Create/Edit modal: cùng header/body/footer, input 44px, validation và button order.
- Delete confirm dùng danger; close/cancel dùng secondary.

PHẦN PHẢI ĐÓNG BĂNG:
- Modal IDs, form IDs, edit data binding, toggle/delete hooks.
- Option/value, `asp-*`, hidden ID và script blocks.

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
- Create/Edit/Delete/Toggle Size.
- Create/Edit/Delete/Toggle Topping.
- Validation, modal stacking, backdrop và focus.
- Table responsive và action không wrap sai.

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
Hãy thực hiện PHASE C — INDEPENDENT VERIFICATION cho đợt `SIZE VÀ TOPPING — CRUD MODAL ĐỒNG BỘ`. Không sửa thêm code trong bước này.

Đọc:
- hai file đặc tả chính trong `docs`;
- `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`;
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
- Create/Edit/Delete/Toggle Size.
- Create/Edit/Delete/Toggle Topping.
- Validation, modal stacking, backdrop và focus.
- Table responsive và action không wrap sai.
9. `dotnet build`, `git diff --check` có pass không.
10. So sánh ảnh trước/sau; chỉ ra trang nào còn lệch với module đã chuẩn hóa.

Trả kết quả dạng bảng: `Tiêu chí | PASS/FAIL | Bằng chứng | Việc cần sửa`.
Kết luận duy nhất:
- `VERIFIED PASS — MAY COMMIT AND PUSH`, hoặc
- `VERIFIED FAIL — MUST FIX BEFORE COMMIT`.
Không sửa code trong Phase C.
```

## Checklist thủ công của frontend

- Create/Edit/Delete/Toggle Size.
- Create/Edit/Delete/Toggle Topping.
- Validation, modal stacking, backdrop và focus.
- Table responsive và action không wrap sai.

Ngoài kiểm thử chức năng, bắt buộc chụp ít nhất:

- Ảnh trước và sau ở `1440×900`.
- Ảnh sau ở `1024×768`.
- Ảnh sau ở `390×844`.
- Một ảnh thể hiện form hoặc modal có validation.
- Một ảnh thể hiện table/list và action.

## Commit và push

Chỉ commit khi Prompt C trả `VERIFIED PASS — MAY COMMIT AND PUSH` và frontend đã tự kiểm tra.

```bash
git add "Areas/Admin/Views/AdminSize/Index.cshtml"
git add "Areas/Admin/Views/AdminTopping/Index.cshtml"
git add "wwwroot/css/Admin/Size/size.css"
git add "wwwroot/css/Admin/Topping/topping.css"
git diff --cached --name-only
git diff --cached --stat
git diff --cached --check
git commit -m "style(admin-product-options): unify size and topping CRUD UI"
git push -u origin feature/admin-ui-fe1-size-topping
```

Sau push: gửi nhóm trưởng branch, commit hash, ảnh trước/sau, checklist, build result và mọi exception còn lại. **Không tự merge.**


# ĐỢT 5 — STORE MENU VÀ DRINK PROFITABILITY

## Điều kiện bắt đầu

Core đã merge; Product visual mẫu đã được duyệt.

- Nhóm trưởng đã xác nhận các dependency cần thiết đã merge vào `develop`.
- `git status --short` sạch.
- Branch phải được tạo mới từ `develop` vừa pull.

```bash
git checkout develop
git pull origin develop
git checkout -b feature/admin-ui-fe1-storemenu-profitability
```

**Mức rủi ro:** Trung bình — dữ liệu dày và numeric.

## File được phép chỉnh

- `Areas/Admin/Views/AdminStoreMenu/Index.cshtml`
- `Areas/Admin/Views/AdminDrinkProfitability/Index.cshtml`
- `wwwroot/css/Admin/StoreMenu/store-menu.css`
- `wwwroot/css/Admin/Profitability/drink-profitability.css`

Cấm chỉnh file ngoài danh sách này. File liên quan khác chỉ được đọc để hiểu hook và visual context.

## Kết quả giao diện phải đạt

- Store Menu và Profitability cùng visual contract với Drink/Size/Topping.
- Giá, vốn, lợi nhuận, biên lợi nhuận và trạng thái dễ quét.
- Numeric căn phải và dùng tabular nums.

## Quy chuẩn chi tiết theo loại trang

- Store Menu: store selector/filter, table và save/toggle action rõ.
- Profitability: KPI/summary, table dữ liệu, cảnh báo BOM hoặc margin đúng semantic.
- Không dùng màu đỏ cho mọi số âm/thiếu ngữ cảnh.

## Phần phải bảo toàn tuyệt đối

- Store/menu binding, selected item IDs, save/toggle form.
- Công thức/số liệu và trạng thái profitability.
- Script/filter hook.

## Prompt A — Audit read-only

Sao chép nguyên khối:

```text
Bạn đang làm Frontend 1 của CafeChain.

BẮT BUỘC đọc đầy đủ, theo thứ tự:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Toàn bộ source của các file được liệt kê cho đợt `STORE MENU VÀ DRINK PROFITABILITY`.

ĐÂY CHỈ LÀ PHASE A — AUDIT READ-ONLY. TUYỆT ĐỐI CHƯA SỬA CODE.

Phạm vi đợt này:
- `Areas/Admin/Views/AdminStoreMenu/Index.cshtml`
- `Areas/Admin/Views/AdminDrinkProfitability/Index.cshtml`
- `wwwroot/css/Admin/StoreMenu/store-menu.css`
- `wwwroot/css/Admin/Profitability/drink-profitability.css`

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
Bạn đang làm Frontend 1 của CafeChain.

BẮT BUỘC đọc lại:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Báo cáo audit Phase A vừa được duyệt.

PHASE B — CHỈ TRIỂN KHAI ĐỢT `STORE MENU VÀ DRINK PROFITABILITY`.

CHỈ ĐƯỢC CHỈNH:
- `Areas/Admin/Views/AdminStoreMenu/Index.cshtml`
- `Areas/Admin/Views/AdminDrinkProfitability/Index.cshtml`
- `wwwroot/css/Admin/StoreMenu/store-menu.css`
- `wwwroot/css/Admin/Profitability/drink-profitability.css`

MỤC TIÊU NGHIỆP VỤ VÀ GIAO DIỆN:
- Store Menu và Profitability cùng visual contract với Drink/Size/Topping.
- Giá, vốn, lợi nhuận, biên lợi nhuận và trạng thái dễ quét.
- Numeric căn phải và dùng tabular nums.

QUY CHUẨN THEO TRANG/FORM:
- Store Menu: store selector/filter, table và save/toggle action rõ.
- Profitability: KPI/summary, table dữ liệu, cảnh báo BOM hoặc margin đúng semantic.
- Không dùng màu đỏ cho mọi số âm/thiếu ngữ cảnh.

PHẦN PHẢI ĐÓNG BĂNG:
- Store/menu binding, selected item IDs, save/toggle form.
- Công thức/số liệu và trạng thái profitability.
- Script/filter hook.

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
- Đổi cửa hàng/filter và kiểm tra dữ liệu.
- Save/toggle Store Menu.
- Profitability table, KPI và trạng thái BOM.
- Responsive numeric table.

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
Hãy thực hiện PHASE C — INDEPENDENT VERIFICATION cho đợt `STORE MENU VÀ DRINK PROFITABILITY`. Không sửa thêm code trong bước này.

Đọc:
- hai file đặc tả chính trong `docs`;
- `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`;
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
- Đổi cửa hàng/filter và kiểm tra dữ liệu.
- Save/toggle Store Menu.
- Profitability table, KPI và trạng thái BOM.
- Responsive numeric table.
9. `dotnet build`, `git diff --check` có pass không.
10. So sánh ảnh trước/sau; chỉ ra trang nào còn lệch với module đã chuẩn hóa.

Trả kết quả dạng bảng: `Tiêu chí | PASS/FAIL | Bằng chứng | Việc cần sửa`.
Kết luận duy nhất:
- `VERIFIED PASS — MAY COMMIT AND PUSH`, hoặc
- `VERIFIED FAIL — MUST FIX BEFORE COMMIT`.
Không sửa code trong Phase C.
```

## Checklist thủ công của frontend

- Đổi cửa hàng/filter và kiểm tra dữ liệu.
- Save/toggle Store Menu.
- Profitability table, KPI và trạng thái BOM.
- Responsive numeric table.

Ngoài kiểm thử chức năng, bắt buộc chụp ít nhất:

- Ảnh trước và sau ở `1440×900`.
- Ảnh sau ở `1024×768`.
- Ảnh sau ở `390×844`.
- Một ảnh thể hiện form hoặc modal có validation.
- Một ảnh thể hiện table/list và action.

## Commit và push

Chỉ commit khi Prompt C trả `VERIFIED PASS — MAY COMMIT AND PUSH` và frontend đã tự kiểm tra.

```bash
git add "Areas/Admin/Views/AdminStoreMenu/Index.cshtml"
git add "Areas/Admin/Views/AdminDrinkProfitability/Index.cshtml"
git add "wwwroot/css/Admin/StoreMenu/store-menu.css"
git add "wwwroot/css/Admin/Profitability/drink-profitability.css"
git diff --cached --name-only
git diff --cached --stat
git diff --cached --check
git commit -m "style(admin-product-insight): unify store menu and profitability UI"
git push -u origin feature/admin-ui-fe1-storemenu-profitability
```

Sau push: gửi nhóm trưởng branch, commit hash, ảnh trước/sau, checklist, build result và mọi exception còn lại. **Không tự merge.**


# ĐỢT 6 — RECIPE CORE — INDEX, CREATE VÀ EDIT

## Điều kiện bắt đầu

Core và Product đã ổn định.

- Nhóm trưởng đã xác nhận các dependency cần thiết đã merge vào `develop`.
- `git status --short` sạch.
- Branch phải được tạo mới từ `develop` vừa pull.

```bash
git checkout develop
git pull origin develop
git checkout -b feature/admin-ui-fe1-recipe-core
```

**Mức rủi ro:** Rất cao — dynamic ingredient rows và script riêng.

## File được phép chỉnh

- `Areas/Admin/Views/AdminRecipe/Index.cshtml`
- `Areas/Admin/Views/AdminRecipe/Create.cshtml`
- `Areas/Admin/Views/AdminRecipe/Edit.cshtml`
- `wwwroot/css/recipe-builder.css`

Cấm chỉnh file ngoài danh sách này. File liên quan khác chỉ được đọc để hiểu hook và visual context.

## Kết quả giao diện phải đạt

- Recipe CRUD phải ưu tiên độ chính xác dữ liệu hơn trang trí.
- Index và Create/Edit dùng cùng header, button, form, table và badge.
- Ingredient rows thẳng cột; số lượng/đơn vị rõ; action thêm/xóa dòng không lấn Save.

## Quy chuẩn chi tiết theo loại trang

- Index: trạng thái BOM, search/filter, table/action rõ.
- Create/Edit: section thông tin công thức và line-item builder; numeric căn phải.
- Action bar Save/Back rõ; delete row là icon/danger nhẹ theo ngữ cảnh.

## Phần phải bảo toàn tuyệt đối

- Dynamic row template, index/name binding, hidden input và add/remove hooks.
- Select2, unit conversion, versioning và submit logic.
- Mọi script trong Recipe view.

## Prompt A — Audit read-only

Sao chép nguyên khối:

```text
Bạn đang làm Frontend 1 của CafeChain.

BẮT BUỘC đọc đầy đủ, theo thứ tự:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Toàn bộ source của các file được liệt kê cho đợt `RECIPE CORE — INDEX, CREATE VÀ EDIT`.

ĐÂY CHỈ LÀ PHASE A — AUDIT READ-ONLY. TUYỆT ĐỐI CHƯA SỬA CODE.

Phạm vi đợt này:
- `Areas/Admin/Views/AdminRecipe/Index.cshtml`
- `Areas/Admin/Views/AdminRecipe/Create.cshtml`
- `Areas/Admin/Views/AdminRecipe/Edit.cshtml`
- `wwwroot/css/recipe-builder.css`

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
Bạn đang làm Frontend 1 của CafeChain.

BẮT BUỘC đọc lại:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Báo cáo audit Phase A vừa được duyệt.

PHASE B — CHỈ TRIỂN KHAI ĐỢT `RECIPE CORE — INDEX, CREATE VÀ EDIT`.

CHỈ ĐƯỢC CHỈNH:
- `Areas/Admin/Views/AdminRecipe/Index.cshtml`
- `Areas/Admin/Views/AdminRecipe/Create.cshtml`
- `Areas/Admin/Views/AdminRecipe/Edit.cshtml`
- `wwwroot/css/recipe-builder.css`

MỤC TIÊU NGHIỆP VỤ VÀ GIAO DIỆN:
- Recipe CRUD phải ưu tiên độ chính xác dữ liệu hơn trang trí.
- Index và Create/Edit dùng cùng header, button, form, table và badge.
- Ingredient rows thẳng cột; số lượng/đơn vị rõ; action thêm/xóa dòng không lấn Save.

QUY CHUẨN THEO TRANG/FORM:
- Index: trạng thái BOM, search/filter, table/action rõ.
- Create/Edit: section thông tin công thức và line-item builder; numeric căn phải.
- Action bar Save/Back rõ; delete row là icon/danger nhẹ theo ngữ cảnh.

PHẦN PHẢI ĐÓNG BĂNG:
- Dynamic row template, index/name binding, hidden input và add/remove hooks.
- Select2, unit conversion, versioning và submit logic.
- Mọi script trong Recipe view.

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
- Create recipe với nhiều ingredient row.
- Thêm/xóa/sửa dòng và validation.
- Edit recipe, save và quay lại.
- Select2/unit/quantity không lệch.
- Mobile/tablet line-item cuộn trong vùng, không toàn trang.

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
Hãy thực hiện PHASE C — INDEPENDENT VERIFICATION cho đợt `RECIPE CORE — INDEX, CREATE VÀ EDIT`. Không sửa thêm code trong bước này.

Đọc:
- hai file đặc tả chính trong `docs`;
- `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`;
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
- Create recipe với nhiều ingredient row.
- Thêm/xóa/sửa dòng và validation.
- Edit recipe, save và quay lại.
- Select2/unit/quantity không lệch.
- Mobile/tablet line-item cuộn trong vùng, không toàn trang.
9. `dotnet build`, `git diff --check` có pass không.
10. So sánh ảnh trước/sau; chỉ ra trang nào còn lệch với module đã chuẩn hóa.

Trả kết quả dạng bảng: `Tiêu chí | PASS/FAIL | Bằng chứng | Việc cần sửa`.
Kết luận duy nhất:
- `VERIFIED PASS — MAY COMMIT AND PUSH`, hoặc
- `VERIFIED FAIL — MUST FIX BEFORE COMMIT`.
Không sửa code trong Phase C.
```

## Checklist thủ công của frontend

- Create recipe với nhiều ingredient row.
- Thêm/xóa/sửa dòng và validation.
- Edit recipe, save và quay lại.
- Select2/unit/quantity không lệch.
- Mobile/tablet line-item cuộn trong vùng, không toàn trang.

Ngoài kiểm thử chức năng, bắt buộc chụp ít nhất:

- Ảnh trước và sau ở `1440×900`.
- Ảnh sau ở `1024×768`.
- Ảnh sau ở `390×844`.
- Một ảnh thể hiện form hoặc modal có validation.
- Một ảnh thể hiện table/list và action.

## Commit và push

Chỉ commit khi Prompt C trả `VERIFIED PASS — MAY COMMIT AND PUSH` và frontend đã tự kiểm tra.

```bash
git add "Areas/Admin/Views/AdminRecipe/Index.cshtml"
git add "Areas/Admin/Views/AdminRecipe/Create.cshtml"
git add "Areas/Admin/Views/AdminRecipe/Edit.cshtml"
git add "wwwroot/css/recipe-builder.css"
git diff --cached --name-only
git diff --cached --stat
git diff --cached --check
git commit -m "style(admin-recipe): unify recipe CRUD UI"
git push -u origin feature/admin-ui-fe1-recipe-core
```

Sau push: gửi nhóm trưởng branch, commit hash, ảnh trước/sau, checklist, build result và mọi exception còn lại. **Không tự merge.**


# ĐỢT 7 — RECIPE DATA HEALTH, VISUALIZE VÀ BOM TREE

## Điều kiện bắt đầu

Recipe Core đã được nhóm trưởng merge.

- Nhóm trưởng đã xác nhận các dependency cần thiết đã merge vào `develop`.
- `git status --short` sạch.
- Branch phải được tạo mới từ `develop` vừa pull.

```bash
git checkout develop
git pull origin develop
git checkout -b feature/admin-ui-fe1-bom-health
```

**Mức rủi ro:** Cao — tree/partial và dữ liệu trạng thái.

## File được phép chỉnh

- `Areas/Admin/Views/AdminRecipe/DataHealth.cshtml`
- `Areas/Admin/Views/AdminRecipe/Visualize.cshtml`
- `Areas/Admin/Views/AdminRecipe/Partials/_BomTree.cshtml`
- `Areas/Admin/Views/AdminRecipe/Partials/_BomTreeNode.cshtml`
- `wwwroot/css/recipe-builder.css`

Cấm chỉnh file ngoài danh sách này. File liên quan khác chỉ được đọc để hiểu hook và visual context.

## Kết quả giao diện phải đạt

- Data Health, Visualize và BOM Tree cùng ngôn ngữ với Recipe CRUD.
- Lỗi, cảnh báo, thiếu dữ liệu và trạng thái tốt có semantic rõ.
- Tree dễ đọc theo cấp, không dùng shadow/gradient nặng.

## Quy chuẩn chi tiết theo loại trang

- Data Health: summary + filter + issue table/callout.
- Visualize: page header, legend và canvas/tree panel rõ.
- Tree node: indentation, connector, badge và numeric rõ; trạng thái không chỉ bằng màu.

## Phần phải bảo toàn tuyệt đối

- Partial recursion, node IDs, expand/collapse và data attributes.
- Dữ liệu health/status mapping và script visualize.

## Prompt A — Audit read-only

Sao chép nguyên khối:

```text
Bạn đang làm Frontend 1 của CafeChain.

BẮT BUỘC đọc đầy đủ, theo thứ tự:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Toàn bộ source của các file được liệt kê cho đợt `RECIPE DATA HEALTH, VISUALIZE VÀ BOM TREE`.

ĐÂY CHỈ LÀ PHASE A — AUDIT READ-ONLY. TUYỆT ĐỐI CHƯA SỬA CODE.

Phạm vi đợt này:
- `Areas/Admin/Views/AdminRecipe/DataHealth.cshtml`
- `Areas/Admin/Views/AdminRecipe/Visualize.cshtml`
- `Areas/Admin/Views/AdminRecipe/Partials/_BomTree.cshtml`
- `Areas/Admin/Views/AdminRecipe/Partials/_BomTreeNode.cshtml`
- `wwwroot/css/recipe-builder.css`

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
Bạn đang làm Frontend 1 của CafeChain.

BẮT BUỘC đọc lại:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Báo cáo audit Phase A vừa được duyệt.

PHASE B — CHỈ TRIỂN KHAI ĐỢT `RECIPE DATA HEALTH, VISUALIZE VÀ BOM TREE`.

CHỈ ĐƯỢC CHỈNH:
- `Areas/Admin/Views/AdminRecipe/DataHealth.cshtml`
- `Areas/Admin/Views/AdminRecipe/Visualize.cshtml`
- `Areas/Admin/Views/AdminRecipe/Partials/_BomTree.cshtml`
- `Areas/Admin/Views/AdminRecipe/Partials/_BomTreeNode.cshtml`
- `wwwroot/css/recipe-builder.css`

MỤC TIÊU NGHIỆP VỤ VÀ GIAO DIỆN:
- Data Health, Visualize và BOM Tree cùng ngôn ngữ với Recipe CRUD.
- Lỗi, cảnh báo, thiếu dữ liệu và trạng thái tốt có semantic rõ.
- Tree dễ đọc theo cấp, không dùng shadow/gradient nặng.

QUY CHUẨN THEO TRANG/FORM:
- Data Health: summary + filter + issue table/callout.
- Visualize: page header, legend và canvas/tree panel rõ.
- Tree node: indentation, connector, badge và numeric rõ; trạng thái không chỉ bằng màu.

PHẦN PHẢI ĐÓNG BĂNG:
- Partial recursion, node IDs, expand/collapse và data attributes.
- Dữ liệu health/status mapping và script visualize.

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
- Mở DataHealth với dữ liệu đủ/thiếu nếu có.
- Mở Visualize và thao tác tree.
- Expand/collapse/selection hoạt động.
- Tree không tràn ngoài panel ở tablet/mobile.

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
Hãy thực hiện PHASE C — INDEPENDENT VERIFICATION cho đợt `RECIPE DATA HEALTH, VISUALIZE VÀ BOM TREE`. Không sửa thêm code trong bước này.

Đọc:
- hai file đặc tả chính trong `docs`;
- `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`;
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
- Mở DataHealth với dữ liệu đủ/thiếu nếu có.
- Mở Visualize và thao tác tree.
- Expand/collapse/selection hoạt động.
- Tree không tràn ngoài panel ở tablet/mobile.
9. `dotnet build`, `git diff --check` có pass không.
10. So sánh ảnh trước/sau; chỉ ra trang nào còn lệch với module đã chuẩn hóa.

Trả kết quả dạng bảng: `Tiêu chí | PASS/FAIL | Bằng chứng | Việc cần sửa`.
Kết luận duy nhất:
- `VERIFIED PASS — MAY COMMIT AND PUSH`, hoặc
- `VERIFIED FAIL — MUST FIX BEFORE COMMIT`.
Không sửa code trong Phase C.
```

## Checklist thủ công của frontend

- Mở DataHealth với dữ liệu đủ/thiếu nếu có.
- Mở Visualize và thao tác tree.
- Expand/collapse/selection hoạt động.
- Tree không tràn ngoài panel ở tablet/mobile.

Ngoài kiểm thử chức năng, bắt buộc chụp ít nhất:

- Ảnh trước và sau ở `1440×900`.
- Ảnh sau ở `1024×768`.
- Ảnh sau ở `390×844`.
- Một ảnh thể hiện form hoặc modal có validation.
- Một ảnh thể hiện table/list và action.

## Commit và push

Chỉ commit khi Prompt C trả `VERIFIED PASS — MAY COMMIT AND PUSH` và frontend đã tự kiểm tra.

```bash
git add "Areas/Admin/Views/AdminRecipe/DataHealth.cshtml"
git add "Areas/Admin/Views/AdminRecipe/Visualize.cshtml"
git add "Areas/Admin/Views/AdminRecipe/Partials/_BomTree.cshtml"
git add "Areas/Admin/Views/AdminRecipe/Partials/_BomTreeNode.cshtml"
git add "wwwroot/css/recipe-builder.css"
git diff --cached --name-only
git diff --cached --stat
git diff --cached --check
git commit -m "style(admin-bom): unify BOM health and visualization UI"
git push -u origin feature/admin-ui-fe1-bom-health
```

Sau push: gửi nhóm trưởng branch, commit hash, ảnh trước/sau, checklist, build result và mọi exception còn lại. **Không tự merge.**


# ĐỢT 8 — PREPARED ITEM VÀ PRODUCTION ORDER

## Điều kiện bắt đầu

Recipe/BOM đã merge.

- Nhóm trưởng đã xác nhận các dependency cần thiết đã merge vào `develop`.
- `git status --short` sạch.
- Branch phải được tạo mới từ `develop` vừa pull.

```bash
git checkout develop
git pull origin develop
git checkout -b feature/admin-ui-fe1-production
```

**Mức rủi ro:** Cao — form/modal và bảng nguyên liệu.

## File được phép chỉnh

- `Areas/Admin/Views/AdminPreparedItem/Index.cshtml`
- `Areas/Admin/Views/AdminProductionOrder/Create.cshtml`
- `wwwroot/css/Admin/ProductionOrder/production-order.css`
- `wwwroot/css/recipe-builder.css`

Cấm chỉnh file ngoài danh sách này. File liên quan khác chỉ được đọc để hiểu hook và visual context.

## Kết quả giao diện phải đạt

- Prepared Item và Production Order nối tiếp trực quan với Recipe/BOM.
- Modal CRUD Prepared Item và form tạo lệnh sơ chế dùng chung contract.
- Số lượng, đơn vị, tồn và nguyên liệu dễ đọc.

## Quy chuẩn chi tiết theo loại trang

- Prepared Item: Index + modal create/edit/delete, table/status/action.
- Production Order: header, thông tin lệnh, line items, summary và action footer.
- Primary chỉ Save/Create/Apply; action phụ giảm nhấn.

## Phần phải bảo toàn tuyệt đối

- PreparedItem modal hooks và form binding.
- ProductionOrder dynamic inputs, recipe selection, quantity và submit.

## Prompt A — Audit read-only

Sao chép nguyên khối:

```text
Bạn đang làm Frontend 1 của CafeChain.

BẮT BUỘC đọc đầy đủ, theo thứ tự:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Toàn bộ source của các file được liệt kê cho đợt `PREPARED ITEM VÀ PRODUCTION ORDER`.

ĐÂY CHỈ LÀ PHASE A — AUDIT READ-ONLY. TUYỆT ĐỐI CHƯA SỬA CODE.

Phạm vi đợt này:
- `Areas/Admin/Views/AdminPreparedItem/Index.cshtml`
- `Areas/Admin/Views/AdminProductionOrder/Create.cshtml`
- `wwwroot/css/Admin/ProductionOrder/production-order.css`
- `wwwroot/css/recipe-builder.css`

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
Bạn đang làm Frontend 1 của CafeChain.

BẮT BUỘC đọc lại:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Báo cáo audit Phase A vừa được duyệt.

PHASE B — CHỈ TRIỂN KHAI ĐỢT `PREPARED ITEM VÀ PRODUCTION ORDER`.

CHỈ ĐƯỢC CHỈNH:
- `Areas/Admin/Views/AdminPreparedItem/Index.cshtml`
- `Areas/Admin/Views/AdminProductionOrder/Create.cshtml`
- `wwwroot/css/Admin/ProductionOrder/production-order.css`
- `wwwroot/css/recipe-builder.css`

MỤC TIÊU NGHIỆP VỤ VÀ GIAO DIỆN:
- Prepared Item và Production Order nối tiếp trực quan với Recipe/BOM.
- Modal CRUD Prepared Item và form tạo lệnh sơ chế dùng chung contract.
- Số lượng, đơn vị, tồn và nguyên liệu dễ đọc.

QUY CHUẨN THEO TRANG/FORM:
- Prepared Item: Index + modal create/edit/delete, table/status/action.
- Production Order: header, thông tin lệnh, line items, summary và action footer.
- Primary chỉ Save/Create/Apply; action phụ giảm nhấn.

PHẦN PHẢI ĐÓNG BĂNG:
- PreparedItem modal hooks và form binding.
- ProductionOrder dynamic inputs, recipe selection, quantity và submit.

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
- PreparedItem create/edit/delete/modal.
- ProductionOrder chọn công thức, số lượng, dòng nguyên liệu và submit validation.
- Numeric alignment và responsive.

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
Hãy thực hiện PHASE C — INDEPENDENT VERIFICATION cho đợt `PREPARED ITEM VÀ PRODUCTION ORDER`. Không sửa thêm code trong bước này.

Đọc:
- hai file đặc tả chính trong `docs`;
- `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`;
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
- PreparedItem create/edit/delete/modal.
- ProductionOrder chọn công thức, số lượng, dòng nguyên liệu và submit validation.
- Numeric alignment và responsive.
9. `dotnet build`, `git diff --check` có pass không.
10. So sánh ảnh trước/sau; chỉ ra trang nào còn lệch với module đã chuẩn hóa.

Trả kết quả dạng bảng: `Tiêu chí | PASS/FAIL | Bằng chứng | Việc cần sửa`.
Kết luận duy nhất:
- `VERIFIED PASS — MAY COMMIT AND PUSH`, hoặc
- `VERIFIED FAIL — MUST FIX BEFORE COMMIT`.
Không sửa code trong Phase C.
```

## Checklist thủ công của frontend

- PreparedItem create/edit/delete/modal.
- ProductionOrder chọn công thức, số lượng, dòng nguyên liệu và submit validation.
- Numeric alignment và responsive.

Ngoài kiểm thử chức năng, bắt buộc chụp ít nhất:

- Ảnh trước và sau ở `1440×900`.
- Ảnh sau ở `1024×768`.
- Ảnh sau ở `390×844`.
- Một ảnh thể hiện form hoặc modal có validation.
- Một ảnh thể hiện table/list và action.

## Commit và push

Chỉ commit khi Prompt C trả `VERIFIED PASS — MAY COMMIT AND PUSH` và frontend đã tự kiểm tra.

```bash
git add "Areas/Admin/Views/AdminPreparedItem/Index.cshtml"
git add "Areas/Admin/Views/AdminProductionOrder/Create.cshtml"
git add "wwwroot/css/Admin/ProductionOrder/production-order.css"
git add "wwwroot/css/recipe-builder.css"
git diff --cached --name-only
git diff --cached --stat
git diff --cached --check
git commit -m "style(admin-production): unify prepared item and production order UI"
git push -u origin feature/admin-ui-fe1-production
```

Sau push: gửi nhóm trưởng branch, commit hash, ảnh trước/sau, checklist, build result và mọi exception còn lại. **Không tự merge.**


# ĐỢT 9 — INVENTORY DOCUMENT — INDEX, CREATE, DETAIL VÀ PARTIAL

## Điều kiện bắt đầu

Core đã ổn định; nên làm sau Product/BOM để tránh sửa nền muộn.

- Nhóm trưởng đã xác nhận các dependency cần thiết đã merge vào `develop`.
- `git status --short` sạch.
- Branch phải được tạo mới từ `develop` vừa pull.

```bash
git checkout develop
git pull origin develop
git checkout -b feature/admin-ui-fe1-inventory-document
```

**Mức rủi ro:** Rất cao — 15 view/partial, modal và dynamic rows.

## File được phép chỉnh

- `Areas/Admin/Views/AdminInventoryDocument/Index.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_ActionBar.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_CreateModal.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_DetailTable.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_DocumentInfo.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_IngredientRow.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_Summary.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DashboardCards.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DetailDocument.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DetailGeneral.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DetailModal.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DocumentTable.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DocumentTabs.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_FilterSection.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/_Pagination.cshtml`
- `wwwroot/css/Admin/InventoryDocument/inventorydocument.css`
- `wwwroot/css/Admin/InventoryDocument/inventorydocumentcreate.css`

Cấm chỉnh file ngoài danh sách này. File liên quan khác chỉ được đọc để hiểu hook và visual context.

## Kết quả giao diện phải đạt

- Toàn bộ chứng từ kho phải đồng bộ từ Index → Create → Detail → modal.
- Document header, trạng thái, line items, totals và action bar có hierarchy rõ.
- Các partial ghép lại phải trông như một trang thống nhất, không mỗi partial một style.

## Quy chuẩn chi tiết theo loại trang

- Index: filter, summary cards, document table, pagination và empty state.
- Create: document info, ingredient table, summary, modal và action bar cùng contract.
- Detail: tabs, general info, line table, dashboard cards, status/action rõ.
- Numeric/quantity/value căn phải; table compact và scroll trong wrapper.

## Phần phải bảo toàn tuyệt đối

- Partial host, modal IDs, dynamic row IDs/index, hidden inputs và pagination params.
- Tab IDs, filters, document status/action forms.
- Không di chuyển partial render hoặc đổi modal host.

## Prompt A — Audit read-only

Sao chép nguyên khối:

```text
Bạn đang làm Frontend 1 của CafeChain.

BẮT BUỘC đọc đầy đủ, theo thứ tự:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Toàn bộ source của các file được liệt kê cho đợt `INVENTORY DOCUMENT — INDEX, CREATE, DETAIL VÀ PARTIAL`.

ĐÂY CHỈ LÀ PHASE A — AUDIT READ-ONLY. TUYỆT ĐỐI CHƯA SỬA CODE.

Phạm vi đợt này:
- `Areas/Admin/Views/AdminInventoryDocument/Index.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_ActionBar.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_CreateModal.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_DetailTable.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_DocumentInfo.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_IngredientRow.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_Summary.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DashboardCards.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DetailDocument.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DetailGeneral.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DetailModal.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DocumentTable.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DocumentTabs.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_FilterSection.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/_Pagination.cshtml`
- `wwwroot/css/Admin/InventoryDocument/inventorydocument.css`
- `wwwroot/css/Admin/InventoryDocument/inventorydocumentcreate.css`

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
Bạn đang làm Frontend 1 của CafeChain.

BẮT BUỘC đọc lại:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Báo cáo audit Phase A vừa được duyệt.

PHASE B — CHỈ TRIỂN KHAI ĐỢT `INVENTORY DOCUMENT — INDEX, CREATE, DETAIL VÀ PARTIAL`.

CHỈ ĐƯỢC CHỈNH:
- `Areas/Admin/Views/AdminInventoryDocument/Index.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_ActionBar.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_CreateModal.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_DetailTable.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_DocumentInfo.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_IngredientRow.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_Summary.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DashboardCards.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DetailDocument.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DetailGeneral.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DetailModal.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DocumentTable.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DocumentTabs.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_FilterSection.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/_Pagination.cshtml`
- `wwwroot/css/Admin/InventoryDocument/inventorydocument.css`
- `wwwroot/css/Admin/InventoryDocument/inventorydocumentcreate.css`

MỤC TIÊU NGHIỆP VỤ VÀ GIAO DIỆN:
- Toàn bộ chứng từ kho phải đồng bộ từ Index → Create → Detail → modal.
- Document header, trạng thái, line items, totals và action bar có hierarchy rõ.
- Các partial ghép lại phải trông như một trang thống nhất, không mỗi partial một style.

QUY CHUẨN THEO TRANG/FORM:
- Index: filter, summary cards, document table, pagination và empty state.
- Create: document info, ingredient table, summary, modal và action bar cùng contract.
- Detail: tabs, general info, line table, dashboard cards, status/action rõ.
- Numeric/quantity/value căn phải; table compact và scroll trong wrapper.

PHẦN PHẢI ĐÓNG BĂNG:
- Partial host, modal IDs, dynamic row IDs/index, hidden inputs và pagination params.
- Tab IDs, filters, document status/action forms.
- Không di chuyển partial render hoặc đổi modal host.

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
- Index filter, pagination, mở detail/modal.
- Create: thêm/xóa ingredient row, validation, summary và submit.
- Detail tabs, status/action và bảng.
- Modal body/footer và table responsive.
- Kiểm tra mọi partial không bị double padding/border.

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
Hãy thực hiện PHASE C — INDEPENDENT VERIFICATION cho đợt `INVENTORY DOCUMENT — INDEX, CREATE, DETAIL VÀ PARTIAL`. Không sửa thêm code trong bước này.

Đọc:
- hai file đặc tả chính trong `docs`;
- `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`;
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
- Index filter, pagination, mở detail/modal.
- Create: thêm/xóa ingredient row, validation, summary và submit.
- Detail tabs, status/action và bảng.
- Modal body/footer và table responsive.
- Kiểm tra mọi partial không bị double padding/border.
9. `dotnet build`, `git diff --check` có pass không.
10. So sánh ảnh trước/sau; chỉ ra trang nào còn lệch với module đã chuẩn hóa.

Trả kết quả dạng bảng: `Tiêu chí | PASS/FAIL | Bằng chứng | Việc cần sửa`.
Kết luận duy nhất:
- `VERIFIED PASS — MAY COMMIT AND PUSH`, hoặc
- `VERIFIED FAIL — MUST FIX BEFORE COMMIT`.
Không sửa code trong Phase C.
```

## Checklist thủ công của frontend

- Index filter, pagination, mở detail/modal.
- Create: thêm/xóa ingredient row, validation, summary và submit.
- Detail tabs, status/action và bảng.
- Modal body/footer và table responsive.
- Kiểm tra mọi partial không bị double padding/border.

Ngoài kiểm thử chức năng, bắt buộc chụp ít nhất:

- Ảnh trước và sau ở `1440×900`.
- Ảnh sau ở `1024×768`.
- Ảnh sau ở `390×844`.
- Một ảnh thể hiện form hoặc modal có validation.
- Một ảnh thể hiện table/list và action.

## Commit và push

Chỉ commit khi Prompt C trả `VERIFIED PASS — MAY COMMIT AND PUSH` và frontend đã tự kiểm tra.

```bash
git add "Areas/Admin/Views/AdminInventoryDocument/Index.cshtml"
git add "Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_ActionBar.cshtml"
git add "Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_CreateModal.cshtml"
git add "Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_DetailTable.cshtml"
git add "Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_DocumentInfo.cshtml"
git add "Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_IngredientRow.cshtml"
git add "Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_Summary.cshtml"
git add "Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DashboardCards.cshtml"
git add "Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DetailDocument.cshtml"
git add "Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DetailGeneral.cshtml"
git add "Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DetailModal.cshtml"
git add "Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DocumentTable.cshtml"
git add "Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DocumentTabs.cshtml"
git add "Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_FilterSection.cshtml"
git add "Areas/Admin/Views/AdminInventoryDocument/_Pagination.cshtml"
git add "wwwroot/css/Admin/InventoryDocument/inventorydocument.css"
git add "wwwroot/css/Admin/InventoryDocument/inventorydocumentcreate.css"
git diff --cached --name-only
git diff --cached --stat
git diff --cached --check
git commit -m "style(admin-inventory-document): unify inventory document workflow UI"
git push -u origin feature/admin-ui-fe1-inventory-document
```

Sau push: gửi nhóm trưởng branch, commit hash, ảnh trước/sau, checklist, build result và mọi exception còn lại. **Không tự merge.**


# ĐỢT 10 — INVENTORY TRANSFER — INDEX, CREATE, DETAIL, TIMELINE VÀ RESOLUTION

## Điều kiện bắt đầu

Inventory Document nên được nhóm trưởng duyệt để thống nhất document contract.

- Nhóm trưởng đã xác nhận các dependency cần thiết đã merge vào `develop`.
- `git status --short` sạch.
- Branch phải được tạo mới từ `develop` vừa pull.

```bash
git checkout develop
git pull origin develop
git checkout -b feature/admin-ui-fe1-inventory-transfer
```

**Mức rủi ro:** Rất cao — 16 view/partial và workflow nhiều trạng thái.

## File được phép chỉnh

- `Areas/Admin/Views/AdminInventoryTransfer/Create.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Detail.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Index.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_ActionBar.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_DetailTable.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_DocumentInfo.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_Header.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_Note.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_StoreSelector.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_DetailTable.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_GeneralInfo.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_Header.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_Note.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_ResolutionPanel.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_StoreFlow.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_Timeline.cshtml`
- `wwwroot/css/Admin/InventoryTransfer/inventorytransfer.css`

Cấm chỉnh file ngoài danh sách này. File liên quan khác chỉ được đọc để hiểu hook và visual context.

## Kết quả giao diện phải đạt

- Transfer phải kế thừa document contract nhưng thể hiện rõ nguồn → đích và timeline.
- Create/Detail/Resolution có hierarchy trạng thái và action đúng bước hiện tại.
- Không để mọi action cùng màu primary.

## Quy chuẩn chi tiết theo loại trang

- Index: filter/status/table/pagination.
- Create: header, store selector, document info, line table, note, action bar.
- Detail: store flow, general info, line table, timeline, note, resolution panel.
- Status badge và timeline semantic nhất quán.

## Phần phải bảo toàn tuyệt đối

- Store selectors, dynamic rows, hidden IDs và submit.
- Timeline/status/resolution action forms và data hooks.
- Không đổi thứ tự workflow hoặc action availability.

## Prompt A — Audit read-only

Sao chép nguyên khối:

```text
Bạn đang làm Frontend 1 của CafeChain.

BẮT BUỘC đọc đầy đủ, theo thứ tự:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Toàn bộ source của các file được liệt kê cho đợt `INVENTORY TRANSFER — INDEX, CREATE, DETAIL, TIMELINE VÀ RESOLUTION`.

ĐÂY CHỈ LÀ PHASE A — AUDIT READ-ONLY. TUYỆT ĐỐI CHƯA SỬA CODE.

Phạm vi đợt này:
- `Areas/Admin/Views/AdminInventoryTransfer/Create.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Detail.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Index.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_ActionBar.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_DetailTable.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_DocumentInfo.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_Header.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_Note.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_StoreSelector.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_DetailTable.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_GeneralInfo.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_Header.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_Note.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_ResolutionPanel.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_StoreFlow.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_Timeline.cshtml`
- `wwwroot/css/Admin/InventoryTransfer/inventorytransfer.css`

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
Bạn đang làm Frontend 1 của CafeChain.

BẮT BUỘC đọc lại:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Báo cáo audit Phase A vừa được duyệt.

PHASE B — CHỈ TRIỂN KHAI ĐỢT `INVENTORY TRANSFER — INDEX, CREATE, DETAIL, TIMELINE VÀ RESOLUTION`.

CHỈ ĐƯỢC CHỈNH:
- `Areas/Admin/Views/AdminInventoryTransfer/Create.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Detail.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Index.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_ActionBar.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_DetailTable.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_DocumentInfo.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_Header.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_Note.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_StoreSelector.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_DetailTable.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_GeneralInfo.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_Header.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_Note.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_ResolutionPanel.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_StoreFlow.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_Timeline.cshtml`
- `wwwroot/css/Admin/InventoryTransfer/inventorytransfer.css`

MỤC TIÊU NGHIỆP VỤ VÀ GIAO DIỆN:
- Transfer phải kế thừa document contract nhưng thể hiện rõ nguồn → đích và timeline.
- Create/Detail/Resolution có hierarchy trạng thái và action đúng bước hiện tại.
- Không để mọi action cùng màu primary.

QUY CHUẨN THEO TRANG/FORM:
- Index: filter/status/table/pagination.
- Create: header, store selector, document info, line table, note, action bar.
- Detail: store flow, general info, line table, timeline, note, resolution panel.
- Status badge và timeline semantic nhất quán.

PHẦN PHẢI ĐÓNG BĂNG:
- Store selectors, dynamic rows, hidden IDs và submit.
- Timeline/status/resolution action forms và data hooks.
- Không đổi thứ tự workflow hoặc action availability.

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
- Index/filter/detail navigation.
- Create source/destination, line items, validation và submit.
- Detail timeline, resolution panel và status actions.
- Responsive table/timeline; không che action bar.

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
Hãy thực hiện PHASE C — INDEPENDENT VERIFICATION cho đợt `INVENTORY TRANSFER — INDEX, CREATE, DETAIL, TIMELINE VÀ RESOLUTION`. Không sửa thêm code trong bước này.

Đọc:
- hai file đặc tả chính trong `docs`;
- `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`;
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
- Index/filter/detail navigation.
- Create source/destination, line items, validation và submit.
- Detail timeline, resolution panel và status actions.
- Responsive table/timeline; không che action bar.
9. `dotnet build`, `git diff --check` có pass không.
10. So sánh ảnh trước/sau; chỉ ra trang nào còn lệch với module đã chuẩn hóa.

Trả kết quả dạng bảng: `Tiêu chí | PASS/FAIL | Bằng chứng | Việc cần sửa`.
Kết luận duy nhất:
- `VERIFIED PASS — MAY COMMIT AND PUSH`, hoặc
- `VERIFIED FAIL — MUST FIX BEFORE COMMIT`.
Không sửa code trong Phase C.
```

## Checklist thủ công của frontend

- Index/filter/detail navigation.
- Create source/destination, line items, validation và submit.
- Detail timeline, resolution panel và status actions.
- Responsive table/timeline; không che action bar.

Ngoài kiểm thử chức năng, bắt buộc chụp ít nhất:

- Ảnh trước và sau ở `1440×900`.
- Ảnh sau ở `1024×768`.
- Ảnh sau ở `390×844`.
- Một ảnh thể hiện form hoặc modal có validation.
- Một ảnh thể hiện table/list và action.

## Commit và push

Chỉ commit khi Prompt C trả `VERIFIED PASS — MAY COMMIT AND PUSH` và frontend đã tự kiểm tra.

```bash
git add "Areas/Admin/Views/AdminInventoryTransfer/Create.cshtml"
git add "Areas/Admin/Views/AdminInventoryTransfer/Detail.cshtml"
git add "Areas/Admin/Views/AdminInventoryTransfer/Index.cshtml"
git add "Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_ActionBar.cshtml"
git add "Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_DetailTable.cshtml"
git add "Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_DocumentInfo.cshtml"
git add "Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_Header.cshtml"
git add "Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_Note.cshtml"
git add "Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_StoreSelector.cshtml"
git add "Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_DetailTable.cshtml"
git add "Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_GeneralInfo.cshtml"
git add "Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_Header.cshtml"
git add "Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_Note.cshtml"
git add "Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_ResolutionPanel.cshtml"
git add "Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_StoreFlow.cshtml"
git add "Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_Timeline.cshtml"
git add "wwwroot/css/Admin/InventoryTransfer/inventorytransfer.css"
git diff --cached --name-only
git diff --cached --stat
git diff --cached --check
git commit -m "style(admin-inventory-transfer): unify inventory transfer workflow UI"
git push -u origin feature/admin-ui-fe1-inventory-transfer
```

Sau push: gửi nhóm trưởng branch, commit hash, ảnh trước/sau, checklist, build result và mọi exception còn lại. **Không tự merge.**


# ĐỢT 11 — FINAL REGRESSION FRONTEND 1 VÀ ĐỐI CHIẾU TOÀN ADMIN

## Điều kiện bắt đầu

Nhóm trưởng đã merge toàn bộ đợt FE1 và các đợt FE2 cần đối chiếu vào `develop`.

- Nhóm trưởng đã xác nhận các dependency cần thiết đã merge vào `develop`.
- `git status --short` sạch.
- Branch phải được tạo mới từ `develop` vừa pull.

```bash
git checkout develop
git pull origin develop
git checkout -b fix/admin-ui-fe1-final-regression
```

**Mức rủi ro:** Cao — chỉ sửa lỗi trong ownership FE1.

## File được phép chỉnh

- `Areas/Admin/Views/Admin/Index.cshtml`
- `Areas/Admin/Views/Shared/StoreScopeError.cshtml`
- `Areas/Admin/Views/Shared/_AdminLayout.cshtml`
- `Areas/Admin/Views/Shared/_EmptyState.cshtml`
- `Areas/Admin/Views/Shared/_IdentityLabel.cshtml`
- `Areas/Admin/Views/Shared/_QuantityWithUnit.cshtml`
- `Areas/Admin/Views/Shared/_StatusBadge.cshtml`
- `Areas/Admin/Views/Shared/_ValidationScriptsPartial.cshtml`
- `Areas/Admin/Views/_ViewImports.cshtml`
- `Areas/Admin/Views/_ViewStart.cshtml`
- `wwwroot/css/Admin/admin-unified-depth.css`
- `wwwroot/css/admin-white-orange-forms.css`
- `Areas/Admin/Views/AdminDrink/Create.cshtml`
- `Areas/Admin/Views/AdminDrink/Edit.cshtml`
- `Areas/Admin/Views/AdminDrink/Index.cshtml`
- `Areas/Admin/Views/AdminDrink/_DrinkTablePartial.cshtml`
- `wwwroot/css/Admin/Drink/drink.css`
- `wwwroot/css/Admin/AI/ai-image-pipeline.css`
- `Areas/Admin/Views/AdminSize/Index.cshtml`
- `Areas/Admin/Views/AdminTopping/Index.cshtml`
- `wwwroot/css/Admin/Size/size.css`
- `wwwroot/css/Admin/Topping/topping.css`
- `Areas/Admin/Views/AdminStoreMenu/Index.cshtml`
- `Areas/Admin/Views/AdminDrinkProfitability/Index.cshtml`
- `wwwroot/css/Admin/StoreMenu/store-menu.css`
- `wwwroot/css/Admin/Profitability/drink-profitability.css`
- `Areas/Admin/Views/AdminRecipe/Index.cshtml`
- `Areas/Admin/Views/AdminRecipe/Create.cshtml`
- `Areas/Admin/Views/AdminRecipe/Edit.cshtml`
- `wwwroot/css/recipe-builder.css`
- `Areas/Admin/Views/AdminRecipe/DataHealth.cshtml`
- `Areas/Admin/Views/AdminRecipe/Visualize.cshtml`
- `Areas/Admin/Views/AdminRecipe/Partials/_BomTree.cshtml`
- `Areas/Admin/Views/AdminRecipe/Partials/_BomTreeNode.cshtml`
- `Areas/Admin/Views/AdminPreparedItem/Index.cshtml`
- `Areas/Admin/Views/AdminProductionOrder/Create.cshtml`
- `wwwroot/css/Admin/ProductionOrder/production-order.css`
- `Areas/Admin/Views/AdminInventoryDocument/Index.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_ActionBar.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_CreateModal.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_DetailTable.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_DocumentInfo.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_IngredientRow.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_Summary.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DashboardCards.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DetailDocument.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DetailGeneral.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DetailModal.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DocumentTable.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DocumentTabs.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_FilterSection.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/_Pagination.cshtml`
- `wwwroot/css/Admin/InventoryDocument/inventorydocument.css`
- `wwwroot/css/Admin/InventoryDocument/inventorydocumentcreate.css`
- `Areas/Admin/Views/AdminInventoryTransfer/Create.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Detail.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Index.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_ActionBar.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_DetailTable.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_DocumentInfo.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_Header.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_Note.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_StoreSelector.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_DetailTable.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_GeneralInfo.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_Header.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_Note.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_ResolutionPanel.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_StoreFlow.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_Timeline.cshtml`
- `wwwroot/css/Admin/InventoryTransfer/inventorytransfer.css`

Cấm chỉnh file ngoài danh sách này. File liên quan khác chỉ được đọc để hiểu hook và visual context.

## Kết quả giao diện phải đạt

- Đối chiếu toàn bộ FE1 với module FE2 để bảo đảm một Admin duy nhất.
- Sửa chữ chìm, kích thước lệch, double padding, overflow, modal/table/validation không đồng bộ.
- Không thay đổi design contract đã được duyệt nếu không có phê duyệt nhóm trưởng.

## Quy chuẩn chi tiết theo loại trang

- So sánh ít nhất một Index, Create/Edit, Detail, modal và document của FE1 với trang tương đương FE2.
- Kiểm tra sidebar/header/button/input/table/modal/badge trên toàn Admin.
- Không sửa file FE2; lập handoff nếu lỗi thuộc FE2.

## Phần phải bảo toàn tuyệt đối

- Toàn bộ ownership FE2.
- JavaScript/backend/DOM như các đợt trước.

## Prompt A — Audit read-only

Sao chép nguyên khối:

```text
Bạn đang làm Frontend 1 của CafeChain.

BẮT BUỘC đọc đầy đủ, theo thứ tự:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Toàn bộ source của các file được liệt kê cho đợt `FINAL REGRESSION FRONTEND 1 VÀ ĐỐI CHIẾU TOÀN ADMIN`.

ĐÂY CHỈ LÀ PHASE A — AUDIT READ-ONLY. TUYỆT ĐỐI CHƯA SỬA CODE.

Phạm vi đợt này:
- `Areas/Admin/Views/Admin/Index.cshtml`
- `Areas/Admin/Views/Shared/StoreScopeError.cshtml`
- `Areas/Admin/Views/Shared/_AdminLayout.cshtml`
- `Areas/Admin/Views/Shared/_EmptyState.cshtml`
- `Areas/Admin/Views/Shared/_IdentityLabel.cshtml`
- `Areas/Admin/Views/Shared/_QuantityWithUnit.cshtml`
- `Areas/Admin/Views/Shared/_StatusBadge.cshtml`
- `Areas/Admin/Views/Shared/_ValidationScriptsPartial.cshtml`
- `Areas/Admin/Views/_ViewImports.cshtml`
- `Areas/Admin/Views/_ViewStart.cshtml`
- `wwwroot/css/Admin/admin-unified-depth.css`
- `wwwroot/css/admin-white-orange-forms.css`
- `Areas/Admin/Views/AdminDrink/Create.cshtml`
- `Areas/Admin/Views/AdminDrink/Edit.cshtml`
- `Areas/Admin/Views/AdminDrink/Index.cshtml`
- `Areas/Admin/Views/AdminDrink/_DrinkTablePartial.cshtml`
- `wwwroot/css/Admin/Drink/drink.css`
- `wwwroot/css/Admin/AI/ai-image-pipeline.css`
- `Areas/Admin/Views/AdminSize/Index.cshtml`
- `Areas/Admin/Views/AdminTopping/Index.cshtml`
- `wwwroot/css/Admin/Size/size.css`
- `wwwroot/css/Admin/Topping/topping.css`
- `Areas/Admin/Views/AdminStoreMenu/Index.cshtml`
- `Areas/Admin/Views/AdminDrinkProfitability/Index.cshtml`
- `wwwroot/css/Admin/StoreMenu/store-menu.css`
- `wwwroot/css/Admin/Profitability/drink-profitability.css`
- `Areas/Admin/Views/AdminRecipe/Index.cshtml`
- `Areas/Admin/Views/AdminRecipe/Create.cshtml`
- `Areas/Admin/Views/AdminRecipe/Edit.cshtml`
- `wwwroot/css/recipe-builder.css`
- `Areas/Admin/Views/AdminRecipe/DataHealth.cshtml`
- `Areas/Admin/Views/AdminRecipe/Visualize.cshtml`
- `Areas/Admin/Views/AdminRecipe/Partials/_BomTree.cshtml`
- `Areas/Admin/Views/AdminRecipe/Partials/_BomTreeNode.cshtml`
- `Areas/Admin/Views/AdminPreparedItem/Index.cshtml`
- `Areas/Admin/Views/AdminProductionOrder/Create.cshtml`
- `wwwroot/css/Admin/ProductionOrder/production-order.css`
- `Areas/Admin/Views/AdminInventoryDocument/Index.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_ActionBar.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_CreateModal.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_DetailTable.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_DocumentInfo.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_IngredientRow.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_Summary.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DashboardCards.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DetailDocument.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DetailGeneral.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DetailModal.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DocumentTable.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DocumentTabs.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_FilterSection.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/_Pagination.cshtml`
- `wwwroot/css/Admin/InventoryDocument/inventorydocument.css`
- `wwwroot/css/Admin/InventoryDocument/inventorydocumentcreate.css`
- `Areas/Admin/Views/AdminInventoryTransfer/Create.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Detail.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Index.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_ActionBar.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_DetailTable.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_DocumentInfo.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_Header.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_Note.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_StoreSelector.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_DetailTable.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_GeneralInfo.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_Header.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_Note.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_ResolutionPanel.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_StoreFlow.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_Timeline.cshtml`
- `wwwroot/css/Admin/InventoryTransfer/inventorytransfer.css`

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
Bạn đang làm Frontend 1 của CafeChain.

BẮT BUỘC đọc lại:
1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`.
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`.
3. `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`.
4. Báo cáo audit Phase A vừa được duyệt.

PHASE B — CHỈ TRIỂN KHAI ĐỢT `FINAL REGRESSION FRONTEND 1 VÀ ĐỐI CHIẾU TOÀN ADMIN`.

CHỈ ĐƯỢC CHỈNH:
- `Areas/Admin/Views/Admin/Index.cshtml`
- `Areas/Admin/Views/Shared/StoreScopeError.cshtml`
- `Areas/Admin/Views/Shared/_AdminLayout.cshtml`
- `Areas/Admin/Views/Shared/_EmptyState.cshtml`
- `Areas/Admin/Views/Shared/_IdentityLabel.cshtml`
- `Areas/Admin/Views/Shared/_QuantityWithUnit.cshtml`
- `Areas/Admin/Views/Shared/_StatusBadge.cshtml`
- `Areas/Admin/Views/Shared/_ValidationScriptsPartial.cshtml`
- `Areas/Admin/Views/_ViewImports.cshtml`
- `Areas/Admin/Views/_ViewStart.cshtml`
- `wwwroot/css/Admin/admin-unified-depth.css`
- `wwwroot/css/admin-white-orange-forms.css`
- `Areas/Admin/Views/AdminDrink/Create.cshtml`
- `Areas/Admin/Views/AdminDrink/Edit.cshtml`
- `Areas/Admin/Views/AdminDrink/Index.cshtml`
- `Areas/Admin/Views/AdminDrink/_DrinkTablePartial.cshtml`
- `wwwroot/css/Admin/Drink/drink.css`
- `wwwroot/css/Admin/AI/ai-image-pipeline.css`
- `Areas/Admin/Views/AdminSize/Index.cshtml`
- `Areas/Admin/Views/AdminTopping/Index.cshtml`
- `wwwroot/css/Admin/Size/size.css`
- `wwwroot/css/Admin/Topping/topping.css`
- `Areas/Admin/Views/AdminStoreMenu/Index.cshtml`
- `Areas/Admin/Views/AdminDrinkProfitability/Index.cshtml`
- `wwwroot/css/Admin/StoreMenu/store-menu.css`
- `wwwroot/css/Admin/Profitability/drink-profitability.css`
- `Areas/Admin/Views/AdminRecipe/Index.cshtml`
- `Areas/Admin/Views/AdminRecipe/Create.cshtml`
- `Areas/Admin/Views/AdminRecipe/Edit.cshtml`
- `wwwroot/css/recipe-builder.css`
- `Areas/Admin/Views/AdminRecipe/DataHealth.cshtml`
- `Areas/Admin/Views/AdminRecipe/Visualize.cshtml`
- `Areas/Admin/Views/AdminRecipe/Partials/_BomTree.cshtml`
- `Areas/Admin/Views/AdminRecipe/Partials/_BomTreeNode.cshtml`
- `Areas/Admin/Views/AdminPreparedItem/Index.cshtml`
- `Areas/Admin/Views/AdminProductionOrder/Create.cshtml`
- `wwwroot/css/Admin/ProductionOrder/production-order.css`
- `Areas/Admin/Views/AdminInventoryDocument/Index.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_ActionBar.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_CreateModal.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_DetailTable.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_DocumentInfo.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_IngredientRow.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_Summary.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DashboardCards.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DetailDocument.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DetailGeneral.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DetailModal.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DocumentTable.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DocumentTabs.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_FilterSection.cshtml`
- `Areas/Admin/Views/AdminInventoryDocument/_Pagination.cshtml`
- `wwwroot/css/Admin/InventoryDocument/inventorydocument.css`
- `wwwroot/css/Admin/InventoryDocument/inventorydocumentcreate.css`
- `Areas/Admin/Views/AdminInventoryTransfer/Create.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Detail.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Index.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_ActionBar.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_DetailTable.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_DocumentInfo.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_Header.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_Note.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_StoreSelector.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_DetailTable.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_GeneralInfo.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_Header.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_Note.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_ResolutionPanel.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_StoreFlow.cshtml`
- `Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_Timeline.cshtml`
- `wwwroot/css/Admin/InventoryTransfer/inventorytransfer.css`

MỤC TIÊU NGHIỆP VỤ VÀ GIAO DIỆN:
- Đối chiếu toàn bộ FE1 với module FE2 để bảo đảm một Admin duy nhất.
- Sửa chữ chìm, kích thước lệch, double padding, overflow, modal/table/validation không đồng bộ.
- Không thay đổi design contract đã được duyệt nếu không có phê duyệt nhóm trưởng.

QUY CHUẨN THEO TRANG/FORM:
- So sánh ít nhất một Index, Create/Edit, Detail, modal và document của FE1 với trang tương đương FE2.
- Kiểm tra sidebar/header/button/input/table/modal/badge trên toàn Admin.
- Không sửa file FE2; lập handoff nếu lỗi thuộc FE2.

PHẦN PHẢI ĐÓNG BĂNG:
- Toàn bộ ownership FE2.
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
- Chạy full build.
- Test toàn bộ luồng FE1 đã nêu.
- Test 5 viewport + zoom 125%.
- Kiểm tra StaffHub/POS không bị ảnh hưởng.
- Lập bảng PASS/FAIL từng module và không để mục chưa kiểm tra.

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
Hãy thực hiện PHASE C — INDEPENDENT VERIFICATION cho đợt `FINAL REGRESSION FRONTEND 1 VÀ ĐỐI CHIẾU TOÀN ADMIN`. Không sửa thêm code trong bước này.

Đọc:
- hai file đặc tả chính trong `docs`;
- `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`;
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
- Chạy full build.
- Test toàn bộ luồng FE1 đã nêu.
- Test 5 viewport + zoom 125%.
- Kiểm tra StaffHub/POS không bị ảnh hưởng.
- Lập bảng PASS/FAIL từng module và không để mục chưa kiểm tra.
9. `dotnet build`, `git diff --check` có pass không.
10. So sánh ảnh trước/sau; chỉ ra trang nào còn lệch với module đã chuẩn hóa.

Trả kết quả dạng bảng: `Tiêu chí | PASS/FAIL | Bằng chứng | Việc cần sửa`.
Kết luận duy nhất:
- `VERIFIED PASS — MAY COMMIT AND PUSH`, hoặc
- `VERIFIED FAIL — MUST FIX BEFORE COMMIT`.
Không sửa code trong Phase C.
```

## Checklist thủ công của frontend

- Chạy full build.
- Test toàn bộ luồng FE1 đã nêu.
- Test 5 viewport + zoom 125%.
- Kiểm tra StaffHub/POS không bị ảnh hưởng.
- Lập bảng PASS/FAIL từng module và không để mục chưa kiểm tra.

Ngoài kiểm thử chức năng, bắt buộc chụp ít nhất:

- Ảnh trước và sau ở `1440×900`.
- Ảnh sau ở `1024×768`.
- Ảnh sau ở `390×844`.
- Một ảnh thể hiện form hoặc modal có validation.
- Một ảnh thể hiện table/list và action.

## Commit và push

Chỉ commit khi Prompt C trả `VERIFIED PASS — MAY COMMIT AND PUSH` và frontend đã tự kiểm tra.

```bash
# Chỉ các path ownership FE1; file không đổi sẽ không được stage
git add "Areas/Admin/Views/Admin/Index.cshtml"
git add "Areas/Admin/Views/Shared/StoreScopeError.cshtml"
git add "Areas/Admin/Views/Shared/_AdminLayout.cshtml"
git add "Areas/Admin/Views/Shared/_EmptyState.cshtml"
git add "Areas/Admin/Views/Shared/_IdentityLabel.cshtml"
git add "Areas/Admin/Views/Shared/_QuantityWithUnit.cshtml"
git add "Areas/Admin/Views/Shared/_StatusBadge.cshtml"
git add "Areas/Admin/Views/Shared/_ValidationScriptsPartial.cshtml"
git add "Areas/Admin/Views/_ViewImports.cshtml"
git add "Areas/Admin/Views/_ViewStart.cshtml"
git add "wwwroot/css/Admin/admin-unified-depth.css"
git add "wwwroot/css/admin-white-orange-forms.css"
git add "Areas/Admin/Views/AdminDrink/Create.cshtml"
git add "Areas/Admin/Views/AdminDrink/Edit.cshtml"
git add "Areas/Admin/Views/AdminDrink/Index.cshtml"
git add "Areas/Admin/Views/AdminDrink/_DrinkTablePartial.cshtml"
git add "wwwroot/css/Admin/Drink/drink.css"
git add "wwwroot/css/Admin/AI/ai-image-pipeline.css"
git add "Areas/Admin/Views/AdminSize/Index.cshtml"
git add "Areas/Admin/Views/AdminTopping/Index.cshtml"
git add "wwwroot/css/Admin/Size/size.css"
git add "wwwroot/css/Admin/Topping/topping.css"
git add "Areas/Admin/Views/AdminStoreMenu/Index.cshtml"
git add "Areas/Admin/Views/AdminDrinkProfitability/Index.cshtml"
git add "wwwroot/css/Admin/StoreMenu/store-menu.css"
git add "wwwroot/css/Admin/Profitability/drink-profitability.css"
git add "Areas/Admin/Views/AdminRecipe/Index.cshtml"
git add "Areas/Admin/Views/AdminRecipe/Create.cshtml"
git add "Areas/Admin/Views/AdminRecipe/Edit.cshtml"
git add "wwwroot/css/recipe-builder.css"
git add "Areas/Admin/Views/AdminRecipe/DataHealth.cshtml"
git add "Areas/Admin/Views/AdminRecipe/Visualize.cshtml"
git add "Areas/Admin/Views/AdminRecipe/Partials/_BomTree.cshtml"
git add "Areas/Admin/Views/AdminRecipe/Partials/_BomTreeNode.cshtml"
git add "Areas/Admin/Views/AdminPreparedItem/Index.cshtml"
git add "Areas/Admin/Views/AdminProductionOrder/Create.cshtml"
git add "wwwroot/css/Admin/ProductionOrder/production-order.css"
git add "Areas/Admin/Views/AdminInventoryDocument/Index.cshtml"
git add "Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_ActionBar.cshtml"
git add "Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_CreateModal.cshtml"
git add "Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_DetailTable.cshtml"
git add "Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_DocumentInfo.cshtml"
git add "Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_IngredientRow.cshtml"
git add "Areas/Admin/Views/AdminInventoryDocument/Partials/Create/_Summary.cshtml"
git add "Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DashboardCards.cshtml"
git add "Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DetailDocument.cshtml"
git add "Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DetailGeneral.cshtml"
git add "Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DetailModal.cshtml"
git add "Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DocumentTable.cshtml"
git add "Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_DocumentTabs.cshtml"
git add "Areas/Admin/Views/AdminInventoryDocument/Partials/Detail/_FilterSection.cshtml"
git add "Areas/Admin/Views/AdminInventoryDocument/_Pagination.cshtml"
git add "wwwroot/css/Admin/InventoryDocument/inventorydocument.css"
git add "wwwroot/css/Admin/InventoryDocument/inventorydocumentcreate.css"
git add "Areas/Admin/Views/AdminInventoryTransfer/Create.cshtml"
git add "Areas/Admin/Views/AdminInventoryTransfer/Detail.cshtml"
git add "Areas/Admin/Views/AdminInventoryTransfer/Index.cshtml"
git add "Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_ActionBar.cshtml"
git add "Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_DetailTable.cshtml"
git add "Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_DocumentInfo.cshtml"
git add "Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_Header.cshtml"
git add "Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_Note.cshtml"
git add "Areas/Admin/Views/AdminInventoryTransfer/Partials/Create/_StoreSelector.cshtml"
git add "Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_DetailTable.cshtml"
git add "Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_GeneralInfo.cshtml"
git add "Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_Header.cshtml"
git add "Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_Note.cshtml"
git add "Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_ResolutionPanel.cshtml"
git add "Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_StoreFlow.cshtml"
git add "Areas/Admin/Views/AdminInventoryTransfer/Partials/Detail/_Timeline.cshtml"
git add "wwwroot/css/Admin/InventoryTransfer/inventorytransfer.css"
git diff --cached --name-only
git diff --cached --stat
git diff --cached --check
git commit -m "fix(admin-ui-fe1): resolve final visual regression"
git push -u origin fix/admin-ui-fe1-final-regression
```

Sau push: gửi nhóm trưởng branch, commit hash, ảnh trước/sau, checklist, build result và mọi exception còn lại. **Không tự merge.**


# Cổng phối hợp bắt buộc của Frontend 1

1. Core Admin là push quan trọng nhất. Frontend 1 phải push Core trước và chờ nhóm trưởng merge.
2. Frontend 2 không được bắt đầu module cho đến khi Core đã merge.
3. Sau khi Core được duyệt, mỗi đợt FE1 tạo branch mới từ `develop`; không tiếp tục trên branch Core cũ.
4. Nếu Frontend 2 báo selector chung thiếu hoặc conflict, FE1 chỉ sửa Core trong một branch fix riêng sau khi nhóm trưởng đồng ý.
5. Final regression chỉ bắt đầu khi các module FE2 cần đối chiếu đã được merge.

# Thứ tự push chốt Frontend 1

1. `feature/admin-ui-fe1-core` — bắt buộc merge trước mọi module FE2.
2. `feature/admin-ui-fe1-drink`.
3. `feature/admin-ui-fe1-size-topping`.
4. `feature/admin-ui-fe1-storemenu-profitability`.
5. `feature/admin-ui-fe1-recipe-core`.
6. `feature/admin-ui-fe1-bom-health`.
7. `feature/admin-ui-fe1-production`.
8. `feature/admin-ui-fe1-inventory-document`.
9. `feature/admin-ui-fe1-inventory-transfer`.
10. `fix/admin-ui-fe1-final-regression`.


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
