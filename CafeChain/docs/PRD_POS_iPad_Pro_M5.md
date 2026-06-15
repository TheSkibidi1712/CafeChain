# PRD: Phân hệ POS iPad Pro M5 — CafeChain

## Problem Statement

Hệ thống POS hiện tại của CafeChain (Razor View + jQuery) được thiết kế cho desktop browser, không tối ưu cho iPad:
- Nút bấm quá nhỏ cho thao tác chạm, không đạt chuẩn 44×44px của Apple HIG.
- In hóa đơn dùng `window.print()` → luôn hiện Print Dialog trên Safari → không đạt Silent Print.
- Offline mode dùng `localStorage` đơn giản, không có Idempotency Key → retry gây trùng đơn.
- Không có cơ chế mở két tiền ESC/POS qua cổng RJ11.
- Layout 2 cột không phù hợp với tỷ lệ màn hình landscape của iPad Pro M5 (12.9").

Thu ngân tại quán cần một giao diện POS chạy mượt trên Safari iPadOS, hỗ trợ bán hàng liên tục kể cả khi mất mạng, và in bill tự động không cần tương tác.

## Solution

Xây dựng lại module POS dành riêng cho iPad Pro M5, chạy trên Safari (iPadOS) landscape mode, kết nối backend ASP.NET Core trên cloud. Giải quyết 3 bài toán kiến trúc khó bằng:

1. **Blind Selling + Negative Inventory** (ADR-0001): Bán mà không check tồn kho khi offline, chấp nhận kho âm, đối soát bằng Inventory Reconciliation.
2. **ClientOrderId Idempotency** (ADR-0002): UUID v4 sinh lúc thanh toán, lưu trên model `Order` với Unique Constraint, chống trùng đơn khi sync.
3. **SignalR Hybrid Bridge** (ADR-0003): .NET Worker Service chạy tại quán, nhận lệnh in từ cloud qua SignalR, forward ESC/POS bytes sang máy in LAN (hoặc Virtual Printer Emulator cho MVP).

## User Stories

### Ca làm việc (Shift Management)
1. As a Thu ngân, I want to mở ca két tiền bằng cách nhập StartingCash trên iPad, so that tôi có thể bắt đầu bán hàng.
2. As a Thu ngân, I want to bị chặn mở ca nếu chưa chấm công Face ID (StaffShift), so that hệ thống đảm bảo chỉ nhân viên đã vào ca mới được truy cập POS.
3. As a Thu ngân, I want to được cảnh báo khi mở ca trễ >30 phút và yêu cầu PIN Trưởng ca, so that các trường hợp ngoại lệ được ghi log kiểm toán.
4. As a Thu ngân, I want to đóng ca bằng cách nhập tiền mặt đếm thực tế (ActualEndingCash), so that hệ thống tính chênh lệch két tiền tự động.
5. As a Thu ngân, I want to bắt buộc nhập lý do nếu két tiền chênh lệch ≠ 0, so that có audit trail cho quản lý đối soát.

### Bán hàng (Order Flow)
6. As a Thu ngân, I want to chọn món từ menu grid với hình ảnh lớn (tối ưu chạm), so that thao tác nhanh trên màn hình cảm ứng.
7. As a Thu ngân, I want to chọn size và topping cho mỗi món, so that đơn hàng ghi nhận đúng cấu hình.
8. As a Thu ngân, I want to thêm/bớt số lượng và xóa món trong giỏ hàng, so that linh hoạt chỉnh sửa trước khi thanh toán.
9. As a Thu ngân, I want to tìm khách hàng thành viên bằng SĐT và áp dụng điểm tích lũy, so that khách hàng được hưởng ưu đãi Loyalty.
10. As a Thu ngân, I want to đăng ký nhanh khách hàng mới ngay trên POS, so that không cần chuyển sang hệ thống khác.
11. As a Thu ngân, I want to áp mã voucher giảm giá, so that khách hàng được hưởng khuyến mãi.
12. As a Thu ngân, I want to thanh toán bằng tiền mặt với numpad lớn (tối ưu chạm), so that nhập nhanh số tiền khách đưa.
13. As a Thu ngân, I want to thanh toán bằng QR (VietQR), so that hỗ trợ thanh toán không tiền mặt.
14. As a Thu ngân, I want to chia thanh toán (Split Payment) giữa tiền mặt và QR, so that xử lý các trường hợp thanh toán hỗn hợp.
15. As a Thu ngân, I want to thấy tiền thối lại tự động sau khi nhập tiền khách đưa, so that giảm sai sót khi thối tiền.
16. As a Thu ngân, I want to chọn loại đơn (Tại quán / Mang đi), so that báo cáo phân loại chính xác.

### Bảo mật & Ủy quyền (Supervisor Auth)
17. As a Trưởng ca, I want to xác thực PIN 4 số khi thu ngân thực hiện thao tác nhạy cảm (hủy đơn, giảm giá sâu, sửa giá gốc), so that mọi hành động rủi ro đều có audit trail.
18. As a Trưởng ca, I want to khóa PIN sau 5 lần nhập sai trong 15 phút, so that chống brute-force.

### Bán hàng Offline (PWA + IndexedDB)
19. As a Thu ngân, I want to thấy trạng thái Online/Offline rõ ràng trên header POS, so that biết mạng đang hoạt động hay không.
20. As a Thu ngân, I want to tiếp tục bán hàng bình thường khi mất mạng (Blind Selling), so that doanh thu không bị gián đoạn.
21. As a Thu ngân, I want to đơn hàng offline được lưu vào IndexedDB kèm UUID v4 (ClientOrderId), so that mỗi đơn có định danh duy nhất bất biến.
22. As a Thu ngân, I want to hệ thống tự động sync đơn offline lên cloud khi mạng phục hồi, so that không cần thao tác thủ công.
23. As a Thu ngân, I want to nhận thông báo kết quả sync (thành công/thất bại), so that biết trạng thái đồng bộ.
24. As a Hệ thống, I want to kiểm tra ClientOrderId trước khi commit đơn, so that retry không gây trùng đơn (Idempotent).
25. As a Quản lý kho, I want to kho cho phép giá trị âm (Negative Inventory) khi sync offline, so that đơn hàng đã bán thực tế không bị từ chối.

### In ấn (Silent Print via SignalR Hybrid Bridge)
26. As a Thu ngân, I want to bill được in tự động sau khi thanh toán mà không hiện Print Dialog, so that flow bán hàng liền mạch.
27. As a Hệ thống, I want to gửi lệnh in ESC/POS từ cloud backend qua SignalR đến Print Bridge tại quán, so that iPad không cần giao tiếp trực tiếp với máy in LAN.
28. As a Print Bridge, I want to forward ESC/POS bytes sang TCP port 9100 của máy in (hoặc Virtual Printer Emulator), so that bill được in ra giấy.
29. As a Print Bridge, I want to gửi lệnh mở két tiền qua ESC/POS (kick cash drawer RJ11), so that két tự động bật sau mỗi giao dịch tiền mặt.
30. As a Thu ngân, I want to nhận cảnh báo real-time khi Print Bridge mất kết nối, so that biết máy in không hoạt động.

### Giao diện iPad (UI/UX)
31. As a Thu ngân, I want to tất cả nút bấm ≥ 44×44px, so that thao tác chạm chính xác trên iPad.
32. As a Thu ngân, I want to layout 3 cột landscape (Menu | Giỏ hàng | Thanh toán), so that tận dụng màn hình 12.9" của iPad Pro.
33. As a Thu ngân, I want to hoàn tất thanh toán tối đa 3 lần chạm (chọn món → chọn size → thanh toán), so that tốc độ phục vụ nhanh nhất.
34. As a Thu ngân, I want to phản hồi thao tác < 1s, so that trải nghiệm mượt mà.

## Implementation Decisions

### Schema Changes
- **Model `Order`**: Thêm trường `ClientOrderId` (Guid?, nullable). Tạo Unique Filtered Index (`WHERE ClientOrderId IS NOT NULL`).
- **Không sửa** `RequestDeduplication` — bảng này giữ vai trò API log tạm, không liên quan đến idempotency đơn hàng.

### Backend Architecture
- **Giữ nguyên N-Tier**: Controller (thin) → Service → Repository → EF Core. Không thay đổi kiến trúc tổng thể.
- **`SyncOfflineOrders` endpoint**: Nhận batch Offline Order, kiểm tra `ClientOrderId` trước khi commit. Nếu đã tồn tại → trả về OrderId cũ (HTTP 200). Nếu mới → commit + Inventory Deduction → trả về OrderId mới (HTTP 201).
- **Inventory Deduction**: Giữ nguyên logic soft-block kho âm trong `InventoryDeductionService`. Không replicate BOM sang client.
- **`PrintBridgeHub`**: SignalR Hub mới, Print Bridge join group theo StoreId. Backend gọi `SendAsync("PrintJob", escPosPayload)` sau khi commit order thành công.
- **ESC/POS Builder**: Service mới tạo mảng byte ESC/POS từ Order data (header quán, danh sách món, tổng tiền, lệnh cắt giấy, lệnh mở két RJ11).

### Print Bridge (.NET Worker Service)
- C# Console App chạy tại quán, kết nối `PrintBridgeHub` trên cloud.
- Nhận payload ESC/POS → forward sang TCP `{printerIp}:9100`.
- MVP: target `localhost:9100` (Virtual Printer Emulator). Production: đổi sang IP máy in thật.
- Heartbeat: gửi ping định kỳ, backend broadcast trạng thái printer cho iPad qua SignalR.

### Frontend (iPad POS)
- **Giữ Razor View** (server-rendered) + vanilla JS. Không chuyển sang SPA framework.
- **IndexedDB** thay thế `localStorage` cho Offline Order storage — hỗ trợ structured data, không giới hạn 5MB.
- **UUID v4** sinh bằng `crypto.randomUUID()` lúc nhấn "Thanh toán".
- **Auto-retry Sync**: Khi `navigator.onLine` chuyển `true`, gửi batch sync với exponential backoff.
- **CSS**: Responsive landscape iPad Pro 12.9" (2048×2732 @2x). Min touch target 44×44px. Dark mode palette giữ nguyên.

### Open API (Mock)
- Swagger endpoint cho Kế toán/ERP integration. Chỉ mock data, không tích hợp thật.
- Các endpoint: GET orders by date range, GET shift summary, GET inventory snapshot.

## Testing Decisions

### Seams kiểm thử
- **Service layer** (`WorkShiftService`, `POSOrderService`, `InventoryDeductionService`): Unit test logic nghiệp vụ — mở/đóng ca, commit order, tính BOM, kho âm.
- **SyncOfflineOrders endpoint**: Integration test — gửi batch có ClientOrderId trùng, xác nhận không tạo đơn mới.
- **SignalR PrintBridgeHub**: Integration test — verify client nhận đúng payload khi order commit.
- **IndexedDB + Sync flow**: Manual test trên Safari iPadOS — tắt WiFi, bán 5 đơn, bật WiFi, xác nhận sync thành công.

### Nguyên tắc
- Test behavior (output), không test implementation (internal state).
- Mỗi ADR (Blind Selling, Idempotency, Print Bridge) phải có ít nhất 1 test case chứng minh quyết định kiến trúc hoạt động đúng.

## Out of Scope

1. **Tích hợp giao hàng** (Grab/AhaMove) — LOẠI BỎ hoàn toàn theo yêu cầu.
2. **Phiếu pha chế barista** (Kitchen Display System / KDS) — chỉ 1 máy in Receipt cho MVP.
3. **Máy in POS vật lý** — dùng Virtual Printer Emulator cho MVP.
4. **Tích hợp Kế toán/ERP thật** — chỉ Mock data + Swagger.
5. **Multi-tablet đồng thời trên cùng WorkShift** — 1 iPad = 1 WorkShift.
6. **Portrait mode** — chỉ hỗ trợ landscape.
7. **Trừ kho BOM đệ quy đầy đủ trên client offline** — chỉ trừ kho server-side khi sync.

## Further Notes

### Tài liệu kiến trúc đã chốt
| ADR | Quyết định | File |
|-----|------------|------|
| ADR-0001 | Blind Selling + Negative Inventory | `docs/adr/0001-blind-selling-negative-inventory.md` |
| ADR-0002 | Idempotency via ClientOrderId on Order | `docs/adr/0002-idempotency-client-order-id.md` |
| ADR-0003 | Print Bridge + Virtual Printer Emulator | `docs/adr/0003-print-bridge-hardware-emulation.md` |

### Domain Glossary
Toàn bộ thuật ngữ chuẩn đã được lưu tại `CONTEXT.md`. Các thuật ngữ quan trọng: WorkShift, StaffShift, BOM, Offline Order, Sync, Blind Selling, Negative Inventory, Inventory Reconciliation, Idempotency Key, ClientOrderId, Silent Print, Print Proxy.

### Ràng buộc phi chức năng
| Metric | Target |
|--------|--------|
| Phản hồi thao tác | < 1s |
| Độ trễ in ấn | < 2s (cloud → SignalR → Print Bridge → Printer) |
| Touch target | ≥ 44×44px |
| Thanh toán | ≤ 3 lần chạm |
| Trình duyệt | Safari iPadOS (landscape) |
| Offline capacity | Không giới hạn (IndexedDB) |
