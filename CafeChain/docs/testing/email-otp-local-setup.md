# OTP email — local setup for team testers

Private repo uses one **shared Gmail test mailbox** for OTP delivery.  
Tracked config holds **non-secret** SMTP settings only.  
**Never commit** Gmail login password or Gmail App Password.

## 1. Pull latest branch

```powershell
git pull
```

## 2. Shared non-secret settings (already in repo)

Tracked `appsettings.json` / `appsettings.Development.json`:

| Key | Expected value |
|---|---|
| `Email:DeliveryMode` | `Smtp` |
| `Email:SmtpHost` | `smtp.gmail.com` |
| `Email:SmtpPort` | `587` |
| `Email:Address` | team shared test Gmail (see tracked config) |
| `Email:Password` | empty in git — supply locally |

ASP.NET Core maps environment variables with `__` to nested keys:

- `Email__Password` → `Email:Password`
- `Email__DeliveryMode` → `Email:DeliveryMode`

## 3. Configure App Password locally (required for real SMTP)

Obtain the **shared Gmail App Password** from the team private channel (not from git, issues, or screenshots).

### Option A — PowerShell session env (quick)

```powershell
$env:Email__Password = "<TEAM_APP_PASSWORD>"
```

Restart the backend in **the same** PowerShell window.

### Option B — User Secrets (persists for this machine)

From the `CafeChain` project folder:

```powershell
cd CafeChain
dotnet user-secrets set "Email:Password" "<TEAM_APP_PASSWORD>"
```

Project already has `UserSecretsId` so secrets load automatically in Development.

### Option C — gitignored Local file (optional)

Copy `appsettings.Local.json.example` → `appsettings.Local.json`  
(file is gitignored). Prefer User Secrets for the password; leave `"Password": ""` in Local if using env/secrets.

## 4. Verify effective config

Before clicking **Gửi OTP**:

1. `DeliveryMode` = `Smtp` (not `Log`)
2. `SmtpHost` / `SmtpPort` as above
3. `Address` = shared test Gmail from tracked config
4. `Password` comes from **env or User Secrets**, not from git

## 5. Restart backend and test

```powershell
dotnet run --project CafeChain --launch-profile http
```

1. Open POS Shift Summary (or any OTP flow).
2. Click **Gửi OTP** once.
3. Approver mailbox receives the OTP email (shared test Gmail / approver account as designed).

## 6. If password is missing

Backend returns a **clear configuration error** (e.g. missing `Email:Password` / App Password guidance).  
It must **not**:

- log the App Password;
- crash the entire host;
- fall back to supervisor PIN.

## 7. Security rules for testers

- Do **not** paste App Password into GitHub issues, PR comments, screenshots, or terminal logs you share.
- Do **not** commit `appsettings.Local.json` with secrets.
- Prefer env or User Secrets over putting the password in any tracked file.
- OTP plaintext must never be committed or shared in issues.

## 8. Optional: Log-only mode (no Gmail)

For pipeline-only testing without SMTP:

```powershell
$env:Email__DeliveryMode = "Log"
```

OTP challenges can still be created in Development with log capture; no real email is sent.
