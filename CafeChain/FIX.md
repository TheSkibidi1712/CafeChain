# TASK: CHUẨN HÓA SEED DATA UNIT / INGREDIENT / SUPPLIER PACKAGE TRONG SEEDALL

Bạn hãy đọc, phân tích cấu trúc project hiện tại và chỉnh sửa **SeedData trong `SeedAll`** để chuẩn hóa dữ liệu liên quan đến:

* Unit
* Ingredient
* Ingredient Base Unit
* Physical Unit Conversion
* Ingredient Unit Conversion nếu project thực sự sử dụng
* Supplier
* SupplierPackage
* SupplierPackage ContentUnit
* SupplierPackage PackageQuantity
* Các dữ liệu demo liên quan đến procurement

Mục tiêu là đảm bảo SeedData phản ánh đúng nghiệp vụ tồn kho, tiêu hao nguyên liệu và nhập hàng từ nhà cung cấp.

---

# I. PHẠM VI THỰC HIỆN

Chỉ được chỉnh sửa:

* `SeedAll`
* Các method/helper seed được `SeedAll` trực tiếp sử dụng nếu cần thiết.
* Seed/demo-data liên quan đến:

  * Unit
  * Ingredient
  * Supplier
  * SupplierPackage
  * UnitConversion
  * IngredientUnitConversion

Nếu project chia SeedAll thành nhiều method như:

```csharp
SeedUnits(...)
SeedIngredients(...)
SeedSuppliers(...)
SeedSupplierPackages(...)
SeedUnitConversions(...)
```

thì được phép chỉnh sửa các method đó để dữ liệu cuối cùng đúng nghiệp vụ.

## TUYỆT ĐỐI KHÔNG:

* Không tạo Migration mới.
* Không sửa Migration cũ.
* Không rewrite Migration history.
* Không sửa Entity Configuration.
* Không sửa `IEntityTypeConfiguration<T>`.
* Không sửa Fluent API configuration.
* Không sửa database schema.
* Không thêm/xóa column.
* Không thay đổi relationship trong EF Configuration.
* Không refactor domain/business logic ngoài scope.
* Không sửa Controller/API chỉ để phù hợp với SeedData.
* Không sửa Service/Repository nếu vấn đề chỉ nằm ở dữ liệu seed.
* Không reset database chỉ để seed lại dữ liệu.
* Không thay đổi contract hiện tại của Entity nếu không thực sự bắt buộc.

Nếu phát hiện schema/configuration hiện tại không hỗ trợ nghiệp vụ, **không tự ý sửa**.

Hãy báo:

```text
SCHEMA_OR_CONFIGURATION_REVIEW_REQUIRED
```

và mô tả vấn đề.

---

# II. NGUYÊN TẮC THIẾT KẾ SEED DATA

SeedData phải phân biệt rõ 3 khái niệm:

```text
1. Ingredient Base Unit
2. Physical Unit Conversion
3. Supplier Package
```

Không được trộn ba khái niệm này với nhau.

---

# III. CHUẨN HÓA INGREDIENT.BASEUNITID

Mỗi Ingredient phải có một `BaseUnitId` phản ánh đúng đơn vị tồn kho/tiêu hao cơ sở.

Quy tắc mặc định:

## 1. Nguyên liệu theo khối lượng

Base Unit:

```text
g
```

Ví dụ:

* Coffee Bean
* Coffee Powder
* Matcha Powder
* Cocoa Powder
* Sugar nếu tồn/tiêu hao theo khối lượng
* Các powder/solid ingredient khác

Ví dụ:

```text
Coffee
BaseUnit = g
```

Không dùng:

```text
kg
pack
bag
box
```

làm BaseUnit nếu nguyên liệu thực tế được tồn và tiêu hao theo gram.

---

## 2. Nguyên liệu theo thể tích

Base Unit:

```text
ml
```

Ví dụ:

* Milk
* Syrup
* Sauce dạng lỏng
* Juice
* Liquid concentrate
* Các liquid ingredient khác

Ví dụ:

```text
Milk
BaseUnit = ml

Vanilla Syrup
BaseUnit = ml
```

Không dùng:

```text
L
Bottle
Can
Pack
```

làm BaseUnit nếu hệ thống tồn/tiêu hao nguyên liệu theo ml.

---

## 3. Vật tư đếm từng cái

Base Unit:

```text
pcs
```

Áp dụng cho những vật tư thực tế tồn kho/tiêu hao từng đơn vị:

* Cup
* Cup Lid
* Straw
* Spoon
* Fork
* Bag
* Napkin nếu quản lý từng cái
* Các disposable item tương tự

Ví dụ:

```text
Plastic Cup
BaseUnit = pcs

Cup Lid
BaseUnit = pcs

Straw
BaseUnit = pcs
```

Không sử dụng:

```text
Pack
Carton
Box
Bottle
Can
```

làm BaseUnit chỉ vì vật tư được mua theo dạng đóng gói đó.

---

# IV. XỬ LÝ INGREDIENT CÓ SEMANTICS KHÔNG RÕ

Không được tự suy đoán một Ingredient nếu không đủ thông tin để xác định:

```text
g
ml
pcs
```

Ví dụ một record có tên hoặc mô tả không đủ để biết:

* tồn theo cái;
* tồn theo chai;
* tồn theo ml;
* hay tồn theo gram.

Trong trường hợp đó:

* Không tự sửa sai semantics.
* Giữ dữ liệu an toàn nhất theo convention hiện tại.
* Ghi nhận record cần review.

Cuối task báo:

```text
NEEDS_REVIEW:
- IngredientId:
- IngredientName:
- CurrentBaseUnit:
- Reason:
```

---

# V. PHYSICAL UNIT CONVERSION

Physical Unit Conversion chỉ được dùng cho các quan hệ đo lường vật lý có semantics ổn định.

Ví dụ hợp lệ:

```text
1 kg = 1000 g
1 L = 1000 ml
```

Có thể có các conversion vật lý tương đương khác nếu project hiện tại đã định nghĩa rõ.

Các conversion này có thể nằm trong:

```text
PhysicalUnitConversion
```

hoặc entity tương đương của project.

## Không được tạo global conversion cho package unit

Tuyệt đối không tạo:

```text
1 carton = 1000 pcs
1 pack = 100 pcs
1 box = 50 pcs
1 bottle = 750 ml
1 can = 330 ml
```

dưới dạng global physical conversion.

Bởi vì:

```text
carton
pack
box
bottle
can
```

là hình thức đóng gói.

Số lượng chứa bên trong phụ thuộc:

* Ingredient
* Product
* Supplier
* Package specification

và không phải universal physical conversion.

---

# VI. INGREDIENT UNIT CONVERSION

Nếu project có `IngredientUnitConversion`, chỉ seed conversion khi nó thực sự cần cho nghiệp vụ đo lường Ingredient.

Ví dụ có thể hợp lệ:

```text
kg -> g
L -> ml
```

nếu Ingredient thực sự hỗ trợ nhập/hiển thị ở các unit đó.

Không được tự tạo conversion:

```text
pack -> pcs
carton -> pcs
box -> pcs
bottle -> ml
can -> ml
```

chỉ vì các Unit này tồn tại trong bảng Unit.

Việc một Unit tồn tại trong danh mục:

```text
Unit
```

không đồng nghĩa Unit đó phải có conversion.

---

# VII. THIẾT KẾ SUPPLIER PACKAGE

`SupplierPackage` phải đại diện cho:

> Quy cách một Ingredient được mua từ một Supplier.

Ví dụ:

```text
Supplier: ABC
Ingredient: Cup Lid
Package: Bag
Quantity: 100 pcs
Price: 50,000
```

hoặc:

```text
Supplier: XYZ
Ingredient: Coffee
Package: Bag
Quantity: 1 kg
Price: 250,000
```

Tuy nhiên dữ liệu lưu trong seed phải được normalize về `Ingredient.BaseUnit`.

---

# VIII. CONTENTUNITID CỦA SUPPLIERPACKAGE

Đối với các demo SeedData mục tiêu:

```text
SupplierPackage.ContentUnitId
=
Ingredient.BaseUnitId
```

Đây phải là convention mặc định khi tạo SupplierPackage seed.

Không seed package theo kiểu:

```text
Coffee BaseUnit = g

SupplierPackage:
ContentUnit = kg
PackageQuantity = 1
```

Thay vào đó normalize thành:

```text
Coffee BaseUnit = g

SupplierPackage:
ContentUnit = g
PackageQuantity = 1000
```

---

# IX. PACKAGEQUANTITY PHẢI NORMALIZE VỀ BASE UNIT

`PackageQuantity` phải thể hiện tổng lượng Ingredient có trong một package mua hàng, tính theo `Ingredient.BaseUnit`.

## Ví dụ 1 — Cup Lid

```text
Ingredient:
Cup Lid
BaseUnit = pcs

Supplier package:
1 bag = 100 lids
```

Seed:

```text
ContentUnitId = pcs
PackageQuantity = 100
```

---

## Ví dụ 2 — Cup

```text
Ingredient:
Cup
BaseUnit = pcs

Supplier package:
1 carton = 1000 cups
```

Seed:

```text
ContentUnitId = pcs
PackageQuantity = 1000
```

Không tạo:

```text
carton -> 1000 pcs
```

trong PhysicalUnitConversion.

---

## Ví dụ 3 — Coffee

```text
Ingredient:
Coffee
BaseUnit = g

Supplier package:
1 bag = 1 kg
```

Normalize:

```text
ContentUnitId = g
PackageQuantity = 1000
```

Không seed:

```text
ContentUnitId = kg
PackageQuantity = 1
```

cho demo package mục tiêu nếu convention mới yêu cầu ContentUnit mặc định theo BaseUnit.

---

## Ví dụ 4 — Syrup

```text
Ingredient:
Vanilla Syrup
BaseUnit = ml

Supplier package:
1 bottle = 750 ml
```

Seed:

```text
ContentUnitId = ml
PackageQuantity = 750
```

Không tạo global conversion:

```text
1 bottle = 750 ml
```

---

## Ví dụ 5 — Milk

Ví dụ nhà cung cấp bán:

```text
1 carton = 12 hộp
mỗi hộp = 1 L
```

Nếu SupplierPackage đại diện cho toàn bộ carton được mua:

```text
BaseUnit = ml
PackageQuantity = 12000
ContentUnitId = ml
```

Nếu SupplierPackage của project đại diện cho từng hộp 1 L:

```text
BaseUnit = ml
PackageQuantity = 1000
ContentUnitId = ml
```

Hãy inspect semantics của SupplierPackage hiện tại trước khi quyết định.

Không tự suy đoán package hierarchy nếu code hiện tại không thể hiện rõ.

---

# X. PACKAGE UNIT KHÔNG PHẢI BASE UNIT

Các Unit như:

```text
Pack
Carton
Box
Bottle
Can
Bag
```

có thể tiếp tục tồn tại trong danh mục `Unit` nếu project cần.

Nhưng không được mặc định sử dụng chúng làm `Ingredient.BaseUnit`.

Ví dụ:

SAI:

```text
Cup:
BaseUnit = Carton
```

ĐÚNG:

```text
Cup:
BaseUnit = pcs
```

SupplierPackage mới thể hiện:

```text
1 carton chứa 1000 pcs
```

---

# XI. PROCUREMENT READINESS

Một `SupplierPackage` chỉ được seed:

```text
IsActive = true
```

khi package có thể thực sự sử dụng cho nghiệp vụ procurement.

Phải kiểm tra tối thiểu:

```text
Ingredient != null
Supplier != null
PackageQuantity > 0
ContentUnitId != null
Price hợp lệ
Supplier scope hợp lệ
Store scope hợp lệ nếu model yêu cầu
```

Ngoài ra:

```text
ContentUnitId
```

phải tương thích với:

```text
Ingredient.BaseUnitId
```

Với demo seed được normalize trong task này, ưu tiên:

```text
SupplierPackage.ContentUnitId
==
Ingredient.BaseUnitId
```

Không seed:

```text
IsActive = true
```

cho package thiếu dữ liệu bắt buộc.

Nếu cần giữ record demo chưa hoàn chỉnh thì:

```text
IsActive = false
```

hoặc xử lý theo convention hiện tại của project.

Không thay đổi business rule/service để ép package đó trở thành hợp lệ.

---

# XII. THỨ TỰ SEED

Kiểm tra dependency và tổ chức SeedAll theo thứ tự hợp lý.

Ưu tiên:

```text
1. Units
2. Physical Unit Conversions
3. Suppliers
4. Ingredients
5. Ingredient Unit Conversions
6. Supplier Packages
7. Các demo data phụ thuộc tiếp theo
```

Mục tiêu là khi seed SupplierPackage thì đã xác định được:

```text
Ingredient
Ingredient.BaseUnitId
Supplier
ContentUnit
```

Không hard-code GUID một cách không cần thiết nếu project đã có helper để resolve seeded entity.

Nếu project hiện tại dùng deterministic ID thì tiếp tục theo convention hiện tại.

---

# XIII. IDEMPOTENCY / RERUN-SAFE

Toàn bộ SeedAll phải có khả năng chạy lại.

Không được tạo duplicate:

```text
Unit
PhysicalUnitConversion
IngredientUnitConversion
SupplierPackage
```

Cần inspect cách project đang xác định uniqueness.

Ví dụ:

Unit:

```text
Code
Name
```

Ingredient:

```text
Code
Name
```

SupplierPackage có thể dựa vào tổ hợp:

```text
SupplierId
IngredientId
PackageUnitId
ContentUnitId
```

hoặc business key hiện tại của project.

Không tự đặt unique rule mới nếu Entity/Configuration hiện tại đã có convention.

Khi seed lại:

* Nếu record chưa tồn tại → insert.
* Nếu record seed đã tồn tại nhưng dữ liệu demo cũ sai → update fields liên quan.
* Không tạo thêm record duplicate.

---

# XIV. XỬ LÝ DATABASE DEMO ĐÃ CÓ SEED CŨ

Database có thể đã tồn tại dữ liệu từ phiên bản seed trước.

Không được:

```text
Drop database
Reset database
Delete migration
Rewrite migration
```

chỉ để sửa SeedData.

SeedAll cần có khả năng normalize/backfill các record seed đã biết theo convention hiện tại.

Ví dụ:

Seed cũ:

```text
Cup:
BaseUnit = Carton
```

Seed mới cần normalize thành:

```text
Cup:
BaseUnit = pcs
```

nếu xác định chắc chắn Cup được tồn và tiêu hao từng cái.

Tương tự SupplierPackage cũ:

```text
Coffee
ContentUnit = kg
PackageQuantity = 1
```

có thể được normalize thành:

```text
Coffee
ContentUnit = g
PackageQuantity = 1000
```

nếu đây là record demo do SeedAll quản lý và có thể nhận diện chắc chắn.

Không update dữ liệu user-created hoặc production-like record ngoài phạm vi SeedData.

---

# XV. KHÔNG HARD-CODE CONVERSION PACKAGE TOÀN CỤC

Hãy search toàn bộ phần SeedAll liên quan để đảm bảo không còn logic kiểu:

```csharp
AddConversion("Carton", "pcs", 1000);
AddConversion("Pack", "pcs", 100);
AddConversion("Bottle", "ml", 750);
```

Nếu có và conversion đó chỉ phục vụ SupplierPackage demo:

* Gỡ khỏi seed physical conversion.
* Di chuyển quantity tương ứng về SupplierPackage.

Ví dụ:

Thay vì:

```text
Carton -> pcs = 1000
```

hãy seed:

```text
SupplierPackage:
PackageUnit = Carton
PackageQuantity = 1000
ContentUnit = pcs
```

---

# XVI. GIỮ NGUYÊN BUSINESS LOGIC HIỆN TẠI

Task này là **SeedData normalization**, không phải redesign toàn hệ thống.

Vì vậy:

Không sửa:

```text
InventoryService
ProcurementService
SupplierService
RecipeService
UnitConversionService
Controller
Repository
DTO
Request/Response contract
Entity Configuration
Migration
```

trừ khi chỉ cần đọc để hiểu contract hiện tại.

Nếu phát hiện bug business logic bên ngoài SeedAll:

Không tự sửa.

Hãy báo:

```text
OUT_OF_SCOPE_BUSINESS_LOGIC_FOUND
```

và giải thích ngắn gọn.

---

# XVII. LOGIC MẪU MONG MUỐN

SeedData sau khi chuẩn hóa phải thể hiện được tư duy sau:

```text
Ingredient
    │
    ├── Coffee
    │      BaseUnit = g
    │
    │      SupplierPackage
    │          Package = Bag
    │          ContentUnit = g
    │          PackageQuantity = 1000
    │
    ├── Syrup
    │      BaseUnit = ml
    │
    │      SupplierPackage
    │          Package = Bottle
    │          ContentUnit = ml
    │          PackageQuantity = 750
    │
    └── Cup Lid
           BaseUnit = pcs

           SupplierPackage
               Package = Bag
               ContentUnit = pcs
               PackageQuantity = 100
```

Trong khi PhysicalUnitConversion chỉ chứa những conversion thực sự mang ý nghĩa vật lý:

```text
kg -> g = 1000
L -> ml = 1000
```

---

# XVIII. YÊU CẦU INSPECT TRƯỚC KHI CHỈNH SỬA

Trước khi code, hãy đọc toàn bộ phần liên quan đến:

```text
SeedAll
Unit
Ingredient
Supplier
SupplierPackage
PhysicalUnitConversion
IngredientUnitConversion
```

để xác định:

1. Unit nào hiện đang tồn tại.
2. Ingredient nào đang dùng BaseUnit sai.
3. SupplierPackage nào đang dùng ContentUnit khác BaseUnit.
4. PackageQuantity hiện đang được hiểu như thế nào.
5. PackageUnit hiện được lưu ở field nào.
6. Có global conversion package nào đang tồn tại không.
7. SeedAll đang dùng Add/Update/Upsert hay helper nào.
8. Business key hiện tại để chống duplicate là gì.
9. Những record nào chắc chắn là SeedData.
10. Những record nào có semantics mơ hồ cần `NEEDS_REVIEW`.

Không sửa code dựa trên suy đoán nếu có thể inspect implementation hiện tại để xác nhận.

---

# XIX. KẾT QUẢ SEED MONG MUỐN

Sau khi hoàn tất, dữ liệu demo tối thiểu phải đạt:

```text
Cup Lid       -> pcs
Cup           -> pcs
Straw         -> pcs

Coffee        -> g

Milk          -> ml
Syrup         -> ml
```

SupplierPackage tương ứng:

```text
ContentUnitId = Ingredient.BaseUnitId
```

và:

```text
PackageQuantity
```

đã được normalize về BaseUnit.

Ví dụ:

```text
100 lids       -> 100 pcs
1000 cups      -> 1000 pcs
1 kg coffee    -> 1000 g
750 ml syrup   -> 750 ml
1 L milk       -> 1000 ml
```

---

# XX. TEST VÀ VERIFY — THỰC HIỆN CUỐI CÙNG

Chỉ chạy test sau khi đã hoàn tất việc inspect và chỉnh sửa SeedAll.

Không ưu tiên viết test trước việc sửa SeedData trong task này.

Chạy focused tests theo convention/SkillTest hiện tại của project.

Verify tối thiểu:

### Test 1 — Count items

```text
Cup Lid.BaseUnit = pcs
Cup.BaseUnit = pcs
Straw.BaseUnit = pcs
```

### Test 2 — Weight ingredient

```text
Coffee.BaseUnit = g
```

### Test 3 — Volume ingredient

```text
Milk.BaseUnit = ml
Syrup.BaseUnit = ml
```

### Test 4 — SupplierPackage ContentUnit

Với các target demo seed:

```text
SupplierPackage.ContentUnitId
==
Ingredient.BaseUnitId
```

### Test 5 — Normalized PackageQuantity

Kiểm tra các package representative:

```text
1 kg coffee -> 1000 g

750 ml syrup -> 750 ml

100 lids -> 100 pcs

1000 cups -> 1000 pcs
```

### Test 6 — Không có package global conversion

Không được tồn tại SeedData kiểu:

```text
carton -> pcs
pack -> pcs
box -> pcs
bottle -> ml
can -> ml
```

nếu đây chỉ là SupplierPackage relationship.

### Test 7 — Physical conversions hợp lệ

Các conversion chuẩn vẫn hoạt động:

```text
kg -> g
L -> ml
```

### Test 8 — Procurement readiness

Mỗi seeded SupplierPackage có:

```text
IsActive = true
```

phải có đủ:

```text
Ingredient
Supplier
ContentUnit
PackageQuantity > 0
Price hợp lệ
Scope hợp lệ
```

### Test 9 — Idempotency

Chạy SeedAll nhiều lần và xác minh không tạo duplicate:

```text
UnitConversion
IngredientUnitConversion
SupplierPackage
```

### Test 10 — Không đụng Migration/Configuration

Kiểm tra Git diff cuối task.

Không được có thay đổi trong:

```text
Migrations/
*Migration.cs
*ModelSnapshot.cs

EntityConfigurations/
IEntityTypeConfiguration<>
OnModelCreating configuration
```

nếu task không yêu cầu.

---

# XXI. BÁO CÁO CUỐI TASK

Sau khi hoàn thành, báo ngắn gọn:

```text
SEED_BASE_UNITS_NORMALIZED

COUNT_ITEMS_USE_PCS_BASE_UNIT

WEIGHT_ITEMS_USE_GRAM_BASE_UNIT

VOLUME_ITEMS_USE_MILLILITER_BASE_UNIT

SUPPLIER_PACKAGE_CONTENT_DEFAULTS_TO_BASE_UNIT

PACKAGE_QUANTITY_SEEDED_IN_BASE_UNIT

NO_PACKAGE_TO_PHYSICAL_GLOBAL_CONVERSION_CREATED

PHYSICAL_UNIT_CONVERSIONS_PRESERVED

ACTIVE_SEEDED_PACKAGES_ARE_PROCUREMENT_READY

SEED_IS_IDEMPOTENT

NO_MIGRATION_CHANGED

NO_ENTITY_CONFIGURATION_CHANGED

NO_BUSINESS_LOGIC_OUTSIDE_SEED_SCOPE_CHANGED

FOCUSED_TESTS_PASSED
```

Nếu có Ingredient chưa xác định được semantics:

```text
NEEDS_REVIEW
```

kèm danh sách cụ thể.

Nếu phát hiện vấn đề ngoài SeedAll:

```text
OUT_OF_SCOPE_BUSINESS_LOGIC_FOUND
```

nhưng không tự sửa.

---

# XXII. KẾT QUẢ CUỐI CÙNG CẦN ĐẠT

Thiết kế SeedAll phải tuân theo nguyên tắc:

```text
BaseUnit
=
đơn vị cơ sở dùng để tồn kho / tiêu hao Ingredient
```

```text
PhysicalUnitConversion
=
quy đổi đo lường vật lý có tính phổ quát
```

```text
SupplierPackage
=
quy cách đóng gói thực tế khi mua Ingredient từ Supplier
```

Do đó:

```text
g
ml
pcs
```

là các BaseUnit ưu tiên theo semantics Ingredient.

Trong khi:

```text
bag
pack
carton
box
bottle
can
```

chủ yếu mô tả package/procurement và **không được biến thành global physical conversion chỉ để phục vụ SeedData**.

Toàn bộ thay đổi phải tập trung vào **SeedAll và SeedData**, không thay đổi Migration hoặc Entity Configuration.
