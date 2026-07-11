# Demo supplier package seed assumptions (#113)

**Status:** Hybrid D — model `HasData` source only
**Not:** real supplier evidence · production cost of truth · migration InsertData proof

## Scope

Authoritative configuration files:

- `Data/Configurations/Inventories/Suppliers/IngredientSupplierConfiguration.cs`
- `Data/Configurations/Inventories/Ingredients/IngredientConfiguration.cs`

Proven by SQLite / SQL Server **`EnsureCreated`** tests (`DemoSupplierPackageSeedIssue113Tests`).
**Not** proven by drop/recreate + `dotnet ef database update` until InitialCreate is regenerated.

## Owner-approved values

| IS# | PackageQuantity | Unit | IsPrimary | Price | Evidence class |
|-----|-----------------|------|-----------|-------|----------------|
| 2 | 380 | ml | true | 27000 | OwnerApprovedSyntheticDemoAssumption |
| 4 | 750 | ml | true | 250000 | OwnerApprovedDemoSeed |
| 6 | 500 | g | true | 450000 | OwnerApprovedDemoSeed |
| 7 | 1 | kg | true | 180000 | OwnerApprovedDemoSeed |
| 8 | 1 | kg | true | 85000 | OwnerApprovedDemoSeed |
| 9 | 200 | g | true | 120000 | OwnerApprovedSyntheticDemoAssumption |

## Synthetic assumptions (must not be treated as supplier specs)

### IS#2 — condensed milk

- Package managed as **380 ml** with base unit **ml**.
- Display name: **Sữa đặc demo lon 380 ml** (not “380 g”).
- **No** g ↔ ml conversion and **no** density inventing.

### IS#9 — tea box

- Net mass **200 g** = 100 bags × 2 g.
- Display name: **Trà đen demo hộp 100 túi × 2 g**.
- Package quantity is **net grams**, not bag count (100).

## Expected base-unit costs (demo math)

| IS# | Formula | Base unit cost |
|-----|---------|----------------|
| 2 | 27000 ÷ 380 ml | ≈ 71.0526 ₫/ml |
| 4 | 250000 ÷ 750 ml | ≈ 333.3333 ₫/ml |
| 6 | 450000 ÷ 500 g | 900 ₫/g |
| 7 | 180000 ÷ 1000 g | 180 ₫/g |
| 8 | 85000 ÷ 1000 g | 85 ₫/g |
| 9 | 120000 ÷ 200 g | 600 ₫/g |

## Production replacement

Before production reliance: replace IS#2 / IS#9 (and any demo-labelled rows) with verified invoice/spec package content, unit, and price. Keep ADR-0005 (no name inference).
