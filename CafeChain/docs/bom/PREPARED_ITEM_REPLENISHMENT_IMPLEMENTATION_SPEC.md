# PreparedItem Stock Threshold and Production Replenishment - Implementation Specification

## Document Status

```text
Stage: /to-spec only
Status: READY_FOR_OWNER_SPEC_REVIEW
Current code evidence: feature/POS @ a77d3ad5
Migration: REQUIRED, ADDITIVE, NOT CREATED IN THIS TASK
Implementation: NOT STARTED
Tickets: NOT STARTED
```

This specification consumes the approved PreparedItem replenishment discovery and Owner decisions. It defines implementation behavior; it does not authorize production code, migration creation, seed mutation or deployment in this task.

## 1. Problem Statement

CafeChain already tracks PreparedItem inventory per Store and can move a confirmed stock alert through RestockRequest, a production sourcing allocation, ProductionRun, accepted output, FIFO inventory posting and fulfillment. The chain is not yet safe or complete for operational replenishment because:

- a Store can configure a low-stock threshold but cannot persist a distinct target stock level;
- the threshold list does not correctly project canonical PreparedItem rows;
- UI and StockAlert service disagree at the exact threshold boundary;
- current demand suggestion replenishes only to the low threshold;
- a cancelled ProductionRun can leave its production allocation active and continue covering demand;
- accepted output updates physical inventory and fulfillment but does not refresh the derived StockAlert/replenishment state;
- the current RBAC seed and threshold service contradict the approved separation of duties.

The user needs to see when a Store's PreparedItem is low, understand the target and uncovered need, explicitly plan production without duplicate supply, execute the existing production workflow, and see accepted output restore physical stock and demand evidence without losing Recipe traceability.

## 2. Solution

Extend the existing chain instead of introducing a second demand or production domain:

```text
StoreInventory
-> StockAlert
-> RestockRequest
-> RestockSourcingAllocation(PRODUCTION)
-> ProductionRun
-> InventoryTransaction / FIFO
-> RestockFulfillmentPosting
```

Add a nullable, Store-specific `TargetStockLevel` policy to StoreInventory. Provide one bounded replenishment query authority that calculates usable stock, low state, gross need, open production coverage and net need. Revalidate that authority when creating or adjusting demand and when planning production. Preserve explicit StoreManager confirmation. Correct cancellation so it releases production coverage. Preserve full accepted physical output and perform a best-effort, idempotent post-commit alert reevaluation.

## 3. Goals

1. Distinguish low-stock warning from target replenishment quantity.
2. Make the policy authority `Store + canonical PreparedItem`.
3. Fail closed when target stock is not configured.
4. Use one calculation authority across query, UI and mutations.
5. Reuse RestockRequest as replenishment demand.
6. Count only proven non-terminal production allocations as open coverage.
7. Prevent cancelled production from covering demand.
8. Pin the exact current PreparedItem Recipe only when ProductionRun is planned.
9. Preserve full accepted-output inventory and capped demand fulfillment.
10. Refresh derived alert/need state after inventory truth commits.
11. Preserve Store scope, AccountPermissionOverride and approved role separation.
12. Keep queries bounded, set-based and business-readable in Vietnamese.

## 4. Non-goals

- ProductionDemand or another demand table.
- A second replenishment or production state machine.
- MRP, demand forecasting, scheduling or batch optimization.
- Automatic ProductionRun creation, Release or Start.
- Silent mutation of an open run after POS consumption.
- Recursive child ProductionRuns.
- PreparedItem supplier PO fallback without an explicit supplier contract.
- RecipeIdentity or future Recipe scheduling.
- Production workflow, inventory ledger or FIFO redesign.
- A generic outbox framework.
- Backfilling target stock from low threshold.
- Rewriting legacy Recipe, ProductionRun, Order or inventory history.

## 5. Current-state Evidence

### 5.1 Reusable authorities

- StoreInventory is Store-scoped and supports stable PreparedItem identity, AvailableQty, ReservedQty, MinStockLevel and RowVersion.
- PreparedItem.BaseUnitId is the physical stock UOM authority.
- StockAlert supports canonical PreparedItem and one active alert per Store + PreparedItem.
- RestockRequest supports one active request per Store + PreparedItem, source allocations, transitions and fulfillment.
- Production source eligibility proves CanProduce, Store capability, exact current Recipe, permission and Store scope.
- ProductionRun pins RecipeId when a production allocation creates a Planned run.
- Production acceptance atomically consumes actual inputs by FIFO, credits full accepted output, creates a cost layer, caps fulfillment to remaining demand and handles Completed replay.
- RequestKey/fingerprint, unique indexes, RowVersion, SQL locks and state revalidation already provide most duplicate-command protections.

### 5.2 Confirmed gaps

- StoreInventory has no persistent target stock policy.
- Inventory threshold projection maps Ingredient and Recipe legacy but not canonical PreparedItem.
- StockAlert uses strict `<`; current threshold Razor uses `<=`.
- current alert-to-request suggestion uses `threshold - usable`, conflating trigger and target.
- ProductionRun cancellation does not release its linked production allocation.
- Production acceptance does not reevaluate the output PreparedItem alert after commit.
- no repository-wide durable outbox convention was found for this boundary.

## 6. Owner Decisions

- PreparedItem remains global stable stock identity.
- replenishment policy is Store-specific.
- StoreManager configures low and target within authorized Store scope.
- RestockRequest remains demand authority.
- ProductionRun remains executable authority.
- missing TargetStockLevel fails closed for quantity calculation.
- open production coverage is planning evidence, never inventory.
- allocation quantity, not rounded expected batch output, is coverage authority.
- demand does not pin Recipe; planning does.
- pinned Recipe never switches silently.
- cancelled runs do not cover demand.
- accepted output credits full physical quantity.
- fulfillment is capped to remaining demand.
- post-accept reevaluation occurs after inventory commit.
- no new replenishment permission by default.
- migration is additive and does not backfill target from low.

## Implementation Decisions

- Build one Store-scoped replenishment query/calculation seam and make UI plus mutation commands consume it.
- Persist only one new policy field, TargetStockLevel, on StoreInventory; add no demand entity.
- Reuse current RestockRequest snapshot fields for PreparedItem base-UOM evidence instead of adding parallel snapshot columns.
- Keep demand creation and demand adjustment explicit; never mutate a pinned ProductionRun to chase new consumption.
- Correct cancellation in the existing Production operation so run and allocation lifecycle remain transactionally consistent.
- Run post-accept alert reevaluation after inventory commit with best-effort recovery because no suitable durable outbox convention exists.
- Correct the CODE_CONFIRMED RBAC seed/service mismatch as an implementation prerequisite without adding permissions.
- Test behavior at application seams and SQL transaction boundaries, not private helper structure.

## 7. Ubiquitous Language

| Technical concept | User-facing term | Contract |
|---|---|---|
| MinStockLevel | Ngưỡng cảnh báo tồn thấp | Trigger only |
| TargetStockLevel | Mức tồn mục tiêu | Desired recovery level |
| StockAlert | Cảnh báo tồn kho | Observation |
| RestockRequest for PreparedItem | Nhu cầu bổ sung | Demand and fulfillment authority |
| Open production allocation | Nguồn sản xuất đang mở | Planning coverage only |
| NetProductionNeed | Còn cần bổ sung | Uncovered need |
| AcceptedOutputBase | Sản lượng đạt | Physical output accepted into inventory |
| PreparedItem | Bán thành phẩm | Stable stock identity |
| ProductionRun | Lệnh sản xuất | Executable instruction |

The UI must not call open coverage `Tồn dự kiến`, `Tồn kho` or any equivalent that implies physical stock.

## 8. User Stories

1. As a StoreManager, I want to configure a low threshold for a PreparedItem in my Store, so that the Store can warn before operations run out.
2. As a StoreManager, I want to configure a separate target stock level, so that replenishment restores an operational quantity rather than merely reaching the warning threshold.
3. As a StoreManager, I want missing target configuration to be explicit, so that the system never invents a production quantity.
4. As a StoreManager, I want to see on-hand, reserved and usable quantities, so that I understand why an alert exists.
5. As a StoreManager, I want canonical PreparedItem names and base UOMs, so that I do not manage a Recipe version as stock identity.
6. As a StoreManager, I want low status to use one strict boundary everywhere, so that the list and alert do not disagree.
7. As a StoreManager, I want to see gross need, open production coverage and uncovered need separately, so that open work is not mistaken for inventory.
8. As a StoreManager, I want demand creation to revalidate current data, so that a stale browser calculation cannot overproduce.
9. As a StoreManager, I want one active demand reused and adjusted, so that concurrent actions do not create duplicate requests.
10. As a StoreManager, I want PreparedItem shortages to prefer internal production, so that they do not silently enter supplier procurement.
11. As a StoreManager, I want production eligibility to fail closed, so that missing capability or Recipe evidence cannot create an invalid run.
12. As a StoreManager, I want a newly published Recipe to be used if planning has not happened, so that new production uses the current formula.
13. As an operator, I want a planned run to retain its pinned Recipe, so that execution and costing remain traceable after a later Recipe publish.
14. As a StoreManager, I want cancellation to release demand coverage, so that I can safely replan the uncovered quantity.
15. As a ShiftSupervisor, I want to Start and Record Actual only for released runs in my Store, so that execution follows the existing workflow.
16. As a BusinessOwner, I want to approve variance without becoming the daily production operator, so that maker-checker separation remains intact.
17. As a StoreManager, I want accepted output to credit the full physical quantity, so that inventory reflects what was actually accepted.
18. As a StoreManager, I want underproduction to leave remaining demand, so that unfinished need remains visible.
19. As a StoreManager, I want overproduction to fulfill only the original demand while keeping excess stock, so that demand and physical truth are both correct.
20. As a StoreManager, I want alert state refreshed after acceptance, so that recovered inventory is reflected without weakening the committed inventory transaction.
21. As a StoreManager, I want additional POS consumption to recalculate need without mutating open runs, so that I explicitly decide whether to add demand.
22. As a RegionManager, I want read-only visibility within my scope, so that I can oversee Stores without changing policy or production.
23. As a WarehouseAccountant, I want inventory/BOM visibility without implicit Production Plan authority, so that duties remain separated.
24. As a SystemAdmin, I want technical access to remain separate from business authority, so that the technical role is not a replenishment superuser.
25. As an auditor, I want to trace alert, demand, allocation, run, Recipe, actuals, inventory movement and fulfillment, so that every production replenishment can be explained.

## 9. Preserved Domain Invariants

- PreparedItem is stable stock identity; RecipeId is exact formula/version evidence.
- canonical StoreInventory is quantity and policy authority for PreparedItem.
- POS continues one-level stock deduction; nested BOM is not recursively deducted.
- ProductionRun pins exact RecipeId and historical runs do not re-resolve live Recipe.
- expected/planned output never changes inventory.
- accepted output is physical credit authority.
- actual input and FIFO consumption remain costing authority.
- full accepted output is credited even when it exceeds demand.
- RestockFulfillmentPosting never exceeds remaining demand.
- legacy historical evidence is readable and is not rewritten.
- cycle/depth guards remain unchanged.

## 10. Store Policy Model

The policy authority tuple is:

```text
StoreId + canonical PreparedItemId
```

Policy fields are stored on the authoritative canonical StoreInventory row:

- `MinStockLevel`: nullable low-warning trigger in PreparedItem base UOM.
- `TargetStockLevel`: nullable desired recovery level in the same base UOM.

Validation:

```text
MinStockLevel is null or >= 0
TargetStockLevel is null or >= 0
if both are present: TargetStockLevel >= MinStockLevel
```

Only canonical PreparedItem rows are editable through the new flow. Legacy/superseded BTP rows remain readable through existing compatibility authority but cannot become independent policy owners.

Policy mutation must use RowVersion and effective `InventoryThreshold.Update` permission scoped to the selected Store. Updating policy must never mutate AvailableQty or ReservedQty.

## 11. TargetStock Migration Contract and Plan

The future implementation requires one additive migration:

```text
StoreInventories.TargetStockLevel decimal(18,3) NULL
```

Required migration behavior:

- add the nullable column, EF model configuration, designer and snapshot updates;
- use the same precision/scale and base-UOM semantics as MinStockLevel;
- leave every existing row NULL;
- do not copy MinStockLevel into TargetStockLevel;
- do not modify PreparedItem master, StockAlert, RestockRequest or ProductionRun history;
- Down removes only constraints and the new column according to repository convention;
- seed changes, if any, are idempotent and do not write business target values.

DB constraints are approved because every existing value starts NULL:

```text
TargetStockLevel IS NULL OR TargetStockLevel >= 0

TargetStockLevel IS NULL
OR MinStockLevel IS NULL
OR TargetStockLevel >= MinStockLevel
```

Service validation remains mandatory to return Vietnamese business messages before a DB exception.

This specification describes the migration only. The `/to-spec` task creates no migration.

## 12. Threshold Semantics

One shared calculation/query authority defines low stock:

```text
UsableStockBase = AvailableQty - ReservedQty

IsLow = MinStockLevel != null
        && UsableStockBase < MinStockLevel
```

At `UsableStockBase == MinStockLevel`, `IsLow` is false and an active threshold alert may resolve. Razor must consume projected `IsLow`; it must not repeat `<=` logic.

If MinStockLevel is NULL, the expression yields `IsLow = false`, but `LowConfigured = false` and DataStatus must identify the policy as unconfigured. UI must not present that row as proven healthy stock.

## 13. Replenishment Calculation

The dedicated calculation authority returns nullable need values:

```text
GrossNeedBase = TargetStockLevel != null
    ? max(TargetStockLevel - UsableStockBase, 0)
    : unavailable

CreditableOpenProductionCoverageBase =
    sum(normalized active PRODUCTION allocation quantity)
    where linked ProductionRun status is:
      Planned
      Released
      InProgress
      AwaitingVarianceApproval
      AwaitingAcceptance

NetProductionNeedBase = GrossNeedBase is available
    ? max(GrossNeedBase - CreditableOpenProductionCoverageBase, 0)
    : unavailable
```

Coverage rules:

- allocation quantity is normalized to PreparedItem base UOM through existing UOM authority;
- expected batch output is display evidence only and is not coverage authority;
- only Active PRODUCTION allocation rows with a linked non-terminal run count;
- Completed is excluded because accepted output is already inventory/fulfillment;
- Cancelled is excluded regardless of stale allocation state;
- coverage is never added to StoreInventory and never labeled as stock;
- target missing makes GrossNeed and NetNeed unavailable, not zero;
- active non-production sourcing for the same request is shown separately and blocks automatic production suggestion until the source decision is reconciled.

## 14. Bounded Replenishment Read Model

Introduce one application query seam equivalent to `PreparedItemReplenishmentReadModel`. It is the highest shared seam for threshold UI, StockAlert/Restock details and the lightweight Recipe Workspace signal.

Projection fields:

```text
StoreId / StoreName
PreparedItemId
PreparedItemName
PreparedItemCode
BaseUnitId / BaseUnitCode / BaseUnitName
OnHandBase
ReservedBase
UsableBase
LowThresholdBase
TargetStockBase
LowConfigured
TargetConfigured
IsLow
GrossNeedBase nullable
OpenProductionCoverageBase
NetNeedBase nullable
ActiveAlert summary nullable
ActiveRestockRequest summary nullable
OpenProductionRun summaries bounded
HasMoreOpenRuns / OpenRunTotal
DataStatus
BusinessMessageVi
RowVersion for authorized policy mutation
```

Suggested stable internal statuses include `READY`, `LOW_TARGET_MISSING`, `LEGACY_IDENTITY_REVIEW`, `ACTIVE_NON_PRODUCTION_SOURCE` and `STORE_SCOPE_DENIED`. UI maps them to Vietnamese and never renders the code.

The query must select one authorized Store, use set-based aggregates, and return a bounded run list with transparent total/has-more metadata.

## 15. StockAlert Lifecycle

- StockAlert remains an observation, not demand or executable work.
- evaluation uses canonical PreparedItem and strict `<` low boundary.
- one active OPEN/CONFIRMED alert per Store + PreparedItem remains enforced by the existing filtered unique index.
- concurrent evaluation returns or updates the single active alert after retry.
- when usable reaches or exceeds low, the active threshold alert resolves.
- low may still be detected when target is missing, but alert details state that a target is required before calculating replenishment quantity.
- no alert operation changes physical inventory.

## 16. RestockRequest Demand Contract

RestockRequest remains the only replenishment demand authority.

### 16.1 Creation from confirmed alert

At command time, revalidate in one application operation:

- effective permission and Store scope;
- active canonical PreparedItem;
- active alert and current low condition;
- current low and target policy;
- current StoreInventory RowVersion/quantity;
- current open production coverage;
- current net need;
- existing active RestockRequest.

The server ignores stale browser quantities. If target is missing or net need is unavailable, creation fails with a Vietnamese explanation. If net need is zero, no new quantity is requested.

If no active request exists:

```text
RequestedQuantity = NetProductionNeedBase
SuggestedQuantity = NetProductionNeedBase
```

### 16.2 Snapshot mapping

Reuse existing fields for PreparedItem production demand:

- `SuggestionAvailableSnapshot` = usable stock in base UOM;
- `SuggestionMinLevelSnapshot` = low threshold in base UOM;
- `TargetStockProcurementQuantity` = target snapshot only, with ProcurementUnitId fixed to PreparedItem base UOM; it is never policy authority;
- `SuggestionIncomingQuantitySnapshot` = creditable open production coverage in base UOM;
- `SuggestedQuantity` = net need in base UOM;
- `RequestedQuantity` = manager-confirmed demand in base UOM;
- `SuggestionReason` = concise Vietnamese explanation, never raw JSON.

Existing procurement-named properties remain internal compatibility fields. PreparedItem UI uses replenishment terms and physical base UOM.

Gross need is reproducible from target snapshot minus usable snapshot. Net need and open coverage snapshots remain explicit. No extra demand snapshot columns are required in this phase.

### 16.3 Existing active request and continued consumption

The unique active Store + PreparedItem request remains the duplicate-demand guard. A second creation returns the active request.

When POS consumption increases need while a request/run is open, the read model recalculates current quantities but never mutates an existing ProductionRun. The explicit demand-adjustment command computes:

```text
RemainingRequestDemandBase =
  max(RequestedQuantity
      - ClosedRemainingQuantity
      - FulfillmentPostedBase, 0)

ActiveRequestUnallocatedBase =
  max(RemainingRequestDemandBase
      - all proven active sourcing allocations in base UOM, 0)

AdditionalDemandBase =
  max(NetProductionNeedBase - ActiveRequestUnallocatedBase, 0)
```

StoreManager confirms `AdditionalDemandBase`. Zero does not create an adjustment. The command revalidates RowVersion, quantities, allocations and scope. It does not change batch count, RecipeId or released execution quantities.

## 17. Production Source Selection

PreparedItem replenishment offers PRODUCTION only when all are proven:

- PreparedItem is active and `CanProduce`;
- Store production capability is active;
- shared exact PreparedItem current Recipe resolver returns one Recipe;
- output contract normalizes to PreparedItem base UOM;
- actor has `ProductionOrder.Plan` in the Store;
- current demand remains uncovered.

PURCHASE is not a fallback. It is available only when both CanPurchase and a PreparedItem-specific supplier/package contract exist. CURRENT supplier-package authority cannot prove this contract, so PreparedItem purchase remains fail closed with a Vietnamese reason.

## 18. Recipe Version Pinning

- StockAlert and RestockRequest are Recipe-version agnostic.
- before a ProductionRun exists, planning resolves the current exact PreparedItem Recipe at the business instant;
- ProductionRun creation in Planned state persists exact RecipeId in the same transaction as the PRODUCTION allocation;
- Release, Start, Record Actual, variance approval and Accept use the persisted RecipeId;
- publishing a new Recipe after planning does not alter the run;
- using the new Recipe requires explicit cancel/replan while cancellation is legal;
- no local Recipe selector may be added.

## 19. Production Cancellation Correction

Cancel remains legal only from the current allowed states, Planned and Released. The cancellation operation becomes one transactionally safe unit:

1. lock/reload ProductionRun and validate RowVersion/state/permission/Store scope;
2. load the unique linked PRODUCTION allocation and RestockRequest;
3. transition ProductionRun to Cancelled and preserve RecipeId/history;
4. transition the allocation from Active to Released/Cancelled using the existing allocation status vocabulary selected during implementation;
5. recompute RestockRequest sourcing state from remaining active allocations;
6. record transitions/reason/actor;
7. commit once.

After commit the cancelled allocation contributes zero open coverage and its quantity is available for explicit replan.

Concurrency behavior:

- cancel replay on an already Cancelled run returns the existing outcome without releasing twice;
- cancel versus replan serializes on request/run state; replan before cancel commit sees active coverage, replan after commit sees released coverage;
- accept versus cancel cannot both succeed because state revalidation permits them from disjoint states;
- stale RowVersion returns a stable internal conflict and Vietnamese reload message.

The run, RecipeId, transitions and source linkage are never deleted.

## 20. Acceptance and Fulfillment

The current acceptance transaction remains inventory authority:

1. authorize `ProductionOrder.AcceptOutput` in run Store;
2. lock and revalidate the v2 run;
3. consume actual inputs with existing FIFO authority;
4. credit full `AcceptedOutputBase` to canonical PreparedItem StoreInventory;
5. create the full output FIFO cost layer;
6. register fulfillment as `min(AcceptedOutputBase, RemainingDemandBase)`;
7. complete the run and persist transition/cost evidence;
8. commit once.

Underproduction leaves remaining demand. Overproduction credits all physical output but fulfills only the request remainder. Excess does not spill into another request. Completed replay creates no second inventory movement, cost layer or fulfillment.

## 21. Post-accept Reevaluation

After the acceptance transaction has committed successfully:

1. invoke bounded, idempotent `EvaluateStoreInventoryItemAsync` for the output StoreInventory;
2. refresh/resolve the PreparedItem StockAlert using current usable quantity and strict low boundary;
3. allow subsequent reads to recalculate current gross/open/net need;
4. deliver notifications through existing StockAlert notification behavior.

No suitable durable outbox convention exists in CURRENT HEAD. This phase therefore uses best-effort post-commit reevaluation:

- catch and log reevaluation/notification failure with ProductionRunId, StoreInventoryId and StoreId;
- return successful acceptance because durable inventory truth is already committed;
- keep evaluation idempotent;
- subsequent POS, receipt, transfer, manual evaluation or reconciliation safely recovers freshness;
- do not create a generic outbox framework.

Acceptance result may include a non-blocking internal diagnostic flag for observability, but business UI must not report inventory failure after a successful commit.

## 22. Read/API Seams

Preferred seams:

1. one replenishment query/calculation service for policy, need and open coverage;
2. one policy mutation operation updating low + target with RowVersion;
3. existing StockAlert evaluation service;
4. existing RestockRequest create/adjust/source operations using the query authority;
5. existing Production source eligibility/current Recipe resolver;
6. existing Production operations with corrected cancellation;
7. existing acceptance and fulfillment services with post-commit reevaluation.

Controllers and Razor do not calculate low, gross need, open coverage or net need. DTOs carry nullable quantities and localized boundary messages. Mutation responses use stable internal reason codes with Vietnamese UI mappings.

## 23. UX Contract

### 23.1 Inventory Threshold

Per authorized Store, show:

- Bán thành phẩm business name;
- technical code as secondary metadata;
- base UOM;
- Tồn khả dụng;
- Ngưỡng cảnh báo tồn thấp;
- Mức tồn mục tiêu;
- status/explanation.

StoreManager sees contextual edit actions. RegionManager sees read-only information without disabled mutation controls. Missing target displays: `Cần cấu hình Mức tồn mục tiêu trước khi tính số lượng bổ sung.`

### 23.2 StockAlert and RestockRequest

Use:

- `Cảnh báo tồn kho`;
- `Nhu cầu bổ sung`;
- `Nguồn sản xuất`;
- `Đang được sản xuất`;
- `Còn cần bổ sung`.

Show current values separately from creation snapshots. Never call open coverage stock. Deep links connect alert -> demand -> run where authorization permits.

### 23.3 Production

Show the originating demand reference and alert reference as secondary evidence. Production remains the execution page; it does not become a threshold editor.

### 23.4 Recipe Workspace A4

When a Store context is authorized, show only a lightweight operational signal: usable stock, low, target, net need and active production indicator with deep links. Do not add demand or Production mutation forms to Recipe Workspace.

## 24. Authorization and RBAC Prerequisite

### 24.1 Target responsibility

- StoreManager: view/update low + target in own Store; confirm/adjust demand; Plan, Release, Accept Output and Cancel.
- ShiftSupervisor: Start and Record Actual in own Store.
- BusinessOwner: global governance/view, current policy update authority where already granted, and Approve Variance.
- RegionManager: read-only in authorized region/scope.
- WarehouseAccountant: current BOM/inventory visibility; no Plan without ProductionOrder.Plan.
- SalesEmployee: no replenishment administration.
- SystemAdmin: no default business replenishment or Production authority.
- AccountPermissionOverride Allow/Deny and Store scope remain authoritative.

No new replenishment permission is introduced.

### 24.2 CODE_CONFIRMED contradiction classification

| Finding | Classification | Spec action |
|---|---|---|
| InventoryThreshold controller requires effective View/Update permission and Store scope | CURRENTLY_CORRECT | Preserve |
| InventoryThresholdService separately allows StoreManager, RegionManager, BusinessOwner and SystemAdmin by role | CURRENTLY_WRONG | Replace role allow-list with effective permission + Store scope at service boundary |
| Seed grants InventoryThreshold.Update to RegionManager | CURRENTLY_WRONG | Remove RegionManager update; keep read-only |
| Seed does not grant InventoryThreshold.Update to SystemAdmin | CURRENTLY_CORRECT | Preserve zero |
| Seed grants Production Plan/Release/Start/RecordActual/AcceptOutput/ApproveVariance/Cancel to SystemAdmin | CURRENTLY_WRONG | Remove all default SystemAdmin business Production grants |
| Seed grants Restock.SelectProductionSource to SystemAdmin although target code uses ProductionOrder.Plan | CURRENTLY_WRONG / LEGACY | Remove SystemAdmin grant; retain permission entity for compatibility |
| Source tests expect zero SystemAdmin Production bits while Seed contains ones | TEST_SEED_MISMATCH | Reconcile seed to approved test matrix before feature slices |
| Previous completion report claimed alignment | STALE_REPORT | CURRENT HEAD evidence overrides report |

This contradiction is resolved from current code and is not a SPEC_BLOCKER. Implementation starts with a small RBAC-hardening prerequisite. It must not broaden SystemAdmin, create a new permission, delete legacy permission codes or reset account overrides.

## 25. Concurrency and Idempotency

Preserve and verify:

- filtered unique active StockAlert per Store + PreparedItem;
- filtered unique active RestockRequest per Store + PreparedItem and per StockAlert;
- RowVersion on policy/inventory, alert, request, allocation and run mutations;
- Production `(StoreId, RequestKey)` uniqueness and fingerprint replay;
- unique ProductionRun link per sourcing allocation;
- state revalidation under SQL transaction;
- unique production inventory movement per run/inventory/type;
- unique fulfillment source tuple;
- acceptance Completed replay.

Required race outcomes:

- concurrent alert evaluations produce one active alert;
- concurrent demand creation produces/returns one active request;
- concurrent plan with the same key produces one run;
- concurrent plans with different keys cannot allocate beyond revalidated uncovered demand;
- cancel and replan serialize around run/request/allocation state;
- accept retry does not double-credit;
- cancel cannot win against an acceptance-state run and accept cannot operate a cancelled run.

RowVersion is stale-edit protection, not the sole duplicate-command mechanism.

## 26. Legacy Compatibility

- historical Recipe-linked StoreInventory rows remain readable through existing compatibility rules;
- target policy is editable only on canonical PreparedItem rows;
- existing MinStockLevel values remain unchanged;
- existing TargetStockProcurementQuantity remains request snapshot/compatibility data, never Store policy;
- historical RestockRequests, allocations, runs, Orders, RecipeIds, actuals and inventory movements are not rewritten;
- legacy Production v1 remains readable;
- current legacy Create/Confirm production routes are not deleted by this feature;
- old alerts without target snapshots remain readable and show unavailable target-derived quantities;
- no automatic repair occurs during read.

## 27. Performance Constraints

- Store-scoped set-based projection only.
- server-side paging for list screens.
- one aggregate for active alert/request/allocation/run evidence; no per-row query.
- bounded open run summaries with total/has-more metadata.
- no full StoreInventory graph.
- no full or recursive Recipe graph for replenishment reads.
- no in-memory counting of filtered totals.
- no inventory mutation to refresh read models.
- exact current Recipe resolution occurs only at production eligibility/Plan, not for every list row.
- indexes already supporting Store + PreparedItem active alert/request are reused; migration adds no speculative index for TargetStockLevel.

## 28. Traceability Contract

The implementation must preserve a navigable/evidence chain:

```text
StockAlertId
-> RestockRequestId
-> RestockSourcingAllocationId
-> ProductionRunId
-> pinned RecipeId
-> ProductionRun transitions and actors
-> actual consumed inputs
-> accepted output
-> InventoryTransaction
-> FIFO cost layer
-> RestockFulfillmentPosting
```

Policy and demand snapshots explain why quantity was requested. Existing transitions and posting entities remain audit authority; no duplicate JSON audit payload is added.

## 29. Testing Decisions and Acceptance Criteria

Tests assert externally visible domain behavior at the highest practical seam: application query/mutation services for policy and need, SQL-backed integration for uniqueness/transactions, existing production operation seams for pin/cancel/accept, and controller/view tests for permission/localization. Tests must not assert private helper structure.

Focused acceptance tests:

```text
PreparedItemThreshold_IsStoreSpecific
TargetStock_MustNotBeBelowLowThreshold
CanonicalPreparedItem_AppearsInThresholdProjection
ThresholdBoundary_UsesStrictLessThan
MissingTarget_DoesNotInventSuggestedQuantity
NetNeed_SubtractsOnlyNonTerminalProductionCoverage
CancelledRun_ReleasesProductionCoverage
ConcurrentAlertEvaluation_CreatesOneActiveAlert
ConcurrentDemandCreation_CreatesOneActiveRequest
ProductionPlan_RequestKey_IsIdempotent
RecipeChangeBeforePlan_UsesNewCurrentRecipe
RecipeChangeAfterPlan_DoesNotSwitchPinnedRecipe
AcceptedOutput_CreditsFullPhysicalQuantity
AcceptedOutput_FulfillmentIsCappedToRemainingDemand
AcceptedOutput_ReevaluatesPreparedItemAlert
Underproduction_LeavesRemainingDemand
Overproduction_DoesNotTruncateInventory
UnauthorizedStore_CannotReadOrMutateReplenishment
PreparedItemWithoutPurchaseContract_DoesNotCreatePO
SystemAdmin_HasNoDefaultBusinessReplenishmentAuthority
```

Additional required coverage:

- exactly-at-low UI and service agree;
- target update RowVersion conflict is business-readable;
- existing active request returns current request rather than duplicate;
- continued consumption calculates only explicit additional demand;
- active non-production sourcing fails closed for production suggestion;
- cancelled allocation is excluded even during stale-read recovery;
- post-commit reevaluation failure does not roll back accepted inventory;
- RegionManager cannot mutate policy;
- account Deny overrides role Allow;
- no raw internal status/reason code appears in changed UI.

SQL-backed verification is required for active uniqueness, concurrent plan, cancel/replan and acceptance replay. Follow repository focused test-scope rules; no full suite by habit.

## 30. Runtime Acceptance Contract

Authenticated runtime verification must cover:

1. PreparedItem with low + target configured.
2. PreparedItem below low.
3. PreparedItem exactly equal to low: not low.
4. PreparedItem above low.
5. target missing: alert visible, need unavailable.
6. zero open production coverage.
7. partial open coverage.
8. open coverage fully covers gross need.
9. cancelled production returns coverage for replan.
10. Recipe changes before Plan and new current Recipe is pinned.
11. Recipe changes after Plan and pinned Recipe remains.
12. underproduction leaves demand.
13. overproduction credits full inventory and caps fulfillment.
14. accepted output resolves/updates alert after commit.
15. POS continues consuming while production is open; run does not mutate.
16. StoreManager operates own Store.
17. StoreManager other Store is denied at backend.
18. ShiftSupervisor executes only permitted actions.
19. BusinessOwner approves variance.
20. SystemAdmin has no default business operation.

Verify base/display UOM conversion, Vietnamese business language, direct denied HTTP requests, empty/unavailable states and bounded list behavior. Owner final smoke remains required after agent verification.

## 31. Risks and Mitigations

| Risk | Mitigation |
|---|---|
| Legacy BTP row becomes policy authority | Edit canonical PreparedItem row only; fail closed on ambiguous identity |
| Target silently defaults to low | Nullable target and explicit unavailable state; no backfill |
| Rounded batch output overstates coverage | Use normalized allocation quantity, not expected output |
| Cancelled run blocks replan | Transactionally release allocation and recompute sourcing |
| Continued POS use duplicates demand | Reuse active request and calculate explicit additional adjustment |
| Reevaluate failure appears as inventory failure | Commit inventory first; catch/log derived refresh failure |
| SystemAdmin bypasses business RBAC | Reconcile seed/service prerequisite; effective permission at service boundary |
| RegionManager mutates Store policy | Remove Update grant; retain View only |
| PreparedItem enters PO path | Fail closed without CanPurchase plus supplier contract |
| Query creates N+1 | Dedicated set-based Store projection and bounded summaries |

## 32. Implementation Order

This is specification ordering only; tickets are not created here.

1. RBAC baseline prerequisite.
2. additive TargetStockLevel migration and policy validation.
3. canonical PreparedItem threshold/replenishment query authority.
4. threshold UI and policy mutation.
5. alert-to-demand revalidation and snapshot mapping.
6. production source/Plan revalidation and continued-consumption adjustment.
7. cancellation allocation release.
8. post-accept reevaluation.
9. StockAlert/Restock/Production/A4 lightweight UX integration.
10. focused SQL, authorization and authenticated runtime hardening.

## 33. Definition of Done

- TargetStockLevel exists as nullable additive StoreInventory policy with no backfill.
- low and target remain distinct and use PreparedItem base UOM.
- missing target never produces zero/fake suggestion.
- canonical PreparedItem is correctly projected and editable by authorized StoreManager.
- service and UI both use strict `<` low boundary.
- one bounded authority calculates usable, gross need, production coverage and net need.
- RestockRequest remains the single demand authority.
- demand creation/adjustment revalidates current server evidence.
- only proven non-terminal PRODUCTION allocations count as open coverage.
- PreparedItem purchase remains fail closed without supplier contract.
- Recipe is resolved and pinned at Plan only and never silently switched.
- cancelled run releases allocation and permits safe replan.
- full accepted output credits inventory; fulfillment remains capped.
- post-commit alert reevaluation cannot roll back inventory truth.
- Store scope, role separation and account overrides are verified.
- SystemAdmin default business grants and RegionManager threshold update mismatch are corrected.
- focused automated, SQL-backed and authenticated runtime acceptance pass.
- no ProductionDemand, MRP, RecipeIdentity, scheduling or inventory rewrite is introduced.
- migration, implementation and ticket commits follow repository exact-staging rules in their future tasks.

## 34. Open Questions

No product-policy question remains unresolved for `/to-tickets`. Implementation may choose the existing allocation terminal label (`Released` versus `Cancelled`) that best matches current constants, provided it is non-active, auditable and excluded from coverage. This is a technical naming choice, not an Owner decision.

## 35. Spec Self-review

```text
No ProductionDemand entity introduced: PASS
No second production workflow introduced: PASS
Low and Target distinct: PASS
Missing Target fails closed: PASS
Open coverage is not inventory: PASS
Cancelled runs do not cover demand: PASS
Accepted output remains full physical truth: PASS
Fulfillment remains capped: PASS
Recipe pins only at Plan: PASS
Pinned Recipe never switches silently: PASS
Store scope explicit: PASS
SystemAdmin not made business superuser: PASS
PreparedItem purchase fails closed without contract: PASS
No target backfill from low: PASS
Migration additive only: PASS
No MRP expansion: PASS
RBAC contradiction resolved from CURRENT HEAD: PASS
```

## Further Notes

- The specification artifact is the authority for future `/to-tickets`; discovery documents remain supporting evidence.
- No issue decomposition, production implementation, migration creation, seed update, test execution or runtime mutation belongs to this `/to-spec` task.
- Epic/Owner runtime acceptance remains a later implementation-stage responsibility.
