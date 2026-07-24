# Audit contract PA - PO - nhận hàng

Phạm vi: issue #216, luồng Kho & Cung ứng từ cảnh báo tồn đến nhận hàng tại chi nhánh.

## Kết luận ngắn

Hệ thống đã có đúng các khối nền tảng:

- `PurchaseAdviceLine` lưu nhu cầu mua theo đơn vị tồn kho chuẩn.
- `IngredientSupplier` lưu quy cách vật lý và giá theo một gói mua.
- `PhysicalUnitConversionService` kiểm tra dimension và quy đổi kg/g, l/ml.
- `PurchaseOrderBatch` là master PO theo Nhà cung cấp.
- Mỗi cửa hàng nhận một child `PurchaseOrder`.
- `PurchaseOrderLineAllocation` truy vết PA line đến child PO line.
- `BranchReceipt` thuộc một cửa hàng và chỉ khi `CONFIRMED` mới ghi tồn/FIFO.
- `PurchaseOrderBatchDocumentRevision` lưu snapshot PDF bất biến, version và bằng chứng gửi.

Không cần migration để sửa contract lượng mua trong scope hiện tại. Các field đang có đủ để
tách rõ:

- nhu cầu được phủ của PA;
- số gói nguyên;
- lượng đặt sau làm tròn;
- lượng dư do quy cách.

Việc sửa phải thay đổi ý nghĩa aggregate trong service/DTO, không được thêm string hack hoặc
đổi schema chỉ để né validation.

## Quantity và UOM hiện tại

| Chứng từ/bảng | Field chính | Đơn vị và ý nghĩa hiện tại |
| --- | --- | --- |
| `StoreInventories` | `AvailableQty`, `ReservedQty`, `MinStockLevel` | Base UOM của Ingredient/PreparedItem. Tồn khả dụng dùng `AvailableQty - ReservedQty`. |
| `StockAlerts` | `CurrentQtySnapshot`, `ThresholdSnapshot` | Base UOM, snapshot tại thời điểm cảnh báo/báo thiếu. |
| `RestockRequests` | `RequestedQuantity`, `SuggestedQuantity` | Base UOM ngầm định theo identity của dòng tồn. |
| `PurchaseAdviceLines` | `RequestedPurchaseBaseQuantity` | Base UOM, có `BaseUnitId` rõ ràng. |
| `PurchaseAdviceLines` | `AllocatedToPoBaseQuantity` | Hiện đang cộng lượng đặt sau quy đổi; cần chuẩn hóa thành lượng nhu cầu PA đã được PO phủ, không vượt requested. |
| `IngredientSuppliers` | `PackageQuantity`, `UnitId` | Nội dung vật lý của một gói mua, ví dụ 1.000 Gram. |
| `IngredientSuppliers` | `CurrentPrice` | Giá của một gói mua, không phải giá/kg hay giá/base unit. |
| `PurchaseOrderLines` | `PackageCount` | Số gói. Field là decimal vì schema legacy, nhưng domain mới phải bắt buộc số nguyên dương. |
| `PurchaseOrderLines` | `PackageQuantitySnapshot`, `PackageUnitIdSnapshot` | Snapshot quy cách Nhà cung cấp lúc đặt. |
| `PurchaseOrderLines` | `OrderedBaseQuantity` | Lượng đặt thực tế sau quy đổi sang base UOM. |
| `PurchaseOrderBatchLines` | `TotalPackageCount`, `TotalBaseQuantity` | Tổng số gói và tổng lượng đặt của master PO. |
| `PurchaseOrderLineAllocations` | `AllocatedPackageQuantity`, `AllocatedBaseQuantity` | Số gói/lượng đặt giao riêng cho một cửa hàng. |
| `BranchReceiptLines` | `InputQuantity`, `InputUnitId` | Lượng thực giao theo đơn vị nhập. |
| `BranchReceiptLines` | `ReceivedBaseQuantity`, `RejectedBaseQuantity`, `BaseUnitId` | Lượng chấp nhận/từ chối đã quy đổi về base UOM. |

## Trả lời các câu hỏi bắt buộc

### Số `5` trên form PO là gì?

Đó là `RestockRequestId`, tức tham chiếu **Yêu cầu nhập hàng #5**, không phải quantity.
Form standalone hiện render nó bằng ô số có thể sửa trong
`Areas/Admin/Views/AdminPurchaseOrders/Create.cshtml`; cách hiển thị này gây hiểu nhầm và
phải đổi thành reference read-only/selector có ngữ cảnh.

### Pack size lưu ở đâu?

`IngredientSupplier.PackageQuantity` + `IngredientSupplier.UnitId`. Ví dụ:
`PackageQuantity = 1000`, Unit = Gram nghĩa là một gói chứa 1.000 Gram.

### Giá đang là per pack, per kg hay per base unit?

`IngredientSupplier.CurrentPrice` và `PurchaseOrderLine.PackagePriceSnapshot` là **giá một
gói mua**. Thành tiền là `PackageCount * PackagePriceSnapshot`.

### `20.000` trong PO detail là Gram hay Kilogram?

Đó là `PurchaseOrderLine.OrderedBaseQuantity`, tức base UOM của Ingredient. UI hiện tách
`PackageUnitName` khỏi lượng quy đổi và không luôn hiển thị tên base UOM cạnh `20.000`, nên
người dùng có thể đọc nhầm thành Kilogram. DTO/detail cần mang và hiển thị `BaseUnitName`.

### Có dimension validation không?

Có. `PhysicalUnitConversionService`:

- từ chối Unit inactive/không tồn tại;
- từ chối đơn vị đếm/đóng gói (`UnitType.Dem`) khi quy đổi vật lý;
- từ chối cross-dimension;
- chỉ hỗ trợ khối lượng/thể tích;
- dùng registry cho kg/g và l/ml.

### Có thể mua fractional pack không?

Hiện tại:

- consolidation UI đã dùng `step="1"` nhưng backend chỉ kiểm tra `PackageCount > 0`;
- standalone PO UI dùng `step="0.001"` và service cũng chỉ kiểm tra lớn hơn 0.

Vì vậy backend vẫn chấp nhận fractional pack. Contract mục tiêu bắt buộc `PackageCount` là
số nguyên dương ở cả preview, create batch và standalone PO.

### PA allocation lưu base qty hay pack count?

Lưu cả hai:

- `AllocatedBaseQuantity`: lượng đặt cho allocation theo base UOM;
- `AllocatedPackageQuantity`: số gói của allocation.

`PurchaseAdviceLine.AllocatedToPoBaseQuantity` chỉ nên là lượng **nhu cầu được phủ**, được
cap theo remaining của PA. Nó không được tiếp tục đồng nghĩa với lượng đặt sau làm tròn.

### Grouped PO và child PO quan hệ thế nào?

`PurchaseOrderBatch` là master PO theo một Supplier. `PurchaseOrder.PurchaseOrderBatchId`
trỏ về master. `PurchaseOrderBatchService` nhóm allocations theo Store và tạo một child
`PurchaseOrder` cho mỗi Store; mỗi allocation trỏ đồng thời batch line, child PO và child
PO line.

### Receiving trừ còn phải giao theo base qty hay pack count?

Theo base qty:

- child `PurchaseOrderLine.OrderedBaseQuantity` là nghĩa vụ giao cho cửa hàng;
- receipt posting cộng `AcceptedBaseQuantity`/`RejectedBaseQuantity`;
- remaining được tính từ ordered base quantity và các posting;
- `BranchReceipt` có `StoreId` và chỉ nhận line thuộc child PO của chính Store đó.

Pack count là contract đặt hàng; base qty là contract nhận và ghi tồn.

### Có PDF library/template hiện tại không?

Có:

- package `QuestPDF`;
- `PurchaseOrderBatchPdfRenderer`;
- `PurchaseOrderBatchDocumentService`;
- `PurchaseOrderBatchDocumentRevision` lưu snapshot JSON, hash, revision, file và sent
  evidence.

Khoảng trống còn lại: tên file chưa theo `PO-<code>-<supplier>-v<version>.pdf`, chưa có nút
In rõ ràng, kênh gửi chưa có `OTHER`, và success message đang hard-code Zalo.

### Vì sao tồn `9.970g`, threshold `2.000g` vẫn hiện “Sắp hết”?

Manual shortage đi qua `StockShortageReportService.MapTypeSeverity`. Hàm này chỉ xét:

- `available <= 0` => `OUT_OF_STOCK/URGENT`;
- còn lại => `LOW_STOCK/WARNING`.

Nó không so sánh với `MinStockLevel`, nên mọi manual report có tồn dương đều bị gắn nhãn
“Sắp hết”, kể cả khi đang trên ngưỡng.

### Auto alert và manual report có dùng chung severity không?

Có. Cả hai đang ghi vào `StockAlert.AlertType`/`Severity`, nhưng rule sinh trạng thái khác
nhau. Auto alert tính theo threshold; manual report bỏ qua threshold rồi ép `LOW_STOCK`.
Mục tiêu là giữ chung entity nhưng phân biệt bằng `Source` và display/status contract:

- auto dưới ngưỡng: `LOW_STOCK` hoặc `OUT_OF_STOCK`;
- manual trên ngưỡng: “Báo thiếu thủ công - Cần xác minh”;
- không tự tạo Restock Request khi auto suggestion bằng 0;
- manual override bắt buộc reason, evidence/note và target/forecast.

## Contract mục tiêu

### Nhu cầu và làm tròn theo từng cửa hàng

Vì child PO và BranchReceipt đều Store-scoped, hệ thống phải làm tròn ở từng allocation:

```text
RemainingDemandBase = max(0, RequestedBase - CoveredDemandBase - ClosedBase)
SuggestedPackCount = ceil(RemainingDemandBase / PackSizeBase)
OrderedBase = SuggestedPackCount * PackSizeBase
DemandCoveredBase = min(RemainingDemandBase, OrderedBase)
RoundingSurplusBase = OrderedBase - DemandCoveredBase
```

Master line:

```text
TotalPackageCount = sum(StorePackageCount)
TotalBaseQuantity = sum(StoreOrderedBase)
LineTotal = TotalPackageCount * PackagePriceSnapshot
```

Với 2.300g và 1.400g, pack 1.000g:

- Store A: 3 pack, ordered 3.000g, surplus 700g;
- Store B: 2 pack, ordered 2.000g, surplus 600g;
- master: 5 pack, 5.000g, không phải 4 pack/4.000g.

### Aggregate và idempotency

- `PurchaseOrderLineAllocation.AllocatedBaseQuantity`: lượng đặt thực tế sau làm tròn.
- `PurchaseAdviceLine.AllocatedToPoBaseQuantity`: demand covered, cap tại requested.
- Tạo batch phải khóa PA lines và dùng request key như hiện tại.
- Hủy batch phải recompute demand-covered từ allocations còn hiệu lực; không trừ mù lượng
  rounded.
- Back-post Accepted/Closed vẫn truy vết allocation/receipt ledger, nhưng aggregate PA phải
  cap theo nhu cầu để lượng dư quy cách không làm PA vượt requested.
- Duplicate request/allocation không được tạo PO hoặc cộng aggregate lần hai.

## Dependency map

```text
StoreInventory
  -> StockAlert
  -> RestockRequest
  -> PurchaseAdvice / PurchaseAdviceLine
  -> PurchaseAdviceConsolidation preview
  -> PurchaseOrderBatch (master supplier PO)
       -> PurchaseOrder per Store
          -> PurchaseOrderLine
             -> PurchaseOrderLineAllocation
                -> BranchReceipt per Store
                   -> PurchaseOrderReceiptPosting
                   -> Inventory transaction/FIFO
       -> PurchaseOrderBatchDocumentRevision
```

Không module nào trước `BranchReceipt.CONFIRMED` được tăng tồn hoặc tạo công nợ.

## Exact file plan

### UOM, pack và rounding

- `Application/DTOs/Admin/Procurement/PurchaseAdviceConsolidationDtos.cs`
- `Application/DTOs/Admin/Procurement/PurchaseOrderDtos.cs`
- `Application/Services/Inventories/PurchaseAdviceConsolidationService.cs`
- `Application/Services/Inventories/PurchaseOrderService.cs`
- `Application/Services/Inventories/PurchaseOrderBatchService.cs`
- `Areas/Admin/Views/AdminPurchaseAdviceConsolidation/Index.cshtml`
- `Areas/Admin/Views/AdminPurchaseOrders/Create.cshtml`
- `Areas/Admin/Views/AdminPurchaseOrders/Details.cshtml`
- targeted tests cho integer pack, dimension, derived ordered qty và rounding.

### Cảnh báo, Restock Request và PA

- `Application/Services/Inventories/StockShortageReportService.cs`
- DTO/controller/view báo thiếu thủ công tương ứng sau khi inspect request path.
- `Application/Services/Inventories/RestockRequestService.cs`
- `Areas/Admin/Views/AdminStockAlerts/Details.cshtml`
- DTO/view PA để hiển thị remaining base qty và reference rõ ràng.

### Master/child PO và receiving

- `Application/Services/Inventories/PurchaseOrderBatchService.cs`
- `Application/Services/Inventories/PurchaseAdviceFulfillmentService.cs`
- `Application/Services/Inventories/BranchReceiptService.cs` nếu verification phát hiện
  thiếu Store guard/remaining aggregate.
- DTO/view batch/receipt và targeted integration tests.

### PDF/version/sent evidence

- `Application/Constants/PurchaseOrderBatchConstants.cs`
- `Application/Services/Inventories/PurchaseOrderBatchDocumentService.cs`
- `Application/Services/Inventories/PurchaseOrderBatchPdfRenderer.cs`
- `Areas/Admin/Views/AdminPurchaseOrderBatches/Details.cshtml`
- document workflow tests.

### Demo

- `docs/demo/pa-po-two-store-demo.md`
- SQL idempotent riêng dưới `docs/scripts` chỉ khi dữ liệu thật hiện có không đủ.

## Migration impact

**Không cần migration ở kế hoạch hiện tại.**

Các field cần thiết đã tồn tại. `PackageCount`/`TotalPackageCount` là decimal ở schema legacy
nhưng service có thể enforce integer an toàn; đổi kiểu cột chỉ tạo migration không cần thiết.
Nếu trong quá trình implement xuất hiện invariant không thể lưu đúng bằng schema hiện có,
phải dừng với `BLOCKED_ON_REQUIRED_SCHEMA_MIGRATION` trước khi tạo migration.
