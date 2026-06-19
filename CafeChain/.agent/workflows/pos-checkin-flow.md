# Active Shift-Locked POS Check-in & Unlock Workflow

---
version: 2.0
last_verified: 2026-05-26
depends_on:
  - docs/module-4-pos-locked-guard.md
  - docs/module-2-staff-hub.md
scope: POS ENTRY GUARD + LEADER ELEVATION (depends on StaffHub attendance)
---

This document defines the zero-trust workflow ensuring only physically present, biometrically checked-in cashiers and authorized shift leaders can unlock and process sales at CafeChain POS terminals.

---

## 1. Flow Design: Zero-Trust Guard on POS
No F&B staff member can access POS transactions without an actively verified work state.

```mermaid
sequenceDiagram
    actor Cashier as Cashier/Thu ngân
    participant POSView as POS UI Station
    participant AttendanceService as IAttendanceActionService
    participant DB as AppDbContext (StaffShifts)
    actor Leader as Shift Leader (Ca trưởng)

    Cashier->>POSView: Click "Vào máy POS"
    POSView->>AttendanceService: Query Active Checked-In State (AccountId)
    AttendanceService->>DB: Scan StaffShifts where actualCheckIn today is NOT null & actualCheckOut IS null
    
    alt No Checked-in Shift Found
        DB-->>POSView: Status = NOT_CHECKED_IN
        POSView-->>Cashier: Lock Screen: "Bạn phải Chấm Công Face ID tại StaffHub trước!"
    else Checked-In Shift Found but Assigned Store mismatch
        DB-->>POSView: Status = STORE_MISMATCH
        POSView-->>Cashier: Lock Screen: "Thiết bị POS thuộc Store khác ca làm việc của bạn."
    else Checked-In Shift Validated
        DB-->>POSView: Status = VALID_SESSION
        POSView-->>Cashier: Unlock POS Interface & Bind Cashier ID to Sales Invoice Model
    end

    rect rgb(255, 240, 245)
        Note over POSView: Restricted Action (e.g. Void Invoice / High Voucher Override)
        Cashier->>POSView: Trigger restricted void action
        POSView->>POSView: Prompt authorization bypass modal
        Leader->>POSView: Quick Face ID verification / PIN Entry
        POSView->>AttendanceService: Validate Shift Leader Biometric Claim
        alt Bypass Approved
            AttendanceService-->>POSView: Return Elevation Token (Commit Sale)
            POSView-->>Cashier: Complete transaction with Leader audit log tag
        else Bypass Rejected
            AttendanceService-->>POSView: Elevation Rejected
            POSView-->>Cashier: Action Blocked
        end
    end
```

---

## 2. Technical Validation Logic (Zero-Trust Guard Clause)

To prevent cashiers from manipulating POST payloads to bypass the check-in requirement, the POS controller actions must contain the following verification check:

```csharp
[HttpPost("CommitSale")]
public async Task<IActionResult> CommitSale([FromBody] SaleTransactionDto saleDto)
{
    var accountIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(accountIdClaim) || !int.TryParse(accountIdClaim, out int loggedInAccountId))
    {
        return Unauthorized(new { success = false, message = "Chưa đăng nhập." });
    }

    // 1. Core Guard: Is the staff member actively checked-in right now?
    var activeShift = await _context.StaffShifts
        .Include(s => s.Shift)
        .FirstOrDefaultAsync(s => s.Staff.AccountId == loggedInAccountId 
                             && s.ActualCheckIn != null 
                             && s.ActualCheckOut == null);

    if (activeShift == null)
    {
        return BadRequest(new { 
            success = false, 
            message = "Giao dịch bị chặn! Bạn không nằm trong ca làm việc tích cực nào. Hãy chấm công trước." 
        });
    }

    // 2. Geofence Guard: Is the sale being processed in the correct store location?
    if (activeShift.Staff.StoreId != saleDto.StoreId)
    {
        return BadRequest(new { 
            success = false, 
            message = "Sai phạm bảo mật! Cửa hàng giao dịch không khớp với địa điểm chấm công của bạn." 
        });
    }

    // Process sale transaction ...
    return Ok(new { success = true, message = "Giao dịch đã được ghi nhận." });
}
```

---

## 3. Shift Leader privileged Elevation Override
If the transaction requires overriding (for example: voiding an item or applying a massive discount), the Cashier can summon a Shift Leader to authenticate on the spot.

1. **Elevation Modal**: Pops up requesting a Face Scan or 4-digit PIN matching the Shift Leader assigned to the same store.
2. **AJAX Elevation Payload**:
   ```json
   {
     "action": "PrivilegedElevation",
     "targetInvoiceId": 10594,
     "leaderPin": "9988",
     "storeId": 4
   }
   ```
3. **Audit Logging**: The server verifies that the entered credentials belong to a checked-in Shift Leader at the same store location. The transaction is then logged in `InvoiceAuditLogs` with both `CashierStaffId` and `ApproverStaffId` references, satisfying rigorous anti-fraud auditing guidelines.
