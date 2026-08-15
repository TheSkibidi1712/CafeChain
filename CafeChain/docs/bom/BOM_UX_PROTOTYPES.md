# BOM UX Prototypes

## 1. Muc tieu prototype

Ba prototype duoi day la wireframe/documentation throwaway. Khong co Razor/CSS/JS production nao duoc sua. Moi huong deu dung cung domain data, khac nhau o entry point va thu tu uu tien thong tin.

## 2. Shared presentation contract

Bat ke chon prototype nao, Recipe Detail phai tra loi theo thu tu:

`Identity -> Version -> Output/Yield -> Inputs -> Nested BTP -> Cost -> Completeness -> Where-used -> Operations -> History`.

Shared rules:

- Business name noi bat hon code/ID.
- "Hoat dong" va "Dang ap dung" la hai thong tin rieng.
- Design cost va Store FIFO cost co label/context rieng.
- Readiness co facet, khong gom thanh mot badge mo ho.
- Nested BTP hien stable PreparedItem truoc, pinned recipe version sau.
- Tren mobile, bang rong doi thanh summary row + detail drawer/accordion; khong ep 8 cot.

---

## 3. Prototype A - Recipe-centric Workspace

### 3.1 Mental model

User bat dau tu **Cong thuc cua mon/topping/BTP nao?** Day la mental model gan nhat voi UI va code hien tai.

### 3.2 List page

```text
+ Cong thuc va dinh muc ---------------------------------------------+
| [Mon ban] [Topping] [Ban thanh pham]    Tim...  Trang thai...      |
|--------------------------------------------------------------------|
| Doi tuong dau ra | Phien ban ap dung | Dinh muc | Cost | Readiness |
| Sua chua M       | RCP... 01/08      | 7 dong   | 14k  | 2 canh bao|
| Kem cheese       | RCP... 05/08      | 3 dong   | 52d/ml| San sang |
+--------------------------------------------------------------------+
```

- Filters: category, lifecycle/effectivity, readiness facet, search.
- Metrics: chi dung tong target, target khong co effective version, cost incomplete, co dependency warning.
- Row click mo workspace; action column chi giu "Xem" va menu context.

### 3.3 Detail page

```text
[Sua chua xoai nha dam M] [Dang ap dung] [Version v7] [Tao phien ban]
Ap dung tu 01/08/2026 | Cong thuc mon ban, size M

[Tong quan] [Dinh muc] [Chi phi] [Duoc dung o dau] [Van hanh] [Lich su]

Output: 1 phan M          Readiness: Config OK | Cost OK | POS OK
Inputs: 7                 Nested BTP: 2
Gia thiet ke: 14.410d     FIFO TDM: 14.980d (cap nhat ...)

Dinh muc dau vao
- Cot tra den (Ban thanh pham) 120 ml
  Cong thuc tham chieu: PREP_BLACK_TEA v4
- Sua tuoi (Nguyen lieu) 90 ml
...
```

Anatomy:

1. Identity header va version selector.
2. Output/portion card.
3. Input table co nested BTP distinction.
4. Cost authority switch/compare.
5. Readiness facets va remediation CTA.
6. Where-used reverse dependency.
7. Production/POS links theo Store.
8. Version timeline va compare.

### 3.4 Create/Edit/New Version

- Step 1: Chon doi tuong dau ra.
- Step 2: Dinh nghia output (chi BTP) hoac portion context.
- Step 3: Them dong dinh muc.
- Step 4: Review normalized UOM, overlap, cost completeness.
- New Version co summary diff voi version hien tai truoc khi publish.
- Neu schedule duoc chap nhan, action tach `Luu nhap`, `Ap dung ngay`, `Len lich`.

### 3.5 PreparedItem

- PreparedItem list van la master danh tinh ton.
- Click Recipe link mo workspace da loc target BTP.
- Inventory theo Store la secondary drill-down, khong tron vao master edit modal.

### 3.6 Nested BOM

- Input row hien PreparedItem name + base UOM.
- Expand de xem child formula va cost roll-up.
- Tree visualization la secondary view, khong thay the input table.

### 3.7 Cost / where-used / production

- Cost tab tach Design Estimate va Store Actual FIFO.
- Where-used nhom theo mon-size, topping, parent BTP; co active/effective marker.
- Operations tab co Store selector, stock, readiness, recent runs, actual yield.

### 3.8 Mobile

- Version selector va primary action xuong dong.
- Tabs thanh horizontal scroll/section menu compact.
- Dinh muc row thanh card: input, qty/UOM, status, cost; detail expand.

---

## 4. Prototype B - Input/Output-centric

### 4.1 Mental model

User bat dau tu dong du lieu **dau vao nao tao ra dau ra nao**. Tot cho WarehouseAccountant va nguoi kiem soat UOM/cost.

### 4.2 List page

```text
+ Ban do dau vao / dau ra --------------------------------------------+
| Dau ra: [Tat ca]  Co BTP con [ ]  Input: [Tim nguyen lieu/BTP]      |
|--------------------------------------------------------------------|
| Dau ra            | San luong       | Dau vao | Nested | Cost/base |
| Kem cheese        | 1.000 ml/me     | 4       | 0      | 52 d/ml   |
| Sua chua M        | 1 phan          | 7       | 2      | 14.410 d  |
+--------------------------------------------------------------------+
```

Primary filter la input/output; phu hop truy vet "Hat chia dang vao mon nao?".

### 4.3 Detail page

```text
                [OUTPUT]
        Kem cheese - 1.000 ml/me
                    |
        +-----------+-----------+
        |           |           |
  Cream 600ml  Sua 350ml  Muoi 5g ...
 [Ingredient] [Ingredient] [Ingredient]

Normalized quantities | cost contribution | completeness
```

- Graph/list toggle.
- Version/effectivity nam trong context panel, khong phai dieu huong chinh.
- Reverse dependency tu output va input co san ngay.

### 4.4 Create/Edit/New Version

- Canvas/table bat dau tu output, sau do them inputs.
- Normalized quantity va line cost la first-class.
- Review panel canh bao cycle, dimension mismatch, missing cost.

### 4.5 PreparedItem va nested BOM

- PreparedItem la node output/input chung.
- Click node mo identity summary, active formula va inventories.
- Nested graph co depth control, default 1-2 level de tranh cognitive overload.

### 4.6 Cost / where-used / production

- Cost contribution va where-used la diem manh cua huong nay.
- Production linkage nam o output node: recipe, batch output, recent runs.
- POS linkage nam o terminal sale targets.

### 4.7 Mobile

- Khong render graph rong mac dinh.
- Dung list `Output -> Inputs`; graph la fullscreen optional.

### 4.8 Risk

- De lam user van hanh bi lac vi version/action khong noi bat.
- De bi bien thanh graph editor phuc tap qua nhu cau cafe chain.

---

## 5. Prototype C - Operations-centric

### 5.1 Mental model

User bat dau tu **hom nay can san xuat/ban gi va dang bi chan o dau**. Tot cho StoreManager/ShiftSupervisor.

### 5.2 List page

```text
+ San sang van hanh BOM - CafeChain Thu Dau Mot ---------------------+
| Store [TDM]  Ngay [10/08]  [Can xu ly] [San sang] [Dang san xuat]  |
|--------------------------------------------------------------------|
| Dau ra       | Effective recipe | Input stock | Cost | Next action |
| Kem cheese   | v4               | Thieu 2kg   | OK   | Bo sung     |
| Cot tra den  | v6               | Du          | OK   | Tao lenh    |
+--------------------------------------------------------------------+
```

### 5.3 Detail page

```text
[Kem cheese]  San sang tai CafeChain TDM: BI CHAN

Planned output: 5 kg (5 me)
Input readiness: 3/4 du
Cost evidence: day du
Recent actual yield: 94.8%

[Tao yeu cau bo sung] [Xem cong thuc v4]

Inputs | Production runs | Inventory postings | History
```

- Recipe structure la linked reference.
- Next action va state chiem uu tien.

### 5.4 Create/Edit/New Version

- Khong nen tao/edit Recipe truc tiep trong operations page.
- Deep-link sang Recipe Workspace, giu role/permission ro.

### 5.5 PreparedItem va nested BOM

- PreparedItem hien theo stock/readiness tai Store.
- Nested BTP la dependency block, khong auto tao child run.

### 5.6 Cost / where-used / production

- Actual FIFO va yield trend noi bat.
- Design cost/where-used la secondary drawer/deep link.
- Production linkage tot nhat trong ba prototype.

### 5.7 Mobile

- Queue card theo next action.
- Sticky compact filter Store/date/status.
- Chi tiet chuyen thanh timeline + metric rows.

### 5.8 Risk

- Lam mo master-data governance, version compare va cong thuc cho POS/topping.
- Phu thuoc Store context, trong khi Recipe master hien tai la global.

---

## 6. So sanh

| Tieu chi | A Recipe-centric | B Input/Output-centric | C Operations-centric |
|---|---:|---:|---:|
| Phu hop code/IA hien tai | 5/5 | 3/5 | 3/5 |
| StoreManager de hanh dong | 4/5 | 2/5 | 5/5 |
| Warehouse/cost traceability | 4/5 | 5/5 | 4/5 |
| Version governance | 5/5 | 3/5 | 2/5 |
| Where-used/nested clarity | 4/5 | 5/5 | 3/5 |
| Do phuc tap implementation | Thap-vua | Cao | Vua-cao |
| Nguy co thanh ERP generic | Thap | Cao | Vua |

## 7. Recommendation tu prototype

Chon **Prototype A - Recipe-centric Workspace** lam trai nghiem chinh.

Lay them hai mieng tot:

- tu B: where-used va expand nested dependency;
- tu C: Store readiness/next action trong Operations tab.

Khong chon graph la entry point va khong chon operations dashboard lam master BOM. Cach ket hop nay giu mental model don gian, phu hop route/service hien co va van mo duoc cac capability ma UI dang che khuất.
