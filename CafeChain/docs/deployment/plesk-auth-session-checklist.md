# Plesk authentication and session deployment

CafeChain intentionally refuses to start in Production until a persistent
Data Protection directory is configured. This prevents IIS recycle/redeploy
from silently invalidating every authentication cookie.

## Required configuration

1. Create a private directory outside the application publish/content root on
   the same Windows server. Do not place it below `wwwroot` or the deployed app
   directory.
2. Grant the Plesk/IIS application-pool identity read, write, create and delete
   permissions for that directory. Do not grant anonymous web access.
3. Set the environment variable `DataProtection__KeysPath` to its absolute path.
   Keys are protected at rest with Windows machine-scope DPAPI, so restoring
   them on another Windows server requires a planned key migration strategy.
4. Set `ConnectionStrings__DefaultConnection`. ASP.NET Session uses the same SQL
   Server through the `dbo.SessionCache` table.
5. Run `SeedAll.sql` before starting the new application version. The script
   creates `dbo.SessionCache` idempotently; this repository intentionally keeps
   a single squashed EF baseline rather than adding a second migration.
6. Override `PosFrontend__Url`, `AppLauncher__Pos__PosUrl` and the corresponding
   health-check URL. Production must not use `127.0.0.1:5173` in a browser URL.

## Plesk/IIS checks

- Keep a stable application-pool identity and preserve the Data Protection
  directory across publish/redeploy operations.
- Record idle timeout, periodic recycle, worker-process count and the exact UTC
  time of every restart. WebSocket support must be enabled for SignalR.
- If the application is moved to another Windows server, do not copy only the
  encrypted key files and assume they remain decryptable. DPAPI machine scope
  binds them to the originating server.

## Verification after deploy

1. A one-time login is expected because the cookie name changes to
   `.CafeChain.Auth`.
2. In browser developer tools, confirm `.CafeChain.Auth` has `Path=/`,
   `HttpOnly`, `Secure` and `SameSite=Lax`.
3. Confirm the StaffHub negotiate requests send the cookie. POS negotiate
   requests must send the Bearer token through `access_token` for the two
   supported hub paths.
4. Recycle the application pool once while the cookie is still valid. Refresh
   StaffHub and confirm negotiate does not return 401.
5. Check logs for `AUTH_COOKIE_FAILED`, `AUTH_CREDENTIAL_MISSING`,
   `AUTH_JWT_FAILED`, `AUTH_POS_SESSION_REJECTED`, `AUTHZ_PERMISSION_DENIED`,
   `POS_SESSION_REPLACED` and `POS_SESSION_EXPIRY_COMPLETED`.
6. Never attach raw cookies, JWTs, passwords or signing keys to diagnostic logs
   or support tickets.
