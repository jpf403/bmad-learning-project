---
baseline_commit: d20a98cd285c99b25a74a546b5e557ceb0685b6c
---

# Story 2.5: Barber's Own Schedule View

Status: ready-for-dev

## Story

As a barber,
I want to see my own day's schedule and cancel appointments from it,
so that I know who's coming in without digging through anyone else's calendar.

## Acceptance Criteria

1. **Given** a barber signs in, **when** they land on My Schedule, **then** it defaults to today and only ever shows their own day (FR11).
2. **Given** the schedule view, **when** rendered, **then** it lists every fixed 30-min slot from 9:00 AM–4:30 PM; booked slots show the customer's name, open slots show "Available" (FR13, UX-DR11).
3. **Given** a weekend date reached via the day-nav arrows, **when** viewed, **then** it shows no bookable slot grid, consistent with the shop-closed rule (FR13, FR7).
4. **Given** the day-nav arrows, **when** clicked, **then** the view steps one day at a time in either direction (FR12, UX-DR14).
5. **Given** a barber's schedule query, **when** executed, **then** it returns only that barber's own appointments — enforced server-side, not just by the UI (FR14).
6. **Given** a booked slot on the barber's own schedule, **when** they click Cancel, **then** it reuses Story 2.4's confirm-popup-then-soft-cancel flow, freeing the slot (FR26, FR30).

## Tasks / Subtasks

- [ ] **Task 1: `BookingService.GetDaySchedule` — a new shared read-model merging fixed slots with bookings** (AC #1, #2, #5) — no existing service method returns "all 16 fixed slots, booked-or-open" for a given barber/date; `FindByBarberAndDate` (built in Story 2.1, reused unchanged here) already returns only the *booked* appointments for a barber/date, already scoped server-side to that one `barberId` — this task adds a thin merge on top, it does not touch or duplicate that filtering.
  - [ ] Add `backend/BarbershopApi/Dtos/ScheduleSlotView.cs`:
    ```csharp
    namespace BarbershopApi.Dtos;

    public class ScheduleSlotView
    {
        public string StartTime { get; set; } = string.Empty;
        public AppointmentView? Appointment { get; set; }
    }
    ```
  - [ ] Add `backend/BarbershopApi/Dtos/DayScheduleView.cs`:
    ```csharp
    namespace BarbershopApi.Dtos;

    public class DayScheduleView
    {
        public string Date { get; set; } = string.Empty;
        public List<ScheduleSlotView> Slots { get; set; } = [];
    }
    ```
  - [ ] Add to `IBookingService.cs`: `Task<DayScheduleView> GetDaySchedule(int barberId, string? date = null, DateTime? now = null);`
  - [ ] Implement in `BookingService.cs`, alongside the existing `FindByBarberAndDate`/`GetAvailableSlots` methods (reuses the private `FixedSlots` list already built for `GetAvailableSlots` — do not redefine the 9:00–4:30 slot list a second time):
    ```csharp
    public async Task<DayScheduleView> GetDaySchedule(int barberId, string? date = null, DateTime? now = null)
    {
        var nowEst = ResolveNowEst(now);
        var resolvedDate = date ?? nowEst.ToString("yyyy-MM-dd");

        var booked = await FindByBarberAndDate(barberId, resolvedDate);
        var byStartTime = booked.ToDictionary(a => a.StartTime);

        var slots = FixedSlots
            .Select(time => new ScheduleSlotView
            {
                StartTime = time,
                Appointment = byStartTime.GetValueOrDefault(time),
            })
            .ToList();

        return new DayScheduleView { Date = resolvedDate, Slots = slots };
    }
    ```
    `date is null` is exactly how the controller signals "no date requested" (AC #1's "defaults to today"); resolving that default from `nowEst` here — not on the frontend — is required by AD-12 (server is sole authority on "today"). `FindByBarberAndDate` already excludes cancelled appointments (`AppointmentRepository.FindByBarberAndDate`'s `CancelledAt == null` filter, Story 2.1) and is already scoped to the one `barberId` passed in — a cancelled or another-barber's appointment can never appear in `byStartTime`, so no extra filtering is needed here.

- [ ] **Task 2: `GET /api/booking/schedule` on `BookingController`, role-gated to Barber and forcing the caller's own id** (AC #1, #5) — new action on the existing `BookingController` (AD-1: Booking is already the right domain concept, no new controller).
  - [ ] Add:
    ```csharp
    [HttpGet("schedule")]
    public async Task<IActionResult> GetSchedule([FromQuery] string? date)
    {
        var account = (Account)HttpContext.Items["Account"]!;
        if (account.Role != Role.Barber)
        {
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Only barbers can view their own schedule.");
        }
        if (date is not null && !ValidCalendarDateAttribute.IsValidDate(date))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Date must be in yyyy-MM-dd format.");
        }
        return Ok(await bookingService.GetDaySchedule(account.Id, date));
    }
    ```
    `account.Id` is always what's passed as `barberId` — never anything from the query string or body — which is what makes AC #5 ("enforced server-side, not just by the UI") true regardless of what a barber's client sends. **Role gate is deliberately Barber-only, not Barber-or-Admin**: Story 2.6 ("Admin Schedule Oversight," currently `backlog` in sprint-status.yaml) is what adds a `barberId`-accepting variant for Admin to this same action — that story owns deciding how an admin selects/passes a target barber (its own AC: "Select Barber dropdown defaulting to the first barber"). Widening this endpoint's role check now, ahead of that decision being made, would be exactly the kind of speculative generality the project's engineering conventions ask you to avoid. Returning 403 for a non-Barber caller today (Customer or Admin) is correct defense-in-depth, not a gap this story leaves open. Note this is a narrower kind of forward-compatibility than Story 2.4's `BookingService.Cancel`: that method's signature never needed to change again for 2.5/2.6 to consume it, but this `GetSchedule` *action* will need direct edits in Story 2.6 (an admin branch, a `barberId` query param) — only the underlying `BookingService.GetDaySchedule`/`FindByBarberAndDate` methods are already generic enough to need no changes there.
  - [ ] `date=` (an explicitly empty query string, as opposed to the param being omitted entirely) fails `ValidCalendarDateAttribute.IsValidDate` and returns 400, the same as any other malformed value — it is not treated as "no date supplied." This mirrors `GetAvailability`'s existing handling of a present-but-empty `date` param; no special-case is needed to keep the two endpoints consistent.
  - [ ] Reuse `ValidCalendarDateAttribute.IsValidDate` (already `using`'d via `Dtos`, same static helper `GetAvailability` already calls) — do not write a second date-format check.

- [ ] **Task 3: Frontend `/my-schedule` route — and the Admin/Barber route-collision this story must not regress** (AC #1) — `landingRoutes.js`'s `LANDING_ROUTE.Admin` has pointed at `/my-schedule` since Story 1.5, in anticipation of Story 2.6, but no `/my-schedule` route exists in `App.jsx` yet (confirmed: `frontend/src/App.jsx` has no such `<Route>`, and Story 2.4's Dev Notes explicitly flagged `/my-schedule` as "unrelated, not-yet-built"). Today, an Admin who signs in lands on an unmatched path and sees a blank page — a known, silent gap. **Adding the route with `roles={['Barber']}` only would make this actively worse, not better**: `RequireRole` redirects a wrong-role user to `LANDING_ROUTE[their role]` (`RequireRole.jsx:49`) — for an Admin hitting a Barber-only `/my-schedule`, that redirect target is `/my-schedule` itself, an infinite redirect loop. This story must ship the route as `roles={['Barber', 'Admin']}` specifically to prevent that regression, even though the Admin-facing content (Task 4) stays a placeholder until Story 2.6.
  - [ ] In `App.jsx`, import `MySchedule` and add, alongside the existing `/schedule-appointment` route:
    ```jsx
    <Route
      path="/my-schedule"
      element={
        <RequireRole roles={['Barber', 'Admin']}>
          <MySchedule />
        </RequireRole>
      }
    />
    ```

- [ ] **Task 4: `MySchedule.jsx` page — barber flow (load, date-nav, slot grid, cancel) plus an explicit Admin placeholder** (AC #1–#6)
  - [ ] Add to `frontend/src/utils/FormatSchedule.js` (extends the existing module, does not create a parallel date-utility file):
    ```js
    export function addDays(date, delta) {
      const [year, month, day] = date.split('-').map(Number)
      const result = new Date(year, month - 1, day)
      result.setDate(result.getDate() + delta)
      const y = result.getFullYear()
      const m = String(result.getMonth() + 1).padStart(2, '0')
      const d = String(result.getDate()).padStart(2, '0')
      return `${y}-${m}-${d}`
    }

    export function isWeekend(date) {
      const [year, month, day] = date.split('-').map(Number)
      const dayOfWeek = new Date(year, month - 1, day).getDay()
      return dayOfWeek === 0 || dayOfWeek === 6
    }

    export function formatDateHeader(date) {
      const [year, month, day] = date.split('-').map(Number)
      const weekday = new Date(year, month - 1, day).toLocaleDateString(
        'en-US',
        { weekday: 'long' },
      )
      return `${weekday}, ${formatDateLabel(date)}`
    }
    ```
    Constructing `new Date(year, month - 1, day)` (not `new Date(dateString)`) is deliberate — `Calendar.jsx`'s existing `fromDateString` uses the identical pattern specifically to avoid `new Date('yyyy-MM-dd')`'s UTC-midnight parsing, which can shift the displayed day by one in negative-UTC-offset zones. `isWeekend`/`addDays` are pure calendar-string math on an already-resolved date, not "now"-dependent timezone math — computing which weekday `2026-08-15` falls on doesn't depend on what timezone the computer thinks it's in, so this does not violate AD-12's "client never does timezone math" (that rule is about interpreting *current time*/"today", which this story still resolves exclusively via the server in Task 1/2 — the client never guesses what today's date is).
  - [ ] Add to `frontend/src/api/BookingApi.js`:
    ```js
    export async function getSchedule(accessToken, date) {
      let response
      try {
        response = await fetch(
          `${API_BASE_URL}/api/booking/schedule${date ? `?date=${date}` : ''}`,
          {
            credentials: 'include',
            headers: { Authorization: `Bearer ${accessToken}` },
          },
        )
      } catch {
        return { ok: false, status: null }
      }

      const body = await response.json().catch(() => null)
      if (!response.ok || body === null) {
        return {
          ok: false,
          status: response.ok ? null : response.status,
          problem: body,
        }
      }
      return { ok: true, schedule: body }
    }
    ```
  - [ ] Add `frontend/src/pages/MySchedule.jsx`. Structure mirrors `ScheduleAppointment.jsx`'s established patterns exactly (a `fetch*`-returns-a-result-object helper, an `isMountedRef` guard, a `cancellingId` in-flight guard, `ConfirmPopup` reuse) — do not invent a different data-loading shape for this page:
    ```jsx
    import { useEffect, useRef, useState } from 'react'
    import { useAuth } from '../context/AuthContext'
    import { getSchedule, cancelAppointment } from '../api/BookingApi'
    import {
      formatTimeLabel,
      formatDateLabel,
      addDays,
      isWeekend,
      formatDateHeader,
    } from '../utils/FormatSchedule'
    import ConfirmPopup from '../components/ConfirmPopup'
    import Button from '../components/Button'
    import './MySchedule.css'

    export default function MySchedule() {
      const { user } = useAuth()

      const [date, setDate] = useState(null)
      const [slots, setSlots] = useState([])
      const [loading, setLoading] = useState(true)
      const [scheduleError, setScheduleError] = useState('')
      const [cancelTarget, setCancelTarget] = useState(null)
      const [cancelError, setCancelError] = useState('')
      const [cancellingId, setCancellingId] = useState(null)
      const isMountedRef = useRef(true)

      useEffect(() => {
        return () => {
          isMountedRef.current = false
        }
      }, [])

      async function fetchSchedule(explicitDate) {
        const result = await getSchedule(user.accessToken, explicitDate)
        if (result.ok) {
          return {
            date: result.schedule.date,
            slots: result.schedule.slots,
            errorMessage: '',
          }
        }
        return {
          date: null,
          slots: [],
          errorMessage: 'Could not load your schedule. Please try again.',
        }
      }

      async function loadDate(explicitDate) {
        setLoading(true)
        const result = await fetchSchedule(explicitDate)
        if (!isMountedRef.current) {
          return
        }
        setLoading(false)
        if (result.errorMessage) {
          setScheduleError(result.errorMessage)
        } else {
          setDate(result.date)
          setSlots(result.slots)
          setScheduleError('')
        }
      }

      useEffect(() => {
        if (user.role !== 'Barber') {
          return
        }
        let cancelled = false

        async function load() {
          const result = await fetchSchedule(null)
          if (cancelled) {
            return
          }
          setLoading(false)
          if (result.errorMessage) {
            setScheduleError(result.errorMessage)
          } else {
            setDate(result.date)
            setSlots(result.slots)
            setScheduleError('')
          }
        }

        load()
        return () => {
          cancelled = true
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
      }, [user.accessToken, user.role])

      async function handleCancelConfirmed() {
        const target = cancelTarget
        if (cancellingId !== null) {
          return
        }
        setCancellingId(target.id)
        setCancelError('')
        const result = await cancelAppointment(user.accessToken, target.id)

        if (result.ok) {
          await loadDate(date)
          setCancellingId(null)
          return
        }
        if (result.status === 409) {
          setCancelError('This appointment has already been cancelled.')
          await loadDate(date)
          setCancellingId(null)
          return
        }
        setCancelError('Something went wrong. Please try again.')
        setCancellingId(null)
      }

      if (user.role !== 'Barber') {
        // Story 2.6 builds the Admin variant (Select Barber dropdown +
        // this same view). Landing an Admin here without infinite-looping
        // (see Task 3) requires this route to accept Admin, but there is
        // no barber to show a schedule for yet -- this placeholder is the
        // deliberate, temporary stand-in until that story lands.
        return (
          <div className="my-schedule">
            <h1 className="my-schedule__title">My Schedule</h1>
            <p className="my-schedule__loading">
              Barber schedule selection is not yet available.
            </p>
          </div>
        )
      }

      const weekend = date !== null && isWeekend(date)

      return (
        <div className="my-schedule">
          <h1 className="my-schedule__title">My Schedule</h1>

          {loading ? (
            <p className="my-schedule__loading">Loading…</p>
          ) : scheduleError ? (
            <p className="my-schedule__error">{scheduleError}</p>
          ) : (
            <>
              <div className="date-header-row">
                <button
                  type="button"
                  className="date-nav-arrow"
                  aria-label="Previous day"
                  onClick={() => loadDate(addDays(date, -1))}
                >
                  &#8249;
                </button>
                <h2 className="date-title">{formatDateHeader(date)}</h2>
                <button
                  type="button"
                  className="date-nav-arrow"
                  aria-label="Next day"
                  onClick={() => loadDate(addDays(date, 1))}
                >
                  &#8250;
                </button>
              </div>

              {weekend ? (
                <p className="my-schedule__closed">
                  Closed — the shop is not open on weekends.
                </p>
              ) : (
                <div className="my-schedule__slot-list">
                  {slots.map((slot) =>
                    slot.appointment ? (
                      <div className="slot-row slot-booked" key={slot.startTime}>
                        <span className="slot-time">
                          {formatTimeLabel(slot.startTime)}
                        </span>
                        <span className="slot-name">
                          {slot.appointment.customerName}
                        </span>
                        <Button
                          variant="destructive"
                          disabled={cancellingId !== null}
                          onClick={() => {
                            setCancelError('')
                            setCancelTarget(slot.appointment)
                          }}
                        >
                          Cancel
                        </Button>
                      </div>
                    ) : (
                      <div className="slot-row slot-open" key={slot.startTime}>
                        <span className="slot-time">
                          {formatTimeLabel(slot.startTime)}
                        </span>
                        <span className="slot-status">Available</span>
                      </div>
                    ),
                  )}
                </div>
              )}
              {cancelError && (
                <p className="my-schedule__error">{cancelError}</p>
              )}
            </>
          )}

          <ConfirmPopup
            open={cancelTarget !== null}
            onOpenChange={(open) => !open && setCancelTarget(null)}
            title="Cancel this appointment?"
            message={
              cancelTarget &&
              `${cancelTarget.customerName} — ${formatTimeLabel(cancelTarget.startTime)}, ${formatDateLabel(cancelTarget.date)}. This cannot be undone.`
            }
            destructive
            confirmLabel="Confirm"
            onConfirm={handleCancelConfirmed}
          />
        </div>
      )
    }
    ```
    `formatDateLabel`, used in the popup message template above, is not new — it already exists in `utils/FormatSchedule.js` alongside `formatTimeLabel`; only `addDays`/`isWeekend`/`formatDateHeader` are new exports (Task 4's first sub-item). **"Available" slots render even for a slot in the past on today's date** — no AC in this story (or Story 2.4/2.1) calls for a "Finished" concept on My Schedule, unlike the customer's My Appointments list; do not add one.
  - [ ] Add `frontend/src/pages/MySchedule.css`, following `DESIGN.md`'s tinted-row treatment (`{components.schedule-row-open}` / `{components.schedule-row-booked}`) and `{components.date-nav-arrow}` tokens, mapped to the existing CSS custom properties in `styles/tokens.css` (`--color-neutral`, `--color-border`, `--rounded-default`, spacing scale) — do not hardcode raw hex/px values, matching how Story 2.4 mapped `mockups/schedule-appointment.html`'s pixel values to tokens:
    ```css
    .my-schedule {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-6);
      max-width: 1120px;
      margin: 0 auto;
      padding: var(--spacing-12) var(--spacing-gutter-mobile);
    }

    .my-schedule__title {
      font-family: var(--font-family-base);
      font-size: var(--typography-h1-size);
      font-weight: var(--typography-h1-weight);
      color: var(--color-text);
      margin: 0;
    }

    .my-schedule__loading,
    .my-schedule__closed {
      font-family: var(--font-family-base);
      font-size: var(--typography-body-size);
      color: var(--color-text-muted);
      margin: 0;
    }

    .my-schedule__error {
      color: var(--color-error);
      font-family: var(--font-family-base);
      font-size: var(--typography-caption-size);
      margin: 0;
    }

    .date-header-row {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: var(--spacing-5);
      flex-wrap: wrap;
    }

    .date-nav-arrow {
      background: none;
      border: none;
      padding: 0;
      width: 20px;
      height: 20px;
      font-size: var(--typography-h2-size);
      line-height: 1;
      color: var(--color-text-muted);
      cursor: pointer;
    }

    .date-nav-arrow:hover {
      color: var(--color-primary);
    }

    .date-title {
      font-family: var(--font-family-base);
      font-size: var(--typography-h2-size);
      font-weight: var(--typography-h2-weight);
      color: var(--color-text);
      margin: 0;
      min-width: 220px;
      text-align: center;
    }

    .my-schedule__slot-list {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-2);
    }

    .slot-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--spacing-4);
      background: var(--color-neutral);
      border-radius: var(--rounded-default);
      padding: var(--spacing-3) var(--spacing-5);
    }

    .slot-row:hover {
      background: var(--color-border);
    }

    .slot-time {
      font-family: var(--font-family-base);
      font-size: var(--typography-body-sm-size);
      color: var(--color-text);
      width: 100px;
      flex-shrink: 0;
    }

    .slot-open .slot-status {
      font-family: var(--font-family-base);
      font-size: var(--typography-body-size);
      color: var(--color-text-muted);
    }

    .slot-booked .slot-name {
      font-family: var(--font-family-base);
      font-size: var(--typography-body-size);
      color: var(--color-text);
      flex: 1;
    }
    ```
    Class names here are split deliberately: `.slot-row`/`.slot-open`/`.slot-booked`/`.slot-time`/`.slot-name`/`.slot-status`/`.date-header-row`/`.date-title` are lifted literally from `mockups/my-schedule.html` (matching Story 2.4's own precedent of reusing its mockup's exact class names for `.appt-row`/`.appt-primary`/`.appt-meta`); `.date-nav-arrow` intentionally does **not** match the mockup's own throwaway `.date-arrow` shorthand — it's named after `DESIGN.md`'s actual token, `{components.date-nav-arrow}`, since that token (not the mockup file) is the authoritative source for this component's hover/disabled-state contract. `date-nav-arrow`'s hover-to-primary rule fires on pointer devices via a plain `:hover` — no separate touch handling is needed since the click itself (not the hover) is the actual action, consistent with the project's hover-is-additive-polish convention already established elsewhere (`NavBar.css`, `Button.css`).

- [ ] **Task 5: Backend tests** (AC #1, #2, #5)
  - [ ] Add to `BookingServiceTests.cs`: `GetDaySchedule_returns_all_sixteen_fixed_slots_as_available_when_nothing_booked` (assert `Slots.Count == 16`, every `Appointment` is null); `GetDaySchedule_attaches_the_booked_appointment_to_its_matching_slot_and_leaves_others_available` (book one slot via `service.Create(...)`, assert exactly that slot's `Appointment` is non-null with the right `CustomerName`, all 15 others remain null); `GetDaySchedule_only_includes_this_barbers_own_appointments` (seed two barbers, book each at the same date/time, call `GetDaySchedule` for barber A, assert barber B's booking never appears in barber A's slots); `GetDaySchedule_excludes_a_cancelled_appointment_from_the_booked_slot` (book, cancel via `service.Cancel(...)`, assert the slot reverts to `Appointment == null`); `GetDaySchedule_defaults_to_todays_EST_date_when_date_is_omitted` (call with `date: null, now: FixedNow`, assert `Date` equals `FixedNow`'s date string); `GetDaySchedule_returns_a_full_available_slot_list_for_a_weekend_date_with_no_special_casing` (call with a Saturday/Sunday date string, assert all 16 slots come back with `Appointment == null` rather than an empty or error response) — this pins down, as an explicit regression test, the deliberate design choice that weekend-closure (AC #3) is enforced entirely client-side in `MySchedule.jsx`'s `isWeekend()` check, not server-side; without this test, a future change that made `GetDaySchedule` weekday-only (mirroring `Create`'s AD-14 validation) could silently break the day-nav UX with nothing catching it. Reuse the existing `SeedAccount`/`NewService`/`FixedNow` helpers already in this file — do not redefine them.
  - [ ] Add to `BookingControllerTests.cs`: `GetSchedule_returns_all_sixteen_slots_with_the_booking_attached_to_its_slot` (seed a barber, book one slot as a customer, call `GET /api/booking/schedule?date=...` as the barber, assert 16 slots with the one match); `GetSchedule_without_date_param_defaults_to_today` (call with no `date` query param, assert response `Date` equals `DateTime.Today.ToString("yyyy-MM-dd")` — the same acceptable wall-clock-vs-EST imprecision this file's own `NextBookableWeekday()`/`DateTime.Today` pattern already relies on elsewhere, not a new flakiness source); `GetSchedule_only_returns_the_callers_own_appointments_not_another_barbers` (two barbers, book each, assert barber A's response never contains barber B's booking); `GetSchedule_with_malformed_date_returns_400`; `GetSchedule_customer_caller_returns_403`; `GetSchedule_admin_caller_returns_403`. Every existing test in this file authenticates via `RegisterAndLogin`, which always creates a `Role.Customer` account — there is no non-Customer HTTP login helper in `BookingControllerTests.cs` yet. Reuse `RoleGatingTests.cs`'s `RegisterAndLoginAs(client, role)` helper (Story 1.6 — registers, then flips the seeded account's role in the DB before logging in) rather than inventing a second, parallel one in this file. `GetSchedule_without_access_token_returns_401`. Reuse `AuthedRequest`/`SeedAccount`/`NextBookableWeekday` already in this file.

- [ ] **Task 6: Frontend tests — `MySchedule.test.jsx`** (AC #1–#6) — extend the same `vi.spyOn(globalThis, 'fetch')` stubbing style already used in `ScheduleAppointment.test.jsx`, not a new mocking approach.
  - [ ] Barber role: renders "Loading…" during the initial fetch; renders all 16 slots with "Available" text when the stubbed `/api/booking/schedule` response has no bookings; renders a booked slot's customer name plus a Cancel button; clicking a date-nav arrow triggers a re-fetch with the adjacent date and updates the displayed header (assert the second `fetch` call's URL contains the expected `date` query value); a weekend-dated response renders the "Closed" message and no slot rows/Cancel buttons; clicking Cancel opens the confirm popup with the exact title/message shape; confirming calls `cancelAppointment` then re-fetches the current date and re-renders; a `409` cancel response shows "This appointment has already been cancelled." and still re-fetches; dismissing the popup (Go Back) makes no network call (reuse `ConfirmPopup.test.jsx`'s own dismissal coverage, as Story 2.4 already established this reuse pattern, rather than re-deriving it).
  - [ ] Admin role: renders the "Barber schedule selection is not yet available." placeholder and never calls `GET /api/booking/schedule` (assert `fetch` was never called with a URL containing `/api/booking/schedule`).

- [ ] **Task 7: Verify CI green and branch/PR**
  - [ ] Branch as `story/2.5-barbers-own-schedule-view` from `main`.
  - [ ] Push and confirm both CI jobs (backend .NET, frontend Vite/React) green on GitHub before merging (AD-11). Per standing preference, Jack handles commit/push/PR/CI-confirmation himself — leave this checkbox unchecked for him to update.

## Dev Notes

### Architecture Compliance (must-follow, not optional)

- **AD-1 (layering)** — `GetSchedule` lives on the existing `BookingController` → `BookingService` → `AppointmentRepository` trio; no new controller/service/repository.
- **AD-8/AD-9 (soft-cancel, index-exclusion-on-cancel)** — `GetDaySchedule` reuses `FindByBarberAndDate` unchanged, which already excludes cancelled rows; cancelling here reuses the exact `TryCancel`/`BookingService.Cancel` mechanism Story 2.4 built (its `Role.Barber` authorization branch — `appointment.BarberId == callerAccountId` — was written in 2.4 specifically for this story to consume unmodified; this story does not touch `BookingService.Cancel` at all).
- **AD-12 (EST authority)** — "today" for AC #1's default is resolved exclusively server-side (`GetDaySchedule`'s `ResolveNowEst`, reused unchanged from Story 2.2/2.3/2.4) — the frontend never guesses what today's date is; it only receives it. Weekday determination for AC #3's weekend closure (`isWeekend` in `FormatSchedule.js`) is pure calendar-string math on an already-server-resolved date, not a timezone judgment call, so it is safe to compute client-side — see Task 4's note for why this doesn't violate AD-12.
- **AD-14 (server-side booking re-validation)** — unaffected; this story adds no new write path, only a read (`GetDaySchedule`) and reuses the existing cancel write path.
- **AD-17 (single shared read path)** — `GetDaySchedule` is built *on top of* `FindByBarberAndDate`, the same method the (not-yet-built) Story 2.6 admin view and this story both will call — it does not reimplement appointment lookup, only adds the fixed-slot merge and the "no date supplied" default. `ToView`'s `Finished` computation is untouched and irrelevant to this story's rendering (My Schedule never shows a "Finished" state, unlike My Appointments — see Task 4's note).
- **That the view defaults to today is not in question — that's AC #1/FR11, non-negotiable.** What no architecture document specifies is the *mechanism* for resolving "today" on this particular read: confirmed by direct review of `ARCHITECTURE-SPINE.md`/`SOLUTION-DESIGN.md` (AD-17 only requires a *shared* read path across views, not a specific method signature or a "get current date" endpoint) and `epics.md` (Story 2.1's closing AC only guarantees "no further schema changes," not that every read shape needed later already exists). This story's answer: `GetDaySchedule`'s `date` parameter is optional, and when omitted the server resolves it to `nowEst`'s date (AD-12) inside the same call — there is no separate "what's today" round-trip, and the frontend never supplies or guesses a default itself (Task 1/4). Task 1's `GetDaySchedule`/`DayScheduleView` design is this story's answer to that gap, following the same "generic enough for the next story to reuse, not more" precedent Story 2.4 set for `BookingService.Cancel`'s role-parameterized signature.

### Current codebase state relevant to this story (verified by direct file read)

- `/my-schedule` does not exist as a route in `frontend/src/App.jsx` today. `landingRoutes.js`'s `LANDING_ROUTE.Admin`/`LANDING_ROUTE.Barber` have both already pointed at `/my-schedule` since Story 1.5 — **Task 3's `roles={['Barber', 'Admin']}` choice is not optional**; see Task 3 for the exact infinite-redirect-loop regression that a Barber-only route would introduce for Admin sign-in, which does not exist today only because `/my-schedule` currently 404s to a blank page instead.
- `BookingService.FindByBarberAndDate(barberId, date)` (built in Story 2.1, used by Story 2.2 internally, exposed nowhere as its own endpoint yet) already returns only non-cancelled appointments for exactly one `barberId` — confirmed via `AppointmentRepository.FindByBarberAndDate:21-26`. This story is its first consumer as a *dedicated* endpoint's data source.
- `BookingService`'s private `FixedSlots` (9:00–16:30, 30-min increments, `BookingService.cs:153-164`) is the single existing source of truth for the fixed slot list — confirmed no second copy of this list exists anywhere in the backend. Task 1 reuses it directly; nothing in this story redefines it.
- `BookingService.Cancel(appointmentId, callerAccountId, callerRole)` (Story 2.4) already has a working `Role.Barber` branch (`appointment.BarberId == callerAccountId`) and `POST /api/booking/{id}/cancel` already maps its exceptions to 404/409 — confirmed via direct read of `BookingService.cs:110-137` and `BookingController.cs:76-97`. This story's cancel flow calls this exact existing endpoint/method with zero changes.
- Frontend: no `MySchedule.jsx`/`.css` exist yet. `ConfirmPopup.jsx`, `Button.jsx` (`destructive` variant), and `utils/FormatSchedule.js`'s `formatTimeLabel`/`formatDateLabel` are already built and must be reused as-is (same precedent Story 2.4 followed for `ScheduleAppointment.jsx`) — do not recreate any of them.
- Checked `deferred-work.md` and the sprint-status action items at story start, satisfying `sprint-status.yaml`'s first standing action item ("read deferred-work.md for items assigned to it or generally applicable... at the start of every new story") directly. Of the remaining three action items: the NavBar-overflow one (`sprint-status.yaml`'s `action_items`, still listed `status: open`) was actually already resolved in Story 2.2 (`3828d25`'s commit message: "Also fixes the NavBar overflow bug (retro action item) with a responsive collapsed-menu toggle" — confirmed by reading current `NavBar.css`, which already has the `@media (max-width: 1023px)`/`(max-width: 639px)` collapsed-menu rules). That action item's `status: open` in `sprint-status.yaml` is stale bookkeeping, not a real open task — this story does not touch `NavBar.css`, and no future story should either on the assumption this item is still outstanding. The other two open action items (Story 2.3's race-test pattern; Story 2.2's calendar/select scoping) don't apply here: this story introduces no new race condition (its only write path is Story 2.4's already-race-safe `Cancel`, reused unmodified) and builds no new calendar/select component.

### Testing Requirements

- Backend: xUnit.v3 + `WebApplicationFactory`/`SqliteApiFactory` against a real temp SQLite instance, never mocked (AD-4/NFR4). Reuse existing helpers (`SeedAccount`, `NewService`, `FixedNow`, `AuthedRequest`, `NextBookableWeekday`) — do not redefine them per-file.
- Test fixture names: `"John"`/`"Smith"` for the default/incidental party in any test; never a real person's name.
- Frontend: Vitest + jsdom + React Testing Library + user-event; stub `fetch` directly via `vi.spyOn(globalThis, 'fetch')` exactly as `ScheduleAppointment.test.jsx`'s existing pattern does — extend that style, don't build a parallel one.
- No new race condition is introduced by this story (its only write path, cancel, is Story 2.4's already-tested race-safe mechanism, reused unmodified) — no new deterministic two-`DbContext` staging test is needed here.

### Previous Story Intelligence (from Story 2.4)

- Story 2.4 built `BookingService.Cancel(appointmentId, callerAccountId, callerRole)` specifically so Stories 2.5/2.6 would not need to touch it — confirmed true; this story calls it exactly as-is via the existing `POST /api/booking/{id}/cancel` endpoint, no signature or behavior changes.
- Story 2.4 established the `fetch*`-returns-a-result-object plus `isMountedRef`/`cancellingId` guard pattern for cancel flows in `ScheduleAppointment.jsx`, after a review round found and fixed a double-cancel race and stale-error-display bug in an earlier version of that pattern. `MySchedule.jsx` (Task 4) is written to already match the *post-review* shape from the start (component-lifetime `isMountedRef`, `cancellingId`-gated disabled state, error cleared at Cancel-click time) rather than reintroducing and then re-fixing the same three bugs.
- Story 2.4's `SqliteApiFactory` connection-pool fix (Story 2.3) remains in place — no action needed, but if any new test in this story behaves flakily under parallel execution, that fix is the first place to check it hasn't regressed.

### Project Structure Notes

- Backend new files: `Dtos/ScheduleSlotView.cs`, `Dtos/DayScheduleView.cs`.
- Backend modifications: `Services/IBookingService.cs`, `Services/BookingService.cs` (`GetDaySchedule` added), `Controllers/BookingController.cs` (`GetSchedule` action added), `BarbershopApi.Tests/BookingServiceTests.cs`, `BarbershopApi.Tests/BookingControllerTests.cs`.
- Frontend new files: `frontend/src/pages/MySchedule.jsx`, `frontend/src/pages/MySchedule.css`, `frontend/src/pages/MySchedule.test.jsx`.
- Frontend modifications: `frontend/src/App.jsx` (new `/my-schedule` route), `frontend/src/api/BookingApi.js` (`getSchedule` added), `frontend/src/utils/FormatSchedule.js` (`addDays`/`isWeekend`/`formatDateHeader` added).
- No new migration, no schema change (consistent with Story 2.1's closing AC).

### References

- [Source: _bmad-output/planning-artifacts/epics.md §Story 2.5, §Epic 2, §UX-DR11/14] — story statement, all 6 ACs verbatim, FR11-14/FR26/FR30 mapping, dependency on Story 2.4's cancel mechanism, forward dependency of Story 2.6 on this story's `GetDaySchedule`/`FindByBarberAndDate` reuse.
- [Source: _bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/ARCHITECTURE-SPINE.md AD-1, AD-8, AD-9, AD-12, AD-14, AD-17] — layering, soft-cancel/index-exclusion, EST authority, server-side re-validation (unaffected), single shared read path.
- [Source: _bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/SOLUTION-DESIGN.md §4] — "one shared `BookingService` method (or a shared read-model it returns)" — the exact phrasing Task 1's `DayScheduleView` design follows.
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-bmad-learning-project-2026-07-23/DESIGN.md §Components (schedule-row-open/booked, date-nav-arrow), §Elevation & Depth] — tinted-row treatment, hover-to-border-tint, date-nav-arrow resting/hover states, 20px sizing.
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-bmad-learning-project-2026-07-23/EXPERIENCE.md §Information Architecture, §Component Patterns ("Date header + arrows", "Schedule row — open/booked"), §State Patterns ("Cold load | My Schedule", "Weekend / shop-closed", "Stale-cancel conflict")] — My Schedule's IA placement, exact behavioral rules for date-nav/slot rows, loading/weekend/cancel-conflict copy.
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-bmad-learning-project-2026-07-23/mockups/my-schedule.html] — exact row/header markup shape (`.slot-row`/`.slot-time`/`.slot-name`/`.slot-status`, date-header-row layout) this story's barber-only view (no admin barber-select) is derived from.
- [Source: _bmad-output/implementation-artifacts/2-4-my-appointments-view-cancel-and-race-safety.md] — `BookingService.Cancel` signature/authorization this story reuses unmodified; the post-review `fetch*`/`isMountedRef`/`cancellingId` pattern `MySchedule.jsx` is written to match from the start.
- [Source: _bmad-output/implementation-artifacts/deferred-work.md, sprint-status.yaml §action_items] — confirmed the NavBar-overflow action item is already resolved (Story 2.2, commit `3828d25`) despite its stale `open` status; confirmed no other open item applies to this story.
- [Source: backend/BarbershopApi/Services/BookingService.cs, IBookingService.cs, Controllers/BookingController.cs, Repositories/AppointmentRepository.cs, Dtos/AppointmentView.cs, Dtos/ValidCalendarDateAttribute.cs] — exact current signatures/behavior this story extends; confirmed `FixedSlots`/`ResolveNowEst`/`FindByBarberAndDate` are reusable as-is.
- [Source: frontend/src/App.jsx, landingRoutes.js, components/RequireRole.jsx, components/NavBar.jsx, components/ConfirmPopup.jsx, components/Button.jsx, pages/ScheduleAppointment.jsx, ScheduleAppointment.css, utils/FormatSchedule.js, api/BookingApi.js, styles/tokens.css] — confirmed no `/my-schedule` route exists yet; confirmed the Admin/Barber `LANDING_ROUTE` collision Task 3 must avoid; confirmed exact reusable components/tokens/formatters and their current shapes.
- [Source: project-context.md §Framework-Specific Rules (React route guards), §Concurrency, §Testing Rules] — route-guard-calls-`/api/auth/me` convention (already satisfied by reusing `RequireRole`, no new guard logic needed); fixture-name convention.
