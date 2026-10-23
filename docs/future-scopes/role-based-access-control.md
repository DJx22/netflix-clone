# Role-Based Access Control — Future Considerations

**Status:** Discussion only. Nothing in this document is scheduled or implemented. No ADR has been
raised from this. It is a parking lot for the role/claims gap that surfaced when planning the admin
`MediaAsset` write endpoint for the Streaming service. Revisit before implementing any endpoint
that must be restricted to a subset of authenticated users.

---

## Trigger

The Streaming service's `MediaAssets` table has no write path. Adding one requires a way to
distinguish an admin caller from a regular user — currently impossible because the JWT issued by
Identity carries only three claims: `sub` (userId), `email`, and `jti`. There is no `role` claim,
no policy, and no authorization beyond "valid JWT = allowed."

Catalog's write endpoints (`POST /titles`, `PUT /titles/{id}`, `DELETE /titles/{id}`) have the same
gap: they are `[Authorize]` but accept any valid token regardless of who signed it.

---

## The actual question

Not "how do we do proper IAM" — this project deliberately rolls its own JWT instead of using
Keycloak/Auth0 (see roadmap §3). The real question is:

> How does a service know the authenticated caller is permitted to perform an admin-level write,
> using only what the hand-rolled Identity service can put in a JWT?

That is a claims-design problem, not an infrastructure problem.

---

## What the current JWT looks like (`Identity.Infrastructure/Security/JwtTokenService.cs`)

```csharp
var claims = new[]
{
    new Claim(JwtRegisteredClaimNames.Sub,   user.Id.Value.ToString()),
    new Claim(JwtRegisteredClaimNames.Email, user.Email.Value),
    new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
};
```

Three claims. No `role`. No `permissions`. No `scope`. Every consuming service (Profile,
Subscription, Payment, Catalog, Streaming) extracts only `sub` from the JWT — there is nothing
else to extract.

---

## Three possible shapes

### 1. Simple role claim in the JWT (recommended for this project's scope)

Add a `role` string column to the `User` entity in Identity (`"user"` or `"admin"`). Include it as
a claim when issuing the token:

```csharp
new Claim(ClaimTypes.Role, user.Role),
```

Consuming services add an authorization policy:

```csharp
services.AddAuthorization(options =>
    options.AddPolicy("AdminOnly", p => p.RequireRole("admin")));
```

Endpoints gate on it:

```csharp
[Authorize(Policy = "AdminOnly")]
```

**What this requires end-to-end:**

| Layer | File(s) to change | Change |
|---|---|---|
| Identity — Domain | `User.cs` | Add `Role` string property; default `"user"` on `Register` |
| Identity — Infrastructure | `JwtTokenService.cs` + new migration | Emit `ClaimTypes.Role` claim; add `Role nvarchar(20)` column |
| Identity — API | `Program.cs` (optional) | Startup seed for first admin account in Development/Testing environments |
| All consuming services | `DependencyInjection.cs` | Add `RequireRole("admin")` policy registration |
| Streaming — Domain | `MediaAsset.cs` | Add `Update(mediaUrl, contentType, durationSeconds)` behaviour method |
| Streaming — Domain | `IMediaAssetRepository.cs` | Add `AddAsync` and `UpdateAsync` signatures |
| Streaming — Infrastructure | `MediaAssetRepository.cs` | Implement `AddAsync` / `UpdateAsync` |
| Streaming — Application | New files | `IMediaAssetAdminService`, `MediaAssetAdminService`, `CreateMediaAssetRequest` DTO + validator |
| Streaming — API | New `MediaAssetsController.cs` | `POST /api/v1/admin/media-assets`, `DELETE /api/v1/admin/media-assets/{titleId}` — both `[Authorize(Policy = "AdminOnly")]` |
| Streaming — OpenAPI | `openapi.yaml` | Document the two new admin endpoints (required before write methods per `IMediaAssetRepository` comment) |
| Postman | `netflix-clone-api.postman_collection.json` | Add `"00 - Admin Setup"` folder; `adminToken` / `adminEmail` / `adminPassword` environment variables; wire `titleId` → `streamingTitleId` automatically |

**Pros:** Minimal; fits within the hand-rolled Identity model; teachable in one session.  
**Cons:** Role is baked into the token at login time — a role change doesn't propagate until the
access token expires (mitigated by short `AccessTokenExpiryMinutes`). Acceptable at this
project's scope; not acceptable in production.

---

### 2. Claim-check against a userId allowlist (no schema change — avoid)

Keep the JWT as-is. Hardcode a known `userId` GUID (the admin's) in the service configuration.
A custom `IAuthorizationHandler` reads the `sub` claim and compares it to the configured GUID.

**Pros:** No Identity changes at all.  
**Cons:** Brittle — the GUID must be known before the account is created; config-driven
authorization is opaque and undocumented in any token; teaches the wrong thing about how claims
work. Do not use this shape.

---

### 3. Separate API key for machine-to-machine admin calls (over-engineered for now)

A long-lived API key (not a JWT) is issued to trusted backend callers and validated by a custom
middleware. User-facing tokens remain unchanged.

**Pros:** Separates human auth from machine auth cleanly.  
**Cons:** Introduces a second authentication scheme that needs its own storage, rotation, and
revocation. No machine-to-machine flows in this project currently require it. Revisit only if the
gateway or an inter-service admin channel is added in a later phase.

---

## Direction, if this is ever picked up

Shape 1 is the right choice for this project's scope. It teaches:

- How a role claim flows from Identity through every downstream service without each service
  calling back to Identity to check permissions
- How ASP.NET Core authorization policies compose with JWT bearer validation
- How to add EF Core migrations for a schema change on an entity already in production
- How the Postman collection evolves to cover a privileged, non-user flow

The implementation order matters: **Identity first, consuming services second.** Existing access
tokens issued before the migration will not carry the `role` claim — they will be rejected by
`RequireRole` until callers log in again and receive a new token. That is a known, accepted
token-breaking change at this stage of the project.

---

## Impact on the Postman test suite

The current collection (`postman/netflix-clone-api.postman_collection.json`) has no `adminToken`
variable and no admin-login step. Once Shape 1 is implemented the collection needs:

| What | Detail |
|---|---|
| New env vars | `adminEmail`, `adminPassword` (secret), `adminToken` (secret, blank default) |
| New folder `"00 - Admin Setup"` | Runs before `"01 - Health"`; contains admin login + MediaAsset seed request |
| Admin login request | `POST /api/v1/auth/login` with admin credentials → captures `adminToken` |
| MediaAsset seed request | `POST /api/v1/admin/media-assets` with `Authorization: Bearer {{adminToken}}` → asserts 201; sets `streamingTitleId` from `titleId` captured in the Catalog folder |
| Streaming folder | Remove the manual "Set `streamingTitleId` before running" note — the suite is now fully self-contained |

The existing negative-path gap (no 401 test on missing token, no 404 test on unknown `titleId` in
Streaming) should also be addressed when the collection is updated for Shape 1.

---

## Bootstrapping the first admin account

There is no admin account in a clean environment until one is created. Two options:

**Option A — Startup seed in `Program.cs`**  
Guard with `IsDevelopment() || IsEnvironment("Testing")`, same pattern as the existing
auto-migration block. Idempotent — only creates the account if `admin@netflix-clone.local` does
not already exist. Credentials come from environment variables or `appsettings.Development.json`.
Simpler; no new endpoint surface.

**Option B — `POST /api/v1/auth/admin/seed` endpoint**  
Unprotected, idempotent (204 if admin already exists). Disabled via environment guard or feature
flag in Production. Makes the Postman `"00 - Admin Setup"` folder fully self-contained — no manual
setup needed even on a cold Docker Compose start.

Choose before writing the migration — the seed mechanism determines what goes in `Program.cs` and
whether `appsettings.Development.json` needs new keys.

---

## Explicitly out of scope, now and near-term

- No Keycloak, Auth0, or any external identity provider — roadmap §3 explicitly defers this.
- No fine-grained permissions (`can:write:titles`, `can:delete:media`) — a two-value role string
  (`"user"` / `"admin"`) is sufficient for the endpoints this project needs to gate.
- No token introspection endpoint — the JWT is self-contained; consuming services validate it
  locally with the shared secret, same as today.
- No role-change propagation or token revocation on role update — short `AccessTokenExpiryMinutes`
  is the mitigation.
- No multi-role users — a user is either `"user"` or `"admin"`, never both.
- No change to Profile, Subscription, or Payment write paths — those are user-scoped by `sub`
  claim already; admin-level writes are not a requirement there.
