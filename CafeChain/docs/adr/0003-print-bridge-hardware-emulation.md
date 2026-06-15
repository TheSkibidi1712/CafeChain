# Print Bridge chạy trên PC/Laptop + Virtual Printer Emulator

Backend trên cloud không thể mở TCP socket đến máy in LAN tại quán. Cần một Print Bridge trung gian chạy tại quán, nhận lệnh in qua SignalR từ cloud và chuyển tiếp sang máy in qua TCP port 9100 (ESC/POS).

Trong phạm vi MVP dự án tốt nghiệp, Print Bridge chạy trên PC/Laptop cá nhân dưới dạng .NET Worker Service, kết nối Virtual Printer Emulator (giả lập) thay vì máy in vật lý. Kiến trúc giữ nguyên tính real-world ready — chỉ cần đổi IP target từ localhost sang IP máy in thật.

## Considered Options

1. **Mac Mini luôn bật tại quán** — Ổn định, production-grade.
   Rejected: Chi phí ~$500, nằm ngoài ngân sách dự án tốt nghiệp.

2. **Raspberry Pi** — Rẻ (~$50), nhỏ gọn.
   Rejected: Cần setup Linux + .NET runtime, tăng độ phức tạp deploy cho team sinh viên.

3. **PC/Laptop cá nhân + Virtual Printer Emulator** — ✅ Chọn.
   Zero cost. Print Bridge là .NET Worker Service siêu nhẹ, forward ESC/POS bytes sang localhost:9100. Virtual Printer Emulator hiển thị bill trên màn hình để demo. Khi deploy thật, chỉ cần đổi IP target.

## Constraints

- MVP: 1 máy in duy nhất (Receipt Printer cho bill khách). Phiếu pha chế barista — ngoài scope.
- Print Bridge kết nối cloud qua SignalR Hub (`PrintBridgeHub`), join group theo StoreId.
- ESC/POS payload bao gồm: header quán, danh sách món, tổng tiền, QR code, lệnh cắt giấy, lệnh mở két RJ11.
