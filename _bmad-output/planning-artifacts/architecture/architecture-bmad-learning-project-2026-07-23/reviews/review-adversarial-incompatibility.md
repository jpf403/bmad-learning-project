---
name: 'Adversarial Incompatibility Review — Barbershop Appointment Scheduler Architecture Spine'
type: review
target: '{planning_artifacts}/architecture/architecture-bmad-learning-project-2026-07-23/ARCHITECTURE-SPINE.md'
created: '2026-07-24'
---

# Adversarial Incompatibility Review

Method: for each AD, construct two hypothetical implementers who each obey the AD's letter but produce incompatible artifacts. Every finding below is either a hole in an existing AD (too vague/underspecified) or a missing AD.

## Finding 1 (highest severity) — Account "delete" (FR40) has no schema support and no AD, so hard-delete and soft-delete implementations are equally "compliant" and mutually incompatible

FR40: *"Deleting a barber account cancels and deletes that barber's future (not-yet-occurred) appointments; past/Finished appointments are retained as history."* This applies to both barber and customer account deletion (FR40, same handling as demoting a barber per FR18).

The ERD in the spine gives `ACCOUNT` no `DeletedAt`/`IsActive`/`IsDeleted` field, and no AD discusses account deletion mechanics at all — AD-7 fixes PK strategy but is silent on soft- vs hard-delete; AD-8 defines soft-delete (`CancelledAt`) only for `APPOINTMENT`.

Concrete divergence:
- **Dev A (Account/Admin service)** reads FR40 literally: "delete" means `DELETE FROM Account WHERE Id=X`. To satisfy "past appointments retained," they add `ON DELETE SET NULL` on `Appointment.BarberId`/`CustomerId` (the only way to hard-delete an Account row that historical Appointments still reference). Historical appointment rows survive, but the barber's/customer's name is now unrecoverable — a regression against "retained as history," and the `Email` unique constraint is freed immediately, letting a new registrant reuse a deleted customer's email address.
- **Dev B (same trio, different day)** instead adds an undocumented `IsDeleted`/`DeletedAt` column to `Account` — a soft-delete the ERD never specified — hides the row from login and admin listing queries, but keeps it for FK integrity so historical appointment joins still resolve to a real name. Under this design the `Email` unique index permanently blocks re-registration with that email, and login must add an `IsDeleted` filter that Dev A's queries never expected.

Both are defensible readings of AD-7/AD-8/FR40; the resulting schemas, login behavior, and email-uniqueness semantics are flatly incompatible, and whichever one shipped first defines behavior the other silently violates the first time someone deletes and re-registers with the same email. **This needs its own AD** specifying the soft-delete field on `ACCOUNT`, its interaction with the `Email` unique constraint, and exactly what "delete" does to future vs. past appointments (row-delete vs. `CancelledAt`-stamp).

## Finding 2 — AD-2 pins the status code for session-liveness failure (401) but leaves role-insufficiency failure unspecified, so two controllers can disagree on 401 vs 403

AD-2's rule: "a JWT's SessionVersion claim is compared... mismatch → 401." It says nothing about what to return when the re-derived Role is valid (session alive) but doesn't have permission for the endpoint — e.g., a signed-in Customer hitting a Barber- or Admin-only route.

Concrete divergence:
- **Dev A**, writing `AccountController` (Admin-only endpoints), extends AD-2's literal "→ 401" pattern to all authorization failures, since AD-2 is the only guidance on record: insufficient role → 401.
- **Dev B**, writing `BookingController`'s barber-only cancel endpoint, follows standard ASP.NET/REST convention (authenticated-but-forbidden = 403) since nothing in the spine contradicts it.

The frontend, built against one assumption (e.g., "401 always means bounce to login"), will incorrectly redirect an authenticated-but-unauthorized barber to the sign-in page on Dev A's endpoints while showing a correct "not allowed" message on Dev B's — an observable, user-facing inconsistency that both developers can honestly claim followed AD-2.

## Finding 3 — `GET /api/auth/me` response shape is never pinned down anywhere in the spine

AD-3 mandates the endpoint ("bootstraps identity for the frontend since the cookie is unreadable") but no DTO shape appears in AD-3, the Consistency Conventions table, or the Structural Seed's `Dtos/` folder description.

Concrete divergence: the backend dev implementing `/api/auth/me` and the frontend dev consuming it in `App`'s bootstrap effect work from independent assumptions about the payload — e.g., backend ships `{ id, email, role }` (matching only the fields AD-2 cares about), while frontend code (written first, against the PRD's account fields) expects `{ id, email, firstName, lastName, role, sessionVersion }` for display in a profile menu. Nothing in the spine would flag this as wrong on either side until integration.

## Finding 4 — `Role` has no defined value domain, so AD-6's seed and AD-2's per-request check can silently disagree on casing/spelling

The ERD declares `Role` as a bare `string` with no enum or allowed-values list anywhere in the spine. AD-6 (bootstrap) seeds "an admin account" and AD-2 (per-request check) compares "the account's current Role" — both assume a shared string vocabulary that is never written down.

Concrete divergence: **Dev A** (writing the `IHostedService` seeder, AD-6) hardcodes `Role = "Admin"`. **Dev B** (writing the `[Authorize]`-equivalent role-check middleware for AD-2), working from the PRD prose which lowercases roles ("customer, barber, admin"), compares against `"admin"`. The seeded admin now fails every admin-gated request — a bug that passes code review on both sides because neither AD constrains the string values, and nothing centralizes them (no shared enum/constants file is mandated by AD-1's structural seed).

## Finding 5 — Appointment-read/schedule-view ownership is ambiguous between the Booking and Account/Admin trios, risking duplicated (and divergent) AD-8 status computation

AD-1 assigns one trio per domain concept: "Auth, Booking, Account/Admin." Three different features all read the same `Appointment` rows and must apply AD-8's "Finished" computation identically: (a) a customer's own upcoming/past list (FR24), (b) a barber's own full-day schedule (FR13), (c) an admin's oversight of *every* barber's schedule. AD-1 doesn't say which trio owns (b) and (c) — both are plausibly "Booking" (it's appointment data) or plausibly "Account/Admin" (it's a role-scoped, self/oversight view analogous to account management).

Concrete divergence: **Dev A** implements the barber's own schedule view and the customer's upcoming list both inside `BookingService`, computing `Finished` once via a shared helper against EST "now" (AD-12). **Dev B**, working the Account/Admin trio in parallel, reasonably reads "admin oversees every barber's schedule" as an Account/Admin-domain feature and implements a second, independent `AdminService` method that re-derives EST "now" and re-implements the same Upcoming/Finished comparison from scratch. Nothing in the spine forces these two to share code or even a status vocabulary — Dev A's DTO field might be `status: "Finished" | "Upcoming"`, Dev B's might be `isPast: bool` — so the same appointment can display as "Finished" on the customer page and differently-shaped/differently-timed on the admin oversight page if the two EST-boundary implementations drift (e.g., one uses `>=` and the other `>` at the exact cutover instant).

## Minor/secondary observations

- **AD-4** allows Playwright e2e "optional" with no rule on what it may or may not mock ("mocks nothing" is stated, but scope/ownership of e2e specs vs. Vitest specs isn't assigned, so overlapping or gap-leaving coverage between two devs is possible).
- **Consistency Conventions table** mandates `ProblemDetails`/`Problem()` for custom errors but doesn't pin the `type`/`title` strings for known conflict cases (booking conflict AD-9, stale cancellation, account edit/delete race FR41) — two developers could each invent their own `title` text for "someone already booked this slot" vs. "someone already cancelled this," and frontend error-message matching (if any) would need per-endpoint special-casing.
- **Structural Seed's `Dtos/` folder** has no rule requiring one DTO per entity/shape reused across controllers — nothing prevents `BookingController` and `AccountController` from each defining their own ad hoc `AppointmentDto` with different field names/order for the same underlying row (this is the concrete mechanism behind Finding 5).

## Verdict

The spine is solid on backend layering, auth/session mechanics, and DB concerns that are naturally single-owner (rate limiting, PK strategy, CI, timezone authority). Its gaps cluster where the same conceptual entity — `Account.Role`, `Appointment`'s computed status, "delete" semantics — is read or mutated from more than one plausible location, and where an AD specifies a mechanism for one case (SessionVersion → 401) without generalizing it to the sibling case (role-insufficiency, deletion, schedule-reads). Finding 1 (account deletion vs. history retention) is the one most likely to produce a real, hard-to-detect production bug (email uniqueness/history-loss) rather than just a cosmetic inconsistency.
