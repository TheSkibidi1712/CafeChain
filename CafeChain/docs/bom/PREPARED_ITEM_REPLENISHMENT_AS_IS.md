# PreparedItem Replenishment - AS-IS

## 1. Phạm vi và nguồn bằng chứng

Tài liệu này mô tả CURRENT HEAD `a77d3ad5` trên nhánh `feature/POS`. Phân tích đi theo route/controller -> service -> model/configuration -> giao dịch tồn kho, không suy luận từ tên màn hình.

Các nguồn chính:

- `Models/Stores/StoreInventory.cs`
- `Application/Services/Admin/StoreInventories/InventoryThresholdService.cs`
- `Application/Services/Inventories/StockAlertService.cs`
- `Application/Services/Inventories/RestockRequestService.cs`
- `Application/Services/Admin/Production/ProductionSourceEligibilityService.cs`
- `Application/Services/Admin/Production/ProductionRunOperationsService.cs`
- `Application/Services/Admin/Production/ProductionRunAcceptanceService.cs`
- các EF configuration tương ứng trong `Data/Configurations`.

`docs/bom/BOM_REFACTOR_IMPLEMENTATION_SPEC.md` được nhắc trong yêu cầu nhưng không tồn tại ở CURRENT HEAD và không thấy trong lịch sử Git đang truy cập. Tài liệu này không tái tạo spec từ trí nhớ; ADR, tài liệu BOM hiện có, code và test hiện tại được dùng làm bằng chứng thay thế.

## 2. Bản đồ nghiệp vụ hiện tại

```text
StoreInventory (Store + PreparedItem)
        |
        | usable = AvailableQty - ReservedQty
        v
MinStockLevel -> StockAlert
        |
        | manager confirms
        v
RestockRequest
        |
        | RestockSourcingAllocation = PRODUCTION
        v
ProductionRun (pins RecipeId)
        |
        v
Plan -> Release -> Start -> Record Actual -> Variance -> Accept
        |
        v
PRODUCTION_OUT inputs + PRODUCTION_IN full accepted output
        |
        v
RestockFulfillmentPosting (capped to remaining demand)
```

Ba khái niệm đã tồn tại dưới các tên kỹ thuật khác nhau:

- `StockAlert`: quan sát tồn thấp.
- `RestockRequest`: nhu cầu bổ sung và tiến độ đáp ứng.
- `ProductionRun`: lệnh thực thi sản xuất.

Vì vậy CURRENT domain không cần một state machine sản xuất thứ hai.

## 3. PreparedItem inventory authority

### 3.1 Identity và Store scope

`StoreInventory` là authority số lượng theo Store. Một dòng có đúng một stock identity: Ingredient, Recipe legacy hoặc PreparedItem. Với BTP mới, identity ổn định là `PreparedItemId`; `RecipeId` chỉ còn là compatibility/version metadata ở các đường legacy.

EF duy trì unique canonical identity `(PreparedItemId, StoreId)` khi `BtpIdentityState = Canonical`. `PreparedItem.BaseUnitId` là đơn vị tồn kho cơ sở.

### 3.2 Số lượng

- `AvailableQty`: số lượng vật lý hiện đang ghi nhận trên dòng tồn.
- `ReservedQty`: lượng đang giữ chỗ.
- usable/khả dụng nghiệp vụ: `AvailableQty - ReservedQty`.
- `MaxNegativeQty`: chính sách âm nếu được cấu hình.
- `RowVersion`: bảo vệ optimistic concurrency.

POS có thể đi vào cơ chế bán mù/âm tồn theo policy đã có. Production acceptance không dùng âm tồn để hợp thức hóa thiếu đầu vào: đầu vào thực tế vẫn phải đủ usable quantity trước khi FIFO consumption.

### 3.3 Các writer chính

- POS: `InventoryDeductionService` trừ đúng một tầng stock identity; không explode nested BOM.
- Transfer/receipt: cập nhật StoreInventory và gọi đánh giá cảnh báo.
- Production acceptance: `ProductionRunAcceptanceService` ghi `PRODUCTION_OUT` cho actual input và `PRODUCTION_IN` cho toàn bộ accepted output.
- Historical `RecipeId`, Order snapshot và ProductionRun Recipe pin không bị resolve lại.

`InventoryTransaction` có unique `(ProductionRunId, StoreInventoryId, Type)` cho movement sản xuất; acceptance Completed được xử lý như replay.

## 4. Threshold và Stock Alert

### 4.1 Policy hiện có

`StoreInventory.MinStockLevel` là ngưỡng cảnh báo nullable, vì nằm trên StoreInventory nên tự nhiên là Store-specific và dùng được cho Ingredient lẫn PreparedItem.

Không có field mức tồn mục tiêu độc lập. `TargetStockProcurementQuantity` trên RestockRequest là snapshot của một quyết định/request, không phải policy bền vững theo Store + PreparedItem.

### 4.2 Evaluation

`StockAlertService`:

- chọn canonical PreparedItem inventory;
- tính usable = `AvailableQty - ReservedQty`;
- mở/cập nhật alert khi usable < `MinStockLevel`;
- resolve khi usable >= `MinStockLevel`;
- dùng unique filtered index để chỉ có một alert OPEN/CONFIRMED cho một Store + PreparedItem;
- retry khi hai evaluator cùng mở alert.

Canonical PreparedItem được hỗ trợ ở service và persistence. Tuy nhiên màn hình cấu hình ngưỡng còn PARTIAL: `InventoryThresholdService` chỉ include/map Ingredient hoặc Recipe legacy, chưa project PreparedItem canonical. BTP canonical có thể không tìm kiếm hoặc hiển thị đúng tên.

Có một boundary mismatch nhỏ nhưng có ý nghĩa: StockAlert service xem tồn thấp khi usable `<` threshold và resolve ở `>=`, trong khi `AdminInventoryThresholds/Index.cshtml` đang tô trạng thái thấp bằng `<=`. Tại đúng bằng ngưỡng, UI và alert authority có thể nói khác nhau. Service rule phải là authority và UI cần dùng cùng contract.

Production acceptance hiện không gọi lại `StockAlertService`. Tồn kho được tăng đúng nhưng cảnh báo có thể stale cho đến khi một writer/evaluation khác chạy.

## 5. Demand và sourcing hiện tại

`RestockRequestService.CreateFromConfirmedAlertAsync` tạo một RestockRequest từ alert đã xác nhận. Công thức hiện tại:

```text
Suggested = max(ThresholdSnapshot - CurrentQtySnapshot, 0)
```

Điều này chỉ bù đến ngưỡng tối thiểu, không bù đến một target stock riêng.

Các unique filtered index ngăn hai active RestockRequest cho cùng Store + PreparedItem và ngăn hai request active cho cùng StockAlert. Request có RequestedQuantity, fulfillment, transition và source allocation, nên đã đủ vai trò một Production Replenishment Demand ở phase này.

`RestockSourcingAllocation` phân biệt `TRANSFER`, `PURCHASE`, `PRODUCTION`, `REJECT`.

- Ingredient có thể đi purchase flow hiện tại.
- PreparedItem chỉ được mua ngoài khi `InventoryItemSourceCapability.CanPurchase` chứng minh được. Hiện supplier-package aggregate chưa chứng minh package thuộc PreparedItem, nên purchase eligibility fail closed.
- PreparedItem có `CanProduce`, Store capability, công thức current và quyền Plan thì được chọn nguồn PRODUCTION.

## 6. Production và Recipe pinning

`ProductionSourceEligibilityService` dùng shared `ICurrentRecipeResolver` với exact target `PreparedItem`. Không có size-less/local fallback.

Recipe được pin tại lúc tạo ProductionRun Planned trong `CreateProductionAllocationAsync`, cùng transaction với allocation PRODUCTION. ProductionRun lưu:

- exact `RecipeId`;
- planned batch count;
- expected output per batch đã normalize về PreparedItem base UOM;
- RequestKey/fingerprint;
- RestockRequest allocation.

Trước điểm này, một Recipe mới được publish sẽ là Recipe current dùng để plan. Sau điểm này, Release/Start/Record/Accept tiếp tục dùng exact `ProductionRun.RecipeId`; không tự đổi sang Recipe mới.

## 7. Accepted output và demand satisfaction

Acceptance v2 chạy trong SQL transaction và lock ProductionRun/StoreInventory. Trình tự authority:

1. kiểm tra permission + Store scope + trạng thái;
2. consume actual inputs theo FIFO;
3. cộng toàn bộ `AcceptedOutputBase` vào PreparedItem inventory;
4. tạo FIFO layer cho toàn bộ output;
5. post fulfillment với `min(AcceptedOutputBase, RemainingDemand)`;
6. chuyển run thành Completed.

Hệ quả:

- Underproduction giữ RestockRequest ở trạng thái partial và còn remaining demand.
- Overproduction vẫn cộng toàn bộ sản lượng vật lý vào tồn kho; phần vượt không tự phân bổ sang demand khác.
- Expected output và planned output không làm tăng tồn.

## 8. Open production supply

Open supply có thể chứng minh từ `RestockSourcingAllocation` liên kết `ProductionRun`. Các trạng thái Planned, Released, InProgress, AwaitingVarianceApproval và AwaitingAcceptance là non-terminal; Completed đã trở thành inventory/fulfillment, Cancelled không còn là nguồn cung.

Gap hiện tại: `ProductionRunOperationsService.CancelAsync` chuyển run sang Cancelled nhưng không release/cancel allocation PRODUCTION. Các phép tổng hợp allocation hiện đếm status Active/Pending mà không join trạng thái run. Một lệnh đã hủy có thể tiếp tục làm request trông như đã được cấp nguồn.

## 9. UOM

Mọi quantity demand/fulfillment/production quan trọng đều quy về PreparedItem Base UOM. UI có thể hiển thị L trong khi persistence tính ml nếu conversion authority chứng minh được. `Batch` chỉ là số lần chạy công thức, không phải physical UOM.

Batch rounding có thể tạo expected output lớn hơn demand. Đây là bình thường: allocation/fulfillment được cap theo demand, còn accepted physical output không bị cắt.

## 10. Authorization hiện tại

Boundary chính dùng effective permission + Store scope:

- cấu hình ngưỡng: `InventoryThreshold.View/Update`;
- xem/xử lý alert và tạo demand: `StockAlert.*`, `Restock.*`;
- chọn nguồn sản xuất: đã dùng `ProductionOrder.Plan`;
- Plan/Release/Start/Record/Accept/ApproveVariance/Cancel: quyền Production tương ứng.

Ma trận seed hiện cấp `InventoryThreshold.Update` cho BusinessOwner, RegionManager và StoreManager. `StockAlert.CreateRestockRequest` chỉ cấp StoreManager; `Restock.Update` cấp StoreManager và WarehouseAccountant. Production source eligibility đã được đổi sang yêu cầu `ProductionOrder.Plan`, phù hợp với StoreManager là planner trong target RBAC.

Không cần permission mới để làm minimum implementation. StoreManager là actor hợp lý để cấu hình policy của Store và plan/release; ShiftSupervisor thực thi; StoreManager accept; BusinessOwner duyệt variance.

Hai inconsistency CURRENT HEAD phải được giải quyết trước implementation:

1. `InventoryThresholdService` vẫn có role allow-list cũ, gồm SystemAdmin, song song với permission boundary.
2. `SeedAll.sql` vẫn cấp nhiều Production action cho SystemAdmin trong khi test mới của commit `a77d3ad5` kỳ vọng các bit đó bằng 0. Đây là RBAC baseline mismatch, không phải lý do tạo permission replenishment mới.

## 11. Concurrency và idempotency

Protections hiện có:

- unique active StockAlert theo Store + item;
- unique active RestockRequest theo Store + item và theo StockAlert;
- RowVersion trên StoreInventory, StockAlert, RestockRequest, allocation, ProductionRun;
- unique `(StoreId, RequestKey)` và fingerprint/replay cho ProductionRun;
- unique allocation per ProductionRun;
- SQL transaction + row locks khi acceptance;
- unique production inventory movements;
- unique fulfillment source tuple.

RowVersion một mình không ngăn duplicate command; unique indexes, RequestKey, transaction và state revalidation mới là protections quyết định.

## 12. Kết luận AS-IS

CafeChain đã có Store-scoped PreparedItem inventory, threshold, alert, demand-like RestockRequest, production sourcing, exact Recipe pinning và accepted-output fulfillment. Chuỗi chưa hoàn chỉnh vì thiếu target-stock policy độc lập, cancellation không release open supply, threshold UI chưa đọc PreparedItem canonical và acceptance chưa reevaluate alert.
