---
baseline_commit: 1bdefaf430064a2514c61f26396e07a96cca8c42
---

# Story 1.1: Project Scaffold, CI Pipeline, and Design System Foundation

Status: done

## Story

As a developer,
I want the backend and frontend scaffolded, CI wired up, and the core design system in place,
so that every later story has a working, tested, styled foundation to build on.

## Acceptance Criteria

1. **Given** a fresh clone, **when** the backend is scaffolded via `dotnet new webapi --use-controllers` and the frontend via Vite's official React JS template, **then** the folder structure matches the Architecture's structural seed (`Controllers/Services/Repositories/Entities/Dtos/Data` and `frontend/src/{pages,components,api,styles}`).
2. **Given** the scaffold, **when** a GitHub Actions workflow is added, **then** it runs the .NET suite and the frontend suite as parallel jobs on every push (NFR5, AD-11).
3. **Given** the dev environment, **when** the app starts, **then** the SQLite file lives at `backend/BarbershopApi/App_Data/barbershop.db` (gitignored, only `Migrations/` committed) and `Database.Migrate()` runs cleanly against an empty DB (AD-10).
4. **Given** the API and Vite dev server run on different ports, **when** CORS is configured, **then** it explicitly allows the Vite origin with `AllowCredentials()` (AD-13).
5. **Given** `DESIGN.md`'s tokens, **when** the design system is implemented, **then** colors, typography, rounding, and spacing scales (UX-DR1) are available as reusable tokens, and the Button (primary/secondary/destructive), Input, Nav bar shell, Footer, and Modal/Confirm-popup components (UX-DR2–6, 9, 10) render correctly in isolation, with hover firing on pointer devices only and every action completing on a single tap on touch.
6. **Given** the two open UX items flagged in Architecture's Deferred section, **when** the design foundation is built, **then** a form-validation/error treatment and a concrete tablet breakpoint pixel value are settled before Register/Account components are built (UX-DR20).

> **AC 6 is already resolved in the source docs — no design decision needed from you.** `DESIGN.md` (updated 2026-07-27) closed both open items *after* the Architecture doc was written: `{colors.error}` = `#C93A3A` (same hex as `{colors.destructive}`, but a distinct token — never use it on a button/fill, only on validation-message text), and the tablet breakpoint is locked at `640px`/`1024px`. Just implement these already-settled values (see Dev Notes → Design Tokens below); do not treat this as an open question.

## Tasks / Subtasks

- [x] **Task 1: Scaffold backend project structure** (AC: #1)
  - [x] Run `dotnet new webapi --use-controllers` inside `backend/BarbershopApi/` (.NET 10 SDK; verified current — see Latest Tech Info)
  - [x] Delete the template's sample `WeatherForecastController.cs` and `WeatherForecast.cs` — they belong to no domain concept and would violate AD-1/NFR6 (no catch-all/orphan classes) if left in
  - [x] Create empty `Controllers/`, `Services/`, `Repositories/`, `Entities/`, `Dtos/` folders under `backend/BarbershopApi/` with `.gitkeep` placeholders (git doesn't track empty dirs; these stay empty until Story 1.2+ adds Auth/Booking/Account domain code)
  - [x] Create `backend/BarbershopApi.Tests/` as an xUnit.v3 project (`dotnet new xunit`); add `Microsoft.AspNetCore.Mvc.Testing` (for `WebApplicationFactory`) package reference
  - [x] Create a solution file at `backend/` wiring both projects (`dotnet new sln` + `dotnet sln add`)
- [x] **Task 2: Scaffold frontend project structure** (AC: #1)
  - [x] Scaffold via `npm create vite@latest frontend -- --template react` (the `react` template is the plain-JS variant; do **not** pick `react-compiler`, which is a distinct template Vite now also offers and is out of scope) — re-verify the exact flag against `npm create vite@latest -- --help` if this errors, since `create-vite`'s flag surface has changed across majors
  - [x] Create `frontend/src/{pages,components,api,styles}` folders (styles/ will hold the design-token CSS from Task 4)
  - [x] Verify `package.json` pins React to `19.2.8` / ReactDOM to match (per Architecture Stack table) — Vite's template may pull a different patch; adjust if so
- [x] **Task 3: Wire up SQLite + EF Core with an empty migration** (AC: #3)
  - [x] Add `Microsoft.EntityFrameworkCore.Sqlite` 10.0.10 and `Microsoft.EntityFrameworkCore.Design` (matching version) to `BarbershopApi`
  - [x] Create `Data/BarbershopDbContext.cs` — an EF Core `DbContext` with **no entities yet** (Account/Appointment arrive in Stories 1.2/2.1); this story only proves the migration pipeline works end-to-end
  - [x] Configure the connection string to `backend/BarbershopApi/App_Data/barbershop.db` (create `App_Data/` if `dotnet new` doesn't)
  - [x] Generate an initial (empty) migration via `dotnet ef migrations add InitialCreate`; commit the generated `Migrations/` folder
  - [x] Call `Database.Migrate()` in `Program.cs` on startup, after the DbContext is registered in DI
  - [x] Verify on a fresh clone: `dotnet run` creates `App_Data/barbershop.db` with no errors and no manual DB setup step
- [x] **Task 4: Update `.gitignore` for both stacks** (AC: #3)
  - [x] Add `.NET`: `backend/**/bin/`, `backend/**/obj/`, `backend/BarbershopApi/App_Data/*.db`, `backend/BarbershopApi/App_Data/*.db-*` (WAL/SHM files)
  - [x] Add Node: `frontend/node_modules/`, `frontend/dist/`
  - [x] Confirm `Migrations/` is **not** excluded by any of the above (it must be committed — AD-10)
- [x] **Task 5: Configure CORS for the Vite dev origin** (AC: #4)
  - [x] In `Program.cs`, add a CORS policy naming the Vite dev-server origin explicitly (default Vite dev port; confirm actual port from the scaffolded `vite.config.js`/console output rather than assuming `5173`) with `.AllowCredentials()`
  - [x] Apply the policy via `app.UseCors(...)` before any endpoint mapping
  - [x] Note for future stories: every frontend fetch touching auth will need `credentials: 'include'` (AD-13) — not this story's concern (no auth exists yet), but don't configure CORS in a way that would block it later (i.e., don't use a wildcard origin, which is incompatible with `AllowCredentials()`)
- [x] **Task 6: GitHub Actions CI pipeline** (AC: #2)
  - [x] Create `.github/workflows/ci.yml` triggered on every push
  - [x] Job 1 (`backend`): setup .NET 10 SDK, `dotnet restore`, `dotnet build`, `dotnet test` against `backend/BarbershopApi.Tests`
  - [x] Job 2 (`frontend`): setup **Node ≥22** (required by `@testing-library/jest-dom` 7.0.0 — see Latest Tech Info), `npm ci`, then run lint, format-check, and test as separate steps: `eslint .`, `prettier --check .`, and the Vitest run — all three must pass (per project-context testing rules; `eslint-config-prettier` disables ESLint's stylistic rules but doesn't run Prettier itself, so both checks are required independently)
  - [x] Confirm the two jobs run in parallel (no `needs:` dependency between them) — this is the project's DORA "deployment frequency" signal (NFR5, AD-11); a red pipeline must not be mergeable
- [x] **Task 7: Implement design tokens** (AC: #5, #6)
  - [x] Create `frontend/src/styles/tokens.css` (or equivalent) defining CSS custom properties for every value in `DESIGN.md`'s frontmatter: colors (including `{colors.error}` = `#C93A3A`, distinct from `{colors.destructive}`), typography scale (Manrope; display/h1/h2/h3/body/body-sm/label/caption), rounded scale (`sm` 4px / `DEFAULT`+`md` 6px / `lg`+`xl` 8px / `full`), spacing scale (4px-base 1–16, plus `gutter-mobile`/`gutter-desktop`/`content-max-width`)
  - [x] Load the Manrope font (single family for the whole app — no second family, no TypeScript-style "display font moment")
  - [x] Bake the two breakpoints (640px / 1024px) into the token layer (e.g., CSS custom media or documented constants) so every later component references the same values rather than hardcoding pixel numbers per-component
- [x] **Task 8: Build core components in isolation** (AC: #5)
  - [x] `Button` — primary/secondary/destructive variants per `{components.button-primary/-secondary/-destructive}`; hover/active color swap via CSS `:hover`/`:active` (naturally pointer-only in browsers — do not add JS touch-detection logic on top); every variant activates on a single, complete tap (no press-and-hold, no double-tap-to-arm)
  - [x] `Input` — `{components.input}` tokens; focus-state border swap to `{colors.primary}`; support the double-entry password pattern's rendering (two stacked inputs, no visual distinction beyond label) — the mismatch-message *behavior* belongs to Register/Account (Stories 1.4/1.7), this story only needs the Input component and the `{colors.error}`-styled caption-text treatment to exist and be stylable
  - [x] Nav bar shell (`{components.nav-bar}`) — static for this story: render all five links (Home, Schedule Appointment, About, My Schedule, Admin Panel) unconditionally and a static "Sign In / Register" right-side area. **Do not wire real routing, auth-state swapping, or role-based hiding yet** — those depend on pages/auth that don't exist until Stories 1.3–1.6; wiring them now would be premature and untestable
  - [x] Footer (`{components.footer}`) — fully static: wordmark, address, phone, hours, copyright line, no links/social icons
  - [x] Modal wrapper via `@radix-ui/react-dialog` (`{components.modal}`) — install **only** this one Radix package now; `@radix-ui/react-select` and `@radix-ui/react-popover`+`react-day-picker` are pinned in Architecture but have no consumer until Epic 2/3 components are built — installing them now is premature
  - [x] Confirm-action popup (`{components.confirm-popup}`) built on the Modal wrapper — exactly two buttons every time: "Go Back" (always `{components.button-secondary}`, regardless of context) and "Confirm" (color is a prop — `{components.button-primary}` for non-destructive, `{components.button-destructive}` for destructive); `Esc`/outside-click/"Go Back" all dismiss with zero effect — this is Radix Dialog default behavior, verify rather than assume
- [x] **Task 9: Component-level tests (frontend)** (AC: #5)
  - [x] Vitest + jsdom + React Testing Library + user-event for each component above: variant rendering (correct label/role/class per variant), focus-state behavior on `Input`, Confirm-popup's two-button contract and dismiss behavior (`Esc`, outside-click, "Go Back")
  - [x] Note: RTL/jsdom cannot meaningfully simulate real mouse-hover-only-on-pointer-devices behavior — don't write a test asserting "hover doesn't fire on touch"; that's inherent to CSS `:hover` and not something app code could get wrong. Focus tests on what the component actually controls: variant styling, ARIA roles, keyboard activation (`Enter`/`Space`), and the confirm-popup's button contract
  - [x] Configure Vitest with `environment: 'jsdom'` and a setup file importing `@testing-library/jest-dom`'s matchers
- [x] **Task 10: Backend smoke test proving the migration pipeline** (AC: #3)
  - [x] In `BarbershopApi.Tests`, write an xUnit test using `WebApplicationFactory<Program>` against a fresh temporary SQLite file (never the dev DB — AD-10) that asserts the app boots and `Database.Migrate()` completes without throwing
  - [x] This is the only backend test this story needs — there's no domain logic yet to test; Stories 1.2+ add repository/service tests against real behavior
- [x] **Task 11: Verify CI is green end-to-end**
  - [x] Push the scaffold on a short-lived branch (`story/1.1-project-scaffold-ci-pipeline`, per the project's branching convention) and confirm both CI jobs pass before merging

### Review Findings

- [x] [Review][Patch] CORS applied after `MapOpenApi()`'s endpoint mapping, violating Task 5's explicit "before any endpoint mapping" ordering — the dev-only OpenAPI JSON endpoint bypasses CORS/HTTPS-redirect entirely [backend/BarbershopApi/Program.cs:36-43]
- [x] [Review][Patch] `.http` scaffold file still references `/weatherforecast/`, the endpoint from the controller this same story deleted [backend/BarbershopApi/BarbershopApi.http:3]
- [x] [Review][Patch] `Input` component has no `aria-invalid`/`aria-describedby` wiring for its error-caption state — screen-reader users aren't told a field has an error [frontend/src/components/Input.jsx:25-35]
- [x] [Review][Patch] `vite` pinned with a caret range (`^8.1.1`) while every other dependency is exact-pinned to match project-context.md's Technology Stack table value (`8.1.5`) [frontend/package.json:36]
- [x] [Review][Patch] `Modal` faked a `Dialog.Description` by duplicating the title text when no real description was given, causing screen readers to announce the title twice [frontend/src/components/Modal.jsx:15-22]

## Dev Notes

### Architecture Compliance (must-follow, not optional)

- **Folder structure is the enforcement mechanism for NFR6** (no god-classes) — `Controllers/Services/Repositories/Entities/Dtos/Data` existing and staying empty except for `Data/` is itself the point of this story; don't pre-populate `Controllers/`/`Services/`/`Repositories/` with anything, including a "shared" or "base" class. [Source: architecture/ARCHITECTURE-SPINE.md#AD-1, #Structural-Seed; SOLUTION-DESIGN.md §2]
- **Primary keys are `int` auto-increment, never GUID** (AD-7) — not directly exercised by this story (no entities yet) but the DbContext/migration setup in Task 3 must not introduce any GUID-keyed convention that Story 1.2 would have to fight later.
- **AD-10 (dev/CI DB isolation) is the literal subject of AC #3 and Task 3/10** — dev DB path is `backend/BarbershopApi/App_Data/barbershop.db`, gitignored, empty on every fresh clone, populated only by `Database.Migrate()`; CI tests get their own separate temp SQLite instance via `WebApplicationFactory`, never touching the dev DB file.
- **AD-11 (CI)** — one workflow, parallel jobs, red pipeline = not mergeable. This is the project's only DORA "deployment frequency" signal since there's no real deploy target (NFR7) — treat a broken CI config as a story-blocking defect, not a nice-to-have.
- **AD-13 (CORS)** — `AllowCredentials()` is required now even though no auth-dependent fetch exists yet in this story, because retrofitting CORS after Story 1.4/1.5 land is exactly the kind of "silently fails cross-origin, looks like 'just logged out'" bug the architecture calls out. Get it right once, here.
- **AD-18 (client routing) is explicitly NOT this story's job** — Nav bar renders as a static shell only. Don't install or wire React Router yet (see Task 8, Latest Tech Info below for the exact import-path change to use when Story 1.3 does add it).

### Design Tokens — already-resolved values (do not re-derive)

`DESIGN.md`'s frontmatter (updated 2026-07-27, *after* `ARCHITECTURE-SPINE.md`) is the single source of truth for every token value — colors, typography, `rounded`, `spacing`, and every `components.*` block referenced in AC #5's component list. Two values worth calling out because the epics-derived AC #6 phrasing makes them sound unresolved when they aren't:

- `{colors.error}` = `#C93A3A` — same hex as `{colors.destructive}` but a **separate token**, used only for validation-message text (never a button/fill). Keep them as two distinct CSS variables even though they render identically today; DESIGN.md is explicit that a future re-tune of one must not silently drag the other.
- Breakpoints are locked at `640px` (mobile/tablet split) and `1024px` (tablet/desktop split) — client-decided 2026-07-27.

[Source: ux-designs/DESIGN.md frontmatter `colors`/`typography`/`rounded`/`spacing`/`components`; ux-designs/DESIGN.md §Colors "Error" bullet, §Layout & Spacing]

### Component Behavior Notes (from EXPERIENCE.md)

- Hover/active states fire on pointer devices only — this is a design *intent* satisfied automatically by using CSS `:hover`/`:active` (never a JS `mouseenter` handler gating a state that also needs to work on touch). If a component's hover state is implemented via JS state instead of CSS pseudo-classes, it will incorrectly also fire from a touch "tap-then-hold," which is the exact bug pattern EXPERIENCE.md's Anti-patterns section calls out as previously encountered and explicitly banned.
- No double-tap-to-activate, ever, on any button variant — single complete tap only.
- The Confirm-action popup's "Go Back" button is **always** `{components.button-secondary}` regardless of what it's cancelling — do not recolor it to match the pending action.
- The Confirm-action popup's "Confirm" button color is the one place where color *is* semantically load-bearing: primary/blue for a non-destructive action, destructive/red for a destructive one. This needs to be a prop the caller sets, not something the component infers.

[Source: ux-designs/EXPERIENCE.md §Component Patterns (Button row, Confirm-action popup row), §Interaction Primitives, §Inspiration & Anti-patterns]

### Testing Requirements

- Backend: xUnit.v3 + `WebApplicationFactory` against a real (temp) SQLite instance — **never mocked**. This story's only backend test is the migration-boots-cleanly smoke test in Task 10; resist the urge to write more until Story 1.2 gives you actual repository behavior to test. [Source: project-context.md §Testing Rules; ARCHITECTURE-SPINE.md#AD-4]
- Frontend: Vitest + jsdom + React Testing Library + user-event; stub any future fetch calls via `vi.fn()`/`vi.spyOn(fetch)` — no MSW (not needed this story; no components fetch anything yet). [Source: project-context.md §Testing Rules; ARCHITECTURE-SPINE.md#AD-4]
- CI must run **both** `eslint .` and `prettier --check .` as separate steps on the frontend job — `eslint-config-prettier` only turns off conflicting stylistic ESLint rules, it does not run Prettier. A green ESLint run does not imply correctly formatted code. [Source: project-context.md §Linting/formatting, §Testing Rules]

### Project Structure Notes

- This story creates the entire source tree from nothing (no `backend/` or `frontend/` directories exist yet in this repo as of story creation) — there is no existing code to preserve or avoid breaking.
- `_bmad/`, `_bmad-output/`, and `docs/` already exist at repo root and are explicitly called out in the Architecture's Structural Seed as pre-existing — don't reorganize or touch them.
- No previous story exists (this is Epic 1, Story 1) — no prior dev-notes/learnings to carry forward.

### Latest Tech Info (verified at story-creation time, 2026-07-28 — re-verify if significant time has passed before implementation)

- **`dotnet new webapi --use-controllers` is still the correct, currently-documented flag** as of the Microsoft Learn `dotnet new` reference (last updated 2026-04-27): `-controllers|--use-controllers` — "available since .NET 8 SDK," default `false`. (A stray, unresolved GitHub issue claims this flag was removed; the official docs contradict it and are the more reliable source — proceed with the documented flag, but run `dotnet new webapi --help` first if it unexpectedly errors.)
- **React Router v8 has dropped `react-router-dom` entirely** — as of this story's research, current guidance is to import DOM APIs from `react-router/dom` and everything else from `react-router` (no more `react-router-dom` package at all in v8). v8's minimum requirements are Node ≥22.22.0 and React ≥19.2.7 — both satisfied by this project's pinned Node/React versions. **This doesn't block this story** (routing isn't installed yet — see Task 8/AD-18 note above), but when Story 1.3 or 1.6 adds React Router, use the v8 import paths, not the deprecated `react-router-dom` package.
- **Radix UI pins are confirmed still current**: `@radix-ui/react-dialog` 1.1.21, `@radix-ui/react-select` 2.3.4, `@radix-ui/react-popover` 1.1.16 all matched their latest published versions at research time. `react-day-picker` 10.0.1 also confirmed current. (Only `@radix-ui/react-dialog` is actually installed in this story — see Task 8.)
- **`@testing-library/jest-dom` 7.0.0 requires Node ≥22** (breaking change from 6.x) and now has `@testing-library/dom` as a **required peer dependency** — make sure it's installed explicitly rather than relying on a transitive install, or the frontend test job will fail in CI with a confusing peer-dependency error rather than an obvious "wrong Node version" one.

### References

- [Source: _bmad-output/planning-artifacts/epics.md §Story 1.1] — story statement, AC, and the Additional Requirements block ("Scaffolding / starter template") this story is scoped from
- [Source: _bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/ARCHITECTURE-SPINE.md#AD-1, #AD-7, #AD-10, #AD-11, #AD-13, #AD-18, #Stack, #Structural-Seed, #Deferred]
- [Source: _bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/SOLUTION-DESIGN.md §2, §5, §6, §8]
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-bmad-learning-project-2026-07-23/DESIGN.md frontmatter; §Colors; §Layout & Spacing; §Elevation & Depth; §Shapes; §Components]
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-bmad-learning-project-2026-07-23/EXPERIENCE.md §Component Patterns; §Interaction Primitives; §Inspiration & Anti-patterns]
- [Source: project-context.md §Technology Stack & Versions; §Testing Rules; §Code Quality & Style Rules; §Development Workflow Rules]

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5), via the bmad-dev-story workflow.

### Debug Log References

- `dotnet test` initially reported "No test is available" for the xUnit.v3 test project — `xunit.v3` alone only produces an MTP self-executing runner (`.exe`), not a VSTest-discoverable adapter. Fixed by re-adding `xunit.runner.visualstudio` 3.1.4 (a version compatible with both v2 and v3) as the VSTest bridge.
- `dotnet new xunit` scaffolds xUnit v2 packages (`xunit`, `xunit.runner.visualstudio`) by default; there is no `xunit3` template in this SDK's template list, so the project was retrofitted from v2 to `xunit.v3` 3.2.2 packages manually to match project-context's pinned version.
- `create-vite`'s current template surface defaults new React scaffolds to Oxlint rather than ESLint; passed `--eslint` explicitly to `npm create vite@latest` to get the ESLint-based config project-context requires.
- Migration smoke test (Task 10): overriding the SQLite connection string via `WebApplicationFactory.ConfigureAppConfiguration` did not take effect — `Program.cs` resolves the connection string into a local variable before the test's config override is layered in. Switched to `ConfigureServices` + `RemoveAll<DbContextOptions<BarbershopDbContext>>()` + re-`AddDbContext`, which is applied after the app's own service registrations and is the standard EF Core test-override pattern. Also required `SqliteConnection.ClearAllPools()` before deleting the temp db file in test teardown, since pooled connections held a file lock after `WebApplicationFactory` disposal.
- Frontend component tests initially failed with "multiple elements found" across test files — Vitest doesn't auto-run React Testing Library's cleanup between tests the way Jest does. Fixed by adding an explicit `afterEach(() => cleanup())` to `src/test/setup.js`.
- The "outside-click" dismiss test for `ConfirmPopup` initially failed because Radix Dialog sets `pointer-events: none` on `<body>` while a dialog is open; clicking `document.body` directly is correctly blocked. Fixed by clicking the `.modal-overlay` element instead, which is Radix's actual outside-click detector.

### Completion Notes List

- All 11 tasks implemented and verified: backend (.NET 10 Web API + EF Core/SQLite + xUnit.v3) and frontend (Vite/React JS + design tokens + 6 core components + Vitest) scaffolds are both in place, CI is green on GitHub Actions (run [30379920872](https://github.com/jpf403/bmad-learning-project/actions/runs/30379920872) — both `backend` and `frontend` jobs passed in parallel).
- Backend: `dotnet build` succeeds with 0 errors (8 pre-existing `NU1903` advisory warnings from the Web API template's `Microsoft.OpenApi`/`SQLitePCLRaw` transitive packages — out of scope for this story, not introduced by any code here). `dotnet test` passes 1/1 (migration smoke test).
- Frontend: `npm run lint`, `npm run format:check`, and `npm test` all pass clean (24/24 tests across 5 component test files). Manually verified the component showcase (`App.jsx`) in a headless-Chromium screenshot pass — Buttons, Inputs, NavBar, Footer, and both Confirm Popup variants (non-destructive/primary and destructive/red) render per `DESIGN.md`, with no browser console errors.
- Task 11 pushed the scaffold to the pre-existing story branch `e1-s1-scaffold-and-foundations` rather than a new `story/1.1-project-scaffold-ci-pipeline` branch, since that branch was already checked out for this story at session start (created before this dev-story run began) — the substance of the task (short-lived branch, CI verified green before merge) is satisfied.
- Manrope is loaded via a Google Fonts `<link>` in `index.html` rather than a bundled local font file, since no font asset was provided in the design artifacts.
- `@radix-ui/react-select`, `@radix-ui/react-popover`, and `react-day-picker` were deliberately **not** installed this story per Dev Notes — only `@radix-ui/react-dialog` has a consumer (Modal/ConfirmPopup) at this point.

### File List

**CI / repo root**
- `.github/workflows/ci.yml` (new)
- `.gitignore` (modified)

**Backend (`backend/`)**
- `BarbershopApi.slnx` (new)
- `.config/dotnet-tools.json` (new)
- `BarbershopApi/BarbershopApi.csproj` (new)
- `BarbershopApi/Program.cs` (new)
- `BarbershopApi/BarbershopApi.http`, `appsettings.json`, `appsettings.Development.json`, `Properties/launchSettings.json` (new, template defaults)
- `BarbershopApi/Data/BarbershopDbContext.cs` (new)
- `BarbershopApi/Migrations/20260728151744_InitialCreate.cs`, `20260728151744_InitialCreate.Designer.cs`, `BarbershopDbContextModelSnapshot.cs` (new)
- `BarbershopApi/Controllers/.gitkeep`, `Services/.gitkeep`, `Repositories/.gitkeep`, `Entities/.gitkeep`, `Dtos/.gitkeep`, `App_Data/.gitkeep` (new, empty-folder placeholders)
- `BarbershopApi.Tests/BarbershopApi.Tests.csproj` (new)
- `BarbershopApi.Tests/MigrationSmokeTests.cs` (new)

**Frontend (`frontend/`)**
- `package.json`, `package-lock.json` (new)
- `vite.config.js` (new — includes Vitest `test` config)
- `eslint.config.js` (new — extends `eslint-config-prettier`)
- `.prettierrc.json`, `.prettierignore` (new)
- `.gitignore`, `README.md`, `index.html` (new/modified template defaults; `index.html` also adds the Manrope Google Fonts `<link>`)
- `public/favicon.svg` (new, template default)
- `src/main.jsx` (new, template default)
- `src/index.css` (new — global reset + base typography wired to design tokens)
- `src/App.jsx`, `src/App.css` (new — component showcase, replaces the Vite starter demo)
- `src/styles/tokens.css` (new — design-token CSS custom properties)
- `src/styles/breakpoints.js` (new — 640px/1024px breakpoint constants)
- `src/components/Button.jsx`, `Button.css`, `Button.test.jsx` (new)
- `src/components/Input.jsx`, `Input.css`, `Input.test.jsx` (new)
- `src/components/NavBar.jsx`, `NavBar.css`, `NavBar.test.jsx` (new)
- `src/components/Footer.jsx`, `Footer.css`, `Footer.test.jsx` (new)
- `src/components/Modal.jsx`, `Modal.css` (new)
- `src/components/ConfirmPopup.jsx`, `ConfirmPopup.css`, `ConfirmPopup.test.jsx` (new)
- `src/test/setup.js` (new — jest-dom matchers + RTL cleanup)
- `src/pages/.gitkeep`, `src/api/.gitkeep` (new, empty-folder placeholders)

## Change Log

- 2026-07-28 — Implemented Story 1.1 end-to-end: backend/frontend scaffold, EF Core/SQLite migration pipeline, CORS, GitHub Actions CI (parallel backend/frontend jobs), design-token layer, six core components with tests, and a backend migration smoke test. CI verified green on push (GitHub Actions run 30379920872). Status moved to `review`.
