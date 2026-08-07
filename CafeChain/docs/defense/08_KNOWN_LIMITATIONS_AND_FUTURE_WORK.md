# Hạn chế và hướng phát triển

## Cách phân loại

| Trạng thái | Ý nghĩa |
|---|---|
| Đã hoàn thiện | Có code authority và test/evidence đủ cho contract nêu ra |
| Đang hoàn thiện | Có phần triển khai nhưng còn compatibility/repair hoặc runtime gap |
| Có thiết kế, chưa runtime verify | Code/ADR có nhưng chưa xác minh tương tác trong task docs |
| Chưa triển khai | Không tìm thấy contract hoàn chỉnh trong code |
| Nợ kỹ thuật | Hoạt động nhưng cấu trúc/khả năng bảo trì còn hạn chế |

## Đã hoàn thiện ở mức code

| Hạng mục | Evidence |
|---|---|
| Permission code + StoreScope + controller/service guards | Authorization extensions, permission seed, controllers |
| POS idempotency bằng `ClientOrderId` | `POSOrderService`, unique index/ADR-0002 |
| PO/POB maker-checker | `PurchaseOrderService`, `PurchaseOrderBatchService` |
| Receipt confirmed mới tăng tồn | `BranchReceiptService`, ADR-0008 |
| Snapshot PO/receipt UOM và giá | Entity procurement/receipt |
| FIFO completeness và Margin/Markup | Profitability services |
| WorkShift row version/request dedup/reconcile | `WorkShiftService` |
| Operational Ice posting idempotency | `OperationalIceService` |

## Hạn chế đã xác định

| Hiện trạng | Phân loại | Ảnh hưởng | Xử lý tạm thời | Hướng phát triển |
|---|---|---|---|---|
| Database demo còn status legacy như Restock `OPEN`, PA `APPROVED` ngoài constants hiện hành | Rủi ro dữ liệu | Demo state có thể không khớp state machine mới | Chọn record mới/rehearsal; ghi rõ legacy | Repair idempotent + constraint/normalized status report |
| Runtime theo từng role chưa được click-through trong task tài liệu | Có thiết kế, chưa runtime verify | Chưa chứng minh menu/action thực tế trong phiên này | Chạy script rehearsal trước bảo vệ | Tạo automated role-navigation smoke |
| Audit là mô hình lai: generic JSON + transition/audit entity riêng | Nợ kỹ thuật/UX | Khó truy vấn timeline liên module | Dùng module-specific audit UI | Chuẩn hóa event envelope và projection nghiệp vụ |
| Một số service legacy còn đưa `ex.Message` vào message | Nợ kỹ thuật/security UX | Có nguy cơ lộ technical detail | Dùng luồng đã harden trong demo | Central business error mapper + correlation ID |
| SystemAdmin không có mọi permission nghiệp vụ | Thiết kế có chủ đích | Người demo dễ hiểu nhầm “admin = superuser” | Dùng đúng account nghiệp vụ | Tài liệu onboarding permission rõ hơn |
| Offline blind selling có thể tạo tồn âm | Thiết kế có chủ đích/rủi ro dữ liệu | Cần đối soát sau sync | Dashboard cảnh báo và reconciliation | Policy negative limit theo store/item khi business chốt |
| Trừ tồn POS sau commit retry-safe nhưng chưa có worker/outbox recovery riêng được code chứng minh | Nợ độ tin cậy | Crash sau commit có thể để side effect thiếu cho tới lần retry/repair | Retry idempotent theo Order và theo dõi warning/log | Hiện thực durable intent, lease, retry/dead-letter và dashboard theo ADR-0009 |
| External PayOS/SMTP/Cloudinary/print bridge phụ thuộc môi trường | Rủi ro demo | Demo có thể lỗi dù core code đúng | Fallback tiền mặt/ảnh local/read-only | Health checks và demo-mode emulator |
| Costing phụ thuộc BOM, conversion và FIFO layer đầy đủ | Rủi ro dữ liệu | Preview có thể incomplete | Hiển thị status từng section, không coi thiếu = 0 | Data quality dashboard và guided repair |
| Topping replacement cost tổng quát chưa có treatment authority trong constants | Chưa triển khai hoàn chỉnh | Không thể khẳng định thay thế component không stack trong mọi trường hợp | Chỉ demo included/additional modes | Thêm replacement mapping, validation và snapshot |
| `Recipe`/BTP còn compatibility fields và comments legacy | Đang hoàn thiện | Dễ nhầm Recipe version là inventory identity | Dùng `PreparedItem` làm authority | Hoàn tất cutover, bỏ alias sau data repair |
| UI admin Razor và POS React dùng hai frontend stack | Nợ kỹ thuật có chủ đích | Tăng chi phí consistency/tooling | Shared API/visual token/docs | Shared component/tokens hoặc BFF contract rõ hơn |
| Lazy-loading proxies tồn tại cùng projection/service queries | Nợ kỹ thuật/performance | N+1 nếu code mới dùng navigation không kiểm soát | `AsNoTracking`/projection ở list | Query profiling và ban lazy load trong read model mới |
| Build targeted phát cảnh báo `EF1002` ở raw SQL legacy BTP consolidation | Nợ kỹ thuật/security review | Raw SQL interpolation cần được chứng minh sanitized hoặc chuyển API parameterized | Không dùng CLI repair legacy trong demo; giữ input kiểm soát | Tạo issue riêng, chuyển sang `FromSql`/parameter hoặc suppress có evidence |
| Dashboard KPI có metadata riêng từng widget | Nợ tài liệu | Dễ nói sai denominator/filter | Tra `DashboardWidgetCatalog` khi demo | Data dictionary sinh tự động từ widget metadata |
| Online customer flow chưa được runtime verify | Có thiết kế, chưa runtime verify | Không nên coi ngang mức POS trong bảo vệ | Demo POS staff | Tạo end-to-end customer checkout smoke |

## Những điều không nên phóng đại

1. Không gọi kiến trúc là microservices; đây là modular monolith ASP.NET Core.
2. Không nói toàn bộ hệ thống event-sourced; audit là nhiều cơ chế kết hợp.
3. Không nói AI tự quyết định mua hàng; recommendation chỉ hỗ trợ, actor vẫn xác nhận.
4. Không nói tồn không bao giờ âm; ADR cho phép âm trong blind selling/offline.
5. Không nói mọi role đã runtime pass trong task docs; chỉ code/test và boundary runtime đã xác nhận.
6. Không nói topping replacement đã hoàn chỉnh khi constants chưa có mode đó.
7. Không nói receipt rejected quantity tăng tồn.

## Ưu tiên phát triển sau đồ án

| Ưu tiên | Hạng mục | Lợi ích |
|---:|---|---|
| P0 | Rehearsal automation cho role navigation và handoff | Giảm rủi ro demo/regression quyền |
| P0 | Repair/constraint status legacy | State machine và data thống nhất |
| P1 | Unified audit projection + correlation ID | Truy vết và bảo mật lỗi tốt hơn |
| P1 | Data-health center cho UOM/BOM/FIFO/supplier | Chủ động chặn costing/procurement lỗi |
| P1 | Observability cho payment, deduction jobs và notification | Phát hiện retry/dead-letter nhanh |
| P2 | Topping replacement contract | Giá và cost chính xác cho tùy chọn thay thế |
| P2 | Dashboard metric catalog tự sinh | Dễ bảo vệ và kiểm toán KPI |
| P2 | Loại bỏ compatibility aliases sau cutover | Giảm nợ kỹ thuật BTP/UOM |
