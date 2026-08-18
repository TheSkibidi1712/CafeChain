# Kho và Chuỗi cung ứng nghiệp vụ tại CafeChain

## 1. Bức tranh lớn

Kho và chuỗi cung ứng giúp CafeChain trả lời:

> Chi nhánh đang cần gì, lấy hàng từ đâu, ai cam kết mua, ai xác nhận hàng thực tế, và vì sao tồn kho thay đổi?

Luồng chính:

```text
Nhu cầu tồn kho
↓
Yêu cầu bổ sung
↓
Chọn nguồn đáp ứng
↓
Đề nghị mua
↓
Đơn đặt hàng
↓
Giao hàng thực tế
↓
Phiếu nhận
↓
Xác nhận nhận hàng
↓
Tồn kho và truy vết
```

Mỗi chứng từ có ý nghĩa khác nhau. Không được coi tất cả đều là “nhập kho”.

## 2. Các vai trò nghiệp vụ

| Vai trò | Trách nhiệm chính |
|---|---|
| StoreManager | Nhìn nhu cầu của chi nhánh và tạo yêu cầu bổ sung trong phạm vi cửa hàng mình |
| WarehouseAccountant | Xem xét nguồn đáp ứng, lập đề nghị mua, tạo và gửi đơn đặt hàng |
| BusinessOwner | Kiểm tra và phê duyệt cam kết mua hàng |
| ShiftSupervisor | Nhận, kiểm đếm và xác nhận hàng vật lý tại chi nhánh |
| RegionManager | Theo dõi trong phạm vi được giao, không tự động có quyền điều hành mua hàng |
| SystemAdmin | Vai trò kỹ thuật; không mặc nhiên có quyền nghiệp vụ mua hàng |

Phân tách này nhằm giảm rủi ro một người vừa đề xuất, vừa cam kết, vừa tự xác nhận hàng.

## 3. Tồn kho và nhu cầu bổ sung

Tồn kho là hàng vật lý mà chi nhánh có thể sử dụng.

```text
Khả dụng = Có sẵn - Đã giữ chỗ
```

Ví dụ:

```text
Cà phê hạt tại Store 1
Có sẵn: 3 kg
Đã giữ chỗ: 0 kg
Khả dụng: 3 kg
```

Khi mức tồn không đủ cho hoạt động, StoreManager tạo **Yêu cầu bổ sung**.

Yêu cầu bổ sung trả lời:

- chi nhánh nào cần;
- cần mặt hàng nào;
- cần bao nhiêu;
- cần trước khi nào;
- vì sao phát sinh nhu cầu.

Yêu cầu bổ sung **không**:

- làm tồn kho tăng;
- đồng nghĩa đã mua hàng;
- đồng nghĩa nhà cung cấp đã giao hàng;
- tự đóng cảnh báo tồn chỉ vì đã tạo yêu cầu.

Ví dụ:

```text
Store 1 cần 10 kg cà phê
→ tạo yêu cầu bổ sung 10 kg
→ tồn kho vẫn giữ nguyên cho tới khi có nhận hàng thật
```

## 4. Chọn nguồn đáp ứng

Sau khi nhận nhu cầu, bộ phận phụ trách chọn nguồn:

```text
Nhu cầu
├─ Điều chuyển nội bộ
├─ Mua ngoài
└─ Sản xuất nội bộ
```

### Điều chuyển nội bộ

Dùng khi một cửa hàng hoặc kho nguồn có thể cấp hàng.

- xuất khỏi nguồn không có nghĩa là đích đã nhận;
- hàng đang đi là hàng đang vận chuyển;
- tồn của chi nhánh đích chỉ tăng khi nhận xác nhận.

### Mua ngoài

Dùng khi cần mua từ nhà cung cấp. Phần mua ngoài đi tiếp sang đề nghị mua và đơn đặt hàng.

### Sản xuất nội bộ

Dùng cho bán thành phẩm hoặc hàng có thể làm trong nội bộ. Cần tách rõ “kế hoạch sản xuất” và “đầu ra đã chấp nhận”.

Không nên nhầm “chọn nguồn MUA” với “đã mua”. Đây mới là quyết định cách đáp ứng nhu cầu.

## 5. Đề nghị mua và đơn đặt hàng

### Đề nghị mua (PA)

PA là bước chuẩn bị và xem xét nhu cầu mua.

Nó tập hợp:

- nhu cầu nào cần mua;
- số lượng cần mua;
- thời điểm cần hàng;
- mức ưu tiên;
- bối cảnh vận hành.

PA không làm tồn kho tăng và chưa phải là cam kết với nhà cung cấp.

### Đơn đặt hàng (PO)

PO là cam kết mua chính thức với nhà cung cấp.

```text
PA
↓
Chọn nhà cung cấp, quy cách và giá
↓
PO nháp
↓
Phê duyệt
↓
Gửi nhà cung cấp
```

PO cần duy trì đường liên kết:

```text
PO
↓
PA
↓
Phân bổ mua ngoài
↓
Yêu cầu bổ sung
```

PO được duyệt hoặc gửi vẫn **không** tăng tồn kho. Nó chỉ tạo nghĩa vụ thương mại: nhà cung cấp cần giao hàng.

## 6. Quy cách nhà cung cấp và đơn vị

Nhà cung cấp bán theo gói thương mại, trong khi kho quản lý lượng vật lý.

Ví dụ runtime đã được kiểm chứng tại CafeChain:

```text
1 gói cà phê
= 5 kg
Giá gói: 500.000 đ
```

Nếu nhu cầu là 10 kg:

```text
10 kg
÷ 5 kg/gói
= 2 gói
```

Điều cần nhớ:

- “gói” là số lượng mua;
- “kg” là lượng vật lý cần quy về tồn;
- không được tự bịa quy đổi bao/chai/thùng;
- nếu không chứng minh được lượng vật lý trong gói, nghiệp vụ phải dừng để bổ sung dữ liệu.

## 7. Phiếu nhận: nơi sự kiện vật lý được ghi nhận

Phiếu nhận ghi lại hàng mà chi nhánh thực tế nhận và kiểm đếm.

```text
PO đã gửi
↓
Nhà cung cấp giao
↓
Phiếu nhận nháp
↓
Kiểm đếm giao / chấp nhận / từ chối
↓
Xác nhận phiếu nhận
```

### Phiếu nhận nháp

Là nơi người nhận nhập kết quả kiểm đếm. Nó không làm tồn kho tăng.

### Phiếu nhận đã xác nhận

Là sự kiện vật lý có thẩm quyền làm tồn kho tăng.

Chỉ lượng **chấp nhận** được cộng vào tồn kho.

Ví dụ đã được xác minh trong CafeChain:

```text
Đặt: 10 kg
Giao: 10 kg
Chấp nhận: 9 kg
Từ chối: 1 kg

→ tồn kho tăng 9 kg, không phải 10 kg
→ PO và nhu cầu còn 1 kg
```

Hàng bị từ chối phải có lý do/sự cố phù hợp và không được coi là hàng sẵn sàng dùng.

## 8. Nhận một phần và hoàn tất

Chuỗi cung ứng thực tế không luôn giao đủ một lần.

```text
PO 10 kg
↓
Lần nhận 1: chấp nhận 9 kg, từ chối 1 kg
↓
Còn phải giao: 1 kg
↓
Lần nhận 2: chấp nhận 1 kg
↓
PO hoàn tất
```

CafeChain đã có bằng chứng runtime cho ví dụ này:

```text
Yêu cầu bổ sung #10: 10 kg
PO #267: 10 kg
Phiếu nhận #266: chấp nhận 9 kg, từ chối 1 kg
Phiếu nhận #267: chấp nhận 1 kg
Tồn kho: 0 → 9 → 10 kg
```

Điều quan trọng:

- từ chối không tự làm phần còn lại biến mất;
- phần còn lại chỉ hết khi nhận thêm hàng hoặc được đóng thiếu theo quyết định có lý do;
- không được đánh dấu hoàn tất chỉ vì PO đã duyệt.

## 9. Điều gì làm tồn kho thay đổi?

| Sự kiện | Tồn kho tại chi nhánh nhận |
|---|---|
| Tạo yêu cầu bổ sung | Không đổi |
| Chọn nguồn mua ngoài | Không đổi |
| Tạo PA | Không đổi |
| Tạo PO nháp | Không đổi |
| Duyệt/gửi PO | Không đổi |
| Lưu phiếu nhận nháp | Không đổi |
| Xác nhận phiếu nhận với lượng chấp nhận | Tăng đúng lượng chấp nhận |
| Hàng bị từ chối | Không tăng |
| Xác nhận lại cùng phiếu | Không được tăng lần hai |

## 10. Truy vết nghiệp vụ

Khi hỏi “vì sao tồn kho tăng?”, CafeChain cần lần được:

```text
Giao dịch tồn kho
↓
Phiếu nhận đã xác nhận
↓
Dòng PO
↓
PA
↓
Phân bổ mua ngoài
↓
Yêu cầu bổ sung
↓
Nhu cầu Store
```

Ví dụ thực tế:

```text
Giao dịch kho #1529
↓
Receipt #267
↓
PO #267
↓
PA #2
↓
Restock #10
```

Traceability không chỉ để kiểm toán. Nó giúp trả lời:

- ai yêu cầu mua;
- ai tạo và gửi PO;
- ai phê duyệt;
- ai nhận hàng;
- tại sao kho tăng đúng số đó;
- phần nào bị từ chối hoặc còn thiếu.

## 11. Ngoại lệ quan trọng

### Giao thiếu hoặc từ chối

Phần chưa chấp nhận vẫn là phần cần xử lý. Không ghi nhận nó như hàng tồn.

### Nhận vượt

Không nên tự nhận vượt nghĩa vụ đã đặt. Cần dừng để kiểm tra sai lệch.

### Gửi lại thao tác xác nhận

Một phiếu đã xác nhận không được làm tăng kho lần hai. CafeChain đã kiểm tra runtime: thử xác nhận lại bị từ chối, tồn kho không đổi.

### Cảnh báo tồn

Một yêu cầu có thể hoàn tất nhưng cảnh báo tồn vẫn cần đánh giá theo số tồn thực tế. Hoàn tất chứng từ không tự khẳng định tồn đã vượt ngưỡng an toàn.

## 12. Cách kể ngắn trong buổi bảo vệ

> Chuỗi cung ứng của CafeChain tách rõ ý định và sự kiện vật lý. Restock, PA và PO chỉ mô tả nhu cầu, quyết định nguồn và cam kết mua; chúng không làm tồn kho tăng. Tồn chỉ tăng khi chi nhánh xác nhận phiếu nhận, đúng bằng lượng hàng chấp nhận. Nhờ đó hệ thống truy được từ giao dịch kho ngược về phiếu nhận, PO, PA và nhu cầu của cửa hàng.
