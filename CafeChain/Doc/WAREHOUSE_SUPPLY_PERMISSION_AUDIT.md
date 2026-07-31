# Audit phân quyền Kho & Cung ứng CafeChain

> **Mục đích:** ghi lại baseline đã inspect và permission chốt cho đợt refactor
> ngày 30/07/2026. Cột “Role hard-code trước refactor” mô tả nợ kỹ thuật tìm thấy,
> không phải cơ chế được phép giữ lại. Source/runtime sau refactor là chuẩn cuối.

## 1. Nguyên tắc chốt

```text
Account hợp lệ
→ Permission
→ EffectiveStoreIds
→ validate requested Store/resource
→ query trong scope
→ business state validation
→ mutation + audit/idempotency
```

- Permission trả lời **được gọi action nào**.
- StaffScope trả lời **được xem/thao tác Store nào**.
- Business rule trả lời **trạng thái có cho phép chuyển bước không**.
- `storeId` trong URL/query/form/JSON chỉ là requested value; backend phải kiểm
  với EffectiveStoreIds trước khi query.
- Default scope không có Owner/SystemAdmin bypass. Scope
  `ReorderSuggestion` là ngoại lệ duy nhất: SystemAdmin active thấy mọi Store
  Active; Owner/Manager vẫn theo StaffScope.
- Account permission override `Deny` thắng role grant.

## 2. Menu thực tế trong `_AdminLayout.cshtml`

Parent **Kho & Cung ứng** chỉ hiển thị khi có ít nhất một permission View dưới
đây. Mỗi link tiếp tục kiểm permission riêng.

| Menu thực tế | Controller | Permission hiển thị chốt |
| --- | --- | --- |
| Tồn kho cửa hàng | `AdminStoreInventory` | `Inventory.View` |
| Ngưỡng tồn kho | `AdminInventoryThresholds` | `InventoryThreshold.View` |
| Cảnh báo kho | `AdminStockAlerts` | `StockAlert.View` |
| Quản lý đá theo ca | `AdminOperationalIce` | `OperationalIce.View` |
| Yêu cầu nhập hàng | `AdminRestockRequests` | `Restock.View` |
| Gợi ý nhập hàng | `AdminReorderSuggestions` | `ReorderSuggestion.View` |
| Đề nghị mua hàng | `AdminPurchaseAdvices` | `PurchaseAdvice.View` |
| Tổng hợp đề nghị mua | `AdminPurchaseAdviceConsolidation` | `PurchaseAdviceConsolidation.View` |
| Đơn đặt hàng gộp | `AdminPurchaseOrderBatches` | `PurchaseOrder.ViewBatch` |
| Đơn đặt hàng | `AdminPurchaseOrders` | `PurchaseOrder.View` |
| Chất lượng nhà cung cấp | `AdminSupplierQuality` | `SupplierQuality.View` |
| Thông báo kho | `AdminNotifications` | `Notification.View` |
| Nguyên liệu | `AdminIngredient` | `Ingredient.View` |
| Đơn vị & quy đổi | `AdminUnitConversion` | `UnitConversion.View` |
| Nhà cung cấp | `AdminSupplier` | `Supplier.View` |

Không tự nhập các module sau vào parent:

- Bán thành phẩm, Công thức/BOM và Lệnh sơ chế đang ở nhóm sản xuất riêng.
- Phiếu kho và Chuyển kho đang ở nhóm “Phiếu kho” riêng.
- Nhận hàng chi nhánh (`AdminBranchReceipts`) là deep-link từ PO/Restock, không
  có menu con độc lập.

## 3. Bảng audit controller/action

### 3.1 Tồn kho, ngưỡng, cảnh báo và đá theo ca

| Menu/module | Controller/action | HTTP | Permission trước refactor | Role hard-code trước refactor | StaffScope baseline | Permission chốt |
| --- | --- | --- | --- | --- | --- | --- |
| Tồn kho | `AdminStoreInventory.Index`, `Transactions` | GET | Chưa có class permission ở baseline đầu | Không phải gate chính | Resolver Store | `Inventory.View` |
| Ngưỡng tồn | `AdminInventoryThresholds.Index` | GET | `InventoryThreshold.View` | `UserCanEditThreshold()` suy từ role cho UI | Store resolver | `InventoryThreshold.View` |
| Ngưỡng tồn | `Update` | POST | `InventoryThreshold.Update` | Helper role kiểm lại dù đã có permission | Kiểm Store trong service | `InventoryThreshold.Update` |
| Cảnh báo | `Index`, `Details` | GET | `StockAlert.View` | `CanView`, `CanManage`, `ViewBag.IsStoreManager` từ role | Store resolver/query | `StockAlert.View` |
| Cảnh báo | `Confirm`, `Reject`, `Close` | POST | `StockAlert.Resolve` | `CanManage` theo Owner/Manager/SystemAdmin | Có Store check | `StockAlert.Resolve` |
| Cảnh báo | `CreateRestockRequest` | POST | `StockAlert.CreateRestockRequest` | Role-derived UI/action helper | Có Store check | `StockAlert.CreateRestockRequest` |
| Đá theo ca | `Index`, `Details`, `Report`, `DownloadReport` | GET | Kiểm runtime `OperationalIce.View` | Không có role gate chính | Permission service nhận target Store | `OperationalIce.View` |
| Đá theo ca | `SavePolicy` | POST | `OperationalIce.Policy` trong method | Không | Store-specific | `OperationalIce.Policy` |
| Đá theo ca | Các action mở/link/sync/đổi/cancel/close allocation | POST | `OperationalIce.Manage` trong method | Role chỉ xuất hiện khi chọn audience thông báo | Store/unit context | `OperationalIce.Manage` |
| Đá theo ca | Quyết định supplemental/variance | POST | `OperationalIce.Approve` hoặc `Manage` theo action | Không dùng làm cổng chính | Store/unit context | Giữ permission tương ứng |

### 3.2 Gợi ý và yêu cầu nhập hàng

| Menu/module | Controller/action | HTTP | Permission trước refactor | Role hard-code trước refactor | StaffScope baseline | Permission chốt |
| --- | --- | --- | --- | --- | --- | --- |
| Gợi ý nhập | `AdminReorderSuggestions.Index` | GET | `ReorderSuggestion.View` | Layout và authorization service còn role allow-list | Generic resolver có SystemAdmin global | `ReorderSuggestion.View` + purpose `ReorderSuggestion` |
| Gợi ý nhập | `Explain` | POST | Kế thừa `ReorderSuggestion.View` | `CanViewAsync` kiểm role | Generic resolver | `ReorderSuggestion.View` + purpose `ReorderSuggestion` |
| Gợi ý nhập | `Confirm` | POST | `Restock.Create` | `CanConfirmAsync` kiểm role | Generic resolver + confirmation check | `Restock.Create` + purpose `ReorderSuggestion` |
| Yêu cầu nhập | `Index`, `Details` | GET | `Restock.View` | `CanView`, `CanWarehouse...` từ role để dựng UI | Một số service có Owner/SystemAdmin bypass | `Restock.View` + default StaffScope |
| Yêu cầu nhập | `CreateManual` GET/POST | GET/POST | `Restock.Create` | Role allow-list rộng | Default resolver | `Restock.Create` |
| Yêu cầu nhập | `CreateCentralPlanner` GET/POST | GET/POST | Dùng chung `Restock.Create` | Kế toán kho/Owner/SystemAdmin | Default resolver | **`Restock.CreateCentralPlan`** |
| Yêu cầu nhập | `CheckActive` | GET | Chỉ class `Restock.View` | Role-derived create helper | Store/ingredient | `Restock.Create` |
| Yêu cầu nhập | `Submit` | POST | `Restock.Submit` | UI role helper | Resource Store | `Restock.Submit` |
| Yêu cầu nhập | `StartProcessing` | POST | `Restock.Approve` | UI role helper | Resource Store | `Restock.Approve` |
| Yêu cầu nhập | `Reject`, `Cancel` | POST | `Restock.Reject`/`Restock.Cancel` | UI role helper | Resource Store | Giữ permission riêng |
| Yêu cầu nhập | `CloseRemaining` | POST | `Restock.CloseRemaining` | Owner/SystemAdmin helper | Resource Store | `Restock.CloseRemaining` |
| Yêu cầu nhập | `AddDemand`, `LinkFulfillment`, `SetSourcingDecision` | POST | `Restock.Update` | Role helper | Resource Store | `Restock.Update` |

`ReorderSuggestionConfirmationService` đã có các control cần giữ: suggestion
token, decision fingerprint, server-side recalculation, application lock,
Serializable transaction, RequestKey, `RequestDeduplicationService`, unit
conversion, offer/package validation và transition snapshot. Refactor permission
không được thay thế hay loại bỏ các control này.

### 3.3 Đề nghị mua, tổng hợp và đơn đặt hàng

| Menu/module | Controller/action | HTTP | Permission trước refactor | Role hard-code trước refactor | StaffScope baseline | Permission chốt |
| --- | --- | --- | --- | --- | --- | --- |
| Đề nghị mua | `AdminPurchaseAdvices.Index`, `Details` | GET | `PurchaseAdvice.View` + roles trong attribute | Permission-plus-role overload | Service có role bypass | `PurchaseAdvice.View` only |
| Đề nghị mua | `Create`, `CreateDirect` | GET/POST | `PurchaseAdvice.Create` | Class role gate gián tiếp | Requested Store/resource | `PurchaseAdvice.Create` + default StaffScope |
| Đề nghị mua | `Edit`, `AddRestockRequestToDraft` | GET/POST | `PurchaseAdvice.Update` | Class role gate | Resource Store | `PurchaseAdvice.Update` |
| Đề nghị mua | `Submit`, `StartReview`, `Reject`, `Cancel` | POST | Action-specific permission | Class role gate | Resource Store + workflow | Giữ `Submit/Review/Reject/Cancel` riêng |
| Tổng hợp PA | `AdminPurchaseAdviceConsolidation.Index` | GET | Baseline dùng `PurchaseAdvice.Consolidate` | Owner/Area/Manager/Warehouse allow-list | Resolver có thể chỉ chọn một Store | **`PurchaseAdviceConsolidation.View`** |
| Tổng hợp PA | `Preview`, `Consolidate` | POST | `PurchaseAdvice.Consolidate` | Class role gate | Phải kiểm **mọi** line Store | `PurchaseAdvice.Consolidate` |
| PO gộp | `AdminPurchaseOrderBatches.Index`, `Details` | GET | `PurchaseOrder.ViewBatch` + roles | Permission-plus-role overload | Baseline query `Any` Store có thể leak line khác | `PurchaseOrder.ViewBatch`; toàn bộ Store con phải trong scope |
| PO gộp | `Create` | POST | `PurchaseOrder.CreateBatch` | Class role gate | Tất cả source lines | `PurchaseOrder.CreateBatch` |
| PO gộp | `Approve`, `Cancel` | POST | `PurchaseOrder.Approve/Cancel` | Class role gate | Batch/resource scope | Giữ permission riêng |
| PO gộp | `GeneratePdf`, `DownloadRevision`, `PrintRevision` | POST/GET | `PurchaseOrder.Export` | Service còn role business gate | Batch/resource scope | `PurchaseOrder.Export` |
| PO gộp | `MarkRevisionSent` | POST | `PurchaseOrder.Send` | Service còn role gate | Batch/resource scope + RequestKey | `PurchaseOrder.Send` |
| PO | `AdminPurchaseOrders.Index`, `Details` | GET | `PurchaseOrder.View` + roles | Permission-plus-role; UI `CanX` từ role | Store filter | `PurchaseOrder.View` only |
| PO | `Create` GET/POST | GET/POST | `PurchaseOrder.Create` | Role helper | Phải kiểm Store của Restock/offer trước load | `PurchaseOrder.Create` |
| PO | `Approve`, `MarkSent` | POST | `PurchaseOrder.Approve/Send` | Role checks lặp trong transition | Resource Store | Giữ permission riêng |
| PO | `Cancel`, `CloseLineRemaining` | POST | `PurchaseOrder.Cancel/CloseRemaining` | Owner/SystemAdmin gate sau attribute | Resource Store + state | Permission tương ứng, role không là cổng chính |

### 3.4 Nhà cung cấp, chất lượng và nhận hàng

| Menu/module | Controller/action | HTTP | Permission trước refactor | Role hard-code trước refactor | StaffScope baseline | Permission chốt |
| --- | --- | --- | --- | --- | --- | --- |
| Nhà cung cấp | `AdminSupplier.Index` và read JSON | GET | `Supplier.View` + `SupplierReadRoles` | Permission-plus-role overload | Dữ liệu supplier/store chưa đồng đều | `Supplier.View` only |
| Nhà cung cấp | `Create` | POST | `Supplier.Create` + mutation roles | Owner/Warehouse/Area/Manager constants | Không phải mọi mutation có Store validation | `Supplier.Create` + default scope khi có Store |
| Nhà cung cấp | `Update`, phone/contact/offer/price/store mapping | POST | `Supplier.Update` + mutation roles | Role allow-list; StoreManager special case | Client gửi StoreId | `Supplier.Update`; validate target Store trước mutation |
| Nhà cung cấp | `ToggleStatus`, `ToggleIngredientOffer` | POST | `Supplier.ToggleStatus` + roles | Role allow-list | Supplier/offer scope | `Supplier.ToggleStatus` |
| Chất lượng NCC | `AdminSupplierQuality.Index` | GET | `SupplierQuality.View` | Không phải gate chính | Store resolver | `SupplierQuality.View` |
| Chất lượng NCC | `Create` GET/POST | GET/POST | POST có `SupplierQuality.Create`; GET thiếu ở baseline | Không | Receipt line Store | `SupplierQuality.Create` cho cả GET/POST |
| Chất lượng NCC | `Transition` | POST | `SupplierQuality.Transition` | Không | Issue Store + state | `SupplierQuality.Transition` |
| Nhận hàng | `AdminBranchReceipts.Index`, `Details` | GET | `Receipt.View` + roles | Permission-plus-role, UI helper | Store filter | `Receipt.View` only |
| Nhận hàng | `ReceivePurchaseOrder`, `Create` GET/POST | GET/POST | `Receipt.Create` | Role helper | PO/Restock/Store scope | `Receipt.Create` |
| Nhận hàng | `Edit/SavePurchaseOrderDraft` | GET/POST | `Receipt.UpdateDraft` | Role helper | Receipt Store | `Receipt.UpdateDraft` |
| Nhận hàng | `Confirm` | POST | `Receipt.Confirm` | Role helper | Receipt Store + state | `Receipt.Confirm` |
| Nhận hàng | `SupplierOptions`, `OfferOptions` | GET | Kế thừa `Receipt.View` | Không | Requested Store | `Receipt.View` + default scope |

### 3.5 Thông báo, nguyên liệu và đơn vị

| Menu/module | Controller/action | HTTP | Permission trước refactor | Role hard-code trước refactor | StaffScope baseline | Permission chốt |
| --- | --- | --- | --- | --- | --- | --- |
| Thông báo | `Index`, `UnreadCount`, `ListJson` | GET | `Notification.View` | Không | Query theo current Staff | `Notification.View` |
| Thông báo | `MarkRead`, `MarkAllRead`, `MarkReadJson` | POST | Dùng View ở baseline | Không | Notification ownership | **`Notification.MarkRead`** + antiforgery |
| Nguyên liệu | `Index`, `GetById`, `GetUnits` | GET | `Ingredient.View` | Không | Master data | `Ingredient.View` |
| Nguyên liệu | `Create`, `Update`, `ToggleStatus` | POST | `Ingredient.Create/Update/ToggleStatus` | Không | Master data/business validation | Giữ action-specific permission |
| Đơn vị/quy đổi | `Index`, `SearchIngredients` | GET | `UnitConversion.View` | Không | Master data | `UnitConversion.View` |
| Đơn vị/quy đổi | `Create`, `Edit`, `ToggleStatus` | GET/POST | Action-specific permission | Không | Conversion validation | Giữ permission riêng |
| Đơn vị/quy đổi | `Evaluate` | POST | Chỉ class View ở baseline | Không | Read-only calculation | `UnitConversion.View` nếu hoàn toàn read-only; không được ghi dữ liệu |

### 3.6 Module liên quan nhưng ở nhóm khác

| Module | Controller/action group | HTTP | Permission | Role hard-code baseline | StaffScope | Chốt |
| --- | --- | --- | --- | --- | --- | --- |
| Bán thành phẩm | `AdminPreparedItem` CRUD | GET/POST | `PreparedItem.*` | Write attribute kèm `RoleHelper` | Master data | Permission-only; giữ ở menu sản xuất |
| Công thức/BOM | `AdminRecipe` CRUD/preview | GET/POST | `Recipe.*` | Write attribute kèm `RoleHelper` | Master data/Store khi cost | Permission-only |
| Lệnh sơ chế | `AdminProductionOrder` calculate/execute | GET/POST | `ProductionOrder.*` | Không phải cổng chính | Store resolver/service | Giữ permission + scope/state |
| Phiếu kho | `AdminInventoryDocument` view/draft/submit/confirm/cancel/export | GET/POST | `InventoryDocument.*` | Một số business helper | Default StaffScope | Không nhận SystemAdmin Reorder global |
| Chuyển kho | `AdminInventoryTransfer` view/draft/dispatch/receive/resolve/cancel | GET/POST | `InventoryTransfer.*` | Owner/SystemAdmin/Area/Manager gates rải rác | Source + destination Store | Permission-first; cả hai Store phải được phép; không nhận Reorder global |

## 4. Permission catalog và role grant chốt

| Permission | Group | Action | Role/grant tối thiểu |
| --- | --- | --- | --- |
| `ReorderSuggestion.View` | `RESTOCK` | `View` | Chủ doanh nghiệp, SystemAdmin, Quản lý chi nhánh; giữ grant hiện hữu Quản lý vùng và Kế toán kho |
| `Restock.Create` | `RESTOCK` | `Create` | Chủ doanh nghiệp, SystemAdmin, Quản lý chi nhánh; giữ Kế toán kho |
| `Restock.CreateCentralPlan` | `RESTOCK` | `CreateCentralPlan` | Chủ doanh nghiệp, SystemAdmin, Kế toán kho |
| `PurchaseAdviceConsolidation.View` | `PURCHASE_ADVICE` | `View` | Chủ doanh nghiệp, SystemAdmin, Kế toán kho |
| `Notification.MarkRead` | `STOCK_ALERT` | `MarkRead` | Đồng bộ role matrix của `Notification.View` |

`ReorderSuggestion.View` và `Restock.Create` đã tồn tại trong
`PermissionConstants`; không tạo alias. Ba code còn lại phải được dùng thống nhất
ở constants, SeedAll, controller, view và test.

Schema thực tế của `Role` không có `Code`; business key duy nhất là `Role.Name`.
Seed phải resolve bằng tên canonical từ `RoleConstants`, không hard-code
`RoleId`. Business key Permission là `Permission.Code`; RolePermission unique
theo `(RoleId, PermissionId)`.

## 5. Role hard-code phải loại bỏ

Các dạng đã tìm thấy:

- `RequirePermission(permission, "role1,role2")` trong PurchaseAdvice, PO,
  PO Batch, Supplier và BranchReceipt.
- `User.IsInRole(...)` và helper `CanView`, `CanManage`, `CanWarehouseActions`,
  `CanCentralPlan`, `CanCancel`, `CanCreateReceipt` trong controller/Razor.
- Owner/SystemAdmin global bypass trong generic ScopeAuthorizationService,
  AdminStoreScopeResolver và các service Restock/PA/PO.
- Role list truyền vào legacy overload Reorder service/authorization.
- Razor menu Reorder dùng role business list ngoài permission.

Thay thế bằng effective permission ở boundary và default/purpose-specific scope
ở service. Không đổi business validation như state machine, người tạo không tự
duyệt, rowversion, posting/cancel rule, supplier/package/unit validation, audit
và idempotency.

## 6. Role usage được phép giữ

Role chỉ còn hợp lệ khi:

- Seed ánh xạ role → permission.
- Resolver purpose `ReorderSuggestion` nhận diện SystemAdmin để cấp global
  Active Store đúng module.
- Chọn **đối tượng nhận thông báo** theo trách nhiệm ca/kho.
- Một separation-of-duties thật sự chưa biểu diễn được bằng permission; trường
  hợp này phải được ghi tên policy và có test riêng, không viết rải rác.

Việc dùng role để suy ra `CanWrite`/`CanManage` chung không được giữ.

## 7. Cross-store và tampering checklist

- Resolve actor/account trước.
- Lấy effective permission, bao gồm account override.
- Lấy toàn bộ EffectiveStoreIds theo đúng purpose.
- Nếu request có một hoặc nhiều Store ngoài tập cho phép: trả 403/validation,
  không silently lấy phần giao, ghi AuditLog với route, actor, requested IDs và
  reason; không log dữ liệu nhạy cảm.
- Không load Store/entity ngoài scope để quyết định trả NotFound.
- Với batch/consolidation/transfer: kiểm **mọi** Store nguồn/đích/line, không chỉ
  `Any`.
- Export, JSON lookup, modal, dropdown và option endpoints dùng cùng scope với
  Index.
- SystemAdmin Reorder không thấy Store inactive.
- Restock manual/central, PO, receipt, inventory document, transfer, Dashboard,
  order và revenue không nhận global scope Reorder.

## 8. Test acceptance

- Ba role bắt buộc mở Reorder Index; role không grant và override Deny nhận 403.
- Owner/Manager tuân thủ StaffScope; sửa Store URL/body/resource bị chặn và có
  audit.
- SystemAdmin xem mọi Store Active trong Reorder, không Store inactive; cùng
  account không tự global ở module khác.
- Nút Confirm chỉ render với `Restock.Create`; gọi API trực tiếp thiếu quyền 403.
- StoreManager có `Restock.Create` nhưng không có
  `Restock.CreateCentralPlan` phải nhận 403 ở CentralPlanner.
- Cùng RequestKey/replay/concurrent confirm chỉ tạo một request, detail và
  transition.
- Consolidation/PO Batch/Supplier mutation/lookup/export không leak cross-store.
- Seed chạy hai lần không duplicate Permission/RolePermission; không đổi
  AccountPermissionOverride và grant ngoài scope; báo cáo RoleName,
  PermissionCode, Granted và permission count.

