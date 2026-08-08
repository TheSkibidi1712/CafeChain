# CAFECHAIN ADMIN UI/UX — PHÂN CÔNG FRONTEND 2: DASHBOARD, OPERATIONS, PROCUREMENT, HR & SYSTEM

> **Phạm vi tổng:** Giao diện Admin của dự án `CafeChain(20260806-113455).zip`.
>
> **Người thực hiện:** Frontend 2.
>
> **Mục tiêu:** Đồng bộ phần Admin được giao theo cùng design system màu nâu CafeChain, bảo toàn tuyệt đối DOM, JavaScript, backend và nghiệp vụ.
>
> **Ngoài phạm vi:** StaffHub và ứng dụng POS riêng.

---

## 1. KHÓA PHẠM VI — BẮT BUỘC, KHÔNG ĐƯỢC DIỄN GIẢI KHÁC

### 1.1. Chỉ được chỉnh Frontend trong phạm vi `.cshtml` và `.css`

- Chỉ được chỉnh các file `.cshtml` và `.css` đã được giao trong tài liệu này.
- **Nghiêm cấm chỉnh JavaScript dưới mọi hình thức**, kể cả sửa nhỏ, đổi selector, đổi event, đổi cấu hình plugin, thêm script hoặc chuyển script sang file khác.
- **Nghiêm cấm chỉnh backend**, gồm controller, service, repository, model, view model, DTO, API, authorization, middleware, filter, helper nghiệp vụ và cấu hình nghiệp vụ.
- **Nghiêm cấm chỉnh database**, migration, SQL, stored procedure, seed, permission seed và dữ liệu mẫu.
- **Nghiêm cấm thay đổi nghiệp vụ**, trạng thái, workflow, quyền, phạm vi dữ liệu, validation, route, action và cách submit hiện tại.
- Không chỉnh StaffHub hoặc ứng dụng POS riêng. Tab POS/WorkShift trong Dashboard Admin chỉ là dữ liệu phân tích của Admin, không phải phạm vi thiết kế lại POS.

### 1.2. Đóng băng cấu trúc `.cshtml`

**Không được thêm, xóa, đổi tên, thay thế hoặc di chuyển cấu trúc thẻ HTML/Razor hiện có.**

Phải giữ nguyên tuyệt đối:

- `id`, `name`, `for`, `value`, `type`, `role`, `aria-*`.
- `asp-area`, `asp-controller`, `asp-action`, `asp-route-*`, `asp-for`, `asp-items`.
- `method`, `action`, antiforgery token, hidden input và validation binding.
- `data-*`, `onclick`, `onchange`, `onsubmit` và mọi event hook.
- Class đang được JavaScript, Bootstrap, Select2, chart, map, modal, tab, collapse, drag/drop hoặc dynamic row sử dụng.
- Modal ID, tab ID, collapse ID, drawer ID, table ID, form ID, partial host và JavaScript hook.
- Thứ tự DOM khi việc thay đổi có thể ảnh hưởng focus, binding, validation, selector hoặc stacking context.

**Cấm thực hiện các thay đổi sau:**

- Thay table bằng card list.
- Thay form full-page bằng modal hoặc ngược lại.
- Thay input/select/textarea bằng component khác.
- Thay tab bằng accordion.
- Tạo lại DOM để “code đẹp hơn”.
- Thêm framework UI hoặc package mới.
- Di chuyển hoặc viết lại script nằm trong `.cshtml`.
- Đổi text nghiệp vụ, label, trạng thái hoặc thứ tự action.

**Chỉ được phép:**

- Chỉnh CSS về màu, typography, spacing, border, radius, shadow, layout hiển thị, responsive và accessibility.
- Chỉnh giá trị `class` khi thật sự cần cho CSS, nhưng phải giữ toàn bộ class cũ và không được ảnh hưởng JavaScript hook.
- Chỉnh CSS trong `<style>` hiện có khi cần, nhưng không thêm/xóa/di chuyển thẻ `<style>` hoặc `<link>`.
- Dùng selector tương thích với markup hiện tại để đồng bộ component.

### 1.3. Định nghĩa “đồng bộ toàn Admin”

- Tất cả module phải dùng chung một hệ màu nâu CafeChain, typography, spacing, radius, shadow, button, input, table, modal, badge và responsive rule.
- Không yêu cầu các trang có HTML giống nhau; nghiệp vụ khác nhau vẫn giữ bố cục riêng.
- Mọi trang phải tạo cảm giác thuộc cùng một sản phẩm, không còn tình trạng mỗi module có header, button, form, table và màu riêng.
- Dashboard, lịch ca, BOM, phiếu kho, mua hàng, quản lý đá và form CRUD được giữ luồng hiện tại nhưng phải cùng visual contract.

---

## 2. KẾT QUẢ RÀ SOÁT TOÀN BỘ ADMIN

| Hạng mục | Kết quả |
|---|---:|
| File `.cshtml` trong `Areas/Admin/Views` | **118** |
| Form trong Admin Views | khoảng **126** |
| Table trong Admin Views | khoảng **68** |
| Button/link mang class kiểu button | khoảng **421** |
| Thẻ `<script>` trong Admin Views | **82** — đóng băng, không chỉnh |
| Thuộc tính `id` | khoảng **1.122** — không đổi |
| Thuộc tính `name` | khoảng **464** — không đổi |
| Thuộc tính `data-*` | khoảng **475** — không đổi |
| Thuộc tính `asp-*` | khoảng **1.223** — không đổi |
| Event inline như `onclick` | khoảng **33** — không đổi |
| CSS liên quan trực tiếp Admin | **32 file** |
| Tổng số dòng CSS liên quan | khoảng **30.164 dòng** |
| Mã màu hex khác nhau | khoảng **569** |
| Màu `rgb/rgba` khác nhau | khoảng **327** |
| Lần dùng `!important` | khoảng **919** |
| Custom property/CSS variable khác nhau | khoảng **342** |
| `<style>` block trong Admin Views | **7** |
| Thuộc tính `style="..."` | khoảng **122** |

### Kết luận rà soát

- Hệ class và màu đang bị phân mảnh theo module: `analytics-*`, `cc-*`, `ops-*`, `pa-*`, `ice-*`, `rb-*`, `supplier-*`, `store-admin-*`, `perm-*`, `pf-*`, `uc-*`, `transfer-*`, `inventory-*`, `category-*`, `drink-*`...
- Vấn đề chính không phải thiếu hiệu ứng mà là thiếu một design contract duy nhất.
- Không nên viết lại DOM của 118 view vì rủi ro phá JavaScript, Razor binding và workflow rất cao.
- Hướng an toàn là giữ CSS module cho bố cục đặc thù và map các selector hiện có về cùng token/component contract.

---

## 3. DESIGN SYSTEM MÀU NÂU CAFECHAIN — ÁP DỤNG CHUNG

### 3.1. Định hướng thẩm mỹ

- Coffee-brown premium admin.
- Sáng, sạch, chuyên nghiệp và có chiều sâu vừa phải.
- Nền kem sáng, tiêu đề đậm, nội dung chính có độ tương phản cao.
- Không biến Admin thành landing page.
- Không dùng glassmorphism nặng, gradient tràn lan, shadow đen nặng hoặc animation gây phân tâm.
- Visual richness: `6/10`; data density: `6/10`; motion: `2/10`.
- Dữ liệu, trạng thái và hành động nghiệp vụ luôn quan trọng hơn trang trí.

### 3.2. Brand brown scale

| Token | Mã màu | Công dụng |
|---|---|---|
| `--cc-brown-950` | `#2B1A12` | Heading rất đậm, active mạnh |
| `--cc-brown-900` | `#3D2418` | Hover primary, active text |
| `--cc-brown-800` | `#4D3021` | Heading/icon đậm |
| `--cc-brown-700` | `#5C3F2B` | Nâu nhận diện phụ |
| `--cc-brown-600` | `#70482F` | Primary chính |
| `--cc-brown-500` | `#8B6247` | Accent trung bình |
| `--cc-caramel-500` | `#A97750` | Accent trang trí |
| `--cc-caramel-300` | `#C99E7D` | Accent/border nhẹ |
| `--cc-brown-200` | `#DFC5B1` | Border nâu mềm |
| `--cc-brown-100` | `#F0E2D6` | Active background nhẹ |
| `--cc-brown-50` | `#FAF6F2` | Surface tint |

### 3.3. Surface và text

| Token | Mã màu | Công dụng |
|---|---|---|
| `--cc-canvas` | `#F7F4F0` | Nền toàn Admin |
| `--cc-surface` | `#FFFDFB` | Card/panel chính |
| `--cc-surface-raised` | `#FFFFFF` | Modal/header/card nổi |
| `--cc-surface-muted` | `#FBF7F2` | Filter/nested panel |
| `--cc-surface-active` | `#F4E9DF` | Nav/tab/row selected |
| `--cc-border` | `#E9DED4` | Border chuẩn |
| `--cc-border-strong` | `#D8C5B6` | Border control mạnh |
| `--cc-text` | `#201812` | Chữ chính |
| `--cc-text-secondary` | `#66584F` | Chữ mô tả |
| `--cc-text-muted` | `#7A6C62` | Metadata/hint |
| `--cc-text-disabled` | `#A69B93` | Disabled |
| `--cc-text-inverse` | `#FFFFFF` | Chữ trên nền đậm |

### 3.4. Semantic colors

| Trạng thái | Màu chính | Nền nhẹ | Border |
|---|---|---|---|
| Success/Completed/Active | `#2F6F5E` | `#EDF6F2` | `#BBDCCD` |
| Warning/Pending/Attention | `#99623B` | `#FBF3EA` | `#E4C6A9` |
| Danger/Error/Rejected | `#991B1B` | `#FDF0F0` | `#E9B8B8` |
| Info/In review | `#3F5F7A` | `#EEF4F8` | `#BDD0DD` |
| Neutral/Draft/Inactive | `#64748B` | `#F1F5F9` | `#CBD5E1` |

Quy tắc:

- Primary action dùng `#70482F`, hover `#3D2418`, chữ trắng.
- `#A97750` chỉ dùng accent; không dùng làm body text nhỏ trên nền trắng.
- Không dùng màu nâu thay success/warning/danger.
- Không truyền trạng thái chỉ bằng màu; luôn giữ text/icon/badge label.
- Danger chỉ dùng cho xóa, từ chối, hủy hoặc khóa có hậu quả.

### 3.5. CSS token chuẩn

```css
:root {
    --cc-brown-950: #2b1a12;
    --cc-brown-900: #3d2418;
    --cc-brown-800: #4d3021;
    --cc-brown-700: #5c3f2b;
    --cc-brown-600: #70482f;
    --cc-brown-500: #8b6247;
    --cc-caramel-500: #a97750;
    --cc-caramel-300: #c99e7d;
    --cc-brown-200: #dfc5b1;
    --cc-brown-100: #f0e2d6;
    --cc-brown-50: #faf6f2;

    --cc-canvas: #f7f4f0;
    --cc-surface: #fffdfb;
    --cc-surface-raised: #ffffff;
    --cc-surface-muted: #fbf7f2;
    --cc-surface-active: #f4e9df;
    --cc-border: #e9ded4;
    --cc-border-strong: #d8c5b6;

    --cc-text: #201812;
    --cc-text-secondary: #66584f;
    --cc-text-muted: #7a6c62;
    --cc-text-disabled: #a69b93;
    --cc-text-inverse: #ffffff;

    --cc-success: #2f6f5e;
    --cc-success-bg: #edf6f2;
    --cc-warning: #99623b;
    --cc-warning-bg: #fbf3ea;
    --cc-danger: #991b1b;
    --cc-danger-bg: #fdf0f0;
    --cc-info: #3f5f7a;
    --cc-info-bg: #eef4f8;
    --cc-neutral: #64748b;
    --cc-neutral-bg: #f1f5f9;

    --cc-focus-ring: 0 0 0 3px rgba(112, 72, 47, 0.22);
}
```

---

## 4. TYPOGRAPHY, SPACING, RADIUS, SHADOW VÀ MOTION

### 4.1. Typography

- Giữ font `Inter`; không thêm font mới.
- Dùng `font-variant-numeric: tabular-nums` cho tiền, số lượng, tỷ lệ, KPI, mã phiếu và dữ liệu bảng.

| Vai trò | Desktop | Mobile | Weight | Line-height |
|---|---:|---:|---:|---:|
| Page title | `32–38px` | `24–28px` | `800` | `1.12` |
| Detail/document title | `28–32px` | `23–26px` | `780–800` | `1.18` |
| Section title | `18–20px` | `17–18px` | `700–750` | `1.3` |
| Card title | `15–16px` | `15px` | `700` | `1.35` |
| Body | `14px` | `14px` | `400–500` | `1.55` |
| Form label | `13px` | `13px` | `650–700` | `1.35` |
| Table header | `12px` | `12px` | `750` | `1.3` |
| Metadata | `12px` | `11.5–12px` | `500–600` | `1.45` |
| Badge | `11–12px` | `11px` | `700` | `1` |

Tiêu đề có chiều sâu bằng hierarchy, khoảng trắng, accent trái và surface mềm; không dùng text-shadow nặng hoặc chữ nâu nhạt trên nền kem.

### 4.2. Spacing

Dùng hệ 4px: `4, 8, 12, 16, 20, 24, 32, 40, 48, 64px`.

- Icon–text: `8px`.
- Button group: `8–12px`.
- Form field vertical gap: `16px`.
- Section gap: `20–24px`.
- Page block gap: `24–32px`.
- Card padding: `20–24px`.
- Data-heavy panel padding: `16–20px`.

### 4.3. Radius

- `--cc-radius-xs: 6px`.
- `--cc-radius-sm: 8px`.
- `--cc-radius-control: 10px`.
- `--cc-radius-card: 16px`.
- `--cc-radius-header: 20px`.
- `--cc-radius-modal: 18px`.
- `--cc-radius-pill: 999px` chỉ dùng badge/chip.

### 4.4. Shadow

```css
--cc-shadow-xs: 0 2px 8px rgba(61, 36, 24, 0.05);
--cc-shadow-sm: 0 8px 22px rgba(61, 36, 24, 0.08);
--cc-shadow-md: 0 16px 38px rgba(61, 36, 24, 0.11),
                0 3px 10px rgba(61, 36, 24, 0.05);
--cc-shadow-lg: 0 24px 60px rgba(61, 36, 24, 0.15),
                0 8px 24px rgba(61, 36, 24, 0.07);
```

- Table/card thường: `xs/sm`.
- Page header: `md`.
- Modal/dropdown: `lg` vừa phải.
- Hover chỉ nâng tối đa `1–2px`.

### 4.5. Motion

- Transition `150–180ms ease`.
- Hover button `translateY(-1px)` tối đa.
- Không animation vô hạn.
- Tôn trọng `prefers-reduced-motion: reduce`.

---

## 5. BỐ CỤC VÀ KÍCH THƯỚC COMPONENT CHUẨN

### 5.1. Sidebar và main content

- Sidebar desktop giữ `260px` để không phá layout.
- Nav item min-height `40px`; sub-item `34–36px`.
- Active: nền `#F4E9DF`, chữ `#3D2418`, accent nâu.
- Main content padding: `30–32px` ở desktop lớn; `24–28px` desktop; `18–20px` tablet; `14–16px` mobile.
- Không để double padding giữa `.main-content` và page wrapper.
- Không để toàn trang cuộn ngang; table rộng cuộn trong wrapper.

### 5.2. Page header

| Thuộc tính | Chuẩn |
|---|---|
| List/dashboard min-height | `148–162px` |
| Create/detail compact min-height | `126–140px` |
| Padding | `26–32px` |
| Radius | `20px` |
| Border | `1px solid rgba(112,72,47,.16)` |
| Background | Trắng → kem rất nhẹ |
| Left accent | `4–5px`, caramel → nâu |
| Shadow | `--cc-shadow-md` |
| Actions | căn phải, wrap khi thiếu chỗ |

### 5.3. Button

| Size | Height | Padding ngang | Font | Icon |
|---|---:|---:|---:|---:|
| Icon XS | `32px` | `0` | — | `13px` |
| Small | `36px` | `12px` | `12.5–13px` | `13px` |
| Default | `44px` | `16–18px` | `13.5–14px` | `14px` |
| Large | `48px` | `20px` | `14px` | `15px` |
| Icon default | `40×40px` | `0` | — | `14px` |

- Radius `10px`; icon-label gap `8px`.
- Chỉ một primary cùng cấp trong một panel/form.
- Secondary cho Back/Cancel/Reset.
- Ghost cho action ít ưu tiên hoặc trong table.
- Danger cho destructive action thực sự.
- Nút trong table dùng `32–36px`.

### 5.4. Input, select, textarea và Select2

| Thành phần | Chuẩn |
|---|---|
| Input/select height | `44px` |
| Small | `36px` |
| Large | `48px` |
| Padding ngang | `12–14px` |
| Radius | `10px` |
| Border | `1px solid #D8C5B6` |
| Focus border | `#70482F` |
| Focus ring | `0 0 0 3px rgba(112,72,47,.22)` |
| Textarea min-height | `108–120px` |

- Label `13px`, weight `650–700`.
- Validation `12.5–13px`, danger color, spacing `6px`.
- Select2 phải khớp input height; không sửa JavaScript khởi tạo.
- Form CRUD desktop ưu tiên 2 cột khi hợp lý, mobile 1 cột.

### 5.5. Table, list và pagination

- Panel border `#E9DED4`, radius `16px`, shadow `xs/sm`.
- Header height `42–46px`.
- Row min-height `52px`; data-heavy `46–48px`.
- Cell padding mặc định `12px 16px`; compact `10px 12px`.
- Hover `#FAF6F2`; selected `#F4E9DF`.
- Text/code căn trái; tiền/số lượng/tỷ lệ căn phải; action căn phải.
- Không center toàn bảng.
- Pagination button `36px`, radius `8px`, gap `6–8px`.
- Giữ nguyên table DOM và Razor paging logic.

### 5.6. Card, KPI, badge, alert

- Card: surface `#FFFDFB`, border `#E9DED4`, radius `16px`, padding `20–24px`.
- KPI: height `96–112px`; value `24–30px`, weight `800`.
- Badge: height `24–28px`, padding `6–10px`, font `11–12px`.
- Alert dùng semantic icon/border; không dùng nâu cho lỗi.
- Empty state không dùng danger nếu chỉ là chưa có dữ liệu.

### 5.7. Modal

| Cỡ | Max-width |
|---|---:|
| Small confirm | `440–480px` |
| Default form | `620–680px` |
| Large | `840–920px` |
| XL document/table | `1080–1180px` |

- Radius `18px`; header/body `20–24px`; footer `16–24px`.
- Body scroll khi cần; footer luôn nhìn thấy.
- Close button vùng click tối thiểu `36×36px`.
- Giữ nguyên modal IDs, `data-bs-*` và JavaScript.

---

## 6. QUY CHUẨN TOÀN BỘ MODULE ADMIN

Phần này xuất hiện trong cả hai tài liệu để hai frontend dùng cùng tiêu chuẩn, dù mỗi người chỉ chỉnh module được giao.

### Dashboard Admin

- Giữ 6 nhóm dữ liệu và tab AI.
- Filter đang áp dụng phải rõ; Apply là primary.
- KPI 24–30px; chart card đồng bộ.
- AI result theo hierarchy: context → conclusion → evidence/chart → limitation → recommendation.
- Loading/no data/partial/error có semantics thống nhất.

### Sản phẩm

- Category, Drink, Size, Topping, Profitability, StoreMenu dùng cùng header, summary, filter, table và form token.
- Ảnh preview radius `12–16px`.
- AI image action là secondary/tertiary.
- Không sửa upload/crop/toggle JavaScript.

### BOM và sơ chế

- PreparedItem, Recipe, ProductionOrder ưu tiên độ chính xác.
- Ingredient rows thẳng cột; số căn phải.
- BOM tree/data health rõ, không trang trí nặng.
- Save/Create/Apply mới là primary.

### Kho và mua hàng

- Thể hiện rõ workflow: Alert → Restock → Purchase Advice → Consolidation/Batch → PO → Receipt → Nhập kho.
- Status badge thống nhất.
- Action chuyển trạng thái tách khỏi navigation.
- AI reorder explanation là nội dung phụ; tạo request/confirm là action chính.

### Phiếu kho và chuyển kho

- Header chứng từ, tabs, line-item table, totals và action bar đồng bộ.
- Giữ partial, hidden input, row template, modal host và dynamic hooks.
- Trạng thái duyệt/xuất/nhận phải rõ.

### Quản lý đá theo ca

- Thể hiện tiến trình ca và vai trò người thao tác.
- Summary định mức, đã cấp, bổ sung, lý thuyết, chênh lệch, chi phí nổi bật.
- Chỉ action của bước hiện tại là primary.
- Không biến mọi chênh lệch thành danger.
- Report ưu tiên đọc/in, ít trang trí.

### Nhân sự, phân quyền, lịch ca

- Staff Create/Edit dùng cùng form token.
- Role, scope, store có hierarchy rõ.
- Permission matrix dễ quét; checkbox focus rõ.
- Lịch tuần có contrast cao cho nhân viên, ngày, ca và trạng thái.
- Không sửa drag/drop, dropdown hoặc modal JavaScript.

### Cửa hàng, Profile, Notification, Settings

- Store Index/Create/Edit cùng header/card/button.
- Không sửa Leaflet/map JavaScript.
- Notification chưa đọc có accent nhẹ.
- Profile rõ, không quá nhiều card cạnh tranh.
- Settings gọn và tập trung.

### Bán hàng, Voucher, Wheel

- Order History có KPI/filter/table cùng design system.
- Voucher modal CRUD đồng bộ.
- Wheel là form cấu hình Admin, không làm quá giống trò chơi.
- Không sửa SweetAlert hoặc JavaScript.

---

## 7. RESPONSIVE, ACCESSIBILITY VÀ CHỐNG CONFLICT

### 7.1. Viewport kiểm thử bắt buộc

- `1440×900`.
- `1280×720`.
- `1024×768`.
- `768×1024`.
- `390×844`.
- Zoom trình duyệt `100%` và `125%`.

### 7.2. Responsive

- KPI: 4 → 2 → 1 cột.
- Form: 2 → 1 cột.
- Header actions wrap.
- Filter grid giảm cột/stack.
- Table giữ DOM và cuộn ngang trong wrapper.
- Không ẩn cột/action nghiệp vụ nếu chưa có quyết định BA.
- Modal không mất footer.
- Không dùng font nhỏ hơn `12px` để nhét nội dung.

### 7.3. Accessibility

- Text thường đạt WCAG AA, mục tiêu `>= 4.5:1`.
- Large/bold text tối thiểu `>= 3:1`.
- `:focus-visible` rõ; không xóa outline nếu không có thay thế.
- Focus indicator tương đương `2–3px` và đủ tương phản.
- Không dùng màu là tín hiệu duy nhất.
- Icon-only button giữ `aria-label`/title hiện có.
- Không sửa hoặc xóa ARIA hiện tại.
- Hỗ trợ `prefers-reduced-motion`.

### 7.4. Chiến lược selector chống conflict

- Scope selector trong khu vực Admin.
- Ưu tiên `:is()`/`:where()` để nhóm class cũ.
- Không selector theo vị trí mơ hồ như `div > div > button`.
- Không dùng global `button {}`, `table {}`, `input {}` ngoài scope Admin.
- Không xóa token cũ; alias token cũ về `--cc-*`.
- Không lạm dụng `!important`; mỗi trường hợp bắt buộc phải có comment lý do/module.
- Không “dọn CSS” hàng loạt khi chưa visual regression.

---

## 8. QUY TRÌNH AUDIT, TRIỂN KHAI VÀ BÁO CÁO

### Trước khi chỉnh

1. Liệt kê chính xác file `.cshtml` và `.css` dự kiến chỉnh.
2. Chụp/ghi hiện trạng trang đại diện.
3. Ghi nhận `id`, `name`, `data-*`, `asp-*`, form action, modal/tab IDs và script references.
4. Xác định selector/class hiện có sẽ được map.
5. Báo rủi ro specificity, inline style, plugin control và responsive table.
6. Xác nhận không chỉnh JS/backend/database/seed/DOM tag structure.

### Trong khi chỉnh

1. Làm token/layout trước.
2. Chuẩn hóa header, button, form, card, table, badge, alert, modal.
3. Làm từng nhóm module; không chỉnh hàng loạt thiếu kiểm thử.
4. Sau mỗi nhóm, test chức năng và responsive.
5. Chỉ thêm exception CSS khi thật sự cần và ghi rõ module/lý do.

### Báo cáo sau khi chỉnh

1. Danh sách file `.cshtml` và `.css` đã chỉnh.
2. Xác nhận không file `.js`, backend, database hoặc seed thay đổi.
3. Xác nhận không thay cấu trúc thẻ DOM.
4. Bảng mapping component cũ → visual contract mới.
5. Bảng design token đã dùng.
6. Viewport đã test.
7. Chức năng đã regression test.
8. Exception CSS còn lại và lý do.
9. Ảnh before/after của trang đại diện.

---

## 9. CHECKLIST NGHIỆM THU CHUNG

### Functional freeze

- [ ] Không file `.js`, `.ts`, backend, SQL, database hoặc seed thay đổi.
- [ ] Không đổi controller/action/route.
- [ ] Không đổi form method/action, antiforgery hoặc hidden input.
- [ ] Không đổi `asp-*`, `id`, `name`, `data-*`, modal/tab/collapse IDs.
- [ ] Không đổi button `type`.
- [ ] Không đổi Razor permission/role condition.
- [ ] Không đổi label, trạng thái hoặc workflow nghiệp vụ.

### Visual consistency

- [ ] Page header cùng visual contract.
- [ ] Primary/secondary/ghost/danger đúng semantics.
- [ ] Input/select cùng height và focus ring.
- [ ] Table header/row/pagination đồng bộ.
- [ ] Badge cùng size và semantic colors.
- [ ] Modal cùng radius/padding/footer.
- [ ] Tiêu đề và nội dung chính không bị chìm.
- [ ] Không module nào dùng cam chói hoặc xanh Bootstrap làm primary ngoài semantic.

### Responsive/accessibility

- [ ] Không toàn trang scroll ngang.
- [ ] Table rộng scroll trong wrapper.
- [ ] Header actions wrap đúng.
- [ ] Form stack đúng.
- [ ] Modal không mất footer.
- [ ] Sidebar/main content không chồng nhau.
- [ ] Focus visible và text contrast đạt yêu cầu.
- [ ] Status không chỉ dựa vào màu.

---

## 10. NGUỒN THAM KHẢO

Các nguồn dùng để rút ra nguyên tắc, không sao chép markup hoặc chuyển framework:

1. Bootstrap 5.3 — button, form, table, grid và breakpoint:  
   https://getbootstrap.com/docs/5.3/components/buttons/  
   https://getbootstrap.com/docs/5.3/forms/overview/  
   https://getbootstrap.com/docs/5.3/forms/form-control/  
   https://getbootstrap.com/docs/5.3/forms/validation/  
   https://getbootstrap.com/docs/5.3/content/tables/  
   https://getbootstrap.com/docs/5.3/layout/grid/  
   https://getbootstrap.com/docs/5.3/layout/breakpoints/
2. AdminLTE — layout shell, sidebar và main content:  
   https://docs.adminlte.io/html/layout
3. Tabler — component admin Bootstrap, card, table, form, empty state:  
   https://docs.tabler.io/
4. Carbon Design System — data-heavy table, form và button hierarchy:  
   https://carbondesignsystem.com/components/data-table/usage/  
   https://carbondesignsystem.com/components/data-table/style/  
   https://carbondesignsystem.com/components/form/usage/  
   https://carbondesignsystem.com/components/button/usage/
5. Atlassian Design System — token, spacing và foundation:  
   https://atlassian.design/tokens/design-tokens  
   https://design-system-docs-proxy.services.atlassian.com/foundations/spacing  
   https://design-system-docs-proxy.services.atlassian.com/foundations/
6. Material Design 3 — hierarchy, field, card và interaction states:  
   https://m3.material.io/components/buttons  
   https://m3.material.io/components/text-fields  
   https://m3.material.io/components/cards  
   https://m3.material.io/foundations/interaction/states/applying-states
7. Ant Design — layout theo dashboard/list/detail/form:  
   https://2x.ant.design/docs/spec/layout
8. CoreUI — Bootstrap form layout:  
   https://coreui.io/bootstrap/docs/forms/layout/
9. W3C WCAG 2.2 — contrast và focus:  
   https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum  
   https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html  
   https://www.w3.org/WAI/WCAG22/Understanding/focus-visible.html  
   https://www.w3.org/WAI/WCAG22/Understanding/focus-appearance.html


## 11. PHÂN CÔNG FRONTEND 2 — DASHBOARD, VẬN HÀNH, MUA HÀNG, NHÂN SỰ VÀ HỆ THỐNG

### 11.1. Khối lượng được giao

- **57/118 file `.cshtml`**.
- **18/32 file CSS**, khoảng **16.164 dòng CSS**.
- Nhóm nghiệp vụ: Dashboard Admin, kho–mua hàng, nhận hàng, tồn kho, quản lý đá, nhân sự, phân quyền, lịch ca, cửa hàng, nhà cung cấp, cài đặt, bán hàng, voucher và wheel.

### 11.2. Vai trò sở hữu để tránh conflict

Frontend 2 chỉ chỉnh các view/CSS được liệt kê trong tài liệu này.

Frontend 2 **không chỉnh**:

- `Areas/Admin/Views/Shared/**`.
- `_ViewStart.cshtml`, `_ViewImports.cshtml`.
- `wwwroot/css/Admin/admin-unified-depth.css`.
- Các view/CSS thuộc Frontend 1.

Mọi nhu cầu thay token hoặc mapping toàn cục phải ghi trong báo cáo và chuyển Frontend 1 thực hiện. Frontend 2 dùng `procurement-design-system.css` làm lớp chuẩn hóa cho nhóm supply chain và CSS module được giao cho exception nghiệp vụ.

### 11.3. Trọng tâm nghiệp vụ

#### Dashboard Admin

- Giữ 6 nhóm Điều hành, POS/WorkShift, Kho, Mua hàng, Sản phẩm, Nhân sự và tab AI.
- Đây chỉ là Dashboard Admin; không chỉnh giao diện POS/StaffHub.
- Filter, KPI, chart, AI result, partial/no-data/error phải có hierarchy rõ.

#### Inventory & Procurement

- Chuỗi workflow phải dễ nhận biết: tồn/ngưỡng → cảnh báo → yêu cầu nhập → đề nghị mua → tổng hợp/batch → PO → receipt → nhập kho.
- Không làm mọi action thành primary.
- PO/receipt detail ưu tiên mã phiếu, nhà cung cấp, cửa hàng, trạng thái và giá trị.
- Không đổi JavaScript, Select2, modal, dynamic rows hoặc action workflow.

#### Operational Ice

- Timeline ca, định mức, cấp đầu ca, bổ sung, chốt ca, chênh lệch và chi phí phải rõ.
- Chỉ action hiện tại là primary; report tối ưu đọc/in.

#### Staff/Permission/Shift

- Staff form giữ 3 nhóm thông tin và modal hiện có.
- Permission matrix, scope store và role hierarchy dễ quét.
- Lịch ca có contrast cao; không sửa drag/drop/dropdown/modal JavaScript.

#### Store/Supplier/System/Sales

- Store CRUD đồng bộ; không sửa Leaflet/map JavaScript.
- Supplier và SupplierQuality dùng cùng form/table/status contract.
- Settings, Notifications, Profile gọn và rõ.
- Order History, Voucher, Wheel dùng cùng hệ button/form/table/modal; không sửa SweetAlert.

### 11.4. Điểm nghiệm thu riêng Frontend 2

- [ ] Dashboard filter/KPI/chart/AI vẫn hoạt động như cũ.
- [ ] Procurement workflow status/action thống nhất và đúng hierarchy.
- [ ] StoreInventory partial/table/transaction modal hoạt động như cũ.
- [ ] PO, receipt, restock, reorder, purchase advice không đổi workflow.
- [ ] Operational Ice timeline/action/report hoạt động như cũ.
- [ ] Staff/Permission/Shift không mất modal, focus, drag/drop hoặc scope.
- [ ] Store map, Voucher modal và SweetAlert hoạt động như cũ.
- [ ] Không chỉnh file thuộc quyền sở hữu Frontend 1.


## 12. QUY TẮC PHỐI HỢP HAI FRONTEND

### 12.1. Thứ tự tích hợp

1. Frontend 1 tạo commit nền tảng gồm token, canvas, sidebar và component contract cơ bản.
2. Frontend 2 pull/rebase commit nền tảng trước khi final polish.
3. Hai người chỉ chỉnh file thuộc danh sách của mình.
4. Không cùng sửa một CSS dùng chung.
5. Mỗi người tạo commit theo nhóm module, không gom toàn bộ thành một commit lớn.
6. Trước merge, chạy visual regression cả các trang đại diện của người còn lại để phát hiện selector lan phạm vi.

### 12.2. Quy tắc branch/commit gợi ý

- Frontend 1: `feature/admin-ui-core-product-documents`.
- Frontend 2: `feature/admin-ui-operations-hr-system`.
- Commit theo mẫu:
  - `style(admin-core): unify tokens and page shell`
  - `style(admin-product): align product CRUD views`
  - `style(admin-inventory-doc): align document workflow UI`
  - `style(admin-procurement): align purchasing workflow UI`
  - `style(admin-hr): align staff permission and shift UI`

### 12.3. Cấm conflict ownership

- Không sửa file của người còn lại để “tiện tay đồng bộ”.
- Không cherry-pick commit chứa file ngoài phạm vi mà chưa kiểm tra.
- Không format toàn file `.cshtml` vì có thể tạo diff lớn dù không đổi nghiệp vụ.
- Không đổi line ending hoặc chạy formatter hàng loạt.
- Khi phát hiện selector chung cần sửa, ghi rõ selector, lý do và ảnh hưởng; chuyển đúng owner xử lý.


## PHỤ LỤC A — DANH SÁCH FILE ĐƯỢC GIAO

### A.1. View `.cshtml` — 57 file

- `Areas/Admin/Views/AdminBranchReceipts/Create.cshtml`
- `Areas/Admin/Views/AdminBranchReceipts/Details.cshtml`
- `Areas/Admin/Views/AdminBranchReceipts/Index.cshtml`
- `Areas/Admin/Views/AdminBranchReceipts/PurchaseOrderDraft.cshtml`
- `Areas/Admin/Views/AdminIngredient/Index.cshtml`
- `Areas/Admin/Views/AdminInventoryThresholds/Index.cshtml`
- `Areas/Admin/Views/AdminNotifications/Index.cshtml`
- `Areas/Admin/Views/AdminOperationalAnomalies/Index.cshtml`
- `Areas/Admin/Views/AdminOperationalIce/Details.cshtml`
- `Areas/Admin/Views/AdminOperationalIce/Index.cshtml`
- `Areas/Admin/Views/AdminOperationalIce/Report.cshtml`
- `Areas/Admin/Views/AdminOrder/History.cshtml`
- `Areas/Admin/Views/AdminPermission/Index.cshtml`
- `Areas/Admin/Views/AdminProfile/MyProfile.cshtml`
- `Areas/Admin/Views/AdminPurchaseAdviceConsolidation/Index.cshtml`
- `Areas/Admin/Views/AdminPurchaseAdvices/Create.cshtml`
- `Areas/Admin/Views/AdminPurchaseAdvices/Details.cshtml`
- `Areas/Admin/Views/AdminPurchaseAdvices/Edit.cshtml`
- `Areas/Admin/Views/AdminPurchaseAdvices/Index.cshtml`
- `Areas/Admin/Views/AdminPurchaseOrderBatches/Details.cshtml`
- `Areas/Admin/Views/AdminPurchaseOrderBatches/Index.cshtml`
- `Areas/Admin/Views/AdminPurchaseOrders/Create.cshtml`
- `Areas/Admin/Views/AdminPurchaseOrders/Details.cshtml`
- `Areas/Admin/Views/AdminPurchaseOrders/Index.cshtml`
- `Areas/Admin/Views/AdminReorderSuggestions/Index.cshtml`
- `Areas/Admin/Views/AdminRestockRequests/CreateCentralPlanner.cshtml`
- `Areas/Admin/Views/AdminRestockRequests/CreateManual.cshtml`
- `Areas/Admin/Views/AdminRestockRequests/Details.cshtml`
- `Areas/Admin/Views/AdminRestockRequests/Index.cshtml`
- `Areas/Admin/Views/AdminSetting/Index.cshtml`
- `Areas/Admin/Views/AdminSetting/Partials/_NegativeInventorySettings.cshtml`
- `Areas/Admin/Views/AdminShiftOptimization/Index.cshtml`
- `Areas/Admin/Views/AdminStaff/Edit.cshtml`
- `Areas/Admin/Views/AdminStaff/Index.cshtml`
- `Areas/Admin/Views/AdminStaff/_CreateStaffModal.cshtml`
- `Areas/Admin/Views/AdminStaffShift/Index.cshtml`
- `Areas/Admin/Views/AdminStockAlerts/Details.cshtml`
- `Areas/Admin/Views/AdminStockAlerts/Index.cshtml`
- `Areas/Admin/Views/AdminStore/Create.cshtml`
- `Areas/Admin/Views/AdminStore/Edit.cshtml`
- `Areas/Admin/Views/AdminStore/Index.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Index.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_InventoryTablePartial.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_PaginationPartial.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_StoreTabsPartial.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_TransactionModalPartial.cshtml`
- `Areas/Admin/Views/AdminStoreInventory/Partials/_TransactionPartial.cshtml`
- `Areas/Admin/Views/AdminSupplier/Index.cshtml`
- `Areas/Admin/Views/AdminSupplierQuality/Create.cshtml`
- `Areas/Admin/Views/AdminSupplierQuality/Index.cshtml`
- `Areas/Admin/Views/AdminUnitConversion/Create.cshtml`
- `Areas/Admin/Views/AdminUnitConversion/Edit.cshtml`
- `Areas/Admin/Views/AdminUnitConversion/Index.cshtml`
- `Areas/Admin/Views/AdminVoucher/Index.cshtml`
- `Areas/Admin/Views/AdminWheel/Index.cshtml`
- `Areas/Admin/Views/Dashboard/Guide.cshtml`
- `Areas/Admin/Views/Dashboard/Index.cshtml`

### A.2. CSS — 18 file. khoảng 16.164 dòng

- `wwwroot/css/Admin/Dashboard/dashboard.css`
- `wwwroot/css/Admin/Ingredient/ingredient.css`
- `wwwroot/css/Admin/InventoryOperations/inventory-operations.css`
- `wwwroot/css/Admin/Notifications/admin-notifications.css`
- `wwwroot/css/Admin/OperationalIce/operational-ice.css`
- `wwwroot/css/Admin/Permissions/admin-permissions.css`
- `wwwroot/css/Admin/Procurement/procurement-design-system.css`
- `wwwroot/css/Admin/Procurement/reorder-suggestions.css`
- `wwwroot/css/Admin/Profile/admin-profile.css`
- `wwwroot/css/Admin/PurchaseAdvice/purchase-advice.css`
- `wwwroot/css/Admin/Settings/negative-inventory.css`
- `wwwroot/css/Admin/Staff/staff.css`
- `wwwroot/css/Admin/StaffShift/admin-staff-shift.css`
- `wwwroot/css/Admin/StaffShift/shift-optimization.css`
- `wwwroot/css/Admin/Store/store-admin.css`
- `wwwroot/css/Admin/StoreInventory/storeinventory.css`
- `wwwroot/css/Admin/Supplier/supplier.css`
- `wwwroot/css/unit-conversion.css`

### A.3. Quy tắc ownership

- Chỉ chỉnh các file trong danh sách trên.
- Không chỉnh file của frontend còn lại.
- Không đổi tên. di chuyển hoặc tạo bản sao file để né ownership.
- Nếu phát hiện cần sửa file ngoài phạm vi. ghi vào báo cáo handoff; không tự sửa.

---

## 13. PROMPT HOÀN CHỈNH ĐỂ ĐƯA CHO ANTIGRAVITY

> Sao chép nguyên khối prompt dưới đây. Prompt đã khóa phạm vi theo đúng phần việc của frontend này.

```text
Bạn là Senior Frontend Engineer kiêm UI Design System Engineer. Hãy đọc kỹ dự án CafeChain trước khi chỉnh sửa. Dự án dùng ASP.NET Core MVC/Razor Areas, Bootstrap 5.3.2, Select2, Font Awesome và font Inter.

MỤC TIÊU
Làm lại và đồng bộ phần giao diện Admin được giao theo phong cách coffee-brown premium: sáng, chuyên nghiệp, có chiều sâu vừa phải, làm nổi bật page title, KPI, nội dung nghiệp vụ, form CRUD, bảng, trạng thái và hành động chính. Kết quả phải hòa vào cùng một design system toàn Admin, không được tạo một phong cách riêng cho từng module.

============================================================
RÀNG BUỘC TUYỆT ĐỐI — VI PHẠM MỘT MỤC LÀ KHÔNG ĐẠT
============================================================

1. CHỈ ĐƯỢC CHỈNH CÁC FILE .CSHTML VÀ .CSS NẰM TRONG DANH SÁCH ĐƯỢC GIAO.
2. NGHIÊM CẤM CHỈNH BẤT KỲ JAVASCRIPT NÀO.
3. NGHIÊM CẤM CHỈNH BACKEND, DATABASE, MIGRATION, SQL, SEED, API, PERMISSION VÀ NGHIỆP VỤ.
4. KHÔNG ĐƯỢC THÊM, XÓA, ĐỔI TÊN, THAY THẾ HOẶC DI CHUYỂN CẤU TRÚC THẺ HTML/RAZOR TRONG .CSHTML.
5. KHÔNG ĐƯỢC ĐỔI/XÓA id, name, for, value, type, role, aria-*, asp-*, data-*, event inline, form action/method, antiforgery, hidden input, modal/tab/collapse/drawer/table/form IDs.
6. KHÔNG ĐƯỢC ĐỔI THỨ TỰ DOM NẾU CÓ THỂ ẢNH HƯỞNG JS, binding, validation, focus hoặc stacking context.
7. KHÔNG THAY TABLE BẰNG CARD, FORM FULL-PAGE BẰNG MODAL, INPUT BẰNG COMPONENT KHÁC HOẶC THAY ĐỔI LUỒNG.
8. KHÔNG THÊM FRAMEWORK UI, JAVASCRIPT LIBRARY HOẶC PACKAGE MỚI.
9. KHÔNG ĐỔI LABEL, TRẠNG THÁI, SỐ LIỆU, QUYỀN, WORKFLOW HOẶC HÀNH ĐỘNG SUBMIT.
10. Khi cần thêm class để style, phải giữ toàn bộ class cũ và chứng minh class mới không ảnh hưởng JavaScript.
11. Không chỉnh StaffHub hoặc ứng dụng POS riêng.
12. Không tự ý dọn code hoặc format hàng loạt ngoài phạm vi giao diện.

Trước khi chỉnh, phải trả báo cáo read-only gồm:
- Danh sách file dự kiến chỉnh.
- Selector/class hiện có sẽ được map.
- Xác nhận không chỉnh JS/backend/database/seed/DOM.
- Rủi ro specificity, inline style, plugin control và responsive.
- Kế hoạch test chức năng hiện tại.

============================================================
DESIGN SYSTEM BẮT BUỘC
============================================================

Màu:
- Brown 950 #2B1A12; Brown 900 #3D2418; Brown 800 #4D3021.
- Brown 700 #5C3F2B; Primary Brown 600 #70482F; Brown 500 #8B6247.
- Caramel #A97750; Caramel soft #C99E7D; Brown 200 #DFC5B1.
- Brown 100 #F0E2D6; Brown 50 #FAF6F2.
- Canvas #F7F4F0; Surface #FFFDFB; Raised #FFFFFF; Muted #FBF7F2.
- Active #F4E9DF; Border #E9DED4; Strong border #D8C5B6.
- Text #201812; secondary #66584F; muted #7A6C62; disabled #A69B93.
- Success #2F6F5E; Warning #99623B; Danger #991B1B; Info #3F5F7A; Neutral #64748B.

Typography:
- Giữ Inter.
- Page title 32–38px desktop, 24–28px mobile, weight 800.
- Section 18–20px; card title 15–16px; body 14px; label 13px; table header 12px.
- Dùng tabular-nums cho tiền, số lượng, tỷ lệ, KPI và mã phiếu.

Spacing/radius/shadow:
- Spacing 4/8/12/16/20/24/32/40/48/64px.
- Control radius 10px; card 16px; header 20px; modal 18px.
- Card padding 20–24px; field gap 16px; section gap 20–24px; page gap 24–32px.
- Shadow mềm, không đen nặng; hover nâng tối đa 1–2px.

Button:
- Small 36px; default 44px; large 48px; icon 40×40px; table action 32–36px.
- Primary #70482F, hover #3D2418, chữ trắng.
- Secondary trắng/kem + border; Ghost cho action phụ; Danger chỉ destructive.
- Một panel chỉ có một primary cùng cấp.

Form:
- Input/select 44px; radius 10px; border #D8C5B6.
- Focus #70482F + ring 0 0 0 3px rgba(112,72,47,.22).
- Textarea 108–120px; Select2 phải khớp input.
- Desktop 2 cột khi hợp lý; mobile 1 cột.

Table:
- Header 42–46px; row 52px; compact 46–48px.
- Padding 12px 16px; compact 10px 12px.
- Numeric căn phải; action căn phải; không center toàn table.
- Giữ DOM; table rộng scroll trong wrapper.

Modal:
- Small 440–480px; default 620–680px; large 840–920px; XL 1080–1180px.
- Radius 18px; body scroll; footer luôn thấy.
- Giữ nguyên ID/data-bs/JavaScript.

Responsive/accessibility:
- Test 1440×900, 1280×720, 1024×768, 768×1024, 390×844; zoom 100%/125%.
- KPI 4→2→1; form 2→1; header action wrap; filter stack.
- WCAG AA; focus-visible rõ; không truyền trạng thái chỉ bằng màu.
- Hỗ trợ prefers-reduced-motion.

============================================================
CHIẾN LƯỢC CSS
============================================================

- Scope selector trong Admin.
- Dùng :is()/:where() để map class hiện có về cùng visual contract.
- Không selector mơ hồ theo vị trí DOM.
- Không global button/table/input ngoài scope Admin.
- Không xóa token cũ; alias về --cc-*.
- Không lạm dụng !important; exception phải có comment module/lý do.
- Không xóa CSS cũ hàng loạt; làm từng nhóm và visual regression.

============================================================
PHẠM VI FILE ĐƯỢC GIAO CHO FRONTEND 2
============================================================

VIEW .CSHTML:
- Areas/Admin/Views/AdminBranchReceipts/Create.cshtml
- Areas/Admin/Views/AdminBranchReceipts/Details.cshtml
- Areas/Admin/Views/AdminBranchReceipts/Index.cshtml
- Areas/Admin/Views/AdminBranchReceipts/PurchaseOrderDraft.cshtml
- Areas/Admin/Views/AdminIngredient/Index.cshtml
- Areas/Admin/Views/AdminInventoryThresholds/Index.cshtml
- Areas/Admin/Views/AdminNotifications/Index.cshtml
- Areas/Admin/Views/AdminOperationalAnomalies/Index.cshtml
- Areas/Admin/Views/AdminOperationalIce/Details.cshtml
- Areas/Admin/Views/AdminOperationalIce/Index.cshtml
- Areas/Admin/Views/AdminOperationalIce/Report.cshtml
- Areas/Admin/Views/AdminOrder/History.cshtml
- Areas/Admin/Views/AdminPermission/Index.cshtml
- Areas/Admin/Views/AdminProfile/MyProfile.cshtml
- Areas/Admin/Views/AdminPurchaseAdviceConsolidation/Index.cshtml
- Areas/Admin/Views/AdminPurchaseAdvices/Create.cshtml
- Areas/Admin/Views/AdminPurchaseAdvices/Details.cshtml
- Areas/Admin/Views/AdminPurchaseAdvices/Edit.cshtml
- Areas/Admin/Views/AdminPurchaseAdvices/Index.cshtml
- Areas/Admin/Views/AdminPurchaseOrderBatches/Details.cshtml
- Areas/Admin/Views/AdminPurchaseOrderBatches/Index.cshtml
- Areas/Admin/Views/AdminPurchaseOrders/Create.cshtml
- Areas/Admin/Views/AdminPurchaseOrders/Details.cshtml
- Areas/Admin/Views/AdminPurchaseOrders/Index.cshtml
- Areas/Admin/Views/AdminReorderSuggestions/Index.cshtml
- Areas/Admin/Views/AdminRestockRequests/CreateCentralPlanner.cshtml
- Areas/Admin/Views/AdminRestockRequests/CreateManual.cshtml
- Areas/Admin/Views/AdminRestockRequests/Details.cshtml
- Areas/Admin/Views/AdminRestockRequests/Index.cshtml
- Areas/Admin/Views/AdminSetting/Index.cshtml
- Areas/Admin/Views/AdminSetting/Partials/_NegativeInventorySettings.cshtml
- Areas/Admin/Views/AdminShiftOptimization/Index.cshtml
- Areas/Admin/Views/AdminStaff/Edit.cshtml
- Areas/Admin/Views/AdminStaff/Index.cshtml
- Areas/Admin/Views/AdminStaff/_CreateStaffModal.cshtml
- Areas/Admin/Views/AdminStaffShift/Index.cshtml
- Areas/Admin/Views/AdminStockAlerts/Details.cshtml
- Areas/Admin/Views/AdminStockAlerts/Index.cshtml
- Areas/Admin/Views/AdminStore/Create.cshtml
- Areas/Admin/Views/AdminStore/Edit.cshtml
- Areas/Admin/Views/AdminStore/Index.cshtml
- Areas/Admin/Views/AdminStoreInventory/Index.cshtml
- Areas/Admin/Views/AdminStoreInventory/Partials/_InventoryTablePartial.cshtml
- Areas/Admin/Views/AdminStoreInventory/Partials/_PaginationPartial.cshtml
- Areas/Admin/Views/AdminStoreInventory/Partials/_StoreTabsPartial.cshtml
- Areas/Admin/Views/AdminStoreInventory/Partials/_TransactionModalPartial.cshtml
- Areas/Admin/Views/AdminStoreInventory/Partials/_TransactionPartial.cshtml
- Areas/Admin/Views/AdminSupplier/Index.cshtml
- Areas/Admin/Views/AdminSupplierQuality/Create.cshtml
- Areas/Admin/Views/AdminSupplierQuality/Index.cshtml
- Areas/Admin/Views/AdminUnitConversion/Create.cshtml
- Areas/Admin/Views/AdminUnitConversion/Edit.cshtml
- Areas/Admin/Views/AdminUnitConversion/Index.cshtml
- Areas/Admin/Views/AdminVoucher/Index.cshtml
- Areas/Admin/Views/AdminWheel/Index.cshtml
- Areas/Admin/Views/Dashboard/Guide.cshtml
- Areas/Admin/Views/Dashboard/Index.cshtml

CSS:
- wwwroot/css/Admin/Dashboard/dashboard.css
- wwwroot/css/Admin/Ingredient/ingredient.css
- wwwroot/css/Admin/InventoryOperations/inventory-operations.css
- wwwroot/css/Admin/Notifications/admin-notifications.css
- wwwroot/css/Admin/OperationalIce/operational-ice.css
- wwwroot/css/Admin/Permissions/admin-permissions.css
- wwwroot/css/Admin/Procurement/procurement-design-system.css
- wwwroot/css/Admin/Procurement/reorder-suggestions.css
- wwwroot/css/Admin/Profile/admin-profile.css
- wwwroot/css/Admin/PurchaseAdvice/purchase-advice.css
- wwwroot/css/Admin/Settings/negative-inventory.css
- wwwroot/css/Admin/Staff/staff.css
- wwwroot/css/Admin/StaffShift/admin-staff-shift.css
- wwwroot/css/Admin/StaffShift/shift-optimization.css
- wwwroot/css/Admin/Store/store-admin.css
- wwwroot/css/Admin/StoreInventory/storeinventory.css
- wwwroot/css/Admin/Supplier/supplier.css
- wwwroot/css/unit-conversion.css

TUYỆT ĐỐI KHÔNG CHỈNH FILE NGOÀI DANH SÁCH TRÊN.

TRÁCH NHIỆM CHÍNH
- Đồng bộ Dashboard Admin.
- Đồng bộ Inventory & Procurement: tồn kho, ngưỡng, cảnh báo, restock, reorder, purchase advice, consolidation/batch, PO, receipt.
- Đồng bộ Operational Ice.
- Đồng bộ Staff, Permission, StaffShift, ShiftOptimization.
- Đồng bộ Store, Supplier, SupplierQuality, Ingredient, UnitConversion.
- Đồng bộ Profile, Notification, Settings, Order History, Voucher và Wheel.
- Không chỉnh Shared, _ViewStart, _ViewImports hoặc admin-unified-depth.css của Frontend 1.


YÊU CẦU RIÊNG
- Dùng procurement-design-system.css và CSS module được giao cho supply-chain mapping; không tạo token cạnh tranh với --cc-*.
- Dashboard giữ 6 nhóm dữ liệu + AI, chart/tab/filter hooks.
- Procurement phải thể hiện đúng workflow và status/action hierarchy.
- StoreInventory giữ partial, transaction modal và pagination hooks.
- Operational Ice giữ timeline/action/report và print readability.
- Staff/Permission/Shift giữ modal, scope, checkbox, drag/drop/dropdown hooks.
- Store giữ Leaflet/map hooks; Voucher/Wheel giữ SweetAlert/JavaScript hooks.
- Regression test tất cả modal, Select2, chart, map, scheduler và workflow thuộc phạm vi.


============================================================
OUTPUT BẮT BUỘC SAU KHI LÀM
============================================================

1. Danh sách chính xác file `.cshtml` và `.css` đã chỉnh.
2. Xác nhận không có file `.js`, backend, database hoặc seed thay đổi.
3. Xác nhận không thay cấu trúc thẻ DOM.
4. Bảng mapping component cũ → visual contract mới.
5. Bảng token đã sử dụng.
6. Danh sách viewport đã test.
7. Danh sách chức năng đã regression test.
8. Exception CSS còn lại và lý do.
9. Không tuyên bố hoàn thành nếu chưa kiểm thử các trang có modal, Select2, dynamic rows, chart, map, drag/drop hoặc workflow.

KẾT QUẢ CUỐI CÙNG
- Phần việc được giao phải hòa vào một hệ thống Admin duy nhất.
- Màu nâu sang trọng, nền kem sáng, tiêu đề và nội dung chính nổi bật.
- Button/input/table/modal cùng size và semantics.
- Chức năng, DOM, JavaScript và backend giữ nguyên.
- Không conflict với module của frontend còn lại.
```

---

## 14. KẾT LUẬN CHỐT CHO FRONTEND 2

- Khối lượng được chia theo cả số view và độ nặng CSS, không chỉ chia số folder.
- Frontend 2 chịu trách nhiệm **57/118 view** và **18/32 CSS**, khoảng **16.164 dòng CSS**.
- Mọi thay đổi chỉ nằm trong `.cshtml` và `.css` được giao.
- JavaScript, backend, database, seed, nghiệp vụ và DOM structure bị khóa tuyệt đối.
- Tiêu chí hoàn thành là giao diện đồng bộ, dễ đọc, có chiều sâu, không conflict và chức năng hiện tại hoạt động nguyên vẹn.
