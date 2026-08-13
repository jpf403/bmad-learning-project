---
baseline_commit: 5882f61350a144e7dbd5fcdf13b695bca4aa2e51
---

# Story 3.2: Admin Account Search

Status: done

## Story

As an admin,
I want a dedicated Admin Panel where I can search for a customer or barber account by name or email,
so that I can quickly find the account I need to manage.

## Acceptance Criteria

1. **Given** the Admin Panel, **when** it renders, **then** it hosts account search and the results list as its own dedicated surface, reachable via the existing (currently dangling) `/admin` nav link and route (FR16).
2. **Given** an admin submits a search query, **when** the search runs, **then** partial, case-insensitive matches on first name, last name, combined "first last," or email appear as clickable, keyboard-operable rows (FR17).
3. **Given** the panel on first load, before any search has been submitted, **when** rendered, **then** it shows "Search by name or email to find an account."
4. **Given** a submitted query that matches no account, **when** searched, **then** it shows "No accounts match your search."
5. **Given** the single admin account, **when** any search is run, **then** it never appears as a result row (FR17, FR34).

## Tasks / Subtasks

- [x] **Task 1: Add the search endpoint to the existing `AccountController`** (AC: #1, #2, #5)
  - [x] Add `public record AccountSummary(int Id, string Email, string FirstName, string LastName, Role Role);` to `backend/BarbershopApi/Dtos/`. **Do not reuse `MeResponse`** even though the shape is identical today — `MeResponse` is the caller's-own-identity contract (`GET /api/auth/me`, `PUT /api/account/me`); this is a different contract (an admin's view of an arbitrary target account in a list) that happens to coincide in shape now. Coupling them would make an independent future change to either endpoint's contract awkward. This matches the project's own existing precedent of one DTO per endpoint context (`BarberSummary` vs `MeResponse` vs `AppointmentView` are all distinct despite overlapping fields).
  - [x] Add `[HttpGet("search")]` to `AccountController` (`GET /api/account/search?query=...`):
    - `[Authorize(Roles = "Admin")]` on the action, **in addition to** the class-level `[Authorize]` (ASP.NET Core combines both — caller must be authenticated *and* hold the Admin role). This is the first production endpoint in the codebase to use attribute-based role gating rather than a manual `HttpContext.Items["Account"]` role branch (which `BookingController.GetSchedule` uses because it needs multi-role *branching* logic, not a hard single-role gate). Attribute gating is provably correct here: `SessionLivenessMiddleware` (registered before `UseAuthorization()` in `Program.cs`) already refreshes the `ClaimTypes.Role` claim from a fresh DB read on every request (AD-2), and `RoleGatingTests.cs`/`RoleGateTestController` already prove `[Authorize(Roles = "Admin")]` correctly yields 401 (no/invalid token) vs 403 (wrong role) vs 200 (admin) against that exact middleware pipeline — reuse that proven mechanism rather than hand-rolling a redundant check.
    - Parameter: `[FromQuery] string? query`.
    - Body: `var accounts = await accountService.SearchAccounts(query ?? string.Empty); return Ok(accounts.Select(a => new AccountSummary(a.Id, a.Email, a.FirstName, a.LastName, a.Role)));`
    - No try/catch needed — `SearchAccounts`/`Search` never throw (confirmed by reading `AccountRepository.Search`/`AccountService.SearchAccounts`, Story 3.1).
  - [x] No `IAccountService`/`AccountService`/`AccountRepository` changes — `Search`/`SearchAccounts` already exist, already exclude `Role.Admin` and soft-deleted rows, and already return `[]` for a blank/whitespace query (Story 3.1). This story is Controller + DTO only on the backend.
- [x] **Task 2: `AccountControllerTests.cs` additions** (AC: #2, #5)
  - [x] `Search_as_admin_returns_matching_accounts` — register two accounts (one Customer, one Barber via direct role-flip like `RoleGatingTests` does), search by a shared name substring, assert both appear with correct `Email`/`FirstName`/`LastName`/`Role`.
  - [x] `Search_excludes_the_admin_account_from_results` — search with a query that would also match the seeded/promoted admin's name; assert the admin never appears in the response body (AC #5 — this is the HTTP-level proof that Story 3.1's repository-level exclusion actually reaches the caller; the repository/service layers are already unit-tested, this test is new because no HTTP surface existed before this story).
  - [x] `Search_with_blank_query_returns_empty_array`.
  - [x] `Search_with_no_matches_returns_empty_array`.
  - [x] `Search_as_non_admin_returns_403` — parametrize or duplicate for both `Role.Customer` and `Role.Barber` callers.
  - [x] `Search_without_access_token_returns_401`.
  - [x] Reuse `RoleGatingTests.RegisterAndLoginAs(_factory, client, Role.Admin, email)` (internal static, already reused cross-file by `BookingControllerTests`) to obtain an authenticated admin caller — do not duplicate that register→promote→login dance a third time in this file.
- [x] **Task 3: `searchAccounts` in `AccountApi.js`** (AC: #2, #3, #4)
  - [x] Add `export async function searchAccounts(accessToken, query)` — `GET /api/account/search`, built with `URLSearchParams` exactly like `BookingApi.js`'s `getSchedule` (`params.set('query', query)` only when `query` is truthy after trimming, so an empty/whitespace call omits the param entirely rather than sending `?query=`). Success envelope `{ ok: true, accounts: body }`; failure envelope `{ ok: false, status, problem }` — same shape as every other `*Api.js` function (`getBarbers`, `getSchedule`).
- [x] **Task 4: `AdminPanel` page, route, and styling** (AC: #1, #2, #3, #4, #5)
  - [x] Create `frontend/src/pages/AdminPanel.jsx` + `frontend/src/pages/AdminPanel.css`. **Name it `AdminPanel`, not `AdminAccounts`** — per `EXPERIENCE.md`'s Information Architecture, the `/admin` route is one dedicated surface hosting search *and* (in Stories 3.3–3.5) the edit/create/delete popups; naming the file for just this story's slice would be misleading once those stories extend the same page. Stories 3.3/3.4/3.5 add popups and buttons to this same file — do not create separate pages for them.
  - [x] Page state: `query` (controlled input string), `searched` (bool — has a search actually been submitted yet), `accounts` (array), `loading` (bool), `error` (string). Use a `requestIdRef` generation counter exactly like `MySchedule.jsx`'s `loadDate` (`frontend/src/pages/MySchedule.jsx:40,75,78`) so a stale response from an earlier submit can't clobber a newer one.
  - [x] Wrap the search input in a `<form onSubmit={...}>` so both Enter and a "Search" button submit — the AC's wording ("enters a search query... when submitted") describes an explicit submit, not live/debounced search; don't add a debounce mechanism nobody asked for. On submit: if `query.trim()` is empty, do nothing (stay on/return to the AC #3 "before any search" state — don't call the API or ever show "No accounts match your search" for an empty query, since AC #3 and AC #4 are meant to be mutually exclusive triggers). Otherwise call `searchAccounts`, set `searched = true`, and on success set `accounts`; on failure set a generic `error` (own state, same "message + Try again button" shape `MySchedule.jsx` already establishes for fetch failures — EXPERIENCE.md doesn't specify a search-failure state, so extend the app's one existing convention rather than inventing a new shape).
  - [x] Render, in order: an `<h1>Admin Panel</h1>` (matches `EXPERIENCE.md`'s IA naming and `DESIGN.md`'s `{typography.h1}` example usage of exactly this string); the search form (`Input` component, `label="Search"`, `placeholder="Name or email"`); then exactly one of:
    - `!searched` → `<p>Search by name or email to find an account.</p>` (AC #3, exact copy).
    - `loading` → a `Searching…` loading line (same convention as `Loading…` elsewhere).
    - `error` → error message + "Try again" button that resubmits the last query.
    - `searched && accounts.length === 0` → `<p>No accounts match your search.</p>` (AC #4, exact copy).
    - `searched && accounts.length > 0` → a list of rows, one per account.
  - [x] Each result row is a full-width `<button type="button" className="admin-account-row">` (native `<button>`, not a `<div>` with manual key handlers — gets Tab-focus, Enter/Space activation, and a visible focus ring for free, satisfying `EXPERIENCE.md`'s accessibility note that admin rows are fully custom, not Radix-backed, so keyboard operability must be deliberate). Row content: full name, email, and role label — DESIGN.md's `admin-account-row` token only specifies the background/hover/foreground styling, not which fields render, so this is this story's own call (an admin needs name+email+role to tell accounts apart before Story 3.3's edit popup exists). **`onClick` is an intentional no-op placeholder** (e.g. a comment-only empty handler) — Story 3.3 is explicitly "an account row is clicked → the edit popup opens"; do not build a stub popup or partial edit UI now, just the clickable/focusable row shell AC #2 requires.
  - [x] `AdminPanel.css`: reuse the `admin-account-row` tokens already established for the shared "tinted sections" look (`{colors.neutral}` resting fill / `{colors.border}` hover, no border, `{colors.text}` foreground — same family as `.schedule-row-open`/`.schedule-row-booked` in `MySchedule.css`). Add a `@media (max-width: 639px)` block stacking the search bar full-width above a full-width stack of rows (per `EXPERIENCE.md`'s Responsive & Platform table: "Admin Panel's search bar sits above a full-width stack of account rows") — the project's other page CSS files (`NavBar.css`, `MySchedule.css`) already use this exact breakpoint; match it, don't introduce a different one.
  - [x] Add the route to `frontend/src/App.jsx`, following the exact `RequireRole` pattern already used for `/my-schedule`:
    ```jsx
    <Route
      path="/admin"
      element={
        <RequireRole roles={['Admin']}>
          <AdminPanel />
        </RequireRole>
      }
    />
    ```
    This resolves the pre-existing dangling nav link — `NavBar.jsx:20` already declares `{ label: 'Admin Panel', to: '/admin', roles: ['Admin'] }` with no matching route in `App.jsx` today; no `NavBar.jsx`/`NavBar.css` change is needed, only `App.jsx`. (`NavBar.test.jsx` already stubs its own independent `<Route path="/admin">` and doesn't render the real `App`, so it needs no change either.)
- [x] **Task 5: `AdminPanel.test.jsx`** (AC: #1, #2, #3, #4, #5)
  - [x] Renders "Search by name or email to find an account." before any search.
  - [x] Submitting a blank/whitespace-only query does not call `searchAccounts` and leaves the "before any search" message visible.
  - [x] Submitting a query that returns matches renders one row per account (name + email visible).
  - [x] Submitting a query that returns no matches shows "No accounts match your search."
  - [x] A failed fetch shows the error message + "Try again" button; clicking it resubmits the same query.
  - [x] Stub `searchAccounts` directly (`vi.fn()`/`vi.spyOn`, per this project's frontend testing convention — no MSW, AD-4) rather than mocking `fetch` for this component's own tests.
- [x] **Task 6: Check `deferred-work.md`** (retro discipline, per the standing Epic 1 action item still in force)
  - [x] Re-read `deferred-work.md` in full at kickoff. None of the currently-open items apply directly to this story's scope (search-only, no edit/create/delete, no cascade) — confirm and note as "checked, not applicable" in Completion Notes. The four items deferred from Story 3.1's review (null-guarding on `AdminCreateBarber`/`AdminUpdateAccount`, blank-email slipping past duplicate-email check, customer soft-delete not cascading, `EnsureNotCurrentlyAdmin`'s missing-vs-non-admin conflation) are explicitly scoped to Stories 3.3/3.4/3.5, not this one.
- [ ] **Task 7: Verify CI green and branch/PR**
  - [x] Branch as `story/3.2-admin-account-search` from `main`.
  - [ ] Push and confirm both CI jobs (Backend .NET, Frontend Vite/React) green before merging (AD-11). **Left for Jack** — per standing project practice, push/PR/CI verification steps are his to run and approve individually, not performed by the dev agent.

### Review Findings

- [x] [Review][Patch] `AdminPanel.jsx` drops the `isMountedRef` unmount guard that `MySchedule.jsx` (this story's own designated pattern to follow) pairs with its `requestIdRef` generation counter — if an admin navigates away mid-search, the resolved fetch still calls `setLoading`/`setSearched`/`setAccounts`/`setError` on an unmounted component. [frontend/src/pages/AdminPanel.jsx] — Fixed: added `isMountedRef` guard mirroring `MySchedule.jsx`.
- [x] [Review][Patch] Neither the "Search" submit button nor the "Try again" button is `disabled` while `loading` is true, unlike every other submit form in the codebase (`ScheduleAppointment.jsx`, `Login.jsx` both disable their submit button during in-flight requests) — a user can double-click and fire redundant concurrent requests. [frontend/src/pages/AdminPanel.jsx:64,76-78] — Fixed: `disabled={loading}` added to the Search submit button (the only one that stays mounted while `loading` is true — the error-state "Try again" button unmounts as soon as loading starts, so it carried no real double-click risk).
- [x] [Review][Patch] `runSearch`'s failure branch has no special case for a `401` (expired/invalid access token) — every other page that calls an authenticated endpoint (`Account.jsx:131-135`) logs the user out and redirects to `/login` with a "session expired" message on 401; `AdminPanel` instead shows the generic error + "Try again," which just repeats the same expired-token request forever. [frontend/src/pages/AdminPanel.jsx] — Fixed: added a `result.status === 401` branch that calls `logout()` and navigates to `/login` with the same session-expired message `Account.jsx` uses.

## Dev Notes

### Architecture Compliance (must-follow, not optional)

- **AD-1 (layering)** — Controllers → Services → Repositories, one-way. This story adds one action to the existing `AccountController` and one new DTO; it does **not** add a new `AdminController`/`AdminService`. There is one Account/Admin trio (per `ARCHITECTURE-SPINE.md`'s Structural Seed) — `Search`/`SearchAccounts` already exist from Story 3.1, this story only wires an HTTP surface on top.
- **AD-2 (role/session liveness per request)** — `[Authorize(Roles = "Admin")]` on the new action satisfies this directly: `SessionLivenessMiddleware` refreshes the `Role` claim from a fresh DB read before `UseAuthorization()` runs (`Program.cs:203-212`), so the attribute is checking current, not stale, role data. Never trust a raw JWT claim without that middleware in the pipeline — it already is, for every controller.
- **project-context.md's fixed 401/403 split** — unauthenticated/invalid token → 401; authenticated but wrong role → 403. `[Authorize(Roles = "Admin")]` yields exactly this split for free (proven by `RoleGatingTests.cs` against the identical middleware pipeline) — do not add a manual status-code branch that could drift from this.
- **AD-15 (soft-delete)** / **AD-7 (int PKs)** — no new work; `Search` already filters `DeletedAt == null`, and `AccountSummary.Id` is the existing `int`.
- **AD-18 (client-side routing mirrors server-side gating)** — `RequireRole roles={['Admin']}` on the new `/admin` route is the client-side half; the real enforcement is the new endpoint's `[Authorize(Roles = "Admin")]`. Hiding the nav link (already done, `NavBar.jsx:20`) is a UX nicety layered on top, never the enforcement itself.
- **AD-4 (testing)** — backend: xUnit.v3 + `WebApplicationFactory` against real SQLite, never mocked. Frontend: Vitest + RTL + `user-event`, stub `searchAccounts`/`fetch` directly, no MSW.

### Design Decisions This Story Must Make (epics/architecture/UX leave these open)

Neither `epics.md`, the architecture docs, nor `DESIGN.md`/`EXPERIENCE.md` specify an endpoint shape, a response DTO, which fields an account row displays, or what a search-failure looks like — they stop at the FR/AC/component-token level (confirmed by direct research: no mockup exists for this screen at all, see below). This story makes the following calls, matching Story 3.1's own practice of documenting open decisions inline:

- **Attribute-based `[Authorize(Roles = "Admin")]` instead of a manual `HttpContext.Items["Account"]` role check.** Every existing controller action uses the manual pattern, but only because they need multi-role *branching* (`BookingController.GetSchedule` treats Barber and Admin differently) or don't need role-gating at all (`AccountController.UpdateMe` just needs "any authenticated caller"). This is the first endpoint that needs a hard single-role gate with no branching — the attribute is simpler and already proven correct against this exact middleware pipeline via `RoleGatingTests`/`RoleGateTestController`. Follow this precedent for any future admin-only-no-branching endpoint (Stories 3.3–3.5 will likely want the same attribute).
- **New `AccountSummary` DTO rather than reusing `MeResponse`.** Same shape today, different semantic contract (own-identity vs. admin's-view-of-arbitrary-account) — see Task 1.
- **`AdminPanel.jsx`, not `AdminAccounts.jsx`, as the page file name.** The `/admin` route is one dedicated surface across Stories 3.2–3.5 (search + edit + create + delete popups, per `EXPERIENCE.md`'s IA row: "Admin Panel | Nav link | Account search, result list, edit/create/delete popup"). Naming the file after only this story's slice would force a rename later; name it for what the whole route becomes.
- **Explicit submit (form/Enter), not live/debounced search-as-you-type.** The AC's own wording is "enters a search query... when submitted" — matches a plain form submit, no new debounce infrastructure.
- **Row click is an intentional no-op for this story.** AC #2 requires rows to be clickable/focusable (the affordance); Story 3.3 explicitly owns "click → edit popup opens" (its own AC #1: "an account row is clicked, when the edit popup opens..."). Building any part of the edit popup now would be scope creep into 3.3.
- **No visual mockup exists for this screen.** `ux-designs/.../mockups/` has only `home.html`, `schedule-appointment.html`, `my-schedule.html`, `confirm-popup.html` — `EXPERIENCE.md`'s own composition-reference list confirms no Admin Panel mockup was ever produced. Build from `DESIGN.md`'s `admin-account-row` token and `EXPERIENCE.md`'s State Patterns table (both quoted above/below), not from a mockup — there isn't one to check against.
- **No pagination.** Nothing in the architecture, PRD, or UX docs mentions pagination for this list, and no pagination envelope DTO exists anywhere in the codebase to follow as precedent. Given NFR7's single-local-instance, no-real-scale scope, a bare array response (matching `GetBarbers`'s existing shape) is correct — do not invent a paging contract.

### Testing Requirements

- Backend: xUnit.v3 + `WebApplicationFactory`/`SqliteApiFactory`, real temp SQLite, no mocked `DbContext`. Reuse `RoleGatingTests.RegisterAndLoginAs` for an authenticated admin caller rather than re-deriving the register→promote-via-repository→login dance a third time.
- `Search_excludes_the_admin_account_from_results` must actually promote an account to `Role.Admin` (or use the auto-seeded admin) and assert it is absent from the JSON body — this is new HTTP-level coverage; Story 3.1's existing `Search_excludes_admin_account` only proves the repository layer, and no Controller test could exist before this story since no Controller action did.
- Frontend: Vitest + `@testing-library/react` + `user-event`; stub `searchAccounts` (or `vi.spyOn(fetch)`) directly per this codebase's established no-MSW convention (AD-4). Use `userEvent.type`/`userEvent.click`/form submit, not raw `fireEvent`, matching `MySchedule.test.jsx`'s style.
- No new backend repository/service tests are needed — `Search`/`SearchAccounts` are already fully covered by Story 3.1's `AccountRepositoryTests.cs`/`AccountServiceTests.cs`.

### Project Structure Notes

- **Backend — modified:** `backend/BarbershopApi/Controllers/AccountController.cs` (new `[HttpGet("search")]` action). **New:** `backend/BarbershopApi/Dtos/AccountSummary.cs`. No `Repositories/`/`Services/` changes — Story 3.1 already built and tested `Search`/`SearchAccounts`.
- **Frontend — new:** `frontend/src/pages/AdminPanel.jsx`, `AdminPanel.css`, `AdminPanel.test.jsx`. **Modified:** `frontend/src/App.jsx` (new `/admin` route), `frontend/src/api/AccountApi.js` (new `searchAccounts` export). `frontend/src/components/NavBar.jsx` needs **no change** — the `/admin` link already exists (`NavBar.jsx:20`), this story just gives it somewhere real to go.
- **Tests — modified:** `backend/BarbershopApi.Tests/AccountControllerTests.cs` (new cases only — no existing test in this file changes behavior). **New:** `frontend/src/pages/AdminPanel.test.jsx`. No change needed to `NavBar.test.jsx` (already stubs its own `/admin` route, doesn't render real `App`) or `App.test.jsx` (only exercises `/` and `/about` today; adding assertions for every protected route there is out of this story's scope).
- `Program.cs` needs no new registrations — `IAccountService`/`AccountService` is already `Scoped` and unchanged by this story.

### Established Codebase Patterns to Extend (current state, confirmed by reading the files directly)

- `IAccountRepository`/`AccountRepository` today (post-3.1): `Create`, `FindByEmail`, `FindById`, `Update`, `AdminExists`, `FindAllByRole`, `Search(string query)`, `AdminUpdate`, `SoftDelete`. `Search` (`AccountRepository.cs:54-69`): trims/lowercases the query, returns `[]` for blank input, matches `FirstName`/`LastName`/`"FirstName LastName"`/`Email` (all case-insensitive `Contains`), excludes `Role.Admin` and `DeletedAt != null`.
- `IAccountService`/`AccountService` today: `UpdateOwnProfile`, `SearchAccounts(string query)` (thin passthrough, `AccountService.cs:70`), `AdminCreateBarber`, `AdminUpdateAccount`, `AdminSoftDeleteAccount`.
- `AccountController` today (`AccountController.cs`): `[ApiController] [Route("api/account")] [Authorize]`, exactly one action, `PUT api/account/me`, pulling the caller from `HttpContext.Items["Account"]`. This story adds the controller's second action and its first `GET`.
- `Program.cs:203-212` middleware order: `UseAuthentication()` → `SessionLivenessMiddleware` (refreshes the Role claim + sets `HttpContext.Items["Account"]` from a fresh DB row) → `UseRateLimiter()` → `UseAuthorization()`. This ordering is exactly why `[Authorize(Roles = "Admin")]` on a new action sees current, not stale, role data.
- `RoleGateTestController.cs` (`backend/BarbershopApi.Tests/TestOnly/`) is the only existing precedent for `[Authorize(Roles = "Admin")]` — test-only until this story; `RoleGatingTests.cs` proves 401/403/200 against it, including "role-change reflected without re-login" (i.e., a promoted-mid-session admin passes on their very next request, no new login needed) — the same guarantee this story's endpoint inherits for free.
- Frontend `*Api.js` convention (`AccountApi.js`, `BookingApi.js`): every function does `fetch` in try/catch (network failure → `{ ok: false, status: null }`), `response.json().catch(() => null)`, then `{ ok: false, status, problem: body }` on `!response.ok` or a null body, else `{ ok: true, <namedKey>: body }`. `BookingApi.js`'s `getSchedule` (`BookingApi.js:101-133`) is the exact template for a GET-with-optional-query-param call — mirror its `URLSearchParams` construction for `searchAccounts`.
- `frontend/src/pages/MySchedule.jsx` is the closest existing page-level pattern for "fetch on user action, render loading/error/empty/data states" (`requestIdRef` generation counter at lines 40/75/78; error-state block with a "Try again" button at lines 280-290) — `AdminPanel.jsx` should follow this shape, not invent a new one.
- `frontend/src/components/RequireRole.jsx` already handles the `/admin` route's client-side gating generically (calls `GET /api/auth/me`, redirects unauthenticated → `/login`, wrong-role → `LANDING_ROUTE[role]`) — no changes needed there, just add the route in `App.jsx` wrapped in it.

### Git Intelligence Summary

Recent commits: `5882f61` (Story 3.1 merge, current `main` tip) → `d84f877` → `442df9f` (Epic 2 retro) → `0b7cbdf` (Story 2.6) → `3e32119`. `5882f61` confirms the established rhythm: create the story on `main`, implement on `story/{epic}.{story}-{slug}`, PR with additions/fixes/test-count summary, merge once both CI jobs are green, delete the branch — push/PR/CI verification left for Jack. Story 3.1 touched only `Repositories/`, `Services/`, and their tests; no `Controllers/`, `Dtos/`, or frontend files — confirmed directly by reading `AccountController.cs` (still one action) and `frontend/src/pages/` (no `Admin*` page exists yet) before writing this story.

### References

- [Source: _bmad-output/planning-artifacts/epics.md §Epic 3 (line 595), §Story 3.1 (line 599), §Story 3.2 (line 627), §Story 3.3 (line 655)] — story statement, five acceptance criteria (verbatim), FR coverage (FR16, FR17, FR34), and Story 3.3's own AC #1 confirming "click → edit popup" is explicitly its scope, not this story's
- [Source: _bmad-output/planning-artifacts/prds/prd-bmad-learning-project-2026-07-21/prd.md §FR16, §FR17, §FR19, §FR34] — exact FR wording: dedicated Admin Panel (FR16); partial name/email match, admin account excluded from the searchable set (FR17); exactly one admin account, never promotable/demotable/deletable (FR34)
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-bmad-learning-project-2026-07-23/DESIGN.md — `admin-account-row` token, Typography section's `{typography.h1}`/`{typography.body-sm}` examples] — row styling tokens (neutral fill, border-tint hover, no border), "Admin Panel" as the canonical h1 example
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-bmad-learning-project-2026-07-23/EXPERIENCE.md — Information Architecture table (line 29), Component Patterns table (admin account row, line 70), State Patterns table (lines 106-107), Responsive & Platform table (line 143)] — IA scope of the Admin Panel route across 3.2-3.5; exact copy for "before any search"/"no results" states; mobile layout instruction (search bar full-width above stacked rows); confirmed no mockup exists for this screen (composition-reference list omits it)
- [Source: _bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/ARCHITECTURE-SPINE.md#AD-1, #AD-2, #AD-4, #AD-7, #AD-15, #AD-18] — layering, role/session liveness enforcement mechanism, testing strategy, PK strategy, soft-delete, client-routing-mirrors-server-gating
- [Source: backend/BarbershopApi/Controllers/AccountController.cs, BookingController.cs; Services/SessionLivenessMiddleware.cs; Program.cs:203-212] — current controller shape, existing manual-role-branch precedent (`GetSchedule`) vs. this story's attribute-based gating choice, middleware ordering that makes the attribute safe to use
- [Source: backend/BarbershopApi/Repositories/AccountRepository.cs:54-69, Services/AccountService.cs:70, Dtos/MeResponse.cs, BarberSummary.cs] — exact current `Search`/`SearchAccounts` implementation this story wires up; DTO precedent this story's new `AccountSummary` follows
- [Source: backend/BarbershopApi.Tests/RoleGatingTests.cs, TestOnly/RoleGateTestController.cs] — proven `[Authorize(Roles = "Admin")]` behavior against this app's exact middleware pipeline; reusable `RegisterAndLoginAs` helper
- [Source: frontend/src/App.jsx, components/NavBar.jsx:20, components/RequireRole.jsx, api/AccountApi.js, api/BookingApi.js:101-133, pages/MySchedule.jsx, pages/MySchedule.css] — existing route/nav/API/page patterns this story extends; confirms the `/admin` nav link is currently dangling (no matching route)
- [Source: _bmad-output/implementation-artifacts/3-1-account-repository-admin-operations.md] — predecessor story: exact `Search`/`SearchAccounts` contract, admin-exclusion guarantee, and Dev Notes structure this story follows
- [Source: _bmad-output/implementation-artifacts/deferred-work.md §Deferred from code review of story-3.1] — confirmed the four items deferred from 3.1's review are explicitly scoped to Stories 3.3/3.4/3.5, not this one
- [Source: project-context.md §Language-Specific Rules (C#, 401/403 split); §Testing Rules; §Naming; §Code organization]

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

- Backend: `dotnet test` — 213/213 passed (7 new `AccountControllerTests` cases for the search endpoint, no regressions).
- Frontend: `npx vitest run` — 20 files / 151 tests passed, including new `AdminPanel.test.jsx` (5 tests). `npx eslint .` clean. `npx prettier --check .` clean after one `--write` pass on `AdminPanel.jsx`.
- RED confirmed before implementation on both sides: backend's 7 new Search tests failed with 404 (no endpoint) before `AccountController.Search` was added; frontend's `AdminPanel.test.jsx` failed to resolve `./AdminPanel` before the component existed.
- One self-caught test bug: the first run of the frontend "Try again" test showed `searchAccounts` called 4 times instead of 2 — the module-level `vi.fn()` from `vi.mock('../api/AccountApi')` was accumulating calls across tests in the file since nothing reset it between tests. Fixed with `searchAccounts.mockReset()` in `beforeEach`; not a component bug.

### Completion Notes List

- Task 1/2 (backend): Added `AccountSummary` DTO and `GET /api/account/search` on the existing `AccountController`, gated with `[Authorize(Roles = "Admin")]` per the story's attribute-based-gating decision. No `Service`/`Repository` changes — `SearchAccounts`/`Search` already existed from Story 3.1. 7 new `AccountControllerTests` cases cover matching, admin-exclusion, blank query, no-match, non-admin 403 (Customer + Barber), and missing-token 401.
- Task 3 (frontend API): Added `searchAccounts(accessToken, query)` to `AccountApi.js`, following `BookingApi.js#getSchedule`'s `URLSearchParams`-omit-when-blank pattern and the project's standard `{ ok, accounts }` / `{ ok: false, status, problem }` envelope.
- Task 4/5 (frontend page): Built `AdminPanel.jsx` + `AdminPanel.css` per the story's exact state/render-order spec (`query`/`searched`/`accounts`/`loading`/`error`, `requestIdRef` generation counter matching `MySchedule.jsx`'s pattern), wired the `/admin` route in `App.jsx` behind `RequireRole roles={['Admin']}` (no `NavBar.jsx` change needed — the link already existed). Result rows are native `<button>` elements with an intentional no-op `onClick` (Story 3.3 owns the edit popup). 5 new `AdminPanel.test.jsx` cases stub `searchAccounts` directly (no MSW, no `fetch` mocking for this component's own behavior).
- Task 6: Re-read `deferred-work.md` in full at kickoff — confirmed none of the currently-open items apply to this story's scope (search-only, no edit/create/delete, no cascade). The four items deferred from Story 3.1's review are explicitly scoped to Stories 3.3/3.4/3.5.
- Task 7: Branch `story/3.2-admin-account-search` already existed from story creation. Push/PR/CI verification intentionally left unchecked for Jack, per standing project practice (see Story 3.1's identical Task 6 precedent).

### File List

**Backend — new:**
- `backend/BarbershopApi/Dtos/AccountSummary.cs`

**Backend — modified:**
- `backend/BarbershopApi/Controllers/AccountController.cs`
- `backend/BarbershopApi.Tests/AccountControllerTests.cs`

**Frontend — new:**
- `frontend/src/pages/AdminPanel.jsx`
- `frontend/src/pages/AdminPanel.css`
- `frontend/src/pages/AdminPanel.test.jsx`

**Frontend — modified:**
- `frontend/src/api/AccountApi.js`
- `frontend/src/App.jsx`

## Change Log

- 2026-08-12: Implemented Story 3.2 (Tasks 1-6) — `AccountController` gained its second action, `GET /api/account/search`, gated with `[Authorize(Roles = "Admin")]` (this codebase's first attribute-based role gate) and backed by the new `AccountSummary` DTO. Frontend gained the `AdminPanel` page (search form, four render states, `admin-account-row` result rows) wired to the previously-dangling `/admin` nav link via `RequireRole`, plus `searchAccounts` in `AccountApi.js`. Backend suite green (213/213); frontend suite green (151/151, 20 files); ESLint and Prettier clean. Task 7 (push/PR/CI verification) intentionally left for Jack per standing project practice.
