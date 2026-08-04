---
project_name: 'bmad-learning-project'
user_name: 'Jack'
date: '2026-07-24'
sections_completed: ['technology_stack', 'language_specific_rules', 'framework_specific_rules', 'testing_rules', 'code_quality_style_rules', 'development_workflow_rules', 'critical_dont_miss_rules']
existing_patterns_found: 0
status: 'complete'
rule_count: 34
optimized_for_llm: true
---

# Project Context for AI Agents

_This file contains critical rules and patterns that AI agents must follow when implementing code in this project. Focus on unobvious details that agents might otherwise miss._

---

## Technology Stack & Versions

**Core:**
- .NET 10 (LTS to 2028-11) / ASP.NET Core Web API — `dotnet new webapi --use-controllers` (controllers, not minimal APIs)
- EF Core 10.0.10 + Microsoft.EntityFrameworkCore.Sqlite 10.0.10
- React 19.2.8 + Vite 8.1.5 (official JS template — no TypeScript)
- SQLite (file-based, local only — no production deploy target, NFR7)

**Key dependencies:**
- Auth: Microsoft.AspNetCore.Authentication.JwtBearer 10.0.9, `PasswordHasher<T>` (bundled), `Microsoft.AspNetCore.RateLimiting` (bundled — no third-party rate-limit package)
- Frontend UI: @radix-ui/react-dialog 1.1.21, @radix-ui/react-select 2.3.4, @radix-ui/react-popover 1.1.16, react-day-picker 10.0.1
- Routing: React Router v7+ — confirm exact package (`react-router` vs `react-router-dom`) at scaffold time
- Data fetching: plain `fetch` + React state — no React Query/TanStack Query
- Testing: xUnit.v3 + WebApplicationFactory 3.2.2 (backend); Vitest 4.1.10 + @testing-library/react 16.3.2 + jest-dom 7.0.0 + user-event 14.6.1 (frontend); Playwright 1.61.1 (optional e2e)

**Version constraints:**
- `@testing-library/jest-dom` 7.0.0 requires Node ≥22 — verify the dev/CI Node version before scaffolding frontend tests.

## Critical Implementation Rules

### Language-Specific Rules

**C# / .NET:**
- Naming: PascalCase for all types, methods, and properties.
- Primary keys: `int` auto-increment on all entities — never GUID/UUID PKs (AD-7).
- Soft-delete only: `CancelledAt` (Appointment, AD-8) / `DeletedAt` (Account, AD-15) nullable columns — never a hard `DELETE`, regardless of trigger (cancellation, demotion cascade, account deletion).
- Errors: ASP.NET Core's built-in `ProblemDetails` (RFC 7807) — automatic for `[ApiController]` validation, `Problem()` helper for custom errors (booking conflicts, stale cancellations). Don't hand-roll a different error shape.
- Concurrency: Account uses an EF Core concurrency token (`RowVersion`/`[Timestamp]`) — first commit wins, second gets 409 (AD-16). Appointment relies on DB-level partial unique indexes instead (AD-9) — a different mechanism for a different race, don't conflate them.
- All dates/times computed server-side in EST (`America/New_York`, DST-aware) — never UTC or a hardcoded offset (AD-12). Wire format is plain `yyyy-MM-dd` / `HH:mm` strings, no offset.

**JavaScript / React (no TypeScript):**
- camelCase for JSON payloads and all JS/React code.
- Client never does timezone math — dates/times arrive as plain strings, server is sole authority (AD-12).
- No React Query/TanStack Query — plain `fetch` + React state only.
- Every auth-related fetch must set `credentials: 'include'` (AD-13) — omitting it silently breaks the refresh-cookie flow cross-origin in dev.
- Access token held in memory only (never localStorage/sessionStorage); refresh token is an HttpOnly cookie never read by JS (AD-3).

### Framework-Specific Rules

**ASP.NET Core (backend layering):**
- Controllers → Services → Repositories, one-way dependency flow only — never reversed, never skip-level (a Controller must not query EF Core/Repositories directly) (AD-1).
- One Controller/Service/Repository trio per domain concept — Auth, Booking, Account/Admin — not one per entity, not a shared catch-all class (AD-1).
- All appointment reads (customer's own list, barber's schedule, admin oversight view) go through one shared `BookingService` method/read-model — including the "Finished" computation — never reimplemented per-Controller (AD-17).
- Every protected endpoint independently re-derives Role + SessionVersion from the DB per request — don't trust the JWT's role claim as-is (AD-2).

**React:**
- Route guards call `GET /api/auth/me` to determine identity/role and redirect on unauthorized/wrong-role access — hiding a nav item is a UX nicety layered on top, never the actual enforcement (AD-18).
- Booking-date validity (past dates, weekends, 30-day cap, same-day 30-min cutoff) must be re-validated server-side even though the calendar/dropdown disables invalid options client-side — the disabled UI state is a convenience, never the enforcement point (AD-14).

### Testing Rules

- Backend tests: xUnit.v3 + `WebApplicationFactory` against a real SQLite instance, never mocked — isolated from the dev DB (its own temp SQLite per CI run) (AD-4, AD-10).
- Frontend tests: Vitest + jsdom + React Testing Library + user-event; stub API calls directly via `vi.fn()`/`vi.spyOn(fetch)` — no MSW, don't introduce a request-mocking framework for this small a fetch surface (AD-4).
- Playwright e2e is optional, and when used, mocks nothing (AD-4).
- CI: one GitHub Actions workflow, parallel jobs for the .NET suite and frontend suite — a red pipeline is not mergeable; this is the project's DORA signal since there's no real deploy target (AD-11).
- Frontend CI job runs `eslint .` (code quality) **and** a separate `prettier --check .` step (formatting) — both must pass; ESLint alone does not catch unformatted code since `eslint-config-prettier` disables its stylistic rules.

### Code Quality & Style Rules

**Code organization (Structural Seed):**
```
backend/BarbershopApi/
  Controllers/   # one per role/domain concept: Auth, Booking, Account/Admin
  Services/
  Repositories/
  Entities/
  Dtos/
  Data/          # DbContext, Migrations/ (committed), App_Data/barbershop.db (gitignored)
backend/BarbershopApi.Tests/
frontend/src/
  pages/
  components/
  api/
  styles/
```

**Naming:**
- PascalCase for C# types/methods/properties.
- camelCase for JSON payloads and JS/React code.
- File naming: PascalCase across the board — C# files (`BookingService.cs`), React components (`ScheduleAppointment.jsx`), and their test files (`ScheduleAppointment.test.jsx`). Non-component JS (utilities, API wrapper modules) also PascalCase.

**Linting/formatting:**
- ESLint (Vite's default React config) + Prettier; `eslint-config-prettier` disables ESLint's stylistic rules so Prettier is the single source of truth for formatting — don't let both tools fight over the same rule.
- `eslint-config-prettier` only turns off conflicting ESLint rules — it does not run Prettier itself. `eslint .` passing does not mean the code is formatted correctly; formatting is enforced separately in CI (see Testing Rules — CI).

### Development Workflow Rules

- Branching: trunk-based — `main` is always deployable, gated by CI (AD-11).
- One short-lived branch per story: `story/{epic}.{story}-{short-slug}` (e.g. `story/1.2-barber-login`), branched from `main`.
- Merge to `main` via PR once the story is complete and CI is green; delete the branch after merge — no long-lived `develop` branch.
- Commit/PR cadence is itself part of the deliverable (DORA deployment-frequency evidence) — prefer several small merges over one large batched one per story.

### Critical Don't-Miss Rules

**Security:**
- Login rate limiting: `Microsoft.AspNetCore.RateLimiting`, sliding window, 5 attempts per email+IP per 15 min on `/api/auth/login` — over-limit returns 429 with the *same* generic invalid-credentials message as a normal failed login, don't leak that the limit was hit (AD-5).
- Password-change rate limiting: same shape as login's (5 attempts per 15 min, sliding window), scoped to `PUT /api/account/me` requests that actually set `NewPassword` — plain name-only edits go through an unlimited partition and are never throttled. Partitioned by `{ip}:{accountId}` (not email, since the caller is already authenticated); requires `UseRateLimiter()` to run after `SessionLivenessMiddleware` so the partition resolver can read the authenticated account id from `HttpContext.Items["Account"]` (AD-5).
- Admin bootstrap: exactly one admin seeded via an `IHostedService` after `Database.Migrate()`, credentials from env vars only (`AdminSeed__Email`/`AdminSeed__Password`) — never `dotnet user-secrets`, never a UI/backdoor for creating admins (AD-6).
- `Role` is a fixed enum (`Customer`/`Barber`/`Admin`, PascalCase) — never an ad-hoc string literal; casing drift between call sites is exactly the bug this prevents (AD-2).

**Concurrency / race conditions:**
- Double-booking guard is defense-in-depth, not either/or: app-level check-then-insert inside a transaction *and* two DB-level partial unique indexes as backstop (`BarberId+Date+StartTime` and `CustomerId+Date+StartTime`, both `WHERE CancelledAt IS NULL`) — implement both, not just one (AD-9).

**Cross-cutting gotchas:**
- CORS: API policy must explicitly allow the Vite dev origin with `AllowCredentials()` — otherwise the refresh cookie silently fails to send cross-origin in local dev (AD-13).
- 401 vs 403 is a fixed split, not a judgment call: 401 = unauthenticated or session-invalid (missing/expired token, SessionVersion mismatch); 403 = authenticated but wrong role — controllers must not invent their own status codes (AD-2).

No performance-specific gotchas are called out in the architecture (single local SQLite instance, no scale requirement) — intentionally out of scope, not an oversight.

---

## Usage Guidelines

**For AI Agents:**
- Read this file before implementing any code
- Follow ALL rules exactly as documented
- When in doubt, prefer the more restrictive option
- Update this file if new patterns emerge

**For Humans:**
- Keep this file lean and focused on agent needs
- Update when technology stack changes
- Review quarterly for outdated rules
- Remove rules that become obvious over time

Last Updated: 2026-07-24
