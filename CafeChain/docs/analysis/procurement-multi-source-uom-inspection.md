# Audit luồng nhu cầu đa nguồn và Procurement UOM

Issue: #224

Epic: #215

Branch: `feature/POS`

## Kết luận

`BLOCKED_ON_REQUIRED_SCHEMA_MIGRATION`

Schema hiện tại đủ cho luồng cũ `StockAlert -> RestockRequest -> PA -> PO -> BranchReceipt`,
nhưng không đủ để biểu diễn đúng contract mới trong `FIX.md`:

- Không có audit nguồn nhu cầu đa nguồn trên `RestockRequest`.
- Không có quyết định/phân bổ nguồn cung `TRANSFER/PURCHASE/PRODUCTION/REJECT`.
- Không có quantity và unit bền vững theo Procurement UOM trên request, PA, PO,
  allocation và receipt draft.
- Receipt draft hiện đã quy đổi sang Inventory Base UOM trước khi xác nhận.
- Fulfillment hiện chặn phần dư quy cách vì so trực tiếp thực nhận với nhu cầu gốc.

Không thể giải quyết các điểm trên bằng DTO alias, chuỗi trong `Note`, hoặc đổi nghĩa
âm thầm các field `...BaseQuantity`. Cần Owner duyệt thay đổi schema trước khi triển khai
issues #225-#229.

Không có migration nào được tạo trong bước inspect này.

## Luồng hiện tại

1. `RestockRequestService.CreateFromConfirmedAlertAsync` chỉ tạo request từ
   `StockAlert` đã xác nhận.
2. `RestockRequest.StockAlertId` đã nullable, nhưng controller/service chưa có đường
   tạo manual hoặc central planner.
3. `PurchaseAdviceLine.RestockRequestId` là bắt buộc, vì vậy PA hiện luôn có source
   request. UI chưa có direct PA tự tạo demand ngầm.
4. PA source được tính từ request đang `PROCESSING/PARTIALLY_RECEIVED`; chưa có
   sourcing decision riêng. Request có thể xuất hiện để tạo PA chỉ dựa vào phần
   base quantity còn lại.
5. Consolidation quy đổi supplier package sang `Ingredient.BaseUnitId` ngay khi
   preview, sau đó lưu `OrderedBaseQuantity`.
6. `BranchReceiptService.SavePurchaseOrderDraftAsync` gọi
   `BuildPurchaseOrderReceiptLineAsync`; hàm này quy đổi số gói/đơn vị nhập thành
   `ReceivedBaseQuantity` và `RejectedBaseQuantity` ngay ở receipt draft.
7. `BranchReceiptService.ConfirmAsync` dùng base quantity đã tính sẵn để ghi fulfillment
   và inventory. Không có snapshot procurement quantity độc lập tại thời điểm confirm.
8. `RestockFulfillmentPostingService` từ chối khi tổng received base quantity lớn hơn
   `RestockRequest.RequestedQuantity`, nên dư quy cách PO hợp lệ vẫn bị chặn.
9. Master PO PDF/version/send đã có service, endpoint và UI; nội dung vẫn dùng các
   field base quantity hiện tại.

## Trả lời các câu hỏi inspect

### Request và nguồn nhu cầu

- `RestockRequest.StockAlertId` có nullable: **có** (`int?`).
- Request có bắt buộc alert trong nghiệp vụ hiện tại: **có ở service/API** vì chỉ có
  `CreateFromConfirmedAlertAsync`.
- Manual request có cần migration: **có**. Ngoài nullable alert, cần lưu `SourceType`,
  `SourceReferenceId`, `NeedByDate`, Procurement UOM và sourcing state.
- `CreatedByActor` có thể map từ `CreatedByStaffId`; `CreatedForStore` có thể dùng
  `StoreId`, không cần field trùng nghĩa.
- Central planner scope có nền `IScopeAuthorizationService`/`StaffScopes`, nhưng chưa
  có create API/service theo target store.

### Request, PA và direct PA

- PA line có liên kết request: **có**, `PurchaseAdviceLine.RestockRequestId` bắt buộc.
- Direct PA tạo implicit demand: **chưa có**.
- Direct PA tránh duplicate existing demand: **chưa có contract/API**.
- Request hiện tự xuất hiện làm PA source khi request đang xử lý và còn base quantity;
  chưa có bước `PURCHASE`, vì vậy vi phạm rule “chỉ PURCHASE tạo PA”.

### Sourcing và allocation

- Transfer allocation được suy ra từ `InventoryTransferDetail.BaseQuantity`.
- Purchase allocation được suy ra từ PA/PO/base counters.
- Không có production allocation hoặc rejected allocation theo quantity.
- `RestockRequestFulfillment.SourceType` hiện chỉ mang nghĩa fulfillment
  `SUPPLIER/MANUAL`, không phải sourcing decision.
- `RemainingUnallocatedQuantity` hiện là:
  `RequestedQuantity - TransferBase - PurchaseBase - ClosedRemainingBase`.
- Không có nguồn dữ liệu bền vững để biểu diễn split decision
  `TRANSFER/PURCHASE/PRODUCTION/REJECT`.
- Orphan có thể xuất hiện khi denormalized PA/PO allocation counter còn giá trị nhưng
  source line không còn active/không còn liên kết hợp lệ. Database chưa có một
  sourcing-allocation record bắt buộc source document.

### Quantity và UOM hiện tại

- Restock request: `RequestedQuantity` theo Inventory Base UOM của Ingredient.
- PA line: `RequestedPurchaseBaseQuantity`, `AllocatedToPoBaseQuantity`,
  `AcceptedBaseQuantity`, `ClosedBaseQuantity` theo base UOM.
- PO line: `PackageCount` và supplier package snapshot, nhưng authoritative ordered
  quantity là `OrderedBaseQuantity`.
- Batch/allocation: `TotalBaseQuantity` và `AllocatedBaseQuantity`.
- Receipt draft: `InputQuantity` có thể là số gói, nhưng accepted/rejected được lưu
  ngay thành `ReceivedBaseQuantity`/`RejectedBaseQuantity`.
- Receipt posting/fulfillment: chỉ lưu base quantity.
- Inventory posting: dùng `ReceivedBaseQuantity` đã được tính trước đó.

Vì vậy request/PA/PO/receipt chưa giữ kg/L xuyên suốt như contract mới.

### Conversion và lỗi rounding

- Conversion kg/L -> g/ml hiện xảy ra trong consolidation và lúc lưu receipt draft,
  trước inventory confirmation.
- Request 8,75 kg không nhận được PO 9 kg vì
  `RestockFulfillmentPostingService.RegisterAsync` từ chối khi `posted + received`
  lớn hơn `RestockRequest.RequestedQuantity`.
- Fulfillment hiện so với request target, không so với remaining PO obligation.
- `PurchaseOrderService.ValidateReceiptLineAsync` có so với
  `PurchaseOrderLine.OrderedBaseQuantity`, nhưng sau đó Restock fulfillment vẫn áp
  trần request, tạo xung đột với rounding surplus.
- Trường hợp 1 kg thành 1.000.000 g có thể xảy ra khi legacy
  `IngredientSupplier.PackageQuantity` đã chứa `1000` theo gram nhưng `UnitId` lại là
  kg: code coi là `1000 kg` rồi nhân conversion factor 1.000 lần nữa.
- `PurchaseUnitAuditService` có thể phát hiện một phần mismatch này, nhưng không thay
  thế được snapshot Procurement UOM bền vững.

### Pack, loose purchase và dimension

- Supplier pack hiện ở `IngredientSupplier.UnitId`, `PackageQuantity`,
  `CurrentPrice`, `MinimumOrderPackageCount`.
- `PhysicalUnitConversionService` đã kiểm tra dimension khối lượng/thể tích và dùng
  decimal.
- Packaged purchase có validation package count nguyên ở service/tests hiện có.
- Chưa có cấu hình rõ `AllowsLoosePurchase`; current path yêu cầu package snapshot.
- Không nên dùng `PackageQuantity == null` để suy đoán loose purchase vì current code
  coi đó là offer không hợp lệ.

### Remaining và receiving

- Request remaining, PA remaining, PO remaining hiện đều là base quantity.
- Receipt validate trước post bằng base quantity đã quy đổi.
- Draft receipt đã lưu base quantity, nên không chứng minh được
  “NoConversionBeforeInventoryConfirmation”.
- Rejected base quantity không tăng inventory, nhưng nghĩa vụ giao và residual demand
  vẫn được quản lý theo base quantity.
- Store isolation, idempotent confirm và per-store child PO đã có nền tốt và phải reuse.

### PDF, route, UI và permission

Đã có:

- `PurchaseOrderBatchDocumentService.GenerateAsync/ListAsync/DownloadAsync/MarkSentAsync`.
- `PurchaseOrderBatchPdfRenderer`.
- `AdminPurchaseOrderBatchesController.GeneratePdf`.
- `AdminPurchaseOrderBatchesController.DownloadRevision`.
- `AdminPurchaseOrderBatchesController.PrintRevision`.
- `AdminPurchaseOrderBatchesController.MarkRevisionSent`.
- UI Master PO có xuất/tải/in/mark sent/revision history.
- Permission PDF: `AccountantWarehouse` hoặc `BusinessOwner`; read có Store/Area scope.
- Download trả `application/pdf`, filename versioned và không trả HTML dưới tên PDF.

Còn thiếu/chưa đúng:

- Child PO detail chưa có read-model/link rõ về Master document.
- UI luôn gọi “Đơn đặt hàng gộp”, chưa đổi CTA theo 1 source hoặc 2+ sources.
- PDF snapshot dùng `TotalBaseQuantity/BaseQuantity`, chưa dùng Procurement UOM.
- Nút “Xem PDF” chưa được đặt tên riêng; hiện download/print mở inline.
- Supplier confirmed mới có display mapper, chưa có domain transition.

### Việt hóa

Nền mapper trạng thái/định dạng Việt Nam đã có, nhưng chưa hoàn tất:

- `StockAlertService` vẫn lưu câu
  `Stock reached the verified manual demand target`.
- Các validation còn lộ `StoreId`, `DRAFT`, `PROCESSING`, `fulfillment`, `identity`,
  `offer`, `lead time`, `ActualPackagePrice`, `RequestKey`, `Revision`.
- Views còn “PDF & gửi Nhà cung cấp”, “lead time”, “revision”, và nhiều label base.
- PDF hiện dùng `Revision R...` và base quantity.
- Cần map legacy English message ở display layer, không sửa audit lịch sử.

## Schema tối thiểu cần Owner duyệt

### RestockRequest

- `SourceType nvarchar(40) not null`
- `SourceReferenceId nvarchar(64) null`
- `NeedByDate date null`
- `RequestedProcurementQuantity decimal(18,3) null` trong giai đoạn backfill
- `ProcurementUnitId int null` FK `Units`
- `TargetStockProcurementQuantity decimal(18,3) null`
- `ForecastEvidence nvarchar(500) null`
- `SourcingStatus nvarchar(32) not null`

`CreatedByActorId` dùng `CreatedByStaffId`; `CreatedForStoreId` dùng `StoreId`.

### RestockSourcingAllocation (bảng mới)

- `RestockSourcingAllocationId`
- `RestockRequestId`
- `DecisionType` = `TRANSFER/PURCHASE/PRODUCTION/REJECT`
- `ProcurementQuantity`
- `ProcurementUnitId`
- `Status`
- `SourceDocumentType`
- `SourceDocumentId`
- `SourceDocumentLineId`
- `Reason`
- `CreatedByStaffId`, `CreatedAtUtc`, `CancelledAtUtc`
- `RowVersion`

Bảng này là authority chống double allocation và orphan allocation. Allocation
`PURCHASE` chỉ active khi có PA line hoặc PO line hợp lệ.

### PurchaseAdviceLine

- `RequestedProcurementQuantity`
- `AllocatedToPoProcurementQuantity`
- `AcceptedProcurementQuantity`
- `ClosedProcurementQuantity`
- `ProcurementUnitId`
- liên kết `RestockSourcingAllocationId`

Các field `...BaseQuantity` cũ được giữ để backfill/compatibility, không đổi nghĩa mù.

### IngredientSupplier

- `AllowsLoosePurchase bit not null default 0`

`UnitId` và `PackageQuantity` tiếp tục là Procurement UOM/content của supplier pack.

### PurchaseOrderBatchLine, PurchaseOrderLine, PurchaseOrderLineAllocation

- `ProcurementUnitId`
- `OrderedPackQuantity` nullable cho loose purchase
- `PackSizeProcurementQuantity`
- `OrderedProcurementQuantity`
- `DemandCoveredProcurementQuantity`
- `RoundingSurplusProcurementQuantity`
- allocation tương ứng theo Procurement UOM

Giữ price snapshot theo pack hoặc direct Procurement UOM.

### BranchReceiptLine và fulfillment postings

- `DeliveredPackQuantity`
- `DeliveredProcurementQuantity`
- `RejectedPackQuantity`
- `RejectedProcurementQuantity`
- `AcceptedPackQuantity`
- `AcceptedProcurementQuantity`
- `ProcurementUnitId`
- `ProcurementToInventoryFactor`
- `InventoryPostingBaseQuantity` nullable đến khi confirm
- dùng `BaseUnitId` hiện có làm Inventory Base UOM

`RestockFulfillmentPosting` và `PurchaseAdviceFulfillmentPosting` cần quantity/unit
procurement để tính demand progress; base quantity chỉ là inventory posting audit.

## Index/constraint bắt buộc

- Index request theo `(StoreId, SourceType, Status, NeedByDate)`.
- FK tất cả ProcurementUnitId -> Units, delete restrict.
- Check procurement quantity > 0.
- Check sourcing decision/status constants.
- Unique active sourcing source link để tránh allocation trùng.
- Check accepted/rejected/delivered procurement quantities.
- Check package quantity là số nguyên khi `AllowsLoosePurchase = 0`.
- Unique receipt posting/source key hiện có tiếp tục được giữ.

## Migration impact

Migration là bắt buộc. Tên đề xuất:

`AddMultiSourceProcurementUomContract`

Migration cần:

1. Thêm fields/tables/index/constraints ở trên.
2. Backfill request/PA/PO/receipt legacy từ Inventory Base UOM sang Procurement UOM
   bằng Unit dimension và conversion registry có kiểm soát.
3. Không đoán unit khi supplier package semantic chưa xác nhận; bản ghi mơ hồ phải
   được đánh dấu review/block, không nhân conversion tự động.
4. Giữ các field base legacy trong giai đoạn chuyển tiếp.
5. Không sửa hoặc tạo migration cho tới khi Owner duyệt riêng.

## File plan sau khi migration được duyệt

Domain/schema:

- `Models/Inventories/Stock/RestockRequest.cs`
- `Models/Inventories/Stock/RestockRequestFulfillment.cs`
- `Models/Inventories/Stock/RestockRequestFulfillmentPosting.cs`
- `Models/Inventories/Procurement/PurchaseAdvice.cs`
- `Models/Inventories/Procurement/PurchaseOrder.cs`
- `Models/Inventories/Procurement/PurchaseOrderBatch.cs`
- `Models/Inventories/Stock/BranchReceiptLine.cs`
- `Models/Inventories/Suppliers/IngredientSupplier.cs`
- configurations tương ứng và `AppDbContext`.

Backend:

- Restock request DTO/interface/service/controller.
- Sourcing allocation DTO/interface/service/controller.
- Purchase advice/direct PA/consolidation services.
- Purchase order/batch/read models.
- Branch receipt draft/confirm/fulfillment services.
- Unit conversion mapper chỉ dùng chính thức lúc inventory posting.
- PDF snapshot/renderer/document service.
- Scope authorization và localized audit mapper.

Frontend/Razor:

- `AdminRestockRequests` create/list/detail/sourcing.
- `AdminStockAlerts` create-request entry.
- `AdminPurchaseAdvices` direct PA.
- `AdminPurchaseAdviceConsolidation` dynamic CTA.
- `AdminPurchaseOrders` child -> master document link.
- `AdminPurchaseOrderBatches` procurement quantities/PDF actions.
- `AdminBranchReceipts` draft/confirm procurement quantities.
- shared status, quantity and legacy-message localization mapper.

Tests:

- Tách theo child issues #225-#232 và bao phủ toàn bộ danh sách section 23 của
  `CafeChain/FIX.md`.

## Dependency map

`#224 inspect`
→ `#225 multi-source request`
→ `#226 sourcing decision`
→ `#227 direct PA audit`
→ `#228 requested/ordered/accepted Procurement UOM`
→ `#229 receiving + conversion once`
→ `#230 PDF UI/read model`
→ `#231 localization`
→ `#232 final two-store acceptance`

Epic #215 tiếp tục OPEN cho Owner nghiệm thu. Không PR, không merge.
