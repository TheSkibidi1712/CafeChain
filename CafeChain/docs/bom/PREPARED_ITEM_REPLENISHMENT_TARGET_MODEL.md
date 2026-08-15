# PreparedItem Replenishment - Target Model

## 1. Mục tiêu thiết kế

Mô hình đích mở rộng domain hiện tại vừa đủ để trả lời: Store đang thiếu bao nhiêu PreparedItem, đã có bao nhiêu nhu cầu được production cover, lệnh nào thực thi, Recipe nào được pin và accepted output đã phục hồi tồn kho ra sao.

Không xây MRP, scheduler hoặc demand entity thứ hai.

## 2. Ubiquitous language

| Thuật ngữ | Ý nghĩa |
|---|---|
| Ngưỡng cảnh báo tồn thấp | Mốc kích hoạt cảnh báo |
| Mức tồn mục tiêu | Mức vận hành muốn phục hồi tới |
| Cảnh báo tồn kho | Observation tại một thời điểm |
| Nhu cầu bổ sung | Số lượng business cần đáp ứng; dùng RestockRequest hiện có |
| Nguồn sản xuất đang mở | Demand coverage từ allocation PRODUCTION non-terminal |
| Lệnh sản xuất | ProductionRun thực thi và pin RecipeId |
| Sản lượng đạt | AcceptedOutputBase được duyệt nhập kho |
| Còn cần bổ sung | Net need sau stock và open coverage |

Không dùng từ `Tồn dự kiến` cho open production coverage vì output chưa được accept chưa phải inventory.

## 3. Entity ownership

### 3.1 Reuse

- `PreparedItem`: global stable stock identity.
- `StoreInventory`: Store-scoped quantity và replenishment policy.
- `StockAlert`: low-stock observation/snapshot.
- `RestockRequest`: replenishment demand, source allocation và fulfillment.
- `RestockSourcingAllocation`: source decision và open coverage.
- `ProductionRun`: executable instruction, exact Recipe pin.
- `InventoryTransaction`/FIFO layer: physical and cost authority.

### 3.2 Minimal additive field

Đề xuất thêm nullable field trên StoreInventory canonical:

```text
TargetStockLevel decimal(18,3) NULL
```

Semantics:

- quantity ở stock base UOM;
- Store-specific vì nằm trên StoreInventory;
- null = chưa cấu hình target;
- >= 0;
- nếu cả MinStockLevel và TargetStockLevel có giá trị thì TargetStockLevel >= MinStockLevel;
- chỉ authoritative canonical row được dùng cho PreparedItem policy.

Không thêm field vào PreparedItem global. Không dùng `RestockRequest.TargetStockProcurementQuantity` làm policy vì đó là request snapshot ở procurement UOM.

## 4. Calculation contract

```text
UsableStockBase = AvailableQty - ReservedQty

Low = MinStockLevel
Target = TargetStockLevel

IsLow = Low != null && UsableStockBase < Low

GrossNeedBase =
  Target != null ? max(Target - UsableStockBase, 0) : unavailable

CreditableOpenProductionCoverageBase =
  sum(active PRODUCTION allocation base quantity)
  where linked ProductionRun is
  Planned | Released | InProgress |
  AwaitingVarianceApproval | AwaitingAcceptance

NetProductionNeedBase =
  max(GrossNeedBase - CreditableOpenProductionCoverageBase, 0)
```

Rules:

- Completed không là open supply; output đã đi vào inventory/fulfillment.
- Cancelled không là open supply và allocation phải được release.
- Allocation amount, không phải rounded expected batch output, là demand coverage authority.
- UI có thể hiển thị expected/recorded output riêng, nhưng không thay thế coverage.
- Missing target không tự biến thành 0 và không tự dùng low threshold làm target.

## 5. Lifecycle contract

### 5.1 Alert

- Khi usable < low: mở/cập nhật một active StockAlert.
- Alert lưu usable và policy snapshot để giải thích nguyên nhân.
- Alert không tự cộng/trừ stock và không phải executable order.

### 5.2 Demand

- StoreManager xác nhận alert và tạo/điều chỉnh một active RestockRequest.
- RequestedQuantity ban đầu = NetProductionNeedBase tại thời điểm xác nhận.
- Snapshot cần lưu/prove: Store, PreparedItem, base UOM, usable, low, target, gross need, open coverage, net need.
- Unique active RestockRequest tiếp tục ngăn demand trùng.

Không cần entity `ProductionDemand` mới. Nếu UI cần tên nghiệp vụ, hiển thị RestockRequest là `Nhu cầu bổ sung`.

### 5.3 Source and plan

- PreparedItem có CanProduce + Store capability -> đề xuất PRODUCTION.
- PURCHASE chỉ xuất hiện nếu CanPurchase và supplier contract thực sự chứng minh được.
- StoreManager explicit plan; không auto-create ProductionRun từ alert.
- Planning revalidates current net need, Store scope, capability và current Recipe.
- ProductionRun Planned + allocation được tạo cùng transaction và RequestKey.

### 5.4 Recipe pin

- Demand chưa pin Recipe.
- Plan tạo ProductionRun và pin exact current `RecipeId`.
- Sau pin, mọi operation dùng RecipeId đã lưu.
- Muốn dùng Recipe mới phải cancel/replan nếu state cho phép.

### 5.5 Cancel

Cancel Planned/Released phải cùng unit of work:

- chuyển ProductionRun -> Cancelled;
- chuyển linked production allocation -> Released/Cancelled;
- recompute RestockRequest sourcing status;
- làm net need có thể plan lại.

Không xóa run hoặc audit.

### 5.6 Accept

Trong acceptance transaction:

- consume actual input FIFO;
- credit toàn bộ accepted physical output;
- fulfill demand tối đa phần còn thiếu;
- complete run idempotently.

Sau commit thành công, trigger bounded StockAlert/replenishment reevaluation cho output StoreInventory. Việc phát notification không được rollback inventory đã commit; nếu cần dùng outbox/retry convention hiện có.

## 6. Demand satisfaction

Giữ explicit fulfillment làm traceability authority, đồng thời re-evaluate stock để phản ánh tiêu thụ mới.

```text
Requested 6.0 L
Accepted 4.8 L
-> inventory +4.8 L
-> fulfillment +4.8 L
-> remaining demand 1.2 L

Requested 6.0 L
Accepted 6.4 L
-> inventory +6.4 L
-> fulfillment +6.0 L max
-> remaining demand 0
```

Không truncate physical output và không spill overproduction sang request khác.

## 7. Read model

Đề xuất bounded projection theo Store + PreparedItem:

```text
PreparedItemReplenishmentReadModel
- StoreId / StoreName
- PreparedItemId / business name / code
- BaseUnit
- OnHandBase
- ReservedBase
- UsableBase
- LowThresholdBase
- TargetStockBase
- IsLow
- GrossNeedBase
- OpenProductionCoverageBase
- NetNeedBase
- ActiveAlertId
- ActiveRestockRequestId
- OpenProductionRun summaries (bounded)
- DataStatus / localized reason
```

Query phải set-based, không N+1 per item và chỉ đọc Store trong authorized scope.

## 8. Authorization model

Không cần permission mới:

- StoreManager: `InventoryThreshold.Update` trong own Store; xác nhận alert, tạo/điều chỉnh demand, `ProductionOrder.Plan/Release/AcceptOutput/Cancel`.
- ShiftSupervisor: `ProductionOrder.Start/RecordActual` trong own Store.
- BusinessOwner: view/governance và `ApproveVariance`.
- RegionManager: read-only trong scope.
- WarehouseAccountant: inventory/BOM visibility theo matrix; không tự plan production nếu không có Plan.
- SalesEmployee/SystemAdmin: không có business replenishment authority mặc định.

AccountPermissionOverride và Store scope tiếp tục là authority. Cần sửa RBAC baseline mismatch hiện tại trước runtime acceptance, không mở rộng role để né lỗi.

## 9. Migration contract

Migration tương lai phải additive, idempotent và không backfill giả:

- add nullable `TargetStockLevel`;
- existing rows để null;
- Owner/StoreManager cấu hình dần;
- không copy MinStockLevel sang target nếu không có business decision;
- không rewrite lịch sử alert/request/run;
- validation target >= low ở service, kèm DB constraint nếu tương thích dữ liệu legacy.

