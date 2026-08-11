# BOM Refactor Discovery Summary

## Executive verdict

CafeChain **da co BOM core kha day du cho mot chuoi cafe**, khong phai module demo chi co CRUD. Code hien tai da xu ly recipe theo size/topping, ban thanh pham, nested BOM, UOM, estimated cost, FIFO, production actual yield va POS snapshot.

Module van chua hoan thien cho governance o quy mo chuoi: effective version dang co defect semantic, khong co where-used/version compare, va readiness/cost authority bi chia nho tren UI.

## 1. BOM hien tai co du cho F&B thuc te khong?

**Du cho flow van hanh cot loi, chua du cho quan tri phien ban va thay doi an toan o quy mo chuoi.**

- Du: dinh muc, BTP, nested BOM, output, UOM, production, inventory, cost, POS consumption.
- Chua du: scheduling version, reverse dependency, compare, unified readiness va business audit timeline.

## 2. Cai gi da tot?

1. `PreparedItem` la danh tinh ton BTP on dinh, khong phu thuoc RecipeId version.
2. Ingredient/PreparedItem co base UOM; conversion fail-closed.
3. BOM line enforce Ingredient XOR child Recipe, duplicate/cycle/depth guards.
4. BTP co output quantity/unit va normalize ve don vi ton.
5. Estimated cost neu thieu data se incomplete, khong gia tong zero.
6. Production v2 tach planned batch, actual input, actual/accepted output va variance.
7. Accepted output moi cong ton; actual input moi tinh FIFO cost.
8. POS tru stock mot tang va giu recipe snapshots cho don da commit.

## 3. Cai gi thieu?

- Where-used/reverse dependency.
- Full version timeline va compare.
- Composite readiness theo facet.
- Business-readable BOM change history.
- Waste reason analytics co cau truc (neu Owner can).
- True future-effective lifecycle (neu product can scheduling).

## 4. Cai gi chi la UX dang lam he thong trong so sai?

- Detail chua gom identity/version/output/input/cost/where-used/operations/history thanh mot workspace.
- `Recipe` va "phien ban cong thuc" chua duoc tach ngon ngu.
- Design estimate va FIFO actual nam khac context, de bi hieu la cung mot gia von.
- Config health, cost health va production readiness bi tach roi.
- Nested BTP hien technical recipe evidence qua noi bat so voi business identity.
- Data Health count co nguy co bi hieu la tong toan bo trong khi query dang tong current page.

## 5. Cai gi that su can backend/domain change?

Bat buoc:

1. Dung mot effective recipe resolver chung.
2. Sua active/effective lifecycle hoac cam future date.
3. Dong nhat timezone/effective selectors tren POS, inventory fallback va production.

Read-model/API projection, khong phai business mutation:

1. Where-used.
2. Version chain/diff.
3. Readiness facets.
4. Correct aggregate health metrics.

Migration chi co the can neu Owner chot Scheduled/Effective lifecycle hoac stable RecipeIdentity entity. Task nay khong tao migration.

## 6. Prototype nao duoc de xuat?

**Prototype A - Recipe-centric Workspace**, ket hop:

- dependency/where-used lens tu prototype Input/Output;
- Store readiness/next action tu prototype Operations.

Day la huong it pha vo code/mental model hien tai nhat va van lam lo ro nhung capability dang bi che.

## 7. Owner decisions can chot truoc to-spec

1. Co can len lich version tuong lai khong?
2. Co can entity RecipeIdentity/RecipeFamily rieng khong?
3. Policy fallback khi scheduled version bi huy/khong hop le?
4. Role nao duoc xem design cost va FIFO actual cost?
5. Readiness mac dinh global hay theo Store?
6. Where-used mac dinh chi current hay ca historical?
7. Version compare chi review hay can approval workflow?
8. Waste taxonomy co nam trong phase BOM tiep theo khong?

## Deliverables

1. `BOM_AS_IS_DOMAIN_MAP.md`
2. `BOM_CAPABILITY_GAP_ANALYSIS.md`
3. `BOM_TARGET_DOMAIN_MODEL.md`
4. `BOM_UX_PROTOTYPES.md`
5. `BOM_RECOMMENDATION.md`
6. `BOM_REFACTOR_DISCOVERY_SUMMARY.md`

## Verification status

- Code/entity/service/query/UI/EF/test evidence da inspect.
- Khong chay full suite hoac focused tests vi task docs-only va SkillTest file khong ton tai trong checkout.
- Khong chay authenticated runtime; tat ca runtime conclusion duoc giu o muc `NOT_RUNTIME_VERIFIED`.
- Khong sua production code/UI/business logic.
- Khong migration.
- Khong PR/merge.

## Required conclusions

BOM_AS_IS_DOMAIN_AUDITED

BOM_REAL_WORLD_CAPABILITIES_ASSESSED

BOM_DOMAIN_LANGUAGE_MAPPED

BOM_UX_PROBLEMS_CLASSIFIED

BOM_TARGET_MODEL_PROPOSED

BOM_PROTOTYPES_COMPLETED

BOM_RECOMMENDATION_COMPLETED

OWNER_DECISIONS_IDENTIFIED

READY_FOR_OWNER_REVIEW_BEFORE_TO_SPEC

NO_IMPLEMENTATION_PERFORMED

NO_PR_PERFORMED

NO_MERGE_PERFORMED
