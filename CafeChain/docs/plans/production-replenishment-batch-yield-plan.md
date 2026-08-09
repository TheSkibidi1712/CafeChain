# Kế hoạch hardening nguồn sản xuất nội bộ theo mẻ và sản lượng thực tế

> Phạm vi: INSPECT & PLAN ONLY. Tài liệu này không thay đổi code, schema, UI,
> dữ liệu hay state machine. Mọi migration/repair bên dưới chỉ là kế hoạch cho
> task implementation riêng sau khi Owner chốt các quyết định mở.

## Quy ước

| Nhãn | Ý nghĩa |
|---|---|
| CODE_CONFIRMED | Xác nhận trực tiếp từ code, schema hoặc test hiện tại |
| ALREADY_SUPPORTED | Contract đã có và có thể tái sử dụng |
| PARTIALLY_SUPPORTED | Có nền nhưng chưa chạy đúng end-to-end |
| MISSING | Chưa có authority hoặc dữ liệu bền vững cần thiết |
| CONFLICTS_WITH_CURRENT_MODEL | Contract mục tiêu xung đột model hiện tại |
| NEEDS_OWNER_DECISION | Code không đủ để tự chốt nghiệp vụ |
| NOT_RUNTIME_VERIFIED | Chưa chạy runtime vì đây là task inspect/plan |

## 1. Executive summary

1. CODE_CONFIRMED: Production là workflow thật. Hệ thống có ProductionRun,
   readiness, tiêu hao BOM, FIFO costing, nhập tồn BTP, ledger, transaction,
   idempotency và concurrency guard.
2. CODE_CONFIRMED: nguồn PRODUCTION của Restock lại mới chỉ là một giá trị
   RestockSourcingAllocation. Nó không tạo/liên kết ProductionRun và không có
   eligibility resolver.
3. CODE_CONFIRMED: UI luôn render Sản xuất nội bộ cho actor được xét nguồn; backend
   chỉ kiểm tra enum/trạng thái/số lượng/UOM/scope nên raw Ingredient cũng qua được.
4. CONFLICTS_WITH_CURRENT_MODEL: completion lấy
   Recipe.OutputQuantity x RequestedRunCount làm đầu ra thực tế và tăng toàn bộ
   vào tồn. Payload chỉ có ProductionRunId, không có actual/accepted yield.
5. MISSING: Restock fulfillment chỉ nhận BRANCH_RECEIPT và INVENTORY_TRANSFER.
   Production hoàn tất không cập nhật remaining demand.
6. CONFLICTS_WITH_CURRENT_MODEL: batch hiện là decimal(18,5), UI step=any và test
   FractionalRunCount_Accepted. Luồng Restock mới cần mặc định batch nguyên theo ceil.
7. PARTIALLY_SUPPORTED: Recipe đã pin exact version, có output PreparedItem,
   expected yield/output UOM; BOM đa tầng có cycle/depth guard và tiêu hao BTP con,
   nhưng chưa orchestration dependency production.

Severity:

| Vấn đề | Mức | Tác động |
|---|---|---|
| Raw Ingredient chọn PRODUCTION | P1 | Bypass purchase/transfer, Restock có thể treo |
| Allocation không có ProductionRun | P1 | UI báo đã phân nguồn nhưng không có chứng từ thực thi |
| Expected output được nhập như actual | P0 khi nối Restock | Sai tồn, giá vốn và trạng thái demand |
| Không có location capability | P1 | Store active bất kỳ có quyền đều có thể chạy |
| Một permission tạo và nhập kho | P1 | Thiếu maker-checker cho yield/waste |

Hướng đề xuất: giữ Restock theo output base quantity; dùng một backend source
resolver; phát triển ProductionRun thành workflow kế hoạch/thực thi; snapshot
expected yield; completion ghi actual input/output/accepted/waste; chỉ accepted
output tăng tồn và fulfillment Restock.

## 2. Current architecture

~~~mermaid
flowchart LR
    R[RestockRequest] --> A[RestockSourcingAllocation]
    A -->|PURCHASE| PA[PA / PO / Receipt]
    A -->|TRANSFER| T[InventoryTransfer]
    A -. PRODUCTION chỉ allocation .-> X[Không có orchestration]
    REC[Recipe + PreparedItem output] --> RUN[ProductionRun]
    RUN --> RD[Readiness]
    RUN --> EX[Execution]
    EX --> IO[PRODUCTION_OUT]
    EX --> II[PRODUCTION_IN expected output]
    II --> FIFO[PreparedItem FIFO layer]
    PA --> F[RestockFulfillmentPosting]
    T --> F
    F --> P[Restock progress]
    II -. chưa nối .-> F
~~~

| Concern | Authority hiện tại | Đánh giá |
|---|---|---|
| Demand | RestockRequest.RequestedQuantity | ALREADY_SUPPORTED |
| Source allocation | RestockSourcingAllocation | PARTIALLY_SUPPORTED |
| Production intent | ProductionRun | ALREADY_SUPPORTED |
| Recipe output | PreparedItemId/OutputQuantity/OutputUnitId | ALREADY_SUPPORTED |
| Readiness | ProductionReadinessService | ALREADY_SUPPORTED cho expected |
| Execution | ProductionRunExecutionService | PARTIALLY_SUPPORTED |
| Costing | FIFO input/output layers | PARTIALLY_SUPPORTED |
| Fulfillment | RestockFulfillmentPostingService | MISSING production |
| Location | active Store + StaffScope | PARTIALLY_SUPPORTED |
| Capability | writer mode/provider | MISSING business capability |
| Permission | View/Create/Confirm | PARTIALLY_SUPPORTED |
| Audit | actor/time, ledger, log | PARTIALLY_SUPPORTED |

Evidence chính:

- Models/Inventories/Production/ProductionRun.cs
- Models/Enums/Inventory/ProductionRunStatus.cs
- Application/Services/Admin/Production/ProductionRunService.cs
- Application/Services/Admin/Production/ProductionReadinessService.cs
- Application/Services/Admin/Production/ProductionRunExecutionService.cs
- Application/Services/Inventories/RestockRequestService.cs
- Application/Services/Inventories/RestockFulfillmentPostingService.cs
- Application/Services/Inventories/RestockRequestWorkflowService.cs
- Areas/Admin/Controllers/AdminProductionOrderController.cs
- Areas/Admin/Views/AdminProductionOrder/Create.cshtml
- Areas/Admin/Views/AdminRestockRequests/Details.cshtml
- Data/Configurations/Inventories/Production/ProductionRunConfiguration.cs
- Data/Configurations/Inventories/Stock/RestockSourcingAllocationConfiguration.cs

## 3. Root cause

Raw material chọn Production vì:

1. RestockSourcingDecisionTypes.All chứa PRODUCTION không gắn capability.
2. Details.cshtml render cố định TRANSFER/PURCHASE/PRODUCTION.
3. SetSourcingDecisionAsync không kiểm item capability, active recipe, output
   mapping/yield/UOM, location capability hay Production permission.
4. Method tạo allocation ACTIVE, SourceDocumentType=PRODUCTION nhưng không tạo
   ProductionRunId.
5. Không có candidate endpoint/backend resolver dùng chung.

Expected được dùng như actual vì ExecuteStock chỉ nhận ProductionRunId, sau đó:

    rawOutput = Recipe.OutputQuantity * RequestedRunCount
    normalizedOutput = Convert(rawOutput -> PreparedItem.BaseUnit)
    outputInventory.AvailableQty += normalizedOutput
    outputUnitCost = totalInputCost / normalizedOutput

Restock không tiến triển vì fulfillment whitelist chỉ BRANCH_RECEIPT và
INVENTORY_TRANSFER. Production ledger không đăng RestockFulfillmentPosting.

Root cause là thiếu domain authority và integration, không phải lỗi riêng ở UI.
Không sửa bằng Ingredient => false hoặc PreparedItem => true.

## 4. Current production behavior

Lifecycle chỉ có Confirmed=1 và Completed=2.

Create/confirm:

- request key + fingerprint, unique theo StoreId/RequestKey;
- pin exact RecipeId;
- validate StoreScope, writer readiness, recipe/output;
- tạo durable intent, chưa đụng tồn.

Readiness:

- expected output per run/total;
- normalize Ingredient hoặc ChildRecipe input;
- kiểm available-reserved và FIFO cost evidence;
- execute vẫn revalidate trong transaction.

Completion:

- lock run, inventories và FIFO layers;
- trừ RecipeDetail quantity x RequestedRunCount;
- cộng expected normalized output;
- tạo PRODUCTION_OUT/IN, cost allocation và output FIFO layer;
- replay Completed không ghi trùng.

Chưa có: Restock link, plan từ demand, actual input, actual/accepted/rejected
output, tolerance, cancellation, lot/expiry, transition audit hoặc maker-checker.

## 5. Inventory item model

| Loại | Identity | Base UOM | Production output |
|---|---|---|---|
| Raw/packaging/consumable | Ingredient | BaseUnitId | Recipe chưa trỏ tới |
| Bán thành phẩm | PreparedItem | BaseUnitId | Recipe.PreparedItemId |
| Balance | StoreInventory | Ingredient XOR PreparedItem, Recipe legacy | Có canonical PreparedItem |
| Restock | RestockRequest | Ingredient XOR PreparedItem, Recipe legacy | Có PreparedItem từ StockAlert |

Ingredient không có ItemType; PreparedItem là identity bền vững, không phải Recipe
version; không có abstraction InventoryItem chung. Manual Restock DTO hiện chỉ
nhận IngredientId.

Đề xuất theo pattern XOR hiện hữu, không tạo mega abstraction ở phase đầu:

    InventoryItemSourceCapability
      IngredientId XOR PreparedItemId
      CanPurchase, CanTransfer, CanProduce, Active

    StoreProductionCapability
      StoreId
      IngredientId XOR PreparedItemId
      CanProduce, Active, EffectiveFrom, EffectiveTo

Ingredient không bị hard-code false. Nó không eligible nếu thiếu capability và
không có production recipe output tương ứng.

## 6. Source eligibility model

Tạo IInventorySourceEligibilityService làm authority cho query và mutation.

    CanUseProduction(item, location, actor, atUtc)
      = item policy CanProduce
      && active production recipe exists
      && recipe output matches item
      && expected yield > 0
      && output UOM converts to item base UOM
      && location-item capability active
      && prepared inventory writer ready
      && actor has permission
      && actor is in location scope

Resolver trả SourceType, Eligible, Vietnamese ReasonCode, recipe/version,
expected yield/base UOM, location candidates và constraints.

| Source | Điều kiện |
|---|---|
| Purchase | CanPurchase; downstream revalidate procurement rules |
| Transfer | CanTransfer; downstream revalidate source availability |
| Production | toàn bộ predicate trên |
| Reject | permission + reason |

UI chỉ render candidates backend trả. SetSourcingDecisionAsync phải revalidate
cùng resolver trong transaction. Direct PRODUCTION request không hợp lệ trả
business error tiếng Việt, không tạo allocation.

## 7. Recipe/yield model

ALREADY_SUPPORTED:

- Recipe.PreparedItemId là output identity;
- OutputQuantity là expected net output cho một standard run;
- OutputUnitId convert được về PreparedItem.BaseUnitId;
- RecipeId pin exact version;
- Status, EffectiveDate, ParentVersionId;
- cycle/self-reference/max-depth checks.

YieldPercentage là legacy, không được áp lại cho BTP output.

    ExpectedYieldPerBatchBaseQty
      = Convert(OutputQuantity, OutputUnitId, PreparedItem.BaseUnitId)

MISSING: actual yield, accepted/rejected output, tolerance, production
equipment/location mapping và run-level expected snapshot. Snapshot lên run để
lịch sử không phụ thuộc read model recipe về sau.

## 8. Batch vs demand UOM decision

Restock giữ TargetOutputQuantity, TargetOutputUom và
NormalizedRequiredBaseQuantity.

Production plan giữ:

    PlannedBatchCount
      = ceil(RemainingDemandBaseQty / ExpectedYieldPerBatchBaseQty)

    ExpectedTotalOutputBaseQty
      = PlannedBatchCount * ExpectedYieldPerBatchBaseQty

Mẻ là execution count, không đăng vào PhysicalUnitConversionRegistry.

Current conflict: RequestedRunCount decimal(18,5), check >0 <=9999, UI step=any
và test chấp nhận fractional. New Restock-driven plan nên dùng integer. Không
drop/alter cột legacy ngay; migration additive và audit fractional rows trước.

## 9. Production workflow state machine

Đề xuất mở rộng ProductionRun, giữ numeric values legacy:

| State | Actor/action | Stock effect |
|---|---|---|
| Confirmed (legacy) | run cũ | theo legacy |
| Completed (legacy/new terminal) | đã post | immutable |
| Planned | planner tạo từ allocation | none |
| Released | authorized releaser | none |
| InProgress | operator bắt đầu | none; có thể reserve |
| AwaitingAcceptance | operator ghi actual | none |
| Cancelled | authorized cancel | release reservation/allocation |
| Completed | acceptor duyệt output | atomic actual posting |

Luồng:

~~~mermaid
stateDiagram-v2
    [*] --> Planned
    Planned --> Released
    Released --> InProgress
    InProgress --> AwaitingAcceptance
    AwaitingAcceptance --> Completed
    Planned --> Cancelled
    Released --> Cancelled
    AwaitingAcceptance --> InProgress: trả chỉnh sửa
~~~

Phase đầu nên giữ stock mutation atomic tại acceptance để giảm blast radius.
Material reservation có thể bổ sung; không trừ kho thật ở Planned/Released.
Nếu business cần issue vật lý trước completion, tách ProductionMaterialIssue ở
phase riêng với reversal contract, không ngầm thay đổi.

## 10. Actual-yield inventory posting

Completion request tối thiểu:

- ProductionRunId + RowVersion + RequestKey;
- ActualBatchCount;
- actual input lines theo RecipeDetail/identity/base quantity;
- ActualOutputQuantity + OutputUomId;
- AcceptedOutputQuantity;
- Rejected/WasteQuantity;
- reason khi variance vượt tolerance.

Invariants:

    ActualOutputBase = Convert(actual output -> PreparedItem base)
    AcceptedBase >= 0
    RejectedBase >= 0
    AcceptedBase + RejectedBase <= ActualOutputBase
    ActualBatchCount > 0
    each actual input > 0 and identity matches snapshot

Transaction:

1. lock run/allocation/inventory/layers;
2. revalidate state, scope, permission, rowversion/idempotency;
3. normalize actual input/output;
4. validate tolerance/approval;
5. consume actual inputs from FIFO;
6. credit only AcceptedOutputBase;
7. create output FIFO layer;
8. create ProductionRunOutput and ledger links;
9. register Restock production fulfillment;
10. transition Completed and commit.

Rejected/waste never increases AvailableQty. Unknown disposition remains blocked,
not silently added to stock.

## 11. Restock progress integration

Mở rộng RestockFulfillmentDocumentTypes với PRODUCTION_RUN_OUTPUT. Một durable
ProductionRunOutput là source line; RestockFulfillmentPosting nên có optional
RestockSourcingAllocationId.

    RemainingDemand = max(
      RequiredBaseQuantity
      - TransferAccepted
      - PurchaseAccepted
      - ProductionAccepted,
      0)

Planned/expected quantity không tạo posting và không đóng Restock. Khi completion:

- phân bổ accepted output vào từng active production allocation tối đa phần còn lại;
- tạo one posting per output/allocation/request, unique để replay không trùng;
- under-yield giữ Restock PartiallyReceived/Processing;
- allocation cần trạng thái PartiallyFulfilled/Fulfilled hoặc read model derive từ
  postings;
- residual allocation sau completed run phải được release hoặc gắn supplemental
  run theo quyết định workflow.

Workflow stepper phải branch theo source; production không đi qua PA/PO/Supplier.

## 12. Transfer/Purchase fallback

Under-yield 9.6/10 kg để lại 0.4 kg. Candidate resolver chạy lại trên remaining:

- supplemental ProductionRun;
- Transfer;
- Purchase nếu item policy cho phép;
- Close remaining với permission/reason hiện hữu.

Không tự chuyển source vì đây là quyết định nghiệp vụ. Có thể gợi ý candidate theo
readiness nhưng actor phải xác nhận. Allocation cũ chỉ giữ phần đã fulfilled;
phần residual được release có audit để tránh double allocation.

PreparedItem purchase là NEEDS_OWNER_DECISION. Nếu cho phép, dùng CanPurchase và
supplier package/UOM contract hiện hữu; không suy từ loại PreparedItem.

## 13. Location capability

Current Store chỉ có Active và địa chỉ; không StoreType/CentralKitchen/CanProduce.
Inventory writer mode là migration safety, không phải business capability.

Đề xuất StoreProductionCapability theo Store + XOR item identity, effective dates,
Active, audit/rowversion. Eligibility cần cả item global policy và location policy.

Không tạo khái niệm Central Kitchen giả nếu code chưa có. Nếu Owner cần kitchen
chuyên biệt, thêm LocationType trong task riêng sau khi xác định Store có phải
location authority duy nhất hay không.

## 14. Permission/maker-checker

Current permissions:

- ProductionOrder.View
- ProductionOrder.Create
- ProductionOrder.Confirm

Seed matrix hiện cấp View cho Owner/Region/Store/Warehouse/ShiftSupervisor và
Create/Confirm cho Owner/Store/Warehouse/ShiftSupervisor. SystemAdmin không được
mặc định trong matrix. Controller dùng Confirm cho cả tạo intent POST và apply stock.

Đề xuất tách:

| Permission | Mục đích |
|---|---|
| ProductionOrder.View | xem |
| ProductionOrder.Plan | tạo plan từ Restock |
| ProductionOrder.Release | phát hành |
| ProductionOrder.Start | bắt đầu |
| ProductionOrder.RecordActual | ghi actual input/yield |
| ProductionOrder.AcceptOutput | nhập accepted output |
| ProductionOrder.ApproveVariance | duyệt yield/waste ngoài tolerance |
| ProductionOrder.Cancel | hủy state hợp lệ |
| Restock.SelectProductionSource | chọn nguồn Production |

Maker-checker đề xuất: người RecordActual không tự ApproveVariance khi vượt ngưỡng;
AcceptOutput yêu cầu state hợp lệ và approval nếu cần. Mapping role cuối cùng là
NEEDS_OWNER_DECISION; gợi ý StoreManager plan/release/accept trong StoreScope,
ShiftSupervisor vận hành/ghi actual, Owner duyệt ngoại lệ, WarehouseAccountant
xét nguồn và read-only/plan tùy mô hình bếp trung tâm.

## 15. Costing impact

Current costing đã dùng actual FIFO cost evidence cho input quantities, nhưng
input quantities và mẫu số output đều là planned/expected.

Target:

    TotalActualInputCost = sum(FIFO cost of actual consumed inputs)
    AcceptedOutputUnitCost = TotalActualInputCost / AcceptedOutputBaseQty

NEEDS_OWNER_DECISION: cost của rejected/waste. Khuyến nghị toàn bộ actual consumed
input cost được phân bổ vào accepted output để yield thấp làm unit cost BTP tăng,
đồng thời lưu waste quantity/value riêng cho reporting. Nếu accepted=0 phải fail
hoặc post toàn bộ vào waste expense theo policy được duyệt, không chia cho zero.

Không đổi FIFO layer cũ. Legacy Completed runs giữ valuation hiện tại và được
đánh dấu legacy expected-output behavior trong audit/report.

## 16. UOM impact

- Expected output: Recipe.OutputUnitId -> PreparedItem.BaseUnitId.
- Actual output: input UOM -> cùng PreparedItem.BaseUnitId.
- Actual input: Recipe line UOM hoặc compatible UOM -> input base UOM.
- Accepted/rejected/output phải cùng normalized base dimension.
- Restock allocation nên snapshot AllocatedBaseQuantity + BaseUnitId để tránh
  suy ngược bằng factor procurement khi rounding.
- Batch không phải physical UOM.
- Cross-dimension chỉ hợp lệ qua conversion authority hiện hữu; không tự thêm
  density hoặc conversion.

Precision hiện inventory/restock là decimal(18,3), recipe run count decimal(18,5).
Implementation phải chốt rounding ở conversion boundary và test deterministic.

## 17. Data model gap analysis

| Concern | Current | Gap | Recommendation | Migration | Risk |
|---|---|---|---|---|---|
| Item capability | none | source enum chung | XOR source policy | Yes | Medium |
| Location capability | none | active Store chưa đủ | Store-item capability | Yes | Medium |
| Recipe output | PI + qty + UOM | thiếu snapshot | snapshot on run | Yes | Low |
| Expected yield | OutputQuantity/run | tên run legacy | base snapshot | Yes | Low |
| Planned batches | decimal RequestedRunCount | fractional | nullable int new flow | Yes | Medium |
| Actual batches | none | không audit | ProductionRunOutput | Yes | Low |
| Actual inputs | planned recipe | sai variance | ProductionRunInputActual | Yes | High |
| Actual output | none | expected auto-post | output row | Yes | High |
| Accepted output | none | không có receiving gate | output row | Yes | High |
| Waste/reject | none | không trace | qty/reason/approval | Yes | Medium |
| Recipe version | RecipeId | đủ FK, thiếu display snapshot | keep FK + snapshot | Yes | Low |
| Location | StoreId | thiếu capability | capability FK/policy | Yes | Medium |
| Actor | creator/completer | thiếu role transitions | transition table | Yes | Low |
| Inventory links | ProductionRunId | đủ ledger, thiếu output row | output FK/link | Yes | Medium |
| Restock link | nullable run FK on allocation | không enforce/orchestrate | transactional link + check | Yes | High |
| Audit | timestamps/log | thiếu history | transitions + structured actuals | Yes | Medium |
| Idempotency | Store+RequestKey | completion payload không fingerprint | operation key/fingerprint | Yes | Medium |

## 18. Migration plan

Không tạo migration trong task này. Kế hoạch additive:

### A. InventoryItemSourceCapabilities

- PK int identity.
- IngredientId int nullable FK Restrict.
- PreparedItemId int nullable FK Restrict.
- CanPurchase/CanTransfer/CanProduce bit not null default false.
- Active bit not null default true.
- CreatedAtUtc/UpdatedAtUtc datetime2; UpdatedByStaffId nullable FK.
- RowVersion rowversion.
- XOR check; filtered unique indexes theo IngredientId và PreparedItemId.
- Backfill: report-only trước; mặc định không tự bật CanProduce.

### B. StoreProductionCapabilities

- PK; StoreId FK Restrict.
- IngredientId XOR PreparedItemId.
- CanProduce/Active bit; EffectiveFromUtc/EffectiveToUtc nullable.
- actor/time/rowversion.
- unique active identity per Store; effective date check.
- Backfill chỉ từ evidence Owner xác nhận, không suy từ ProductionRun lịch sử.

### C. ProductionRuns additive

- PlannedBatchCount int nullable.
- OutputPreparedItemId int nullable FK.
- OutputBaseUnitId int nullable FK.
- ExpectedYieldPerBatchBaseQuantity decimal(18,3) nullable.
- ExpectedTotalOutputBaseQuantity decimal(18,3) nullable.
- PlanningSource nvarchar(24) nullable.
- new status values, Released/Started timestamps/actors.
- LegacyContractVersion smallint not null default 1; new flow version 2.
- check constraints chỉ áp v2; giữ RequestedRunCount cho legacy.

### D. ProductionRunInputActuals

- PK, ProductionRunId, RecipeDetailId nullable.
- IngredientId XOR PreparedItemId.
- PlannedBaseQuantity/ActualBaseQuantity decimal(18,3).
- BaseUnitId, RecordedBy/At, RowVersion.
- unique run + source line/identity.

### E. ProductionRunOutputs

- PK, unique ProductionRunId.
- PreparedItemId, OutputUnitId, BaseUnitId.
- ActualOutputQuantity, ActualOutputBaseQuantity.
- AcceptedBaseQuantity, RejectedBaseQuantity decimal(18,3).
- VarianceReason/Approval actor/time.
- ProductionInTransactionId and OutputCostLayerId nullable unique FKs.
- RequestKey/fingerprint; quantity checks.

### F. Restock integration

- RestockSourcingAllocations: add AllocatedBaseQuantity/BaseUnitId; production-link
  consistency check for new contract rows.
- RestockFulfillmentPostings: nullable RestockSourcingAllocationId FK; source type
  allows PRODUCTION_RUN_OUTPUT; unique source output/allocation/request.
- ProductionRunTransitions table for state audit.

Backfill:

1. Inventory/capability dry-run.
2. Integral legacy RequestedRunCount may backfill PlannedBatchCount only as legacy.
3. Fractional runs flagged LEGACY_FRACTIONAL, never rounded silently.
4. Completed legacy ledger remains immutable.
5. Existing PRODUCTION allocations remain blocked/reviewed, not auto-linked by
   nearest timestamp.

Down strategy: drop only new FK/index/table/columns; never rewrite or delete
legacy ledger/ProductionRun. Deployment must be expand -> backfill/flag -> switch
read/write -> optional later cleanup.

## 19. Legacy data repair plan

Dry-run report columns: RestockId/code, allocation id/status/qty/UOM, item
identity/type, recipe/output/yield, Store, capability, ProductionRun link/status,
postings/ledger, actor/time.

Rules:

| Detection | Classification | Action plan |
|---|---|---|
| Raw Ingredient active PRODUCTION | INVALID_BLOCKING | block new execution; review release to unallocated |
| PreparedItem without active recipe | INVALID_BLOCKING | release/re-source after Owner review |
| Recipe output mismatches item | INVALID_BLOCKING | no auto relink |
| yield <=0 or bad UOM | INVALID_BLOCKING | fix recipe via governed workflow |
| location lacks capability | NEEDS_REVIEW | confirm location; do not infer |
| allocation without run | NEEDS_REVIEW | release or create plan after revalidation |
| completed run posted expected | NEEDS_REVIEW | preserve ledger; flag legacy |
| fractional run | NEEDS_REVIEW | preserve; no rounding |
| duplicate active allocation/run link | INVALID_BLOCKING | retain earliest valid only after downstream audit |

SAFE_AUTO_REPAIR chỉ dành cho metadata deterministic không đổi nghiệp vụ, ví dụ
backfill base quantity khi conversion authority duy nhất và exact. Repair command
phải dry-run mặc định, transaction, idempotency key, audit và rerun-safe. Không
hard-code record ID, không sửa production DB trong task này.

## 20. API plan

Read:

- GET Restock/{id}/source-candidates
  trả source eligibility + reason + recipe/yield/location options.
- GET Production/plans/{id}
  trả planned/actual/audit/readiness.
- POST Production/plans/preview
  nhận allocation/location, trả integer batches/expected output/BOM readiness.

Mutation:

- POST Restock/{id}/allocate-production
  request key, rowversions, allocation base qty, location, recipe version.
  Transaction tạo allocation + Planned ProductionRun và link hai chiều.
- POST Production/{id}/release
- POST Production/{id}/start
- POST Production/{id}/record-actual
- POST Production/{id}/accept-output
- POST Production/{id}/approve-variance
- POST Production/{id}/cancel

Mỗi endpoint enforce permission + scope + state + capability + rowversion,
idempotency/fingerprint và trả lỗi tiếng Việt. Backend không tin recipe, expected
yield, identity hoặc cost do client gửi. Existing Execute/ExecuteStock được giữ
cho legacy contract v1 rồi deprecate, không đổi phá vỡ ngay.

## 21. UI plan

Restock source:

- chỉ render candidates backend trả;
- raw item không producible không có Sản xuất nội bộ;
- direct request vẫn backend reject;
- disabled reason chỉ dùng khi có ích cho actor có quyền cấu hình.

Khi chọn Production:

- nhu cầu còn lại + base UOM;
- recipe/version;
- expected yield mỗi mẻ;
- planned integer batches;
- expected total output;
- production location;
- input readiness;
- one primary CTA Tạo kế hoạch sản xuất.

Production detail:

- stepper Planned -> Released -> In progress -> Awaiting acceptance -> Completed;
- planned vs actual panels;
- input actual editing;
- completion form: actual batches/output, accepted, rejected/waste, reason;
- variance and approval status;
- Restock links và remaining demand;
- no button Apply expected output to stock.

States: loading/data/empty/validation/business conflict/technical error/permission
denied; Vietnamese text, focus visible, labels and accessible dialog actions.

## 22. Test plan

Không chạy full suite trong task PLAN. Implementation chạy theo thứ tự isolated ->
affected module -> SQL integration/concurrency -> runtime; full suite chỉ khi
SkillTest trigger hoặc Owner yêu cầu.

Eligibility:

- RawMaterialWithoutProductionCapability_CannotUseProduction
- PreparedItemWithValidRecipe_CanUseProduction
- PreparedItemWithoutActiveRecipe_CannotUseProduction
- LocationWithoutProductionCapability_CannotUseProduction
- DirectProductionRequest_IsRejectedWhenNotEligible
- SourceCandidates_AndMutationUseSameResolver

Planning/UOM:

- ProductionPlan_UsesIntegerBatchCount
- ProductionPlan_CeilsDemandByExpectedYield
- BatchIsNotPhysicalUnitConversion
- ExpectedYield_ConvertsToPreparedItemBaseUom
- PlanningSnapshotsExactRecipeVersion
- FractionalLegacyRun_RemainsReadableButCannotBeCreatedByV2

Completion/inventory/cost:

- ActualYield_NotExpectedYield_IncreasesInventory
- RejectedOutput_DoesNotIncreaseAvailableInventory
- ActualInputQuantity_DrivesFifoConsumption
- LowYield_IncreasesAcceptedOutputUnitCost
- AcceptedZero_DoesNotDivideByZeroOrCreditInventory
- Completion_Replay_DoesNotDuplicateLedgerLayerOrPosting
- ConcurrentAcceptance_AllowsOnlyOneCommit
- StaleRowVersion_ReturnsVietnameseConflict

Restock:

- PlannedProduction_DoesNotMarkDemandReceived
- CompletedAcceptedProduction_UpdatesProgress
- RemainingDemand_UsesAcceptedProductionQuantity
- UnderYield_LeavesRemainingRestockDemand
- SupplementalSource_DoesNotDoubleAllocateResidual
- ProductionOutput_CannotFulfillUnrelatedRestock
- Overage_InventoryPostingFollowsOwnerPolicy

State/permission:

- InvalidProductionTransition_IsRejected
- RecordActual_CannotApproveOwnVarianceWhenMakerCheckerApplies
- OutOfScopeLocation_IsRejected
- ActorWithoutProductionPermission_CannotAllocateOrComplete
- CancelledRun_CannotPostInventory

Multi-level/regression:

- ProductionBom_CycleIsRejected
- ChildPreparedItemAvailability_IsChecked
- ChildRecipeVersion_IsPinned
- PurchaseAndTransferFlowsRemainUnchanged
- LegacyCompletedRunLedgerRemainsImmutable

Existing test files to adapt, not blindly delete assertions:

- ProductionRunIssue119Tests.cs
- ProductionRunExecutionIssue120Tests.cs
- ProductionRunValuationIssue132Tests.cs
- ProductionReadinessIssue148Tests.cs
- RestockProcurementRoutingIssue177Tests.cs
- RestockRequestIssue100Tests.cs

## 23. Runtime plan

Local/dev data only.

A - Raw material:
Cà phê hạt Restock -> candidates chỉ Purchase/Transfer -> direct PRODUCTION POST
bị reject -> không có allocation/run.

B - Prepared item:
Trân châu đã nấu, demand 10 kg, yield 5 kg/mẻ -> plan 2 mẻ, expected 10 kg ->
release/start không đổi tồn.

C - Under-yield:
actual output 9.8, accepted 9.6, reject 0.2 -> inventory +9.6 -> Restock remaining
0.4 -> FIFO unit cost dùng actual inputs/9.6 -> candidate fallback xuất hiện.

D - Overage:
accepted > demand -> thực hiện policy Owner chốt -> Restock consume tối đa remaining,
ledger/audit overage rõ, replay không duplicate.

E - Invalid API/security:
raw item, wrong Store, inactive recipe, stale rowversion, actor thiếu permission ->
backend reject đúng lỗi tiếng Việt.

F - Concurrency:
hai tab accept cùng run -> một success, một replay/conflict, một output layer,
một ledger set và một fulfillment set.

G - Multi-level:
BTP cha cần BTP con -> readiness kiểm tồn BTP con; thiếu thì block/gợi ý dependency
plan theo policy; không auto recurse tạo vô hạn.

Evidence cần capture: request/response, DB rows run/input/output/allocation/posting,
inventory before/after, FIFO layer, Restock before/after và role/action visibility.
Hiện trạng: NOT_RUNTIME_VERIFIED theo stop condition của task plan.

## 24. Exact file implementation plan

Current files dự kiến sửa:

- Application/Constants/ProcurementContractConstants.cs
- Application/Constants/RestockRequestConstants.cs
- Application/Constants/PermissionConstants.Catalog.cs
- Models/Drinks/Recipe.cs
- Models/Inventories/Production/ProductionRun.cs
- Models/Enums/Inventory/ProductionRunStatus.cs
- Models/Inventories/Stock/RestockSourcingAllocation.cs
- Models/Inventories/Stock/RestockFulfillmentPosting.cs
- Data/AppDbContext.cs
- Data/Configurations/Inventories/Production/ProductionRunConfiguration.cs
- Data/Configurations/Inventories/Stock/RestockSourcingAllocationConfiguration.cs
- Data/Configurations/Inventories/Stock/RestockFulfillmentPostingConfiguration.cs
- Application/DTOs/Admin/Production/ProductionRunDtos.cs
- Application/DTOs/Admin/RestockRequests/RestockRequestDtos.cs
- Application/DTOs/Admin/RestockRequests/RestockWorkflowDtos.cs
- Application/Interfaces/Admin/Production/IProductionRunService.cs
- Application/Interfaces/Admin/Production/IProductionRunExecutionService.cs
- Application/Interfaces/Inventories/IRestockFulfillmentPostingService.cs
- Application/Services/Admin/Production/ProductionRunService.cs
- Application/Services/Admin/Production/ProductionReadinessService.cs
- Application/Services/Admin/Production/ProductionRunExecutionService.cs
- Application/Services/Inventories/RestockRequestService.cs
- Application/Services/Inventories/RestockFulfillmentPostingService.cs
- Application/Services/Inventories/RestockRequestWorkflowService.cs
- Areas/Admin/Controllers/AdminProductionOrderController.cs
- Areas/Admin/Controllers/AdminRestockRequestsController.cs
- Areas/Admin/Views/AdminProductionOrder/Create.cshtml
- Areas/Admin/Views/AdminRestockRequests/Details.cshtml
- Extensions/Services/ApplicationServiceExtensions.cs
- Scripts/SeedAll.sql

New files dự kiến:

- Models/Inventories/Production/InventoryItemSourceCapability.cs
- Models/Inventories/Production/StoreProductionCapability.cs
- Models/Inventories/Production/ProductionRunInputActual.cs
- Models/Inventories/Production/ProductionRunOutput.cs
- Models/Inventories/Production/ProductionRunTransition.cs
- configurations tương ứng trong Data/Configurations/Inventories/Production
- IInventorySourceEligibilityService + InventorySourceEligibilityService
- IProductionPlanningService + ProductionPlanningService
- IProductionCompletionService + ProductionCompletionService
- DTO request/result/candidate riêng theo convention hiện có
- migration additive mới, không sửa InitialCreate
- dry-run/repair service hoặc command theo convention repo
- ProductionRestockEligibilityTests.cs
- ProductionBatchYieldTests.cs
- ProductionBatchYieldSqlServerTests.cs
- ProductionLegacyRepairTests.cs

Tên cuối cùng phải theo convention khi implementation inspect lại ownership; không
gom service thành một class nhiều cờ.

## 25. Risks

| Risk | Mitigation |
|---|---|
| Double stock posting | operation idempotency + unique output/ledger/posting indexes |
| Allocation và run lệch transaction | tạo/link trong cùng transaction + row locks |
| Legacy decimal run | additive v2 fields; không round/rewrite |
| Recipe đổi sau plan | pin RecipeId + expected snapshot |
| Low yield sai cost | actual input cost / accepted output |
| Zero accepted output | block division/post or governed waste-only path |
| Overage đóng quá demand | cap Restock posting; policy riêng cho inventory |
| Multi-level cycle | reuse cycle/depth guard; dependency graph visited set |
| N+1 candidates | projection/batched recipe/capability lookup |
| Permission chỉ ẩn UI | backend permission/scope/state on every mutation |
| Migration downtime | expand/backfill/switch; filtered indexes |
| Repair xóa lịch sử | dry-run, no delete, manual review for downstream docs |

## 26. Open Owner decisions

1. Overage:
   - A recommended: nhập toàn bộ accepted physical output, Restock chỉ consume
     remaining; phần dư là tồn không gắn demand.
   - B chặn overproduction.
   - C yêu cầu approval khi vượt tolerance.
2. Legacy fractional runs giữ được tạo mới hay chỉ read-only?
3. Capability global + Store override hay bắt buộc explicit Store-item?
4. PreparedItem có được Purchase không, scope nào?
5. Role mapping cho Plan/Release/RecordActual/Accept/ApproveVariance.
6. Actual input bắt buộc nhập từng line hay default planned rồi chỉnh exception?
7. Tolerance theo recipe, item, location hay global?
8. Under-yield tự tạo supplemental plan hay actor chọn fallback?
9. Accepted=0 phân bổ cost toàn bộ vào waste hay block completion?
10. Có cần lot/expiry/best-before cho BTP output trong phase đầu?
11. Có cần material issue trước completion hay atomic acceptance đủ cho MVP?
12. Multi-level dependency tự tạo child runs hay chỉ block/gợi ý?
13. Production output có thể phân bổ nhiều Restock trong một run không?
14. Cần Central Kitchen entity/type riêng hay Store là location authority?

## 27. Suggested implementation order

1. Owner chốt các quyết định 1-6 tối thiểu.
2. Domain capability + source resolver, khóa bug raw Ingredient trước.
3. Additive schema cho plan/actual/output/audit/link.
4. Production planning từ Restock allocation.
5. State transitions và permission split.
6. Actual completion + FIFO/ledger atomic posting.
7. Restock fulfillment/progress và under-yield fallback.
8. UI candidate/plan/detail/completion.
9. Dry-run + controlled legacy repair.
10. Isolated/affected/SQL concurrency/runtime verification.
11. Chỉ mở full suite nếu SkillTest trigger.
12. Rollout feature flag/cutover; giữ legacy read path.

## 28. Exact implementation plan

### Phase 1 - Domain contract/capability

- Sửa constants, models, AppDbContext/EF configurations, DI và seed permission.
- Tạo InventoryItemSourceCapability, StoreProductionCapability và resolver.
- Restock candidate/mutation cùng dùng resolver; backend khóa invalid PRODUCTION.
- Migration: hai bảng capability + indexes/checks.
- Tests: eligibility, permission/scope, candidate parity.
- Dependency: Owner quyết định capability scope và role.
- Rollback risk: thấp/trung bình; có thể feature-flag candidate resolver.

### Phase 2 - Recipe expected yield

- Reuse Recipe output contract và normalizer.
- Thêm run snapshot fields; validate active exact Recipe/output/UOM.
- Không dùng YieldPercentage lần hai.
- Migration: nullable snapshots + contract version.
- Tests: normalization, recipe version pin, invalid yield/UOM.
- Dependency: Phase 1.
- Rollback risk: thấp vì additive.

### Phase 3 - Source resolver

- Thêm source-candidates API/read model.
- SetSourcingDecision revalidate transactionally.
- Khi PRODUCTION hợp lệ, gọi planning service thay vì tạo allocation mồ côi.
- Tests direct request, stale rowversion, concurrent allocation.
- Dependency: Phase 1-2.
- Rollback risk: trung bình vì thay hành vi source selection.

### Phase 4 - Production document/workflow

- Mở rộng status và transition audit.
- Tạo planned run + allocation link trong một transaction.
- Thêm Release/Start/RecordActual/Cancel endpoints và permission split.
- Migration: status checks, actor/time, transitions.
- Tests state machine, maker-checker, idempotency.
- Dependency: Owner role/state decisions.
- Rollback risk: cao nếu đổi legacy status; giảm bằng ContractVersion.

### Phase 5 - Actual yield + inventory posting

- Tạo actual input/output entities và completion service.
- Refactor reusable FIFO planning từ execution legacy, không copy logic.
- Chỉ accepted output tạo inventory/FIFO; actual inputs tạo PRODUCTION_OUT.
- Giữ ExecuteStock v1 cho legacy read/controlled execution đến cutover.
- Migration: input/output tables, unique links/checks.
- Tests actual/accepted/waste, cost, replay, concurrency, rollback.
- Dependency: Phase 4 + overage/waste decisions.
- Rollback risk: cao; feature flag và SQL integration bắt buộc.

### Phase 6 - Restock progress

- Cho fulfillment source PRODUCTION_RUN_OUTPUT.
- Link posting tới allocation; update remaining bằng accepted quantity.
- Branch workflow stepper; under-yield residual release/supplement policy.
- Migration: posting allocation FK/index, allocation base snapshot/status.
- Tests planned not fulfilled, accepted fulfilled, under/over yield.
- Dependency: Phase 5.
- Rollback risk: cao do cross-module transaction.

### Phase 7 - UI

- Restock render backend candidates.
- Production plan/detail/completion forms và localized errors/states.
- Không còn Apply expected output to stock; batch input integer cho v2.
- Responsive/accessibility smoke; không thêm dependency.
- Tests Razor/source + integration routes.
- Dependency: stable API Phase 3-6.
- Rollback risk: trung bình; legacy UI route giữ tạm nếu cần.

### Phase 8 - Legacy repair

- Tạo dry-run report và repair command idempotent.
- Không tự sửa ambiguous capability, fractional runs hoặc completed ledger.
- Release invalid orphan allocations chỉ theo approved policy/audit.
- Migration/data: không delete, không rewrite historical ledger.
- Tests dry-run, rerun, downstream preservation.
- Dependency: target schema đã deploy.
- Rollback risk: cao với data; default dry-run và batch approval.

### Phase 9 - Tests/runtime

- Chạy exact isolated tests ở mục 22.
- Chạy affected Production + Restock + Inventory/FIFO modules.
- Chạy SQL Server idempotency/concurrency.
- Runtime A-G ở mục 23 với local/dev data.
- Full suite chỉ khi trigger rõ.
- Ghi exact command, count, duration và known unrelated failures.
- Dependency: tất cả phase.
- Rollback risk: không có data production; runtime dùng demo/local.

## Evidence index

| Kết luận | File/class/test | Trạng thái |
|---|---|---|
| PRODUCTION enum chung | Application/Constants/ProcurementContractConstants.cs | CODE_CONFIRMED |
| UI render Production cứng | Areas/Admin/Views/AdminRestockRequests/Details.cshtml | CODE_CONFIRMED |
| Backend không eligibility | RestockRequestService.SetSourcingDecisionAsync | CODE_CONFIRMED |
| Allocation có nullable run FK | RestockSourcingAllocation.cs/config | CODE_CONFIRMED |
| Production workflow thật | ProductionRunService/ExecutionService | CODE_CONFIRMED |
| Expected được nhập tồn | ProductionRunExecutionService | CODE_CONFIRMED |
| Fractional batch được hỗ trợ | ProductionRunConfiguration + Issue119 test + Create.cshtml | CODE_CONFIRMED |
| Actual yield thiếu | ProductionRun entity/DTO/controller | CODE_CONFIRMED |
| Restock fulfillment thiếu production | RestockFulfillmentPostingService | CODE_CONFIRMED |
| Restock progress dựa posting | RestockRequestWorkflowService | CODE_CONFIRMED |
| Location capability thiếu | Store.cs + repo-wide capability scan | CODE_CONFIRMED |
| Recipe output/yield/UOM có sẵn | Recipe.cs + ProductionReadinessService | CODE_CONFIRMED |
| Multi-level cycle guard | AdminRecipeService | CODE_CONFIRMED |
| Actual runtime data | Không chạy theo task plan | NOT_RUNTIME_VERIFIED |

## Task verification record

- Rules/skills đã đọc: Rule.md, CafeChain/RULES.md, FIX.md,
  UI_LANGUAGE_AND_TEST_SCOPE_RULE.md, CafeChain/SkillTest_SKILL.md,
  AGENTS.md, CONTEXT.md, issue tracker/domain/triage docs, triage, to-issues,
  improve-codebase-architecture và tdd skills.
- Không tìm thấy AGENT_TASK_RULES.md và .agents/skills/SkillTest/SKILL.md; dùng
  CafeChain/SkillTest_SKILL.md là authority tương đương có trong repo.
- Epic #386 và analysis issue #387 đã tạo.
- RULES_AND_SKILLS_READ và TEST_SCOPE_PLAN đã comment lên issue #387.
- Architecture audit comment không được gửi vì công cụ chặn egress chi tiết nội
  bộ; toàn bộ audit được lưu trong tài liệu này.
- Automated tests: không chạy, đúng test scope cho docs-only plan.
- Migration: không tạo.
- Data repair: không chạy.
- Runtime: không chạy; đã lập exact plan.
- Implementation: không thực hiện.
