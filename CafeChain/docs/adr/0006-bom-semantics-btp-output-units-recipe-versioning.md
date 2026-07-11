# ADR-0006: BOM Semantics, BTP Output Units, and Recipe Versioning

| Field | Value |
|-------|--------|
| **Status** | Accepted |
| **Date** | 2026-07-11 |
| **Accepted Date** | 2026-07-11 |
| **Issue** | [#107](https://github.com/TheSkibidi1712/CafeChain/issues/107) |
| **Branch context** | `feature/POS` |
| **Related** | ADR-0001, ADR-0004, **ADR-0005 Accepted**, `4a8cfa2`, `513cbfc`, Phase 0 #106/#108/#109 |

---

## 1. Title

BOM / Recipe semantics for POS drink, topping, and BTP; **stable PreparedItem inventory identity** separate from Recipe versions; one-level POS stock vs recursive COGS; output units; yield; shared recipe resolution; order JSON snapshot.

## 2. Status

**Accepted** — domain decisions locked for Phase 1 implementation.  
This ADR document does not include schema/code; implementation and migration follow later issues/PRs.  
Locked: **PreparedItem** as BTP stock identity, live BOM component = **PreparedItemId** (not pinned child Recipe version for stock), **global physical unit conversion** ownership, staged migration cutover, output normalization, yield (no double-apply), shared exact resolver, ACTIVE/ARCHIVED, JSON order snapshot, backend overlap confirmation.

## 3. Date

2026-07-11 (draft + review); **Accepted Date: 2026-07-11**

## 4. Context

CafeChain uses `Recipe` / `RecipeDetail` for POS drink+size, topping, and BTP BOMs. ADR-0004 locked **one-level POS stock**. Code/`4a8cfa2` fail-close ingredient conversion. ADR-0005 locks package → base unit **cost** (not redefined here).

Gaps this ADR closes:

1. BTP stock today keys on **`StoreInventory.RecipeId`**, which collides with **versioned Recipe rows** (archive + new id on edit).  
2. BTP quantity meaning (batch vs physical output).  
3. Catalog vs deduction recipe selection mismatch.  
4. No order BOM snapshot.  
5. Ambiguous `YieldPercentage` vs planned output.  
6. Overlap BTP/raw without backend confirmation.  
7. Stale ADR-0004 “raw quantity” wording vs fail-closed code.

---

## 5. Current-state findings (code-verified)

### 5.1 Entities (as-is)

| Item | Finding |
|------|---------|
| `Recipe` | `DrinkId`/`SizeId`/`ToppingId`, `Status` Active\|Archived, `EffectiveDate`, `ParentVersionId`, `YieldPercentage`; **no** OutputQuantity/OutputUnitId; **no** PreparedItemId |
| `RecipeDetail` | Ingredient XOR ChildRecipe (DB check); unique per ingredient/child; qty+UnitId |
| `StoreInventory` | `IngredientId` XOR **`RecipeId`** (BTP) |
| `StockAlert` / `RestockRequest` | Optional `RecipeId` for BTP identity |
| `OrderDetail` | Drink/size/price/qty only — **no** recipe/snapshot |
| Admin types | POS / TOPPING / SUBRECIPE (VM only; no DB RecipeType) |

### 5.2 Recipe selection (as-is)

| Path | Rule |
|------|------|
| **Catalog** | Exact `DrinkId + SizeId + ToppingId`, Active+Status Active; **no** size-null fallback |
| **Deduction** | Exact size, then **fallback** `SizeId == null`; missing recipe → log + **soft skip** |
| **Topping** | `ToppingId` + `DrinkId == null` |

### 5.3 Deduction / COGS / production (as-is)

- POS: one-level Ingredient (convert fail-closed) or ChildRecipe raw qty × sold; no explode.  
- COGS: recursive depth 5 + cycle; conversion fail → ServiceResult failure; package price normalization is ADR-0005.  
- Production: hardcodes store, output = batches onto `StoreInventory.RecipeId`.  
- Update recipe: archive old, new Active + `ParentVersionId` (new `RecipeId` → **breaks** inventory identity if stock keyed by version id).

### 5.4 ADR-0004 stale note

Missing conversion “raw quantity” is **stale**. Law is fail-closed (`4a8cfa2`). This ADR reaffirms fail-closed for stock/qty conversion.

---

## 6. Problem statement

Using **Recipe version id** as BTP inventory identity means editing a BOM (new version row) orphans or splits stock, alerts, and restock against the wrong id. Batch-count inventory and silent size fallback compound operational risk. Historical orders recompute BOM from live Active recipes.

---

## 7. Definitions

| Term | Meaning |
|------|---------|
| **PreparedItem** | Stable BTP/prepared-good **inventory identity** (not a recipe version) |
| **Recipe (version)** | One immutable-ish BOM/production formula version linked to a sale identity or a PreparedItem |
| **POS drink recipe** | Formula for Drink+Size sale |
| **Topping recipe** | Formula for Topping sale |
| **BTP recipe version** | Formula that **produces** a PreparedItem |
| **One-level deduction** | Only top-level components of the resolved sale recipe move stock |
| **Recursive COGS** | Cost walk only; no stock mutate |
| **EstimatedBomCost / StoreOperationalCost / HistoricalOrderCogs** | Per ADR-0005 |

---

## 8. Decision

### 8.1 Stable BTP identity (locked)

**Separate:**

| Concept | Role |
|---------|------|
| **PreparedItem** | Stable stock-keeping identity for semi-finished goods |
| **Recipe version** | Production/prep formula that yields a PreparedItem (or defines POS/topping sale BOM) |

**Target entity:**

```text
PreparedItem
  PreparedItemId
  Code
  Name
  BaseUnitId      // canonical inventory unit for this BTP
  Active
```

**BTP Recipe version (target fields):**

```text
Recipe (version row)
  RecipeId
  PreparedItemId      // when category = BTP producer
  Version             // optional display/monotonic
  ParentVersionId
  OutputQuantity      // > 0 expected net output of this version run definition
  OutputUnitId
  Status              // ACTIVE | ARCHIVED (MVP)
  EffectiveFrom
  EffectiveTo?
  // POS/Topping: DrinkId/SizeId/ToppingId as today; PreparedItemId null
```

**Target `StoreInventory` identity:**

```text
IngredientId  XOR  PreparedItemId
```

**Do not** use Recipe **version** id as long-term BTP inventory identity.

#### Rejected weaker alternative: RecipeRootId / RecipeFamilyId as inventory identity

| Approach | Why rejected as primary design |
|----------|--------------------------------|
| `RecipeRootId` / family id on version chain | Still couples “formula family” to “stock item”; rename/split/merge of commercial BTP SKU is harder; alerts/restock language is “item” not “recipe root”; PreparedItem can outlive recipe rewrites cleanly |

Use **PreparedItem** unless implementation proves impossible (no evidence it is impossible; current pain is exactly RecipeId-as-stock).

#### Current → target migration impact (document only)

| Current | Remediation |
|---------|-------------|
| `StoreInventory.RecipeId` | Map each distinct BTP stock row to a **PreparedItem**; migrate qty into `PreparedItemId` |
| `StockAlert.RecipeId` | Remap to PreparedItem identity |
| `RestockRequest.RecipeId` | Remap to PreparedItem identity |
| `RecipeDetail.ChildRecipeId` | Live BOM: become **`PreparedItemId`** (stable SKU). Child **Recipe version** is **not** the live stock component identity |
| Existing production history keyed by RecipeId | Map via **explicit approved** ops mapping; **no name inference** |

**No entity/migration in this ADR task.** See §21 staged cutover.

### 8.1.1 Live BTP component reference (locked)

A **live** `RecipeDetail` that consumes a BTP references the stable:

```text
PreparedItemId
```

It does **not** require a pinned `ChildRecipeVersionId` for **stock deduction**.

**Target live RecipeDetail identity:**

```text
IngredientId?      // XOR
PreparedItemId?    // XOR
Quantity
UnitId
```

**Enforce XOR:** exactly one of `IngredientId` or `PreparedItemId`.

**Reasons:**

- POS consumes a **stable prepared-item SKU**.
- Inventory is held by **`PreparedItemId`**.
- Recipe **version** changes must not split or migrate existing BTP stock.
- Stock deduction is **independent** of which recipe version produced the existing stock on hand.

**Child Recipe version** is resolved only for:

- current **EstimatedBomCost** (which ACTIVE production formula currently defines that PreparedItem’s estimated cost);
- audit / explanation in admin UI;
- **sale-time snapshot** (historical freeze).

**At sale-time snapshot**, store when applicable:

| Snapshot field | Purpose |
|----------------|---------|
| `PreparedItemId` | Stable stock SKU consumed |
| `ResolvedChildRecipeId` / `ResolvedChildRecipeVersion` | Formula version used for estimate/audit at sale |
| `Quantity` | Component qty |
| `SourceUnitId` | Unit on the live BOM line |
| `NormalizedBaseQuantity` | Qty in PreparedItem.BaseUnitId (or ingredient base) |
| Cost snapshot metadata | When Phase cost implementation is available |

**Clarify immutability vs estimate drift:**

| Scope | Behavior |
|-------|----------|
| **Recipe version immutability** | Component identities (`IngredientId` / `PreparedItemId`) and quantities on that version row do not change after publish (archive + new version instead) |
| **Current EstimatedBomCost** | **May change** when the current resolved child production recipe version or cost source (ADR-0005) changes |
| **HistoricalOrderCogs** | **Never** changes — resolved child version and cost are **snapshotted** at sale |

**Rejected alternative:** mandatory `ChildRecipeVersionId` as the **stock** component identity (would re-couple stock to formula versions).

### 8.2 BTP output model (locked)

| Field | Meaning |
|-------|---------|
| `PreparedItem.BaseUnitId` | **Canonical inventory unit** for stock qty |
| `Recipe.OutputQuantity` | Expected **net** output for one production run definition of this version (`> 0`) |
| `Recipe.OutputUnitId` | Unit of that planned output; must be **physically convertible** to `PreparedItem.BaseUnitId` |

```
normalizedExpectedOutputInBase
  = PhysicalConvert(OutputQuantity, OutputUnitId → PreparedItem.BaseUnitId)

StoreInventory.AvailableQty  // always in PreparedItem.BaseUnitId
```

**Example:** PreparedItem Cold Brew `BaseUnit=ml`; Recipe `OutputQuantity=4.5`, `OutputUnit=l` → **4500 ml** inventory.

**Forbidden:** treat inventory as opaque **batch count** unless BaseUnit is explicitly a count UOM chosen as product policy (default physical content units).

### 8.3 Global physical unit conversion ownership (locked)

**Physical unit conversions belong to the Unit domain**, not to a specific Ingredient.

| Physical (Unit domain) | Not physical (do not model as universal conversion) |
|------------------------|------------------------------------------------------|
| kg ↔ g | bottle / bag / box as universal UOM |
| l ↔ ml | supplier package commerce (ADR-0005) |

**Recommend:**

```text
IPhysicalUnitConversionService
```

**Responsibilities:**

- Convert `decimal` quantity between **compatible physical** units  
- Validate **dimension** compatibility (mass vs volume mismatch → reject)  
- Reject zero / negative / invalid factors  
- **Never** require a fake `IngredientId`  
- **Never** treat bottle/bag/box as universal physical conversion  
- Support Ingredient, PreparedItem, Recipe output, and document flows  

**Target interaction with existing conversion:**

```text
IUnitConversionService  (ingredient-context, as today)
  may delegate to IPhysicalUnitConversionService for universal physical rules
  then apply ingredient-specific conversion only when genuinely required
    (e.g. product-specific non-universal factors — rare; not for kg↔g)
```

**Clarify:** current ingredient-context `IUnitConversionService` **does not fully solve BTP conversion**. BTP must not invent IngredientIds to call it.

**Temporary MVP restriction** (implementation constraint until generic physical conversion exists — **not** final target architecture):

1. Live `RecipeDetail.UnitId` for a **PreparedItem** component **must equal** `PreparedItem.BaseUnitId`.  
2. `Recipe.OutputUnitId` may differ from `PreparedItem.BaseUnitId` **only after** physical conversion support is available; otherwise output must be entered **in base unit**.  

**Final target:** physical conversion always available; component/output units may differ when convertible.

### 8.4 Yield semantics (locked)

| Concept | Owner | Rule |
|---------|--------|------|
| **OutputQuantity** | Recipe version | Authoritative **planned net output after expected standard loss** |
| **COGS per output base unit** | Costing | `totalInputCost / normalizedExpectedOutputQuantity` |
| **YieldPercentage** | Recipe | **Legacy / derived / informational** — **not** a second authoritative COGS factor |
| **Actual yield** | ProductionOrder (#106) | PlannedInput, ActualInput, ExpectedOutput, ActualOutput, Waste, ActualYieldPercentage |

**Do not** multiply or divide by `YieldPercentage` again in COGS if `OutputQuantity` already embeds expected net output after standard loss.

Plan remediation/deprecation of dual COGS use of `YieldPercentage` in Phase 1.

### 8.5 POS one-level deduction (locked)

| Top-level component | Stock movement |
|---------------------|----------------|
| Ingredient | `StoreInventory(IngredientId)` after convert to ingredient base (ADR-0005 conversion service) |
| Child BTP / PreparedItem | `StoreInventory(PreparedItemId)` after normalize to PreparedItem base unit |

No recursive explode. Inputs inside BTP consumed at **production** (#106).

### 8.6 Recursive COGS (locked)

- Recurse recipe versions / child structures for **money only**.  
- Max depth 5 + cycle detection (align existing).  
- Incomplete cost per ADR-0005 → not fake complete total.  
- Never mutates inventory.

Child cost allocation (when version has OutputQuantity):

```
lineCost =
  (componentQtyInPreparedBase / normalizedExpectedOutputOfChildVersion)
  × completeChildInputCost
```

(Equivalent: cost per output base unit × component base qty.)

### 8.7 Component rules (locked)

Each **live** detail targets **exactly one**:

- `IngredientId`, or  
- `PreparedItemId`  

(See §8.1.1 — not pinned child Recipe version for stock.)

Reject: both/neither; duplicate exact component; self-ref; cycle; max-depth overflow.

**Mixed** BTP + direct Ingredient: **allowed**.

**Overlap** (direct Ingredient also inside child BTP tree):

- **Warning**, not automatic hard-block.  
- Backend requires **`confirmIngredientOverlap = true`** on save.  
- Frontend-only confirm is **insufficient**.  
- Audit confirmation when audit infrastructure supports it.  
- POS still deducts **only top-level** listed components.

### 8.8 Recipe resolution (locked)

**Single shared deterministic resolver:**

```text
IActiveRecipeResolver
```

| Identity | Graduation MVP rule |
|----------|---------------------|
| Drink | **Exact** `DrinkId + SizeId` only — **no** implicit `SizeId` null fallback |
| Topping | Exact `ToppingId` |
| BTP production formula | Exact `PreparedItemId` → one Active/effective Recipe version |

If product later wants “all-size default” recipe: **model explicitly**, never silent null SizeId.

Catalog and deduction **must** call the same resolver → same version.

**Only one Active/effective version per identity.**

### 8.9 Version lifecycle (locked)

**Graduation MVP statuses:**

- `ACTIVE`  
- `ARCHIVED`  

**DRAFT deferred** unless implementation proves required.

Edit Active recipe:

1. Do **not** destructive overwrite components in place for history safety.  
2. Archive old version.  
3. Create new ACTIVE version with `ParentVersionId` chain.  
4. Uniqueness: one ACTIVE effective version per identity (DB + service).

**EffectiveFrom / EffectiveTo:**

- On publish: set `EffectiveFrom` (default now).  
- On archive/supersede: set prior version `EffectiveTo` (or rely on Status=ARCHIVED as end of effectiveness for MVP).  
- Resolver picks the single ACTIVE row for identity (MVP); EffectiveTo supports audit and future scheduling.

### 8.10 Order snapshot (locked — JSON MVP)

**Choose JSON snapshot for graduation MVP** (normalized tables = future).

**OrderDetail target fields:**

```text
RecipeId
RecipeVersion          // display/monotonic if needed
RecipeSnapshotJson
RecipeSnapshotSchemaVersion
RecipeResolvedAt
```

**Snapshot includes (per component when applicable):**

- parent recipe identity/version used for the sale line  
- component type  
- `IngredientId` or `PreparedItemId`  
- `ResolvedChildRecipeId` / `ResolvedChildRecipeVersion` for BTP components (cost/audit freeze)  
- quantity, `SourceUnitId`  
- `NormalizedBaseQuantity`  
- cost snapshot metadata **only when** Phase cost implementation is available  

Historical orders **never** rebuild BOM from current ACTIVE recipe.  
Current EstimatedBomCost **may** change when active child production version or cost source changes; historical does not.

### 8.11 Missing data behavior (locked)

| Case | Behavior |
|------|----------|
| Online missing active Recipe | Catalog item/size **unavailable** before payment |
| Paid deduction missing/invalid Recipe | **Durable failure** (#109); **no silent skip** |
| Offline | ADR-0001 blind selling; sync failure **persisted** (#109) |
| Missing conversion | Fail-closed; **no raw quantity** |
| Missing cost | COGS `IsComplete=false`; does **not** fail valid qty deduction |

### 8.12 Cost concepts

Do not redefine package cost — **ADR-0005**. EstimatedBomCost / StoreOperationalCost / HistoricalOrderCogs remain separate; historical uses sale snapshot.

---

## 9. Recipe categories (target)

| Category | Identity | Stock |
|----------|----------|--------|
| POS drink | DrinkId + SizeId | Components: Ingredient / PreparedItem |
| Topping | ToppingId | Same |
| BTP formula | PreparedItemId + version | Produces PreparedItem stock |

---

## 10–18. (Structured locks — see Decision §8)

Sections map to: identity (§8.1), components (§8.7), POS one-level (§8.5), COGS (§8.6), output (§8.2), yield (§8.4), version (§8.9), effective dates (§8.9), snapshot (§8.10), missing data (§8.11).

---

## 19. Authorization / store scope

- Recipe/PreparedItem master: admin BOM editors.  
- Overlap confirm: elevated recipe editor; backend flag mandatory.  
- Stock: store-scoped `StoreInventory`.  
- Snapshot: written at order commit for that order’s store.  
- Cost visibility: ADR-0005.

---

## 20. UI implications

1. BTP master = PreparedItem; versions listed under it.  
2. OutputQuantity + OutputUnit + base unit display.  
3. Overlap warning + server-enforced confirm flag.  
4. Same unavailable reasons online; no size fallback surprises.  
5. “Giá vốn ước tính” incomplete badge (ADR-0005).  
6. Do not show stock explode of BTP on POS.

---

## 21. Migration / data remediation (explicit — not now)

### 21.1 Staged cutover (locked plan)

1. **Add** `PreparedItem` and **nullable** new FKs (`PreparedItemId` on Recipe, StoreInventory, StockAlert, RestockRequest, RecipeDetail, etc.).  
2. **Create explicit approved legacy mapping** (ops-reviewed table/spreadsheet → seed mapping rows). **Do not** infer from Recipe display names.  
3. **Backfill:**  
   - `Recipe.PreparedItemId`  
   - `StoreInventory.PreparedItemId`  
   - `StockAlert.PreparedItemId`  
   - `RestockRequest.PreparedItemId`  
   - `RecipeDetail.PreparedItemId` (from legacy ChildRecipeId via mapping)  
4. **Temporary dual-read:**  
   - **`PreparedItemId` is authoritative** when present.  
   - Legacy `RecipeId` fallback allowed **only during cutover**.  
5. **All new writes** use `PreparedItemId`.  
6. **Validate** counts, quantities, and references (reject ambiguous mappings).  
7. **Remove legacy columns** in a **later cleanup** migration.  

**Do not allow permanent dual-read behavior.**

### 21.2 Likely schema items

- PreparedItem table; Recipe.PreparedItemId / OutputQuantity / OutputUnitId  
- StoreInventory / StockAlert / RestockRequest PreparedItem identity  
- RecipeDetail IngredientId XOR PreparedItemId  
- OrderDetail JSON snapshot fields  
- One ACTIVE Recipe per identity indexes  
- YieldPercentage remediation  
- Global physical unit conversion store + `IPhysicalUnitConversionService`  

**No migration is created in this ADR task.**

---

## 22. Consequences

### Positive

- Stock survives recipe version churn.  
- Clear ml/g inventory for BTP.  
- Catalog/deduction consistency.  
- Historical BOM integrity.  
- Yield not double-counted in COGS.

### Negative

- Large migration surface (inventory, alerts, restock).  
- Temporary dual-read period during cutover.  
- Physical conversion service or strict same-unit MVP limit.

---

## 23. Rejected alternatives

| Alternative | Why |
|-------------|-----|
| Recipe version id as BTP stock key | Breaks on version publish |
| RecipeRootId/FamilyId as primary stock identity | Weaker than PreparedItem SKU |
| Batch-count inventory by default | Not measurable content |
| Silent SizeId-null fallback | Wrong BOM risk |
| Frontend-only overlap confirm | Bypassable |
| YieldPercentage as second COGS factor with OutputQuantity net | Double-apply loss |
| Fake IngredientId for BTP conversion | Pollutes ingredient graph |
| Normalized snapshot tables in MVP | Defer; JSON MVP |
| DRAFT status in graduation MVP | Deferred |

---

## 24. Test requirements (future)

1. Stable PreparedItem inventory survives Recipe version change.  
2. BTP v1 and v2 consume the **same** PreparedItem inventory identity.  
3. Output 4.5 l normalizes to 4500 ml.  
4. No batch-count interpretation (default).  
5. Yield not double-applied to COGS.  
6. Catalog and deduction resolve the **same** exact Recipe version.  
7. No implicit size-null fallback.  
8. Only one Active/effective Recipe per identity.  
9. JSON snapshot unchanged after Recipe v2 created.  
10. Overlap requires **backend** confirmation flag.  
11. Missing Recipe after Paid → durable failure (#109).  
12. Existing one-level deduction remains.  
13. Recursive COGS never mutates inventory.  
14. Ingredient/PreparedItem XOR enforced.  
15. Migration/backfill rejects ambiguous mappings.  
16. Missing conversion fail-closed.  
17. Missing cost → incomplete COGS only.  
18. Offline sync no double-deduct.  
19. Cross-store auth unchanged.

---

## 25. Dependencies / follow-ups

| Item | Role |
|------|------|
| ADR-0005 | Package cost → BaseUnitCost; multi-package timing |
| **#107** | This ADR |
| #106 | Production actual yield; consume/produce PreparedItem |
| #108 | Restock/alert identity → PreparedItem |
| #109 | Durable paid deduction failure |
| Phase 1 | Entities, resolver, snapshot, uniqueness, COGS yield fix, physical conversion |

### 25.1 Multi-package dependency (locked)

**ADR-0006 does not select supplier packages.**

BOM costing consumes a **normalized BaseUnitCost** (or incomplete result) from the costing service defined by **ADR-0005**.

**Multi-package timing does not block** BOM identity, versioning, output units, or snapshots.

---

## 26. Remaining questions

**Removed as domain blockers (now decided):**

- Live BOM `PreparedItemId` vs pinned ChildRecipeVersionId for stock  
- Ownership of global physical conversion  
- Multi-package timing vs BOM identity  

**Implementation-level only (must not reopen domain semantics):**

1. Exact table/schema name for global physical conversion factors.  
2. Exact duration of dual-read release window.  
3. Whether DRAFT lifecycle is added later.  
4. JSON serializer / `RecipeSnapshotSchemaVersion` convention.

**Not open:** PreparedItem stock identity; live component = PreparedItemId; physical conversion ownership; MVP same-unit temporary limit; staged cutover; ADR-0005 cost; no size-null fallback; JSON snapshot; yield double-apply ban; backend overlap confirm.

---

## Examples (normative)

### 1 — Latte M
Espresso PreparedItem 30 ml + milk ingredient 180 ml → POS deducts those two lines only.

### 2 — Mixed BTP + direct
Tea base PI + tapioca PI + syrup ingredient → valid.

### 3 — Overlap
Direct sugar + BTP containing sugar → warn; save only with `confirmIngredientOverlap=true`; POS deducts top-level only.

### 4 — Cold brew output
PreparedItem base ml; Recipe 4.5 l → 4500 ml stock; never “1 batch” unless BaseUnit is batch by explicit policy.

### 5 — Versioning
Order A snapshot v1; v2 published; Order A unchanged; inventory still same PreparedItem.

---

## Decision summary (locked)

1. **PreparedItem** = stable BTP inventory identity; **Recipe** = versioned formula.  
2. Stock: `IngredientId` XOR `PreparedItemId`.  
3. **Live RecipeDetail BTP ref = PreparedItemId only** (not stock-pinned child Recipe version).  
4. Child Recipe version for **estimate/audit/snapshot only**.  
5. OutputQuantity + OutputUnitId on version; inventory always PreparedItem **base unit**.  
6. **IPhysicalUnitConversionService** owns kg↔g / l↔ml; `IUnitConversionService` may delegate; no fake IngredientId.  
7. **MVP temporary:** PreparedItem component UnitId must equal BaseUnitId until physical conversion ships.  
8. OutputQuantity = net planned output; **no** second YieldPercentage COGS factor.  
9. Actual yield on **ProductionOrder**.  
10. Shared **IActiveRecipeResolver**; exact drink+size; **no** size-null fallback.  
11. ACTIVE/ARCHIVED MVP; archive+new version; one Active per identity.  
12. Order **JSON snapshot** MVP (includes resolved child version at sale).  
13. Overlap: backend `confirmIngredientOverlap`.  
14. Staged migration dual-read temporary only.  
15. Multi-package (ADR-0005) does not block this ADR.  
16. Missing recipe/conversion/cost as §8.11.
