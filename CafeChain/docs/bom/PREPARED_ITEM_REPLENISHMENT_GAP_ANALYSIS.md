# PreparedItem Replenishment - Gap Analysis

## 1. Thang phân loại

- `SUPPORTED`: authority và invariant đã có.
- `PARTIAL`: có phần lõi nhưng thiếu projection, lifecycle hoặc integration.
- `MISSING`: chưa có dữ liệu/contract cần thiết.
- `WRONG_DOMAIN`: logic hiện tại dùng sai khái niệm cho nhu cầu mục tiêu.
- `FOLLOW_UP`: không chặn minimum implementation nhưng cần xử lý riêng.

## 2. Capability matrix

| Capability | Status | Bằng chứng hiện tại | Gap / quyết định |
|---|---|---|---|
| PreparedItem Store inventory | SUPPORTED | StoreInventory có StoreId + PreparedItemId canonical | Giữ PreparedItem là stock identity |
| Base UOM | SUPPORTED | PreparedItem.BaseUnitId, output normalizer | Không dùng Batch/package UOM |
| Available/on-hand/reserved | SUPPORTED | AvailableQty, ReservedQty; usable = hiệu | Cần đặt nhãn UI rõ để tránh hiểu AvailableQty đã trừ giữ chỗ |
| Negative stock | SUPPORTED | Policy âm tồn hiện có cho POS | Không áp dụng để bỏ qua kiểm tra actual production input |
| Store low threshold | SUPPORTED | StoreInventory.MinStockLevel | Nullable = chưa cấu hình |
| Threshold UI cho PreparedItem canonical | PARTIAL | StockAlertService hỗ trợ; InventoryThresholdService chỉ map Ingredient/Recipe | Bổ sung bounded projection PreparedItem |
| Target stock policy | MISSING | Không có field policy độc lập | Cần additive StoreInventory target field |
| Low-stock alert | SUPPORTED | StockAlert + unique active Store/item | Giữ alert là observation |
| Threshold boundary consistency | PARTIAL | Service dùng `<`; threshold UI dùng `<=` | Dùng chung service/read-model rule, không duplicate ở Razor |
| Alert reevaluation after production | MISSING | Acceptance không gọi StockAlertService | Reevaluate sau accepted output |
| Gross production need | WRONG_DOMAIN | Hiện dùng threshold - current | Phải dùng target - usable |
| Open production supply | PARTIAL | Allocation liên kết ProductionRun | Query phải xét run status và không credit inventory sớm |
| Cancelled-run supply release | MISSING | Cancel chỉ đổi ProductionRun status | Atomically release allocation/recompute sourcing |
| Net production need | PARTIAL | Có allocation/remaining demand nhưng chưa có read model tổng hợp | `max(target - usable - open coverage, 0)` |
| Duplicate alert prevention | SUPPORTED | Unique OPEN/CONFIRMED + retry | Giữ nguyên |
| Duplicate demand prevention | SUPPORTED | Unique active RestockRequest Store + PreparedItem | Không tạo demand table thứ hai |
| Duplicate planning prevention | PARTIAL | RequestKey + unique ProductionRun + active allocation | Cần sửa cancelled allocation và revalidate net need |
| Production source decision | SUPPORTED | PRODUCTION allocation + eligibility service | PreparedItem ưu tiên production khi CanProduce |
| PreparedItem external purchase | PARTIAL | CanPurchase check tồn tại nhưng supplier package không chứng minh được | Fail closed; follow-up riêng nếu business cần |
| Recipe selection before planning | SUPPORTED | shared CurrentRecipeResolver exact PreparedItem | Recipe current tại thời điểm plan |
| Recipe pinning | SUPPORTED | ProductionRun.RecipeId được ghi lúc Planned | Không silent switch sau pin |
| Production workflow | SUPPORTED | Plan/Release/Start/Record/Variance/Accept/Cancel | Tái sử dụng, không tạo state machine mới |
| Expected output semantics | SUPPORTED | Expected output chỉ là planning evidence | Không cộng inventory |
| Accepted output inventory | SUPPORTED | Full accepted output -> PRODUCTION_IN + FIFO layer | Giữ full physical truth |
| Demand fulfillment | SUPPORTED | RestockFulfillmentPosting, capped remaining | Underproduction còn demand; overproduction không spill |
| Re-evaluate target after fulfillment | PARTIAL | Request fulfillment có, stock alert reevaluation thiếu | Dùng cả explicit fulfillment và stock re-evaluation |
| Store scope | SUPPORTED | effective permission + Store scope ở Production/query boundaries | Test negative Store A/Store B |
| Threshold RBAC | PARTIAL | Permission có; service còn role allow-list legacy | Bỏ role allow-list trong implementation RBAC-safe |
| Production RBAC baseline | FOLLOW_UP | Seed và test của HEAD mâu thuẫn về SystemAdmin | Sửa baseline trước runtime acceptance |
| Traceability | PARTIAL | Alert -> Request -> Allocation -> Run -> transactions/fulfillment | Cần read model gom evidence, không duplicate audit |
| Concurrency | SUPPORTED | unique indexes, RowVersion, locks, RequestKey | Thêm test race evaluation/plan/cancel/accept |
| Recipe Workspace signal | MISSING | A4 chưa có replenishment projection | Chỉ thêm lightweight Store signal + deep link |

## 3. Purchase replenishment và production replenishment

Phân biệt đã có nền tảng domain:

```text
Ingredient + purchase authority
-> PURCHASE

PreparedItem + CanProduce + Store capability + current Recipe
-> PRODUCTION
```

PreparedItem không được tự động đưa vào PO. `CanPurchase` chỉ mở khả năng xem xét mua ngoài; hiện package authority chưa hỗ trợ PreparedItem nên hệ thống fail closed. Đây là hành vi an toàn.

## 4. Công thức còn thiếu

CURRENT:

```text
Suggested = max(MinStockLevel - UsableStock, 0)
```

Target:

```text
GrossNeed = max(TargetStock - UsableStock, 0)

OpenCoverage = active PRODUCTION allocations
               linked to non-terminal ProductionRuns

NetNeed = max(GrossNeed - OpenCoverage, 0)
```

`OpenCoverage` là planning coverage để chống tạo trùng, không phải tồn kho dự phóng và không được cộng vào on-hand.

## 5. Migration assessment

Để chỉ bù đến `MinStockLevel`, schema hiện tại đủ. Để đáp ứng đúng business case `Low threshold = 3 L`, `Target = 8 L`, schema hiện tại không đủ vì không có policy target bền vững theo Store + PreparedItem.

Kết luận:

```text
ADDITIVE MIGRATION REQUIRED
```

Migration tối thiểu chỉ bổ sung target stock nullable trên StoreInventory canonical. Không cần ProductionDemand table, RecipeIdentity hoặc production-state migration.
