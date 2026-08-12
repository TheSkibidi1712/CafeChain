# BOM Recommendation

## 1. De xuat chinh

Xay future spec theo **Recipe-centric Workspace**, bo sung hai facet:

1. **Dependency lens**: where-used, nested BTP, version diff.
2. **Operations lens**: readiness, stock/FIFO va production theo Store.

Khong rewrite BOM. Loi domain hien tai da co nhieu contract dung voi F&B; can sua mot so authority va dua thong tin ra dung vi tri.

## 2. Vi sao phu hop CafeChain

- Nguoi dung van tim cong thuc theo mon, topping hoac BTP nhu hien tai.
- Phan quyen Recipe/PreparedItem hien co khong can doi mental model.
- Production va POS deu pin exact RecipeId, nen workspace co the lam diem trace chung.
- PreparedItem van la stock identity on dinh, khong bi tron voi formula version.
- Where-used va compare giai quyet nhu cau chuoi ma khong keo he thong thanh ERP san xuat tong quat.

## 3. Giu lai

1. Target categories: mon+size, topping, ban thanh pham.
2. Archive-and-new-row version history.
3. Ingredient XOR child recipe line constraint.
4. Stable PreparedItem stock identity.
5. Base UOM va conversion fail-closed.
6. Nested BOM cycle/depth guards.
7. Estimated cost completeness.
8. Production v2 actual input/yield/accepted output.
9. POS one-level stock deduction va sale-time snapshots.
10. FIFO actual costing theo Store.

## 4. Refactor can thiet

### 4.1 Information architecture

- Recipe list la entry point chung.
- Detail la workspace voi tabs: Tong quan, Dinh muc, Chi phi, Duoc dung o dau, Van hanh, Lich su.
- PreparedItem list giu master-data role; active recipe mo sang workspace.
- Data Health tro thanh secondary context/filter cua Recipe, khong phai mot the gioi tach roi.

### 4.2 Domain presentation

- Hien `Recipe` nhu **phien ban cong thuc**.
- Tach `Duoc phep su dung` khoi `Dang ap dung tu`.
- POS/topping dung "mot phan"; BTP dung "san luong chuan mot me".
- Nested row hien PreparedItem identity truoc, pinned child version sau.
- Tat ca cost co authority label.

### 4.3 Read model

Can projection moi/bo sung:

- recipe identity + version chain;
- effective version summary;
- where-used reverse dependency;
- version diff;
- readiness facets;
- design estimate vs Store FIFO comparison;
- paged Data Health totals dung toan bo bo loc, khong chi current page.

## 5. Backend/domain changes that su can

### 5.1 Bat buoc de loai bo defect hien tai

1. **Central effective recipe resolver** dung chung cho catalog, fallback deduction, production/read models.
2. **Dong nhat effective lifecycle**: khong archive old ngay neu new version chua effective, hoac cam future date neu san pham khong can scheduling.
3. Loai bo selector dung host-time conversion/khac policy.

### 5.2 Read-model only

1. Where-used query.
2. Version chain va compare query.
3. Composite readiness projection.
4. Correct Data Health aggregate counts.

### 5.3 Optional sau Owner decision

- Stable `RecipeIdentity/RecipeFamily` entity.
- Draft/Scheduled/Effective/Archived lifecycle.
- Friendly monotonic version number.
- Audit event timeline chuyen biet cho publish/schedule.

## 6. UI-only changes

- Workspace/tab anatomy.
- Label/glossary.
- Cost authority cards.
- Readiness facet presentation.
- Version selector/timeline va diff UI (data can read model).
- Responsive table-to-card.
- Contextual action theo permission; read-only role khong thay disabled editor controls.

## 7. Migration

**NO MIGRATION** cho:

- workspace UI;
- where-used;
- version diff;
- readiness projection;
- cost presentation;
- Data Health aggregate fix.

**CONDITIONAL MIGRATION** chi khi Owner chot future scheduling hoac stable RecipeIdentity:

- additive state/identity/effective interval fields;
- filtered unique/index thay doi de cho old Effective va new Scheduled cung ton tai;
- deterministic backfill theo target tuple va parent chain;
- ambiguous version chains -> `NEEDS_REVIEW`;
- khong rewrite ProductionRun, Order snapshot, FIFO hay historical RecipeId.

Khong co migration nao duoc tao trong task discovery nay.

## 8. Nhung thu khong nen xay them luc nay

- May/tram san xuat, routing va labor scheduling.
- MRP recursion tu dong.
- Tu tao child ProductionRun.
- Universal package UOM.
- General ledger cho waste neu accounting domain chua co.
- Recipe graph editor la UI chinh.
- Lot/expiry moi chi de "giong ERP".

## 9. Suggested future phases

### Phase 0 - Owner decisions

- Chot co future scheduling hay chi publish ngay.
- Chot stable RecipeIdentity entity co can trong do an hay khong.
- Chot readiness nao hien cho tung role.

### Phase 1 - Authority hardening

- Shared resolver.
- Effective lifecycle invariant.
- Regression cho POS/production/deduction.

### Phase 2 - Read models

- Version chain/diff.
- Where-used.
- Readiness facets.
- Correct health aggregates.

### Phase 3 - Recipe Workspace

- List/detail/form IA.
- Version selector, output, inputs, costs.
- Where-used/history.

### Phase 4 - Operational lens

- Store selector.
- FIFO/current stock/recent run.
- Role-based next action.

### Phase 5 - Runtime acceptance

- Long names, nested BTP, incomplete cost, future/effective transition, Store contexts, mobile.

## 10. Risk matrix

| Risk | Muc | Kiem soat |
|---|---|---|
| Effective selector thay doi lam POS khong resolve recipe | Cao | Contract tests across catalog/order/deduction, rollout theo resolver |
| Version backfill ambiguous | Cao neu migration | Dry-run, target tuple + chain evidence, NEEDS_REVIEW |
| Where-used query N+1 | Vua | Projection/batched query, pagination |
| Nested cost/tree query qua nang | Vua | Depth cap, lazy expansion, cached request projection |
| UI tron design cost va actual cost | Cao | Authority label + Store/time context bat buoc |
| Role thay action khong duoc phep | Vua | Backend permission authority + contextual CTA |
| Legacy BTP RecipeId path bi hieu la target | Vua | Stable PreparedItem-first presentation |

## 11. Owner decisions can chot truoc to-spec

1. **Co can len lich phien ban cong thuc tuong lai khong?**
   - Neu khong: cam future date va giu Active/Archived.
   - Neu co: can Scheduled/Effective semantics va conditional migration.
2. **Co can stable RecipeIdentity/RecipeFamily entity khong?**
   - De xuat chua tao trong phase UX/read-model; derive tu target tuple truoc.
3. **Khi version scheduled bi huy/loi, co cho fallback version cu khong?**
4. **Ai duoc xem FIFO actual cost va supplier/design cost?**
5. **Readiness mac dinh tren detail la global hay theo Store dang chon?**
   - De xuat global config/cost truoc; operations phai chon Store.
6. **Where-used co can bao gom historical archived parents hay chi current effective?**
   - De xuat default current, filter mo historical.
7. **Version compare co can approval workflow hay chi evidence review?**
   - De xuat chi evidence review trong phase dau.
8. **Waste taxonomy co can bao cao theo ly do ngay trong phase BOM khong?**
   - De xuat follow-up production reporting, khong chen vao Recipe master.

## 12. Definition of Done cho future implementation

- Moi selector tra cung effective Recipe cho cung target/business instant.
- Future version khong tao gap/overlap.
- Detail tra loi 8 cau hoi nghiep vu trong 10 giay.
- Where-used va version compare co evidence ro.
- Design/FIFO/historical cost khong bi tron.
- BTP stock identity van la PreparedItem.
- POS snapshot, production pinning, FIFO va UOM invariants khong regression.
- Responsive/read-only role/runtime acceptance pass.

## 13. Recommendation status

`READY_FOR_OWNER_REVIEW_BEFORE_TO_SPEC`.

Khong co production implementation, migration, PR hoac merge trong task nay.
