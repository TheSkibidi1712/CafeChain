# ADR-0005: Canonical Units, Purchase Packaging, and Base Unit Cost

| Field | Value |
|-------|--------|
| **Status** | Accepted |
| **Date** | 2026-07-11 |
| **Accepted Date** | 2026-07-11 |
| **Issue** | [#105](https://github.com/TheSkibidi1712/CafeChain/issues/105) |
| **Branch context** | `feature/POS` |
| **Related** | ADR-0001, ADR-0004, commit `4a8cfa2`, Phase 0 issues #106–#109 |

---

## 1. Title

Canonical inventory units, purchase packaging (`PackageQuantity` + content unit), and cost-per-base-unit for ingredients and estimated BOM cost.

## 2. Status

**Accepted** — domain decisions locked for Phase 1 implementation.  
This ADR document does not include schema/code; implementation follows in a later issue/PR.  
Locked: package structure, formulas (not `Convert(1, unit→base)`), MVP `PackageQuantity` on `IngredientSupplier`, three cost concepts, validation, and history snapshot rules.

## 3. Date

2026-07-11 (draft + review); **Accepted Date: 2026-07-11**

## 4. Context

CafeChain mixes base inventory units, purchase package pricing, recipe units, and display labels without a single costing rule.

After commit `4a8cfa2`, missing/invalid **physical** conversion fails closed on POS catalog, deduction conversion, and COGS conversion. That does **not** fix **price normalization**: package price is still used as if it were already cost-per-base.

BOM Builder can show **140.000 VND/Gram** for a 140.000 VND coffee package whose content unit is **kg**.

**ADR-0004 staleness:** still describes missing conversion as “use raw quantity.” Code at `4a8cfa2` rejects that. Amendment tracked under **#107**. This ADR forbids raw quantity for **cost** math.

---

## 5. Current-state findings (code-verified)

### 5.1 Entities

| Entity | Fields | Observed meaning |
|--------|--------|------------------|
| `Ingredient` | `BaseUnitId` | Canonical stock unit |
| `Unit` | `UnitCode`, `Name`, `Type` | g, kg, ml, l, bottle, pack, … |
| `UnitConversion` | Ingredient-scoped From/To qty | Physical (+ some bottle→ml seeds) |
| `IngredientSupplier` | `UnitId`, `CurrentPrice` | **No `PackageQuantity` today** |
| Unique index | `(IngredientId, SupplierId)` **unique** | **At most one row per ingredient–supplier pair** |
| `IngredientSupplierPriceHistory` | `Price`, `EffectiveDate`, `IsCurrent` | Price only — no package snapshot |
| `InventoryDocumentDetail` | `Quantity`, `UnitId`, `BaseQuantity`, `UnitPrice`, `CostPrice` | Entry vs base stock qty |
| `InventoryCostLayer` | qty + `UnitCost` | Import sets **per-base** unit cost |
| `RecipeDetail` | `Quantity`, `UnitId` | Recipe line |
| `StoreInventory` | `AvailableQty` | Base (ingredient) / BTP (#107) |

### 5.2 Seeds (illustrative)

| Ingredient | Base | Supplier UnitId | CurrentPrice | Conversion seed |
|------------|------|-----------------|--------------|-----------------|
| Coffee #1 | g | kg (2) | 140000 | 1 kg → 1000 g |
| Syrup #8 | ml | ml (3) | 250000 | also bottle→750 ml (ambiguous purchase model) |
| Sugar #6 | g | kg (2) | 22000 | 1 kg → 1000 g |

Ingredient **names** containing “1kg” / “750ml” are **not** structured package data.

### 5.3 Services / UI bug path

| Path | Behavior |
|------|----------|
| Import process | `baseUnitCost = lineTotal / BaseQuantity` — **correct per-base pattern** |
| `CalculateRecipeCogsAsync` | `detailCost = CurrentPrice × recipeQtyInBase` — **wrong** when price is per package |
| BOM Builder | `BaseCost = CurrentPrice`, label = **BaseUnit.Name** → package price shown as **/Gram** |
| BTP in builder | `BaseCost = 0` hardcoded → “Chưa có giá”; total can still look complete from other lines |

### 5.4 Exact root cause of 140.000 VND/Gram

1. Coffee package price **140.000** on content unit **kg** (seed).  
2. Controller maps that price into `BaseCost` while UI unit is **Gram** (base).  
3. JS: `@@ 140.000/Gram` and `line = grams × 140000`.  
4. Correct with locked formula: convert **package content** 1 kg → 1000 g; `140000/1000 = 140 VND/g`.

---

## 6. Problems

1. `CurrentPrice` is package money; consumers treat it as base-unit money.  
2. UI labels base unit while showing package price.  
3. No `PackageQuantity` — cannot represent 500 g bag or 750 ml bottle as “500 of unit g” / “750 of unit ml” without overloading conversion.  
4. Unique `(IngredientId, SupplierId)` ⇒ **one commercial offer per supplier relationship** today.  
5. Price history lacks package snapshot.  
6. Zero price can yield “complete” zero cost.  
7. Estimated / operational / historical costs are conflated.  
8. Bottle/bag as universal conversion UOMs confuses commercial packaging with physical measure.

---

## 7. Decision (locked)

### 7.1 CurrentPrice semantics

**`IngredientSupplier.CurrentPrice` is the price of one purchase package** for that supplier–ingredient offer.

It is **not** the price of one `Ingredient.BaseUnit` unless the package happens to be exactly one base unit (rare; still computed, never assumed).

### 7.2 Package structure (Graduation MVP)

Each active supplier offer carries:

| Field | Role | Constraint |
|-------|------|------------|
| `PackageQuantity` | Amount of **physical content** in the package | `decimal`, **required**, **> 0** |
| `UnitId` | **Physical content unit** of that quantity (kg, g, ml, …) | Required; not “marketing bottle” as universal UOM |
| `CurrentPrice` | Price of **that whole package** | ≥ 0; 0 ⇒ incomplete (see validation) |

**Examples (MVP encoding):**

| Package | PackageQuantity | UnitId | CurrentPrice |
|---------|-----------------|--------|--------------|
| Coffee 1 kg bag | **1** | **kg** | 140000 |
| Syrup 750 ml bottle | **750** | **ml** | 120000 |
| Coffee 500 g bag | **500** | **g** | (price of that bag) |

### 7.3 Formula (locked — not Convert(1, unit→base))

```
baseQuantityPerPackage
  = UnitConversionService.Convert(
      quantity: PackageQuantity,
      fromUnitId: IngredientSupplier.UnitId,   // content unit
      toUnitId:   Ingredient.BaseUnitId
    )

baseUnitCost
  = CurrentPrice / baseQuantityPerPackage

recipeQuantityInBaseUnit
  = UnitConversionService.Convert(
      quantity: RecipeDetail.Quantity,
      fromUnitId: RecipeDetail.UnitId,
      toUnitId:   Ingredient.BaseUnitId
    )

recipeLineCost
  = recipeQuantityInBaseUnit × baseUnitCost
```

**Rejected as universal formula:**

```
Convert(1, purchaseUnit → baseUnit)   // WRONG as sole model
```

That form cannot represent 500 g bags, 750 ml bottles, or any package whose content quantity is not “1 content-unit”.

Conversion must fail closed (missing/invalid factor) → cost **incomplete**, never raw qty.

### 7.4 MVP data model

**Extend `IngredientSupplier` with `PackageQuantity` (decimal, required > 0).**

- Keep `IngredientSupplier.UnitId` = **physical content unit** of the package content.  
- Keep `CurrentPrice` = **price of that package**.  
- MVP: **one active/default package per `IngredientSupplier` row**.

**Schema limitation (current code):**

```text
Unique index on (IngredientId, SupplierId)
```

⇒ At most **one** offer per ingredient–supplier pair. MVP accepts this: one default package per relationship. Multiple simultaneous packages from the same supplier require the **future** entity (below), not duplicate supplier rows under the current unique key.

**Do not** invent `PackageQuantity` from ingredient names (“1kg” in the title).

### 7.5 Multi-package future model (not MVP schema)

When the product needs multiple packages per supplier (500 g and 1 kg simultaneously):

```text
IngredientSupplierPackage
  - IngredientSupplierPackageId
  - IngredientSupplierId
  - PackageName
  - PackageQuantity      // > 0
  - UnitId               // physical content unit
  - CurrentPrice         // price of that package
  - IsDefault
  - Active
```

Same formulas, with package fields read from the selected/default package row.

**MVP does not add this table** unless implementation discovers `PackageQuantity` on `IngredientSupplier` is unsafe — inspection shows the opposite: unique (IngredientId, SupplierId) fits one package; multi-package is explicitly future.

### 7.6 Packaging vs physical units

| Physical `UnitConversion` | Packaging data |
|---------------------------|----------------|
| kg ↔ g | PackageQuantity + content UnitId + package price |
| l ↔ ml | Not “bottle/bag/box” as universal conversion graph nodes |

**Reason:** one “bottle” may be 500 ml, 750 ml, or 1 l. Commercial packaging is **not** a fixed physical unit of the chain.

Existing seeds that use bottle/can → ml are transitional; new design prefers **content quantity in ml/g** on the supplier package (e.g. 750 + ml) plus physical conversions only among true physical units.

### 7.7 Price validation (locked)

| Condition | Result |
|-----------|--------|
| Price **null** / missing | **Incomplete** |
| Price **zero** | **Incomplete / draft only** — must **not** yield a **complete** zero-cost recipe |
| Price **negative** | **Rejected** |
| PackageQuantity ≤ 0 or null | **Rejected** |
| Missing/invalid conversion | **Incomplete** |
| Fake completed COGS | **Forbidden** |

### 7.8 Three distinct cost concepts (locked)

| Concept | Vietnamese UI label (recommended) | Meaning |
|---------|-----------------------------------|---------|
| **A. EstimatedBomCost** | **Giá vốn ước tính** | Global recipe design estimate from active/default supplier package normalized to base unit |
| **B. StoreOperationalCost** | (store ops) | Store-context: latest confirmed import / current inventory cost layer |
| **C. HistoricalOrderCogs** | (order history) | Snapshot **at sale**; must **not** recompute from live supplier price |

These are **not** interchangeable sources. UI and APIs must label which concept is shown.

### 7.9 Cost-source hierarchy (locked)

**Global BOM estimate (EstimatedBomCost):**

1. Active **primary** supplier package with complete price + PackageQuantity + unit + conversion.  
2. Another active default supplier package **only if** product policy explicitly allows.  
3. Otherwise **incomplete**.

**Store operational cost (StoreOperationalCost):**

1. Current valid cost layer / confirmed import cost (per base).  
2. Normalized supplier package estimate as **explicitly labeled fallback** (not silent).  
3. **Incomplete**.

**Historical order (HistoricalOrderCogs):**

1. **Sale-time snapshot only** (#107).  
2. Never live `CurrentPrice`.

### 7.10 Computed vs stored BaseUnitCost (locked)

- **Source of truth for live estimate:** compute in backend from package quantity, content unit, price, conversion.  
- **Do not** make a mutable cached `BaseUnitCost` column on supplier the source of truth.  
- **Do persist snapshot values** when recording history / import / (future) order cost:
  - supplier price history  
  - confirmed import detail / cost layer (`UnitCost` per base already)  
  - future order cost snapshot  

### 7.11 Price history recommendation (future)

Snapshot at least:

- SupplierId  
- IngredientId  
- PackageQuantity  
- PackageUnitId (`UnitId`)  
- PackagePrice  
- BaseQuantity (`baseQuantityPerPackage` at that time)  
- CalculatedBaseUnitCost  
- EffectiveAt  

Changing `CurrentPrice` later **must not** rewrite historical rows.

---

## 8. Canonical terminology

| Term | Definition |
|------|------------|
| Base unit | `Ingredient.BaseUnitId`; unit of ingredient `StoreInventory` qty |
| Package | One commercial buy unit with content amount + content unit + price |
| PackageQuantity | Content amount in the package (physical measure count) |
| Package content unit | `IngredientSupplier.UnitId` (MVP) — kg, g, ml, … |
| CurrentPrice | Price of **the package** |
| Base quantity per package | Package content converted to base unit |
| Base unit cost | Price / base quantity per package |
| EstimatedBomCost | Global design estimate (“Giá vốn ước tính”) |
| StoreOperationalCost | Store layer / import cost |
| HistoricalOrderCogs | Frozen at sale |

---

## 9. Data model recommendation (final)

### MVP (Phase 1)

```text
IngredientSupplier
  + PackageQuantity  decimal NOT NULL, check > 0
  UnitId             // physical content unit (existing)
  CurrentPrice       // package price (existing semantics locked)
  Unique (IngredientId, SupplierId)  // one package offer per pair
```

Backfill rules: **only** when explicit structured unit/conversion data proves package size; **never** parse names like “1kg”.

### Future

`IngredientSupplierPackage` as in §7.5 when multi-pack per supplier is required.

---

## 10. Formulas (final)

```
baseQuantityPerPackage
  = Convert(PackageQuantity, UnitId → BaseUnitId)

baseUnitCost
  = CurrentPrice / baseQuantityPerPackage
  // only if price > 0, package qty > 0, conversion complete

recipeLineCost
  = Convert(RecipeDetail.Quantity, RecipeDetail.UnitId → BaseUnitId)
    × baseUnitCost
```

Import (unchanged pattern, actual cost):

```
baseUnitCost_actual = lineTotal / BaseQuantity
```

---

## 11. Examples (updated)

### Coffee — 1 kg package @ 140.000

| Step | Value |
|------|-------|
| PackageQuantity | 1 |
| UnitId | kg |
| CurrentPrice | 140000 |
| baseQuantityPerPackage | Convert(1, kg→g) = **1000** |
| baseUnitCost | 140000/1000 = **140 VND/g** |
| Recipe 18 g | **2520 VND** |

### Syrup — 750 ml package @ 120.000

| Step | Value |
|------|-------|
| PackageQuantity | 750 |
| UnitId | ml |
| CurrentPrice | 120000 |
| baseQuantityPerPackage | Convert(750, ml→ml) = **750** |
| baseUnitCost | **160 VND/ml** |
| Recipe 15 ml | **2400 VND** |

### 500 g package

| Step | Value |
|------|-------|
| PackageQuantity | **500** |
| UnitId | **g** |
| baseQuantityPerPackage | 500 |
| baseUnitCost | price / 500 |

### Missing price / conversion / package

| Result |
|--------|
| `IsComplete = false` |
| Warnings listed |
| **No authoritative total COGS** |
| Zero price ≠ complete zero-cost recipe |

---

## 12. Validation rules (final)

| Rule | Action |
|------|--------|
| PackageQuantity null or ≤ 0 | Reject |
| UnitId missing | Reject / incomplete |
| CurrentPrice null | Incomplete |
| CurrentPrice = 0 | Incomplete/draft only — not complete COGS |
| CurrentPrice < 0 | Reject |
| Conversion fail | Incomplete |
| Present complete total with any incomplete line | Forbidden |
| Name-based package inference | Forbidden |

---

## 13. Cost-source hierarchy (final)

See §7.9. Summary:

- **Estimate:** primary complete package → optional other supplier → incomplete.  
- **Store ops:** cost layer/import → labeled supplier fallback → incomplete.  
- **History:** sale snapshot only.

---

## 14. Authorization / store scope

- Master supplier package + estimated BOM: ingredient/supplier admin roles.  
- Store operational cost: store-scoped layers/imports.  
- POS devices: do not expose detailed supplier cost unless product explicitly allows (default admin-only for detailed VND cost).

---

## 15. UI implications

1. Show package price as package (e.g. 140.000 VND / 1 kg), not as VND/g unless computed.  
2. Subtitle unit cost must use **base unit** after normalization (140 VND/g).  
3. Label estimated total **“Giá vốn ước tính”** with incomplete badge when needed.  
4. Do not mix EstimatedBomCost badge with store operational cost without label.  
5. Quantity inputs always show unit.

---

## 16. Migration / data-remediation impact

### Likely Phase 1 migration (not created now)

1. Add `IngredientSupplier.PackageQuantity` (`decimal`, check `> 0`).  
2. **Backfill carefully:**
   - Prefer rows where purchase `UnitId` already has conversion to base and commercial intent is “1 content-unit package” (e.g. 1 kg) → `PackageQuantity = 1` **only when structured data supports it**.  
   - For content already in base unit with known bottle seeds, ops may set 750 ml **only** if conversion/seed proves content — **not** from the word “750ml” in the name alone.  
   - If unknown → leave for manual ops; mark offers incomplete until filled.  
3. Validate package/unit/price after backfill.  
4. Optional later: `IngredientSupplierPackage` table.  
5. Later: extend price history snapshot columns.

### Explicit non-goals now

- No migration file in this task.  
- No production SQL.  
- No backfill invented from ingredient display names.

---

## 17. Consequences

### Positive

- Stops 140.000 VND/Gram class bugs.  
- Represents 500 g / 750 ml packages correctly.  
- Separates estimate vs store vs historical cost.  
- Aligns with import per-base cost layers.

### Negative

- Requires Phase 1 migration + BOM/COGS code changes.  
- Demo food-cost numbers will change.  
- One package per supplier until multi-package entity.  
- Manual backfill for ambiguous syrup/bottle seeds.

---

## 18. Rejected alternatives

| Alternative | Why rejected |
|-------------|--------------|
| `Convert(1, unit→base)` as universal package size | Cannot model 500 g / 750 ml packages |
| `CurrentPrice` means per base unit | Contradicts seed and D1 |
| Parse “1kg” from name | Unreliable |
| MVP multi-package table immediately | Unique (IngredientId, SupplierId) + PackageQuantity sufficient for graduation MVP |
| Bottle as global physical UOM without content qty | One bottle ≠ fixed volume |
| Mutable cached BaseUnitCost as SoT | Stale when price/package changes |
| Zero price ⇒ complete 0 COGS | Financial false confidence |

---

## 19. Test requirements (Phase 1)

1. 1 kg → 1000 g.  
2. 1 l → 1000 ml.  
3. Package 1 kg @ 140000 → 140 VND/g; 18 g → 2520.  
4. Package 750 ml @ 120000 → 160 VND/ml; 15 ml → 2400.  
5. PackageQuantity 500, UnitId g → base qty 500.  
6. Zero/negative PackageQuantity rejected.  
7. Negative price rejected; zero price incomplete (not complete zero recipe).  
8. Missing conversion → incomplete.  
9. Missing price → incomplete.  
10. BOM Builder never shows package price as price/g.  
11. Confirmed import still writes correct per-base cost layer.  
12. History snapshot unchanged when CurrentPrice changes (when history extended).  
13. Unauthorized roles cannot read detailed supplier cost if policy denies.

---

## 20. Follow-up issues

| Issue | Role |
|-------|------|
| #105 | This ADR |
| #107 | BOM/BTP/snapshot; ADR-0004 raw-qty amendment |
| #106 | Preprocessing uses Convert + base units |
| #108 | Receipt BaseQuantity / package on documents |
| #109 | Deduction failure includes conversion/cost taxonomy |
| Phase 1 | `PackageQuantity` migration, cost service, BOM/COGS UI fix |

---

## 21. Open questions

1. Exact backfill policy for existing syrup rows (UnitId=ml, price 250000, bottle conversion 750) — ops choice, not name parse.  
2. Whether secondary non-primary suppliers are allowed in estimate hierarchy (policy flag).  
3. Yield adjustment formula placement (after base unit cost only) — confirm with BOM UI owners in Phase 1.  
4. When to schedule `IngredientSupplierPackage` (first multi-store supplier demand).

**Not open:** package formula, CurrentPrice semantics, three cost concepts, MVP `PackageQuantity` column, rejection of `Convert(1,…)` as universal model.

---

## Decision summary (locked)

1. `CurrentPrice` = **package price**.  
2. MVP package = **`PackageQuantity` + `UnitId` (content) + `CurrentPrice`**.  
3. `baseQuantityPerPackage = Convert(PackageQuantity, UnitId → BaseUnitId)`.  
4. `baseUnitCost = CurrentPrice / baseQuantityPerPackage`.  
5. One package per `IngredientSupplier` (unique IngredientId+SupplierId).  
6. Multi-package = future `IngredientSupplierPackage`.  
7. Physical conversion ≠ packaging commerce.  
8. EstimatedBomCost / StoreOperationalCost / HistoricalOrderCogs are separate.  
9. Zero/missing price ⇒ incomplete, not complete zero COGS.  
10. Compute live for estimates; snapshot for history/import/orders.  
11. Implementation and migration are Phase 1 follow-up; this ADR only freezes domain decisions.
