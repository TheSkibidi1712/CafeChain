# OTP email — local setup for team testers

Private repo uses one **shared Gmail test mailbox** for OTP delivery.  
Tracked config holds **non-secret** SMTP settings only.  
**Never commit** Gmail login password or Gmail App Password.

---

## Quick start

```powershell
git pull

# From repository root:
.\scripts\setup-team-otp-email.ps1

# Restart backend (required after secrets change)
cd CafeChain
dotnet run --launch-profile http
```

Then open POS → Shift Summary → **Gửi OTP**.  
Check the **approver** inbox (Ca trưởng email in Admin/DB), including **Spam**.

---

## What is already in the repository

Tracked `appsettings.json` / `appsettings.Development.json`:

| Key | Value |
|---|---|
| `Email:DeliveryMode` | `Smtp` |
| `Email:SmtpHost` | `smtp.gmail.com` |
| `Email:SmtpPort` | `587` |
| `Email:Address` | shared test Gmail (`cafechain8386@gmail.com`) |
| `Email:Password` | **not stored in git** |

You only need the **shared Gmail App Password** from the team private channel.

---

## Configuration provider order (runtime)

Actual host order after `Program.cs`:

1. `appsettings.json`
2. `appsettings.{Environment}.json` (e.g. Development)
3. User Secrets (Development; also re-applied after Local)
4. `appsettings.Local.json` (optional, gitignored — **connection string only**)
5. **User Secrets again** (Development) so Local cannot wipe secrets
6. **Environment variables** (final authority for `Email__Password`)
7. Command-line args (CreateBuilder defaults; env re-added last among our overrides)

**Critical:** never put `"Email:Password": ""` in `appsettings.Local.json`.  
Empty string would override User Secrets if Local were last — current host re-applies secrets + env after Local.

Environment mapping:

- `Email__Password` → `Email:Password`
- `Email__DeliveryMode` → `Email:DeliveryMode`

---

## Setup script (recommended)

```powershell
.\scripts\setup-team-otp-email.ps1
```

The script:

1. Finds the web project with `UserSecretsId`
2. Prompts for App Password via **SecureString** (not echoed)
3. Sets User Secrets: DeliveryMode, Host, Port, Address, Password
4. Prints **Password is configured** without printing the value
5. Reminds you to **restart backend**

Clear secrets:

```powershell
.\scripts\clear-team-otp-email.ps1
```

Manual alternatives:

```powershell
# Session env (same PowerShell window as dotnet run)
$env:Email__Password = "<TEAM_APP_PASSWORD>"

# Or User Secrets
cd CafeChain
dotnet user-secrets set "Email:Password" "<TEAM_APP_PASSWORD>"
```

---

## Security rules

- Do **not** put App Password in `appsettings.json`, issues, screenshots, or terminal logs you share.
- Do **not** commit `appsettings.Local.json` with secrets.
- Prefer setup script / User Secrets / `Email__Password`.
- Never commit OTP plaintext.

---

## Troubleshooting

### A. `EMAIL_SMTP_PASSWORD_NOT_CONFIGURED` / “Thiếu Email:Password”

→ Run `.\scripts\setup-team-otp-email.ps1`, then **restart** backend.

### B. UI shows “Development capture OTP …”

→ SMTP path ran but send failed (auth/network).  
Check DeliveryMode is `Smtp` (not `Log`), App Password still valid, 2FA enabled on shared Gmail.

### C. User Secrets set but runtime still empty

→ Ensure `appsettings.Local.json` has **no** `Email:Password` key (especially not `""`).  
Restart process after changing secrets.  
Confirm Development environment (`--launch-profile http`).

### D. SMTP authentication failed

→ App Password expired/revoked, or account 2FA not enabled, or wrong sender address.

### E. OTP request HTTP 500 / “cấu hình hệ thống hoặc cơ sở dữ liệu”

→ Often DB schema: `OtpChallenges` needs `PayloadFingerprint` + `RowVersion`.  
Recreate DB from current InitialCreate, or apply migrations.  
Also check unique index conflicts from expired Pending rows (fixed in recent OTP expire-stale commit).

### F. Optional Log-only (no real Gmail)

```powershell
$env:Email__DeliveryMode = "Log"
```

No real email is sent; Development may capture codes in messages when SMTP fails for other reasons.

---

## Verify effective non-secret config

| Check | Expected |
|---|---|
| DeliveryMode | `Smtp` |
| Host/Port | `smtp.gmail.com` / `587` |
| Address | shared test Gmail from tracked config |
| Password | User Secrets or `Email__Password` only |

```powershell
cd CafeChain
dotnet user-secrets list
# Expect Email:Password = ***** (value present; do not copy it into chats)
```
