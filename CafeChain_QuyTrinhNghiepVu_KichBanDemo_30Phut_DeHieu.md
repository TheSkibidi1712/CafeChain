# CAFECHAIN – QUY TRÌNH NGHIỆP VỤ VÀ KỊCH BẢN DEMO 30 PHÚT

> Tài liệu này được viết lại theo hướng **dễ nói, dễ nhớ, hạn chế thuật ngữ kỹ thuật và tiếng Anh**. Các tên bắt buộc như **POS, BOM, AI** vẫn được giữ vì đó là tên các phần của dự án.
>
> Khi trên giao diện có tên tiếng Anh, người demo có thể chỉ đúng mục trên màn hình nhưng khi thuyết trình nên giải thích bằng tiếng Việt như trong tài liệu này.

---

# 1. QUY TRÌNH NGHIỆP VỤ TỔNG THỂ

CafeChain quản lý hoạt động của chuỗi cửa hàng theo một luồng liên kết từ bán hàng, định mức nguyên liệu, tồn kho, mua hàng, nhận hàng cho tới phân tích dữ liệu.

```text
Khách mua hàng tại POS
        ↓
Hệ thống ghi nhận đơn hàng và thanh toán
        ↓
BOM cho biết món vừa bán cần những nguyên liệu nào
        ↓
Hệ thống ghi nhận lượng nguyên liệu đã sử dụng
        ↓
Theo dõi lượng hàng còn trong kho
        ↓
Nếu thiếu hàng → tạo yêu cầu bổ sung
        ↓
Chọn cách bổ sung
   ├─ Chuyển hàng từ nơi khác
   ├─ Tự sản xuất
   └─ Mua từ nhà cung cấp
            ↓
       Tạo đề nghị mua
            ↓
       Tạo đơn đặt hàng
            ↓
       Nhà cung cấp giao hàng
            ↓
       Kiểm tra số lượng đạt / không đạt
            ↓
       Xác nhận nhận hàng
            ↓
Chỉ số lượng đạt yêu cầu mới được cộng vào kho
            ↓
Dữ liệu được dùng cho báo cáo và AI phân tích
            ↓
Kiểm thử giúp kiểm tra các quy tắc quan trọng của hệ thống
```

## Những điểm cả nhóm phải nhớ

- **POS** là nơi ghi nhận việc bán hàng và thanh toán.
- **BOM** cho biết một món cần dùng những nguyên liệu nào và bao nhiêu.
- **Yêu cầu bổ sung hàng** chỉ thể hiện nhu cầu, chưa làm tăng tồn kho.
- **Đề nghị mua** là bước chuẩn bị trước khi đặt hàng.
- **Đơn đặt hàng** thể hiện doanh nghiệp đã đặt mua, nhưng hàng vẫn chưa được tính vào kho.
- Chỉ khi **xác nhận phiếu nhận hàng**, số lượng thực tế đạt yêu cầu mới được cộng vào kho.
- **Quy cách mua** cho biết nhà cung cấp bán theo gói, chai, thùng hay đơn vị nào và mỗi gói chứa bao nhiêu.
- **AI** chỉ phân tích dữ liệu mà người dùng được phép xem, không tự sửa dữ liệu hoặc tự đặt hàng.
- **Tester** kiểm tra lại các quy tắc quan trọng để hạn chế lỗi khi hệ thống thay đổi.

---

# 2. QUY TRÌNH NGHIỆP VỤ THEO THỨ TỰ DEMO

# 2.1. PHÚC – POS

## Mục đích

POS dùng để bán hàng tại cửa hàng. Nhân viên mở ca, chọn món, nhận thanh toán và hệ thống ghi nhận giao dịch.

## Luồng nghiệp vụ

```text
Chọn quầy/máy bán hàng
        ↓
Mở ca làm việc
        ↓
Nhập tiền đầu ca
        ↓
Chọn món
        ↓
Chọn size và món thêm nếu có
        ↓
Hệ thống kiểm tra giá và thông tin món
        ↓
Chọn phương thức thanh toán
        ↓
Hoàn tất đơn hàng
        ↓
Lưu thông tin giao dịch
        ↓
Ghi nhận lượng nguyên liệu đã sử dụng theo BOM
```

## Điểm cần nói rõ

- Nhân viên chỉ chọn món; hệ thống kiểm tra lại giá trước khi tạo đơn hàng.
- Hệ thống lưu thông tin của món và giá tại thời điểm khách mua để lịch sử giao dịch không bị thay đổi nếu sau này giá hoặc công thức thay đổi.
- Nếu thao tác gửi lại do mạng chậm hoặc người dùng bấm lại, hệ thống phải hạn chế việc tạo trùng đơn hàng hoặc trừ kho hai lần.

## Đóng ca

```text
Tiền theo hệ thống
        ↓
Nhân viên nhập tiền thực tế
        ↓
Hệ thống tính chênh lệch
        ↓
Xác nhận đóng ca
```

## Phúc nên demo

1. Vào POS.
2. Chọn quầy/máy bán hàng.
3. Mở ca và nhập tiền đầu ca.
4. Chọn một món đã chuẩn bị sẵn.
5. Chọn size.
6. Chọn món thêm nếu có và đã thử trước.
7. Thanh toán bằng tiền mặt.
8. Mở lại đơn hàng vừa tạo để cho thấy giao dịch thành công.
9. Nếu còn thời gian, chỉ nhanh thông tin tiền trong ca.

## Câu chuyển sang Danh

> “POS đã ghi nhận món vừa bán. Tiếp theo hệ thống cần biết món đó sử dụng những nguyên liệu nào và số lượng bao nhiêu. Phần này được quản lý bằng BOM và Danh sẽ trình bày tiếp.”

---

# 2.2. DANH – BOM

## BOM là gì?

BOM là **định mức nguyên liệu của một món**.

Nó trả lời ba câu hỏi:

1. Món này cần nguyên liệu nào?
2. Cần bao nhiêu?
3. Dùng đơn vị gì?

Ví dụ:

```text
Latte size M
 ├─ Cà phê: số lượng theo công thức
 ├─ Sữa: số lượng theo công thức
 └─ Thành phần khác nếu có
```

Khi demo, chỉ dùng đúng số liệu đang có trong hệ thống, không tự đặt số.

## BOM dùng để làm gì?

- Chuẩn hóa công thức pha chế.
- Biết một món bán ra cần sử dụng bao nhiêu nguyên liệu.
- Hỗ trợ tính chi phí nguyên liệu ước tính.
- Hỗ trợ quản lý các nguyên liệu đã được sơ chế hoặc chuẩn bị trước.

## Luồng nghiệp vụ BOM

```text
Chọn món
    ↓
Mở công thức
    ↓
Thêm các thành phần
    ↓
Nhập số lượng
    ↓
Chọn đơn vị tính
    ↓
Lưu công thức
    ↓
POS dùng công thức để xác định lượng nguyên liệu cần sử dụng
```

## Nguyên liệu đã chuẩn bị trước

Một số thành phần có thể được làm sẵn trước khi bán.

Ví dụ, nếu một loại sốt đã được sản xuất trước thì nguyên liệu để làm sốt đã được ghi nhận ở bước sản xuất. Khi bán món, hệ thống chỉ ghi nhận lượng sốt đã dùng theo cách hệ thống đã thiết kế, tránh việc trừ nguyên liệu hai lần.

## Chi phí nguyên liệu

Nếu hệ thống đủ dữ liệu giá và đơn vị, BOM có thể hỗ trợ ước tính chi phí nguyên liệu.

Nếu thiếu giá hoặc thiếu thông tin quy đổi thì phải nói:

> “Hệ thống chưa đủ dữ liệu để tính chính xác.”

Không được nói chi phí bằng 0 nếu thực tế chỉ là thiếu dữ liệu.

## Danh nên demo

1. Vào phần **Công thức & BOM**.
2. Mở đúng món Phúc vừa bán.
3. Chỉ từng nguyên liệu trong công thức.
4. Chỉ số lượng và đơn vị tính.
5. Nếu dữ liệu đầy đủ, mở phần ước tính chi phí nguyên liệu.
6. Giải thích ngắn gọn mối liên hệ giữa món bán ra và lượng nguyên liệu cần dùng.

## Câu chuyển sang Hưng

> “BOM cho biết một món bán ra làm giảm những nguyên liệu nào. Khi lượng hàng trong kho giảm xuống, cửa hàng sẽ phát sinh nhu cầu bổ sung. Hưng sẽ trình bày phần kho và chuỗi cung ứng.”

---

# 2.3. HƯNG – KHO VÀ CHUỖI CUNG ỨNG

## Mục đích

Phần này theo dõi lượng hàng trong kho và xử lý khi cửa hàng cần bổ sung nguyên liệu.

## Luồng bổ sung hàng

```text
Kho thiếu hoặc sắp thiếu hàng
        ↓
Tạo yêu cầu bổ sung hàng
        ↓
Xác định cách bổ sung
   ├─ Chuyển từ nơi khác
   ├─ Tự sản xuất
   └─ Mua ngoài
```

Yêu cầu bổ sung hàng cho biết:

- Cửa hàng cần mặt hàng nào.
- Cần bao nhiêu.
- Khi nào cần.
- Lý do cần bổ sung.

**Tạo yêu cầu bổ sung không làm tăng tồn kho.**

## Nếu chọn mua từ nhà cung cấp

```text
Yêu cầu bổ sung
        ↓
Đề nghị mua
        ↓
Xem xét và xử lý
        ↓
Đơn đặt hàng
        ↓
Nhà cung cấp giao hàng
        ↓
Lập phiếu nhận hàng
        ↓
Kiểm tra số lượng giao
        ↓
Xác định số lượng đạt và không đạt
        ↓
Xác nhận nhận hàng
        ↓
Chỉ số lượng đạt mới được cộng vào kho
```

## Ý nghĩa từng bước

| Bước | Ý nghĩa | Tồn kho tăng chưa? |
|---|---|---:|
| Yêu cầu bổ sung | Cửa hàng đang cần hàng | Chưa |
| Đề nghị mua | Chuẩn bị việc mua hàng | Chưa |
| Đơn đặt hàng | Đã đặt hàng với nhà cung cấp | Chưa |
| Phiếu nhận hàng chưa xác nhận | Đang ghi nhận hàng giao tới | Chưa |
| Phiếu nhận hàng đã xác nhận | Hàng đã được kiểm tra và chấp nhận | Có, nhưng chỉ cộng số lượng đạt |

## Ví dụ dễ nhớ

```text
Đặt mua:       10 kg
Nhà cung cấp giao: 10 kg
Đạt yêu cầu:    9 kg
Không đạt:      1 kg

=> Kho chỉ tăng 9 kg
```

## Vì sao đơn đặt hàng chưa làm tăng tồn kho?

Vì đơn đặt hàng mới thể hiện doanh nghiệp **đã đặt mua**. Nó chưa chứng minh hàng đã thực sự giao đến và đạt yêu cầu.

Chỉ khi nhân viên kiểm tra hàng và xác nhận số lượng đạt thì lượng đó mới được tính vào kho.

## Hưng nên demo

1. Mở một mặt hàng trong kho.
2. Mở một yêu cầu bổ sung hàng đã chuẩn bị.
3. Chỉ số lượng, lý do và trạng thái.
4. Mở đề nghị mua liên quan.
5. Mở đơn đặt hàng.
6. Mở phiếu nhận hàng có số lượng giao, đạt và không đạt.
7. Nếu đã thử trước và an toàn, xác nhận phiếu nhận hàng.
8. Mở lại kho để cho thấy chỉ lượng đạt được cộng vào.

## Câu chuyển sang Khôi

> “Để mua đúng số lượng, hệ thống còn phải biết nhà cung cấp bán nguyên liệu theo quy cách nào, một gói chứa bao nhiêu và đơn vị tính được quy đổi ra sao. Khôi sẽ trình bày phần này.”

---

# 2.4. KHÔI – QUY CÁCH MUA, NHÀ CUNG CẤP VÀ QUY ĐỔI ĐƠN VỊ

## Nhà cung cấp cần quản lý những gì?

Không chỉ có tên, địa chỉ hoặc số điện thoại. Với từng nguyên liệu, hệ thống còn cần biết:

- Nhà cung cấp bán nguyên liệu nào.
- Một gói/chai/thùng chứa bao nhiêu.
- Bên trong được tính theo kg, g, lít, ml hoặc đơn vị phù hợp.
- Giá một gói.
- Số lượng mua tối thiểu.
- Thời gian giao hàng dự kiến.
- Có phải nhà cung cấp chính hay không.
- Có cho phép mua lẻ hay không.

## Ví dụ quy cách mua

```text
Nhà cung cấp bán cà phê:
1 gói = 5 kg
Giá 1 gói = 500.000 đồng
Cửa hàng cần 10 kg

=> Cần mua 2 gói
```

Nếu nhà cung cấp yêu cầu tối thiểu nhiều hơn thì hệ thống phải tuân theo mức tối thiểu đó.

## Quy đổi đơn vị

Các quy đổi đơn giản:

```text
1 kg = 1000 g
1 lít = 1000 ml
```

Mục đích là đưa các số lượng về cùng một đơn vị để hệ thống tính đúng lượng hàng, chi phí và tồn kho.

## Vì sao không thể nói “1 chai luôn bằng 500 ml”?

Vì mỗi sản phẩm có thể có dung tích khác nhau:

- Chai 500 ml.
- Chai 750 ml.
- Chai 1 lít.

Vì vậy hệ thống phải lưu dung tích thực tế của từng quy cách mua. Không được dùng một con số chung cho mọi loại chai.

## Khi thiếu thông tin quy đổi

Nếu hệ thống không biết cách đổi giữa hai đơn vị thì phải báo thiếu dữ liệu.

Không được tự lấy nguyên số lượng rồi tiếp tục tính, vì có thể làm sai số lượng mua và tồn kho.

## Khôi nên demo

1. Vào phần **Nhà cung cấp**.
2. Chọn một nhà cung cấp đã chuẩn bị.
3. Mở phần quy cách mua nguyên liệu.
4. Chỉ:
   - nguyên liệu;
   - lượng trong một gói;
   - đơn vị;
   - giá;
   - số lượng mua tối thiểu;
   - thời gian giao hàng.
5. Nếu có mua lẻ thì chỉ thêm phần đó.
6. Sang phần **Đơn vị và quy đổi**.
7. Demo một ví dụ dễ hiểu như kg → g hoặc lít → ml.
8. Kết nối lại với phần kho: khi nhận hàng, hệ thống cần đưa số lượng về đơn vị phù hợp để cộng tồn kho chính xác.

## Câu chuyển sang Thế Anh

> “Sau khi bán hàng, quản lý công thức, kho và mua hàng, hệ thống đã có một lượng dữ liệu vận hành tương đối đầy đủ. Thế Anh sẽ demo cách AI sử dụng các dữ liệu đó để hỗ trợ phân tích.”

---

# 2.5. THẾ ANH – AI

## Vai trò của AI

AI trong CafeChain dùng để **hỗ trợ phân tích dữ liệu**, không thay người dùng thực hiện các nghiệp vụ quan trọng.

Luồng đơn giản:

```text
Người dùng chọn khoảng thời gian và cửa hàng
        ↓
Nhập câu hỏi
        ↓
Hệ thống lấy dữ liệu người dùng được phép xem
        ↓
AI phân tích dữ liệu đó
        ↓
Trả kết quả bằng nội dung, bảng hoặc biểu đồ
```

## AI được làm gì?

- Phân tích dữ liệu bán hàng.
- Tóm tắt tình hình kinh doanh.
- Hỗ trợ tìm sản phẩm bán chạy.
- Hỗ trợ nhận biết một số xu hướng hoặc điểm bất thường nếu dữ liệu đủ.
- Trình bày kết quả bằng câu trả lời, bảng hoặc biểu đồ.

## AI không được làm gì?

- Không tự sửa dữ liệu trong hệ thống.
- Không tự tạo đơn đặt hàng.
- Không tự xem dữ liệu ngoài phạm vi quyền của người dùng.
- Không tự bịa số khi dữ liệu không có.

Nếu dữ liệu không đủ, hệ thống phải thể hiện rằng kết quả chỉ trả lời được một phần hoặc chưa đủ cơ sở để kết luận.

## Thế Anh nên demo

1. Mở Dashboard.
2. Chọn khoảng thời gian đã có dữ liệu.
3. Chọn cửa hàng phù hợp.
4. Áp dụng bộ lọc.
5. Mở phần **Hỏi AI**.
6. Nhập câu hỏi đã thử trước, ví dụ:

> “Top 10 sản phẩm bán chạy nhất trong kỳ là gì?”

7. Bấm phân tích.
8. Chỉ:
   - câu trả lời chính;
   - số liệu đi kèm;
   - bảng hoặc biểu đồ;
   - thông báo hạn chế nếu dữ liệu chưa đủ.

## Câu chuyển sang Tester

> “Phần AI hoàn thành luồng demo chức năng. Cuối cùng, Phong và Khang sẽ chạy một số kiểm thử để chứng minh các quy tắc quan trọng của hệ thống vẫn được kiểm soát.”

---

# 3. PHONG VÀ KHANG – TESTER

# 3.1. Mục đích

Phần kiểm thử không cần chạy tất cả các bài kiểm tra. Nhóm chỉ nên chọn những bài liên quan trực tiếp đến các nghiệp vụ vừa demo.

Các nội dung chính:

```text
POS → kiểm tra tiền mặt và tính tiền thừa
Quy đổi đơn vị → kiểm tra kg, g và trường hợp thiếu thông tin quy đổi
Kho → kiểm tra hàng không đạt không được cộng vào kho
Lặp thao tác → không được làm dữ liệu bị cộng hoặc xử lý hai lần
```

# 3.2. PHONG – Kiểm thử POS và đơn vị tính

Trước buổi demo, cần chắc chắn dự án đã chạy được trên máy trình bày.

Lệnh chạy:

```bash
dotnet test CafeChain.Tests/CafeChain.Tests.csproj --no-build --nologo --filter "FullyQualifiedName~POSCashTenderHardeningTests|FullyQualifiedName~UnitConversionServiceTests"
```

## Phong nên giải thích

> “Ở phần này em kiểm tra hai nhóm quy tắc. Thứ nhất là POS phải xử lý đúng tiền khách đưa và tiền thừa. Thứ hai là việc quy đổi đơn vị phải chính xác; nếu hệ thống không có thông tin quy đổi thì phải báo lỗi thay vì tự tính tiếp.”

Không cần đọc toàn bộ tên các bài kiểm tra nếu hội đồng không hỏi.

# 3.3. KHANG – Kiểm thử nhận hàng

Lệnh chạy:

```bash
dotnet test CafeChain.Tests/CafeChain.Tests.csproj --no-build --nologo --filter "FullyQualifiedName~PurchaseOrderPartialReceiptIssue178Tests.RejectedReceipt_DoesNotFulfillRestock_AndReplayDoesNotDuplicatePoPosting"
```

## Khang nên giải thích

> “Bài kiểm tra này tập trung vào quy tắc quan trọng của phần kho. Hàng không đạt yêu cầu không được tính là hàng đã nhập, và nếu thao tác bị gửi lại thì hệ thống không được cộng kho thêm một lần nữa.”

## Câu kết thúc của Khang

> “Qua toàn bộ phần demo, CafeChain liên kết từ bán hàng tới công thức, tồn kho, mua hàng, nhận hàng và phân tích dữ liệu. Mỗi bước đều có nhiệm vụ riêng và việc kiểm thử giúp đảm bảo các quy tắc đó vẫn hoạt động đúng khi hệ thống được thay đổi hoặc phát triển thêm.”

---

# 4. 10 CÂU HỎI VỀ QUY TRÌNH NGHIỆP VỤ VÀ GỢI Ý TRẢ LỜI

## Câu 1. POS của CafeChain có vai trò gì?

**Trả lời:** POS dùng để ghi nhận việc bán hàng. Hệ thống lưu món khách mua, số tiền, phương thức thanh toán, ca làm việc và từ món đã bán có thể xác định lượng nguyên liệu cần sử dụng theo BOM.

---

## Câu 2. Tại sao phải mở ca trước khi bán hàng?

**Trả lời:** mở ca giúp hệ thống xác định ai đang vận hành quầy và số tiền đầu ca. Khi kết thúc ca, hệ thống có thể so sánh tiền theo giao dịch với tiền thực tế để kiểm tra chênh lệch.

---

## Câu 3. BOM khác với menu và tồn kho như thế nào?

**Trả lời:** menu là danh sách món khách có thể mua. BOM là công thức và định mức nguyên liệu của món. Tồn kho là lượng nguyên liệu thực tế đang có. Khi bán món, hệ thống dựa vào BOM để biết cần ghi nhận sử dụng những nguyên liệu nào.

---

## Câu 4. Vì sao một nguyên liệu đã được chế biến sẵn không nên bị trừ nguyên liệu gốc thêm một lần khi bán?

**Trả lời:** vì nguyên liệu gốc đã được ghi nhận sử dụng ở lúc sản xuất phần chế biến sẵn. Nếu khi bán lại trừ tiếp nguyên liệu gốc thì có thể làm số liệu kho bị trừ hai lần.

---

## Câu 5. Nếu công thức hoặc giá món thay đổi sau này thì đơn hàng cũ có thay đổi không?

**Trả lời:** không. Thông tin quan trọng của giao dịch được lưu theo thời điểm khách mua, vì vậy lịch sử đơn hàng vẫn phản ánh đúng giá và thông tin lúc giao dịch phát sinh.

---

## Câu 6. Yêu cầu bổ sung, đề nghị mua, đơn đặt hàng và phiếu nhận hàng khác nhau như thế nào?

**Trả lời:** yêu cầu bổ sung cho biết cửa hàng đang cần hàng. Đề nghị mua là bước chuẩn bị việc mua. Đơn đặt hàng là việc doanh nghiệp chính thức đặt mua với nhà cung cấp. Phiếu nhận hàng ghi nhận hàng thực tế được giao tới. Chỉ lượng hàng đạt yêu cầu sau khi xác nhận nhận hàng mới được cộng vào kho.

---

## Câu 7. Đặt mua 10 kg nhưng chỉ có 9 kg đạt yêu cầu thì tồn kho tăng bao nhiêu?

**Trả lời:** chỉ tăng 9 kg. 1 kg không đạt yêu cầu không được cộng vào kho.

---

## Câu 8. Tại sao không thể quy định tất cả “1 chai = 500 ml”?

**Trả lời:** vì mỗi loại hàng có thể dùng chai dung tích khác nhau. Có chai 500 ml, 750 ml hoặc 1 lít. Hệ thống phải lưu đúng dung tích của từng quy cách mua thay vì dùng một con số chung.

---

## Câu 9. Nếu hệ thống thiếu thông tin quy đổi đơn vị thì xử lý thế nào?

**Trả lời:** hệ thống phải báo thiếu dữ liệu để người dùng bổ sung. Không được tự coi hai đơn vị là giống nhau vì có thể làm sai số lượng mua, chi phí và tồn kho.

---

## Câu 10. Làm thế nào để hạn chế AI trả lời sai hoặc tự ý thay đổi dữ liệu?

**Trả lời:** hệ thống chỉ đưa cho AI dữ liệu mà người dùng được phép xem. AI dùng dữ liệu đó để phân tích và trả lời, nhưng không có quyền tự sửa dữ liệu hoặc tự tạo đơn đặt hàng. Khi dữ liệu chưa đủ thì phải thể hiện rõ là chưa đủ cơ sở để kết luận.

---

# 5. KỊCH BẢN THUYẾT TRÌNH ĐÚNG 30 PHÚT

## Phân bổ thời gian

| Thời gian | Người | Nội dung | Thời lượng |
|---|---|---|---:|
| 00:00–05:30 | **Phúc** | POS | 5 phút 30 giây |
| 05:30–09:30 | **Danh** | BOM | 4 phút |
| 09:30–14:30 | **Hưng** | Kho & chuỗi cung ứng | 5 phút |
| 14:30–18:30 | **Khôi** | Quy cách mua + nhà cung cấp + đơn vị | 4 phút |
| 18:30–23:00 | **Thế Anh** | AI | 4 phút 30 giây |
| 23:00–26:30 | **Phong** | Kiểm thử POS + đơn vị | 3 phút 30 giây |
| 26:30–30:00 | **Khang** | Kiểm thử nhận hàng + kết luận | 3 phút 30 giây |
| | | **Tổng cộng** | **30 phút** |

---

# 5.1. PHÚC – POS – 00:00 → 05:30

## 00:00–00:35 – Giới thiệu

> “Nhóm em sẽ demo CafeChain theo một quy trình xuyên suốt từ bán hàng, công thức, kho, mua hàng, nhà cung cấp cho tới AI và kiểm thử. Đầu tiên em bắt đầu tại POS, là nơi phát sinh giao dịch bán hàng.”

## 00:35–01:40 – Mở ca

**Thao tác:**

1. Mở POS.
2. Chọn quầy/máy bán hàng.
3. Nhập tiền đầu ca.
4. Xác nhận mở ca.

**Lời nói:**

> “Khi mở ca, hệ thống ghi nhận người đang vận hành và số tiền đầu ca. Đây là cơ sở để cuối ca kiểm tra tiền thực tế có khớp với các giao dịch hay không.”

## 01:40–03:40 – Bán hàng

**Thao tác:**

1. Chọn món.
2. Chọn size.
3. Chọn món thêm nếu có.
4. Chọn thanh toán tiền mặt.
5. Hoàn tất đơn hàng.

**Lời nói:**

> “Nhân viên chọn món trên màn hình, nhưng hệ thống sẽ kiểm tra lại thông tin và giá trước khi tạo đơn hàng. Sau khi thanh toán thành công, giao dịch được lưu lại và lượng nguyên liệu sử dụng sẽ được xác định dựa trên BOM.”

## 03:40–04:40 – Mở lại đơn hàng

> “Thông tin quan trọng của đơn hàng được giữ theo thời điểm bán. Vì vậy nếu sau này giá hoặc công thức thay đổi thì giao dịch cũ vẫn giữ đúng thông tin ban đầu.”

## 04:40–05:05 – Thông tin ca

> “Cuối ca, hệ thống có thể so sánh số tiền theo các giao dịch với số tiền thực tế để xác định chênh lệch.”

## 05:05–05:30 – Chuyển phần

> “POS cho biết món nào vừa được bán. Để biết món đó sử dụng những nguyên liệu nào và bao nhiêu thì cần BOM. Danh sẽ trình bày tiếp phần này.”

---

# 5.2. DANH – BOM – 05:30 → 09:30

## 05:30–06:10 – Giới thiệu BOM

> “BOM là định mức nguyên liệu của món. Nó cho biết món cần thành phần nào, số lượng bao nhiêu và dùng đơn vị gì.”

## 06:10–07:40 – Mở công thức

**Thao tác:**

1. Vào Công thức & BOM.
2. Mở món Phúc vừa bán.
3. Chỉ các nguyên liệu.
4. Chỉ số lượng và đơn vị.

**Lời nói:**

> “Đây là các thành phần hệ thống dùng để xác định lượng nguyên liệu cần ghi nhận khi món được bán.”

## 07:40–08:30 – Thành phần chuẩn bị trước

> “Một số thành phần có thể được làm sẵn. Khi sản xuất phần này, nguyên liệu gốc đã được ghi nhận sử dụng. Vì vậy khi bán món, hệ thống phải tránh việc trừ lại cùng nguyên liệu một lần nữa.”

## 08:30–09:05 – Chi phí nguyên liệu

> “Nếu đầy đủ dữ liệu giá và đơn vị, hệ thống có thể ước tính chi phí nguyên liệu của công thức. Nếu thiếu dữ liệu thì hệ thống phải thể hiện là chưa đủ thông tin thay vì tự coi chi phí bằng 0.”

## 09:05–09:30 – Chuyển phần

> “Khi món được bán và nguyên liệu được sử dụng, lượng hàng trong kho sẽ giảm. Hưng sẽ trình bày quy trình bổ sung hàng và nhận hàng vào kho.”

---

# 5.3. HƯNG – KHO & CHUỖI CUNG ỨNG – 09:30 → 14:30

## 09:30–10:20 – Kho và yêu cầu bổ sung

**Thao tác:** mở tồn kho và một yêu cầu bổ sung đã chuẩn bị.

> “Khi cửa hàng thiếu hoặc sắp thiếu hàng, hệ thống tạo yêu cầu bổ sung. Yêu cầu này chỉ thể hiện nhu cầu, chưa làm tăng lượng hàng trong kho.”

## 10:20–11:05 – Chọn cách bổ sung

> “Tùy tình huống, doanh nghiệp có thể chuyển hàng từ nơi khác, tự sản xuất hoặc mua từ nhà cung cấp. Trong phần demo này nhóm đi theo nhánh mua ngoài.”

## 11:05–12:10 – Đề nghị mua và đơn đặt hàng

**Thao tác:** mở đề nghị mua rồi mở đơn đặt hàng.

> “Đề nghị mua là bước chuẩn bị. Sau khi thống nhất nhà cung cấp và số lượng, hệ thống tạo đơn đặt hàng. Tuy nhiên đơn đặt hàng vẫn chưa làm tăng tồn kho vì hàng chưa chắc đã được giao và đạt yêu cầu.”

## 12:10–13:40 – Nhận hàng

**Thao tác:** mở phiếu nhận hàng.

> “Khi nhà cung cấp giao hàng, nhân viên kiểm tra số lượng thực tế. Phần đạt yêu cầu được chấp nhận, phần không đạt bị loại. Chỉ sau khi xác nhận phiếu nhận hàng thì số lượng đạt mới được cộng vào kho.”

Ví dụ:

> “Nếu đặt 10 kg nhưng chỉ có 9 kg đạt, kho chỉ tăng 9 kg.”

## 13:40–14:10 – Tránh cộng trùng

> “Nếu thao tác bị gửi lại do mạng hoặc người dùng bấm lại, hệ thống phải tránh cộng cùng một lượng hàng vào kho hai lần.”

## 14:10–14:30 – Chuyển phần

> “Để mua đúng, hệ thống còn phải biết mỗi nhà cung cấp bán nguyên liệu theo quy cách nào và cách đổi đơn vị ra sao. Khôi sẽ trình bày tiếp.”

---

# 5.4. KHÔI – NHÀ CUNG CẤP, QUY CÁCH MUA VÀ ĐƠN VỊ – 14:30 → 18:30

## 14:30–15:30 – Nhà cung cấp

**Thao tác:** vào Nhà cung cấp → chọn một nhà cung cấp → mở quy cách mua nguyên liệu.

> “Với mỗi nguyên liệu, hệ thống cần biết nhà cung cấp bán theo gói, chai hay thùng; mỗi gói chứa bao nhiêu; giá bao nhiêu; mức mua tối thiểu và thời gian giao hàng.”

## 15:30–16:30 – Ví dụ quy cách mua

> “Ví dụ một gói cà phê chứa 5 kg và cửa hàng cần 10 kg thì về cơ bản cần 2 gói. Nếu nhà cung cấp có quy định số lượng mua tối thiểu thì hệ thống phải tuân theo điều kiện đó.”

## 16:30–17:30 – Quy đổi đơn vị

**Thao tác:** demo kg → g hoặc lít → ml.

> “Quy đổi đơn vị giúp hệ thống đưa các số lượng về cùng cách tính. Ví dụ 1 kg bằng 1000 g. Nếu thiếu thông tin quy đổi thì hệ thống phải báo thiếu dữ liệu, không được tự tính tiếp.”

## 17:30–18:05 – Gói/chai/thùng

> “Các đơn vị như chai hoặc thùng không có một dung tích cố định cho mọi sản phẩm. Vì vậy hệ thống cần lưu đúng dung tích của từng quy cách mua.”

## 18:05–18:30 – Chuyển phần

> “Từ bán hàng, công thức, kho và mua hàng, hệ thống đã tạo ra dữ liệu vận hành. Thế Anh sẽ dùng phần AI để phân tích các dữ liệu đó.”

---

# 5.5. THẾ ANH – AI – 18:30 → 23:00

## 18:30–19:10 – Giới thiệu

> “AI trong CafeChain được dùng để hỗ trợ phân tích dữ liệu. AI không tự sửa dữ liệu, không tự đặt hàng và chỉ sử dụng dữ liệu mà tài khoản hiện tại được phép xem.”

## 19:10–20:10 – Chọn dữ liệu

**Thao tác:**

1. Mở Dashboard.
2. Chọn khoảng thời gian.
3. Chọn cửa hàng.
4. Áp dụng bộ lọc.
5. Mở Hỏi AI.

> “Khoảng thời gian và cửa hàng được chọn sẽ xác định phạm vi dữ liệu dùng để phân tích.”

## 20:10–21:15 – Đặt câu hỏi

Nhập:

> “Top 10 sản phẩm bán chạy nhất trong kỳ là gì?”

Bấm phân tích.

## 21:15–22:20 – Giải thích kết quả

> “AI đưa ra kết luận dựa trên dữ liệu hệ thống cung cấp. Nhóm có thể đối chiếu nội dung trả lời với số liệu hoặc biểu đồ đi kèm.”

Chỉ câu trả lời, bảng hoặc biểu đồ.

## 22:20–22:40 – Khi dữ liệu chưa đủ

> “Nếu dữ liệu chưa đủ, hệ thống phải thể hiện rằng chưa đủ cơ sở để kết luận thay vì tự tạo ra số liệu.”

## 22:40–23:00 – Chuyển phần

> “Cuối cùng Phong và Khang sẽ chạy một số kiểm thử để kiểm tra các quy tắc quan trọng vừa trình bày.”

---

# 5.6. PHONG – TESTER – 23:00 → 26:30

## 23:00–23:30 – Giới thiệu

> “Trong thời gian demo, nhóm không chạy toàn bộ bài kiểm tra mà chỉ chọn các phần liên quan trực tiếp tới nghiệp vụ vừa trình bày.”

## 23:30–25:30 – Chạy kiểm thử

```bash
dotnet test CafeChain.Tests/CafeChain.Tests.csproj --no-build --nologo --filter "FullyQualifiedName~POSCashTenderHardeningTests|FullyQualifiedName~UnitConversionServiceTests"
```

## 25:30–26:20 – Giải thích

> “Các bài kiểm tra này kiểm tra việc xử lý tiền mặt, tính tiền thừa và quy đổi đơn vị. Nếu thiếu thông tin quy đổi, hệ thống phải dừng và báo lỗi thay vì tiếp tục với số liệu sai.”

## 26:20–26:30 – Chuyển phần

> “Khang sẽ kiểm tra tiếp quy tắc nhận hàng của phần kho.”

---

# 5.7. KHANG – TESTER VÀ KẾT LUẬN – 26:30 → 30:00

## 26:30–28:15 – Chạy kiểm thử nhận hàng

```bash
dotnet test CafeChain.Tests/CafeChain.Tests.csproj --no-build --nologo --filter "FullyQualifiedName~PurchaseOrderPartialReceiptIssue178Tests.RejectedReceipt_DoesNotFulfillRestock_AndReplayDoesNotDuplicatePoPosting"
```

## 28:15–29:05 – Giải thích

> “Bài kiểm tra này xác nhận rằng hàng không đạt yêu cầu không được tính vào lượng hàng đã nhập và việc gửi lại cùng một thao tác không được làm tồn kho tăng thêm lần nữa.”

## 29:05–29:40 – Tổng kết

> “Nhìn xuyên suốt, POS ghi nhận bán hàng, BOM xác định nguyên liệu cần dùng, kho theo dõi lượng hàng và nhu cầu bổ sung, phần mua hàng xử lý việc đặt và nhận hàng, nhà cung cấp quản lý quy cách mua, AI hỗ trợ phân tích dữ liệu và kiểm thử giúp kiểm tra các quy tắc quan trọng.”

## 29:40–30:00 – Kết thúc

> “Nhóm em kết thúc phần demo tại đây và sẵn sàng trả lời câu hỏi của thầy cô.”

---

# 6. CHECKLIST TRƯỚC KHI DEMO

## Phúc – POS

- [ ] Tài khoản đăng nhập được.
- [ ] Quầy/máy bán hàng hoạt động.
- [ ] Có món đang bán.
- [ ] Món có size và BOM.
- [ ] Có thể mở ca.
- [ ] Đã thử thanh toán tiền mặt.
- [ ] Có một đơn hàng cũ dự phòng nếu tạo đơn mới bị lỗi.

## Danh – BOM

- [ ] Biết chính xác món Phúc sẽ demo.
- [ ] Đã mở sẵn công thức của món đó.
- [ ] Các nguyên liệu, số lượng và đơn vị dễ nhìn.
- [ ] Nếu demo chi phí thì dữ liệu giá phải đầy đủ.
- [ ] Không sửa công thức trực tiếp nếu có thể ảnh hưởng phần sau.

## Hưng – Kho

- [ ] Có yêu cầu bổ sung hàng mẫu.
- [ ] Có đề nghị mua.
- [ ] Có đơn đặt hàng.
- [ ] Có phiếu nhận hàng thể hiện số lượng đạt và không đạt.
- [ ] Nếu xác nhận nhận hàng trực tiếp thì đã thử trước.
- [ ] Biết nơi xem lại lượng tồn sau khi xác nhận.

## Khôi – Nhà cung cấp và đơn vị

- [ ] Có nhà cung cấp đang hoạt động.
- [ ] Có quy cách mua của ít nhất một nguyên liệu.
- [ ] Có giá, lượng trong gói và đơn vị rõ ràng.
- [ ] Có ví dụ kg → g hoặc lít → ml đã thử trước.
- [ ] Không dùng ví dụ “1 chai = 500 ml” như quy tắc chung.

## Thế Anh – AI

- [ ] Chọn khoảng thời gian có dữ liệu.
- [ ] Chọn đúng cửa hàng.
- [ ] Câu hỏi AI đã thử trước.
- [ ] Biết vị trí câu trả lời và biểu đồ.
- [ ] Có ảnh chụp kết quả dự phòng nếu phần AI gặp lỗi mạng hoặc dịch vụ.

## Phong và Khang – Tester

- [ ] Máy demo có .NET.
- [ ] Dự án đã chạy kiểm thử thành công trước buổi bảo vệ.
- [ ] Hai lệnh kiểm thử đã được thử trước.
- [ ] Cửa sổ dòng lệnh có chữ đủ lớn để hội đồng nhìn thấy.
- [ ] Không cập nhật thư viện hoặc phần mềm ngay trước giờ demo.

---

# 7. NHỮNG ĐIỂM KHÔNG ĐƯỢC NÓI SAI

## POS

**Không nói:** “Mở màn hình POS là đã mở ca.”  
**Nên nói:** “Ca chỉ được ghi nhận khi người dùng xác nhận mở ca và nhập tiền đầu ca.”

**Không nói:** “Nhân viên tự quyết định giá rồi hệ thống chỉ lưu lại.”  
**Nên nói:** “Hệ thống kiểm tra lại giá và thông tin món trước khi tạo đơn hàng.”

---

## BOM

**Không nói:** “BOM là tồn kho.”  
**Nên nói:** “BOM là định mức nguyên liệu; tồn kho là lượng hàng thực tế đang có.”

**Không nói:** “Thiếu giá thì chi phí bằng 0.”  
**Nên nói:** “Thiếu dữ liệu thì chưa thể tính chính xác.”

---

## Kho và mua hàng

**Không nói:** “Tạo yêu cầu bổ sung là hàng đã vào kho.”  
**Nên nói:** “Yêu cầu bổ sung chỉ thể hiện nhu cầu.”

**Không nói:** “Tạo đơn đặt hàng là tồn kho tăng.”  
**Nên nói:** “Đơn đặt hàng chỉ thể hiện đã đặt mua.”

**Không nói:** “Nhà cung cấp giao bao nhiêu thì cộng kho bấy nhiêu.”  
**Nên nói:** “Chỉ lượng đạt yêu cầu sau khi xác nhận nhận hàng mới được cộng vào kho.”

---

## Nhà cung cấp và đơn vị

**Không nói:** “Một chai luôn bằng một số ml cố định.”  
**Nên nói:** “Dung tích phụ thuộc từng loại hàng và từng quy cách mua.”

**Không nói:** “Thiếu thông tin quy đổi thì cứ lấy nguyên số lượng để tính.”  
**Nên nói:** “Thiếu thông tin thì phải báo để bổ sung dữ liệu.”

---

## AI

**Không nói:** “AI tự truy cập toàn bộ dữ liệu và tự đặt hàng.”  
**Nên nói:** “AI chỉ phân tích dữ liệu được hệ thống cho phép và không tự thực hiện nghiệp vụ mua hàng.”

**Không nói:** “AI thiếu dữ liệu thì tự ước lượng.”  
**Nên nói:** “Nếu chưa đủ dữ liệu thì hệ thống phải thể hiện rõ là chưa đủ cơ sở kết luận.”

---

# 8. SƠ ĐỒ NHỚ NHANH CHO CẢ NHÓM

```text
PHÚC – POS
Khách mua gì? Thanh toán thế nào?
        ↓
DANH – BOM
Món đó cần những nguyên liệu nào? Bao nhiêu?
        ↓
HƯNG – KHO
Kho còn bao nhiêu? Có cần bổ sung không?
Yêu cầu bổ sung → Đề nghị mua → Đơn đặt hàng → Nhận hàng
        ↓
KHÔI – NHÀ CUNG CẤP & ĐƠN VỊ
Mua từ ai? Một gói bao nhiêu? Giá bao nhiêu? Đổi đơn vị thế nào?
        ↓
THẾ ANH – AI
Phân tích dữ liệu bán hàng và vận hành
        ↓
PHONG + KHANG – TESTER
Kiểm tra các quy tắc quan trọng của hệ thống
```

## Một câu để nhớ toàn dự án

> **“CafeChain bắt đầu từ việc bán hàng, dùng BOM để biết nguyên liệu cần sử dụng, theo dõi lượng hàng trong kho, bổ sung và nhận hàng khi cần, quản lý quy cách mua của nhà cung cấp, dùng AI để hỗ trợ phân tích dữ liệu và dùng kiểm thử để đảm bảo các quy tắc hoạt động đúng.”**
