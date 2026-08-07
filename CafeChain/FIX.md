# ROLE

Bạn hãy đóng vai là một Tech Lead/Solution Architect có hơn 20 năm kinh nghiệm phát triển hệ thống POS, ERP, Inventory, StaffHub và các hệ thống doanh nghiệp quy mô lớn sử dụng ASP.NET Core MVC, Entity Framework Core, SQL Server, SignalR và Layered Architecture.

Đây là lần refactor và kiểm thử cuối cùng trước khi nghiệm thu.

Mục tiêu không phải chỉ sửa lỗi đang nhìn thấy mà phải phân tích toàn bộ nghiệp vụ, tìm Root Cause, refactor đúng kiến trúc, đảm bảo không phát sinh Regression và đảm bảo toàn bộ module hoạt động ổn định.

==========================================================
I. YÊU CẦU BẮT BUỘC
==========================================================

Trước khi sửa bất kỳ dòng code nào, hãy thực hiện đầy đủ các bước sau:

1. Đọc toàn bộ flow hiện tại.

2. Trace toàn bộ luồng từ:

UI
→ Controller
→ Service
→ Repository
→ Database
→ SignalR
→ Middleware
→ Authentication
→ Session
→ Notification
→ Background Service

3. Phân tích Root Cause của từng lỗi.

4. Không sửa theo hiện tượng.

5. Không fix bằng cách hide UI hoặc bypass validation.

6. Chỉ bắt đầu sửa khi đã xác định được nguyên nhân gốc.

7. Nếu phát hiện nhiều nguyên nhân thì phải liệt kê đầy đủ trước khi sửa.

==========================================================
II. KIỂM TRA TOÀN BỘ MODULE LIÊN QUAN
==========================================================

Không chỉ sửa file đang lỗi.

Hãy inspect toàn bộ các module liên quan:

- POS
- StaffHub User
- StaffHub Business
- Notification
- Terminal
- Terminal Registration
- OTP
- POS Session
- Shift
- Authentication
- Authorization
- SignalR
- Background Service
- Middleware
- DTO
- ViewModel
- Entity
- Repository
- Service
- Controller

Nếu phát hiện logic bị duplicate thì phải hợp nhất.

Không để nhiều nơi xử lý cùng một nghiệp vụ.

==========================================================
III. REFACTOR THÔNG BÁO OTP
==========================================================

Hiện tại sau khi người dùng đóng popup OTP thì không còn cách nào xem lại OTP nếu OTP vẫn còn hiệu lực.

Đây là trải nghiệm chưa tốt.

Refactor lại như sau.

Mỗi yêu cầu đăng ký Terminal phải sinh một Notification riêng.

Notification phải lưu:

- Terminal
- Store
- Người gửi yêu cầu
- Người xác nhận
- Thời gian gửi
- Thời gian hết hạn
- Trạng thái OTP

Bao gồm:

Waiting

Used

Expired

Cancelled

Không lưu duy nhất một chuỗi text.

----------------------------------------------------------

Trong Notification Detail hoặc Modal Notification luôn có:

- Xem OTP
- Tiếp tục xác nhận Terminal

Nếu:

OTP chưa hết hạn

và

OTP chưa sử dụng

thì cho phép mở lại popup nhập OTP.

Không bắt người dùng gửi OTP mới.

----------------------------------------------------------

Mỗi Notification phải có nút:

Đánh dấu đã đọc

riêng.

Không chỉ có:

Đánh dấu tất cả.

Đánh dấu một Notification không được ảnh hưởng Notification khác.

----------------------------------------------------------

Notification phải hiển thị:

Tên Terminal

Tên chi nhánh

Người gửi

Thời gian gửi

Thời gian hết hạn

Trạng thái

Thời gian còn lại

Ví dụ:

OTP còn hiệu lực:

08 phút 35 giây

Không hiển thị timestamp ISO.

----------------------------------------------------------

Notification phải tự cập nhật trạng thái nếu:

OTP hết hạn

OTP đã dùng

OTP bị hủy

Không cần refresh.

Nếu hệ thống đã có SignalR thì sử dụng SignalR.

----------------------------------------------------------

Nếu OTP hết hạn thì không cho mở popup.

Hiển thị:

OTP đã hết hạn.

Vui lòng gửi yêu cầu mới.

==========================================================
IV. REFACTOR POPUP ĐĂNG KÝ TERMINAL
==========================================================

Hiện tại sau khi gửi OTP thành công vẫn hiển thị nút:

Gửi OTP

Điều này gây hiểu nhầm.

Sau khi gửi OTP thành công:

Ẩn hoàn toàn nút Gửi OTP.

Thay bằng:

✓ OTP đã được gửi.

Đồng thời hiển thị countdown.

Ví dụ:

Gửi lại sau

00:29

00:28

...

Khi countdown kết thúc:

Đổi thành:

Gửi lại OTP.

Không được hiển thị đồng thời:

Gửi OTP

và

Gửi lại OTP.

----------------------------------------------------------

Trong lúc gửi OTP:

Disable Button

Loading

Disable Double Click

Disable Spam

----------------------------------------------------------

Nếu đóng popup rồi mở lại:

Countdown phải tiếp tục.

Không reset.

Countdown phải lấy từ Server.

Không lấy từ Client.

----------------------------------------------------------

Nếu OTP vẫn còn hiệu lực:

Không tạo OTP mới.

Không gửi Email mới.

Server trả về OTP hiện tại.

==========================================================
V. REFACTOR LỖI POS SESSION
==========================================================

Hiện tại xuất hiện lỗi:

Phiên POS đã hết hạn

mặc dù:

Terminal vừa đăng ký

vừa mở ca

mở ca ngoài lịch

refresh

đổi người thao tác

nhưng hệ thống vẫn giao dịch bình thường.

Đây là lỗi logic.

Không được fix bằng cách ẩn thông báo.

Phải tìm đúng Root Cause.

Kiểm tra toàn bộ flow:

Terminal

↓

Authentication

↓

Session

↓

Shift

↓

POS Session

↓

Current Session

↓

Current Shift

↓

Notification

↓

UI

----------------------------------------------------------

Chỉ hiển thị:

Phiên POS đã hết hạn

khi:

Session thật sự hết hạn

Terminal bị revoke

Session bị logout

Admin kết thúc Session

Token hết hạn

Terminal bị khóa

Không hiển thị nếu:

Mở ca thành công

Mở ca ngoài lịch

Đăng ký Terminal mới

Refresh

Đổi người thao tác

==========================================================
VI. CẢI THIỆN COUNTDOWN POS SESSION
==========================================================

Hiện tại hiển thị:

359 phút

380 phút

Khó hiểu.

Đổi thành:

HH:mm:ss

Ví dụ:

05:59:59

05:59:58

...

Nếu nhỏ hơn 1 giờ:

MM:ss

Ví dụ:

29:59

29:58

Countdown giảm từng giây.

Không hiển thị tổng số phút.

==========================================================
VII. CHUẨN HÓA NGHIỆP VỤ MỞ CA TRỄ
==========================================================

Đọc lại toàn bộ:

StaffHub User

StaffHub Business

Sau đó chuẩn hóa lại nghiệp vụ.

----------------------------------------------------------

0 → 15 phút

Cho mở ca.

Ghi Audit Log.

Hiển thị:

Mở ca trễ X phút.

----------------------------------------------------------

15 → 30 phút

Cho mở.

Bắt buộc nhập lý do.

Lưu Audit Log.

Store Manager xem được.

----------------------------------------------------------

Trên 30 phút

Không cho POS tự mở.

Hiển thị:

Ca làm đã quá hạn hơn 30 phút.

Vui lòng liên hệ Quản lý để xác nhận.

Manager có thể:

Duyệt mở ca

Từ chối

Hoặc chuyển sang ca ngoài lịch.

----------------------------------------------------------

Nếu ca đã kết thúc từ lâu:

Không cho mở lại ca cũ.

Chỉ được:

Tạo ca ngoài lịch

hoặc

Manager tạo ca bổ sung.

==========================================================
VIII. CẬP NHẬT TÀI LIỆU STAFFHUB
==========================================================

Sau khi refactor phải cập nhật:

StaffHub User

StaffHub Business

bao gồm:

Flow mở ca

Flow mở ca ngoài lịch

Flow mở ca trễ

Flow duyệt

Flow Notification

Flow Session

Flow Terminal

Flow OTP

Flow Audit

Đảm bảo tài liệu và code đồng bộ.

==========================================================
IX. VALIDATION & SECURITY
==========================================================

Kiểm tra đầy đủ:

✓ Double Click

✓ Double Submit

✓ Spam OTP

✓ Spam mở ca

✓ OTP hết hạn

✓ OTP đã dùng

✓ OTP bị hủy

✓ Refresh Browser

✓ Browser Back

✓ Session Timeout

✓ Session Revoke

✓ Hai Browser

✓ Hai Terminal

✓ Hai nhân viên

✓ SignalR Disconnect

✓ SignalR Reconnect

✓ Network Error

✓ SQL Timeout

✓ Deadlock

✓ Race Condition

✓ Concurrent Register Terminal

✓ Concurrent Open Shift

✓ Duplicate Notification

✓ Duplicate OTP

✓ Duplicate Session

✓ Idempotency

✓ Optimistic Concurrency

✓ Audit Log

✓ Transaction

==========================================================
X. PERFORMANCE
==========================================================

Kiểm tra:

N+1 Query

Pagination

Memory Leak

Connection Pool

SignalR Broadcast

Lazy Loading

Caching

Dispose

Background Job

Không tạo query dư thừa.

==========================================================
XI. ERROR HANDLING
==========================================================

Thiết kế đầy đủ Error Handling cho:

SMTP lỗi

SignalR mất kết nối

Database Timeout

SQL Deadlock

Network Error

Terminal Offline

Session Expired

OTP Expired

Duplicate Request

Retry

Rollback

==========================================================
XII. ANTI REGRESSION
==========================================================

Sau khi refactor phải kiểm tra ảnh hưởng tới:

POS

StaffHub

Admin

Notification

Authentication

Authorization

Inventory

Payment

Order

Shift

Terminal

OTP

Session

Không được tạo Regression.

==========================================================
XIII. QUY TẮC IMPLEMENT
==========================================================

Không Hard Code.

Không duplicate logic.

Không tạo file dư thừa nếu không cần.

Ưu tiên tái sử dụng Service hiện có.

Controller không chứa Business Logic.

Service không truy cập trực tiếp DbContext nếu dự án đã dùng Repository.

Countdown, OTP và Session phải lấy từ Backend làm Single Source of Truth.

Mọi thao tác quan trọng phải ghi Audit Log.

==========================================================
XIV. TEST CUỐI CÙNG (BẮT BUỘC)
==========================================================

Đây là lần kiểm thử cuối cùng trước khi nghiệm thu.

Sau khi hoàn thành toàn bộ việc refactor, KHÔNG kết thúc ngay.

Hãy thực hiện đầy đủ quá trình kiểm thử cuối cùng (Final Verification):

1. Kiểm tra lại toàn bộ các chức năng đã chỉnh sửa.
2. Kiểm tra toàn bộ luồng nghiệp vụ từ đầu đến cuối.
3. Chạy lại toàn bộ các Validation.
4. Kiểm tra các Boundary Case.
5. Kiểm tra Negative Test.
6. Kiểm tra Concurrency Test.
7. Kiểm tra Race Condition.
8. Kiểm tra SignalR.
9. Kiểm tra Session.
10. Kiểm tra OTP.
11. Kiểm tra Shift.
12. Kiểm tra Notification.
13. Kiểm tra Terminal Registration.
14. Đảm bảo không còn lỗi UI, Business Logic hoặc Security.
15. Chỉ khi toàn bộ các kiểm thử đều đạt thì mới được xem là hoàn thành.

==========================================================
XV. BÁO CÁO SAU KHI HOÀN THÀNH
==========================================================

Sau khi hoàn thành, hãy xuất báo cáo đầy đủ gồm:

1. Root Cause của từng lỗi.
2. Business Changes.
3. Technical Changes.
4. Database Changes (nếu có).
5. API Changes (nếu có).
6. UI Changes.
7. SignalR Changes.
8. Các file đã chỉnh sửa.
9. Test Cases đã thực hiện.
10. Kết quả Final Verification.
11. Regression Risks còn lại (nếu có).
12. Đề xuất cải tiến trong tương lai.

Chỉ kết thúc khi toàn bộ yêu cầu trên đã được hoàn thành và quá trình Final Verification không còn phát hiện lỗi nào.