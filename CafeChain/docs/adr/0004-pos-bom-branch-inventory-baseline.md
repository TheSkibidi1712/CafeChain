# ADR-0004: POS BOM và Branch Inventory Baseline (Kho chi nhánh)

Status: Accepted  
Issue: [#95](https://github.com/TheSkibidi1712/CafeChain/issues/95)  
Date: 2026-07-10  
Related: ADR-0001 (Blind Selling + Negative Inventory), ADR-0002 (ClientOrderId idempotency), Issue #86 inventory deduction guardrails

## Context

Module **Kho chi nhánh / Stock Alert** (#96–#102) cần một baseline rõ ràng về:

- Cách POS trừ kho theo BOM (Bill of Materials).
- Cách POS đánh giá availability món bán.
- Identity tồn kho chi nhánh trên `StoreInventory`.

Trước khi thiết kế `StockAlert` / `MinStockLevel`, team phải khóa hành vi hiện tại để tránh “đoán” BOM đệ quy sai.

## Decision

### 1. POS Inventory Deduction = **một tầng (one-level)** — STRICT OPTION B

Service: `InventoryDeductionService`  
Entry: `DeductStockForCommittedOrderAsync` (sau order Completed + Paid) / `DeductStockForOrderAsync`.

Với mỗi `RecipeDetail` của recipe món (hoặc topping):

| Loại detail | Hành vi trừ kho |
|-------------|-----------------|
| `IngredientId` có giá trị | Trừ `StoreInventory` khóa `(StoreId, IngredientId)`, sau **unit conversion** về `Ingredient.BaseUnit` khi có. |
| `ChildRecipeId` có giá trị (BTP) | Trừ `StoreInventory` khóa `(StoreId, RecipeId = ChildRecipeId)`. **Không** bóc tách sang nguyên liệu lá của child recipe. |

Comment trong code: *“STRICT OPTION B: Xuất thẳng Bán Thành Phẩm / Nguyên Liệu, KHÔNG bóc tách đệ quy”*.

**Không** rewrite deduction sang recursive BOM trừ khi có quyết định sản phẩm / ADR mới.

### 2. Identity tồn kho chi nhánh

`StoreInventory`:

- `StoreId` (chi nhánh)
- `IngredientId?` **hoặc** `RecipeId?` (BTP / semi-finished)
- `AvailableQty`, `ReservedQty`, `MaxNegativeQty?`

Unique (filtered): `(StoreId, IngredientId)` và `(StoreId, RecipeId)`.

**StockAlert (tương lai #97) phải target cùng identity:**

- `(StoreId + IngredientId)` **hoặc**
- `(StoreId + RecipeId)`

Chỉ alert nguyên liệu thô sẽ **sai** vì BTP cũng là hàng tồn POS.

### 3. Size recipe và topping recipe

**Drink + size (deduction — `GetActiveRecipeAsync`)**

1. Tìm recipe active: `DrinkId` + `SizeId` + `ToppingId == null`.
2. Fallback: `DrinkId` + `SizeId == null` + `ToppingId == null`.
3. Nếu không có recipe → log warning, **bỏ qua** trừ kho phần drink đó (đơn vẫn paid / soft skip).

**Topping (deduction)**

- Mỗi topping trên sold item: recipe `ToppingId` + `DrinkId == null`.
- Trừ theo details one-level giống drink.
- Quantity topping = `sold line Quantity` (nhân theo số ly), không có field qty topping riêng trên path này.

**Unit conversion**

- Chỉ áp dụng khi `detail.IngredientId` có giá trị → quy về `Ingredient.BaseUnit`.
- Path `ChildRecipeId`/BTP: dùng `detail.Quantity * soldQuantity` **raw**, không convert unit.

### 4. POS catalog availability (online) — cùng mô hình one-level

`POSCatalogController.HasSufficientRecipeInventoryAsync`:

- Load recipe **exact match**: `DrinkId` + `SizeId` + `ToppingId` (active + Status Active).
- **Khác deduction:** availability **không** fallback `SizeId == null` khi thiếu size-specific recipe → `MissingRecipe`.
- Với mỗi detail: so sánh `AvailableQty` với required (unit conversion **chỉ** cho ingredient).
- Lookup tồn: `(StoreId, IngredientId, RecipeId)` với `RecipeId = detail.ChildRecipeId` khi BTP.
- Status: `MissingRecipe` | `MissingInventory` | `InsufficientStock` | `TemporarilyUnavailable`.
- Món available nếu **ít nhất một size** đủ kho.

Availability **không** đệ quy ChildRecipe; kiểm tra tồn BTP qua `RecipeId`.

### 5. COGS có thể đệ quy — **khác** deduction

`CalculateRecipeCogsAsync` / `CalculateRecipeCogsInternalAsync`:

- Đệ quy `ChildRecipeId` với `MAX_BOM_DEPTH = 5` + cycle guard.
- Dùng để tính giá vốn, **không** dùng để trừ kho POS.

| Path | Recursive? | Mục đích |
|------|------------|----------|
| COGS | Có (≤ 5 tầng) | Costing / giá vốn |
| POS deduction | Không | Xuất kho bán hàng |
| POS availability | Không | Disable món online |

### 6. Guardrails cho commit/idempotency

- Chỉ trừ khi order `Completed` + `Paid` (khi có `referenceOrderId`).
- Idempotent: đã có `InventoryTransaction` với `ReferenceOrderId` + type `SALES_DEDUCTION` → không trừ lại.
- Cho phép `AvailableQty < 0` (ADR-0001); ghi `NEGATIVE_CONFIRMED` khi âm.

### 7. Auto-create inventory row

`GetOrCreateInventoryItem`: nếu chưa có row → tạo với `AvailableQty = 0` rồi trừ (có thể âm ngay).  
Ảnh hưởng StockAlert: row 0 + min threshold cấu hình sau này có thể sinh alert ồn — #97 cần filter cẩn thận.

## Consequences

### Positive

- Khớp vận hành kho quán: BTP nhập/tồn riêng, không bắt buộc nổ BOM lá lúc bán.
- StockAlert design đơn giản: mirror `StoreInventory`.
- Guardrail tests (#86, #95) khóa hành vi.

### Negative / risks

1. **Missing recipe** → skip deduction, under-deduct im lặng (chỉ log).
2. **Missing unit conversion** → dùng raw quantity → qty sai.
3. **Offline blind selling** (ADR-0001) → trừ kho lúc sync, có thể âm.
4. **Auto-create qty 0** → nhiễu alert nếu auto LOW/OUT không có rule.
5. **COGS ≠ deduction** → dev dễ nhầm khi implement alert “theo full BOM”.

## Guardrail rules cho issue #96–#97

### #96 Kho chi nhánh (read-only)

- List **mọi** `StoreInventory` của store: cả Ingredient và Recipe/BTP.
- Không mutate stock.
- Nếu chưa có min threshold: hiển thị *“Chưa cấu hình ngưỡng tối thiểu”* (không tự bịa min).

### #97 Stock Alert

- Key unresolved alert: `(StoreId, IngredientId)` XOR `(StoreId, RecipeId)`.
- **Không** auto LOW_STOCK khi min null.
- OUT / LOW dựa trên qty của **chính row** đó, không expand ChildRecipe.
- Hook sau deduction/sync/document phải idempotent với rule dedupe.

### Không làm trong baseline

- Đổi deduction sang recursive.
- Implement StockAlert / RestockRequest / notification UI.
- Migration schema mới (trừ khi issue sau approve).

## Evidence in codebase

| Thành phần | File |
|------------|------|
| Deduction one-level | `Application/Services/Inventories/InventoryDeductionService.cs` |
| Availability one-level | `Controllers/Api/v1/POSCatalogController.cs` |
| Recipe model | `Models/Drinks/Recipe.cs`, `RecipeDetail.cs` |
| Stock model | `Models/Stores/StoreInventory.cs` |
| Transaction | `Models/Inventories/Transactions/InventoryTransaction.cs` |
| Blind selling | `docs/adr/0001-blind-selling-negative-inventory.md` |
| Tests #86 | `CafeChain.Tests/POSInventoryDeductionGuardrailsIssue86Tests.cs` |
| Tests #95 | `CafeChain.Tests/POSBomBranchInventoryBaselineIssue95Tests.cs` |

## Decision summary (locked)

1. POS deduction / availability = **one-level**.
2. Ingredient → `StoreInventory.IngredientId`.
3. ChildRecipe/BTP → `StoreInventory.RecipeId`.
4. COGS may recurse; POS stock paths do not.
5. Future StockAlert targets **StoreInventory identity**, both Ingredient and Recipe.
