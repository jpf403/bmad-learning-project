---
baseline_commit: 869f0b77e1e0a69266326774cb6dcc59083bfaa9
---

# Story 1.4: Customer Self-Registration

Status: ready-for-dev

## Story

As a visitor,
I want to self-register a customer account with my email, a password (typed twice), first name, and last name,
so that I can access booking features.

## Acceptance Criteria

1. **Given** a not-yet-used email and matching passwords, **when** submitted on Register, **then** a new `Account` row is created with `Role=Customer` and a hashed password (`PasswordHasher<Account>`), and the user is redirected to Login (route `/login` — not yet built, see Dev Notes) with "Account created. Sign in to continue." displayed (FR1).
2. **Given** an email already registered, **when** submitted, **then** registration is rejected with "That email is already in use." and the email field is retained (FR1).
3. **Given** an email with no `@` or no domain `.` (e.g. "testbademail"), **when** submitted, **then** registration is rejected with an error and the email field is retained (FR1).
4. **Given** mismatched password/confirm-password fields, **when** submitted, **then** "Passwords do not match" is shown, only the two password fields clear, and all other entered fields are retained (FR1).
5. **Given** the Register form, **when** rendered, **then** it uses the double-entry password `Input` pattern inside the tinted `{components.form-section}` card (UX-DR5, UX-DR6).

## Tasks / Subtasks

- [ ] **Task 1: Shared plausible-email validation** (AC: #3; reused by Stories 1.7, 3.1, 3.3, 3.4 — build it once, correctly)
  - [ ] Create `backend/BarbershopApi/Dtos/PlausibleEmailAttribute.cs`: a `ValidationAttribute` requiring at least one `@` and a domain segment containing `.` (regex: `^[^@\s]+@[^@\s]+\.[^@\s]+$`). Placed in `Dtos/` (not a new top-level folder) to stay within the Architecture's locked Structural Seed (`Controllers/Services/Repositories/Entities/Dtos/Data` only) — see Dev Notes.
  - [ ] **Do not use `[EmailAddress]`** — verified against current .NET behavior: `EmailAddressAttribute.IsValid` only checks for a single `@` not at the first/last character position; it does **not** require a domain dot, so `"testbademail@x"` would incorrectly pass it. This is exactly the gap AC#3 tests for.
  - [ ] Default `ErrorMessage = "Enter a valid email address."` (no upstream-specified copy for this exact state — EXPERIENCE.md's State Patterns table only specifies copy for the *duplicate*-email state, not the *format*-invalid one; this is a proposed on-brand default, plain and specific per the locked voice register).

- [ ] **Task 2: Register DTOs** (AC: #1-#3)
  - [ ] `backend/BarbershopApi/Dtos/RegisterRequest.cs`: `Email` (`[Required]`, `[PlausibleEmail]`), `Password` (`[Required]`), `FirstName` (`[Required]`), `LastName` (`[Required]`). No `ConfirmPassword` field — password-match checking is purely client-side (Task 5), since the confirm value is never persisted and re-checking it server-side adds no data-integrity guarantee (unlike AD-14's booking-date re-validation, which *is* server-revalidated because it protects real data integrity).
  - [ ] `backend/BarbershopApi/Dtos/RegisterResponse.cs`: `Id`, `Email`, `FirstName`, `LastName` (no `PasswordHash`, no `Role` — nothing the frontend needs, since registration does not auto-sign-in).

- [ ] **Task 3: `AuthService.Register`** (AC: #1, #2) — first content in `Services/`; this is the **Auth** domain trio (AD-1) that Story 1.5 will extend with Login/Logout/Refresh/Me — do not create a separate `RegistrationService`.
  - [ ] Create `backend/BarbershopApi/Services/DuplicateEmailException.cs` (plain `Exception` subclass, no special members needed).
  - [ ] Create `backend/BarbershopApi/Services/IAuthService.cs` / `AuthService.cs`, constructor-injecting `IAccountRepository` and `IPasswordHasher<Account>`.
  - [ ] `Task<Account> Register(RegisterRequest request)`:
    1. `FindByEmail(request.Email)` — if a match exists, `throw new DuplicateEmailException()`.
    2. Build `new Account { Email = request.Email, FirstName = request.FirstName, LastName = request.LastName, Role = Role.Customer }` (leave `SessionVersion`/`DeletedAt`/`RowVersion` at their EF defaults — same defaults Story 1.2 already tests for).
    3. `account.PasswordHash = _passwordHasher.HashPassword(account, request.Password)` — `PasswordHasher<TUser>.HashPassword` only uses the user object as a generic type parameter, so passing the not-yet-persisted account is fine.
    4. `try { return await _accountRepository.Create(account); } catch (DbUpdateException) { throw new DuplicateEmailException(); }` — defense-in-depth mirroring AD-9's check-then-insert-plus-DB-backstop pattern (Story 1.2's partial unique index on `Email WHERE DeletedAt IS NULL` is the backstop here), for the race where two registrations for the same email land between the check and the insert. This is a deliberate reuse of an established pattern, not new scope.
  - [ ] Register `IPasswordHasher<Account>` → `PasswordHasher<Account>` and `IAuthService` → `AuthService` as `Scoped` in `Program.cs`. **No NuGet package needed for `PasswordHasher<T>`** — `Microsoft.AspNetCore.Identity` ships inside the ASP.NET Core shared framework that `Microsoft.NET.Sdk.Web` already references; project-context.md already notes this as "bundled." Do not add an `Identity`-family `PackageReference`.

- [ ] **Task 4: `AuthController`** (AC: #1, #2, #3) — first content in `Controllers/`.
  - [ ] Create `backend/BarbershopApi/Controllers/AuthController.cs`: `[ApiController] [Route("api/auth")]`.
  - [ ] `[HttpPost("register")] public async Task<IActionResult> Register(RegisterRequest request)`:
    - Model-invalid (missing field, bad email format) → automatic 400 `ValidationProblemDetails` from `[ApiController]` — **no manual code**, that's the point of Task 1/2's attributes.
    - Success → `try { var account = await _authService.Register(request); return StatusCode(201, new RegisterResponse(account.Id, account.Email, account.FirstName, account.LastName)); }`.
    - `catch (DuplicateEmailException) { return Problem(statusCode: StatusCodes.Status409Conflict, title: "That email is already in use."); }`.
  - [ ] Delete `Controllers/.gitkeep` and `Services/.gitkeep` (same pattern Story 1.2 used for `Entities/`/`Repositories/`).

- [ ] **Task 5: Backend tests** (AC: #1-#3) — reuse `SqliteApiFactory` (it's already a `WebApplicationFactory<Program>` subclass — `factory.CreateClient()` works as-is, no new fixture needed) in a new `AuthControllerTests.cs`.
  - [ ] `Register_with_new_email_creates_customer_account` — POST valid payload, assert 201, then verify via `IAccountRepository.FindByEmail` that a row exists with `Role == Role.Customer`.
  - [ ] `Register_hashes_password_not_stored_plaintext` — assert persisted `PasswordHash != request.Password`, and that `new PasswordHasher<Account>().VerifyHashedPassword(account, storedHash, "the-plaintext-password")` returns `PasswordVerificationResult.Success`.
  - [ ] `Register_with_duplicate_email_returns_409` and `Register_with_differently_cased_duplicate_email_returns_409` (reuses Story 1.2's case-insensitive `FindByEmail`).
  - [ ] `Register_with_missing_at_sign_returns_400` (e.g. `"testbademail"`) and `Register_with_no_domain_dot_returns_400` (e.g. `"test@bademail"`).
  - [ ] `Register_with_missing_required_field_returns_400` (omit `FirstName`).
  - [ ] **Inspect and record the actual JSON key casing** of the 400 response's `errors` dictionary (e.g. `errors.Email` vs `errors.email`) via one of the above tests' raw response body — do not assume either casing. `Register.jsx` (Task 6) must key its field-error lookup off whatever this test observes, since ASP.NET Core's `ModelState`-driven validation-error keys and the app's normal camelCase JSON convention are populated by different code paths and are not guaranteed to agree.

- [ ] **Task 6: Frontend API base URL** (AC: #1-#3) — first real `fetch` call in the app; centralize now so Stories 1.5/1.7/2.x/3.x don't each invent their own base URL.
  - [ ] Create `frontend/src/api/ApiConfig.js` exporting `export const API_BASE_URL = 'https://localhost:7113'` — the **HTTPS** launch profile port (`backend/BarbershopApi/Properties/launchSettings.json`), not the HTTP port (`5290`). Program.cs's `UseHttpsRedirection()` would otherwise 307-redirect every HTTP-port request to HTTPS, adding an avoidable extra hop/CORS-preflight-on-redirect risk. If the browser fetch fails with a TLS error during manual verification, run `dotnet dev-certs https --trust` locally (a one-time machine setup step, not a code fix).
  - [ ] Delete `frontend/src/api/.gitkeep`.

- [ ] **Task 7: `AuthApi.js`** (AC: #1-#3) — PascalCase filename per project-context.md's "non-component JS (utilities, API wrapper modules) also PascalCase" rule.
  - [ ] Create `frontend/src/api/AuthApi.js` exporting `async function registerAccount({ email, password, firstName, lastName })`: `fetch(`${API_BASE_URL}/api/auth/register`, { method: 'POST', credentials: 'include', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email, password, firstName, lastName }) })` (`credentials: 'include'` per AD-13's blanket rule for every fetch touching the `/api/auth` surface, even though this particular call sets no cookie yet).
  - [ ] On `response.ok`, return `{ ok: true }`. On failure, parse the JSON body (`response.json()`, guarded with `.catch(() => null)` in case the body isn't JSON) and return `{ ok: false, status: response.status, problem }`. Do not throw — `Register.jsx` branches on the return value, not a try/catch, matching a plain `fetch`-stubbing test style (AD-4, no MSW).

- [ ] **Task 8: Build the `form-section` component** (AC: #5) — first page to need it; DESIGN.md already tokenizes `{components.form-section}` but Story 1.1 didn't build it (only Button/Input/NavBar-shell/Footer/Modal/ConfirmPopup).
  - [ ] Create `frontend/src/components/FormSection.jsx` + `.css`: a simple wrapper — `{colors.neutral}` fill, no border, `{rounded.lg}` corners, `{spacing.6}` padding (`--color-neutral`, `--rounded-lg`, `--spacing-6` — all already in `tokens.css`). Renders `children` inside a `<div className="form-section">`. No test file needed beyond what `Register.test.jsx` already exercises (it's a trivial styled wrapper, same tier as `Footer`).
  - [ ] This component will be reused as-is by Login (1.5) and Account (1.7) — do not build Register-specific styling into it.

- [ ] **Task 9: Build the Register page** (AC: #1-#5)
  - [ ] Create `frontend/src/pages/Register.jsx` + `Register.css` + `Register.test.jsx`.
  - [ ] Controlled fields via existing `Input` component: `email`, `firstName`, `lastName`, `password`, `confirmPassword` — plain `useState` per field (no form library; matches this project's "plain fetch + React state" convention, same spirit for forms).
  - [ ] On submit: `e.preventDefault()`. If `password !== confirmPassword`: set an error caption ("Passwords do not match") on both password `Input`s via their `error` prop (already wired to `{colors.error}` styling in `Input.css` — no new CSS needed), clear `password`/`confirmPassword` state, **do not call the API** (a pure client-side check needs no round trip). Otherwise call `registerAccount(...)`.
  - [ ] On `{ ok: true }`: `navigate('/login', { state: { message: 'Account created. Sign in to continue.' } })`.
  - [ ] On `{ ok: false, status: 409 }`: set the email `Input`'s `error` prop to `"That email is already in use."`; retain every field's value (controlled inputs already do this by default — just don't clear state).
  - [ ] On `{ ok: false, status: 400 }`: read the email-format error out of `problem.errors` using whatever key casing Task 5 determined, set it on the email `Input`'s `error` prop; retain all fields.
  - [ ] Wrap the form in `<FormSection>` (Task 8); every field uses `Input`; submit button is `<Button variant="primary" type="submit">Register</Button>`.
  - [ ] **Do not build a Login page or a `/login` route in this story** — AC#1's redirect target does not exist yet (Story 1.5). This exactly mirrors Story 1.3's Home-CTA situation: the route is not registered, so the running app shows a blank page at `/login` until 1.5 lands. This is expected, not a bug to fix here.

- [ ] **Task 10: Wire the `/register` route** (AC: #1-#5)
  - [ ] In `App.jsx`, add `<Route path="/register" element={<Register />} />` — the exact path Story 1.3 already locked in its Route Naming Convention table. Do not add `/login` (still not this story's route to add, per Task 9).
  - [ ] Wire `NavBar`'s existing `Register` button (`<Button variant="primary">Register</Button>`, currently inert) to navigate to `/register` — either `<Link>`-wrap it consistent with Story 1.3's `Home`/`About` pattern, or use `useNavigate()` on click; either is fine, but it must become a real, reachable link (not still a bare `<Button>` with no destination).

- [ ] **Task 11: Frontend tests** (AC: #1-#5)
  - [ ] `Register.test.jsx`: renders all five fields inside the form-section wrapper; submitting matching-but-new email navigates to `/login` with the confirmation message (assert via a stub `<Route path="/login" element={<div>{/* read location.state.message */}</div>} />` inside `<MemoryRouter>` + real `<Routes>`, same pattern Story 1.3 established for `Home.test.jsx` — no mocked `useNavigate`). Stub `global.fetch` via `vi.fn()` (AD-4, no MSW) for the success/409/400 cases.
  - [ ] Mismatched-password case: assert the error caption text, that both password fields are now empty, and that `firstName`/`lastName`/`email` retain their previously entered values — **and that `fetch` was never called** for this case (Task 9's "no round trip" behavior).
  - [ ] Duplicate-email (409) and bad-format (400) cases: assert the email field's error caption and that its value is retained.
  - [ ] `NavBar.test.jsx`: update/add an assertion that "Register" is now a real link to `/register` (it was previously excluded from the "not a link" assertion set — recheck Story 1.3's exact NavBar test structure before editing, since `Register`/`Sign In` were never part of either the routed-links or inert-links lists there, only `Home`/`About` vs. `Schedule Appointment`/`My Schedule`/`Admin Panel`).

- [ ] **Task 12: Verify CI green**
  - [ ] Branch as `story/1.4-customer-self-registration` from `main`.
  - [ ] Run `dotnet test` (backend), `npm run lint`, `npm run format:check`, `npm test` (frontend) locally; push and confirm both CI jobs pass before merging (AD-11).

## Dev Notes

### Architecture Compliance (must-follow, not optional)

- **AD-1 (layering)** — this is the **first** story to add real content to `Controllers/` and `Services/`. One trio for the **Auth** domain concept: `AuthController` → `AuthService` → (existing) `AccountRepository`. Story 1.5 extends this exact same `AuthController`/`AuthService` pair with Login/Logout/Refresh/Me — do not create a second Auth-flavored controller/service.
- **AD-2 (`Role` as fixed enum)** — the created account's `Role` is `Role.Customer` from the existing enum (Story 1.2); never a string literal.
- **AD-4/NFR4 (testing)** — `AuthControllerTests` must hit real HTTP endpoints via `SqliteApiFactory.CreateClient()` against real SQLite — this is the first *controller-level* (HTTP) test in the repo; Story 1.2's tests only exercised the repository directly. No mocked `DbContext`, no mocked `IAccountRepository`.
- **AD-13 (CORS/credentials)** — no CORS change needed: the backend's CORS policy already allows `http://localhost:5173` with `AllowCredentials()` (Story 1.1); it's independent of which backend port (`5290` vs `7113`) the frontend targets, since CORS is checked against the request's `Origin` header, not the destination port.
- **Not this story's job:** session/token issuance (register does **not** auto-sign-in, per EXPERIENCE.md's Registration-success state — the user is redirected to Login, not logged in), rate limiting (AD-5 is login-only, Story 1.5), and the `/login` page/route itself (Story 1.5).

### Why No Server-Side Password-Confirmation Check

AD-14 establishes a pattern of *always* server-revalidating client-side-convenience checks — but only where skipping the server check would let bad data reach storage or violate a real invariant (booking dates: AD-14; email format/duplicate: this story's Task 1-4). Password confirmation doesn't fit that pattern: `confirmPassword` is never persisted, is compared only to `password` itself, and a client that lies about the comparison just ends up creating an account with whatever single password value it actually sent — no data-integrity or security consequence follows either way. Treat this as purely a client-side UX check (Task 9), not a gap.

### Auth-State / Routing Placeholder (temporary, same shape as Story 1.3's)

Story 1.3 established the precedent for building a route whose destination doesn't exist yet: Home's CTA already navigates to `/login` today and renders blank there. This story adds a second navigator to that same not-yet-built destination (post-registration redirect), carrying a message via router `state`. **Story 1.5 is responsible for**: building the `/login` route/page, and reading `location.state?.message` to render the "Account created. Sign in to continue." banner. Nothing to do here beyond passing that state correctly and testing it via a stub route (Task 11), exactly as `Home.test.jsx` stubbed `/login` in Story 1.3.

### File Placement: Why `PlausibleEmailAttribute` Lives in `Dtos/`, Not a New `Validation/` Folder

The Architecture's Structural Seed locks exactly six backend folders (`Controllers/Services/Repositories/Entities/Dtos/Data`) with no `Validation/` or similar. Since this attribute exists purely to decorate DTO properties and every future consumer (Stories 1.7, 3.1, 3.3, 3.4) will apply it the same way, `Dtos/` is the closest fit without introducing an unlocked seventh folder.

### Project Structure Notes

- `Controllers/.gitkeep` and `Services/.gitkeep` — deleted this story (first real content), same treatment `Entities/.gitkeep`/`Repositories/.gitkeep` got in Story 1.2.
- `frontend/src/api/.gitkeep` — deleted this story (first real content: `ApiConfig.js`, `AuthApi.js`).
- `App.jsx` currently has `<Routes>` with only `/` and `/about` (Story 1.3). This story adds exactly one more: `/register`. Do **not** add `/login` (see Auth-State placeholder above).
- `NavBar.jsx` currently renders `Sign In`/`Register` as plain `<Button>`s with no navigation wired (Story 1.1's static shell, untouched through Story 1.3). This story wires `Register` only — `Sign In` stays inert until Story 1.5 builds `/login`.
- No `Login.jsx`/`Account.jsx` exist yet and none should be created here.

### Testing Requirements

- Backend: xUnit.v3 + `WebApplicationFactory` (via the existing `SqliteApiFactory`) against real SQLite — first HTTP-level (`factory.CreateClient()`) usage of that fixture; it already supports this with zero changes.
- Frontend: Vitest + jsdom + React Testing Library + user-event; stub `fetch` directly (`vi.fn()`/`vi.spyOn`), no MSW (AD-4). Navigation assertions use real `<MemoryRouter>` + `<Routes>` with a stub destination, never a mocked `useNavigate` — the pattern `Home.test.jsx` already established in Story 1.3.

### Previous Story Intelligence (from Stories 1.2 and 1.3)

- Story 1.2 built `Account`/`Role`/`AccountRepository` with case-insensitive, trimmed email normalization already applied inside `Create`/`FindByEmail`/`Update` — **do not** re-normalize email in `AuthService`; the repository already handles it. `Create` throws `DbUpdateException` on a duplicate active email (the partial unique index), which Task 3's `try/catch` relies on directly.
- Story 1.2's `SqliteApiFactory` is a full `WebApplicationFactory<Program>` subclass — already usable for `CreateClient()` HTTP tests, not just `CreateDbContext()`. No new test fixture needed.
- Story 1.3 introduced React Router v8 (`"react-router"`, not `"react-router-dom"`) and established the routing convention table (`/register` reserved for this story) and the "stub destination route in tests, no mocked `useNavigate`" pattern — both reused directly here.
- Story 1.3 also flagged (deferred, still unresolved as of this story): `NavBar` doesn't collapse/wrap below ~640px width. Out of scope here too — do not fix it as a drive-by change.

### Git Intelligence Summary

Recent commits (`ebd095d`/`35ed372` Story 1.2 → `3894539`/`869f0b7` Story 1.3) show every prior story following implement → self-verify CI green → review → patch → `done`, entirely within its own short-lived `story/{epic}.{story}-{slug}` branch. Follow the same shape here.

### Latest Tech Info (verified at story-creation time)

- **`EmailAddressAttribute` is not sufficient for AC#3** — its current implementation only requires exactly one `@` not at the first/last character; it does not require a domain dot. Task 1's custom `PlausibleEmailAttribute` is required, not optional polish.
- **`PasswordHasher<TUser>` requires no additional `PackageReference`** — it ships in `Microsoft.AspNetCore.Identity`, part of the ASP.NET Core shared framework already referenced by `Microsoft.NET.Sdk.Web` (confirmed against this project's `BarbershopApi.csproj`, which has no Identity package today and doesn't need one added).
- **Frontend→backend base URL is a new architectural gap this story resolves**, not something pre-existing: no Vite proxy is configured (`vite.config.js` has none), and AD-13's CORS discussion presupposes direct cross-origin `fetch` calls (a proxy would make CORS unnecessary) — confirming direct-fetch-to-absolute-URL is the intended pattern, not a proxy. Task 6 locks this in as `https://localhost:7113` (the HTTPS launch profile) for the first time; every later story's API module should import `API_BASE_URL` from `ApiConfig.js` rather than hardcoding a new one.

### References

- [Source: _bmad-output/planning-artifacts/epics.md §Story 1.4] — story statement, AC, FR1's amended email-format clause
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-bmad-learning-project-2026-07-23/DESIGN.md frontmatter `components.form-section`, `components.input`; §Components ("Form inputs", "Form-section card")] — form-section token values, double-entry password pattern, `{colors.error}` treatment
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-bmad-learning-project-2026-07-23/EXPERIENCE.md §State Patterns ("Registration success", "Duplicate email", "Password mismatch")] — exact default copy for each state
- [Source: _bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/ARCHITECTURE-SPINE.md#AD-1, #AD-2, #AD-4, #AD-13, #Structural-Seed] — layering, Role enum, testing strategy, CORS, locked folder set
- [Source: _bmad-output/implementation-artifacts/1-2-account-entity-and-repository.md] — `AccountRepository`/`SqliteApiFactory` contracts and email-normalization behavior this story builds on
- [Source: _bmad-output/implementation-artifacts/1-3-home-and-about-pages.md §Route Naming Convention, §Auth-State Placeholder] — routing precedent this story extends
- [Source: backend/BarbershopApi/Repositories/AccountRepository.cs, Entities/Account.cs, Program.cs; backend/BarbershopApi.Tests/SqliteApiFactory.cs] — current (pre-story) state of files this story builds on
- [Source: frontend/src/components/Input.jsx, Input.css, Button.jsx; frontend/src/styles/tokens.css; frontend/src/App.jsx, NavBar.jsx] — current (pre-story) state of files this story modifies/reuses
- [Source: backend/BarbershopApi/Properties/launchSettings.json] — confirmed backend dev ports (5290 http / 7113 https)
- [Source: project-context.md §Language-Specific Rules; §Code Quality & Style Rules (file naming); §Critical Don't-Miss Rules]

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
