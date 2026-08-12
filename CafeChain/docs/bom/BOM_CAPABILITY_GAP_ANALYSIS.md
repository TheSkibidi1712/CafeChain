# BOM Capability Gap Analysis

## 1. Ket luan ngan

CafeChain **da co loi BOM du manh cho van hanh F&B co kiem soat**: cong thuc theo size/topping, BTP on dinh, nested BOM, UOM fail-closed, cost completeness, production actual yield, FIFO va POS snapshot deu ton tai trong code.

He thong chua dat muc "governance day du" cho chuoi cafe khi can lap lich phien ban tuong lai, tra loi where-used, so sanh phien ban va xem readiness trong mot man hinh. Phan lon cam giac so sai den tu read model/IA, nhung effective lifecycle la gap domain that.

## 2. Thang phan loai

- `SUPPORTED`: contract va implementation chinh da co.
- `PARTIAL`: co loi chinh nhung con legacy, selector khong dong nhat hoac thieu mot phan can thiet.
- `MISSING`: khong tim thay capability/read model.
- `CONFUSING_UI`: data co nhung presentation de gay hieu sai.
- `WRONG_DOMAIN`: model/hien tai co the vi pham nghia nghiep vu.
- `NEEDS_OWNER_DECISION`: khong du authority de chot mot trong nhieu policy hop le.

## 3. Capability matrix

| Capability | Trang thai | Evidence | Danh gia F&B |
|---|---|---|---|
| Recipe identity | `SUPPORTED` | Target POS = Drink+Size, topping = Topping, BTP = PreparedItem | Phu hop chuoi cafe |
| Recipe versioning | `PARTIAL` | Archive old + create new + `ParentVersionId` | Giu lich su, nhung thieu sequence/timeline/compare |
| Effective date | `WRONG_DOMAIN` | New future version Active ngay, old version Archived ngay | Co the tao khoang trong khong co cong thuc effective |
| Active/effective recipe | `WRONG_DOMAIN` | Active flag/index va effective-date query khong cung mot state authority | Can resolver va lifecycle thong nhat |
| BOM lines | `SUPPORTED` | `RecipeDetail`, XOR Ingredient/ChildRecipe, unique constraint | Dung cho BOM quan ca phe |
| Ingredient quantity | `SUPPORTED` | Decimal quantity + UnitId, validation > 0 | Du cho dinh muc F&B thong thuong |
| UOM normalization | `SUPPORTED` | Base UOM, physical va ingredient conversion, fail-closed | Diem manh cua he thong |
| Menu size support | `SUPPORTED` | Exact Drink+Size recipe va resolver | Dung voi cong thuc theo size |
| Topping support | `SUPPORTED` | Topping recipe + per-drink policy snapshots | Co du sale/cost treatment; UI nguon qty phuc tap |
| PreparedItem/BTP | `SUPPORTED` | Stable PreparedItem stock identity, base UOM, active recipe | Phu hop so che/ban thanh pham |
| Nested BOM | `SUPPORTED` | Exact child Recipe version, cycle/depth guard | Cost recurse, POS tru mot tang dung contract |
| Expected Yield | `PARTIAL` | BTP `OutputQuantity/OutputUnit`; POS/topping ngam dinh mot phan | BTP ro; menu/topping chua trinh bay output semantic |
| Actual Yield | `SUPPORTED` | Production v2 output actual/accepted/rejected/variance | Du cho run execution |
| Batch/Me semantics | `PARTIAL` | V2 integer batch; v1 decimal factor con read path | Can label legacy ro, khong tron hai nghia |
| Loss/waste | `PARTIAL` | Rejected/zero accepted/variance reason | Chua co taxonomy waste chuan va line-level loss analytics |
| Cost per recipe | `SUPPORTED` | `EstimatedBomCostService` | Co complete/incomplete, nested cost |
| Cost per portion | `SUPPORTED` | POS/topping total per recipe/portion | Can giu label "uoc tinh" |
| Cost per base output UOM | `SUPPORTED` | BTP total / normalized expected output | Dung de so sanh voi FIFO actual |
| FIFO costing | `SUPPORTED` | FIFO layers va actual input consumption | Gia van hanh theo Store co authority |
| Cost completeness | `SUPPORTED` | Missing quote/conversion/child cost -> incomplete | Khong fake tong zero |
| Where-used / reverse dependency | `MISSING` | Chi co delete guard `ChildRecipeId`; khong co query/UI | Kho danh gia blast radius khi doi BTP |
| Recipe readiness | `PARTIAL` + `CONFUSING_UI` | Config health, costing health va store production readiness tach roi | Data co nhung user kho tong hop trong 10 giay |
| Production linkage | `SUPPORTED` | ProductionRun pin Recipe; detail co recent runs/readiness | Tot cho BTP, khong auto child production |
| Inventory linkage | `SUPPORTED` + `PARTIAL` | Stable PreparedItem identity; legacy Recipe dual-read con ton tai | New path dung; legacy can trinh bay ro |
| POS consumption linkage | `SUPPORTED` | Order recipe snapshots, one-level deduction, idempotency | Bao ve lich su khi recipe doi |
| History/audit | `PARTIAL` | Parent chain, pinned IDs, ledger links | Thieu business timeline tap trung |
| Version comparison | `MISSING` | Version rows du du lieu nhung khong co diff read model/UI | Nen la read-model, khong can ERP phuc tap |

## 4. Cac cau hoi 10 giay tren UI hien tai

| Cau hoi | Tra loi duoc? | Exact reason | Phan loai |
|---|---|---|---|
| 1. Day la cong thuc cua cai gi? | Co | Hero va overview hien target/name/type | Tot |
| 2. Phien ban nao dang ap dung? | Mot phan | Co code/status/effective date, nhung khong co version sequence/timeline va Active co the khong effective | `DOMAIN_PRESENTATION_PROBLEM` + `WRONG_DOMAIN` |
| 3. Mot lan tao ra bao nhieu? | BTP: co; POS/topping: mo ho | BTP hien output/mẻ; menu/topping dung "phan" ngam dinh | `DOMAIN_PRESENTATION_PROBLEM` |
| 4. Dung nguyen lieu nao? | Co | Bang dinh muc liet ke component, qty, normalized qty | Tot |
| 5. Co dung BTP con khong? | Co | Child row va link version co san | Tot, nhung ma ky thuat con noi bat |
| 6. Gia von hien tai bao nhieu? | Mot phan | Design cost ro; FIFO actual nam trong context Store/operations | `INFORMATION_ARCHITECTURE_PROBLEM` |
| 7. Cong thuc dang duoc dung o dau? | Khong | Khong co where-used query/UI | `MISSING_CAPABILITY` |
| 8. San sang san xuat/ban chua? | Mot phan | Configuration, costing va store readiness tach section/context | `INFORMATION_ARCHITECTURE_PROBLEM` |

## 5. Gap theo muc do

### P0 - Co the lam sai nghiep vu

1. **Future effective version gap** (`WRONG_DOMAIN`): archive version cu truoc khi version moi den ngay hieu luc.
2. **Selector drift** (`PARTIAL`): catalog co effective filter; mot fallback trong inventory deduction chi loc Active va con size-null fallback.

### P1 - De ra quyet dinh sai hoac khong thay dependency

1. **Khong co where-used**: editor khong thay BTP/cong thuc dang tac dong mon nao.
2. **Khong co version compare**: kho review thay doi quantity, UOM, output va cost.
3. **Readiness bi chia nho**: complete config khong dong nghia cost complete hoac san xuat san sang tai Store.
4. **Cost authority de nham**: design estimate va FIFO actual can nam cung context nhung label/timestamp ro.

### P2 - UX/domain language

1. `Recipe` trong code thuc chat la version row, trong UI lai thuong duoc goi chung la cong thuc.
2. "Hoat dong" de bi hieu la "dang ap dung" du EffectiveDate co the khac.
3. Legacy run factor va batch v2 co the bi goi chung la "me".
4. Ma Recipe/PreparedItem noi bat hon business name o mot so bang.
5. Data Health tong theo page hien tai, de bi hieu nham la tong toan bo bo loc.

### P3 - Visual polish

UI gan day da co hero, section cards, badge va responsive shell kha dong bo. Visual polish khong phai nut that chinh cua task refactor tiep theo.

## 6. Capability khong nen them chi vi ERP khac co

Khong co evidence CafeChain can cac phan sau trong phase gan:

- routing/may moc/work center;
- MRP da tang tu dong;
- auto tao child ProductionRun;
- lot/expiry cho moi BOM neu inventory chua co authority ben vung;
- standard labor/overhead accounting phuc tap;
- workflow ECN/engineering change da cap.

F&B cafe chain can version, output, UOM, cost, readiness va traceability ro hon; khong can bien BOM thanh manufacturing ERP tong quat.

## 7. Test evidence va residual risk

Active tests bao phu tot:

- BTP output/UOM/version invariants (`RecipePreparedItemOutputIssue112Tests`).
- create validation/concurrency/UOM/cycle (`BomRecipeCreateHardeningIssue243Tests`).
- production actual yield/FIFO/idempotency (`ProductionBatchYieldTests`).
- POS PreparedItem consumption va snapshots (`PosPreparedItemConsumptionIssue121Tests`, `POSInventoryDeductionGuardrailsIssue86Tests`).

Mot so file test ve detail/readiness/topping dang bi comment toan bo, do do khong duoc tinh la coverage dang chay:

- `BomDetailOperationalLinkageIssue150Tests.cs`
- `BomToppingConsumptionSourcesIssue149Tests.cs`
- `ProductionReadinessIssue148Tests.cs`

Task discovery nay khong chay test. Cac ket luan runtime van mang nhan `NOT_RUNTIME_VERIFIED`.

## 8. Verdict

**BOM hien tai du cho luong F&B cot loi, nhung chua du cho quan tri phien ban va ra quyet dinh nhanh o quy mo chuoi.**

- Khong nen rewrite domain tu dau.
- Can sua effective lifecycle/selector neu Owner can lap lich phien ban.
- Can bo sung where-used, compare va readiness projection.
- Can redesign theo recipe workspace de bien cac capability dang co thanh trai nghiem de hieu.
