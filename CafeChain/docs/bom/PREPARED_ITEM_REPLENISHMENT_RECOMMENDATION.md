# PreparedItem Replenishment - Recommendation

## 1. Kết luận ngắn

CafeChain không thiếu toàn bộ replenishment flow. Hệ thống đã có khoảng giữa và cuối chuỗi: Store inventory, StockAlert, RestockRequest, production sourcing, ProductionRun, Recipe pinning, accepted output, FIFO và fulfillment.

Nên mở rộng domain hiện tại, không tạo ProductionDemand entity hoặc state machine mới.

Để hỗ trợ đúng hai khái niệm `Low threshold` và `Target stock`, cần một additive migration tối thiểu cho StoreInventory. Không migration nào được thực hiện trong discovery này.

## 2. Xây gì ngay sau khi Owner duyệt

### Slice 0 - RBAC baseline prerequisite

- reconcile `SeedAll.sql` với test/Owner matrix để SystemAdmin không có business Production actions;
- bỏ role allow-list legacy khỏi InventoryThresholdService, chỉ dùng effective permission + Store scope;
- không tạo permission replenishment mới.

### Slice 1 - Store + PreparedItem policy

- thêm nullable `TargetStockLevel` trên StoreInventory canonical;
- validate target >= low;
- sửa threshold query/UI để project canonical PreparedItem;
- cấu hình low + target theo authorized Store.

### Slice 2 - Bounded replenishment read model

- usable stock;
- low threshold;
- target;
- gross need;
- active production coverage;
- net need;
- active alert/request/run links;
- explicit unavailable states.

### Slice 3 - Safe production planning integration

- RestockRequest tiếp tục là demand authority;
- PreparedItem mặc định đề xuất PRODUCTION nếu eligible;
- planning revalidates net need và dùng shared current Recipe resolver;
- RecipeId pin tại ProductionRun Planned;
- no auto-plan/no auto-release.

### Slice 4 - Lifecycle closure

- cancel run release linked allocation và recompute request sourcing;
- accepted output tiếp tục credit full physical quantity;
- fulfillment cap remaining demand;
- post-commit reevaluate alert/replenishment state.

### Slice 5 - UX integration

- Inventory Threshold: low + target per Store + canonical PreparedItem.
- Stock Alert/Restock: dùng nhãn `Nhu cầu bổ sung`, `Nguồn sản xuất`, `Đang được sản xuất`, `Còn cần bổ sung`.
- Production Order: deep link về demand/alert.
- A4 Recipe Workspace: chỉ một Store-scoped signal nhỏ và deep links; không biến thành inventory/production page.

## 3. Không nên xây

- ProductionDemand table riêng trong phase này;
- auto ProductionRun từ alert;
- auto Release/Start;
- recursive child ProductionRuns;
- MRP/forecast/scheduler;
- Recipe scheduling hoặc RecipeIdentity;
- supplier PO fallback cho PreparedItem;
- inventory ledger/state-machine rewrite.

## 4. Migration decision

```text
MIGRATION_REQUIRED = YES, ADDITIVE ONLY
```

Lý do chính xác: current schema không thể lưu độc lập `Low = 3 L` và `Target = 8 L` theo Store + PreparedItem. `MinStockLevel` chỉ là trigger; RestockRequest target chỉ là snapshot, không phải policy.

Minimal schema:

```text
StoreInventories.TargetStockLevel decimal(18,3) NULL
```

Không backfill target bằng low. Không sửa lịch sử. Không thêm entity/table nếu chưa có invariant khác buộc phải lưu.

## 5. Permission recommendation

`RBAC_EXTENSION_REQUIRED = NO` cho minimum implementation.

- StoreManager: cấu hình low/target trong own Store, xác nhận nhu cầu, Plan/Release/Accept/Cancel.
- ShiftSupervisor: Start/RecordActual trong own Store.
- BusinessOwner: global view/governance, ApproveVariance.
- RegionManager: read-only trong scope.
- WarehouseAccountant: BOM/inventory visibility; không plan production mặc định.
- SalesEmployee: không quản lý replenishment.
- SystemAdmin: không có business authority mặc định.

Re-use `InventoryThreshold.Update`, StockAlert/Restock permissions và ProductionOrder action permissions. Account overrides vẫn áp dụng.

## 6. Read/API seams

Nên có một application query seam riêng cho `PreparedItemReplenishmentReadModel`, thay vì controller tự ghép nhiều query. Mutation seams cần tái sử dụng:

- InventoryThreshold service cho policy;
- StockAlert service cho evaluation;
- RestockRequest service cho demand/source;
- Production source eligibility + current Recipe resolver;
- Production operations/acceptance;
- Restock fulfillment posting.

Query constraints:

- selected authorized Store only;
- set-based projection;
- bounded open runs;
- no full inventory/Recipe graph;
- no N+1;
- no raw reason codes ở UI.

## 7. Test seams đề xuất

Focused future tests:

- `PreparedItemThreshold_IsStoreSpecific`
- `TargetStock_MustNotBeBelowLowThreshold`
- `CanonicalPreparedItem_AppearsInThresholdProjection`
- `NetNeed_SubtractsOnlyNonTerminalProductionCoverage`
- `CancelledRun_ReleasesProductionCoverage`
- `ConcurrentAlertEvaluation_CreatesOneActiveAlert`
- `ConcurrentDemandCreation_CreatesOneActiveRequest`
- `ProductionPlan_RequestKey_IsIdempotent`
- `RecipeChangeBeforePlan_UsesNewCurrentRecipe`
- `RecipeChangeAfterPlan_DoesNotSwitchPinnedRecipe`
- `AcceptedOutput_CreditsFullPhysicalQuantity`
- `AcceptedOutput_FulfillmentIsCappedToRemainingDemand`
- `AcceptedOutput_ReevaluatesPreparedItemAlert`
- `UnauthorizedStore_CannotReadOrMutateReplenishment`
- `PreparedItemWithoutPurchaseContract_DoesNotCreatePO`

SQL-backed tests phải cover unique active demand, cancel/plan race và acceptance replay. Không cần full suite theo thói quen.

## 8. Rủi ro

- Legacy Recipe/PreparedItem inventory rows có thể làm policy đọc sai nếu không buộc canonical authority.
- Dùng expected rounded batch output làm demand coverage có thể che thiếu hoặc thổi phồng supply.
- Không release allocation khi cancel sẽ chặn replan.
- Gọi notification trong inventory transaction có thể làm acceptance rollback vì lỗi phụ.
- Seed RBAC hiện mâu thuẫn với test mới; runtime role matrix phải được sửa/verify trước rollout.
- Nếu UI gọi tất cả là `nhập hàng`, người dùng dễ đưa PreparedItem sang procurement sai.

## 9. Hai mươi câu trả lời bắt buộc

1. **PreparedItem hiện có Store-scoped inventory không?** Có, qua StoreInventory canonical theo Store + PreparedItem.
2. **Stock-alert hiện tại biểu diễn PreparedItem được không?** Có ở service/persistence; UI cấu hình threshold còn partial với canonical PreparedItem.
3. **Threshold có cấu hình được không?** Có, qua MinStockLevel.
4. **Threshold có Store-specific không?** Có, vì nằm trên StoreInventory.
5. **Target stock có được biểu diễn không?** Chưa có policy độc lập; request snapshot không thay thế được.
6. **Open Production supply có xác định an toàn không?** Có thể từ allocation PRODUCTION + non-terminal run, nhưng query hiện chưa loại Cancelled đúng cách.
7. **Ngăn duplicate replenishment thế nào?** Unique active alert/request, allocation, RequestKey/fingerprint, transaction, state revalidation và release allocation khi cancel.
8. **Có thật sự cần Production Demand entity riêng không?** Không; RestockRequest đã đảm nhiệm demand, sourcing và fulfillment.
9. **RecipeId được pin chính xác ở state nào?** Khi tạo ProductionRun ở state Planned trong production allocation transaction.
10. **Recipe đổi trước pin thì sao?** Plan dùng current Recipe mới qua shared resolver.
11. **Recipe đổi sau pin thì sao?** Run giữ RecipeId cũ; muốn đổi phải cancel/replan khi state cho phép.
12. **Accepted Output ảnh hưởng inventory và demand thế nào?** Full accepted output vào inventory; fulfillment chỉ tới remaining demand; sau đó reevaluate.
13. **Schema hiện tại hỗ trợ không migration không?** Không cho target-stock độc lập; cần additive field. Phần còn lại reuse schema hiện có.
14. **Role nào cấu hình threshold?** StoreManager trong own Store, với Owner governance/read; exact grant dùng effective permission.
15. **Role nào initiate/plan replenishment?** StoreManager.
16. **Role nào execute?** ShiftSupervisor Start/RecordActual.
17. **Role nào accept output?** StoreManager; BusinessOwner chỉ variance approval theo matrix.
18. **Cần permission addition nào?** Không cho minimum implementation; cần sửa RBAC baseline inconsistency.
19. **Traceability cần dữ liệu gì?** Alert, request snapshot, allocation, run + RecipeId, transitions/actors, actual inputs/output, inventory movements, fulfillment.
20. **Smallest safe production implementation là gì?** Add Store target field, canonical threshold projection, bounded net-need read model, production allocation revalidation, cancel-release allocation và accept-triggered reevaluation; giữ nguyên Production workflow.

## 10. Owner decisions cần chốt trước `/to-spec`

1. Tên field/UI chính thức: `Mức tồn mục tiêu`.
2. StoreManager có được chỉnh cả low và target trong own Store hay cần Owner override policy.
3. Khi target chưa cấu hình: chỉ cảnh báo, không cho auto-suggest production quantity; khuyến nghị fail closed.
4. Demand adjustment khi POS tiếp tục tiêu thụ trong lúc run đang mở: khuyến nghị explicit manager confirmation.
5. Alert reevaluation sau accept dùng synchronous bounded call hay post-commit retry/outbox theo convention được chọn.

Sau khi năm quyết định trên được chốt, nội dung đã đủ để chuyển sang `/to-spec`. Discovery này dừng trước bước đó.

