# POS iPad Pro M5 — Issues Breakdown

> **Note**: `gh` CLI chưa được cài trên máy. Dưới đây là toàn bộ 13 issues với nội dung đầy đủ.
> Sau khi cài `gh` (`winget install GitHub.cli`), chạy block lệnh ở cuối file để publish tất cả lên GitHub.

---

## Issue #1 — Schema: Thêm `ClientOrderId` vào model `Order` + Migration

**Type**: 🧑‍💻 HITL | **Labels**: `backend`, `database`, `ready-for-agent`

### What to build

Thêm trường `ClientOrderId` (Guid?, nullable) lên model `Order`. Tạo EF Core migration với Unique Filtered Index (`WHERE ClientOrderId IS NOT NULL`) để đảm bảo idempotency ở DB level. Trường này nullable vì đơn online không cần — chỉ Offline Order mới có ClientOrderId.

Tuân thủ ADR-0002: ClientOrderId là thuộc tính nghiệp vụ cốt lõi, lưu vĩnh viễn trên Order.

### Acceptance criteria

- [ ] Model `Order` có trường `public Guid? ClientOrderId { get; set; }`
- [ ] Migration tạo Unique Filtered Index trên `ClientOrderId` (`WHERE ClientOrderId IS NOT NULL`)
- [ ] `dotnet ef migrations add` chạy thành công, không conflict
- [ ] `dotnet build` pass 0 errors
- [ ] **HITL**: Dev review file migration trước khi chạy `Update-Database`

### Blocked by

None — can start immediately.

---

## Issue #2 — API: Idempotent `SyncOfflineOrders` endpoint

**Type**: 🤖 AFK | **Labels**: `backend`, `api`, `ready-for-agent`

### What to build

Refactor `SyncOfflineOrders` trong `AdminPOSController` để kiểm tra `ClientOrderId` trước khi commit. Nếu đã tồn tại Order với ClientOrderId trùng → trả về OrderId cũ (HTTP 200, không tạo mới). Nếu mới → commit order + trigger Inventory Deduction (cho phép kho âm theo ADR-0001) → trả về OrderId mới (HTTP 201).

Cập nhật `OfflineOrderSyncDTO` thêm trường `ClientOrderId` (Guid).

### Acceptance criteria

- [ ] `OfflineOrderSyncDTO` có trường `ClientOrderId` (Guid)
- [ ] `SyncOfflineOrders` kiểm tra `ClientOrderId` trước khi gọi `CommitOrderAsync`
- [ ] Gửi batch sync với UUID trùng → không tạo đơn mới, trả về OrderId cũ
- [ ] Gửi batch sync gây kho âm → commit thành công, `AvailableQty < 0` được chấp nhận
- [ ] Integration test: batch 5 đơn, 2 đơn UUID trùng → chỉ 3 đơn mới trong DB

### Blocked by

- Issue #1 (Schema ClientOrderId)

---

## Issue #3 — SignalR: Tạo `PrintBridgeHub` + print status broadcast

**Type**: 🤖 AFK | **Labels**: `backend`, `signalr`, `ready-for-agent`

### What to build

Tạo SignalR Hub mới `PrintBridgeHub`. Print Bridge client join group theo StoreId (`JoinPrintGroup(int storeId)`). Server method `SendPrintJob(int storeId, byte[] escPosPayload)` gửi payload đến group. Heartbeat: Print Bridge gửi ping định kỳ, backend broadcast trạng thái printer (`PrinterStatus`) cho iPad qua group.

Đăng ký Hub trong `Program.cs` tại endpoint `/hubs/print-bridge`.

### Acceptance criteria

- [ ] `PrintBridgeHub` tồn tại với methods: `JoinPrintGroup`, `ReportPrinterStatus`
- [ ] Hub registered tại `/hubs/print-bridge` trong `Program.cs`
- [ ] Server có thể gọi `SendAsync("PrintJob", payload)` đến group `Store_{storeId}`
- [ ] Server có thể broadcast `PrinterStatus` (online/offline) đến group
- [ ] `dotnet build` pass 0 errors

### Blocked by

None — can start immediately.

---

## Issue #4 — Service: ESC/POS Receipt Builder

**Type**: 🧑‍💻 HITL | **Labels**: `backend`, `service`, `ready-for-agent`

### What to build

Tạo service `IEscPosBuilder` / `EscPosBuilder` trong `Application/Services/POS/`. Method `BuildReceipt(Order order, string storeName)` trả về `byte[]` chứa chuỗi lệnh ESC/POS:

1. Initialize printer (`ESC @`)
2. Header quán (center, bold, double size)
3. Dòng kẻ `---`
4. Danh sách món: tên, size, topping, SL, đơn giá, thành tiền
5. Tổng tạm tính, voucher, điểm, tổng thanh toán
6. Tiền khách đưa, tiền thối
7. Footer (cảm ơn, ngày giờ)
8. Lệnh cắt giấy (`GS V 1`)
9. Lệnh mở két tiền RJ11 (`ESC p 0 25 250`) — chỉ khi thanh toán tiền mặt

### Acceptance criteria

- [ ] Interface `IEscPosBuilder` + implementation `EscPosBuilder` tồn tại
- [ ] DI registered trong `Program.cs`
- [ ] `BuildReceipt` trả về `byte[]` chứa đúng ESC/POS commands
- [ ] Byte output chứa `0x1B 0x40` (initialize), `0x1D 0x56 0x01` (cut), `0x1B 0x70 0x00` (kick drawer)
- [ ] Unit test verify byte output cho order mẫu
- [ ] **HITL**: Dev review byte sequences trước khi merge

### Blocked by

None — can start immediately.

---

## Issue #5 — Integration: Trigger Silent Print sau CommitOrder

**Type**: 🤖 AFK | **Labels**: `backend`, `integration`, `ready-for-agent`

### What to build

Sau khi `CommitOrderAsync` hoặc `SyncOfflineOrders` commit thành công, gọi `EscPosBuilder.BuildReceipt()` → `PrintBridgeHub.Clients.Group("Store_{storeId}").SendAsync("PrintJob", payload)`.

Thêm flag `skipPrint` (optional) trên DTO để test có thể skip print trigger. Inject `IHubContext<PrintBridgeHub>` vào `POSOrderService` hoặc tạo `IPrintDispatcher` trung gian.

### Acceptance criteria

- [ ] Mỗi order commit thành công → ESC/POS payload được gửi qua SignalR
- [ ] Offline Order sync → cũng trigger print cho từng đơn sync thành công
- [ ] `skipPrint = true` trên DTO → không trigger print (cho test)
- [ ] Integration test: commit order → verify SignalR group nhận `PrintJob` event

### Blocked by

- Issue #3 (PrintBridgeHub)
- Issue #4 (ESC/POS Builder)

---

## Issue #6 — Print Bridge: .NET Worker Service + TCP forward

**Type**: 🧑‍💻 HITL | **Labels**: `backend`, `worker-service`, `ready-for-agent`

### What to build

Tạo project mới `CafeChain.PrintBridge` (dạng .NET Worker Service / Console App). Kết nối `PrintBridgeHub` trên cloud qua SignalR client (`Microsoft.AspNetCore.SignalR.Client`).

Nhận event `PrintJob` → forward `byte[]` sang TCP `{printerIp}:9100`. Config từ `appsettings.json`: `HubUrl`, `StoreId`, `PrinterIp` (default `localhost`), `PrinterPort` (default `9100`).

Heartbeat: gửi `ReportPrinterStatus("online")` mỗi 30s. Reconnect tự động khi mất kết nối.

### Acceptance criteria

- [ ] Project `CafeChain.PrintBridge` tồn tại, build thành công
- [ ] Kết nối SignalR Hub, join group theo StoreId từ config
- [ ] Nhận `PrintJob` → forward bytes sang TCP target
- [ ] Config printer IP/port từ `appsettings.json`
- [ ] Auto-reconnect khi mất kết nối SignalR
- [ ] Heartbeat `ReportPrinterStatus` mỗi 30s
- [ ] **HITL**: Dev review project structure và config trước khi merge

### Blocked by

- Issue #3 (PrintBridgeHub)

---

## Issue #7 — Frontend: IndexedDB thay thế localStorage cho Offline Order

**Type**: 🤖 AFK | **Labels**: `frontend`, `offline`, `ready-for-agent`

### What to build

Refactor `pos-app.js` Section 11 (Offline Mode): thay thế `localStorage` (`CafeChain_Offline_Orders`) bằng IndexedDB. Mỗi Offline Order sinh `crypto.randomUUID()` lúc nhấn "Thanh toán" và lưu kèm vào record.

Auto-sync: khi `navigator.onLine` chuyển `true`, gửi batch sync qua `SyncOfflineOrders` API với exponential backoff (1s → 2s → 4s → max 30s). Hiển thị toast kết quả sync. Xóa record khỏi IndexedDB sau khi sync thành công.

### Acceptance criteria

- [ ] `localStorage` không còn dùng cho offline orders
- [ ] IndexedDB store `offlineOrders` tồn tại với schema `{ clientOrderId, orderData, createdAt, syncStatus }`
- [ ] `crypto.randomUUID()` sinh lúc nhấn "Thanh toán"
- [ ] Auto-sync với exponential backoff khi online
- [ ] Toast thông báo: "Đã đồng bộ X/Y đơn offline"
- [ ] Records xóa khỏi IndexedDB sau sync thành công

### Blocked by

- Issue #1 (Schema — API cần `ClientOrderId` field)

---

## Issue #8 — Frontend: iPad Landscape Layout 3 cột

**Type**: 🤖 AFK | **Labels**: `frontend`, `ui`, `ready-for-agent`

### What to build

Refactor `pos-premium.css` + `Index.cshtml` từ layout 2 cột thành 3 cột landscape: Menu (left) | Giỏ hàng (center) | Thanh toán (right). Tất cả nút bấm ≥ 44×44px (Apple HIG). Numpad lớn tối ưu chạm. Responsive cho iPad Pro 12.9" (2048×2732 @2x). Dark mode palette giữ nguyên.

### Acceptance criteria

- [ ] Layout 3 cột hiển thị đúng trên viewport 1024×1366 (iPad Pro landscape @1x)
- [ ] Tất cả nút bấm có min-width/min-height ≥ 44px
- [ ] Numpad digits ≥ 56×56px
- [ ] Menu grid hiển thị 3-4 cột cards với hình ảnh
- [ ] Thanh toán tối đa 3 lần chạm: chọn món → chọn size → thanh toán
- [ ] Dark mode palette không thay đổi

### Blocked by

None — can start immediately.

---

## Issue #9 — API: Open API Mock endpoints cho Kế toán/ERP

**Type**: 🤖 AFK | **Labels**: `backend`, `api`, `ready-for-agent`

### What to build

3 GET endpoints mới (controller riêng `OpenApiController` hoặc thêm vào `PosController`):

1. `GET /api/open/orders?from={date}&to={date}` — danh sách orders theo date range
2. `GET /api/open/shifts?from={date}&to={date}` — danh sách WorkShift summary
3. `GET /api/open/inventory?storeId={id}` — snapshot tồn kho

Mock data / real data đều OK. Swagger documentation đầy đủ với XML comments.

### Acceptance criteria

- [ ] 3 endpoints tồn tại và trả về JSON
- [ ] Swagger UI hiển thị đầy đủ endpoint + mô tả
- [ ] Response format có pagination (page, pageSize, total)
- [ ] `dotnet build` pass 0 errors

### Blocked by

None — can start immediately.

---

## Issue #10 — Frontend: Printer Status Indicator real-time

**Type**: 🤖 AFK | **Labels**: `frontend`, `signalr`, `ready-for-agent`

### What to build

iPad POS kết nối `PrintBridgeHub` qua SignalR JS client. Nhận event `PrinterStatus` từ backend. Hiển thị icon trên POS header:
- 🟢 Printer Online
- 🔴 Printer Offline (+ SweetAlert cảnh báo lần đầu)

Reconnect tự động khi mất kết nối SignalR.

### Acceptance criteria

- [ ] SignalR JS client kết nối `/hubs/print-bridge`
- [ ] Icon trạng thái máy in hiển thị trên header (cạnh network indicator)
- [ ] SweetAlert cảnh báo khi printer chuyển từ online → offline
- [ ] Auto-reconnect SignalR

### Blocked by

- Issue #3 (PrintBridgeHub)
- Issue #6 (Print Bridge Worker — cần chạy để có heartbeat)

---

## Issue #11 — Frontend: Online/Offline Banner + Network Indicator upgrade

**Type**: 🤖 AFK | **Labels**: `frontend`, `ui`, `ready-for-agent`

### What to build

Cải thiện UX indicator hiện có trong `pos-app.js`:
- Animation mượt khi chuyển đổi Online ↔ Offline
- Badge đếm số đơn offline pending trong IndexedDB
- Progress bar / spinner khi đang sync
- Disable nút Sync thủ công khi đang sync (debounce)

### Acceptance criteria

- [ ] Badge hiển thị số đơn offline pending (cập nhật real-time)
- [ ] Progress indicator khi sync đang chạy
- [ ] Animation CSS transition khi banner show/hide
- [ ] Nút sync (nếu có) disabled khi đang sync

### Blocked by

- Issue #7 (IndexedDB — cần đếm records từ IndexedDB)

---

## Issue #12 — Testing: Integration test suite cho ADR decisions

**Type**: 🤖 AFK | **Labels**: `backend`, `testing`, `ready-for-agent`

### What to build

3 integration tests chứng minh 3 ADR hoạt động đúng:

1. **ADR-0001 (Blind Selling)**: Sync 10 đơn Matcha khi kho chỉ còn 3 phần → `AvailableQty = -7`, tất cả 10 order commit thành công.
2. **ADR-0002 (Idempotency)**: Gửi batch 5 đơn, 2 đơn có `ClientOrderId` trùng → DB chỉ có 3 order mới.
3. **ADR-0003 (Print Bridge)**: Commit order → SignalR group nhận `PrintJob` event với `byte[]` chứa ESC/POS commands.

### Acceptance criteria

- [ ] 3 test cases pass
- [ ] Test case 1: verify `StoreInventory.AvailableQty < 0` sau sync
- [ ] Test case 2: verify duplicate `ClientOrderId` không tạo order mới
- [ ] Test case 3: verify SignalR mock client nhận `PrintJob` payload
- [ ] `dotnet test` pass 0 failures

### Blocked by

- Issue #1 (Schema)
- Issue #2 (Idempotent Sync)
- Issue #5 (Print Trigger)

---

## Issue #13 — Documentation: Cập nhật STAFFHUB_POS_BUSINESS_LOGIC.md

**Type**: 🤖 AFK | **Labels**: `documentation`, `ready-for-agent`

### What to build

Cập nhật tài liệu nghiệp vụ `STAFFHUB_POS_BUSINESS_LOGIC.md` và `POS_BUSINESS_LOGIC_PROMPT.md` phản ánh kiến trúc mới:
- IndexedDB thay thế localStorage
- ClientOrderId + Idempotency flow
- SignalR Print Bridge architecture
- Layout 3 cột iPad
- Blind Selling + Negative Inventory
- Open API Mock endpoints

### Acceptance criteria

- [ ] Tài liệu phản ánh đúng kiến trúc mới
- [ ] Mermaid diagrams cập nhật
- [ ] Cheat Sheet file structure cập nhật
- [ ] Không còn reference đến localStorage cho offline orders

### Blocked by

- Issues #1 through #8 (tất cả implementation phải hoàn thành trước khi update docs)

---

## Dependency Graph

```
#1 Schema ──────► #2 Idempotent Sync ──► #5 Trigger Print ──► #12 Tests
   (HITL)            (AFK)                    (AFK)               (AFK)
                                                ▲
#3 PrintBridgeHub ──► #5                 #4 ESC/POS Builder ──► #5
     (AFK)                                    (HITL)
       │
       ├──► #6 Print Bridge Worker (HITL)
       └──► #10 Printer Status UI (AFK)

#1 ──► #7 IndexedDB (AFK) ──► #11 Offline UX (AFK)

#8 iPad Layout (AFK, standalone)
#9 Open API Mock (AFK, standalone)
#13 Docs (AFK, after #1-#8)
```

## Quick Start Order (suggested)

Parallel track A (Backend): `#1` → `#2` → `#3` → `#4` → `#5` → `#6` → `#12`
Parallel track B (Frontend): `#8` (standalone) → `#7` → `#11` → `#10`
Final: `#9` (standalone) → `#13` (docs)

---

## gh CLI Commands (chạy sau khi cài gh)

Sau khi cài `gh` CLI (`winget install GitHub.cli` + `gh auth login`), chạy các lệnh dưới đây theo thứ tự dependency:

```powershell
# #1 Schema (HITL)
gh issue create --title "[POS-iPad] Schema: Thêm ClientOrderId vào Order + Migration" --label "backend,database,ready-for-agent" --body "## What to build`nThêm ClientOrderId (Guid?, nullable) lên Order. Tạo Unique Filtered Index. HITL: Dev review migration trước khi Update-Database.`n`n## Acceptance criteria`n- [ ] Model Order có ClientOrderId (Guid?)`n- [ ] Migration tạo Unique Filtered Index`n- [ ] dotnet build pass`n`n## Blocked by`nNone"

# #3 PrintBridgeHub (AFK, no blocker)
gh issue create --title "[POS-iPad] SignalR: Tạo PrintBridgeHub + print status broadcast" --label "backend,signalr,ready-for-agent" --body "## What to build`nSignalR Hub mới PrintBridgeHub. Group theo StoreId. Heartbeat printer status.`n`n## Acceptance criteria`n- [ ] PrintBridgeHub registered tại /hubs/print-bridge`n- [ ] SendPrintJob + ReportPrinterStatus methods`n- [ ] dotnet build pass`n`n## Blocked by`nNone"

# #4 ESC/POS Builder (HITL, no blocker)
gh issue create --title "[POS-iPad] Service: ESC/POS Receipt Builder" --label "backend,service,ready-for-agent" --body "## What to build`nIEscPosBuilder.BuildReceipt() trả byte[] ESC/POS. HITL: Dev review byte sequences.`n`n## Acceptance criteria`n- [ ] BuildReceipt trả byte[] với init/cut/kick commands`n- [ ] Unit test verify byte output`n`n## Blocked by`nNone"

# #8 iPad Layout (AFK, no blocker)
gh issue create --title "[POS-iPad] Frontend: iPad Landscape Layout 3 cột" --label "frontend,ui,ready-for-agent" --body "## What to build`nRefactor layout 2→3 cột. Touch target ≥44px. Dark mode.`n`n## Acceptance criteria`n- [ ] 3 cột landscape iPad Pro`n- [ ] Nút ≥44x44px`n- [ ] Numpad ≥56x56px`n`n## Blocked by`nNone"

# #9 Open API Mock (AFK, no blocker)
gh issue create --title "[POS-iPad] API: Open API Mock endpoints cho Kế toán/ERP" --label "backend,api,ready-for-agent" --body "## What to build`n3 GET endpoints mock: orders, shifts, inventory. Swagger docs.`n`n## Acceptance criteria`n- [ ] 3 endpoints trả JSON`n- [ ] Swagger UI hiển thị`n`n## Blocked by`nNone"

# Tiếp tục cho #2, #5, #6, #7, #10, #11, #12, #13 theo dependency order...
```
