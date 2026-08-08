# Quy tắc nghiệp vụ Dashboard, AI, Operational Anomaly và Supplier Intelligence

Tài liệu này mô tả hợp đồng nghiệp vụ sau refactor. Nguồn authoritative là backend; UI chỉ hiển thị dữ liệu/quyền backend trả về, còn AI chỉ giải thích evidence đã được cấp quyền.

## 1. Mô hình phân quyền

Quyền runtime không suy ra từ tên role:

```text
App.AdminDashboard
+ module/section permission
+ widget/action permission
+ StaffScope thực tế
```

- `App.AdminDashboard` chỉ cho phép vào Dashboard, không cho phép xem toàn bộ widget.
- Account-level Deny có ưu tiên cao hơn Allow.
- Không có StaffScope thì từ chối dữ liệu cần scope; Store ngoài scope trả 403 trước khi query nghiệp vụ.
- Nhiều role hợp permission nhưng không tự sinh thêm Store/Province/District.
- `SystemAdmin` là role kỹ thuật và mặc định chỉ có `System.*`; không tự có doanh thu, lợi nhuận, nhân sự hay dữ liệu thương mại.
- `Scripts/SeedAll.sql` là nguồn seed mặc định duy nhất. EF configuration chỉ mô tả schema; migration mới không xóa custom override/scope.

## 2. Section và widget Dashboard

| Section | Permission section | Widget nhạy cảm bổ sung |
|---|---|---|
| Executive | `Dashboard.Executive.View` | tài chính: `Dashboard.FinancialSummary.View`; heatmap/order: `Order.View` |
| Operations | `Dashboard.Operations.View` | WorkShift: `POS.WorkShift.View`; danh tính nhân viên: thêm `Staff.View` |
| Inventory | `Dashboard.Inventory.View` | tồn: `Inventory.View`; reorder/threshold dùng permission riêng |
| Procurement | `Dashboard.Procurement.View` | PO: `PurchaseOrder.View`; chất lượng NCC: `SupplierQuality.View`; giá nhập: `Receipt.ViewCost` |
| Product | `Dashboard.Product.View` | sản phẩm: `Drink.View`; margin/COGS: `Profitability.View` |
| Workforce | `Dashboard.Workforce.View` | lịch: `Shift.View`; performance: thêm `Staff.View` và `POS.WorkShift.View` |

`IDashboardAuthorizationService` trả `AllowedSections`, `AllowedWidgets`, `AllowedCapabilities`, `Scope` và `CanUseAi`. API kiểm tra entry → section/widget/action → StaffScope → filter rồi mới query. Frontend không gửi request cho phần bị ẩn; không có section hợp lệ trả 403.

## 3. StaffScope và bộ lọc

- Scope lấy từ assignment đang active, gồm type/label, Store được phép và khả năng chọn Province/District/Store hoặc aggregate.
- Aggregate chỉ chứa Store trong scope.
- Bộ lọc dùng Province/District/Store thật; không tạo `AreaId` giả.
- BusinessOwner, AreaManager, StoreManager và AccountantWarehouse chỉ khác default permission/scope được seed; runtime vẫn dùng effective permission.

## 4. Business date và NetSales

- `IBusinessDateService` dùng `Asia/Ho_Chi_Minh` (fallback Windows `SE Asia Standard Time`).
- Worker chỉ phân tích ngày kinh doanh đã hoàn chỉnh.
- Order legacy dùng local business interval; dữ liệu lưu UTC dùng khoảng UTC chuyển từ ngày Việt Nam.
- NetSales Dashboard và anomaly Revenue dùng `dbo.ufn_AnalyticsOrderFacts`; không tạo công thức doanh thu thứ hai.

## 5. Ranh giới AI Dashboard

AI cần đồng thời `App.AdminDashboard`, `Dashboard.AI.Use`, permission domain/widget và StaffScope. Domain được classify trước DataPlan/repository. Context cache được authorize lại mỗi lần dùng.

AI không tạo SQL, không truy cập DB trực tiếp, không sửa dữ liệu, không resolve anomaly, không tạo/duyệt PO và không chọn NCC. Ollama timeout/unavailable/response lỗi phải trả phần deterministic gồm metric, score, confidence, warning và evidence.

## 6. Operational Anomaly v1

- Feature gate theo Store: `Enabled`, `ShadowMode`, `StoreAllowlist` hoặc `FullRollout`. Allowlist rỗng không có nghĩa toàn chuỗi.
- Baseline: 28 ngày, tối thiểu 14 observation thật; missing không biến thành zero.
- Median/MAD, lệch tương đối tối thiểu 25%, robust score tối thiểu 3.5.
- Revenue cần lệch tuyệt đối tối thiểu 500.000 VND; cash discrepancy tối thiểu 100.000 VND.
- `CRITICAL` khi `abs(robust) >= 5` hoặc `abs(deviation) >= 50%`; còn đạt ngưỡng là `HIGH`.
- Business key duy nhất: Store + BusinessDate + Metric + DetectionVersion `v1`. Rerun cập nhật evidence nhưng không reset state và không gửi notification trùng.
- State machine: `OPEN -> ACKNOWLEDGED -> RESOLVED`; không tự reopen/auto-resolve.
- Feedback độc lập: `Useful`, `NotUseful`, `FalsePositive`.
- View/Acknowledge/Resolve/Feedback dùng permission riêng và kiểm tra Store trước khi tải full record.
- Audit lưu actor, thời gian, state cũ/mới, note/feedback. AI chỉ diễn đạt “tín hiệu cần kiểm tra”, không kết luận gian lận hoặc trách nhiệm cá nhân.
- UI đặt tại **Nhân sự & Vận hành → Tín hiệu vận hành** và dùng tên nghiệp vụ tiếng Việt: **Giá trị ghi nhận**, **Mức thông thường trước đây**, **Mức chênh lệch**, **Mức cần ưu tiên**, **Trạng thái xử lý**. Mã metric, detection version và điểm chuẩn hóa chỉ nằm trong **Thông tin kỹ thuật**.
- Giải thích AI và fallback đều trình bày: điều được phát hiện, so sánh với mức thường thấy, dữ liệu nên kiểm tra và cảnh báo đây chưa phải kết luận. Nội dung chính không lặp mã metric hoặc thuật ngữ thống kê.

## 7. Supplier Intelligence v1

Compare cần `PurchaseAdvice.View`, `SupplierQuality.View`, StaffScope và feature gate. Candidate chỉ được tính khi Supplier, IngredientSupplier, SupplierStore active; giá/số lượng/conversion hợp lệ.

- Hỗ trợ mua đóng gói và mua rời.
- `RequiredPackages = max(ceil(required / packageBaseQuantity), MOQ)`; mua rời áp dụng minimum và quantity step.
- Backend trả purchased quantity, excess, excess ratio và total cost; UI không tự tính lại.
- Window hiệu suất 180 ngày; weight `v1`: price 30%, on-time 20%, fill 20%, quality 20%, lead time 10%.
- Missing metric giữ `null`, không đổi thành 0/100.
- Lead time fallback 30 ngày phải ghi `FALLBACK` và warning; candidate đó không rankable.
- Confidence theo receipt xác nhận: `HIGH >= 20`, `MEDIUM 5–19`, dưới 5 là `INSUFFICIENT_DATA`.
- Chỉ HIGH/MEDIUM đủ toàn bộ metric mới rankable. Dưới hai supplier rankable không gọi là “best/recommended”.
- UI tích hợp tại Purchase Advice `UNDER_REVIEW` → **So sánh nhà cung cấp** → chọn quy cách → kiểm tra bản tổng hợp → tạo PO/batch → approval.
- Backend tái kiểm tra giá, conversion, MOQ, phần còn cần mua và scope khi tạo PO. Intelligence OFF/shadow/lỗi không làm hỏng mua thủ công.
- Khi tạo PO từ comparison, audit snapshot có Store, ingredient, nhu cầu, candidates, score/confidence/warning/version, supplier được chọn, actor, timestamp và PO.

## 8. Feature gate, telemetry và rollout

| Chế độ | Hành vi |
|---|---|
| OFF | Không tính/không chạy worker; nghiệp vụ thủ công vẫn hoạt động |
| Shadow | Có tính và ghi telemetry, không gửi notification/tác động production ngoài ý muốn |
| Allowlist | Chỉ Store được liệt kê |
| Full rollout | Chỉ bật rõ ràng sau exit gate |

`SeedAll.sql` chỉ tạo các khóa còn thiếu để chạy Supplier Intelligence ở **CafeChain Thủ Dầu Một** trong `ShadowMode=true`, `FullRollout=false`; không ghi đè cấu hình quản trị đã tồn tại. `IntelligencePilotRuns` chỉ lưu telemetry không PII/prompt/secret. Rollout production vẫn OFF. Chỉ được công bố production-ready sau security/deterministic/regression test, AI fallback, audit và dữ liệu pilot đều đạt.

## 9. Liên kết

- [Hướng dẫn người dùng Dashboard/AI/Anomaly/Supplier](./DASHBOARD_AI_ANOMALY_SUPPLIER_USER_GUIDE.md)
- [Hướng dẫn StaffHub/POS](./STAFFHUB_USER_BUSINESS_FLOWS.md)
- [Quy tắc StaffHub/POS](./STAFFHUB_POS_WORKSHIFT_BUSINESS_RULES.md)
- [Hướng dẫn đăng ký Terminal POS](./POS_TERMINAL_USER_GUIDE.md)

Quyền OTP/Terminal (`POS.WorkShift.*`, `Notification.View`) độc lập với authorization Dashboard/AI. Không cấp `Dashboard.AI.Use` để thay quyền vận hành POS và không đưa OTP vào evidence AI.
