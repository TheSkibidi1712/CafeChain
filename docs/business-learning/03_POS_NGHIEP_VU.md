# POS / Bán hàng nghiệp vụ tại CafeChain

## 1. POS tồn tại để làm gì?

POS là điểm vận hành bán hàng tại quầy. Nó giúp nhân viên:

- mở ca và nhận trách nhiệm két;
- chọn món, size, topping;
- nhận thanh toán;
- in hóa đơn;
- ghi nhận doanh thu;
- kích hoạt tiêu hao tồn theo đơn bán;
- đóng ca và đối soát tiền.

POS không chỉ là màn hình bấm món. Nó nối ba việc:

```text
Khách mua hàng
↓
Doanh thu và thanh toán
↓
Trách nhiệm ca/két
↓
Tiêu hao kho
```

## 2. Những người tham gia

| Vai trò | Ý nghĩa trong POS |
|---|---|
| Nhân viên bán hàng | Nhận khách, lập đơn, thu tiền |
| ShiftSupervisor | Hỗ trợ các tình huống vận hành cần quyền cao hơn theo chính sách |
| StoreManager | Quản lý vận hành chi nhánh, terminal và ca |
| BusinessOwner / AreaManager | Theo dõi, phê duyệt một số ngoại lệ theo phạm vi quyền |
| Khách hàng | Người mua; có thể là khách lẻ hoặc thành viên |

Mỗi người không chỉ “đăng nhập để dùng”. Họ có trách nhiệm vận hành khác nhau.

## 3. Ca làm việc và ca két

Cần phân biệt:

| Khái niệm | Ý nghĩa |
|---|---|
| Lịch làm việc | Ca nhân sự dự kiến |
| Ca két / phiên POS | Khoảng thời gian một người chịu trách nhiệm vận hành một terminal và tiền trong két |
| Terminal | Thiết bị/quầy POS cụ thể |
| Người thao tác hiện tại | Người đang thao tác trên quầy; không tự động chuyển toàn bộ trách nhiệm két |

Luồng bình thường:

```text
Chọn terminal
↓
Xác nhận bối cảnh mở ca
↓
Nhập tiền đầu ca
↓
Mở ca két
↓
Bán hàng
↓
Kiểm đếm cuối ca
↓
Đóng ca
```

Tiền đầu ca quan trọng vì nó là mốc đối soát tiền mặt cuối ca.

Không nên nhầm:

- mở POS với chấm công;
- đổi người thao tác với đổi người chịu trách nhiệm két;
- lịch làm việc với một ca két đã mở.

## 4. Mở ca đúng lịch, trễ hoặc ngoài lịch

CafeChain phân biệt bối cảnh mở ca:

```text
Đúng lịch
↓
Mở ca bình thường

Trễ / sớm / ngoài lịch
↓
Nêu lý do
↓
Xin phê duyệt phù hợp
↓
Mở ca nếu được chấp nhận
```

Lý do của phân biệt này:

- tránh ca mở không có trách nhiệm rõ ràng;
- vẫn cho phép cửa hàng xử lý nhu cầu thật ngoài lịch;
- giữ bằng chứng ai cho phép ngoại lệ.

Nếu người dùng rời thao tác trước khi xác nhận mở ca, không nên xem như một ca két nghiệp vụ đã tồn tại.

## 5. Một terminal, một trách nhiệm vận hành

CafeChain coi terminal là một điểm vận hành có trách nhiệm rõ ràng.

Ví dụ:

```text
Terminal A đang có ca mở của nhân viên An
↓
Nhân viên Bình chọn Terminal A
↓
Không tự mở thêm một ca khác trên cùng terminal
```

Mục tiêu là tránh hai người cùng chịu trách nhiệm mơ hồ cho một két tiền vật lý.

Một cửa hàng vẫn có thể có nhiều terminal, và các nhân viên có thể mở các ca độc lập trên những terminal trống khác nhau.

## 6. Bán hàng tại quầy

Luồng bán hàng cơ bản:

```text
Chọn món
↓
Chọn size
↓
Chọn topping nếu có
↓
Kiểm tra giá và ưu đãi
↓
Nhận thanh toán
↓
Hoàn tất đơn
↓
In hóa đơn / phục vụ
```

Giá một dòng bán có thể gồm:

```text
Giá món
+ phần tăng/giảm theo size
+ giá topping
× số lượng
- giảm giá hợp lệ
```

Khách thành viên có thể được nhận diện để áp dụng chính sách hạng thành viên, điểm hoặc voucher theo điều kiện cho phép.

Không nên nhầm:

- voucher hợp lệ với giảm giá thủ công;
- khách thành viên với bất kỳ số điện thoại nhập tự do;
- đơn nháp với đơn đã hoàn tất thanh toán.

## 7. Thao tác nhạy cảm và phân tách trách nhiệm

Một số thao tác có rủi ro doanh thu hoặc tiền mặt, ví dụ:

- mở ca ngoài lịch;
- xử lý chênh lệch két;
- đóng ca ngoại lệ;
- đối soát ca;
- các thao tác vận hành được yêu cầu phê duyệt.

Nguyên tắc:

```text
Người yêu cầu
↓
Nêu đúng bối cảnh/lý do
↓
Người có quyền phù hợp xác nhận
↓
Thao tác được ghi nhận
```

Mục đích không phải làm chậm bán hàng, mà để tránh một cá nhân tự xử lý sự kiện nhạy cảm mà không có dấu vết.

Chưa có đủ bằng chứng trong hệ thống hiện tại để khẳng định mọi loại giảm giá thủ công đều dùng chung một cơ chế phê duyệt. Khi giải thích, nên tách rõ các ngoại lệ ca/két đã có quy tắc xác nhận với các chính sách khuyến mại riêng.

## 8. Đơn bán và tiêu hao kho

Khi đơn bán được chốt, CafeChain cần biết món đã dùng gì theo công thức tại thời điểm bán.

```text
Đơn bán hoàn tất
↓
Giữ ngữ cảnh món, size, topping và công thức lúc bán
↓
Ghi giảm tồn theo thành phần phù hợp
```

Nguyên tắc nghiệp vụ:

- đơn đã bán phải giữ được ngữ cảnh lịch sử;
- thay đổi công thức sau này không được làm lịch sử đơn cũ đổi nghĩa;
- tiêu hao POS dùng nguyên liệu hoặc bán thành phẩm ở một tầng phù hợp;
- không phân rã lặp toàn bộ BOM lồng nhau tại thời điểm bán;
- việc ghi giảm cần tránh nhân đôi khi thao tác được gửi lại.

Ví dụ:

```text
Khách mua Cold Brew Cam
↓
Đơn ghi nhận Cold Brew Base theo định mức đã chốt
↓
Không đồng thời trừ lại toàn bộ cà phê hạt bên trong Cold Brew Base
```

## 9. Ngoại tuyến và đồng bộ

Trong thực tế cửa hàng có thể mất kết nối.

Mục tiêu nghiệp vụ của chế độ ngoại tuyến là:

- không bỏ lỡ giao dịch với khách;
- giữ thông tin đơn để đồng bộ lại;
- tránh một đơn được tính doanh thu hoặc trừ kho nhiều lần khi mạng trở lại.

Luồng mong muốn:

```text
Mất kết nối
↓
Lưu giao dịch chờ đồng bộ
↓
Phục vụ khách theo khả năng vận hành
↓
Có mạng trở lại
↓
Đồng bộ một lần có kiểm soát
↓
Ghi nhận tiêu hao phù hợp
```

Chưa có đủ bằng chứng trong hệ thống hiện tại để khẳng định mọi tình huống ngoại tuyến đều cho phép hoàn tất thanh toán theo cùng một chính sách. Khi vận hành, cần tuân thủ chính sách của chi nhánh về tiền mặt, hóa đơn và đồng bộ.

## 10. Đóng ca và đối soát

Đóng ca không đơn thuần là bấm “kết thúc”.

```text
Tổng hợp tiền theo giao dịch
↓
Tính tiền kỳ vọng trong két
↓
Nhân viên kiểm đếm thực tế
↓
So sánh chênh lệch
↓
Giải trình hoặc xin xác nhận ngoại lệ nếu cần
↓
Đóng ca
```

Ví dụ:

```text
Tiền kỳ vọng: 5.500.000 đ
Tiền thực tế: 4.500.000 đ
Chênh lệch: -1.000.000 đ
```

Nếu chênh lệch vượt mức được chấp nhận, nhân viên không nên tự đóng ca. Cần kiểm tra và xác nhận theo quyền vận hành.

Không được coi việc đổi người thao tác trong ca là đã bàn giao trách nhiệm tiền. Trách nhiệm ca/két chỉ kết thúc khi quy trình đóng và đối soát hoàn tất.

## 11. Những tình huống cần giải thích khi bảo vệ

### Ca hết giờ khi đang bán

Mục tiêu vận hành là không làm hỏng giao dịch đang dở. Nhân viên cần hoàn tất giao dịch hiện tại, sau đó xử lý đóng hoặc bàn giao theo quy định.

### Hai thiết bị dùng cùng một két

Phải tránh mở hai trách nhiệm song song trên cùng terminal/két. Nếu cần nhiều quầy cùng lúc, nên dùng các terminal riêng.

### Mất mạng

Không được coi thao tác đồng bộ lại là cơ hội tạo lại đơn hoặc trừ kho lần nữa.

### Thay đổi công thức sau khi bán

Đơn cũ cần giữ dấu vết định mức tại thời điểm bán; không tính lại lịch sử theo công thức mới.

## 12. Cách kể ngắn trong buổi bảo vệ

> POS của CafeChain quản lý đồng thời bán hàng, trách nhiệm ca/két và tiêu hao kho. Nhân viên mở ca trên terminal với tiền đầu ca, bán món theo menu và định mức đã chốt, rồi đóng ca bằng đối soát tiền thực tế. Các ngoại lệ quan trọng cần lý do và người có quyền xác nhận. Khi đơn hoàn tất, hệ thống giữ ngữ cảnh lúc bán để doanh thu, công thức và tồn kho có thể truy vết nhất quán.

