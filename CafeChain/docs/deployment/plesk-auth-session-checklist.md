# Plesk authentication and session deployment

CafeChain refuses to start without a persistent Data Protection directory,
except when a local Development launch profile explicitly opts into ephemeral
keys. This prevents an accidentally Development-configured IIS site from
silently invalidating every authentication and session cookie after recycle.

The production failure diagnosed on 2026-09-03 had a five-minute application
pool idle timeout. The replacement process logged
`PersistentDataProtectionKeys=False`, `Using an in-memory repository` and a
missing key-ring exception. API/SignalR 401 responses were consequences of the
cookie ticket no longer being decryptable, not missing RBAC grants.

## Required configuration

1. If the application content root is `httpdocs`, create the directory
   `../private/CafeChain/DataProtectionKeys` on the same Windows server. Do not
   place it below `httpdocs`, `wwwroot` or another directory replaced by
   publish/upload.
2. Grant the Plesk/IIS application-pool identity read, write, create and delete
   permissions for that directory. Do not grant anonymous web access.
3. Deploy the repository `web.config`. It fixes
   `ASPNETCORE_ENVIRONMENT=Production` and configures
   `DataProtection__KeysPath=..\private\CafeChain\DataProtectionKeys` relative
   to the application content root. Confirm the uploaded file was not replaced
   by an older generated `web.config`.
4. Keys are protected at rest with Windows machine-scope DPAPI. Moving them to
   another Windows server requires a planned key migration strategy; copying
   only the XML files is insufficient.
5. Set `ConnectionStrings__DefaultConnection`. ASP.NET Session uses the same SQL
   Server through the `dbo.SessionCache` table.
6. Run `SeedAll.sql` before starting the new application version. The script
   creates `dbo.SessionCache` idempotently; this repository intentionally keeps
   a single squashed EF baseline rather than adding a second migration.
7. Override `PosFrontend__Url`, `AppLauncher__Pos__PosUrl` and the corresponding
   health-check URL. Production must not use `127.0.0.1:5173` in a browser URL.

## Plesk/IIS checks

- Keep a stable application-pool identity and preserve the Data Protection
  directory across publish/redeploy operations.
- Set application-pool `Idle Time-out` to `0`, `Maximum Worker Processes` to
  `1`, and use `AlwaysRunning` plus site preload when the hosting plan exposes
  those settings. Record periodic recycle and the exact UTC time of every
  restart. WebSocket support must be enabled for SignalR.
- If the application is moved to another Windows server, do not copy only the
  encrypted key files and assume they remain decryptable. DPAPI machine scope
  binds them to the originating server.

## Verification after deploy

1. Delete the old `.CafeChain.Auth` and `.CafeChain.Session` cookies, then sign
   in once. Existing cookies were issued by the lost ephemeral key and cannot
   be recovered.
2. In browser developer tools, confirm `.CafeChain.Auth` has `Path=/`,
   `HttpOnly`, `Secure` and `SameSite=Lax`.
3. Confirm startup logs contain `EnvironmentName=Production`,
   `PersistentDataProtectionKeys=True`, `KeyDirectoryReady=True` and
   `DataProtectionRepository=FileSystemDpapiMachine`. Confirm at least one
   `key-*.xml` exists in the external directory.
4. Confirm the StaffHub negotiate requests send the cookie. POS negotiate
   requests must send the Bearer token through `access_token` for the two
   supported hub paths.
5. Wait longer than six minutes, refresh the page and confirm notification APIs
   and both hub negotiate requests do not return 401.
6. Recycle the application pool once while the cookie is still valid. Confirm
   the process ID changes, then refresh StaffHub and confirm negotiate succeeds
   with the same cookie. A brief WebSocket disconnect during recycle is normal;
   reconnect must negotiate successfully.
7. Check logs for `AUTH_COOKIE_FAILED`, `AUTH_CREDENTIAL_MISSING`,
   `AUTH_JWT_FAILED`, `AUTH_POS_SESSION_REJECTED`, `AUTHZ_PERMISSION_DENIED`,
   `POS_SESSION_REPLACED` and `POS_SESSION_EXPIRY_COMPLETED`.
8. There must be no `Using an in-memory repository`, `ephemeral key repository`
   or `key was not found in the key ring` warning after cutover.
9. Never attach raw cookies, JWTs, passwords or signing keys to diagnostic logs
   or support tickets.
