# 🔄 HANDOFF — POS API Backend

> **Phiên làm việc**: 2026-06-27  
> **Dự án**: CafeChain Backend POS API (.NET 8 Web API)  
> **Repo**: `TheSkibidi1712/CafeChain`

---

## 1. Trạng thái hiện tại (Current State)

### ✅ Issues đã Close trên GitHub

| GitHub # | Title | Ghi chú |
|----------|-------|---------|
| [#62](https://github.com/TheSkibidi1712/CafeChain/issues/62) | Foundation: Migration + JWT | EF Migration `AddDrinkCategoryIcon`, JWT Bearer dual-scheme, PosApiController, ClaimsExtensions, 8 DTO files |
| [#63](https://github.com/TheSkibidi1712/CafeChain/issues/63) | Catalog APIs | `POSCatalogController` — 3 GET endpoints, Projection `.Select()` chống N+1 |
| [#67](https://github.com/TheSkibidi1712/CafeChain/issues/67) | WorkShift Management | `POSShiftController` — open/close/current, reuse `IWorkShiftService` |

### 🟡 Issues đang Open (chưa code)

| GitHub # | Title | Blocked by |
|----------|-------|------------|
| [#64](https://github.com/TheSkibidi1712/CafeChain/issues/64) | **Core Order Commit** 🔴 HITL | #62 ✅ → **UNBLOCKED** |
| [#65](https://github.com/TheSkibidi1712/CafeChain/issues/65) | Side-effects: Inventory + Print | #64 |
| [#66](https://github.com/TheSkibidi1712/CafeChain/issues/66) | Offline Batch Sync | #64 |
| [#68](https://github.com/TheSkibidi1712/CafeChain/issues/68) | Order History | #64 |
| [#69](https://github.com/TheSkibidi1712/CafeChain/issues/69) | Integration + Frontend 🔴 HITL | #63, #66, #67, #68 |

### Recovery Command

```bash
gh issue list --label pos-api --state all --json number,title,state
```

---

## 2. Quyết định kỹ thuật đã chốt (Architectural Decisions)

| Quyết định | Chi tiết |
|------------|----------|
| **Dual Auth Scheme** | Cookie (default) cho Admin MVC, JWT Bearer cho POS API. Không ảnh hưởng chéo. |
| **Claims Extraction** | `ClaimsExtensions.GetStoreId()` / `GetStaffId()` — Strict mode (throw nếu thiếu). Claim types: `"StoreId"`, `"StaffId"` — khớp `AccountService.cs` L265-268. |
| **POS API không nhận StoreId/StaffId từ request body** | Tất cả lấy từ JWT token qua `PosApiController.CurrentStoreId` / `CurrentStaffId`. |
| **Catalog queries dùng Projection** | `.Select()` để tránh N+1. Không dùng Eager Loading cho catalog endpoints. |
| **WorkShift reuse** | `POSShiftController` wrap `IWorkShiftService` đã có — không duplicate logic. |
| **DrinkCategory.Icon** | `nvarchar(10)` nullable, seed emoji: Coffee→☕, Trà sữa→🧋, Nước ngọt→🥤 |
| **JWT Config** | Issuer=`CafeChain`, Audience=`CafeChain.POS`, ExpirationHours=`12`, Key trong `appsettings.json` |
| **Inventory âm kho** | Cho phép ghi nhận âm tạm thời, đơn hàng KHÔNG bị reject vì thiếu kho, trả warning. |

---

## 3. 🚨 Nhiệm vụ ngày mai (Next Action)

### ⛔ LƯU Ý ĐỎ — HITL GATE

> **TUYỆT ĐỐI KHÔNG CODE Issue #64 ngay lập tức.**
>
> Nhiệm vụ ĐẦU TIÊN khi bắt đầu ngày mai:
>
> **Viết bản Spec/Đặc tả logic chi tiết** cho Issue #64 (Core Order Commit) để PO duyệt.

### Spec phải làm rõ 4 điểm:

1. **Idempotency** — Cơ chế chống trùng đơn qua `ClientOrderId` (GUID). Check `FindOrderByClientOrderIdAsync()` → nếu trùng → trả order cũ (200).

2. **Server-side Price Calculation** — Logic lấy giá từ DB:
   - `OrderDetail.Price` = `DrinkSize.Price` (lookup từ DB theo DrinkId + SizeId)
   - `OrderTopping.Price` = `Topping.Price` (lookup từ DB theo ToppingId)
   - **KHÔNG tin giá từ client request**

3. **DB Transaction** — Cấu trúc transaction:
   - `BeginTransaction` → Create `Order` → Create `OrderDetail[]` → Create `OrderTopping[]` → Create `Payment[]` → `CommitTransaction`
   - Rollback nếu bất kỳ step nào fail

4. **WorkShift Cash Update** — Logic cập nhật `ExpectedEndingCash`:
   - Nếu `PaymentMethodId == 1` (Cash) → `WorkShift.ExpectedEndingCash += cashAmount`
   - Update trong cùng transaction

### Sau khi PO approve spec → mới bắt đầu code.

---

## 4. Files đã tạo/sửa trong phiên này

### New Files
- `Controllers/Api/v1/PosApiController.cs` — Abstract base controller
- `Controllers/Api/v1/POSCatalogController.cs` — Catalog endpoints
- `Controllers/Api/v1/POSShiftController.cs` — Shift endpoints
- `Extensions/ClaimsExtensions.cs` — JWT claims extraction
- `Application/DTOs/POS/POSCategoryDto.cs`
- `Application/DTOs/POS/POSMenuItemDto.cs`
- `Application/DTOs/POS/POSToppingDto.cs`
- `Application/DTOs/POS/POSOrderCommitResponseDto.cs`
- `Application/DTOs/POS/ShiftSummaryDto.cs`
- `Application/DTOs/POS/POSOrderHistoryDto.cs`
- `Application/DTOs/POS/OfflineBatchSyncDto.cs`
- `Application/DTOs/POS/OpenShiftRequestDto.cs`
- `Migrations/20260627143732_AddDrinkCategoryIcon.cs`

### Modified Files
- `Models/Drinks/DrinkCategory.cs` — Added `Icon` property
- `Data/Configurations/Drinks/DrinkInfos/DrinkCategoryConfiguration.cs` — Icon config + seed
- `Program.cs` — Added `.AddJwtBearer()` dual-scheme
- `appsettings.json` — Added `Jwt` config section
- `CafeChain.csproj` — Added `Microsoft.AspNetCore.Authentication.JwtBearer 8.0.0`

---

## 5. Existing Services Available (không cần tạo mới)

| Service | Interface | Đã có logic |
|---------|-----------|-------------|
| `POSOrderService` | `IPOSOrderService` | `CommitOrderAsync()`, `GetMenuDataAsync()` |
| `WorkShiftService` | `IWorkShiftService` | `OpenShiftAsync()`, `CloseShiftAsync()`, `GetActiveShiftAsync()` |
| `POSOrderRepository` | `IPOSOrderRepository` | `FindOrderByClientOrderIdAsync()`, `BeginTransactionAsync()`, `CommitTransactionAsync()` |

> DI đã registered tại `Program.cs` L529-542.
