# Deferred Work

## Deferred from: story-2.6-admin-schedule-oversight (created 2026-08-10)

- `BarberSeedService.cs` (dev-only, env-var-gated barber account seeding, extended by this story to a second optional slot for manual multi-barber testing) is throwaway scaffolding standing in for a real creation path — delete the whole class (and its `Program.cs` registration) when Story 3.4 ("Admin Creates a Barber Account") ships, since barber accounts can be created through the real Admin Panel UI at that point. No test coverage was ever added for it, by design — nothing to migrate, just remove. [backend/BarbershopApi/Services/BarberSeedService.cs, backend/BarbershopApi/Program.cs]

## Deferred from: code review of story-2.5-barbers-own-schedule-view, round 2 (2026-08-10)

- `MySchedule.jsx`'s `loadDate` always sets `loading = true`, so the post-cancel refresh flashes the whole page to "Loading…" instead of updating just the affected row — pre-existing since the original implementation (not introduced by round 1's fixes); real but low-severity UX rough edge. [frontend/src/pages/MySchedule.jsx:54-68]
- ~~Rapid double-clicks on the date-nav arrows before the first fetch resolves can show a momentarily mismatched date, since neither request is aborted or superseded by a generation counter — narrow window, same unguarded-fetch-ordering pattern already present elsewhere (e.g. `ScheduleAppointment.jsx`); a proper fix (AbortController/request-generation tracking) is a cross-cutting change, not scoped to one page.~~ **Resolved** (2026-08-10) — added a `requestIdRef` generation counter to both `MySchedule.jsx`'s `loadDate` and `ScheduleAppointment.jsx`'s availability-loading (`fetchAvailability`/`refreshAvailability`), so a stale response is discarded once a newer request has started, regardless of resolution order. See Story 2.5's Review Findings and Story 2.4's Change Log for the full details. [frontend/src/pages/MySchedule.jsx, frontend/src/pages/ScheduleAppointment.jsx]
- A 401 (expired session) on the schedule fetch gets the same generic retry banner as any other failure, and "Try again" would just resend the same expired access token indefinitely — pre-existing pattern shared verbatim with `ScheduleAppointment.jsx` (Story 2.4, already shipped, not unique to this story); fixing it belongs across both pages. [frontend/src/pages/MySchedule.jsx:38-52]

## Deferred from: code review of story-2.5-barbers-own-schedule-view (2026-08-10)

- Confirm-cancel in-flight guard (`cancellingId !== null` checked in the same synchronous handler that sets it) is a check-then-set race under a true double-click before React's first re-render commits — inherited unmodified from Story 2.4's `ScheduleAppointment.jsx` (already shipped/reviewed), not introduced by this story. Backend's `TryCancel` still makes it safe (a second call just gets a benign 409), so no data-integrity impact. Fix belongs across both files, not scoped to 2.5 alone. [frontend/src/pages/MySchedule.jsx:98-102, frontend/src/pages/ScheduleAppointment.jsx]
- `BookingApi.js#getSchedule` loses the real HTTP status when `response.ok` is `true` but the body fails to parse as JSON (`status: null` instead of `response.status`) — pre-existing pattern already present in `getBarbers`/`getAvailability`/`getMyAppointments` in the same file; this story correctly matched established style rather than introducing a new shape. [frontend/src/api/BookingApi.js:101-124]

## Deferred from: code review of story-2.4-my-appointments-view-cancel-and-race-safety (2026-08-07)

- `CancelBooking`'s per-exception-type catch-block mapping duplicates `CreateBooking`'s existing catch-block shape verbatim — pre-existing pattern (`CreateBooking` already established this convention); a shared exception-to-`ProblemDetails` helper would need to touch both actions, out of this story's scope. [backend/BarbershopApi/Controllers/BookingController.cs:76-97, :50-67]

## Deferred from: code review of story-2.3-double-booking-and-self-conflict-guards (2026-08-07)

- ~~`DateTime? now = null` optional-parameter test seam on `IBookingService.Create` lets any future caller bypass window validation (or pass a wrong-`DateTimeKind` value and silently corrupt the check)~~ **Resolved** (2026-08-07) — added a shared `ResolveNowEst` guard in `BookingService` that throws `ArgumentException` when a caller-supplied `now` isn't `DateTimeKind.Unspecified`, closing the silent-corruption risk. The optional parameter itself was kept (matches `GetAvailableSlots`'s pre-existing convention) rather than a full `TimeProvider` DI refactor. [backend/BarbershopApi/Services/BookingService.cs:124-133]
- The `now = null` default is declared independently on both `IBookingService.Create` and `BookingService.Create` — C# resolves interface default parameters at the caller's static type, so if the two defaults are ever edited out of sync, behavior would silently diverge by reference type — pre-existing pattern (already true of `GetAvailableSlots`), no live bug today since both defaults agree. [backend/BarbershopApi/Services/IBookingService.cs:8, BookingService.cs:18]
- ~~No validation that `startTime` is actually one of the fixed appointment slots~~ **Resolved** (2026-08-07) — `BookingService.Create` now rejects any `startTime` absent from `FixedSlots` via `InvalidBookingWindowException`, the same 400 path as the other AD-14 window checks. [backend/BarbershopApi/Services/BookingService.cs:29-31]

## Deferred from: code review of story-2-2-customer-books-an-appointment (2026-08-06)

- `BookingController.CreateBooking`'s `catch (Exception)` has no logging before returning 500 — pre-existing pattern, identical to `AccountController`'s and `AuthController`'s no-`ILogger`-anywhere catch-alls. [backend/BarbershopApi/Controllers/BookingController.cs:62-65]

## Deferred from: code review of story-2-1-appointment-entity-and-repository (2026-08-04)

- `BookingService.Create`'s DB-level race backstop (and `Cancel`'s read-then-write race) can't be tested deterministically without a real concurrent request, which AD-4 disallows mocking around — same accepted limitation already logged below for `AuthService` in story-1-4's review, just recurring at the Appointment layer. [backend/BarbershopApi/Services/BookingService.cs:34, backend/BarbershopApi/Repositories/AppointmentRepository.cs]
- A dangling FK insert and a genuine unique-index conflict both raise `SqliteErrorCode 19`, so a bad `CustomerId`/`BarberId` would currently be misreported as `BookingConflictException` instead of a not-found/bad-request error — unreachable today (no Controller exists yet to pass bad ids). Flagged for Story 2.2/2.6 to either validate ids before calling `BookingService.Create` or distinguish the SQLite extended error code. [backend/BarbershopApi/Services/BookingService.cs:34]
- No validation that `CustomerId` is actually `Role.Customer` / `BarberId` is `Role.Barber`, or that they differ (self-booking) — not covered by any AC for this story; genuinely ambiguous whether `BookingService` or the Controller layer should own this (an Admin-driven booking flow in Story 2.6 may need to bypass strict role checks). Flagged for Story 2.2/2.6 to resolve explicitly. [backend/BarbershopApi/Services/BookingService.cs:14-28]
- No format validation on `date`/`startTime` strings in `BookingService.Create` — a malformed string would silently corrupt the ordinal-string comparisons used for `Finished`/upcoming-appointment filtering and index uniqueness. Deferred to Story 2.2's Controller/DTO layer, matching AD-14's established client-convenience/server-enforcement split. [backend/BarbershopApi/Services/BookingService.cs:14]
- Static `TimeZoneInfo.FindSystemTimeZoneById("America/New_York")` has no failure handling; missing tzdata on the host would poison the type for the process lifetime via `TypeInitializationException` — very low real risk given GitHub Actions/Windows dev runners ship full tzdata and NFR7 rules out a minimal-container deploy target. [backend/BarbershopApi/Services/BookingService.cs:12]

## Deferred from: code review of story-1.7-self-service-account-management, round 2 (2026-08-03)

- ~~No rate limiting/lockout on the current-password verification branch...~~ **Resolved** (2026-08-03) — added `PasswordChangePolicy` (same 5-per-15-min sliding window as `LoginPolicy`, counting both failed and successful password-change attempts, keyed by `{ip}:{accountId}`); plain name-only edits are exempted via `RateLimitPartition.GetNoLimiter`. Required reordering `UseRateLimiter()` to after `SessionLivenessMiddleware` so the partition resolver can read the authenticated account id.
- Backend gives an identical "Current password is incorrect." message whether the field was wrong or simply missing [backend/BarbershopApi/Services/AccountService.cs:18-24] — only matters for non-browser callers since the UI already blocks blank submission client-side.
- ~~`AccountService.UpdateOwnProfile` mutates `FirstName`/`LastName` before the current-password check can throw...~~ **Resolved** (2026-08-03) — reordered so both password checks (and hash computation) complete before any property on `account` is mutated; strengthened the three failure-path tests to reload from a fresh `DbContext` and assert the name was never persisted.
- No test covers `currentPassword` supplied without `newPassword` (silently ignored server-side) [backend/BarbershopApi.Tests/AccountServiceTests.cs] — low-value coverage gap.
- `AccountApi.js`'s malformed-body guard reports `status: null` even when `response.ok` was `true` [frontend/src/api/AccountApi.js:31-33] — harmless today since nothing branches on it, but discards information.
- ~~The 401 "session has expired" message is shown but nothing redirects/logs the user out...~~ **Resolved** (2026-08-03) — `Account.jsx` now calls `logout()` and navigates to `/login` with the message passed via router state, same pattern as `Register.jsx`.
- ~~Saving the password section with a filled Current Password but blank New/Confirm Password silently no-ops...~~ **Resolved** (2026-08-03) — `handleSavePasswordClick` now requires a non-empty `newPassword` before opening the confirm popup, showing "New password is required" instead.

## Deferred from: code review of story-1.7-self-service-account-management (2026-08-03)

- Generic `catch (Exception)` with no logging [backend/BarbershopApi/Controllers/AccountController.cs:26] — pre-existing pattern, identical to `AuthController`'s three catch blocks.
- `[StringLength(100)]` validates FirstName/LastName before `.Trim()` runs in the service [backend/BarbershopApi/Dtos/UpdateAccountRequest.cs:8,13] — pre-existing pattern shared with `RegisterRequest`; deliberate per story 1.7's Dev Notes ("AD consistency, not a new policy").
- Validation regexes duplicated verbatim between `RegisterRequest`/`UpdateAccountRequest` rather than shared [backend/BarbershopApi/Dtos/UpdateAccountRequest.cs] — same rationale as above.
- ~~`UpdateMe_two_concurrent_edits_...` test has no explicit synchronization barrier...~~ **Resolved** (2026-08-03) — confirmed flaky in practice when Jack ran `dotnet test` locally; replaced with a deterministic version (`UpdateMe_on_stale_RowVersion_returns_409`) that forces the same conflict via two `DbContext`s instead of hoping two real HTTP requests race.
- `ConfirmPopup` closes before the async save resolves, no loading affordance during the in-flight window [frontend/src/components/ConfirmPopup.jsx:15-18] — UX polish only; double-submit itself is already blocked via `disabled={isSubmitting}` on all Save/Cancel buttons.
- `RequireRole roles={['Customer', 'Barber', 'Admin']}` hardcodes the full `Role` enum to mean "any authenticated user" [frontend/src/App.jsx:28] — brittle if a 4th role is ever added, but matches story 1.7's own Task 5 spec.
- ~~`AccountApi.js`'s `updateAccount` can return `{ok: true, identity: null}` on a malformed 200 body...~~ **Resolved** in round 2 of this same story's review — `AccountApi.js` now explicitly treats a malformed/empty 200 body as a failure (`{ ok: false, status: null }`).

## Deferred from: code review of story-1-6-server-side-role-gating-and-protected-routing (2026-07-31)

- `SessionLivenessMiddleware` 401s any authenticated request regardless of whether the target endpoint requires authorization at all — no current endpoint is reachable this way since no anonymous-but-optionally-authenticated endpoint exists yet in this app's own flows. Worth a `context.GetEndpoint()` metadata check when that need actually arises. [backend/BarbershopApi/Services/SessionLivenessMiddleware.cs:11]
- No test covers the "missing/unparseable `sessionVersion` claim" 401 branch of `SessionLivenessMiddleware`, a path the story's own Task 2 notes call out as a hard requirement to guard — the guarded code (`TryParse` checks) reads as correct, just untested. [backend/BarbershopApi.Tests/RoleGatingTests.cs, MeEndpointTests.cs, RefreshEndpointTests.cs]
- `RequireRole.jsx` has no default/guard for a missing `roles` prop (`roles.includes(...)` would throw) — not reachable today since no route in `App.jsx` uses the component yet. [frontend/src/components/RequireRole.jsx:12,25]

## Deferred from: code review of story-1-5-sign-in-sign-out-and-first-admin-bootstrap, round 2 (2026-07-30)

- Login success banner is captured once via a `useState` initializer on mount — if `Login` is ever reached a second time via client-side navigation with a new `location.state.message` while already mounted, the new message would never render. Not currently a real path in this app's routing. [frontend/src/pages/Login.jsx:21]

## Deferred from: code review of story-1-5-sign-in-sign-out-and-first-admin-bootstrap (2026-07-30)

- Rate limiter throttles every login attempt (success or failure) toward the same 5-per-15-min email+IP cap, not just failed ones — per Jack: the odds of a legitimate user hitting 6 same-email-same-IP logins in 15 minutes are vanishingly low, and if it does happen it reads as spam sign-in behavior anyway, so the false-positive 429 is an acceptable outcome. [backend/BarbershopApi/Program.cs:80-106]
- Rate-limiter partition-key resolver uses a blocking sync-over-async body read (`ReadToEndAsync().GetAwaiter().GetResult()`) — this is the story's own literal Dev Notes — Rate-Limiter Partition-Key Recipe, not a shortcut introduced independently. [backend/BarbershopApi/Program.cs:84]
- Partition-key email lookup (`JsonDocument.TryGetProperty`) is case-sensitive, mismatching ASP.NET's case-insensitive model binder — an email sent with different casing falls into the shared "unknown" bucket instead of its own per-account bucket. [backend/BarbershopApi/Program.cs:91]
- CORS is pinned to `http://localhost:5173` while the new refresh-token cookie requires `Secure` (HTTPS-only) — a scheme mismatch risk for local dev depending on HTTPS cert trust setup. CORS/`UseHttpsRedirection` setup predates this story; `Secure` cookie is spec-mandated by AC #1. [backend/BarbershopApi/Program.cs:27-34, Controllers/AuthController.cs:40-46]
- Frontend tests stub `fetch` with plain object literals rather than real `Response` instances, so nothing asserts `credentials: 'include'`/headers are actually sent — the code itself is correct (verified `AuthApi.js` sets `credentials: 'include'` on every auth fetch per AD-13). [frontend/src/pages/Login.test.jsx, frontend/src/components/NavBar.test.jsx]
- Rate-limit tests likely never exercise the IP half of the partition key, since `TestServer`'s `RemoteIpAddress` is commonly unpopulated under `WebApplicationFactory`. [backend/BarbershopApi.Tests/AuthControllerTests.cs:300]
- `AuthApi.js`'s `loginAccount` would crash on `result.session.role` if a 200 response ever returned a malformed/empty body — unreachable under the server's current contract (always returns a valid `LoginResponse` on 200). [frontend/src/api/AuthApi.js:42]

## Deferred from: code review of story-1-4-customer-self-registration, round 2 (2026-07-30)

- `[StringLength(254)]` on Email checks the raw untrimmed value while `[PlausibleEmail]` trims before its regex check — extremely low-probability edge case, not worth a custom validation attribute at this scale. [backend/BarbershopApi/Dtos/RegisterRequest.cs]
- The DB-constraint race backstop (`SqliteException { SqliteErrorCode: 19 }`) is untested — inherently hard to test deterministically without mocking the DB layer, which AD-4 disallows. [backend/BarbershopApi/Services/AuthService.cs]
- No logging anywhere in `AuthController`'s exception handling — real observability gap, but a bigger infrastructure decision than a patch round; no `ILogger` precedent exists elsewhere yet. [backend/BarbershopApi/Controllers/AuthController.cs]
- `OperationCanceledException` from a client disconnect is caught by the generic `catch (Exception)` and misreported as a 500 rather than propagating as a cancellation — narrow edge case, no real user-facing impact. [backend/BarbershopApi/Controllers/AuthController.cs]

## Deferred from: code review of story-1-4-customer-self-registration (2026-07-30)

- No rate limiting or bot protection on `/api/auth/register` — AD-5 only scopes rate limiting to `/api/auth/login`; registration throttling was never part of this story's or the architecture's mandate. [backend/BarbershopApi/Controllers/AuthController.cs]
- `NavBar`'s `Register` action changed from a `<Link>` to a `<button onClick={...}>`, losing `href` semantics (no open-in-new-tab/middle-click/copy-link) — explicit, spec-sanctioned tradeoff (Task 10 allowed either approach; dev notes document the button-styling rationale). Flagged as a future accessibility polish item only. [frontend/src/components/NavBar.jsx:47-49]

## Deferred from: code review of story-1-3-home-and-about-pages (2026-07-29)

- Home CTA navigates to `/login` (and any unmatched path) with no registered `<Route>`/no catch-all — renders blank until Story 1.5 builds Login; spec explicitly forbids adding a placeholder route now. [frontend/src/App.jsx:12-15]
- `isSignedIn` is never passed by `App.jsx`, so AC#3's signed-in branch is unreachable in the running app, only exercised via direct prop injection in tests — documented temporary auth seam (Stories 1.5/1.6). [frontend/src/pages/Home.jsx:5, frontend/src/App.jsx:13]
- NavBar overflows/doesn't wrap below ~640px, causing horizontal overflow on every page including Home/About — pre-existing (Story 1.1 shell, acknowledged in Dev Notes as Story 1.5's job). [frontend/src/components/NavBar.css]
- `SQLitePCLRaw.lib.e_sqlite3` 2.1.11→2.1.12 bump may not actually contain the CVE-2025-6965 fix (advisory data suggests the fix only ships in the 3.x line) — out of this story's scope (tracked under Story 1.2's changelog), needs external verification. [backend/BarbershopApi/BarbershopApi.csproj:17]
- react-router 8.3.0 requires Node ≥22.22.0 but CI only pins the major version (`node-version: '22'`) — informational risk already flagged in this story's own Dev Notes, not confirmed to break CI. [.github/workflows/ci.yml:31]
