---
baseline_commit: 13e3969e9c57a196f2303a3574771937c7bc28b9
---

# Story 1.5: Sign In, Sign Out, and First-Admin Bootstrap

Status: in-progress

## Story

As a registered user,
I want to sign in with my email/password and sign out when done,
so that I can securely access my account; and as the shop owner, I want an admin account seeded automatically on first startup, so that I never need a manual backdoor to get one.

## Acceptance Criteria

1. **Given** valid credentials, **when** submitted on Login, **then** the user is authenticated (access token in memory, refresh token in an HttpOnly+Secure+SameSite=Strict cookie) and routed per FR4 (customer → `/schedule-appointment`, barber/admin → `/my-schedule`) — the destination pages themselves are built in Epic 2 (Stories 2.2/2.5/2.6); this story only wires the routing decision (FR4).
2. **Given** invalid credentials (unregistered email or wrong password), **when** submitted, **then** the same generic "Invalid email or password." error is shown in both cases, with no indication of which was wrong (FR2).
3. **Given** repeated failed login attempts for the same email+IP, **when** a 6th attempt is made within the trailing 15-minute sliding window, **then** the API returns 429 and the on-screen copy reads "Too many attempts. Try again in a few minutes." — a deliberate, product-decided divergence from AD-5's "identical message" wording (see Dev Notes — Rate-Limit Message Divergence).
4. **Given** a signed-in user, **when** they open the profile-icon dropdown and click Logout, **then** their session ends server-side and every open tab/device for that account is signed out immediately (FR23) — enforced via a `SessionVersion` bump this story writes; full multi-device enforcement lands when Story 1.6 adds per-request `SessionVersion` re-checking (see Dev Notes — Scope Boundary With Story 1.6).
5. **Given** no admin account exists yet, **when** the app starts for the first time, **then** exactly one admin account is created from `AdminSeed__Email`/`AdminSeed__Password` environment variables via an `IHostedService` running after `Database.Migrate()` (FR31, AD-6).
6. **Given** the nav bar, **when** a user is signed out, **then** Login/Register buttons show; **and** when signed in, a profile-icon dropdown (Account, Logout) shows instead (FR29).

## Tasks / Subtasks

- [x] **Task 1: JWT + rate-limiting backend scaffolding** (AC: #1, #2, #3, #4)
  - [x] Add `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.9 `PackageReference` to `backend/BarbershopApi/BarbershopApi.csproj`. `Microsoft.AspNetCore.RateLimiting` needs **no** package reference — it ships in the ASP.NET Core shared framework (already confirmed for AD-5; same as `PasswordHasher<T>`).
  - [x] Create `backend/BarbershopApi/Services/JwtOptions.cs`: `public class JwtOptions { public string Key { get; set; } = string.Empty; }`. Only the signing key is configurable — Issuer/Audience are non-secret constants, hardcoded in `AuthService` (e.g. `"BarbershopApi"`), not worth a config section.
  - [x] In `appsettings.json`, add an empty placeholder: `"Jwt": { "Key": "" }` — mirrors AD-6's "appsettings.json keeps only empty placeholder keys" pattern, extended here to the JWT signing key for the same reason (never commit a real secret).
  - [x] **Do not** add `AdminSeed`/`Jwt` real values to `appsettings.Development.json`. Both are supplied via env vars only, one credential path everywhere (mirrors AD-6's rationale, extended to the signing key for consistency): locally, set `AdminSeed__Email`, `AdminSeed__Password`, and `Jwt__Key` (a random string ≥32 characters — HMAC-SHA256 needs sufficient key entropy) as shell env vars before `dotnet run`; in CI, the same three as GitHub Actions secrets.
  - [x] In `Program.cs`: bind `builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));`, then read the key once (`var jwtKey = builder.Configuration["Jwt:Key"];`) and **fail fast** if missing/empty: `if (string.IsNullOrWhiteSpace(jwtKey)) throw new InvalidOperationException("Jwt:Key is not configured. Set the Jwt__Key environment variable.")`. Unlike admin-bootstrap (Task 6, which warns-and-skips), a missing signing key makes login categorically non-functional, so fail loudly at startup rather than later at first login attempt.
  - [x] Add JWT bearer auth:
    ```csharp
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false; // see Dev Notes — Claim-Mapping Gotcha
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "BarbershopApi",
                ValidateAudience = true,
                ValidAudience = "BarbershopApi",
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            };
        });
    builder.Services.AddAuthorization();
    ```
    `AddAuthorization()` is currently **missing entirely** even though `UseAuthorization()` is already called (line 52) — this is a pre-existing gap (nothing in the app used `[Authorize]` before this story). Add it now; skipping it makes `[Authorize]` on the new Logout endpoint throw at startup/first-request.
  - [x] Add rate limiting (see Dev Notes — Rate-Limiter Partition-Key Recipe for the exact body-buffering technique):
    ```csharp
    builder.Services.AddRateLimiter(options =>
    {
        options.OnRejected = async (context, token) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.HttpContext.Response.WriteAsJsonAsync(
                new { title = "Too many attempts. Try again in a few minutes." }, token);
        };
        options.AddPolicy("LoginPolicy", httpContext => /* see Dev Notes recipe */);
    });
    ```
  - [x] Register the pipeline in this order (append to existing `Program.cs`, don't reorder what's already there): `app.UseHttpsRedirection(); app.UseCors(VitePolicy); app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();` — `UseAuthentication()` must precede `UseAuthorization()` (the existing gap); `UseRateLimiter()` goes before both since the login rate-limit check has nothing to do with auth state.
  - [x] Register `builder.Services.AddHostedService<AdminBootstrapService>();` (Task 6).

- [x] **Task 2: Login request/response DTOs and exception type** (AC: #1, #2)
  - [x] `backend/BarbershopApi/Dtos/LoginRequest.cs`: `Email` (`[Required]` only — **no** `[PlausibleEmail]**. Login isn't in FR1/FR18/FR19's "applies everywhere email is collected/edited" list (epics.md), because signing in doesn't collect/edit an email — don't copy Task 1's Register pattern here.), `Password` (`[Required]` only).
  - [x] `backend/BarbershopApi/Dtos/LoginResponse.cs`: `record LoginResponse(string AccessToken, int Id, string Email, string FirstName, string LastName, Role Role);` — deliberately the same `{id, email, firstName, lastName, role}` shape AD-3 locks for the future `/me` endpoint, plus `accessToken`. Keeps the two contracts aligned when Story 1.6 builds `/me`.
  - [x] `backend/BarbershopApi/Services/InvalidCredentialsException.cs`: plain `Exception` subclass, same shape as `DuplicateEmailException`. One exception type covers both "no such email" and "wrong password" — never branch on which — this is the mechanism behind AC #2's identical-message guarantee.
  - [x] In `Program.cs`, add `builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));` — **new for this story**: nothing before now serialized a `Role` value in a response body, so the enum-to-string converter has never been needed. Without it, `role` would serialize as `0`/`1`/`2` instead of `"Customer"`/`"Barber"`/`"Admin"`, and the frontend's role-based redirect (Task 9) would silently misroute. This is a global config change — every future response containing a `Role` (e.g. Story 1.6's `/me`) inherits it for free.

- [x] **Task 3: `AuthService.Login`** (AC: #1, #2)
  - [x] Extend `IAuthService` with `Task<(Account Account, string AccessToken, string RefreshToken)> Login(LoginRequest request);`.
  - [x] Inject `IOptions<JwtOptions>` into `AuthService`'s constructor (alongside the existing `IAccountRepository`/`IPasswordHasher<Account>`).
  - [x] `Login` logic: `FindByEmail` (already excludes soft-deleted rows per Story 1.2 — a deleted account is already indistinguishable from "no such account," satisfying AD-15 for free, nothing new to build here) → if `null`, `throw new InvalidCredentialsException()`. Else `passwordHasher.VerifyHashedPassword(account, account.PasswordHash, request.Password)` → if `PasswordVerificationResult.Failed`, `throw new InvalidCredentialsException()`. Both `Success` and `SuccessRehashNeeded` count as a valid login (rehashing on `SuccessRehashNeeded` is an optional hardening step, not required by any AC — skip it, don't add it as a drive-by).
  - [x] On success, mint both tokens via two private helpers (`GenerateAccessToken`/`GenerateRefreshToken`) using `JwtSecurityTokenHandler`/`SymmetricSecurityKey`/`SigningCredentials` (namespace `System.IdentityModel.Tokens.Jwt`, `Microsoft.IdentityModel.Tokens`):
    - Access token: claim `sub` = `account.Id.ToString()` only, `expires: DateTime.UtcNow.AddMinutes(60)` (AD-3). No role claim — nothing in this story reads a role claim off the access token; Story 1.6's per-request role re-derivation reads role from the DB by account id, per AD-2 ("never trusts the JWT's role claim as-is"), so putting role on the token would be dead weight that invites exactly the mistake AD-2 warns against.
    - Refresh token: claims `sub` = `account.Id.ToString()` and `sessionVersion` = `account.SessionVersion.ToString()`, `expires: DateTime.UtcNow.AddDays(15)` (AD-3). Nothing in *this* story validates the refresh token yet (Story 1.6 builds `/api/auth/refresh`) — it's minted and cookied here so a signed-in session can eventually survive a reload, but this story doesn't need to prove that end-to-end.
  - [x] Return `(account, accessToken, refreshToken)` — no HTTP/cookie concerns in the service layer (AD-1); the controller sets the cookie.

- [x] **Task 4: `AuthService.Logout` and repository extension** (AC: #4, #5)
  - [x] Extend `IAuthService` with `Task Logout(int accountId);`. Implementation: `FindById(accountId)` → if `null`, return (already-gone account, nothing to invalidate, idempotent no-op — mirrors the project's existing idempotency conventions, e.g. FR30's cancel-twice handling). Else `account.SessionVersion++; await accountRepository.Update(account);`.
    - **Known, deliberately deferred edge case** (do not fix as a drive-by): `Update` uses `RowVersion` optimistic concurrency (Story 1.2) and can throw `DbUpdateConcurrencyException` if another write races this one. Not handled here — no AC requires it, and it's a narrow race with no observable user-facing impact (the account ends up logged out either way, since whichever write wins still bumps `SessionVersion`).
  - [x] Extend `IAccountRepository`/`AccountRepository` with `Task<bool> AdminExists();` → `context.Accounts.AnyAsync(a => a.Role == Role.Admin && a.DeletedAt == null)`. This is exactly the kind of incremental extension Story 1.2 anticipated ("Stories 1.4, 1.5, and 1.7 ... add only business logic ... no further schema changes" — method additions, not schema changes, are expected) and the same pattern Story 3.1 later applies for admin-search/edit methods.

- [x] **Task 5: `AuthController` login/logout endpoints** (AC: #1, #2, #3, #4)
  - [x] `[HttpPost("login")] [EnableRateLimiting("LoginPolicy")]`:
    ```csharp
    try
    {
        var (account, accessToken, refreshToken) = await authService.Login(request);
        Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(15),
        });
        return Ok(new LoginResponse(accessToken, account.Id, account.Email, account.FirstName, account.LastName, account.Role));
    }
    catch (InvalidCredentialsException)
    {
        return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password.");
    }
    ```
    (plus the same `catch (Exception)` → 500 `Problem()` pattern `Register` already uses).
  - [x] `[HttpPost("logout")] [Authorize]`:
    ```csharp
    var accountId = int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
    await authService.Logout(accountId);
    Response.Cookies.Delete("refreshToken");
    return NoContent();
    ```
    Read the claim via the literal `"sub"` name (`JwtRegisteredClaimNames.Sub`), matching `options.MapInboundClaims = false` set in Task 1 — see Dev Notes — Claim-Mapping Gotcha for why this pairing matters.

- [x] **Task 6: First-admin bootstrap `IHostedService`** (AC: #5)
  - [x] Create `backend/BarbershopApi/Services/AdminBootstrapService.cs` implementing `IHostedService`, constructor-injecting `IServiceScopeFactory`, `IConfiguration`, `ILogger<AdminBootstrapService>` (first use of `ILogger` in this codebase — no new package, just the logging already baked into the default host builder).
  - [x] `StartAsync`: open a DI scope, resolve `IAccountRepository` — if `AdminExists()` is true, return. Else read `AdminSeed:Email`/`AdminSeed:Password` from config; if either is missing/blank, `logger.LogWarning(...)` and return (does **not** throw — unlike the JWT key, a missing admin is a recoverable, non-blocking state; the app should still start and let customers register). Otherwise resolve `IPasswordHasher<Account>`, build `new Account { Email = ..., FirstName = "Admin", LastName = "Admin", Role = Role.Admin }` (FirstName/LastName are unspecified anywhere upstream and never surfaced in any spec'd UI for the admin account — "Admin"/"Admin" is a safe placeholder default), hash the password, `Create` it. `StopAsync` → `Task.CompletedTask`.
  - [x] **No special ordering logic needed**: `Database.Migrate()` already runs synchronously in top-level `Program.cs` code *before* `app.Run()`/`app.StartAsync()` — and `IHostedService.StartAsync` only fires once the host starts, which happens inside `Run()`. Registering `AdminBootstrapService` via `AddHostedService` is sufficient; the existing code structure already guarantees the "runs after migrate" ordering AD-6 requires.
  - [x] Concurrent multi-instance startup races are out of scope — NFR7 (local-only, single SQLite instance, no distributed deploy target) makes this scenario inapplicable.

- [x] **Task 7: Backend tests** (AC: #1–#5)
  - [x] **Modify `backend/BarbershopApi.Tests/SqliteApiFactory.cs`**: add a base in-memory config layer inside `ConfigureWebHost` so every test using this fixture has a valid signing key (otherwise the Task 1 fail-fast throws on host startup for *every* existing and new test):
    ```csharp
    builder.ConfigureAppConfiguration((_, config) =>
        config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "test-signing-key-at-least-32-characters-long",
        }));
    ```
    Do **not** default `AdminSeed:Email`/`AdminSeed:Password` here — leave them unset in the shared fixture so ordinary tests exercise the "no admin configured" skip path; bootstrap-specific tests below layer their own values on top via `factory.WithWebHostBuilder(...)`.
  - [x] New `AuthControllerTests` cases (extend the existing file, same `SqliteApiFactory.CreateClient()` pattern):
    - `Login_with_valid_credentials_returns_200_with_access_token_and_sets_refresh_cookie` — register first, then log in; assert 200, `accessToken` present, `Set-Cookie` header contains `refreshToken=...; HttpOnly` (and note `Secure`/`SameSite=Strict` won't necessarily be inspectable via `HttpClient`'s cookie handling in-test — assert on the raw `Set-Cookie` header string instead).
    - `Login_with_unregistered_email_returns_401_generic_message` and `Login_with_wrong_password_returns_401_generic_message` — assert both produce the exact same response body/title, proving AC #2's no-enumeration guarantee.
    - `Login_sixth_attempt_within_window_returns_429_with_rate_limit_message` — 5 failed attempts for the same email+IP, then a 6th; assert 429 and the exact "Too many attempts. Try again in a few minutes." copy.
    - `Logout_with_valid_access_token_returns_204_and_increments_session_version` — log in, capture `accessToken`, call logout with `Authorization: Bearer`, then read the account directly via `IAccountRepository`/`CreateDbContext()` and assert `SessionVersion` incremented by 1.
    - `Logout_without_access_token_returns_401` — no `Authorization` header.
  - [x] New `AdminBootstrapServiceTests` (or add to `MigrationSmokeTests.cs`'s file if a single boot-behavior test class reads more naturally):
    - Admin-seeds-on-first-startup: `factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string,string?> { ["AdminSeed:Email"] = "admin@test.local", ["AdminSeed:Password"] = "TestPassword123!" })))`, then `CreateClient()` to trigger startup, then assert via a DB read that exactly one `Role.Admin` account exists with that email.
    - Admin-not-reseeded-if-one-exists: seed an admin directly via the repository first, boot the factory with the same env-style config, assert still exactly one admin row.
    - Admin-bootstrap-skips-without-throwing-when-unconfigured: boot the plain (unmodified) `SqliteApiFactory` (no `AdminSeed` config) and assert the app still starts successfully (`CreateClient()` doesn't throw) and zero admin accounts exist.

- [x] **Task 8: Frontend `AuthContext` and `AuthApi` additions** (AC: #1, #4, #6)
  - [x] Create `frontend/src/context/AuthContext.jsx` — a new top-level frontend folder. The locked Structural Seed (`frontend/src/{pages,components,api,styles}`) is a scaffold convention from Story 1.1, not a hard per-file architectural rule the way the *backend's* six folders are (those map directly to AD-1's layering; frontend has no equivalent AD constraining folder count). Cross-cutting client session state doesn't fit `pages`/`components`/`api`/`styles` — `context/` is the smallest reasonable addition, not a reinterpretation of an existing folder's purpose.
    ```jsx
    import { createContext, useContext, useState } from 'react'

    const AuthContext = createContext(null)

    export function AuthProvider({ children }) {
      const [user, setUser] = useState(null)
      return (
        <AuthContext.Provider value={{ user, login: setUser, logout: () => setUser(null) }}>
          {children}
        </AuthContext.Provider>
      )
    }

    export function useAuth() {
      return useContext(AuthContext)
    }
    ```
    `user` shape when signed in: `{ accessToken, id, email, firstName, lastName, role }` (the exact `LoginResponse` body) — `null` when signed out. State lives in memory only (`useState`, no `localStorage`/`sessionStorage`), satisfying AD-3's access-token-in-memory rule at the same time as tracking sign-in state.
  - [x] In `App.jsx`, wrap the existing returned JSX in `<AuthProvider>` (import from `./context/AuthContext`).
  - [x] Add to `frontend/src/api/AuthApi.js` — follow `registerAccount`'s existing shape/try-catch conventions, but note both new functions must actually parse/return the JSON body (unlike `registerAccount`, which today only returns `{ ok: true }` on success and discards the 201 body — don't copy that part):
    ```js
    export async function loginAccount({ email, password }) {
      let response
      try {
        response = await fetch(`${API_BASE_URL}/api/auth/login`, {
          method: 'POST',
          credentials: 'include',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ email, password }),
        })
      } catch {
        return { ok: false, status: null, problem: null }
      }

      const body = await response.json().catch(() => null)
      if (response.ok) {
        return { ok: true, session: body }
      }
      return { ok: false, status: response.status, problem: body }
    }

    export async function logoutAccount(accessToken) {
      try {
        await fetch(`${API_BASE_URL}/api/auth/logout`, {
          method: 'POST',
          credentials: 'include',
          headers: { Authorization: `Bearer ${accessToken}` },
        })
      } catch {
        // best-effort: caller clears local session regardless of network outcome
      }
    }
    ```

- [x] **Task 9: Build the Login page** (AC: #1, #2, #3)
  - [x] Create `frontend/src/pages/Login.jsx` + `Login.css` + `Login.test.jsx`, mirroring `Register.jsx`'s structure (`FormSection` wrapper, controlled `Input`s, `Button` submit, `isSubmitting` guard against double-submit).
  - [x] Fields: `email`, `password` only — **no double-entry password field**. DESIGN.md's double-entry pattern is scoped to Register/Account/Admin-edit/Admin-create contexts only; Login never appears in that list (confirmed against both `DESIGN.md` and `EXPERIENCE.md`) — don't carry Register's pattern over here.
  - [x] Read `location.state?.message` (React Router `useLocation`) and render it as a success banner above the form when present — this is the "Account created. Sign in to continue." message Story 1.4's `Register.jsx` already navigates here with; this story is the first to actually consume it.
  - [x] On submit, call `loginAccount({ email, password })`. Branch on the result:
    - `ok` → `login(result.session)` (from `useAuth()`), then `navigate(LANDING_ROUTE[result.session.role] ?? '/')` where `LANDING_ROUTE = { Customer: '/schedule-appointment', Barber: '/my-schedule', Admin: '/my-schedule' }` (FR4; exact paths per the Route Naming Convention table Story 1.3 locked — Story 1.5's own row already reserves `/login` for this story).
    - `status === 429` → form-level error: "Too many attempts. Try again in a few minutes." (replaces, doesn't append to, any other error).
    - `status === 401` → form-level error: "Invalid email or password."
    - `status === 400` → form-level error: "Please check the form and try again." (blank-field submissions; there's no client-side pre-check to catch these before the round trip, unlike Register's password-match check, since login has no equivalent client-only invariant).
    - anything else (network failure, 500) → "Something went wrong. Please try again."
  - [x] Do **not** build `/schedule-appointment` or `/my-schedule` routes/pages here — same "route target doesn't exist yet" precedent Stories 1.3/1.4 established for `/login` itself; navigating there will render blank until Epic 2, which is expected.

- [x] **Task 10: NavBar signed-in state** (AC: #6)
  - [x] Add `@radix-ui/react-dropdown-menu` to `frontend/package.json` — **new dependency this story introduces**, not in the architecture's originally pinned stack list (only `dialog`/`select`/`popover` were pinned). DESIGN.md's own Component Patterns table names the profile menu as "Radix-powered" explicitly, so this is filling a real gap, not an unauthorized addition — verify the current stable release against React 19 compatibility at install time (same verification step Story 1.1 applied to `react-day-picker`/`@radix-ui/react-select`).
  - [x] In `NavBar.jsx`, read `const { user, logout } = useAuth()`. Wire the currently-inert `Sign In` button: `onClick={() => navigate('/login')}`. When `user` is truthy, replace **both** the `Sign In` and `Register` buttons with a Radix `DropdownMenu` trigger — a circular (`{rounded.full}`) icon button (DESIGN.md leaves the icon's exact visual treatment "left open for implementation" — a simple generic user-glyph is a reasonable default) opening a menu with two items: "Account" (`navigate('/account')` — not built until Story 1.7, same "wire now, build later" precedent) and "Logout".
  - [x] Logout handler: `await logoutAccount(user.accessToken); logout(); navigate('/')`. Call the API best-effort (already swallows network errors per Task 8) before clearing local state, so a slow/failed request doesn't block the user from appearing signed-out locally.
  - [x] Do **not** touch `INERT_LINKS` (`Schedule Appointment`, `My Schedule`, `Admin Panel`) — role-based nav visibility/DOM-removal is explicitly Story 1.6's job (FR3, UX-DR18-equivalent accessibility floor), not this story's.
  - [x] No dedicated `profile-dropdown` design token exists in `DESIGN.md` — reuse the existing `{components.select-dropdown}` token values (`menu-background`, `menu-shadow: floating`, `menu-radius: {rounded.DEFAULT}`, `option-hover-background: {colors.neutral}`) for the dropdown menu's styling, the closest existing pattern, rather than inventing a parallel set of tokens.

- [x] **Task 11: Wire the `/login` route** (AC: #1)
  - [x] In `App.jsx`, add `<Route path="/login" element={<Login />} />`.

- [x] **Task 12: Frontend tests** (AC: #1, #2, #3, #6)
  - [x] `Login.test.jsx`: renders email/password fields inside `FormSection`; successful login navigates to the correct role-based route (test all three roles via stubbed `fetch` responses — use a stub destination `<Route>` inside `<MemoryRouter>`, same pattern `Home.test.jsx`/`Register.test.jsx` established, no mocked `useNavigate`); 401/429/400/network-failure cases each assert their exact form-error copy; renders the success banner when `location.state.message` is set (mirror how `Register.test.jsx` proved the `/login` handoff — this test proves the receiving end). Stub `fetch` via `vi.spyOn` (AD-4, no MSW).
  - [x] `NavBar.test.jsx`: wrap renders in `<AuthProvider>` (new requirement now that `NavBar` calls `useAuth()`); add cases for signed-out (Sign In/Register visible, Sign In navigates to `/login`) vs. signed-in (dropdown visible instead, Logout clears session and navigates to `/`) — signed-in state achieved by seeding the test's `AuthProvider` (may need a lightweight way to preset context value for the test, e.g. a test-only wrapper component, or by driving a real login through the UI if simpler).

- [x] **Task 13: Verify CI green**
  - [x] Branch as `story/1.5-sign-in-sign-out-admin-bootstrap` from `main`. (Checked out as `story/1.5-sign-in-sign-out`.)
  - [x] Run `dotnet test`, `npm run lint`, `npm run format:check`, `npm test` locally: confirm all green before push.
  - [ ] Push and confirm both CI jobs pass.

## Dev Notes

### Architecture Compliance (must-follow, not optional)

- **AD-1 (layering)** — extends the same `AuthController` → `AuthService` → `AccountRepository` trio Story 1.4 started; do not create a second Auth-flavored controller/service. Story 1.6 extends this same trio further (or introduces the role-gating middleware alongside it) — don't preempt that work here.
- **AD-2 (role never trusted from JWT)** — reflected in the access token deliberately carrying no role claim (Task 3); role-based authorization checks are Story 1.6's job, re-derived from the DB, not from anything minted here.
- **AD-3 (token mechanics)** — 60-min access token, in-memory only (`AuthContext`, Task 8); 15-day refresh token, HttpOnly+Secure+SameSite=Strict cookie carrying a `sessionVersion` claim (Task 3/5). Non-rotating refresh tokens (no rotation/reuse-detection) is an accepted, explicitly deferred trade-off — don't build it.
- **AD-5 (rate limiting)** — `Microsoft.AspNetCore.RateLimiting`, sliding window, 5/email+IP/15-min, scoped to `/api/auth/login` only (Task 1/5).
- **AD-6 (admin bootstrap)** — single `IHostedService`, env-vars-only credentials, runs after `Database.Migrate()` (Task 6) — see the ordering note there; it's automatic, not something to engineer.
- **AD-15 (soft-deleted accounts can't sign in)** — comes free from Story 1.2's existing `FindByEmail` filtering; nothing new to build for it in `Login`.

### Rate-Limit Message Divergence (AC #3) — this is intentional, not a bug

The architecture's AD-5 and `SOLUTION-DESIGN.md` §3.6 both specify that a 429 should return the *same generic invalid-credentials message* as a normal failed login, specifically to prevent an attacker from distinguishing "rate-limited" from "wrong password" as separate signals. **`epics.md`'s own Story 1.5 AC explicitly overrides this** with distinct 429 copy ("Too many attempts. Try again in a few minutes."), calling it out by name as "a deliberate divergence from AD-5's 'identical message' wording, per product decision, trading a small enumeration-resistance gap for a clearer user-facing signal." Implement the epics.md version (distinct message) — it's the more specific, more recent, product-approved source for this exact story.

### Claim-Mapping Gotcha (Task 1/5)

`JwtSecurityTokenHandler`'s default inbound claim mapping silently renames the `sub` claim to `ClaimTypes.NameIdentifier` (a long XML-namespace URI string) during token validation. If `options.MapInboundClaims = false` isn't set, `User.FindFirstValue(JwtRegisteredClaimNames.Sub)` in `Logout` will return `null` — not throw, just silently fail to find the claim, then blow up on `int.Parse(null)`. Setting `MapInboundClaims = false` keeps the claim type in `User.Claims` literally as `"sub"`, matching what `GenerateAccessToken` wrote. Pick this approach (rather than reading via `ClaimTypes.NameIdentifier` instead) for symmetry between what's written and what's read.

### Rate-Limiter Partition-Key Recipe (Task 1)

Partitioning by `email + IP` requires the partition-key resolver to read the POST body's `email` field, but `RateLimitPartition` key resolvers are synchronous `Func<HttpContext, ...>` and rate limiting runs before MVC model binding. The workable pattern:

```csharp
options.AddPolicy("LoginPolicy", httpContext =>
{
    httpContext.Request.EnableBuffering();
    using var reader = new StreamReader(httpContext.Request.Body, leaveOpen: true);
    var body = reader.ReadToEndAsync().GetAwaiter().GetResult();
    httpContext.Request.Body.Position = 0; // rewind so MVC's own model binding can still read it

    var email = "unknown";
    try
    {
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("email", out var emailProp))
        {
            email = emailProp.GetString()?.Trim().ToLowerInvariant() ?? "unknown";
        }
    }
    catch (JsonException) { /* malformed body — fall through to "unknown" bucket, model binding will 400 it anyway */ }

    var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    return RateLimitPartition.GetSlidingWindowLimiter($"{ip}:{email}", _ => new SlidingWindowRateLimiterOptions
    {
        PermitLimit = 5,
        Window = TimeSpan.FromMinutes(15),
        SegmentsPerWindow = 3,
        QueueLimit = 0,
    });
});
```

This is a real, if inelegant, ASP.NET Core rate-limiting pattern (buffering + synchronous read + rewind) — not an invented workaround. Lowercase/trim the email for the partition key so `Test@Foo.com`/`test@foo.com` share one bucket, matching per-email intent.

### Known Risk to Verify: SameSite=Strict Across a Scheme Mismatch (flag now, matters more in Story 1.6)

The frontend runs on `http://localhost:5173`; the backend is targeted at `https://localhost:7113` (Story 1.4 chose the HTTPS launch profile specifically to dodge `UseHttpsRedirection`'s 307 hop). Under **schemeful same-site** (default in Chrome since v89), `http://localhost:5173` and `https://localhost:7113` are different "sites" despite sharing a hostname — a `SameSite=Strict` (or even `Lax`) cookie set by the HTTPS origin may not be sent back on requests from the HTTP origin. The architecture's own reasoning ("unaffected by the port difference — site = registrable domain, not port") addresses the *port* difference correctly but doesn't address the *scheme* difference, which schemeful same-site treats as a distinct dimension.

This story mints and cookies the refresh token but never reads it back (nothing here calls `/api/auth/refresh`), so it can't yet be proven to work end-to-end. **Verify manually** during Task 13 (browser devtools → Application → Cookies, after a login) that the `refreshToken` cookie is actually set and visible for the `localhost:7113` origin. If Story 1.6 later finds the cookie isn't being sent back on refresh calls, this scheme mismatch is the likely cause.

**Resolution path (Jack's call, 2026-07-30):** if this bites in Story 1.6, run the Vite dev server over HTTPS too, so frontend and backend share a scheme (`vite --https`, or the equivalent `server.https` config in `vite.config.js`, plus trusting the dev cert the same way `dotnet dev-certs https --trust` was already needed for the backend in Story 1.4). Don't reach for `SameSite=None` or downgrading the backend to HTTP as the fix.

### Scope Boundary With Story 1.6 (important — don't over-build)

Story 1.6 ("Server-Side Role Gating & Protected Routing") owns: `POST /api/auth/refresh`, `GET /api/auth/me`, the per-request DB re-derivation of Role + `SessionVersion` for arbitrary protected endpoints, `[Authorize(Roles = ...)]`-style policies, and frontend route guards. **Do not build any of that here.** This story's `[Authorize]` on `Logout` is intentionally the simplest possible case — "is there any valid, non-expired access token" — with no role check and no DB-driven session validity re-check, because Logout doesn't care about role and a single request re-checking its own about-to-be-bumped `SessionVersion` would be circular. The practical effect: immediately after Logout, *other* devices' still-unexpired access tokens keep working against any (currently nonexistent) protected endpoint until Story 1.6's per-request check exists — this is expected and acceptable, since no protected business endpoints exist yet for that gap to matter against.

### Project Structure Notes

- Backend: extends the existing `Controllers/Services/Repositories/Entities/Dtos/Data` seed — no new backend folders. New files: `Dtos/LoginRequest.cs`, `Dtos/LoginResponse.cs`, `Services/JwtOptions.cs`, `Services/InvalidCredentialsException.cs`, `Services/AdminBootstrapService.cs`. Modified: `AuthController.cs`, `AuthService.cs`, `IAuthService.cs`, `AccountRepository.cs`, `IAccountRepository.cs`, `Program.cs`, `appsettings.json`, `BarbershopApi.csproj`.
- Frontend: adds one new top-level folder, `frontend/src/context/` (see Task 8 for why this doesn't violate the locked backend-style seed — the frontend seed isn't defended by an equivalent AD). New files: `context/AuthContext.jsx`, `pages/Login.jsx`/`.css`/`.test.jsx`. Modified: `App.jsx`, `NavBar.jsx`, `NavBar.test.jsx`, `api/AuthApi.js`, `package.json`.
- Tests: modifies the shared `backend/BarbershopApi.Tests/SqliteApiFactory.cs` fixture (adds a base `Jwt:Key` config) — this affects every test using it, not just this story's new ones; a missing update here would break the *entire* existing test suite the moment Task 1's fail-fast check lands.

### Testing Requirements

- Backend: xUnit.v3 + `WebApplicationFactory` (`SqliteApiFactory.CreateClient()`) against real SQLite, same as Story 1.4 — no mocked `DbContext`/`IAccountRepository`. `WithWebHostBuilder(...)` layers per-test config (e.g. `AdminSeed` values) on top of the shared fixture without needing real OS env vars in tests.
- Frontend: Vitest + jsdom + RTL + user-event; `vi.spyOn(fetch)`, no MSW; real `<MemoryRouter>` + `<Routes>` + stub destination routes for navigation assertions, never a mocked `useNavigate` (established Story 1.3/1.4 pattern).

### Previous Story Intelligence (from Story 1.4)

- `AuthController`/`AuthService`/`IAuthService` currently only have `Register` — this story is a pure extension, not a rewrite; keep `Register`'s existing behavior untouched.
- `Account.SessionVersion` (int) has existed on the entity since Story 1.2 but has never been read or written until this story — confirms the entity was deliberately pre-provisioned for this.
- `AccountRepository.Update` already reloads the entity after save (to pick up the new `RowVersion`) — reuse as-is for the `SessionVersion` bump, no changes needed to `Update` itself.
- Frontend `AuthApi.js`'s existing `registerAccount` doesn't parse/return its success body — don't copy that shortcut into `loginAccount`/`logoutAccount` (Task 8 calls this out explicitly) since the login flow needs the body's session data.
- Story 1.4 established the "wire the navigate() call now, build the destination page later" precedent twice (Home's CTA → `/login`, Register's redirect → `/login`) — this story both resolves one of those (building `/login` itself) and extends the same precedent forward twice more (`/schedule-appointment`, `/my-schedule`).
- `Register.jsx`'s `isSubmitting`-guarded submit handler and layered error-branching (specific status codes before a generic fallback) is the template `Login.jsx` should mirror structurally.

### Git Intelligence Summary

Recent commits (`869f0b7`/`f74670c` Story 1.4 → `3894539`/`35ed372` Story 1.3 → prior Story 1.2) all follow: implement on a short-lived `story/{epic}.{story}-{slug}` branch → self-verify CI green locally → push → review → patch rounds → merge → `done`. Story 1.4 in particular went through two patch rounds after initial review (password policy, error-handling gaps, test coverage additions) before merging — expect a similar review-and-patch cycle here given this story's larger surface area (JWT, rate limiting, a new hosted service).

### Latest Tech Info (verified at story-creation time)

- `Microsoft.AspNetCore.RateLimiting` and `PasswordHasher<T>` both ship in the ASP.NET Core 10 shared framework — no separate `PackageReference` for either. Only `Microsoft.AspNetCore.Authentication.JwtBearer` (10.0.9) is a genuine new package for this story.
- `@radix-ui/react-dropdown-menu` is not currently in `frontend/package.json` (only `@radix-ui/react-dialog` 1.1.21 is installed so far — `react-select`/`react-popover` mentioned in `project-context.md` aren't installed yet either, presumably deferred to the stories that actually need them) — this story is what first pulls in the dropdown-menu primitive; verify current stable version against React 19.2.8 at install time.
- `JwtSecurityTokenHandler.DefaultInboundClaimTypeMap` claim-renaming behavior is current as of .NET 10's `System.IdentityModel.Tokens.Jwt` — verify `MapInboundClaims` is still the correct opt-out property name at implementation time (unchanged across recent .NET versions, but worth a quick confirm).

### References

- [Source: _bmad-output/planning-artifacts/epics.md §Story 1.5] — story statement, AC (including the AD-5 rate-limit-message divergence, stated explicitly in the AC itself), Route Naming Convention cross-reference
- [Source: _bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/ARCHITECTURE-SPINE.md #AD-1–AD-6, #AD-15, #Deferred] — layering, token mechanics, rate limiting, admin bootstrap, soft-delete/login interaction, refresh-rotation deferral
- [Source: _bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/SOLUTION-DESIGN.md §2, §3.2–3.6, §5–§8] — SessionVersion semantics, 401/403 convention, rate-limiter message rationale (the one this story's AC deliberately overrides), stack table, deferred items
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-bmad-learning-project-2026-07-23/DESIGN.md — Components §nav-bar, §form-section; frontmatter `components.select-dropdown`; §Shapes] — profile-dropdown-is-Radix, form-section reuse, circular icon shape, no dedicated profile-dropdown token
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-bmad-learning-project-2026-07-23/EXPERIENCE.md §State Patterns ("Login error", "Login rate-limited", "Registration success"), §Component Patterns ("Profile icon dropdown"), §Interaction Primitives] — exact copy strings, Radix keyboard/focus behavior "for free"
- [Source: _bmad-output/implementation-artifacts/1-3-home-and-about-pages.md §Route Naming Convention] — locked paths for `/login`, `/schedule-appointment`, `/my-schedule`, `/account`, `/admin`
- [Source: _bmad-output/implementation-artifacts/1-4-customer-self-registration.md] — established Auth trio, `SqliteApiFactory` contract, PascalCase 400-error-key convention, camelCase response-body convention, `/login` navigate-before-built precedent
- [Source: backend/BarbershopApi/Controllers/AuthController.cs, Services/AuthService.cs, Services/IAuthService.cs, Entities/Account.cs, Entities/Role.cs, Repositories/AccountRepository.cs, Repositories/IAccountRepository.cs, Program.cs, BarbershopApi.csproj, appsettings.json, appsettings.Development.json, Properties/launchSettings.json] — current (pre-story) state of every backend file this story modifies or extends
- [Source: backend/BarbershopApi.Tests/SqliteApiFactory.cs, AuthControllerTests.cs] — existing test fixture/coverage this story extends
- [Source: frontend/src/App.jsx, components/NavBar.jsx, components/Input.jsx, components/Button.jsx, components/FormSection.jsx, pages/Register.jsx, api/ApiConfig.js, api/AuthApi.js, package.json] — current (pre-story) state of every frontend file this story modifies or extends
- [Source: project-context.md §Technology Stack, §Language-Specific Rules, §Critical Don't-Miss Rules] — package versions, PascalCase/camelCase conventions, rate-limiting/bootstrap/CORS rules

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

### Completion Notes List

- Tasks 1-6 (backend): JWT bearer auth + `LoginPolicy` sliding-window rate limiter wired in `Program.cs`; `LoginRequest`/`LoginResponse`/`InvalidCredentialsException`; `AuthService.Login`/`Logout` (access token carries no role claim per AD-2; refresh token carries `sessionVersion`); `AccountRepository.AdminExists()`; `AuthController` login/logout endpoints; `AdminBootstrapService` hosted service. `dotnet build` required two fixes beyond the story's literal snippets: `RateLimitPartition`/`SlidingWindowRateLimiterOptions` resolve from `System.Threading.RateLimiting`, not `Microsoft.AspNetCore.RateLimiting`; `ClaimsPrincipal.FindFirstValue` needs an explicit `using System.Security.Claims;` (not covered by this project's implicit usings).
- Task 7 (backend tests): extended `SqliteApiFactory` and `MigrationSmokeTests` with the shared `Jwt:Key` in-memory config (the latter wasn't called out in Dev Notes but constructs its own `WebApplicationFactory<Program>` directly, so it would have broken on Task 1's fail-fast otherwise). Added 6 `AuthControllerTests` cases and 3 `AdminBootstrapServiceTests` cases (using `factory.Services.CreateScope()` off the `WithWebHostBuilder`-returned factory to read the DB, since it shares the outer `SqliteApiFactory` instance's db file/connection string). Two things not spelled out in the story required fixes: (1) `HttpContent.ReadFromJsonAsync<LoginResponse>()`'s default client-side `JsonSerializerOptions` don't include the server's `JsonStringEnumConverter`, so the `Role` enum failed to deserialize — added an explicit options instance in the test file; (2) the `Set-Cookie` header's `HttpOnly` attribute is rendered lowercase (`httponly`) — asserted case-insensitively. Full backend suite green: 44/44 passing.
- Tasks 8-11 (frontend): `AuthContext`/`AuthApi` additions exactly as specified; `Login.jsx`/`.css` (no double-entry password field, per Dev Notes); `NavBar.jsx` signed-in profile dropdown via newly-installed `@radix-ui/react-dropdown-menu@2.1.24` (confirmed React 19-compatible peer range); `/login` route wired in `App.jsx` alongside the new `AuthProvider` wrap.
- Task 12 (frontend tests): `Login.test.jsx` (renders fields, success-banner, all three role redirects, 401/429/400/network-failure/500 branches, in-flight disabled state) and `NavBar.test.jsx` extended with a `SignInOnMount` test-only wrapper (calls `useAuth().login()` in a mount effect) to drive signed-in-state coverage without mocking `useNavigate` — Radix's dropdown interacted cleanly with `user-event` in jsdom with no extra polyfills needed. Full frontend suite green: 61/61 passing.
- Task 13: `dotnet test` (44/44), `npm run lint` (clean), `npm test` (61/61) all green. `npm run format:check` initially flagged every file in the repo, including ones untouched by this story — root cause is this Windows checkout's `core.autocrlf=true` rewriting the repo's LF-committed blobs to CRLF on disk; confirmed by re-running Prettier against LF-normalized copies, which found genuine (non-EOL) issues in only 3 of my new/touched files (`AuthContext.jsx`, `Login.test.jsx`, `NavBar.test.jsx` — all line-wrap issues), now fixed. This is the same pre-existing, environment-only finding Story 1.4 already noted, not a regression. CI (`ubuntu-latest`, no autocrlf rewriting) sees LF throughout and isn't affected. Branch creation, commit, and push are left for Jack per standing instruction to review the diff first.

### File List

- backend/BarbershopApi/Dtos/LoginRequest.cs (new)
- backend/BarbershopApi/Dtos/LoginResponse.cs (new)
- backend/BarbershopApi/Services/JwtOptions.cs (new)
- backend/BarbershopApi/Services/InvalidCredentialsException.cs (new)
- backend/BarbershopApi/Services/AdminBootstrapService.cs (new)
- backend/BarbershopApi.Tests/AdminBootstrapServiceTests.cs (new)
- backend/BarbershopApi/BarbershopApi.csproj (modified)
- backend/BarbershopApi/Program.cs (modified)
- backend/BarbershopApi/appsettings.json (modified)
- backend/BarbershopApi/Controllers/AuthController.cs (modified)
- backend/BarbershopApi/Services/AuthService.cs (modified)
- backend/BarbershopApi/Services/IAuthService.cs (modified)
- backend/BarbershopApi/Repositories/AccountRepository.cs (modified)
- backend/BarbershopApi/Repositories/IAccountRepository.cs (modified)
- backend/BarbershopApi.Tests/SqliteApiFactory.cs (modified)
- backend/BarbershopApi.Tests/MigrationSmokeTests.cs (modified)
- backend/BarbershopApi.Tests/AuthControllerTests.cs (modified)
- frontend/src/context/AuthContext.jsx (new)
- frontend/src/pages/Login.jsx (new)
- frontend/src/pages/Login.css (new)
- frontend/src/pages/Login.test.jsx (new)
- frontend/src/App.jsx (modified)
- frontend/src/api/AuthApi.js (modified)
- frontend/src/components/NavBar.jsx (modified)
- frontend/src/components/NavBar.css (modified)
- frontend/src/components/NavBar.test.jsx (modified)
- frontend/package.json (modified)
- frontend/package-lock.json (modified)
