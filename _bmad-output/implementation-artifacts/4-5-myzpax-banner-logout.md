---
baseline_commit: ef0fff4
---

# Story 4.5: myzPAX Banner Logout

Status: ready-for-dev

## Story

As an SSO-authenticated user,
I want the myzPAX banner's logout control to end both my z-pax session and my barbershop session together,
so that logging out from the banner actually signs me out of this app too, not just the SSO layer.

## Acceptance Criteria

1. **Given** a signed-in session holding a z-pax access token in memory (Story 4.4), **when** `MyzpaxBanner.init` is called, **then** it is passed an `onLogout` callback in addition to the existing `getToken`/`currentAppId`/`position` (FR48).
2. **Given** the visitor triggers the banner's logout control, **when** `onLogout` fires, **then** it tears down this app's session exactly as the existing NavBar Logout does — calling the app's logout endpoint to revoke the server-side refresh session, then clearing `AuthContext` — before navigating away (FR48, mirrors `NavBar.handleLogout`).
3. **Given** the session has been torn down, **when** `onLogout` completes its app-side teardown, **then** the browser is redirected via `window.location.assign` to the same URL the "Sign in with z-pax" button uses (`${API_BASE_URL}/api/auth/sso/login`) (FR48).
4. **Given** that redirect target is unverified against z-pax's actual session-termination behavior, **when** this story is implemented, **then** it's manually verified end-to-end with a live z-pax SSO session before the story is marked done — if the visitor lands back in the app still signed in, the fallback is to find z-pax's real end-session endpoint and swap the config value; flagged going in as unverified, same as Story 4.4's `currentAppId` (FR48).
5. **Given** the existing in-app "Logout" menu item (`NavBar.jsx`), **when** this story is implemented, **then** it is left completely unchanged and remains available to every account — SSO or password — as a fallback (FR48, no regression).
6. **Given** the new `onLogout` wiring, **when** tested, **then** it's covered in `MyzpaxBanner.test.jsx` — asserting the callback clears the session and navigates to the SSO login URL — without introducing any new mocking beyond what the existing suite already stubs (AD-4).

## Tasks / Subtasks

- [ ] **Task 1: Wire an `onLogout` callback into `MyzpaxBanner.init`** (AC: #1, #2, #3)
  - [ ] `frontend/src/components/MyzpaxBanner.jsx`: import `API_BASE_URL` from `../api/ApiConfig` and `logoutAccount` from `../api/AuthApi` (both already exist and are used exactly this way by `NavBar.jsx` — reuse verbatim, don't reinvent).
  - [ ] Destructure `logout` alongside the existing `user` from `useAuth()`.
  - [ ] The component only reads `user.accessToken` at effect-setup time today (only `user?.zpaxAccessToken` is tracked via `tokenRef`). `onLogout` needs the *current* app access token when it eventually fires, which can be long after the one-time `init()` call (`initializedRef` gates `init` to fire only once, mirrored by AC #1's "already Story 4.4 wiring, add one field" framing) — add a second ref (e.g. `accessTokenRef`) kept fresh via its own `useEffect([user?.accessToken])`, exactly mirroring the existing `tokenRef`/`token` pattern two lines above it. Do not read `user.accessToken` directly inside a closure captured at first mount — same staleness trap `tokenRef` already exists to avoid for the zpax token.
  - [ ] Define `onLogout` as an async function (inline in the `init()` call or just above it) that: (a) calls `await logoutAccount(accessTokenRef.current)`, (b) calls `logout()` from context, (c) calls `window.location.assign(`${API_BASE_URL}/api/auth/sso/login`)`. This is the same three-step shape as `NavBar.jsx`'s `handleLogout` (`await logoutAccount(user.accessToken); logout(); window.location.href = '/'`), swapping `.href = '/'` for `.assign(<sso-login-url>)` per AC #3 — do not use `.href =` here, the AC and the architecture addendum both specify `window.location.assign` explicitly.
  - [ ] Pass `onLogout` into the existing `window.MyzpaxBanner.init({ getToken, currentAppId, position })` call as a 4th field — do not change `getToken`/`currentAppId`/`position`.

- [ ] **Task 2: Manual live-SSO verification gate** (AC: #4)
  - [ ] Not a code task — flag explicitly for Jack. Before this story is marked done, sign in via a live z-pax SSO session, trigger the banner's logout control, and confirm the visitor lands on z-pax's login screen rather than being silently re-authenticated and bounced back into this app still signed in.
  - [ ] If the visitor lands back in the app still signed in: this app's own session teardown (step (a)/(b) above) still succeeded — the failure is specifically that `GET /api/auth/sso/login` isn't ending the z-pax-side session. Find z-pax's real end-session/logout endpoint and swap the literal redirect target in `MyzpaxBanner.jsx` — no architecture rework, no other file changes needed.

- [ ] **Task 3: Confirm `NavBar.jsx`'s own Logout is untouched** (AC: #5)
  - [ ] No code change expected here. After Task 1, diff `NavBar.jsx` to confirm it's identical to its pre-story state — this story's entire scope is additive within `MyzpaxBanner.jsx`.

- [ ] **Task 4: Test coverage for the `onLogout` wiring** (AC: #6)
  - [ ] `frontend/src/components/MyzpaxBanner.test.jsx`: extend the existing `'loads the banner script and initializes it once a z-pax token is present'` test (or add a new one) to assert `window.MyzpaxBanner.init` is called with an `onLogout: expect.any(Function)` field alongside the existing three.
  - [ ] Add a new test that invokes the captured `onLogout` callback (pulled off `window.MyzpaxBanner.init.mock.calls[0][0]`, same pattern the existing `getToken` test already uses) and asserts: (a) `fetch` was called for `POST {API_BASE_URL}/api/auth/logout` (the existing `beforeEach` already `vi.spyOn(globalThis, 'fetch')`s — no new mock needed, just assert on an additional call), (b) the session clears — same observable signal `AuthContext.test.jsx`'s `AuthProbe` pattern uses, or simplest: re-render/query the DOM for whatever indicates `user` became `null` if the test harness exposes it, otherwise assert indirectly via the `logout` behavior already proven in `AuthContext.test.jsx` and only assert the callable surface here, (c) `window.location.assign` was called with `` `${API_BASE_URL}/api/auth/sso/login` ``.
  - [ ] For asserting `window.location.assign`, follow `NavBar.test.jsx`'s existing `window.location` stubbing pattern exactly: `const originalLocation = window.location; delete window.location; window.location = { ...originalLocation, assign: vi.fn() }` before the action, restore `window.location = originalLocation` in a `finally` block afterward — don't invent a different stubbing approach.
  - [ ] No new mocking library or pattern beyond what `MyzpaxBanner.test.jsx` and `NavBar.test.jsx` already establish (AD-4).

- [ ] **Task 5: Re-check `deferred-work.md`**
  - [ ] Already checked during story creation: the most recent entries (Story 4.4's zpax-token TOCTOU race, Story 4.3's CSRF/state-validation bypass) don't touch `MyzpaxBanner.jsx`, `NavBar.jsx`, or the logout/session-teardown path this story adds to. Re-confirm at implementation kickoff in case anything new landed since story creation, and note the outcome in Completion Notes.

- [ ] **Task 6: Verify CI green and branch/PR**
  - [ ] Branch is `story/4.5-myzpax-banner-logout` (renamed from an earlier working name once this work was recognized as a story). Per the sprint-change-proposal, this branch's scope intentionally also covers the standalone mobile-nav-dropdown defect fix alongside this story.
  - [ ] Push and confirm both CI jobs (Backend .NET, Frontend Vite/React) green before merging (AD-11).

## Dev Notes

### Architecture Compliance (must-follow, not optional)

- **AD-19 addendum ("myzPAX banner logout (FR48)")** — this is the authoritative spec for this story; full paragraph in `ARCHITECTURE-SPINE.md`. Key points already folded into Task 1: `onLogout` performs the *same* teardown as `NavBar.handleLogout` (revoke server-side refresh session, clear `AuthContext`), then does a full-page navigation (`window.location.assign`, not client-side routing — same rationale AD-19/AD-3 already use: the vendor banner injects DOM/global state outside React's control that only a full navigation reliably clears, per Story 4.4's own logout-bug fix) to `GET /api/auth/sso/login`. This target is explicitly unverified — Task 2 is a hard gate, not optional polish.
- **AD-3 (token transport)** — `logoutAccount` already sends the app's own access token as a Bearer header exactly like `NavBar.handleLogout` does; nothing new here, just reuse.
- **AD-4 (no new mocking)** — Task 4 must ride on the existing `vi.spyOn(globalThis, 'fetch')` stub already in `MyzpaxBanner.test.jsx`'s `beforeEach` and the existing `window.location` stubbing idiom from `NavBar.test.jsx` — introducing MSW or any other new mocking layer would violate this.
- **No backend change** — this story is frontend-only. `POST /api/auth/logout` already exists and already clears the pending `zpaxAccessToken` cookie (fixed in Story 4.4's review round) — nothing to touch there.
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

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
