# SC-01 - Ke hoach back-post nhan/close ve Purchase Advice

## 1. Executive summary

SC-01 can chot quan he giua nhu cau mua PurchaseAdvice va ket qua thuc te cua PO. Hien tai BranchReceipt da la inventory writer dung: receipt CONFIRMED tang StoreInventory, tao InventoryTransaction va InventoryCostLayer, ghi PurchaseOrderReceiptPosting va RestockFulfillmentPosting. Tuy nhien Accepted/Closed tren PurchaseAdviceLine chua co writer nghiep vu duoc chung minh.

PurchaseOrderService.CloseLineRemainingAsync luu so luong dong con lai tren PurchaseOrderLine, nhung chua ghi Closed ve PA. Vi vay PA co the dung o ALLOCATED va khong phan biet da nhan, da dong khong giao bu hay con chua xu ly.

Khuyen nghi: them PurchaseAdviceFulfillmentPosting lam ledger traceability cho Accepted/Closed, dong thoi giu PurchaseAdviceLine.AcceptedBaseQuantity va ClosedBaseQuantity lam cached aggregate cap nhat atomically. Cach nay phu hop voi RestockFulfillmentPosting, replay idempotent va audit; khong thay the inventory writer.

MIGRATION_REQUIRED = YES. Migration `AddPurchaseAdviceFulfillmentPostings` duoc tao local de verify model, nhung khong commit migration/ModelSnapshot theo quyet dinh Owner; schema chinh thuc se duoc regenerate trong #190.

## 2. Baseline va GitHub issue

| Muc | Ket qua |
| --- | --- |
| Branch | feature/POS |
| HEAD | b5481784d63fd55a8d67a2acfc46e9dc74aa4980 |
| So voi origin | HEAD...origin/feature/POS = 4 0 |
| Dirty co san | CafeChain.Frontend/package-lock.json, CafeChain/FIX.md, CafeChain/appsettings.json |
| Staged | Khong co |
| git diff --check | Pass |
| GitHub issue | #193: https://github.com/TheSkibidi1712/CafeChain/issues/193 |

Search da tim thay #184, #186, #189 ve PA/batch/receiving va #190 ve InitialCreate, nhung khong co issue trung ve back-post Accepted/Closed ve PA. Da tao issue #193 dung scope.

Owner da review va phê duyet plan: chon Option A; giu mot allocation cho moi PO line; PA line duoc chia qua nhieu PO line; Close Remaining bat buoc RequestKey on dinh; line status derive, header status persisted; backfill chi dry-run/report; khong commit migration/snapshot trong #193 va schema chinh thuc xu ly qua #190.

Implementation #193 chi thay doi procurement domain/service, contract close remaining toi thieu, tests va tai lieu nay. Dirty files bao ve, migration/snapshot va UI ngoai scope khong duoc stage/commit.

## 3. Current broken flow

    RestockRequest
      -> PurchaseAdviceLine RequestedPurchaseBaseQuantity
      -> PurchaseOrderLineAllocation AllocatedBaseQuantity
      -> PurchaseOrderLine OrderedBaseQuantity
      -> BranchReceipt CONFIRMED
           -> PurchaseOrderReceiptPosting Accepted/Rejected
           -> Inventory + FIFO
           -> RestockFulfillmentPosting
      -> THIEU: PurchaseAdviceLine.AcceptedBaseQuantity

    PO CloseRemaining
      -> PurchaseOrderLine.ClosedRemainingQuantity + reason + actor + time
      -> PO status recalculated
      -> THIEU: PurchaseAdviceLine.ClosedBaseQuantity

Evidence:

- CafeChain/Models/Inventories/Procurement/PurchaseAdvice.cs co AcceptedBaseQuantity va ClosedBaseQuantity.
- CafeChain/Application/Services/Inventories/PurchaseAdviceService.cs chi map hai field, khong cap nhat.
- PurchaseOrderBatchService chi cap nhat AllocatedToPoBaseQuantity va status ALLOCATED.
- BranchReceiptService ghi PO posting va Restock posting truoc inventory, trong cung transaction.
- PurchaseOrderService.CloseLineRemainingAsync chi update PO line, khong co PA back-post.
- PA Status la string persisted; khong co writer fulfillment sau ALLOCATED.

Finding hien tai: MISSING_EVIDENCE/High. Inventory receipt hien van co authority dung; gap nam o PA tracking.

## 4. PurchaseAdvice authority

Entity: CafeChain/Models/Inventories/Procurement/PurchaseAdvice.cs.

- Header co AdviceNumber, RequestKey, StoreId, Status, RowVersion, Lines, Transitions.
- Line co Requested, Allocated, Accepted, Closed, IsActiveReservation, RowVersion.
- Header co nhieu line; line tro ve mot RestockRequest va Ingredient.
- Accepted/Closed la field co that, precision decimal(18,3), default 0, check non-negative.
- Header status persisted; PurchaseAdviceTransition la status history, khong phai fulfillment ledger.
- Config: unique AdviceNumber, RequestKey; index StoreId/Status/CreatedAt.
- Line co filtered unique RestockRequestId khi IsActiveReservation = 1.

Status constants hien tai trong PurchaseAdviceConstants.cs: DRAFT, SUBMITTED, UNDER_REVIEW, REJECTED, CANCELLED, ALLOCATED. Service writer thuc te cung chi tao cac status nay. UI co mapping PARTIALLY_ALLOCATED, FULLY_ALLOCATED, PARTIALLY_FULFILLED, COMPLETED nhung domain service chua tao cac status do: DOCUMENTATION_DRIFT.

## 5. PurchaseOrderLineAllocation authority

Entity: CafeChain/Models/Inventories/Procurement/PurchaseOrder.cs.

Fields: PurchaseAdviceLineId, PurchaseOrderBatchLineId, PurchaseOrderId, PurchaseOrderLineId, AllocatedBaseQuantity, AllocatedPackageQuantity, RowVersion.

Config: PurchaseOrderBatchConfiguration.cs.

- Base/package precision decimal(18,3).
- Check base/package positive.
- Index PA line, batch line, PO.
- Unique index PurchaseOrderLineId: mot PO line hien tai chi co mot allocation.
- FK Restrict toi PA line/PO/batch; batch-line cascade.

Ket luan: allocation la exact trace/link va quantity authority cho phan da dat mua, khong phai chi display. Schema hien tai khong cho mot PO line co nhieu PA line. Owner da chot giu restriction mot allocation cho moi PO line trong SC-01; mot PA line van duoc aggregate qua nhieu PO line.

Mot PA line co the co nhieu PO line ve mat model vi khong co unique tren PurchaseAdviceLineId, nhung batch service hien tai phan theo tung PO line. Can test/lock cho truong hop do.

## 6. Receipt confirm transaction

File: CafeChain/Application/Services/Inventories/BranchReceiptService.cs, ConfirmAsync.

    Load receipt + auth + row version
      -> begin DB transaction
      -> replay check CONFIRMED
      -> validate line, cost, quantity
      -> lock/validate PO line
      -> RestockFulfillmentPosting
      -> PurchaseOrderReceiptPosting
      -> resolve Ingredient/PreparedItem inventory
      -> lock StoreInventory theo id tang dan
      -> update inventory
      -> create InventoryCostLayer
      -> create InventoryTransaction BRANCH_RECEIPT_IN
      -> set receipt CONFIRMED
      -> evaluate stock alert trong transaction
      -> SaveChanges -> commit

Rejected khong tang PO Accepted, PA Accepted, ton hay FIFO.

Insertion point de xuat: ngay sau RegisterReceiptPostingAsync, truoc inventory write, goi PurchaseAdviceBackPostService trong cung transaction. Service phai tim exact allocation, lock allocation va PA line theo id tang dan, validate Accepted + Closed + incoming <= Allocated, tao Accepted posting idempotent, update aggregate va recompute status.

Khong dua back-post ra background sau commit. Neu back-post fail, receipt va inventory rollback cung nhau.

## 7. Close Remaining transaction

File: CafeChain/Application/Services/Inventories/PurchaseOrderService.cs, CloseLineRemainingAsync.

Current sequence:

1. BusinessOwner auth, reason bat buoc, RowVersion.
2. Begin transaction, lock PO line, check store/status/version.
3. Sum PurchaseOrderReceiptPosting.AcceptedBaseQuantity.
4. Remaining = OrderedBaseQuantity - accepted - ClosedRemainingQuantity.
5. Cong remaining vao ClosedRemainingQuantity, luu reason/actor/time.
6. SaveChanges, recalculate PO status, SaveChanges, commit.

Insertion point de xuat: sau khi lock va tinh remaining, truoc SaveChanges dau tien, resolve allocation, tao Closed posting idempotent, cap nhat PA Closed aggregate/status, sau do luu PO va PA trong cung transaction.

Close khong goi RestockFulfillmentPosting, khong tang Accepted, inventory hay FIFO. Close PO khong tu dong dong Restock theo Owner decision.

## 8. Allocation and deterministic ordering

Exact stored allocation phai duoc ton trong. Accepted/Closed khong duoc re-guess tu Restock quantity.

Neu mot PO line duoc mo rong cho nhieu PA line, thu tu:

1. NeededByDate som nhat.
2. SubmittedAtUtc som nhat.
3. AdviceNumber tang dan.
4. PurchaseAdviceLineId tang dan.

Lock thu tu de tranh deadlock: PO line id -> allocation id -> PA line id -> PA header id. Validate lai sau lock. Moi slice phai co posting/source key rieng; khong cong tat ca vao line dau tien.

## 9. Authority options

| Tieu chi | Option A: posting ledger | Option B: allocation fields |
| --- | --- | --- |
| Traceability | Accepted/Closed co source document/line, actor/time | Phai them source metadata vao allocation |
| Idempotency | Unique source key theo receipt/close | Can unique guard cho tung operation |
| Audit | Append-only posting history | Aggregate update de mat lich su neu khong them audit |
| Concurrency | Lock posting + aggregate | Lock allocation + aggregate |
| Query | Them join/sum, cached fields giam chi phi | Don gian hon |
| Backfill | Map tu receipt/close exact source | Kho phan biet source neu chi con aggregate |
| Codebase fit | Giong RestockFulfillmentPosting | Khac mo hinh hien tai |

RECOMMENDED_AUTHORITY = OPTION_A_PURCHASE_ADVICE_FULFILLMENT_POSTING_LEDGER_WITH_CACHED_AGGREGATES

- Ledger la source of truth cho Accepted/Closed.
- PA fields la cache de query/UI nhanh; update cung transaction voi posting.
- Reconciliation co the so sanh SUM(postings) voi cached fields.
- Posting type: ACCEPTED, CLOSED.
- Source type: BRANCH_RECEIPT_LINE, PO_CLOSE_REMAINING.

Option B chi la fallback neu Owner muon toi thieu table va chap nhan mat audit. Neu chon B, allocation can accepted/closed per operation, source reference, actor/time va unique guard. Plan chinh khong chon B.

## 10. Schema and migration impact

MIGRATION_REQUIRED = YES.

De xuat table PurchaseAdviceFulfillmentPostings:

- PurchaseAdviceFulfillmentPostingId identity.
- PurchaseAdviceLineId FK Restrict.
- PurchaseOrderLineAllocationId FK Restrict.
- PurchaseOrderLineId FK Restrict.
- BranchReceiptLineId nullable FK; bat buoc voi ACCEPTED.
- CloseOperationKey nullable unique; bat buoc voi CLOSED.
- PostingType ACCEPTED/CLOSED.
- Quantity decimal(18,3) positive.
- BaseUnitId FK Restrict.
- SourceDocumentType, SourceDocumentId, SourceDocumentLineId.
- ActorStaffId FK Restrict.
- CreatedAtUtc UTC.

Indexes/unique guards:

- PurchaseAdviceLineId + CreatedAtUtc.
- PurchaseOrderLineAllocationId + PostingType.
- BranchReceiptLineId + PostingType unique cho Accepted.
- CloseOperationKey + PostingType unique cho Closed.
- SourceDocumentType + SourceDocumentId + SourceDocumentLineId + PostingType + PurchaseAdviceLineId unique.
- PO line va PA line cho reconciliation.

Close Remaining hien khong co RequestKey; can them CloseOperationKey immutable tu command/ref, khong dung timestamp lam idempotency key.

Khong doi inventory table, khong doi BranchReceipt inventory authority. Khong tao migration trong phien plan.

## 11. Status recompute

Formulas Owner da khoa:

    RemainingToOrder = max(0, Requested - Allocated)
    RemainingToReceive = max(0, Allocated - Accepted - Closed)
    Unresolved = max(0, Requested - Accepted - Closed)

Invariants:

    Allocated <= Requested
    Accepted + Closed <= Allocated

RecomputeLineStatus:

1. Workflow terminal REJECTED/CANCELLED thi giu terminal header decision.
2. Allocated = 0 thi giu DRAFT/SUBMITTED/UNDER_REVIEW theo header workflow.
3. 0 < Allocated < Requested -> PARTIALLY_ALLOCATED.
4. Allocated >= Requested va Accepted + Closed = 0 -> FULLY_ALLOCATED.
5. Accepted + Closed > 0 va < Requested -> PARTIALLY_FULFILLED.
6. Accepted + Closed >= Requested -> COMPLETED.

RecomputeHeaderStatus:

1. Neu tat ca active lines Accepted + Closed >= Requested -> COMPLETED.
2. Neu co line Accepted + Closed > 0 -> PARTIALLY_FULFILLED.
3. Neu tat ca active lines Allocated >= Requested -> FULLY_ALLOCATED.
4. Neu co line Allocated > 0 -> PARTIALLY_ALLOCATED.
5. Neu chua allocate -> preserve DRAFT/SUBMITTED/UNDER_REVIEW.

Line status co the derived DTO; header Status persisted va transition history phai ghi khi status thay doi. Batch cancel reverse Allocated, khong reverse Accepted/Closed cua receipt da confirmed.

## 12. Stable errors

Them vao PurchaseAdviceErrorCodes hoac namespace rieng:

- PURCHASE_ADVICE_ALLOCATION_NOT_FOUND
- PURCHASE_ADVICE_ACCEPTED_EXCEEDS_ALLOCATION
- PURCHASE_ADVICE_CLOSED_EXCEEDS_ALLOCATION
- PURCHASE_ADVICE_BACKPOST_ALREADY_APPLIED
- PURCHASE_ADVICE_BACKPOST_STALE_VERSION
- PURCHASE_ADVICE_BACKPOST_CONFLICT
- PURCHASE_ADVICE_BACKPOST_TRACE_MISSING
- PURCHASE_ADVICE_STATUS_INCONSISTENT
- PURCHASE_ADVICE_BACKPOST_SOURCE_INVALID

Unique violation chi duoc xu ly thanh replay success neu source key va target trace khop.

## 13. Existing-data backfill

Khong chay trong phien nay.

Dry-run:

1. Load PA lines, allocations, PO receipt postings, BranchReceiptLines va PO close fields.
2. Join confirmed receipt posting -> PO line -> exact allocation -> PA line.
3. De xuat Accepted posting theo accepted receipt quantity.
4. Join PO ClosedRemaining voi exact allocation de de xuat Closed posting.
5. Tinh aggregate expected, so voi DB current values.
6. Xuat discrepancy: missing allocation, quantity vuot, duplicate source, aggregate drift.

Write phase sau Owner duyet: batch nho, idempotent theo source key, khong doan allocation. Untraceable -> MANUAL_REVIEW_REQUIRED, khong tu tao posting. Rerun no-op va co before/after totals.

Backfill tests:

- ConfirmedReceipt_CreatePostingOnce.
- UntraceableAllocation_ReportManualReview.
- Rerun_IsIdempotent.
- AggregateMatchesLedger.

## 14. Tests

Receipt:

- ReceiptAccepted_BackPostsToSingleAdviceLine.
- ReceiptAccepted_BackPostsAcrossMultipleAdviceLinesDeterministically.
- ReceiptRejected_DoesNotIncreaseAdviceAccepted.
- PartialReceipt_SetsPartiallyFulfilled.
- SecondReceipt_CompletesAdvice.
- ReceiptReplay_DoesNotDoubleBackPost.

Close:

- CloseRemaining_BackPostsClosedQuantity.
- CloseRemaining_DoesNotIncreaseAccepted.
- CloseRemaining_DoesNotFulfillRestock.
- CloseRemaining_WithAccepted_CompletesAdviceWithClosedPart.
- CloseRemaining_Replay_IsIdempotent.

Status/allocation:

- PartialAllocation_SetsPartiallyAllocated.
- FullAllocation_SetsFullyAllocated.
- AcceptedPlusClosed_CompletesAdvice.
- BatchCancel_ReversesAllocationAndRecomputesStatus.
- MultiLineHeader_DerivesCorrectStatus.

SQL concurrency:

- SqlServer_ConcurrentReceipts_DoNotOverAccept.
- SqlServer_ReceiveAndClose_OneWinner.
- SqlServer_ReplayReceipt_NoDuplicatePosting.
- SqlServer_ConcurrentStatusRecompute_IsConsistent.

Regression:

- BranchReceiptRestockIssue128Tests.
- PurchaseOrderPartialReceiptIssue178Tests.
- PurchaseAdviceIssue184Tests.
- PurchaseOrderBatchIssue186Tests.
- PurchaseAdviceBatchPoE2EIssue189Tests.
- SQL variants cua cac suite tren.

## 15. UI follow-up

Khong sua UI trong SC-01 plan step. Sau domain contract, PA list/detail/timeline hien Requested, Allocated, Accepted, Closed, RemainingToOrder, RemainingToReceive, Unresolved va source posting actor/time.

UI khong duoc hieu COMPLETED la chi nhan du; co the la nhan mot phan + dong phan con lai. Khong tu dong dong Restock khi PO completed.

Likely files follow-up: Areas/Admin/Views/AdminPurchaseAdvices/Index.cshtml, Details.cshtml, Application/DTOs/Admin/Procurement/PurchaseAdviceDtos.cs, AdminStatusDisplay mapping va UI tests.

## 16. GitHub issue

Created issue:

- #193 - Procurement: back-post receipt and close quantities to Purchase Advice
- https://github.com/TheSkibidi1712/CafeChain/issues/193

Issue body da co business context, broken flow, Owner decisions, authority options, status rules, deterministic allocation, transaction/idempotency, concurrency, backfill, schema/migration impact, UI follow-up, tests, definition of done, dependencies va protected files.

Related: #184 PA foundation, #186 batch/child PO, #189 PA/batch/PDF/receiving contracts, #190 InitialCreate workflow.

## 17. Implementation phases

### Phase 1 - Domain authority

- Files du kien: new PurchaseAdviceFulfillmentPosting entity/config, AppDbContext, constants/error codes, service/interface, DI.
- Schema: new ledger + indexes, migration required.
- Tests: posting uniqueness, traceability, aggregate rebuild.
- Risk: source key va current one-allocation-per-PO-line constraint.
- Gate: posting load/reconcile duoc, khong co inventory side effect.

### Phase 2 - Receipt back-post

- Files: BranchReceiptService, PurchaseOrderService receipt helper, back-post service/interface, SC01 tests.
- Schema: dung Phase 1.
- Tests: accepted/rejected/partial/replay/multi-allocation.
- Risk: rollback chung voi inventory/FIFO.
- Gate: receipt atomically cap nhat PO, PA cache/ledger, inventory va Restock.

### Phase 3 - Close Remaining

- Files: PurchaseOrderService.CloseLineRemainingAsync, request model neu can, back-post service, #178 extensions.
- Schema: CloseOperationKey/index neu can.
- Tests: closed-only, replay, concurrent receive.
- Risk: old close path thieu RequestKey.
- Gate: close khong tang Accepted, Restock hay inventory.

### Phase 4 - Status recompute

- Files: status policy, PurchaseAdviceService, constants, transitions, tests.
- Schema: uu tien derived line status; header persisted.
- Tests: partial/full allocation, partial/full fulfillment, accepted+closed, reject/cancel.
- Gate: formulas/invariants co mot authority.

### Phase 5 - Backfill/migration

- Files: dry-run/backfill service/command, discrepancy DTO/report, migration chi khi duyet.
- Schema: ledger/indexes, khong destructive migration.
- Tests: dry-run, manual review, rerun idempotent.
- Gate: dry-run totals duoc Owner duyet.

### Phase 6 - UI/timeline

- Files: PA DTO, Index/Details, status display, UI tests.
- Schema: none.
- Gate: Accepted/Closed/remaining hien rieng, Completed khong gay nham.

### Phase 7 - SQL concurrency

- Files: SQL fixtures/tests, khong suppress expected changes.
- Tests: receive/close race, replay, over-accept.
- Risk: moi truong audit hien tai loi SSPI.
- Gate: exactly-once posting va invariant duoi concurrency.

### Phase 8 - Runtime smoke

- Files: none by default.
- Environment: disposable/test DB only, khong dung DB chinh.
- Flow: PA -> batch -> approve -> partial receipt -> remaining receipt -> close remaining.
- Gate: trace PA -> allocation -> PO -> receipt/close query duoc.

## 18. Risks and Owner decisions

- Owner chot Option A: posting ledger la authority; Accepted/Closed tren PA line la cached aggregate.
- Giu unique PurchaseOrderLineId: mot PO line chi co mot allocation; mot PA line co the nam tren nhieu PO line.
- Close Remaining bat buoc stable RequestKey tu client; replay cung key/payload la no-op, khac payload la conflict.
- Line status derive; header status persisted va ghi PurchaseAdviceTransition.
- Existing data untraceable khong duoc tu doan; dry-run danh dau MANUAL_REVIEW_REQUIRED.
- Migration local chi de verify; schema chinh thuc regenerate InitialCreate trong #190.
- PA ledger khong thay the PurchaseOrderReceiptPosting, RestockFulfillmentPosting hay inventory writer.
- SQL Server concurrency verification co the phu thuoc Windows/SSPI cua moi truong local.

## 19. Implementation status

- Da them PurchaseAdviceFulfillmentPosting, unique source guards va cached aggregate recompute.
- Receipt accepted va Close Remaining da back-post atomically trong transaction hien co.
- Close Remaining da yeu cau stable RequestKey va co replay/conflict semantics.
- Header status va transition duoc recompute sau allocation/fulfillment.
- Da them dry-run backfill report, khong ghi du lieu va khong tao manual-review queue.
- Da them tests cho accepted, closed, replay, multi-PO aggregation, trace validation, manual PO, status va dry-run.
- Verification: Release build pass; SC-01 11/11; procurement regression khong phu thuoc SQL Server 69/69.
- Full suite: 1225 pass, 138 SQL Server integration tests khong khoi tao duoc do local SSPI context; khong co assertion failure trong nhom test chay duoc.
- Migration duoc tao local de inspect, khong stage/commit theo Owner decision.
- Khong thay doi inventory writer, Restock fulfillment, Docker, frontend hay SC-02.

SC01_IMPLEMENTED_AND_VERIFIED
