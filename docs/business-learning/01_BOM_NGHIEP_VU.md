# BOM / Công thức nghiệp vụ tại CafeChain

## 1. BOM là gì?

**BOM** (Bill of Materials) là định mức nguyên liệu để làm ra một món bán hoặc một bán thành phẩm.

Nó trả lời câu hỏi kinh doanh:

> Để bán một ly Latte cỡ M, quán cần tiêu hao những thứ gì, bao nhiêu, theo đơn vị nào?

BOM tồn tại để CafeChain có thể:

- pha chế nhất quán giữa các chi nhánh;
- biết món nào cần những nguyên liệu nào;
- ước tính giá vốn trước khi bán;
- hỗ trợ sản xuất bán thành phẩm;
- trừ kho đúng khi món được bán.

Không nên nhầm BOM với:

- **danh mục món bán**: BOM không phải menu;
- **tồn kho**: BOM mô tả định mức, không phải số lượng đang còn;
- **đơn mua hàng**: BOM nói cần dùng gì để làm món, không nói đã cam kết mua gì;
- **công thức phiên bản cũ**: một công thức có thể được thay thế nhưng lịch sử vẫn cần biết món từng dùng công thức nào.

Ví dụ CafeChain:

```text
Latte M
↓
Espresso 1 shot
Sữa tươi 180 ml
Đá 120 g
```

Khi thu ngân bán Latte M, đây là cơ sở để hệ thống hiểu các thành phần cần được tiêu hao theo quy tắc hiện hành.

## 2. Những khái niệm phải phân biệt

| Khái niệm | Ý nghĩa kinh doanh | Không được nhầm với |
|---|---|---|
| Món bán | Thứ khách mua, ví dụ Cappuccino M | Công thức |
| Size | Biến thể phục vụ của món, ví dụ M/L | Đơn vị tồn kho |
| Topping | Phần thêm vào theo lựa chọn khách | Một nguyên liệu mặc định của món |
| Công thức | Một phiên bản định mức để làm một mục tiêu cụ thể | Mã tồn kho |
| Dòng công thức | Một thành phần và định lượng của công thức | Phiếu nhập hàng |
| Nguyên liệu | Vật tư tồn kho cơ sở, ví dụ cà phê hạt, sữa | Gói mua của nhà cung cấp |
| Bán thành phẩm | Thành phẩm làm trước để dùng tiếp, ví dụ cold brew base | Công thức cũ hoặc tên gọi tự do |
| Đơn vị cơ sở | Đơn vị chuẩn để theo dõi tồn, ví dụ kg, g, ml | Quy cách bao bì |
| Quy cách gói mua | Nội dung của một gói hàng mua từ nhà cung cấp | Đơn vị cơ sở |

Ví dụ:

```text
1 gói cà phê = 5 kg
```

“Gói” là cách mua thương mại; “kg” là lượng vật lý để quản lý tồn. Một gói không tự động là một đơn vị quy đổi chung cho mọi nguyên liệu.

## 3. Công thức phục vụ ai?

### Nhân viên pha chế và Store Manager

Cần BOM để biết món phải được chuẩn bị theo định mức nào và để phát hiện thiếu nguyên liệu.

### Warehouse Accountant

Cần BOM để hiểu nhu cầu tiêu hao có liên quan đến nguyên liệu nào, từ đó chuẩn bị mua hoặc điều phối nguồn hàng.

### BusinessOwner

Cần BOM để so sánh giá bán, giá vốn ước tính và ảnh hưởng của việc thay đổi nguyên liệu hay quy cách mua.

### ShiftSupervisor

Cần hiểu BOM ở mức vận hành: thay đổi món, topping hoặc bán thành phẩm có thể ảnh hưởng đến tiêu hao và cần được xử lý theo đúng quyền.

## 4. Cấu trúc một BOM

Một công thức có thể chứa hai loại thành phần:

```text
Công thức món bán
├─ Nguyên liệu trực tiếp
└─ Bán thành phẩm
   └─ Công thức con
```

Ví dụ:

```text
Cold Brew Cam
├─ Cold Brew Base: 180 ml
├─ Nước cam: 60 ml
└─ Đá: 120 g

Cold Brew Base
├─ Cà phê hạt
└─ Nước lọc
```

Lý do tách bán thành phẩm:

- có thể làm trước theo mẻ;
- kiểm soát năng suất thực tế;
- dùng lại cho nhiều món;
- biết lượng tồn của phần đã sơ chế.

Không được hiểu rằng khi bán Cold Brew Cam, hệ thống phải luôn phân rã vô hạn mọi lớp công thức. Trong vận hành bán hàng, CafeChain áp dụng nguyên tắc tiêu hao **một tầng**: tiêu hao nguyên liệu trực tiếp hoặc bán thành phẩm phù hợp, không làm nổ toàn bộ cây công thức ở thời điểm bán.

## 5. Đơn vị đo và quy đổi

Mỗi nguyên liệu hoặc bán thành phẩm cần một **đơn vị cơ sở** để tồn kho có một tiếng nói thống nhất.

Ví dụ:

```text
Sữa tươi
Đơn vị cơ sở: ml

Công thức Latte M
180 ml sữa
```

Một dòng BOM có thể được nhập bằng đơn vị phù hợp với thao tác pha chế, nhưng trước khi ảnh hưởng đến tồn hoặc giá vốn, lượng đó phải hiểu được theo đơn vị cơ sở.

Nguyên tắc nghiệp vụ:

- chỉ dùng quy đổi vật lý có ý nghĩa;
- kg và g có thể liên hệ trong cùng loại khối lượng;
- l và ml có thể liên hệ trong cùng loại thể tích;
- thiếu quy đổi hoặc quy đổi không tương thích phải dừng để kiểm tra;
- không được lấy số lượng “thô” rồi giả định là đúng.

Ví dụ:

```text
1 kg cà phê
↓
1.000 g cà phê
```

Nhưng:

```text
1 chai siro
```

không tự nhiên luôn bằng một lượng ml cố định. Nội dung vật lý của chai phải đến từ quy cách đã được xác nhận.

## 6. Giá vốn: ba câu hỏi khác nhau

CafeChain phân biệt ba ý nghĩa giá vốn:

| Loại giá vốn | Câu hỏi được trả lời |
|---|---|
| Giá vốn ước tính | Nếu làm món hôm nay theo gói mua hiện hành thì định mức có giá bao nhiêu? |
| Giá vốn vận hành tại chi nhánh | Hàng thực tế đang có ở chi nhánh có giá vốn bao nhiêu? |
| Giá vốn lịch sử đơn bán | Tại thời điểm bán, đơn đó đã ghi nhận giá vốn bao nhiêu? |

Ba con số này có thể khác nhau và không nên gộp thành một.

Ví dụ:

```text
Gói cà phê mới: 500.000 đ / 5 kg
→ giá vốn ước tính: 100.000 đ / kg

Kho còn lô cũ giá khác
→ giá vốn vận hành có thể khác

Đơn bán tuần trước
→ giữ giá vốn lịch sử của tuần trước
```

Để tính giá vốn ước tính từ gói mua:

```text
Giá gói
÷
Lượng vật lý trong gói sau khi quy về đơn vị cơ sở
=
Giá vốn một đơn vị cơ sở
```

Ví dụ:

```text
500.000 đ / 5 kg
= 100.000 đ / kg
```

Nếu thiếu giá, thiếu lượng trong gói hoặc thiếu quy đổi hợp lệ, kết quả phải được xem là **chưa đủ dữ liệu**, không phải giá vốn bằng 0.

## 7. Phiên bản công thức và lịch sử

Công thức thay đổi vì giá nguyên liệu, khẩu vị, size hoặc cách vận hành thay đổi. Vì vậy cần nhìn công thức như một **phiên bản**.

```text
Công thức Latte M phiên bản cũ
↓
Được thay thế
↓
Công thức Latte M phiên bản mới
```

Lý do giữ phiên bản:

- đơn hàng cũ vẫn phải giải thích được đã bán theo định mức nào;
- mẻ sản xuất phải truy về công thức đã dùng;
- không nên viết đè lịch sử chỉ vì hôm nay thay đổi định lượng.

Điều cần phân biệt:

- **công thức** có thể đổi theo phiên bản;
- **bán thành phẩm** cần danh tính ổn định để tồn kho không bị đổi tên hoặc đổi nghĩa khi công thức thay đổi.

Ví dụ:

```text
Cold Brew Base
```

vẫn là một đối tượng tồn kho vận hành, ngay cả khi tỷ lệ cà phê:nước trong công thức mới được điều chỉnh.

Chưa có đủ bằng chứng trong hệ thống hiện tại để khẳng định mọi thay đổi công thức đều có quy trình phê duyệt nghiệp vụ riêng trước khi có hiệu lực. Khi vận hành, nên xem thay đổi định mức là quyết định cần kiểm soát vì nó ảnh hưởng đến chất lượng và giá vốn.

## 8. BOM đi vào sản xuất như thế nào?

Khi một bán thành phẩm cần được làm:

```text
Nhu cầu bán thành phẩm
↓
Lập kế hoạch mẻ
↓
Chuẩn bị đầu vào
↓
Thực hiện sản xuất
↓
Kiểm nhận đầu ra
↓
Bán thành phẩm được chấp nhận vào tồn kho
```

Điểm quan trọng:

- kế hoạch sản xuất không làm tồn kho tăng;
- đầu vào dự kiến không phải lượng tiêu hao thực tế;
- lượng đầu vào thực tế mới là cơ sở vận hành và giá vốn;
- chỉ lượng đầu ra **được chấp nhận** mới làm tăng tồn kho;
- hàng lỗi, hao hụt hoặc bị từ chối không được coi như đầu ra đạt chuẩn.

Ví dụ:

```text
Kế hoạch: 10 lít Cold Brew Base
Thực tế làm được: 9,4 lít đạt chuẩn
0,6 lít không đạt

→ chỉ 9,4 lít vào tồn kho
```

## 9. BOM trong POS

Khi khách thanh toán đơn:

```text
Món + size + topping được chọn
↓
Chốt định mức tại thời điểm bán
↓
Ghi nhận đơn bán
↓
Tiêu hao kho theo định mức đã chốt
```

Việc “chốt” rất quan trọng: nếu công thức thay đổi sau đó, đơn đã bán vẫn phải giữ ngữ cảnh lúc bán.

Topping cũng cần được xem theo cách tương tự:

- topping có thể làm tăng giá bán;
- topping có thể có định mức riêng;
- topping không được suy diễn chỉ từ tên hiển thị.

## 10. Các ngoại lệ cần giải thích khi bảo vệ

### Thiếu quy đổi đơn vị

Không thể khẳng định lượng tiêu hao hoặc giá vốn đúng. Cách xử lý đúng là yêu cầu bổ sung dữ liệu, không tự đoán.

### Công thức lồng nhau

Cần phân biệt “cây cấu tạo để hiểu món” với “cách ghi giảm tồn lúc bán”. CafeChain dùng cây lồng nhau để đọc, tính và truy vết; tiêu hao POS được giữ ở một tầng để tránh trừ kho lặp.

### Năng suất sản xuất thấp

Làm ít hơn dự kiến không tự biến mất. Phần thiếu còn là nhu cầu cần theo dõi hoặc cần quyết định vận hành tiếp theo.

### Giá gói mua bằng 0 hoặc thiếu

Không được trình bày như giá vốn hoàn chỉnh bằng 0.

## 11. Cách kể ngắn trong buổi bảo vệ

> BOM là định mức chuẩn nối món bán với nguyên liệu và bán thành phẩm. Mỗi thành phần phải có lượng và đơn vị hiểu được theo đơn vị tồn kho. BOM giúp chuẩn hóa pha chế, ước tính giá vốn và hỗ trợ tiêu hao khi bán. Công thức có phiên bản để bảo toàn lịch sử, còn bán thành phẩm có danh tính ổn định để tồn kho không bị thay đổi ý nghĩa khi công thức được cải tiến.

