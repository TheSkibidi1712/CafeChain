# Module 1 Guide: Authentication & Role-Based Redirect (StaffHub Focus)

---
version: 2.0
last_verified: 2026-05-26
depends_on:
  - learnings/project-context.md
  - rules/dotnet-architecture.md
scope: REDIRECT + ATTENDANCE (IP Geofencing deferred to Module 2)
---

This guide details the **authentication pipeline**, **role-based redirect logic**, and **edge case handling** for routing staff members to the correct portal after login. IP/Geofencing concerns are **deferred to Module 2** and are NOT part of this module.

---

## 1. Objectives & Business Logic

1. **Accurate Role Redirection**: Route users to the correct portal based on their role using **exact array matching** (NOT string containment — synced with actual `AccountController.cs`).
   - `adminRoles[]` → Admin Dashboard (`/Admin/AdminStaff/Index`)
   - `kioskRoles[]` → StaffHub Portal (`/StaffHub/Index`)
   - Default → Customer Storefront (`/Home/Index`)
2. **StoreId Claim Injection**: Inject `StoreId` claim during cookie sign-in for store-bound staff.
3. **Storefront Safety**: Hide "Customer Profile" for logged-in staff to prevent null reference crashes.
4. **First-Login Password Change**: Force staff to change default password on first StaffHub access.

---

## 2. Role Classification Matrix (Synced With Codebase)

Roles defined in `Application/Constants/RoleConstants.cs`:

| Constant | Vietnamese String | Target Portal |
|---|---|---|
| `SuperAdmin` | "Super Admin" | Admin |
| `CEO` | "CEO / Ban Giám đốc" | Admin |
| `CFO` | "Kế toán trưởng / Tài chính" | Admin |
| `MarketingManager` | "Giám đốc Marketing" | Admin |
| `OperationsManager` | "Giám đốc Vận hành" | Admin |
| `HRManager` | "Quản lý Nhân sự" | Admin |
| `AreaManager` | "Quản lý Khu vực" | Admin |
| `StoreManager` | "Cửa hàng trưởng" | Admin |
| `ShiftSupervisor` | "Ca trưởng" | **StaffHub** |
| `Cashier` | "Thu ngân" | **StaffHub** |
| `WarehouseKeeper` | "Thủ kho" | **StaffHub** |
| `GeneralStaff` | "Nhân viên chung" | **StaffHub** |
| `Customer` | "Khách hàng" | Home |

---

## 3. Technical Implementation (Synced With Actual Code)

### A. RedirectByRole — Exact Array Matching Pattern

**File**: `Controllers/AccountController.cs` — Method `RedirectByRole()`

```csharp
private IActionResult RedirectByRole(string role, string? returnUrl)
{
    if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
    {
        return Redirect(returnUrl);
    }

    var adminRoles = new[]
    {
        RoleConstants.SuperAdmin,       // "Super Admin"
        RoleConstants.CEO,              // "CEO / Ban Giám đốc"
        RoleConstants.CFO,              // "Kế toán trưởng / Tài chính"
        RoleConstants.MarketingManager, // "Giám đốc Marketing"
        RoleConstants.OperationsManager,// "Giám đốc Vận hành"
        RoleConstants.HRManager,        // "Quản lý Nhân sự"
        RoleConstants.AreaManager,      // "Quản lý Khu vực"
        RoleConstants.StoreManager      // "Cửa hàng trưởng"
    };

    var staffHubRoles = new[]
    {
        RoleConstants.ShiftSupervisor,  // "Ca trưởng"
        RoleConstants.Cashier,          // "Thu ngân"
        RoleConstants.WarehouseKeeper,  // "Thủ kho"
        RoleConstants.GeneralStaff      // "Nhân viên chung"
    };

    if (adminRoles.Contains(role))
    {
        return RedirectToAction("Index", "AdminStaff", new { area = "Admin" });
    }

    if (staffHubRoles.Contains(role))
    {
        return RedirectToAction("Index", "StaffHub"); // ← Renamed from "Kiosk"
    }

    return RedirectToAction("Index", "Home");
}
```

> **CRITICAL**: Use `RoleConstants.*` instead of hardcoded Vietnamese strings. The array uses `.Contains(role)` for **exact match**, NOT `string.Contains()`.

### B. Claims Setup (Service Layer)

**File**: `Application/Services/Accounts/AccountService.cs`

The `LoginAsync()` method must inject `StoreId` claim for store-bound staff:

```csharp
var claims = new List<Claim>
{
    new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()),
    new Claim(ClaimTypes.Name, account.FullName),
    new Claim(ClaimTypes.Role, roleName)
};

// StoreId injection — null-safe with .Value
if (staff?.StoreId > 0)
{
    claims.Add(new Claim("StoreId", staff.StoreId.ToString()));
}
```

### C. Storefront Layout Guard

**File**: `Views/Shared/_Layout.cshtml`

```html
@if (User.Identity.IsAuthenticated && User.IsInRole("Khách hàng"))
{
    <li class="nav-item">
        <a class="nav-link" href="@Url.Action("Index", "Profile")">
            <i class="fas fa-user"></i> Hồ sơ của tôi
        </a>
    </li>
}
```

---

## 4. Edge Cases & Error Recovery Matrix

### A. Already-Authenticated User Tries Login Page

| Trigger | Current Handling | Status |
|---|---|---|
| User navigates to `/Account/Login` while logged in | Auto-redirect to `/Home/Index` | ✅ Handled (L92-95, L119-124) |

### B. Account Lock (Brute-Force Protection)

| Trigger | Current Handling | Status |
|---|---|---|
| 5+ failed login attempts | Anti-brute-force 800ms delay + account lock timer | ✅ Handled |
| Locked account login attempt | Redirect with `isLocked=true` + `minutes` param | ✅ Handled |
| AJAX lock check | `CheckLockStatus(email)` API endpoint | ✅ Handled |

### C. Role Not Found / Unknown Role

| Trigger | Expected Behavior | Status |
|---|---|---|
| Staff has role not in `adminRoles[]` or `staffHubRoles[]` | Falls through to `Home/Index` as customer | ⚠️ Works but unclear UX |
| New role added to DB but not to `RoleConstants.cs` | Same fallthrough — staff sees customer storefront | 🔴 **Silent failure** |

**Solution**: Add explicit fallback logging:
```csharp
// After staffHubRoles check, before default redirect:
_logger.LogWarning("Unrecognized role '{Role}' for AccountId {Id}. Defaulting to Home.",
    role, loggedInAccountId);
```

### D. Cookie Expiration Mid-Session

| Trigger | Expected Behavior | Status |
|---|---|---|
| 7-day cookie expires while using StaffHub | Next request → 302 redirect to `/Account/Login` | ✅ Auto-handled by ASP.NET |
| AJAX call when cookie expired | Returns 401 → frontend should detect & redirect | ⚠️ **Need SweetAlert handler** |

**Solution**: Global AJAX error handler for 401:
```javascript
$(document).ajaxError(function(event, xhr) {
    if (xhr.status === 401) {
        Swal.fire({
            icon: 'warning',
            title: 'Phiên hết hạn',
            text: 'Vui lòng đăng nhập lại để tiếp tục.',
            confirmButtonText: 'Đăng nhập'
        }).then(() => {
            window.location.href = '/Account/Login';
        });
    }
});
```

### E. First-Login Password Change (Staff Onboarding)

| Trigger | Expected Behavior | Status |
|---|---|---|
| New staff logs in with default password | StaffHub detects `RequiresPasswordChange` → show password modal | ✅ API exists (`FirstLoginChangePassword`) |
| Staff dismisses modal | Should be blocked from accessing StaffHub features | ⚠️ **Need frontend enforcement** |

### F. Concurrent Sessions (Same Account, Multiple Devices)

| Trigger | Expected Behavior | Status |
|---|---|---|
| Staff logs in on PC, then logs in on phone | Both sessions active (IsPersistent cookie) | ⚠️ **Currently allowed** |
| Staff checks in on PC, then tries to check in on phone | Should block: "Bạn đã chấm công rồi" | 🔴 **Need server-side guard** |

**Solution**: Add duplicate check-in guard in `SubmitTimeActionAsync`:
```csharp
// Before processing check-in:
var alreadyCheckedIn = await _context.StaffShifts
    .AnyAsync(s => s.StaffId == staff.StaffId
        && s.WorkDate == DateTime.Today
        && s.ActualCheckIn != null
        && s.ActualCheckOut == null);

if (alreadyCheckedIn && actionType == "CHECK_IN")
{
    return ServiceResult.Failure("Bạn đã chấm công vào ca hôm nay rồi!");
}
```

---

## 5. Files Affected By This Module

| File | Change Type | Description |
|---|---|---|
| `Controllers/AccountController.cs` | MODIFY | Use `RoleConstants`, rename Kiosk→StaffHub |
| `Application/Constants/RoleConstants.cs` | REFERENCE | Role string constants |
| `Application/Services/Accounts/AccountService.cs` | MODIFY | StoreId claim injection (null-safe) |
| `Views/Shared/_Layout.cshtml` | MODIFY | Storefront guard for staff |
| `Views/Shared/_Layout.cshtml` (JS) | ADD | Global 401 AJAX handler |
