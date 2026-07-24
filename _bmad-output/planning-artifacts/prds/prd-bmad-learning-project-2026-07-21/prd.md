---
title: Barbershop Appointment Scheduler — PRD
status: final
created: 2026-07-21
updated: 2026-07-24
---

# Barbershop Appointment Scheduler — PRD

## Overview

This PRD scopes a small web application for booking barbershop appointments — customers pick a barber, a date, and an available time slot; barbers see their own day; an admin oversees every barber's schedule and manages accounts. Three roles (customer, barber, admin) share one .NET backend, React frontend, and SQLite database, gated behind hashed-password authentication.

The goal is straightforward: give customers a way to book a time slot in advance — with real, current availability — so they don't have to sit and wait during their busy days; give barbers visibility into their own day without digging through everyone else's; and give the shop admin oversight across all barbers plus easy control over who has staff access — without anyone touching the database directly.

## Goals & Success Metrics

| ID | Metric | Counter-metric |
|---|---|---|
| SM1 | 100% of role-gated pages/actions reject unauthenticated or wrong-role access, verified by automated tests | Gate holds at the request layer, not just hidden UI |
| SM2 | Booking a slot writes to SQLite and the slot immediately disappears from availability | Test exercises a real DB write, not a mocked repository |
| SM3 | Double-booking a slot is rejected with an error message; taken slots never appear as options | Two near-simultaneous submissions for the same slot resolve to exactly one booking |
| SM4 | CI runs the automated suite on every push, keeping the codebase in an always-deployable state | Suite is capable of catching a real regression, not rubber-stamping every change |
| SM5 | Code stays organized by responsibility so behavior lives in a predictable location, per manual review (see NFR6) | Not achieved via premature abstraction for appearance |
| SM6 | Layout renders cleanly on both mobile and desktop viewport widths | Hover-dependent actions (home CTA) confirmed single-tap functional on touch, not double-tap |
| SM7 | Authentication is secure end-to-end and sessions persist correctly across page navigation | A signed-out session (e.g., after logout, or via back-button) cannot still reach a protected page or action |
| SM8 | A new customer can go from landing on the home page to a confirmed booking in under a minute, without having to sit and wait | Speed isn't achieved by skipping real validation — the double-booking guard (FR10) and every other check still run on the fast path |

## User Journeys

**UJ-1: Customer registers and books an appointment (signed-in).**
A new customer lands on the home page and selects Create Account, entering email, password, first name, and last name to register. On a return visit, the customer signs in with email/password. Once signed in, they navigate to the Schedule Appointment page, select their barber, open the calendar widget to pick a date (past dates disabled, future dates capped at 30 days out), open the time dropdown to select an available slot, and submit — the appointment is booked under their account.

**UJ-2: Barber views their schedule.**
A barber signs in and is routed directly to their own schedule (not the home page). The view defaults to today and lists every fixed time slot from 9:00 AM to 4:30 PM in 30-minute increments; booked slots show the customer's name, open slots show as available. Back/forward arrows navigate to other days. The barber can also book an appointment via the Schedule Appointment page, and sees only their own appointments, never another barber's.

**UJ-3: Admin oversees schedules and manages accounts.**
An admin signs in and lands on the same schedule view as a barber, plus a Select Barber dropdown to view any individual barber's day. A separate Admin Panel lets the admin search for and select any customer or barber account, then edit any of that account's fields — email, first name, last name, permission level (customer/barber), or password (set directly, no reset-link mechanism). A Create Account button on the same page lets the admin create new barber accounts directly. There is exactly one admin account in the system, acting as the shop's owner — it isn't manageable through this panel.

**UJ-4: Sign out.**
Any signed-in user — customer, barber, or admin — can sign out, ending their session.

## Functional Requirements

### Authentication & Accounts
- FR1: Any visitor can self-register a customer account from the home page (email, password typed twice to confirm, first name, last name). A registration with an email already in use is rejected with an error message directing the user to use a different email; email is the unique key for an account. If the password and confirm-password fields don't match, submission is rejected with a "passwords do not match" error prompting the user to retype just those two fields — other entered fields are not cleared. This same mismatched-confirmation handling applies everywhere a password is typed twice in this PRD (FR1, FR18, FR19, FR28).
- FR2: Registered users sign in with email/password; passwords stored hashed. A failed attempt — whether the email isn't registered or the password is wrong — returns the same generic error (e.g., "Invalid email or password") in both cases, so failed logins never reveal whether a given email exists in the system.
- FR3: Unauthenticated users cannot reach booking, barber dashboard, or admin pages; the Schedule Appointment nav button is hidden entirely for signed-out users. Signed-in users attempting to reach a page or action outside their role (e.g., a customer or barber hitting an admin-only URL, a barber viewing another barber's schedule) are rejected server-side the same way — this is not limited to the unauthenticated case. Navigation and UI controls for pages a user's role cannot access are hidden entirely for that role, not merely blocked after the fact.
- FR4: On sign-in, customers land directly on the Schedule Appointment page; barbers and admins are routed directly to their schedule view.
- FR23: Any signed-in user can sign out. Sign-out ends the session server-side, so it takes effect everywhere that account is signed in — other open tabs or devices are signed out too, not just the tab where Logout was clicked.
- FR28: On a new Account page, a signed-in user can edit their own first name, last name, and password (typed twice to confirm) — but not email. Changing your own password here does not end your own current session. Every edit here requires an explicit confirm step before it takes effect.
- FR29: Top-right nav area: signed-out users see Login and Register buttons; signed-in users see a profile icon that opens a dropdown with Account (link to the Account page) and Logout.
- FR31: On first application startup, if no admin account exists yet, the system creates exactly one from server-side configuration (not through the normal registration UI). This is the only admin account that will ever exist in the system (see FR34) — it acts as the shop's owner.
- FR35: When an admin changes another account's password (FR18), every active session for that account is immediately terminated, forcing re-sign-in. This is distinct from FR28's self-service password change, which does not end the user's own session. When an admin changes another account's permission level, the affected session is not force-ended — a page refresh on the user's end is enough to pick up the new permission level and enforce it going forward; no full re-login is required.

### Booking (shared by all roles)
- FR5: Any signed-in user can access the Schedule Appointment page. A signed-out user clicking a Schedule/booking CTA (e.g., on the home page) is redirected to the Login page instead.
- FR6: User selects a barber. If no barber accounts exist, the selector shows an explicit "No barbers available" state instead of an empty or broken dropdown.
- FR7: User selects a date via calendar widget. Past dates are disabled; future dates are capped at 30 days out (roughly a month). Weekends are not selectable — the shop is treated as closed Saturday and Sunday, so only weekdays are bookable.
- FR8: Time dropdown shows only open slots for the chosen barber/date, drawn from fixed 9:00 AM–4:30 PM, 30-minute increments. When the selected date is today, any slot within 30 minutes of the current time is excluded (e.g., at 8:59 AM, the 9:00 AM slot is no longer offered).
- FR9: Submitting creates the appointment under the signed-in user's account and transitions to a confirmation screen (e.g., "Appointment booked with [Barber] at [Time]") — this also naturally prevents an accidental duplicate submission of the same booking, since the bookable form is no longer on screen once a booking succeeds. A signed-in user cannot hold two appointments at the same date/time across different barbers — the same customer double-booking themselves is blocked the same way a slot conflict between two different customers is.
- FR10: A submission for a slot taken between load and submit is rejected: an error message is shown on the user's screen, and the backend does not create a second appointment for that slot and does not error out or crash (double-booking guard).
- FR24: The Schedule Appointment page lists the signed-in user's own upcoming (not-yet-occurred) booked appointments at the bottom of the page. Past appointments are retained in the database, not deleted, but are not displayed in this list at all — the visible list is upcoming-only.
- FR25: From that list, a user can cancel one of their own appointments, freeing the slot back to availability.

### Barber Dashboard
- FR11: On sign-in, barber lands on their own schedule, defaulted to today.
- FR12: Back/forward arrows navigate to other days.
- FR13: View lists the full fixed time range; booked slots show the customer's name, open slots show as available. Weekend days show as closed (no bookable slot grid), consistent with FR7.
- FR14: Barber sees only their own appointments — never another barber's; enforced server-side, not just by hiding the option in the UI.
- FR26: From their schedule view, a barber can cancel any appointment shown there, freeing the slot back to availability.

### Admin Dashboard & Account Management
- FR15: Admin lands on the same schedule view as barbers, plus a Select Barber dropdown to view any barber's day; the dropdown defaults to the first barber (auto-selected) rather than an empty state.
- FR16: A separate Admin Panel hosts account management.
- FR17: Admin can search for a customer or barber account by name or email, with partial matches shown, then select one from the results. There is exactly one admin account in the system; it is not part of this searchable/manageable set.
- FR18: Admin can edit any field of a selected customer or barber account — email, first name, last name, permission level (customer/barber), or password (set directly, typed twice to confirm). There is exactly one admin account in this system (see FR31/FR34); no account can ever be promoted to admin, and the admin account itself is not a target of this action — admin management here applies only to customer and barber accounts. An email edit that collides with another account's email is rejected with an error message, same as FR1. Every account edit requires an explicit confirm step before it takes effect. Demoting a barber to customer cancels and deletes that barber's future (not-yet-occurred) appointments; past/Finished appointments are retained as history (see FR40 for the same handling on account deletion).
- FR19: Admin can create new barber accounts directly via a Create Account button (password typed twice to confirm, same as FR1/FR18/FR28). This action creates barber accounts only — it cannot create another admin account, since there is exactly one admin account in this system (see FR34).
- FR27: From their schedule view (including via the Select Barber dropdown), an admin can cancel any appointment the same way a barber does.
- FR30: Every cancellation action (FR25, FR26, FR27) requires an explicit confirm step before the appointment is actually cancelled. Cancellation is transactional and idempotent: it succeeds exactly once, and a second cancellation attempt on an already-cancelled appointment (whether from a race between two actors or a stale UI) is rejected with an error message rather than silently succeeding or erroring ambiguously. The same handling applies to a cancellation racing a new booking for the freed slot.
- FR34: There is exactly one admin account in this system, acting as the shop's owner (created per FR31). No account can ever be promoted to admin, the admin account can never be demoted to another role, and it can never be deleted — the Admin Panel's edit, delete, and permission-level actions (FR17–FR19, FR40) apply only to customer and barber accounts, never to the admin account itself.
- FR40: Admin can delete a customer or barber account from the same Admin Panel used to edit and create accounts, gated behind an explicit confirm step. Deleting a barber account cancels and deletes that barber's future (not-yet-occurred) appointments; past/Finished appointments are retained as history (same handling as demoting a barber, FR18).
- FR41: Concurrent edits or deletes targeting the same account (e.g., an admin edit racing the user's own self-edit via FR28) are handled transactionally the same way as double-booking: the first to commit succeeds, the second gets a conflict error message rather than silently overwriting or corrupting the account.

### Shared Pages
- FR20: Home page includes hero, tagline, CTA, and at least one hover interaction.
- FR21: About page (static content).
- FR22: Layout is responsive and renders cleanly across mobile and desktop viewport widths — no broken/overflowing layouts.

## Non-Functional Requirements & Quality/DORA Practices

- **NFR1 (Security)**: Passwords stored via industry-standard salted hashing (never plaintext); role checks enforced server-side, not just hidden in the UI; authenticated sessions maintained securely (mechanism deferred to Architecture — see addendum). Login attempts are rate-limited to mitigate brute-force guessing (specific threshold deferred to Architecture). All dates/times are interpreted and compared in a fixed EST timezone (server-authoritative, not the client's local clock) — this governs "today," "past dates," and the 30-minute booking cutoff alike.
- **NFR2 (Data integrity)**: Booking writes are transactional enough that two near-simultaneous submissions for the same slot can't both succeed. The same guarantee extends to cancellations (cancel-vs-cancel and cancel-vs-book races) and to account edits/deletes (edit-vs-edit, edit-vs-delete races) — the first action to commit wins, the second gets an error message, never silent corruption or double effect.
- **NFR3 (Responsiveness)**: Layout adapts cleanly across mobile and desktop widths; no hover-dependent action lacks a working single-tap touch equivalent.
- **NFR4 (Automated testing)**: Test suite covers DB CRUD, auth, and role permissions against a real (not mocked) SQLite instance.
- **NFR5 (CI/CD)**: CI pipeline runs the full suite on every push; a red pipeline means the codebase is not currently deployable, by design.
- **NFR6 (Maintainability)**: Code is organized by responsibility (e.g., one controller/service per role area or domain concept, no god-classes) so a given behavior lives in a predictable, discoverable location — assessed via manual code review at completion, since this isn't automatable, not by a passing test.
- **NFR7 (Deployment target)**: Runs locally only; no production hosting or public deploy target required.

## Out of Scope / Non-Goals

- Payment processing
- Email/SMS reminders or notifications of any kind
- Self-service password reset flow (admin can directly change any account's password, covering this need without a reset-link/email mechanism)
- Multi-location support
- Guest booking (unauthenticated booking) — possible same-day addition, not committed; revisit before Architecture if it gets added
- An actual deploy target or public hosting — runs locally only; CI keeps the codebase always-deployable, nothing is actually deployed anywhere
- Audit trail / logging of admin account edits
- Password strength/complexity requirements (no minimum enforced — email uniqueness is the only account-level constraint)

## Deliverables & Timeline

The working app — scoped as above, runnable locally, with CI wired up on GitHub.

**Constraints:**
- Solo build, no other contributors.
- Soft personal target: finished by Friday, 2026-07-31.
