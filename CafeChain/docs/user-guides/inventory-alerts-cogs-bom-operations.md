# Hướng dẫn vận hành kho, cảnh báo, BOM và giá vốn CafeChain

Tài liệu này dành cho Chủ doanh nghiệp, Quản lý khu vực, Quản lý chi nhánh, Kế toán/Kho và Ca trưởng.
Tên màn hình dưới đây đồng bộ với menu Admin của phân hệ **Kho & Cung ứng**.

## Thuật ngữ trên giao diện

| Tên trên giao diện | Ý nghĩa |
| --- | --- |
| Yêu cầu nhập hàng | Nhu cầu bổ sung tồn của chi nhánh |
| Đề nghị mua hàng | Phần nhu cầu cần mua từ Nhà cung cấp |
| Tổng hợp đề nghị mua | Nơi Kế toán gom đề nghị của nhiều chi nhánh |
| Đơn đặt hàng gộp | Đơn tổng hợp gửi Nhà cung cấp |
| Đơn đặt hàng chi nhánh | Phần phân bổ để từng chi nhánh nhận hàng |
| Phiếu nhận hàng | Chứng từ kiểm đếm và nhập kho |
| Bán thành phẩm | Thành phần đã sơ chế được quản lý tồn riêng |
| Công thức BOM | Định mức thành phần cho món, topping hoặc bán thành phẩm |
| Lớp giá FIFO | Lô giá vốn của hàng tồn |

Trong mã nguồn, các khái niệm trên lần lượt có thể mang tên `RestockRequest`, `PurchaseAdvice`, `PurchaseOrderBatch`, `PurchaseOrder`, `BranchReceipt`, `PreparedItem` và `InventoryCostLayer`. Nhân viên vận hành chỉ cần dùng tên tiếng Việt trên giao diện.

## 1. Hiểu đúng bốn khái niệm

| Khái niệm        | Ý nghĩa vận hành                                                                                     |
| ---------------- | ---------------------------------------------------------------------------------------------------- |
| Tồn kho chi nhánh | Tồn theo từng chi nhánh, gồm **Nguyên liệu** và **Bán thành phẩm**.                                   |
| Công thức BOM     | Định mức nguyên liệu hoặc bán thành phẩm cho món bán theo size, topping hoặc một mẻ sơ chế.          |
| Cảnh báo kho     | Tín hiệu thiếu hàng cần quản lý xác nhận; không phải chứng từ nhập kho.                              |
| Giá vốn          | Chi phí mô phỏng theo lớp giá FIFO của chi nhánh được chọn; giá bán đồ uống áp dụng toàn hệ thống.   |

Số liệu trên màn hình ngưỡng được hiểu như sau:

```text
Tồn vật lý = số lượng hiện có tại chi nhánh
Khả dụng = Tồn vật lý - Giữ chỗ
```

Không sửa số lượng tồn trực tiếp để “làm đẹp” số liệu.
Tồn phải thay đổi qua bán hàng, phiếu nhận, phiếu kho, điều chuyển hoặc lệnh sơ chế có chứng từ.

## 2. Phân quyền chính

| Công việc                             | Vai trò chính                                                                    |
| ------------------------------------- | -------------------------------------------------------------------------------- |
| Xem tồn kho theo phạm vi              | Các vai trò quản lý/vận hành được cấp phạm vi chi nhánh/khu vực                  |
| Cập nhật ngưỡng tồn                   | Quản lý chi nhánh, Quản lý khu vực, Chủ doanh nghiệp, Quản trị hệ thống          |
| Xác nhận/báo sai cảnh báo             | Quản lý chi nhánh hoặc Chủ doanh nghiệp                                          |
| Xem cảnh báo                          | Có thể gồm Kế toán/Kho, Ca trưởng, Nhân viên bán hàng theo phạm vi               |
| Sửa bán thành phẩm/Công thức BOM      | Kế toán/Kho, Chủ doanh nghiệp, Quản trị hệ thống                                 |
| Xem mô phỏng giá vốn                  | Chủ doanh nghiệp, Kế toán/Kho, Quản lý khu vực/chi nhánh theo phạm vi            |
| Đổi giá bán/chính sách topping        | Chủ doanh nghiệp                                                                 |
| Tổng hợp đề nghị, tạo đơn gộp         | Kế toán/Kho hoặc Chủ doanh nghiệp                                                |
| Duyệt đơn đặt hàng gộp                | Chủ doanh nghiệp                                                                 |
| Nhận hàng tại chi nhánh               | Quản lý chi nhánh/Ca trưởng đúng phạm vi                                         |

Hệ thống vẫn kiểm tra vai trò và phạm vi chi nhánh/khu vực. Việc nhìn thấy nút trên giao diện không thay thế phân quyền phía máy chủ.

## 3. Chuẩn bị dữ liệu trước khi vận hành

Thực hiện theo thứ tự sau:

1. Vào **Kho & Cung ứng → Đơn vị và quy đổi**: cấu hình đơn vị vật lý như kg ↔ g, l ↔ ml.
2. Vào **Nguyên liệu**: mỗi nguyên liệu phải có đơn vị cơ sở đúng.
3. Vào **Nhà cung cấp**: khai báo quy cách và giá cung cấp, số lượng trong gói, đơn vị nội dung, giá gói, số lượng đặt tối thiểu và thời gian giao.
4. Nếu có bán thành phẩm như trà nền/sốt: tạo **Bán thành phẩm** với đơn vị cơ sở ổn định.
5. Vào **Sản xuất/BOM → Công thức BOM** để tạo công thức món, topping và bán thành phẩm.
6. Vào **Tình trạng dữ liệu BOM** và xử lý hết lỗi quan trọng.
7. Phát hành món/size cho từng chi nhánh trong **Menu cửa hàng**.
8. Nhập tồn đầu hoặc nhận hàng đúng chứng từ, sau đó cấu hình ngưỡng.

### Quy cách mua và giá gói

Giá hiện hành là giá của **một gói mua**, không phải giá một gram/ml. Ví dụ:

```text
Gói cà phê: 1 kg, giá 140.000 ₫
Quy đổi: 1 kg = 1.000 g
Giá cơ sở: 140.000 / 1.000 = 140 ₫/g
```

Không suy ra quy cách từ tên nguyên liệu và không dùng “chai/gói” như một quy đổi vật lý cố định nếu mỗi chai/gói có dung tích khác nhau.

## 4. Cấu hình BOM đúng cách

Mở **Sản xuất / BOM → Công thức BOM**. Có ba tab:

- **Món bán**: công thức theo đồ uống + size.
- **Topping**: định mức cho một phần topping.
- **Bán thành phẩm**: công thức sơ chế, có sản lượng mỗi mẻ và đơn vị đầu ra.

### Quy tắc bắt buộc

1. Món bán phải có công thức BOM đúng size đang phát hành trên POS.
2. Một dòng công thức chỉ chọn **Nguyên liệu hoặc Bán thành phẩm**, không chọn cả hai.
3. Số lượng và đơn vị phải có quy đổi hợp lệ về đơn vị cơ sở.
4. Bán thành phẩm phải có sản lượng, đơn vị đầu ra và tỷ lệ thu hồi hợp lệ.
5. Khi sửa công thức đã dùng, hệ thống lưu phiên bản mới; tồn bán thành phẩm luôn theo đúng mã bán thành phẩm.
6. Mở **Tình trạng dữ liệu BOM** để xử lý: thiếu giá, thiếu quy đổi, thiếu đầu ra hoặc liên kết lỗi.

### Tính một cấp và tính nhiều tầng khác nhau

| Luồng                           | Cách đọc BOM                                   |
| ------------------------------- | ---------------------------------------------- |
| POS kiểm tra còn bán được không | Một cấp                                        |
| POS trừ kho sau thanh toán      | Một cấp                                        |
| Tính giá vốn BOM                | Có thể đi sâu tối đa 5 tầng, có chống vòng lặp |

Nếu công thức món dùng bán thành phẩm, POS trừ thẳng tồn của bán thành phẩm đó. POS **không** bóc tiếp thành nguyên liệu lá khi bán. Muốn có tồn bán thành phẩm, phải hoàn tất lệnh sơ chế trước.

### Chính sách topping

Trong màn hình **Vốn và lợi nhuận gộp dự kiến**, chính sách topping theo món và size xác định:

- topping có được chọn mặc định hay bắt buộc;
- giá topping đã gồm trong giá gốc hay cộng thêm;
- giá vốn topping đã nằm trong BOM món, cộng từ BOM topping, hay chỉ hiển thị;
- số lượng topping trên mỗi ly.

Không vừa đưa topping vào BOM món vừa cấu hình cộng BOM topping, vì sẽ tính vốn trùng.

## 5. Vận hành tồn kho hằng ngày

Mở **Kho & Cung ứng → Tồn kho cửa hàng**:

1. Chọn đúng chi nhánh.
2. Kiểm tra cả hai tab **Nguyên liệu** và **Bán thành phẩm**.
3. Đối chiếu tồn, giữ chỗ, lớp giá FIFO và lịch sử biến động.
4. Nếu số liệu bất thường, mở lịch sử để tìm chứng từ nguồn: đơn POS, phiếu nhận, điều chuyển, sơ chế hoặc phiếu kho.

Các hành động thay đổi tồn:

- Đơn POS đã thanh toán: trừ công thức BOM đúng một lần.
- Đơn offline: chưa trừ khi còn trên máy; trừ khi đồng bộ thành công.
- Phiếu nhận hàng đã xác nhận: cộng đúng số lượng chấp nhận và tạo lớp giá FIFO.
- Điều chuyển: chi nhánh nguồn giảm khi xuất; chi nhánh đích tăng khi xác nhận nhận.
- Lệnh sơ chế đã hoàn thành: trừ đầu vào và cộng đầu ra bán thành phẩm theo dữ liệu đã chốt của lệnh.

Chính sách bán khi thiếu tồn cho phép đơn offline đã bán được đồng bộ dù tồn xuống âm. Khi đó phải đối soát tồn vật lý; không xóa lịch sử giao dịch kho.

## 6. Cấu hình và xử lý cảnh báo kho

### Cấu hình ngưỡng

Mở **Ngưỡng tồn kho**:

1. Chọn chi nhánh.
2. Tìm nguyên liệu/bán thành phẩm.
3. Nhập ngưỡng tồn tối thiểu theo đơn vị cơ sở và bấm **Lưu**.
4. Để trống nghĩa là chưa cấu hình; hệ thống không tự bịa ngưỡng.

Trạng thái **Sắp hết hàng** xuất hiện khi lượng khả dụng không lớn hơn ngưỡng. **Hết hàng** phản ánh lượng khả dụng đã hết hoặc âm. Mỗi chi nhánh và mỗi mặt hàng kho có tối đa một cảnh báo thiếu hụt đang hoạt động.

### Xử lý cảnh báo

Mở **Cảnh báo kho → Chi tiết**:

1. So sánh số liệu tại thời điểm cảnh báo với tồn hiện tại và lịch sử biến động.
2. Nếu đúng, nhập ghi chú và chọn **Xác nhận cảnh báo**.
3. Nếu số liệu/báo cáo sai, nhập lý do và chọn **Đánh dấu báo sai**.
4. Sau khi xác nhận, tạo **Yêu cầu nhập hàng** với số lượng, ưu tiên và ghi chú.
5. Chỉ đóng thủ công khi có lý do nghiệp vụ rõ ràng.

Gợi ý số lượng trên form là:

```text
max(0, ngưỡng tối thiểu - khả dụng hiện tại)
```

Đây chỉ là gợi ý có thể kiểm chứng, không phải quyết định mua tự động.

### Khi nào cảnh báo được giải quyết?

- Tạo yêu cầu nhập hàng không làm tăng tồn và không tự đóng cảnh báo.
- Xuất điều chuyển cũng chưa làm tăng tồn tại chi nhánh nhận.
- Sau khi phiếu nhận hàng được xác nhận, hệ thống đánh giá lại tồn thực tế.
- Chỉ khi khả dụng vượt ngưỡng thì cảnh báo mới được giải quyết.
- Yêu cầu nhập hàng có thể hoàn thành nhưng cảnh báo vẫn còn nếu số thực nhận chưa đủ.

## 7. Luồng nhập hàng hoàn chỉnh

```text
Cảnh báo kho
→ Yêu cầu nhập hàng
→ Đề nghị mua hàng
→ Tổng hợp đề nghị mua theo Nhà cung cấp
→ Đơn đặt hàng gộp
→ Đơn đặt hàng theo chi nhánh
→ Chủ doanh nghiệp duyệt đơn gộp
→ Xuất phiên bản PDF + gửi Zalo
→ Chi nhánh nhận hàng bằng Phiếu nhận hàng
→ Tồn kho + lớp giá FIFO
→ đánh giá lại cảnh báo kho
```

### Các bước thao tác

1. **Gợi ý nhập hàng** chỉ phân tích; không tự đặt hàng. Trạng thái thiếu ngưỡng, giá mua, nguồn cung, quy đổi hoặc thời gian giao phải được sửa ở dữ liệu nền.
2. **Yêu cầu nhập hàng** là nhu cầu bổ sung tồn, không phải chứng từ kho.
3. **Đề nghị mua hàng** lấy phần còn thiếu sau khi trừ số đã điều chuyển, đã nằm trong đề nghị/đơn đặt hàng đang hoạt động và phần đã đóng.
4. Tại **Tổng hợp đề nghị mua**, Kế toán chọn Nhà cung cấp, quy cách cung cấp và số kiện; hệ thống kiểm tra lại số lượng đặt tối thiểu, giá, quy đổi, số lượng còn cần đặt và phạm vi phục vụ chi nhánh.
5. Tạo **Đơn đặt hàng gộp** sẽ sinh một **Đơn đặt hàng chi nhánh** cho mỗi chi nhánh trong cùng giao dịch.
6. Chủ doanh nghiệp duyệt đơn gộp một lần; không duyệt lại cùng quyết định trên từng đơn chi nhánh.
7. Xuất/tải đúng phiên bản PDF, sao chép nội dung và gửi Zalo. “Đã gửi” không có nghĩa Nhà cung cấp đã xác nhận.
8. Mỗi chi nhánh mở đơn đặt hàng của mình để nhận hàng. Ghi đúng số lượng thực giao, số lượng từ chối và lý do.
9. Chỉ phiếu nhận hàng đã xác nhận mới tăng tồn và tạo lớp giá FIFO; xác nhận lặp lại không được cộng kho hai lần.

## 8. Đọc màn hình giá vốn và lợi nhuận

Mở **Sản phẩm → Vốn & lợi nhuận dự kiến**:

1. Chọn **Chi nhánh mô phỏng FIFO**.
2. Chọn đồ uống và bấm **Tải mô phỏng**.
3. Xem từng size, BOM, thành phần FIFO và topping mặc định.

Các chỉ số:

```text
Lợi nhuận gộp = Giá bán - Giá vốn FIFO
Margin (%) = Lợi nhuận gộp / Giá bán × 100
Markup (%) = Lợi nhuận gộp / Giá vốn × 100
```

- **Giá bán** áp dụng toàn hệ thống.
- **Giá vốn FIFO** thay đổi theo chi nhánh và lớp hàng thực tế.
- “Giá vốn BOM ước tính” trong màn hình công thức là dữ liệu cấu hình/tham khảo; không thay thế FIFO vận hành tại chi nhánh.
- Nếu thiếu công thức BOM, quy đổi, lớp giá hoặc lượng FIFO, trạng thái phải là chưa đầy đủ. Không xem phần thiếu là 0.
- Chỉ Chủ doanh nghiệp được lưu giá bán toàn hệ thống. Khi dữ liệu vốn chưa đầy đủ, phải nhập lý do và xác nhận thủ công theo giao diện.

## 9. Catalog POS hiển thị ra sao

POS chỉ đưa vào catalog các món/size:

- đồ uống, size và món tại chi nhánh đang hoạt động;
- đã phát hành và đang trong thời gian hiệu lực;
- thuộc đúng chi nhánh trong phiên đăng nhập POS.

Món đã phát hành vẫn hiển thị nhưng bị khóa khi không bán được:

| Trạng thái/lý do thường gặp                       | Cần kiểm tra                                                |
| ------------------------------------------------- | ----------------------------------------------------------- |
| Chưa cấu hình công thức                           | Công thức BOM hoạt động đúng món và size                     |
| Chưa có tồn kho tại chi nhánh                     | Tồn nguyên liệu/bán thành phẩm đúng chi nhánh                |
| Hết nguyên liệu                                   | Lượng khả dụng sau khi trừ phần giữ chỗ của từng thành phần  |
| Topping bắt buộc không khả dụng                   | Cấu hình topping, công thức và tồn topping                   |
| Tạm hết hàng                                      | Trạng thái vận hành kho bán thành phẩm và dữ liệu liên kết   |

Thu ngân không thể thêm món bị khóa vào giỏ. Danh mục bán được lưu tạm theo chi nhánh trên trình duyệt và tự đồng bộ phiên bản; đơn offline là dữ liệu riêng, không được xóa khi làm mới danh mục.

## 10. Checklist vận hành

### Đầu ngày

- Kiểm tra tồn âm và bán thành phẩm sắp hết.
- Xem cảnh báo đang mở/đã xác nhận.
- Kiểm tra món POS đang bị khóa và lý do.
- Kiểm tra lệnh sơ chế cần chạy.
- Kiểm tra đơn đặt hàng chi nhánh dự kiến giao trong ngày.

### Khi nhận hàng

- Chọn đúng chi nhánh và đúng đơn đặt hàng chi nhánh.
- Đối chiếu đơn vị cơ sở/quy cách gói.
- Nhập số thực nhận, không dùng số yêu cầu thay thế.
- Ghi rõ sai lệch hoặc lý do đóng phần còn lại khi có.
- Xác nhận một lần và kiểm tra giao dịch kho/lớp giá FIFO.

### Cuối ngày

- Đối soát đơn offline đã sync và tồn âm.
- Kiểm tra cảnh báo mới từ POS và đơn offline vừa đồng bộ.
- Đối chiếu bán thành phẩm thực tế với lệnh sơ chế.
- Kiểm tra cảnh báo đã giải quyết nhưng yêu cầu nhập hàng còn mở.

### Hằng tuần

- Rà soát ngưỡng theo tốc độ tiêu thụ và thời gian giao.
- Mở **Tình trạng dữ liệu BOM**.
- Kiểm tra quy cách và giá cung cấp, số lượng đặt tối thiểu và quy đổi.
- So sánh biên lợi nhuận theo chi nhánh; xử lý size thiếu FIFO trước khi quyết định giá.

## 11. Xử lý sự cố nhanh

### POS không có menu

1. Xác nhận phiên đăng nhập thuộc đúng chi nhánh và máy chủ đang chạy.
2. Kiểm tra **Menu cửa hàng** đã bật và phát hành món-size.
3. Mở bảng điều khiển trình duyệt: nếu có lỗi bộ nhớ cục bộ, tải lại sau khi frontend mới được triển khai; danh mục bán sẽ được tạo lại nhưng đơn offline vẫn giữ nguyên.
4. Nếu UI báo lỗi, bấm **Thử tải lại**.
5. Kiểm tra API `/api/v1/pos/catalog`; lỗi 500 cần xử lý máy chủ hoặc cấu trúc cơ sở dữ liệu, không đưa dữ liệu mẫu vào frontend để che lỗi.

### Món hiện nhưng bị khóa

Đọc nhãn trên món, sau đó kiểm tra đúng công thức BOM theo size, quy đổi đơn vị và tồn nguyên liệu/bán thành phẩm tại chi nhánh. Không bật bán bằng cách bỏ kiểm soát tồn kho.

### Giá vốn chưa đầy đủ

Kiểm tra theo thứ tự: Công thức BOM → liên kết bán thành phẩm → quy đổi đơn vị → quy cách/giá Nhà cung cấp → lớp giá FIFO của chi nhánh → chính sách topping.

### Cảnh báo không biến mất

Kiểm tra lượng khả dụng hiện tại có thực sự **lớn hơn** ngưỡng chưa. Yêu cầu nhập hàng đã hoàn thành không bảo đảm đủ tồn nếu nhận thiếu hoặc vẫn có giữ chỗ.

## 12. Nguyên tắc không được phá

- Không trừ kho trước khi đơn hàng được thanh toán và ghi nhận thành công.
- Không trừ kho hai lần khi gửi lại, webhook hoặc đồng bộ offline bị trùng.
- Không dùng giá gói như giá trên đơn vị cơ sở.
- Không bóc bán thành phẩm khi POS bán; chỉ trừ tồn bán thành phẩm một cấp.
- Không coi dữ liệu giá vốn thiếu là 0.
- Không coi yêu cầu nhập hàng hoặc “đã gửi Zalo” là hàng đã về.
- Không xóa ledger/chứng từ để sửa tồn; dùng quy trình đối soát/điều chỉnh có audit.
