# PreparedItem Replenishment - End-to-End Workflow

## 1. Happy path

```text
StoreInventory
  On hand 2 L
  Reserved 0 L
  Low 3 L
  Target 8 L
        |
        v
StockAlert
  usable 2 L < low 3 L
        |
        v
Replenishment evaluation
  gross need = 8 - 2 = 6 L
  open production coverage = 0 L
  net need = 6 L
        |
        v
StoreManager confirms Nhu cầu bổ sung 6 L
        |
        v
Source eligibility
  PreparedItem CanProduce
  Store capability exists
  current exact Recipe exists
        |
        v
StoreManager Plan
  ProductionRun Planned
  exact RecipeId pinned
  allocation PRODUCTION created atomically
        |
        v
StoreManager Release
        |
        v
ShiftSupervisor Start -> Record Actual
        |
        +---- variance outside tolerance ----> BusinessOwner Approve
        |
        v
StoreManager Accept Output
  actual input -> FIFO PRODUCTION_OUT
  full accepted output -> PRODUCTION_IN
  fulfillment -> capped remaining demand
        |
        v
Re-evaluate Stock / Alert / Net Need
```

## 2. Alert, demand và order không phải một object

```text
StockAlert
"Tồn đang thấp"

RestockRequest
"Cần bổ sung 6 L và đã cover bao nhiêu"

ProductionRun
"Thực hiện công thức nào, ai làm, thực tế ra sao"
```

Mỗi object giữ một trách nhiệm. Liên kết hiện có cung cấp traceability mà không cần duplicate state.

## 3. Open supply chống planning trùng

Ví dụ có target 8 L, usable 2 L và một allocation production 5 L:

```text
GrossNeed = 8 - 2 = 6 L
OpenCoverage = 5 L
NetNeed = 1 L
```

5 L chưa phải inventory. Nó chỉ là demand coverage để người dùng không lập thêm 6 L. Khi run bị Cancelled, coverage trở về 0 và net need trở lại 6 L.

## 4. Recipe timing scenarios

### Case A - Recipe đổi trước khi pin

```text
08:00 Need +6 L, chưa có ProductionRun
09:00 Recipe v4 trở thành current
10:00 StoreManager Plan
```

Kết quả: shared current resolver chọn v4 và ProductionRun pin v4.

### Case B - Recipe đổi sau khi pin

```text
08:10 ProductionRun Planned, pin Recipe v3
09:00 Recipe v4 trở thành current
```

Kết quả: run cũ tiếp tục dùng v3. Không silent switch. Nếu cần v4, StoreManager cancel run khi còn Planned/Released, allocation được release, sau đó plan run mới pin v4.

## 5. Underproduction

```text
Demand 6.0 L
Accepted output 4.8 L
```

- inventory credit: +4.8 L;
- fulfillment: +4.8 L;
- demand remaining: 1.2 L;
- reevaluation cho biết current stock và net need mới;
- StoreManager có thể plan phần còn lại theo current policy.

## 6. Overproduction

```text
Demand 6.0 L
Accepted output 6.4 L
```

- inventory credit: +6.4 L;
- fulfillment: tối đa 6.0 L;
- demand remaining: 0;
- 0.4 L dư là physical stock thật, không bị cắt và không tự gán request khác.

## 7. Consumption while production is open

Nếu POS tiếp tục tiêu thụ trong lúc run đang mở:

- usable stock giảm theo inventory authority;
- active allocation vẫn cover demand đã plan;
- read model tính lại gross need và net need;
- active request có thể nhận demand adjustment sau xác nhận, thay vì tạo request trùng;
- không tự tăng batch của run đã pin/đã release.

## 8. Cancellation và retry

### Cancellation

```text
Cancel Planned/Released run
-> preserve run history
-> release linked allocation
-> recompute request sourcing
-> allow safe replan
```

### Retry

- alert evaluation retry trả về active alert hiện có;
- demand create retry trả về active request hiện có;
- Production Plan retry dùng RequestKey/fingerprint;
- Accept retry trên Completed không ghi movement lần hai;
- fulfillment source unique không post hai lần.

## 9. Purchase exception

PreparedItem chỉ đi purchase path khi cả business capability và supplier contract được chứng minh. CURRENT code chưa có supplier package authority cho PreparedItem nên purchase phải fail closed. Không fallback sang PO chỉ vì production eligibility thất bại.

## 10. Traceability chain

```text
StockAlertId
-> RestockRequestId
-> RestockSourcingAllocationId
-> ProductionRunId + pinned RecipeId
-> ProductionRun transitions/actual inputs/output
-> InventoryTransaction + FIFO cost layers
-> RestockFulfillmentPosting
```

Chuỗi này trả lời được trigger, need, source, Recipe, actor, actual consumption, accepted output, remaining demand và inventory movement mà không duplicate audit payload.

