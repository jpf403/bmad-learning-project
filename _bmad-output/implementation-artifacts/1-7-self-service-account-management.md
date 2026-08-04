---
baseline_commit: 9f5d702d207c21ff6f6bd8b37f07ac94554ae477
---

# Story 1.7: Self-Service Account Management

Status: done

## Story

As a signed-in user,
I want to edit my own first name, last name, and password from an Account page,
so that I can keep my profile current without needing an admin.

## Acceptance Criteria

1. **Given** the Account page, **when** a signed-in user edits first name, last name, and/or password (double-entry), **then** a confirm-action popup appears before the change takes effect (FR28).
2. **Given** a confirmed save, **when** it completes, **then** "Changes saved." appears above the form and the user's current session continues uninterrupted — self password changes never bump `SessionVersion` or force a re-login (FR28).
3. **Given** mismatched password/confirm fields, **when** submitted, **then** "Passwords do not match" is shown and only those two fields clear.
4. **Given** the Account page, **when** rendered, **then** email is displayed but is not editable (FR28).

## Tasks / Subtasks

- [x] **Task 1: `AccountController` / `AccountService` — new domain trio** (AC: #1, #2, #4)
  - [x] This is the **first new domain trio since `Auth`** (Story 1.4). Per AD-1, "Account/Admin" is its own domain concept, distinct from "Auth" — do **not** add this endpoint to `AuthController`/`AuthService`. Create `AccountController`, `IAccountService`/`AccountService`, reusing the existing `AccountRepository` unchanged (no repository changes needed — `FindById`/`Update` already do everything this story needs).
  - [x] `backend/BarbershopApi/Dtos/UpdateAccountRequest.cs`:
    ```csharp
    public class UpdateAccountRequest
    {
        [Required]
        [StringLength(100)]
        [RegularExpression(@"(?s).*\S.*", ErrorMessage = "First name is required.")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [RegularExpression(@"(?s).*\S.*", ErrorMessage = "Last name is required.")]
        public string LastName { get; set; } = string.Empty;

        [MinLength(8)]
        [StringLength(128)]
        [RegularExpression(@"^\S+$", ErrorMessage = "Password cannot contain whitespace.")]
        public string? NewPassword { get; set; }
    }
    ```
    Same `FirstName`/`LastName`/password constraints as `RegisterRequest` (AD consistency, not a new policy). `NewPassword` is nullable/optional — `[Required]` is deliberately omitted; `MinLengthAttribute`/`StringLengthAttribute`/`RegularExpressionAttribute` all treat `null` as valid, so leaving the password fields blank skips password validation entirely and leaves the password unchanged.
  - [x] **No `ConfirmNewPassword` field in the DTO.** Password-mismatch handling is a pure client-side check only (see Task 3) — this exactly mirrors `Register.jsx`'s existing precedent ("do not call the API for a pure client-side check"). Don't invent a server-side confirm-match validator.
  - [x] `backend/BarbershopApi/Services/AccountConflictException.cs` — plain `Exception` subclass, same shape as `DuplicateEmailException`/`InvalidSessionException`.
  - [x] `backend/BarbershopApi/Services/IAccountService.cs` / `AccountService.cs`:
    ```csharp
    public interface IAccountService
    {
        Task<Account> UpdateOwnProfile(int accountId, string firstName, string lastName, string? newPassword);
    }

    public class AccountService(IAccountRepository accountRepository, IPasswordHasher<Account> passwordHasher) : IAccountService
    {
        public async Task<Account> UpdateOwnProfile(int accountId, string firstName, string lastName, string? newPassword)
        {
            var account = await accountRepository.FindById(accountId)
                ?? throw new InvalidOperationException("Account not found for an authenticated caller.");

            account.FirstName = firstName.Trim();
            account.LastName = lastName.Trim();

            if (!string.IsNullOrEmpty(newPassword))
            {
                account.PasswordHash = passwordHasher.HashPassword(account, newPassword);
            }

            try
            {
                await accountRepository.Update(account);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new AccountConflictException();
            }

            return account;
        }
    }
    ```
    **Critical — do NOT add `account.SessionVersion++` anywhere in this method**, even though a password change might look like "the same kind of write" `AuthService.Logout` does (`account.SessionVersion++; await accountRepository.Update(account);`). AC #2 and AD-2 are explicit that only an *admin-driven* password change (FR35, Epic 3) bumps `SessionVersion`; a self-service change never does. This is the single easiest thing to get wrong by pattern-matching on `Logout`.
  - [x] `AccountRepository.Update`'s existing `context.Update(account); await context.SaveChangesAsync();` on the same tracked entity `FindById` returned is what makes the `DbUpdateConcurrencyException` on line above possible: if another request (e.g. a future Epic 3 admin edit) commits a change to the same row between this request's `FindById` and this request's `SaveChangesAsync`, EF's `RowVersion` concurrency-token check fails and throws — this is AD-16's "admin edit racing the account holder's own self-edit" scenario, verbatim from `ARCHITECTURE-SPINE.md`. No client-supplied `RowVersion` is needed in the request — the same `DbContext` instance retains the value it originally read.
  - [x] `backend/BarbershopApi/Controllers/AccountController.cs`:
    ```csharp
    [ApiController]
    [Route("api/account")]
    [Authorize]
    public class AccountController(IAccountService accountService) : ControllerBase
    {
        [HttpPut("me")]
        public async Task<IActionResult> UpdateMe(UpdateAccountRequest request)
        {
            var account = (Account)HttpContext.Items["Account"]!;
            try
            {
                var updated = await accountService.UpdateOwnProfile(account.Id, request.FirstName, request.LastName, request.NewPassword);
                return Ok(new MeResponse(updated.Id, updated.Email, updated.FirstName, updated.LastName, updated.Role));
            }
            catch (AccountConflictException)
            {
                return Problem(statusCode: StatusCodes.Status409Conflict, title: "This account was updated elsewhere. Please refresh and try again.");
            }
            catch (Exception)
            {
                return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Something went wrong. Please try again.");
            }
        }
    }
    ```
    Route is `PUT /api/account/me` — deliberately under `api/account`, not `api/auth`, per the domain split above. `[Authorize]` with no `Roles` restriction: every role can self-edit, same precedent as `GET /api/auth/me`. `HttpContext.Items["Account"]` is guaranteed populated by `SessionLivenessMiddleware` (Story 1.6) by the time this action runs — no null-check needed, same reasoning `AuthController.Me()` already relies on.
  - [x] **Reuse `MeResponse` as the response shape** — don't invent a second "who am I after update" DTO. Mirrors the "one shared who-am-I shape" principle AD-3 already established for `/me`.
  - [x] **No new GET endpoint.** The Account page's initial `firstName`/`lastName`/`email` values come straight from `AuthContext`'s existing cached `user` object (already populated via the session-bootstrap `/me` call or `RequireRole`'s own guard check) — do not build a redundant `GET /api/account/me`.
  - [x] In `Program.cs`, register the new service: `builder.Services.AddScoped<IAccountService, AccountService>();`. `IAccountRepository` is already registered (Story 1.2) — no change needed there.

- [x] **Task 2: Backend tests** (AC: #1, #2, #4)
  - [x] New `backend/BarbershopApi.Tests/AccountServiceTests.cs` (direct instantiation against a real SQLite `DbContext` — no mocking, AD-4/NFR4):
    - `UpdateOwnProfile_updates_first_and_last_name`
    - `UpdateOwnProfile_with_new_password_hashes_it_and_does_not_change_SessionVersion` — the single most important regression test in this story: assert `SessionVersion` is unchanged after the call, and that the new password (not the old one) verifies via `passwordHasher.VerifyHashedPassword`.
    - `UpdateOwnProfile_without_new_password_leaves_PasswordHash_unchanged`
    - `UpdateOwnProfile_on_stale_RowVersion_throws_AccountConflictException` — reuse the exact two-`DbContext` staleness technique `AccountRepositoryTests.Update_with_stale_RowVersion_throws_DbUpdateConcurrencyException` already uses (load the same row via two separate contexts/repositories, commit a change via the second, then attempt the update via the service using the first's already-loaded state) — but assert the service translates the underlying `DbUpdateConcurrencyException` into `AccountConflictException`, not that the raw EF exception leaks out.
  - [x] New `backend/BarbershopApi.Tests/AccountControllerTests.cs` (via `SqliteApiFactory.CreateClient()`, real SQLite, matching every existing controller test):
    - `UpdateMe_without_access_token_returns_401`
    - `UpdateMe_updates_profile_and_returns_MeResponse` — login, PUT new first/last name, assert 200 + body matches, then confirm the change actually persisted via a follow-up `/me` call.
    - `UpdateMe_with_new_password_allows_login_with_new_password_and_rejects_old` — the end-to-end proof that password rotation works and, critically, that the **original access token obtained before the password change still works against `/me` afterward** (proving `SessionVersion` was not bumped — a stale-token 401 here would mean the "no session interruption" AC silently broke).
    - `UpdateMe_with_blank_first_name_returns_400_with_PascalCase_error_key` — same PascalCase-error-body convention as `Register_400_error_body_uses_PascalCase_field_keys` (Story 1.4), asserting `errors.FirstName`.
    - `UpdateMe_with_short_new_password_returns_400`
    - `UpdateMe_two_concurrent_edits_to_same_account_one_succeeds_one_returns_409` — fire two `PUT /api/account/me` requests for the same signed-in account concurrently (`Task.WhenAll`) with different `FirstName` values; assert exactly one response is `200` and the other is `409`. SQLite serializes the two writes at the storage layer, but each request's `SaveChangesAsync` still checks against the `RowVersion` value *it* read before waiting — so the second to commit legitimately loses the race and gets a real `DbUpdateConcurrencyException` → `409`, not a flaky/order-dependent assertion.
  - [x] Use the codebase's established fixture identity in every new test: `email: "john@example.com"`, `FirstName: "John"`, `LastName: "Smith"`. **Never use "Jack"** — this is a hard project convention, confirmed across six existing test files.

- [x] **Task 3: `AccountApi.js`** (AC: #1, #2, #3, #4)
  - [x] `frontend/src/api/AccountApi.js`, mirroring `AuthApi.js`'s exact fetch-wrapper shape (try/catch around `fetch()` itself, `.json().catch(() => null)` guard, `credentials: 'include'`):
    ```js
    import { API_BASE_URL } from './ApiConfig'

    export async function updateAccount(accessToken, { firstName, lastName, newPassword }) {
      let response
      try {
        response = await fetch(`${API_BASE_URL}/api/account/me`, {
          method: 'PUT',
          credentials: 'include',
          headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer ${accessToken}`,
          },
          body: JSON.stringify({ firstName, lastName, newPassword: newPassword || null }),
        })
      } catch {
        return { ok: false, status: null }
      }

      const body = await response.json().catch(() => null)
      if (!response.ok) {
        return { ok: false, status: response.status, problem: body }
      }
      return { ok: true, identity: body }
    }
    ```

- [x] **Task 4: `Account` page — two independent edit sections** (AC: #1, #2, #3, #4)
  - [x] **Design (Jack, 2026-08-03):** the page is a read-only profile view by default, not a single always-editable form. Email is always visible, always read-only. Name (first + last together) and password are two **independent** edit flows, each with its own trigger, its own Save, and its own confirm popup:
    - **Name**: displayed as plain text with an edit icon/button next to it. Clicking it reveals two side-by-side inputs (First Name, Last Name) plus Save/Cancel underneath.
    - **Password**: never displayed. A "Change Password" button reveals New Password + Confirm New Password inputs plus Save/Cancel underneath. Cancel clears both fields and hides them again.
    - Each section's Save opens the confirm popup before committing (per the answered question above — FR28 draws no distinction between name and password edits). The two sections can be in edit mode independently and saved independently; one `ConfirmPopup` instance is shared, keyed by which section is pending (`pendingAction`), since only one save can be in flight/confirming at a time.
  - [x] **No backend/DTO change from Task 1.** A password-only save still sends the current (unchanged) `firstName`/`lastName` from component state alongside `newPassword`; a name-only save omits `newPassword`. `UpdateAccountRequest` already supports both shapes as designed — don't add partial-update semantics or a second endpoint.
  - [x] `frontend/src/pages/Account.jsx` + sibling `frontend/src/pages/Account.css` (every existing page has its own stylesheet — follow that convention). Reuse `ConfirmPopup`/`Input`/`Button` as-is; no new Modal/Confirm-popup component needed:
    ```jsx
    import { useState } from 'react'
    import { useAuth } from '../context/AuthContext'
    import { updateAccount } from '../api/AccountApi'
    import FormSection from '../components/FormSection'
    import Input from '../components/Input'
    import Button from '../components/Button'
    import ConfirmPopup from '../components/ConfirmPopup'
    import './Account.css'

    export default function Account() {
      const { user, login } = useAuth()

      const [isEditingName, setIsEditingName] = useState(false)
      const [firstName, setFirstName] = useState(user.firstName)
      const [lastName, setLastName] = useState(user.lastName)

      const [isChangingPassword, setIsChangingPassword] = useState(false)
      const [newPassword, setNewPassword] = useState('')
      const [confirmPassword, setConfirmPassword] = useState('')
      const [passwordError, setPasswordError] = useState('')

      const [fieldErrors, setFieldErrors] = useState({})
      const [savedMessage, setSavedMessage] = useState('')
      const [errorMessage, setErrorMessage] = useState('')
      const [isSubmitting, setIsSubmitting] = useState(false)

      const [confirmOpen, setConfirmOpen] = useState(false)
      const [pendingAction, setPendingAction] = useState(null) // 'name' | 'password'

      function clearMessages() {
        setSavedMessage('')
        setErrorMessage('')
        setFieldErrors({})
      }

      function handleCancelName() {
        setFirstName(user.firstName)
        setLastName(user.lastName)
        setIsEditingName(false)
      }

      function handleSaveNameClick() {
        clearMessages()
        setPendingAction('name')
        setConfirmOpen(true)
      }

      function handleCancelPassword() {
        setNewPassword('')
        setConfirmPassword('')
        setPasswordError('')
        setIsChangingPassword(false)
      }

      function handleSavePasswordClick() {
        clearMessages()
        if (newPassword !== confirmPassword) {
          setPasswordError('Passwords do not match')
          setNewPassword('')
          setConfirmPassword('')
          return
        }
        setPasswordError('')
        setPendingAction('password')
        setConfirmOpen(true)
      }

      async function handleConfirm() {
        setIsSubmitting(true)
        const result = await updateAccount(user.accessToken, {
          firstName,
          lastName,
          newPassword: pendingAction === 'password' ? newPassword : undefined,
        })
        setIsSubmitting(false)

        if (result.ok) {
          login({ ...user, firstName: result.identity.firstName, lastName: result.identity.lastName })
          if (pendingAction === 'name') {
            setIsEditingName(false)
          } else {
            setNewPassword('')
            setConfirmPassword('')
            setIsChangingPassword(false)
          }
          setSavedMessage('Changes saved.')
          return
        }
        if (result.status === 409) {
          setErrorMessage(result.problem?.title ?? 'This account was updated elsewhere. Please refresh and try again.')
        } else if (result.status === 400 && result.problem?.errors) {
          setFieldErrors(result.problem.errors)
        } else {
          setErrorMessage('Something went wrong. Please try again.')
        }
      }

      return (
        <FormSection className="account-page">
          {savedMessage && <p>{savedMessage}</p>}
          {errorMessage && <p className="error-message">{errorMessage}</p>}

          <Input label="Email" value={user.email} disabled />

          <section className="account-section">
            {!isEditingName ? (
              <div className="account-name-display">
                <span>{firstName} {lastName}</span>
                <button type="button" aria-label="Edit name" onClick={() => { clearMessages(); setIsEditingName(true) }}>✎</button>
              </div>
            ) : (
              <div className="account-name-edit">
                <div className="account-name-fields">
                  <Input label="First Name" value={firstName} onChange={(e) => setFirstName(e.target.value)} error={fieldErrors.FirstName?.[0]} />
                  <Input label="Last Name" value={lastName} onChange={(e) => setLastName(e.target.value)} error={fieldErrors.LastName?.[0]} />
                </div>
                <Button onClick={handleSaveNameClick} disabled={isSubmitting}>Save</Button>
                <Button variant="secondary" onClick={handleCancelName} disabled={isSubmitting}>Cancel</Button>
              </div>
            )}
          </section>

          <section className="account-section">
            {!isChangingPassword ? (
              <Button variant="secondary" onClick={() => { clearMessages(); setIsChangingPassword(true) }}>Change Password</Button>
            ) : (
              <div className="account-password-edit">
                <Input label="New Password" type="password" value={newPassword} onChange={(e) => setNewPassword(e.target.value)} error={passwordError || fieldErrors.NewPassword?.[0]} />
                <Input label="Confirm New Password" type="password" value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} />
                <Button onClick={handleSavePasswordClick} disabled={isSubmitting}>Save</Button>
                <Button variant="secondary" onClick={handleCancelPassword} disabled={isSubmitting}>Cancel</Button>
              </div>
            )}
          </section>

          <ConfirmPopup
            open={confirmOpen}
            onOpenChange={setConfirmOpen}
            title="Save changes?"
            message={pendingAction === 'password' ? 'Save your new password?' : 'Save these changes to your account?'}
            onConfirm={handleConfirm}
          />
        </FormSection>
      )
    }
    ```
    Check `FormSection.jsx`'s actual signature before assuming it forwards `className` (research found it as a trivial `<div className="form-section">{children}</div>` with no prop passthrough confirmed) — if it doesn't accept/merge a `className` prop, either add one (small, backward-compatible change) or nest a plain `<div className="account-page">` inside it instead. Either way, the outer container must still get `form-section`'s tinted-card treatment — `DESIGN.md` explicitly names Account as one of the pages using it, alongside Login/Register.
    Confirm the exact shape of `ProblemDetails.errors` (dictionary of `string[]`, ASP.NET Core's default) before wiring `fieldErrors.FirstName?.[0]` — match whatever `Register.jsx` already does for `problem.errors.Email`, don't diverge.
  - [x] **Edit icon**: no icon library exists in this codebase (confirmed — nothing beyond plain text/emoji is used anywhere today). The `✎` unicode glyph above is a placeholder choice, not a locked design decision — swap for whatever's simplest/consistent if an icon convention emerges, but don't pull in a new icon package for one glyph.
  - [x] **Cancel is required, not optional**, on both sections — without it there's no way to back out of edit mode once entered without saving. Cancel resets fields to the last-saved values (`user.firstName`/`user.lastName`, or blank for password) and returns to display mode; it does not call the API.
  - [x] **Email renders as a `disabled` `Input`** (not static text) — keeps consistent `Input` styling/label treatment; there's no existing "read-only field" precedent in this codebase, so this is the first one and sets the pattern.
  - [x] **`login({ ...user, firstName, lastName })` after a successful name save is not optional.** `AuthContext`'s cached `user` object is never automatically refetched — without this call, `NavBar` or any future consumer reading `user.firstName`/`user.lastName` would show stale data after a save, since nothing else in the codebase refreshes it. `login` is literally `setUser` (see `AuthContext.jsx`), so this is a safe, idiomatic use. (A password-only save also calls `login` with the same merged shape for consistency, even though only the password changed — harmless, keeps one code path.)
  - [x] Password-mismatch handling on `handleSavePasswordClick` is a pure client-side check (no API call, no confirm popup) before anything else happens — exact precedent from `Register.jsx`. Clears only the two password fields, per AC #3.
  - [x] `"Changes saved."` renders as the **first element on the page**, above both sections, satisfying "appears above the form" (AC #2) regardless of which section was saved, matching `EXPERIENCE.md`'s "a plain confirmation line appears above the form."
  - [x] **Correction to `project-context.md`**: its "plain-text, no dedicated color" note for form validation is **outdated**. `DESIGN.md`/`EXPERIENCE.md` were updated 2026-07-27 (after `project-context.md` was generated) — password-mismatch and other validation messages now use `colors.error` (`#C93A3A`) via the `Input` component's existing `error` prop (`typography.caption`/red), not plain uncolored text. The success message ("Changes saved.") *is* still correctly plain/unstyled — don't apply `colors.error` there; only the mismatch/validation-error text gets the red caption treatment, which `Input`'s existing `error` prop already handles automatically.

- [x] **Task 5: Wire the route** (AC: #1, #2, #3, #4)
  - [x] `frontend/src/App.jsx`: add `<Route path="/account" element={<RequireRole roles={['Customer', 'Barber', 'Admin']}><Account /></RequireRole>} />`. `NavBar.jsx`'s profile dropdown already calls `navigate('/account')` (built in Story 1.5) — this route currently renders blank; this task is what finally resolves it. No change needed to `NavBar.jsx` or `RequireRole.jsx` themselves.

- [x] **Task 6: Frontend tests** (AC: #1, #2, #3, #4)
  - [x] New `frontend/src/pages/Account.test.jsx`. Follow the established sign-in-harness pattern from `NavBar.test.jsx`/`RequireRole.test.jsx`: real `AuthProvider`, `vi.spyOn(globalThis, 'fetch')` (no MSW), a `SIGNED_IN_USER` fixture using `john@example.com`/`John`/`Smith`, sign in via the context before rendering `Account`.
  - [x] Cases: email renders disabled and shows the signed-in user's email, with no edit affordance next to it; name and password display in their collapsed/view state by default (no inputs visible, password never shown); clicking the name edit icon reveals the First/Last Name inputs + Save/Cancel, clicking Cancel reverts to display without calling `fetch`; clicking "Change Password" reveals the password inputs + Save/Cancel, Cancel clears both fields and hides them without calling `fetch`; name Save opens the confirm popup and, on confirm, calls `updateAccount` with no `newPassword` key and shows "Changes saved." above both sections, then collapses back to display mode with the updated name; password Save with matching fields opens the confirm popup and, on confirm, calls `updateAccount` with the *unchanged* current `firstName`/`lastName` plus the new password; mismatched password/confirm shows "Passwords do not match" **without** opening the confirm popup or calling `fetch`, and clears only the two password fields; a 409 response shows the conflict message; a 400 response with `errors.FirstName` surfaces on the First Name field (only reachable while the name section is in edit mode).
  - [x] `AccountApi.js`'s own ok/network-failure/malformed-body branches are covered indirectly through `Account.test.jsx`'s `fetch` mock scenarios (a 200 success, a network throw, a malformed 200 body) — confirmed no `AuthApi.test.js` exists as a standalone file; `AuthApi.js` is exercised the same indirect way through `AuthContext.test.jsx`/`NavBar.test.jsx`/`RequireRole.test.jsx`. Don't create a separate `AccountApi.test.js` file — it would break from the established convention.

- [x] **Task 7: Verify CI green**
  - [x] Branch as `story/1.7-self-service-account-management` from `main`.
  - [x] Run `dotnet test`, `npm run lint`, `npm run format:check`, `npm test` locally; confirm all green before push.
  - [x] Push and confirm both CI jobs pass.

### Review Findings

- [x] [Review][Patch] No current-password confirmation required to change password — `UpdateAccountRequest`/`AccountService.UpdateOwnProfile` let an authenticated caller set `NewPassword` with no re-entry of the existing password. Anyone holding a stolen-but-still-valid access token (XSS, leaked token, forgotten logged-in device) could silently take over the account's credentials. Not required by any AC or Dev Note in this story; Jack decided (2026-08-03) to patch it now rather than defer. Fixed: added required `CurrentPassword` to `UpdateAccountRequest`, verified via `IPasswordHasher.VerifyHashedPassword` in `AccountService.UpdateOwnProfile` (new `InvalidCurrentPasswordException` → 400), wired a "Current Password" field into `Account.jsx`'s password section with client-side required check, updated `AccountApi.js`/tests accordingly.
- [x] [Review][Patch] Task 6's checklist claims network-failure/malformed-body coverage that doesn't exist [frontend/src/pages/Account.test.jsx] — the story text says these are "covered indirectly through Account.test.jsx's fetch mock scenarios (a 200 success, a network throw, a malformed 200 body)," but the file only had 200-success/401/409/400 cases, no thrown-fetch or malformed-body case. Fixed: added both missing test cases; `AccountApi.js` now also explicitly treats a malformed/empty 200 body as a failure (`{ ok: false, status: null }`) instead of returning `{ ok: true, identity: null }`.
- [x] [Review][Patch] 401 mid-edit shows a generic error instead of a session-expired message [frontend/src/pages/Account.jsx:94-103] — if the access token expires/invalidates while the Account page is open, a save attempt fell into the generic `else` branch ("Something went wrong. Please try again.") instead of a clear re-auth prompt. Fixed: added an explicit `result.status === 401` branch setting "Your session has expired. Please sign in again."; covered by a new test.
- [x] [Review][Defer] Generic `catch (Exception)` with no logging [backend/BarbershopApi/Controllers/AccountController.cs:26] — deferred, pre-existing (identical pattern in `AuthController`'s three catch blocks).
- [x] [Review][Defer] `[StringLength(100)]` validates FirstName/LastName before `.Trim()` runs in the service [backend/BarbershopApi/Dtos/UpdateAccountRequest.cs:8,13] — deferred, pre-existing (shared with `RegisterRequest`; explicitly deliberate per this story's own Dev Notes, "AD consistency, not a new policy").
- [x] [Review][Defer] Validation regexes duplicated verbatim between `RegisterRequest`/`UpdateAccountRequest` rather than shared [backend/BarbershopApi/Dtos/UpdateAccountRequest.cs] — deferred, pre-existing, same rationale as above.
- [x] [Review][Resolved] `UpdateMe_two_concurrent_edits_...` test had no explicit synchronization barrier and was intermittently flaky [backend/BarbershopApi.Tests/AccountControllerTests.cs] — confirmed flaky in practice (2026-08-03) when Jack ran `dotnet test` locally and got a real, non-env-related failure. Fixed: replaced with `UpdateMe_on_stale_RowVersion_returns_409`, which reproduces the same optimistic-concurrency conflict deterministically via two separate `DbContext`s (same technique as `AccountServiceTests.UpdateOwnProfile_on_stale_RowVersion_throws_AccountConflictException`) driven through the real `AccountController` — no mocking, no timing dependency. Verified with 5 solo runs + 3 full-suite runs, all green.
- [x] [Review][Defer] `ConfirmPopup` closes before the async save resolves, no loading affordance during the in-flight window [frontend/src/components/ConfirmPopup.jsx:15-18] — deferred, UX polish only (double-submit itself is already blocked via `disabled={isSubmitting}` on all Save/Cancel buttons).
- [x] [Review][Defer] `RequireRole roles={['Customer', 'Barber', 'Admin']}` hardcodes the full `Role` enum to mean "any authenticated user" [frontend/src/App.jsx:28] — deferred, brittle if a 4th role is ever added, but matches this story's own Task 5 spec.
- [x] [Review][Defer] `AccountApi.js`'s `updateAccount` can return `{ok: true, identity: null}` on a malformed 200 body, which would crash `Account.jsx` reading `result.identity.firstName` [frontend/src/api/AccountApi.js:26,30] — deferred, not reachable today since `AccountController` only returns well-formed JSON on 200.

### Review Findings (Round 2 — reviewing the patches above)

- [x] [Review][Patch] Reject setting the new password to the same value as the current password, instead of silently succeeding — Jack decided (2026-08-03) to add an explicit check rather than defer. Fixed: `AccountService.UpdateOwnProfile` now verifies `newPassword` against the existing hash and throws a new `SameAsCurrentPasswordException` (→ 400 "New password must be different from your current password."); surfaced on the New Password field in `Account.jsx`.
- [x] [Review][Patch] Round-1's deferred-work.md entry about `AccountApi.js` malformed-body handling is now stale — this same patch round already fixed the `{ok: true, identity: null}` case it describes, but the entry was carried forward verbatim, contradicting the shipped code. Fixed: marked resolved in `deferred-work.md`.
- [x] [Review][Patch] `Account.jsx`'s 400-error routing conflates "current password is incorrect" with *any* title-only 400 lacking a field-errors dict [frontend/src/pages/Account.jsx:112-113] — a future business-rule 400 following the same `Problem()`+title convention would be silently mis-attributed to the Current Password field. Fixed: routing now matches on the two specific known titles (`CURRENT_PASSWORD_INCORRECT_TITLE`/`SAME_AS_CURRENT_PASSWORD_TITLE`) instead of the shape alone.
- [x] [Review][Patch] `CurrentPassword` has no length cap unlike `NewPassword` [backend/BarbershopApi/Dtos/UpdateAccountRequest.cs] — an arbitrarily large string flows straight into `VerifyHashedPassword` with no `[StringLength]`, unlike every other password field on this DTO and `RegisterRequest`. Fixed: added `[StringLength(128)]`.
- [x] [Review][Resolved] No rate limiting/lockout on the current-password verification branch [backend/BarbershopApi/Services/AccountService.cs] — a valid-token holder could brute-force the real password with no throttling. Fixed (2026-08-03, at Jack's request): added `PasswordChangePolicy` — same 5-per-15-min sliding window as `LoginPolicy`, counting both failed and successful password-change attempts, keyed by `{ip}:{accountId}`; plain name-only edits are exempted via `RateLimitPartition.GetNoLimiter`. Required moving `UseRateLimiter()` to after `SessionLivenessMiddleware` in `Program.cs` so the partition resolver can read the authenticated account id.
- [x] [Review][Defer] Backend gives an identical "Current password is incorrect." message whether the field was wrong or simply missing [backend/BarbershopApi/Services/AccountService.cs:18-24] — only matters for non-browser callers since the UI already blocks blank submission client-side.
- [x] [Review][Resolved] `AccountService.UpdateOwnProfile` mutates `FirstName`/`LastName` before the current-password check can throw [backend/BarbershopApi/Services/AccountService.cs:15-24] — currently harmless (no `SaveChanges` on the throw path) but a maintainability landmine. Fixed (2026-08-03, at Jack's request): reordered so both password checks (and hash computation) complete before any property on `account` is mutated; the three failure-path service tests now reload from a fresh `DbContext` and assert the name was never persisted.
- [x] [Review][Defer] No test covers `currentPassword` supplied without `newPassword` (silently ignored server-side) [backend/BarbershopApi.Tests/AccountServiceTests.cs] — low-value coverage gap.
- [x] [Review][Defer] `AccountApi.js`'s malformed-body guard reports `status: null` even when `response.ok` was `true` [frontend/src/api/AccountApi.js:31-33] — harmless today since nothing branches on it, but discards information.
- [x] [Review][Resolved] The 401 "session has expired" message is shown but nothing redirects/logs the user out [frontend/src/pages/Account.jsx:106-107] — they could retry indefinitely against a dead token. Fixed (2026-08-03, at Jack's request): `Account.jsx` now calls `logout()` and navigates to `/login` with the message passed via router state, matching `Register.jsx`'s existing pattern.
- [x] [Review][Resolved] Saving the password section with a filled Current Password but blank New/Confirm Password silently no-ops (server receives `newPassword: null`) and still shows "Changes saved." [frontend/src/pages/Account.jsx:63-68] — Fixed (2026-08-03, at Jack's request): `handleSavePasswordClick` now requires a non-empty `newPassword` before opening the confirm popup, showing "New password is required" instead.

### Review Findings (Round 3 — rate limiting, mutation ordering, name/password mutual exclusion, session redirect)

- [x] [Review][Patch] Frontend has no branch for HTTP 429 on password-change save [frontend/src/pages/Account.jsx:131-155] — falls through to the generic "Something went wrong" instead of the backend's specific "Too many attempts. Try again in a few minutes." message. Confirmed independently by all three review layers. Fixed: added a `result.status === 429` branch matching the 409 pattern; covered by a new test.
- [x] [Review][Patch] `PasswordChangePolicy`'s JSON body sniffing for `"newPassword"` is case-sensitive [backend/BarbershopApi/Program.cs] while ASP.NET Core's model binding is case-insensitive by default — a differently-cased key (e.g. `"NewPassword"`) would still bind and succeed as a real password change while silently evading the rate limit. Fixed: lookup now uses `EnumerateObject()` + `StringComparison.OrdinalIgnoreCase`.
- [x] [Review][Patch] "Edit name" icon and "Change Password" button lack `disabled={isSubmitting}` [frontend/src/pages/Account.jsx] — every other actionable control in the file has this guard. Fixed: both now have `disabled={isSubmitting}`.
- [x] [Review][Patch] `UpdateMe_on_stale_RowVersion_returns_409`'s comment overstates its coverage [backend/BarbershopApi.Tests/AccountControllerTests.cs] — claims it "drives it through the real AccountController" for "the 409 mapping," but it bypasses routing, `[Authorize]`, `[EnableRateLimiting]`, and `SessionLivenessMiddleware` entirely. Fixed: reworded to state precisely what it covers.
- [x] [Review][Patch] `CurrentPassword` is missing the same whitespace guard (`^\S+$`) that `NewPassword` and registration passwords already enforce [backend/BarbershopApi/Dtos/UpdateAccountRequest.cs]. Fixed: added `[RegularExpression(@"^\S+$")]`.
- [x] [Review][Patch] No test proves `PasswordChangePolicy` is scoped per-account [backend/BarbershopApi.Tests/AccountControllerTests.cs]. Fixed: added `UpdateMe_rate_limit_is_scoped_per_account_not_shared_across_accounts`.
- [x] [Review][Patch] No test proves a *successful* password change also consumes a rate-limit permit [backend/BarbershopApi.Tests/AccountControllerTests.cs]. Fixed: added `UpdateMe_successful_password_changes_also_count_toward_the_rate_limit`.
- [x] [Review][Patch] AD-5 in `project-context.md` doesn't mention the new `PasswordChangePolicy`. Fixed: added a note alongside the existing Login-policy entry.
- [x] [Review][Patch] Add a regression test confirming `/api/auth/login` still works and still enforces `LoginPolicy` correctly even when a valid bearer token is attached to the request. Fixed: added `Login_still_succeeds_and_still_rate_limits_when_request_carries_a_bearer_token` to `AuthControllerTests.cs`.
- [x] [Review][Defer] `AccountService`/`Account.jsx` always send the client's cached `user.firstName`/`user.lastName` on every PUT, including password-only saves — if the account's name changed elsewhere since this client's last login/refresh, a password-only save silently reverts it to the stale cached value. Pre-existing "whole-resource PUT" characteristic dating to the original story 1.7 implementation (every PUT has always required FirstName+LastName), not introduced by this round.
- [x] [Review][Defer] Rate-limit partition key `$"{ip}:{accountKey}"` shares the same IPv6-colon-ambiguity and IP-rotation-resets-throttle characteristics as the pre-existing `LoginPolicy` it deliberately mirrors — inherited, not new, and matches Jack's explicit "make it the same as sign in" request.
- [x] [Review][Defer] Raw request-body read in `PasswordChangePolicy` isn't wrapped in the same try/catch as the JSON parse — exact continuation of `LoginPolicy`'s existing pattern, not a new risk.
- [x] [Review][Dismiss] "Rate limiter now runs after `SessionLivenessMiddleware`'s DB check, defeating its purpose" — verified false alarm. The alternative (reading the JWT's `sub` claim without signature validation to avoid reordering) would let an attacker forge a token claiming a victim's account id purely to exhaust the victim's rate-limit bucket — a real targeted-DoS vector the current design avoids by relying on `SessionLivenessMiddleware`'s already-authenticated `HttpContext.Items["Account"]`. The one added cost (a single indexed-by-primary-key SELECT) is far cheaper than the operations the limiter still fully blocks (password hashing, the service's own additional DB reads/writes). Strengthened the code comment to make this reasoning explicit instead of just stating the ordering.
- [x] [Review][Dismiss] Login/Refresh's new dependency on middleware order — verified safe: neither endpoint attaches a bearer token from this app's own client, and `SessionLivenessMiddleware` only acts when `IsAuthenticated` is already true; closed with the regression test above instead of a design change.
- [x] [Review][Dismiss] Bundling the `AdminBootstrapServiceTests` env-var fix into this same diff — legitimate commit-hygiene note (unrelated concern, same diff), not a code defect; worth its own commit when we get to committing, not a patch.
- [x] [Review][Dismiss] Bare marker exceptions (`InvalidCurrentPasswordException`/`SameAsCurrentPasswordException`) with no custom message — matches the exact existing convention already used by `AccountConflictException`/`DuplicateEmailException` throughout this codebase.
- [x] [Review][Dismiss] Non-string `newPassword` JSON values evading the rate-limiter's sniffing — verified non-exploitable: a JSON number/bool can't deserialize into the DTO's `string?` property, so such a request 400s at model binding before any real password change could occur.
- [x] [Review][Dismiss] Removing the flaky `UpdateMe_two_concurrent_edits...` test without an equivalent full-pipeline replacement — by design; that test was flaky *because* it exercised the full pipeline non-deterministically. The mechanism is now covered deterministically at both the service and controller layers; re-adding a flaky full-pipeline version would reintroduce the exact problem already fixed.


### Architecture Compliance (must-follow, not optional)

- **AD-1 (layering)** — this story creates the "Account/Admin" domain's `Controller`/`Service` for the first time (the `Repository` already existed from Story 1.2). It is a sibling to `Auth`, not an extension of it — do not add this endpoint to `AuthController`. Epic 3's admin account-management stories will extend this **same** `AccountController`/`AccountService`/`AccountRepository` trio with admin-only endpoints, not create a new one.
- **AD-2 (self vs. admin password change)** — only an admin-driven password change (FR35, Epic 3) increments `SessionVersion`; this story's self-service change must not. [Source: ARCHITECTURE-SPINE.md #AD-2]
- **AD-16 (optimistic concurrency on Account)** — `RowVersion` conflict → `ProblemDetails` 409, first commit wins. The exact scenario named is "an admin editing an account at the same moment the account holder edits their own profile" — i.e., a future Epic 3 story racing this one. [Source: SOLUTION-DESIGN.md §4]
- **Error shape** — `ProblemDetails` (RFC 7807) via `Problem()`, consistent with every existing controller action; no hand-rolled error body.
- **Password hashing** — reuse `IPasswordHasher<Account>` (`PasswordHasher<T>`, bundled with .NET), exactly as `AuthService.Register`/`Login` already do. No new package.

### Previous Story Intelligence (from Story 1.6 and earlier)

- `SessionLivenessMiddleware` already re-derives the current `Account` from the DB every request and stashes it on `HttpContext.Items["Account"]` — pull the caller's account off that, exactly like `AuthController.Me()` does, rather than re-parsing the `sub` claim.
- `RequireRole.jsx` was built in Story 1.6 explicitly naming Story 1.7 as a future consumer; it needs no changes to be used here.
- `NavBar.jsx`'s "Account" menu item has pointed at `/account` since Story 1.5 with no route to receive it — this story is what finally builds the destination. No `NavBar` change needed.
- `FormSection.jsx` was explicitly held open for "Login (1.5) and Account (1.7)" reuse since Story 1.4 — confirmed still a trivial, unstyled wrapper. Story 1.7 still uses it as the page's outer container (per `DESIGN.md`'s tinted-card treatment naming Account explicitly), even though the page is no longer one continuously-editable form — see "UI Design Decision" below.
- The PascalCase 400-error-body convention (`errors.FirstName`, etc.) and the `Register.jsx` field-error-mapping pattern must be followed identically for consistency.
- `PlausibleEmailAttribute` (built for a future story) is **not** used here — email is read-only display only, no email validation applies to this story.

### UI Design Decision (Jack, 2026-08-03 — supersedes the original single-form draft)

The Account page is a **read-only profile view with two independent, toggleable edit sections**, not one always-editable form:

- Email: always visible, always read-only, no edit affordance.
- Name: view state shows "First Last" as text with an edit icon; editing reveals First Name + Last Name side by side with Save/Cancel underneath.
- Password: never displayed; a "Change Password" button reveals New Password + Confirm New Password with Save/Cancel underneath.

**Both sections' Save buttons open the confirm popup before committing** — FR28's "every edit here requires an explicit confirm step" makes no distinction between name and password edits, so neither section is exempt. This was an explicit product decision, not left to interpretation.

Consequences for implementation:
- One shared `ConfirmPopup` instance, gated by a `pendingAction` ('name' | 'password') state, rather than two separately-mounted popups — only one save can be pending confirmation at a time regardless.
- The backend contract (Task 1) is unchanged — a password-only save simply echoes back the current, unchanged `firstName`/`lastName` values already held in component state; a name-only save omits `newPassword`. Don't build partial-update semantics into the DTO/service for this.
- Cancel (on both sections) is a required affordance, not a nice-to-have — it's the only way to leave edit mode without saving, and resets fields to their last-saved values without an API call.
- No UX-design source (`DESIGN.md`/`EXPERIENCE.md`) describes this exact two-section layout — those docs predate this revision and only described a single combined form. Where this section's guidance and the UX docs disagree (e.g. field layout), this section wins; where they don't conflict (confirm-popup button semantics, validation-message color, "Changes saved." copy/placement), the UX docs still apply as documented elsewhere in this file.

### Testing Requirements

- Backend: xUnit.v3 + `WebApplicationFactory` (`SqliteApiFactory.CreateClient()`) against real SQLite — never mocked (NFR4, AD-4). Service-level tests instantiate `AccountService` directly against a real `DbContext` (still not a mock — same technique `AccountRepositoryTests` already uses for its stale-`RowVersion` test).
- Frontend: Vitest + jsdom + RTL + user-event; `vi.spyOn(globalThis, 'fetch')`, no MSW; real `AuthProvider` sign-in harness, matching `NavBar.test.jsx`/`RequireRole.test.jsx`.
- Fixture identity: `john@example.com` / `John` / `Smith` — established across six existing test files. **Never "Jack."**

### Project Structure Notes

- Backend new: `Dtos/UpdateAccountRequest.cs`, `Services/IAccountService.cs`, `Services/AccountService.cs`, `Services/AccountConflictException.cs`, `Controllers/AccountController.cs`, `BarbershopApi.Tests/AccountServiceTests.cs`, `BarbershopApi.Tests/AccountControllerTests.cs`. Modified: `Program.cs` (new DI registration only).
- Frontend new: `api/AccountApi.js`, `pages/Account.jsx`, `pages/Account.css`, `pages/Account.test.jsx`. Modified: `App.jsx` (one new route).
- No schema/entity/migration changes — `Account.FirstName`, `LastName`, `PasswordHash` all already exist (Story 1.2).

### References

- [Source: _bmad-output/planning-artifacts/epics.md §Story 1.7] — story statement, AC.
- [Source: _bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/ARCHITECTURE-SPINE.md #AD-1, #AD-2, #AD-16] — domain-trio layering, self-vs-admin `SessionVersion` rule, Account optimistic concurrency.
- [Source: _bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/SOLUTION-DESIGN.md §4, §7] — the admin-edit-races-self-edit scenario RowVersion/409 exists for; `ProblemDetails` error-shape list including account-edit conflicts.
- [Source: _bmad-output/planning-artifacts/prds/prd-bmad-learning-project-2026-07-21/prd.md FR1, FR28, FR29, FR35, FR41] — self-service edit scope, shared password-mismatch rule, profile-dropdown Account link, admin-vs-self password-change session distinction, concurrent-edit conflict handling.
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-bmad-learning-project-2026-07-23/DESIGN.md — form-section, modal, confirm-popup, input tokens] and [EXPERIENCE.md — IA table, State Patterns "Password mismatch", Component Patterns "Profile icon dropdown"] — component reuse, validation-message color (supersedes project-context.md's stale plain-text note), "Changes saved." placement/copy, confirm-popup button semantics for a non-destructive save.
- [Source: _bmad-output/implementation-artifacts/1-6-server-side-role-gating-and-protected-routing.md §Dev Notes] — `SessionLivenessMiddleware`/`HttpContext.Items["Account"]` pattern, `RequireRole` readiness for this story, `MeResponse` shape reuse.
- [Source: _bmad-output/implementation-artifacts/1-5-sign-in-sign-out-and-first-admin-bootstrap.md, 1-4-customer-self-registration.md] — `AuthApi.js`/`AuthContext.jsx` fetch and state conventions, `Register.jsx`'s password-mismatch and PascalCase-error precedents, `FormSection` reuse note.
- [Source: backend/BarbershopApi/{Controllers/AuthController.cs, Services/AuthService.cs, Repositories/AccountRepository.cs, Entities/Account.cs}, frontend/src/{App.jsx, context/AuthContext.jsx, components/NavBar.jsx, components/Modal.jsx, components/ConfirmPopup.jsx, api/AuthApi.js}] — current (pre-story) state of every file this story touches or extends.
- [Source: project-context.md §Language-Specific Rules, §Critical Implementation Rules] — AD-16 concurrency mechanism, `ProblemDetails` error shape, PascalCase/camelCase conventions.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (BMad Amelia dev agent)

### Debug Log References

- Local `dotnet test`/`npm run lint`/`npm run format:check`/`npx vitest run` were transiently blocked by the machine's endpoint security software mid-session (file-load `Access is denied` on freshly-built `testhost.dll`/`node`/MSBuild task assemblies). Confirmed via a `git stash -u` re-run against the unmodified pre-story baseline that this was a pre-existing environment issue, not caused by this story's changes. Retried later in the session once unblocked — all four commands passed clean (see Completion Notes).
- Prettier's `--write` reformatted `AccountApi.js`, `Account.jsx`, and `Account.test.jsx` (wrapping/indentation only, no logic change) after the initial `format:check` flagged them.

### Completion Notes List

- Implemented the `Account`/`AccountService` domain trio (AD-1) reusing the existing `AccountRepository` unchanged. Self-service password changes deliberately do not bump `SessionVersion` (AD-2); stale `RowVersion` conflicts translate to `AccountConflictException` → 409 (AD-16).
- Backend tests: `AccountServiceTests.cs` (4 tests) and `AccountControllerTests.cs` (6 tests), all using real SQLite via `SqliteApiFactory`/direct `DbContext` instantiation — no mocking, per AD-4/NFR4.
- Frontend: `AccountApi.js` fetch wrapper mirrors `AuthApi.js` conventions; `Account.jsx` implements the two-independent-edit-section design (name, password) with a shared `ConfirmPopup` gated by `pendingAction`, per Jack's 2026-08-03 UI design decision superseding the original single-form draft. `FormSection` does not forward `className`, so a plain inner `<div className="account-page">` is nested inside it instead (no shared-component change).
- Wired `/account` behind `RequireRole roles={['Customer', 'Barber', 'Admin']}` in `App.jsx`; `NavBar`'s existing profile-dropdown link now resolves.
- `Account.test.jsx` (9 tests) covers the full AC surface: disabled/non-editable email, collapsed-by-default name/password sections, independent Cancel flows with no fetch call, name-save and password-save confirm-popup flows with request-body assertions, password-mismatch client-side-only handling, 409 conflict message, and 400 field-error surfacing.
- Full verification, run after the local environment's AV/security-software interference cleared mid-session: `dotnet test` — 70/70 passed; `npm run lint` — clean; `npm run format:check` — clean (after one Prettier `--write` pass); `npx vitest run` — 85/85 passed (13 files).
- **Post-implementation UI correction (Jack, live review):** the Task 4 Dev Notes decision to render email as a `disabled` `Input` was superseded after seeing it rendered — it looked like an editable box despite being disabled, inconsistent with the read-only convention the name display already established. Changed to plain text (`<span className="input-field__label">Email</span>` + `<span>{user.email}</span>`) matching the name section's pre-edit treatment; `Account.test.jsx`'s email test updated accordingly (`getByText`/`queryByLabelText` instead of `getByLabelText`/`toBeDisabled`).
- **Post-implementation layout fix:** the side-by-side First/Last Name inputs overflowed the `form-section` card during edit mode — flex items default to `min-width: auto`, which for `<input>` elements refuses to shrink below their intrinsic content width. Added `min-width: 0` to `.account-page__name-fields > *` and made the split uneven (First Name `flex: 2`, Last Name `flex: 3`) per Jack's request, both in `Account.css`.
- Re-ran `npx vitest run`, `npm run lint`, `npm run format:check` after these two fixes — 85/85 tests, lint clean, format clean.
- **Post-implementation app-shell layout fix (Jack, live review, out of story scope):** the footer floated up under the content on short pages instead of sitting at the bottom of the viewport, app-wide (not specific to Account). Wrapped `NavBar`/`main`/`Footer` in a new `.app-shell` flex column (`min-height: 100vh`) in `App.jsx`, with `main` taking `flex: 1` to push the footer down — new `App.css`. Confirmed `App.test.jsx` (structural assertions only, no DOM-shape coupling) still passes.
- Re-ran `npx vitest run`, `npm run lint`, `npm run format:check` after the app-shell fix — 85/85 tests, lint clean, format clean.
- Committed (`5b6ddf8`) and pushed to `story/1.7-self-service-account-management`. GitHub Actions run #34 completed `success` on both jobs: **Frontend (Vite/React)** (lint, format check, test) and **Backend (.NET)** (restore, build, test).

### File List

**New:**
- `backend/BarbershopApi/Dtos/UpdateAccountRequest.cs`
- `backend/BarbershopApi/Services/AccountConflictException.cs`
- `backend/BarbershopApi/Services/IAccountService.cs`
- `backend/BarbershopApi/Services/AccountService.cs`
- `backend/BarbershopApi/Controllers/AccountController.cs`
- `backend/BarbershopApi.Tests/AccountServiceTests.cs`
- `backend/BarbershopApi.Tests/AccountControllerTests.cs`
- `frontend/src/api/AccountApi.js`
- `frontend/src/pages/Account.jsx`
- `frontend/src/pages/Account.css`
- `frontend/src/pages/Account.test.jsx`
- `frontend/src/App.css` (app-shell sticky-footer layout)

**Modified:**
- `backend/BarbershopApi/Program.cs` (new `IAccountService` DI registration)
- `frontend/src/App.jsx` (new `/account` route behind `RequireRole`; wrapped layout in `.app-shell` for sticky footer)
