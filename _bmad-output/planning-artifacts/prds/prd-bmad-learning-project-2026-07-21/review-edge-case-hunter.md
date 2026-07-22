---
title: Edge Case Hunter Review — Barbershop Appointment Scheduler PRD
source: prd.md, addendum.md
method: bmad-review-edge-case-hunter (exhaustive path enumeration, unhandled paths only)
generated: 2026-07-22
---

# Edge Case Hunter — Findings

Scope: `prd.md` (full document), `addendum.md` (context only, not treated as normative spec).
Method: mechanical walk of every branching path and boundary condition named or implied by the FRs/NFRs/User Journeys. Only **unhandled** paths are listed — anything the PRD already specifies behavior for was discarded silently.

Each finding: location in `prd.md`, the trigger condition (the branch/boundary), a minimal spec-language sketch that would close the gap, and the potential consequence if left unhandled.

```json
[
  {
    "location": "prd.md:47 (FR1)",
    "trigger_condition": "Registration submitted with an email already in use",
    "guard_snippet": "FR1a: Registration rejects a duplicate email with a clear error; no account is created.",
    "potential_consequence": "Undefined behavior on collision — silent overwrite, crash, or duplicate accounts sharing an email"
  },
  {
    "location": "prd.md:47 (FR1)",
    "trigger_condition": "Typed-twice password fields don't match on registration",
    "guard_snippet": "FR1b: Mismatched confirmation is rejected before submission with a clear error.",
    "potential_consequence": "User registers with a password different from what they believe they set, locking themselves out"
  },
  {
    "location": "prd.md:48 (FR2)",
    "trigger_condition": "Sign-in with wrong password vs. sign-in with unregistered email",
    "guard_snippet": "FR2a: Both cases return the same generic 'invalid email or password' error (no user-enumeration signal); no lockout/rate-limit policy is defined for repeated failures.",
    "potential_consequence": "Attacker can enumerate valid emails, or brute-force a password with no throttling"
  },
  {
    "location": "prd.md:49 (FR3)",
    "trigger_condition": "Authenticated customer directly navigates (URL) to a barber or admin page",
    "guard_snippet": "FR3a: A signed-in user whose role doesn't match a page's required role is redirected/blocked server-side, distinct from the unauthenticated case already covered.",
    "potential_consequence": "FR3 only names unauthenticated visitors; a logged-in customer hitting a barber/admin route by URL is a role-boundary gap the PRD never resolves"
  },
  {
    "location": "prd.md:50, 56 (FR4, FR5)",
    "trigger_condition": "Signed-out user clicks a booking CTA, is redirected to Login, then signs in",
    "guard_snippet": "FR5a: On successful sign-in from a CTA-triggered redirect, the user returns to the Schedule Appointment page rather than the generic FR4 default landing.",
    "potential_consequence": "User loses their original intent/context after login, landing somewhere other than where they were trying to go"
  },
  {
    "location": "prd.md:57 (FR6)",
    "trigger_condition": "No barber accounts exist (all demoted/deleted) when a customer opens the booking page",
    "guard_snippet": "FR6a: Barber selector defines an explicit empty state ('no barbers available') rather than an empty/broken dropdown.",
    "potential_consequence": "Booking flow presents a dead-end control with no explanation"
  },
  {
    "location": "prd.md:58 (FR7)",
    "trigger_condition": "\"Past dates disabled\" boundary is evaluated against an unspecified clock (server vs. client, timezone/DST, clock skew)",
    "guard_snippet": "FR7a: Date validity is computed from server time in a fixed timezone, not the client's local clock.",
    "potential_consequence": "A user near a timezone/DST boundary can book, or be blocked from booking, 'today' inconsistently with what the server enforces"
  },
  {
    "location": "prd.md:58 (FR7)",
    "trigger_condition": "\"No forward-looking limit\" has no upper bound at all",
    "guard_snippet": "FR7b: State explicitly whether an arbitrarily distant future date (e.g., year 9999) is intended to be bookable, or add a practical cap.",
    "potential_consequence": "Unbounded date range is either a deliberate choice or an unreviewed gap — currently indistinguishable"
  },
  {
    "location": "prd.md:59 (FR8)",
    "trigger_condition": "Booking for today after the current time has already passed some slots",
    "guard_snippet": "FR8a: When the selected date is today, slots earlier than the current time are excluded from the open-slots list.",
    "potential_consequence": "Customer can book (and a barber could see) an appointment scheduled in the past relative to now"
  },
  {
    "location": "prd.md:59 (FR8), 68 (FR13)",
    "trigger_condition": "A day the shop is closed (e.g., Sunday, holiday) — no concept of barber/shop non-working days exists",
    "guard_snippet": "FR8b: Define whether every day always exposes the full 9:00–4:30 grid, or whether closed days/exceptions are modeled.",
    "potential_consequence": "Customers can book, and barbers/admins will see, appointment slots on days the shop may not actually be open"
  },
  {
    "location": "prd.md:60-61 (FR9, FR10)",
    "trigger_condition": "Same user double-clicks/double-submits the booking form for the same slot",
    "guard_snippet": "FR9a: Submission is idempotent per user+slot (e.g., button disabled after first click, or server dedupes) distinct from the cross-user race FR10 already covers.",
    "potential_consequence": "A single user could end up with two bookings for the same slot, or a confusing duplicate-error on their own resubmission"
  },
  {
    "location": "prd.md:61 (FR10), 89 (NFR2)",
    "trigger_condition": "A slot is freed by a cancellation (FR25/26/27) at the same instant another user's booking request targets it",
    "guard_snippet": "NFR2a: Extend the transactional guarantee to cancel-then-book races, not only submission-vs-submission races, so a cancel and a concurrent book resolve deterministically.",
    "potential_consequence": "NFR2's wording ('two near-simultaneous submissions') doesn't cover a cancel racing a book; outcome for that interleaving is unspecified"
  },
  {
    "location": "prd.md:62 (FR24)",
    "trigger_condition": "A user's own-appointments list grows without bound (no forward booking limit per FR7, no stated retention/filter)",
    "guard_snippet": "FR24a: Specify whether the list shows only upcoming appointments, only a recent window, or is paginated once it grows large.",
    "potential_consequence": "Page could render an ever-growing, unfiltered list mixing years of past and future appointments"
  },
  {
    "location": "prd.md:63, 70, 78 (FR25, FR26, FR27)",
    "trigger_condition": "Two actors (e.g., the customer and the barber, or the barber and the admin) cancel the same appointment near-simultaneously",
    "guard_snippet": "FR30a: Cancellation re-validates the appointment still exists/is active at confirm-time; a second cancel attempt on an already-cancelled appointment returns a clear error rather than succeeding silently or erroring ambiguously.",
    "potential_consequence": "Double-cancel race has no defined resolution, unlike the double-book race which FR10/NFR2 explicitly cover"
  },
  {
    "location": "prd.md:63 (FR25), 70 (FR26)",
    "trigger_condition": "Cancelling an appointment whose time has already started or already passed",
    "guard_snippet": "FR25a: State whether cancellation of an already-occurred/in-progress appointment is permitted or blocked.",
    "potential_consequence": "A customer or barber could 'cancel' (and free the slot for rebooking) an appointment that already happened"
  },
  {
    "location": "prd.md:69 (FR14)",
    "trigger_condition": "A barber directly invokes the 'view/cancel another barber's schedule' capability that the admin's Select Barber dropdown (FR15) uses",
    "guard_snippet": "FR14a: Explicitly require server-side enforcement that a barber-role request for any schedule other than their own is rejected, not just relying on the general NFR1 statement.",
    "potential_consequence": "FR14 is the one property flagged as most important to hold under real stakes, yet the PRD never ties it to a specific server-side check the way FR3's unauthenticated case is"
  },
  {
    "location": "prd.md:75-76 (FR17, FR18)",
    "trigger_condition": "An admin edits their own account through the Accounts tab and lowers their own permission level, or demotes the last remaining admin account",
    "guard_snippet": "FR18a: Either block an admin from demoting themself/the last admin, or explicitly accept the possibility of a zero-admin system state.",
    "potential_consequence": "Self-service admin editing (FR17/18) combined with no safeguard could leave the system with no admin account able to restore access"
  },
  {
    "location": "prd.md:76 (FR18)",
    "trigger_condition": "Admin changes the permission level of a user who currently has an active/valid signed-in session",
    "guard_snippet": "FR18b: Specify whether a role change takes effect on the user's next sign-in only, or forcibly invalidates their current session immediately.",
    "potential_consequence": "Given the addendum's JWT leaning (role baked into the token), a demoted user's live token could keep granting the old role's access until it naturally expires"
  },
  {
    "location": "prd.md:76 (FR18)",
    "trigger_condition": "Admin edits an account's email to a value already used by another account",
    "guard_snippet": "FR18c: Duplicate-email edits are rejected with a clear error, mirroring the registration-time uniqueness gap (FR1a).",
    "potential_consequence": "Two accounts could end up sharing one email, breaking login lookup by email"
  },
  {
    "location": "prd.md:76 (FR18)",
    "trigger_condition": "Admin demotes a barber (to customer/admin) who has existing future appointments booked under the barber role",
    "guard_snippet": "FR18d: State what happens to a demoted barber's existing/future appointments (kept as historical record, auto-cancelled, or blocked from demotion while appointments exist).",
    "potential_consequence": "Orphaned appointments could reference a barber identity that no longer holds barber permissions, with no defined handling"
  },
  {
    "location": "prd.md:77 (FR19)",
    "trigger_condition": "Admin-created barber/admin account's password field — FR19 doesn't mention a typed-twice confirmation, unlike FR1/FR18/FR28",
    "guard_snippet": "FR19a: Confirm whether the Create Account form requires password confirmation like every other password-setting flow in the PRD, or is intentionally single-entry.",
    "potential_consequence": "Inconsistent password-entry UX across the app, and a higher chance of an admin fat-fingering a new account's password unnoticed"
  },
  {
    "location": "prd.md:51, 53 (FR23, FR29)",
    "trigger_condition": "User is signed in across multiple tabs/devices and signs out in one",
    "guard_snippet": "FR23a: Specify whether sign-out invalidates the session/token server-side (affecting all tabs/devices) or only clears local client state in the tab where logout was clicked.",
    "potential_consequence": "Under the addendum's token-based leaning, a 'signed-out' user's other open tab or a copied bearer token could keep working until natural token expiry"
  },
  {
    "location": "prd.md:52 (FR28), 76 (FR18)",
    "trigger_condition": "A user edits their own account (FR28) at the same moment an admin edits that same account (FR18)",
    "guard_snippet": "FR28a: Define a conflict resolution rule (last-write-wins, optimistic-concurrency rejection, etc.) for concurrent edits to the same account from two different entry points.",
    "potential_consequence": "One of the two concurrent edits could be silently lost with no indication to either party"
  },
  {
    "location": "prd.md:53 (FR29), Goals table row 7",
    "trigger_condition": "A session/token expires organically while the user is mid-session on a protected page (not via logout or back-button)",
    "guard_snippet": "FR29a: Specify the in-page behavior when a token expires during an active session (silent redirect to Login, blocked action with re-auth prompt, etc.).",
    "potential_consequence": "The Goals table only guarantees a signed-out session can't reach a protected page after logout/back-button — natural mid-session expiry is a distinct branch left unaddressed"
  },
  {
    "location": "prd.md:73 (FR15)",
    "trigger_condition": "Admin's initial page load before any barber is chosen in the Select Barber dropdown",
    "guard_snippet": "FR15a: Specify the default view on first load (no barber selected/empty state, vs. auto-selecting the first barber alphabetically).",
    "potential_consequence": "Undefined initial-state rendering the first time an admin opens the schedule view in a session"
  },
  {
    "location": "prd.md:57 (FR6), 60 (FR9)",
    "trigger_condition": "Same customer books overlapping/identical time slots with two different barbers on the same date",
    "guard_snippet": "FR9b: State whether a customer is allowed to hold simultaneous appointments with different barbers at the same time, or whether that's blocked.",
    "potential_consequence": "A customer could end up double-booked against themselves with no error, since the double-booking guard (FR10) is scoped per barber/slot, not per customer"
  }
]
```

## Notes

- The addendum's auth-session leaning (JWT-style bearer tokens, no refresh strategy specified) is the direct source of several findings above (logout-invalidation, mid-session expiry, role-change-while-logged-in) — these are PRD-level behavioral gaps regardless of which session mechanism Architecture ultimately picks, since the PRD's own Goals table makes a security guarantee ("a signed-out session ... cannot still reach a protected page or action") that the FRs don't fully operationalize for the token-expiry and multi-tab cases.
- Concurrency handling is explicit and strong for the create-vs-create booking race (FR10, NFR2). The PRD does not extend the same explicit treatment to cancel-vs-book, cancel-vs-cancel, or account-edit-vs-account-edit races, despite the project's stated "real stakes" full-rigor treatment.
- No findings are reported for paths the PRD already resolves — e.g., unauthenticated access (FR3), the create-create booking race (FR10/NFR2), touch/hover parity (NFR3), or the confirm-step requirement itself (FR30) as a control, only its confirm-vs-concurrent-cancel interaction.
