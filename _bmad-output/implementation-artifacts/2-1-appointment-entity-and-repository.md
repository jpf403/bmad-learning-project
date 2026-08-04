---
baseline_commit: 47c5a27ae5302fbaf42dab62591e2d25239ec33c
---

# Story 2.1: Appointment Entity & Repository

Status: ready-for-dev

## Story

As a developer,
I want the `Appointment` entity, its migration (including the double-booking guard indexes), and every repository/service method Epic 2's stories will need,
so that booking, cancellation, and all three schedule views (customer, barber, admin) can be built as pure business logic and UI on top of a working, tested data layer — no further schema changes for anything Epic 2 needs.

## Acceptance Criteria

1. **Given** no `Appointment` entity exists yet, **when** this story is implemented, **then** the entity/migration is created with `int` auto-increment PK (AD-7), `CustomerId` FK, `BarberId` FK, `Date` (string, `yyyy-MM-dd`), `StartTime` (string, `HH:mm`), `CancelledAt` (nullable `DateTime`, soft-cancel per AD-8) — plus the two DB-level partial unique indexes as the double-booking backstop: `UNIQUE(BarberId, Date, StartTime)` and `UNIQUE(CustomerId, Date, StartTime)`, both `WHERE CancelledAt IS NULL` (AD-9).
2. **Given** the entity, **when** the `AppointmentRepository`/`BookingService` is built, **then** it exposes every method Epic 2 needs: `Create` (book, app-level check-then-insert guarded, backed by the two DB-level partial unique indexes per AD-9), `FindByBarberAndDate` (barber/admin schedule views — must accept an arbitrary `barberId`, not "current user", so Story 2.6's admin Select-Barber view can reuse it per AD-17), `FindUpcomingByCustomer` (My Appointments), and `Cancel` (soft-cancel via `CancelledAt`, idempotent — a second cancel on an already-cancelled row must error, not silently no-op) — all through the single shared read/write path AD-17 requires, never duplicated per role.
3. **Given** "Finished" status, **when** any read method returns appointments, **then** it's computed at read time from `Date`/`StartTime` vs. current EST "now" (AD-12) — never persisted as a column (AD-8).
4. **Given** the repository and service, **when** tested, **then** every method — including the double-booking guard (both the app-level check and the DB-level backstop) and soft-cancel idempotency — is covered by xUnit.v3 + `WebApplicationFactory` against a real (temp) SQLite instance, never mocked (NFR4, AD-4).
5. **Given** this story is complete, **when** Stories 2.2–2.6 are built, **then** they add only business logic (Controller) and UI on top of `BookingService` — no further schema changes, and no Controller/UI code talks to `AppointmentRepository` directly (AD-1 layering).

## Tasks / Subtasks

- [ ] **Task 1: Define the `Appointment` entity** (AC: #1)
  - [ ] Create `backend/BarbershopApi/Entities/Appointment.cs`: `Id` (`int`), `CustomerId` (`int`), `BarberId` (`int`), `Date` (`string`), `StartTime` (`string`), `CancelledAt` (`DateTime?`).
  - [ ] **Do not add a `RowVersion`/concurrency-token column.** AD-16's optimistic-concurrency mechanism is Account-only — Appointment's race protection is entirely the transaction-style check-then-insert + two partial unique indexes (AD-9), a different mechanism for a different race. Copying Story 1.2's `RowVersion`/trigger pattern here is a mistake this story must avoid.
  - [ ] No `Status`/`Finished` column — see Task 4's Finished-computation notes; a stored status field is explicitly the anti-pattern AD-8 forbids.
- [ ] **Task 2: Configure EF Core mapping** (AC: #1)
  - [ ] In `BarbershopDbContext`, add `public DbSet<Appointment> Appointments => Set<Appointment>();` and extend `OnModelCreating`.
  - [ ] Configure the two partial unique indexes exactly as Story 1.2 configured Account's `Email` index (same proven `HasFilter` → SQLite `WHERE` clause translation, no raw SQL needed):
    - `.HasIndex(a => new { a.BarberId, a.Date, a.StartTime }).IsUnique().HasFilter("CancelledAt IS NULL")`
    - `.HasIndex(a => new { a.CustomerId, a.Date, a.StartTime }).IsUnique().HasFilter("CancelledAt IS NULL")`
  - [ ] Configure `CustomerId`/`BarberId` as FKs to `Account` (`HasOne(...).WithMany().HasForeignKey(...)`). Set `.OnDelete(DeleteBehavior.Restrict)` on both — defensive only: Account never hard-deletes (AD-15), so no cascade path should ever fire, but EF Core's default `Cascade` behavior on a required relationship is the wrong contract to leave in place given the whole schema's soft-delete design.
- [ ] **Task 3: Migration** (AC: #1)
  - [ ] Run `dotnet ef migrations add AddAppointmentEntity` from `backend/BarbershopApi/`.
  - [ ] Unlike Story 1.2's `AddAccountEntity` migration, **no hand-written SQL trigger is needed** — there's no `RowVersion` column to auto-increment. The generated migration should need no manual editing beyond what EF produces for the two `CreateIndex` calls with filters; verify the generated `Up()` actually contains `WHERE "CancelledAt" IS NULL` on both indexes before moving on.
  - [ ] Verify `dotnet ef database update` runs clean against both a fresh temp DB and the existing local dev DB (`backend/BarbershopApi/App_Data/barbershop.db`, if present).
- [ ] **Task 4: Build `AppointmentRepository` (thin persistence layer)** (AC: #2)
  - [ ] Create `backend/BarbershopApi/Repositories/IAppointmentRepository.cs` / `AppointmentRepository.cs`, constructor-injecting `BarbershopDbContext` — the only layer allowed to touch the DbContext directly (AD-1).
  - [ ] `Task<Appointment> Create(Appointment appointment)` — plain add + `SaveChangesAsync()`, returns the entity with `Id` populated. **No app-level conflict check here** — that check belongs in `BookingService` (see Task 5), mirroring exactly where `AuthService.Register` puts its duplicate-email check (`AccountRepository.Create` is equally thin, with no pre-check of its own) [Source: backend/BarbershopApi/Services/AuthService.cs].
  - [ ] `Task<List<Appointment>> FindByBarberAndDate(int barberId, string date)` — raw query filtered to `BarberId == barberId && Date == date && CancelledAt == null`. A cancelled appointment must not appear here — its slot is meant to read as open again immediately (mirrors AD-15's "soft-deleted row behaves as not-found" discipline, applied to soft-cancel instead).
  - [ ] `Task<List<Appointment>> FindUpcomingByCustomer(int customerId, DateTime nowEst)` — filtered to `CustomerId == customerId && CancelledAt == null`, plus a "not yet occurred" comparison against the `nowEst` parameter. Take `nowEst` as a parameter rather than computing "now" inside the repository — EST/DST-awareness (AD-12) is `BookingService`'s job, the repository just compares whatever instant it's handed. Because `Date` (`yyyy-MM-dd`) and `StartTime` (`HH:mm`) are zero-padded, ISO-ordered strings, a direct string comparison against `nowEst`'s own `yyyy-MM-dd`/`HH:mm`-formatted parts is safe and avoids parsing in the query.
  - [ ] `Task Cancel(int appointmentId)` — loads the appointment by `Id`; if `CancelledAt` is already set, throw `AppointmentAlreadyCancelledException` (idempotency guard, AC #2/#5's "second cancel must error, not no-op"); otherwise set `CancelledAt = DateTime.UtcNow` and `SaveChangesAsync()`. `CancelledAt` is only an audit timestamp (not used in any Date/StartTime business comparison), so UTC is fine here — this is not an AD-12 violation.
  - [ ] Register `IAppointmentRepository` → `AppointmentRepository` as `Scoped` in `Program.cs`.
- [ ] **Task 5: Build `BookingService`** (AC: #2, #3, #5)
  - [ ] **This is a deliberate departure from Story 1.2's precedent** (which built only a Repository, no Service, since Epic 1 didn't need one yet). AD-17 explicitly requires "one shared `BookingService` method" for every appointment read, and this story's own AC #2 names `BookingService` directly — so unlike 1.2, this story builds the Service now. `Controllers/` still stays untouched (`.gitkeep`-only) — Story 2.2 is the first to add an `AppointmentController`/`BookingController` that calls into this `BookingService`, never `AppointmentRepository` directly (AD-1).
  - [ ] Create `backend/BarbershopApi/Services/IBookingService.cs` / `BookingService.cs`, constructor-injecting `IAppointmentRepository` and `IAccountRepository` (needed to resolve customer/barber display names — see the read-model note below).
  - [ ] `Task<Appointment> Create(int customerId, int barberId, string date, string startTime)`:
    1. App-level check: query for any existing non-cancelled appointment matching `BarberId+Date+StartTime` OR `CustomerId+Date+StartTime` (add whatever repository query shape is convenient for this — the AC's four named methods are the public contract Epic 2's later stories need; an internal helper query is an implementation detail, same precedent as `IAccountRepository` growing `AdminExists()` in Story 1.5 when needed). If found, throw `BookingConflictException` immediately.
    2. Otherwise call `appointmentRepository.Create(...)`, wrapped in `try/catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 })` → throw `BookingConflictException`. This is the exact catch shape `AuthService.Register` already uses for its own duplicate-email DB backstop [Source: backend/BarbershopApi/Services/AuthService.cs:43] — reuse it, don't reinvent it.
    3. **On AD-9's "inside a transaction" phrasing:** the established codebase precedent (`AuthService.Register`) does not use an explicit `BeginTransactionAsync` — a single `SaveChangesAsync` insert is already atomic, and the two-step check-then-insert's race window is closed by the partial-unique-index backstop, not by wrapping both steps in one SQL transaction. Follow this same precedent; don't introduce `BeginTransactionAsync` where the codebase's own established pattern doesn't use it.
  - [ ] `Task<List<AppointmentView>> FindByBarberAndDate(int barberId, string date)` — delegates to the repository, maps each `Appointment` to the shared `AppointmentView` read-model (see below), computing `Finished` per row.
  - [ ] `Task<List<AppointmentView>> FindUpcomingByCustomer(int customerId)` — computes `nowEst` once via `TimeZoneInfo.FindSystemTimeZoneById("America/New_York")` (DST-aware, AD-12), passes it to the repository, maps results to the same `AppointmentView` shape (`Finished` will always be `false` for this list by construction, but reusing one DTO type across all three views is what AD-17's "never duplicated per role" actually means in practice).
  - [ ] `Task Cancel(int appointmentId)` — delegates to the repository; lets `AppointmentAlreadyCancelledException` propagate (Controller-level mapping to a `Problem()` response is Story 2.4's job, not this story's).
  - [ ] Create `backend/BarbershopApi/Dtos/AppointmentView.cs` — the one shared read-model AD-17 requires: `Id`, `CustomerId`, `CustomerName`, `BarberId`, `BarberName`, `Date`, `StartTime`, `Finished` (bool), `CancelledAt`. Resolve `CustomerName`/`BarberName` via `IAccountRepository.FindById` per appointment. This is an N+1 query pattern — an accepted tradeoff, not a gap: project-context.md is explicit that this is a single local SQLite instance with no scale requirement (NFR7), and batching would be premature optimization here.
  - [ ] Create two new marker exceptions in `Services/`, matching the exact shape of `DuplicateEmailException`/`InvalidCredentialsException` (`public class BookingConflictException : Exception;` and `public class AppointmentAlreadyCancelledException : Exception;`) — ready for Story 2.2/2.4's Controllers to catch and map via `Problem()` (per project-context.md: "`Problem()` helper for custom errors (booking conflicts, stale cancellations)").
  - [ ] Register `IBookingService` → `BookingService` as `Scoped` in `Program.cs`.
- [ ] **Task 6: Repository & Service tests** (AC: #4)
  - [ ] Reuse `backend/BarbershopApi.Tests/SqliteApiFactory.cs` verbatim (built in Story 1.2 for exactly this reuse) — do not re-derive the test-fixture pattern.
  - [ ] `AppointmentRepositoryTests.cs`:
    - `Create_persists_appointment_with_expected_defaults`.
    - `Create_second_appointment_for_same_barber_slot_throws` and `Create_second_appointment_for_same_customer_slot_across_different_barbers_throws` — call `AppointmentRepository.Create` directly **twice, via two independent `DbContext`/repository instances**, bypassing `BookingService`'s app-level check entirely. This proves the DB-level partial-unique-index backstop itself throws, independent of the app-level guard — deterministically, with no real concurrency/threading involved (same two-`DbContext` staging pattern Story 1.2 used for `RowVersion` conflicts, per the Epic 1 retro's action item to reuse it for Story 2's booking-race tests). This also closes a coverage gap flagged in `deferred-work.md` for `AuthService`'s own untested DB-constraint backstop — worth doing right here since it's Epic 2's central value proposition, not a side concern.
    - `FindByBarberAndDate_excludes_cancelled_appointments`.
    - `FindUpcomingByCustomer_excludes_past_and_cancelled_appointments` — construct with an explicit `nowEst` parameter so the test doesn't depend on wall-clock time.
    - `Cancel_sets_CancelledAt`.
    - `Cancel_on_already_cancelled_appointment_throws_AppointmentAlreadyCancelledException`.
  - [ ] `BookingServiceTests.cs`:
    - `Create_throws_BookingConflictException_when_barber_slot_already_booked` and `Create_throws_BookingConflictException_when_customer_already_booked_a_different_barber_at_same_time` — exercise the app-level pre-check path (two sequential calls through `BookingService.Create`, no need for multiple `DbContext`s here since the service's own pre-check should catch it before ever reaching the DB).
    - `FindByBarberAndDate_computes_Finished_correctly_at_the_EST_boundary` — cover an appointment just before, at, and just after the computed "now" (this boundary is a genuine open question the architecture docs don't pin down — Finished triggers the instant `Date`+`StartTime` <= current EST "now", since there's no `EndTime`/duration field on the entity; document this interpretation in the story's own Completion Notes since it's a design decision this story is making, not one handed down by the architecture).
    - `FindByBarberAndDate_resolves_customer_and_barber_names`.
  - [ ] Backend suite must stay green (`dotnet test`).
- [ ] **Task 7: Fix NavBar overflow bug** (Epic 1 retro action item #3 — see Dev Notes)
  - [ ] **Not applicable to this story.** This story makes no frontend changes at all (it's backend entity/repository/service only) — there is no page for the NavBar to overflow on. The retro committed to fixing this "as part of Epic 2's page work," which starts with Story 2.2 (the first Epic 2 story that touches any UI). Re-flag explicitly in this story's own Completion Notes as "checked, not applicable — deferred to 2.2" so the retro's own action item (deferred work must be re-checked at story kickoff, not silently skipped) is honestly satisfied rather than silently dropped again.
- [ ] **Task 8: Verify CI green and branch/PR**
  - [ ] Branch as `story/2.1-appointment-entity-and-repository` from `main`.
  - [ ] Push and confirm both CI jobs pass before merging (AD-11). Frontend CI job is unaffected by this story's scope (no frontend changes) but must still be green.

## Dev Notes

### Architecture Compliance (must-follow, not optional)

- **AD-1 (layering)** — `Controllers → Services → Repositories`, one-way. This story populates `Entities/`, `Repositories/`, and — unlike Story 1.2 — `Services/` too (justified below). `Controllers/` stays `.gitkeep`-only; building ahead into Controllers now would mean guessing at a shape Story 2.2 hasn't earned yet.
- **AD-7 (int PKs)** — `Appointment.Id` is a plain auto-increment `int`; EF's default convention handles it, no explicit config needed.
- **AD-8 (status computed, not stored)** — no `Status`/`Finished` column, ever. `CancelledAt` is the only real state change (soft-cancel, nullable `DateTime`), and "Finished" is a value computed at read time by `BookingService`, never by the repository and never persisted.
- **AD-9 (double-booking guard, defense-in-depth)** — app-level check-then-insert (in `BookingService.Create`) *and* the two DB-level partial unique indexes (in the migration) are both required; neither alone is this story's contract, both are.
- **AD-12 (fixed EST semantics)** — `America/New_York`, DST-aware, computed server-side. `BookingService` is where "now" gets computed (via `TimeZoneInfo.FindSystemTimeZoneById`); `AppointmentRepository` only ever receives an already-resolved `DateTime` — it does not know what timezone anything is in.
- **AD-17 (single shared read path)** — this is *why* this story builds `BookingService` at all, unlike Story 1.2's repository-only scope. Every appointment read Epic 2 needs (barber/admin schedule, My Appointments) must go through this one service and its one `AppointmentView` DTO shape — never a separately-implemented per-role query, never a re-derived Finished computation.
- **AD-4/AD-10/NFR4 (testing)** — real temp SQLite via `WebApplicationFactory`/`SqliteApiFactory`, never mocked, never the EF in-memory provider (it wouldn't enforce the partial unique indexes this story's whole guard depends on).

### Repository vs. Service split — why this story differs from Story 1.2's shape

Story 1.2 built only `AccountRepository`, no `AccountService` — Epic 1 didn't need one until Stories 1.4/1.5 arrived with actual business logic. Story 2.1 is different: AD-17 requires a single shared read path *now*, before any Controller exists to consume it, because Stories 2.4/2.5/2.6 all depend on that shared path being the same method rather than each reinventing it. So this story builds a thin `AppointmentRepository` (pure persistence, mirrors `AccountRepository` exactly) plus a `BookingService` (business rules: conflict guard, Finished computation, name resolution) — following the same repository/service relationship `AuthService`/`AccountRepository` already established for Auth, just introduced one story earlier in this domain's lifecycle than Account's was.

### Finished computation — the boundary decision this story must make

Neither `ARCHITECTURE-SPINE.md` nor `SOLUTION-DESIGN.md` specify the exact Finished boundary condition, and the entity has no `EndTime`/duration field. This story's own interpretation (document it in Completion Notes as a decision, not an oversight): an appointment is Finished the instant `Date`+`StartTime` (interpreted in `America/New_York`) is at or before the current EST "now" — i.e., a 9:00 AM appointment is Finished starting at 9:00:00 AM sharp, not after some elapsed duration. If this reads wrong once Story 2.5's schedule view is actually built and visually reviewed, revisit here — this is a one-line comparison, cheap to change.

### Testing Requirements

- xUnit.v3 + `WebApplicationFactory` against `SqliteApiFactory`'s real temp SQLite instance (built in Story 1.2, reused verbatim) — no mocked `DbContext`, no EF in-memory provider.
- The double-booking backstop test (Task 6) must use two independent `DbContext`/repository instances calling `AppointmentRepository.Create` directly, never a real concurrent-request race — this was learned the hard way fixing flaky concurrency tests in Stories 1.2 and 1.7, and the Epic 1 retro explicitly calls out reusing this exact pattern for Epic 2's booking-race tests (its own action item was scoped to Story 2.3, but the same lesson applies here since this story's own AC already requires race coverage).
- Soft-cancel idempotency (a second `Cancel` on an already-cancelled row throwing rather than silently no-opping) must be explicitly tested — this is the same shape as FR30's later requirement in Story 2.4, but the underlying repository-level guarantee is this story's to build and prove.

### Project Structure Notes

- `Entities/` and `Repositories/` currently contain only the Account/Role trio — this story is a clean, greenfield addition of `Appointment.cs`, `IAppointmentRepository.cs`/`AppointmentRepository.cs`.
- `Services/` currently contains `AuthService`/`IAuthService`, `AccountService`, `SessionLivenessMiddleware`, and the exception marker classes (`DuplicateEmailException`, `InvalidCredentialsException`, `InvalidSessionException`, `AccountConflictException`, `InvalidCurrentPasswordException`, `SameAsCurrentPasswordException`) — this story adds `BookingService.cs`/`IBookingService.cs` plus `BookingConflictException.cs`/`AppointmentAlreadyCancelledException.cs` alongside them, following the exact same one-class-per-file, plain-marker-exception convention.
- `Dtos/` gets a new `AppointmentView.cs` — the shared read-model DTO.
- No frontend changes in this story — backend-only (data + business-logic layer). Task 7 explicitly confirms the NavBar-overflow retro action item is checked and correctly deferred (to Story 2.2), not silently skipped.
- `Program.cs` gets exactly two new `AddScoped` lines (`IAppointmentRepository`, `IBookingService`) — don't touch CORS, connection-string resolution, or `Database.Migrate()`.

### Previous Story Intelligence (from Story 1.2)

- `SqliteApiFactory` (subclasses `WebApplicationFactory<Program>`, real temp SQLite file per test class, `SqliteConnection.ClearAllPools()` + best-effort sidecar-file cleanup in `Dispose()`) was built in Story 1.2 explicitly "as a reusable fixture for Stories 2.1/3.1" — reuse it verbatim.
- Story 1.2's single biggest gotcha (SQLite has no native auto-incrementing rowversion, needs a hand-written trigger) **does not apply here** — Appointment has no concurrency token. Don't port that trigger pattern over by habit.
- Repository test convention: reload state via a **second**, independent `DbContext`/repository instance rather than reusing the same tracked context — reusing the same instance returns the tracked in-memory object via EF's identity map and can mask a broken constraint or write.
- `IAccountRepository` picked up a new method (`AdminExists()`) in a later story purely because a later story needed it — confirms repository interfaces in this codebase are expected to grow incrementally, not be over-built with speculative methods up front. The same applies to any internal conflict-check query `BookingService.Create` needs beyond the four AC-named methods.

### Git Intelligence Summary

Recent commits (`52e6866` → `9f5d702` → `f0f682b` → `f8bb1ed` → `47c5a27`) show the established rhythm: create story on `main`, implement on `story/{epic}.{story}-{slug}`, PR with a summary of domain additions/fixes/test counts, merge once both CI jobs are green. `47c5a27` (Epic 1 Retrospective) is the current tip — no Epic 2 code exists yet; this story is a clean, greenfield addition to `Entities/`, `Repositories/`, `Services/`, and `Dtos/`.

### Deferred Work / Retro Action Items Checked

- Retro action item #1 (re-check `deferred-work.md` at every story kickoff): checked in full — every open item in `deferred-work.md` concerns Auth/Account (rate limiting, password-change messaging, `AccountApi.js` response shapes, `AuthController`/`AccountController` logging) — none apply to a backend-only Appointment entity/repository story. No re-defer needed; nothing here to fix or carry forward.
- Retro action item #2 (Story 2.3's race tests must use the two-`DbContext` pattern): not this story's action item by name, but this story's own AC #4 already requires race coverage, so the same pattern is applied here proactively (see Testing Requirements above) rather than waiting for 2.3.
- Retro action item #3 (NavBar overflow, fix during Epic 2's page work): not applicable to this story — see Task 7. Still open, correctly deferred to Story 2.2 (the first Epic 2 story with a page).
- Retro action item #4 (Story 2.2 must scope the Calendar/Select components): not this story's concern — noted for awareness only, since Story 2.1 is backend-only.

### References

- [Source: _bmad-output/planning-artifacts/epics.md §Story 2.1, §Epic 2] — story statement, AC, cross-story dependency map (2.2–2.6's reliance on this story's repository contract)
- [Source: _bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/ARCHITECTURE-SPINE.md#AD-1, #AD-7, #AD-8, #AD-9, #AD-12, #AD-17, #Structural-Seed] — layering, PK strategy, computed-status rule, double-booking guard, EST semantics, shared read path
- [Source: _bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/SOLUTION-DESIGN.md §4] — Appointment schema rationale, partial-unique-index SQL literals, computed-status rationale
- [Source: _bmad-output/implementation-artifacts/1-2-account-entity-and-repository.md] — repository/entity task shape, `SqliteApiFactory` fixture, partial-unique-index EF config pattern, two-`DbContext` concurrency-test pattern
- [Source: backend/BarbershopApi/Services/AuthService.cs] — established check-then-insert + `DbUpdateException`/`SqliteException{SqliteErrorCode:19}` backstop pattern, reused here for `BookingService.Create`
- [Source: backend/BarbershopApi/Services/DuplicateEmailException.cs, backend/BarbershopApi/Controllers/AuthController.cs] — plain-marker-exception + `Problem()`-mapping convention
- [Source: _bmad-output/implementation-artifacts/epic-1-retro-2026-08-04.md] — action items checked above (deferred-work re-check, two-DbContext race-test pattern, NavBar deferral)
- [Source: _bmad-output/implementation-artifacts/deferred-work.md] — confirmed no open items apply to this story
- [Source: project-context.md §Technology Stack & Versions; §Language-Specific Rules (C#); §Testing Rules; §Critical Don't-Miss Rules]

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
