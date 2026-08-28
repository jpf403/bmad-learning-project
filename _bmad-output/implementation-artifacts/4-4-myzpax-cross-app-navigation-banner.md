---
baseline_commit: e16d396
---

# Story 4.4: myzPAX Cross-App Navigation Banner

Status: review

## Story

As an SSO-authenticated user,
I want to see the other myzPAX apps I have access to right from this app,
so that I can move between the tools in the suite without a separate portal.

## Acceptance Criteria

1. **Given** a successful z-pax SSO sign-in (Story 4.2's callback), **when** the backend resolves the local account, **then** the z-pax access token obtained during the identity exchange is also set in a short-lived (2-minute), single-use HttpOnly+Secure+SameSite=Strict cookie (`zpaxAccessToken`, path `/api/auth/sso`) alongside the existing refresh-token cookie (FR47, AD-19).
2. **Given** the `GET /api/auth/sso/zpax-token` endpoint, **when** called by an authenticated session that has a pending `zpaxAccessToken` cookie, **then** it returns the token once in the response body and deletes the cookie; a subsequent call, or a password-only session, returns 404 (FR47, AD-19).
3. **Given** the frontend session bootstrap (`AuthContext`), **when** it completes, **then** it calls `GET /api/auth/sso/zpax-token` once; on success, the returned z-pax access token is held in memory alongside the app's own access token — never persisted (FR47, AD-19).
4. **Given** a signed-in session holding a z-pax access token in memory, **when** any authenticated page renders, **then** the myzPAX banner (`banner.js`, loaded from `https://dev.zpax-banner.myzpax.com/banner/v1/banner.js`) mounts directly above the Nav bar via `MyzpaxBanner.init({ getToken, currentAppId: 'barbershop_demo', position: 'static' })`, where `getToken` returns whatever z-pax access token is currently held in memory (UX-DR21).
5. **Given** a signed-in session with no z-pax access token in memory (a password-only login, or an SSO session whose token has already gone stale or been consumed), **when** any authenticated page renders, **then** the banner script is not mounted at all — no wasted external request, no partial widget (FR47).
   > **Note (future consideration, out of scope for this story):** this AC is deliberately final for now — once the token goes stale/is consumed there's no re-acquisition path this session, only a fresh SSO login. A later revision may have SSO-linked accounts transparently re-fetch a fresh z-pax token when the current one goes stale, so the banner survives longer than ~20 minutes per session. Logged in `ARCHITECTURE-SPINE.md`'s Deferred section — do not build this now.
6. **Given** the `currentAppId` value used above (`barbershop_demo`), **when** this story is implemented, **then** it's confirmed against z-pax's actual launcher-registry entry for this app before merge — flagged going in as unverified.
7. **Given** automated tests should never depend on a live external service (mirrors AD-4), **when** this story is implemented, **then** the new endpoint's cookie-present / cookie-absent / already-consumed paths are covered by xUnit + `WebApplicationFactory`, and the frontend's conditional-mount and `getToken`-wiring logic are covered by Vitest with the banner script itself stubbed — never loading the real external script in tests (NFR4, AD-4).

## Tasks / Subtasks

- [x] **Task 1: Surface the raw z-pax access token out of the identity exchange** (AC: #1)
  - [x] `backend/BarbershopApi/Services/ISsoClient.cs`: add a 5th field to the `SsoIdentity` record — `SsoIdentity(string Email, string FirstName, string LastName, string SubjectId, string AccessToken)`. This is a pure data-shape extension; the interface method signature (`ExchangeCodeForIdentity(string code)`) is unchanged.
  - [x] `backend/BarbershopApi/Services/ZPaxSsoClient.cs`: in `ExchangeCodeForIdentity`, the token response is already deserialized into `token` (the local variable holding `ZPaxTokenResponse`) before the `SsoIdentity` is constructed at the bottom of the method — thread `token.AccessToken` into the new field on the returned `SsoIdentity`.
  - [x] `backend/BarbershopApi.Tests/TestOnly/FakeSsoClient.cs`: extend the `NextIdentity` default (`new("john@example.com", "John", "Smith", "1001")`) with a 5th fake token argument, e.g. `"fake-zpax-access-token"`.
  - [x] `backend/BarbershopApi.Tests/ZPaxSsoClientTests.cs`: `ExchangeCodeForIdentity_maps_token_and_userinfo_responses_to_SsoIdentity` (line ~50) constructs an expected `SsoIdentity` for comparison — add the access-token assertion there.

- [x] **Task 2: Hand the z-pax access token to the frontend via a short-lived cookie** (AC: #1)
  - [x] `backend/BarbershopApi/Controllers/AuthController.cs`, `SsoCallback`: `identity` (the `SsoIdentity` resolved from `ssoClient.ExchangeCodeForIdentity(code)`) is already in scope when the method appends the `refreshToken` cookie before its final redirect — append a second cookie there: name `"zpaxAccessToken"`, value `identity.AccessToken`, `HttpOnly = true`, `Secure = true`, `SameSite = SameSiteMode.Strict`, `Path = SsoStateCookiePath` (the existing `"/api/auth/sso"` constant already used for `ssoState` — reuse it, don't invent a second path constant), `Expires = DateTimeOffset.UtcNow.AddMinutes(2)`.
  - [x] Only set this cookie on the success path (right before the `Redirect($"https://localhost:5173/{landingRoute}")` return) — every earlier `return Redirect(SsoRedirects.Failure)` branch must NOT set it.
  - [x] Tests (`AuthControllerTests.cs`): extend `SsoCallback_with_valid_code_and_state_creates_new_customer_account_and_redirects_to_schedule_appointment` and `SsoCallback_with_valid_code_links_to_existing_barber_account_by_email_preserving_role_and_password` (or add a new dedicated test) to assert a `Set-Cookie` header for `zpaxAccessToken=` is present with `httponly`, `secure`, `samesite=strict`, and `path=/api/auth/sso` — same assertion shape already used for the `ssoState` cookie in `SsoLogin_...` (~line 465-470).

- [x] **Task 3: `GET /api/auth/sso/zpax-token` pickup endpoint** (AC: #2)
  - [x] New DTO `backend/BarbershopApi/Dtos/ZpaxTokenResponse.cs`: `public record ZpaxTokenResponse(string ZpaxAccessToken);` — matches this codebase's existing one-record-per-response-shape convention (`RefreshResponse`, `MeResponse`); System.Text.Json's default camelCase policy (already configured project-wide, see `RefreshResponse.AccessToken` → wire `accessToken`) turns this into `{ "zpaxAccessToken": "..." }`.
  - [x] `AuthController.cs`: new action, placed after `SsoCallback` (keeps the SSO group together):
    ```csharp
    [HttpGet("sso/zpax-token")]
    [Authorize]
    public IActionResult ZpaxToken()
    {
        var token = Request.Cookies["zpaxAccessToken"];
        if (string.IsNullOrEmpty(token))
        {
            return NotFound();
        }

        Response.Cookies.Delete("zpaxAccessToken", new CookieOptions { Path = SsoStateCookiePath });
        return Ok(new ZpaxTokenResponse(token));
    }
    ```
    `[Authorize]` reuses the exact same JWT-bearer + `SessionLivenessMiddleware` pipeline already protecting `/me` — no new auth wiring needed. No rate-limit policy is required here (this isn't a credential-guessing surface like login/password-change — AD-5 doesn't apply).
  - [x] Tests (`AuthControllerTests.cs`): four new cases —
    1. Authenticated call right after a successful `SsoCallback` (cookie pending) → 200, body `{ zpaxAccessToken: <the token from FakeSsoClient's identity> }`.
    2. A second authenticated call immediately after → 404 (cookie already consumed/deleted).
    3. An authenticated call from a plain password-login session (never went through SSO, so no cookie was ever set) → 404.
    4. No bearer token at all → 401 (mirrors the existing `Me`/`Logout` unauthenticated-call pattern elsewhere in this file, if not already implicitly covered by a shared auth test).

- [x] **Task 4: Frontend session bootstrap picks up the z-pax token once** (AC: #3)
  - [x] `frontend/src/api/AuthApi.js`: add `getZpaxToken(accessToken)`, modeled directly on the existing `getCurrentUser(accessToken)` in the same file — `fetch(`${API_BASE_URL}/api/auth/sso/zpax-token`, { credentials: 'include', headers: { Authorization: `Bearer ${accessToken}` } })`. A 404 is an **expected, non-error** outcome here (no z-pax token pending) — return `{ ok: false, status: response.status }` for any non-2xx exactly like `getCurrentUser` already does; the caller (AuthContext) treats `ok: false` as "no banner," not as a failure to surface.
  - [x] `frontend/src/context/AuthContext.jsx`, inside `bootstrap()`: after `meResult.ok` and `setUser({...})` currently runs, call `getZpaxToken(refreshResult.accessToken)` and fold its result into the same `setUser` object: `zpaxAccessToken: zpaxResult.ok ? zpaxResult.zpaxAccessToken : null`. Keep this inside the existing `if (meResult.ok) { ... }` branch (no `zpaxAccessToken` fetch if `/me` itself failed) and respect the existing `cancelled` guard before the final `setUser` call, same as the current code already does for `meResult`.
  - [x] `frontend/src/context/AuthContext.test.jsx`: the 3 existing tests that stub `fetch` (rehydrate-success, refresh-fails, malformed-/me-body) each throw `Unexpected fetch: ${url}` for any unhandled URL — since bootstrap now issues a 3rd fetch, extend each `fetch` mock's `if (url...)` chain to also handle `/api/auth/sso/zpax-token` (return `{ ok: false, status: 404 }` for these three, since none of them represent a real SSO session). Add a **new** test asserting a successful token pickup ends up on the context — extend the `AuthProbe` test helper to also render `user?.zpaxAccessToken` and assert it shows up after all three fetches resolve.

- [x] **Task 5: Conditional banner mount below the Nav bar** (AC: #4, #5)
  - [x] New component `frontend/src/components/MyzpaxBanner.jsx`. Renders `null` whenever `user?.zpaxAccessToken` is falsy (AC #5 — no script tag, no network request at all in that case). When a token is present, mount the vendor script exactly once and call `window.MyzpaxBanner.init({ getToken, currentAppId: 'barbershop_demo', position: 'static' })` once it loads, where `getToken` reads the *current* in-memory token (not a stale value captured at first mount) so it stays correct if the value the widget was initialized with is later invalidated by the vendor's own degradation path. Placement is **above** the Nav bar (AC #4) — this is purely a JSX-ordering call in Task 5's `App.jsx` wiring below; nothing about the component itself changes based on where it's mounted.
  - [x] Extract the actual `<script>`-injection into its own small function (e.g. a local `mountBannerScript(src, onload)` helper, or a separate tiny module under `frontend/src/lib/`) so tests can substitute it entirely — AC #7 and AD-4 both require that **no test ever causes jsdom to attempt loading the real `https://dev.zpax-banner.myzpax.com/...` URL**. This is this story's own design call (no existing precedent for a vendor-script-loading seam anywhere in this codebase) — pick whichever shape reads most naturally once you're in the file, but the point is: the production component must not be untestable by construction.
  - [x] Guard against a double-mount under React's `StrictMode` double-invoke of effects (the codebase already has this exact class of bug in mind — see Previous Story Intelligence below): don't call `init` a second time (or inject a second `<script>` tag) if the effect re-runs.
  - [x] Wire `<MyzpaxBanner />` into `frontend/src/App.jsx` directly **above** `<NavBar />` — i.e. as the first child inside `<AuthProvider>`, before `<NavBar />`, still above `<main>`. This requires `useAuth()`, so it must render inside the provider tree, same as `NavBar` already does.
  - [x] Tests: new `frontend/src/components/MyzpaxBanner.test.jsx` — (a) renders nothing / calls no script-loading seam when `user` is `null` or `user.zpaxAccessToken` is falsy; (b) when a token is present, asserts the stubbed loader is invoked and, once "loaded," `MyzpaxBanner.init` is called with `currentAppId: 'barbershop_demo'` and `position: 'static'`, and that calling the passed `getToken()` returns the current token; (c) a `StrictMode`-wrapped regression test asserting `init` is called exactly once (mirror the pattern Story 4.3 already established for its own StrictMode effect regression test in `Login.test.jsx`).
  - [x] `frontend/src/App.test.jsx`: no required change (App.jsx's own wiring is a one-line addition; `MyzpaxBanner.test.jsx` is the real coverage) — only touch it if a mounted-but-untokened `AuthProvider` in the existing tests would otherwise throw from `MyzpaxBanner` reading `useAuth()` before `ready` is true; if so, confirm `MyzpaxBanner` treats "no user yet" identically to "no token" (render `null`, do nothing) rather than crashing during the loading window.

- [ ] **Task 6: Verify `currentAppId` against z-pax's launcher registry** (AC: #6)
  - [ ] This is **not a code task** — flag explicitly for Jack: confirm `barbershop_demo` is this app's actual registered `currentAppId` in z-pax's launcher registry before the story is merged. Do not assume it's correct and do not silently change it — if Jack confirms a different value, update the literal in `MyzpaxBanner.jsx` (and nowhere else — it's used in exactly one place).

- [x] **Task 7: Check `deferred-work.md`**
  - [x] Re-read `_bmad-output/implementation-artifacts/deferred-work.md` in full at kickoff. The most recent entry (Story 4.3's review, 2026-08-26) covers the CSRF/state-validation bypass in `AuthController.SsoCallback`/`ZPaxSsoClient` — this story doesn't touch that code path (state validation, `BuildAuthorizationUrl`) at all, only the *success* tail of `SsoCallback` and a brand-new endpoint; **checked, still not applicable, remains deferred** (it is explicitly out of scope per the sprint-change-proposal).
  - [x] No other open item touches `AuthController.cs`, `ZPaxSsoClient.cs`, `AuthContext.jsx`, or `App.jsx`.

- [x] **Task 8: Verify CI green and branch/PR**
  - [x] Branch as `story/4.4-myzpax-cross-app-navigation-banner` from `main`. (Already checked out at kickoff.)
  - [ ] Push and confirm both CI jobs (Backend .NET, Frontend Vite/React) green before merging (AD-11). **Left for Jack** — per standing project practice, push/PR/CI verification steps are his to run and approve individually, not performed by the dev agent.

## Dev Notes

### Architecture Compliance (must-follow, not optional)

- **AD-19 addendum (myzPAX banner support, FR47)** — this is the authoritative spec for this entire story; see the full paragraph in `ARCHITECTURE-SPINE.md`. Key points already folded into the tasks above: the `zpaxAccessToken` cookie is 2-minute, single-use, HttpOnly+Secure+SameSite=Strict, scoped to the existing `/api/auth/sso` path; the pickup endpoint is `[Authorize]`'d via this app's own session (not a new auth mechanism); the frontend calls it exactly once during bootstrap; the token is held in memory only and never re-fetched.
- **AD-3 (token transport)** — the z-pax access token follows the *same* in-memory-only philosophy already governing this app's own access token: never `localStorage`/`sessionStorage`, lost on a hard refresh, no persistence layer added. This story doesn't touch how the app's *own* tokens are handled — only adds a second, unrelated in-memory value alongside them.
- **AD-4 (no live external dependencies in tests)** — mirrors the existing `ISsoClient`/`FakeSsoClient` pattern on the backend (already in place, untouched by this story) and extends the same philosophy to the frontend for the *first* time: the banner's vendor script must never actually load in a test run. This is a new kind of test seam for this codebase (no prior story has stubbed a third-party `<script src>` injection) — see Task 5's note on extracting a loader seam.
- **No schema migration** — confirmed by the sprint-change-proposal; nothing in this story touches `Account` or any EF Core migration.
- **AD-13 (CORS)** unaffected — the pickup endpoint is same-origin-authenticated exactly like `/me`/`/refresh`; no new cross-origin surface. The banner script itself is loaded directly by the browser from z-pax's CDN, not proxied through this app's API, so it isn't subject to this app's CORS policy at all.
- **This story only extends the success tail of `AuthController.SsoCallback`** — the failure branches (`Redirect(SsoRedirects.Failure)`), `SsoLogin`, and `BuildAuthorizationUrl`/state-validation logic are all out of scope and must be left exactly as they are (including the known, already-deferred `[DEBUG-TEMP]` CSRF gap — do not touch it as part of this story).

### Design Decisions This Story Must Make (epics/architecture leave these open)

- **The script-loading test seam's exact shape** (a local function vs. a separate module under `frontend/src/lib/`) — no existing precedent in this codebase for stubbing a vendor `<script>` tag in tests. Whatever shape is chosen, the non-negotiable constraint is: a test must be able to fully substitute it so jsdom never attempts the real network request.
- **`getToken`'s exact implementation (closure vs. ref)** — the AC's literal wording is "returns whatever z-pax access token is currently held in memory," which in practice is stable after the one-time bootstrap fetch in this app's current design (no token refresh exists per AD-19's deferred section), so a plain closure over the current render's `user.zpaxAccessToken` is very likely sufficient — but confirm this doesn't produce a stale value across re-renders in whatever mount strategy Task 5 lands on.
- **`MyzpaxBanner.jsx`'s exact `useEffect` dependency shape** for StrictMode-safety — mirror whatever pattern already proved out for Story 4.3's own StrictMode regression test in `Login.jsx`/`Login.test.jsx` (a lazy-initializer style avoided a `set-state-in-effect` ESLint trip there; this component's effect doesn't call `setState` at all, so that specific trap likely doesn't apply here, but double-check).

### Testing Requirements

- Backend: xUnit.v3 + `WebApplicationFactory` against a real SQLite instance, using the existing `FakeSsoClient` test double (already registered in the test host) — never the real `ZPaxSsoClient`. New tests follow the exact `NewSsoClient()` / cookie-assertion patterns already established in `AuthControllerTests.cs` for `SsoLogin`/`SsoCallback`.
- Frontend: Vitest + jsdom + React Testing Library + `user-event`. `AuthApi.js`'s new `getZpaxToken` is stubbed the same way every other `AuthApi` function already is in this codebase's tests (`vi.spyOn(globalThis, 'fetch')`, matched by URL suffix) — no new mocking library. The banner script itself must be stubbed at the loader-seam level (see Task 5) — this is the one genuinely new test pattern this story introduces.
- No Playwright/e2e coverage expected for this story (optional in this project, not otherwise used for Epic 4).

### Project Structure Notes

- **Modified, not new:** `backend/BarbershopApi/Services/ISsoClient.cs`, `backend/BarbershopApi/Services/ZPaxSsoClient.cs`, `backend/BarbershopApi/Controllers/AuthController.cs`, `frontend/src/api/AuthApi.js`, `frontend/src/context/AuthContext.jsx`, `frontend/src/App.jsx`, and their corresponding test files (`ZPaxSsoClientTests.cs`, `AuthControllerTests.cs`, `AuthContext.test.jsx`, `TestOnly/FakeSsoClient.cs`).
- **New:** `backend/BarbershopApi/Dtos/ZpaxTokenResponse.cs`, `frontend/src/components/MyzpaxBanner.jsx` + `frontend/src/components/MyzpaxBanner.test.jsx` (and optionally a small script-loader helper module under `frontend/src/lib/`, per Task 5's design decision).
- **No new backend endpoint file/controller** — `GET /api/auth/sso/zpax-token` lives in the existing `AuthController`, per AD-1's one-controller-per-domain-concept rule (SSO stays folded into Auth, per AD-19).
- This is the 4th and final story of Epic 4.

### Previous Story Intelligence (Story 4.3)

- Story 4.3 hit and worked around an ESLint `react-hooks/set-state-in-effect` trip when a plain effect body called `setFormError` directly — it switched to a lazy `useState` initializer instead. `MyzpaxBanner`'s effect calls `window.MyzpaxBanner.init(...)`, not `setState`, so this specific trap probably doesn't apply — but if the eventual implementation needs any local React state (e.g. a "script loaded" flag), watch for the same lint rule.
- Story 4.3 added a StrictMode-wrapped regression test for its new effect (mirroring `MySchedule.test.jsx`'s existing pattern) specifically because a double-invoked effect is a real risk class in this codebase — Task 5 above asks for the same treatment on `MyzpaxBanner`'s script-mount effect.
- Test fixture convention confirmed again: `FakeSsoClient.NextIdentity` already uses "John Smith" as its placeholder name — Task 1's edit to add a 5th field must not introduce a real name anywhere.
- Story 4.3 was frontend-only by design; this story is backend+frontend, more similar in shape to Story 4.2 (backend SSO plumbing) plus a frontend wiring layer.

### Git Intelligence Summary

Recent commits: `e16d396` (Epic 4 doc update for the banner — this story's baseline) → `a5852e8` (Story 4.3 merged via PR #21) → `4529578` (Story 4.3 created) → `4ba2e3d` (Story 4.2 merged via PR #20). Established rhythm unchanged: create the story on `main`, implement on `story/{epic}.{story}-{slug}`, PR with a summary, merge once both CI jobs are green, delete the branch — Task 8 follows this unchanged.

### References

- [Source: _bmad-output/planning-artifacts/epics.md §Epic 4, §Story 4.4] — story statement, seven acceptance criteria
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-08-26.md] — full rationale, decision log, and explicit scope boundaries for this story (SSO-only banner, no refresh-token infrastructure, unverified `currentAppId`)
- [Source: _bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/ARCHITECTURE-SPINE.md#AD-19] — full technical spec for the cookie hand-off and pickup endpoint (the "myzPAX banner support (FR47)" paragraph), plus #AD-3, #AD-4, #AD-13 for cross-cutting constraints
- [Source: _bmad-output/implementation-artifacts/4-3-sign-in-with-zpax-login-page-ui.md] — previous story in this epic; StrictMode-effect regression-test precedent and confirms no frontend SSO code existed before it
- [Source: backend/BarbershopApi/Controllers/AuthController.cs] — exact current `SsoCallback`/`SsoLogin` implementation this story extends; `SsoStateCookiePath` constant to reuse
- [Source: backend/BarbershopApi/Services/ISsoClient.cs, ZPaxSsoClient.cs] — `SsoIdentity` record and its one real implementation; where the raw access token is currently discarded
- [Source: backend/BarbershopApi.Tests/TestOnly/FakeSsoClient.cs, AuthControllerTests.cs] — existing SSO test doubles/patterns (`NewSsoClient()`, cookie-attribute assertions) to extend rather than reinvent
- [Source: frontend/src/context/AuthContext.jsx, AuthContext.test.jsx] — exact current bootstrap sequence and its existing fetch-stubbing test convention (all 3 tests need updating for the new 3rd fetch call)
- [Source: frontend/src/api/AuthApi.js] — `getCurrentUser`'s shape, directly mirrored by the new `getZpaxToken`
- [Source: frontend/src/App.jsx, components/NavBar.jsx] — exact mount point for the new `<MyzpaxBanner />` (above `<NavBar />`, inside `<AuthProvider>`)
- [Source: _bmad-output/implementation-artifacts/deferred-work.md] — most recent entry (Story 4.3's CSRF/state-bypass deferral) confirmed not touched by this story (Task 7)
- [Source: project-context.md §Framework-Specific Rules (React); §Testing Rules; §Critical Don't-Miss Rules]

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

- `dotnet test BarbershopApi.Tests/BarbershopApi.Tests.csproj` — 292 passed, 4 skipped (pre-existing `[DEBUG-TEMP]` CSRF-bypass skips, unrelated to this story), 0 failed.
- `npx vitest run` (frontend) — 21 test files, 198 tests passed.
- `npx eslint .` and `npx prettier --check .` (frontend) — both clean. One lint error was hit and fixed during Task 5 (`react-hooks/refs`: a ref was being mutated during render in `MyzpaxBanner.jsx`; moved the `tokenRef.current = token` assignment into its own `useEffect([token])` instead of assigning inline during render).
- Empirically verified (via a throwaway test, since removed) that the SSO callback's pre-existing `refreshToken` cookie is *not* narrowed to the `/api/auth/sso` path by the browser's/`.NET`'s default-cookie-path behavior — a theoretical concern raised while reviewing `AuthController.SsoCallback`, ruled out before writing Task 3's tests around it. No code change resulted; not logged to `deferred-work.md` since nothing was actually found.

### Completion Notes List

- Tasks 1–5, 7 complete: TDD red→green for every task, no regressions in the full backend (292/296, 4 pre-existing skips) or frontend (198/198) suites, and clean `eslint`/`prettier --check`.
- **Task 6 intentionally left unchecked — needs Jack's action before merge.** AC #6 requires confirming `barbershop_demo` against z-pax's actual launcher-registry `currentAppId` entry; this is not something the dev agent can verify. The literal lives in exactly one place: `frontend/src/components/MyzpaxBanner.jsx`'s `CURRENT_APP_ID` constant — update it there (and only there) if z-pax's registry disagrees.
- Task 8: branch was already `story/4.4-myzpax-cross-app-navigation-banner` off `main` at kickoff (checked). Push/PR/CI verification left for Jack per standing project practice (matches Stories 3.1–4.3's identical pattern).
- Task 7: re-read `deferred-work.md` in full; the only recent entry touching this area (Story 4.3's CSRF/state-validation bypass) is unrelated to this story's diff (success-tail-only + new endpoint) — checked, remains deferred, not touched.

### File List

**New:**
- `backend/BarbershopApi/Dtos/ZpaxTokenResponse.cs`
- `frontend/src/components/MyzpaxBanner.jsx`
- `frontend/src/components/MyzpaxBanner.test.jsx`
- `frontend/src/lib/loadScript.js`

**Modified:**
- `backend/BarbershopApi/Services/ISsoClient.cs`
- `backend/BarbershopApi/Services/ZPaxSsoClient.cs`
- `backend/BarbershopApi/Controllers/AuthController.cs`
- `backend/BarbershopApi.Tests/TestOnly/FakeSsoClient.cs`
- `backend/BarbershopApi.Tests/ZPaxSsoClientTests.cs`
- `backend/BarbershopApi.Tests/AuthControllerTests.cs`
- `frontend/src/api/AuthApi.js`
- `frontend/src/context/AuthContext.jsx`
- `frontend/src/context/AuthContext.test.jsx`
- `frontend/src/App.jsx`
- `frontend/src/components/NavBar.jsx`
- `frontend/src/components/NavBar.test.jsx`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Change Log

- 2026-08-26: Implemented Tasks 1–5 and 7 (backend token hand-off cookie + pickup endpoint, frontend bootstrap pickup, conditional banner mount) via red→green TDD per task. Full backend/frontend suites green, lint/format clean. Task 6 (currentAppId registry confirmation) and part of Task 8 (CI push/PR) intentionally left for Jack.
- 2026-08-27: [Bug fix, found by Jack during manual review] Signing out left the myzPAX banner visibly stranded on screen. Root cause: the vendor widget has no documented teardown/destroy call, and `NavBar.jsx`'s Logout handler only ever did a client-side `navigate('/')`, which never removes DOM/script state the vendor's `init()` injected outside React's control. Fix: `handleLogout` now does a full-page navigation (`window.location.href = '/'`) instead, guaranteeing all such state is cleared on sign-out. Added `NavBar.test.jsx` coverage asserting a full navigation (not a route change) happens on Logout.
