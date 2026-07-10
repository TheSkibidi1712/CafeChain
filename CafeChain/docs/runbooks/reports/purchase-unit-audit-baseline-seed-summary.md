# Baseline Purchase/Unit Audit Summary (Seed)

**Checkpoint:** #113 A (read-only)
**Source:** SQLite EnsureCreated seed data via `PurchaseUnitAuditService`
**JSON:** `purchase-unit-audit-baseline-seed.json`
**Mode:** ReadOnly — no seed/DB mutation

## Summary counts (from baseline JSON)

| Metric | Count (seed) |
|--------|----------------|
| Offers total | 9 |
| COMPLETE | Coffee #3, Sugar #1, Cream #5 (and any other primary with complete package) |
| SAFE_REMEDIATION_CANDIDATE | Cacao #7, Milk powder #8 (sole complete, not primary) |
| BUSINESS_DECISION_REQUIRED | Condensed milk, syrup, matcha, tea (null PackageQuantity) |
| Recipes COMPLETE | 0 (demo BOMs depend on incomplete offers / ice / child BTP) |
| Recipes INCOMPLETE | All Active seed recipes |

## Golden COMPLETE offer (protected)

| Offer | Ingredient | Package | Base unit cost |
|-------|------------|---------|----------------|
| IS#3 | Coffee ING00001 | 1 kg @ 140000 | **140 ₫/g** |

## Intentionally incomplete

- Syrup / condensed milk / matcha / tea — need owner package specs
- Ice, starch, brown sugar, water — no Active offer
- Recipe 3 → ChildRecipe 5 — `LEGACY_CHILD_RECIPE_WITHOUT_OUTPUT` until BTP mapping (#115/#114)

## Command (local SQL Server)

```bash
dotnet run --project CafeChain -- audit-purchase-units --out ./CafeChain/docs/runbooks/reports/purchase-unit-audit-local.json
```

Do not commit secrets or connection strings with reports.
