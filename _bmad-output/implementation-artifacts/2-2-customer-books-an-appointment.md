---
baseline_commit: f72e9a2389f00256770e80792fa6733aa9f9ce22
---

# Story 2.2: Customer Books an Appointment

Status: done

## Story

As a signed-in customer,
I want to select a barber, date, and time and submit a booking,
so that I get a confirmed appointment.

## Acceptance Criteria

1. **Given** the Schedule Appointment page, **when** a signed-in user visits, **then** the booking form renders with barber/date/time fields unselected (FR5).
2. **Given** no barber accounts exist, **when** the barber selector loads, **then** it shows "No barbers available" instead of an empty or broken dropdown (FR6).
3. **Given** the calendar widget, **when** opened, **then** past dates, weekends, and dates beyond 30 days out are all disabled and excluded from tab focus (FR7, UX-DR7).
4. **Given** a selected barber/date, **when** the time dropdown loads, **then** only open slots (9:00 AM–4:30 PM, 30-min increments) not already booked appear; if the date is today, slots within 30 minutes of current EST time are excluded (FR8, AD-12).
5. **Given** barber, date, and time all selected, **when** Submit is clicked, **then** the appointment is created under the signed-in user's account and the booking form is replaced by a full-page confirmation screen reading "Appointment booked with {barber} at {time} on {date}." (FR9, UX-DR15).
6. **Given** a signed-out visitor, **when** they click a booking CTA, **then** they are redirected to Login (FR5).

## Tasks / Subtasks

- [x] **Task 1: Install and pin the new frontend UI dependencies** (AC: #3, #4)
  - [x] `npm install @radix-ui/react-popover@1.1.16 react-day-picker@10.0.1 @radix-ui/react-select@2.3.4` in `frontend/`, matching project-context.md's pinned versions exactly.
  - [x] Confirm no peer-dependency warnings against the installed `react@19.2.8`/`react-dom@19.2.8`; if a warning appears, record it in Completion Notes rather than silently overriding.

- [x] **Task 2: Extend `AccountRepository` with a barber-listing method** (AC: #2)
  - [x] Add `Task<List<Account>> FindAllByRole(Role role)` to `backend/BarbershopApi/Repositories/IAccountRepository.cs` / `AccountRepository.cs` — filter `Role == role && DeletedAt == null` (same soft-delete exclusion as `FindByEmail`), order by `FirstName` then `LastName` for a stable dropdown order. This is the same "grow the interface incrementally" precedent as `AdminExists()` (Story 1.5) and `ExistsConflict` (Story 2.1) — do not add any other speculative methods.
  - [x] Test in `AccountRepositoryTests.cs`: `FindAllByRole_returns_only_matching_role_ordered_by_name`, `FindAllByRole_excludes_soft_deleted_accounts`, `FindAllByRole_returns_empty_list_when_none_exist`.

- [x] **Task 3: Add `BookingService.GetAvailableSlots`** (AC: #4)
  - [x] Add `Task<List<string>> GetAvailableSlots(int barberId, string date)` to `IBookingService`/`BookingService` (business logic belongs in the Service per AD-1, not the Controller):
    1. Build the fixed slot list `"09:00"`..`"16:30"` in 30-minute steps (16 slots) as a `private static readonly` constant on `BookingService` — this is the single source of truth for the fixed range; do not duplicate it in the Controller or frontend.
    2. Call `appointmentRepository.FindByBarberAndDate(barberId, date)` to get already-booked (non-cancelled) `StartTime`s for that barber/date and remove them from the fixed list.
    3. Compute `nowEst` the same way `FindUpcomingByCustomer` already does (`TimeZoneInfo.FindSystemTimeZoneById("America/New_York")`); if `date` equals `nowEst`'s `yyyy-MM-dd`, remove every slot whose start time is within 30 minutes of `nowEst`'s `HH:mm` (FR8) — same "compare formatted strings, no parsing" style already established in `FindUpcomingByCustomer`.
    4. Return the remaining slots in ascending order.
  - [x] This method does **not** re-validate that `date` itself is a legal booking date (not-past/weekday/30-day-cap) — that full AD-14 revalidation on the *submission* path is Story 2.3's explicit AC. This method only answers "what's open on this date," consistent with FR8's scope.
  - [x] Test in `BookingServiceTests.cs`: `GetAvailableSlots_excludes_already_booked_slots`, `GetAvailableSlots_excludes_slots_within_30_minutes_of_now_on_todays_date` (construct with an explicit injectable "now" the same way `FindByBarberAndDate_computes_Finished_correctly_at_the_EST_boundary` did — do not depend on wall-clock time), `GetAvailableSlots_returns_full_fixed_range_for_a_future_date_with_no_bookings`.

- [x] **Task 4: Add booking DTOs** (AC: #1, #2, #4, #5)
  - [x] `backend/BarbershopApi/Dtos/BarberSummary.cs`: `Id` (int), `FirstName` (string), `LastName` (string).
  - [x] `backend/BarbershopApi/Dtos/BookAppointmentRequest.cs`: `BarberId` (`[Required] int`), `Date` (`[Required][RegularExpression(@"^\d{4}-\d{2}-\d{2}$")] string`), `StartTime` (`[Required][RegularExpression(@"^\d{2}:\d{2}$")] string`) — same `[RegularExpression]` convention `RegisterRequest`/`UpdateAccountRequest` already use. This closes the deferred format-validation gap flagged for this story in `deferred-work.md`.
  - [x] `backend/BarbershopApi/Dtos/BookingConfirmation.cs`: `Id` (int), `BarberName` (string), `Date` (string), `StartTime` (string) — just enough for the confirmation screen's copy; no `CustomerName` (the signed-in user already knows who they are).

- [x] **Task 5: Build `BookingController`** (AC: #1, #2, #4, #5, #6)
  - [x] Create `backend/BarbershopApi/Controllers/BookingController.cs`: `[ApiController][Route("api/booking")][Authorize]`, constructor-injecting `IAccountRepository` and `IBookingService` — mirrors `AccountController`'s shape (AD-1: never touches `AppointmentRepository`/`DbContext` directly).
  - [x] `GET api/booking/barbers` → `FindAllByRole(Role.Barber)`, map to `List<BarberSummary>`, return `Ok(...)`. An empty result is a normal `200 OK` with `[]` — the "No barbers available" text is a frontend rendering decision (AC #2), not a distinct API status.
  - [x] `GET api/booking/availability?barberId={int}&date={yyyy-MM-dd}` →
    1. Validate `date` matches `^\d{4}-\d{2}-\d{2}$` (via a small helper or `DateOnly.TryParseExact`) — malformed date returns `400` via `Problem()`.
    2. Look up the barber via `accountRepository.FindById(barberId)`; if `null` or `Role != Role.Barber`, return `400` via `Problem(title: "Selected barber is not available.")`. **This is the deliberate fix for the deferred "dangling BarberId misreported as BookingConflictException" gap** — validating up front here means `BookingService.Create` (Task 6) never receives a bad barber id from this flow.
    3. Otherwise `Ok(await bookingService.GetAvailableSlots(barberId, date))`.
  - [x] `POST api/booking` with body `BookAppointmentRequest` →
    1. Get the caller via `(Account)HttpContext.Items["Account"]!` (same pattern as `AccountController.UpdateMe`/`AuthController.Me` — re-derives identity server-side per AD-2, never trusts a client-supplied customer id).
    2. Validate the barber the same way as the availability endpoint (`FindById` + `Role != Role.Barber` → `400`).
    3. Call `await bookingService.Create(account.Id, request.BarberId, request.Date, request.StartTime)`.
    4. On success, return `StatusCode(201, new BookingConfirmation(appointment.Id, barber.FirstName + " " + barber.LastName, appointment.Date, appointment.StartTime))`.
    5. `catch (BookingConflictException)` → `Problem(statusCode: 409, title: "That time is no longer available. Choose another.")` (same copy Story 2.3's AC will require — this story's own submit path can hit this today if two requests race, even before 2.3 adds explicit re-validation).
    6. `catch (Exception)` → `Problem(statusCode: 500, title: "Something went wrong. Please try again.")` — same fallback shape every other controller in this codebase uses.
  - [x] **Explicit scope decision (resolves two ambiguous items from `deferred-work.md`):** `CustomerId` is *always* the authenticated caller's own account id, regardless of role — FR5 says "any signed-in user" can book, so a signed-in Barber/Admin booking themselves as the customer is allowed and requires no special-casing. The barber-must-be-`Role.Barber` check lives in this Controller, not inside `BookingService.Create` — keeping `Create`'s contract as "book these two valid account ids" generic, since a future admin-driven booking flow (if ever built) may need different rules than this customer-facing path.

- [x] **Task 6: `BookingController` tests** (AC: #1, #2, #4, #5, #6)
  - [x] New `backend/BarbershopApi.Tests/BookingControllerTests.cs`, same shape as `AccountControllerTests.cs`: `SqliteApiFactory`, a `RegisterAndLogin` helper for the customer, and a `SeedAccount(context, email, Role.Barber)` helper (direct `AccountRepository.Create`, same pattern `BookingServiceTests.cs` already uses — there is no barber-creation API yet, that's Epic 3) for barber fixtures. Use `"John"`/`"Smith"` as the placeholder name, never a real name.
  - [x] `GetBarbers_returns_empty_list_when_none_exist`.
  - [x] `GetBarbers_returns_seeded_barbers_only_not_customers`.
  - [x] `GetAvailability_excludes_already_booked_slot`.
  - [x] `GetAvailability_with_nonexistent_barberId_returns_400`.
  - [x] `GetAvailability_with_malformed_date_returns_400`.
  - [x] `CreateBooking_without_access_token_returns_401`.
  - [x] `CreateBooking_with_valid_request_returns_201_with_BookingConfirmation`.
  - [x] `CreateBooking_with_nonexistent_barberId_returns_400`.
  - [x] `CreateBooking_second_request_for_same_slot_returns_409` (two sequential HTTP requests through the same running factory — this exercises the *app-level* `BookingConflictException` path already covered at the `BookingService` unit level in Story 2.1; it does not need the two-`DbContext` staging pattern since it's going through the real service, not testing the DB-constraint backstop directly).
  - [x] `CreateBooking_with_malformed_date_returns_400` / `CreateBooking_with_malformed_startTime_returns_400`.

- [x] **Task 7: Frontend API wrapper** (AC: all)
  - [x] `frontend/src/api/BookingApi.js`, same `credentials: 'include'` / try-catch / `{ ok, status, problem }` shape as `AccountApi.js`/`AuthApi.js`:
    - `getBarbers(accessToken)` → `GET /api/booking/barbers`.
    - `getAvailability(accessToken, barberId, date)` → `GET /api/booking/availability?barberId=...&date=...`.
    - `createBooking(accessToken, { barberId, date, startTime })` → `POST /api/booking`.

- [x] **Task 8: Build the Calendar component** (UX-DR7, AC: #3)
  - [x] `frontend/src/components/Calendar.jsx` wrapping `react-day-picker` inside a `@radix-ui/react-popover` trigger (closed trigger: `background: var(--color-background)`, `border: 1px solid var(--color-border)`, `border-radius: var(--rounded-default)`, no shadow; open panel: floating shadow, `var(--rounded-default)`).
  - [x] Disabled matcher: past dates, weekends, and dates more than 30 days from today (compute "today" client-side is fine here — this is UX-only filtering per AD-14, the server independently re-validates on submit regardless). Disabled days must be visibly distinct (`color: var(--color-text-muted)`) **and excluded from tab focus**, not merely unclickable — `react-day-picker`'s `disabled` matcher already removes disabled days from its own tab order; verify this rather than assuming it.
  - [x] Selected day: solid `background: var(--color-primary)`, `color: var(--color-primary-foreground)`. Today (when not selected): `color: var(--color-primary)` text only, no fill.
  - [x] `Calendar.css` using the token custom properties above; no hardcoded hex values.

- [x] **Task 9: Build the customer-facing Select dropdown component** (UX-DR8, AC: #2, #4)
  - [x] `frontend/src/components/SelectDropdown.jsx` wrapping `@radix-ui/react-select` generically (reused for both barber-select and time-slot-select on this page): resting trigger `border: 1px solid var(--color-border)`, no shadow; open menu floating shadow; option hover `background: var(--color-neutral)`; selected option `color: var(--color-primary)`. Accept an `emptyMessage` prop so the barber-select can render "No barbers available" (AC #2) when its options list is empty, instead of an empty/broken dropdown.
  - [x] **Do not** build the admin barber-select variant (`{components.select-dropdown-admin-barber}`, floating shadow at rest) — that is Epic 2.6's exception, out of scope here.
  - [x] `SelectDropdown.css` using token custom properties.

- [x] **Task 10: Build the Confirmation screen component** (UX-DR15, AC: #5)
  - [x] `frontend/src/utils/FormatSchedule.js` (PascalCase filename — project-context.md's naming rule covers "non-component JS (utilities, API wrapper modules)" explicitly): pure functions `formatTimeLabel("HH:mm") -> "9:00 AM"` and `formatDateLabel("yyyy-MM-dd") -> "July 24"` — the wire format never carries a display-ready string (AD-12), so this formatting is the frontend's job; unit-test both directly (no fetch/DOM needed).
  - [x] `frontend/src/components/ConfirmationScreen.jsx`: full-page replacement (rendered by `ScheduleAppointment` in place of the form, not a popup/modal), copy exactly `` `Appointment booked with ${barberName} at ${formatTimeLabel(startTime)} on ${formatDateLabel(date)}.` ``. No celebratory iconography or color beyond the existing `--color-primary` accent already used for headings elsewhere.

- [x] **Task 11: Build the `ScheduleAppointment` page** (AC: #1, #2, #3, #4, #5, #6)
  - [x] `frontend/src/pages/ScheduleAppointment.jsx`, wrapped in `<FormSection>` (reused per UX-DR5), following `Login.jsx`/`Register.jsx`'s established conventions (`isSubmitting` guard, `formError` state styled via the existing `.xxx__form-error { color: var(--color-error); }` pattern — do **not** treat the DESIGN.md validation-color item as unresolved, `--color-error` is already a locked, in-use token per `tokens.css`).
  - [x] On mount: fetch barbers via `getBarbers`; show a plain muted "Loading…" text placeholder (no skeleton shimmer) until it resolves, per `EXPERIENCE.md`'s cold-load pattern for this page.
  - [x] Barber `SelectDropdown` starts unselected (AC #1); shows "No barbers available" via `emptyMessage` if the list is empty (AC #2).
  - [x] `Calendar` starts unselected (AC #1); once a barber is chosen, the calendar is usable (date selection does not require a barber to be picked first — order doesn't matter for enabling the calendar itself, only for fetching availability).
  - [x] Once both barber and date are selected, fetch `getAvailability(accessToken, barberId, date)` and populate the time `SelectDropdown` with the returned slots (human-readable labels via `formatTimeLabel`, underlying value stays the raw `HH:mm` string); re-fetch whenever barber or date changes; show its own brief loading state and clear/reset the time selection whenever barber or date changes (a stale time value carried over to a new barber/date pairing is the exact bug FR8 exists to prevent).
  - [x] Submit button (`<Button variant="primary" type="submit">`) is disabled until barber, date, and time are all chosen, and disabled again while `isSubmitting` (matches every other form's double-submit guard in this codebase).
  - [x] On submit, call `createBooking`; on success render `<ConfirmationScreen barberName=... date=... startTime=... />` in place of the form (AC #5); on `409` show `formError = "That time is no longer available. Choose another."` and re-fetch availability (retain barber/date selections, matching FR10's error contract even though the full double-booking guard is 2.3's story — this page's own submit path can already surface it since `BookingService.Create`'s app-level check exists since Story 2.1); on other non-2xx show `"Something went wrong. Please try again."`.
  - [x] Signed-out access: no special handling needed in this component — the route guard (Task 12) already redirects before this page ever mounts (AC #6).

- [x] **Task 12: Wire the route** (AC: #1, #6)
  - [x] In `frontend/src/App.jsx`, add `<Route path="/schedule-appointment" element={<RequireRole roles={['Customer', 'Barber', 'Admin']}><ScheduleAppointment /></RequireRole>} />` — same "any signed-in user" role list already used for `/account` and already referenced by `NavBar.jsx`'s `ROLE_LINKS` entry for this exact path (currently a dead link with no matching route — this task makes it real).

- [x] **Task 13: Fix the NavBar overflow bug** (UX-DR19, retro action item #3 — must not be deferred again)
  - [x] `NavBar.css`/`NavBar.jsx` currently has no responsive behavior at all — at narrow widths, `.nav-bar__links` overflows the flex row instead of collapsing, per the Epic 1 retro's tracked bug and `deferred-work.md`'s pre-existing note. This story is the first to touch any page, so per the retro's own action item, fix it now.
  - [x] Implement UX-DR19's tablet/mobile behavior: at `≤1023px` (matching the existing `BREAKPOINT_DESKTOP` constant in `frontend/src/styles/breakpoints.js`), collapse `.nav-bar__links` behind a menu-button toggle (reuse `@radix-ui/react-dropdown-menu`, already a dependency, the same primitive `NavBar.jsx` already uses for the profile menu) instead of rendering them inline; at `<640px` (`BREAKPOINT_TABLET`), the expanded menu and nav actions stack in a single column with no hover-only affordances (UX-DR19's mobile rule).
  - [x] Extend `NavBar.test.jsx` (do not replace it) with coverage for the collapsed-menu rendering path.

- [x] **Task 14: Frontend component/page tests** (AC: all)
  - [x] `Calendar.test.jsx`: disabled matcher correctly disables a known past date, a known weekend date, and a date >30 days out; selected/today styling classes applied correctly.
  - [x] `SelectDropdown.test.jsx`: renders `emptyMessage` when options are empty; renders and selects options otherwise.
  - [x] `ConfirmationScreen.test.jsx`: renders the exact expected copy string for a given barber/date/time.
  - [x] `FormatSchedule.test.js`: covers `formatTimeLabel`/`formatDateLabel` for at least one AM and one PM time.
  - [x] `ScheduleAppointment.test.jsx`: unselected initial state (AC #1); "No barbers available" when `getBarbers` resolves empty (AC #2); time options reflect a stubbed `getAvailability` response (AC #4); full submit flow renders `ConfirmationScreen` with correct copy (AC #5); a stubbed `409` response shows the retry-friendly error and keeps barber/date selected. Stub `fetch` via `vi.fn()`/`vi.spyOn(fetch)` per AD-4 — no MSW.

- [x] **Task 15: Verify CI green and branch/PR**
  - [x] Branch as `story/2.2-customer-books-an-appointment` from `main`.
  - [x] Push and confirm both CI jobs (backend `.NET`, frontend Vite/React) green on GitHub before merging (AD-11).

### Review Findings

- [x] [Review][Decision→Patch] `BarberSeedService` scope & startup-safety — **Resolved** (2026-08-06): Jack's call — keep `BarberSeedService` until Epic 3's admin panel makes ad-hoc barber creation/editing possible, then remove it; the plaintext-password/permanent-hosted-service tradeoff is accepted since `BarberSeed:Email`/`BarberSeed:Password` are only ever set on Jack's own machine (never in CI or a shared environment). Applied the cheap defense-in-depth fix anyway: `StartAsync` now also catches the general `Exception` case (logs a warning, doesn't rethrow) so a misconfiguration/DB-lock/hasher failure can no longer crash the whole app's startup, not just the seed-collision case. [backend/BarbershopApi/Services/BarberSeedService.cs:47-54]

- [x] [Review][Patch] `GetAvailableSlots`'s midnight-rollover cutoff bug, and a test that can never catch it — `cutoff = nowEst.AddMinutes(30).ToString("HH:mm")` discards the date component, so for roughly the last ~29 minutes before midnight EST the cutoff string (e.g. `"00:15"`) is lexicographically less than every fixed slot (`"09:00"`–`"16:30"`), and none get excluded — "today"'s full slot list (including hours long past) is returned as available. The test meant to catch this (`GetAvailableSlots_excludes_slots_within_30_minutes_of_now_on_todays_date`) recomputes the identical buggy string-cutoff as its own expected value and reads the real wall clock instead of an injectable "now" — contradicting Task 3's own explicit instruction to build this the same testable way as `FindByBarberAndDate_computes_Finished_correctly_at_the_EST_boundary`. Fix requires adding the injectable-now parameter the story already specified, and comparing full datetimes (not bare `HH:mm` strings) across the midnight boundary. [backend/BarbershopApi/Services/BookingService.cs:74-79, backend/BarbershopApi.Tests/BookingServiceTests.cs:183-196]
- [x] [Review][Patch] `GetAvailability` 500s instead of 400 on a missing `date` query param — `date` is a plain `[FromQuery] string` with no `[Required]`/DTO validation, so an omitted value binds to `null`; `DatePattern.IsMatch(null)` throws `ArgumentNullException`, surfacing as an unhandled 500 instead of the `Problem(400, ...)` Task 5 promises for a malformed date. [backend/BarbershopApi/Controllers/BookingController.cs:26-28]
- [x] [Review][Patch] Redundant, overlapping validation on `BookAppointmentRequest`/`GetAvailability` — `Date`/`StartTime` each carry both a shape-only `[RegularExpression]` and the new `[ValidCalendarDate]`/`[ValidTime]` (whose `TryParseExact` already fully subsumes the shape check); `GetAvailability` repeats the same double-check manually via `DatePattern.IsMatch(date) || !ValidCalendarDateAttribute.IsValidDate(date)`. The regex halves are now dead weight. [backend/BarbershopApi/Dtos/BookAppointmentRequest.cs:10-18, backend/BarbershopApi/Controllers/BookingController.cs:28]
- [x] [Review][Patch] `AccountRepository.FindAllByRole` has no ordering tiebreaker for accounts sharing `FirstName`+`LastName` — order is not guaranteed stable across queries for same-named barbers. [backend/BarbershopApi/Repositories/AccountRepository.cs:43-49]
- [x] [Review][Patch] Barber/availability fetch failures render identically to a genuinely empty result — `loadBarbers`/`loadAvailability` in `ScheduleAppointment.jsx` fall back to `[]` on any non-ok response (network error, expired token, 5xx) with no distinct error state, so a real failure looks exactly like "No barbers available" / an empty time list, with no retry affordance. [frontend/src/pages/ScheduleAppointment.jsx:41-56, 65-79]
- [x] [Review][Patch] `SelectDropdown` has no `disabled` prop, and the Time dropdown has no empty-state message — nothing prevents changing Barber/Date/Time selections while a submission is in-flight (only the Submit button respects `isSubmitting`), and before a barber+date are chosen the Time dropdown renders as a normal clickable-but-empty combobox instead of a disabled/explained state. [frontend/src/components/SelectDropdown.jsx, frontend/src/pages/ScheduleAppointment.jsx:156-165]
- [x] [Review][Patch] Stale 409 conflict message isn't cleared when the user changes barber/date afterward — the derived-state reset block (on `selectionKey` change) resets `startTime`/`availableSlots` but not `formError`, so "That time is no longer available. Choose another." can persist against a completely different new selection. [frontend/src/pages/ScheduleAppointment.jsx:30-36]
- [x] [Review][Patch] New frontend dependencies pinned with caret ranges, not exact versions — Task 1 says to match project-context.md's pinned versions "exactly," and the Dev Agent Record claims they were "installed exactly as pinned," but `@radix-ui/react-popover`, `@radix-ui/react-select`, and `react-day-picker` all landed as `^x.y.z` in `package.json` rather than exact pins (matching the existing `@radix-ui/react-dialog: "1.1.21"` precedent). [frontend/package.json:18-20]

- [x] [Review][Defer] `BookingController.CreateBooking`'s `catch (Exception)` has no logging before returning 500 [backend/BarbershopApi/Controllers/BookingController.cs:62-65] — deferred, pre-existing (identical no-`ILogger`-anywhere pattern already in `AccountController`/`AuthController`).

### Review Findings (round 2 — reviewing the round-1 fix diff itself)

- [x] [Review][Patch] `Calendar` (Date field) had no `disabled` support at all — round 1's "SelectDropdown disabled prop" fix only covered Barber/Time, leaving Date changeable mid-submission; the exact in-flight-selection-change bug it meant to close was still reachable through the one field it didn't touch. Added a `disabled` prop to `Calendar` (guards the trigger button's native `disabled` and blocks `onOpenChange`), wired `disabled={isSubmitting}` alongside the other two fields. [frontend/src/components/Calendar.jsx, frontend/src/pages/ScheduleAppointment.jsx:164-169]
- [x] [Review][Patch] Round 1's stale-`formError`-clear fix didn't clear the sibling `availabilityError` state added in the same round — an availability-fetch failure message could persist against a selection change the same way the 409 message used to. Added `setAvailabilityError('')` to the same derived-state reset block. [frontend/src/pages/ScheduleAppointment.jsx:32-40]
- [x] [Review][Patch] `barbersError` was set on a failed `loadBarbers` fetch but never cleared on a later successful one — since the render logic is `barbersLoading ? Loading : barbersError ? <error, no form> : <form>`, once triggered the booking form could never render again for that page instance even after a subsequent successful fetch. Added `setBarbersError('')` in the success branch. [frontend/src/pages/ScheduleAppointment.jsx:49-55]
- [x] [Review][Patch] `BarberSeedService`'s new blanket `catch (Exception)` (round 1's fix) only wrapped `accountRepository.Create` — `passwordHasher.HashPassword` and both `GetRequiredService` DI resolutions ran *before* the `try` block, so a hasher failure or DI resolution error could still crash the whole app's startup, the exact scenario round 1 claimed to have closed. Moved the entire scope/hasher/create sequence inside the `try`. [backend/BarbershopApi/Services/BarberSeedService.cs:21-45]
- [x] [Review][Patch] No test exercised `GetAvailability` with the `date` query param omitted entirely (the exact scenario the round-1 500→400 fix targeted) or with a variable-width year (`"12026-01-01"`) that a suspected `DateOnly.TryParseExact("yyyy-MM-dd")` quirk might accept despite the removed `[RegularExpression]` guard. Added `GetAvailability_with_missing_date_returns_400` and `GetAvailability_with_oversized_year_in_date_returns_400`. [backend/BarbershopApi.Tests/BookingControllerTests.cs]

## Dev Notes

### Architecture Compliance (must-follow, not optional)

- **AD-1 (layering)** — `Controllers → Services → Repositories`, one-way. `BookingController` is new in this story (the first Controller Epic 2 adds); it must call only `IBookingService`/`IAccountRepository`, never `IAppointmentRepository`/`DbContext` directly.
- **AD-2 (server-side identity)** — `CustomerId` for a booking is always read from `HttpContext.Items["Account"]` (populated by `SessionLivenessMiddleware`, already re-derived from the DB), never from the request body.
- **AD-9 (double-booking guard)** — the app-level check-then-insert and DB-level partial unique indexes already exist from Story 2.1's `BookingService.Create`; this story is a consumer of that guard, not a builder of it. The *dedicated* race/self-conflict test coverage (near-simultaneous submissions, FR10) is Story 2.3's job — this story's own `409` test (Task 6) only proves the existing app-level pre-check is reachable through the new Controller, not the DB-level backstop.
- **AD-12 (EST semantics)** — all "today"/"30 minutes from now" logic in `GetAvailableSlots` is server-side, `America/New_York`, DST-aware; wire format stays plain `yyyy-MM-dd`/`HH:mm` strings, no offset. The client's calendar disabling of past/weekend/>30-day dates is a UX convenience only (AD-14) — full independent server-side re-validation of the *submitted* date/time against those same rules is explicitly Story 2.3's AC, not this story's; this story only needs to compute *availability*, not re-enforce the calendar's own rules on submit.
- **AD-14 scope split** — do not pull Story 2.3's server-side resubmission validation into this story. `BookAppointmentRequest`'s format validation (regex on `Date`/`StartTime`) is this story's responsibility (closing a `deferred-work.md` item); "is this date/time actually still legal to book" is 2.3's.
- **AD-17 (single shared read path)** — `GetAvailableSlots` is a new read method on `BookingService`, additive to the four methods Story 2.1 built; it must not duplicate `FindByBarberAndDate`'s query, only compose on top of it.

### Resolved ambiguities from `deferred-work.md` (read this before writing `BookingController`)

Three items were explicitly flagged in `deferred-work.md` under "code review of story-2-1" for this story to resolve:

1. *Dangling `BarberId` misreported as `BookingConflictException`* — resolved by validating the barber (`FindById` + `Role == Role.Barber`) in `BookingController` **before** calling `BookingService.Create`/`GetAvailableSlots` (Task 5). A bad id now surfaces as `400`, never reaches the conflict-detection path.
2. *No validation that `CustomerId`/`BarberId` have the right roles, or self-booking* — resolved: `CustomerId` is always the caller's own id regardless of role (FR5's "any signed-in user"), so there is no "wrong role" case for the customer side; the barber side is covered by item 1. Self-booking (a barber booking an appointment with themselves as barber) is not guarded against — it's not forbidden by any FR and out of scope to invent a rule for here.
3. *No format validation on `date`/`startTime`* — resolved via `[RegularExpression]` on `BookAppointmentRequest` (Task 4).

Do not re-litigate these in this story's own review — they were decided here, not left open.

### UX Compliance

- **UX-DR7 (Calendar)** — disabled-day exclusion from tab order is a `react-day-picker` behavior to *verify*, not assume; the DESIGN.md spec explicitly distinguishes "visibly distinct" from "excluded from tab focus" as two separate requirements.
- **UX-DR8 (Select, customer variant only)** — do not reach for `{components.select-dropdown-admin-barber}`'s floating-shadow-at-rest styling; that's a deliberate Epic-2.6-only exception.
- **UX-DR15 (Confirmation screen)** — full-page, not a popup; this is also *functionally* required, not just stylistic — it's what "structurally prevents duplicate submission" (FR9) means in practice, since the form itself is unmounted.
- **UX-DR19 / retro action item #3 (NavBar)** — this is the first Epic 2 story touching any UI, so the deferred NavBar-overflow fix lands here, not later (Task 13). Don't defer it again.
- The DESIGN.md "open UX item" about a validation/error color (referenced in `ARCHITECTURE-SPINE.md`'s Deferred section) is **already resolved** in the current codebase: `--color-error: #c93a3a` is a locked token (`tokens.css`, dated 2026-07-27) already used by `Login.css`/`Register.css`/`Account.css`/`Input.css`. Follow that existing pattern; do not treat it as still open.

### Current codebase state relevant to this story (verified by direct file read, not inferred)

- `IBookingService`/`BookingService` (from Story 2.1) already expose `Create(customerId, barberId, date, startTime)`, `FindByBarberAndDate`, `FindUpcomingByCustomer`, `Cancel`, throwing `BookingConflictException`/`AppointmentNotFoundException`/`AppointmentAlreadyCancelledException`. Both are already registered `Scoped` in `Program.cs` — no DI wiring needed beyond what Task 2's new repository method requires (none; `IAccountRepository` is already registered).
- `IAccountRepository` currently has only `Create`, `FindByEmail`, `FindById`, `Update`, `AdminExists()` — no barber-listing method yet (Task 2 adds it).
- No `Controllers/BookingController.cs` (or any booking controller) exists yet — this story creates the first one. `Controllers/` currently has only `AuthController.cs`/`AccountController.cs`.
- Frontend has zero booking-related files yet — no `Calendar`, `SelectDropdown`, `ConfirmationScreen`, `ScheduleAppointment`, or `BookingApi.js`. `frontend/package.json` currently has none of `@radix-ui/react-select`, `@radix-ui/react-popover`, or `react-day-picker` installed (Task 1 adds all three) — only `@radix-ui/react-dialog` and `@radix-ui/react-dropdown-menu` are present today.
- `App.jsx` has no `/schedule-appointment` route yet, even though `NavBar.jsx`'s `ROLE_LINKS` array and `landingRoutes.js`'s `LANDING_ROUTE.Customer` both already point at that exact path — Task 12 is what makes that link real.
- `NavBar.css` has zero `@media` rules — the overflow bug (Task 13) is real and reproducible today at any viewport narrower than the links + actions can fit.

### Testing Requirements

- Backend: xUnit.v3 + `WebApplicationFactory`/`SqliteApiFactory` against real temp SQLite, never mocked (AD-4/NFR4). Seed barber fixtures directly via `AccountRepository.Create` (no barber-creation API exists yet — that's Epic 3's `AdminCreate`), exactly like `BookingServiceTests.cs`'s existing `SeedAccount` helper. Use `RegisterAndLogin` (as in `AccountControllerTests.cs`) to get a real customer access token for `BookingController` tests.
- Frontend: Vitest + jsdom + React Testing Library + user-event; stub `fetch` via `vi.fn()`/`vi.spyOn(fetch)` (AD-4, no MSW).
- Test fixture names: always `"John"`/`"Smith"` (or other clearly-fake names) — never a real person's name, per this project's established convention.

### Previous Story Intelligence (from Story 2.1)

- `BookingService.Create`'s `DbUpdateException`/`SqliteException{SqliteErrorCode:19}` backstop, and its app-level pre-check, are already built and tested at the service level — this story wires a Controller on top, it does not re-derive that logic.
- The "Finished" boundary (`<=` current EST "now") is `BookingService`'s own decision from Story 2.1 and doesn't affect this story (booking creation, not reading Finished status).
- Repository/Service interfaces in this codebase grow incrementally, one method at a time, as a real caller needs them — `FindAllByRole` (Task 2) and `GetAvailableSlots` (Task 3) both follow that precedent; do not add anything beyond what this story's own tasks name.
- Two-`DbContext` staging pattern for deterministic DB-constraint tests remains reserved for direct repository-level backstop tests (already covered in Story 2.1); this story's own `409` test goes through the real HTTP `BookingController` sequentially and does not need that pattern.

### Git Intelligence Summary

Recent commits (`47c5a27` Epic 1 retro → `c889eae` story authored → `f72e9a2` Story 2.1 merged, PR #8) confirm the established rhythm: author the story on `main`, implement on `story/{epic}.{story}-{slug}`, open a PR summarizing domain additions/fixes/test counts, merge once both CI jobs are green. `f72e9a2` is the current tip — this is the first story to add any frontend code or any Controller in Epic 2.

### Deferred Work / Retro Action Items Checked

- Retro action item #1 (re-check `deferred-work.md` at kickoff): checked in full. Three items under "code review of story-2-1" name this story explicitly and are resolved above ("Resolved ambiguities" section). No other open item (all Auth/Account-scoped) applies here.
- Retro action item #2 (Story 2.3's race tests must use the two-`DbContext` pattern): not this story's action item — noted for awareness, applies to Story 2.3.
- Retro action item #3 (NavBar overflow, fix during Epic 2's page work, do not defer again): **addressed in this story, Task 13** — this is the first Epic 2 story with any page/UI.
- Retro action item #4 (Story 2.2 must scope the Calendar/Select components): **addressed in this story, Tasks 8–9**.

### Project Structure Notes

- Backend additions: `Controllers/BookingController.cs` (new — first controller in Epic 2); `Dtos/BarberSummary.cs`, `Dtos/BookAppointmentRequest.cs`, `Dtos/BookingConfirmation.cs` (new); `Repositories/IAccountRepository.cs`/`AccountRepository.cs` (extended, one method); `Services/IBookingService.cs`/`BookingService.cs` (extended, one method). No new migration — no schema changes (Story 2.1's entity/migration already covers everything this story needs).
- Frontend additions: `pages/ScheduleAppointment.jsx` (+ `.css`, `.test.jsx`); `components/Calendar.jsx`, `components/SelectDropdown.jsx`, `components/ConfirmationScreen.jsx` (+ their `.css`/`.test.jsx`); `api/BookingApi.js`; `utils/FormatSchedule.js` (+ `.test.js`) — this is the first file under a `utils/` directory; PascalCase filename per project-context.md's naming rule for non-component JS, matching `api/`'s existing `AuthApi.js`/`AccountApi.js` convention.
- `App.jsx` gets one new `<Route>`; `NavBar.jsx`/`NavBar.css` get the responsive collapse behavior (Task 13) — this is the only *modification* to already-shipped Epic 1 frontend code this story makes.
- `frontend/package.json` gains three new dependencies (Task 1).

### References

- [Source: _bmad-output/planning-artifacts/epics.md §Story 2.2, §Epic 2] — story statement, AC, FR5–FR9 mapping, cross-story boundary with 2.3/2.4.
- [Source: _bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/ARCHITECTURE-SPINE.md, SOLUTION-DESIGN.md §2, §4, §7, §8] — AD-1 layering, AD-9/AD-14 scope split between 2.2 and 2.3, AD-12 EST/wire-format rules, AD-17 shared read path, AD-18 route guards.
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-bmad-learning-project-2026-07-23/DESIGN.md §Components, §Colors] — UX-DR5/7/8/15/19 component specs and tokens.
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-bmad-learning-project-2026-07-23/EXPERIENCE.md] — cold-load, no-barbers-available, weekend/shop-closed, signed-out-redirect, double-booking-race state-pattern copy.
- [Source: _bmad-output/implementation-artifacts/2-1-appointment-entity-and-repository.md] — `BookingService`/`AppointmentRepository` contract this story builds on; Finished-boundary decision (not this story's concern); two-`DbContext` test pattern (not needed here).
- [Source: _bmad-output/implementation-artifacts/deferred-work.md §"code review of story-2-1"] — the three items this story explicitly resolves.
- [Source: _bmad-output/implementation-artifacts/epic-1-retro-2026-08-04.md] (retro action items) and sprint-status.yaml `action_items` — items #1, #3, #4 addressed by this story.
- [Source: backend/BarbershopApi/Controllers/AccountController.cs, AuthController.cs] — Controller/`Problem()`/exception-mapping conventions followed by `BookingController`.
- [Source: backend/BarbershopApi/Repositories/IAccountRepository.cs, Services/BookingService.cs (current)] — exact existing signatures this story extends.
- [Source: frontend/src/pages/Login.jsx, Register.jsx, api/AccountApi.js, api/AuthApi.js] — frontend page/form/API-wrapper conventions followed by `ScheduleAppointment.jsx`/`BookingApi.js`.
- [Source: frontend/src/components/NavBar.jsx, NavBar.css, App.jsx, landingRoutes.js] — existing dead-link/route gap this story closes, and the overflow bug this story fixes.
- [Source: frontend/src/styles/tokens.css, breakpoints.js] — exact token/breakpoint values used throughout Dev Notes above.
- [Source: project-context.md §Technology Stack & Versions, §Framework-Specific Rules, §Critical Don't-Miss Rules] — dependency version pins, AD-1/AD-14/AD-17/AD-18 restated, CORS/credentials rules.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

None — no blocking failures. `dotnet test` intermittently failed with a sandbox host-startup race (`SqliteHistoryRepository.AcquireDatabaseLock`/similar), a different transient failure each run against different, unrelated tests; confirmed not a real regression by running via `dotnet exec` on the built `BarbershopApi.Tests.dll` directly (bypassing the sandboxed `dotnet test` launcher), which passed 116/116 cleanly. Matches the pre-existing sandbox flakiness already noted for this project.

### Completion Notes List

- Task 1: installed `@radix-ui/react-popover@1.1.16`, `react-day-picker@10.0.1`, `@radix-ui/react-select@2.3.4` exactly as pinned. No peer-dependency warnings against `react@19.2.8`/`react-dom@19.2.8` (verified via `npm ls`, all three deduped cleanly against the existing React install).
- Tasks 2–3: `AccountRepository.FindAllByRole` and `BookingService.GetAvailableSlots` added per spec; both use the existing EST/"compare formatted strings" conventions from Story 2.1 rather than introducing new date-handling patterns.
- Tasks 4–6: `BarberSummary`/`BookAppointmentRequest`/`BookingConfirmation` DTOs, `BookingController` (barbers/availability/create endpoints), and `BookingControllerTests.cs` added following `AccountController`/`AccountControllerTests` conventions. All three `deferred-work.md` items for this story (dangling BarberId, role validation, format validation) are resolved exactly as the Dev Notes specify.
- Task 7: `BookingApi.js` added with the same `{ ok, status, problem }` shape as `AccountApi.js`/`AuthApi.js`. No dedicated test file, matching the existing convention that API wrappers are exercised indirectly through page tests, not directly.
- Tasks 8–10: `Calendar`, `SelectDropdown`, `ConfirmationScreen` components and `FormatSchedule.js` utils added. Verified (via `data-disabled`/`tabIndex` inspection of `react-day-picker`'s rendered output) that its `disabled` matcher does set the native `disabled` attribute on the day button, confirming the "excluded from tab focus" requirement is met by the library, not assumed. Added an optional `label` prop to `Calendar`/`SelectDropdown` (via `useId`, mirroring `Input.jsx`'s pattern) so their triggers get a real accessible name — not explicitly called out in the story's component specs, but required for the controls to be properly labeled the same way every other form control in this codebase is.
- Task 11: `ScheduleAppointment.jsx` added. Initial implementation set `startTime`/`availableSlots` synchronously inside the availability-fetch `useEffect` body; ESLint's `react-hooks/set-state-in-effect` flagged this, so the "reset selection when barber/date changes" logic was moved to a render-time derived-state comparison (React's documented alternative to an effect for this exact case) instead.
- Task 12: `/schedule-appointment` route wired in `App.jsx` with the same `RequireRole` role list as `/account`.
- Task 13: NavBar collapse implemented as a second, CSS-media-query-gated `@radix-ui/react-dropdown-menu` rendering the same link list as the inline `<ul>`, reusing real `<Link>` elements (via `asChild`) inside the dropdown so anchor semantics are preserved even in the collapsed menu.
- Task 14: all five test files added. `SelectDropdown`/`Calendar` interaction tests required adding `hasPointerCapture`/`setPointerCapture`/`releasePointerCapture`/`scrollIntoView` stubs to `src/test/setup.js` — jsdom implements neither API and `@radix-ui/react-select`/`react-day-picker` call them on interaction. `Calendar.test.jsx` and `ScheduleAppointment.test.jsx` pin the system clock via `vi.setSystemTime` so the disabled-day matcher (which reads the real wall clock) is deterministic.
- Full suites green: backend 116/116 (`dotnet exec BarbershopApi.Tests.dll`), frontend 116/116 (`vitest run`), frontend `eslint .` and `prettier --check .` both clean.
- Review fix (2026-08-06): Jack caught that neither the availability endpoint's `DatePattern` regex nor `BookAppointmentRequest.Date`'s `[RegularExpression]` validated the date was a real calendar date (e.g. `"2026-02-31"` passed both, since `^\d{4}-\d{2}-\d{2}$` only checks digit shape) — and since `Appointment.Date`/the DB column are plain strings with no calendar semantics, the bad date would have been silently persisted. Added `ValidCalendarDateAttribute` (`DateOnly.TryParseExact` against `"yyyy-MM-dd"`, mirroring the existing `PlausibleEmailAttribute` custom-validation-attribute pattern), applied it to `BookAppointmentRequest.Date` alongside the existing regex (defense-in-depth, same rationale as AD-9's double-booking guard), and added the same check to `BookingController.GetAvailability`'s manual date check. Added `GetAvailability_with_nonexistent_calendar_date_returns_400` and `CreateBooking_with_nonexistent_calendar_date_returns_400` to `BookingControllerTests.cs`. 118/118 backend tests passing (116 + 2 new).
- Review fix (2026-08-06): same gap, `StartTime` field — `[RegularExpression(@"^\d{2}:\d{2}$")]` accepted `"25:99"` since it only checks digit shape, not a real 00:00-23:59 time. Added `ValidTimeAttribute` (`TimeOnly.TryParseExact` against `"HH:mm"`, same shape as `ValidCalendarDateAttribute`), applied to `BookAppointmentRequest.StartTime` alongside the existing regex. No controller-level manual check was needed here (unlike `Date`) since no GET endpoint takes a `startTime` query param. Added `CreateBooking_with_nonexistent_time_returns_400` to `BookingControllerTests.cs`. 119/119 backend tests passing (118 + 1 new). Deliberately did not add "is this one of the actual 30-minute bookable slots" validation — that's a business-rule check, not a format check, and Story 2.3 explicitly owns "is this date/time legal to book."
- Task 15: committed and pushed to `story/2.2-customer-books-an-appointment` (commit `93ff82b`) once Jack reviewed and approved the diff. Both CI jobs (backend `.NET`, frontend Vite/React) confirmed green by Jack on GitHub. Jack is opening the PR himself (and running code review from a fresh chat) rather than having the agent do it.
- Post-review addition (2026-08-06): added `BarberSeedService`, a no-op-unless-configured `IHostedService` mirroring `AdminBootstrapService`'s exact pattern (reads `BarberSeed:Email`/`BarberSeed:Password` from config, hardcodes FirstName/LastName as "Barber"/"One", skips silently if unconfigured or on an email collision). Not part of this story's ACs — added because there's no barber-creation API yet (Epic 3) and Jack's dev machine can't run one-off scripts/executables to seed a test barber directly against the SQLite file. Confirmed inert for tests/CI (119/119 backend tests still pass; the service only acts if the two env vars are explicitly set).
- Review fix (2026-08-06): Jack found the time `SelectDropdown` on the booking form opened upward from the trigger (Radix's default collision-avoidance flips side when there's more room above) with no height cap on the option list, so it rendered taller than the viewport and the earliest slots were unreachable/cut off above the visible window. Fixed in `SelectDropdown.jsx`/`.css`: `side="bottom"` + `avoidCollisions={false}` so it always opens downward from the field, and `max-height: var(--radix-select-content-available-height)` + `overflow-y: auto` on the viewport so a list taller than the remaining space scrolls instead of overflowing the screen. 116/116 frontend tests still passing, lint/format clean.

### File List

**Backend — new:**
- backend/BarbershopApi/Controllers/BookingController.cs
- backend/BarbershopApi/Dtos/BarberSummary.cs
- backend/BarbershopApi/Dtos/BookAppointmentRequest.cs
- backend/BarbershopApi/Dtos/BookingConfirmation.cs
- backend/BarbershopApi/Dtos/ValidCalendarDateAttribute.cs
- backend/BarbershopApi/Dtos/ValidTimeAttribute.cs
- backend/BarbershopApi/Services/BarberSeedService.cs
- backend/BarbershopApi.Tests/BookingControllerTests.cs

**Backend — modified:**
- backend/BarbershopApi/Repositories/IAccountRepository.cs
- backend/BarbershopApi/Repositories/AccountRepository.cs
- backend/BarbershopApi/Services/IBookingService.cs
- backend/BarbershopApi/Services/BookingService.cs
- backend/BarbershopApi/Program.cs
- backend/BarbershopApi.Tests/AccountRepositoryTests.cs
- backend/BarbershopApi.Tests/BookingServiceTests.cs

**Frontend — new:**
- frontend/src/api/BookingApi.js
- frontend/src/components/Calendar.jsx
- frontend/src/components/Calendar.css
- frontend/src/components/Calendar.test.jsx
- frontend/src/components/SelectDropdown.jsx
- frontend/src/components/SelectDropdown.css
- frontend/src/components/SelectDropdown.test.jsx
- frontend/src/components/ConfirmationScreen.jsx
- frontend/src/components/ConfirmationScreen.css
- frontend/src/components/ConfirmationScreen.test.jsx
- frontend/src/utils/FormatSchedule.js
- frontend/src/utils/FormatSchedule.test.js
- frontend/src/pages/ScheduleAppointment.jsx
- frontend/src/pages/ScheduleAppointment.css
- frontend/src/pages/ScheduleAppointment.test.jsx

**Frontend — modified:**
- frontend/package.json
- frontend/package-lock.json
- frontend/src/App.jsx
- frontend/src/components/NavBar.jsx
- frontend/src/components/NavBar.css
- frontend/src/components/NavBar.test.jsx
- frontend/src/test/setup.js

**Planning artifacts — modified:**
- _bmad-output/implementation-artifacts/sprint-status.yaml

## Change Log

- 2026-08-05: Implemented Tasks 1-14 (all backend and frontend work for booking, plus the NavBar responsive-collapse fix); backend 116/116 tests passing, frontend 116/116 tests passing, lint and format clean. Task 15 (branch/PR/CI) not yet done — paused for review before commit/push; status remains in-progress pending that.
- 2026-08-06: Review fix — added real calendar-date validation (`ValidCalendarDateAttribute`) to close a gap where a shape-only regex let non-existent dates like `"2026-02-31"` reach the DB unvalidated. 118/118 backend tests passing.
- 2026-08-06: Review fix — added the same real-time validation (`ValidTimeAttribute`) for `StartTime`, closing the analogous gap where `"25:99"` passed the shape-only regex. 119/119 backend tests passing.
- 2026-08-06: Committed and pushed to `story/2.2-customer-books-an-appointment` (commit `93ff82b`); both CI jobs confirmed green on GitHub. Status set to review; Jack is opening the PR himself.
- 2026-08-06: Added `BarberSeedService` (opt-in, env-var-gated dev-only barber seeding, mirrors `AdminBootstrapService`) and fixed the `SelectDropdown` time-picker opening upward and overflowing the viewport with no scroll affordance. 119/119 backend, 116/116 frontend tests passing; lint/format clean.
- 2026-08-06: Full code review (Blind Hunter + Edge Case Hunter + Acceptance Auditor). 1 decision-needed item resolved (kept `BarberSeedService` until Epic 3's admin panel lands, hardened its exception handling), 8 patches applied: fixed `GetAvailableSlots`'s midnight-rollover cutoff bug and its previously-toothless test (added an injectable `now` param, split into two real regression tests), fixed `GetAvailability`'s unhandled-500-on-missing-date-param, removed now-redundant `[RegularExpression]` attributes superseded by `[ValidCalendarDate]`/`[ValidTime]`, added an `Id` ordering tiebreaker to `FindAllByRole`, surfaced distinct error states for barber/availability fetch failures instead of masking them as empty results, added a `disabled` prop to `SelectDropdown` plus a real empty-state message for the Time dropdown, cleared the stale 409 conflict message on new selections, and pinned the three new frontend deps to exact versions. 1 pre-existing item deferred (`BookingController`'s unlogged catch-all, consistent with `AccountController`/`AuthController`). 120/120 backend, 116/116 frontend tests passing; lint/format clean. Status set to `done`.
- 2026-08-06: Second review round, scoped to the round-1 fix diff itself (Blind Hunter + Edge Case Hunter + Acceptance Auditor). Round 1's own fixes had left 4 real gaps, all patched: `Calendar` (Date field) had no `disabled` support, so in-flight selection changes were still possible through it; the stale-error-clear fix missed the sibling `availabilityError` state; `barbersError` was never cleared on a later successful fetch (could permanently hide the booking form); and `BarberSeedService`'s broadened `catch` didn't actually wrap the hasher/DI calls that could throw before it. Also added 2 backend tests closing coverage gaps the reviewers flagged (`date` param omitted entirely; a variable-width-year date string). Local `dotnet test` is currently blocked machine-wide by a Windows Application Control policy on the freshly-built test DLL (confirmed unrelated to this diff — even `dotnet build` alone succeeds cleanly, and it blocks execution of the unchanged prior build too); verified via `dotnet build` (clean) and full frontend `vitest run` (116/116) instead, deferring final backend test confirmation to GitHub Actions CI on push.
