---
baseline_commit: 7f7d874f7118701b40a5ad160fa974b15a1c4a92
---

# Story 1.6: Server-Side Role Gating & Protected Routing

Status: ready-for-dev

## Story

As a signed-in user,
I want pages and actions outside my role rejected server-side and hidden from navigation,
so that the app can't be tricked into exposing something I shouldn't see.

## Acceptance Criteria

1. **Given** an unauthenticated request to any protected endpoint, **when** received, **then** the API returns 401.
2. **Given** an authenticated request to an endpoint outside the caller's current role, **when** received, **then** the API returns 403, with role re-derived from the database on that same request (never trusted from the JWT claim) (FR3, AD-2).
3. **Given** a signed-in user's role, **when** the nav bar renders, **then** links to pages outside that role are removed entirely from the DOM and tab order, not merely hidden via CSS (FR3, UX-DR18).
4. **Given** a signed-in user manually navigates to a URL outside their role, **when** the route guard calls `GET /api/auth/me`, **then** they are redirected away rather than shown the page content (AD-18).
5. **Given** an expired access token or a fresh page load, **when** the frontend calls `POST /api/auth/refresh`, **then** a new access token is issued as long as the refresh token's `SessionVersion` still matches the database (AD-3).

## Tasks / Subtasks

- [ ] **Task 1: Carry `SessionVersion` on the access token** (AC: #2, #5)
  - [ ] In `AuthService.GenerateAccessToken` (`backend/BarbershopApi/Services/AuthService.cs`), add a second claim identical in name/shape to the one `GenerateRefreshToken` already writes: `new Claim("sessionVersion", account.SessionVersion.ToString())`. Story 1.5 deliberately left the access token with only a `sub` claim ("no role claim... Story 1.6's per-request role re-derivation reads role from the DB"), but never added `sessionVersion` to it either — that gap is this story's to close. Without this, there is nothing for the per-request liveness check (Task 2) to compare against, and the Story 1.5 Dev Notes' own "Scope Boundary" section names this exact mechanism as Story 1.6's job.
  - [ ] Do **not** add a role claim to the access token. AD-2 stays satisfied only if role is never read from the token anywhere — Task 2's middleware re-derives it from the DB every request.

- [ ] **Task 2: DB-derived role + session-liveness middleware** (AC: #1, #2)
  - [ ] Create `backend/BarbershopApi/Services/SessionLivenessMiddleware.cs`. This single middleware is the one mechanism that satisfies AD-2 for every current and future protected endpoint — future Epic 2/3 controllers will get role gating "for free" by adding `[Authorize(Roles = "...")]`, without ever touching a JWT role claim, because this middleware injects a DB-fresh role claim before ASP.NET Core's built-in authorization runs:
    ```csharp
    using System.Security.Claims;
    using System.IdentityModel.Tokens.Jwt;
    using BarbershopApi.Repositories;

    namespace BarbershopApi.Services;

    public class SessionLivenessMiddleware(RequestDelegate next)
    {
        public async Task InvokeAsync(HttpContext context, IAccountRepository accountRepository)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var subClaim = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
                var sessionVersionClaim = context.User.FindFirstValue("sessionVersion");

                if (subClaim is null || !int.TryParse(subClaim, out var accountId) ||
                    sessionVersionClaim is null || !int.TryParse(sessionVersionClaim, out var tokenSessionVersion))
                {
                    await Reject(context);
                    return;
                }

                var account = await accountRepository.FindById(accountId);
                if (account is null || account.SessionVersion != tokenSessionVersion)
                {
                    await Reject(context);
                    return;
                }

                var existingIdentity = (ClaimsIdentity)context.User.Identity;
                var refreshedIdentity = new ClaimsIdentity(existingIdentity.Claims, existingIdentity.AuthenticationType);
                refreshedIdentity.AddClaim(new Claim(ClaimTypes.Role, account.Role.ToString()));
                context.User = new ClaimsPrincipal(refreshedIdentity);
                context.Items["Account"] = account;
            }

            await next(context);
        }

        private static async Task Reject(HttpContext context)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { title = "Session expired. Please sign in again." });
        }
    }
    ```
    Placed under `Services/` even though it isn't a per-domain business service — same precedent Story 1.5 set with `AdminBootstrapService`/`JwtOptions` (cross-cutting infra that doesn't fit the Controllers/Services/Repositories domain-trio shape gets a home in `Services/` rather than a new top-level folder).
  - [ ] `context.Items["Account"]` caches the fetched row so downstream controller actions (Task 3's `/me`) don't re-query — this is the literal "same lookup also checks SessionVersion, so it's not an extra query" phrasing from AD-2/SOLUTION-DESIGN §3.2, extended here to also avoid a *third* query in `/me`.
  - [ ] A missing/soft-deleted account (`FindById` already excludes `DeletedAt IS NOT NULL`) is treated identically to a version mismatch — 401, not a crash. A missing/unparseable `sessionVersion` claim (e.g., any access token minted before this story shipped) also 401s rather than throwing — there is no upgrade path to worry about (no deployed users, NFR7), but the code must not `int.Parse` a null/malformed claim unguarded.
  - [ ] In `Program.cs`, register the middleware between authentication and authorization — order is load-bearing: `app.UseAuthentication(); app.UseMiddleware<SessionLivenessMiddleware>(); app.UseAuthorization();`. It must run after `UseAuthentication()` (needs `context.User` populated by JWT validation) and before `UseAuthorization()` (so any `[Authorize(Roles = ...)]` on a downstream endpoint sees the DB-fresh role claim, not none at all).
  - [ ] Register `IAccountRepository` is already scoped in DI (Story 1.2) — no new registration needed; `InvokeAsync`'s `IAccountRepository accountRepository` parameter resolves per-request via ASP.NET Core's middleware-parameter-injection convention (the standard, idiomatic way for a singleton middleware to consume a scoped service — do **not** inject it via the constructor, which would capture a single scoped instance for the app's lifetime).
  - [ ] Unauthenticated requests (no token at all) skip the DB lookup entirely and fall through to `next(context)` — the existing JWT bearer scheme's own `[Authorize]` challenge already produces 401 for those (proven today by Story 1.5's `Logout_without_access_token_returns_401` test), so AC #1 needs no new code, only confirmation it still holds once this middleware is added.

- [ ] **Task 3: `GET /api/auth/me`** (AC: #4)
  - [ ] `backend/BarbershopApi/Dtos/MeResponse.cs`: `public record MeResponse(int Id, string Email, string FirstName, string LastName, Role Role);` — exactly the shape AD-3 locks (`{ id, email, firstName, lastName, role }`), the same shape `LoginResponse` already partially mirrors.
  - [ ] In `AuthController`:
    ```csharp
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var account = (Account)HttpContext.Items["Account"]!;
        return Ok(new MeResponse(account.Id, account.Email, account.FirstName, account.LastName, account.Role));
    }
    ```
    No try/catch needed — by the time this action runs, Task 2's middleware has already rejected any unauthenticated or session-invalid request with 401, so `HttpContext.Items["Account"]` is guaranteed populated whenever this line executes.
  - [ ] `[Authorize]` with no `Roles` restriction — `/me` is reachable by any signed-in role; it's an identity lookup, not a role-gated resource.

- [ ] **Task 4: `POST /api/auth/refresh`** (AC: #5)
  - [ ] `backend/BarbershopApi/Dtos/RefreshResponse.cs`: `public record RefreshResponse(string AccessToken);` — deliberately **not** `LoginResponse`-shaped. AD-3 names `/me` as "the one shared who-am-I shape... no other endpoint invents its own shape for who am I" — `/refresh` returns only a fresh access token; the frontend calls `/me` separately to (re)hydrate identity. Don't shortcut this by bundling identity fields into the refresh response.
  - [ ] `backend/BarbershopApi/Services/InvalidSessionException.cs`: plain `Exception` subclass, same shape as `InvalidCredentialsException`/`DuplicateEmailException` — a distinct exception because "refresh token invalid/expired/stale" is a different failure mode than "wrong password," even though both currently map to a 401.
  - [ ] Extend `IAuthService` with `Task<(Account Account, string AccessToken)> Refresh(string refreshToken);`. Implementation in `AuthService`:
    ```csharp
    public async Task<(Account Account, string AccessToken)> Refresh(string refreshToken)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        ClaimsPrincipal principal;
        try
        {
            principal = handler.ValidateToken(refreshToken, ValidationParameters(), out _);
        }
        catch (SecurityTokenException)
        {
            throw new InvalidSessionException();
        }

        var subClaim = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var sessionVersionClaim = principal.FindFirstValue("sessionVersion");
        if (subClaim is null || !int.TryParse(subClaim, out var accountId) ||
            sessionVersionClaim is null || !int.TryParse(sessionVersionClaim, out var tokenSessionVersion))
        {
            throw new InvalidSessionException();
        }

        var account = await accountRepository.FindById(accountId);
        if (account is null || account.SessionVersion != tokenSessionVersion)
        {
            throw new InvalidSessionException();
        }

        return (account, GenerateAccessToken(account));
    }

    private TokenValidationParameters ValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = "BarbershopApi",
        ValidateAudience = true,
        ValidAudience = "BarbershopApi",
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Value.Key)),
    };
    ```
    **This is not covered by the JWT bearer authentication scheme at all** — the client has no valid access token to send as `Authorization: Bearer` at refresh time (that's the whole reason it's refreshing), so this validates the **refresh cookie's** JWT manually via a standalone `JwtSecurityTokenHandler`. That handler instance needs its own `MapInboundClaims = false` — Story 1.5's Dev Notes "Claim-Mapping Gotcha" documented this exact `sub`-gets-renamed-to-a-URI behavior for the bearer scheme's options; that setting does **not** carry over to a manually constructed handler, so the same bug reappears here in a new spot if this line is skipped. Reuse the same `ValidIssuer`/`ValidAudience`/key as the bearer scheme (`"BarbershopApi"` constants, `jwtOptions.Value.Key`) — one signing key, one issuer/audience pair, for both token kinds, unchanged from Story 1.5.
  - [ ] `AuthController`:
    ```csharp
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(refreshToken))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Session expired. Please sign in again.");
        }

        try
        {
            var (_, accessToken) = await authService.Refresh(refreshToken);
            return Ok(new RefreshResponse(accessToken));
        }
        catch (InvalidSessionException)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Session expired. Please sign in again.");
        }
    }
    ```
    No `[Authorize]` attribute — this endpoint is anonymous by design (reads the cookie directly via `Request.Cookies`, not the bearer pipeline).

- [ ] **Task 5: Backend tests** (AC: #1, #2, #4, #5)
  - [ ] Create `backend/BarbershopApi.Tests/TestOnly/RoleGateTestController.cs` — a minimal, real-role-gated endpoint used only by tests, since no actual business endpoint requiring `[Authorize(Roles = "Admin")]` exists yet (Epic 2/3 build those):
    ```csharp
    [ApiController]
    [Route("api/test-only")]
    public class RoleGateTestController : ControllerBase
    {
        [HttpGet("admin")]
        [Authorize(Roles = "Admin")]
        public IActionResult AdminOnly() => Ok(new { message = "ok" });
    }
    ```
    Register it only for the test host in `SqliteApiFactory.ConfigureWebHost`: `builder.ConfigureServices(services => services.AddControllers().AddApplicationPart(typeof(RoleGateTestController).Assembly));`. This never ships in production `Program.cs` (which never references the Tests assembly) — it exists purely to prove the middleware + `[Authorize(Roles=...)]` combination works end-to-end before any real consumer exists.
  - [ ] New `RoleGatingTests`:
    - `AdminOnlyEndpoint_without_token_returns_401`
    - `AdminOnlyEndpoint_with_customer_token_returns_403`
    - `AdminOnlyEndpoint_with_admin_token_returns_200`
    - `AdminOnlyEndpoint_reflects_db_role_change_without_new_login` — log in as a `Customer`-role account (capture its access token), then directly flip that account's `Role` to `Admin` via `IAccountRepository.Update` against the test DB, then call `/api/test-only/admin` with the **same, still-Customer-era** access token and assert **200**. This is the one test that actually proves AD-2 ("never trusts the JWT's role claim as-is") rather than merely proving a working `[Authorize(Roles=...)]` — if role were read from the token instead of the DB, this test would 403 instead.
  - [ ] New `MeEndpointTests`:
    - `Me_without_access_token_returns_401`
    - `Me_with_valid_access_token_returns_identity` — login, call `/me` with the access token, assert the body matches `{id, email, firstName, lastName, role}`.
    - `Me_after_logout_returns_401` — login, logout (bumps `SessionVersion`), call `/me` with the now-stale access token, assert 401. This is the exact gap Story 1.5's Dev Notes flagged as open ("other devices' still-unexpired access tokens keep working... until Story 1.6's per-request check exists") — this test proves it's now closed.
  - [ ] New `RefreshEndpointTests` (rely on `SqliteApiFactory.CreateClient()`'s default cookie handling — `WebApplicationFactory`'s client tracks `Set-Cookie` across requests on the same `HttpClient` instance by default, so a login followed by a refresh call on the *same* client automatically resends the `refreshToken` cookie):
    - `Refresh_without_cookie_returns_401`
    - `Refresh_with_valid_cookie_returns_new_access_token` — login, then call `/refresh` on the same client, assert 200 and a non-empty, different `accessToken`, and that the new token also works against `/me`.
    - `Refresh_after_logout_returns_401` — login, logout, call `/refresh` on the same client (cookie still present but `SessionVersion` now stale), assert 401.
  - [ ] All new tests follow the existing `SqliteApiFactory.CreateClient()` / real-SQLite pattern — no mocking (NFR4, AD-4).

- [ ] **Task 6: Frontend `AuthApi` additions** (AC: #4, #5)
  - [ ] Add to `frontend/src/api/AuthApi.js`, following the existing functions' try/catch shape:
    ```js
    export async function getCurrentUser(accessToken) {
      let response
      try {
        response = await fetch(`${API_BASE_URL}/api/auth/me`, {
          credentials: 'include',
          headers: { Authorization: `Bearer ${accessToken}` },
        })
      } catch {
        return { ok: false, status: null }
      }

      if (!response.ok) {
        return { ok: false, status: response.status }
      }
      const identity = await response.json()
      return { ok: true, identity }
    }

    export async function refreshSession() {
      let response
      try {
        response = await fetch(`${API_BASE_URL}/api/auth/refresh`, {
          method: 'POST',
          credentials: 'include',
        })
      } catch {
        return { ok: false }
      }

      if (!response.ok) {
        return { ok: false }
      }
      const body = await response.json()
      return { ok: true, accessToken: body.accessToken }
    }
    ```
    Both set `credentials: 'include'` (AD-13) — `refreshSession` needs it to send the `refreshToken` cookie at all; `getCurrentUser` doesn't strictly need it (it authenticates via the `Authorization` header, not the cookie) but keeping the convention uniform across every auth-related fetch avoids a future "why does this one call not send credentials" surprise.

- [ ] **Task 7: `AuthContext` session bootstrap on mount** (AC: #5)
  - [ ] In `frontend/src/context/AuthContext.jsx`, add a `ready` flag and a mount-time bootstrap effect:
    ```jsx
    import { createContext, useContext, useEffect, useState } from 'react'
    import { getCurrentUser, refreshSession } from '../api/AuthApi'

    const AuthContext = createContext(null)

    export function AuthProvider({ children }) {
      const [user, setUser] = useState(null)
      const [ready, setReady] = useState(false)

      useEffect(() => {
        let cancelled = false

        async function bootstrap() {
          const refreshResult = await refreshSession()
          if (!refreshResult.ok) {
            if (!cancelled) setReady(true)
            return
          }

          const meResult = await getCurrentUser(refreshResult.accessToken)
          if (cancelled) return
          if (meResult.ok) {
            setUser({ accessToken: refreshResult.accessToken, ...meResult.identity })
          }
          setReady(true)
        }

        bootstrap()
        return () => { cancelled = true }
      }, [])

      return (
        <AuthContext.Provider value={{ user, ready, login: setUser, logout: () => setUser(null) }}>
          {children}
        </AuthContext.Provider>
      )
    }
    ```
    This is the "fresh page load" trigger AD-3 names explicitly: the in-memory access token evaporates on every reload by construction, so this effect is what makes "still logged in" survive a refresh — it calls `/refresh` (using the `HttpOnly` cookie the browser already holds), then `/me` to rehydrate `user`, exactly mirroring the backend sequence diagram in `SOLUTION-DESIGN.md` §3.4.
  - [ ] `ready` is not required by any single AC in isolation, but is necessary for correctness: without it, `RequireRole` (Task 9) and `NavBar` (Task 8) would render their signed-out treatment for one tick while the bootstrap fetch is still in flight, causing a visible flash/incorrect redirect on every page load. Consumers should treat `ready === false` as "don't make an auth decision yet."
  - [ ] `useAuth()`'s existing outside-provider guard (Story 1.5's patch-round fix) is untouched — still throws if called outside `AuthProvider`.

- [ ] **Task 8: `NavBar` role-based link visibility** (AC: #3)
  - [ ] In `frontend/src/components/NavBar.jsx`, replace the blanket `INERT_LINKS` array/rendering with a role-gated list:
    ```js
    const ROLE_LINKS = [
      { label: 'Schedule Appointment', to: '/schedule-appointment', roles: ['Customer', 'Barber', 'Admin'] },
      { label: 'My Schedule', to: '/my-schedule', roles: ['Barber', 'Admin'] },
      { label: 'Admin Panel', to: '/admin', roles: ['Admin'] },
    ]
    ```
    Routes per the locked Route Naming Convention table (`1-3-home-and-about-pages.md`). "Schedule Appointment" is visible to every signed-in role per FR5 ("Any signed-in user can access Schedule Appointment"); UX-DR3 only names `My Schedule`/`Admin Panel` as role-restricted.
  - [ ] Render only the links matching the current user's role, as real `<Link>` elements identical in treatment to `ROUTED_LINKS` (active-link class included) — not spans, not CSS-hidden:
    ```jsx
    {ROLE_LINKS.filter((link) => user && link.roles.includes(user.role)).map(({ label, to }) => (
      <li key={label}>
        <Link className={/* same active-link logic as ROUTED_LINKS */} to={to}>
          {label}
        </Link>
      </li>
    ))}
    ```
    When signed out, or when a role doesn't cover a given link, that link renders **nothing** — no inert placeholder, full removal from the DOM/tab order (AC #3, UX-DR18's "real DOM removal" requirement). This is a real behavior change from Story 1.5, which explicitly deferred it: "Do not touch `INERT_LINKS`... role-based nav visibility/DOM-removal is explicitly Story 1.6's job."
  - [ ] **Known regression to fix, not a new bug**: `NavBar.test.jsx`'s existing test "renders Home and About as real links, and the rest as inert text" asserts `getByText(label)` for all three role-gated labels when signed out — that assertion is now false (they don't render at all signed-out) and **must** be rewritten as part of this story, the same way Story 1.3 flagged (and required fixing) an equivalent `NavBar.test.jsx` break for its own Router-context change.
  - [ ] These routes don't resolve to real pages yet (`/schedule-appointment`, `/my-schedule`, `/admin` are Epic 2/3 work) — this is the same "wire now, build later" precedent Story 1.4/1.5 established for `/login`/`/schedule-appointment`/`/my-schedule` navigation targets, just applied to nav links instead of button clicks.

- [ ] **Task 9: `RequireRole` route guard** (AC: #4)
  - [ ] Create `frontend/src/components/RequireRole.jsx` — a reusable wrapper future stories (2.2, 2.5, 2.6, 3.2, 1.7) will use to guard their page routes once those routes exist. **No route in `App.jsx` uses it yet** — there is nothing to wrap it around until a protected page exists, so this task is pure infrastructure, mirroring Task 2's middleware on the frontend side.
    ```jsx
    import { useEffect, useState } from 'react'
    import { Navigate } from 'react-router'
    import { useAuth } from '../context/AuthContext'
    import { getCurrentUser } from '../api/AuthApi'

    const LANDING_ROUTE = { Customer: '/schedule-appointment', Barber: '/my-schedule', Admin: '/my-schedule' }

    export default function RequireRole({ roles, children }) {
      const { user } = useAuth()
      const [check, setCheck] = useState({ status: 'pending' })

      useEffect(() => {
        let cancelled = false

        if (!user) {
          setCheck({ status: 'unauthenticated' })
          return
        }

        getCurrentUser(user.accessToken).then((result) => {
          if (cancelled) return
          if (!result.ok) {
            setCheck({ status: 'unauthenticated' })
          } else if (!roles.includes(result.identity.role)) {
            setCheck({ status: 'wrong-role', role: result.identity.role })
          } else {
            setCheck({ status: 'allowed' })
          }
        })

        return () => { cancelled = true }
        // eslint-disable-next-line react-hooks/exhaustive-deps
      }, [user])

      if (check.status === 'pending') return null
      if (check.status === 'unauthenticated') return <Navigate to="/login" replace />
      if (check.status === 'wrong-role') return <Navigate to={LANDING_ROUTE[check.role]} replace />
      return children
    }
    ```
  - [ ] This literally calls `GET /api/auth/me` on every mount, per AC #4's exact wording — the authorization *decision* comes from that fresh response, not from `AuthContext`'s cached `user.role` (which could theoretically be stale if role changed server-side since the last `/me` call; `user.accessToken` from context is used only as the bearer credential to make the check, never as the basis for the redirect decision itself). This mirrors AD-2's "never trust a stale claim" principle on the client side.
  - [ ] Redirect target on wrong-role match: the user's own default landing route, no visible "blocked" screen — per `EXPERIENCE.md`'s "Wrong-role direct-URL access" state pattern ("redirecting to the user's own default landing page with no visible 'blocked' screen — mirroring the product's broader stance that role-gated surfaces are hidden, not flaunted-then-refused").
  - [ ] While `check.status === 'pending'`, render nothing (`null`) rather than a placeholder — no AC specifies loading copy for this guard, and inventing one risks conflicting with whatever `EXPERIENCE.md` state pattern the actual page (once built in Epic 2/3) defines for its own cold-load state.

- [ ] **Task 10: Frontend tests** (AC: #3, #4, #5)
  - [ ] New `AuthContext.test.jsx`: stub `fetch` — (a) refresh succeeds + me succeeds → a consuming component sees `ready === true` and the expected `user`; (b) refresh fails (401, e.g. no cookie) → `ready` becomes `true`, `user` stays `null`, no unhandled rejection/crash.
  - [ ] Update `NavBar.test.jsx`: replace the now-false "renders... the rest as inert text" assertion with role-based cases — signed-out hides all three; signed-in `Customer` shows only "Schedule Appointment"; signed-in `Barber` shows "Schedule Appointment" + "My Schedule" (not "Admin Panel"); signed-in `Admin` shows all three. Assert via `queryByText`/`queryByRole('link', ...)` returning `null` for hidden links (proving DOM removal, not a CSS class), per AC #3. Extend `SIGNED_IN_USER`/`SignInOnMount` test helpers already in this file to accept a `role` override instead of hardcoding `'Customer'`.
  - [ ] New `RequireRole.test.jsx`: stub `fetch` for `/api/auth/me`; cases — no signed-in user in context → renders the `/login` stub route; signed-in but wrong role → renders the redirect-target stub route (per `LANDING_ROUTE`); signed-in with an allowed role → renders `children`. Use a real `<MemoryRouter>` + `<Routes>` with stub destination routes, matching every prior story's established pattern (never a mocked `useNavigate`/`Navigate`).
  - [ ] Stub `fetch` via `vi.spyOn(globalThis, 'fetch')`, no MSW (AD-4), consistent with every existing test file.

- [ ] **Task 11: Verify CI green, and resolve Story 1.5's flagged SameSite risk**
  - [ ] Branch as `story/1.6-role-gating-protected-routing` from `main`.
  - [ ] **This story is the first to actually read the refresh cookie back** (Story 1.5 minted and set it but never validated it round-trip). Story 1.5's Dev Notes flagged a specific, named risk here: the frontend runs on `http://localhost:5173`, the backend on `https://localhost:7113` — under schemeful same-site (Chrome ≥89), these count as different "sites" despite sharing a hostname, so a `SameSite=Strict` cookie set by the HTTPS origin may not be sent back from the HTTP origin. **Verify manually in a browser**: sign in, then trigger `/api/auth/refresh` (reload the page, or call it directly) and confirm via devtools (Application → Cookies / Network tab) that the `refreshToken` cookie is actually attached to the request.
  - [ ] If the cookie is *not* sent: Jack's pre-approved resolution (2026-07-30) is to run the Vite dev server over HTTPS too (`vite --https` / `server.https` in `vite.config.js`, plus `dotnet dev-certs https --trust`-style dev cert trust on the frontend side) so both origins share a scheme. **Do not** reach for `SameSite=None` or downgrading the backend to HTTP as the fix — both were explicitly ruled out already.
  - [ ] Run `dotnet test`, `npm run lint`, `npm run format:check`, `npm test` locally: confirm all green before push.
  - [ ] Push and confirm both CI jobs pass.

## Dev Notes

### Architecture Compliance (must-follow, not optional)

- **AD-1 (layering)** — extends the existing `AuthController` → `AuthService` → `AccountRepository` trio; `SessionLivenessMiddleware` is cross-cutting infrastructure (not a fourth domain trio), placed in `Services/` per the precedent Story 1.5 set with `AdminBootstrapService`/`JwtOptions`.
- **AD-2 (role & session liveness re-derived server-side per request)** — this story's central mechanism (Task 2). The DB-role-injection-into-`ClaimsPrincipal` approach means every *future* role-gated endpoint (Epic 2/3) only needs `[Authorize(Roles = "Admin")]` — the framework's own built-in role check — and gets AD-2 compliance for free, without any controller ever reading a role claim off the raw token.
- **AD-3 (token mechanics)** — access token now carries `sessionVersion` (Task 1) in addition to `sub`; refresh token unchanged from Story 1.5. `GET /api/auth/me` returns exactly `{id, email, firstName, lastName, role}` — no other endpoint (including the new `/refresh`) invents a competing "who am I" shape.
- **AD-13 (CORS/credentials)** — both new frontend fetches (`getCurrentUser`, `refreshSession`) set `credentials: 'include'`, consistent with every existing auth fetch.
- **AD-18 (client routing mirrors server gating)** — `RequireRole` calls `GET /api/auth/me` to decide, exactly as AD-18 specifies; hiding a nav link (Task 8) remains "a UX nicety layered on top, never the enforcement itself."

### Scope Boundary (important — don't over-build)

This story ships **infrastructure with no real page/endpoint consumer yet**:
- `RequireRole` isn't applied to any route in `App.jsx` — no protected page exists to wrap (`ScheduleAppointment` is Story 2.2, `MySchedule` is 2.5/2.6, `AdminPanel` is 3.2, `Account` is 1.7). Don't build any of those pages here.
- The `SessionLivenessMiddleware` + `[Authorize(Roles=...)]` combination has no real business endpoint to protect yet either — that's why Task 5 introduces a test-only controller (`RoleGateTestController`) purely to prove the mechanism works before Epic 2/3 exist to consume it.
- Don't build Epic 2 (Booking) or Epic 3 (Account management) business logic — this story is exclusively the auth/routing substrate those epics will build on top of.

### Previous Story Intelligence (from Story 1.5)

- Access token, as shipped by 1.5, carries only `sub` — no `role`, no `sessionVersion`. This story adds `sessionVersion` (Task 1) but still never adds `role` to it (Task 2's middleware supplies role freshly per-request instead).
- Story 1.5's own "Scope Boundary With Story 1.6" section named exactly this story's job: `/api/auth/refresh`, `/api/auth/me`, per-request DB re-derivation of Role + SessionVersion, `[Authorize(Roles=...)]`-style policies, and frontend route guards — all confirmed still accurate against the current codebase.
- **SameSite=Strict scheme-mismatch risk** (Story 1.5 Dev Notes, "flag now, matters more in Story 1.6") — see Task 11. This is the first story where it can actually bite, since Story 1.5 minted the cookie but never read it back.
- Claim-Mapping Gotcha (`MapInboundClaims = false`) — Story 1.5 set this on the JWT bearer *scheme* options; Task 4's manually-constructed `JwtSecurityTokenHandler` for refresh-token validation needs the same flag set independently, or `sub` silently renames to a claim type nothing here reads.
- `useAuth()` already throws outside `AuthProvider` (a Story 1.5 patch-round fix) — don't reintroduce the earlier silent-`null` bug when adding `ready` to the context value.
- `NavBar.test.jsx`'s current "renders... rest as inert text" test will break by design once Task 8 lands — this is the same category of expected, required test-repair Story 1.3 already established a precedent for.
- React Router package is `react-router` (not `react-router-dom`), v8.3.0 — confirmed in `frontend/package.json`; `RequireRole`'s `<Navigate>` import matches this.

### Testing Requirements

- Backend: xUnit.v3 + `WebApplicationFactory` (`SqliteApiFactory.CreateClient()`) against real SQLite — no mocked `DbContext`/`IAccountRepository` (NFR4, AD-4). The `RoleGateTestController` application-part registration is test-host-only and never reachable in production.
- Frontend: Vitest + jsdom + RTL + user-event; `vi.spyOn(fetch)`, no MSW; real `<MemoryRouter>` + `<Routes>` + stub destination routes for every redirect assertion, never a mocked `useNavigate`/`Navigate` (established Story 1.3–1.5 pattern).

### Project Structure Notes

- Backend: no new top-level folders. New files: `Services/SessionLivenessMiddleware.cs`, `Services/InvalidSessionException.cs`, `Dtos/MeResponse.cs`, `Dtos/RefreshResponse.cs`, `BarbershopApi.Tests/TestOnly/RoleGateTestController.cs` (new test-only subfolder), `BarbershopApi.Tests/RoleGatingTests.cs`, `BarbershopApi.Tests/MeEndpointTests.cs`, `BarbershopApi.Tests/RefreshEndpointTests.cs`. Modified: `AuthController.cs`, `AuthService.cs`, `IAuthService.cs`, `Program.cs`, `SqliteApiFactory.cs`.
- Frontend: new files `components/RequireRole.jsx`, `components/RequireRole.test.jsx`, `context/AuthContext.test.jsx`. Modified: `context/AuthContext.jsx`, `api/AuthApi.js`, `components/NavBar.jsx`, `components/NavBar.test.jsx`.

### Git Intelligence Summary

Recent commits (`7f7d874`/`49be080` Story 1.5 → `13e3969`/`f74670c` Story 1.4) follow: implement on a short-lived `story/{epic}.{story}-{slug}` branch → self-verify CI green locally → push → review → patch round(s) → merge → `done`. Story 1.5 needed two patch rounds for a comparably-sized surface (JWT + rate limiting + a hosted service); expect similar here given this story introduces a new middleware, two new endpoints, and two new frontend components.

### References

- [Source: _bmad-output/planning-artifacts/epics.md §Story 1.6] — story statement, AC.
- [Source: _bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/ARCHITECTURE-SPINE.md #AD-1, #AD-2, #AD-3, #AD-13, #AD-18] — layering, role/session liveness, token mechanics, CORS, client routing.
- [Source: _bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/SOLUTION-DESIGN.md §3.2–3.4, §3.6] — role-liveness rationale ("never trusting the claim, not by revoking the token"), token-transport sequence diagram, `/me` origin story.
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-bmad-learning-project-2026-07-23/EXPERIENCE.md §State Patterns ("Signed-out hits a protected surface", "Wrong-role direct-URL access")] — redirect targets and no-visible-block-screen convention.
- [Source: _bmad-output/implementation-artifacts/1-5-sign-in-sign-out-and-first-admin-bootstrap.md §Dev Notes — Scope Boundary With Story 1.6, Known Risk to Verify, Claim-Mapping Gotcha] — exactly what this story inherits and must resolve.
- [Source: _bmad-output/implementation-artifacts/1-3-home-and-about-pages.md §Route Naming Convention] — locked paths for `/schedule-appointment`, `/my-schedule`, `/admin`.
- [Source: backend/BarbershopApi/{Controllers/AuthController.cs, Services/AuthService.cs, Services/IAuthService.cs, Program.cs, Entities/Account.cs, Entities/Role.cs, Repositories/AccountRepository.cs, Repositories/IAccountRepository.cs}] — current (pre-story) state of every backend file this story modifies or extends.
- [Source: backend/BarbershopApi.Tests/{SqliteApiFactory.cs, AuthControllerTests.cs}] — existing test fixture/coverage this story extends.
- [Source: frontend/src/{App.jsx, context/AuthContext.jsx, components/NavBar.jsx, components/NavBar.test.jsx, api/AuthApi.js, api/ApiConfig.js}, frontend/package.json] — current (pre-story) state of every frontend file this story modifies or extends; confirms `react-router` 8.3.0.
- [Source: project-context.md §Framework-Specific Rules, §Critical Don't-Miss Rules] — AD-2 401/403 split, AD-18 route-guard convention.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List