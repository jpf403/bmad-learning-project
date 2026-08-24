---
baseline_commit: 4ba2e3d
---

# Story 4.3: "Sign in with z-pax" Login Page UI

Status: ready-for-dev

## Story

As a visitor,
I want a clearly visible SSO option on the Login page,
so that I can choose either sign-in method.

## Acceptance Criteria

1. **Given** the Login page, **when** rendered, **then** it shows the existing email/password fields plus a "Sign in with z-pax" button, visually separated (e.g., a divider), following the existing Button component styling (UX-DR2).
2. **Given** a user clicks "Sign in with z-pax", **when** the flow starts, **then** the browser navigates away to z-pax (full redirect, not a popup).
3. **Given** the OAuth flow completes with an error (see Story 4.2), **when** redirected back to Login, **then** the error message renders using the existing login-error display pattern.
4. **Given** any viewport width, **when** the Login page renders with the new SSO option, **then** the layout remains responsive with no broken/overflowing elements (FR22).

## Tasks / Subtasks

- [ ] **Task 1: Add "Sign in with z-pax" button + divider to the Login page** (AC: #1, #2, #4)
  - [ ] `frontend/src/pages/Login.jsx`: below the existing `<form className="login__form">` (inside `FormSection`, after the closing `</form>` tag but still inside `<FormSection>`), add a divider element and the new button — e.g. `<div className="login__divider"><span>or</span></div>` followed by `<Button variant="secondary" type="button" onClick={handleSsoLogin}>Sign in with z-pax</Button>`. The divider's exact visual treatment (a labeled line, per the AC's "e.g., a divider" phrasing) is this story's own design call — no existing `{components.divider}` token exists in DESIGN.md, and neither EXPERIENCE.md nor the sprint-change-proposal specifies one; keep it CSS-only in `Login.css`, no new shared component.
  - [ ] Use `variant="secondary"` on the new `Button` (not `primary`) — matches DESIGN.md's existing precedent of the nav's "Sign In" being secondary/neutral as "a lower-emphasis companion" to a more prominent action; the primary "Sign In" submit button in the form above stays the visually dominant choice, and z-pax is the alternate path.
  - [ ] `handleSsoLogin` performs a **real browser navigation**, not a React Router `navigate()` call and not a `fetch`: `window.location.href = \`${API_BASE_URL}/api/auth/sso/login\`` (import `API_BASE_URL` from `../api/ApiConfig`, same import already used in `AuthApi.js`). This is required by AC #2 ("full redirect, not a popup") and by how `GET /api/auth/sso/login` actually works (Story 4.2): it responds with an HTTP 302 to z-pax's authorize endpoint, which only a real top-level navigation follows correctly — client-side routing or `fetch` would never leave the SPA and would hit a CORS/opaque-redirect wall instead.
  - [ ] `frontend/src/pages/Login.css`: add `.login__divider` styling reusing existing design tokens only (`--color-border`, `--color-text-muted`, `--typography-body-sm-size`/`--font-family-base`, `--spacing-*`) — no new colors/sizes. `.login` is already `flex-direction: column` with a `--spacing-6` gap and `max-width: 480px`, and the existing `@media (min-width: 640px)` breakpoint already covers this page's only responsive rule (UX-DR19's mobile-first single-column stacking applies with no change needed) — only add CSS for the divider/button pair themselves, don't restructure the page's existing responsive rules.

- [ ] **Task 2: Render the SSO failure error using the existing login-error pattern** (AC: #3)
  - [ ] `frontend/src/pages/Login.jsx`: read the `error` query param from the URL via `useSearchParams()` from `react-router` (package `react-router` 8.3.0, already installed — **first use of this hook in the codebase**; every other query-param-shaped need so far has gone through `location.state`, not `location.search`). On mount, if `searchParams.get('error') === 'sso_failed'`, set `formError` to a fixed message string.
  - [ ] Render this exactly the same way the existing 401/429/400/network-failure branches already do: `{formError && <p className="login__form-error">{formError}</p>}` — this line already exists at Login.jsx:88, do not add a second error element or a new CSS class. This *is* "the existing login-error display pattern" the AC refers to; nothing new to style.
  - [ ] Error copy is this story's own decision (checked: EXPERIENCE.md's State Patterns table has no pre-existing SSO-error entry, and the sprint-change-proposal explicitly defers this to Story 4.3 — "reuses the existing login-error pattern... Story 4.3 specifies" the state itself). Use **"Sign-in with z-pax failed. Please try again."** — generic, no failure-reason detail, consistent with this app's established no-enumeration convention (FR2) and with the backend's own `error=sso_failed` param carrying no further detail by design (Story 4.2 Task 4 Dev Notes).
  - [ ] After reading it once, strip the `error` param from the URL so a page refresh doesn't keep re-showing it — mirror the existing one-time-consume pattern already used for `location.state?.message` at Login.jsx:22-27 (`navigate(location.pathname, { replace: true, state: {} })`); do the equivalent for the search param (e.g. `setSearchParams({}, { replace: true })` or `navigate(location.pathname, { replace: true })`).
  - [ ] Do not touch the unrelated `successMessage` banner logic — its existing guard (`successMessage && !formError && !isSubmitting`) already correctly suppresses the banner whenever `formError` is set for any reason, including this new SSO-failure case.

- [ ] **Task 3: Tests** (AC: #1–#4)
  - [ ] All new tests live in the existing `frontend/src/pages/Login.test.jsx` — no new test file, matching this codebase's one-file-per-component convention.
  - [ ] `renders a "Sign in with z-pax" button and a divider separating it from the form` — assert the button (`getByRole('button', { name: 'Sign in with z-pax' })`) and the divider element both render.
  - [ ] `clicking "Sign in with z-pax" navigates the browser to the SSO login endpoint` — this is a **new test seam for this codebase**: every existing test asserts on client-side `navigate()` outcomes (via rendered stub routes), but this action sets `window.location.href` directly, which jsdom does not let you spy on via `vi.spyOn(window.location, 'href', 'set')` in older jsdom versions but **does** support in this project's jsdom 30.0.0 — use `vi.spyOn(window.location, 'href', 'set')` (or, if that throws under jsdom's read-only `location` in this version, fall back to `Object.defineProperty(window, 'location', { value: { ...window.location, href: '' }, writable: true })` before the test and restore after); assert the captured value equals `${API_BASE_URL}/api/auth/sso/login` after the click. Import `API_BASE_URL` from `../api/ApiConfig` in the test to avoid hardcoding the origin twice.
  - [ ] `renders the SSO failure message when the URL has ?error=sso_failed` — render via `renderLogin({ initialEntries: [{ pathname: '/login', search: '?error=sso_failed' }] })` (extend the existing `renderLogin` helper to accept a full location object, not just a path string, since it currently only takes `initialEntries = ['/login']`); assert `screen.getByText('Sign-in with z-pax failed. Please try again.')`.
  - [ ] `does not render an SSO error when no error query param is present` — regression guard using the default `renderLogin()` call; assert the error paragraph is absent.
  - [ ] No backend changes in this story (frontend-only) — do not run or modify any `.NET`/`dotnet test` suite; confirm the frontend suite (`npm test` in `frontend/`) is green with no regressions to the existing Login tests before marking this story done.

- [ ] **Task 4: Check `deferred-work.md`**
  - [ ] Re-read `_bmad-output/implementation-artifacts/deferred-work.md` in full at kickoff. One entry touches this exact file — "Login success banner is captured once via a `useState` initializer on mount... if `Login` is ever reached a second time via client-side navigation with a new `location.state.message` while already mounted, the new message would never render" (deferred from Story 1.5 round 2) — this story does not change that code path or make it newly reachable (the new `error` query-param logic is a separate `useSearchParams`/`useEffect` concern from the existing `useState` initializer for `successMessage`); **note as "checked, still not applicable, remains deferred"** rather than fixing speculatively.
  - [ ] No other open item touches `Login.jsx`/the Auth frontend surface.

- [ ] **Task 5: Verify CI green and branch/PR**
  - [ ] Branch as `story/4.3-sign-in-with-zpax-login-page-ui` from `main`.
  - [ ] Push and confirm both CI jobs (Backend .NET, Frontend Vite/React) green before merging (AD-11). This story makes no backend changes — backend job should pass through unaffected. **Left for Jack** — per standing project practice, push/PR/CI verification steps are his to run and approve individually, not performed by the dev agent.

## Dev Notes

### Architecture Compliance (must-follow, not optional)

- **AD-19 (z-pax SSO)** — this story is explicitly scoped to the frontend-visible surface only: the "Sign in with z-pax" button and the Login-page error rendering. Story 4.2 already built and shipped the two backend endpoints (`GET /api/auth/sso/login`, `GET /api/auth/sso/callback`) this story's button and error-reading logic consume; **do not** touch `AuthController.cs`, `AuthService.cs`, or any backend SSO file.
- **AD-3 (token transport)** — unaffected by this story. On a successful SSO flow, the backend redirects straight to `/schedule-appointment` or `/my-schedule` (never back through `/login`), so Login.jsx never sees a "success" case for SSO — only ever the failure case via `?error=sso_failed`. `AuthContext.jsx`'s existing `bootstrap()` effect (unchanged, out of scope) hydrates the session on that landing page exactly as it already does for any fresh page load.
- **AD-18 (client-side routing)** — unaffected; no route guard changes, no new routes. The SSO button performs a hard browser navigation away from the SPA entirely (AC #2), not a React Router transition.
- **FR22 / UX-DR19 (responsiveness)** — Login.css already implements the mobile-first single-column layout with one `@media (min-width: 640px)` breakpoint; the new divider/button must fit inside that existing pattern, not introduce a second responsive scheme.
- **UX-DR2 (Button component)** — the new button must be the existing shared `Button` from `frontend/src/components/Button.jsx`, not a bespoke `<button>` or a new component. Use `variant="secondary"`.

### Design Decisions This Story Must Make (epics/UX leave these open)

- **Divider visual treatment.** UX-DR20 in the epics/architecture only flags two *other* open UX items (a password-mismatch error color, and the tablet breakpoint's exact pixel value) — the SSO divider is a *third*, separate open decision that belongs to this story alone. No `{components.divider}` token exists anywhere in `DESIGN.md`. Build it CSS-only, reusing existing design tokens (border color, muted text, spacing scale) — do not invent new colors/typography sizes for it.
- **Button variant: secondary, not primary.** Not explicitly specified by the AC; chosen to match `DESIGN.md`'s only existing precedent for a lower-emphasis auth-adjacent action (the nav's secondary "Sign In" button next to a primary "Register").
- **SSO-error copy.** Neither `EXPERIENCE.md`'s State Patterns table nor the sprint-change-proposal defines this text — both explicitly leave it for this story ("Story 4.3 specifies" the state). Use **"Sign-in with z-pax failed. Please try again."** — deliberately generic, matching FR2's no-detail-leak convention and the backend's own single generic `error=sso_failed` param (no more specific reason is ever sent by Story 4.2's implementation, so no more specific message can be shown here without inventing detail the backend doesn't provide).

### Testing Requirements

- Vitest + jsdom + React Testing Library + `user-event` — same as every existing frontend test, no new libraries.
- No `fetch` involved in this story's new code paths (the button sets `window.location.href` directly; the error-reading logic only reads `useSearchParams()`) — nothing to stub via `vi.spyOn(fetch)` for the *new* tests, unlike most of this file's existing tests.
- Spying on `window.location.href` assignment is a new test pattern for this codebase (jsdom 30.0.0 present here supports `vi.spyOn(window.location, 'href', 'set')`; verify this works in this project's actual jsdom config before falling back to `Object.defineProperty` reassignment).
- `renderLogin`'s helper currently only accepts `initialEntries = ['/login']` (a bare path string) — extend it to accept a full location object (`{ pathname, search }`) so the `?error=sso_failed` test can set a query string; keep backward compatibility with existing callers that pass nothing.

### Project Structure Notes

- **Modified, not new:** `frontend/src/pages/Login.jsx`, `frontend/src/pages/Login.css`, `frontend/src/pages/Login.test.jsx`.
- **No new files.** No backend changes, no new components — reuses `Button` (`frontend/src/components/Button.jsx`), `FormSection` (`frontend/src/components/FormSection.jsx`), and `API_BASE_URL` (`frontend/src/api/ApiConfig.js`) exactly as they exist today.
- This is the final story of Epic 4 — after this story, no further Auth/SSO work is scheduled.

### Previous Story Intelligence (Story 4.2)

- The failure redirect this story's error-reading logic depends on is an **exact, locked contract**: `https://localhost:5173/login?error=sso_failed` — Story 4.2's own Dev Notes flag "Story 4.3 depends on this exact param name — do not invent a different one without updating this note." Confirmed still current as of Story 4.2's completion (backed by a shared `SsoRedirects.Failure` constant referenced from both `AuthController.cs` and `ZPaxSsoClient.cs`, added during Story 4.2's code review to prevent literal-duplication drift).
- The success path **never routes through `/login` at all** — it redirects straight to `https://localhost:5173/schedule-appointment` (Customer) or `https://localhost:5173/my-schedule` (Barber/Admin), per `frontend/src/landingRoutes.js`'s existing `LANDING_ROUTE` mapping. This story's Login.jsx changes have no "SSO success" case to handle.
- Story 4.2 introduced no frontend code at all (explicitly out of scope for it) — this story is the first and only frontend-touching piece of Epic 4's SSO work.
- Established test-fixture convention across this codebase: test fixture names use "John Smith"-style placeholders, never a real person's name (already followed by the existing `renderLogin`/`fillForm` helpers in `Login.test.jsx`, which this story's new tests should match).

### Git Intelligence Summary

Recent commits: `4ba2e3d` (Story 4.2 merged via PR #20, current `main` tip and this story's baseline) → `976ca13` (Story 4.2 created) → `b097454` (Story 4.1 merged via PR #19). Established rhythm across every prior story: create the story on `main`, implement on `story/{epic}.{story}-{slug}`, PR with a summary, merge once both CI jobs are green, delete the branch — Task 5 follows this unchanged.

### References

- [Source: _bmad-output/planning-artifacts/epics.md §Epic 4, §Story 4.3] — story statement, four acceptance criteria
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-08-19.md §Section 4, Story 4.3] — original AC text and the explicit note that this story owns the SSO-error copy/state ("reuses the existing login-error pattern — no new visual state needed beyond what Story 4.3 specifies")
- [Source: _bmad-output/implementation-artifacts/4-2-zpax-oauth-login-flow.md] — exact failure-redirect contract (`error=sso_failed`, no further detail) and success-redirect routes this story's UI must consume; confirms no frontend code exists yet for SSO
- [Source: _bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/ARCHITECTURE-SPINE.md#AD-19, #AD-3, #AD-18] — SSO flow ownership boundaries, token-transport mechanism, client-side routing conventions
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-bmad-learning-project-2026-07-23/DESIGN.md] — Button component variants/usage precedent (secondary "Sign In" as a lower-emphasis companion action), confirms no existing divider token
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-bmad-learning-project-2026-07-23/EXPERIENCE.md §State Patterns] — confirms no pre-existing SSO-error state pattern; existing "Login error"/"Login rate-limited" entries as the pattern to match generically
- [Source: frontend/src/pages/Login.jsx, Login.css, Login.test.jsx] — exact current implementation this story extends (existing `formError`/`login__form-error` pattern, `renderLogin`/`fillForm` test helpers, existing responsive breakpoint)
- [Source: frontend/src/components/Button.jsx, Button.css] — existing variant classes (`primary`/`secondary`/`destructive`) this story's new button must reuse
- [Source: frontend/src/api/ApiConfig.js, AuthApi.js] — `API_BASE_URL` constant and its existing import convention
- [Source: frontend/package.json] — confirms `react-router` (not `react-router-dom`) 8.3.0 is the installed package providing `useSearchParams`
- [Source: _bmad-output/implementation-artifacts/deferred-work.md] — one open item touching `Login.jsx` (Story 1.5 round 2's success-banner-on-remount gap) — checked, not applicable/reachable by this story's changes (Task 4)
- [Source: project-context.md §Framework-Specific Rules (React); §Testing Rules; §Critical Don't-Miss Rules]

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
