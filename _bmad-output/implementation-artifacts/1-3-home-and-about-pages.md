---
baseline_commit: 35ed372cb7f0b041e9928565b431b627a30d1f42
---

# Story 1.3: Home and About Pages

Status: review

## Story

As a visitor,
I want to see the Home page (hero, tagline, CTA) and a static About page,
so that I can learn about the shop before signing up.

## Acceptance Criteria

1. **Given** a signed-out visitor on Home, **when** the page loads, **then** the hero renders with the diagonal white/primary-teal split, headline, tagline, and a "Schedule Appointment" CTA, with at least one hover interaction on desktop (FR20, UX-DR16).
2. **Given** a signed-out visitor, **when** they click the Home CTA, **then** they are redirected to Login (route `/login` — the Login page itself is built in Story 1.5; this story only wires the routing decision).
3. **Given** a signed-in visitor, **when** they click the Home CTA, **then** they are routed toward the booking flow (route `/schedule-appointment` — the destination page itself is built in Epic 2; this story only wires the routing decision).
4. **Given** the About page, **when** visited, **then** it renders static shop content (location, phone, hours, barber list) (FR21).
5. **Given** any viewport width, **when** Home or About renders, **then** the layout adapts cleanly with no broken/overflowing elements on mobile or desktop (FR22).

## Tasks / Subtasks

- [x] **Task 1: Install and wire React Router v8** (AC: #1-#5, foundational)
  - [x] `npm install react-router@8.3.0` in `frontend/` (exact-pinned, matching this project's convention of exact-pinning every dependency — see Latest Tech Info in Dev Notes for why v8, not v7 or `react-router-dom`).
  - [x] In `frontend/src/main.jsx`, import `BrowserRouter` from `"react-router"` (not `"react-router-dom"` — that package no longer exists in v8; not `"react-router/dom"` either — that subpath is only for `RouterProvider`/`HydratedRouter`, the data/framework-mode APIs this declarative SPA doesn't use) and wrap `<App />` with it.
  - [x] **Do not** put `<BrowserRouter>` inside `App.jsx` itself — keep it in `main.jsx` only, so `App.jsx` stays testable by wrapping it in `<MemoryRouter>` in tests without a nested-router conflict.
  - [x] In `App.jsx`, remove the Story 1.1 design-system showcase (buttons/inputs/confirm-popup demo) — it was always provisional (a manual rendering smoke-check), and every component it demonstrated already has its own dedicated test file (`Button.test.jsx`, `Input.test.jsx`, etc.), so nothing is lost by deleting it. Replace the body with `<NavBar />`, a `<Routes>` block (`"/"` → `Home`, `"/about"` → `About`), and `<Footer />`.
  - [x] **Route-naming convention this story establishes — future stories must use these exact paths, not invent their own:** `/login` (Story 1.5), `/register` (Story 1.4), `/schedule-appointment` (Story 2.2), `/my-schedule` (Stories 2.5/2.6), `/admin` (Story 3.2), `/account` (Story 1.7). Do not create placeholder `<Route>` entries or stub page components for any of these now — only `/` and `/about` get real routes in this story.

- [x] **Task 2: Build the Home page** (AC: #1, #2, #3, #5)
  - [x] Create `frontend/src/pages/Home.jsx` + `Home.css`.
  - [x] Diagonal hero split — white half (left) / `{colors.primary}`-filled half (right), per `DESIGN.md`'s Home hero component spec. Use `clip-path` (or an equivalent skewed-divider technique) on the teal half; this is CSS-only, no new component needed.
  - [x] White half: headline (`{typography.display}` — 40px/700) + tagline (`{typography.body}`). Exact copy is a content-pass decision, not fixed upstream (`DESIGN.md`: "exact headline copy is a content-pass decision owned by EXPERIENCE.md/copywriting, not fixed here") — propose sensible on-brand copy in the locked voice register (clean, plain-spoken, no exclamation points — see `EXPERIENCE.md` §Voice and Tone), e.g. headline "Your next haircut, booked in under a minute." / tagline "Walk-in convenience, without the wait."
  - [x] CTA button: reuse the existing `Button` component (`variant="primary"`), label **"Schedule Appointment"** — this exact label is a locked value, not a proposal (`DESIGN.md` §Components: "Used for 'Schedule Appointment' (nav CTA **and Home hero CTA**)"). `Button`'s existing `:hover` CSS (from Story 1.1) already satisfies the "at least one hover interaction on desktop" requirement (FR20) — do not add any new hover logic.
  - [x] Teal half: one small decorative inline SVG (scissors-and-comb, crossed like an X — "the only illustrative graphic element in the entire product," per `DESIGN.md` §Components). Mark it `aria-hidden="true"` — it's decorative, not content. Visual fidelity of the SVG itself is not AC-gated; keep it simple.
  - [x] Accept an `isSignedIn` prop (default `false`). This is a **temporary seam, not a real auth system** — see "Auth-State Placeholder" below. CTA `onClick` calls `useNavigate()` (from `"react-router"`) with `navigate(isSignedIn ? '/schedule-appointment' : '/login')`.
  - [x] Responsive: hero and copy must not overflow/break at any width; verify at ~375px, ~768px, ~1280px against the existing 640px/1024px breakpoints (`frontend/src/styles/breakpoints.js`) — reuse those constants/media-query values, don't hardcode new breakpoint numbers.

- [x] **Task 3: Build the About page** (AC: #4, #5)
  - [x] Create `frontend/src/pages/About.jsx` + `About.css`.
  - [x] Location/phone/hours: reuse the **exact same copy already in `Footer.jsx`** — "123 Main Street, Springfield" / "(555) 010-2020" / "Mon–Fri, 9:00 AM – 4:30 PM". `DESIGN.md` §Components is explicit these must match ("address and phone (same fake contact info as the About page)") — do not invent different values.
  - [x] Barber list: static placeholder names only (e.g., "Manny, Dana, and Theo" — "Manny" matches the barber name already used throughout `EXPERIENCE.md`'s example flows, for continuity). **This is fake shop copy, not a data-driven feature** — no repository/endpoint exists to list real barber accounts, and no FR/story anywhere scopes building one; don't add a fetch call or wire this to the `AccountRepository` from Story 1.2.
  - [x] Responsive layout, same no-overflow requirement as Home (FR22).

- [x] **Task 4: Wire NavBar to real routes + active-link state**
  - [x] In `NavBar.jsx`, replace the `Home` and `About` `<a href="#">` placeholders with `<Link to="/">`/`<Link to="/about">` (from `"react-router"`).
  - [x] Leave `Schedule Appointment`, `My Schedule`, and `Admin Panel` as plain, non-interactive text (e.g. a `<span>`), **not** `<a href="#">` and **not** wired to a route — their destination pages don't exist until Epic 2/3, and linking to an unregistered route would just render blank. This is a deliberate, temporary regression from Story 1.1's "all five render as links" shell; Stories 1.4/1.5(nav auth-state)/1.6(role-gated visibility) are what eventually make these real again.
  - [x] Add active-link styling: use `useLocation()` (from `"react-router"`) to compare the current pathname against each real link's target; apply a new `.nav-bar__link--active` class using the already-defined `--color-primary` token (`nav-bar.link-foreground-active` / `link-underline-active` in `DESIGN.md`) — the CSS variable already exists in `tokens.css`, only the new class + underline rule need adding.

- [x] **Task 5: Update existing tests for the routing change** (regression fix — do not skip)
  - [x] `NavBar.test.jsx`: wrap `render(<NavBar />)` in `<MemoryRouter>` (required now that `Link`/`useLocation` need a router context — rendering `<NavBar />` bare will throw). Update the "renders all five nav links" test: assert `Home`/`About` via `getByRole('link', { name })`, and assert `Schedule Appointment`/`My Schedule`/`Admin Panel` are present as text but **not** links via `queryByRole('link', { name })` returning `null`. Add a test asserting the active-link class lands on the correct link for a given `initialEntries` route.

- [x] **Task 6: Frontend tests for the new pages** (AC: all)
  - [x] `Home.test.jsx`: hero renders headline/tagline/CTA text. CTA click while signed-out navigates to `/login`; CTA click with `isSignedIn` navigates to `/schedule-appointment`. Test navigation by wrapping in `<MemoryRouter initialEntries={['/']}>` with real `<Routes>` including stub destination routes (e.g. `<Route path="/login" element={<div>Login Stub</div>} />`) and asserting the stub renders after the click — **do not** mock `useNavigate` directly; RTL's documented pattern for testing React Router navigation is real routes + real navigation, not a mocked hook.
  - [x] `About.test.jsx`: renders address/phone/hours/barber-list text.
  - [x] `App.test.jsx` (new): `<MemoryRouter initialEntries={['/']}>` around `<App />` renders Home content + `NavBar` + `Footer`; `initialEntries={['/about']}` renders About content.

- [x] **Task 7: Verify CI green**
  - [x] Branch as `story/1.3-home-and-about-pages` from `main` (continuing the convention resumed in Story 1.2).
  - [x] Run `npm run lint`, `npm run format:check`, `npm test` locally; push and confirm both CI jobs pass before merging (AD-11). No backend changes in this story — the backend CI job is unaffected but must still stay green.

## Dev Notes

### Architecture Compliance (must-follow, not optional)

- **AD-18 (client-side routing)** — this is the story that finally adds React Router (Story 1.1 explicitly deferred it: "Do NOT install or wire React Router yet"). Route guards that call `GET /api/auth/me` and redirect on unauthorized/wrong-role access are **not** this story's job — that's Story 1.6. This story only needs `BrowserRouter`/`Routes`/`Route`/`Link`/`useNavigate`/`useLocation` for two static pages plus one conditional CTA branch.
- **AD-1 (layering)** — frontend-only story, no backend touch. Don't create `frontend/src/api/` fetch calls for the About page's barber list (see Task 3) — that would be inventing a feature no FR requires.
- **NFR6 discipline (don't build ahead)** — Story 1.1's Dev Notes explicitly held NavBar's real routing/auth-state/role-hiding for "Stories 1.3–1.6." This story is 1.3's slice only: real links for pages that exist (Home, About), inert placeholders for pages that don't. Resist wiring Login/Register/Schedule Appointment routes or stub pages now — that's each later story's own scope.

### Auth-State Placeholder (temporary seam — read before implementing AC #2/#3)

No login/session system exists yet (Stories 1.4/1.5/1.6 build it). AC #2 and #3 require the Home CTA to branch on sign-in state *today*, so `Home` takes an `isSignedIn` prop (default `false`) rather than reading real auth state. This is intentionally the smallest possible seam — a prop, not a Context/hook/global store — because inventing a bigger auth abstraction now would itself be scope creep this story doesn't own.

**What replaces it later:** Story 1.5 (Sign In/Out) and Story 1.6 (server-side role gating) are what introduce real, `/api/auth/me`-backed identity state. When that lands, `App.jsx` should pass the real signed-in value into `Home` instead of relying on the default — expect to delete the standalone prop default at that point, not extend it into a bigger mechanism now.

This same "no real auth yet" gap is why Task 4 leaves `Schedule Appointment`/`My Schedule`/`Admin Panel` nav links inert rather than swapping the whole nav to "signed-in mode" — FR29's Login/Register-vs-profile-dropdown nav swap is explicitly Story 1.5's AC, not this one's.

### Route Naming Convention (locked by this story)

First story to add real routes — the paths chosen here are the ones every later story must match exactly, to avoid the classic drift bug where two stories invent two different names for the same page:

| Path | Page | Story that builds it |
|---|---|---|
| `/` | Home | 1.3 (this story) |
| `/about` | About | 1.3 (this story) |
| `/login` | Login | 1.5 |
| `/register` | Register | 1.4 |
| `/schedule-appointment` | Schedule Appointment | 2.2 |
| `/my-schedule` | My Schedule (barber/admin) | 2.5 / 2.6 |
| `/admin` | Admin Panel | 3.2 |
| `/account` | Account | 1.7 |

### Testing Requirements

- Frontend only: Vitest + jsdom + React Testing Library + user-event, consistent with every prior story — no MSW (AD-4).
- React Router integration tests use real `<MemoryRouter>` + real `<Routes>`, never a mocked `useNavigate` — this is both the RTL-documented pattern and the only way to actually prove navigation happened rather than just that a function was called with an argument.
- `NavBar.test.jsx` **will fail as currently written** the moment `Link`/`useLocation` are introduced (no Router context in that test's bare `render(<NavBar />)`) — Task 5 fixing it is a required regression fix, not optional polish.

### Project Structure Notes

- `frontend/src/pages/` currently holds only `.gitkeep` (Story 1.1) — this story is what populates it for the first time, with `Home.jsx`/`Home.css`/`Home.test.jsx` and `About.jsx`/`About.css`/`About.test.jsx`.
- `App.jsx`'s Story 1.1 showcase content is being replaced, not extended — see Task 1. `main.jsx` gains one new import/wrapper (`BrowserRouter`) and is otherwise unchanged.
- `NavBar.jsx`/`NavBar.css` are modified, not rebuilt — only the link elements and an added active-state class change; the component's existing static shell (logo, Sign In/Register buttons) is untouched (that's Story 1.5's job).
- No backend changes (`backend/`) in this story.

### Previous Story Intelligence (from Stories 1.1 and 1.2)

- Story 1.1 built `Button`, `Input`, `NavBar` (static shell), `Footer` (fully static, already has the address/phone/hours copy this story's About page must match), `Modal`, and `ConfirmPopup` — all reusable as-is. Story 1.1 also explicitly flagged that it deliberately did **not** install React Router and left NavBar's links/auth-state/routing static "since those depend on pages/auth that don't exist until Stories 1.3–1.6" — this story is the first to cash that in, for Home/About only.
- Story 1.1's Dev Notes already researched React Router's v8 packaging change (`react-router-dom` gone, import from `"react-router"` / `"react-router/dom"`) specifically flagging it for "when Story 1.3 or 1.6 adds React Router" — see Latest Tech Info below for the confirmed-current version.
- Story 1.1 found Vitest doesn't auto-run RTL cleanup between tests (fixed via `afterEach(() => cleanup())` in `src/test/setup.js`, already in place — no action needed here) and that Radix Dialog's outside-click detection requires clicking `.modal-overlay`, not `document.body` (relevant only if this story's tests ever interact with `ConfirmPopup`, which they don't).
- Story 1.2 (backend-only, Account entity/repository) has no direct carryover to this frontend-only story beyond confirming the branch-naming convention (`story/{epic}.{story}-{slug}`) that Task 7 continues.

### Git Intelligence Summary

Recent commits (`0c2cb3f` sprint planning → `1bdefaf`/`1149733` Story 1.1 → `ebd095d`/`35ed372` Story 1.2) show every prior story following the same implement → self-verify CI green → review → patch cycle before flipping to `done`, entirely within its own short-lived branch. Follow the same shape here.

### Latest Tech Info (verified 2026-07-29, story-creation time)

- **React Router v8 is current** (v8.0 shipped 2026-06-17, v8.3.0 published within the last day as of this research) — `react-router-dom` no longer exists as a package. Install `react-router` only. For this project's plain Vite SPA in **declarative mode** (no framework/data-router mode), `BrowserRouter`, `Routes`, `Route`, `Link`, `useNavigate`, and `useLocation` all import from `"react-router"` directly — the `"react-router/dom"` subpath is only needed for `RouterProvider`/`HydratedRouter` (data/framework mode), which this app doesn't use. [Confirmed against reactrouter.com/start/declarative/installation.]
- v8's minimum requirements are Node ≥22.22.0 and React ≥19.2.7 — this project's React pin (19.2.8) clears that; CI's `setup-node@v7` with `node-version: '22'` should resolve to a current 22.x patch, but verify the actual resolved version is ≥22.22.0 when CI runs (re-check if the frontend job fails on an engine-version error, don't assume).
- Pin `react-router` at exactly `8.3.0` (latest at research time) to match this project's exact-pinning convention for every other dependency — re-verify it's still the current patch at actual implementation time, the package updates frequently right now.

### References

- [Source: _bmad-output/planning-artifacts/epics.md §Story 1.3] — story statement, AC
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-bmad-learning-project-2026-07-23/DESIGN.md frontmatter `components.button-primary`; §Components ("Home hero"); §Colors; §Typography] — hero spec, CTA label, color/type tokens
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-bmad-learning-project-2026-07-23/EXPERIENCE.md §Information Architecture; §Component Patterns ("Home hero" row); §Voice and Tone; §Responsive & Platform] — routing/IA, CTA branching behavior, copy register, breakpoint behavior
- [Source: _bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/ARCHITECTURE-SPINE.md#AD-18, #Stack] — client routing convention, React Router version note
- [Source: _bmad-output/implementation-artifacts/1-1-project-scaffold-ci-pipeline-and-design-system-foundation.md §Dev Notes ("AD-18 is explicitly NOT this story's job"), §Latest Tech Info (React Router v8 packaging research)] — established component inventory, deferred-routing rationale
- [Source: frontend/src/components/NavBar.jsx, NavBar.test.jsx, Footer.jsx, Button.jsx, styles/tokens.css, styles/breakpoints.js] — current (pre-story) state of files this story modifies/reuses
- [Source: project-context.md §Technology Stack & Versions; §Framework-Specific Rules (React); §Testing Rules; §Code Quality & Style Rules]
- [reactrouter.com/start/declarative/installation](https://reactrouter.com/start/declarative/installation) — confirmed v8 declarative-mode install/import pattern

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (Amelia persona, bmad-dev-story workflow)

### Debug Log References

- `npm test` (frontend, Vitest) — 31/31 passed across 8 test files.
- `npm run lint` (ESLint) — 0 errors.
- `npm run format:check` (Prettier) — reports 31 pre-existing files (including files untouched by this story) as unformatted; confirmed via `git stash` that this same warning fires against the unmodified baseline (33 files) due to local `core.autocrlf=true` converting the Windows checkout to CRLF while Prettier's default `endOfLine` expects `lf` — a pre-existing local-environment artifact, not a regression from this story. CI runs on `ubuntu-latest` where checkout doesn't rewrite line endings, so this shouldn't reproduce there; flagged for Jack's awareness rather than silently patched (fixing it would touch every file in the repo, well outside this story's scope).
- `npm run build` (Vite) — succeeded, no errors.
- Manual verification via headless Playwright (Chromium): navigated to `/` and `/about` at 1280px and 375px viewports, screenshotted both, clicked the Home CTA signed-out (confirmed navigation to `/login`), and checked `console --errors`/`pageerror` — none fired. Confirmed NavBar's active-link class (`nav-bar__link--active`) applies correctly and `Schedule Appointment`/`My Schedule`/`Admin Panel` render as non-link text.

### Completion Notes List

- Installed `react-router@8.3.0` (exact-pinned) and wrapped `<App />` in `<BrowserRouter>` in `main.jsx` only — `App.jsx` stays testable via `<MemoryRouter>` with no nested-router conflict.
- Replaced `App.jsx`'s Story 1.1 design-system showcase with `<NavBar />`, a `<Routes>` block (`/` → `Home`, `/about` → `About`), and `<Footer />`; deleted the now-dead `App.css` (its only rules were showcase-specific) and its import.
- Built `Home.jsx`/`Home.css`: diagonal white/teal hero via `clip-path` above the 640px breakpoint (stacks vertically below it, reusing the existing `breakpoints.js`-documented 640/1024 values as hardcoded `@media` queries per the established tokens.css convention), headline/tagline copy in the locked voice register, `Button variant="primary"` CTA reusing Story 1.1's existing hover styling, and a simple aria-hidden crossed-line SVG standing in for the scissors-and-comb graphic. `isSignedIn` prop (default `false`) branches the CTA's `useNavigate()` target between `/login` and `/schedule-appointment` — the documented temporary seam, not a real auth mechanism.
- Built `About.jsx`/`About.css`: address/phone/hours copied verbatim from `Footer.jsx`, static barber list ("Manny, Dana, and Theo"), no data fetching.
- `NavBar.jsx`: `Home`/`About` now `<Link>`s from `react-router`; `Schedule Appointment`/`My Schedule`/`Admin Panel` are inert `<span>`s (not links, not `href="#"`); added `.nav-bar__link--active` (via `useLocation()`) using the existing `--color-primary` token, per `DESIGN.md`'s locked active-link spec.
- Updated `NavBar.test.jsx` for the routing change (wrapped in `<MemoryRouter>`, split the "renders all five" assertion into routed-link vs. inert-text checks) and added a new active-link-class test.
- Added `Home.test.jsx`, `About.test.jsx`, and `App.test.jsx` per Task 6 — Home's navigation tests use real `<MemoryRouter>` + `<Routes>` with stub destination routes, no mocked `useNavigate`.
- Manual browser check (headless Chromium via Playwright) surfaced one pre-existing, out-of-scope gap: `NavBar` doesn't collapse/wrap at narrow viewports (causes horizontal overflow below ~640px), per `EXPERIENCE.md`'s documented (but not-yet-built) nav-collapse pattern. This predates this story (Story 1.1's static shell, untouched here per this story's own Dev Notes — "that's Story 1.5's job") and isn't introduced by any change in this diff; Home's and About's own content does not overflow at any tested width. Flagging for whichever later story ends up owning NavBar's responsive behavior.
- No backend changes; backend CI job unaffected.

### File List

- `frontend/package.json` (modified — added `react-router@8.3.0`)
- `frontend/package-lock.json` (modified)
- `frontend/src/main.jsx` (modified — wrapped `<App />` in `<BrowserRouter>`)
- `frontend/src/App.jsx` (modified — removed showcase, added `<NavBar />`/`<Routes>`/`<Footer />`)
- `frontend/src/App.css` (deleted — dead showcase-only styles)
- `frontend/src/App.test.jsx` (new)
- `frontend/src/pages/Home.jsx` (new)
- `frontend/src/pages/Home.css` (new)
- `frontend/src/pages/Home.test.jsx` (new)
- `frontend/src/pages/About.jsx` (new)
- `frontend/src/pages/About.css` (new)
- `frontend/src/pages/About.test.jsx` (new)
- `frontend/src/components/NavBar.jsx` (modified — real `Link`s, inert spans, active-link state)
- `frontend/src/components/NavBar.css` (modified — added `.nav-bar__link--active`/`.nav-bar__link--inert`)
- `frontend/src/components/NavBar.test.jsx` (modified — `MemoryRouter` wrap, routed-vs-inert assertions, active-link test)

## Change Log

- 2026-07-29: Implemented Home and About pages (Tasks 1-7); React Router v8 wired, all ACs satisfied, 31/31 frontend tests passing, lint clean, build clean; status set to review.
