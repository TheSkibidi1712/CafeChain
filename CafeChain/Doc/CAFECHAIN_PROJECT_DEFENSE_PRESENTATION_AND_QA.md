# Nội dung bảo vệ đồ án CafeChain và bộ câu hỏi hội đồng

> Phạm vi trình bày bám theo source code hiện tại. Những phần đang pilot, feature OFF hoặc chưa có UI hoàn chỉnh được ghi rõ; không xem là tính năng production hoàn chỉnh.

## 1. Tóm tắt dự án

CafeChain giải quyết bài toán quản lý chuỗi cửa hàng đồ uống có nhiều chi nhánh: bán hàng tại quầy, ca làm việc và tiền két, tồn kho, mua hàng, nhà cung cấp, nhân sự, Dashboard và hỗ trợ phân tích. Đối tượng sử dụng gồm Business Owner, Area Manager, Store Manager, Accountant/Warehouse, Shift Supervisor, Sales Staff, System Admin và khách hàng.

Các module chính:

- StaffHub, Terminal và POS: mở/đóng ca, OTP hoặc phê duyệt ngoại lệ, Current Operator, order, payment và đối soát.
- Kho và mua hàng: tồn kho, gợi ý nhập, Purchase Advice, chọn nguồn cung, PO/batch, Receipt và chất lượng nhà cung cấp.
- Dashboard/RBAC: một Dashboard chung, section/widget theo permission và StaffScope.
- AI/Analytics: AI Dashboard dựa trên EvidencePack, giải thích gợi ý nhập, Tín hiệu vận hành và Supplier Intelligence deterministic.
- Quản trị nền: tài khoản, role, permission, account override, Store assignment, master data, menu/BOM và audit.

Kiến trúc triển khai:

```text
Razor MVC Admin/StaffHub + React/Vite POS
                 ↓ HTTP/JSON + SignalR
ASP.NET Core 8 Controller/API
                 ↓
Authentication → Authorization → Validation → Application Service
                 ↓
Repository/EF Core 8 + Stored procedure/read model
                 ↓
SQL Server

Provider phụ trợ: Ollama, Pexels/ComfyUI, PayOS, email SMTP
```

Cookie được dùng cho web MVC; JWT có session/JTI được dùng cho POS API. SignalR đồng bộ order/payment, WorkShift, Current Operator và notification. SQL Server và backend là nguồn dữ liệu authoritative; frontend không tự suy luận quyền hay số liệu nghiệp vụ.

## 2. Role, RBAC và StaffScope

Runtime authorization không dựa trực tiếp vào tên role mà theo:

```text
Permission vào ứng dụng
+ permission module/widget/action
+ account override (Deny ưu tiên)
+ StaffScope được gán thật
```

Role chỉ là bộ quyền mặc định trong `Scripts/SeedAll.sql`. Business Owner/Area Manager/Store Manager có quyền nghiệp vụ theo phạm vi được gán; Accountant/Warehouse tập trung tài chính, kho và mua hàng; Shift Supervisor/Sales Staff dùng StaffHub/POS; System Admin là role kỹ thuật và không mặc định thấy dữ liệu kinh doanh. Sửa URL sang Store khác phải bị backend trả 403 trước khi query nghiệp vụ.

## 3. Năm nghiệp vụ trọng tâm

### 3.1 StaffHub → mở POS → WorkShift/OTP/approval

**Người dùng muốn làm gì?** Nhân viên muốn mở đúng terminal, đúng lịch và chỉ bắt đầu chịu trách nhiệm két sau khi xác nhận tiền đầu ca.

```text
StaffHub preview
→ Cookie authentication + App.POS/POS.WorkShift permission
→ kiểm StaffScope, Terminal, lịch và WorkShift active
→ đúng lịch: cấp exchange context
→ ngoài lịch: OTP cho approver đúng quyền/scope
→ trễ 30–45 phút: Manager được duyệt/từ chối/chuyển ngoài lịch
→ trễ >45 phút: chỉ từ chối hoặc chuyển ngoài lịch, không OTP
→ React POS nhận exchange context
→ POST /api/v1/pos/shifts/open
→ revalidate + transaction + idempotency
→ tạo đúng một WorkShift, bind POS session
→ SignalR cập nhật các tab
```

API đáng nói là preview/issue context tại `StaffHubController`, OTP `/api/v1/otp/*`, quyết định mở ca trễ tại `AdminWorkShiftOpenApprovals`, và commit `POST /api/v1/pos/shifts/open`. Preview và IssuePosToken không tạo WorkShift. Nút hủy chỉ hủy intent/session/challenge chưa dùng; không xóa lịch sử audit. Double-click dùng RequestKey và unique active constraint để không tạo hai ca.

Trường hợp đặc biệt:

- Trễ dưới 30 phút xử lý lý do theo policy; từ 30 phút dùng `WorkShiftOpenApprovalRequest`.
- Ngoài lịch dùng OTP ưu tiên Shift Supervisor có `ApproveOutsideSchedule`, sau đó mới fallback Manager.
- Terminal registration là luồng OTP riêng: Store Manager **Xem OTP → Xác nhận Terminal**.
- WorkShift chỉ được tạo khi người dùng bấm **Xác nhận mở ca** với tiền đầu ca.
- Current Operator có thể đổi bằng PIN nhưng `WorkShift.UserId` và trách nhiệm két không đổi.

**Phần em trực tiếp thực hiện:** boundary commit WorkShift, cancellation, OTP dual-channel, UTC/countdown, typed notification, Current Operator/PIN, idempotency, permission/scope và SignalR đồng bộ.

**Khó khăn:** nhiều trạng thái có thể race giữa hủy, duyệt, resend và mở ca. Cách giải quyết là context có hạn, RequestKey, row-version/unique index, transaction và backend revalidate ngay trước commit.

### 3.2 Dashboard chung theo RBAC và StaffScope

**Người dùng muốn làm gì?** Mỗi vai trò mở cùng một Dashboard nhưng chỉ thấy section/widget và Store được phép.

```text
GET /Admin/Dashboard
→ cookie authentication
→ App.AdminDashboard
→ effective permission + account Deny
→ IDashboardAuthorizationService
→ AllowedSections/Widgets/Capabilities + Scope
→ validate Province/District/Store/date
→ chỉ gọi repository cho widget đã được phép
→ DTO + Razor/JavaScript render
```

Business logic thực sự là permission ở cấp entry/section/widget/action, StaffScope deny-by-default, aggregate chỉ trên Store hợp lệ và financial/staff widget có permission nhạy cảm riêng. Frontend chỉ là lớp hiển thị; direct API vẫn bị chặn.

**Phần em trực tiếp thực hiện:** service authorization tập trung, permission mapping, backend guard trước query, DTO quyền cho UI, filter scope, SeedAll least privilege và AI dùng lại cùng authorization model.

**Khó khăn:** một tab có nhiều widget độ nhạy khác nhau. Giải pháp là không dùng `App.AdminDashboard` như quyền xem tất cả mà tách `AllowedWidgets` và capability.

### 3.3 Purchase Advice → Supplier → PO → Receipt

**Người dùng muốn làm gì?** Từ nhu cầu thiếu nguyên liệu, người phụ trách muốn chọn nguồn cung minh bạch, tạo PO và nhận hàng mà không mua vượt quyền hoặc sai quy đổi.

```text
Purchase Advice UNDER_REVIEW
→ CompareSuppliers
→ auth: PurchaseAdvice.View + SupplierQuality.View + StaffScope + feature gate
→ lọc supplier/offer active
→ quy đổi base unit + MOQ/quantity step
→ hiệu suất receipt 180 ngày
→ score/confidence/rankability deterministic
→ modal so sánh
→ người dùng chọn offer
→ backend tái kiểm tra khi tạo PO/batch
→ approve theo permission
→ Receipt confirm cập nhật tồn và audit trong transaction
```

Supplier Intelligence không để LLM chọn nhà cung cấp. Backend tính giá, số gói, lượng mua, lượng dư, total cost, score v1 và confidence. Missing metric giữ null; ít hơn hai candidate rankable không gọi là “tốt nhất”. Pilot hiện chỉ bật ShadowMode tại CafeChain Thủ Dầu Một; mua thủ công vẫn hoạt động.

**Phần em trực tiếp thực hiện:** workflow Purchase Advice/PO/Receipt, scope/permission guard, conversion/MOQ, ranking deterministic, pilot feature gate từ `SystemSettings`, UI modal và audit snapshot.

**Khó khăn:** khác đơn vị mua và đơn vị tồn dễ làm sai chi phí/tồn. Giải pháp là quy đổi về base quantity ở backend và tái kiểm tra dữ liệu tại thời điểm tạo chứng từ.

### 3.4 AI Dashboard có EvidencePack

**Người dùng muốn làm gì?** Owner/Manager đặt câu hỏi về dữ liệu Dashboard mà không để AI tự truy cập database hoặc bịa số.

```text
Câu hỏi
→ POST Dashboard/Analyze + antiforgery
→ App.AdminDashboard + Dashboard.AI.Use
→ classify domain/focus
→ kiểm widget permission + StaffScope
→ DataPlan từ catalog cố định
→ repository lấy structured data
→ EvidencePack có id/metric/filter
→ Ollama structured JSON + validator
→ nếu lỗi: deterministic fallback cùng evidence
→ chart/table/proof points
```

AI không sinh SQL, không sửa dữ liệu, không tạo/duyệt PO và không mở rộng scope. Numeric claim/entity ngoài EvidencePack bị từ chối. Cache context được authorize lại để tránh dùng evidence cũ sau khi quyền đổi.

**Phần em trực tiếp thực hiện:** question catalog, DataPlan/widget catalog, EvidencePack, authorization-before-query, structured skill/schema, validation, fallback và UI chống stale response.

**Khó khăn:** vừa muốn câu trả lời dễ đọc vừa phải grounded. Cách giải quyết là backend tạo facts trước; LLM chỉ diễn đạt và validator so lại dữ liệu.

### 3.5 Operational Anomaly và giải thích dễ hiểu

**Người dùng muốn làm gì?** Quản lý muốn phát hiện sớm thay đổi vận hành đáng chú ý nhưng không biến tín hiệu thống kê thành cáo buộc.

```text
Worker chọn Store qua feature gate
→ BusinessDate Việt Nam hoàn chỉnh
→ observation thật, missing không thành zero
→ baseline 28 ngày, tối thiểu 14 mẫu
→ median/MAD + ngưỡng tuyệt đối/tương đối
→ upsert theo Store+Date+Metric+Version
→ notification đúng permission/scope
→ OPEN → ACKNOWLEDGED → RESOLVED + feedback
→ AI/fallback chỉ giải thích authorized evidence
```

UI Việt hóa tên chỉ số và trạng thái; mã kỹ thuật nằm trong vùng mở rộng. Nội dung giải thích gồm phát hiện, mức thường thấy, dữ liệu nên kiểm tra và cảnh báo chưa đủ cơ sở kết luận nguyên nhân/trách nhiệm.

**Phần em trực tiếp thực hiện:** BusinessDate, missing-data rules, scoring/state/idempotency, permission/action scope, notification/audit, mapping tiếng Việt, safe DOM modal và fallback.

**Khó khăn:** dữ liệu thiếu có thể bị hiểu nhầm thành doanh thu bằng 0. Giải pháp là chỉ đếm observation thật và không phát hiện khi baseline chưa đủ mẫu.

## 4. CRUD, business logic và kỹ thuật đáng trình bày

| Loại | Ví dụ | Có nên nhấn mạnh? |
|---|---|---|
| CRUD thường | tạo/sửa category, size, topping, xem danh sách supplier | Giới thiệu ngắn để chứng minh module đầy đủ |
| Business logic | mở ca đúng lịch/ngoài lịch, state PA/PO/Receipt, MOQ/conversion, anomaly, authorization theo widget | Nên trình bày sâu |
| Kỹ thuật | cookie + JWT session, RequestKey, transaction, row-version, SignalR, EvidencePack, fallback | Nên gắn với vấn đề nghiệp vụ, không đọc tên công nghệ rời rạc |

## 5. Nội dung slide và lời thoại

### Slide 1 — CafeChain: quản lý vận hành chuỗi cửa hàng

Nội dung trên slide:

- POS và trách nhiệm ca/két
- Kho, mua hàng và nhà cung cấp
- Dashboard theo quyền và phạm vi
- AI chỉ giải thích dữ liệu backend

Lời thoại: “CafeChain là hệ thống quản lý chuỗi cửa hàng đồ uống. Em tập trung giải quyết ba nhóm khó nhất: StaffHub/POS và trách nhiệm két, Dashboard/RBAC/AI, và chuỗi mua hàng từ đề nghị đến nhận hàng. Nguyên tắc xuyên suốt là backend quyết định quyền, phạm vi và số liệu; AI chỉ hỗ trợ giải thích.”

### Slide 2 — Bài toán thực tế

Nội dung trên slide:

- Nhiều Store, nhiều role, dữ liệu nhạy cảm
- Ca mở sai và thao tác chung terminal
- Quy đổi/MOQ làm sai chi phí mua
- Dashboard/AI có nguy cơ vượt quyền

Lời thoại: “Khó khăn không nằm ở CRUD mà ở quan hệ giữa người dùng, Store và trạng thái. Một nhân viên có thể mở ngoài lịch, một terminal có thể bị tranh chấp, một nhà cung cấp có quy cách khác đơn vị tồn, còn Dashboard và AI phải tuyệt đối không đọc dữ liệu ngoài scope.”

### Slide 3 — Kiến trúc hệ thống

Nội dung trên slide:

- Razor MVC cho Admin/StaffHub
- React + Vite cho POS
- ASP.NET Core 8 API/service
- EF Core + SQL Server
- SignalR và provider ngoài

Sơ đồ: dùng sơ đồ kiến trúc tại mục 1.

Lời thoại: “Giao diện quản trị và StaffHub dùng Razor MVC, còn POS là React build bằng Vite. Controller nhận HTTP, service xử lý quyền và nghiệp vụ, repository/EF Core làm việc với SQL Server. SignalR đồng bộ trạng thái nhanh; Ollama, email, PayOS và ảnh là provider phụ, không phải nguồn sự thật.”

### Slide 4 — Authentication và authorization

Nội dung trên slide:

- Cookie cho web, JWT session cho POS
- Permission thay cho role hard-code
- Account Deny ưu tiên Allow
- StaffScope giới hạn Store

Lời thoại: “Em tách authentication và authorization. Đăng nhập web dùng cookie; POS dùng JWT nhưng mỗi request còn kiểm session/JTI còn active. Sau đó backend kiểm effective permission và StaffScope. Role chỉ để seed nhóm quyền, còn runtime không viết if role để suy ra quyền dữ liệu.”

### Slide 5 — Luồng mở ca POS

Nội dung trên slide:

- Preview read-only
- OTP ngoài lịch; ca trễ 30–45 phút cần Manager, trên 45 phút khóa duyệt lịch cũ
- WorkShift chỉ tạo khi xác nhận tiền đầu ca
- RequestKey + transaction chống double-click

Sơ đồ: `StaffHub → context → React POS → open transaction → WorkShift`.

Lời thoại: “Điểm em sửa quan trọng là preview và cấp context không được tạo WorkShift sớm. Nếu trễ từ 30 đến 45 phút, Manager có thể duyệt, từ chối hoặc chuyển ngoài lịch; trên 45 phút chỉ còn từ chối hoặc chuyển ngoài lịch. Nếu người dùng hủy trước tiền đầu ca, session và context bị vô hiệu mà không tạo WorkShift. Chỉ khi xác nhận tiền đầu ca, backend mới tạo đúng một WorkShift.”

### Slide 6 — OTP, Terminal và mở ca trễ

Nội dung trên slide:

- OTP bind action/requester/approver/Store/Terminal
- SMTP chỉ là kênh phụ
- Terminal: Xem OTP → Xác nhận Terminal
- Ca trễ 30–45 phút: Manager được duyệt; trên 45 phút chỉ từ chối/chuyển ngoài lịch

Lời thoại: “OTP không chỉ là sáu ký tự. Challenge được bind đúng ngữ cảnh, có hạn, cooldown, giới hạn sai và không xuất hiện trong SignalR hay log. Email lỗi thì notification nội bộ vẫn dùng được. Riêng Terminal cần Manager xác nhận; còn mở ca trễ dùng hàng đợi quyết định, không dùng OTP.”

### Slide 7 — Current Operator

Nội dung trên slide:

- Đổi người thao tác bằng PIN cá nhân
- Không đóng/mở lại két
- Order/Payment ghi người thực hiện thật
- Người chịu trách nhiệm két không đổi

Lời thoại: “Current Operator giải quyết trường hợp nhiều nhân viên thao tác tại cùng quầy. Sau khi xác minh PIN, giao dịch mới ghi đúng nhân viên thực hiện. Tuy nhiên WorkShift.UserId, tiền đầu ca và trách nhiệm tài chính vẫn thuộc người mở két. Đây là bàn giao thao tác, không phải bàn giao két.”

### Slide 8 — Một Dashboard, nhiều quyền

Nội dung trên slide:

- Entry → section → widget → action
- StaffScope trước repository
- UI nhận AllowedWidgets/Capabilities
- Direct URL vẫn 403

Lời thoại: “Em giữ một Dashboard chung nhưng không xem quyền vào Dashboard là quyền xem tất cả. Backend tính section, widget và capability theo permission thật. Store/filter được kiểm trước query, nên ẩn tab chỉ là UX; bảo mật thật vẫn nằm ở API.”

### Slide 9 — AI Dashboard grounded

Nội dung trên slide:

- Classify trước khi lấy dữ liệu
- DataPlan cố định, không SQL động
- EvidencePack giới hạn số liệu
- Ollama lỗi vẫn có fallback

Lời thoại: “AI Dashboard không cho model truy cập database. Backend hiểu câu hỏi, kiểm domain và scope rồi mới dựng DataPlan từ catalog. Dữ liệu được đóng thành EvidencePack; model chỉ diễn đạt. Nếu response sai schema hoặc nhắc số ngoài evidence, backend loại và trả fallback deterministic.”

### Slide 10 — Chuỗi mua hàng

Nội dung trên slide:

- PA → chọn nguồn cung → PO → Receipt
- Quy đổi base unit, MOQ, quantity step
- Tái kiểm tra tại backend
- Permission/state/row-version từng bước

Lời thoại: “Chuỗi mua hàng là stateful workflow chứ không chỉ CRUD. Nhu cầu từ Purchase Advice được đối chiếu offer và quy đổi. Khi tạo PO, backend tính lại để không tin dữ liệu frontend. Khi confirm Receipt, inventory và audit được cập nhật trong transaction.”

### Slide 11 — Supplier Intelligence

Nội dung trên slide:

- Score v1 deterministic
- Chi phí, lượng dư, confidence, rankability
- Missing data không biến thành điểm tốt
- ShadowMode tại một Store

Lời thoại: “Tên có Intelligence nhưng phần quyết định là thuật toán backend, không phải LLM. Hệ thống so sánh giá, giao đúng hạn, fill rate, chất lượng và lead time. Nếu thiếu dữ liệu thì score có thể null và candidate chỉ để tham khảo. Hiện em chỉ bật pilot ShadowMode tại Store Thủ Dầu Một.”

### Slide 12 — Tín hiệu vận hành

Nội dung trên slide:

- Missing khác zero
- 28 ngày, tối thiểu 14 quan sát
- Upsert idempotent, state/audit rõ
- Giải thích tiếng Việt, không cáo buộc

Lời thoại: “Tín hiệu vận hành tìm thay đổi lớn so với lịch sử. Điểm quan trọng là ngày không có observation không được coi là zero. Kết quả chỉ là tín hiệu để kiểm tra. UI dùng ngôn ngữ nghiệp vụ và AI không được suy đoán gian lận, nguyên nhân hay người chịu trách nhiệm.”

### Slide 13 — Data flow và database

Nội dung trên slide:

- EF Core transaction cho write workflow
- Stored procedure/read model cho Dashboard
- Unique index + row-version + RequestKey
- Audit cho hành động quan trọng

Lời thoại: “Em dùng EF Core cho nghiệp vụ ghi có transaction. Dashboard dùng read model và stored procedure ở những phần tổng hợp. Ba lớp chống xung đột là RequestKey ở application, row-version khi cập nhật và unique index ở database. Audit lưu quyết định, không lưu OTP/PIN hay raw AI reasoning.”

### Slide 14 — Xử lý lỗi và fallback

Nội dung trên slide:

- 400 input, 403 permission/scope, 409 conflict
- SMTP/Ollama lỗi không phá nghiệp vụ chính
- UI loading và lỗi có hướng xử lý
- Correlation/log không chứa secret

Lời thoại: “Em cố gắng trả lỗi theo bản chất. Sai input là 400, vượt quyền là 403, replay hoặc state conflict là 409. Email và Ollama là kênh phụ nên lỗi provider không rollback challenge hoặc số liệu deterministic. UI giữ modal khi cancel thất bại và không che lỗi bằng thông báo chung.”

### Slide 15 — Phần em trực tiếp thực hiện

Nội dung trên slide:

- StaffHub/POS/OTP/Current Operator
- Dashboard RBAC + StaffScope
- AI EvidencePack + fallback
- PA/Supplier/PO/Receipt
- Anomaly + feature gate/audit

Lời thoại: “Ở phần này em phụ trách sâu các luồng StaffHub/POS, Dashboard/RBAC/AI và chuỗi mua hàng. Em không chỉ làm giao diện mà xử lý boundary transaction, permission/scope, state machine, idempotency và fallback. Các module CRUD khác em trình bày như thành phần của hệ thống, không nhận là trọng tâm kỹ thuật.”

### Slide 16 — Khó khăn và quyết định thiết kế

Nội dung trên slide:

- Race giữa quyết định/cancel/open
- Scope nhiều Store và nhiều role
- Dữ liệu thiếu trong scoring/anomaly
- AI phải dễ đọc nhưng không bịa

Lời thoại: “Bốn vấn đề khó nhất là concurrency, scope, missing data và AI grounded. Em lựa chọn revalidate trước commit, permission-first và deny-by-default, giữ missing là null, và để backend tạo evidence trước khi gọi model. Các lựa chọn này làm hệ thống thận trọng hơn nhưng dễ audit.”

### Slide 17 — Demo flow

Nội dung trên slide:

- Sales Staff mở ngoài lịch / Current Operator
- Manager xem notification và duyệt
- Owner xem Dashboard/AI/Anomaly
- Accountant so sánh NCC và tạo PO

Lời thoại: “Demo bắt đầu bằng salesstaff mở POS và một approver xử lý notification. Sau đó em đổi Current Operator để chứng minh trách nhiệm két không đổi. Tiếp theo owner xem Dashboard, AI và tín hiệu vận hành. Cuối cùng accountant mở Purchase Advice, so sánh supplier pilot rồi đi tiếp luồng PO/Receipt.”

Tài khoản demo local/test, mật khẩu `The@1712`: `salesstaff@cafechain.vn`, `salesstaff2@cafechain.vn`, `shiftsupervisor@cafechain.vn`, `storemanager@cafechain.vn`, `owner@cafechain.vn`, `accountantwarehouse@cafechain.vn`. Chỉ dùng account có thật sau khi chạy migration hiện tại và `SeedAll.sql`.

### Slide 18 — Kiểm thử, giới hạn và hướng phát triển

Nội dung trên slide:

- Unit/contract/SQL/browser theo risk
- Supplier còn ShadowMode một Store
- AI/anomaly cần dữ liệu pilot dài hạn
- Chưa có multi-turn/monitoring/forecast production

Lời thoại: “Em không khẳng định production-ready. Supplier vẫn ở ShadowMode một Store; anomaly và AI chưa có số liệu pilot dài hạn. Forecast và POS recommendation mới có nền backend, chưa có UI/exit gate production. Nếu phát triển thêm em sẽ bổ sung monitoring tập trung, đánh giá chất lượng dài hạn và scale worker/cache.”

### Slide 19 — Kết luận

Nội dung trên slide:

- Correctness và authorization trước AI
- Backend là nguồn sự thật
- Workflow có state/audit/idempotency
- Có lộ trình pilot trước rollout

Lời thoại: “Kết quả chính không phải là AI trả lời hay mà là hệ thống giữ đúng quyền, đúng Store và đúng trạng thái. AI chỉ đứng sau số liệu deterministic. Với các feature mới, em chọn pilot và audit trước khi mở rộng toàn chuỗi.”

## 6. Bộ câu hỏi hội đồng và trả lời mẫu

### 6.1 Tổng quan dự án

**Câu hỏi: CafeChain khác một website bán đồ uống thông thường ở điểm nào?**

Trả lời mẫu: “Website bán hàng chỉ là một phần. CafeChain còn quản lý ca và tiền két, terminal, kho, mua hàng, nhà cung cấp, nhân sự và Dashboard nhiều chi nhánh. Phần em tập trung là các workflow có nhiều trạng thái và phân quyền, ví dụ mở ca ngoại lệ, Purchase Advice đến Receipt và Dashboard theo StaffScope.”

**Câu hỏi: Đối tượng chính của hệ thống là ai?**

Trả lời mẫu: “Hệ thống có cả khách hàng và nhân sự nội bộ. Nội bộ gồm Owner, quản lý vùng, quản lý chi nhánh, kế toán/kho, ca trưởng, nhân viên bán hàng và quản trị kỹ thuật. Mỗi đối tượng dùng cùng dữ liệu nền nhưng permission và phạm vi Store khác nhau.”

### 6.2 Câu hỏi nghiệp vụ

**Câu hỏi: Vì sao WorkShift không được tạo ngay ở StaffHub?**

Trả lời mẫu: “StaffHub mới là bước kiểm tra và cấp context. Nếu tạo WorkShift ở đó thì người dùng đóng modal hoặc chưa nhập tiền đầu ca vẫn để lại ca giả. Em chuyển điểm commit sang API open của POS; backend revalidate rồi tạo WorkShift, bind session và consume approval trong transaction.”

**Câu hỏi: Đổi Current Operator có phải bàn giao két không?**

Trả lời mẫu: “Không. Nó chỉ giúp ghi đúng người thực hiện Order, Payment và audit khi nhiều nhân viên dùng chung quầy. WorkShift.UserId, tiền đầu ca và trách nhiệm tài chính vẫn thuộc người mở ca. Muốn bàn giao két thật phải đóng và đối soát ca cũ rồi mở ca mới.”

**Câu hỏi: Tại sao trễ trên 30 phút không dùng OTP?**

Trả lời mẫu: “OTP chỉ xác minh người duyệt đã cung cấp mã, còn ca trễ cần quyết định nghiệp vụ có trạng thái và lý do. Từ 30 đến 45 phút Manager có thể duyệt, từ chối hoặc chuyển ngoài lịch; trên 45 phút cả UI lẫn backend đều khóa duyệt theo lịch cũ. Request vẫn có row-version, RequestKey và audit.”

### 6.3 API/REST

**Câu hỏi: Tại sao không trình bày toàn bộ endpoint?**

Trả lời mẫu: “Danh sách CRUD không thể hiện logic. Em trình bày theo một request hoàn chỉnh: frontend gọi API, authentication, authorization, validation, service, transaction database và response. Chỉ nêu endpoint quan trọng như open shift hoặc Analyze để xác định boundary nghiệp vụ.”

**Câu hỏi: Hệ thống dùng status code thế nào?**

Trả lời mẫu: “400 cho input hoặc quyết định không hợp lệ, 401 khi chưa xác thực hoặc POS session hết hiệu lực, 403 khi thiếu permission/scope, 404 khi resource không tồn tại và 409 khi state/replay/row-version xung đột. UI dựa vào nhóm lỗi này để đưa hướng xử lý.”

### 6.4 Authentication

**Câu hỏi: Vì sao vừa dùng Cookie vừa dùng JWT?**

Trả lời mẫu: “Razor MVC phù hợp cookie vì trình duyệt và antiforgery đã tích hợp tốt. React POS gọi API nên dùng JWT. Tuy nhiên JWT POS không đứng một mình; mỗi lần validate còn kiểm PosAccessSessionId và JTI trong database để có thể revoke khi đóng ca hoặc hủy phiên.”

**Câu hỏi: JWT bị lấy cắp thì sao?**

Trả lời mẫu: “Rủi ro vẫn tồn tại nên token có hạn, HTTPS là bắt buộc và backend kiểm session/JTI active. Khi session bị end/revoke, token còn hạn cũng bị từ chối. Hướng phát triển là rotation chặt hơn, device binding và centralized token monitoring.”

### 6.5 Authorization/phân quyền

**Câu hỏi: Tại sao không kiểm tra role trực tiếp?**

Trả lời mẫu: “Một account có thể nhiều role và có override riêng. Nếu hard-code role thì rất khó xử lý quyền tùy chỉnh. Em dùng effective permission; role chỉ seed mặc định. Account Deny ưu tiên Allow và StaffScope vẫn được tính từ assignment thật.”

**Câu hỏi: Ẩn menu đã đủ bảo mật chưa?**

Trả lời mẫu: “Chưa. Ẩn menu chỉ cải thiện UX. Controller/service vẫn kiểm permission, action capability và Store scope trước repository. Nếu người dùng sửa URL hoặc gọi API trực tiếp, backend phải trả 403 và không query dữ liệu ngoài quyền.”

**Câu hỏi: SystemAdmin có xem toàn bộ doanh thu không?**

Trả lời mẫu: “Không mặc định. SystemAdmin là role kỹ thuật, SeedAll chỉ cấp nhóm System.*. Muốn xem dữ liệu kinh doanh vẫn phải được cấp explicit permission và StaffScope giống account khác. Em chọn vậy theo nguyên tắc least privilege.”

### 6.6 Database

**Câu hỏi: Làm sao tránh tạo hai WorkShift khi double-click?**

Trả lời mẫu: “Frontend disable nút chỉ là lớp đầu. Backend dùng RequestKey để replay cùng payload trả kết quả cũ, transaction để commit nguyên khối và unique active index cho staff/terminal để chặn race ở tầng database.”

**Câu hỏi: Khi nào dùng EF Core, khi nào dùng stored procedure?**

Trả lời mẫu: “Workflow ghi và state transition dùng EF Core transaction vì cần theo dõi entity và audit. Dashboard tổng hợp nhiều dữ liệu dùng read model/stored procedure để ổn định contract và hiệu năng. Dù cách truy vấn khác nhau, permission/scope vẫn được kiểm trước.”

**Câu hỏi: SeedAll có xóa custom permission không?**

Trả lời mẫu: “Không. `Scripts/SeedAll.sql` là nguồn default policy và phải idempotent. Nó đồng bộ role grant được quản lý nhưng không xóa account override hoặc assignment scope tùy chỉnh. Các khóa feature pilot cũng chỉ insert khi thiếu, không ghi đè quyết định quản trị.”

### 6.7 Frontend ↔ Backend

**Câu hỏi: Frontend có tự tính quyền hoặc score Supplier không?**

Trả lời mẫu: “Không. Frontend nhận AllowedSections/Widgets/Capabilities và Supplier DTO đã tính. Nó chỉ render. Khi tạo PO, backend còn tính lại conversion, MOQ, giá và số lượng còn cần mua để không tin snapshot trên trình duyệt.”

**Câu hỏi: SignalR có phải nguồn sự thật không?**

Trả lời mẫu: “Không. SignalR chỉ báo thay đổi nhanh để client reload hoặc cập nhật. Database/API vẫn authoritative. Nếu mất SignalR thì polling hoặc refresh vẫn lấy đúng trạng thái; điều này đặc biệt quan trọng với OTP, approval và Current Operator.”

### 6.8 Security

**Câu hỏi: OTP được bảo vệ thế nào?**

Trả lời mẫu: “OTP bind action, requester, approver, Store, Terminal và request context; có TTL, cooldown, giới hạn sai và one-time use. Payload realtime/list/log không chứa mã. Manager chỉ reveal qua endpoint kiểm recipient, permission và scope; DB không lưu mã plaintext.”

**Câu hỏi: AI có thể làm lộ dữ liệu Store khác không?**

Trả lời mẫu: “Pipeline classify domain rồi authorization và scope trước DataPlan/repository. Chỉ evidence đã lọc mới đi tới Ollama. Context cache cũng được authorize lại. Nếu domain hoặc Store không hợp lệ thì trả 403 trước khi query hay gửi prompt.”

### 6.9 Performance

**Câu hỏi: Nếu Dashboard nhiều Store thì có chậm không?**

Trả lời mẫu: “Hiện tại Dashboard giới hạn Store theo scope và kỳ dữ liệu, dùng read query/stored procedure cho tổng hợp và chỉ gọi widget được phép. Khi scale lớn hơn em sẽ đo query plan, thêm cache theo permission/filter fingerprint, pre-aggregation và tách analytics workload khỏi OLTP.”

**Câu hỏi: Supplier scoring có N+1 không?**

Trả lời mẫu: “Đây là điểm cần theo dõi. Luồng hiện tại đã lọc candidate và giới hạn Store/ingredient, nhưng phần hiệu suất supplier vẫn có thể cần batch-load tốt hơn khi số offer tăng. Nếu mở rộng em sẽ gom receipt/performance theo một query và benchmark trước rollout.”

### 6.10 Error handling

**Câu hỏi: SMTP hoặc Ollama lỗi thì sao?**

Trả lời mẫu: “SMTP chỉ là kênh gửi phụ; challenge và notification nội bộ đã commit vẫn hợp lệ. Ollama chỉ giải thích; score, metric và evidence deterministic vẫn trả bằng fallback. Vì vậy provider lỗi không làm hỏng nghiệp vụ chính.”

**Câu hỏi: Vì sao dùng 409 cho cancel sau khi đã mở ca?**

Trả lời mẫu: “Vì request hợp lệ về cú pháp nhưng xung đột với trạng thái hiện tại. Khi WorkShift đã tồn tại, cancel intent không được phép xóa ca. Backend trả 409 để UI hướng người dùng sang quy trình đóng/đối soát thay vì báo input sai.”

### 6.11 Testing

**Câu hỏi: Em ưu tiên test gì?**

Trả lời mẫu: “Em ưu tiên theo rủi ro: authorization và Store tampering, state machine/idempotency, deterministic calculation, SQL/seed chạy lại, rồi UI browser và regression. AI được test sau evidence backend vì provider không được che lỗi số liệu.”

**Câu hỏi: Test nào chứng minh repository không bị gọi khi không có quyền?**

Trả lời mẫu: “Ở unit/contract test em mock repository và xác nhận zero invocation khi permission hoặc scope fail. Ở integration test em gọi direct endpoint với account ngoài scope và kiểm 403. Đây quan trọng hơn việc chỉ kiểm tab đã ẩn.”

### 6.12 Deployment

**Câu hỏi: Secret được lưu ở đâu?**

Trả lời mẫu: “Development dùng .NET User Secrets hoặc environment variable. Production phải dùng secret store/environment của hạ tầng. Appsettings và tài liệu không chứa Gmail password, JWT key hay provider key; log cũng không ghi credential/OTP/PIN.”

**Câu hỏi: Feature mới được rollout thế nào?**

Trả lời mẫu: “Em dùng Enabled, ShadowMode, StoreAllowlist và FullRollout. Supplier hiện chỉ ShadowMode ở Store Thủ Dầu Một. Sau test mới xem telemetry/data quality và exit gate; không dùng một boolean để bật toàn chuỗi ngay.”

### 6.13 Quyết định thiết kế

**Câu hỏi: Tại sao AI không tự tạo SQL?**

Trả lời mẫu: “Dynamic SQL làm khó kiểm soát permission, scope, hiệu năng và fabricated query. Em dùng DataPlan từ catalog widget cố định. Cách này ít linh hoạt hơn nhưng predictable, testable và evidence có thể audit.”

**Câu hỏi: Tại sao missing supplier metric giữ null?**

Trả lời mẫu: “Không có receipt không có nghĩa chất lượng 100 hoặc 0. Nếu ép thành điểm tốt sẽ tạo ranking sai. Em giữ null, hạ confidence/rankability và hiển thị warning để người dùng biết giới hạn dữ liệu.”

### 6.14 Câu hỏi “Tại sao em làm như vậy?”

**Câu hỏi: Tại sao backend phải tính lại khi tạo PO dù frontend đã có comparison?**

Trả lời mẫu: “Dữ liệu offer, giá, phần còn cần mua và row-version có thể đổi sau lúc mở modal. Frontend cũng không đáng tin cậy về security. Vì vậy snapshot chỉ để hiển thị; service tạo PO tái kiểm tra dữ liệu authoritative trước transaction.”

**Câu hỏi: Tại sao anomaly không tự resolve khi số liệu trở lại bình thường?**

Trả lời mẫu: “Nếu worker tự resolve sẽ làm mất dấu quy trình xác minh và ghi chú của người quản lý. V1 giữ state OPEN → ACKNOWLEDGED → RESOLVED do người có quyền thao tác. Rerun chỉ cập nhật evidence, không reset state.”

### 6.15 Nếu hệ thống có nhiều người dùng hơn

**Câu hỏi: Nếu mở lên hàng trăm Store thì sao?**

Trả lời mẫu: “Em sẽ tách workload analytics/worker khỏi request web, dùng queue và distributed lock cho scheduled job, cache/read replica cho Dashboard, batch query supplier performance và partition/index theo Store/BusinessDate. Trước hết phải benchmark từ telemetry pilot thay vì tối ưu theo giả định.”

**Câu hỏi: SignalR scale nhiều server thế nào?**

Trả lời mẫu: “Một instance hiện tại dùng hub trực tiếp. Khi scale-out cần shared backplane như Redis hoặc managed SignalR và sticky/session strategy phù hợp. Event vẫn chỉ là notification; client reload API authoritative nên không phụ thuộc tuyệt đối vào việc nhận đủ event.”

### 6.16 Câu hỏi kiểm tra sinh viên có thực sự làm dự án

**Câu hỏi: Em hãy chỉ đúng nơi WorkShift được tạo.**

Trả lời mẫu: “Điểm commit là `POST /api/v1/pos/shifts/open`, qua `POSShiftController` và `WorkShiftService`. StaffHub preview/IssuePosToken chỉ cấp context, không tạo record. Em có thể chỉ transaction revalidate terminal, permission, approval và StartingCash trước khi save/bind session.”

**Câu hỏi: Em hãy phân biệt permission và StaffScope bằng một lỗi thực tế.**

Trả lời mẫu: “Một Store Manager có thể có `Inventory.View` nhưng assignment chỉ Store 1. Permission trả lời ‘được xem tồn kho’, còn StaffScope trả lời ‘được xem ở đâu’. Nếu sửa URL sang Store 3 thì vẫn 403 dù permission đúng.”

**Câu hỏi: Em tìm Current Operator ở đâu và nó khác UserId thế nào?**

Trả lời mẫu: “WorkShift giữ `UserId` là Responsible Staff và `CurrentOperatorStaffId` là người thao tác hiện tại. Switch bằng PIN chỉ đổi trường operator và thời điểm, sau đó SignalR cập nhật UI. Các giao dịch mới dùng operator để attribution nhưng close/reconciliation vẫn quy trách nhiệm theo WorkShift.UserId.”

**Câu hỏi: Supplier score có phải AI tính không?**

Trả lời mẫu: “Không. `SupplierIntelligenceService` tính deterministic theo weight v1 và dữ liệu receipt/conversion. AI nếu dùng chỉ có thể giải thích structured comparison, không đổi score, weight hay chọn supplier.”

## 7. Mười câu hội đồng dễ hỏi nhất

### 1. Dự án giải quyết vấn đề gì?

Ý bắt buộc: chuỗi nhiều Store; POS/ca/kho/mua hàng/Dashboard; không chỉ CRUD.

Trả lời tự nhiên: “CafeChain giúp quản lý vận hành chuỗi cửa hàng đồ uống từ bán hàng tại quầy đến ca/két, tồn kho và mua hàng. Ở phần em phụ trách, em tập trung vào những điểm dễ sai khi nhiều Store và nhiều role cùng dùng dữ liệu, đặc biệt là POS, Dashboard/RBAC/AI và chuỗi mua hàng.”

### 2. Phần nào em trực tiếp làm?

Ý bắt buộc: StaffHub/POS; Dashboard/RBAC/AI; PA/Supplier/PO/Receipt; không nhận quá mức.

Trả lời tự nhiên: “Em phụ trách sâu StaffHub/POS và OTP/Current Operator, Dashboard theo permission và StaffScope, AI dùng EvidencePack, cùng chuỗi Purchase Advice–Supplier–PO–Receipt. Các CRUD master data khác em có tham gia tích hợp nhưng không xem là điểm kỹ thuật chính.”

### 3. Vì sao không dùng role để phân quyền?

Ý bắt buộc: nhiều role, override, scope; permission runtime.

Trả lời tự nhiên: “Em lựa chọn permission vì một tài khoản có thể nhiều role và có override. Role chỉ giúp seed mặc định. Backend tính effective permission, Deny của account được ưu tiên, sau đó StaffScope mới giới hạn Store nên linh hoạt hơn hard-code theo tên role.”

### 4. Làm sao ngăn người dùng xem Store khác?

Ý bắt buộc: backend trước repository; 403; UI không phải security.

Trả lời tự nhiên: “Frontend chỉ hiển thị Store được cấp nhưng backend vẫn resolve assignment thật. Mỗi API kiểm StoreId có thuộc scope trước khi gọi repository; sửa URL hoặc gửi request thủ công sẽ nhận 403. Em không dùng cách query xong rồi lọc response.”

### 5. Vì sao hủy modal trước đây vẫn tạo ca và em sửa thế nào?

Ý bắt buộc: pre-open WorkShift; commit boundary ở open API.

Trả lời tự nhiên: “Nguyên nhân là WorkShift từng được tạo sớm với tiền đầu ca bằng 0. Em tách preview và issue context thành read/authorization, còn WorkShift chỉ được tạo trong API open sau khi người dùng xác nhận tiền. Hủy trước bước đó chỉ cancel intent/session nên không còn ca kinh doanh rác.”

### 6. AI có tự truy vấn database không?

Ý bắt buộc: không SQL; DataPlan; EvidencePack; fallback.

Trả lời tự nhiên: “Không. Backend của em classify câu hỏi, kiểm permission/scope rồi chọn widget từ catalog và dựng EvidencePack. Ollama chỉ diễn đạt số liệu đó. Nếu sai JSON, nhắc số ngoài evidence hoặc timeout thì hệ thống bỏ response và dùng fallback deterministic.”

### 7. Supplier Intelligence có tự chọn nhà cung cấp không?

Ý bắt buộc: deterministic; confidence/rankability; user decides; shadow pilot.

Trả lời tự nhiên: “Không. Backend tính chi phí, MOQ, lượng dư và score deterministic. Confidence tách khỏi score; thiếu dữ liệu thì candidate có thể chỉ tham khảo. Người dùng vẫn chọn và backend tính lại khi tạo PO. Hiện feature còn ShadowMode ở một Store.”

### 8. Current Operator có tác dụng gì?

Ý bắt buộc: attribution; không chuyển trách nhiệm két.

Trả lời tự nhiên: “Nó cho phép đổi người thực tế thao tác trên cùng quầy mà không đóng ca. Order và Payment sau đó ghi đúng người làm. Nhưng người mở ca vẫn chịu trách nhiệm két, WorkShift.UserId và tiền đầu ca không đổi; đây không phải bàn giao két.”

### 9. Em xử lý double-click và race thế nào?

Ý bắt buộc: RequestKey, transaction, row-version/unique index.

Trả lời tự nhiên: “Disable button chỉ hỗ trợ UX. Ở backend em dùng RequestKey để request lặp trả kết quả cũ, transaction để các thay đổi cùng commit, row-version cho quyết định concurrent và unique index để database chặn hai ca active cùng staff hoặc terminal.”

### 10. Hạn chế hiện tại là gì?

Ý bắt buộc: nói thẳng pilot/OFF/no long-term metrics.

Trả lời tự nhiên: “Hiện tại em chưa xem hệ thống là production-ready. Supplier mới ShadowMode một Store; anomaly và AI chưa có số liệu pilot dài hạn. Forecast/POS recommendation chưa có UI và exit gate hoàn chỉnh. Nếu phát triển thêm em sẽ làm monitoring tập trung, đánh giá chất lượng và scale worker/read model dựa trên telemetry thực tế.”

## 8. Checklist trước buổi bảo vệ

- Chạy migration hiện tại rồi `Scripts/SeedAll.sql`; không sửa credential demo trên slide.
- Xác nhận account, permission và StaffScope trước từng demo.
- Chuẩn bị một terminal trống, một ca ngoài lịch, một late approval và một PA `UNDER_REVIEW`.
- Chuẩn bị phương án fallback khi SMTP/Ollama không chạy; trình bày đây là thiết kế chứ không che lỗi.
- Không gọi Supplier/Anomaly production-ready; nói rõ ShadowMode/pilot và giới hạn dữ liệu.
- Khi bị hỏi endpoint, trả lời theo luồng nghiệp vụ rồi mới chỉ tên controller/action quan trọng.
