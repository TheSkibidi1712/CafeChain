# Hướng dẫn vận hành kho, cảnh báo, BOM và giá vốn CafeChain

Tài liệu này dành cho Chủ doanh nghiệp, Quản lý khu vực, Quản lý cửa hàng, Kế toán/Kho và Ca trưởng. Tên màn hình được viết đúng theo menu Admin hiện tại.

## 1. Hiểu đúng bốn khái niệm

| Khái niệm | Ý nghĩa vận hành |
|---|---|
| Tồn kho cửa hàng | Tồn theo từng Store, gồm **Nguyên liệu** và **Bán thành phẩm (PreparedItem/BTP)**. |
| BOM/Công thức | Định mức nguyên liệu hoặc BTP cho món bán theo size, topping hoặc một mẻ sơ chế. |
| Cảnh báo kho | Tín hiệu thiếu hàng cần quản lý xác nhận; không phải chứng từ nhập kho. |
| Giá vốn | Chi phí mô phỏng theo lớp FIFO của Store được chọn; giá bán đồ uống vẫn là giá global toàn hệ thống. |

Số liệu trên màn hình ngưỡng được hiểu như sau:

```text
Tồn vật lý = StoreInventory.AvailableQty
Khả dụng = Tồn vật lý - Giữ chỗ
```

Không sửa số lượng tồn trực tiếp để “làm đẹp” số liệu. Tồn phải thay đổi qua bán hàng, phiếu nhận, phiếu kho, điều chuyển hoặc lệnh sơ chế có chứng từ.

## 2. Phân quyền chính

| Công việc | Vai trò chính |
|---|---|
| Xem tồn kho theo phạm vi | Các vai trò quản lý/vận hành được cấp Store/Area scope |
| Cập nhật ngưỡng tồn | Quản lý cửa hàng, Quản lý khu vực, Chủ doanh nghiệp, System Admin |
| Xác nhận/báo sai cảnh báo | Quản lý cửa hàng hoặc Chủ doanh nghiệp |
| Xem cảnh báo | Có thể gồm Kế toán/Kho, Ca trưởng, Nhân viên bán hàng theo scope |
| Sửa PreparedItem/BOM | Kế toán/Kho, Chủ doanh nghiệp, System Admin |
| Xem mô phỏng giá vốn | Chủ doanh nghiệp, Kế toán/Kho, Quản lý khu vực/cửa hàng, System Admin theo scope |
| Đổi giá bán global/chính sách topping | Chủ doanh nghiệp |
| Tổng hợp PA, tạo batch | Kế toán/Kho hoặc Chủ doanh nghiệp |
| Duyệt batch mua hàng | Chủ doanh nghiệp |
| Nhận hàng tại chi nhánh | Quản lý cửa hàng/Ca trưởng đúng Store theo policy |

Backend vẫn kiểm tra role và Store/Area scope. Việc nhìn thấy nút trên giao diện không thay thế phân quyền backend.

## 3. Chuẩn bị dữ liệu trước khi vận hành

Thực hiện theo thứ tự sau:

1. Vào **Kho & Cung ứng → Đơn vị và quy đổi**: cấu hình đơn vị vật lý như kg ↔ g, l ↔ ml.
2. Vào **Nguyên liệu**: mỗi nguyên liệu phải có đơn vị cơ sở đúng.
3. Vào **Nhà cung cấp**: khai báo offer, quy cách gói, số lượng trong gói, đơn vị nội dung, giá gói, MOQ và lead time.
4. Nếu có BTP như trà nền/sốt: tạo **PreparedItem** với đơn vị cơ sở ổn định.
5. Vào **Sản xuất/BOM → Công thức BOM** để tạo công thức món, topping và BTP.
6. Vào **Tình trạng dữ liệu BOM** và xử lý hết lỗi quan trọng.
7. Publish món/size cho từng Store trong Store Menu.
8. Nhập tồn đầu hoặc nhận hàng đúng chứng từ, sau đó cấu hình ngưỡng.

### Quy cách mua và giá gói

`CurrentPrice` là giá của **một gói mua**, không phải giá một gram/ml. Ví dụ:

```text
Gói cà phê: PackageQuantity = 1, Unit = kg, CurrentPrice = 140.000đ
Quy đổi: 1 kg = 1.000 g
Giá cơ sở: 140.000 / 1.000 = 140đ/g
```

Không suy ra quy cách từ tên nguyên liệu và không dùng “chai/gói” như một quy đổi vật lý cố định nếu mỗi chai/gói có dung tích khác nhau.

## 4. Cấu hình BOM đúng cách

Mở **Sản xuất / BOM → Công thức BOM**. Có ba tab:

- **Món bán**: công thức theo Drink + Size.
- **Topping**: định mức cho một phần topping.
- **Bán thành phẩm**: công thức sản xuất PreparedItem, có sản lượng/mẻ và đơn vị output.

### Quy tắc bắt buộc

1. Món bán phải có BOM đúng size đang publish trên POS.
2. Một dòng BOM chỉ chọn **Nguyên liệu hoặc PreparedItem**, không chọn cả hai.
3. Quantity và Unit phải có quy đổi hợp lệ về đơn vị cơ sở.
4. BTP phải có PreparedItem, output quantity, output unit và yield hợp lệ.
5. Khi sửa công thức đã dùng, hệ thống lưu phiên bản mới; không dùng RecipeId làm định danh tồn BTP.
6. Mở **Tình trạng dữ liệu BOM** để xử lý: thiếu giá, thiếu quy đổi, thiếu output hoặc mapping lỗi.

### One-level và recursive khác nhau

| Luồng | Cách đọc BOM |
|---|---|
| POS kiểm tra còn bán được không | One-level |
| POS trừ kho sau thanh toán | One-level |
| Tính giá vốn BOM | Có thể đi sâu tối đa 5 tầng, có chống vòng lặp |

Nếu BOM món dùng BTP, POS trừ thẳng tồn của PreparedItem đó. POS **không** bóc tiếp BTP thành nguyên liệu lá khi bán. Muốn có tồn BTP, phải hoàn tất lệnh sơ chế trước.

### Chính sách topping

Trong màn hình **Vốn và lợi nhuận gộp dự kiến**, chính sách topping theo DrinkSize xác định:

- topping có được chọn mặc định hay bắt buộc;
- giá topping đã gồm trong giá gốc hay cộng thêm;
- giá vốn topping đã nằm trong BOM món, cộng từ BOM topping, hay chỉ hiển thị;
- số lượng topping trên mỗi ly.

Không vừa đưa topping vào BOM món vừa cấu hình cộng BOM topping, vì sẽ tính vốn trùng.

## 5. Vận hành tồn kho hằng ngày

Mở **Kho & Cung ứng → Tồn kho cửa hàng**:

1. Chọn đúng Store.
2. Kiểm tra cả hai tab **Nguyên liệu** và **Bán thành phẩm**.
3. Đối chiếu tồn, giữ chỗ, giá/lớp FIFO và lịch sử biến động.
4. Nếu số liệu bất thường, mở lịch sử để tìm chứng từ nguồn: đơn POS, phiếu nhận, điều chuyển, sơ chế hoặc phiếu kho.

Các hành động thay đổi tồn:

- POS paid/committed: trừ BOM một lần.
- Offline order: chưa trừ khi còn local; trừ khi sync thành công.
- Phiếu nhận chi nhánh confirmed: cộng đúng số lượng thực nhận và tạo lớp giá FIFO.
- Điều chuyển: nguồn giảm khi dispatch; đích tăng khi xác nhận nhận theo workflow.
- Lệnh sơ chế completed: trừ input và cộng output PreparedItem theo snapshot của lệnh.

Blind Selling cho phép đơn offline đã bán được sync dù tồn xuống âm. Khi đó phải đối soát tồn vật lý; không xóa InventoryTransaction.

## 6. Cấu hình và xử lý cảnh báo kho

### Cấu hình ngưỡng

Mở **Ngưỡng tồn kho**:

1. Chọn Store.
2. Tìm nguyên liệu/BTP.
3. Nhập `MinStockLevel` theo đơn vị cơ sở và bấm **Lưu**.
4. Để trống nghĩa là chưa cấu hình; hệ thống không tự bịa ngưỡng.

`LOW_STOCK` được đánh giá khi khả dụng không lớn hơn ngưỡng. `OUT_OF_STOCK` phản ánh khả dụng đã hết/âm. Mỗi Store và mỗi identity kho có tối đa một cảnh báo thiếu hụt đang hoạt động theo chính sách dedupe.

### Xử lý cảnh báo

Mở **Cảnh báo kho → Chi tiết**:

1. So sánh snapshot lúc cảnh báo với tồn hiện tại và lịch sử biến động.
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

- Tạo RestockRequest không làm tăng tồn và không tự resolve cảnh báo.
- Dispatch hàng cũng chưa làm tăng tồn tại Store nhận.
- Sau phiếu nhận confirmed, hệ thống đánh giá lại tồn thực tế.
- Chỉ khi khả dụng vượt ngưỡng thì cảnh báo được resolve theo policy.
- RestockRequest có thể completed nhưng cảnh báo vẫn còn nếu số thực nhận chưa đủ.

## 7. Luồng nhập hàng hoàn chỉnh

```text
StockAlert
→ RestockRequest
→ Purchase Advice (PA)
→ PA chờ tổng hợp theo Supplier
→ PurchaseOrderBatch
→ Child PurchaseOrder theo Store
→ Owner duyệt batch
→ PDF revision + gửi Zalo thủ công
→ Store nhận hàng bằng BranchReceipt
→ Inventory + FIFO cost layer
→ đánh giá lại StockAlert
```

### Các bước thao tác

1. **Gợi ý nhập hàng** chỉ phân tích; không tự đặt hàng. Trạng thái “Thiếu ngưỡng/giá mua/nguồn cung/quy đổi/lead time” phải được sửa ở master data.
2. **Yêu cầu nhập hàng** là nhu cầu/intent, không phải chứng từ kho.
3. **Đề nghị mua hàng** lấy phần Restock còn thiếu sau khi trừ transfer, PA/PO đang hoạt động và phần đã phân bổ.
4. Tại **PA chờ tổng hợp**, Kế toán chọn một Supplier, offer và số kiện; backend kiểm tra lại MOQ, giá, quy đổi, remaining và Store phục vụ.
5. Tạo batch sẽ sinh một child PO cho mỗi Store trong cùng transaction.
6. Chủ doanh nghiệp duyệt batch một lần; không duyệt lại cùng quyết định trên từng child PO.
7. Tạo/tải PDF đúng revision, copy nội dung và gửi Zalo thủ công. “Đã gửi” không có nghĩa Supplier đã xác nhận.
8. Mỗi Store mở child PO của mình để nhận hàng. Ghi đúng số thực nhận, thiếu/thừa/từ chối và lý do.
9. Chỉ BranchReceipt confirmed mới tăng tồn và tạo FIFO layer; thao tác confirm lặp lại không được cộng kho hai lần.

## 8. Đọc màn hình giá vốn và lợi nhuận

Mở **Sản phẩm → Vốn & lợi nhuận dự kiến**:

1. Chọn **Cửa hàng mô phỏng FIFO**.
2. Chọn đồ uống và bấm **Tải mô phỏng**.
3. Xem từng size, BOM, thành phần FIFO và topping mặc định.

Các chỉ số:

```text
Lợi nhuận gộp = Giá bán - Giá vốn FIFO
Margin (%) = Lợi nhuận gộp / Giá bán × 100
Markup (%) = Lợi nhuận gộp / Giá vốn × 100
```

- **Giá bán** là global toàn hệ thống.
- **Giá vốn FIFO** thay đổi theo Store và lớp hàng thực tế.
- “Giá vốn BOM ước tính” trong BOM Builder là dữ liệu cấu hình/tham khảo; không thay thế FIFO vận hành tại Store.
- Nếu thiếu BOM, quy đổi, cost layer hoặc lượng FIFO, trạng thái phải là chưa đầy đủ. Không xem phần thiếu là 0.
- Chỉ Chủ doanh nghiệp được lưu giá global. Khi dữ liệu vốn chưa đầy đủ, phải nhập lý do và xác nhận thủ công theo UI.

## 9. Catalog POS hiển thị ra sao

POS chỉ đưa vào catalog các món/size:

- Drink, Size và StoreMenuItem đang active/enabled;
- đã publish và đang trong thời gian hiệu lực;
- thuộc đúng Store trong JWT.

Món đã publish vẫn hiển thị nhưng bị khóa khi không bán được:

| Trạng thái/lý do thường gặp | Cần kiểm tra |
|---|---|
| BOM size chưa sẵn sàng / `RECIPE_INVALID` | BOM active đúng Drink + Size và unit conversion |
| Chưa có tồn kho nguyên liệu/BTP | Row tồn Ingredient/PreparedItem đúng Store |
| Không đủ nguyên liệu khả dụng / `OUT_OF_STOCK` | Available trừ Reserved của thành phần one-level |
| Topping bắt buộc không khả dụng | StoreTopping, BOM topping và tồn topping |
| Kho BTP cửa hàng đang bị khóa / `STORE_NOT_READY` | Trạng thái cutover/writer của Store và mapping PreparedItem |

Thu ngân không thể thêm món bị khóa vào giỏ. Catalog được cache theo Store trong IndexedDB và tự đồng bộ version; đơn offline trong `cartSyncQueue` là dữ liệu riêng, không được xóa khi làm mới cache catalog.

## 10. Checklist vận hành

### Đầu ngày

- Kiểm tra tồn âm và BTP sắp hết.
- Xem cảnh báo OPEN/CONFIRMED.
- Kiểm tra món POS đang bị khóa và lý do.
- Kiểm tra lệnh sơ chế cần chạy.
- Kiểm tra child PO dự kiến giao trong ngày.

### Khi nhận hàng

- Chọn đúng Store và đúng child PO.
- Đối chiếu đơn vị cơ sở/quy cách gói.
- Nhập số thực nhận, không dùng số yêu cầu thay thế.
- Ghi discrepancy/short-close khi có.
- Confirm một lần và kiểm tra InventoryTransaction/FIFO layer.

### Cuối ngày

- Đối soát đơn offline đã sync và tồn âm.
- Kiểm tra cảnh báo mới từ POS/OFFLINE_SYNC.
- Đối chiếu BTP thực tế với lệnh sơ chế.
- Kiểm tra cảnh báo đã resolve nhưng RestockRequest còn mở.

### Hằng tuần

- Rà soát ngưỡng theo tốc độ tiêu thụ và lead time.
- Mở **Tình trạng dữ liệu BOM**.
- Kiểm tra offer Supplier, giá gói, MOQ và quy đổi.
- So sánh margin theo Store; xử lý size thiếu FIFO trước khi quyết định giá.

## 11. Xử lý sự cố nhanh

### POS không có menu

1. Xác nhận JWT thuộc đúng Store và backend đang chạy.
2. Kiểm tra Store Menu đã enable/publish món-size.
3. Mở console: nếu có `DexieError`, tải lại sau khi frontend mới được deploy; cache catalog sẽ được tạo lại nhưng offline order vẫn giữ nguyên.
4. Nếu UI báo lỗi, bấm **Thử tải lại**.
5. Kiểm tra API `/api/v1/pos/catalog`; lỗi 500 cần xử lý backend/schema, không seed mock vào frontend.

### Món hiện nhưng bị khóa

Đọc nhãn trên card, sau đó kiểm tra đúng BOM size, unit conversion và tồn Ingredient/PreparedItem tại Store. Không bật bán bằng cách bỏ inventory guard.

### Giá vốn chưa đầy đủ

Kiểm tra theo thứ tự: BOM → mapping PreparedItem → unit conversion → Supplier package/price → FIFO cost layer của Store → topping policy.

### Cảnh báo không biến mất

Kiểm tra khả dụng hiện tại có thực sự **lớn hơn** ngưỡng chưa. Restock completed không bảo đảm đủ tồn nếu nhận thiếu hoặc vẫn có giữ chỗ.

## 12. Nguyên tắc không được phá

- Không trừ kho trước khi order paid/committed.
- Không trừ kho hai lần khi retry/webhook/offline sync trùng.
- Không dùng giá gói như giá trên đơn vị cơ sở.
- Không explode BTP khi POS bán; trừ PreparedItem one-level.
- Không coi dữ liệu giá vốn thiếu là 0.
- Không coi RestockRequest hoặc “đã gửi Zalo” là hàng đã về.
- Không xóa ledger/chứng từ để sửa tồn; dùng quy trình đối soát/điều chỉnh có audit.
