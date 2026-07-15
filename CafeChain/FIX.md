# HƯỚNG DẪN SỬ DỤNG PHIẾU KHO VÀ XUẤT ÂM KHO

> Phiên bản tài liệu: 15/07/2026
>
> Phạm vi: Form Phiếu Kho, quy trình phê duyệt xuất âm và cấu hình vận hành
>
> Đối tượng: nhân viên kho, quản lý cửa hàng, quản lý vùng, kế toán/kho, chủ doanh nghiệp, quản trị hệ thống và DBA
>
> Nguồn kỹ thuật và trạng thái nghiệm thu: [Scripts/NegativeInventoryAcceptance.md](Scripts/NegativeInventoryAcceptance.md)

## 1. Mục đích và cảnh báo an toàn

Tài liệu này hướng dẫn cách sử dụng màn hình **Phiếu Kho**, gồm:

- Tạo phiếu nhập kho.
- Tạo phiếu xuất kho.
- Tạo phiếu kiểm kê.
- Tạo phiếu hủy kho.
- Lưu nháp, xác nhận, hủy và xuất chứng từ.
- Bật/tắt tính năng xuất âm.
- Gửi, duyệt hoặc từ chối yêu cầu xuất âm.
- Hiểu giá vốn, cost-gap và cách nhập bù tồn âm.

Tính năng xuất âm thủ công mặc định đang **tắt**. Không bật tính năng chỉ để vượt qua lỗi thiếu tồn. Chỉ được bật sau khi:

1. Người phụ trách nghiệp vụ kho phê duyệt phạm vi sử dụng.
2. Dữ liệu tồn kho, FIFO và cost-gap đã được đối soát.
3. SQL Server integration gate trong `NegativeInventoryAcceptance.md` đã chạy thành công.
4. Có ít nhất hai người tham gia quy trình maker-checker: một người gửi và một người duyệt.
5. Đã thống nhất hạn mức âm theo cửa hàng/nguyên liệu.

Nếu SQL Server gate chưa đạt, phải giữ:

```text
inventory_manual_external_export_negative_enabled = false
```

## 2. Thuật ngữ cần biết

| Thuật ngữ | Ý nghĩa vận hành |
|---|---|
| `AvailableQty` | Lượng tồn tự do hiện có để xuất. Đây đã là lượng khả dụng; không trừ `ReservedQty` thêm lần nữa. |
| Đơn vị chứng từ | Đơn vị người dùng chọn trên form, ví dụ thùng, chai, kg. |
| Đơn vị base | Đơn vị chuẩn hệ thống dùng để ghi sổ kho, ví dụ g, ml, cái. |
| `BaseQuantity` | Số lượng sau khi quy đổi từ đơn vị chứng từ về đơn vị base. |
| Tồn âm | `AvailableQty` sau xuất nhỏ hơn 0. |
| FIFO | Xuất giá vốn từ lớp nhập cũ nhất trước, theo thời gian tạo rồi Id. |
| Preflight | Bước server kiểm tra trước khi xác nhận: tồn trước, lượng xuất, tồn dự kiến, hạn mức và kết quả. |
| Approval | Yêu cầu phê duyệt bền vững cho một phiếu xuất âm. |
| Maker-checker | Người tạo/gửi phiếu và người duyệt phải là hai người khác nhau. |
| Cost-gap | Phần đã xuất thực tế nhưng chưa có đủ bằng chứng giá vốn FIFO. |
| Settlement | Dùng giá vốn của lần nhập sau để tất toán cost-gap cũ. |
| Store scope | Danh sách cửa hàng mà tài khoản được phép đọc hoặc thao tác. |
| Policy version | Phiên bản chính sách dùng để làm cũ các approval khi cấu hình thay đổi. |

## 3. Quyền và trách nhiệm

### 3.1. Người tạo phiếu

Người dùng phải có quyền truy cập khu vực quản trị Phiếu Kho và có scope tới cửa hàng được chọn. Dropdown cửa hàng chỉ hỗ trợ thao tác; server vẫn kiểm tra lại scope.

StoreManager hoặc người có quyền lập phiếu có thể gửi yêu cầu xuất âm trong cửa hàng được phân quyền, nhưng không được tự duyệt.

### 3.2. Người duyệt xuất âm

Các vai trò có thể duyệt khi đồng thời có store scope hợp lệ:

- BusinessOwner.
- SystemAdmin.
- AreaManager.
- AccountantWarehouse.

Các vai trò trên vẫn không được:

- Tự duyệt yêu cầu do chính mình gửi.
- Duyệt phiếu ngoài phạm vi cửa hàng/vùng.
- Bỏ qua hạn mức.
- Duyệt approval đã stale.
- Duyệt khi kill switch đã tắt.

Nút duyệt trên giao diện chỉ được hiển thị khi điều kiện quyền ban đầu đạt. Application service là nơi quyết định cuối cùng.

### 3.3. DBA hoặc người vận hành hệ thống

DBA chịu trách nhiệm:

- Kiểm tra SQL Server gate.
- Backup trước khi thay đổi cấu hình production.
- Bật/tắt feature trong `dbo.SystemSettings`.
- Đặt hạn mức item trong `dbo.StoreInventories.MaxNegativeQty`.
- Tăng policy version sau mỗi lần thay đổi policy hoặc limit.
- Lưu ticket/biên bản phê duyệt và kết quả truy vấn sau thay đổi.

## 4. Mở màn hình Phiếu Kho

Đường dẫn trên sidebar:

**Admin → Phiếu Kho → Phiếu kho**

Màn hình chính gồm:

1. **Dashboard**: tổng số phiếu, phiếu nháp, đã xác nhận, đã hủy và số phiếu trong tháng.
2. **Bộ lọc**: mã phiếu/đối tác, loại, trạng thái, mục đích, cửa hàng và khoảng ngày.
3. **Tab nghiệp vụ**: Nhập kho, Xuất kho, Kiểm kê và Hủy kho.
4. **Bảng danh sách**: mã phiếu, loại, mục đích, cửa hàng, đối tác, ngày, giá trị, trạng thái và thao tác.
5. **Tạo phiếu mới**: mở popup chọn loại phiếu.
6. **Xuất Excel**: xuất danh sách theo bộ lọc hiện tại.

### 4.1. Trạng thái phiếu

| Trạng thái | Ý nghĩa | Kho đã thay đổi? |
|---|---|---:|
| `DRAFT` | Phiếu mới lưu nháp, chưa xác nhận. | Không |
| `PENDING` | Phiếu xuất âm đang chờ người khác duyệt. | Không |
| `CONFIRMED` | Phiếu đã xử lý thành công và có snapshot. | Có |
| `CANCELLED` | Phiếu đã hủy hoặc yêu cầu âm bị từ chối. | Không phát sinh mutation mới |

Luồng trạng thái chính:

```text
DRAFT ───────────────→ CONFIRMED
  │
  ├──────────────────→ CANCELLED
  │
  └→ PENDING ────────→ CONFIRMED
          └──────────→ CANCELLED
```

`PENDING` không được tạo transaction, cost allocation, cost-gap hoặc confirmed snapshot.

### 4.2. Các nút trên danh sách

- **Biểu tượng mắt**: xem chi tiết phiếu.
- **Mở để duyệt**: chỉ hiện với phiếu đang chờ duyệt xuất âm và tài khoản reviewer đủ role, scope, không phải requester; thao tác duyệt/từ chối vẫn nằm trong chi tiết.
- **Biểu tượng dấu kiểm trên DRAFT**: mở bản xem trước để xác nhận hoặc hủy draft.
- **Biểu tượng in**: mở lựa chọn PDF/Word. Chỉ phiếu `CONFIRMED` có snapshot mới xuất được.
- **Xuất Excel**: xuất danh sách và chi tiết phù hợp bộ lọc; không đồng nghĩa mọi dòng đều là chứng từ đã xác nhận.

## 5. Quy trình chung khi tạo phiếu

### Bước 1 — Chọn loại phiếu

Bấm **Tạo phiếu mới**, sau đó chọn một trong bốn loại:

- Phiếu Nhập Kho.
- Phiếu Xuất Kho.
- Phiếu Kiểm Kê.
- Phiếu Hủy Kho.

Chuyển kho không nằm trong popup này. Dùng **Admin → Phiếu Kho → Phiếu chuyển kho**.

### Bước 2 — Kiểm tra thông tin chung

- **Mã phiếu**: hệ thống tự sinh; không sửa thủ công.
- **Ngày chứng từ**: mặc định là ngày hiện tại và được server kiểm tra.
- **Cửa hàng**: chỉ chọn cửa hàng thuộc scope.
- **Mục đích**: quyết định validation, nguồn nguyên liệu, giá và policy tồn âm.
- **Nhà cung cấp/Đối tác**: chỉ hiển thị khi nghiệp vụ cần.
- **Ghi chú**: mô tả chung hoặc lý do điều chỉnh/hủy.
- **Lý do xuất âm**: trường riêng trên phiếu xuất; không được thay bằng ghi chú chung.

### Bước 3 — Thêm nguyên liệu

1. Bấm **Thêm nguyên liệu**.
2. Chọn nguyên liệu.
3. Chọn đơn vị tính.
4. Nhập số lượng.
5. Kiểm tra dòng “Quy đổi” để chắc chắn số lượng base đúng.
6. Kiểm tra tồn/MOQ/chênh lệch tùy loại phiếu.
7. Thêm các dòng còn lại.

Không được chọn cùng một nguyên liệu ở hai dòng. Nếu chọn trùng, form sẽ xóa lựa chọn dòng vừa nhập và yêu cầu chọn lại.

Nếu hiển thị **Chưa cấu hình quy đổi**, dừng thao tác và cấu hình đơn vị trước. Không dùng số lượng ước đoán để thay thế conversion factor.

Nguồn nguyên liệu phụ thuộc loại phiếu:

| Loại phiếu | Nguồn hiển thị |
|---|---|
| Nhập từ nhà cung cấp | Nguyên liệu được cấu hình với nhà cung cấp. |
| Nhập điều chỉnh | Master nguyên liệu đang hoạt động; xác nhận nhập có thể tạo `StoreInventory` mới. |
| Xuất `SALE/GIFT/DEBT/SAMPLE` | Nguyên liệu đang hoạt động đã có `StoreInventory` tại cửa hàng, kể cả tồn bằng 0 hoặc đang âm. |
| Xuất `ADJUSTMENT_OUT` | Nguyên liệu đã có `StoreInventory` tại cửa hàng và `AvailableQty > 0`. |
| Kiểm kê | Mọi nguyên liệu đang hoạt động đã có `StoreInventory` tại cửa hàng, kể cả tồn dương, 0 hoặc âm. |
| Hủy kho | Nguyên liệu đã có `StoreInventory` tại cửa hàng và `AvailableQty > 0`. |

Đổi cửa hàng, loại phiếu hoặc mục đích sẽ tải lại nguồn nguyên liệu và đặt lại các dòng đã chọn để không giữ số tồn/giá của nguồn cũ. Một payload tự ghép chứa nguyên liệu không thuộc `StoreInventory` của cửa hàng bị server từ chối với `INGREDIENT_NOT_IN_STORE_INVENTORY`.

### Bước 4 — Kiểm tra tổng hợp

Khối tổng hợp hiển thị:

- Số dòng.
- Tổng số lượng chứng từ.
- Tổng số lượng quy đổi base theo từng đơn vị.
- Tổng tiền.
- VAT.
- Thành tiền.

Phiếu kiểm kê và phiếu hủy là chứng từ quantity-only nên các dòng tiền bị ẩn và luôn được normalize về 0.

### Bước 5 — Chọn cách lưu

- **Lưu nháp**: tạo/cập nhật `DRAFT`, chưa thay đổi kho.
- **Tạo & xác nhận**: chạy validation, preflight cần thiết và xử lý phiếu.
- **Hủy** trên modal: đóng form, không phải action hủy một document đã lưu.

Nếu đã lưu nháp, dùng nút xác nhận trên danh sách. Server tải lại draft và không tin chi tiết gửi lại từ client.

## 6. Hướng dẫn từng loại phiếu

### 6.1. Nhập kho từ nhà cung cấp

Chọn:

```text
Loại phiếu: IMPORT
Mục đích: IMPORT_PURCHASE — Nhập từ nhà cung cấp
```

Các bước:

1. Chọn cửa hàng nhận hàng.
2. Chọn nhà cung cấp đang hoạt động.
3. Form tải danh sách nguyên liệu phù hợp nhà cung cấp.
4. Chọn nguyên liệu và đơn vị đúng với chứng từ giao hàng.
5. Nhập số lượng thực nhận.
6. Kiểm tra đơn giá nhập và thành tiền.
7. Đối chiếu MOQ/còn nhận nếu form hiển thị.
8. Lưu nháp hoặc tạo và xác nhận.

Nhà cung cấp là bắt buộc. Server từ chối nhà cung cấp không tồn tại hoặc đã ngừng hoạt động.

Khi xác nhận, hệ thống tăng tồn, ghi transaction nhập và tạo FIFO cost layer từ bằng chứng giá nhập.

### 6.2. Nhập điều chỉnh

Chọn:

```text
Loại phiếu: IMPORT
Mục đích: IMPORT_ADJUSTMENT — Điều chỉnh tăng
```

Sử dụng khi biên bản kiểm tra/đối soát chứng minh tồn phải tăng nhưng không phải giao dịch mua hàng thông thường.

Yêu cầu:

- Bắt buộc ghi rõ lý do điều chỉnh trong **Ghi chú**.
- Đơn giá nhập của mọi dòng phải lớn hơn 0.
- Nguyên liệu và đơn vị phải đang hoạt động/hợp lệ.
- Lưu kèm mã biên bản hoặc tham chiếu trong ghi chú.

Không dùng nhập điều chỉnh để che cost-gap chưa được phân tích.

### 6.3. Xuất kho

Các mục đích:

| Purpose | Nhãn trên form | Có thể xin xuất âm? | Yêu cầu đặc biệt |
|---|---|---:|---|
| `SALE` | Xuất bán hàng | Có | Có thể nhập tên đối tác/khách hàng. |
| `GIFT` | Xuất quà tặng | Có | Hàng phải đã giao thực tế. |
| `DEBT` | Xuất ghi nợ | Có | Hàng đã giao và đã ghi nhận công nợ. |
| `SAMPLE` | Xuất hàng mẫu | Có | Hàng mẫu đã giao thực tế. |
| `ADJUSTMENT_OUT` | Điều chỉnh giảm | Không | Bắt buộc ghi chú; thiếu tồn hoặc FIFO bị chặn. |

Các bước xuất không âm:

1. Chọn cửa hàng xuất.
2. Chọn đúng purpose.
3. Với `SALE`, nhập đối tác nếu cần.
4. Chọn nguyên liệu và đơn vị.
5. Nhập số lượng thực xuất.
6. Đối chiếu cột **Tồn khả dụng** và quy đổi base.
7. Bấm **Tạo & xác nhận**.

Danh sách chọn chỉ chứa nguyên liệu đã có bản ghi `StoreInventory` tại cửa hàng đang chọn. Đối với `SALE`, `GIFT`, `DEBT`, `SAMPLE`, item tồn bằng 0 hoặc đang âm vẫn xuất hiện để có thể đi qua preflight và quy trình xin duyệt; điều này không đồng nghĩa phiếu chắc chắn được phép xuất.

Giá vốn xuất do server xác định từ FIFO. Giá hiển thị hoặc dữ liệu client không phải bằng chứng giá vốn có thẩm quyền.

`ADJUSTMENT_OUT`, `WASTE`, `PRODUCTION_OUT` và nguồn chuyển kho luôn fail-closed nếu thao tác làm âm.

### 6.4. Kiểm kê

Chọn:

```text
Loại phiếu: STOCK_TAKE
Mục đích: STOCK_TAKE — Kiểm kê
```

Các bước:

1. Đếm vật lý trước khi nhập số liệu.
2. Chọn cửa hàng và nguyên liệu.
3. Nhập **Số lượng thực tế**, không nhập lượng chênh lệch.
4. Đọc cột **Tồn hệ thống**.
5. Kiểm tra cột **Chênh lệch**:
   - Số dương: tăng tồn.
   - Số âm: giảm tồn.
   - Bằng 0: khớp tồn.
6. Xác nhận kết quả kiểm kê.

Số lượng thực tế bằng 0 hợp lệ. Số lượng thực tế âm không hợp lệ.

Form chỉ hiển thị nguyên liệu đã có `StoreInventory` tại cửa hàng kiểm kê. Item tồn bằng 0 hoặc đang âm vẫn phải xuất hiện để người kiểm kê nhập số thực tế và đối soát. Nguyên liệu chỉ tồn tại trong master data hoặc chỉ có ở cửa hàng khác không xuất hiện.

Trên `InventoryDocumentDetail`, bốn trường sau luôn bằng 0:

```text
UnitPrice = 0
TotalAmount = 0
CostPrice = 0
CostAmount = 0
```

Chi phí xử lý thật, nếu có giảm tồn, vẫn được ghi ở transaction và cost allocation; không lấy bốn trường detail làm sổ giá vốn.

### 6.5. Hủy kho

Chọn một lý do:

- `DAMAGED`: hàng hỏng.
- `EXPIRED`: hết hạn.
- `BROKEN`: bị vỡ.
- `CONTAMINATED`: nhiễm bẩn.
- `LOST`: thất thoát.

Các bước:

1. Kiểm tra hàng vật lý và lập biên bản nếu quy trình nội bộ yêu cầu.
2. Chọn cửa hàng và lý do hủy.
3. Chọn nguyên liệu còn tồn.
4. Nhập **Số lượng hủy**.
5. Bắt buộc nhập lý do cụ thể trong **Ghi chú**.
6. Đối chiếu tồn khả dụng.
7. Xác nhận phiếu.

Không thể hủy vượt tồn khả dụng và không có đường phê duyệt âm cho phiếu hủy.

Form hủy chỉ hiển thị nguyên liệu đã có `StoreInventory` tại cửa hàng và `AvailableQty > 0`. Item tồn bằng 0/âm, item chỉ có ở cửa hàng khác hoặc chỉ có trong master data không được đưa vào danh sách.

Giống phiếu kiểm kê, `UnitPrice`, `TotalAmount`, `CostPrice`, `CostAmount` trên detail luôn bằng 0; giá vốn thật vẫn nằm ở ledger/allocation.

## 7. Bật, tắt và cấu hình xuất âm

### 7.1. Bật/tắt ở đâu?

Màn hình cấu hình hiện nằm tại **Admin → Cài đặt hệ thống → Kho & tồn âm**. Chỉ **BusinessOwner** và **SystemAdmin** được xem và lưu cấu hình.

Các bước bật có kiểm soát:

1. Kiểm tra SQL Server gate trong `Scripts/NegativeInventoryAcceptance.md` đã đạt.
2. Mở tab **Kho & tồn âm**.
3. Giữ `approval_required = true` ở trạng thái khóa.
4. Giữ hạn mức mặc định bằng 0 hoặc nhập hạn mức đã được phê duyệt.
5. Lọc cửa hàng, tìm item và chọn **Chặn**, **Theo mặc định** hoặc **Hạn mức riêng**.
6. Bật switch **Cho phép gửi yêu cầu xuất âm kho**.
7. Bấm **Lưu cấu hình âm kho**, đọc cảnh báo approval stale và xác nhận.
8. Tải lại màn hình, kiểm tra badge **Bật có kiểm soát** và hạn mức hiệu lực của item.

Để tắt khẩn cấp, tắt switch rồi lưu. Provider đọc lại setting ở request kế tiếp nên kill switch không cần restart ứng dụng.

Nguồn runtime có thẩm quyền là:

```text
dbo.SystemSettings
```

Hạn mức riêng của từng dòng tồn nằm tại:

```text
dbo.StoreInventories.MaxNegativeQty
```

Màn hình trên ghi vào `dbo.SystemSettings` và `dbo.StoreInventories.MaxNegativeQty`. File `Data/Configurations/Systems/SystemSettingConfiguration.cs` chỉ seed giá trị mặc định cho database mới; sửa file seed không tự thay đổi database đang vận hành. Không sửa `appsettings.json` để bật feature.

Policy provider đọc trực tiếp bốn setting ở mỗi request, không dùng cache. Kill switch có hiệu lực từ request kế tiếp.

### 7.2. Bốn setting bắt buộc

| SettingKey | Giá trị seed | Quy tắc |
|---|---|---|
| `inventory_manual_external_export_negative_enabled` | `false` | `true` mới cho phép đi vào quy trình xin xuất âm. |
| `inventory_manual_external_export_approval_required` | `true` | Phải luôn là `true` khi feature bật. |
| `inventory_manual_external_export_default_max_negative_quantity` | `0` | Hạn mức mặc định, không được âm. |
| `inventory_manual_external_export_policy_version` | `manual-export-v1` | Không được rỗng; phải đổi sau mỗi thay đổi policy/limit. |

Thiếu key, boolean sai định dạng, limit âm, version rỗng, hoặc `enabled=true` cùng `approval_required=false` đều trả `NEGATIVE_SETTING_INVALID` và chặn xuất âm.

### 7.3. Thứ tự ưu tiên hạn mức

```text
effectiveLimit = StoreInventories.MaxNegativeQty
                 nếu MaxNegativeQty khác NULL;
                 ngược lại dùng default setting.
```

Ví dụ:

| Item limit | Default limit | Effective limit |
|---:|---:|---:|
| `NULL` | 5 | 5 |
| 2 | 5 | 2 |
| 0 | 5 | 0 — item bị chặn âm |

Khuyến nghị production:

- Giữ default limit bằng 0.
- Chỉ đặt `MaxNegativeQty > 0` cho `StoreInventoryId` đã được phê duyệt.
- Không mở một hạn mức global lớn cho mọi nguyên liệu.

### 7.4. SQL kiểm tra hiện trạng — chỉ đọc

DBA chạy trên đúng database CafeChain:

```sql
SELECT
    SettingKey,
    SettingValue,
    Description
FROM dbo.SystemSettings
WHERE SettingKey IN
(
    N'inventory_manual_external_export_negative_enabled',
    N'inventory_manual_external_export_approval_required',
    N'inventory_manual_external_export_default_max_negative_quantity',
    N'inventory_manual_external_export_policy_version'
)
ORDER BY SettingKey;

SELECT
    StoreInventoryId,
    StoreId,
    IngredientId,
    PreparedItemId,
    AvailableQty,
    ReservedQty,
    MaxNegativeQty,
    LastUpdated
FROM dbo.StoreInventories
WHERE MaxNegativeQty IS NOT NULL
ORDER BY StoreId, IngredientId, PreparedItemId, StoreInventoryId;
```

Kết quả truy vấn setting phải có đúng bốn dòng.

### 7.5. SQL bật feature an toàn

Mẫu dưới đây bật feature nhưng giữ default limit bằng 0. Sau đó DBA mở limit riêng cho từng item ở mục 7.7.

Thay `manual-export-v1-YYYYMMDD-NN` bằng version duy nhất gắn với ticket triển khai.

```sql
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DefaultMaxNegativeQuantity decimal(18,3) = 0.000;
DECLARE @NewPolicyVersion nvarchar(100) = N'manual-export-v1-YYYYMMDD-NN';

IF @DefaultMaxNegativeQuantity < 0
    THROW 51000, N'Default max negative quantity không được âm.', 1;

IF NULLIF(LTRIM(RTRIM(@NewPolicyVersion)), N'') IS NULL
    THROW 51000, N'Policy version là bắt buộc.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF
    (
        SELECT COUNT(*)
        FROM dbo.SystemSettings WITH (UPDLOCK, HOLDLOCK)
        WHERE SettingKey IN
        (
            N'inventory_manual_external_export_negative_enabled',
            N'inventory_manual_external_export_approval_required',
            N'inventory_manual_external_export_default_max_negative_quantity',
            N'inventory_manual_external_export_policy_version'
        )
    ) <> 4
        THROW 51000, N'Thiếu hoặc trùng setting âm kho; đã hủy thao tác.', 1;

    UPDATE dbo.SystemSettings
    SET SettingValue = N'true'
    WHERE SettingKey = N'inventory_manual_external_export_approval_required';

    UPDATE dbo.SystemSettings
    SET SettingValue = CONVERT(nvarchar(100), @DefaultMaxNegativeQuantity)
    WHERE SettingKey = N'inventory_manual_external_export_default_max_negative_quantity';

    UPDATE dbo.SystemSettings
    SET SettingValue = @NewPolicyVersion
    WHERE SettingKey = N'inventory_manual_external_export_policy_version';

    -- Bật ở bước cuối để các setting liên quan đã nhất quán trước.
    UPDATE dbo.SystemSettings
    SET SettingValue = N'true'
    WHERE SettingKey = N'inventory_manual_external_export_negative_enabled';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT SettingKey, SettingValue
FROM dbo.SystemSettings
WHERE SettingKey LIKE N'inventory_manual_external_export_%'
ORDER BY SettingKey;
```

Sau khi chạy:

1. Lưu kết quả `SELECT` vào ticket.
2. Chạy preflight thử với một item limit bằng 0; kết quả phải bị chặn khi projected after âm.
3. Chỉ tiếp tục mở item limit sau khi kiểm tra maker-checker.

### 7.6. SQL tắt khẩn cấp

Tắt feature không xóa approval/gap lịch sử. Approval đang chờ sẽ không thể duyệt theo policy cũ.

```sql
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @NewPolicyVersion nvarchar(100) = N'manual-export-disabled-YYYYMMDD-NN';

IF NULLIF(LTRIM(RTRIM(@NewPolicyVersion)), N'') IS NULL
    THROW 51000, N'Policy version là bắt buộc.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF
    (
        SELECT COUNT(*)
        FROM dbo.SystemSettings WITH (UPDLOCK, HOLDLOCK)
        WHERE SettingKey IN
        (
            N'inventory_manual_external_export_negative_enabled',
            N'inventory_manual_external_export_approval_required',
            N'inventory_manual_external_export_default_max_negative_quantity',
            N'inventory_manual_external_export_policy_version'
        )
    ) <> 4
        THROW 51000, N'Thiếu hoặc trùng setting âm kho; đã hủy thao tác.', 1;

    -- Tắt ở bước đầu để request kế tiếp fail-closed.
    UPDATE dbo.SystemSettings
    SET SettingValue = N'false'
    WHERE SettingKey = N'inventory_manual_external_export_negative_enabled';

    UPDATE dbo.SystemSettings
    SET SettingValue = @NewPolicyVersion
    WHERE SettingKey = N'inventory_manual_external_export_policy_version';

    -- Giữ maker-checker ở trạng thái hợp lệ cho lần bật sau.
    UPDATE dbo.SystemSettings
    SET SettingValue = N'true'
    WHERE SettingKey = N'inventory_manual_external_export_approval_required';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT SettingKey, SettingValue
FROM dbo.SystemSettings
WHERE SettingKey LIKE N'inventory_manual_external_export_%'
ORDER BY SettingKey;
```

Sau khi tắt, chạy preflight projected âm. Kết quả mong đợi:

```text
Outcome: Blocked
ReasonCode: MANUAL_NEGATIVE_FEATURE_DISABLED
```

### 7.7. Đặt hạn mức cho một item

Trước tiên dùng truy vấn chỉ đọc để tìm chính xác `StoreInventoryId`. Không update theo tên nguyên liệu.

```sql
DECLARE @StoreId int = 0;          -- Thay bằng cửa hàng cần cấu hình.
DECLARE @IngredientId int = NULL;  -- Điền IngredientId hoặc PreparedItemId.
DECLARE @PreparedItemId int = NULL;

IF @StoreId <= 0
    THROW 51000, N'StoreId không hợp lệ.', 1;

IF (@IngredientId IS NULL AND @PreparedItemId IS NULL)
   OR (@IngredientId IS NOT NULL AND @PreparedItemId IS NOT NULL)
    THROW 51000, N'Phải chọn đúng một identity IngredientId/PreparedItemId.', 1;

SELECT
    si.StoreInventoryId,
    si.StoreId,
    si.IngredientId,
    si.PreparedItemId,
    si.AvailableQty,
    si.MaxNegativeQty
FROM dbo.StoreInventories AS si
WHERE si.StoreId = @StoreId
  AND
  (
      (si.IngredientId = @IngredientId AND si.PreparedItemId IS NULL)
      OR (si.PreparedItemId = @PreparedItemId AND si.IngredientId IS NULL)
  );
```

Sau khi xác minh đúng identity, đặt hạn mức và đổi policy version trong cùng transaction:

```sql
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @StoreInventoryId int = 0; -- Thay bằng Id đã đối chiếu.
DECLARE @MaxNegativeQty decimal(18,3) = 5.000;
DECLARE @NewPolicyVersion nvarchar(100) = N'manual-export-v1-YYYYMMDD-NN';

IF @StoreInventoryId <= 0
    THROW 51000, N'StoreInventoryId không hợp lệ.', 1;

IF @MaxNegativeQty < 0
    THROW 51000, N'MaxNegativeQty không được âm.', 1;

IF NULLIF(LTRIM(RTRIM(@NewPolicyVersion)), N'') IS NULL
    THROW 51000, N'Policy version là bắt buộc.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    UPDATE dbo.StoreInventories WITH (UPDLOCK)
    SET MaxNegativeQty = @MaxNegativeQty
    WHERE StoreInventoryId = @StoreInventoryId;

    IF @@ROWCOUNT <> 1
        THROW 51000, N'Không tìm thấy đúng một StoreInventoryId.', 1;

    UPDATE dbo.SystemSettings
    SET SettingValue = @NewPolicyVersion
    WHERE SettingKey = N'inventory_manual_external_export_policy_version';

    IF @@ROWCOUNT <> 1
        THROW 51000, N'Không cập nhật được policy version.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT StoreInventoryId, StoreId, IngredientId, PreparedItemId, MaxNegativeQty
FROM dbo.StoreInventories
WHERE StoreInventoryId = @StoreInventoryId;
```

### 7.8. Xóa hạn mức riêng của item

Đặt `MaxNegativeQty = NULL` để item quay về dùng default limit. Không dùng `NULL` nếu mục đích là chặn item; để chặn riêng item, đặt bằng 0.

```sql
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @StoreInventoryId int = 0;
DECLARE @NewPolicyVersion nvarchar(100) = N'manual-export-v1-YYYYMMDD-NN';

IF @StoreInventoryId <= 0
    THROW 51000, N'StoreInventoryId không hợp lệ.', 1;

IF NULLIF(LTRIM(RTRIM(@NewPolicyVersion)), N'') IS NULL
    THROW 51000, N'Policy version là bắt buộc.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    UPDATE dbo.StoreInventories WITH (UPDLOCK)
    SET MaxNegativeQty = NULL
    WHERE StoreInventoryId = @StoreInventoryId;

    IF @@ROWCOUNT <> 1
        THROW 51000, N'Không tìm thấy đúng một StoreInventoryId.', 1;

    UPDATE dbo.SystemSettings
    SET SettingValue = @NewPolicyVersion
    WHERE SettingKey = N'inventory_manual_external_export_policy_version';

    IF @@ROWCOUNT <> 1
        THROW 51000, N'Không cập nhật được policy version.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
```

## 8. Xuất âm kho từng bước

### 8.1. Điều kiện để được xin xuất âm

Tất cả điều kiện sau phải đúng:

1. Loại phiếu là `EXPORT`.
2. Purpose là `SALE`, `GIFT`, `DEBT` hoặc `SAMPLE`.
3. Feature đang bật.
4. Approval-required đang là `true`.
5. Lý do xuất âm không rỗng.
6. `ProjectedAfter` không thấp hơn `-EffectiveLimit`.
7. Item/store nằm trong phạm vi được phép.

`ADJUSTMENT_OUT`, `WASTE`, `PRODUCTION_OUT` và `TRANSFER_DISPATCH` không bao giờ đi vào workflow xin âm.

### 8.2. Cách lập phiếu

1. Mở **Phiếu Kho → Phiếu kho → Tạo phiếu mới → Phiếu Xuất Kho**.
2. Chọn cửa hàng trong scope.
3. Chọn `SALE`, `GIFT`, `DEBT` hoặc `SAMPLE`.
4. Chọn nguyên liệu và đơn vị.
5. Nhập đúng số lượng đã xuất thực tế; form không tự giảm về tồn hiện tại cho bốn purpose này.
6. Kiểm tra quy đổi base.
7. Nhập **Lý do xuất âm**, mô tả:
   - Giao dịch thực tế nào đã phát sinh.
   - Tại sao không thể trì hoãn ghi nhận.
   - Tham chiếu đơn hàng/biên bản/công nợ nếu có.
8. Bấm **Tạo & xác nhận**.
9. Server chạy preflight và hiển thị kết quả từng dòng.

### 8.3. Cách đọc preflight

```text
Before         = AvailableQty trước xuất
Issue          = số lượng xuất theo base unit
ProjectedAfter = Before - Issue
Limit          = MaxNegativeQty có hiệu lực
```

Ví dụ:

```text
Before = 3
Issue = 5
ProjectedAfter = -2
Limit = 4
```

Vì `-2 >= -4`, số âm nằm trong hạn mức và có thể trả `ApprovalRequired`.

### 8.4. Ba outcome

| Outcome | Ý nghĩa | Hành động |
|---|---|---|
| `Allowed` | Dòng không âm hoặc approval hợp lệ đã được đánh giá lại. | Tiếp tục xử lý. |
| `ApprovalRequired` | Âm hợp lệ trong limit nhưng chưa có approval. | Gửi yêu cầu và chờ người khác duyệt. |
| `Blocked` | Vi phạm feature, purpose, reason, limit, scope hoặc strict operation. | Không có mutation; sửa nghiệp vụ/dữ liệu. |

Cảnh báo “sắp hết tồn” là low-stock warning, không phải quyền cho phép âm.

### 8.5. Gửi yêu cầu

Khi preflight trả `ApprovalRequired`:

- Document chuyển `PENDING`.
- Hệ thống lưu requester, reason, policy version và từng dòng before/issue/after/limit.
- Không giảm `AvailableQty`.
- Không consume FIFO.
- Không tạo transaction, gap hoặc snapshot.

Không thay đổi trực tiếp document `PENDING` trong database.

### 8.6. Duyệt hoặc từ chối

Nút duyệt không nằm trên form tạo phiếu. Nó chỉ xuất hiện sau khi server đã tạo approval `REQUESTED` và document đã chuyển sang `PENDING`.

#### Tài khoản người tạo

1. Bấm **Tạo & xác nhận**.
2. Kiểm tra kết quả là `PENDING`/`ApprovalRequired`, không phải `Blocked`.
3. Ghi lại mã phiếu hoặc `approvalId`.
4. Đăng xuất hoặc chuyển sang tài khoản người duyệt khác.

Người tạo không được tự duyệt, kể cả có đồng thời role BusinessOwner/SystemAdmin. Khi chính requester mở chi tiết, hệ thống hiển thị **“Bạn không thể tự duyệt phiếu do chính mình tạo”** thay cho hai nút.

#### Tài khoản người duyệt

1. Đăng nhập bằng tài khoản khác requester.
2. Tài khoản phải có một trong các role: **BusinessOwner**, **SystemAdmin**, **AreaManager**, **AccountantWarehouse**.
3. Tài khoản phải có scope tại cửa hàng của phiếu.
4. Mở **Admin → Phiếu Kho → Phiếu kho**.
5. Lọc trạng thái **PENDING** hoặc tìm badge **Chờ duyệt xuất âm**.
6. Bấm **Mở để duyệt**. Nếu danh sách chỉ có biểu tượng con mắt, bấm **Xem chi tiết**.
7. Trong panel **Phê duyệt tồn âm**, đối chiếu lý do, requester, policy version và từng dòng Before/Issue/After/Limit.
8. Chọn **Duyệt xuất âm** (ghi chú tùy chọn) hoặc **Từ chối** (bắt buộc lý do).
9. Xác nhận hộp thoại và chờ trang tải lại trạng thái mới.

Phần approval trong chi tiết hiển thị trạng thái, người đề nghị, người duyệt, policy version, thời điểm đề nghị/duyệt, lý do, review note và Before/Issue/After/Limit từng dòng.

Khi duyệt, server lock và đánh giá lại stock, scope, reason, limit, row version và policy version. Dữ liệu đã thay đổi trả `APPROVAL_STALE`; người dùng phải tải lại và lập lượt xét duyệt mới.

Kết quả:

- Duyệt thành công: approval thành `APPROVED`, document thành `CONFIRMED`, sau đó mới ghi stock transaction/FIFO/cost-gap/snapshot.
- Từ chối thành công: approval thành `REJECTED`, document thành `CANCELLED`, không giảm tồn.
- Request đồng thời hoặc approval đã xử lý: trả `409`, không xử lý lần hai.

#### Vì sao không thấy nút duyệt?

| Hiện tượng | Nguyên nhân | Cách xử lý |
|---|---|---|
| Có panel approval nhưng hiện thông báo không thể tự duyệt | Tài khoản hiện tại chính là requester. | Đăng nhập bằng người duyệt khác. |
| Tài khoản StoreManager không có nút | StoreManager chỉ được tạo/gửi yêu cầu. | Chuyển cho một trong bốn role duyệt được phép. |
| Không thấy phiếu trong danh sách | Người duyệt không có store scope hoặc bộ lọc đang loại phiếu. | Kiểm tra scope và lọc `PENDING`; không mở rộng scope bằng sửa database. |
| Phiếu là `DRAFT` | Chưa submit hoặc chưa phát sinh approval. | Xác nhận draft; chỉ yêu cầu hợp lệ trong limit mới chuyển `PENDING`. |
| Phiếu đã `CONFIRMED`/`CANCELLED` | Approval đã được xử lý. | Xem timeline, approver và review note; không duyệt lại. |
| Phiếu `PENDING` nhưng không có approval | Trạng thái dữ liệu bất thường. | Dừng thao tác, liên hệ DBA/kỹ thuật; không tự thêm approval. |
| Bấm duyệt nhận `APPROVAL_STALE` | Stock, limit, feature hoặc policy version đã đổi. | Tải lại, xử lý lượt cũ theo quy trình và lập yêu cầu mới từ dữ liệu hiện tại. |

Không được đổi setting hoặc sửa row tồn để làm approval cũ vượt qua stale check.

### 8.7. Ví dụ nghiệp vụ

#### Đúng hạn mức

```text
Before = 2
Issue = 5
After = -3
Limit = 3
Kết quả = ApprovalRequired
```

Biên `After = -Limit` là hợp lệ.

#### Vượt hạn mức

```text
Before = 2
Issue = 6
After = -4
Limit = 3
Kết quả = Blocked / MANUAL_NEGATIVE_LIMIT_EXCEEDED
```

#### Item limit ghi đè default

```text
Default limit = 10
Item MaxNegativeQty = 2
After = -3
Kết quả = Blocked vì effective limit là 2
```

#### Feature bị tắt trong lúc chờ duyệt

```text
Lúc gửi: enabled = true
Lúc duyệt: enabled = false, policy version đã đổi
Kết quả = APPROVAL_STALE hoặc feature disabled; không mutate kho
```

## 9. Giá vốn, cost-gap và nhập bù

### 9.1. Vì sao cần cost-gap?

Xuất âm là ghi nhận hàng đã đi ra khi kho chưa có đủ bằng chứng FIFO. Hệ thống không được tự đoán giá cho phần thiếu.

Ví dụ kho có 3 kg, FIFO cost 80.000 đ/kg, nhưng hàng thực tế đã xuất 5 kg:

```text
Before = 3 kg
Issue = 5 kg
After = -2 kg
FIFO coverage = 3 kg × 80.000
Outstanding cost-gap = 2 kg
```

Sau khi được duyệt:

- Ba kg có cost allocation thật.
- Hai kg thiếu cost evidence tạo `InventoryNegativeCostGap`.
- Transaction của phần thiếu được đánh dấu cost incomplete/null phù hợp.
- Không dùng giá client, giá layer cuối hoặc giá 0 làm giá vốn giả.

### 9.2. Nhập bù ít hơn deficit

Kho đang -2 kg, nhập 1,5 kg với cost thật 90.000 đ/kg:

```text
settled = min(1,5; abs(min(-2; 0))) = 1,5
after = -0,5
inbound layer Quantity = 1,5
RemainingQuantity = 0
gap còn mở = 0,5
```

Toàn bộ lượng nhập đã dùng để bù phần xuất trước đó nên chưa có FIFO available mới.

### 9.3. Nhập bù bằng deficit

Kho đang -2 kg, nhập đúng 2 kg:

```text
after = 0
gap đóng
RemainingQuantity = 0
```

### 9.4. Nhập bù lớn hơn deficit

Kho đang -2 kg, nhập 5 kg:

```text
settled = 2
after = 3
gap đóng
RemainingQuantity = 3
```

Settlement xử lý gap cũ nhất trước theo `OccurredAt`, sau đó theo Id.

### 9.5. Xem cost-gap

Mở chi tiết phiếu `CONFIRMED`, chuyển sang tab chứng từ. Bảng cost-gap hiển thị:

- Mã gap.
- Nguồn.
- Trạng thái.
- Số lượng ban đầu.
- Đã settlement.
- Còn outstanding.

Không đóng gap thủ công bằng cách sửa status hoặc outstanding quantity trong database.

## 10. Snapshot, hủy và xuất file

- `DRAFT` chưa có confirmed snapshot.
- `PENDING` chưa có confirmed snapshot.
- `CONFIRMED` có đúng một snapshot tạo sau xử lý thành công.
- PDF/Word chỉ đọc confirmed snapshot.
- Tiêu đề được map đúng Phiếu Nhập, Phiếu Xuất, Phiếu Kiểm Kê hoặc Phiếu Hủy.
- Hủy draft không hoàn tác stock vì draft chưa mutate.
- Từ chối approval chuyển phiếu sang `CANCELLED` trong cùng transaction.
- Sau khi phiếu đã `CONFIRMED`, không sửa/xóa dữ liệu để “hủy”; cần workflow reversal phù hợp.

## 11. Lỗi thường gặp

| HTTP / ReasonCode | Nguyên nhân | Cách xử lý |
|---|---|---|
| `400` | DTO, request key, ngày, identity, đơn vị hoặc số lượng không hợp lệ. | Sửa dữ liệu; không gửi lại nguyên payload lỗi. |
| `403` | Không có role hoặc store/area scope. | Kiểm tra phân quyền; không đổi store bằng client để bypass. |
| `409 / APPROVAL_STALE` | Stock, reason, limit, row version hoặc policy version đã đổi. | Tải lại và lập lượt xét duyệt mới. |
| `409 / IDEMPOTENCY_KEY_REUSED` | Cùng request key nhưng payload khác. | Mở lại form/tạo request key mới cho ý định mới. |
| `422 / MANUAL_NEGATIVE_FEATURE_DISABLED` | Kill switch đang tắt. | Không tự bật; kiểm tra rollout gate. |
| `422 / NEGATIVE_SETTING_INVALID` | Thiếu/sai setting hoặc approval-required bị tắt. | DBA sửa bộ setting và chạy lại truy vấn kiểm tra. |
| `422 / MANUAL_NEGATIVE_PURPOSE_NOT_ALLOWED` | Purpose không thuộc SALE/GIFT/DEBT/SAMPLE. | Chọn đúng nghiệp vụ; không đổi purpose giả. |
| `422 / MANUAL_NEGATIVE_REASON_REQUIRED` | Thiếu lý do xuất âm. | Nhập lý do cụ thể. |
| `422 / MANUAL_NEGATIVE_LIMIT_EXCEEDED` | After thấp hơn `-Limit`. | Giảm lượng xuất, nhập bổ sung hoặc xin thay đổi limit theo quy trình. |
| `422 / SELF_APPROVAL_FORBIDDEN` | Requester đang tự duyệt. | Chuyển cho người duyệt khác. |
| `422 / ADJUSTMENT_OUT_NEGATIVE_FORBIDDEN` | Điều chỉnh giảm làm âm. | Đối soát/kiểm kê hoặc nhập bổ sung. |
| `422 / WASTE_NEGATIVE_FORBIDDEN` | Hủy vượt tồn. | Kiểm tra hàng vật lý và số liệu. |
| `422 / PRODUCTION_OUT_NEGATIVE_FORBIDDEN` | Xuất sản xuất thiếu nguồn. | Bổ sung tồn/cost evidence. |
| `422 / TRANSFER_SOURCE_NEGATIVE_FORBIDDEN` | Dispatch chuyển kho làm âm nguồn. | Không dispatch; bổ sung tồn nguồn. |

Nếu một request không rõ đã thành công hay chưa, kiểm tra document/transaction theo request key trước khi bấm lại. Không tự tạo nhiều request key để ép xử lý trùng.

## 12. Checklist vận hành

### 12.1. Trước khi tạo phiếu

- [ ] Đúng cửa hàng và đúng ngày chứng từ.
- [ ] Đúng loại/purpose.
- [ ] Nguyên liệu đang hoạt động.
- [ ] Đơn vị và conversion factor hợp lệ.
- [ ] Không trùng nguyên liệu giữa các dòng.
- [ ] Số lượng là số thực tế, không phải lượng chênh lệch trừ trường hợp hệ thống tự tính kiểm kê.
- [ ] Nhà cung cấp/đối tác phù hợp.
- [ ] Ghi chú bắt buộc đã nhập.

### 12.2. Trước khi gửi xuất âm

- [ ] Purpose thuộc SALE/GIFT/DEBT/SAMPLE.
- [ ] Giao dịch/hàng giao thực tế đã phát sinh.
- [ ] Lý do xuất âm có tham chiếu kiểm chứng.
- [ ] Before/Issue/After và base unit đã đối chiếu.
- [ ] After không vượt limit.
- [ ] Không dùng xuất âm thay cho kiểm kê hoặc điều chỉnh dữ liệu.

### 12.3. Trước khi duyệt

- [ ] Người duyệt không phải requester.
- [ ] Cửa hàng thuộc scope.
- [ ] Reason và purpose đúng bản chất giao dịch.
- [ ] Từng dòng before/issue/after/limit hợp lý.
- [ ] Policy version hiện tại đúng với ticket rollout.
- [ ] Đã hiểu phần cost-gap có thể phát sinh.

### 12.4. Trước khi bật feature

- [ ] Backup và kế hoạch rollback sẵn sàng.
- [ ] `NegativeInventoryAcceptance.md` xác nhận SQL Server gate đạt.
- [ ] Bốn setting tồn tại và parse hợp lệ.
- [ ] `approval_required=true`.
- [ ] Default limit bằng 0 hoặc đã được phê duyệt rõ ràng.
- [ ] Danh sách item limit đã được duyệt.
- [ ] Policy version mới, duy nhất.
- [ ] Có người gửi và người duyệt khác nhau.
- [ ] Có telemetry/audit và người trực xử lý sự cố.

### 12.5. Khi tắt khẩn cấp

- [ ] Update `enabled=false` trước.
- [ ] Đổi policy version.
- [ ] Xác minh truy vấn trả `false`.
- [ ] Chạy preflight thử và xác nhận bị block.
- [ ] Rà soát approval đang `REQUESTED`.
- [ ] Không xóa approval, transaction, gap hoặc settlement lịch sử.
- [ ] Tiếp tục xử lý nhập bù/cost-gap đã tồn tại.

### 12.6. Sau khi một phiếu âm được xác nhận

- [ ] Document ở `CONFIRMED`.
- [ ] Approval ở `APPROVED`, requester khác approver.
- [ ] `AvailableQty` đúng với after.
- [ ] Transaction và allocation không bị nhân đôi.
- [ ] Gap outstanding đúng phần thiếu FIFO.
- [ ] Snapshot tồn tại đúng một bản.
- [ ] PDF/Word phản ánh đúng lý do và approval.
- [ ] Kế hoạch nhập bù đã được giao cho người phụ trách.

## 13. Khi nào phải dừng và liên hệ kỹ thuật

Dừng thao tác, không cố gọi API hoặc sửa trực tiếp dữ liệu nghiệp vụ khi gặp một trong các trường hợp:

- Setting thiếu, trùng hoặc `NEGATIVE_SETTING_INVALID` lặp lại.
- AvailableQty không khớp ledger.
- FIFO layer remaining không khớp tồn.
- Gap outstanding lớn hơn deficit thực tế.
- Approval requester/approver hoặc scope sai.
- Phiếu `PENDING` đã có transaction/allocation/snapshot.
- Cùng request tạo nhiều movement/snapshot.
- Nhập bù tạo FIFO available dù kho vẫn âm.
- SQL deadlock/concurrency lặp lại.
- Không xác định database/migration history đang vận hành.

Liên hệ DBA, kế toán kho và kỹ thuật ứng dụng. Giữ nguyên dữ liệu để điều tra; không clamp số âm về 0 và không xóa audit trail.

## 14. Tài liệu liên quan

- [Bảng nghiệm thu và trạng thái SQL Server gate](Scripts/NegativeInventoryAcceptance.md)
- `Data/Configurations/Systems/SystemSettingConfiguration.cs`
- `Application/Services/Inventories/InventoryIssuePolicy.cs`
- `Application/Services/Inventories/InventoryIssueSettingsProvider.cs`

Tài liệu này phản ánh form và policy hiện tại. Nếu UI, setting, purpose hoặc approval workflow thay đổi, phải cập nhật hướng dẫn trong cùng pull request.

## 15. Xác định cửa hàng và item được phép xin xuất âm

### 15.1. Điều kiện kết luận

Không thể chỉ nhìn `MaxNegativeQty` để kết luận một item đang được phép xuất âm. Một store-item chỉ hiển thị **Có thể xin xuất âm** khi đồng thời thỏa mãn:

1. `inventory_manual_external_export_negative_enabled = true`.
2. `inventory_manual_external_export_approval_required = true`.
3. Cả bốn setting tồn tại, parse hợp lệ và policy version không rỗng.
4. Hạn mức hiệu lực lớn hơn 0.
5. Cửa hàng và item còn hoạt động.

Hạn mức hiệu lực được xác định như sau:

| `StoreInventories.MaxNegativeQty` | Ý nghĩa |
|---|---|
| `NULL` | Dùng `inventory_manual_external_export_default_max_negative_quantity`. |
| `0` | Chặn riêng item, kể cả default limit lớn hơn 0. |
| Lớn hơn `0` | Dùng hạn mức riêng của store-item. |

Trạng thái **Có thể xin xuất âm** chỉ có nghĩa item đủ điều kiện cấu hình. Phiếu cụ thể vẫn phải là `EXPORT` với `SALE`, `GIFT`, `DEBT` hoặc `SAMPLE`, có lý do xuất âm, không vượt hạn mức và được một người khác phê duyệt.

### 15.2. Dữ liệu mẫu Store 3 / Ingredient 2 đang có trong code

`StoreConfiguration` hiện có bản ghi sau. Bảng này chỉ mô tả dữ liệu đang có; tài liệu không thay đổi seed hoặc database:

| Trường | Giá trị hiện tại |
|---|---|
| `StoreInventoryId` | `4` |
| `StoreId` | `3` — CafeChain Dĩ An |
| `IngredientId` | `2` — Sữa đặc demo lon 380 ml (`ING00002`) |
| Base unit | `ml` |
| `AvailableQty` | `60.000` |
| `ReservedQty` | `0.000` |
| `MaxNegativeQty` | `NULL` |
| Hạn mức mặc định được seed | `0` |
| Feature mặc định | `false` |
| Kết quả hiện tại | Không được xin xuất âm |

`MaxNegativeQty = NULL` không có nghĩa là được âm không giới hạn. Bản ghi này đang kế thừa default limit bằng 0 nên bị chặn. Dữ liệu trong bảng trên không tự cập nhật database đang vận hành.

### 15.3. Màn hình bật/tắt và đặt hạn mức

Chỉ **Chủ doanh nghiệp** và **Quản trị hệ thống** được sử dụng màn hình này.

1. Mở **Admin → Cài đặt hệ thống**.
2. Chọn tab **Kho & tồn âm**.
3. Kiểm tra badge trạng thái, policy version và số approval đang chờ.
4. Dùng bộ lọc chọn Store `3`.
5. Tìm mã `ING00002`, tên “Sữa đặc demo lon 380 ml” hoặc Item ID `2`.
6. Tại cột **Chế độ**, chọn **Hạn mức riêng**.
7. Nhập `5.000` tại cột **Hạn mức riêng**.
8. Bật switch **Cho phép gửi yêu cầu xuất âm kho**.
9. Nhấn **Lưu cấu hình âm kho**, đọc cảnh báo và xác nhận.
10. Tải lại tab và xác minh item hiển thị **Có thể xin xuất âm tối đa 5.000 ml**.

`5.000` ở ví dụ này là **5 ml theo base unit**, không phải 5 lon. Khi lập phiếu bằng đơn vị khác, server quy đổi về base unit trước khi so hạn mức.

Ba chế độ item trên màn hình:

- **Chặn**: lưu `MaxNegativeQty = 0`.
- **Theo mặc định**: lưu `MaxNegativeQty = NULL`.
- **Hạn mức riêng**: bắt buộc nhập số lớn hơn 0, tối đa 3 chữ số thập phân.

Khi feature bị tắt, item vẫn bị chặn dù hạn mức riêng đang là 5. Bật feature chỉ mở quy trình maker-checker; không tự duyệt hoặc tự xác nhận phiếu, không cho phép tự duyệt và không mở âm kho cho `WASTE`, `ADJUSTMENT_OUT`, `PRODUCTION_OUT` hoặc `TRANSFER_DISPATCH`.

Mỗi lần feature, default limit hoặc item limit thực sự thay đổi, server tự tạo policy version mới. Approval đang `REQUESTED` theo version cũ có thể trả `APPROVAL_STALE` và phải được tải lại/lập lượt xét duyệt mới.

### 15.4. Truy vấn chỉ đọc để đối chiếu toàn bộ store-item

Truy vấn sau không cập nhật dữ liệu. Nó kết hợp global setting với hạn mức từng item để đưa ra kết luận cấu hình:

```sql
WITH NegativeSettings AS
(
    SELECT
        TRY_CONVERT(bit, MAX(CASE
            WHEN SettingKey = N'inventory_manual_external_export_negative_enabled'
            THEN SettingValue END)) AS FeatureEnabled,
        TRY_CONVERT(bit, MAX(CASE
            WHEN SettingKey = N'inventory_manual_external_export_approval_required'
            THEN SettingValue END)) AS ApprovalRequired,
        TRY_CONVERT(decimal(18,3), MAX(CASE
            WHEN SettingKey = N'inventory_manual_external_export_default_max_negative_quantity'
            THEN SettingValue END)) AS DefaultMaxNegativeQty,
        MAX(CASE
            WHEN SettingKey = N'inventory_manual_external_export_policy_version'
            THEN SettingValue END) AS PolicyVersion,
        COUNT(DISTINCT CASE
            WHEN SettingKey IN
            (
                N'inventory_manual_external_export_negative_enabled',
                N'inventory_manual_external_export_approval_required',
                N'inventory_manual_external_export_default_max_negative_quantity',
                N'inventory_manual_external_export_policy_version'
            )
            THEN SettingKey END) AS SettingKeyCount
    FROM dbo.SystemSettings
), InventoryEligibility AS
(
    SELECT
        si.StoreInventoryId,
        si.StoreId,
        s.Name AS StoreName,
        si.IngredientId,
        si.PreparedItemId,
        COALESCE(i.Code, pi.Code) AS ItemCode,
        COALESCE(i.Name, pi.Name) AS ItemName,
        u.UnitCode AS BaseUnit,
        si.AvailableQty,
        si.ReservedQty,
        si.MaxNegativeQty,
        ns.DefaultMaxNegativeQty,
        COALESCE(si.MaxNegativeQty, ns.DefaultMaxNegativeQty) AS EffectiveMaxNegativeQty,
        ns.FeatureEnabled,
        ns.ApprovalRequired,
        ns.PolicyVersion,
        ns.SettingKeyCount,
        s.Active AS StoreActive,
        COALESCE(i.Active, pi.Active) AS ItemActive
    FROM dbo.StoreInventories si
    INNER JOIN dbo.Stores s ON s.StoreId = si.StoreId
    LEFT JOIN dbo.Ingredients i ON i.IngredientId = si.IngredientId
    LEFT JOIN dbo.PreparedItems pi ON pi.PreparedItemId = si.PreparedItemId
    LEFT JOIN dbo.Units u ON u.UnitId = COALESCE(i.BaseUnitId, pi.BaseUnitId)
    CROSS JOIN NegativeSettings ns
    WHERE
        (si.IngredientId IS NOT NULL AND si.PreparedItemId IS NULL)
        OR (si.IngredientId IS NULL AND si.PreparedItemId IS NOT NULL)
)
SELECT
    StoreInventoryId,
    StoreId,
    StoreName,
    IngredientId,
    PreparedItemId,
    ItemCode,
    ItemName,
    BaseUnit,
    AvailableQty,
    ReservedQty,
    MaxNegativeQty,
    DefaultMaxNegativeQty,
    EffectiveMaxNegativeQty,
    FeatureEnabled,
    ApprovalRequired,
    PolicyVersion,
    CASE
        WHEN SettingKeyCount <> 4
             OR FeatureEnabled IS NULL
             OR ApprovalRequired IS NULL
             OR DefaultMaxNegativeQty IS NULL
             OR DefaultMaxNegativeQty < 0
             OR NULLIF(LTRIM(RTRIM(PolicyVersion)), N'') IS NULL
            THEN N'Cấu hình lỗi - fail closed'
        WHEN ApprovalRequired <> 1
            THEN N'Cấu hình lỗi - approval phải bật'
        WHEN StoreActive <> 1 OR ItemActive <> 1
            THEN N'Item/cửa hàng ngừng hoạt động'
        WHEN FeatureEnabled <> 1
            THEN N'Feature đang tắt'
        WHEN EffectiveMaxNegativeQty <= 0
            THEN N'Bị chặn'
        ELSE N'Có thể xin xuất âm'
    END AS Eligibility
FROM InventoryEligibility
ORDER BY StoreId, IngredientId, PreparedItemId, StoreInventoryId;
```

Để chỉ kiểm tra ví dụ trong chương này, thêm điều kiện vào truy vấn cuối:

```sql
WHERE StoreId = 3 AND IngredientId = 2
```

Không sửa trực tiếp approval, transaction, cost gap hoặc snapshot để “mở” item. Nếu màn hình báo **Cấu hình lỗi**, dừng bật feature và liên hệ DBA để kiểm tra đủ bốn setting. Kill switch là tắt switch global và lưu; provider không cache bốn setting này nên request kế tiếp sẽ đọc trạng thái mới.
