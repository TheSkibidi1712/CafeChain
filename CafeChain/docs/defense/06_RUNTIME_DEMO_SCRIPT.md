# Kịch bản demo bảo vệ

## Nguyên tắc chuẩn bị

- Chỉ dùng local/demo, không dùng dữ liệu production.
- Chuẩn bị account theo **role**, không ghi password vào tài liệu.
- Mỗi tab/browser profile chỉ giữ một role để tránh nhầm session.
- Chuẩn bị sẵn một store, terminal, WorkShift, menu/BOM, tồn, supplier và request key mới.
- Chạy rehearsal toàn bộ ít nhất một lần; ghi lại mã Order/Restock/PA/PO vừa tạo.
- Khi external service lỗi, dùng fallback đã nêu; không sửa dữ liệu trực tiếp trong lúc bảo vệ.

## Timeline 20 phút đề xuất

| Thời lượng | Nội dung | Thông điệp |
|---:|---|---|
| 0–2 phút | Bài toán và kiến trúc | Một chuỗi dữ liệu xuyên POS, kho và mua hàng |
| 2–8 phút | Kịch bản A: bán hàng/ca | Giá do server xác nhận, snapshot, idempotency, đối soát |
| 8–16 phút | Kịch bản B: Restock → Receipt | Phân vai và maker-checker, chỉ receipt confirmed tăng tồn |
| 16–19 phút | Dashboard/costing/audit | Dữ liệu giao dịch thành chỉ số quản trị có scope |
| 19–20 phút | Kết luận | Traceability, permission, concurrency và hạn chế |

## Dữ liệu cần chuẩn bị

| Dữ liệu | Yêu cầu |
|---|---|
| Store demo | Active, có manager/supervisor/sales staff và terminal |
| Menu | Một đồ uống có size, BOM exact và topping policy active |
| Inventory | Đủ lớp FIFO cho demo costing; thêm một component thiếu để minh họa completeness nếu cần |
| WorkShift | Không có active shift xung đột trên terminal trước khi demo |
| Supplier | Active, assigned store, có offer/UOM/package hợp lệ |
| Procurement | Không dùng lại request key/PA allocation đã ordered |
| Browser | Bốn profile: Store Manager, Sales Staff, Accountant, Owner |

### Bí danh tài khoản demo

Các bí danh dưới đây trỏ tới account local do nhóm giữ trong bảng setup riêng; không ghi email/password vào repository.

| Bí danh | Role |
|---|---|
| `DEMO_STORE_MANAGER` | Store Manager của store demo |
| `DEMO_SHIFT_SUPERVISOR` | Shift Supervisor của store demo |
| `DEMO_SALES_STAFF` | Sales Staff của store demo |
| `DEMO_ACCOUNTANT` | AccountantWarehouse |
| `DEMO_OWNER` | BusinessOwner |

## Kịch bản A — Bán hàng và ca POS (6 phút)

### A1. Quản lý/Ca trưởng chuẩn bị ca

| Mục | Nội dung |
|---|---|
| Role | Store Manager hoặc Shift Supervisor có permission mở ca |
| Tài khoản demo | `DEMO_STORE_MANAGER` hoặc `DEMO_SHIFT_SUPERVISOR` |
| Trang | StaffHub → Mở POS/WorkShift |
| Dữ liệu cần chuẩn bị | Store, terminal và lịch active; không có ca xung đột |
| Thao tác | Chọn terminal, nhập tiền đầu ca, xác nhận lịch |
| Kết quả | WorkShift `OPEN`, gắn store/staff/terminal/business date |
| Câu nói | “Ca POS là boundary trách nhiệm của người và két, không chỉ là một trạng thái giao diện.” |
| Fallback | Mở một WorkShift seed đang hợp lệ và trình bày chi tiết thay vì tạo mới |

### A2. Nhân viên bán hàng tạo đơn

| Mục | Nội dung |
|---|---|
| Role | Sales Staff |
| Tài khoản demo | `DEMO_SALES_STAFF` |
| Trang | React POS `/order` |
| Dữ liệu cần chuẩn bị | WorkShift open, catalog/menu/BOM/topping policy active |
| Thao tác | Chọn món, size, topping mặc định/tùy chọn, số lượng |
| Kết quả | Giá thay đổi đúng policy; cart giữ recipe/catalog snapshot |
| Câu nói | “Client gửi lựa chọn, nhưng server xác nhận catalog, BOM và giá.” |
| Fallback | Mở một Order demo và chỉ ra snapshot trong detail/history |

### A3. Thanh toán

| Mục | Nội dung |
|---|---|
| Role | Sales Staff |
| Tài khoản demo | `DEMO_SALES_STAFF` |
| Trang | POS checkout |
| Dữ liệu cần chuẩn bị | Cart hợp lệ và request key/ClientOrderId mới |
| Thao tác | Chọn tiền mặt; nhập tiền khách đưa; commit |
| Kết quả | Order completed/paid, Payment, tiền thừa và expected cash của WorkShift |
| Câu nói | “Order, payment và cập nhật két được ghi trong transaction; `ClientOrderId` chống tạo trùng khi retry.” |
| Fallback | Nếu PayOS không sẵn sàng, luôn dùng tiền mặt; nếu print bridge không chạy, mở reprint preview/history |

### A4. Chỉ ra snapshot và tồn

1. Mở order history/detail.
2. Chỉ tên món/size, giá bán, topping, payment và WorkShift.
3. Mở inventory/ledger nếu quyền demo cho phép.
4. Giải thích side effect trừ kho retry độc lập sau commit.

**Điểm phản biện:** nếu hội đồng hỏi mạng lỗi, giải thích IndexedDB + `ClientOrderId`; nếu hỏi tồn âm, giải thích ADR blind selling và đối soát.

### A5. Đóng/đối soát ca

| Mục | Nội dung |
|---|---|
| Role | Sales Staff gửi đóng; Shift Supervisor/Manager xử lý ngoại lệ |
| Tài khoản demo | `DEMO_SALES_STAFF`, `DEMO_SHIFT_SUPERVISOR` |
| Trang | POS/StaffHub → Đóng ca |
| Dữ liệu cần chuẩn bị | Payment hoàn tất; offline queue đã sync hoặc manifest ngoại lệ |
| Thao tác | Kiểm tiền, nhập actual cash và lý do nếu chênh |
| Kết quả | `CLOSED` hoặc `RECONCILIATION_REQUIRED` |
| Câu nói | “Payment đang mở hoặc offline queue chưa sync sẽ chặn đóng thường; ngoại lệ phải có audit/OTP theo rule.” |
| Fallback | Dùng WorkShift demo `RECONCILIATION_REQUIRED` và trình bày read-only |

## Kịch bản B — Restock đến nhận hàng (8 phút)

### B1. Store Manager tạo nhu cầu

| Mục | Nội dung |
|---|---|
| Role | Store Manager |
| Tài khoản demo | `DEMO_STORE_MANAGER` |
| Trang | Yêu cầu nhập hàng → tạo thủ công |
| Dữ liệu cần chuẩn bị | Ingredient active, demand UOM conversion hợp lệ |
| Thao tác | Chọn ingredient, nhập quantity/UOM nhu cầu, need-by, priority; gửi |
| Kết quả | Restock có mã `RR-...`, status submitted, không tăng tồn |
| Câu nói | “Restock chỉ là nhu cầu cửa hàng; backend chuẩn hóa quantity về base UOM.” |
| Fallback | Mở một Restock seed và chỉ status/history |

### B2. Kế toán tiếp nhận và chọn nguồn

| Mục | Nội dung |
|---|---|
| Role | AccountantWarehouse |
| Tài khoản demo | `DEMO_ACCOUNTANT` |
| Trang | Restock detail → nguồn cung → PA |
| Dữ liệu cần chuẩn bị | Restock submitted, quantity chưa được PA bao phủ |
| Thao tác | Tiếp nhận Restock, chọn `PURCHASE`, tạo PA cho phần chưa được đề nghị |
| Kết quả | Purchase allocation và PA có trace về Restock |
| Câu nói | “Coverage tính theo số lượng, không phải chỉ kiểm tra đã có PA hay chưa.” |
| Fallback | Mở PA demo và chỉ Restock reference/quantities |

### B3. Hoàn thiện PA và tạo PO

**Tài khoản demo:** `DEMO_ACCOUNTANT`. **Trang:** PA detail/PO create. **Dữ liệu:** supplier-store assignment và offer/UOM active.

1. Chuyển PA sang review theo state hợp lệ.
2. Chọn supplier được gán cho store, offer/package hoặc loose mode.
3. Tạo PO thường từ một nguồn; chỉ chọn POB nếu có từ hai nguồn tương thích.
4. Chỉ snapshot package, UOM, price, MOQ và conversion.

**Câu nói:** “PO thường cho một nguồn; POB là aggregate cho nhiều nguồn tương thích, không phải wrapper bắt buộc.”

### B4. Owner duyệt

| Mục | Nội dung |
|---|---|
| Role | BusinessOwner |
| Tài khoản demo | `DEMO_OWNER` |
| Trang | PO/POB detail chờ duyệt |
| Dữ liệu cần chuẩn bị | Chứng từ do `DEMO_ACCOUNTANT` tạo, RowVersion mới nhất |
| Thao tác | Mở PO chờ duyệt, xem trace và duyệt |
| Kết quả | `APPROVED`; `ApprovedByStaffId` khác creator |
| Câu nói | “Đây là maker-checker: người tạo PO không thể tự duyệt.” |
| Fallback | Dùng PO approved demo và đối chiếu creator/approver read-only |

### B5. Gửi NCC và nhận hàng

| Mục | Nội dung |
|---|---|
| Role | Accountant tạo PDF/gửi; Store Manager hoặc Shift Supervisor nhận |
| Tài khoản demo | `DEMO_ACCOUNTANT`, sau đó `DEMO_STORE_MANAGER`/`DEMO_SHIFT_SUPERVISOR` |
| Trang | PO detail → Receipt create/confirm |
| Dữ liệu cần chuẩn bị | PO approved/sent và còn quantity nhận |
| Thao tác | Mark sent; tạo receipt; nhập accepted/rejected; confirm |
| Kết quả | Chỉ accepted quantity tăng tồn; ledger/cost layer/fulfillment trace được ghi một lần |
| Câu nói | “PO không tăng tồn. Chỉ phiếu nhận `CONFIRMED` mới là authority ghi kho.” |
| Fallback | Dùng receipt confirmed demo, chỉ transaction ID và before/after quantity |

## Kịch bản C — Menu, giá vốn và giá bán (3 phút, tùy chọn)

1. Role Owner/Accountant mở “Vốn và lợi nhuận dự kiến”.
2. Chọn store và drink.
3. Chỉ BOM cơ sở, BTP, topping, UOM completeness và FIFO layers.
4. Đối chiếu `GrossProfit = Price - Cost`, Margin và Markup.
5. Tính giá gợi ý với một target; nhấn mạnh preview không persist.
6. Chỉ action lưu giá tách riêng, cần Owner/reason/RowVersion/audit.

**Fallback:** nếu preview lỗi vì dữ liệu FIFO thiếu, dùng chính trạng thái incomplete để trình bày fail-closed; không coi phần thiếu là 0.

## Kịch bản D — Operational Ice (3 phút, tùy chọn)

1. Store Manager mở danh sách OperationalShift.
2. Chỉ shift source, time, lead và status.
3. Mở detail: WorkShift links, allocation, supplement, carry-over, theoretical/actual và variance.
4. Giải thích ca POS và ca đá là hai aggregate khác nhau.
5. Nếu có dữ liệu phù hợp, chỉ report; không mutate nếu rehearsal chưa khóa dữ liệu.

**Fallback:** local/demo có hai OperationalShift open; dùng read-only detail/report. Link/close chưa runtime verify trong task docs này.

## Checklist ngay trước buổi bảo vệ

- [ ] Build và test targeted xanh.
- [ ] Database demo đã backup/snapshot.
- [ ] Không còn WorkShift active xung đột trên terminal demo.
- [ ] Mỗi role đăng nhập được và đúng store/scope.
- [ ] Supplier/offer/UOM demo active.
- [ ] External service có fallback.
- [ ] Không mở tab secret, user-secrets, appsettings hoặc log chứa dữ liệu nhạy cảm.
- [ ] Có sẵn mã Order/Restock/PA/PO/Receipt fallback.
- [ ] Mermaid/tài liệu có thể mở offline.

## Runtime status của lần lập tài liệu

`RUNTIME_CONFIRMED`: backend, login boundary, 401/302 authorization boundary, database role counts và dữ liệu workflow.
`NOT_RUNTIME_VERIFIED`: click-through theo từng account, React POS dev server, payment external, print bridge, SMTP và chuỗi mutate A/B. Kịch bản trên là script rehearsal bắt buộc trước ngày bảo vệ, không phải tuyên bố đã chạy toàn bộ trong task docs.
