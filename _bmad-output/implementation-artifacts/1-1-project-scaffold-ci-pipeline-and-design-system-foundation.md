# Story 1.1: Project Scaffold, CI Pipeline, and Design System Foundation

Status: ready-for-dev

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

- [ ] **Task 1: Scaffold backend project structure** (AC: #1)
  - [ ] Run `dotnet new webapi --use-controllers` inside `backend/BarbershopApi/` (.NET 10 SDK; verified current — see Latest Tech Info)
  - [ ] Delete the template's sample `WeatherForecastController.cs` and `WeatherForecast.cs` — they belong to no domain concept and would violate AD-1/NFR6 (no catch-all/orphan classes) if left in
  - [ ] Create empty `Controllers/`, `Services/`, `Repositories/`, `Entities/`, `Dtos/` folders under `backend/BarbershopApi/` with `.gitkeep` placeholders (git doesn't track empty dirs; these stay empty until Story 1.2+ adds Auth/Booking/Account domain code)
  - [ ] Create `backend/BarbershopApi.Tests/` as an xUnit.v3 project (`dotnet new xunit`); add `Microsoft.AspNetCore.Mvc.Testing` (for `WebApplicationFactory`) package reference
  - [ ] Create a solution file at `backend/` wiring both projects (`dotnet new sln` + `dotnet sln add`)
- [ ] **Task 2: Scaffold frontend project structure** (AC: #1)
  - [ ] Scaffold via `npm create vite@latest frontend -- --template react` (the `react` template is the plain-JS variant; do **not** pick `react-compiler`, which is a distinct template Vite now also offers and is out of scope) — re-verify the exact flag against `npm create vite@latest -- --help` if this errors, since `create-vite`'s flag surface has changed across majors
  - [ ] Create `frontend/src/{pages,components,api,styles}` folders (styles/ will hold the design-token CSS from Task 4)
  - [ ] Verify `package.json` pins React to `19.2.8` / ReactDOM to match (per Architecture Stack table) — Vite's template may pull a different patch; adjust if so
- [ ] **Task 3: Wire up SQLite + EF Core with an empty migration** (AC: #3)
  - [ ] Add `Microsoft.EntityFrameworkCore.Sqlite` 10.0.10 and `Microsoft.EntityFrameworkCore.Design` (matching version) to `BarbershopApi`
  - [ ] Create `Data/BarbershopDbContext.cs` — an EF Core `DbContext` with **no entities yet** (Account/Appointment arrive in Stories 1.2/2.1); this story only proves the migration pipeline works end-to-end
  - [ ] Configure the connection string to `backend/BarbershopApi/App_Data/barbershop.db` (create `App_Data/` if `dotnet new` doesn't)
  - [ ] Generate an initial (empty) migration via `dotnet ef migrations add InitialCreate`; commit the generated `Migrations/` folder
  - [ ] Call `Database.Migrate()` in `Program.cs` on startup, after the DbContext is registered in DI
  - [ ] Verify on a fresh clone: `dotnet run` creates `App_Data/barbershop.db` with no errors and no manual DB setup step
- [ ] **Task 4: Update `.gitignore` for both stacks** (AC: #3)
  - [ ] Add `.NET`: `backend/**/bin/`, `backend/**/obj/`, `backend/BarbershopApi/App_Data/*.db`, `backend/BarbershopApi/App_Data/*.db-*` (WAL/SHM files)
  - [ ] Add Node: `frontend/node_modules/`, `frontend/dist/`
  - [ ] Confirm `Migrations/` is **not** excluded by any of the above (it must be committed — AD-10)
- [ ] **Task 5: Configure CORS for the Vite dev origin** (AC: #4)
  - [ ] In `Program.cs`, add a CORS policy naming the Vite dev-server origin explicitly (default Vite dev port; confirm actual port from the scaffolded `vite.config.js`/console output rather than assuming `5173`) with `.AllowCredentials()`
  - [ ] Apply the policy via `app.UseCors(...)` before any endpoint mapping
  - [ ] Note for future stories: every frontend fetch touching auth will need `credentials: 'include'` (AD-13) — not this story's concern (no auth exists yet), but don't configure CORS in a way that would block it later (i.e., don't use a wildcard origin, which is incompatible with `AllowCredentials()`)
- [ ] **Task 6: GitHub Actions CI pipeline** (AC: #2)
  - [ ] Create `.github/workflows/ci.yml` triggered on every push
  - [ ] Job 1 (`backend`): setup .NET 10 SDK, `dotnet restore`, `dotnet build`, `dotnet test` against `backend/BarbershopApi.Tests`
  - [ ] Job 2 (`frontend`): setup **Node ≥22** (required by `@testing-library/jest-dom` 7.0.0 — see Latest Tech Info), `npm ci`, then run lint, format-check, and test as separate steps: `eslint .`, `prettier --check .`, and the Vitest run — all three must pass (per project-context testing rules; `eslint-config-prettier` disables ESLint's stylistic rules but doesn't run Prettier itself, so both checks are required independently)
  - [ ] Confirm the two jobs run in parallel (no `needs:` dependency between them) — this is the project's DORA "deployment frequency" signal (NFR5, AD-11); a red pipeline must not be mergeable
- [ ] **Task 7: Implement design tokens** (AC: #5, #6)
  - [ ] Create `frontend/src/styles/tokens.css` (or equivalent) defining CSS custom properties for every value in `DESIGN.md`'s frontmatter: colors (including `{colors.error}` = `#C93A3A`, distinct from `{colors.destructive}`), typography scale (Manrope; display/h1/h2/h3/body/body-sm/label/caption), rounded scale (`sm` 4px / `DEFAULT`+`md` 6px / `lg`+`xl` 8px / `full`), spacing scale (4px-base 1–16, plus `gutter-mobile`/`gutter-desktop`/`content-max-width`)
  - [ ] Load the Manrope font (single family for the whole app — no second family, no TypeScript-style "display font moment")
  - [ ] Bake the two breakpoints (640px / 1024px) into the token layer (e.g., CSS custom media or documented constants) so every later component references the same values rather than hardcoding pixel numbers per-component
- [ ] **Task 8: Build core components in isolation** (AC: #5)
  - [ ] `Button` — primary/secondary/destructive variants per `{components.button-primary/-secondary/-destructive}`; hover/active color swap via CSS `:hover`/`:active` (naturally pointer-only in browsers — do not add JS touch-detection logic on top); every variant activates on a single, complete tap (no press-and-hold, no double-tap-to-arm)
  - [ ] `Input` — `{components.input}` tokens; focus-state border swap to `{colors.primary}`; support the double-entry password pattern's rendering (two stacked inputs, no visual distinction beyond label) — the mismatch-message *behavior* belongs to Register/Account (Stories 1.4/1.7), this story only needs the Input component and the `{colors.error}`-styled caption-text treatment to exist and be stylable
  - [ ] Nav bar shell (`{components.nav-bar}`) — static for this story: render all five links (Home, Schedule Appointment, About, My Schedule, Admin Panel) unconditionally and a static "Sign In / Register" right-side area. **Do not wire real routing, auth-state swapping, or role-based hiding yet** — those depend on pages/auth that don't exist until Stories 1.3–1.6; wiring them now would be premature and untestable
  - [ ] Footer (`{components.footer}`) — fully static: wordmark, address, phone, hours, copyright line, no links/social icons
  - [ ] Modal wrapper via `@radix-ui/react-dialog` (`{components.modal}`) — install **only** this one Radix package now; `@radix-ui/react-select` and `@radix-ui/react-popover`+`react-day-picker` are pinned in Architecture but have no consumer until Epic 2/3 components are built — installing them now is premature
  - [ ] Confirm-action popup (`{components.confirm-popup}`) built on the Modal wrapper — exactly two buttons every time: "Go Back" (always `{components.button-secondary}`, regardless of context) and "Confirm" (color is a prop — `{components.button-primary}` for non-destructive, `{components.button-destructive}` for destructive); `Esc`/outside-click/"Go Back" all dismiss with zero effect — this is Radix Dialog default behavior, verify rather than assume
- [ ] **Task 9: Component-level tests (frontend)** (AC: #5)
  - [ ] Vitest + jsdom + React Testing Library + user-event for each component above: variant rendering (correct label/role/class per variant), focus-state behavior on `Input`, Confirm-popup's two-button contract and dismiss behavior (`Esc`, outside-click, "Go Back")
  - [ ] Note: RTL/jsdom cannot meaningfully simulate real mouse-hover-only-on-pointer-devices behavior — don't write a test asserting "hover doesn't fire on touch"; that's inherent to CSS `:hover` and not something app code could get wrong. Focus tests on what the component actually controls: variant styling, ARIA roles, keyboard activation (`Enter`/`Space`), and the confirm-popup's button contract
  - [ ] Configure Vitest with `environment: 'jsdom'` and a setup file importing `@testing-library/jest-dom`'s matchers
- [ ] **Task 10: Backend smoke test proving the migration pipeline** (AC: #3)
  - [ ] In `BarbershopApi.Tests`, write an xUnit test using `WebApplicationFactory<Program>` against a fresh temporary SQLite file (never the dev DB — AD-10) that asserts the app boots and `Database.Migrate()` completes without throwing
  - [ ] This is the only backend test this story needs — there's no domain logic yet to test; Stories 1.2+ add repository/service tests against real behavior
- [ ] **Task 11: Verify CI is green end-to-end**
  - [ ] Push the scaffold on a short-lived branch (`story/1.1-project-scaffold-ci-pipeline`, per the project's branching convention) and confirm both CI jobs pass before merging

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

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created

### File List
