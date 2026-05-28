# .NET Clean N-Tier Architecture & Security Rules

To ensure a robust, highly secure, and enterprise-grade backend code standard, all C# development inside CafeChain must follow these architectural laws.

---

## 1. Clean N-Tier Architecture & The "Thin Controller" Rule
We enforce a strict separation of concerns using a classic N-Tier model:
`Presentation (Razor Views/Controllers) -> Application (DTOs/Services/Interfaces) -> Infrastructure/Data (DbContext/Repositories/Entities)`.

### Core Coding Laws:
1. **No DbContext in Controllers**: Controllers must never inject `AppDbContext` or interact with EF Core queries directly.
2. **Lean Actions**: Controllers should only handle HTTP routing, request parsing, input model-state checks, calling application services, and returning Views or unified JSON payloads.
3. **No Direct Entity Exposure (Rule 1)**: Do not return raw EF Entities (`Staff`, `Account`) directly to Controllers or Views. Doing so exposes private schema columns, increases overposting risks, and triggers circular reference loops in JSON serializations.
4. **Service Mapping Mandate**: The Application Service Layer must map EF Entities to distinct DTOs or ViewModels (such as `StaffIndexVM`, `StaffFormMasterDataVM`) before returning them to the Controller.

#### Bad Practice (Direct DB Access & Entity Leak):
```csharp
public class AdminStaffController : Controller
{
    private readonly AppDbContext _context; // ❌ VIOLATION: DbContext inside controller
    
    public async Task<IActionResult> Create(Staff staff) // ❌ VIOLATION: Direct Entity Model Binding
    {
        _context.Staffs.Add(staff); // ❌ VIOLATION: DB writing in controller
        await _context.SaveChangesAsync();
        return View(staff);
    }
}
```

#### Good Practice (Separation of Concerns):
```csharp
public class AdminStaffController : Controller
{
    private readonly IAdminStaffService _staffService; // ✅ Injecting Interface Service

    public async Task<IActionResult> Create(StaffCreateVM model) // ✅ Using safe ViewModels
    {
        if (!ModelState.IsValid) return View(model);
        
        var result = await _staffService.CreateStaffAsync(model);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Thêm nhân viên thành công!";
            return RedirectToAction("Index");
        }
        
        ModelState.AddModelError(string.Empty, result.Message);
        return View(model);
    }
}
```

---

## 2. Zero-Trust Security Patterns
Our backend operates on a "Zero-Trust" model. The client browser is untrusted and hostile.

### A. Anti-IDOR (Insecure Direct Object Reference)
Never rely on client-supplied IDs (e.g., hidden inputs like `<input type="hidden" name="AccountId" />` or query string parameters) to perform sensitive mutations like editing a profile, changing passwords, or clocking in.
- **Action**: Always resolve the user identity from the server-side identity cookie claims:
```csharp
var accountIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
if (string.IsNullOrEmpty(accountIdClaim) || !int.TryParse(accountIdClaim, out int loggedInAccountId))
{
    return Unauthorized();
}
```

### B. Decentralized Scope Overriding (Rule 5)
If a user is logged in as a `Store Manager`, their data modification scope is hard-locked to their store.
- **Action**: The Service Layer must read the current user's claims. If their claim contains a `StoreId` (because they are a Store Manager), the backend must **override** any user-supplied `StoreId` in the request with their claim value, and throw an `UnauthorizedAccessException` if they attempt to modify roles beyond their authorization matrix (e.g., adding a Super Admin).

### C. CSRF Protection for AJAX (Rule 3)
All AJAX, Fetch, or POST submissions must be guarded against Cross-Site Request Forgery.
- **Action**: Inject `@Html.AntiForgeryToken()` inside Razor views. Include the token in the AJAX header and tag the controller action with `[ValidateAntiForgeryToken]`:
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> ToggleStatus(int id) { ... }
```

---

## 3. strategic Database Transactions (Rule 4)
When writing complex relations (such as clear-and-replace actions for Phone/Address lists associated with a Staff profile), standard loops that trigger multiple EF updates can lead to incomplete data commits or locking.
- **Action**: Use a strict database transaction wrapper. Clear the child lists first using `RemoveRange`, then add the updated inputs using `AddRangeAsync` within the transaction bounds:
```csharp
public async Task<ServiceResult> UpdateStaffShiftsAsync(int staffId, List<StaffShiftDto> newShifts)
{
    using var transaction = await _context.Database.BeginTransactionAsync();
    try
    {
        // 1. Fetch current shifts and clear
        var oldShifts = await _context.StaffShifts
            .Where(x => x.StaffId == staffId)
            .ToListAsync();
        _context.StaffShifts.RemoveRange(oldShifts);

        // 2. Clear empty records (Rule 6)
        var validatedShifts = newShifts.Where(x => x.ShiftId > 0).ToList();

        // 3. Add new shifts
        await _context.StaffShifts.AddRangeAsync(validatedShifts);

        await _context.SaveChangesAsync();
        await transaction.CommitAsync(); // Commit transaction safely
        return ServiceResult.Success("Cập nhật ca làm việc thành công.");
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync(); // Rollback on error
        return ServiceResult.Failure($"Lỗi giao dịch: {ex.Message}");
    }
}
```

---

## 4. HTML Model Binding for Dynamic Arrays (Rule 2)
When building Razor views that submit list inputs dynamically managed via JavaScript (e.g., list of phone numbers or list of shifts):
- **Cú pháp Index**: The inputs rendered must use explicit indexes to match the default MVC model binder.
- **Form Pattern**: `Phones[0]`, `Phones[1]`, `Phones[2]`.
- **Cấm tuyệt đối**: Using empty bracket nomenclature like `name="Phones[]"`. This causes the binder to receive `null` or break binding.

---

## 5. Defensive Programming & Error Handling
- Never leak raw stack traces to the user interface.
- For standard Razor views: set error states in `TempData["ErrorMessage"]` and redirect gracefully to safe pages.
- For AJAX/APIs: always return a unified JSON payload matching:
```json
{
  "success": false,
  "message": "Chi tiết thông điệp lỗi (Tiếng Việt).",
  "errorCode": "OPTIONAL_ERROR_CODE"
}
```
