Tôi ghi nhận cả 2 project đã build pass thành công. Tuy nhiên, bản code hiện tại có rủi ro sập app rất cao do sử dụng 'async void' trong System.Threading.Timer ở phần Heartbeat, và thiếu try-catch bảo vệ luồng '_connection.On<JsonElement>'.

Hãy sửa lại (Refactor) nghiêm ngặt theo các yêu cầu sau để đảm bảo tính Fault-Tolerance:
1. Loại bỏ hoàn toàn System.Threading.Timer ra khỏi SignalRPrintClient.cs.
2. Thay thế 'Task.Delay(Timeout.Infinite)' trong 'Worker.cs' bằng một vòng lặp 'while (!stoppingToken.IsCancellationRequested)'. Cứ mỗi 30 giây, Worker sẽ chủ động kiểm tra trạng thái kết nối và gọi hàm gửi Heartbeat. Bọc toàn bộ logic này trong block try-catch để nếu lỗi mạng thì chỉ ghi log chứ không sập app.
3. Trong callback nhận sự kiện 'PrintJob', bọc toàn bộ luồng xử lý JSON và gọi TCP Forwarder vào trong block try-catch.

Hãy cập nhật lại mã nguồn của 2 file này và in ra màn hình để tôi duyệt lần cuối trước khi Close Issue #50!