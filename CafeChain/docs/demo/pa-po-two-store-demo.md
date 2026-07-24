# Demo PA/PO hai chi nhánh

## 1. Mục tiêu

Runbook này dùng để nghiệm thu luồng mua hàng từ đề nghị mua (PA) đến đơn đặt hàng gộp, nhận hàng theo từng chi nhánh và tài liệu PDF gửi Nhà cung cấp.

Kịch bản chuẩn:

| Nội dung | Chi nhánh A | Chi nhánh B | Toàn hệ thống |
|---|---:|---:|---:|
| Nhu cầu thực | 2.300g | 1.400g | 3.700g |
| Quy cách mua | 1.000g/gói | 1.000g/gói | 1.000g/gói |
| Số gói sau làm tròn | 3 | 2 | 5 |
| Lượng đặt | 3.000g | 2.000g | 5.000g |
| Dư do làm tròn | 700g | 600g | 1.300g |
| Giá một gói | 160.000đ | 160.000đ | 160.000đ |
| Thành tiền | 480.000đ | 320.000đ | 800.000đ |

Công thức bắt buộc:

```text
Số gói chi nhánh = ceiling(Nhu cầu còn lại / 1.000g)
Lượng đặt chi nhánh = Số gói chi nhánh * 1.000g
Tiền chi nhánh = Số gói chi nhánh * 160.000đ
```

Hệ thống làm tròn theo từng chi nhánh trước khi cộng vào PO gộp. Không được tính `ceiling((2.300 + 1.400) / 1.000) = 4 gói`, vì cách đó làm mất nghĩa vụ giao nguyên gói cho từng nơi.

## 2. Phạm vi và vai trò

| Bước | Vai trò |
|---|---|
| Tạo/gửi PA | Quản lý chi nhánh |
| Xem xét PA, tổng hợp và tạo PO gộp | Kế toán/kho |
| Duyệt PO gộp | Chủ doanh nghiệp |
| Tạo PDF, tải/in, ghi nhận đã gửi | Kế toán/kho hoặc Chủ doanh nghiệp |
| Nhận hàng tại chi nhánh | Quản lý đúng chi nhánh hoặc vai trò được cấp quyền kho |

Không dùng tài khoản của Chi nhánh B để nhận đơn con của Chi nhánh A. Kết quả đúng là bị từ chối truy cập.

## 3. Chuẩn bị dữ liệu

Không chạy seed hoặc script ghi dữ liệu trên database thật chỉ để phục vụ demo. Chọn dữ liệu hiện có theo các điều kiện dưới đây, hoặc tạo qua UI nghiệp vụ.

### 3.1. Danh sách ID cần ghi lại

| Biến demo | Giá trị đã chọn |
|---|---|
| `STORE_A_ID` | |
| `STORE_B_ID` | |
| `INGREDIENT_ID` | |
| `SUPPLIER_ID` | |
| `INGREDIENT_SUPPLIER_ID` | |
| `PA_LINE_A_ID` | |
| `PA_LINE_B_ID` | |
| `BATCH_ID` sau khi tạo | |
| `CHILD_PO_A_ID` sau khi duyệt | |
| `CHILD_PO_B_ID` sau khi duyệt | |

Hai PA phải thuộc hai chi nhánh khác nhau nhưng cùng nguyên liệu, cùng Nhà cung cấp và cùng Supplier SKU (`IngredientSupplierId`).

### 3.2. Contract dữ liệu bắt buộc

- Nguyên liệu đang hoạt động, base unit là `g`.
- Nhà cung cấp đang hoạt động và được gán cho cả hai chi nhánh.
- Supplier SKU đang hoạt động.
- `IngredientSupplier.PackageQuantity = 1000`.
- Đơn vị của Supplier SKU cùng dimension với base unit.
- `IngredientSupplier.CurrentPrice = 160000`.
- `MinimumOrderPackageCount <= 1`.
- PA A ở trạng thái đã xem xét, còn nhu cầu mua `2300g`.
- PA B ở trạng thái đã xem xét, còn nhu cầu mua `1400g`.
- Không PA line nào đã được phân bổ vào PO khác.

Nếu một điều kiện không đúng, dừng demo và chọn lại dữ liệu. Không sửa trực tiếp quantity trong database để ép kết quả.

## 4. Tạo và duyệt PA

1. Đăng nhập bằng Quản lý Chi nhánh A.
2. Vào **Kho & Cung ứng > Đề nghị mua hàng theo chi nhánh**.
3. Tạo PA từ yêu cầu nhập hàng của nguyên liệu đã chọn với nhu cầu `2.300g`.
4. Lưu nháp rồi chọn **Gửi Kế toán**.
5. Lặp lại tại Chi nhánh B với nhu cầu `1.400g`.
6. Đăng nhập bằng Kế toán/kho, mở từng PA và chọn **Bắt đầu xem xét**.
7. Ghi lại `PurchaseAdviceLineId` của hai dòng dùng cho demo.

Checkpoint:

- Hai PA có hai `StoreId` khác nhau.
- Cùng `IngredientId`.
- `RequestedPurchaseBaseQuantity` lần lượt là `2300` và `1400`.
- Trạng thái đủ điều kiện xuất hiện tại màn hình tổng hợp.

## 5. Tổng hợp thành PO Nhà cung cấp

1. Vào `/Admin/AdminPurchaseAdviceConsolidation`.
2. Chọn đúng hai PA line đã ghi ở phần chuẩn bị.
3. Chọn cùng Supplier SKU có quy cách `1.000g/gói`, giá `160.000đ/gói`.
4. Chọn **Xem trước tổng hợp**.
5. Đối chiếu trước khi tạo:
   - Chi nhánh A: 3 gói, đặt 3.000g, phủ 2.300g, dư 700g.
   - Chi nhánh B: 2 gói, đặt 2.000g, phủ 1.400g, dư 600g.
   - Tổng: 5 gói, 5.000g, 800.000đ.
6. Tạo đơn đặt hàng gộp và ghi lại `BATCH_ID`.

Checkpoint:

- Chỉ có một `PurchaseOrderBatch`.
- Có đúng một master line cho Supplier SKU.
- Có hai allocation và hai child PO, mỗi child PO thuộc đúng một chi nhánh.
- Giá gói được snapshot tại thời điểm tạo; thay đổi giá Supplier SKU sau đó không được âm thầm sửa PO đã tạo.
- PA chỉ ghi nhận đã phân bổ theo nhu cầu thực `2.300g + 1.400g`, không ghi `5.000g`.

## 6. Duyệt PO gộp

1. Đăng nhập bằng Chủ doanh nghiệp.
2. Vào **Kho & Cung ứng > Đơn đặt hàng gộp**.
3. Mở batch vừa tạo và chọn **Duyệt đơn đặt hàng gộp**.
4. Ghi lại ID của hai child PO.

Checkpoint:

- Batch chuyển sang trạng thái đã duyệt.
- Child PO A có lượng đặt `3.000g`, tổng tiền `480.000đ`.
- Child PO B có lượng đặt `2.000g`, tổng tiền `320.000đ`.
- Tổng batch là `5.000g` và `800.000đ`.
- Nhấn lại hành động với request cũ không được tạo thêm batch, allocation hoặc child PO.

## 7. PDF, tải xuống, in và bằng chứng gửi

1. Trong chi tiết batch đã duyệt, chọn **Xuất PDF**.
2. Kiểm tra revision đầu là `v1`.
3. Kiểm tra tên file theo mẫu:

```text
PO-<BatchNumber>-<SupplierCodeOrName>-v1.pdf
```

4. Chọn **Tải PDF** và mở file.
5. Chọn **Mở để in PDF**; trình duyệt phải mở PDF inline để dùng lệnh in.
6. Đối chiếu nội dung PDF:
   - CafeChain và mã PO;
   - Nhà cung cấp, mã số thuế và thông tin liên hệ nếu dữ liệu có cấu hình;
   - ngày tạo/duyệt, người lập/duyệt;
   - nguyên liệu/Supplier SKU;
   - quy cách 1.000g, 5 gói, giá gói 160.000đ, tổng 800.000đ;
   - phân bổ giao 3.000g cho A và 2.000g cho B;
   - địa chỉ/ngày cần hàng, ghi chú/điều khoản.
7. Chọn kênh **Zalo**, **Email** hoặc **Khác**.
8. Nhập bằng chứng, ví dụ `Gửi nhóm Zalo NCC lúc 09:30, người nhận Nguyễn Văn A`.
9. Chọn **Đánh dấu đã gửi Nhà cung cấp**.

Checkpoint:

- Sent evidence lưu đúng revision, kênh, người thao tác, thời gian và ghi chú.
- Đánh dấu lại bằng cùng idempotency key không tạo lần gửi logic thứ hai.
- Batch đã hủy không được ghi nhận gửi.
- UI chỉ xác nhận đã ghi nhận gửi; không khẳng định Nhà cung cấp đã nhận hoặc xác nhận.

### Kiểm tra revision

- Tạo PDF lại khi snapshot không đổi phải trả lại revision hiện có.
- Khi dữ liệu PO thay đổi qua một nghiệp vụ hợp lệ, lần tạo sau sinh `v2`; `v1` thành superseded và vẫn tải được.
- Không sửa file `v1` tại chỗ.
- Vì batch đã duyệt không có màn hình sửa tùy ý, có thể dùng automated test ở mục 10 để trình diễn contract revision mà không sửa database demo trực tiếp.

## 8. Nhận hàng riêng theo chi nhánh

### 8.1. Nhận tại Chi nhánh A

1. Đăng nhập bằng Quản lý Chi nhánh A.
2. Mở child PO A và chọn nhận hàng.
3. Kiểm đếm, lưu phiếu nháp, sau đó xác nhận lượng đạt `3.000g`.
4. Mở lại batch.

Kết quả mong đợi:

- Tồn kho chỉ tăng tại Chi nhánh A.
- Allocation A đã nhận `3.000g`, còn `0`.
- Child PO A hoàn tất.
- Allocation B vẫn đã nhận `0`, còn `2.000g`.
- Child PO B chưa hoàn tất.
- Batch ở trạng thái nhận một phần.
- Replay xác nhận cùng phiếu không tăng kho hoặc posting lần hai.

### 8.2. Nhận tại Chi nhánh B

1. Đăng nhập bằng Quản lý Chi nhánh B.
2. Mở child PO B và xác nhận lượng đạt `2.000g`.
3. Mở lại batch.

Kết quả mong đợi:

- Tồn kho Chi nhánh B tăng độc lập `2.000g`.
- Allocation B còn `0`.
- Cả hai child PO hoàn tất.
- Batch hoàn tất.
- PA A ghi nhận hoàn thành tối đa theo nhu cầu `2.300g`.
- PA B ghi nhận hoàn thành tối đa theo nhu cầu `1.400g`.
- `ActualEndingCash`, POS payment và các module ngoài procurement không bị tác động.

## 9. Trường hợp âm phải kiểm tra

- Package count bằng `0`, số âm hoặc số lẻ bị từ chối.
- Đơn vị mua khác dimension với base unit bị từ chối.
- Phân bổ vượt nhu cầu còn lại bị từ chối nếu không có override hợp lệ.
- Cùng PA allocation/request key không tạo PO trùng.
- Quản lý A không nhận child PO B và ngược lại.
- Hủy batch sau khi đã nhận hàng bị từ chối.
- Hủy trước nhận hàng không tạo Inventory Transaction.
- Tạo PDF trước khi duyệt bị từ chối.
- Ghi nhận gửi khi chưa có snapshot PDF bị từ chối.

## 10. Automated acceptance

Chạy tại workspace root:

```powershell
dotnet build .\CafeChain\CafeChain.slnx --no-restore

dotnet test .\CafeChain\CafeChain.slnx --no-restore --no-build `
  --filter "FullyQualifiedName~PurchasePackMathIssue185Tests|FullyQualifiedName~PurchaseOrderBatchIssue186Tests|FullyQualifiedName~PurchaseOrderBatchPdfIssue187Tests|FullyQualifiedName~PurchaseOrderBatchPdfSqlServerIssue187Tests|FullyQualifiedName~PurchaseOrderBatchUiIssue188Tests|FullyQualifiedName~PurchaseAdviceBatchPoE2EIssue189Tests|FullyQualifiedName~PurchaseAdviceFulfillmentIssue193Tests"

dotnet test .\CafeChain\CafeChain.slnx --no-restore --no-build

git diff --check
git status --short
git rev-list --left-right --count HEAD...origin/feature/POS
```

Các test contract trọng tâm:

- `TwoStorePaConsolidatesBySupplierSku_AndRoundsEachStoreAllocation`
- `SqlServer_PerStoreReceipt_UpdatesOnlyItsAllocationAndMasterProgress`
- `SqlServer_E2E_PaBatchPdfZaloAndConcurrentReceiving_AreConsistent`
- `BatchPdf_SentRevisionCannotBeOverwritten`
- `BatchSend_SupportsOtherChannelAndRejectsCancelledBatch`

## 11. Bằng chứng nghiệm thu

Lưu các bằng chứng sau trong comment Owner acceptance:

- Hai mã PA và hai Store ID.
- Mã batch và hai mã child PO.
- Ảnh preview thể hiện 3+2 gói.
- Ảnh chi tiết batch thể hiện tổng 5.000g / 800.000đ.
- File PDF `v1` và ảnh màn hình print preview.
- Kênh, người gửi, thời gian, ghi chú bằng chứng.
- Ảnh sau khi nhận A: batch nhận một phần, B chưa đổi.
- Ảnh sau khi nhận B: batch hoàn tất.
- Kết quả build/test và commit hash.

## 12. Tiêu chí PASS

Demo chỉ PASS khi đồng thời thỏa:

- `PA_PO_UNIT_CONTRACT_NORMALIZED`
- `TWO_STORE_CONSOLIDATION_IMPLEMENTED`
- `PER_STORE_RECEIVING_ENFORCED`
- `SUPPLIER_PO_PDF_IMPLEMENTED`
- Không có allocation, receipt posting, inventory transaction hoặc sent evidence bị nhân đôi khi replay.
- Local branch đồng bộ với `origin/feature/POS`.
- Không tạo PR và không merge.
