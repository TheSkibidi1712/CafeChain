# Hướng dẫn AI Dashboard, cấu hình lịch và cảnh báo CafeChain

Tài liệu này dành cho chủ doanh nghiệp, quản lý cửa hàng và nhóm kỹ thuật. AI Dashboard hỗ trợ ra quyết định; màn hình lịch chỉ thu thập cấu hình và cảnh báo thiếu nhân sự. AI không tự sửa dữ liệu, không tự chạy SQL và không tự phân công lịch làm việc.

## 1. AI Dashboard dùng để làm gì?

AI Dashboard đọc dữ liệu đã được backend CafeChain kiểm tra và giúp quản lý:

- Tóm tắt tình hình kinh doanh trong kỳ đang chọn.
- Giải thích doanh thu, đơn hàng, sản phẩm, tồn kho, nhà cung cấp và vận hành.
- Chỉ ra bất thường có bằng chứng.
- Đưa khuyến nghị để quản lý kiểm tra hoặc xử lý tiếp.
- Trình bày dữ liệu bằng biểu đồ và bảng tiếng Việt.

Phạm vi phân tích luôn lấy từ bộ lọc Dashboard:

- Từ ngày.
- Đến ngày.
- Cửa hàng được chọn.

Nếu nội dung câu hỏi nhắc đến ngày hoặc cửa hàng khác, hệ thống vẫn ưu tiên bộ lọc hiện tại và hiển thị phạm vi thực tế đã sử dụng. Người dùng chỉ xem được cửa hàng thuộc quyền `StaffScope`.

## 2. Cách đặt câu hỏi

Chọn ngày, cửa hàng, sau đó có thể hỏi:

- Doanh thu kỳ này tăng hay giảm?
- Cửa hàng nào đóng góp doanh thu cao nhất?
- Khung giờ nào có ít đơn?
- Sản phẩm nào bán tốt nhưng lợi nhuận thấp?
- Nguyên liệu nào đang dưới tồn tối thiểu?
- Nhà cung cấp nào có tỷ lệ hàng bị từ chối cao?
- Tôi cần chú ý điều gì trong kỳ này?

Nên hỏi một mục tiêu rõ trong mỗi câu. AI không thay đổi bộ lọc bằng câu hỏi và không tự truy vấn SQL.

## 3. Cách đọc báo cáo AI

### Tóm tắt

Nêu phạm vi ngày, cửa hàng, kết quả chính và giới hạn dữ liệu bằng 2–5 câu.

### Số liệu chính

Là dữ liệu hoặc phép tổng hợp do backend xác định, ví dụ doanh thu, số đơn, tỷ lệ hủy đơn và tồn khả dụng.

### Phân tích

Giải thích thận trọng dựa trên số liệu. Các cụm từ “có thể”, “có dấu hiệu” hoặc “cần kiểm tra thêm” thể hiện đây là suy luận, không phải dữ liệu chắc chắn.

### Bất thường

Bao gồm bất thường thống kê và cảnh báo vận hành như:

- Nguyên liệu dưới ngưỡng tồn.
- Chênh lệch tiền mặt.
- Đơn mua hàng quá hạn.
- Sự cố nhà cung cấp.
- Dữ liệu giá vốn chưa đầy đủ.

### Khuyến nghị

Là bước kiểm tra đề xuất, không phải lệnh tự động. Mỗi khuyến nghị hợp lệ có:

- Mức ưu tiên.
- Mã bằng chứng `EvidenceId`.
- Điều kiện cần xác minh trước khi hành động.

### Kết luận và cảnh báo dữ liệu

Kết luận nhắc lại vấn đề ưu tiên. Cảnh báo dữ liệu cho biết phần nào thiếu dữ liệu, dùng fallback hoặc chưa có kỳ so sánh.

## 4. Các thuật ngữ

- **Fact — Sự kiện dữ liệu:** giá trị backend xác định chắc chắn.
- **Statistic — Chỉ số tổng hợp:** tỷ lệ, trung bình hoặc mức thay đổi được tính theo quy tắc nghiệp vụ.
- **Inference — Nhận định:** giải thích có điều kiện của AI.
- **Anomaly — Bất thường:** vấn đề do rule backend, operational alert hoặc so sánh đáng tin cậy phát hiện.
- **Recommendation — Khuyến nghị:** bước quản lý nên kiểm tra; hệ thống không tự thực hiện.
- **Evidence — Bằng chứng:** nguồn dữ liệu gắn mã để kiểm chứng nhận định.
- **Giá vốn hàng bán (COGS):** chi phí nguyên liệu đã xác nhận cho sản phẩm bán ra.
- **Đơn mua hàng (PO):** chứng từ mua nguyên liệu từ nhà cung cấp.
- **Công thức nguyên liệu (BOM):** định mức nguyên liệu dùng để tạo sản phẩm.

## 5. Trạng thái dữ liệu và AI

| Trạng thái | Hiển thị | Ý nghĩa |
|---|---|---|
| `Complete` | Đầy đủ | Dữ liệu cần thiết đã đủ cho phạm vi phân tích. |
| `Partial` | Một phần | Có dữ liệu nhưng thiếu một phần như baseline hoặc giá vốn. |
| `Insufficient` | Chưa đủ dữ liệu | Chưa đủ bằng chứng để kết luận. |
| `Fallback` | Chế độ dự phòng | Ollama không khả dụng hoặc kết quả AI không hợp lệ; fact, cảnh báo và biểu đồ backend vẫn dùng được. |

Confidence phản ánh chất lượng bằng chứng. Confidence giảm khi mẫu ít, thiếu kỳ so sánh, COGS một phần, widget lỗi hoặc thiếu bằng chứng cấp thực thể.

## 6. Biểu đồ và đơn vị

- Đường: xu hướng doanh thu, đơn theo giờ hoặc giá mua.
- Cột ngang: xếp hạng cửa hàng và sản phẩm.
- Donut: cơ cấu phương thức thanh toán hoặc trạng thái.
- Heatmap: phân bổ đơn theo ngày và giờ.
- Scatter: mối quan hệ giữa số lượng và lợi nhuận.

Tiền được định dạng VND theo `vi-VN`; tỷ lệ hiển thị `%`; đơn hàng hiển thị “đơn”; tồn kho hiển thị số lượng kèm đơn vị thực tế. Nếu thiếu trục, thiếu dòng hoặc toàn bộ giá trị rỗng, giao diện tự chuyển sang bảng.

## 7. Màn hình “Cấu hình dữ liệu lịch”

Mở màn hình lịch của cửa hàng và chọn **Cấu hình dữ liệu lịch**. Bốn nhóm dữ liệu là thông tin tham khảo cho quản lý khi phân lịch thủ công; hệ thống không tự kiểm tra, cảnh báo hoặc áp dụng lịch.

### Khả dụng

Khai báo nhân viên có thể làm vào thứ nào, từ giờ nào đến giờ nào và khoảng ngày hiệu lực. Khả dụng không có nghĩa là nhân viên đã được xếp lịch.

### Giới hạn giờ

Khai báo:

- Số giờ mục tiêu mỗi tuần.
- Số giờ tối đa mỗi tuần.
- Số giờ tối đa mỗi ngày.
- Thời gian nghỉ tối thiểu giữa hai ca, tính bằng phút.

### Định mức

Chọn ca và ngày trong tuần, sau đó khai báo số nhân viên tối thiểu, mục tiêu, tối đa và vai trò bắt buộc nếu có.

### Nghỉ phép

Chọn nhân viên, thời gian bắt đầu, thời gian kết thúc và lý do. Khoảng nghỉ đã duyệt được loại khỏi danh sách nhân viên phù hợp.

### Sau khi lưu cấu hình

- Kiểm tra lại phần **Cấu hình đã lưu** để xác nhận tên nhân viên, ca, vai trò, ngày và trạng thái.
- Mở màn hình lịch nhân viên và phân ca thủ công sau khi quản lý đã xác minh tình hình thực tế.

Hệ thống không tạo phương án phân công, không cung cấp nút tự động áp dụng lịch và không thay đổi các lịch đã có.

## 8. Trạng thái cảnh báo lịch

Cơ chế worker/service tự động phát hiện và gửi cảnh báo thiếu lịch đã được gỡ. Các bản ghi notification lịch cũ được giữ trong database cho audit nhưng không hiển thị và không tính vào số chưa đọc.

## 9. Cấu hình kỹ thuật

```json
{
  "DashboardIntelligence": {
    "ExplanationEnabled": false
  }
}
```

## 10. Giới hạn bắt buộc

AI chỉ được đọc, phân tích, giải thích, cảnh báo và khuyến nghị. AI không:

- Tạo hoặc chạy SQL.
- Chọn cửa hàng ngoài StaffScope.
- Tự tạo, sửa hoặc áp dụng lịch.
- Tự tạo đơn mua hàng.
- Tự thay đổi giá, tồn kho, ca làm hoặc dữ liệu nhân viên.
- Nêu tên cửa hàng, sản phẩm, nguyên liệu hoặc nhà cung cấp không tồn tại trong Evidence.
