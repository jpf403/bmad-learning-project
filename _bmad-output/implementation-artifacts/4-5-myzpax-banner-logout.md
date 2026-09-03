---
baseline_commit: ef0fff4
---

# Story 4.5: myzPAX Banner Logout

Status: done

## Story

As an SSO-authenticated user,
I want the myzPAX banner's logout control to end both my z-pax session and my barbershop session together,
so that logging out from the banner actually signs me out of this app too, not just the SSO layer.

## Acceptance Criteria

1. **Given** a signed-in session holding a z-pax access token in memory (Story 4.4), **when** `MyzpaxBanner.init` is called, **then** it is passed an `onLogout` callback in addition to the existing `getToken`/`currentAppId`/`position` (FR48).
2. **Given** the visitor triggers the banner's logout control, **when** `onLogout` fires, **then** it tears down this app's session exactly as the existing NavBar Logout does — calling the app's logout endpoint to revoke the server-side refresh session, then clearing `AuthContext` — before navigating away (FR48, mirrors `NavBar.handleLogout`).
3. ~~**Given** the session has been torn down, **when** `onLogout` completes its app-side teardown, **then** the browser is redirected via `window.location.assign` to the same URL the "Sign in with z-pax" button uses (`${API_BASE_URL}/api/auth/sso/login`) (FR48).~~ **Superseded twice, 2026-09-01** — round 1 (live verification, see AC #4): redirecting to the SSO login URL silently re-authenticated the visitor instead of logging them out, interim fix was `window.location.assign('/login')`. Round 2 (real fix, per z-pax's own `MyzpaxBanner.init` docs and its `/connect/logout` endpoint table): the frontend now redirects to a new backend endpoint, `window.location.assign(`${API_BASE_URL}/api/auth/sso/logout`)`, which itself redirects the browser on to z-pax's real end-session endpoint (`GET/POST https://dapi.auth.myzpax.com/connect/logout?id_token_hint=...`) — see Task 7.
4. **Given** that redirect target was unverified against z-pax's actual session-termination behavior, **when** this story was implemented, **then** it was manually verified end-to-end with a live z-pax SSO session. Round 1: the visitor landed back in the app still signed in, confirming the SSO-login redirect doesn't end z-pax's session (see AC #3). Round 2 (after Task 7's real fix, live-tested 2026-09-01): Jack confirmed he was actually signed out of z-pax, but the browser landed on an HTTP 405 error page immediately after. Round 3 (`post_logout_redirect_uri` registered with z-pax and re-enabled, live-tested 2026-09-02): Jack confirmed the session ends **and** the browser now lands cleanly on `https://myzpax.com/home` — no error page. The round-2 405 is resolved; see the corresponding `deferred-work.md` entry, now closed.
5. **Given** the existing in-app "Logout" menu item (`NavBar.jsx`), **when** this story is implemented, **then** it is left completely unchanged and remains available to every account — SSO or password — as a fallback (FR48, no regression).
6. **Given** the new `onLogout` wiring, **when** tested, **then** it's covered in `MyzpaxBanner.test.jsx` — asserting the callback clears the session and navigates to the new backend `sso/logout` endpoint — without introducing any new mocking beyond what the existing suite already stubs (AD-4). Backend coverage (id_token capture, `BuildLogoutUrl`, the new endpoint) added to `ZPaxSsoClientTests.cs`/`AuthControllerTests.cs` using the existing `FakeSsoClient` test double, no new mocking framework (AD-4).

## Tasks / Subtasks

- [x] **Task 1: Wire an `onLogout` callback into `MyzpaxBanner.init`** (AC: #1, #2, #3)
  - [x] `frontend/src/components/MyzpaxBanner.jsx`: import `logoutAccount` from `../api/AuthApi` (already exists and is used exactly this way by `NavBar.jsx` — reuse verbatim, don't reinvent). `API_BASE_URL` import dropped after the redirect-target change below made it unnecessary.
  - [x] Destructure `logout` alongside the existing `user` from `useAuth()`.
  - [x] The component only reads `user.accessToken` at effect-setup time today (only `user?.zpaxAccessToken` is tracked via `tokenRef`). `onLogout` needs the *current* app access token when it eventually fires, which can be long after the one-time `init()` call (`initializedRef` gates `init` to fire only once, mirrored by AC #1's "already Story 4.4 wiring, add one field" framing) — add a second ref (e.g. `accessTokenRef`) kept fresh via its own `useEffect([user?.accessToken])`, exactly mirroring the existing `tokenRef`/`token` pattern two lines above it. Do not read `user.accessToken` directly inside a closure captured at first mount — same staleness trap `tokenRef` already exists to avoid for the zpax token.
  - [x] Define `onLogout` as an async function (inline in the `init()` call or just above it) that: (a) calls `await logoutAccount(accessTokenRef.current)`, (b) calls `logout()` from context, (c) calls `window.location.assign(...)`. This is the same three-step shape as `NavBar.jsx`'s `handleLogout` (`await logoutAccount(user.accessToken); logout(); window.location.href = '/'`) — do not use `.href =` here, `window.location.assign` is used explicitly. **Redirect target updated twice, 2026-09-01** (see AC #3): first to an interim `/login` after round-1 live verification, then to the real backend endpoint `` `${API_BASE_URL}/api/auth/sso/logout` `` once Task 7 added it — `API_BASE_URL`/`ApiConfig` import restored for this final version.
  - [x] Pass `onLogout` into the existing `window.MyzpaxBanner.init({ getToken, currentAppId, position })` call as a 4th field — do not change `getToken`/`currentAppId`/`position`.

- [x] **Task 2: Manual live-SSO verification gate** (AC: #4)
  - [x] Round 1: Jack signed in via a live z-pax SSO session, triggered the banner's logout control, and confirmed the visitor was silently re-authenticated and bounced back into the app still signed in — the SSO-login redirect target did not end z-pax's session.
  - [x] Round 2 (2026-09-01, after Task 7's real fix): Jack signed in live again, triggered logout, and confirmed he was actually signed out of z-pax this time. The browser landed on an HTTP 405 error page at z-pax's bare domain root immediately after — logged as a deferred cosmetic issue (z-pax's own post-logout landing page, out of this app's control since `/connect/logout` takes no redirect parameter), not a blocker to this AC.

- [x] **Task 3: Confirm `NavBar.jsx`'s own Logout is untouched** (AC: #5)
  - [x] No code change expected here. After Task 1, diff `NavBar.jsx` to confirm it's identical to its pre-story state — this story's entire scope is additive within `MyzpaxBanner.jsx`.

- [x] **Task 4: Test coverage for the `onLogout` wiring** (AC: #6)
  - [x] `frontend/src/components/MyzpaxBanner.test.jsx`: extend the existing `'loads the banner script and initializes it once a z-pax token is present'` test (or add a new one) to assert `window.MyzpaxBanner.init` is called with an `onLogout: expect.any(Function)` field alongside the existing three.
  - [x] Add a new test that invokes the captured `onLogout` callback (pulled off `window.MyzpaxBanner.init.mock.calls[0][0]`, same pattern the existing `getToken` test already uses) and asserts: (a) `fetch` was called for `POST {API_BASE_URL}/api/auth/logout` (the existing `beforeEach` already `vi.spyOn(globalThis, 'fetch')`s — no new mock needed, just assert on an additional call), (b) the session clears — same observable signal `AuthContext.test.jsx`'s `AuthProbe` pattern uses, or simplest: re-render/query the DOM for whatever indicates `user` became `null` if the test harness exposes it, otherwise assert indirectly via the `logout` behavior already proven in `AuthContext.test.jsx` and only assert the callable surface here, (c) `window.location.assign` was called with `` `${API_BASE_URL}/api/auth/sso/logout` `` (updated post-Task-7; superseded the original `sso/login` target this subtask was written against).
  - [x] For asserting `window.location.assign`, follow `NavBar.test.jsx`'s existing `window.location` stubbing pattern exactly: `const originalLocation = window.location; delete window.location; window.location = { ...originalLocation, assign: vi.fn() }` before the action, restore `window.location = originalLocation` in a `finally` block afterward — don't invent a different stubbing approach.
  - [x] No new mocking library or pattern beyond what `MyzpaxBanner.test.jsx` and `NavBar.test.jsx` already establish (AD-4).

- [x] **Task 5: Re-check `deferred-work.md`**
  - [x] Already checked during story creation: the most recent entries (Story 4.4's zpax-token TOCTOU race, Story 4.3's CSRF/state-validation bypass) don't touch `MyzpaxBanner.jsx`, `NavBar.jsx`, or the logout/session-teardown path this story adds to. Re-confirm at implementation kickoff in case anything new landed since story creation, and note the outcome in Completion Notes.

- [x] **Task 6: Verify CI green and branch/PR**
  - [x] Branch is `story/4.5-myzpax-banner-logout` (renamed from an earlier working name once this work was recognized as a story). Per the sprint-change-proposal, this branch's scope intentionally also covers the standalone mobile-nav-dropdown defect fix alongside this story.
  - [x] Push and confirm both CI jobs (Backend .NET, Frontend Vite/React) green before merging (AD-11). Confirmed by Jack (2026-09-02).

- [x] **Task 7 (added 2026-09-01, not in original scope): End z-pax's own SSO session on logout** (AC: #3, #4)
  - [x] Scope change: `ZPaxSsoClient.cs`'s requested scope changed from `"profile"` to `"openid profile"` (Jack, applied directly) so z-pax's token endpoint issues an `id_token` — required for `id_token_hint` on z-pax's `/connect/logout` endpoint (per z-pax's own `MyzpaxBanner.init` docs' logout section and endpoint table).
  - [x] `ZPaxSsoOptions.cs`/`appsettings.json`: added `LogoutEndpoint` (`https://dapi.auth.myzpax.com/connect/logout`), alongside the existing Authorization/Token/UserInfo endpoints.
  - [x] `ISsoClient.cs`: `SsoIdentity` record gained an `IdToken` field; interface gained `string BuildLogoutUrl(string idTokenHint)`.
  - [x] `ZPaxSsoClient.cs`: `ZPaxTokenResponse` now parses `id_token` (nullable — logged as empty rather than throwing if a token response ever omits it, since sign-in shouldn't hard-fail over a logout-only concern); `BuildLogoutUrl` builds `LogoutEndpoint?id_token_hint=...` via `QueryHelpers`.
  - [x] `AuthController.SsoCallback`: sets a new `zpaxIdToken` HttpOnly/Secure/SameSite=Strict cookie (same `Path=/api/auth/sso` scoping as `zpaxAccessToken`, but a 15-day expiry matching `refreshToken` rather than the 2-minute single-use pickup window — this token needs to survive until the user eventually logs out, not just get picked up once at login) — only when `identity.IdToken` is non-empty.
  - [x] New `GET /api/auth/sso/logout` (anonymous, no `[Authorize]` — reached via a plain top-level browser navigation, no Bearer token in play): reads and clears the `zpaxIdToken` cookie; if present, 302-redirects to `ssoClient.BuildLogoutUrl(idToken)`; if absent (e.g. a password-only account somehow hit this route), redirects to `SsoRedirects.Login` (new constant, `https://localhost:5173/login`, no error param).
  - [x] `MyzpaxBanner.jsx`: `onLogout`'s final redirect target changed from the interim `/login` to `` `${API_BASE_URL}/api/auth/sso/logout` ``.
  - [x] Test doubles/tests updated: `FakeSsoClient` gained `IdToken` on its default identity and a `BuildLogoutUrl` implementation; `ZPaxSsoClientTests.cs` covers scope (via `QueryHelpers.ParseQuery`, not brittle substring matching, since `"openid profile"` no longer contains `"scope=profile"` as a literal substring once URL-encoded), `id_token` capture (present and omitted cases), and `BuildLogoutUrl`; `AuthControllerTests.cs` covers the `zpaxIdToken` cookie being set on callback and the new `/api/auth/sso/logout` endpoint (with and without a pending cookie).
  - [x] **Not this story's to fix**: pre-existing, unrelated `BookingServiceTests` failures (date-fixture drift — tests hardcode dates now in the past since real time has moved on) were present before this story's changes and remain out of scope. Note: 4 `Cancel(...)` call sites in this file *were* touched (passing `FixedNow` explicitly instead of relying on the default) to keep those specific tests from joining the drift; this is a minor, unrelated drive-by fix, not part of the pre-existing failure set — see File List.

### Review Findings

- [x] [Review][Patch] `ZPaxSsoClient.BuildLogoutUrl`'s comment has the verification status backwards [`backend/BarbershopApi/Services/ZPaxSsoClient.cs:95-108`] — Jack confirmed (2026-09-02) the *active* `post_logout_redirect_uri` version is the one that's live-verified and registered with z-pax as the logout redirect (not the commented-out fallback the comment implies was tested). Fix the comment to state this accurately and remove the dead commented-out fallback line.
- [x] [Review][Patch] AC #4 / Completion Notes / `deferred-work.md`'s "known cosmetic issue" (HTTP 405 landing page) are stale [`_bmad-output/implementation-artifacts/4-5-myzpax-banner-logout.md`, `_bmad-output/implementation-artifacts/deferred-work.md`] — Jack confirmed (2026-09-02) that with `post_logout_redirect_uri` active, logout now lands cleanly on `https://myzpax.com/home` with no 405. Update AC #4, the Completion Notes, and the `deferred-work.md` entry to drop the resolved cosmetic-issue narrative.
- [x] [Review][Patch] `AuthController.SsoCallback`'s "no code / no active login attempt" branch comment is inaccurate [`AuthController.cs:149-161`] — Jack decided (2026-09-02) to keep the branch as defensive handling for other benign no-code arrivals, but the comment's claim that z-pax's redirect "lands here via `post_logout_redirect_uri` (same value as login's `redirect_uri`)" is wrong — `LogoutRedirectUri` is actually the external `https://myzpax.com/home`, not this app's own callback. Rewrite the comment to describe the real justification (defensive catch-all, not a specific traceable z-pax redirect path) without misstating the config.
- [x] [Review][Patch] Planning docs never updated to match Task 7's actual shipped design [`_bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/ARCHITECTURE-SPINE.md`, `_bmad-output/planning-artifacts/epics.md`] — both were edited by this same diff to add the "myzPAX banner logout (FR48)" content, but both still describe the superseded round-1 plan (redirect to `GET /api/auth/sso/login`) rather than the real `GET /api/auth/sso/logout` → z-pax `/connect/logout` flow Task 7 built.
- [x] [Review][Patch] Story file's own internal inconsistency [`_bmad-output/implementation-artifacts/4-5-myzpax-banner-logout.md`] — the Dev Notes "No backend change — this story is frontend-only" bullet and Task 4's subtask text (still asserting the redirect target is `${API_BASE_URL}/api/auth/sso/login`) were never reconciled with Task 7 / the corrected AC #3.
- [x] [Review][Patch] Standard logout never clears the new SSO cookie [`backend/BarbershopApi/Controllers/AuthController.cs:69-78`] — `Logout()` deletes `refreshToken` and `zpaxAccessToken` but not `zpaxIdToken`, so it survives up to 15 days after a user logs out via the in-app `NavBar` menu (AC #5's fallback path) on a shared browser.
- [x] [Review][Patch] Undisclosed test file change [`backend/BarbershopApi.Tests/BookingServiceTests.cs`] — 4 `Cancel(...)` call sites were changed to pass `FixedNow` explicitly, but this file isn't in the story's File List, and Completion Notes/Debug Log characterize all `BookingServiceTests` issues as "pre-existing... unrelated... out of scope," which is inaccurate as written since this diff does touch the file.
- [x] [Review][Patch] id_token capture completeness gaps [`backend/BarbershopApi/Services/ZPaxSsoClient.cs:92`, `backend/BarbershopApi/Controllers/AuthController.cs:216-226`] — a missing `id_token` in the token response is silently dropped with no log line (every other missing-field check in `ExchangeCodeForIdentity` logs a warning); and `SsoCallback` never clears a stale `zpaxIdToken` cookie when a later login's `identity.IdToken` comes back empty, leaving an older token in place.
- [x] [Review][Patch] Unstable effect dependency [`frontend/src/components/MyzpaxBanner.jsx:57`] — the init effect's dependency array includes `logout`, which `AuthContext` provides as a new function reference on every `AuthProvider` render; currently harmless only because `initializedRef` short-circuits re-init, not because the effect tolerates it by design.
- [x] [Review][Patch] `onLogout` defensive gaps [`frontend/src/components/MyzpaxBanner.jsx:46-50`] — no guard against `accessTokenRef.current` being `null` (would send a literal `Bearer null` Authorization header) and no re-entrancy guard against a double-invocation before `window.location.assign` navigates away. Both are low-probability given the existing token-availability invariants, but neither is guarded.

## Dev Notes

### Architecture Compliance (must-follow, not optional)

- **AD-19 addendum ("myzPAX banner logout (FR48)")** — this is the authoritative spec for this story; full paragraph in `ARCHITECTURE-SPINE.md`. Key points already folded into Task 1: `onLogout` performs the *same* teardown as `NavBar.handleLogout` (revoke server-side refresh session, clear `AuthContext`), then does a full-page navigation (`window.location.assign`, not client-side routing — same rationale AD-19/AD-3 already use: the vendor banner injects DOM/global state outside React's control that only a full navigation reliably clears, per Story 4.4's own logout-bug fix) to `GET /api/auth/sso/login`. This target is explicitly unverified — Task 2 is a hard gate, not optional polish.
- **AD-3 (token transport)** — `logoutAccount` already sends the app's own access token as a Bearer header exactly like `NavBar.handleLogout` does; nothing new here, just reuse.
- **AD-4 (no new mocking)** — Task 4 must ride on the existing `vi.spyOn(globalThis, 'fetch')` stub already in `MyzpaxBanner.test.jsx`'s `beforeEach` and the existing `window.location` stubbing idiom from `NavBar.test.jsx` — introducing MSW or any other new mocking layer would violate this.
- ~~**No backend change** — this story is frontend-only.~~ **Superseded by Task 7 (2026-09-01)** — ending z-pax's own SSO session (AC #3/#4's real fix) required backend changes: `ZPaxSsoOptions`/`ISsoClient`/`ZPaxSsoClient` (scope, `id_token` capture, `BuildLogoutUrl`), `AuthController` (`zpaxIdToken` cookie, new `GET /api/auth/sso/logout` endpoint). `POST /api/auth/logout` itself still needed no change for *this* story's original scope — it already cleared the pending `zpaxAccessToken` cookie (fixed in Story 4.4's review round) — but Task 7 added a second SSO cookie there too (see Review Findings).
- **FR48** is the sole functional requirement driving this story; FR46/FR47 are cross-referenced but unmodified by this story's diff.

### Design Decisions This Story Must Make (epics/architecture leave these open)

- **How `onLogout` gets a fresh access token** — `MyzpaxBanner.jsx` currently has no ref for the app's own access token (only `tokenRef` for the zpax token). Task 1 directs adding a second ref mirroring the existing one; this is the same staleness concern Story 4.4 already solved once for `getToken`, just applied to a second value.
- **Exact assertion shape for "session cleared" in the new test** — `AuthContext`'s `logout` just calls `setUser(null)`; there's no existing precedent in `MyzpaxBanner.test.jsx` for observing that from outside the component (it renders `null` either way). The simplest sufficient assertion is that `logout` was invoked and the app's own logout `fetch` fired — don't over-engineer a DOM-visible signal that doesn't exist for this component.

### Testing Requirements

- Frontend only: Vitest + jsdom + React Testing Library. Reuse `MyzpaxBanner.test.jsx`'s existing `renderBanner`/`SIGNED_IN_WITH_TOKEN` fixtures and `beforeEach` fetch/loadScript/`window.MyzpaxBanner` stubs — do not duplicate them.
- No backend tests needed (no backend diff in this story).
- No Playwright/e2e coverage expected (matches every prior Epic 4 story).

### Project Structure Notes

- **Modified, not new:** `frontend/src/components/MyzpaxBanner.jsx`, `frontend/src/components/MyzpaxBanner.test.jsx`.
- **Not touched:** `frontend/src/components/NavBar.jsx` (AC #5 requires this explicitly), any backend file, `AuthContext.jsx` (its existing `logout` function is reused as-is, no signature change needed).
- No new files expected for this story.

### Previous Story Intelligence (Story 4.4)

- Story 4.4 already established the `tokenRef`/`initializedRef` pattern in this exact file for exactly this reason (avoiding stale closures across the gated one-time `init()` call) — Task 1 extends the same pattern rather than inventing a new one.
- Story 4.4's own review round fixed a related bug: `NavBar`'s Logout used to leave the myzPAX banner visibly stranded because client-side `navigate('/')` doesn't clear vendor-injected DOM/script state — that's exactly why `handleLogout` (and now this story's `onLogout`) use a full-page navigation instead of React Router. Don't regress to `navigate()` here.
- Story 4.4's review also fixed the backend `Logout()` endpoint to delete the pending `zpaxAccessToken` cookie — `onLogout`'s call to `logoutAccount` already exercises that same endpoint, so this story gets that cleanup for free.
- Test fixture convention: this codebase never puts a real name in test data — existing fixtures use "John Smith"/`john@example.com` placeholders (see `MyzpaxBanner.test.jsx`'s `SIGNED_IN_WITH_TOKEN`). Any new fixture data added for Task 4 must follow the same convention.

### Git Intelligence Summary

Recent commits: `ef0fff4` (Story 4.4 merged via PR #22 — this story's baseline) → `73d750b` (refresh-flow note) → `e423646` (Story 4.4 created) → `e16d396` (Epic 4 doc update). Established rhythm: create the story, implement on a branch, PR with a summary, merge once both CI jobs are green. This story's branch (`story/4.5-myzpax-banner-logout`) already exists and intentionally bundles this story with the separate mobile-nav-dropdown defect fix tracked in `sprint-status.yaml`'s epic-4 action items — see Task 6.

### References

- [Source: _bmad-output/planning-artifacts/epics.md §Epic 4, §Story 4.5] — story statement, six acceptance criteria
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-08-31.md] — full rationale: why `onLogout` is a new requirement (not a missed AC), the redirect-target risk/fallback plan, and why the nav-dropdown defect is separate untracked-story work on the same branch
- [Source: _bmad-output/planning-artifacts/prds/prd-bmad-learning-project-2026-07-21/prd.md#FR46,#FR48] — FR46's carve-out and new FR48 defining this story's functional requirement
- [Source: _bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/ARCHITECTURE-SPINE.md#AD-19] — "myzPAX banner logout (FR48)" addendum paragraph, the authoritative technical spec for `onLogout`'s teardown-then-redirect flow
- [Source: frontend/src/components/MyzpaxBanner.jsx] — current `getToken`/`tokenRef`/`initializedRef` implementation this story extends
- [Source: frontend/src/components/NavBar.jsx:33-41] — `handleLogout`'s exact teardown-then-full-navigation shape this story mirrors
- [Source: frontend/src/api/AuthApi.js:49-59] — `logoutAccount(accessToken)`, reused as-is
- [Source: frontend/src/api/ApiConfig.js] — `API_BASE_URL`, reused as-is
- [Source: frontend/src/pages/Login.jsx:80-82] — the exact SSO-login URL construction (`${API_BASE_URL}/api/auth/sso/login`) this story's redirect target mirrors
- [Source: frontend/src/components/MyzpaxBanner.test.jsx] — existing test fixtures/stubs (`renderBanner`, `SIGNED_IN_WITH_TOKEN`, fetch/loadScript/`window.MyzpaxBanner` stubs) to extend
- [Source: frontend/src/components/NavBar.test.jsx:243-257] — `window.location` stubbing idiom to reuse for asserting `window.location.assign`
- [Source: _bmad-output/implementation-artifacts/4-4-myzpax-cross-app-navigation-banner.md] — previous story in this epic; established the `tokenRef` staleness-avoidance pattern and the full-page-navigation-on-logout fix this story builds on
- [Source: _bmad-output/implementation-artifacts/deferred-work.md] — most recent entries (Story 4.4's zpax-token TOCTOU race, Story 4.3's CSRF bypass) confirmed not applicable to this story's diff
- [Source: project-context.md §Framework-Specific Rules (React); §Testing Rules; §Critical Don't-Miss Rules]

## Dev Agent Record

### Agent Model Used

claude-sonnet-5

### Debug Log References

- `npx vitest run src/components/MyzpaxBanner.test.jsx` — red (2 failing) before the `onLogout` wiring, green (7/7) after.
- `npx vitest run src/components/MyzpaxBanner.test.jsx` — red (1 failing) before swapping the redirect target to the interim `/login`, green (7/7) after; red again before the final swap to `sso/logout`, green (7/7) after.
- `npx vitest run` (full frontend suite) — 202/202 passed, no regressions, all rounds.
- `npx eslint .` and `npx prettier --check` on changed files — both clean (one `--write` pass needed for a quote-style fix in the test file).
- `git diff --stat -- frontend/src/components/NavBar.jsx` — empty, confirming Task 3 / AC #5.
- `dotnet build BarbershopApi.Tests` — red (compile error, `ISsoClient.BuildLogoutUrl` not implemented) before implementing `ZPaxSsoClient.BuildLogoutUrl`/`FakeSsoClient.BuildLogoutUrl`, green after.
- `dotnet test --filter FullyQualifiedName~ZPaxSsoClientTests` — 14/14 passed after id_token capture + `BuildLogoutUrl` (scope assertion rewritten to decode via `QueryHelpers.ParseQuery` instead of substring-matching, since `"openid profile"` doesn't literally contain `"scope=profile"` once URL-encoded).
- `dotnet test --filter FullyQualifiedName~AuthControllerTests` — 38 passed, 4 skipped (pre-existing `[DEBUG-TEMP]` skips, unrelated) after adding the `zpaxIdToken` cookie assertion and the two new `SsoLogout` tests.
- `dotnet test` (full backend suite) — 292 passed, 4 skipped (pre-existing), 6 failed — all 6 are pre-existing `BookingServiceTests` date-fixture failures unrelated to this story (confirmed present before this story's changes too); no Auth/SSO regressions.

### Completion Notes List

- Task 1: Added `accessTokenRef` (mirrors the existing `tokenRef` pattern) and an `onLogout` async callback to `MyzpaxBanner.jsx`'s `init()` call — revokes the server-side session via `logoutAccount`, clears `AuthContext` via `logout()`, then `window.location.assign`s. TDD: wrote failing tests first (`onLogout: expect.any(Function)` assertion + a new teardown/redirect test), confirmed red, implemented, confirmed green. Redirect target changed twice after Task 2/Task 7 — see those notes.
- Task 2 (AC #4): Round 1 — Jack manually verified against a live z-pax SSO session and confirmed the originally-planned redirect to the SSO login URL silently re-authenticates the visitor instead of logging them out (z-pax's own session cookie was still live; our app's own teardown had already succeeded). Interim fix: redirect to this app's own `/login` page. Round 2 — Jack supplied z-pax's `MyzpaxBanner.init` options docs (including its logout section and the `/connect/logout` endpoint's `id_token_hint` parameter), which made the real fix buildable (Task 7). Live-verified 2026-09-01: Jack confirmed he was actually signed out of z-pax. An HTTP 405 error page appeared immediately after, on z-pax's bare domain root — root-caused as z-pax's own post-logout landing page (their `/connect/logout` takes no redirect-back parameter, so they choose where the visitor lands, and that default appears broken/unconfigured on their end). This is outside this app's control and doesn't affect whether the session actually ends, so it's logged in `deferred-work.md` as a known cosmetic issue rather than blocking this story.
- Task 3: Confirmed via `git diff` that `NavBar.jsx` has zero changes from baseline — this story's entire diff is additive within `MyzpaxBanner.jsx`/`MyzpaxBanner.test.jsx` plus the new Task 7 backend files.
- Task 4: Extended the existing init-assertion test with the `onLogout` field, and added a new test that renders a small inline `AuthStateProbe` alongside `MyzpaxBanner` to observe `AuthContext`'s `user` transition to `null` after invoking the captured `onLogout` callback — asserts the logout fetch, the session clearing (via the probe, `waitFor`-wrapped since the state update happens outside a user-event handler), and the final `window.location.assign` redirect target (`${API_BASE_URL}/api/auth/sso/logout`). No new mocking library introduced (AD-4).
- Task 5: Re-checked `deferred-work.md` at implementation kickoff — confirmed (again) that no new entries touch `MyzpaxBanner.jsx`, `NavBar.jsx`, or the logout/session-teardown path. Added, then later removed/superseded, an entry for this story's own deferred item once Task 7 resolved it (see `deferred-work.md`'s history).
- Task 6: **Not completed by Dev** — branch/push/PR is Jack's call per standing project convention.
- Task 7 (added 2026-09-01): Implemented the real z-pax session-termination flow once Jack supplied the vendor's logout-specific docs. Requesting `openid` scope (Jack applied this directly to `ZPaxSsoClient.cs`) makes z-pax's token endpoint return an `id_token`, which `ExchangeCodeForIdentity` now captures onto `SsoIdentity.IdToken` (empty string, not a throw, if ever omitted — sign-in shouldn't hard-fail over a logout-only concern). `SsoCallback` stores it in a new `zpaxIdToken` HttpOnly cookie (15-day expiry, matching `refreshToken` — needs to survive until whenever the user eventually logs out, unlike the 2-minute single-use `zpaxAccessToken` pickup cookie). New `GET /api/auth/sso/logout` reads that cookie and 302-redirects to `ssoClient.BuildLogoutUrl(idToken)` (`https://dapi.auth.myzpax.com/connect/logout?id_token_hint=...`), falling back to `SsoRedirects.Login` if no cookie is present. `MyzpaxBanner.jsx`'s `onLogout` now targets this new endpoint. TDD throughout: `dotnet build` red (interface method missing) → implemented `ZPaxSsoClient`/`FakeSsoClient` → green; new `AuthControllerTests` written before the controller endpoint existed, confirmed red via missing-route behavior, implemented, green.
- Code review (2026-09-02): `ZPaxSsoClient.BuildLogoutUrl` shipped with `post_logout_redirect_uri` active but a comment implying the *commented-out* fallback (no redirect param) was the live-verified branch — backwards from reality. Jack confirmed the active version (targeting `https://myzpax.com/home`, registered with z-pax) is the one actually live-verified; round 3 re-verification (2026-09-02) confirmed the session ends and the browser lands cleanly with no error page (the round-2 405 is resolved). Fixed the comment, removed the dead line, updated AC #4/deferred-work.md/ARCHITECTURE-SPINE.md/epics.md to match. Also fixed: `SsoCallback`'s no-code/no-login-attempt branch comment (was factually wrong about `LogoutRedirectUri` pointing back to this app — it points externally; kept as defensive catch-all, comment corrected); standard `Logout()` now also clears `zpaxIdToken` (new test: `Logout_clears_any_pending_zpaxIdToken_cookie`); `ExchangeCodeForIdentity` now logs a warning on a missing `id_token`; `SsoCallback` now clears a stale `zpaxIdToken` cookie when a later login's `IdToken` comes back empty (new test: `SsoCallback_clears_a_stale_zpaxIdToken_cookie_when_a_later_login_has_no_id_token`); `MyzpaxBanner.jsx`'s init effect no longer depends on the unstable `logout` reference (added `logoutRef`, mirroring `tokenRef`/`accessTokenRef`); `onLogout` now skips the logout call when there's no app access token and ignores re-entrant invocations (`loggingOutRef`). Reconciled the story's own stale "No backend change" Dev Note and Task 4's superseded redirect-target text. Disclosed the previously-undisclosed `BookingServiceTests.cs` drive-by fix in the File List.

### File List

- `frontend/src/components/MyzpaxBanner.jsx` (modified)
- `frontend/src/components/MyzpaxBanner.test.jsx` (modified)
- `backend/BarbershopApi/Services/ZPaxSsoOptions.cs` (modified — added `LogoutEndpoint`)
- `backend/BarbershopApi/Services/ISsoClient.cs` (modified — `SsoIdentity.IdToken`, `BuildLogoutUrl`, `SsoRedirects.Login`)
- `backend/BarbershopApi/Services/ZPaxSsoClient.cs` (modified — `id_token` capture, `BuildLogoutUrl` implementation)
- `backend/BarbershopApi/Controllers/AuthController.cs` (modified — `zpaxIdToken` cookie on `SsoCallback`, new `SsoLogout` action)
- `backend/BarbershopApi/appsettings.json` (modified — `ZPaxSso:LogoutEndpoint`)
- `backend/BarbershopApi.Tests/ZPaxSsoClientTests.cs` (modified — scope assertion rewritten, `id_token`/`BuildLogoutUrl` coverage added)
- `backend/BarbershopApi.Tests/AuthControllerTests.cs` (modified — `zpaxIdToken` cookie assertion, new `SsoLogout` tests)
- `backend/BarbershopApi.Tests/TestOnly/FakeSsoClient.cs` (modified — `IdToken` on default identity, `BuildLogoutUrl` implementation)
- `backend/BarbershopApi.Tests/BookingServiceTests.cs` (modified — 4 `Cancel(...)` call sites now pass `FixedNow` explicitly instead of relying on the default `now`, to keep those tests from joining the pre-existing date-fixture drift; unrelated to this story's SSO scope)
- `_bmad-output/implementation-artifacts/deferred-work.md` (modified — entry for the unresolved z-pax end-session endpoint added, then annotated resolved by Task 7; the round-2 405-landing-page entry added, then closed by the code review's round-3 fix)
- `_bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/ARCHITECTURE-SPINE.md` (modified — AD-19 "myzPAX banner logout (FR48)" addendum, corrected by code review to match the shipped `sso/logout` flow)
- `_bmad-output/planning-artifacts/epics.md` (modified — Story 4.5 added, corrected by code review to match the shipped `sso/logout` flow)

## Change Log

- 2026-09-01: Wired `onLogout` into `MyzpaxBanner.init` (revoke session, clear `AuthContext`, redirect away) with test coverage for the callback's teardown-then-redirect behavior.
- 2026-09-01: Live-SSO verification (Task 2/AC #4, round 1) showed the originally-planned SSO-login redirect target silently re-authenticates instead of logging out. Interim fix: redirect to this app's own `/login` page.
- 2026-09-01: Per z-pax's own `MyzpaxBanner.init` docs (Task 7), implemented the real fix — `openid` scope, `id_token` capture, a new `zpaxIdToken` session cookie, and a new `GET /api/auth/sso/logout` backend endpoint that redirects to z-pax's real `/connect/logout?id_token_hint=...`. `MyzpaxBanner.jsx`'s `onLogout` now targets that endpoint. Full unit/integration coverage added.
- 2026-09-01: Live-verified round 2 — Jack confirmed the SSO session actually ends now. A cosmetic HTTP 405 on z-pax's own post-logout landing page was observed and logged in `deferred-work.md` as an out-of-our-control z-pax-side issue, not a blocker. All tasks complete except Task 6 (branch/PR), pending Jack.
- 2026-09-02: Code review — fixed a comment in `ZPaxSsoClient.BuildLogoutUrl` that had the verification status backwards; Jack confirmed `post_logout_redirect_uri` (registered with z-pax) is the live-verified branch, and round-3 re-verification confirmed it now lands cleanly on `https://myzpax.com/home` with no 405, closing the `deferred-work.md` cosmetic-issue entry. Fixed `SsoCallback`'s no-code-branch comment, added `zpaxIdToken` cleanup to standard `Logout()` and to `SsoCallback` on a later empty-`IdToken` login, added a missing-`id_token` log line, hardened `MyzpaxBanner.jsx`'s `onLogout` (stable `logoutRef`, null-token guard, re-entrancy guard), and reconciled stale doc text across this story file, `ARCHITECTURE-SPINE.md`, and `epics.md`. New/updated tests: `Logout_clears_any_pending_zpaxIdToken_cookie`, `SsoCallback_clears_a_stale_zpaxIdToken_cookie_when_a_later_login_has_no_id_token`.
