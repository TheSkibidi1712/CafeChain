# ADR-0009: Durable POS Inventory Deduction Failure and Retry

| Field | Value |
|-------|--------|
| **Status** | Accepted |
| **Date** | 2026-07-11 |
| **Accepted Date** | 2026-07-11 |
| **Issue** | [#109](https://github.com/TheSkibidi1712/CafeChain/issues/109) |
| **Branch context** | `feature/POS` |
| **Related** | ADR-0001, ADR-0002, ADR-0004, **ADR-0005/0006/0007/0008 Accepted**, #65, #66, #86, unit conversion `4a8cfa2` |

---

## 1. Title

Durable **InventoryDeductionJob** intent created in the same transaction as Order + Payment + sale-time Recipe/BOM snapshot; worker lease + bounded retry; REQUIRES_REVIEW for deterministic data failures; stable **SnapshotComponentKey** movement uniqueness; SUCCEEDED only when the full expected component movement set commits with stock and ledger in one transaction; shared **InventoryDeductionOrchestrator** for immediate attempt and worker; reconciliation safety net; StoreManager manual retry (status only); payment status never flipped by inventory.

## 2. Status

**Accepted** — domain decisions locked for Phase 1 implementation.  
This ADR document does **not** include schema migration, business code, or Phase 1 issues. Implementation follows later issues/PRs.  
Locked: same-tx PENDING job + snapshot + SnapshotComponentKey; atomic stock/ledger/SUCCEEDED; lease/retry/REQUIRES_REVIEW lifecycle (no CANCELLED MVP); full movement-set completion invariant; reconciliation; StoreManager manual retry to PENDING only; StockAlert non-blocking post-success.

Does **not** reopen accepted decisions:

- POS deduction remains **one-level** (ADR-0004 / ADR-0006).
- **PreparedItem** is stable BTP stock identity (ADR-0006).
- Order stores **sale-time BOM snapshot** (ADR-0006).
- Missing unit conversion is **fail-closed** (`4a8cfa2` / ADR-0005/0006).
- Paid/Completed order is **not** rolled back because a later stock side effect fails (ADR-0001 / #65).
- Temporary negative override remains **deferred** (#102).

## 3. Date

2026-07-11 (draft + review); **Accepted Date: 2026-07-11**

---

## 4. Context

CafeChain POS commits **Order + Payment** first, then attempts inventory deduction as a **post-commit side effect**. Failures (missing recipe, missing/invalid conversion, DB timeout, process crash) leave the order **Paid/Completed** while stock may never move.

Today:

- Failures are logged and/or returned as `inventoryWarnings`.
- Method-level retry is possible only if a later request re-invokes deduction (duplicate webhook repair, offline re-sync, manual re-call).
- There is **no durable outbox/job**, **no lease**, **no admin failure dashboard**, and **no guaranteed recovery** after a crash between order commit and deduction.
- Idempotency uses “any `InventoryTransaction` with `ReferenceOrderId` + `SALES_DEDUCTION`” — too coarse for partial/ambiguous legacy evidence.

Issue #109 requires locking a durable architecture so that:

1. Every committed POS order that requires stock deduction has a durable deduction intent.
2. Process crash after order commit cannot permanently lose deduction.
3. Retry never double-deducts.
4. Cash, VietQR/PayOS webhook, and offline sync share one deduction pipeline.
5. Failures are classified and visible.
6. Transient failures retry automatically.
7. Data/business failures require operational review.
8. StoreManager can safely retry after the cause is fixed.
9. Historical sale-time Recipe/BOM snapshot is used.
10. Payment/order success is not falsely changed because inventory retry is pending.
11. Completed deduction is immutable.
12. Correction after successful deduction uses reversal/adjustment, not re-running deduction.

---

## 5. Current-state findings (code-verified)

### 5.1 Paths that call deduction

| Path | Entry | When |
|------|--------|------|
| **Cash online commit** | `POSOrderController.CommitOrder` → `DeductInventorySafeAsync` | After successful `CommitOrderAsync` when order is **not** `requiresPayment` (paid cash path) |
| **VietQR / PayOS webhook** | `PayOSWebhookProcessor` → `DeductInventoryForPaidPosOrderSafeAsync` | After atomic confirm transition Unpaid/AwaitingPayment → Paid/Completed for `Source == POS` |
| **PayOS already-paid repair** | `RepairInventoryForAlreadyPaidPosOrderSafeAsync` | When webhook returns `ALREADY_PAID` — opportunistic re-call of same safe deduct |
| **Offline sync** | `POSOrderController.SyncOfflineOrders` → `DeductInventorySafeAsync` | After `CommitOfflineSyncedOrderAsync` success for both `created` and `duplicate` |
| **Mock / test** | `MockPOSController` → `DeductStockForOrderAsync` | Dev/mock path without committed-order guard (not production POS pipeline) |

No other production POS payment path was found that performs sales stock deduction.

### 5.2 Transaction boundaries (as-is)

| Step | Boundary |
|------|----------|
| Order + Payment commit (cash / offline) | Own transaction in `POSOrderService` / repository |
| PayOS payment confirm | Own transaction in `ConfirmPaymentTransactionAsync` |
| Inventory deduction | **Separate** transaction inside `InventoryDeductionService` |
| StockAlert evaluation | **After** successful deduction commit; best-effort |
| Print dispatch | Separate side effect |

Deduction is after commit, synchronous `await`, best-effort (order success unchanged). Not durable.

### 5.3 InventoryDeductionService behavior (as-is)

| Rule | Behavior |
|------|----------|
| Eligibility | Completed + Paid when `referenceOrderId` set |
| Idempotency | **Any** `InventoryTransaction` with `ReferenceOrderId` + `SALES_DEDUCTION` → treat as already deducted |
| Recipe source | Live **Active** recipe |
| Missing recipe | Soft-skip + log (under-deduct) |
| Conversion | Fail-closed → rollback deduction tx |
| One-level | Ingredient / ChildRecipe (BTP); no explode |
| Negative stock | Allowed (ADR-0001) |
| Movement uniqueness | **None** beyond optional coarse ReferenceOrderId check |

### 5.4 Entities / workers (as-is)

- No `RecipeSnapshot` on `OrderDetail` yet (ADR-0006 planned).
- No `InventoryDeductionJob`.
- `InventoryTransaction.ReferenceOrderId` indexed, not unique completion proof.
- Workers: `OrderCleanupWorker`, `PaymentCleanupWorker` only.

### 5.5 Gap summary

Crash after commit loses deduction. Soft-skip under-deducts. “Any ReferenceOrderId movement” can hide partial/ambiguous state. No admin/worker durable retry path.

---

## 6. Failure scenarios (current gaps → target)

| Scenario | Current | Target |
|----------|---------|--------|
| Crash after commit, before deduct | Lost | Job PENDING in order tx; worker recovers |
| DB timeout during deduction | Rollback + log | RETRY_SCHEDULED after separate failure-state tx |
| Missing recipe / conversion | Soft-skip or rollback + log | REQUIRES_REVIEW (post-pay); no raw qty |
| Duplicate webhook / offline | Opportunistic re-call | No second job; shared orchestrator; one effect |
| Partial legacy movements | Coarse “already deducted” risk | REQUIRES_REVIEW unless full expected key set matches |
| Success then retry | Any-ReferenceOrderId no-op | SUCCEEDED terminal + full movement set invariant |

---

## 7. Terminology

| Term | Meaning |
|------|---------|
| **InventoryDeductionJob** | Durable intent that a committed paid POS order requires sales stock deduction; **source of truth** for processing state |
| **InventoryDeductionOrchestrator** | Shared claim + attempt + idempotency path used by immediate request attempt and background worker |
| **Deduction intent** | Job row created in the same DB transaction as Order + Payment + snapshot for an eligible sale |
| **Sale-time snapshot** | Immutable BOM/component payload stored on the order at commit, including stable **SnapshotComponentKey** per top-level component |
| **SnapshotComponentKey** | Stable identity assigned and persisted at order commit for each top-level snapshot component; not a mutable runtime array index |
| **One-level deduction** | Ingredient → Ingredient inventory; PreparedItem → PreparedItem inventory |
| **Exactly-once effect** | At most one complete successful stock effect per order via durable intent + full expected movement set |
| **REQUIRES_REVIEW** | Deterministic data/business (or ambiguous legacy) failure; auto-retry stopped |
| **Reversal/adjustment** | Post-SUCCEEDED correction via new movements; never re-run original deduction |

---

## 8. Decision

Adopt a **durable InventoryDeductionJob** architecture:

1. **Same-transaction producer:** For every eligible new sale, one DB transaction commits Order state, Payment state, OrderDetail Recipe/BOM snapshot (with **SnapshotComponentKey**), and one `InventoryDeductionJob` **PENDING**.
2. **Separate operational boundary for stock:** Stock mutation remains outside payment success semantics; paid order stays paid while job is pending/failed.
3. **Atomic success attempt:** Stock mutations + ledger rows + job `SUCCEEDED` commit in **one** transaction under a valid lease.
4. **Shared orchestrator:** Immediate request attempt and background worker claim the same job with the same lease, transaction, and idempotency rules. No independent best-effort writer after cutover.
5. **Sale-time snapshot only** for historical BOM (ADR-0006).
6. **Completion proof:** job `SUCCEEDED` **and** complete expected set of unique component movements — **not** “any ReferenceOrderId row exists”.
7. **Failure classification** into transient vs review-required; **no CANCELLED** in graduation MVP; no generic FAILED.
8. **Reconciliation** for missing jobs, stuck leases, legacy/partial movements — never invent historical BOM.
9. **Manual retry** transitions REQUIRES_REVIEW → PENDING only; does not mutate stock.
10. **StockAlert / notifications** are post-success non-blocking side effects.
11. **Printing** remains out of scope.

---

## 9. Durable intent creation (order commit transaction)

### 9.1 Principle (locked)

For every **eligible new sale**, the **same database transaction** commits:

1. **Order** state (Completed as required by path)
2. **Payment** state (Paid as required by path)
3. **OrderDetail Recipe/BOM snapshot** (including stable **SnapshotComponentKey** per top-level component)
4. **One** `InventoryDeductionJob` with status **PENDING**

Applies to:

- Cash commit
- The **winning** PayOS webhook state transition (AwaitingPayment/Unpaid → Completed/Paid)
- Offline order commit/sync

### 9.2 Rollback coupling

| Outcome | Effect |
|---------|--------|
| Order/payment transaction rolls back | **No** job exists; **no** snapshot commit |
| Job insertion fails | Order/payment transition **also rolls back** |
| Snapshot write fails | Entire transaction rolls back |

Intent durability is not optional: if the sale is committed as paid/completed and requires deduction, the PENDING job must have been part of that commit.

### 9.3 Duplicates and ALREADY_PAID

- Duplicate webhook / offline calls **must not** create a second job (unique `OrderId`).
- For **ALREADY_PAID** or idempotent duplicate paths:
  - Do **not** create a second job.
  - An idempotent **ensure / reconciliation** path may restore a **missing** job **only when a valid sale-time snapshot exists**.
  - Without a valid snapshot → do not invent BOM; surface `LEGACY_ORDER_WITHOUT_SNAPSHOT` / REQUIRES_REVIEW.

### 9.4 What not to rely on

- Commit order → call deduction afterward → hope the process stays alive.
- Log-only failures without a durable job.
- Live Active Recipe as historical truth.

### 9.5 Eligibility

Create a job only when the order requires POS sales stock deduction (`Source = POS` or equivalent; terminal paid/completed transition). Not for pure AwaitingPayment holds or non-POS channels in MVP.

---

## 10. Job model

### 10.1 Aggregate: `InventoryDeductionJob`

| Field | Purpose |
|-------|---------|
| `InventoryDeductionJobId` | PK |
| `OrderId` | Owning order; **unique** (one job per order) |
| `StoreId` | Scope |
| `ReferenceOrderId` | Grouping/audit alignment with ledger (typically = OrderId) |
| `ClientOrderId?` | Offline correlation |
| `Status` | See §11 (MVP set only) |
| `AttemptCount` | Attempts executed |
| `MaxAutomaticAttempts` | Cap before REQUIRES_REVIEW |
| `NextAttemptAt?` | Worker due time |
| `LastAttemptAt?` | Audit |
| `LastErrorCode?` / `LastErrorSummary?` | Sanitized |
| `FailureCategory?` | Transient \| DataBusiness \| AlreadyCompleted |
| `LockedAt?` / `LockedBy?` / `LockToken?` | Lease |
| `CreatedAt` | Intent creation |
| `ProcessingStartedAt?` | Lease/process start |
| `SucceededAt?` | Terminal success |
| `RequiresReviewAt?` | When entered review |
| `RowVersion` | Optimistic concurrency |

**Not in graduation MVP:** `CancelledAt`, `CANCELLED` status.

### 10.2 Uniqueness

- **Unique index on `OrderId`**.
- One active job record per order; no recreate after `SUCCEEDED`.

### 10.3 Do not persist

- Raw exception stacks as user-visible content.
- Payment secrets, tokens, full webhook payloads.
- Customer-sensitive data not required for deduction.

### 10.4 Attempt history table

MVP: **no** separate `InventoryDeductionAttempt` table. Counters + last sanitized error + application logs suffice.

### 10.5 Order projection (optional)

Optional denormalized `Order.InventoryDeductionStatus` for query performance:

- **Not** independently writable.
- **Must** be derived/synchronized from the job.
- **Must not** contradict the job.
- **`InventoryDeductionJob` remains the source of truth.**

---

## 11. Status lifecycle (final MVP)

### 11.1 Locked statuses

| Status | Meaning |
|--------|---------|
| `PENDING` | Durable intent created; not successfully completed |
| `PROCESSING` | Time-limited lease owned by orchestrator attempt |
| `RETRY_SCHEDULED` | Transient failure; automatic retry planned |
| `REQUIRES_REVIEW` | Non-transient / ambiguous failure; auto-retry stopped |
| `SUCCEEDED` | Full deduction complete; **terminal and immutable** |

### 11.2 CANCELLED removed from graduation MVP

**Remove `CANCELLED` from graduation MVP.**

Reason:

- The job is created only after the order/payment transition requiring inventory deduction has committed.
- A paid sale’s deduction obligation **cannot** be casually cancelled.
- Refund / return / cancellation inventory correction is a **separate reversal workflow**, not a job cancel.

### 11.3 Locked transitions

```
(create in order/payment/snapshot tx) → PENDING

PENDING            → PROCESSING
RETRY_SCHEDULED    → PROCESSING
PROCESSING         → SUCCEEDED
PROCESSING         → RETRY_SCHEDULED
PROCESSING         → REQUIRES_REVIEW
REQUIRES_REVIEW    → PENDING     (authorized manual retry only)

SUCCEEDED          → ∅           (immutable)
```

No other transitions in MVP. No generic `FAILED`.

---

## 12. Failure classification

### 12.1 Transient / retryable

`DB_TIMEOUT`, `DEADLOCK`, `TEMPORARY_CONNECTION_FAILURE`, `WORKER_INTERRUPTED`, `LEASE_EXPIRED`, `CONCURRENCY_CONFLICT`.

### 12.2 Data / business review

`MISSING_RECIPE_SNAPSHOT`, `INVALID_RECIPE_SNAPSHOT`, `MISSING_UNIT_CONVERSION`, `INVALID_UNIT_CONVERSION`, `INVALID_COMPONENT_IDENTITY`, `STORE_INVENTORY_MAPPING_ERROR`, `INVALID_QUANTITY`, `LEGACY_ORDER_WITHOUT_SNAPSHOT`, `ORDER_NOT_ELIGIBLE`, `PARTIAL_OR_AMBIGUOUS_MOVEMENTS`, `RETRY_CAP_REACHED`.

### 12.3 Already completed

`DEDUCTION_ALREADY_SUCCEEDED` — only when job is SUCCEEDED **or** reconciliation proves the **complete expected movement set** (not any single ReferenceOrderId row).

### 12.4 Explicit non-failures

- Insufficient stock / negative AvailableQty under ADR-0001 for POS sales.
- Soft-skip missing recipe is **rejected** going forward.

### 12.5 Retry policy

- Transient → bounded automatic backoff.
- Deterministic / ambiguous data → REQUIRES_REVIEW until authorized manual retry.
- Already complete → terminal SUCCEEDED; no stock rewrite.

---

## 13. Worker / lease model

### 13.1 Hosted service

`InventoryDeductionWorker` (name flexible):

1. Fetch due `PENDING` / `RETRY_SCHEDULED` (`NextAttemptAt <= now` or null).
2. Claim atomically (`RowVersion` + status guard).
3. Set `PROCESSING` + lease fields.
4. Call **InventoryDeductionOrchestrator** (same as immediate path).
5. Success: atomic stock + ledger + SUCCEEDED (§16).
6. Failure: separate short failure-state transaction (§17).
7. Crash: lease expires; reclaim safely.

### 13.2 Lease

- Time-limited (configuration-owned).
- Updates while PROCESSING require matching LockToken.
- Expired PROCESSING → reclaim to PENDING or RETRY_SCHEDULED (or re-claim with new token) without double stock effect (movement keys + full-set check).

### 13.3 Concurrency

One claim winner; loser no-ops. Inventory changes once.

---

## 14. Immediate attempt vs worker (shared path)

After the order transaction commits, the request/webhook path **may** attempt immediate processing for low latency.

**Locked rules:**

1. It must **claim** the already-persisted job.
2. It must use the **same lease protocol**.
3. It must call the **same InventoryDeductionOrchestrator**.
4. It must use the **same success transaction and idempotency rules** as the worker.

It is **only an optimization**. The **background worker remains recovery authority**.

After cutover: **no separate direct best-effort deduction writer** remains.

Concurrent immediate + worker:

- One claims the job.
- The other receives conflict / no-op.
- Inventory changes once.

API may report job-derived status (`PENDING` / `SUCCEEDED` / `REQUIRES_REVIEW` for UX). Order payment success is independent.

---

## 15. Sale-time snapshot and SnapshotComponentKey

### 15.1 Target source (ADR-0006)

- `OrderDetail.RecipeSnapshotJson` (+ schema version).
- Worker **must not** resolve current Active Recipe as historical truth.

### 15.2 Snapshot contents

Normalized top-level components for one-level POS deduction:

- IngredientId **or** PreparedItemId
- Quantity / source unit / normalized base quantity
- Recipe/version metadata for audit
- **`SnapshotComponentKey`** — stable key **generated and persisted when the order commits**

### 15.3 SnapshotComponentKey (locked)

- Assigned at **order commit** time and stored inside the snapshot (and/or denormalized columns if useful).
- **Must not** be derived only from a mutable array position at processing time.
- Survives serialization/deserialization unchanged.
- Distinct keys when two OrderDetails consume the same Ingredient/PreparedItem (quantity and line context differ).

### 15.4 Legacy without snapshot

- `LEGACY_ORDER_WITHOUT_SNAPSHOT`.
- Do not silently use live Active Recipe.
- Reconciliation surfaces review; does not invent historical BOM.

### 15.5 Online unavailable-before-pay

Complementary product rule: online POS item without resolvable recipe unavailable before payment. Offline blind selling (ADR-0001) continues; sync-time snapshot/recipe gaps → durable review.

---

## 16. Success attempt transaction (locked)

**One atomic deduction transaction** for a successful attempt:

1. Verify valid `PROCESSING` lease.
2. Verify committed/paid order eligibility (correct store/source).
3. Load immutable sale-time snapshot.
4. Validate **all** component identities and quantities.
5. Reload `StoreInventory` rows.
6. Apply **all** one-level stock mutations.
7. Insert **all** component `InventoryTransaction` rows (with unique keys + `InventoryDeductionJobId` + `ReferenceOrderId`).
8. Mark `InventoryDeductionJob` **SUCCEEDED**.
9. Set `SucceededAt`.
10. **Commit once.**

**Locked:** `StoreInventory` mutations, ledger movements, and job `SUCCEEDED` **must commit in the same transaction**.

**Do not** commit stock first and update the job later.

Any failure:

- Rollback stock.
- Rollback ledger.
- Rollback SUCCEEDED transition.

### 16.1 SUCCEEDED invariant (locked)

`InventoryDeductionJob` may be `SUCCEEDED` only when:

1. Every expected snapshot component has **exactly one** committed movement for its unique key.
2. No expected component is missing.
3. Committed movement quantities match **normalized snapshot quantities**.
4. Stock before/after and ledger writes are committed.
5. The job success transition is in the **same** transaction.

**Do not** mark SUCCEEDED only because a method returned without throwing.

After SUCCEEDED the job is **immutable**.

### 16.2 StockAlert semantics (locked)

The core deduction success transaction includes:

- Stock mutations
- Inventory ledger
- Job SUCCEEDED

**StockAlert evaluation is a separate post-success, non-blocking side effect.**

If StockAlert evaluation fails:

- Job remains SUCCEEDED.
- Deduction is **not** retried.
- Alert failure is logged / reconciled separately.

Notification failure also does **not** change deduction success.

StockAlert is **not** part of the exactly-once stock invariant.

---

## 17. Failure-state persistence (locked)

After the deduction transaction **rolls back**:

Use a **separate short transaction** to update the job:

- `AttemptCount`
- `LastAttemptAt`
- Sanitized error code / summary
- `RETRY_SCHEDULED` + `NextAttemptAt`  
  **or**  
  `REQUIRES_REVIEW` + `RequiresReviewAt`
- Clear / update lease fields as appropriate

If this failure-state update **also** fails:

- The job may remain `PROCESSING`.
- **Lease expiry and reconciliation must recover it.**
- **No stock mutation has committed.**

**Do not** attempt to preserve failure state inside the transaction being rolled back.

---

## 18. Idempotency / movement keys / ReferenceOrderId

### 18.1 Defense in depth

| Layer | Mechanism |
|-------|-----------|
| **A. Unique job** | One job per `OrderId` |
| **B. Status guard** | Only PENDING / RETRY_SCHEDULED claimable; SUCCEEDED terminal |
| **C. Lease** | One processor owns PROCESSING |
| **D. Unique movement keys** | Per component via SnapshotComponentKey (§18.2) |
| **E. Full-set completion** | SUCCEEDED only when all expected keys present with matching qty |
| **F. ReferenceOrderId** | Grouping / audit / coarse compatibility — **not completion proof** |

### 18.2 Stable movement uniqueness (locked)

Recommended uniqueness:

```
OrderId + OrderDetailId + SnapshotComponentKey + MovementType(SALES_DEDUCTION)
```

Each `InventoryTransaction` should also reference:

- `InventoryDeductionJobId`
- `ReferenceOrderId` (grouping/audit)

**IngredientId / PreparedItemId alone is not a sufficient key** because:

- Multiple OrderDetails may consume the same item.
- Quantity and component context differ.

### 18.3 ReferenceOrderId is not completion proof (locked)

Existing ReferenceOrderId usage remains useful for **grouping and coarse compatibility**.

It is **not sufficient** to conclude the full order was deducted merely because **any** transaction with that `ReferenceOrderId` exists.

**Do not keep as the final target:**

> if any movement exists for ReferenceOrderId, return already deducted

**Target completion** is based on:

1. Job status **SUCCEEDED**, and
2. The **complete expected set** of unique component movements.

Legacy partial/ambiguous movements require **reconciliation / review**, not automatic “already done”.

---

## 19. Retry / backoff

### 19.1 Automatic

- Bounded exponential backoff (configuration-owned; e.g. 1m, 5m, 15m, 1h).
- Cap `MaxAutomaticAttempts` → REQUIRES_REVIEW (`RETRY_CAP_REACHED`).

### 19.2 Manual

See §21. Manual action only moves status; orchestrator performs stock work.

### 19.3 Deterministic

No automatic retry without data change or authorized manual action.

---

## 20. Reconciliation rules (locked)

| Finding | Action |
|---------|--------|
| Paid order **with valid snapshot** and **no job** | Create one PENDING job **idempotently** |
| Paid **legacy** order **without snapshot** | Do **not** resolve current Active Recipe; create/surface REQUIRES_REVIEW with `LEGACY_ORDER_WITHOUT_SNAPSHOT` (or equivalent anomaly record) |
| Expired PROCESSING lease | Reclaim safely to PENDING / RETRY_SCHEDULED |
| Overdue RETRY_SCHEDULED | Make eligible for claim |
| Existing movements but job not SUCCEEDED | Compare **expected** component movement keys with actual committed movements: if **complete** and quantities match → reconcile safely to SUCCEEDED per documented policy; if **partial, missing, or ambiguous** → REQUIRES_REVIEW |
| Completion shortcut | **Never** treat “any ReferenceOrderId movement exists” as complete |
| Partial remaining lines | **Never** blindly deduct remaining lines without controlled analysis |
| Historical BOM | Reconciliation **never invents** historical BOM data |

---

## 21. Manual review / retry (locked)

### 21.1 Who

| Role | View | Manual retry |
|------|------|--------------|
| **StoreManager** | Same-store | **Yes** (same-store) |
| **SystemAdmin** | All | **Yes** when elevated policy permits |
| **ShiftSupervisor** | Warning visibility only | **No** (MVP) |
| **AccountantWarehouse** | Read-only if enabled | **No** |
| **SalesStaff** | No admin failure view (optional sale warning) | **No** |
| **AreaManager / BusinessOwner** | Audit scope | **No** (graduation MVP) |

Cross-store retry is **blocked by backend**.

### 21.2 Manual retry must

1. Verify job is `REQUIRES_REVIEW`.
2. Verify **no active PROCESSING lease**.
3. Revalidate snapshot / configuration.
4. Require an **operational review note**.
5. Transition job to **PENDING** only.

### 21.3 Manual retry must not

- Directly mutate inventory.
- Mark SUCCEEDED without complete movement evidence.
- Edit inventory quantities from the failure screen.
- Delete the job.
- Change payment status.
- Call a separate deduction code path.

The shared worker/orchestrator performs the deduction after the job becomes PENDING.

### 21.4 Operational view

Order code, store, payment/commit time, job status, attempts, last sanitized error, next/last attempt, order link, snapshot validation summary, review notes.

---

## 22. Authorization / store scope

Backend enforces role **and** store scope. Align with existing POS/admin store scoping patterns. Worker is system-scoped for claim/process only.

---

## 23. Notifications

| Event | Who |
|-------|-----|
| REQUIRES_REVIEW | StoreManager + SystemAdmin / technical ops |
| Retry cap reached | StoreManager + AreaManager and/or SystemAdmin by scope |
| Manual retry succeeded | Requester / reviewer |
| Long-stuck PENDING/PROCESSING | Technical |
| Normal SUCCEEDED | No spam |

Notification failure does **not** roll back job or stock. Do **not** reuse StockAlert as the deduction-failure record.

---

## 24. Order / payment / UI semantics

| Concern | Rule |
|---------|------|
| Source of truth | **`InventoryDeductionJob`** |
| Order projection | Optional, derived only, never contradicts job |
| Order / payment | Remain Paid/Completed |
| Receipt / printing | Independent of deduction pending |

UI mapping:

| Job status | Display |
|------------|---------|
| PENDING / PROCESSING / RETRY_SCHEDULED | “Đang cập nhật tồn kho” |
| SUCCEEDED | “Đã cập nhật tồn kho” |
| REQUIRES_REVIEW | “Cần kiểm tra trừ kho” |

Do not display raw technical exceptions to cashiers.

---

## 25. Reversal / correction

After `SUCCEEDED`:

- Do not retry or edit the original deduction job.
- Do not delete `InventoryTransaction` rows.
- Correction uses **inventory adjustment / reversal** movements under separate business rules.
- Payment refund / cancel does **not** automatically restore inventory without explicit return/cancellation policy.
- That workflow is **not** a `CANCELLED` job status.

---

## 26. Migration / rollout (locked order)

### 26.1 Expected future schema (not in this ADR task)

- OrderDetail snapshot fields + stable **SnapshotComponentKey**.
- `InventoryDeductionJob` table + indexes + RowVersion + lease fields.
- Unique movement keys on `InventoryTransaction` (+ optional `InventoryDeductionJobId` FK).
- Optional Order projection synchronized from job.
- Optional review note fields.

### 26.2 Safe rollout sequence

1. Add OrderDetail snapshot fields and stable **SnapshotComponentKey**.
2. Add `InventoryDeductionJob` schema and movement unique keys.
3. Deploy **producer** logic that writes snapshot + PENDING job in the same order/payment transaction.
4. Keep **worker disabled** until producers are verified.
5. Route optional immediate attempts through the **shared job orchestrator**.
6. Enable background worker.
7. Enable reconciliation scan.
8. Verify cash, PayOS, and offline all use the shared pipeline.
9. Remove/disable direct best-effort-only deduction calls.
10. Run legacy reconciliation.
11. Ensure **only one deduction writer** remains.

Feature flags may control rollout but **must never allow two independent writers** to mutate inventory for POS sales deduction.

**No migration is created by this ADR document.**

---

## 27. Consequences

### Positive

- Crash after paid commit cannot permanently lose deduction intent.
- Cash / PayOS / offline share one durable pipeline and orchestrator.
- Full-set SUCCEEDED invariant prevents false “already deducted”.
- Partial legacy evidence becomes reviewable instead of silently wrong.
- Payment success semantics remain honest.

### Negative / cost

- Extra table + worker + ops UI + snapshot dependency.
- Dual short transactions for failure persistence.
- Reconciliation load for legacy/partial orders.

### Neutral

- StockAlert and printing remain separate side effects.
- ADR-0001 negative stock on sale unchanged.

---

## 28. Rejected alternatives

| Alternative | Why rejected |
|-------------|--------------|
| Keep post-commit best-effort only | Not durable |
| Stock mutation inside payment commit | Not approved; wrong operational boundary |
| CANCELLED job status in MVP | Paid sale obligation not casually cancellable; reversal is separate |
| Generic FAILED only | Mixes retry vs review |
| Failure-only outbox (write on error) | Misses crash-before-attempt; intent-at-commit is stronger |
| “Any ReferenceOrderId movement ⇒ done” as target | Hides partial/ambiguous completion |
| IngredientId alone as movement key | Collides across OrderDetails |
| Mutable array index as component key | Unstable across serialize/process |
| Commit stock then update job later | Crash window breaks SUCCEEDED invariant |
| StockAlert inside exactly-once stock tx as hard dependency | Alert failure must not undo stock/job success semantics incorrectly; alert is non-blocking post-success |
| Separate immediate vs worker writers | Race double-deduct risk |
| Manual retry mutates stock directly | Bypasses orchestrator and lease |
| Silent live Active Recipe for legacy | Falsifies historical BOM |
| Mark SUCCEEDED without full movement set | Inventory integrity lie |

---

## 29. Test requirements (future implementation)

### 29.1 Core producer / paths

1. Cash commit creates one PENDING job + snapshot in the same transaction.
2. Failed order commit creates no job and no snapshot.
3. Snapshot and PENDING job **rollback if job insertion fails**.
4. PayOS winning state transition creates one job.
5. Duplicate webhook creates no duplicate job.
6. Offline sync creates one job by ClientOrderId/OrderId.
7. Repeated offline sync creates no duplicate job.

### 29.2 Success / failure transactions

8. Stock + ledger + SUCCEEDED commit **atomically**.
9. Failure rollback leaves **no** stock/ledger/success status.
10. Failure-state update occurs **after** rollback.
11. Failure-state update failure is recovered by lease expiry / reconciliation.
12. Process crash after commit leaves job recoverable.

### 29.3 Keys / completion

13. Stable SnapshotComponentKey survives serialization/deserialization.
14. Two OrderDetails consuming the same Ingredient create **distinct** movement keys.
15. Partial legacy movement does **not** count as completed deduction.
16. Complete expected movement set is required for SUCCEEDED.
17. Second success attempt does not double-deduct.
18. ReferenceOrderId alone is **not** treated as full completion in target behavior.

### 29.4 Concurrency / side effects

19. Worker claims one job; concurrent workers one winner.
20. Immediate attempt and worker race has one winner.
21. Lease expiry allows safe recovery.
22. StockAlert failure does **not** retry deduction; job stays SUCCEEDED.
23. Notification failure does not roll back job/deduction.

### 29.5 Review / auth / recon

24. Transient → RETRY_SCHEDULED; deterministic → REQUIRES_REVIEW; retry bounded.
25. Manual retry only transitions REQUIRES_REVIEW → PENDING (with note; no active lease).
26. Manual retry does **not** directly mutate stock.
27. StoreManager same-store retry allowed; ShiftSupervisor cannot; cross-store blocked.
28. Paid order with valid snapshot but missing job is repaired.
29. Legacy order without snapshot becomes review-required.
30. Reconciliation does not invent a Recipe.
31. Existing movements incomplete/ambiguous → REQUIRES_REVIEW (not blind remaining deduct).
32. No CANCELLED job path in MVP.
33. Only one writer remains after rollout.
34. Negative stock remains allowed under ADR-0001.
35. Refund does not automatically reverse inventory.
36. Existing POS/order/payment tests remain green.
37. Sale-time snapshot used instead of Active Recipe for historical orders.

---

## 30. Dependencies / follow-ups

| Dependency | Relation |
|------------|----------|
| **ADR-0006** | Snapshot, PreparedItem, SnapshotComponentKey co-design |
| **ADR-0005** / `4a8cfa2` | Fail-closed conversion |
| **ADR-0001** | Negative stock / offline blind selling |
| **ADR-0002** | ClientOrderId offline identity |
| **ADR-0004** | One-level POS stock |
| Phase 1 issues | Schema, producer, orchestrator, worker, recon, admin UI, cutover tests (**not** created by this ADR task) |

---

## 31. Remaining questions

Non-blocking for architecture acceptance:

1. Exact `MaxAutomaticAttempts` and backoff ladder defaults.
2. Lease duration default.
3. Whether `AttemptCount` increments on manual retry enqueue.
4. Whether optional Order projection column is required for history list performance.
5. Exact `StaffNotification` type names and channel ownership.
6. Whether AccountantWarehouse read-only dashboard is MVP or later.
7. Safe policy details for reconciling complete legacy movement sets to SUCCEEDED (audit requirements).
8. Whether immediate attempt is default-on for cash or feature-flagged.
9. SystemAdmin retry audit trail requirements.

**None of the above blocks this ADR’s structural decisions.**

---

## Document control

| Item | Value |
|------|--------|
| Authors context | Phase 0 #109 on `feature/POS` |
| Status | **Accepted** |
| Accepted Date | 2026-07-11 |
| Implementation | Not started in this document |
| Migration in this task | **No** |
| Code changes in this task | **No** |
