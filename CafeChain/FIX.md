# PROMPT: RÀ SOÁT VÀ CẢI THIỆN NGHIỆP VỤ STAFFHUB – POS – CA LÀM VIỆC – OTP/PIN – TERMINAL

Bạn hãy đọc và phân tích toàn bộ source code hiện tại liên quan đến **StaffHub, POS, ca làm việc, phân quyền, OTP/PIN và đăng ký Terminal** trước khi chỉnh sửa.

Mục tiêu là **sửa đúng nghiệp vụ ở cả Backend và Frontend**, không chỉ vá lỗi UI. Phải kiểm tra API, middleware/guard phân quyền, trạng thái ca, trạng thái terminal và các trường hợp truy cập trực tiếp bằng URL.

Ưu tiên:

1. Đúng nghiệp vụ và bảo mật.
2. Backend là nguồn xác thực cuối cùng.
3. Frontend chỉ hỗ trợ UX, không được dùng frontend để thay thế authorization phía backend.
4. Hạn chế phá vỡ nghiệp vụ đang hoạt động.
5. Tái sử dụng code hiện tại nếu hợp lý.
6. Refactor những đoạn logic trùng lặp hoặc khó bảo trì.
7. Test toàn bộ sau khi hoàn tất các thay đổi.

---

# 1. CHẶN TRUY CẬP POS TRỰC TIẾP BẰNG URL

## Vấn đề hiện tại

Hiện tại người dùng có thể truy cập trực tiếp URL của POS mà không cần đi qua StaffHub.

Đặc biệt:

* Nhân viên đã kết thúc ca POS.
* Sau đó nhập trực tiếp URL POS.
* Hệ thống vẫn cho phép truy cập POS.

Đây là lỗi nghiệp vụ và lỗi phân quyền.

## Yêu cầu

POS chỉ được phép truy cập khi người dùng đáp ứng đầy đủ điều kiện nghiệp vụ.

Không được coi việc biết URL là đủ điều kiện truy cập POS.

Backend phải kiểm tra ít nhất:

* Người dùng đã đăng nhập hay chưa.
* Người dùng có quyền sử dụng POS hay không.
* Người dùng có thuộc cửa hàng/chi nhánh tương ứng hay không.
* Terminal có hợp lệ hay không.
* Terminal có được xác nhận/approved hay chưa.
* Người dùng hiện có ca làm việc hợp lệ hay không.
* Ca đã được mở hay chưa.
* Ca đã kết thúc hay chưa.
* Trạng thái ca có cho phép thao tác POS hay không.

Nếu không hợp lệ:

* Backend trả HTTP status phù hợp, ví dụ `401` hoặc `403`.
* Frontend không render màn POS.
* Điều hướng người dùng về StaffHub hoặc màn hình thích hợp.
* Hiển thị thông báo giải thích nguyên nhân.

Ví dụ:

* "Bạn chưa mở ca làm việc."
* "Ca làm việc đã kết thúc."
* "Bạn không có quyền truy cập POS."
* "Terminal chưa được xác nhận."
* "Bạn không thuộc cửa hàng này."

## Lưu ý quan trọng

Không được chỉ làm:

```text
if (!fromStaffHub) redirect(...)
```

vì query param, localStorage, sessionStorage hoặc frontend state đều có thể bị giả mạo.

Quyền truy cập POS phải được Backend xác thực dựa trên session/token + user + terminal + shift + permission.

---

# 2. RÀ SOÁT PHÂN QUYỀN STAFFHUB VÀ POS

Hãy kiểm tra toàn bộ authorization hiện tại của:

* StaffHub.
* POS.
* API StaffHub.
* API POS.
* API mở ca.
* API đóng ca.
* API thay đổi lịch.
* API xác nhận ngoại lệ ca.
* API xác nhận Terminal.

Kiểm tra theo Role và Permission thực tế của hệ thống, không chỉ kiểm tra tên role ở frontend.

Ví dụ các nhóm:

* Owner.
* Manager / Quản lý cửa hàng.
* Staff.
* Các user được cấp permission đặc biệt.

Nếu hệ thống hiện tại hỗ trợ permission-based authorization thì ưu tiên:

```text
Permission
```

thay vì hard-code:

```text
role === OWNER || role === MANAGER
```

Ví dụ:

```text
SHIFT_EDIT
SHIFT_OVERRIDE
POS_ACCESS
TERMINAL_APPROVE
STAFFHUB_ACCESS
```

Tên permission thực tế phải theo convention hiện có trong project.

Backend phải là nơi quyết định cuối cùng user có quyền hay không.

Frontend có thể:

* Ẩn button.
* Disable button.
* Hiển thị thông báo.

Nhưng API vẫn bắt buộc kiểm tra authorization.

---

# 3. CẢI THIỆN NGHIỆP VỤ CA LÀM VIỆC VÀ THAY ĐỔI LỊCH

Hiện tại có các trường hợp xử lý chưa hợp lý.

Ví dụ:

```text
Ca được xếp:
03:00 → 07:00
```

Nhân viên mở modal liên quan đến ca nhưng không bấm chuyển sang POS.

Trong lúc đó Owner/Manager hoặc người có quyền chỉnh lịch thay đổi lịch làm việc.

Hệ thống cần xác định rõ trạng thái nào được dùng làm dữ liệu chính thức.

## Nguyên tắc

Việc chỉ **mở modal** không được coi là đã xác nhận hoặc mở ca.

Ca chỉ được xác nhận khi người dùng thực hiện action chính thức và Backend ghi nhận thành công.

Ví dụ:

```text
Scheduled
→ Pending Confirmation
→ Confirmed
→ Opened
→ Closed
```

Hoặc mapping tương đương với state hiện tại của project.

Không tạo state mới nếu hệ thống hiện có state đủ dùng.

---

# 4. TRƯỜNG HỢP LỊCH BỊ THAY ĐỔI KHI USER ĐANG MỞ MODAL

Ví dụ:

Ban đầu:

```text
03:00 - 07:00
```

Nhân viên mở modal lúc:

```text
02:55
```

Sau đó Manager chỉnh lịch thành:

```text
04:00 - 08:00
```

Nhân viên vẫn đang giữ modal cũ.

Khi nhân viên bấm xác nhận hoặc chuyển POS:

**không được sử dụng dữ liệu schedule cũ trên frontend.**

Frontend phải gọi lại backend để kiểm tra trạng thái ca mới nhất.

Có thể sử dụng một API dạng:

```http
POST /shifts/{shiftId}/validate-open
```

hoặc tích hợp validation vào API mở ca hiện tại.

Backend kiểm tra lại:

```text
Current schedule
Current server time
Shift status
User
Store
Terminal
Permission
Exception status
```

Nếu lịch đã bị chỉnh:

```text
SHIFT_SCHEDULE_CHANGED
```

Frontend thông báo:

```text
"Lịch làm việc của bạn vừa được thay đổi. Vui lòng kiểm tra lại lịch mới trước khi mở ca."
```

Sau đó refresh thông tin ca.

---

# 5. CA TRỄ – CA SỚM – CA NGOÀI LỊCH

Hiện tại hệ thống có trường hợp chỉ hiển thị:

```text
"Ca đã trễ"
```

nhưng chưa cung cấp luồng xử lý phù hợp khi có Manager/Owner/người có quyền override.

Hãy chuẩn hóa nghiệp vụ cho ba trường hợp:

### A. EARLY SHIFT – mở ca sớm

Người dùng mở ca trước thời gian cho phép.

### B. LATE SHIFT – mở ca trễ

Người dùng mở ca sau thời gian bắt đầu hoặc vượt tolerance quy định.

### C. OUTSIDE SCHEDULE – ngoài lịch

Người dùng không có lịch phù hợp tại thời điểm hiện tại.

---

# 6. FLOW XỬ LÝ NGOẠI LỆ CA

Không được chỉ hiển thị lỗi rồi chặn hoàn toàn.

Nếu người dùng thông thường không đủ quyền:

```text
User mở ca
↓
Backend phát hiện EARLY / LATE / OUTSIDE_SCHEDULE
↓
Không cho mở trực tiếp
↓
Yêu cầu xác nhận/override
```

Nếu Owner/Manager hoặc user có permission phù hợp:

```text
SHIFT_OVERRIDE
```

thì hệ thống cho phép xử lý ngoại lệ.

Có thể theo flow:

```text
Phát hiện ngoại lệ
↓
Hiển thị loại ngoại lệ
↓
Nhập lý do
↓
Xác thực PIN/OTP nếu nghiệp vụ yêu cầu
↓
Người có quyền xác nhận
↓
Backend kiểm tra permission lần cuối
↓
Ghi audit log
↓
Mở ca
↓
Cho phép chuyển POS
```

Phải lưu:

* Người mở ca.
* Người xác nhận override.
* Loại ngoại lệ.
* Thời gian dự kiến.
* Thời gian thực tế.
* Lý do.
* Store.
* Terminal.
* Timestamp.
* Các dữ liệu audit hiện có của hệ thống.

Không được silently bypass validation chỉ vì user là Owner/Manager.

---

# 7. TRƯỚC KHI MỞ CA KHÔNG ĐƯỢC CHUYỂN TÁC VỤ

Đây là rule bắt buộc:

```text
Chưa mở ca
→ Không được chuyển tác vụ
```

Ví dụ:

```text
StaffHub → POS
StaffHub → nghiệp vụ yêu cầu active shift
```

phải bị chặn nếu chưa mở ca.

Flow đúng:

```text
Login
↓
StaffHub
↓
Kiểm tra lịch
↓
Xác nhận ca
↓
Xử lý ngoại lệ nếu có
↓
Mở ca thành công
↓
Backend trả active shift
↓
Cho phép chuyển POS/tác vụ
```

Không được:

```text
Login
↓
StaffHub
↓
POS
↓
mới kiểm tra ca
```

API phía POS vẫn phải kiểm tra active shift để tránh bypass bằng URL/API trực tiếp.

---

# 8. CẢI THIỆN OTP VÀ PIN

Hãy refactor UI nhập:

* OTP.
* PIN.

Thành component dùng chung nếu kiến trúc frontend cho phép.

## Giao diện

Hiển thị dạng:

```text
[ A ] [ B ] [ C ] [ 1 ] [ 2 ] [ 3 ]
```

6 ô vuông riêng biệt.

UI phải:

* Responsive.
* Có trạng thái focus rõ ràng.
* Có trạng thái lỗi.
* Có trạng thái disabled.
* Có loading khi verify.
* Không làm layout nhảy khi hiển thị lỗi.

---

# 9. HÀNH VI INPUT OTP/PIN

Hỗ trợ:

### Auto focus

Nhập một ký tự:

```text
[A] → tự chuyển → [B]
```

### Backspace

Nếu ô hiện tại rỗng:

```text
Backspace
→ quay lại ô trước
```

### Paste

Cho phép copy một chuỗi:

```text
ABC123
```

và paste trực tiếp.

Frontend tự động chia thành:

```text
[A][B][C][1][2][3]
```

### Normalize

Input:

```text
abc123
```

phải tự chuyển thành:

```text
ABC123
```

Không phân biệt hoa/thường khi verify.

### Không dấu

Nếu nhập chữ tiếng Việt:

```text
ấ
```

phải xử lý theo rule thống nhất của hệ thống.

Nếu OTP/PIN chỉ cho phép ASCII thì reject ký tự không hợp lệ.

### Không ký tự đặc biệt

Không cho:

```text
@
#
$
%
!
-
_
...
```

### Length

Tối đa:

```text
6 ký tự
```

Không cho nhập ký tự thứ 7.

---

# 10. VALIDATION OTP VÀ PIN

Không mặc định OTP và PIN có cùng format nếu backend hiện tại quy định khác nhau.

Hãy kiểm tra implementation hiện tại.

Ví dụ:

### OTP

Nếu OTP cho phép:

```text
A-Z
0-9
```

thì regex có thể tương đương:

```regex
^[A-Z0-9]{6}$
```

### PIN

Nếu PIN chỉ là số:

```regex
^[0-9]{6}$
```

Nếu PIN cũng alphanumeric thì áp dụng rule tương ứng.

Backend cũng phải validate lại.

Không chỉ validate ở frontend.

---

# 11. NÚT COPY OTP

Khi hệ thống gửi/generate OTP và nghiệp vụ hiện tại có hiển thị OTP cho người dùng, thêm button:

```text
Copy OTP
```

Khi bấm:

```text
Copy OTP vào clipboard
```

Hiển thị feedback:

```text
"Đã sao chép mã OTP"
```

Không copy thêm whitespace.

Lưu ý:

Nếu đây là production authentication OTP được gửi qua email/SMS và không được phép hiển thị trực tiếp, không thay đổi chính sách bảo mật để expose OTP.

Chỉ thêm chức năng copy ở nơi OTP **đã được nghiệp vụ hiện tại cho phép hiển thị**.

---

# 12. XỬ LÝ NHẬP SAI OTP QUÁ NHIỀU LẦN

Cần có giới hạn số lần verify OTP sai.

Không chỉ xử lý bằng frontend.

Backend phải lưu/check trạng thái attempts.

Ví dụ flow:

```text
Verify OTP sai
↓
Tăng failedAttempts
↓
Đạt MAX_FAILED_ATTEMPTS
↓
Khóa resend OTP trong 2 phút
```

Thời gian cooldown:

```text
120 giây
```

Trong thời gian khóa:

* Disable `Gửi lại OTP`.
* Disable các action cần thiết nếu business rule yêu cầu.
* Hiển thị countdown.

Ví dụ:

```text
Gửi lại mã sau 01:59
```

Sau đó:

```text
01:58
01:57
...
00:01
```

Khi:

```text
00:00
```

button được enable lại.

---

# 13. COUNTDOWN PHẢI DỰA TRÊN BACKEND

Không được chỉ dùng:

```text
setTimeout(120000)
```

rồi coi frontend là nguồn dữ liệu chính.

Backend nên trả:

```json
{
  "locked": true,
  "retryAfter": 120
}
```

hoặc:

```json
{
  "lockedUntil": "..."
}
```

Frontend dùng dữ liệu đó để render countdown.

Nếu user:

* Refresh trang.
* Đóng modal.
* Mở lại modal.
* Đăng nhập lại.

thì cooldown vẫn phải còn hiệu lực.

---

# 14. GIỚI HẠN LÝ DO

Các input như:

```text
Lý do mở ca trễ
Lý do mở ca sớm
Lý do làm việc ngoài lịch
Lý do override
```

phải có giới hạn độ dài.

Ví dụ:

```text
Min: 5
Max: 255 hoặc 500
```

Hãy ưu tiên theo convention/database schema hiện có.

UI hiển thị counter:

```text
32 / 255
```

Backend cũng phải validate độ dài.

Không cho:

* Empty string.
* Chỉ chứa whitespace.
* Chuỗi vượt quá giới hạn.

Trim dữ liệu trước khi lưu.

---

# 15. REFACTOR LUỒNG ĐĂNG KÝ VÀ XÁC NHẬN TERMINAL

Hiện có lỗi:

```text
Gửi yêu cầu đăng ký Terminal
↓
QLCH bấm xác nhận
↓
UI bị đơ
↓
Không thông báo lỗi
↓
Terminal cũng không được xác nhận
```

Hãy trace toàn bộ flow từ frontend → API → service → database.

Kiểm tra:

```text
Frontend request
↓
Controller
↓
DTO Validation
↓
Authentication
↓
Authorization
↓
Service
↓
Database
↓
Transaction
↓
Response
↓
Frontend state
```

Không được chỉ sửa symptom `loading`.

---

# 16. TERMINAL STATE MACHINE

Rà soát các trạng thái Terminal hiện tại.

Nếu phù hợp với hệ thống, flow phải tương đương:

```text
UNREGISTERED
↓
PENDING_APPROVAL
↓
APPROVED
```

và có thể:

```text
REJECTED
REVOKED
DISABLED
```

nếu hệ thống hiện tại có hỗ trợ.

Không tạo thêm state dư thừa nếu project đã có state tương đương.

---

# 17. API XÁC NHẬN TERMINAL

Khi Manager/QLCH xác nhận Terminal:

Backend phải:

1. Authentication user.
2. Kiểm tra permission.
3. Kiểm tra Terminal tồn tại.
4. Kiểm tra Store ownership/scope.
5. Kiểm tra trạng thái Terminal hiện tại.
6. Validate transition state.
7. Update Terminal.
8. Ghi approvedBy.
9. Ghi approvedAt.
10. Ghi audit log nếu hệ thống hỗ trợ.
11. Commit transaction.
12. Trả response rõ ràng.

Ví dụ response thành công:

```json
{
  "success": true,
  "message": "Terminal đã được xác nhận.",
  "data": {
    "terminalId": "...",
    "status": "APPROVED"
  }
}
```

Không bắt buộc sử dụng đúng response format trên nếu project đã có response convention riêng.

---

# 18. KHÔNG ĐỂ UI BỊ TREO KHI API LỖI

Frontend phải xử lý đầy đủ:

```text
try
catch
finally
```

hoặc cơ chế tương đương.

Button:

```text
Xác nhận Terminal
```

khi gọi API:

```text
normal
→ loading
→ success/error
→ normal
```

Trong mọi trường hợp:

```text
loading = false
```

phải được thực hiện.

Không để:

```text
Promise rejection
```

không được catch.

---

# 19. HIỂN THỊ LỖI TERMINAL RÕ RÀNG

Không chỉ:

```text
Something went wrong
```

Hãy map các trường hợp thành message rõ ràng.

Ví dụ:

```text
TERMINAL_NOT_FOUND
→ "Không tìm thấy Terminal."
```

```text
TERMINAL_ALREADY_APPROVED
→ "Terminal này đã được xác nhận."
```

```text
TERMINAL_NOT_PENDING
→ "Terminal không ở trạng thái chờ xác nhận."
```

```text
FORBIDDEN
→ "Bạn không có quyền xác nhận Terminal."
```

```text
STORE_SCOPE_INVALID
→ "Bạn không có quyền quản lý Terminal của cửa hàng này."
```

---

# 20. CHỐNG DOUBLE CLICK / DOUBLE REQUEST

Khi Manager bấm:

```text
Xác nhận Terminal
```

phải disable button trong lúc request đang chạy.

Backend vẫn phải xử lý idempotency hoặc state validation để hai request đồng thời không tạo dữ liệu sai.

Ví dụ:

```text
Request A → APPROVED
Request B → phát hiện đã APPROVED
```

Không crash.

Không tạo duplicate record.

---

# 21. XỬ LÝ CONCURRENCY CHO CA LÀM VIỆC

Tương tự Terminal, nghiệp vụ ca cũng phải phòng trường hợp:

```text
Nhân viên đang mở modal
↓
Manager thay đổi lịch
↓
Nhân viên xác nhận bằng dữ liệu cũ
```

Backend không được tin schedule gửi từ frontend.

Backend phải lấy schedule mới nhất từ database khi mở ca.

Nếu cần có thể áp dụng:

```text
version
updatedAt
optimistic locking
```

theo kiến trúc hiện tại.

Mục tiêu:

```text
Dữ liệu stale trên frontend
≠
dữ liệu được phép ghi xuống backend
```

---

# 22. API ERROR CODE

Hãy chuẩn hóa các lỗi nghiệp vụ để frontend không phải parse message.

Ví dụ:

```text
SHIFT_NOT_OPENED
SHIFT_ALREADY_CLOSED
SHIFT_TOO_EARLY
SHIFT_LATE
SHIFT_OUTSIDE_SCHEDULE
SHIFT_SCHEDULE_CHANGED
SHIFT_OVERRIDE_REQUIRED
SHIFT_OVERRIDE_FORBIDDEN

POS_ACCESS_DENIED

OTP_INVALID
OTP_EXPIRED
OTP_ATTEMPTS_EXCEEDED
OTP_RESEND_COOLDOWN

TERMINAL_NOT_FOUND
TERMINAL_NOT_PENDING
TERMINAL_ALREADY_APPROVED
TERMINAL_APPROVAL_FORBIDDEN
```

Tên thực tế phải theo convention project.

Frontend dựa vào:

```text
error.code
```

để quyết định UI.

Không phụ thuộc hoàn toàn vào text message.

---

# 23. TRÁNH LẶP BUSINESS LOGIC

Không viết riêng cùng một rule ở:

```text
StaffHub Controller
POS Controller
Shift Controller
```

nếu chúng cùng kiểm tra một nghiệp vụ.

Hãy gom logic vào service/domain phù hợp.

Ví dụ:

```text
ShiftAccessService
POSAccessService
AuthorizationService
TerminalService
OtpService
```

Tên class/module phải phù hợp kiến trúc hiện có.

Ví dụ logic:

```text
canAccessPOS(user, store, terminal)
```

có thể kiểm tra:

```text
permission
activeShift
terminal
storeScope
```

và được tái sử dụng ở những nơi cần thiết.

---

# 24. AUDIT LOG

Đối với các action quan trọng, nếu hệ thống đã có audit infrastructure thì hãy sử dụng.

Đặc biệt:

* Override ca trễ.
* Override ca sớm.
* Override ngoài lịch.
* Thay đổi lịch.
* Manager xác nhận ca.
* Xác nhận Terminal.
* Reject Terminal.

Log nên có:

```text
action
actorId
targetId
storeId
oldValue
newValue
reason
timestamp
```

Không log OTP/PIN plaintext.

---

# 25. YÊU CẦU VỀ SECURITY

Tuyệt đối không:

* Tin role từ frontend.
* Tin `storeId` frontend gửi lên mà không kiểm tra scope.
* Tin `shiftStatus` frontend gửi lên.
* Tin `terminalStatus` frontend gửi lên.
* Cho phép mở POS chỉ nhờ route guard frontend.
* Lưu OTP/PIN plaintext nếu architecture hiện tại cho phép hash.
* Log OTP/PIN.
* Bypass authorization do user nhập URL trực tiếp.

Backend phải là nguồn xác thực cuối cùng.

---

# 26. EXPECTED BUSINESS FLOW HOÀN CHỈNH

Flow cuối cùng mong muốn:

```text
USER LOGIN
    ↓
STAFFHUB
    ↓
CHECK PERMISSION
    ↓
CHECK TERMINAL
    ↓
CHECK SCHEDULE
    ↓
Có ca phù hợp?
 ┌───────────────┴───────────────┐
 YES                            NO
 ↓                               ↓
Kiểm tra thời gian          OUTSIDE_SCHEDULE
 ↓                               ↓
EARLY / NORMAL / LATE       Override?
 ↓                               ↓
Cần override?              YES → Verify quyền
 ↓                               ↓
Verify quyền/PIN/OTP       Nhập lý do
 ↓                               ↓
OPEN SHIFT ←─────────────────────┘
 ↓
Backend tạo Active Shift
 ↓
Cho phép POS
 ↓
POS tiếp tục validate Active Shift
 ↓
Làm việc
 ↓
CLOSE SHIFT
 ↓
POS Access bị thu hồi
```

Sau khi:

```text
CLOSE SHIFT
```

người dùng nhập lại URL POS:

```text
→ Backend từ chối
→ Frontend redirect về StaffHub
```

---

# 27. FLOW TERMINAL

```text
Thiết bị gửi đăng ký
↓
Terminal = PENDING_APPROVAL
↓
QLCH/Manager nhận yêu cầu
↓
Kiểm tra permission + store scope
↓
Approve / Reject
↓
Backend update transaction
↓
Frontend nhận response
↓
Refresh Terminal state
↓
Hiển thị kết quả
```

Không còn trường hợp:

```text
loading vô hạn
```

hoặc:

```text
API lỗi nhưng UI không thông báo
```

---

# 28. SAU KHI CODE XONG

Hãy cung cấp cho tôi bản tổng hợp gồm:

### A. Các lỗi tìm thấy

Mỗi lỗi ghi:

```text
File:
Function:
Nguyên nhân:
Ảnh hưởng:
```

### B. Các thay đổi đã thực hiện

```text
File:
Thay đổi:
Lý do:
```

### C. Business flow mới

Mô tả ngắn gọn:

```text
StaffHub → Shift → Override → Open Shift → POS
```

### D. API bị thay đổi

Với mỗi API:

```text
Method
Endpoint
Permission
Request
Response
Error code
```

### E. Các quyết định backend quan trọng

Giải thích:

* Authorization đặt ở đâu.
* Shift validation đặt ở đâu.
* POS access validation đặt ở đâu.
* Terminal approval xử lý ở đâu.
* OTP attempt/cooldown lưu ở đâu.
* Concurrency được xử lý thế nào.

---

# 29. TEST SAU CÙNG

Chỉ bắt đầu test toàn bộ sau khi đã hoàn tất việc rà soát, sửa nghiệp vụ và refactor cần thiết.

Không sửa code theo kiểu chạy test trước rồi chỉ patch từng test case.

## Test POS Access

### Case 1

```text
User chưa mở ca
→ nhập URL POS
→ DENY
```

### Case 2

```text
User đang có active shift
→ POS
→ ALLOW
```

### Case 3

```text
User đã đóng ca
→ nhập URL POS
→ DENY
```

### Case 4

```text
User không có POS permission
→ DENY
```

### Case 5

```text
Terminal chưa approve
→ DENY
```

---

## Test Schedule Changed

```text
User mở modal
↓
Manager thay đổi schedule
↓
User xác nhận
```

Expected:

```text
Backend phát hiện schedule changed
→ không sử dụng dữ liệu cũ
→ refresh schedule
```

---

## Test Early Shift

```text
Mở ca quá sớm
```

Expected:

```text
Không có override permission
→ DENY

Có override permission + reason hợp lệ
→ ALLOW
```

---

## Test Late Shift

```text
Mở ca trễ
```

Expected:

```text
Không chỉ thông báo "ca đã trễ"
→ phải có flow xử lý ngoại lệ
```

---

## Test Outside Schedule

```text
Không có ca tại thời điểm hiện tại
```

Expected:

```text
Staff thông thường
→ DENY

Người có quyền override
→ xác nhận + reason
→ ALLOW
```

---

## Test Task Switching

```text
Chưa mở ca
→ chuyển POS/tác vụ
→ DENY
```

Sau:

```text
Open Shift success
→ chuyển POS
→ ALLOW
```

---

## Test OTP/PIN

Test:

```text
Nhập từng ký tự
Paste 6 ký tự
Paste > 6 ký tự
Lowercase
Uppercase
Chữ có dấu
Special character
Backspace
Empty
Invalid length
```

Đảm bảo behavior chính xác theo validation rule.

---

## Test OTP Lock

```text
Nhập sai đến giới hạn
↓
Resend bị khóa
↓
Countdown 120s
↓
Refresh page
```

Expected:

```text
Countdown vẫn chính xác
```

Sau khi hết cooldown:

```text
Resend enabled
```

---

## Test Terminal Approval

### Success

```text
Pending Terminal
→ Manager approve
→ APPROVED
```

### Unauthorized

```text
Staff approve
→ 403
```

### Wrong Store

```text
Manager Store A
→ approve Terminal Store B
→ 403
```

### Duplicate request

```text
Double click Approve
→ không duplicate
→ UI không treo
```

### API error

```text
Backend exception
→ loading dừng
→ error được hiển thị
```

---

# 30. KẾT QUẢ CUỐI CÙNG

Mục tiêu cuối cùng không phải chỉ "fix cho chạy", mà phải đảm bảo:

```text
StaffHub
+
Shift
+
Schedule
+
POS
+
Permission
+
OTP/PIN
+
Terminal
```

hoạt động như một flow nghiệp vụ thống nhất.

Sau khi hoàn thành, hãy báo cáo:

```text
1. Root cause của từng lỗi.
2. File đã sửa.
3. Business logic đã thay đổi.
4. API đã thay đổi.
5. Permission đã bổ sung/chỉnh sửa.
6. Database/schema có thay đổi hay không.
7. Các trường hợp backward compatibility cần lưu ý.
8. Danh sách test đã chạy.
9. Test nào PASS/FAIL.
10. Những rủi ro hoặc technical debt còn lại.
```

Không tự ý thay đổi những nghiệp vụ không liên quan.

Nếu phát hiện implementation hiện tại khác với giả định trong prompt này, hãy ưu tiên **phân tích architecture/source code hiện tại trước**, sau đó giữ nguyên convention của dự án và điều chỉnh giải pháp cho phù hợp thay vì tạo thêm một kiến trúc mới không cần thiết.
