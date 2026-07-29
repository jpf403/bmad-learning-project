---
baseline_commit: 99350bb2a2181335f2e08ea2ae4aedc8ad636ce3
---

# Story 1.2: Account Entity & Repository

Status: done

## Story

As a developer,
I want the `Account` entity, its migration, and every repository method Epic 1's stories will need,
so that registration, sign-in, admin bootstrap, and self-service editing can all be built as pure business logic on top of a working, tested data layer — no schema changes after this story.

## Acceptance Criteria

1. **Given** no `Account` entity exists yet, **when** this story is implemented, **then** the entity/migration is created with `int` auto-increment PK (AD-7), `Email` (unique, scoped to non-deleted rows — `UNIQUE(Email) WHERE DeletedAt IS NULL`, AD-15), `PasswordHash`, `FirstName`, `LastName`, `Role` (fixed `Customer`/`Barber`/`Admin` enum, AD-2), `SessionVersion` (int, AD-3), `DeletedAt` (nullable, AD-15), and `RowVersion` (EF Core concurrency token, AD-16).
2. **Given** the entity, **when** the `AccountRepository` is built, **then** it exposes every method Epic 1 needs: `Create` (normalizing `Email` to lowercase before persisting — client decision, 2026-07-28, since email matching is case-insensitive throughout the product), `FindByEmail` (normalizing the lookup value to lowercase before querying, excluding soft-deleted rows), `FindById` (excluding soft-deleted rows — AD-15 treats a deleted account as "does not exist"), and `Update` (with optimistic concurrency via `RowVersion`).
3. **Given** the repository, **when** tested, **then** every method is covered by xUnit + `WebApplicationFactory` tests against a real (temp) SQLite instance — never mocked (NFR4, AD-4).
4. **Given** this story is complete, **when** Stories 1.4, 1.5, and 1.7 are built, **then** they add only business logic (Controller/Service) on top of this repository — no further schema changes for anything Epic 1 needs (AD-1 layering).

## Tasks / Subtasks

- [x] **Task 1: Define the `Role` enum and `Account` entity** (AC: #1)
  - [x] Create `backend/BarbershopApi/Entities/Role.cs`: `public enum Role { Customer, Barber, Admin }` — the one shared type every future role check/seeder references (AD-2).
  - [x] Create `backend/BarbershopApi/Entities/Account.cs` with: `Id` (`int`), `Email` (`string` — always stored lowercase, see Email Normalization in Dev Notes), `PasswordHash` (`string`), `FirstName` (`string`), `LastName` (`string`), `Role` (`Role` enum — not a raw string/int field on the C# side), `SessionVersion` (`int`, defaults to `0`), `DeletedAt` (`DateTime?`), `RowVersion` (`int` — **not** `byte[]`; see Task 2's SQLite note for why).
- [x] **Task 2: Configure EF Core mapping** (AC: #1)
  - [x] In `BarbershopDbContext`, add `public DbSet<Account> Accounts => Set<Account>();` and override `OnModelCreating`.
  - [x] Map `Role` with `.HasConversion<string>()` — the ERD in both `ARCHITECTURE-SPINE.md` and `SOLUTION-DESIGN.md` types the column as `string Role`, and storing the enum's name (not its int ordinal) is what keeps the DB value human-readable and immune to reordering the enum later.
  - [x] Configure the partial unique index: `.HasIndex(a => a.Email).IsUnique().HasFilter("DeletedAt IS NULL")` (AD-15) — SQLite has supported partial indexes since 3.8.0 and EF Core's SQLite provider translates `HasFilter` directly to a `WHERE` clause, so no raw SQL is needed for this one.
  - [x] Configure `RowVersion`: `.Property(a => a.RowVersion).IsConcurrencyToken().HasDefaultValue(0)`. **Read the SQLite concurrency gotcha in Dev Notes before doing this** — `IsRowVersion()`/`IsConcurrencyToken()` alone only makes EF include `RowVersion` in the `WHERE` clause of `UPDATE`/`DELETE` statements; it does **not** make SQLite auto-increment the value the way SQL Server's native `rowversion` type does. Task 3 adds the trigger that actually makes this work.
- [x] **Task 3: Migration, including the RowVersion trigger** (AC: #1)
  - [x] Run `dotnet ef migrations add AddAccountEntity` from `backend/BarbershopApi/`.
  - [x] Edit the generated migration's `Up()` to append a raw-SQL trigger via `migrationBuilder.Sql(...)`: `CREATE TRIGGER trg_Accounts_RowVersion AFTER UPDATE ON Accounts BEGIN UPDATE Accounts SET RowVersion = RowVersion + 1 WHERE rowid = NEW.rowid; END;` — without this trigger, `RowVersion` never actually changes on update and AD-16's "first commit wins" guarantee silently does nothing.
  - [x] Add the matching `DROP TRIGGER IF EXISTS trg_Accounts_RowVersion;` to the migration's `Down()`.
  - [x] Verify `dotnet ef database update` runs clean against a fresh temp DB and against the existing dev DB (`backend/BarbershopApi/App_Data/barbershop.db`, if present locally).
- [x] **Task 4: Build `AccountRepository`** (AC: #2, #4)
  - [x] Create `backend/BarbershopApi/Repositories/IAccountRepository.cs` and `AccountRepository.cs`, constructor-injecting `BarbershopDbContext` (the repository is the only layer allowed to touch the DbContext directly — AD-1).
  - [x] `Task<Account> Create(Account account)` — lowercases `account.Email` (invariant culture, e.g. `.ToLowerInvariant()`) before adding and saving; returns the entity with its generated `Id` populated.
  - [x] `Task<Account?> FindByEmail(string email)` — lowercases the incoming `email` parameter the same way before querying, filters `DeletedAt == null`; returns `null` on no match (AD-15: a deleted account's row still exists but must behave as if it doesn't for every caller of this method).
  - [x] `Task<Account?> FindById(int id)` — same `DeletedAt == null` filter, same reasoning: this is the method Story 1.6's per-request role/session re-derivation (AD-2) will call, and a soft-deleted account must produce the same "not found" result a truly-absent account would.
  - [x] `Task Update(Account account)` — attaches the entity (`_context.Update(account)` / `Entry(account).State = EntityState.Modified`) and calls `SaveChangesAsync()`. **Do not catch `DbUpdateConcurrencyException` here** — let it propagate. Converting it into a 409 `ProblemDetails` response is a Controller/Service concern that doesn't exist yet (arrives with Stories 1.7/3.1/3.3); this story's contract is just "a stale `RowVersion` throws."
  - [x] Register `IAccountRepository` → `AccountRepository` as `Scoped` in `Program.cs` (`builder.Services.AddScoped<IAccountRepository, AccountRepository>();`) — this is the one `Program.cs` touch this story makes; do not add anything to `Controllers/` or `Services/` (both stay `.gitkeep`-only until Stories 1.4+ need them, per AD-1/NFR6 — see Story 1.1's Dev Notes on why pre-populating those folders early is itself a violation).
- [x] **Task 5: Repository tests** (AC: #3)
  - [x] Follow Story 1.1's `MigrationSmokeTests` pattern: `WebApplicationFactory<Program>` + `ConfigureServices` → `RemoveAll<DbContextOptions<BarbershopDbContext>>()` → re-`AddDbContext` pointed at a fresh temp SQLite file, `SqliteConnection.ClearAllPools()` in teardown before deleting the file. Consider extracting this boilerplate into a shared test fixture/base class now (`BarbershopApi.Tests/SqliteApiFactory.cs` or similar) — Stories 2.1 and 3.1 will need the identical setup and duplicating it three times is exactly the kind of thing worth naming once.
  - [x] `Create_persists_account_with_expected_defaults` — new row gets an auto-generated `Id`, `SessionVersion == 0`, `DeletedAt == null`, `RowVersion` at its initial value.
  - [x] `Create_lowercases_email_before_persisting` — `Create` with e.g. `"Jack@Example.com"` persists `"jack@example.com"`.
  - [x] `Create_with_duplicate_active_email_throws` — a second `Create` with the same `Email` on a non-deleted row throws (the partial unique index backstop, AD-15).
  - [x] `Create_with_differently_cased_duplicate_email_throws` — `"jack@example.com"` then `"Jack@Example.com"` collide the same way, proving normalization happens *before* the uniqueness check, not just on read.
  - [x] `Create_after_soft_delete_of_duplicate_email_succeeds` — soft-delete (`DeletedAt` set) the first account, then `Create` a new one with the same email succeeds immediately (AD-15's explicit "email becomes registerable again immediately").
  - [x] `FindByEmail_matches_regardless_of_input_casing` — an account created with `"jack@example.com"` is found via `FindByEmail("Jack@Example.com")`.
  - [x] `FindByEmail_returns_null_for_soft_deleted_account` and `FindByEmail_returns_null_when_no_match`.
  - [x] `FindById_returns_null_for_soft_deleted_account`.
  - [x] `Update_increments_RowVersion` — load, mutate, `Update`, reload, assert `RowVersion` is exactly one greater (proves the Task 3 trigger actually fires — this is the test that would fail silently-wrong if the trigger were forgotten).
  - [x] `Update_with_stale_RowVersion_throws_DbUpdateConcurrencyException` — load the same row via two separate `DbContext` instances (two separate factory-created scopes, simulating two concurrent requests), update-and-save via the first, then attempt `Update` via the second (still holding the pre-update `RowVersion`) and assert it throws `DbUpdateConcurrencyException`.
- [x] **Task 6: Verify CI green**
  - [x] Branch as `story/1.2-account-entity-repository` from `main` (Story 1.1 landed on `e1-s1-scaffold-and-foundations` instead of the `story/1.1-...` convention — resume the documented convention here now that `main` has it).
  - [x] Push and confirm both CI jobs pass before merging (AD-11).

### Review Findings

- [x] [Review][Decision] Email trimming scope — `Create`/`FindByEmail`/`Update` normalize case (`ToLowerInvariant()`) but never trim whitespace, so `" jack@example.com"` and `"jack@example.com"` are treated as distinct for the partial unique index. The 2026-07-28 client decision only covered case-insensitivity, not whitespace. **Resolved by Jack: add `.Trim()` now** — applied to `Create`/`FindByEmail`/`Update`.
- [x] [Review][Decision] Task 6 CI confirmation — Completion Notes cite only local `dotnet build`/`dotnet test` runs; Task 6 ("push and confirm both CI jobs pass") is checked off with no evidence of the actual GitHub Actions run being confirmed green. **Resolved by Jack: confirmed CI passed on `story/1.2-account-entity-repository`.** Verified `ci.yml`'s `dotnet test --no-build BarbershopApi.Tests` step and `BarbershopApi.Tests.csproj` have no filtering that would exclude `AccountRepositoryTests.cs` — the new tests were in scope of that green run.
- [x] [Review][Patch] `AccountRepository.Update` never re-lowercases `Email` [backend/BarbershopApi/Repositories/AccountRepository.cs:30] — breaks the story's own "normalize in exactly one place" invariant on the update path; a mixed-case email edit bypasses the case-insensitive uniqueness/lookup guarantee. Add `account.Email = account.Email.ToLowerInvariant();` to `Update`, plus a regression test for a duplicate-email-via-`Update` collision.
- [x] [Review][Patch] `AccountRepository.Update` leaves the in-memory `RowVersion` stale after a successful save [backend/BarbershopApi/Repositories/AccountRepository.cs:30-34, backend/BarbershopApi/Data/BarbershopDbContext.cs:20-22] — the SQLite trigger bumps `RowVersion` in the DB but EF never re-reads it back onto the tracked entity (only `ValueGeneratedOnAdd` applies). A second `Update` on the same instance throws a spurious `DbUpdateConcurrencyException`. Refresh the property after `SaveChangesAsync` (e.g. `await context.Entry(account).ReloadAsync();`).
- [x] [Review][Patch] Story's own File List/Change Log omit the PRD/epics/sprint-status changes bundled into this branch — `_bmad-output/planning-artifacts/epics.md`, `.../prd.md`, `.memlog.md`, and `_bmad-output/implementation-artifacts/sprint-status.yaml` are all part of this diff but aren't listed in `## Dev Agent Record → File List` or `## Change Log`. Add them for an accurate audit trail.
- [x] [Review][Patch] `SqliteApiFactory.Dispose()` File.Delete calls have no retry/catch around a possible Windows file-lock `IOException` right after `ClearAllPools()` [backend/BarbershopApi.Tests/SqliteApiFactory.cs:38-49] — Windows-only, timing-dependent edge case; CI backend job runs on `ubuntu-latest` so it can't fire there, only on a local Windows `dotnet test` run. Wrapped in try/catch per Jack's call (low-priority but cheap).
- [x] [Review][Patch] Missing test: `FindById_returns_null_when_no_match` for an id that never existed (distinct from the existing soft-deleted-id test) [backend/BarbershopApi.Tests/AccountRepositoryTests.cs].

**Round 2** (re-review of the round-1 patch, 2026-07-29):

- [x] [Review][Patch] Completion Notes (line ~150) still said normalization "happens only at the repository boundary in `Create`/`FindByEmail`" — stale after `Update` was patched to normalize too. Wording corrected.
- [x] [Review][Patch] No regression test reproduced the actual reported bug (calling `Update` twice on the same tracked instance without an intervening reload). Added `Update_twice_on_same_instance_does_not_throw_spurious_concurrency_exception` — confirms the `ReloadAsync` fix actually resolves it.
- [x] [Review][Patch] `Create`'s and `FindByEmail`'s new `.Trim()` behavior was untested. Added `Create_trims_whitespace_before_persisting` and `FindByEmail_matches_despite_surrounding_whitespace`.
- [x] [Review][Patch] `SqliteApiFactory.Dispose()`'s single try/catch skipped `-wal`/`-shm` sidecar cleanup entirely if the main `.db` delete threw first. Refactored into an independent `TryDelete` call per file.
- [x] [Review][Patch] `SqliteApiFactory.Dispose()` caught only `IOException`; `UnauthorizedAccessException` would still propagate. Widened to catch both.

**Round 3** (re-review of the round-2 patch, 2026-07-29):

- [x] [Review][Patch] `Create_trims_whitespace_before_persisting` only verified the in-memory instance, not a DB round-trip. Now reloads via a separate `DbContext`/repository before asserting, matching the established pattern (`Update_increments_RowVersion`).
- [x] [Review][Patch] `Update_twice_on_same_instance_does_not_throw_spurious_concurrency_exception` only asserted `FirstName` on the same tracked instance. Now reloads via a separate context and asserts both `FirstName` and `RowVersion`.
- [x] [Review][Patch] `SqliteApiFactory` sidecar cleanup omitted SQLite's default rollback-journal file (`-journal`) — added alongside `-wal`/`-shm`.
- [x] [Review][Patch] `TryDelete`'s `File.Exists` check before `File.Delete` was redundant (`File.Delete` is already a no-op on a missing file) — removed.

## Dev Notes

### Architecture Compliance (must-follow, not optional)

- **AD-1 (layering)** — this story adds `Entities/` and `Repositories/` content only. `Controllers/` and `Services/` stay `.gitkeep`-only; don't create an `AccountService` or `AccountController` "to get ahead" — that's Stories 1.4/1.5/1.7's job and doing it now means guessing at a shape those stories haven't earned yet.
- **AD-2 (`Role` as a fixed enum)** — `Role` must be the actual C# enum type on `Account`, not a `string`/`int` field the caller has to remember to validate. The `.HasConversion<string>()` mapping in Task 2 is what makes the *DB column* a string (matching the ERD) while keeping the *C# property* strongly typed — don't pick one or the other.
- **AD-7 (int PKs)** — `Account.Id` is a plain auto-increment `int`, EF Core's default convention for an `int Id` property; no explicit configuration needed.
- **AD-15 (soft-delete + relaxed email uniqueness)** — this AC's `DeletedAt`/partial-unique-index pair is the literal reason `FindByEmail` and `FindById` both filter it out: every future caller (login, `/api/auth/me`, admin search) gets "not found" for a deleted account without having to remember to add the filter themselves.
- **AD-16 (optimistic concurrency)** — see the SQLite-specific note below; this is the single highest-risk part of this story to get subtly wrong (code that compiles and looks right but never actually detects a conflict).
- **AD-4/NFR4 (testing)** — real temp SQLite via `WebApplicationFactory`, never a mocked `DbContext` or an in-memory provider. The in-memory EF Core provider in particular would silently pass every test in this story, including the concurrency ones, since it doesn't enforce real SQL constraints or triggers — it would be actively worse than not testing at all here.

### Critical SQLite Gotcha: RowVersion Does Not Auto-Increment Like SQL Server

The architecture's AD-16 says "EF Core concurrency token (`RowVersion`/`[Timestamp]`)," which is SQL-Server-flavored phrasing — SQL Server's native `rowversion` type auto-increments on every row change with zero application code. **SQLite has no equivalent native type.** Researched against current EF Core guidance (confirmed for EF Core 10, 2026): `.IsRowVersion()`/`.IsConcurrencyToken()` on a SQLite-backed property only tells EF's change tracker to (a) include the column in the `WHERE` clause of generated `UPDATE`/`DELETE` statements and (b) throw `DbUpdateConcurrencyException` when that `WHERE` clause matches zero rows. It does **not** make the database bump the value itself. The correct, current pattern — and the reason the ERD already types `RowVersion` as `int` rather than `byte[]` — is:

1. A plain `int RowVersion` column (not `byte[]`), configured as a concurrency token with a default value.
2. A hand-written `AFTER UPDATE` SQL trigger (Task 3) that does `SET RowVersion = RowVersion + 1` on every update.

Skipping the trigger produces code that compiles, an entity that looks correctly configured, and a first pass of manual testing that appears to work — but two back-to-back updates would both silently "succeed" because `RowVersion` never actually changes between them, defeating AD-16/FR41/NFR2 entirely without any visible error. This is exactly the kind of gap `Update_increments_RowVersion` (Task 5) exists to catch.

[Source: current EF Core + SQLite concurrency-token guidance, verified 2026 — SQLite lacks a native rowversion type; the documented pattern is an app-defined `int`/`byte[]` version column plus a database trigger to auto-increment it, since `IsRowVersion()` alone only configures EF's own concurrency check, not database-side auto-generation.]

### Email Normalization (client decision, 2026-07-28)

Email matching is case-insensitive throughout the product — `"Jack@Example.com"` and `"jack@example.com"` are the same account. Neither the PRD nor the architecture specified this, so it was confirmed directly with Jack during story creation, resolving what would otherwise have been an open question blocking Stories 1.4/1.5.

**Implementation approach:** normalize at the repository boundary, not further up. `Create` lowercases `Email` before insert; `FindByEmail` lowercases its input before querying. This keeps the invariant in exactly one place (the repository) rather than requiring every future caller — registration (1.4), sign-in (1.5), admin search/edit (3.1–3.3) — to remember to lowercase on their own. Do **not** rely on a SQLite `COLLATE NOCASE` column instead of normalizing the stored value: `HasFilter`'s partial-unique-index expression and plain equality lookups both need the *stored* value to already be lowercase for this to compose cleanly with AD-15's `WHERE DeletedAt IS NULL` filter without introducing a second collation concept alongside it.

### Testing Requirements

- xUnit.v3 + `WebApplicationFactory` against a real temp SQLite instance, mirroring `MigrationSmokeTests.cs`'s setup/teardown pattern exactly (`RemoveAll<DbContextOptions<BarbershopDbContext>>()` + re-`AddDbContext`, `SqliteConnection.ClearAllPools()` before file deletion) — this pattern is already proven to work in this repo, don't rediscover it from scratch. [Source: backend/BarbershopApi.Tests/MigrationSmokeTests.cs]
- No mocking of `BarbershopDbContext` or its `DbSet`s anywhere in this story's tests (AD-4, NFR4).
- This story's tests are the first "real" repository tests in the codebase — Stories 2.1 and 3.1 will follow the same shape for `Appointment` and admin-account operations respectively, so getting the fixture/pattern right here pays off twice more later.

### Project Structure Notes

- `Entities/` and `Repositories/` currently contain only `.gitkeep` placeholders (added in Story 1.1) — this story is what actually populates them for the first time.
- `BarbershopDbContext` currently has **no `DbSet`s and an empty `OnModelCreating`** (it was deliberately left bare in Story 1.1 to prove the migration pipeline without any entities) — this story is what gives it its first real content.
- `Program.cs` already has CORS, DB registration, and `Database.Migrate()` wired from Story 1.1; the only addition this story makes to it is the one `AddScoped<IAccountRepository, AccountRepository>()` line. Don't touch the CORS policy, the connection-string resolution, or the `Database.Migrate()` call.
- No frontend changes in this story — it's backend-only (data layer).

### Previous Story Intelligence (from Story 1.1)

- The test-fixture pattern for overriding the DbContext against a temp SQLite file is proven and documented in `MigrationSmokeTests.cs` — reuse it verbatim rather than re-deriving (see Testing Requirements above). Story 1.1 specifically found that overriding the connection string via `ConfigureAppConfiguration` does **not** work because `Program.cs` resolves the connection string into a local variable before that config layer applies — the working approach is `ConfigureServices` + `RemoveAll` + re-`AddDbContext`.
- `xunit.v3` alone only produces an MTP self-executing runner, not a VSTest-discoverable one — `xunit.runner.visualstudio` 3.1.4 is already added to `BarbershopApi.Tests.csproj` as the bridge; no action needed here, just noting why it's there if it looks redundant.
- Story 1.1 deliberately left `BarbershopDbContext` and `OnModelCreating` empty and deliberately did not pre-populate `Controllers/`/`Services/` — this story continues that discipline for `Controllers/`/`Services/` (still empty after this story) while being the one that finally adds real content to `Data/` and the two data-layer folders.
- Story 1.1 branched as `e1-s1-scaffold-and-foundations` rather than following the `story/{epic}.{story}-{slug}` convention documented in project-context.md (a session-start artifact, not a deliberate deviation) — Task 6 resumes the documented convention for this story.

### Git Intelligence Summary

Recent commits (`1201de4` scaffold → `6fb7a6c` mark complete → `fe33441` code-review patches → `bf33828` record CI-verified outcome → `99350bb` CI Node-version bump) show Story 1.1 went through a full implement → review → patch → verify cycle entirely within its own branch before the sprint status flipped to `done`. Follow the same shape here: implement, self-verify CI green, then hand off to `code-review` rather than marking `done` directly.

### References

- [Source: _bmad-output/planning-artifacts/epics.md §Story 1.2] — story statement, AC
- [Source: _bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/ARCHITECTURE-SPINE.md#AD-1, #AD-2, #AD-7, #AD-15, #AD-16, #Structural-Seed] — layering, Role enum, PK strategy, soft-delete/uniqueness, concurrency
- [Source: _bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/SOLUTION-DESIGN.md §2, §4] — layering rationale, data-model rationale (soft-delete choice, concurrency-token choice, `int` PK justification)
- [Source: _bmad-output/planning-artifacts/prds/prd-bmad-learning-project-2026-07-21/prd.md — FR1, FR2, FR18, FR28, FR31, FR34, FR35, FR41; NFR1, NFR2, NFR4]
- [Source: backend/BarbershopApi.Tests/MigrationSmokeTests.cs] — proven WebApplicationFactory + temp-SQLite test pattern
- [Source: backend/BarbershopApi/Data/BarbershopDbContext.cs, backend/BarbershopApi/Program.cs] — current (pre-story) state of the files this story modifies
- [Source: _bmad-output/implementation-artifacts/1-1-project-scaffold-ci-pipeline-and-design-system-foundation.md §Dev Agent Record] — established test-fixture pattern and its rationale
- [Source: project-context.md §Technology Stack & Versions; §Language-Specific Rules (C#); §Testing Rules; §Development Workflow Rules]

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (Amelia persona, bmad-dev-story workflow)

### Debug Log References

- `dotnet ef database update --connection "Data Source=<scratch>/verify-account-migration.db"` — clean apply to fresh temp DB.
- `dotnet ef database update` — clean apply to existing local dev DB (`backend/BarbershopApi/App_Data/barbershop.db`).
- `dotnet test` (backend) — 12/12 passed (1 MigrationSmokeTests + 11 AccountRepositoryTests).

### Completion Notes List

- Implemented `Role` enum and `Account` entity per AC1; `Role` mapped via `.HasConversion<string>()` to keep the DB column a string while the C# property stays a strongly-typed enum (AD-2).
- Configured partial unique index on `Email` (`WHERE DeletedAt IS NULL`, AD-15) and `RowVersion` as an EF concurrency token (AD-16).
- Generated `AddAccountEntity` migration and hand-added the `trg_Accounts_RowVersion` `AFTER UPDATE` trigger (+ matching `DROP TRIGGER` in `Down()`) since SQLite has no native auto-incrementing rowversion type — verified against both a fresh temp DB and the existing local dev DB.
- Built `AccountRepository`/`IAccountRepository` with `Create`, `FindByEmail`, `FindById`, `Update`; email normalization (`Trim().ToLowerInvariant()`) happens at the repository boundary in all three of `Create`/`FindByEmail`/`Update`, per the 2026-07-28 client decision recorded in Dev Notes (whitespace-trimming added during code review). `Update` does not catch `DbUpdateConcurrencyException` — left to propagate per AC2/Task 4.
- Registered `IAccountRepository` as `Scoped` in `Program.cs`; no changes to `Controllers/`/`Services/` (still `.gitkeep`-only, AD-1).
- Extracted `SqliteApiFactory` (subclasses `WebApplicationFactory<Program>`, boots the app once per test instance to run `Database.Migrate()` against a fresh temp SQLite file, then hands out raw `BarbershopDbContext` instances pointed at that same file) as a reusable fixture for Stories 2.1/3.1, per Task 5's suggestion.
- `Update_increments_RowVersion` deliberately reloads via a *second* `BarbershopDbContext`/repository instance rather than the one that performed the update — reusing the same context would return the tracked, unrefreshed in-memory value via EF's identity resolution and silently mask a missing/broken trigger.
- `Update_with_stale_RowVersion_throws_DbUpdateConcurrencyException` uses two independent `BarbershopDbContext` instances against the same DB file to simulate two concurrent request scopes.
- Renamed the working branch from `e1-s2-account-entity-and-repository` to `story/1.2-account-entity-repository` (Jack's choice) to resume the `story/{epic}.{story}-{slug}` convention documented in project-context.md, per Task 6 and Story 1.1's noted deviation.
- No frontend changes; CI's frontend job is unaffected. Backend CI job (restore/build/test) covers this story's scope — verified locally with `dotnet build` and `dotnet test`.

### File List

- `backend/BarbershopApi/Entities/Role.cs` (new)
- `backend/BarbershopApi/Entities/Account.cs` (new)
- `backend/BarbershopApi/Entities/.gitkeep` (deleted)
- `backend/BarbershopApi/Repositories/IAccountRepository.cs` (new)
- `backend/BarbershopApi/Repositories/AccountRepository.cs` (new)
- `backend/BarbershopApi/Repositories/.gitkeep` (deleted)
- `backend/BarbershopApi/Data/BarbershopDbContext.cs` (modified)
- `backend/BarbershopApi/Program.cs` (modified)
- `backend/BarbershopApi/Migrations/20260728201142_AddAccountEntity.cs` (new)
- `backend/BarbershopApi/Migrations/20260728201142_AddAccountEntity.Designer.cs` (new)
- `backend/BarbershopApi/Migrations/BarbershopDbContextModelSnapshot.cs` (modified)
- `backend/BarbershopApi.Tests/SqliteApiFactory.cs` (new)
- `backend/BarbershopApi.Tests/AccountRepositoryTests.cs` (new)
- `_bmad-output/planning-artifacts/epics.md` (modified — FR1/FR18/FR19 email-format amendment)
- `_bmad-output/planning-artifacts/prds/prd-bmad-learning-project-2026-07-21/prd.md` (modified)
- `_bmad-output/planning-artifacts/prds/prd-bmad-learning-project-2026-07-21/.memlog.md` (modified)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified)
- `backend/BarbershopApi/BarbershopApi.csproj` (modified — dependency-vulnerability patch, see Change Log 2026-07-29)

## Change Log

- 2026-07-28: Implemented Account entity/migration/repository (Tasks 1-5); all ACs satisfied, 12/12 backend tests passing; branch renamed to `story/1.2-account-entity-repository`; status set to review.
- 2026-07-29: PRD reopened during dev — FR1/FR18/FR19 amended to require a plausible email format (`@` + domain); epics.md updated to match. No code change to this story's scope (format validation belongs to Stories 1.4/1.5/1.7 per AD-1).
- 2026-07-29: Code review patches applied — `Update` now trims+lowercases `Email` and reloads `RowVersion` after save; `Create`/`FindByEmail` now trim in addition to lowercasing; added regression tests for email normalization on `Update` and for `FindById` on a never-existed id; `SqliteApiFactory.Dispose()` wrapped in try/catch for the Windows file-lock edge case. 15/15 backend tests passing; status set to done.
- 2026-07-29: Round-2 review of the above patch — added a regression test proving `ReloadAsync` fixes the double-`Update` concurrency bug, test coverage for `.Trim()` on `Create`/`FindByEmail`, hardened `SqliteApiFactory.Dispose()`'s cleanup (independent per-file delete, wider exception catch), and corrected stale Completion Notes prose. 18/18 backend tests passing.
- 2026-07-29: Round-3 review of the round-2 patch — strengthened the two new tests to verify via a fresh `DbContext` reload rather than the in-memory instance, added `-journal` to `SqliteApiFactory`'s sidecar cleanup, and dropped a redundant `File.Exists` guard. 18/18 backend tests passing.
- 2026-07-29: Dependency-vulnerability patch (found while working Story 1.3) — pinned transitive packages flagged by `dotnet restore`'s NU1903 advisories: `Microsoft.OpenApi` 2.0.0 → 2.11.0 (CVE-2026-49451, stack-overflow DoS parsing untrusted OpenAPI documents) and `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 → 2.1.12 (CVE-2025-6965, native SQLite memory-corruption bug); both stayed within their existing major version — did not adopt `SQLitePCLRaw`'s v3 `SourceGear.sqlite3` rename/restructure, which drops classic-Xamarin support and wasn't warranted for a same-family patch fix. Verified via `dotnet build` (0 warnings, 0 errors) and `dotnet test` (18/18 passing), plus a manual `dotnet run` + `curl` smoke check confirming EF Core migrations still execute correctly against real SQLite with the new native binary.
