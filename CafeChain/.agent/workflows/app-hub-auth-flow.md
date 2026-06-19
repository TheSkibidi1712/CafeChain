# StaffHub Auth & Role Redirect Workflow

---
version: 2.0
last_verified: 2026-05-26
depends_on:
  - docs/module-1-auth-rbac.md
scope: LOGIN → REDIRECT → STAFFHUB ACCESS (IP Geofencing DEFERRED)
---

This document specifies the end-to-end authentication routing for CafeChain employees, from login submission to StaffHub dashboard access. IP geofencing checks are **deferred** and not included in this flow.

---

## 1. Core Login → Redirect Flow

```mermaid
sequenceDiagram
    actor Employee as Nhân viên
    participant LoginPage as Account/Login
    participant Controller as AccountController
    participant Service as AccountService
    participant Cookie as Cookie Auth

    Employee->>LoginPage: Nhập Email + Password
    LoginPage->>Controller: POST /Account/Login

    Controller->>Service: LoginAsync(email, password)

    alt Account Locked (>5 attempts)
        Service-->>Controller: IsLocked=true, LockMinutes=N
        Controller-->>Employee: Redirect với ?isLocked=true&minutes=N
        Note over Employee: SweetAlert2: "Tài khoản bị khóa N phút"
    end

    alt Invalid Credentials
        Service-->>Controller: IsSuccess=false
        Controller-->>Controller: Task.Delay(800ms) // Anti-brute
        Controller-->>Employee: Show error on Login page
    end

    alt Login Success
        Service-->>Controller: Claims + Role + StoreId

        Controller->>Cookie: SignInAsync(Claims, IsPersistent)
        Note over Cookie: Cookie expires: 7 days (remember) or session

        Controller->>Controller: RedirectByRole(role, returnUrl)

        rect rgb(220, 252, 231)
            Note over Controller: Role Classification (Exact Match)
            alt adminRoles.Contains(role)
                Controller-->>Employee: 302 → /Admin/AdminStaff/Index
            else staffHubRoles.Contains(role)
                Controller-->>Employee: 302 → /StaffHub/Index
            else Default (Customer)
                Controller-->>Employee: 302 → /Home/Index
            end
        end
    end
```

---

## 2. StaffHub Dashboard Load Flow

```mermaid
sequenceDiagram
    actor Staff as Nhân viên
    participant StaffHub as StaffHubController
    participant Service as AttendanceActionService
    participant DB as Database

    Staff->>StaffHub: GET /StaffHub/Index
    StaffHub->>StaffHub: Extract AccountId from Claims

    alt No AccountId in Claims
        StaffHub-->>Staff: Redirect → /Account/Login
    end

    StaffHub->>Service: GetKioskDataAsync(accountId)
    Service->>DB: Load Staff + Today's Shifts + FaceDescriptor status

    alt Staff not found
        Service-->>StaffHub: Failure
        StaffHub-->>Staff: Redirect → /Account/Login + ErrorMessage
    end

    Service-->>StaffHub: KioskData DTO
    StaffHub-->>Staff: Render StaffHub Dashboard

    Note over Staff: Dashboard shows:
    Note over Staff: - Store name & address
    Note over Staff: - Live server-synced clock
    Note over Staff: - Today's shift schedule
    Note over Staff: - FaceID status (registered/not)
    Note over Staff: - Check-in / Check-out buttons
    Note over Staff: - POS launcher (if eligible)
```

---

## 3. Check-In Attendance Flow (FaceID)

```mermaid
sequenceDiagram
    actor Staff as Nhân viên
    participant UI as StaffHub Page
    participant Face as face-api.js
    participant API as POST /api/Attendance/SubmitTimeAction
    participant Service as AttendanceActionService
    participant DB as Database

    Staff->>UI: Click "Vào ca"
    UI->>UI: checkCameraPermission()

    alt Camera denied
        UI-->>Staff: SweetAlert2 "Cần bật Camera"
        Note over Staff: Hướng dẫn bật camera
    end

    UI->>Face: Activate camera stream
    Face->>Face: Scan 3 angles (Straight, Left, Right)
    Face-->>UI: 128-dim face descriptor vector

    UI->>API: POST accountId, actionType="CHECK_IN", faceDescriptor

    API->>Service: SubmitTimeActionAsync(accountId, "CHECK_IN", vector)

    Service->>DB: Load Staff.FaceDescriptor
    Service->>Service: CalculateFaceDistance(input, db)

    alt Distance > 0.4 (NOT matched)
        Service-->>API: Failure("Khuôn mặt không trùng khớp")
        API-->>UI: 400 Bad Request
        UI-->>Staff: SweetAlert2 error
    end

    Service->>DB: Check duplicate (already checked in today?)
    alt Already checked in
        Service-->>API: Failure("Đã chấm công rồi!")
        API-->>UI: 400
        UI-->>Staff: SweetAlert2 warning
    end

    Service->>DB: Find StaffShift (today ± 2h window + overnight)

    alt No shift found & forceSave=false
        Service-->>API: errorCode="AD_HOC_CONFIRMATION_REQUIRED"
        API-->>UI: 400 with AD_HOC code
        UI-->>Staff: SweetAlert2 "Ca tự do - Xác nhận?"
        Staff->>UI: Confirm
        UI->>API: Re-POST with forceSave=true
        Service->>DB: Insert StaffShift(IsAdHoc=true)
    end

    Service->>DB: Update ActualCheckIn = now
    Service->>DB: Insert AttendanceLog(IsFaceVerified=true)
    Service-->>API: Success
    API-->>UI: 200
    UI-->>Staff: SweetAlert2 success + refresh dashboard
```

---

## 4. Error Recovery Matrix

| # | Edge Case | Trigger | Auto Recovery | Manual Fallback |
|---|---|---|---|---|
| 1 | Cookie expired during StaffHub use | 7-day expiry or idle timeout | 302 → Login page (for page requests) | Global AJAX 401 handler → SweetAlert2 |
| 2 | Duplicate check-in (same day) | 2 devices, same account | Server blocks with error message | Staff sees "Đã chấm công rồi" |
| 3 | Overnight shift cross-day | Check-in 23:50, shift starts 23:00 | Query includes yesterday's overnight shifts | Manual ad-hoc if auto-match fails |
| 4 | Camera permission denied | Browser setting | Detect via `navigator.permissions` | Show instruction overlay |
| 5 | Face model load timeout | Slow network (>30s) | Promise.race timeout + retry button | Skip FaceID (admin override) |
| 6 | Unknown role after login | New role not in constants | Falls through to Home | Log warning + notify admin |
| 7 | Staff has no FaceDescriptor | New hire, not registered | Service returns clear error | Redirect to face registration |
| 8 | IDOR attack on API | Malicious accountId in POST | Extract from Claims, ignore POST body | Return 401 Unauthorized |
| 9 | First-login password | Default password detected | Modal blocks all StaffHub access | Must change before proceeding |
| 10 | Concurrent POS + Check-out | Staff checks out while POS is open | POS should detect shift end | SweetAlert2 "Ca đã kết thúc" on next POS action |
