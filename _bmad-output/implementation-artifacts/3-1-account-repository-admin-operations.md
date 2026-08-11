---
baseline_commit: 442df9f3e3e7719e36ca0a636fd900116136d50c
---

# Story 3.1: Account Repository — Admin Operations

Status: ready-for-dev

## Story

As a developer,
I want the `Account` repository extended with the admin-only operations Epic 3's stories will need,
so that account search, admin-driven edit/create/delete, and the appointment-cascade on barber removal can all be built as pure business logic on top of a tested data layer.

## Acceptance Criteria

1. **Given** the `AccountRepository` built in Story 1.2, **when** extended for Epic 3, **then** the Account data/business layer (Repository + `AccountService`, mirroring how Story 2.1's AC combined `AppointmentRepository`/`BookingService` under one contract) exposes: `Search` (partial, case-insensitive match on first name/last name/email; excludes the single admin account and any soft-deleted row — FR17), `AdminUpdate`/`AdminUpdateAccount` (email/first/last/role/password — a password change bumps `SessionVersion`, a permission-only change does not — FR35), `AdminCreate`/`AdminCreateBarber` (always creates `Role.Barber`, never another admin — FR19), and `SoftDelete`/`AdminSoftDeleteAccount` (sets `DeletedAt`, never a hard row delete — AD-15, FR40).
2. **Given** FR34's invariant (exactly one admin account; it can never be promoted-to, demoted-from, or deleted), **when** any admin operation targets the admin account **or** would assign `Role.Admin` to any account, **then** both directions are rejected at the repository level — independent of whatever the Service layer already checked, as a defense-in-depth backstop against a Service-layer bug (FR18).
3. **Given** a demote-to-customer (`Role.Barber` → `Role.Customer` via `AdminUpdate`) or a soft-delete of a barber account, **when** executed, **then** it also cancels that barber's future (not-yet-occurred) appointments by reusing Epic 2's existing `BookingService.Cancel` mechanism — past/Finished appointments are retained as history, never touched (FR18, FR40).
4. **Given** concurrent admin operations on the same account — two admins editing the same account, or an admin edit racing the account holder's own self-edit from Story 1.7 — **when** two commits race, **then** the existing `RowVersion` concurrency token (Story 1.2, AD-16) rejects the second writer with a conflict, surfaced the same way Story 1.7's self-edit conflict already is (`AccountConflictException`, 409) (FR41).
5. **Given** these repository and service methods, **when** tested, **then** every one of them — including the appointment cascade and the concurrency-conflict path — is covered by xUnit.v3 + `WebApplicationFactory` against a real (temp) SQLite instance, never mocked (NFR4, AD-4).

## Tasks / Subtasks

- [ ] **Task 1: Extend `IAccountRepository`/`AccountRepository`** (AC: #1, #2, #4)
  - [ ] Add `Task<List<Account>> Search(string query)` — case-insensitive partial match on `FirstName`, `LastName`, or `Email` (`EF.Functions.Like` or `.Contains` — match whatever casing approach `FindByEmail`'s existing normalize-then-compare already establishes), filtered to `Role != Role.Admin && DeletedAt == null`. Empty/whitespace query returns an empty list (mirrors the UX's "before any search" empty state — no results without input, not "return everyone").
  - [ ] Add `Task AdminUpdate(Account account)` — same persistence shape as the existing `Update` (trim/lowercase email, `context.Update(account)`, `SaveChangesAsync`, `ReloadAsync`), **plus two repository-level guards, both required**: (1) before applying the update, re-fetch the account's *current* persisted `Role` via a separate `AsNoTracking` query on `account.Id` and throw `AdminAccountProtectedException` if that current role is `Admin` — this is deliberately a fresh DB read, not a check on the (already-mutated) `account` parameter, so it catches "this row is currently the admin account" regardless of what the caller already changed on the in-memory object; (2) separately, check the *incoming* `account.Role` value being written and throw `InvalidRoleAssignmentException` if it is `Role.Admin` — guard (1) alone only protects a row that is *already* admin, it does nothing to stop a non-admin row from being promoted to `Role.Admin`, which is the other half of what AC #2 requires. Do **not** add either guard to the existing `Update` method — that method is still used unmodified by Story 1.7's self-service `UpdateOwnProfile`, where the admin account editing its own profile must keep working.
  - [ ] Add `Task SoftDelete(Account account)` — same repository-level `Role == Admin` guard as `AdminUpdate` (re-fetch current role, throw `AdminAccountProtectedException` if admin), then set `account.DeletedAt = DateTime.UtcNow`, `context.Update(account)`, `SaveChangesAsync`. This formalizes what `AccountRepositoryTests` today does ad hoc (`account.DeletedAt = DateTime.UtcNow; await repository.Update(account);`) into a named, guarded method — do not leave the ad-hoc pattern in place once this method exists.
  - [ ] **Do not add a new repository-level `AdminCreate` method.** The existing `Create(Account account)` is already role-agnostic (used today by both `AuthService.Register` for customers and `AdminBootstrapService` for the seeded admin) and needs no repository change — the "always `Role.Barber`, never another admin" contract belongs at the Service layer (Task 2), which builds the entity with `Role = Role.Barber` before calling the existing `Create`. This matches the established "repository interfaces grow incrementally, only when a genuinely new persistence shape is needed" precedent from Story 2.1's Dev Notes (re: `IAccountRepository.AdminExists()`).
- [ ] **Task 2: Extend `IAccountService`/`AccountService`** (AC: #1, #2, #3, #4)
  - [ ] Add `IBookingService` as a new constructor dependency on `AccountService` (first cross-domain Service→Service dependency in the codebase — see Dev Notes' Design Decisions section for why this is the right seam, not a Repository→Service or Controller→Repository violation of AD-1).
  - [ ] `Task<List<Account>> SearchAccounts(string query)` — thin passthrough to `accountRepository.Search(query)`. No business logic needed yet; exists so a future `AdminController` never calls `AccountRepository` directly (AD-1).
  - [ ] `Task<Account> AdminCreateBarber(string email, string firstName, string lastName, string password)`:
    - Duplicate-email check via `accountRepository.FindByEmail`, throwing the existing `DuplicateEmailException` on collision (reuse — don't invent a second duplicate-email exception type). Wrap the actual `Create` call in the same `try/catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 })` → `DuplicateEmailException` backstop `AuthService.Register` already uses, for the same race-window reason.
    - Hash the password via the existing injected `IPasswordHasher<Account>`.
    - Build the entity with `Role = Role.Barber` hardcoded (ignore any role the caller might somehow pass — there is no role parameter on this method's signature at all, by design, so "never another admin" (FR19) is enforced by the method shape itself, not a runtime check).
    - **Do not add email-format validation here.** Plausible-email-format checking (FR1/FR19) is DTO/Controller-layer request validation, deferred to whichever of Stories 3.3/3.4 builds the Controller — same client-convenience/server-enforcement split Story 2.1 already established for booking-window validation (AD-14).
  - [ ] `Task<Account> AdminUpdateAccount(int accountId, string email, string firstName, string lastName, Role role, string? newPassword, int actingAdminId)`:
    - Load via `accountRepository.FindById(accountId)`; throw a new `AccountNotFoundException` if null (no such exception exists yet — self-service `UpdateOwnProfile` never needed one since its caller's own account always exists; an admin operating on an arbitrary id can legitimately hit a bad/stale id).
    - **Validate `role` is `Customer` or `Barber` only** — throw `InvalidRoleAssignmentException` if `role == Role.Admin`. This is the FR18 "no account can be promoted to admin" guard, checked here as the fast-fail primary check *before* the repository's own redundant re-check of the same incoming value (Task 1's guard (2)) — two independent layers for the same invariant, mirroring AD-9's app-level-pre-check-plus-backstop shape.
    - Duplicate-email check + `SqliteErrorCode: 19` backstop, same as `AdminCreateBarber`, when `email` differs from the loaded account's current email.
    - Detect a demotion: `account.Role == Role.Barber && role == Role.Customer`.
    - Mutate `account.Email`/`FirstName`/`LastName`/`Role` unconditionally; if `newPassword` is non-empty, hash it into `PasswordHash` **and increment `account.SessionVersion`** (FR35 — this is the one behavior that deliberately diverges from `UpdateOwnProfile`, which never touches `SessionVersion`). A permission-only change (no `newPassword`) must leave `SessionVersion` untouched.
    - Call `accountRepository.AdminUpdate(account)`; catch `DbUpdateConcurrencyException` and rethrow as the existing `AccountConflictException` (same 409 shape as Story 1.7's self-edit conflict — don't invent a second conflict exception type).
    - If the update succeeded and this was a demotion, call the new `bookingService.CancelAllFutureForBarber(accountId, actingAdminId, Role.Admin)` (Task 3) **after** the account update commits, not before — if the account update itself fails (duplicate email, stale RowVersion, admin-protected), no appointments should be touched.
  - [ ] `Task AdminSoftDeleteAccount(int accountId, int actingAdminId)`:
    - Load via `FindById`, throw `AccountNotFoundException` if missing.
    - Call `accountRepository.SoftDelete(account)` (repository throws `AdminAccountProtectedException` if this is the admin account — let it propagate), wrapped in the **same `catch (DbUpdateConcurrencyException) → AccountConflictException` mapping `AdminUpdateAccount` uses**. `SoftDelete` writes through the same `RowVersion` token as `AdminUpdate` (Task 1), so a delete can race a concurrent edit exactly as AC #4 and AD-16 describe ("an edit racing a delete") — don't skip this catch just because `AdminUpdateAccount`'s version reads as the "obvious" place for it.
    - If the loaded account's `Role == Role.Barber`, call `bookingService.CancelAllFutureForBarber(accountId, actingAdminId, Role.Admin)` after the soft-delete commits, same ordering rule as above.
  - [ ] New exception files in `Services/` (one-liner marker classes, matching the existing `public class Foo : Exception;` convention exactly):
    - `AdminAccountProtectedException.cs`
    - `AccountNotFoundException.cs`
    - `InvalidRoleAssignmentException.cs`
  - [ ] No `Program.cs` changes needed for `IAccountService`'s new `IBookingService` dependency — both are already registered `Scoped`; the DI container resolves the added constructor parameter automatically.
- [ ] **Task 3: Extend `IAppointmentRepository`/`AppointmentRepository` and `IBookingService`/`BookingService` for the cascade** (AC: #3)
  - [ ] Add `Task<List<Appointment>> FindFutureByBarber(int barberId, DateTime nowEst)` to `IAppointmentRepository`/`AppointmentRepository` — same shape as the existing `FindUpcomingByCustomer(int customerId, DateTime nowEst)`, filtered to `BarberId == barberId && CancelledAt == null` plus the same "not yet occurred" string comparison against `nowEst`.
  - [ ] Add `Task CancelAllFutureForBarber(int barberId, int callerAccountId, Role callerRole, DateTime? now = null)` to `IBookingService`/`BookingService`:
    - Resolve `nowEst` via the existing private `ResolveNowEst(now)` helper — reuse it verbatim, do not duplicate its `DateTimeKind.Unspecified` validation logic (per the existing hardening already in place from Story 2.3's review).
    - Fetch `appointmentRepository.FindFutureByBarber(barberId, nowEst)`, then call the existing `Cancel(appointmentId, callerAccountId, callerRole, now)` for each one found.
    - Catch and swallow `AppointmentAlreadyCancelledException`/`AppointmentAlreadyFinishedException` per-appointment inside the loop (log nothing — no `ILogger` precedent exists anywhere in this codebase yet, matching every other catch-all) rather than letting one already-resolved appointment abort the cascade for the rest. This is a narrow, accepted race window (time passing between the fetch and each individual cancel call) — not expected to matter in a single local SQLite instance with no concurrent admin traffic (NFR7), but cheap to guard against.
    - Pass `Role.Admin` as `callerRole` when called from `AccountService` — `Cancel`'s existing authorization switch already treats `Role.Admin` as unconditionally authorized (`Role.Admin => true`), so no change to `Cancel` itself is needed.
- [ ] **Task 4: Repository, Service, and cascade tests** (AC: #5)
  - [ ] Reuse `SqliteApiFactory` verbatim — no new test fixture needed.
  - [ ] `AccountRepositoryTests.cs` additions:
    - `Search_matches_partial_name_or_email_case_insensitive`
    - `Search_excludes_admin_account`
    - `Search_excludes_soft_deleted_accounts`
    - `Search_with_blank_query_returns_empty_list`
    - `AdminUpdate_updates_fields_and_increments_RowVersion`
    - `AdminUpdate_on_admin_account_throws_AdminAccountProtectedException`
    - `AdminUpdate_on_stale_RowVersion_throws_DbUpdateConcurrencyException` — two independent `DbContext`/repository instances both load the same row, first `AdminUpdate` succeeds, second throws (same two-`DbContext` deterministic pattern as Story 1.7's `Update_on_stale_RowVersion...` test — do not attempt a real concurrent-HTTP race).
    - `AdminUpdate_promoting_a_non_admin_account_to_Admin_throws_InvalidRoleAssignmentException` — proves guard (2) independently of guard (1): a non-admin row, with an incoming `Role.Admin` value, must still be rejected.
    - `SoftDelete_sets_DeletedAt`
    - `SoftDelete_on_admin_account_throws_AdminAccountProtectedException`
    - `SoftDelete_on_stale_RowVersion_throws_DbUpdateConcurrencyException` — same two-`DbContext` pattern as `AdminUpdate`'s, proving `SoftDelete`'s write is also RowVersion-guarded.
  - [ ] `AccountServiceTests.cs` additions:
    - `AdminCreateBarber_creates_account_with_Role_Barber`
    - `AdminCreateBarber_rejects_duplicate_email`
    - `AdminUpdateAccount_password_change_increments_SessionVersion`
    - `AdminUpdateAccount_permission_only_change_does_not_increment_SessionVersion`
    - `AdminUpdateAccount_rejects_role_Admin_value_with_InvalidRoleAssignmentException`
    - `AdminUpdateAccount_on_admin_account_throws_AdminAccountProtectedException`
    - `AdminUpdateAccount_on_missing_account_throws_AccountNotFoundException`
    - `AdminUpdateAccount_on_stale_RowVersion_throws_AccountConflictException` — two-`DbContext` pattern again, through the Service (not just the repository) to prove the `DbUpdateConcurrencyException` → `AccountConflictException` mapping.
    - `AdminUpdateAccount_demoting_barber_to_customer_cancels_future_appointments_but_retains_past`
    - `AdminUpdateAccount_editing_a_customer_account_leaves_its_appointments_untouched` — real-DB behavioral assertion (seed the customer's own future appointment, edit their name, assert it's still there and not cancelled), not a call/spy check — this codebase has no mocking framework for backend tests (AD-4).
    - `AdminSoftDeleteAccount_on_barber_cancels_future_appointments_but_retains_past`
    - `AdminSoftDeleteAccount_on_admin_account_throws_AdminAccountProtectedException`
    - `AdminSoftDeleteAccount_on_stale_RowVersion_throws_AccountConflictException` — two-`DbContext` pattern, through the Service, proving `AdminSoftDeleteAccount`'s new concurrency catch actually maps correctly.
  - [ ] `AppointmentRepositoryTests.cs` / `BookingServiceTests.cs` additions:
    - `FindFutureByBarber_excludes_past_and_cancelled_appointments`
    - `CancelAllFutureForBarber_cancels_all_future_appointments_for_that_barber_only` (seed a second barber with their own future appointment; confirm it's untouched)
    - `CancelAllFutureForBarber_tolerates_an_already_cancelled_appointment_without_aborting_the_rest`
  - [ ] Backend suite must stay green (`dotnet test`).
- [ ] **Task 5: Check `deferred-work.md` and Epic 2 retro action items** (retro discipline, per Epic 1/2's own established practice)
  - [ ] Re-read `deferred-work.md` in full at kickoff (per the standing Epic 1 retro action item, still in force). None of the currently-open items (NavBar `aria-live`, zero-barbers retry affordance, `BarberSeedService` removal tied to Story 3.4) apply to this backend-only repository/service story — confirm and note as "checked, not applicable" in Completion Notes rather than silently skipping.
  - [ ] The Epic 2 retro's Key Insight for Epic 3 (FR41 reuses Account's existing `RowVersion`/AD-16 mechanism from Story 1.2/1.7, already proven working — no new concurrency mechanism to invent) is exactly what Task 1/2 above do — no additional action needed beyond building on top of it as designed.
- [ ] **Task 6: Verify CI green and branch/PR**
  - [ ] Branch as `story/3.1-account-repository-admin-operations` from `main`.
  - [ ] Push and confirm both CI jobs (Backend .NET, Frontend Vite/React) green before merging (AD-11). This story makes no frontend changes, so the frontend job should be an unaffected pass-through.

## Dev Notes

### Architecture Compliance (must-follow, not optional)

- **AD-1 (layering)** — Controllers → Services → Repositories, one-way. This story adds to `Repositories/` and `Services/` only; `Controllers/AccountController.cs` stays untouched — Stories 3.2/3.3/3.4/3.5 are what add the Admin-facing HTTP surface on top of what this story ships. There is **one** Account/Admin trio (per `ARCHITECTURE-SPINE.md`'s Structural Seed and Section 2's explicit rationale) — do not create a separate `AdminController`/`AdminService`/`AdminRepository`; everything here extends the existing `AccountRepository`/`AccountService`.
- **AD-2 (Role enum)** — `Role` is the fixed `Customer`/`Barber`/`Admin` enum everywhere; never a string literal. `AdminUpdateAccount`'s `role` parameter is typed `Role`, not `string`.
- **AD-7 (int PKs)** — no change; `Account.Id` is already an int auto-increment.
- **AD-15 (Account soft-delete, relaxed uniqueness)** — `SoftDelete` only ever sets `DeletedAt`; never a hard `DELETE`. The existing `UNIQUE(Email) WHERE DeletedAt IS NULL` index already makes a soft-deleted account's email immediately reusable — no index change needed in this story.
- **AD-16 (Account optimistic concurrency)** — `RowVersion` already exists on the entity and is already configured as an EF concurrency token (Story 1.2). This story's `AdminUpdate`/`SoftDelete` reuse it as-is; do not add a second concurrency mechanism.
- **AD-6 (admin bootstrap, do-not-touch boundary)** — explicitly prevents "an admin-creation UI/backdoor." `AdminCreateBarber` has no `role` parameter at all (always `Role.Barber`, structurally) specifically so this story can never become a second way to mint an admin account. Do not add a role parameter to this method "for flexibility" — that would reopen exactly the hole AD-6 closes.
- **AD-4/AD-10/NFR4 (testing)** — real temp SQLite via `WebApplicationFactory`/`SqliteApiFactory`, never mocked. The concurrency-conflict tests use two independent `DbContext` instances (deterministic staging), never a real concurrent HTTP race — this lesson was learned the hard way in Stories 1.2/1.7 and is now standing practice.

### Design Decisions This Story Must Make (epics/architecture leave these open)

Neither `epics.md` nor the architecture documents specify method signatures for Epic 3's admin operations — they stop at the AD/FR/invariant level (confirmed by direct research: no admin endpoint, DTO, or method shape is written down anywhere). This story makes the following calls; revisit here if a later story finds the shape wrong, same as Story 2.1 flagged its own Finished-boundary decision for later reconsideration:

- **Repository vs. Service split for the four named operations.** `Search` is pure data access (repository-only, Service passes through). `AdminCreate`'s "always Barber" contract lives entirely at the Service layer (`AdminCreateBarber`) reusing the existing role-agnostic `Create` — no new repository method. `AdminUpdate`/`SoftDelete` get **new, distinct repository methods** (not reuse of the existing `Update`) specifically so the FR34 admin-protection guard can live at the repository level without affecting Story 1.7's self-service `UpdateOwnProfile` path, which must keep working unmodified for the admin account editing its own profile.
- **The repository-level FR34 guard re-fetches current state rather than trusting the passed-in entity.** By the time `AdminUpdate(account)` is called, the caller (Service) may have already mutated `account.Role` away from whatever it was — so the repository re-queries the account's *persisted* role fresh (`AsNoTracking`) before applying changes, to catch "this is currently the admin account" regardless of caller behavior. This is the actual mechanism behind AC #2's "rejected at the repository level, independent of the Service layer."
- **`AccountService` gains its first cross-domain Service dependency (`IBookingService`).** Every Service built so far (`AuthService`, `AccountService`, `BookingService`) has been independent; only Controllers previously coordinated across domains (e.g., via `HttpContext.Items["Account"]`). The appointment-cascade requirement (AC #3) genuinely needs Account-domain logic to trigger Booking-domain behavior, and AD-1 governs Controller→Service→Repository ordering, not Service→Service composition — so `AccountService` depending on `IBookingService` is the correct seam, not a layering violation. If a future story needs the reverse (Booking depending on Account), stop and reconsider before wiring it, since that would risk a dependency cycle.
- **Cascade ordering: account mutation commits before the appointment cascade runs.** If `AdminUpdateAccount`/`AdminSoftDeleteAccount` fail (duplicate email, stale RowVersion, admin-protected), no appointments must be touched — the cascade is a side effect of a *successful* demotion/deletion, not a parallel operation.
- **`actingAdminId` is threaded through explicitly**, not read from ambient context — this story has no Controller/`HttpContext` to pull it from yet. The admin's own account id becomes an explicit parameter that whichever of Stories 3.3/3.5 builds the Controller will supply from `HttpContext.Items["Account"]`, matching the existing `AccountController`/`SessionLivenessMiddleware` pattern.

### Testing Requirements

- xUnit.v3 + `WebApplicationFactory` against `SqliteApiFactory`'s real temp SQLite instance — no mocked `DbContext`, no EF in-memory provider (it wouldn't enforce the filtered unique index the admin-protection/soft-delete guards depend on).
- Concurrency-conflict tests (both the repository-level `AdminUpdate` test and the Service-level `AdminUpdateAccount` test) must use two independent `DbContext`/repository/service instances loading the same row before either writes — never a real concurrent-HTTP race. This is standing practice since Stories 1.2/1.7's flaky-test fixes and was explicitly re-applied proactively (not just where an AC demanded it) throughout Epic 2.
- The appointment-cascade tests must seed a *second* barber with their own future appointment to prove `CancelAllFutureForBarber` is scoped correctly (`barberId`-filtered, not "cancel everything") — a cascade that accidentally cancelled another barber's schedule would be a silent, hard-to-notice disaster exactly of the kind this workflow exists to prevent.
- Cover the "already Finished" boundary is **not** re-litigated here — `FindFutureByBarber` only returns not-yet-occurred appointments by construction, so `CancelAllFutureForBarber` should never actually hit `AppointmentAlreadyFinishedException` in normal operation; the per-appointment catch in Task 3 is a defensive backstop for the narrow fetch-to-cancel race window, not the primary mechanism.

### Project Structure Notes

- `Repositories/AccountRepository.cs` and `Services/AccountService.cs` are both **modified**, not new — this is Epic 3's first story and it extends Epic 1's foundation rather than adding a new domain folder.
- `Repositories/AppointmentRepository.cs` and `Services/BookingService.cs` are also **modified** (the cascade addition) — this is the one place this story reaches into Epic 2's domain, and it does so exactly as AD-1 prescribes: `AccountService` calls `IBookingService`'s public interface, never `AppointmentRepository` directly.
- `Services/` gains three new one-line exception files: `AdminAccountProtectedException.cs`, `AccountNotFoundException.cs`, `InvalidRoleAssignmentException.cs` — same convention as the existing eleven marker exceptions (`DuplicateEmailException`, `InvalidCredentialsException`, `InvalidSessionException`, `AccountConflictException`, `InvalidCurrentPasswordException`, `SameAsCurrentPasswordException`, `BookingConflictException`, `AppointmentAlreadyCancelledException`, `AppointmentAlreadyFinishedException`, `AppointmentNotFoundException`, `InvalidBookingWindowException`).
- No `Controllers/`, `Dtos/`, or frontend changes at all in this story — purely backend data/business layer, matching this project's established "dedicated repository/service story before feature/UI stories" pattern already used for Stories 1.2 and 2.1.
- `Program.cs` needs no new registrations — both `IAccountRepository`/`AccountRepository` and `IAppointmentRepository`/`AppointmentRepository`/`IBookingService`/`BookingService` are already `Scoped`; `AccountService` picking up a new constructor parameter resolves automatically.

### Established Codebase Patterns to Extend (current state, read in full for this story)

- `IAccountRepository` today: `Create`, `FindByEmail`, `FindById`, `Update`, `AdminExists`, `FindAllByRole` — all thin persistence, no business rules. `FindById`/`FindByEmail` already filter out soft-deleted rows (`DeletedAt == null`), so there's no existing way to fetch a soft-deleted account by id — not needed for this story's scope, just noted so it isn't assumed to already exist.
- `IAccountService` today has exactly one method, `UpdateOwnProfile(int accountId, string firstName, string lastName, string? newPassword, string? currentPassword)` — loads, validates current password (`InvalidCurrentPasswordException`/`SameAsCurrentPasswordException`), mutates, calls `Update`, catches `DbUpdateConcurrencyException` → `AccountConflictException`. This is the closest existing template for `AdminUpdateAccount`'s shape, minus the current-password check (an admin doesn't need to know the target's current password) and plus the `SessionVersion` bump this story introduces.
- `Account` entity fields (unchanged by this story): `Id` (int), `Email`, `PasswordHash`, `FirstName`, `LastName`, `Role` (enum, stored as string via `HasConversion<string>()`), `SessionVersion` (int), `DeletedAt` (nullable `DateTime`), `RowVersion` (int, `IsConcurrencyToken()`, default `0`).
- `AccountController` today: `[Authorize]` class-level, single `PUT api/account/me` action, pulls the caller's `Account` from `HttpContext.Items["Account"]` (populated by `SessionLivenessMiddleware` per AD-2), maps service exceptions to `Problem()` responses with a catch-all 500. The only precedent for role-gating a whole controller in this codebase is the test-only `RoleGateTestController`'s `[Authorize(Roles = "Admin")]` — worth knowing for whichever story builds the real Admin Controller, though irrelevant to this repository/service-only story.
- `BookingService.Cancel(int appointmentId, int callerAccountId, Role callerRole, DateTime? now = null)`'s authorization switch already special-cases `Role.Admin => true` (unconditionally authorized) — confirmed by reading the current implementation. `CancelAllFutureForBarber` relies on this exact behavior and must not duplicate or reimplement it.
- Test fixture convention across every existing Account/Appointment test file: `IDisposable` class holding a `private readonly SqliteApiFactory _factory = new();`, no mocks anywhere, `MethodName_condition_expectedOutcome` naming, a local `NewAccount(...)`/`NewAppointment(...)` factory helper with named optional parameters. New test additions in this story must match this exactly rather than introducing a new fixture style.

### Git Intelligence Summary

Recent commits (`47c5a27` → ... → `0b7cbdf` → `442df9f`) confirm the established rhythm: create the story on `main`, implement on `story/{epic}.{story}-{slug}`, PR with a summary of additions/fixes/test counts, merge once both CI jobs are green, delete the branch. `442df9f` (Epic 2 Retrospective) is the current tip of `main` — no Epic 3 code exists yet; this story is the first change to the Account/Appointment domains since Story 2.6.

### References

- [Source: _bmad-output/planning-artifacts/epics.md §Epic 3, §Story 3.1] — story statement, five acceptance criteria, FR coverage map (FR16–FR19, FR34, FR35, FR40, FR41), and Stories 3.2–3.5's own ACs (cross-referenced for what each depends on from this story)
- [Source: _bmad-output/planning-artifacts/prds/prd-bmad-learning-project-2026-07-21/prd.md §FR1, §FR16–FR19, §FR31, §FR34, §FR35, §FR40, §FR41, §NFR1, §NFR2, §NFR4] — exact FR/NFR wording
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-bmad-learning-project-2026-07-23/EXPERIENCE.md, DESIGN.md] — admin search/edit/create popup field lists, empty states, concurrent-edit error copy (informs what the Service layer must support even with no UI in this story)
- [Source: _bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/ARCHITECTURE-SPINE.md#AD-1, #AD-2, #AD-6, #AD-7, #AD-15, #AD-16, #Structural-Seed] — layering, Role enum, admin-bootstrap boundary, PK strategy, soft-delete, concurrency
- [Source: _bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/SOLUTION-DESIGN.md §2, §4, §6] — Account/Admin trio rationale, schema/concurrency/soft-delete rationale, no-DB-mocking testing rationale
- [Source: backend/BarbershopApi/Repositories/AccountRepository.cs, IAccountRepository.cs, Services/AccountService.cs, IAccountService.cs, Entities/Account.cs, Controllers/AccountController.cs, Data/BarbershopDbContext.cs] — current implementation this story extends
- [Source: backend/BarbershopApi/Services/BookingService.cs, IBookingService.cs, Repositories/AppointmentRepository.cs, IAppointmentRepository.cs] — `Cancel`'s existing authorization logic and `FindUpcomingByCustomer`'s query shape, reused/mirrored for the cascade
- [Source: _bmad-output/implementation-artifacts/2-1-appointment-entity-and-repository.md] — repository/service split precedent, two-`DbContext` concurrency-test pattern, Dev Notes structure this story follows
- [Source: _bmad-output/implementation-artifacts/1-7-self-service-account-management.md] — `UpdateOwnProfile`/`AccountConflictException` precedent this story's `AdminUpdateAccount` extends
- [Source: _bmad-output/implementation-artifacts/epic-2-retro-2026-08-11.md §Key Insight for Epic 3] — FR41 reuses Story 1.2's existing RowVersion mechanism, no new concurrency design needed
- [Source: _bmad-output/implementation-artifacts/deferred-work.md] — confirmed no currently-open item applies to this backend-only story
- [Source: project-context.md §Technology Stack & Versions; §Language-Specific Rules (C#); §Framework-Specific Rules; §Testing Rules; §Critical Don't-Miss Rules]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
