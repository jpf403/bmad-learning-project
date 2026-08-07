---
baseline_commit: 200958fdb7aaec1f0bd403dbc3f4a7d79e8b661c
---

# Story 2.4: My Appointments — View, Cancel, and Race Safety

Status: review

## Story

As a signed-in user,
I want to see my own upcoming appointments and cancel one safely,
so that I can manage my bookings without contacting the shop or worrying about a stale click.

## Acceptance Criteria

1. **Given** the Schedule Appointment page, **when** it loads, **then** the signed-in user's own upcoming (not-yet-occurred) appointments list renders at the bottom, via the shared `BookingService` read path (FR24, AD-8, AD-17); past appointments stay in the DB but are never shown here.
2. **Given** no upcoming appointments, **when** the list renders, **then** it shows "No upcoming appointments."
3. **Given** an upcoming appointment in the list, **when** the user clicks Cancel, **then** a confirm-action popup (destructive Confirm) appears before the cancellation takes effect (FR25, FR30).
4. **Given** a confirmed cancellation, **when** it completes, **then** the appointment's `CancelledAt` is set (soft-cancel, never a hard delete) and the slot is immediately free for booking again (FR25, AD-8).
5. **Given** an appointment already cancelled by another actor (a race, not a user error), **when** a second cancellation attempt is made on it, **then** it's rejected with an on-screen error rather than a silent no-op or crash, and the view refreshes to the current, accurate state (FR30).
6. **Given** this cancel mechanism, **when** built, **then** it's the single shared implementation every cancel path (customer, barber, admin) reuses in Stories 2.5/2.6 — never duplicated per role.

## Tasks / Subtasks

- [x] **Task 1: Replace `Cancel` with a race-safe conditional-update repository primitive** (AC #4, #5) — the architecture docs name "stale cancellation" as an error category (SOLUTION-DESIGN.md §7) but never specify the enforcement mechanism, and `Appointment` has no `RowVersion`/concurrency token (unlike `Account`, AD-16) and adding one would be a schema change Story 2.1's closing AC explicitly forbids for 2.2–2.6 ("no further schema changes for anything Epic 2 needs"). The chosen mechanism: a single atomic conditional `UPDATE` via EF Core's `ExecuteUpdateAsync`, using the existing `CancelledAt IS NULL` predicate itself as the race guard — no new column, no read-then-write gap.
  - [x] In `IAppointmentRepository.cs`, replace `Task Cancel(Appointment appointment);` with `Task<bool> TryCancel(int appointmentId, DateTime cancelledAtUtc);`.
  - [x] In `AppointmentRepository.cs`, implement:
    ```csharp
    public async Task<bool> TryCancel(int appointmentId, DateTime cancelledAtUtc)
    {
        var rowsAffected = await context.Appointments
            .Where(a => a.Id == appointmentId && a.CancelledAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.CancelledAt, cancelledAtUtc));
        return rowsAffected == 1;
    }
    ```
    `ExecuteUpdateAsync` issues one `UPDATE ... WHERE Id=@id AND CancelledAt IS NULL` directly against the database, bypassing the change tracker entirely — no `SaveChangesAsync` call, no entity load required. This is the mechanism that closes the race: two concurrent calls both filter on `CancelledAt == null`, only one `UPDATE` can win, the loser affects 0 rows.
  - [x] Update every existing call site that used the old `Cancel(Appointment appointment)` signature in `AppointmentRepositoryTests.cs` to call `TryCancel(id, DateTime.UtcNow)` instead: `FindByBarberAndDate_excludes_cancelled_appointments` (cancels `cancelled`), `FindUpcomingByCustomer_excludes_past_and_cancelled_appointments` (cancels `toCancel`), `Cancel_sets_CancelledAt` (rename to `TryCancel_sets_CancelledAt_and_returns_true`), `ExistsConflict_false_when_matching_slot_is_cancelled` (cancels `appointment`).
  - [x] Add `AppointmentRepositoryTests.cs`: `TryCancel_returns_false_and_leaves_CancelledAt_unchanged_when_already_cancelled` — cancel once (assert `true`), cancel again (assert `false`), then reload and assert `CancelledAt` still equals the first value (proves the second call didn't overwrite the timestamp).
  - [x] Add `AppointmentRepositoryTests.cs`: `TryCancel_returns_false_when_appointment_does_not_exist` (id `999999`).

- [x] **Task 2: Add caller-scoped authorization to `BookingService.Cancel`, wired to the new atomic primitive** (AC #4, #5, #6) — the architecture never specifies who may cancel an appointment; AC #6 requires the *same* method to serve customer (this story), barber (2.5), and admin (2.6) cancellation without modification later, so the authorization branch is built now, parameterized by caller identity/role, even though only the `Role.Customer` branch is exercised by this story's UI.
  - [x] Change `IBookingService.Cancel(int appointmentId)` to `Task Cancel(int appointmentId, int callerAccountId, Role callerRole);`.
  - [x] Implement in `BookingService.cs`:
    ```csharp
    public async Task Cancel(int appointmentId, int callerAccountId, Role callerRole)
    {
        var appointment = await appointmentRepository.FindById(appointmentId);
        if (appointment is null)
        {
            throw new AppointmentNotFoundException();
        }

        var authorized = callerRole switch
        {
            Role.Customer => appointment.CustomerId == callerAccountId,
            Role.Barber => appointment.BarberId == callerAccountId,
            Role.Admin => true,
            _ => false,
        };
        if (!authorized)
        {
            // Not-found, not forbidden -- never confirm that a specific
            // appointment id belongs to someone else.
            throw new AppointmentNotFoundException();
        }

        var cancelled = await appointmentRepository.TryCancel(appointmentId, DateTime.UtcNow);
        if (!cancelled)
        {
            throw new AppointmentAlreadyCancelledException();
        }
    }
    ```
  - [x] Update `BookingServiceTests.cs` existing calls: `Cancel_throws_AppointmentNotFoundException_when_appointment_does_not_exist` → `service.Cancel(999999, 1, Role.Customer)` (any id/role is fine — not-found short-circuits before the authorization branch runs). `Cancel_on_already_cancelled_appointment_throws_AppointmentAlreadyCancelledException` → seed the appointment via `service.Create(customer.Id, ...)` as today, then `service.Cancel(created.Id, customer.Id, Role.Customer)` twice.
  - [x] Add `BookingServiceTests.cs`: `Cancel_throws_AppointmentNotFoundException_when_caller_is_not_the_owning_customer` (a different customer id, `Role.Customer`), `Cancel_succeeds_when_caller_is_the_appointments_barber` (`Role.Barber`, matching `BarberId`), `Cancel_throws_AppointmentNotFoundException_when_caller_is_a_different_barber` (`Role.Barber`, non-matching `BarberId`), `Cancel_succeeds_when_caller_is_admin_regardless_of_owner` (`Role.Admin`, any unrelated account id).

- [x] **Task 3: Add `GET /api/booking/mine` and `POST /api/booking/{id}/cancel` to `BookingController`** (AC #1, #2, #4, #5)
  - [x] Add ordering to `AppointmentRepository.FindUpcomingByCustomer` — it currently has no `OrderBy` at all (verified: `AppointmentRepository.cs:28-38`), so today's list order is DB-insertion-order, not soonest-first. Add `.OrderBy(a => a.Date).ThenBy(a => a.StartTime)` before `.ToListAsync()` so the customer's list is always soonest-appointment-first — no FR/AD/UX doc states this explicitly, but an unordered list is a real UX defect, not an acceptable default.
  - [x] Add to `BookingController.cs`:
    ```csharp
    [HttpGet("mine")]
    public async Task<IActionResult> GetMyAppointments()
    {
        var account = (Account)HttpContext.Items["Account"]!;
        return Ok(await bookingService.FindUpcomingByCustomer(account.Id));
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelBooking(int id)
    {
        var account = (Account)HttpContext.Items["Account"]!;
        try
        {
            await bookingService.Cancel(id, account.Id, account.Role);
            return NoContent();
        }
        catch (AppointmentNotFoundException)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Appointment not found.");
        }
        catch (AppointmentAlreadyCancelledException)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "This appointment has already been cancelled.");
        }
        catch (Exception)
        {
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Something went wrong. Please try again.");
        }
    }
    ```
    Route order/placement: add both actions after the existing `CreateBooking` action, same file, same `[Authorize]` class-level gate — no new controller (AD-1: one Controller per domain concept, Booking already exists).
  - [x] Add `BookingControllerTests.cs`: `GetMyAppointments_returns_only_the_callers_upcoming_appointments_ordered_soonest_first`, `GetMyAppointments_returns_empty_list_when_none_exist`, `CancelBooking_returns_204_and_frees_the_slot_for_rebooking` (cancel, then successfully re-book the identical barber/date/time), `CancelBooking_on_someone_elses_appointment_returns_404`, `CancelBooking_on_nonexistent_id_returns_404`, `CancelBooking_on_already_cancelled_appointment_returns_409` (two sequential HTTP calls — this is the integration-level check; Task 4 covers the deterministic DB-level race separately), `CancelBooking_without_access_token_returns_401`.

- [x] **Task 4: Deterministic cancel-race test at the repository layer** (AC #5) — closes the exact gap `deferred-work.md` names under "code review of story-2-1": *"`Cancel`'s read-then-write race... can't be tested deterministically without a real concurrent request"*. Story 2.3 closed the analogous booking-insert race using the two-`DbContext` staging pattern (`AppointmentRepositoryTests.cs:51-69`); mirror it here, adapted for an atomic conditional update rather than a unique-index collision.
  - [x] Add `AppointmentRepositoryTests.cs`: `TryCancel_returns_false_when_a_second_context_cancels_after_the_first_commits_first`:
    ```csharp
    [Fact]
    public async Task TryCancel_returns_false_when_a_second_context_cancels_after_the_first_commits_first()
    {
        await using var seedContext = _factory.CreateDbContext();
        var customer = await SeedAccount(seedContext, "customer@example.com", Role.Customer);
        var barber = await SeedAccount(seedContext, "barber@example.com", Role.Barber);
        var repository = new AppointmentRepository(seedContext);
        var appointment = await repository.Create(NewAppointment(customer.Id, barber.Id));

        await using var contextA = _factory.CreateDbContext();
        var repositoryA = new AppointmentRepository(contextA);
        Assert.True(await repositoryA.TryCancel(appointment.Id, DateTime.UtcNow));

        await using var contextB = _factory.CreateDbContext();
        var repositoryB = new AppointmentRepository(contextB);
        Assert.False(await repositoryB.TryCancel(appointment.Id, DateTime.UtcNow));
    }
    ```
    Unlike the booking-insert race (which needed a staged pre-check read to simulate the TOCTOU instant), `ExecuteUpdateAsync`'s conditional `WHERE` clause makes this deterministic with two straightforward sequential calls from separate contexts — no stale-entity staging needed, because the guard is evaluated fresh against the database on every call, not against in-memory entity state. **Do not** attempt to force this with `Task.WhenAll`/real threads — same scope boundary Story 2.3 documented for the booking race.

- [x] **Task 5: My Appointments list and cancel flow on the Schedule Appointment page** (AC #1, #2, #3, #4, #5) — no new page/route: per EXPERIENCE.md's IA table and epics.md's AC #1 ("the Schedule Appointment page... list at the bottom"), this list lives on the existing `/schedule-appointment` route below the booking form. `/my-schedule` (`NavBar.jsx` `ROLE_LINKS`, `landingRoutes.js`) is a **different**, Barber/Admin-only page that does not exist yet — do not confuse the two or build against that route.
  - [x] Add to `frontend/src/api/BookingApi.js`:
    ```js
    export async function getMyAppointments(accessToken) {
      let response
      try {
        response = await fetch(`${API_BASE_URL}/api/booking/mine`, {
          credentials: 'include',
          headers: { Authorization: `Bearer ${accessToken}` },
        })
      } catch {
        return { ok: false, status: null }
      }

      const body = await response.json().catch(() => null)
      if (!response.ok || body === null) {
        return { ok: false, status: response.ok ? null : response.status, problem: body }
      }
      return { ok: true, appointments: body }
    }

    export async function cancelAppointment(accessToken, appointmentId) {
      let response
      try {
        response = await fetch(`${API_BASE_URL}/api/booking/${appointmentId}/cancel`, {
          method: 'POST',
          credentials: 'include',
          headers: { Authorization: `Bearer ${accessToken}` },
        })
      } catch {
        return { ok: false, status: null }
      }

      if (response.ok) {
        return { ok: true }
      }
      const problem = await response.json().catch(() => null)
      return { ok: false, status: response.status, problem }
    }
    ```
    **Do not copy `createBooking`'s always-parse-JSON pattern for `cancelAppointment`** — the cancel endpoint returns `204 No Content` on success, which has no body; calling `response.json()` unconditionally and treating a `null` result as failure (as `getBarbers`/`createBooking` do) would misreport every successful cancel as a failure. Check `response.ok` first, and only parse a body on the error path.
  - [x] In `ScheduleAppointment.jsx`, add appointments state (`appointments`, `appointmentsLoading`, `appointmentsError`, `cancelTarget` for the popup, `cancelError`) and a `loadAppointments` function fetched once on mount alongside `loadBarbers` (same `useEffect`-with-cleanup pattern already used for barbers, `ScheduleAppointment.jsx:42-63`). Note: because `ConfirmationScreen` is a full takeover with no "back to form" affordance (verified: `ConfirmationScreen.jsx` has no navigation, and `ScheduleAppointment.jsx:132-140` early-returns to it after a successful booking), a newly-created appointment will **not** appear in this same render — it appears the next time the page mounts. No AC in this story requires an immediate post-booking refresh; do not add one.
  - [x] Render, below the existing `<FormSection>` block (only in the non-`confirmation` branch):
    ```jsx
    <section className="schedule-appointment__appointments">
      <h2 className="section-title">My Appointments</h2>
      {appointmentsLoading ? (
        <p className="schedule-appointment__loading">Loading…</p>
      ) : appointments.length === 0 ? (
        <p>No upcoming appointments.</p>
      ) : (
        appointments.map((appt) => (
          <div className="appt-row" key={appt.id}>
            <div className="appt-info">
              <span className="appt-primary">{appt.barberName}</span>
              <span className="appt-meta">
                {`${formatTimeLabel(appt.startTime)}, ${formatDateLabel(appt.date)}`}
              </span>
            </div>
            <Button variant="destructive" onClick={() => setCancelTarget(appt)}>
              Cancel
            </Button>
          </div>
        ))
      )}
      {cancelError && <p className="schedule-appointment__form-error">{cancelError}</p>}
    </section>
    ```
    Matches `mockups/schedule-appointment.html`'s exact markup shape (`.appt-row` / `.appt-info` / `.appt-primary` / `.appt-meta`) and reuses `formatTimeLabel`/`formatDateLabel` (`utils/FormatSchedule.js`, already imported for the time dropdown) — do not introduce a new date/time formatter.
  - [x] Render `ConfirmPopup` (existing component, `components/ConfirmPopup.jsx` — do not build a new dialog) once, outside the list, driven by `cancelTarget`:
    ```jsx
    <ConfirmPopup
      open={cancelTarget !== null}
      onOpenChange={(open) => !open && setCancelTarget(null)}
      title="Cancel this appointment?"
      message={
        cancelTarget &&
        `${cancelTarget.barberName} — ${formatTimeLabel(cancelTarget.startTime)}, ${formatDateLabel(cancelTarget.date)}. This cannot be undone.`
      }
      destructive
      confirmLabel="Confirm"
      onConfirm={handleCancelConfirmed}
    />
    ```
  - [x] `handleCancelConfirmed`: call `cancelAppointment(user.accessToken, cancelTarget.id)`. On success, refetch `getMyAppointments` (do not just splice the row out client-side — a refetch is the "view refreshes to the current, accurate state" AC #5 calls for, and it's the same one round-trip either way). On `409`, set `cancelError` to exactly `"This appointment has already been cancelled."` (EXPERIENCE.md's Voice/Tone table, verbatim copy) and **also** refetch the list (the row must disappear even though this browser's click "failed" — someone else already cancelled it). On any other failure, set `cancelError` to `"Something went wrong. Please try again."` (existing app-wide fallback copy, `ScheduleAppointment.jsx:129`).
  - [x] Add to `ScheduleAppointment.css`: `.appt-row` (flex, space-between, `background: var(--color-neutral)`, `border-radius: var(--rounded-default)`, `padding: var(--spacing-4) var(--spacing-5)`, `margin-bottom: var(--spacing-3)`), `.appt-info` (flex column, `gap: var(--spacing-1)`), `.appt-primary` (`font-size: var(--typography-body-size)`, `color: var(--color-text)`), `.appt-meta` (`font-size: var(--typography-body-sm-size)`, `color: var(--color-text-muted)`), `.section-title` (reuse `--typography-h2-*` tokens — check whether a shared `.section-title` class already exists project-wide before adding a duplicate). Map the mockup's literal hex/px values (`mockups/schedule-appointment.html`) to these token names — do not hardcode raw colors/pixels.
  - [x] Add to `ScheduleAppointment.test.jsx`, extending `mockFetch` to also stub `GET /api/booking/mine` and `POST /api/booking/{id}/cancel`: renders "No upcoming appointments." on an empty list; renders a row with barber name + formatted time/date and a Cancel button for a non-empty list; clicking Cancel opens the confirm popup with the exact title/message shape; confirming calls the cancel endpoint then re-fetches and re-renders the list without the cancelled row; a `409` response shows "This appointment has already been cancelled." and still refetches; dismissing the popup (Go Back) makes no network call (reuse the dismissal assertions already proven in `components/ConfirmPopup.test.jsx`, don't re-derive them from scratch).

- [ ] **Task 6: Verify CI green and branch/PR**
  - [x] Branch as `story/2.4-my-appointments-view-cancel-and-race-safety` from `main`.
  - [ ] Push and confirm both CI jobs (backend .NET, frontend Vite/React) green on GitHub before merging (AD-11).

## Dev Notes

### Architecture Compliance (must-follow, not optional)

- **AD-1 (layering)** — new endpoints live on the existing `BookingController` → `BookingService` → `AppointmentRepository` trio; no new controller/service/repository for "my appointments" or "cancel" (Booking is already the right domain concept).
- **AD-8 (soft-cancel, computed status)** — cancellation is exclusively `CancelledAt` being set via `TryCancel`; never a hard `DELETE`. "Finished" stays computed at read time (`BookingService.IsFinished`) — this story does not touch that computation, and the My Appointments list never shows a Finished appointment anyway (`FindUpcomingByCustomer` already excludes them).
- **AD-9 (existing double-booking indexes)** — both partial unique indexes filter `WHERE CancelledAt IS NULL`; cancelling an appointment via `TryCancel` automatically frees the slot for the two existing indexes with no additional code (AC #4's "immediately free for booking again" falls out of AD-9's existing design, not new logic this story adds).
- **AD-12 (EST authority)** — `FindUpcomingByCustomer`'s upcoming/not-yet-occurred filtering already uses server-side EST `nowEst` (`BookingService.FindUpcomingByCustomer` → `GetNowEst()`); this story adds no new date/time computation, only ordering.
- **AD-17 (single shared read path)** — `GetMyAppointments` calls the existing `BookingService.FindUpcomingByCustomer`, unchanged in shape; do not add a second, controller-specific query.
- **No stated architecture mechanism for a cancel race or cancel-ownership rule exists** (confirmed by direct review of `ARCHITECTURE-SPINE.md`/`SOLUTION-DESIGN.md` — NFR2 names "cancel" as a race category and SOLUTION-DESIGN.md §7 names "stale cancellation" as an error category, but neither pins down the mechanism). Task 1/2's `TryCancel` conditional-update + role-scoped authorization in `BookingService.Cancel` **is** this story's answer to that gap — treat it as the established mechanism going forward (Stories 2.5/2.6 will call the same `Cancel(appointmentId, callerAccountId, callerRole)` signature, not invent their own).

### Current codebase state relevant to this story (verified by direct file read)

- `IAppointmentRepository.Cancel(Appointment appointment)` / `AppointmentRepository.Cancel` (`AppointmentRepository.cs:40-44`) exist today, unused by any Controller, and have **zero** race protection — a plain unconditional `appointment.CancelledAt = DateTime.UtcNow; SaveChangesAsync()`. Task 1 replaces this signature entirely (not additively) — every existing test caller must be updated, not just new tests added.
- `IBookingService.Cancel(int appointmentId)` / `BookingService.Cancel` (`BookingService.cs:110-123`) already do a `FindById` → null-check → `CancelledAt`-check → `Cancel` sequence, but this is exactly the TOCTOU race the story exists to close (two concurrent calls can both pass the `CancelledAt is not null` check before either writes). Task 2 replaces this method's body and signature.
- `AppointmentRepository.FindUpcomingByCustomer` (`AppointmentRepository.cs:28-38`) has no `.OrderBy` — confirmed by direct read, not assumption.
- `BookingController.cs` currently has exactly three actions (`GetBarbers`, `GetAvailability`, `CreateBooking`) and catches `InvalidBookingWindowException`/`BookingConflictException`/`Exception` — `AppointmentNotFoundException`/`AppointmentAlreadyCancelledException` are defined (`Services/AppointmentNotFoundException.cs`, `Services/AppointmentAlreadyCancelledException.cs`) but caught nowhere yet; this story is their first consumer.
- `Role` enum: `BarbershopApi.Entities.Role { Customer, Barber, Admin }` (`Entities/Role.cs`) — already `using`'d in `BookingController.cs`.
- Frontend: no "My Appointments" or customer-facing page exists anywhere yet. `/my-schedule` (`NavBar.jsx`, `landingRoutes.js`) is an unrelated, not-yet-built Barber/Admin page — do not touch it or confuse it with this story's scope. `ScheduleAppointment.jsx` currently renders only the booking form; `ConfirmPopup.jsx`, `Modal.jsx`, and `Button.jsx` (with an existing `destructive` variant) are already built and must be reused as-is, not recreated.
- `deferred-work.md`'s open item under "code review of story-2-1" — *"`Cancel`'s read-then-write race... can't be tested deterministically without a real concurrent request"* — is fully closed by Task 1/4. No other open `deferred-work.md` item applies to this story (items #2/#4/#5 there are scoped to `BookingController`'s generic-exception logging, dangling-FK/role-validation on `Create`, and other stories).

### Testing Requirements

- Backend: xUnit.v3 + `WebApplicationFactory`/`SqliteApiFactory` against a real temp SQLite instance, never mocked (AD-4/NFR4). Reuse existing helpers (`SeedAccount`, `NewAppointment`, `NewService`, `AuthedRequest`, `NextBookableWeekday`) — do not redefine them per-file.
- Test fixture names: `"John"`/`"Smith"` for the default/incidental party in any test; never a real person's name.
- Frontend: Vitest + jsdom + React Testing Library + user-event; stub `fetch` directly via `vi.spyOn(globalThis, 'fetch')` exactly as `ScheduleAppointment.test.jsx`'s existing `mockFetch` helper already does — extend that helper, don't build a parallel one.
- The two-`DbContext` deterministic staging pattern (Task 4) is required for the cancel race, mirroring Story 2.3's booking-race test — do not attempt a real concurrent (`Task.WhenAll`) race; that reintroduces non-determinism this pattern exists to avoid.

### Previous Story Intelligence (from Story 2.3)

- Story 2.3 fixed a cross-test SQLite connection-pool corruption bug in `SqliteApiFactory.cs` (a global `ClearAllPools()` call was invalidating other tests' pooled connections under parallel execution). This is already fixed at the infrastructure level — no action needed here, but if any new test in this story behaves flakily across parallel runs, that fix is the first place to check it hasn't regressed.
- Story 2.3 established: full `DateTime` comparison (never `"HH:mm"` string comparison) for any new past/cutoff logic, and hardcoded near-future date literals are a wall-clock time bomb — reuse `NextBookableWeekday()` in any new `BookingControllerTests.cs` test rather than a hardcoded date. Neither applies directly to Task 1–4's cancel logic (no new date-window math is added), but applies to any new controller test seeding a booking to then cancel it.
- Story 2.3's `deferred-work.md`/retro-action-item check is itself a standing per-story requirement — already performed above under "Current codebase state."

### Project Structure Notes

- Backend modifications: `Repositories/IAppointmentRepository.cs`, `Repositories/AppointmentRepository.cs` (`Cancel` → `TryCancel`, ordering added to `FindUpcomingByCustomer`), `Services/IBookingService.cs`, `Services/BookingService.cs` (`Cancel` signature + authorization), `Controllers/BookingController.cs` (two new actions), `BarbershopApi.Tests/AppointmentRepositoryTests.cs`, `BarbershopApi.Tests/BookingServiceTests.cs`, `BarbershopApi.Tests/BookingControllerTests.cs`.
- Frontend modifications: `frontend/src/api/BookingApi.js` (two new exports), `frontend/src/pages/ScheduleAppointment.jsx`, `frontend/src/pages/ScheduleAppointment.css`, `frontend/src/pages/ScheduleAppointment.test.jsx`.
- No new files, no new migration, no schema change, no new route/page.

### References

- [Source: _bmad-output/planning-artifacts/epics.md §Story 2.4, §Epic 2, §UX-DR9/10/11] — story statement, all 6 ACs verbatim, FR24/FR25/FR30 mapping, forward dependency of Stories 2.5/2.6 on this story's cancel mechanism.
- [Source: _bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/ARCHITECTURE-SPINE.md AD-1, AD-8, AD-9, AD-12, AD-17] — layering, soft-cancel/Finished computation, index-exclusion-on-cancel, EST authority, single shared read path.
- [Source: _bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/SOLUTION-DESIGN.md §1 NFR2, §4, §7] — "first commit wins" extended to cancel races; "stale cancellation" named as an error category with no mechanism specified (the gap Task 1/2 close).
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-bmad-learning-project-2026-07-23/EXPERIENCE.md §IA table, §Component Patterns, §State Patterns, §Voice and Tone] — My Appointments list placement/behavior, empty-state copy, exact "already cancelled" error copy, confirm-popup dismiss behavior.
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-bmad-learning-project-2026-07-23/mockups/schedule-appointment.html, mockups/confirm-popup.html] — exact row/dialog markup shape (`.appt-row`/`.appt-info`/`.appt-primary`/`.appt-meta`, popup heading/detail/buttons).
- [Source: _bmad-output/implementation-artifacts/2-3-double-booking-and-self-conflict-guards.md] — two-`DbContext` deterministic race-test pattern this story's Task 4 mirrors; `SqliteApiFactory` pool-corruption fix; wall-clock test-literal convention.
- [Source: _bmad-output/implementation-artifacts/deferred-work.md §"code review of story-2-1"] — the `Cancel` read-then-write race gap this story closes.
- [Source: backend/BarbershopApi/Services/BookingService.cs, IBookingService.cs, Controllers/BookingController.cs, Repositories/AppointmentRepository.cs, IAppointmentRepository.cs, Entities/Appointment.cs, Entities/Role.cs, Dtos/AppointmentView.cs] — exact current signatures/behavior this story extends; confirmed no `RowVersion` exists on `Appointment`.
- [Source: backend/BarbershopApi.Tests/AppointmentRepositoryTests.cs, BookingServiceTests.cs, BookingControllerTests.cs] — exact existing test helpers/conventions this story's new tests reuse (`SeedAccount`, `NewAppointment`, `NewService`, `AuthedRequest`, `NextBookableWeekday`).
- [Source: frontend/src/pages/ScheduleAppointment.jsx, ScheduleAppointment.css, ScheduleAppointment.test.jsx, api/BookingApi.js, components/ConfirmPopup.jsx, components/ConfirmationScreen.jsx, utils/FormatSchedule.js, styles/tokens.css] — confirmed no customer-facing "my appointments" UI exists yet; exact reusable components/tokens/formatters; confirmed `ConfirmationScreen` has no back-to-form affordance.
- [Source: project-context.md §Concurrency / race conditions, §Naming, §Testing Rules] — NFR2 restated; exception-naming precedent; fixture-name convention.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (Amelia persona)

### Debug Log References

None — no failures requiring investigation. Full backend and frontend suites both green on first run after each task's implementation.

### Completion Notes List

- Task 1: Replaced `IAppointmentRepository.Cancel(Appointment)` with `TryCancel(int appointmentId, DateTime cancelledAtUtc)` using `ExecuteUpdateAsync` against the `CancelledAt IS NULL` predicate — no new column, no read-then-write gap. Updated all 3 existing repository test call sites and added 2 new tests (already-cancelled no-op, nonexistent id).
- Task 2: `BookingService.Cancel` now takes `(int appointmentId, int callerAccountId, Role callerRole)` and authorizes Customer (owns as customer)/Barber (owns as barber)/Admin (always) before calling `TryCancel`; unauthorized callers get the same `AppointmentNotFoundException` as a truly missing id (never leaks existence). Updated 2 existing service tests, added 4 new authorization tests.
- Task 3: Added `GET /api/booking/mine` and `POST /api/booking/{id}/cancel` to `BookingController`, reusing the existing `BookingService.FindUpcomingByCustomer` read path (AD-17) and mapping `AppointmentNotFoundException`/`AppointmentAlreadyCancelledException` to 404/409. Added `.OrderBy(a => a.Date).ThenBy(a => a.StartTime)` to `FindUpcomingByCustomer` (soonest-first) as part of this same task, since it touches the same method Task 1 already had open. Added 7 new controller tests.
- Task 4: Added the two-`DbContext` deterministic cancel-race test (`TryCancel_returns_false_when_a_second_context_cancels_after_the_first_commits_first`) alongside Task 1's other repository test changes — no staged pre-read needed since `ExecuteUpdateAsync`'s `WHERE` clause is evaluated fresh against the DB on every call.
- Task 5: Added `getMyAppointments`/`cancelAppointment` to `BookingApi.js` (the latter checks `response.ok` before ever parsing a body, since a 204 has none). Added My Appointments list + `ConfirmPopup`-driven cancel flow to `ScheduleAppointment.jsx` below the existing booking form, reusing `formatTimeLabel`/`formatDateLabel`/`ConfirmPopup`/`Button` as specified — no new components. A 409 on cancel shows the exact "This appointment has already been cancelled." copy and still refetches (the row disappears even though this click "failed"). Extended `mockFetch` in `ScheduleAppointment.test.jsx` with `/api/booking/mine` and `/api/booking/{id}/cancel` stubs; added 6 new tests covering empty state, populated row, popup open, confirm-then-refetch, 409 handling, and Go-Back-makes-no-network-call (reusing `ConfirmPopup.test.jsx`'s own dismissal coverage rather than re-deriving it).
- Task 6: branch `story/2.4-my-appointments-view-cancel-and-race-safety` already checked out off `main` (pre-created). Push + CI confirmation intentionally left unchecked and undone — per standing preference, Jack handles commit/push/PR/CI-confirmation himself; that checkbox should be updated once he's done so.
- Full backend suite: 149/149 passing (`dotnet test`). Full frontend suite: 122/122 passing (`vitest run`). `eslint .` and `prettier --check .` both clean.
- Checked `deferred-work.md` and the sprint-status action items at story start: the one item scoped to this story (the `Cancel` read-then-write race, "code review of story-2-1") is fully closed by Tasks 1/4. The open NavBar-overflow and Story-2.3-race-test-pattern action items don't apply to this story's task list (NavBar isn't touched by any AC here; the race-test pattern requirement is already followed in Task 4).

### File List

- `backend/BarbershopApi/Repositories/IAppointmentRepository.cs` (modified — `Cancel` → `TryCancel`)
- `backend/BarbershopApi/Repositories/AppointmentRepository.cs` (modified — `TryCancel` via `ExecuteUpdateAsync`; `FindUpcomingByCustomer` ordered soonest-first)
- `backend/BarbershopApi/Services/IBookingService.cs` (modified — `Cancel` signature adds `callerAccountId`/`callerRole`)
- `backend/BarbershopApi/Services/BookingService.cs` (modified — caller-scoped authorization in `Cancel`)
- `backend/BarbershopApi/Controllers/BookingController.cs` (modified — new `GetMyAppointments`/`CancelBooking` actions)
- `backend/BarbershopApi.Tests/AppointmentRepositoryTests.cs` (modified — `Cancel` call sites updated; 4 new `TryCancel` tests including the deterministic race test)
- `backend/BarbershopApi.Tests/BookingServiceTests.cs` (modified — `Cancel` call sites updated; 4 new authorization tests)
- `backend/BarbershopApi.Tests/BookingControllerTests.cs` (modified — 7 new `GetMyAppointments`/`CancelBooking` tests)
- `frontend/src/api/BookingApi.js` (modified — `getMyAppointments`/`cancelAppointment` added)
- `frontend/src/pages/ScheduleAppointment.jsx` (modified — My Appointments list + cancel flow)
- `frontend/src/pages/ScheduleAppointment.css` (modified — `.section-title`/`.appt-row`/`.appt-info`/`.appt-primary`/`.appt-meta` added)
- `frontend/src/pages/ScheduleAppointment.test.jsx` (modified — 6 new tests, `mockFetch` extended)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified — story status tracking)

### Change Log

- 2026-08-07: Implemented Story 2.4 — race-safe `TryCancel` conditional-update primitive, caller-scoped authorization in `BookingService.Cancel` (customer/barber/admin, shared by Stories 2.5/2.6), `GET /api/booking/mine` + `POST /api/booking/{id}/cancel` endpoints, and the My Appointments list/cancel UI on the Schedule Appointment page.
