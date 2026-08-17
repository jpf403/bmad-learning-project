---
baseline_commit: 5e227c525601f4e6939615fa3ed722677774e4ba
---

# Story 3.5: Admin Deletes an Account

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an admin,
I want to delete a customer or barber account,
so that I can remove accounts that are no longer needed.

## Acceptance Criteria

1. **Given** an account in the edit popup, **when** Delete is clicked, **then** a confirm-action popup (destructive Confirm) appears before the account is actually deleted (FR40).
2. **Given** a confirmed delete, **when** it completes, **then** the account is soft-deleted (`DeletedAt` set) — never a hard row delete — and its email becomes registerable again immediately (FR40, AD-15).
3. **Given** a deleted barber account, **when** the deletion completes, **then** that barber's future appointments are cancelled the same way a demotion cascades them; past appointments retained as history (FR40).
4. **Given** the single admin account, **when** any delete action is attempted against it, **then** it's rejected (FR34).
5. **Given** a deleted account, **when** it attempts to sign in afterward, **then** auth treats it identically to "account does not exist" (AD-15).
6. **Given** a deleted customer account with future appointments, **when** the deletion completes, **then** those appointments are also cancelled — closing a real display bug (a barber's schedule would otherwise render a blank customer name for an appointment whose customer no longer resolves via `FindById`) that this story must not ship with. Not in `epics.md`'s literal AC text; flagged explicitly in `deferred-work.md` for this story to resolve. Past/Finished customer appointments are retained as history, same as the barber case.
7. **Given** two conflicting operations on the same account (an admin delete racing another admin's edit, or racing the holder's own self-edit), **when** both are submitted, **then** the first commit wins and the second gets a conflict error — the existing `RowVersion` mechanism (AD-16), same guarantee AC #8 of Story 3.3 already proved for edits, now exercised for delete too (NFR2, FR41).

## Tasks / Subtasks

- [x] **Task 1: Add the admin-delete endpoint to the existing `AccountController`** (AC: #1, #2, #4, #7)
  - [x] Add `[HttpDelete("{id:int}")]` to `AccountController` (`DELETE /api/account/{id}`), gated `[Authorize(Roles = "Admin")]` — the controller's fourth `[Authorize(Roles = "Admin")]`-gated action and its fifth action overall (after `UpdateMe`, `Search`, `AdminUpdate`, `AdminCreate`). No route conflict: `DELETE` is a distinct verb from the existing `PUT api/account/{id}` on the identical route template.
    - Body:
      ```csharp
      [HttpDelete("{id:int}")]
      [Authorize(Roles = "Admin")]
      public async Task<IActionResult> AdminDelete(int id)
      {
          var admin = (Account)HttpContext.Items["Account"]!;
          try
          {
              await accountService.AdminSoftDeleteAccount(id, admin.Id);
              return NoContent();
          }
          catch (AccountNotFoundException) { return Problem(statusCode: StatusCodes.Status404NotFound, title: "Account not found."); }
          catch (AdminAccountProtectedException) { return Problem(statusCode: StatusCodes.Status400BadRequest, title: "The admin account cannot be deleted."); }
          catch (AccountConflictException) { return Problem(statusCode: StatusCodes.Status409Conflict, title: "This account was changed elsewhere. Refresh and try again."); }
          catch (Exception) { return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Something went wrong. Please try again."); }
      }
      ```
    - **204 No Content on success, matching `BookingController.CancelBooking`'s exact precedent** for a state-changing action with nothing to return in the body (not 200 with a body — there's no updated resource to hand back, unlike `AdminUpdate`/`AdminCreate`).
    - `AccountNotFoundException` → 404, `AdminAccountProtectedException` → 400, `AccountConflictException` → 409: **identical mappings to `AdminUpdate`'s existing precedent for the same three exception types** — reuse them verbatim, don't invent new copy or status codes for exceptions that already have an established mapping in this same controller. Only the `AdminAccountProtectedException` message text changes (`"cannot be deleted"` vs. `AdminUpdate`'s `"cannot be edited"`) to match the actual action.
    - No request body/DTO needed — the id in the route is the only input.
  - [x] **Extend `AccountService.AdminSoftDeleteAccount` and its Repository layer to also cascade-cancel a deleted customer's own future appointments** (AC: #6) — see Task 2. This is the one place this story deviates from Stories 3.2–3.4's "Controller + DTO + frontend only" pattern, and it's necessary: `AccountService.AdminSoftDeleteAccount` (built in Story 3.1) already cascades for `Role.Barber` but does nothing for `Role.Customer`, and `deferred-work.md`'s "Deferred from: code review of story-3.1" section explicitly flags this as a gap for this story to close before shipping the delete UI.
- [x] **Task 2: Extend `BookingService`/`IBookingService` and `AppointmentRepository`/`IAppointmentRepository` with a customer-side mirror of the existing barber cascade** (AC: #6)
  - [x] Add `Task<List<Appointment>> FindFutureByCustomer(int customerId, DateTime nowEst)` to `IAppointmentRepository`/`AppointmentRepository` — copy `FindFutureByBarber`'s exact query shape (`backend/BarbershopApi/Repositories/AppointmentRepository.cs:41-52`), filtering on `CustomerId` instead of `BarberId`.
  - [x] Add `Task CancelAllFutureForCustomer(int customerId, int callerAccountId, Role callerRole, DateTime? now = null)` to `IBookingService`/`BookingService` — copy `CancelAllFutureForBarber`'s exact body (`backend/BarbershopApi/Services/BookingService.cs:169-193`), calling `FindFutureByCustomer` instead of `FindFutureByBarber`; same per-appointment try/catch swallowing `AppointmentAlreadyCancelledException`/`AppointmentAlreadyFinishedException` (an appointment finishing or being cancelled by someone else mid-cascade is not a failure) and any other exception (one bad appointment must not abort the cascade for the rest, same rationale already documented on the barber method).
  - [x] In `AccountService.AdminSoftDeleteAccount` (`backend/BarbershopApi/Services/AccountService.cs:154-172`), after the `SoftDelete` call commits, branch on the account's role: `Role.Barber` → `bookingService.CancelAllFutureForBarber` (existing, unchanged), `Role.Customer` → `bookingService.CancelAllFutureForCustomer` (new). No branch needed for `Role.Admin` (unreachable — `SoftDelete` already throws `AdminAccountProtectedException` for that role before this point).
  - [x] **No changes to `Cancel`'s authorization logic or `AD-17`'s shared-read-path guarantee.** Both new methods are pure plumbing that funnel into the existing, unmodified `Cancel(appointmentId, callerAccountId, callerRole, now)` — the acting admin's own id/role, exactly as `CancelAllFutureForBarber` already does. This is not a new cancellation mechanism, just the barber cascade's exact shape applied to the other role that can hold appointments.
- [x] **Task 3: `AccountServiceTests.cs` additions** (AC: #3, #6)
  - [x] `AdminSoftDeleteAccount_on_customer_cancels_future_appointments_but_retains_past` — mirror the existing `AdminSoftDeleteAccount_on_barber_cancels_future_appointments_but_retains_past` test (`backend/BarbershopApi.Tests/AccountServiceTests.cs:361-397`) exactly, but seed the future/past appointments under the customer role instead and delete the customer, not the barber.
  - [x] The existing `AdminSoftDeleteAccount_on_barber_cancels_future_appointments_but_retains_past`, `_on_admin_account_throws_AdminAccountProtectedException`, and `_on_stale_RowVersion_throws_AccountConflictException` tests already exist from Story 3.1 and need no changes — this story only adds the customer-side case.
- [x] **Task 4: `AccountControllerTests.cs` additions** (AC: #1, #2, #4, #5, #7)
  - [x] Add an `AdminDeleteRequest(int id, string? accessToken = null)` HTTP-request-builder helper alongside the existing `UpdateMeRequest`/`SearchRequest`/`AdminUpdateRequest`/`AdminCreateRequest` ones (`backend/BarbershopApi.Tests/AccountControllerTests.cs:42-84`), building a plain `HttpMethod.Delete` request to `/api/account/{id}` with no body.
  - [x] `AdminDelete_as_admin_soft_deletes_account_and_returns_204` — DELETE a customer/barber account as an admin, assert 204, then verify via a fresh `AccountRepository.FindById` that it now returns `null` (soft-deleted rows are excluded, per the existing `FindById`/`FindByEmail` query shape) while a direct `DbContext` read (bypassing the repository's `DeletedAt == null` filter) confirms the row still exists with `DeletedAt` set — proving soft-delete, not a hard row delete (AC #2).
  - [x] `AdminDelete_makes_the_deleted_accounts_email_registerable_again` — delete an account, then call `POST /api/auth/register` with that same email and assert 201 (AC #2's "email becomes registerable again immediately"). Reuses the existing `AuthControllerTests`-style register flow via `RegisterRequest`/`AuthController`, called directly against the same `WebApplicationFactory` client — no new registration logic to write, this is a thin end-to-end proof.
  - [x] `AdminDelete_a_deleted_account_cannot_sign_in` — delete an account, then `POST /api/auth/login` with its original credentials and assert the same generic 401 "Invalid email or password" any nonexistent-account login gets (AC #5) — reuse `AuthController.Login`'s existing behavior, don't special-case deleted accounts differently from "account doesn't exist."
  - [x] `AdminDelete_on_missing_account_returns_404`.
  - [x] `AdminDelete_on_the_admin_account_returns_400` — target the seeded/promoted admin's own id, same shape as `AdminUpdate_on_the_admin_account_returns_400`.
  - [x] `AdminDelete_on_stale_RowVersion_returns_409` — two independent `DbContext`/repository/service instances loading the same row before either writes, same deterministic pattern as `AdminUpdate_on_stale_RowVersion_returns_409`/`UpdateMe_on_stale_RowVersion_returns_409` above and `AccountServiceTests.AdminSoftDeleteAccount_on_stale_RowVersion_throws_AccountConflictException`'s existing Service-level equivalent — never a real concurrent-HTTP race (standing practice since Stories 1.2/1.7, reused in every Epic 3 story so far).
  - [x] `AdminDelete_demoting_a_barber_cancels_future_appointments_via_http` — thin Controller-level smoke test mirroring `AdminUpdate_demoting_barber_to_customer_cancels_future_appointments_via_http`'s shape (seed a barber + one far-future and one far-past appointment via `AppointmentRepository.Create`, call the real `DELETE` endpoint, assert via a fresh repository read that the future one is cancelled and the past one isn't). Task 3 already proves this exhaustively at the Service layer — this test only proves the HTTP path wires to it correctly.
  - [x] `AdminDelete_deleting_a_customer_cancels_their_future_appointments_via_http` — same shape as the previous test, but deleting the customer side of the appointment instead of the barber side (AC #6's HTTP-level proof).
  - [x] `AdminDelete_as_non_admin_returns_403` — `[Theory]` over `Role.Customer` and `Role.Barber`, same shape as `AdminUpdate_as_non_admin_returns_403`/`Search_as_non_admin_returns_403`/`AdminCreateBarber_as_non_admin_returns_403`.
  - [x] `AdminDelete_without_access_token_returns_401`.
  - [x] Reuse `RoleGatingTests.RegisterAndLoginAs` for admin/target setup, same as every other `AccountControllerTests` admin-only test — do not re-derive the register→promote→login dance a fourth time in this file.
- [x] **Task 5: `deleteAccount` in `AccountApi.js`** (AC: #1, #2, #7)
  - [x] Add `export async function deleteAccount(accessToken, accountId)` — `DELETE /api/account/${accountId}`, `credentials: 'include'` (AD-13), `Authorization: Bearer` header, no body. On a 204 (no JSON body to parse — unlike every other function in this file, don't call `response.json()` on a successful response, it will throw on an empty body), return `{ ok: true }`. On failure, same `response.json().catch(() => null)` / `{ ok: false, status, problem }` envelope shape as `adminUpdateAccount`/`createBarberAccount`, mirrored for the error path only.
- [x] **Task 6: "Delete" button inside the existing admin edit popup in `AdminPanel.jsx`** (AC: #1, #2, #4, #7)
  - [x] **The Delete button lives inside the existing hand-rolled edit popup — do not build a separate popup or a third `ConfirmPopup` instance.** `EXPERIENCE.md`'s Component Patterns table is explicit: "Admin account-edit popup ... Save routes through the confirm-action popup (non-destructive Confirm); Delete routes through it too (destructive Confirm)" — Delete is a third action inside the *same* popup Stories 3.3 built, not a new surface. Story 3.3's own Dev Notes explicitly deferred this ("No Delete button in this popup ... Story 3.5 ... explicitly owns that").
  - [x] **Extend the existing `pendingAction` state from `'details' | 'password'` to `'details' | 'password' | 'delete'`, reusing the single shared `ConfirmPopup` instance already in the identity view — do not add a fourth, independent `ConfirmPopup`.** This is the opposite call from Story 3.4's create popup (which correctly needed its *own* independent `ConfirmPopup`, because create has no target account): Delete acts on the exact same `editingAccount` the identity/password saves already target, so it belongs on the same shared instance, distinguished the same way those two already are.
    - The `ConfirmPopup`'s `destructive` prop must become conditional: `destructive={pendingAction === 'delete'}` (currently hardcoded `false` at `frontend/src/pages/AdminPanel.jsx:647-657`, since only non-destructive saves used it before now).
    - `title`/`message` for the delete case: something in the confirm-popup's established voice, e.g. title `"Delete Account?"`, message `"Delete this account? This cannot be undone."` — parallel to Story 3.4's create-flow title/message pair (`"Create Barber?"` / `"Create this barber account?"`), not the identical-string mistake that story's own review caught and fixed.
  - [x] `handleDeleteClick`: clears `editError`, sets `pendingAction = 'delete'`, opens `confirmOpen` — same shape as `handleSaveDetailsClick`, no password-mismatch-style guard needed (there's no input to validate before confirming a delete).
  - [x] Add a "Delete" button (`variant="destructive"`) to the identity view's footer, alongside "Save Changes" and the popup's own "Cancel" — visible whenever `!isAdminAccount` (same guard already used for "Save Changes"/"Change Password", since the admin row renders read-only with none of these actions per Story 3.3's existing "admin account cannot be edited" branch — extend that same read-only guard to cover Delete too, rather than needing a second, separate check for AC #4's "admin can never be deleted"). Disabled while `isSubmitting`, matching every other button in this popup.
  - [x] Extend `handleConfirmEdit` (`frontend/src/pages/AdminPanel.jsx:224-300`) with a third branch on `pendingAction === 'delete'`: call the new `deleteAccount(user.accessToken, editingAccount.id)` instead of `adminUpdateAccount`.
    - On success: remove the deleted account from `accounts` (`setAccounts((current) => current.filter((a) => a.id !== editingAccount.id))`) and close the popup (`setEditingAccount(null)`) — unlike a details/password save, there's no updated account to fold back into the row, the row simply disappears from the current search results.
    - On `status === 401`: identical `logout()` + navigate-to-`/login` branch every other flow in this file already uses — this branch is already shared/unconditional in the existing code, no new logic needed here beyond making sure the delete branch reaches it.
    - On `status === 409`: falls through to the existing generic conflict handling (`setEditError(result.problem?.title ?? 'This account was changed elsewhere. Refresh and try again.')`) — already unconditional on `pendingAction`, no new branch needed (AC #7).
    - On `status === 400` (the admin-protected-account case — defensive only, since the UI already hides Delete for admin rows) or any other failure: fall through to the existing generic `editError` fallback — don't add a delete-specific error path where the existing generic one already covers it correctly.
  - [x] Add `.admin-edit-popup__delete-button` (or reuse an existing spacing utility class already in `AdminPanel.css`) only if the destructive button needs visual separation from "Save Changes"/"Cancel" in the footer row — check `DESIGN.md`'s `admin-account-popup` token's `footer-gap` spacing first; if the existing `.admin-edit-popup__footer` flex-gap already produces adequate visual separation for a third button, no new CSS is needed.
- [x] **Task 7: `AdminPanel.test.jsx` additions** (AC: #1, #2, #4, #7)
  - [x] Extend the existing `vi.mock('../api/AccountApi')` block to also stub `deleteAccount: vi.fn()`.
  - [x] The edit popup (opened via `searchAndOpenEditPopup`) shows a "Delete" button in the identity view, alongside "Save Changes" and "Cancel".
  - [x] Clicking "Delete" opens the shared confirm popup with the destructive-styled Confirm button and the delete-specific message — assert via the `ConfirmPopup`'s rendered title/message text, not styling (jsdom doesn't meaningfully assert CSS class-driven color).
  - [x] Confirming a delete (Delete → Confirm) calls `deleteAccount` with the target account's id, closes the edit popup, and removes that account's row from the currently displayed search results.
  - [x] Declining the delete confirm ("Go Back") leaves the account undeleted, the row unchanged, and `deleteAccount` not called — mirror the existing "declining the confirm popup ... leaves the edit unsaved" test's shape (`frontend/src/pages/AdminPanel.test.jsx:483-497`) for the delete case.
  - [x] A `409` conflict response from `deleteAccount` shows the existing refresh-and-retry message and keeps the popup open (reuses the already-existing generic 409 handling — this test proves the delete path reaches it, not that new copy exists).
  - [x] A `401` response from `deleteAccount` logs out and navigates to `/login` with the session-expired message (reuses the existing 401 branch).
  - [x] The Admin-role read-only popup (`ADMIN_ACCOUNT`, existing test at `frontend/src/pages/AdminPanel.test.jsx:538-557`) has no "Delete" button, extending that test's existing assertions rather than writing a new one from scratch (AC #4's UI-level defense-in-depth, on top of the backend's own `AdminAccountProtectedException` guard).
- [x] **Task 8: Check `deferred-work.md`** (retro discipline, per the standing Epic 1 action item still in force)
  - [x] Re-read `deferred-work.md` in full at kickoff.
  - [x] Mark the "`AdminSoftDeleteAccount` on a customer doesn't cascade..." item (under "Deferred from: code review of story-3.1-account-repository-admin-operations") **resolved** by this story's Task 2 (`CancelAllFutureForCustomer`).
  - [x] Mark the "`EnsureNotCurrentlyAdmin` conflates a missing account with a non-admin account" item **checked, still not applicable** — this story's `AdminDelete` action always pre-loads the account via `AccountService.AdminSoftDeleteAccount`'s own `FindById` call (throwing `AccountNotFoundException` first) before `SoftDelete`/`EnsureNotCurrentlyAdmin` ever runs on an unverified id, same as `AdminUpdateAccount` already established.
  - [x] Confirm the Tab-trap `isSubmitting` gap (round 2 of story-3.3's review) is unaffected — this story's Delete button uses the same native `disabled={isSubmitting}` pattern already in place, not a new instance of the gap.
  - [x] Confirm the rate-limiting gap noted for `AdminCreate` (`POST /api/account`, no rate limit despite setting a password hash) is **not applicable** here — `AdminDelete` sets no password and creates no credential, so the same rationale for that gap doesn't transfer.
- [ ] **Task 9: Verify CI green and branch/PR**
  - [x] Branch as `story/3.5-admin-deletes-an-account` from `main`.
  - [ ] Push and confirm both CI jobs (Backend .NET, Frontend Vite/React) green before merging (AD-11).
  - [ ] Open PR and merge to `main` — left for Jack per standing project practice.

## Dev Notes

### Architecture Compliance (must-follow, not optional)

- **AD-1 (layering)** — this story adds one action to the existing `AccountController`; no new `AdminController`/`AdminService`. `AccountService.AdminSoftDeleteAccount` (already built, Story 3.1) is where the business logic belongs — this story wires an HTTP surface on top (Task 1) and closes a real, pre-existing Service-layer gap in that same method (Task 2), same shape as every prior Epic 3 story's Controller-first pattern, with the one deliberate deviation Task 2 explains.
- **AD-2 (role/session liveness)** — `[Authorize(Roles = "Admin")]` on the new action, same proven mechanism as `Search`/`AdminUpdate`/`AdminCreate`.
- **AD-8 (soft-delete, computed status)** — deleting a barber or customer never hard-deletes their appointment rows either; the cascade only ever sets `CancelledAt` via the existing `Cancel` mechanism, exactly like every other cancellation path (AC #3, #6).
- **AD-15 (Account soft-delete)** — this story's entire backend behavior is already built and tested (`AccountRepository.SoftDelete`, Story 3.1): sets `DeletedAt`, relies on `UNIQUE(Email) WHERE DeletedAt IS NULL` for AC #2's "email becomes registerable again," and `FindByEmail`/`FindById` already exclude soft-deleted rows everywhere (auth, search, admin-update, admin-create's duplicate-email check) so AC #5 ("deleted account can't sign in") needs no new code — it falls out of `AuthService.Login`'s existing `FindByEmail` call finding nothing.
- **AD-16 (Account optimistic concurrency)** — `AccountConflictException` (stale `RowVersion`) → 409, reusing the exact mechanism Story 3.1 built and Stories 1.7/3.3 already proved works, now exercised for delete (AC #7).
- **AD-17 (single read path for appointment views)** — the new `CancelAllFutureForCustomer` reuses the same shared `Cancel` method every other cancellation path uses; it does not introduce a second way to cancel an appointment.
- **project-context.md's fixed 401/403 split** — `[Authorize(Roles = "Admin")]` yields this for free, already proven by `RoleGatingTests` against this exact pipeline.
- **AD-4 (testing)** — backend: xUnit.v3 + `WebApplicationFactory` against real SQLite, no mocking. Frontend: Vitest + RTL + `user-event`, stub `deleteAccount` directly, no MSW.

### Design Decisions This Story Must Make (epics/architecture/UX leave these open)

- **Route: `DELETE /api/account/{id}`, gated `[Authorize(Roles = "Admin")]`, returning 204.** Neither `epics.md` nor the architecture docs specify an endpoint shape — same gap every prior Epic 3 story already navigated. `DELETE` is the correct REST verb here (unlike `BookingController.CancelBooking`'s `POST .../cancel`, which models a state transition on an appointment that keeps existing, not a resource's removal) and slots naturally alongside the existing `PUT`/`POST` actions on this same `api/account` route family.
- **The customer-cascade gap (AC #6) is this story's job to close, not a future one.** `deferred-work.md`'s own note is explicit that Story 3.5 "should account for this before shipping a delete UI" — shipping the delete UI without it would ship a real, visible bug (a barber's schedule rendering a blank customer name for an appointment whose customer just got deleted), not a hypothetical one. The fix (Task 2) is a direct, minimal mirror of the barber cascade already in the codebase — no new mechanism, no new authorization logic, just the same shape applied to the other role that holds appointments.
- **Delete is a third `pendingAction` on the existing shared `ConfirmPopup`, not a new popup instance.** This is the opposite call from Story 3.4's create-popup `ConfirmPopup` (which needed independence because create has no target account) — Delete shares `editingAccount` with the identity/password saves already in this popup, so it belongs on their shared confirm instance. Get this distinction right: "does this action share a target account with the popup's other actions?" is the actual test, not "is this a different kind of action."
- **No new frontend page/component.** Matches every prior Epic 3 story's explicit instruction that Stories 3.2–3.5 extend `AdminPanel.jsx` rather than creating separate surfaces.
- **`deleteAccount`'s success path returns `{ ok: true }` with no `account`/`identity` payload** — a 204 has no body to parse, unlike every other function in `AccountApi.js`. Don't call `response.json()` unconditionally on the success path the way `adminUpdateAccount`/`createBarberAccount` do; it will throw on an empty 204 body.

### Testing Requirements

- Backend: xUnit.v3 + `WebApplicationFactory`/`SqliteApiFactory`, real temp SQLite, no mocked `DbContext`. This story's Controller tests are thin HTTP-wiring smoke tests for the delete action itself — the demotion/cascade *mechanics* are proven at the Service layer (Task 3); don't re-derive that full matrix at the Controller layer, same division of labor Story 3.3/3.4 already established.
- The new `CancelAllFutureForCustomer`/`FindFutureByCustomer` pair needs its own direct test coverage (Task 3) since it's genuinely new code, unlike the rest of this story's backend surface which is Controller-only.
- Concurrency test (`AdminDelete_on_stale_RowVersion_returns_409`) must use the two-independent-`DbContext` deterministic pattern — never a real concurrent-HTTP race (standing practice since Stories 1.2/1.7's flaky-test fixes, reused in every Epic 3 story so far).
- Frontend: Vitest + `@testing-library/react` + `user-event`; stub `deleteAccount` directly via the existing `vi.mock('../api/AccountApi')` block in `AdminPanel.test.jsx`, extending its `searchAccounts`/`adminUpdateAccount`/`createBarberAccount` stubs rather than replacing the file's mocking approach.

### Previous Story Intelligence (Stories 3.1, 3.3, 3.4)

- **Story 3.1** already built and tested `AccountService.AdminSoftDeleteAccount`/`AccountRepository.SoftDelete` in full for the barber-cascade + admin-protection + concurrency-conflict cases — this story's backend work is genuinely incremental (one new Controller action + the customer-cascade gap), not a rebuild.
- **Story 3.3** established: the shared `ConfirmPopup` distinguished by `pendingAction`, the "read last-confirmed identity from `editingAccount`, never live form state" rule (not relevant to delete itself, since delete reads no form fields, but the surrounding popup machinery this story extends), the Admin-role read-only popup guard (extend it to hide Delete too), and — explicitly in its own Dev Notes — deferred the Delete button itself to this story by name.
- **Story 3.4** established: the `Modal`-vs-hand-rolled-overlay decision rule ("does this popup contain a `SelectDropdown`?") — not relevant here since this story adds a button to the *existing* hand-rolled edit popup, not a new popup — and the "does this action share a target account with an existing flow?" rule for whether a new action needs its own `ConfirmPopup` instance or can share one. Story 3.5 is the first case since 3.3 where the answer to that question is "yes, share it" (delete targets `editingAccount`, same as the identity/password saves) rather than "no, it needs its own" (create had no target account at all).
- The `result.status === 401` → `logout()` + navigate to `/login` branch is this file's standing 401-handling convention (Story 3.2's review) — the delete branch must reach it too, not reinvent it.

### Git Intelligence Summary

Recent commits: `5e227c5` (Story 3.4 code-review fixes, current `main`/branch tip) → `932d9a3` (Story 3.4 implementation) → `6e8295e` (Story 3.4 created) → `7c2bb6f` (Story 3.3 merge, #16). Established rhythm continues unchanged: create the story on `main`, implement on `story/3.5-admin-deletes-an-account`, PR with an additions/fixes/test-count summary, merge once both CI jobs are green, delete the branch — push/PR/CI verification left for Jack. `5e227c5` confirms `AccountController` currently has exactly four actions (`UpdateMe`, `Search`, `AdminUpdate`, `AdminCreate`) and `AccountService.AdminSoftDeleteAccount`/`AccountRepository.SoftDelete` already exist, fully tested for the barber-cascade case, from Story 3.1 — this story adds the controller's fifth action on top of it and closes the one Service-layer gap that method still has.

### Project Structure Notes

- **Backend — modified:** `backend/BarbershopApi/Controllers/AccountController.cs` (new `[HttpDelete("{id:int}")]` action), `backend/BarbershopApi/Services/AccountService.cs` (branch `AdminSoftDeleteAccount`'s post-delete cascade on `Role.Customer` too), `backend/BarbershopApi/Services/IBookingService.cs` + `BookingService.cs` (new `CancelAllFutureForCustomer`), `backend/BarbershopApi/Repositories/IAppointmentRepository.cs` + `AppointmentRepository.cs` (new `FindFutureByCustomer`). No new backend files, no DTO needed (delete takes no request body).
- **Frontend — modified:** `frontend/src/pages/AdminPanel.jsx` (Delete button, `pendingAction` extended to include `'delete'`, `handleDeleteClick`, `handleConfirmEdit`'s new branch), `frontend/src/pages/AdminPanel.css` (only if the footer's existing gap needs adjustment for a third button — check before adding), `frontend/src/api/AccountApi.js` (new `deleteAccount` export). No new frontend files.
- **Tests — modified:** `backend/BarbershopApi.Tests/AccountServiceTests.cs` (one new case), `backend/BarbershopApi.Tests/AccountControllerTests.cs` (new cases only), `frontend/src/pages/AdminPanel.test.jsx` (new cases only, plus extending the existing Admin-role-read-only test's assertions).
- `_bmad-output/implementation-artifacts/deferred-work.md` — modified (one item marked resolved, one confirmed still-not-applicable per Task 8).
- `Program.cs` needs no changes — no new DI registrations, `IAccountService`/`IBookingService`/`IAppointmentRepository` are already `Scoped` and unchanged in shape (only new methods added to existing interfaces).

### Established Codebase Patterns to Extend (current state, confirmed by reading the files directly)

- `AccountController` today (post-3.4): `[ApiController] [Route("api/account")] [Authorize]` class-level, `PUT api/account/me` (self-service), `GET api/account/search`, `PUT api/account/{id:int}`, `POST api/account` (all three `[Authorize(Roles = "Admin")]`). This story adds the controller's fifth action, `DELETE api/account/{id:int}`, its fourth `[Authorize(Roles = "Admin")]`-gated one.
- `AccountService.AdminSoftDeleteAccount(int accountId, int actingAdminId)` (Story 3.1, `AccountService.cs:154-172`, already tested for the barber case): loads by id (`AccountNotFoundException` if missing) → `accountRepository.SoftDelete` (maps `DbUpdateConcurrencyException` → `AccountConflictException`) → if `account.Role == Role.Barber`, cascades via `bookingService.CancelAllFutureForBarber`. This story's Controller action is a direct pass-through to this signature (unchanged); this story's Task 2 extends the method's *body* to add the `Role.Customer` branch — the public signature does not change.
- `AccountRepository.SoftDelete(Account account)` (`AccountRepository.cs:85-92`, Story 3.1): calls `EnsureNotCurrentlyAdmin` first (→ `AdminAccountProtectedException`), sets `DeletedAt = DateTime.UtcNow`, saves. Already fully tested (`AccountRepositoryTests.cs:453-493`); no changes needed.
- `AppointmentRepository.FindFutureByBarber(int barberId, DateTime nowEst)` (`AppointmentRepository.cs:41-52`) and `BookingService.CancelAllFutureForBarber(int barberId, int callerAccountId, Role callerRole, DateTime? now = null)` (`BookingService.cs:169-193`) are the exact templates Task 2's new `FindFutureByCustomer`/`CancelAllFutureForCustomer` copy, substituting `CustomerId` for `BarberId`.
- `BookingController.CancelBooking` (`BookingController.cs:116-141`) is the closest existing precedent for a `NoContent()`-returning action with a try/catch mapping domain exceptions to `Problem()` calls — this story's `AdminDelete` follows the identical shape, swapping in `AccountNotFoundException`/`AdminAccountProtectedException`/`AccountConflictException` for `AppointmentNotFoundException`/`AppointmentAlreadyCancelledException`/`AppointmentAlreadyFinishedException`.
- `AdminPanel.jsx`'s hand-rolled edit popup (`frontend/src/pages/AdminPanel.jsx:465-645`) already has: the `isAdminAccount`/`identityDisabled` read-only guard, the shared `confirmOpen`/`pendingAction` state distinguishing `'details'`/`'password'`, and a footer row (`.admin-edit-popup__footer`) holding "Save Changes"/"Cancel" — this story adds "Delete" to that same footer and a third `pendingAction` value, touching no other structural part of the popup.
- `ConfirmPopup` (`frontend/src/components/ConfirmPopup.jsx`) already accepts a `destructive` prop that swaps the Confirm button's variant (`primary` vs. `destructive`) — this story is the first place in `AdminPanel.jsx` that needs to pass `destructive={true}` conditionally rather than a fixed value, since the same instance now serves both non-destructive (details/password) and destructive (delete) confirmations.
- `AccountApi.js`'s `adminUpdateAccount`/`createBarberAccount` are the closest templates for `deleteAccount`'s fetch/try-catch structure, but neither is an exact match — both call `response.json()` unconditionally because they expect a body on success; `deleteAccount` must not, since `DELETE` returns 204 with none.

### References

- [Source: _bmad-output/planning-artifacts/epics.md §Story 3.5 (line 723)] — story statement, five acceptance criteria (verbatim, AC #1–#5 of this story), FR coverage (FR40, FR34, AD-15)
- [Source: _bmad-output/planning-artifacts/prds/prd-bmad-learning-project-2026-07-21/prd.md §FR40, §FR34, §FR41] — exact FR wording: admin deletes customer/barber, confirm-gated, barber-cascade (FR40); single admin never deletable (FR34); concurrent edit/delete conflict handling (FR41)
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-bmad-learning-project-2026-07-23/EXPERIENCE.md — Component Patterns (admin account-edit popup, line 68: "Delete routes through it too"; confirm-action popup, line 67), State Patterns (barber demotion/deletion cascade, line 112), Key Flow (line 190: "an account deletion follows the exact same popup, just with a destructive-red Confirm instead of blue")] — Delete lives inside the existing edit popup, not a new surface; exact confirm-popup color-by-consequence rule
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-bmad-learning-project-2026-07-23/DESIGN.md — `confirm-popup` token, Button-Destructive (line 331: "Delete" in the admin account-edit popup is explicitly named as a destructive-button use case), Do's/Don'ts (line 353, 358: destructive color reserved for Cancel/Delete only; Confirm colored by consequence)] — this story's Delete button and its Confirm are exactly the case these tokens were written for
- [Source: _bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/ARCHITECTURE-SPINE.md#AD-1, #AD-2, #AD-8, #AD-15, #AD-16, #AD-17] — layering, role/session liveness, computed-status/soft-cancel mechanism, account soft-delete + relaxed email uniqueness, optimistic concurrency, single shared appointment read/cancel path
- [Source: backend/BarbershopApi/Controllers/AccountController.cs, BookingController.cs; Services/AccountService.cs, IAccountService.cs, BookingService.cs, IBookingService.cs; Repositories/AccountRepository.cs, IAccountRepository.cs, AppointmentRepository.cs, IAppointmentRepository.cs] — current controller/service/repository shapes this story extends; `AdminSoftDeleteAccount`'s existing signature and the customer-cascade gap in its body; `CancelAllFutureForBarber`'s exact template for the new customer-side mirror; `CancelBooking`'s `NoContent()`/exception-mapping precedent
- [Source: backend/BarbershopApi.Tests/AccountServiceTests.cs:361-428, AccountRepositoryTests.cs:453-493, AccountControllerTests.cs] — existing barber-cascade/admin-protection/concurrency test coverage this story extends with the customer-side case; `AdminUpdateRequest`/`AdminCreateRequest` HTTP-request-builder-helper pattern this story's new `AdminDeleteRequest` follows; `RoleGatingTests.RegisterAndLoginAs` helper
- [Source: frontend/src/pages/AdminPanel.jsx, AdminPanel.test.jsx; components/ConfirmPopup.jsx, Modal.jsx, Button.jsx; api/AccountApi.js] — existing edit-popup structure, `pendingAction`/shared-`ConfirmPopup` pattern, Admin-role read-only guard, 401-handling convention this story extends; the `destructive` prop this story is the first to set conditionally
- [Source: _bmad-output/implementation-artifacts/3-1-account-repository-admin-operations.md, 3-3-admin-edits-an-account.md, 3-4-admin-creates-a-barber-account.md] — predecessor stories: `AdminSoftDeleteAccount`/`SoftDelete`'s exact contract and already-complete barber-case test coverage (3.1); the shared-`ConfirmPopup`/`pendingAction` pattern and the explicit deferral of this story's Delete button (3.3); the "does this action share a target account?" rule for whether a new action needs its own `ConfirmPopup` instance (3.4, applied in reverse here)
- [Source: _bmad-output/implementation-artifacts/deferred-work.md §Deferred from: code review of story-3.1-account-repository-admin-operations, §Deferred from: code review of story-3.3-admin-edits-an-account, round 2] — the customer-cascade gap this story resolves (Task 2); the `EnsureNotCurrentlyAdmin` conflation and Tab-trap `isSubmitting` gap this story confirms remain not-applicable/unaffected
- [Source: project-context.md §Language-Specific Rules (C#, 401/403 split, soft-delete-only rule); §Testing Rules; §Naming; §Code organization; §Critical Don't-Miss Rules (concurrency, cross-cutting gotchas)]

## Dev Agent Record

### Agent Model Used

Amelia (claude-sonnet-5), via the `bmad-dev-story` workflow.

### Debug Log References

None — no blocking failures encountered. All red→green cycles resolved on the first implementation pass; no HALT conditions triggered.

### Completion Notes List

- Task 1: Added `AccountController.AdminDelete` (`DELETE /api/account/{id}`, `[Authorize(Roles = "Admin")]`, 204/404/400/409/500 mapping identical to `AdminUpdate`'s existing exception-to-status precedent, only the `AdminAccountProtectedException` message text changed to "cannot be deleted"). Verbatim per the story's Dev Notes body.
- Task 2: Added `IAppointmentRepository.FindFutureByCustomer`/`AppointmentRepository.FindFutureByCustomer` (exact copy of `FindFutureByBarber`'s query shape, filtered on `CustomerId`) and `IBookingService.CancelAllFutureForCustomer`/`BookingService.CancelAllFutureForCustomer` (exact copy of `CancelAllFutureForBarber`'s body, same per-appointment exception-swallowing). Branched `AccountService.AdminSoftDeleteAccount` on `Role.Customer` to call the new method after `SoftDelete` commits — closes the customer-cascade gap `deferred-work.md` flagged from Story 3.1's review (AC #6).
- Task 3: Added `AdminSoftDeleteAccount_on_customer_cancels_future_appointments_but_retains_past` to `AccountServiceTests.cs`, mirroring the existing barber-case test with the customer role instead. Also added direct coverage for the new repository/service methods (`FindFutureByCustomer_excludes_past_and_cancelled_appointments` in `AppointmentRepositoryTests.cs`; `CancelAllFutureForCustomer_cancels_all_future_appointments_for_that_customer_only` and `CancelAllFutureForCustomer_tolerates_an_already_cancelled_appointment_without_aborting_the_rest` in `BookingServiceTests.cs`) per the story's Testing Requirements note that this new code needs its own direct coverage, not just the Service-level integration test.
- Task 4: Added an `AdminDeleteRequest` HTTP-request-builder helper and 9 new `AccountControllerTests.cs` cases (all listed sub-bullets). Confirmed RED (compile failure — `AccountController.AdminDelete`/`IBookingService.CancelAllFutureForCustomer`/`IAppointmentRepository.FindFutureByCustomer` didn't exist yet) before implementing Tasks 1-2, then GREEN after. Full backend suite: 257/257 passing, no regressions.
- Task 5: Added `deleteAccount` to `AccountApi.js` — `DELETE` request, no body on success (`{ ok: true }`, no `response.json()` call on the 204 path since there's nothing to parse), same failure envelope shape as `adminUpdateAccount`/`createBarberAccount`. No dedicated unit test file for this function, consistent with the existing pattern — exercised via `AdminPanel.test.jsx`'s mocks instead.
- Task 6: Added the "Delete" button (`variant="destructive"`, visible whenever `!isAdminAccount`) to the existing hand-rolled edit popup's footer in `AdminPanel.jsx`, extended `pendingAction` to `'details' | 'password' | 'delete'`, added `handleDeleteClick`, made the shared `ConfirmPopup`'s `destructive`/`title`/`message` props conditional on `pendingAction === 'delete'`, and extended `handleConfirmEdit` with a delete branch (calls `deleteAccount` instead of `adminUpdateAccount`; on success filters the deleted account out of `accounts` and closes the popup; 401/409/400 paths reuse the existing unconditional branches, narrowing the duplicate-email 409 check to `pendingAction === 'details'` only since delete/password saves can never hit that title). No new CSS needed — the existing `.admin-edit-popup__footer` flex-gap already separates the third button adequately.
- Task 7: Added a `describe('Delete Account', ...)` block with 6 new `AdminPanel.test.jsx` cases (all listed sub-bullets) and extended the existing Admin-role read-only popup test to also assert no "Delete" button renders. All 42 `AdminPanel.test.jsx` tests pass; full frontend suite: 188/188 passing (20 files), no regressions. `eslint .` clean; `prettier --check .` initially flagged the two touched files, resolved with `prettier --write` and re-verified clean.
- Task 8: Re-read `deferred-work.md` in full. Marked the customer-cascade gap (from Story 3.1's review) resolved by Task 2's `CancelAllFutureForCustomer`; marked the `EnsureNotCurrentlyAdmin` conflation item checked/still-not-applicable (same pre-load-via-`FindById` rationale as `AdminUpdateAccount`); added a new "Checked during story-3.5" section confirming the Tab-trap `isSubmitting` gap and `AdminCreate`'s rate-limiting gap are both unaffected/not-applicable to this story.
- Task 9: Already on `story/3.5-admin-deletes-an-account` (branched from `main` at story-creation time; `baseline_commit` in this file's frontmatter confirms it). Push, CI verification, and PR/merge intentionally left for Jack per standing project practice (and this session's standing instruction to pause before commit/push/PR) — not executed by this dev session.

### File List

**Backend — modified:**
- `backend/BarbershopApi/Controllers/AccountController.cs`
- `backend/BarbershopApi/Services/AccountService.cs`
- `backend/BarbershopApi/Services/IBookingService.cs`
- `backend/BarbershopApi/Services/BookingService.cs`
- `backend/BarbershopApi/Repositories/IAppointmentRepository.cs`
- `backend/BarbershopApi/Repositories/AppointmentRepository.cs`

**Backend — tests modified:**
- `backend/BarbershopApi.Tests/AccountServiceTests.cs`
- `backend/BarbershopApi.Tests/AccountControllerTests.cs`
- `backend/BarbershopApi.Tests/AppointmentRepositoryTests.cs`
- `backend/BarbershopApi.Tests/BookingServiceTests.cs`

**Frontend — modified:**
- `frontend/src/pages/AdminPanel.jsx`
- `frontend/src/api/AccountApi.js`

**Frontend — tests modified:**
- `frontend/src/pages/AdminPanel.test.jsx`

**Docs — modified:**
- `_bmad-output/implementation-artifacts/deferred-work.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Change Log

- 2026-08-17: Implemented Story 3.5 (Tasks 1-8) — `AccountController` gained its fifth action, `DELETE /api/account/{id}`, gated `[Authorize(Roles = "Admin")]`, returning 204, backed by the existing `AccountService.AdminSoftDeleteAccount` (Story 3.1). Closed the one real Service-layer gap that method still had: added `CancelAllFutureForCustomer`/`FindFutureByCustomer` (direct mirrors of the existing barber cascade) so deleting a customer with future appointments cancels them too, matching the barber-deletion cascade and closing a real display bug (blank customer name on a barber's schedule). Frontend gained a "Delete" button inside the existing admin edit popup, routed through the same shared `ConfirmPopup` (now conditionally destructive) as the identity/password saves. Backend suite green (257/257); frontend suite green (188/188, 20 files); ESLint and Prettier clean. Updated `deferred-work.md`: resolved the customer-cascade gap, confirmed the `EnsureNotCurrentlyAdmin` conflation remains not-applicable, and confirmed the Tab-trap and `AdminCreate` rate-limiting gaps are unaffected. Task 9 (push/PR/CI verification) intentionally left for Jack per standing project practice.
