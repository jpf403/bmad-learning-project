---
baseline_commit: 3828d255f9d723e91fdaa75b49a0ee9679c0d37a
---

# Story 2.3: Double-Booking & Self-Conflict Guards

Status: done

## Story

As a customer submitting a booking,
I want the system to reject a slot that's already taken — by me or anyone else — between page-load and submit,
so that I never end up with two conflicting appointments.

## Acceptance Criteria

1. **Given** two near-simultaneous submissions for the same barber/date/time, **when** both are submitted, **then** only one succeeds; the second gets an on-screen error ("That time is no longer available. Choose another."), retains the barber/date selections, and the time dropdown re-queries current availability (FR10).
2. **Given** a signed-in customer already holding an appointment at a given date/time with a different barber, **when** they try to book another appointment at that same date/time, **then** it's blocked the same way, with an equivalent on-screen error (FR9).
3. **Given** any booking attempt, **when** processed, **then** an application-level check-then-insert runs inside a transaction, backed by the two DB-level partial unique indexes from Story 2.1 (AD-9).
4. **Given** a booking submission, **when** received, **then** the server independently re-validates: not in the past, weekday only, within the 30-day cap, and (same-day) not within 30 minutes of current EST time — regardless of what the client already filtered (AD-14).

## Tasks / Subtasks

- [x] **Task 1: Confirm what's already built (do not re-implement)** (AC: #1, #2, #3)
  - [x] AC #1 and AC #3 are **already fully implemented and tested** as of Story 2.1/2.2: `BookingService.Create` (`backend/BarbershopApi/Services/BookingService.cs:15-39`) does an app-level `ExistsConflict` check-then-insert inside `AppointmentRepository.Create`'s single `SaveChangesAsync` transaction, and catches `DbUpdateException`/`SqliteException{SqliteErrorCode:19}` as a backstop, both mapped to `BookingConflictException` → `BookingController.CreateBooking` already maps that to `409` with the exact copy AC #1 specifies (`BookingController.cs:55-58`). The frontend's 409-handling in `ScheduleAppointment.jsx:119-127` already retains barber/date, clears time, and re-fetches availability. **Do not touch any of this for AC #1/#3's frontend or the 409-mapping — it's done.** This story's job for AC #1/#3 is closing the one real test gap (Task 2) and adding the one missing end-to-end test for AC #2 (Task 3).
  - [x] AC #2's guard is **already implemented**: `AppointmentRepository.ExistsConflict(barberId, customerId, date, startTime)` (`AppointmentRepository.cs:46-51`) already checks `a.BarberId == barberId || a.CustomerId == customerId` — a customer double-booking themselves against a *different* barber at the same date/time is already caught by the app-level check and by the DB-level `UNIQUE(CustomerId, Date, StartTime) WHERE CancelledAt IS NULL` index. This is already unit-tested at the `AppointmentRepository`/`BookingService` level (`AppointmentRepositoryTests.cs:186-198`, `BookingServiceTests.cs:45-58`). **Do not re-implement the guard.** The gap is that no test exercises this through the actual `BookingController` HTTP flow yet — Task 3 closes it.

- [x] **Task 2: Close the DB-level backstop test gap (AC #1, #3)** — this is the item retro action #2 and `deferred-work.md`'s "code review of story-2-1" note both flag: the DB-level unique-index backstop (the `catch (DbUpdateException...)` branch in `BookingService.Create`) has never been exercised by a test, because hitting it through the real `Create()` method requires a genuine concurrent race (two `ExistsConflict` checks both passing before either insert commits) — not deterministic, and AD-4 forbids mocking around it.
  - [x] Add `AppointmentRepositoryTests.cs`: `Create_throws_when_a_second_context_inserts_the_same_barber_slot_after_the_first_commits`. Use the **two-DbContext staging pattern** from `AccountServiceTests.UpdateOwnProfile_on_stale_RowVersion_throws_AccountConflictException` (`AccountServiceTests.cs:122-140`) — deterministic, no real concurrency: open `contextA`/`contextB` from `_factory`, seed a barber + two customers, call `repositoryB.ExistsConflict(...)` first and assert it's `false` (staging the "B's pre-check already passed" moment of the race), *then* `await repositoryA.Create(NewAppointment(...))` for the same barber/date/time to commit A's row, *then* `await repositoryB.Create(NewAppointment(...))` for the identical barber/date/time — this now deterministically hits the real SQLite partial unique index (`IX_Appointments_BarberId_Date_StartTime`) on B's `SaveChangesAsync`, not the app-level check. Assert it throws `DbUpdateException` whose `InnerException` is `SqliteException` with `SqliteErrorCode == 19` (same assertion shape `BookingService.Create`'s own catch clause pattern-matches on, `BookingService.cs:35`).
  - [x] This test targets the repository/DB-constraint layer directly, not `BookingService.Create`'s own catch-and-rethrow line — reaching *that* line through the public `Create()` method still requires genuine thread concurrency, which remains the accepted limitation `deferred-work.md` already documents for `AuthService`. Do not attempt to force a real race with `Task.WhenAll`; that reintroduces the non-determinism this task exists to avoid. Note this scope boundary in Dev Agent Record.
  - [x] Add a mirroring test for the customer-side index, `Create_throws_when_a_second_context_inserts_the_same_customer_slot_after_the_first_commits` (same pattern, same customer + two different barbers, hits `IX_Appointments_CustomerId_Date_StartTime`).

- [x] **Task 3: Close the AC #2 end-to-end test gap** — add `BookingControllerTests.cs`: `CreateBooking_when_customer_already_holds_a_different_barber_at_the_same_time_returns_409`. Same shape as the existing `CreateBooking_second_request_for_same_slot_returns_409` (`BookingControllerTests.cs:234-253`), but seed **two barbers** and use **one customer's access token** for both requests (first books BarberA at the slot, second attempts BarberB at the identical date/time) — proves FR9's "equivalent on-screen error" claim end-to-end through the real Controller, not just at the repository/service level where it's already covered.

- [x] **Task 4: Add `InvalidBookingWindowException` and AD-14 re-validation to `BookingService.Create`** (AC #4 — the actual new behavior this story adds)
  - [x] Add `backend/BarbershopApi/Services/InvalidBookingWindowException.cs` — one-liner, same shape as `BookingConflictException.cs`: `public class InvalidBookingWindowException : Exception;`. Use a distinct exception (not `BookingConflictException`) because this is a different failure category — "this isn't a legal booking window at all" (400, bad input) vs. "this specific slot just got taken" (409, conflict) — matching how `AppointmentNotFoundException`/`AppointmentAlreadyCancelledException` are already kept distinct from `BookingConflictException` for the same reason.
  - [x] Add an optional `DateTime? now = null` parameter to `IBookingService.Create`/`BookingService.Create` — same injectable-"now" convention `GetAvailableSlots` already established (`IBookingService.cs:12`, `BookingService.cs:67`) for deterministic tests, defaulting to `GetNowEst()` when omitted.
  - [x] At the top of `Create`, before the existing `ExistsConflict` check, validate the booking window:
    1. Parse `date`/`startTime` into a combined `DateTime` (`DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture)` / `TimeOnly.ParseExact(startTime, "HH:mm", CultureInfo.InvariantCulture)`, then `.ToDateTime(...)`) — safe to assume well-formed input here, since `BookAppointmentRequest`'s `[ValidCalendarDate]`/`[ValidTime]` attributes already reject malformed values before the Controller action runs.
    2. **Use full `DateTime` comparison, not bare `"HH:mm"` string comparison, for the past/cutoff check** — `GetAvailableSlots` shipped a real bug in Story 2.2 (midnight-rollover: comparing formatted `"HH:mm"` strings silently broke when the 30-minute cutoff crossed into the next calendar day) precisely because it used string comparison for something that's actually a `DateTime` question. Do not repeat that pattern here. Comparing the full `appointmentStart` against `nowEst.AddMinutes(30)` directly and correctly covers **both** "not in the past" and "same-day, not within 30 minutes of now" in one comparison — any date/time combination before that instant is invalid, with no separate midnight-boundary case to get wrong.
    3. Separately check the date-only rules (unaffected by time-of-day): the parsed date's `DayOfWeek` is not `Saturday`/`Sunday`, and the date is not later than `DateOnly.FromDateTime(nowEst).AddDays(30)` (inclusive — exactly 30 days out is allowed, matching `Calendar.jsx`'s existing client-side cap where only days *strictly greater* than `today+30` are disabled, `Calendar.jsx:26-27,33`).
    4. If any check fails, throw `InvalidBookingWindowException` before the conflict check runs — a booking that's illegal on its face shouldn't even reach the "is this slot taken" question.
  - [x] In `BookingController.CreateBooking`, add `catch (InvalidBookingWindowException) → Problem(statusCode: 400, title: "That date or time is no longer available for booking.")`, ordered before the existing `catch (BookingConflictException)` (exception-type ordering doesn't affect behavior here since the types are disjoint, but mirrors reading order top-to-bottom: input-shape problems before conflict problems). No frontend change is required or in scope: no AC in this story specifies distinct on-screen copy for this case (unlike AC #1/#2's explicit copy), and in the real UI this path is normally unreachable — the calendar already disables past/weekend/>30-day dates and `GetAvailableSlots` already excludes same-day sub-30-minute slots from the dropdown (AD-14 frontend convenience layer, already built in Story 2.2). It's reachable only via a stale client (e.g., a tab left open past midnight or past the 30-day boundary, or a direct API call) — exactly the defense-in-depth AD-14 describes. The existing frontend catch-all (`ScheduleAppointment.jsx:129`, "Something went wrong. Please try again.") already handles any non-409 failure; leave it as is.

- [x] **Task 5: Fix the pre-existing wall-clock time bomb this story's own change would otherwise create** — this is the most important non-obvious risk in this story. Once `Create` enforces AD-14, every existing test that calls it (directly or via HTTP) with the hardcoded literal `"2026-09-01"` becomes a correctness question relative to whatever "now" actually is when the test runs, not a fixed fact. Two different fixes are needed depending on whether the test can inject `now`:
  - [x] **`BookingServiceTests.cs` (calls `BookingService.Create` directly — can inject `now`):** update the existing `Create_throws_BookingConflictException_when_barber_slot_already_booked` (line 31), `Create_throws_BookingConflictException_when_customer_already_booked_a_different_barber_at_same_time` (line 45), and the third `Create` call at line 117, to pass an explicit fixed `now` (e.g., `new DateTime(2026, 9, 1, 8, 0, 0)` — a weekday, `"09:00"` is >30 min after 8:00, well inside the cap) so they stay deterministic and keep passing regardless of real wall-clock date, matching the exact rationale already documented for `GetAvailableSlots`'s tests (`BookingServiceTests.cs:182-196` construct an explicit "now" rather than depending on wall-clock time). Apply the same explicit-`now` treatment to every new test Task 4 adds.
  - [x] **`BookingControllerTests.cs` (calls `POST /api/booking` over real HTTP — no test seam exists to inject "now", and none should be added: a test-only backdoor on a production Controller/DTO is worse than the problem it'd solve):** every test that POSTs a *legally-shaped* booking date (i.e., every occurrence of the literal `"2026-09-01"` **except** the three tests deliberately exercising malformed/invalid values — `CreateBooking_with_malformed_date_returns_400`, `CreateBooking_with_nonexistent_calendar_date_returns_400`, `CreateBooking_with_malformed_startTime_returns_400`, `CreateBooking_with_nonexistent_time_returns_400`, which are testing format validation and are unaffected by AD-14) must switch from the hardcoded literal to a date computed relative to real "now" at test-run time, or the whole file becomes a suite that silently starts failing the moment real wall-clock time passes 2026-09-01 (or falls outside the 30 days before it) — the exact class of bug 2.2's own code review flagged for `GetAvailableSlots`' original test, just at the integration layer this time.
    - Add a private helper, e.g. `NextBookableWeekday(int minDaysAhead = 1)`: starting from `DateTime.Today.AddDays(minDaysAhead)`, advance a day at a time until landing on a weekday, return it formatted `"yyyy-MM-dd"`. `minDaysAhead = 1` (the default) sidesteps the same-day 30-minute-cutoff question entirely — every call site needs a stable, unambiguously-legal date, not a boundary case (boundary cases belong in Task 4's own dedicated tests, at the Service level where `now` is controlled).
    - Replace the literal `"2026-09-01"` with `NextBookableWeekday()` in: `GetAvailability_excludes_already_booked_slot` (both the seeding `POST` at line 103 **and** the `GET .../availability?...&date=2026-09-01` at line 108 — they must use the *same* computed value), `CreateBooking_with_valid_request_returns_201_with_BookingConfirmation` (line 208 — and update its `Assert.Equal("2026-09-01", body.Date)` at line 215 to assert against the same computed value, not the old literal), `CreateBooking_with_nonexistent_barberId_returns_400` (line 227), `CreateBooking_second_request_for_same_slot_returns_409` (lines 243, 249), and Task 3's new self-conflict test.
    - `CreateBooking_without_access_token_returns_401` (line 187-197) needs no change — it 401s in auth middleware before any booking logic runs, regardless of the date's legality.

- [ ] **Task 6: Verify CI green and branch/PR**
  - [x] Branch as `story/2.3-double-booking-and-self-conflict-guards` from `main`.
  - [ ] Push and confirm both CI jobs (backend `.NET`, frontend Vite/React) green on GitHub before merging (AD-11). No frontend files change in this story, so the frontend job is only re-confirming no regression, not testing anything new.

## Dev Notes

### Architecture Compliance (must-follow, not optional)

- **AD-1 (layering)** — the new AD-14 validation belongs in `BookingService.Create` (business logic), not `BookingController` — same split Story 2.2 already established for `GetAvailableSlots`.
- **AD-4 (real DB, never mocked)** — Task 2's DB-backstop test must run against the real temp SQLite instance via `SqliteApiFactory`, like every other repository test in this codebase. Do not mock `DbContext`/`SqliteException`.
- **AD-9 (double-booking guard)** — already fully built (Story 2.1) and already exercised at the app-level-check layer (Story 2.1/2.2 tests). This story's only remaining AD-9 work is Task 2's DB-level backstop test.
- **AD-12 (EST semantics)** — the new window check must use the same `TimeZoneInfo.FindSystemTimeZoneById("America/New_York")`-derived `nowEst` `BookingService` already computes (`GetNowEst()`, `BookingService.cs:108`) — do not introduce a second timezone-conversion path.
- **AD-14 (this story's actual new scope)** — server-side re-validation of not-in-past/weekday/30-day-cap/same-day-30-min-cutoff on the *submission* path. Story 2.2's Dev Notes explicitly deferred this exact check to this story (`2-2-customer-books-an-appointment.md` §Architecture Compliance, AD-14 scope split) — this is that promised work, not new scope invented here.

### Current codebase state relevant to this story (verified by direct file read, not inferred)

- `BookingService.Create(customerId, barberId, date, startTime)` (`BookingService.cs:15-39`) currently does **zero** date/time legality checking — it only checks for slot conflicts. `IBookingService.Create` (`IBookingService.cs:8`) has no `now` parameter yet; `GetAvailableSlots` is the only method with that convention today (`IBookingService.cs:12`).
- `AppointmentRepository.ExistsConflict` (`AppointmentRepository.cs:46-51`) already covers both barber-conflict and customer-self-conflict (the `||` on line 50) — confirmed by existing tests at `AppointmentRepositoryTests.cs:171-198` and `BookingServiceTests.cs:30-58`. No repository/service change needed for AC #2's actual guard.
- The two DB-level partial unique indexes both exist today, unchanged since Story 2.1's migration (`Migrations/20260804171154_AddAppointmentEntity.cs:43-55`): `IX_Appointments_BarberId_Date_StartTime` and `IX_Appointments_CustomerId_Date_StartTime`, both `WHERE CancelledAt IS NULL`.
- `BookingController.CreateBooking` (`BookingController.cs:39-63`) currently catches only `BookingConflictException` (409) and a generic `catch (Exception)` (500) — Task 4 adds a third catch block for `InvalidBookingWindowException` (400), which must be a distinct `catch` clause, not folded into the generic one (the generic 500 fallback is explicitly the wrong status for a validation failure).
- No exception in this codebase currently distinguishes "this booking's date/time is outside the legal window" from "this slot is already taken" — `InvalidBookingWindowException` is new. Existing sibling exceptions for reference/naming precedent: `Services/BookingConflictException.cs`, `Services/AppointmentNotFoundException.cs`, `Services/AppointmentAlreadyCancelledException.cs`, `Services/InvalidCredentialsException.cs` (all one-line `Exception` subclasses, no custom constructor/message).
- `Calendar.jsx`'s client-side disabled-date matcher (`Calendar.jsx:23-34`) independently computes past/weekend/30-day-cap using the *browser's local clock*, not server EST — a known, already-accepted AD-14 gap (client filtering is "a UX convenience only, never the enforcement point"). This story does not touch `Calendar.jsx` or any frontend file; it only adds the server-side enforcement AD-14 requires regardless of what the client-side filter does or doesn't catch.

### Testing Requirements

- Backend: xUnit.v3 + `WebApplicationFactory`/`SqliteApiFactory` against a real temp SQLite instance, never mocked (AD-4/NFR4).
- Test fixture names: `"John"`/`"Smith"` (repository/service-level default helpers already use this) — never a real person's name, per this project's established convention.
- No frontend tests are added or changed by this story — confirm this by re-reading all four ACs: none of them specify new UI copy, and the two ACs with UI-visible behavior (#1, #2) are already implemented and tested on the frontend as of Story 2.2.

### Previous Story Intelligence (from Story 2.2)

- Story 2.2's own Dev Notes explicitly scoped AD-14's full submission-time re-validation out of itself and into this story (§Architecture Compliance: *"do not pull Story 2.3's server-side resubmission validation into this story... 'is this date/time actually still legal to book' is 2.3's [job]"*) — this story is that exact, previously-deferred scope, not new ambiguity.
- Story 2.2's review caught a real bug from exactly the anti-pattern Task 4 warns against: `GetAvailableSlots`'s original cutoff logic compared formatted `"HH:mm"` strings and silently broke across a midnight rollover, and the test meant to catch it recomputed the same buggy logic as its own expected value instead of using independent full-`DateTime` math. Task 4's guidance to use full `DateTime` comparison for the new window check exists specifically to not reproduce this.
- Story 2.2 also established the "construct with an explicit injectable `now`, never depend on wall-clock time" testing convention (used for `GetAvailableSlots`) — Task 4/5 extend that same convention to `Create`.

### Deferred Work / Retro Action Items Checked

- Retro action item #1 (re-check `deferred-work.md` at kickoff): checked in full. One open item under "code review of story-2-1" applies directly to this story and is resolved by Task 2: *"`BookingService.Create`'s DB-level race backstop... can't be tested deterministically without a real concurrent request, which AD-4 disallows mocking around"* — Task 2 resolves this via the two-DbContext staging pattern rather than a real concurrent request, closing the gap instead of re-deferring it. The remaining open items in `deferred-work.md` are all scoped to Auth/Account/BookingController-logging and don't apply here.
- Retro action item #2 (*"Story 2.3's double-booking race tests must use the deterministic two-DbContext staging pattern... instead of a real concurrent-request race"*): this is this story's own action item — addressed directly by Task 2.
- No other open action item applies to this story.

### Project Structure Notes

- Backend additions: `Services/InvalidBookingWindowException.cs` (new, one-liner).
- Backend modifications: `Services/IBookingService.cs`/`BookingService.cs` (new `now` param + window validation on `Create`), `Controllers/BookingController.cs` (new `catch` clause), `BarbershopApi.Tests/AppointmentRepositoryTests.cs` (2 new tests), `BarbershopApi.Tests/BookingServiceTests.cs` (3 existing `Create` calls updated to pass explicit `now`, new window-validation tests added), `BarbershopApi.Tests/BookingControllerTests.cs` (new `NextBookableWeekday` helper, several existing tests' hardcoded dates replaced, 1 new self-conflict test).
- No new migration, no schema change, no new Dto, no frontend file touched — the smallest-footprint story in Epic 2 so far.

### References

- [Source: _bmad-output/planning-artifacts/epics.md §Story 2.3, §Epic 2] — story statement, AC, FR9/FR10 mapping.
- [Source: _bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/SOLUTION-DESIGN.md §4 "Double-booking prevention is defense-in-depth", §4 "Server-side re-validation of booking date rules (AD-14)"] — exact AD-9/AD-14 wording this story implements.
- [Source: _bmad-output/implementation-artifacts/2-2-customer-books-an-appointment.md §Architecture Compliance, §Review Findings] — AD-14 scope split into this story; the midnight-rollover string-comparison bug this story's Task 4 must not repeat.
- [Source: _bmad-output/implementation-artifacts/2-1-appointment-entity-and-repository.md] — origin of `ExistsConflict`, the two partial unique indexes, and the two-`DbContext` test pattern precedent.
- [Source: _bmad-output/implementation-artifacts/deferred-work.md §"code review of story-2-1"] — the DB-backstop test gap this story closes.
- [Source: backend/BarbershopApi/Services/BookingService.cs, IBookingService.cs, Controllers/BookingController.cs] — exact current signatures/behavior this story extends.
- [Source: backend/BarbershopApi/Repositories/AppointmentRepository.cs, Migrations/20260804171154_AddAppointmentEntity.cs] — `ExistsConflict` and the two DB-level indexes, unchanged since Story 2.1.
- [Source: backend/BarbershopApi.Tests/AccountServiceTests.cs:122-140] — two-DbContext staging pattern this story's Task 2 follows.
- [Source: backend/BarbershopApi.Tests/AppointmentRepositoryTests.cs, BookingServiceTests.cs, BookingControllerTests.cs] — exact existing test helpers/conventions (`SeedAccount`, `NewAppointment`, `NewService`, `AuthedRequest`) this story's new tests reuse.
- [Source: frontend/src/pages/ScheduleAppointment.jsx:119-127, components/Calendar.jsx:23-34] — confirmation that AC #1/#2's frontend behavior and the client-side date-disabling convenience layer are already built; not touched by this story.
- [Source: project-context.md §Concurrency / race conditions, §Naming] — AD-9 defense-in-depth restated; exception-naming precedent.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (Amelia persona)

### Debug Log References

None — no failures requiring investigation. `dotnet test` sandbox `Access is denied` workaround applied (ran via `dotnet exec` on the built `BarbershopApi.Tests.dll` with sandbox disabled, per project convention).

### Completion Notes List

- Task 1: Re-verified every already-built claim directly against current source (`BookingService.cs`, `IBookingService.cs`, `BookingController.cs`, `AppointmentRepository.cs`) before writing any code — all confirmed accurate, no re-implementation done.
- Task 2 (scope note): the two DB-level backstop tests this task specifies (`Create_throws_when_a_second_context_inserts_the_same_barber_slot_after_the_first_commits` / `..._customer_slot_after_the_first_commits`) were substantively already present in `AppointmentRepositoryTests.cs` since Story 2.1 (`Create_second_appointment_for_same_barber_slot_throws` / `Create_second_appointment_for_same_customer_slot_across_different_barbers_throws`), using the identical two-`DbContext` pattern and assertion shape. Rather than add near-duplicate tests, I renamed and extended those two existing tests in place with the `ExistsConflict`-staging assertion this story's spec calls for (documenting the exact TOCTOU instant being simulated), satisfying Task 2's intent without redundant coverage.
- Task 4: Added `InvalidBookingWindowException`, extended `IBookingService.Create`/`BookingService.Create` with an optional `DateTime? now` parameter (same convention as `GetAvailableSlots`), and implemented AD-14 window validation (not-in-past/30-min-cutoff via full `DateTime` comparison, weekday-only, 30-day cap inclusive) ahead of the existing conflict check. `BookingController` gains a dedicated `catch (InvalidBookingWindowException)` → 400, ordered before the existing 409 conflict catch.
- Task 5: Updated all pre-existing `BookingService.Create` calls in `BookingServiceTests.cs` to pass an explicit fixed `now` (`2026-09-01 08:00`, a Tuesday). Added a `NextBookableWeekday()` helper to `BookingControllerTests.cs` and replaced the hardcoded `"2026-09-01"` literal everywhere it represented a real booking date (not the 4 malformed/invalid-value format tests, which fail model validation before reaching `Create` and are date-value-agnostic).
- Task 6: branch `story/2.3-double-booking-and-self-conflict-guards` already exists off `main` (pre-created). Push + CI confirmation intentionally left unchecked and undone — per standing preference, Jack handles commit/push/PR/CI-confirmation himself; that checkbox should be updated once he's done so.
- Full backend suite: 130/130 passing, 0 failures, after all changes (`dotnet exec BarbershopApi.Tests.dll`).
- No frontend files touched — no AC in this story required it (re-confirmed per Dev Notes' "Testing Requirements").

### File List

- `backend/BarbershopApi/Services/InvalidBookingWindowException.cs` (new)
- `backend/BarbershopApi/Services/IBookingService.cs` (modified — `Create` now takes optional `DateTime? now`)
- `backend/BarbershopApi/Services/BookingService.cs` (modified — AD-14 window validation added to `Create`)
- `backend/BarbershopApi/Controllers/BookingController.cs` (modified — new `catch (InvalidBookingWindowException)` → 400)
- `backend/BarbershopApi.Tests/AppointmentRepositoryTests.cs` (modified — 2 DB-backstop tests renamed/extended with `ExistsConflict` staging)
- `backend/BarbershopApi.Tests/BookingServiceTests.cs` (modified — explicit `now` on existing `Create` calls; 7 new window-validation tests added)
- `backend/BarbershopApi.Tests/BookingControllerTests.cs` (modified — `NextBookableWeekday()` helper added; hardcoded dates replaced where booking-legality-relevant; 1 new AC#2 end-to-end self-conflict test)

### Change Log

- 2026-08-07: Implemented Story 2.3 — AD-14 server-side booking-window re-validation added to `BookingService.Create`; closed the AC#2 controller-level test gap; hardened the two DB-level backstop tests with explicit TOCTOU staging; fixed wall-clock-dependent test literals across the booking test suite.

### Review Findings

- [x] [Review][Patch] Add a controller-level HTTP test for the `InvalidBookingWindowException` → 400 path — no test in `BookingControllerTests.cs` exercises the new `catch` block end-to-end (e.g., a Saturday date or a date beyond the 30-day cap posted to `/api/booking`); a regression in catch-clause ordering or status code would go undetected by CI. [backend/BarbershopApi.Tests/BookingControllerTests.cs, backend/BarbershopApi/Controllers/BookingController.cs:55-58]
- [x] [Review][Patch] Extract the inline `30`-minute cutoff and `30`-day cap literals in `BookingService.Create` to named constants for readability and single-point-of-change. [backend/BarbershopApi/Services/BookingService.cs:23,25]
- [x] [Review][Patch] Add a test pinning the exact 30-minute boundary (`<` vs `<=`) for the booking cutoff — existing tests cover 15-minutes-out (throws) and the midnight-rollover case, but nothing asserts behavior at exactly `now + 30min`. [backend/BarbershopApi.Tests/BookingServiceTests.cs]
- [x] [Review][Patch] ~~`DateTime? now = null` optional-parameter test seam on `IBookingService.Create` lets any future caller bypass window validation (or pass a wrong-`DateTimeKind` value and silently corrupt the check)~~ **Resolved (2026-08-07)** — reclassified from defer to patch at Jack's request; added a `ResolveNowEst` guard shared by `Create`/`GetAvailableSlots` that throws `ArgumentException` if a caller-supplied `now` has a `DateTimeKind` other than `Unspecified`, so a Utc/Local mismatch fails loudly instead of silently corrupting the window math. The parameter itself stays (matches `GetAvailableSlots`'s existing convention; full `TimeProvider` DI refactor was considered and declined as out of scope for an already-`done` story). [backend/BarbershopApi/Services/BookingService.cs:124-133]
- [x] [Review][Defer] The `now = null` default is declared independently on both `IBookingService.Create` and `BookingService.Create` — C# resolves interface default parameters at the caller's static type, so if the two defaults are ever edited out of sync, behavior would silently diverge by reference type — deferred, pre-existing pattern (already true of `GetAvailableSlots`), no live bug today since both defaults agree. [backend/BarbershopApi/Services/IBookingService.cs:8, BookingService.cs:18]
- [x] [Review][Patch] ~~No validation that `startTime` is actually one of the fixed appointment slots~~ **Resolved (2026-08-07)** — reclassified from defer to patch at Jack's request; `Create` now rejects any `startTime` not present in `FixedSlots` with `InvalidBookingWindowException` (same 400 path as the other window checks). [backend/BarbershopApi/Services/BookingService.cs:29-31]
