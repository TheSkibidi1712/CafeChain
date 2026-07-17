# Hướng dẫn sử dụng CafeChain Dashboard Analytics

## 1. Quyền truy cập và phạm vi dữ liệu

Dashboard yêu cầu permission `App.AdminDashboard`. Dữ liệu không chỉ phụ thuộc vào card App Launcher: controller vẫn kiểm tra permission, còn service tiếp tục giới hạn Province, District và Store theo `StaffScope` của nhân viên.

Nếu nhận HTTP 403, hãy kiểm tra role permission, account override và phạm vi cửa hàng. Không dùng tài khoản khác hoặc sửa URL để vượt phạm vi.

## 2. Bộ lọc

| Bộ lọc | Ý nghĩa |
|---|---|
| From Date / To Date | Khoảng nghiệp vụ cần phân tích. Backend dùng khoảng ngày nửa mở. |
| Province | Chỉ các tỉnh thuộc store scope. |
| District | Được lọc từ Province và store scope. |
| Store | Một cửa hàng cụ thể hoặc toàn bộ cửa hàng được cấp quyền. |
| Granularity | `Hour`, `Day`, `Week`, `Month`; tuần bắt đầu thứ Hai. |
| Top | Số phần tử xếp hạng, từ 1 đến 100. |

Sau khi thay đổi filter, nhấn **Áp dụng**. Dashboard hủy request đang chạy, xóa cache của sáu tab và tải lại tab hiện tại. Một tab chưa mở sẽ chưa gọi API; khi mở lần đầu, dữ liệu được tải và cache cho tới lần Apply kế tiếp.

Ví dụ:

- Theo dõi hôm nay tại một cửa hàng: chọn cùng From/To, Granularity `Hour`, rồi chọn Store.
- So sánh tuần: chọn khoảng nhiều tuần và Granularity `Week`.
- Tìm sản phẩm chủ lực: mở tab Sản phẩm và tăng Top lên 20 hoặc 50.

## 3. Sáu tab nghiệp vụ

### Điều hành

- KPI tổng quan và net-sales line.
- Store ranking bar.
- Payment mix donut.
- Order heatmap theo ngày/giờ.
- Operational alerts cần xử lý.

### POS / WorkShift

- Cash discrepancy và các ca lệch két lớn.
- Shift sales và payment mix theo ca.
- Offline reconciliation.
- Hourly orders và KPI vận hành ca.

### Kho

- Shortage risk và hàng dưới ngưỡng.
- Inventory movement.
- Threshold và reorder suggestion.
- Waste và tuổi lớp FIFO.

### Mua hàng

- PO pipeline và PO quá hạn.
- Supplier quality.
- Purchase price trend và spend.
- Issue mix theo nhóm sự cố.

### Sản phẩm

- Top products.
- Volume-margin scatter.
- Size margin và topping performance.
- BOM health và sản phẩm có hiệu quả tiêu hao thấp.

### Nhân sự

- Trạng thái StaffShift.
- Nhu cầu nhân sự theo giờ.
- Hiệu suất nhân viên trong phạm vi cửa hàng được phép.

## 4. Cách đọc biểu đồ

- Di chuột lên điểm/cột để xem tooltip chính xác.
- Bấm legend để ẩn hoặc hiện series.
- Heatmap: màu đậm hơn biểu thị mật độ đơn cao hơn.
- Scatter volume-margin: trục khối lượng và biên lợi nhuận phải được đọc cùng nhau; doanh số cao không đồng nghĩa lợi nhuận tốt.
- Trên mobile, bảng rộng cuộn ngang; ECharts tự resize theo vùng hiển thị.

## 5. Công thức nghiệp vụ

- Chỉ đơn `Completed` được tính.
- Merchandise net = `Order.Total - ShippingFee`.
- Full refund đảo doanh thu của đơn hoàn tất.
- Topping revenue nhân số lượng `OrderDetail`.
- Gross profit chỉ được xác nhận khi COGS đầy đủ. Nếu thiếu cost layer hoặc BOM, widget có thể trả `PARTIAL_DATA`.

## 6. Trạng thái widget

- `LOADING`: skeleton đang chờ dữ liệu.
- `NO_DATA`: procedure chạy thành công nhưng không có dòng phù hợp.
- `PARTIAL_DATA`: một phần dữ liệu hoặc một procedure trong tab bị lỗi; các widget còn lại vẫn hiển thị.
- `ERROR`: widget không tải được. Dùng Retry trên widget; không cần tải lại cả trang.

## 7. Xử lý sự cố

| Hiện tượng | Cách xử lý |
|---|---|
| Không có dữ liệu | Mở rộng khoảng ngày, bỏ Store filter hoặc kiểm tra nghiệp vụ đã Completed. |
| HTTP 403 | Nhờ quản trị viên kiểm tra `App.AdminDashboard`, account override và StaffScope. |
| Một widget lỗi | Bấm Retry; widget khác vẫn có thể dùng bình thường. |
| Filter đã đổi nhưng biểu đồ chưa đổi | Nhấn Apply để hủy request cũ và tải lại cache tab. |
| Số liệu chưa mới | Dashboard không auto-refresh; Apply hoặc tải lại trang. |

## 8. Giới hạn hiện tại

- Chưa hỗ trợ export trực tiếp từ Dashboard.
- Không hiển thị dữ liệu ngoài `StaffScope`.
- Không tự refresh liên tục.
- Không thay thế báo cáo kế toán đã khóa sổ; đây là màn hình analytics vận hành.
