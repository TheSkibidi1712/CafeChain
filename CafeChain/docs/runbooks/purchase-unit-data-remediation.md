# Purchase And Unit Data Remediation Runbook

**Issue:** [#113](https://github.com/TheSkibidi1712/CafeChain/issues/113)
**Checkpoint A:** Read-only audit infrastructure (this document + audit command)
**Status:** Checkpoint A delivers audit only — **no data remediation applied**

---

## 1. Purpose

Provide a controlled, auditable process to discover incomplete or invalid **supplier package metadata** and **primary-supplier configuration** that prevent **EstimatedBomCost** (#117) from returning COMPLETE.

This runbook:

- forbids deriving package size from `Ingredient.Name`;
- separates **read-only audit** from **mutation** (later checkpoints);
- never rewrites stock, inventory transactions, or operational cost layers.

---

## 2. Binding dependencies

| Artifact | Role |
|----------|------|
| ADR-0005 | Package price ≠ base-unit cost; no name inference |
| ADR-0006 | Recipe/BTP identity; output contract separate from this remediation |
| #110 | Physical unit conversion |
| #111 | `PackageQuantity` on `IngredientSupplier` |
| #117 | `IEstimatedBomCostService`, `CostIssueCodes`, COMPLETE/INCOMPLETE |
| #113 | This remediation verification process |

Local migrations (uncommitted policy): keep the migration pile; do not clean or commit snapshots as part of #113 audit.

---

## 3. Safety rules

1. **Checkpoint A is read-only.** No `SaveChanges`, no seed edits, no SQL UPDATE executed.
2. **Never parse `Ingredient.Name`** for PackageQuantity, unit, or price meaning (no “750ml”, “1kg”, “500g” hacks).
3. **Never invent** package values to force COMPLETE.
4. **Do not** rewrite `StoreInventory`, `InventoryTransaction`, `InventoryCostLayer`, Orders, Payments.
5. **Do not** delete bottle/can/pack conversion rows in audit phase.
6. **Do not** map Recipe ↔ PreparedItem here (#115 / #114).
7. Secrets/connection strings **must not** appear in this document or committed JSON samples.
8. Production changes require backup, dry-run, reviewed mapping, transaction, row counts, rollback.

---

## 4. No Ingredient.Name inference

Application audit classification uses only structured fields:

- `IsPrimary`, `Active`, `CurrentPrice`, `PackageQuantity`, `UnitId`
- `Ingredient.BaseUnitId`
- `#117` costing results / issue codes

Display names may appear in reports for human readability **only**.

---

## 5. Audit command usage

Development-only entry (does not start the web server):

```bash
# From repository root (or CafeChain project directory)
dotnet run --project CafeChain -- audit-purchase-units

# Custom JSON path
dotnet run --project CafeChain -- audit-purchase-units --out ./docs/runbooks/reports/purchase-unit-audit-baseline.json
```

Requirements:

- Valid `appsettings.json` connection (local) — not documented here.
- Migrations applied so schema includes `PackageQuantity` and related tables.

The command:

- builds a **Host** with DB + application services;
- calls `IPurchaseUnitAuditService.RunAuditAsync()`;
- prints a console summary;
- writes machine-readable JSON;
- **never mutates** database rows.

---

## 6. JSON report location / format

Default path (if `--out` omitted):

```text
CafeChain/docs/runbooks/reports/purchase-unit-audit-{yyyyMMdd-HHmmss}.json
```

Schema version: `113.A.1`

Top-level fields:

| Field | Meaning |
|-------|---------|
| `GeneratedAtUtc` | Report timestamp |
| `Mode` | Always `ReadOnly` in Checkpoint A |
| `Offers` | Per `IngredientSupplier` classification |
| `Primaries` | Per-Ingredient primary-offer audit |
| `PriceHistories` | Per-offer history health |
| `Recipes` | Active recipe EstimatedBomCost snapshot |
| `Summary` | Counts for triage |

Enums are serialized as strings (e.g. `Complete`, `BusinessDecisionRequired`).

---

## 7. CostIssueCodes interpretation (#117)

Reuse existing codes (do not invent parallel costing codes):

| Code | Meaning |
|------|---------|
| `MISSING_PACKAGE_QUANTITY` | Primary/package lacks `PackageQuantity` |
| `INVALID_PACKAGE_QUANTITY` | Quantity ≤ 0 |
| `ZERO_PACKAGE_PRICE` | Price ≤ 0 (not COMPLETE free stock) |
| `MISSING_PACKAGE_UNIT` / `INACTIVE_PACKAGE_UNIT` | Unit missing/inactive |
| `MISSING_UNIT_CONVERSION` | Package or line unit cannot convert |
| `MISSING_SUPPLIER_OFFER` | No exactly-one Active primary usable for cost |
| `MULTIPLE_PRIMARY_SUPPLIERS` | >1 Active primary |
| `REJECTED_PACKAGING_UNIT` | bottle/can/pack as content unit |
| `LEGACY_CHILD_RECIPE_WITHOUT_OUTPUT` | ChildRecipe lacks PreparedItem output |
| `RECIPE_CYCLE` / `MAX_DEPTH_EXCEEDED` | Graph safety |

Audit-only codes (`PurchaseUnitAuditIssueCodes`):

| Code | Meaning |
|------|---------|
| `PRICE_HISTORY_MISSING_CURRENT` | No `IsCurrent` history row |
| `PRICE_HISTORY_MULTIPLE_CURRENT` | >1 `IsCurrent` |
| `PRICE_HISTORY_SNAPSHOT_MISMATCH` | Current history ≠ offer package/price |
| `PRICE_HISTORY_INCOMPLETE_SNAPSHOT` | History lacks package qty/unit |
| `PRICE_HISTORY_INVALID_PRICE` | History price ≤ 0 |
| `PRICE_HISTORY_INACTIVE_PACKAGE_UNIT` | History unit inactive/missing |
| `SOLE_COMPLETE_OFFER_NOT_PRIMARY` | Single Active complete offer with `IsPrimary=false` |
| `NO_ACTIVE_OFFER` | Ingredient has zero Active offers |

---

## 8. Supplier-offer classification

| Class | When |
|-------|------|
| **COMPLETE** | Exactly one Active primary; package complete; #117 base-unit cost succeeds for that offer |
| **SAFE_REMEDIATION_CANDIDATE** | Structured config unambiguous (e.g. sole Active offer with complete package but not primary) — **still needs owner approval before mutation** |
| **BUSINESS_DECISION_REQUIRED** | Package qty/unit/price/primary intent not proven by structured data |
| **INVALID_CONFIGURATION** | Multiple primary, zero price, inactive/rejected unit, invalid qty, etc. |

**Never** classify SAFE because the name contains kg/g/ml/l/bottle/bag/can/pack.

---

## 9. Primary-supplier audit rules

#117 policy (binding):

- Authoritative EstimatedBomCost requires **exactly one Active `IsPrimary`** offer with complete package metadata.
- Do **not** silently select first/cheapest/latest/non-primary.

| Condition | Expected audit status |
|-----------|----------------------|
| 0 Active offers | `NO_ACTIVE_OFFER` |
| 0 Active primary | `MISSING_SUPPLIER_OFFER` (+ maybe `SOLE_COMPLETE_OFFER_NOT_PRIMARY`) |
| >1 Active primary | `MULTIPLE_PRIMARY_SUPPLIERS` |
| 1 primary incomplete | `PRIMARY_INCOMPLETE` + package codes |
| 1 primary complete | `PRIMARY_COMPLETE` |

---

## 10. Package / unit validation

Complete package definition requires all of:

- `PackageQuantity` present and **> 0**
- `CurrentPrice` **> 0**
- Unit exists and **Active**
- Unit is not commercial packaging (`bottle`/`can`/`pack`)
- Conversion to `Ingredient.BaseUnitId` succeeds (#110/#117)
- Normalized package base quantity **> 0**

---

## 11. Price-history validation

For each offer:

- Prefer **at most one** `IsCurrent = true` row
- Flag missing current, multiple current, incomplete package snapshot, unit problems, price ≤ 0
- Flag mismatch when history package/price differs from current offer
- **Do not** rewrite historical rows solely from current offer in Checkpoint A

---

## 12. Recipe costing verification

Audit runs `IEstimatedBomCostService.CalculateRecipeEstimatedCostAsync` for every **Active + Status=Active** recipe.

| Result | Report |
|--------|--------|
| COMPLETE | `TotalCost` populated |
| INCOMPLETE | `TotalCost` null; issue codes listed |

Recipe incompleteness (e.g. Recipe 3 → ChildRecipe 5 without PreparedItem output) is **verification only** — do not auto-map BTP in #113.

---

## 13. Owner decision table template

| IngredientId | OfferId | Decision class | PackageQuantity | Content UnitId | Price | IsPrimary | Source (invoice/spec) | Approver | Date |
|--------------|---------|----------------|-----------------|----------------|-------|-----------|----------------------|----------|------|
| | | BUSINESS / SAFE | | | | | | | |

Required inputs for BUSINESS_DECISION rows:

1. Supplier SKU / invoice
2. Package content quantity
3. Content unit (physical g/kg/ml/l or true count pcs)
4. Price of **that** package
5. Primary Y/N

---

## 14. Local DB remediation procedure (future checkpoint — not executed here)

1. Run read-only audit; archive JSON baseline.
2. Fill owner decision table.
3. Backup local DB.
4. Apply **reviewed** updates in a transaction (application command or SQL reviewed by human).
5. Re-run audit; compare counts.
6. Run `dotnet test CafeChain/CafeChain.slnx`.
7. Do **not** put guessed UPDATEs into EF migrations.

---

## 15. Production procedure (future)

1. **Backup** database.
2. **Dry-run** report (read-only audit + planned mapping file).
3. **Reviewed mapping** signed by ops.
4. **Transaction** or controlled batches.
5. **Row-count** validation (offers updated, histories inserted).
6. **Rollback** script ready.
7. Re-run audit + #117 tests on staging before prod.
8. No deployment-time automatic guess.

---

## 16. Intentionally incomplete items (expected after Checkpoint A)

Until owner supplies package specs:

- Syrup, condensed milk, matcha, tea primary offers with null `PackageQuantity`
- Ingredients with no Active offers (ice, starch, brown sugar, water, …)
- Recipes using those ingredients
- ChildRecipe lines without PreparedItem/output (legacy BTP)

These must remain **INCOMPLETE**, not fake COMPLETE.

---

## 17. Follow-up references

| Topic | Issue / work |
|-------|----------------|
| Store inventory identity cutover | #115 |
| Stock/alert/restock PreparedItem | #114 |
| Legacy bottle/can conversion cleanup | After package SoT everywhere |
| Package-normalized costing | #117 (done) |
| PreparedItem master / recipe output | #116 / #112 (done) |

---

## 18. Checkpoint A deliverables checklist

- [x] This runbook
- [x] `IPurchaseUnitAuditService` / `PurchaseUnitAuditService`
- [x] `dotnet run -- audit-purchase-units`
- [x] Console summary + JSON output
- [x] Tests for classification + read-only behavior
- [ ] Checkpoint B: apply approved remediation (not this checkpoint)
- [ ] Close #113 (after full remediation verification)
