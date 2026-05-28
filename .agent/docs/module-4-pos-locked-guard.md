# Module 4 Guide: POS Active-Shift Lock & Privileged Elevation

This guide details the POS lock guard implementations, transaction session binding, and Shift Leader privileged override overrides.

---

## 1. Objectives & Business Logic
1. **POS Entrance Guard**: Stop cashiers from entering the `/Pos` sales panel unless they have a validated, actively checked-in `StaffShift` running at the specific designated store.
2. **Sale Session Binding**: Lock the cashier's active shift ID to every sales invoice generated at the terminal, ensuring full traceability of drawers.
3. **Shift Leader Elevation Modal**: When restricted transactions are triggered (e.g. voiding invoices, changing base pricing, overriding voucher discounts), lock the screen and require local Shift Leader authentication (Face ID scan or 4-digit PIN bypass) before committing changes.

---

## 2. Technical Implementation Architecture

### A. POS Access Controller Guard
Implement server-side action filters to block unauthorized access to the POS index:

```csharp
[Authorize(Roles = "Thu ngân, Ca trưởng")]
public async Task<IActionResult> Index()
{
    var accountIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(accountIdStr) || !int.TryParse(accountIdStr, out int accountId))
    {
        return Unauthorized();
    }

    // Guard Clause: Scan for an active shift
    var activeShift = await _context.StaffShifts
        .Include(s => s.Staff)
        .FirstOrDefaultAsync(s => s.Staff.AccountId == accountId 
                             && s.ActualCheckIn != null 
                             && s.ActualCheckOut == null);

    if (activeShift == null)
    {
        ViewBag.LockReason = "Bạn chưa thực hiện Chấm Công Vào Ca thành công.";
        return View("POSAccessLocked");
    }

    ViewBag.ActiveShiftId = activeShift.Id;
    ViewBag.StoreName = activeShift.Staff.Store?.Name ?? "CafeChain";
    return View();
}
```

### B. Dynamic Transaction Binding (Data Layer)
Tag incoming orders with the appropriate checked-in session metadata:

```csharp
public class POSOrderCommitDto
{
    public List<POSSoldItemDto> SoldItems { get; set; } = new();
    public int StoreId { get; set; }
    public decimal SubTotal { get; set; }
}

// Inside Invoice processing service:
public async Task<ServiceResult> CommitOrderAsync(int cashierAccountId, POSOrderCommitDto dto)
{
    // Retrieve active shift again on backend to block payload spoofing
    var activeShift = await _context.StaffShifts
        .FirstOrDefaultAsync(s => s.Staff.AccountId == cashierAccountId 
                             && s.ActualCheckIn != null 
                             && s.ActualCheckOut == null);

    if (activeShift == null)
    {
        return ServiceResult.Failure("Lỗi bảo mật! Tài khoản chưa chấm công hoặc ca làm việc đã kết thúc.");
    }

    var invoice = new Invoice
    {
        StoreId = dto.StoreId,
        CashierStaffId = activeShift.StaffId,
        StaffShiftId = activeShift.Id, // Session bound!
        SubTotal = dto.SubTotal,
        CreatedAt = DateTime.UtcNow
    };

    await _context.Invoices.AddAsync(invoice);
    await _context.SaveChangesAsync();
    return ServiceResult.Success("Hóa đơn đã được lập thành công.");
}
```

### C. Shift Leader Privilege Elevation API
When a high-risk operation is requested, prompt a SweetAlert2 bypass and send a verify request:

```csharp
[HttpPost("AuthorizeBypass")]
public async Task<IActionResult> AuthorizeBypass([FromBody] LeaderBypassRequest request)
{
    // 1. Verify that the credentials belong to a designated Shift Leader (Ca trưởng) or higher
    var leader = await _context.Staffs
        .Include(s => s.Account)
        .FirstOrDefaultAsync(s => s.StoreId == request.StoreId 
                             && s.PasscodePin == request.LeaderPin); // 4-digit PIN comparison

    if (leader == null)
    {
        return BadRequest(new { success = false, message = "Mã PIN xác thực Trưởng ca không hợp lệ." });
    }

    // 2. Audit log the bypass commit
    var auditLog = new InvoiceAuditLog
    {
        InvoiceId = request.TargetInvoiceId,
        Action = request.ActionName,
        AuthorizedByStaffId = leader.Id,
        CreatedAt = DateTime.UtcNow
    };

    await _context.InvoiceAuditLogs.AddAsync(auditLog);
    await _context.SaveChangesAsync();

    return Ok(new { success = true, message = "Ủy quyền thành công." });
}

public class LeaderBypassRequest
{
    public int TargetInvoiceId { get; set; }
    public string LeaderPin { get; set; } = string.Empty;
    public int StoreId { get; set; }
    public string ActionName { get; set; } = string.Empty;
}
```
