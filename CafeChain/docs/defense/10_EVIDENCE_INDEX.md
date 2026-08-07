# Chỉ mục bằng chứng

## Quy ước

- `CODE_CONFIRMED`: đã đọc trực tiếp source/config/schema.
- `RUNTIME_CONFIRMED`: đã kiểm tra local/demo read-only ngày 08/08/2026.
- `NOT_RUNTIME_VERIFIED`: chưa có phiên đăng nhập tương tác phù hợp.
- Đường dẫn tính từ repository root.

## Kiến trúc và bảo mật

| Chủ đề | Kết luận | Loại | File/Class/Route | Runtime |
|---|---|---|---|---|
| Backend | ASP.NET Core MVC/API .NET 8 | CODE_CONFIRMED | `CafeChain/CafeChain.csproj`, `CafeChain/Program.cs` | App start PASS |
| Admin UI | Razor MVC Areas | CODE_CONFIRMED | `CafeChain/Areas/Admin`, route Areas | Login page PASS |
| POS UI | React 19 + TypeScript + Vite | CODE_CONFIRMED | `CafeChain.Frontend/package.json`, `src/App.tsx` | NOT_RUN |
| Database | SQL Server + EF Core 8 | CODE_CONFIRMED | `DatabaseServiceExtensions.cs`, `AppDbContext.cs` | Connection/query PASS |
| Web auth | Cookie, secure/HttpOnly/SameSite | CODE_CONFIRMED | `AuthenticationServiceExtensions.cs` | Admin anonymous 302 PASS |
| POS auth | JWT bearer | CODE_CONFIRMED | `AuthenticationServiceExtensions.cs`, API controllers | Anonymous API 401 PASS |
| Permission | Dynamic `PermissionRequirement` | CODE_CONFIRMED | `Application/Authorization/PermissionRequirement.cs` | Covered by targeted tests |
| Scope | Store scope resolved/revalidated | CODE_CONFIRMED | `Application/Services/Admin/StoreScope/AdminStoreScopeResolver.cs` | NOT_RUNTIME_VERIFIED by role |
| Menu visibility | Effective permissions drive menu | CODE_CONFIRMED | `Areas/Admin/Views/Shared/_AdminLayout.cshtml` | NOT_RUNTIME_VERIFIED |
| SignalR | Order/payment/print/inventory/workshift hubs | CODE_CONFIRMED | `Extensions/Pipeline/EndpointRouteExtensions.cs` | NOT_RUN |
| Workers | Order/payment cleanup workers only | CODE_CONFIRMED | `Extensions/Services/WorkerServiceExtensions.cs` | Startup logs PASS |
| Inventory recovery | Retry-safe call exists; dedicated durable worker not proven | CODE_CONFIRMED | `POSOrderController.DeductInventorySafeAsync`, `PayOSWebhookProcessor` | NOT_RUNTIME_VERIFIED failure path |

## Role và permission

| Chủ đề | Kết luận | Loại | File/Class/Route | Runtime |
|---|---|---|---|---|
| Role authority | 8 role, gồm 7 nội bộ + Customer | CODE_CONFIRMED | `Application/Constants/RoleConstants.cs`, `Scripts/SeedAll.sql` | Active role accounts PASS |
| Quản lý vùng | Code name là `AreaManager` | CODE_CONFIRMED | `RoleConstants.AreaManager` | Account exists PASS |
| Owner | PO approval/global pricing | CODE_CONFIRMED | permission seed, PO/profitability controllers | Account exists; actions NOT_RUN |
| Store Manager | Restock/store/ice, không PA/PO | CODE_CONFIRMED | permission seed + controllers | 2 accounts exist; actions NOT_RUN |
| Shift Supervisor | POS/receipt/ice operations | CODE_CONFIRMED | permission seed, POS/Ice controllers | Account exists; actions NOT_RUN |
| Accountant | Supplier, source, PA/PO/PDF/send | CODE_CONFIRMED | permission seed, procurement controllers | Account exists; actions NOT_RUN |
| SystemAdmin | Platform admin, không auto business superuser | CODE_CONFIRMED | permission seed | Account exists; actions NOT_RUN |
| Sales Staff | StaffHub/POS | CODE_CONFIRMED | permission seed, POS controllers | 2 accounts exist; actions NOT_RUN |
| PO maker-checker | Creator cannot approve own PO | CODE_CONFIRMED | `PurchaseOrderService.TransitionAsync` | Permission/procurement targeted tests PASS |
| POB maker-checker | Creator cannot approve own batch | CODE_CONFIRMED | `PurchaseOrderBatchService.ApproveAsync` | Procurement targeted tests PASS |

## POS và WorkShift

| Chủ đề | Kết luận | Loại | File/Class/Route | Runtime |
|---|---|---|---|---|
| Commit route | POST `/api/v1/pos/orders/commit` | CODE_CONFIRMED | `Controllers/Api/v1/POSOrderController.cs` | 401 anonymous PASS |
| Server pricing | Catalog/recipe/topping revalidated | CODE_CONFIRMED | `POSStoreMenuSaleValidator.cs`, `POSOrderService.cs` | NOT_RUNTIME_VERIFIED |
| Order idempotency | `ClientOrderId` returns compatible existing order | CODE_CONFIRMED | `POSOrderService.CommitOrderAsync`, `Order.ClientOrderId` | DB contains keys PASS |
| Offline sync | Batch handles created/duplicate/failed separately | CODE_CONFIRMED | `POSOrderController.SyncOfflineOrders` | NOT_RUN |
| Snapshot | OrderDetail stores accepted price/catalog/recipe/ice | CODE_CONFIRMED | `Models/Orders/OrderDetail.cs` | Rows exist PASS |
| Payment | Multiple payment lines sum exactly to total | CODE_CONFIRMED | `POSOrderService.CommitOrderAsync` | Payment data exists |
| Cash shift | Expected cash updated in commit transaction | CODE_CONFIRMED | `POSOrderService.cs` | NOT_RUNTIME_VERIFIED calculation |
| WorkShift state | OPEN/CLOSING/EXPIRED/CLOSED/RECONCILIATION | CODE_CONFIRMED | `Models/Stores/WorkShift.cs` | 64 closed, 1 reconcile PASS |
| Open guards | Permission, active staff, schedule, terminal | CODE_CONFIRMED | `WorkShiftService.OpenShiftAsync` | NOT_RUN |
| Close guards | Payment/offline/cash/OTP | CODE_CONFIRMED | `WorkShiftService.CloseShiftAsync` | NOT_RUN |
| Reconcile | Request key + transaction + RowVersion | CODE_CONFIRMED | `WorkShiftService.ReconcileAsync` | Reconcile record exists |
| Blind selling | Negative inventory allowed with reconciliation | CODE_CONFIRMED | ADR-0001, `InventoryDeductionService.cs` | NOT_FORCED |

## Procurement, receipt và supplier

| Chủ đề | Kết luận | Loại | File/Class/Route | Runtime |
|---|---|---|---|---|
| Restock | Intent only, no direct inventory increase | CODE_CONFIRMED | `RestockRequest.cs`, ADR-0008 | 3 rows PASS |
| Restock route | Create/submit/process/source/add-demand | CODE_CONFIRMED | `AdminRestockRequestsController.cs` | NOT_RUN |
| Sourcing | Purchase/transfer/production/reject allocations | CODE_CONFIRMED | `RestockSourcingAllocation.cs`, services | NOT_RUN |
| PA trace | PA line links Restock/allocation and quantities | CODE_CONFIRMED | `PurchaseAdvice.cs` | 1 row PASS |
| PA state | Draft/Submitted/UnderReview and allocation states | CODE_CONFIRMED | `PurchaseAdviceConstants.cs`, service | DB has legacy APPROVED flagged |
| PO normal | One valid source can create normal PO | CODE_CONFIRMED | `AdminPurchaseOrdersController.CreateFromAdvice`, services/tests | Existing PO rows PASS |
| POB | Multiple compatible sources and child orders | CODE_CONFIRMED | `PurchaseOrderBatchService`, batch entity | NOT_RUNTIME_VERIFIED creation |
| PO maker-checker | Actor creator check before approval | CODE_CONFIRMED | `PurchaseOrderService.cs` | NOT_RUN |
| PO snapshots | package/procurement UOM, price, conversion | CODE_CONFIRMED | `PurchaseOrderLine` | Existing PO rows PASS |
| Receipt authority | Only CONFIRMED posts inventory | CODE_CONFIRMED | `BranchReceiptService.ConfirmAsync`, ADR-0008 | 5 confirmed, 1 draft PASS |
| Receipt idempotency | Confirmed replay returns previous tx IDs | CODE_CONFIRMED | `BranchReceiptService.cs` | NOT_REPLAYED |
| Accepted only | Rejected separated and not added to stock | CODE_CONFIRMED | `BranchReceiptLine`, confirm service | NOT_MUTATED |
| Supplier identity | Supplier code/name/tax/active + RowVersion | CODE_CONFIRMED | `Models/Inventories/Suppliers/Supplier.cs` | 55 rows PASS |
| Supplier store | Assignment + lead time override/active | CODE_CONFIRMED | `SupplierStore.cs`, `AdminSupplierController` | NOT_RUN |
| Supplier offer | Package and loose modes are separated | CODE_CONFIRMED | `IngredientSupplier.cs` | NOT_RUN |
| UOM compatibility | Options/conversion derived by ingredient | CODE_CONFIRMED | `UnitConversion.cs`, supplier/restock endpoints | Supplier/UOM targeted tests PASS |

## Menu, BOM, costing và pricing

| Chủ đề | Kết luận | Loại | File/Class/Route | Runtime |
|---|---|---|---|---|
| BTP identity | PreparedItem stable, Recipe versioned | CODE_CONFIRMED | `PreparedItem.cs`, `Recipe.cs`, ADR-0006 | NOT_RUN |
| FIFO | Layers ordered by created time/ID | CODE_CONFIRMED | `DrinkSizeProfitabilityQueryService.SimulateAsync` | NOT_RUN UI |
| Completeness | Missing component makes total cost incomplete | CODE_CONFIRMED | `DrinkSizeProfitabilityQueryService` | Profitability targeted tests PASS |
| Gross profit | price - cost | CODE_CONFIRMED | profitability services | Profitability targeted tests PASS |
| Margin | profit / selling price | CODE_CONFIRMED | `PriceSuggestionService.cs` | Profitability targeted tests PASS |
| Markup | profit / cost | CODE_CONFIRMED | `PriceSuggestionService.cs` | Profitability targeted tests PASS |
| Suggest preview | Calculate only, no persistence | CODE_CONFIRMED | `PriceSuggestionService`, `AdminDrinkProfitabilityController.Suggest` | NOT_RUN |
| Price update | Separate Owner action + reason/audit | CODE_CONFIRMED | `DrinkSizePricingService.cs`, controller | NOT_RUN |
| Topping policy | default, price, cost, quantity independent | CODE_CONFIRMED | `DrinkSizeToppingPolicy.cs`, service | NOT_RUN |
| Replacement mode | No general replacement constant found | UNKNOWN_NEEDS_CONFIRMATION | `DrinkProfitabilityConstants.cs` | NOT_APPLICABLE |

## Operational Ice và reporting

| Chủ đề | Kết luận | Loại | File/Class/Route | Runtime |
|---|---|---|---|---|
| Operational Shift | Separate aggregate from WorkShift | CODE_CONFIRMED | `OperationalShift.cs`, constants | 2 open rows PASS |
| Link candidate | Same store/time/state/no conflict | CODE_CONFIRMED | `OperationalIceService`, link tests | NOT_RUN interaction |
| Supplement/handoff | Dedicated actions/permissions | CODE_CONFIRMED | `AdminOperationalIceController`, constants | NOT_RUN |
| Variance out | Inventory transaction + idempotent posting | CODE_CONFIRMED | `OperationalIceService.ApproveVarianceAsync` | NOT_MUTATED |
| Negative variance | Reconcile without auto stock increase | CODE_CONFIRMED | `ReconcileVarianceAsync` | NOT_RUN |
| Dashboard access | Policy + scope | CODE_CONFIRMED | `DashboardController`, authorization | Anonymous redirect PASS |
| KPI metadata | Widget-specific metric/dimension/unit | CODE_CONFIRMED | `DashboardWidgetCatalog.cs` | NOT_RUN authenticated |

## Runtime evidence log

| Kiểm tra | Kết quả | Loại |
|---|---|---|
| Start backend `dotnet run --no-build --launch-profile http` | Listening `http://localhost:5111`; cleanup workers started | RUNTIME_CONFIRMED |
| GET `/Account/Login` | HTTP 200 | RUNTIME_CONFIRMED |
| GET `/Admin/Dashboard` anonymous | HTTP 302 tới login | RUNTIME_CONFIRMED |
| GET protected POS API anonymous | HTTP 401 | RUNTIME_CONFIRMED |
| Active role account query | Đủ 7 role nội bộ; Sales/Store có 2 account | RUNTIME_CONFIRMED |
| Entity count query | 3 stores, 55 suppliers, 3 Restock, 1 PA, 8 PO, 6 receipts, 136 orders, 65 WorkShift, 2 OperationalShift | RUNTIME_CONFIRMED |
| State distribution | WorkShift closed/reconcile; receipts draft/confirmed; PO completed/sent; legacy Restock/PA states detected | RUNTIME_CONFIRMED |
| Role-by-role browser navigation | Không có authenticated browser session trong task | NOT_RUNTIME_VERIFIED |
| Mutating demo scenarios | Không chạy để tránh đổi dữ liệu demo khi thiếu account session | NOT_RUNTIME_VERIFIED |

## Automated verification log

| Phạm vi | Exact command | Kết quả | Thời lượng |
|---|---|---:|---:|
| Permission/role/scope | `dotnet test CafeChain.Tests/CafeChain.Tests.csproj --no-restore --filter "FullyQualifiedName~AdminPermissionScopeUiSourceTests\|FullyQualifiedName~SupplyChainOperationalIcePermissionHardeningTests\|FullyQualifiedName~POSShiftSupervisorRoleWiringIssue94Tests\|FullyQualifiedName~OrderStoreScopePermissionsIssue212Tests"` | 24 pass, 0 fail, 0 skip | 45,74 giây (gồm build) |
| Procurement/Supplier/Costing/Ice | `dotnet test CafeChain.Tests/CafeChain.Tests.csproj --no-restore --no-build --filter "FullyQualifiedName~RestockProcurementRoutingIssue177Tests\|FullyQualifiedName~PurchaseOrderBatchIssue186Tests\|FullyQualifiedName~PurchaseOrderPartialReceiptIssue178Tests\|FullyQualifiedName~SupplierProcurementUomHardeningTests\|FullyQualifiedName~DrinkSizeProfitabilityFoundationTests\|FullyQualifiedName~OperationalIceWorkShiftLinkHardeningTests"` | 111 pass, 0 fail, 0 skip | 19,96 giây |
| Tổng | Hai nhóm đúng `TEST_SCOPE_PLAN`; không chạy full suite | 135 pass, 0 fail, 0 skip | 65,70 giây |
| Static docs | Required files, 58 Q&A, Mermaid fence, secret/unfinished-marker scan, `.cs` reference existence, `git diff --check` | PASS | local |

Build sinh warning nullable/obsolete sẵn có và warning `EF1002` tại `LegacyBtpConsolidationService.cs`. Task chỉ thêm Markdown nên không sửa warning ngoài scope; warning được ghi trong hạn chế.

## Quy tắc và quy trình task

| Chủ đề | Evidence |
|---|---|
| Rules/skills read | Comment `RULES_AND_SKILLS_READ` trên issue #352 |
| Test scope | Comment `TEST_SCOPE_PLAN` trên issue #352 |
| Issue | `https://github.com/TheSkibidi1712/CafeChain/issues/352` |
| Migration | None; task chỉ thêm Markdown |
| Production data | Không truy cập/sửa; chỉ local/demo read-only |
| PR/Merge | Không thực hiện |

## Các điểm chưa xác minh phải nói rõ

1. Menu/action visibility thực tế của từng account: `NOT_RUNTIME_VERIFIED`.
2. React POS click-through và offline replay thật: `NOT_RUNTIME_VERIFIED`.
3. PayOS/SMTP/Cloudinary/print bridge: `NOT_RUNTIME_VERIFIED`.
4. Full mutating Restock → Receipt demo: `NOT_RUNTIME_VERIFIED`.
5. Topping replacement tổng quát: `UNKNOWN_NEEDS_CONFIRMATION`/chưa có contract code hoàn chỉnh.
6. Durable inventory deduction worker/outbox: không tìm thấy trong worker registration hiện tại.
