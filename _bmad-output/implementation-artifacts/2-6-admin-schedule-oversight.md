---
baseline_commit: b3a38847999e867e827be32418a81a80a4b08a06
---

# Story 2.6: Admin Schedule Oversight

Status: review

## Story

As an admin,
I want to view any barber's schedule via a Select Barber dropdown and cancel appointments from it,
so that I have full oversight without a separate tool.

## Acceptance Criteria

1. **Given** an admin signs in, **when** they land on My Schedule, **then** they see the identical view a barber sees, plus a Select Barber dropdown defaulting to the first barber — never an empty state (FR15).
2. **Given** the admin switches the Select Barber dropdown, **when** a different barber is chosen, **then** the same visible date re-renders for the newly selected barber — the date does not reset (FR15, UX-DR8 admin variant).
3. **Given** the admin's schedule view, **when** rendered, **then** it reads through the exact same shared `BookingService` method used by the customer and barber views — never a separately-implemented admin-only query (AD-17).
4. **Given** a booked slot in the admin's current view, **when** they click Cancel, **then** it reuses Story 2.4's confirm-popup-then-soft-cancel flow (FR27, FR30).

**Required but not in epics.md's numbered ACs** (per this workflow's rule that the system must work end-to-end, not just satisfy stated ACs): zero barber accounts must not crash or hang the view. FR15's own wording ("never an empty state") only promises a default *selection* once barbers exist — it does not claim at least one barber always exists. FR6 already established the sibling precedent for this exact situation (booking's barber selector shows "No barbers available" instead of an empty/broken dropdown). This story must handle it the same way: no crash, no permanently-stuck "Loading…", no dropdown with nothing in it. **Note:** in this project's actual dev/CI environments, this state is not hypothetical-only — see Dev Notes' "Current codebase state" for `BarberSeedService`, which means CI/automated tests genuinely do exercise a zero-barber DB (it never sets the seed env vars), even though Jack's local dev machine normally has at least one seeded barber.

## Scope note (read before starting)

This story adds **zero new business logic**. `BookingService.GetDaySchedule`, `FindByBarberAndDate`, and `Cancel` are all reused completely unmodified from Stories 2.4/2.5 — Story 2.5 built `GetDaySchedule` generic in `barberId` specifically so this story wouldn't need to touch the service or repository layer at all (AD-17). If any part of the implementation seems to require a `BookingService`/`IBookingService`/`AppointmentRepository` change, stop — that's a sign of drifting from the intended design, not a legitimate requirement of this story. The only backend change is a Controller-level `barberId` branch (Task 1); everything else is frontend wiring plus one small, dev-only seeding convenience (Task 7).

## Tasks / Subtasks

- [x] **Task 1: `GET /api/booking/schedule` — accept an admin-supplied `barberId`, keep the barber path untouched** (AC #1, #3) — `GetSchedule` (`BookingController.cs:69-87`) currently 403s any non-`Barber` caller. `BookingService.GetDaySchedule`/`FindByBarberAndDate` are already barber-id-generic (built that way in Story 2.5 specifically so this story would need zero service-layer changes — confirmed via direct read, `BookingService.cs:58,110`) — this task only touches the Controller action, satisfying AC #3 by construction (no new query is written, the existing one is just called with a different `barberId` source).
  - [x] Add `[FromQuery] int? barberId` to `GetSchedule`'s signature. Branch on `account.Role`:
    ```csharp
    [HttpGet("schedule")]
    public async Task<IActionResult> GetSchedule([FromQuery] string? date, [FromQuery] int? barberId)
    {
        var account = (Account)HttpContext.Items["Account"]!;

        int targetBarberId;
        if (account.Role == Role.Barber)
        {
            targetBarberId = account.Id;
        }
        else if (account.Role == Role.Admin)
        {
            if (barberId is null)
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: "barberId is required.");
            }
            var barber = await accountRepository.FindById(barberId.Value);
            if (barber is null || barber.Role != Role.Barber)
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Selected barber is not available.");
            }
            targetBarberId = barberId.Value;
        }
        else
        {
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Only barbers and admins can view a schedule.");
        }

        var dateWasSupplied = Request.Query.ContainsKey("date");
        if (dateWasSupplied && !ValidCalendarDateAttribute.IsValidDate(date ?? string.Empty))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Date must be in yyyy-MM-dd format.");
        }
        return Ok(await bookingService.GetDaySchedule(targetBarberId, dateWasSupplied ? date : null));
    }
    ```
    A `Barber` caller who happens to also pass `barberId` in the query string is **not honored** — `targetBarberId` for that branch is always `account.Id`, exactly as before. This preserves AC #5 from Story 2.5 ("enforced server-side, not just by the UI") unmodified: a barber still cannot view or act on another barber's day by tampering with a query param.
    `accountRepository` is already a constructor dependency on `BookingController` (used by `GetBarbers`/`GetAvailability`/`CreateBooking`) — no new dependency to wire up. The `barber is null || barber.Role != Role.Barber` check and its 400/"Selected barber is not available." message are copied verbatim from `GetAvailability` (`BookingController.cs:30-34`) — reuse that exact wording, don't invent new copy for the same failure mode.
  - [x] **Update the now-incorrect existing test** `GetSchedule_admin_caller_returns_403` (`BookingControllerTests.cs:674-685`) — an Admin calling this endpoint is the entire point of this story, so this test's assertion is now wrong, not just incomplete. Replace it (don't leave both an old-and-new version) with the admin-specific cases in Task 5 below.

- [x] **Task 2: Backend tests for the admin branch** (AC #1, #3)
  - [x] `GetSchedule_admin_without_barberId_returns_400`.
  - [x] `GetSchedule_admin_with_nonexistent_barberId_returns_400`.
  - [x] `GetSchedule_admin_with_a_customer_id_as_barberId_returns_400` (seed a customer, pass their id as `barberId` — same "wrong role, not just wrong id" case `GetAvailability`'s existing tests already cover for booking; mirror that coverage here).
  - [x] `GetSchedule_admin_with_valid_barberId_returns_that_barbers_schedule` (seed two barbers via `SeedAccount` — no HTTP login needed for either, since they're only targets, not callers — book one slot for each, call as Admin with each `barberId` in turn, assert each response only contains that barber's own booking — proves the admin path reuses the same barber-scoped read `GetSchedule_only_returns_the_callers_own_appointments_not_another_barbers` already exercises for the Barber path, satisfying AC #3 without re-testing `GetDaySchedule` itself, which Story 2.5 already covers).
  - [x] `GetSchedule_barber_supplied_barberId_is_ignored` — a Barber caller passing a *different* barber's id as `barberId` still only ever gets their own schedule back (regression guard for the "not honored" rule in Task 1).
  - [x] `GetSchedule_admin_with_malformed_date_returns_400` — the existing malformed-date coverage (`BookingControllerTests.cs:655-659` and neighbors) is Barber-only; since Task 1 resolves `barberId` before validating `date`, add the equivalent case for an Admin caller with a valid `barberId` and a bad `date` value, confirming the ordering doesn't accidentally let a malformed date slip through on the admin branch.
  - [x] Reuse `RoleGatingTests.RegisterAndLoginAs(_factory, client, Role.Admin, email)` (already used by the test you're replacing) and `SeedAccount`/`AuthedRequest` already in this file — do not add a second Admin-login helper.

- [x] **Task 3: `BookingApi.js#getSchedule` — accept an optional `barberId`** (AC #1, #2) — the naive extension of the existing template-literal query string (appending `&barberId=` after a possibly-absent `?date=`) breaks the very first admin page load, where `date` is `null` (defaulting to "today") but `barberId` is set — that call would produce `.../schedule&barberId=5` with no leading `?`. Use `URLSearchParams` instead so param order/presence is never hand-assembled:
    ```js
    export async function getSchedule(accessToken, date, barberId) {
      const params = new URLSearchParams()
      if (date) {
        params.set('date', date)
      }
      if (barberId !== undefined && barberId !== null) {
        params.set('barberId', barberId)
      }
      const query = params.toString()

      let response
      try {
        response = await fetch(
          `${API_BASE_URL}/api/booking/schedule${query ? `?${query}` : ''}`,
          {
            credentials: 'include',
            headers: { Authorization: `Bearer ${accessToken}` },
          },
        )
      } catch {
        return { ok: false, status: null }
      }
      // ...unchanged body below this line
    }
    ```
    A `Barber` caller's existing call sites (`loadDate`'s default param resolves to `barberId` state, which stays `null` for a Barber) never set the `barberId` param — the query string produced for the Barber path is byte-for-byte identical to today's (`?date=...` or empty). Both `null` and `undefined` for `barberId` must be treated as "omit" — `loadDate`'s default-parameter mechanism can hand either through depending on call site, and the explicit `!== undefined && !== null` check (not a truthiness check like `if (barberId)`) is what makes that safe even if a valid `barberId` were ever `0` (not reachable today since account ids start at 1, but don't rely on that).

- [x] **Task 4: `MySchedule.jsx` — Admin barber-loading, default-selection, and the Select Barber dropdown** (AC #1, #2, #4, plus the required no-barbers state above)
  - [x] Replace the current hard `if (user.role !== 'Barber') { return <placeholder> }` early-return with Admin-specific handling. The Barber-only mount effect (`MySchedule.jsx:82-114`) stays completely unchanged — do not fold Admin logic into it; add a **second**, parallel mount effect gated on `user.role === 'Admin'` instead, following the same "own local `cancelled` flag, not `isMountedRef`" shape the existing effect already uses (Story 2.5's Dev Notes: `isMountedRef` is reset to `true` on every StrictMode remount, so it can't distinguish two concurrent invocations the way a per-invocation `cancelled` closure can):
    ```jsx
    const [barbers, setBarbers] = useState([])
    const [barbersLoading, setBarbersLoading] = useState(true)
    const [barbersError, setBarbersError] = useState('')
    const [barberId, setBarberId] = useState(null)

    async function fetchBarbers() {
      const result = await getBarbers(user.accessToken)
      if (result.ok) {
        return { barbers: result.barbers, errorMessage: '' }
      }
      return {
        barbers: [],
        errorMessage: 'Could not load barbers. Please try again.',
      }
    }

    useEffect(() => {
      if (user.role !== 'Admin') {
        return
      }
      let cancelled = false

      async function load() {
        const { barbers: loadedBarbers, errorMessage } = await fetchBarbers()
        if (cancelled) return
        setBarbersLoading(false)
        if (errorMessage) {
          setBarbersError(errorMessage)
          return
        }
        setBarbers(loadedBarbers)
        setBarbersError('')
        if (loadedBarbers.length === 0) {
          return
        }
        const firstId = loadedBarbers[0].id
        setBarberId(firstId)
        const scheduleResult = await fetchSchedule(null, firstId)
        if (cancelled) return
        setLoading(false)
        if (scheduleResult.errorMessage) {
          setScheduleError(scheduleResult.errorMessage)
        } else {
          setDate(scheduleResult.date)
          setSlots(scheduleResult.slots)
          setScheduleError('')
        }
      }

      load()
      return () => {
        cancelled = true
      }
      // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [user.accessToken, user.role])
    ```
    `GET /api/booking/barbers` (`BookingApi.js#getBarbers`, already built for Story 2.2) is reused unmodified — it has no per-role gate on the Controller (`[Authorize]` class-level only), so no backend change is needed to let an Admin call it. `AccountRepository.FindAllByRole`'s existing ordering (`FirstName`, then `LastName`, then `Id`) is what "the first barber" means — the same order the customer-facing barber-select dropdown already renders in, so "first" is consistent across both surfaces without this story inventing a new ordering rule.
  - [x] Extend `fetchSchedule`/`loadDate` to accept a `barberId`, defaulting to the current selection so every existing call site (nav arrows, the "Try again" retry) keeps working with zero changes to those call sites:
    ```jsx
    async function fetchSchedule(explicitDate, explicitBarberId) {
      const result = await getSchedule(user.accessToken, explicitDate, explicitBarberId)
      // ...unchanged below this line
    }

    async function loadDate(explicitDate, explicitBarberId = barberId) {
      // ...unchanged body, just pass explicitBarberId into fetchSchedule(explicitDate, explicitBarberId)
    }
    ```
    For a Barber caller, `barberId` state is never set (stays `null`), so `explicitBarberId` defaults to `null`/`undefined` there too — `getSchedule` then omits the query param exactly as it does today, so the Barber path's request shape is byte-for-byte unchanged.
  - [x] Add the barber-switch handler, called from the new `SelectDropdown`'s `onChange`. This is the one path required to **not** call `loadDate` from inside an effect (an effect-internal call to an externally-declared, setState-containing function is the exact ESLint gotcha `ScheduleAppointment.jsx`'s Dev Notes already flagged for this codebase) — it's a plain event handler, so calling `loadDate` directly here is fine, same as the nav-arrow `onClick`s already do:
    ```jsx
    function handleBarberChange(newBarberId) {
      const id = Number(newBarberId)
      setBarberId(id)
      setCancelTarget(null)
      setCancelError('')
      loadDate(date, id)
    }
    ```
    Passing the *current* `date` (not `null`) is what satisfies AC #2 — switching barbers must not reset the visible date.
  - [x] Add a small retry handler for the barbers-list failure state (mirrors the existing schedule-fetch "Try again" button, `MySchedule.jsx:170-179`), declared outside any effect so it may call `loadDate` directly once barbers do load. It guards with `isMountedRef` (not a local `cancelled` flag) because — unlike the mount effect, which can only ever have its *own* invocation racing itself under StrictMode's synthetic remount — this is a one-shot event-handler call with no second concurrent invocation to distinguish from; `isMountedRef`'s only job here is the ordinary "component unmounted mid-await" guard, the same role it plays for `handleCancelConfirmed`:
    ```jsx
    async function retryLoadBarbers() {
      setBarbersLoading(true)
      const { barbers: loadedBarbers, errorMessage } = await fetchBarbers()
      if (!isMountedRef.current) return
      setBarbersLoading(false)
      if (errorMessage) {
        setBarbersError(errorMessage)
        return
      }
      setBarbers(loadedBarbers)
      setBarbersError('')
      if (loadedBarbers.length > 0) {
        const firstId = loadedBarbers[0].id
        setBarberId(firstId)
        await loadDate(null, firstId)
      }
    }
    ```
  - [x] Render, in place of the removed placeholder: first, a **defensive fallback for any role that is neither `Barber` nor `Admin`** (the shared render below assumes one of the two, and while `RequireRole roles={['Barber', 'Admin']}` on the route already prevents a `Customer` from reaching this component today, the placeholder being removed defensively covered "any non-Barber role" — don't narrow that coverage to "any non-Admin-non-Barber falls through with no return and `loading` stuck permanently `true`"):
    ```jsx
    if (user.role !== 'Barber' && user.role !== 'Admin') {
      return (
        <div className="my-schedule">
          <h1 className="my-schedule__title">My Schedule</h1>
          <p className="my-schedule__loading">
            Schedule view is not available for this account.
          </p>
        </div>
      )
    }
    ```
    Then, three early-return states specific to Admin (loading barbers, barbers failed to load, zero barbers), each reusing the existing `my-schedule__loading`/`my-schedule__error-state`/`my-schedule__error` classes so no new CSS classes are needed for these three states:
    ```jsx
    if (user.role === 'Admin') {
      if (barbersLoading) {
        return (
          <div className="my-schedule">
            <h1 className="my-schedule__title">My Schedule</h1>
            <p className="my-schedule__loading">Loading…</p>
          </div>
        )
      }
      if (barbersError) {
        return (
          <div className="my-schedule">
            <h1 className="my-schedule__title">My Schedule</h1>
            <div className="my-schedule__error-state">
              <p className="my-schedule__error">{barbersError}</p>
              <Button variant="secondary" onClick={retryLoadBarbers}>
                Try again
              </Button>
            </div>
          </div>
        )
      }
      if (barbers.length === 0) {
        return (
          <div className="my-schedule">
            <h1 className="my-schedule__title">My Schedule</h1>
            <p className="my-schedule__loading">No barbers available.</p>
          </div>
        )
      }
    }
    ```
    Falling through past this block, the rest of the component (loading/scheduleError/date-header-row/slot-list/`ConfirmPopup`) is **shared** between Barber and Admin unchanged — this is what makes AC #1's "identical view a barber sees" true structurally, not just by copying markup.
  - [x] `.date-header-row` is currently a flat 3-column CSS grid (`20px 1fr 20px`, added in Story 2.5's review round specifically to keep the two arrows pinned at fixed edge positions regardless of the date title's text width — see that story's final Change Log entry). A 4th, admin-only child cannot simply be appended into that same flat grid — per `mockups/my-schedule.html`'s admin layout (`.date-header-row { justify-content: space-between }`, with the date-nav trio grouped on the left and `.admin-barber-select` on the right), the two nav arrows + title must be wrapped in their own group div so the row becomes a 2-item flex (`[nav-group] [admin dropdown]`) for Admin, while the Barber-only render keeps today's flat 3-child grid completely untouched. The JSX must render one of two distinct shapes depending on role — do not merge them into one shared markup block with the dropdown conditionally spliced into the flat grid, which would leave `justify-content: space-between` distributing all 4 flat children instead of grouping arrows+title against the dropdown:
    ```jsx
    <div className={`my-schedule${user.role === 'Admin' ? ' my-schedule--admin' : ''}`}>
      <h1 className="my-schedule__title">My Schedule</h1>
      {/* ...loading/scheduleError branches unchanged... */}
      <div className="date-header-row">
        {user.role === 'Admin' ? (
          <>
            <div className="date-nav-group">
              <button /* previous-day arrow, unchanged onClick/aria-label */>…</button>
              <h2 className="date-title">{formatDateHeader(date)}</h2>
              <button /* next-day arrow, unchanged onClick/aria-label */>…</button>
            </div>
            <SelectDropdown
              variant="admin-barber"
              ariaLabel="Select barber"
              value={String(barberId)}
              onChange={handleBarberChange}
              disabled={cancellingId !== null}
              options={barbers.map((barber) => ({
                value: String(barber.id),
                label: `${barber.firstName} ${barber.lastName}`,
              }))}
            />
          </>
        ) : (
          <>
            <button /* previous-day arrow, unchanged */>…</button>
            <h2 className="date-title">{formatDateHeader(date)}</h2>
            <button /* next-day arrow, unchanged */>…</button>
          </>
        )}
      </div>
      {/* ...slot-list/cancelError/ConfirmPopup unchanged... */}
    </div>
    ```
    The `my-schedule--admin` modifier class belongs on this single shared outer `<div>` only — **not** on the three Admin-specific early-return blocks above (loading-barbers/barbers-error/no-barbers), which never render `.date-header-row` at all and have no need of the modifier. No `label`/`emptyMessage` prop is passed to the admin `SelectDropdown` — `barbers.length === 0` is already handled by the early-return above, so `SelectDropdown`'s own empty-message branch is unreachable here by construction, and the mockup (`mockups/my-schedule.html`'s `.admin-barber-select`) shows only the selected barber's name inside the trigger itself, no separate label text above it. `ariaLabel="Select barber"` (Task 5) supplies the accessible name a visible `<label>` would otherwise give it, satisfying UX-DR18's keyboard/screen-reader floor without adding visible label text the mockup doesn't show.
  - [x] Add the matching CSS, scoped to the `.my-schedule--admin` modifier so the Barber-only `.date-header-row` grid rule is completely untouched:
    ```css
    .my-schedule--admin .date-header-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: var(--spacing-5);
    }

    .date-nav-group {
      display: flex;
      align-items: center;
      gap: var(--spacing-5);
    }
    ```
  - [x] Add the admin-variant floating-shadow-at-rest styling (`DESIGN.md`'s `select-dropdown-admin-barber` token, "the one deliberate exception in the elevation model" per `EXPERIENCE.md`'s Elevation & Depth section) by reusing the app's **one existing** floating-shadow value rather than inventing a second one — `.select-dropdown__content`'s shadow (`SelectDropdown.css:31-33`) is already this app's canonical "floating" elevation; there is no dedicated shadow custom property in `tokens.css` to reference instead:
    ```css
    .select-dropdown--admin-barber .select-dropdown__trigger {
      box-shadow:
        0 10px 15px -3px rgba(0, 0, 0, 0.1),
        0 4px 6px -2px rgba(0, 0, 0, 0.05);
      min-width: 160px;
    }
    ```
    (`mockups/my-schedule.html`'s own `.admin-barber-select` illustrates a different, one-off `rgba(23,36,42,0.12)` value — per Information Architecture's own rule that `DESIGN.md`/`EXPERIENCE.md` win over the mocks on conflict, and since neither doc pins an exact rgba, reusing the shadow value the app already has is the more consistent engineering call than adding a second magic shadow definition for one component.)

- [x] **Task 5: `SelectDropdown.jsx`/`.css` — add `variant` and `ariaLabel` props** (AC #2, plus the accessible-name requirement Task 4 depends on) — small, additive change following `Button.jsx`'s existing `VARIANT_CLASS` map pattern exactly, so this codebase has one consistent way components expose style variants:
  ```jsx
  const WRAPPER_VARIANT_CLASS = {
    default: 'select-dropdown',
    'admin-barber': 'select-dropdown select-dropdown--admin-barber',
  }

  export default function SelectDropdown({
    label,
    ariaLabel,
    value,
    onChange,
    options,
    placeholder = 'Select…',
    emptyMessage,
    disabled = false,
    variant = 'default',
  }) {
    const wrapperClass = WRAPPER_VARIANT_CLASS[variant] ?? WRAPPER_VARIANT_CLASS.default
    // ...replace the two literal "select-dropdown" class strings on the wrapper <div>s with wrapperClass
    // ...add aria-label={label ? undefined : ariaLabel} to <Select.Trigger> (only needed when there's no visible <label htmlFor>)
  }
  ```
  Every existing call site (`ScheduleAppointment.jsx`'s barber/time dropdowns) omits both new props, so they keep resolving to `'default'` → the unchanged `select-dropdown` class and no `aria-label` attribute (their existing visible `<label>` already supplies the accessible name) — zero visual/behavioral/accessibility change for them. This also gives tests a reliable, unambiguous query for the label-less admin dropdown: `screen.getByRole('combobox', { name: 'Select barber' })`, since `findByLabelText` (the pattern `ScheduleAppointment.test.jsx` uses for its labeled dropdowns) has nothing to match against a trigger with no associated `<label>`.

- [x] **Task 6: Frontend tests — extend `MySchedule.test.jsx`** (AC #1, #2, #4, plus the no-barbers state)
  - [x] **Remove** the now-obsolete `'renders the Admin placeholder and never calls GET /api/booking/schedule'` test (`MySchedule.test.jsx:501-...`) — this story replaces that placeholder, so the test's premise no longer holds.
  - [x] Extend `mockFetch` (`MySchedule.test.jsx:95-118`) with a `barbersResponse` / `barbersFail` option and a branch matching `/api/booking/barbers`, following the same `href.includes(...)` dispatch shape the function already uses for `/api/booking/schedule` and the cancel endpoint — e.g. when `barbersFail` is set, resolve `{ ok: false, status: 500 }` for that URL the same way the existing `cancel` override models a failure; otherwise resolve `{ ok: true, json: async () => barbersResponse }`.
  - [x] Admin, barbers load successfully: renders "Loading…" then the schedule for the first (alphabetically, per `AccountRepository.FindAllByRole`'s ordering) barber returned, with the barber's name visible in the Select Barber trigger — query it via `screen.getByRole('combobox', { name: 'Select barber' })` (Task 5's `ariaLabel`), **not** `findByLabelText` (that only works for `SelectDropdown`'s labeled call sites, and this one intentionally renders no visible `<label>`).
  - [x] Admin, switching the Select Barber dropdown to a second barber: open it via `userEvent.click` on the `getByRole('combobox', ...)` trigger and select the target option by its rendered name text (mirror `ScheduleAppointment.test.jsx`'s Radix-select interaction mechanics — click-to-open, then click the option — adapted to this label-less query instead of its `findByLabelText`-based one). Assert the `fetch` call to `/api/booking/schedule` includes both the new `barberId` **and** the still-current `date` (proving AC #2 — the date does not reset).
  - [x] Admin, zero barbers: asserts "No barbers available." renders and `fetch` is never called with a URL containing `/api/booking/schedule`.
  - [x] Admin, barbers fetch fails: asserts the error message and a working "Try again" that succeeds on retry (mirror the existing schedule-fetch retry test's shape, `MySchedule.test.jsx` — locate it via the `attemptedDateRef`/"Try again" tests already covering the schedule-load failure path).
  - [x] Admin, cancel flow: reuse the existing Barber cancel-flow test bodies (open confirm popup → confirm → `cancelAppointment` called → re-fetch) against an Admin-rendered page, asserting the re-fetch after a successful cancel includes the currently selected `barberId` (not just `date`) — this is the one behavior genuinely new to the Admin path (AC #4 says "reuses" the flow, but the re-fetch parameters are Admin-specific).
  - [x] Barber-role regression assertion (concrete, not just "re-run existing tests"): render as `SIGNED_IN_BARBER` and assert `fetch` is never called with a URL containing `/api/booking/barbers` — proves the new Admin-only mount effect (Task 4) doesn't fire or leak a request for the unmodified Barber path. Combine with running the full existing Barber-role test suite in this file to confirm the `barberId`-defaulted `loadDate`/`fetchSchedule` signature changes introduced no behavioral regression there.
  - [x] Unexpected-role defensive fallback: render with a role that is neither `'Barber'` nor `'Admin'` (e.g. `'Customer'`) and assert the "Schedule view is not available for this account." message renders with no `fetch` call at all — covers the defensive early-return Task 4 adds ahead of the Barber/Admin branching.

- [x] **Task 7: `BarberSeedService` — add a second, optional dev-only seed for manual verification** (not tied to any AC; a testing convenience Jack asked for so he can see the Select Barber dropdown actually switch between two real barbers locally, before closing out this story and Epic 2) — `BarberSeedService.cs` already seeds exactly one barber ("Barber One") from `BarberSeed:Email`/`BarberSeed:Password` config (env vars `BarberSeed__Email`/`BarberSeed__Password`), no-op if either is unset, mirroring `AdminBootstrapService`'s AD-6 pattern. Generalize it to loop over two independently-configured seed slots instead of hardcoding one:
  ```csharp
  public class BarberSeedService(
      IServiceScopeFactory scopeFactory,
      IConfiguration configuration,
      ILogger<BarberSeedService> logger) : IHostedService
  {
      private const int SqliteConstraintViolation = 19;

      // Second slot is a temporary, manual-testing-only convenience added for Story 2.6
      // (Admin Schedule Oversight) -- verifying the Select Barber dropdown actually
      // switches between two different barbers' schedules requires a second barber
      // account to exist locally. No-op unless BarberSeed2:Email/BarberSeed2:Password
      // are set, same as the original slot -- safe to leave configured indefinitely,
      // and safe to just stop setting the env vars once manual testing is done (no
      // code needs reverting either way).
      private static readonly (string ConfigPrefix, string FirstName, string LastName)[] Seeds =
      [
          ("BarberSeed", "Barber", "One"),
          ("BarberSeed2", "Barber", "Two"),
      ];

      public async Task StartAsync(CancellationToken cancellationToken)
      {
          foreach (var (configPrefix, firstName, lastName) in Seeds)
          {
              await SeedOne(configPrefix, firstName, lastName);
          }
      }

      private async Task SeedOne(string configPrefix, string firstName, string lastName)
      {
          var email = configuration[$"{configPrefix}:Email"];
          var password = configuration[$"{configPrefix}:Password"];
          if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
          {
              return;
          }

          try
          {
              using var scope = scopeFactory.CreateScope();
              var accountRepository = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
              var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Account>>();

              var barber = new Account
              {
                  Email = email,
                  FirstName = firstName,
                  LastName = lastName,
                  Role = Role.Barber,
              };
              barber.PasswordHash = passwordHasher.HashPassword(barber, password);

              await accountRepository.Create(barber);
          }
          catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: SqliteConstraintViolation })
          {
              logger.LogWarning(
                  "{ConfigPrefix}:Email {Email} collides with an existing account — skipping barber seed.", configPrefix, email);
          }
          catch (Exception ex)
          {
              logger.LogWarning(ex, "Barber seed failed for {ConfigPrefix} — skipping.", configPrefix);
          }
      }

      public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
  }
  ```
  Behavior for the existing single-seed setup is unchanged (the `"BarberSeed"` slot is identical to today's hardcoded logic, just looped); the `"BarberSeed2"` slot only does anything once Jack sets `BarberSeed2__Email`/`BarberSeed2__Password` locally. **No test coverage needed for this task** — `BarberSeedService` as a whole is throwaway scaffolding, not a permanent feature: it exists only because Epic 3 (specifically Story 3.4, "Admin Creates a Barber Account") hasn't been built yet, and the entire class — this story's second slot included — is expected to be deleted once that story lands and barber accounts can be created through the real Admin Panel UI instead. Don't add a test file for it now; that effort would be discarded almost immediately.

- [ ] **Task 8: Verify CI green and branch/PR**
  - [x] Branch as `story/2.6-admin-schedule-oversight` from `main`.
  - [ ] Push and confirm both CI jobs (backend .NET, frontend Vite/React) green on GitHub before merging (AD-11). Per standing preference, Jack handles commit/push/PR/CI-confirmation himself — leave this checkbox unchecked for him to update.

## Dev Notes

### Architecture Compliance (must-follow, not optional)

- **AD-1 (layering)** — the admin branch lives entirely inside the existing `GetSchedule` action on `BookingController`; no new controller/service/repository, no new dependency injected (`IAccountRepository` is already a constructor param on this controller).
- **AD-17 (single shared read path)** — this is the story's central constraint. `BookingService.GetDaySchedule`/`FindByBarberAndDate` are not touched at all; Story 2.5 deliberately built `GetDaySchedule(barberId, date, now)` generic in `barberId` specifically so this story would only need a Controller-level change (confirmed via direct read of `BookingService.cs` and Story 2.5's own Dev Notes, which name this story explicitly as the reason for that generality). If any implementation approach requires touching `BookingService`/`AppointmentRepository` to make the admin view work, that is a signal the approach has drifted from this AD — stop and reconsider before adding a second query path.
- **AD-14 (server-side re-validation)** — unaffected; this story adds no new write path, only widens who may call an existing read (`GetSchedule`) and reuses the existing cancel write path (`BookingService.Cancel`, whose `Role.Admin => true` authorization branch has existed unchanged since Story 2.4 specifically for this story to consume — confirmed via direct read, `BookingService.cs:137-143`).
- **FR14 boundary (do not weaken)** — the Barber branch of `GetSchedule` must keep deriving `targetBarberId` from `account.Id` only, never from a caller-supplied `barberId`, even after this story adds that query param for the Admin branch. Task 2's `GetSchedule_barber_supplied_barberId_is_ignored` test is the regression guard for this.

### Current codebase state relevant to this story (verified by direct file read)

- `frontend/src/App.jsx`'s `/my-schedule` route already accepts both `Barber` and `Admin` (`roles={['Barber', 'Admin']}`, added in Story 2.5 specifically in anticipation of this story) — **no routing change is needed**.
- `MySchedule.jsx`'s current Admin behavior is a hardcoded placeholder (`user.role !== 'Barber'` → "Barber schedule selection is not yet available.") — confirmed via direct read, `MySchedule.jsx:145-160`. This story replaces that block; nothing else in the file's Barber-only logic should need to change in shape, only in a few function signatures (Task 4).
- `GET /api/booking/barbers` (`BookingController.cs:15-20`) has no role restriction beyond the class-level `[Authorize]` — confirmed by direct read. It already returns barbers ordered by `FirstName`, `LastName`, `Id` (`AccountRepository.cs:43-49`), the same order the customer-facing barber-select already displays them in.
- `BookingService.Cancel(appointmentId, callerAccountId, callerRole, now)` already has a working `Role.Admin => true` branch (unconditional — an admin may cancel any appointment) — confirmed via direct read, `BookingService.cs:129-167`. This story's cancel flow calls this exact existing endpoint/method (`POST /api/booking/{id}/cancel`) with zero changes, exactly as Story 2.5 did for the Barber path.
- No admin-specific mockup exists as a separate file, but `mockups/my-schedule.html` **is** the admin-view mockup (its own header comment: "My Schedule, admin view") — it shows the `.admin-barber-select` positioned to the right of the date-nav group via `justify-content: space-between`, not inside Story 2.5's later 3-column grid refinement (which postdates this mock and was Barber-only in scope). Task 4's CSS restructuring reconciles the two without regressing Story 2.5's fix.
- `SelectDropdown.jsx` has no `variant`/`className` prop today (confirmed via direct read) — Task 5's addition is the first consumer of that concept for this component, following `Button.jsx`'s existing `VARIANT_CLASS` map as the established in-codebase precedent for how a component exposes style variants.
- Checked `deferred-work.md` and `sprint-status.yaml`'s `action_items` at story start (satisfying the standing action item to do so): all three still-open items (NavBar overflow — already resolved per Story 2.5's own note; Story 2.3's race-test pattern; Story 2.2's calendar/select scoping) are inapplicable here — this story introduces no new race condition (its only write path is the already-race-safe `Cancel`, reused unmodified) and builds no new Calendar component (only a `SelectDropdown` variant).
- `backend/BarbershopApi/Services/BarberSeedService.cs` already exists (confirmed via direct read) — an `IHostedService`, registered in `Program.cs:47`, that seeds exactly one barber account ("Barber One") from `BarberSeed:Email`/`BarberSeed:Password` config, no-op if unset. It predates this story (added ad hoc as local-dev tooling, not from any prior story's ACs — there is no barber-creation API until Epic 3) and mirrors `AdminBootstrapService`'s AD-6 pattern. Practically, this means **Jack's local dev DB normally already has one barber account** once he sets those env vars — the "zero barbers" state this story must still handle gracefully (see the Acceptance Criteria section above) is primarily an automated-test-suite concern (`WebApplicationFactory`/`SqliteApiFactory` never configures `BarberSeed:Email`/`Password`, so CI genuinely starts from zero barbers) rather than something Jack will normally see interactively. Task 7 extends this service with a second, independently-gated seed slot so Jack can also verify the multi-barber switching behavior (AC #2) locally without a barber-creation UI (still Epic 3's job). **The whole class is temporary**: it is expected to be deleted once Story 3.4 ("Admin Creates a Barber Account") ships a real creation path — do not build permanent test coverage or polish for it (Task 7 is explicit about this).

### Testing Requirements

- Backend: xUnit.v3 + `WebApplicationFactory`/`SqliteApiFactory` against a real temp SQLite instance, never mocked (AD-4/NFR4). Reuse `SeedAccount`, `AuthedRequest`, and `RoleGatingTests.RegisterAndLoginAs` already in `BookingControllerTests.cs` — do not add a second Admin-login helper.
- Test fixture names: `"John"`/`"Smith"` for the default/incidental party in any test; never a real person's name.
- Frontend: Vitest + jsdom + React Testing Library + user-event; stub `fetch` via `vi.spyOn(globalThis, 'fetch')`, extending `MySchedule.test.jsx`'s existing `mockFetch` helper rather than building a parallel one.
- No new race condition is introduced by this story (its only write path, cancel, is Story 2.4's already-tested race-safe mechanism) — no new deterministic two-`DbContext` staging test is needed here.

### Previous Story Intelligence (from Story 2.5)

- Story 2.5 explicitly built `GetDaySchedule`'s `date` parameter and `BookingService`'s generic `barberId` threading *for this story to consume unmodified* — confirmed true by this story's design; Task 1 is a Controller-only change.
- Story 2.5 established (and its review rounds hardened) the `fetch*`-returns-a-result-object / `isMountedRef` (post-mount-effect only) / `cancelled`-flag (inside mount effects) / `requestIdRef` (stale-response guard) pattern for `MySchedule.jsx`. This story extends that same file and must keep using the same conventions rather than introducing a different data-loading shape for the new Admin logic — Task 4 is written to match this from the start.
- Story 2.5's Round 2 review found that an effect calling an externally-declared function containing `setState` doesn't trip `react-hooks/exhaustive-deps` the way you'd expect, but a related rule (`react-hooks/set-state-in-effect`, per `ScheduleAppointment.jsx`'s Dev Notes) does flag it — this is why Task 4's mount effect duplicates `loadDate`'s state-setting logic inline instead of calling `loadDate` itself, matching the existing Barber-only effect's own shape exactly.
- Story 2.5's `SqliteApiFactory` connection-pool fix (from Story 2.3) remains in place — no action needed, but it's the first thing to check if any new test in this story behaves flakily under parallel execution.

### Project Structure Notes

- Backend modifications only: `Controllers/BookingController.cs` (`GetSchedule` action extended), `BarbershopApi.Tests/BookingControllerTests.cs` (one test replaced, several added), `Services/BarberSeedService.cs` (generalized to a second, optional dev-only seed slot — Task 7, not tied to any AC). No new files, no migration, no `BookingService`/`IBookingService`/repository changes.
- Frontend modifications: `frontend/src/pages/MySchedule.jsx`, `frontend/src/pages/MySchedule.css`, `frontend/src/pages/MySchedule.test.jsx`, `frontend/src/api/BookingApi.js` (`getSchedule` extended), `frontend/src/components/SelectDropdown.jsx`, `frontend/src/components/SelectDropdown.css`. No new frontend files, no route changes (`App.jsx` untouched — already correct since Story 2.5).

### References

- [Source: _bmad-output/planning-artifacts/epics.md §Story 2.6, §Epic 2, §UX-DR8] — story statement, all 4 ACs verbatim, FR15/FR27/FR30 mapping, explicit dependency on Story 2.5's `GetDaySchedule`/`FindByBarberAndDate` reuse and Story 2.4's cancel mechanism.
- [Source: _bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/ARCHITECTURE-SPINE.md AD-1, AD-14, AD-17] — layering, server-side re-validation (unaffected), single shared read path (this story's central constraint).
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-bmad-learning-project-2026-07-23/DESIGN.md §Components (select-dropdown-admin-barber), §Elevation & Depth] — the floating-shadow-at-rest exception and its rationale.
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-bmad-learning-project-2026-07-23/EXPERIENCE.md §Information Architecture, §Component Patterns ("Barber-select dropdown, admin variant"), §Responsive Breakpoints] — "defaults to the first barber, auto-selected — never an empty state," "switching barbers re-renders the same visible date... the date does not reset."
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-bmad-learning-project-2026-07-23/mockups/my-schedule.html] — the admin-view mockup (its own header comment identifies it as such); exact `.admin-barber-select` positioning/shadow this story's CSS reconciles against Story 2.5's later grid refinement.
- [Source: _bmad-output/implementation-artifacts/2-5-barbers-own-schedule-view.md] — `GetDaySchedule`'s admin-ready generality, the `.date-header-row` 3-column grid fix (final Change Log entry) this story must not regress, the `fetch*`/`isMountedRef`/`cancelled`/`requestIdRef` conventions this story extends.
- [Source: backend/BarbershopApi/Controllers/BookingController.cs, Services/BookingService.cs, Services/IBookingService.cs, Repositories/AccountRepository.cs] — exact current signatures/behavior this story extends; confirmed `GetDaySchedule`/`FindByBarberAndDate`/`Cancel`'s `Role.Admin` branch and `FindAllByRole`'s ordering are reusable as-is.
- [Source: backend/BarbershopApi/Services/BarberSeedService.cs, Program.cs:47, Services/AdminBootstrapService.cs] — the existing single-barber dev seed and its AD-6-mirroring pattern, generalized in Task 7 to a second optional slot.
- [Source: frontend/src/pages/MySchedule.jsx, MySchedule.css, App.jsx, api/BookingApi.js, components/SelectDropdown.jsx, SelectDropdown.css, components/Button.jsx, pages/ScheduleAppointment.jsx] — confirmed the current Admin placeholder, the already-Admin-ready route, the barber-select data shape (`getBarbers`) already used by the booking flow, and `Button.jsx`'s variant-map pattern this story's `SelectDropdown` change follows.
- [Source: project-context.md §Framework-Specific Rules, §Testing Rules] — fixture-name convention; no client-side timezone math (unaffected — this story adds no new date computation, only a `barberId` selector).

## Dev Agent Record

### Agent Model Used

claude-sonnet-5 (Amelia)

### Debug Log References

- Task 6 first run: 2 test failures — the `barbersFail` mock branch omitted a `json` method (`getBarbers` unconditionally calls `response.json()` before checking `response.ok`, matching the rest of `BookingApi.js`'s existing pattern), and the defensive-fallback test's `not.toHaveBeenCalled()` assertion didn't account for `AuthProvider`'s own mount-time `/api/auth/refresh` call. Both fixed in the test file itself (added `json: async () => null` to the failure mock; narrowed the assertion to "no `/api/booking/` call"); no production code changes needed.
- `npx prettier --check .` flagged `SelectDropdown.jsx`, `MySchedule.jsx`, and `MySchedule.test.jsx` after implementation (inline code samples in the story file aren't pre-formatted); ran `npx prettier --write` on the three files and reconfirmed clean.

### Completion Notes List

- All 4 ACs implemented and covered by tests, plus the required zero-barbers state: AC1 (identical Barber view + Select Barber dropdown defaulting to the first barber, never empty) — Admin mount effect in `MySchedule.jsx` + shared render block; AC2 (switching barbers re-renders the same visible date) — `handleBarberChange` passes the current `date` into `loadDate`, covered by a test asserting both `barberId` and `date` on the re-fetch; AC3 (reads through the exact same shared `BookingService.GetDaySchedule`/`FindByBarberAndDate`) — satisfied by construction, Task 1 only branches at the Controller level, no service/repository change; AC4 (Cancel reuses Story 2.4's confirm-popup-then-soft-cancel flow) — zero changes to `ConfirmPopup`/`BookingService.Cancel`, only the re-fetch now carries `barberId`; zero-barbers state — dedicated early-return rendering "No barbers available." with no schedule fetch attempted.
- Implementation matched the story's provided code almost verbatim — no deviations from the specified approach (Controller-only backend change, `URLSearchParams`-based query building, the two-mount-effect/`cancelled`-flag pattern, the `date-nav-group` flex restructuring). No new files were needed; no planning-doc drift occurred (unlike Story 2.5, no scope changes were requested mid-story).
- Task 7 (`BarberSeedService` second seed slot) has no test coverage by design, per the story's explicit instruction — the whole class is throwaway scaffolding slated for deletion once Story 3.4 ships a real barber-creation path.
- Task 8's branch subtask is satisfied (already on `story/2.6-admin-schedule-oversight`); its push/PR/CI-confirmation subtask is intentionally left unchecked per the story's own instruction and Jack's standing preference to handle that step himself.
- Final verification: backend `dotnet test` — 176/176 passed (full suite, no regressions). Frontend `npx vitest run` — 146/146 passed (19 files, full suite). `npx eslint .` — clean. `npx prettier --check .` — clean.
- **Post-review layout fix (requested by Jack)**: `.date-nav-group` (the Admin-only wrapper around the two nav arrows + date title) was originally plain `flex` with a `gap`, so the next-day arrow's position shifted with the date title's text width — silently reintroducing the exact jitter bug Story 2.5's review fixed for the Barber view (`.date-header-row`'s `20px 1fr 20px` grid, which pins each arrow to a fixed-width column regardless of title width). Fixed by giving `.date-nav-group` the same `20px 1fr 20px` grid technique, with `flex: 1; min-width: 0` so it still fills the row's available space to the left of the Select Barber dropdown. Net effect: previous-day arrow pinned to the row's left edge (matching the Barber view), next-day arrow now pinned immediately left of the dropdown instead of the row's far-right edge (which the dropdown now occupies), and the date title stays centered between the two arrows via the grid's `1fr` middle column — same principle as 2.5, just anchored against the dropdown instead of the row's outer edge. CSS-only change, no JSX/markup change. Full regression suite still green (176 backend, 146 frontend); ESLint/Prettier clean.

## File List

**Backend — modified:**
- backend/BarbershopApi/Controllers/BookingController.cs
- backend/BarbershopApi/Services/BarberSeedService.cs
- backend/BarbershopApi.Tests/BookingControllerTests.cs

**Frontend — modified:**
- frontend/src/api/BookingApi.js
- frontend/src/pages/MySchedule.jsx
- frontend/src/pages/MySchedule.css
- frontend/src/pages/MySchedule.test.jsx
- frontend/src/components/SelectDropdown.jsx
- frontend/src/components/SelectDropdown.css

## Change Log

- 2026-08-10: Implemented Story 2.6 (Tasks 1-7) — `BookingController.GetSchedule` admin `barberId` branch (Controller-only, no service/repository changes per AD-17), `BookingApi.js#getSchedule` extended with an optional `barberId` via `URLSearchParams`, `MySchedule.jsx`'s Admin path (barber-loading mount effect, Select Barber dropdown, barber-switch/retry handlers, loading/error/zero-barbers states, `.date-header-row` flex restructuring for Admin), `SelectDropdown`'s new `variant`/`ariaLabel` props, and `BarberSeedService`'s second dev-only seed slot. Backend and frontend test coverage added for all 4 ACs plus the zero-barbers state. Full regression suite green (176 backend, 146 frontend); ESLint/Prettier clean.
- 2026-08-10: Post-review layout fix requested by Jack — the Admin-only nav-arrows+title wrapper (`.date-nav-group`) used plain flex, so the next-day arrow's position shifted with the date title's text width, reintroducing the exact jitter bug Story 2.5 fixed for the Barber view. Changed `.date-nav-group` to the same `20px 1fr 20px` grid technique (plus `flex: 1; min-width: 0` so it still fills the row alongside the Select Barber dropdown) — previous-day arrow now pinned to the row's left edge, next-day arrow pinned immediately left of the dropdown, date title centered between the two. CSS-only; no JSX change. Full regression suite still green (176 backend, 146 frontend); ESLint/Prettier clean.
