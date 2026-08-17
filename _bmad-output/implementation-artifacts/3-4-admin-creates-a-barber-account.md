---
baseline_commit: 7c2bb6f67867f49db2d019243f8be92d6c12ff87
---

# Story 3.4: Admin Creates a Barber Account

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an admin,
I want to create new barber accounts directly,
so that I can add staff without a self-registration flow.

## Acceptance Criteria

1. **Given** the Create Barber button, **when** clicked, **then** a create popup opens with email/first name/last name/password (double-entry, required) fields — no permission selector (FR19).
2. **Given** valid, non-duplicate input, **when** confirmed via the confirm-action popup, **then** a new `Role=Barber` account is created (FR19).
3. **Given** a duplicate email, **when** submitted, **then** it's rejected the same way as registration/edit ("That email is already in use." on the Email field, entered values retained).
4. **Given** an email with no `@` or no domain `.`, **when** submitted, **then** it's rejected the same way as registration/edit (FR19/FR1).
5. **Given** mismatched passwords, **when** submitted, **then** "Passwords do not match" is shown, only those fields clear.

## Tasks / Subtasks

- [x] **Task 1: Add the admin-create endpoint to the existing `AccountController`** (AC: #1–#4)
  - [x] Add `backend/BarbershopApi/Dtos/AdminCreateBarberRequest.cs`:
    ```csharp
    public class AdminCreateBarberRequest
    {
        [Required] [PlausibleEmail] [StringLength(254)]
        public string Email { get; set; } = string.Empty;

        [Required] [StringLength(100)] [RegularExpression(@"(?s).*\S.*", ErrorMessage = "This field cannot be blank.")]
        public string FirstName { get; set; } = string.Empty;

        [Required] [StringLength(100)] [RegularExpression(@"(?s).*\S.*", ErrorMessage = "This field cannot be blank.")]
        public string LastName { get; set; } = string.Empty;

        [Required] [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
        [StringLength(128)]
        [RegularExpression(@"^\S+$", ErrorMessage = "Password cannot contain spaces.")]
        public string Password { get; set; } = string.Empty;
    }
    ```
    Mirrors `RegisterRequest`'s attributes verbatim (same email/password/name validation contract as self-registration — this is also an account-creation DTO, just admin-driven). No `Role` field at all — enforced by the method shape itself, not a runtime check (see `AccountService.AdminCreateBarber`'s existing signature below). **This DTO is what resolves the `AdminCreateBarber` half of `deferred-work.md`'s "No null-guarding in `AdminCreateBarber`/`AdminUpdateAccount`" item** (the `AdminUpdateAccount` half was already resolved by Story 3.3's `AdminUpdateAccountRequest`) — `[ApiController]`'s automatic model validation now rejects a null/blank/malformed email, blank name, or short/whitespace-containing password with a 400 before `AccountService.AdminCreateBarber` ever runs.
  - [x] Add `[HttpPost]` to `AccountController` (`POST /api/account`), gated `[Authorize(Roles = "Admin")]` — same attribute-based gating precedent `Search`/`AdminUpdate` established. No route conflict with the existing `PUT api/account/me` or `PUT api/account/{id:int}` actions (different HTTP verb, same base route).
    - Body:
      ```csharp
      [HttpPost]
      [Authorize(Roles = "Admin")]
      public async Task<IActionResult> AdminCreate(AdminCreateBarberRequest request)
      {
          try
          {
              var created = await accountService.AdminCreateBarber(request.Email, request.FirstName, request.LastName, request.Password);
              return StatusCode(201, new AccountSummary(created.Id, created.Email, created.FirstName, created.LastName, created.Role));
          }
          catch (InvalidPasswordException) { return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Password must be at least 8 characters and cannot contain spaces."); }
          catch (DuplicateEmailException) { return Problem(statusCode: StatusCodes.Status409Conflict, title: "That email is already in use."); }
          catch (Exception) { return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Something went wrong. Please try again."); }
      }
      ```
    - **201 status code, matching `AuthController.Register`'s exact precedent** for "a new account was created" (not 200 — this creates a resource, `AdminUpdate`/`Search` don't).
    - **Reuse `AccountSummary` (Story 3.2) for the response — do not add a new DTO.** Same "admin's view of an account" shape `AdminUpdate` already reuses it for.
    - `DuplicateEmailException` → 409 and `InvalidPasswordException` → 400, matching `AdminUpdate`'s and `AuthController.Register`'s existing mappings for the same exception types — don't invent new status codes for exceptions that already have an established mapping elsewhere in this codebase.
  - [x] **No `Services`/`Repositories` changes.** `AccountService.AdminCreateBarber(string email, string firstName, string lastName, string password)` already exists and is fully tested from Story 3.1 (`AccountService.cs`) — duplicate-email check, `Role.Barber` hardcoded with no role parameter at all (AD-6's "never a second way to mint an admin" guarantee), password hashing, and `ValidatePassword` (min 8 chars, no whitespace) already wired in. This story is Controller + DTO + frontend only, exactly like Stories 3.2/3.3 were for their respective endpoints.
- [x] **Task 2: `AccountControllerTests.cs` additions** (AC: #1–#4)
  - [x] `AdminCreateBarber_as_admin_creates_account_and_returns_summary` — POST valid email/first/last/password as an admin, assert 201 and the returned `AccountSummary` has `Role == Role.Barber` and the submitted email/first/last.
  - [x] `AdminCreateBarber_with_duplicate_email_returns_409`.
  - [x] `AdminCreateBarber_with_implausible_email_returns_400` (e.g. `"testbademail"`, no `@`/domain `.`).
  - [x] `AdminCreateBarber_with_blank_first_name_returns_400`.
  - [x] `AdminCreateBarber_with_blank_last_name_returns_400`.
  - [x] `AdminCreateBarber_with_short_password_returns_400` (< 8 chars).
  - [x] `AdminCreateBarber_with_password_containing_spaces_returns_400`.
  - [x] `AdminCreateBarber_as_non_admin_returns_403` — `[Theory]` over `Role.Customer` and `Role.Barber`, same shape as `Search_as_non_admin_returns_403`/`AdminUpdate_as_non_admin_returns_403`.
  - [x] `AdminCreateBarber_without_access_token_returns_401`.
  - [x] Reuse `RoleGatingTests.RegisterAndLoginAs` for the admin caller, same as every other `AccountControllerTests` admin-only test — do not re-derive the register→promote→login dance again.
- [x] **Task 3: `createBarberAccount` in `AccountApi.js`** (AC: #1–#4)
  - [x] Add `export async function createBarberAccount(accessToken, { email, firstName, lastName, password })` — `POST /api/account`, `Content-Type: application/json`, `credentials: 'include'` (AD-13), body `{ email, firstName, lastName, password }`. Same try/catch/`response.json().catch(() => null)` shape as `adminUpdateAccount`/`updateAccount` — mirror it directly.
  - [x] Success envelope: `{ ok: true, account: body }` (body is the `AccountSummary` JSON from the 201 response: `{ id, email, firstName, lastName, role }`, `role: "Barber"`).
- [x] **Task 4: "Create Barber" button and create-account popup in `AdminPanel.jsx`** (AC: #1–#5)
  - [x] Add a "Create Barber" button, always visible on the page (not gated behind a search) — placed above/beside the search form. Neither `epics.md` nor the UX docs specify exact placement beyond "Admin Panel, 'Create Barber' action"; always-visible is the only placement consistent with the action being independent of any search result.
  - [x] **Compose the shared `Modal` component directly for this popup — do not hand-roll an overlay like the edit popup does.** The edit popup's hand-rolled `<div>` overlay exists *only* because its Permission field is a Radix `Select` nested inside a Radix `Dialog`, which deadlocks in this project's jsdom test environment (logged in `deferred-work.md`, flagged for this story to check). **This popup has no permission selector at all (AC #1) — no `SelectDropdown`, no conflict — so `Modal` composes normally here**, exactly as `DESIGN.md`'s `admin-account-popup` token originally specified ("Field layout inside `{components.modal}` for both the edit and create variants"). Using `Modal` also means initial focus, Tab-trap, and focus-return come for free from Radix `Dialog` — none of the hand-rolled a11y code the edit popup needed.
  - [x] State: `createOpen` (bool), `createEmail`, `createFirstName`, `createLastName`, `createPassword`, `createConfirmPassword`, `createFieldErrors` (object, same `{FieldName: [msg]}` shape as `editFieldErrors`), `createPasswordError` (string), `createError` (string), `isCreating` (bool), and a separate `createConfirmOpen` (bool) for this flow's own `ConfirmPopup` instance.
    - **A second, independent `ConfirmPopup` instance, not a third `pendingAction` branch on the existing edit-popup one.** The existing `confirmOpen`/`pendingAction`/`handleConfirmEdit` machinery is scoped to `editingAccount` (an existing row being edited) — creation has no target account and no relationship to the edit popup's state at all. Reusing that machinery would mean threading a third, unrelated branch through a function whose every existing branch reads `editingAccount.*`; a dedicated `createConfirmOpen` + `handleConfirmCreate` pair is the minimal, correct addition (`Account.jsx`'s single shared `ConfirmPopup` only makes sense there because both of its flows target the *same* account — that precedent doesn't apply here).
  - [x] `handleOpenCreate()`: resets all `create*` state to empty/false, opens `createOpen`.
  - [x] `handleCreatePasswordChange`/`handleCreateConfirmPasswordChange`: strip whitespace on input the same way `Register.jsx`'s `stripWhitespace` does (password fields never accept spaces server-side either).
  - [x] `handleSaveCreateClick`: clears `createError`/`createFieldErrors`/`createPasswordError`; if `createPassword !== createConfirmPassword`, set `createPasswordError = 'Passwords do not match'`, clear both password fields, and return **without** opening the confirm popup (AC #5) — same shape as `Register.jsx`'s and the edit popup's own password-mismatch guards. Otherwise open `createConfirmOpen`.
  - [x] `handleConfirmCreate` (called by the create `ConfirmPopup`'s `onConfirm`): `setIsCreating(true)`, call `createBarberAccount(user.accessToken, { email: createEmail, firstName: createFirstName, lastName: createLastName, password: createPassword })`.
    - On success (`result.ok`): close the popup (`setCreateOpen(false)`), reset all `create*` fields, show a transient confirmation message (e.g. `createdMessage = 'Barber account created.'`, same pattern as `Account.jsx`'s `savedMessage`). **Do not add the new account to the current `accounts` search-results list** — that list only ever reflects an actual search (Story 3.2's established contract); a freshly created barber has no defined relationship to whatever query is currently displayed. The admin re-searches to see it, same as they would after creating via any other means.
    - On `status === 401`: `logout()` + navigate to `/login` with the same session-expired message every other authenticated page in this codebase uses.
    - On `status === 409` (duplicate email — the only 409 this endpoint can return): set `createFieldErrors.Email` to `[result.problem.title]`, keep the popup open with entered values (AC #3).
    - On `status === 400` with `result.problem?.errors`: merge into `createFieldErrors` (same PascalCase `Email`/`FirstName`/`LastName`/`Password` shape the DTO's validation attributes produce).
    - Any other failure: `createError = 'Something went wrong. Please try again.'`.
  - [x] `handleCancelCreate`: closes `createOpen` (guarded against `isCreating`, matching the edit popup's `handleCancelPopup` in-flight guard) and resets all `create*` state — `Modal`'s own `onOpenChange`/`Esc`/outside-click already route here.
  - [x] Popup content: `Input` for Email (`error={createFieldErrors.Email?.[0]}`), First Name (`error={createFieldErrors.FirstName?.[0]}`), Last Name (`error={createFieldErrors.LastName?.[0]}`), `Input type="password"` × 2 labeled **"Password" / "Confirm Password"** (not "New Password" — this creates an account, it isn't changing one; mirrors `Register.jsx`'s exact labeling, the closest precedent since this is also an account-creation flow) with `error={createPasswordError || createFieldErrors.Password?.[0]}` on the first and no error prop needed on the second (matches `Register.jsx`'s pattern of putting the shared password error on both — actually mirror `Register.jsx` exactly: put `passwordError` on *both* password `Input`s, same as it does). All inputs `disabled={isCreating}`. Footer: "Create" (primary) → `handleSaveCreateClick`, "Cancel" (secondary) → `handleCancelCreate`, both `disabled={isCreating}`.
  - [x] Render the create `ConfirmPopup` (`destructive={false}`, message e.g. "Create this barber account?", `onConfirm={handleConfirmCreate}`), separate from the existing edit-flow `ConfirmPopup`.
  - [x] Add `.admin-create-popup` styles to `AdminPanel.css` following `DESIGN.md`'s `{components.admin-account-popup}` token — same `field-gap`/`section-gap`/`footer-gap` variables (`--spacing-4`/`--spacing-6`/`--spacing-3`) the edit popup's CSS already uses; since this popup composes `Modal` (which already provides its own overlay/panel/shadow/radius via `Modal.css`), only the internal field-stack/footer layout needs new rules — no new overlay/panel/shadow declarations.
- [x] **Task 5: `AdminPanel.test.jsx` additions** (AC: #1–#5)
  - [x] Extend the existing `vi.mock('../api/AccountApi')` block to also stub `createBarberAccount: vi.fn()`.
  - [x] "Create Barber" button is visible on page load (before any search) and opens the create popup with empty Email/First Name/Last Name/Password/Confirm Password fields.
  - [x] Entering mismatched passwords and clicking "Create" shows "Passwords do not match", clears both password fields, and does **not** open the confirm popup (`createBarberAccount` not called).
  - [x] Submitting valid, matching input (Create → Confirm) calls `createBarberAccount` with the entered email/first/last/password, closes the popup, and shows a confirmation message.
  - [x] A `409` "That email is already in use." response shows that message on the Email field and keeps the popup open with the entered values.
  - [x] A `400` response with `problem.errors` (e.g. implausible email, blank first name) surfaces the corresponding field error and keeps the popup open.
  - [x] A `401` response logs out and navigates to `/login` with the session-expired message.
  - [x] Clicking the popup's "Cancel" (or pressing Escape) closes it without calling `createBarberAccount`, and reopening "Create Barber" afterward shows empty fields again.
  - [x] Creating a barber does **not** add a row to the currently displayed search results (assert the results list, if a search was already run, is unchanged after a successful create).
- [x] **Task 6: Delete `BarberSeedService` (per the standing Epic-2 deferred-work item and sprint-status action item)**
  - [x] Delete `backend/BarbershopApi/Services/BarberSeedService.cs` entirely — it was throwaway dev-only scaffolding explicitly created as a stand-in until this story shipped a real barber-creation path (no test coverage exists for it, by design; nothing to migrate).
  - [x] Remove its `Program.cs` registration: `builder.Services.AddHostedService<BarberSeedService>();` (currently line 47, immediately after `AdminBootstrapService`'s registration).
  - [x] Confirmed via search: no `appsettings.json`, CI workflow, or other doc references `BarberSeed`/`BarberSeed2` env vars outside `BarberSeedService.cs` itself and historical story/retro files (which are left untouched) — a clean two-file removal.
- [x] **Task 7: Check `deferred-work.md`** (retro discipline, per the standing Epic 1 action item still in force)
  - [x] Re-read `deferred-work.md` in full at kickoff.
  - [x] Mark the "No null-guarding in `AdminCreateBarber`/`AdminUpdateAccount`" item's `AdminCreateBarber` half **resolved** by this story's new `AdminCreateBarberRequest` DTO (`[Required]` on Email/FirstName/LastName/Password).
  - [x] Mark the "Nesting a `SelectDropdown` inside `Modal` causes a focus-scope deadlock" item **checked, not applicable** — this popup has no `SelectDropdown`/permission selector at all, so it composes `Modal` normally without hitting that conflict. Note this explicitly rather than silently ignoring the flagged item.
  - [x] Confirm the `BarberSeedService` removal item is **resolved** by Task 6.
  - [x] Confirm the remaining open items (the Tab-trap `isSubmitting` gap, `AdminSoftDeleteAccount` customer-cascade gap, `EnsureNotCurrentlyAdmin` conflation, `AccountController`'s unlogged `catch (Exception)`) are **not applicable** to this story (the first two are scoped to the edit popup/Story 3.5 respectively; the latter two are pre-existing, unrelated to account creation).
- [ ] **Task 8: Verify CI green and branch/PR**
  - [x] Branch as `story/3.4-admin-creates-a-barber-account` from `main`.
  - [ ] Push and confirm both CI jobs (Backend .NET, Frontend Vite/React) green before merging (AD-11).
  - [ ] Open PR and merge to `main` — left for Jack per standing project practice.

### Review Findings

- [x] [Review][Patch] Success message never clears — `createdMessage` isn't reset by `handleOpenCreate`/`handleCancelCreate`, so "Barber account created." persists indefinitely across later popup opens/cancels for the rest of the session [frontend/src/pages/AdminPanel.jsx:304-315,388-401] — fixed: `handleOpenCreate` now resets `createdMessage`
- [x] [Review][Patch] Create-popup error text borrows the edit popup's `.admin-edit-popup__error` CSS class instead of a scoped `.admin-create-popup__error` — no such class exists in AdminPanel.css despite Task 4's styling being scoped to `.admin-create-popup` [frontend/src/pages/AdminPanel.jsx:668] — fixed: added `.admin-create-popup__error` to AdminPanel.css, updated the className
- [x] [Review][Patch] Create-flow `ConfirmPopup`'s `title` and `message` are the identical string ("Create this barber account?"), unlike the sibling edit-flow `ConfirmPopup` which uses a distinct title/message pair [frontend/src/pages/AdminPanel.jsx:727-728] — fixed: `title` changed to "Create Barber?"
- [x] [Review][Patch] `handleOpenCreate` doesn't reset `createConfirmOpen` to `false` — currently unreachable since `ConfirmPopup` always closes itself synchronously on confirm/cancel/escape, but the defensive reset is missing [frontend/src/pages/AdminPanel.jsx:304-315] — fixed: `handleOpenCreate` now resets `createConfirmOpen`
- [x] [Review][Defer] ~~`AccountService.AdminCreateBarber` assigns `Email = email` with no `.Trim()`~~ [backend/BarbershopApi/Services/AccountService.cs:84] — **false positive**, `AccountRepository.Create` already trims+lowercases the email before persisting for every caller; regression test added (`AccountServiceTests.AdminCreateBarber_trims_email`), no fix needed
- [x] [Review][Defer] `POST /api/account` (AdminCreate) has no rate-limiting policy despite setting a new account's password hash [backend/BarbershopApi/Controllers/AccountController.cs] — deferred, mirrors `AuthController.Register`'s existing gap; a codebase-wide policy decision, not a regression from this diff
- [x] [Review][Defer] The create popup's `Modal`-in-`Modal` nesting (create popup + its own `ConfirmPopup`) is confirmed working only under jsdom in `AdminPanel.test.jsx` [frontend/src/pages/AdminPanel.jsx, AdminPanel.test.jsx] — deferred, recommend manual verification in a real browser (focus return, overlay stacking) since jsdom doesn't reproduce Radix's real focus-scope behavior

## Dev Notes

### Architecture Compliance (must-follow, not optional)

- **AD-1 (layering)** — this story adds one action + one DTO to the existing `AccountController`; no new `AdminController`/`AdminService`. `AccountService.AdminCreateBarber` (already built, Story 3.1) is exactly where the business logic belongs — this story only wires an HTTP surface and request-validation layer on top, same shape as Stories 3.2/3.3.
- **AD-2 (role/session liveness)** — `[Authorize(Roles = "Admin")]` on the new action, same proven mechanism as `Search`/`AdminUpdate`.
- **AD-6 (admin bootstrap, do-not-touch boundary)** — `AdminCreateBarber`'s method signature has no `role` parameter at all; this story's DTO/Controller must not add one "for flexibility." That would reopen the exact hole AD-6 closes (a second way to mint an admin account). This is also why deleting `BarberSeedService` (Task 6) is safe and correct now: this endpoint is the one, real, permanent barber-creation path AD-6 anticipated.
- **project-context.md's fixed 401/403 split** — `[Authorize(Roles = "Admin")]` yields this for free, already proven by `RoleGatingTests` against this exact pipeline; no custom status-code branch needed for auth failures.
- **AD-4 (testing)** — backend: xUnit.v3 + `WebApplicationFactory` against real SQLite, no mocking. Frontend: Vitest + RTL + `user-event`, stub `createBarberAccount` directly, no MSW.

### Design Decisions This Story Must Make (epics/architecture/UX leave these open)

- **Route: `POST /api/account`, gated `[Authorize(Roles = "Admin")]`, returning 201.** Neither `epics.md` nor the architecture docs specify an endpoint shape — same gap Stories 3.2/3.3 already navigated. 201 (not 200) matches `AuthController.Register`'s precedent for "a new account resource was created."
- **Reuse `AccountSummary` as the response DTO**, same "admin's view of an account" contract `AdminUpdate` already established reusing it for.
- **New `AdminCreateBarberRequest` DTO** mirroring `RegisterRequest`'s attributes verbatim (same duplication-is-deliberate rationale documented since Story 1.7/3.3) — not a reuse of `RegisterRequest` itself (that DTO is bound to the public, unauthenticated `/api/auth/register` endpoint's semantics) and not a reuse of `AdminUpdateAccountRequest` (whose `Email`/name fields are optional-context-dependent for an *edit*, and which carries a `Role` field this create flow must never have).
- **This popup composes the shared `Modal` component directly — the edit popup's hand-rolled-overlay deviation does not apply here.** That deviation exists specifically because the edit popup's Permission `SelectDropdown` deadlocks Radix `Dialog`'s focus scope under jsdom (`deferred-work.md`). This popup has no permission selector (AC #1 explicitly excludes one), so `Modal` composes cleanly — matching `DESIGN.md`'s original, undeviated `admin-account-popup` token spec ("Field layout inside `{components.modal}` for both the edit and create variants"). Confirm this by construction (no `SelectDropdown` anywhere in this popup) rather than by testing for the jsdom hang — if a future revision of this story ever adds a `SelectDropdown` here, revisit this decision first.
- **A second, independent `ConfirmPopup` instance for the create flow**, not a third branch on the edit popup's shared one — see Task 4 for the reasoning (the edit popup's shared-`ConfirmPopup` precedent only applies when both flows target the same account; create has no target account).
- **"Create Barber" button is always visible**, not gated behind a search — the action is independent of any search result, unlike the edit popup which requires a row to click.
- **No live update to the `accounts` search-results list after a successful create.** The list only ever reflects an actual search (Story 3.2's contract); a newly created barber has no defined relationship to whatever query is currently on screen.
- **Password fields labeled "Password" / "Confirm Password"**, not "New Password" — this is account *creation*, not a password *change*; `Register.jsx` is the closest precedent, not the admin edit-popup's password-change section.
- **Deleting `BarberSeedService` is in scope for this story** (Task 6) — it was explicitly created as throwaway scaffolding standing in for exactly this endpoint (`deferred-work.md` §Deferred from story-2.6-admin-schedule-oversight), not a separate follow-up.

### Testing Requirements

- Backend: xUnit.v3 + `WebApplicationFactory`/`SqliteApiFactory`, real temp SQLite, no mocked `DbContext`. This story's Controller tests are thin HTTP-wiring smoke tests — `AccountService.AdminCreateBarber`'s business logic (duplicate-email check, `Role.Barber` hardcoding, password validation/hashing) is already exhaustively covered at the Service layer by Story 3.1's `AccountServiceTests`; do not re-derive that coverage here.
- Frontend: Vitest + `@testing-library/react` + `user-event`; stub `createBarberAccount` directly via the existing `vi.mock('../api/AccountApi')` block in `AdminPanel.test.jsx`, extending its `searchAccounts`/`adminUpdateAccount` stubs rather than replacing the file's mocking approach.

### Previous Story Intelligence (Story 3.3)

- `AdminPanel.jsx`'s edit popup is hand-rolled (not `Modal`) *specifically* because of a Radix `Select`-inside-`Dialog` focus-scope deadlock under jsdom — this story's popup has no `Select`, so it should use `Modal` normally, not copy the hand-rolled pattern. Copying it here would be over-applying a workaround to a case that doesn't have the underlying problem.
- The established "field errors as `{FieldName: [msg]}`, merged from `problem.errors` on a 400" pattern (`editFieldErrors` in Story 3.3) is the shape to reuse for `createFieldErrors` — don't invent a different error-state shape.
- `isMountedRef`/`disabled={isSubmitting}`-during-in-flight-request patterns are already established in this file (Story 3.2/3.3's reviews) — apply the same to `isCreating` and the create popup's buttons.
- The `result.status === 401` → `logout()` + navigate to `/login` branch is this file's standing 401-handling convention (Story 3.2's review) — `handleConfirmCreate` must follow it identically.

### Git Intelligence Summary

Recent commits: `7c2bb6f` (Story 3.3 merge, current `main` tip) → `b35d6c1` → `d49d9a4` (Story 3.2 merge) → `9b9b946` → `5882f61` (Story 3.1 merge). Established rhythm continues unchanged: create the story on `main`, implement on `story/3.4-admin-creates-a-barber-account`, PR with an additions/fixes/test-count summary, merge once both CI jobs are green, delete the branch — push/PR/CI verification left for Jack. `7c2bb6f` confirms `AccountController` currently has exactly three actions (`UpdateMe`, `Search`, `AdminUpdate`) and `AccountService.AdminCreateBarber` already exists fully tested from Story 3.1 — this story adds the Controller's fourth action on top of it.

### Project Structure Notes

- **Backend — new:** `backend/BarbershopApi/Dtos/AdminCreateBarberRequest.cs`.
- **Backend — modified:** `backend/BarbershopApi/Controllers/AccountController.cs` (new `[HttpPost]` action), `backend/BarbershopApi/Program.cs` (remove `BarberSeedService` registration).
- **Backend — deleted:** `backend/BarbershopApi/Services/BarberSeedService.cs`.
- **Frontend — modified:** `frontend/src/pages/AdminPanel.jsx` (create popup, state, handlers), `frontend/src/pages/AdminPanel.css` (create-popup field/footer layout), `frontend/src/api/AccountApi.js` (new `createBarberAccount` export). No new frontend files — matches Story 3.2's explicit instruction that Stories 3.3–3.5 extend this one page rather than creating separate ones.
- **Tests — modified:** `backend/BarbershopApi.Tests/AccountControllerTests.cs` (new cases only), `frontend/src/pages/AdminPanel.test.jsx` (new cases only). No other test file needs changes (no `BarberSeedService` tests exist to remove — it never had any, by design).
- `_bmad-output/implementation-artifacts/deferred-work.md` — modified (three items marked resolved/not-applicable per Task 7).
- `Program.cs` needs no new DI registrations for the endpoint itself — `IAccountService`/`AccountService` is already `Scoped` and unchanged by this story; the only `Program.cs` change is *removing* the `BarberSeedService` line.

### Established Codebase Patterns to Extend (current state, confirmed by reading the files directly)

- `AccountController` today (post-3.3): `[ApiController] [Route("api/account")] [Authorize]` class-level, `PUT api/account/me` (self-service), `GET api/account/search` and `PUT api/account/{id:int}` (both `[Authorize(Roles = "Admin")]`). This story adds the controller's fourth action, `POST api/account`, its third `[Authorize(Roles = "Admin")]`-gated one.
- `AccountService.AdminCreateBarber(string email, string firstName, string lastName, string password)` (Story 3.1, already fully tested): duplicate-email check via `FindByEmail` → `DuplicateEmailException` (plus the same `SqliteErrorCode: 19` DB-race backstop `AuthService.Register` uses), `ValidatePassword` → `InvalidPasswordException` (min 8 chars, no whitespace — added during Story 3.1's own review), hashes via `IPasswordHasher<Account>`, builds the entity with `Role = Role.Barber` hardcoded (no role parameter exists on this method at all). This story's Controller action is a direct, unmodified pass-through to this exact signature.
- `AuthController.Register` (`AuthController.cs:16-32`) is the closest existing precedent for this story's Controller action: `try`/catch `DuplicateEmailException` → 409, catch-all → 500, `StatusCode(201, ...)` on success. This story's `AdminCreate` action follows the identical shape, with `[Authorize(Roles = "Admin")]` added and one extra `catch (InvalidPasswordException)` → 400 (a case `Register` doesn't need since `RegisterRequest.Password`'s DTO attributes already cover the same rule at the model-binding layer — this story's DTO does too, so `InvalidPasswordException` is a defensive backstop, not the primary path, matching `AdminUpdate`'s identical relationship to its own `[Required]`-validated fields).
- `Register.jsx`'s `handleSubmit` is the closest existing frontend precedent for this story's create-popup submit handler: mismatch check → clear-and-retype, length check, then submit, branching 409/400/other. Mirror its exact copy and field-clearing behavior for the popup's password fields.
- `Modal`/`ConfirmPopup`/`Input`/`Button` (all in `frontend/src/components/`) are already built and used by `Account.jsx` and `ConfirmPopup` itself composes `Modal` — this story's create popup is pure composition of `Modal` + these existing components, no new shared component needed, and (unlike the edit popup) no `SelectDropdown` involved at all.

### References

- [Source: _bmad-output/planning-artifacts/epics.md §Story 3.4 (line 695)] — story statement, five acceptance criteria (verbatim), FR coverage (FR19, FR1)
- [Source: _bmad-output/planning-artifacts/prds/prd-bmad-learning-project-2026-07-21/prd.md §FR1, §FR19] — exact FR wording: plausible-email validation (FR1); admin creates new barber accounts, always `Role.Barber`, no permission selector (FR19)
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-bmad-learning-project-2026-07-23/EXPERIENCE.md — Component Patterns (admin account-create popup, line 69; double-entry password fields, line 80), State Patterns (duplicate email line 102, password mismatch line 105)] — exact field list (email/first/last/password ×2, no permission selector) and copy for duplicate-email/password-mismatch states
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-bmad-learning-project-2026-07-23/DESIGN.md — `admin-account-popup` token (field/section/footer-gap, explicitly "for both the edit and create variants" inside `{components.modal}`), `confirm-popup` token] — this popup's spacing tokens and its (undeviated, unlike the edit popup) `Modal`-composition basis
- [Source: _bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/ARCHITECTURE-SPINE.md#AD-1, #AD-2, #AD-6] — layering, role/session liveness mechanism, admin-bootstrap do-not-touch boundary (why `AdminCreateBarber` has no role parameter, and why deleting `BarberSeedService` now is correct)
- [Source: backend/BarbershopApi/Controllers/AccountController.cs, AuthController.cs; Services/AccountService.cs, IAccountService.cs] — current controller shape this story extends; `AdminCreateBarber`'s existing, already-tested signature; `Register`'s 201/409/500 precedent this story's new action mirrors
- [Source: backend/BarbershopApi/Dtos/RegisterRequest.cs, AdminUpdateAccountRequest.cs, PlausibleEmailAttribute.cs, AccountSummary.cs] — exact validation-attribute and DTO-reuse precedents `AdminCreateBarberRequest` follows
- [Source: backend/BarbershopApi/Services/BarberSeedService.cs, Program.cs:47] — the dev-only scaffolding this story deletes, and its one registration line
- [Source: frontend/src/pages/AdminPanel.jsx, Register.jsx, Account.jsx; components/Modal.jsx, ConfirmPopup.jsx, Input.jsx, Button.jsx; api/AccountApi.js] — existing page/component/API patterns this story composes; `AdminPanel.jsx`'s current hand-rolled edit-popup pattern (explicitly NOT the template for this story's popup) vs. `Modal`'s direct-composition pattern (which IS the template here)
- [Source: backend/BarbershopApi.Tests/AccountControllerTests.cs, RoleGatingTests.cs] — `RegisterAndLoginAs` helper, existing `Search_as_non_admin_returns_403`/`AdminUpdate_as_non_admin_returns_403` shape this story's own non-admin test follows
- [Source: _bmad-output/implementation-artifacts/3-1-account-repository-admin-operations.md, 3-2-admin-account-search.md, 3-3-admin-edits-an-account.md] — predecessor stories: `AdminCreateBarber`'s exact contract and already-complete Service-layer test coverage (3.1); `AdminPanel.jsx`'s established page conventions, 401-handling, `isMountedRef`/`disabled`-during-loading patterns (3.2); the hand-rolled-overlay deviation and exactly why it does NOT apply to this story's popup (3.3)
- [Source: _bmad-output/implementation-artifacts/deferred-work.md §Deferred from story-3.3-admin-edits-an-account, §Deferred from code review of story-3.1, §Deferred from story-2.6-admin-schedule-oversight] — the `SelectDropdown`-in-`Modal` conflict this story confirms doesn't apply; the `AdminCreateBarber` null-guarding item this story resolves; the `BarberSeedService` removal this story performs
- [Source: project-context.md §Language-Specific Rules (C#, 401/403 split); §Testing Rules; §Naming; §Code organization]

## Dev Agent Record

### Agent Model Used

Amelia (claude-sonnet-5), via the `bmad-dev-story` workflow.

### Debug Log References

None — no blocking failures encountered. All red→green cycles resolved on the first implementation pass; no HALT conditions triggered.

### Completion Notes List

- Task 1: Added `AdminCreateBarberRequest` DTO (mirrors `RegisterRequest`'s validation attributes verbatim) and `AccountController.AdminCreate` (`POST /api/account`, `[Authorize(Roles = "Admin")]`, 201/400/409/500 mapping identical to `AuthController.Register`'s precedent plus the `InvalidPasswordException` → 400 backstop). No `Services`/`Repositories` changes — `AccountService.AdminCreateBarber` already existed, fully tested, from Story 3.1.
- Task 2: Added 10 new `AccountControllerTests.cs` cases (all listed sub-bullets) plus an `AdminCreateRequest` HTTP-request-builder helper alongside the existing `AdminUpdateRequest`/`SearchRequest` ones. Confirmed RED (all 10 failing with 404, endpoint didn't exist yet) before implementing Task 1, then GREEN (all 10 passing) after. Full backend suite: 241/241 passing, no regressions.
- Task 3: Added `createBarberAccount` to `AccountApi.js`, matching `adminUpdateAccount`'s exact fetch/error-envelope shape. No dedicated unit test file for this function, consistent with the existing pattern (`adminUpdateAccount`/`searchAccounts` also have no direct tests — exercised via the page-level `AdminPanel.test.jsx` mocks instead).
- Task 4: Added the "Create Barber" button (always visible, above the search form) and the create-account popup to `AdminPanel.jsx`, composing the shared `Modal` component directly (no `SelectDropdown` in this popup, so the edit popup's hand-rolled-overlay deadlock workaround doesn't apply — confirmed by Task 5's tests, which exercise a `Modal`-in-`Modal` (create popup + its own `ConfirmPopup`) nesting not previously exercised elsewhere in this codebase). Added `.admin-create-popup`/`.admin-panel__actions`/`.admin-panel__created-message` styles to `AdminPanel.css`.
- Task 5: Added 8 new `AdminPanel.test.jsx` cases under a `describe('Create Barber', ...)` block (all listed sub-bullets, one test per bullet plus the "Create Barber" visibility/empty-fields case). All 36 `AdminPanel.test.jsx` tests pass; full frontend suite: 182/182 passing, no regressions. `eslint .` and `prettier --check .` both clean after an initial `prettier --write` pass on the two touched files.
- Task 6: Deleted `backend/BarbershopApi/Services/BarberSeedService.cs` and its `Program.cs` `AddHostedService<BarberSeedService>()` registration. Confirmed via search: no remaining `BarberSeed` references anywhere in source. Backend suite re-run clean (241/241) after removal.
- Task 7: Re-read `deferred-work.md` in full. Marked the `AdminCreateBarber` half of the null-guarding item resolved; marked the `SelectDropdown`-in-`Modal` item checked/not-applicable (with the Dialog-in-Dialog nesting note); marked the `BarberSeedService` removal item resolved; added an explicit "Checked during story-3.4" section confirming the four remaining open items are not applicable to this story. Also flipped the corresponding sprint-status.yaml action item from `open` to `done`.
- Task 8: Already on `story/3.4-admin-creates-a-barber-account` (branched from `main` at story-creation time, confirmed via `git status`/`git log`). Push, CI verification, and PR/merge intentionally left for Jack per standing project practice (and this session's standing instruction to pause before commit/push/PR) — not executed by this dev session.

### File List

**Backend — new:**
- `backend/BarbershopApi/Dtos/AdminCreateBarberRequest.cs`

**Backend — modified:**
- `backend/BarbershopApi/Controllers/AccountController.cs`
- `backend/BarbershopApi/Program.cs`

**Backend — deleted:**
- `backend/BarbershopApi/Services/BarberSeedService.cs`

**Backend — tests modified:**
- `backend/BarbershopApi.Tests/AccountControllerTests.cs`

**Frontend — modified:**
- `frontend/src/pages/AdminPanel.jsx`
- `frontend/src/pages/AdminPanel.css`
- `frontend/src/api/AccountApi.js`

**Frontend — tests modified:**
- `frontend/src/pages/AdminPanel.test.jsx`

**Docs — modified:**
- `_bmad-output/implementation-artifacts/deferred-work.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Change Log

- 2026-08-14: Implemented Story 3.4 (Tasks 1-7) — `AccountController` gained its fourth action, `POST /api/account`, gated `[Authorize(Roles = "Admin")]`, returning 201, backed by the new `AdminCreateBarberRequest` DTO; no `Service`/`Repository` changes needed (Story 3.1 already built and tested `AccountService.AdminCreateBarber`). Frontend gained the "Create Barber" button and create-account popup in `AdminPanel.jsx` (composing the shared `Modal` component directly, plus its own independent `ConfirmPopup` instance — the first place in this codebase where a `Modal` nests inside another `Modal`, confirmed working under jsdom) and `createBarberAccount` in `AccountApi.js`. Deleted the throwaway `BarberSeedService` dev-seeding scaffolding and its `Program.cs` registration now that this real creation path exists. Backend suite green (241/241); frontend suite green (182/182, 20 files); ESLint and Prettier clean. Updated `deferred-work.md`: resolved the `AdminCreateBarber` null-guarding item, confirmed the `SelectDropdown`-in-`Modal` conflict doesn't apply here, resolved the `BarberSeedService` removal item, and confirmed the remaining open items aren't applicable to this story. Task 8 (push/PR/CI verification) intentionally left for Jack per standing project practice.
