---
name: 'Barbershop Appointment Scheduler'
type: architecture-spine
purpose: build-substrate
altitude: initiative
paradigm: 'Layered Architecture (Controller-Service-Repository)'
scope: 'Backend layering, auth/session mechanics, data model, testing/CI approach, and cross-cutting conventions for the Barbershop Appointment Scheduler (.NET/React/SQLite) — precedes epics/stories; no parent spine to inherit from.'
status: final
created: '2026-07-23'
updated: '2026-08-26'
binds: []
sources:
  - '{planning_artifacts}/prds/prd-bmad-learning-project-2026-07-21/prd.md'
  - '{planning_artifacts}/prds/prd-bmad-learning-project-2026-07-21/addendum.md'
  - '{planning_artifacts}/ux-designs/ux-bmad-learning-project-2026-07-23/DESIGN.md'
companions: ['SOLUTION-DESIGN.md']
---

# Architecture Spine — Barbershop Appointment Scheduler

## Design Paradigm

**Layered Architecture (Controller-Service-Repository).** Backend namespaces map directly to layers: `Controllers/` (HTTP surface, one per role/domain concept) → `Services/` (business rules, transactions) → `Repositories/` (EF Core/SQLite data access). One Controller/Service/Repository trio per domain concept — Auth, Booking, Account/Admin — not per entity and not a single catch-all. Dependencies flow one way only; see AD-1.

## Invariants & Rules

```mermaid
graph LR
    Controllers --> Services --> Repositories --> DB[(SQLite via EF Core)]
```

### AD-1 — Layered backend architecture

- **Binds:** all backend code; Auth, Booking, Account/Admin domains (NFR6)
- **Prevents:** cross-layer shortcuts (e.g., a controller querying EF Core directly), god-classes spanning multiple domains
- **Rule:** Controllers depend on Services depend on Repositories, one-way, never reversed or skip-level. One Controller/Service/Repository trio per role or domain concept (Auth, Booking, Account/Admin) — no shared catch-all classes.

### AD-2 — Role & session liveness enforced server-side per request

- **Binds:** all protected endpoints; FR3, FR14, FR35
- **Prevents:** trusting a JWT's role claim (stale after a permission change) or letting a revoked/password-changed session keep working; Controllers independently inventing inconsistent auth-failure status codes; `Role` string literals drifting in casing between call sites
- **Rule:** every protected endpoint re-derives the account's current Role from the DB per request (the same lookup also checks SessionVersion, so it's not an extra query); a JWT's SessionVersion claim is compared to the DB's current value on every protected request. Status codes are fixed: **401** for unauthenticated or session-invalid (missing/expired token, SessionVersion mismatch); **403** for authenticated-but-wrong-role. Only an admin-driven password change (FR35) increments SessionVersion; permission/role changes never do — role liveness is handled entirely by the DB re-check, not token invalidation. `Role` is a fixed enum — `Customer` | `Barber` | `Admin`, PascalCase — one shared type referenced everywhere (seeder, auth checks, DB storage), never an ad-hoc string literal.

### AD-3 — Token transport & refresh lifecycle

- **Binds:** all authenticated requests; FR2, FR23, FR35
- **Prevents:** storing tokens in localStorage or JS-readable cookies (XSS exposure); requiring full re-login on every access-token expiry
- **Rule:** access token (JWT, 60-min expiry) held in memory only, sent as `Authorization: Bearer`; refresh token (JWT, 15-day expiry, carries SessionVersion) lives in an HttpOnly+Secure+SameSite=Strict cookie, never read by JS. `POST /api/auth/refresh` validates SessionVersion and mints a new access token, called on access-token expiry and on every fresh page load; `GET /api/auth/me` bootstraps identity for the frontend since the cookie is unreadable, returning exactly `{ id, email, firstName, lastName, role }` — no other endpoint invents its own shape for "who am I." Refresh tokens are non-rotating — a stolen refresh cookie stays valid up to 15 days or until the next password change (rotation/reuse-detection deferred, see Deferred).

### AD-4 — Testing strategy

- **Binds:** NFR4; all
- **Prevents:** mocking the database layer in backend tests; adding a request-mocking framework for the frontend's small fetch surface
- **Rule:** backend tests use xUnit + WebApplicationFactory against a real SQLite instance (NFR4), isolated from the dev DB; frontend component tests use Vitest + jsdom + React Testing Library + user-event, stubbing API calls directly via `vi.fn()`/`vi.spyOn(fetch)` — no MSW. Playwright e2e is optional and mocks nothing.

### AD-5 — Login rate limiting

- **Binds:** FR2, NFR1; `/api/auth/login`
- **Prevents:** unbounded brute-force credential guessing
- **Rule:** built-in `Microsoft.AspNetCore.RateLimiting`, sliding window, 5 attempts per email+IP per 15-minute window on the login endpoint; over-limit returns 429 with the same generic invalid-credentials message as a normal failed login.

### AD-6 — First-admin bootstrap

- **Binds:** FR31, FR34
- **Prevents:** an admin-creation UI/backdoor; seed credentials committed to source control
- **Rule:** a single `IHostedService` runs after `Database.Migrate()`, seeding exactly one admin account via `PasswordHasher<T>` if none exists. Credentials come only from environment variables (`AdminSeed__Email`/`AdminSeed__Password`) — shell locally, a GitHub Actions secret in CI — never `dotnet user-secrets` (one credential path everywhere, not two tools to keep in sync). `appsettings.json` keeps only empty placeholder keys.

### AD-7 — Primary key strategy

- **Binds:** all entities
- **Prevents:** introducing GUID/UUID primary keys
- **Rule:** all entity primary keys are int auto-increment, justified by a single local SQLite instance with no distributed-write concern; do not switch to GUIDs without revisiting this AD.

### AD-8 — Appointment status is computed, not stored

- **Binds:** FR24, FR18, FR40; Appointment entity
- **Prevents:** a background job/scheduled task to flip appointment status; a stored status field drifting from real time; a hard SQL DELETE on Appointment from any trigger
- **Rule:** "Finished" (FR24) is computed at read time by comparing Date/StartTime to current EST "now" (AD-12), never persisted. Cancellation is the only real state change, captured as a nullable `CancelledAt` (soft-delete; row retained for history per FR18/FR40). "Cancels and deletes" in FR18/FR40 (demoting/removing a barber cancels their future appointments) means this same soft-cancel — setting `CancelledAt` — never a hard row delete, regardless of trigger (user cancellation, demotion cascade, or account-deletion cascade, AD-15). One mechanism for every cancellation path.

### AD-9 — Double-booking guard is defense-in-depth

- **Binds:** NFR2, FR9, FR10
- **Prevents:** a race between two near-simultaneous bookings for the same slot both succeeding; the same signed-in customer holding two appointments at the same date/time across different barbers (FR9)
- **Rule:** application-level check-then-insert inside a transaction is the primary guard; two DB-level partial unique indexes are the hard backstop against any application-logic bug: `UNIQUE(BarberId, Date, StartTime) WHERE CancelledAt IS NULL` (no double-booking a barber's slot) and `UNIQUE(CustomerId, Date, StartTime) WHERE CancelledAt IS NULL` (no customer holding two simultaneous appointments across barbers).

### AD-10 — Dev database & CI isolation

- **Binds:** NFR7; CI
- **Prevents:** committing local data to source control; CI tests touching or corrupting the dev DB
- **Rule:** the SQLite file lives at `backend/BarbershopApi/App_Data/barbershop.db`, gitignored — only `Migrations/` (code) is committed. The dev DB starts empty via `Database.Migrate()` on startup, no seeded sample data (see Deferred). CI tests run against their own separate temp SQLite instance via `WebApplicationFactory`.

### AD-11 — CI pipeline

- **Binds:** NFR5, NFR7, NFR4
- **Prevents:** merging a change that breaks either test suite; treating CI as a real-deploy signal it isn't
- **Rule:** one GitHub Actions workflow runs on every push, with parallel jobs for the .NET suite (real SQLite, NFR4) and the frontend suite (Vitest); a red pipeline is not mergeable — the DORA signal this project demonstrates, since no real deploy target exists (NFR7).

### AD-12 — Fixed EST timezone semantics

- **Binds:** NFR1; FR7, FR8, FR11
- **Prevents:** comparing dates/times in UTC or the client's local clock; a hardcoded UTC-5 offset that breaks across DST
- **Rule:** "EST" means US Eastern Time (`America/New_York`), correctly DST-aware, computed server-side; the server is sole authority on "today," "past," and the 30-minute booking cutoff.

### AD-13 — CORS & credentialed requests

- **Binds:** all; frontend↔API requests
- **Prevents:** the refresh cookie silently failing to send cross-origin during local dev
- **Rule:** the API's CORS policy explicitly allows the Vite dev-server origin with `AllowCredentials()`; every frontend fetch touching auth sets `credentials: 'include'`. `SameSite=Strict` is unaffected by the port difference (site = registrable domain, not port), and NFR7's local-only scope means cross-domain `SameSite=None` never arises.

### AD-14 — Booking date-range validity is always server-revalidated

- **Binds:** FR7, FR8
- **Prevents:** trusting the calendar/time-dropdown widget's client-side disabling as the actual guard (same failure mode AD-2/FR3 closes for role checks)
- **Rule:** on every booking submission, the server independently re-checks: date is not in the past, date is a weekday (weekends closed), date is within the 30-day forward cap (FR7), and — for a same-day booking — the slot isn't within 30 minutes of current EST time (FR8/AD-12). A disabled calendar cell or filtered dropdown option is a UX convenience, never the enforcement point.

### AD-15 — Account soft-delete with relaxed email uniqueness

- **Binds:** FR40; Account entity
- **Prevents:** hard-deleting an Account and orphaning the FK on historical Appointments that must be retained (FR18/FR40); an email being permanently unregistrable after its account is deleted
- **Rule:** Account deletion is soft-delete — a nullable `DeletedAt`, same shape as Appointment's `CancelledAt` — the row is retained forever so historical appointments still resolve to a name. The unique constraint on Email is scoped to non-deleted rows: `UNIQUE(Email) WHERE DeletedAt IS NULL`, so a deleted account's email becomes registerable again immediately. A deleted account can never sign in (auth checks treat `DeletedAt IS NOT NULL` the same as "account does not exist").

### AD-16 — Account optimistic concurrency

- **Binds:** FR41, NFR2; Account entity
- **Prevents:** an admin edit racing a user's own self-edit (or two admin edits, or an edit racing a delete) silently overwriting or corrupting the account
- **Rule:** Account carries an EF Core concurrency token (`RowVersion`/`[Timestamp]`); the first commit wins, the second gets a conflict error (`ProblemDetails`, 409) rather than a silent overwrite — the same "first commit wins" guarantee as AD-9's booking guard, applied to account mutation.

### AD-17 — Single read path for appointment views

- **Binds:** FR11, FR15, FR24; Appointment entity
- **Prevents:** the customer's own list, a barber's own schedule, and the admin's oversight view each independently computing "Finished" or naming fields differently, drifting apart at the EST boundary
- **Rule:** all three views read appointments through one shared `BookingService` method (or a shared read-model it returns) — including the Finished computation (AD-8) — never duplicated per-Controller.

### AD-18 — Client-side routing mirrors server-side role gating

- **Binds:** FR3, FR4; frontend
- **Prevents:** a route guard that only hides a nav link (client-side-only gating is not access control — the same principle AD-2 enforces server-side)
- **Rule:** client-side routing uses React Router (current major, v7+ — confirm the exact package, `react-router` vs. `react-router-dom`, against current docs at scaffold time given recent packaging changes across v6→v7→v8). Route guards call `GET /api/auth/me` to determine identity/role and redirect unauthenticated or wrong-role access; hiding a nav item (FR3) is a UX nicety layered on top, never the enforcement itself.

### AD-19 — z-pax SSO (OAuth2 Authorization Code)

- **Binds:** FR42–FR48; Auth domain (AuthController/AuthService/AccountRepository)
- **Prevents:** committing Client ID/Secret to source control; z-pax's token lifecycle leaking into this app's own session model; a duplicate account being created for an email that already exists; a login-CSRF attack that tricks a victim into completing an SSO flow initiated by an attacker
- **Rule:** SSO is folded into the existing Auth trio, not a new domain concept. `GET /api/auth/sso/login` generates a random `state` value, stores it in a short-lived cookie, and redirects to z-pax's authorize endpoint (`https://sapi.auth.myzpax.com/connect/authorize`) with `client_id`, `scope=offline_access`, `response_type=code`, `redirect_uri=https://localhost:7113/api/auth/sso/callback`, and `state` (fixed redirect URI, since NFR7 is local-only). `GET /api/auth/sso/callback` receives `code` and `state`; if `state` is missing or doesn't match the stored value, the callback fails the same way as a missing/invalid `code` (redirect to Login with an error, no account created or session issued) — per RFC 6749 §4.1.2, a spec-compliant authorization server always echoes `state` back unmodified, so a mismatch means the callback didn't originate from a flow this app initiated. Once `state` is validated, the backend exchanges `code` at z-pax's token endpoint (`https://sapi.auth.myzpax.com/connect/token`) using `client_id`/`client_secret` from environment variables `ZPaxSso__ClientId`/`ZPaxSso__ClientSecret` (never `appsettings.json`, never committed, same convention as AD-6) — the two endpoint URLs themselves aren't secret and are stored as regular config (`ZPaxSso__AuthorizationEndpoint`/`ZPaxSso__TokenEndpoint` in `appsettings.json`) rather than environment variables. The resulting z-pax access token is used exactly once to fetch identity (email, first name, last name) before being discarded — z-pax's own token/refresh lifecycle is never persisted or relied on afterward. If no `Account` row matches by email, one is created (`Role=Customer`, `PasswordHash=null`, `SsoProvider="zpax"`, `SsoSubjectId=<z-pax subject id>`); if a row already matches by email, that identity is attached to the existing row without touching its `PasswordHash` — both login methods remain valid afterward. Once identity is resolved, the app mints its own access/refresh tokens exactly as `POST /api/auth/login` does (AD-3) — SSO and password sign-in converge on the same session mechanism from that point on. An account with `PasswordHash=null` always fails password-login attempts with the same generic invalid-credentials message as FR2/AD-5 — no distinct "use SSO" message. No account created or linked via SSO can ever be `Role=Admin` (FR34/FR45). Automated tests use a fake `ISsoClient` double rather than the live z-pax service, mirroring AD-4's existing DB-isolation principle.

**myzPAX banner support (FR47):** the raw z-pax access token obtained during `ExchangeCodeForIdentity` — previously discarded entirely once used to fetch identity — is now also handed back to `SsoCallback`, which sets it in a short-lived (2-minute), single-use HttpOnly+Secure+SameSite=Strict cookie (`zpaxAccessToken`, scoped to the existing `/api/auth/sso` path) alongside the existing refresh-token cookie. A new endpoint, `GET /api/auth/sso/zpax-token` (`[Authorize]`'d via this app's own session), reads and deletes that cookie server-side and returns the token once in the JSON response body; if the cookie isn't present — a password-only login, or any request after the pickup window has passed — it returns 404. The frontend calls this endpoint once during session bootstrap and, if it succeeds, holds the returned z-pax access token in memory alongside its own access token (`AuthContext`) — never persisted, never re-fetched, and lost across a hard page refresh just like the app's own access token (AD-3). `MyzpaxBanner.init`'s `getToken` callback returns whatever's currently held in memory; once it's stale or was never obtained, the widget degrades to its own built-in minimal "Return to myzPAX" strip per its documented fail-safe behavior. z-pax's own access tokens have a fixed 20-minute lifetime — long enough for a single demo/testing session to see the full banner render — so no z-pax-side token-refresh infrastructure is built for this integration; letting the banner degrade after 20 minutes (or across a hard page refresh) is an accepted trade-off, mirroring AD-3's existing refresh-token-rotation deferral.

**myzPAX banner logout (FR48):** `MyzpaxBanner.init` (FR47) is now also passed an `onLogout` callback. Without it, the banner's logout control only ends the z-pax session — this app's own session (in-memory access token, server-side refresh session) would survive untouched. `onLogout` performs the same teardown as the existing NavBar Logout — revoke the server-side refresh session, clear `AuthContext` — then does a full-page navigation (`window.location.assign`, not client-side routing, for the same reason AD-19's existing Logout flow uses one) to a new backend endpoint, `GET /api/auth/sso/logout`. That endpoint reads and clears this app's `zpaxIdToken` cookie (set by `SsoCallback` at sign-in, alongside `zpaxAccessToken`, whenever z-pax's token response includes an `id_token` — requested via the `openid` scope) and 302-redirects the browser to z-pax's real end-session endpoint, `GET https://dapi.auth.myzpax.com/connect/logout?id_token_hint=...&post_logout_redirect_uri=...`, per z-pax's own `MyzpaxBanner.init` docs. `post_logout_redirect_uri` is registered with z-pax as this app's logout redirect. Live-verified end-to-end with a real SSO session (Story 4.5) — the z-pax session ends and the browser lands cleanly on the configured redirect page. The existing in-app Logout control (AD-3) is unchanged and remains available to every account, SSO or password, as a fallback — it clears `refreshToken`/`zpaxAccessToken`/`zpaxIdToken` but does not end the z-pax-side session, a known asymmetry accepted for this fallback path.

**myzPAX banner token refresh (FR49):** the authorization request built by `BuildAuthorizationUrl` (AD-19) now requests scope `"openid profile offline_access"` (previously `"openid profile"`), so z-pax's token response includes a `refresh_token` alongside `access_token`/`id_token`. `ZPaxTokenResponse` captures it, and `SsoCallback` stores it in a new `zpaxRefreshToken` cookie — HttpOnly+Secure+SameSite=Strict, path `/api/auth/sso` — set alongside the existing `zpaxAccessToken` (2-minute, single-use) and `zpaxIdToken` (15-day) cookies. A new `ISsoClient` method, `RefreshAccessToken(string refreshToken)`, POSTs to z-pax's token endpoint with `grant_type=refresh_token` and the stored token; a new endpoint, `GET /api/auth/sso/zpax-refresh` (`[Authorize]`'d via this app's own session), reads the cookie, calls it, and returns the new z-pax access token in the response body — overwriting `zpaxRefreshToken` if z-pax returns a rotated refresh token, so a stale one is never reused. The frontend schedules a call to this endpoint ahead of the z-pax access token's lifetime and adopts the result transparently; if the refresh fails for any reason (missing cookie, z-pax rejects the token), the banner degrades to its own built-in fallback exactly as it does today when no token is available (Story 4.4) — this app's own session is unaffected either way, matching FR49's degrade-silently behavior. This is built and live-verified against z-pax's original short-lived configuration (20-minute access / 60-minute refresh) first; only once proven does z-pax change this app's configured lifetimes to 60-minute access / 15-day refresh, matching this app's own tokens (Story 4.6).

## Consistency Conventions

| Concern | Convention |
| --- | --- |
| Naming (entities, files, interfaces, events) | PascalCase for C# types/methods/properties; camelCase for JSON payloads (System.Text.Json default) and JS/React code. |
| Data & formats (ids, dates, error shapes, envelopes) | Dates/times on the wire are plain `yyyy-MM-dd` / `HH:mm` strings, no offset — client never does timezone math (server is sole authority, AD-12). Errors use ASP.NET Core's built-in `ProblemDetails` (RFC 7807) — automatic for `[ApiController]` validation errors, `Problem()` helper for custom errors (booking conflict, stale cancellation). |
| State & cross-cutting (mutation, errors, logging, config, auth) | Auth: DB-derived role + SessionVersion checks, split access/refresh tokens, fixed 401/403 split, `Role` enum (AD-2, AD-3). CORS: Vite dev origin allowed with credentials (AD-13). Concurrency: optimistic `RowVersion` on Account (AD-16), unique-index backstop on Appointment (AD-9) — first commit wins everywhere. |

## Stack

| Name | Version |
| --- | --- |
| .NET | 10 (LTS, supported to 2028-11) |
| ASP.NET Core Web API | 10 — `dotnet new webapi --use-controllers` (controllers, not minimal APIs) |
| EF Core | 10.0.10 |
| Microsoft.EntityFrameworkCore.Sqlite | 10.0.10 |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.9 |
| ASP.NET Core Identity `PasswordHasher<T>` (PBKDF2) | bundled with .NET 10 |
| Microsoft.AspNetCore.RateLimiting | bundled with .NET 10 (no third-party package) |
| React | 19.2.8 |
| Vite | 8.1.5 — official React JS template (non-TS variant) |
| Frontend language | JavaScript (no TypeScript) |
| Frontend data-fetching | plain `fetch` + React state (no React Query/TanStack Query) |
| React Router | current major, v7+ (AD-18) — confirm exact package name at scaffold time |
| @radix-ui/react-dialog | 1.1.21 — modal (account-edit, confirm-action popups) |
| @radix-ui/react-select | 2.3.4 — dropdowns (barber-select, time-slot, admin permission-select) |
| @radix-ui/react-popover | 1.1.16 — calendar trigger/panel container |
| react-day-picker | 10.0.1 — calendar grid logic, paired with Popover (Radix has no native calendar primitive; this is Radix's own recommended pairing) |
| z-pax SSO integration | plain `HttpClient` (no OAuth client NuGet package) | Two-endpoint integration (AD-19) doesn't justify a dependency, consistent with this project's existing bias against adding one for a small surface (see no-MSW, no-React-Query reasoning) |
| xUnit.v3 (+ WebApplicationFactory) | 3.2.2 |
| Vitest | 4.1.10 |
| @testing-library/react | 16.3.2 |
| @testing-library/jest-dom | 7.0.0 (requires Node ≥22, `@testing-library/dom` peer dep) |
| @testing-library/user-event | 14.6.1 |
| Playwright | 1.61.1 — optional e2e |

## Structural Seed

```text
{repo-root}/
  backend/
    BarbershopApi/
      Controllers/       # one per role/domain concept: Auth, Booking, Account/Admin
      Services/
      Repositories/
      Entities/
      Dtos/
      Data/              # DbContext, Migrations/ (committed), App_Data/barbershop.db (gitignored)
    BarbershopApi.Tests/
  frontend/
    src/
      pages/
      components/
      api/
      styles/
  .github/
    workflows/
      ci.yml
  _bmad/                 # existing
  _bmad-output/           # existing
  docs/                  # existing
```

```mermaid
erDiagram
    ACCOUNT ||--o{ APPOINTMENT : "books (as Customer)"
    ACCOUNT ||--o{ APPOINTMENT : "works (as Barber)"
    ACCOUNT {
        int Id PK
        string Email UK
        string PasswordHash "nullable — null for SSO-only accounts"
        string FirstName
        string LastName
        string Role
        int SessionVersion
        datetime DeletedAt
        int RowVersion
        string SsoProvider "nullable"
        string SsoSubjectId "nullable"
    }
    APPOINTMENT {
        int Id PK
        int CustomerId FK
        int BarberId FK
        string Date
        string StartTime
        datetime CancelledAt
    }
```

**Deployment & CI envelope:** runs locally only (NFR7) — no production hosting or public deploy target. GitHub Actions CI (AD-11) is the only "deployment-shaped" surface: it keeps the codebase provably deployable on every push without deploying anywhere.

## Deferred

- **Refresh-token rotation & reuse-detection** — accepted trade-off for now (AD-3); revisit as a future hardening pass if the stolen-cookie exposure window (up to 15 days) becomes unacceptable.
- **Guest (unauthenticated) booking** — PRD non-goal, called out as a possible same-day addition that never landed; revisit architecture if it's added later.
- **Dev database seeding with sample data** — explicitly declined; dev DB starts empty via `Database.Migrate()` (AD-10).
- **UX open items touching implementation** — DESIGN.md flags no error/warning color exists yet for form-validation states (e.g., password-mismatch messages), and the exact tablet breakpoint pixel value is undefined (only named, not sized); both need a decision before the components that depend on them are built, but neither is an architecture-level call. Owner: UX (Sally) — revisit before the ScheduleAppointment/Register/Account components are built.
- **Hiding in-app Account/Logout UI for SSO-authenticated users** — deferred; currently the in-app Account and Logout controls remain visible and available to every account, SSO or password, as a fallback (FR48). Revisit during Story 4.6's review period whether SSO-authenticated visitors should have these hidden now that the myzPAX banner provides its own logout control.
- **myzPAX banner re-fetch on a stale token (FR47/AD-19)** — currently, once the one-time `zpaxAccessToken` goes stale/is consumed, the banner simply never mounts (or degrades to the vendor's own fallback) for the rest of that session — there's no path back to a fresh token short of a brand-new SSO login. A future revision could have an SSO-linked account transparently re-acquire a fresh z-pax token when the in-memory one goes stale (e.g. a silent re-auth against z-pax), so the banner stays available for the account's whole session rather than just its first ~20 minutes. Deliberately out of scope for Story 4.4 — flagged by Jack as a likely future change, not a defect in the current design.
