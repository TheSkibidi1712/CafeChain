# ADR-0007: Preprocessing Orders and Atomic Inventory Movements

| Field | Value |
|-------|--------|
| **Status** | Accepted |
| **Date** | 2026-07-11 |
| **Accepted Date** | 2026-07-11 |
| **Issue** | [#106](https://github.com/TheSkibidi1712/CafeChain/issues/106) |
| **Branch context** | `feature/POS` |
| **Related** | ADR-0001, ADR-0004, **ADR-0005 Accepted**, **ADR-0006 Accepted** (`6e45cf2`), unit conversion `4a8cfa2` |

---

## 1. Title

Lệnh sơ chế (ProductionOrder): DRAFT → IN_PROGRESS → COMPLETED lifecycle, planned/actual input–output, variance/waste, atomic inventory movements, concurrency/idempotency, store-scoped authorization, and PreparedItem cost layers.

## 2. Status

**Accepted** — domain decisions locked for Phase 2 implementation.  
This ADR document does not include schema/code; implementation and migration follow later issues/PRs.  
Locked: **final MVP statuses (no persistent FAILED)**, **Start freezes snapshot**, **authorization matrix (full role names)**, **ActualInput/Output/waste rules**, **defense-in-depth Complete concurrency**, **CostStatus COMPLETE/INCOMPLETE**, WorkShift optional, cancel/reversal rules.

## 3. Date

2026-07-11 (draft + review); **Accepted Date: 2026-07-11**

## 4. Context

POS stock is **one-level** (ADR-0004/0006): sale deducts Ingredient or **PreparedItem**, not raw materials inside BTP. Preprocessing / production therefore:

1. Consumes ingredients and/or other PreparedItems (one level).  
2. Produces PreparedItem stock in **PreparedItem.BaseUnitId**.

Today that path is `AdminProductionOrderController` without a durable order aggregate (hardcoded store, batch-count output, no conversion, history from loose `PRODUCTION_IN` rows).

This ADR defines ProductionOrder **without redefining** package cost (ADR-0005) or PreparedItem/Recipe version identity (ADR-0006).

---

## 5. Current-state findings (code-verified)

### 5.1 Surface

| Item | Finding |
|------|---------|
| Controller | `AdminProductionOrderController` |
| EF entity ProductionOrder | **Missing** |
| Preview | `Quantity × batches`; **yield forced 100%**; no unit conversion; `estimatedOutput = batches` |
| Execute | **`storeId = 1` hardcoded**; input `qty × batches` no convert; BTP stock via **`RecipeId`**; output **`= Batches`** |
| DB transaction | Wraps execute; shortage → **Rollback** |
| Ledger | `PRODUCTION_OUT` / `PRODUCTION_IN`; **no** ProductionOrder reference |
| Idempotency / staff / shift / cost layer | **None** |
| Negative production stock | Explicitly blocked (insufficient check) |

### 5.2 Inherited baseline

PreparedItem stock identity; Recipe version formula; OutputQuantity = planned net output; POS sale one-level unrelated; physical conversion Unit domain; YieldPercentage not actual production SoT; cost via ADR-0005.

---

## 6. Problems

Hardcoded store; no durable order; batch output; no conversion; RecipeId as BTP stock; no Start/snapshot; no idempotency; no role matrix; no planned/actual/waste model; no cost completeness for production output.

---

## 7. Terminology

| Term | Meaning |
|------|---------|
| **ProductionOrder** | Lệnh sơ chế aggregate |
| **Runs** | Multiplier of one frozen recipe definition |
| **ExpectedOutputQuantityBase** | Normalized planned net output in PreparedItem.BaseUnitId |
| **ActualOutputQuantityBase** | Confirmed produced qty in base unit |
| **OutputVarianceQuantityBase** | Actual − Expected (display variance; **not** automatic waste) |
| **AchievementPercent** | Actual/Expected × 100 |
| **WasteQuantityBase** | **Explicit** discarded qty on an input line |
| **CostStatus** | COMPLETE \| INCOMPLETE |
| **PRODUCTION_OUT / PRODUCTION_IN** | Ledger movement types (aliases: consumption / output) |

Vietnamese UI:

- Sản lượng dự kiến  
- Sản lượng thực tế  
- Chênh lệch sản lượng  
- Tỷ lệ đạt sản lượng  
- Hao hụt ghi nhận  

Do not use English “yield” in UI without showing the formula.

---

## 8. Decision

### 8.1 Aggregate

**ProductionOrder** is the system of record.  
MVP: **one PreparedItem output** per order. Multi-output/by-product = future.

### 8.2 Final MVP lifecycle (locked)

```text
DRAFT
IN_PROGRESS
COMPLETED
CANCELLED
```

**Do not** use **FAILED** as a persistent ProductionOrder status in MVP.

| Transition | Meaning |
|------------|---------|
| DRAFT → IN_PROGRESS | **Start**: freeze recipe snapshot; no stock mutation |
| IN_PROGRESS → COMPLETED | **Complete**: atomic stock + ledger |
| DRAFT → CANCELLED | Cancel before start |
| IN_PROGRESS → CANCELLED | Cancel after start, before complete; no stock mutation |

**COMPLETED** is terminal and **immutable**.

**When Complete fails:**

- Rollback **all** inventory/order-completion writes.  
- ProductionOrder **remains IN_PROGRESS**.  
- Return a clear error.  
- Allow **safe retry** after the cause is corrected.

**Future optional:** `ProductionOrderExecutionAttempt` for durable failure history (not MVP required).

**PLANNED** is **not** required in MVP (Start = freeze + IN_PROGRESS).

---

## 9. Aggregate / data model (locked fields)

### ProductionOrder

| Field | Notes |
|-------|--------|
| ProductionOrderId | PK |
| StoreId | Auth scope; never hard-coded |
| PreparedItemId | Output SKU |
| RecipeId | Frozen version |
| RecipeVersion | Display/monotonic |
| RecipeSnapshotJson | Component freeze |
| RecipeSnapshotSchemaVersion | Schema version |
| Status | DRAFT \| IN_PROGRESS \| COMPLETED \| CANCELLED |
| Runs | > 0 |
| ExpectedOutputQuantityBase | Normalized |
| ActualOutputQuantityBase | On complete; > 0 |
| OutputVarianceQuantityBase | Computed |
| AchievementPercent | Computed |
| CostStatus | COMPLETE \| INCOMPLETE |
| ActualProductionCost? | When cost complete |
| OutputUnitCost? | When cost complete |
| CreatedByStaffId | |
| StartedByStaffId? | On Start |
| CompletedByStaffId? | On Complete |
| CancelledByStaffId? | On Cancel |
| WorkShiftId? | Optional if active shift |
| CreatedAt / StartedAt? / CompletedAt? / CancelledAt? | |
| CancellationReason? | Required on cancel |
| Notes? | |
| RowVersion | Concurrency token |

### ProductionOrderInput

| Field | Notes |
|-------|--------|
| ProductionOrderInputId | PK |
| ProductionOrderId | FK |
| ComponentType | Ingredient \| PreparedItem |
| IngredientId? / PreparedItemId? | XOR |
| SourceRecipeDetailId | From snapshot |
| PlannedQuantityBase | Frozen at Start |
| ActualQuantityBase | Default = planned |
| BaseUnitId | Snapshot |
| AdjustmentReason? | **Required** if Actual ≠ Planned |
| WasteQuantityBase? | Explicit discard; ≥ 0 |
| WasteReason? | With waste |
| InputUnitCostSnapshot? / InputTotalCost? | |
| InventoryTransactionId? | After complete |

**MVP:** single output on ProductionOrder header (PreparedItemId + planned/actual output fields). No multi-output table in MVP.

---

## 10. Status lifecycle (detail)

See §8.2. Summary:

- DRAFT: edit Recipe/runs/notes; **no** stock mutation; **no** reservation.  
- IN_PROGRESS: snapshot locked; **no** stock mutation until Complete; **no** reservation in MVP.  
- COMPLETED: inputs deducted, output added, ledger written; immutable.  
- CANCELLED: only from DRAFT or IN_PROGRESS; reason required; no stock mutation.

---

## 11. Start / snapshot semantics (locked)

### DRAFT

- Recipe, runs, notes may be edited.  
- No stock mutation.  
- No reservation.

### Start (DRAFT → IN_PROGRESS)

1. Validate Recipe/PreparedItem (Active production version for PreparedItem via shared resolver ADR-0006).  
2. Lock `RecipeId` and `RecipeVersion`.  
3. Snapshot all components into `RecipeSnapshotJson`.  
4. Normalize planned inputs → component base units.  
5. Normalize expected output → `PreparedItem.BaseUnitId` × Runs.  
6. Record `StartedByStaffId`, `StartedAt`.  
7. **Do not** deduct or reserve stock.

### Complete

- Uses **frozen snapshot only** — never re-reads current Active Recipe.  
- **Revalidates current stock** at Complete because MVP has **no** reservation.

---

## 12. Planned vs actual (locked)

### ActualInput

- Defaults to PlannedInput.  
- **StoreManager** / **ShiftSupervisor** may adjust before Complete.  
- Rules:  
  - quantity **> 0**  
  - component identity **cannot** change  
  - unit cannot change outside approved base-unit conversion  
  - **cannot add/remove components** in MVP  
  - if Actual ≠ Planned → **`AdjustmentReason` required**  
  - backend recalculates stock preview, cost, validation  
  - edits **do not** mutate the frozen Recipe snapshot  

If a new component is needed: create a **new Recipe version** or future adjustment workflow — do not silently change the production formula.

### ActualOutput

- **Required**, **> 0** before Complete.  
- Not inferred from YieldPercentage.

---

## 13. Variance / waste formulas (locked)

```
ExpectedOutputQuantityBase
  = PhysicalConvert(Recipe.OutputQuantity, OutputUnitId → PreparedItem.BaseUnitId)
    × Runs

OutputVarianceQuantityBase
  = ActualOutputQuantityBase - ExpectedOutputQuantityBase

AchievementPercent
  = ActualOutputQuantityBase / ExpectedOutputQuantityBase × 100
```

**Do not** automatically treat negative OutputVariance as Waste.

**Waste** = explicit physical discard on an **input** line:

- `WasteQuantityBase` ≥ 0  
- `WasteQuantityBase` ≤ `ActualQuantityBase` (ActualInput) on that line (MVP; no exceed without future policy)  
- `WasteReason` required when waste is positive  
- **`WasteQuantityBase` is an auditable subset of `ActualQuantityBase` (ActualInput).**  
  It **must not** create an **additional** inventory deduction beyond `ActualQuantityBase`.  
  Stock is decremented by **ActualInput only**; waste is accounting/audit metadata of how much of that consumed input was discarded or not converted to usable product, not a second `PRODUCTION_OUT`.

UI terms: §7 (Vietnamese list). Do not use bare “yield” without formula.

No second application of `Recipe.YieldPercentage` when OutputQuantity is already net (ADR-0006).

---

## 14. Atomic Complete transaction (locked)

One DB transaction:

1. Verify status is **IN_PROGRESS**.  
2. Acquire concurrency protection (`RowVersion`).  
3. Validate frozen snapshot.  
4. Reload and validate `StoreInventory` rows.  
5. Validate no negative input stock (Available ≥ ActualInput).  
6. Deduct actual Ingredient/PreparedItem inputs.  
7. Write one **PRODUCTION_OUT** ledger movement per input.  
8. Add `ActualOutputQuantityBase` to PreparedItem inventory.  
9. Write one **PRODUCTION_IN** output ledger movement.  
10. Create output cost layer **only if** CostStatus will be COMPLETE.  
11. Mark ProductionOrder **COMPLETED**.  
12. Set `CompletedByStaffId`, `CompletedAt`.  
13. Commit.

**Any failure:**

- Rollback inventory changes, ledger rows, status completion.  
- Leave order **IN_PROGRESS**.  
- No partial writes.

---

## 15. Inventory ledger

Each movement:

- Type: PRODUCTION_OUT (input) / PRODUCTION_IN (output)  
- Source = ProductionOrder; Reference = ProductionOrderId  
- Before/after balances  
- Staff/time; WorkShift if set  
- Link `InventoryTransactionId` on input/output rows  

---

## 16. Concurrency and idempotency (locked — defense in depth)

### A. ProductionOrder concurrency token

- `RowVersion` (or equivalent optimistic concurrency).

### B. Status guard

- Complete **only** from **IN_PROGRESS**.  
- Conditional/concurrency-safe update.  
- Only one concurrent Complete may proceed.

### C. Unique inventory movement keys

**Input uniqueness:**

```text
(ProductionOrderId, ProductionOrderInputId, MovementType = PRODUCTION_OUT)
```

**Output uniqueness:**

```text
(ProductionOrderId, PreparedItemId, MovementType = PRODUCTION_IN)
```

| Scenario | Result |
|----------|--------|
| Second Complete after success | **AlreadyCompleted**; no inventory mutation; no new ledger |
| Concurrent Completes | One succeeds; others conflict/already completed; never double-deduct/add |
| Retry after rolled-back failure | Succeeds **exactly once** after fix |

Do **not** rely on frontend button disable.

---

## 17. Stock / negative policy (locked)

Production **does not** inherit ADR-0001 blind-selling negative stock.

- Negative stock **not allowed**.  
- Missing/insufficient Ingredient or PreparedItem **blocks Complete**.  
- Validate before first mutation; revalidate inside transaction.  
- MVP **no reservation** in DRAFT/IN_PROGRESS.  
- Future reservation = separate issue.  
- Temporary negative override **#102 does not apply**.

---

## 18. Authorization / store scope (locked MVP)

**Do not use ambiguous abbreviations in product docs.** Full role names:

| Role | View | Create/edit DRAFT | Start | Complete | Cancel |
|------|------|-------------------|-------|----------|--------|
| **StoreManager** | Yes | Yes | Yes | Yes | Yes |
| **ShiftSupervisor** | Yes | Yes | Yes | Yes | Yes (same-store only) |
| **SalesStaff** | Only if existing POS/Admin policy explicitly allows; otherwise **no access** | No | No | No | No |
| **AccountantWarehouse** | Read-only branch production orders | No | No | No | No |
| **AreaManager / BusinessOwner / SystemAdmin** | Read/audit by accessible store scope | No direct Complete in MVP unless also acting via assigned store-level staff role | — | — | — |

- Backend enforces **role + accessible StoreId**.  
- **No hardcoded StoreId.**  
- No central-kitchen policy in this ADR.

---

## 19. WorkShift (locked)

- `WorkShiftId` **optional**.  
- If operator has an **active** WorkShift for the store: capture it.  
- If none: do **not** create a fake shift; do **not** block StoreManager/ShiftSupervisor unless later policy requires open shift.

---

## 20. Cost status and cost layer (locked)

### CostStatus

```text
COMPLETE
INCOMPLETE
```

Input costing uses **StoreOperationalCost** (ADR-0005).

If all required input costs are valid:

```
ActualProductionCost = Σ (ActualInputBase × InputCostPerBase)
OutputUnitCost       = ActualProductionCost / ActualOutputBase
```

Create **PreparedItem cost layer**:

- quantity = ActualOutputBase  
- unit cost = OutputUnitCost  
- reference = ProductionOrderId  

If any required cost is missing:

- Quantity movements may still **Complete**.  
- **CostStatus = INCOMPLETE**.  
- **Do not** create a zero-cost or fake-complete cost layer.  
- UI: **“Chưa đủ dữ liệu giá vốn”**.  
- Log/audit incomplete cost.  
- Future reconciliation via separate controlled workflow.

**Missing cost must not be silently converted to zero.**

If cost-layer schema is Ingredient-only today → Phase 2 extends to PreparedItem; do not silently drop the requirement.

---

## 21. Cancellation and post-complete correction (locked)

| State | Cancel |
|-------|--------|
| DRAFT or IN_PROGRESS | Allowed; **CancellationReason required**; **no** stock mutation |
| COMPLETED | **Cannot** cancel, edit, or delete |

Correction after completion:

- Future **reversal/adjustment** document  
- Append inverse ledger movements  
- **Never** edit/delete original transactions  

Reversal UI out of scope for first implementation; rule still stands.

---

## 22. UI implications

Header: order code, store, PreparedItem, recipe version, status, actors, times.  
Planning: runs, expected output + unit, notes.  
Inputs: type, planned/actual, base unit, available stock, sufficiency, cost status, waste if any.  
Output: expected, actual, chênh lệch sản lượng, tỷ lệ đạt sản lượng.  
Actions: Save draft, Start, Complete, Cancel.  

Block Complete when: not IN_PROGRESS; actual output ≤ 0; insufficient input; missing conversion; invalid snapshot; already COMPLETED/CANCELLED.

Confirmation modal: before/after for inputs and output balances.

---

## 23. Migration / data remediation

Later (not now):

1. ProductionOrder + ProductionOrderInput tables + RowVersion.  
2. Unique Complete/idempotency and ledger uniqueness indexes.  
3. ProductionOrderId on InventoryTransaction (or equivalent reference).  
4. PreparedItem stock keys (ADR-0006 cutover).  
5. PreparedItem cost layer support.  
6. Do not invent ProductionOrder history solely from bare PRODUCTION_IN without mapping.

---

## 24. Consequences

### Positive

Multi-store safety; correct base-unit output; frozen recipe; atomic no partial stock; clear auth; incomplete cost honesty; concurrent Complete safe.

### Negative

Larger than current controller; cost-layer schema extension; ops training (actual output entry).

---

## 25. Rejected alternatives

| Alternative | Why |
|-------------|-----|
| Persistent FAILED status in MVP | Order stays IN_PROGRESS on complete failure |
| PLANNED required in MVP | Start freezes snapshot |
| StoreId = 1 | Multi-store broken |
| Batch-count inventory | Violates ADR-0006 base unit |
| Negative production stock | Not ADR-0001 |
| UI-only double-submit guard | Not durable |
| Auto waste = −variance | Waste is explicit discard |
| Zero-cost layer on missing cost | Fake complete cost |
| SalesStaff Complete | Role matrix |
| Permanent dual-write without concurrency keys | Double mutate risk |

---

## 26. Test requirements (implementation)

1. DRAFT Start freezes Recipe snapshot.  
2. Active Recipe changes after Start do not affect the order.  
3. Failed Complete leaves status **IN_PROGRESS**.  
4. Concurrent Completes mutate once.  
5. RowVersion conflict handled.  
6. Unique ledger keys block duplicate input/output.  
7. ActualInput variance requires reason.  
8. Cannot add/remove component during execution.  
9. Output variance is not automatically waste.  
10. Waste is explicit and validated.  
11. StoreManager/ShiftSupervisor may Complete same-store order.  
12. SalesStaff and AccountantWarehouse cannot Complete.  
13. AreaManager/Owner/SystemAdmin are read/audit-only in MVP.  
14. Missing cost completes quantity with CostStatus INCOMPLETE.  
15. No zero-cost layer created.  
16. Complete cost creates PreparedItem cost layer.  
17. Cancel IN_PROGRESS causes no stock mutation.  
18. Completed order is immutable.  
19. Retry after rollback completes once.  
20. Second Complete returns AlreadyCompleted.  
21. StoreId not hard-coded.  
22. Insufficient stock fails before mutation.  
23. Missing conversion fails before mutation.  
24. POS one-level sale regression unchanged.  
25. YieldPercentage not double-applied to expected output.  
26. Cross-store blocked.

---

## 27. Dependencies / follow-ups

| Item | Role |
|------|------|
| ADR-0005 | StoreOperationalCost / package → base cost |
| ADR-0006 | PreparedItem, recipe version, output units, physical conversion |
| **#106** | This ADR |
| #108 | Branch receipt (separate from production) |
| #109 | Sale-side durable deduction failure (not production FAILED status) |
| Phase 2 | Implement ProductionOrder + UI + migrations |

---

## 28. Remaining questions

**Implementation-level only (do not reopen domain locks):**

1. Exact dual-column cutover timing with ADR-0006 PreparedItem migration.  
2. Whether first UI ships Start as explicit button or combined “Start & Complete” with same status rules.  
3. Physical conversion table name (ADR-0006).  
4. ExecutionAttempt table timing (future failure history).

**Not open:** lifecycle statuses; Start snapshot; auth matrix; ActualInput/Output/waste; atomic Complete; concurrency/idempotency; CostStatus; WorkShift optional; cancel rules.

---

## Examples (normative)

### 1 — Cold Brew
PreparedItem base ml; Recipe 500 g coffee + 5000 ml water → Output 4500 ml.  
Runs = 2 → expected 9000 ml. Actual output 8700 ml → atomic −inputs +8700 ml; COMPLETED.

### 2 — BTP consumes BTP
Tea base PreparedItem + milk Ingredient → milk tea PreparedItem; one-level; no explode.

### 3 — Insufficient stock
Need 1000 g, have 800 → Complete fails; remains **IN_PROGRESS**; no ledger/output.

### 4 — Retry / second Complete
Conversion missing → fail, stay IN_PROGRESS. Fix → Complete once. Second call → AlreadyCompleted, no mutate.

---

## Decision summary (locked)

1. Lifecycle: **DRAFT → IN_PROGRESS → COMPLETED**; cancel from DRAFT/IN_PROGRESS; **no FAILED status**.  
2. Start freezes recipe snapshot; Complete never re-reads Active Recipe.  
3. Complete revalidates stock (no reservation MVP).  
4. Auth: StoreManager + ShiftSupervisor full ops same-store; AW read-only; SalesStaff no Complete.  
5. ActualInput adjustable with reason; no component add/remove.  
6. ActualOutput > 0 required; variance ≠ waste; waste explicit on inputs.  
7. One DB transaction for Complete; failure → stay IN_PROGRESS.  
8. RowVersion + status guard + unique ledger keys.  
9. No negative production stock; #102 N/A.  
10. CostStatus COMPLETE/INCOMPLETE; no fake zero cost layer.  
11. WorkShift optional.  
12. COMPLETED immutable; reversal via future adjustment only.  
13. Aligns with ADR-0005/0006.
