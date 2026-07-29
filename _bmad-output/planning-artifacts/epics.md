---
stepsCompleted: [requirements-extraction, epic-design, epic-1-stories, epic-2-stories, epic-3-stories, all-stories-generated, final-validation]
inputDocuments:
  - "_bmad-output/planning-artifacts/prds/prd-bmad-learning-project-2026-07-21/prd.md"
  - "_bmad-output/planning-artifacts/prds/prd-bmad-learning-project-2026-07-21/addendum.md"
  - "_bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/ARCHITECTURE-SPINE.md"
  - "_bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/SOLUTION-DESIGN.md"
  - "_bmad-output/planning-artifacts/ux-designs/ux-bmad-learning-project-2026-07-23/DESIGN.md"
  - "_bmad-output/planning-artifacts/ux-designs/ux-bmad-learning-project-2026-07-23/EXPERIENCE.md"
---

# Barbershop Appointment Scheduler - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for the Barbershop Appointment Scheduler, decomposing the requirements from the PRD, UX Design, and Architecture into implementable stories.

## Requirements Inventory

### Functional Requirements

**Authentication & Accounts**
- FR1: Any visitor can self-register a customer account (email, password ×2, first name, last name). Duplicate email rejected with error; email is the unique account key. Email must be a plausible address (at minimum one `@` and a domain with a `.`) or it's rejected with an error — applies everywhere email is collected/edited: FR1, FR18, FR19. Mismatched password confirmation rejects with "passwords do not match," clearing only the two password fields (applies everywhere a password is typed twice: FR1, FR18, FR19, FR28).
- FR2: Registered users sign in with email/password (hashed storage). Failed attempts (bad email or bad password) return the same generic "Invalid email or password" error — no user enumeration.
- FR3: Unauthenticated users cannot reach booking, barber dashboard, or admin pages; nav items for inaccessible pages are hidden entirely, not just blocked after the fact. Signed-in users hitting a page/action outside their role are rejected server-side the same way.
- FR4: On sign-in, customers land on Schedule Appointment; barbers/admins land on their schedule view.
- FR23: Any signed-in user can sign out; logout ends the session server-side across all tabs/devices for that account.
- FR28: On the Account page, a signed-in user edits their own first name, last name, and password (×2) — not email. Self password change does not end their own session. Requires an explicit confirm step.
- FR29: Nav top-right: signed-out shows Login/Register; signed-in shows a profile icon dropdown (Account, Logout).
- FR31: On first startup, if no admin account exists, one is created from server-side configuration (not the registration UI) — the only admin account that will ever exist (see FR34).
- FR35: Admin-driven password change on another account immediately terminates all that account's active sessions. Admin-driven permission-level change does not force-end sessions — a page refresh is enough to pick up the new role.

**Booking (shared by all roles)**
- FR5: Any signed-in user can access Schedule Appointment. A signed-out user clicking a booking CTA is redirected to Login.
- FR6: User selects a barber; if none exist, selector shows explicit "No barbers available."
- FR7: User selects a date via calendar widget — past dates disabled, future capped at 30 days out, weekends not selectable (shop closed Sat/Sun).
- FR8: Time dropdown shows only open slots for the chosen barber/date (fixed 9:00 AM–4:30 PM, 30-min increments); for today, slots within 30 minutes of current time are excluded.
- FR9: Submitting creates the appointment and transitions to a confirmation screen, structurally preventing duplicate submission. A signed-in user cannot hold two appointments at the same date/time across different barbers.
- FR10: A submission for a slot taken between load and submit is rejected with an on-screen error; backend does not create a duplicate booking or crash (double-booking guard).
- FR24: Schedule Appointment lists the signed-in user's own upcoming (not-yet-occurred) appointments at the bottom. Past appointments are retained in the DB but never shown in this list.
- FR25: From that list, a user can cancel one of their own appointments, freeing the slot.

**Barber Dashboard**
- FR11: On sign-in, barber lands on their own schedule, defaulted to today.
- FR12: Back/forward arrows navigate to other days.
- FR13: View lists the full fixed time range; booked slots show the customer's name, open slots show as available. Weekend days show as closed.
- FR14: Barber sees only their own appointments, enforced server-side.
- FR26: From their schedule view, a barber can cancel any appointment shown there, freeing the slot.

**Admin Dashboard & Account Management**
- FR15: Admin lands on the same schedule view as barbers, plus a Select Barber dropdown defaulting to the first barber (never an empty state).
- FR16: A separate Admin Panel hosts account management.
- FR17: Admin can search for a customer or barber account by name or email (partial match), then select from results. The single admin account is not part of this searchable/manageable set.
- FR18: Admin can edit any field of a selected customer/barber account — email, first name, last name, permission level, or password (×2). No account can be promoted to admin. Duplicate email edit rejected; email must also be a plausible address (FR1). Requires explicit confirm step. Demoting a barber to customer cancels and deletes that barber's future appointments; past/Finished appointments retained as history.
- FR19: Admin can create new barber accounts via a Create Account button (password ×2). Creates barber accounts only — never another admin. Email must be a plausible address, same rule as FR1.
- FR27: From their schedule view (including via Select Barber), an admin can cancel any appointment the same way a barber does.
- FR30: Every cancellation (FR25–FR27) requires an explicit confirm step. Cancellation is transactional and idempotent — a second cancellation attempt on an already-cancelled appointment is rejected with an error, not a silent success or crash. Same handling applies to a cancellation racing a new booking for the freed slot.
- FR34: There is exactly one admin account, acting as the shop's owner (created per FR31); it can never be promoted-to, demoted-from, or deleted — Admin Panel edit/delete/permission actions (FR17–FR19, FR40) apply only to customer and barber accounts.
- FR40: Admin can delete a customer or barber account from the Admin Panel, gated behind an explicit confirm step. Deleting a barber cancels and deletes that barber's future appointments; past/Finished appointments retained as history (same handling as FR18 demotion).
- FR41: Concurrent edits/deletes targeting the same account are handled transactionally — first to commit succeeds, second gets a conflict error.

**Shared Pages**
- FR20: Home page includes hero, tagline, CTA, and at least one hover interaction.
- FR21: About page (static content).
- FR22: Layout is responsive and renders cleanly across mobile and desktop viewport widths.

### NonFunctional Requirements

- NFR1 (Security): Passwords stored via salted hashing, never plaintext; role checks enforced server-side, not just hidden in UI; sessions maintained securely (mechanism per Architecture); login rate-limited against brute-force; all dates/times interpreted/compared in fixed EST (server-authoritative).
- NFR2 (Data integrity): Booking writes are transactional enough that near-simultaneous submissions for the same slot can't both succeed; same guarantee extends to cancellations (cancel-vs-cancel, cancel-vs-book races) and account edits/deletes (edit-vs-edit, edit-vs-delete races) — first commit wins, never silent corruption.
- NFR3 (Responsiveness): Layout adapts cleanly across mobile and desktop widths; no hover-dependent action lacks a working single-tap touch equivalent.
- NFR4 (Automated testing): Test suite covers DB CRUD, auth, and role permissions against a real (not mocked) SQLite instance.
- NFR5 (CI/CD): CI pipeline runs the full suite on every push; a red pipeline means the codebase is not currently deployable, by design.
- NFR6 (Maintainability): Code organized by responsibility (one controller/service per role area or domain concept, no god-classes) — assessed via manual code review, not automatable.
- NFR7 (Deployment target): Runs locally only; no production hosting or public deploy target required.

### Additional Requirements

**Scaffolding / starter template (impacts Epic 1 Story 1):**
- Backend scaffold: `dotnet new webapi --use-controllers` (.NET 10 ASP.NET Core Web API — controllers, not minimal APIs).
- Frontend scaffold: Vite 8.1.5 official React JS template (plain JavaScript, no TypeScript).
- Structural seed to create at scaffold time: `backend/BarbershopApi/{Controllers,Services,Repositories,Entities,Dtos,Data}` + `backend/BarbershopApi.Tests/`; `frontend/src/{pages,components,api,styles}`; `.github/workflows/ci.yml`.

**Backend layering & domain structure (AD-1):**
- Controllers → Services → Repositories, one-way dependency only, never reversed or skip-level.
- One Controller/Service/Repository trio per domain concept — Auth, Booking, Account/Admin — not per entity, not a shared catch-all class.

**Auth & session mechanics (AD-2, AD-3):**
- Every protected endpoint independently re-derives Role + SessionVersion from the DB per request — never trusts the JWT role claim as-is.
- Access token: JWT, 60-min expiry, held in memory only, sent as `Authorization: Bearer`.
- Refresh token: JWT, 15-day expiry, carries SessionVersion, lives in an HttpOnly+Secure+SameSite=Strict cookie, never read by JS.
- `POST /api/auth/refresh` validates SessionVersion and mints a new access token; called on access-token expiry and on every fresh page load.
- `GET /api/auth/me` returns exactly `{ id, email, firstName, lastName, role }` — the one shared "who am I" shape.
- Fixed status codes: 401 (unauthenticated / session-invalid), 403 (authenticated, wrong role) — never invented ad hoc per controller.
- `Role` is a fixed C# enum (`Customer` | `Barber` | `Admin`, PascalCase), one shared type referenced everywhere.

**Testing strategy (AD-4):**
- Backend: xUnit.v3 + WebApplicationFactory against a real SQLite instance (never mocked), isolated from the dev DB.
- Frontend: Vitest + jsdom + React Testing Library + user-event, stubbing API calls via `vi.fn()`/`vi.spyOn(fetch)` — no MSW.
- Playwright e2e optional, mocks nothing.

**Login rate limiting (AD-5):**
- `Microsoft.AspNetCore.RateLimiting`, sliding window, 5 attempts per email+IP per 15-minute window on `/api/auth/login`; over-limit returns 429 with the same generic invalid-credentials message as a normal failed login.

**First-admin bootstrap (AD-6):**
- A single `IHostedService` runs after `Database.Migrate()`, seeding exactly one admin account via `PasswordHasher<T>` if none exists.
- Credentials come only from environment variables (`AdminSeed__Email`/`AdminSeed__Password`) — never `dotnet user-secrets`; `appsettings.json` keeps only empty placeholder keys.

**Data model & integrity (AD-7 through AD-9, AD-15–AD-17):**
- All entity primary keys are `int` auto-increment — never GUID/UUID.
- "Finished" appointment status is computed at read time (Date/StartTime vs. current EST "now"), never persisted. Cancellation is the only real state change: nullable `CancelledAt` (soft-delete, row retained for history). "Cancels and deletes" (FR18/FR40 cascades) means this same soft-cancel — never a hard row delete, regardless of trigger.
- Double-booking guard: application-level check-then-insert inside a transaction, plus two DB-level partial unique indexes as backstop: `UNIQUE(BarberId, Date, StartTime) WHERE CancelledAt IS NULL` and `UNIQUE(CustomerId, Date, StartTime) WHERE CancelledAt IS NULL`.
- Account soft-delete: nullable `DeletedAt` (same shape as `CancelledAt`), row retained forever. `UNIQUE(Email) WHERE DeletedAt IS NULL` — a deleted account's email becomes registerable again immediately. A deleted account can never sign in.
- Account optimistic concurrency: EF Core concurrency token (`RowVersion`/`[Timestamp]`) — first commit wins, second gets a 409 conflict via `ProblemDetails`.
- Single shared read path: customer's own list, barber's own schedule, and admin's oversight view all read through one shared `BookingService` method/read-model, including the Finished computation — never duplicated per-Controller.

**Dev/CI database isolation (AD-10, AD-11):**
- Dev SQLite file at `backend/BarbershopApi/App_Data/barbershop.db`, gitignored; only `Migrations/` (code) committed. Dev DB starts empty via `Database.Migrate()` on startup — no seeded sample data.
- CI tests run against their own separate temp SQLite instance via `WebApplicationFactory`.
- One GitHub Actions workflow runs on every push, with parallel jobs for the .NET suite and the frontend suite; a red pipeline is not mergeable.

**Timezone semantics (AD-12):**
- "EST" means US Eastern Time (`America/New_York`), DST-aware, computed server-side. Wire format: plain `yyyy-MM-dd` / `HH:mm` strings, no offset — client never does timezone math.

**CORS (AD-13):**
- API's CORS policy explicitly allows the Vite dev-server origin with `AllowCredentials()`; every frontend fetch touching auth sets `credentials: 'include'`.

**Server-side booking re-validation (AD-14):**
- On every booking submission, the server independently re-checks: not in the past, weekday only, within the 30-day forward cap, and (same-day) not within 30 minutes of current EST time. Client-side disabling is a UX convenience only, never the enforcement point.

**Client-side routing (AD-18):**
- React Router (current major, v7+ — confirm exact package name at scaffold time). Route guards call `GET /api/auth/me` to determine identity/role and redirect on unauthorized/wrong-role access; hiding a nav item is a UX nicety layered on top, never the enforcement itself.

**Cross-cutting conventions:**
- Errors: ASP.NET Core's built-in `ProblemDetails` (RFC 7807) — automatic for `[ApiController]` validation, `Problem()` helper for custom errors (booking conflict, stale cancellation, account-edit conflict). One consistent error envelope.
- Naming: PascalCase for C# types/methods/properties; camelCase for JSON payloads and JS/React code.
- UI dependency pins to verify/re-check at scaffold time: `@radix-ui/react-dialog` 1.1.21, `@radix-ui/react-select` 2.3.4, `@radix-ui/react-popover` 1.1.16, `react-day-picker` 10.0.1 (recent Radix patches fixed real React-19 re-render bugs — re-verify still current).

**Explicitly deferred (not in scope for this build):**
- Refresh-token rotation & reuse-detection (accepted trade-off).
- Guest (unauthenticated) booking.
- Dev database seeding with sample data.

### UX Design Requirements

- UX-DR1: Implement the design token system per `DESIGN.md` frontmatter — colors (primary `#0E7C9B`, primary-hover `#0B5F79`, destructive `#C93A3A`, destructive-hover `#A83030`, background `#FFFFFF`, neutral `#EFF6F8`, border `#D3E4E9`, text `#17242A`, text-muted `#5B7480`), typography scale (Manrope; display/h1/h2/h3/body/body-sm/label/caption), rounded scale (sm 4px / DEFAULT+md 6px / lg+xl 8px / full), and spacing scale (4px-base steps 1–16 plus gutter-mobile/gutter-desktop/content-max-width).
- UX-DR2: Build the Button component with primary/secondary/destructive variants (`{components.button-primary}` / `-secondary` / `-destructive`); hover/active color swap fires on pointer devices only; every button is a single, complete tap on touch (no double-tap-to-arm, no press-and-hold).
- UX-DR3: Build the Nav bar (`{components.nav-bar}`) — role-based link visibility (My Schedule hidden unless barber/admin, Admin Panel hidden unless admin) via full DOM removal, not `display:none`; active link marked via the active-link token; signed-out shows Sign In + Register, signed-in shows a profile-icon dropdown (Account, Logout).
- UX-DR4: Build the Footer (`{components.footer}`) — static, identical on every page regardless of role or auth state: wordmark, address, phone, hours, copyright line, no links/social icons.
- UX-DR5: Build the Form-section card (`{components.form-section}`) — tinted container wrapping the Schedule Appointment booking form and reused on Account/Login/Register pages.
- UX-DR6: Build the Input component (`{components.input}`) with focus-state border swap to primary; implement the double-entry password field pattern (Register, Account, Admin edit-password, Admin create-account) where a mismatch shows plain-text "Passwords do not match" and only the two password fields clear for retyping.
- UX-DR7: Restyle the Radix Calendar/date-picker (`{components.calendar}`) — disabled past/weekend/>30-day dates visibly distinct and excluded from tab focus (not merely unclickable); selected day is a solid primary fill; today is indicated by primary text only (no fill).
- UX-DR8: Restyle Radix Select dropdowns — the customer-facing variant (`{components.select-dropdown}`, barber-select and time-slot-select) and the admin-only barber-select variant (`{components.select-dropdown-admin-barber}`) which carries the floating shadow at rest as a deliberate exception.
- UX-DR9: Build the Modal/Dialog wrapper via Radix (`{components.modal}`) used for the admin account-edit/create popup and the confirm-action popup — floating shadow, overlay scrim.
- UX-DR10: Build the Confirm-action popup (`{components.confirm-popup}`) — exactly two buttons every time: "Go Back" (secondary, always neutral regardless of context) and "Confirm" (primary color for a non-destructive action, destructive color for a destructive action); `Esc`/outside-click/"Go Back" all dismiss with zero effect; enforce one-level popup stacking only (the admin edit/create popup may have a confirm popup on top, never two confirm popups nested).
- UX-DR11: Build the Schedule row components — open slot (`{components.schedule-row-open}`, "Available" text, no action) and booked slot (`{components.schedule-row-booked}`, customer name + destructive Cancel button) — and reuse the booked-row treatment row-for-row in the My Appointments list.
- UX-DR12: Build the Admin account row (`{components.admin-account-row}`) — clickable, tinted, hover-darkened, opens the edit popup.
- UX-DR13: Build the Admin account-edit popup (email, first name, last name, permission dropdown restricted to customer/barber, optional double-entry password — blank keeps current password) and the Admin account-create popup (email, first name, last name, required double-entry password, no permission selector — always creates a barber account).
- UX-DR14: Build the Date-nav-arrow component (`{components.date-nav-arrow}`) for My Schedule day-stepping, with resting/hover/disabled states, 20px icon-only.
- UX-DR15: Build the Confirmation screen (`{components.confirmation-screen}`) — a full-page replacement (not a popup) of the booking form on successful submit, copy pattern "Appointment booked with {barber} at {time} on {date}," no celebratory iconography.
- UX-DR16: Build the Home hero — diagonal split between a white half (headline + primary CTA) and a primary-teal half (scissor-and-comb graphic); the CTA redirects signed-out users to Login and signed-in users straight to Schedule Appointment.
- UX-DR17: Implement every named State Pattern from `EXPERIENCE.md` with its specified default copy: cold-load "Loading…" placeholders (Schedule Appointment, My Schedule), registration-success redirect-to-Login banner, account-save-success banner, no-barbers-available, double-booking-race error, self-double-booking error, My-Appointments-empty, weekend/shop-closed, stale-cancel conflict, cancellation-double-attempt, login error, login rate-limited message, duplicate-email error, signed-out-hits-protected-surface redirect, wrong-role direct-URL-access redirect, password-mismatch message, concurrent-account-edit-conflict message, admin-driven password-change/permission-change session effects, and barber-demotion/account-deletion cascade.
- UX-DR18: Implement the accessibility floor — WCAG 2.2 AA across the whole app; full keyboard operability for every custom-built interactive element (Tab focus order, Enter/Space activation, visible focus ring) alongside what Radix already provides for free on the three Radix-backed surfaces; role-based nav hiding must be real DOM removal; no state relies on color alone.
- UX-DR19: Implement responsive breakpoint behavior — desktop ≥1024px (nav inline, content capped at 1120px), tablet 640–1023px (narrowed gutters, nav collapses to a menu button only once links would wrap), mobile <640px (single-column stacking throughout, no hover-only affordances).
- UX-DR20: Resolve two flagged open UX items before building the components that depend on them — owner UX (Sally), per Architecture's Deferred section: (a) a form-validation/error color for password-mismatch states (interim default is plain text, no color), and (b) the exact tablet breakpoint pixel value (currently named, not sized).

### FR Coverage Map

FR1: Epic 1 - Customer self-registration
FR2: Epic 1 - Sign in with email/password
FR3: Epic 1 - Role-gated page/action access, nav hidden per role
FR4: Epic 1 - Post-sign-in landing routed by role
FR20: Epic 1 - Home page hero/CTA/hover
FR21: Epic 1 - About page (static)
FR22: Epic 1 - Responsive layout, mobile/desktop
FR23: Epic 1 - Sign out ends session server-side, all tabs/devices
FR28: Epic 1 - Self-service Account page edit (name, password)
FR29: Epic 1 - Nav auth-state area (Login/Register vs. profile dropdown)
FR31: Epic 1 - First-admin bootstrap on startup
FR5: Epic 2 - Signed-in access to Schedule Appointment; signed-out redirect to Login
FR6: Epic 2 - Barber selector, "No barbers available" state
FR7: Epic 2 - Date selection via calendar (past/weekend/30-day rules)
FR8: Epic 2 - Time dropdown, open slots only, same-day 30-min cutoff
FR9: Epic 2 - Booking submit → confirmation screen; blocks self-double-booking
FR10: Epic 2 - Double-booking guard on submit
FR11: Epic 2 - Barber lands on own schedule, defaulted to today
FR12: Epic 2 - Schedule day navigation (back/forward arrows)
FR13: Epic 2 - Schedule view: full time range, booked vs. open, weekend closed
FR14: Epic 2 - Barber sees only own appointments (server-enforced)
FR15: Epic 2 - Admin schedule view + Select Barber dropdown
FR24: Epic 2 - My Appointments list (upcoming only)
FR25: Epic 2 - Customer cancels own appointment
FR26: Epic 2 - Barber cancels appointment from schedule view
FR27: Epic 2 - Admin cancels appointment from schedule view
FR30: Epic 2 - Cancellation requires confirm step; idempotent/transactional
FR16: Epic 3 - Admin Panel hosts account management
FR17: Epic 3 - Admin searches accounts by name/email (partial match)
FR18: Epic 3 - Admin edits any customer/barber account field; demotion cascade
FR19: Epic 3 - Admin creates new barber accounts
FR34: Epic 3 - Exactly one admin account, never promotable/demotable/deletable
FR40: Epic 3 - Admin deletes customer/barber account; deletion cascade
FR41: Epic 3 - Concurrent account edit/delete conflict handling
FR35: Epic 3 - Admin password change force-ends target sessions; permission change does not

## Epic List

### Epic 1: Account Access & Site Foundation

Any visitor can discover the shop (Home, About), self-register or sign in, and every page thereafter respects who's signed in — secure sessions, role-gated navigation, and self-service profile editing — on top of a fully scaffolded, CI-wired, design-system-backed codebase.
**FRs covered:** FR1, FR2, FR3, FR4, FR20, FR21, FR22, FR23, FR28, FR29, FR31

### Epic 2: Appointment Booking & Schedule Management
A signed-in customer can book, view, and cancel their own appointments against real, race-safe availability; barbers see their own day, admins see any barber's day (via Select Barber), and both can cancel appointments shown there — one shared, correctness-first scheduling engine powering all three views.
**FRs covered:** FR5, FR6, FR7, FR8, FR9, FR10, FR11, FR12, FR13, FR14, FR15, FR24, FR25, FR26, FR27, FR30

### Epic 3: Admin Account Management
The admin can search, create, edit, and delete customer/barber accounts from a dedicated panel — including safely demoting/removing barbers with their future appointments cascade-cancelled — without ever touching the database directly.
**FRs covered:** FR16, FR17, FR18, FR19, FR34, FR35, FR40, FR41

## Epic 1: Account Access & Site Foundation

Any visitor can discover the shop (Home, About), self-register or sign in, and every page thereafter respects who's signed in — secure sessions, role-gated navigation, and self-service profile editing — on top of a fully scaffolded, CI-wired, design-system-backed codebase.

### Story 1.1: Project Scaffold, CI Pipeline, and Design System Foundation

As a developer,
I want the backend and frontend scaffolded, CI wired up, and the core design system in place,
So that every later story has a working, tested, styled foundation to build on.

**Acceptance Criteria:**

**Given** a fresh clone
**When** the backend is scaffolded via `dotnet new webapi --use-controllers` and the frontend via Vite's official React JS template
**Then** the folder structure matches the Architecture's structural seed (`Controllers/Services/Repositories/Entities/Dtos/Data` and `frontend/src/{pages,components,api,styles}`)

**Given** the scaffold
**When** a GitHub Actions workflow is added
**Then** it runs the .NET suite and the frontend suite as parallel jobs on every push (NFR5, AD-11)

**Given** the dev environment
**When** the app starts
**Then** the SQLite file lives at `backend/BarbershopApi/App_Data/barbershop.db` (gitignored, only `Migrations/` committed) and `Database.Migrate()` runs cleanly against an empty DB (AD-10)

**Given** the API and Vite dev server run on different ports
**When** CORS is configured
**Then** it explicitly allows the Vite origin with `AllowCredentials()` (AD-13)

**Given** `DESIGN.md`'s tokens
**When** the design system is implemented
**Then** colors, typography, rounding, and spacing scales (UX-DR1) are available as reusable tokens, and the Button (primary/secondary/destructive), Input, Nav bar shell, Footer, and Modal/Confirm-popup components (UX-DR2–6, 9, 10) render correctly in isolation, with hover firing on pointer devices only and every action completing on a single tap on touch

**Given** the two open UX items flagged in Architecture's Deferred section
**When** the design foundation is built
**Then** a form-validation/error treatment (plain-text, no dedicated color per DESIGN.md's interim default) and a concrete tablet breakpoint pixel value are settled before Register/Account components are built (UX-DR20)

### Story 1.2: Account Entity & Repository

As a developer,
I want the `Account` entity, its migration, and every repository method Epic 1's stories will need,
So that registration, sign-in, admin bootstrap, and self-service editing can all be built as pure business logic on top of a working, tested data layer — no schema changes after this story.

**Acceptance Criteria:**

**Given** no `Account` entity exists yet
**When** this story is implemented
**Then** the entity/migration is created with `int` auto-increment PK (AD-7), `Email` (unique, scoped to non-deleted rows — `UNIQUE(Email) WHERE DeletedAt IS NULL`, AD-15), `PasswordHash`, `FirstName`, `LastName`, `Role` (fixed `Customer`/`Barber`/`Admin` enum, AD-2), `SessionVersion` (int, AD-3), `DeletedAt` (nullable, AD-15), and `RowVersion` (EF Core concurrency token, AD-16)

**Given** the entity, **When** the `AccountRepository` is built, **Then** it exposes every method Epic 1 needs: `Create`, `FindByEmail` (excluding soft-deleted rows), `FindById`, and `Update` (with optimistic concurrency via `RowVersion`)

**Given** the repository, **When** tested, **Then** every method is covered by xUnit + `WebApplicationFactory` tests against a real (temp) SQLite instance — never mocked (NFR4, AD-4)

**Given** this story is complete, **When** Stories 1.4, 1.5, and 1.7 are built, **Then** they add only business logic (Controller/Service) on top of this repository — no further schema changes for anything Epic 1 needs (AD-1 layering)

### Story 1.3: Home and About Pages

As a visitor,
I want to see the Home page (hero, tagline, CTA) and a static About page,
So that I can learn about the shop before signing up.

**Acceptance Criteria:**

**Given** a signed-out visitor on Home
**When** the page loads
**Then** the hero renders with the diagonal white/primary-teal split, headline, tagline, and a "Schedule Appointment" CTA, with at least one hover interaction on desktop (FR20, UX-DR16)

**Given** a signed-out visitor
**When** they click the Home CTA
**Then** they are redirected to Login

**Given** a signed-in visitor
**When** they click the Home CTA
**Then** they are routed toward the booking flow (the destination page itself is built in Epic 2; this story only wires the routing decision)

**Given** the About page
**When** visited
**Then** it renders static shop content (location, phone, hours, barber list) (FR21)

**Given** any viewport width
**When** Home or About renders
**Then** the layout adapts cleanly with no broken/overflowing elements on mobile or desktop (FR22)

### Story 1.4: Customer Self-Registration

As a visitor,
I want to self-register a customer account with my email, a password (typed twice), first name, and last name,
So that I can access booking features.

**Acceptance Criteria:**

**Given** a not-yet-used email and matching passwords
**When** submitted on Register
**Then** a new Account row is created with `Role=Customer` and a hashed password (`PasswordHasher<T>`), and the user is redirected to Login with "Account created. Sign in to continue." displayed

**Given** an email already registered
**When** submitted
**Then** registration is rejected with an error directing the user to a different email, and the email field is retained (FR1)

**Given** an email with no `@` or no domain `.` (e.g. "testbademail")
**When** submitted
**Then** registration is rejected with an error and the email field is retained (FR1)

**Given** mismatched password/confirm-password fields
**When** submitted
**Then** "Passwords do not match" is shown, only the two password fields clear, and all other entered fields are retained (FR1)

**Given** the Register form
**When** rendered
**Then** it uses the double-entry password Input pattern inside the tinted `{components.form-section}` card (UX-DR5, UX-DR6)

### Story 1.5: Sign In, Sign Out, and First-Admin Bootstrap

As a registered user,
I want to sign in with my email/password and sign out when done,
So that I can securely access my account; and as the shop owner, I want an admin account seeded automatically on first startup, so that I never need a manual backdoor to get one.

**Acceptance Criteria:**

**Given** valid credentials
**When** submitted on Login
**Then** the user is authenticated (access token in memory, refresh token in an HttpOnly+Secure+SameSite=Strict cookie) and routed per FR4 (customer → Schedule Appointment route, barber/admin → My Schedule route) — the destination pages themselves are built in Epic 2 (Stories 2.2 and 2.5); this story only wires the routing decision

**Given** invalid credentials (unregistered email or wrong password)
**When** submitted
**Then** the same generic "Invalid email or password" error is shown in both cases, with no indication of which was wrong (FR2)

**Given** repeated failed login attempts for the same email+IP
**When** a 6th attempt is made within the trailing 15-minute sliding window
**Then** the API returns 429 and the on-screen copy reads "Too many attempts. Try again in a few minutes." — a deliberate divergence from AD-5's "identical message" wording, per product decision, trading a small enumeration-resistance gap for a clearer user-facing signal

**Given** a signed-in user
**When** they open the profile-icon dropdown and click Logout
**Then** their session ends server-side and every open tab/device for that account is signed out immediately (FR23)

**Given** no admin account exists yet
**When** the app starts for the first time
**Then** exactly one admin account is created from `AdminSeed__Email`/`AdminSeed__Password` environment variables via an `IHostedService` running after `Database.Migrate()` (FR31, AD-6)

**Given** the nav bar
**When** a user is signed out
**Then** Login/Register buttons show
**And** when signed in, a profile-icon dropdown (Account, Logout) shows instead (FR29)

### Story 1.6: Server-Side Role Gating & Protected Routing

As a signed-in user,
I want pages and actions outside my role rejected server-side and hidden from navigation,
So that the app can't be tricked into exposing something I shouldn't see.

**Acceptance Criteria:**

**Given** an unauthenticated request to any protected endpoint
**When** received
**Then** the API returns 401

**Given** an authenticated request to an endpoint outside the caller's current role
**When** received
**Then** the API returns 403, with role re-derived from the database on that same request (never trusted from the JWT claim) (FR3, AD-2)

**Given** a signed-in user's role
**When** the nav bar renders
**Then** links to pages outside that role are removed entirely from the DOM and tab order, not merely hidden via CSS (FR3, UX-DR18)

**Given** a signed-in user manually navigates to a URL outside their role
**When** the route guard calls `GET /api/auth/me`
**Then** they are redirected away rather than shown the page content (AD-18)

**Given** an expired access token or a fresh page load
**When** the frontend calls `POST /api/auth/refresh`
**Then** a new access token is issued as long as the refresh token's `SessionVersion` still matches the database (AD-3)

### Story 1.7: Self-Service Account Management

As a signed-in user,
I want to edit my own first name, last name, and password from an Account page,
So that I can keep my profile current without needing an admin.

**Acceptance Criteria:**

**Given** the Account page
**When** a signed-in user edits first name, last name, and/or password (double-entry)
**Then** a confirm-action popup appears before the change takes effect (FR28)

**Given** a confirmed save
**When** it completes
**Then** "Changes saved." appears above the form and the user's current session continues uninterrupted — self password changes never bump `SessionVersion` or force a re-login (FR28)

**Given** mismatched password/confirm fields
**When** submitted
**Then** "Passwords do not match" is shown and only those two fields clear

**Given** the Account page
**When** rendered
**Then** email is displayed but is not editable (FR28)

## Epic 2: Appointment Booking & Schedule Management

A signed-in customer can book, view, and cancel their own appointments against real, race-safe availability; barbers see their own day, admins see any barber's day (via Select Barber), and both can cancel appointments shown there — one shared, correctness-first scheduling engine powering all three views.

### Story 2.1: Appointment Entity & Repository

As a developer,
I want the `Appointment` entity, its migration (including the double-booking guard indexes), and every repository method Epic 2's stories will need,
So that booking, cancellation, and all three schedule views can be built as pure business logic on top of a working, tested data layer.

**Acceptance Criteria:**

**Given** no `Appointment` entity exists yet
**When** this story is implemented
**Then** the entity/migration is created with `int` auto-increment PK (AD-7), `CustomerId` FK, `BarberId` FK, `Date`, `StartTime`, `CancelledAt` (nullable, soft-cancel per AD-8) — plus the two DB-level partial unique indexes as the double-booking backstop: `UNIQUE(BarberId, Date, StartTime)` and `UNIQUE(CustomerId, Date, StartTime)`, both `WHERE CancelledAt IS NULL` (AD-9)

**Given** the entity
**When** the `AppointmentRepository`/`BookingService` is built
**Then** it exposes every method Epic 2 needs: `Create` (book, inside a transaction with check-then-insert per AD-9), `FindByBarberAndDate` (barber/admin schedule views), `FindUpcomingByCustomer` (My Appointments), and `Cancel` (soft-cancel via `CancelledAt`, idempotent) — all through the single shared read/write path AD-17 requires, never duplicated per role

**Given** "Finished" status
**When** any read method returns appointments
**Then** it's computed at read time from `Date`/`StartTime` vs. current EST "now" — never persisted as a column (AD-8, AD-12)

**Given** the repository
**When** tested
**Then** every method — including the double-booking race and soft-cancel idempotency — is covered by xUnit + `WebApplicationFactory` against a real (temp) SQLite instance, never mocked (NFR4, AD-4)

**Given** this story is complete
**When** Stories 2.2–2.6 are built
**Then** they add only business logic and UI on top of this repository — no further schema changes for anything Epic 2 needs

### Story 2.2: Customer Books an Appointment

As a signed-in customer,
I want to select a barber, date, and time and submit a booking,
So that I get a confirmed appointment.

**Acceptance Criteria:**

**Given** the Schedule Appointment page
**When** a signed-in user visits
**Then** the booking form renders with barber/date/time fields unselected (FR5)

**Given** no barber accounts exist
**When** the barber selector loads
**Then** it shows "No barbers available" instead of an empty or broken dropdown (FR6)

**Given** the calendar widget
**When** opened
**Then** past dates, weekends, and dates beyond 30 days out are all disabled (FR7)

**Given** a selected barber/date
**When** the time dropdown loads
**Then** only open slots (9:00 AM–4:30 PM, 30-min increments) not already booked appear; if the date is today, slots within 30 minutes of current EST time are excluded (FR8, AD-12)

**Given** barber, date, and time all selected
**When** Submit is clicked
**Then** the appointment is created under the signed-in user's account and the booking form is replaced by a full-page confirmation screen reading "Appointment booked with {barber} at {time} on {date}" (FR9, UX-DR15)

**Given** a signed-out visitor
**When** they click a booking CTA
**Then** they are redirected to Login (FR5)

### Story 2.3: Double-Booking & Self-Conflict Guards

As a customer submitting a booking,
I want the system to reject a slot that's already taken — by me or anyone else — between page-load and submit,
So that I never end up with two conflicting appointments.

**Acceptance Criteria:**

**Given** two near-simultaneous submissions for the same barber/date/time
**When** both are submitted
**Then** only one succeeds; the second gets an on-screen error ("That time is no longer available. Choose another."), retains the barber/date selections, and the time dropdown re-queries current availability (FR10)

**Given** a signed-in customer already holding an appointment at a given date/time with a different barber
**When** they try to book another appointment at that same date/time
**Then** it's blocked the same way, with an equivalent on-screen error (FR9)

**Given** any booking attempt
**When** processed
**Then** an application-level check-then-insert runs inside a transaction, backed by the two DB-level partial unique indexes from Story 2.1 (AD-9)

**Given** a booking submission
**When** received
**Then** the server independently re-validates: not in the past, weekday only, within the 30-day cap, and (same-day) not within 30 minutes of current EST time — regardless of what the client already filtered (AD-14)

### Story 2.4: My Appointments — View, Cancel, and Race Safety

As a signed-in user,
I want to see my own upcoming appointments and cancel one safely,
So that I can manage my bookings without contacting the shop or worrying about a stale click.

**Acceptance Criteria:**

**Given** the Schedule Appointment page
**When** it loads
**Then** the signed-in user's own upcoming (not-yet-occurred) appointments list at the bottom, via the shared `BookingService` read path (FR24, AD-8, AD-17); past appointments stay in the DB but are never shown here

**Given** no upcoming appointments
**When** the list renders
**Then** it shows "No upcoming appointments."

**Given** an upcoming appointment in the list
**When** the user clicks Cancel
**Then** a confirm-action popup (destructive Confirm) appears before the cancellation takes effect (FR25, FR30)

**Given** a confirmed cancellation
**When** it completes
**Then** the appointment's `CancelledAt` is set (soft-cancel, never a hard delete) and the slot is immediately free for booking again (FR25, AD-8)

**Given** an appointment already cancelled by another actor (a race, not a user error)
**When** a second cancellation attempt is made on it
**Then** it's rejected with an on-screen error rather than a silent no-op or crash, and the view refreshes to the current, accurate state (FR30)

**Given** this cancel mechanism
**When** built
**Then** it's the single shared implementation every cancel path (customer, barber, admin) reuses in Stories 2.5/2.6 — never duplicated per role

### Story 2.5: Barber's Own Schedule View

As a barber,
I want to see my own day's schedule and cancel appointments from it,
So that I know who's coming in without digging through anyone else's calendar.

**Acceptance Criteria:**

**Given** a barber signs in
**When** they land on My Schedule
**Then** it defaults to today and only ever shows their own day (FR11)

**Given** the schedule view
**When** rendered
**Then** it lists every fixed 30-min slot from 9:00 AM–4:30 PM; booked slots show the customer's name, open slots show "Available" (FR13, UX-DR11)

**Given** a weekend date reached via the day-nav arrows
**When** viewed
**Then** it shows no bookable slot grid, consistent with the shop-closed rule (FR13, FR7)

**Given** the day-nav arrows
**When** clicked
**Then** the view steps one day at a time in either direction (FR12, UX-DR14)

**Given** a barber's schedule query
**When** executed
**Then** it returns only that barber's own appointments — enforced server-side, not just by the UI (FR14)

**Given** a booked slot on the barber's own schedule
**When** they click Cancel
**Then** it reuses Story 2.4's confirm-popup-then-soft-cancel flow, freeing the slot (FR26, FR30)

### Story 2.6: Admin Schedule Oversight

As an admin,
I want to view any barber's schedule via a Select Barber dropdown and cancel appointments from it,
So that I have full oversight without a separate tool.

**Acceptance Criteria:**

**Given** an admin signs in
**When** they land on My Schedule
**Then** they see the identical view a barber sees, plus a Select Barber dropdown defaulting to the first barber — never an empty state (FR15)

**Given** the admin switches the Select Barber dropdown
**When** a different barber is chosen
**Then** the same visible date re-renders for the newly selected barber — the date does not reset (FR15, UX-DR8 admin variant)

**Given** the admin's schedule view
**When** rendered
**Then** it reads through the exact same shared `BookingService` method used by the customer and barber views — never a separately-implemented admin-only query (AD-17)

**Given** a booked slot in the admin's current view
**When** they click Cancel
**Then** it reuses Story 2.4's confirm-popup-then-soft-cancel flow (FR27, FR30)

## Epic 3: Admin Account Management

The admin can search, create, edit, and delete customer/barber accounts from a dedicated panel — including safely demoting/removing barbers with their future appointments cascade-cancelled — without ever touching the database directly.

### Story 3.1: Account Repository — Admin Operations

As a developer,
I want the `Account` repository extended with the admin-only operations Epic 3's stories will need,
So that account search, admin-driven edit/create/delete, and the appointment-cascade on barber removal can all be built as pure business logic on top of a tested data layer.

**Acceptance Criteria:**

**Given** the `AccountRepository` built in Story 1.2
**When** extended for Epic 3
**Then** it exposes `Search` (partial match on name/email, excluding the admin account and soft-deleted rows), `AdminUpdate` (email/first/last/role/password — password change bumps `SessionVersion`, permission change does not), `AdminCreate` (always `Role=Barber`), and `SoftDelete` (sets `DeletedAt`)

**Given** FR34's invariant
**When** any admin operation targets the single admin account
**Then** it's rejected at the repository level — that account can never be edited, demoted, or deleted through these methods

**Given** a demote-to-customer or soft-delete on a barber account
**When** executed
**Then** it also cancels that barber's future (not-yet-occurred) appointments via Epic 2's `Cancel` mechanism — past/Finished appointments retained as history

**Given** concurrent admin operations on the same account (or an admin edit racing the holder's own self-edit from Story 1.7)
**When** two commits race
**Then** the `RowVersion` token (already on the entity from Story 1.2) rejects the second with a 409 conflict

**Given** these methods
**When** tested
**Then** they're covered by xUnit + `WebApplicationFactory` against a real SQLite instance, including the cascade and concurrency-conflict paths (NFR4, AD-4)

### Story 3.2: Admin Account Search

As an admin,
I want a dedicated Admin Panel where I can search for a customer or barber account by name or email,
So that I can quickly find the account I need to manage.

**Acceptance Criteria:**

**Given** the Admin Panel
**When** rendered
**Then** it hosts account search, results, and management as its own dedicated surface (FR16)

**Given** an admin enters a search query
**When** submitted
**Then** partial matches on name or email appear as clickable rows (FR17)

**Given** no query yet
**When** the panel first loads
**Then** it shows "Search by name or email to find an account."

**Given** a query with no matches
**When** searched
**Then** it shows "No accounts match your search."

**Given** the single admin account
**When** any search is run
**Then** it never appears as a result row (FR17, FR34)

### Story 3.3: Admin Edits an Account

As an admin,
I want to edit any customer or barber account's email, name, permission level, or password,
So that I can correct mistakes or manage staff without touching the database.

**Acceptance Criteria:**

**Given** an account row is clicked
**When** the edit popup opens
**Then** it shows editable email/first/last/permission(customer/barber only)/password (optional double-entry, blank = unchanged) fields (FR18)

**Given** a save
**When** confirmed via the confirm-action popup (non-destructive)
**Then** the change takes effect (FR18)

**Given** a password change via this popup
**When** saved
**Then** every active session for that account is immediately terminated, forcing re-sign-in (FR35)

**Given** a permission-level change via this popup
**When** saved
**Then** the account's existing sessions are not force-ended — a page refresh picks up the new role (FR35)

**Given** a duplicate email on save
**When** submitted
**Then** it's rejected with "That email is already in use." and the email field is retained (FR18)

**Given** an email with no `@` or no domain `.`
**When** submitted
**Then** it's rejected with an error and the email field is retained (FR18/FR1)

**Given** demoting a barber to customer
**When** saved
**Then** that barber's future appointments are cancelled and past ones retained as history (FR18)

**Given** two conflicting edits (two admin tabs, or an admin edit racing the holder's own self-edit)
**When** both are submitted
**Then** the first commit wins and the second gets "This account was changed elsewhere. Refresh and try again." (FR41)

### Story 3.4: Admin Creates a Barber Account

As an admin,
I want to create new barber accounts directly,
So that I can add staff without a self-registration flow.

**Acceptance Criteria:**

**Given** the Create Account button
**When** clicked
**Then** a create popup opens with email/first/last/password (double-entry, required) — no permission selector (FR19)

**Given** valid, non-duplicate input
**When** confirmed via the confirm-action popup
**Then** a new `Role=Barber` account is created (FR19)

**Given** a duplicate email
**When** submitted
**Then** it's rejected the same way as registration/edit

**Given** an email with no `@` or no domain `.`
**When** submitted
**Then** it's rejected the same way as registration/edit (FR19/FR1)

**Given** mismatched passwords
**When** submitted
**Then** "Passwords do not match" is shown, only those fields clear

### Story 3.5: Admin Deletes an Account

As an admin,
I want to delete a customer or barber account,
So that I can remove accounts that are no longer needed.

**Acceptance Criteria:**

**Given** an account in the edit popup
**When** Delete is clicked
**Then** a confirm-action popup (destructive Confirm) appears before the account is actually deleted (FR40)

**Given** a confirmed delete
**When** it completes
**Then** the account is soft-deleted (`DeletedAt` set) — never a hard row delete — and its email becomes registerable again immediately (FR40, AD-15)

**Given** a deleted barber account
**When** the deletion completes
**Then** that barber's future appointments are cancelled the same way a demotion cascades them; past appointments retained as history (FR40)

**Given** the single admin account
**When** any delete action is attempted against it
**Then** it's rejected (FR34)

**Given** a deleted account
**When** it attempts to sign in afterward
**Then** auth treats it identically to "account does not exist" (AD-15)
