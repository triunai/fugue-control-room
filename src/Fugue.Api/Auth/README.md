# Auth folder map

**Issue the token**
- `AuthOptions`: the `Auth:*` settings (issuer, audience, signing key, token lifetimes) and the startup check that rejects a weak key.
- `JwtAccessTokenIssuer`: mints the HS256 access token; it carries identity only (subject, id, times), never permissions.

**Check the token on every request**
- `JwtBearerSetup`: validates the token, then looks up the user's current permissions and adds them to the request in memory.
- It also writes the 401 and 403 problem responses, each with an `access_audit` row (the 401 rows are capped, see Limits).

**Who is calling**
- `JwtCurrentUser`: implements `ICurrentUser`, so handlers get the user id, tenant and permissions without seeing ASP.NET.
- `FugueClaimTypes`: the one place the claim names (`sub`, `tenant_id`, `perm`) are written.

**What an action requires**
- `HasPermissionAttribute`: names the permission code an action needs; the policy provider and handler in the same file check it.

**Limits**
- `AuthRateLimiting`: slows sign-in brute force: per client IP, per IP plus email, and a looser ceiling per email from any IP.
- `UnauthenticatedAuditLimiter`: caps how many 401 audit rows one client IP can write per minute.

**Request path:** token -> `JwtBearerSetup` -> `HasPermission` -> handler reads `JwtCurrentUser`.
