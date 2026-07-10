# ADR-0008: RestockRequest to Branch Receipt Workflow

| Field | Value |
|-------|--------|
| **Status** | Accepted |
| **Date** | 2026-07-11 |
| **Accepted Date** | 2026-07-11 |
| **Issue** | [#108](https://github.com/TheSkibidi1712/CafeChain/issues/108) |
| **Branch context** | `feature/POS` |
| **Related** | ADR-0004, **ADR-0005/0006/0007 Accepted**, Restock #100, StockAlert #97–#99 |

---

## 1. Title

RestockRequest as intent; **line-level fulfillment** to document/transfer details; durable **BranchReceipt**; dispatch vs destination receipt; partial/short/over/rejected quantities; alert re-evaluation; cost layers; roles and idempotent confirmation.

## 2. Status

**Accepted** — domain decisions locked for implementation.  
This ADR document does not include schema/code; implementation and migration follow later issues/PRs.  
Locked: **RestockRequestFulfillment per source detail line**, separate **BranchReceipt / BranchReceiptLine**, lifecycle without APPROVED/RECEIVED, transfer **mandatory DISPATCHED** + in-transit cost capture, short-close fields, full role matrix, atomic confirm + unique keys, alert policy after receipt only.

## 3. Date

2026-07-11 (draft + review); **Accepted Date: 2026-07-11**

## 4. Context

Issue #100 shipped RestockRequest as a **ticket** from CONFIRMED StockAlert: create + notify AccountantWarehouse; **no stock mutation**; no document/transfer link.

Inventory still grows via independent **InventoryDocument Confirm** (IMPORT → `AvailableQty += BaseQuantity` + cost layer) and **InventoryTransfer Confirm** (same tx: **source − and destination +**). Neither path models restock lifecycle, line-level multi-item docs, or destination-only branch receipt.

Target:

```
Alert CONFIRMED → RestockRequest (intent)
  → AW PROCESS + line-level fulfillment(s)
  → Dispatch (source− / in-transit; destination stock unchanged)
  → BranchReceipt CONFIRMED (destination stock + received only)
  → RestockRequest status derived from totals
  → StockAlert re-evaluate (not auto-complete on request alone)
```

Does **not** redefine package cost (ADR-0005), PreparedItem (ADR-0006), or ProductionOrder (ADR-0007).

---

## 5. Current-state findings (code-verified)

### 5.1 RestockRequest

| Item | Finding |
|------|---------|
| Statuses | SUBMITTED, PROCESSING, COMPLETED, REJECTED, CANCELLED |
| Create | StoreManager; alert CONFIRMED; one open SUBMITTED per alert |
| Stock | **No mutation** |
| HandledByStaffId / HandledAt | Reserved, unused |
| Item | IngredientId XOR RecipeId (→ PreparedItem) |
| Fulfillment FK | **None** |
| Notify | RESTOCK_REQUEST_SUBMITTED → AccountantWarehouse |

### 5.2 InventoryDocument IMPORT

- Status DRAFT / PENDING / CONFIRMED / CANCELLED.  
- **Confirm** processes stock + cost layer (BaseQuantity).  
- Detail-level Quantity / UnitId / BaseQuantity / UnitPrice.  
- RequestKey on header; no restock link; no separate branch receipt entity.

### 5.3 InventoryTransfer

- Status DRAFT / COMPLETED / CANCELLED only.  
- **Confirm** = source− **and** destination+ in one step (`OUT_TRANSFER` + `IN_TRANSFER`).  
- Details: **Ingredient only**.  
- RequestKey + deduplication. **No** dispatch/receipt split; **no** in-transit account.

### 5.4 StockAlert

Evaluate may **RESOLVE** when qty > MinStockLevel. Not driven by RestockRequest create/dispatch.

---

## 6. Problems

Intent confused with stock; no line-level multi-item fulfillment; transfer both-ends confirm hides in-transit; no durable receipt immutability; partial/short not on request; dual risk of double receive without unique receipt keys.

---

## 7. Terminology

| Term | Meaning |
|------|---------|
| **RestockRequest** | Intent only |
| **RestockRequestFulfillment** | Link of one request to **one source detail line** (document detail or transfer detail) |
| **Dispatch** | Source leaves; destination available **unchanged** |
| **BranchReceipt** | Destination confirm; only CONFIRMED mutates stock |
| **InTransitQuantityBase** | Dispatched − Received (per fulfillment/transfer line) |
| **ClosedShortQuantityBase** | Explicitly closed unreceived remainder (not received) |
| **RejectedQuantityBase** | Damaged/rejected; not AvailableQty |

---

## 8. Decision

### 8.1 Intent vs stock

- Create/process/dispatch RestockRequest **does not** increase destination stock.  
- Destination stock increases **only** on **BranchReceipt CONFIRMED**.  
- Dispatch ≠ receipt.

### 8.2 Line-level fulfillment (locked)

**RestockRequestFulfillment** links a RestockRequest to **one source detail line**.

```text
RestockRequestFulfillment
  RestockRequestFulfillmentId
  RestockRequestId
  FulfillmentType: PURCHASE_DOCUMENT | INVENTORY_TRANSFER
  InventoryDocumentDetailId?   // XOR
  InventoryTransferDetailId?   // XOR
  PlannedQuantityBase
  DispatchedQuantityBase
  ReceivedQuantityBase
  ClosedShortQuantityBase
  Status
  RowVersion
```

**XOR:** exactly one of `InventoryDocumentDetailId` or `InventoryTransferDetailId`.

**Do not** link only to document/transfer **header**.

**Reasons:** multi-line documents; multi-batch/sources; partial qty auditable per line.

**MVP:** multiple fulfillment rows per one RestockRequest **supported**.

A source detail line must **not** silently fulfill multiple requests unless **explicitly split** into separate fulfillment records.

### 8.3 Durable BranchReceipt (locked)

**BranchReceipt** and **BranchReceiptLine** are separate durable records.

```text
BranchReceipt
  BranchReceiptId
  StoreId
  SourceType
  InventoryDocumentId?
  InventoryTransferId?
  Status: DRAFT | CONFIRMED | CANCELLED
  ReceiptKey / IdempotencyKey
  ReceivedByStaffId
  ReceivedAt?
  Notes?
  RowVersion

BranchReceiptLine
  BranchReceiptLineId
  BranchReceiptId
  RestockRequestFulfillmentId
  InventoryDocumentDetailId?
  InventoryTransferDetailId?
  IngredientId?          // XOR with PreparedItemId
  PreparedItemId?        // XOR with IngredientId
  ExpectedQuantityBase
  ReceivedQuantityBase
  RejectedQuantityBase
  BaseUnitId
  UnitCostSnapshot?
  DiscrepancyReason?
  InventoryTransactionId?
```

**BranchReceiptLine item identity (locked):** enforce **exactly one** of `IngredientId` or `PreparedItemId` — never both, never neither.

- **Only CONFIRMED** receipt mutates `StoreInventory`.  
- **Confirmed receipt is immutable.**  
- Post-confirm correction = future adjustment/reversal; **never** edit/delete original stock movements.

### 8.4 Item identity

On request, fulfillment, and receipt lines: IngredientId **XOR** **PreparedItemId** (ADR-0006); quantities in canonical base unit; conversion fail-closed.

---

## 9. RestockRequest lifecycle (locked MVP)

```text
SUBMITTED
PROCESSING
DISPATCHED
PARTIALLY_RECEIVED
COMPLETED
REJECTED
CANCELLED
```

- **No** separate APPROVED in MVP.  
- **No** separate RECEIVED; full close = **COMPLETED**.

| Status | Meaning |
|--------|---------|
| SUBMITTED | No accepted fulfillment work yet |
| PROCESSING | AW accepted/created fulfillment |
| DISPATCHED | Dispatched qty > 0 and received qty = 0 |
| PARTIALLY_RECEIVED | Cumulative received > 0 and outstanding remains |
| COMPLETED | Fully received **or** remaining explicitly closed short |
| REJECTED | AW reject + reason; no stock |
| CANCELLED | Cancelled while reversible + reason |

**Status is derived/set by backend** from fulfillment totals — **no arbitrary UI status changes**.

### Derivation sketch

```
if rejected/cancelled → terminal
else if received + closedShort >= planned (allowed) → COMPLETED
else if received > 0 → PARTIALLY_RECEIVED
else if dispatched > 0 → DISPATCHED
else if fulfillment linked → PROCESSING
else → SUBMITTED
```

---

## 10. Fulfillment model

See §8.2 — line-level only; multi-row MVP.

---

## 11. Supplier import path

**DISPATCHED is optional** for pure supplier purchase.

Allowed:

```
SUBMITTED → PROCESSING → PARTIALLY_RECEIVED / COMPLETED
```

or when shipping tracked:

```
PROCESSING → DISPATCHED → receipt(s)
```

Stock increases only on **BranchReceipt CONFIRMED** against import detail lines (not on document draft create). Document confirm semantics must align with receipt confirm (implementation maps current Confirm to destination receipt where appropriate).

---

## 12. Transfer path (locked)

**DISPATCHED is mandatory** before destination receipt.

### Dispatch

- Decreases **source** inventory  
- Writes **TRANSFER_OUT** (`OUT_TRANSFER`)  
- Snapshots **transferred cost allocations**  
- Destination stock **does not** increase  

### Receipt

- Increases **destination** inventory by received qty only  
- Writes **TRANSFER_IN** (`IN_TRANSFER` / receipt movement)  
- Uses **dispatched source cost basis** — **not** `Supplier.CurrentPrice`  

### In-transit

```
InTransitQuantityBase = DispatchedQuantityBase - ReceivedQuantityBase
```

MVP: store/derive on transfer detail and/or fulfillment.  
**No** fake destination `StoreInventory` increase at dispatch.  
Enterprise reporting must be able to include in-transit quantity.

---

## 13. Branch receipt model

See §8.3.

---

## 14. Quantity semantics

All base unit:

| Field | Meaning |
|-------|---------|
| RequestedQuantityBase | Manager request |
| PlannedQuantityBase | On fulfillment line (from AW plan) |
| DispatchedQuantityBase | Source sent |
| ReceivedQuantityBase | Cumulative confirmed receive |
| ClosedShortQuantityBase | Explicit unreceived closure |
| RejectedQuantityBase | Not into AvailableQty |

UI may use purchase units; backend converts.

---

## 15. Partial / short / over / rejected (locked)

| Case | Policy |
|------|--------|
| **Partial** | Supported; each CONFIRMED receipt increases stock by **that receipt’s ReceivedQuantity only**; cumulative update transactional; request PARTIALLY_RECEIVED while remaining > 0 |
| **Full receive** | COMPLETED |
| **Short close** | Fields: ClosedShortQuantityBase, CloseShortReason, ClosedShortByStaffId, ClosedShortAt. **Not** silently “received”. Requires **branch discrepancy confirmation** + **AccountantWarehouse** processing/ack + **reason** |
| **Over-receipt** | **Blocked** graduation MVP |
| **Rejected/damaged** | `RejectedQuantityBase` + reason required; **does not increase `AvailableQty`**; **is not included in `ReceivedQuantityBase`**; quarantine workflow is future scope |

---

## 16. Atomic receipt transaction (locked)

Confirm BranchReceipt in **one** DB transaction:

1. Validate receipt **DRAFT**.  
2. Validate source and destination StoreId.  
3. Validate role/store scope.  
4. Apply RowVersion/concurrency.  
5. Validate cumulative dispatched/received/closed-short.  
6. Validate **no over-receipt**.  
7. Normalize to base unit (fail-closed).  
8. Increase StoreInventory by **ReceivedQuantityBase only**.  
9. Write **one** receipt InventoryTransaction **per line**.  
10. Create/update cost layer if cost complete.  
11. Mark BranchReceipt **CONFIRMED**.  
12. Update fulfillment cumulative totals/status.  
13. Update RestockRequest status (derived).  
14. Re-evaluate StockAlert.  
15. Commit.

**Any error:** rollback stock, ledger, receipt, request/fulfillment/alert updates. No partial writes.

---

## 17. Idempotency / concurrency (locked)

- Unique **ReceiptKey**  
- Unique transaction key: **`BranchReceiptId + BranchReceiptLineId + MovementType`**  
- Second confirm does not double stock  
- Concurrent confirms: one winner  
- Retry after rollback: once  

Do not rely on frontend disable.

---

## 18. StockAlert resolution (locked)

**RestockRequest state does not directly resolve StockAlert.**

After **each confirmed receipt**:

- Re-evaluate actual StoreInventory.  
- Active shortage alerts include **OPEN** and **CONFIRMED**.  
- If `AvailableQty > MinStockLevel` → resolve active alert.  
- If still `<= threshold` → keep/update alert.  
- **MANAGER_REJECTED** is **not** automatically reopened/resolved without explicit service policy.

A RestockRequest may be **COMPLETED** (including short-close) while StockAlert **remains unresolved** if stock is still low — document this distinction clearly.

---

## 19. Cost layer (locked)

| Path | Rule |
|------|------|
| Supplier receipt | Layer qty = **actual ReceivedQuantityBase**; unit cost from confirmed document/package normalization (ADR-0005); **never** requested or dispatched qty for the new layer |
| Transfer receipt | Preserve cost allocations captured at **source dispatch** |
| Incomplete cost | Quantity receipt may complete if policy permits; mark incomplete; **no** zero-cost layer; notify/audit |
| PreparedItem | PreparedItem identity + base unit |

---

## 20. Authorization / store scope (locked MVP)

| Role | Create request | Process / link detail fulfillment | Dispatch | Branch receipt | Close-short | Reject |
|------|----------------|-----------------------------------|----------|----------------|-------------|--------|
| **StoreManager** | Yes (own store) | No | No | **Yes** own store | Report discrepancy | Cancel own SUBMITTED |
| **ShiftSupervisor** | View same-store | No | **No** | **Yes** same-store | Report discrepancy | Limited |
| **AccountantWarehouse** | View | **Yes** | **Yes** | **No** by default | **Handle** close-short | Yes |
| **SalesStaff** | Shortage report only | No | No | No | No | No |
| **AreaManager / BusinessOwner / SystemAdmin** | Audit by scope | No | No | No in MVP | — | — |

Backend enforces role + StoreId. Frontend is not authorization.

---

## 21. Cancellation / reversal

| Status | Rule |
|--------|------|
| SUBMITTED | Reject/cancel + reason |
| PROCESSING | Cancel only if no irreversible dispatch |
| DISPATCHED / PARTIALLY_RECEIVED | No simple cancel; receive / return / reversal |
| COMPLETED | Immutable; future adjustment only |

Never delete inventory transactions. Confirmed BranchReceipt immutable.

---

## 22. Notifications (non-blocking)

Submitted → AW; rejected → SM; processing → SM; dispatched → SM/ShiftSupervisor; partial/complete → SM+AW; discrepancy/close-short → AW+SM; cost incomplete → AW. Email optional.

---

## 23. UI implications

Request details: item, base unit, requested/planned/dispatched/received/closed-short/remaining, timeline, fulfillment lines (doc/transfer **detail** refs), actions by role.

Receipt: expected, already received, this qty, rejected, unit suffix, remaining, before/after, cost status.

**RestockRequest form never mutates stock.**

---

## 24. Migration / data remediation

Future (not now):

- RestockRequestFulfillment table  
- BranchReceipt + BranchReceiptLine  
- Transfer dispatched/received/status + cost allocation / in-transit fields  
- PreparedItem on request/fulfillment/receipt lines  
- Base quantity fields; RowVersion  
- Unique ReceiptKey / ledger indexes  
- Close-short + discrepancy fields  

**Legacy completed transfers** that already source−/destination+ **must not** be replayed as new receipts during backfill.

No name-based mapping.

---

## 25. Consequences

### Positive

Line-level audit; true branch receipt; transfer in-transit; partial/short control; alert honesty; role split.

### Negative

Breaking change vs transfer both-ends confirm; larger schema; ops training.

---

## 26. Rejected alternatives

| Alternative | Why |
|-------------|-----|
| Header-only document/transfer link | Multi-line ambiguity |
| Stock + on request/dispatch | Violates receipt rule |
| Transfer both-ends as final model | Hides in-transit |
| Over-receipt free MVP | Control risk |
| Silent short = received | False stock |
| Alert resolve on request create | Qty may still be low |
| Edit confirmed receipt | Audit hole |

---

## 27. Test requirements (implementation)

1. One document multi-line → multiple fulfillment records.  
2. One request may have multiple fulfillment rows.  
3. Supplier receipt may skip DISPATCHED.  
4. Transfer receipt cannot occur before dispatch.  
5. Transfer dispatch tracks in-transit quantity.  
6. Transfer cost basis preserved.  
7. Receipt line stock uses ReceivedQuantity only.  
8. RejectedQuantity does not increase stock.  
9. Close-short requires reason + AW handling.  
10. Request completed short may leave StockAlert unresolved.  
11. ShiftSupervisor may confirm same-store receipt.  
12. ShiftSupervisor cannot dispatch.  
13. Receipt status immutable after confirmation.  
14. Restock status derived from fulfillment totals.  
15. Concurrent partial receipts cannot exceed dispatched.  
16. Legacy completed transfer not double-received in migration.  
17. Create request no stock mutate.  
18. Over-receipt blocked.  
19. Duplicate receipt no double increase.  
20. Cross-store receipt blocked.  
21. SalesStaff cannot receive.  
22. AW cannot destination-receive by default.  
23. Cost layer uses received qty; no zero layer.  
24. Unit conversion fail-closed.  
25. Regression #100 create/notify.  

---

## 28. Dependencies / follow-ups

| Item | Role |
|------|------|
| ADR-0005 | Import cost normalization |
| ADR-0006 | PreparedItem identity |
| ADR-0007 | Separate from production |
| **#108** | This ADR |
| #109 | Sale deduction failure (separate) |

---

## 29. Remaining questions

**Implementation-level only:**

1. Exact transfer status enum names for DISPATCHED vs COMPLETED after receipt.  
2. Dual-read window with PreparedItem cutover.  
3. Whether supplier IMPORT always skips DISPATCHED in first UI.  
4. Notification channel matrix (in-app only vs email).

**Not open:** line-level fulfillment; BranchReceipt durable; lifecycle; transfer dispatch mandatory; in-transit; partial/short/over/rejected; auth matrix; atomic confirm; alert policy; cost rules.

---

## Examples (normative)

### 1 — Full supplier  
10 kg requested → receive 10 kg base → stock+, ledger, cost layer, COMPLETED, alert re-eval.

### 2 — Partial  
6 kg then 4 kg; PARTIALLY_RECEIVED then COMPLETED.

### 3 — Transfer  
Dispatch A −5 kg, in-transit 5 kg, B unchanged; receipt B +5 kg, cost from dispatch allocation.

### 4 — Short  
9.5/10 with CloseShortReason + AW ack → COMPLETED; alert may remain if still low.

### 5 — Multi-line document  
One IMPORT with coffee + sugar lines → two fulfillment rows → separate receipts.

### 6 — Duplicate receipt  
Second confirm → no second stock increase.

---

## Decision summary (locked)

1. RestockRequest = intent only.  
2. **Line-level RestockRequestFulfillment** (detail XOR, multi-row).  
3. **BranchReceipt + Line** durable; only CONFIRMED mutates stock; immutable after.  
4. Lifecycle SUBMITTED…COMPLETED; status derived; no APPROVED/RECEIVED MVP.  
5. Supplier DISPATCHED optional; transfer DISPATCHED mandatory.  
6. In-transit = dispatched − received; cost snapshotted at transfer dispatch.  
7. Partial yes; short-close explicit + AW; over no; rejected not stocked.  
8. Atomic confirm + ReceiptKey + unique ledger keys.  
9. Alert re-eval after receipt; may stay open after short complete.  
10. Cost on received qty; transfer source basis; no zero fake layer.  
11. SM + ShiftSupervisor receive same-store; AW process/dispatch; no hard-coded store.
