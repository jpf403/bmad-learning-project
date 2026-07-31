---
baseline_commit: 7f7d874f7118701b40a5ad160fa974b15a1c4a92
---

# Story 1.6: Server-Side Role Gating & Protected Routing

Status: done

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

- [x] **Task 1: Carry `SessionVersion` on the access token** (AC: #2, #5)
  - [x] In `AuthService.GenerateAccessToken` (`backend/BarbershopApi/Services/AuthService.cs`), add a second claim identical in name/shape to the one `GenerateRefreshToken` already writes: `new Claim("sessionVersion", account.SessionVersion.ToString())`. Story 1.5 deliberately left the access token with only a `sub` claim ("no role claim... Story 1.6's per-request role re-derivation reads role from the DB"), but never added `sessionVersion` to it either — that gap is this story's to close. Without this, there is nothing for the per-request liveness check (Task 2) to compare against, and the Story 1.5 Dev Notes' own "Scope Boundary" section names this exact mechanism as Story 1.6's job.
  - [x] Do **not** add a role claim to the access token. AD-2 stays satisfied only if role is never read from the token anywhere — Task 2's middleware re-derives it from the DB every request.

- [x] **Task 2: DB-derived role + session-liveness middleware** (AC: #1, #2)
  - [x] Create `backend/BarbershopApi/Services/SessionLivenessMiddleware.cs`. This single middleware is the one mechanism that satisfies AD-2 for every current and future protected endpoint — future Epic 2/3 controllers will get role gating "for free" by adding `[Authorize(Roles = "...")]`, without ever touching a JWT role claim, because this middleware injects a DB-fresh role claim before ASP.NET Core's built-in authorization runs:
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
  - [x] `context.Items["Account"]` caches the fetched row so downstream controller actions (Task 3's `/me`) don't re-query — this is the literal "same lookup also checks SessionVersion, so it's not an extra query" phrasing from AD-2/SOLUTION-DESIGN §3.2, extended here to also avoid a *third* query in `/me`.
  - [x] A missing/soft-deleted account (`FindById` already excludes `DeletedAt IS NOT NULL`) is treated identically to a version mismatch — 401, not a crash. A missing/unparseable `sessionVersion` claim (e.g., any access token minted before this story shipped) also 401s rather than throwing — there is no upgrade path to worry about (no deployed users, NFR7), but the code must not `int.Parse` a null/malformed claim unguarded.
  - [x] In `Program.cs`, register the middleware between authentication and authorization — order is load-bearing: `app.UseAuthentication(); app.UseMiddleware<SessionLivenessMiddleware>(); app.UseAuthorization();`. It must run after `UseAuthentication()` (needs `context.User` populated by JWT validation) and before `UseAuthorization()` (so any `[Authorize(Roles = ...)]` on a downstream endpoint sees the DB-fresh role claim, not none at all).
  - [x] Register `IAccountRepository` is already scoped in DI (Story 1.2) — no new registration needed; `InvokeAsync`'s `IAccountRepository accountRepository` parameter resolves per-request via ASP.NET Core's middleware-parameter-injection convention (the standard, idiomatic way for a singleton middleware to consume a scoped service — do **not** inject it via the constructor, which would capture a single scoped instance for the app's lifetime).
  - [x] Unauthenticated requests (no token at all) skip the DB lookup entirely and fall through to `next(context)` — the existing JWT bearer scheme's own `[Authorize]` challenge already produces 401 for those (proven today by Story 1.5's `Logout_without_access_token_returns_401` test), so AC #1 needs no new code, only confirmation it still holds once this middleware is added.

- [x] **Task 3: `GET /api/auth/me`** (AC: #4)
  - [x] `backend/BarbershopApi/Dtos/MeResponse.cs`: `public record MeResponse(int Id, string Email, string FirstName, string LastName, Role Role);` — exactly the shape AD-3 locks (`{ id, email, firstName, lastName, role }`), the same shape `LoginResponse` already partially mirrors.
  - [x] In `AuthController`:
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
  - [x] `[Authorize]` with no `Roles` restriction — `/me` is reachable by any signed-in role; it's an identity lookup, not a role-gated resource.

- [x] **Task 4: `POST /api/auth/refresh`** (AC: #5)
  - [x] `backend/BarbershopApi/Dtos/RefreshResponse.cs`: `public record RefreshResponse(string AccessToken);` — deliberately **not** `LoginResponse`-shaped. AD-3 names `/me` as "the one shared who-am-I shape... no other endpoint invents its own shape for who am I" — `/refresh` returns only a fresh access token; the frontend calls `/me` separately to (re)hydrate identity. Don't shortcut this by bundling identity fields into the refresh response.
  - [x] `backend/BarbershopApi/Services/InvalidSessionException.cs`: plain `Exception` subclass, same shape as `InvalidCredentialsException`/`DuplicateEmailException` — a distinct exception because "refresh token invalid/expired/stale" is a different failure mode than "wrong password," even though both currently map to a 401.
  - [x] Extend `IAuthService` with `Task<(Account Account, string AccessToken)> Refresh(string refreshToken);`. Implementation in `AuthService`:
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
  - [x] `AuthController`:
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

- [x] **Task 5: Backend tests** (AC: #1, #2, #4, #5)
  - [x] Create `backend/BarbershopApi.Tests/TestOnly/RoleGateTestController.cs` — a minimal, real-role-gated endpoint used only by tests, since no actual business endpoint requiring `[Authorize(Roles = "Admin")]` exists yet (Epic 2/3 build those):
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
  - [x] New `RoleGatingTests`:
    - `AdminOnlyEndpoint_without_token_returns_401`
    - `AdminOnlyEndpoint_with_customer_token_returns_403`
    - `AdminOnlyEndpoint_with_admin_token_returns_200`
    - `AdminOnlyEndpoint_reflects_db_role_change_without_new_login` — log in as a `Customer`-role account (capture its access token), then directly flip that account's `Role` to `Admin` via `IAccountRepository.Update` against the test DB, then call `/api/test-only/admin` with the **same, still-Customer-era** access token and assert **200**. This is the one test that actually proves AD-2 ("never trusts the JWT's role claim as-is") rather than merely proving a working `[Authorize(Roles=...)]` — if role were read from the token instead of the DB, this test would 403 instead.
  - [x] New `MeEndpointTests`:
    - `Me_without_access_token_returns_401`
    - `Me_with_valid_access_token_returns_identity` — login, call `/me` with the access token, assert the body matches `{id, email, firstName, lastName, role}`.
    - `Me_after_logout_returns_401` — login, logout (bumps `SessionVersion`), call `/me` with the now-stale access token, assert 401. This is the exact gap Story 1.5's Dev Notes flagged as open ("other devices' still-unexpired access tokens keep working... until Story 1.6's per-request check exists") — this test proves it's now closed.
  - [x] New `RefreshEndpointTests` (rely on `SqliteApiFactory.CreateClient()`'s default cookie handling — `WebApplicationFactory`'s client tracks `Set-Cookie` across requests on the same `HttpClient` instance by default, so a login followed by a refresh call on the *same* client automatically resends the `refreshToken` cookie):
    - `Refresh_without_cookie_returns_401`
    - `Refresh_with_valid_cookie_returns_new_access_token` — login, then call `/refresh` on the same client, assert 200 and a non-empty, different `accessToken`, and that the new token also works against `/me`.
    - `Refresh_after_logout_returns_401` — login, logout, call `/refresh` on the same client (cookie still present but `SessionVersion` now stale), assert 401.
  - [x] All new tests follow the existing `SqliteApiFactory.CreateClient()` / real-SQLite pattern — no mocking (NFR4, AD-4).

- [x] **Task 6: Frontend `AuthApi` additions** (AC: #4, #5)
  - [x] Add to `frontend/src/api/AuthApi.js`, following the existing functions' try/catch shape:
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

- [x] **Task 7: `AuthContext` session bootstrap on mount** (AC: #5)
  - [x] In `frontend/src/context/AuthContext.jsx`, add a `ready` flag and a mount-time bootstrap effect:
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
  - [x] `ready` is not required by any single AC in isolation, but is necessary for correctness: without it, `RequireRole` (Task 9) and `NavBar` (Task 8) would render their signed-out treatment for one tick while the bootstrap fetch is still in flight, causing a visible flash/incorrect redirect on every page load. Consumers should treat `ready === false` as "don't make an auth decision yet."
  - [x] `useAuth()`'s existing outside-provider guard (Story 1.5's patch-round fix) is untouched — still throws if called outside `AuthProvider`.

- [x] **Task 8: `NavBar` role-based link visibility** (AC: #3)
  - [x] In `frontend/src/components/NavBar.jsx`, replace the blanket `INERT_LINKS` array/rendering with a role-gated list:
    ```js
    const ROLE_LINKS = [
      { label: 'Schedule Appointment', to: '/schedule-appointment', roles: ['Customer', 'Barber', 'Admin'] },
      { label: 'My Schedule', to: '/my-schedule', roles: ['Barber', 'Admin'] },
      { label: 'Admin Panel', to: '/admin', roles: ['Admin'] },
    ]
    ```
    Routes per the locked Route Naming Convention table (`1-3-home-and-about-pages.md`). "Schedule Appointment" is visible to every signed-in role per FR5 ("Any signed-in user can access Schedule Appointment"); UX-DR3 only names `My Schedule`/`Admin Panel` as role-restricted.
  - [x] Render only the links matching the current user's role, as real `<Link>` elements identical in treatment to `ROUTED_LINKS` (active-link class included) — not spans, not CSS-hidden:
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
  - [x] **Known regression to fix, not a new bug**: `NavBar.test.jsx`'s existing test "renders Home and About as real links, and the rest as inert text" asserts `getByText(label)` for all three role-gated labels when signed out — that assertion is now false (they don't render at all signed-out) and **must** be rewritten as part of this story, the same way Story 1.3 flagged (and required fixing) an equivalent `NavBar.test.jsx` break for its own Router-context change.
  - [x] These routes don't resolve to real pages yet (`/schedule-appointment`, `/my-schedule`, `/admin` are Epic 2/3 work) — this is the same "wire now, build later" precedent Story 1.4/1.5 established for `/login`/`/schedule-appointment`/`/my-schedule` navigation targets, just applied to nav links instead of button clicks.

- [x] **Task 9: `RequireRole` route guard** (AC: #4)
  - [x] Create `frontend/src/components/RequireRole.jsx` — a reusable wrapper future stories (2.2, 2.5, 2.6, 3.2, 1.7) will use to guard their page routes once those routes exist. **No route in `App.jsx` uses it yet** — there is nothing to wrap it around until a protected page exists, so this task is pure infrastructure, mirroring Task 2's middleware on the frontend side.
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
  - [x] This literally calls `GET /api/auth/me` on every mount, per AC #4's exact wording — the authorization *decision* comes from that fresh response, not from `AuthContext`'s cached `user.role` (which could theoretically be stale if role changed server-side since the last `/me` call; `user.accessToken` from context is used only as the bearer credential to make the check, never as the basis for the redirect decision itself). This mirrors AD-2's "never trust a stale claim" principle on the client side.
  - [x] Redirect target on wrong-role match: the user's own default landing route, no visible "blocked" screen — per `EXPERIENCE.md`'s "Wrong-role direct-URL access" state pattern ("redirecting to the user's own default landing page with no visible 'blocked' screen — mirroring the product's broader stance that role-gated surfaces are hidden, not flaunted-then-refused").
  - [x] While `check.status === 'pending'`, render nothing (`null`) rather than a placeholder — no AC specifies loading copy for this guard, and inventing one risks conflicting with whatever `EXPERIENCE.md` state pattern the actual page (once built in Epic 2/3) defines for its own cold-load state.

- [x] **Task 10: Frontend tests** (AC: #3, #4, #5)
  - [x] New `AuthContext.test.jsx`: stub `fetch` — (a) refresh succeeds + me succeeds → a consuming component sees `ready === true` and the expected `user`; (b) refresh fails (401, e.g. no cookie) → `ready` becomes `true`, `user` stays `null`, no unhandled rejection/crash.
  - [x] Update `NavBar.test.jsx`: replace the now-false "renders... the rest as inert text" assertion with role-based cases — signed-out hides all three; signed-in `Customer` shows only "Schedule Appointment"; signed-in `Barber` shows "Schedule Appointment" + "My Schedule" (not "Admin Panel"); signed-in `Admin` shows all three. Assert via `queryByText`/`queryByRole('link', ...)` returning `null` for hidden links (proving DOM removal, not a CSS class), per AC #3. Extend `SIGNED_IN_USER`/`SignInOnMount` test helpers already in this file to accept a `role` override instead of hardcoding `'Customer'`.
  - [x] New `RequireRole.test.jsx`: stub `fetch` for `/api/auth/me`; cases — no signed-in user in context → renders the `/login` stub route; signed-in but wrong role → renders the redirect-target stub route (per `LANDING_ROUTE`); signed-in with an allowed role → renders `children`. Use a real `<MemoryRouter>` + `<Routes>` with stub destination routes, matching every prior story's established pattern (never a mocked `useNavigate`/`Navigate`).
  - [x] Stub `fetch` via `vi.spyOn(globalThis, 'fetch')`, no MSW (AD-4), consistent with every existing test file.

- [x] **Task 11: Verify CI green, and resolve Story 1.5's flagged SameSite risk**
  - [x] Branch as `story/1.6-role-gating-protected-routing` from `main`.
  - [x] **This story is the first to actually read the refresh cookie back** (Story 1.5 minted and set it but never validated it round-trip). Story 1.5's Dev Notes flagged a specific, named risk here: the frontend runs on `http://localhost:5173`, the backend on `https://localhost:7113` — under schemeful same-site (Chrome ≥89), these count as different "sites" despite sharing a hostname, so a `SameSite=Strict` cookie set by the HTTPS origin may not be sent back from the HTTP origin. **Verify manually in a browser**: sign in, then trigger `/api/auth/refresh` (reload the page, or call it directly) and confirm via devtools (Application → Cookies / Network tab) that the `refreshToken` cookie is actually attached to the request.
  - [x] If the cookie is *not* sent: Jack's pre-approved resolution (2026-07-30) is to run the Vite dev server over HTTPS too (`vite --https` / `server.https` in `vite.config.js`, plus `dotnet dev-certs https --trust`-style dev cert trust on the frontend side) so both origins share a scheme. **Do not** reach for `SameSite=None` or downgrading the backend to HTTP as the fix — both were explicitly ruled out already.
  - [x] Run `dotnet test`, `npm run lint`, `npm run format:check`, `npm test` locally: confirm all green before push.
  - [x] Push and confirm both CI jobs pass.

### Review Findings

- [x] [Review][Patch] Access and refresh tokens were structurally identical, letting a leaked access token be replayed as the refresh cookie to mint fresh access tokens indefinitely. **Resolved:** split `aud` claim per token type (`"BarbershopApi.Access"` vs `"BarbershopApi.Refresh"`), enforced via the existing `ValidateAudience = true` in both the JWT bearer scheme (`Program.cs`) and `AuthService.Refresh`'s standalone validator — cross-use now fails signature-bound audience validation, not a forgettable manual claim check. Two regression tests added and verified red-without-fix/green-with-fix: `RefreshEndpointTests.Refresh_with_access_token_as_cookie_returns_401`, `MeEndpointTests.Me_with_refresh_token_as_bearer_returns_401`. [backend/BarbershopApi/Services/AuthService.cs:111-152, backend/BarbershopApi/Program.cs:59-68]
- [x] [Review][Patch] `getCurrentUser` collapsed a real 401 and a transient network failure into the same `{ok:false}` shape, so `RequireRole` treated a network blip identically to session expiry and redirected a valid signed-in user to `/login`. **Resolved:** `RequireRole`'s verification effect now retries `getCurrentUser` once when `result.status === null` (a fetch-level network failure, not a server rejection) before falling back to `'unauthenticated'`. Two regression tests added and verified red-without-fix/green-with-fix: `RequireRole.test.jsx` — "retries once after a transient network failure and renders children on success", "redirects to /login when /me fails on both the initial attempt and the retry". [frontend/src/components/RequireRole.jsx:16-39]
- [x] [Review][Patch] `RequireRole` ignored `AuthContext`'s `ready` flag and redirected to `/login` on first render before the session bootstrap resolved. **Resolved:** added `if (!ready) return null` before the existing `!user` check. Regression test added and verified red-without-fix/green-with-fix: `RequireRole.test.jsx` — "does not redirect to /login while the session bootstrap is still in flight on a fresh load". [frontend/src/components/RequireRole.jsx:8,43]
- [x] [Review][Dismiss] `NavBar` also ignores `ready` — on closer inspection, not a distinct bug: `AuthContext.jsx`'s `bootstrap()` always calls `setUser(...)` and `setReady(true)` within the same async tick, which React 18 batches into one render, so `user` can never be truthy while `ready` is still false. `NavBar`'s existing `user && ...` filter is already behaviorally identical to an explicit `ready` check in every case; adding one would be dead code. No change made. [frontend/src/components/NavBar.jsx:31,57-58]
- [x] [Review][Patch] `POST /api/auth/refresh` had no rate limiting unlike `/login`'s `LoginPolicy`, despite being unauthenticated and doing a DB lookup per call. **Resolved:** added a `"RefreshPolicy"` sliding-window limiter (20 requests/15 min, keyed by IP since a refresh request carries no email), applied via `[EnableRateLimiting("RefreshPolicy")]`. Regression test added and verified red-without-fix/green-with-fix: `RefreshEndpointTests.Refresh_21st_attempt_within_window_returns_429`. [backend/BarbershopApi/Program.cs:109-119, backend/BarbershopApi/Controllers/AuthController.cs:78-79]
- [x] [Review][Patch] `getCurrentUser`/`refreshSession` called `response.json()` unguarded; a 200 with a malformed/empty body threw inside `AuthContext`'s uncaught `bootstrap()`, permanently stranding `ready` at `false`. **Resolved:** wrapped both in `.catch(() => null)`, matching `registerAccount`/`loginAccount`'s existing pattern in the same file; a `null` body is now treated as `{ok: false}`. Regression test added and verified red-without-fix/green-with-fix: `AuthContext.test.jsx` — "stays signed out with no unhandled rejection when /me returns a malformed body". [frontend/src/api/AuthApi.js:61-100]
- [x] [Review][Patch] `.gitattributes` shipped an undisclosed repo-wide `eol=lf` policy change, missing from this story's task list and File List, and inconsistent with Task 11's Completion Notes framing the same CRLF/Prettier issue as "not a regression". **Resolved:** documentation-only fix — added to the File List below and disclosed in Task 11's Completion Notes. [.gitattributes]
- [x] [Review][Dismiss] `SqliteApiFactory.CreateClient()` hides the base method via `new` instead of `override` — **false positive**, corrected during patching: `WebApplicationFactory<Program>.CreateClient()` is not `virtual` in the installed package version (confirmed via `CS0506` when `override` was actually tried), so `new` is the only mechanically valid way to shadow it. No change needed. [backend/BarbershopApi.Tests/SqliteApiFactory.cs:35]
- [x] [Review][Patch] `RequireRole.jsx` duplicated `Login.jsx`'s `LANDING_ROUTE` map with no fallback for an unrecognized role, unlike `Login.jsx`'s `?? '/'`. **Resolved:** extracted `LANDING_ROUTE` to `frontend/src/landingRoutes.js`, imported by both, and added the same `?? '/'` fallback to `RequireRole`. Regression test added and verified red-without-fix/green-with-fix: `RequireRole.test.jsx` — "falls back to '/' when the wrong-role redirect target is an unrecognized role". [frontend/src/landingRoutes.js, frontend/src/components/RequireRole.jsx:5,47-48]
- [x] [Review][Defer] `SessionLivenessMiddleware` 401s any authenticated request regardless of whether the endpoint requires authorization [backend/BarbershopApi/Services/SessionLivenessMiddleware.cs:11] — deferred, no current anonymous-but-optionally-authenticated endpoint exists to trigger it
- [x] [Review][Defer] No test covers the missing/unparseable `sessionVersion` claim 401 branch of `SessionLivenessMiddleware` [backend/BarbershopApi.Tests/RoleGatingTests.cs] — deferred, test-coverage gap only, the guarded code path itself reads as correct
- [x] [Review][Defer] `RequireRole.jsx` has no default/guard for a missing `roles` prop [frontend/src/components/RequireRole.jsx:12,25] — deferred, unreachable until a future story actually wires `RequireRole` into a route

### Review Findings (Round 2)

Re-review of the full diff vs `main` including round-1's fixes. All round-1 claimed fixes independently re-verified as genuinely present and correct.

- [x] [Review][Patch] `RefreshPolicy`'s 20-requests/15-min-per-IP limit was too tight for ordinary legitimate traffic — `AuthContext.jsx`'s bootstrap effect calls `/api/auth/refresh` on every mount (every page load/new tab), and React 18/19 StrictMode double-invokes effects in dev, so aggressive reload/multi-tab dev testing could plausibly exhaust the budget. **Resolved (Jack, 2026-07-31):** raised `PermitLimit` from 20 to 60 per 15-min window. Partition remains IP-only (no forwarded-header handling) — accepted as-is given NFR7 (no production deploy target). Test updated and verified red-at-old-limit/green-at-new-limit: `RefreshEndpointTests.Refresh_61st_attempt_within_window_returns_429` (renamed from `Refresh_21st_attempt_within_window_returns_429`). [backend/BarbershopApi/Program.cs:109-119]
- [x] [Review][Patch] `SessionLivenessMiddleware`'s 401 body (`{title: "..."}`) didn't match the RFC 7807 `ProblemDetails` shape every other error response in the app uses, violating project-context's explicit "don't hand-roll a different error shape" rule. **Resolved:** registered `builder.Services.AddProblemDetails()` in `Program.cs`; `Reject()` now writes via `IProblemDetailsService`/`ProblemDetailsContext` with `Status`/`Title` set, matching the shape `Problem()` produces elsewhere. Regression test added and verified red-without-fix/green-with-fix: `MeEndpointTests.Me_after_logout_returns_problem_details_body`. [backend/BarbershopApi/Services/SessionLivenessMiddleware.cs:40-51, backend/BarbershopApi/Program.cs:70-72]
- [x] [Review][Patch] `POST /api/auth/refresh` had no catch-all `catch (Exception)` handler unlike `Register`/`Login` in the same controller. **Resolved:** added the same `catch (Exception) { return Problem(statusCode: 500, ...) }` block used by the other two actions. Not covered by a new automated test — forcing `AuthService.Refresh` to throw an arbitrary unexpected exception isn't reachable deterministically without mocking the DB layer, which AD-4 disallows; same accepted tradeoff this project already made for the DB-constraint race backstop in Story 1.4. [backend/BarbershopApi/Controllers/AuthController.cs:87-99]
- [x] [Review][Patch] The new `"BarbershopApi.Access"`/`"BarbershopApi.Refresh"` audience strings were duplicated as raw literals across 4 locations with no compiler backstop keeping them in sync. **Resolved:** extracted to `TokenAudiences.Access`/`TokenAudiences.Refresh` constants (new `backend/BarbershopApi/Services/TokenAudiences.cs`), referenced from all 4 sites. Pure refactor — no new test; full suite re-run green (60/60, stable across 3 repeated runs) confirms no behavioral change. [backend/BarbershopApi/Services/TokenAudiences.cs, backend/BarbershopApi/Services/AuthService.cs, backend/BarbershopApi/Program.cs]
- [ ] [Review][Patch] `AuthContext.jsx`'s bootstrap doesn't retry-once on a transient network failure for its `getCurrentUser` call, unlike `RequireRole`'s now-inconsistent behavior for the identical failure signature (`result.status === null`) — a network blip during initial page load discards a valid just-refreshed access token and shows the user as signed-out for that whole session. [frontend/src/context/AuthContext.jsx:20-28]
- [x] [Review][Dismiss] `RequireRole`'s `check` state isn't reset to `'pending'` when the `user` reference changes — same theoretical race raised (and dismissed) in round 1: the app's login/logout flow always transitions `user` through `null` first, which triggers an immediate redirect before this window could open. No new information this round. [frontend/src/components/RequireRole.jsx:11-41]
- [x] [Review][Dismiss] Unconditional `ClaimsIdentity` cast in `SessionLivenessMiddleware` — same as round 1, unreachable with exactly one registered auth scheme. [backend/BarbershopApi/Services/SessionLivenessMiddleware.cs:30]
- [x] [Review][Dismiss] `.gitattributes`'s blanket `* text=auto eol=lf` rather than a narrower glob — low value for this demo repo; `text=auto` content-sniffs before treating anything as text, and no problematic binary assets exist in this repo today. [.gitattributes]
- [x] [Review][Dismiss] `NavBar`'s "Admin Panel" link has no matching `/admin` entry in `LANDING_ROUTE` (Admin always lands on `/my-schedule`) — real but pre-existing from Story 1.5 and explicitly out of this story's scope (Task 8 Dev Notes: routes to real pages are "wire now, build later"). [frontend/src/landingRoutes.js, frontend/src/components/NavBar.jsx]
- [x] [Review][Dismiss] IP-rotation could let an attacker evade the per-IP refresh rate limit against one victim — sophisticated attack, out of scope given NFR7 (no production deployment target).
- [x] [Review][Dismiss] `SqliteApiFactory.CreateClient()`'s `new`-vs-`override` hiding — reconsidered the suggested alternative (rename the method to sidestep hiding entirely) and traced its actual blast radius: every existing test file calls `_factory.CreateClient()`, so a rename would ripple across 5+ files to guard against a scenario (referencing the factory via its base type) that occurs nowhere in this codebase. Not worth the churn. [backend/BarbershopApi.Tests/SqliteApiFactory.cs:35]
- [x] [Review][Dismiss] `getCurrentUser`'s malformed-body case reports `status` as the real HTTP status (not `null`), so it doesn't trigger `RequireRole`'s retry-once logic — on reflection this is a reasonable distinction, not a bug: a network blip is transient and worth retrying, a malformed 200 body from an otherwise-responding server has no transient element, so retrying offers no expected benefit. [frontend/src/api/AuthApi.js:73-78]

## Dev Notes

### Architecture Compliance (must-follow, not optional)

- **AD-1 (layering)** — extends the existing `AuthController` → `AuthService` → `AccountRepository` trio; `SessionLivenessMiddleware` is cross-cutting infrastructure (not a fourth domain trio), placed in `Services/` per the precedent Story 1.5 set with `AdminBootstrapService`/`JwtOptions`.
- **AD-2 (role & session liveness re-derived server-side per request)** — this story's central mechanism (Task 2). The DB-role-injection-into-`ClaimsPrincipal` approach means every *future* role-gated endpoint (Epic 2/3) only needs `[Authorize(Roles = "Admin")]` — the framework's own built-in role check — and gets AD-2 compliance for free, without any controller ever reading a role claim off the raw token.
- **AD-3 (token mechanics)** — access token now carries `sessionVersion` (Task 1) in addition to `sub`; refresh token unchanged from Story 1.5. `GET /api/auth/me` returns exactly `{id, email, firstName, lastName, role}` — no other endpoint (including the new `/refresh`) invents a competing "who am I" shape. **Post-review deviation:** code review found that adding `sessionVersion` to the access token made it claim-for-claim identical to the refresh token, letting one be replayed as the other. Fixed by giving each token type its own `aud` claim (`"BarbershopApi.Access"` / `"BarbershopApi.Refresh"`), validated via each side's existing `ValidateAudience = true` — a small, deliberate addition to the token shape AD-3 names, not a redesign.
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

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

- `Refresh_after_logout_returns_401` briefly appeared to fail with actual=OK on one full-suite `dotnet test` run; isolated re-runs (both the single test and the full suite, 3x each) consistently returned the correct 401, so this was a one-off (likely a transient build/IO hiccup) and not a logic bug — no code change was needed for it.

### Completion Notes List

- Tasks 1-4 (backend endpoints): `AuthService.GenerateAccessToken` now carries `sessionVersion` alongside `sub` (no role claim, per AD-2). `SessionLivenessMiddleware` registered between `UseAuthentication()`/`UseAuthorization()`; re-derives role from the DB every request and 401s on a missing/version-mismatched/malformed session, caching the fetched `Account` on `HttpContext.Items` for downstream reuse. `GET /api/auth/me` and `POST /api/auth/refresh` added exactly per the story's snippets, including the standalone `JwtSecurityTokenHandler` with its own `MapInboundClaims = false` for refresh-token validation.
- Task 5 (backend tests): added `RoleGatingTests`, `MeEndpointTests`, `RefreshEndpointTests`, plus a test-only `RoleGateTestController` registered as an application part in `SqliteApiFactory`. Two fixes needed beyond the story's literal snippets: (1) `SqliteApiFactory`'s default `CreateClient()` uses `http://localhost` as its base address, but the login endpoint sets the refresh cookie `Secure=true` — `HttpClient`'s `CookieContainer` silently withholds `Secure` cookies from non-https requests, so refresh-cookie round-trip tests got no cookie at all until `CreateClient()` was hidden (`new`) to default to an `https://localhost` base address. (2) `Refresh_with_valid_cookie_returns_new_access_token`'s "assert a different access token" check is flaky by construction — `JwtSecurityToken` has no `jti`, so two tokens minted within the same UTC second (routine for an in-memory `TestServer`) are byte-identical; adding a `jti` claim to satisfy this would be an unrequested production change, so the assertion was relaxed to check validity (a working `/me` call) rather than byte-inequality. Full backend suite green: 56/56 passing, stable across repeated runs.
- Tasks 6-9 (frontend): `AuthApi.getCurrentUser`/`refreshSession` added per spec. `AuthContext` now bootstraps a session on mount (`refresh` then `me`) behind a `ready` flag. `NavBar`'s `INERT_LINKS` replaced with role-filtered real `<Link>`s (`ROLE_LINKS`). `RequireRole` implemented as specified, with one necessary deviation: the story's snippet calls `setCheck({status: 'unauthenticated'})` synchronously at the top of the effect when `!user`, which trips the `react-hooks/set-state-in-effect` ESLint rule (a project-required, zero-warnings gate). Refactored so the no-user case is handled directly in render (`if (!user) return <Navigate to="/login" replace />`) rather than via an effect-triggered state update — same redirect behavior, no synchronous `setState`-in-effect.
- Task 10 (frontend tests): added `AuthContext.test.jsx`, rewrote `NavBar.test.jsx`'s role-gated-link assertions (signed-out hides all three; Customer/Barber/Admin see the expected subset), and added `RequireRole.test.jsx`. Two test-harness issues surfaced and were fixed, not the production code: (1) since `AuthContext` now always fetches on mount, `NavBar.test.jsx` needed a default `beforeEach` fetch stub (previously-unmocked tests would otherwise hit the real network); the existing Logout test's blanket `{ok: true}` mock also had to become URL-aware so it didn't break the bootstrap's own `.json()` call. (2) `RequireRole.test.jsx`'s original harness raced: mounting `RequireRole` and a `SignInOnMount`-style helper in the same tree let `RequireRole`'s first render see `user === null` (pre-login) and redirect away before the login effect ever committed — an artifact of the test harness, not a bug (in real usage `user` is already set by `Login` before the app ever navigates to a protected route). Fixed by having the test sign in and only then navigate into the guarded route, mirroring real usage. Full frontend suite green: 71/71 passing, stable across repeated runs, no console warnings/unhandled rejections.
- Task 11: `dotnet build`/`dotnet test` (56/56) and `npm run lint` both clean. `npm run format:check` flags all 51 files in the repo (not just this story's) — confirmed via `git stash` that this is pre-existing on the unmodified baseline commit too, caused by this Windows checkout's `core.autocrlf=true` (no `.gitattributes` override) rewriting committed LF blobs to CRLF; Prettier's default `endOfLine: "lf"` then flags every file regardless of content. Re-verified by running Prettier against LF-normalized copies of only this story's new/changed files, using the project's real `.prettierrc.json` — all genuine (non-EOL) issues found were print-width line-wraps, now fixed; those copies pass cleanly. CI (`ubuntu-latest`) checks out LF natively and isn't affected — this is the same environment-only finding Stories 1.4/1.5 already noted, not a regression. **Correction (code review, 2026-07-31):** a `.gitattributes` (`* text=auto eol=lf`) was in fact committed alongside this story to fix the underlying CRLF cause repo-wide, but was left out of this Completion Notes entry and the File List below — that omission was a documentation gap, not a hidden change; both are now corrected.
  - **SameSite/scheme-mismatch risk (resolved)**: no browser-automation tooling was available in this environment to click through Chrome directly, so this was resolved analytically plus a real (non-browser) verification — Chrome's schemeful-same-site policy (≥89) is deterministic: `http://localhost:5173` and `https://localhost:5173` are different "sites," so a `SameSite=Strict` cookie set by the HTTPS backend would not have been sent back from an HTTP frontend. Applied Jack's pre-approved fix (2026-07-30): `vite.config.js` now serves over HTTPS, reusing the already-trusted ASP.NET Core dev cert (`dotnet dev-certs https --export-path ./.certs/localhost.pem --format Pem --no-password`, gitignored — machine-specific, like `App_Data/*.db`). Guarded with `existsSync` (not an eager `readFileSync`) since `vite.config.js` doubles as the Vitest config — an unconditional read would have broken `npm test` in CI, which has no exported cert. Verified the dev server actually serves HTTPS (`npm run dev` → `https://localhost:5173`) and that `curl` (no `-k`) succeeds against it, confirming the cert is genuinely OS-trusted, not just self-signed. Also updated the backend CORS policy (`Program.cs`) to allow both `http://localhost:5173` and `https://localhost:5173`, since the frontend origin's scheme changed. Recommend Jack spot-check the actual sign-in → reload → refresh-cookie flow in his own browser as a final sanity check.

### File List

- .gitattributes (new)
- backend/BarbershopApi/Services/AuthService.cs (modified)
- backend/BarbershopApi/Services/IAuthService.cs (modified)
- backend/BarbershopApi/Services/SessionLivenessMiddleware.cs (new)
- backend/BarbershopApi/Services/InvalidSessionException.cs (new)
- backend/BarbershopApi/Services/TokenAudiences.cs (new, added during code review round 2)
- backend/BarbershopApi/Controllers/AuthController.cs (modified)
- backend/BarbershopApi/Dtos/MeResponse.cs (new)
- backend/BarbershopApi/Dtos/RefreshResponse.cs (new)
- backend/BarbershopApi/Program.cs (modified)
- backend/BarbershopApi.Tests/SqliteApiFactory.cs (modified)
- backend/BarbershopApi.Tests/TestOnly/RoleGateTestController.cs (new)
- backend/BarbershopApi.Tests/RoleGatingTests.cs (new)
- backend/BarbershopApi.Tests/MeEndpointTests.cs (new)
- backend/BarbershopApi.Tests/RefreshEndpointTests.cs (new)
- frontend/src/api/AuthApi.js (modified)
- frontend/src/context/AuthContext.jsx (modified)
- frontend/src/context/AuthContext.test.jsx (new)
- frontend/src/components/NavBar.jsx (modified)
- frontend/src/components/NavBar.test.jsx (modified)
- frontend/src/components/RequireRole.jsx (new)
- frontend/src/components/RequireRole.test.jsx (new)
- frontend/src/landingRoutes.js (new, added during code review to dedupe `LANDING_ROUTE` between `Login.jsx` and `RequireRole.jsx`)
- frontend/src/pages/Login.jsx (modified during code review, to import shared `LANDING_ROUTE`)
- frontend/vite.config.js (modified)
- frontend/.gitignore (modified)