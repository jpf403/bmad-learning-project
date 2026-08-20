---
baseline_commit: f53dd8ebf008ad67548f75cd5e5eacba03449fe6
---

# Story 4.1: Account Schema & SSO-Aware Repository

Status: ready-for-dev

## Story

As a developer,
I want the `Account` entity extended for SSO identities and the repository/service layer updated to handle a nullable password,
so that SSO login, linking, and creation can be built as pure business logic on top of a working, tested data layer.

## Acceptance Criteria

1. **Given** no SSO support exists yet, **when** this story is implemented, **then** `Account.PasswordHash` becomes nullable, and `SsoProvider`/`SsoSubjectId` (nullable strings) are added via migration, with a partial unique index `UNIQUE(SsoProvider, SsoSubjectId) WHERE SsoProvider IS NOT NULL` (AD-19).
2. **Given** the existing `AuthService.Login` path, **when** a login attempt targets an account with `PasswordHash = null`, **then** it fails with the same generic "Invalid email or password" message as any other failed attempt (FR43, FR2) — no distinct SSO-only messaging, and no null-reference error.
3. **Given** the `AccountRepository`, **when** extended for SSO, **then** it exposes `FindBySsoIdentity(provider, subjectId)` and `CreateOrLinkSsoAccount(email, firstName, lastName, provider, subjectId)` — the latter creates a new `Role=Customer` account if no email match exists, or attaches the SSO identity to the existing matching account without altering its `PasswordHash` (FR43, FR44).
4. **Given** FR34's admin invariant, **when** `CreateOrLinkSsoAccount` runs, **then** it can never create or link to the single admin account, nor ever assign `Role=Admin` (FR45).
5. **Given** every existing code path that assumed `PasswordHash` is always non-null (registration, login, Epic 3's admin account edit/create), **when** this story is complete, **then** those paths and their existing tests are reviewed and updated as needed for a nullable `PasswordHash`, with no regression to prior behavior.
6. **Given** the repository, **when** tested, **then** every new method — including account-linking and admin-invariant rejection — is covered by xUnit.v3 + `WebApplicationFactory` against a real SQLite instance (NFR4, AD-4).

## Tasks / Subtasks

- [ ] **Task 1: Extend `Account` entity + EF configuration, generate the migration** (AC: #1)
  - [ ] `Entities/Account.cs`: change `PasswordHash` from `string` (default `string.Empty`) to `string?` (no default). Add `public string? SsoProvider { get; set; }` and `public string? SsoSubjectId { get; set; }`.
  - [ ] `Data/BarbershopDbContext.cs`, inside the existing `modelBuilder.Entity<Account>(entity => { ... })` block: add a partial unique index mirroring the existing Email/Appointment index style —
    `entity.HasIndex(a => new { a.SsoProvider, a.SsoSubjectId }).IsUnique().HasFilter("SsoProvider IS NOT NULL");`
  - [ ] Generate the migration with `dotnet ef migrations add AddSsoFieldsToAccount --project backend/BarbershopApi` — do **not** hand-write a migration body from scratch the way a simple `AddColumn`-only change would allow.
  - [ ] **Critical — inspect the generated migration before trusting it:** SQLite cannot relax a `NOT NULL` constraint via `ALTER TABLE`, so EF Core's SQLite provider will emit a full table-rebuild sequence (create a new `Accounts` table with the new schema, copy rows, drop the old table, rename) to make `PasswordHash` nullable. The `trg_Accounts_RowVersion` trigger — added by hand-written raw SQL in Story 1.2's `AddAccountEntity` migration (`migrationBuilder.Sql(...)`), not part of EF's own model — has **no EF metadata** and will **not** be recreated by the rebuild; dropping/renaming the old `Accounts` table silently drops the trigger with it. The migration's `Up()` must explicitly `DROP TRIGGER IF EXISTS trg_Accounts_RowVersion;` before the rebuild and re-run the exact `CREATE TRIGGER trg_Accounts_RowVersion AFTER UPDATE ON Accounts BEGIN UPDATE Accounts SET RowVersion = RowVersion + 1 WHERE rowid = NEW.rowid; END;` (verbatim from `AddAccountEntity.cs`) after it; `Down()` needs the same treatment in reverse. **Do not skip this** — a silently-dropped trigger breaks AD-16 optimistic concurrency for every future Account write, not just this story's.
  - [ ] Verify the trigger survived by running the existing `AccountRepositoryTests.Update_increments_RowVersion` test (no new test needed — if this migration silently drops the trigger, that test starts failing and is your signal).
  - [ ] `Migrations/BarbershopDbContextModelSnapshot.cs` is regenerated automatically by the `dotnet ef migrations add` command above — don't hand-edit it.

- [ ] **Task 2: Extend `IAccountRepository`/`AccountRepository` with SSO-aware methods** (AC: #3, #4)
  - [ ] Add `Task<Account?> FindBySsoIdentity(string provider, string subjectId)` — matches on `SsoProvider == provider && SsoSubjectId == subjectId`, excludes soft-deleted (`DeletedAt == null`), same convention as `FindByEmail`/`FindById`.
  - [ ] Add `Task<Account> CreateOrLinkSsoAccount(string email, string firstName, string lastName, string provider, string subjectId)`:
    - Look up an existing account by email via the existing `FindByEmail(email)` — it already trims/lowercases and excludes soft-deleted rows; don't duplicate that normalization here.
    - If found: throw the existing `AdminAccountProtectedException` (reuse — don't invent a new exception type) if `existing.Role == Role.Admin` (AC #4). Otherwise set `existing.SsoProvider`/`existing.SsoSubjectId`, then `context.Update(existing)` → `SaveChangesAsync()` → `context.Entry(existing).ReloadAsync()` (same persistence shape as `Update`) — leave `PasswordHash` and `Role` untouched (AC #3's "without altering its PasswordHash").
    - If not found: build a new `Account` with `Role = Role.Customer` hardcoded (**no `role` parameter on this method's signature at all** — same structural "can never mint an admin" guarantee Story 3.1 established for `AdminCreateBarber`, satisfying AC #4's "nor ever assign Role=Admin" by construction, not a runtime check), `PasswordHash = null`, `Email`/`FirstName.Trim()`/`LastName.Trim()`, `SsoProvider`/`SsoSubjectId` set — then persist via the existing role-agnostic `Create` (no new repository `Create` overload needed, same "repository interfaces grow incrementally" precedent from Stories 2.1/3.1).
  - [ ] **Do not** reuse `AdminUpdate`/`SoftDelete`'s `EnsureNotCurrentlyAdmin` (`AsNoTracking` re-fetch) pattern here. That pattern exists specifically because `AdminUpdate`/`SoftDelete` receive an `Account` object the Service layer may have already mutated before calling in. `CreateOrLinkSsoAccount` receives only primitives and loads its own fresh copy via `FindByEmail` inside the same call, so checking `existing.Role` directly is already safe — no re-fetch-via-`AsNoTracking` needed.

- [ ] **Task 3: Harden existing `PasswordHash`-dependent code for nullability** (AC: #2, #5)
  - [ ] `Services/AuthService.cs`, `Login`: after the existing `account is null` check, also treat `account.PasswordHash is null` as an immediate `InvalidCredentialsException` — **before** calling `passwordHasher.VerifyHashedPassword(account, account.PasswordHash, request.Password)`, whose `hashedPassword` parameter is non-nullable and would otherwise throw on a null argument (not gracefully return `Failed`). Same generic message either way — no distinct "this account uses SSO" branch (FR43/FR2).
  - [ ] `Services/AccountService.cs`, `UpdateOwnProfile`: the current-password check (`passwordHasher.VerifyHashedPassword(account, account.PasswordHash, currentPassword)`) must treat `account.PasswordHash is null` (an SSO-only account attempting a self-service password change) as an `InvalidCurrentPasswordException` — the exact same failure a wrong-password attempt already produces, not a null-reference error. No SSO-specific message.
  - [ ] Audit (read through, don't blindly edit) every other `PasswordHash` site: `AuthService.Register`, `AccountService.AdminCreateBarber`, `AccountService.AdminUpdateAccount`, `AdminBootstrapService` — all of these only ever *write* a freshly-hashed value and never read the pre-existing hash, so a nullable backing field needs no logic change there. Confirm this while reading through each one; don't skip re-verifying it just because it's "probably fine."

- [ ] **Task 4: Repository and Service tests** (AC: #6)
  - [ ] Reuse `SqliteApiFactory` verbatim — no new test fixture needed.
  - [ ] `AccountRepositoryTests.cs` additions:
    - `FindBySsoIdentity_matches_provider_and_subject_id`
    - `FindBySsoIdentity_returns_null_when_no_match`
    - `FindBySsoIdentity_excludes_soft_deleted_accounts`
    - `CreateOrLinkSsoAccount_creates_new_account_with_Role_Customer_and_null_PasswordHash_when_no_email_match`
    - `CreateOrLinkSsoAccount_links_to_existing_account_by_email_without_altering_PasswordHash`
    - `CreateOrLinkSsoAccount_linking_preserves_the_existing_account_Role` (link to a pre-existing `Role.Barber` account; assert `Role` is still `Barber`, not reset to `Customer`)
    - `CreateOrLinkSsoAccount_on_existing_admin_account_throws_AdminAccountProtectedException`
  - [ ] `AuthControllerTests.cs` addition (Login is exercised at the controller/HTTP level in this codebase, not via a standalone `AuthServiceTests`):
    - `Login_against_account_with_null_PasswordHash_returns_401_generic_message` — seed an account directly through a `DbContext` with `PasswordHash = null` (there's no SSO login flow yet to create one through — that's Story 4.2), then assert `POST /api/auth/login` for that email returns the same generic 401 body as `Login_with_wrong_password_returns_401_generic_message`.
  - [ ] `AccountServiceTests.cs` addition:
    - `UpdateOwnProfile_on_account_with_null_PasswordHash_and_newPassword_throws_InvalidCurrentPasswordException`
  - [ ] Backend suite must stay green (`dotnet test`).

- [ ] **Task 5: Check `deferred-work.md`** (retro discipline, standing practice since Epic 1)
  - [ ] Re-read `deferred-work.md` in full at kickoff. None of the currently-open items (NavBar `aria-live`, zero-barbers retry affordance, admin-edit popup Tab-trap `isSubmitting` gap, `POST /api/account` rate-limiting gap, various generic-error-message fallthroughs) touch the Auth domain or `Account` schema — confirm and note as "checked, not applicable" in Completion Notes rather than silently skipping.

- [ ] **Task 6: Verify CI green and branch/PR**
  - [ ] Branch as `story/4.1-account-schema-and-sso-aware-repository` from `main`.
  - [ ] Push and confirm both CI jobs (Backend .NET, Frontend Vite/React) green before merging (AD-11). This story makes no frontend changes, so the frontend job should be an unaffected pass-through. **Left for Jack** — per standing project practice, push/PR/CI verification steps are his to run and approve individually, not performed by the dev agent.

## Dev Notes

### Architecture Compliance (must-follow, not optional)

- **AD-19 (z-pax SSO)** — this story implements only the schema + repository slice of AD-19: nullable `PasswordHash`, `SsoProvider`/`SsoSubjectId`, the partial unique index, and `FindBySsoIdentity`/`CreateOrLinkSsoAccount`. **Out of scope for this story:** `ISsoClient`, `HttpClient` calls to z-pax's endpoints, the `state`/CSRF cookie flow, `AuthController`/`AuthService` OAuth endpoints, and any `appsettings.json`/env-var config (`ZPaxSso__*`) — all of that is Story 4.2's job. Don't scope-creep into it.
- **AD-1 (layering)** — this story touches `Entities/`, `Data/`, `Migrations/`, `Repositories/` (new methods), and two narrow guard edits inside existing `Services/` methods. No `Controllers/`, `Dtos/`, or frontend changes. There is still exactly one Auth trio and one Account/Admin trio — `CreateOrLinkSsoAccount`/`FindBySsoIdentity` extend the existing `AccountRepository`, they do not create a new repository.
- **AD-2 (Role enum)** — `CreateOrLinkSsoAccount` never takes a `Role` parameter; the new-account path hardcodes `Role.Customer` exactly like `AdminCreateBarber` hardcodes `Role.Barber` (Story 3.1) — never a string literal.
- **AD-4/NFR4 (testing)** — real temp SQLite via `WebApplicationFactory`/`SqliteApiFactory`, never mocked. No new fixture pattern needed.
- **AD-6 (admin bootstrap boundary)** — unaffected; `AdminBootstrapService` still seeds the one admin account with a real password hash, untouched by this story.
- **AD-7 (int PKs)** — unaffected; no PK changes.
- **AD-15 (Account soft-delete)** — `FindBySsoIdentity`/`FindByEmail`-based lookups inside `CreateOrLinkSsoAccount` both already exclude `DeletedAt != null` rows, consistent with the existing convention — a soft-deleted account can't be SSO-linked into, same as it can't be found by email today.
- **AD-16 (Account optimistic concurrency)** — `RowVersion` and its SQLite trigger must survive this story's migration unmodified (see Task 1's critical note). `CreateOrLinkSsoAccount`'s link path goes through a normal tracked-entity update, so the trigger increments `RowVersion` exactly as it does for every other Account write — no new concurrency mechanism needed or introduced.

### Design Decisions This Story Must Make (epics/architecture leave these open)

- **`CreateOrLinkSsoAccount` keys off *email*, not SSO identity.** `FindBySsoIdentity(provider, subjectId)` is a separate, independently-useful method (Story 4.2's OAuth callback will likely call it first, for "have I seen this exact z-pax subject before" on a repeat login) — but the epics' own AC #3 wording is explicit that `CreateOrLinkSsoAccount`'s create-vs-link decision is made by matching `email`, not by matching `provider`/`subjectId`. Don't conflate the two methods' lookup keys.
- **No `AsNoTracking` re-fetch guard inside `CreateOrLinkSsoAccount`** (unlike `AdminUpdate`/`SoftDelete`'s `EnsureNotCurrentlyAdmin`) — see Task 2's note. The re-fetch pattern defends against a caller passing in an already-mutated in-memory entity; this method takes only primitives, so there's no equivalent attack surface, and adding the pattern anyway would just be needless duplication.
- **Migration correctness is the highest-risk part of this story**, not the repository code. The repository additions are straightforward EF Core; the migration's interaction with the hand-written `trg_Accounts_RowVersion` trigger (Task 1) is the one place a mistake would silently break existing, already-shipped behavior (AD-16) across all three prior epics, not just this story's own scope.

### Testing Requirements

- xUnit.v3 + `WebApplicationFactory` against `SqliteApiFactory`'s real temp SQLite instance — no mocked `DbContext`, no EF in-memory provider (it wouldn't run the actual migration or enforce the new partial unique index).
- `SqliteApiFactory.CreateDbContext()` forces `Program.cs`'s `Database.Migrate()` to run against the temp DB before returning a context — this is what will actually exercise the new migration (including the trigger-survival risk) in every repository test, not just a dedicated migration test.
- No new concurrency test is required for `CreateOrLinkSsoAccount`'s link path — RowVersion/trigger behavior is already covered generically by the existing `Update_increments_RowVersion` test; this story doesn't introduce a new concurrency mechanism to test in isolation.

### Project Structure Notes

- `Entities/Account.cs`, `Data/BarbershopDbContext.cs`, `Repositories/IAccountRepository.cs`, `Repositories/AccountRepository.cs` are all **modified**, not new.
- `Services/AuthService.cs` and `Services/AccountService.cs` get narrow, targeted edits (null-guards) inside existing methods — no new Service methods and no new constructor dependencies in this story (contrast with Story 3.1, which added `AccountService`'s first cross-domain dependency; this story adds none).
- A new `Migrations/<timestamp>_AddSsoFieldsToAccount.cs` + `.Designer.cs` pair, generated by `dotnet ef migrations add` (Task 1) — do not hand-author these from scratch.
- No `Controllers/`, `Dtos/`, `Program.cs`, `appsettings.json`, or frontend changes at all in this story — purely backend data/business layer, matching this project's established "dedicated repository/service story before feature/UI stories" pattern (Stories 1.2, 2.1, 3.1), now extended to Epic 4.

### Established Codebase Patterns to Extend (current state, read in full for this story)

- `IAccountRepository` today: `Create`, `FindByEmail`, `FindById`, `Update`, `AdminExists`, `FindAllByRole`, `Search`, `AdminUpdate`, `SoftDelete`. `FindByEmail`/`FindById` already filter `DeletedAt == null` and `FindByEmail` already trims/lowercases — the two new methods should match this exactly rather than introduce a second normalization convention.
- `AccountRepository.Create(Account account)` is already role-agnostic (used by `AuthService.Register` for customers, `AdminBootstrapService` for the seeded admin, and `AccountService.AdminCreateBarber` for barbers) — this story's new-account path in `CreateOrLinkSsoAccount` is the fourth caller of the same unmodified method, not a reason to add a new repository `Create` overload.
- `Account` entity fields before this story: `Id` (int), `Email`, `PasswordHash` (was non-nullable), `FirstName`, `LastName`, `Role` (enum via `HasConversion<string>()`), `SessionVersion`, `DeletedAt` (nullable), `RowVersion` (concurrency token, default 0, DB-trigger-incremented).
- `AuthService.Login(LoginRequest request)` today: `FindByEmail` → null check → `VerifyHashedPassword` → generate tokens. The null check for `account` and the new null check for `account.PasswordHash` both throw the identical `InvalidCredentialsException` — same generic-failure precedent AD-5's rate-limiter and FR2's no-enumeration guarantee already rely on.
- `AccountService.UpdateOwnProfile` today: loads by id, verifies `currentPassword` against `account.PasswordHash` only when a `newPassword` was supplied, then mutates and calls `accountRepository.Update`. This is the one place in the whole codebase (besides `AuthService.Login`) that *reads* `PasswordHash` rather than only writing it — confirmed by the full-codebase `PasswordHash` grep done for this story.
- Test fixture convention across every existing Account test file: `IDisposable` class holding `private readonly SqliteApiFactory _factory = new();`, no mocks, `MethodName_condition_expectedOutcome` naming, a local `NewAccount(...)` factory helper with named optional parameters. Match this exactly.

### Git Intelligence Summary

Recent commits: `f53dd8e` (sprint-status update for Epic 4) → `5204ab6` (Epic 4 added via course-correct: PRD FR42–46, `ARCHITECTURE-SPINE.md` AD-19, `epics.md` Stories 4.1–4.3) → `db209ee`/`9981b33`/`d6f7b47` (README-only) → `27279cc` (Epic 3 retrospective) → `dc00de4` (Story 3.5 merge). `f53dd8e` is the current tip of `main` and this story's baseline — no Epic 4 code exists yet; this is the first change to the Auth/Account domain since Story 3.5 (2026-08-17). The established rhythm across every prior story: create the story on `main`, implement on `story/{epic}.{story}-{slug}`, PR with a summary, merge once both CI jobs are green, delete the branch.

### References

- [Source: _bmad-output/planning-artifacts/epics.md §Epic 4, §Story 4.1] — story statement, six acceptance criteria, FR coverage map (FR42–FR46)
- [Source: _bmad-output/planning-artifacts/prds/prd-bmad-learning-project-2026-07-21/prd.md §FR42–FR46] — exact FR wording (SSO login option, first-sign-in account creation, existing-account linking, single-admin invariant, session parity)
- [Source: _bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/ARCHITECTURE-SPINE.md#AD-19] — full SSO rule (OAuth flow, env-var secrets, schema shape, admin-invariant enforcement) — note this story implements only the schema/repository portion; the OAuth mechanics belong to Story 4.2
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-08-19.md §5.1, §5.2, §5.3] — the original course-correction proposal this epic/AD/story text was drafted from, including the Decision Log rationale (account linking by email, password survives linking, SSO role ceiling, `state` CSRF amendment) for context on *why* these ACs read the way they do
- [Source: backend/BarbershopApi/Entities/Account.cs, Data/BarbershopDbContext.cs, Repositories/IAccountRepository.cs, AccountRepository.cs, Services/AuthService.cs, AccountService.cs, Migrations/20260728201142_AddAccountEntity.cs] — current implementation this story extends, and the exact `trg_Accounts_RowVersion` SQL that must survive the new migration
- [Source: _bmad-output/implementation-artifacts/3-1-account-repository-admin-operations.md] — repository-first-story precedent this story follows (dedicated data-layer story before feature stories), `AdminCreateBarber`'s "no role parameter" structural-safety pattern, `EnsureNotCurrentlyAdmin`'s re-fetch rationale (and why it doesn't apply here)
- [Source: _bmad-output/implementation-artifacts/deferred-work.md] — confirmed no currently-open item applies to this backend-only, Auth-domain story
- [Source: project-context.md §Technology Stack & Versions; §Language-Specific Rules (C#); §Framework-Specific Rules; §Testing Rules]

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List