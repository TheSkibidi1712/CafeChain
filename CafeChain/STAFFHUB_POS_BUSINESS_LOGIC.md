# 📑 TÀI LIỆU PHÂN TÍCH KIẾN TRÚC & NGHIỆP VỤ HỆ THỐNG STAFF HUB & POS (CAFECHAIN)
## 🎯 Báo Cáo Kỹ Thuật Hội Đồng Bảo Vệ & Cẩm Nang Vibe Coding

Tài liệu này hệ thống hóa toàn bộ kiến trúc và nghiệp vụ (business logic) của hệ thống **Staff Hub (Cổng nhân viên)** và **POS (Điểm bán hàng tại quầy)** thuộc dự án **CafeChain** theo mô hình chuỗi F&B chuyên nghiệp. Tài liệu làm nổi bật 4 "Vũ khí" kiến trúc độc quyền giúp hệ thống vận hành ổn định với hiệu năng cao chuẩn Enterprise.

---

## 🚀 4 "VŨ KHÍ" KIẾN TRÚC ĐỘC QUYỀN (ARCHITECTURAL WEAPONS)

### 1. 🏗️ Kiến Trúc Decoupled (Tách Rời) & Cơ Chế Xác Thực One-Time Ticket (OTT)
Để đảm bảo tính độc lập và tối ưu hiệu năng tải trang, hệ thống được thiết kế theo mô hình Decoupled hoàn toàn giữa Web App chính (MVC Backend) và POS Terminal (React Frontend).

- **Phân tách Port hoạt động:**
  - **Backend C# ASP.NET Core MVC:** Chạy trên cổng `7231` (HTTPS) đóng vai trò là API Gateway, quản lý nghiệp vụ tập trung, cơ sở dữ liệu và xác thực sinh trắc học Face ID.
  - **Frontend React (Vite + TypeScript + Tailwind CSS):** Chạy trên cổng `5173` đóng vai trò là Client POS chuyên biệt chạy trực tiếp trên iPad của thu ngân tại quầy.
- **Cơ chế xác thực One-Time Ticket (OTT) để Single Sign-On (SSO):**
  - **Bước 1 (MVC -> React):** Sau khi nhân viên chấm công Face ID thành công tại cổng Staff Hub (`http://localhost:7231`), hệ thống sinh ra một mã Ticket sử dụng một lần (One-Time Ticket - OTT) được ký mã hóa đối xứng và có thời hạn sống siêu ngắn (khoảng 30 giây). Mã OTT này liên kết trực tiếp với thông tin nhân viên (`StaffId`, `StoreId`, `Role`).
  - **Bước 2 (Chuyển tiếp):** Staff Hub tự động chuyển hướng (Redirect) nhân viên sang trang POS React kèm theo Ticket trên URL: `http://localhost:5173/order?ticket=OTT_TOKEN_HERE`.
  - **Bước 3 (Thẩm định & Trao quyền):** React App khởi tạo, đọc mã `ticket` từ URL và gọi API xác thực `POST /api/auth/validate-ott` của Backend. Backend xác thực chữ ký của Ticket, trích xuất thông tin phiên làm việc, xóa Ticket khỏi bộ nhớ đệm (đảm bảo tính One-Time) rồi sinh trả về một mã Access Token (JWT) cho React POS sử dụng lâu dài.

```mermaid
sequenceDiagram
    participant MVC as Staff Hub (C# - 7231)
    participant Browser as Browser Client
    participant React as React POS (5173)
    participant API as API Server (C# - 7231)

    Browser->>MVC: Chấm công Face ID thành công
    MVC->>MVC: Sinh OTT (One-Time Ticket) ký mã hóa (Hạn dùng 30s)
    MVC-->>Browser: Redirect sang http://localhost:5173/order?ticket=OTT_XXX
    Browser->>React: Tải React App với query parameter ticket=OTT_XXX
    React->>API: POST /api/auth/validate-ott { ticket: "OTT_XXX" }
    API->>API: Kiểm tra chữ ký & Hạn dùng của OTT
    API->>API: Hủy OTT ngay lập tức (Chống replay attack)
    API-->>React: Trả về JWT Access Token + Store Context
    React->>React: Lưu JWT vào bộ nhớ trong & mở khóa giao diện POS
```

---

### 2. 📱 Cơ Chế Offline-First Chuẩn Enterprise (Zero-Latency UI)
Đối với F&B, việc mất mạng không bao giờ được phép gián đoạn hoạt động thu ngân. CafeChain áp dụng mô hình Offline-First giúp bán hàng mượt mà với độ trễ bằng 0.

- **Sức mạnh của IndexedDB & Thư viện Dexie.js:**
  - Thay vì dùng `localStorage` (giới hạn dung lượng 5MB và gây nghẽn luồng chính do xử lý đồng bộ), React POS sử dụng **IndexedDB** qua wrapper **Dexie.js** để lưu trữ cơ sở dữ liệu local (Menu nước, Toppings, Hội viên, Cấu hình).
  - Giao diện bán hàng đọc trực tiếp từ IndexedDB mang lại trải nghiệm **Zero-Latency UI** (phản hồi tức thì dưới 1ms cho mọi tương tác chạm).
- **Hàng đợi Đồng bộ `cartSyncQueue`:**
  - Khi thu ngân bấm "Thanh toán", nếu kiểm tra thấy mất kết nối mạng (`navigator.onLine == false` hoặc API timeout), đơn hàng không bị hủy mà được đóng gói và ghi nhận ngay vào bảng `cartSyncQueue` trong IndexedDB với trạng thái `Pending`.
  - Hộc tiền ngăn kéo cơ học vẫn mở, hóa đơn giấy lưu tạm vẫn được hiển thị, thu ngân chốt đơn thành công và phục vụ khách offline bình thường.
  - Khi có mạng trở lại, dịch vụ `OfflineSyncService.ts` tự động phát hiện sự kiện `online` và tiến hành đồng bộ tuần tự toàn bộ hàng đợi lên Server.

```mermaid
flowchart TD
    A[Thu ngân bấm THANH TOÁN] --> B{Kiểm tra mạng?}
    B -->|Có kết nối| C[Gửi trực tiếp lên Cloud Backend]
    B -->|Mất kết nối| D[Ghi đơn hàng vào IndexedDB bảng 'cartSyncQueue']
    D --> E[Mở két tiền + Hiển thị màn hình thành công - Offline Mode]
    E --> F[Chờ sự kiện window.online hoặc polling tự động]
    F --> G[OfflineSyncService.ts kích hoạt đồng bộ]
    G --> H[Đẩy đơn hàng trong queue lên API Backend]
    H --> I[Cập nhật trạng thái syncStatus thành 'Completed']
```

---

### 3. 🛡️ Luồng Chống Trùng Lặp Đơn Hàng (Idempotency - ADR-0002) & Trừ Kho Bán Thành Phẩm Đệ Quy (BOM)
Khi mạng chập chờn, việc gửi lại đơn hàng (retry) là cực kỳ phổ biến. Hệ thống đảm bảo tính toàn vẹn tài chính và kho quỹ qua cơ chế sau:

- **Idempotency qua `clientOrderId` (UUID v4):**
  - Ngay thời điểm Thu ngân nhấn nút "Thanh toán" tại POS, React Client sinh ra một mã định danh duy nhất UUID v4 dưới dạng `clientOrderId` và gắn chết vào đơn hàng trước khi lưu vào IndexedDB.
  - Khi thực hiện đồng bộ (Sync), dù request có bị gửi lặp lại nhiều lần do lỗi Timeout, API `SyncOfflineOrders` của Backend vẫn kiểm tra sự tồn tại của `clientOrderId` trong DB trước.
  - Nếu đã tồn tại, Backend sẽ bỏ qua việc tạo đơn mới và trả về thông tin đơn cũ, loại bỏ hoàn toàn rủi ro trùng đơn hoặc thu thừa tiền.
- **Trừ kho nguyên vật liệu đệ quy (BOM - Bill of Materials):**
  - Khi đơn hàng được đồng bộ thành công, lớp `InventoryDeductionService.cs` thực hiện bóc tách công thức đồ uống đệ quy (tối đa `MAX_BOM_DEPTH = 5` kèm cơ chế chặn vòng lặp `visited` set).
  - Tách rời từ sản phẩm bán lẻ (VD: Ly Cafe Sữa) ra các bán thành phẩm (BTP như Sữa pha) và nguyên liệu thô (như Hạt cafe, Sữa đặc) để trừ kho thực tế.
  - **Chấp nhận kho âm (Negative Inventory - ADR-0001):** Nếu tồn kho nguyên vật liệu thô bị âm, hệ thống vẫn cho phép trừ âm và ghi nhận giao dịch (`SALES_DEDUCTION`) để đảm bảo số liệu doanh thu thực tế được ghi nhận trước, việc đối soát và bù kho sẽ được thực hiện cuối ca.

```csharp
// Minh họa cơ chế kiểm tra trùng lặp (Idempotency) trong AdminPOSController.cs
if (orderDto.ClientOrderId.HasValue)
{
    var existingOrder = await _repository.FindOrderByClientOrderIdAsync(orderDto.ClientOrderId.Value);
    if (existingOrder != null)
    {
        skippedCount++;
        // Bỏ qua không insert để tránh trùng đơn
        continue;
    }
}
```

---

### 4. 🖨️ Hệ Thống In Ấn Kháng Lỗi (Fault-Tolerant Printing)
Để in phiếu chế biến bar/bếp và hóa đơn tại quầy từ một Cloud Backend, CafeChain xây dựng giải pháp Print Bridge trung gian.

- **Kiến trúc Print Bridge (SignalR + Worker.cs):**
  - Một ứng dụng .NET Worker Service (`CafeChain.PrintBridge`) chạy ngầm trên máy tính tại quán, thiết lập kết nối SignalR Hub (`PrintBridgeHub`) liên tục tới Cloud Backend.
  - Khi đơn hàng hoàn tất, Cloud Backend gửi sự kiện `PrintJob` chứa Esc/POS bytes mã hóa Base64 tới kênh SignalR của quán đó. Worker nhận sự kiện và bắn lệnh in qua giao thức TCP (port 9100) tới máy in LAN.
- **Thiết kế Kháng Lỗi (Fault-Tolerance):**
  - Vòng lặp gửi Heartbeat độc lập trong `Worker.cs` chạy mỗi 30 giây, tách biệt hoàn toàn khỏi `System.Threading.Timer` để tránh lỗi sập ứng dụng do các tác vụ `async void`.
  - Toàn bộ luồng xử lý JSON và chuyển tiếp TCP của máy in được bọc nghiêm ngặt trong khối `try-catch` lớn. Lỗi mất điện máy in, kẹt giấy hay ngắt mạng LAN của máy in chỉ được ghi log cảnh báo để hệ thống tự động retry mà không bao giờ làm sập ứng dụng Worker.
  - Phía iPad POS tích hợp một custom hook `usePrinterStatus.ts` và bảng giả lập máy in `PrinterStatusSimulator.tsx` (trong chế độ Dev) để người dùng theo dõi trạng thái máy in (Sẵn sàng - Lỗi máy in - Mất kết nối) trực quan trên thanh TopNavbar.

```mermaid
graph LR
    Cloud[Cloud Backend C#] -- SignalR Hub --► Bridge[Print Bridge Worker]
    Bridge -- TCP Port 9100 --► Printer[Máy In Hóa Đơn LAN]
    Cloud -- SignalR Hub --► iPad[iPad POS UI]
    
    subgraph Fault-Tolerance
        BridgeStyle[Bọc try-catch toàn bộ + Auto Reconnect]
    end
```

---

## 5. 🏗️ KIẾN TRÚC HAI LOẠI CA (DUAL-SHIFT ARCHITECTURE)

Trong hệ thống quản lý chuỗi F&B chuyên nghiệp, khái niệm **"Ca làm việc"** được chia làm 2 tầng nghiệp vụ tách biệt hoàn toàn nhưng liên kết chặt chẽ với nhau:

```mermaid
graph TD
    A[Staff Hub: Đăng nhập nhân viên] --> B[Chấm công sinh trắc học Face ID]
    B -->|Check-in thành công| C[Khởi tạo StaffShift - Ca chấm công/tính lương]
    C -->|Mở khóa quyền truy cập| D[POS Gateway]
    D -->|Cashier/Supervisor| E[Mở ca két tiền WorkShift - POS Session]
    E -->|Nhập tiền lẻ lẻ đầu ca| F[Thao tác Bán hàng & In hóa đơn]
    F -->|Mỗi Order gắn WorkShiftId| G[Chốt ca két tiền WorkShift]
    G -->|Đếm tiền thực tế & Đối soát lệch| H[Chấm công ra Face ID]
    H -->|Check-out thành công| I[Background Job tính PayrollHours làm tròn 15m]
```

### 📊 Bảng so sánh chi tiết giữa StaffShift và WorkShift
| Tiêu chí | 📅 Ca Chấm Công / Nhân Sự (`StaffShift`) | 💰 Ca Thu Ngân / Két Tiền (`WorkShift`) |
| :--- | :--- | :--- |
| **Đối tượng** | Áp dụng cho **tất cả** nhân viên cửa hàng (Pha chế, Thu ngân, Phục vụ, Thủ kho...). | Chỉ áp dụng cho **Thu ngân (Cashier)** và **Ca trưởng (Shift Supervisor)** giữ két tiền. |
| **Mục đích** | Ghi nhận công làm việc thực tế, tính lương (`PayrollHours`), chấm công sinh trắc học. | Quản lý dòng tiền mặt tại quầy, đối soát tiền lẻ đầu ca, tiền mặt thu được và tiền chốt ca. |
| **Mã nguồn** | `Models/Staffs/StaffShift.cs` | `Models/Stores/WorkShift.cs` |
| **Ràng buộc** | Cần lịch làm việc được lên trước (`Shift`) hoặc tạo ca tự do (`IsAdHoc = true`). | Bắt buộc phải có **`StaffShift` đang mở** (đã check-in thành công) thì mới được truy cập POS để mở két. |
| **Navigation** | Gắn với `StaffId`, `ShiftId` (giờ làm). | Gắn với `StoreId`, `UserId` (StaffId) và danh sách các đơn hàng `ICollection<Order>`. |

---

## 6. 📱 CHI TIẾT NGHIỆP VỤ STAFF HUB (EMPLOYEE PORTAL)

Staff Hub (`Views/StaffHub/Index.cshtml`) là cổng thông tin cá nhân của nhân viên hoạt động tại cửa hàng, đóng vai trò như một **Kiosk di động** hoặc máy trạm tại chỗ.

### A. Quy trình Đăng nhập & Bảo mật ban đầu
1. **Chuyển hướng theo Role (RedirectByRole):**
   - Lấy thông tin tài khoản qua Claims (`ClaimTypes.Role`).
   - **Nhóm Admin/Manager** (`SuperAdmin`, `CEO`, `CFO`, `StoreManager`...): Chuyển hướng tới Portal Quản trị (`/Admin/AdminStaff/Index`).
   - **Nhóm Cửa hàng (StaffHub Roles)** (`ShiftSupervisor`, `Cashier`, `WarehouseKeeper`, `GeneralStaff`): Chuyển hướng tới `/StaffHub/Index`.
   - **Nhóm Khách hàng**: Chuyển hướng tới storefront (`/Home/Index`).
2. **Tiêm StoreId Claim (Claims Injection):** Khi nhân viên cửa hàng đăng nhập thành công, hệ thống bắt buộc phải truy vấn thực tế `Staff.StoreId` và inject vào cookie claims để phục vụ cho các logic POS và chấm công về sau.
3. **Mật khẩu nhân viên:**
   - StaffHub không còn kiểm tra hoặc khóa giao diện theo cờ `RequiresPasswordChange`.
   - Nhân viên vào StaffHub ngay sau khi xác thực và có thể chủ động đổi mật khẩu qua chức năng hồ sơ thông thường.

---

### B. 👥 Phân Quyền Chi Tiết Cho 4 Nhóm Vai Trò Trên Staff Hub
Staff Hub là một Employee Portal dùng chung, nhưng giao diện sẽ tự động thích ứng để hiển thị các Module và giới hạn quyền hạn dựa theo từng chức vụ (Role):

```mermaid
graph TD
    User([Nhân Viên Đăng Nhập]) --> R1[Nhân viên chung]
    User --> R2[Thủ kho]
    User --> R3[Thu ngân]
    User --> R4[Ca trưởng]

    R1 -->|Chỉ xem| F1[Chấm công Face ID + Xem Lịch ca]
    R1 -->|POS| G1[Bị Khóa/Ẩn nút POS]
    
    R2 -->|Báo cáo kho| F2[Inventory Gateway: Nhập/Xuất/Kiểm kho]
    R2 -->|POS| G2[Bị Khóa/Ẩn nút POS]

    R3 -->|Vào ca xong| F3[Mở khóa POS Gateway]
    R3 -->|Bán hàng| G3[Mở ca két tiền WorkShift]

    R4 -->|Toàn quyền StaffHub| F4[Tạo PIN Trưởng ca + Duyệt ca tạm thời]
    R4 -->|POS Restricted| G4[Mở ca két tiền + Ủy quyền hạn mức nâng cao]
```

#### 1. Nhân viên chung (`GeneralStaff` - Pha chế/Barista, Phục vụ, Runner, Tiếp tân):
* **Nhiệm vụ:** Trực tiếp phục vụ khách hàng, pha chế đồ uống, dọn dẹp và chuẩn bị cửa hàng.
* **Giao diện & Tính năng khả dụng trên Staff Hub:**
  * **Chấm công Face ID:** Thực hiện check-in và check-out đầu và cuối ngày.
  * **Xem lịch làm việc:** Đọc lịch phân ca cá nhân theo tuần/tháng, xem chi tiết giờ bắt đầu, kết thúc và vị trí làm việc.
  * **Yêu cầu ca tạm thời (Ad-hoc Shift):** Khi làm tăng ca đột xuất hoặc đổi ca cho đồng nghiệp, nhân viên chung bấm nút gửi yêu cầu ca ad-hoc và đợi Ca trưởng phê duyệt trên hệ thống.
  * **Khóa cổng POS:** Nút *"Đi tới máy POS"* bị ẩn hoàn toàn và thay thế bằng dòng chữ: *"Chức vụ của bạn không có quyền truy cập POS"*. Không có quyền chạm vào hệ thống bán hàng quầy.

#### 2. Thủ kho (`WarehouseKeeper`):
* **Nhiệm vụ:** Quản lý kho nguyên vật liệu (hạt cafe, sữa, syrup, ly cốc...), thực hiện nhập xuất kho và kiểm kê định kỳ.
* **Giao diện & Tính năng khả dụng trên Staff Hub:**
  * **Chấm công Face ID & Xem lịch ca:** Tương tự nhân viên chung.
  * **Cổng Inventory Gateway (Thủ kho Portal):**
    * Giao diện nhập phiếu nhận nguyên liệu (`InventoryDocument` loại *Receipt*).
    * Giao diện tạo phiếu xuất kho cho quầy bar (`InventoryDocument` loại *Issue*).
    * Màn hình kiểm kho định kỳ (`StockTakeSession`) để nhập số lượng nguyên liệu tồn thực tế đối chiếu với phần mềm.
  * **Khóa cổng POS:** Bị ẩn hoàn toàn nút truy cập POS.

#### 3. Thu ngân (`Cashier`):
* **Nhiệm vụ:** Đứng quầy order, thu tiền, in hóa đơn và bàn giao ca két tiền.
* **Giao diện & Tính năng khả dụng trên Staff Hub:**
  * **Chấm công Face ID & Xem lịch ca:** Thực hiện check-in để mở đầu ngày làm việc.
  * **POS Gateway:** Nút *"Đi tới máy POS"* hiển thị rõ ràng. Nút này sẽ **mở khóa (Active)** ngay sau khi trạng thái chấm công báo *Checked-in*. 
  * **Thao tác POS:** Mở ca két tiền `WorkShift`, bán hàng và chốt ca bàn giao két tiền.

#### 4. Ca trưởng (`ShiftSupervisor`):
* **Nhiệm vụ:** Điều phối toàn bộ nhân viên trong ca làm việc, xử lý chênh lệch tiền nong, phê duyệt các yêu cầu đặc biệt và trực tiếp đứng quầy POS nếu cần.
* **Giao diện & Tính năng khả dụng trên Staff Hub:**
  * **Toàn quyền cổng POS:** Nút *"Đi tới máy POS"* luôn sáng (khi đã check-in).
  * **OTP phê duyệt (không còn PIN cố định):** Thao tác nhạy cảm dùng OTP one-time 6 ký tự gửi email Ca trưởng (#139–#143). Không còn UI/API đặt PIN / `Staff.PinHash`.
  * **Phê duyệt ca tự do (Ad-hoc approval):** Màn hình hiển thị danh sách yêu cầu ca tạm thời từ Pha chế/Phục vụ để Ca trưởng bấm phê duyệt trực tiếp.

---

### C. Nghiệp vụ Chấm Công Sinh Trắc Học Face ID (`SubmitTimeAction`)
Hệ thống sử dụng thư viện `face-api.js` phía Client để quét khuôn mặt và backend C# thực hiện kiểm tra so khớp vector.

```mermaid
sequenceDiagram
    actor Staff as Nhân viên
    participant UI as StaffHub UI (Client)
    participant Server as Attendance API (Server)
    
    Staff->>UI: Nhấn "Vào ca" (CHECK_IN)
    UI->>UI: Bật Camera, Quét 3 góc độ (Thẳng, Trái, Phải)
    UI->>UI: Tính Vector 128 chiều trung bình từ 3 góc quét
    UI->>Server: POST /api/Attendance/SubmitTimeAction { actionType, faceDescriptorJson }
    Server->>Server: Tính khoảng cách Euclidean/Cosine giữa vector gửi lên và FaceDescriptor trong DB
    alt Khoảng cách > 0.4
        Server-->>UI: Trả về lỗi 400 "Xác thực thất bại!"
    else Khoảng cách <= 0.4
        Server->>Server: Tìm ca làm việc hôm nay trong khoảng +-2 tiếng
        alt Không tìm thấy ca đăng ký lịch trước
            Server-->>UI: Trả về mã lỗi "AD_HOC_CONFIRMATION_REQUIRED"
            UI->>Staff: Hiển thị popup "Bạn không có ca đăng ký. Vào ca tự do (Ad-hoc)?"
            Staff->>UI: Đồng ý
            UI->>Server: Gọi lại API với tham số forceSave = true
            Server->>Server: Tạo mới StaffShift (IsAdHoc = true)
        end
        Server->>Server: Cập nhật ActualCheckIn = Giờ hiện tại, Status = CheckedIn
        Server-->>UI: Trả về 200 Thành công!
    end
```

#### 🛡️ Các Biên Nghiệp Vụ Cần Xử Lý (Edge Cases):
1. **Vá lỗ hổng bảo mật IDOR (Anti-IDOR Guard):** 
   - 🚫 **Cấm:** Không nhận `accountId` hay `staffId` từ body hoặc query string do Client gửi lên.
   - ✅ **Chuẩn:** Lấy trực tiếp `AccountId` từ `User.FindFirst(ClaimTypes.NameIdentifier).Value` trên Server.
2. **Chặn chấm công trùng lặp (Duplicate Check-In Guard):**
   - Trước khi xử lý `actionType == "CHECK_IN"`, hệ thống kiểm tra xem nhân viên đã có ca nào trong ngày hôm nay có `ActualCheckIn != null && ActualCheckOut == null` hay chưa.
   - Nếu đã có, trả về `409 Conflict` kèm thông báo: *"Bạn đang trong một phiên làm việc khác. Vui lòng tan ca trước khi vào lại."*
3. **Xử lý ca làm việc qua đêm (Overnight Shifts):**
   - Khi quét ca làm việc hiện tại của nhân viên để ghép check-in/check-out, hệ thống phải quét cả ngày hôm trước (`yesterday = today.AddDays(-1)`) và kiểm tra xem ca đó có thuộc tính `IsOvernight == true` hay không.
4. **Tải tài nguyên AI bị lỗi (Face-API Timeout):**
   - Client tải các file models AI nặng ~5MB. Nếu mạng chậm hoặc timeout sau 30 giây, hiển thị SweetAlert2 thông báo lỗi mạng kèm nút *"Thử lại"* thay vì đơ giao diện.

---

### D. Background Worker Tính Lương & Giờ Công (`PayrollCalculationWorker`)
Khi nhân viên chấm công ra (Check-out) thành công, hệ thống ghi nhận `ActualCheckOut`. Sau đó, một Background Worker (hoặc logic Service) sẽ tự động chạy để tính toán giờ công thực tế:
1. **Công thức tính giờ thô (Raw Hours):** `rawHours = (ActualCheckOut - ActualCheckIn).TotalHours`.
2. **Quy tắc làm tròn 15 phút (Nearest 15-minute Rounding):** 
   - Để thuận tiện cho việc tính lương chuỗi, tổng giờ công (`PayrollHours`) được làm tròn về mốc **0.25h** gần nhất bằng thuật toán:
     $$\text{roundedHours} = \frac{\text{Round}(rawHours \times 4)}{4}$$
   - *Ví dụ:* 
     - Làm 3 giờ 5 phút ($3.08h$) $\rightarrow$ Làm tròn thành **3.00h**.
     - Làm 3 giờ 12 phút ($3.20h$) $\rightarrow$ Làm tròn thành **3.25h**.
     - Làm 3 giờ 25 phút ($3.41h$) $\rightarrow$ Làm tròn thành **3.50h**.
3. **Cập nhật trạng thái:** Chuyển `StatusId` của `StaffShift` sang `Completed`.

---

## 7. 🛒 CHI TIẾT NGHIỆP VỤ POS GATEWAY & KÉT TIỀN (POINT OF SALE)

POS là giao diện thực hiện giao dịch trực tiếp với khách hàng tại cửa hàng. Chỉ các tài khoản có Role là `Cashier` (Thu ngân) hoặc `ShiftSupervisor` (Ca trưởng) mới có quyền thao tác.

### A. POS Entrance Guard (Chốt Chặn Cổng POS)
- Khi truy cập `/Admin/AdminPOS/Index` (hoặc POS React app), hệ thống kiểm tra xem nhân viên đăng nhập có ca hoạt động (`StaffShift`) hay không:
  - Điều kiện: có `StaffShift` tồn tại trong ngày hôm nay hoặc hôm qua (ca qua đêm), đã có `ActualCheckIn` và chưa có `ActualCheckOut`.
  - Nếu **Không có ca hoạt động**: Chặn truy cập ngay lập tức, chuyển hướng về Staff Hub kèm thông báo: *"Bạn chưa thực hiện Chấm Công Vào Ca thành công."*
  - Nếu **Hợp lệ**: Cho phép vào màn hình POS và lưu `StaffShiftId` vào bộ nhớ Client/Session.

---

### B. Nghiệp vụ Mở Ca Két Tiền POS (`WorkShift` - Cash Session)
Khi bắt đầu phiên bán hàng tại quầy POS, thu ngân không được bán hàng ngay mà phải thực hiện **Mở ca két tiền (Open Cash Session):**

```mermaid
graph LR
    A[Vào POS] --> B{Kiểm tra WorkShift đang mở?}
    B -->|Chưa có ca| C[Hiển thị Form Mở Ca POS]
    C --> D[Thu ngân nhập số tiền lẻ đầu ca StartingCash]
    D --> E[Lưu DB WorkShift: Status='Open']
    B -->|Đã có ca đang mở| F[Cho phép vào màn hình bán hàng]
```

1. **Starting Cash:** Tiền lẻ đầu ca do cửa hàng trưởng giao cho thu ngân (ví dụ: 500,000đ hoặc 1,000,000đ để thối tiền).
2. **Gắn kết Hóa đơn (Order Session Binding):** 
   - Tất cả các đơn hàng (`Order`) phát sinh tại quầy trong suốt phiên làm việc bắt buộc phải lưu trường `WorkShiftId` và `StaffId` (người thực hiện).
   - Logic này giúp báo cáo doanh thu két tiền và đối soát tài chính cuối ca hoàn chỉnh.

---

### 💵 Vòng Đời Tiền Lẻ Đầu Ca & Chu Kỳ Đối Soát Tiền Mặt (Day 1 vs Day 2)
Trong F&B, việc quản lý két tiền quầy POS được chuẩn hóa theo mô hình **Standard Float (Định mức tiền lẻ cố định)** để kiểm soát dòng tiền mặt chặt chẽ:

```mermaid
sequenceDiagram
    participant C1 as Thu Ngân Ca 1
    participant Safe as Két An Toàn Cửa Hàng (Manager)
    participant C2 as Thu Ngân Ca 2 (Ngày tiếp theo)
    
    Note over C1: ĐẦU CA 1: Đếm két thấy 1.000.000đ tiền lẻ
    C1->>C1: Nhập StartingCash = 1.000.000đ
    Note over C1: Bán hàng thu về +4.500.000đ tiền mặt ròng
    Note over C1: CUỐI CA 1 (CHỐT CA): Expected Cash = 5.500.000đ
    C1->>C1: Đếm két thực tế thấy 5.500.000đ (Actual Cash)
    C1->>Safe: Trích 4.500.000đ nộp Doanh thu (Drop Money)
    C1->>C1: Để lại đúng 1.000.000đ trong két sắt POS (Float)
    
    Note over C2: ĐẦU CA 2 (NGÀY 2): Đếm két lẻ
    alt Két lẻ khớp định mức
        C2->>C2: Thấy đúng 1.000.000đ -> Nhập StartingCash = 1.000.000đ
    else Két lẻ bị lệch từ ca trước (Ví dụ: Thừa 50.000đ)
        C2->>C2: Thấy 1.050.000đ -> Bắt buộc nhập StartingCash = 1.050.000đ
        Note over C2: Hệ thống tự động ghi nhận chênh lệch của ca 1 và khởi tạo ca 2 từ mốc 1.050.000đ
    end
```

#### 📌 Kịch bản chi tiết qua các ngày:

* **NGÀY 1 (Khởi động hệ thống):**
  * **Mở ca:** Thu ngân mở két, đếm đúng số tiền lẻ của cửa hàng bàn giao đầu ngày (định mức tiêu chuẩn là **1.000.000đ** gồm các tờ tiền lẻ 10k, 20k, 50k...). Thu ngân nhập `StartingCash = 1.000.000đ`.
  * **Bán hàng:** Trong ngày phát sinh doanh thu bán hàng thu tiền mặt ròng là **4.500.000đ** (đã trừ đi tiền thối cho khách).
  * **Chốt ca:**
    * Hệ thống tính doanh thu lý thuyết trong két: 
      $$\text{Expected Ending Cash} = \text{StartingCash (1.000.000đ)} + \text{Doanh thu ròng (4.500.000đ)} = 5.500.000đ$$
    * Thu ngân đếm két thực tế thấy đúng **5.500.000đ** $\rightarrow$ Nhập `ActualEndingCash = 5.500.000đ` (Lệch két = 0).
    * **Rút tiền mặt bàn giao (Safe Drop):** Thu ngân trích lấy đúng **4.500.000đ** tiền doanh thu ròng bỏ vào phong bì nộp cho Cửa hàng trưởng hoặc két an toàn của quán.
    * **Để lại tiền lẻ:** Thu ngân **chừa lại đúng 1.000.000đ tiền mặt lẻ** trong két bán hàng POS và khóa lại để bàn giao cho ngày hôm sau.

* **NGÀY 2 (Ngày làm việc tiếp theo):**
  * **Kịch bản A (Mọi thứ hoàn hảo):**
    * Thu ngân ngày 2 mở két, đếm lại két lẻ thấy có đúng **1.000.000đ** $\rightarrow$ Nhập `StartingCash = 1.000.000đ` để tiếp tục chu kỳ.
  * **Kịch bản B (Xảy ra lỗi đếm tiền của Ca trước):**
    * Thu ngân ca trước nộp doanh thu sai, để lại két lẻ bị thừa **1.050.000đ** (hoặc thiếu, chỉ còn **950.000đ**).
    * Thu ngân ngày 2 đếm két lẻ thực tế thấy bao nhiêu **bắt buộc phải nhập đúng con số thực tế đếm được** làm `StartingCash` (ví dụ đếm thấy 950k thì nhập `StartingCash = 950.000đ`). 
    * *Ý nghĩa:* Hệ thống ghi nhận điểm xuất phát mới của két ngày thứ 2 là 950k để không làm ảnh hưởng đến doanh số ngày 2, đồng thời báo cáo chốt ca ngày 1 của nhân viên trước đó sẽ tự động bị đánh dấu cảnh báo **Lệch két (thiếu 50.000đ)** để Quản lý tiến hành kiểm toán phạt/thu hồi.

---

### C. Cơ Chế Ủy Quyền Trưởng Ca (Shift Leader Privilege Elevation)
Trong vận hành quán cafe, thu ngân có quyền hạn giới hạn. Khi gặp các trường hợp nhạy cảm hoặc có nguy cơ thất thoát dòng tiền, hệ thống POS sẽ tự động khóa và yêu cầu **Trưởng ca hoặc Cửa hàng trưởng** duyệt trực tiếp tại chỗ.

```mermaid
sequenceDiagram
    actor Cashier as Thu ngân
    actor Supervisor as Trưởng ca (Supervisor)
    participant POS as Màn hình POS (Client)
    participant API as POS Authorize API (Server)
    
    Cashier->>POS: Nhấn "Hủy món" / "Giảm giá sâu > 20%" / "Đổi giá gốc"
    POS->>POS: Khóa màn hình bán hàng, hiển thị Popup Xác Thực
    Supervisor->>POS: Nhập OTP phê duyệt 6 ký tự (one-time challenge)
    POS->>API: OTP request/verify/consume (bound payload + OtpChallengePublicId)
    API->>API: Verify challenge status/fingerprint/approver eligibility (no Staff.PinHash)
    alt Xác thực sai
        API-->>POS: Trả về lỗi OTP (hết hạn / sai mã / attempts)
    else Xác thực đúng
        API->>API: Consume challenge + audit historical evidence (không dùng audit làm auth)
        API-->>POS: Trả về thành công
        POS->>POS: Mở khóa màn hình, thực hiện thao tác nhạy cảm
    end
```

#### 🔒 Các Thao Tác Bắt Buộc Yêu Cầu Duyệt:
1. **Hủy hóa đơn đã thanh toán / Hủy món ăn trong hóa đơn đang tạm tính.**
2. **Áp dụng mức giảm giá thủ công (Manual Discount) > 15% hoặc vượt quá giới hạn voucher.**
3. **Thay đổi đơn giá gốc của đồ uống tại quầy.**

---

### D. Nghiệp vụ Chốt Ca Két Tiền POS (Close WorkShift)
Cuối ca làm việc, thu ngân thực hiện **Chốt ca két tiền** để bàn giao cho ca tiếp theo:
1. **Expected Ending Cash (Tiền mặt lý thuyết):**
   - Hệ thống tự động tính toán: 
     $$\text{Expected Cash} = \text{Starting Cash} + \text{Tổng tiền mặt thu được từ các đơn hàng trong ca} - \text{Tổng tiền mặt thối ra}$$
2. **Actual Ending Cash (Tiền mặt thực tế):**
   - Thu ngân đếm toàn bộ số tiền mặt có trong hộc kéo và nhập số liệu thủ công vào hệ thống.
3. **Xử lý chênh lệch (Discrepancy Auditing):**
   - Hệ thống so sánh: Lệch = $\text{Actual Ending Cash} - \text{Expected Ending Cash}$.
   - Nếu có chênh lệch (âm hoặc dương), hệ thống yêu cầu thu ngân điền **Lý do chênh lệch** và ghi nhận vào bảng kiểm toán.
   - Trạng thái `WorkShift` chuyển sang `Closed`.
   - **Chốt chặn:** Sau khi đóng `WorkShift`, tài khoản thu ngân không thể tạo thêm bất kỳ `Order` nào tại POS trừ khi mở ca két tiền mới.

---

## 8. 📝 LƯU Ý VIẾT TEXT PROMPT & HƯỚNG DẪN CÁC MODEL AI (LLM CHEAT SHEET)

Khi làm việc với các model AI trong chế độ **Vibe Coding** (viết code nhanh bằng ngôn ngữ tự nhiên), bạn nên sao chép đoạn hướng dẫn chuẩn hóa dưới đây để dán vào prompt. Nó sẽ giúp model hiểu rõ cấu trúc dự án CafeChain hiện tại và không sinh code lỗi.

### 📋 COPY-PASTE PROMPT CHO MODEL AI:
> "Chúng ta đang phát triển dự án CafeChain trên nền tảng ASP.NET Core MVC (N-Tier Architecture) kết hợp React POS Client. Hãy tuân thủ nghiêm ngặt các quy tắc sau khi viết code cho mình:
> 
> 1. **Phân biệt hai loại ca (Dual-Shift):**
>    - `StaffShift` (trong `Models/Staffs`): Ca chấm công nhân sự, dùng cho Face ID check-in/out, tính lương, ghi nhận công.
>    - `WorkShift` (trong `Models/Stores`): Ca két tiền POS, dùng để quản lý tiền mặt hộc kéo (StartingCash, ExpectedEndingCash, ActualEndingCash, Status = 'Open'/'Closed').
> 
> 2. **Chống lỗi IDOR (Bảo mật tối cao):**
>    - Tuyệt đối KHÔNG nhận `accountId` hay `staffId` từ body hoặc query do Client gửi lên trong các API chấm công/POS.
>    - Bắt buộc lấy `AccountId` trực tiếp từ JWT/Cookie claims bằng cách dùng:
>      `int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int accountId)`
>    - Chỉ inject và đọc `StoreId` thông qua Claims.
> 
> 3. **Quy tắc N-Tier Architecture (Kiểu kiến trúc phân tầng):**
>    - Controller phải cực mỏng (Thin Controller). Cấm gọi trực tiếp `DbContext` hay thực hiện logic tính toán trong Controller.
>    - Mọi logic DB, tính toán khoảng cách vector gương mặt, lọc ca làm việc bắt buộc phải viết trong lớp Service thuộc `Application/Services` và gọi qua Interface.
> 
> 4. **Tránh xung đột chấm công (Concurrent Check-In):**
>    - Trước khi xử lý CHECK_IN trong `SubmitTimeActionAsync`, phải dùng `AnyAsync` kiểm tra xem nhân viên đã có ca làm việc hoạt động nào trong ngày chưa (`ActualCheckIn != null && ActualCheckOut == null`). Nếu có, trả về lỗi 409 Conflict.
> 
> 5. **Ủy quyền Trưởng ca (Shift Leader Override):**
>    - OTP one-time được hash (BCrypt) trên từng challenge; không lưu credential PIN cố định trên Staff. Không log plaintext OTP."

---

## 9. 🛠️ CHEAT SHEET BẢN ĐỒ CƠ SỞ MÃ NGUỒN CAFECHAIN

Dành cho các Model AI định vị nhanh các file khi thực hiện thay đổi:

```
 CafeChain (Backend C#)
 ├── 📂 Application
 │    ├── 📂 Constants
 │    │    └── 📄 RoleConstants.cs            <-- Định nghĩa tên các Role chuẩn ("Thu ngân", "Ca trưởng"...)
 │    └── 📂 Interfaces
 │         └── 📂 Attendance
 │              ├── 📄 IAttendanceActionService.cs
 │              └── 📄 IAttendanceSecurityService.cs
 ├── 📂 Controllers
 │    ├── 📄 AttendanceController.cs          <-- API Endpoint cho FaceID, Đổi PIN, Đổi Pass
 │    ├── 📄 StaffHubController.cs            <-- Controller cổng thông tin nhân viên
 │    └── 📄 PosController.cs                 <-- POS API Controller (Validate Voucher)
 ├── 📂 Hubs
 │    └── 📄 PrintBridgeHub.cs                <-- SignalR Hub điều phối lệnh in & báo trạng thái máy in
 ├── 📂 Areas
 │    └── 📂 Admin
 │         └── 📂 Controllers
 │              └── 📄 AdminPOSController.cs  <-- POS View Controller chính (Mở ca, check ca, sync offline)
 ├── 📂 Models
 │    ├── 📂 Staffs
 │    │    ├── 📄 Staff.cs                    <-- Profile nhân viên (FaceDescriptor; no PinHash #143)
 │    │    └── 📄 StaffShift.cs               <-- Ca chấm công thực tế (ActualCheckIn, ActualCheckOut, PayrollHours)
 │    └── 📂 Stores
 │         └── 📄 WorkShift.cs                <-- Phiên két tiền POS (StartingCash, ExpectedCash, Status)
 ├── 📂 Data
 │    └── 📄 AppDbContext.cs                 <-- Lớp DbContext chính quản lý các thực thể
 
 CafeChain.PrintBridge (.NET Worker Service)
 ├── 📄 Program.cs
 ├── 📄 Worker.cs                             <-- Gửi Heartbeat định kỳ mỗi 30s lên Hub (bọc try-catch)
 └── 📂 Services
      ├── 📄 SignalRPrintClient.cs            <-- Đăng ký nhận PrintJob, bọc luồng xử lý ESC/POS an toàn
      └── 📄 TcpPrinterForwarder.cs          <-- Chuyển tiếp byte lệnh in qua cổng TCP 9100 đến máy in thật/giả lập
      
 CafeChain.Frontend (React Client POS)
 ├── 📄 package.json                          <-- Thư viện @microsoft/signalr và sweetalert2
 ├── 📄 .env.development                      <-- Biến VITE_API_BASE_URL trỏ về backend cổng 7231
 └── 📂 src
      ├── 📄 App.tsx                           <-- Chứa RootLayout kết hợp component Simulator
      ├── 📂 hooks
      │    └── 📄 usePrinterStatus.ts         <-- Custom hook kết nối Hub, lắng nghe status & báo lỗi
      └── 📂 components
           ├── 📄 PrinterStatusBadge.tsx      <-- Hiện màu xanh/đỏ nhấp nháy/xám trên TopNavbar
           ├── 📄 TopNavbar.tsx               <-- Thanh công cụ đầu trang POS
           └── 📂 dev
                └── 📄 PrinterStatusSimulator.tsx <-- Widget giả lập trạng thái máy in (chỉ chạy trong DEV)
```

---

## 10. ✨ QUY CHUẨN THIẾT KẾ GIAO DIỆN CAO CẤP (PREMIUM UI DESIGN)

Đối với các dự án F&B hiện đại, giao diện trực quan sinh động giúp nhân viên thao tác cực nhanh và giảm thiểu sai sót:

1. **Phối màu chủ đạo (Color Palette):**
   - Sử dụng tông màu tối sang trọng làm nền: **Dark Navy (`#0f172a` hoặc `#1e293b`)**.
   - Các nút chức năng nổi bật sử dụng màu gradient mượt mà:
     - Nút Chấm Công Vào Ca: `bg-gradient-to-r from-emerald-500 to-teal-600` (Xanh ngọc lục bảo).
     - Nút POS Bán Hàng: `bg-gradient-to-r from-sky-500 to-indigo-600` (Xanh đại dương).
     - Nút Tan Ca / Cảnh báo: `bg-gradient-to-r from-rose-500 to-orange-600` (Đỏ cam).
2. **Typography (Chữ viết):** Bắt buộc sử dụng font **Inter** hoặc **Outfit** từ Google Fonts. Cỡ chữ chuẩn cho quầy POS tối thiểu là **14px - 16px** để dễ đọc dưới ánh sáng mạnh tại quầy bán hàng.
3. **Hiệu ứng Micro-animations (Chuyển động tinh tế):**
   - Thêm hiệu ứng phóng to nhẹ `hover:scale-[1.03] transition-all duration-200` cho các card chức năng.
   - Thêm hiệu ứng loading spinner mượt mà trên camera quét Face ID khi AI đang tính toán vector nhằm đem lại cảm giác ứng dụng "đang sống" và chuyên nghiệp.
4. **Bố cục Responsive:**
   - Grid 3 cột chuyên biệt trên màn hình máy tính bảng iPad nằm ngang (Bên trái: Danh mục nước & Danh sách đồ uống; Giữa: Giỏ hàng & thông tin Hội viên; Phải: Bảng điều khiển phím số & Thông tin thanh toán).
   - Thiết kế thân thiện với màn hình cảm ứng, các nút bấm diện tích tối thiểu 44x44px.
