# Hướng dẫn sử dụng Dashboard, AI, Operational Anomaly và Supplier Intelligence

## 1. Tài khoản demo

Các account dưới đây được tạo bởi dữ liệu demo trong `Scripts/SeedAll.sql` và dùng mật khẩu demo `The@1712`. Chỉ dùng môi trường local/test; không dùng credential production.

| Vai trò | Tài khoản | Mục đích |
|---|---|---|
| Business Owner | `owner@cafechain.vn` | Dashboard đầy đủ theo scope, AI/anomaly/supplier |
| Area Manager | `areamanager@cafechain.vn` | kiểm tra dữ liệu vùng và Store trong vùng |
| Store Manager | `storemanager@cafechain.vn` | kiểm tra một/nhiều Store được gán |
| Accountant/Warehouse | `accountantwarehouse@cafechain.vn` | tài chính đối soát, kho, mua hàng, supplier |
| System Admin | `systemadmin@cafechain.vn` | negative case: không được xem business data nếu chưa cấp explicit permission + scope |

Nếu menu/nút khác hướng dẫn, kiểm tra đã chạy migration mới và `Scripts/SeedAll.sql`, account/staff còn active, permission không bị Deny và StaffScope có Store đang chọn.

## 2. Mở Dashboard

1. Đăng nhập account phù hợp.
2. Từ App Launcher bấm **Admin Dashboard** hoặc mở `/Admin/Dashboard`.
3. Tab đầu tiên là section đầu tiên backend cho phép; tab không có quyền sẽ không xuất hiện.
4. Chọn thời gian, Province/Ward/Store. Store ngoài scope không xuất hiện; sửa URL thủ công phải nhận 403.
5. Mỗi widget chỉ xuất hiện khi có permission riêng. Ví dụ có Executive nhưng thiếu `Dashboard.FinancialSummary.View` thì không thấy số tài chính.

Kỳ vọng theo account:

- Owner: thấy các section được seed trong toàn bộ scope được gán.
- Area Manager: chỉ Store thuộc vùng được gán; aggregate không chứa vùng khác.
- Store Manager: một Store thì selector bị khóa/ẩn; nhiều Store chỉ hiện danh sách được gán.
- Accountant: thấy financial summary/kho/mua hàng theo quyền; không mặc định thấy nhân sự/anomaly.
- System Admin: không thấy Admin Dashboard business theo mặc định. Đây là kết quả đúng, không phải lỗi UI.

## 3. Dùng AI Dashboard

1. Mở tab **AI Dashboard** khi có `Dashboard.AI.Use` và ít nhất một widget/domain được phép.
2. Chọn bộ lọc trước, nhập câu hỏi đúng domain đang được phép, ví dụ “Giải thích biến động doanh thu của Store đang chọn”.
3. Bấm nút phân tích/gửi câu hỏi theo giao diện.
4. Kiểm tra câu trả lời nêu evidence, khoảng thời gian, warning/confidence; AI không được tạo PO, resolve anomaly hoặc gọi một NCC là lựa chọn bắt buộc.

Nếu hỏi domain không có quyền, backend trả 403 và không query evidence. Nếu Ollama không chạy, UI vẫn phải nhận phần deterministic và thông báo fallback, không mất metric/score.

## 4. Operational Anomaly

1. Đăng nhập Owner/Manager đã có `OperationalAnomaly.View` và StaffScope.
2. Mở **Nhân sự & Vận hành → Tín hiệu vận hành** hoặc `/Admin/AdminOperationalAnomalies`.
3. Chọn Store trong selector. Danh sách chính hiển thị **Chỉ số cần kiểm tra**, **Giá trị ghi nhận**, **Mức thông thường trước đây**, **Mức chênh lệch**, **Mức cần ưu tiên** và **Trạng thái xử lý** bằng tiếng Việt. Bấm **Thông tin kỹ thuật** khi thực sự cần xem mã, phiên bản phát hiện hoặc điểm chuẩn hóa.
4. Với quyền tương ứng:
   - bấm **Tiếp nhận** để ghi nhận đã xem;
   - bấm **Đánh dấu đã xử lý**, nhập ghi chú để hoàn tất;
   - bấm **Phản hồi**, chọn **Hữu ích**, **Không hữu ích** hoặc **Cảnh báo không phù hợp**;
   - bấm **Giải thích dễ hiểu** để xem diễn giải và danh sách dữ liệu nên kiểm tra.
5. Refresh và xác nhận state/feedback không bị worker reset.

Lỗi 403 nghĩa là thiếu action permission hoặc Store ngoài scope. Lỗi 409 thường là rowversion đã đổi; tải lại trang rồi thao tác trên bản mới. Không dùng anomaly để kết luận gian lận.

## 5. So sánh nhà cung cấp từ Purchase Advice

1. Đăng nhập `accountantwarehouse@cafechain.vn` hoặc manager/owner có `PurchaseAdvice.View`, `PurchaseAdvice.SelectSupplier`, `SupplierQuality.View` và quyền tạo PO.
2. Vào **Kho & Cung ứng** → **Đề nghị mua hàng**.
3. Mở đề nghị, chuyển từ **Nháp** bằng **Chuyển sang xem xét**; người duyệt bấm **Bắt đầu xem xét** để đạt `UNDER_REVIEW`.
4. Bấm **Chọn nhà cung cấp và tổng hợp**.
5. Tại từng dòng, bấm **So sánh nhà cung cấp**. Store **CafeChain Thủ Dầu Một** được seed làm pilot `ShadowMode`; modal kết quả hiển thị nhà cung cấp, hình thức mua, số gói/số lượng, tổng tiền, lượng dư, điểm có thể chưa tính được, độ đầy đủ dữ liệu và cảnh báo.
6. Chọn Nhà cung cấp/quy cách/hình thức mua, tick dòng cần xử lý rồi bấm **Kiểm tra bản tổng hợp**.
7. Đọc lại MOQ, số gói/mua rời, phần phủ nhu cầu, phần dư và tổng tiền.
8. Bấm **Tạo đơn đặt hàng** hoặc **Tạo đơn đặt hàng gộp**. Backend tái kiểm tra dữ liệu; không tin phép tính frontend.
9. Người có `PurchaseOrder.Approve` mở PO/batch và bấm **Duyệt**. Người chỉ có View/SelectSupplier không thấy hoặc không gọi được nút duyệt.

Nếu Supplier Intelligence OFF, Store ngoài allowlist, không có candidate, conversion lỗi hoặc telemetry lỗi, vẫn có thể tiếp tục procurement thủ công nếu dữ liệu offer hợp lệ. `ShadowMode` chỉ gắn nhãn dữ liệu pilot và không tự chọn hoặc tạo PO. Một candidate hoặc một candidate rankable chỉ là dữ liệu tham khảo, không phải “recommended supplier”.

## 6. Lỗi thường gặp

| Hiện tượng | Kiểm tra |
|---|---|
| 403 khi mở Dashboard | `App.AdminDashboard`, section permission, account Deny |
| 403 khi đổi Store | StaffScope/Store assignment; không sửa URL để vượt scope |
| Tab có nhưng widget trống/ẩn | widget capability riêng, dữ liệu trong khoảng chọn |
| AI báo access denied | `Dashboard.AI.Use` và permission đúng domain/widget |
| Anomaly không có dòng | feature OFF/shadow, allowlist, chưa đủ 14 observation hoặc không vượt threshold |
| Compare trả 409 | feature chưa enabled cho Store/allowlist |
| Candidate không rankable | receipt dưới ngưỡng, missing metric hoặc lead time fallback |
| Tạo PO thất bại | trạng thái PA, rowversion, MOQ, conversion, quantity còn lại, permission/scope |

## 7. Hướng dẫn liên quan

- [Quy tắc nghiệp vụ Dashboard/AI/Anomaly/Supplier](./DASHBOARD_AI_ANOMALY_SUPPLIER_BUSINESS_RULES.md)
- [Hướng dẫn StaffHub/POS](./STAFFHUB_USER_BUSINESS_FLOWS.md)
- [Quy tắc StaffHub/POS/WorkShift](./STAFFHUB_POS_WORKSHIFT_BUSINESS_RULES.md)
- [Hướng dẫn đăng ký Terminal POS](./POS_TERMINAL_USER_GUIDE.md)

OTP mở ngoài lịch và đăng ký Terminal là luồng POS riêng. Không tìm OTP hoặc nút **Xác nhận Terminal** trong Dashboard/AI.
