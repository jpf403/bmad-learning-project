---
title: Adversarial Review — Barbershop Appointment Scheduler PRD
reviewed_document: prd.md
context_document: addendum.md
review_type: cynical / adversarial (bmad-review-adversarial-general)
date: 2026-07-22
---

# Adversarial Review: Barbershop Appointment Scheduler PRD

**Scope reviewed:** `prd.md` (the capability-level spec). `addendum.md` was read only as context for technical leanings (auth session mechanism, testing tooling) and is not itself a review target.

**Stance:** This review assumes the PRD is guilty of gaps until proven innocent. Findings below are ranked roughly by how much downstream damage they'd cause if carried unresolved into UX and Architecture — not by how they were discovered.

---

## Findings

### 1. Bootstrap problem: no path to create the first admin account
**Severity: High**

FR1 permits only self-registration of *customer* accounts. FR19 permits only an *admin* to create barber/admin accounts. There is no FR, note, or Open Item describing how the very first admin account comes into existence. As written, the system is unbootstrappable through its own UI — the first admin has to be inserted some other way (seed migration, direct DB insert, a hidden setup step), none of which is specified. This is exactly the kind of gap that gets discovered by whoever writes Architecture and then bounces back to the PRD author. It should be resolved now, not discovered later.

### 2. No model for shop-closed days, holidays, or barber unavailability
**Severity: High**

FR13 states the barber view "lists the full fixed time range" (9:00 AM–4:30 PM, 30-min increments) — implicitly for every date, every day of the week, forever. Nothing in the PRD models:
- Days the shop is closed (e.g., Sundays, holidays)
- A barber's day off, vacation, or sick day
- Any admin control to block off time

Combined with FR7's "no forward-looking limit" on booking dates, a customer can book a real slot on a day the shop has no intention of being open, with no mechanism to prevent it and no mechanism for staff to un-book it in bulk (only individual cancellation, FR25–27, exists). This is a core scheduling-domain gap, not a nice-to-have — it directly contradicts the goal of "real, current availability."

### 3. JWT leaning creates a stale-privilege window that NFR1 doesn't account for
**Severity: High**

The addendum leans toward token-based (JWT) sessions rather than server-side cookie sessions. NFR1 requires "role checks enforced server-side, not just hidden in the UI" — but says nothing about what happens to an *already-issued* token when FR18 lets an admin change that same user's role or password mid-session. Classic JWT failure mode: if role is baked into the token's claims, a user demoted from barber to customer (or an account whose password was just changed by an admin because of a suspected compromise) keeps their old privileges until the token expires, because there's no server-side revocation list implied anywhere. Given this app's entire admin-recovery story ("admin can directly change any account's password" in lieu of self-service reset) depends on that change taking effect *immediately*, this gap directly undermines a stated design decision. This needs an explicit NFR (short token TTL + refresh, or a revocation/version check) before Architecture locks in the JWT approach.

### 4. "Last admin" self-lockout is unguarded
**Severity: Medium-High**

FR18 lets an admin edit any account's permission level, including — as written — presumably their own account or the only other admin's. Nothing prevents an admin from demoting themselves, or demoting every other admin, leaving zero admin accounts. Since there is no self-service password reset and no documented DB-repair procedure, this is an unrecoverable dead end reachable through normal UI use, not an obscure edge case.

### 5. "Open slot" is not defined relative to the current time on today's date
**Severity: Medium**

FR8 says the time dropdown shows "only open slots for the chosen barber/date." It never states whether slots earlier than the current wall-clock time should be excluded when the chosen date is today. As written, a customer loading the booking page at 3:00 PM could see and successfully book a 9:00 AM slot for today. This is a basic real-world booking rule that's conspicuously absent.

### 6. Account deactivation/deletion is neither in-scope nor explicitly out-of-scope
**Severity: Medium**

The Out of Scope list is fairly thorough (payments, notifications, password reset, multi-location, guest booking, deploy target, audit trail) but never mentions account deactivation or deletion. Admin can create and edit accounts, but there's no way to handle a barber who leaves the shop other than changing their role (which, per Finding #2's absence of an availability model, still leaves their historical/future slots ambiguous). Silence here reads as an oversight rather than a decision — it should be an explicit line in Out of Scope if that's really the intent.

### 7. Confirm-step rigor is inconsistent between cancellation and account overwrite
**Severity: Medium**

FR30 mandates an explicit confirm step before any cancellation (FR25/26/27). No equivalent confirm gate is required before an admin overwrites another account's password or permission level (FR18) — arguably a more destructive and more security-sensitive action than cancelling an appointment. If the intent is "protect against fat-fingering," the more dangerous action is under-protected relative to the least dangerous one.

### 8. Duplicate email, password policy, and email format validation are unspecified
**Severity: Medium**

FR1/FR2 describe registration and sign-in but never state:
- What happens when someone registers with an email already in use
- Any minimum password strength/length requirement
- Any email format validation

NFR1 covers hashing but not input validation. This is normal PRD-level detail to defer to Architecture for *mechanism*, but the *behavior* (e.g., "duplicate email registration is rejected with a clear error") is a product decision that belongs here, the same way the double-booking behavior (FR10) was spelled out.

### 9. Admin's "search for a user" (FR17) doesn't define what's searchable
**Severity: Low-Medium**

FR17 says the admin "can search for and select a user account" without specifying whether search matches name, email, partial strings, or exact match. Small in isolation, but it's exactly the kind of ambiguity that causes UX and Architecture to independently guess and disagree.

### 10. No login throttling / brute-force protection requirement
**Severity: Low-Medium**

NFR1 is otherwise reasonably specific about security (salted hashing, server-side role checks, secure sessions) but is silent on authentication attempt throttling or lockout. For a real-stakes exercise explicitly treated at full rigor, this is a live gap rather than a deferred nice-to-have — brute-forcing a weak customer password is realistic even for a small local app if it's ever exposed beyond localhost.

### 11. Timezone / "today" handling is unaddressed
**Severity: Low-Medium**

FR11 centers the barber/admin dashboard on "today," and FR7's calendar disables "past dates" — both depend on a well-defined notion of "now" and "today." The PRD never states whether dates/times are stored and compared in a single fixed timezone, UTC, or local server time, nor how client and server clocks are reconciled. A naive implementation is very likely to show the wrong day near midnight or misjudge "past" vs. "future" for slots.

### 12. Role-gating of the Accounts tab is implied by section headers, not stated as an FR
**Severity: Low-Medium**

The Goals table asserts "100% of role-gated pages/actions reject unauthenticated or wrong-role access." FR3 explicitly covers unauthenticated users being blocked from booking/barber/admin pages, but no FR explicitly states that a signed-in *barber* must be rejected from the Accounts tab / admin-only actions. It's inferable from the document's section structure ("Admin Dashboard & Account Management") but inference is not the same as a testable requirement, and this table's own metric demands verification by automated test — you can't write that test against an FR that doesn't exist.

### 13. Unfalsifiable success metric
**Severity: Low**

"A reader can locate where any given behavior lives without hunting" (Goals & Success Metrics table) has no automatable acceptance test and is entirely subjective — unlike every other row in that table, it can't be verified pass/fail. It reads more like an engineering value (which NFR6 already restates) than a measurable goal, and its presence in a table that otherwise contains rigorously falsifiable metrics weakens the table's credibility.

### 14. Unbounded forward booking window has no stated rationale or cap
**Severity: Low**

FR7 explicitly commits to "no forward-looking limit." That's a deliberate decision, which is good, but combined with Finding #2 (no shop-closed-day model), it means the schedule can accumulate bookings arbitrarily far into a future the shop may not even be operating in, with no stated business reason for why an unlimited window was chosen over, say, a rolling 60-day window typical of real scheduling apps.

### 15. Timeline realism
**Severity: Low (flagged, not blocking)**

The soft target of 2026-07-31 gives roughly nine days (from PRD creation on 2026-07-21) for a solo build covering: full auth with hashed passwords, three role-gated views, a concurrency-safe booking system with a real double-booking guard, CI wired to GitHub, and a non-mocked automated test suite (NFR4). It's explicitly marked "soft," so this isn't a spec defect, but it's worth naming out loud given how much of the above (Findings #1–#8) still needs to be resolved before Architecture can even start.

---

## Summary

The PRD is well-organized and unusually rigorous for its stated scope — the double-booking guard, the confirm-step requirement on cancellations, and the explicit non-goals list all show real discipline. The gaps found here cluster around three themes: (1) **domain modeling of "availability"** is incomplete (no closed days, no barber time-off, no "today" cutoff on slots) despite availability being the app's central promise; (2) **admin/security edge cases** (bootstrap, last-admin lockout, JWT staleness, confirm-step asymmetry) are under-specified relative to the PRD's stated security bar; and (3) a handful of **requirements are stated as capabilities without acceptance-level behavior** (duplicate email, search fields, wrong-role blocking of the Accounts tab). None of these are difficult to fix at the PRD stage — they get expensive once UX and Architecture have already built on top of the gaps.
