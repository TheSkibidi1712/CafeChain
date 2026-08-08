# Báo cáo refactor Dashboard, AI, StaffHub/POS và Procurement

Ngày xác minh: 2026-08-08.

## 1. Files inspected

Đã rà soát controller/view/DTO Dashboard, permission/role/scope, `Scripts/SeedAll.sql`, EF configuration/migration, StaffHub/POS/OTP/WorkShift, AI Dashboard, Operational Anomaly, Supplier Intelligence, Purchase Advice và Purchase Order.

## 2. Current architecture discovered

- ASP.NET Core MVC + API, EF Core/SQL Server, StaffHub Razor/JavaScript và POS React/Vite.
- RBAC dùng permission kết hợp `StaffScope`; Dashboard dùng stored procedure analytics.
- OTP có notification nội bộ + email; POS access session và WorkShift là hai aggregate riêng.
- AI dùng dữ liệu structured do backend chuẩn bị.

## 3. Business inconsistencies found

- `IssuePosToken` từng tạo WorkShift với tiền đầu ca bằng 0 trước lúc người dùng xác nhận.
- Hủy modal không hủy OTP/approval/session active; SMTP lỗi chưa được diễn đạt rõ.
- SystemAdmin từng được bypass/blanket business permission.
- Dashboard/AI authorization bị phân tán; anomaly và supplier thiếu một số rule deterministic, feature gate và audit.
- EF `HasData` và `SeedAll.sql` có thể tạo policy khác nhau.

## 4. Authorization changes

Đã tập trung Dashboard authorization, bỏ SystemAdmin bypass, áp dụng entry + section/widget/capability + StaffScope, Account Deny ưu tiên và kiểm tra Store trước query. Bổ sung permission AI/financial/anomaly và kích hoạt supplier selection.

## 5. Seed/Migration changes

Không sửa migration hoặc EF configuration trong đợt này. `SeedAll.sql` tiếp tục là nguồn default permission/feature setting authoritative; đã bổ sung Supplier pilot bằng `SystemSettings` theo kiểu insert-if-missing, giữ nguyên custom setting/override. Script chạy trên database do caller chọn và chặn nhầm `master/model/msdb/tempdb`, thay vì âm thầm chuyển sang database tên cố định.

## 6. Backend changes

Ngoài các thay đổi WorkShift/OTP/Dashboard đã có, đã bổ sung runtime `ISupplierIntelligenceFeatureGate` đọc `SystemSettings` và fallback options, kiểm gate/permission/StaffScope trước repository. Notification mở ca trễ có typed deep-link tới hàng đợi duyệt; anomaly có presentation mapping tiếng Việt tập trung.

## 7. Frontend changes

Admin layout có menu **Tín hiệu vận hành**, **Duyệt mở ca trễ** theo effective permission và giữ nhóm **Kho & Cung ứng** mở tại Phiếu nhận hàng. Notification có **Xem và duyệt yêu cầu**. Purchase Advice hiển thị Supplier comparison bằng modal/loading/error rõ ràng. Anomaly dùng tên nghiệp vụ tiếng Việt; nội dung AI được dựng bằng DOM/text an toàn.

## 8. AI changes

AI classify domain trước DataPlan, dùng chung Dashboard authorization, authorize lại cached context và giữ deterministic fallback khi Ollama lỗi. Skill/fallback anomaly đã bỏ thuật ngữ thống kê khỏi nội dung chính, đưa ra dữ liệu nên kiểm tra và không suy đoán nguyên nhân, gian lận hoặc cá nhân chịu trách nhiệm.

## 9. Operational Anomaly changes

Ngoài pipeline deterministic hiện có, DTO/UI/notification/explanation đã map `REVENUE`, `ORDER_COUNT`, `WASTE_ADJUSTMENT`, `CASH_DISCREPANCY`, `SUPPLIER_ISSUE`, `PRODUCT_VOLUME:*`, severity, confidence, status và reason code sang tiếng Việt. Mã/phiên bản/điểm chuẩn hóa chỉ nằm trong **Thông tin kỹ thuật**.

## 10. Supplier Intelligence changes

Compare dùng runtime feature gate, trả feature mode/source và lỗi nghiệp vụ rõ khi Store ngoài allowlist. Modal hiển thị packaged/loose, quantity/package, total/excess, score nullable, confidence/rankability và warning; không gọi một candidate là tốt nhất khi chưa đủ cạnh tranh.

## 11. Feature flag changes

Supplier Intelligence được seed pilot tại **CafeChain Thủ Dầu Một** với `Enabled=true`, `ShadowMode=true`, `FullRollout=false`, allowlist Store 1. Existing setting không bị ghi đè; allowlist rỗng không tự bật toàn chuỗi.

## 12. Audit changes

Đã bổ sung/giữ audit cho OTP/open intent/session, anomaly transition/feedback và PO tạo từ Supplier Intelligence. Record `CANCELLED` được giữ để truy vết.

## 13. Tests added/run – PHẦN CUỐI

- Backend build; frontend production build + ESLint.
- 49 targeted tests cho Supplier gate/anomaly presentation/Admin layout/deep-link/OTP/Current Operator/late approval/Purchase Advice.
- Regression `FullyQualifiedName!~SqlServer`: 1.866 pass; một test tên `RuntimeModel_*` vẫn khởi tạo SQL và bị SSPI.
- Database `CafeChain_RefactorVerify_20260808`: migration hiện tại áp dụng thành công; `SeedAll.sql` chạy lặp trực tiếp và tổng dòng cuối không đổi.
- Full SQL regression đã thử với connection template tới `localhost\SQLEXPRESS02` nhưng test runner bị `Failed to generate SSPI context`.
- Backend local trả trang login HTTP 200 khi chạy ngoài sandbox; Browser plugin không có browser backend (`[]`), nên browser click/E2E không chạy.

## 14. Test results

- Backend: 0 error; còn 676 warning hiện hữu/toàn solution.
- Frontend build/lint: đạt; còn warning chunk size và annotation từ dependency SignalR.
- Targeted: 49/49 đạt; contract tài liệu phát hiện sau full run đã sửa và test lại 1/1 đạt.
- Non-SQL regression thực tế: 1.866 test đạt; test còn lại bị phụ thuộc SQL/SSPI, không phải assertion nghiệp vụ.
- Fresh database/migration/SeedAll direct: đạt; permission matrix xác nhận Owner/Area/Store Manager có `ApproveLateOpen`, Shift Supervisor không có; Supplier pilot keys đúng và không tăng tổng dòng khi rerun.
- SQL test runner và browser E2E: chưa đạt điều kiện môi trường để nghiệm thu.

## 15. Remaining risks

- Full SQL xUnit bị SSPI dù `sqlcmd`/EF migrate/SeedAll trực tiếp dùng cùng instance hoạt động; chưa thể coi SQL regression pass.
- Browser plugin không có browser backend nên chưa kiểm click/auth/session thực tế cho menu, Receipt, late approval, Supplier modal và anomaly modal.
- Chưa áp migration/SeedAll lên production; chưa có dữ liệu ShadowMode/pilot để đánh giá false positive/data quality.
- Dashboard repository hiện vẫn có section path có thể tải rộng hơn widget trước khi lọc response.
- Cancel OTP/approval/session đi qua nhiều service transaction riêng, chưa phải một distributed transaction duy nhất.
- Supplier performance còn N+1 theo candidate; batch PO chưa có audit snapshot đầy đủ như PO đơn.
- Một số metric anomaly ngoài revenue còn cần kiểm chứng sâu việc nhóm ngày UTC/local.
- Database kiểm thử `CafeChain_RefactorVerify_20260808` vẫn được giữ để đối chiếu; không phải database production.

## 16. Recommended rollout state

Giữ Supplier Intelligence ở đúng **ShadowMode + allowlist Store CafeChain Thủ Dầu Một**, không bật FullRollout. Operational Anomaly chỉ nên dùng pilot theo feature gate hiện có. Chưa đủ cơ sở tuyên bố production-ready cho đến khi xử lý môi trường SQL xUnit, chạy browser/E2E và thu thập dữ liệu vận hành pilot.
